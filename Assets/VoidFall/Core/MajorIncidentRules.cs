using System;

namespace VoidFall.Core
{
    public enum MajorIncidentKind { None, BlackHole, DestroyerRaid, Eclipse }
    public enum MajorIncidentPhase { None, Warning, Active, Release }

    /// <summary>One incident clock. Elapsed and Remaining span the entire incident.</summary>
    public sealed class MajorIncidentState
    {
        public MajorIncidentKind Kind { get; private set; }
        public MajorIncidentPhase Phase { get; private set; }
        public double Elapsed { get; private set; }
        public double Remaining => Kind == MajorIncidentKind.None
            ? 0 : Math.Max(0, MajorIncidentRules.TotalDuration(Kind) - Elapsed);
        public double Strength { get; private set; }

        public void Begin(MajorIncidentKind kind)
        {
            Reset();
            if (MajorIncidentRules.TotalDuration(kind) <= 0) return;
            Kind = kind;
            Phase = MajorIncidentPhase.Warning;
        }

        public void Step(double dt)
        {
            if (Kind == MajorIncidentKind.None || !MajorIncidentRules.IsFinite(dt) || dt <= 0) return;
            // Compare before adding so even a finite double.MaxValue step cannot overflow.
            if (dt >= Remaining)
            {
                Reset();
                return;
            }
            Elapsed += dt;
            var activeElapsed = Elapsed - MajorIncidentRules.WarningSeconds;
            if (activeElapsed < 0)
            {
                Phase = MajorIncidentPhase.Warning;
                Strength = 0;
            }
            else if (activeElapsed < MajorIncidentRules.ActiveDuration(Kind))
            {
                Phase = MajorIncidentPhase.Active;
                Strength = MajorIncidentRules.Smooth01(activeElapsed / MajorIncidentRules.ActivationSeconds);
            }
            else
            {
                Phase = MajorIncidentPhase.Release;
                Strength = MajorIncidentRules.Smooth01(Remaining / MajorIncidentRules.ReleaseDuration(Kind));
            }
        }

        public void Reset()
        {
            Kind = MajorIncidentKind.None;
            Phase = MajorIncidentPhase.None;
            Elapsed = 0;
            Strength = 0;
        }
    }

    public static class MajorIncidentRules
    {
        public const double WarningSeconds = 2.5;
        public const double ActivationSeconds = 1.5;
        public const double BossLeadSeconds = 15;

        public static bool IsArenaEligible(string arenaId)
        {
            return string.Equals(arenaId, "abyss", StringComparison.Ordinal) ||
                string.Equals(arenaId, "void", StringComparison.Ordinal);
        }

        public static bool CanBegin(MajorIncidentKind kind, double survivalSecondsRemaining,
            bool bossActive, bool safeState, bool otherIncidentActive)
        {
            var duration = TotalDuration(kind);
            return duration > 0 && !bossActive && !safeState && !otherIncidentActive &&
                IsFinite(survivalSecondsRemaining) && survivalSecondsRemaining >= duration + BossLeadSeconds;
        }

        public static double ActiveDuration(MajorIncidentKind kind)
        {
            switch (kind)
            {
                case MajorIncidentKind.BlackHole: return 10;
                case MajorIncidentKind.DestroyerRaid: return 32;
                case MajorIncidentKind.Eclipse: return 19.5;
                default: return 0;
            }
        }

        public static double ReleaseDuration(MajorIncidentKind kind)
        {
            switch (kind)
            {
                case MajorIncidentKind.BlackHole: return 1.5;
                case MajorIncidentKind.DestroyerRaid: return 3;
                case MajorIncidentKind.Eclipse: return 2;
                default: return 0;
            }
        }

        public static double TotalDuration(MajorIncidentKind kind)
        {
            var active = ActiveDuration(kind);
            return active <= 0 ? 0 : WarningSeconds + active + ReleaseDuration(kind);
        }

        public static double BlackHolePullScale(double distance, double radius, double strength)
        {
            if (!IsFinite(distance) || !IsFinite(radius) || !IsFinite(strength) ||
                radius <= 0 || distance <= 0 || distance >= radius) return 0;
            var u = distance / radius;
            return 4 * u * (1 - u) * Math.Max(0, Math.Min(1, strength));
        }

        internal static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        internal static double Smooth01(double value)
        {
            var t = Math.Max(0, Math.Min(1, value));
            return t * t * (3 - 2 * t);
        }
    }
}
