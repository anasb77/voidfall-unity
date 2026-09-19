using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    // Opt-in captures from the canonical player; isolated before profile loading.
    public sealed class LegacyRestorationProbe : MonoBehaviour
    {
        private static string Output => ArsenalValidationProbe.Argument("-vfrestoration-check=");
        internal static string ProfilePath => string.IsNullOrEmpty(Output) ? null : Path.Combine(Path.GetFullPath(Output), "profile.json");
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            if (ProfilePath == null) return;
            var host = new GameObject("Restoration Validation"); DontDestroyOnLoad(host); host.AddComponent<LegacyRestorationProbe>();
        }
        private IEnumerator Start()
        {
            Application.runInBackground = true;
            yield return null; yield return null;
            var runtime = FindAnyObjectByType<VoidFallGameRuntime>();
            if (runtime == null) { Application.Quit(1); yield break; }
            yield return runtime.CaptureLegacyRestoration(Path.GetFullPath(Output));
        }
    }

    public sealed partial class VoidFallGameRuntime
    {
        internal IEnumerator CaptureLegacyRestoration(string output)
        {
            Directory.CreateDirectory(output);
            _runExportDirectoryOverride = Path.Combine(output, "RunExports");
            _diagnosticRunSeedOverride = 0x41525345;
            StartRunInternal(true);
            enabled = false;
            _applicationInactive = false; _paused = false;
            _gameSim.Player.Iframes = 9999;
            _hudGroup.alpha = 1;
            DestroyEnemiesForVoidTransition(); ClearMeteors();
            var ids = new[] { "chaser", "runner", "swarmer", "shuriken", "spiky", "exploder" };
            for (var i = 0; i < 90; i++)
            {
                var a = i * 2.399963f;
                var r = 130 + Mathf.Sqrt(i) * 22;
                SpawnEnemy(ids[i % ids.Length], new Vector2(Mathf.Cos(a) * r * 1.6f, Mathf.Sin(a) * r));
            }
            for (var i = 0; i < _gameSim.Enemies.Length; i++)
            {
                var e = _gameSim.Enemies[i]; if (!e.Active) continue;
                e.Age = 3; e.Speed = 0; e.Health = e.MaxHealth = 5000; _gameSim.Enemies[i] = e;
            }
            _time = 172; _level = 6; _xp = _xpNeed * .6f;
            _partsEarned = 124; _kills = 368;
            RebuildEnemyGrid();
            for (var rank = 1; rank <= 6; rank++)
            {
                ClearTransitionProjectiles();
                _upgradeProgress.WeaponRanks[0] = rank;
                RecalculatePlayerStats(false);
                _weaponCooldowns[0] = 0;
                UpdateWeapons(.016f);
                for (var frame = 0; frame < 8; frame++)
                {
                    _ambientClock += .016f;
                    UpdateEnemies(.016f); RebuildEnemyGrid(); UpdateBullets(.016f);
                    Render(); RenderJunction(); UpdateHud(); SyncUiScreen();
                    yield return null;
                }
                yield return CaptureRestorationFrame(output, "pulse-rank-" + rank + ".png");
            }
            // Exercise the live Clock/Boomerang renderers and both Spiky sizes.
            Array.Clear(_upgradeProgress.WeaponRanks, 0, _upgradeProgress.WeaponRanks.Length);
            _upgradeProgress.WeaponRanks[8] = 4; _upgradeProgress.WeaponRanks[9] = 4;
            RecalculatePlayerStats(false);
            _weaponCooldowns[9] = 0;
            for (var frame = 0; frame < 15; frame++)
            { UpdateWeapons(.016f); UpdateArsenalWeapons(.016f); Render(); UpdateHud(); yield return null; }
            yield return CaptureRestorationFrame(output, "clock-boomerang.png");
            Array.Clear(_upgradeProgress.WeaponRanks, 0, _upgradeProgress.WeaponRanks.Length);
            // Owner-approved HUD: exercise sparse and capacity loadouts in the real player.
            foreach (var slot in new[] { 0, 1, 2, 3 }) _upgradeProgress.WeaponRanks[slot] = 4;
            for (var i = 0; i < _upgradeProgress.SupportRanks.Length; i++) _upgradeProgress.SupportRanks[i] = 1;
            for (var i = 0; i < _upgradeProgress.LateRanks.Length; i++) _upgradeProgress.LateRanks[i] = 1;
            RecalculatePlayerStats(false); ClearTransitionProjectiles(); Render(); RefreshApprovedBuildHud(); UpdateHud();
            yield return CaptureRestorationFrame(output, "hud-full-build.png");
            _gameSim.Player.Health = 9; _secondWindRemaining = 168; UpdateHud();
            yield return CaptureRestorationFrame(output, "hud-low-hp.png");
            ShowApprovedSlot(_approvedSecondWind);
            yield return CaptureRestorationFrame(output, "hud-slot-tooltip.png");
            HideApprovedSlot();
            _gameSim.Player.Health = _gameSim.Player.MaxHealth;
            Array.Clear(_upgradeProgress.SupportRanks, 0, _upgradeProgress.SupportRanks.Length);
            Array.Clear(_upgradeProgress.LateRanks, 0, _upgradeProgress.LateRanks.Length);
            RecalculatePlayerStats(false); RefreshApprovedBuildHud();
            _levelOptions = UpgradeRules.RollProgressionOptions(_upgradeProgress, new Rng(7), 100)
                .Where(o => o.TargetId == "phaseRounds" || o.TargetId == "split-pistol" || o.TargetId == "secondWind").ToArray();
            Debug.Log("RESTORATION CARD COUNT " + _levelOptions.Length);
            _levelUpActive = true; _paused = true;
            _ui.LevelUp.ShowUpgrades(BuildUpgradeCards(_levelOptions), 0, SelectLevelOption);
            SyncUiScreen();
            yield return CaptureRestorationFrame(output, "cards.png");
            _levelUpActive = false; _paused = false; _levelOptions = null;
            SyncUiScreen();
            _paused = true; SyncUiScreen();
            yield return CaptureRestorationFrame(output, "pause-typography.png");
            _paused = false; SyncUiScreen();
            DestroyEnemiesForVoidTransition(); ClearTransitionProjectiles();
            SpawnEnemy("spiky", new Vector2(0, 150));
            _gameSim.Enemies[0].SpawnId = 17; _gameSim.Enemies[0].Age = .35f; _gameSim.Enemies[0].Speed = 0;
            for (var i = 0; i < 12; i++)
            {
                var angle = i * Mathf.PI / 6;
                SpawnEnemy(i % 3 == 0 ? "exploder" : "chaser", new Vector2(Mathf.Cos(angle) * 42, 150 + Mathf.Sin(angle) * 42));
                _gameSim.Enemies[i + 1].Speed = 0;
            }
            UpdateEnemies(.01f); ApplySpikyGrowthPushes(); Render(); UpdateHud();
            yield return CaptureRestorationFrame(output, "spiky-before-growth.png");
            for (var i = 0; i < 18; i++) { UpdateEnemies(.016f); ApplySpikyGrowthPushes(); Render(); yield return null; }
            yield return CaptureRestorationFrame(output, "spiky-after-growth.png");
            DestroyEnemiesForVoidTransition(); ClearTransitionProjectiles();
            _gameSim.Player.Position = new Vector2(900, 400); _cameraFollowPosition = _gameSim.Player.Position;
            OnVoidObjectiveCompleted();
            StepVoidCompletionDelay(10f);
            for (var tick = 0; tick < 24; tick++)
            {
                UpdateJourneyFlow(.1f); Render(); RenderJunction(); UpdateHud(); SyncUiScreen();
                if (tick == 2 || tick == 7 || tick == 12 || tick == 22)
                    yield return CaptureRestorationFrame(output, "crossing-" + tick + ".png");
                yield return null;
            }
            // Use the normal UI callback and persistence path, keeping diagnostics on monitor two.
            Screen.GetDisplayLayout(_displayLayout);
            var monitorTestIndex = _displayLayout.FindIndex(d => d.Equals(Screen.mainWindowDisplayInfo));
            if (monitorTestIndex < 0) monitorTestIndex = 0;
            _ui.Callbacks.SetMonitor(monitorTestIndex);
            var monitorDeadline = Time.realtimeSinceStartup + 10;
            while (_monitorMove != null && Time.realtimeSinceStartup < monitorDeadline) yield return null;
            Screen.GetDisplayLayout(_displayLayout);
            var monitorPassed = _monitorMove == null && monitorTestIndex < _displayLayout.Count &&
                Screen.mainWindowDisplayInfo.Equals(_displayLayout[monitorTestIndex]) &&
                _saveData.settings.monitorIndex == monitorTestIndex;
            File.WriteAllText(Path.Combine(output, "monitor-check.txt"),
                "passed=" + monitorPassed + ";savedIndex=" + _saveData.settings.monitorIndex +
                ";actual=" + Screen.mainWindowDisplayInfo.name + ";position=" + Screen.mainWindowPosition +
                ";width=" + Screen.width + ";height=" + Screen.height);
            if (!monitorPassed) { Debug.LogError("MONITOR CHECK FAILED"); Application.Quit(1); yield break; }
            RefreshSettingsUi(); _ui.SetScreen(VoidFall.UI.UIScreen.Settings);
            // Bring the Video section into view without changing any preferences.
            var scroll = _ui.Settings.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true);
            if (scroll != null) scroll.verticalNormalizedPosition = .45f;
            yield return CaptureRestorationFrame(output, "graphics-monitor.png");
            EndRun(); SyncUiScreen();
            yield return CaptureRestorationFrame(output, "death-typography.png");
            EnterMainMenu(); SyncUiScreen();
            yield return CaptureRestorationFrame(output, "menu-typography.png");
            FinishRunExport("diagnostic_complete");
            File.WriteAllText(Path.Combine(output, "complete.txt"), "Restoration captures completed. Final flow: " + JourneyStatus);
            Debug.Log("RESTORATION CAPTURES COMPLETE " + JourneyStatus);
            Application.Quit(0);
        }
        private IEnumerator CaptureRestorationFrame(string output, string file)
        {
            // Wait for the real card entrance/layout and GPU presentation before sampling.
            yield return new WaitForSecondsRealtime(.65f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, file));
            yield return new WaitForSecondsRealtime(.2f);
        }
    }
}
