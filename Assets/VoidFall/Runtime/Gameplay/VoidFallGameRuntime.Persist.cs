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
        {
            if (data == null) return null;
            return JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
        }

        private void RestoreFailedRunSave(
            SaveData previousSaveData,
            bool previousLastRunIsBest,
            int previousLastRunRank)
        {
            _saveData = previousSaveData;
            _lastRunIsBest = previousLastRunIsBest;
            _lastRunRank = previousLastRunRank;
            // Keep the terminal run eligible for a later SaveRun call. The
            // profile snapshot above prevents a failed attempt from being
            // applied again when that retry happens.
            _runSaved = false;
            _lastRunSaved = false;
        }

        private void SaveRun()
        {
            // Browser recordRun runs only after the terminal Game Over path. Do
            // not turn an application close during a live run into a fake score.
            if (_runSaved || _mainMenuBrowsing || !_gameOver) return;
            // Snapshot the complete profile before mutating it. SaveStore.Save
            // serializes its own clone, but this runtime object is updated in
            // place below; restore it if the disk transaction fails.
            var previousSaveData = CloneSaveData(_saveData);
            var previousLastRunIsBest = _lastRunIsBest;
            var previousLastRunRank = _lastRunRank;
            _lastRunSaved = false;
            if (_saveStore == null || _saveData == null)
            {
                RestoreFailedRunSave(
                    previousSaveData,
                    previousLastRunIsBest,
                    previousLastRunRank);
                SetMenuNotice("Progress was not saved.");
                return;
            }
            var savedDamageDealt = RoundedDamageCounter(_damageDealt);
            var savedDamageTaken = RoundedDamageCounter(_damageTaken);
            var previousBestScore = _saveData.highScores != null && _saveData.highScores.Length > 0 && _saveData.highScores[0] != null
                ? _saveData.highScores[0].score
                : -1;
            _saveData.stats.totalRuns = AddCounter(_saveData.stats.totalRuns, 1);
            _saveData.stats.totalPlaySeconds = AddCounter(
                _saveData.stats.totalPlaySeconds, Mathf.Max(0, Mathf.FloorToInt(_time)));
            _saveData.stats.totalKills = AddCounter(_saveData.stats.totalKills, _kills);
            _saveData.stats.totalEliteKills = AddCounter(_saveData.stats.totalEliteKills, _eliteKills);
            _saveData.stats.totalBossKills = AddCounter(_saveData.stats.totalBossKills, _bossKills);
            _saveData.stats.totalDamageDealt = AddDamageCounter(_saveData.stats.totalDamageDealt, savedDamageDealt);
            _saveData.stats.totalDamageTaken = AddDamageCounter(_saveData.stats.totalDamageTaken, savedDamageTaken);
            _saveData.stats.totalPartsEarned = AddCounter(_saveData.stats.totalPartsEarned, _partsEarned);
            // The browser keeps run rewards in partsEarned until recordRun at
            // terminal Game Over. Commit the complete run total here so
            // pickups, elite/boss rewards, and tune-limit Parts are all saved
            // once and a live run cannot mutate the profile early.
            CommitRunParts(_saveData, _partsEarned);
            _saveData.stats.bestScore = Mathf.Max(_saveData.stats.bestScore, CurrentScore());
            _saveData.stats.bestTime = Mathf.Max(_saveData.stats.bestTime, Mathf.FloorToInt(_time));
            _saveData.stats.bestKills = Mathf.Max(_saveData.stats.bestKills, _kills);
            _saveData.stats.highestLevel = Mathf.Max(_saveData.stats.highestLevel, _level);

            var run = new RunRecordEntry
            {
                score = CurrentScore(),
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
            _lastRunIsBest = run.score > previousBestScore;
            var recentRuns = new List<RunRecordEntry>();
            foreach (var previous in _saveData.recentRuns ?? Array.Empty<RunRecordEntry>())
            {
                if (previous != null) recentRuns.Add(previous);
            }
            recentRuns.Insert(0, run);
            if (recentRuns.Count > SaveStore.MaxRecentRuns)
                recentRuns.RemoveRange(SaveStore.MaxRecentRuns, recentRuns.Count - SaveStore.MaxRecentRuns);
            _saveData.recentRuns = recentRuns.ToArray();

            var scoreEntry = new HighScoreEntry
            {
                score = run.score,
                kills = run.kills,
                time = run.time,
                level = run.level,
                eliteKills = run.eliteKills,
                bossKills = run.bossKills,
                partsEarned = run.partsEarned,
                date = run.date,
            };
            var highScores = new List<HighScoreEntry>();
            foreach (var previous in _saveData.highScores ?? Array.Empty<HighScoreEntry>())
            {
                if (previous != null) highScores.Add(previous);
            }
            highScores.Add(scoreEntry);
            highScores.Sort(SaveStore.CompareScores);
            var rawRank = highScores.IndexOf(scoreEntry);
            _lastRunRank = rawRank >= 0 && rawRank < SaveStore.MaxHighScores ? rawRank : -1;
            if (highScores.Count > SaveStore.MaxHighScores)
                highScores.RemoveRange(SaveStore.MaxHighScores, highScores.Count - SaveStore.MaxHighScores);
            _saveData.highScores = highScores.ToArray();
            try
            {
                _saveStore.Save(_saveData);
                _runSaved = true;
                _lastRunSaved = true;
            }
            catch (Exception exception)
            {
                RestoreFailedRunSave(
                    previousSaveData,
                    previousLastRunIsBest,
                    previousLastRunRank);
                Debug.LogError("VoidFall run save failed: " + exception.Message);
            }
            ExportTelemetrySnapshot(_runVictory ? "escaped" : _gameOver ? "gameover" : "active");
            if (!_lastRunSaved) SetMenuNotice("Progress was not saved.");
        }

        private void ExportTelemetrySnapshot(string status)
        {
            if (_time <= 0)
            {
                EnqueueToast("No run data yet", null, 2.2f, ToastKind.Info);
                SetMenuNotice("No run data yet.");
                return;
            }
            _lastTelemetryPath = _telemetry.Export(
                status,
                (float)_time,
                CurrentScore(),
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
                _saveStore == null ? null : System.IO.Path.GetDirectoryName(_saveStore.PathOnDisk));
            if (!string.IsNullOrEmpty(_lastTelemetryPath))
            {
                EnqueueToast("Run data exported", _lastTelemetryPath, 2.2f, ToastKind.Info);
                SetMenuNotice("Run data exported.");
            }
        }

        private void ExportBrowserSave()
        {
            try
            {
                CommitSettings();
                var path = System.IO.Path.Combine(
                    Application.persistentDataPath,
                    "VoidFallBrowserSave.json");
                System.IO.File.WriteAllText(path, BrowserSaveExporter.Export(_saveData));
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
    }
}
