using UnityEngine;
using UnityEngine.UI;

namespace VoidFall.UI
{
    /// <summary>
    /// The weapon evolution splash, rebuilt from .evolution-reveal.
    ///
    /// The distinctive parts of the original are all here: the rotated
    /// diamond-cornered badge with its counter-rotated glyph, the two accent
    /// crosslines that read as striking through the type, and the scale envelope
    /// that punches to 1.04 before settling.
    /// </summary>
    public sealed class EvolutionRevealView : UIViewBase
    {
        private RectTransform _stage;
        private CanvasGroup _group;
        private Image _crossHorizontal;
        private Image _crossVertical;
        private Image _badge;
        private Image _badgeHalo;
        private Text _badgeGlyph;
        private Text _kicker;
        private Text _previous;
        private Image _previousStrike;
        private Text _title;
        private float _duration;
        private float _elapsed;
        private bool _playing;

        protected override void Build()
        {
            _stage = UIBuilder.CreateRect(Root, "Stage");
            _stage.anchorMin = new Vector2(0.5f, 0.5f);
            _stage.anchorMax = new Vector2(0.5f, 0.5f);
            _stage.pivot = new Vector2(0.5f, 0.5f);
            _stage.sizeDelta = new Vector2(900f, 420f);
            _group = UIBuilder.EnsureGroup(_stage.gameObject);
            _group.blocksRaycasts = false;
            _group.interactable = false;

            // ::before and ::after - a wide horizontal rule and a shorter vertical
            // one, both accent gradients fading at each end.
            _crossHorizontal = UIBuilder.CreateFill(_stage, "CrossH", UITheme.CyanLight);
            _crossHorizontal.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _crossHorizontal.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _crossHorizontal.rectTransform.sizeDelta = new Vector2(620f, 1f);

            _crossVertical = UIBuilder.CreateFill(_stage, "CrossV", UITheme.CyanLight);
            _crossVertical.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _crossVertical.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _crossVertical.rectTransform.sizeDelta = new Vector2(1f, 330f);

            BuildBadge();

            _kicker = UIBuilder.CreateText(
                _stage,
                "Kicker",
                "WEAPON EVOLVED",
                10f,
                UITheme.CyanLight,
                TextAnchor.MiddleCenter,
                true,
                FontStyle.Bold,
                0.32f);
            _kicker.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _kicker.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _kicker.rectTransform.sizeDelta = new Vector2(640f, 26f);
            _kicker.rectTransform.anchoredPosition = new Vector2(0f, -22f);
            AddPlate(_kicker.rectTransform);

            _previous = UIBuilder.CreateText(
                _stage,
                "Previous",
                string.Empty,
                11f,
                UITheme.TextSubtle,
                TextAnchor.MiddleCenter,
                true,
                FontStyle.Bold,
                0.16f);
            _previous.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _previous.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _previous.rectTransform.sizeDelta = new Vector2(640f, 24f);
            _previous.rectTransform.anchoredPosition = new Vector2(0f, -50f);
            AddPlate(_previous.rectTransform);

            // The stylesheet strikes this line through with text-decoration. The
            // legacy text component has no decorations, so the rule is drawn as a
            // thin bar sized to the label in ShowEvolution.
            _previousStrike = UIBuilder.CreateFill(
                _previous.rectTransform,
                "Strike",
                UITheme.WithAlpha(UITheme.TextSubtle, 0.85f));
            _previousStrike.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _previousStrike.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _previousStrike.rectTransform.sizeDelta = new Vector2(0f, 1f);

            _title = UIBuilder.CreateText(
                _stage,
                "Title",
                string.Empty,
                52f,
                UITheme.TextBrightest,
                TextAnchor.MiddleCenter,
                true,
                FontStyle.Bold,
                0.12f);
            _title.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _title.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _title.rectTransform.sizeDelta = new Vector2(760f, 64f);
            _title.rectTransform.anchoredPosition = new Vector2(0f, -92f);
            UIBuilder.FitText(_title, 24f, 52f);
            AddPlate(_title.rectTransform);
        }

