using System;

namespace VoidFall.Core
{
    /// <summary>Cosmetic rules shared by the HUD and music frame; never changes combat state.</summary>
    public static class OverclockPresentationRules
    {
        public static float StackScale(int streak) => 1f + Math.Max(0, streak - 1) * 0.10f;

        public static float PulseGain(int streak) => 1.5f + Math.Max(0, streak - 1) * 0.20f;

        public static float PulseScale(int streak, float beat)
            => 1f + Math.Max(0f, Math.Min(1f, beat)) *
                (0.014f + Math.Max(1, streak) * 0.006f) * PulseGain(streak);

        public static float ChargeFraction(float remaining, int streak)
            => Math.Max(0f, Math.Min(1f, remaining /
                (Math.Max(1, streak) * OverclockRules.StackDurationSeconds)));
    }
}
