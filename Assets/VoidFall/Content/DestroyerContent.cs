namespace VoidFall.Core
{
    public static class DestroyerContent
    {
        public const int RaidCount = 8;
        // Preserve the five roles, then add Maw, Razor and Spite.
        public static int RaidTypeAt(int index) => index < 5 ? index : index == 5 ? 0 : index == 6 ? 1 : 4;
        public static readonly EnemyDefinition[] Enemies =
        {
            Make("maw", "Maw", 360, 34, 122, 27, 330, .95, 2.6),
            Make("razor", "Razor", 270, 22, 135, 19, 260, .85, 2.05),
            Make("husk", "Husk", 760, 38, 57, 38, 115, 1.1, 2.7),
            Make("grasp", "Grasp", 620, 33, 70, 28, 140, 1.2, 3),
            Make("spite", "Spite", 320, 26, 66, 17, 510, 1.1, 3.3),
        };
        public static float RaidHealthMultiplier(float challengeSeconds)
            => 1f + 1.5f * System.Math.Max(0f, System.Math.Min(1f, challengeSeconds / 900f));

        public static float RaidEntryDistance(int type)
            => type < 2 ? 300f : type == 2 ? 230f : type == 3 ? 250f : 340f;
        private static EnemyDefinition Make(string id, string name, double hp, double radius,
            double speed, double damage, double range, double warning, double recovery)
            => new EnemyDefinition { Id = "destroyer-" + id, Name = name, Behavior = "destroyer-" + id,
                Health = hp, Radius = radius, Speed = speed, ContactDamage = damage,
                PreferredDistance = range, TelegraphSeconds = warning, RecoverySeconds = recovery,
                Color = "#ffffff", Xp = 8, ProjectileSpeed = 215, BlastRadius = id == "husk" ? 145 : 165 };
        public static EnemyDefinition Find(string id)
        {
            for (var i = 0; i < Enemies.Length; i++) if (Enemies[i].Id == id) return Enemies[i];
            return null;
        }
    }
}
