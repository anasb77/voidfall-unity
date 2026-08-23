using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VoidFall.Core;

namespace VoidFall.UI
{
    /// <summary>
    /// The route-selection overlay (spec §6, §30): the branch choice after a
    /// Void's objective completes. Cards are projected by
    /// RouteSelectController; this view only renders them and reports the
    /// confirmed void id - all reveal/locking rules stay in the route run.
    /// </summary>
    public sealed class RouteSelectView : UIViewBase
    {
        private const float CardWidth = 300f;
        private const float CardHeight = 420f;
        private const float CardGap = 24f;

        private readonly List<CardWidgets> _cards = new List<CardWidgets>();

        private Text _bannerText;
        private Text _routeLineText;
        private Button _enterButton;
        private Text _enterLabel;
        private Action<string> _onEnter;
        private string _focusedId;

        private sealed class CardWidgets
        {
            public string Id;
            public bool Selectable;
            public RectTransform Root;
            public Image Border;
            public Image Fill;
            public Button Button;
            public Text Name;
            public Text Threat;
            public Text Description;
            public Text Objective;
            public Text Reward;
            public Text State;
        }

        protected override void Build()
        {
            var scrim = UIBuilder.CreateScrim(Root, "Scrim", UITheme.OverlayScrim);
            scrim.raycastTarget = true;

            var content = UIBuilder.CreateRect(Root, "Content");
            content.anchorMin = new Vector2(0.5f, 0.5f);
            content.anchorMax = new Vector2(0.5f, 0.5f);
            content.pivot = new Vector2(0.5f, 0.5f);
            content.sizeDelta = new Vector2(1020f, 660f);

            _bannerText = UIBuilder.CreateText(
                content, "Banner", string.Empty, 24f,
                Color.white, TextAnchor.UpperCenter, true, FontStyle.Bold);
            _bannerText.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            _bannerText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _bannerText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _bannerText.rectTransform.anchoredPosition = new Vector2(0f, -18f);
            _bannerText.rectTransform.sizeDelta = new Vector2(1000f, 34f);

            _routeLineText = UIBuilder.CreateText(
                content, "RouteLine", string.Empty, 11f,
                UITheme.CyanPale, TextAnchor.UpperCenter, true, FontStyle.Normal, 0.2f);
            _routeLineText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            _routeLineText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            _routeLineText.rectTransform.pivot = new Vector2(0.5f, 0f);
            _routeLineText.rectTransform.anchoredPosition = new Vector2(0f, 62f);
            _routeLineText.rectTransform.sizeDelta = new Vector2(1000f, 18f);

            _enterButton = UIBuilder.CreatePrimaryAction(
                content, "EnterButton", "ENTER THE VOID", string.Empty, EnterFocused, 46f);
            var enterRect = _enterButton.GetComponent<RectTransform>();
            enterRect.anchorMin = new Vector2(0.5f, 0f);
            enterRect.anchorMax = new Vector2(0.5f, 0f);
            enterRect.pivot = new Vector2(0.5f, 0f);
            enterRect.anchoredPosition = new Vector2(0f, 8f);
            _enterLabel = _enterButton.GetComponentInChildren<Text>();

            var group = UIBuilder.EnsureGroup(gameObject);
            group.blocksRaycasts = true;

            HideAll();
        }

        /// <summary>
        /// Presents the pending choice. Cards are given in focus order; the
        /// first selectable card starts focused.
        /// </summary>
        public void Show(
            IReadOnlyList<RouteCardData> cards,
            string banner,
            string routeLine,
            Action<string> onEnter)
        {
            _onEnter = onEnter;
            _bannerText.text = banner;
            _routeLineText.text = routeLine;
            _focusedId = null;

            while (_cards.Count < cards.Count)
            {
                var card = BuildCard(_cards.Count, cards.Count);
                _cards.Add(card);
            }

            for (var index = 0; index < _cards.Count; index++)
            {
                var card = _cards[index];
                var visible = index < cards.Count;
                card.Root.gameObject.SetActive(visible);
                if (!visible) continue;
                ApplyCard(card, cards[index]);
                if (card.Selectable && _focusedId == null) _focusedId = card.Id;
            }
            RefreshFocus();
            SetVisible(true);
        }

