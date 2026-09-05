using System;

namespace VoidFall.Core
{
    /// <summary>Player-facing reward facts and wheel geometry. Never consumes random draws.</summary>
    public static class RoulettePresentationRules
    {
        public static double Probability(RouletteWedgeDefinition[] table, int index, RouletteSpinContext context)
        {
            if (table == null || index < 0 || index >= table.Length) return 0;
            double total = 0, protectedWeight = 0;
            foreach (var wedge in table)
            {
                total += Math.Max(0, wedge.Weight);
                if (NeedsResample(wedge, context)) protectedWeight += Math.Max(0, wedge.Weight);
            }
            if (total <= 0) return 1d / table.Length;
            var probability = Math.Max(0, table[index].Weight) / total;
            // A protected outcome gets one re-sample, including the possibility of itself.
            return probability * (protectedWeight / total + (NeedsResample(table[index], context) ? 0 : 1));
        }

        private static bool NeedsResample(RouletteWedgeDefinition wedge, RouletteSpinContext context)
        {
            return context.ProtectionsEnabled &&
                ((context.CeremoniesSeen == 0 && wedge.Tier == RouletteTier.Mediocre) ||
                 (context.HasPrevious && wedge.Kind == context.PreviousKind && wedge.Tier <= RouletteTier.Standard));
        }

        public static double StartDegrees(RouletteWedgeDefinition[] table, int index, RouletteSpinContext context)
        {
            double start = 0;
            for (var i = 0; i < index; i++) start += Probability(table, i, context) * 360;
            return start;
        }

        public static double CentreDegrees(RouletteWedgeDefinition[] table, int index, RouletteSpinContext context)
            => StartDegrees(table, index, context) + Probability(table, index, context) * 180;

        public static string Effect(RouletteWedgeDefinition wedge)
        {
            switch (wedge.Kind)
            {
                case RoulettePrizeKind.Parts: return "+" + RouletteRules.PartsReward(wedge.Tier) + " Parts for the Workshop";
                case RoulettePrizeKind.UpgradeRandomOwned: return "+1 rank to a random owned card";
                case RoulettePrizeKind.NewRandomCard: return "A random unowned weapon or support, rank 1";
                case RoulettePrizeKind.WeaponUpgradeQuality: return "+2 ranks to a random owned weapon";
                case RoulettePrizeKind.SupportUpgradeQuality: return "+2 ranks to a random owned support";
                case RoulettePrizeKind.PowerUp: return "A rare power-up drops at your feet";
                case RoulettePrizeKind.RareBoon: return "Restore all integrity and gain 500 score";
                case RoulettePrizeKind.WildCard: return "Gain one random, unowned Wild Card";
                default: return wedge.Description;
            }
        }

        public static string ShortEffect(RouletteWedgeDefinition wedge)
        {
            switch (wedge.Kind)
            {
                case RoulettePrizeKind.Parts: return "+" + RouletteRules.PartsReward(wedge.Tier);
                case RoulettePrizeKind.UpgradeRandomOwned: return "+1 RANK";
                case RoulettePrizeKind.NewRandomCard: return "NEW CARD";
                case RoulettePrizeKind.WeaponUpgradeQuality:
                case RoulettePrizeKind.SupportUpgradeQuality: return "+2 RANKS";
                case RoulettePrizeKind.PowerUp: return "POWER-UP";
                case RoulettePrizeKind.RareBoon: return "RESTORE";
                default: return "WILD";
            }
        }

        public static string Fallback(RoulettePrizeKind kind)
        {
            switch (kind)
            {
                case RoulettePrizeKind.UpgradeRandomOwned:
                case RoulettePrizeKind.WeaponUpgradeQuality:
                case RoulettePrizeKind.SupportUpgradeQuality: return "Ranks stop at the cap. No eligible card: +40 Parts.";
                case RoulettePrizeKind.NewRandomCard: return "Every card owned: +40 Parts instead.";
                case RoulettePrizeKind.WildCard: return "Every Wild Card held: +80 Parts and 750 score.";
                default: return string.Empty;
            }
        }
    }
}
