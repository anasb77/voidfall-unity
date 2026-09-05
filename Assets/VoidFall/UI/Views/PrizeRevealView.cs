using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VoidFall.Core;

namespace VoidFall.UI
{
    /// <summary>The single acceptance screen. The runtime has already applied this exact reward once.</summary>
    public sealed class PrizeRevealView : UIViewBase
    {
        private Text _title, _detail, _tierLabel, _sigil;
        private Image _cardFill;
        private RectTransform _content, _card;
        private Button _continueButton;
        private Action _onContinue;
        private float _revealElapsed;
        private Font _titleFont;

        protected override void Build()
        {
            UIBuilder.CreateScrim(Root, "Scrim", new Color(0.018f, 0.026f, 0.045f, 0.97f));
            _content = RouletteView.Place(Root, "Reveal", 0, 0, 1280, 800);
            RouletteView.Label(_content, "Kicker", "FORTUNE ANSWERS", 0, 345, 800, 22, 12, RouletteWheelGraphic.Gold);
            _titleFont = Font.CreateDynamicFontFromOSFont(new[] { "Georgia", "Times New Roman" }, 48);
            var heading = RouletteView.Label(_content, "Heading", "It belongs to you.", 0, 298, 1100, 65, 38, new Color(0.96f, 0.93f, 0.86f));
            heading.font = _titleFont;
            var wheel = RouletteView.Place(_content, "Relic silhouette", 0, -3, 610, 610);
            var silhouette = wheel.gameObject.AddComponent<RouletteWheelGraphic>();
            silhouette.Configure(RouletteRules.DefaultTable(), default);
            var group = UIBuilder.EnsureGroup(wheel.gameObject);
            group.alpha = 0.15f;
            _card = RouletteView.Place(_content, "Prize Card", 0, -10, 440, 462);
            _cardFill = UIBuilder.CreateSurface(_card, "Fill", UISprites.Rounded(2,
                new Color(0.09f, 0.1f, 0.15f), new Color(0.025f, 0.03f, 0.05f), new Color(0.5f, 0.42f, 0.28f)));
            UIBuilder.Stretch(_cardFill.rectTransform);
            RouletteView.Label(_card, "Card Kicker", "THE VOID YIELDS", 0, 184, 390, 24, 12, RouletteWheelGraphic.Gold);
            _sigil = RouletteView.Label(_card, "Sigil", "◆", 0, 102, 160, 100, 64, RouletteWheelGraphic.Gold);
            _tierLabel = RouletteView.Label(_card, "Tier", string.Empty, 0, 30, 390, 24, 12, RouletteWheelGraphic.Gold);
            _title = RouletteView.Label(_card, "Title", string.Empty, 0, -25, 392, 70, 28, Color.white);
            _title.font = _titleFont;
            _title.horizontalOverflow = HorizontalWrapMode.Wrap;
            _title.resizeTextForBestFit = true;
            _title.resizeTextMinSize = 20;
            _title.resizeTextMaxSize = 32;
            RouletteView.Surface(_card, "Divider", 0, -80, 338, 1, new Color(0.45f, 0.4f, 0.3f, 0.4f));
            _detail = RouletteView.Label(_card, "Detail", string.Empty, 0, -145, 356, 110, 16, new Color(0.74f, 0.78f, 0.84f));
            _detail.horizontalOverflow = HorizontalWrapMode.Wrap;
            _continueButton = RouletteView.MakeButton(_content, "Continue", 0, -320, 280, 62, true, OnContinue, out _);
        }

        public void Show(string title, string detail, RouletteTier tier, Action onContinue)
        {
            _onContinue = onContinue;
            _revealElapsed = 0;
            _title.text = title;
            _detail.text = detail;
            _tierLabel.text = tier == RouletteTier.Mediocre ? "COMMON" : tier.ToString().ToUpperInvariant();
            var accent = RouletteWheelGraphic.Accent(new RouletteWedgeDefinition(RoulettePrizeKind.RareBoon, tier, 1, "", "", ""));
            _tierLabel.color = _sigil.color = accent;
            _continueButton.interactable = false;
            SetVisible(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_continueButton.gameObject);
        }

        private void Update()
        {
            if (_card == null) return;
            _revealElapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(_revealElapsed / 0.55f);
            _card.localScale = Vector3.one * Mathf.Lerp(0.94f, 1, 1 - Mathf.Pow(1 - t, 3));
            _content.localScale = Vector3.one * Mathf.Max(0.1f, Mathf.Min(Root.rect.width / 1280f, Root.rect.height / 820f));
            // Do not let the same held submit that started the spin dismiss its reward.
            _continueButton.interactable = _revealElapsed >= 0.45f;
        }

        private void OnContinue()
        {
            if (_revealElapsed < 0.45f) return;
            var handler = _onContinue;
            _onContinue = null;
            SetVisible(false);
            handler?.Invoke();
        }

        private void OnDestroy()
        {
            if (_titleFont == null) return;
            if (Application.isPlaying) Destroy(_titleFont); else DestroyImmediate(_titleFont);
        }
    }
}