        private CardWidgets BuildCard(int index, int total)
        {
            var root = UIBuilder.CreateRect(Root, "Card " + index);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(CardWidth, CardHeight);
            var rowWidth = total * CardWidth + (total - 1) * CardGap;
            root.anchoredPosition = new Vector2(
                -rowWidth / 2 + CardWidth / 2 + index * (CardWidth + CardGap), -6f);

            var fill = UIBuilder.CreateSurface(
                root, "Fill", UISprites.Rounded(
                    UITheme.RadiusCard, UITheme.PreviewFill, UITheme.PreviewFill,
                    UITheme.BorderPreview));
            UIBuilder.Stretch(fill.rectTransform);
            fill.color = UITheme.WithAlpha(new Color(0.04f, 0.05f, 0.09f), 0.94f);
            fill.raycastTarget = true;

            var border = root.gameObject.AddComponent<Image>();
            border.sprite = UISprites.Rounded(
                UITheme.RadiusCard, Color.clear, Color.clear, UITheme.Cyan);
            border.type = Image.Type.Sliced;
            border.color = UITheme.WithAlpha(UITheme.Cyan, 0.28f);
            border.raycastTarget = false;
            UIBuilder.Stretch(border.rectTransform);

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.None;

            var widgets = new CardWidgets
            {
                Id = null,
                Root = root,
                Border = border,
                Fill = fill,
                Button = button,
            };

            Text Label(string name, string initial, float size, Color color,
                TextAnchor anchor, float y, float height, FontStyle style = FontStyle.Normal)
            {
                var text = UIBuilder.CreateText(
                    root, name, initial, size, color, anchor, true, style, 0.18f);
                text.rectTransform.anchorMin = new Vector2(0f, 1f);
                text.rectTransform.anchorMax = new Vector2(1f, 1f);
                text.rectTransform.pivot = new Vector2(0.5f, 1f);
                text.rectTransform.anchoredPosition = new Vector2(0f, -y);
                text.rectTransform.sizeDelta = new Vector2(-28f, height);
                return text;
            }

            widgets.Threat = Label("Threat", string.Empty, 11f, UITheme.CyanPale,
                TextAnchor.UpperCenter, 14f, 18f, FontStyle.Bold);
            widgets.Name = Label("Name", string.Empty, 22f, Color.white,
                TextAnchor.UpperCenter, 38f, 30f, FontStyle.Bold);
            widgets.Description = Label("Description", string.Empty, 12f,
                UITheme.WithAlpha(Color.white, 0.72f), TextAnchor.UpperLeft, 84f, 108f);
            widgets.Objective = Label("Objective", string.Empty, 12f,
                UITheme.WithAlpha(Color.white, 0.92f), TextAnchor.UpperLeft, 212f, 84f);
            widgets.Reward = Label("Reward", string.Empty, 12f, UITheme.CyanPale,
                TextAnchor.UpperLeft, 306f, 62f);
            widgets.State = Label("State", string.Empty, 11f,
                UITheme.WithAlpha(Color.white, 0.55f), TextAnchor.UpperCenter, 380f, 18f);
            return widgets;
        }

        private void ApplyCard(CardWidgets card, RouteCardData data)
        {
            card.Id = data.Id;
            card.Selectable = data.Selectable;
            card.Name.text = data.DisplayName.ToUpperInvariant();
            card.Threat.text = data.ThreatLabel + " - x" +
                data.ThreatMultiplier.ToString("0.00");
            card.Description.text = data.Description;
            card.Objective.text = "OBJECTIVE\n" + data.ObjectiveSummary;
            card.Reward.text = "REWARD\n" + data.RewardSummary;
            card.State.text = data.Selectable
                ? "AVAILABLE"
                : data.StateLabel;
            card.Button.onClick.RemoveAllListeners();
            card.Button.onClick.AddListener(() =>
            {
                if (card.Selectable)
                {
                    _focusedId = card.Id;
                    RefreshFocus();
                }
            });
        }

        private void RefreshFocus()
        {
            foreach (var card in _cards)
            {
                var focused = card.Id != null && card.Id == _focusedId;
                var color = !card.Selectable
                    ? UITheme.WithAlpha(Color.white, 0.35f)
                    : focused ? Color.white : UITheme.WithAlpha(Color.white, 0.8f);
                card.Name.color = color;
                card.Border.color = card.Selectable
                    ? UITheme.WithAlpha(UITheme.Cyan, focused ? 0.85f : 0.28f)
                    : UITheme.WithAlpha(Color.white, 0.12f);
                card.Root.localScale = new Vector3(1f, 1f, 1f);
            }
            if (_enterButton != null)
                _enterButton.interactable = _focusedId != null;
        }

        private void EnterFocused()
        {
            if (_focusedId == null) return;
            var handler = _onEnter;
            var id = _focusedId;
            _onEnter = null;
            SetVisible(false);
            handler?.Invoke(id);
        }

        private void HideAll()
        {
            foreach (var card in _cards) card.Root.gameObject.SetActive(false);
            _bannerText.text = string.Empty;
            _routeLineText.text = string.Empty;
            SetVisible(false);
        }
    }
}
