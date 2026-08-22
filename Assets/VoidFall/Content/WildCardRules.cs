namespace VoidFall.Core
{
    /// <summary>
    /// Implemented run-only Wild Cards (spec section 44). Each is unique per
    /// run; unimplemented spec cards are deliberately absent so the roulette
    /// never grants a card that does nothing.
    /// </summary>
    public enum WildCardId
    {
        None = 0,
        Standstill,
        Greed,
        SecondLife,
        Overclocker,
    }

    public static class WildCardRules
    {
        // 44.2 STANDSTILL: deal double damage while stationary. The stance
        // activates after the player holds still for this long, inside the
        // spec's recommended 0.35-0.5 second window.
        public const double StandstillDamageMultiplier = 2.0;
        public const double StandstillActivationSeconds = 0.4;

        // 44.3 GREED: double XP from all sources; pickup magnet disabled.
        public const int GreedXpMultiplier = 2;

        // 44.6 SECOND LIFE: one extra revive at half health.
        public const int SecondLifeBonusRevives = 1;

        public static bool StandstillActive(double stationarySeconds)
        {
            var seconds = IsFinite(stationarySeconds) ? stationarySeconds : 0;
            return seconds >= StandstillActivationSeconds;
        }

        public static bool IsImplemented(WildCardId id)
        {
            return id == WildCardId.Standstill ||
                id == WildCardId.Greed ||
                id == WildCardId.SecondLife ||
                id == WildCardId.Overclocker;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}