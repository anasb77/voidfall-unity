using System;

namespace VoidFall.Core
{
    /// <summary>
    /// One tick's worth of simulation facts an objective may consume. The host
    /// fills the feed before calling TickObjective and resets it afterwards;
    /// objectives must not keep a reference to it between ticks.
    /// </summary>
    public struct VoidObjectiveFeed
    {
        public int Kills;
        public int StructuresDestroyed;
        public string SpawnedId;
        public string KilledId;
        public int BossesSpawned;
        public int BossesKilled;
        public double ZoneHoldSeconds;

        public void Reset()
        {
            Kills = 0;
            StructuresDestroyed = 0;
            SpawnedId = null;
            KilledId = null;
            BossesSpawned = 0;
            BossesKilled = 0;
            ZoneHoldSeconds = 0;
        }
    }

    /// <summary>
    /// Void escape conditions (spec §10). Objectives are pure state machines:
    /// they consume the feed the host reports each fixed tick and never touch
    /// the simulation directly, so any Void's objective is testable headless
    /// and costs nothing on the render path. GetObjectiveText is the only
    /// allocating member and must only be called at UI frequency.
    /// </summary>
    public interface IVoidObjective
    {
        void BeginObjective();
        void TickObjective(double deltaTime, in VoidObjectiveFeed feed);
        bool IsComplete { get; }
        double Progress01 { get; }
        string GetObjectiveText();
    }
}
