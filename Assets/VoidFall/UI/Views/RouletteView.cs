using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VoidFall.Core;

namespace VoidFall.UI
{
    /// <summary>The relic unfolds, wagers reshape it, and one sampled result is revealed to the runtime.</summary>
    public sealed class RouletteView : UIViewBase
    {
        private enum Stage { Idle, Choosing, Spinning, Landing, Complete }
        private const float SpinSeconds = 6.8f;
        private readonly List<RectTransform> _markers = new List<RectTransform>();
        private readonly List<Text> _effects = new List<Text>();
        private readonly List<Text> _odds = new List<Text>();
        private readonly List<Image> _rowAccents = new List<Image>();
        private RectTransform _content, _wheel, _wheelHolder;
        private RouletteWheelGraphic _wheelGraphic;
        private CanvasGroup _entrance;
        private Text _heading, _statusLabel, _partsLabel, _centreLabel, _improveDetail, _raiseDetail;
        private Text _improveOddsCostLabel, _raiseStakesCostLabel, _spinLabel, _selectedDetail;
        private Button _improveOddsButton, _raiseStakesButton, _spinButton;
        private RouletteSession _session;
        private Rng _rng;
        private RouletteSpinContext _spinContext;
        private int _availableParts;
        private Stage _stage;
        private float _spinElapsed, _targetRotation, _currentRotation, _landingElapsed, _openElapsed;
        private int _lastTick = -1;
        private Vector2 _lastSize;
        private Font _titleFont;
        private static readonly Color Muted = new Color(0.68f, 0.73f, 0.8f);

        public event Action<RouletteSession> CeremonyComplete;
        public event Action Tick;
        public event Action WagerChanged;
        public event Action Landed;

        protected override void Build()
        {
            UIBuilder.CreateScrim(Root, "Scrim", new Color(0.018f, 0.026f, 0.045f, 0.94f));
            _content = Place(Root, "Ceremony", 0, 0, 1280, 800);
            _entrance = UIBuilder.EnsureGroup(_content.gameObject);
            Label(_content, "Kicker", "GUARDIAN FELLED  /  REWARD", 0, 354, 900, 22, 12, RouletteWheelGraphic.Gold);
            _heading = Label(_content, "Title", "THE VOID ROULETTE", 0, 312, 1150, 65, 40, new Color(0.96f, 0.93f, 0.86f));
            _titleFont = Font.CreateDynamicFontFromOSFont(new[] { "Georgia", "Times New Roman" }, 48);
            _heading.font = _titleFont;
            _heading.fontSize = 48;
            Label(_content, "Subtitle", "Its guardian is gone. Its power is yours to wager.", 0, 264, 950, 26, 14, Muted);
            _wheelHolder = Place(_content, "Wheel Holder", 195, -3, 520, 520);
            var glow = UIBuilder.CreateSurface(_wheelHolder, "Relic Glow", UISprites.Glow(256));
            UIBuilder.Stretch(glow.rectTransform, -80);
            glow.color = new Color(0.8f, 0.6f, 0.3f, 0.15f);
            glow.raycastTarget = false;
            _wheel = Place(_wheelHolder, "Wheel", 0, 0, 520, 520);
            _wheelGraphic = _wheel.gameObject.AddComponent<RouletteWheelGraphic>();
            Place(_wheelHolder, "Fixed rim and pointer", 0, 0, 520, 520).gameObject.AddComponent<RouletteWheelGraphic>().Configure(null, default, true);
            Label(_wheelHolder, "Core Kicker", "THE VOID", 0, 12, 125, 22, 10, RouletteWheelGraphic.Gold);
            _centreLabel = Label(_wheelHolder, "Core State", "AWAITS", 0, -14, 125, 25, 14, Color.white);
            Label(_content, "Possibilities", "WHAT YOU CAN WIN", -410, 212, 340, 24, 12, RouletteWheelGraphic.Gold, TextAnchor.MiddleLeft);
            Label(_content, "Odds caption", "Reward effects and current chances", -410, 186, 340, 22, 12, Muted, TextAnchor.MiddleLeft);
            var table = RouletteRules.DefaultTable();
            for (var index = 0; index < table.Length; index++)
            {
                var row = Place(_content, "Prize " + index, -410, 142 - index * 49, 350, 46);
                var captured = index;
                var fill = row.gameObject.AddComponent<Image>();
                fill.color = new Color(0.055f, 0.07f, 0.1f, 0.8f);
                var button = row.gameObject.AddComponent<Button>();
                button.targetGraphic = fill;
                button.onClick.AddListener(() => DescribePrize(captured));
                _rowAccents.Add(Surface(row, "Tier", -173, 0, 2, 41, RouletteWheelGraphic.Accent(table[index])));
                Label(row, "Name", table[index].Name, -15, 10, 296, 18, 11, Color.white, TextAnchor.MiddleLeft);
                _effects.Add(Label(row, "Effect", string.Empty, -2, -11, 321, 22, 10.5f, Muted, TextAnchor.MiddleLeft));
                _odds.Add(Label(row, "Chance", string.Empty, 147, 10, 56, 18, 11, RouletteWheelGraphic.Gold, TextAnchor.MiddleRight));
            }
            _selectedDetail = Label(_content, "Reward Detail", "Select a reward to inspect its limits and fallback.", -405, -265, 360, 50, 11, Muted, TextAnchor.UpperLeft);
            _selectedDetail.horizontalOverflow = HorizontalWrapMode.Wrap;
            _partsLabel = Label(_content, "Parts", string.Empty, 488, 205, 200, 30, 17, RouletteWheelGraphic.Gold, TextAnchor.MiddleRight);
            _improveOddsButton = MakeButton(_content, "Improve Odds", -429, -334, 318, 86, false, OnImproveOdds, out var improveTitle);
            improveTitle.rectTransform.anchoredPosition = new Vector2(0, 23);
            _improveDetail = Label(_improveOddsButton.transform, "Effect", "Parts cache: 60 → 90 Parts", 0, -2, 300, 24, 12, Muted);
            _improveOddsCostLabel = Label(_improveOddsButton.transform, "Cost", string.Empty, 0, -25, 280, 20, 12, RouletteWheelGraphic.Gold);
            _raiseStakesButton = MakeButton(_content, "Raise Stakes", 429, -334, 318, 86, false, OnRaiseStakes, out var raiseTitle);
            raiseTitle.rectTransform.anchoredPosition = new Vector2(0, 23);
            _raiseDetail = Label(_raiseStakesButton.transform, "Effect", string.Empty, 0, -2, 300, 24, 12, Muted);
            _raiseStakesCostLabel = Label(_raiseStakesButton.transform, "Cost", string.Empty, 0, -25, 280, 20, 12, RouletteWheelGraphic.Gold);
            _spinButton = MakeButton(_content, "Spin", 0, -324, 276, 62, true, OnSpinPressed, out _spinLabel);
            _spinLabel.text = "LET IT RIDE";
            _statusLabel = Label(_content, "Status", string.Empty, 0, -388, 1200, 24, 11, Muted);
        }

