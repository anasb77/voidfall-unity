namespace VoidFall.Core
{
    /// <summary>
    /// Unity-first Hydra content. Kept separate from the generated browser
    /// parity catalogue so the deprecated source cannot overwrite it.
    /// </summary>
    public static class HydraContent
    {
        public static readonly ArenaDefinition Arena = new ArenaDefinition
        {
            Id = "hydra",
            Name = "Hydra",
            Description = "A viridian ossuary grown around a toxic neural organism.",
            Modifier = "Mutated enemies · Hydra Prime",
            StarTint = "#b7ff5a",
            WeightMultipliers = new WeightedValueDefinition[0],
            Features = new[] { "mutations", "hydraPrime" },
            Cycles = new[]
            {
                new ArenaCycleDefinition { Id = "dormant", Name = "Dormant tissue", Seconds = 42, FlashRate = 0.02 },
                new ArenaCycleDefinition { Id = "breathing", Name = "Breathing marrow", Seconds = 30, FlashRate = 0.06 },
                new ArenaCycleDefinition { Id = "hostile", Name = "Hostile recombination", Seconds = 22, FlashRate = 0.13 },
                new ArenaCycleDefinition { Id = "rupture", Name = "Genetic rupture", Seconds = 16, FlashRate = 0.24 },
            },
            EliteCadenceMultiplier = 1.15,
            EliteRewardMultiplier = 1.2,
        };

        public static readonly BossDefinition Boss = new BossDefinition
        {
            Id = "hydra-prime",
            Name = "Hydra Prime",
            Health = 12000,
            Speed = 0,
            ContactDamage = 22,
            Radius = 92,
            Color = "#78ff5a",
            StartsAtSeconds = -1,
            RepeatEverySeconds = 0,
            PhaseTwoHealthRatio = 0.5,
            PhaseTwoSpeedMultiplier = 1,
            PhaseTwoCooldownMultiplier = 0.78,
            RewardParts = 50,
            Attacks = new[]
            {
                new BossAttackDefinition
                {
                    Id = "hydra-marrow", TelegraphSeconds = 1.05, ActiveSeconds = 2.5,
                    RecoverySeconds = 0.8, CooldownSeconds = 5.4, Damage = 18, Radius = 64,
                },
                new BossAttackDefinition
                {
                    Id = "hydra-evasion", TelegraphSeconds = 0.75, ActiveSeconds = 2.4,
                    RecoverySeconds = 0.65, CooldownSeconds = 5.0, Damage = 0,
                },
                new BossAttackDefinition
                {
                    Id = "hydra-ribs", TelegraphSeconds = 1.0, ActiveSeconds = 0.12,
                    RecoverySeconds = 0.85, CooldownSeconds = 4.8, Damage = 14,
                    Radius = HydraEncounterRules.RibProjectileRadius,
                    ProjectileCount = 8, ProjectileSpeed = 300,
                },
                new BossAttackDefinition
                {
                    Id = "hydra-optic", TelegraphSeconds = 1.1, ActiveSeconds = 2.2,
                    RecoverySeconds = 0.85, CooldownSeconds = 6.2, Damage = 16,
                    BeamLength = 760, BeamWidth = 38, RotationSpeed = 0.72,
                },
            },
        };

        public static ArenaDefinition FindArena(string id) => id == Arena.Id ? Arena : null;
        public static BossDefinition FindBoss(string id) => id == Boss.Id ? Boss : null;
    }
}
