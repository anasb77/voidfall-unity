using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using VoidFall.Core;
using VoidFall.UI;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private enum ClaimKind { WeaponRank, SupportRank, Parts, WildCard, PowerUp }

        private sealed class PendingRouletteClaim
        {
            public ClaimKind Kind;
            public int Index, FromRank, ToRank, Parts, Score;
            public WildCardId WildCard;
            public string Title, Detail, Id;
            public RouletteTier Tier;
        }

        [Serializable]
        private sealed class RouletteClaimTelemetry
        {
            public int cardIndex, cardCount, fromRank, toRank, partsBefore, partsAfter;
            public string rewardId, title, decision;
        }

        private readonly List<PendingRouletteClaim> _rouletteClaims = new List<PendingRouletteClaim>(2);
        private int _rouletteClaimIndex, _rouletteClaimGeneration;
        private bool _rouletteClaimBusy;
        private RouletteTelemetryDetail _rouletteClaimTelemetry;

        private void ResetRouletteClaims()
        {
            // Unity objects can retain a managed reference after native teardown.
            // Use Unity's null check rather than ?. during runtime destruction.
            if (_rouletteClaims.Count > 0 && _ui != null && _ui.LevelUp != null) _ui.LevelUp.SetVisible(false);
            _rouletteClaims.Clear();
            _rouletteClaimIndex = 0;
            _rouletteClaimGeneration++;
            _rouletteClaimBusy = false;
            _rouletteClaimTelemetry = null;
            if (_ui != null && _ui.PrizeReveal != null) _ui.PrizeReveal.SetVisible(false);
        }

        private void BeginRouletteClaims(RouletteSession session)
        {
            ResetRouletteClaims();
            _rouletteClaimTelemetry = BuildRouletteTelemetryDetail(session);
            var detail = _rouletteClaimTelemetry;
            detail.buildBefore = BuildTelemetryProgress();
            detail.partsBefore = _partsEarned;
            detail.scoreBefore = _score;
            detail.healthBefore = _gameSim.Player.Health;
            detail.maxHealthBefore = _gameSim.Player.MaxHealth;
            detail.revivesBefore = _revivesRemaining;
            detail.wildCardsBefore = RouletteTelemetryWildCards();
            // Wagers were committed by the spin. Settle them at landing even
            // if the run is later abandoned between individual reward claims.
            _partsEarned = Math.Max(0, _partsEarned - session.PartsSpent + session.PartsRefunded);
            PrepareRouletteClaims(session.Result);
            _rouletteActive = false;
            _prizeRevealActive = true;
            _paused = true;
            ShowRouletteClaim();
        }

        private void PrepareRouletteClaims(RouletteWedgeDefinition prize)
        {
            if (prize == null || _upgradeProgress == null)
            {
                AddPartsClaim(40, RouletteTier.Mediocre, "No eligible reward remains. Receive 40 Parts instead.");
                return;
            }
            switch (prize.Kind)
            {
                case RoulettePrizeKind.WeaponUpgradeQuality: PrepareOwnedRankClaims(2, true, false, prize.Tier); break;
                case RoulettePrizeKind.SupportUpgradeQuality: PrepareOwnedRankClaims(2, false, true, prize.Tier); break;
                case RoulettePrizeKind.UpgradeRandomOwned: PrepareOwnedRankClaims(1, false, false, prize.Tier); break;
                case RoulettePrizeKind.NewRandomCard: PrepareNewCardClaim(prize.Tier); break;
                case RoulettePrizeKind.Parts: AddPartsClaim(RouletteRules.PartsReward(prize.Tier), prize.Tier); break;
                // Keep the legacy serialized kind; its actual reward is now Parts.
                case RoulettePrizeKind.RareBoon: AddPartsClaim(RouletteRules.BonusPartsReward, prize.Tier); break;
                case RoulettePrizeKind.WildCard:
                    var choices = new List<WildCardId>();
                    foreach (WildCardId id in Enum.GetValues(typeof(WildCardId)))
                        if (id != WildCardId.None && !HasWildCard(id) && WildCardRules.IsImplemented(id)) choices.Add(id);
                    if (choices.Count == 0)
                    {
                        AddPartsClaim(80, RouletteTier.Premium, "Every Wild Card is already owned. Receive 80 Parts and 750 score.");
                        _rouletteClaims[_rouletteClaims.Count - 1].Score = 750;
                    }
                    else
                    {
                        var chosen = choices[_rouletteRng.Int(choices.Count)];
                        _rouletteClaims.Add(new PendingRouletteClaim { Kind = ClaimKind.WildCard, WildCard = chosen,
                            Id = chosen.ToString(), Title = WildCardRules.DisplayName(chosen),
                            Detail = WildCardRules.Description(chosen), Tier = prize.Tier });
                    }
                    break;
                case RoulettePrizeKind.PowerUp:
                    var room = CanPlaceSpecialPickup();
                    if (!room) AddPartsClaim(40, RouletteTier.Mediocre, "The ground is full. Receive 40 Parts instead of a power-up drop.");
                    else _rouletteClaims.Add(new PendingRouletteClaim { Kind = ClaimKind.PowerUp, Id = "random_power_up",
                        Title = "Random Power-Up Drop", Detail = "Claim to place a random power-up at your feet. Collect it when play resumes.", Tier = prize.Tier });
                    break;
            }
            if (_rouletteClaims.Count == 0) AddPartsClaim(40, RouletteTier.Mediocre, "No eligible reward remains. Receive 40 Parts instead.");
        }

        private void AddPartsClaim(int amount, RouletteTier tier, string detail = null)
        {
            _rouletteClaims.Add(new PendingRouletteClaim { Kind = ClaimKind.Parts, Parts = amount, Tier = tier,
                Id = "parts", Title = amount + " Parts", Detail = detail ?? "Add " + amount + " Parts to your run earnings for the Workshop." });
        }

        private void PrepareOwnedRankClaims(int ranks, bool weaponsOnly, bool supportsOnly, RouletteTier tier)
        {
            var weapons = new List<int>();
            var supports = new List<int>();
            for (var index = 0; index < _upgradeProgress.WeaponRanks.Length; index++)
                if (_upgradeProgress.WeaponRanks[index] > 0 && _upgradeProgress.WeaponRanks[index] < ProgressionRules.MaxWeaponRank) weapons.Add(index);
            var definitions = ExtendedCatalog.AllSupports();
            for (var index = 0; index < _upgradeProgress.SupportRanks.Length; index++)
                if (_upgradeProgress.SupportRanks[index] > 0 && _upgradeProgress.SupportRanks[index] < definitions[index].MaxRank) supports.Add(index);
            // Same selection order as the existing grant path; UI never re-rolls.
            var useWeapon = !supportsOnly && weapons.Count > 0 &&
                (weaponsOnly || _rouletteRng.Int(weapons.Count + supports.Count) < weapons.Count);
            if (useWeapon) AddRankClaims(true, weapons[_rouletteRng.Int(weapons.Count)], ranks, tier);
            else if (!weaponsOnly && supports.Count > 0) AddRankClaims(false, supports[_rouletteRng.Int(supports.Count)], ranks, tier);
            else AddPartsClaim(40, RouletteTier.Mediocre, "No owned card can gain another rank. Receive 40 Parts instead.");
        }

        private void PrepareNewCardClaim(RouletteTier tier)
        {
            var weapons = new List<int>();
            var supports = new List<int>();
            var owned = 0;
            foreach (var rank in _upgradeProgress.WeaponRanks) if (rank > 0) owned++;
            if (owned < UpgradeRules.WeaponSlotLimit(_upgradeProgress))
                for (var index = 0; index < _upgradeProgress.WeaponRanks.Length; index++)
                    if (_upgradeProgress.WeaponRanks[index] <= 0) weapons.Add(index);
            for (var index = 0; index < _upgradeProgress.SupportRanks.Length; index++)
                if (_upgradeProgress.SupportRanks[index] <= 0) supports.Add(index);
            if (weapons.Count + supports.Count == 0)
            {
                AddPartsClaim(40, RouletteTier.Mediocre, "No new card is available. Receive 40 Parts instead.");
                return;
            }
            var chosen = _rouletteRng.Int(weapons.Count + supports.Count);
            if (chosen < weapons.Count) AddRankClaims(true, weapons[chosen], 1, tier);
            else AddRankClaims(false, supports[chosen - weapons.Count], 1, tier);
        }

        private void AddRankClaims(bool weapon, int index, int count, RouletteTier tier)
        {
            var current = weapon ? _upgradeProgress.WeaponRanks[index] : _upgradeProgress.SupportRanks[index];
            var support = weapon ? null : ExtendedCatalog.AllSupports()[index];
            var maximum = weapon ? ProgressionRules.MaxWeaponRank : support.MaxRank;
            for (var offset = 0; offset < count && current + offset < maximum; offset++)
            {
                var from = current + offset;
                var to = from + 1;
                var card = weapon ? ContentCatalog.Weapons[index] : null;
                var description = weapon ? DescribeClaimWeaponRank(card, from, to)
                    : support.Descriptions != null && support.Descriptions.Length >= to ? support.Descriptions[to - 1] : "Gain the next support rank.";
                _rouletteClaims.Add(new PendingRouletteClaim { Kind = weapon ? ClaimKind.WeaponRank : ClaimKind.SupportRank,
                    Index = index, FromRank = from, ToRank = to, Tier = tier, Id = weapon ? card.Id : support.Id,
                    Title = weapon ? card.Name : support.Name, Detail = description });
            }
        }

        private static string DescribeClaimWeaponRank(WeaponDefinition weapon, int from, int to)
        {
            var text = new StringBuilder(weapon.Summary ?? string.Empty);
            if (weapon.Ranks == null || weapon.Ranks.Length < to) return text.ToString();
            var next = weapon.Ranks[to - 1].Stats;
            var previous = from > 0 ? weapon.Ranks[from - 1].Stats : null;
            if (previous == null || previous.Damage != next.Damage)
                text.Append("\nDamage: ").Append(previous == null ? "" : previous.Damage.ToString("0.#") + " → ").Append(next.Damage.ToString("0.#"));
            if (previous == null || previous.Cooldown != next.Cooldown)
                text.Append("\nAttack interval: ").Append(previous == null ? "" : previous.Cooldown.ToString("0.##") + "s → ").Append(next.Cooldown.ToString("0.##")).Append('s');
            if (previous != null && previous.ProjectileCount != next.ProjectileCount)
                text.Append("\nProjectiles: ").Append(previous.ProjectileCount).Append(" → ").Append(next.ProjectileCount);
            return text.ToString();
        }

        private void ShowRouletteClaim()
        {
            var card = _rouletteClaims[_rouletteClaimIndex];
            var index = _rouletteClaimIndex;
            var generation = _rouletteClaimGeneration;
            var ranked = card.Kind == ClaimKind.WeaponRank || card.Kind == ClaimKind.SupportRank;
            var maximum = card.Kind == ClaimKind.WeaponRank ? ProgressionRules.MaxWeaponRank
                : card.Kind == ClaimKind.SupportRank ? ExtendedCatalog.AllSupports()[card.Index].MaxRank : 0;
            var accent = card.Kind == ClaimKind.WeaponRank ? ContentCatalog.Weapons[card.Index].Accent
                : card.Kind == ClaimKind.SupportRank ? ExtendedCatalog.AllSupports()[card.Index].Accent : null;
            var data = new UpgradeCardData
            {
                Title = card.Title,
                Category = card.Kind == ClaimKind.WeaponRank ? "Weapon" : card.Kind == ClaimKind.SupportRank ? "Support"
                    : card.Kind == ClaimKind.WildCard ? "Wild Card" : "Reward",
                Description = card.Detail,
                LevelText = ranked ? (card.FromRank == 0 ? "NEW · RANK 1" : "RANK " + card.FromRank + " → " + card.ToRank) : string.Empty,
                CurrentRank = card.FromRank, MaxRank = maximum,
                AccentColor = ParseColor(accent, card.Kind == ClaimKind.WildCard ? new Color(.94f, .64f, .47f) : UITheme.CyanLight)
            };
            _ui.SetScreen(UIScreen.LevelUp);
            _ui.LevelUp.ShowReward(data, () => ClaimRouletteReward(generation, index),
                card.Kind == ClaimKind.WildCard ? (Action)(() => DeclineRouletteReward(generation, index)) : null,
                index + 1, _rouletteClaims.Count);
            RecordRunHistory("roulette_claim_presented", card.Id, sourceId: "roulette",
                instanceId: _rouletteCeremoniesSeen + 1, detail: JsonUtility.ToJson(new RouletteClaimTelemetry {
                    cardIndex = index + 1, cardCount = _rouletteClaims.Count, fromRank = card.FromRank,
                    toRank = card.ToRank, rewardId = card.Id, title = card.Title, decision = "offered" }));
        }

        private void DeclineRouletteReward(int generation, int index)
        {
            if (_gameOver || !_prizeRevealActive || _rouletteSession == null || generation != _rouletteClaimGeneration ||
                index != _rouletteClaimIndex || index >= _rouletteClaims.Count || _rouletteClaimBusy) return;
            var card = _rouletteClaims[index];
            if (card.Kind != ClaimKind.WildCard) return;
            // Leaving ends this offer: no effect, compensation or replacement roll.
            _rouletteClaimIndex++;
            RecordRunHistory("roulette_declined", card.Id, sourceId: "roulette", instanceId: _rouletteCeremoniesSeen + 1,
                progress: BuildTelemetryProgress(), detail: JsonUtility.ToJson(new RouletteClaimTelemetry {
                    cardIndex = index + 1, cardCount = _rouletteClaims.Count, rewardId = card.Id, title = card.Title,
                    decision = "leave", partsBefore = _partsEarned, partsAfter = _partsEarned }));
            FinalizeRouletteClaims(_rouletteSession, new RoulettePrizeReveal(card.Title, "Wild Card left without granting its effects.", card.Tier),
                _rouletteClaimTelemetry, false);
        }

        private void ClaimRouletteReward(int generation, int index)
        {
            if (_gameOver || !_prizeRevealActive || _rouletteSession == null || generation != _rouletteClaimGeneration ||
                index != _rouletteClaimIndex || index >= _rouletteClaims.Count || _rouletteClaimBusy) return;
            _rouletteClaimBusy = true;
            var card = _rouletteClaims[index];
            var before = _partsEarned;
            var previousSource = _telemetryRewardSource;
            var previousParent = _telemetryRewardParent;
            try
            {
                _telemetryRewardSource = "roulette";
                _telemetryRewardParent = _rouletteCeremoniesSeen + 1;
                switch (card.Kind)
                {
                    case ClaimKind.WeaponRank: ApplyWeaponRanks(card.Index, 1); break;
                    case ClaimKind.SupportRank: ApplyCardRanks(card.Index, 1); break;
                    case ClaimKind.Parts: _partsEarned += card.Parts; _score += card.Score; break;
                    case ClaimKind.WildCard: ActivateWildCard(card.WildCard, false); break;
                    case ClaimKind.PowerUp: SpawnRarePickup(_gameSim.Player.Position); break;
                }
                RecordRunHistory("roulette_claimed", card.Id, sourceId: "roulette", instanceId: _rouletteCeremoniesSeen + 1,
                    amount: card.Kind == ClaimKind.WeaponRank || card.Kind == ClaimKind.SupportRank ? 1 : _partsEarned - before,
                    hp: _gameSim.Player.Health, maxHp: _gameSim.Player.MaxHealth, progress: BuildTelemetryProgress(),
                    detail: JsonUtility.ToJson(new RouletteClaimTelemetry { cardIndex = index + 1, cardCount = _rouletteClaims.Count,
                        fromRank = card.FromRank, toRank = card.ToRank, partsBefore = before, partsAfter = _partsEarned,
                        rewardId = card.Id, title = card.Title, decision = "take" }));
                _rouletteClaimIndex++;
            }
            finally
            {
                _telemetryRewardSource = previousSource;
                _telemetryRewardParent = previousParent;
                _rouletteClaimBusy = false;
            }
            if (_rouletteClaimIndex < _rouletteClaims.Count) ShowRouletteClaim();
            else
            {
                var title = card.Title;
                var detail = card.Detail;
                if (_rouletteClaims.Count > 1) { title += " +" + _rouletteClaims.Count; detail = _rouletteClaims.Count + " ranks claimed."; }
                FinalizeRouletteClaims(_rouletteSession, new RoulettePrizeReveal(title, detail, card.Tier), _rouletteClaimTelemetry);
            }
        }
    }
}
