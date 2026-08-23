using System;
using UnityEngine;
using UnityEngine.UI;
using VoidFall.Core;

namespace VoidFall.UI
{
    /// <summary>
    /// The single-card prize reveal: after the roulette lands, the won prize
    /// is presented as one full card - the same visual language as the
    /// level-up choice, but with exactly one option. No toast popups: this
    /// screen IS the announcement.
    /// </summary>
    public sealed class PrizeRevealView : UIViewBase
    {
        private Text _kicker;
        private Text _title;
        private Text _detail;
        private Text _tierLabel;
        private Image _cardFill;
        private Button _continueButton;
        private Action _onContinue;
        private float _revealElapsed;

        protected override void Build()
        {
            var scrim = UIBuilder.CreateScrim(Root, "Scrim", UITheme.OverlayScrim);
            scrim.raycastTarget = true;

            var card = UIBuilder.CreateRect(Root, "Prize Card");
            card.anchorMin = new Vector2(0.5f, 0.5f);
            card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(430f, 560f);

            _cardFill = UIBuilder.CreateSurface(
                card, "Fill", UISprites.Rounded(
                    UITheme.RadiusCard, UITheme.PreviewFill, UITheme.PreviewFill,
                    UITheme.BorderPreview));
            UIBuilder.Stretch(_cardFill.rectTransform);
            _cardFill.color = new Color(0.05f, 0.07f, 0.12f, 0.97f);
            _cardFill.raycastTarget = true;

            _kicker = UIBuilder.CreateText(
                card, "Kicker", "THE VOID YIELDS", 13f,
                UITheme.CyanPale, TextAnchor.UpperCenter, true, FontStyle.Bold, 0.3f);
            _kicker.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            _kicker.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _kicker.rectTransform.pivot = new Vector2(0.5f, 1f);
            _kicker.rectTransform.anchoredPosition = new Vector2(0f, -34f);
            _kicker.rectTransform.sizeDelta = new Vector2(400f, 20f);

            _title = UIBuilder.CreateText(
                card, "Title", string.Empty, 34f,
                Color.white, TextAnchor.UpperCenter, true, FontStyle.Bold);
            _title.rectTransform.anchorMin = new Vector2(0.08f, 1f);
            _title.rectTransform.anchorMax = new Vector2(0.92f, 1f);
            _title.rectTransform.pivot = new Vector2(0.5f, 1f);
            _title.rectTransform.anchoredPosition = new Vector2(0f, -110f);
            _title.rectTransform.sizeDelta = new Vector2(0f, 90f);

            _detail = UIBuilder.CreateText(
                card, "Detail", string.Empty, 15f,
                UITheme.WithAlpha(Color.white, 0.82f), TextAnchor.UpperCenter, true);
            _detail.rectTransform.anchorMin = new Vector2(0.1f, 0.34f);
            _detail.rectTransform.anchorMax = new Vector2(0.9f, 0.66f);

            _tierLabel = UIBuilder.CreateText(
                card, "Tier", string.Empty, 12f,
                UITheme.CyanPale, TextAnchor.UpperCenter, true, FontStyle.Bold, 0.25f);
            _tierLabel.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            _tierLabel.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            _tierLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
            _tierLabel.rectTransform.anchoredPosition = new Vector2(0f, 118f);
            _tierLabel.rectTransform.sizeDelta = new Vector2(400f, 18f);

            _continueButton = UIBuilder.CreatePrimaryAction(
                card, "Continue", "CONTINUE", string.Empty, OnContinue, 50f);
            var rect = _continueButton.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 34f);
            rect.sizeDelta = new Vector2(240f, 50f);

            var group = UIBuilder.EnsureGroup(gameObject);
            group.blocksRaycasts = true;
            SetVisible(false);
        }

        public void Show(string title, string detail, RouletteTier tier, Action onContinue)
        {
            _onContinue = onContinue;
            _revealElapsed = 0f;
            _title.text = title;
            _detail.text = detail;
            _tierLabel.text = TierName(tier);
            _cardFill.color = TierColor(tier);
            gameObject.SetActive(true);
            SetVisible(true);
        }

        private void Update()
        {
            // Landing pop: the card settles 1.08x -> 1x over half a second.
            if (!gameObject.activeSelf || _title == null) return;
            _revealElapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(_revealElapsed / 0.5f);
            var scale = Mathf.Lerp(1.08f, 1f, 1f - Mathf.Pow(1f - t, 3f));
            var card = _title.transform.parent.GetComponent<RectTransform>();
            if (card != null) card.localScale = new Vector3(scale, scale, 1f);
        }

        private void OnContinue()
        {
            var handler = _onContinue;
            _onContinue = null;
            SetVisible(false);
            handler?.Invoke();
        }

        private static string TierName(RouletteTier tier)
        {
            switch (tier)
            {
                case RouletteTier.Mediocre: return "COMMON";
                case RouletteTier.Standard: return "STANDARD";
                case RouletteTier.Premium: return "PREMIUM";
                default: return "LEGENDARY";
            }
        }

        private static Color TierColor(RouletteTier tier)
        {
            switch (tier)
            {
                case RouletteTier.Mediocre: return new Color(0.09f, 0.10f, 0.13f, 0.97f);
                case RouletteTier.Standard: return new Color(0.06f, 0.12f, 0.16f, 0.97f);
                case RouletteTier.Premium: return new Color(0.13f, 0.10f, 0.04f, 0.97f);
                default: return new Color(0.16f, 0.07f, 0.03f, 0.97f);
            }
        }
    }
}
