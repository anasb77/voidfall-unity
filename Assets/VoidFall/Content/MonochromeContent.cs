namespace VoidFall.Core
{
    /// <summary>
    /// Unity-first Monochrome Court content. It stays outside the generated
    /// browser-parity catalogue so the deprecated source cannot overwrite it.
    /// </summary>
    public static class MonochromeContent
    {
        public static readonly ArenaDefinition Arena = new ArenaDefinition
        {
            Id = "monochrome-court",
            Name = "Monochrome Court",
            Description = "A living chess court where two armies rewrite the board.",
            Modifier = "Black army · White army · Floor is lava",
            StarTint = "#e8e8e3",
            WeightMultipliers = new WeightedValueDefinition[0],
            Features = new[] { "courtRoster", "twinGrandmasters" },
            Cycles = new[]
            {
                new ArenaCycleDefinition { Id = "spiral", Name = "Spiral court", Seconds = 14, FlashRate = 0.02 },
                new ArenaCycleDefinition { Id = "black-rule", Name = "Black rule", Seconds = 42, FlashRate = 0.06 },
                new ArenaCycleDefinition { Id = "white-rule", Name = "White rule", Seconds = 42, FlashRate = 0.06 },
                new ArenaCycleDefinition { Id = "convergence", Name = "Convergence", Seconds = 18, FlashRate = 0.14 },
            },
            EliteCadenceMultiplier = 1.1,
            EliteRewardMultiplier = 1.25,
        };

        public static readonly EnemyDefinition[] Enemies =
        {
            new EnemyDefinition
            {
                Id = "court-pawn", Name = "Pawn", Behavior = "court-pawn",
                Health = 30, Speed = 78, ContactDamage = 8, Radius = 15, Xp = 1,
                Color = "#e5e7eb", NaturalStartSeconds = 0,
            },
            new EnemyDefinition
            {
                Id = "court-rook", Name = "Rook", Behavior = "court-rook",
                Health = 220, Speed = 82, ContactDamage = 22, Radius = 30, Xp = 8,
                Color = "#f3f4f6", NaturalStartSeconds = 35,
                AttackCooldown = 4.5, TelegraphSeconds = 0.8, RecoverySeconds = 0.75,
            },
            new EnemyDefinition
            {
                Id = "court-bishop", Name = "Bishop", Behavior = "court-bishop",
                Health = 70, Speed = 42, ContactDamage = 10, Radius = 18, Xp = 6,
                Color = "#d1d5db", NaturalStartSeconds = 25,
                PreferredDistance = 500, AttackCooldown = 4.2, TelegraphSeconds = 1.15,
                ProjectileSpeed = 440,
            },
            new EnemyDefinition
            {
                Id = "court-knight", Name = "Knight", Behavior = "court-knight",
                Health = 55, Speed = 100, ContactDamage = 18, Radius = 17, Xp = 5,
                Color = "#f9fafb", NaturalStartSeconds = 18,
                AttackCooldown = 3.1, TelegraphSeconds = 0.7, RecoverySeconds = 0.55,
            },
            new EnemyDefinition
            {
                Id = "court-queen", Name = "Queen", Behavior = "court-queen",
                Health = 190, Speed = 50, ContactDamage = 16, Radius = 26, Xp = 12,
                Color = "#e5e7eb", NaturalStartSeconds = 80,
                PreferredDistance = 360, AttackCooldown = 5.2, TelegraphSeconds = 1.0,
                ProjectileSpeed = 360,
            },
        };

        public static readonly BossDefinition BlackBoss = CreateBoss(
            "court-grandmaster-black", "Black Grandmaster", "court-black-volley", "#111827");
        public static readonly BossDefinition WhiteBoss = CreateBoss(
            "court-grandmaster-white", "White Grandmaster", "court-white-volley", "#f3f4f6");

        public static ArenaDefinition FindArena(string id) => id == Arena.Id ? Arena : null;

        public static EnemyDefinition FindEnemy(string id)
        {
            foreach (var enemy in Enemies) if (enemy.Id == id) return enemy;
            return null;
        }

        public static BossDefinition FindBoss(string id)
        {
            if (id == BlackBoss.Id) return BlackBoss;
            return id == WhiteBoss.Id ? WhiteBoss : null;
        }

        private static BossDefinition CreateBoss(string id, string name, string attackId, string color) =>
            new BossDefinition
            {
                Id = id,
                Name = name,
                Health = 9000,
                Speed = 0,
                ContactDamage = 20,
                Radius = 66,
                Color = color,
                StartsAtSeconds = -1,
                RepeatEverySeconds = 0,
                PhaseTwoHealthRatio = 0.5,
                PhaseTwoSpeedMultiplier = 1,
                PhaseTwoCooldownMultiplier = 0.76,
                RewardParts = 0,
                Attacks = new[]
                {
                    new BossAttackDefinition
                    {
                        Id = attackId,
                        TelegraphSeconds = 1.05,
                        ActiveSeconds = 1.25,
                        RecoverySeconds = 0.65,
                        CooldownSeconds = 4.5,
                        Damage = 18,
                        BeamLength = 900,
                        BeamWidth = 72,
                    },
                },
            };
    }
}
