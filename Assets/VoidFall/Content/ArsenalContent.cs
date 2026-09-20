using System;
using System.Globalization;

namespace VoidFall.Core
{
    /// <summary>Owner-approved browser weapon concepts. Historical catalogue entries retain their indices.</summary>
    public static class ArsenalContent
    {
        public const int FirstWeaponIndex = 6;
        public const double MineArmingSeconds = 0.55;
        public const double MineChainSeconds = 0.14;
        public const double MineLifetimeSeconds = 15;
        public const double MineMinimumPlacementSeconds = 0.9;
        public const double MineFreezeSeconds = 1.2;
        public const double MineFreezeRecoverySeconds = 1.2;
        public const double SummonAcquisitionRange = 420;
        public const double SummonReturnLeash = 700;
        public const double ClockOpacity = 0.5;
        public const double ClockFaceOpacity = 0.126;
        public const double BoomerangSizeScale = 0.675;
        public const double MineRangeOpacity = 0.7;
        public const int ClockHandCapacity = 3;

        // Stable slots: original hand, evolved counterclockwise hand, rank-III seconds hand.
        public static bool ClockHandActive(int hand, int rank, bool evolved)
            => rank > 0 && (hand == 0 || hand == 1 && evolved || hand == 2 && rank >= 3);
        public static double ClockHandScale(int hand) => hand == 2 ? 0.5 : 1;
        public static double ClockHandSpeed(int hand) => hand == 2 ? 2 : 1;

        public static WeaponDefinition[] AppendWeapons(WeaponDefinition[] original)
        {
            var result = new WeaponDefinition[original.Length + 4];
            Array.Copy(original, result, original.Length);
            var ids = new[] { "mines", "summons", "clock", "boomerang" };
            var names = new[] { "Mines", "Summons", "Clock", "Boomerang" };
            var colors = new[] { "#ffb75e", "#87f5ab", "#b8e9f7", "#63dfff" };
            var summaries = new[] { "Lay traps along your escape route.", "Kamikaze rushers wait beside you, then attack nearby targets.", "A clock hand cuts through enemies around you.", "A thrown blade ricochets between enemies before returning." };
            for (var i = 0; i < ids.Length; i++)
            {
                var ranks = new WeaponRankDefinition[6];
                for (var r = 1; r <= 6; r++) ranks[r - 1] = new WeaponRankDefinition { Rank = r, Stats = RankStats(ids[i], r) };
                result[original.Length + i] = new WeaponDefinition { Id = ids[i], Name = names[i], Kind = ids[i], Accent = colors[i], Summary = summaries[i], Ranks = ranks };
            }
            return result;
        }

        public static EvolutionDefinition[] AppendEvolutions(EvolutionDefinition[] original)
        {
            var result = new EvolutionDefinition[original.Length + 4];
            Array.Copy(original, result, original.Length);
            result[original.Length] = Evolution("mines", "amplifier", "Permafrost Mines", "Explosions freeze ordinary enemies for 1.2 seconds, followed by 1.2 seconds of freeze immunity. Bosses resist freezing.", "#8ceaff");
            result[original.Length + 1] = Evolution("summons", "dodge", "Volatile Brood", "Rushers explode on impact, damaging nearby enemies.", "#87f5ab");
            result[original.Length + 2] = Evolution("clock", "cycling", "Hazard's Clock", "A second hand rotates counterclockwise and damages enemies independently.", "#99f6e4");
            result[original.Length + 3] = Evolution("boomerang", "projectileSpeed", "Triple Return", "Launch three ricocheting blades with each throw.", "#63dfff");
            return result;
        }

        private static EvolutionDefinition Evolution(string weapon, string support, string name, string description, string accent)
            => new EvolutionDefinition { WeaponId = weapon, SupportId = support, Name = name, Description = description, Accent = accent };

        public static bool IsArsenalWeapon(string id) => id == "mines" || id == "summons" || id == "clock" || id == "boomerang";

        public static WeaponStatsDefinition RankStats(string id, int rank)
        {
            var r = Math.Max(1, Math.Min(6, rank)) - 1;
            if (id == "mines") return new WeaponStatsDefinition
            {
                Damage = new double[] { 60, 75, 75, 95, 115, 140 }[r],
                Cooldown = new double[] { 2.4, 2.4, 2.1, 2.1, 1.9, 1.8 }[r],
                BlastRadius = new double[] { 90, 90, 105, 105, 115, 125 }[r],
                Range = 46, ProjectileRadius = 10, ProjectileCount = 1
            };
            if (id == "summons") return new WeaponStatsDefinition
            {
                Damage = new double[] { 40, 50, 50, 65, 80, 100 }[r],
                Cooldown = new double[] { 3, 3, 3, 2.6, 2.3, 2 }[r],
                ProjectileCount = r < 2 ? 2 : 3,
                ProjectileSpeed = 300, Range = SummonAcquisitionRange, ProjectileRadius = 7, BlastRadius = 88
            };
            if (id == "clock") return new WeaponStatsDefinition
            {
                Damage = new double[] { 20, 26, 32, 40, 48, 60 }[r],
                OrbitSpeed = Math.PI * 2 / new double[] { 5.5, 5.5, 4.7, 4, 3.4, 3 }[r],
                OrbitRadius = 125, Range = 125, HitCooldown = 0.32, ProjectileRadius = 7, OrbitCount = 1
            };
            if (id == "boomerang") return new WeaponStatsDefinition
            {
                Damage = new double[] { 24, 32, 32, 40, 48, 60 }[r],
                Cooldown = new double[] { 1.8, 1.8, 1.65, 1.65, 1.45, 1.3 }[r],
                ChainCount = new int[] { 2, 2, 3, 3, 4, 5 }[r],
                ProjectileSpeed = 450, Range = 650, ProjectileRadius = 9 * BoomerangSizeScale, ProjectileCount = 1
            };
            throw new ArgumentException("Unknown arsenal weapon", nameof(id));
        }

        public static string RankDescription(WeaponDefinition weapon, int nextRank)
        {
            var after = weapon.Ranks[nextRank - 1].Stats;
            var before = nextRank > 1 ? weapon.Ranks[nextRank - 2].Stats : null;
            var damage = Change("Damage", before?.Damage, after.Damage);
            if (weapon.Id == "mines") return damage + "\n" + Change("Drop delay", before?.Cooldown, after.Cooldown, "s") + "\n" + Change("Blast radius", before?.BlastRadius, after.BlastRadius);
            if (weapon.Id == "summons") return damage + "\n" + Change("Squad size", before?.ProjectileCount, after.ProjectileCount) + "\n" + Change("Spawn delay", before?.Cooldown, after.Cooldown, "s");
            if (weapon.Id == "clock") return damage + "\n" + Change("Full rotation", before == null ? (double?)null : Math.PI * 2 / before.OrbitSpeed, Math.PI * 2 / after.OrbitSpeed, "s") + "\nReach 125" + (nextRank >= 3 ? "\n" + (nextRank == 3 ? "Unlock seconds hand: " : "Seconds hand: ") + "2x speed, half reach and damage" : "");
            return damage + "\n" + Change("Targets per throw", before?.ChainCount, after.ChainCount) + "\n" + Change("Throw delay", before?.Cooldown, after.Cooldown, "s");
        }

        private static string Change(string label, double? before, double after, string suffix = "")
        {
            var value = after.ToString("0.##", CultureInfo.InvariantCulture) + suffix;
            return label + " " + (before.HasValue && before.Value != after ? before.Value.ToString("0.##", CultureInfo.InvariantCulture) + suffix + " → " : "") + value;
        }
    }
}
