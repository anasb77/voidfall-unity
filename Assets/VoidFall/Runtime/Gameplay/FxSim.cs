using System;
using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
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
    }
}
