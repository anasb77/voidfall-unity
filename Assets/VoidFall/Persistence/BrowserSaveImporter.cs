using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace VoidFall.Persistence
{
    /// <summary>
    /// Converts the browser save shape into the array-based Unity save shape.
    /// Unity's JsonUtility cannot deserialize dynamic object maps, which are
    /// used by the browser for workshop, bestiary, and run-record fields.
    /// </summary>
    public static class BrowserSaveImporter
    {
        public static bool TryConvert(string json, out SaveData data)
        {
            data = null;
            if (string.IsNullOrWhiteSpace(json)) return false;

            try
            {
                var root = new JsonParser(json).Parse() as Dictionary<string, object>;
                if (root == null || GetObject(root, "workshop") == null || GetObject(root, "bestiary") == null)
                    return false;

                data = new SaveData
                {
                    version = GetInt(root, "version", 0),
                    parts = GetInt(root, "parts", 0),
                    settings = ReadSettings(GetObject(root, "settings")),
                    workshop = ReadEntries(GetObject(root, "workshop")),
                    stats = ReadStats(GetObject(root, "stats")),
                    highScores = ReadHighScores(GetList(root, "highScores")),
                    recentRuns = ReadRuns(GetList(root, "recentRuns")),
                    bestiary = ReadBestiary(GetObject(root, "bestiary")),
                    arena = GetString(root, "arena", "void"),
                };
                return true;
            }
            catch
            {
                data = null;
                return false;
            }
        }

        public static bool TryConvertLegacyScores(string json, out SaveData data)
        {
            data = null;
            if (string.IsNullOrWhiteSpace(json)) return false;

            try
            {
                var values = new JsonParser(json).Parse() as List<object>;
                if (values == null) return false;

                data = SaveStore.CreateDefault();
                data.highScores = ReadHighScores(values);
                data = SaveStore.Sanitize(data);
                return true;
            }
            catch
            {
                data = null;
                return false;
            }
        }

        private static SaveSettings ReadSettings(Dictionary<string, object> source)
        {
            return new SaveSettings
            {
                masterVolume = GetFloat(source, "masterVolume", 0.8f),
                effectsVolume = GetFloat(source, "effectsVolume", 0.9f),
                musicVolume = GetFloat(source, "musicVolume", 0.7f),
                shake = GetFloat(source, "shake", 0.8f),
                reducedMotion = GetBool(source, "reducedMotion", false),
                highContrast = GetBool(source, "highContrast", false),
                touchSize = GetFloat(source, "touchSize", 1f),
                quality = GetString(source, "quality", "high"),
            };
        }

        private static LifetimeStats ReadStats(Dictionary<string, object> source)
        {
            return new LifetimeStats
            {
                totalRuns = GetInt(source, "totalRuns", 0),
                totalPlaySeconds = GetInt(source, "totalPlaySeconds", 0),
                totalKills = GetInt(source, "totalKills", 0),
                totalEliteKills = GetInt(source, "totalEliteKills", 0),
                totalBossKills = GetInt(source, "totalBossKills", 0),
                totalDamageDealt = GetLong(source, "totalDamageDealt", 0),
                totalDamageTaken = GetLong(source, "totalDamageTaken", 0),
                totalPartsEarned = GetInt(source, "totalPartsEarned", 0),
                bestScore = GetInt(source, "bestScore", 0),
                bestTime = GetInt(source, "bestTime", 0),
                bestKills = GetInt(source, "bestKills", 0),
                highestLevel = GetInt(source, "highestLevel", 1),
            };
        }

        private static HighScoreEntry[] ReadHighScores(List<object> values)
        {
            if (values == null) return Array.Empty<HighScoreEntry>();
            var result = new List<HighScoreEntry>(values.Count);
            foreach (var value in values)
            {
                var source = value as Dictionary<string, object>;
                if (source == null) continue;
                result.Add(ReadHighScore(source));
            }
            return result.ToArray();
        }

        private static RunRecordEntry[] ReadRuns(List<object> values)
        {
            if (values == null) return Array.Empty<RunRecordEntry>();
            var result = new List<RunRecordEntry>(values.Count);
            foreach (var value in values)
            {
                var source = value as Dictionary<string, object>;
                if (source == null) continue;
                var record = new RunRecordEntry
                {
                    score = GetInt(source, "score", 0),
                    kills = GetInt(source, "kills", 0),
                    time = GetInt(source, "time", 0),
                    level = GetInt(source, "level", 1),
                    eliteKills = GetInt(source, "eliteKills", 0),
                    bossKills = GetInt(source, "bossKills", 0),
                    partsEarned = GetInt(source, "partsEarned", 0),
                    date = GetLong(source, "date", 0),
                    damageDealt = GetLong(source, "damageDealt", 0),
                    damageTaken = GetLong(source, "damageTaken", 0),
                    weapons = ReadEntries(GetObject(source, "weapons")),
                    weaponDamage = ReadWeaponDamage(GetObject(source, "weaponDamage")),
                    supports = ReadEntries(GetObject(source, "supports")),
                    late = ReadEntries(GetObject(source, "late")),
                    evolved = ReadEntries(GetObject(source, "evolved")),
                };
                result.Add(record);
            }
            return result.ToArray();
        }

        private static HighScoreEntry ReadHighScore(Dictionary<string, object> source)
        {
            return new HighScoreEntry
            {
                score = GetInt(source, "score", 0),
                kills = GetInt(source, "kills", 0),
                time = GetInt(source, "time", 0),
                level = GetInt(source, "level", 1),
                eliteKills = GetInt(source, "eliteKills", 0),
                bossKills = GetInt(source, "bossKills", 0),
                partsEarned = GetInt(source, "partsEarned", 0),
                date = GetLong(source, "date", 0),
            };
        }

        private static WorkshopEntry[] ReadEntries(Dictionary<string, object> source)
        {
            if (source == null) return Array.Empty<WorkshopEntry>();
            var result = new List<WorkshopEntry>(source.Count);
            foreach (var pair in source)
                result.Add(new WorkshopEntry { id = pair.Key, rank = GetInt(pair.Value, 0) });
            return result.ToArray();
        }

        private static WeaponDamageEntry[] ReadWeaponDamage(Dictionary<string, object> source)
        {
            if (source == null) return Array.Empty<WeaponDamageEntry>();
            var result = new List<WeaponDamageEntry>(source.Count);
            foreach (var pair in source)
                result.Add(new WeaponDamageEntry { id = pair.Key, damage = GetLong(pair.Value, 0) });
            return result.ToArray();
        }

        private static BestiaryEntry[] ReadBestiary(Dictionary<string, object> source)
        {
            if (source == null) return Array.Empty<BestiaryEntry>();
            var result = new List<BestiaryEntry>(source.Count);
            foreach (var pair in source)
                result.Add(new BestiaryEntry { id = pair.Key, discovered = GetBool(pair.Value, false) });
            return result.ToArray();
        }

        private static Dictionary<string, object> GetObject(Dictionary<string, object> source, string key)
        {
            if (source == null || !source.TryGetValue(key, out var value)) return null;
            return value as Dictionary<string, object>;
        }

        private static List<object> GetList(Dictionary<string, object> source, string key)
        {
            if (source == null || !source.TryGetValue(key, out var value)) return null;
            return value as List<object>;
        }

        private static string GetString(Dictionary<string, object> source, string key, string fallback)
        {
            if (source == null || !source.TryGetValue(key, out var value)) return fallback;
            return value as string ?? fallback;
        }

        private static float GetFloat(Dictionary<string, object> source, string key, float fallback)
        {
            if (source == null || !source.TryGetValue(key, out var value)) return fallback;
            return GetFloat(value, fallback);
        }

        private static float GetFloat(object value, float fallback)
        {
            if (!TryGetDouble(value, out var number) || double.IsNaN(number) || double.IsInfinity(number))
                return fallback;
            return (float)number;
        }

        private static int GetInt(Dictionary<string, object> source, string key, int fallback)
        {
            if (source == null || !source.TryGetValue(key, out var value)) return fallback;
            return GetInt(value, fallback);
        }

        private static int GetInt(object value, int fallback)
        {
            if (value is bool boolean) return boolean ? 1 : 0;
            if (!TryGetDouble(value, out var number) || double.IsNaN(number) || double.IsInfinity(number))
                return fallback;
            if (number <= int.MinValue) return int.MinValue;
            if (number >= int.MaxValue) return int.MaxValue;
            return (int)Math.Round(number, MidpointRounding.AwayFromZero);
        }

        private static long GetLong(Dictionary<string, object> source, string key, long fallback)
        {
            if (source == null || !source.TryGetValue(key, out var value)) return fallback;
            return GetLong(value, fallback);
        }

        private static long GetLong(object value, long fallback)
        {
            if (!TryGetDouble(value, out var number) || double.IsNaN(number) || double.IsInfinity(number))
                return fallback;
            if (number <= long.MinValue) return long.MinValue;
            if (number >= long.MaxValue) return long.MaxValue;
            return (long)Math.Round(number, MidpointRounding.AwayFromZero);
        }

        private static bool GetBool(Dictionary<string, object> source, string key, bool fallback)
        {
            if (source == null || !source.TryGetValue(key, out var value)) return fallback;
            return GetBool(value, fallback);
        }

        private static bool GetBool(object value, bool fallback)
        {
            if (value is bool boolean) return boolean;
            return fallback;
        }

        private static bool TryGetDouble(object value, out double number)
        {
            switch (value)
            {
                case long integer:
                    number = integer;
                    return true;
                case double floating:
                    number = floating;
                    return true;
                case int integer32:
                    number = integer32;
                    return true;
                default:
                    number = 0;
                    return false;
            }
        }

        private sealed class JsonParser
        {
            private readonly string _json;
            private int _index;

            public JsonParser(string json)
            {
                _json = json;
            }

            public object Parse()
            {
                SkipWhitespace();
                var value = ParseValue();
                SkipWhitespace();
                if (_index != _json.Length) throw new FormatException("Unexpected JSON content.");
                return value;
            }

            private object ParseValue()
            {
                SkipWhitespace();
                switch (Current)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case 't': ConsumeLiteral("true"); return true;
                    case 'f': ConsumeLiteral("false"); return false;
                    case 'n': ConsumeLiteral("null"); return null;
                    default: return ParseNumber();
                }
            }

            private Dictionary<string, object> ParseObject()
            {
                Expect('{');
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                SkipWhitespace();
                if (Current == '}')
                {
                    _index++;
                    return result;
                }

                while (true)
                {
                    SkipWhitespace();
                    var key = ParseString();
                    SkipWhitespace();
                    Expect(':');
                    result[key] = ParseValue();
                    SkipWhitespace();
                    if (Current == '}')
                    {
                        _index++;
                        return result;
                    }
                    Expect(',');
                }
            }

            private List<object> ParseArray()
            {
                Expect('[');
                var result = new List<object>();
                SkipWhitespace();
                if (Current == ']')
                {
                    _index++;
                    return result;
                }

                while (true)
                {
                    result.Add(ParseValue());
                    SkipWhitespace();
                    if (Current == ']')
                    {
                        _index++;
                        return result;
                    }
                    Expect(',');
                }
            }

            private string ParseString()
            {
                Expect('"');
                var result = new StringBuilder();
                while (_index < _json.Length)
                {
                    var character = _json[_index++];
                    if (character == '"') return result.ToString();
                    if (character != '\\')
                    {
                        if (character < 0x20) throw new FormatException("Invalid JSON string.");
                        result.Append(character);
                        continue;
                    }

                    if (_index >= _json.Length) throw new FormatException("Incomplete JSON escape.");
                    switch (_json[_index++])
                    {
                        case '"': result.Append('"'); break;
                        case '\\': result.Append('\\'); break;
                        case '/': result.Append('/'); break;
                        case 'b': result.Append('\b'); break;
                        case 'f': result.Append('\f'); break;
                        case 'n': result.Append('\n'); break;
                        case 'r': result.Append('\r'); break;
                        case 't': result.Append('\t'); break;
                        case 'u': result.Append(ParseUnicodeEscape()); break;
                        default: throw new FormatException("Unknown JSON escape.");
                    }
                }

                throw new FormatException("Unterminated JSON string.");
            }

            private char ParseUnicodeEscape()
            {
                if (_index + 4 > _json.Length) throw new FormatException("Incomplete unicode escape.");
                var hex = _json.Substring(_index, 4);
                if (!ushort.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
                    throw new FormatException("Invalid unicode escape.");
                _index += 4;
                return (char)value;
            }

            private object ParseNumber()
            {
                var start = _index;
                if (Current == '-') _index++;
                while (char.IsDigit(Current)) _index++;
                var isFloating = false;
                if (Current == '.')
                {
                    isFloating = true;
                    _index++;
                    while (char.IsDigit(Current)) _index++;
                }
                if (Current == 'e' || Current == 'E')
                {
                    isFloating = true;
                    _index++;
                    if (Current == '-' || Current == '+') _index++;
                    while (char.IsDigit(Current)) _index++;
                }

                if (start == _index) throw new FormatException("Expected JSON value.");
                var token = _json.Substring(start, _index - start);
                if (!isFloating && long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
                    return integer;
                if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var floating))
                    return floating;
                throw new FormatException("Invalid JSON number.");
            }

            private void ConsumeLiteral(string literal)
            {
                if (_index + literal.Length > _json.Length ||
                    string.CompareOrdinal(_json, _index, literal, 0, literal.Length) != 0)
                    throw new FormatException("Invalid JSON literal.");
                _index += literal.Length;
            }

            private void Expect(char expected)
            {
                if (Current != expected) throw new FormatException("Unexpected JSON character.");
                _index++;
            }

            private void SkipWhitespace()
            {
                while (char.IsWhiteSpace(Current)) _index++;
            }

            private char Current => _index < _json.Length ? _json[_index] : '\0';
        }
    }
}
