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

        private static SaveData CloneSaveData(SaveData data)
            => data == null ? null : SaveStore.Clone(data);

        private void SaveRun()
        {
            // Browser recordRun runs only after the terminal Game Over path. Do
            // not turn an application close during a live run into a fake score.
            if (_runSaved || _mainMenuBrowsing || !_gameOver) return;
            FreezePressureAndScore();
            _lastRunSaved = false;
            if (_saveStore == null || _saveData == null)
            {
                SetMenuNotice("Progress was not saved.");
                return;
            }
            var candidate = SaveStore.Sanitize(CloneSaveData(_saveData));
            var savedDamageDealt = RoundedDamageCounter(_damageDealt);
            var savedDamageTaken = RoundedDamageCounter(_damageTaken);
            var previousBestScore = candidate.highScores != null && candidate.highScores.Length > 0 && candidate.highScores[0] != null
                ? SaveStore.ScoreForRanking(candidate.highScores[0])
                : -1;
            candidate.stats.totalRuns = AddCounter(candidate.stats.totalRuns, 1);
            candidate.stats.totalPlaySeconds = AddCounter(
                candidate.stats.totalPlaySeconds, Mathf.Max(0, Mathf.FloorToInt(_time)));
            candidate.stats.totalKills = AddCounter(candidate.stats.totalKills, _kills);
            candidate.stats.totalEliteKills = AddCounter(candidate.stats.totalEliteKills, _eliteKills);
            candidate.stats.totalBossKills = AddCounter(candidate.stats.totalBossKills, _bossKills);
            candidate.stats.totalDamageDealt = AddDamageCounter(candidate.stats.totalDamageDealt, savedDamageDealt);
            candidate.stats.totalDamageTaken = AddDamageCounter(candidate.stats.totalDamageTaken, savedDamageTaken);
            candidate.stats.totalPartsEarned = AddCounter(candidate.stats.totalPartsEarned, _partsEarned);
            // The browser keeps run rewards in partsEarned until recordRun at
            // terminal Game Over. Commit the complete run total here so
            // pickups, elite/boss rewards, and tune-limit Scraps are all saved
            // once and a live run cannot mutate the profile early.
            CommitRunParts(candidate, _partsEarned);
            candidate.stats.bestScore = Mathf.Max(candidate.stats.bestScore, (int)Math.Min(999_999_999L, _frozenRunScore.BaseScore));
            candidate.stats.bestFinalScore = Math.Max(candidate.stats.bestFinalScore, _frozenRunScore.FinalScore);
            candidate.stats.bestTime = Mathf.Max(candidate.stats.bestTime, Mathf.FloorToInt(_time));
            candidate.stats.bestKills = Mathf.Max(candidate.stats.bestKills, _kills);
            candidate.stats.highestLevel = Mathf.Max(candidate.stats.highestLevel, _level);
            // Totals already include this run. Stage gates once, without an
            // intermediate save or a success toast before the terminal commit.
            var newlyUnlocked = StageFormProgress(candidate, candidate.stats.totalBossKills);

            var run = new RunRecordEntry
            {
                score = (int)Math.Min(999_999_999L, _frozenRunScore.BaseScore),
                baseScore = _frozenRunScore.BaseScore,
                pressureHundredths = _frozenRunScore.PressureHundredths,
                multiplierHundredths = _frozenRunScore.MultiplierHundredths,
                finalScore = _frozenRunScore.FinalScore,
                scoringVersion = RunScoreRules.Version,
                directorId = (int)_runDirectorProfile,
                kills = _kills,
                time = Mathf.Max(0, Mathf.FloorToInt(_time)),
                level = Mathf.Max(1, _level),
                eliteKills = _eliteKills,
                bossKills = _bossKills,
                partsEarned = _partsEarned,
                date = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                damageDealt = savedDamageDealt,
                damageTaken = savedDamageTaken,
                weapons = BuildRankEntries(WeaponIds(), _upgradeProgress?.WeaponRanks),
                weaponDamage = BuildWeaponDamageEntries(),
                supports = BuildRankEntries(SupportIds(), _upgradeProgress?.SupportRanks),
                late = BuildRankEntries(LateIds(), _upgradeProgress?.LateRanks),
                evolved = BuildEvolvedEntries(),
            };
            var lastRunIsBest = run.finalScore > previousBestScore;
            var recentRuns = new List<RunRecordEntry>();
            foreach (var previous in candidate.recentRuns ?? Array.Empty<RunRecordEntry>())
            {
                if (previous != null) recentRuns.Add(previous);
            }
            recentRuns.Insert(0, run);
            if (recentRuns.Count > SaveStore.MaxRecentRuns)
                recentRuns.RemoveRange(SaveStore.MaxRecentRuns, recentRuns.Count - SaveStore.MaxRecentRuns);
            candidate.recentRuns = recentRuns.ToArray();

            var scoreEntry = new HighScoreEntry
            {
                score = run.score,
                baseScore = run.baseScore,
                pressureHundredths = run.pressureHundredths,
                multiplierHundredths = run.multiplierHundredths,
                finalScore = run.finalScore,
                scoringVersion = run.scoringVersion,
                directorId = run.directorId,
                kills = run.kills,
                time = run.time,
                level = run.level,
                eliteKills = run.eliteKills,
                bossKills = run.bossKills,
                partsEarned = run.partsEarned,
                date = run.date,
            };
            var highScores = new List<HighScoreEntry>();
            foreach (var previous in candidate.highScores ?? Array.Empty<HighScoreEntry>())
            {
                if (previous != null) highScores.Add(previous);
            }
            highScores.Add(scoreEntry);
            highScores.Sort(SaveStore.CompareScores);
            var rawRank = highScores.IndexOf(scoreEntry);
            var lastRunRank = rawRank >= 0 && rawRank < SaveStore.MaxHighScores ? rawRank : -1;
            if (highScores.Count > SaveStore.MaxHighScores)
                highScores.RemoveRange(SaveStore.MaxHighScores, highScores.Count - SaveStore.MaxHighScores);
            candidate.highScores = highScores.ToArray();
            try
            {
                _saveStore.Save(candidate);
                _saveData = candidate;
                _lastRunIsBest = lastRunIsBest;
                _lastRunRank = lastRunRank;
                _runSaved = true;
                _lastRunSaved = true;
                AnnounceFormUnlocks(newlyUnlocked);
            }
            catch (Exception exception)
            {
                Debug.LogError("VoidFall run save failed: " + exception.Message);
            }
            if (!_lastRunSaved) SetMenuNotice("Progress was not saved.");
        }

        private void ExportTelemetrySnapshot(string status)
        {
            // The manual entry point shares the automatic writer and stays silent.
            if (!_runExportActive && _runExportStatus == null) return;
            status = _runExportStatus ?? status;
            _lastTelemetryPath = _telemetry.Export(
                status,
                (float)_time,
                (int)Math.Min(int.MaxValue, _hasFrozenRunScore ? _frozenRunScore.BaseScore : CurrentEarnedBaseScore()),
                _kills,
                _eliteKills,
                _bossKills,
                _level,
                RoundedDamageCounter(_damageDealt),
                RoundedDamageCounter(_damageTaken),
                _partsEarned,
                ActiveBosses(),
                ActiveEnemies(),
                ActivePickups(),
                BuildTelemetryProgress(),
                BuildTelemetryDamage(),
                Mathf.FloorToInt(XpOnGround()),
                XpHeldByHarvesters(),
                null,
                _hasFrozenRunScore ? _frozenRunScore : new FrozenRunScore(CurrentEarnedBaseScore(), PressureHundredths),
                (int)_runDirectorProfile, _hasFrozenRunScore);
            var error = _telemetry.LastExportError ?? _telemetry.HistoryInfo?.lastError;
            if (!string.IsNullOrEmpty(error) && error != _runExportLastError)
            {
                _runExportLastError = error;
                Debug.LogWarning("VoidFall run export I/O error: " + error);
            }
        }

        private void ExportBrowserSave()
        {
            try
            {
                if (!CommitSettings()) return;
                var path = System.IO.Path.Combine(
                    Application.persistentDataPath,
                    "VoidFallBrowserSave.json");
                BrowserSaveExporter.WriteFile(path, _saveData);
                SetMenuNotice("Browser save exported: " + path);
            }
            catch (Exception exception)
            {
                SetMenuNotice("Browser save export failed: " + exception.Message);
            }
        }

        private void ImportBrowserSave()
        {
            if (!_mainMenuBrowsing && !_gameOver)
            {
                SetMenuNotice("Import browser save from the main menu.");
                return;
            }

            if (_saveStore == null)
            {
                SetMenuNotice("Browser save import is unavailable.");
                return;
            }

            if (!_saveStore.TryImportBrowserSave(
                    _browserSaveImportText,
                    out var imported,
                    out var error))
            {
                SetMenuNotice("Browser save import failed: " + error);
                return;
            }

            _saveData = imported;
            _settingsController.MarkClean();
            _resetProgressArmed = false;
            _resetProgressTimer = 0;
            _workshopPreviewId = null;
            _workshopFocusedId = null;
            _browserSaveImportText = string.Empty;
            ApplySettings();
            SetMenuNotice("Browser save imported.");
        }

        private GUIStyle ResultSaveWarningStyle()
        {
            if (_resultSaveWarningStyle == null)
            {
                _resultSaveWarningStyle = CreateResultBadgeStyle(
                    new Color(0.498f, 0.114f, 0.114f, 0.20f),
                    new Color(0.973f, 0.443f, 0.443f, 0.32f),
                    new Color(0.996f, 0.796f, 0.796f, 1f),
                    "VoidFall Result Save Warning");
            }
            return _resultSaveWarningStyle;
        }

        // Death report: killed-by attribution for the run-result screen.
        // Every accepted player hit records its source id; EndRun reads the
        // final one. All state here is presentational: it never feeds
        // simulation, RNG or the golden-master hash.
        private string _lastHitSourceId = string.Empty;
        private bool _lastHitElite;

        private void ResetDeathReport()
        {
            _lastHitSourceId = string.Empty;
            _lastHitElite = false;
            if (_gameSim.HostileShotKillerIds != null)
                System.Array.Clear(_gameSim.HostileShotKillerIds, 0, _gameSim.HostileShotKillerIds.Length);
            if (_gameSim.HostileShotElite != null)
                System.Array.Clear(_gameSim.HostileShotElite, 0, _gameSim.HostileShotElite.Length);
        }

        private void ResolveDeathSource(out string name, out string glyph, out Color accent)
        {
            glyph = "\u25C6";
            accent = ParseColor("#fb7185", new Color(0.98f, 0.44f, 0.52f));
            name = PrettifySourceId(_lastHitSourceId);
            if (string.IsNullOrEmpty(_lastHitSourceId)) return;

            string environmental;
            if (TryEnvironmentalSource(_lastHitSourceId, out environmental))
            {
                name = environmental;
                glyph = "\u25B2";
                return;
            }

            var boss = FindBoss(_lastHitSourceId) ??
                NullCityContent.FindBoss(_lastHitSourceId);
            if (boss != null)
            {
                name = string.IsNullOrEmpty(boss.Name) ? PrettifySourceId(_lastHitSourceId) : boss.Name;
                accent = ParseColor(boss.Color, accent);
                return;
            }

            var enemy = FindDeathEnemyDefinition(_lastHitSourceId);
            if (enemy != null)
            {
                name = string.IsNullOrEmpty(enemy.Name) ? PrettifySourceId(_lastHitSourceId) : enemy.Name;
                accent = ParseColor(enemy.Color, accent);
            }

            if (_lastHitElite)
            {
                name = "Elite " + name;
                accent = ParseColor("#facc15", accent);
            }
        }

        private static EnemyDefinition FindDeathEnemyDefinition(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (var index = 0; index < ContentCatalog.Enemies.Length; index++)
            {
                if (ContentCatalog.Enemies[index].Id == id) return ContentCatalog.Enemies[index];
            }
            var court = MonochromeContent.FindEnemy(id);
            if (court != null) return court;
            var nullIndex = NullCityContent.EnemyIndex(id);
            if (nullIndex >= 0) return NullCityContent.Enemies[nullIndex];
            return null;
        }

        private static bool TryEnvironmentalSource(string id, out string name)
        {
            switch (id)
            {
                case "lane-strike": name = "Meteor lane"; return true;
                case "explosive-meteor": name = "Explosive meteor"; return true;
                case "meteor-shard": name = "Meteor shard"; return true;
                case "chess-floor": name = "Chess floor"; return true;
                case "null-purge": name = "Null City purge"; return true;
                case "null-bomb": name = "Null City bomb"; return true;
                default: name = null; return false;
            }
        }

        private static string PrettifySourceId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "The Void";
            var parts = id.Split('-');
            for (var index = 0; index < parts.Length; index++)
            {
                if (parts[index].Length == 0) continue;
                parts[index] = char.ToUpperInvariant(parts[index][0]) + parts[index].Substring(1);
            }
            return string.Join(" ", parts);
        }

        private static string FormatDeathTime(float seconds)
        {
            var total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (total / 60).ToString() + ":" + (total % 60).ToString("00");
        }
    }
}
