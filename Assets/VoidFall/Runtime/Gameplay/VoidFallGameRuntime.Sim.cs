using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using VoidFall.Core;
using VoidFall.Persistence;
using VoidFall.Runtime.Rendering;
using VoidFall.UI;
namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {

        private void UpdatePhaseFx(float realDt)
        {
            var speed = _gameOver ? 0.35f : 0.12f;
            var dt = Mathf.Clamp(realDt, 0, 0.1f) * speed;
            if (dt <= 0) return;
            // Keep the browser updateFx() lifecycle order: bolts, damage
            // indicators, impact marks, blast waves, death ghosts, particles,
            // then floaters. Meteor shards and ring waves are Unity's pooled
            // particle equivalents; both advance in the source particle slot.
            UpdateArcEffects(dt);
            UpdateDamageIndicators(dt);
            UpdateImpactMarks(dt);
            UpdateBlastWaves(dt);
            UpdateDeathGhosts(dt);
            UpdateSourceParticles(dt);
            UpdateMeteorShards(dt);
            UpdateRingWaves(dt);
            UpdateFloaters(dt);
        }

        private void UpdateVisualCapture()
        {
            if (_visualCaptureIssued || string.IsNullOrWhiteSpace(_visualCapturePath)) return;
            if (_visualCaptureFramesRemaining > 0)
            {
                _visualCaptureFramesRemaining--;
                return;
            }
            if (_visualCaptureFramesRemaining < 0) return;

            ScreenCapture.CaptureScreenshot(_visualCapturePath, 1);
            _visualCaptureIssued = true;
            Debug.Log($"VoidFall visual capture requested: {_visualCapturePath}");
            if (_visualCaptureQuit) Invoke(nameof(QuitAfterVisualCapture), 0.5f);
        }

        private static float AdvanceArenaBanner(float remaining, float dt)
        {
            return remaining > 0 ? Mathf.Max(0, remaining - dt) : 0;
        }

        private void MovePlayer(float dt)
        {
            var input = _input.ReadMoveAxis(_saveData?.settings?.touchSize ?? 1f);
            var mobility = SupportRank("mobility");
            var adrenal = SupportRank("adrenal");
            var speed = (float)ContentCatalog.Operative.MoveSpeed *
                _moveSpeedMultiplier * (1 + (_adrenalTimer > 0 ? adrenal * 0.06f : 0)) *
                (float)OverclockRules.MovementMultiplier(_overclock.PowerTier);
            if (_gameSim.Player.Health <= 0) input = Vector2.zero;
            // STANDSTILL stance timer: rest counts up, any movement resets.
            if (input.sqrMagnitude < 0.0001f) _standstillSeconds += dt;
            else _standstillSeconds = 0f;
            var targetVelocity = input * speed;
            var movementBlend = 1 - Mathf.Exp(-14f * dt);
            _gameSim.Player.Velocity += (targetVelocity - _gameSim.Player.Velocity) * movementBlend;
            var velocity = _gameSim.Player.Velocity;
            _gameSim.Player.Position += velocity * dt;
            _playerTrailTimer -= dt;
            if (_qualityPreset.PlayerTrail &&
                !(_saveData?.settings != null && _saveData.settings.reducedMotion) &&
                PlayerTrailSpeedExceeded(velocity) && _playerTrailTimer <= 0)
            {
                _playerTrailTimer = 0.06f;
                var jitter = new Vector2(
                    ((float)_fxSim.FxRng.Next() - 0.5f) * 20f,
                    ((float)_fxSim.FxRng.Next() - 0.5f) * 20f);
                var trailColor = PlayerTrailDotColor(
                    _overclock.Active,
                    _adrenalTimer > 0);
                EmitTrailParticle(
                    _gameSim.Player.Position,
                    -velocity * 0.08f + jitter,
                    trailColor,
                    0.32f,
                    0.5f);
            }
        }

        private void UpdateCameraFollow(float dt)
        {
            var blend = 1f - Mathf.Exp(-6f * Mathf.Max(0f, dt));
            _cameraFollowPosition += (_gameSim.Player.Position - _cameraFollowPosition) * blend;
        }

        private static bool PlayerTrailSpeedExceeded(Vector2 velocity)
        {
            return Mathf.Abs(velocity.x) + Mathf.Abs(velocity.y) > 40f;
        }

        private static Color PlayerTrailDotColor(bool overdrive, bool adrenal)
        {
            // The browser selects sprites.dot.yellow/orange/cyan. Those sprites
            // carry their own alpha profile; the particle renderer should add
            // only the lifetime fade, not another hand-tuned opacity multiplier.
            return overdrive
                ? SourceDotColor("yellow")
                : adrenal
                    ? SourceDotColor("orange")
                    : SourceDotColor("cyan");
        }

        /// <summary>
        /// Distance from the player to the closest live boss, or -1 when none is
        /// active.
        /// </summary>
        private float NearestActiveBossDistance()
        {
            var nearest = -1f;
            for (var order = 0; order < _gameSim.BossOrderCount; order++)
            {
                var boss = _gameSim.Bosses[_gameSim.BossOrder[order]];
                if (!boss.Active) continue;
                var distance = (boss.Position - _gameSim.Player.Position).magnitude;
                if (nearest < 0 || distance < nearest) nearest = distance;
            }
            return nearest;
        }

        /// <summary>
        /// Ambient spawn intensity while a boss is alive, scaled by how far the
        /// player has run from it. Staying in the fight keeps the browser's calm
        /// 0.55; abandoning it ramps toward a level above a normal run so there
        /// is no quiet corner to farm.
        /// </summary>
        private float BossProximityIntensity()
        {
            var distance = NearestActiveBossDistance();
            if (distance < 0) return BossEngagedIntensity;
            var taper = Mathf.InverseLerp(
                BossPursuitStartDistance,
                BossPursuitFullDistance,
                distance);
            return Mathf.Lerp(BossEngagedIntensity, BossAbandonedIntensity, taper);
        }

        /// <summary>
        /// Absolute pursuit speed floor for a boss, ramping in with separation.
        /// Zero inside <see cref="BossPursuitStartDistance"/> so ordinary fighting
        /// range is untouched by design.
        /// </summary>
        private static float BossPursuitSpeed(float distance)
        {
            var taper = Mathf.InverseLerp(
                BossPursuitStartDistance,
                BossPursuitFullDistance,
                distance);
            return BossPursuitFloorSpeed * Mathf.SmoothStep(0f, 1f, taper);
        }

        private void UpdateSpawns(float dt)
        {
            UpdateDirector(dt);
            var bossActive = ActiveBosses() > 0;
            var activeBossCount = ActiveBosses();
            var bossRecoveryActive = _time < _bossRecoveryUntil;
            var hasBossCapacity = activeBossCount < DirectorRules.BossCapacityAt(_time);
            var bossQuiet = bossRecoveryActive ||
                (hasBossCapacity && _time >= _nextBossTime - 15f);
            var eventId = _directorActive ? _nextDirectorEvent.Id : null;
            var nextEventConflictsWithBoss =
                _nextDirectorEvent.StartsAtSeconds >= _nextBossTime - 15f;
            var warningActive = _directorWarned && !_directorActive && !bossQuiet;
            var arenaFolding = IsArenaFolding(_arenaTransitionState.Phase);

            var sustained = 1f;
            if (!bossActive && !bossQuiet && !warningActive &&
                _directorRecoveryTimer <= 0 && !nextEventConflictsWithBoss)
            {
                var secondsToEvent = (float)_nextDirectorEvent.StartsAtSeconds - _time;
                if (secondsToEvent <= 10f)
                {
                    sustained = 1f +
                        (1f - Mathf.Max(0f, secondsToEvent) / 10f) * 0.25f;
                }
            }

            // A live boss quiets the ambient spawner, freezes arena cycling and
            // holds _nextBossTime, so disengaging used to buy a calmer run with
            // no timer pressure. Scale the quiet by how close the player stays:
            // fight it and the arena stays clear, leave and the ambient pressure
            // climbs past its normal level to fill the space.
            var intensity = bossActive
                ? BossProximityIntensity()
                : eventId == "surge"
                    ? 1.75f
                    : eventId == "rushers"
                        ? 0.45f
                        : eventId == "swarm" || eventId == "encircle"
                            ? 0f
                            : bossQuiet
                                ? 0.55f
                                : warningActive
                                    ? 0.35f
                                    : _directorRecoveryTimer > 0
                                        ? 0.55f
                                        : sustained;
            var reliefMultiplier = _pressureReliefTimer > 0 && !bossActive ? 0.72f : 1f;
            var ambientIntensity = intensity * reliefMultiplier;
            var activeEnemies = ActiveEnemies();
            var cap = DirectorRules.ActiveEnemyCap(_time, activeBossCount);
            if (ambientIntensity > 0)
            {
                _spawnTimer -= dt;
            }
            else
            {
                _spawnTimer = Mathf.Max(_spawnTimer, 0.45f);
            }
            if (ambientIntensity > 0 && _spawnTimer <= 0 && activeEnemies < cap)
            {
                var baseDelay = Mathf.Max(0.22f, 0.84f - _time * 0.0014f) / ambientIntensity;
                var delay = bossActive ? 1.1f : baseDelay;
                _spawnTimer += (float)DirectorRules.ScaledSpawnDelay(delay);
                var count = bossActive
                    ? 1
                    : Mathf.Min(
                        8,
                        1 + Mathf.FloorToInt(_time / 105f) + (eventId == "surge" ? 1 : 0));
                SpawnAmbientBatch(count, activeBossCount);
            }

            // Browser authority schedules the standard charging Elite separately
            // from elite variants and suppresses it during boss warning windows.
            var standardEliteHealthMultiplier = 1f + Mathf.Min(2.5f, (float)_time / 1200f);
            if (!bossActive && !bossQuiet && _time >= _nextEliteTime &&
                SpawnEnemy("elite", null, null, false, false, 0, standardEliteHealthMultiplier, null))
            {
                _nextEliteTime = _time + Mathf.Max(
                    55f,
                    (float)ContentCatalog.Elite.RepeatEverySeconds - _time / 90f);
                ShowArenaToast("Elite incoming", 2.5f, ToastKind.Danger);
                _audio?.Play(ProceduralAudio.Cue.Warning, 0.72f);
            }

            var initialVoid = _arenaId == ArenaId.Void && _arenaTransitionState.Index == 0;
            if (hasBossCapacity && !_bossWarned && _time >= _nextBossTime - 15f)
            {
                _bossWarned = true;
                _pendingDoubleBoss = initialVoid &&
                    DirectorRules.InitialVoidDoubleBoss(_runSeed, _bossSequence);
                var first = DirectorRules.BossEncounter(_runSeed, _bossSequence);
                var second = _pendingDoubleBoss
                    ? DirectorRules.BossEncounter(_runSeed, _bossSequence + 1)
                    : default(BossEncounterDefinition);
                ShowArenaToast(
                    _pendingDoubleBoss
                        ? FindBoss(first.Id)?.Name + " + " + FindBoss(second.Id)?.Name + " incoming  /  TWO CONTACTS"
                        : FindBoss(first.Id)?.Name + " incoming",
                    2.5f,
                    ToastKind.Danger);
                _audio?.Play(ProceduralAudio.Cue.Boss, 0.72f);
            }

            if (_time >= _nextBossTime && hasBossCapacity && !bossRecoveryActive && !arenaFolding)
            {
                var encounter = DirectorRules.BossEncounter(_runSeed, _bossSequence++);
                var spawnPair = _pendingDoubleBoss && ActiveBosses() == 0 && initialVoid;
                SpawnBoss(encounter.Id, encounter.HealthScale, encounter.DamageScale, encounter.Cycle);
                if (spawnPair)
                {
                    var second = DirectorRules.BossEncounter(_runSeed, _bossSequence++);
                    SpawnBoss(second.Id, second.HealthScale, second.DamageScale, second.Cycle);
                }
                _nextBossTime = (float)DirectorRules.NextBossTimeAfterSpawn(
                    _time, _runSeed, _bossSequence);
                _pendingDoubleBoss = false;
                _bossWarned = false;
            }
        }

        private void UpdateDirector(float dt)
        {
            var bossActive = ActiveBosses() > 0;
            var activeBossCount = ActiveBosses();
            var bossRecoveryActive = _time < _bossRecoveryUntil;
            var hasBossCapacity = activeBossCount < DirectorRules.BossCapacityAt(_time);
            var bossQuiet = bossRecoveryActive ||
                (hasBossCapacity && _time >= _nextBossTime - 15);

            if (bossActive && _directorActive)
            {
                _directorActive = false;
                _directorTimer = 0;
                _directorRecoveryTimer = 0;
                _directorIndex = _nextDirectorEvent.Index + 1;
                _nextDirectorEvent = DirectorRules.Event(_runSeed, _directorIndex);
                _directorWarned = false;
                _directorSpawned = 0;
            }

            if (bossActive)
            {
                while (_nextDirectorEvent.StartsAtSeconds <= _time + 2)
                {
                    _directorIndex++;
                    _nextDirectorEvent = DirectorRules.Event(_runSeed, _directorIndex);
                    _directorWarned = false;
                }
            }

            _directorRecoveryTimer = Mathf.Max(0, _directorRecoveryTimer - dt);
            _pressureReliefTimer = Mathf.Max(0, _pressureReliefTimer - dt);
            var warningActive = _directorWarned && !_directorActive && !bossQuiet;
            if (_time >= _nextEliteVariantTime &&
                (bossActive || bossQuiet || warningActive || _directorActive ||
                 _directorRecoveryTimer > 0 || _pressureReliefTimer > 0 ||
                 IsArenaFolding(_arenaTransitionState.Phase) ||
                 ActiveEliteVariantTotal() >= EliteRules.EliteCadenceActiveCap(
                     FindArena(ArenaIdName(_arenaId))?.EliteCadenceMultiplier ?? 1)))
            {
                _nextEliteVariantTime = _time + (float)EliteRules.EliteCadenceBlockRetrySeconds;
            }
            if (_directorActive)
            {
                _directorTimer -= dt;
                if (_nextDirectorEvent.Id == "rushers")
                {
                    _directorSpawnTimer -= dt;
                    var pulseCount = Mathf.CeilToInt(_nextDirectorEvent.EnemyCount / 2f);
                    var pulseDelay = (float)_nextDirectorEvent.DurationSeconds / Mathf.Max(1, pulseCount);
                    while (_directorSpawnTimer <= 0 && _directorTimer > 0 &&
                           _directorSpawned < _nextDirectorEvent.EnemyCount)
                    {
                        var amount = Mathf.Min(2, _nextDirectorEvent.EnemyCount - _directorSpawned);
                        _directorSpawnTimer += pulseDelay;
                        // Browser authority advances pressureSpawned by the
                        // scheduled pulse amount, even when the active-enemy
                        // cap prevents one or more runners from materializing.
                        // Do not bank that pulse for a later opening.
                        SpawnRusherPressure(_nextDirectorEvent.SpawnEdge, amount);
                        _directorSpawned += amount;
                    }
                }

                if (_directorTimer <= 0)
                {
                    _directorActive = false;
                    _directorRecoveryTimer = (float)_nextDirectorEvent.RecoverySeconds;
                    _directorIndex++;
                    _nextDirectorEvent = DirectorRules.Event(_runSeed, _directorIndex);
                    _directorWarned = false;
                    _directorSpawned = 0;
                    _score += 75;
                    if (_gameSim.Rng.Next() < 0.35)
                    {
                        var angle = (float)(_gameSim.Rng.Next() * Math.PI * 2);
                        SpawnSpecialPickup(
                            _gameSim.Player.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 150,
                            1,
                            PickupKind.Part);
                    }
                }

                return;
            }

            var nextEventConflictsWithBoss =
                _nextDirectorEvent.StartsAtSeconds >= _nextBossTime - 15;
            if (!bossActive && !bossQuiet && !nextEventConflictsWithBoss &&
                !_directorWarned && _time >= _nextDirectorEvent.WarningAtSeconds)
            {
                _directorWarned = true;
                var warningCue = _nextDirectorEvent.Id == "rushers"
                    ? ProceduralAudio.Cue.Rusher
                    : ProceduralAudio.Cue.Warning;
                _audio?.Play(warningCue, 0.9f);
                ShowArenaToast(DirectorPressureLabel(_nextDirectorEvent.Id), 2.5f, ToastKind.Danger);
            }

            var reliefExpired = _pressureReliefTimer <= 0 ||
                _time >= _nextDirectorEvent.StartsAtSeconds + 6;
            if (bossActive || bossQuiet || nextEventConflictsWithBoss ||
                _directorRecoveryTimer > 0 || !reliefExpired ||
                _time < _nextDirectorEvent.StartsAtSeconds)
            {
                return;
            }

            StartDirectorEvent();
        }

        private static string DirectorPressureLabel(string eventId)
        {
            if (eventId == "swarm") return "Swarm incoming";
            if (eventId == "encircle") return "Encirclement incoming";
            if (eventId == "rushers") return "Rushers incoming";
            return "Enemy surge";
        }

        private void StartDirectorEvent()
        {
            _directorActive = true;
            _directorTimer = (float)_nextDirectorEvent.DurationSeconds;
            _directorSpawnTimer = 0;
            _directorSpawned = 0;
            _directorWarned = true;
            _spawnTimer = Mathf.Max(_spawnTimer, 0.65f);
            _audio?.Play(ProceduralAudio.Cue.Warning, 0.9f);

            if (_nextDirectorEvent.Id != "swarm" && _nextDirectorEvent.Id != "encircle") return;

            var count = Mathf.Min(
                _nextDirectorEvent.EnemyCount,
                Mathf.Max(0, DirectorRules.ActiveEnemyCap(_time, ActiveBosses()) - ActiveEnemies()));
            var encircle = _nextDirectorEvent.Id == "encircle";
            var safeGapWidth = encircle ? Mathf.PI / 4f : Mathf.PI / 3.25f;
            var occupiedArc = Mathf.PI * 2f - safeGapWidth;
            var viewportHalf = GameplayViewportHalfExtent();
            // Legacy hypots the *full* viewport then halves it:
            //   radius = Math.hypot(w, h) / 2 + (encircle ? 20 : 45)
            // Hypotting the half extents already yields hypot(w, h) / 2, so the
            // extra * 0.5f that used to sit here halved the ring again and made
            // swarm/encircle waves materialise inside the visible frame instead
            // of walking in from off-screen. Keep this as the plain magnitude of
            // the half extents.
            var radius = viewportHalf.magnitude + (encircle ? 20f : 45f);
            var useRunners = _time >= 180 && _gameSim.Rng.Next() < (encircle ? 0.6 : 0.35);
            var safeGapAngle = SafestEscapeAngle((float)_nextDirectorEvent.SafeGapAngle);
            for (var index = 0; index < count; index++)
            {
                var angle = safeGapAngle + safeGapWidth * 0.5f +
                    occupiedArc * ((index + 0.5f) / Mathf.Max(1, count));
                var position = _gameSim.Player.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (SpawnEnemy(useRunners ? "runner" : "chaser", position)) _directorSpawned++;
            }
        }

        private static float EnemyThreat(EnemyState enemy)
        {
            if (enemy.EliteKind.HasValue)
                return (float)EliteRules.EliteVariantDef(enemy.EliteKind.Value).ThreatCost;
            if (enemy.Elite) return 8f;
            return (float)DirectorRules.EnemyThreatCost(enemy.Id) *
                (float)(enemy.Roster == EnemyRoster.Two ? EnemyRosterRules.RosterTwoThreatMultiplier : 1f);
        }

        private int SpawnRusherPressure(int edge, int amount)
        {
            var spawned = 0;
            var viewportHalf = GameplayViewportHalfExtent();
            var halfWidth = viewportHalf.x + 90;
            var halfHeight = viewportHalf.y + 90;
            for (var index = 0; index < amount; index++)
            {
                if (ActiveEnemies() >= DirectorRules.ActiveEnemyCap(_time, ActiveBosses())) break;
                var laneSize = edge < 2 ? viewportHalf.x * 1.44f : viewportHalf.y * 1.44f;
                var offset = ((float)_gameSim.Rng.Next() - 0.5f) * laneSize;
                var x = edge == 2
                    ? _gameSim.Player.Position.x - halfWidth
                    : edge == 3 ? _gameSim.Player.Position.x + halfWidth : _gameSim.Player.Position.x + offset;
                var y = edge == 0
                    ? _gameSim.Player.Position.y - halfHeight
                    : edge == 1 ? _gameSim.Player.Position.y + halfHeight : _gameSim.Player.Position.y + offset;
                if (SpawnEnemy("runner", new Vector2(x, y))) spawned++;
            }
            return spawned;
        }

        private bool TrySpawnEliteVariant(string replacedType)
        {
            if (_time < _nextEliteVariantTime) return false;
            var activeBossCount = ActiveBosses();
            var budget = DirectorRules.ActiveThreatBudget(_time, activeBossCount);
            var activeThreat = ActiveEnemyThreat();
            var replacedCost = DirectorRules.EnemyThreatCost(replacedType);
            var arena = FindArena(ArenaIdName(_arenaId));
            var context = new EliteCadenceContext
            {
                ElapsedSeconds = _time,
                PickRoll = _gameSim.Rng.Next(),
                ActiveTotal = ActiveEliteVariantTotal(),
                ActiveCap = EliteRules.EliteCadenceActiveCap(arena?.EliteCadenceMultiplier ?? 1),
                ThreatHeadroom = budget - activeThreat - replacedCost,
                ReplacedThreatCost = replacedCost,
            };
            var allowedKinds = new List<EliteVariantId>();
            foreach (var candidate in EliteRules.EliteVariantOrder)
            {
                if (AmbientTypeAllowed(EliteRules.EliteVariantDef(candidate).BaseId))
                    allowedKinds.Add(candidate);
            }
            context.AllowedKinds = allowedKinds.ToArray();
            context.Active[EliteVariantId.Exploder] = ActiveEliteVariantCount(EliteVariantId.Exploder);
            context.Active[EliteVariantId.Mortar] = ActiveEliteVariantCount(EliteVariantId.Mortar);
            context.Active[EliteVariantId.Gunner] = ActiveEliteVariantCount(EliteVariantId.Gunner);

            var kind = EliteRules.SelectEliteVariantForCadence(context);
            if (!kind.HasValue) return false;
            var spawned = SpawnEnemy(
                EliteRules.EliteVariantDef(kind.Value).BaseId,
                null,
                kind.Value);
            if (!spawned) return false;
            _nextEliteVariantTime = _time + (float)EliteRules.EliteCadenceIntervalSeconds(
                _gameSim.Rng.Next(),
                arena?.EliteCadenceMultiplier ?? 1);
            return true;
        }

        private int ActiveEnemyTypeCount(string id)
        {
            var count = 0;
            foreach (var enemy in _gameSim.Enemies)
            {
                if (enemy.Active && enemy.Id == id) count++;
            }
            return count;
        }

        private void SpawnAmbientBatch(int count, int activeBossCount)
        {
            var threatBudget = DirectorRules.ActiveThreatBudget(_time, activeBossCount);
            var activeThreat = ActiveEnemyThreat();
            var cadenceDue = _time >= _nextEliteVariantTime;
            var cadenceAttempted = false;
            for (var index = 0; index < count; index++)
            {
                if (ActiveEnemies() >= DirectorRules.ActiveEnemyCap(_time, activeBossCount)) break;
                var type = ChooseAmbientEnemy();
                if (!AmbientTypeAllowed(type)) type = "chaser";
                var cost = DirectorRules.EnemyThreatCost(type);
                if (activeThreat + cost > threatBudget)
                {
                    type = "chaser";
                    cost = DirectorRules.EnemyThreatCost(type);
                }
                if (activeThreat + cost > threatBudget) break;

                if (cadenceDue && !cadenceAttempted)
                {
                    cadenceAttempted = true;
                    if (TrySpawnEliteVariant(type))
                    {
                        activeThreat = ActiveEnemyThreat();
                        continue;
                    }
                }

                if (!SpawnEnemy(type)) break;
                activeThreat += cost;
            }
            if (cadenceDue && _nextEliteVariantTime <= _time)
                _nextEliteVariantTime = _time + (float)EliteRules.EliteCadenceBlockRetrySeconds;
        }

        private string ChooseAmbientEnemy()
        {
            // Bands are matched against the paced roster clock, not raw run time,
            // so the reveal order stays exactly as authored while the spacing
            // between reveals is reshaped. See DirectorRules.RosterRevealTime.
            var rosterTime = DirectorRules.RosterRevealTime(_time);
            for (var bandIndex = 0; bandIndex < ContentCatalog.SpawnTimeline.Length; bandIndex++)
            {
                var band = ContentCatalog.SpawnTimeline[bandIndex];
                if (rosterTime < band.StartSeconds || rosterTime >= band.EndSeconds) continue;
                var roll = _gameSim.Rng.Next();
                var cursor = 0.0;
                foreach (var weight in band.Weights)
                {
                    cursor += weight.Weight;
                    if (roll <= cursor) return weight.Id;
                }
                return band.Weights[band.Weights.Length - 1].Id;
            }

            return "chaser";
        }

        private void UpdateEnemies(float dt)
        {
            var globalHarvesterXp = XpHeldByHarvesters();
            // Enemy behaviour presentation hooks (see GameSim contract notes).
            _gameSim.EnemyBurstFxHook = EnemyBurstFxForSim;
            _gameSim.EnemyRingWaveHook = EnemyRingWaveForSim;
            _gameSim.EnemyFloaterHook = EnemyFloaterForSim;
            _gameSim.EnemySpawnDroneHook = EnemySpawnDroneForSim;
            _gameSim.EnemyAudioCueHook = EnemyAudioCueForSim;
            _gameSim.EnemyParticleScaleHook = EnemyParticleScaleForSim;
            _gameSim.EnemyFxRollHook = EnemyFxRollForSim;
            _gameSim.EnemyDamagePlayerHook = EnemyDamagePlayerForSim;
            _gameSim.EnemyBlastWaveHook = EnemyBlastWaveForSim;
            _gameSim.EnemyImpactMarkHook = EnemyImpactMarkForSim;
            _gameSim.EnemyFreezeHook = EnemyFreezeForSim;
            _gameSim.EnemyAmberFlashHook = EnemyAmberFlashForSim;
            _gameSim.EnemyDamageAreaHook = EnemyDamageAreaForSim;
            _gameSim.EnemySpawnShotHook = EnemySpawnShotForSim;
            _gameSim.EnemyFuseWarningHook = EnemyFuseWarningForSim;
            _gameSim.EnemyResolveDeathHook = EnemyResolveDeathForSim;
            _gameSim.EnemyShotsRemainingQuery = EnemyShotsRemainingForSim;
            _gameSim.EnemyGameOverQuery = EnemyGameOverForSim;
            _gameSim.EnemyRevivePendingQuery = EnemyRevivePendingForSim;
            _gameSim.EnemyHidePickupViewHook = EnemyHidePickupViewForSim;
            _gameSim.EnemyTelemetryHook = EnemyTelemetryForSim;
            _gameSim.EnemyShakeHook = EnemyShakeForSim;
            // Browser updateEnemies walks its compact array backwards. The
            // logical order list preserves that behavior across pooled slots.
            for (var order = _gameSim.EnemyOrderCount - 1; order >= 0; order--)
            {
                var i = _gameSim.EnemyOrder[order];
                var enemy = _gameSim.Enemies[i];
                if (!enemy.Active) continue;
                enemy.Age += dt;
                var delta = _gameSim.Player.Position - enemy.Position;
                var distance = SourceLengthOrOne(delta);
                var direction = delta / distance;
                enemy.AttackCooldown = Mathf.Max(0, enemy.AttackCooldown - dt);
                enemy.BlockCooldown = Mathf.Max(0, enemy.BlockCooldown - dt);
                enemy.ContactCooldown = Mathf.Max(0, enemy.ContactCooldown - dt);
                enemy.BladeCooldown = Mathf.Max(0, enemy.BladeCooldown - dt);
                enemy.HollowCooldown = Mathf.Max(0, enemy.HollowCooldown - dt);
                enemy.HitTimer = Mathf.Max(0, enemy.HitTimer - dt);
                enemy.Rotation = SourceEnemyRotationAdvance(enemy.Rotation, enemy.Spin, dt);

                if (enemy.Elite && !enemy.EliteKind.HasValue)
                {
                    UpdateStandardElite(ref enemy, dt, distance, direction);
                }
                else if (enemy.EliteKind.HasValue && enemy.EliteKind.Value == EliteVariantId.Gunner)
                {
                    UpdateGunner(ref enemy, dt, distance, direction);
                }
                else if (enemy.Id == "gunner" || enemy.Id == "twinGunner")
                {
                    UpdateGunner(ref enemy, dt, distance, direction);
                }
                else if (enemy.EliteKind.HasValue && enemy.EliteKind.Value == EliteVariantId.Mortar)
                {
                    UpdateMortar(ref enemy, dt, distance, direction);
                }
                else if (enemy.Id == "mortar")
                {
                    UpdateMortar(ref enemy, dt, distance, direction);
                }
                else if (enemy.Id == "technician")
                {
                    UpdateTechnician(ref enemy, dt, distance, direction);
                }
                else if (enemy.Id == "exploder")
                {
                    UpdateExploder(ref enemy, dt, distance, direction);
                }
                else if (enemy.Id == "bulwark")
                {
                    UpdateBulwark(ref enemy, dt, direction);
                }
                else if (enemy.Id == "harvester")
                {
                    UpdateHarvester(ref enemy, dt, direction, ref globalHarvesterXp);
                }
                else if (enemy.Id == "carrier")
                {
                    UpdateCarrier(ref enemy, dt, distance, direction);
                }
                else if (enemy.Id == "dasher")
                {
                    UpdateDasher(ref enemy, dt, distance, direction);
                }
                else if (enemy.Roster == EnemyRoster.Two && enemy.Id == "chaser")
                {
                    UpdateRosterPincer(ref enemy, dt, distance, direction);
                }
                else if (enemy.Roster == EnemyRoster.Two && enemy.Id == "guard")
                {
                    UpdateRosterGuard(ref enemy, dt, direction);
                }
                else
                {
                    // The browser only ever adds a small symmetric wobble here,
                    // so every body in this branch drives at the player along a
                    // near-radial line. Distant spawns therefore arrive on the
                    // same bearing and compact into a shell the player can just
                    // orbit. A persistent per-enemy angular offset makes each
                    // approach an arc with its own handedness, which fans the
                    // population out across the arena instead.
                    var wobble = Mathf.Sin(_time * 2.2f + enemy.Seed) *
                        (enemy.Id == "runner" ? 0.45f : 0.18f) +
                        ApproachBias(enemy.Seed, distance);
                    var rotated = new Vector2(
                        direction.x * Mathf.Cos(wobble) - direction.y * Mathf.Sin(wobble),
                        direction.x * Mathf.Sin(wobble) + direction.y * Mathf.Cos(wobble));
                    enemy.Velocity = rotated * enemy.Speed;
                }

                enemy.Knockback *= Mathf.Exp(-8f * dt);
                enemy.Position += (enemy.Velocity + enemy.Knockback) * dt;
                if ((!enemy.Elite || enemy.EliteKind.HasValue) && distance > 1750f)
                {
                    var angle = (float)(_gameSim.Rng.Next() * Math.PI * 2);
                    var viewportHalf = GameplayViewportHalfExtent();
                    var radius = Mathf.Sqrt(
                        Mathf.Pow(viewportHalf.x * 2f, 2) +
                        Mathf.Pow(viewportHalf.y * 2f, 2)) * 0.5f + 90f;
                    enemy.Position = _gameSim.Player.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    enemy.Age = 0;
                }

                var canContactPlayer = _gameSim.Player.Health > 0 && !_gameOver && !_revivePending &&
                    _gameSim.Player.DyingTimer <= 0 && _gameSim.Player.Iframes <= 0;
                if (enemy.Active && enemy.Id != "exploder" && enemy.Age > 0.4f &&
                    distance < enemy.Radius + PlayerRadius && enemy.ContactCooldown <= 0 &&
                    canContactPlayer)
                {
                    var contactDamage = enemy.Elite && !enemy.EliteKind.HasValue && enemy.State == 2
                        ? enemy.Damage * (float)ContentCatalog.Elite.ChargeDamageMultiplier
                        : enemy.Damage;
                    DamagePlayer(contactDamage, direction);
                    ApplySourcePlayerKnockback(ref enemy, direction);
                    enemy.ContactCooldown = 0.72f;
                }
                _gameSim.Enemies[i] = enemy;
                if (!enemy.Active) RemoveEnemyOrder(i);
            }
        }

        private void UpdateDasher(ref EnemyState enemy, float dt, float distance, Vector2 direction) => _gameSim.UpdateDasher(ref enemy, dt, distance, direction);

        private void UpdateRosterPincer(ref EnemyState enemy, float dt, float distance, Vector2 direction) => _gameSim.UpdateRosterPincer(ref enemy, dt, distance, direction);

        private void UpdateStandardElite(ref EnemyState enemy, float dt, float distance, Vector2 direction) => _gameSim.UpdateStandardElite(ref enemy, dt, distance, direction);
        private void UpdateBulwark(ref EnemyState enemy, float dt, Vector2 direction) => _gameSim.UpdateBulwark(ref enemy, dt, direction);
        private void UpdateHarvester(ref EnemyState enemy, float dt, Vector2 fallbackDirection, ref float globalStoredXp)
            => _gameSim.UpdateHarvester(ref enemy, dt, fallbackDirection, ref globalStoredXp, _xpNeed, (float)_time, _bossCycle);

        private void UpdateGunner(ref EnemyState enemy, float dt, float distance, Vector2 direction) => _gameSim.UpdateGunner(ref enemy, dt, distance, direction);
        private float XpHeldByHarvesters()
        {
            var total = 0f;
            for (var index = 0; index < _gameSim.Enemies.Length; index++)
            {
                if (_gameSim.Enemies[index].Active && _gameSim.Enemies[index].Id == "harvester") total += _gameSim.Enemies[index].StoredXp;
            }

            return total;
        }


        private void EnemyBurstFxForSim(Vector2 position, Color color, int count, float speed, float lifetime, float explicitSize)
            => BurstFx(position, color, count, speed, lifetime, explicitSize);

        private void EnemyRingWaveForSim(Vector2 position, float startRadius, float growth, float life, Color color)
            => SpawnRingWave(position, startRadius, growth, life, color);

        private void EnemyFloaterForSim(Vector2 position, string text, Color color, float size)
            => SpawnFloater(position, text, color, size);


        private void EnemyAudioCueForSim(ProceduralAudio.Cue cue, float volume)
            => _audio?.Play(cue, volume);

        private float EnemyParticleScaleForSim() => _qualityPreset.ParticleScale;


        private void EnemyDamagePlayerForSim(float damage, Vector2 sourceDirection) => DamagePlayer(damage, sourceDirection);

        private void EnemyBlastWaveForSim(Vector2 position, float maxRadius, float life, bool bomb) => SpawnBlastWave(position, maxRadius, life, bomb);

        private void EnemyImpactMarkForSim(Vector2 position, float radius, float rotation) => SpawnImpactMark(position, radius, rotation);

        private void EnemyFreezeForSim(float seconds) => TriggerFreeze(seconds);

        private void EnemyAmberFlashForSim(float intensity) => _amberFlash = Mathf.Max(_amberFlash, intensity);

        private void EnemyDamageAreaForSim(Vector2 origin, float radius, float damage, int excludedIdentity, int weaponIndex)
            => DamageArea(origin, radius, damage, excludedIdentity, weaponIndex);

        private void EnemySpawnShotForSim(Vector2 position, Vector2 direction, float damage, float speed, float curvature, bool meteorOwned, int visualVariant)
            => SpawnHostileShot(position, direction, damage, speed, curvature, meteorOwned, visualVariant);

        private void EnemyFuseWarningForSim(int stage) => PlayFuseWarning(stage);

        private void EnemyResolveDeathForSim(int index, bool selfDetonated) => ResolveEnemyDeath(index, selfDetonated);

        private int EnemyShotsRemainingForSim() => MaxHostileShots - ActiveHostileShots();

        private bool EnemyGameOverForSim() => _gameOver;


        private void EnemyHidePickupViewForSim(int slot) => Hide(_pickupViews[slot]);

        private void EnemyTelemetryForSim(float absorbed) => _telemetry.RecordXpAbsorbedByHarvester(absorbed);

        private void EnemyShakeForSim(float amount) => AddCameraShake(amount);
        private bool EnemyRevivePendingForSim() => _revivePending;
        private double EnemyFxRollForSim() => _fxSim.FxRng.Next();
        private bool EnemySpawnDroneForSim(int carrierSpawnId, Vector2 position)
            => SpawnCarrierDrone(carrierSpawnId, position);
        private void UpdateCarrier(ref EnemyState enemy, float dt, float distance, Vector2 direction) => _gameSim.UpdateCarrier(ref enemy, dt, distance, direction);

        private void UpdateRosterGuard(ref EnemyState enemy, float dt, Vector2 direction) => _gameSim.UpdateRosterGuard(ref enemy, dt, direction);

        private void UpdateTechnician(ref EnemyState enemy, float dt, float distance, Vector2 direction) => _gameSim.UpdateTechnician(ref enemy, dt, distance, direction);
        private bool SpawnCarrierDrone(int carrierSpawnId, Vector2 position)
        {
            if (!SpawnEnemy("runner", position, null, true, false)) return false;
            var spawnedId = _nextEnemyId - 1;
            for (var index = 0; index < _gameSim.Enemies.Length; index++)
            {
                var drone = _gameSim.Enemies[index];
                if (!drone.Active || !drone.CarrierDrone || drone.SpawnId != spawnedId) continue;
                drone.SummonedByCarrierSpawnId = carrierSpawnId;
                _gameSim.Enemies[index] = drone;
                return true;
            }
            return false;
        }

        private void UpdateMortar(ref EnemyState enemy, float dt, float distance, Vector2 direction)
            => _gameSim.UpdateMortar(ref enemy, dt, distance, direction, _time);

        private void UpdateExploder(ref EnemyState enemy, float dt, float distance, Vector2 direction) => _gameSim.UpdateExploder(ref enemy, dt, distance, direction);
        private void UpdateMeteors(float dt)
        {
            // Browser updateMeteors keeps its timer active during the warning;
            // only the collapse and settle phases are the on-screen fold.
            if (ArenaHasFeature("meteors") && !IsArenaFolding(_arenaTransitionState.Phase))
            {
                _meteorSpawnTimer -= dt;
                if (_meteorSpawnTimer <= 0)
                {
                    _meteorSpawnTimer = 1.35f;
                    if (CountMeteors(false) < _meteorTarget) TrySpawnMeteor(false);
                    else if (CountMeteors(true) < MeteorRules.MaxExplosiveMeteors) TrySpawnMeteor(true);
                }
            }

            // The browser moves every meteor first, then resolves all fuse
            // expirations. The state half of that loop now lives on GameSim
            // (AdvanceMeteors); this wrapper hides views for the slots it
            // reports and then runs the deferred detonations. View operations
            // never feed back into the simulation, so hoisting the hides out
            // of the loop is behavior-neutral; the golden master proves it.
            EnsureMeteorOrderEntries();
            _gameSim.PendingMeteorDetonationCount = 0;
            // Player lifecycle flags cannot change inside the meteor loop, so
            // the per-meteor vulnerability check hoists to one evaluation.
            var playerVulnerable = _gameSim.Player.Health > 0 && !_gameOver && !_revivePending &&
                _gameSim.Player.DyingTimer <= 0;
            var expiredCount = _gameSim.AdvanceMeteors(
                dt,
                ref _gameSim.Player.Position,
                ref _gameSim.Player.Velocity,
                playerVulnerable,
                _meteorExpiredSlots,
                _meteorCulledSlots,
                out var culledCount);
            for (var slotIndex = 0; slotIndex < expiredCount; slotIndex++)
            {
                var expiredSlot = _meteorExpiredSlots[slotIndex];
                Hide(_meteorViews[expiredSlot]);
                Hide(_meteorHitViews[expiredSlot]);
                Hide(_meteorCoreViews[expiredSlot]);
                Hide(_meteorDangerArcViews[expiredSlot]);
                Hide(_meteorDangerRingViews[expiredSlot]);
                Hide(_meteorHealthArcViews[expiredSlot]);
            }
            for (var slotIndex = 0; slotIndex < culledCount; slotIndex++)
            {
                var culledSlot = _meteorCulledSlots[slotIndex];
                Hide(_meteorViews[culledSlot]);
                Hide(_meteorHitViews[culledSlot]);
                Hide(_meteorCoreViews[culledSlot]);
                Hide(_meteorDangerArcViews[culledSlot]);
                Hide(_meteorDangerRingViews[culledSlot]);
                Hide(_meteorHealthArcViews[culledSlot]);
            }

            for (var index = 0; index < _gameSim.PendingMeteorDetonationCount; index++)
                DetonateMeteor(_gameSim.PendingMeteorDetonations[index]);
            _gameSim.PendingMeteorDetonationCount = 0;
        }

        private void TrySpawnMeteor(bool explosive)
        {
            var limit = explosive ? MeteorRules.MaxExplosiveMeteors : _meteorTarget;
            if (CountMeteors(explosive) >= limit) return;
            var variant = Mathf.FloorToInt((float)(_gameSim.Rng.Next() * MeteorRules.MeteorVariantCount(explosive)));
            var radius = (float)MeteorRules.MeteorCollisionRadius(variant, explosive);
            var enemyCount = 0;
            for (var index = 0; index < _gameSim.Enemies.Length; index++)
            {
                if (!_gameSim.Enemies[index].Active) continue;
                _meteorPlacementEnemyCircles[enemyCount++] = new CircleDefinition(
                    _gameSim.Enemies[index].Position.x,
                    _gameSim.Enemies[index].Position.y,
                    _gameSim.Enemies[index].Radius);
            }
            var meteorCount = 0;
            for (var index = 0; index < _gameSim.Meteors.Length; index++)
            {
                if (!_gameSim.Meteors[index].Active) continue;
                _meteorPlacementCircles[meteorCount++] = new CircleDefinition(
                    _gameSim.Meteors[index].Position.x,
                    _gameSim.Meteors[index].Position.y,
                    _gameSim.Meteors[index].Radius);
            }
            _meteorPlacementContext.PlayerX = _gameSim.Player.Position.x;
            _meteorPlacementContext.PlayerY = _gameSim.Player.Position.y;
            _meteorPlacementContext.PlayerRadius = MeteorRules.PlayerCollisionRadius;
            _meteorPlacementContext.Enemies = _meteorPlacementEnemyCircles;
            _meteorPlacementContext.EnemyCount = enemyCount;
            _meteorPlacementContext.Meteors = _meteorPlacementCircles;
            _meteorPlacementContext.MeteorCount = meteorCount;
            for (var attempt = 0; attempt < 10; attempt++)
            {
                var angle = (float)(_gameSim.Rng.Next() * Math.PI * 2);
                var distance = 380f + (float)_gameSim.Rng.Next() * 520f;
                var candidate = _gameSim.Player.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                var safe = MeteorRules.IsSafeMeteorPlacement(
                    new CircleDefinition(candidate.x, candidate.y, radius),
                    _meteorPlacementContext,
                    _meteorPlacementProjectedCircles);
                if (!safe) continue;

                // Insertion (slot find, state rolls, order append) is GameSim's
                // now; the roll order inside it matches the browser exactly.
                var slot = _gameSim.TryInsertMeteor(
                    candidate,
                    radius,
                    variant,
                    explosive,
                    _time);
                if (slot < 0) return;
                var view = EnsureMeteorView(slot);
                view.sprite = ProceduralSpriteFactory.Meteor(variant, explosive);
                view.transform.rotation = Quaternion.Euler(0, 0, _gameSim.Meteors[slot].Rotation * Mathf.Rad2Deg);
                view.color = Color.white;
                view.enabled = true;
                var coreView = EnsureMeteorCoreView(slot);
                coreView.sprite = ProceduralSpriteFactory.MeteorCore();
                coreView.transform.rotation = view.transform.rotation;
                coreView.color = Color.white;
                coreView.enabled = explosive;
                EnsureMeteorDangerArcView(slot);
                EnsureMeteorDangerRingView(slot);
                return;
            }
        }

        private void DetonateMeteor(MeteorState meteor)
        {
            _telemetry.RecordMeteorDetonated(ArenaIdName(_arenaId));
            // Keep the blast readable as a heavy meteor break: the browser
            // throws larger fragments before resolving the chain and damage.
            ShatterMeteor(meteor, 1.5f);
            var radius = (float)MeteorRules.ExplosiveBlastRadius;
            var enemyDamage = (float)(MeteorRules.ExplosiveEnemyDamage + _time * 0.28);
            IgniteMeteorsInRadius(meteor.Position, radius);
            DamageArea(meteor.Position, radius, enemyDamage, -1);

            var shardDamage = ExplosiveMeteorShardDamageAt((float)_time, _bossCycle);
            var baseAngle = meteor.Variant * 0.71f;
            if (HasExplosiveShardCapacity(ActiveHostileShots()))
            {
                for (var index = 0; index < MeteorRules.ExplosiveShardCount; index++)
                {
                    var angle = baseAngle + index / (float)MeteorRules.ExplosiveShardCount * Mathf.PI * 2;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    SpawnHostileShot(
                        meteor.Position + direction * 18,
                        direction,
                        shardDamage,
                        (float)MeteorRules.ExplosiveShardSpeed,
                        0,
                        true,
                        index % 4);
                }
            }

            if (_gameSim.Player.Health > 0 && !_gameOver && !_revivePending && _gameSim.Player.DyingTimer <= 0 &&
                _gameSim.Player.Iframes <= 0 && Vector2.Distance(_gameSim.Player.Position, meteor.Position) < radius + PlayerRadius)
            {
                DamagePlayer(
                    ExplosiveMeteorPlayerDamageAt((float)_time, _bossCycle, enemyDamage),
                    _gameSim.Player.Position - meteor.Position);
                _telemetry.RecordMeteorPlayerHit(ArenaIdName(_arenaId));
            }

            SpawnBlastWave(meteor.Position, radius, 0.46f, false);
            BurstFx(meteor.Position, SourceDotColor("orange"), 18, 320, 0.5f, 0.9f);
            BurstFx(meteor.Position, SourceDotColor("yellow"), 8, 240, 0.34f, 0.68f);
            BurstFx(meteor.Position, SourceDotColor("white"), 4, 150, 0.18f, 0.46f);
            SpawnRingWave(
                meteor.Position,
                8f,
                radius * 2.2f,
                0.34f,
                new Color(1f, 0.46f, 0.12f, 0.78f));
            // Browser detonateMeteor emits the final flash, shake, and cue
            // after target damage and all chained presentation side effects.
            _amberFlash = Mathf.Max(_amberFlash, 0.3f);
            AddCameraShake(0.42f);
            _audio?.Play(ProceduralAudio.Cue.ExploderBlast, 0.92f);
        }

        private void IgniteMeteorsInRadius(Vector2 origin, float radius)
        {
            var ignited = _gameSim.IgniteMeteorsInRadius(origin, radius);
            for (var replay = 0; replay < ignited; replay++)
            {
                PlayFuseWarning(4);
                _telemetry.RecordMeteorDestroyed(ArenaIdName(_arenaId), true);
            }
        }

        private int CountMeteors(bool explosive) => _gameSim.CountMeteors(explosive);

        private void ClearMeteors()
        {
            for (var index = 0; index < _gameSim.Meteors.Length; index++)
            {
                Hide(_meteorViews[index]);
                Hide(_meteorHitViews[index]);
                Hide(_meteorCoreViews[index]);
                Hide(_meteorDangerArcViews[index]);
                Hide(_meteorDangerRingViews[index]);
                Hide(_meteorHealthArcViews[index]);
            }
            var clearedCount = _fxSim.ClearMeteorShards(_fxClearedScratch);
            for (var index = 0; index < clearedCount; index++)
                Hide(_meteorShardViews[_fxClearedScratch[index]]);
            _gameSim.ClearMeteorStates();
        }

        private void ShatterMeteor(MeteorState meteor, float force)
        {
            var shardCount = _qualityPreset.Detail > 1 ? 6 : 4;
            var visibleRadius = Mathf.Max(1f, meteor.VisibleRadius);
            // The budget guard is constant across this loop: nothing here
            // changes the live particle count between iterations.
            var allowInsert = !(ActiveFxVisualCount() >= SourceParticleLimit(_qualityPreset.ParticleScale));
            for (var index = 0; index < shardCount; index++)
            {
                if (!_fxSim.TrySpawnMeteorShard(meteor.Position, visibleRadius, force, index, allowInsert, out var shard)) continue;
                var view = EnsureMeteorShardView(shard.View);
                view.sprite = ProceduralSpriteFactory.MeteorShard(shard.Variant);
                view.color = Color.white;
                view.transform.position = shard.Position;
                view.transform.rotation = Quaternion.Euler(0, 0, shard.Rotation * Mathf.Rad2Deg);
                view.transform.localScale = Vector3.one *
                    SourceMeteorShardWorldSize(shard.Size, 1f);
                view.enabled = true;
            }

            // Dust, not sparks: the browser keeps an ordinary rock break warm
            // and short so it never reads as a second hazard.
            BurstFx(meteor.Position, SourceDotColor("orange"), 5, 120 * force, 0.4f, 0.7f);
            BurstFx(meteor.Position, SourceDotColor("white"), 2, 90 * force, 0.22f, 0.4f);
        }

        private int FindMeteorShardSlot() => _fxSim.FindMeteorShardSlot();

        private void UpdateSourceParticles(float dt)
        {
            var expired = _fxSim.AdvanceSourceParticles(dt, _fxExpiryScratch);
            for (var index = 0; index < expired; index++)
                Hide(_sourceParticleViews[_fxExpiryScratch[index]]);
        }
        private void UpdateMeteorShards(float dt)
        {
            var expired = _fxSim.AdvanceMeteorShards(dt, _fxExpiryScratch);
            for (var index = 0; index < expired; index++)
                Hide(_meteorShardViews[_fxExpiryScratch[index]]);
        }

        private void UpdateBosses(float dt)
        {
            EnsureBossOrderEntries();
            var initialBossOrderCount = _gameSim.BossOrderCount;
            for (var order = 0; order < initialBossOrderCount; order++)
            {
                var i = _gameSim.BossOrder[order];
                var boss = _gameSim.Bosses[i];
                if (!boss.Active)
                {
                    if (boss.DeathTimer > 0)
                    {
                        boss.DeathTimer = Mathf.Max(0, boss.DeathTimer - dt);
                        if (boss.DeathTimer <= 0) Hide(_bossViews[i]);
                        _gameSim.Bosses[i] = boss;
                    }
                    continue;
                }
                boss.ContactCooldown = Mathf.Max(0, boss.ContactCooldown - dt);
                boss.HitTimer = Mathf.Max(0, boss.HitTimer - dt);
                boss.BladeCooldown = Mathf.Max(0, boss.BladeCooldown - dt);
                boss.ShieldHitTimer = Mathf.Max(0, boss.ShieldHitTimer - dt);
                boss.HollowCooldown = Mathf.Max(0, boss.HollowCooldown - dt);
                boss.BeamHitCooldown = Mathf.Max(0, boss.BeamHitCooldown - dt);
                var definition = FindBoss(boss.Id);
                if (definition == null) continue;
                if (boss.State == 4)
                {
                    boss.StateTimer -= dt;
                    if (boss.StateTimer <= 0) boss.State = 0;
                    _gameSim.Bosses[i] = boss;
                    continue;
                }
                var delta = _gameSim.Player.Position - boss.Position;
                var distance = SourceLengthOrOne(delta);
                var direction = delta / distance;
                var phaseTwo = boss.Health / Mathf.Max(1, boss.MaxHealth) <= definition.PhaseTwoHealthRatio;
                if (boss.PressureTier > 0 && !boss.TierPressureTriggered &&
                    boss.Health / Mathf.Max(1, boss.MaxHealth) <= 0.72f)
                {
                    // React resolves the pressure-tier threshold before the
                    // phase-two reinforcement when both become true together.
                    ApplyBossTierPressure(ref boss);
                }

                if (phaseTwo && !boss.Reinforced)
                {
                    boss.Reinforced = true;
                    if (boss.Id == "warden")
                    {
                        for (var spawn = 0; spawn < 5; spawn++)
                        {
                            var angle = spawn / 5f * Mathf.PI * 2;
                            SpawnEnemy(
                                spawn % 2 == 0 ? "runner" : "chaser",
                                boss.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 100,
                                null,
                                false,
                                false,
                                healthMultiplier: 1.1f);
                        }
                    }
                    SpawnRingWave(boss.Position, 30f, 300f, 0.5f,
                        new Color(0.8f, 0.55f, 1f, 0.82f));
                    BurstFx(boss.Position, BossParticleColor(boss.Id), 16, 260, 0.5f, 0.8f);
                    TriggerFreeze(0.05f);
                    AddCameraShake(0.4f);
                    _audio?.Play(ProceduralAudio.Cue.BossCharge, 0.9f);
                    ShowArenaToast(definition.Name + ": phase two", 2.5f, ToastKind.Danger);
                }

                var speed = boss.Speed * (phaseTwo ? (float)definition.PhaseTwoSpeedMultiplier : 1f) *
                    (1 + Mathf.Min(0.18f, boss.PressureTier * 0.06f));
                // Boss.Speed is fixed at spawn and never scales, and even the
                // fastest boss at full phase-two pressure tops out around 85
                // against a 235 base player. Walking away therefore beats every
                // encounter outright. Apply an absolute pursuit floor that ramps
                // in with separation: identical inside fighting range, but a
                // fleeing player is always run down. Expressed as a floor rather
                // than a multiplier so slow and fast bosses converge on the same
                // chase speed instead of the slow ones staying uncatchable.
                speed = Mathf.Max(speed, BossPursuitSpeed(distance));
                var cooldownScale = (phaseTwo ? (float)definition.PhaseTwoCooldownMultiplier : 1f) *
                    (1 - Mathf.Min(0.24f, boss.PressureTier * 0.08f));
                if (boss.State == 1)
                {
                    boss.StateTimer -= dt;
                    // Browser windup only damps the previous charge velocity;
                    // the boss remains stationary until the active charge step.
                    if (boss.StateTimer <= 0)
                    {
                        boss.State = 2;
                        boss.StateTimer = boss.ActiveAttack?.ActiveSeconds > 0
                            ? (float)boss.ActiveAttack.ActiveSeconds
                            : 0.1f;
                        boss.ActionApplied = false;
                        // Browser authority starts the charge/beam cue when
                        // the telegraph hands off to the active attack, not
                        // when the wind-up begins.
                        if (boss.ActiveAttack?.Id == "charge" || boss.ActiveAttack?.Id == "beam")
                            _audio?.Play(ProceduralAudio.Cue.BossCharge, 0.9f);
                    }
                }
                else if (boss.State == 2)
                {
                    ApplyBossAttack(ref boss, definition, dt);
                    boss.StateTimer -= dt;
                    if (boss.StateTimer <= 0)
                    {
                        boss.State = 3;
                        boss.StateTimer = (float)(boss.ActiveAttack?.RecoverySeconds ?? 0.7);
                    }
                }
                else if (boss.State == 3)
                {
                    boss.StateTimer -= dt;
                    if (boss.StateTimer <= 0) boss.State = 0;
                }
                else
                {
                    boss.Position += direction * speed * dt;
                    boss.AttackCooldown -= dt;
                    // Attack states 1-3 hold the boss still. Committing to one at
                    // long range would hand a fleeing player free distance and
                    // undo the pursuit floor above, so only engage once the
                    // player is close enough for the attack to mean anything.
                    if (boss.AttackCooldown <= 0 && distance <= BossEngagementDistance &&
                        definition.Attacks != null && definition.Attacks.Length > 0)
                    {
                        boss.ActiveAttack = definition.Attacks[boss.AttackIndex % definition.Attacks.Length];
                        boss.AttackIndex++;
                        boss.AttackCooldown = (float)boss.ActiveAttack.CooldownSeconds * cooldownScale;
                        boss.DashDirection = direction;
                        boss.TargetPosition = _gameSim.Player.Position;
                        boss.AttackAngle = Mathf.Atan2(direction.y, direction.x);
                        if (boss.ActiveAttack.Id == "blink")
                        {
                            var angle = (float)(_gameSim.Rng.Next() * Math.PI * 2);
                            var blinkDistance = 230f + (float)_gameSim.Rng.Next() * 100f;
                            boss.TargetPosition = _gameSim.Player.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * blinkDistance;
                        }
                        else if (boss.ActiveAttack.Id == "beam")
                        {
                            boss.AttackAngle -= 0.7f;
                        }
                        else if (boss.ActiveAttack.Id == "burst")
                        {
                            boss.AttackAngle = (float)(_gameSim.Rng.Next() * Math.PI * 2);
                        }
                        boss.State = 1;
                        boss.StateTimer = (float)boss.ActiveAttack.TelegraphSeconds;
                        boss.ActionApplied = false;
                    }
                }

                var contactDelta = _gameSim.Player.Position - boss.Position;
                var contactDistance = SourceLengthOrOne(contactDelta);
                var canContactPlayer = _gameSim.Player.Health > 0 && !_gameOver && !_revivePending &&
                    _gameSim.Player.DyingTimer <= 0 && _gameSim.Player.Iframes <= 0;
                if (boss.Active && contactDistance < boss.Radius + AttackPlayerRadius &&
                    boss.ContactCooldown <= 0 && canContactPlayer)
                {
                    var chargeContact = boss.State == 2 &&
                        boss.ActiveAttack != null && boss.ActiveAttack.Id == "charge";
                    var contactDamage = chargeContact
                        ? (float)boss.ActiveAttack.Damage * boss.DamageScale
                        : boss.Damage;
                    DamagePlayer(contactDamage, contactDelta / contactDistance);
                    boss.ContactCooldown = 0.75f;
                }
                _gameSim.Bosses[i] = boss;
            }
            EnsureBossOrderEntries();
        }

        private void ApplyBossTierPressure(ref BossState boss)
        {
            boss.TierPressureTriggered = true;
            var tier = boss.PressureTier;
            var towardPlayer = Mathf.Atan2(
                _gameSim.Player.Position.y - boss.Position.y,
                _gameSim.Player.Position.x - boss.Position.x);
            if (boss.Id == "warden")
            {
                var count = 3 + Mathf.Min(2, tier);
                for (var index = 0; index < count; index++)
                {
                    var angle = index / (float)count * Mathf.PI * 2 + 0.25f;
                    SpawnEnemy(
                        "chaser",
                        boss.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 115,
                        summonedByBossTelemetryId: boss.TelemetryInstanceId,
                        healthMultiplier: 0.8f,
                        forcedRoster: EnemyRoster.Two);
                }
            }
            else if (boss.Id == "matriarch")
            {
                for (var index = 0; index < 3; index++)
                {
                    var angle = index / 3f * Mathf.PI * 2 - 0.4f;
                    SpawnEnemy(
                        index == 0 ? "guard" : "chaser",
                        boss.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 120,
                        summonedByBossTelemetryId: boss.TelemetryInstanceId,
                        healthMultiplier: 0.86f,
                        forcedRoster: EnemyRoster.Two);
                }
            }
            else if (boss.Id == "herald")
            {
                var count = 12 + Mathf.Min(4, tier * 2);
                for (var index = 0; index < count; index++)
                {
                    var angle = index / (float)count * Mathf.PI * 2 + 0.08f;
                    var difference = Mathf.Atan2(
                        Mathf.Sin(angle - towardPlayer),
                        Mathf.Cos(angle - towardPlayer));
                    if (Mathf.Abs(difference) < 0.34f) continue;
                    SpawnHostileShot(
                        boss.Position,
                        new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)),
                        (8 + tier * 1.5f) * boss.DamageScale,
                        225,
                        0);
                }
            }
            else
            {
                foreach (var spread in BossPressureSpreads)
                {
                    var angle = towardPlayer + spread;
                    SpawnHostileShot(
                        boss.Position,
                        new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)),
                        (9 + tier * 1.5f) * boss.DamageScale,
                        285,
                        0);
                }
            }
            var pressureAccent = BossAccent(boss);
            pressureAccent.a = 0.78f;
            SpawnRingWave(boss.Position, boss.Radius, 330f, 0.52f, pressureAccent);
            BurstFx(boss.Position, BossParticleColor(boss.Id), 18, 250, 0.48f, 0.78f);
            TriggerFreeze(0.045f);
            AddCameraShake(0.32f);
            _audio?.Play(ProceduralAudio.Cue.BossCharge, 0.9f);
            var pressureDefinition = FindBoss(boss.Id);
            ShowArenaToast((pressureDefinition?.Name ?? boss.Id) + " reinforced", 2.5f, ToastKind.Danger);
        }

        private static int BossEncounterCycle(int telemetryInstanceId)
        {
            return Mathf.Max(0, (telemetryInstanceId - 1) / 4);
        }

        private static float BossChargeSpeed(int encounterCycle)
        {
            return 620f + Mathf.Min(90f, Mathf.Max(0, encounterCycle) * 15f);
        }

        private static float BossBeamRotationSpeed(float baseSpeed, int encounterCycle)
        {
            var sweepScale = 1f + Mathf.Min(0.3f, Mathf.Max(0, encounterCycle) * 0.06f);
            return baseSpeed * sweepScale;
        }

        private static float BossSlamRadius(float baseRadius, int encounterCycle)
        {
            return baseRadius + Mathf.Min(40f, Mathf.Max(0, encounterCycle) * 10f);
        }

        private static int BossProjectileCount(int baseCount, bool reinforced, int encounterCycle)
        {
            return baseCount + (reinforced ? 4 : 0) + Mathf.Min(6, Mathf.Max(0, encounterCycle) * 2);
        }

        private static int BossSummonCount(int baseCount, int encounterCycle)
        {
            return baseCount + Mathf.Min(4, Mathf.Max(0, encounterCycle));
        }

        private void ApplyBossAttack(ref BossState boss, BossDefinition definition, float dt)
        {
            var attack = boss.ActiveAttack;
            if (attack == null) return;
            var attackDamage = (float)attack.Damage * boss.DamageScale;
            // Browser boss scaling keys off encounterIndex / 4, not elapsed
            // time. TelemetryInstanceId is encounterIndex + 1 in this pool.
            var encounterCycle = BossEncounterCycle(boss.TelemetryInstanceId);
            if (attack.Id == "charge")
            {
                var chargeSpeed = BossChargeSpeed(encounterCycle);
                boss.Position += boss.DashDirection * chargeSpeed * dt;
                return;
            }
            if (attack.Id == "beam")
            {
                boss.AttackAngle += BossBeamRotationSpeed(
                    (float)(attack.RotationSpeed ?? 0.8),
                    encounterCycle) * dt;
                if (_gameSim.Player.Iframes > 0 || boss.BeamHitCooldown > 0) return;
                var delta = _gameSim.Player.Position - boss.Position;
                var beamX = Mathf.Cos(boss.AttackAngle);
                var beamY = Mathf.Sin(boss.AttackAngle);
                var along = delta.x * beamX + delta.y * beamY;
                var across = Mathf.Abs(delta.x * beamY - delta.y * beamX);
                if (!HazardRules.SegmentedSweepContains(
                    along,
                    across,
                    attack.BeamLength ?? 680,
                    attack.BeamWidth ?? 48,
                    AttackPlayerRadius)) return;
                DamagePlayer(attackDamage, delta);
                boss.BeamHitCooldown = 0.45f;
                return;
            }
            if (boss.ActionApplied) return;
            boss.ActionApplied = true;
            if (attack.Id == "slam")
            {
                _audio?.Play(ProceduralAudio.Cue.BossSlam, 0.92f);
                AddCameraShake(0.55f);
                var radius = BossSlamRadius((float)(attack.Radius ?? 190), encounterCycle);
                var delta = _gameSim.Player.Position - boss.Position;
                SpawnRingWave(boss.Position, 24f, radius * 2.2f, 0.62f, BossAccent(boss));
                if (delta.magnitude < radius + AttackPlayerRadius) DamagePlayer(attackDamage, delta);
            }
            else if (attack.Id == "burst" || attack.Id == "volley")
            {
                var count = BossProjectileCount(
                    attack.ProjectileCount ?? 12,
                    boss.Reinforced,
                    encounterCycle);
                var volleyBaseAngle = Mathf.Atan2(boss.DashDirection.y, boss.DashDirection.x);
                for (var index = 0; index < count; index++)
                {
                    var angle = attack.Id == "volley"
                        ? volleyBaseAngle + (index / Mathf.Max(1f, count - 1f) - 0.5f) * 52f * Mathf.Deg2Rad
                        : boss.AttackAngle + index / (float)count * Mathf.PI * 2;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    SpawnHostileShot(
                        boss.Position,
                        direction,
                        attackDamage,
                        (float)(attack.ProjectileSpeed ?? 215),
                        0);
                }
                SpawnRingWave(boss.Position, 20f, 180f, 0.35f, BossAccent(boss));
                _audio?.Play(ProceduralAudio.Cue.GunnerShot, 0.9f);
            }
            else if (attack.Id == "summon")
            {
                var count = BossSummonCount(attack.SummonCount ?? 6, encounterCycle);
                for (var index = 0; index < count; index++)
                {
                    var angle = index / (float)count * Mathf.PI * 2 + (float)_gameSim.Rng.Next() * 0.16f;
                    SpawnEnemy(
                        index % 3 == 0 ? "runner" : "chaser",
                        boss.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 105,
                        null,
                        false,
                        false,
                        boss.TelemetryInstanceId,
                        0.78f);
                }
                SpawnRingWave(boss.Position, 24f, 250f, 0.5f, BossAccent(boss));
                _audio?.Play(ProceduralAudio.Cue.Warning, 0.9f);
            }
            else if (attack.Id == "blink")
            {
                boss.Position = boss.TargetPosition;
                var delta = _gameSim.Player.Position - boss.Position;
                var radius = (float)(attack.Radius ?? 80);
                SpawnRingWave(boss.Position, 18f, radius * 2.2f, 0.42f,
                    new Color(0.376f, 0.647f, 0.98f, 0.8f));
                BurstFx(boss.Position, SourceDotColor("blue"),
                    14, 270, 0.48f, 0.8f);
                if (delta.magnitude < radius + AttackPlayerRadius) DamagePlayer(attackDamage, delta);
                _audio?.Play(ProceduralAudio.Cue.Dash, 0.94f);
            }
        }

        private void ResetEnemyOrder() => _gameSim.ResetEnemyOrder();
        private void AppendEnemyOrder(int slot) => _gameSim.AppendEnemyOrder(slot);
        private void RemoveEnemyOrder(int slot) => _gameSim.RemoveEnemyOrder(slot);

        private void ResetBossOrder() => _gameSim.ResetBossOrder();
        private void AppendBossOrder(int slot) => _gameSim.AppendBossOrder(slot);
        private void RemoveBossOrder(int slot) => _gameSim.RemoveBossOrder(slot);
        private void EnsureBossOrderEntries() => _gameSim.EnsureBossOrderEntries();

        private void RebuildEnemyGrid() => _gameSim.RebuildEnemyGrid();
        private bool IsCurrentGridEnemy(int index) => _gameSim.IsCurrentGridEnemy(index);

        private void ResetMeteorOrder() => _gameSim.ResetMeteorOrder();
        private void AppendMeteorOrder(int slot) => _gameSim.AppendMeteorOrder(slot);
        private void RemoveMeteorOrder(int slot) => _gameSim.RemoveMeteorOrder(slot);
        private void EnsureMeteorOrderEntries() => _gameSim.EnsureMeteorOrderEntries();

        private void ResetPickupOrder() => _gameSim.ResetPickupOrder();
        private void AppendPickupOrder(int slot) => _gameSim.AppendPickupOrder(slot);
        private void RemovePickupOrder(int slot) => _gameSim.RemovePickupOrder(slot);
        private void RemovePickupOrderAt(int position) => _gameSim.RemovePickupOrderAt(position);
        private void ResetDamageIndicatorOrder() => _damageIndicatorOrder.Reset();

        private void AppendDamageIndicatorOrder(int slot) => _damageIndicatorOrder.Append(slot);

        private void RemoveDamageIndicatorOrder(int slot) => _damageIndicatorOrder.Remove(slot);

        private void EnsureDamageIndicatorOrderEntries()
        {
            for (var index = 0; index < _damageIndicators.Length; index++)
            {
                if (_damageIndicators[index].Active) AppendDamageIndicatorOrder(index);
            }
            for (var order = _damageIndicatorOrder.Count - 1; order >= 0; order--)
            {
                var slot = _damageIndicatorOrder.SlotAt(order);
                if (slot < 0 || slot >= _damageIndicators.Length || !_damageIndicators[slot].Active)
                    RemoveDamageIndicatorOrder(slot);
            }
        }

        private void ResetBulletOrder() => _gameSim.BulletOrder.Reset();

        private void AppendBulletOrder(int slot) => _gameSim.BulletOrder.Append(slot);

        private void RemoveBulletOrder(int slot) => _gameSim.BulletOrder.Remove(slot);

        private void EnsureBulletOrderEntries()
        {
            for (var index = 0; index < _gameSim.Bullets.Length; index++)
            {
                if (_gameSim.Bullets[index].Active) AppendBulletOrder(index);
            }
            for (var order = _gameSim.BulletOrder.Count - 1; order >= 0; order--)
            {
                var slot = _gameSim.BulletOrder.SlotAt(order);
                if (slot < 0 || !_gameSim.Bullets[slot].Active) RemoveBulletOrder(slot);
            }
        }

        private EnemyEffectTarget[] CaptureEnemyEffectSnapshot(out int snapshotCount) => _gameSim.CaptureEnemyEffectSnapshot(out snapshotCount);
        private static void ReleaseEnemyEffectSnapshot(EnemyEffectTarget[] snapshot) => GameSim.ReleaseEnemyEffectSnapshot(snapshot);
        private bool IsLiveEnemyEffectTarget(EnemyEffectTarget target) => _gameSim.IsLiveEnemyEffectTarget(target);
        /// <summary>
        /// Persistent per-enemy angular offset applied to a beeline approach.
        /// Seed is a stable per-spawn value in [0, 100), so mapping it to a
        /// signed fraction gives each body its own bias and its own handedness
        /// while staying deterministic for a given run seed. Roughly half the
        /// population curves each way, which splits an incoming crowd into two
        /// counter-rotating streams rather than one packed arc.
        ///
        /// The bias tapers to zero as the body closes, so the fan shapes the
        /// long approach but the endgame still commits straight at the player
        /// and close-quarters fights stay readable.
        /// </summary>
        private static float ApproachBias(float seed, float distance)
        {
            var handed = Mathf.Repeat(seed, 100f) * 0.01f * 2f - 1f;
            var taper = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    ApproachBiasCommitDistance,
                    ApproachBiasFullDistance,
                    distance));
            return handed * ApproachBiasMaxRadians * taper;
        }

        private void UpdateBullets(float dt)
        {
            // The loop skeleton, homing targeting, identity bookkeeping and
            // hit resolution live in GameSim.AdvanceBullets; the runtime owns
            // the FX/damage cascades behind these cached hooks. Hook bodies
            // are the original in-loop blocks, unchanged. Delegates are cached
            // once - no per-step allocation.
            if (_bulletTrailHook == null)
            {
                _bulletTrailHook = slot =>
                {
                    var bullet = _gameSim.Bullets[slot];
                    // Browser updateBullets emits the projectile trail only for
                    // homing bullets, before movement, with the shared FX RNG.
                    // The browser guards the FX RNG draw behind the quality
                    // flag; Low quality must not advance the shared visual
                    // stream when projectile trails are disabled.
                    if (_qualityPreset.ProjectileTrails &&
                        SourceProjectileTrailEligible(true, true, _fxSim.FxRng.Next()))
                    {
                        EmitTrailParticle(
                            bullet.Position,
                            new Vector2(
                                ((float)_fxSim.FxRng.Next() - 0.5f) * 24f,
                                ((float)_fxSim.FxRng.Next() - 0.5f) * 24f),
                            new Color(163f / 255f, 230f / 255f, 53f / 255f, 1f),
                            0.2f,
                            0.42f);
                    }
                };
                _bulletEnemyHitHook = (slot, enemyIndex) =>
                {
                    var bullet = _gameSim.Bullets[slot];
                    var enemy = _gameSim.Enemies[enemyIndex];
                    var railgun = bullet.WeaponIndex >= 0 &&
                        bullet.WeaponIndex < ContentCatalog.Weapons.Length &&
                        ContentCatalog.Weapons[bullet.WeaponIndex].Id == "railgun";
                    var critical = _gameSim.Rng.Next() < _critChance;
                    ApplyEnemyDamage(enemyIndex, bullet.Damage * (critical ? 2.1f : 1),
                        bullet.Velocity, bullet.Knockback, critical, bullet.WeaponIndex);
                    if (railgun) RailgunImpact(enemy.Position, bullet.Hits);
                    if (bullet.BlastRadius > 0)
                    {
                        var weaponId = ContentCatalog.Weapons[Mathf.Clamp(bullet.WeaponIndex, 0, ContentCatalog.Weapons.Length - 1)].Id;
                        DamageArea(bullet.Position, bullet.BlastRadius,
                            bullet.Damage * (weaponId == "seeker" ? 0.8f : 0.35f),
                            bullet.HitEnemy0, bullet.WeaponIndex);
                    }
                    if (bullet.Cluster)
                    {
                        bullet.Cluster = false;
                        SpawnClusterChargesAfterEnemyHit(bullet, bullet.HitEnemy0);
                        _gameSim.Bullets[slot] = bullet;
                    }
                };
                _bulletBossHitHook = (slot, bossIndex) =>
                {
                    var bullet = _gameSim.Bullets[slot];
                    var boss = _gameSim.Bosses[bossIndex];
                    var railgun = bullet.WeaponIndex >= 0 &&
                        bullet.WeaponIndex < ContentCatalog.Weapons.Length &&
                        ContentCatalog.Weapons[bullet.WeaponIndex].Id == "railgun";
                    var critical = _gameSim.Rng.Next() < _critChance;
                    ApplyBossDamage(
                        bossIndex,
                        bullet.Damage * (critical ? 2.1f : 1),
                        bullet.WeaponIndex,
                        critical);
                    if (railgun) RailgunImpact(boss.Position, bullet.Hits);
                    if (bullet.BlastRadius > 0)
                    {
                        // The browser gives a boss hit a cyan presentation
                        // cue only; its blast damage is for enemy/meteor
                        // hits, not an extra boss-area hit.
                        BurstFx(
                            bullet.Position,
                            SourceDotColor("cyan"),
                            7,
                            190,
                            0.35f,
                            0.7f);
                    }
                    if (bullet.Cluster)
                    {
                        bullet.Cluster = false;
                        SpawnClusterCharges(bullet);
                        _gameSim.Bullets[slot] = bullet;
                    }
                };
                _bulletMeteorHitHook = (slot, meteorIndex) =>
                {
                    var bullet = _gameSim.Bullets[slot];
                    var meteor = _gameSim.Meteors[meteorIndex];
                    if (!DamageMeteor(meteorIndex, bullet.Damage)) return false;
                    var railgun = bullet.WeaponIndex >= 0 &&
                        bullet.WeaponIndex < ContentCatalog.Weapons.Length &&
                        ContentCatalog.Weapons[bullet.WeaponIndex].Id == "railgun";
                    if (railgun) RailgunImpact(meteor.Position, bullet.Hits);
                    if (bullet.BlastRadius > 0)
                    {
                        var weaponId = ContentCatalog.Weapons[
                            Mathf.Clamp(bullet.WeaponIndex, 0, ContentCatalog.Weapons.Length - 1)].Id;
                        DamageArea(
                            bullet.Position,
                            bullet.BlastRadius,
                            bullet.Damage * (weaponId == "seeker" ? 0.8f : 0.35f),
                            -1,
                            bullet.WeaponIndex);
                    }
                    return true;
                };
                _bulletRicochetHook = slot =>
                {
                    var bullet = _gameSim.Bullets[slot];
                    if (!RetargetRicochet(ref bullet)) return false;
                    _gameSim.Bullets[slot] = bullet;
                    BurstFx(bullet.Position, SourceDotColor("white"), 4, 140, 0.24f, 0.55f);
                    return true;
                };
            }
            _gameSim.BulletTrailHook = _bulletTrailHook;
            _gameSim.BulletEnemyHitHook = _bulletEnemyHitHook;
            _gameSim.BulletBossHitHook = _bulletBossHitHook;
            _gameSim.BulletMeteorHitHook = _bulletMeteorHitHook;
            _gameSim.BulletRicochetHook = _bulletRicochetHook;

            _gameSim.AdvanceBullets(dt, _bulletExpiredSlots, out var expiredCount);
            for (var slotIndex = 0; slotIndex < expiredCount; slotIndex++)
            {
                Hide(_bulletViews[_bulletExpiredSlots[slotIndex]]);
                Hide(_bulletContrastViews[_bulletExpiredSlots[slotIndex]]);
            }
        }

        internal static bool SourceRosterPincerDashFxEligible(bool effectsEnabled, double fxRoll)
        {
            return effectsEnabled && fxRoll < 0.32;
        }

        private bool BulletAlreadyHitEnemy(BulletState bullet, int enemyIndex)
        {
            if (enemyIndex < 0 || enemyIndex >= _gameSim.Enemies.Length) return false;
            var identity = EnemyIdentity(_gameSim.Enemies[enemyIndex], enemyIndex);
            return bullet.HitEnemy0 == identity || bullet.HitEnemy1 == identity ||
                bullet.HitEnemy2 == identity || bullet.HitEnemy3 == identity;
        }

        private void SpawnClusterCharges(BulletState source)
        {
            SpawnClusterChargesCore(source, -1);
        }

        private void SpawnClusterChargesAfterEnemyHit(BulletState source, int excludedEnemyIdentity)
        {
            SpawnClusterChargesCore(source, excludedEnemyIdentity);
        }

        private void SpawnClusterChargesCore(BulletState source, int excludedEnemyIdentity)
        {
            var weaponIndex = 5;
            if (_upgradeProgress == null || _upgradeProgress.WeaponRanks.Length <= weaponIndex) return;
            var rank = _upgradeProgress.WeaponRanks[weaponIndex];
            if (rank <= 0) return;
            var stats = ContentCatalog.Weapons[weaponIndex].Ranks[Mathf.Clamp(rank, 1, 6) - 1].Stats;
            var visited = _clusterVisited;
            var visitedCount = 0;
            if (excludedEnemyIdentity >= 0) AddVisited(visited, ref visitedCount, excludedEnemyIdentity);
            for (var index = 0; index < _gameSim.Bosses.Length; index++)
            {
                if (BossAlreadyHit(source, _gameSim.Bosses[index], index))
                    AddVisited(visited, ref visitedCount, -BossIdentity(_gameSim.Bosses[index], index));
            }
            var sourceAngle = Mathf.Atan2(source.Velocity.y, source.Velocity.x);
            for (var charge = 0; charge < 3; charge++)
            {
                // React's nearestUnvisitedHostile uses only this local visited
                // set; parent enemy hit history must not suppress new cluster
                // targets, especially for a boss/meteor-triggered cluster.
                var target = FindNearestHostileFrom(
                    source.Position,
                    440,
                    source,
                    false,
                    null,
                    visited,
                    visitedCount);
                _clusterTargets[charge] = target;
                if (target.Valid)
                    AddVisited(visited, ref visitedCount, target.Boss
                        ? -target.Identity
                        : target.Identity);
            }
            for (var charge = 0; charge < 3; charge++)
            {
                if (ActiveBullets() >= MaxBullets) break;
                var target = _clusterTargets[charge];
                var angle = target.Valid
                    ? Mathf.Atan2(target.Position.y - source.Position.y, target.Position.x - source.Position.x)
                    : sourceAngle + charge * Mathf.PI * 2 / 3;
                SpawnWeaponProjectileFromPosition(
                    weaponIndex,
                    stats,
                    source.Position,
                    angle,
                    rank,
                    0.42f,
                    0.82f,
                    0,
                    Mathf.Max(24f, (float)stats.BlastRadius * 0.42f),
                    false,
                    0,
                    10f,
                    410f,
                    1.15f,
                    Mathf.Max(3f, source.Radius * 0.58f),
                    source.Knockback * 0.5f,
                    false,
                    excludedEnemyIdentity,
                    source.BossHit0,
                    source.BossHit1,
                    source.BossHit2,
                    source.BossHit3);
            }
            BurstFx(source.Position, SourceDotColor("lime"),
                9, 210, 0.38f, 0.7f);
            SpawnRingWave(source.Position, 7f, 180f, 0.26f,
                new Color(0.639f, 0.902f, 0.216f, 0.72f));
        }

        private static int EnemyIdentity(EnemyState enemy, int slot)
        {
            // SpawnId is the browser-equivalent object identity. A few
            // reflection fixtures construct a zero-initialized EnemyState, so
            // retain deterministic slot identity only for that test-only case.
            return enemy.SpawnId > 0 ? enemy.SpawnId : slot;
        }

        private static int BossIdentity(BossState boss, int slot)
        {
            // TelemetryInstanceId is the browser boss instance identity. A
            // slot fallback keeps reflection fixtures deterministic.
            return boss.TelemetryInstanceId > 0 ? boss.TelemetryInstanceId : slot + 1;
        }

        private static bool BossAlreadyHit(BulletState bullet, BossState boss, int slot)
        {
            var identity = BossIdentity(boss, slot);
            return bullet.BossHit0 == identity || bullet.BossHit1 == identity ||
                bullet.BossHit2 == identity || bullet.BossHit3 == identity;
        }

        private void UpdatePickups(float dt)
        {
            // Movement, magnet pull and collection detection live in
            // GameSim.AdvancePickups; the runtime applies collected effects
            // through this cached hook, invoked after the slot is freed so a
            // Bomb reward drop can reuse it within the same step.
            if (_pickupCollectedHook == null)
            {
                _pickupCollectedHook = (slot, order, collectedFromPull) =>
                {
                    var pickup = _gameSim.Pickups[slot];
                    Hide(_pickupViews[slot]);
                    RemovePickupOrderAt(order);
                    if (pickup.Kind == PickupKind.Xp)
                    {
                        // GREED doubles every source; the pull flag was
                        // captured before the slot freed.
                        if (collectedFromPull) collectedFromPull = !HasWildCard(WildCardId.Greed);
                        _xp += pickup.Value * GreedXpMultiplier();
                        _telemetry.RecordXpCollected(pickup.Value);
                        _pickupStep = _pickupStepTimer > 0 ? _pickupStep + 1 : 0;
                        _pickupStepTimer = 0.9f;
                        // A magnet pull can collect most of the 280-entry pool
                        // in a moment. Keep the pitch climb, but coalesce the
                        // individual ticks while the aggregate music effect is
                        // doing the heavy lifting.
                        if (!collectedFromPull || (_pickupStep & 3) == 0)
                            _audio?.PlayGem(_pickupStep);
                        BurstFx(_gameSim.Player.Position, SourceDotColor("emerald"), 2, 120, 0.25f, 0.6f);
                    }
                    else if (pickup.Kind == PickupKind.Part)
                    {
                        var parts = Mathf.Max(1, Mathf.RoundToInt(pickup.Value));
                        _partsEarned += parts;
                        SpawnFloater(
                            _gameSim.Player.Position + Vector2.up * 18f,
                            "+" + parts + " Part" + (parts > 1 ? "s" : string.Empty),
                            new Color(0.98f, 0.79f, 0.08f, 1f),
                            12);
                        _audio?.Play(ProceduralAudio.Cue.Currency, 1f);
                        BurstFx(_gameSim.Player.Position, SourceDotColor("yellow"), 3, 130, 0.28f, 0.65f);
                    }
                    else if (pickup.Kind == PickupKind.Magnet)
                    {
                        // GREED disables every magnet effect, including this
                        // pickup's pull-all - no hidden exceptions (spec 44.3).
                        if (!HasWildCard(WildCardId.Greed))
                        {
                            for (var otherIndex = 0; otherIndex < _gameSim.Pickups.Length; otherIndex++)
                            {
                                var other = _gameSim.Pickups[otherIndex];
                                if (other.Active && other.Kind == PickupKind.Xp)
                                {
                                    other.Pull = true;
                                    _gameSim.Pickups[otherIndex] = other;
                                }
                            }
                        }
                        _audio?.Play(ProceduralAudio.Cue.Pickup, 1f);
                        BurstFx(_gameSim.Player.Position, SourceDotColor("cyan"), 12, 230, 0.45f, 0.8f);
                        ShowArenaToast("Experience pulled in", 2.5f, ToastKind.Reward);
                    }
                    else if (pickup.Kind == PickupKind.Repair)
                    {
                        var before = _gameSim.Player.Health;
                        _gameSim.Player.Health = Mathf.Min(
                            _gameSim.Player.MaxHealth,
                            _gameSim.Player.Health + Mathf.Max(20, _gameSim.Player.MaxHealth * 0.22f));
                        var restored = SourceRound(_gameSim.Player.Health - before);
                        _audio?.Play(ProceduralAudio.Cue.Pickup, 1f);
                        BurstFx(_gameSim.Player.Position, SourceDotColor("emerald"), 12, 220, 0.45f, 0.8f);
                        ShowArenaToast(
                            "Integrity restored  " + (restored > 0 ? "+" + restored : "Already full"),
                            2.5f,
                            ToastKind.Reward);
                    }
                    else if (pickup.Kind == PickupKind.Bomb)
                    {
                        DetonateBomb();
                    }
                    else if (pickup.Kind == PickupKind.Overdrive)
                    {
                        var previousStreak = _overclock.Streak;
                        _overclock.ApplyPickup();
                        _overclockHudPunch = 1f;
                        _overclockVisualSurge = _overclock.Streak >= 4 ? 1f : 0.72f;
                        _music?.NotifyOverclockStreak(previousStreak, _overclock.Streak);
                        _cyanFlash = Mathf.Max(_cyanFlash, 0.3f);
                        _audio?.Play(ProceduralAudio.Cue.Pickup, 1f);
                        SpawnRingWave(_gameSim.Player.Position, 14f, 250f, 0.4f,
                            new Color(0.35f, 0.95f, 1f, 0.72f));
                        BurstFx(_gameSim.Player.Position, SourceDotColor("yellow"), 16, 260, 0.5f, 0.8f);
                        BurstFx(_gameSim.Player.Position, SourceDotColor("white"), 7, 180, 0.3f, 0.65f);
                        ShowArenaToast(
                            _overclock.Streak > 1
                                ? "OVERCLOCKED ×" + _overclock.Streak
                                : "OVERCLOCKED",
                            2.5f,
                            ToastKind.Reward);
                    }
                    else if (pickup.Kind == PickupKind.TrackShift)
                    {
                        // Spec section 50: crossfade to the next track at a
                        // combat entry point; combat never pauses.
                        _music?.ShiftToNextCombatTrack();
                        _score += 150;
                        BurstFx(_gameSim.Player.Position, SourceDotColor("cyan"), 10, 200, 0.4f, 0.75f);
                        ShowArenaToast("TRACK SHIFT", 2f, ToastKind.Reward);
                    }
                    _telemetry.RecordPickup(PickupKindName(pickup.Kind), pickup.Value);
                };
            }
            _gameSim.PickupCollectedHook = _pickupCollectedHook;
            var greedActive = HasWildCard(WildCardId.Greed);

            _gameSim.AdvancePickups(
                dt,
                greedActive ? 0f : _pickupRadius + _workshopMagnet * 8,
                PlayerRadius,
                _gameSim.Player.Health > 0 && !_gameOver && !_revivePending,
                out var pulledXpCount,
                out var pulledXpValue);
            _magnetTarget = MusicReactiveMath.MagnetTarget(pulledXpCount, pulledXpValue);
            _pickupStepTimer = Mathf.Max(0, _pickupStepTimer - dt);
            if (_pickupStepTimer <= 0) _pickupStep = 0;
        }

        private void UpdateWeapons(float dt)
        {
            if (_upgradeProgress == null || _gameSim.Player.Health <= 0 || _gameOver || _revivePending) return;
            UpdatePulseBurst(dt);
            var recoveryScale = WeaponRecoveryScale();
            var weaponCount = Mathf.Min(
                ContentCatalog.Weapons.Length,
                Mathf.Min(_upgradeProgress.WeaponRanks.Length, _weaponCooldowns.Length));
            for (var weaponIndex = 0; weaponIndex < weaponCount; weaponIndex++)
            {
                var weapon = ContentCatalog.Weapons[weaponIndex];
                var rank = _upgradeProgress.WeaponRanks[weaponIndex];
                _weaponCooldowns[weaponIndex] -= dt;
                if (rank <= 0 || weapon.Kind == "orbit") continue;
                var rankDefinition = weapon.Ranks[Mathf.Clamp(rank, 1, weapon.Ranks.Length) - 1];
                var stats = rankDefinition.Stats;
                if (_weaponCooldowns[weaponIndex] > 0) continue;
                var target = FindNearestHostile((float)stats.Range);
                if (!target.Valid)
                {
                    _weaponCooldowns[weaponIndex] = 0;
                    continue;
                }

                _weaponCooldowns[weaponIndex] = (float)stats.Cooldown * recoveryScale *
                    (weapon.Id == "pistol" && _upgradeProgress.Evolved[weaponIndex] ? 1.32f : 1f);
                if (weapon.Id == "pistol" && _upgradeProgress.Evolved[weaponIndex])
                {
                    if (_pulseBurstShots <= 0)
                    {
                        _pulseBurstShots = 3;
                        _pulseBurstTimer = 0;
                    }
                    continue;
                }

                FireWeapon(weaponIndex, stats, rank, target);
            }
        }

        private void UpdatePulseBurst(float dt)
        {
            if (_pulseBurstShots <= 0 || _gameSim.Player.Health <= 0 || _gameOver || _revivePending) return;
            _pulseBurstTimer -= dt;
            if (_pulseBurstTimer > 0) return;

            var rank = _upgradeProgress.WeaponRanks[0];
            var weapon = ContentCatalog.Weapons[0];
            var stats = weapon.Ranks[Mathf.Clamp(rank, 1, weapon.Ranks.Length) - 1].Stats;
            var target = FindNearestHostile((float)stats.Range);
            if (!target.Valid)
            {
                _pulseBurstShots = 0;
                return;
            }

            var direction = target.Position - _gameSim.Player.Position;
            var angle = Mathf.Atan2(direction.y, direction.x);
            var thirdRound = _pulseBurstShots == 1;
            var pulseAngles = CombatRules.ProjectileAngles(
                angle,
                stats.ProjectileCount,
                (float)stats.SpreadDegrees);
            for (var index = 0; index < pulseAngles.Length; index++)
            {
                // Browser pulse rounds retain the current rank's full
                // projectile count, including the third-round ricochet flag.
                SpawnWeaponProjectile(
                    0,
                    stats,
                    (float)pulseAngles[index],
                    rank,
                    thirdRound ? 1.35f : 0.9f,
                    thirdRound ? 1.35f : 1f,
                    thirdRound ? 1 : (int?)null,
                    null,
                    false,
                    thirdRound ? 1 : 0);
            }
            _audio?.Play(ProceduralAudio.Cue.Fire);
            BurstFx(
                _gameSim.Player.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 20f,
                thirdRound ? SourceDotColor("white") : SourceDotColor("cyan"),
                thirdRound ? 4 : 2,
                150,
                0.16f,
                0.55f);
            _pulseBurstShots--;
            _pulseBurstTimer = 0.085f *
                (float)OverclockRules.CooldownMultiplier(_overclock.PowerTier);
        }

        private float WeaponRecoveryScale()
        {
            // COLOSSUS ARSENAL: -25% fire rate, applied after every other
            // recovery source so the penalty cannot be out-built.
            var scale = (float)CombatRules.WeaponRecoveryMultiplier(
                _cooldownMultiplier,
                _adrenalTimer > 0 ? SupportRank("adrenal") : 0,
                (float)OverclockRules.CooldownMultiplier(_overclock.PowerTier));
            return HasWildCard(WildCardId.ColossusArsenal)
                ? scale * (float)WildCardRules.ColossusRecoveryPenaltyMultiplier
                : scale;
        }

        private void FireWeapon(int weaponIndex, WeaponStatsDefinition stats, int rank, HostileTarget target)
        {
            var weapon = ContentCatalog.Weapons[weaponIndex];
            if (weapon.Kind == "chain")
            {
                FireArc(weaponIndex, stats, rank, target);
                return;
            }
            // Browser authority returns before any weapon cue or muzzle FX when
            // the player-bullet array is already at capacity. Preserve that
            // boundary so a rejected shot stays silent and side-effect free.
            if (ActiveBullets() >= MaxBullets) return;
            var direction = target.Position - _gameSim.Player.Position;
            var baseAngle = Mathf.Atan2(direction.y, direction.x);
            var evolved = _upgradeProgress.Evolved[weaponIndex];
            var spread = weapon.Id == "scattergun" && evolved ? 14 : (float)stats.SpreadDegrees;
            var angles = CombatRules.ProjectileAngles(baseAngle, stats.ProjectileCount, spread);
            for (var index = 0; index < angles.Length; index++)
            {
                // Browser fireWeapon breaks before calculating the next
                // Scattergun jitter when the player-bullet array fills. Do
                // not consume gameplay RNG for a shot SpawnWeaponProjectile
                // cannot create.
                if (ActiveBullets() >= MaxBullets) break;
                var angle = (float)angles[index];
                if (weapon.Id == "scattergun")
                {
                    angle += ((float)_gameSim.Rng.Next() - 0.5f) * spread * Mathf.Deg2Rad * 0.12f;
                }
                SpawnWeaponProjectile(weaponIndex, stats, angle, rank, 1, 1, null, null,
                    weapon.Id == "seeker" && evolved);
            }

            if (weapon.Id == "scattergun" && evolved)
            {
                SpawnWeaponProjectile(weaponIndex, stats, baseAngle, rank, 2.35f,
                    Mathf.Max(1.45f, 7f / Mathf.Max(1f, (float)stats.ProjectileRadius)),
                    Mathf.Max(3, stats.Pierce + 2), 56, false);
            }
            if (weapon.Id == "railgun" && evolved)
            {
                var railDirection = new Vector2(Mathf.Cos(baseAngle), Mathf.Sin(baseAngle));
                var start = _gameSim.Player.Position + railDirection * 18;
                StartRailTrail(
                    start,
                    start + railDirection * (float)stats.Range,
                    (float)stats.Damage * _damageMultiplier * 0.18f,
                    weaponIndex);
            }
            if (weapon.Id == "scattergun")
            {
                _audio?.Play(ProceduralAudio.Cue.Scattergun);
                AddCameraShake(0.12f);
            }
            else if (weapon.Id == "railgun")
            {
                _audio?.Play(ProceduralAudio.Cue.Railgun, 0.78f);
                AddCameraShake(0.24f);
                TriggerFreeze(0.035f);
            }
            else if (weapon.Id == "seeker")
            {
                _audio?.Play(ProceduralAudio.Cue.Seeker);
            }
            else
            {
                _audio?.Play(ProceduralAudio.Cue.Fire);
            }
            if (weapon.Id == "railgun")
            {
                var muzzle = _gameSim.Player.Position + new Vector2(Mathf.Cos(baseAngle), Mathf.Sin(baseAngle)) * 22f;
                BurstFx(muzzle, SourceDotColor("white"), 6, 250, 0.2f, 0.48f);
                BurstFx(muzzle, SourceDotColor("violet"),
                    7, 190, 0.24f, 0.58f);
                SpawnRingWave(muzzle, 5f, 135f, 0.22f,
                    new Color(0.655f, 0.545f, 0.98f, 0.72f));
            }
            var muzzleColor = weapon.Id == "scattergun"
                ? SourceDotColor("orange")
                : weapon.Id == "railgun"
                    ? SourceDotColor("violet")
                    : weapon.Id == "seeker"
                        ? SourceDotColor("lime")
                        : SourceDotColor("cyan");
            BurstFx(
                _gameSim.Player.Position + new Vector2(Mathf.Cos(baseAngle), Mathf.Sin(baseAngle)) * 20f,
                muzzleColor,
                2 + Mathf.FloorToInt(rank / 2f),
                130,
                0.16f,
                0.55f);
        }

        private void SpawnWeaponProjectile(
            int weaponIndex,
            WeaponStatsDefinition stats,
            float angle,
            int rank,
            float damageScale,
            float radiusScale,
            int? pierceOverride,
            float? blastRadiusOverride,
            bool cluster,
            int ricochetOverride = 0)
        {
            SpawnWeaponProjectileFromPosition(
                weaponIndex,
                stats,
                _gameSim.Player.Position,
                angle,
                rank,
                damageScale,
                radiusScale,
                pierceOverride,
                blastRadiusOverride,
                cluster,
                ricochetOverride);
        }

        private void SpawnWeaponProjectileFromPosition(
            int weaponIndex,
            WeaponStatsDefinition stats,
            Vector2 origin,
            float angle,
            int rank,
            float damageScale,
            float radiusScale,
            int? pierceOverride,
            float? blastRadiusOverride,
            bool cluster,
            int ricochetOverride = 0,
            float homingTurnRateOverride = -1f,
            float projectileSpeedOverride = -1f,
            float lifeOverride = -1f,
            float radiusOverride = -1f,
            float knockbackOverride = -1f,
            bool offsetOrigin = true,
            int excludedEnemyIndex = -1,
            int bossHit0Override = -1,
            int bossHit1Override = -1,
            int bossHit2Override = -1,
            int bossHit3Override = -1)
        {
            if ((float)stats.ProjectileSpeed <= 0) return;
            var slot = FindInactive(_gameSim.Bullets);
            if (slot < 0) return;
            var weapon = ContentCatalog.Weapons[weaponIndex];
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var radius = radiusOverride > 0
                ? radiusOverride
                : (float)stats.ProjectileRadius * radiusScale;
            // COLOSSUS ARSENAL: double projectile size (spec 44.4).
            if (HasWildCard(WildCardId.ColossusArsenal)) radius *= 2f;
            var blastRadius = blastRadiusOverride.HasValue
                ? blastRadiusOverride.Value * _areaMultiplier
                : (float)stats.BlastRadius * _areaMultiplier;
            _gameSim.Bullets[slot] = new BulletState
            {
                Active = true,
                Position = offsetOrigin ? origin + direction * 18 : origin,
                Velocity = direction * (projectileSpeedOverride > 0 ? projectileSpeedOverride : (float)stats.ProjectileSpeed),
                Damage = (float)stats.Damage * _damageMultiplier * damageScale,
                Life = lifeOverride > 0
                    ? lifeOverride
                    : (float)stats.Range / Mathf.Max(1, (float)stats.ProjectileSpeed),
                Radius = radius,
                WeaponIndex = weaponIndex,
                Rank = rank,
                PierceRemaining = pierceOverride ?? stats.Pierce,
                HitEnemy0 = excludedEnemyIndex,
                HitEnemy1 = -1,
                HitEnemy2 = -1,
                HitEnemy3 = -1,
                BossHitMask = 0,
                BossHit0 = bossHit0Override,
                BossHit1 = bossHit1Override,
                BossHit2 = bossHit2Override,
                BossHit3 = bossHit3Override,
                Ricochets = ricochetOverride,
                Knockback = knockbackOverride >= 0 ? knockbackOverride : (float)stats.Knockback,
                BlastRadius = blastRadius,
                Homing = weapon.Id == "seeker",
                HomingTurnRate = weapon.Id == "seeker"
                    ? (homingTurnRateOverride > 0 ? homingTurnRateOverride : 7f)
                    : 0,
                HomingTargetIndex = -1,
                HomingTargetIdentity = -1,
                HomingTargetBoss = false,
                HomingRefreshTimer = 0,
                Cluster = cluster,
                Evolved = _upgradeProgress.Evolved[weaponIndex],
                Hits = 0,
                View = slot,
            };
            AppendBulletOrder(slot);
            var view = EnsureBulletView(slot);
            var frame = SourceProjectileFrameIndex(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
            view.sprite = ProceduralSpriteFactory.ProjectileFrame(weapon.Id, frame);
            view.transform.rotation = Quaternion.identity;
            view.color = Color.white;
            view.enabled = true;
            var contrast = _bulletContrastViews[slot];
            contrast.sprite = view.sprite;
            contrast.transform.rotation = Quaternion.identity;
            contrast.color = new Color(1f, 1f, 1f, 0.9f);
            contrast.enabled = false;
        }

        private void FireArc(int weaponIndex, WeaponStatsDefinition stats, int rank, HostileTarget first)
        {
            if (!first.Valid) return;
            var visited = _arcVisited;
            var visitedCount = 0;
            var evolved = _upgradeProgress.Evolved[weaponIndex];
            var endpoint = FireArcChainBuffered(
                stats,
                first,
                visited,
                ref visitedCount,
                out var points,
                weaponIndex);
            CreateArcEffect(points.ToArray(), evolved);
            if (evolved)
            {
                // Browser authority resolves the evolved second chain before
                // emitting the first endpoint burst. If no second target is
                // available, the first endpoint still receives its burst.
                var second = FindNearestUnvisitedHostile(
                    _gameSim.Player.Position,
                    (float)stats.Range,
                    visited,
                    visitedCount);
                if (second.Valid)
                {
                    var secondEndpoint = FireArcChainBuffered(
                        stats,
                        second,
                        visited,
                        ref visitedCount,
                        out var secondPoints,
                        weaponIndex);
                    CreateArcEffect(secondPoints.ToArray(), true);
                    ArcEndpointBurst(secondEndpoint, (float)stats.Damage * _damageMultiplier, weaponIndex);
                }
            }
            if (evolved) ArcEndpointBurst(endpoint, (float)stats.Damage * _damageMultiplier, weaponIndex);
            _audio?.Play(ProceduralAudio.Cue.Arc);
        }

        // Keep the original private signature for reflection fixtures and
        // older editor tooling. Live gameplay uses the fixed-buffer overload
        // below, so this compatibility path is not on the normal cast path.
        private HostileTarget FireArcChain(
            WeaponStatsDefinition stats,
            HostileTarget first,
            HashSet<int> visited,
            out List<Vector2> points,
            int weaponIndex)
        {
            var buffer = _arcVisited;
            var count = 0;
            foreach (var identity in visited) AddVisited(buffer, ref count, identity);
            var endpoint = FireArcChainBuffered(
                stats,
                first,
                buffer,
                ref count,
                out points,
                weaponIndex);
            for (var index = 0; index < count; index++) visited.Add(buffer[index]);
            return endpoint;
        }

        private HostileTarget FireArcChainBuffered(
            WeaponStatsDefinition stats,
            HostileTarget first,
            int[] visited,
            ref int visitedCount,
            out List<Vector2> points,
            int weaponIndex)
        {
            points = new List<Vector2> { _gameSim.Player.Position };
            var current = first;
            for (var jump = 0; jump <= stats.ChainCount; jump++)
            {
                var key = current.Boss ? -current.Identity : current.Identity;
                if (IsVisited(null, visited, visitedCount, key)) break;
                AddVisited(visited, ref visitedCount, key);
                var previous = points[points.Count - 1];
                points.Add(current.Position);
                var critical = _gameSim.Rng.Next() < _critChance;
                var damage = (float)stats.Damage * _damageMultiplier * (critical ? 2.1f : 1);
                var direction = current.Position - previous;
                if (current.Boss) ApplyBossDamage(current.Index, damage, weaponIndex, critical);
                else ApplyEnemyDamage(current.Index, damage, direction, 40, critical, weaponIndex);

                if (jump == stats.ChainCount) break;
                var next = FindNearestUnvisitedHostile(
                    current.Position,
                    200f * _areaMultiplier,
                    visited,
                    visitedCount);
                // Browser fireArcPath keeps the last valid hostile as the
                // endpoint when no further chain target exists.
                if (!next.Valid) break;
                current = next;
            }

            return current;
        }

        private void UpdateBlades(float dt)
        {
            if (_upgradeProgress == null || _gameSim.Player.Health <= 0 || _gameOver || _revivePending)
            {
                HideBlades(0);
                return;
            }
            if (_upgradeProgress.WeaponRanks.Length <= 3)
            {
                HideBlades(0);
                return;
            }
            var rank = _upgradeProgress.WeaponRanks[3];
            if (rank <= 0)
            {
                HideBlades(0);
                return;
            }
            var weapon = ContentCatalog.Weapons[3];
            var stats = weapon.Ranks[Mathf.Clamp(rank, 1, weapon.Ranks.Length) - 1].Stats;
            _bladeAngle += dt * (float)stats.OrbitSpeed;
            var recoveryScale = WeaponRecoveryScale();
            var evolved = _upgradeProgress.Evolved[3];
            if (evolved)
            {
                if (_hollowBladeActive)
                {
                    _hollowBladeAge += dt;
                    if (_hollowBladeAge >= 1.38f)
                    {
                        _hollowBladeActive = false;
                        _hollowBladeCooldown = 0.42f * recoveryScale;
                    }
                }
                else
                {
                    _hollowBladeCooldown -= dt;
                    if (_hollowBladeCooldown <= 0)
                    {
                        _hollowBladeAngle = DensestEnemyAngle(HollowBladeReach(stats));
                        _hollowBladeAge = 0;
                        _hollowBladeActive = true;
                        _audio?.Play(ProceduralAudio.Cue.BladeLaunch, 0.9f);
                    }
                }
            }
            else
            {
                _hollowBladeActive = false;
                _hollowBladeCooldown = 0;
                Hide(_hollowBladeView);
                Hide(_hollowBladeFarView);
                Hide(_hollowBladeNearView);
            }
            var activeBladeCount = Mathf.Min((int)stats.OrbitCount, _bladeViews.Length);
            HideBlades(activeBladeCount);
            for (var bladeIndex = 0; bladeIndex < activeBladeCount; bladeIndex++)
            {
                var angle = _bladeAngle + bladeIndex / Mathf.Max(1f, stats.OrbitCount) * Mathf.PI * 2;
                var position = _gameSim.Player.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) *
                    (float)stats.OrbitRadius * _areaMultiplier;
                var view = EnsureBladeView(bladeIndex);
                view.transform.position = position;
                // The blade sprite is authored horizontally; rotate it a
                // quarter-turn so its long axis is tangent to the orbit, as in
                // the browser renderer's angle + PI/2 draw path.
                view.transform.rotation = Quaternion.Euler(
                    0f,
                    0f,
                    angle * Mathf.Rad2Deg + 90f);
                view.transform.localScale = Vector3.one * SourceBladeSpriteWorldSize(false);
                view.color = ParseColor(
                    _upgradeProgress.Evolved[3] ? "#5eead4" : weapon.Accent,
                    new Color(0.2f, 0.85f, 0.65f, 1));
                view.enabled = true;

                var enemyCandidateCount = _gameSim.EnemyGrid.QueryNeighborhood(
                    position.x,
                    position.y,
                    1,
                    _gameSim.EnemyGridBulletCandidates);
                for (var candidate = 0; candidate < enemyCandidateCount; candidate++)
                {
                    var enemyIndex = _gameSim.EnemyGridBulletCandidates[candidate];
                    var enemy = _gameSim.Enemies[enemyIndex];
                    if (!IsCurrentGridEnemy(enemyIndex) || enemy.Age < 0.2f || enemy.BladeCooldown > 0) continue;
                    var reach = enemy.Radius + (float)stats.ProjectileRadius;
                    if ((enemy.Position - position).sqrMagnitude > reach * reach) continue;
                    var critical = _gameSim.Rng.Next() < _critChance;
                    enemy.BladeCooldown = (float)stats.HitCooldown * recoveryScale;
                    _gameSim.Enemies[enemyIndex] = enemy;
                    ApplyEnemyDamage(enemyIndex,
                        (float)stats.Damage * _damageMultiplier * (critical ? 2.1f : 1),
                        enemy.Position - position,
                        190,
                        critical,
                        3);
                }
                EnsureBossOrderEntries();
                for (var bossOrder = 0; bossOrder < _gameSim.BossOrderCount; bossOrder++)
                {
                    var bossIndex = _gameSim.BossOrder[bossOrder];
                    var boss = _gameSim.Bosses[bossIndex];
                    if (!boss.Active || boss.State == 4 || boss.BladeCooldown > 0) continue;
                    var reach = boss.Radius + (float)stats.ProjectileRadius;
                    if ((boss.Position - position).sqrMagnitude >= reach * reach) continue;
                    boss.BladeCooldown = (float)stats.HitCooldown * recoveryScale;
                    _gameSim.Bosses[bossIndex] = boss;
                    var critical = _gameSim.Rng.Next() < _critChance;
                    ApplyBossDamage(
                        bossIndex,
                        (float)stats.Damage * _damageMultiplier * (critical ? 2.1f : 1),
                        3,
                        critical);
                }
            }

            if (!evolved || !_hollowBladeActive)
            {
                Hide(_hollowBladeView);
                Hide(_hollowBladeFarView);
                Hide(_hollowBladeNearView);
                return;
            }

            var progress = Mathf.Clamp01(_hollowBladeAge / 1.38f);
            var travel = progress < 0.42f
                ? progress / 0.42f
                : progress > 0.58f ? 1 - (progress - 0.58f) / 0.42f : 1;
            var orbitRadius = (float)stats.OrbitRadius * _areaMultiplier;
            var maximum = HollowBladeReach(stats);
            var hollowDistance = orbitRadius + (maximum - orbitRadius) * Mathf.Clamp01(travel);
            var hollowPosition = _gameSim.Player.Position + new Vector2(
                Mathf.Cos(_hollowBladeAngle), Mathf.Sin(_hollowBladeAngle)) * hollowDistance;
            var hollowView = EnsureHollowBladeView();
            hollowView.transform.position = hollowPosition;
            hollowView.transform.rotation = Quaternion.Euler(
                0f,
                0f,
                _hollowBladeAngle * Mathf.Rad2Deg + 90f);
            hollowView.transform.localScale = Vector3.one * SourceBladeSpriteWorldSize(true);
            hollowView.color = new Color(0.37f, 0.9f, 0.82f, 1f);
            hollowView.enabled = true;

            // Browser drawBlades keeps two dim hollow-blade afterimages behind
            // the travelling blade at offsets 27 and 14 with alpha 0.1/0.2.
            var direction = new Vector2(
                Mathf.Cos(_hollowBladeAngle),
                Mathf.Sin(_hollowBladeAngle));
            var trailScale = Vector3.one * SourceBladeSpriteWorldSize(true);
            var farTrail = EnsureHollowBladeTrailView(false);
            farTrail.transform.position = hollowPosition - direction * 27f;
            farTrail.transform.rotation = hollowView.transform.rotation;
            farTrail.transform.localScale = trailScale;
            farTrail.color = new Color(0.37f, 0.9f, 0.82f, 0.1f);
            farTrail.enabled = true;
            var nearTrail = EnsureHollowBladeTrailView(true);
            nearTrail.transform.position = hollowPosition - direction * 14f;
            nearTrail.transform.rotation = hollowView.transform.rotation;
            nearTrail.transform.localScale = trailScale;
            nearTrail.color = new Color(0.37f, 0.9f, 0.82f, 0.2f);
            nearTrail.enabled = true;
            UpdateHollowBladeDamage(hollowPosition, stats, recoveryScale);
        }

        private float DensestEnemyAngle(float maximumRange)
        {
            var maximumRangeSquared = maximumRange * maximumRange;
            var bestPosition = _gameSim.Player.Position + new Vector2(Mathf.Cos(_bladeAngle), Mathf.Sin(_bladeAngle));
            var bestCount = -1;
            var eligibleCount = 0;
            for (var order = 0; order < _gameSim.EnemyOrderCount; order++)
            {
                var index = _gameSim.EnemyOrder[order];
                var candidate = _gameSim.Enemies[index];
                if (!candidate.Active || candidate.Age < 0.15f || (candidate.Position - _gameSim.Player.Position).sqrMagnitude > maximumRangeSquared) continue;
                eligibleCount++;
                var count = 0;
                var neighborCount = _gameSim.EnemyGrid.QueryNeighborhood(
                    candidate.Position.x,
                    candidate.Position.y,
                    2,
                    _gameSim.EnemyGridBulletCandidates);
                for (var n = 0; n < neighborCount; n++)
                {
                    var otherIndex = _gameSim.EnemyGridBulletCandidates[n];
                    var other = _gameSim.Enemies[otherIndex];
                    if (other.Active && (other.Position - candidate.Position).sqrMagnitude < 150f * 150f) count++;
                }
                if (count > bestCount)
                {
                    bestCount = count;
                    bestPosition = candidate.Position;
                }
                if (eligibleCount >= 48) break;
            }
            if (bestCount >= 0) return Mathf.Atan2(bestPosition.y - _gameSim.Player.Position.y, bestPosition.x - _gameSim.Player.Position.x);
            EnsureBossOrderEntries();
            for (var bossOrder = 0; bossOrder < _gameSim.BossOrderCount; bossOrder++)
            {
                var index = _gameSim.BossOrder[bossOrder];
                var boss = _gameSim.Bosses[index];
                if (boss.Active && (boss.Position - _gameSim.Player.Position).sqrMagnitude <= maximumRangeSquared)
                {
                    return Mathf.Atan2(boss.Position.y - _gameSim.Player.Position.y, boss.Position.x - _gameSim.Player.Position.x);
                }
            }
            return _bladeAngle;
        }

        private void UpdateHollowBladeDamage(Vector2 position, WeaponStatsDefinition stats, float recoveryScale)
        {
            var enemyCandidateCount = _gameSim.EnemyGrid.QueryNeighborhood(
                position.x,
                position.y,
                1,
                _gameSim.EnemyGridBulletCandidates);
            for (var candidate = 0; candidate < enemyCandidateCount; candidate++)
            {
                var enemyIndex = _gameSim.EnemyGridBulletCandidates[candidate];
                var enemy = _gameSim.Enemies[enemyIndex];
                if (!IsCurrentGridEnemy(enemyIndex) || enemy.Age < 0.2f || enemy.HollowCooldown > 0) continue;
                var reach = enemy.Radius + (float)stats.ProjectileRadius * 1.25f;
                if ((enemy.Position - position).sqrMagnitude > reach * reach) continue;
                var critical = _gameSim.Rng.Next() < _critChance;
                enemy.HollowCooldown = (float)stats.HitCooldown * 0.62f * recoveryScale;
                _gameSim.Enemies[enemyIndex] = enemy;
                ApplyEnemyDamage(
                    enemyIndex,
                    (float)stats.Damage * _damageMultiplier * 1.45f * (critical ? 2.1f : 1),
                    enemy.Position - position,
                    280,
                    critical,
                    3);
            }
            EnsureBossOrderEntries();
            for (var bossOrder = 0; bossOrder < _gameSim.BossOrderCount; bossOrder++)
            {
                var bossIndex = _gameSim.BossOrder[bossOrder];
                var boss = _gameSim.Bosses[bossIndex];
                if (!boss.Active || boss.State == 4 || boss.HollowCooldown > 0) continue;
                var reach = boss.Radius + (float)stats.ProjectileRadius * 1.25f;
                if ((boss.Position - position).sqrMagnitude >= reach * reach) continue;
                var critical = _gameSim.Rng.Next() < _critChance;
                boss.HollowCooldown = (float)stats.HitCooldown * 0.62f * recoveryScale;
                _gameSim.Bosses[bossIndex] = boss;
                ApplyBossDamage(
                    bossIndex,
                    (float)stats.Damage * _damageMultiplier * 1.45f * (critical ? 2.1f : 1),
                    3,
                    critical);
            }
        }

        private void UpdateArcEffects(float dt)
        {
            for (var index = 0; index < _arcEffects.Length; index++)
            {
                var effect = _arcEffects[index];
                if (!effect.Active) continue;
                effect.Life -= dt;
                if (effect.Life <= 0)
                {
                    effect.Active = false;
                    Hide(_arcViews[index]);
                    Hide(_arcCoreViews[index]);
                }
                else if (_arcViews[index] != null)
                {
                    var alpha = Mathf.Clamp01(
                        effect.Life / Mathf.Max(0.001f, effect.MaxLife));
                    var color = _arcViews[index].startColor;
                    color.a = alpha;
                    _arcViews[index].startColor = color;
                    _arcViews[index].endColor = color;
                    var core = _arcCoreViews[index];
                    if (core != null)
                    {
                        var coreColor = core.startColor;
                        coreColor.a = alpha;
                        core.startColor = coreColor;
                        core.endColor = coreColor;
                    }
                }
                _arcEffects[index] = effect;
            }
        }

        private void UpdateRailTrails(float dt)
        {
            // Browser railTrails is an append-ordered list updated newest first.
            // Slots are reusable, so array index is not insertion order after
            // the first oldest-trail replacement. Select by sequence instead.
            var processedMask = 0L;
            for (var order = 0; order < _railTrails.Length; order++)
            {
                var index = -1;
                var newestSequence = int.MinValue;
                for (var candidate = 0; candidate < _railTrails.Length; candidate++)
                {
                    var bit = 1L << candidate;
                    var candidateTrail = _railTrails[candidate];
                    if ((processedMask & bit) != 0 || !candidateTrail.Active ||
                        candidateTrail.Sequence <= newestSequence) continue;
                    newestSequence = candidateTrail.Sequence;
                    index = candidate;
                }
                if (index < 0) break;
                processedMask |= 1L << index;
                var trail = _railTrails[index];
                trail.Life -= dt;
                trail.DamageLife -= dt;
                trail.Tick -= dt;
                if (trail.DamageLife > 0 && trail.Tick <= 0)
                {
                    trail.Tick = 0.22f;
                    var segmentDirection = trail.End - trail.Start;
                    var enemySnapshot = CaptureEnemyEffectSnapshot(out var enemySnapshotCount);
                    try
                    {
                        for (var target = 0; target < enemySnapshotCount; target++)
                        {
                            var snapshot = enemySnapshot[target];
                            if (!IsLiveEnemyEffectTarget(snapshot)) continue;
                            var enemy = snapshot.State;
                            if (enemy.Age < 0.15f || DistanceToSegment(enemy.Position, trail.Start, trail.End) > enemy.Radius + 13) continue;
                            ApplyEnemyDamage(snapshot.Slot, trail.Damage, segmentDirection, 42, false, trail.WeaponIndex);
                        }
                    }
                    finally
                    {
                        ReleaseEnemyEffectSnapshot(enemySnapshot);
                    }
                    EnsureBossOrderEntries();
                    for (var bossOrder = 0; bossOrder < _gameSim.BossOrderCount; bossOrder++)
                    {
                        var bossIndex = _gameSim.BossOrder[bossOrder];
                        var boss = _gameSim.Bosses[bossIndex];
                        if (!boss.Active || DistanceToSegment(boss.Position, trail.Start, trail.End) >= boss.Radius + 13) continue;
                        ApplyBossDamage(bossIndex, trail.Damage, trail.WeaponIndex);
                    }
                }

                if (trail.Life <= 0)
                {
                    trail.Active = false;
                    Hide(_railTrailViews[index]);
                }
                else
                {
                    RenderRailTrail(index, trail);
                }
                _railTrails[index] = trail;
            }
        }

        private bool SpawnEnemy(string id)
        {
            return SpawnEnemy(id, null);
        }

        private static float EnemyHealthScaleAt(float elapsedSeconds, int bossCycle, float healthMultiplier)
        {
            var time = Mathf.Max(0, elapsedSeconds);
            var cycle = Mathf.Max(0, bossCycle);
            return (1f + time / 105f + Mathf.Pow(time / 420f, 1.45f) + cycle * 0.18f) *
                Mathf.Max(0, healthMultiplier);
        }

        private static float EnemySpeedScaleAt(float elapsedSeconds, int bossCycle)
        {
            var time = Mathf.Max(0, elapsedSeconds);
            var cycle = Mathf.Max(0, bossCycle);
            return Mathf.Min(1.52f, 1f + time / 1600f + cycle * 0.025f);
        }

        private static float EnemyDamageScaleAt(float elapsedSeconds, int bossCycle)
        {
            var time = Mathf.Max(0, elapsedSeconds);
            var cycle = Mathf.Max(0, bossCycle);
            return Mathf.Min(4.5f, 1f + time / 720f + cycle * 0.08f);
        }

        internal static float HarvesterDamageGainScaleAt(float elapsedSeconds, int bossCycle)
        {
            return EnemyDamageScaleAt(elapsedSeconds, bossCycle);
        }

        private static float ExplosiveMeteorShardDamageAt(float elapsedSeconds, int bossCycle)
        {
            return (float)MeteorRules.ExplosiveShardDamage(
                elapsedSeconds,
                EnemyDamageScaleAt(elapsedSeconds, bossCycle));
        }

        private static int ExplosiveMeteorPlayerDamageAt(
            float elapsedSeconds,
            int bossCycle,
            float enemyDamage)
        {
            return MeteorRules.ExplosivePlayerDamage(
                enemyDamage,
                EnemyDamageScaleAt(elapsedSeconds, bossCycle));
        }

        private bool SpawnEnemy(
            string id,
            Vector2? forcedPosition,
            EliteVariantId? eliteKind,
            bool carrierDrone,
            bool splitterFragment)
        {
            return SpawnEnemy(
                id,
                forcedPosition,
                eliteKind,
                carrierDrone,
                splitterFragment,
                0);
        }

        private bool SpawnEnemy(
            string id,
            Vector2? forcedPosition,
            EliteVariantId? eliteKind,
            bool carrierDrone,
            bool splitterFragment,
            int summonedByBossTelemetryId)
        {
            return SpawnEnemy(
                id,
                forcedPosition,
                eliteKind,
                carrierDrone,
                splitterFragment,
                summonedByBossTelemetryId,
                1f,
                null);
        }

        private bool SpawnEnemy(
            string id,
            Vector2? forcedPosition,
            EliteVariantId? eliteKind = null,
            bool carrierDrone = false,
            bool splitterFragment = false,
            int summonedByBossTelemetryId = 0,
            float healthMultiplier = 1f,
            EnemyRoster? forcedRoster = null)
        {
            if (id == "harvester" && ActiveEnemyTypeCount(id) >= 3) return false;
            var slot = FindInactive(_gameSim.Enemies);
            if (slot < 0) return false;
            var standardElite = id == ContentCatalog.Elite.Id;
            var definition = standardElite ? null : FindEnemy(id);
            if (!standardElite && definition == null) return false;
            var elite = standardElite || eliteKind.HasValue;
            DiscoverBestiary(id);
            if (elite) DiscoverBestiary("elite");
            var variantDefinition = eliteKind.HasValue ? EliteRules.EliteVariantDef(eliteKind.Value) : null;
            var variantStats = eliteKind.HasValue
                ? EliteRules.EliteVariantStatsFor(eliteKind.Value)
                : default(EliteVariantStats);
            var enemyId = _nextEnemyId++;
            // Browser spawnEnemy only selects time-based Roster II for ordinary
            // world spawns. Boss summons stay in Roster I unless a pressure
            // tier explicitly supplies forcedRoster.
            var roster = forcedRoster ?? (
                elite || carrierDrone || splitterFragment || summonedByBossTelemetryId != 0
                    ? EnemyRoster.One
                    : EnemyRosterRules.EnemyRosterForSpawn(
                        id,
                        _time,
                        EnemyRosterRules.RosterSpawnRoll(_runSeed, enemyId)));
            var angle = (float)(_gameSim.Rng.Next() * Math.PI * 2);
            var viewportHalf = GameplayViewportHalfExtent();
            var distance = Mathf.Sqrt(
                    Mathf.Pow(viewportHalf.x * 2f, 2) + Mathf.Pow(viewportHalf.y * 2f, 2)) * 0.5f +
                70f + (float)_gameSim.Rng.Next() * 130f;
            var position = forcedPosition ?? _gameSim.Player.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            // Browser carrier drones pass hpScale=0.55 through spawnEnemy;
            // keep that source-specific health reduction coupled to the flag
            // so every carrier-drone spawn path uses the same rule.
            var healthScale = EnemyHealthScaleAt(
                _time,
                _bossCycle,
                healthMultiplier * (carrierDrone ? 0.55f : 1f));
            var speedScale = EnemySpeedScaleAt(_time, _bossCycle);
            var damageScale = EnemyDamageScaleAt(_time, _bossCycle);
            var rosterHealth = roster == EnemyRoster.Two ? EnemyRosterRules.RosterTwoHealthMultiplier : 1;
            var rosterRadius = roster == EnemyRoster.Two ? EnemyRosterRules.RosterTwoRadiusMultiplier : 1;
            var rosterSpeed = roster == EnemyRoster.Two ? EnemyRosterRules.RosterTwoSpeedMultiplier : 1;
            var rosterDamage = roster == EnemyRoster.Two ? EnemyRosterRules.RosterTwoDamageMultiplier : 1;
            var baseHealth = eliteKind.HasValue
                ? variantStats.Health
                : standardElite ? ContentCatalog.Elite.Health : definition.Health;
            var baseSpeed = eliteKind.HasValue
                ? variantStats.Speed
                : (standardElite ? ContentCatalog.Elite.Speed : definition.Speed) *
                    (splitterFragment ? 1.55 : carrierDrone ? 1.18 : 1);
            var baseRadius = eliteKind.HasValue
                ? variantStats.Radius
                : (splitterFragment ? 8 : carrierDrone ? 7 : standardElite ? ContentCatalog.Elite.Radius : definition.Radius);
            var baseDamage = eliteKind.HasValue
                ? variantStats.ContactDamage
                : (standardElite ? ContentCatalog.Elite.ContactDamage : definition.ContactDamage) *
                    (splitterFragment ? 0.55 : carrierDrone ? 0.7 : 1);
            var baseXp = splitterFragment || carrierDrone
                ? 1
                : standardElite ? ContentCatalog.Elite.Xp : definition.Xp;
            var shield = !elite && id == "guard"
                ? (float)((definition.Shield ?? 0) * healthScale * rosterHealth)
                : 0;
            var enemy = new EnemyState
            {
                Active = true,
                Id = id,
                Position = position,
                MaxHealth = (float)baseHealth * healthScale * (float)rosterHealth,
                Radius = (float)baseRadius * (float)rosterRadius,
                Speed = (float)baseSpeed * speedScale * (float)rosterSpeed * (0.92f + (float)_gameSim.Rng.Next() * 0.16f),
                Damage = (float)baseDamage * damageScale * (float)rosterDamage,
                Xp = Mathf.Max(1, SourceRound((float)(baseXp * (roster == EnemyRoster.Two ? EnemyRosterRules.RosterTwoXpMultiplier : 1)))),
                Age = 0,
                Shield = shield,
                MaxShield = shield,
                Roster = roster,
                Elite = elite,
                EliteKind = eliteKind,
                CarrierDrone = carrierDrone,
                SplitterFragment = splitterFragment,
                SummonedByBossTelemetryId = summonedByBossTelemetryId,
                SummonedByCarrierSpawnId = 0,
                SpawnId = enemyId,
                Facing = (_gameSim.Player.Position - position).sqrMagnitude > 0.001f
                    ? (_gameSim.Player.Position - position).normalized
                    : Vector2.right,
                State = 0,
                StateTimer = standardElite
                    ? 2.2f
                    : eliteKind.HasValue ? (float)variantStats.TelegraphSeconds : 0,
                AttackCooldown = !elite &&
                    (id == "gunner" ||
                        id == "twinGunner" ||
                        id == "technician" ||
                        id == "mortar" ||
                        id == "carrier" ||
                        (roster == EnemyRoster.Two && id == "guard"))
                    ? 1.2f + (float)_gameSim.Rng.Next()
                    : 0,
                Rotation = (float)(_gameSim.Rng.Next() * Math.PI * 2),
                Spin = !elite && id == "exploder"
                    ? (_gameSim.Rng.Next() < 0.5 ? -0.48f : 0.48f)
                    : ((float)_gameSim.Rng.Next() - 0.5f) * 2.2f,
                Seed = (float)_gameSim.Rng.Next() * 100,
                Volley = 0,
                View = slot,
            };
            if (eliteKind.HasValue && eliteKind.Value == EliteVariantId.Exploder)
                enemy.Spin = _gameSim.Rng.Next() < 0.5 ? -0.42f : 0.42f;
            enemy.Health = enemy.MaxHealth;
            _gameSim.Enemies[slot] = enemy;
            AppendEnemyOrder(slot);
            if (elite)
                _telemetry.RecordEliteSpawn(
                    ArenaIdName(_arenaId),
                    standardElite ? "standard" : variantDefinition.BaseId);
            else if (roster == EnemyRoster.Two)
                _telemetry.RecordRosterTwoSpawn(ArenaIdName(_arenaId));
            var view = EnsureEnemyView(slot);
            var accent = eliteKind.HasValue
                ? ParseColor(variantDefinition.Accent, Color.magenta)
                : standardElite
                    ? ParseColor(ContentCatalog.Elite.Color, Color.yellow)
                : ParseColor(definition.Color, Color.magenta);
            view.sprite = ProceduralSpriteFactory.Enemy(
                SourceEnemySpriteId(id, elite, variantDefinition?.BaseId),
                CachedEnemySpriteAccent(enemy),
                false);
            view.color = Color.white;
            view.enabled = true;
            return true;
        }

        private void SpawnBoss(string id, double healthScale, double damageScale)
        {
            var encounterCycle = Mathf.Max(0, (_bossSequence - 1) / Mathf.Max(1, ContentCatalog.Bosses.Length));
            SpawnBoss(id, healthScale, damageScale, encounterCycle);
        }

        private void SpawnBoss(string id, double healthScale, double damageScale, int encounterCycle)
        {
            // The browser keeps defeated bosses in its array until the 1.4 s
            // fade is over. Do not overwrite that logical entry early just
            // because its fixed Unity slot is no longer damageable.
            var slot = FindInactiveBossSlot();
            if (slot < 0) return;
            var definition = FindBoss(id);
            if (definition == null) return;
            NotifyObjectiveBossSpawned(id);
            _bossCycle = Mathf.Max(_bossCycle, Mathf.Max(0, encounterCycle));
            var activeBeforeSpawn = ActiveBosses();
            DiscoverBestiary(id);
            var angle = (float)(_gameSim.Rng.Next() * Math.PI * 2) +
                activeBeforeSpawn * (Mathf.PI * 2f / Mathf.Max(2, activeBeforeSpawn + 1));
            var viewportHalf = GameplayViewportHalfExtent();
            var distance = Mathf.Min(
                480f,
                Mathf.Sqrt(Mathf.Pow(viewportHalf.x * 2f, 2) + Mathf.Pow(viewportHalf.y * 2f, 2)) * 0.5f + 120f);
            var position = _gameSim.Player.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            var boss = new BossState
            {
                Active = true,
                Id = id,
                Position = position,
                MaxHealth = (float)(definition.Health * healthScale * DirectorRules.BossHealthScaleAt(_time, id)),
                Radius = (float)definition.Radius,
                Speed = (float)definition.Speed,
                Damage = (float)(definition.ContactDamage * damageScale),
                DamageScale = (float)damageScale,
                AttackCooldown = 1.2f + activeBeforeSpawn * 0.8f,
                State = 4,
                StateTimer = 1.6f,
                DeathTimer = 0,
                AttackIndex = 0,
                Reinforced = false,
                TierPressureTriggered = false,
                PressureTier = DirectorRules.BossPressureTierAt(_time),
                EncounterIndex = Mathf.Max(0, _bossSequence - 1),
                TargetPosition = position,
                TelemetryInstanceId = _nextBossTelemetryId++,
                View = slot,
            };
            boss.Health = boss.MaxHealth;
            _gameSim.Bosses[slot] = boss;
            AppendBossOrder(slot);
            _telemetry.RecordBossSpawn(
                id,
                boss.TelemetryInstanceId,
                Mathf.Max(0, _bossSequence - 1),
                (float)_time,
                boss.MaxHealth,
                Mathf.Max(0, ActiveBosses() - 1));
            var view = EnsureBossView(slot);
            view.sprite = ProceduralSpriteFactory.Boss(
                id,
                ParseColor(definition.Color, Color.magenta),
                false);
            view.color = Color.white;
            view.enabled = true;
            TriggerFreeze(0.06f);
            var bossAccent = BossAccent(boss);
            bossAccent.a = 0.78f;
            SpawnRingWave(position, 30f, 420f, 0.6f, bossAccent);
            BurstFx(position, BossParticleColor(boss.Id), 18, 300, 0.6f, 0.9f);
        }

        private int FindInactiveBossSlot()
        {
            for (var index = 0; index < _gameSim.Bosses.Length; index++)
            {
                if (!_gameSim.Bosses[index].Active && _gameSim.Bosses[index].DeathTimer <= 0) return index;
            }
            return -1;
        }

        private void SpawnHostileShot(
            Vector2 position,
            Vector2 direction,
            float damage,
            float speed,
            float curvature,
            bool meteorOwned = false,
            int visualVariant = -1)
        {
            // Insertion (curved-cap check, slot find, state write, order
            // append) is GameSim's now; check order matches the browser.
            var slot = _gameSim.TryInsertHostileShot(
                position, direction, damage, speed, curvature, meteorOwned, visualVariant);
            if (slot < 0) return;
            var curved = curvature != 0f;
            var view = EnsureHostileShotView(slot);
            view.sprite = curved
                ? ProceduralSpriteFactory.Projectile("curved")
                : meteorOwned
                    ? ProceduralSpriteFactory.MeteorCore()
                    : ProceduralSpriteFactory.ProjectileFrame(
                        "gunner",
                        SourceProjectileFrameIndex(direction));
            // Never assign null here: see ResolveDefaultSpriteMaterial. Guarded
            // so a failed shader lookup leaves the existing material in place
            // rather than blanking the renderer.
            var shotMaterial = meteorOwned
                ? ResolveAdditiveSpriteMaterial()
                : ResolveDefaultSpriteMaterial();
            if (shotMaterial != null) view.sharedMaterial = shotMaterial;
            view.transform.rotation = Quaternion.identity;
            // Browser meteor-owned hostile shots draw the hot core at a fixed
            // 18 px square with 0.82 alpha; ordinary and curved shots keep
            // their authored sprite alpha untouched.
            view.color = meteorOwned ? new Color(1f, 1f, 1f, 0.82f) : Color.white;
            view.enabled = true;
        }

        private void UpdateHostileShots(float dt)
        {
            // The runtime keeps DamagePlayer and telemetry; GameSim drives the
            // loop and calls back at the exact points the browser resolves an
            // impact, so iframes set by one hit still gate later shots in the
            // same pass. Delegates are cached - no per-step allocation.
            if (_hostileShotVulnerableQuery == null)
            {
                _hostileShotVulnerableQuery = () =>
                    !_gameOver && !_revivePending &&
                    _gameSim.Player.DyingTimer <= 0 && _gameSim.Player.Iframes <= 0;
                _hostileShotImpactHandler = (index, impactDirection) =>
                {
                    DamagePlayer(_gameSim.HostileShots[index].Damage, impactDirection);
                    if (_gameSim.HostileShots[index].MeteorOwned)
                        _telemetry.RecordMeteorPlayerHit(ArenaIdName(_arenaId));
                };
            }
            _gameSim.PlayerVulnerableQuery = _hostileShotVulnerableQuery;
            _gameSim.HostileShotImpact = _hostileShotImpactHandler;

            _gameSim.AdvanceHostileShots(
                dt,
                AttackPlayerRadius,
                _hostileShotExpiredSlots,
                out var expiredCount);
            for (var slotIndex = 0; slotIndex < expiredCount; slotIndex++)
                Hide(_hostileShotViews[_hostileShotExpiredSlots[slotIndex]]);
        }

        private void SpawnPickup(Vector2 position, float value)
        {
            var drops = PickupRules.XpDropValues(
                Mathf.Max(0, Mathf.FloorToInt(value)),
                () => _gameSim.Rng.Next());
            var released = 0;
            for (var index = 0; index < drops.Length; index++)
            {
                var drop = drops[index];
                released += drop;
                var slot = FindInactive(_gameSim.Pickups);
                if (slot < 0)
                {
                    var target = FindXpOverflowTarget(position);
                    if (target >= 0)
                    {
                        var existing = _gameSim.Pickups[target];
                        existing.Value += drop;
                        if ((existing.Position - position).sqrMagnitude < 180f * 180f)
                        {
                            var mergeAngle = (float)(_gameSim.Rng.Next() * Math.PI * 2);
                            var mergeSpeed = 40f + (float)_gameSim.Rng.Next() * 60f;
                            existing.Velocity = new Vector2(Mathf.Cos(mergeAngle), Mathf.Sin(mergeAngle)) * mergeSpeed;
                        }
                        existing.Age = 0;
                        existing.Pull = false;
                        existing.Speed = 0;
                        _gameSim.Pickups[target] = existing;
                        RefreshPickupView(target);
                    }
                    // The extra pickup slot preserves one XP gem when a full
                    // set consists entirely of special drops. If that slot is
                    // also occupied, the browser has no reclaimable target.
                    continue;
                }

                var angle = (float)(_gameSim.Rng.Next() * Math.PI * 2);
                var speed = 40f + (float)_gameSim.Rng.Next() * 85f;
                _gameSim.Pickups[slot] = new PickupState
                {
                    Active = true,
                    Position = position,
                    Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed,
                    Value = drop,
                    Age = (float)_gameSim.Rng.Next() * 5f,
                    Speed = 0,
                    Kind = PickupKind.Xp,
                    Pull = false,
                    View = slot,
                };
                AppendPickupOrder(slot);
                RefreshPickupView(slot);
            }
            if (released > 0) _telemetry.RecordXpReleased(released);
        }

        private bool SpawnSpecialPickup(Vector2 position, float value, PickupKind kind)
        {
            // MaxPickupSlots includes one overflow slot reserved for XP. Rare
            // and other special drops must never consume that slot, otherwise a
            // full special-pickup set silently prevents later XP from spawning.
            var slot = FindInactive(_gameSim.Pickups, MaxPickups);
            if (slot < 0) return false;
            _gameSim.Pickups[slot] = new PickupState
            {
                Active = true,
                Position = position,
                Velocity = new Vector2(
                    ((float)_gameSim.Rng.Next() - 0.5f) * 90f,
                    ((float)_gameSim.Rng.Next() - 0.5f) * 90f),
                Value = value,
                Age = 0,
                Speed = 0,
                Kind = kind,
                Pull = false,
                View = slot,
            };
            AppendPickupOrder(slot);
            RefreshPickupView(slot);
            return true;
        }

        private void SpawnRarePickup(Vector2 position)
        {
            var roll = _gameSim.Rng.Next();
            // TRACK SHIFT rides the rare pool at a modest band; the music
            // switch is a delight, not a build-around.
            var kind = roll < 0.08
                ? PickupKind.TrackShift
                : roll < 0.36
                    ? PickupKind.Magnet
                    : roll < 0.67
                        ? PickupKind.Repair
                        : roll < 0.87
                            ? PickupKind.Bomb
                            : PickupKind.Overdrive;
            SpawnSpecialPickup(position, 1, kind);
        }

        private void RefreshPickupView(int slot)
        {
            var pickup = _gameSim.Pickups[slot];
            var view = EnsurePickupView(slot);
            view.sprite = pickup.Kind == PickupKind.Xp
                ? ProceduralSpriteFactory.Gem(XpPickupTier(pickup.Value))
                : ProceduralSpriteFactory.Pickup(PickupKindName(pickup.Kind));
            view.color = Color.white;
            view.enabled = true;
        }

        private static string PickupKindName(PickupKind kind)
        {
            switch (kind)
            {
                case PickupKind.Xp: return "xp";
                case PickupKind.Part: return "part";
                case PickupKind.Magnet: return "magnet";
                case PickupKind.Repair: return "repair";
                case PickupKind.Bomb: return "bomb";
                case PickupKind.Overdrive: return "overdrive";
                case PickupKind.TrackShift: return "trackshift";
                default: return "unknown";
            }
        }

        private static int XpPickupTier(float value)
        {
            return value >= 10 ? 2 : value >= 4 ? 1 : 0;
        }

        private static float SourcePickupPulseScale(string kind, float age, bool pickupPulse)
        {
            if (!pickupPulse) return kind == "xp" ? 1f : 0.88f;
            var pulse = Mathf.Sin(age * 5f);
            return kind == "xp" ? 1f + pulse * 0.1f : 0.88f + pulse * 0.06f;
        }

        private static float SourcePickupRotationRadians(string kind, float age)
        {
            return kind == "part" || kind == "bomb" ? age * 0.7f : 0f;
        }

        private static float SourceXpPickupTierScale(int tier)
        {
            return Mathf.Clamp(tier, 0, 2) == 0
                ? 1f
                : tier == 1 ? 60f / 52f : 72f / 52f;
        }

        private static float SourceXpPickupFrameSize(int tier)
        {
            return Mathf.Clamp(tier, 0, 2) == 0
                ? 52f
                : tier == 1 ? 60f : 72f;
        }

        private static float SourceSpecialPickupFrameSize()
        {
            return 42f;
        }

        internal static float SourceEnemyRotationFromDirection(Vector2 direction)
        {
            return direction.sqrMagnitude > 0f
                ? Mathf.Atan2(direction.y, direction.x)
                : 0f;
        }

        private static Vector2 SourcePickupPullVelocity(Vector2 delta, float speed)
        {
            // Browser updatePickups divides by Math.sqrt(distanceSq) || 1:
            // tiny non-zero offsets still become a full-speed pull vector.
            return delta / SourceLengthOrOne(delta) * speed;
        }

        private static float SourceEnemyRotationAdvance(float rotation, float spin, float dt)
        {
            return rotation + spin * dt;
        }

        private static float SourceEliteRingSize(float ambientClock)
        {
            return 112f * (1f + Mathf.Sin(ambientClock * 4f) * 0.1f);
        }

        private static float SourceEliteRingAlpha(bool eliteVariant)
        {
            return eliteVariant ? 0.55f : 0.4f;
        }

        private static float SourceBossBodyRotationRadians(float ambientClock)
        {
            return Mathf.Sin(ambientClock * 0.8f) * 0.05f;
        }

        private static float SourceEnemyIntroScale(float age)
        {
            if (age >= 0.35f) return 1f;
            var progress = Mathf.Clamp01(age / 0.35f);
            return BackOut(progress);
        }

        private void ApplyEnemyDamage(int index, float damage)
        {
            ApplyEnemyDamage(index, damage, Vector2.zero, 0, false, -1);
        }

        private void DamagePlayer(float damage, Vector2 sourceDirection)
        {
            if (_gameOver || _revivePending || _gameSim.Player.DyingTimer > 0 || _gameSim.Player.Iframes > 0) return;
            var appliedDamage = Mathf.Max(1, damage);
            // Commit the browser's player state before any hurt presentation:
            // later effects in the same simulation step observe the reduced
            // health, pressure timer, and invulnerability window immediately.
            _damageTaken += appliedDamage;
            _gameSim.Player.Health -= appliedDamage;
            _music?.NotifyPlayerDamage(
                _gameSim.Player.MaxHealth > 0f ? appliedDamage / _gameSim.Player.MaxHealth : 1f,
                _gameSim.Player.Health <= 0f);
            _pressureReliefTimer = Mathf.Max(
                _pressureReliefTimer,
                _gameSim.Player.Health < _gameSim.Player.MaxHealth * 0.35f ? 5f : 2.5f);
            _gameSim.Player.Iframes = 0.65f;
            _redFlash = 1f;
            TriggerFreeze(0.04f);
            _audio?.Play(ProceduralAudio.Cue.Hurt, 0.82f);
            AddCameraShake(0.5f);
            BurstFx(_gameSim.Player.Position, SourceDotColor("red"), 9, 250, 0.48f, 0.85f);
            SpawnDamageIndicator(sourceDirection);
            var impactDirection = SourceNormalizedDirection(sourceDirection);
            if (impactDirection != Vector2.zero)
                _gameSim.Player.Velocity += impactDirection * 250f;
            if (SupportRank("adrenal") > 0 && _gameSim.Player.Health > 0)
            {
                _adrenalTimer = 5;
                _amberFlash = Mathf.Max(_amberFlash, 0.15f);
                SpawnFloater(
                    _gameSim.Player.Position + Vector2.up * 22f,
                    "ADRENAL SURGE",
                    new Color(0.984f, 0.576f, 0.188f, 1f),
                    12);
            }
            if (_gameSim.Player.Health <= 0)
            {
                _gameSim.Player.Health = 0;
                _gameSim.Player.DyingTimer = 1.1f;
                _gameSim.Player.Iframes = 10f;
                _targetTimeScale = 0.22f;
                TriggerFreeze(0.08f);
                AddCameraShake(1f);
                // Browser authority gives a revivable defeat the boss cue, and
                // reserves the full game-over sting for a terminal defeat.
                _audio?.Play(DefeatCueFor(_revivesRemaining));
                BurstFx(_gameSim.Player.Position, SourceDotColor("cyan"), 34, 390, 0.85f, 1.1f);
                BurstFx(_gameSim.Player.Position, SourceDotColor("white"), 18, 270, 0.65f, 0.85f);
                SpawnRingWave(
                    _gameSim.Player.Position,
                    20f,
                    320f,
                    0.68f,
                    new Color(0.133f, 0.827f, 0.933f, 1f));
            }
        }

        private void ApplyEnemyDamage(
            int index,
            float damage,
            Vector2 direction,
            float knockback,
            bool critical,
            int weaponIndex = -1)
        {
            var enemy = _gameSim.Enemies[index];
            if (!enemy.Active) return;
            damage *= PlayerDamageMultiplier();
            var appliedDamage = Mathf.Max(0, damage);
            if (enemy.Id == "bulwark" && direction.sqrMagnitude > 0.001f)
            {
                var sourceDirection = -direction.normalized;
                if (Vector2.Dot(enemy.Facing, sourceDirection) > Mathf.Cos(1.08f))
                {
                    appliedDamage *= 0.25f;
                    if (enemy.BlockCooldown <= 0)
                    {
                        enemy.BlockCooldown = 0.16f;
                        SpawnFloater(
                            enemy.Position + Vector2.up * enemy.Radius,
                            "BLOCK",
                            new Color(0.49f, 0.82f, 0.99f, 1f),
                            10);
                        BurstFx(enemy.Position, SourceDotColor("cyan"), 3, 110, 0.22f, 0.5f);
                    }
                }
            }
            var remaining = appliedDamage;
            var shieldBroken = false;
            if (enemy.Shield > 0)
            {
                var absorbed = Mathf.Min(enemy.Shield, remaining);
                enemy.Shield -= absorbed;
                remaining -= absorbed;
                _damageDealt += absorbed;
                TrackWeaponDamage(weaponIndex, absorbed);
                shieldBroken = absorbed > 0 && enemy.Shield <= 0;
            }

            if (shieldBroken)
            {
                BurstFx(enemy.Position, SourceDotColor("cyan"), 12, 220, 0.45f, 0.8f);
                SpawnRingWave(enemy.Position, enemy.Radius, 150f, 0.35f,
                    new Color(0.35f, 0.85f, 1f, 0.72f));
                _audio?.Play(ProceduralAudio.Cue.ShieldBreak, 0.9f);
            }

            var appliedHealth = Mathf.Min(Mathf.Max(0, remaining), enemy.Health);
            enemy.Health -= remaining;
            enemy.HitTimer = 0.09f;
            _damageDealt += appliedHealth;
            TrackWeaponDamage(weaponIndex, appliedHealth);
            if (knockback > 0)
            {
                var resistance = enemy.Id == "brute" ? 0.28f : enemy.Elite ? 0.1f : 1f;
                enemy.Knockback += direction / SourceLengthOrOne(direction) * knockback * resistance;
            }
            _gameSim.Enemies[index] = enemy;
            if (remaining > 0)
            {
                // Browser damageEnemy creates the damage number before the
                // hit burst, so the shared FX RNG stays source-ordered.
                SpawnDamageFloater(
                    index + 1,
                    enemy.Position + Vector2.up * enemy.Radius,
                    remaining,
                    critical);
                BurstFx(enemy.Position, EnemyParticleColor(enemy), critical ? 5 : 3, 190, 0.35f, 0.7f);
                _audio?.Play(ProceduralAudio.Cue.Hit, critical ? 1f : 0.9f);
                if (critical)
                {
                    _audio?.Play(ProceduralAudio.Cue.Crit, 1f);
                    TriggerFreeze(0.018f);
                }
            }
            if (critical && SupportRank("overload") > 0 && remaining > 0)
            {
                DamageOverloadArea(enemy.Position, 76f * _areaMultiplier,
                    remaining * 0.25f * SupportRank("overload"),
                    EnemyIdentity(enemy, index), weaponIndex);
                BurstFx(enemy.Position, SourceDotColor("cyan"),
                    4, 160, 0.3f, 0.6f);
            }
            if (enemy.Health <= 0) KillEnemy(index);
        }

        private void ApplyBossDamage(int index, float damage, int weaponIndex = -1, bool critical = false)
        {
            if (index < 0 || index >= _gameSim.Bosses.Length) return;
            var boss = _gameSim.Bosses[index];
            if (!boss.Active || boss.State == 4) return;
            damage *= PlayerDamageMultiplier();
            if (IsMatriarchShielded(boss))
            {
                if (boss.ShieldHitTimer <= 0)
                {
                    boss.ShieldHitTimer = 0.24f;
                    SpawnFloater(
                        boss.Position + Vector2.up * boss.Radius,
                        "SHIELDED",
                        new Color(0.431f, 0.906f, 0.718f, 1f),
                        12);
                    BurstFx(
                        boss.Position,
                        SourceDotColor("emerald"),
                        4,
                        150,
                        0.3f,
                        0.65f);
                }
                _gameSim.Bosses[index] = boss;
                return;
            }
            var appliedDamage = Mathf.Min(Mathf.Max(0, damage), boss.Health);
            _damageDealt += appliedDamage;
            TrackWeaponDamage(weaponIndex, appliedDamage);
            boss.Health -= appliedDamage;
            boss.HitTimer = 0.08f;
            _gameSim.Bosses[index] = boss;
            if (appliedDamage > 0)
            {
                // Browser damageBoss creates the damage number before the
                // hit burst, preserving the shared FX RNG sequence.
                SpawnDamageFloater(
                    MaxEnemies + index + 1,
                    boss.Position + Vector2.up * boss.Radius,
                    appliedDamage,
                    critical);
                BurstFx(boss.Position, BossParticleColor(boss.Id), critical ? 5 : 3, 180, 0.32f, 0.7f);
                _audio?.Play(ProceduralAudio.Cue.Hit, critical ? 1f : 0.9f);
                if (critical) _audio?.Play(ProceduralAudio.Cue.Crit, 1f);
            }
            if (boss.Health <= 0) KillBoss(index);
        }

        private void DamageOverloadArea(
            Vector2 origin,
            float radius,
            float damage,
            int excludedEnemyIdentity,
            int weaponIndex)
        {
            // Browser overload uses its own copied enemy/boss loops. It does
            // not call damageArea: meteors and the orange blast presentation
            // must stay out of this critical-hit-only effect.
            var enemySnapshot = CaptureEnemyEffectSnapshot(out var enemySnapshotCount);
            try
            {
                for (var target = 0; target < enemySnapshotCount; target++)
                {
                    var snapshot = enemySnapshot[target];
                    if (EnemyIdentity(snapshot.State, snapshot.Slot) == excludedEnemyIdentity ||
                        !IsLiveEnemyEffectTarget(snapshot)) continue;
                    var enemy = snapshot.State;
                    var delta = enemy.Position - origin;
                    var reach = radius + enemy.Radius;
                    if (delta.sqrMagnitude >= reach * reach) continue;
                    ApplyEnemyDamage(snapshot.Slot, damage, delta, 60, false, weaponIndex);
                }
            }
            finally
            {
                ReleaseEnemyEffectSnapshot(enemySnapshot);
            }

            EnsureBossOrderEntries();
            for (var bossOrder = 0; bossOrder < _gameSim.BossOrderCount; bossOrder++)
            {
                var index = _gameSim.BossOrder[bossOrder];
                var boss = _gameSim.Bosses[index];
                if (!boss.Active || boss.State == 4) continue;
                var delta = boss.Position - origin;
                if (delta.sqrMagnitude >= (radius + boss.Radius) * (radius + boss.Radius)) continue;
                ApplyBossDamage(index, damage, weaponIndex, false);
            }
        }

        private void DamageArea(
            Vector2 origin,
            float radius,
            float damage,
            int excludedEnemyIdentity,
            int weaponIndex = -1)
        {
            DamageMeteorsInRadius(origin, radius, damage);
            var cellSpan = Mathf.CeilToInt(radius / CollisionGrid.CellSize) + 1;
            var candidateCount = _gameSim.EnemyGrid.QueryNeighborhood(
                origin.x,
                origin.y,
                cellSpan,
                _gameSim.EnemyGridAreaCandidates);
            // The browser grid stores enemy object references. Capture the
            // queried identities before applying damage so recursive effects
            // cannot read a newly spawned enemy from a recycled pooled slot.
            var enemySnapshot = ArrayPool<EnemyEffectTarget>.Shared.Rent(Math.Max(1, candidateCount));
            var enemySnapshotCount = 0;
            try
            {
                for (var candidate = 0; candidate < candidateCount; candidate++)
                {
                    var index = _gameSim.EnemyGridAreaCandidates[candidate];
                    var enemy = _gameSim.Enemies[index];
                    if (!IsCurrentGridEnemy(index) ||
                        EnemyIdentity(enemy, index) == excludedEnemyIdentity) continue;
                    enemySnapshot[enemySnapshotCount++] = new EnemyEffectTarget
                    {
                        Slot = index,
                        State = enemy,
                    };
                }
                for (var target = 0; target < enemySnapshotCount; target++)
                {
                    var snapshot = enemySnapshot[target];
                    if (!IsLiveEnemyEffectTarget(snapshot)) continue;
                    var enemy = snapshot.State;
                    var reach = radius + enemy.Radius;
                    if ((enemy.Position - origin).sqrMagnitude >= reach * reach) continue;
                    ApplyEnemyDamage(snapshot.Slot, damage, enemy.Position - origin, 90, false, weaponIndex);
                }
            }
            finally
            {
                ReleaseEnemyEffectSnapshot(enemySnapshot);
            }
            EnsureBossOrderEntries();
            for (var bossOrder = 0; bossOrder < _gameSim.BossOrderCount; bossOrder++)
            {
                var index = _gameSim.BossOrder[bossOrder];
                var boss = _gameSim.Bosses[index];
                if (!boss.Active || boss.State == 4) continue;
                var reach = radius + boss.Radius;
                if ((boss.Position - origin).sqrMagnitude >= reach * reach) continue;
                ApplyBossDamage(index, damage, weaponIndex);
            }
            // The browser's shared damageArea presentation is emitted once
            // after all target resolution, including meteor-only blasts.
            BurstFx(origin, SourceDotColor("orange"), 8, 190, 0.35f, 0.7f);
            SpawnRingWave(
                origin,
                8f,
                radius * 2f,
                0.3f,
                new Color(1f, 0.46f, 0.12f, 0.68f));
        }

        private void DamageMeteorsInRadius(Vector2 origin, float radius, float damage)
        {
            EnsureMeteorOrderEntries();
            for (var order = _gameSim.MeteorOrderCount - 1; order >= 0; order--)
            {
                var index = _gameSim.MeteorOrder[order];
                var meteor = _gameSim.Meteors[index];
                if (!meteor.Active || meteor.FuseTimer > 0) continue;
                var reach = radius + meteor.Radius;
                if ((meteor.Position - origin).sqrMagnitude > reach * reach) continue;
                DamageMeteor(index, damage);
            }
        }

        private bool DamageMeteor(int index, float damage)
        {
            if (index < 0 || index >= _gameSim.Meteors.Length) return false;
            var meteor = _gameSim.Meteors[index];
            if (!meteor.Active || meteor.FuseTimer > 0) return false;
            damage *= PlayerDamageMultiplier();
            meteor.Health -= damage;
            meteor.HitTimer = 0.09f;
            if (meteor.Health > 0)
            {
                _gameSim.Meteors[index] = meteor;
                return true;
            }

            meteor.Health = 0;
            if (meteor.Explosive)
            {
                _telemetry.RecordMeteorDestroyed(ArenaIdName(_arenaId), true);
                meteor.FuseTimer = (float)MeteorRules.ExplosiveFlashSeconds;
                PlayFuseWarning(5);
                _gameSim.Meteors[index] = meteor;
                return true;
            }

            _telemetry.RecordMeteorDestroyed(ArenaIdName(_arenaId), false);
            meteor.Active = false;
            RemoveMeteorOrder(index);
            ShatterMeteor(meteor, 0.6f);
            Hide(_meteorViews[index]);
            Hide(_meteorHitViews[index]);
            Hide(_meteorCoreViews[index]);
            Hide(_meteorDangerArcViews[index]);
            Hide(_meteorDangerRingViews[index]);
            Hide(_meteorHealthArcViews[index]);
            _audio?.Play(ProceduralAudio.Cue.Die, 0.78f);
            _gameSim.Meteors[index] = meteor;
            _meteorTarget = MeteorRules.MinOrdinaryMeteors +
                Mathf.FloorToInt((float)(_gameSim.Rng.Next() *
                    (MeteorRules.MaxOrdinaryMeteors - MeteorRules.MinOrdinaryMeteors + 1)));
            return true;
        }

        private void TrackWeaponDamage(int weaponIndex, float damage)
        {
            if (weaponIndex < 0 || weaponIndex >= _weaponDamage.Length || damage <= 0) return;
            _weaponDamage[weaponIndex] += damage;
        }

        private void KillEnemy(int index)
        {
            ResolveEnemyDeath(index, false);
        }

        private void ResolveEnemyDeath(int index, bool selfDetonated)
        {
            var enemy = _gameSim.Enemies[index];
            if (!enemy.Active) return;
            // Browser removeEnemy marks the object dead and compacts the
            // dynamic array before resolving death effects. Do the same in
            // the logical order list while retaining the pooled slot.
            enemy.Active = false;
            _gameSim.Enemies[index] = enemy;
            RemoveEnemyOrder(index);
            var enemyDefinition = FindEnemy(enemy.Id);
            SpawnDeathGhost(enemy, index);
            var destroyedExploder = enemy.Id == "exploder";
            var shouldReward = !selfDetonated || enemy.EliteKind.HasValue;
            if (!shouldReward)
            {
                Hide(_enemyViews[index]);
                Hide(_enemyHarvesterFullViews[index]);
                Hide(_enemyExploderWarningViews[index]);
                Hide(_enemyHarvesterCapacityRingViews[index]);
                Hide(_enemyTelegraphExploderFillViews[index]);
                for (var segment = 0; segment < ExploderTelegraphSegmentCount; segment++)
                    Hide(_enemyTelegraphExploderSegmentViews[index * ExploderTelegraphSegmentCount + segment]);
                Hide(_eliteMarkViews[index]);
                Hide(_eliteChargeLaneViews[index]);
                Hide(_eliteChargeArrowViews[index]);
                return;
            }
            _kills++;
            NotifyObjectiveKill();
            var rewardXp = enemy.Xp;
            // Browser removeEnemy awards a flat 10 score to normal enemies;
            // XP is a separate reward and must not change the kill score.
            var rewardScore = enemy.Elite ? 275 : 10;
            var rewardParts = 0;
            if (enemy.EliteKind.HasValue)
            {
                TriggerFreeze(0.06f);
                _eliteKills++;
                _telemetry.RecordEliteKill(
                    ArenaIdName(_arenaId),
                    EliteRules.EliteVariantDef(enemy.EliteKind.Value).BaseId);
                var arena = FindArena(ArenaIdName(_arenaId));
                var reward = EliteRules.EliteVariantRewardFor(
                    enemy.EliteKind.Value,
                    arena?.EliteRewardMultiplier ?? 1);
                // Browser removeEnemy releases the base enemy XP plus the
                // elite-variant bounty; the bounty is not a replacement.
                rewardXp = enemy.Xp + reward.Xp;
                rewardScore = reward.Score;
                rewardParts = reward.Parts;
            }
            else if (enemy.Elite)
            {
                _eliteKills++;
                _telemetry.RecordEliteKill(ArenaIdName(_arenaId), "standard");
                rewardScore = 275;
                rewardParts = 8;
            }
            else if (enemy.Roster == EnemyRoster.Two)
            {
                _telemetry.RecordRosterTwoKill(ArenaIdName(_arenaId));
            }

            _score += rewardScore;
            if (rewardParts > 0)
            {
                _partsEarned += rewardParts;
            }
            Hide(_enemyViews[index]);
                Hide(_enemyHarvesterFullViews[index]);
                Hide(_enemyExploderWarningViews[index]);
                Hide(_enemyHarvesterCapacityRingViews[index]);
                Hide(_enemyTelegraphExploderFillViews[index]);
                for (var segment = 0; segment < ExploderTelegraphSegmentCount; segment++)
                    Hide(_enemyTelegraphExploderSegmentViews[index * ExploderTelegraphSegmentCount + segment]);
                Hide(_eliteMarkViews[index]);
            Hide(_eliteChargeLaneViews[index]);
            Hide(_eliteChargeArrowViews[index]);

            if (enemy.EliteKind.HasValue)
            {
                AddCameraShake(0.5f);
                SpawnRingWave(enemy.Position, 22f, 320f, 0.5f,
                    new Color(1f, 0.86f, 0.08f, 0.78f));
                BurstFx(enemy.Position, SourceDotColor("yellow"), 20, 320, 0.62f, 0.9f);
                // Browser removeEnemy emits eliteDie after the variant death
                // layers and immediately before its clear toast.
                _audio?.Play(ProceduralAudio.Cue.Elite, 0.72f);
                ShowArenaToast(
                    $"{EliteRules.EliteVariantDef(enemy.EliteKind.Value).Name} cleared",
                    2.5f,
                    ToastKind.Reward,
                    $"+{rewardParts} Parts");
            }

            if (enemy.Elite && !enemy.EliteKind.HasValue)
            {
                // Standard Elite defeat presentation is separate from normal
                // enemy death FX in the browser.
                TriggerFreeze(0.085f);
                AddCameraShake(0.65f);
                SpawnRingWave(enemy.Position, 28f, 390f, 0.58f,
                    new Color(0.98f, 0.8f, 0.08f, 0.82f));
                BurstFx(enemy.Position, SourceDotColor("yellow"), 28, 350, 0.75f, 1f);
                // Keep the standard elite cue ordered after its presentation
                // effects, matching the browser's eliteDie() call.
                _audio?.Play(ProceduralAudio.Cue.Elite, 0.72f);
                ShowArenaToast("Elite cleared", 2.5f, ToastKind.Reward, "+8 Parts");
            }
            else if (destroyedExploder && !selfDetonated)
            {
                // A destroyed Exploder, including the Elite Exploder variant,
                // produces the browser's friendly-side chain blast. Its self-
                // fuse path above remains separate and deactivates in place.
                DetonateExploderBlast(enemy, EnemyIdentity(enemy, index));
            }
            else
            {
                var brute = enemy.Id == "brute";
                var runner = enemy.Id == "runner";
                AddCameraShake(brute ? 0.12f : 0.055f);
                BurstFx(
                    enemy.Position,
                    EnemyParticleColor(enemy),
                    brute ? 16 : 8,
                    runner ? 330 : 250,
                    runner ? 0.34f : 0.52f,
                    0.85f);
                if (brute)
                {
                    SpawnRingWave(enemy.Position, 18f, 170f, 0.4f,
                        new Color(1f, 0.46f, 0.12f, 0.68f));
                }
                else if (enemy.Id == "guard")
                {
                    SpawnRingWave(enemy.Position, 14f, 190f, 0.35f,
                        new Color(0.25f, 0.85f, 1f, 0.68f));
                }
                else if (enemy.Id == "gunner" || enemy.Id == "twinGunner")
                {
                    BurstFx(enemy.Position, SourceDotColor("orange"), 5, 290, 0.4f, 0.7f);
                }
                _audio?.Play(ProceduralAudio.Cue.Die, 1.08f);
            }

            if (enemy.Id == "splitter" && !enemy.SplitterFragment)
            {
                for (var fragment = 0; fragment < 3; fragment++)
                {
                    var angle = fragment / 3f * Mathf.PI * 2 + enemy.Seed;
                    SpawnEnemy(
                        "splitter",
                        enemy.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 14,
                        null,
                        false,
                        true,
                        0,
                        0.3f);
                }
                SpawnRingWave(enemy.Position, 12f, 145f, 0.3f,
                    ParseColor(enemyDefinition?.Color, new Color(0.96f, 0.45f, 0.71f, 0.72f)));
            }

            if (enemy.Id == "carrier")
            {
                for (var child = 0; child < _gameSim.Enemies.Length; child++)
                {
                    if (_gameSim.Enemies[child].Active &&
                        _gameSim.Enemies[child].CarrierDrone &&
                        _gameSim.Enemies[child].SummonedByCarrierSpawnId == enemy.SpawnId)
                        KillEnemy(child);
                }
            }

            // Browser resolves splitter/carrier death side effects first, then
            // releases the defeated enemy's XP. Keep this order so pooled slot
            // reuse and gameplay RNG consume the same sequence.
            SpawnPickup(enemy.Position, rewardXp);

            // Browser removeEnemy applies the Part roll to every non-Elite
            // enemy, including Carrier Drones and Splitter Fragments.
            if (!enemy.Elite && _gameSim.Rng.Next() < 0.045)
            {
                SpawnSpecialPickup(enemy.Position, 1, PickupKind.Part);
            }
            var rareChance = enemy.EliteKind.HasValue ? 0.35 : 0.011;
            if ((enemy.Elite && !enemy.EliteKind.HasValue) || _gameSim.Rng.Next() < rareChance)
            {
                SpawnRarePickup(enemy.Position);
            }
            // Browser removeEnemy checks kill milestones after each individual
            // death, before the frame-level score milestone pass.
            CheckKillMilestone();
        }

        private void KillBoss(int index)
        {
            var boss = _gameSim.Bosses[index];
            if (!boss.Active) return;
            // Browser killBoss marks the encounter dead and schedules the next
            // boss before any reward-drop RNG is consumed. Keep the pooled
            // presentation state, but commit the defeated state first.
            boss.Active = false;
            boss.State = 5;
            boss.StateTimer = 1.4f;
            boss.DeathTimer = 1.4f;
            boss.Health = 0;
            boss.ActiveAttack = null;
            _gameSim.Bosses[index] = boss;
            var noBossesRemain = ActiveBosses() == 0;
            TriggerFreeze(0.14f);
            AddCameraShake(0.85f);
            _telemetry.RecordBossDefeat(boss.TelemetryInstanceId, (float)_time);
            if (noBossesRemain)
            {
                var schedule = DirectorRules.BossScheduleAfterClear(
                    _time,
                    _nextBossTime,
                    _gameSim.Rng.Next());
                _bossRecoveryUntil = (float)schedule.RecoveryUntil;
                _nextBossTime = (float)schedule.NextBossTime;
                _bossWarned = false;
                _pendingDoubleBoss = false;
            }
            var bossAccent = BossAccent(boss);
            bossAccent.a = 0.95f;
            // Browser killBoss emits the defeat ring before either burst
            // layer. Preserve that order so the ring gets the last cosmetic
            // slot when the shared particle budget is nearly full.
            SpawnRingWave(boss.Position, 40f, 560f, 0.95f, bossAccent);
            BurstFx(boss.Position, BossParticleColor(boss.Id), 36, 390, 0.9f, 1.2f);
            bossAccent.a = 0.82f;
            BurstFx(boss.Position, SourceDotColor("white"), 18, 280, 0.7f, 0.9f);
            _bossKills++;
            NotifyObjectiveBossKilled(boss.Id);
            _score += 1000;
            var definition = FindBoss(boss.Id);
            if (definition != null)
            {
                _partsEarned += (int)definition.RewardParts;
            }
            SpawnPickup(boss.Position, 70f + boss.EncounterIndex * 5f);
            SpawnRarePickup(boss.Position);
            // Browser killBoss() emits the death cue after its reward drops,
            // then shows the named clear toast with the Parts detail.
            _audio?.Play(ProceduralAudio.Cue.BossDeath, 0.9f);
            if (definition != null)
            {
                ShowArenaToast(
                    definition.Name + " cleared",
                    2.5f,
                    ToastKind.Reward,
                    "+" + definition.RewardParts + " Parts");
            }
            // The Void demands its ceremony once the encounter is fully clear.
            if (noBossesRemain) OpenBossRoulette();
        }

        private static void CommitRunParts(SaveData saveData, int partsEarned)
        {
            if (saveData == null) return;
            saveData.parts = AddCounter(saveData.parts, partsEarned);
        }

        private static long AddDamageCounter(long current, long amount)
        {
            var total = Math.Max(0, current) + Math.Max(0, amount);
            return Math.Min(999_999_999_999L, total);
        }

        private static long RoundedDamageCounter(double value)
        {
            // JavaScript Math.round is floor(value + 0.5) for non-negative
            // damage totals; do not use Convert.ToInt64, which is banker’s
            // rounding at exact .5 values.
            return (long)Math.Floor(Math.Max(0d, value) + 0.5d);
        }

        private WeaponDamageEntry[] BuildWeaponDamageEntries()
        {
            var result = new List<WeaponDamageEntry>();
            for (var index = 0; index < Mathf.Min(ContentCatalog.Weapons.Length, _weaponDamage.Length); index++)
            {
                var damage = RoundedDamageCounter(_weaponDamage[index]);
                if (damage <= 0) continue;
                result.Add(new WeaponDamageEntry
                {
                    id = ContentCatalog.Weapons[index].Id,
                    damage = damage,
                });
            }
            return result.ToArray();
        }

        private UnityTelemetryDamageValue[] BuildTelemetryDamage()
        {
            var result = new List<UnityTelemetryDamageValue>();
            for (var index = 0; index < Mathf.Min(ContentCatalog.Weapons.Length, _weaponDamage.Length); index++)
            {
                var damage = RoundedDamageCounter(_weaponDamage[index]);
                if (damage <= 0) continue;
                result.Add(new UnityTelemetryDamageValue
                {
                    id = ContentCatalog.Weapons[index].Id,
                    value = damage,
                });
            }
            return result.ToArray();
        }

        private static string[] WeaponIds()
        {
            var ids = new string[ContentCatalog.Weapons.Length];
            for (var index = 0; index < ids.Length; index++) ids[index] = ContentCatalog.Weapons[index].Id;
            return ids;
        }

        private static float SourceMeteorShardWorldSize(float size, float progress)
        {
            return 24f * Mathf.Max(0f, size) *
                (0.4f + Mathf.Clamp01(progress) * 0.6f);
        }

        private static Color EnemyParticleColor(EnemyState enemy)
        {
            // Browser damage/death bursts use ENEMY_PARTICLE, not the actor
            // body definition color. Elite variants retain their base enemy id.
            switch (enemy.Id)
            {
                case "chaser": return SourceDotColor("pink");
                case "runner": return SourceDotColor("violet");
                case "dasher": return SourceDotColor("fuchsia");
                case "brute": return SourceDotColor("orange");
                case "gunner": return SourceDotColor("red");
                case "twinGunner": return SourceDotColor("orange");
                case "guard": return SourceDotColor("cyan");
                case "exploder": return SourceDotColor("yellow");
                case "technician": return SourceDotColor("emerald");
                case "mortar": return SourceDotColor("orange");
                case "splitter": return SourceDotColor("pink");
                case "bulwark": return SourceDotColor("blue");
                case "harvester": return SourceDotColor("lime");
                case "carrier": return SourceDotColor("yellow");
                case "elite": return SourceDotColor("yellow");
                default: return SourceDotColor("yellow");
            }
        }

        private static Color BossParticleColor(string bossId)
        {
            // Browser bossParticle(): herald violet, matriarch emerald, reaver
            // blue, and warden red. Boss body colors are a separate visual path.
            switch (bossId)
            {
                case "herald": return SourceDotColor("violet");
                case "matriarch": return SourceDotColor("emerald");
                case "reaver": return SourceDotColor("blue");
                default: return SourceDotColor("red");
            }
        }

        private Color EnemySpriteAccent(EnemyState enemy)
        {
            var spriteId = SourceEnemySpriteId(enemy);
            if (spriteId == "elite")
                return ParseColor(ContentCatalog.Elite.Color, Color.yellow);
            return ParseColor(FindEnemy(spriteId)?.Color, EnemyAccent(enemy));
        }

        private Color EnemyAccent(EnemyState enemy)
        {
            if (enemy.EliteKind.HasValue)
                return ParseColor(EliteRules.EliteVariantDef(enemy.EliteKind.Value).Accent, Color.magenta);
            if (enemy.Elite)
                return ParseColor(ContentCatalog.Elite.Color, Color.yellow);
            return ParseColor(FindEnemy(enemy.Id)?.Color, Color.magenta);
        }

        private Color BossAccent(BossState boss)
        {
            return ParseColor(FindBoss(boss.Id)?.Color, Color.magenta);
        }

        private void SpawnFloater(
            Vector2 position,
            string text,
            Color color,
            float size,
            int targetKey = 0,
            int value = 0,
            bool critical = false)
        {
            if (_canvas == null || string.IsNullOrEmpty(text)) return;
            EnsureFloaterOrderEntries();
            if (targetKey > 0 && value > 0)
            {
                for (var order = 0; order < _floaterOrder.Count; order++)
                {
                    var index = _floaterOrder.SlotAt(order);
                    var existing = _floaters[index];
                    if (!existing.Active || existing.TargetKey != targetKey || existing.Value <= 0 ||
                        existing.Life <= existing.MaxLife - 0.3f) continue;
                    existing.Value += value;
                    existing.Text = existing.Value.ToString();
                    existing.Position = (existing.Position + position) * 0.5f;
                    existing.Critical |= critical;
                    existing.Color = existing.Critical
                        ? new Color(0.969f, 0.443f, 0.443f, 1f)
                        : new Color(0.886f, 0.91f, 0.941f, 1f);
                    existing.FontSize = SourceFloaterFontSize(existing.Critical ? 17f : 12.5f);
                    existing.Life = Mathf.Max(existing.Life, 0.48f);
                    var mergedView = _floaterViews[index];
                    if (mergedView != null)
                    {
                        mergedView.text = existing.Text;
                        mergedView.fontSize = existing.FontSize;
                    }
                    _floaters[index] = existing;
                    return;
                }
            }

            var limit = Mathf.Max(8, Mathf.RoundToInt(MaxFloaters * _qualityPreset.FloaterScale));
            var active = 0;
            var slot = -1;
            for (var index = 0; index < _floaters.Length; index++)
            {
                if (_floaters[index].Active)
                {
                    active++;
                    continue;
                }
                if (slot < 0) slot = index;
            }
            if (active >= limit || slot < 0) return;

            var floater = new FloaterState
            {
                Active = true,
                Position = position + new Vector2(
                    ((float)_fxSim.FxRng.Next() - 0.5f) * 14f,
                    SourceFloatingTextAnchorOffset),
                Life = 0.68f,
                MaxLife = 0.68f,
                TargetKey = targetKey,
                Value = Mathf.Max(0, value),
                Critical = critical,
                Color = color,
                FontSize = SourceFloaterFontSize(size),
                View = slot,
            };
            floater.Text = text;
            _floaters[slot] = floater;
            AppendFloaterOrder(slot);
            var view = _floaterViews[slot];
            if (view != null)
            {
                view.text = text;
                view.fontSize = floater.FontSize;
                view.color = color;
                view.enabled = true;
            }
        }

        private void SpawnDamageFloater(int targetKey, Vector2 position, float damage, bool critical)
        {
            // Browser damage numbers use Math.max(1, Math.round(damage));
            // Unity's RoundToInt uses midpoint-to-even at exact .5 values.
            var value = Mathf.Max(1, SourceRound(damage));
            SpawnFloater(
                position,
                value.ToString(),
                critical
                    ? new Color(0.969f, 0.443f, 0.443f, 1f)
                    : new Color(0.886f, 0.91f, 0.941f, 1f),
                critical ? 17f : 12.5f,
                targetKey,
                value,
                critical);
        }

        private void SpawnDamageIndicator(Vector2 sourceDirection)
        {
            var direction = SourceNormalizedDirection(sourceDirection);
            var slot = -1;
            var oldestLife = float.MaxValue;
            for (var index = 0; index < _damageIndicators.Length; index++)
            {
                if (!_damageIndicators[index].Active)
                {
                    slot = index;
                    break;
                }
                if (_damageIndicators[index].Life < oldestLife)
                {
                    oldestLife = _damageIndicators[index].Life;
                    slot = index;
                }
            }
            if (slot < 0) return;
            if (_damageIndicators[slot].Active) RemoveDamageIndicatorOrder(slot);

            var indicator = new DamageIndicatorState
            {
                Active = true,
                Angle = Mathf.Atan2(-direction.y, -direction.x),
                Life = 0.85f,
                MaxLife = 0.85f,
                View = slot,
            };
            _damageIndicators[slot] = indicator;
            AppendDamageIndicatorOrder(slot);
            var view = _damageIndicatorViews[slot];
            if (view != null) view.enabled = true;
        }

        private void SpawnDeathGhost(EnemyState enemy, int sourceIndex)
        {
            if (_qualityPreset.DeathGhosts == false ||
                (_saveData?.settings != null && _saveData.settings.reducedMotion)) return;
            var slot = SelectDeathGhostSlot(_deathGhosts);
            if (slot < 0) return;

            var ghost = new DeathGhostState
            {
                Active = true,
                Position = enemy.Position,
                Radius = enemy.Radius,
                // Browser spawnDeathGhost uses sprites.enemy[enemy.type], not
                // the Roster II silhouette canvas used by the live actor.
                VisualSize = SourceEnemySpriteWorldSize(
                    enemy.Id,
                    enemy.Elite,
                    enemy.EliteKind.HasValue
                        ? EliteRules.EliteVariantDef(enemy.EliteKind.Value).BaseId
                        : null,
                    false,
                    enemy.Radius),
                Rotation = enemy.Rotation * Mathf.Rad2Deg,
                Life = 0.16f,
                MaxLife = 0.16f,
                Id = enemy.Id,
                Accent = EnemyAccent(enemy),
                Elite = enemy.Elite,
                EliteKind = enemy.EliteKind,
                View = slot,
            };
            _deathGhosts[slot] = ghost;
            AppendDeathGhostOrder(slot);
            var view = _deathGhostViews[slot];
            if (view != null)
            {
                view.sprite = ProceduralSpriteFactory.Enemy(
                    SourceEnemySpriteId(enemy),
                    CachedEnemySpriteAccent(enemy),
                    false);
                view.transform.position = ghost.Position;
                view.transform.rotation = Quaternion.Euler(0, 0, ghost.Rotation);
                view.transform.localScale = Vector3.one * ghost.VisualSize;
                view.color = new Color(1f, 1f, 1f, 0.5f);
                view.enabled = true;
            }
        }

        private void UpdateFloaters(float dt)
        {
            for (var index = 0; index < _floaters.Length; index++)
            {
                var floater = _floaters[index];
                if (!floater.Active) continue;
                floater.Life -= dt;
                floater.Position += Vector2.up * 46f * dt;
                if (floater.Life <= 0)
                {
                    floater.Active = false;
                    RemoveFloaterOrder(index);
                    Hide(_floaterViews[index]);
                }
                _floaters[index] = floater;
            }
        }

        private void UpdateDeathGhosts(float dt)
        {
            for (var index = 0; index < _deathGhosts.Length; index++)
            {
                var ghost = _deathGhosts[index];
                if (!ghost.Active) continue;
                ghost.Life -= dt;
                if (ghost.Life <= 0)
                {
                    ghost.Active = false;
                    RemoveDeathGhostOrder(index);
                    Hide(_deathGhostViews[index]);
                }
                _deathGhosts[index] = ghost;
            }
        }

        private void UpdateDamageIndicators(float dt)
        {
            for (var index = 0; index < _damageIndicators.Length; index++)
            {
                var indicator = _damageIndicators[index];
                if (!indicator.Active) continue;
                indicator.Life -= dt;
                if (indicator.Life <= 0)
                {
                    indicator.Active = false;
                    RemoveDamageIndicatorOrder(index);
                    Hide(_damageIndicatorViews[index]);
                }
                _damageIndicators[index] = indicator;
            }
        }

        private void SetEnemyPresentationPriority(int index, int priority)
        {
            if (index < 0 || index >= _gameSim.Enemies.Length) return;
            SetRendererPriority(_enemyViews[index], priority);
            SetRendererPriority(_enemyHarvesterFullViews[index], priority);
            SetRendererPriority(_enemyExploderWarningViews[index], priority);
            SetRendererPriority(_eliteMarkViews[index], priority);
            SetRendererPriority(_eliteChargeLaneViews[index], priority);
            SetRendererPriority(_eliteChargeArrowViews[index], priority);
            SetRendererPriority(_eliteChargeFillRenderers[index], priority);
            SetRendererPriority(_eliteChargeArrowFillRenderers[index], priority);
            SetRendererPriority(_enemyTelegraphRingViews[index], priority);
            SetRendererPriority(_enemyTelegraphLineViews[index], priority);
            SetRendererPriority(_enemyTelegraphSecondaryLineViews[index], priority);
            SetRendererPriority(_enemyTelegraphTertiaryLineViews[index], priority);
            SetRendererPriority(_enemyHarvesterCapacityRingViews[index], priority);
            SetRendererPriority(_enemyTelegraphSiegeDashRenderers[index], priority);
            SetRendererPriority(_enemyTelegraphExploderFillViews[index], priority);
            SetRendererPriority(_enemyTelegraphMortarFillViews[index], priority);
            SetRendererPriority(_enemyTelegraphFillRenderers[index], priority);
            SetRendererPriority(_enemyTelegraphArrowFillRenderers[index], priority);
            SetRendererPriority(_enemyHealthArcViews[index], priority);
            SetRendererPriority(_enemyShieldArcViews[index], priority);
            SetRendererPriority(_enemyHealthBackgroundViews[index], priority);
            SetRendererPriority(_enemyHealthFillViews[index], priority);
            for (var segment = 0; segment < ExploderTelegraphSegmentCount; segment++)
                SetRendererPriority(
                    _enemyTelegraphExploderSegmentViews[index * ExploderTelegraphSegmentCount + segment],
                    priority);
            for (var segment = 0; segment < MortarTelegraphSegmentCount; segment++)
                SetRendererPriority(
                    _enemyTelegraphMortarSegmentViews[index * MortarTelegraphSegmentCount + segment],
                    priority);
        }

        private static int OwnedUpgradeChipRows(float viewportHeight)
        {
            var maxHeight = Mathf.Min(viewportHeight * 0.48f, 360f);
            return Mathf.Max(1, Mathf.FloorToInt((maxHeight + 5f) / 32f));
        }

        private static void ConfigureOwnedUpgradeRankLayout(Text rank, bool narrow, float rankWidth)
        {
            if (rank == null) return;
            rank.rectTransform.anchoredPosition = new Vector2(narrow ? -6f : -8f, 0f);
            rank.rectTransform.sizeDelta = new Vector2(
                narrow ? Mathf.Max(13f, rankWidth) : 42f,
                rank.rectTransform.sizeDelta.y);
        }

        internal bool CommitSettings()
        {
            if (_saveStore == null || _saveData == null)
            {
                SetMenuNotice("Settings could not be saved");
                return false;
            }

            try
            {
                _saveData = SaveStore.Sanitize(_saveData);
                _saveStore.Save(_saveData);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("VoidFall settings could not be saved: " + exception.Message);
                SetMenuNotice("Settings could not be saved");
                return false;
            }
        }

        private static string WeaponDisplayAccent(int weaponIndex, bool evolved)
        {
            if (weaponIndex < 0 || weaponIndex >= ContentCatalog.Weapons.Length)
                return "#67e8f9";
            var weapon = ContentCatalog.Weapons[weaponIndex];
            if (!evolved) return weapon.Accent;
            for (var index = 0; index < ContentCatalog.Evolutions.Length; index++)
            {
                var evolution = ContentCatalog.Evolutions[index];
                if (evolution.WeaponId == weapon.Id) return evolution.Accent;
            }
            return weapon.Accent;
        }

        private static string WeaponDisplayName(int weaponIndex, bool evolved)
        {
            if (weaponIndex < 0 || weaponIndex >= ContentCatalog.Weapons.Length)
                return string.Empty;
            var weapon = ContentCatalog.Weapons[weaponIndex];
            if (!evolved) return weapon.Name;
            for (var index = 0; index < ContentCatalog.Evolutions.Length; index++)
            {
                var evolution = ContentCatalog.Evolutions[index];
                if (evolution.WeaponId == weapon.Id) return evolution.Name;
            }
            return weapon.Name;
        }

        private static float UpgradeCardContentPadding(bool shortLandscape)
        {
            return shortLandscape ? 13f : 20f;
        }

        private static float UpgradeRankPipWidth()
        {
            return 26f;
        }

        private static float UpgradeRankPipHeight()
        {
            return 3f;
        }

        private static int UpgradeCardMetaFontSize()
        {
            return BrowserNearestFontSize(10f * 1.15f);
        }

        private static int UpgradeCardNameFontSize()
        {
            return BrowserNearestFontSize(18f * 1.15f);
        }

        private static int UpgradeCardDescriptionFontSize()
        {
            return BrowserNearestFontSize(12.5f * 1.15f);
        }

        private static int UpgradeCardIndexFontSize()
        {
            return BrowserNearestFontSize(10f * 1.15f);
        }

        private static float UpgradeCardIconMarginBottom()
        {
            return 15f;
        }

        private static float UpgradeCardMetaLineHeight()
        {
            return 10f * 1.15f * 1.2f;
        }

        private static float UpgradeCardNameMarginTop()
        {
            return 8f;
        }

        private static float UpgradeCardNameLineHeight()
        {
            return 18f * 1.15f * 1.15f;
        }

        private static float UpgradeCardDescriptionMarginTop()
        {
            return 11f;
        }

        private GUIStyle UpgradeCardStyle(Color accent, bool evolution)
        {
            var key = ColorUtility.ToHtmlStringRGB(accent) + (evolution ? ":evolution" : ":normal");
            if (_upgradeCardStyleCache.TryGetValue(key, out var cached)) return cached;

            var style = new GUIStyle(GUI.skin.button)
            {
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                border = new RectOffset(12, 12, 12, 12),
            };
            var normalTop = evolution
                ? Color.Lerp(new Color(0.075f, 0.098f, 0.20f, 0.98f), accent, 0.08f)
                : new Color(0.063f, 0.082f, 0.18f, 0.95f);
            var normalBottom = evolution
                ? Color.Lerp(new Color(0.027f, 0.035f, 0.094f, 0.99f), accent, 0.035f)
                : new Color(0.031f, 0.039f, 0.102f, 0.97f);
            var normalBorder = evolution
                ? Color.Lerp(Color.white, accent, 0.68f)
                : Color.Lerp(new Color(0.20f, 0.255f, 0.333f, 1f), accent, 0.27f);
            var hoverBorder = Color.Lerp(new Color(0.796f, 0.835f, 0.882f, 1f), accent, 0.72f);
            var activeBorder = Color.Lerp(new Color(0.796f, 0.835f, 0.882f, 1f), accent, 0.86f);
            var normal = RoundedGradientGuiTexture(
                normalTop,
                normalBottom,
                normalBorder,
                128,
                256,
                12f,
                "VoidFall Upgrade Card " + key);
            var hover = RoundedGradientGuiTexture(
                evolution
                    ? Color.Lerp(normalTop, accent, 0.035f)
                    : new Color(0.075f, 0.11f, 0.22f, 0.98f),
                evolution
                    ? Color.Lerp(normalBottom, accent, 0.02f)
                    : new Color(0.035f, 0.045f, 0.12f, 0.99f),
                hoverBorder,
                128,
                256,
                12f,
                "VoidFall Upgrade Card Hover " + key);
            var active = RoundedGradientGuiTexture(
                normalTop,
                normalBottom,
                activeBorder,
                128,
                256,
                12f,
                "VoidFall Upgrade Card Active " + key);
            SetGuiStyleState(style.normal, normal, Color.white);
            SetGuiStyleState(style.hover, hover, Color.white);
            SetGuiStyleState(style.active, active, Color.white);
            SetGuiStyleState(style.focused, hover, Color.white);
            _upgradeCardStyleCache[key] = style;
            return style;
        }

        private GUIStyle UpgradeIconStyle(Color accent, bool evolution)
        {
            var key = ColorUtility.ToHtmlStringRGB(accent) + (evolution ? ":evolution" : ":normal");
            if (_upgradeIconStyleCache.TryGetValue(key, out var cached)) return cached;

            var style = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                border = new RectOffset(12, 12, 12, 12),
            };
            var fill = new Color(accent.r, accent.g, accent.b, 0.11f);
            var border = new Color(accent.r, accent.g, accent.b, 0.44f);
            var texture = RoundedGradientGuiTexture(
                fill,
                fill,
                border,
                56,
                56,
                evolution ? 12f : 28f,
                "VoidFall Upgrade Icon " + key);
            SetGuiStyleState(style.normal, texture, Color.white);
            SetGuiStyleState(style.hover, texture, Color.white);
            SetGuiStyleState(style.focused, texture, Color.white);
            _upgradeIconStyleCache[key] = style;
            return style;
        }

        private float UpgradeCardEntranceProgressForRuntime(int index)
        {
            if (_levelUpPromptOpenedAt < 0f) return 1f;
            return UpgradeCardEntranceProgress(
                Time.realtimeSinceStartup - _levelUpPromptOpenedAt,
                index);
        }

        private static float UpgradeCardEntranceProgress(float elapsedSeconds, int index)
        {
            var delay = Mathf.Max(0, index) * UpgradeCardEntranceDelay();
            if (elapsedSeconds <= delay) return 0f;
            var local = elapsedSeconds - delay;
            if (local >= UpgradeCardEntranceDuration()) return 1f;
            var normalized = Mathf.Clamp01(local / UpgradeCardEntranceDuration());
            return CubicBezierEase(normalized, 0.22f, 1.4f, 0.36f, 1f);
        }

        private static bool UpgradeCardIsHovered(Rect rect)
        {
            return Event.current != null &&
                   Event.current.type == EventType.Repaint &&
                   rect.Contains(Event.current.mousePosition);
        }

        private static float UpgradeCardHoverLift()
        {
            return 6f;
        }

        private static float UpgradeCardHoverScale()
        {
            return 1.03f;
        }

        private static float UpgradeCardEntranceDuration()
        {
            return 0.34f;
        }

        private static float UpgradeCardEntranceDelay()
        {
            return 0.07f;
        }

        private static float UpgradeCardMinHeight(bool narrow)
        {
            return narrow ? 142f : 242f;
        }

        private static float UpgradeCardSmallViewportMinHeight()
        {
            return 190f;
        }

        private static float UpgradeCardGridGap()
        {
            return 16f;
        }

        private static string UpgradeOptionIconId(UpgradeOptionDefinition option)
        {
            if (option == null) return "repair";
            switch (option.Kind)
            {
                case UpgradeOptionKind.Weapon:
                case UpgradeOptionKind.Support:
                case UpgradeOptionKind.Late:
                case UpgradeOptionKind.Evolution:
                    return option.TargetId ?? "repair";
                default:
                    return "repair";
            }
        }

        private static Texture2D UpgradeOptionIconTexture(string iconId)
        {
            if (iconId != "repair") return BuildChipIconTexture();
            if (_upgradeHeartIconTexture != null) return _upgradeHeartIconTexture;
            _upgradeHeartIconTexture = Resources.Load<Texture2D>("VoidFall/UpgradeHeartRaster");
            if (_upgradeHeartIconTexture != null) return _upgradeHeartIconTexture;
            var sprite = Resources.Load<Sprite>("VoidFall/UpgradeHeart");
            _upgradeHeartIconTexture = sprite != null
                ? sprite.texture
                : Resources.Load<Texture2D>("VoidFall/UpgradeHeart");
            return _upgradeHeartIconTexture;
        }

        private static Rect UpgradeOptionIconUv(string iconId)
        {
            return iconId == "repair" ? new Rect(0f, 0f, 1f, 1f) : BuildChipIconUv(iconId);
        }

        private static string UpgradeOptionLabel(UpgradeOptionDefinition option)
        {
            if (option == null) return string.Empty;
            switch (option.Kind)
            {
                case UpgradeOptionKind.Weapon:
                    return option.CurrentRank == 0
                        ? "New weapon"
                        : "Weapon " + option.CurrentRank + " \u2192 " + option.NextRank;
                case UpgradeOptionKind.Support:
                    return "Support " + option.CurrentRank + " \u2192 " + option.NextRank;
                case UpgradeOptionKind.Evolution:
                    return "Evolution ready";
                case UpgradeOptionKind.Late:
                    return "Long-run tune " + option.NextRank;
                default:
                    return "Immediate";
            }
        }

        private static string UpgradeOptionRankPips(UpgradeOptionDefinition option)
        {
            if (option == null ||
                (option.Kind != UpgradeOptionKind.Weapon && option.Kind != UpgradeOptionKind.Support) ||
                option.MaxRank <= 0)
            {
                return string.Empty;
            }

            var pips = string.Empty;
            for (var rank = 0; rank < option.MaxRank; rank++)
            {
                if (rank < option.CurrentRank) pips += " [#]";
                else if (rank == option.CurrentRank) pips += " [>]";
                else pips += " [ ]";
            }
            return "\nRanks" + pips;
        }

        private static float PlayerRingRotationRate()
        {
            return 1.35f;
        }

        private static int ResultDamageFontSize()
        {
            // React computes the 13px source damage row at 14.95px.
            return 15;
        }

        private static float ResultDamageRowGap()
        {
            return 6f;
        }

        private GUIStyle ResultDamageLabelStyle()
        {
            if (_resultDamageLabelStyle == null)
            {
                _resultDamageLabelStyle = new GUIStyle(MenuBodyStyle())
                {
                    font = BrowserBodyFont(),
                    fontSize = ResultDamageFontSize(),
                    alignment = TextAnchor.MiddleLeft,
                    wordWrap = false,
                    padding = new RectOffset(0, 0, 0, 0),
                    normal = { textColor = new Color(0.796f, 0.835f, 0.882f, 1f) },
                };
            }
            return _resultDamageLabelStyle;
        }

        private GUIStyle ResultDamageValueStyle()
        {
            if (_resultDamageValueStyle == null)
            {
                _resultDamageValueStyle = new GUIStyle(ResultDamageLabelStyle())
                {
                    font = BrowserDisplayFont(),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleRight,
                    normal = { textColor = new Color(0.973f, 0.98f, 0.988f, 1f) },
                };
            }
            return _resultDamageValueStyle;
        }

        private static float ResultBadgeMinHeight()
        {
            return 28f;
        }

        private void UpdateGameplayCameraViewport()
        {
            if (_camera == null || Screen.width <= 0 || Screen.height <= 0) return;
            var viewportHalf = GameplayViewportHalfExtent(Screen.width, Screen.height);
            _camera.orthographicSize = viewportHalf.y;
            _camera.aspect = viewportHalf.x / Mathf.Max(0.5f, viewportHalf.y);
        }

        private bool SpawnSourceParticle(
            Vector2 position,
            Vector2 velocity,
            float life,
            float size,
            Color color)
        {
            if (!_fxSim.TrySpawnSourceParticle(
                    position,
                    velocity,
                    life,
                    size,
                    color,
                    SourceParticleLimit(_qualityPreset.ParticleScale),
                    out var slot))
            {
                return false;
            }
            var view = EnsureSourceParticleView(slot);
            view.transform.position = position;
            view.color = color;
            view.enabled = true;
            return true;
        }

        private SpriteRenderer EnsureEnemyView(int index)
        {
            if (_enemyViews[index] != null) return _enemyViews[index];
            _enemyViews[index] = CreateView("Enemy_" + index, ProceduralSpriteFactory.Circle(), 15);
            return _enemyViews[index];
        }

        private SpriteRenderer EnsureEnemyHarvesterFullView(int index)
        {
            if (_enemyHarvesterFullViews[index] != null) return _enemyHarvesterFullViews[index];
            var view = CreateView(
                "Enemy Harvester Full Overlay_" + index,
                ProceduralSpriteFactory.Enemy("harvester", Color.white, true),
                16);
            var screenMaterial = ResolveBlastWaveScreenMaterial();
            if (screenMaterial != null) view.sharedMaterial = screenMaterial;
            _enemyHarvesterFullViews[index] = view;
            return view;
        }

        private SpriteRenderer EnsureEnemyExploderWarningView(int index)
        {
            if (_enemyExploderWarningViews[index] != null) return _enemyExploderWarningViews[index];
            var view = CreateView(
                "Enemy Exploder Warning Overlay_" + index,
                ProceduralSpriteFactory.Enemy("exploder", Color.white, true),
                17);
            var screenMaterial = ResolveBlastWaveScreenMaterial();
            if (screenMaterial != null) view.sharedMaterial = screenMaterial;
            _enemyExploderWarningViews[index] = view;
            return view;
        }

        private SpriteRenderer EnsureEliteMarkView(int index)
        {
            if (_eliteMarkViews[index] != null) return _eliteMarkViews[index];
            _eliteMarkViews[index] = CreateView("Elite Mark_" + index, ProceduralSpriteFactory.EliteMark(), 18);
            return _eliteMarkViews[index];
        }

        private SpriteRenderer EnsureBulletView(int index)
        {
            if (_bulletViews[index] != null) return _bulletViews[index];
            _bulletContrastViews[index] = CreateView("BulletContrast_" + index, ProceduralSpriteFactory.Circle(), 34);
            _bulletViews[index] = CreateView("Bullet_" + index, ProceduralSpriteFactory.Circle(), 35);
            var additiveMaterial = ResolveAdditiveSpriteMaterial();
            if (additiveMaterial != null)
            {
                _bulletContrastViews[index].sharedMaterial = additiveMaterial;
                _bulletViews[index].sharedMaterial = additiveMaterial;
            }
            return _bulletViews[index];
        }

        private static float SourceBulletVisualScale(string weaponId, float radius, int rank)
        {
            var referenceRadius = weaponId == "pistol"
                ? 3f
                : weaponId == "scattergun"
                    ? 4f
                    : weaponId == "railgun" ? 9f : 6f;
            return (1f + Mathf.Max(0, rank - 1) * 0.035f) *
                Mathf.Clamp(radius / referenceRadius, 0.58f, 1.72f);
        }

        private SpriteRenderer EnsureMeteorView(int index)
        {
            if (_meteorViews[index] != null) return _meteorViews[index];
            _meteorViews[index] = CreateView("Meteor_" + index, ProceduralSpriteFactory.Circle(), 5);
            return _meteorViews[index];
        }

        private SpriteRenderer EnsureMeteorHitView(int index)
        {
            if (_meteorHitViews[index] != null) return _meteorHitViews[index];
            _meteorHitViews[index] = CreateView("Meteor Hit_" + index, ProceduralSpriteFactory.Circle(), 6);
            var additiveMaterial = ResolveAdditiveSpriteMaterial();
            if (additiveMaterial != null) _meteorHitViews[index].sharedMaterial = additiveMaterial;
            return _meteorHitViews[index];
        }

        private SpriteRenderer EnsureMeteorCoreView(int index)
        {
            if (_meteorCoreViews[index] != null) return _meteorCoreViews[index];
            _meteorCoreViews[index] = CreateView("Meteor Core_" + index, ProceduralSpriteFactory.MeteorCore(), 7);
            var additiveMaterial = ResolveAdditiveSpriteMaterial();
            if (additiveMaterial != null) _meteorCoreViews[index].sharedMaterial = additiveMaterial;
            return _meteorCoreViews[index];
        }

        private SpriteRenderer EnsureMeteorShardView(int index)
        {
            if (_meteorShardViews[index] != null) return _meteorShardViews[index];
            _meteorShardViews[index] = CreateView("Meteor Shard_" + index, ProceduralSpriteFactory.MeteorShard(index), 40);
            return _meteorShardViews[index];
        }

        private LineRenderer EnsureMeteorDangerArcView(int index)
        {
            if (_meteorDangerArcViews[index] != null) return _meteorDangerArcViews[index];
            _meteorDangerArcViews[index] = CreateLineView("Meteor Danger Arc_" + index, 7);
            return _meteorDangerArcViews[index];
        }

        private LineRenderer EnsureMeteorDangerRingView(int index)
        {
            if (_meteorDangerRingViews[index] != null) return _meteorDangerRingViews[index];
            _meteorDangerRingViews[index] = CreateLineView("Meteor Danger Ring_" + index, 7);
            return _meteorDangerRingViews[index];
        }

        private LineRenderer EnsureMeteorHealthArcView(int index)
        {
            if (_meteorHealthArcViews[index] != null) return _meteorHealthArcViews[index];
            _meteorHealthArcViews[index] = CreateLineView("Meteor Health Arc_" + index, 8);
            return _meteorHealthArcViews[index];
        }

        private LineRenderer EnsureEliteChargeLaneView(int index)
        {
            if (_eliteChargeLaneViews[index] != null) return _eliteChargeLaneViews[index];
            _eliteChargeLaneViews[index] = CreateLineView("Elite Charge Lane_" + index, 12);
            return _eliteChargeLaneViews[index];
        }

        private LineRenderer EnsureEliteChargeArrowView(int index)
        {
            if (_eliteChargeArrowViews[index] != null) return _eliteChargeArrowViews[index];
            _eliteChargeArrowViews[index] = CreateLineView("Elite Charge Arrow_" + index, 13);
            return _eliteChargeArrowViews[index];
        }

        private MeshFilter EnsureEliteChargeFillView(int index)
        {
            if (_eliteChargeFillViews[index] != null) return _eliteChargeFillViews[index];
            _eliteChargeFillViews[index] = CreateMeshView(
                "Elite Charge Fill_" + index,
                12,
                out _eliteChargeFillRenderers[index]);
            _eliteChargeFillBuffers[index] = new TelegraphQuadBuffer();
            return _eliteChargeFillViews[index];
        }

        private MeshFilter EnsureEliteChargeArrowFillView(int index)
        {
            if (_eliteChargeArrowFillViews[index] != null) return _eliteChargeArrowFillViews[index];
            _eliteChargeArrowFillViews[index] = CreateMeshView(
                "Elite Charge Arrow Fill_" + index,
                13,
                out _eliteChargeArrowFillRenderers[index]);
            _eliteChargeArrowFillBuffers[index] = new TelegraphQuadBuffer();
            return _eliteChargeArrowFillViews[index];
        }

        private LineRenderer EnsureEnemyHarvesterCapacityRingView(int index)
        {
            if (_enemyHarvesterCapacityRingViews[index] != null)
                return _enemyHarvesterCapacityRingViews[index];
            var view = CreateLineView("Enemy Harvester Capacity Ring_" + index, 16);
            var additiveMaterial = ResolveAdditiveSpriteMaterial();
            if (additiveMaterial != null) view.sharedMaterial = additiveMaterial;
            _enemyHarvesterCapacityRingViews[index] = view;
            return view;
        }

        private LineRenderer EnsureEnemyHealthArcView(int index)
        {
            if (_enemyHealthArcViews[index] != null) return _enemyHealthArcViews[index];
            _enemyHealthArcViews[index] = CreateLineView("Enemy Health Arc_" + index, 19);
            return _enemyHealthArcViews[index];
        }

        private LineRenderer EnsureEnemyShieldArcView(int index)
        {
            if (_enemyShieldArcViews[index] != null) return _enemyShieldArcViews[index];
            _enemyShieldArcViews[index] = CreateLineView("Enemy Shield Arc_" + index, 20);
            return _enemyShieldArcViews[index];
        }

        private SpriteRenderer EnsureEnemyHealthBackgroundView(int index)
        {
            if (_enemyHealthBackgroundViews[index] != null) return _enemyHealthBackgroundViews[index];
            _enemyHealthBackgroundViews[index] = CreateView(
                "Enemy Health Background_" + index,
                ProceduralSpriteFactory.Square(),
                21);
            return _enemyHealthBackgroundViews[index];
        }

        private SpriteRenderer EnsureEnemyHealthFillView(int index)
        {
            if (_enemyHealthFillViews[index] != null) return _enemyHealthFillViews[index];
            _enemyHealthFillViews[index] = CreateView(
                "Enemy Health Fill_" + index,
                ProceduralSpriteFactory.Square(),
                22);
            return _enemyHealthFillViews[index];
        }

        private void SpawnImpactMark(Vector2 position, float radius, float rotation)
        {
            var slot = -1;
            var oldestAge = -1f;
            for (var index = 0; index < _impactMarks.Length; index++)
            {
                if (!_impactMarks[index].Active)
                {
                    slot = index;
                    break;
                }
                if (_impactMarks[index].Age > oldestAge)
                {
                    oldestAge = _impactMarks[index].Age;
                    slot = index;
                }
            }
            if (slot < 0) return;
            if (_impactMarks[slot].Active) RemoveImpactMarkOrder(slot);
            _impactMarks[slot] = new ImpactMarkState
            {
                Active = true,
                Position = position,
                Radius = radius,
                Rotation = rotation,
                Age = 0,
                Life = 4.2f,
                View = slot,
            };
            AppendImpactMarkOrder(slot);
            var view = EnsureImpactMarkView(slot);
            view.transform.position = position;
            view.transform.rotation = Quaternion.Euler(0, 0, rotation * Mathf.Rad2Deg);
            view.transform.localScale = Vector3.one * (radius * 2f);
            view.color = new Color(1f, 1f, 1f, 0.72f);
            view.enabled = true;
            for (var segment = 0; segment < ImpactHeatSegmentCount; segment++)
                EnsureImpactHeatView(slot, segment).enabled = true;
        }

        private void UpdateImpactMarks(float dt)
        {
            for (var index = 0; index < _impactMarks.Length; index++)
            {
                var mark = _impactMarks[index];
                if (!mark.Active) continue;
                mark.Age += dt;
                if (mark.Age >= mark.Life)
                {
                    mark.Active = false;
                    RemoveImpactMarkOrder(index);
                    Hide(_impactMarkViews[index]);
                    for (var segment = 0; segment < ImpactHeatSegmentCount; segment++)
                        Hide(_impactHeatViews[ImpactHeatSlot(index, segment)]);
                }
                _impactMarks[index] = mark;
            }
        }

        private void SpawnRingWave(Vector2 position, float startRadius, float growth, float life, Color color)
        {
            if (_saveData?.settings != null && _saveData.settings.reducedMotion) return;
            if (_qualityPreset.ParticleScale <= 0.01f) return;
            if (ActiveFxVisualCount() >= SourceParticleLimit(_qualityPreset.ParticleScale)) return;
            _fxSim.TrySpawnRingWave(position, startRadius, growth, life, color, out var slot);
            if (slot < 0) return;
            EnsureRingWaveSpriteView(slot).enabled = true;
        }

        private void UpdateRingWaves(float dt)
        {
            var expired = _fxSim.AdvanceRingWaves(dt, _fxExpiryScratch);
            for (var index = 0; index < expired; index++)
            {
                var slot = _fxExpiryScratch[index];
                Hide(_ringWaveViews[slot]);
                Hide(_ringWaveGlowViews[slot]);
                Hide(_ringWaveSpriteViews[slot]);
            }
        }
        private void SpawnBlastWave(Vector2 position, float maxRadius, float life, bool bomb)
        {
            var slot = -1;
            var oldestAge = -1f;
            for (var index = 0; index < _blastWaves.Length; index++)
            {
                if (!_blastWaves[index].Active)
                {
                    slot = index;
                    break;
                }
                if (_blastWaves[index].Age > oldestAge)
                {
                    oldestAge = _blastWaves[index].Age;
                    slot = index;
                }
            }
            if (slot < 0) return;
            if (_blastWaves[slot].Active) RemoveBlastWaveOrder(slot);
            _blastWaves[slot] = new BlastWaveState
            {
                Active = true,
                Position = position,
                MaxRadius = Mathf.Max(0, maxRadius),
                Age = 0,
                Life = Mathf.Max(0.05f, life),
                Bomb = bomb,
                View = slot,
            };
            AppendBlastWaveOrder(slot);
            EnsureBlastWaveViews(slot);
            _blastWaveFillViews[slot].enabled = true;
            _blastWaveRimViews[slot].enabled = true;
            _blastWaveArcViews[slot].enabled = true;
        }

        private void UpdateBlastWaves(float dt)
        {
            for (var index = 0; index < _blastWaves.Length; index++)
            {
                var wave = _blastWaves[index];
                if (!wave.Active) continue;
                wave.Age += dt;
                if (wave.Age >= wave.Life)
                {
                    wave.Active = false;
                    RemoveBlastWaveOrder(index);
                    Hide(_blastWaveFillViews[index]);
                    Hide(_blastWaveRimViews[index]);
                    Hide(_blastWaveArcViews[index]);
                }
                _blastWaves[index] = wave;
            }
        }

        private SpriteRenderer EnsureBossShieldFillView(int index)
        {
            if (_bossShieldFillViews[index] != null) return _bossShieldFillViews[index];
            _bossShieldFillViews[index] = CreateView("Boss Shield Fill_" + index, ProceduralSpriteFactory.Circle(), 24);
            return _bossShieldFillViews[index];
        }

        private static float BossShieldVisualRadius(float bossRadius)
        {
            return bossRadius + 13f;
        }

        private static float BossShieldVisualDiameter(float bossRadius)
        {
            return BossShieldVisualRadius(bossRadius) * 2f;
        }

        private static Color BossShieldVisualColor()
        {
            return new Color(52f / 255f, 211f / 255f, 153f / 255f, 0.13f);
        }

        private static Material ResolveBlastWaveScreenMaterial()
        {
            return VoidFallRenderMaterials.ScreenBlend;
        }

        /// <summary>
        /// The plain alpha-blended sprite material, i.e. what a fresh
        /// SpriteRenderer starts with.
        ///
        /// Needed because hostile shot views are pooled. A slot that last
        /// carried a meteor-owned shot still has the additive material bound, so
        /// reusing it for an ordinary shot has to put a real material back.
        /// Clearing it to null instead leaves the renderer with no material at
        /// all, and a SpriteRenderer with no material silently submits no draw:
        /// the shot still moves and still damages the player, it just cannot be
        /// seen. That was why gunner fire was invisible.
        /// </summary>
        private static Material ResolveDefaultSpriteMaterial()
        {
            return VoidFallRenderMaterials.DefaultUnlit;
        }

        private static Material ResolveAdditiveSpriteMaterial()
        {
            return VoidFallRenderMaterials.AdditiveSprite;
        }

        private SpriteRenderer EnsurePickupView(int index)
        {
            if (_pickupViews[index] != null) return _pickupViews[index];
            _pickupViews[index] = CreateView("Pickup_" + index, ProceduralSpriteFactory.Gem(0), 10);
            return _pickupViews[index];
        }

        private SpriteRenderer EnsureBossView(int index)
        {
            if (_bossViews[index] != null) return _bossViews[index];
            _bossViews[index] = CreateView("Boss_" + index, ProceduralSpriteFactory.Circle(), 25);
            return _bossViews[index];
        }

        private static Sprite WeaponChipHudBackgroundSprite()
        {
            if (_weaponChipHudBackgroundSprite != null) return _weaponChipHudBackgroundSprite;
            var texture = RoundedGradientGuiTexture(
                Color.white,
                Color.white,
                Color.white,
                128,
                40,
                6f,
                "VoidFall Weapon Chip HUD Mask");
            _weaponChipHudBackgroundSprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                1f);
            _weaponChipHudBackgroundSprite.name = "VoidFall Weapon Chip HUD Mask Sprite";
            return _weaponChipHudBackgroundSprite;
        }

        private static Sprite WeaponChipHudBorderSprite()
        {
            if (_weaponChipHudBorderSprite != null) return _weaponChipHudBorderSprite;
            var texture = RoundedGradientGuiTexture(
                Color.clear,
                Color.clear,
                Color.white,
                128,
                40,
                6f,
                "VoidFall Weapon Chip HUD Border");
            _weaponChipHudBorderSprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                1f);
            _weaponChipHudBorderSprite.name = "VoidFall Weapon Chip HUD Border Sprite";
            return _weaponChipHudBorderSprite;
        }

        private static Sprite WeaponChipHudRankBackgroundSprite()
        {
            if (_weaponChipHudRankBackgroundSprite != null) return _weaponChipHudRankBackgroundSprite;
            var texture = RoundedGradientGuiTexture(
                Color.white,
                Color.white,
                Color.clear,
                18,
                18,
                3f,
                "VoidFall Weapon Chip HUD Rank Background");
            _weaponChipHudRankBackgroundSprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                1f);
            _weaponChipHudRankBackgroundSprite.name = "VoidFall Weapon Chip HUD Rank Background Sprite";
            return _weaponChipHudRankBackgroundSprite;
        }

        private void UpdateArenaDecor(float dt, bool reducedMotion)
        {
            var cycle = ArenaCycleRules.At(ArenaIdName(_arenaId), ArenaCycleElapsedSeconds());
            var visual = ArenaCycleVisual(cycle.CycleId, (float)cycle.Progress);
            var step = Mathf.Clamp(dt, 0, 0.1f);
            var motion = reducedMotion ? 0.25f : 1f;
            if (_mainMenuBrowsing && !reducedMotion) motion *= MenuVoidMotionSpeed;
            _arenaDecorClock += step * motion;

            var angle = ArenaMoteAngle(_arenaId) + visual.Current * 0.35f;
            var speed = ArenaMoteSpeed(_arenaId) * (0.35f + visual.Current * 0.9f) * motion;
            _arenaDecorDrift += new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed * step;
            _arenaDecorDrift.x = Mathf.Repeat(_arenaDecorDrift.x, ArenaDecorField);
            _arenaDecorDrift.y = Mathf.Repeat(_arenaDecorDrift.y, ArenaDecorField);
        }

        private void UpdateArenaCycleFlash(float dt)
        {
            var cycle = ArenaCycleRules.At(ArenaIdName(_arenaId), ArenaCycleElapsedSeconds());
            var flashRate = ArenaCycleFlashRate(cycle.ArenaId, cycle.CycleId);
            _arenaFlash = Mathf.Max(0, _arenaFlash - dt * 1.9f);
            if (flashRate <= 0) return;
            _arenaFlashT -= dt;
            if (_arenaFlashT > 0) return;

            _arenaFlashT = 1.2f + (float)_fxSim.FxRng.Next() * (2.2f / flashRate);
            var reducedMotion = _saveData?.settings != null && _saveData.settings.reducedMotion;
            if (!reducedMotion) _arenaFlash = 0.4f + (float)_fxSim.FxRng.Next() * 0.35f;
        }

        private void UpdateTransitionOverlay()
        {
            if (_transitionOverlay == null) return;
            var reducedMotion = _saveData?.settings != null && _saveData.settings.reducedMotion;
            var accent = ParseColor(
                _arenaTransitionState.Incoming.HasValue
                    ? FindArena(ArenaIdName(_arenaTransitionState.Incoming.Value))?.StarTint
                    : null,
                new Color(0.3f, 0.9f, 1f, 1));
            _transitionOverlay.SetState(
                _arenaTransitionState.Phase,
                _arenaTransitionState.PhaseT,
                _arenaTransitionState.Index,
                _time,
                accent,
                reducedMotion,
                _qualityPreset.Detail);
        }

        private static EnemyDefinition FindEnemy(string id)
        {
            foreach (var definition in ContentCatalog.Enemies) if (definition.Id == id) return definition;
            return null;
        }

        private static BossDefinition FindBoss(string id)
        {
            foreach (var definition in ContentCatalog.Bosses) if (definition.Id == id) return definition;
            return null;
        }

        private double ActiveEnemyThreat()
        {
            var total = 0.0;
            foreach (var enemy in _gameSim.Enemies)
            {
                if (!enemy.Active) continue;
                total += enemy.Elite && !enemy.EliteKind.HasValue
                    ? 8
                    : enemy.EliteKind.HasValue
                    ? EliteRules.EliteVariantDef(enemy.EliteKind.Value).ThreatCost
                    : DirectorRules.EnemyThreatCost(enemy.Id) *
                        (enemy.Roster == EnemyRoster.Two ? EnemyRosterRules.RosterTwoThreatMultiplier : 1);
            }

            return total;
        }

        private int ActiveEliteVariantCount(EliteVariantId id)
        {
            var count = 0;
            foreach (var enemy in _gameSim.Enemies)
            {
                if (enemy.Active && enemy.EliteKind == id) count++;
            }

            return count;
        }

        private int ActiveEliteVariantTotal()
        {
            var count = 0;
            foreach (var enemy in _gameSim.Enemies)
            {
                if (enemy.Active && enemy.EliteKind.HasValue) count++;
            }

            return count;
        }

        private int ActiveRosterTwoTotal()
        {
            var count = 0;
            foreach (var enemy in _gameSim.Enemies)
            {
                if (enemy.Active && enemy.Roster == EnemyRoster.Two) count++;
            }

            return count;
        }

        private int ActiveBosses() => _gameSim.ActiveBosses();
        private int ActiveBullets() => _gameSim.ActiveBullets();
        private int ActivePickups() => _gameSim.ActivePickups();
        private float XpOnGround()
        {
            var total = 0f;
            foreach (var pickup in _gameSim.Pickups)
                if (pickup.Active && pickup.Kind == PickupKind.Xp) total += Mathf.Max(0, pickup.Value);
            return total;
        }

        private int ActiveMeteors() => _gameSim.ActiveMeteors();
    }
}
