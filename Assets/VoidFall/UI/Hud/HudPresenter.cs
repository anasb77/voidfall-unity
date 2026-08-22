using UnityEngine;

namespace VoidFall.UI
{
    /// <summary>
    /// Drives the HUD views from immutable <see cref="HudSnapshot"/> frames.
    ///
    /// Pure consumer: no references to the game runtime or simulation. Owns
    /// the presentation-side animation state (health ghost bar, overclock
    /// punch) and the change-detection caches that implement the VF-009
    /// contract - text is rewritten only when its source value changes.
    ///
    /// Values mirror the original UpdateHud exactly:
    /// - ghost bar chases health downward with a 3.2/s time constant and
    ///   never trails below the real fraction (heals snap instantly);
    /// - overclock punch decays at 4.8/s while active;
    /// - boss bar appears only while bosses are active.
    /// </summary>
    public sealed class HudPresenter
    {
        private const float HudFadeSeconds = 0.30f;
        private const float GhostChasePerSecond = 3.2f;
        private const float PunchDecayPerSecond = 4.8f;

        private readonly IHudViewSink _views;

        // Presentation animation state (owned here, not by the sim).
        private float _hudAlpha;
        private float _healthGhostFraction = 1f;
        private float _overclockPunch;

        // Change-detection caches (VF-009): text rewrites only on change.
        private bool _hasPrevious;
        private float _lastHealth = -1f;
        private float _lastMaxHealth = -1f;
        private int _lastSeconds = -1;
        private int _lastLevel = -1;
        private int _lastKills = -1;
        private int _lastParts = -1;
        private int _lastScore = -1;

        public HudPresenter(IHudViewSink views)
        {
            _views = views ?? throw new System.ArgumentNullException(nameof(views));
        }

        /// <summary>Current ghost-bar fraction (exposed for tests).</summary>
        public float HealthGhostFraction => _healthGhostFraction;

        /// <summary>Current overclock punch value (exposed for tests).</summary>
        public float OverclockPunch => _overclockPunch;

        public void Tick(in HudSnapshot s, float unscaledDeltaTime)
        {
            var frameDt = Mathf.Max(0f, unscaledDeltaTime);

            // Fade group: approach target; alpha drives canvas-group fade.
            var targetAlpha = s.HudVisible ? 1f : 0f;
            _hudAlpha = Mathf.MoveTowards(_hudAlpha, targetAlpha, frameDt / HudFadeSeconds);
            _views.SetHudFade(_hudAlpha, s.HudVisible);

            // Health bars. The ghost chases downward exponentially and snaps
            // up instantly on heal; it starts at full so run start reads as a
            // draining bar rather than an instant fill.
            var hpFraction = s.HealthFraction;
            if (!_hasPrevious) _healthGhostFraction = hpFraction < 1f ? 1f : hpFraction;
            _healthGhostFraction +=
                (hpFraction - _healthGhostFraction) *
                (1f - Mathf.Exp(-GhostChasePerSecond * frameDt));
            if (_healthGhostFraction < hpFraction) _healthGhostFraction = hpFraction;
            _views.SetHealthFill(hpFraction);
            _views.SetHealthGhostFill(Mathf.Clamp01(_healthGhostFraction));

            // Integrity labels: rewrite only when either source value changes.
            if (!_hasPrevious || _lastHealth != s.Health || _lastMaxHealth != s.MaxHealth)
            {
                _lastHealth = s.Health;
                _lastMaxHealth = s.MaxHealth;
                _views.SetHealthText(
                    $"INTEGRITY   {Mathf.CeilToInt(Mathf.Max(0, s.Health))}/{Mathf.CeilToInt(s.MaxHealth)}");
                _views.SetHealthValueText(
                    $"{Mathf.CeilToInt(Mathf.Max(0, s.Health))}/{Mathf.CeilToInt(s.MaxHealth)}");
            }

            _views.SetXpFill(s.XpFraction);

            // Run clock: M:SS, rewritten once per in-game second.
            var seconds = Mathf.Max(0, Mathf.FloorToInt(s.TimeSeconds));
            if (!_hasPrevious || seconds != _lastSeconds)
            {
                _lastSeconds = seconds;
                _views.SetTimeText($"{seconds / 60}:{seconds % 60:00}");
            }

            if (!_hasPrevious || _lastLevel != s.Level)
            {
                _lastLevel = s.Level;
                _views.SetLevelText($"LV {s.Level}");
            }

            // Metrics strip: kills/parts/score share one rewrite gate.
            if (!_hasPrevious || _lastKills != s.Kills || _lastParts != s.PartsEarned ||
                _lastScore != s.Score)
            {
                _lastKills = s.Kills;
                _lastParts = s.PartsEarned;
                _lastScore = s.Score;
                _views.SetMetricsSummary($"K {s.Kills}   P {s.PartsEarned}   SCORE {s.Score:N0}");
                _views.SetMetricValue(0, s.Kills.ToString());
                _views.SetMetricValue(1, s.PartsEarned.ToString());
                _views.SetMetricValue(2, s.Score.ToString("N0"));
            }

            TickOverclock(in s, frameDt);
            TickBossBar(in s);

            _hasPrevious = true;
        }

        private void TickOverclock(in HudSnapshot s, float frameDt)
        {
            if (s.OverclockActive)
            {
                _overclockPunch = Mathf.MoveTowards(_overclockPunch, 0f, frameDt * PunchDecayPerSecond);
            }
            else
            {
                _overclockPunch = 0f;
            }

            // Browser overdrive stacks add a full duration per pickup, so the
            // bar denominator scales with streak.
            var stackSeconds = 15f;
            var fill = Mathf.Clamp01(
                s.OverclockRemainingSeconds /
                Mathf.Max(stackSeconds, s.OverclockStreak * stackSeconds));
            _views.SetBoostPanel(s.OverclockActive, s.OverclockPowerTier, fill, _overclockPunch);
        }

        private void TickBossBar(in HudSnapshot s)
        {
            var visible = s.ActiveBossCount > 0;
            _views.SetBossBar(visible, s.BossFraction);
            if (!visible)
            {
                return;
            }

            var header = s.BossHeader;
            _views.SetBossNameText(header);
            _views.SetBossHealthText(Mathf.CeilToInt(s.BossHealth).ToString());
        }
    }
}