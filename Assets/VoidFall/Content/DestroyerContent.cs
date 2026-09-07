namespace VoidFall.Core
{
    public static class DestroyerContent
    {
        public static readonly EnemyDefinition[] Enemies =
        {
            Make("maw", "Maw", 245, 34, 122, 27, 330, .95, 2.6),
            Make("razor", "Razor", 175, 22, 135, 19, 260, .85, 2.05),
            Make("husk", "Husk", 510, 38, 57, 38, 115, 1.1, 2.7),
            Make("grasp", "Grasp", 420, 33, 70, 28, 140, 1.2, 3),
            Make("spite", "Spite", 210, 26, 66, 17, 510, 1.1, 3.3),
        };
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
