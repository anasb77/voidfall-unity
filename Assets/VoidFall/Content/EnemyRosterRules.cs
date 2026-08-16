using System;

namespace VoidFall.Core
{
    public enum EnemyRoster
    {
        One = 1,
        Two = 2,
    }

    public static class EnemyRosterRules
    {
        public const double RosterTwoStartSeconds = 9 * 60;
        public const double RosterTwoFullPressureSeconds = 15 * 60;
        public const double RosterTwoInitialShare = 0.15;
        public const double RosterTwoMaxShare = 0.75;
        public const double RosterTwoHealthMultiplier = 1.3;
        public const double RosterTwoSpeedMultiplier = 1.06;
        public const double RosterTwoDamageMultiplier = 1.12;
        public const double RosterTwoCooldownMultiplier = 0.82;
        public const double RosterTwoRadiusMultiplier = 1.08;
        public const double RosterTwoXpMultiplier = 1.15;
        public const double RosterTwoThreatMultiplier = 1.55;

        private static readonly string[] RosterTwoTypes =
        {
            "chaser",
            "gunner",
            "exploder",
            "guard",
        };

        public static double RosterSpawnRoll(uint seed, int spawnId)
        {
            var safeSpawnId = Math.Max(1, spawnId);
            unchecked
            {
                var mixed = Mix32(seed ^ ((uint)safeSpawnId * 0x9e3779b9u));
                return mixed / 4294967296.0;
            }
        }

        public static bool RosterTwoEligible(string type)
        {
            for (var index = 0; index < RosterTwoTypes.Length; index++)
            {
                if (RosterTwoTypes[index] == type) return true;
            }

            return false;
        }

        public static bool RosterTwoEligible(EnemyId type)
        {
            return RosterTwoEligible(EnemyIdName(type));
        }

        public static double RosterTwoShare(double elapsedSeconds)
        {
            var elapsed = IsFinite(elapsedSeconds) ? Math.Max(0, elapsedSeconds) : 0;
            if (elapsed < RosterTwoStartSeconds) return 0;
            var progress = Math.Min(
                1,
                (elapsed - RosterTwoStartSeconds) /
                    (RosterTwoFullPressureSeconds - RosterTwoStartSeconds));
            return RosterTwoInitialShare + (RosterTwoMaxShare - RosterTwoInitialShare) * progress;
        }

        public static EnemyRoster EnemyRosterForSpawn(string type, double elapsedSeconds, double roll)
        {
            if (!RosterTwoEligible(type)) return EnemyRoster.One;
            var safeRoll = IsFinite(roll) ? Math.Min(1, Math.Max(0, roll)) : 1;
            return safeRoll < RosterTwoShare(elapsedSeconds) ? EnemyRoster.Two : EnemyRoster.One;
        }

        public static EnemyRoster EnemyRosterForSpawn(EnemyId type, double elapsedSeconds, double roll)
        {
            return EnemyRosterForSpawn(EnemyIdName(type), elapsedSeconds, roll);
        }

        public static double RosterCooldownSeconds(double seconds, EnemyRoster roster)
        {
            return Math.Max(0, seconds * (roster == EnemyRoster.Two ? RosterTwoCooldownMultiplier : 1));
        }

        private static uint Mix32(uint value)
        {
            unchecked
            {
                value = (value ^ (value >> 16)) * 0x7feb352du;
                value = (value ^ (value >> 15)) * 0x846ca68bu;
                return value ^ (value >> 16);
            }
        }

        private static string EnemyIdName(EnemyId type)
        {
            switch (type)
            {
                case EnemyId.Chaser: return "chaser";
                case EnemyId.Gunner: return "gunner";
                case EnemyId.Exploder: return "exploder";
                case EnemyId.Guard: return "guard";
                default: return type.ToString();
            }
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
