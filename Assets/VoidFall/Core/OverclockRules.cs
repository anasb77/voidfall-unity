using System;

namespace VoidFall.Core
{
    public static class OverclockRules
    {
        public const float StackDurationSeconds = 15f;
        public const int MaximumPowerTier = 3;

        private static readonly double[] Movement = { 1.00, 2.00, 2.30, 2.60 };
        private static readonly double[] FireRate = { 1.00, 1.35, 1.70, 2.15 };
        private static readonly double[] MusicRates = { 1.00, 2.00, 2.00, 2.00 };

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

        /// <summary>
        /// OVERCLOCKER wild card (spec 44.1): keeps a permanent tier-1 state.
        /// Called after Step whenever the card is held; never raises the tier
        /// or streak above what pickups earned - it only prevents full expiry.
        /// </summary>
        public void HoldTier1()
        {
            if (Active) return;
            PowerTier = 1;
            if (Streak < 1) Streak = 1;
            RemainingSeconds = OverclockRules.StackDurationSeconds;
        }
    }
}
