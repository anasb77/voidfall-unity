using VoidFall.Core;

namespace VoidFall.Runtime
{
    /// <summary>
    /// Owns the combat simulation state: enemies, bullets, hostile shots,
    /// pickups, bosses, meteors, their pooled order bookkeeping, the enemy
    /// spatial grid scratch buffers, and the deterministic combat RNG.
    ///
    /// v0 is state ownership only - method bodies remain on the runtime and
    /// reference this state through <c>_gameSim</c>, exactly as FxSim did
    /// before its logic migrated. Families migrate inward piece by piece;
    /// the PlayMode golden master proves each step behavior-neutral.
    /// </summary>
    internal sealed class GameSim
    {
        public readonly EnemyState[] Enemies;
        public readonly BulletState[] Bullets;
        public readonly HostileShotState[] HostileShots;
        public readonly PickupState[] Pickups;
        public readonly BossState[] Bosses;
        public readonly MeteorState[] Meteors;
        public readonly MeteorState[] PendingMeteorDetonations;

        // Pooled insertion-order bookkeeping. The enemy trio intentionally has
        // no duplicate guard on append; boss/meteor/pickup keep their own
        // historical semantics (see MIGRATION_STATUS on SlotOrder scoping).
        public readonly int[] EnemyOrder;
        public readonly int[] EnemyOrderPosition;
        public int EnemyOrderCount;

        public readonly SlotOrder BulletOrder;
        public readonly SlotOrder HostileShotOrder;

        public readonly int[] PickupOrder;
        public readonly int[] PickupOrderPosition;
        public int PickupOrderCount;

        public readonly int[] BossOrder;
        public int BossOrderCount;

        public readonly int[] MeteorOrder;
        public readonly int[] MeteorOrderPosition;
        public int MeteorOrderCount;

        // Spatial broad-phase scratch buffers for enemy collision queries.
        public readonly CollisionGrid EnemyGrid;
        public readonly int[] EnemyGridSpawnIds;
        public readonly int[] EnemyGridBulletCandidates;
        public readonly int[] EnemyGridAreaCandidates;
        public readonly int[] EnemyGridSeparationCandidates;

        /// <summary>The deterministic combat random stream.</summary>
        public Rng Rng;

        public GameSim(
            int maxEnemies,
            int maxBullets,
            int maxHostileShots,
            int maxPickupSlots,
            int maxBosses,
            int maxMeteors,
            uint seed)
        {
            Enemies = new EnemyState[maxEnemies];
            Bullets = new BulletState[maxBullets];
            HostileShots = new HostileShotState[maxHostileShots];
            Pickups = new PickupState[maxPickupSlots];
            Bosses = new BossState[maxBosses];
            Meteors = new MeteorState[maxMeteors];
            PendingMeteorDetonations = new MeteorState[maxMeteors];

            EnemyOrder = new int[maxEnemies];
            EnemyOrderPosition = new int[maxEnemies];
            BulletOrder = new SlotOrder(maxBullets);
            HostileShotOrder = new SlotOrder(maxHostileShots);
            PickupOrder = new int[maxPickupSlots];
            PickupOrderPosition = new int[maxPickupSlots];
            BossOrder = new int[maxBosses];
            MeteorOrder = new int[maxMeteors];
            MeteorOrderPosition = new int[maxMeteors];

            EnemyGrid = new CollisionGrid(maxEnemies);
            EnemyGridSpawnIds = new int[maxEnemies];
            EnemyGridBulletCandidates = new int[maxEnemies];
            EnemyGridAreaCandidates = new int[maxEnemies];
            EnemyGridSeparationCandidates = new int[maxEnemies];

            Rng = new Rng(seed);
        }
    }
}
