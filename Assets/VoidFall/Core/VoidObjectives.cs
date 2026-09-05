using System;
using System.Text;

namespace VoidFall.Core
{
    /// <summary>
    /// The seven reusable objective families from spec §10. Every Void's
    /// escape condition composes these; new Voids add data, not new families.
    /// </summary>
    public static class VoidObjectives
    {
        public static string FormatClock(double seconds)
        {
            var total = Math.Max(0, (int)Math.Floor(seconds));
            return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
        }

        /// <summary>
        /// The escape condition for a Void, keyed by the arena stable id
        /// ("abyss", "red-nebula", ...). Voids without a built objective
        /// return null and the run keeps its endless behavior there.
        ///
        /// Every built Void now shares one cadence: survive five minutes, then
        /// clear its complete boss encounter. Standard Voids choose bosses at
        /// runtime; special Voids retain their authored encounters.
        /// </summary>
        public static IVoidObjective ForArena(string arenaStableId)
        {
            switch (arenaStableId)
            {
                case "abyss":
                    return new MultiPhaseObjective(
                        "ABYSS",
                        new SurviveObjective(VoidProgressionRules.SurvivalSeconds, "Survive"),
                        new BossEncounterObjective("Defeat the Void Boss"));
                case "red-nebula":
                    return new MultiPhaseObjective(
                        "RED NEBULA",
                        new SurviveObjective(VoidProgressionRules.SurvivalSeconds, "Survive"),
                        new BossEncounterObjective("Defeat the Void Boss"));
                case "white-sakura":
                    return new MultiPhaseObjective(
                        "WHITE SAKURA",
                        new SurviveObjective(VoidProgressionRules.SurvivalSeconds, "Survive"),
                        new BossEncounterObjective("Defeat the Void Boss"));
                case "hydra":
                    return new MultiPhaseObjective(
                        "HYDRA",
                        new SurviveObjective(VoidProgressionRules.SurvivalSeconds, "Survive"),
                        new BossEncounterObjective("Defeat the Void Boss"));
                case "monochrome-court":
                    return new MultiPhaseObjective(
                        "MONOCHROME COURT",
                        new SurviveObjective(VoidProgressionRules.SurvivalSeconds, "Survive"),
                        new BossEncounterObjective("Defeat the Void Boss"));
                case "null-city":
                    return new MultiPhaseObjective(
                        "NULL CITY",
                        new SurviveObjective(VoidProgressionRules.SurvivalSeconds, "Survive"),
                        new BossEncounterObjective("Defeat Motherload"));
                default:
                    return null;
            }
        }
    }

    /// <summary>Completes after every boss spawned for one encounter is dead.</summary>
    public sealed class BossEncounterObjective : IVoidObjective
    {
        private readonly string _label;
        private int _spawned;
        private int _killed;
        private bool _begun;

        public BossEncounterObjective(string label)
        {
            _label = string.IsNullOrEmpty(label) ? "Defeat the Void Boss" : label;
        }

        public bool IsComplete => _begun && _spawned > 0 && _killed >= _spawned;
        public double Progress01 => _spawned <= 0 ? 0 : Math.Max(0, Math.Min(1, (double)_killed / _spawned));

        public void BeginObjective()
        {
            _spawned = 0;
            _killed = 0;
            _begun = true;
        }

        public void TickObjective(double deltaTime, in VoidObjectiveFeed feed)
        {
            if (!_begun || IsComplete) return;
            _spawned += Math.Max(0, feed.BossesSpawned);
            _killed = Math.Min(_spawned, _killed + Math.Max(0, feed.BossesKilled));
        }

        public string GetObjectiveText()
        {
            if (_spawned <= 0) return _label;
            if (IsComplete) return _label + " — COMPLETE";
            return _spawned > 1 ? _label + ": " + _killed + " / " + _spawned : _label;
        }
    }

    /// <summary>Survive for a fixed duration (Abyss opening, board cycles).</summary>
    public sealed class SurviveObjective : IVoidObjective
    {
        private readonly double _targetSeconds;
        private readonly string _label;
        private double _elapsed;
        private bool _begun;

        public SurviveObjective(double targetSeconds, string label)
        {
            _targetSeconds = targetSeconds > 0 ? targetSeconds : 1;
            _label = label ?? "Survive";
        }

        public bool IsComplete => _elapsed >= _targetSeconds;
        public double Progress01 => Math.Max(0, Math.Min(1, _elapsed / _targetSeconds));

        public void BeginObjective()
        {
            _begun = true;
            _elapsed = 0;
        }

