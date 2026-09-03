using System;

namespace VoidFall.Core
{
    /// <summary>
    /// Owns the active void objective and its per-tick feed. The host
    /// reports simulation facts into the feed as they happen; Step consumes
    /// the batch once per fixed tick and resets it. Pure observation over
    /// the simulation - the tracker holds none of the hashed state and
    /// touches nothing, so wiring it cannot disturb the golden master.
    /// </summary>
    public sealed class VoidObjectiveTracker
    {
        private VoidObjectiveFeed _feed = new VoidObjectiveFeed();

        public IVoidObjective Objective { get; private set; }

        public bool HasObjective => Objective != null;
        public bool IsComplete => Objective != null && Objective.IsComplete;
        public double Progress01 => Objective != null ? Objective.Progress01 : 0;

        public void Begin(IVoidObjective objective)
        {
            Objective = objective;
            _feed.Reset();
            objective?.BeginObjective();
        }

        /// <summary>Detaches without completing; the HUD line disappears.</summary>
        public void Clear()
        {
            Objective = null;
            _feed.Reset();
        }

        public void NotifyKill() => _feed.Kills++;

        public void NotifyStructureDestroyed() => _feed.StructuresDestroyed++;

        /// <summary>
        /// First named spawn in a tick wins if several land in the same
        /// batch; same-tick duplicate spawns are a prototype non-concern.
        /// </summary>
        public void NotifyNamedSpawned(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            _feed.BossesSpawned++;
            if (_feed.SpawnedId == null) _feed.SpawnedId = id;
        }

        public void NotifyNamedKilled(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            _feed.BossesKilled++;
            if (_feed.KilledId == null) _feed.KilledId = id;
        }

        public void NotifyZoneHold(double seconds) =>
            _feed.ZoneHoldSeconds += Math.Max(0, seconds);

        /// <summary>
        /// One fixed tick: hand the batch to the objective in the order it
        /// was reported, then clear it. Once complete, the objective stops
        /// consuming so late events cannot disturb a finished state.
        /// </summary>
        public void Step(double deltaTime)
        {
            if (Objective == null || Objective.IsComplete)
            {
                _feed.Reset();
                return;
            }
            Objective.TickObjective(deltaTime, _feed);
            _feed.Reset();
        }

        /// <summary>Allocating; read at UI cadence, not per frame.</summary>
        public string Text => Objective != null ? Objective.GetObjectiveText() : null;
    }
}
