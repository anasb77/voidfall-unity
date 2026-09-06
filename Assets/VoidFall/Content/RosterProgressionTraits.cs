namespace VoidFall.Core
{
    // Approved browser authoring values; no run-time parsing or per-actor allocation.
    public struct RosterProgressionTraits
    {
        public float ZigzagAmplitude;
        public float ZigzagFrequency;
        public float SprintEvery;
        public float SprintDuration;
        public float SprintMultiplier;
        public float ShotCount;
        public float ShotSpread;
        public float DashSpeed;
        public float DashDuration;
        public float DashWindup;
        public float DashCount;
        public float BlastRadius;
        public float BlastCount;
        public float BlastSpacing;
        public float BlastDelay;
        public float ProximityRadius;
        public float ShieldReduction;
        public float ShieldArc;
        public float HealRadius;
        public float HealAmount;
        public float HealCount;
        public float SplitCount;
        public float ChildTier;
        public float ShotSpeed;
        public float ProjectileRadius;
        public float HarvestRadius;
        public float HarvestAmount;
        public float HarvestTargets;
        public float SpawnCount;
        public float DriftRadius;
        public float LockSeconds;
        public float CurvatureAcceleration;
        public float CurveGapSlots;
        public float AimWindup;
        public static RosterProgressionTraits Get(string id, EnemyRoster tier, bool elite = false)
        {
            if (elite) return GetElite(id, tier);
            switch (id)
            {
                case "chaser":
                    switch (tier)
                    {
                        case EnemyRoster.One: return new RosterProgressionTraits {  };
                        case EnemyRoster.Two: return new RosterProgressionTraits {  };
                        case EnemyRoster.Three: return new RosterProgressionTraits {  };
                        case EnemyRoster.Four: return new RosterProgressionTraits {  };
                    }
                    break;
                case "runner":
                    switch (tier)
                    {
                        case EnemyRoster.One: return new RosterProgressionTraits { ZigzagAmplitude = 0.6f, ZigzagFrequency = 6f, SprintEvery = 0f, SprintDuration = 0f, SprintMultiplier = 1f };
                        case EnemyRoster.Two: return new RosterProgressionTraits { ZigzagAmplitude = 0.72f, ZigzagFrequency = 7f, SprintEvery = 4f, SprintDuration = 0.7f, SprintMultiplier = 1.25f };
                        case EnemyRoster.Three: return new RosterProgressionTraits { ZigzagAmplitude = 0.9f, ZigzagFrequency = 8f, SprintEvery = 3f, SprintDuration = 0.85f, SprintMultiplier = 1.4f };
                        case EnemyRoster.Four: return new RosterProgressionTraits { ZigzagAmplitude = 1f, ZigzagFrequency = 8f, SprintEvery = 2.8f, SprintDuration = 1f, SprintMultiplier = 1.42f };
                    }
                    break;
                case "gunner":
                    switch (tier)
                    {
                        case EnemyRoster.One: return new RosterProgressionTraits { ShotCount = 1f, ShotSpread = 0f };
                        case EnemyRoster.Two: return new RosterProgressionTraits { ShotCount = 3f, ShotSpread = 0.12f };
                        case EnemyRoster.Three: return new RosterProgressionTraits { ShotCount = 4f, ShotSpread = 0.11f };
                        case EnemyRoster.Four: return new RosterProgressionTraits { ShotCount = 4f, ShotSpread = 0.14f };
                    }
                    break;
                case "twinGunner":
                    switch (tier)
                    {
                        case EnemyRoster.One: return new RosterProgressionTraits { ShotCount = 2f, ShotSpread = 0.13f };
                        case EnemyRoster.Two: return new RosterProgressionTraits { ShotCount = 3f, ShotSpread = 0.16f };
                        case EnemyRoster.Three: return new RosterProgressionTraits { ShotCount = 4f, ShotSpread = 0.2f };
                        case EnemyRoster.Four: return new RosterProgressionTraits { ShotCount = 4f, ShotSpread = 0.25f };
                    }
                    break;
                case "dasher":
                    switch (tier)
                    {
                        case EnemyRoster.One: return new RosterProgressionTraits { DashSpeed = 340f, DashDuration = 0.55f, DashWindup = 0.85f, DashCount = 1f };
                        case EnemyRoster.Two: return new RosterProgressionTraits { DashSpeed = 360f, DashDuration = 0.6f, DashWindup = 0.8f, DashCount = 1f };
                        case EnemyRoster.Three: return new RosterProgressionTraits { DashSpeed = 385f, DashDuration = 0.55f, DashWindup = 0.7f, DashCount = 2f };
                        case EnemyRoster.Four: return new RosterProgressionTraits { DashSpeed = 410f, DashDuration = 0.6f, DashWindup = 0.75f, DashCount = 2f };
                    }
                    break;
                case "brute":
                    switch (tier)
                    {
                        case EnemyRoster.One: return new RosterProgressionTraits {  };
                        case EnemyRoster.Two: return new RosterProgressionTraits { BlastRadius = 64f, BlastCount = 1f, BlastSpacing = 0f, BlastDelay = 1f };
                        case EnemyRoster.Three: return new RosterProgressionTraits { BlastRadius = 100f, BlastCount = 1f, BlastSpacing = 0f, BlastDelay = 0.85f };
                        case EnemyRoster.Four: return new RosterProgressionTraits { BlastRadius = 120f, BlastCount = 1f, BlastSpacing = 0f, BlastDelay = 1f };
                    }
                    break;
                case "exploder":
                    switch (tier)
                    {
                        case EnemyRoster.One: return new RosterProgressionTraits { BlastRadius = 80f, BlastCount = 1f, BlastSpacing = 0f, BlastDelay = 0.8f, ProximityRadius = 70f };
                        case EnemyRoster.Two: return new RosterProgressionTraits { BlastRadius = 100f, BlastCount = 1f, BlastSpacing = 0f, BlastDelay = 0.85f, ProximityRadius = 80f };
                        case EnemyRoster.Three: return new RosterProgressionTraits { BlastRadius = 110f, BlastCount = 2f, BlastSpacing = 100f, BlastDelay = 0.85f, ProximityRadius = 90f };
                        case EnemyRoster.Four: return new RosterProgressionTraits { BlastRadius = 115f, BlastCount = 3f, BlastSpacing = 95f, BlastDelay = 1.1f, ProximityRadius = 100f };
                    }
                    break;
                case "guard":
                    switch (tier)
                    {
                        case EnemyRoster.One: return new RosterProgressionTraits { ShieldReduction = 0.37f, ShieldArc = 1.8f };
                        case EnemyRoster.Two: return new RosterProgressionTraits { ShieldReduction = 0.55f, ShieldArc = 2.1f };
                        case EnemyRoster.Three: return new RosterProgressionTraits { ShieldReduction = 0.68f, ShieldArc = 2.6f };
                        case EnemyRoster.Four: return new RosterProgressionTraits { ShieldReduction = 0.72f, ShieldArc = 2.8f };
                    }
                    break;
                case "technician":
                    switch (tier)
                    {
                        case EnemyRoster.One: return new RosterProgressionTraits { HealRadius = 150f, HealAmount = 8f, HealCount = 2f };
                        case EnemyRoster.Two: return new RosterProgressionTraits { HealRadius = 185f, HealAmount = 12f, HealCount = 3f };
                        case EnemyRoster.Three: return new RosterProgressionTraits { HealRadius = 220f, HealAmount = 18f, HealCount = 5f };
                        case EnemyRoster.Four: return new RosterProgressionTraits { HealRadius = 260f, HealAmount = 23f, HealCount = 6f };
                    }
                    break;
                case "mortar":
                    switch (tier)
                    {
                        case EnemyRoster.One: return new RosterProgressionTraits { BlastRadius = 65f, BlastCount = 1f, BlastSpacing = 0f, BlastDelay = 1.25f };
                        case EnemyRoster.Two: return new RosterProgressionTraits { BlastRadius = 75f, BlastCount = 2f, BlastSpacing = 95f, BlastDelay = 1.25f };
                        case EnemyRoster.Three: return new RosterProgressionTraits { BlastRadius = 82f, BlastCount = 3f, BlastSpacing = 105f, BlastDelay = 1.35f };
                        case EnemyRoster.Four: return new RosterProgressionTraits { BlastRadius = 87f, BlastCount = 4f, BlastSpacing = 110f, BlastDelay = 1.6f };
                    }
                    break;
                case "splitter":
                    switch (tier)
                    {
                        case EnemyRoster.One: return new RosterProgressionTraits { SplitCount = 2f, ChildTier = 1f };
                        case EnemyRoster.Two: return new RosterProgressionTraits { SplitCount = 3f, ChildTier = 1f };
                        case EnemyRoster.Three: return new RosterProgressionTraits { SplitCount = 4f, ChildTier = 2f };
                        case EnemyRoster.Four: return new RosterProgressionTraits { SplitCount = 5f, ChildTier = 3f };
                    }
                    break;
                case "bulwark":
                    switch (tier)
                    {
                        case EnemyRoster.One: return new RosterProgressionTraits { ShotCount = 3f, ShotSpread = 0.22f, ShotSpeed = 150f, ProjectileRadius = 8f, ShieldReduction = 0f, ShieldArc = 0f };
                        case EnemyRoster.Two: return new RosterProgressionTraits { ShotCount = 4f, ShotSpread = 0.2f, ShotSpeed = 165f, ProjectileRadius = 9f, ShieldReduction = 0.18f, ShieldArc = 1.8f };
                        case EnemyRoster.Three: return new RosterProgressionTraits { ShotCount = 4f, ShotSpread = 0.3f, ShotSpeed = 180f, ProjectileRadius = 11f, ShieldReduction = 0.35f, ShieldArc = 2.2f };
                        case EnemyRoster.Four: return new RosterProgressionTraits { ShotCount = 4f, ShotSpread = 0.34f, ShotSpeed = 190f, ProjectileRadius = 12f, ShieldReduction = 0.48f, ShieldArc = 2.45f };
                    }
                    break;
                case "harvester":
                    switch (tier)
                    {
                        case EnemyRoster.One: return new RosterProgressionTraits { HarvestRadius = 80f, HarvestAmount = 10f, HarvestTargets = 1f };
                        case EnemyRoster.Two: return new RosterProgressionTraits { HarvestRadius = 120f, HarvestAmount = 16f, HarvestTargets = 2f };
                        case EnemyRoster.Three: return new RosterProgressionTraits { HarvestRadius = 160f, HarvestAmount = 22f, HarvestTargets = 3f };
                        case EnemyRoster.Four: return new RosterProgressionTraits { HarvestRadius = 190f, HarvestAmount = 28f, HarvestTargets = 4f };
                    }
                    break;
                case "carrier":
                    switch (tier)
                    {
                        case EnemyRoster.One: return new RosterProgressionTraits { SpawnCount = 1f, ChildTier = 1f };
                        case EnemyRoster.Two: return new RosterProgressionTraits { SpawnCount = 2f, ChildTier = 1f };
                        case EnemyRoster.Three: return new RosterProgressionTraits { SpawnCount = 3f, ChildTier = 2f };
                        case EnemyRoster.Four: return new RosterProgressionTraits { SpawnCount = 4f, ChildTier = 3f };
                    }
                    break;
            }
            return default;
        }
        private static RosterProgressionTraits GetElite(string id, EnemyRoster tier)
        {
            switch (id)
            {
                case "exploder":
                    switch (tier)
                    {
                        case EnemyRoster.One: return new RosterProgressionTraits { BlastCount = 1f, BlastRadius = 95f, BlastDelay = 1.1f, BlastSpacing = 0f, ProximityRadius = 70f };
                        case EnemyRoster.Two: return new RosterProgressionTraits { BlastCount = 1f, BlastRadius = 108f, BlastDelay = 1.2f, BlastSpacing = 0f, ProximityRadius = 82f };
                        case EnemyRoster.Three: return new RosterProgressionTraits { BlastCount = 2f, BlastRadius = 112f, BlastDelay = 1.25f, BlastSpacing = 92f, ProximityRadius = 92f };
                        case EnemyRoster.Four: return new RosterProgressionTraits { BlastCount = 3f, BlastRadius = 116f, BlastDelay = 1.35f, BlastSpacing = 104f, ProximityRadius = 100f };
                    }
                    break;
                case "mortar":
                    switch (tier)
                    {
                        case EnemyRoster.One: return new RosterProgressionTraits { BlastCount = 1f, BlastRadius = 58.5f, BlastDelay = 1.5f, BlastSpacing = 0f, DriftRadius = 26f, LockSeconds = 0.45f };
                        case EnemyRoster.Two: return new RosterProgressionTraits { BlastCount = 2f, BlastRadius = 68f, BlastDelay = 1.55f, BlastSpacing = 100f, DriftRadius = 30f, LockSeconds = 0.45f };
                        case EnemyRoster.Three: return new RosterProgressionTraits { BlastCount = 3f, BlastRadius = 76f, BlastDelay = 1.65f, BlastSpacing = 112f, DriftRadius = 36f, LockSeconds = 0.45f };
                        case EnemyRoster.Four: return new RosterProgressionTraits { BlastCount = 3f, BlastRadius = 84f, BlastDelay = 1.75f, BlastSpacing = 130f, DriftRadius = 42f, LockSeconds = 0.45f };
                    }
                    break;
                case "gunner":
                    switch (tier)
                    {
                        case EnemyRoster.One: return new RosterProgressionTraits { ShotCount = 4f, ShotSpread = 0.1f, CurvatureAcceleration = 210f, CurveGapSlots = 5f, AimWindup = 0.7f };
                        case EnemyRoster.Two: return new RosterProgressionTraits { ShotCount = 4f, ShotSpread = 0.13f, CurvatureAcceleration = 230f, CurveGapSlots = 5f, AimWindup = 0.75f };
                        case EnemyRoster.Three: return new RosterProgressionTraits { ShotCount = 4f, ShotSpread = 0.16f, CurvatureAcceleration = 250f, CurveGapSlots = 5f, AimWindup = 0.8f };
                        case EnemyRoster.Four: return new RosterProgressionTraits { ShotCount = 4f, ShotSpread = 0.19f, CurvatureAcceleration = 270f, CurveGapSlots = 5f, AimWindup = 0.85f };
                    }
                    break;
            }
            return default;
        }
    }
}