        public void TickObjective(double deltaTime, in VoidObjectiveFeed feed)
        {
            if (!_begun || IsComplete) return;
            _elapsed += Math.Max(0, deltaTime);
        }

        public string GetObjectiveText()
        {
            return _label + ": " + VoidObjectives.FormatClock(_elapsed) + " / " +
                   VoidObjectives.FormatClock(_targetSeconds);
        }
    }

    /// <summary>Kill one named target (Gatekeeper, Gravekeeper, Hydra Prime).</summary>
    public sealed class KillTargetObjective : IVoidObjective
    {
        private readonly string _targetId;
        private readonly string _label;
        private bool _down;

        public KillTargetObjective(string targetId, string label)
        {
            _targetId = targetId ?? string.Empty;
            _label = label ?? "Kill the target";
        }

        public bool IsComplete => _down;
        public double Progress01 => _down ? 1 : 0;

        public void BeginObjective() { }

        public void TickObjective(double deltaTime, in VoidObjectiveFeed feed)
        {
            if (_down) return;
            if (!string.IsNullOrEmpty(feed.KilledId) &&
                string.Equals(feed.KilledId, _targetId, StringComparison.Ordinal))
            {
                _down = true;
            }
        }

        public string GetObjectiveText() => _down ? _label + " — DOWN" : _label;
    }

    /// <summary>Destroy N structures (Void Anchors, Gene Nodes, Control Nodes, beacons).</summary>
    public sealed class DestroyTargetsObjective : IVoidObjective
    {
        private readonly int _required;
        private readonly string _label;
        private int _destroyed;
        private bool _begun;

        public DestroyTargetsObjective(int required, string label)
        {
            _required = required > 0 ? required : 1;
            _label = label ?? "Targets";
        }

        public bool IsComplete => _destroyed >= _required;
        public double Progress01 => Math.Max(0, Math.Min(1, (double)_destroyed / _required));
        public int Destroyed => _destroyed;

        public void BeginObjective()
        {
            _begun = true;
            _destroyed = 0;
        }

        public void TickObjective(double deltaTime, in VoidObjectiveFeed feed)
        {
            if (!_begun || IsComplete) return;
            // Overflow events beyond the requirement are ignored so a single
            // chain reaction cannot complete and over-count the next phase.
            _destroyed += Math.Max(0, feed.StructuresDestroyed);
        }

        public string GetObjectiveText() => _label + ": " +
            Math.Min(_destroyed, _required) + " / " + _required;
    }

    /// <summary>
    /// Hold a zone for N cumulative seconds (Sakura rift). Leaving the zone
    /// pauses progress; it never resets (spec §13).
    /// </summary>
    public sealed class CaptureZoneObjective : IVoidObjective
    {
        private readonly double _requiredSeconds;
        private readonly string _label;
        private double _held;
        private bool _begun;

        public CaptureZoneObjective(double requiredSeconds, string label)
        {
            _requiredSeconds = requiredSeconds > 0 ? requiredSeconds : 1;
            _label = label ?? "Stabilize Rift";
        }

        public bool IsComplete => _held >= _requiredSeconds;
        public double Progress01 => Math.Max(0, Math.Min(1, _held / _requiredSeconds));

        public void BeginObjective()
        {
            _begun = true;
            _held = 0;
        }

        public void TickObjective(double deltaTime, in VoidObjectiveFeed feed)
        {
            if (!_begun || IsComplete) return;
            _held += Math.Max(0, feed.ZoneHoldSeconds);
        }

        public string GetObjectiveText() => _label + ": " +
            VoidObjectives.FormatClock(_held) + " / " + VoidObjectives.FormatClock(_requiredSeconds);
    }

    /// <summary>Charge the escape with N kills.</summary>
    public sealed class ChargeWithKillsObjective : IVoidObjective
    {
        private readonly int _requiredKills;
        private readonly string _label;
        private int _kills;
        private bool _begun;

        public ChargeWithKillsObjective(int requiredKills, string label)
        {
            _requiredKills = requiredKills > 0 ? requiredKills : 1;
            _label = label ?? "Charge the Rift";
        }

        public bool IsComplete => _kills >= _requiredKills;
        public double Progress01 => Math.Max(0, Math.Min(1, (double)_kills / _requiredKills));
        public int Kills => _kills;

        public void BeginObjective()
        {
            _begun = true;
            _kills = 0;
        }

        public void TickObjective(double deltaTime, in VoidObjectiveFeed feed)
        {
            if (!_begun || IsComplete) return;
            _kills += Math.Max(0, feed.Kills);
        }

