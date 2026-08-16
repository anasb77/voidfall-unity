using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Persistence
{
    [Serializable]
    public sealed class SaveSettings
    {
        public float masterVolume = 0.8f;
        public float effectsVolume = 0.9f;
        public float musicVolume = 0.7f;
        public float shake = 0.8f;
        public bool reducedMotion;
        public bool highContrast;
        public float touchSize = 1f;
        public string quality = "high";
    }

    [Serializable]
    public sealed class WorkshopEntry
    {
        public string id;
        public int rank;
    }

    [Serializable]
    public sealed class WeaponDamageEntry
    {
        public string id;
        public long damage;
    }

    [Serializable]
    public sealed class BestiaryEntry
    {
        public string id;
        public bool discovered;
    }

    [Serializable]
    public sealed class LifetimeStats
    {
        public int totalRuns;
        public int totalPlaySeconds;
        public int totalKills;
        public int totalEliteKills;
        public int totalBossKills;
        public long totalDamageDealt;
        public long totalDamageTaken;
        public int totalPartsEarned;
        public int bestScore;
        public int bestTime;
        public int bestKills;
        public int highestLevel = 1;
    }

    [Serializable]
    public class HighScoreEntry
    {
        public int score;
        public int kills;
        public int time;
        public int level;
        public int eliteKills;
        public int bossKills;
        public int partsEarned;
        public long date;
    }

    [Serializable]
    public sealed class RunRecordEntry : HighScoreEntry
    {
        public long damageDealt;
        public long damageTaken;
        public WorkshopEntry[] weapons = Array.Empty<WorkshopEntry>();
        public WeaponDamageEntry[] weaponDamage = Array.Empty<WeaponDamageEntry>();
        public WorkshopEntry[] supports = Array.Empty<WorkshopEntry>();
        public WorkshopEntry[] late = Array.Empty<WorkshopEntry>();
        public WorkshopEntry[] evolved = Array.Empty<WorkshopEntry>();
    }

    [Serializable]
    public sealed class SaveData
    {
        public int version = SaveStore.SaveVersion;
        public int parts;
        public SaveSettings settings = new SaveSettings();
        public WorkshopEntry[] workshop = Array.Empty<WorkshopEntry>();
        public LifetimeStats stats = new LifetimeStats();
        public HighScoreEntry[] highScores = Array.Empty<HighScoreEntry>();
        public RunRecordEntry[] recentRuns = Array.Empty<RunRecordEntry>();
        public BestiaryEntry[] bestiary = Array.Empty<BestiaryEntry>();
        public string arena = "void";
    }

    public sealed class SaveStore
    {
        public const int SaveVersion = 5;
        public const string SaveKey = "voidfall_save_v4";
        public const int MaxHighScores = 8;
        public const int MaxRecentRuns = 12;
        public const int WorkshopMaxRank = 3;
        public const int MaxRunEntryFields = 32;
        private const string LegacyScoreKey = "voidfall_scores_v1";
        private const long MaxCounter = 999_999_999;
        private const long MaxDamageCounter = 999_999_999_999;
        private const long UnixMillisThreshold = 10_000_000_000_000;

        private static readonly string[] PreviousSaveKeys =
        {
            "service_yard_save_v3",
            "voidfall_save_v3",
        };

        private static readonly string[] WorkshopOrder =
        {
            "integrity", "power", "mobility", "recovery", "magnet", "precision", "arsenal", "protocol",
        };

        private static readonly string[] BestiaryOrder =
        {
            "chaser", "runner", "gunner", "twinGunner", "dasher", "brute", "exploder", "guard",
            "technician", "mortar", "splitter", "bulwark", "harvester", "carrier", "elite",
            "herald", "warden", "matriarch", "reaver",
        };

        private readonly string _path;

        public SaveStore(string path = null)
        {
            _path = string.IsNullOrEmpty(path)
                ? Path.Combine(Application.persistentDataPath, SaveKey + ".json")
                : path;
        }

        public string PathOnDisk => _path;

        public SaveData Load()
        {
            var sourcePath = FindLoadPath();
            if (sourcePath == null)
            {
                if (TryLoadLegacyScores(out var legacyScores))
                    return PersistRecovery(legacyScores);
                return PersistRecovery(CreateDefault());
            }

            string raw = null;
            try
            {
                raw = File.ReadAllText(sourcePath);
                if (BrowserSaveImporter.TryConvert(raw, out var browserData))
                {
                    var imported = Sanitize(browserData);
                    Save(imported);
                    return imported;
                }

                var data = JsonUtility.FromJson<SaveData>(raw);
                if (data == null) throw new FormatException("Save root is not an object.");
                // Keep the raw value before Sanitize mutates the object to v5.
                var storedVersion = data.version;
                var sanitized = Sanitize(data);
                // Browser loadSave() persists a v3/v4 migration immediately.
                // Do the same for Unity-native saves so one-time protocol refunds
                // and other legacy normalization cannot be applied again after a
                // restart.
                // Browser loadSave() compares the raw stored version, not the
                // clamped value used by sanitization. Persist unknown/future
                // versions too, so the repaired v5 profile is durable and a
                // restart cannot re-enter the migration path.
                if (!string.Equals(sourcePath, _path, StringComparison.OrdinalIgnoreCase) ||
                    storedVersion != SaveVersion)
                    Save(sanitized);
                return sanitized;
            }
            catch (Exception exception)
            {
                BackupCorruptFile(raw, exception.Message);
                if (TryLoadLegacyScores(out var legacyScores))
                    return PersistRecovery(legacyScores);
                return PersistRecovery(CreateDefault());
            }
        }

        private string FindLoadPath()
        {
            if (File.Exists(_path)) return _path;

            foreach (var key in PreviousSaveKeys)
            {
                var candidate = CompanionPath(key);
                if (File.Exists(candidate)) return candidate;
            }

            return null;
        }

        private string CompanionPath(string key)
        {
            var directory = System.IO.Path.GetDirectoryName(_path);
            return string.IsNullOrEmpty(directory)
                ? key + ".json"
                : System.IO.Path.Combine(directory, key + ".json");
        }

        private bool TryLoadLegacyScores(out SaveData data)
        {
            data = null;
            var path = CompanionPath(LegacyScoreKey);
            if (!File.Exists(path)) return false;

            try
            {
                return BrowserSaveImporter.TryConvertLegacyScores(File.ReadAllText(path), out data);
            }
            catch
            {
                data = null;
                return false;
            }
        }

        private SaveData PersistRecovery(SaveData data)
        {
            // Browser loadSave() attempts safeSet() but still returns the
            // usable profile when storage is unavailable.
            try { Save(data); }
            catch { }
            return data;
        }

        public bool TryImportBrowserSave(string json, out SaveData imported, out string error)
        {
            imported = null;
            error = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Paste a browser save JSON document first.";
                return false;
            }

            // Browser exports are small profile documents. Keep malformed or
            // unexpectedly large clipboard input from becoming an unbounded
            // parser/file-write operation.
            const int maxImportCharacters = 1_000_000;
            if (json.Length > maxImportCharacters)
            {
                error = "Browser save is too large to import.";
                return false;
            }

            if (!BrowserSaveImporter.TryConvert(json, out var browserData) || browserData == null)
            {
                error = "Browser save JSON is invalid or missing required profile fields.";
                return false;
            }

            try
            {
                imported = Sanitize(browserData);
                Save(imported);
                return true;
            }
            catch (Exception exception)
            {
                imported = null;
                error = "Imported profile could not be saved: " + exception.Message;
                return false;
            }
        }

        public void Save(SaveData data)
        {
            var sanitized = Sanitize(data);
            var directory = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var temporaryPath = _path + ".tmp";
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(sanitized, true));

            try
            {
                if (File.Exists(_path)) File.Replace(temporaryPath, _path, null);
                else File.Move(temporaryPath, _path);
            }
            catch
            {
                File.Copy(temporaryPath, _path, true);
                File.Delete(temporaryPath);
            }
        }

        public static SaveData CreateDefault()
        {
            var data = new SaveData
            {
                version = SaveVersion,
                parts = 0,
                settings = new SaveSettings(),
                workshop = Array.Empty<WorkshopEntry>(),
                stats = new LifetimeStats(),
                highScores = Array.Empty<HighScoreEntry>(),
                recentRuns = Array.Empty<RunRecordEntry>(),
                arena = "void",
            };
            data.bestiary = CreateDefaultBestiary();
            data.workshop = CreateDefaultWorkshop();
            return data;
        }

        public static SaveData Sanitize(SaveData data)
        {
            var result = data ?? CreateDefault();
            var sourceVersion = ClampInt(result.version, 0, SaveVersion);
            var legacyProtocolRank = LegacyProtocolRank(result.workshop);
            var protocolRefund = sourceVersion > 0 && sourceVersion < SaveVersion
                ? legacyProtocolRank >= 3 ? 360 : legacyProtocolRank == 2 ? 160 : 0
                : 0;
            var migratedParts = (long)Math.Max(0, result.parts) + protocolRefund;
            result.version = SaveVersion;
            result.parts = ClampInt(
                migratedParts > int.MaxValue ? int.MaxValue : migratedParts < int.MinValue ? int.MinValue : (int)migratedParts,
                0,
                (int)MaxCounter);
            result.settings = SanitizeSettings(result.settings);
            result.workshop = SanitizeWorkshop(result.workshop);
            result.stats = SanitizeStats(result.stats);
            result.highScores = SanitizeHighScores(result.highScores);
            result.recentRuns = SanitizeRecentRuns(result.recentRuns);
            result.bestiary = SanitizeBestiary(result.bestiary);
            result.arena = IsArena(result.arena) ? result.arena : "void";
            foreach (var score in result.highScores)
            {
                if (score == null) continue;
                result.stats.bestScore = Math.Max(result.stats.bestScore, score.score);
                result.stats.bestTime = Math.Max(result.stats.bestTime, score.time);
                result.stats.bestKills = Math.Max(result.stats.bestKills, score.kills);
                result.stats.highestLevel = Math.Max(result.stats.highestLevel, score.level);
            }
            return result;
        }

        private static int LegacyProtocolRank(WorkshopEntry[] entries)
        {
            var rank = 0;
            foreach (var entry in entries ?? Array.Empty<WorkshopEntry>())
            {
                if (entry != null && entry.id == "protocol") rank = Math.Max(rank, entry.rank);
            }
            return ClampInt(rank, 0, WorkshopMaxRank);
        }

        private void BackupCorruptFile(string raw, string reason)
        {
            try
            {
                if (raw == null && File.Exists(_path)) raw = File.ReadAllText(_path);
                if (raw == null) return;
                var backup = _path + "_corrupt_" + DateTime.UtcNow.Ticks + ".json";
                File.WriteAllText(backup, raw);
                Debug.LogWarning("VoidFall save was corrupt and backed up: " + backup + " (" + reason + ")");
            }
            catch (Exception backupException)
            {
                Debug.LogError("VoidFall save backup failed: " + backupException.Message);
            }
        }

        private static SaveSettings SanitizeSettings(SaveSettings settings)
        {
            var value = settings ?? new SaveSettings();
            value.masterVolume = Clamp(value.masterVolume, 0, 1, 0.8f);
            value.effectsVolume = Clamp(value.effectsVolume, 0, 1, 0.9f);
            value.musicVolume = Clamp(value.musicVolume, 0, 1, 0.7f);
            value.shake = Clamp(value.shake, 0, 1, 0.8f);
            value.touchSize = Clamp(value.touchSize, 0.75f, 1.35f, 1f);
            if (value.quality != "auto" && value.quality != "low" && value.quality != "balanced" && value.quality != "high")
                value.quality = "high";
            return value;
        }

        private static WorkshopEntry[] SanitizeWorkshop(WorkshopEntry[] entries)
        {
            var result = CreateDefaultWorkshop();
            foreach (var entry in entries ?? Array.Empty<WorkshopEntry>())
            {
                var index = Array.IndexOf(WorkshopOrder, entry?.id);
                if (index < 0) continue;
                var maxRank = WorkshopOrder[index] == "protocol" ? 1 : WorkshopMaxRank;
                result[index].rank = ClampInt(entry.rank, 0, maxRank);
            }
            return result;
        }

        private static LifetimeStats SanitizeStats(LifetimeStats stats)
        {
            var value = stats ?? new LifetimeStats();
            value.totalRuns = ClampInt(value.totalRuns, 0, (int)MaxCounter);
            value.totalPlaySeconds = ClampInt(value.totalPlaySeconds, 0, (int)MaxCounter);
            value.totalKills = ClampInt(value.totalKills, 0, (int)MaxCounter);
            value.totalEliteKills = ClampInt(value.totalEliteKills, 0, (int)MaxCounter);
            value.totalBossKills = ClampInt(value.totalBossKills, 0, (int)MaxCounter);
            value.totalDamageDealt = ClampLong(value.totalDamageDealt, 0, MaxDamageCounter);
            value.totalDamageTaken = ClampLong(value.totalDamageTaken, 0, MaxDamageCounter);
            value.totalPartsEarned = ClampInt(value.totalPartsEarned, 0, (int)MaxCounter);
            value.bestScore = ClampInt(value.bestScore, 0, (int)MaxCounter);
            value.bestTime = ClampInt(value.bestTime, 0, 86_400);
            value.bestKills = ClampInt(value.bestKills, 0, (int)MaxCounter);
            value.highestLevel = ClampInt(value.highestLevel, 1, 999);
            return value;
        }

        private static HighScoreEntry[] SanitizeHighScores(HighScoreEntry[] scores)
        {
            var source = scores ?? Array.Empty<HighScoreEntry>();
            var result = new List<HighScoreEntry>(Math.Min(MaxHighScores, source.Length));
            foreach (var score in source)
            {
                if (score == null) continue;
                result.Add(SanitizeHighScore(score));
            }
            result.Sort(CompareScores);
            if (result.Count > MaxHighScores) result.RemoveRange(MaxHighScores, result.Count - MaxHighScores);
            return result.ToArray();
        }

        private static RunRecordEntry[] SanitizeRecentRuns(RunRecordEntry[] runs)
        {
            var source = runs ?? Array.Empty<RunRecordEntry>();
            var result = new List<RunRecordEntry>(Math.Min(MaxRecentRuns, source.Length));
            for (var i = 0; i < source.Length; i++)
            {
                var value = source[i];
                if (value == null) continue;
                SanitizeHighScore(value);
                value.damageDealt = ClampLong(value.damageDealt, 0, MaxDamageCounter);
                value.damageTaken = ClampLong(value.damageTaken, 0, MaxDamageCounter);
                value.weapons = SanitizeKnownEntries(value.weapons, WeaponIds(), WeaponMaxRanks());
                value.weaponDamage = SanitizeKnownWeaponDamage(value.weaponDamage);
                value.supports = SanitizeKnownEntries(value.supports, SupportIds(), SupportMaxRanks());
                value.late = SanitizeKnownEntries(value.late, LateIds(), LateMaxRanks());
                value.evolved = SanitizeKnownEntries(value.evolved, WeaponIds(), Ones(WeaponIds().Length));
                result.Add(value);
            }
            result.Sort((left, right) => right.date.CompareTo(left.date));
            if (result.Count > MaxRecentRuns)
                result.RemoveRange(MaxRecentRuns, result.Count - MaxRecentRuns);
            return result.ToArray();
        }

        public static int CompareScores(HighScoreEntry left, HighScoreEntry right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return 1;
            if (right == null) return -1;
            var comparison = right.score.CompareTo(left.score);
            if (comparison != 0) return comparison;
            comparison = right.kills.CompareTo(left.kills);
            if (comparison != 0) return comparison;
            comparison = right.time.CompareTo(left.time);
            if (comparison != 0) return comparison;
            return left.date.CompareTo(right.date);
        }

        private static HighScoreEntry SanitizeHighScore(HighScoreEntry score)
        {
            var value = score ?? new HighScoreEntry();
            value.score = ClampInt(value.score, 0, (int)MaxCounter);
            value.kills = ClampInt(value.kills, 0, (int)MaxCounter);
            value.time = ClampInt(value.time, 0, 86_400);
            value.level = ClampInt(value.level, 1, 999);
            value.eliteKills = ClampInt(value.eliteKills, 0, (int)MaxCounter);
            value.bossKills = ClampInt(value.bossKills, 0, (int)MaxCounter);
            value.partsEarned = ClampInt(value.partsEarned, 0, (int)MaxCounter);
            value.date = NormalizeDate(value.date);
            return value;
        }

        private static long NormalizeDate(long value)
        {
            if (value <= 0) return 0;
            if (value < UnixMillisThreshold) return value;
            if (value > DateTime.MaxValue.Ticks) return UnixMillisThreshold - 1;
            try
            {
                return new DateTimeOffset(new DateTime(value, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
            }
            catch
            {
                return 0;
            }
        }

        private static BestiaryEntry[] SanitizeBestiary(BestiaryEntry[] entries)
        {
            var result = CreateDefaultBestiary();
            foreach (var entry in entries ?? Array.Empty<BestiaryEntry>())
            {
                var index = Array.IndexOf(BestiaryOrder, entry?.id);
                if (index >= 0) result[index].discovered = entry.discovered;
            }
            return result;
        }

        private static WorkshopEntry[] SanitizeEntries(WorkshopEntry[] entries)
        {
            var source = entries ?? Array.Empty<WorkshopEntry>();
            var length = Math.Min(MaxRunEntryFields, source.Length);
            var result = new WorkshopEntry[length];
            for (var index = 0; index < length; index++)
            {
                var entry = source[index] ?? new WorkshopEntry();
                var id = string.IsNullOrEmpty(entry.id) ? "unknown" : entry.id;
                result[index] = new WorkshopEntry
                {
                    id = id.Substring(0, Math.Min(48, id.Length)),
                    rank = ClampInt(entry.rank, 0, 999),
                };
            }
            return result;
        }

        private static WorkshopEntry[] SanitizeKnownEntries(WorkshopEntry[] entries, string[] ids, int[] maxRanks)
        {
            var result = new List<WorkshopEntry>();
            foreach (var entry in entries ?? Array.Empty<WorkshopEntry>())
            {
                if (entry == null) continue;
                var index = Array.IndexOf(ids, entry.id);
                if (index < 0) continue;
                var rank = ClampInt(entry.rank, 0, maxRanks[index]);
                if (rank <= 0) continue;
                result.Add(new WorkshopEntry { id = entry.id, rank = rank });
                if (result.Count >= MaxRunEntryFields) break;
            }
            return result.ToArray();
        }

        private static WeaponDamageEntry[] SanitizeWeaponDamage(WeaponDamageEntry[] entries)
        {
            var source = entries ?? Array.Empty<WeaponDamageEntry>();
            var length = Math.Min(MaxRunEntryFields, source.Length);
            var result = new WeaponDamageEntry[length];
            for (var index = 0; index < length; index++)
            {
                var entry = source[index] ?? new WeaponDamageEntry();
                var id = string.IsNullOrEmpty(entry.id) ? "unknown" : entry.id;
                result[index] = new WeaponDamageEntry
                {
                    id = id.Substring(0, Math.Min(48, id.Length)),
                    damage = ClampLong(entry.damage, 0, MaxDamageCounter),
                };
            }
            return result;
        }

        private static WeaponDamageEntry[] SanitizeKnownWeaponDamage(WeaponDamageEntry[] entries)
        {
            var result = new List<WeaponDamageEntry>();
            var ids = WeaponIds();
            foreach (var entry in entries ?? Array.Empty<WeaponDamageEntry>())
            {
                if (entry == null || Array.IndexOf(ids, entry.id) < 0) continue;
                var damage = ClampLong(entry.damage, 0, MaxDamageCounter);
                if (damage <= 0) continue;
                result.Add(new WeaponDamageEntry { id = entry.id, damage = damage });
                if (result.Count >= MaxRunEntryFields) break;
            }
            return result.ToArray();
        }

        private static string[] WeaponIds()
        {
            var ids = new string[ContentCatalog.Weapons.Length];
            for (var index = 0; index < ids.Length; index++) ids[index] = ContentCatalog.Weapons[index].Id;
            return ids;
        }

        private static int[] WeaponMaxRanks()
        {
            var ranks = new int[ContentCatalog.Weapons.Length];
            for (var index = 0; index < ranks.Length; index++) ranks[index] = ContentCatalog.Weapons[index].Ranks.Length;
            return ranks;
        }

        private static string[] SupportIds()
        {
            var ids = new string[ContentCatalog.Supports.Length];
            for (var index = 0; index < ids.Length; index++) ids[index] = ContentCatalog.Supports[index].Id;
            return ids;
        }

        private static int[] SupportMaxRanks()
        {
            var ranks = new int[ContentCatalog.Supports.Length];
            for (var index = 0; index < ranks.Length; index++) ranks[index] = ContentCatalog.Supports[index].MaxRank;
            return ranks;
        }

        private static string[] LateIds()
        {
            var ids = new string[ContentCatalog.LateUpgrades.Length];
            for (var index = 0; index < ids.Length; index++) ids[index] = ContentCatalog.LateUpgrades[index].Id;
            return ids;
        }

        private static int[] LateMaxRanks()
        {
            var ranks = new int[ContentCatalog.LateUpgrades.Length];
            for (var index = 0; index < ranks.Length; index++) ranks[index] = ContentCatalog.LateUpgrades[index].MaxRank;
            return ranks;
        }

        private static int[] Ones(int length)
        {
            var result = new int[length];
            for (var index = 0; index < result.Length; index++) result[index] = 1;
            return result;
        }

        private static WorkshopEntry[] CreateDefaultWorkshop()
        {
            var result = new WorkshopEntry[WorkshopOrder.Length];
            for (var i = 0; i < result.Length; i++) result[i] = new WorkshopEntry { id = WorkshopOrder[i], rank = 0 };
            return result;
        }

        private static BestiaryEntry[] CreateDefaultBestiary()
        {
            var result = new BestiaryEntry[BestiaryOrder.Length];
            for (var i = 0; i < result.Length; i++) result[i] = new BestiaryEntry { id = BestiaryOrder[i], discovered = false };
            return result;
        }

        private static bool IsArena(string value)
        {
            return value == "void" || value == "redNebula" || value == "whiteSakura";
        }

        private static int ClampInt(int value, int min, int max) => Math.Min(max, Math.Max(min, value));
        private static long ClampLong(long value, long min, long max) => Math.Min(max, Math.Max(min, value));

        private static float Clamp(float value, float min, float max, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return fallback;
            return Mathf.Clamp(value, min, max);
        }
    }
}
