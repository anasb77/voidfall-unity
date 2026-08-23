using System;
using System.Collections.Generic;
using UnityEngine;
using VoidFall.Core;
using VoidFall.UI;

namespace VoidFall.Runtime
{
    /// <summary>
    /// Boss Roulette live wiring (spec section 43). The ceremony opens when an
    /// encounter's last boss dies and pauses the run exactly like a level-up
    /// prompt. Prizes apply through the same upgrade-progress state the
    /// level-up flow uses; the ceremony's deterministic Rng stream is seeded
    /// from the run seed and the boss kill count, so replays reproduce both
    /// the wheel result and every purchased table modification.
    /// </summary>
    public sealed partial class VoidFallGameRuntime
    {
        private RouletteSession _rouletteSession;
        private Rng _rouletteRng;
        private bool _rouletteActive;

        // Per-run ceremony history: drives the luck pity (each ceremony
        // tilts the next table upward) and the repeat protection.
        private int _rouletteCeremoniesSeen;
        private RoulettePrizeKind _rouletteLastKind;
        private RouletteTier _rouletteLastTier;
        private bool _rouletteHasLast;

        private void ResetRouletteLuck()
        {
            _rouletteCeremoniesSeen = 0;
            _rouletteHasLast = false;
        }

        private void OpenBossRoulette()
        {
            if (_ui == null || _gameOver || _revivePending || _rouletteActive) return;
            _rouletteRng = new Rng(_runSeed ^ ((uint)_bossKills * 0x9e3779b9u));
            _rouletteSession = new RouletteSession(
                _runSeed,
                _bossKills,
                RouletteRules.ApplyLuck(
                    RouletteRules.DefaultTable(), _rouletteCeremoniesSeen));
            _rouletteActive = true;
            _paused = true;
            _ui.Roulette.CeremonyComplete -= OnRouletteComplete;
            _ui.Roulette.CeremonyComplete += OnRouletteComplete;
            _ui.SetScreen(UIScreen.Roulette);
            _ui.Roulette.Present(
                _rouletteSession,
                _rouletteRng,
                Mathf.Max(0, _partsEarned),
                new RouletteSpinContext
                {
                    CeremoniesSeen = _rouletteCeremoniesSeen,
                    ProtectionsEnabled = true,
                    HasPrevious = _rouletteHasLast,
                    PreviousKind = _rouletteLastKind,
                    PreviousTier = _rouletteLastTier,
                });
        }

        private void OnRouletteComplete(RouletteSession session)
        {
            if (_ui != null) _ui.Roulette.CeremonyComplete -= OnRouletteComplete;
            if (session != null)
            {
                ApplyRoulettePrize(session);
                // Refunded wagers were returned by the Void while keeping the
                // effect, so only the net spend leaves the run economy.
                var netSpend = session.PartsSpent - session.PartsRefunded;
                _partsEarned = Math.Max(0, _partsEarned - netSpend);
                if (session.Result != null)
                {
                    _rouletteLastKind = session.Result.Kind;
                    _rouletteLastTier = session.Result.Tier;
                    _rouletteHasLast = true;
                }
                _rouletteCeremoniesSeen++;
            }
            _rouletteSession = null;
            _rouletteRng = null;
            _rouletteActive = false;
            _paused = false;
            _ui?.SetScreen(UIScreen.None);
        }

        private void ApplyRoulettePrize(RouletteSession session)
        {
            var prize = session.Result;
            if (prize == null || _upgradeProgress == null) return;
            switch (prize.Kind)
            {
                case RoulettePrizeKind.PowerUp:
                    // A gift materializes at the player's feet; a rare pickup
                    // keeps it exciting without new spawn plumbing.
                    SpawnRarePickup(_gameSim.Player.Position);
                    break;
                case RoulettePrizeKind.Parts:
                    _partsEarned += 60;
                    ShowArenaToast("+60 Parts", 2.5f, ToastKind.Reward);
                    break;
                case RoulettePrizeKind.UpgradeRandomOwned:
                    GrantRandomOwnedRank(session, 1);
                    break;
                case RoulettePrizeKind.NewRandomCard:
                    GrantNewCardRank(session);
                    break;
                case RoulettePrizeKind.WeaponUpgradeQuality:
                    GrantRandomOwnedRank(session, 2, weaponsOnly: true);
                    break;
                case RoulettePrizeKind.SupportUpgradeQuality:
                    GrantRandomOwnedRank(session, 2, supportsOnly: true);
                    break;
                case RoulettePrizeKind.RareBoon:
                    _gameSim.Player.Health = _gameSim.Player.MaxHealth;
                    _score += 500;
                    ShowArenaToast("Rare boon - integrity restored", 2.5f, ToastKind.Reward);
                    break;
                case RoulettePrizeKind.WildCard:
                    if (!TryGrantRandomWildCard(session))
                    {
                        // Every implemented card is already held: cash out.
                        _partsEarned += 80;
                        _score += 750;
                        ShowArenaToast("Wild card cashes out early", 2.5f, ToastKind.Reward);
                    }
                    break;
            }
        }

