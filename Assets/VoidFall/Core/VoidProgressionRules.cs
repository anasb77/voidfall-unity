using System;

namespace VoidFall.Core
{
    /// <summary>Deterministic rules for objective-driven Void progression.</summary>
    public static class VoidProgressionRules
    {
        public const double SurvivalSeconds = 300.0;
        public const int MinimumPostBossDelaySeconds = 14;
        public const int MaximumPostBossDelaySeconds = 22;
        public const double BaseDoubleBossChance = 0.25;
        public const double DoubleBossChancePerClear = 0.06;

        public static double DoubleBossChance(int completedVoids)
        {
            return Math.Min(1.0, BaseDoubleBossChance + Math.Max(0, completedVoids) * DoubleBossChancePerClear);
        }

        public static bool ShouldSpawnDoubleBoss(uint seed, int completedVoids)
        {
            return Hash01(seed, completedVoids, 0x3c6ef372u) < DoubleBossChance(completedVoids);
        }

        public static int PostBossDelaySeconds(uint seed, int completedVoids)
        {
            const uint span = MaximumPostBossDelaySeconds - MinimumPostBossDelaySeconds + 1;
            return MinimumPostBossDelaySeconds +
                   (int)(Hash(seed, Math.Max(0, completedVoids), 0xa54ff53au) % span);
        }

        public static string SpecialBossId(string voidId)
        {
            if (string.Equals(voidId, "hydra", StringComparison.Ordinal)) return "hydra-prime";
            if (string.Equals(voidId, "monochrome-court", StringComparison.Ordinal)) return "court-grandmasters";
            return null;
        }

        private static double Hash01(uint seed, int index, uint salt)
        {
            return Hash(seed, Math.Max(0, index), salt) / ((double)uint.MaxValue + 1.0);
        }

        private static uint Hash(uint seed, int index, uint salt)
        {
            unchecked
            {
                var value = seed ^ ((uint)(index + 1) * 0x9e3779b9u) ^ salt;
                value = (value ^ (value >> 16)) * 0x7feb352du;
                value = (value ^ (value >> 15)) * 0x846ca68bu;
                return value ^ (value >> 16);
            }
        }
    }
}