        public void Present(RouletteSession session, Rng rng, int availableParts, RouletteSpinContext spinContext = default)
        {
            _session = session;
            _rng = rng;
            _availableParts = Math.Max(0, availableParts);
            _spinContext = spinContext;
            _stage = Stage.Choosing;
            _spinElapsed = _landingElapsed = _openElapsed = _currentRotation = 0;
            _lastTick = -1;
            _heading.text = "The Void Roulette";
            _centreLabel.text = "AWAITS";
            _spinLabel.text = "LET IT RIDE";
            _spinButton.interactable = session != null;
            _statusLabel.text = "One spin. One reward. Wagers have a 30% refund chance.";
            _selectedDetail.text = "Select a reward to inspect its limits and fallback.";
            SetVisible(true);
            RebuildMarkers();
            RefreshWagerUi();
            Resize();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_spinButton.gameObject);
        }

        private void RebuildMarkers()
        {
            foreach (var marker in _markers)
            {
                marker.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(marker.gameObject); else DestroyImmediate(marker.gameObject);
            }
            _markers.Clear();
            _wheel.localRotation = Quaternion.identity;
            if (_session == null) return;
            _wheelGraphic.Configure(_session.Wedges, _spinContext);
            for (var index = 0; index < _session.Wedges.Length; index++)
            {
                var wedge = _session.Wedges[index];
                var chance = RoulettePresentationRules.Probability(_session.Wedges, index, _spinContext);
                var angle = (90f - (float)RoulettePresentationRules.CentreDegrees(_session.Wedges, index, _spinContext)) * Mathf.Deg2Rad;
                var marker = Place(_wheel, "Wedge " + index, Mathf.Cos(angle) * 170, Mathf.Sin(angle) * 170, 100, 46);
                var accent = RouletteWheelGraphic.Accent(wedge);
                // Tiny protected segments use the readable reward list for their text.
                if (chance >= 0.035)
                {
                    Label(marker, "Category", Category(wedge.Kind), 0, 9, 100, 22, 10, accent);
                    Label(marker, "Reward", RoulettePresentationRules.ShortEffect(wedge), 0, -13, 100, 21, 12, Color.white);
                }
                _markers.Add(marker);
                if (index >= _effects.Count) continue;
                _effects[index].text = RoulettePresentationRules.Effect(wedge);
                _odds[index].text = (chance * 100).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "%";
                _rowAccents[index].color = accent;
            }
        }

