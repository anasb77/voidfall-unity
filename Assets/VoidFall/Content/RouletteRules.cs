using System;
using System.Collections.Generic;

namespace VoidFall.Core
{
    /// <summary>What a landed roulette wedge grants. Spec section 43.2.</summary>
    public enum RoulettePrizeKind
    {
        PowerUp,
        UpgradeRandomOwned,
        NewRandomCard,
        WeaponUpgradeQuality,
        SupportUpgradeQuality,
        Parts,
        WildCard,
        RareBoon,
    }

    /// <summary>
    /// Reward quality band. Improve Odds removes/upgrades the bottom band;
    /// Raise Stakes widens the top bands. The wheel visibly mixes desirable
    /// and mediocre results, so tiers are part of the presentation.
    /// </summary>
    public enum RouletteTier
    {
        Mediocre,
        Standard,
        Premium,
        Legendary,
    }

    public sealed class RouletteWedgeDefinition
    {
        public RouletteWedgeDefinition(
            RoulettePrizeKind kind,
            RouletteTier tier,
            double weight,
            string name,
            string description,
            string accent)
        {
            Kind = kind;
            Tier = tier;
            Weight = weight;
            Name = name;
            Description = description;
            Accent = accent;
        }

        public RoulettePrizeKind Kind { get; }
        public RouletteTier Tier { get; }
        public double Weight { get; private set; }
        public string Name { get; }
        public string Description { get; }
        public string Accent { get; }

        public void ScaleWeight(double multiplier)
        {
            Weight = Math.Max(0, Weight * (IsFinite(multiplier) ? Math.Abs(multiplier) : 1));
        }

        /// <summary>
        /// Improve Odds conversion: a low-quality wedge becomes its standard
        /// counterpart. The identity stays on the wheel so the player can still
        /// read what was rescued away.
        /// </summary>
        public void UpgradeTier()
        {
            if (Tier == RouletteTier.Mediocre) TierPromote(RouletteTier.Standard);
            else if (Tier == RouletteTier.Standard) TierPromote(RouletteTier.Premium);
            else if (Tier == RouletteTier.Premium) TierPromote(RouletteTier.Legendary);
        }

        private void TierPromote(RouletteTier tier)
        {
            SetTier(tier);
            // Promotion alone would make promoted wedges strictly better than
            // native wedges of the new tier; the small trim keeps the table's
            // overall shape recognizable after two purchases.
            Weight *= 0.8;
        }

        internal void SetTier(RouletteTier tier)
        {
            TierField = tier;
        }

        // Backing field trick: the public property is read-only by convention
        // like the rest of the content definitions, while the rules engine
        // needs controlled mutation for purchased table modifications.
        private RouletteTier TierField
        {
            get => Tier;
            set => SetTierBacking(value);
        }

        partial void SetTierBacking(RouletteTier value);

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}