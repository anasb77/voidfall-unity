using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VoidFall.UI
{
    /// <summary>One upgrade offer shown on the level-up screen.</summary>
    public struct UpgradeCardData
    {
        public string Title;
        public string Category;
        public string Description;
        public string LevelText;
        public Color AccentColor;

        /// <summary>Ranks already held, used to fill the pip row.</summary>
        public int CurrentRank;

        /// <summary>Total ranks available; zero hides the pip row.</summary>
        public int MaxRank;

        /// <summary>Applies the brighter evolution treatment.</summary>
        public bool IsEvolution;
    }

    /// <summary>
    /// The upgrade choice, rebuilt from .level-overlay / .upgrade-card.
    ///
    /// As in the stylesheet, the card that holds the three options carries no
    /// panel chrome of its own; only the dimmed backdrop and the three offers are
    /// visible. Each offer is tinted by its own accent through the same
    /// color-mix percentages the browser build uses.
    /// </summary>
    public sealed class LevelUpView : UIViewBase
    {
        private const float ContentWidth = 930f;
        private const float CardHeight = 268f;
        private const float CardGap = 16f;

        private RectTransform _grid;
        private Button _reroll;
        private Text _rerollLabel;
        private Action<int> _onSelect;

        protected override void Build()
        {
            UIBuilder.CreateScrim(Root, "Scrim", UITheme.OverlayScrim);

            var content = UIBuilder.CreateRect(Root, "Content");
            content.anchorMin = new Vector2(0.5f, 0.5f);
            content.anchorMax = new Vector2(0.5f, 0.5f);
            content.pivot = new Vector2(0.5f, 0.5f);
            content.sizeDelta = new Vector2(LevelUpContentWidth(), CardHeight + 168f);

            BuildHeader(content);

            _grid = UIBuilder.CreateRect(content, "Grid");
            _grid.anchorMin = new Vector2(0f, 1f);
            _grid.anchorMax = new Vector2(1f, 1f);
            _grid.pivot = new Vector2(0.5f, 1f);
            _grid.sizeDelta = new Vector2(0f, CardHeight);
            _grid.anchoredPosition = new Vector2(0f, -104f);

            var layout = UIBuilder.AddHorizontalLayout(_grid, CardGap, null, TextAnchor.UpperCenter);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            BuildRerollRow(content);
        }

        private static float LevelUpContentWidth()
        {
            if (Screen.width <= 0 || Screen.height <= 0) return ContentWidth;
            var safePixelWidth = Screen.safeArea.width > 0f ? Screen.safeArea.width : Screen.width;
            var referenceWidth = safePixelWidth * UITheme.ReferenceHeight / Screen.height;
            // Preserve the desktop composition, but keep the three-card row
            // inside the safe area on narrow or portrait displays.
            return Mathf.Min(ContentWidth, Mathf.Max(320f, referenceWidth - 32f));
        }

        private void BuildHeader(RectTransform parent)
        {
            var header = UIBuilder.CreateRect(parent, "Header");
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.sizeDelta = new Vector2(0f, 88f);

            var group = UIBuilder.EnsureGroup(header.gameObject);

            // The mint kicker with wide 0.5em tracking and a green bloom.
            var kicker = UIBuilder.CreateText(
                header,
                "Kicker",
                "LEVEL UP",
                11f,
                UITheme.GreenLight,
                TextAnchor.UpperCenter,
                true,
                FontStyle.Bold,
                0.5f);
            kicker.rectTransform.anchorMin = new Vector2(0f, 1f);
            kicker.rectTransform.anchorMax = new Vector2(1f, 1f);
            kicker.rectTransform.pivot = new Vector2(0.5f, 1f);
            kicker.rectTransform.sizeDelta = new Vector2(0f, 20f);

            var bloom = UIBuilder.CreateSurface(kicker.rectTransform, "Bloom", UISprites.Glow(192));
            bloom.type = Image.Type.Simple;
            UIBuilder.Stretch(bloom.rectTransform, -120f, -18f, -120f, -12f);
            bloom.color = UITheme.WithAlpha(UITheme.Green, 0.30f);
            bloom.rectTransform.SetAsFirstSibling();

            var title = UIBuilder.CreateText(
                header,
                "Title",
                "CHOOSE AN UPGRADE",
                40f,
                UITheme.TextHeading,
                TextAnchor.UpperCenter,
                true,
                FontStyle.Bold,
                0.18f);
            title.rectTransform.anchorMin = new Vector2(0f, 0f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.offsetMin = new Vector2(0f, 0f);
            title.rectTransform.offsetMax = new Vector2(0f, -30f);
            UIBuilder.FitText(title, 22f, 40f);

            var titleGlow = UIBuilder.CreateSurface(header, "TitleGlow", UISprites.Glow(256));
            titleGlow.type = Image.Type.Simple;
            titleGlow.rectTransform.anchorMin = new Vector2(0.5f, 0.35f);
            titleGlow.rectTransform.anchorMax = new Vector2(0.5f, 0.35f);
            titleGlow.rectTransform.sizeDelta = new Vector2(720f, 200f);
            titleGlow.color = UITheme.WithAlpha(UITheme.CyanLight, 0.14f);
            titleGlow.rectTransform.SetAsFirstSibling();

            header.gameObject.AddComponent<UIRiseIn>()
                .Bind(header, group, 0.48f, UITheme.PanelRiseOffset);
        }

        private void BuildRerollRow(RectTransform parent)
        {
            var row = UIBuilder.CreateRect(parent, "RerollRow");
            row.anchorMin = new Vector2(0.5f, 0f);
            row.anchorMax = new Vector2(0.5f, 0f);
            row.pivot = new Vector2(0.5f, 0f);
            row.sizeDelta = new Vector2(280f, 46f);
            row.anchoredPosition = new Vector2(0f, 0f);

            _reroll = UIBuilder.CreateSecondaryAction(
                row,
                "Reroll",
                "Reroll",
                null,
                () => Callbacks?.RerollUpgrades?.Invoke(),
                46f);
            UIBuilder.Stretch(_reroll.GetComponent<RectTransform>());
            _rerollLabel = _reroll.transform.Find("Label")?.GetComponent<Text>();
        }

        /// <summary>Retained signature used by the runtime.</summary>
        public void ShowUpgrades(IReadOnlyList<UpgradeCardData> cards, Action<int> onSelect)
        {
            ShowUpgrades(cards, -1, onSelect);
        }

        /// <summary>
        /// Rebuilds the offers. A negative reroll count hides the counter and
        /// leaves the button enabled.
        /// </summary>
        public void ShowUpgrades(IReadOnlyList<UpgradeCardData> cards, int rerollsRemaining, Action<int> onSelect)
        {
            _onSelect = onSelect;
            ClearChildren(_grid);

            var count = cards?.Count ?? 0;
            for (var index = 0; index < count; index++)
            {
                BuildCard(cards[index], index);
            }

            if (_rerollLabel != null)
            {
                _rerollLabel.text = rerollsRemaining < 0
                    ? "Reroll   [Q]"
                    : rerollsRemaining > 0
                        ? "Reroll (" + rerollsRemaining.ToString() + ")   [Q]"
                        : "Reroll used";
                _rerollLabel.color = rerollsRemaining == 0 ? UITheme.TextDisabled : UITheme.TextStrong;
            }
            if (_reroll != null) _reroll.interactable = rerollsRemaining != 0;

            SetVisible(true);
        }

        private void BuildCard(UpgradeCardData data, int index)
        {
            var accent = data.AccentColor.a <= 0f ? UITheme.CyanLight : data.AccentColor;
            var evolution = data.IsEvolution;

            var card = UIBuilder.CreateRect(_grid, "Card" + index.ToString());

            var group = UIBuilder.EnsureGroup(card.gameObject);
            // The card root is a child of the horizontal layout group. Keep its
            // position entirely under layout control and animate only this
            // stretched visual child; otherwise the entrance/hover scripts can
            // overwrite the layout group's position before its first rebuild.
            var visual = UIBuilder.Stretch(UIBuilder.CreateRect(card, "Visual"));

            var surface = card.gameObject.AddComponent<Image>();
            surface.type = Image.Type.Sliced;
            surface.raycastTarget = true;

            // border: color-mix(accent 27%, #334155); hover mixes to 72% toward #cbd5e1.
            var rest = UISprites.Rounded(
                UITheme.RadiusPanel,
                evolution ? UITheme.EvolutionCardTop : UITheme.UpgradeCardTop,
                evolution ? UITheme.EvolutionCardBottom : UITheme.UpgradeCardBottom,
                evolution
                    ? UITheme.Mix(accent, 68f, UITheme.TextBrightest)
                    : UITheme.Mix(accent, 27f, UITheme.Hex("#334155")),
                1f,
                UITheme.CardGradientAngle,
                true);
            var hover = UISprites.Rounded(
                UITheme.RadiusPanel,
                evolution ? UITheme.EvolutionCardTop : UITheme.UpgradeCardTop,
                evolution ? UITheme.EvolutionCardBottom : UITheme.UpgradeCardBottom,
                UITheme.Mix(accent, 72f, UITheme.TextChip),
                1.5f,
                UITheme.CardGradientAngle,
                true);
            surface.sprite = rest;

            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = surface;
            button.transition = Selectable.Transition.SpriteSwap;
            var spriteState = button.spriteState;
            spriteState.highlightedSprite = hover;
            spriteState.selectedSprite = hover;
            spriteState.pressedSprite = hover;
            button.spriteState = spriteState;

            var captured = index;
            button.onClick.AddListener(() =>
            {
                // Hide first: the runtime clears its level-up state synchronously,
                // and leaving the card visible for a frame reads as a stuck click.
                SetVisible(false);
                _onSelect?.Invoke(captured);
            });

            // 0 0 34px color-mix(accent 22%) on hover, always-on at lower alpha.
            var glow = UIBuilder.CreateSurface(visual, "Glow", UISprites.Glow(256));
            glow.type = Image.Type.Simple;
            UIBuilder.Stretch(glow.rectTransform, -26f);
            glow.color = UITheme.MixTransparent(accent, evolution ? 22f : 12f);
            glow.rectTransform.SetAsFirstSibling();

            // ::before - the short accent tick, widened into a fading bar on
            // evolution cards.
            var tick = UIBuilder.CreateFill(visual, "Tick", accent);
            if (evolution)
            {
                tick.rectTransform.anchorMin = new Vector2(0.08f, 1f);
                tick.rectTransform.anchorMax = new Vector2(0.92f, 1f);
                tick.rectTransform.sizeDelta = new Vector2(0f, 3f);
            }
            else
            {
                tick.rectTransform.anchorMin = new Vector2(0.10f, 1f);
                tick.rectTransform.anchorMax = new Vector2(0.42f, 1f);
                tick.rectTransform.sizeDelta = new Vector2(0f, 2f);
            }
            tick.rectTransform.pivot = new Vector2(0.5f, 1f);
            tick.rectTransform.anchoredPosition = Vector2.zero;

            if (evolution)
            {
                // inset 4px 0 0 var(--accent)
                var rail = UIBuilder.CreateFill(visual, "Rail", accent);
                rail.rectTransform.anchorMin = new Vector2(0f, 0f);
                rail.rectTransform.anchorMax = new Vector2(0f, 1f);
                rail.rectTransform.pivot = new Vector2(0f, 0.5f);
                rail.rectTransform.sizeDelta = new Vector2(4f, -24f);
                rail.rectTransform.anchoredPosition = new Vector2(1f, 0f);
            }

            BuildCardIcon(visual, accent, evolution);

            var indexBadge = UIBuilder.CreateText(
                visual,
                "Index",
                (index + 1).ToString(),
                10f,
                UITheme.TextIndex,
                TextAnchor.UpperRight,
                true,
                FontStyle.Bold);
            indexBadge.rectTransform.offsetMin = new Vector2(0f, 0f);
            indexBadge.rectTransform.offsetMax = new Vector2(-11f, -10f);

            var kickerText = string.IsNullOrEmpty(data.LevelText) ? data.Category : data.LevelText;
            var kicker = UIBuilder.CreateText(
                visual,
                "Kicker",
                (kickerText ?? string.Empty).ToUpperInvariant(),
                10f,
                accent,
                TextAnchor.UpperCenter,
                true,
                FontStyle.Bold,
                0.09f);
            kicker.rectTransform.anchorMin = new Vector2(0f, 1f);
            kicker.rectTransform.anchorMax = new Vector2(1f, 1f);
            kicker.rectTransform.pivot = new Vector2(0.5f, 1f);
            kicker.rectTransform.sizeDelta = new Vector2(-32f, 14f);
            kicker.rectTransform.anchoredPosition = new Vector2(0f, -92f);

            var title = UIBuilder.CreateText(
                visual,
                "Title",
                data.Title,
                18f,
                UITheme.TextBody,
                TextAnchor.UpperCenter,
                true,
                FontStyle.Bold);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.sizeDelta = new Vector2(-32f, 26f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -112f);
            title.horizontalOverflow = HorizontalWrapMode.Wrap;

            var description = UIBuilder.CreateParagraph(
                visual,
                "Description",
                data.Description,
                12.5f,
                UITheme.TextDescription,
                TextAnchor.UpperCenter);
            description.rectTransform.anchorMin = new Vector2(0f, 0f);
            description.rectTransform.anchorMax = new Vector2(1f, 1f);
            description.rectTransform.offsetMin = new Vector2(20f, 44f);
            description.rectTransform.offsetMax = new Vector2(-20f, -146f);

            if (data.MaxRank > 0)
            {
                var pipHost = UIBuilder.CreateRect(visual, "Ranks");
                pipHost.anchorMin = new Vector2(0.5f, 0f);
                pipHost.anchorMax = new Vector2(0.5f, 0f);
                pipHost.pivot = new Vector2(0.5f, 0f);
                pipHost.sizeDelta = new Vector2(0f, 12f);
                pipHost.anchoredPosition = new Vector2(0f, 18f);

                var pips = UIBuilder.CreateRankPips(
                    pipHost,
                    "PipRow",
                    data.MaxRank,
                    data.CurrentRank,
                    26f,
                    accent);
                var pipRow = pipHost.Find("PipRow") as RectTransform;
                if (pipRow != null)
                {
                    pipRow.anchorMin = new Vector2(0.5f, 0.5f);
                    pipRow.anchorMax = new Vector2(0.5f, 0.5f);
                    pipRow.pivot = new Vector2(0.5f, 0.5f);
                    var width = pips.Length * 26f + Mathf.Max(0, pips.Length - 1) * 4f;
                    pipRow.sizeDelta = new Vector2(width, 12f);
                }
            }

            var lift = card.gameObject.AddComponent<UIHoverLift>();
            lift.Bind(visual, 6f);

            // card-rise-in with a 70ms per-card stagger.
            card.gameObject.AddComponent<UIRiseIn>().Bind(
                visual,
                group,
                UITheme.CardRiseSeconds,
                UITheme.CardRiseOffset,
                index * UITheme.CardRiseStagger,
                0.94f);
        }

        /// <summary>
        /// The circular .upgrade-icon frame. Evolution cards use the stylesheet's
        /// asymmetric 12px/50% corner shape, approximated here by rotating a
        /// rounded square, with the glyph counter-rotated to stay upright.
        /// </summary>
        private static void BuildCardIcon(RectTransform card, Color accent, bool evolution)
        {
            var frame = UIBuilder.CreateRect(card, "IconFrame");
            frame.anchorMin = new Vector2(0.5f, 1f);
            frame.anchorMax = new Vector2(0.5f, 1f);
            frame.pivot = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = new Vector2(56f, 56f);
            frame.anchoredPosition = new Vector2(0f, -50f);

            var image = frame.gameObject.AddComponent<Image>();
            if (evolution)
            {
                image.sprite = UISprites.Rounded(
                    14f,
                    UITheme.MixTransparent(accent, 11f),
                    UITheme.MixTransparent(accent, 11f),
                    UITheme.MixTransparent(accent, 44f));
                image.type = Image.Type.Sliced;
                frame.localRotation = Quaternion.Euler(0f, 0f, -8f);
            }
            else
            {
                image.sprite = UISprites.Circle(96);
                image.color = UITheme.MixTransparent(accent, 18f);
            }
            image.raycastTarget = false;

            // box-shadow: 0 0 20px color-mix(accent 25%)
            var glow = UIBuilder.CreateSurface(frame, "Glow", UISprites.Glow(128));
            glow.type = Image.Type.Simple;
            UIBuilder.Stretch(glow.rectTransform, -16f);
            glow.color = UITheme.MixTransparent(accent, 25f);
            glow.rectTransform.SetAsFirstSibling();

            if (!evolution)
            {
                var ring = UIBuilder.CreateRect(frame, "Ring");
                UIBuilder.Stretch(ring);
                var ringImage = ring.gameObject.AddComponent<Image>();
                ringImage.sprite = UISprites.Circle(96);
                ringImage.color = UITheme.MixTransparent(accent, 44f);
                ringImage.raycastTarget = false;
                ring.SetAsFirstSibling();

                var innerMask = UIBuilder.CreateRect(frame, "Inner");
                UIBuilder.Stretch(innerMask, 1.5f);
                var innerImage = innerMask.gameObject.AddComponent<Image>();
                innerImage.sprite = UISprites.Circle(96);
                innerImage.color = UITheme.MixTransparent(accent, 11f);
                innerImage.raycastTarget = false;
            }

            var glyph = UIBuilder.CreateText(
                frame,
                "Glyph",
                evolution ? "\u25C7" : "\u25CF",
                evolution ? 22f : 15f,
                accent,
                TextAnchor.MiddleCenter,
                true,
                FontStyle.Bold);
            if (evolution) glyph.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 8f);
        }
    }
}
