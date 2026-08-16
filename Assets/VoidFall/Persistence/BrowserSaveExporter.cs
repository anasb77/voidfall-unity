using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace VoidFall.Persistence
{
    /// <summary>
    /// Emits the browser's v5 save shape, including its object maps. This is
    /// intentionally separate from JsonUtility so a Unity profile can be
    /// moved back to the browser without losing dynamic map fields.
    /// </summary>
    public static class BrowserSaveExporter
    {
        public static string Export(SaveData value)
        {
            // SaveStore.Sanitize clamps in place and hands back the same
            // reference, so sanitizing the caller's profile here would mutate
            // live game state as a side effect of an export. Detach first.
            var save = SaveStore.Sanitize(Clone(value));
            var json = new StringBuilder(4096);
            json.Append('{');
            Property(json, "version");
            json.Append(save.version);
            json.Append(',');
            Property(json, "parts");
            json.Append(save.parts);
            json.Append(',');
            Property(json, "settings");
            AppendSettings(json, save.settings);
            json.Append(',');
            Property(json, "workshop");
            AppendWorkshopMap(json, save.workshop);
            json.Append(',');
            Property(json, "stats");
            AppendStats(json, save.stats);
            json.Append(',');
            Property(json, "highScores");
            AppendHighScores(json, save.highScores);
            json.Append(',');
            Property(json, "recentRuns");
            AppendRuns(json, save.recentRuns);
            json.Append(',');
            Property(json, "bestiary");
            AppendBestiaryMap(json, save.bestiary);
            json.Append(',');
            Property(json, "arena");
            AppendString(json, save.arena);
            json.Append('}');
            return json.ToString();
        }

        /// <summary>
        /// Detached deep copy, so sanitizing for export cannot clamp or reorder
        /// the profile the game is still running against.
        /// </summary>
        private static SaveData Clone(SaveData value)
        {
            if (value == null) return SaveStore.CreateDefault();
            var copy = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(value));
            return copy ?? SaveStore.CreateDefault();
        }

        private static void AppendSettings(StringBuilder json, SaveSettings settings)
        {
            var value = settings ?? new SaveSettings();
            json.Append('{');
            Property(json, "masterVolume");
            AppendFloat(json, value.masterVolume);
            json.Append(',');
            Property(json, "effectsVolume");
            AppendFloat(json, value.effectsVolume);
            json.Append(',');
            Property(json, "musicVolume");
            AppendFloat(json, value.musicVolume);
            json.Append(',');
            Property(json, "shake");
            AppendFloat(json, value.shake);
            json.Append(',');
            Property(json, "reducedMotion");
            json.Append(value.reducedMotion ? "true" : "false");
            json.Append(',');
            Property(json, "highContrast");
            json.Append(value.highContrast ? "true" : "false");
            json.Append(',');
            Property(json, "touchSize");
            AppendFloat(json, value.touchSize);
            json.Append(',');
            Property(json, "quality");
            AppendString(json, value.quality);
            json.Append('}');
        }

        private static void AppendStats(StringBuilder json, LifetimeStats stats)
        {
            var value = stats ?? new LifetimeStats();
            json.Append('{');
            Property(json, "totalRuns");
            json.Append(value.totalRuns);
            json.Append(',');
            Property(json, "totalPlaySeconds");
            json.Append(value.totalPlaySeconds);
            json.Append(',');
            Property(json, "totalKills");
            json.Append(value.totalKills);
            json.Append(',');
            Property(json, "totalEliteKills");
            json.Append(value.totalEliteKills);
            json.Append(',');
            Property(json, "totalBossKills");
            json.Append(value.totalBossKills);
            json.Append(',');
            Property(json, "totalDamageDealt");
            json.Append(value.totalDamageDealt);
            json.Append(',');
            Property(json, "totalDamageTaken");
            json.Append(value.totalDamageTaken);
            json.Append(',');
            Property(json, "totalPartsEarned");
            json.Append(value.totalPartsEarned);
            json.Append(',');
            Property(json, "bestScore");
            json.Append(value.bestScore);
            json.Append(',');
            Property(json, "bestTime");
            json.Append(value.bestTime);
            json.Append(',');
            Property(json, "bestKills");
            json.Append(value.bestKills);
            json.Append(',');
            Property(json, "highestLevel");
            json.Append(value.highestLevel);
            json.Append('}');
        }

        private static void AppendHighScores(StringBuilder json, HighScoreEntry[] entries)
        {
            json.Append('[');
            var first = true;
            foreach (var entry in entries ?? Array.Empty<HighScoreEntry>())
            {
                if (entry == null) continue;
                if (!first) json.Append(',');
                first = false;
                json.Append('{');
                AppendScoreFields(json, entry);
                json.Append('}');
            }
            json.Append(']');
        }

        private static void AppendRuns(StringBuilder json, RunRecordEntry[] entries)
        {
            json.Append('[');
            var first = true;
            foreach (var entry in entries ?? Array.Empty<RunRecordEntry>())
            {
                if (entry == null) continue;
                if (!first) json.Append(',');
                first = false;
                json.Append('{');
                AppendScoreFields(json, entry);
                json.Append(',');
                Property(json, "damageDealt");
                json.Append(entry.damageDealt);
                json.Append(',');
                Property(json, "damageTaken");
                json.Append(entry.damageTaken);
                json.Append(',');
                Property(json, "weapons");
                AppendWorkshopMap(json, entry.weapons);
                json.Append(',');
                Property(json, "weaponDamage");
                AppendWeaponDamageMap(json, entry.weaponDamage);
                // React's v5 RunRecord declares no per-run supports, late, or
                // evolved fields, but its sanitizeRunRecord() rebuilds the
                // record from known keys and ignores unrecognized ones. Emitting
                // Unity's richer snapshots is therefore still browser-readable,
                // and it makes export -> import lossless. Omitting them meant a
                // player who exported and re-imported destroyed these three
                // arrays for all twelve retained runs.
                json.Append(',');
                Property(json, "supports");
                AppendWorkshopMap(json, entry.supports);
                json.Append(',');
                Property(json, "late");
                AppendWorkshopMap(json, entry.late);
                json.Append(',');
                Property(json, "evolved");
                AppendWorkshopMap(json, entry.evolved);
                json.Append('}');
            }
            json.Append(']');
        }

        private static void AppendScoreFields(StringBuilder json, HighScoreEntry entry)
        {
            Property(json, "score");
            json.Append(entry.score);
            json.Append(',');
            Property(json, "kills");
            json.Append(entry.kills);
            json.Append(',');
            Property(json, "time");
            json.Append(entry.time);
            json.Append(',');
            Property(json, "level");
            json.Append(entry.level);
            json.Append(',');
            Property(json, "eliteKills");
            json.Append(entry.eliteKills);
            json.Append(',');
            Property(json, "bossKills");
            json.Append(entry.bossKills);
            json.Append(',');
            Property(json, "partsEarned");
            json.Append(entry.partsEarned);
            json.Append(',');
            Property(json, "date");
            json.Append(entry.date);
        }

        private static void AppendWorkshopMap(StringBuilder json, WorkshopEntry[] entries)
        {
            json.Append('{');
            var first = true;
            foreach (var entry in entries ?? Array.Empty<WorkshopEntry>())
            {
                if (entry == null || string.IsNullOrEmpty(entry.id)) continue;
                if (!first) json.Append(',');
                first = false;
                AppendString(json, entry.id);
                json.Append(':').Append(entry.rank);
            }
            json.Append('}');
        }

        private static void AppendWeaponDamageMap(StringBuilder json, WeaponDamageEntry[] entries)
        {
            json.Append('{');
            var first = true;
            foreach (var entry in entries ?? Array.Empty<WeaponDamageEntry>())
            {
                if (entry == null || string.IsNullOrEmpty(entry.id)) continue;
                if (!first) json.Append(',');
                first = false;
                AppendString(json, entry.id);
                json.Append(':').Append(entry.damage);
            }
            json.Append('}');
        }

        private static void AppendBestiaryMap(StringBuilder json, BestiaryEntry[] entries)
        {
            json.Append('{');
            var first = true;
            foreach (var entry in entries ?? Array.Empty<BestiaryEntry>())
            {
                if (entry == null || string.IsNullOrEmpty(entry.id)) continue;
                if (!first) json.Append(',');
                first = false;
                AppendString(json, entry.id);
                json.Append(':').Append(entry.discovered ? "true" : "false");
            }
            json.Append('}');
        }

        private static void Property(StringBuilder json, string name)
        {
            AppendString(json, name);
            json.Append(':');
        }

        private static void AppendFloat(StringBuilder json, float value)
        {
            json.Append(value.ToString("0.########", CultureInfo.InvariantCulture));
        }

        private static void AppendString(StringBuilder json, string value)
        {
            json.Append('"');
            foreach (var character in value ?? string.Empty)
            {
                switch (character)
                {
                    case '"': json.Append("\\\""); break;
                    case '\\': json.Append("\\\\"); break;
                    case '\b': json.Append("\\b"); break;
                    case '\f': json.Append("\\f"); break;
                    case '\n': json.Append("\\n"); break;
                    case '\r': json.Append("\\r"); break;
                    case '\t': json.Append("\\t"); break;
                    default:
                        if (character < 0x20)
                            json.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        else json.Append(character);
                        break;
                }
            }
            json.Append('"');
        }
    }
}