        private void DescribePrize(int index)
        {
            if (_session == null || index >= _session.Wedges.Length || _stage != Stage.Choosing) return;
            var prize = _session.Wedges[index];
            _selectedDetail.text = RoulettePresentationRules.Effect(prize) + ". " + RoulettePresentationRules.Fallback(prize.Kind);
        }

        private void RefreshWagerUi()
        {
            if (_session == null) return;
            var parts = _availableParts - _session.PartsSpent + _session.PartsRefunded;
            _partsLabel.text = "PARTS  " + parts;
            var improveCost = RouletteRules.ImproveOddsCost(_session.ImproveOddsUses);
            var raiseCost = RouletteRules.RaiseStakesCost(_session.RaiseStakesUses);
            var canImprove = RouletteRules.CanImproveOdds(_session.Wedges) && _session.ImproveOddsUses < RouletteRules.MaxUsesPerPurchase;
            _improveOddsButton.interactable = _stage == Stage.Choosing && canImprove && parts >= improveCost;
            _improveOddsCostLabel.text = !canImprove ? "ALREADY IMPROVED" : parts < improveCost ? "NEED " + improveCost + " PARTS" : improveCost + " PARTS";
            _improveDetail.text = canImprove ? "Parts cache: 60 → 90 Parts" : "Parts cache upgraded. No further charge.";
            var capped = _session.RaiseStakesUses >= RouletteRules.MaxUsesPerPurchase;
            _raiseStakesButton.interactable = _stage == Stage.Choosing && !capped && parts >= raiseCost;
            _raiseStakesCostLabel.text = capped ? "MAXIMUM STAKES" : parts < raiseCost ? "NEED " + raiseCost + " PARTS" : raiseCost + " PARTS";
            var before = LegendaryChance(_session.Wedges);
            var after = capped ? before : LegendaryChance(RouletteRules.ApplyRaiseStakes(_session.Wedges));
            _raiseDetail.text = "Legendary: " + before.ToString("0.#") + "%" + (capped ? string.Empty : " → " + after.ToString("0.#") + "%");
        }

        private double LegendaryChance(RouletteWedgeDefinition[] table)
        {
            double chance = 0;
            for (var i = 0; i < table.Length; i++)
                if (table[i].Tier == RouletteTier.Legendary) chance += RoulettePresentationRules.Probability(table, i, _spinContext) * 100;
            return chance;
        }

        private void OnImproveOdds() => PurchaseAndRefresh(true);
        private void OnRaiseStakes() => PurchaseAndRefresh(false);
        private void PurchaseAndRefresh(bool improve)
        {
            if (_stage != Stage.Choosing || _session == null) return;
            if (!RouletteRules.Purchase(_session, improve, _availableParts - _session.PartsSpent + _session.PartsRefunded, _rng, out _, out var refund)) return;
            RebuildMarkers();
            RefreshWagerUi();
            _statusLabel.text = refund ?? (improve ? "Parts cache upgraded. The weakest outcome is now worth 90 Parts." : "Legendary segments expanded. These are your new chances.");
            WagerChanged?.Invoke();
        }

        private void OnSpinPressed()
        {
            if (_stage != Stage.Choosing || _session == null) return;
            RouletteRules.Spin(_session, _rng, _spinContext);
            _targetRotation = 5 * 360f + (float)RoulettePresentationRules.CentreDegrees(_session.Wedges, _session.ResultIndex, _spinContext);
            _spinElapsed = 0;
            _stage = Stage.Spinning;
            _heading.text = "Let it ride.";
            _centreLabel.text = "DECIDES";
            _spinLabel.text = "FATE IS TURNING";
            _spinButton.interactable = _improveOddsButton.interactable = _raiseStakesButton.interactable = false;
            _statusLabel.text = "Your wager is sealed.";
        }

