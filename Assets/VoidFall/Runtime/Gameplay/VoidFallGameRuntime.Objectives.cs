using UnityEngine;
using UnityEngine.UI;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    /// <summary>
    /// Objective tracking for the current Void. The tracker is pure
    /// observation over the simulation (kills, boss spawns and deaths, zone
    /// holds) and holds none of the golden-master-hashed state, so this
    /// wiring cannot change simulation behavior.
    ///
    /// The HUD line is rebuilt on a fixed 0.25 s cadence inside the tick and
    /// written to the label only when its content changes, matching the
    /// VF-009 no-per-frame-allocation HUD contract.
    /// </summary>
    public sealed partial class VoidFallGameRuntime
    {
        private const float ObjectiveLineRebuildSeconds = 0.25f;

        private VoidObjectiveTracker _objectives;
        private string _objectiveLine;
        private float _objectiveLineTimer;
        private string _lastObjectiveLine;

        private Text _objectiveText;

        /// <summary>
        /// Attaches the escape condition for the arena the run is entering.
        /// Called at run start and whenever the endless clock rotates the
        /// arena mid-run, so the objective always matches the current Void.
        /// </summary>
        private void BeginObjectiveForCurrentArena()
        {
            ResetHydraEncounterState();
            ResetMonochromeEncounterState();
            ResetNullCityEncounterState();
            // With a route active the objective keys on the Void, not the
            // arena: Hydra shares the Abyss arena as a placeholder but must
            // not share its escape condition.
            var key = _voidRoute != null
                ? _voidRoute.CurrentVoidId
                : ArenaCatalogRules.StableId(_arenaId);
            var objective = VoidObjectives.ForArena(key);
            Debug.Log($"VOIDFLOW begin key={key} null={objective == null} t={_time:F1}");
            if (objective == null)
            {
                _objectives?.Clear();
                _objectiveLine = null;
                return;
            }
            if (_objectives == null) _objectives = new VoidObjectiveTracker();
            _objectives.Begin(objective);
            ClearNebulaStrikes();
            _objectiveLine = objective.GetObjectiveText();
            _objectiveLineTimer = ObjectiveLineRebuildSeconds;
            _objectivesCompletionHandled = false;
            _objectiveCompleteAt = -1f;
            _voidBossEncounterSpawned = false;
            _voidCompletionPending = false;
            _voidCompletionDelayRemaining = 0f;
        }

        private void NotifyObjectiveKill() => _objectives?.NotifyKill();

        private void NotifyObjectiveBossSpawned(string bossId)
        {
            Debug.Log($"VOIDFLOW boss-spawned id={bossId} t={_time:F1}");
            _objectives?.NotifyNamedSpawned(bossId);
        }

        private void NotifyObjectiveBossKilled(string bossId)
        {
            Debug.Log($"VOIDFLOW boss-killed id={bossId} t={_time:F1}");
            _objectives?.NotifyNamedKilled(bossId);
        }

        private void StepObjectiveTracker(double deltaTime)
        {
            if (_objectives == null) return;
            _objectives.Step(deltaTime);
            SyncVoidBossEncounterWithObjective();
            if (_objectives.IsComplete && !_objectivesCompletionHandled)
            {
                _objectivesCompletionHandled = true;
                _objectiveCompleteAt = (float)_time;
                OnVoidObjectiveCompleted();
            }
            // Departure/reward time is advanced from Update even while combat is stopped.
            _objectiveLineTimer -= (float)deltaTime;
            if (_objectiveLineTimer > 0) return;
            _objectiveLineTimer = ObjectiveLineRebuildSeconds;
            _objectiveLine = _objectives.Text;
        }
    }
}
