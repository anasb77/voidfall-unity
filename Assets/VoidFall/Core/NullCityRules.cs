using System;

namespace VoidFall.Core
{
    public enum NullCityCycle
    {
        Surveillance,
        Lockdown,
    }

    public enum MotherloadMove
    {
        Cannons,
        Tractor,
        Brood,
        Bombardment,
        Vent,
    }

    public readonly struct NullCityPurge
    {
        public readonly bool Visible;
        public readonly bool Active;
        public readonly int Lane;
        public readonly double X;
        public readonly double Y;
        public readonly double Width;
        public readonly double Height;
        public readonly double WarningRemaining;

        public NullCityPurge(
            bool visible,
            bool active,
            int lane,
            double x,
            double y,
            double width,
            double height,
            double warningRemaining)
        {
            Visible = visible;
            Active = active;
            Lane = lane;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            WarningRemaining = warningRemaining;
        }
    }

    public static class NullCityRules
    {
        public const double SurveillanceSeconds = 22;
        public const double LockdownSeconds = 24;
        public const double CycleSeconds = SurveillanceSeconds + LockdownSeconds;

        public const double PurgeBeatSeconds = 6;
        public const double PurgeWarningSeconds = 2.4;
        public const double PurgeVisibleSeconds = 4.3;
        public const double PurgePlayerDamage = 18;
        public const double PurgeEnemyDamagePerSecond = 125;

        public const double TractorWarnSeconds = 1.8;
        public const double TractorActiveSeconds = 4;
        public const double TractorPullSpeed = 125;
        public const double TractorMinDistance = 145;
        public const double TractorMaxDistance = 640;
        public const double TractorHalfAngleRadians = 0.38;

        public const double MotherloadRecoverySeconds = 1.6;
        public const double MotherloadVentSeconds = 4;
        public const double MotherloadBodyRadius = 114;
        public const double NormalIncomingDamageMultiplier = 1;
        public const double BossIncomingDamageMultiplier = 2;

        public const double ArenaLeft = 180;
        public const double ArenaRight = 1420;
        public const double ArenaTop = 220;
        public const double ArenaBottom = 746;

        private const double HorizontalLaneWidth = 1240;
        private const double HorizontalLaneHeight = 68;
        private const double VerticalLaneY = 218;
        private const double VerticalLaneWidth = 54;
        private const double VerticalLaneHeight = 527;
        private const double TimeEpsilon = 0.000000001;

        public static NullCityCycle CycleAt(double elapsed, bool bossActive)
        {
            if (bossActive) return NullCityCycle.Lockdown;
            return NormalCycleElapsed(elapsed) < SurveillanceSeconds
                ? NullCityCycle.Surveillance
                : NullCityCycle.Lockdown;
        }

        public static double CycleProgress(double elapsed, bool bossActive)
        {
            var safeElapsed = SafeElapsed(elapsed);
            if (bossActive) return safeElapsed % LockdownSeconds / LockdownSeconds;

            var cycleElapsed = safeElapsed % CycleSeconds;
            return cycleElapsed < SurveillanceSeconds
                ? cycleElapsed / SurveillanceSeconds
                : (cycleElapsed - SurveillanceSeconds) / LockdownSeconds;
        }

        public static NullCityPurge PurgeAt(double elapsed, bool bossActive)
        {
            var safeElapsed = SafeElapsed(elapsed);
            double lockdownElapsed;
            long pass;

            if (bossActive)
            {
                pass = (long)Math.Floor(safeElapsed / LockdownSeconds);
                lockdownElapsed = safeElapsed % LockdownSeconds;
            }
            else
            {
                var cycleElapsed = safeElapsed % CycleSeconds;
                if (cycleElapsed < SurveillanceSeconds) return UnavailablePurge();
                pass = (long)Math.Floor(safeElapsed / CycleSeconds);
                lockdownElapsed = cycleElapsed - SurveillanceSeconds;
            }

            var beat = (int)Math.Floor(lockdownElapsed / PurgeBeatSeconds);
            if (beat < 0 || beat > 3) return UnavailablePurge();

            var beatElapsed = lockdownElapsed % PurgeBeatSeconds;
            if (beatElapsed - PurgeVisibleSeconds > TimeEpsilon) return UnavailablePurge();

            var active = beatElapsed + TimeEpsilon >= PurgeWarningSeconds;
            var warningRemaining = active ? 0 : PurgeWarningSeconds - beatElapsed;

            switch (beat)
            {
                case 0:
                    return new NullCityPurge(true, active, beat, ArenaLeft, 311,
                        HorizontalLaneWidth, HorizontalLaneHeight, warningRemaining);
                case 1:
                    return new NullCityPurge(true, active, beat, ArenaLeft, 558,
                        HorizontalLaneWidth, HorizontalLaneHeight, warningRemaining);
                case 2:
                    return new NullCityPurge(true, active, beat, pass % 2 == 0 ? 956 : 1030,
                        VerticalLaneY, VerticalLaneWidth, VerticalLaneHeight, warningRemaining);
                default:
                    return new NullCityPurge(true, active, beat, 488,
                        VerticalLaneY, VerticalLaneWidth, VerticalLaneHeight, warningRemaining);
            }
        }

        public static bool IsInsideTractor(double dx, double dy, double aimRadians)
        {
            if (!IsFinite(dx) || !IsFinite(dy) || !IsFinite(aimRadians)) return false;

            var cosine = Math.Cos(aimRadians);
            var sine = Math.Sin(aimRadians);
            var forward = dx * cosine + dy * sine;
            if (forward <= TractorMinDistance || forward >= TractorMaxDistance) return false;

            var side = Math.Abs(-dx * sine + dy * cosine);
            return side < forward * Math.Tan(TractorHalfAngleRadians);
        }

        public static MotherloadMove NextMotherloadMove(int moveIndex)
        {
            var normalized = moveIndex % 5;
            if (normalized < 0) normalized += 5;
            return (MotherloadMove)normalized;
        }

        private static double NormalCycleElapsed(double elapsed) => SafeElapsed(elapsed) % CycleSeconds;

        private static double SafeElapsed(double elapsed) =>
            IsFinite(elapsed) ? Math.Max(0, elapsed) : 0;

        private static bool IsFinite(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value);

        private static NullCityPurge UnavailablePurge() =>
            new NullCityPurge(false, false, -1, 0, 0, 0, 0, 0);
    }
}
