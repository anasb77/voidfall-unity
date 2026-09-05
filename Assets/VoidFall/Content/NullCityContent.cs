namespace VoidFall.Core
{
    /// <summary>
    /// Unity-first Null City catalogue. It remains separate from the generated
    /// legacy catalogue so browser-parity generation cannot overwrite it.
    /// </summary>
    public static class NullCityContent
    {
        public const string StableId = "null-city";
        public const string MotherloadId = "null-motherload";

        public static readonly ArenaDefinition Arena = new ArenaDefinition
        {
            Id = StableId,
            Name = "Null City",
            Description = "An illuminated alien city that turns its infrastructure against intruders.",
            Modifier = "Surveillance · Lockdown · Purge lanes",
            StarTint = "#63eade",
            WeightMultipliers = new WeightedValueDefinition[0],
            Features = new[] { "nullCityRoster", "purgeLanes", "motherload" },
            Cycles = new[]
            {
                new ArenaCycleDefinition
                {
                    Id = "surveillance", Name = "Surveillance",
                    Seconds = NullCityRules.SurveillanceSeconds, FlashRate = 0.03,
                },
                new ArenaCycleDefinition
                {
                    Id = "lockdown", Name = "Lockdown",
                    Seconds = NullCityRules.LockdownSeconds, FlashRate = 0.16,
                },
            },
            EliteCadenceMultiplier = 1.1,
            EliteRewardMultiplier = 1.25,
        };

        public static readonly EnemyDefinition[] Enemies =
        {
            Enemy("null-patrol", "Patrol", 44, 70, 12, 13, 2, "#64efe5", 0,
                preferredDistance: 245, attackCooldown: 4.4, telegraphSeconds: 1.35,
                projectileSpeed: 250),
            Enemy("null-enforcer", "Enforcer", 175, 32, 8, 22, 6, "#e989de", 20,
                attackCooldown: 6, telegraphSeconds: 1.35, recoverySeconds: 0.5),
            Enemy("null-sentinel", "Rail Sentinel", 85, 20, 12, 17, 4, "#ffd19b", 12,
                preferredDistance: 320, attackCooldown: 5, telegraphSeconds: 1.35,
                projectileSpeed: 465),
            Enemy("null-crawler", "Crawler", 36, 85, 8, 15, 1, "#b6efb4", 0),
            Enemy("null-volatile", "Volatile Crawler", 90, 58, 27, 25, 5, "#ff985f", 38,
                attackCooldown: 5, telegraphSeconds: 1.5, triggerDistance: 100,
                blastRadius: 124),
            Enemy("null-gunship", "Heavy Gunship", 630, 25, 12, 43, 14, "#87b7ff", 85,
                preferredDistance: 385, attackCooldown: 7, telegraphSeconds: 1.35,
                projectileSpeed: 295, recoverySeconds: 1.08),
            Enemy("null-mech", "Siege Mech", 760, 24, 29, 39, 16, "#e5a4f6", 105,
                attackCooldown: 5, telegraphSeconds: 1.55, triggerDistance: 153,
                blastRadius: 128),
            Enemy("null-broodmother", "Broodmother", 840, 18, 20, 57, 18, "#d4f790", 125,
                preferredDistance: 220, attackCooldown: 8),
            Enemy("null-light-gunship", "Light Gunship", 240, 48, 12, 27, 8, "#b4b0ff", 52,
                preferredDistance: 300, attackCooldown: 5, telegraphSeconds: 1.35,
                projectileSpeed: 340, recoverySeconds: 0.54),
            Enemy("null-interceptor", "Interceptor", 95, 90, 8, 17, 5, "#4b9dff", -1,
                attackCooldown: 5, telegraphSeconds: 1.35, recoverySeconds: 0.3),
            Enemy("null-marshal", "Marshal", 310, 31, 8, 28, 10, "#72b6ff", -1,
                preferredDistance: 180, attackCooldown: 6, recoverySeconds: 3),
            Enemy("null-suppressor", "Suppressor", 170, 35, 12, 21, 7, "#4088ff", -1,
                preferredDistance: 260, attackCooldown: 5, telegraphSeconds: 1.35,
                projectileSpeed: 250),
        };

        public static readonly BossDefinition Motherload = new BossDefinition
        {
            Id = MotherloadId,
            Name = "Motherload",
            Health = 12000,
            Speed = 22,
            ContactDamage = 24,
            Radius = NullCityRules.MotherloadBodyRadius,
            Color = "#ffd58d",
            StartsAtSeconds = -1,
            RepeatEverySeconds = 0,
            PhaseTwoHealthRatio = 0.5,
            PhaseTwoSpeedMultiplier = 1,
            PhaseTwoCooldownMultiplier = 1,
            RewardParts = 50,
            Attacks = new[]
            {
                new BossAttackDefinition
                {
                    Id = "null-cannons", TelegraphSeconds = 1.4, ActiveSeconds = 1.44,
                    RecoverySeconds = NullCityRules.MotherloadRecoverySeconds,
                    CooldownSeconds = 0, Damage = 12, ProjectileCount = 8, ProjectileSpeed = 320,
                },
                new BossAttackDefinition
                {
                    Id = "null-tractor", TelegraphSeconds = NullCityRules.TractorWarnSeconds,
                    ActiveSeconds = NullCityRules.TractorActiveSeconds, RecoverySeconds = 4,
                    CooldownSeconds = 0, Damage = 12,
                    BeamLength = NullCityRules.TractorMaxDistance,
                },
                new BossAttackDefinition
                {
                    Id = "null-brood", TelegraphSeconds = 1.4, ActiveSeconds = 0,
                    RecoverySeconds = NullCityRules.MotherloadRecoverySeconds,
                    CooldownSeconds = 0, Damage = 0, SummonCount = 4,
                },
                new BossAttackDefinition
                {
                    Id = "null-bombardment", TelegraphSeconds = 1.1, ActiveSeconds = 1.6,
                    RecoverySeconds = NullCityRules.MotherloadRecoverySeconds,
                    CooldownSeconds = 0, Damage = 28, Radius = 70, ProjectileCount = 3,
                },
                new BossAttackDefinition
                {
                    Id = "null-vent", TelegraphSeconds = 0,
                    ActiveSeconds = NullCityRules.MotherloadVentSeconds,
                    RecoverySeconds = NullCityRules.MotherloadRecoverySeconds,
                    CooldownSeconds = 0, Damage = 0,
                },
            },
        };

        public static ArenaDefinition FindArena(string id) => id == StableId ? Arena : null;

        public static EnemyDefinition FindEnemy(string id)
        {
            var index = EnemyIndex(id);
            return index >= 0 ? Enemies[index] : null;
        }

        public static BossDefinition FindBoss(string id) => id == MotherloadId ? Motherload : null;

        public static int EnemyIndex(string id)
        {
            for (var i = 0; i < Enemies.Length; i++)
            {
                if (Enemies[i].Id == id) return i;
            }

            return -1;
        }

        private static EnemyDefinition Enemy(
            string id,
            string name,
            double health,
            double speed,
            double contactDamage,
            double radius,
            double xp,
            string color,
            double naturalStartSeconds,
            double? preferredDistance = null,
            double? attackCooldown = null,
            double? telegraphSeconds = null,
            double? projectileSpeed = null,
            double? triggerDistance = null,
            double? blastRadius = null,
            double? recoverySeconds = null) =>
            new EnemyDefinition
            {
                Id = id,
                Name = name,
                Behavior = id,
                Health = health,
                Speed = speed,
                ContactDamage = contactDamage,
                Radius = radius,
                Xp = xp,
                Color = color,
                NaturalStartSeconds = naturalStartSeconds,
                PreferredDistance = preferredDistance,
                AttackCooldown = attackCooldown,
                TelegraphSeconds = telegraphSeconds,
                ProjectileSpeed = projectileSpeed,
                TriggerDistance = triggerDistance,
                BlastRadius = blastRadius,
                RecoverySeconds = recoverySeconds,
            };
    }
}
