using System;
namespace VoidFall.Core
{
    public static class CrascendoRules
    {
        public const float GrowthPerHit = .2f;
        public const float MaximumGrowth = 5f;
        public const int HitsToMaximum = 20;
        public static float Growth(int hits) => 1f + Math.Min(HitsToMaximum, Math.Max(0, hits)) * GrowthPerHit;
        public static float Intensity(double elapsed, bool bossOrReward) => bossOrReward ? 1f : (float)Math.Max(0, Math.Min(1, elapsed / VoidProgressionRules.SurvivalSeconds));
        public static float Blend(float intensity) { var t = Math.Max(0, Math.Min(1, intensity)); t = t < .5f ? t * 2 : (t - .5f) * 2; return t * t * (3 - 2 * t); }
    }
}
