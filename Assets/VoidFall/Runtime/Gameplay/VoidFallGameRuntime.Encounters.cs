using System;
using UnityEngine;
using VoidFall.Core;
using VoidFall.UI;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private enum EncounterMovement { None, Crossing, Volley, Withdrawal, Natural }
        private struct EncounterMember
        {
            public int SpawnId, Owner;
            public EncounterMovement Movement;
            public Vector2 Direction, Aim;
            public float Remaining, AttackTimer, Windup;
        }

        private readonly CombatEncounterClock _encounter = new CombatEncounterClock();
        private readonly EncounterMember[] _encounterMembers = new EncounterMember[MaxEnemies];
        private readonly LineRenderer[] _encounterWarnings = new LineRenderer[8];
        private readonly int[] _directorActorIds = new int[MaxEnemies];
        private readonly float[] _directorActorExpiry = new float[MaxEnemies];
        private bool _encounterInitialized, _bossWindowStarted, _bossLeadStarted;
        private int _encounterSequence, _encounterOwner, _bossReinforcementWaves;
        private float _nextEncounterTime, _nextBossReinforcementTime;
        private Vector2 _encounterOrigin, _encounterAxis, _encounterAcross;
        private float _encounterHalfDepth, _encounterHalfSpan, _encounterGapOffset;
        private string _lastSpawnBlockReason;

        public string CurrentEncounterPhase => _encounter.Phase.ToString();
        public string LastSpawnBlockReason => _lastSpawnBlockReason;
        private float DirectorChallengeSeconds => _runPressure.CreditedProgressSeconds <= 0
            ? 0 : Mathf.Clamp01(_runPressure.ProgressionPressureHundredths / (float)DirectorProfiles.For(_runDirectorProfile).PressureCeilingHundredths) * 2400f;
        private float LocalDirectorSurvivalSeconds => _objectives?.Objective is MultiPhaseObjective phases
            ? (float)(VoidProgressionRules.SurvivalSeconds * (phases.PhaseIndex > 0 ? 1 : phases.CurrentPhase.Progress01)) : 0;
        private float DirectorSurvivalSecondsRemaining => Mathf.Max(0, (float)VoidProgressionRules.SurvivalSeconds - LocalDirectorSurvivalSeconds);

        private float DurationAdjustedDifficultySeconds
        {
            get
            {
                if (_stressScenario != null || _voidRoute == null || !(_objectives?.Objective is MultiPhaseObjective)) return _time;
                // The captured entry stage remains stable through rewards, when completedVoids already advanced.
                // Remove only the added survival time; elapsed boss combat still advances the original stat curves.
                var survivalVisits = Mathf.Max(0, _pressureStageIndex) + LocalDirectorSurvivalSeconds / (float)VoidProgressionRules.SurvivalSeconds;
                var addedSeconds = (float)(VoidProgressionRules.SurvivalSeconds - VoidProgressionRules.BossDifficultyReferenceSurvivalSeconds);
                return Mathf.Max(0, _time - addedSeconds * survivalVisits);
            }
        }

        private void ResetEncounterDirector()
        {
            _encounter.Reset();
            Array.Clear(_encounterMembers, 0, _encounterMembers.Length);
            Array.Clear(_directorActorIds, 0, _directorActorIds.Length);
            Array.Clear(_directorActorExpiry, 0, _directorActorExpiry.Length);
            _encounterInitialized = true;
            _encounterSequence = _encounterOwner = 0;
            _nextEncounterTime = _time + 14;
            _spawnTimer = Mathf.Max(.5f, RunOpeningSeconds - _time);
            _bossWindowStarted = _bossLeadStarted = false;
            _bossReinforcementWaves = 0;
            _lastSpawnBlockReason = "opening";
            ResetSustainedDirector();
            if (UsesSustainedDirector) _nextEncounterTime = _time + 30;
            HideEncounterWarnings();
        }

        private void CancelEncounterDirector()
        {
            _encounter.Reset();
            HideEncounterWarnings();
            _spawnTimer = .65f;
            _nextEncounterTime = _time + 12;
        }

        private int DirectorBodyLimit()
        {
            if (_stressScenario != null) return MaxEnemies; // Explicit diagnostic pool-fill exception.
            if (UsesSustainedDirector) return MaxEnemies;
            if (CurrentVoidIsNullCity) return _nullCityBossActive ? 28 : 56;
            var cap = DirectorProfiles.PopulationLimit(_runDirectorProfile, PressureHundredths);
            return ActiveBosses() > 0 ? Mathf.Min(cap, 24) : Mathf.Min(MaxEnemies, cap);
        }

        private bool AdmitDirectorSpawn(string id, EnemyRoster roster, EliteVariantId? variant, bool elite)
        {
            if (!_encounterInitialized || _stressScenario != null) return true;
            if (ActiveEnemies() >= DirectorBodyLimit())
            {
                _lastSpawnBlockReason = "population";
                RecordRunHistory("spawn_rejected", id, _lastSpawnBlockReason);
                return false;
            }
            if (CurrentVoidIsNullCity || CurrentVoidIsMonochrome) return true;
            var cost = variant.HasValue ? (float)EliteRules.EliteVariantDef(variant.Value).ThreatCost
                : elite ? 8 : (float)(DirectorRules.EnemyThreatCost(id) * EnemyRosterRules.ThreatMultiplier(roster));
            var budget = DirectorBodyLimit() * (ActiveBosses() > 0 ? 1.25f : 1.55f);
            if (ActiveEnemyThreat() + cost > budget)
            {
                _lastSpawnBlockReason = "threat";
                RecordRunHistory("spawn_rejected", id, _lastSpawnBlockReason, amount: cost);
                return false;
            }
            _lastSpawnBlockReason = null;
            return true;
        }

        private static bool IsDemandingEnemy(string id) => id == "gunner" || id == "twinGunner" ||
            id == "mortar" || id == "dasher" || id == "exploder" || id == "elite";

        private int ActiveDemandingEnemies()
        {
            var count = 0;
            for (var i = 0; i < _gameSim.Enemies.Length; i++)
                if (_gameSim.Enemies[i].Active && (IsDemandingEnemy(_gameSim.Enemies[i].Id) || _gameSim.Enemies[i].EliteKind.HasValue)) count++;
            return count;
        }

        private void RegisterDirectorActor(int slot, EnemyState enemy)
        {
            _directorActorIds[slot] = enemy.SpawnId;
            _directorActorExpiry[slot] = _time + (enemy.Elite ? 65 : 48);
            _encounterMembers[slot] = default;
        }

        private void UpdateEncounterSpawns(float dt)
        {
            if (dt <= 0) return;
            if (UsesSustainedDirector) { UpdateSustainedDirectorSpawns(dt); return; }
            if (_majorIncident.Kind != MajorIncidentKind.None)
            { _spawnTimer = .65f; _lastSpawnBlockReason = "major incident reservation"; return; }
            _directorActive = _directorWarned = false;
            if (_stressScenario != null)
            {
                // Stress scenarios deliberately maintain a fixed concentrated workload;
                // ordinary encounter sequencing is validated by normal-run fixtures.
                _spawnTimer = Mathf.Max(0, _spawnTimer - dt);
                if (_spawnTimer <= 0) { _spawnTimer = .5f; SpawnDirectorAmbient(3, false); }
                return;
            }
            if (_time < RunOpeningSeconds) { _spawnTimer = RunOpeningSeconds - _time; _lastSpawnBlockReason = "opening"; return; }
            var bossActive = ActiveBosses() > 0;
            if (bossActive)
            {
                if (!_bossWindowStarted)
                {
                    CancelEncounterDirector();
                    BeginDirectorWithdrawal(true);
                    _bossWindowStarted = true;
                    _nextBossReinforcementTime = _time + 8;
                }
                _spawnTimer = .65f;
                // A finite pair of reinforcement groups, then a genuinely open fight.
                if (_bossReinforcementWaves < 2 && _time >= _nextBossReinforcementTime && ActiveEnemies() < 4)
                {
                    var allowance = _runDirectorProfile == DirectorProfileId.Standard ? 5 : _runDirectorProfile == DirectorProfileId.Veteran ? 8 : 11;
                    SpawnDirectorAmbient(allowance, true);
                    _bossReinforcementWaves++;
                    _nextBossReinforcementTime = _time + 18;
                }
                _lastSpawnBlockReason = "boss engagement window";
                return;
            }
            if (DirectorSurvivalSecondsRemaining <= 15 && _stressScenario == null)
            {
                if (!_bossLeadStarted) { CancelEncounterDirector(); BeginDirectorWithdrawal(true); _bossLeadStarted = true; }
                _spawnTimer = .65f; _lastSpawnBlockReason = "boss lead-in"; return;
            }

            var previous = _encounter.Phase;
            _encounter.Step(dt, CountEncounterMembers(), HasIncomingDirectorThreat(), DirectorOpeningSafe());
            if (_encounter.NeedsWithdrawal) BeginDirectorWithdrawal(_encounter.PhaseSeconds >= 6);
            if (previous != _encounter.Phase && _encounter.Phase == CombatEncounterPhase.Recovery)
            {
                HideEncounterWarnings();
                if (_encounter.Admitted > 0 && !_encounter.TimedOut) _score += 75;
            }
            if (previous == CombatEncounterPhase.Recovery && _encounter.Phase == CombatEncounterPhase.Flow)
                _nextEncounterTime = _time + (_runDirectorProfile == DirectorProfileId.Standard ? 14 : _runDirectorProfile == DirectorProfileId.Veteran ? 10 : 7);
            if (_encounter.Phase == CombatEncounterPhase.Deployment) DeployDirectorEncounter();
            if (_encounter.Phase != CombatEncounterPhase.Flow)
            {
                _spawnTimer = .65f; _lastSpawnBlockReason = _encounter.Phase == CombatEncounterPhase.Recovery ? "recovery" : "encounter reservation";
                return;
            }
            var formationsEligible = !CurrentVoidIsEonSea && !CurrentVoidIsCrascendo && _arenaId != ArenaId.Hydra;
            if (formationsEligible && _time >= _nextEncounterTime && DirectorSurvivalSecondsRemaining > 30 && DirectorOpeningSafe())
            {
                BeginDirectorEncounter((_encounterSequence & 1) == 0 ? CombatEncounterKind.Crossing : CombatEncounterKind.Volley);
                return;
            }
            if (_time >= _nextEncounterTime && !DirectorOpeningSafe())
            {
                RecordRunHistory("encounter_deferred", reason: "unsafe_opening");
                _nextEncounterTime = _time + 3;
            }
            if (ActiveEnemies() >= DirectorBodyLimit())
            { _spawnTimer = .45f; _lastSpawnBlockReason = "population"; return; }
            _spawnTimer = Mathf.Max(0, _spawnTimer - dt);
            if (_spawnTimer > 0) return;
            _spawnTimer = Mathf.Lerp(.64f, .35f, DirectorChallengeSeconds / 2400f);
            var batch = 1 + Mathf.FloorToInt(DirectorChallengeSeconds / 900) + (_runDirectorProfile == DirectorProfileId.Standard ? 0 : 1);
            SpawnDirectorAmbient(batch, false);
        }

        private void SpawnDirectorAmbient(int count, bool bossReinforcement)
        {
            for (var i = 0; i < count; i++)
            {
                var id = bossReinforcement ? (i % 3 == 0 ? "runner" : "chaser") : ChooseAmbientEnemy();
                if (!AmbientTypeAllowed(id)) id = "chaser";
                if (IsDemandingEnemy(id) && ActiveDemandingEnemies() >= DirectorProfiles.AttackLimit(_runDirectorProfile, PressureHundredths))
                {
                    RecordRunHistory("spawn_substituted", id, "demanding_limit", sourceId: "chaser");
                    id = "chaser";
                }
                RecordRunHistory("director_choice", id, bossReinforcement ? "boss_reinforcement" : "ambient");
                if (!SpawnEnemy(id)) break;
            }
            if (!bossReinforcement && _time >= _nextEliteVariantTime && _time > 150 && ActiveDemandingEnemies() == 0)
            {
                TrySpawnEliteVariant("chaser");
                _nextEliteVariantTime = _time + (_runDirectorProfile == DirectorProfileId.Standard ? 65 : 48);
            }
        }

        private void BeginDirectorEncounter(CombatEncounterKind kind)
        {
            _encounterSequence++;
            _encounterOwner++;
            var hash = _runSeed ^ (uint)(_completedVoids * 7919 + _encounterSequence * 104729);
            hash ^= hash >> 16;
            var direction = (int)(hash & 3);
            _encounterAxis = direction == 0 ? Vector2.right : direction == 1 ? Vector2.left : direction == 2 ? Vector2.up : Vector2.down;
            _encounterAcross = new Vector2(-_encounterAxis.y, _encounterAxis.x);
            _encounterOrigin = _gameSim.Player.Position;
            var viewport = GameplayViewportHalfExtent();
            _encounterHalfDepth = Mathf.Abs(_encounterAxis.x) > .5f ? viewport.x : viewport.y;
            _encounterHalfSpan = Mathf.Abs(_encounterAxis.x) > .5f ? viewport.y : viewport.x;
            _encounterGapOffset = ((hash >> 3) % 3 - 1f) * Mathf.Min(110, _encounterHalfSpan * .25f);
            _encounter.Begin(kind, DirectorProfiles.For(_runDirectorProfile).RecoverySeconds);
            RecordRunHistory("encounter_selected", kind.ToString(), "safe_opening", instanceId: _encounterOwner);
            _spawnTimer = .65f;
            ShowArenaToast(kind == CombatEncounterKind.Crossing ? "CROSSING PACK · FIND THE GAP" : "FIRING LINE · MOVE BETWEEN SHOTS", 2.5f, ToastKind.Danger);
        }

        public void ForceEncounterForDiagnostics(string kind)
        {
            if (UsesSustainedDirector)
            {
                BeginSustainedBeat(kind == "volley" ? CombatEncounterKind.Hunt :
                    kind == "brute" ? CombatEncounterKind.Breakthrough : kind == "flank" ? CombatEncounterKind.Flank : CombatEncounterKind.Pursuit);
                return;
            }
            BeginDirectorEncounter(kind == "volley" ? CombatEncounterKind.Volley : CombatEncounterKind.Crossing);
        }

        private void DeployDirectorEncounter()
        {
            HideEncounterWarnings();
            var admitted = 0;
            if (_encounter.Kind == CombatEncounterKind.Crossing)
            {
                var rows = _runDirectorProfile == DirectorProfileId.Standard ? 1 : 2;
                var span = _encounterHalfSpan + 70;
                for (var row = 0; row < rows; row++)
                for (var across = -span; across <= span; across += 42)
                {
                    if (Mathf.Abs(across - _encounterGapOffset) < 95) continue;
                    var axis = _runDirectorProfile == DirectorProfileId.Extreme && row == 1 ? -_encounterAxis : _encounterAxis;
                    var position = _encounterOrigin - axis * (_encounterHalfDepth + 85 + row * 55) + _encounterAcross * across;
                    var slot = FindInactive(_gameSim.Enemies);
                    if (slot < 0 || !SpawnEnemy("chaser", position)) continue;
                    var e = _gameSim.Enemies[slot];
                    _encounterMembers[slot] = new EncounterMember { SpawnId = e.SpawnId, Owner = _encounterOwner,
                        Movement = EncounterMovement.Crossing, Direction = axis, Remaining = (_encounterHalfDepth * 2 + 280 + row * 55) / 150f };
                    admitted++;
                }
            }
            else
            {
                var count = DirectorProfiles.AttackLimit(_runDirectorProfile, PressureHundredths);
                for (var i = 0; i < count; i++)
                {
                    var position = _encounterOrigin - _encounterAxis * Mathf.Min(420, _encounterHalfDepth - 30) +
                        _encounterAcross * ((i - (count - 1) * .5f) * 145);
                    var slot = FindInactive(_gameSim.Enemies);
                    if (slot < 0 || !SpawnEnemy("gunner", position)) continue;
                    var e = _gameSim.Enemies[slot];
                    _encounterMembers[slot] = new EncounterMember { SpawnId = e.SpawnId, Owner = _encounterOwner,
                        Movement = EncounterMovement.Volley, Aim = _encounterOrigin, Remaining = 10, AttackTimer = .9f, Windup = .9f };
                    admitted++;
                }
            }
            _encounter.CommitDeployment(admitted);
        }

        private bool TryUpdateEncounterMember(ref EnemyState enemy, float dt)
        {
            if (UsesSustainedDirector) return false;
            if (!_encounterInitialized || CurrentVoidIsNullCity || CurrentVoidIsMonochrome) return false;
            var slot = enemy.View;
            ref var member = ref _encounterMembers[slot];
            if (member.SpawnId != enemy.SpawnId) member = default;
            if (member.Movement == EncounterMovement.None && _directorActorIds[slot] == enemy.SpawnId &&
                _time >= _directorActorExpiry[slot] && enemy.SummonedByBossTelemetryId == 0)
                SetDirectorWithdrawal(slot, enemy);
            if (member.Movement == EncounterMovement.None) return false;
            member.Remaining = Mathf.Max(0, member.Remaining - dt);
            if (member.Remaining <= 0)
            {
                if (member.Movement == EncounterMovement.Volley) SetDirectorWithdrawal(slot, enemy);
                else { RetireDirectorActor(ref enemy); member = default; return true; }
            }
            enemy.State = 0;
            if (member.Movement == EncounterMovement.Crossing)
                enemy.Velocity = member.Direction * 150;
            else if (member.Movement == EncounterMovement.Withdrawal)
            { enemy.Velocity = member.Direction * 300; enemy.AttackCooldown = 3; }
            else
            {
                enemy.Velocity = Vector2.zero;
                member.AttackTimer -= dt;
                if (member.AttackTimer <= 0)
                {
                    if (member.Windup > 0)
                    {
                        var direction = (member.Aim - enemy.Position).normalized;
                        SpawnHostileShot(enemy.Position, direction, enemy.Damage, 225, 0);
                        member.Windup = 0;
                        member.AttackTimer = _runDirectorProfile == DirectorProfileId.Standard ? 2.4f : 1.8f;
                    }
                    else
                    {
                        member.Aim = _gameSim.Player.Position;
                        member.Windup = .9f;
                        member.AttackTimer = .9f;
                    }
                }
            }
            return true;
        }

        private void SetDirectorWithdrawal(int slot, EnemyState enemy)
        {
            var delta = enemy.Position - _gameSim.Player.Position;
            _encounterMembers[slot] = new EncounterMember { SpawnId = enemy.SpawnId, Owner = _encounterMembers[slot].Owner,
                Movement = EncounterMovement.Withdrawal, Direction = delta.sqrMagnitude < 1 ? Vector2.right : delta.normalized, Remaining = 2.5f };
        }

        private void BeginDirectorWithdrawal(bool includeAmbient)
        {
            for (var i = 0; i < _gameSim.Enemies.Length; i++)
            {
                var e = _gameSim.Enemies[i];
                if (!e.Active || e.SummonedByBossTelemetryId != 0 || IsNullCityEnemy(e.Id) || IsCourtEnemy(e.Id)) continue;
                if (_encounterMembers[i].SpawnId == e.SpawnId && _encounterMembers[i].Movement == EncounterMovement.Withdrawal) continue;
                if (includeAmbient || (_encounterMembers[i].SpawnId == e.SpawnId && _encounterMembers[i].Owner == _encounterOwner)) SetDirectorWithdrawal(i, e);
            }
            HideEncounterWarnings();
        }

        private void RetireDirectorActor(ref EnemyState enemy)
        {
            RecordEnemyRemoval(enemy.View, "director_withdrawal");
            if (enemy.Id == "harvester" && enemy.StoredXp > 0) SpawnPickup(enemy.Position, enemy.StoredXp);
            enemy.StoredXp = 0;
            enemy.Active = false;
            Hide(_enemyViews[enemy.View]);
            Hide(_eliteMarkViews[enemy.View]);
            Hide(_enemyHarvesterFullViews[enemy.View]);
            Hide(_enemyExploderWarningViews[enemy.View]);
            // No death bounty: this actor visibly withdrew, rather than being killed.
        }

        private int CountEncounterMembers()
        {
            var count = 0;
            for (var i = 0; i < _gameSim.Enemies.Length; i++)
                if (_gameSim.Enemies[i].Active && _encounterMembers[i].SpawnId == _gameSim.Enemies[i].SpawnId &&
                    _encounterMembers[i].Owner == _encounterOwner && _encounterMembers[i].Movement != EncounterMovement.None) count++;
            return count;
        }

        private bool HasIncomingDirectorThreat()
        {
            for (var i = 0; i < _gameSim.HostileShots.Length; i++)
            {
                var shot = _gameSim.HostileShots[i];
                if (!shot.Active) continue;
                var delta = _gameSim.Player.Position - shot.Position;
                if (delta.sqrMagnitude < 500 * 500 && Vector2.Dot(delta, shot.Velocity) > 0) return true;
            }
            return false;
        }

        private bool DirectorOpeningSafe()
        {
            var nearby = 0;
            for (var i = 0; i < _gameSim.Enemies.Length; i++)
                if (_gameSim.Enemies[i].Active && (_gameSim.Enemies[i].Position - _gameSim.Player.Position).sqrMagnitude < 230 * 230) nearby++;
            return nearby <= 5 && !HasIncomingDirectorThreat();
        }

        private void RenderEncounterWarnings()
        {
            HideEncounterWarnings();
            if (_mainMenuBrowsing || _gameOver || JourneyStopsCombat) return;
            if (UsesSustainedDirector) return; // No formation solution overlay in Director I.
            var line = 0;
            if (_encounter.Phase == CombatEncounterPhase.Warning && _encounter.Kind == CombatEncounterKind.Crossing)
            {
                var depth = _encounterHalfDepth + 100;
                var span = _encounterHalfSpan + 70;
                DrawEncounterCorridor(line++, -span, _encounterGapOffset - 95, depth);
                DrawEncounterCorridor(line++, _encounterGapOffset + 95, span, depth);
            }
            else if (_encounter.Phase == CombatEncounterPhase.Warning && _encounter.Kind == CombatEncounterKind.Volley)
            {
                var count = DirectorProfiles.AttackLimit(_runDirectorProfile, PressureHundredths);
                for (var i = 0; i < count; i++)
                {
                    var origin = _encounterOrigin - _encounterAxis * Mathf.Min(420, _encounterHalfDepth - 30) + _encounterAcross * ((i - (count - 1) * .5f) * 145);
                    DrawEncounterAim(line++, origin, _encounterOrigin);
                }
            }
            for (var i = 0; i < _gameSim.Enemies.Length && line < _encounterWarnings.Length; i++)
            {
                var e = _gameSim.Enemies[i];var member = _encounterMembers[i];
                if (e.Active && member.SpawnId == e.SpawnId && member.Movement == EncounterMovement.Volley && member.Windup > 0)
                    DrawEncounterAim(line++, e.Position, member.Aim);
            }
        }

        private LineRenderer EncounterWarning(int index)
        {
            if (_encounterWarnings[index] == null) _encounterWarnings[index] = CreateLineView("Director warning " + index, 23);
            var line = _encounterWarnings[index];line.enabled = true;line.useWorldSpace = true;
            line.startWidth = line.endWidth = 2;
            line.startColor = line.endColor = new Color(.85f,.91f,1,.62f);
            return line;
        }

        private void DrawEncounterAim(int index, Vector2 origin, Vector2 target)
        {
            var line = EncounterWarning(index);line.loop = false;line.positionCount = 2;
            var axis = target - origin;if(axis.sqrMagnitude < 1) axis = Vector2.right;
            line.SetPosition(0, origin);line.SetPosition(1, origin + axis.normalized * 900);
        }

        private void DrawEncounterCorridor(int index, float minimum, float maximum, float depth)
        {
            var line = EncounterWarning(index);line.loop = true;line.positionCount = 4;
            line.SetPosition(0, _encounterOrigin - _encounterAxis * depth + _encounterAcross * minimum);
            line.SetPosition(1, _encounterOrigin + _encounterAxis * depth + _encounterAcross * minimum);
            line.SetPosition(2, _encounterOrigin + _encounterAxis * depth + _encounterAcross * maximum);
            line.SetPosition(3, _encounterOrigin - _encounterAxis * depth + _encounterAcross * maximum);
        }

        private void HideEncounterWarnings()
        {
            for (var i = 0; i < _encounterWarnings.Length; i++) if (_encounterWarnings[i] != null) _encounterWarnings[i].enabled = false;
        }
    }
}
