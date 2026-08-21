using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

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
        public UnityTelemetryNamedValue[] weapons;
        public UnityTelemetryNamedValue[] supports;
        public UnityTelemetryNamedValue[] late;
        public string[] evolved;
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
    }

    [Serializable]
    public sealed class UnityTelemetrySummary
    {
        public string status;
        public float timeSeconds;
        public int score;
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
        public int schemaVersion = 3;
        public string game = "VoidFall";
        public uint seed;
        public string startedAt;
        public string exportedAt;
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
    /// Bounded offline recorder matching the browser's local run-report intent.
    /// It never allocates in the simulation path except at the capped sample/event
    /// boundaries and fails closed if the report cannot be written.
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

        public void Begin(uint seed)
        {
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
            _maximumEnemies = Mathf.Max(_maximumEnemies, sample.enemies);
            _maximumProjectiles = Mathf.Max(_maximumProjectiles, sample.projectiles);
            _maximumPickups = Mathf.Max(_maximumPickups, sample.pickups);
            if (_sampleCount >= MaxSamples)
            {
                // Browser RunTelemetry.shift() drops the oldest sample so the
                // exported window always contains the newest observations.
                _samples[_sampleHead] = NormalizeSample(sample);
                _sampleHead = (_sampleHead + 1) % MaxSamples;
                _droppedRecords++;
                _droppedSamples++;
            }
            else
            {
                _samples[(_sampleHead + _sampleCount) % MaxSamples] = NormalizeSample(sample);
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
            float xpHeldByHarvesters = 0)
        {
            var safeWeaponDamage = weaponDamage ?? Array.Empty<UnityTelemetryDamageValue>();
            long attributedDamage = 0;
            for (var index = 0; index < safeWeaponDamage.Length; index++)
                attributedDamage += Math.Max(0, safeWeaponDamage[index]?.value ?? 0);

            var report = new UnityTelemetryReport
            {
                seed = _seed,
                startedAt = _startedAt ?? DateTime.UtcNow.ToString("O"),
                exportedAt = DateTime.UtcNow.ToString("O"),
                summary = new UnityTelemetrySummary
                {
                    status = status ?? "active",
                    timeSeconds = BrowserRounded(timeSeconds),
                    score = Mathf.Max(0, score),
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
                },
                samples = GetSamplesArray(),
                droppedRecords = new UnityTelemetryDroppedRecords
                {
                    samples = _droppedSamples,
                    upgrades = _droppedUpgrades,
                    bosses = _droppedBosses,
                    arenaTransitions = _droppedArenaTransitions,
                },
            };

            try
            {
                var directory = Application.persistentDataPath;
                Directory.CreateDirectory(directory);
                var filename = $"voidfall-run-{_seed}-{Mathf.Max(0, Mathf.FloorToInt(timeSeconds))}s.json";
                var path = Path.Combine(directory, filename);
                File.WriteAllText(path, JsonUtility.ToJson(report, true));
                return path;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("VoidFall telemetry export skipped: " + exception.Message);
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
            if (destination.Count >= MaxEvents)
            {
                _droppedRecords++;
                if (ReferenceEquals(destination, _levels)) _droppedUpgrades++;
                else if (ReferenceEquals(destination, _upgrades)) _droppedUpgrades++;
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
