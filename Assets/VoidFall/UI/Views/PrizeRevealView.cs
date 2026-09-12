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
        private const float ContinueGuardSeconds = 0.45f;

        private Text _screenKicker, _heading, _cardKicker, _claimProgress;
        private Text _title, _detail, _tierLabel, _rankTransition, _sigil, _continueLabel;
        private Image _cardFill, _rewardIcon;
        private RectTransform _content, _card;
        private Button _continueButton;
        private Action _onContinue;
        private float _revealElapsed;
        private Font _titleFont;
        private bool _claimed;

        protected override void Build()
        {
            UIBuilder.CreateScrim(Root, "Scrim", new Color(0.018f, 0.026f, 0.045f, 0.97f));
            _content = RouletteView.Place(Root, "Reveal", 0, 0, 1280, 800);
            _screenKicker = RouletteView.Label(_content, "Kicker", "FORTUNE ANSWERS", 0, 345, 800, 22, 12, RouletteWheelGraphic.Gold);
            _titleFont = Font.CreateDynamicFontFromOSFont(new[] { "Georgia", "Times New Roman" }, 48);
            _heading = RouletteView.Label(_content, "Heading", "It belongs to you.", 0, 298, 1100, 65, 38, new Color(0.96f, 0.93f, 0.86f));
            _heading.font = _titleFont;
            var wheel = RouletteView.Place(_content, "Relic silhouette", 0, -3, 610, 610);
            var silhouette = wheel.gameObject.AddComponent<RouletteWheelGraphic>();
            silhouette.Configure(RouletteRules.DefaultTable(), default);
            var group = UIBuilder.EnsureGroup(wheel.gameObject);
            group.alpha = 0.15f;
            _card = RouletteView.Place(_content, "Prize Card", 0, -10, 440, 462);
            _cardFill = UIBuilder.CreateSurface(_card, "Fill", UISprites.Rounded(2,
                new Color(0.09f, 0.1f, 0.15f), new Color(0.025f, 0.03f, 0.05f), new Color(0.5f, 0.42f, 0.28f)));
            UIBuilder.Stretch(_cardFill.rectTransform);
            _cardKicker = RouletteView.Label(_card, "Card Kicker", "THE VOID YIELDS", 0, 184, 390, 24, 12, RouletteWheelGraphic.Gold);
            _claimProgress = RouletteView.Label(_card, "Claim Progress", string.Empty, 0, 184, 390, 24, 12, RouletteWheelGraphic.Gold);
            _claimProgress.gameObject.SetActive(false);
            _sigil = RouletteView.Label(_card, "Sigil", "◆", 0, 102, 160, 100, 64, RouletteWheelGraphic.Gold);
            var iconRect = RouletteView.Place(_card, "Reward Icon", 0, 110, 76, 76);
            _rewardIcon = iconRect.gameObject.AddComponent<Image>();
            _rewardIcon.preserveAspect = true;
            _rewardIcon.raycastTarget = false;
            _rewardIcon.gameObject.SetActive(false);
            _tierLabel = RouletteView.Label(_card, "Tier", string.Empty, 0, 30, 390, 24, 12, RouletteWheelGraphic.Gold);
            _rankTransition = RouletteView.Label(_card, "Rank Transition", string.Empty, 0, 27, 390, 28, 17, Color.white);
            _rankTransition.fontStyle = FontStyle.Bold;
            _rankTransition.gameObject.SetActive(false);
            _title = RouletteView.Label(_card, "Title", string.Empty, 0, -25, 392, 70, 28, Color.white);
            _title.font = _titleFont;
            _title.horizontalOverflow = HorizontalWrapMode.Wrap;
            _title.resizeTextForBestFit = true;
            _title.resizeTextMinSize = 20;
            _title.resizeTextMaxSize = 32;
            RouletteView.Surface(_card, "Divider", 0, -80, 338, 1, new Color(0.45f, 0.4f, 0.3f, 0.4f));
            _detail = RouletteView.Label(_card, "Detail", string.Empty, 0, -145, 356, 110, 16, new Color(0.74f, 0.78f, 0.84f));
            _detail.horizontalOverflow = HorizontalWrapMode.Wrap;
            _detail.verticalOverflow = VerticalWrapMode.Truncate;
            _detail.resizeTextForBestFit = true;
            _detail.resizeTextMinSize = 13;
            _detail.resizeTextMaxSize = 16;
            _continueButton = RouletteView.MakeButton(_content, "Continue", 0, -320, 280, 62, true, OnContinue, out _continueLabel);
        }

        public void Show(string title, string detail, RouletteTier tier, Action onContinue)
        {
            SetClaimPresentation(false);
            _onContinue = onContinue;
            _revealElapsed = 0;
            _claimed = false;
            _title.text = title;
            _detail.text = detail;
            ConfigureTier(tier);
            _continueButton.interactable = false;
            SetVisible(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_continueButton.gameObject);
        }

        public void ShowClaim(
            string title,
            string detail,
            RouletteTier tier,
            Sprite icon,
            int fromRank,
            int toRank,
            int claimIndex,
            int claimCount,
            Action onClaim)
        {
            SetClaimPresentation(true);
            _onContinue = onClaim;
            _revealElapsed = 0f;
            _claimed = false;
            _title.text = title;
            _detail.text = detail;
            _claimProgress.text = "CARD " + Mathf.Max(1, claimIndex) + " OF " + Mathf.Max(1, claimCount);
            _rankTransition.text = FormatRankTransition(fromRank, toRank);
            _rankTransition.gameObject.SetActive(!string.IsNullOrEmpty(_rankTransition.text));
            _rewardIcon.sprite = icon;
            _rewardIcon.gameObject.SetActive(icon != null);
            _sigil.gameObject.SetActive(icon == null);
            ConfigureTier(tier);
            _continueButton.interactable = false;
            SetVisible(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_continueButton.gameObject);
        }

        private void SetClaimPresentation(bool claim)
        {
            _screenKicker.gameObject.SetActive(!claim);
            _screenKicker.text = "FORTUNE ANSWERS";
            _heading.text = claim ? "CLAIM YOUR REWARD" : "It belongs to you.";
            _cardKicker.gameObject.SetActive(!claim);
            _claimProgress.gameObject.SetActive(claim);
            _rankTransition.gameObject.SetActive(false);
            _rewardIcon.sprite = null;
            _rewardIcon.gameObject.SetActive(false);
            _sigil.gameObject.SetActive(true);
            _sigil.rectTransform.anchoredPosition = new Vector2(0f, claim ? 110f : 102f);
            _sigil.rectTransform.sizeDelta = claim ? new Vector2(76f, 76f) : new Vector2(160f, 100f);
            _tierLabel.rectTransform.anchoredPosition = new Vector2(0f, claim ? 56f : 30f);
            _title.rectTransform.anchoredPosition = new Vector2(0f, claim ? -30f : -25f);
            _continueLabel.text = claim ? "CLAIM" : "CONTINUE";
        }

        private void ConfigureTier(RouletteTier tier)
        {
            _tierLabel.text = tier == RouletteTier.Mediocre ? "COMMON" : tier.ToString().ToUpperInvariant();
            var accent = RouletteWheelGraphic.Accent(new RouletteWedgeDefinition(RoulettePrizeKind.RareBoon, tier, 1, "", "", ""));
            _tierLabel.color = _sigil.color = accent;
        }

        private static string FormatRankTransition(int fromRank, int toRank)
        {
            if (toRank <= 0) return string.Empty;
            var destination = RomanRank(toRank);
            return fromRank <= 0
                ? "NEW → RANK " + destination
                : "RANK " + RomanRank(fromRank) + " → " + destination;
        }

        private static string RomanRank(int rank)
        {
            switch (rank)
            {
                case 1: return "I";
                case 2: return "II";
                case 3: return "III";
                case 4: return "IV";
                case 5: return "V";
                case 6: return "VI";
                default: return rank.ToString();
            }
        }

        private void Update()
        {
            if (_card == null) return;
            _revealElapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(_revealElapsed / 0.55f);
            _card.localScale = Vector3.one * Mathf.Lerp(0.94f, 1, 1 - Mathf.Pow(1 - t, 3));
            _content.localScale = Vector3.one * Mathf.Max(0.1f, Mathf.Min(Root.rect.width / 1280f, Root.rect.height / 820f));
            // Do not let the same held submit that started the spin dismiss its reward.
            _continueButton.interactable = !_claimed && _revealElapsed >= ContinueGuardSeconds;
        }

        private void OnContinue()
        {
            if (_claimed || _revealElapsed < ContinueGuardSeconds) return;
            _claimed = true;
            var handler = _onContinue;
            _onContinue = null;
            _continueButton.interactable = false;
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
