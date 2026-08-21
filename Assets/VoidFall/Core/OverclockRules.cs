using System;

namespace VoidFall.Core
{
    public static class OverclockRules
    {
        public const float StackDurationSeconds = 15f;
        public const int MaximumPowerTier = 3;

        private static readonly double[] Movement = { 1.00, 2.00, 2.30, 2.60 };
        private static readonly double[] FireRate = { 1.00, 1.35, 1.70, 2.15 };
        private static readonly double[] MusicRates = { 1.00, 1.40, 1.48, 1.56 };

        public static double MovementMultiplier(int tier) => Movement[ClampTier(tier)];
        public static double FireRateMultiplier(int tier) => FireRate[ClampTier(tier)];
        public static double MusicRate(int tier) => MusicRates[ClampTier(tier)];
        public static double CooldownMultiplier(int tier) => 1.0 / FireRateMultiplier(tier);

        private static int ClampTier(int tier) => Math.Max(0, Math.Min(MaximumPowerTier, tier));
    }

    public struct OverclockState
    {
        public int PowerTier { get; private set; }
        public int Streak { get; private set; }
        public float RemainingSeconds { get; private set; }
        public bool Active => PowerTier > 0 && RemainingSeconds > 0f;

        public void ApplyPickup()
        {
            if (!Active)
            {
                PowerTier = 1;
                Streak = 1;
                RemainingSeconds = OverclockRules.StackDurationSeconds;
                return;
            }

            PowerTier = Math.Min(OverclockRules.MaximumPowerTier, PowerTier + 1);
            Streak++;
            RemainingSeconds += OverclockRules.StackDurationSeconds;
        }

        public void Step(float simulationSeconds)
        {
            if (!Active || simulationSeconds <= 0f) return;
            RemainingSeconds = Math.Max(0f, RemainingSeconds - simulationSeconds);
            if (RemainingSeconds > 0f) return;
            Reset();
        }

        public void Reset()
        {
            PowerTier = 0;
            Streak = 0;
            RemainingSeconds = 0f;
        }
    }
}
