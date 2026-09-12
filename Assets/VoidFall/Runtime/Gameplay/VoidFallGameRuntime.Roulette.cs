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
        private bool _prizeRevealActive;
        private bool _roulettePendingAfterRevive;

        // Per-run ceremony history: drives the luck pity (each ceremony
        // tilts the next table upward) and the repeat protection.
        private int _rouletteCeremoniesSeen;
        private RoulettePrizeKind _rouletteLastKind;
        private RouletteTier _rouletteLastTier;
        private bool _rouletteHasLast;
        private int _rouletteTelemetryPartsSpent;
        private int _rouletteTelemetryPartsRefunded;
        private int _rouletteTelemetryImproveUses;

        [Serializable]
        private sealed class RouletteTelemetryWedge
        {
            public int index;
            public string kind, tier, name;
            public double weight, probability;
        }

        [Serializable]
        private sealed class RouletteTelemetryDetail
        {
            public uint seed;
            public int bossIndex, ceremonyIndex, ceremoniesSeen, resultIndex;
            public bool protectionsEnabled, hasPrevious;
            public string previousKind, previousTier, resultKind, resultTier;
            public int partsSpent, partsRefunded, improveOddsUses, raiseStakesUses, availableParts;
            public RouletteTelemetryWedge[] wedges;
            public string[] log;
            public UnityTelemetryProgress buildBefore, buildAfter;
            public int partsBefore, partsAfter, scoreBefore, scoreAfter, revivesBefore, revivesAfter;
            public float healthBefore, healthAfter, maxHealthBefore, maxHealthAfter;
            public string[] wildCardsBefore, wildCardsAfter;
            public string grantedTitle, grantedDetail, grantedTier;
        }

        private RouletteTelemetryDetail BuildRouletteTelemetryDetail(RouletteSession session)
        {
            var context = new RouletteSpinContext
            {
                CeremoniesSeen = _rouletteCeremoniesSeen,
                ProtectionsEnabled = true,
                HasPrevious = _rouletteHasLast,
                PreviousKind = _rouletteLastKind,
                PreviousTier = _rouletteLastTier,
            };
            var wedges = new RouletteTelemetryWedge[session.Wedges.Length];
            for (var i = 0; i < wedges.Length; i++)
            {
                var wedge = session.Wedges[i];
                wedges[i] = new RouletteTelemetryWedge
                {
                    index = i, kind = wedge.Kind.ToString(), tier = wedge.Tier.ToString(),
                    name = wedge.Name, weight = wedge.Weight,
                    probability = RoulettePresentationRules.Probability(session.Wedges, i, context),
                };
            }
            var log = new string[session.Log.Count];
            for (var i = 0; i < log.Length; i++) log[i] = session.Log[i];
            return new RouletteTelemetryDetail
            {
                seed = session.Seed, bossIndex = session.BossIndex,
                ceremonyIndex = _rouletteCeremoniesSeen + 1, ceremoniesSeen = _rouletteCeremoniesSeen,
                protectionsEnabled = true, hasPrevious = _rouletteHasLast,
                previousKind = _rouletteHasLast ? _rouletteLastKind.ToString() : null,
                previousTier = _rouletteHasLast ? _rouletteLastTier.ToString() : null,
                resultIndex = session.ResultIndex, resultKind = session.Result?.Kind.ToString(),
                resultTier = session.Result?.Tier.ToString(), partsSpent = session.PartsSpent,
                partsRefunded = session.PartsRefunded, improveOddsUses = session.ImproveOddsUses,
                raiseStakesUses = session.RaiseStakesUses,
                availableParts = Math.Max(0, _partsEarned - session.PartsSpent + session.PartsRefunded),
                wedges = wedges, log = log,
            };
        }

        private string[] RouletteTelemetryWildCards()
        {
            var cards = new string[_activeWildCards.Count];
            var index = 0;
            foreach (var card in _activeWildCards) cards[index++] = card.ToString();
            Array.Sort(cards, StringComparer.Ordinal);
            return cards;
        }

        private void RecordRouletteSpin(RouletteSession session)
        {
            if (!_rouletteActive || session != _rouletteSession || session == null || !session.Spun) return;
            RecordRunHistory("roulette_rolled", session.Result?.Kind.ToString(), sourceId: "roulette",
                instanceId: _rouletteCeremoniesSeen + 1, detail: JsonUtility.ToJson(BuildRouletteTelemetryDetail(session)));
        }

        private void ResetRouletteLuck()
        {
            ResetRouletteClaims();
            if (_ui?.Roulette != null) _ui.Roulette.CeremonyComplete -= OnRouletteComplete;
            UnbindRouletteAudio();
            _rouletteActive = false;
            _prizeRevealActive = false;
            _rouletteSession = null;
            _rouletteRng = null;
            _rouletteCeremoniesSeen = 0;
            _rouletteHasLast = false;
            _roulettePendingAfterRevive = false;
            _rouletteTelemetryPartsSpent = 0;
            _rouletteTelemetryPartsRefunded = 0;
            _rouletteTelemetryImproveUses = 0;
        }

        private void OpenBossRoulette()
        {
            if (_ui == null || _gameOver || _revivePending || _rouletteActive || _prizeRevealActive) return;
            if (_gameSim.Player.Health <= 0)
            {
                // The boss fell as the player fell. Defer the ceremony
                // until the revive question resolves.
                _roulettePendingAfterRevive = true;
                return;
            }
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
            UnbindRouletteAudio();
            _ui.Roulette.Tick += PlayRouletteTick;
            _ui.Roulette.WagerChanged += PlayRouletteWager;
            _ui.Roulette.Landed += PlayRouletteLanding;
            _ui.Roulette.Spun += RecordRouletteSpin;
            _rouletteTelemetryPartsSpent = 0;
            _rouletteTelemetryPartsRefunded = 0;
            _rouletteTelemetryImproveUses = 0;
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
            RecordRunHistory("roulette_opened", sourceId: "roulette", instanceId: _rouletteCeremoniesSeen + 1,
                detail: JsonUtility.ToJson(BuildRouletteTelemetryDetail(_rouletteSession)));
        }

        private void OnRouletteComplete(RouletteSession session)
        {
            if (!_rouletteActive || session != _rouletteSession || session == null || !session.Spun) return;
            if (_ui != null) _ui.Roulette.CeremonyComplete -= OnRouletteComplete;
            UnbindRouletteAudio();
            BeginRouletteClaims(session);
        }

        private void FinalizeRouletteClaims(RouletteSession session, RoulettePrizeReveal granted, RouletteTelemetryDetail detail, bool rewardTaken = true)
        {
            if (session != _rouletteSession || session == null) return;
            {
                // Net wager spend was settled at landing; claims only add rewards.
                detail.buildAfter = BuildTelemetryProgress();
                detail.partsAfter = _partsEarned;
                detail.scoreAfter = _score;
                detail.healthAfter = _gameSim.Player.Health;
                detail.maxHealthAfter = _gameSim.Player.MaxHealth;
                detail.revivesAfter = _revivesRemaining;
                detail.wildCardsAfter = RouletteTelemetryWildCards();
                detail.grantedTitle = granted.Title;
                detail.grantedDetail = granted.Detail;
                detail.grantedTier = granted.Tier.ToString();
                RecordRunHistory(rewardTaken ? "roulette_granted" : "roulette_completed", session.Result?.Kind.ToString(), sourceId: "roulette",
                    instanceId: _rouletteCeremoniesSeen + 1, amount: _partsEarned - detail.partsBefore,
                    hp: _gameSim.Player.Health, maxHp: _gameSim.Player.MaxHealth,
                    detail: JsonUtility.ToJson(detail), progress: detail.buildAfter);
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
            _prizeRevealActive = false;
            _openRouteAfterRoulette = false;
            _paused = _applicationInactive || _levelUpActive;
            ResetRouletteClaims();
            if (_levelUpActive && _levelOptions != null)
                _ui?.LevelUp?.ShowUpgrades(BuildUpgradeCards(_levelOptions), _rerollsRemaining, SelectLevelOption);
            // Every card was claimed. Continue the same escape window
            // without a second confirmation or resetting its remaining active time.
            SyncUiScreen();
        }

        private void ClosePrizeReveal()
        {
            if (_rouletteClaims.Count > 0) return; // Only the displayed Claim action can grant a pending reward.
            if (!_prizeRevealActive) return;
            _prizeRevealActive = false;
            _paused = false;
            if (_openRouteAfterRoulette)
            {
                _openRouteAfterRoulette = false;
                _voidCompletionPending = true;
                // A manually collected reward must not skip the grace period/countdown.
                StepVoidCompletionDelay(0f);
                return;
            }
            _paused = false;
            _ui?.SetScreen(UIScreen.None);
        }

        private readonly struct RoulettePrizeReveal
        {
            public RoulettePrizeReveal(string title, string detail, RouletteTier tier)
            {
                Title = title;
                Detail = detail;
                Tier = tier;
            }

            public string Title { get; }
            public string Detail { get; }
            public RouletteTier Tier { get; }
        }

        /// <summary>
        /// Applies the won prize and returns what the reveal card should
        /// say. Presentation is the card screen's job - nothing here pops a
        /// toast.
        /// </summary>
        private RoulettePrizeReveal ApplyRoulettePrize(RouletteSession session)
        {
            var prize = session.Result;
            if (prize == null || _upgradeProgress == null)
                return new RoulettePrizeReveal("NOTHING", "The Void kept its prize.", RouletteTier.Mediocre);
            switch (prize.Kind)
            {
                case RoulettePrizeKind.PowerUp:
                    // A gift materializes at the player's feet; a rare pickup
                    // keeps it exciting without new spawn plumbing.
                    SpawnRarePickup(_gameSim.Player.Position);
                    return new RoulettePrizeReveal(
                        "VOID GIFT",
                        "A powerful pickable materializes at your feet. Go take it.",
                        prize.Tier);
                case RoulettePrizeKind.Parts:
                    var amount = RouletteRules.PartsReward(prize.Tier);
                    _partsEarned += amount;
                    return new RoulettePrizeReveal(
                        "PARTS CACHE", "+" + amount + " Parts earned for the Workshop.", prize.Tier);
                case RoulettePrizeKind.UpgradeRandomOwned:
                {
                    var (applied, name) = GrantRandomOwnedRank(1);
                    return OwnedRankReveal(prize, applied, name);
                }
                case RoulettePrizeKind.NewRandomCard:
                {
                    var (granted, name) = GrantNewCardRank();
                    return granted
                        ? new RoulettePrizeReveal(name, "A new card joins your arsenal.", prize.Tier)
                        : new RoulettePrizeReveal("EVERY CARD OWNED", "+40 Parts instead.", RouletteTier.Mediocre);
                }
                case RoulettePrizeKind.WeaponUpgradeQuality:
                {
                    var (applied, name) = GrantRandomOwnedRank(2, weaponsOnly: true);
                    return OwnedRankReveal(prize, applied, name);
                }
                case RoulettePrizeKind.SupportUpgradeQuality:
                {
                    var (applied, name) = GrantRandomOwnedRank(2, supportsOnly: true);
                    return OwnedRankReveal(prize, applied, name);
                }
                case RoulettePrizeKind.RareBoon:
                    _partsEarned += RouletteRules.BonusPartsReward;
                    return new RoulettePrizeReveal(
                        "500 Parts", "+500 Parts earned for the Workshop.", prize.Tier);
                case RoulettePrizeKind.WildCard:
                {
                    if (TryGrantRandomWildCard(session, out var granted, announce: false))
                        return new RoulettePrizeReveal(
                            WildCardRules.DisplayName(granted),
                            "A rule-breaking card bends the run.",
                            RouletteTier.Legendary);
                    _partsEarned += 80;
                    _score += 750;
                    return new RoulettePrizeReveal(
                        "WILD CARD CASHES OUT", "Every card is already held: +80 Parts, +750 score.",
                        RouletteTier.Premium);
                }
                default:
                    return new RoulettePrizeReveal(prize.Name, prize.Description + ".", prize.Tier);
            }
        }

        private static RoulettePrizeReveal OwnedRankReveal(
            RouletteWedgeDefinition prize, int applied, string name)
        {
            return applied > 0
                ? new RoulettePrizeReveal(
                    name + " +" + applied,
                    applied + (applied == 1 ? " rank" : " ranks") + " applied to " + name + ".",
                    prize.Tier)
                : new RoulettePrizeReveal(
                    "NOTHING LEFT TO UPGRADE", "+40 Parts instead.", RouletteTier.Mediocre);
        }

        /// <summary>
        /// Grants <paramref name="ranks"/> to one random owned card inside the
        /// requested families, clamped at its max rank. Uniform pick via the
        /// ceremony's Rng; (0, null) when nothing qualifies - the caller
        /// shapes the reveal (and the Parts fallback) from that.
        /// </summary>
        private (int Applied, string Name) GrantRandomOwnedRank(
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
                if (rank > 0 && rank < ExtendedCatalog.AllSupports()[index].MaxRank) supportCandidates.Add(index);
            }

            var useWeapon = !supportsOnly && weaponCandidates.Count > 0 &&
                (weaponsOnly || _rouletteRng.Int(weaponCandidates.Count + supportCandidates.Count) < weaponCandidates.Count);
            if (useWeapon)
            {
                var index = weaponCandidates[_rouletteRng.Int(weaponCandidates.Count)];
                var applied = ApplyWeaponRanks(index, ranks);
                return (applied, ContentCatalog.Weapons[index].Name);
            }
            if (!weaponsOnly && supportCandidates.Count > 0)
            {
                var index = supportCandidates[_rouletteRng.Int(supportCandidates.Count)];
                var applied = ApplyCardRanks(index, ranks);
                return (applied, ExtendedCatalog.AllSupports()[index].Name);
            }

            _partsEarned += 40;
            return (0, null);
        }

        private (bool Granted, string Name) GrantNewCardRank()
        {
            var weaponCandidates = new List<int>();
            var supportCandidates = new List<int>();
            var ownedWeapons = 0;
            foreach (var rank in _upgradeProgress.WeaponRanks) if (rank > 0) ownedWeapons++;
            var hasWeaponSlot = ownedWeapons < UpgradeRules.WeaponSlotLimit(_upgradeProgress);
            for (var index = 0; index < _upgradeProgress.WeaponRanks.Length; index++)
            {
                if (hasWeaponSlot && _upgradeProgress.WeaponRanks[index] <= 0) weaponCandidates.Add(index);
            }
            for (var index = 0; index < _upgradeProgress.SupportRanks.Length; index++)
            {
                if (_upgradeProgress.SupportRanks[index] <= 0) supportCandidates.Add(index);
            }

            var total = weaponCandidates.Count + supportCandidates.Count;
            if (total == 0)
            {
                _partsEarned += 40;
                return (false, null);
            }

            var pick = _rouletteRng.Int(total);
            if (pick < weaponCandidates.Count)
            {
                var index = weaponCandidates[pick];
                _upgradeProgress.WeaponRanks[index] = 1;
                RefreshCachedRanks();
                return (true, ContentCatalog.Weapons[index].Name);
            }
            else
            {
                var index = supportCandidates[pick - weaponCandidates.Count];
                _upgradeProgress.SupportRanks[index] = 1;
                RefreshCachedRanks();
                return (true, ExtendedCatalog.AllSupports()[index].Name);
            }
        }

        private int ApplyCardRanks(int supportIndex, int ranks)
        {
            var max = ExtendedCatalog.AllSupports()[supportIndex].MaxRank;
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
            var previousMaxHealth = _gameSim.Player.MaxHealth;
            RecalculatePlayerStats(false);
            if (_gameSim.Player.MaxHealth > previousMaxHealth)
                _gameSim.Player.Health = Mathf.Min(_gameSim.Player.MaxHealth, _gameSim.Player.Health + _gameSim.Player.MaxHealth - previousMaxHealth);
        }

        private void PlayRouletteTick() => _audio?.Play(ProceduralAudio.Cue.Ui, 0.3f);
        private void PlayRouletteWager()
        {
            _audio?.Play(ProceduralAudio.Cue.Currency, 0.65f);
            if (!_rouletteActive || _rouletteSession == null) return;
            var session = _rouletteSession;
            var purchase = session.ImproveOddsUses > _rouletteTelemetryImproveUses ? "improve_odds" : "raise_stakes";
            var cost = session.PartsSpent - _rouletteTelemetryPartsSpent;
            var refund = session.PartsRefunded - _rouletteTelemetryPartsRefunded;
            RecordRunHistory("roulette_purchase", purchase, reason: refund > 0 ? "refunded" : "paid",
                sourceId: "roulette", instanceId: _rouletteCeremoniesSeen + 1, amount: cost - refund,
                detail: JsonUtility.ToJson(BuildRouletteTelemetryDetail(session)));
            _rouletteTelemetryPartsSpent = session.PartsSpent;
            _rouletteTelemetryPartsRefunded = session.PartsRefunded;
            _rouletteTelemetryImproveUses = session.ImproveOddsUses;
        }

        private void PlayRouletteLanding()
        {
            _audio?.Play(ProceduralAudio.Cue.LevelUp, 0.8f);
            if (!_rouletteActive || _rouletteSession == null) return;
            RecordRunHistory("roulette_landed", _rouletteSession.Result?.Kind.ToString(), sourceId: "roulette",
                instanceId: _rouletteCeremoniesSeen + 1);
        }

        private void UnbindRouletteAudio()
        {
            if (_ui?.Roulette == null) return;
            _ui.Roulette.Tick -= PlayRouletteTick;
            _ui.Roulette.WagerChanged -= PlayRouletteWager;
            _ui.Roulette.Landed -= PlayRouletteLanding;
            _ui.Roulette.Spun -= RecordRouletteSpin;
        }
    }
}