        private void Update()
        {
            if (_content == null) return;
            Resize();
            var dt = Time.unscaledDeltaTime;
            _openElapsed += dt;
            var entrance = Mathf.Clamp01(_openElapsed / 0.8f);
            _entrance.alpha = Mathf.Clamp01(_openElapsed / 0.3f);
            _wheelHolder.localScale = Vector3.one * Mathf.Lerp(0.16f, 1, 1 - Mathf.Pow(1 - entrance, 3));
            if (_stage == Stage.Landing)
            {
                _landingElapsed += dt;
                if (_landingElapsed >= 0.85f)
                {
                    _stage = Stage.Complete;
                    var finished = _session;
                    _session = null;
                    CeremonyComplete?.Invoke(finished);
                }
                return;
            }
            if (_stage != Stage.Spinning) return;
            _spinElapsed += dt;
            var t = Mathf.Clamp01(_spinElapsed / SpinSeconds);
            const float acceleration = 0.12f;
            const float normalization = acceleration / 2 + (1 - acceleration) / 3;
            var progress = t < acceleration ? t * t / (2 * acceleration) / normalization
                : (acceleration / 2 + (1 - acceleration) / 3 * (1 - Mathf.Pow((1 - t) / (1 - acceleration), 3))) / normalization;
            // Accumulated rotation preserves full revolutions; LerpAngle would discard them.
            _currentRotation = Mathf.Lerp(0, _targetRotation, progress);
            _wheel.localRotation = Quaternion.Euler(0, 0, _currentRotation);
            foreach (var marker in _markers) marker.localRotation = Quaternion.Euler(0, 0, -_currentRotation);
            var atPointer = Mathf.Repeat(_currentRotation, 360);
            var cursor = 0f;
            for (var i = 0; i < _session.Wedges.Length; i++)
            {
                cursor += (float)RoulettePresentationRules.Probability(_session.Wedges, i, _spinContext) * 360;
                if (atPointer >= cursor) continue;
                if (_lastTick != i) { _lastTick = i; Tick?.Invoke(); }
                break;
            }
            if (t < 1) return;
            _stage = Stage.Landing;
            _landingElapsed = 0;
            _centreLabel.text = "YIELDS";
            _heading.text = "The wheel has spoken.";
            _spinLabel.text = "REVEALING REWARD";
            _wheelGraphic.Configure(_session.Wedges, _spinContext, selected: _session.ResultIndex);
            Landed?.Invoke();
        }

        private void Resize()
        {
            if (_content == null || Root.rect.size == _lastSize) return;
            _lastSize = Root.rect.size;
            _content.localScale = Vector3.one * Mathf.Max(0.1f, Mathf.Min(_lastSize.x / 1280f, _lastSize.y / 820f));
        }

        private void OnDestroy()
        {
            if (_titleFont == null) return;
            if (Application.isPlaying) Destroy(_titleFont); else DestroyImmediate(_titleFont);
        }

        private static string Category(RoulettePrizeKind kind)
        {
            switch (kind)
            {
                case RoulettePrizeKind.Parts: return "PARTS";
                case RoulettePrizeKind.UpgradeRandomOwned: return "UPGRADE";
                case RoulettePrizeKind.NewRandomCard: return "ARSENAL";
                case RoulettePrizeKind.WeaponUpgradeQuality: return "WEAPON";
                case RoulettePrizeKind.SupportUpgradeQuality: return "SUPPORT";
                case RoulettePrizeKind.PowerUp: return "VOID GIFT";
                case RoulettePrizeKind.RareBoon: return "BOON";
                default: return "WILD CARD";
            }
        }

        internal static RectTransform Place(Transform parent, string name, float x, float y, float w, float h)
        {
            var rect = UIBuilder.CreateRect(parent, name);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(w, h);
            return rect;
        }

        internal static Text Label(Transform parent, string name, string text, float x, float y, float w, float h, float size, Color tint, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var label = UIBuilder.CreateText(parent, name, text, size, tint, alignment, false);
            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(w, h);
            return label;
        }

        internal static Image Surface(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            var image = Place(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        internal static Button MakeButton(Transform parent, string name, float x, float y, float w, float h, bool primary, UnityEngine.Events.UnityAction action, out Text label)
        {
            var rect = Place(parent, name, x, y, w, h);
            var image = rect.gameObject.AddComponent<Image>();
            var gold = RouletteWheelGraphic.Gold;
            image.sprite = UISprites.Rounded(2, primary ? new Color(0.94f, 0.84f, 0.63f) : new Color(0.07f, 0.09f, 0.14f), primary ? new Color(0.74f, 0.58f, 0.31f) : new Color(0.035f, 0.045f, 0.08f), primary ? gold : new Color(0.33f, 0.32f, 0.3f));
            image.type = Image.Type.Sliced;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = colors.selectedColor = new Color(1.18f, 1.18f, 1.18f);
            colors.pressedColor = new Color(0.8f, 0.75f, 0.65f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);
            button.colors = colors;
            button.onClick.AddListener(action);
            label = Label(rect, "Label", name.ToUpperInvariant(), 0, 0, w - 20, 30, 15, primary ? new Color(0.1f, 0.08f, 0.05f) : new Color(0.92f, 0.9f, 0.85f));
            return button;
        }
    }
}
