using System;
using System.Buffers;
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