        private void BuildBadge()
        {
            var frame = UIBuilder.CreateRect(_stage, "Badge");
            frame.anchorMin = new Vector2(0.5f, 0.5f);
            frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.pivot = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = new Vector2(78f, 78f);
            frame.anchoredPosition = new Vector2(0f, 62f);
            frame.localRotation = Quaternion.Euler(0f, 0f, -8f);

            // 0 0 36px color-mix(accent 32%) plus the 8px accent ring at 8%.
            _badgeHalo = UIBuilder.CreateSurface(frame, "Halo", UISprites.Glow(192));
            _badgeHalo.type = Image.Type.Simple;
            UIBuilder.Stretch(_badgeHalo.rectTransform, -46f);
            _badgeHalo.color = UITheme.WithAlpha(UITheme.CyanLight, 0.32f);

            _badge = UIBuilder.CreateSurface(frame, "Body", UISprites.Rounded(
                16f,
                UITheme.RevealPlate,
                UITheme.RevealPlate,
                UITheme.CyanLight));
            UIBuilder.Stretch(_badge.rectTransform);

            _badgeGlyph = UIBuilder.CreateText(
                _badge.rectTransform,
                "Glyph",
                "\u25C7",
                30f,
                UITheme.CyanLight,
                TextAnchor.MiddleCenter,
                true,
                FontStyle.Bold);
            _badgeGlyph.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 8f);
        }

        /// <summary>
        /// The translucent plate behind each line, which is what makes the
        /// crosslines read as struck through rather than crossing the glyphs.
        /// </summary>
        private static void AddPlate(RectTransform target)
        {
            var plate = UIBuilder.CreateFill(target, "Plate", UITheme.RevealPlate);
            UIBuilder.Stretch(plate.rectTransform);
            plate.rectTransform.SetAsFirstSibling();
        }

        /// <summary>Retained signature used by the runtime.</summary>
        public void ShowEvolution(string name, string detail, float duration)
        {
            ShowEvolution(name, detail, UITheme.CyanLight, duration);
        }

        /// <summary>
        /// Plays the reveal. The accent tints the badge, crosslines and kicker the
        /// way --evolution-accent does in the stylesheet.
        /// </summary>
        public void ShowEvolution(string name, string previousName, Color accent, float duration)
        {
            if (accent.a <= 0f) accent = UITheme.CyanLight;

            UIBuilder.SetText(_title, (name ?? string.Empty).ToUpperInvariant());
            if (_previous != null)
            {
                UIBuilder.SetText(_previous, (previousName ?? string.Empty).ToUpperInvariant());
                _previous.gameObject.SetActive(!string.IsNullOrEmpty(previousName));

                if (_previousStrike != null)
                {
                    // Size the rule to the rendered line so it reads as a strike
                    // rather than a divider spanning the whole stage.
                    var width = _previous.preferredWidth + 8f;
                    _previousStrike.rectTransform.sizeDelta = new Vector2(width, 1f);
                    _previousStrike.color = UITheme.WithAlpha(UITheme.TextSubtle, 0.85f);
                }
            }
            if (_kicker != null) _kicker.color = accent;
            if (_badge != null)
            {
                _badge.sprite = UISprites.Rounded(16f, UITheme.RevealPlate, UITheme.RevealPlate, accent);
            }
            if (_badgeGlyph != null) _badgeGlyph.color = accent;
            if (_badgeHalo != null) _badgeHalo.color = UITheme.MixTransparent(accent, 32f);
            if (_crossHorizontal != null) _crossHorizontal.color = UITheme.WithAlpha(accent, 0.7f);
            if (_crossVertical != null) _crossVertical.color = UITheme.WithAlpha(accent, 0.28f);
            if (_title != null)
            {
                _title.color = UITheme.TextBrightest;
            }

            _duration = Mathf.Max(0.4f, duration);
            _elapsed = 0f;
            _playing = true;
            gameObject.SetActive(true);
            Apply(0f);
        }

        private void Update()
        {
            if (!_playing) return;
            _elapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(_elapsed / _duration);
            Apply(t);
            if (t < 1f) return;
            _playing = false;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// The @keyframes evolution-reveal envelope: fade and scale up to 1.04 by
        /// 13%, hold at rest until 78%, then fade out slightly larger.
        /// </summary>
        private void Apply(float t)
        {
            const float rest = 0.7f;
            float alpha;
            float scale;

            if (t < 0.13f)
            {
                var k = t / 0.13f;
                alpha = k;
                scale = Mathf.Lerp(0.82f, 1.04f, k) * rest;
            }
            else if (t < 0.22f)
            {
                var k = (t - 0.13f) / 0.09f;
                alpha = 1f;
                scale = Mathf.Lerp(1.04f, 1f, k) * rest;
            }
            else if (t < 0.78f)
            {
                alpha = 1f;
                scale = rest;
            }
            else
            {
                var k = (t - 0.78f) / 0.22f;
                alpha = 1f - k;
                scale = Mathf.Lerp(1f, 1.02f, k) * rest;
            }

            if (_group != null) _group.alpha = alpha;
            if (_stage != null) _stage.localScale = Vector3.one * scale;
        }

        public override void SetVisible(bool visible)
        {
            // Driven by its own timer rather than screen state; hiding is handled
            // when the envelope completes.
            if (!visible)
            {
                _playing = false;
                gameObject.SetActive(false);
            }
        }
    }
}
