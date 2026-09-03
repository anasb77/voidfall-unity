using System;

namespace VoidFall.Runtime
{
    /// <summary>Small, allocation-free constants shared by combat simulation and presentation.</summary>
    public static class CombatTweakRules
    {
        public const int MatriarchBodyguardCount = 8;
        public const float MatriarchBodyguardOrbitRadius = 108f;
        public const float MatriarchBodyguardOrbitSpeed = 4.8f;
        public const float MatriarchSummonLaunchSpeed = 480f;

        public static double RusherPreviewAlpha(double sourceAlpha) =>
            Math.Max(0.0, Math.Min(1.0, sourceAlpha * 0.8));

        public static double RangedPreviewAlpha(double sourceAlpha) =>
            Math.Max(0.0, Math.Min(1.0, sourceAlpha * 0.75));

        public static double StandardEliteSpinMultiplier(bool moving) => moving ? 5.5 : 1.0;

        public static double MatriarchBodyguardOrbitAngle(double elapsedSeconds, int slot)
        {
            var wrappedSlot = ((slot % MatriarchBodyguardCount) + MatriarchBodyguardCount) %
                MatriarchBodyguardCount;
            return Math.Max(0.0, elapsedSeconds) * MatriarchBodyguardOrbitSpeed +
                   wrappedSlot / (double)MatriarchBodyguardCount * Math.PI * 2.0;
        }

        public static double WardenRushRotationDegrees(double remainingSeconds, double durationSeconds)
        {
            var duration = Math.Max(0.0001, durationSeconds);
            var progress = 1.0 - Math.Max(0.0, Math.Min(duration, remainingSeconds)) / duration;
            return progress * 1440.0;
        }

        public static string RosterTwoRusherAccent() => "#a855f7";

        public static bool ShowStandardEliteOverlay() => false;
    }
}
