using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    [Serializable]
    public sealed class UnityTelemetrySample
    {
        public float timeSeconds;
        public int level;
        public float hp;
        public float maxHp;
        public int enemies;
        public int activeBosses;
        public int projectiles;
        public int pickups;
        public float xpOnGround;
        public float xpHeldByHarvesters;
        public float fps;
        public float frameMs;
        public float quality;
        public float hpMultiplier;
        public float speedMultiplier;
        public float damageMultiplier;
        public string directorEvent;
        public string arenaId;
        public string arenaPhase;
        public int activeEliteVariants;
        public int meteors;
        public int pressureHundredths;
        public float challengeSeconds;
        public int directorId;
        public string encounterPhase;
        public string encounterKind;
        public string spawnReason;
        public float playerX;
        public float playerY;
        public float viewportWidth;
        public float viewportHeight;
        public float arenaWidth;
        public float arenaHeight;
        public int xp;
        public long baseScore;
        public float nearestEnemyDistance;
        public int onScreenEnemies;
        public int specialAttackLimit;
        public int committedSpecialAttacks;
        public int hostileProjectiles;
        public int xpPickupCount;
        public int specialPickupCount;
        public int distantLootCount;
        public float localSurvivalSeconds;
        public float survivalRemainingSeconds;
        public float bossDifficultySeconds;
    }

    [Serializable]
    public sealed class UnityTelemetryContext
    {
        public string buildVersion;
        public string unityVersion;
        public string buildGuid;
        public string platform;
        public string cpu;
        public string gpu;
        public string graphicsApi;
        public string graphicsDeviceVersion;
        public string fullscreenMode;
        public int windowWidth;
        public int windowHeight;
        public bool runInBackground;
        public int systemMemoryMb;
        public int graphicsMemoryMb;
        public int directorId;
        public string formId;
        public string startingWeaponId;
        public UnityTelemetryProgress startingProgress;
        public int baseWeaponSlots;
        public int expandedWeaponSlots;
        public int maxedWeaponsForExtraSlot;
        public string arsenalBalanceVersion;
        public string restorationVersion;
        public float arrivalRateMultiplier;
        public double ordinaryRareDropChance, overclockMaximumBankedSeconds, xpMultiplierAfterLevelFive, boomerangSizeScale, clockFaceOpacity;
        public int selectedMonitorIndex;
        public string actualMonitorName;
        public int startingPressureHundredths;
        public float spikyBaseRadius, spikyExpandedScale, spikyPhaseSeconds, shurikenSpinRadians, swarmIntervalSeconds;
        public float escapeSeconds;
        public float arrivalGraceSeconds;
        public int incidentBalanceVersion;
        public int enemyCapacity;
        public int initialPopulationLimit;
        public int directorVersion;
        public int lootPolicyVersion;
        public int pickupCapacity;
        public int reservedSpecialPickupSlots;
        public float survivalSeconds;
        public UnityTelemetryNamedValue[] workshopRanks;
        public string captureKind;
    }

    [Serializable]
    public sealed class UnityTelemetryHistoryEvent
    {
        public long sequence;
        public float timeSeconds;
        public float wallTimeSeconds = -1;
        public string kind;
        public string id;
        public string reason;
        public string arenaId;
        public int visitIndex = -1;
        public int encounterIndex = -1;
        public int transitionIndex = -1;
        public int pressureHundredths = -1;
        public int directorId = -1;
        public int instanceId;
        public int relatedInstanceId;
        public string sourceId;
        public float amount;
        public float hp;
        public float maxHp;
        public float speed;
        public float damage;
        public float x;
        public float y;
        public int rosterTier;
        public bool elite;
        public int activeEnemies;
        public float challengeSeconds = -1;
        public UnityTelemetryProgress progress;
        public string[] options;
        public string detail;
        public float durationSeconds;
        public int budgetLimit = -1;
        public int budgetUsed = -1;
        public int blockedAttempts = -1;
        public int level;
        public int nextXp;
        public int bufferedXp;
        public UnityTelemetrySample sample;
        public UnityTelemetryContext context;
    }

    [Serializable]
    public sealed class UnityTelemetryHistoryInfo
    {
        public string file;
        public string format = "jsonl";
        public int schemaVersion = 4;
        public long submitted;
        public long written;
        public long flushed;
        public long dropped;
        public int pending;
        public int queueCapacity;
        public int maximumQueuedCharacters;
        public bool closed;
        public bool drainTimedOut;
        public int errorCount;
        public string lastError;
    }

    [Serializable]
    public sealed class UnityTelemetryNamedValue
    {
        public string id;
        public int value;
    }

    [Serializable]
    public sealed class UnityTelemetryDamageValue
    {
        public string id;
        public long value;
    }

    [Serializable]
    public sealed class UnityTelemetryProgress
    {
        public int weaponSlotLimit;
        public UnityTelemetryNamedValue[] weapons;
        public UnityTelemetryNamedValue[] supports;
        public UnityTelemetryNamedValue[] late;
        public string[] evolved;
        public string legendaryId;
        public int legendaryRank, soundBladeFragments, chargedRifleFragments;
        public float dealerShield, delayedPowerCombatSeconds;
        public int dealerExtraWeapon = -1, dealerHealthBonus, dealerRecoveryCharges;
    }

    [Serializable]
    public sealed class UnityTelemetryEvent
    {
        public float timeSeconds;
        public int level;
        public string id;
        public string kind;
        public int value;
        public int nextXp;
        public int bufferedXp;
        public UnityTelemetryProgress progress;
    }

    [Serializable]
    public sealed class UnityTelemetryPerformance
    {
        public int framesObserved;
        public float minimumFps;
        public float maximumFrameMs;
        public int maximumEnemies;
        public int maximumProjectiles;
        public int maximumPickups;
        public float meanFrameMs;
        public float p50FrameMs;
        public float p95FrameMs;
        public float p99FrameMs;
        public int framesOver16_67Ms;
        public int framesOver33_33Ms;
        public int framesOver50Ms;
        public float histogramResolutionMs = 0.25f;
        public float histogramMaximumMs = 1000f;
    }

    [Serializable]
    public sealed class UnityTelemetrySummary
    {
        public string status;
        public bool scoreIsFinal;
        public float timeSeconds;
        public int score;
        public long baseScore;
        public int pressureHundredths;
        public int multiplierHundredths;
        public long finalScore;
        public int scoringVersion;
        public int directorId;
        public int kills;
        public int eliteKills;
        public int bossKills;
        public int level;
        public long damageDealt;
        public long damageTaken;
        public long unattributedDamage;
        public int partsEarned;
        public int activeBosses;
        public int enemies;
        public int pickups;
        public int xpOnGround;
        public float xpHeldByHarvesters;
        public UnityTelemetryProgress progress;
        public UnityTelemetryDamageValue[] weaponDamage;
    }

    [Serializable]
    public sealed class UnityTelemetryExperience
    {
        public int released;
        public int collected;
        public int absorbedByHarvesters;
        public int onGround;
        public float heldByHarvesters;
        public int accountingGap;
    }

    [Serializable]
    public sealed class UnityTelemetryBossEvent
    {
        public string id;
        public int instanceId;
        public int encounterIndex;
        public float spawnedAtSeconds;
        public float defeatedAtSeconds = -1;
        public float fightSeconds = -1;
        public float maxHp;
        public int activeBossesOnSpawn;
    }

    [Serializable]
    public sealed class UnityTelemetryPickupCount
    {
        public string id;
        public int count;
        public float totalValue;
    }

    [Serializable]
    public sealed class UnityTelemetryArenaTransition
    {
        public int index;
        public string from;
        public string to;
        public float warnedAtSeconds;
        public float swappedAtSeconds = -1;
        public float completedAtSeconds = -1;
    }

    [Serializable]
    public sealed class UnityTelemetryArenaSummary
    {
        public string id;
        public float timeSeconds;
        public int framesObserved;
        public float minimumFps;
        public float maximumFrameMs;
        public int maximumEnemies;
        public int maximumProjectiles;
        public int maximumPickups;
        public UnityTelemetryNamedValue[] eliteSpawns;
        public UnityTelemetryNamedValue[] eliteKills;
        public UnityTelemetryRosterTwo rosterTwo;
        public UnityTelemetryMeteors meteors;
    }

    [Serializable]
    public sealed class UnityTelemetryRosterTwo
    {
        public int spawns;
        public int kills;
    }

    [Serializable]
    public sealed class UnityTelemetryMeteors
    {
        public int ordinaryDestroyed;
        public int explosiveArmed;
        public int explosiveDetonated;
        public int playerHits;
    }

    [Serializable]
    public sealed class UnityTelemetryArenas
    {
        public UnityTelemetryArenaTransition[] transitions;
        public int deferredTransitions;
        public UnityTelemetryArenaSummary[] byArena;
    }

    [Serializable]
    public sealed class UnityTelemetryDroppedRecords
    {
        public int samples;
        public int upgrades;
        public int bosses;
        public int arenaTransitions;
        public int milestones;
    }

    [Serializable]
    public sealed class UnityTelemetryProgression
    {
        public UnityTelemetryEvent[] levels;
        public UnityTelemetryEvent[] upgrades;
        public UnityTelemetryEvent[] milestones;
    }

    [Serializable]
    public sealed class UnityTelemetryReport
    {
        public int schemaVersion = 4;
        public string game = "VoidFall";
        public uint seed;
        public string startedAt;
        public string exportedAt;
        public string runId;
        public UnityTelemetryContext context;
        public UnityTelemetryHistoryInfo history;
        public UnityTelemetrySummary summary;
        public UnityTelemetryProgression progression;
        public UnityTelemetryExperience experience;
        public UnityTelemetryBossEvent[] bosses;
        public UnityTelemetryPickupCount[] pickupsCollected;
        public UnityTelemetryArenas arenas;
        public UnityTelemetryPerformance performance;
        public UnityTelemetrySample[] samples;
        public UnityTelemetryDroppedRecords droppedRecords;
    }

    /// <summary>
    /// Bounded summary recorder with an optional append-only run journal.
    /// Journal serialization snapshots DTOs on the caller thread; bounded queued
    /// strings are written by one background worker without gameplay access.
    /// </summary>
    public sealed class RunTelemetryRecorder
    {
        private const int MaxSamples = 2160;
        private const int MaxEvents = 2048;
        private readonly UnityTelemetrySample[] _samples = new UnityTelemetrySample[MaxSamples];
        private int _sampleHead;
        private int _sampleCount;
        private readonly List<UnityTelemetryEvent> _levels = new List<UnityTelemetryEvent>(64);
        private readonly List<UnityTelemetryEvent> _upgrades = new List<UnityTelemetryEvent>(128);
        private readonly List<UnityTelemetryEvent> _milestones = new List<UnityTelemetryEvent>(128);
        private readonly List<UnityTelemetryBossEvent> _bosses = new List<UnityTelemetryBossEvent>(16);
        private readonly List<UnityTelemetryPickupCount> _pickups = new List<UnityTelemetryPickupCount>(8);
        private readonly List<UnityTelemetryArenaTransition> _arenaTransitions = new List<UnityTelemetryArenaTransition>(8);
        private readonly List<UnityTelemetryArenaSummary> _arenas = new List<UnityTelemetryArenaSummary>(3);
        private uint _seed;
        private string _startedAt;
        private int _droppedRecords;
        private int _framesObserved;
        private float _minimumFps = float.PositiveInfinity;
        private float _maximumFrameMs;
        private int _maximumEnemies;
        private int _maximumProjectiles;
        private int _maximumPickups;
        private int _xpReleased;
        private int _xpCollected;
        private int _xpAbsorbedByHarvesters;
        private int _deferredArenaTransitions;
        private int _droppedSamples;
        private int _droppedUpgrades;
        private int _droppedBosses;
        private int _droppedArenaTransitions;
        private int _droppedMilestones;
        private HistoryWriter _history;
        private UnityTelemetryContext _context;
        private string _outputDirectory;
        private long _historySequence;
        private bool _hasHistoryContext;
        private float _historyWallTimeSeconds, _historyChallengeSeconds;
        private int _historyLevel, _historyVisitIndex, _historyPressureHundredths, _historyDirectorId;
        private string _historyArenaId;
        // Upper-bound histogram quantiles: 0.25 ms buckets, with a final overflow
        // bucket represented by the observed maximum. No per-frame allocations.
        private readonly int[] _frameHistogram = new int[4002];
        private double _totalFrameMs;
        private int _framesOver16_67Ms, _framesOver33_33Ms, _framesOver50Ms;
        public string RunId { get; private set; }
        public string LastExportError { get; private set; }
        public bool HistoryCaptureEnabled => _history != null && _history.Accepting;
        public UnityTelemetryHistoryInfo HistoryInfo => _history?.Snapshot();

        public static string DefaultExportDirectory => Path.Combine(Path.GetDirectoryName(Application.dataPath), "RunExports");

        public void SetHistoryContext(float wallTimeSeconds, int level, string arenaId, int visitIndex,
            int pressureHundredths, int directorId, float challengeSeconds)
        {
            _hasHistoryContext = true;
            _historyWallTimeSeconds = wallTimeSeconds;
            _historyLevel = level;
            _historyArenaId = arenaId;
            _historyVisitIndex = visitIndex;
            _historyPressureHundredths = pressureHundredths;
            _historyDirectorId = directorId;
            _historyChallengeSeconds = challengeSeconds;
        }

        public void ConfigureHistory(string outputDirectory, UnityTelemetryContext context)
        {
            CloseHistory();
            if (string.IsNullOrEmpty(RunId)) RunId = Guid.NewGuid().ToString("N");
            _outputDirectory = string.IsNullOrEmpty(outputDirectory) ? DefaultExportDirectory : outputDirectory;
            _context = context;
            _history = new HistoryWriter(Path.Combine(_outputDirectory, "voidfall-run-" + RunId + ".jsonl"));
            RecordHistory(new UnityTelemetryHistoryEvent { kind = "run_metadata", id = RunId, context = context });
        }

        // All serialization runs on the caller (Unity) thread. The worker owns only
        // immutable strings and filesystem handles, never Unity objects or live DTOs.
        public void RecordHistory(UnityTelemetryHistoryEvent value)
        {
            if (_history == null || value == null) return;
            if (_hasHistoryContext)
            {
                // Negative context fields mean unspecified; explicit zero remains
                // meaningful. Historical simulation time is never overwritten.
                if (value.wallTimeSeconds < 0) value.wallTimeSeconds = _historyWallTimeSeconds;
                if (value.level <= 0) value.level = _historyLevel;
                if (string.IsNullOrEmpty(value.arenaId)) value.arenaId = _historyArenaId;
                if (value.visitIndex < 0) value.visitIndex = _historyVisitIndex;
                if (value.pressureHundredths < 0) value.pressureHundredths = _historyPressureHundredths;
                if (value.directorId < 0) value.directorId = _historyDirectorId;
                if (value.challengeSeconds < 0) value.challengeSeconds = _historyChallengeSeconds;
            }
            value.sequence = ++_historySequence;
            if (_history.RejectIfUnavailable()) return;
            try { _history.Enqueue(JsonUtility.ToJson(value)); }
            catch (Exception exception) { _history.RecordRejected(exception.Message); }
        }

        public void FlushHistory() => _history?.RequestFlush();
        public void CloseHistory() => _history?.Close();

        public void Begin(uint seed)
        {
            CloseHistory();
            _history = null;
            _context = null;
            _outputDirectory = null;
            _historySequence = 0;
            _hasHistoryContext = false;
            Array.Clear(_frameHistogram, 0, _frameHistogram.Length);
            _totalFrameMs = 0;
            _framesOver16_67Ms = _framesOver33_33Ms = _framesOver50Ms = 0;
            LastExportError = null;
            RunId = Guid.NewGuid().ToString("N");
            _seed = seed;
            _startedAt = DateTime.UtcNow.ToString("O");
            _sampleHead = 0;
            _sampleCount = 0;
            _levels.Clear();
            _upgrades.Clear();
            _milestones.Clear();
            _bosses.Clear();
            _pickups.Clear();
            _arenaTransitions.Clear();
            _arenas.Clear();
            _droppedRecords = 0;
            _droppedSamples = 0;
            _droppedUpgrades = 0;
            _droppedBosses = 0;
            _droppedArenaTransitions = 0;
            _droppedMilestones = 0;
            _deferredArenaTransitions = 0;
            _xpReleased = 0;
            _xpCollected = 0;
            _xpAbsorbedByHarvesters = 0;
            _framesObserved = 0;
            _minimumFps = float.PositiveInfinity;
            _maximumFrameMs = 0;
            _maximumEnemies = 0;
            _maximumProjectiles = 0;
            _maximumPickups = 0;
        }

        public void RecordXpReleased(float value) => _xpReleased += Mathf.Max(0, Mathf.FloorToInt(value));

        public void RecordXpCollected(float value) => _xpCollected += Mathf.Max(0, Mathf.FloorToInt(value));

        public void RecordXpAbsorbedByHarvester(float value) => _xpAbsorbedByHarvesters += Mathf.Max(0, Mathf.FloorToInt(value));

        public void RecordPickup(string id, float value)
        {
            var safeId = string.IsNullOrEmpty(id) ? "unknown" : id;
            for (var index = 0; index < _pickups.Count; index++)
            {
                if (_pickups[index].id != safeId) continue;
                _pickups[index].count++;
                _pickups[index].totalValue += Mathf.Max(0, value);
                return;
            }
            _pickups.Add(new UnityTelemetryPickupCount
            {
                id = safeId,
                count = 1,
                totalValue = Mathf.Max(0, value),
            });
        }

        public void RecordBossSpawn(string id, int instanceId, int encounterIndex, float timeSeconds, float maxHp, int activeBosses)
        {
            RecordHistory(new UnityTelemetryHistoryEvent { kind = "boss_spawn", id = id, instanceId = instanceId,
                encounterIndex = encounterIndex, timeSeconds = timeSeconds, maxHp = maxHp, amount = activeBosses });
            if (_bosses.Count >= 512)
            {
                _droppedBosses++;
                return;
            }
            _bosses.Add(new UnityTelemetryBossEvent
            {
                id = id ?? "unknown",
                instanceId = instanceId,
                encounterIndex = encounterIndex,
                spawnedAtSeconds = BrowserRounded(timeSeconds),
                maxHp = BrowserRounded(maxHp),
                activeBossesOnSpawn = Mathf.Max(0, activeBosses),
            });
        }

        public void RecordBossDefeat(int instanceId, float timeSeconds)
        {
            RecordHistory(new UnityTelemetryHistoryEvent { kind = "boss_defeat", instanceId = instanceId, timeSeconds = timeSeconds });
            for (var index = _bosses.Count - 1; index >= 0; index--)
            {
                var boss = _bosses[index];
                if (boss.instanceId != instanceId || boss.defeatedAtSeconds >= 0) continue;
                boss.defeatedAtSeconds = BrowserRounded(timeSeconds);
                boss.fightSeconds = BrowserRounded(boss.defeatedAtSeconds - boss.spawnedAtSeconds);
                _bosses[index] = boss;
                return;
            }
        }

        public void RecordArenaWarning(int index, string from, string to, float timeSeconds)
        {
            RecordHistory(new UnityTelemetryHistoryEvent { kind = "arena_warning", transitionIndex = index,
                sourceId = from, id = to, timeSeconds = timeSeconds });
            if (_arenaTransitions.Count >= 64)
            {
                _droppedArenaTransitions++;
                return;
            }
            _arenaTransitions.Add(new UnityTelemetryArenaTransition
            {
                index = Mathf.Max(0, index),
                from = from ?? "void",
                to = to ?? "void",
                warnedAtSeconds = BrowserRounded(timeSeconds),
            });
        }

        public void RecordArenaSwap(int index, float timeSeconds)
        {
            RecordHistory(new UnityTelemetryHistoryEvent { kind = "arena_swap", transitionIndex = index, timeSeconds = timeSeconds });
            for (var cursor = _arenaTransitions.Count - 1; cursor >= 0; cursor--)
            {
                var transition = _arenaTransitions[cursor];
                if (transition.index != index || transition.swappedAtSeconds >= 0) continue;
                transition.swappedAtSeconds = BrowserRounded(timeSeconds);
                _arenaTransitions[cursor] = transition;
                return;
            }
        }

        public void RecordArenaComplete(int index, float timeSeconds)
        {
            RecordHistory(new UnityTelemetryHistoryEvent { kind = "arena_complete", transitionIndex = index, timeSeconds = timeSeconds });
            for (var cursor = _arenaTransitions.Count - 1; cursor >= 0; cursor--)
            {
                var transition = _arenaTransitions[cursor];
                if (transition.index != index || transition.completedAtSeconds >= 0) continue;
                transition.completedAtSeconds = BrowserRounded(timeSeconds);
                _arenaTransitions[cursor] = transition;
                return;
            }
        }

        public void RecordArenaDeferred() => _deferredArenaTransitions++;

        public void RecordEliteSpawn(string arenaId, string kind)
        {
            var arena = GetArenaSummary(arenaId);
            if (arena != null) arena.eliteSpawns = IncrementNamedValue(arena.eliteSpawns, kind);
        }

        public void RecordEliteKill(string arenaId, string kind)
        {
            var arena = GetArenaSummary(arenaId);
            if (arena != null) arena.eliteKills = IncrementNamedValue(arena.eliteKills, kind);
        }

        public void RecordRosterTwoSpawn(string arenaId)
        {
            var arena = GetArenaSummary(arenaId);
            if (arena != null) arena.rosterTwo.spawns++;
        }

        public void RecordRosterTwoKill(string arenaId)
        {
            var arena = GetArenaSummary(arenaId);
            if (arena != null) arena.rosterTwo.kills++;
        }

        public void RecordMeteorDestroyed(string arenaId, bool explosive)
        {
            var arena = GetArenaSummary(arenaId);
            if (arena == null) return;
            if (explosive) arena.meteors.explosiveArmed++;
            else arena.meteors.ordinaryDestroyed++;
        }

        public void RecordMeteorDetonated(string arenaId)
        {
            var arena = GetArenaSummary(arenaId);
            if (arena != null) arena.meteors.explosiveDetonated++;
        }

        public void RecordMeteorPlayerHit(string arenaId)
        {
            var arena = GetArenaSummary(arenaId);
            if (arena != null) arena.meteors.playerHits++;
        }

        public void RecordArenaFrame(string id, float seconds, float fps, float frameMs, int enemies, int projectiles, int pickups)
        {
            var arena = GetArenaSummary(id);
            if (arena == null)
            {
                return;
            }
            arena.timeSeconds += Mathf.Max(0, seconds);
            arena.framesObserved++;
            arena.minimumFps = Mathf.Min(arena.minimumFps, Mathf.Max(0, fps));
            arena.maximumFrameMs = Mathf.Max(arena.maximumFrameMs, Mathf.Max(0, frameMs));
            arena.maximumEnemies = Mathf.Max(arena.maximumEnemies, enemies);
            arena.maximumProjectiles = Mathf.Max(arena.maximumProjectiles, projectiles);
            arena.maximumPickups = Mathf.Max(arena.maximumPickups, pickups);
        }

        public void RecordArenaTime(string id, float seconds)
        {
            var arena = GetArenaSummary(id);
            if (arena != null) arena.timeSeconds += Mathf.Max(0, seconds);
        }

        public void ObserveFrame(float fps, float frameMs)
        {
            if (!IsFinite(fps) || !IsFinite(frameMs)) return;
            _framesObserved++;
            var duration = Mathf.Max(0, frameMs);
            _totalFrameMs += duration;
            _frameHistogram[duration >= 1000f ? 4001 : Mathf.CeilToInt(duration * 4f)]++;
            if (duration > 16.67f) _framesOver16_67Ms++;
            if (duration > 33.33f) _framesOver33_33Ms++;
            if (duration > 50f) _framesOver50Ms++;
            _minimumFps = Mathf.Min(_minimumFps, Mathf.Max(0, fps));
            _maximumFrameMs = Mathf.Max(_maximumFrameMs, Mathf.Max(0, frameMs));
        }

        public void ObserveFrame(
            string arenaId,
            float fps,
            float frameMs,
            int enemies,
            int projectiles,
            int pickups)
        {
            ObserveFrame(fps, frameMs);
            _maximumEnemies = Mathf.Max(_maximumEnemies, enemies);
            _maximumProjectiles = Mathf.Max(_maximumProjectiles, projectiles);
            _maximumPickups = Mathf.Max(_maximumPickups, pickups);
            var arena = GetArenaSummary(arenaId);
            if (arena == null) return;
            arena.framesObserved++;
            arena.minimumFps = Mathf.Min(arena.minimumFps, Mathf.Max(0, fps));
            arena.maximumFrameMs = Mathf.Max(arena.maximumFrameMs, Mathf.Max(0, frameMs));
            arena.maximumEnemies = Mathf.Max(arena.maximumEnemies, enemies);
            arena.maximumProjectiles = Mathf.Max(arena.maximumProjectiles, projectiles);
            arena.maximumPickups = Mathf.Max(arena.maximumPickups, pickups);
        }

        public void RecordSample(UnityTelemetrySample sample)
        {
            if (sample == null) return;
            var normalized = NormalizeSample(sample);
            RecordHistory(new UnityTelemetryHistoryEvent { kind = "sample", timeSeconds = normalized.timeSeconds,
                arenaId = normalized.arenaId, pressureHundredths = normalized.pressureHundredths,
                challengeSeconds = normalized.challengeSeconds, directorId = normalized.directorId, sample = normalized });
            _maximumEnemies = Mathf.Max(_maximumEnemies, sample.enemies);
            _maximumProjectiles = Mathf.Max(_maximumProjectiles, sample.projectiles);
            _maximumPickups = Mathf.Max(_maximumPickups, sample.pickups);
            if (_sampleCount >= MaxSamples)
            {
                // Browser RunTelemetry.shift() drops the oldest sample so the
                // exported window always contains the newest observations.
                _samples[_sampleHead] = normalized;
                _sampleHead = (_sampleHead + 1) % MaxSamples;
                _droppedRecords++;
                _droppedSamples++;
            }
            else
            {
                _samples[(_sampleHead + _sampleCount) % MaxSamples] = normalized;
                _sampleCount++;
            }
        }

        public void RecordLevel(float timeSeconds, int level, int nextXp, int bufferedXp)
        {
            AddEvent(_levels, new UnityTelemetryEvent
            {
                timeSeconds = BrowserRounded(timeSeconds),
                level = Mathf.Max(1, level),
                id = "level",
                kind = "level",
                value = Mathf.Max(0, nextXp),
                nextXp = Mathf.Max(0, nextXp),
                bufferedXp = Mathf.Max(0, bufferedXp),
            });
        }

        public void RecordUpgrade(float timeSeconds, int level, string id, string kind, UnityTelemetryProgress progress = null)
        {
            AddEvent(_upgrades, new UnityTelemetryEvent
            {
                timeSeconds = BrowserRounded(timeSeconds),
                level = Mathf.Max(1, level),
                id = id ?? "unknown",
                kind = kind ?? "unknown",
                value = 0,
                progress = progress ?? new UnityTelemetryProgress
                {
                    weapons = Array.Empty<UnityTelemetryNamedValue>(),
                    supports = Array.Empty<UnityTelemetryNamedValue>(),
                    late = Array.Empty<UnityTelemetryNamedValue>(),
                    evolved = Array.Empty<string>(),
                },
            });
        }

        public void RecordMilestone(float timeSeconds, string kind, int value)
        {
            AddEvent(_milestones, new UnityTelemetryEvent
            {
                timeSeconds = BrowserRounded(timeSeconds),
                level = 0,
                id = kind ?? "unknown",
                kind = "milestone",
                value = Mathf.Max(0, value),
            });
        }

        // With an active history worker this returns the queued checkpoint path.
        // CloseHistory before a terminal Export to synchronously commit the final
        // summary with the drained journal counters. Legacy callers remain synchronous.
        public string Export(
            string status,
            float timeSeconds,
            int score,
            int kills,
            int eliteKills,
            int bossKills,
            int level,
            long damageDealt,
            long damageTaken,
            int partsEarned,
            int activeBosses,
            int enemies,
            int pickups,
            UnityTelemetryProgress progress = null,
            UnityTelemetryDamageValue[] weaponDamage = null,
            int xpOnGround = 0,
            float xpHeldByHarvesters = 0,
            string outputDirectory = null,
            FrozenRunScore? frozenScore = null, int directorId = 0, bool? scoreIsFinal = null)
        {
            var safeWeaponDamage = weaponDamage ?? Array.Empty<UnityTelemetryDamageValue>();
            long attributedDamage = 0;
            for (var index = 0; index < safeWeaponDamage.Length; index++)
                attributedDamage += Math.Max(0, safeWeaponDamage[index]?.value ?? 0);

            var report = new UnityTelemetryReport
            {
                runId = RunId,
                context = _context,
                history = HistoryInfo,
                seed = _seed,
                startedAt = _startedAt ?? DateTime.UtcNow.ToString("O"),
                exportedAt = DateTime.UtcNow.ToString("O"),
                summary = new UnityTelemetrySummary
                {
                    status = status ?? "active",
                    scoreIsFinal = scoreIsFinal ?? frozenScore.HasValue,
                    timeSeconds = BrowserRounded(timeSeconds),
                    score = Mathf.Max(0, score),
                    baseScore = frozenScore?.BaseScore ?? Math.Max(0, score),
                    pressureHundredths = frozenScore?.PressureHundredths ?? 0,
                    multiplierHundredths = frozenScore?.MultiplierHundredths ?? 0,
                    finalScore = frozenScore?.FinalScore ?? 0,
                    scoringVersion = frozenScore.HasValue ? RunScoreRules.Version : 0,
                    directorId = directorId,
                    kills = Mathf.Max(0, kills),
                    eliteKills = Mathf.Max(0, eliteKills),
                    bossKills = Mathf.Max(0, bossKills),
                    level = Mathf.Max(1, level),
                    damageDealt = Math.Max(0, damageDealt),
                    damageTaken = Math.Max(0, damageTaken),
                    unattributedDamage = Math.Max(0, Math.Max(0, damageDealt) - attributedDamage),
                    partsEarned = Mathf.Max(0, partsEarned),
                    activeBosses = Mathf.Max(0, activeBosses),
                    enemies = Mathf.Max(0, enemies),
                    pickups = Mathf.Max(0, pickups),
                    xpOnGround = Mathf.Max(0, xpOnGround),
                    xpHeldByHarvesters = BrowserFloorNonNegative(xpHeldByHarvesters),
                    progress = progress ?? new UnityTelemetryProgress
                    {
                        weapons = Array.Empty<UnityTelemetryNamedValue>(),
                        supports = Array.Empty<UnityTelemetryNamedValue>(),
                        late = Array.Empty<UnityTelemetryNamedValue>(),
                        evolved = Array.Empty<string>(),
                    },
                    weaponDamage = safeWeaponDamage,
                },
                progression = new UnityTelemetryProgression
                {
                    levels = _levels.ToArray(),
                    upgrades = _upgrades.ToArray(),
                    milestones = _milestones.ToArray(),
                },
                experience = new UnityTelemetryExperience
                {
                    released = _xpReleased,
                    collected = _xpCollected,
                    absorbedByHarvesters = _xpAbsorbedByHarvesters,
                    onGround = Mathf.Max(0, xpOnGround),
                    heldByHarvesters = BrowserFloorNonNegative(xpHeldByHarvesters),
                    accountingGap = _xpReleased - _xpCollected - Mathf.Max(0, xpOnGround) - _xpAbsorbedByHarvesters,
                },
                bosses = _bosses.ToArray(),
                pickupsCollected = _pickups.ToArray(),
                arenas = new UnityTelemetryArenas
                {
                    transitions = _arenaTransitions.ToArray(),
                    deferredTransitions = _deferredArenaTransitions,
                    byArena = BuildArenaSummariesForExport(),
                },
                performance = new UnityTelemetryPerformance
                {
                    framesObserved = _framesObserved,
                    minimumFps = IsFinite(_minimumFps) ? Mathf.FloorToInt(_minimumFps) : 0,
                    maximumFrameMs = BrowserRounded(_maximumFrameMs),
                    maximumEnemies = _maximumEnemies,
                    maximumProjectiles = _maximumProjectiles,
                    maximumPickups = _maximumPickups,
                    meanFrameMs = _framesObserved == 0 ? 0 : BrowserRounded((float)(_totalFrameMs / _framesObserved)),
                    p50FrameMs = FramePercentile(50),
                    p95FrameMs = FramePercentile(95),
                    p99FrameMs = FramePercentile(99),
                    framesOver16_67Ms = _framesOver16_67Ms,
                    framesOver33_33Ms = _framesOver33_33Ms,
                    framesOver50Ms = _framesOver50Ms,
                },
                samples = GetSamplesArray(),
                droppedRecords = new UnityTelemetryDroppedRecords
                {
                    samples = _droppedSamples,
                    upgrades = _droppedUpgrades,
                    bosses = _droppedBosses,
                    arenaTransitions = _droppedArenaTransitions,
                    milestones = _droppedMilestones,
                },
            };

            try
            {
                var directory = string.IsNullOrEmpty(outputDirectory) ? (_outputDirectory ?? DefaultExportDirectory) : outputDirectory;
                if (string.IsNullOrEmpty(RunId)) RunId = Guid.NewGuid().ToString("N");
                report.runId = RunId;
                var filename = "voidfall-run-" + RunId + ".json";
                var path = Path.Combine(directory, filename);
                var json = JsonUtility.ToJson(report, true);
                if (_history == null || !_history.TryQueueSummary(path, json))
                {
                    var history = HistoryInfo;
                    if (history != null && history.drainTimedOut && !history.closed)
                        throw new IOException("History is still draining; final summary deferred to avoid overwriting a newer checkpoint.");
                    WriteSummaryAtomically(path, json);
                }
                LastExportError = null;
                return path;
            }
            catch (Exception exception)
            {
                LastExportError = exception.Message;
                return null;
            }
        }

        private UnityTelemetrySample[] GetSamplesArray()
        {
            var result = new UnityTelemetrySample[_sampleCount];
            for (int i = 0; i < _sampleCount; i++)
            {
                result[i] = _samples[(_sampleHead + i) % MaxSamples];
            }
            return result;
        }

        private void AddEvent(List<UnityTelemetryEvent> destination, UnityTelemetryEvent value)
        {
            RecordHistory(new UnityTelemetryHistoryEvent { kind = ReferenceEquals(destination, _upgrades) ? "upgrade" : value.kind,
                id = value.id, reason = value.kind, timeSeconds = value.timeSeconds, level = value.level,
                amount = value.value, nextXp = value.nextXp, bufferedXp = value.bufferedXp, progress = value.progress });
            if (destination.Count >= MaxEvents)
            {
                _droppedRecords++;
                if (ReferenceEquals(destination, _levels)) _droppedUpgrades++;
                else if (ReferenceEquals(destination, _upgrades)) _droppedUpgrades++;
                else if (ReferenceEquals(destination, _milestones)) _droppedMilestones++;
                return;
            }
            destination.Add(value);
        }

        private static UnityTelemetrySample NormalizeSample(UnityTelemetrySample sample)
        {
            return new UnityTelemetrySample
            {
                timeSeconds = BrowserRounded(sample.timeSeconds),
                level = sample.level,
                hp = BrowserRounded(sample.hp),
                maxHp = BrowserRounded(sample.maxHp),
                enemies = sample.enemies,
                activeBosses = sample.activeBosses,
                projectiles = sample.projectiles,
                pickups = sample.pickups,
                xpOnGround = BrowserFloorNonNegative(sample.xpOnGround),
                xpHeldByHarvesters = BrowserFloorNonNegative(sample.xpHeldByHarvesters),
                fps = sample.fps,
                frameMs = BrowserRounded(sample.frameMs),
                quality = sample.quality,
                hpMultiplier = BrowserRounded(sample.hpMultiplier, 3),
                speedMultiplier = BrowserRounded(sample.speedMultiplier, 3),
                damageMultiplier = BrowserRounded(sample.damageMultiplier, 3),
                directorEvent = sample.directorEvent,
                arenaId = sample.arenaId,
                arenaPhase = sample.arenaPhase,
                activeEliteVariants = sample.activeEliteVariants,
                meteors = sample.meteors,
                pressureHundredths = sample.pressureHundredths,
                challengeSeconds = BrowserRounded(sample.challengeSeconds),
                directorId = sample.directorId,
                encounterPhase = sample.encounterPhase,
                encounterKind = sample.encounterKind,
                spawnReason = sample.spawnReason,
                playerX = BrowserRounded(sample.playerX),
                playerY = BrowserRounded(sample.playerY),
                viewportWidth = BrowserRounded(sample.viewportWidth),
                viewportHeight = BrowserRounded(sample.viewportHeight),
                arenaWidth = BrowserRounded(sample.arenaWidth),
                arenaHeight = BrowserRounded(sample.arenaHeight),
                xp = sample.xp,
                baseScore = sample.baseScore,
                nearestEnemyDistance = BrowserRounded(sample.nearestEnemyDistance),
                onScreenEnemies = sample.onScreenEnemies,
                specialAttackLimit = sample.specialAttackLimit,
                committedSpecialAttacks = sample.committedSpecialAttacks,
                hostileProjectiles = sample.hostileProjectiles,
                xpPickupCount = sample.xpPickupCount,
                specialPickupCount = sample.specialPickupCount,
                distantLootCount = sample.distantLootCount,
                localSurvivalSeconds = sample.localSurvivalSeconds,
                survivalRemainingSeconds = sample.survivalRemainingSeconds,
                bossDifficultySeconds = sample.bossDifficultySeconds,
            };
        }

        private UnityTelemetryArenaSummary[] BuildArenaSummariesForExport()
        {
            var result = new UnityTelemetryArenaSummary[_arenas.Count];
            for (var index = 0; index < _arenas.Count; index++)
            {
                var source = _arenas[index];
                result[index] = new UnityTelemetryArenaSummary
                {
                    id = source.id,
                    timeSeconds = BrowserRounded(source.timeSeconds),
                    framesObserved = source.framesObserved,
                    minimumFps = source.framesObserved > 0
                        ? Mathf.FloorToInt(source.minimumFps)
                        : 0,
                    maximumFrameMs = BrowserRounded(source.maximumFrameMs),
                    maximumEnemies = source.maximumEnemies,
                    maximumProjectiles = source.maximumProjectiles,
                    maximumPickups = source.maximumPickups,
                    eliteSpawns = source.eliteSpawns,
                    eliteKills = source.eliteKills,
                    rosterTwo = source.rosterTwo,
                    meteors = source.meteors,
                };
            }
            return result;
        }

        private UnityTelemetryArenaSummary GetArenaSummary(string id)
        {
            var safeId = string.IsNullOrEmpty(id) ? "void" : id;
            for (var index = 0; index < _arenas.Count; index++)
            {
                if (_arenas[index].id == safeId) return _arenas[index];
            }
            if (_arenas.Count >= 64)
            {
                _droppedRecords++;
                return null;
            }
            var created = new UnityTelemetryArenaSummary
            {
                id = safeId,
                minimumFps = float.PositiveInfinity,
                eliteSpawns = Array.Empty<UnityTelemetryNamedValue>(),
                eliteKills = Array.Empty<UnityTelemetryNamedValue>(),
                rosterTwo = new UnityTelemetryRosterTwo(),
                meteors = new UnityTelemetryMeteors(),
            };
            _arenas.Add(created);
            return created;
        }

        private static UnityTelemetryNamedValue[] IncrementNamedValue(UnityTelemetryNamedValue[] values, string id)
        {
            var safeId = string.IsNullOrEmpty(id) ? "unknown" : id;
            values = values ?? Array.Empty<UnityTelemetryNamedValue>();
            for (var index = 0; index < values.Length; index++)
            {
                if (values[index].id != safeId) continue;
                values[index].value++;
                return values;
            }
            var expanded = new UnityTelemetryNamedValue[values.Length + 1];
            Array.Copy(values, expanded, values.Length);
            expanded[values.Length] = new UnityTelemetryNamedValue { id = safeId, value = 1 };
            return expanded;
        }

        private float FramePercentile(int percentile)
        {
            if (_framesObserved == 0) return 0;
            var target = Math.Max(1, (int)(((long)_framesObserved * percentile + 99) / 100));
            var count = 0;
            for (var index = 0; index < _frameHistogram.Length; index++)
            {
                count += _frameHistogram[index];
                if (count >= target) return index == 4001 ? _maximumFrameMs : index * 0.25f;
            }
            return _maximumFrameMs;
        }

        private static void WriteSummaryAtomically(string path, string json)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var bytes = new UTF8Encoding(false).GetBytes(json);
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporaryPath, path, null);
                else File.Move(temporaryPath, path);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        private sealed class HistoryWriter
        {
            private const int Capacity = 4096;
            private const int MaximumQueuedCharacters = 4 * 1024 * 1024;
            private const int MaximumRecordCharacters = 128 * 1024;
            private readonly object _gate = new object();
            private readonly Queue<string> _queue = new Queue<string>();
            private readonly string _path;
            private readonly Thread _thread;
            private bool _accepting = true;
            private bool _closeRequested;
            private bool _closed;
            private bool _flushRequested;
            private bool _drainTimedOut;
            private int _queuedCharacters;
            private int _inFlight;
            private long _submitted;
            private long _written;
            private long _flushed;
            private long _dropped;
            private string _lastError;
            private int _errorCount;
            // At most one pending summary; newer checkpoints supersede older ones.
            private string _summaryPath;
            private string _summaryJson;

            public HistoryWriter(string path)
            {
                _path = path;
                _thread = new Thread(WriteLoop) { IsBackground = true, Name = "VoidFall run history" };
                _thread.Start();
            }

            public bool Accepting { get { lock (_gate) return _accepting; } }

            public bool RejectIfUnavailable()
            {
                lock (_gate)
                {
                    if (_accepting) return false;
                    _submitted++;
                    _dropped++;
                    return true;
                }
            }

            public void Enqueue(string json)
            {
                lock (_gate)
                {
                    _submitted++;
                    if (!_accepting || _queue.Count >= Capacity || json.Length > MaximumRecordCharacters ||
                        _queuedCharacters + json.Length > MaximumQueuedCharacters)
                    {
                        _dropped++;
                        return;
                    }
                    _queue.Enqueue(json);
                    _queuedCharacters += json.Length;
                    Monitor.Pulse(_gate);
                }
            }

            public void RecordRejected(string error)
            {
                lock (_gate) { _submitted++; _dropped++; _errorCount++; _lastError = error; }
            }

            public bool TryQueueSummary(string path, string json)
            {
                lock (_gate)
                {
                    if (!_accepting) return false;
                    _summaryPath = path;
                    _summaryJson = json;
                    _flushRequested = true;
                    Monitor.Pulse(_gate);
                    return true;
                }
            }

            public void RequestFlush()
            {
                lock (_gate) { _flushRequested = true; Monitor.Pulse(_gate); }
            }

            public void Close()
            {
                lock (_gate)
                {
                    _accepting = false;
                    _closeRequested = true;
                    Monitor.Pulse(_gate);
                }
                // An inaccessible/stalled device must not hold application quit forever.
                if (!_thread.Join(5000))
                {
                    lock (_gate)
                    {
                        if (!_drainTimedOut) _errorCount++;
                        _drainTimedOut = true;
                        _lastError = "History drain timed out after 5000 ms; pending records may be incomplete.";
                    }
                }
            }

            public UnityTelemetryHistoryInfo Snapshot()
            {
                lock (_gate) return new UnityTelemetryHistoryInfo
                {
                    file = _path, submitted = _submitted, written = _written, flushed = _flushed,
                    dropped = _dropped, pending = _queue.Count + _inFlight, queueCapacity = Capacity,
                    maximumQueuedCharacters = MaximumQueuedCharacters, closed = _closed,
                    drainTimedOut = _drainTimedOut, errorCount = _errorCount, lastError = _lastError,
                };
            }

            private void WriteLoop()
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path)));
                    // CreateNew prevents accidental reuse of another recorder's journal.
                    using (var stream = new FileStream(_path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 65536))
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 65536))
                    {
                        var timer = System.Diagnostics.Stopwatch.StartNew();
                        while (true)
                        {
                            for (var count = 0; count < 128; count++)
                            {
                                string line;
                                lock (_gate)
                                {
                                    if (_queue.Count == 0) break;
                                    line = _queue.Dequeue();
                                    _queuedCharacters -= line.Length;
                                    _inFlight = 1;
                                }
                                writer.WriteLine(line);
                                lock (_gate) { _written++; _inFlight = 0; }
                            }
                            bool flush;
                            bool close;
                            string summaryPath;
                            string summaryJson;
                            lock (_gate)
                            {
                                close = _closeRequested && _queue.Count == 0;
                                flush = close || _flushRequested || timer.ElapsedMilliseconds >= 1000;
                                _flushRequested = false;
                                summaryPath = _summaryPath;
                                summaryJson = _summaryJson;
                                _summaryPath = null;
                                _summaryJson = null;
                            }
                            if (flush)
                            {
                                writer.Flush();
                                stream.Flush(true);
                                lock (_gate) _flushed = _written;
                                timer.Restart();
                            }
                            if (summaryJson != null)
                            {
                                try { WriteSummaryAtomically(summaryPath, summaryJson); }
                                catch (Exception exception)
                                {
                                    lock (_gate) { _errorCount++; _lastError = "Summary: " + exception.Message; }
                                }
                            }
                            if (close) break;
                            lock (_gate)
                            {
                                if (_queue.Count == 0 && !_closeRequested && !_flushRequested && _summaryJson == null)
                                    Monitor.Wait(_gate, 1000);
                            }
                        }
                    }
                }
                catch (Exception exception)
                {
                    lock (_gate)
                    {
                        _lastError = exception.Message;
                        _errorCount++;
                        _accepting = false;
                        _dropped += _queue.Count + _inFlight + (_written - _flushed);
                        _queue.Clear();
                        _queuedCharacters = 0;
                        _inFlight = 0;
                    }
                }
                finally
                {
                    lock (_gate) { _accepting = false; _closed = true; }
                }
            }
        }

        private static float Safe(float value)
        {
            return IsFinite(value) ? Mathf.Max(0, value) : 0;
        }

        private static float BrowserRounded(float value, int digits = 2)
        {
            if (!IsFinite(value)) return 0;
            var scale = Mathf.Pow(10f, Mathf.Max(0, digits));
            return Mathf.Floor(value * scale + 0.5f) / scale;
        }

        private static float BrowserFloorNonNegative(float value)
        {
            return IsFinite(value) ? Mathf.Max(0, Mathf.Floor(value)) : 0;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
