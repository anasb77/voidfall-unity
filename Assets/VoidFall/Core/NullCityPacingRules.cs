namespace VoidFall.Core
{
    /// <summary>
    /// Native Null City pacing targets. These are authored encounter bounds,
    /// rather than a player-DPS refill controller.
    /// </summary>
    public static class NullCityPacingRules
    {
        public const int Version = 1;
        public const float QuietOrdinaryArrivalsPerSecond = 5f;
        public const float LockdownOrdinaryArrivalsPerSecond = 9f;
        public const int QuietActiveTarget = 44;
        public const int LockdownActiveTarget = 72;
        public const int QuietActiveCap = 50;
        public const int LockdownActiveCap = 90;
        public const float FirstHeavySeconds = 90f;
        public const float SecondHeavySeconds = 180f;
        public const float HeavyCooldownSeconds = 17f;
        public const int MaximumPolicePerWave = 9;

        public static bool IsLockdown(bool bossActive, bool cycleLockdown) => bossActive || cycleLockdown;

        public static float OrdinaryInterval(bool lockdown) =>
            1f / (lockdown ? LockdownOrdinaryArrivalsPerSecond : QuietOrdinaryArrivalsPerSecond);

        public static int ActiveTarget(bool lockdown) => lockdown ? LockdownActiveTarget : QuietActiveTarget;

        public static int ActiveCap(bool lockdown) => lockdown ? LockdownActiveCap : QuietActiveCap;

        public static int HeavyLimit(float elapsedSeconds, bool bossActive)
        {
            if (bossActive) return 2;
            if (elapsedSeconds >= SecondHeavySeconds) return 2;
            return elapsedSeconds >= FirstHeavySeconds ? 1 : 0;
        }

        public static bool IsHeavy(string id) =>
            id == "null-gunship" || id == "null-mech" || id == "null-broodmother";
    }
}
