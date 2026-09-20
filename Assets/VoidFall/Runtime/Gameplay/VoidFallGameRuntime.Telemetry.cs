using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        // Observation sidecars never participate in combat RNG, hashes or pool order.
        private bool _runExportActive;
        private string _runExportDirectoryOverride;
        private string _runExportStatus;
        private string _runExportLastError;
        private double _runExportStartedRealtime;
        private float _runExportCheckpointTimer;
        private readonly int[] _telemetryEnemyIds = new int[MaxEnemies];
        private readonly float[] _telemetryFirstHit = new float[MaxEnemies];
        private readonly float[] _telemetryEnemyDamage = new float[MaxEnemies];
        private readonly float[] _telemetryRivalDamage = new float[MaxEnemies];
        private readonly int[] _telemetryPickupIds = new int[MaxPickupSlots];
        private int _telemetryNextPickupId;
        private readonly double[] _telemetryWeaponDamage = new double[ContentOrder.Weapons.Length];
        private string _telemetryLastFlow, _telemetryLastSpawnReason;
        private long _telemetryFlowSignature = long.MinValue;
        private int _telemetryLastOverclock = -1;
        private string _telemetryRewardSource;
        private int _telemetryRewardParent;
        private Action<int, float> _pickupAbsorbedTelemetryHook;
        private double _cpuSimulationMs, _cpuRenderMs, _cpuHudMs;
        private double _cpuSimulationSum, _cpuRenderSum, _cpuHudSum, _cpuUpdateSum, _cpuUpdateMax;
        private int _cpuFrames, _cpuGen0Start;

        private void RecordPerformancePhase(string phase, double milliseconds)
        {
            if (!_runExportActive || _paused || _mainMenuBrowsing || _gameOver || JourneyStopsCombat) return;
            if (phase == "simulation") _cpuSimulationMs = milliseconds;
            else if (phase == "render") _cpuRenderMs = milliseconds;
            else if (phase == "hud") _cpuHudMs = milliseconds;
            else if (phase == "update-total")
            {
                _cpuSimulationSum += _cpuSimulationMs;
                _cpuRenderSum += _cpuRenderMs;
                _cpuHudSum += _cpuHudMs;
                _cpuUpdateSum += milliseconds;
                _cpuUpdateMax = Math.Max(_cpuUpdateMax, milliseconds);
                _cpuFrames++;
            }
        }

        private void ResetPerformanceWindow()
        {
            _cpuSimulationSum = _cpuRenderSum = _cpuHudSum = _cpuUpdateSum = _cpuUpdateMax = 0;
            _cpuFrames = 0;
            _cpuGen0Start = GC.CollectionCount(0);
        }

        private UnityTelemetryCpuSample ConsumePerformanceWindow()
        {
            var divisor = Math.Max(1, _cpuFrames);
            var sample = new UnityTelemetryCpuSample
            {
                frames = _cpuFrames,
                simulationMeanMs = _cpuSimulationSum / divisor,
                renderMeanMs = _cpuRenderSum / divisor,
                hudMeanMs = _cpuHudSum / divisor,
                updateMeanMs = _cpuUpdateSum / divisor,
                updateMaxMs = _cpuUpdateMax,
                gen0Collections = Math.Max(0, GC.CollectionCount(0) - _cpuGen0Start),
                managedBytes = GC.GetTotalMemory(false)
            };
            ResetPerformanceWindow();
            return sample;
        }

        private void BeginRunExport(bool realRun, bool diagnostic)
        {
            _runExportActive = false;
            ResetPerformanceWindow();
            _runExportStatus = null;
            _runExportLastError = null;
            if (!realRun) return;
            var directory = _runExportDirectoryOverride;
            var testRunner = false;
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (argument.Equals("-runTests", StringComparison.OrdinalIgnoreCase)) testRunner = true;
                if (argument.StartsWith("-vfprofile=", StringComparison.OrdinalIgnoreCase) ||
                    argument.StartsWith("-vfrestoration-check=", StringComparison.OrdinalIgnoreCase)) diagnostic = true;
                if (argument.StartsWith("-vfrunexports=", StringComparison.OrdinalIgnoreCase))
                    directory = argument.Substring("-vfrunexports=".Length);
            }
            // Other suites must not produce player runs or touch real exports.
            if (testRunner && string.IsNullOrEmpty(directory)) return;
            var workshop = new List<UnityTelemetryNamedValue>();
            if (_saveData?.workshop != null)
                foreach (var entry in _saveData.workshop)
                    if (entry != null) workshop.Add(new UnityTelemetryNamedValue { id = entry.id, value = entry.rank });
            _telemetry.ConfigureHistory(directory, new UnityTelemetryContext
            {
                frameTimingVersion = 2,
                mapPresentationVersion = ApprovedMapRules.Version,
                buildVersion = Application.version,
                buildGuid = Application.buildGUID,
                unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(),
                cpu = SystemInfo.processorType,
                gpu = SystemInfo.graphicsDeviceName,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                graphicsDeviceVersion = SystemInfo.graphicsDeviceVersion,
                fullscreenMode = Screen.fullScreenMode.ToString(),
                windowWidth = Screen.width,
                windowHeight = Screen.height,
                runInBackground = Application.runInBackground,
                systemMemoryMb = SystemInfo.systemMemorySize,
                graphicsMemoryMb = SystemInfo.graphicsMemorySize,
                directorId = (int)_runDirectorProfile,
                formId = _formId,
                startingWeaponId = PlayerForms.StartingWeapon(_formId),
                startingProgress = BuildTelemetryProgress(),
                baseWeaponSlots = ProgressionRules.BaseWeaponSlots,
                expandedWeaponSlots = ProgressionRules.ExpandedWeaponSlots,
                maxedWeaponsForExtraSlot = ProgressionRules.MaxedWeaponsForExtraSlot,
                arsenalBalanceVersion = "2026-09-20-approved-weapons-v1",
                restorationVersion = LegacyRestorationRules.Version,
                arrivalRateMultiplier = LegacyRestorationRules.ArrivalRateMultiplier,
                ordinaryRareDropChance = LegacyRestorationRules.OrdinaryRareDropChance,
                overclockMaximumBankedSeconds = OverclockRules.MaximumBankedSeconds,
                xpMultiplierAfterLevelFive = 1.25,
                boomerangSizeScale = ArsenalContent.BoomerangSizeScale,
                clockFaceOpacity = ArsenalContent.ClockFaceOpacity,
                selectedMonitorIndex = _saveData?.settings?.monitorIndex ?? -1,
                actualMonitorName = Screen.mainWindowDisplayInfo.name,
                startingPressureHundredths = LegacyRestorationRules.StartingPressureHundredths,
                spikyBaseRadius = LegacyRestorationRules.SpikyBaseRadius,
                spikyExpandedScale = LegacyRestorationRules.SpikyExpandedScale,
                spikyPhaseSeconds = LegacyRestorationRules.SpikyPhaseSeconds,
                shurikenSpinRadians = LegacyRestorationRules.ShurikenSpinRadians,
                swarmIntervalSeconds = LegacyRestorationRules.SwarmIntervalSeconds,
                escapeSeconds = EscapeDurationSeconds,
                arrivalGraceSeconds = 2.5f,
                incidentBalanceVersion = 2,
                enemyCapacity = MaxEnemies,
                initialPopulationLimit = DirectorBodyLimit(),
                directorVersion = UsesSustainedDirector ? SustainedDirectorVersion : 1,
                lootPolicyVersion = LootPolicyVersion,
                pickupCapacity = MaxPickupSlots,
                reservedSpecialPickupSlots = ReservedSpecialPickupSlots,
                survivalSeconds = (float)VoidProgressionRules.SurvivalSeconds,
                workshopRanks = workshop.ToArray(),
                captureKind = testRunner ? "test" : diagnostic ? "diagnostic" : "play",
            });
            _runExportActive = true;
            _runExportStartedRealtime = Time.realtimeSinceStartupAsDouble;
            _runExportCheckpointTimer = 30f;
            _telemetrySampleTimer = 1f;
            _telemetryLastFlow = _telemetryLastSpawnReason = null;
            _telemetryFlowSignature = long.MinValue;
            _telemetryLastOverclock = -1;
            _telemetryRewardSource = null;
            _telemetryRewardParent = 0;
            Array.Clear(_telemetryEnemyIds, 0, _telemetryEnemyIds.Length);
            Array.Clear(_telemetryEnemyDamage, 0, _telemetryEnemyDamage.Length);
            Array.Clear(_telemetryRivalDamage, 0, _telemetryRivalDamage.Length);
            Array.Clear(_telemetryPickupIds, 0, _telemetryPickupIds.Length);
            Array.Clear(_telemetryWeaponDamage, 0, _telemetryWeaponDamage.Length);
            _telemetryNextPickupId = 0;
            ObserveRunExportState();
            RecordRunHistory("run_start", reason: diagnostic ? "diagnostic" : "player", progress: BuildTelemetryProgress());
            ExportTelemetrySnapshot("active");
        }

        private void RecordRunHistory(string kind, string id = null, string reason = null,
            string sourceId = null, int instanceId = 0, int relatedInstanceId = 0,
            float amount = 0, float hp = 0, float maxHp = 0, string detail = null,
            UnityTelemetryProgress progress = null, string[] options = null, float durationSeconds = 0,
            int budgetLimit = -1, int budgetUsed = -1, int blockedAttempts = -1, Vector2? position = null)
        {
            if (!_runExportActive) return;
            _telemetry.SetHistoryContext((float)(Time.realtimeSinceStartupAsDouble - _runExportStartedRealtime),
                _level, ArenaIdName(_arenaId), _pressureStageIndex + 1, PressureHundredths,
                (int)_runDirectorProfile, DirectorChallengeSeconds);
            _telemetry.RecordHistory(new UnityTelemetryHistoryEvent
            {
                timeSeconds = _time,
                wallTimeSeconds = (float)(Time.realtimeSinceStartupAsDouble - _runExportStartedRealtime),
                level = _level,
                kind = kind, id = id, reason = reason, sourceId = sourceId,
                instanceId = instanceId, relatedInstanceId = relatedInstanceId,
                amount = amount, hp = hp, maxHp = maxHp, detail = detail,
                progress = progress, options = options, durationSeconds = durationSeconds,
                budgetLimit = budgetLimit, budgetUsed = budgetUsed, blockedAttempts = blockedAttempts,
                arenaId = ArenaIdName(_arenaId), visitIndex = _pressureStageIndex + 1,
                pressureHundredths = PressureHundredths, directorId = (int)_runDirectorProfile,
                challengeSeconds = DirectorChallengeSeconds,
                activeEnemies = _gameSim.EnemyOrderCount,
                x = (position ?? _gameSim.Player.Position).x, y = (position ?? _gameSim.Player.Position).y,
            });
        }

        private void FinishRunExport(string status)
        {
            if (!_runExportActive) return;
            ObserveRunExportState();
            FlushCombatTelemetry();
            RecordTelemetrySample(Mathf.Max(.0001f, _debugFrameEmaMs / 1000f));
            RecordRunHistory("run_end", reason: status, progress: BuildTelemetryProgress());
            _runExportStatus = status;
            _telemetry.CloseHistory();
            ExportTelemetrySnapshot(status);
            _runExportActive = false;
        }

        private void UpdateRunExport(float frameDt)
        {
            if (!_runExportActive) return;
            ObserveRunExportState();
            _runExportCheckpointTimer -= Mathf.Max(0, frameDt);
            if (_runExportCheckpointTimer > 0) return;
            _runExportCheckpointTimer = 30f;
            _telemetry.FlushHistory();
            ExportTelemetrySnapshot("active");
        }

        private void ObserveRunExportState()
        {
            if (!_runExportActive) return;
            _telemetry.SetHistoryContext((float)(Time.realtimeSinceStartupAsDouble - _runExportStartedRealtime),
                _level, ArenaIdName(_arenaId), _pressureStageIndex + 1, PressureHundredths,
                (int)_runDirectorProfile, DirectorChallengeSeconds);
            var signature = ((long)_journeyStage << 32) | ((long)_encounter.Phase << 24) |
                ((long)_majorIncident.Kind << 16) | ((long)_majorIncident.Phase << 8) |
                (_paused ? 2L : 0L) | (_applicationInactive ? 1L : 0L);
            if (signature != _telemetryFlowSignature)
            {
                var flow = _journeyStage + "/" + _encounter.Phase + "/" + _majorIncident.Kind + "/" +
                    _majorIncident.Phase + "/" + (_paused ? "paused" : "playing") + "/" +
                    (_applicationInactive ? "unfocused" : "focused");
                RecordRunHistory("flow_state", flow, reason: _telemetryLastFlow);
                _telemetryLastFlow = flow;
                _telemetryFlowSignature = signature;
            }
            if (_lastSpawnBlockReason != _telemetryLastSpawnReason)
            {
                RecordRunHistory("spawn_gate", reason: _lastSpawnBlockReason ?? "open");
                _telemetryLastSpawnReason = _lastSpawnBlockReason;
            }
            if (_telemetryLastOverclock != _overclock.Streak)
            {
                RecordRunHistory("overclock", amount: _overclock.Streak, durationSeconds: _overclock.RemainingSeconds,
                    detail: "powerTier=" + _overclock.PowerTier + ";bankLimit=" + OverclockRules.MaximumBankedSeconds);
                _telemetryLastOverclock = _overclock.Streak;
            }
        }

        private void RecordEnemySpawn(int slot)
        {
            if (!_runExportActive) return;
            var enemy = _gameSim.Enemies[slot];
            _telemetryEnemyIds[slot] = enemy.SpawnId;
            _telemetryFirstHit[slot] = -1;
            _telemetryEnemyDamage[slot] = _telemetryRivalDamage[slot] = 0;
            var evt = EnemyHistory("enemy_spawn", enemy);
            evt.amount = enemy.Xp;
            evt.reason = enemy.SummonedByBossTelemetryId != 0 ? "boss_summon" :
                enemy.CarrierDrone ? "carrier_drone" : enemy.SplitterFragment ? "splitter_fragment" :
                _encounter.Phase == CombatEncounterPhase.Deployment ? "encounter" :
                CurrentVoidIsNullCity || CurrentVoidIsMonochrome ? "native_arena" : "ambient";
            evt.relatedInstanceId = enemy.SummonedByBossTelemetryId != 0 ? enemy.SummonedByBossTelemetryId : _factionControllerIdentity;
            evt.detail = "mutation=" + enemy.MutationGene + ";shield=" + enemy.Shield.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _telemetry.RecordHistory(evt);
        }

        private UnityTelemetryHistoryEvent EnemyHistory(string kind, EnemyState enemy)
        {
            return new UnityTelemetryHistoryEvent
            {
                kind = kind, id = enemy.Id, instanceId = enemy.SpawnId,
                timeSeconds = _time, wallTimeSeconds = (float)(Time.realtimeSinceStartupAsDouble - _runExportStartedRealtime),
                level = _level, arenaId = ArenaIdName(_arenaId), visitIndex = _pressureStageIndex + 1,
                pressureHundredths = PressureHundredths, directorId = (int)_runDirectorProfile,
                challengeSeconds = DirectorChallengeSeconds, activeEnemies = _gameSim.EnemyOrderCount,
                hp = enemy.Health, maxHp = enemy.MaxHealth, speed = enemy.Speed, damage = enemy.Damage,
                x = enemy.Position.x, y = enemy.Position.y, rosterTier = (int)enemy.Roster,
                elite = enemy.Elite, sourceId = FactionOf(enemy).ToString(),
            };
        }

        private void RecordEnemyDamage(int slot, EnemyState enemy, float applied)
        {
            if (!_runExportActive || applied <= 0 || _telemetryEnemyIds[slot] != enemy.SpawnId) return;
            if (_telemetryFirstHit[slot] < 0)
            {
                _telemetryFirstHit[slot] = _time;
                var evt = EnemyHistory("enemy_first_hit", enemy);
                evt.sourceId = _damageFaction.ToString();
                _telemetry.RecordHistory(evt);
            }
            if (_damageFaction == CombatFaction.Player) _telemetryEnemyDamage[slot] += applied;
            else _telemetryRivalDamage[slot] += applied;
        }

        private void FlushEnemyDamage(int slot, EnemyState enemy)
        {
            for (var faction = 0; faction < 2; faction++)
            {
                var damage = faction == 0 ? _telemetryEnemyDamage[slot] : _telemetryRivalDamage[slot];
                if (damage <= 0) continue;
                var evt = EnemyHistory("enemy_damage_window", enemy);
                evt.amount = damage;
                evt.sourceId = faction == 0 ? "Player" : "NonPlayer";
                _telemetry.RecordHistory(evt);
            }
            _telemetryEnemyDamage[slot] = _telemetryRivalDamage[slot] = 0;
        }

        private void RecordEnemyRemoval(int slot, string reason)
        {
            FlushSpikyGrowth(slot);
            if (!_runExportActive || _telemetryEnemyIds[slot] == 0) return;
            var enemy = _gameSim.Enemies[slot];
            if (_telemetryEnemyIds[slot] != enemy.SpawnId) return;
            FlushEnemyDamage(slot, enemy);
            var evt = EnemyHistory(reason == "killed" || reason == "self_detonated" ? "enemy_death" : "enemy_despawn", enemy);
            evt.reason = reason;
            evt.sourceId = _damageFaction.ToString();
            evt.durationSeconds = _telemetryFirstHit[slot] < 0 ? -1 : _time - _telemetryFirstHit[slot];
            _telemetry.RecordHistory(evt);
            _telemetryEnemyIds[slot] = 0;
        }

        private void FlushCombatTelemetry()
        {
            if (!_runExportActive) return;
            for (var i = 0; i < _gameSim.Enemies.Length; i++)
                if (_telemetryEnemyIds[i] != 0 && _telemetryEnemyIds[i] == _gameSim.Enemies[i].SpawnId)
                    FlushEnemyDamage(i, _gameSim.Enemies[i]);
            for (var i = 0; i < _gameSim.Bosses.Length; i++)
            {
                var boss = _gameSim.Bosses[i];
                if (!boss.Active) continue;
                RecordRunHistory("boss_state", boss.Id, instanceId: boss.TelemetryInstanceId,
                    hp: boss.Health, maxHp: boss.MaxHealth, amount: boss.State,
                    detail: boss.ActiveAttack?.Id);
            }
            FlushLegendaryTelemetry();
            for (var i = 0; i < _spikyGrowth.Length; i++) FlushSpikyGrowth(i);
            for (var i = 0; i < _weaponDamage.Length; i++)
            {
                var delta = _weaponDamage[i] - _telemetryWeaponDamage[i];
                if (delta > 0) RecordRunHistory("weapon_damage_window", ContentCatalog.Weapons[i].Id, amount: (float)delta);
                _telemetryWeaponDamage[i] = _weaponDamage[i];
            }
        }

        private void RecordPickupHistory(string kind, int slot, PickupState pickup, float amount = -1, string reason = null, int sourcePickupId = 0)
        {
            if (!_runExportActive) return;
            if (kind == "drop_spawn") _telemetryPickupIds[slot] = ++_telemetryNextPickupId;
            var evt = new UnityTelemetryHistoryEvent
            {
                kind = kind, id = PickupKindName(pickup.Kind), instanceId = _telemetryPickupIds[slot],
                reason = reason,
                relatedInstanceId = sourcePickupId > 0 ? sourcePickupId : kind == "drop_absorbed" ? _factionControllerIdentity : _telemetryRewardParent,
                sourceId = sourcePickupId > 0 ? "pickup" : kind == "drop_absorbed" ? "harvester" : _telemetryRewardSource ?? "world",
                timeSeconds = _time, wallTimeSeconds = (float)(Time.realtimeSinceStartupAsDouble - _runExportStartedRealtime),
                level = _level, arenaId = ArenaIdName(_arenaId), visitIndex = _pressureStageIndex + 1,
                pressureHundredths = PressureHundredths, directorId = (int)_runDirectorProfile,
                amount = amount < 0 ? pickup.Value : amount, x = pickup.Position.x, y = pickup.Position.y,
            };
            _telemetry.RecordHistory(evt);
        }

        private void RecordUpgradeOffers(string reason)
        {
            if (!_runExportActive || _levelOptions == null) return;
            var options = Array.ConvertAll(_levelOptions, option => option.Id);
            RecordRunHistory("upgrade_offered", reason: reason, amount: _rerollsRemaining,
                options: options, progress: BuildTelemetryProgress());
        }
    }
}
