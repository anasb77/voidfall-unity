using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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

        // VIDEO preferences. The field defaults double as the old-save
        // migration: JsonUtility leaves missing fields at their initializers,
        // so a pre-VIDEO profile deserializes to native resolution, the
        // platform's default fullscreen window, and the shipped post-effect
        // intensities. These bounds mirror VideoSettingsRules in VoidFall.UI.
        public int resolutionWidth;
        public int resolutionHeight;
        public int fullscreenMode = 1;
        public float bloom = -1f;
        public float chromatic = -1f;

        public const float MaxBloom = 2f;
        public const float MaxChromatic = 0.5f;
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

        private static readonly string[] BestiaryOrder = BuildBestiaryOrder();

        private static string[] BuildBestiaryOrder()
        {
            // Keep legacy entries in order and append Unity-authored content.
            // Discovery and sanitization must share the live content IDs.
            var ids = new List<string>();
            foreach (var enemy in ContentCatalog.Enemies) ids.Add(enemy.Id);
            ids.Add(ContentCatalog.Elite.Id);
            foreach (var boss in ContentCatalog.Bosses) ids.Add(boss.Id);
            foreach (var enemy in MonochromeContent.Enemies) ids.Add(enemy.Id);
            ids.Add(HydraContent.Boss.Id);
            ids.Add(MonochromeContent.BlackBoss.Id);
            ids.Add(MonochromeContent.WhiteBoss.Id);
            foreach (var enemy in NullCityContent.Enemies) ids.Add(enemy.Id);
            ids.Add(NullCityContent.MotherloadId);
            return ids.ToArray();
        }

        private readonly string _path;

        /// <summary>
        /// Set when a save file exists but could not be read this session. While
        /// this is true the file is presumed to hold real progression that we
        /// simply could not see, so ordinary saves refuse to overwrite it rather
        /// than replacing it with the default profile the player is looking at.
        /// </summary>
        private bool _storageUnreadable;
        private bool _preserveBackupUntilSave;

        public SaveStore(string path = null)
        {
            _path = string.IsNullOrEmpty(path)
                ? Path.Combine(Application.persistentDataPath, SaveKey + ".json")
                : path;
        }

        public string PathOnDisk => _path;

        public bool StorageUnreadable => _storageUnreadable;

        public SaveData Load()
        {
            // A migrated legacy file can still exist, but the current backup
            // contains newer progression and takes priority over that file.
            if (!File.Exists(_path) && TryRecoverBackup(out var backup)) return backup;
            var sourcePath = FindLoadPath();
            if (sourcePath == null)
            {
                if (TryLoadLegacyScores(out var legacyScores))
                    return PersistRecovery(legacyScores);
                return PersistRecovery(CreateDefault());
            }

            // Reading is kept separate from parsing. A read failure means the
            // storage was unavailable (file lock, permissions, transient I/O),
            // not that the profile is bad, so nothing may be written over it.
            string raw;
            try
            {
                raw = File.ReadAllText(sourcePath);
            }
            catch (Exception exception)
            {
                // Latch the failure so a later run-end save cannot quietly
                // replace the unread profile with this session's blank one.
                _storageUnreadable = true;
                Debug.LogError(
                    "VoidFall save could not be read and was left untouched: " + exception.Message);
                return CreateDefault();
            }

            // Only parsing happens inside this block. It performs no writes, so
            // a failure here really does mean the stored document is corrupt.
            // The previous version persisted migrations inside this try, which
            // let a storage error be misread as corruption and replace a valid
            // profile with defaults.
            SaveData resolved;
            bool persistMigration;
            try
            {
                if (BrowserSaveImporter.TryConvert(raw, out var browserData))
                {
                    resolved = Sanitize(browserData);
                    persistMigration = true;
                }
                else
                {
                    var data = JsonUtility.FromJson<SaveData>(raw);
                    if (data == null) throw new FormatException("Save root is not an object.");
                    // Keep the raw value before Sanitize mutates the object to v5.
                    var storedVersion = data.version;
                    resolved = Sanitize(data);
                    // Browser loadSave() persists a v3/v4 migration immediately.
                    // Do the same for Unity-native saves so one-time protocol refunds
                    // and other legacy normalization cannot be applied again after a
                    // restart.
                    // Browser loadSave() compares the raw stored version, not the
                    // clamped value used by sanitization. Persist unknown/future
                    // versions too, so the repaired v5 profile is durable and a
                    // restart cannot re-enter the migration path.
                    persistMigration =
                        !string.Equals(sourcePath, _path, StringComparison.OrdinalIgnoreCase) ||
                        storedVersion != SaveVersion;
                }
            }
            catch (Exception exception)
            {
                BackupCorruptFile(raw, exception.Message);
                if (TryRecoverBackup(out var recovered)) return recovered;
                if (TryLoadLegacyScores(out var legacyScores))
                    return PersistRecovery(legacyScores);
                return PersistRecovery(CreateDefault());
            }

            // The profile is already recovered at this point. Persisting the
            // migration is best-effort: browser loadSave() likewise returns the
            // usable profile even when its safeSet() cannot write.
            if (persistMigration) TryPersistMigration(resolved);
            return resolved;
        }

        private bool TryRecoverBackup(out SaveData recovered)
        {
            recovered = null;
            var backupPath = _path + ".bak";
            if (!File.Exists(backupPath)) return false;

            string raw;
            try
            {
                raw = File.ReadAllText(backupPath);
            }
            catch (Exception exception)
            {
                // An unreadable backup may be the only remaining progression.
                // Do not let a default profile replace it on a later save.
                _storageUnreadable = true;
                recovered = CreateDefault();
                Debug.LogError("VoidFall save backup could not be read and was left untouched: " + exception.Message);
                return true;
            }

            try
            {
                var data = BrowserSaveImporter.TryConvert(raw, out var browserData)
                    ? browserData
                    : JsonUtility.FromJson<SaveData>(raw);
                if (data == null) throw new FormatException("Save backup root is not an object.");
                recovered = Sanitize(data);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("VoidFall save backup could not be parsed: " + exception.Message);
                return false;
            }

            _preserveBackupUntilSave = true;
            try
            {
                // Do not rotate the corrupt primary over the last good backup.
                Save(recovered);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("VoidFall recovered save could not be persisted: " + exception.Message);
            }
            return true;
        }

        private void TryPersistMigration(SaveData data)
        {
            try
            {
                Save(data);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "VoidFall save migration could not be persisted: " + exception.Message);
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
                // An import replaces the whole profile and cannot be undone, so
                // keep the outgoing native save alongside it. Unity-native runs
                // carry per-run supports/late/evolved snapshots, and a browser
                // document that was produced before those were exported would
                // otherwise drop them permanently.
                BackupBeforeImport();
                imported = Sanitize(browserData);
                // An import is an explicit request to replace the profile, so it
                // may proceed even if this session could not read the old file.
                Save(imported, true);
                _storageUnreadable = false;
                return true;
            }
            catch (Exception exception)
            {
                imported = null;
                error = "Imported profile could not be saved: " + exception.Message;
                return false;
            }
        }

        private void BackupBeforeImport()
        {
            try
            {
                if (!File.Exists(_path)) return;
                File.Copy(_path, _path + ".pre-import.bak", true);
            }
            catch (Exception exception)
            {
                // A missing safety copy must not block the import the player
                // explicitly asked for; surface it and continue.
                Debug.LogWarning(
                    "VoidFall pre-import save backup failed: " + exception.Message);
            }
        }

        /// <param name="allowOverwriteUnreadable">
        /// Only for destructive actions the player asked for explicitly, such as
        /// resetting progress or importing a browser save. Ordinary saves must
        /// leave a profile alone when this session could not read it.
        /// </param>
        public void Save(SaveData data, bool allowOverwriteUnreadable = false)
        {
            Save(data, allowOverwriteUnreadable, _preserveBackupUntilSave);
        }

        private void Save(SaveData data, bool allowOverwriteUnreadable, bool preserveBackup)
        {
            if (_storageUnreadable && !allowOverwriteUnreadable)
            {
                throw new IOException(
                    "Refusing to overwrite the save file because it could not be read this session.");
            }

            var sanitized = Sanitize(Clone(data));
            var directory = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var temporaryPath = _path + ".tmp";

            // File.WriteAllText only closes the handle; it does not force the
            // data to the device. Without the explicit Flush(true) below, the
            // rename can be committed to the filesystem journal before the
            // replacement's contents land, so a power loss could leave a
            // zero-length profile. Matches File.WriteAllText's BOM-less UTF-8
            // so BrowserSaveImporter still reads the file byte-for-byte.
            using (var stream = new FileStream(
                       temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(JsonUtility.ToJson(sanitized, true));
                writer.Flush();
                stream.Flush(true);
            }

            // File.Replace is the only atomic path here, and it now keeps a
            // one-generation backup instead of discarding it. The previous
            // version fell back to File.Copy(overwrite: true), which truncates
            // the live save in place and can leave it half-written. A failed
            // Save must leave the last good profile intact, so the exception is
            // allowed to reach the caller; every call site handles it.
            try
            {
                if (File.Exists(_path)) File.Replace(temporaryPath, _path, preserveBackup ? null : _path + ".bak");
                else File.Move(temporaryPath, _path);
                _preserveBackupUntilSave = false;
            }
            catch
            {
                try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
                catch { /* the stale temp file is harmless; the next Save truncates it */ }
                throw;
            }
        }

        private static SaveData Clone(SaveData value)
        {
            if (value == null) return CreateDefault();
            var copy = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(value));
            return copy ?? CreateDefault();
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
            // 0 x 0 is the cycler's AUTO (native) entry, so a half-written pair
            // collapses to auto rather than an unusable size.
            value.resolutionWidth = ClampInt(value.resolutionWidth, 0, (int)MaxCounter);
            value.resolutionHeight = ClampInt(value.resolutionHeight, 0, (int)MaxCounter);
            if (value.resolutionWidth == 0 || value.resolutionHeight == 0)
            {
                value.resolutionWidth = 0;
                value.resolutionHeight = 0;
            }
            // Stored as a FullScreenMode value; the cycler only offers
            // 0 (exclusive), 1 (fullscreen window), and 3 (windowed).
            if (value.fullscreenMode != 0 && value.fullscreenMode != 1 && value.fullscreenMode != 3)
                value.fullscreenMode = 1;
            value.bloom = ClampOptional(value.bloom, SaveSettings.MaxBloom);
            value.chromatic = ClampOptional(value.chromatic, SaveSettings.MaxChromatic);
            return value;
        }

        /// <summary>
        /// Clamps an optional effect intensity. Any negative value — including
        /// NaN/Infinity and the -1 "use the shipped default" sentinel —
        /// normalizes to exactly -1; valid values clamp to [0, max].
        /// </summary>
        private static float ClampOptional(float value, float max)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f) return -1f;
            return Mathf.Clamp(value, 0f, max);
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
            var weaponIds = WeaponIds();
            var weaponMaxRanks = WeaponMaxRanks();
            var supportIds = SupportIds();
            var supportMaxRanks = SupportMaxRanks();
            var lateIds = LateIds();
            var lateMaxRanks = LateMaxRanks();
            var evolvedMaxRanks = Ones(weaponIds.Length);
            for (var i = 0; i < source.Length; i++)
            {
                var value = source[i];
                if (value == null) continue;
                SanitizeHighScore(value);
                value.damageDealt = ClampLong(value.damageDealt, 0, MaxDamageCounter);
                value.damageTaken = ClampLong(value.damageTaken, 0, MaxDamageCounter);
                value.weapons = SanitizeKnownEntries(value.weapons, weaponIds, weaponMaxRanks);
                value.weaponDamage = SanitizeKnownWeaponDamage(value.weaponDamage, weaponIds);
                value.supports = SanitizeKnownEntries(value.supports, supportIds, supportMaxRanks);
                value.late = SanitizeKnownEntries(value.late, lateIds, lateMaxRanks);
                value.evolved = SanitizeKnownEntries(value.evolved, weaponIds, evolvedMaxRanks);
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
            // An unrepresentable date becomes "oldest", matching the browser's
            // integer(value.date, 0, ...) fallback. Returning a far-future
            // sentinel here instead sorted the junk entry to the front of
            // recentRuns and evicted a genuine run at the 12-entry cap.
            if (value > DateTime.MaxValue.Ticks) return 0;
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

        private static WeaponDamageEntry[] SanitizeKnownWeaponDamage(WeaponDamageEntry[] entries, string[] ids)
        {
            var result = new List<WeaponDamageEntry>();
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
            var supports = ExtendedCatalog.AllSupports();
            var ids = new string[supports.Length];
            for (var index = 0; index < ids.Length; index++) ids[index] = supports[index].Id;
            return ids;
        }

        private static int[] SupportMaxRanks()
        {
            var ranks = new int[ExtendedCatalog.SupportCount];
            for (var index = 0; index < ranks.Length; index++) ranks[index] = ExtendedCatalog.AllSupports()[index].MaxRank;
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
