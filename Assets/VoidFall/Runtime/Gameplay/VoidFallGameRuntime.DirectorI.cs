using System;
using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private const int SustainedDirectorVersion = 5;
        // Reach the existing arrival rate by five minutes, then sustain it through the added minute.
        private const float SustainedArrivalRampSeconds = 300f;
        private CombatEncounterKind? _lastSustainedBeat, _previousSustainedBeat;
        private struct DirectorAttackReservation
        {
            public int Slot, Identity;
            public bool Active, LiveShot;
        }
        private readonly DirectorAttackReservation[] _directorAttacks = new DirectorAttackReservation[8];
        private Func<EnemyState, bool> _directorAttackQuery;
        private int _directorAttackDenials;
        private float _directorAttackReportAt;
        private bool _directorHold750;
        private bool _directorPlaytestActive;
        public int DirectorCapacityTarget => _directorHold750 ? 750 : 0;
        public int DirectorCapacitySteps { get; private set; }
        public int DirectorCapacityMinimum { get; private set; }
        public int DirectorCapacityMaximum { get; private set; }
        public int DirectorCapacityMinimumBeforeRefill { get; private set; }
        public int DirectorCapacityFailures { get; private set; }
        private bool UsesSustainedDirector => _encounterInitialized &&
            _runDirectorProfile == DirectorProfileId.Standard && _stressScenario == null;

        private void ResetSustainedDirector()
        {
            _lastSustainedBeat = _previousSustainedBeat = null;
            Array.Clear(_directorAttacks, 0, _directorAttacks.Length);
            _directorAttackDenials = 0; _directorAttackReportAt = _time + 1;
        }

        private void ResetDirectorRunDiagnostics()
        {
            _directorHold750 = false;
            _directorPlaytestActive = false;
            DirectorCapacitySteps = DirectorCapacityMaximum = DirectorCapacityFailures = 0;
            DirectorCapacityMinimum = DirectorCapacityMinimumBeforeRefill = 750;
        }

        private int SustainedAttackLimit() => ActiveBosses() > 0 || _pressureReliefTimer > 0 || _encounter.Phase == CombatEncounterPhase.Recovery ||
            _majorIncident.Kind != MajorIncidentKind.None ? 1 : _runPressure.ProgressionPressureHundredths < 100 ? 2 : _runPressure.ProgressionPressureHundredths < 200 ? 3 : 4;

        private void PrepareDirectorAttackBudget()
        {
            _directorAttackQuery ??= CanCommitDirectorAttack;
            _gameSim.EnemyCanCommitAttack = UsesSustainedDirector ? _directorAttackQuery : null;
            if (!UsesSustainedDirector) return;
            for (var i = 0; i < _directorAttacks.Length; i++) _directorAttacks[i].LiveShot = false;
            // One provenance scan per controller step, never a scan per waiting enemy.
            for (var shot = 0; shot < _gameSim.HostileShots.Length; shot++)
            {
                if (!_gameSim.HostileShots[shot].Active) continue;
                var source = _gameSim.HostileShotSources[shot];
                if (source.SpawnId <= 0) continue;
                for (var i = 0; i < _directorAttacks.Length; i++)
                    if (_directorAttacks[i].Active && _directorAttacks[i].Identity == source.SpawnId)
                        _directorAttacks[i].LiveShot = true;
            }
            for (var i = 0; i < _directorAttacks.Length; i++)
            {
                var reservation = _directorAttacks[i];
                if (!reservation.Active) continue;
                var owner = _gameSim.Enemies[reservation.Slot];
                // Frozen windups and chained attacks retain ownership. Death and
                // slot reuse cannot cancel a projectile already travelling.
                var committed = owner.Active && owner.SpawnId == reservation.Identity &&
                    (owner.State != 0 || (IsNullCityEnemy(owner.Id) && _nullCityUnits[reservation.Slot].Identity == reservation.Identity &&
                        _nullCityUnits[reservation.Slot].Shots > 0));
                if (committed || reservation.LiveShot) continue;
                RecordRunHistory("director_attack_released", instanceId: reservation.Identity);
                _directorAttacks[i] = default;
            }
            if (_time >= _directorAttackReportAt)
            {
                RecordRunHistory("director_attack_budget", amount: SustainedAttackLimit(),
                    budgetLimit: SustainedAttackLimit(), budgetUsed: ActiveDirectorReservations(), blockedAttempts: _directorAttackDenials);
                _directorAttackDenials = 0; _directorAttackReportAt = _time + 1;
            }
        }

        private int ActiveDirectorReservations()
        {
            var count = 0;
            for (var i = 0; i < _directorAttacks.Length; i++) if (_directorAttacks[i].Active) count++;
            return count;
        }

        private bool CanCommitDirectorAttack(EnemyState enemy)
        {
            if (!UsesSustainedDirector) return true;
            var occupied = 0; var free = -1;
            for (var i = 0; i < _directorAttacks.Length; i++)
            {
                if (!_directorAttacks[i].Active) { if (free < 0) free = i; continue; }
                occupied++;
                if (_directorAttacks[i].Identity == enemy.SpawnId) { _directorAttackDenials++; return false; }
            }
            if (occupied >= SustainedAttackLimit() || free < 0) { _directorAttackDenials++; return false; }
            _directorAttacks[free] = new DirectorAttackReservation { Active = true, Slot = enemy.View, Identity = enemy.SpawnId };
            RecordRunHistory("director_attack_admitted", enemy.Id, instanceId: enemy.SpawnId, amount: occupied + 1);
            return true;
        }

        private void UpdateSustainedDirectorSpawns(float dt)
        {
            _directorActive = _directorWarned = false;
            if (_time < RunOpeningSeconds) { _spawnTimer = RunOpeningSeconds - _time; _lastSpawnBlockReason = "opening"; return; }
            if (_arrivalGrace > 0) { _lastSpawnBlockReason = "arrival_grace"; return; }
            var local = LocalDirectorSurvivalSeconds;
            var boss = ActiveBosses() > 0;
            if (boss && !_bossWindowStarted)
            {
                _bossWindowStarted = true;
                _encounter.Reset(); HideEncounterWarnings();
                _spawnTimer = 4;
                RecordRunHistory("director_boss_window", reason: "existing_combat_retained");
            }
            if (!boss) TryDeployLegacySwarm();
            if (!boss && TryIntroduceRestorationEnemy()) return;
            if (!boss)
            {
                var previous = _encounter.Phase;
                _encounter.Step(dt, CountEncounterMembers(), HasIncomingDirectorThreat(), false);
                if (previous != _encounter.Phase)
                    RecordRunHistory("director_beat_phase", _encounter.Kind.ToString(), _encounter.Phase.ToString(), instanceId: _encounterOwner);
                if (previous == CombatEncounterPhase.Recovery && _encounter.Phase == CombatEncounterPhase.Flow)
                    _nextEncounterTime = _time + 7f + (float)(_gameSim.Rng.Next() * 7.0);
                if (_encounter.Phase == CombatEncounterPhase.Deployment) DeploySustainedBeat();
                if (_encounter.Phase == CombatEncounterPhase.Flow && _time >= _nextEncounterTime && DirectorSurvivalSecondsRemaining > 30 &&
                    _majorIncident.Kind == MajorIncidentKind.None)
                {
                    // Do not require a nearly empty screen before creating variety.
                    if (_pressureReliefTimer > 0)
                    { _nextEncounterTime = _time + 5; RecordRunHistory("encounter_deferred", reason: "recent_player_damage"); }
                    else BeginSustainedBeat(ChooseSustainedBeat(local));
                }
            }
            var recovery = _pressureReliefTimer > 0;
            var target = boss ? 64 : Mathf.Min(700, 200 + Mathf.FloorToInt(local * 1.44f) + _pressureStageIndex * 80);
            if (_encounter.Phase == CombatEncounterPhase.ActiveThreat) target = Mathf.Min(700, target + 45);
            if (_gameSim.EnemyOrderCount >= target)
            { _spawnTimer = .5f; _lastSpawnBlockReason = "arrival_target"; return; }
            _spawnTimer = Mathf.Max(0, _spawnTimer - dt);
            if (_spawnTimer > 0) return;
            var arrivalMultiplier = boss || recovery ? 2f : LegacyRestorationRules.ArrivalRateMultiplier;
            _spawnTimer = (boss ? 3.5f : recovery ? .65f : Mathf.Lerp(.50f, .32f, Mathf.Clamp01(local / SustainedArrivalRampSeconds))) / arrivalMultiplier;
            var batch = boss ? 2 : recovery ? 2 : 2 + Mathf.FloorToInt(Mathf.Clamp(local / 65, 0, 4));
            if (!boss && _majorIncident.Kind != MajorIncidentKind.None) batch = Mathf.Max(2, batch / 2);
            if (!boss && DirectorSurvivalSecondsRemaining <= 15) { batch = 1; _spawnTimer = .5f; }
            _lastSpawnBlockReason = null;
            RecordRunHistory("director_arrival_budget", reason: boss ? "boss" : recovery ? "damage_relief" : "sustained",
                amount: batch, detail: "target=" + target + ";version=" + SustainedDirectorVersion + ";rateMultiplier=" + arrivalMultiplier);
            for (var i = 0; i < batch && _gameSim.EnemyOrderCount < target; i++)
            {
                var id = boss || recovery ? (i % 2 == 0 ? "chaser" : "runner") : ChooseAmbientEnemy();
                id = EligibleDirectorType(id);
                if (!RestorationTypeIntroduced(id) || !AmbientTypeAllowed(id)) id = "chaser";
                RecordRunHistory("director_choice", id, "sustained_ambient");
                if (!SpawnEnemy(id, SustainedSpawnPosition(-1))) break;
            }
            if (!boss && !recovery && _majorIncident.Kind == MajorIncidentKind.None && _time >= _nextEliteVariantTime && local >= 160)
            {
                TrySpawnEliteVariant("chaser");
                _nextEliteVariantTime = _time + 60;
            }
        }

        private CombatEncounterKind ChooseSustainedBeat(float local)
        {
            var first = (int)((_runSeed ^ (uint)(_encounterSequence * 104729 + _pressureStageIndex * 7919)) % 4);
            for (var offset = 0; offset < 4; offset++)
            {
                var kind = (CombatEncounterKind)(2 + (first + offset) % 4);
                if (kind == _lastSustainedBeat || kind == _previousSustainedBeat) continue;
                if (local < 45 && (kind == CombatEncounterKind.Hunt || kind == CombatEncounterKind.Breakthrough)) continue;
                return kind;
            }
            return _lastSustainedBeat == CombatEncounterKind.Pursuit ? CombatEncounterKind.Flank : CombatEncounterKind.Pursuit;
        }

        private bool SustainedIncidentOpeningSafe()
        {
            if (HasIncomingDirectorThreat()) return false;
            var northEast = 0; var northWest = 0; var southEast = 0; var southWest = 0;
            for (var i = 0; i < _gameSim.Enemies.Length; i++)
            {
                var enemy = _gameSim.Enemies[i]; if (!enemy.Active) continue;
                var delta = enemy.Position - _gameSim.Player.Position;
                if (delta.sqrMagnitude >= 180 * 180) continue;
                if (delta.x >= 0) { if (delta.y >= 0) northEast++; else southEast++; }
                else { if (delta.y >= 0) northWest++; else southWest++; }
            }
            return Mathf.Min(Mathf.Min(northEast, northWest), Mathf.Min(southEast, southWest)) <= 2;
        }

        private void BeginSustainedBeat(CombatEncounterKind kind)
        {
            _previousSustainedBeat = _lastSustainedBeat; _lastSustainedBeat = kind;
            _encounterSequence++; _encounterOwner++;
            // Mixed green rings are tactical events; the uniform legacy rush has its own clock.
            _circleBeat = _encounterSequence % 3 == 0 && RestorationTypeIntroduced("swarmer");
            if (_circleBeat) ShowArenaToast("SWARM INCOMING", 2f, ToastKind.Danger);
            _encounter.BeginSustained(kind, 3.5 + _gameSim.Rng.Next() * 3.0);
            RecordRunHistory("encounter_selected", kind.ToString(), "sustained", instanceId: _encounterOwner);
        }

        private Vector2 SustainedSpawnPosition(int edge)
        {
            var viewport = GameplayViewportHalfExtent();
            if (edge < 0)
            {
                // First contact comes from the nearer vertical edges; later
                // arrivals include side approaches without a prescribed line.
                edge = _time < 12 ? (_gameSim.Rng.Next() < .5 ? 0 : 1) : _gameSim.Rng.Int(4);
            }
            var across = (float)(_gameSim.Rng.Next() * 2 - 1) * (_time < 12 ? .4f : .8f);
            var offset = edge == 0 ? new Vector2(viewport.x * across, viewport.y + 48) :
                edge == 1 ? new Vector2(viewport.x * across, -viewport.y - 48) :
                edge == 2 ? new Vector2(-viewport.x - 48, viewport.y * across) : new Vector2(viewport.x + 48, viewport.y * across);
            return _gameSim.Player.Position + offset;
        }

        private void DeploySustainedBeat()
        {
            if (DeployRestorationCircle()) return;
            var edge = (int)((_runSeed + (uint)_encounterSequence * 17) % 4);
            var admitted = 0;
            for (var i = 0; i < 10; i++)
            {
                var id = _encounter.Kind == CombatEncounterKind.Pursuit ? (i < 6 ? "runner" : "chaser") :
                    _encounter.Kind == CombatEncounterKind.Flank ? (i < 2 ? "dasher" : "runner") :
                    _encounter.Kind == CombatEncounterKind.Hunt ? (i < 2 ? "gunner" : "chaser") : (i < 2 ? "brute" : "chaser");
                id = EligibleDirectorType(id);
                if (!RestorationTypeIntroduced(id)) id = "chaser";
                var slot = FindInactive(_gameSim.Enemies);
                if (slot < 0 || !SpawnEnemy(id, SustainedSpawnPosition(_encounter.Kind == CombatEncounterKind.Flank ? (edge + (i % 2) * 2) % 4 : edge))) continue;
                _encounterMembers[slot] = new EncounterMember { SpawnId = _gameSim.Enemies[slot].SpawnId,
                    Owner = _encounterOwner, Movement = EncounterMovement.Natural };
                admitted++;
            }
            _encounter.CommitDeployment(admitted);
            RecordRunHistory("director_beat_deployed", _encounter.Kind.ToString(), instanceId: _encounterOwner, amount: admitted);
        }

        private void StepDirectorCapacityProbe()
        {
            if (!_directorHold750 || _stressScenario == null || _gameOver) return;
            DirectorCapacityMinimumBeforeRefill = Mathf.Min(DirectorCapacityMinimumBeforeRefill, _gameSim.EnemyOrderCount);
            for (var attempt = 0; attempt < 750 && _gameSim.EnemyOrderCount < 750; attempt++)
                if (!SpawnEnemy("chaser")) break;
            var count = _gameSim.EnemyOrderCount;
            DirectorCapacitySteps++;
            DirectorCapacityMinimum = Mathf.Min(DirectorCapacityMinimum, count);
            DirectorCapacityMaximum = Mathf.Max(DirectorCapacityMaximum, count);
            if (count != 750) DirectorCapacityFailures++;
            if (DirectorCapacitySteps % 60 == 0)
                RecordRunHistory("director_capacity_probe", amount: count, detail: "target=750;steps=" + DirectorCapacitySteps + ";failures=" + DirectorCapacityFailures);
        }

        public bool ApplyDirectorPlaytest(uint seed)
        {
            _diagnosticRunSeedOverride = seed == 0 ? FixtureRunSeed : seed;
            StartRunInternal(false);
            _directorPlaytestActive = true;
            RecordRunHistory("director_playtest", "scripted_input", detail: "normal_health;fresh_profile;first_offered_upgrade;director_version=5");
            return true;
        }

        private Vector2 DirectorPlaytestInput()
        {
            var input = new Vector2(Mathf.Cos(_time * .21f), Mathf.Sin(_time * .21f)) * .35f;
            var nearest = 500f * 500f;
            for (var i = 0; i < _gameSim.Pickups.Length; i++)
            {
                var pickup = _gameSim.Pickups[i]; if (!pickup.Active) continue;
                var delta = pickup.Position - _gameSim.Player.Position;
                if (delta.sqrMagnitude >= nearest) continue;
                nearest = delta.sqrMagnitude; input = delta.normalized;
            }
            for (var i = 0; i < _gameSim.Enemies.Length; i++)
            {
                var enemy = _gameSim.Enemies[i]; if (!enemy.Active) continue;
                var away = _gameSim.Player.Position - enemy.Position; var distance = away.magnitude;
                if (distance > .1f && distance < 180) input += away / distance * (1 - distance / 180) * 2;
            }
            return Vector2.ClampMagnitude(input, 1);
        }
    }
}