        public string GetObjectiveText() => _label + ": " +
            Math.Min(_kills, _requiredKills) + " / " + _requiredKills + " kills";
    }

    /// <summary>
    /// A named boss must spawn and then die. Completion requires both, so a
    /// boss killed before its objective began (previous Void leftovers, a
    /// stolen kill during a transition) does not silently complete it.
    /// </summary>
    public sealed class BossObjective : IVoidObjective
    {
        private readonly string _bossId;
        private readonly string _label;
        private bool _spawned;
        private bool _down;

        public BossObjective(string bossId, string label)
        {
            _bossId = bossId ?? string.Empty;
            _label = label ?? "Kill the boss";
        }

        public bool IsComplete => _down;
        public double Progress01 => _down ? 1 : (_spawned ? 0.5 : 0);
        public bool Spawned => _spawned;

        public void BeginObjective()
        {
            _spawned = false;
            _down = false;
        }

        public void TickObjective(double deltaTime, in VoidObjectiveFeed feed)
        {
            if (!_spawned &&
                !string.IsNullOrEmpty(feed.SpawnedId) &&
                string.Equals(feed.SpawnedId, _bossId, StringComparison.Ordinal))
            {
                _spawned = true;
            }
            if (_spawned && !_down &&
                !string.IsNullOrEmpty(feed.KilledId) &&
                string.Equals(feed.KilledId, _bossId, StringComparison.Ordinal))
            {
                _down = true;
            }
        }

        public string GetObjectiveText() => _down
            ? _label + " — DOWN"
            : (_spawned ? _label + " — ENGAGED" : "Awaiting " + _label);
    }

    /// <summary>
    /// Strictly ordered phases (survive then Gatekeeper; nodes then Hydra
    /// Prime; both board cycles then the Twin Grandmasters). The next phase begins only
    /// after the current one completes; each tick's feed is consumed by
    /// exactly one phase - the boundary batch goes to the newly begun phase.
    /// </summary>
    public sealed class MultiPhaseObjective : IVoidObjective
    {
        private readonly IVoidObjective[] _phases;
        private readonly string _title;
        private int _index;
        private bool _begun;
        private bool _transitionPending;

        public MultiPhaseObjective(string title, params IVoidObjective[] phases)
        {
            _title = title ?? string.Empty;
            _phases = phases ?? new IVoidObjective[0];
        }

        public int PhaseIndex => Math.Min(_index, Math.Max(0, _phases.Length - 1));
        public int PhaseCount => _phases.Length;
        public IVoidObjective CurrentPhase => _phases.Length > 0 ? _phases[PhaseIndex] : null;
        public bool IsComplete => _phases.Length > 0 && _index >= _phases.Length;

        public double Progress01
        {
            get
            {
                if (_phases.Length == 0) return 0;
                if (IsComplete) return 1;
                var sum = (double)_index;
                var current = CurrentPhase;
                if (current != null) sum += current.Progress01;
                return Math.Max(0, Math.Min(1, sum / _phases.Length));
            }
        }

        public void BeginObjective()
        {
            _begun = true;
            _index = 0;
            _transitionPending = false;
            if (_phases.Length > 0) _phases[0].BeginObjective();
        }

        public void TickObjective(double deltaTime, in VoidObjectiveFeed feed)
        {
            if (!_begun || IsComplete) return;
            if (_transitionPending)
            {
                // The previous phase completed on an earlier tick. Begin the
                // next phase and hand it this tick's batch: every batch is
                // consumed by exactly one phase, and a named kill landing on
                // the boundary tick (a boss dying the instant the survive
                // phase ends) is not silently swallowed.
                _transitionPending = false;
                var next = _phases[_index];
                next.BeginObjective();
                next.TickObjective(deltaTime, feed);
                if (next.IsComplete)
                {
                    _index++;
                    if (_index < _phases.Length) _transitionPending = true;
                }
                return;
            }
            var phase = _phases[_index];
            phase.TickObjective(deltaTime, feed);
            if (phase.IsComplete)
            {
                _index++;
                if (_index < _phases.Length) _transitionPending = true;
            }
        }

        public string GetObjectiveText()
        {
            if (_phases.Length == 0) return _title;
            if (IsComplete) return _title + " COMPLETE";
            var builder = new StringBuilder();
            if (_title.Length > 0) builder.Append(_title).Append(" | ");
            builder.Append("Phase ").Append(_index + 1).Append('/').Append(_phases.Length)
                .Append(": ").Append(CurrentPhase.GetObjectiveText());
            return builder.ToString();
        }
    }
}
