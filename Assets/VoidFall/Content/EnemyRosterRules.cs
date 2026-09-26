using System;

namespace VoidFall.Core
{
    public enum EnemyRoster
    {
        One = 1,
        Two = 2,
        Three = 3,
        Four = 4,
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
            "guard", "runner", "twinGunner", "dasher", "brute", "technician",
            "mortar", "splitter", "bulwark", "harvester", "carrier",
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
            return TierAt(elapsedSeconds, roll);
        }

        public static EnemyRoster EnemyRosterForSpawn(EnemyId type, double elapsedSeconds, double roll)
        {
            return EnemyRosterForSpawn(EnemyIdName(type), elapsedSeconds, roll);
        }

        /// <summary>
        /// Shared director tier policy. The first shared visit only previews a
        /// small late-Abyss tier-II share; the second visit enters at 15% and
        /// rises to 50%. Later visits retain the established global tiers.
        /// </summary>
        public static EnemyRoster SharedRosterForSpawn(
            string type,
            int sharedVisit,
            double localSeconds,
            double familyAge,
            double globalSeconds,
            double roll)
        {
            if (!RosterTwoEligible(type)) return EnemyRoster.One;
            var share = LegacyRestorationRules.SharedTierTwoShare(sharedVisit, localSeconds, familyAge);
            if (double.IsNaN(share)) return EnemyRosterForSpawn(type, globalSeconds, roll);
            return roll < share ? EnemyRoster.Two : EnemyRoster.One;
        }

        public static double RosterCooldownSeconds(double seconds, EnemyRoster roster)
        {
            return Math.Max(0, seconds * CooldownMultiplier(roster));
        }

        public static EnemyRoster TierAt(double seconds, double roll)
        {
            if (!IsFinite(seconds)) seconds = 0;
            roll = IsFinite(roll) ? Math.Min(1, Math.Max(0, roll)) : 1;
            if (seconds >= 2400) return EnemyRoster.Four;
            if (seconds > 2040) return roll < (seconds - 2040) / 360 ? EnemyRoster.Four : EnemyRoster.Three;
            if (seconds >= 1800) return EnemyRoster.Three;
            if (seconds > 1440) return roll < (seconds - 1440) / 360 ? EnemyRoster.Three : EnemyRoster.Two;
            if (seconds >= 900) return EnemyRoster.Two;
            if (seconds > 540) return roll < (seconds - 540) / 360 ? EnemyRoster.Two : EnemyRoster.One;
            return EnemyRoster.One;
        }
        public static double HealthMultiplier(EnemyRoster tier) => tier == EnemyRoster.Four ? 4.2 : tier == EnemyRoster.Three ? 2.6 : tier == EnemyRoster.Two ? 1.3 : 1;
        public static double SpeedMultiplier(EnemyRoster tier) => tier == EnemyRoster.Four ? 1.16 : tier == EnemyRoster.Three ? 1.12 : tier == EnemyRoster.Two ? 1.06 : 1;
        public static double RadiusMultiplier(EnemyRoster tier) => tier == EnemyRoster.Four ? 1.23 : tier == EnemyRoster.Three ? 1.16 : tier == EnemyRoster.Two ? 1.08 : 1;
        public static double DamageMultiplier(EnemyRoster tier) => tier == EnemyRoster.Four ? 1.9 : tier == EnemyRoster.Three ? 1.5 : tier == EnemyRoster.Two ? 1.12 : 1;
        public static double CooldownMultiplier(EnemyRoster tier) => tier == EnemyRoster.Four ? .58 : tier == EnemyRoster.Three ? .65 : tier == EnemyRoster.Two ? .82 : 1;
        public static double ProjectileMultiplier(EnemyRoster tier) => tier == EnemyRoster.Four ? 1.18 : tier == EnemyRoster.Three ? 1.15 : tier == EnemyRoster.Two ? 1.06 : 1;
        public static double ThreatMultiplier(EnemyRoster tier) => tier > EnemyRoster.One ? RosterTwoThreatMultiplier : 1;

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
                default: var name = type.ToString(); return char.ToLowerInvariant(name[0]) + name.Substring(1);
            }
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
