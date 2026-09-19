using System;
using System.Collections.Generic;

namespace VoidFall.Core
{
    // Authored additions; generated parity catalogues retain their stable ordering.
    public static class LegacyRestorationRules
    {
        public const string Version = "2026-09-19-density-v3";
        public const double SecondWindCooldown = 180;
        public const float SpikyBaseRadius = 19.5f;
        public const float SpikyExpandedScale = 3f;
        public const float SpikyPhaseSeconds = .5f;
        public const double OrdinaryRareDropChance = 1.0 / 300.0;
        public const float ShurikenSpinRadians = 14f;
        public const float SwarmIntervalSeconds = 34f;
        public const float ArrivalRateMultiplier = 2.5f;
        public const int StartingPressureHundredths = 100;
        public static bool SupportsPiercing(string id) => id == "pistol" || id == "scattergun" || id == "railgun";
        public static bool SupportsSplit(string id) => SupportsPiercing(id) || id == "seeker";
        public static bool CanHeal(double hp, double maxHp, int level, double remaining)
            => hp > 0 && maxHp > 0 && hp <= maxHp * .1 && level > 0 && remaining <= 0;
        public static double GiantSlayerMultiplier(int rank) => 1 + Math.Min(3, Math.Max(0, rank)) * .15;
        public static double RevealSeconds(string id)
        {
            switch (id)
            {
                case "chaser": return 0;
                case "runner": return 18;
                case "swarmer": return 40;
                case "gunner": return 75;
                case "dasher": return 70;
                case "shuriken": return 210;
                case "brute": return 270;
                case "exploder": return 60;
                case "spiky": return 420;
                case "guard": return 480;
                case "technician": return 540;
                case "twinGunner": return 600;
                case "splitter": return 660;
                case "mortar": return 720;
                case "bulwark": return 780;
                case "harvester": return 840;
                case "carrier": return 900;
                default: return 0; // Arena-exclusive and boss populations own their introductions.
            }
        }
        public static bool IsNewEnemy(string id) => id == "swarmer" || id == "spiky" || id == "shuriken";
        public static EnemyDefinition[] AppendEnemies(EnemyDefinition[] source)
        {
            var result = new List<EnemyDefinition>(source);
            result.Add(Enemy("swarmer", "Swarmer", "#86efac", 6, 7, 115, 4, 1));
            result.Add(Enemy("shuriken", "Ninja Shuriken", "#f59e0b", 12, 30, 85, 10, 3));
            result.Add(Enemy("spiky", "Spiky", "#c084fc", SpikyBaseRadius, 28, 66, 12, 3));
            return result.ToArray();
        }
        private static EnemyDefinition Enemy(string id, string name, string accent, double radius, double health, double speed, double damage, double xp)
            => new EnemyDefinition { Id = id, Name = name, Color = accent, Radius = radius, Health = health,
                Speed = speed, ContactDamage = damage, Xp = xp, Behavior = "direct", NaturalStartSeconds = RevealSeconds(id) };

        public static SupportDefinition[] AppendSupports(SupportDefinition[] source)
        {
            var result = new List<SupportDefinition>(source);
            result.Add(Support("phaseRounds", "Phase Rounds", 3, "#67e8f9", "+1 piercing for compatible projectiles", 6));
            result.Add(Support("giantSlayer", "Giant Slayer", 3, "#fbbf24", "+15% damage against bosses and elites", 6));
            result.Add(Support("secondWind", "Second Wind", 1, "#86efac", "At 10% HP or below, heal HP equal to your level. Cooldown: 180 seconds.", 5));
            foreach (var id in new[] { "pistol", "scattergun", "railgun", "seeker" })
                result.Add(Support("split-" + id, "Split Shot — " + UpgradeRules.WeaponDisplayName(id), 2, "#a5f3fc", "+1 projectile per attack", 3));
            return result.ToArray();
        }
        private static SupportDefinition Support(string id, string name, int ranks, string accent, string description, double weight)
        {
            var descriptions = new string[ranks];
            for (var i = 0; i < ranks; i++) descriptions[i] = description;
            return new SupportDefinition { Id = id, Name = name, MaxRank = ranks, Accent = accent, Weight = weight, Descriptions = descriptions };
        }
        public static bool SupportEligible(string id, UpgradeProgress progress)
        {
            if (id != "phaseRounds" && !id.StartsWith("split-", StringComparison.Ordinal)) return true;
            for (var i = 0; i < ContentCatalog.Weapons.Length && i < progress.WeaponRanks.Length; i++)
                if (progress.WeaponRanks[i] > 0 && (id == "phaseRounds" ? SupportsPiercing(ContentCatalog.Weapons[i].Id) : id == "split-" + ContentCatalog.Weapons[i].Id)) return true;
            return false;
        }
        public static string EligibleWeaponNames(UpgradeProgress progress)
        {
            var names = new List<string>();
            for (var i = 0; i < ContentCatalog.Weapons.Length; i++)
                if (progress.WeaponRanks[i] > 0 && SupportsPiercing(ContentCatalog.Weapons[i].Id)) names.Add(ContentCatalog.Weapons[i].Name);
            return string.Join(", ", names);
        }
    }
}
