using System;

namespace VoidFall.Core
{
    public enum CourtFaction
    {
        Black,
        White,
    }

    public enum CourtHazardStage
    {
        Warning,
        Burning,
        Recovery,
    }

    public enum CourtLine
    {
        Rank,
        File,
        DiagonalRise,
        DiagonalFall,
    }

    public readonly struct MonochromePoint : IEquatable<MonochromePoint>
    {
        public MonochromePoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }
        public double Y { get; }

        public bool Equals(MonochromePoint other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is MonochromePoint other && Equals(other);
        public override int GetHashCode()
        {
            unchecked { return (X.GetHashCode() * 397) ^ Y.GetHashCode(); }
        }
        public override string ToString() => "(" + X + ", " + Y + ")";
        public static bool operator ==(MonochromePoint left, MonochromePoint right) => left.Equals(right);
        public static bool operator !=(MonochromePoint left, MonochromePoint right) => !left.Equals(right);
    }

    public readonly struct CourtHazardState : IEquatable<CourtHazardState>
    {
        public CourtHazardState(CourtFaction faction, CourtHazardStage stage)
        {
            Faction = faction;
            Stage = stage;
        }

        public CourtFaction Faction { get; }
        public CourtHazardStage Stage { get; }

        public bool Equals(CourtHazardState other) =>
            Faction == other.Faction && Stage == other.Stage;

        public override bool Equals(object obj) => obj is CourtHazardState other && Equals(other);
        public override int GetHashCode() => ((int)Faction * 397) ^ (int)Stage;
        public override string ToString() => Faction + " " + Stage;
        public static bool operator ==(CourtHazardState left, CourtHazardState right) => left.Equals(right);
        public static bool operator !=(CourtHazardState left, CourtHazardState right) => !left.Equals(right);
    }

    /// <summary>
    /// Engine-free board and encounter rules for Monochrome Court. Presentation and
    /// pooled entity state remain in the Unity runtime partial.
    /// </summary>
    public static class MonochromeEncounterRules
    {
        public const double HazardWarningSeconds = 0.9;
        public const double HazardBurningSeconds = 2.2;
        public const double HazardRecoverySeconds = 0.5;
        public const double PhaseTwoHazardWarningSeconds = 0.7;
        public const double PhaseTwoHazardBurningSeconds = 2.4;

        public static MonochromePoint KnightCorner(
            double startX,
            double startY,
            double targetX,
            double targetY,
            bool horizontalFirst)
        {
            return horizontalFirst
                ? new MonochromePoint(targetX, startY)
                : new MonochromePoint(startX, targetY);
        }

        public static CourtLine[] QueenAttackLines(int cycleIndex) => new[]
        {
            CourtLine.Rank,
            CourtLine.File,
            (cycleIndex & 1) == 0 ? CourtLine.DiagonalRise : CourtLine.DiagonalFall,
        };

        public static CourtHazardState HazardAt(double elapsedSeconds, bool phaseTwo)
        {
            const double boundaryEpsilon = 1e-9;
            var elapsed = double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds)
                ? 0
                : Math.Max(0, elapsedSeconds);
            var warning = phaseTwo ? PhaseTwoHazardWarningSeconds : HazardWarningSeconds;
            var burning = phaseTwo ? PhaseTwoHazardBurningSeconds : HazardBurningSeconds;
            var pulse = warning + burning + HazardRecoverySeconds;
            var pulseIndex = (int)Math.Floor(elapsed / pulse);
            var cursor = elapsed % pulse;
            var stage = cursor + boundaryEpsilon < warning
                ? CourtHazardStage.Warning
                : cursor + boundaryEpsilon < warning + burning
                    ? CourtHazardStage.Burning
                    : CourtHazardStage.Recovery;
            var faction = (pulseIndex & 1) == 0 ? CourtFaction.White : CourtFaction.Black;
            return new CourtHazardState(faction, stage);
        }

        public static bool IsTileDangerous(CourtHazardState hazard, CourtFaction tileFaction) =>
            hazard.Stage == CourtHazardStage.Burning && hazard.Faction == tileFaction;
    }
}