        /// <summary>
        /// Grants <paramref name="ranks"/> to one random owned card inside the
        /// requested families, clamped at its max rank. Uniform pick via the
        /// ceremony's Rng; falls back to Parts when nothing qualifies.
        /// </summary>
        private void GrantRandomOwnedRank(
            RouletteSession session,
            int ranks,
            bool weaponsOnly = false,
            bool supportsOnly = false)
        {
            var weaponCandidates = new List<int>();
            var supportCandidates = new List<int>();
            for (var index = 0; index < _upgradeProgress.WeaponRanks.Length; index++)
            {
                var rank = _upgradeProgress.WeaponRanks[index];
                if (rank > 0 && rank < ProgressionRules.MaxWeaponRank) weaponCandidates.Add(index);
            }
            for (var index = 0; index < _upgradeProgress.SupportRanks.Length; index++)
            {
                var rank = _upgradeProgress.SupportRanks[index];
                if (rank > 0 && rank < ContentCatalog.Supports[index].MaxRank) supportCandidates.Add(index);
            }

            var useWeapon = !supportsOnly && weaponCandidates.Count > 0 &&
                (supportsOnly || _rouletteRng.Int(weaponCandidates.Count + supportCandidates.Count) < weaponCandidates.Count);
            if (useWeapon)
            {
                var index = weaponCandidates[_rouletteRng.Int(weaponCandidates.Count)];
                var applied = ApplyWeaponRanks(index, ranks);
                ShowArenaToast(
                    ContentCatalog.Weapons[index].Name + " +" + applied,
                    2.5f, ToastKind.Reward);
                return;
            }
            if (!weaponsOnly && supportCandidates.Count > 0)
            {
                var index = supportCandidates[_rouletteRng.Int(supportCandidates.Count)];
                var applied = ApplyCardRanks(index, ranks);
                ShowArenaToast(
                    ContentCatalog.Supports[index].Name + " +" + applied,
                    2.5f, ToastKind.Reward);
                return;
            }

            _partsEarned += 40;
            ShowArenaToast("Nothing left to upgrade - +40 Parts", 2.5f, ToastKind.Reward);
        }

        private void GrantNewCardRank(RouletteSession session)
        {
            var weaponCandidates = new List<int>();
            var supportCandidates = new List<int>();
            for (var index = 0; index < _upgradeProgress.WeaponRanks.Length; index++)
            {
                if (_upgradeProgress.WeaponRanks[index] <= 0) weaponCandidates.Add(index);
            }
            for (var index = 0; index < _upgradeProgress.SupportRanks.Length; index++)
            {
                if (_upgradeProgress.SupportRanks[index] <= 0) supportCandidates.Add(index);
            }

            var total = weaponCandidates.Count + supportCandidates.Count;
            if (total == 0)
            {
                _partsEarned += 40;
                ShowArenaToast("Every card owned - +40 Parts", 2.5f, ToastKind.Reward);
                return;
            }

            var pick = _rouletteRng.Int(total);
            if (pick < weaponCandidates.Count)
            {
                var index = weaponCandidates[pick];
                _upgradeProgress.WeaponRanks[index] = 1;
                RefreshCachedRanks();
                ShowArenaToast("New card: " + ContentCatalog.Weapons[index].Name, 2.5f, ToastKind.Reward);
            }
            else
            {
                var index = supportCandidates[pick - weaponCandidates.Count];
                _upgradeProgress.SupportRanks[index] = 1;
                RefreshCachedRanks();
                ShowArenaToast("New card: " + ContentCatalog.Supports[index].Name, 2.5f, ToastKind.Reward);
            }
        }

        private int ApplyCardRanks(int supportIndex, int ranks)
        {
            var max = ContentCatalog.Supports[supportIndex].MaxRank;
            var next = Mathf.Clamp(_upgradeProgress.SupportRanks[supportIndex] + ranks, 0, max);
            var applied = next - _upgradeProgress.SupportRanks[supportIndex];
            _upgradeProgress.SupportRanks[supportIndex] = next;
            RefreshCachedRanks();
            return applied;
        }

        private int ApplyWeaponRanks(int weaponIndex, int ranks)
        {
            var next = Mathf.Clamp(
                _upgradeProgress.WeaponRanks[weaponIndex] + ranks,
                0,
                ProgressionRules.MaxWeaponRank);
            var applied = next - _upgradeProgress.WeaponRanks[weaponIndex];
            _upgradeProgress.WeaponRanks[weaponIndex] = next;
            RefreshCachedRanks();
            return applied;
        }

        private void RefreshCachedRanks()
        {
            _pistolRank = _upgradeProgress.WeaponRanks.Length > 0
                ? _upgradeProgress.WeaponRanks[0]
                : 0;
            _calibrationRank = SupportRank("calibration");
        }
    }
}