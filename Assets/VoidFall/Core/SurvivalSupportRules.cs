using System;

namespace VoidFall.Core
{
    public static class SurvivalSupportRules
    {
        public const string Version = "2026-09-26-survival-v1";
        public const double OrdinaryScrapChance = .02;
        public const int KillThreshold = 200;
        public const int ScrapThreshold = 200;
        public const float ScrapShieldGrant = 5;
        public const float InitialShieldCapacity = 20;

        public static float LifeStealHealing(int rank)
        {
            switch (rank)
            {
                case 1: return .2f;
                case 2: return .4f;
                case 3: return .5f;
                case 4: return .7f;
                default: return rank >= 5 ? .9f : 0;
            }
        }

        public static double ScrapChance(int rank)
            => OrdinaryScrapChance * (1 + .05 * Math.Max(0, Math.Min(4, rank)));

        // A larger grant can establish a larger pool; repeated small grants cannot
        // keep raising the ceiling. Future sources may specify a higher capacity.
        public static float GrantShield(ref float current, ref float capacity, float amount, float minimumCapacity = 0)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0) return 0;
            capacity = Math.Max(InitialShieldCapacity, Math.Max(capacity, Math.Max(amount, minimumCapacity)));
            var before = current;
            current = Math.Min(capacity, Math.Max(0, current) + amount);
            return Math.Max(0, current - before);
        }

        // Width is capacity, fill is current HP. Clamp only at the available HUD
        // space so large health builds cannot overlap the central run timer.
        public static float HealthBarWidth(float maxHealth, float maximumWidth)
            => Math.Min(maximumWidth, 19f * Math.Max(1, maxHealth) / 100f);
    }
}
