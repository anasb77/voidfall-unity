using System;
using System.Buffers;
using System.Collections.Generic;
using UnityEngine;
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

        /// <summary>Deferred detonation queue depth for this simulation step.</summary>
        public int PendingMeteorDetonationCount;

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

        /// <summary>Player kinematics and vitals (migrated from the runtime).</summary>
        public PlayerState Player;

        /// <summary>Live count of curved hostile projectiles (browser cap state).</summary>
        public int CurvedShotCount;

        // Hostile-shot advance wiring. The runtime keeps DamagePlayer (health,
        // iframes, death/revive flow, shake, telemetry), so the loop invokes
        // these cached delegates at the exact points the browser resolves an
        // impact. Both are instance-cached; nothing allocates per step.
        public Func<bool> PlayerVulnerableQuery;
        public Action<int, Vector2> HostileShotImpact;

        // Bullet advance wiring. The loop skeleton, homing targeting, identity
        // bookkeeping and hit resolution live here; the runtime supplies the
        // view/FX/damage cascades through these cached delegates at the exact
        // points the browser interleaves them (FX RNG draw order is hashed by
        // the golden master, so the call points must not move).
        public Action<int> BulletTrailHook;            // slot: homing trail emission
        public Action<int, int> BulletEnemyHitHook;    // slot, enemyIndex
        public Action<int, int> BulletBossHitHook;     // slot, bossIndex
        public Func<int, int, bool> BulletMeteorHitHook; // slot, meteorIndex -> consumed?
        public Func<int, bool> BulletRicochetHook;     // slot -> retargeted?

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

        public void ResetEnemyOrder()
        {
            EnemyOrderCount = 0;
            for (var index = 0; index < EnemyOrderPosition.Length; index++)
            {
                EnemyOrder[index] = -1;
                EnemyOrderPosition[index] = -1;
            }
        }
        public void AppendEnemyOrder(int slot)
        {
            if (slot < 0 || slot >= Enemies.Length || EnemyOrderCount >= EnemyOrder.Length) return;
            EnemyOrderPosition[slot] = EnemyOrderCount;
            EnemyOrder[EnemyOrderCount++] = slot;
        }
        public void RemoveEnemyOrder(int slot)
        {
            if (slot < 0 || slot >= Enemies.Length) return;
            var position = EnemyOrderPosition[slot];
            if (position < 0 || position >= EnemyOrderCount || EnemyOrder[position] != slot) return;
            var lastPosition = --EnemyOrderCount;
            if (position != lastPosition)
            {
                var replacement = EnemyOrder[lastPosition];
                EnemyOrder[position] = replacement;
                EnemyOrderPosition[replacement] = position;
            }
            EnemyOrder[lastPosition] = -1;
            EnemyOrderPosition[slot] = -1;
        }
        public void ResetBossOrder()
        {
            BossOrderCount = 0;
            for (var index = 0; index < BossOrder.Length; index++) BossOrder[index] = -1;
        }
        public void AppendBossOrder(int slot)
        {
            if (slot < 0 || slot >= Bosses.Length || BossOrderCount >= BossOrder.Length) return;
            if (Bosses[slot].Active && Array.IndexOf(BossOrder, slot, 0, BossOrderCount) >= 0) return;
            BossOrder[BossOrderCount++] = slot;
        }
        public void RemoveBossOrder(int slot)
        {
            if (slot < 0 || slot >= Bosses.Length) return;
            var position = -1;
            for (var index = 0; index < BossOrderCount; index++)
            {
                if (BossOrder[index] != slot) continue;
                position = index;
                break;
            }
            if (position < 0) return;
            // Source bosses.filter(...) preserves survivor order. Shift rather
            // than swap so removing the first encounter cannot reorder the
            // remaining encounter before a later slot is appended.
            for (var index = position + 1; index < BossOrderCount; index++)
                BossOrder[index - 1] = BossOrder[index];
            BossOrder[--BossOrderCount] = -1;
        }
        public void EnsureBossOrderEntries()
        {
            // Runtime spawns append explicitly. Reconcile active/death-fade
            // fixtures without disturbing the already established order.
            for (var index = 0; index < Bosses.Length; index++)
            {
                if (!Bosses[index].Active && Bosses[index].DeathTimer <= 0) continue;
                var present = false;
                for (var order = 0; order < BossOrderCount; order++)
                {
                    if (BossOrder[order] != index) continue;
                    present = true;
                    break;
                }
                if (!present) AppendBossOrder(index);
            }
            for (var order = BossOrderCount - 1; order >= 0; order--)
            {
                var slot = BossOrder[order];
                if (slot < 0 || (!Bosses[slot].Active && Bosses[slot].DeathTimer <= 0))
                    RemoveBossOrder(slot);
            }
        }
        public void RebuildEnemyGrid()
        {
            EnemyGrid.Clear();
            Array.Clear(EnemyGridSpawnIds, 0, EnemyGridSpawnIds.Length);
            for (var order = 0; order < EnemyOrderCount; order++)
            {
                var index = EnemyOrder[order];
                if (Enemies[index].Active)
                {
                    EnemyGridSpawnIds[index] = Enemies[index].SpawnId;
                    EnemyGrid.Insert(index, Enemies[index].Position.x, Enemies[index].Position.y);
                }
            }
        }
        public bool IsCurrentGridEnemy(int index)
        {
            return index >= 0 && index < Enemies.Length && Enemies[index].Active &&
                EnemyGridSpawnIds[index] == Enemies[index].SpawnId;
        }
        public void ResetMeteorOrder()
        {
            MeteorOrderCount = 0;
            for (var index = 0; index < MeteorOrderPosition.Length; index++)
            {
                MeteorOrder[index] = -1;
                MeteorOrderPosition[index] = -1;
            }
        }
        public void AppendMeteorOrder(int slot)
        {
            if (slot < 0 || slot >= Meteors.Length || MeteorOrderCount >= MeteorOrder.Length) return;
            if (MeteorOrderPosition[slot] >= 0) return;
            MeteorOrderPosition[slot] = MeteorOrderCount;
            MeteorOrder[MeteorOrderCount++] = slot;
        }
        public void RemoveMeteorOrder(int slot)
        {
            if (slot < 0 || slot >= Meteors.Length) return;
            var position = MeteorOrderPosition[slot];
            if (position < 0 || position >= MeteorOrderCount || MeteorOrder[position] != slot) return;
            var lastPosition = --MeteorOrderCount;
            if (position != lastPosition)
            {
                var replacement = MeteorOrder[lastPosition];
                MeteorOrder[position] = replacement;
                MeteorOrderPosition[replacement] = position;
            }
            MeteorOrder[lastPosition] = -1;
            MeteorOrderPosition[slot] = -1;
        }
        public void EnsureMeteorOrderEntries()
        {
            // Runtime spawns append explicitly. This small reconciliation also
            // keeps reflection-seeded PlayMode fixtures deterministic without
            // changing the live compact order.
            for (var index = 0; index < Meteors.Length; index++)
            {
                if (Meteors[index].Active) AppendMeteorOrder(index);
            }
            for (var order = MeteorOrderCount - 1; order >= 0; order--)
            {
                var slot = MeteorOrder[order];
                if (slot < 0 || !Meteors[slot].Active) RemoveMeteorOrder(slot);
            }
        }
        /// <summary>
        /// Advances every active meteor: drift, spin, hit-timer decay, fuse
        /// countdown, and distance culling.
        ///
        /// Fuse-expired meteors are deactivated, removed from the order, and
        /// queued into PendingMeteorDetonations exactly as the browser's
        /// two-phase update requires; their slots are reported through
        /// expiredSlots so the caller can hide views. Distance-culled meteors
        /// are reported through culledSlots without queueing a detonation.
        ///
        /// Player kinematics are still owned by the runtime, so position and
        /// velocity arrive by ref: the meteor-push resolution mutates them
        /// mid-loop exactly as the single-pass original did. Detonation damage
        /// stays on the runtime; only this state half migrated.
        /// </summary>
        public int AdvanceMeteors(
            float dt,
            ref Vector2 playerPosition,
            ref Vector2 playerVelocity,
            bool playerVulnerable,
            int[] expiredSlots,
            int[] culledSlots,
            out int culledCount)
        {
            var expiredCount = 0;
            culledCount = 0;
            // Reverse order walk matches the browser: newest meteors move
            // first, and removals inside the loop swap order tail entries.
            for (var order = MeteorOrderCount - 1; order >= 0; order--)
            {
                var index = MeteorOrder[order];
                var meteor = Meteors[index];
                if (!meteor.Active) continue;
                meteor.Position += meteor.Velocity * dt;
                meteor.Rotation += meteor.Spin * dt;
                meteor.HitTimer = Mathf.Max(0, meteor.HitTimer - dt);
                if (meteor.FuseTimer > 0)
                {
                    meteor.FuseTimer -= dt;
                    if (meteor.FuseTimer <= 0)
                    {
                        meteor.Active = false;
                        RemoveMeteorOrder(index);
                        Meteors[index] = meteor;
                        if (PendingMeteorDetonationCount < PendingMeteorDetonations.Length)
                            PendingMeteorDetonations[PendingMeteorDetonationCount++] = meteor;
                        if (expiredSlots != null && expiredCount < expiredSlots.Length)
                            expiredSlots[expiredCount++] = index;
                        continue;
                    }
                }

                if ((meteor.Position - playerPosition).sqrMagnitude > 1900f * 1900f)
                {
                    meteor.Active = false;
                    RemoveMeteorOrder(index);
                    Meteors[index] = meteor;
                    if (culledSlots != null && culledCount < culledSlots.Length)
                        culledSlots[culledCount++] = index;
                    continue;
                }

                if (playerVulnerable && meteor.FuseTimer <= 0)
                {
                    var push = MeteorRules.ResolveMeteorPush(
                        playerPosition.x,
                        playerPosition.y,
                        new CircleDefinition(meteor.Position.x, meteor.Position.y, meteor.Radius));
                    if (push.Slow)
                    {
                        playerPosition += new Vector2((float)push.PushX, (float)push.PushY);
                        playerVelocity *= (float)MeteorRules.MeteorSlowFactor;
                    }
                }

                Meteors[index] = meteor;
            }

            return expiredCount;
        }

        /// <summary>
        /// Inserts a spawned meteor into an inactive slot: deterministic state
        /// rolls happen here so the combat stream order matches the browser
        /// (drift angle, speed, rotation, spin, seed, in that order), then the
        /// slot joins the pooled order. Returns the slot, or -1 when the pool
        /// is full - the caller abandons further placement attempts either way.
        /// View creation stays on the runtime.
        /// </summary>
        public int TryInsertMeteor(
            Vector2 candidate,
            float radius,
            int variant,
            bool explosive,
            double elapsedSeconds)
        {
            var slot = FindInactive(Meteors);
            if (slot < 0) return -1;
            var driftAngle = (float)(Rng.Next() * Math.PI * 2);
            var speed = 6f + (float)Rng.Next() * 11f;
            var maxHealth = explosive
                ? MeteorRules.ExplosiveMeteorMaxHealth(elapsedSeconds)
                : MeteorRules.MeteorMaxHealth(elapsedSeconds);
            Meteors[slot] = new MeteorState
            {
                Active = true,
                Position = candidate,
                Velocity = new Vector2(Mathf.Cos(driftAngle), Mathf.Sin(driftAngle)) * speed,
                Rotation = (float)(Rng.Next() * Math.PI * 2),
                Spin = ((float)Rng.Next() - 0.5f) * 0.26f,
                Health = maxHealth,
                MaxHealth = maxHealth,
                Radius = radius,
                VisibleRadius = (float)MeteorRules.MeteorVisibleRadius(variant, explosive),
                HitTimer = 0,
                FuseTimer = 0,
                Seed = (float)(Rng.Next() * 100),
                Explosive = explosive,
                Variant = variant,
                View = slot,
            };
            AppendMeteorOrder(slot);
            return slot;
        }

        /// <summary>
        /// Rebuilds the hostile-shot insertion order to exactly the active
        /// shots: appends every active slot the order is missing, then drops
        /// stale entries. Same shape as the runtime's original helper.
        /// </summary>
        public void EnsureHostileShotOrderEntries()
        {
            for (var index = 0; index < HostileShots.Length; index++)
            {
                if (HostileShots[index].Active) HostileShotOrder.Append(index);
            }
            for (var order = HostileShotOrder.Count - 1; order >= 0; order--)
            {
                var slot = HostileShotOrder.SlotAt(order);
                if (slot < 0 || !HostileShots[slot].Active) HostileShotOrder.Remove(slot);
            }
        }

        /// <summary>
        /// Inserts a spawned hostile shot: curved-cap check, slot find, state
        /// write, and order append. No Rng draws happen here, matching the
        /// browser spawnHostileShot. Returns the slot, or -1 when the pool is
        /// full or the curved cap blocks the spawn. View creation stays on the
        /// runtime.
        /// </summary>
        public int TryInsertHostileShot(
            Vector2 position,
            Vector2 direction,
            float damage,
            float speed,
            float curvature,
            bool meteorOwned = false,
            int visualVariant = -1)
        {
            var slot = FindInactive(HostileShots);
            if (slot < 0) return -1;
            // Browser spawnHostileShot uses an exact non-zero check for
            // curvature; tiny valid values still consume the curved-shot cap
            // and receive lateral acceleration.
            var curved = curvature != 0f;
            if (curved && CurvedShotCount >= EliteRules.MaxCurvedProjectiles) return -1;
            // Browser spawnHostileShot stores the supplied nx/ny directly; it
            // does not substitute a right-facing unit vector for zero input.
            var angle = Mathf.Atan2(direction.y, direction.x);
            var acceleration = curved
                ? new Vector2(
                    Mathf.Cos(angle + Mathf.PI / 2) * curvature * (float)EliteRules.CurvedLateralAcceleration,
                    Mathf.Sin(angle + Mathf.PI / 2) * curvature * (float)EliteRules.CurvedLateralAcceleration)
                : Vector2.zero;
            HostileShots[slot] = new HostileShotState
            {
                Active = true,
                Position = position,
                Velocity = direction * speed,
                Acceleration = acceleration,
                Damage = damage,
                Life = curved ? 3.6f : 3.2f,
                Radius = curved ? 7f : 6f,
                Curved = curved,
                MeteorOwned = meteorOwned,
                Variant = visualVariant,
                View = slot,
            };
            HostileShotOrder.Append(slot);
            if (curved) CurvedShotCount++;
            return slot;
        }

        /// <summary>
        /// Advances every active hostile shot: curve acceleration, drift, life
        /// decay, player-impact resolution, and expiry.
        ///
        /// Player impacts call <see cref="HostileShotImpact"/> mid-loop at the
        /// exact point the browser resolves them - DamagePlayer may set
        /// iframes or end the run, which gates later shots in this same pass,
        /// so the impact must not be deferred. Vulnerability beyond health
        /// (game over, revive pending, dying timer, iframes) is queried live
        /// through <see cref="PlayerVulnerableQuery"/> for the same reason.
        /// Expired slots are reported through expiredSlots so the caller can
        /// hide views.
        /// </summary>
        public void AdvanceHostileShots(
            float dt,
            float attackPlayerRadius,
            int[] expiredSlots,
            out int expiredCount)
        {
            expiredCount = 0;
            EnsureHostileShotOrderEntries();
            var initialOrderCount = HostileShotOrder.Count;
            for (var order = initialOrderCount - 1; order >= 0; order--)
            {
                var index = HostileShotOrder.SlotAt(order);
                var shot = HostileShots[index];
                if (!shot.Active)
                {
                    HostileShotOrder.Remove(index);
                    continue;
                }
                if (shot.Curved) shot.Velocity += shot.Acceleration * dt;
                shot.Position += shot.Velocity * dt;
                shot.Life -= dt;
                if (shot.Life > 0 && Player.Health > 0 &&
                    PlayerVulnerableQuery != null && PlayerVulnerableQuery() &&
                    Vector2.Distance(shot.Position, Player.Position) <
                        shot.Radius + attackPlayerRadius)
                {
                    var impactDirection = shot.Velocity / SourceLengthOrOne(shot.Velocity);
                    HostileShotImpact?.Invoke(index, impactDirection);
                    shot.Life = 0;
                }

                if (shot.Life <= 0)
                {
                    shot.Active = false;
                    if (shot.Curved) CurvedShotCount = Mathf.Max(0, CurvedShotCount - 1);
                    if (expiredSlots != null && expiredCount < expiredSlots.Length)
                        expiredSlots[expiredCount++] = index;
                    HostileShotOrder.Remove(index);
                }

                HostileShots[index] = shot;
            }
        }

        private static float SourceLengthOrOne(Vector2 value)
        {
            var length = value.magnitude;
            return length > 0f ? length : 1f;
        }

        public void EnsureBulletOrderEntries()
        {
            for (var index = 0; index < Bullets.Length; index++)
            {
                if (Bullets[index].Active) BulletOrder.Append(index);
            }
            for (var order = BulletOrder.Count - 1; order >= 0; order--)
            {
                var slot = BulletOrder.SlotAt(order);
                if (slot < 0 || !Bullets[slot].Active) BulletOrder.Remove(slot);
            }
        }

        internal static int EnemyIdentity(EnemyState enemy, int slot)
        {
            // SpawnId is the browser-equivalent object identity. A few
            // reflection fixtures construct a zero-initialized EnemyState, so
            // retain deterministic slot identity only for that test-only case.
            return enemy.SpawnId > 0 ? enemy.SpawnId : slot;
        }

        internal static int BossIdentity(BossState boss, int slot)
        {
            // TelemetryInstanceId is the browser boss instance identity. A
            // slot fallback keeps reflection fixtures deterministic.
            return boss.TelemetryInstanceId > 0 ? boss.TelemetryInstanceId : slot + 1;
        }

        private bool BulletAlreadyHitEnemy(BulletState bullet, int enemyIndex)
        {
            if (enemyIndex < 0 || enemyIndex >= Enemies.Length) return false;
            var identity = EnemyIdentity(Enemies[enemyIndex], enemyIndex);
            return bullet.HitEnemy0 == identity || bullet.HitEnemy1 == identity ||
                bullet.HitEnemy2 == identity || bullet.HitEnemy3 == identity;
        }

        private static bool BossAlreadyHit(BulletState bullet, BossState boss, int slot)
        {
            var identity = BossIdentity(boss, slot);
            return bullet.BossHit0 == identity || bullet.BossHit1 == identity ||
                bullet.BossHit2 == identity || bullet.BossHit3 == identity;
        }

        /// <summary>
        /// Nearest-hostile scan in insertion order so equal-distance ties
        /// resolve identically to the browser.
        /// </summary>
        internal HostileTarget FindNearestHostileFrom(
            Vector2 origin,
            float range,
            BulletState bullet,
            bool excludeHitHistory = true,
            HashSet<int> visited = null,
            int[] visitedBuffer = null,
            int visitedCount = 0)
        {
            var target = new HostileTarget
            {
                Valid = false,
                Index = -1,
                DistanceSquared = range * range,
            };
            for (var order = 0; order < EnemyOrderCount; order++)
            {
                var index = EnemyOrder[order];
                var enemy = Enemies[index];
                if (!enemy.Active || enemy.Age < 0.15f ||
                    IsVisited(visited, visitedBuffer, visitedCount, EnemyIdentity(enemy, index)) ||
                    (excludeHitHistory && BulletAlreadyHitEnemy(bullet, index))) continue;
                var distance = (enemy.Position - origin).sqrMagnitude;
                if (distance >= target.DistanceSquared) continue;
                target = new HostileTarget
                {
                    Valid = true,
                    Boss = false,
                    Index = index,
                    Identity = EnemyIdentity(enemy, index),
                    Position = enemy.Position,
                    DistanceSquared = distance,
                };
            }
            EnsureBossOrderEntries();
            for (var bossOrder = 0; bossOrder < BossOrderCount; bossOrder++)
            {
                var index = BossOrder[bossOrder];
                var boss = Bosses[index];
                if (!boss.Active || boss.State == 4 ||
                    IsVisited(visited, visitedBuffer, visitedCount, -BossIdentity(boss, index)) ||
                    (excludeHitHistory && BossAlreadyHit(bullet, boss, index))) continue;
                var distance = (boss.Position - origin).sqrMagnitude;
                if (distance >= target.DistanceSquared) continue;
                target = new HostileTarget
                {
                    Valid = true,
                    Boss = true,
                    Index = index,
                    Identity = BossIdentity(boss, index),
                    Position = boss.Position,
                    DistanceSquared = distance,
                };
            }
            return target;
        }

        private static bool IsVisited(
            HashSet<int> visited,
            int[] visitedBuffer,
            int visitedCount,
            int identity)
        {
            if (visited != null) return visited.Contains(identity);
            for (var index = 0; index < visitedCount; index++)
            {
                if (visitedBuffer[index] == identity) return true;
            }
            return false;
        }

        /// <summary>
        /// Advances every active bullet: homing targeting/steering, trail hook,
        /// drift, life expiry, and the enemy/boss/meteor collision cascade.
        ///
        /// Damage application, FX emission and cluster spawns stay on the
        /// runtime behind the five bullet hooks; each hook is invoked with the
        /// slot's state already published to <see cref="Bullets"/> so nested
        /// reads (cluster charges, damage areas) see this-iteration changes
        /// exactly as the single-loop original did. The local copy is re-read
        /// after every hook because cascades can kill enemies or spawn bullets
        /// into the pool mid-iteration. Deactivated slots are reported through
        /// expiredSlots for view hides.
        /// </summary>
        public void AdvanceBullets(float dt, int[] expiredSlots, out int expiredCount)
        {
            expiredCount = 0;
            EnsureBulletOrderEntries();
            var initialOrderCount = BulletOrder.Count;
            for (var order = initialOrderCount - 1; order >= 0; order--)
            {
                var i = BulletOrder.SlotAt(order);
                var bullet = Bullets[i];
                if (!bullet.Active)
                {
                    BulletOrder.Remove(i);
                    continue;
                }
                if (bullet.Homing)
                {
                    bullet.HomingRefreshTimer -= dt;
                    var target = new HostileTarget
                    {
                        Valid = false,
                        Index = -1,
                    };
                    if (bullet.HomingTargetIndex >= 0)
                    {
                        if (bullet.HomingTargetBoss)
                        {
                            if (bullet.HomingTargetIndex < Bosses.Length)
                            {
                                var boss = Bosses[bullet.HomingTargetIndex];
                                if (boss.Active && boss.State != 4 &&
                                    BossIdentity(boss, bullet.HomingTargetIndex) == bullet.HomingTargetIdentity)
                                {
                                    target = new HostileTarget
                                    {
                                        Valid = true,
                                        Boss = true,
                                        Index = bullet.HomingTargetIndex,
                                        Identity = BossIdentity(boss, bullet.HomingTargetIndex),
                                        Position = boss.Position,
                                        DistanceSquared = (boss.Position - bullet.Position).sqrMagnitude,
                                    };
                                }
                            }
                        }
                        else if (bullet.HomingTargetIndex < Enemies.Length)
                        {
                            var enemy = Enemies[bullet.HomingTargetIndex];
                            if (enemy.Active && enemy.Age >= 0.15f &&
                                EnemyIdentity(enemy, bullet.HomingTargetIndex) == bullet.HomingTargetIdentity)
                            {
                                target = new HostileTarget
                                {
                                    Valid = true,
                                    Boss = false,
                                    Index = bullet.HomingTargetIndex,
                                    Identity = EnemyIdentity(enemy, bullet.HomingTargetIndex),
                                    Position = enemy.Position,
                                    DistanceSquared = (enemy.Position - bullet.Position).sqrMagnitude,
                                };
                            }
                        }
                    }
                    if (bullet.HomingRefreshTimer <= 0 || !target.Valid ||
                        target.DistanceSquared > 620f * 620f * 1.44f)
                    {
                        target = FindNearestHostileFrom(bullet.Position, 620, bullet, false);
                        bullet.HomingTargetIndex = target.Valid ? target.Index : -1;
                        bullet.HomingTargetIdentity = target.Valid ? target.Identity : -1;
                        bullet.HomingTargetBoss = target.Valid && target.Boss;
                        bullet.HomingRefreshTimer = 0.09f + (order % 4) * 0.01f;
                    }
                    if (target.Valid)
                    {
                        var desired = Mathf.Atan2(target.Position.y - bullet.Position.y, target.Position.x - bullet.Position.x);
                        var current = Mathf.Atan2(bullet.Velocity.y, bullet.Velocity.x);
                        var difference = Mathf.Atan2(Mathf.Sin(desired - current), Mathf.Cos(desired - current));
                        var turn = Mathf.Clamp(
                            difference,
                            -bullet.HomingTurnRate * dt,
                            bullet.HomingTurnRate * dt);
                        var speed = bullet.Velocity.magnitude;
                        var angle = current + turn;
                        bullet.Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                    }

                    // Trail emission draws the shared FX RNG on the runtime; it
                    // is a view-side effect but its RNG consumption is hashed.
                    Bullets[i] = bullet;
                    BulletTrailHook?.Invoke(i);
                    bullet = Bullets[i];
                }
                bullet.Position += bullet.Velocity * dt;
                bullet.Life -= dt;
                if (bullet.Life <= 0)
                {
                    bullet.Active = false;
                    Bullets[i] = bullet;
                    if (expiredSlots != null && expiredCount < expiredSlots.Length)
                        expiredSlots[expiredCount++] = i;
                    BulletOrder.Remove(i);
                    continue;
                }
                var hit = false;
                var enemyCandidateCount = EnemyGrid.QueryNeighborhood(
                    bullet.Position.x,
                    bullet.Position.y,
                    1,
                    EnemyGridBulletCandidates);
                for (var candidate = 0; candidate < enemyCandidateCount; candidate++)
                {
                    var enemyIndex = EnemyGridBulletCandidates[candidate];
                    var enemy = Enemies[enemyIndex];
                    if (!IsCurrentGridEnemy(enemyIndex) || BulletAlreadyHitEnemy(bullet, enemyIndex)) continue;
                    var radius = bullet.Radius + enemy.Radius;
                    if (enemy.Age < 0.12f ||
                        (enemy.Position - bullet.Position).sqrMagnitude >= radius * radius) continue;
                    // Browser bullets retain Enemy object identity in hit0..3.
                    // A pooled slot can be reused after a kill, so store the
                    // stable SpawnId rather than the transient array index.
                    bullet.HitEnemy3 = bullet.HitEnemy2;
                    bullet.HitEnemy2 = bullet.HitEnemy1;
                    bullet.HitEnemy1 = bullet.HitEnemy0;
                    bullet.HitEnemy0 = EnemyIdentity(enemy, enemyIndex);
                    bullet.Hits++;
                    Bullets[i] = bullet;
                    BulletEnemyHitHook?.Invoke(i, enemyIndex);
                    bullet = Bullets[i];
                    hit = true;
                    break;
                }

                if (!hit)
                {
                    EnsureBossOrderEntries();
                    for (var bossOrder = 0; bossOrder < BossOrderCount; bossOrder++)
                    {
                        var bossIndex = BossOrder[bossOrder];
                        var boss = Bosses[bossIndex];
                        if (!boss.Active || boss.State == 4 ||
                            BossAlreadyHit(bullet, boss, bossIndex)) continue;
                        var radius = bullet.Radius + boss.Radius;
                        if ((boss.Position - bullet.Position).sqrMagnitude >= radius * radius) continue;
                        bullet.BossHit3 = bullet.BossHit2;
                        bullet.BossHit2 = bullet.BossHit1;
                        bullet.BossHit1 = bullet.BossHit0;
                        bullet.BossHit0 = BossIdentity(boss, bossIndex);
                        // Keep the old slot mask populated for reflection
                        // fixtures; gameplay history uses stable IDs above.
                        bullet.BossHitMask |= 1 << bossIndex;
                        bullet.Hits++;
                        Bullets[i] = bullet;
                        BulletBossHitHook?.Invoke(i, bossIndex);
                        bullet = Bullets[i];
                        hit = true;
                        break;
                    }
                }

                if (!hit)
                {
                    EnsureMeteorOrderEntries();
                    for (var meteorOrder = MeteorOrderCount - 1; meteorOrder >= 0; meteorOrder--)
                    {
                        var meteorIndex = MeteorOrder[meteorOrder];
                        var meteor = Meteors[meteorIndex];
                        if (!meteor.Active || meteor.FuseTimer > 0 || meteor.HitTimer > 0) continue;
                        var radius = bullet.Radius + meteor.Radius;
                        if ((meteor.Position - bullet.Position).sqrMagnitude >= radius * radius) continue;
                        Bullets[i] = bullet;
                        var consumed = BulletMeteorHitHook != null &&
                            BulletMeteorHitHook(i, meteorIndex);
                        bullet = Bullets[i];
                        if (!consumed) continue;
                        bullet.Hits++;
                        hit = true;
                        break;
                    }
                }

                if (hit)
                {
                    if (bullet.Ricochets > 0 && BulletRicochetHook != null && BulletRicochetHook(i))
                    {
                        bullet = Bullets[i];
                        bullet.Ricochets--;
                        bullet.Life = Mathf.Max(bullet.Life, 0.55f);
                    }
                    else if (bullet.PierceRemaining > 0) bullet.PierceRemaining--;
                    else bullet.Active = false;
                }
                if (bullet.Life <= 0) bullet.Active = false;
                Bullets[i] = bullet;
                if (!bullet.Active)
                {
                    if (expiredSlots != null && expiredCount < expiredSlots.Length)
                        expiredSlots[expiredCount++] = i;
                    BulletOrder.Remove(i);
                }
            }
        }

        /// <summary>
        /// Invoked when a pickup is collected, AFTER the slot has been
        /// deactivated and freed - the browser removes the item before running
        /// its effect, which matters for Bomb reward drops reusing the freed
        /// slot within the same step. The runtime hides the view, removes the
        /// order entry and applies the pickup effect. collectedFromPull
        /// carries the pull flag the freed slot no longer holds (it gates the
        /// gem-audio coalescing).
        /// </summary>
        public Action<int, int, bool> PickupCollectedHook;

        /// <summary>
        /// Advances every active pickup: aging, magnet pull acceleration or
        /// velocity decay, drift, and collection detection. Collection frees
        /// the slot first and then invokes <see cref="PickupCollectedHook"/>;
        /// slots reported pulled are counted for the music-reactive magnet.
        /// </summary>
        public void AdvancePickups(
            float dt,
            float magnetRadius,
            float collectRadius,
            bool playerAlive,
            out int pulledXpCount,
            out float pulledXpValue)
        {
            pulledXpCount = 0;
            pulledXpValue = 0f;
            // Browser updatePickups walks the compact pickup array backwards.
            // The pooled runtime keeps the same newest-first swap-removal
            // semantics through a slot-order list; pickups spawned by a
            // same-step effect append after the captured range and wait for
            // the next simulation step, just like browser array growth.
            var initialOrderCount = PickupOrderCount;
            for (var order = initialOrderCount - 1; order >= 0; order--)
            {
                var i = PickupOrder[order];
                var pickup = Pickups[i];
                if (!pickup.Active) continue;
                pickup.Age += dt;
                var delta = Player.Position - pickup.Position;
                var distanceSquared = delta.sqrMagnitude;
                if (playerAlive && (pickup.Pull || distanceSquared < magnetRadius * magnetRadius))
                {
                    pickup.Pull = true;
                    pickup.Speed = Mathf.Min(950, pickup.Speed + 2800 * dt);
                    pickup.Velocity = SourcePickupPullVelocity(delta, pickup.Speed);
                }
                else
                {
                    var decay = Mathf.Exp(-5 * dt);
                    pickup.Velocity *= decay;
                }
                if (pickup.Kind == PickupKind.Xp && pickup.Pull)
                {
                    pulledXpCount++;
                    pulledXpValue += Mathf.Max(0f, pickup.Value);
                }
                pickup.Position += pickup.Velocity * dt;
                var collectedFromPull = pickup.Pull;
                if (playerAlive && distanceSquared < (collectRadius + 7) * (collectRadius + 7))
                {
                    // Free the pooled slot before the effect runs (see hook).
                    pickup.Active = false;
                    pickup.Pull = false;
                    pickup.Velocity = Vector2.zero;
                    pickup.Speed = 0;
                    Pickups[i] = pickup;
                    PickupCollectedHook?.Invoke(i, order, collectedFromPull);
                    // A collected effect may reuse the freed slot (Bomb reward
                    // drops do exactly that): never write the stale value back.
                    continue;
                }
                Pickups[i] = pickup;
            }
        }

        private static Vector2 SourcePickupPullVelocity(Vector2 delta, float speed)
        {
            // Browser updatePickups divides by Math.sqrt(distanceSq) || 1:
            // tiny non-zero offsets still become a full-speed pull vector.
            return delta / SourceLengthOrOne(delta) * speed;
        }

        public void ResetPickupOrder()
        {
            PickupOrderCount = 0;
            for (var index = 0; index < PickupOrderPosition.Length; index++)
                PickupOrderPosition[index] = -1;
        }
        public void AppendPickupOrder(int slot)
        {
            if (slot < 0 || slot >= Pickups.Length || PickupOrderCount >= PickupOrder.Length)
                return;
            if (PickupOrderPosition[slot] >= 0) return;
            PickupOrderPosition[slot] = PickupOrderCount;
            PickupOrder[PickupOrderCount++] = slot;
        }
        public void RemovePickupOrder(int slot)
        {
            if (slot < 0 || slot >= PickupOrderPosition.Length) return;
            var position = PickupOrderPosition[slot];
            if (position >= 0) RemovePickupOrderAt(position);
        }
        public void RemovePickupOrderAt(int position)
        {
            if (position < 0 || position >= PickupOrderCount) return;
            var removedSlot = PickupOrder[position];
            var lastPosition = --PickupOrderCount;
            if (position < lastPosition)
            {
                var movedSlot = PickupOrder[lastPosition];
                PickupOrder[position] = movedSlot;
                PickupOrderPosition[movedSlot] = position;
            }
            PickupOrderPosition[removedSlot] = -1;
        }

        public void SeparateEnemies()
        {
            // The browser runs one bounded grid-backed pair pass after enemy
            // movement. Querying a radius larger than the largest catalog body
            // keeps this exact rule allocation-free while avoiding an O(n^2)
            // scan when the director fills the pool.
            for (var order = 0; order < EnemyOrderCount; order++)
            {
                var index = EnemyOrder[order];
                var enemy = Enemies[index];
                if (!enemy.Active) continue;
                var candidateCount = EnemyGrid.QueryNeighborhood(
                    enemy.Position.x,
                    enemy.Position.y,
                    1,
                    EnemyGridSeparationCandidates);
                for (var candidate = 0; candidate < candidateCount; candidate++)
                {
                    var otherIndex = EnemyGridSeparationCandidates[candidate];
                    var other = Enemies[otherIndex];
                    // The browser resolves each pair once using the immutable
                    // spawn ID, not the pooled array slot. Slot reuse would
                    // otherwise reverse the push order for a live pair.
                    if (!IsCurrentGridEnemy(otherIndex) || other.SpawnId <= enemy.SpawnId) continue;

                    var delta = other.Position - enemy.Position;
                    var distanceSquared = delta.sqrMagnitude;
                    var minimumDistance = SeparationRules.MinimumDistance(enemy.Radius, other.Radius);
                    if (minimumDistance <= 0 || distanceSquared >= minimumDistance * minimumDistance ||
                        distanceSquared < 0.0001f) continue;

                    var distance = Mathf.Sqrt(distanceSquared);

                    var push = SeparationRules.PushMagnitude(minimumDistance, distance);
                    delta *= push;
                    var otherWeight = SeparationRules.OtherWeight(enemy.Radius, other.Radius);
                    enemy.Position -= delta * otherWeight;
                    other.Position += delta * (1f - otherWeight);
                    Enemies[otherIndex] = other;
                }
                Enemies[index] = enemy;
            }
        }
        public int ActiveEnemies()
        {
            var count = 0;
            foreach (var enemy in Enemies) if (enemy.Active) count++;
            return count;
        }
        public int ActiveHostileShots()
        {
            var count = 0;
            foreach (var shot in HostileShots) if (shot.Active) count++;
            return count;
        }
        public static int FindInactive(EnemyState[] states)
        {
            for (var i = 0; i < states.Length; i++) if (!states[i].Active) return i;
            return -1;
        }
        public static int FindInactive(BulletState[] states)
        {
            for (var i = 0; i < states.Length; i++) if (!states[i].Active) return i;
            return -1;
        }
        public static int FindInactive(HostileShotState[] states)
        {
            for (var i = 0; i < states.Length; i++) if (!states[i].Active) return i;
            return -1;
        }
        public static int FindInactive(MeteorState[] states)
        {
            for (var i = 0; i < states.Length; i++) if (!states[i].Active) return i;
            return -1;
        }
        public static int FindInactive(PickupState[] states)
        {
            return FindInactive(states, states != null ? states.Length : 0);
        }
        public static int FindInactive(PickupState[] states, int count)
        {
            if (states == null) return -1;
            var limit = Mathf.Min(states.Length, Mathf.Max(0, count));
            for (var i = 0; i < limit; i++) if (!states[i].Active) return i;
            return -1;
        }
        public static int FindInactive(BossState[] states)
        {
            for (var i = 0; i < states.Length; i++) if (!states[i].Active) return i;
            return -1;
        }
        public EnemyEffectTarget[] CaptureEnemyEffectSnapshot(out int snapshotCount)
        {
            // Browser effects iterate over [...this.enemies]. A copied target
            // list is important because damage can remove an enemy, chain an
            // Exploder, or immediately reuse the pooled slot for a fragment.
            snapshotCount = EnemyOrderCount;
            var snapshot = ArrayPool<EnemyEffectTarget>.Shared.Rent(Math.Max(1, snapshotCount));
            for (var order = 0; order < snapshotCount; order++)
            {
                var slot = EnemyOrder[order];
                snapshot[order] = new EnemyEffectTarget
                {
                    Slot = slot,
                    State = Enemies[slot],
                };
            }
            return snapshot;
        }
        public static void ReleaseEnemyEffectSnapshot(EnemyEffectTarget[] snapshot)
        {
            if (snapshot != null) ArrayPool<EnemyEffectTarget>.Shared.Return(snapshot);
        }
        public bool IsLiveEnemyEffectTarget(EnemyEffectTarget target)
        {
            if (target.Slot < 0 || target.Slot >= Enemies.Length) return false;
            var live = Enemies[target.Slot];
            return live.Active && live.SpawnId == target.State.SpawnId;
        }
        public int ActiveBosses()
        {
            var count = 0;
            foreach (var boss in Bosses) if (boss.Active) count++;
            return count;
        }
        public int ActiveBullets()
        {
            var count = 0;
            foreach (var bullet in Bullets) if (bullet.Active) count++;
            return count;
        }
        public int ActivePickups()
        {
            var count = 0;
            foreach (var pickup in Pickups) if (pickup.Active) count++;
            return count;
        }
        public int ActiveMeteors()
        {
            var count = 0;
            foreach (var meteor in Meteors) if (meteor.Active) count++;
            return count;
        }
    }
}
