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

        private void RecordStartupMenuFrame()
        {
            if (_startupMenuReportLogged || _startupMenuReadyRealtime <= 0 || !_mainMenuBrowsing) return;
            if (_startupMenuSkipNextFrame)
            {
                _startupMenuSkipNextFrame = false;
                return;
            }

            var frameSeconds = Mathf.Max(0, Time.unscaledDeltaTime);
            _startupMenuSampleSeconds += frameSeconds;
            _startupMenuFrameCount++;
            if (frameSeconds > _startupMenuWorstFrameSeconds)
            {
                _startupMenuWorstFrameSeconds = frameSeconds;
                _startupMenuWorstFrameElapsed =
                    Time.realtimeSinceStartupAsDouble - _startupMenuReadyRealtime;
            }
            if (Time.realtimeSinceStartupAsDouble - _startupMenuReadyRealtime < 10.0) return;

            _startupMenuReportLogged = true;
            var averageFps = _startupMenuSampleSeconds > 0
                ? _startupMenuFrameCount / _startupMenuSampleSeconds
                : 0;
            Debug.Log(
                "VOIDFALL_MENU_STABILITY seconds=10" +
                " frames=" + _startupMenuFrameCount +
                " averageFps=" + averageFps.ToString("F1") +
                " worstFrameMs=" + (_startupMenuWorstFrameSeconds * 1000f).ToString("F1") +
                " worstAtSeconds=" + _startupMenuWorstFrameElapsed.ToString("F2") +
                " emaFrameMs=" + _debugFrameEmaMs.ToString("F1"));
        }

        private void ToggleDebugOverlay()
        {
            _debugOverlay = !_debugOverlay;
            _ui?.DebugOverlay?.Toggle();
        }

        private void TryBuyWorkshopFromUi(string id)
        {
            TryBuyWorkshop(id);
            RefreshWorkshopUi();
            // Parts changed, so the home screen's balance and the Workshop nav
            // card's detail line are both stale now.
            RefreshMenuProfileUi();
        }

        private void RefundAllWorkshopFromUi()
        {
            RefundAllWorkshop();
            RefreshWorkshopUi();
            RefreshMenuProfileUi();
        }

        private void RefreshWorkshopUi()
        {
            if (_ui?.Workshop == null) return;
            var list = new List<WorkshopItemData>();
            foreach (var id in WorkshopOrder)
            {
                var rank = WorkshopRank(id);
                var maxRank = id == "protocol" ? 1 : SaveStore.WorkshopMaxRank;
                var cost = WorkshopCost(id, rank);
                list.Add(new WorkshopItemData
                {
                    Id = id,
                    Name = WorkshopName(id),
                    Description = WorkshopDescription(id),
                    CurrentRank = rank,
                    MaxRank = maxRank,
                    Cost = cost,
                    CanAfford = (_saveData?.parts ?? 0) >= cost && cost >= 0
                });
            }
            _ui.Workshop.Populate(_saveData?.parts ?? 0, list);
        }

        private void RefreshRecordsUi()
        {
            if (_ui?.Records == null) return;
            var scores = new List<HighScoreRow>();
            if (_saveData?.highScores != null)
            {
                foreach (var entry in _saveData.highScores)
                {
                    if (entry == null) continue;
                    scores.Add(new HighScoreRow
                    {
                        Score = entry.score,
                        Time = entry.time,
                        Level = entry.level,
                        Kills = entry.kills,
                        BossKills = entry.bossKills
                    });
                }
            }
            _ui.Records.PopulateHighScores(scores);

            var stats = _saveData?.stats;
            if (stats == null) return;
            _ui.Records.PopulateLifetime(new UILifetimeStats
            {
                TotalRuns = stats.totalRuns,
                TotalKills = stats.totalKills,
                BestScore = stats.bestScore,
                BestTime = stats.bestTime,
                TotalBossKills = stats.totalBossKills,
                TotalEliteKills = stats.totalEliteKills,
                TotalPlaySeconds = stats.totalPlaySeconds,
                TotalPartsEarned = stats.totalPartsEarned,
                BestKills = stats.bestKills,
                HighestLevel = stats.highestLevel,
                TotalDamageDealt = stats.totalDamageDealt,
                TotalDamageTaken = stats.totalDamageTaken
            });
        }

        private void RefreshSettingsUi()
        {
            var settings = _saveData?.settings;
            if (_ui?.Settings == null || settings == null) return;
            _ui.Settings.Apply(new UISettingsState
            {
                MasterVolume = settings.masterVolume,
                EffectsVolume = settings.effectsVolume,
                MusicVolume = settings.musicVolume,
                ScreenShake = settings.shake,
                TouchSize = settings.touchSize,
                Quality = settings.quality,
                ReducedMotion = settings.reducedMotion,
                HighContrast = settings.highContrast
            });
        }

        private void RefreshMenuProfileUi()
        {
            if (_ui?.MainMenu == null) return;
            _ui.MainMenu.UpdateProfile(new UIProfileState
            {
                Parts = _saveData?.parts ?? 0,
                BestScore = CurrentBestScore(),
                TotalRuns = _saveData?.stats?.totalRuns ?? 0,
                ArenaName = ArenaName(_arenaId)
            });
        }

        private void OpenMenuPageFromUi(MenuPage page)
        {
            _menuPage = page;
            _menuScroll = Vector2.zero;
            if (page == MenuPage.Workshop) RefreshWorkshopUi();
            if (page == MenuPage.Records) RefreshRecordsUi();
            if (page == MenuPage.Settings) RefreshSettingsUi();
            _audio?.Play(ProceduralAudio.Cue.Ui, 1f);
            SyncUiScreen();
        }

        /// <summary>
        /// Maps the runtime's own state onto the single screen the interface
        /// should be showing. The runtime stays the owner of that state, so
        /// keyboard shortcuts, focus loss and gameplay events all keep working
        /// without the UI holding a second copy that could disagree.
        /// </summary>
        private void SyncUiScreen()
        {
            if (_ui == null) return;

            UIScreen screen;
            if (_menuPage == MenuPage.Home) screen = UIScreen.Home;
            else if (_menuPage == MenuPage.Workshop) screen = UIScreen.Workshop;
            else if (_menuPage == MenuPage.Records) screen = UIScreen.Records;
            else if (_menuPage == MenuPage.Settings) screen = UIScreen.Settings;
            // MenuPage.Main was an in-run summary page with no counterpart in the
            // browser build. Its figures now ride along on the pause overlay.
            else if (_menuPage == MenuPage.Main) screen = UIScreen.Pause;
            else if (_levelUpActive) screen = UIScreen.LevelUp;
            else if (_revivePending) screen = UIScreen.Revive;
            else if (_gameOver) screen = UIScreen.GameOver;
            else if (_paused) screen = UIScreen.Pause;
            else screen = UIScreen.None;

            if (screen == UIScreen.Pause && _ui.CurrentScreen != UIScreen.Pause)
            {
                _ui.Pause?.UpdateSnapshot(new UIRunSnapshot
                {
                    Score = CurrentScore(),
                    ElapsedSeconds = _time,
                    Kills = _kills,
                    Level = _level,
                    PartsEarned = _partsEarned,
                    BossKills = _bossKills
                });
            }

            if (screen != _ui.CurrentScreen) _ui.SetScreen(screen);
        }

        private void RefundAllWorkshop()
        {
            if (_saveData?.workshop == null) return;
            var refundedParts = 0;
            foreach (var entry in _saveData.workshop)
            {
                if (entry == null) continue;
                for (var r = 0; r < entry.rank; r++)
                {
                    var cost = WorkshopCost(entry.id, r);
                    if (cost > 0) refundedParts += cost;
                }
                entry.rank = 0;
            }
            _saveData.parts = AddCounter(_saveData.parts, refundedParts);
            _saveStore?.Save(_saveData);
            EnqueueToast("Workshop refunded", $"+{refundedParts} Parts", 2.5f, ToastKind.Reward);
        }

        private void EnterMainMenu()
        {
            // Entering the menu is not gameplay. Let the existing incremental
            // warm continue across menu frames instead of blocking the first
            // visible screen on every procedural combat sprite.
            StartRunInternal(false, false);
            _mainMenuBrowsing = true;
            _input.ResetTouch();
            _menuPage = MenuPage.Home;
            _menuScroll = Vector2.zero;
            _gameOverScroll = Vector2.zero;
            _paused = true;
            _runSaved = true;
            _lastRunSaved = true;
            if (_canvas != null) _canvas.enabled = false;
            _audio?.StopPad();
            // Menu tracks are exclusive to the menu, so this cross-fades away
            // from whatever the last run was playing.
            _music?.PlayMainMenu();
            if (_ui != null)
            {
                RefreshMenuProfileUi();
                RefreshSettingsUi();
                PushUiBackdrop();
                _ui.RefreshMuteGlyph();
                _ui.SwitchToMainMenu();
                RefreshWorkshopUi();
                RefreshRecordsUi();
            }
        }

        private static Vector2 MainMenuCameraPosition(float ambientClock)
        {
            return new Vector2(
                Mathf.Cos(ambientClock * 0.07f) * 220f,
                Mathf.Sin(ambientClock * 0.052f) * 180f);
        }

        private static float SourceHarvesterFullOverlayAlpha(float ambientClock, float seed)
        {
            return 0.16f + (0.5f + Mathf.Sin(ambientClock * 7f + seed) * 0.5f) * 0.2f;
        }

        private static float SourceLowHealthOverlayAlpha(bool lowHealth, float ambientClock)
        {
            return lowHealth ? 0.28f + 0.14f * Mathf.Sin(ambientClock * 5f) : 0f;
        }

        private int WorkshopRank(string id)
        {
            if (_saveData?.workshop == null) return 0;
            foreach (var entry in _saveData.workshop)
            {
                if (entry != null && entry.id == id) return entry.rank;
            }
            return 0;
        }

        private void AcceptRevive()
        {
            if (!_revivePending || _revivesRemaining <= 0) return;
            _revivesRemaining--;
            _revivePending = false;
            _dyingTimer = 0;
            _playerHealth = Mathf.Ceil(_playerMaxHealth * 0.5f);
            _playerIframes = 2.5f;
            _playerVelocity = Vector2.zero;
            _cyanFlash = 0.9f;
            _paused = false;
            _targetTimeScale = 1;
            TriggerFreeze(0.08f);
            _audio?.Play(ProceduralAudio.Cue.LevelUp, 1.12f);
            BurstFx(_playerPosition, SourceDotColor("cyan"), 26, 380, 0.7f, 1f);
            BurstFx(_playerPosition, SourceDotColor("white"), 14, 300, 0.55f, 0.8f);
            SpawnRingWave(_playerPosition, 26f, 640f, 0.72f, new Color(0.133f, 0.827f, 0.933f, 1f));
            SpawnRingWave(_playerPosition, 14f, 420f, 0.55f, new Color(0.133f, 0.827f, 0.933f, 1f));

            var enemySnapshot = CaptureEnemyEffectSnapshot(out var enemySnapshotCount);
            try
            {
                for (var target = 0; target < enemySnapshotCount; target++)
                {
                    var snapshot = enemySnapshot[target];
                    if (!IsLiveEnemyEffectTarget(snapshot)) continue;
                    var enemy = snapshot.State;
                    var delta = enemy.Position - _playerPosition;
                    var reach = 340f + enemy.Radius;
                    if (delta.sqrMagnitude >= reach * reach) continue;
                    ApplyEnemyDamage(snapshot.Slot, 60f + _time * 0.2f, delta, 460, false);
                }
            }
            finally
            {
                ReleaseEnemyEffectSnapshot(enemySnapshot);
            }
            EnqueueToast(
                "Revived",
                _revivesRemaining > 0 ? $"{_revivesRemaining} left" : null,
                2.2f,
                ToastKind.Reward);
            SetMenuNotice(_revivesRemaining > 0 ? $"Revived — {_revivesRemaining} left." : "Revived.");
        }

        private void DeclineRevive()
        {
            if (!_revivePending) return;
            EndRun();
        }

        private void DiscoverBestiary(string id)
        {
            if (_saveData?.bestiary == null || string.IsNullOrEmpty(id)) return;
            foreach (var entry in _saveData.bestiary)
            {
                if (entry != null && entry.id == id)
                {
                    entry.discovered = true;
                    return;
                }
            }
        }

        private void OpenLevelUp()
        {
            if (_upgradeProgress == null) return;
            if (_levelUpTimer >= 0) return;
            _audio?.Play(ProceduralAudio.Cue.LevelUp);
            _cyanFlash = 0.8f;
            BurstFx(_playerPosition, SourceDotColor("cyan"), 20, 310, 0.66f, 0.95f);
            SpawnRingWave(_playerPosition, 18f, 430f, 0.62f, new Color(0.133f, 0.827f, 0.933f, 1f));
            _levelOptions = null;
            _levelUpActive = false;
            _levelUpTimer = 0.38f;
            _targetTimeScale = 0.12f;
        }

        private void BeginEvolutionReveal(UpgradeOptionDefinition option)
        {
            if (option == null || option.Kind != UpgradeOptionKind.Evolution) return;
            _evolutionRevealPreviousName = option.TargetId;
            for (var index = 0; index < ContentCatalog.Weapons.Length; index++)
            {
                if (ContentCatalog.Weapons[index].Id != option.TargetId) continue;
                _evolutionRevealPreviousName = ContentCatalog.Weapons[index].Name;
                break;
            }
            _evolutionRevealName = option.Name;
            _evolutionRevealWeaponId = option.TargetId;
            _evolutionRevealAccent = ParseColor(option.Accent, new Color(0.35f, 0.9f, 1f, 1f));
            _evolutionRevealTimer = 2.6f;
            // The reveal reads as a replacement, so it shows the weapon being
            // superseded struck through above its evolved name, tinted by the
            // evolution's own accent.
            _ui?.Evolution?.ShowEvolution(
                option.Name,
                _evolutionRevealPreviousName,
                _evolutionRevealAccent,
                2.6f);
        }

        private List<UpgradeCardData> BuildUpgradeCards(UpgradeOptionDefinition[] options)
        {
            var cards = new List<UpgradeCardData>();
            if (options == null) return cards;

            for (var index = 0; index < options.Length; index++)
            {
                var option = options[index];
                var showsRanks = option.Kind == UpgradeOptionKind.Weapon ||
                    option.Kind == UpgradeOptionKind.Support;
                cards.Add(new UpgradeCardData
                {
                    Title = option.Name,
                    Category = option.Kind.ToString(),
                    Description = option.Description,
                    LevelText = UpgradeOptionLabel(option),
                    // Each option carries its own accent in content data; the
                    // browser build tints the whole card from it rather than
                    // using a fixed palette.
                    AccentColor = ParseColor(option.Accent, UITheme.CyanLight),
                    CurrentRank = option.CurrentRank,
                    MaxRank = showsRanks ? option.MaxRank : 0,
                    IsEvolution = option.Kind == UpgradeOptionKind.Evolution
                });
            }
            return cards;
        }

        private static Color EvolutionAccent(string targetId)
        {
            switch (targetId)
            {
                case "scattergun": return SourceDotColor("orange");
                case "seeker": return SourceDotColor("lime");
                case "railgun": return SourceDotColor("violet");
                case "arc": return SourceDotColor("yellow");
                default: return SourceDotColor("cyan");
            }
        }

        private static void SetTopHudAnchor(RectTransform rect, bool narrow, float width, float y)
        {
            if (rect == null) return;
            var anchor = narrow ? new Vector2(1f, 1f) : new Vector2(0.5f, 1f);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = narrow ? new Vector2(1f, 1f) : new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(narrow ? -64f : -10f, y);
        }

        private static float BuildChipHudNarrowWidth(float rankPreferredWidth)
        {
            return 35f + Mathf.Max(13f, Mathf.Ceil(rankPreferredWidth));
        }

        private string BuildLoadoutHudText()
        {
            if (_upgradeProgress == null) return string.Empty;

            var builder = new StringBuilder(320);
            builder.Append("LOADOUT  ");
            var wroteWeapon = false;
            for (var index = 0; index < Mathf.Min(ContentCatalog.Weapons.Length, _upgradeProgress.WeaponRanks.Length); index++)
            {
                var rank = _upgradeProgress.WeaponRanks[index];
                if (rank <= 0) continue;
                if (wroteWeapon) builder.Append("  ·  ");
                builder.Append(ContentCatalog.Weapons[index].Name)
                    .Append(' ')
                    .Append(rank)
                    .Append('/')
                    .Append(ContentCatalog.Weapons[index].Ranks.Length);
                if (index < _upgradeProgress.Evolved.Length && _upgradeProgress.Evolved[index]) builder.Append('+');
                wroteWeapon = true;
            }
            if (!wroteWeapon) builder.Append("none");

            builder.Append('\n').Append("SUP  ");
            var wroteSupport = false;
            for (var index = 0; index < Mathf.Min(ContentCatalog.Supports.Length, _upgradeProgress.SupportRanks.Length); index++)
            {
                var rank = _upgradeProgress.SupportRanks[index];
                if (rank <= 0) continue;
                if (wroteSupport) builder.Append("  ·  ");
                builder.Append(ContentCatalog.Supports[index].Name)
                    .Append(' ')
                    .Append(rank)
                    .Append('/')
                    .Append(ContentCatalog.Supports[index].MaxRank);
                wroteSupport = true;
            }
            if (!wroteSupport) builder.Append("none");

            builder.Append('\n').Append("LATE ");
            var wroteLate = false;
            for (var index = 0; index < Mathf.Min(ContentCatalog.LateUpgrades.Length, _upgradeProgress.LateRanks.Length); index++)
            {
                var rank = _upgradeProgress.LateRanks[index];
                if (rank <= 0) continue;
                if (wroteLate) builder.Append("  ·  ");
                builder.Append(ContentCatalog.LateUpgrades[index].Name)
                    .Append(' ')
                    .Append(rank)
                    .Append('/')
                    .Append(ContentCatalog.LateUpgrades[index].MaxRank);
                wroteLate = true;
            }
            if (!wroteLate) builder.Append("none");

            return builder.ToString();
        }

        private string BuildUpgradeStripHudText(bool late)
        {
            if (_upgradeProgress == null) return string.Empty;

            var builder = new StringBuilder(180);
            if (late)
            {
                for (var index = 0; index < Mathf.Min(ContentCatalog.LateUpgrades.Length, _upgradeProgress.LateRanks.Length); index++)
                {
                    var rank = _upgradeProgress.LateRanks[index];
                    if (rank <= 0) continue;
                    if (builder.Length > 0) builder.Append('\n');
                    builder.Append(ContentCatalog.LateUpgrades[index].Name)
                        .Append("  ")
                        .Append(rank)
                        .Append('/')
                        .Append(ContentCatalog.LateUpgrades[index].MaxRank);
                }
            }
            else
            {
                for (var index = 0; index < Mathf.Min(ContentCatalog.Supports.Length, _upgradeProgress.SupportRanks.Length); index++)
                {
                    var rank = _upgradeProgress.SupportRanks[index];
                    if (rank <= 0) continue;
                    if (builder.Length > 0) builder.Append('\n');
                    builder.Append(ContentCatalog.Supports[index].Name)
                        .Append("  ")
                        .Append(rank)
                        .Append('/')
                        .Append(ContentCatalog.Supports[index].MaxRank);
                }
            }

            return builder.ToString();
        }

        private bool ShouldShowHud()
        {
            // React derives HUD visibility from the run phase. Main-menu
            // profile pages remain in the menu phase, even though Unity uses
            // the same runtime object to draw them; keep those pages free of
            // gameplay chrome. Pause/level-up are the only non-playing phases
            // that retain the HUD.
            return !_mainMenuBrowsing &&
                   !_gameOver &&
                   !_revivePending &&
                   (_menuPage == MenuPage.None || _paused || _levelUpActive || _levelUpTimer >= 0f);
        }

        private static bool BuildChipHudUsesFullBorder(bool active, bool showMaxRank)
        {
            return active && !showMaxRank;
        }

        private static bool BuildChipHudUsesAccentBar(bool active, bool showMaxRank, bool evolved)
        {
            return active && (showMaxRank || evolved);
        }

        private static float BuildChipHudBackgroundAlpha(bool showMaxRank, bool evolved)
        {
            if (showMaxRank) return 0.68f;
            return evolved ? 0.86f : 0.74f;
        }

        private static float BuildChipHudAccentWidth(bool evolved)
        {
            return evolved ? 3f : 2f;
        }

        private static float BuildChipHudIconSize(bool showMaxRank)
        {
            return showMaxRank ? 13f : 14f;
        }

        private static Color BuildChipHudLabelColor(bool showMaxRank)
        {
            return showMaxRank
                ? new Color(0.682f, 0.741f, 0.800f, 1f)
                : new Color(0.796f, 0.835f, 0.882f, 1f);
        }

        private void TogglePauseFromHud()
        {
            TogglePause();
        }

        private void TogglePause()
        {
            if (_revivePending || _gameOver || _levelUpActive || _menuPage != MenuPage.None) return;
            if (_paused)
            {
                _paused = false;
                RestartQualitySession();
                _audio?.Resume();
                // Browser togglePause() resumes the context and then plays the
                // gated UI cue; the pause cue is only for entering pause.
                _audio?.Play(ProceduralAudio.Cue.Ui, 1f);
                // Overlay visibility is reconciled from _paused by SyncUiScreen,
                // so this only needs to move the game state.
                SyncUiScreen();
            }
            else
            {
                _paused = true;
                _audio?.Play(ProceduralAudio.Cue.Pause, 0.86f);
                SyncUiScreen();
            }
        }

        private void RecordTelemetrySample(float frameDt)
        {
            _telemetry.RecordSample(new UnityTelemetrySample
            {
                timeSeconds = (float)_time,
                level = _level,
                hp = _playerHealth,
                maxHp = _playerMaxHealth,
                enemies = ActiveEnemies(),
                activeBosses = ActiveBosses(),
                projectiles = ActiveBullets() + ActiveHostileShots(),
                pickups = ActivePickups(),
                xpOnGround = Mathf.FloorToInt(XpOnGround()),
                xpHeldByHarvesters = XpHeldByHarvesters(),
                fps = TelemetryFpsForFrame(frameDt),
                frameMs = frameDt * 1000f,
                quality = TelemetryQualityValue(),
                hpMultiplier = EnemyHealthScaleAt((float)_time, _bossCycle, 1f),
                speedMultiplier = EnemySpeedScaleAt((float)_time, _bossCycle),
                damageMultiplier = EnemyDamageScaleAt((float)_time, _bossCycle),
                directorEvent = _nextDirectorEvent.Id,
                arenaId = ArenaIdName(_arenaId),
                arenaPhase = _arenaTransitionState.Phase.ToString(),
                activeEliteVariants = ActiveEliteVariantTotal(),
                meteors = ActiveMeteors(),
            });
        }

        private void ShowMilestoneToast(string kind, int value)
        {
            var major = MilestoneRules.IsMajor(kind, value);
            EnqueueToast($"{value:N0} {kind}", null, major ? 4f : 2.5f, ToastKind.Reward);
            _audio?.Play(major ? ProceduralAudio.Cue.MilestoneMajor : ProceduralAudio.Cue.Milestone);
            _telemetry.RecordMilestone((float)_time, kind, value);
            if (major) _cyanFlash = Mathf.Max(_cyanFlash, 0.48f);
            if (major) SpawnRingWave(_playerPosition, 18f, 470f, 0.62f, new Color(1f, 0.78f, 0.28f, 0.78f));
            BurstFx(
                _playerPosition,
                major ? SourceDotColor("yellow") : SourceDotColor("cyan"),
                major ? 14 : 7,
                major ? 260 : 170,
                major ? 0.5f : 0.32f,
                major ? 0.76f : 0.58f);
        }

        private void ShowArenaToast(string message, float seconds)
        {
            EnqueueToast(message, null, seconds, ToastKind.Info);
        }

        private void ShowArenaToast(string message, float seconds, ToastKind kind, string detail = null)
        {
            EnqueueToast(message, detail, seconds, kind);
        }

        private void EnqueueToast(string text, string detail, float seconds, ToastKind kind)
        {
            // Find an inactive slot first; only evict the oldest if all are full.
            var targetSlot = -1;
            for (var index = 0; index < _toastStates.Length; index++)
            {
                if (!_toastStates[index].Active)
                {
                    targetSlot = index;
                    break;
                }
            }
            if (targetSlot < 0)
            {
                // All slots full — shift left to evict the oldest (index 0).
                for (var index = 1; index < _toastStates.Length; index++)
                    _toastStates[index - 1] = _toastStates[index];
                targetSlot = _toastStates.Length - 1;
            }
            _toastStates[targetSlot] = new ToastState
            {
                Active = true,
                Text = (text ?? string.Empty).ToUpperInvariant(),
                Detail = string.IsNullOrEmpty(detail) ? null : detail.ToUpperInvariant(),
                Remaining = Mathf.Max(0.1f, seconds),
                Duration = Mathf.Max(0.1f, seconds),
                Kind = kind,
            };
            // Combat toasts stay on the runtime's own canvas, which already
            // renders this queue. Routing them through the menu layer as well
            // would show every notice twice.
        }

        private void ClearToasts()
        {
            for (var index = 0; index < _toastStates.Length; index++)
            {
                _toastStates[index] = new ToastState();
                if (_toastViews[index] == null) continue;
                _toastViews[index].text = string.Empty;
                _toastViews[index].enabled = false;
            }
        }

        private static Color ToastColor(ToastKind kind)
        {
            switch (kind)
            {
                case ToastKind.Danger:
                    return new Color(0.984f, 0.443f, 0.522f, 1f);
                case ToastKind.Reward:
                    return new Color(0.431f, 0.906f, 0.702f, 1f);
                default:
                    return new Color(0.81f, 0.98f, 1f, 1f);
            }
        }

        private static int ToastQueueCapacity()
        {
            return MaxToasts;
        }

        private static float ToastStackTop(float viewportHeight, float safeTopInset)
        {
            return Mathf.Max(viewportHeight * ToastStackTopPercent, safeTopInset + ToastStackTopInset);
        }

        private static int ToastFontSize(float viewportWidth)
        {
            return Mathf.RoundToInt(Mathf.Clamp(viewportWidth * 0.03f, 17f, 24f));
        }

        private static float ToastAnimationAlphaAt(float elapsed, float duration)
        {
            if (duration <= 0f || elapsed <= 0f) return 0f;
            if (elapsed >= duration) return 0f;
            var progress = Mathf.Clamp01(elapsed / duration);
            if (progress < ToastIntroEnd)
            {
                return CubicBezierEase(progress / ToastIntroEnd, 0.22f, 1f, 0.36f, 1f);
            }
            if (progress < ToastOutroStart) return 1f;
            return 1f - CubicBezierEase(
                (progress - ToastOutroStart) / (1f - ToastOutroStart),
                0.22f,
                1f,
                0.36f,
                1f);
        }

        private static float ToastAnimationScaleAt(float elapsed, float duration)
        {
            if (duration <= 0f || elapsed <= 0f) return 0.9f;
            if (elapsed >= duration) return 0.98f;
            var progress = Mathf.Clamp01(elapsed / duration);
            if (progress < ToastIntroEnd)
            {
                return Mathf.Lerp(
                    0.9f,
                    1.04f,
                    CubicBezierEase(progress / ToastIntroEnd, 0.22f, 1f, 0.36f, 1f));
            }
            if (progress < ToastSettleEnd)
            {
                return Mathf.Lerp(
                    1.04f,
                    1f,
                    CubicBezierEase(
                        (progress - ToastIntroEnd) / (ToastSettleEnd - ToastIntroEnd),
                        0.22f,
                        1f,
                        0.36f,
                        1f));
            }
            if (progress < ToastOutroStart) return 1f;
            return Mathf.Lerp(
                1f,
                0.98f,
                CubicBezierEase(
                    (progress - ToastOutroStart) / (1f - ToastOutroStart),
                    0.22f,
                    1f,
                    0.36f,
                    1f));
        }

        private static float ToastAnimationOffsetAt(float elapsed, float duration)
        {
            if (duration <= 0f || elapsed <= 0f) return -14f;
            if (elapsed >= duration) return -8f;
            var progress = Mathf.Clamp01(elapsed / duration);
            if (progress < ToastIntroEnd)
            {
                return Mathf.Lerp(
                    -14f,
                    0f,
                    CubicBezierEase(progress / ToastIntroEnd, 0.22f, 1f, 0.36f, 1f));
            }
            if (progress < ToastOutroStart) return 0f;
            return Mathf.Lerp(
                0f,
                -8f,
                CubicBezierEase(
                    (progress - ToastOutroStart) / (1f - ToastOutroStart),
                    0.22f,
                    1f,
                    0.36f,
                    1f));
        }

        private void ToggleMenu()
        {
            if (_levelUpActive || _revivePending) return;
            if (_menuPage == MenuPage.Home) return;
            _audio?.Play(ProceduralAudio.Cue.Ui, 0.9f);
            if (_menuPage == MenuPage.None)
            {
                _menuPage = MenuPage.Main;
                _menuScroll = Vector2.zero;
                _paused = true;
            }
            else
            {
                CloseMenu();
            }
        }

        private void CloseMenu()
        {
            CommitSettings();
            if (_mainMenuBrowsing)
            {
                _menuPage = MenuPage.Home;
                _menuScroll = Vector2.zero;
                _paused = true;
                RefreshMenuProfileUi();
                SyncUiScreen();
                return;
            }
            _menuPage = MenuPage.None;
            if (!_gameOver) _paused = false;
            if (!_gameOver) RestartQualitySession();
            SyncUiScreen();
        }

        private void ApplySettings()
        {
            var settings = _saveData?.settings;
            if (settings == null) return;
            var qualityMode = string.IsNullOrEmpty(settings.quality) ? "high" : settings.quality;
            if (_qualityController == null || _qualityModeApplied != qualityMode)
            {
                _qualityAuto = qualityMode == "auto";
                var startQuality = _qualityAuto
                    ? QualityRules.RecommendedInitialQuality(
                        Application.isMobilePlatform || Input.touchSupported,
                        Screen.width)
                    : QualityRules.FromName(qualityMode);
                _qualityController = new AdaptiveQualityController(startQuality);
                _qualityModeApplied = qualityMode;
                RestartQualitySession();
            }
            ApplyQualityPreset(_qualityController.CurrentPreset);
            // ProceduralAudio owns the browser-equivalent master/effects/music
            // gains; keep the global listener neutral so master is not applied
            // twice to every generated clip.
            AudioListener.volume = 1f;
            _audio?.SetVolumes(settings.masterVolume, settings.effectsVolume, settings.musicVolume);
            _music?.SetVolumes(settings.masterVolume, settings.musicVolume);
            // Mute lives in ProceduralAudio (it owns the PlayerPrefs key), so
            // mirror it here to keep the soundtrack in sync on boot as well as
            // after a toggle.
            if (_audio != null) _music?.SetMuted(_audio.Muted);
            switch (qualityMode)
            {
                case "low":
                    QualitySettings.vSyncCount = 0;
                    Application.targetFrameRate = 45;
                    break;
                case "balanced":
                    QualitySettings.vSyncCount = 0;
                    Application.targetFrameRate = 60;
                    break;
                case "auto":
                    QualitySettings.vSyncCount = 1;
                    Application.targetFrameRate = -1;
                    break;
                default:
                    QualitySettings.vSyncCount = 1;
                    Application.targetFrameRate = 60;
                    break;
            }
        }

        private void SetMenuNotice(string message)
        {
            _menuNotice = message;
            _menuNoticeTimer = 3f;
            // These notices had no renderer at all: the IMGUI screens set the
            // string but never drew it, so purchase confirmations, save failures,
            // import results and mute changes were silently discarded. The uGUI
            // notice stack is their first actual output.
            _ui?.Toasts?.ShowNotice(message);
        }

        private string ActiveOverlayAnimationKey()
        {
            if (_revivePending) return "revive";
            if (_levelUpActive) return null;
            if (_gameOver && _menuPage == MenuPage.None) return "gameover";
            if (_paused && _menuPage == MenuPage.None) return "pause";
            return null;
        }

        private void SyncOverlayAnimation()
        {
            var key = ActiveOverlayAnimationKey();
            if (string.Equals(key, _overlayAnimationKey, StringComparison.Ordinal)) return;
            _overlayAnimationKey = key;
            _overlayAnimationOpenedAt = string.IsNullOrEmpty(key) ? -1f : Time.realtimeSinceStartup;
        }

        private float CurrentOverlayFadeAlpha()
        {
            if (string.IsNullOrEmpty(_overlayAnimationKey)) return 1f;
            return OverlayFadeAlphaAt(Time.realtimeSinceStartup - _overlayAnimationOpenedAt);
        }

        private float CurrentOverlayCardAlpha()
        {
            if (string.IsNullOrEmpty(_overlayAnimationKey)) return 1f;
            return OverlayCardAlphaAt(Time.realtimeSinceStartup - _overlayAnimationOpenedAt);
        }

        private float CurrentOverlayCardOffset()
        {
            if (string.IsNullOrEmpty(_overlayAnimationKey)) return 0f;
            return OverlayCardRiseOffsetAt(Time.realtimeSinceStartup - _overlayAnimationOpenedAt);
        }

        private static float OverlayFadeAlphaAt(float elapsed)
        {
            if (elapsed <= 0f) return 0f;
            if (elapsed >= OverlayFadeSeconds) return 1f;
            var progress = Mathf.Clamp01(elapsed / OverlayFadeSeconds);
            return CubicBezierEase(progress, 0.25f, 0.1f, 0.25f, 1f);
        }

        private static float OverlayCardAlphaAt(float elapsed)
        {
            if (elapsed <= 0f) return 0f;
            if (elapsed >= OverlayCardRiseSeconds) return 1f;
            var progress = Mathf.Clamp01(elapsed / OverlayCardRiseSeconds);
            return CubicBezierEase(progress, 0.22f, 1f, 0.36f, 1f);
        }

        private static float OverlayCardRiseOffsetAt(float elapsed)
        {
            return OverlayCardRiseOffset * (1f - OverlayCardAlphaAt(elapsed));
        }

        private void SyncMainMenuAnimation()
        {
            if (_menuPage == MenuPage.Home)
            {
                if (_mainMenuAnimationOpenedAt < 0f)
                    _mainMenuAnimationOpenedAt = Time.realtimeSinceStartup;
                return;
            }
            _mainMenuAnimationOpenedAt = -1f;
        }

        private float CurrentMainMenuAlpha()
        {
            if (_mainMenuAnimationOpenedAt < 0f) return 1f;
            return MainMenuAlphaAt(Time.realtimeSinceStartup - _mainMenuAnimationOpenedAt);
        }

        private static float MainMenuAlphaAt(float elapsed)
        {
            if (elapsed <= 0f) return 0f;
            if (elapsed >= 0.30f) return 1f;
            return CubicBezierEase(elapsed / 0.30f, 0.25f, 0.1f, 0.25f, 1f);
        }

        private GUISkin MenuSkin()
        {
            if (_menuSkin == null)
            {
                _menuSkin = CreateMenuSkin(GUI.skin);
            }
            return _menuSkin ?? GUI.skin;
        }

        private GUIStyle EvolutionMarkStyle(Color accent)
        {
            var key = ColorUtility.ToHtmlStringRGB(accent);
            if (_evolutionMarkStyleCache.TryGetValue(key, out var cached)) return cached;
            var style = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                border = new RectOffset(16, 16, 16, 16),
            };
            var fill = new Color(0.012f, 0.027f, 0.071f, 0.90f);
            var border = new Color(accent.r, accent.g, accent.b, 1f);
            var texture = RoundedGradientGuiTexture(
                fill,
                fill,
                border,
                78,
                78,
                16f,
                "VoidFall Evolution Mark " + key);
            SetGuiStyleState(style.normal, texture, Color.white);
            SetGuiStyleState(style.hover, texture, Color.white);
            _evolutionMarkStyleCache[key] = style;
            return style;
        }

        private static Texture2D EvolutionMarkRingTexture(Color accent)
        {
            var key = ColorUtility.ToHtmlStringRGB(accent);
            if (_evolutionMarkRingCache.TryGetValue(key, out var cached)) return cached;
            const int size = 94;
            const float halfShape = 39f;
            const float radius = 16f;
            const float ringWidth = 8f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "VoidFall Evolution Mark Ring " + key,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color[size * size];
            var half = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var distance = RoundedRectSignedDistance(
                        x - half,
                        y - half,
                        halfShape,
                        halfShape,
                        radius);
                    var edge = Mathf.Clamp01((ringWidth - distance) / 1.5f) *
                        Mathf.Clamp01((distance + 0.5f) / 1.5f);
                    pixels[y * size + x] = new Color(accent.r, accent.g, accent.b, 0.08f * edge);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            _evolutionMarkRingCache[key] = texture;
            return texture;
        }

        private static Texture2D EvolutionMarkGlowTexture(Color accent)
        {
            var key = ColorUtility.ToHtmlStringRGB(accent);
            if (_evolutionMarkGlowCache.TryGetValue(key, out var cached)) return cached;
            const int size = 150;
            const float halfShape = 39f;
            const float radius = 16f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "VoidFall Evolution Mark Glow " + key,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color[size * size];
            var half = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var distance = Mathf.Max(0f, RoundedRectSignedDistance(
                        x - half,
                        y - half,
                        halfShape,
                        halfShape,
                        radius));
                    pixels[y * size + x] = new Color(
                        accent.r,
                        accent.g,
                        accent.b,
                        EvolutionMarkGlowAlpha(distance));
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            _evolutionMarkGlowCache[key] = texture;
            return texture;
        }

        private static Texture2D EvolutionCrossLineTexture(Color accent)
        {
            var key = ColorUtility.ToHtmlStringRGB(accent);
            if (_evolutionCrossLineCache.TryGetValue(key, out var cached)) return cached;
            const int width = 256;
            var texture = new Texture2D(width, 1, TextureFormat.RGBA32, false)
            {
                name = "VoidFall Evolution Cross Line " + key,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color[width];
            for (var x = 0; x < width; x++)
            {
                var normalizedPosition = x / (float)(width - 1);
                pixels[x] = new Color(
                    accent.r,
                    accent.g,
                    accent.b,
                    EvolutionCrossLineAlpha(normalizedPosition));
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            _evolutionCrossLineCache[key] = texture;
            return texture;
        }

        private static float EvolutionCrossLineAlpha(float normalizedPosition)
        {
            var position = Mathf.Clamp01(normalizedPosition);
            return 1f - Mathf.Abs(position * 2f - 1f);
        }

        private static float EvolutionMarkGlowAlpha(float distance)
        {
            const float peakAlpha = 0.32f;
            const float blurRadius = 13f;
            var safeDistance = Mathf.Max(0f, distance);
            return peakAlpha * Mathf.Exp(
                -(safeDistance * safeDistance) / (2f * blurRadius * blurRadius));
        }

        private static float EvolutionRevealDuration()
        {
            return 2.6f;
        }

        private static float EvolutionRevealCrossLineLength(float viewportWidth)
        {
            return Mathf.Min(viewportWidth * 0.42f, 330f);
        }

        private static float EvolutionRevealScale(float elapsedSeconds)
        {
            var progress = Mathf.Clamp01(elapsedSeconds / EvolutionRevealDuration());
            const float baseScale = 0.7f;
            if (progress <= 0.13f)
            {
                var segment = CubicBezierEase(progress / 0.13f, 0.22f, 1f, 0.36f, 1f);
                return Mathf.LerpUnclamped(baseScale * 0.82f, baseScale * 1.04f, segment);
            }
            if (progress <= 0.22f)
            {
                var segment = CubicBezierEase((progress - 0.13f) / 0.09f, 0.22f, 1f, 0.36f, 1f);
                return Mathf.LerpUnclamped(baseScale * 1.04f, baseScale, segment);
            }
            if (progress <= 0.78f) return baseScale;
            var outro = CubicBezierEase((progress - 0.78f) / 0.22f, 0.22f, 1f, 0.36f, 1f);
            return Mathf.LerpUnclamped(baseScale, baseScale * 1.02f, outro);
        }

        private static float EvolutionRevealOpacity(float elapsedSeconds)
        {
            var progress = Mathf.Clamp01(elapsedSeconds / EvolutionRevealDuration());
            if (progress <= 0.13f)
                return CubicBezierEase(progress / 0.13f, 0.22f, 1f, 0.36f, 1f);
            if (progress <= 0.78f) return 1f;
            var outro = CubicBezierEase((progress - 0.78f) / 0.22f, 0.22f, 1f, 0.36f, 1f);
            return 1f - outro;
        }

        private static float EvolutionRevealTitleGlowAlpha(float alpha)
        {
            return Mathf.Clamp01(alpha) * 0.55f;
        }

        private static float EvolutionRevealIntroBlur(float elapsedSeconds)
        {
            if (elapsedSeconds <= 0f) return 8f;
            var progress = Mathf.Clamp01(elapsedSeconds / EvolutionRevealDuration());
            if (progress >= 0.13f) return 0f;
            var eased = CubicBezierEase(progress / 0.13f, 0.22f, 1f, 0.36f, 1f);
            return Mathf.LerpUnclamped(8f, 0f, eased);
        }

        private static int EvolutionRevealTitleFontSize(float viewportWidth)
        {
            return Mathf.Clamp(Mathf.RoundToInt(viewportWidth * 0.06f), 27, 52);
        }

        private static GUISkin CreateMenuSkin(GUISkin baseSkin)
        {
            if (baseSkin == null)
            {
                return null;
            }

            var skin = ScriptableObject.CreateInstance<GUISkin>();
            skin.hideFlags = HideFlags.HideAndDontSave;
            var bodyFont = BrowserBodyFont();
            skin.font = bodyFont;
            skin.label = new GUIStyle(baseSkin.label);
            skin.label.font = bodyFont;
            skin.label.normal.textColor = new Color(0.90f, 0.93f, 0.96f, 1f);
            skin.label.hover.textColor = skin.label.normal.textColor;
            skin.label.onNormal.textColor = skin.label.normal.textColor;
            skin.label.onHover.textColor = skin.label.normal.textColor;

            var panel = RoundedGradientGuiTexture(
                MenuPanelGradientStartColor(),
                MenuPanelGradientEndColor(),
                MenuPanelBorderColor(),
                64,
                64,
                12f,
                "VoidFall Menu Panel",
                MenuPanelGradientAngleDegrees());
            var buttonNormal = RoundedGradientGuiTexture(
                new Color(0.06f, 0.18f, 0.23f, 0.94f),
                new Color(0.025f, 0.08f, 0.11f, 0.94f),
                new Color(0.40f, 0.91f, 1f, 0.28f),
                64,
                32,
                8f,
                "VoidFall Menu Button");
            var buttonHover = RoundedGradientGuiTexture(
                new Color(0.08f, 0.30f, 0.36f, 0.98f),
                new Color(0.035f, 0.15f, 0.19f, 0.98f),
                new Color(0.40f, 0.91f, 1f, 0.62f),
                64,
                32,
                8f,
                "VoidFall Menu Button Hover");
            var buttonActive = RoundedGradientGuiTexture(
                new Color(0.10f, 0.44f, 0.51f, 1f),
                new Color(0.04f, 0.25f, 0.31f, 1f),
                new Color(0.65f, 0.96f, 1f, 0.90f),
                64,
                32,
                8f,
                "VoidFall Menu Button Active");
            var sliderTrack = RoundedGradientGuiTexture(
                new Color(0.14f, 0.19f, 0.27f, 1f),
                new Color(0.08f, 0.11f, 0.17f, 1f),
                new Color(0.40f, 0.91f, 1f, 0.22f),
                64,
                16,
                6f,
                "VoidFall Menu Slider Track");
            var sliderThumb = RoundedGradientGuiTexture(
                new Color(0.70f, 0.98f, 1f, 1f),
                new Color(0.25f, 0.78f, 0.90f, 1f),
                Color.white,
                24,
                24,
                8f,
                "VoidFall Menu Slider Thumb");
            var textColor = new Color(0.87f, 0.97f, 0.99f, 1f);

            skin.box = new GUIStyle(baseSkin.box)
            {
                padding = new RectOffset(12, 12, 10, 10),
                border = new RectOffset(12, 12, 12, 12),
            };
            SetGuiStyleState(skin.box.normal, panel, textColor);
            SetGuiStyleState(skin.box.hover, panel, textColor);

            skin.button = new GUIStyle(baseSkin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                font = bodyFont,
                fontStyle = FontStyle.Bold,
                fontSize = 14,
                padding = new RectOffset(12, 12, 7, 7),
                margin = new RectOffset(4, 4, 4, 4),
                border = new RectOffset(8, 8, 8, 8),
            };
            SetGuiStyleState(skin.button.normal, buttonNormal, textColor);
            SetGuiStyleState(skin.button.hover, buttonHover, textColor);
            SetGuiStyleState(skin.button.active, buttonActive, Color.white);
            SetGuiStyleState(skin.button.focused, buttonHover, textColor);
            SetGuiStyleState(skin.button.onNormal, buttonActive, Color.white);
            SetGuiStyleState(skin.button.onHover, buttonActive, Color.white);
            SetGuiStyleState(skin.button.onActive, buttonActive, Color.white);
            SetGuiStyleState(skin.button.onFocused, buttonActive, Color.white);

            skin.toggle = new GUIStyle(baseSkin.toggle)
            {
                font = bodyFont,
                fontSize = 14,
                padding = new RectOffset(10, 10, 6, 6),
                margin = new RectOffset(4, 4, 4, 4),
                border = new RectOffset(8, 8, 8, 8),
            };
            SetGuiStyleState(skin.toggle.normal, buttonNormal, textColor);
            SetGuiStyleState(skin.toggle.hover, buttonHover, textColor);
            SetGuiStyleState(skin.toggle.active, buttonActive, Color.white);
            SetGuiStyleState(skin.toggle.focused, buttonHover, textColor);
            SetGuiStyleState(skin.toggle.onNormal, buttonActive, Color.white);
            SetGuiStyleState(skin.toggle.onHover, buttonActive, Color.white);
            SetGuiStyleState(skin.toggle.onActive, buttonActive, Color.white);
            SetGuiStyleState(skin.toggle.onFocused, buttonActive, Color.white);

            skin.horizontalSlider = new GUIStyle(baseSkin.horizontalSlider)
            {
                fixedHeight = 10f,
                margin = new RectOffset(8, 8, 8, 8),
            };
            SetGuiStyleState(skin.horizontalSlider.normal, sliderTrack, Color.white);
            SetGuiStyleState(skin.horizontalSlider.hover, sliderTrack, Color.white);
            skin.horizontalSliderThumb = new GUIStyle(baseSkin.horizontalSliderThumb)
            {
                fixedWidth = 16f,
                fixedHeight = 20f,
                margin = new RectOffset(0, 0, 3, 3),
            };
            SetGuiStyleState(skin.horizontalSliderThumb.normal, sliderThumb, Color.white);
            SetGuiStyleState(skin.horizontalSliderThumb.hover, sliderThumb, Color.white);
            SetGuiStyleState(skin.horizontalSliderThumb.active, sliderThumb, Color.white);

            skin.verticalScrollbar = new GUIStyle(baseSkin.verticalScrollbar)
            {
                fixedWidth = 12f,
            };
            skin.verticalScrollbarThumb = new GUIStyle(baseSkin.verticalScrollbarThumb)
            {
                fixedWidth = 12f,
            };
            SetGuiStyleState(skin.verticalScrollbar.normal, sliderTrack, Color.white);
            SetGuiStyleState(skin.verticalScrollbarThumb.normal, sliderThumb, Color.white);
            skin.horizontalScrollbar = new GUIStyle(baseSkin.horizontalScrollbar)
            {
                fixedHeight = 12f,
            };
            skin.horizontalScrollbarThumb = new GUIStyle(baseSkin.horizontalScrollbarThumb)
            {
                fixedHeight = 12f,
            };
            SetGuiStyleState(skin.horizontalScrollbar.normal, sliderTrack, Color.white);
            SetGuiStyleState(skin.horizontalScrollbarThumb.normal, sliderThumb, Color.white);

            skin.scrollView = new GUIStyle(baseSkin.scrollView)
            {
                padding = new RectOffset(2, 2, 2, 2),
            };
            return skin;
        }

        private static Color MenuPanelGradientStartColor()
        {
            return new Color(13f / 255f, 17f / 255f, 38f / 255f, 0.90f);
        }

        private static Color MenuPanelGradientEndColor()
        {
            return new Color(7f / 255f, 9f / 255f, 22f / 255f, 0.94f);
        }

        private static Color MenuPanelBorderColor()
        {
            return new Color(103f / 255f, 232f / 255f, 249f / 255f, 0.16f);
        }

        private static float MenuPanelGradientAngleDegrees()
        {
            return 160f;
        }

        private static float BrowserResultCardWidth(float safeAreaWidth)
        {
            return Mathf.Min(
                BrowserResultCardMaxWidth,
                Mathf.Max(1f, safeAreaWidth - BrowserResultCardViewportInset));
        }

        private static float BrowserResultCardHeight(float safeAreaHeight)
        {
            return Mathf.Min(
                BrowserResultCardMaxHeight,
                Mathf.Max(1f, safeAreaHeight - BrowserResultCardViewportInset));
        }

        private GUIStyle ResultActionButtonStyle(bool primary)
        {
            if (primary && _resultActionPrimaryStyle != null) return _resultActionPrimaryStyle;
            if (!primary && _resultActionSecondaryStyle != null) return _resultActionSecondaryStyle;

            var style = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                font = BrowserBodyFont(),
                fontSize = ResultActionLabelFontSize(),
                fontStyle = FontStyle.Normal,
                padding = new RectOffset(14, 14, 10, 10),
                margin = new RectOffset(0, 0, 0, 0),
                border = new RectOffset(7, 7, 7, 7),
                fixedHeight = ResultActionButtonHeight(),
            };
            var background = RoundedGradientGuiTexture(
                primary
                    ? new Color(0.031f, 0.114f, 0.153f, 0.84f)
                    : new Color(0.059f, 0.090f, 0.133f, 0.72f),
                primary
                    ? new Color(0.031f, 0.114f, 0.153f, 0.84f)
                    : new Color(0.059f, 0.090f, 0.133f, 0.72f),
                primary
                    ? new Color(0.404f, 0.851f, 0.953f, 0.72f)
                    : new Color(0.580f, 0.639f, 0.722f, 0.23f),
                180,
                46,
                7f,
                primary ? "VoidFall Result Primary Action" : "VoidFall Result Secondary Action");
            var hoverBackground = RoundedGradientGuiTexture(
                primary ? ResultActionPrimaryHoverFill() : ResultActionSecondaryHoverFill(),
                primary ? ResultActionPrimaryHoverFill() : ResultActionSecondaryHoverFill(),
                primary ? ResultActionPrimaryHoverBorder() : ResultActionSecondaryHoverBorder(),
                180,
                46,
                7f,
                primary
                    ? "VoidFall Result Primary Action Hover"
                    : "VoidFall Result Secondary Action Hover");
            var textColor = primary
                ? new Color(0.875f, 0.973f, 0.992f, 1f)
                : new Color(0.859f, 0.898f, 0.933f, 1f);
            SetGuiStyleState(style.normal, background, textColor);
            SetGuiStyleState(style.hover, hoverBackground, textColor);
            SetGuiStyleState(style.active, background, textColor);
            SetGuiStyleState(style.focused, hoverBackground, textColor);
            if (primary) _resultActionPrimaryStyle = style;
            else _resultActionSecondaryStyle = style;
            return style;
        }

        private static float ResultActionButtonHeight()
        {
            return 46f;
        }

        private static float ResultActionButtonGap()
        {
            return 9f;
        }

        private static bool ResultActionButtonIsActive(Rect rect)
        {
            var currentEvent = Event.current;
            if (currentEvent == null || !rect.Contains(currentEvent.mousePosition)) return false;
            if (currentEvent.type == EventType.MouseDown || currentEvent.type == EventType.MouseDrag)
                return true;
            return currentEvent.type == EventType.Repaint && GUIUtility.hotControl != 0;
        }

        private static Texture2D MenuStartOuterShadowTexture(
            int buttonWidth,
            int buttonHeight,
            bool hovered,
            float breathe)
        {
            buttonWidth = Mathf.Max(16, Mathf.RoundToInt(buttonWidth / 16f) * 16);
            buttonHeight = Mathf.Max(16, Mathf.RoundToInt(buttonHeight / 16f) * 16);
            var state = MenuStartShadowState(hovered, breathe);
            var bucket = Mathf.RoundToInt(state * 8f);
            var key = buttonWidth + "x" + buttonHeight + "-" + (hovered ? "hover" : "b" + bucket);
            if (_menuStartOuterShadowCache.TryGetValue(key, out var cached)) return cached;

            var margin = MenuStartShadowTextureMarginForState(hovered, state);
            var width = buttonWidth + margin * 2;
            var height = buttonHeight + margin * 2;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "VoidFall Menu Start Outer Shadow " + key,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color[width * height];
            var halfWidth = buttonWidth * 0.5f;
            var halfHeight = buttonHeight * 0.5f;
            var centerX = margin + halfWidth;
            var centerY = margin + halfHeight;
            var blurRadius = MenuStartActionOuterShadowBlurRadius(hovered, state);
            var blurDenominator = 2f * blurRadius * blurRadius;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var distance = RoundedRectSignedDistance(
                        x - centerX,
                        y - centerY,
                        halfWidth,
                        halfHeight,
                        MenuStartActionShadowCornerRadius());
                    var safeDistance = Mathf.Max(0f, distance);
                    var alpha = distance >= 0f
                        ? MenuStartActionOuterShadowAlpha(hovered, state) * Mathf.Exp(
                            -(safeDistance * safeDistance) / blurDenominator)
                        : 0f;
                    pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            CacheTextureBounded(_menuStartOuterShadowCache, key, texture);
            return texture;
        }

        private static Texture2D MenuStartInsetShadowTexture(
            int buttonWidth,
            int buttonHeight,
            bool hovered,
            float breathe)
        {
            buttonWidth = Mathf.Max(16, Mathf.RoundToInt(buttonWidth / 16f) * 16);
            buttonHeight = Mathf.Max(16, Mathf.RoundToInt(buttonHeight / 16f) * 16);
            var state = MenuStartShadowState(hovered, breathe);
            var bucket = Mathf.RoundToInt(state * 8f);
            var key = buttonWidth + "x" + buttonHeight + "-" + (hovered ? "hover" : "b" + bucket);
            if (_menuStartInsetShadowCache.TryGetValue(key, out var cached)) return cached;

            var texture = new Texture2D(buttonWidth, buttonHeight, TextureFormat.RGBA32, false)
            {
                name = "VoidFall Menu Start Inset Shadow " + key,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color[buttonWidth * buttonHeight];
            var halfWidth = buttonWidth * 0.5f;
            var halfHeight = buttonHeight * 0.5f;
            var blurRadius = MenuStartActionInsetShadowBlurRadius(hovered, state);
            var blurWidth = Mathf.Max(1f, blurRadius * 0.45f);
            var blurDenominator = 2f * blurWidth * blurWidth;
            for (var y = 0; y < buttonHeight; y++)
            {
                for (var x = 0; x < buttonWidth; x++)
                {
                    var distance = RoundedRectSignedDistance(
                        x - halfWidth,
                        y - halfHeight,
                        halfWidth,
                        halfHeight,
                        MenuStartActionShadowCornerRadius());
                    var edgeDepth = Mathf.Max(0f, -distance);
                    var alpha = distance <= 0f
                        ? MenuStartActionInsetShadowAlpha(hovered, state) * Mathf.Exp(
                            -(edgeDepth * edgeDepth) / blurDenominator)
                        : 0f;
                    pixels[y * buttonWidth + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            CacheTextureBounded(_menuStartInsetShadowCache, key, texture);
            return texture;
        }

        private static float MenuStartShadowState(bool hovered, float breathe)
        {
            if (hovered) return 1f;
            return Mathf.Clamp01(Mathf.Round(Mathf.Clamp01(breathe) * 8f) / 8f);
        }

        private static float MenuStartActionOuterShadowBlurRadius(bool hovered, float breathe)
        {
            var state = MenuStartShadowState(hovered, breathe);
            return hovered ? 34f : Mathf.Lerp(18f, 34f, state);
        }

        private static float MenuStartActionOuterShadowAlpha(bool hovered, float breathe)
        {
            var state = MenuStartShadowState(hovered, breathe);
            return hovered ? 0.45f : Mathf.Lerp(0.19f, 0.38f, state);
        }

        private static float MenuStartActionInsetShadowBlurRadius(bool hovered, float breathe)
        {
            var state = MenuStartShadowState(hovered, breathe);
            return hovered ? 24f : Mathf.Lerp(18f, 24f, state);
        }

        private static float MenuStartActionInsetShadowAlpha(bool hovered, float breathe)
        {
            var state = MenuStartShadowState(hovered, breathe);
            return hovered ? 0.20f : Mathf.Lerp(0.10f, 0.16f, state);
        }

        private static float MenuStartActionShadowCornerRadius()
        {
            return 6f;
        }

        private static int MenuStartShadowTextureMargin(bool hovered, float breathe)
        {
            return MenuStartShadowTextureMarginForState(hovered, MenuStartShadowState(hovered, breathe));
        }

        private static int MenuStartShadowTextureMarginForState(bool hovered, float state)
        {
            return Mathf.CeilToInt(MenuStartActionOuterShadowBlurRadius(hovered, state) * 1.5f);
        }

        private static bool PrimaryActionButtonIsHovered(Rect rect)
        {
            var currentEvent = Event.current;
            return currentEvent != null && rect.Contains(currentEvent.mousePosition);
        }

        private static float ResultCardGap()
        {
            return 9f;
        }

        private void EnsureLevelUpPromptStyles()
        {
            if (_levelUpKickerStyle == null)
            {
                _levelUpKickerStyle = new GUIStyle(MenuBodyStyle())
                {
                    alignment = TextAnchor.MiddleCenter,
                    font = BrowserDisplayFont(),
                    fontSize = LevelUpKickerFontSize(),
                    fontStyle = FontStyle.Bold,
                    wordWrap = false,
                };
                _levelUpKickerStyle.normal.textColor = new Color(0.431f, 0.906f, 0.608f, 1f);
            }
            if (_levelUpTitleStyle == null)
            {
                _levelUpTitleStyle = new GUIStyle(MenuTitleStyle())
                {
                    alignment = TextAnchor.MiddleCenter,
                    font = BrowserDisplayFont(),
                    fontStyle = FontStyle.Bold,
                    wordWrap = false,
                };
                _levelUpTitleStyle.normal.textColor = new Color(0.945f, 0.961f, 0.976f, 1f);
            }
            _levelUpTitleStyle.fontSize = LevelUpTitleFontSize(Screen.safeArea.width);
        }

        private static int LevelUpKickerFontSize()
        {
            // React computes the 11px source kicker through the 1.15 UI scale.
            return BrowserNearestFontSize(11f * 1.15f);
        }

        private static float LevelUpContentWidth(float viewportWidth)
        {
            return Mathf.Min(930f, viewportWidth * 0.96f);
        }

        private static float LevelUpHeaderGap()
        {
            return 24f;
        }

        private static int LevelUpTitleFontSize(float viewportWidth)
        {
            const float uiTextScale = 1.15f;
            if (viewportWidth <= 430f)
                return BrowserNearestFontSize(22f * uiTextScale);
            var preferred = viewportWidth * 0.05f * uiTextScale;
            return BrowserNearestFontSize(Mathf.Clamp(
                preferred,
                28f * uiTextScale,
                40f * uiTextScale));
        }

        private static float RerollButtonHeight()
        {
            return 46f;
        }

        private static int LevelUpGridColumns(float viewportWidth, int optionCount)
        {
            if (optionCount <= 0) return 0;
            return viewportWidth > 720f ? Mathf.Min(3, optionCount) : 1;
        }

        private static bool LevelUpUsesShortLandscapeLayout(float viewportWidth, float viewportHeight)
        {
            return viewportWidth > viewportHeight && viewportHeight <= 560f;
        }

        private void EnsureUpgradeCardStyles()
        {
            if (_upgradeCardMetaStyle != null) return;
            _upgradeCardMetaStyle = new GUIStyle(MenuBodyStyle())
            {
                alignment = TextAnchor.MiddleCenter,
                font = BrowserDisplayFont(),
                fontSize = UpgradeCardMetaFontSize(),
                fontStyle = FontStyle.Bold,
                wordWrap = false,
            };
            _upgradeCardNameStyle = new GUIStyle(MenuSectionStyle())
            {
                alignment = TextAnchor.MiddleCenter,
                font = BrowserDisplayFont(),
                fontSize = UpgradeCardNameFontSize(),
                wordWrap = true,
            };
            _upgradeCardNameStyle.normal.textColor = new Color(0.97f, 0.98f, 1f, 1f);
            _upgradeCardDescriptionStyle = new GUIStyle(MenuBodyStyle())
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = UpgradeCardDescriptionFontSize(),
                wordWrap = true,
            };
            _upgradeCardIndexStyle = new GUIStyle(MenuBodyStyle())
            {
                alignment = TextAnchor.UpperRight,
                fontSize = UpgradeCardIndexFontSize(),
                fontStyle = FontStyle.Bold,
                wordWrap = false,
            };
            _upgradeCardMobileMetaStyle = new GUIStyle(_upgradeCardMetaStyle)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = UpgradeCardMetaFontSize(),
            };
            _upgradeCardMobileNameStyle = new GUIStyle(_upgradeCardNameStyle)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = UpgradeCardNameFontSize(),
            };
            _upgradeCardMobileDescriptionStyle = new GUIStyle(_upgradeCardDescriptionStyle)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = UpgradeCardDescriptionFontSize(),
            };
        }

        private static void ApplyUpgradeCardTransform(Rect rect, float entranceProgress)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint) return;
            if (entranceProgress < 1f)
            {
                var scale = Mathf.LerpUnclamped(0.94f, 1f, entranceProgress);
                GUIUtility.ScaleAroundPivot(
                    new Vector2(scale, scale),
                    rect.center);
                GUI.matrix = Matrix4x4.TRS(
                        new Vector3(0f, (1f - entranceProgress) * 26f, 0f),
                        Quaternion.identity,
                        Vector3.one) * GUI.matrix;
                return;
            }

            if (UpgradeCardIsHovered(rect))
            {
                GUIUtility.ScaleAroundPivot(
                    new Vector2(UpgradeCardHoverScale(), UpgradeCardHoverScale()),
                    rect.center);
                GUI.matrix = Matrix4x4.TRS(
                        new Vector3(0f, -UpgradeCardHoverLift(), 0f),
                        Quaternion.identity,
                        Vector3.one) * GUI.matrix;
            }
        }

        private void EnsureReviveStyles()
        {
            if (_reviveCardStyle == null)
            {
                _reviveCardStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = new RectOffset(25, 25, 25, 25),
                    margin = new RectOffset(0, 0, 0, 0),
                    border = new RectOffset(12, 12, 12, 12),
                };
                var card = RoundedGradientGuiTexture(
                    OverlayCardGradientStartColor(),
                    OverlayCardGradientEndColor(),
                    new Color(0.404f, 0.91f, 0.98f, 0.16f),
                    390,
                    240,
                    12f,
                    "VoidFall Revive Card",
                    OverlayCardGradientAngleDegrees());
                SetGuiStyleState(_reviveCardStyle.normal, card, Color.white);
                SetGuiStyleState(_reviveCardStyle.hover, card, Color.white);
            }
            if (_reviveKickerStyle == null)
            {
                _reviveKickerStyle = new GUIStyle(MenuBodyStyle())
                {
                    alignment = TextAnchor.MiddleCenter,
                    font = BrowserDisplayFont(),
                    fontSize = ReviveKickerFontSize(),
                    fontStyle = FontStyle.Bold,
                    wordWrap = false,
                };
                _reviveKickerStyle.normal.textColor = new Color(0.557f, 0.863f, 0.941f, 1f);
            }
            if (_reviveTitleStyle == null)
            {
                _reviveTitleStyle = new GUIStyle(MenuTitleStyle())
                {
                    alignment = TextAnchor.MiddleCenter,
                    font = BrowserDisplayFont(),
                    fontSize = ReviveTitleFontSize(),
                    fontStyle = FontStyle.Bold,
                    wordWrap = false,
                };
                _reviveTitleStyle.normal.textColor = new Color(0.945f, 0.961f, 0.976f, 1f);
            }
            if (_reviveButtonLabelStyle == null)
            {
                _reviveButtonLabelStyle = new GUIStyle(MenuBodyStyle())
                {
                    alignment = TextAnchor.MiddleLeft,
                    font = BrowserBodyFont(),
                    fontSize = ReviveButtonLabelFontSize(),
                    fontStyle = FontStyle.Normal,
                    wordWrap = false,
                };
            }
            if (_revivePrimaryButtonStyle == null)
            {
                _revivePrimaryButtonStyle = ReviveButtonStyle(
                    new Color(8f / 255f, 29f / 255f, 39f / 255f, 0.84f),
                    new Color(0.404f, 0.851f, 0.953f, 0.72f),
                    new Color(0.010f, 0.169f, 0.216f, 0.92f),
                    new Color(0.647f, 0.953f, 0.988f, 1f),
                    "VoidFall Revive Primary");
            }
            if (_reviveSecondaryButtonStyle == null)
            {
                _reviveSecondaryButtonStyle = ReviveButtonStyle(
                    new Color(0.059f, 0.090f, 0.133f, 0.72f),
                    new Color(0.58f, 0.64f, 0.72f, 0.23f),
                    new Color(0.078f, 0.125f, 0.180f, 0.82f),
                    new Color(0.404f, 0.851f, 0.953f, 0.43f),
                    "VoidFall Revive Secondary");
            }
        }

        private static GUIStyle ReviveButtonStyle(
            Color normalFill,
            Color normalBorder,
            Color hoverFill,
            Color hoverBorder,
            string key)
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                border = new RectOffset(7, 7, 7, 7),
            };
            var normal = RoundedGradientGuiTexture(
                normalFill,
                normalFill,
                normalBorder,
                390,
                46,
                7f,
                key);
            var hover = RoundedGradientGuiTexture(
                hoverFill,
                hoverFill,
                hoverBorder,
                390,
                46,
                7f,
                key + " Hover");
            SetGuiStyleState(style.normal, normal, Color.white);
            SetGuiStyleState(style.hover, hover, Color.white);
            SetGuiStyleState(style.active, hover, Color.white);
            SetGuiStyleState(style.focused, hover, Color.white);
            return style;
        }

        private static int ReviveKickerFontSize()
        {
            return BrowserNearestFontSize(10f * 1.15f);
        }

        private static int ReviveTitleFontSize()
        {
            return BrowserNearestFontSize(27f * 1.15f);
        }

        private static int ReviveButtonLabelFontSize()
        {
            return BrowserNearestFontSize(16f * 1.15f);
        }

        private static float ReviveActionIconBoxSize(bool prominent, float iconSize)
        {
            return prominent ? iconSize + 20f : iconSize;
        }

        private static float ReviveActionActiveScale()
        {
            return 0.988f;
        }

        private static bool ReviveActionButtonIsActive(Rect rect)
        {
            var currentEvent = Event.current;
            if (currentEvent == null || !rect.Contains(currentEvent.mousePosition)) return false;
            if (currentEvent.type == EventType.MouseDown || currentEvent.type == EventType.MouseDrag)
                return true;
            return currentEvent.type == EventType.Repaint && GUIUtility.hotControl != 0;
        }

        private static float ReviveCardWidth(float viewportWidth)
        {
            return Mathf.Min(390f, viewportWidth * 0.92f);
        }

        private static float ReviveCardHeight(bool paused)
        {
            // React's .pause-card is an auto-height grid: 25px padding on
            // both sides, a 1px border, 9px row gaps, the computed kicker
            // and title line boxes, their source margins, and 46px compact
            // actions. Pause has one additional action row.
            var actionCount = paused ? 3 : 2;
            return 25f * 2f + 2f +
                ReviveKickerLineHeight() + 4f +
                ReviveTitleLineHeight() + 9f +
                actionCount * ReviveActionHeight() +
                (actionCount + 1) * ReviveActionGap();
        }

        private static float ReviveKickerLineHeight()
        {
            return 10f * 1.15f;
        }

        private static float ReviveKickerToTitleGap()
        {
            // The kicker's 4px bottom margin plus the grid's 9px row gap.
            return 13f;
        }

        private static float ReviveTitleLineHeight()
        {
            return 27f * 1.15f * 1.05f;
        }

        private static float ReviveTitleToActionGap()
        {
            // The title's 9px bottom margin plus the grid's 9px row gap.
            return 18f;
        }

        private static float ReviveActionHeight()
        {
            return 46f;
        }

        private static float ReviveActionGap()
        {
            return 9f;
        }

        private static int HomeMenuColumns(float width)
        {
            return width <= 720f ? 2 : 3;
        }

        private static bool HomeMenuUsesLandscapeLayout(float width, float height)
        {
            // React's max-height:560px landscape rule switches the menu back
            // to three columns and a compact, top-aligned stack.
            return width > height && height <= 560f;
        }

        private static int HomeMenuColumnsForLayout(float width, float height)
        {
            return HomeMenuUsesLandscapeLayout(width, height) ? 3 : HomeMenuColumns(width);
        }

        private static int HomeMenuTitleFontSize(float width, bool landscapeLayout)
        {
            const float uiTextScale = 1.15f;
            if (landscapeLayout) return Mathf.RoundToInt(38f * uiTextScale);
            return Mathf.RoundToInt(Mathf.Clamp(width * 0.15f, 46f, 68f) * uiTextScale);
        }

        private static float HomeTitleDriftOffset(float elapsed)
        {
            return -4f + Mathf.Cos(elapsed * Mathf.PI * 2f / 5f) * 4f;
        }

        private static float HomeTitleShimmerProgress(float elapsed, int glyphIndex, int glyphCount)
        {
            var normalizedIndex = glyphIndex / (float)Mathf.Max(1, glyphCount - 1);
            var progress = normalizedIndex + Mathf.Repeat(elapsed / 6f, 1f);
            return progress > 1f ? progress - 1f : progress;
        }

        private static float HomeStartBreathe(float elapsed)
        {
            return 0.5f - 0.5f * Mathf.Cos(elapsed * Mathf.PI * 2f / 2.4f);
        }

        private static int WorkshopMenuColumns(float width)
        {
            return width <= 720f ? 1 : 2;
        }

        private static int WorkshopPreviewRankColumns(int itemCount)
        {
            return Mathf.Min(5, Mathf.Max(1, itemCount));
        }

        private static Texture2D HomeIconTexture()
        {
            if (_homeIconTexture != null) return _homeIconTexture;
            _homeIconTexture = Resources.Load<Texture2D>("VoidFall/HomeIconsRaster");
            if (_homeIconTexture != null) return _homeIconTexture;
            var sprite = Resources.Load<Sprite>("VoidFall/HomeIcons");
            _homeIconTexture = sprite != null
                ? sprite.texture
                : Resources.Load<Texture2D>("VoidFall/HomeIcons");
            return _homeIconTexture;
        }

        private static int HomeIconSlot(string iconId)
        {
            switch (iconId)
            {
                case "wrench": return 0;
                case "trophy": return 1;
                case "settings": return 2;
                case "coins": return 3;
                case "skull": return 4;
                default: return -1;
            }
        }

        private static Rect HomeIconUv(string iconId)
        {
            var slot = HomeIconSlot(iconId);
            if (slot < 0) return new Rect(0f, 0f, 1f, 1f);
            var column = slot % 3;
            var row = slot / 3;
            return new Rect(
                column / 3f,
                1f - ((row + 1) / 2f),
                1f / 3f,
                1f / 2f);
        }

        private static bool ProfilePageHeaderVisible(bool mainMenuBrowsing, MenuPage page)
        {
            return mainMenuBrowsing && page != MenuPage.Home;
        }

        private static string ProfilePageKicker(MenuPage page)
        {
            switch (page)
            {
                case MenuPage.Workshop:
                    return "Permanent upgrades";
                case MenuPage.Settings:
                    return "Local preferences";
                default:
                    return "Local profile";
            }
        }

        private static string ProfilePageTitle(MenuPage page)
        {
            switch (page)
            {
                case MenuPage.Workshop:
                    return "Workshop";
                case MenuPage.Settings:
                    return "Settings";
                default:
                    return "Records";
            }
        }

        private void HandleWorkshopRowInteraction(string id, string controlName, Rect rowRect)
        {
            var currentEvent = Event.current;
            if (currentEvent == null) return;

            if (currentEvent.type == EventType.KeyDown || currentEvent.type == EventType.KeyUp)
            {
                _workshopFocusVisible = true;
            }
            else if (currentEvent.type == EventType.MouseDown ||
                     currentEvent.type == EventType.MouseMove ||
                     currentEvent.type == EventType.MouseUp)
            {
                _workshopFocusVisible = false;
            }

            if (currentEvent.type == EventType.MouseMove)
            {
                var nextPreview = WorkshopPreviewAfterPointerMove(
                    _workshopPreviewId,
                    id,
                    rowRect.Contains(currentEvent.mousePosition),
                    true);
                if (nextPreview != _workshopPreviewId)
                {
                    _workshopPreviewId = nextPreview;
                    GUI.changed = true;
                }
            }
            else if (currentEvent.type == EventType.MouseUp && currentEvent.button == 0 &&
                     rowRect.Contains(currentEvent.mousePosition))
            {
                _workshopPreviewId = id;
                GUI.FocusControl(controlName);
                GUI.changed = true;
            }

            if (currentEvent.type != EventType.Repaint) return;

            var focused = GUI.GetNameOfFocusedControl() == controlName;
            var wasFocused = _workshopFocusedId == id;
            var nextFocusedPreview = WorkshopPreviewAfterFocusChange(
                _workshopPreviewId,
                id,
                focused,
                wasFocused);
            if (focused && !wasFocused)
                _workshopFocusedId = id;
            else if (!focused && wasFocused)
                _workshopFocusedId = null;

            if (nextFocusedPreview != _workshopPreviewId)
            {
                _workshopPreviewId = nextFocusedPreview;
                GUI.changed = true;
            }

            if (_workshopFocusVisible && focused)
                DrawWorkshopFocusOutline(rowRect);
        }

        private static float WorkshopFocusOutlineThickness()
        {
            return 2f;
        }

        private static float WorkshopFocusOutlineOffset()
        {
            return 2f;
        }

        private static string WorkshopRowControlName(string id)
        {
            return "VoidFall_WorkshopRow_" + id;
        }

        private static string WorkshopPreviewAfterPointerMove(
            string currentPreviewId,
            string rowId,
            bool rowContainsPointer,
            bool isMousePointer)
        {
            if (!isMousePointer) return currentPreviewId;
            if (rowContainsPointer) return rowId;
            return currentPreviewId == rowId ? null : currentPreviewId;
        }

        private static string WorkshopPreviewAfterFocusChange(
            string currentPreviewId,
            string rowId,
            bool focused,
            bool wasFocused)
        {
            if (focused && !wasFocused) return rowId;
            if (!focused && wasFocused && currentPreviewId == rowId) return null;
            return currentPreviewId;
        }

        private static Texture2D WorkshopCoinsTexture()
        {
            if (_workshopCoinsTexture != null) return _workshopCoinsTexture;
            _workshopCoinsTexture = Resources.Load<Texture2D>("VoidFall/WorkshopCoinsRaster");
            if (_workshopCoinsTexture == null)
                _workshopCoinsTexture = Resources.Load<Texture2D>("VoidFall/WorkshopCoins");
            return _workshopCoinsTexture;
        }

        private static GUIStyle MenuBodyStyleForButton()
        {
            if (_workshopPurchaseStyle == null)
            {
                _workshopPurchaseStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    fontSize = 12,
                    wordWrap = false,
                };
            }
            return _workshopPurchaseStyle;
        }

        private GUIStyle WorkshopRowStyle(bool previewing)
        {
            if (_workshopRowStyle == null)
            {
                _workshopRowStyle = CreateWorkshopRowStyle(
                    new Color(0.031f, 0.051f, 0.082f, 0.56f),
                    new Color(0.031f, 0.051f, 0.082f, 0.56f),
                    new Color(0.58f, 0.64f, 0.69f, 0.14f),
                    "VoidFall Workshop Row");
            }
            if (_workshopPreviewRowStyle == null)
            {
                _workshopPreviewRowStyle = CreateWorkshopRowStyle(
                    new Color(0.043f, 0.114f, 0.169f, 0.80f),
                    new Color(0.043f, 0.114f, 0.169f, 0.80f),
                    new Color(0.404f, 0.91f, 1f, 0.54f),
                    "VoidFall Workshop Row Preview");
            }
            return previewing ? _workshopPreviewRowStyle : _workshopRowStyle;
        }

        private static GUIStyle CreateWorkshopRowStyle(
            Color top,
            Color bottom,
            Color borderColor,
            string textureName)
        {
            var style = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(11, 11, 11, 11),
                margin = new RectOffset(0, 0, 0, 0),
                border = new RectOffset(7, 7, 7, 7),
            };
            var normal = RoundedGradientGuiTexture(top, bottom, borderColor, 64, 64, 7f, textureName);
            var hover = RoundedGradientGuiTexture(
                new Color(0.039f, 0.075f, 0.114f, 0.72f),
                new Color(0.039f, 0.075f, 0.114f, 0.72f),
                new Color(0.49f, 0.83f, 0.99f, 0.27f),
                64,
                64,
                7f,
                textureName + " Hover");
            SetGuiStyleState(style.normal, normal, Color.white);
            SetGuiStyleState(style.hover, hover, Color.white);
            SetGuiStyleState(style.focused, hover, Color.white);
            return style;
        }

        private GUIStyle WorkshopPreviewPanelStyle()
        {
            if (_workshopPreviewPanelStyle == null)
            {
                _workshopPreviewPanelStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0),
                    border = new RectOffset(9, 9, 9, 9),
                };
                var background = RoundedGradientGuiTexture(
                    new Color(0.012f, 0.027f, 0.071f, 0.80f),
                    new Color(0.012f, 0.027f, 0.071f, 0.80f),
                    new Color(0.404f, 0.91f, 1f, 0.18f),
                    64,
                    64,
                    9f,
                    "VoidFall Workshop Preview Panel");
                SetGuiStyleState(_workshopPreviewPanelStyle.normal, background, Color.white);
                SetGuiStyleState(_workshopPreviewPanelStyle.hover, background, Color.white);
                SetGuiStyleState(_workshopPreviewPanelStyle.focused, background, Color.white);
            }
            return _workshopPreviewPanelStyle;
        }

        private GUIStyle WorkshopPreviewHeaderStyle()
        {
            if (_workshopPreviewHeaderStyle == null)
            {
                _workshopPreviewHeaderStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = new RectOffset(13, 13, 9, 6),
                    margin = new RectOffset(0, 0, 0, 0),
                    border = new RectOffset(0, 0, 0, 0),
                };
                var background = RoundedGradientGuiTexture(
                    new Color(0.059f, 0.11f, 0.212f, 0.78f),
                    new Color(0.027f, 0.047f, 0.106f, 0.88f),
                    Color.clear,
                    64,
                    64,
                    2f,
                    "VoidFall Workshop Preview Header");
                SetGuiStyleState(_workshopPreviewHeaderStyle.normal, background, Color.white);
                SetGuiStyleState(_workshopPreviewHeaderStyle.hover, background, Color.white);
            }
            return _workshopPreviewHeaderStyle;
        }

        private GUIStyle WorkshopPreviewKickerStyle()
        {
            if (_workshopPreviewKickerStyle == null)
            {
                _workshopPreviewKickerStyle = new GUIStyle(MenuBodyStyle())
                {
                    font = BrowserDisplayFont(),
                    fontSize = 8,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(0, 0, 0, 0),
                    fixedHeight = 12f,
                    normal = { textColor = new Color(0.404f, 0.91f, 0.976f, 1f) },
                };
            }
            return _workshopPreviewKickerStyle;
        }

        private GUIStyle WorkshopPreviewTitleStyle()
        {
            if (_workshopPreviewTitleStyle == null)
            {
                _workshopPreviewTitleStyle = new GUIStyle(MenuBodyStyle())
                {
                    font = BrowserDisplayFont(),
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(0, 0, 0, 0),
                    clipping = TextClipping.Clip,
                    normal = { textColor = new Color(0.898f, 0.929f, 0.957f, 1f) },
                };
            }
            return _workshopPreviewTitleStyle;
        }

        private GUIStyle WorkshopPreviewRankStripStyle()
        {
            if (_workshopPreviewRankStripStyle == null)
            {
                _workshopPreviewRankStripStyle = new GUIStyle
                {
                    padding = new RectOffset(1, 1, 1, 1),
                    margin = new RectOffset(0, 0, 0, 0),
                };
                var background = RoundedGradientGuiTexture(
                    new Color(0.027f, 0.11f, 0.14f, 0.08f),
                    new Color(0.027f, 0.11f, 0.14f, 0.08f),
                    Color.clear,
                    8,
                    8,
                    0f,
                    "VoidFall Workshop Preview Rank Strip");
                SetGuiStyleState(_workshopPreviewRankStripStyle.normal, background, Color.white);
            }
            return _workshopPreviewRankStripStyle;
        }

        private GUIStyle WorkshopPreviewRankStyle(bool active)
        {
            if (_workshopPreviewRankStyle == null || _workshopPreviewRankActiveStyle == null)
            {
                _workshopPreviewRankStyle ??= CreateWorkshopPreviewRankStyle(
                    new Color(0.019f, 0.039f, 0.086f, 0.96f),
                    "VoidFall Workshop Preview Rank");
                _workshopPreviewRankActiveStyle ??= CreateWorkshopPreviewRankStyle(
                    new Color(0.055f, 0.455f, 0.565f, 0.18f),
                    "VoidFall Workshop Preview Rank Active");
            }
            return active ? _workshopPreviewRankActiveStyle : _workshopPreviewRankStyle;
        }

        private static GUIStyle CreateWorkshopPreviewRankStyle(Color color, string textureName)
        {
            var style = new GUIStyle
            {
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
            };
            var background = RoundedGradientGuiTexture(color, color, Color.clear, 8, 8, 0f, textureName);
            SetGuiStyleState(style.normal, background, Color.white);
            SetGuiStyleState(style.hover, background, Color.white);
            SetGuiStyleState(style.focused, background, Color.white);
            return style;
        }

        private GUIStyle WorkshopPreviewRankTextStyle(bool active)
        {
            if (_workshopPreviewRankTextStyle == null || _workshopPreviewRankActiveTextStyle == null)
            {
                _workshopPreviewRankTextStyle = new GUIStyle(MenuBodyStyle())
                {
                    font = BrowserDisplayFont(),
                    fontSize = 10,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(0, 0, 0, 0),
                    normal = { textColor = new Color(0.443f, 0.514f, 0.6f, 1f) },
                };
                _workshopPreviewRankActiveTextStyle = new GUIStyle(_workshopPreviewRankTextStyle)
                {
                    normal = { textColor = new Color(0.647f, 0.953f, 0.988f, 1f) },
                };
            }
            return active ? _workshopPreviewRankActiveTextStyle : _workshopPreviewRankTextStyle;
        }

        private GUIStyle WorkshopIconFrameStyle()
        {
            if (_workshopIconFrameStyle == null)
            {
                _workshopIconFrameStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0),
                    border = new RectOffset(6, 6, 6, 6),
                };
                var background = RoundedGradientGuiTexture(
                    new Color(0.055f, 0.455f, 0.565f, 0.16f),
                    new Color(0.055f, 0.455f, 0.565f, 0.16f),
                    Color.clear,
                    36,
                    36,
                    6f,
                    "VoidFall Workshop Icon Frame");
                SetGuiStyleState(_workshopIconFrameStyle.normal, background, Color.white);
                SetGuiStyleState(_workshopIconFrameStyle.hover, background, Color.white);
            }
            return _workshopIconFrameStyle;
        }

        private GUIStyle WorkshopNameStyle()
        {
            if (_workshopNameStyle == null)
            {
                _workshopNameStyle = new GUIStyle(GUI.skin.label)
                {
                    font = BrowserDisplayFont(),
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(0, 0, 0, 0),
                    normal = { textColor = new Color(0.84f, 0.88f, 0.91f, 1f) },
                };
            }
            return _workshopNameStyle;
        }

        private GUIStyle WorkshopDetailStyle()
        {
            if (_workshopDetailStyle == null)
            {
                _workshopDetailStyle = new GUIStyle(GUI.skin.label)
                {
                    font = BrowserBodyFont(),
                    fontSize = 11,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(0, 0, 0, 0),
                    normal = { textColor = new Color(0.50f, 0.56f, 0.63f, 1f) },
                };
            }
            return _workshopDetailStyle;
        }

        private GUIStyle WorkshopPipFilledStyle()
        {
            if (_workshopPipFilledStyle == null)
            {
                _workshopPipFilledStyle = CreateWorkshopPipStyle(
                    new Color(0.22f, 0.74f, 0.97f, 1f),
                    "VoidFall Workshop Pip Filled");
            }
            return _workshopPipFilledStyle;
        }

        private GUIStyle WorkshopPipEmptyStyle()
        {
            if (_workshopPipEmptyStyle == null)
            {
                _workshopPipEmptyStyle = CreateWorkshopPipStyle(
                    new Color(0.39f, 0.45f, 0.55f, 0.28f),
                    "VoidFall Workshop Pip Empty");
            }
            return _workshopPipEmptyStyle;
        }

        private static GUIStyle CreateWorkshopPipStyle(Color color, string textureName)
        {
            var style = new GUIStyle
            {
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
            };
            var texture = RoundedGradientGuiTexture(color, color, Color.clear, 17, 3, 1f, textureName);
            SetGuiStyleState(style.normal, texture, Color.white);
            SetGuiStyleState(style.hover, texture, Color.white);
            return style;
        }

        private static string WorkshopIconId(string workshopId)
        {
            switch (workshopId)
            {
                case "integrity": return "plating";
                case "power": return "pistol";
                case "mobility": return "footprints";
                case "recovery": return "repair";
                case "magnet": return "collector";
                case "precision": return "target";
                case "arsenal": return "scattergun";
                case "protocol": return "regenerator";
                default: return "repair";
            }
        }

        private static Texture2D WorkshopIconTexture(string iconId)
        {
            if (iconId == "repair") return UpgradeOptionIconTexture("repair");
            if (iconId == "footprints" || iconId == "target")
            {
                if (_workshopIconTexture != null) return _workshopIconTexture;
                _workshopIconTexture = Resources.Load<Texture2D>("VoidFall/WorkshopIconsRaster");
                if (_workshopIconTexture != null) return _workshopIconTexture;
                var sprite = Resources.Load<Sprite>("VoidFall/WorkshopIcons");
                _workshopIconTexture = sprite != null
                    ? sprite.texture
                    : Resources.Load<Texture2D>("VoidFall/WorkshopIcons");
                return _workshopIconTexture;
            }
            return BuildChipIconTexture();
        }

        private static Rect WorkshopIconUv(string iconId)
        {
            if (iconId == "footprints") return new Rect(0f, 0f, 0.5f, 1f);
            if (iconId == "target") return new Rect(0.5f, 0f, 0.5f, 1f);
            if (iconId == "repair") return new Rect(0f, 0f, 1f, 1f);
            return BuildChipIconUv(iconId);
        }

        private static float WorkshopPreviewSourceScale(Rect contentRect)
        {
            // WorkshopPreview draws its upgrade layers in a 300x300 source
            // frame inside the browser's 600x340 canvas. The browser then
            // scales the complete canvas responsively, so source-sized player
            // and ring draws must use the same 300px-to-content ratio.
            return Mathf.Max(0f, contentRect.width) / 300f;
        }

        private static float WorkshopPowerPulseSize(int power)
        {
            var rank = Mathf.Clamp(power, 1, 3);
            // Dot() has a 4.5-unit source-radius inside a 24-unit sprite box;
            // the browser pulse radius is exactly 3 + power.
            return 24f * (3f + rank) / 4.5f;
        }

        private static float WorkshopMobilityTrailLength(int rank, int index, float time)
        {
            return 35f + rank * 9f + Mathf.Sin(time * 8f + index) * 5f;
        }

        private string WorkshopPreviewSelection()
        {
            if (string.IsNullOrEmpty(_workshopPreviewId) || _saveData?.workshop == null)
                return null;
            foreach (var entry in _saveData.workshop)
            {
                if (entry != null && entry.id == _workshopPreviewId)
                    return _workshopPreviewId;
            }
            return null;
        }

        private int PreviewWorkshopRank(string id, string selectedId)
        {
            var rank = WorkshopRank(id);
            if (id != selectedId) return rank;
            var maxRank = id == "protocol" ? 1 : SaveStore.WorkshopMaxRank;
            return Mathf.Min(maxRank, rank + 1);
        }

        private static int RecordMenuColumns(float width)
        {
            return width <= 720f ? 2 : 3;
        }

        private static float RecordMetricGridWidth(float viewportWidth)
        {
            var panelWidth = MenuPanelWidth(viewportWidth, MenuPage.Records);
            var contentWidth = Mathf.Max(1f, panelWidth - 44f);
            // Narrow records overflow vertically, so the source content loses
            // the 12px vertical scrollbar track from its usable row width.
            return Mathf.Max(1f, contentWidth - (viewportWidth <= 720f ? 12f : 4f));
        }

        private GUIStyle MenuPanelShadowStyle()
        {
            if (_menuPanelShadowStyle != null) return _menuPanelShadowStyle;

            _menuPanelShadowStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                border = new RectOffset(12, 12, 12, 12),
            };
            var shadow = RoundedGradientGuiTexture(
                new Color(0f, 0f, 0f, 0.34f),
                new Color(0f, 0f, 0f, 0.34f),
                Color.clear,
                64,
                64,
                12f,
                "VoidFall Menu Panel Shadow");
            SetGuiStyleState(_menuPanelShadowStyle.normal, shadow, Color.clear);
            SetGuiStyleState(_menuPanelShadowStyle.hover, shadow, Color.clear);
            return _menuPanelShadowStyle;
        }

        private static float MenuPanelMaxWidth(MenuPage page)
        {
            switch (page)
            {
                case MenuPage.Workshop:
                    return 880f;
                case MenuPage.Settings:
                    return 580f;
                default:
                    return 610f;
            }
        }

        private static float MenuPanelWidth(float safeAreaWidth, MenuPage page)
        {
            if (page == MenuPage.Settings)
                return SettingsPanelWidth(safeAreaWidth);
            return Mathf.Min(
                MenuPanelMaxWidth(page),
                Mathf.Max(1f, safeAreaWidth * 0.94f));
        }

        private static float MenuPanelMaxHeight(float safeAreaHeight)
        {
            return Mathf.Min(760f, Mathf.Max(1f, safeAreaHeight * 0.90f));
        }

        private static Rect MenuPanelAccentRect(float x, float y, float width)
        {
            return new Rect(x + width * 0.07f, y, width * 0.37f, 2f);
        }

        private static Rect MenuPanelInsetHighlightRect(float x, float y, float width)
        {
            return new Rect(x + 1f, y + 1f, Mathf.Max(0f, width - 2f), 1f);
        }

        private static Rect MenuPanelShadowRect(float x, float y, float width, float height)
        {
            return new Rect(x - 18f, y + 10f, width + 36f, height + 38f);
        }

        private static bool SettingsToggleRowWasClicked(Rect rowRect, Event currentEvent)
        {
            return currentEvent != null &&
                SettingsToggleRowWasClicked(
                    rowRect,
                    currentEvent.type,
                    currentEvent.button,
                    currentEvent.mousePosition);
        }

        private static bool SettingsToggleRowWasClicked(
            Rect rowRect,
            EventType eventType,
            int button,
            Vector2 mousePosition)
        {
            return eventType == EventType.MouseUp &&
                button == 0 &&
                rowRect.Contains(mousePosition);
        }

        private static bool SettingsQualityLabelWasClicked(
            Rect rowRect,
            Rect controlRect,
            Event currentEvent)
        {
            return currentEvent != null &&
                SettingsQualityLabelWasClicked(
                    rowRect,
                    controlRect,
                    currentEvent.type,
                    currentEvent.button,
                    currentEvent.mousePosition);
        }

        private static bool SettingsQualityLabelWasClicked(
            Rect rowRect,
            Rect controlRect,
            EventType eventType,
            int button,
            Vector2 mousePosition)
        {
            return eventType == EventType.MouseUp &&
                button == 0 &&
                rowRect.Contains(mousePosition) &&
                !controlRect.Contains(mousePosition);
        }

        private void HandleSettingsQualityKeyboard(SaveSettings settings)
        {
            var currentEvent = Event.current;
            if (currentEvent == null || currentEvent.type != EventType.KeyDown)
                return;

            var focused = GUI.GetNameOfFocusedControl() == "VoidFall.SettingsQualitySelect";
            if (!_settingsQualityMenuOpen && !focused)
                return;

            switch (currentEvent.keyCode)
            {
                case KeyCode.UpArrow:
                case KeyCode.DownArrow:
                case KeyCode.Home:
                case KeyCode.End:
                    var currentIndex = SettingsQualityIndex(settings.quality);
                    var nextIndex = currentIndex;
                    if (currentEvent.keyCode == KeyCode.UpArrow)
                        nextIndex = (currentIndex + SettingsQualityOptions.Length - 1) % SettingsQualityOptions.Length;
                    else if (currentEvent.keyCode == KeyCode.DownArrow)
                        nextIndex = (currentIndex + 1) % SettingsQualityOptions.Length;
                    else if (currentEvent.keyCode == KeyCode.Home)
                        nextIndex = 0;
                    else if (currentEvent.keyCode == KeyCode.End)
                        nextIndex = SettingsQualityOptions.Length - 1;

                    var nextQuality = SettingsQualityOptions[nextIndex];
                    if (settings.quality != nextQuality)
                    {
                        var previousSettings = CloneSettings(settings);
                        settings.quality = nextQuality;
                        ApplyAndCommitSettings(previousSettings);
                    }
                    currentEvent.Use();
                    return;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.Space:
                    if (_settingsQualityMenuOpen)
                    {
                        _settingsQualityMenuOpen = false;
                        currentEvent.Use();
                    }
                    return;
                case KeyCode.Escape:
                    if (_settingsQualityMenuOpen)
                    {
                        _settingsQualityMenuOpen = false;
                        currentEvent.Use();
                    }
                    return;
                default:
                    return;
            }
        }

        private static int SettingsQualityIndex(string quality)
        {
            for (var index = 0; index < SettingsQualityOptions.Length; index++)
            {
                if (SettingsQualityOptions[index] == quality)
                    return index;
            }
            return 0;
        }

        private static float SettingsToggleWidth()
        {
            return 43f;
        }

        private static float SettingsSelectWidth(float viewportWidth)
        {
            var panelWidth = SettingsPanelWidth(viewportWidth);
            var contentWidth = Mathf.Max(0f, panelWidth - 44f);
            return Mathf.Clamp(contentWidth - 150f - 14f, 140f, 220f);
        }

        private static float SettingsPanelWidth(float viewportWidth)
        {
            var width = Mathf.Min(Mathf.Max(0f, viewportWidth) * 0.94f, 580f);
            return Mathf.Min(width, Mathf.Max(1f, viewportWidth - 36f));
        }

        private static bool SettingsUsesStackedLayout(float viewportWidth)
        {
            // React's @media (max-width: 720px) changes .setting-row from
            // label/control columns to one column with an 8px row gap.
            return viewportWidth <= 720f;
        }

        private static float SettingsControlWidth(float viewportWidth)
        {
            if (!SettingsUsesStackedLayout(viewportWidth))
                return SettingsSelectWidth(viewportWidth);

            var panelWidth = SettingsPanelWidth(viewportWidth);
            // 22px menu-panel padding plus 11px setting-row padding on each
            // side leaves the full-width mobile control column.
            return Mathf.Max(1f, panelWidth - 66f);
        }

        private static float SettingsSelectHeight()
        {
            return 36f;
        }

        private static float SettingsSelectOptionHeight()
        {
            return 30f;
        }

        private static float SettingsToggleHeight()
        {
            return 24f;
        }

        private static float SettingsToggleKnobSize()
        {
            return 18f;
        }

        private static float SettingsToggleKnobInset()
        {
            return 3f;
        }

        private static float SettingsToggleKnobOnOffset()
        {
            return 19f;
        }

        private static float SettingsSliderHeight()
        {
            return 18f;
        }

        private static Texture2D SettingsSliderTrackTexture()
        {
            if (_settingsSliderTrackTexture != null) return _settingsSliderTrackTexture;
            _settingsSliderTrackTexture = RoundedGradientGuiTexture(
                new Color(0.30f, 0.31f, 0.31f, 1f),
                new Color(0.25f, 0.26f, 0.27f, 1f),
                new Color(0.49f, 0.50f, 0.50f, 1f),
                64,
                12,
                6f,
                "VoidFall Settings Slider Track");
            return _settingsSliderTrackTexture;
        }

        private static Texture2D SettingsSliderFillTexture()
        {
            if (_settingsSliderFillTexture != null) return _settingsSliderFillTexture;
            _settingsSliderFillTexture = RoundedGradientGuiTexture(
                new Color(0.133f, 0.827f, 0.933f, 1f),
                new Color(0.133f, 0.827f, 0.933f, 1f),
                Color.clear,
                64,
                12,
                6f,
                "VoidFall Settings Slider Fill");
            return _settingsSliderFillTexture;
        }

        private static Texture2D SettingsSliderThumbTexture()
        {
            if (_settingsSliderThumbTexture != null) return _settingsSliderThumbTexture;
            _settingsSliderThumbTexture = RoundedGradientGuiTexture(
                new Color(0.133f, 0.827f, 0.933f, 1f),
                new Color(0.133f, 0.827f, 0.933f, 1f),
                Color.clear,
                18,
                18,
                9f,
                "VoidFall Settings Slider Thumb");
            return _settingsSliderThumbTexture;
        }

        private static Texture2D SettingsToggleTrackTexture(bool isOn)
        {
            if (isOn && _settingsToggleTrackOnTexture != null)
                return _settingsToggleTrackOnTexture;
            if (!isOn && _settingsToggleTrackOffTexture != null)
                return _settingsToggleTrackOffTexture;

            var color = isOn
                ? new Color(14f / 255f, 116f / 255f, 144f / 255f, 1f)
                : new Color(38f / 255f, 51f / 255f, 66f / 255f, 1f);
            var texture = RoundedGradientGuiTexture(
                color,
                color,
                Color.clear,
                43,
                24,
                12f,
                isOn ? "VoidFall Settings Toggle Track On" : "VoidFall Settings Toggle Track Off");
            if (isOn) _settingsToggleTrackOnTexture = texture;
            else _settingsToggleTrackOffTexture = texture;
            return texture;
        }

        private static Texture2D SettingsToggleKnobTexture(bool isOn)
        {
            if (isOn && _settingsToggleKnobOnTexture != null)
                return _settingsToggleKnobOnTexture;
            if (!isOn && _settingsToggleKnobOffTexture != null)
                return _settingsToggleKnobOffTexture;

            var color = isOn
                ? new Color(207f / 255f, 250f / 255f, 254f / 255f, 1f)
                : new Color(154f / 255f, 169f / 255f, 185f / 255f, 1f);
            var texture = RoundedGradientGuiTexture(
                color,
                color,
                Color.clear,
                18,
                18,
                9f,
                isOn ? "VoidFall Settings Toggle Knob On" : "VoidFall Settings Toggle Knob Off");
            if (isOn) _settingsToggleKnobOnTexture = texture;
            else _settingsToggleKnobOffTexture = texture;
            return texture;
        }

        private static SaveSettings CloneSettings(SaveSettings settings)
        {
            var value = settings ?? new SaveSettings();
            return new SaveSettings
            {
                masterVolume = value.masterVolume,
                effectsVolume = value.effectsVolume,
                musicVolume = value.musicVolume,
                shake = value.shake,
                reducedMotion = value.reducedMotion,
                highContrast = value.highContrast,
                touchSize = value.touchSize,
                quality = value.quality,
            };
        }

        private void ApplyAndCommitSettings(SaveSettings previousSettings)
        {
            _settingsDirty = true;
            if (CommitSettings())
            {
                ApplySettings();
                return;
            }

            // Match React's save-first updateSettings contract: a failed
            // persistence write must not leave the rejected value active in
            // memory or in the live quality/audio controller.
            if (_saveData != null)
                _saveData.settings = CloneSettings(previousSettings);
            _settingsDirty = false;
            ApplySettings();
        }

        private void TryBuyWorkshop(string id)
        {
            if (_saveData?.workshop == null) return;
            foreach (var entry in _saveData.workshop)
            {
                if (entry == null || entry.id != id) continue;
                var cost = WorkshopCost(id, entry.rank);
                if (cost < 0)
                {
                    SetMenuNotice("That upgrade is already at maximum rank.");
                    return;
                }
                if (_saveData.parts < cost)
                {
                    SetMenuNotice($"Need {cost - _saveData.parts} more Parts.");
                    return;
                }
                _saveData.parts -= cost;
                entry.rank++;
                // Every other Save() call site reports failure. This one used to
                // let the exception escape through the IMGUI layout pass, which
                // left the player charged in memory with no explanation. Roll the
                // purchase back so the shown balance matches what is on disk.
                try
                {
                    _saveStore.Save(_saveData);
                }
                catch (Exception exception)
                {
                    _saveData.parts += cost;
                    entry.rank--;
                    Debug.LogError("VoidFall workshop purchase could not be saved: " + exception.Message);
                    SetMenuNotice("Purchase could not be saved. Parts were not spent.");
                    return;
                }
                _workshopPreviewId = WorkshopPreviewAfterPurchase();
                SetMenuNotice($"{WorkshopName(id)} upgraded to rank {entry.rank}. Applies next run.");
                return;
            }
        }

        public static string WorkshopPreviewAfterPurchase()
        {
            // React clears previewWorkshop after every successful purchase,
            // even when the purchased row is not the currently focused row.
            return null;
        }

        private string MenuPageName()
        {
            switch (_menuPage)
            {
                case MenuPage.Home: return "HOME";
                case MenuPage.Workshop: return "WORKSHOP";
                case MenuPage.Records: return "RECORDS";
                case MenuPage.Settings: return "SETTINGS";
                default: return "OVERVIEW";
            }
        }

        private static string WorkshopName(string id)
        {
            switch (id)
            {
                case "integrity": return "Integrity";
                case "power": return "Power";
                case "mobility": return "Mobility";
                case "recovery": return "Recovery";
                case "magnet": return "Magnet";
                case "precision": return "Precision";
                case "arsenal": return "Arsenal";
                case "protocol": return "Revival Protocol";
                default: return id;
            }
        }

        private static string WorkshopDescription(string id)
        {
            switch (id)
            {
                case "integrity": return "+5 maximum health per rank.";
                case "power": return "+4% weapon damage per rank.";
                case "mobility": return "+3% movement speed per rank.";
                case "recovery": return "Restore 3 health after each level per rank.";
                case "magnet": return "+8 pickup radius per rank.";
                case "precision": return "+2% critical chance per rank.";
                case "arsenal": return "Weapons recover 3% faster per rank.";
                case "protocol": return "+1 revive per run. Maximum one in this slice.";
                default: return "Permanent upgrade.";
            }
        }

        private static int WorkshopCost(string id, int rank)
        {
            switch (id)
            {
                case "integrity": return rank == 0 ? 35 : rank == 1 ? 75 : rank == 2 ? 130 : -1;
                case "power": return rank == 0 ? 45 : rank == 1 ? 95 : rank == 2 ? 165 : -1;
                case "mobility": return rank == 0 ? 40 : rank == 1 ? 85 : rank == 2 ? 145 : -1;
                case "recovery": return rank == 0 ? 30 : rank == 1 ? 70 : rank == 2 ? 120 : -1;
                case "magnet": return rank == 0 ? 25 : rank == 1 ? 60 : rank == 2 ? 105 : -1;
                case "precision": return rank == 0 ? 50 : rank == 1 ? 110 : rank == 2 ? 190 : -1;
                case "arsenal": return rank == 0 ? 90 : rank == 1 ? 150 : rank == 2 ? 195 : -1;
                case "protocol": return rank == 0 ? 120 : -1;
                default: return -1;
            }
        }

        private static string FormatRecordDate(long value)
        {
            try
            {
                if (value > 0 && value < 10_000_000_000_000L)
                    return DateTimeOffset.FromUnixTimeMilliseconds(value).ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                return new DateTime(Math.Max(0, value), DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            }
            catch
            {
                return "unknown date";
            }
        }

        private static Texture2D HomeBackdropTexture()
        {
            if (_homeBackdropTexture != null) return _homeBackdropTexture;

            const int size = 256;
            _homeBackdropTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "VoidFall Home Backdrop",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat,
            };
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x - size * 0.5f) / (size * 0.5f);
                    var dy = (y - size * 0.5f) / (size * 0.5f);
                    var centre = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 0.78f);
                    var color = Color.Lerp(
                        new Color(0.008f, 0.015f, 0.042f, 1f),
                        new Color(0.025f, 0.075f, 0.13f, 1f),
                        centre * 0.72f);
                    if (x % 64 == 0 || y % 64 == 0)
                        color = Color.Lerp(color, new Color(0.12f, 0.28f, 0.38f, 1f), 0.22f);
                    var starHash = (x * 1973 + y * 9277 + 89173) % 4096;
                    if (starHash < 2)
                        color = Color.Lerp(color, new Color(0.45f, 0.75f, 0.86f, 1f), 0.45f);
                    pixels[y * size + x] = color;
                }
            }
            _homeBackdropTexture.SetPixels(pixels);
            _homeBackdropTexture.Apply(false, true);
            return _homeBackdropTexture;
        }

        private GUIStyle HomeTitleStyle()
        {
            if (_homeTitleStyle == null)
            {
                _homeTitleStyle = new GUIStyle(GUI.skin.label)
                {
                    font = BrowserDisplayFont(),
                    fontSize = 104,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0),
                    normal = { textColor = new Color(0.78f, 0.95f, 1f, 1f) },
                };
            }
            return _homeTitleStyle;
        }

        private GUIStyle HomeStartStyle()
        {
            if (_homeStartStyle == null)
            {
                _homeStartStyle = new GUIStyle(GUI.skin.label)
                {
                    font = BrowserDisplayFont(),
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(0, 0, 0, 0),
                    normal = { textColor = new Color(0.86f, 0.96f, 1f, 1f) },
                };
            }
            return _homeStartStyle;
        }

        private GUIStyle HomeStartButtonStyle()
        {
            if (_homeStartButtonStyle == null)
            {
                _homeStartButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0),
                    border = new RectOffset(6, 6, 6, 6),
                };
                var normal = RoundedGradientGuiTexture(
                    new Color(0.133f, 0.827f, 0.925f, 0.18f),
                    new Color(0.133f, 0.827f, 0.925f, 0.06f),
                    new Color(0.404f, 0.91f, 0.953f, 0.56f),
                    330,
                    60,
                    6f,
                    "VoidFall Home Start");
                var hover = RoundedGradientGuiTexture(
                    new Color(0.133f, 0.827f, 0.925f, 0.30f),
                    new Color(0.133f, 0.827f, 0.925f, 0.10f),
                    new Color(0.647f, 0.953f, 0.988f, 0.82f),
                    330,
                    60,
                    6f,
                    "VoidFall Home Start Hover");
                var active = RoundedGradientGuiTexture(
                    new Color(0.133f, 0.827f, 0.925f, 0.24f),
                    new Color(0.133f, 0.827f, 0.925f, 0.08f),
                    new Color(0.647f, 0.953f, 0.988f, 0.9f),
                    330,
                    60,
                    6f,
                    "VoidFall Home Start Active");
                SetGuiStyleState(_homeStartButtonStyle.normal, normal, Color.white);
                SetGuiStyleState(_homeStartButtonStyle.hover, hover, Color.white);
                SetGuiStyleState(_homeStartButtonStyle.active, active, Color.white);
                SetGuiStyleState(_homeStartButtonStyle.focused, hover, Color.white);
            }
            return _homeStartButtonStyle;
        }

        private GUIStyle HomeStatusStyle()
        {
            if (_homeStatusStyle == null)
            {
                _homeStatusStyle = new GUIStyle(GUI.skin.box)
                {
                    // The nested metric labels already contribute their
                    // intrinsic vertical line spacing in IMGUI. Reduce the
                    // style padding so the whole group, including those
                    // children, remains the source 58px rather than growing
                    // to 71px and pushing the card grid down.
                    padding = new RectOffset(14, 14, 2, 3),
                    margin = new RectOffset(0, 0, 0, 0),
                    border = new RectOffset(9, 9, 9, 9),
                    fixedHeight = 58f,
                };
                var panel = RoundedGradientGuiTexture(
                    new Color(0.051f, 0.067f, 0.149f, 0.82f),
                    new Color(0.027f, 0.035f, 0.086f, 0.90f),
                    new Color(0.404f, 0.91f, 1f, 0.16f),
                    620,
                    58,
                    9f,
                    "VoidFall Home Status");
                SetGuiStyleState(_homeStatusStyle.normal, panel, Color.white);
                SetGuiStyleState(_homeStatusStyle.hover, panel, Color.white);
            }
            return _homeStatusStyle;
        }

        private GUIStyle HomeStatusStyleForHeight(float height)
        {
            if (height >= 58f) return HomeStatusStyle();
            if (_homeStatusCompactStyle == null)
            {
                _homeStatusCompactStyle = new GUIStyle(HomeStatusStyle())
                {
                    fixedHeight = height,
                };
            }
            return _homeStatusCompactStyle;
        }

        private GUIStyle HomeMetricLabelStyle()
        {
            if (_homeMetricLabelStyle == null)
            {
                _homeMetricLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    font = BrowserBodyFont(),
                    fontSize = 10,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(4, 0, 0, 0),
                    normal = { textColor = new Color(0.62f, 0.72f, 0.86f, 1f) },
                };
            }
            return _homeMetricLabelStyle;
        }

        private GUIStyle HomeMetricValueStyle()
        {
            if (_homeMetricValueStyle == null)
            {
                _homeMetricValueStyle = new GUIStyle(GUI.skin.label)
                {
                    font = BrowserDisplayFont(),
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(4, 0, 0, 0),
                    normal = { textColor = new Color(0.90f, 0.95f, 1f, 1f) },
                };
            }
            return _homeMetricValueStyle;
        }

        private GUIStyle HomeCardTitleStyle()
        {
            if (_homeCardTitleStyle == null)
            {
                _homeCardTitleStyle = new GUIStyle(GUI.skin.label)
                {
                    font = BrowserDisplayFont(),
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(0, 0, 0, 0),
                    normal = { textColor = new Color(0.88f, 0.95f, 0.99f, 1f) },
                };
            }
            return _homeCardTitleStyle;
        }

        private GUIStyle HomeCardDetailStyle()
        {
            if (_homeCardDetailStyle == null)
            {
                _homeCardDetailStyle = new GUIStyle(GUI.skin.label)
                {
                    font = BrowserBodyFont(),
                    fontSize = 11,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(0, 0, 0, 0),
                    normal = { textColor = new Color(0.53f, 0.64f, 0.77f, 1f) },
                };
            }
            return _homeCardDetailStyle;
        }

        private GUIStyle HomeCardButtonStyle()
        {
            if (_homeCardButtonStyle == null)
            {
                _homeCardButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    alignment = TextAnchor.MiddleCenter,
                    font = BrowserBodyFont(),
                    fontSize = 12,
                    padding = new RectOffset(11, 11, 11, 11),
                    margin = new RectOffset(0, 0, 0, 0),
                    border = new RectOffset(9, 9, 9, 9),
                };
                var normal = RoundedGradientGuiTexture(
                    new Color(0.051f, 0.067f, 0.149f, 0.78f),
                    new Color(0.027f, 0.035f, 0.086f, 0.88f),
                    new Color(0.404f, 0.91f, 1f, 0.15f),
                    64,
                    64,
                    9f,
                    "VoidFall Home Card");
                var hover = RoundedGradientGuiTexture(
                    new Color(0.067f, 0.106f, 0.208f, 0.90f),
                    new Color(0.031f, 0.051f, 0.114f, 0.94f),
                    new Color(0.404f, 0.91f, 1f, 0.54f),
                    64,
                    64,
                    9f,
                    "VoidFall Home Card Hover");
                var active = RoundedGradientGuiTexture(
                    new Color(0.10f, 0.18f, 0.28f, 0.98f),
                    new Color(0.04f, 0.09f, 0.17f, 1f),
                    new Color(0.65f, 0.96f, 1f, 0.82f),
                    64,
                    64,
                    9f,
                    "VoidFall Home Card Active");
                SetGuiStyleState(_homeCardButtonStyle.normal, normal, new Color(0.84f, 0.90f, 0.94f, 1f));
                SetGuiStyleState(_homeCardButtonStyle.hover, hover, Color.white);
                SetGuiStyleState(_homeCardButtonStyle.active, active, Color.white);
                SetGuiStyleState(_homeCardButtonStyle.focused, hover, Color.white);
                SetGuiStyleState(_homeCardButtonStyle.onNormal, active, Color.white);
                SetGuiStyleState(_homeCardButtonStyle.onHover, active, Color.white);
                SetGuiStyleState(_homeCardButtonStyle.onActive, active, Color.white);
                SetGuiStyleState(_homeCardButtonStyle.onFocused, active, Color.white);
            }
            return _homeCardButtonStyle;
        }

        private static Color OverlayCardGradientStartColor()
        {
            return new Color(13f / 255f, 17f / 255f, 38f / 255f, 0.90f);
        }

        private static Color OverlayCardGradientEndColor()
        {
            return new Color(7f / 255f, 9f / 255f, 22f / 255f, 0.94f);
        }

        private static float OverlayCardGradientAngleDegrees()
        {
            return 160f;
        }

        private static float OverlayCardBackdropBlurRadius()
        {
            return 14f;
        }

        private static int OverlayCardBackdropBlurSampleCount()
        {
            return 8;
        }

        private static float OverlayCardBackdropBlurSampleAlpha()
        {
            return 0.10f;
        }

        private static Texture2D OverlayCardShadowTexture(int cardWidth, int cardHeight)
        {
            cardWidth = Mathf.Max(16, Mathf.RoundToInt(cardWidth / 16f) * 16);
            cardHeight = Mathf.Max(16, Mathf.RoundToInt(cardHeight / 16f) * 16);
            var key = cardWidth + "x" + cardHeight;
            if (_overlayCardShadowCache.TryGetValue(key, out var cached)) return cached;

            var margin = OverlayCardShadowTextureMargin();
            var width = cardWidth + margin * 2;
            var height = cardHeight + margin * 2;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "VoidFall Overlay Card Shadow " + key,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color[width * height];
            var halfWidth = cardWidth * 0.5f;
            var halfHeight = cardHeight * 0.5f;
            var centerX = margin + halfWidth;
            var centerY = margin + halfHeight + OverlayCardShadowVerticalOffset();
            var blurRadius = OverlayCardShadowBlurRadius();
            var blurDenominator = 2f * blurRadius * blurRadius;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var distance = RoundedRectSignedDistance(
                        x - centerX,
                        y - centerY,
                        halfWidth,
                        halfHeight,
                        OverlayCardShadowCornerRadius());
                    var safeDistance = Mathf.Max(0f, distance);
                    var blurAlpha = OverlayCardShadowAlpha() * Mathf.Exp(
                        -(safeDistance * safeDistance) / blurDenominator);
                    var outlineAlpha = distance >= 0f && distance <= 1f
                        ? OverlayCardShadowOutlineAlpha() * (1f - distance)
                        : 0f;
                    pixels[y * width + x] = new Color(
                        0f,
                        0f,
                        0f,
                        Mathf.Max(blurAlpha, outlineAlpha));
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            CacheTextureBounded(_overlayCardShadowCache, key, texture);
            return texture;
        }

        private static float OverlayCardShadowBlurRadius()
        {
            return 80f;
        }

        private static float OverlayCardShadowVerticalOffset()
        {
            return 24f;
        }

        private static float OverlayCardShadowAlpha()
        {
            return 0.60f;
        }

        private static float OverlayCardShadowOutlineAlpha()
        {
            return 0.40f;
        }

        private static float OverlayCardShadowCornerRadius()
        {
            return 12f;
        }

        private static int OverlayCardShadowTextureMargin()
        {
            return Mathf.CeilToInt(
                OverlayCardShadowBlurRadius() + OverlayCardShadowVerticalOffset());
        }

        private GUIStyle ResultCardStyle()
        {
            if (_resultCardStyle == null)
            {
                _resultCardStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = new RectOffset(25, 25, 25, 25),
                    margin = new RectOffset(0, 0, 0, 0),
                    border = new RectOffset(
                        ResultCardBorderRadius(),
                        ResultCardBorderRadius(),
                        ResultCardBorderRadius(),
                        ResultCardBorderRadius()),
                };
                var background = RoundedGradientGuiTexture(
                    OverlayCardGradientStartColor(),
                    OverlayCardGradientEndColor(),
                    new Color(0.404f, 0.91f, 0.976f, 0.16f),
                    ResultCardTextureWidth(),
                    ResultCardTextureHeight(),
                    ResultCardBorderRadius(),
                    "VoidFall Result Card",
                    OverlayCardGradientAngleDegrees());
                SetGuiStyleState(_resultCardStyle.normal, background, Color.white);
                SetGuiStyleState(_resultCardStyle.hover, background, Color.white);
                SetGuiStyleState(_resultCardStyle.focused, background, Color.white);
            }
            return _resultCardStyle;
        }

        private GUIStyle ReviveTitleGlowStyle()
        {
            if (_reviveTitleGlowStyle == null)
            {
                EnsureReviveStyles();
                _reviveTitleGlowStyle = new GUIStyle(_reviveTitleStyle);
                _reviveTitleGlowStyle.normal.textColor = new Color(
                    34f / 255f,
                    211f / 255f,
                    238f / 255f,
                    1f);
            }
            return _reviveTitleGlowStyle;
        }

        private static float ReviveTitleShadowRadius(int ring)
        {
            return ring == 0 ? 12f : 42f;
        }

        private static float ReviveTitleShadowAlpha(int ring)
        {
            return ring == 0 ? 0.58f : 0.25f;
        }

        private static int ResultCardBorderRadius()
        {
            return 12;
        }

        private static int ResultCardTextureWidth()
        {
            return 390;
        }

        private static int ResultCardTextureHeight()
        {
            return 720;
        }

        private GUIStyle ResultDetailPanelStyle()
        {
            if (_resultDetailPanelStyle == null)
            {
                _resultDetailPanelStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = new RectOffset(12, 12, 12, 12),
                    margin = new RectOffset(0, 0, 4, 7),
                    border = new RectOffset(6, 6, 6, 6),
                };
                var background = RoundedGradientGuiTexture(
                    new Color(0.008f, 0.024f, 0.071f, 0.34f),
                    new Color(0.008f, 0.024f, 0.071f, 0.34f),
                    new Color(0.404f, 0.91f, 1f, 0.14f),
                    64,
                    64,
                    6f,
                    "VoidFall Result Detail Panel");
                SetGuiStyleState(_resultDetailPanelStyle.normal, background, Color.white);
                SetGuiStyleState(_resultDetailPanelStyle.hover, background, Color.white);
            }
            return _resultDetailPanelStyle;
        }

        private GUIStyle RecordMetricBoxStyle()
        {
            if (_recordMetricBoxStyle == null)
            {
                _recordMetricBoxStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = new RectOffset(10, 10, 10, 10),
                    margin = new RectOffset(0, 0, 0, 0),
                    border = new RectOffset(6, 6, 6, 6),
                };
                var background = RoundedGradientGuiTexture(
                    new Color(0.031f, 0.051f, 0.082f, 0.56f),
                    new Color(0.031f, 0.051f, 0.082f, 0.56f),
                    new Color(0.58f, 0.64f, 0.72f, 0.14f),
                    64,
                    66,
                    6f,
                    "VoidFall Record Metric");
                SetGuiStyleState(_recordMetricBoxStyle.normal, background, Color.white);
                SetGuiStyleState(_recordMetricBoxStyle.hover, background, Color.white);
            }
            return _recordMetricBoxStyle;
        }

        private GUIStyle RecordMetricLabelStyle()
        {
            if (_recordMetricLabelStyle == null)
            {
                _recordMetricLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    font = BrowserBodyFont(),
                    fontSize = BrowserMetricLabelFontSize(),
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(0, 0, 0, 0),
                    normal = { textColor = new Color(0.47f, 0.53f, 0.60f, 1f) },
                };
            }
            return _recordMetricLabelStyle;
        }

        private GUIStyle RecordMetricValueStyle()
        {
            if (_recordMetricValueStyle == null)
            {
                _recordMetricValueStyle = new GUIStyle(GUI.skin.label)
                {
                    font = BrowserDisplayFont(),
                    fontSize = BrowserMetricValueFontSize(),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(0, 0, 0, 0),
                    normal = { textColor = new Color(0.90f, 0.93f, 0.96f, 1f) },
                };
            }
            return _recordMetricValueStyle;
        }

        private GUIStyle RecordTableWrapStyle()
        {
            if (_recordTableWrapStyle == null)
            {
                _recordTableWrapStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0),
                    border = new RectOffset(7, 7, 7, 7),
                };
                var background = RoundedGradientGuiTexture(
                    new Color(0.031f, 0.051f, 0.082f, 0.72f),
                    new Color(0.020f, 0.032f, 0.055f, 0.80f),
                    new Color(0.58f, 0.64f, 0.69f, 0.14f),
                    64,
                    32,
                    7f,
                    "VoidFall Score Table");
                SetGuiStyleState(_recordTableWrapStyle.normal, background, Color.white);
                SetGuiStyleState(_recordTableWrapStyle.hover, background, Color.white);
            }
            return _recordTableWrapStyle;
        }

        private GUIStyle RecordTableHeaderStyle()
        {
            if (_recordTableHeaderStyle == null)
            {
                _recordTableHeaderStyle = new GUIStyle
                {
                    fixedHeight = 32f,
                    padding = new RectOffset(10, 10, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0),
                };
                var background = RoundedGradientGuiTexture(
                    new Color(0.059f, 0.090f, 0.137f, 0.72f),
                    new Color(0.059f, 0.090f, 0.137f, 0.72f),
                    Color.clear,
                    64,
                    32,
                    0f,
                    "VoidFall Score Table Header");
                SetGuiStyleState(_recordTableHeaderStyle.normal, background, Color.white);
                SetGuiStyleState(_recordTableHeaderStyle.hover, background, Color.white);
            }
            return _recordTableHeaderStyle;
        }

        private GUIStyle RecordTableHeaderTextStyle()
        {
            if (_recordTableHeaderTextStyle == null)
            {
                _recordTableHeaderTextStyle = new GUIStyle(GUI.skin.label)
                {
                    font = BrowserBodyFont(),
                    fontSize = 9,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(0, 0, 0, 0),
                    normal = { textColor = new Color(0.47f, 0.53f, 0.60f, 1f) },
                };
            }
            return _recordTableHeaderTextStyle;
        }

        private GUIStyle RecordTableRowStyle()
        {
            if (_recordTableRowStyle == null)
            {
                _recordTableRowStyle = new GUIStyle
                {
                    fixedHeight = 34f,
                    padding = new RectOffset(10, 10, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0),
                };
            }
            return _recordTableRowStyle;
        }

        private GUIStyle RecordTableCellStyle()
        {
            if (_recordTableCellStyle == null)
            {
                _recordTableCellStyle = new GUIStyle(GUI.skin.label)
                {
                    font = BrowserBodyFont(),
                    fontSize = 11,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(0, 0, 0, 0),
                    normal = { textColor = new Color(0.86f, 0.90f, 0.93f, 1f) },
                };
            }
            return _recordTableCellStyle;
        }

        private GUIStyle RecordTableScoreStyle()
        {
            if (_recordTableScoreStyle == null)
            {
                _recordTableScoreStyle = new GUIStyle(RecordTableCellStyle())
                {
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.73f, 0.90f, 0.99f, 1f) },
                };
            }
            return _recordTableScoreStyle;
        }

        private GUIStyle SettingsRowStyle()
        {
            if (_settingsRowStyle == null)
            {
                _settingsRowStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = new RectOffset(11, 11, 9, 9),
                    margin = new RectOffset(0, 0, 0, 0),
                    border = new RectOffset(7, 7, 7, 7),
                };
                var background = RoundedGradientGuiTexture(
                    new Color(0.031f, 0.051f, 0.082f, 0.54f),
                    new Color(0.031f, 0.051f, 0.082f, 0.54f),
                    new Color(0.58f, 0.64f, 0.69f, 0.13f),
                    64,
                    64,
                    7f,
                    "VoidFall Settings Row");
                var hover = RoundedGradientGuiTexture(
                    new Color(0.039f, 0.075f, 0.114f, 0.70f),
                    new Color(0.039f, 0.075f, 0.114f, 0.70f),
                    new Color(0.49f, 0.83f, 0.99f, 0.24f),
                    64,
                    64,
                    7f,
                    "VoidFall Settings Row Hover");
                SetGuiStyleState(_settingsRowStyle.normal, background, Color.white);
                SetGuiStyleState(_settingsRowStyle.hover, hover, Color.white);
                SetGuiStyleState(_settingsRowStyle.focused, hover, Color.white);
            }
            return _settingsRowStyle;
        }

        private GUIStyle SettingsLabelStyle()
        {
            if (_settingsLabelStyle == null)
            {
                _settingsLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    font = BrowserDisplayFont(),
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(0, 0, 0, 0),
                    normal = { textColor = new Color(0.86f, 0.90f, 0.93f, 1f) },
                };
            }
            return _settingsLabelStyle;
        }

        private GUIStyle SettingsDetailStyle()
        {
            if (_settingsDetailStyle == null)
            {
                _settingsDetailStyle = new GUIStyle(GUI.skin.label)
                {
                    font = BrowserBodyFont(),
                    fontSize = 10,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(0, 0, 0, 0),
                    normal = { textColor = new Color(0.45f, 0.52f, 0.59f, 1f) },
                };
            }
            return _settingsDetailStyle;
        }

        private GUIStyle SettingsSelectStyle()
        {
            if (_settingsSelectStyle == null)
            {
                _settingsSelectStyle = new GUIStyle(GUI.skin.button)
                {
                    font = BrowserDisplayFont(),
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(10, 10, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0),
                    border = new RectOffset(6, 6, 6, 6),
                };
                var normal = RoundedGradientGuiTexture(
                    new Color(11f / 255f, 20f / 255f, 32f / 255f, 1f),
                    new Color(11f / 255f, 20f / 255f, 32f / 255f, 1f),
                    new Color(125f / 255f, 211f / 255f, 252f / 255f, 0.24f),
                    64,
                    36,
                    6f,
                    "VoidFall Settings Select");
                var hover = RoundedGradientGuiTexture(
                    new Color(15f / 255f, 30f / 255f, 45f / 255f, 1f),
                    new Color(15f / 255f, 30f / 255f, 45f / 255f, 1f),
                    new Color(125f / 255f, 211f / 255f, 252f / 255f, 0.38f),
                    64,
                    36,
                    6f,
                    "VoidFall Settings Select Hover");
                SetGuiStyleState(_settingsSelectStyle.normal, normal, Color.white);
                SetGuiStyleState(_settingsSelectStyle.hover, hover, Color.white);
                SetGuiStyleState(_settingsSelectStyle.active, hover, Color.white);
                SetGuiStyleState(_settingsSelectStyle.focused, hover, Color.white);
            }
            return _settingsSelectStyle;
        }

        private GUIStyle SettingsSelectValueStyle()
        {
            if (_settingsSelectValueStyle == null)
            {
                _settingsSelectValueStyle = new GUIStyle(GUI.skin.label)
                {
                    font = BrowserDisplayFont(),
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(0, 0, 0, 0),
                    normal = { textColor = new Color(0.86f, 0.92f, 0.995f, 1f) },
                };
            }
            return _settingsSelectValueStyle;
        }

        private GUIStyle SettingsSelectArrowStyle()
        {
            if (_settingsSelectArrowStyle == null)
            {
                _settingsSelectArrowStyle = new GUIStyle(GUI.skin.label)
                {
                    font = BrowserDisplayFont(),
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(0, 0, 0, 0),
                    normal = { textColor = new Color(0.53f, 0.83f, 0.96f, 1f) },
                };
            }
            return _settingsSelectArrowStyle;
        }

        private GUIStyle SettingsSelectPopupStyle()
        {
            if (_settingsSelectPopupStyle == null)
            {
                _settingsSelectPopupStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = new RectOffset(4, 4, 4, 4),
                    margin = new RectOffset(0, 0, 2, 0),
                    border = new RectOffset(6, 6, 6, 6),
                };
                var background = RoundedGradientGuiTexture(
                    new Color(11f / 255f, 20f / 255f, 32f / 255f, 1f),
                    new Color(11f / 255f, 20f / 255f, 32f / 255f, 1f),
                    new Color(125f / 255f, 211f / 255f, 252f / 255f, 0.24f),
                    64,
                    64,
                    6f,
                    "VoidFall Settings Select Popup");
                SetGuiStyleState(_settingsSelectPopupStyle.normal, background, Color.white);
            }
            return _settingsSelectPopupStyle;
        }

        private GUIStyle SettingsSelectOptionStyle()
        {
            if (_settingsSelectOptionStyle == null)
            {
                _settingsSelectOptionStyle = new GUIStyle(GUI.skin.button)
                {
                    font = BrowserDisplayFont(),
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(10, 10, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0),
                    border = new RectOffset(4, 4, 4, 4),
                };
                var normal = RoundedGradientGuiTexture(
                    new Color(11f / 255f, 20f / 255f, 32f / 255f, 0.2f),
                    new Color(11f / 255f, 20f / 255f, 32f / 255f, 0.2f),
                    Color.clear,
                    64,
                    30,
                    4f,
                    "VoidFall Settings Select Option");
                var hover = RoundedGradientGuiTexture(
                    new Color(15f / 255f, 30f / 255f, 45f / 255f, 1f),
                    new Color(15f / 255f, 30f / 255f, 45f / 255f, 1f),
                    new Color(125f / 255f, 211f / 255f, 252f / 255f, 0.24f),
                    64,
                    30,
                    4f,
                    "VoidFall Settings Select Option Hover");
                SetGuiStyleState(_settingsSelectOptionStyle.normal, normal, new Color(0.86f, 0.92f, 0.995f, 1f));
                SetGuiStyleState(_settingsSelectOptionStyle.hover, hover, new Color(0.86f, 0.92f, 0.995f, 1f));
                SetGuiStyleState(_settingsSelectOptionStyle.active, hover, Color.white);
            }
            return _settingsSelectOptionStyle;
        }

        private GUIStyle SettingsValueStyle()
        {
            if (_settingsValueStyle == null)
            {
                _settingsValueStyle = new GUIStyle(GUI.skin.label)
                {
                    font = BrowserBodyFont(),
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(0, 0, 0, 0),
                    normal = { textColor = new Color(0.65f, 0.95f, 0.99f, 1f) },
                };
            }
            return _settingsValueStyle;
        }

        private GUIStyle ProfilePageHeaderStyle()
        {
            if (_profilePageHeaderStyle == null)
            {
                _profilePageHeaderStyle = new GUIStyle
                {
                    padding = new RectOffset(0, 0, 0, 15),
                    margin = new RectOffset(0, 0, 0, 0),
                };
            }
            return _profilePageHeaderStyle;
        }

        private GUIStyle ProfilePageKickerStyle()
        {
            if (_profilePageKickerStyle == null)
            {
                _profilePageKickerStyle = new GUIStyle(GUI.skin.label)
                {
                    font = BrowserDisplayFont(),
                    fontSize = 10,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    wordWrap = false,
                    padding = new RectOffset(0, 0, 0, 0),
                    normal = { textColor = new Color(0.49f, 0.83f, 0.99f, 1f) },
                };
            }
            return _profilePageKickerStyle;
        }

        private GUIStyle ProfilePageTitleStyle()
        {
            if (_profilePageTitleStyle == null)
            {
                _profilePageTitleStyle = new GUIStyle(GUI.skin.label)
                {
                    font = BrowserDisplayFont(),
                    fontSize = 27,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    wordWrap = false,
                    padding = new RectOffset(0, 0, 0, 0),
                    normal = { textColor = new Color(0.945f, 0.961f, 0.976f, 1f) },
                };
            }
            return _profilePageTitleStyle;
        }

        private GUIStyle ProfilePartsBalanceStyle()
        {
            if (_profilePartsBalanceStyle == null)
            {
                _profilePartsBalanceStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = new RectOffset(7, 9, 7, 7),
                    margin = new RectOffset(0, 0, 0, 0),
                    border = new RectOffset(6, 6, 6, 6),
                };
                var background = RoundedGradientGuiTexture(
                    new Color(0.443f, 0.247f, 0.071f, 0.14f),
                    new Color(0.443f, 0.247f, 0.071f, 0.14f),
                    new Color(0.98f, 0.80f, 0.08f, 0.22f),
                    64,
                    32,
                    6f,
                    "VoidFall Profile Parts Balance");
                SetGuiStyleState(_profilePartsBalanceStyle.normal, background, Color.white);
                SetGuiStyleState(_profilePartsBalanceStyle.hover, background, Color.white);
            }
            return _profilePartsBalanceStyle;
        }

        private GUIStyle ProfilePartsBalanceTextStyle()
        {
            if (_profilePartsBalanceTextStyle == null)
            {
                _profilePartsBalanceTextStyle = new GUIStyle(GUI.skin.label)
                {
                    font = BrowserDisplayFont(),
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = false,
                    padding = new RectOffset(0, 0, 0, 0),
                    normal = { textColor = new Color(0.99f, 0.90f, 0.54f, 1f) },
                };
            }
            return _profilePartsBalanceTextStyle;
        }

        private GUIStyle MenuTitleStyle()
        {
            if (_menuTitleStyle == null)
            {
                _menuTitleStyle = new GUIStyle(GUI.skin.label)
                {
                    font = BrowserDisplayFont(),
                    fontSize = 24,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.35f, 0.9f, 1f) },
                };
            }
            return _menuTitleStyle;
        }

        private GUIStyle MenuSectionStyle()
        {
            if (_menuSectionStyle == null)
            {
                _menuSectionStyle = new GUIStyle(GUI.skin.label)
                {
                    font = BrowserDisplayFont(),
                    fontSize = 17,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.98f, 0.7f, 0.32f) },
                };
            }
            return _menuSectionStyle;
        }

        private GUIStyle MenuBodyStyle()
        {
            if (_menuBodyStyle == null)
            {
                _menuBodyStyle = new GUIStyle(GUI.skin.label)
                {
                    font = BrowserBodyFont(),
                    fontSize = 14,
                    wordWrap = true,
                    normal = { textColor = new Color(0.8f, 0.86f, 0.95f) },
                };
            }
            return _menuBodyStyle;
        }

        private GUIStyle MenuValueStyle()
        {
            if (_menuValueStyle == null)
            {
                _menuValueStyle = new GUIStyle(MenuBodyStyle())
                {
                    alignment = TextAnchor.MiddleRight,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.97f, 0.98f, 0.99f, 1f) },
                };
            }
            return _menuValueStyle;
        }

        private void SetupHud()
        {
            var canvasObject = new GameObject("VoidFall HUD");
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
            _hudGroup = canvasObject.AddComponent<CanvasGroup>();
            _hudGroup.alpha = 0f;
            _hudGroup.interactable = false;
            _hudGroup.blocksRaycasts = false;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            // The HUD is authored in the browser's CSS pixels against a 1600x900
            // desktop capture. ConstantPixelSize reproduced that exactly at that
            // one resolution and nowhere else: because the layout is in raw
            // pixels, the whole HUD stayed a fixed physical size, so it read far
            // too small at 1440p and 4K.
            //
            // Scale against the authored reference instead, matching on height.
            // The gameplay camera is orthographic with a fixed vertical extent
            // (WorldHalfHeight), so height is the axis the world itself scales
            // on; matching it keeps the HUD locked to the view rather than to
            // the pixel grid. The explicit narrow breakpoint in
            // UpdateHudResponsiveLayout still owns phone-shaped viewports.
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            canvasObject.AddComponent<GraphicRaycaster>();
            var perimeterObject = new GameObject("Music Reactive Perimeter");
            perimeterObject.transform.SetParent(canvasObject.transform, false);
            _musicPerimeter = perimeterObject.AddComponent<MusicPerimeterGraphic>();
            var perimeterRect = _musicPerimeter.rectTransform;
            perimeterRect.anchorMin = Vector2.zero;
            perimeterRect.anchorMax = Vector2.one;
            perimeterRect.offsetMin = Vector2.zero;
            perimeterRect.offsetMax = Vector2.zero;
            _musicPerimeter.Configure(
                unchecked((int)_runSeed),
                _qualityPreset.Detail,
                _saveData?.settings != null && _saveData.settings.reducedMotion);
            _musicPerimeter.transform.SetAsFirstSibling();
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                var eventSystemObject = new GameObject("VoidFall UI EventSystem");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
            }
            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();

            _xpBarBackground = CreateHudImage(canvasObject.transform, "XP Bar Background");
            _xpBarBackground.sprite = ProceduralSpriteFactory.Square();
            _xpBarBackground.color = new Color(0.008f, 0.024f, 0.047f, 0.78f);
            _xpBarBackground.type = Image.Type.Filled;
            _xpBarBackground.fillMethod = Image.FillMethod.Horizontal;
            _xpBarBackground.fillOrigin = 0;
            _xpBarBackground.fillAmount = 1f;
            _xpBarBackground.rectTransform.anchorMin = new Vector2(0, 1);
            _xpBarBackground.rectTransform.anchorMax = new Vector2(1, 1);
            _xpBarBackground.rectTransform.pivot = new Vector2(0.5f, 1);
            _xpBarBackground.rectTransform.offsetMin = new Vector2(0, -6);
            _xpBarBackground.rectTransform.offsetMax = Vector2.zero;
            _xpBarBackground.enabled = true;
            _xpBarFill = CreateHudImage(canvasObject.transform, "XP Bar Fill");
            _xpBarFill.sprite = ProceduralSpriteFactory.Square();
            _xpBarFill.color = new Color(0.204f, 0.827f, 0.6f, 1f);
            _xpBarFill.type = Image.Type.Filled;
            _xpBarFill.fillMethod = Image.FillMethod.Horizontal;
            _xpBarFill.fillOrigin = 0;
            _xpBarFill.rectTransform.anchorMin = new Vector2(0, 1);
            _xpBarFill.rectTransform.anchorMax = new Vector2(1, 1);
            _xpBarFill.rectTransform.pivot = new Vector2(0.5f, 1);
            _xpBarFill.rectTransform.offsetMin = new Vector2(0, -6);
            _xpBarFill.rectTransform.offsetMax = Vector2.zero;
            _xpBarFill.enabled = true;

            _healthPanel = CreateHudImage(canvasObject.transform, "Integrity Panel");
            _healthPanel.sprite = ProceduralSpriteFactory.Square();
            _healthPanel.color = new Color(0.02f, 0.035f, 0.063f, 0.7f);
            _healthPanel.rectTransform.anchorMin = new Vector2(0, 1);
            _healthPanel.rectTransform.anchorMax = new Vector2(0, 1);
            _healthPanel.rectTransform.pivot = new Vector2(0, 1);
            _healthPanel.rectTransform.anchoredPosition = new Vector2(12, -13);
            _healthPanel.rectTransform.sizeDelta = new Vector2(240, 43);
            _healthPanel.enabled = true;
            _healthBarBackground = CreateHudImage(canvasObject.transform, "Integrity Bar Background");
            _healthBarBackground.sprite = ProceduralSpriteFactory.Square();
            _healthBarBackground.color = new Color(0.106f, 0.141f, 0.188f, 1f);
            ConfigureTopLeftBar(_healthBarBackground, new Vector2(22, -37));
            _healthBarBackground.enabled = true;
            _healthBarGhost = CreateHudImage(canvasObject.transform, "Integrity Bar Ghost");
            _healthBarGhost.sprite = ProceduralSpriteFactory.Square();
            _healthBarGhost.color = new Color(0.996f, 0.804f, 0.827f, 0.38f);
            ConfigureTopLeftBar(_healthBarGhost, new Vector2(22, -37));
            _healthBarGhost.enabled = true;
            _healthBarFill = CreateHudImage(canvasObject.transform, "Integrity Bar Fill");
            _healthBarFill.sprite = ProceduralSpriteFactory.Square();
            _healthBarFill.color = new Color(0.91f, 0.337f, 0.439f, 1f);
            ConfigureTopLeftBar(_healthBarFill, new Vector2(22, -37));
            _healthBarFill.enabled = true;
            _healthText = CreateText(canvasObject.transform, new Vector2(22, -23), new Vector2(0, 1), 11, new Color(0.72f, 0.78f, 0.84f));
            _healthText.rectTransform.sizeDelta = new Vector2(220, 18);
            _healthText.enabled = false;
            var healthIconObject = new GameObject("Integrity Icon");
            healthIconObject.transform.SetParent(canvasObject.transform, false);
            _healthIcon = healthIconObject.AddComponent<RawImage>();
            _healthIcon.texture = ControlIconTexture();
            _healthIcon.uvRect = ControlIconUv("heart");
            _healthIcon.color = new Color(0.984f, 0.443f, 0.529f, 1f);
            _healthIcon.raycastTarget = false;
            var healthIconRect = _healthIcon.rectTransform;
            healthIconRect.anchorMin = new Vector2(0, 1);
            healthIconRect.anchorMax = new Vector2(0, 1);
            healthIconRect.pivot = new Vector2(0.5f, 0.5f);
            healthIconRect.anchoredPosition = new Vector2(28, -27);
            healthIconRect.sizeDelta = new Vector2(12, 12);
            _healthLabelText = CreateText(
                canvasObject.transform,
                new Vector2(43, -27),
                new Vector2(0, 1),
                10,
                new Color(0.72f, 0.78f, 0.84f, 1f));
            _healthLabelText.rectTransform.pivot = new Vector2(0, 0.5f);
            _healthLabelText.rectTransform.anchoredPosition = new Vector2(43, -27);
            _healthLabelText.rectTransform.sizeDelta = new Vector2(120, 16);
            _healthLabelText.alignment = TextAnchor.MiddleLeft;
            _healthValueText = CreateText(
                canvasObject.transform,
                new Vector2(242, -27),
                new Vector2(0, 1),
                10,
                new Color(0.72f, 0.78f, 0.84f, 1f));
            _healthValueText.rectTransform.pivot = new Vector2(1, 0.5f);
            _healthValueText.rectTransform.anchoredPosition = new Vector2(242, -27);
            _healthValueText.rectTransform.sizeDelta = new Vector2(80, 16);
            _healthValueText.alignment = TextAnchor.MiddleRight;

            _clockPanel = CreateHudImage(canvasObject.transform, "Run Clock Panel");
            _clockPanel.sprite = ProceduralSpriteFactory.Square();
            _clockPanel.color = new Color(0.02f, 0.035f, 0.063f, 0.72f);
            _clockPanel.rectTransform.anchorMin = new Vector2(0.5f, 1);
            _clockPanel.rectTransform.anchorMax = new Vector2(0.5f, 1);
            _clockPanel.rectTransform.pivot = new Vector2(0.5f, 1);
            _clockPanel.rectTransform.anchoredPosition = new Vector2(-10, -13);
            _clockPanel.rectTransform.sizeDelta = new Vector2(94, 52);
            _clockPanel.enabled = true;
            _timeText = CreateText(canvasObject.transform, new Vector2(-10, -19), new Vector2(0.5f, 1), 23, new Color(0.945f, 0.961f, 0.976f));
            _timeText.alignment = TextAnchor.UpperCenter;
            _timeText.rectTransform.sizeDelta = new Vector2(94, 30);
            _levelText = CreateText(canvasObject.transform, new Vector2(-10, -49), new Vector2(0.5f, 1), 10, new Color(0.49f, 0.827f, 0.988f));
            _levelText.alignment = TextAnchor.UpperCenter;
            _levelText.rectTransform.sizeDelta = new Vector2(94, 16);

            _metricsPanel = CreateHudImage(canvasObject.transform, "Run Metrics Panel");
            _metricsPanel.sprite = ProceduralSpriteFactory.Square();
            _metricsPanel.color = new Color(0.02f, 0.035f, 0.063f, 0.72f);
            _metricsPanel.rectTransform.anchorMin = new Vector2(1, 1);
            _metricsPanel.rectTransform.anchorMax = new Vector2(1, 1);
            _metricsPanel.rectTransform.pivot = new Vector2(1, 1);
            _metricsPanel.rectTransform.anchoredPosition = new Vector2(-64, -13);
            _metricsPanel.rectTransform.sizeDelta = new Vector2(186, 44);
            _metricsPanel.enabled = true;
            _metricsText = CreateText(canvasObject.transform, new Vector2(-64, -27), new Vector2(1, 1), 11, new Color(0.9f, 0.93f, 0.96f));
            _metricsText.alignment = TextAnchor.MiddleRight;
            _metricsText.rectTransform.sizeDelta = new Vector2(190, 32);
            _metricsText.enabled = false;
            var metricIds = new[] { "skull", "coins", "trophy" };
            for (var metricIndex = 0; metricIndex < _metricIcons.Length; metricIndex++)
            {
                // RectTransform positions grow leftward from the right edge,
                // while the browser DOM reads left-to-right as skull, coins,
                // trophy. Reverse only the visual slot; keep metricIndex
                // aligned with the live value arrays in UpdateHud().
                var visualIndex = _metricIcons.Length - 1 - metricIndex;
                var rightEdge = -64f - visualIndex * 62f;
                var centreX = rightEdge - 31f;
                var iconObject = new GameObject("Metric Icon " + metricIndex);
                iconObject.transform.SetParent(canvasObject.transform, false);
                var icon = iconObject.AddComponent<RawImage>();
                icon.texture = HomeIconTexture();
                icon.uvRect = HomeIconUv(metricIds[metricIndex]);
                icon.color = new Color(0.58f, 0.647f, 0.72f, 1f);
                icon.raycastTarget = false;
                var iconRect = icon.rectTransform;
                iconRect.anchorMin = new Vector2(1, 1);
                iconRect.anchorMax = new Vector2(1, 1);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = new Vector2(centreX - 12f, -35f);
                iconRect.sizeDelta = new Vector2(12, 12);
                _metricIcons[metricIndex] = icon;

                var value = CreateText(
                    canvasObject.transform,
                    new Vector2(centreX + 1f, -35f),
                    new Vector2(1, 1),
                    12,
                    new Color(0.898f, 0.929f, 0.957f, 1f));
                value.rectTransform.pivot = new Vector2(0, 0.5f);
                value.rectTransform.anchoredPosition = new Vector2(centreX + 1f, -35f);
                value.rectTransform.sizeDelta = new Vector2(32, 20);
                value.alignment = TextAnchor.MiddleLeft;
                _metricValues[metricIndex] = value;

                if (visualIndex > 0 && metricIndex < _metricDividers.Length)
                {
                    var dividerObject = new GameObject("Metric Divider " + metricIndex);
                    dividerObject.transform.SetParent(canvasObject.transform, false);
                    var divider = dividerObject.AddComponent<Image>();
                    divider.sprite = ProceduralSpriteFactory.Square();
                    divider.color = new Color(0.58f, 0.647f, 0.72f, 0.14f);
                    divider.raycastTarget = false;
                    var dividerRect = divider.rectTransform;
                    dividerRect.anchorMin = new Vector2(1, 1);
                    dividerRect.anchorMax = new Vector2(1, 1);
                    dividerRect.pivot = new Vector2(0.5f, 0.5f);
                    dividerRect.anchoredPosition = new Vector2(rightEdge, -35f);
                    dividerRect.sizeDelta = new Vector2(1, 24);
                    _metricDividers[metricIndex] = divider;
                }
            }

            var pauseButtonObject = new GameObject("Pause Button");
            pauseButtonObject.transform.SetParent(canvasObject.transform, false);
            var pauseImage = pauseButtonObject.AddComponent<Image>();
            pauseImage.sprite = ProceduralSpriteFactory.Square();
            pauseImage.color = new Color(0.027f, 0.043f, 0.075f, 0.9f);
            _pauseButton = pauseButtonObject.AddComponent<Button>();
            _pauseButton.targetGraphic = pauseImage;
            _pauseButton.onClick.AddListener(TogglePauseFromHud);
            var pauseColors = _pauseButton.colors;
            pauseColors.normalColor = new Color(0.027f, 0.043f, 0.075f, 0.9f);
            pauseColors.highlightedColor = new Color(0.08f, 0.18f, 0.24f, 0.96f);
            pauseColors.pressedColor = new Color(0.12f, 0.3f, 0.36f, 1f);
            pauseColors.disabledColor = new Color(0.027f, 0.043f, 0.075f, 0.35f);
            pauseColors.colorMultiplier = 1f;
            _pauseButton.colors = pauseColors;
            var pauseRect = pauseButtonObject.GetComponent<RectTransform>();
            pauseRect.anchorMin = new Vector2(1, 1);
            pauseRect.anchorMax = new Vector2(1, 1);
            pauseRect.pivot = new Vector2(1, 1);
            pauseRect.anchoredPosition = new Vector2(-12, -13);
            pauseRect.sizeDelta = new Vector2(44, 44);
            _pauseButtonText = CreateText(pauseButtonObject.transform, Vector2.zero, new Vector2(0.5f, 0.5f), 14, new Color(0.8f, 0.94f, 0.98f));
            _pauseButtonText.text = ControlIconTexture() == null ? "||" : string.Empty;
            _pauseButtonText.alignment = TextAnchor.MiddleCenter;
            _pauseButtonText.raycastTarget = false;
            _pauseButtonText.rectTransform.sizeDelta = new Vector2(44, 44);
            var pauseIconObject = new GameObject("Pause Button Icon");
            pauseIconObject.transform.SetParent(pauseButtonObject.transform, false);
            _pauseButtonIcon = pauseIconObject.AddComponent<RawImage>();
            _pauseButtonIcon.texture = ControlIconTexture();
            _pauseButtonIcon.uvRect = ControlIconUv("pause");
            _pauseButtonIcon.color = new Color(0.8f, 0.94f, 0.98f, 1f);
            _pauseButtonIcon.raycastTarget = false;
            var pauseIconRect = _pauseButtonIcon.rectTransform;
            pauseIconRect.anchorMin = new Vector2(0.5f, 0.5f);
            pauseIconRect.anchorMax = new Vector2(0.5f, 0.5f);
            pauseIconRect.pivot = new Vector2(0.5f, 0.5f);
            pauseIconRect.anchoredPosition = Vector2.zero;
            pauseIconRect.sizeDelta = new Vector2(22, 22);

            _boostPanel = CreateHudImage(canvasObject.transform, "Overclock Panel");
            _boostPanel.sprite = ProceduralSpriteFactory.Square();
            _boostPanel.color = new Color(0.031f, 0.047f, 0.071f, 0.78f);
            _boostPanel.rectTransform.anchorMin = new Vector2(0.5f, 0);
            _boostPanel.rectTransform.anchorMax = new Vector2(0.5f, 0);
            _boostPanel.rectTransform.pivot = new Vector2(0.5f, 0);
            _boostPanel.rectTransform.anchoredPosition = new Vector2(0, 13);
            _boostPanel.rectTransform.sizeDelta = new Vector2(226, 44);
            _boostPanel.enabled = false;
            var boostIconObject = new GameObject("Overclock Icon");
            boostIconObject.transform.SetParent(canvasObject.transform, false);
            _boostIcon = boostIconObject.AddComponent<RawImage>();
            _boostIcon.texture = BuildChipIconTexture();
            _boostIcon.uvRect = BuildChipIconUv("railgun");
            _boostIcon.color = new Color(0.98f, 0.8f, 0.08f, 1f);
            _boostIcon.raycastTarget = false;
            var boostIconRect = _boostIcon.rectTransform;
            boostIconRect.anchorMin = new Vector2(0.5f, 0);
            boostIconRect.anchorMax = new Vector2(0.5f, 0);
            boostIconRect.pivot = new Vector2(0.5f, 0.5f);
            boostIconRect.anchoredPosition = new Vector2(-101, 43);
            boostIconRect.sizeDelta = new Vector2(13, 13);
            _boostIcon.enabled = false;
            _boostText = CreateText(canvasObject.transform, new Vector2(-24, 34), new Vector2(0.5f, 0), 11, new Color(0.42f, 0.94f, 1f));
            _boostText.alignment = TextAnchor.MiddleLeft;
            _boostText.fontStyle = FontStyle.Bold;
            _boostText.rectTransform.sizeDelta = new Vector2(158, 20);
            _boostText.enabled = false;
            _boostGhostA = CreateText(canvasObject.transform, new Vector2(-24, 34), new Vector2(0.5f, 0), 11, new Color(0.1f, 0.9f, 1f, 0f));
            _boostGhostA.alignment = TextAnchor.MiddleLeft;
            _boostGhostA.fontStyle = FontStyle.Bold;
            _boostGhostA.rectTransform.sizeDelta = new Vector2(158, 20);
            _boostGhostA.raycastTarget = false;
            _boostGhostA.enabled = false;
            _boostGhostB = CreateText(canvasObject.transform, new Vector2(-24, 34), new Vector2(0.5f, 0), 11, new Color(1f, 0.08f, 0.7f, 0f));
            _boostGhostB.alignment = TextAnchor.MiddleLeft;
            _boostGhostB.fontStyle = FontStyle.Bold;
            _boostGhostB.rectTransform.sizeDelta = new Vector2(158, 20);
            _boostGhostB.raycastTarget = false;
            _boostGhostB.enabled = false;
            _boostSecondsText = CreateText(canvasObject.transform, new Vector2(91, 34), new Vector2(0.5f, 0), 10, new Color(0.82f, 0.94f, 1f));
            _boostSecondsText.alignment = TextAnchor.MiddleRight;
            _boostSecondsText.rectTransform.sizeDelta = new Vector2(30, 20);
            _boostSecondsText.enabled = false;
            _boostBar = CreateHudImage(canvasObject.transform, "Overclock Bar");
            _boostBar.sprite = ProceduralSpriteFactory.Square();
            _boostBar.color = new Color(0.98f, 0.8f, 0.08f, 0.92f);
            _boostBar.type = Image.Type.Filled;
            _boostBar.fillMethod = Image.FillMethod.Horizontal;
            _boostBar.fillOrigin = 0;
            _boostBar.rectTransform.anchorMin = new Vector2(0.5f, 0);
            _boostBar.rectTransform.anchorMax = new Vector2(0.5f, 0);
            _boostBar.rectTransform.pivot = new Vector2(0.5f, 0);
            _boostBar.rectTransform.anchoredPosition = new Vector2(0, 18);
            _boostBar.rectTransform.sizeDelta = new Vector2(198, 3);
            _boostBar.enabled = false;

            _loadoutText = CreateText(canvasObject.transform, new Vector2(-18, 18), new Vector2(1, 0), 10, new Color(0.72f, 0.79f, 0.88f));
            _loadoutText.alignment = TextAnchor.LowerRight;
            _loadoutText.rectTransform.sizeDelta = new Vector2(470, 80);
            _loadoutText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _loadoutText.verticalOverflow = VerticalWrapMode.Overflow;
            _loadoutText.raycastTarget = false;
            _loadoutText.enabled = false;
            _supportStripText = CreateText(canvasObject.transform, new Vector2(18, -80), new Vector2(0, 1), 9, new Color(0.68f, 0.78f, 0.86f));
            _supportStripText.alignment = TextAnchor.UpperLeft;
            _supportStripText.rectTransform.sizeDelta = new Vector2(210, 330);
            _supportStripText.raycastTarget = false;
            _supportStripText.enabled = false;
            _lateStripText = CreateText(canvasObject.transform, new Vector2(-18, -80), new Vector2(1, 1), 9, new Color(0.68f, 0.78f, 0.86f));
            _lateStripText.alignment = TextAnchor.UpperRight;
            _lateStripText.rectTransform.sizeDelta = new Vector2(210, 330);
            _lateStripText.raycastTarget = false;
            _lateStripText.enabled = false;

            var arenaBannerObject = new GameObject("Arena Shift Banner");
            arenaBannerObject.transform.SetParent(canvasObject.transform, false);
            _arenaBannerPanel = arenaBannerObject.AddComponent<Image>();
            _arenaBannerPanel.sprite = ProceduralSpriteFactory.Square();
            _arenaBannerPanel.color = new Color(4f / 255f, 6f / 255f, 11f / 255f, 0.55f);
            _arenaBannerPanel.raycastTarget = false;
            var arenaBannerRect = _arenaBannerPanel.rectTransform;
            arenaBannerRect.anchorMin = new Vector2(0.5f, 1f);
            arenaBannerRect.anchorMax = new Vector2(0.5f, 1f);
            arenaBannerRect.pivot = new Vector2(0.5f, 0.5f);
            arenaBannerRect.anchoredPosition = new Vector2(0, -122.4f);
            arenaBannerRect.sizeDelta = new Vector2(300, 56);
            _arenaBannerOutline = arenaBannerObject.AddComponent<Outline>();
            // uGUI Outline is not a stroke: it re-emits the whole graphic four
            // times at the four diagonal offsets. At 1.5px on bold dynamic-font
            // glyphs the four copies overlap into a muddy fringe rather than an
            // edge. One pixel keeps the red warning glow legible while collapsing
            // the copies close enough to read as a single outline. A true stroke
            // needs an SDF text shader, which uGUI Text cannot do.
            _arenaBannerOutline.effectDistance = new Vector2(1f, 1f);
            _arenaBannerOutline.useGraphicAlpha = true;
            _arenaBannerTitle = CreateText(
                arenaBannerObject.transform,
                new Vector2(0, 10),
                new Vector2(0.5f, 0.5f),
                18,
                new Color(254f / 255f, 226f / 255f, 226f / 255f, 1));
            _arenaBannerTitle.font = BrowserBodyFont();
            _arenaBannerTitle.fontStyle = FontStyle.Bold;
            _arenaBannerTitle.alignment = TextAnchor.MiddleCenter;
            _arenaBannerTitle.rectTransform.sizeDelta = new Vector2(286, 24);
            _arenaBannerTitle.text = "THE ABYSS DEEPENS..";
            _arenaBannerTitle.raycastTarget = false;
            _arenaBannerDetail = CreateText(
                arenaBannerObject.transform,
                new Vector2(0, -15),
                new Vector2(0.5f, 0.5f),
                12,
                new Color(203f / 255f, 213f / 255f, 225f / 255f, 0.85f));
            _arenaBannerDetail.font = BrowserBodyFont();
            _arenaBannerDetail.alignment = TextAnchor.MiddleCenter;
            _arenaBannerDetail.rectTransform.sizeDelta = new Vector2(286, 20);
            _arenaBannerDetail.raycastTarget = false;
            _arenaBannerPanel.enabled = false;
            _arenaBannerTitle.enabled = false;
            _arenaBannerDetail.enabled = false;
            _redFlashOverlay = CreateFullscreenOverlay(
                canvasObject.transform,
                "Red Flash Overlay",
                new Color(1f, 1f, 1f, 0f));
            _redFlashOverlay.sprite = ProceduralSpriteFactory.RedHealthVignette();
            _cyanFlashOverlay = CreateFullscreenOverlay(
                canvasObject.transform,
                "Cyan Flash Overlay",
                new Color(0.404f, 0.91f, 0.976f, 0));
            _amberFlashOverlay = CreateFullscreenOverlay(
                canvasObject.transform,
                "Amber Flash Overlay",
                new Color(0.984f, 0.361f, 0.063f, 0));
            var transitionObject = new GameObject("Arena Transition Overlay");
            transitionObject.transform.SetParent(canvasObject.transform, false);
            _transitionOverlay = transitionObject.AddComponent<ArenaTransitionGraphic>();
            _transitionOverlay.raycastTarget = false;
            var transitionRect = _transitionOverlay.rectTransform;
            transitionRect.anchorMin = Vector2.zero;
            transitionRect.anchorMax = Vector2.one;
            transitionRect.offsetMin = Vector2.zero;
            transitionRect.offsetMax = Vector2.zero;
            _hudText = CreateText(canvasObject.transform, new Vector2(18, -18), new Vector2(0, 1), 20, Color.white);
            _hudText.enabled = false;
            _helpText = CreateText(canvasObject.transform, new Vector2(18, 18), new Vector2(0, 0), 16, new Color(0.65f, 0.75f, 0.9f));
            _helpText.text = "WASD / arrows move  •  auto-fire  •  M / Tab menu  •  Esc pause";
            _bossText = CreateText(canvasObject.transform, new Vector2(0, -78), new Vector2(0.5f, 1), 12, new Color(1f, 0.796f, 0.796f));
            _bossText.alignment = TextAnchor.MiddleCenter;
            _bossText.rectTransform.sizeDelta = new Vector2(560, 24);
            _bossText.enabled = false;
            _bossNameText = CreateText(
                canvasObject.transform,
                new Vector2(-220, -78),
                new Vector2(0.5f, 1),
                10,
                new Color(0.996f, 0.796f, 0.796f, 1f));
            _bossNameText.rectTransform.pivot = new Vector2(0, 0.5f);
            _bossNameText.rectTransform.anchoredPosition = new Vector2(-220, -78);
            _bossNameText.rectTransform.sizeDelta = new Vector2(220, 18);
            _bossNameText.alignment = TextAnchor.MiddleLeft;
            _bossNameText.enabled = false;
            _bossHealthText = CreateText(
                canvasObject.transform,
                new Vector2(220, -78),
                new Vector2(0.5f, 1),
                10,
                new Color(0.996f, 0.796f, 0.796f, 1f));
            _bossHealthText.rectTransform.pivot = new Vector2(1, 0.5f);
            _bossHealthText.rectTransform.anchoredPosition = new Vector2(220, -78);
            _bossHealthText.rectTransform.sizeDelta = new Vector2(220, 18);
            _bossHealthText.alignment = TextAnchor.MiddleRight;
            _bossHealthText.enabled = false;
            _bossBarBackground = CreateHudImage(canvasObject.transform, "Boss Health Background");
            _bossBarBackground.sprite = ProceduralSpriteFactory.Square();
            _bossBarBackground.color = new Color(0.039f, 0.02f, 0.031f, 0.92f);
            _bossBarBackground.rectTransform.anchorMin = new Vector2(0.5f, 1);
            _bossBarBackground.rectTransform.anchorMax = new Vector2(0.5f, 1);
            _bossBarBackground.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _bossBarBackground.rectTransform.anchoredPosition = new Vector2(0, -105);
            _bossBarBackground.rectTransform.sizeDelta = new Vector2(440, 9);
            _bossBarFill = CreateHudImage(canvasObject.transform, "Boss Health Fill");
            _bossBarFill.sprite = ProceduralSpriteFactory.Square();
            _bossBarFill.color = new Color(0.937f, 0.267f, 0.267f, 1f);
            _bossBarFill.rectTransform.anchorMin = new Vector2(0.5f, 1);
            _bossBarFill.rectTransform.anchorMax = new Vector2(0.5f, 1);
            _bossBarFill.rectTransform.pivot = new Vector2(0, 0.5f);
            _bossBarFill.rectTransform.anchoredPosition = new Vector2(-220, -105);
            _bossBarFill.rectTransform.sizeDelta = new Vector2(0, 9);
            _toastText = CreateToastView(canvasObject.transform, new Vector2(0, -142), 20);
            _toastViews[0] = _toastText;
            _toastShadows[0] = ConfigureToastShadow(_toastText);
            for (var index = 1; index < _toastViews.Length; index++)
            {
                _toastViews[index] = CreateToastView(
                    canvasObject.transform,
                    new Vector2(0, -142 - index * 42),
                    20);
                _toastShadows[index] = ConfigureToastShadow(_toastViews[index]);
            }
            _touchBaseImage = CreateHudImage(canvasObject.transform, "Touch Joystick Base");
            _touchKnobImage = CreateHudImage(canvasObject.transform, "Touch Joystick Knob");
            _touchBaseImage.sprite = ProceduralSpriteFactory.TouchJoystickBase();
            _touchBaseImage.color = Color.white;
            _touchKnobImage.color = new Color(0.404f, 0.91f, 0.976f, 0.42f);
            SetupHudFxViews();
            CreateBuildChipHud(canvasObject.transform);
        }

        private static Text CreateToastView(Transform parent, Vector2 position, int size)
        {
            var text = CreateText(parent, position, new Vector2(0.5f, 1), size, Color.white);
            text.alignment = TextAnchor.UpperCenter;
            text.fontStyle = FontStyle.Bold;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.rectTransform.sizeDelta = new Vector2(680, 38);
            text.enabled = false;
            return text;
        }

        private static Shadow ConfigureToastShadow(Text text)
        {
            if (text == null) return null;
            var shadow = text.gameObject.GetComponent<Shadow>();
            if (shadow == null) shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectDistance = new Vector2(0f, -1f);
            shadow.useGraphicAlpha = true;
            return shadow;
        }

        private void SetupHudFxViews()
        {
            for (var index = 0; index < _floaterViews.Length; index++)
            {
                var text = CreateText(_canvas.transform, Vector2.zero, new Vector2(0.5f, 0.5f), 14, Color.white);
                text.alignment = TextAnchor.MiddleCenter;
                text.raycastTarget = false;
                text.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                text.rectTransform.anchoredPosition = Vector2.zero;
                text.rectTransform.sizeDelta = new Vector2(180, 42);
                text.enabled = false;
                _floaterViews[index] = text;
            }

            for (var index = 0; index < _damageIndicatorViews.Length; index++)
            {
                var image = CreateHudImage(_canvas.transform, "Damage Indicator_" + index);
                image.sprite = ProceduralSpriteFactory.DamageIndicator();
                image.preserveAspect = true;
                image.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                image.rectTransform.anchoredPosition = Vector2.zero;
                image.rectTransform.sizeDelta = new Vector2(48, 48);
                image.color = new Color(0.94f, 0.27f, 0.29f, 0);
                _damageIndicatorViews[index] = image;
            }

            _floaterSiblingBase = _floaterViews.Length > 0 && _floaterViews[0] != null
                ? _floaterViews[0].transform.GetSiblingIndex()
                : 0;
            _damageIndicatorSiblingBase = _damageIndicatorViews.Length > 0 && _damageIndicatorViews[0] != null
                ? _damageIndicatorViews[0].transform.GetSiblingIndex()
                : 0;

            for (var index = 0; index < _deathGhostViews.Length; index++)
            {
                _deathGhostViews[index] = CreateView(
                    "Death Ghost_" + index,
                    ProceduralSpriteFactory.Circle(),
                    9);
            }
        }

        private void CreateBuildChipHud(Transform parent)
        {
            for (var index = 0; index < _weaponChipBackgrounds.Length; index++)
            {
                CreateBuildChipView(
                    parent,
                    "Weapon Chip " + index,
                    new Vector2(0, 0),
                    new Vector2(12 + index * 161, 12),
                    new Vector2(154, 34),
                    out _weaponChipBackgrounds[index],
                    out _weaponChipAccentBars[index],
                    out _weaponChipIcons[index],
                    out _weaponChipNames[index],
                    out _weaponChipRanks[index]);
            }

            for (var index = 0; index < _supportChipBackgrounds.Length; index++)
            {
                CreateBuildChipView(
                    parent,
                    "Support Chip " + index,
                    new Vector2(0, 1),
                    new Vector2(12, -76 - index * 32),
                    new Vector2(174, 27),
                    out _supportChipBackgrounds[index],
                    out _supportChipAccentBars[index],
                    out _supportChipIcons[index],
                    out _supportChipNames[index],
                    out _supportChipRanks[index]);
            }

            for (var index = 0; index < _lateChipBackgrounds.Length; index++)
            {
                CreateBuildChipView(
                    parent,
                    "Late Upgrade Chip " + index,
                    new Vector2(1, 1),
                    new Vector2(-12, -80 - index * 32),
                    new Vector2(174, 27),
                    out _lateChipBackgrounds[index],
                    out _lateChipAccentBars[index],
                    out _lateChipIcons[index],
                    out _lateChipNames[index],
                    out _lateChipRanks[index]);
            }
        }

        private static Image CreateHudImage(Transform parent, string name)
        {
            var objectRoot = new GameObject(name);
            objectRoot.transform.SetParent(parent, false);
            var image = objectRoot.AddComponent<Image>();
            image.sprite = ProceduralSpriteFactory.Circle();
            image.raycastTarget = false;
            image.enabled = false;
            image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            image.rectTransform.sizeDelta = new Vector2(128, 128);
            return image;
        }

        private static Image CreateFullscreenOverlay(Transform parent, string name, Color color)
        {
            var image = CreateHudImage(parent, name);
            image.sprite = ProceduralSpriteFactory.Square();
            image.color = color;
            image.raycastTarget = false;
            image.rectTransform.anchorMin = Vector2.zero;
            image.rectTransform.anchorMax = Vector2.one;
            image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            image.rectTransform.offsetMin = Vector2.zero;
            image.rectTransform.offsetMax = Vector2.zero;
            image.enabled = true;
            return image;
        }

        private static void SetFullscreenOverlay(Image image, float alpha)
        {
            if (image == null) return;
            var color = image.color;
            color.a = alpha;
            image.color = color;
            image.enabled = alpha > 0.001f;
        }
    }
}
