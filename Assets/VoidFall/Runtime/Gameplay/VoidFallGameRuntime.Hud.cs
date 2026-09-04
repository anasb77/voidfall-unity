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

        private void UpdateOverclockHudAnimation(bool active)
        {
            _overclockHudPunch = Mathf.MoveTowards(
                _overclockHudPunch,
                0f,
                Mathf.Max(0f, Time.unscaledDeltaTime) * 4.8f);
            if (_boostText != null)
            {
                var scale = active ? 1f + _overclockHudPunch * 0.28f : 1f;
                _boostText.rectTransform.localScale = new Vector3(scale, scale, 1f);
                _boostText.color = _overclock.PowerTier >= 3
                    ? Color.Lerp(new Color(0.28f, 0.94f, 1f), new Color(1f, 0.32f, 0.84f), _overclockHudPunch)
                    : new Color(0.45f, 0.94f, 1f, 1f);
            }

            var ghostsVisible = active && _overclock.Streak >= 2 && _overclockHudPunch > 0.01f;
            if (_boostGhostA != null)
            {
                _boostGhostA.enabled = ghostsVisible;
                _boostGhostA.rectTransform.anchoredPosition = new Vector2(-24f - 5f * _overclockHudPunch, 34f);
                _boostGhostA.color = new Color(0.08f, 0.9f, 1f, _overclockHudPunch * 0.65f);
            }
            if (_boostGhostB != null)
            {
                _boostGhostB.enabled = ghostsVisible && _overclock.Streak >= 3;
                _boostGhostB.rectTransform.anchoredPosition = new Vector2(-24f + 5f * _overclockHudPunch, 34f);
                _boostGhostB.color = new Color(1f, 0.05f, 0.68f, _overclockHudPunch * 0.58f);
            }
            if (!active)
            {
                _lastOverclockHudStreak = -1;
                _lastOverclockHudSecond = -1;
            }
        }

        private void UpdateHud()
        {
            var hudVisible = ShouldShowHud();            if (_hudGroup != null)
            {
                _hudGroup.alpha = Mathf.MoveTowards(
                    _hudGroup.alpha,
                    hudVisible ? 1f : 0f,
                    Time.unscaledDeltaTime / HudFadeSeconds);
                _hudGroup.interactable = hudVisible;
                _hudGroup.blocksRaycasts = hudVisible;
            }
            // EnterMainMenu disables this canvas so the menus do not pay for HUD
            // batching. Re-enabling unconditionally here undid that every frame,
            // and also let a fully faded HUD keep submitting geometry. Stay
            // enabled only while something is actually visible.
            if (_canvas != null)
                _canvas.enabled = hudVisible || _hudGroup == null || _hudGroup.alpha > 0.001f;
            UpdateHudResponsiveLayout();
            UpdateDamageOverlays();
            UpdateArenaBanner();
            var hpFraction = _gameSim.Player.MaxHealth > 0
                ? Mathf.Clamp01(_gameSim.Player.Health / _gameSim.Player.MaxHealth)
                : 0;
            _healthGhostFraction +=
                (hpFraction - _healthGhostFraction) *
                (1f - Mathf.Exp(-3.2f * Mathf.Max(0, Time.unscaledDeltaTime)));
            if (_healthGhostFraction < hpFraction) _healthGhostFraction = hpFraction;
            if (_xpBarFill != null)
                _xpBarFill.fillAmount = _xpNeed > 0 ? Mathf.Clamp01(_xp / _xpNeed) : 0;
            if (_healthBarFill != null) _healthBarFill.fillAmount = hpFraction;
            if (_healthBarGhost != null) _healthBarGhost.fillAmount = Mathf.Clamp01(_healthGhostFraction);
            if (_healthText != null && (_lastHudHealth != _gameSim.Player.Health || _lastHudMaxHealth != _gameSim.Player.MaxHealth))
            {
                _lastHudHealth = _gameSim.Player.Health;
                _lastHudMaxHealth = _gameSim.Player.MaxHealth;
                _healthText.text = $"INTEGRITY   {Mathf.CeilToInt(Mathf.Max(0, _gameSim.Player.Health))}/{Mathf.CeilToInt(_gameSim.Player.MaxHealth)}";
            }
            if (_healthLabelText != null) _healthLabelText.text = "INTEGRITY";
            if (_healthValueText != null && (_lastHudHealth != _gameSim.Player.Health || _lastHudMaxHealth != _gameSim.Player.MaxHealth))
                _healthValueText.text = $"{Mathf.CeilToInt(Mathf.Max(0, _gameSim.Player.Health))}/{Mathf.CeilToInt(_gameSim.Player.MaxHealth)}";
            if (_timeText != null)
            {
                var seconds = Mathf.Max(0, Mathf.FloorToInt(_time));
                if (seconds != _lastHudSeconds)
                {
                    _lastHudSeconds = seconds;
                    _timeText.text = $"{seconds / 60}:{seconds % 60:00}";
                }
            }
            if (_levelText != null && _lastHudLevel != _level)
            {
                _lastHudLevel = _level;
                _levelText.text = $"LV {_level}";
            }
            // The objective line is rebuilt on a fixed cadence inside the
            // simulation tick (StepObjectiveTracker); the HUD only rewrites
            // the label when that cached string actually changes.
            if (_objectiveText != null && _objectiveLine != _lastObjectiveLine)
            {
                _lastObjectiveLine = _objectiveLine;
                _objectiveText.text = _objectiveLine;
                _objectiveText.enabled = !string.IsNullOrEmpty(_objectiveLine);
            }
            AnimateRiftPortal(Time.unscaledDeltaTime);
            UpdateRouletteChest(Time.unscaledDeltaTime);
            var hudScore = CurrentScore();
            if ((_metricsText != null || _metricValues[0] != null) &&
                (_lastHudKills != _kills || _lastHudParts != _partsEarned || _lastHudScore != hudScore))
            {
                _lastHudKills = _kills;
                _lastHudParts = _partsEarned;
                _lastHudScore = hudScore;
                if (_metricsText != null)
                    _metricsText.text = $"K {_kills}   P {_partsEarned}   SCORE {hudScore:N0}";
                if (_metricValues[0] != null) _metricValues[0].text = _kills.ToString();
                if (_metricValues[1] != null) _metricValues[1].text = _partsEarned.ToString();
                if (_metricValues[2] != null) _metricValues[2].text = hudScore.ToString("N0");
            }
            if (_pauseButton != null)
            {
                _pauseButton.gameObject.SetActive(hudVisible);
                _pauseButton.interactable =
                    !_paused && !_revivePending && !_levelUpActive && _menuPage == MenuPage.None;
            }
            // Only the glyph fallback carries text. When the icon atlas resolved,
            // writing "||" here as well drew both on top of each other.
            if (_pauseButtonText != null)
                _pauseButtonText.text = ControlIconTexture() == null ? "||" : string.Empty;
            var overdriveActive = _overclock.Active && !_gameOver;
            if (_boostPanel != null) _boostPanel.enabled = overdriveActive;
            if (_boostIcon != null)
            {
                _boostIcon.enabled = overdriveActive;
                _boostIcon.color = _overclock.PowerTier >= 3
                    ? new Color(1f, 0.24f, 0.78f, 1f)
                    : new Color(0.12f, 0.9f, 1f, 1f);
            }
            if (_boostText != null)
            {
                _boostText.enabled = overdriveActive;
                if (overdriveActive && _lastOverclockHudStreak != _overclock.Streak)
                {
                    _lastOverclockHudStreak = _overclock.Streak;
                    var label = _overclock.Streak > 1
                        ? "OVERCLOCKED ×" + _overclock.Streak
                        : "OVERCLOCKED";
                    _boostText.text = label;
                    if (_boostGhostA != null) _boostGhostA.text = label;
                    if (_boostGhostB != null) _boostGhostB.text = label;
                }
            }
            if (_boostSecondsText != null)
            {
                _boostSecondsText.enabled = overdriveActive;
                if (overdriveActive)
                {
                    var seconds = Mathf.CeilToInt(_overclock.RemainingSeconds);
                    if (_lastOverclockHudSecond != seconds)
                    {
                        _lastOverclockHudSecond = seconds;
                        _boostSecondsText.text = seconds + "s";
                    }
                }
            }
            if (_boostBar != null)
            {
                _boostBar.enabled = overdriveActive;
                _boostBar.color = _overclock.PowerTier >= 3
                    ? new Color(1f, 0.15f, 0.68f, 0.96f)
                    : _overclock.PowerTier == 2
                        ? new Color(0.58f, 0.24f, 1f, 0.95f)
                        : new Color(0.08f, 0.86f, 1f, 0.94f);
                _boostBar.fillAmount = Mathf.Clamp01(
                    _overclock.RemainingSeconds /
                    Mathf.Max(OverclockRules.StackDurationSeconds, _overclock.Streak * OverclockRules.StackDurationSeconds));
            }
            UpdateOverclockHudAnimation(overdriveActive);
            if (_loadoutText != null && Time.unscaledTime >= _nextLoadoutHudRefresh)
            {
                _nextLoadoutHudRefresh = Time.unscaledTime + 0.25f;
                var loadout = BuildLoadoutHudText();
                if (loadout != _lastLoadoutHudText)
                {
                    _lastLoadoutHudText = loadout;
                    _loadoutText.text = loadout;
                    if (_supportStripText != null)
                    {
                        _supportStripText.text = BuildUpgradeStripHudText(false);
                        _supportStripText.enabled = false;
                    }
                    if (_lateStripText != null)
                    {
                        _lateStripText.text = BuildUpgradeStripHudText(true);
                        _lateStripText.enabled = false;
                    }
                    UpdateBuildChipHud();
                }
            }
            // This early bring-up readout is disabled at setup and has no
            // counterpart in the browser HUD, but it was still formatting five
            // interpolated lines every frame for an invisible component. Keep the
            // path for diagnostics, and only pay for it when it is on screen.
            if (_hudText != null && _hudText.enabled)
            {
                var phase = _gameOver
                    ? "GAME OVER — press R"
                    : _revivePending
                        ? "INTEGRITY ZERO — press Y to revive or N to end"
                    : _levelUpActive
                        ? "LEVEL UP — press 1, 2, or 3"
                        : _paused ? "PAUSED — press Esc" : "PLAYING";
                _hudText.text = $"{phase}\nTime {_time:0.0}s   Level {_level}   XP {_xp:0}/{_xpNeed}\n" +
                    $"Integrity {_gameSim.Player.Health:0}/{_gameSim.Player.MaxHealth:0}   Kills {_kills}   Score {CurrentScore()}\n" +
                    $"Pistol rank {_pistolRank}/6   Calibration {_calibrationRank}/4\n" +
                    $"Arena {ArenaName(_arenaId)}   Cycle {ArenaCycleRules.At(ArenaIdName(_arenaId), ArenaCycleElapsedSeconds()).CycleId}";
            }

            if (_helpText != null)
            {
                // The browser keeps normal-run controls out of the HUD. The
                // Unity hint was useful during early bring-up, but it is an
                // extra visible element compared with GameUI.tsx. Keep the
                // text view only for the diagnostic level-up list.
                _helpText.enabled = _levelUpActive && _levelOptions != null;
                if (_helpText.enabled)
                {
                    var options = "LEVEL UP\n";
                    for (var index = 0; index < _levelOptions.Length; index++)
                    {
                        options += (index + 1) + ". " + _levelOptions[index].Name + " - " + _levelOptions[index].Description + "\n";
                    }
                    options += "Q. Reroll (" + _rerollsRemaining + " left)\n";
                    _helpText.text = options;
                }
            }

            var activeBossCount = ActiveBosses();
            var bossHealth = 0f;
            var bossMaxHealth = 0f;
            BossState firstBoss = default(BossState);
            var firstBossSet = false;
            EnsureBossOrderEntries();
            for (var bossOrder = 0; bossOrder < _gameSim.BossOrderCount; bossOrder++)
            {
                var index = _gameSim.BossOrder[bossOrder];
                var boss = _gameSim.Bosses[index];
                if (!boss.Active) continue;
                if (!firstBossSet)
                {
                    firstBoss = boss;
                    firstBossSet = true;
                }
                bossHealth += Mathf.Max(0, boss.Health);
                bossMaxHealth += Mathf.Max(0, boss.MaxHealth);
            }
            var bossFraction = bossMaxHealth > 0
                ? Mathf.Clamp01(bossHealth / bossMaxHealth)
                : 0;
            if (_bossText != null)
            {
                var bossHpInt = activeBossCount > 0 ? Mathf.CeilToInt(bossHealth) : 0;
                var bossName = activeBossCount == 0
                    ? string.Empty
                    : activeBossCount == 1
                        ? (FindBoss(firstBoss.Id)?.Name ?? firstBoss.Id).ToUpperInvariant()
                        : activeBossCount + " BOSSES";
                if (bossName != _bossHudName || bossHpInt != _bossHudHp || activeBossCount != _bossHudCount)
                {
                    _bossHudName = bossName;
                    _bossHudHp = bossHpInt;
                    _bossHudCount = activeBossCount;
                    if (activeBossCount == 0)
                    {
                        _bossText.text = "";
                        _bossText.enabled = false;
                        if (_bossNameText != null) _bossNameText.enabled = false;
                        if (_bossHealthText != null) _bossHealthText.enabled = false;
                    }
                    else
                    {
                        _bossText.text = bossName + "    " + bossHpInt;
                        _bossText.enabled = false;
                        if (_bossNameText != null)
                        {
                            _bossNameText.text = bossName;
                            _bossNameText.enabled = true;
                        }
                        if (_bossHealthText != null)
                        {
                            _bossHealthText.text = bossHpInt.ToString();
                            _bossHealthText.enabled = true;
                        }
                    }
                }
            }
            if (_bossBarBackground != null && _bossBarFill != null)
            {
                _bossBarBackground.enabled = activeBossCount > 0;
                _bossBarFill.enabled = activeBossCount > 0;
                if (activeBossCount > 0)
                {
                    const float barWidth = 440f;
                    const float barHeight = 9f;
                    var fillWidth = barWidth * bossFraction;
                    _bossBarFill.rectTransform.sizeDelta = new Vector2(fillWidth, barHeight);
                    _bossBarFill.rectTransform.anchoredPosition = new Vector2(-barWidth * 0.5f, -105f);
                }
            }
            UpdateToastViews();
            UpdateTouchHud();
        }

        private void UpdateArenaBanner()
        {
            if (_arenaBannerPanel == null || _arenaBannerTitle == null || _arenaBannerDetail == null)
                return;

            var visible = _arenaBannerRemaining > 0.001f &&
                _menuPage == MenuPage.None &&
                !_gameOver &&
                !_levelUpActive &&
                !_revivePending;
            _arenaBannerPanel.enabled = visible;
            _arenaBannerTitle.enabled = visible;
            _arenaBannerDetail.enabled = visible;
            if (!visible) return;

            // Match the browser's 0.35-second fade-in and ambient pulse.
            var fadeIn = Mathf.Clamp01(
                ((float)ArenaRules.WarningSeconds - _arenaBannerRemaining) / 0.35f);
            var pulse = 0.72f + 0.28f * Mathf.Sin(_ambientClock * 6f);
            var panelColor = _arenaBannerPanel.color;
            panelColor.a = 0.55f * fadeIn;
            _arenaBannerPanel.color = panelColor;
            var outlineColor = new Color(
                248f / 255f,
                113f / 255f,
                113f / 255f,
                0.55f * pulse * fadeIn);
            _arenaBannerOutline.effectColor = outlineColor;
            var titleColor = _arenaBannerTitle.color;
            titleColor.a = pulse * fadeIn;
            _arenaBannerTitle.color = titleColor;
            var detailColor = _arenaBannerDetail.color;
            detailColor.a = 0.85f * fadeIn;
            _arenaBannerDetail.color = detailColor;
            var seconds = Mathf.Max(1, Mathf.CeilToInt(_arenaBannerRemaining));
            _arenaBannerDetail.text = ArenaName(_arenaBannerIncoming) + " · " + seconds + "s";
        }

        private void UpdateHudResponsiveLayout()
        {
            if (_canvas == null) return;
            var viewportWidth = Screen.width;
            var viewportHeight = Screen.height;
            var safeArea = Screen.safeArea;
            var narrow = viewportWidth <= 720;
            if (_hudLayoutInitialized &&
                narrow == _hudNarrow &&
                viewportWidth == _hudLayoutWidth &&
                viewportHeight == _hudLayoutHeight &&
                safeArea == _hudLayoutSafeArea) return;
            _hudLayoutInitialized = true;
            _hudNarrow = narrow;
            _hudLayoutWidth = viewportWidth;
            _hudLayoutHeight = viewportHeight;
            _hudLayoutSafeArea = safeArea;
            var safeLeftInset = Mathf.Max(12f, safeArea.xMin);
            var safeRightInset = Mathf.Max(12f, viewportWidth - safeArea.xMax);
            var safeTopInset = Mathf.Max(0f, viewportHeight - safeArea.yMax);

            // The browser's narrow grid gives the integrity block the
            // remaining first-column width: viewport minus the 78px clock,
            // 44px pause button, two 8px gaps, and 12px outer margins. Keep
            // the source minimum of 120px instead of leaving the desktop
            // 240px panel stranded beside the clock.
            var healthWidth = narrow ? Mathf.Max(120f, Screen.width - 162f) : 240f;
            if (_healthPanel != null)
            {
                _healthPanel.rectTransform.sizeDelta = new Vector2(healthWidth, 43f);
                if (_healthBarBackground != null)
                    _healthBarBackground.rectTransform.sizeDelta = new Vector2(healthWidth - 20f, 10f);
                if (_healthBarGhost != null)
                    _healthBarGhost.rectTransform.sizeDelta = new Vector2(healthWidth - 20f, 10f);
                if (_healthBarFill != null)
                    _healthBarFill.rectTransform.sizeDelta = new Vector2(healthWidth - 20f, 10f);
                if (_healthText != null)
                    _healthText.rectTransform.sizeDelta = new Vector2(healthWidth - 20f, 18f);
                if (_healthLabelText != null)
                    _healthLabelText.rectTransform.sizeDelta = new Vector2(Mathf.Max(80f, healthWidth - 90f), 16f);
                if (_healthValueText != null)
                    _healthValueText.rectTransform.anchoredPosition = new Vector2(12f + healthWidth - 10f, -27f);
            }

            if (_clockPanel != null)
            {
                var clockWidth = narrow ? 78f : 94f;
                _clockPanel.rectTransform.sizeDelta = new Vector2(clockWidth, 52f);
                SetTopHudAnchor(_clockPanel.rectTransform, narrow, clockWidth, -13f);
                if (_timeText != null)
                {
                    _timeText.rectTransform.sizeDelta = new Vector2(clockWidth, 30f);
                    SetTopHudAnchor(_timeText.rectTransform, narrow, clockWidth, -19f);
                }
                if (_levelText != null)
                {
                    _levelText.rectTransform.sizeDelta = new Vector2(clockWidth, 16f);
                    SetTopHudAnchor(_levelText.rectTransform, narrow, clockWidth, -49f);
                }
            }

            var metricsVisible = !narrow;
            if (_metricsPanel != null) _metricsPanel.enabled = metricsVisible;
            for (var index = 0; index < _metricIcons.Length; index++)
            {
                if (_metricIcons[index] != null) _metricIcons[index].enabled = metricsVisible;
                if (_metricValues[index] != null) _metricValues[index].enabled = metricsVisible;
                if (index < _metricDividers.Length && _metricDividers[index] != null)
                    _metricDividers[index].enabled = metricsVisible;
            }

            for (var index = 0; index < _weaponChipBackgrounds.Length; index++)
            {
                var width = narrow ? 56f : 122f;
                if (_weaponChipBackgrounds[index] != null)
                {
                    _weaponChipBackgrounds[index].rectTransform.sizeDelta = new Vector2(width, 34f);
                    _weaponChipBackgrounds[index].rectTransform.anchoredPosition =
                        new Vector2(12f + index * (narrow ? 63f : 129f), 12f);
                }
            }
            ResizeOwnedChipViews(
                _supportChipBackgrounds,
                _supportChipRanks,
                narrow,
                narrow ? 60f : 174f,
                new Vector2(safeLeftInset, -OwnedUpgradeStripTop(false, safeTopInset)),
                false,
                viewportHeight);
            ResizeOwnedChipViews(
                _lateChipBackgrounds,
                _lateChipRanks,
                narrow,
                narrow ? 60f : 174f,
                new Vector2(-safeRightInset, -OwnedUpgradeStripTop(true, safeTopInset)),
                true,
                viewportHeight);
            SetChipLabelVisibility(_weaponChipNames, _weaponChipBackgrounds, !narrow);
            SetChipLabelVisibility(_supportChipNames, _supportChipBackgrounds, !narrow);
            SetChipLabelVisibility(_lateChipNames, _lateChipBackgrounds, !narrow);
        }

        private static float OwnedUpgradeStripTop(bool late, float safeTopInset)
        {
            return Mathf.Max(late ? 76f : 72f, safeTopInset + (late ? 72f : 68f));
        }

        private void UpdateBuildChipHud()
        {
            for (var index = 0; index < _weaponChipBackgrounds.Length; index++)
            {
                var rank = _upgradeProgress != null && index < _upgradeProgress.WeaponRanks.Length
                    ? _upgradeProgress.WeaponRanks[index]
                    : 0;
                var evolved = _upgradeProgress != null && index < _upgradeProgress.Evolved.Length &&
                    _upgradeProgress.Evolved[index];
                var active = index < ContentCatalog.Weapons.Length && rank > 0;
                var weapon = active ? ContentCatalog.Weapons[index] : null;
                SetBuildChipView(
                    _weaponChipBackgrounds[index],
                    _weaponChipAccentBars[index],
                    _weaponChipIcons[index],
                    _weaponChipNames[index],
                    _weaponChipRanks[index],
                    active,
                    weapon == null ? string.Empty : weapon.Id,
                    active ? WeaponDisplayName(index, evolved) : string.Empty,
                    rank,
                    weapon == null ? 0 : weapon.Ranks.Length,
                    active ? ParseColor(WeaponDisplayAccent(index, evolved), new Color(0.4f, 0.9f, 1f, 1f)) : Color.white,
                    false,
                    evolved);
            }

            for (var index = 0; index < _supportChipBackgrounds.Length; index++)
            {
                var rank = _upgradeProgress != null && index < _upgradeProgress.SupportRanks.Length
                    ? _upgradeProgress.SupportRanks[index]
                    : 0;
                var active = index < ContentCatalog.Supports.Length && rank > 0;
                var support = active ? ContentCatalog.Supports[index] : null;
                SetBuildChipView(
                    _supportChipBackgrounds[index],
                    _supportChipAccentBars[index],
                    _supportChipIcons[index],
                    _supportChipNames[index],
                    _supportChipRanks[index],
                    active,
                    support == null ? string.Empty : support.Id,
                    support == null ? string.Empty : support.Name,
                    rank,
                    support == null ? 0 : support.MaxRank,
                    support == null ? Color.white : ParseColor(support.Accent, new Color(0.4f, 0.9f, 1f, 1f)),
                    true,
                    false);
            }

            for (var index = 0; index < _lateChipBackgrounds.Length; index++)
            {
                var rank = _upgradeProgress != null && index < _upgradeProgress.LateRanks.Length
                    ? _upgradeProgress.LateRanks[index]
                    : 0;
                var active = index < ContentCatalog.LateUpgrades.Length && rank > 0;
                var late = active ? ContentCatalog.LateUpgrades[index] : null;
                SetBuildChipView(
                    _lateChipBackgrounds[index],
                    _lateChipAccentBars[index],
                    _lateChipIcons[index],
                    _lateChipNames[index],
                    _lateChipRanks[index],
                    active,
                    late == null ? string.Empty : late.Id,
                    late == null ? string.Empty : late.Name,
                    rank,
                    late == null ? 0 : late.MaxRank,
                    late == null ? Color.white : ParseColor(late.Accent, new Color(0.4f, 0.9f, 1f, 1f)),
                    true,
                    false);
            }
            SetChipLabelVisibility(_weaponChipNames, _weaponChipBackgrounds, !_hudNarrow);
            SetChipLabelVisibility(_supportChipNames, _supportChipBackgrounds, !_hudNarrow);
            SetChipLabelVisibility(_lateChipNames, _lateChipBackgrounds, !_hudNarrow);
            ResizeOwnedChipViews(
                _supportChipBackgrounds,
                _supportChipRanks,
                _hudNarrow,
                _hudNarrow ? 60f : 174f,
                new Vector2(
                    Mathf.Max(12f, Screen.safeArea.xMin),
                    -OwnedUpgradeStripTop(
                        false,
                        Mathf.Max(0f, Screen.height - Screen.safeArea.yMax))),
                false,
                Screen.height);
            ResizeOwnedChipViews(
                _lateChipBackgrounds,
                _lateChipRanks,
                _hudNarrow,
                _hudNarrow ? 60f : 174f,
                new Vector2(
                    -Mathf.Max(12f, Screen.width - Screen.safeArea.xMax),
                    -OwnedUpgradeStripTop(
                        true,
                        Mathf.Max(0f, Screen.height - Screen.safeArea.yMax))),
                true,
                Screen.height);
        }

        private void UpdateTouchHud()
        {
            if (_touchBaseImage == null || _touchKnobImage == null) return;
            var visible = _input.TouchActive && !_paused && !_gameOver && !_levelUpActive && !_revivePending;
            if (!visible)
            {
                _touchBaseImage.enabled = false;
                _touchKnobImage.enabled = false;
                return;
            }

            var scale = Mathf.Clamp(_saveData?.settings?.touchSize ?? 1f, 0.75f, 1.35f);
            var radius = 64f * scale * 0.82f;
            var safeArea = Screen.safeArea;
            var safeX = Mathf.Clamp(_input.TouchOrigin.x, safeArea.xMin + radius, safeArea.xMax - radius);
            var safeY = Mathf.Clamp(_input.TouchOrigin.y, safeArea.yMin + radius, safeArea.yMax - radius);
            var basePosition = new Vector3(safeX, safeY, 0);
            var knobPosition = basePosition + new Vector3(
                _input.TouchAxis.x * radius * 0.78f,
                _input.TouchAxis.y * radius * 0.78f,
                0);
            _touchBaseImage.rectTransform.position = basePosition;
            _touchKnobImage.rectTransform.position = knobPosition;
            _touchBaseImage.rectTransform.sizeDelta = Vector2.one * (radius * 2f);
            _touchKnobImage.rectTransform.sizeDelta = Vector2.one * (radius * 0.84f);
            _touchBaseImage.enabled = true;
            _touchKnobImage.enabled = true;
        }

        private void UpdateDamageOverlays()
        {
            if (_redFlashOverlay == null || _cyanFlashOverlay == null || _amberFlashOverlay == null) return;
            var reducedMotion = _saveData?.settings != null && _saveData.settings.reducedMotion;
            var lowHealth = !_gameOver && _gameSim.Player.Health > 0 && _gameSim.Player.Health < _gameSim.Player.MaxHealth * 0.3f;
            var redAlpha = _redFlash * (reducedMotion ? 0.35f : 0.8f) +
                SourceLowHealthOverlayAlpha(lowHealth, _ambientClock);
            var cyanAlpha = reducedMotion ? 0 : _cyanFlash * 0.13f;
            var amberAlpha = _amberFlash * (reducedMotion ? 0.07f : 0.2f);
            SetFullscreenOverlay(_redFlashOverlay, Mathf.Clamp01(redAlpha));
            SetFullscreenOverlay(_cyanFlashOverlay, Mathf.Clamp01(cyanAlpha));
            SetFullscreenOverlay(_amberFlashOverlay, Mathf.Clamp01(amberAlpha));
        }

        private void UpdateToastTimers(float frameDt)
        {
            var write = 0;
            for (var read = 0; read < _toastStates.Length; read++)
            {
                var toast = _toastStates[read];
                if (!toast.Active) continue;
                toast.Remaining = Mathf.Max(0, toast.Remaining - frameDt);
                if (toast.Remaining <= 0) continue;
                _toastStates[write++] = toast;
            }
            while (write < _toastStates.Length)
                _toastStates[write++] = new ToastState();
        }

        private void UpdateToastViews()
        {
            var visible = _menuPage == MenuPage.None &&
                !_gameOver &&
                !_levelUpActive &&
                !_revivePending &&
                !_paused;
            var highContrast = _saveData?.settings != null && _saveData.settings.highContrast;
            // Font size only depends on screen width: rewrite views only on change.
            var fontSize = ToastFontSize(Screen.width);
            var fontSizeChanged = fontSize != _toastFontSize;
            if (fontSizeChanged) _toastFontSize = fontSize;
            for (var index = 0; index < _toastViews.Length; index++)
            {
                var view = _toastViews[index];
                if (view == null) continue;
                var toast = _toastStates[index];
                if (!visible || !toast.Active || toast.Remaining <= 0)
                {
                    if (view.enabled)
                    {
                        view.text = string.Empty;
                        view.enabled = false;
                    }
                    view.rectTransform.localScale = Vector3.one;
                    continue;
                }

                var elapsed = Mathf.Max(0f, toast.Duration - toast.Remaining);
                var alpha = ToastAnimationAlphaAt(elapsed, toast.Duration);
                var color = highContrast ? Color.white : ToastColor(toast.Kind);
                color.a *= alpha;
                if (fontSizeChanged) view.fontSize = fontSize;
                var scale = ToastAnimationScaleAt(elapsed, toast.Duration);
                view.rectTransform.localScale = new Vector3(scale, scale, 1f);
                var scaleFactor = _canvas != null ? Mathf.Max(0.0001f, _canvas.scaleFactor) : 1f;
                var safeTopInset = Mathf.Max(0f, Screen.height - Screen.safeArea.yMax);
                var stackTop = ToastStackTop(Screen.height, safeTopInset) / scaleFactor;
                view.rectTransform.anchoredPosition = new Vector2(
                    0f,
                    -stackTop - index * ToastStackRowSpacing / scaleFactor +
                        ToastAnimationOffsetAt(elapsed, toast.Duration) / scaleFactor);
                view.color = color;
                if (_toastShadows[index] != null)
                {
                    _toastShadows[index].effectColor = highContrast
                        ? new Color(0f, 0f, 0f, 0.7f * alpha)
                        : new Color(color.r, color.g, color.b, 0.52f * alpha);
                }
                if (view.text != toast.Formatted) view.text = toast.Formatted;
                view.enabled = true;
            }
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
            _objectiveText = CreateText(canvasObject.transform, new Vector2(-10, -68), new Vector2(0.5f, 1), 10, new Color(0.663f, 0.733f, 0.812f));
            _objectiveText.alignment = TextAnchor.UpperCenter;
            _objectiveText.rectTransform.sizeDelta = new Vector2(420, 16);
            _objectiveText.enabled = false;

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
    }
}
