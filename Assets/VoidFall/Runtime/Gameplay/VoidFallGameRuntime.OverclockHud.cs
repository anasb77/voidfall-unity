using UnityEngine;
using UnityEngine.UI;
using VoidFall.Core;
using VoidFall.UI;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private const float OverclockFontRenderScale = 24f / 64f;
        private RectTransform _overclockHudRoot, _overclockWordRow, _overclockWordClip;
        private CanvasGroup _overclockHudGroup;
        private Image _overclockLineTrack, _overclockLineGlow, _bossHudPanel, _bossHudGhost;
        private Outline _overclockOutline;
        private float _overclockEntryAge, _overclockRefillAge, _overclockDisplayedFraction = 1f;
        private float _overclockHudBottom, _bossHudBottom, _bossGhostFraction = 1f, _lastBossFraction = -1f;
        private int _overclockShownStack;
        private float _overclockGlyphWidth = 336f;
        private bool _overclockWasActive, _overclockPickupPending, _overclockNewActivationPending;

        private void SetupOverclockRemaster()
        {
            var parent = _canvas.transform;
            _healthLabelText.fontSize = 11;
            _healthValueText.fontSize = 14;
            _healthPanel.color = new Color(0.035f, 0.06f, 0.11f, 0.94f);
            _healthBarFill.color = new Color(0.88f, 0.43f, 0.53f);
            _timeText.fontSize = 26;
            _levelText.fontSize = 11;
            _objectiveText.fontSize = 12;
            _objectiveText.color = new Color(0.78f, 0.84f, 0.90f);
            _objectiveText.rectTransform.sizeDelta = new Vector2(520, 25);
            _clockPanel.color = _metricsPanel.color = new Color(0.025f, 0.05f, 0.10f, 0.94f);
            foreach (var value in _metricValues) if (value != null) value.fontSize = 13;
            foreach (var icon in _metricIcons)
                if (icon != null) { icon.color = new Color(0.65f, 0.76f, 0.85f); icon.rectTransform.sizeDelta = new Vector2(14, 14); }

            _bossHudPanel = CreateHudImage(parent, "Boss HUD Backing");
            _bossHudPanel.sprite = UISprites.Rounded(2, new Color(.07f, .055f, .10f), new Color(.085f, .075f, .13f), new Color(.43f, .27f, .36f, .6f));
            _bossHudPanel.type = Image.Type.Sliced;
            PositionTop(_bossHudPanel.rectTransform, 0, 97, 472, 48);
            _bossHudPanel.transform.SetSiblingIndex(_bossNameText.transform.GetSiblingIndex());
            _bossHudGhost = CreateHudImage(parent, "Boss Damage Trail");
            _bossHudGhost.sprite = ProceduralSpriteFactory.Square();
            _bossHudGhost.color = new Color(.89f, .72f, .77f, .42f);
            _bossHudGhost.type = Image.Type.Filled;
            _bossHudGhost.fillMethod = Image.FillMethod.Horizontal;
            _bossHudGhost.transform.SetSiblingIndex(_bossBarFill.transform.GetSiblingIndex());
            _bossNameText.fontSize = 13;
            _bossNameText.color = new Color(.94f, .76f, .82f);
            _bossHealthText.fontSize = 12;
            _bossBarFill.color = new Color(.9f, .39f, .52f);

            _overclockHudRoot = UIBuilder.CreateRect(parent, "Unified Overclock Counter");
            PositionTop(_overclockHudRoot, 0, 142, 360, 65);
            _overclockHudGroup = UIBuilder.EnsureGroup(_overclockHudRoot.gameObject);
            _overclockHudGroup.blocksRaycasts = false;
            _overclockHudGroup.interactable = false;
            _overclockWordRow = UIBuilder.CreateRect(_overclockHudRoot, "Charged word");
            PositionTop(_overclockWordRow, 0, 0, 336, 37);
            _overclockWordClip = UIBuilder.CreateRect(_overclockWordRow, "Time remaining mask");
            _overclockWordClip.anchorMin = _overclockWordClip.anchorMax = _overclockWordClip.pivot = new Vector2(0, 1);
            _overclockWordClip.anchoredPosition = Vector2.zero;
            _overclockWordClip.sizeDelta = new Vector2(336, 37);
            _overclockWordClip.gameObject.AddComponent<RectMask2D>();
            ReparentOverclockText(_boostGhostA, _overclockWordRow, false);
            _boostGhostA.transform.SetAsFirstSibling();
            ReparentOverclockText(_boostGhostB, _overclockWordClip, true);
            ReparentOverclockText(_boostText, _overclockWordClip, true);
            _overclockOutline = _boostText.gameObject.AddComponent<Outline>();
            _overclockOutline.effectDistance = new Vector2(.55f, .55f);
            _overclockOutline.useGraphicAlpha = true;
            if (_boostText.font != null && _boostText.font.dynamic)
                _boostText.font.RequestCharactersInTexture("OVERCLOCKED ×0123456789", 64, FontStyle.Bold);
            _boostPanel.enabled = false;
            _boostIcon.enabled = false;
            _boostSecondsText.enabled = false;
            _overclockLineTrack = CreateHudImage(_overclockHudRoot, "Countdown Track");
            _overclockLineTrack.sprite = ProceduralSpriteFactory.Square();
            _overclockLineTrack.enabled = true;
            _overclockLineTrack.color = new Color(.16f, .20f, .26f, .65f);
            PositionTop(_overclockLineTrack.rectTransform, 0, 44, 336, 5);
            _overclockLineGlow = CreateHudImage(_overclockHudRoot, "Countdown Bloom");
            _overclockLineGlow.sprite = UISprites.Glow(128);
            _overclockLineGlow.type = Image.Type.Simple;
            _overclockLineGlow.enabled = true;
            PositionTop(_overclockLineGlow.rectTransform, 0, 34, 354, 26);
            _boostBar.transform.SetParent(_overclockHudRoot, false);
            PositionTop(_boostBar.rectTransform, 0, 44, 336, 5);
            _boostBar.type = Image.Type.Filled;
            _boostBar.fillMethod = Image.FillMethod.Horizontal;
            _boostBar.fillOrigin = 0;
            _overclockHudRoot.gameObject.SetActive(false);
            _bossHudPanel.enabled = _bossHudGhost.enabled = false;
        }

        private static void ReparentOverclockText(Text label, Transform parent, bool leftAnchored)
        {
            label.transform.SetParent(parent, false);
            PositionTop(label.rectTransform, 0, 0, 336, 37);
            if (leftAnchored)
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(0, 1);
            // Rasterize above the normal display size so stacked text stays sharp.
            label.fontSize = 64;
            label.rectTransform.sizeDelta = new Vector2(336, 37) / OverclockFontRenderScale;
            label.rectTransform.localScale = Vector3.one * OverclockFontRenderScale;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.raycastTarget = false;
        }

        private static void PositionTop(RectTransform rect, float x, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(x, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        private void RegisterOverclockPickup(int previousStreak)
        {
            _overclockPickupPending = true;
            _overclockNewActivationPending |= previousStreak == 0;
            _musicPerimeter?.NotifyPickup(previousStreak == 0);
        }

        private void ResetOverclockPresentation()
        {
            _overclockWasActive = _overclockPickupPending = _overclockNewActivationPending = false;
            _overclockShownStack = 0;
            _overclockHudBottom = 0;
            if (_overclockHudRoot != null) _overclockHudRoot.gameObject.SetActive(false);
        }

        private void UpdateBossHudRemaster(int activeBossCount, float fraction)
        {
            if (_bossHudPanel == null) return;
            var visible = activeBossCount > 0;
            _bossHudPanel.enabled = _bossHudGhost.enabled = visible;
            _bossHudBottom = 0;
            if (!visible) { _lastBossFraction = -1; return; }
            var top = _arenaBannerPanel != null && _arenaBannerPanel.enabled ? 162f : 97f;
            PositionTop(_bossHudPanel.rectTransform, 0, top, 472, 48);
            PositionTop(_bossNameText.rectTransform, -110, top + 7, 220, 18);
            PositionTop(_bossHealthText.rectTransform, 110, top + 7, 220, 18);
            PositionTop(_bossBarBackground.rectTransform, 0, top + 30, 448, 9);
            PositionTop(_bossHudGhost.rectTransform, 0, top + 30, 448, 9);
            PositionTop(_bossBarFill.rectTransform, 0, top + 30, 448, 9);
            _bossBarFill.type = Image.Type.Filled;
            _bossBarFill.fillMethod = Image.FillMethod.Horizontal;
            _bossBarFill.fillOrigin = 0;
            _bossBarFill.fillAmount = fraction;
            if (_lastBossFraction < 0 || fraction > _lastBossFraction) _bossGhostFraction = fraction;
            _bossGhostFraction = Mathf.Lerp(_bossGhostFraction, fraction, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 3.2f));
            _bossHudGhost.fillAmount = _bossGhostFraction;
            _lastBossFraction = fraction;
            _bossHudBottom = top + 48;
        }

        private void UpdateUnifiedOverclockHud()
        {
            if (_overclockHudRoot == null) return;
            var active = _overclock.Active && !_gameOver && !_mainMenuBrowsing;
            var visible = active && !_revivePending && !_rouletteActive && !_prizeRevealActive && _menuPage == MenuPage.None;
            _overclockHudRoot.gameObject.SetActive(visible);
            _overclockHudBottom = 0;
            if (!active)
            {
                _overclockWasActive = false;
                _overclockShownStack = 0;
                _overclockPickupPending = _overclockNewActivationPending = false;
                return;
            }
            var stack = _overclock.Streak;
            var fresh = !_overclockWasActive || _overclockNewActivationPending || stack < _overclockShownStack;
            var pickup = fresh || _overclockPickupPending || stack > _overclockShownStack;
            if (fresh) { _overclockEntryAge = 0; _overclockDisplayedFraction = 1; }
            if (pickup) _overclockRefillAge = 0;
            if (stack != _overclockShownStack)
            {
                var text = "OVERCLOCKED ×" + stack;
                _boostText.text = _boostGhostA.text = _boostGhostB.text = text;
                _overclockGlyphWidth = Mathf.Min(336, _boostText.preferredWidth * OverclockFontRenderScale);
                _overclockShownStack = stack;
            }
            _overclockWasActive = true;
            _overclockPickupPending = _overclockNewActivationPending = false;
            var dt = Time.unscaledDeltaTime;
            var canvasScale = Mathf.Max(.01f, _canvas.scaleFactor);
            var fit = Mathf.Max(.2f, (Screen.width / canvasScale - 48) / (360f * 1.13f));
            var scale = Mathf.Min(OverclockPresentationRules.StackScale(stack), fit);
            _overclockEntryAge += dt;
            _overclockRefillAge += dt;
            var fraction = OverclockPresentationRules.ChargeFraction(_overclock.RemainingSeconds, stack);
            _overclockDisplayedFraction = Mathf.Lerp(_overclockDisplayedFraction, fraction, 1 - Mathf.Exp(-dt * 18));
            var phase = OverclockCountdownColor(fraction);
            var analysis = _music != null ? _music.AnalysisFrame : MusicAnalysisFrame.Zero;
            var reduced = _saveData?.settings != null && _saveData.settings.reducedMotion;
            var rawBeat = reduced ? 0 : Mathf.Clamp01(analysis.Bass * .65f + analysis.Transient * .65f);
            var beat = rawBeat * OverclockPresentationRules.PulseGain(stack);
            var core = Color.Lerp(phase, Color.white, Mathf.Min(.78f, .12f + stack * .06f + beat * .12f));
            _boostText.enabled = _boostGhostA.enabled = _boostGhostB.enabled = _boostBar.enabled = true;
            _boostGhostA.color = new Color(.40f, .52f, .58f);
            _boostText.color = core;
            _boostGhostB.color = new Color(phase.r, phase.g, phase.b, Mathf.Min(.65f, .18f + stack * .05f + beat * .15f));
            _boostGhostB.rectTransform.localScale = Vector3.one * OverclockFontRenderScale * (1.006f + Mathf.Min(5, stack) * .001f);
            _overclockOutline.effectColor = new Color(phase.r, phase.g, phase.b, Mathf.Min(.85f, .35f + stack * .06f + beat * .15f));
            _boostBar.color = core;
            _boostBar.fillAmount = _overclockDisplayedFraction;
            var lineHeight = (4f + Mathf.Min(5, stack) * .65f) / Mathf.Max(.2f, scale);
            _boostBar.rectTransform.sizeDelta = _overclockLineTrack.rectTransform.sizeDelta = new Vector2(336, lineHeight);
            _overclockLineGlow.color = new Color(phase.r, phase.g, phase.b, Mathf.Min(.65f, .15f + stack * .045f + beat * .12f));
            var glowHeight = (12 + Mathf.Min(8, stack) * 2 + beat * 4) / scale;
            _overclockLineGlow.rectTransform.sizeDelta = new Vector2(336 * _overclockDisplayedFraction + 12 / scale, glowHeight);
            _overclockLineGlow.rectTransform.anchoredPosition = new Vector2(-168 * (1 - _overclockDisplayedFraction), -44 - lineHeight * .5f + glowHeight * .5f);
            // Drain the letters themselves, excluding the centred label's empty side margins.
            var wordPadding = (336 - _overclockGlyphWidth) * .5f;
            _overclockWordClip.sizeDelta = new Vector2(wordPadding + _overclockGlyphWidth * _overclockDisplayedFraction, 37);
            var top = _bossHudBottom > 0 ? _bossHudBottom + 26 : 142f;
            // Null City's own encounter overlay is owned by its separate implementation.
            if (CurrentVoidIsNullCity && _nullCityBossActive) top = Mathf.Max(top, 210);
            if (_arenaBannerPanel != null && _arenaBannerPanel.enabled) top = Mathf.Max(top, 178);
            top += Mathf.Max(0, Screen.height - Screen.safeArea.yMax) / canvasScale;
            var entry = reduced ? 1 : Mathf.Clamp01(_overclockEntryAge / .8f);
            var intro = 1 - Mathf.Pow(1 - entry, 3);
            var refill = reduced ? 0 : Mathf.Max(0, 1 - _overclockRefillAge / .35f);
            // Only constrain growth when the word would leave the physical viewport.
            _overclockHudRoot.localScale = Vector3.one * scale;
            _overclockHudRoot.anchoredPosition = new Vector2(0, -(top - (1 - intro) * 5));
            var pulse = OverclockPresentationRules.PulseScale(stack, rawBeat);
            _overclockWordRow.localScale = Vector3.one * Mathf.Min(1.13f, pulse + refill * .04f);
            _overclockHudGroup.alpha = Mathf.Clamp01(_overclockEntryAge / .12f);
            _overclockHudBottom = visible ? top + 65 * scale : 0;
        }

        private static Color OverclockCountdownColor(float fraction)
        {
            var ice = new Color(127 / 255f, 214 / 255f, 1);
            var violet = new Color(176 / 255f, 133 / 255f, 244 / 255f);
            var amber = new Color(242 / 255f, 181 / 255f, 112 / 255f);
            var ember = new Color(239 / 255f, 224 / 255f, 186 / 255f);
            return fraction >= .6f ? Color.Lerp(violet, ice, (fraction - .6f) / .4f)
                : fraction >= .25f ? Color.Lerp(amber, violet, (fraction - .25f) / .35f)
                : Color.Lerp(ember, amber, fraction / .25f);
        }
    }
}
