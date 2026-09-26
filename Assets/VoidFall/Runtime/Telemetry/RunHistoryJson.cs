using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace VoidFall.Runtime
{
    /// <summary>
    /// History schema 5 retains scalar fields and serializes only present
    /// payloads. JsonUtility expands null nested DTOs into thousands of bytes
    /// of empty defaults; ordinary combat events must not carry those trees.
    /// The returned string is immutable before the background writer sees it.
    /// </summary>
    public static class RunHistoryJson
    {
        public const int SchemaVersion = 5;
        public static string Serialize(UnityTelemetryHistoryEvent value)
            => Serialize(value, new StringBuilder(768));

        internal static string Serialize(UnityTelemetryHistoryEvent value, StringBuilder json)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            json.Clear();
            json.Append('{');
            Number(json, "schemaVersion", value.schemaVersion);
            Number(json, "sequence", value.sequence);
            Number(json, "timeSeconds", value.timeSeconds);
            Number(json, "wallTimeSeconds", value.wallTimeSeconds);
            String(json, "kind", value.kind);
            String(json, "id", value.id);
            String(json, "reason", value.reason);
            String(json, "arenaId", value.arenaId);
            Number(json, "visitIndex", value.visitIndex);
            Number(json, "encounterIndex", value.encounterIndex);
            Number(json, "transitionIndex", value.transitionIndex);
            Number(json, "pressureHundredths", value.pressureHundredths);
            Number(json, "directorId", value.directorId);
            Number(json, "instanceId", value.instanceId);
            Number(json, "relatedInstanceId", value.relatedInstanceId);
            String(json, "sourceId", value.sourceId);
            Number(json, "amount", value.amount);
            Number(json, "hp", value.hp);
            Number(json, "maxHp", value.maxHp);
            Number(json, "speed", value.speed);
            Number(json, "damage", value.damage);
            Number(json, "x", value.x);
            Number(json, "y", value.y);
            Number(json, "rosterTier", value.rosterTier);
            Boolean(json, "elite", value.elite);
            Number(json, "activeEnemies", value.activeEnemies);
            Number(json, "challengeSeconds", value.challengeSeconds);
            Payload(json, "progress", value.progress);
            Strings(json, "options", value.options);
            String(json, "detail", value.detail);
            Number(json, "durationSeconds", value.durationSeconds);
            Number(json, "budgetLimit", value.budgetLimit);
            Number(json, "budgetUsed", value.budgetUsed);
            Number(json, "blockedAttempts", value.blockedAttempts);
            Number(json, "level", value.level);
            Number(json, "nextXp", value.nextXp);
            Number(json, "bufferedXp", value.bufferedXp);
            Payload(json, "sample", value.sample);
            Payload(json, "context", value.context);
            json.Append('}');
            return json.ToString();
        }

        private static void Key(StringBuilder json, string name)
        {
            if (json.Length > 1) json.Append(',');
            json.Append('"').Append(name).Append("\":");
        }
        private static void Number(StringBuilder json, string name, long value)
        {
            Key(json, name);
            json.Append(value.ToString(CultureInfo.InvariantCulture));
        }
        private static void Number(StringBuilder json, string name, float value)
        {
            Key(json, name);
            json.Append(float.IsNaN(value) || float.IsInfinity(value) ? "0" : value.ToString("R", CultureInfo.InvariantCulture));
        }
        private static void Boolean(StringBuilder json, string name, bool value)
        {
            Key(json, name);
            json.Append(value ? "true" : "false");
        }
        private static void String(StringBuilder json, string name, string value)
        {
            if (value == null) return;
            Key(json, name);
            Quote(json, value);
        }
        private static void Strings(StringBuilder json, string name, string[] values)
        {
            if (values == null) return;
            Key(json, name);
            json.Append('[');
            for (var i = 0; i < values.Length; i++)
            {
                if (i != 0) json.Append(',');
                if (values[i] == null) json.Append("null");
                else Quote(json, values[i]);
            }
            json.Append(']');
        }
        private static void Payload(StringBuilder json, string name, object value)
        {
            if (value == null) return;
            Key(json, name);
            json.Append(JsonUtility.ToJson(value));
        }
        private static void Quote(StringBuilder json, string value)
        {
            json.Append('"');
            foreach (var character in value)
            {
                switch (character)
                {
                    case '"': json.Append("\\\""); break;
                    case '\\': json.Append("\\\\"); break;
                    case '\n': json.Append("\\n"); break;
                    case '\r': json.Append("\\r"); break;
                    case '\t': json.Append("\\t"); break;
                    default:
                        if (character < 32) json.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        else json.Append(character);
                        break;
                }
            }
            json.Append('"');
        }
    }
}
