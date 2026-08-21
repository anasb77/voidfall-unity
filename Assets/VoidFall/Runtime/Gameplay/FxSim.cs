using System;
using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    /// <summary>Which FX family an insertion-order entry belongs to.</summary>
    internal enum SourceFxKind
    {
        Particle,
        MeteorShard,
        RingWave,
    }

    /// <summary>Simulated meteor shard. Formerly nested in the runtime class.</summary>
    internal struct MeteorShardState
    {
        public bool Active;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Life;
        public float MaxLife;
        public float Size;
        public float Rotation;
        public float Spin;
        public int Variant;
        public int View;
    }

    /// <summary>Simulated ring wave. Formerly nested in the runtime class.</summary>
    internal struct RingWaveState
    {
        public bool Active;
        public Vector2 Position;
        public float StartRadius;
        public float Size;
        public float Growth;
        public float Age;
        public float Life;
        public Color Color;
        public int View;
    }

    /// <summary>Simulated source particle. Formerly nested in the runtime class.</summary>
    internal struct SourceParticleState
    {
        public bool Active;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Life;
        public float MaxLife;
        public float Size;
        public Color Color;
        public int View;
    }

    /// <summary>
    /// Owns the deterministic cosmetic-FX simulation state: source particles,
    /// meteor shards, ring waves, their shared insertion-order bookkeeping,
    /// and the dedicated FX random stream. v0 is state ownership only - the
    /// update/spawn method bodies remain on the runtime until they are split
    /// away from their view-sync halves.
    /// </summary>
    internal sealed class FxSim
    {
        // Source meteorShardDrag: shard velocity decay per second. Matches the
        // runtime's MeteorShardDrag constant; keep the values in sync.
        private const float MeteorShardDragSeconds = 3.2f;

        public readonly MeteorShardState[] MeteorShards;
        public readonly SourceParticleState[] SourceParticles;
        public readonly RingWaveState[] RingWaves;

        public readonly int[] SourceFxOrderKind = new int[0];
        public readonly int[] SourceFxOrderSlot;
        public readonly int[] SourceParticleOrderPosition;
        public readonly int[] MeteorShardOrderPosition;
        public readonly int[] RingWaveOrderPosition;
        public int SourceFxOrderCount;

        /// <summary>The dedicated FX random stream, independent of combat RNG.</summary>
        public Rng FxRng;

        public FxSim(int maxSourceParticles, int maxMeteorShards, int maxRingWaves, uint fxSeed)
        {
            MeteorShards = new MeteorShardState[maxMeteorShards];
            SourceParticles = new SourceParticleState[maxSourceParticles];
            RingWaves = new RingWaveState[maxRingWaves];
            SourceFxOrderKind = new int[maxSourceParticles];
            SourceFxOrderSlot = new int[maxSourceParticles];
            SourceParticleOrderPosition = new int[maxSourceParticles];
            MeteorShardOrderPosition = new int[maxMeteorShards];
            RingWaveOrderPosition = new int[maxRingWaves];
            FxRng = new Rng(fxSeed);
        }

        /// <summary>
        /// Advances source-particle simulation. Expired slot indices are
        /// written to <paramref name="expired"/> so the caller can hide views;
        /// the state outcome does not depend on when that happens.
        /// </summary>
        public int AdvanceSourceParticles(float dt, int[] expired)
        {
            if (dt <= 0) return 0;
            var expiredCount = 0;
            var decay = Mathf.Exp(-3.2f * Mathf.Clamp(dt, 0, 0.1f));
            for (var index = 0; index < SourceParticles.Length; index++)
            {
                var particle = SourceParticles[index];
                if (!particle.Active) continue;
                particle.Life -= dt;
                if (particle.Life <= 0)
                {
                    particle.Active = false;
                    RemoveSourceFxOrder(SourceFxKind.Particle, index);
                    expired[expiredCount++] = index;
                }
                else
                {
                    particle.Velocity *= decay;
                    particle.Position += particle.Velocity * dt;
                }
                SourceParticles[index] = particle;
            }
            return expiredCount;
        }

        public int AdvanceMeteorShards(float dt, int[] expired)
        {
            var expiredCount = 0;
            for (var index = 0; index < MeteorShards.Length; index++)
            {
                var shard = MeteorShards[index];
                if (!shard.Active) continue;
                var decay = Mathf.Exp(-MeteorShardDragSeconds * dt);
                shard.Velocity *= decay;
                shard.Position += shard.Velocity * dt;
                shard.Life -= dt;
                if (shard.Life <= 0)
                {
                    shard.Active = false;
                    RemoveSourceFxOrder(SourceFxKind.MeteorShard, index);
                    expired[expiredCount++] = index;
                }
                MeteorShards[index] = shard;
            }
            return expiredCount;
        }

        public int AdvanceRingWaves(float dt, int[] expired)
        {
            var expiredCount = 0;
            for (var index = 0; index < RingWaves.Length; index++)
            {
                var wave = RingWaves[index];
                if (!wave.Active) continue;
                wave.Age += dt;
                wave.Size += wave.Growth * dt;
                if (wave.Age >= wave.Life)
                {
                    wave.Active = false;
                    RemoveSourceFxOrder(SourceFxKind.RingWave, index);
                    expired[expiredCount++] = index;
                }
                RingWaves[index] = wave;
            }
            return expiredCount;
        }

        public int FindSourceParticleSlot()
        {
            for (var index = 0; index < SourceParticles.Length; index++)
                if (!SourceParticles[index].Active) return index;
            return -1;
        }

        public int FindMeteorShardSlot()
        {
            var oldest = -1;
            var lowestLife = float.MaxValue;
            for (var index = 0; index < MeteorShards.Length; index++)
            {
                if (!MeteorShards[index].Active) return index;
                if (MeteorShards[index].Life < lowestLife)
                {
                    lowestLife = MeteorShards[index].Life;
                    oldest = index;
                }
            }
            return oldest;
        }

        public void ResetSourceFxOrder()
        {
            SourceFxOrderCount = 0;
            for (var index = 0; index < SourceFxOrderKind.Length; index++)
            {
                SourceFxOrderKind[index] = -1;
                SourceFxOrderSlot[index] = -1;
            }
            for (var index = 0; index < SourceParticleOrderPosition.Length; index++)
                SourceParticleOrderPosition[index] = -1;
            for (var index = 0; index < MeteorShardOrderPosition.Length; index++)
                MeteorShardOrderPosition[index] = -1;
            for (var index = 0; index < RingWaveOrderPosition.Length; index++)
                RingWaveOrderPosition[index] = -1;
        }

        private int OrderPosition(SourceFxKind kind, int slot)
        {
            if (slot < 0) return -1;
            switch (kind)
            {
                case SourceFxKind.Particle:
                    return slot < SourceParticleOrderPosition.Length
                        ? SourceParticleOrderPosition[slot]
                        : -1;
                case SourceFxKind.MeteorShard:
                    return slot < MeteorShardOrderPosition.Length
                        ? MeteorShardOrderPosition[slot]
                        : -1;
                case SourceFxKind.RingWave:
                    return slot < RingWaveOrderPosition.Length
                        ? RingWaveOrderPosition[slot]
                        : -1;
                default:
                    return -1;
            }
        }

        private void SetOrderPosition(SourceFxKind kind, int slot, int position)
        {
            if (slot < 0) return;
            switch (kind)
            {
                case SourceFxKind.Particle:
                    if (slot < SourceParticleOrderPosition.Length)
                        SourceParticleOrderPosition[slot] = position;
                    break;
                case SourceFxKind.MeteorShard:
                    if (slot < MeteorShardOrderPosition.Length)
                        MeteorShardOrderPosition[slot] = position;
                    break;
                case SourceFxKind.RingWave:
                    if (slot < RingWaveOrderPosition.Length)
                        RingWaveOrderPosition[slot] = position;
                    break;
            }
        }

        public void AppendSourceFxOrder(SourceFxKind kind, int slot)
        {
            if (slot < 0 || SourceFxOrderCount >= SourceFxOrderKind.Length) return;
            var position = OrderPosition(kind, slot);
            if (position >= 0 && position < SourceFxOrderCount &&
                SourceFxOrderKind[position] == (int)kind &&
                SourceFxOrderSlot[position] == slot)
            {
                return;
            }
            var order = SourceFxOrderCount++;
            SourceFxOrderKind[order] = (int)kind;
            SourceFxOrderSlot[order] = slot;
            SetOrderPosition(kind, slot, order);
        }

        public void RemoveSourceFxOrder(SourceFxKind kind, int slot)
        {
            var position = OrderPosition(kind, slot);
            if (position < 0 || position >= SourceFxOrderCount ||
                SourceFxOrderKind[position] != (int)kind ||
                SourceFxOrderSlot[position] != slot)
            {
                SetOrderPosition(kind, slot, -1);
                return;
            }

            var lastPosition = --SourceFxOrderCount;
            if (position != lastPosition)
            {
                var replacementKind = (SourceFxKind)SourceFxOrderKind[lastPosition];
                var replacementSlot = SourceFxOrderSlot[lastPosition];
                SourceFxOrderKind[position] = (int)replacementKind;
                SourceFxOrderSlot[position] = replacementSlot;
                SetOrderPosition(replacementKind, replacementSlot, position);
            }
            SourceFxOrderKind[lastPosition] = -1;
            SourceFxOrderSlot[lastPosition] = -1;
            SetOrderPosition(kind, slot, -1);
        }

        public bool SourceFxEntryActive(SourceFxKind kind, int slot)
        {
            switch (kind)
            {
                case SourceFxKind.Particle:
                    return slot >= 0 && slot < SourceParticles.Length && SourceParticles[slot].Active;
                case SourceFxKind.MeteorShard:
                    return slot >= 0 && slot < MeteorShards.Length && MeteorShards[slot].Active;
                case SourceFxKind.RingWave:
                    return slot >= 0 && slot < RingWaves.Length && RingWaves[slot].Active;
                default:
                    return false;
            }
        }

        public void EnsureSourceFxOrderEntries()
        {
            // Runtime spawns append explicitly. Reconcile active reflection
            // fixtures without disturbing entries that already have source
            // insertion order.
            for (var index = 0; index < SourceParticles.Length; index++)
                if (SourceParticles[index].Active)
                    AppendSourceFxOrder(SourceFxKind.Particle, index);
            for (var index = 0; index < MeteorShards.Length; index++)
                if (MeteorShards[index].Active)
                    AppendSourceFxOrder(SourceFxKind.MeteorShard, index);
            for (var index = 0; index < RingWaves.Length; index++)
                if (RingWaves[index].Active)
                    AppendSourceFxOrder(SourceFxKind.RingWave, index);

            for (var order = SourceFxOrderCount - 1; order >= 0; order--)
            {
                var kind = (SourceFxKind)SourceFxOrderKind[order];
                var slot = SourceFxOrderSlot[order];
                if (!SourceFxEntryActive(kind, slot))
                    RemoveSourceFxOrder(kind, slot);
            }
        }
    }
}
