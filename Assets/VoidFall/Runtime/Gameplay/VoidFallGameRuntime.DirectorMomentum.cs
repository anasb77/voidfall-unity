using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        // Authored beats share the encounter clock and admission path; no extra combat loop.
        private int _momentumBeat, _momentumEdge, _momentumCompletedMask;
        private int _arenaIncidentOpportunities;
        private float _incidentOpportunityDeadline;

        private void ResetDirectorMomentum()
        {
            _momentumBeat = _momentumCompletedMask = _arenaIncidentOpportunities = 0;
            _incidentOpportunityDeadline = 0;
            if (UsesSustainedDirector)
                _nextIncidentOpportunity = _time + (_pressureStageIndex > 0 ? 60 : 85 + _runSeed % 36);
        }

        private static string MomentumBeatId(int beat) => beat == 1 ? "elite_escort" :
            beat == 2 ? "rusher_flank" : "incident_fallback";

        private void TryScheduleMomentumBeat(float local)
        {
            if (_pressureStageIndex != 0 || _arenaId != ArenaId.Void) return;
            for (var beat = 1; beat <= 2; beat++)
            {
                var bit = 1 << beat;
                if ((_momentumCompletedMask & bit) != 0) continue;
                var start = beat == 1 ? 270 : 315;
                var end = beat == 1 ? 300 : 336;
                if (local < start) continue;
                if (local >= end)
                {
                    _momentumCompletedMask |= bit;
                    RecordRunHistory("director_signature", MomentumBeatId(beat), "window_expired");
                    continue;
                }
                if (_encounter.Phase != CombatEncounterPhase.Flow || _arrivalGrace > 0 || _pressureReliefTimer > 0 ||
                    _majorIncident.Kind != MajorIncidentKind.None || (beat == 1 && ActiveEnemyTypeCount("elite") >= 2)) return;
                _momentumCompletedMask |= bit;
                BeginMomentumBeat(beat);
                return;
            }
        }

        private void BeginMomentumBeat(int beat)
        {
            BeginSustainedBeat(beat == 2 ? CombatEncounterKind.Flank : CombatEncounterKind.Pursuit);
            _circleBeat = false;
            _momentumBeat = beat;
            _momentumEdge = (int)((_runSeed + (uint)_encounterSequence * 17) % 4);
            _encounter.BeginSustained(_encounter.Kind, 4, 2);
            var side = _momentumEdge == 0 ? "NORTH" : _momentumEdge == 1 ? "SOUTH" : _momentumEdge == 2 ? "WEST" : "EAST";
            ShowArenaToast(beat == 1 ? "ELITE ESCORT · " + side : beat == 2 ? "RUSHER FLANK · " + side : "REINFORCEMENTS · " + side,
                2.5f, ToastKind.Danger);
            _audio?.Play(ProceduralAudio.Cue.Warning, .72f);
            _rosterIntroductionReadyAt = Mathf.Max(_rosterIntroductionReadyAt, _time + 8);
            _nextLegacySwarmAt = Mathf.Max(_nextLegacySwarmAt, _time + 8);
            RecordRunHistory("director_signature", MomentumBeatId(beat), "warning", instanceId: _encounterOwner,
                durationSeconds: 2, detail: "edge=" + _momentumEdge);
        }

        private bool DeployMomentumBeat()
        {
            if (_momentumBeat == 0) return false;
            var beat = _momentumBeat;
            _momentumBeat = 0;
            var admitted = 0;
            var requested = beat == 1 ? 31 : beat == 2 ? 28 : 24;
            var blocked = _pressureReliefTimer > 0 || _arrivalGrace > 0 || ActiveBosses() > 0 ||
                _majorIncident.Kind != MajorIncidentKind.None || DirectorSurvivalSecondsRemaining <= 8;
            if (!blocked)
            {
                var composition = string.Empty;
                for (var i = 0; i < requested && _gameSim.EnemyOrderCount < 700; i++)
                {
                    var id = beat == 1 ? (i == 0 ? "elite" : i < 25 ? "chaser" : "swarmer") :
                        beat == 2 ? (i < 10 ? "runner" : i < 12 ? "dasher" : "chaser") : (i % 3 == 0 ? "swarmer" : "chaser");
                    if (id == "elite" && ActiveEnemyTypeCount("elite") >= 2) id = "chaser";
                    if (id != "elite")
                    {
                        id = ChooseEligibleRestorationFamily(id, BasicFallbacks);
                    }
                    // One entry edge for escort/fallback. Adjacent edges for flank leave an escape half-plane.
                    var edge = beat == 2 && i % 2 != 0 ? (_momentumEdge < 2 ? 2 : 0) : _momentumEdge;
                    var slot = FindInactive(_gameSim.Enemies);
                    if (slot < 0 || !SpawnEnemy(id, SustainedSpawnPosition(edge))) continue;
                    _encounterMembers[slot] = new EncounterMember { SpawnId = _gameSim.Enemies[slot].SpawnId,
                        Owner = _encounterOwner, Movement = EncounterMovement.Natural };
                    if (id == "elite") _nextEliteTime = Mathf.Max(_nextEliteTime, _time + 55);
                    if (composition.Length > 0) composition += ",";
                    composition += id;
                    admitted++;
                }
                RecordRunHistory("director_signature", MomentumBeatId(beat), "composition", instanceId: _encounterOwner,
                    amount: admitted, detail: "sharedStage=" + _sharedDirectorVisitIndex + ";composition=" + composition);
            }
            _encounter.CommitDeployment(admitted);
            RecordRunHistory("director_signature", MomentumBeatId(beat), blocked ? "cancelled_safety_window" : "deployed",
                instanceId: _encounterOwner, amount: admitted, detail: "requested=" + requested + ";edge=" + _momentumEdge);
            return true;
        }

        private bool SustainedIncidentOpportunityDue => UsesSustainedDirector && IncidentArenaEligible() &&
            _arenaIncidentOpportunities < 2 && _time >= _nextIncidentOpportunity &&
            _majorIncident.Kind == MajorIncidentKind.None && LocalDirectorSurvivalSeconds >= 45 &&
            DirectorSurvivalSecondsRemaining > 45;

        private void FinishSustainedIncidentOpportunity(string reason)
        {
            _arenaIncidentOpportunities++;
            _incidentOpportunityDeadline = 0;
            _nextIncidentOpportunity = _time + 100;
            RecordRunHistory("director_incident_opportunity", reason: reason, instanceId: _incidentSequence,
                amount: _arenaIncidentOpportunities, detail: "nextAt=" + _nextIncidentOpportunity + ";maximumPerArena=2");
        }

        private void TrySustainedIncidentOpportunity()
        {
            if (_arenaIncidentOpportunities >= 2 || _time < _nextIncidentOpportunity) return;
            if (_incidentOpportunityDeadline <= 0)
            {
                _incidentSequence++;
                _incidentOpportunityDeadline = _time + 48;
                RecordRunHistory("director_incident_opportunity", reason: "opened", instanceId: _incidentSequence, durationSeconds: 48);
            }
            if (_time >= _incidentOpportunityDeadline || DirectorSurvivalSecondsRemaining <= 45)
            { FinishSustainedIncidentOpportunity("expired_safety_window"); return; }
            _nextIncidentOpportunity = _time + 8;
            if (_arrivalGrace > 0 || _pressureReliefTimer > 0 || _encounter.Phase != CombatEncounterPhase.Flow ||
                LocalDirectorSurvivalSeconds < 45 || !SustainedIncidentOpeningSafe() || ActiveEnemies() < 8 ||
                ActiveEnemies() > DirectorBodyLimit() - 32)
            {
                RecordRunHistory("incident_deferred", reason: "pacing_population_or_unsafe_opening", instanceId: _incidentSequence);
                return;
            }
            var kind = (MajorIncidentKind)(1 + ((_runSeed ^ (uint)(_incidentSequence * 104729)) % 3));
            if (kind == _lastIncidentKind) kind = (MajorIncidentKind)(1 + (int)kind % 3);
            var fallback = _arenaId == ArenaId.RedNebula && ActiveMeteorsCountForIncident() > 2 ? "meteor_fallback" :
                !MajorIncidentRules.CanBegin(kind, DirectorSurvivalSecondsRemaining, false, false, false) ? "duration_fallback" :
                kind == MajorIncidentKind.DestroyerRaid && ActiveEnemyThreat() > DirectorBodyLimit() * 1.55f - 45 ? "threat_fallback" : null;
            if (fallback != null)
            {
                BeginMomentumBeat(3);
                FinishSustainedIncidentOpportunity(fallback);
                return;
            }
            BeginMajorIncident(kind);
            FinishSustainedIncidentOpportunity("incident_started");
        }
    }
}
