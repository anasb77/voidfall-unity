using UnityEngine;
using VoidFall.Core;
// Combat simulation data types promoted from nested private declarations
// of VoidFallGameRuntime so GameSim can own them. Pure data; no behaviour.
namespace VoidFall.Runtime
{

        internal struct EnemyState
        {
            public bool Active;
            public string Id;
            public Vector2 Position;
            public Vector2 Velocity;
            public Vector2 Knockback;
            public float Health;
            public float MaxHealth;
            public float Radius;
            public float Speed;
            public float Damage;
            public float Xp;
            public float Age;
            public float HitTimer;
            public float BlockCooldown;
            public float ContactCooldown;
            public float BladeCooldown;
            public float AttackCooldown;
            public float TelegraphPulseTimer;
            public float StateTimer;
            public float Shield;
            public float MaxShield;
            public float StoredXp;
            public Vector2 DashDirection;
            public Vector2 AimPosition;
            public Vector2 Facing;
            public float Rotation;
            public float Spin;
            public float HollowCooldown;
            public float Seed;
            public EnemyRoster Roster;
            public MutationGene MutationGene;
            public bool Elite;
            public EliteVariantId? EliteKind;
            public bool CarrierDrone;
            public bool SplitterFragment;
            public int SummonedByBossTelemetryId;
            public bool MatriarchBodyguard;
            public int MatriarchBodyguardSlot;
            public int SummonedByCarrierSpawnId;
            public int SpawnId;
            public int State;
            public int Volley;
            public int View;
        }

        internal struct EnemyEffectTarget
        {
            public int Slot;
            public EnemyState State;
        }

        internal struct BulletState
        {
            public bool Active;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Damage;
            public float Life;
            public float Radius;
            public int WeaponIndex;
            public int Rank;
            public int PierceRemaining;
            public int HitEnemy0;
            public int HitEnemy1;
            public int HitEnemy2;
            public int HitEnemy3;
            public int BossHitMask;
            public int BossHit0;
            public int BossHit1;
            public int BossHit2;
            public int BossHit3;
            public int Ricochets;
            public float Knockback;
            public float BlastRadius;
            public bool Homing;
            public float HomingTurnRate;
            public int HomingTargetIndex;
            public int HomingTargetIdentity;
            public bool HomingTargetBoss;
            public float HomingRefreshTimer;
            public bool Cluster;
            public bool Evolved;
            public int Hits;
            public int View;
        }

        internal struct HostileShotState
        {
            public bool Active;
            public Vector2 Position;
            public Vector2 Velocity;
            public Vector2 Acceleration;
            public float Damage;
            public float Life;
            public float Radius;
            public bool Curved;
            public bool MeteorOwned;
            public int Variant;
            public int View;
        }

        internal struct MeteorState
        {
            public bool Active;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Rotation;
            public float Spin;
            public float Health;
            public float MaxHealth;
            public float Radius;
            public float VisibleRadius;
            public float HitTimer;
            public float FuseTimer;
            public float Seed;
            public bool Explosive;
            public int Variant;
            public int View;
        }

        internal enum PickupKind
        {
            Xp,
            Part,
            Magnet,
            Repair,
            Bomb,
            Overdrive,
            TrackShift,
        }

        internal struct PickupState
        {
            public bool Active;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Value;
            public float Age;
            public float Speed;
            public PickupKind Kind;
            public bool Pull;
            public int View;
        }

        internal struct BossState
        {
            public bool Active;
            public string Id;
            public Vector2 Position;
            public float Health;
            public float MaxHealth;
            public float Radius;
            public float Speed;
            public float Damage;
            public float DamageScale;
            public float ContactCooldown;
            public float HitTimer;
            public float ShieldHitTimer;
            public float BladeCooldown;
            public float HollowCooldown;
            public float AttackCooldown;
            public float StateTimer;
            public float DeathTimer;
            public Vector2 DashDirection;
            public Vector2 TargetPosition;
            public float AttackAngle;
            public BossAttackDefinition ActiveAttack;
            public int State;
            public int AttackIndex;
            public bool ActionApplied;
            public bool Reinforced;
            public bool TierPressureTriggered;
            public int PressureTier;
            public float BeamHitCooldown;
            public int EncounterIndex;
            public int TelemetryInstanceId;
            public int HydraStep;
            public float HydraAttackElapsed;
            public int View;
        }

        /// <summary>
        /// Player kinematics and vitals. Migrated from runtime fields so combat
        /// families (meteors today, bullets/hostile shots next) can read and
        /// mutate the player without by-ref parameter plumbing. Flow flags that
        /// drive UI navigation (revive pending, game over) stay on the runtime.
        /// </summary>
        internal struct PlayerState
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Health;
            public float MaxHealth;
            public float Iframes;
            public float DyingTimer;
        }

        /// <summary>
        /// Nearest-hostile query result. Promoted from a private runtime nested
        /// type so GameSim's homing/ricochet logic can share it.
        /// </summary>
        internal struct HostileTarget
        {
            public bool Valid;
            public bool Boss;
            public int Index;
            public int Identity;
            public Vector2 Position;
            public float DistanceSquared;
        }
}
