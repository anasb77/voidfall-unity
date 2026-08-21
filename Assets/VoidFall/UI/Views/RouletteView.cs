using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VoidFall.Core;

namespace VoidFall.UI
{
    /// <summary>
    /// The Boss Roulette ceremony, rebuilt from spec section 43.
    ///
    /// Integrity rule: the landing wedge is sampled exactly once, at the press
    /// of LET IT RIDE, from the run's Rng stream handed in through Present().
    /// The animation only reveals that sample — it never re-rolls and never
    /// fabricates near-misses. Purchases (Improve Odds / Raise Stakes) mutate
    /// the table before the sample, which is what makes them worth buying.
    ///
    /// Presentation budget: the reveal targets 2.5-5 seconds total so boss
    /// kills never become fifteen-second interruptions.
    /// </summary>
    public sealed class RouletteView : UIViewBase
    {
        private enum Stage
        {
            Idle,
            Choosing,
            Spinning,
            Revealed,
        }

        private const float WheelDiameter = 520f;
        private const float MarkerRadius = WheelDiameter * 0.36f;
        private const float SpinSeconds = 2.7f;
        private const int ExtraRevolutions = 4;

        private readonly List<RectTransform> _markers = new List<RectTransform>();

        private RectTransform _wheel;
        private Text _partsLabel;
        private Text _statusLabel;
        private Button _improveOddsButton;
        private Button _raiseStakesButton;
        private Button _spinButton;
        private Button _continueButton;
        private Text _improveOddsCostLabel;
        private Text _raiseStakesCostLabel;
        private RectTransform _resultPanel;
        private Text _resultTitle;
        private Text _resultDetail;
        private Text _refundLine;

        private RouletteSession _session;
        private Rng _rng;
        private int _availableParts;
        private Stage _stage = Stage.Idle;

        // Spin interpolation state.
        private float _spinElapsed;
        private float _startRotation;
        private float _targetRotation;
        private Action<RouletteSession> _onComplete;

        /// <summary>Raised once, after the player accepts the revealed prize.</summary>
        public event Action<RouletteSession> CeremonyComplete;

        protected override void Build()
        {
            var scrim = UIBuilder.CreateScrim(Root, "Scrim", UITheme.OverlayScrim);
            scrim.raycastTarget = true;

            var content = UIBuilder.CreateRect(Root, "Content");
            content.anchorMin = new Vector2(0.5f, 0.5f);
            content.anchorMax = new Vector2(0.5f, 0.5f);
            content.pivot = new Vector2(0.5f, 0.5f);
            content.sizeDelta = new Vector2(960f, 700f);

            BuildHeader(content);
            BuildWheel(content);
            BuildWagerRow(content);
            BuildResultPanel(content);

            var group = UIBuilder.EnsureGroup(gameObject);
            group.blocksRaycasts = true;
        }

        private void BuildHeader(RectTransform parent)
        {
            var header = UIBuilder.CreateRect(parent, "Header");
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.sizeDelta = new Vector2(0f, 92f);

            var kicker = UIBuilder.CreateText(
                header, "Kicker", "BOSS FELLED", 13f,
                UITheme.CyanPale, TextAnchor.UpperCenter, true, FontStyle.Bold, 0.34f);
            kicker.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            kicker.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            kicker.rectTransform.pivot = new Vector2(0.5f, 1f);
            kicker.rectTransform.anchoredPosition = new Vector2(0f, -8f);

            var title = UIBuilder.CreateText(
                header, "Title", "THE VOID ROULETTE", 34f,
                Color.white, TextAnchor.UpperCenter, true, FontStyle.Bold);
            title.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -30f);

            _statusLabel = UIBuilder.CreateText(
                header, "Status", "LET IT RIDE.", 12f,
                UITheme.WithAlpha(Color.white, 0.62f), TextAnchor.UpperCenter, true, FontStyle.Normal, 0.22f);
            _statusLabel.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            _statusLabel.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _statusLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
            _statusLabel.rectTransform.anchoredPosition = new Vector2(0f, -70f);

            _partsLabel = UIBuilder.CreateText(
                header, "Parts", string.Empty, 15f,
                UITheme.CyanPale, TextAnchor.UpperCenter, true, FontStyle.Bold);
            _partsLabel.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            _partsLabel.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _partsLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
            _partsLabel.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            _partsLabel.transform.SetSiblingIndex(1);
        }

        private void BuildWheel(RectTransform parent)
        {
            var holder = UIBuilder.CreateRect(parent, "Wheel Holder");
            holder.anchorMin = new Vector2(0.5f, 0.5f);
            holder.anchorMax = new Vector2(0.5f, 0.5f);
            holder.pivot = new Vector2(0.5f, 0.5f);
            holder.sizeDelta = new Vector2(WheelDiameter, WheelDiameter);
            holder.anchoredPosition = new Vector2(0f, 10f);

            var disc = UIBuilder.CreateSurface(holder, "Disc", UISprites.Circle(256));
            disc.type = Image.Type.Simple;
            UIBuilder.Stretch(disc.rectTransform);
            disc.color = UITheme.WithAlpha(new Color(0.04f, 0.05f, 0.09f), 0.92f);
            disc.raycastTarget = false;

            var rim = UIBuilder.CreateRect(holder, "Rim");
            UIBuilder.Stretch(rim);
            var rimImage = rim.gameObject.AddComponent<Image>();
            rimImage.sprite = UISprites.Circle(256);
            rimImage.color = UITheme.WithAlpha(UITheme.Cyan, 0.35f);
            rimImage.raycastTarget = false;
            rim.transform.SetAsFirstSibling();

            // Rotating group: every wedge marker is a child of _wheel, so the
            // whole spin is one rotation on this rect.
            _wheel = UIBuilder.CreateRect(holder, "Wheel");
            UIBuilder.Stretch(_wheel);

            var pointer = UIBuilder.CreateSurface(holder, "Pointer", UISprites.Circle(64));
            pointer.type = Image.Type.Simple;
            pointer.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            pointer.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            pointer.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            pointer.rectTransform.sizeDelta = new Vector2(18f, 18f);
            pointer.rectTransform.anchoredPosition = new Vector2(0f, -6f);
            pointer.color = Color.white;
            pointer.raycastTarget = false;
        }

        private void BuildWagerRow(RectTransform parent)
        {
            var row = UIBuilder.CreateRect(parent, "Wager Row");
            row.anchorMin = new Vector2(0f, 0f);
            row.anchorMax = new Vector2(1f, 0f);
            row.pivot = new Vector2(0.5f, 0f);
            row.sizeDelta = new Vector2(0f, 96f);

            var improve = CreateWagerButton(row, "ImproveOdds", "IMPROVE ODDS",
                "Upgrade every weak slice", out _improveOddsCostLabel);
            improve.anchorMin = new Vector2(0f, 0.5f);
            improve.anchorMax = new Vector2(0f, 0.5f);
            improve.pivot = new Vector2(0f, 0.5f);
            improve.anchoredPosition = new Vector2(24f, 0f);
            improve.sizeDelta = new Vector2(240f, 76f);
            improve.GetComponent<Button>().onClick.AddListener(OnImproveOdds);
            _improveOddsButton = improve.GetComponent<Button>();

            var raise = CreateWagerButton(row, "RaiseStakes", "RAISE STAKES",
                "Double the top-tier slices", out _raiseStakesCostLabel);
            raise.anchorMin = new Vector2(1f, 0.5f);
            raise.anchorMax = new Vector2(1f, 0.5f);
            raise.pivot = new Vector2(1f, 0.5f);
            raise.anchoredPosition = new Vector2(-24f, 0f);
            raise.sizeDelta = new Vector2(240f, 76f);
            raise.GetComponent<Button>().onClick.AddListener(OnRaiseStakes);
            _raiseStakesButton = raise.GetComponent<Button>();

            var spin = CreateWagerButton(row, "Spin", "LET IT RIDE",
                "Sample the wheel. No take-backs.", out _);
            spin.anchorMin = new Vector2(0.5f, 0.5f);
            spin.anchorMax = new Vector2(0.5f, 0.5f);
            spin.pivot = new Vector2(0.5f, 0.5f);
            spin.sizeDelta = new Vector2(260f, 84f);
            spin.GetComponent<Button>().onClick.AddListener(OnSpinPressed);
            _spinButton = spin.GetComponent<Button>();
        }

        private static RectTransform CreateWagerButton(
            RectTransform parent,
            string name,
            string title,
            string subtitle,
            out Text costLabel)
        {
            var rect = UIBuilder.CreateRect(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = UISprites.Rounded(10f, Color.white, Color.white, Color.white);
            image.type = Image.Type.Sliced;
            image.color = UITheme.WithAlpha(new Color(0.10f, 0.16f, 0.22f), 0.9f);

            var label = UIBuilder.CreateText(
                rect, "Title", title, 17f, Color.white, TextAnchor.MiddleCenter, true, FontStyle.Bold);
            label.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            label.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            label.rectTransform.pivot = new Vector2(0.5f, 1f);
            label.rectTransform.sizeDelta = new Vector2(0f, 30f);
            label.rectTransform.anchoredPosition = new Vector2(0f, -34f);

            var detail = UIBuilder.CreateText(
                rect, "Subtitle", subtitle, 10f,
                UITheme.WithAlpha(Color.white, 0.6f), TextAnchor.UpperCenter, true, FontStyle.Normal);
            detail.rectTransform.anchorMin = new Vector2(0f, 0f);
            detail.rectTransform.anchorMax = new Vector2(1f, 0f);
            detail.rectTransform.pivot = new Vector2(0.5f, 0f);
            detail.rectTransform.sizeDelta = new Vector2(-16f, 26f);
            detail.rectTransform.anchoredPosition = new Vector2(0f, 30f);

            costLabel = UIBuilder.CreateText(
                rect, "Cost", string.Empty, 12f, UITheme.CyanPale,
                TextAnchor.LowerCenter, true, FontStyle.Bold);
            costLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
            costLabel.rectTransform.anchorMax = new Vector2(1f, 0f);
            costLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
            costLabel.rectTransform.sizeDelta = new Vector2(0f, 22f);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            return rect;
        }

        private void BuildResultPanel(RectTransform parent)
        {
            _resultPanel = UIBuilder.CreateRect(parent, "Result Panel");
            _resultPanel.anchorMin = new Vector2(0.5f, 0.5f);
            _resultPanel.anchorMax = new Vector2(0.5f, 0.5f);
            _resultPanel.pivot = new Vector2(0.5f, 0.5f);
            _resultPanel.sizeDelta = new Vector2(640f, 240f);
            _resultPanel.anchoredPosition = new Vector2(0f, 10f);

            var card = UIBuilder.CreateRect(_resultPanel, "Card");
            UIBuilder.Stretch(card, 10f);
            var image = card.gameObject.AddComponent<Image>();
            image.sprite = UISprites.Rounded(14f, Color.white, Color.white, Color.white);
            image.type = Image.Type.Sliced;
            image.color = new Color(0.05f, 0.07f, 0.12f, 0.97f);

            _resultTitle = UIBuilder.CreateText(
                card, "Prize", string.Empty, 30f, Color.white,
                TextAnchor.MiddleCenter, true, FontStyle.Bold);
            _resultTitle.rectTransform.anchorMin = new Vector2(0f, 0.55f);
            _resultTitle.rectTransform.anchorMax = new Vector2(1f, 0.95f);

            _resultDetail = UIBuilder.CreateText(
                card, "Detail", string.Empty, 14f,
                UITheme.WithAlpha(Color.white, 0.75f), TextAnchor.UpperCenter, true, FontStyle.Normal);
            _resultDetail.rectTransform.anchorMin = new Vector2(0.1f, 0.36f);
            _resultDetail.rectTransform.anchorMax = new Vector2(0.9f, 0.55f);

            _refundLine = UIBuilder.CreateText(
                card, "Refund", string.Empty, 12f, UITheme.CyanPale,
                TextAnchor.UpperCenter, true, FontStyle.Italic);
            _refundLine.rectTransform.anchorMin = new Vector2(0.08f, 0.2f);
            _refundLine.rectTransform.anchorMax = new Vector2(0.92f, 0.36f);

            var cont = CreateWagerButton(_resultPanel, "Continue", "CONTINUE",
                string.Empty, out _);
            cont.anchorMin = new Vector2(0.5f, 0f);
            cont.anchorMax = new Vector2(0.5f, 0f);
            cont.pivot = new Vector2(0.5f, 0f);
            cont.anchoredPosition = new Vector2(0f, 14f);
            cont.sizeDelta = new Vector2(220f, 52f);
            cont.GetComponent<Button>().onClick.AddListener(OnContinue);
            _continueButton = cont.GetComponent<Button>();

            _resultPanel.gameObject.SetActive(false);
        }

        /// <summary>
        /// Opens the ceremony for a freshly created (not yet spun) session.
        /// The Rng handed in here must be the run's deterministic stream so
        /// replays from a seed reproduce the same prize.
        /// </summary>
        public void Present(RouletteSession session, Rng rng, int availableParts)
        {
            _session = session;
            _rng = rng;
            _availableParts = Math.Max(0, availableParts);
            _stage = Stage.Choosing;
            _spinElapsed = 0f;

            gameObject.SetActive(true);
            RebuildMarkers();
            RefreshWagerUi();

            if (_resultPanel != null) _resultPanel.gameObject.SetActive(false);
            if (_statusLabel != null) _statusLabel.text = "THE VOID OFFERS A WAGER.";
        }

        private void RebuildMarkers()
        {
            if (_wheel == null) return;
            ClearChildren(_wheel);
            _markers.Clear();
            if (_session == null) return;

            var wedges = _session.Wedges;
            var step = 360f / Mathf.Max(1, wedges.Length);
            for (var index = 0; index < wedges.Length; index++)
            {
                var wedge = wedges[index];
                var accent = ParseAccent(wedge.Accent);

                var marker = UIBuilder.CreateRect(_wheel, "Wedge " + index);
                marker.anchorMin = new Vector2(0.5f, 0.5f);
                marker.anchorMax = new Vector2(0.5f, 0.5f);
                marker.pivot = new Vector2(0.5f, 0.5f);

                var angle = 90f - index * step;
                var radians = angle * Mathf.Deg2Rad;
                marker.anchoredPosition = new Vector2(
                    Mathf.Cos(radians) * MarkerRadius,
                    Mathf.Sin(radians) * MarkerRadius);
                marker.localRotation = Quaternion.identity;

                var dot = UIBuilder.CreateSurface(marker, "Dot", UISprites.Circle(64));
                dot.type = Image.Type.Simple;
                dot.rectTransform.sizeDelta = new Vector2(14f, 14f);
                dot.rectTransform.anchoredPosition = new Vector2(0f, 16f);
                dot.color = accent;
                dot.raycastTarget = false;

                var label = UIBuilder.CreateText(
                    marker, "Label", wedge.Name, 11f,
                    UITheme.WithAlpha(Color.white, 0.85f), TextAnchor.UpperCenter, true, FontStyle.Bold);
                label.rectTransform.pivot = new Vector2(0.5f, 1f);
                label.rectTransform.sizeDelta = new Vector2(120f, 30f);
                label.rectTransform.anchoredPosition = new Vector2(0f, 6f);

                _markers.Add(marker);
            }

            _wheel.localRotation = Quaternion.identity;
        }

        private void RefreshWagerUi()
        {
            if (_session == null) return;
            var netParts = _availableParts - NetSpend();
            if (_partsLabel != null) _partsLabel.text = "PARTS  " + netParts;

            var improveCost = RouletteRules.ImproveOddsCost(_session.ImproveOddsUses);
            var raiseCost = RouletteRules.RaiseStakesCost(_session.RaiseStakesUses);
            SetWagerButton(
                _improveOddsButton,
                _improveOddsCostLabel,
                _session.ImproveOddsUses >= RouletteRules.MaxUsesPerPurchase,
                netParts < improveCost,
                improveCost,
                "MAXED");
            SetWagerButton(
                _raiseStakesButton,
                _raiseStakesCostLabel,
                _session.RaiseStakesUses >= RouletteRules.MaxUsesPerPurchase,
                netParts < raiseCost,
                raiseCost,
                "MAXED");
        }

        private static void SetWagerButton(
            Button button,
            Text costLabel,
            bool capped,
            bool unaffordable,
            int cost,
            string cappedText)
        {
            if (button == null) return;
            var disabled = capped || unaffordable;
            button.interactable = !disabled;
            if (costLabel == null) return;
            costLabel.text = capped ? cappedText : cost + " PARTS";
            costLabel.color = disabled
                ? UITheme.WithAlpha(UITheme.CyanPale, 0.35f)
                : UITheme.CyanPale;
        }

        private int NetSpend()
        {
            if (_session == null) return 0;
            return _session.PartsSpent - _session.PartsRefunded;
        }

        private void OnImproveOdds()
        {
            PurchaseAndRefresh(true);
        }

        private void OnRaiseStakes()
        {
            PurchaseAndRefresh(false);
        }

        private void PurchaseAndRefresh(bool improveOdds)
        {
            if (_stage != Stage.Choosing || _session == null) return;
            if (!RouletteRules.Purchase(
                    _session, improveOdds, _availableParts - NetSpend(), _rng,
                    out _, out var refundLine))
            {
                return;
            }

            // The wheel visibly changes when money changes the table.
            RebuildMarkers();
            RefreshWagerUi();
            if (_statusLabel != null)
            {
                _statusLabel.text = refundLine ?? (improveOdds
                    ? "WEAK SLICES UPGRADED."
                    : "TOP-TIER SLICES DOUBLED.");
            }
        }

        private void OnSpinPressed()
        {
            if (_stage != Stage.Choosing || _session == null) return;

            // The one and only sample. Purchases from this point on are locked
            // by the rules engine; the animation below only reveals the result.
            RouletteRules.Spin(_session, _rng);

            var wedges = _session.Wedges;
            var step = 360f / Mathf.Max(1, wedges.Length);
            var current = _wheel != null ? _wheel.localEulerAngles.z : 0f;
            var target = -_session.ResultIndex * step;
            while (target < current - 1f + ExtraRevolutions * 360f)
            {
                target += 360f;
            }

            _startRotation = current;
            _targetRotation = target;
            _spinElapsed = 0f;
            _stage = Stage.Spinning;

            SetWagerRowInteractable(false);
            if (_statusLabel != null) _statusLabel.text = string.Empty;
        }

        private void Update()
        {
            if (_stage != Stage.Spinning || _wheel == null) return;
            _spinElapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(_spinElapsed / SpinSeconds);
            // Ease-out cubic: fast launch, long decelerating settle.
            var eased = 1f - Mathf.Pow(1f - t, 3f);
            var angle = Mathf.LerpAngle(_startRotation, _targetRotation, eased);
            _wheel.localRotation = Quaternion.Euler(0f, 0f, angle);

            if (!(t >= 1f)) return;
            _wheel.localRotation = Quaternion.Euler(0f, 0f, _targetRotation);
            RevealResult();
        }

        private void RevealResult()
        {
            _stage = Stage.Revealed;
            var prize = _session != null ? _session.Result : null;
            if (prize == null) return;

            if (_resultPanel != null) _resultPanel.gameObject.SetActive(true);
            if (_resultTitle != null)
            {
                _resultTitle.text = prize.Name;
                _resultTitle.color = ParseAccent(prize.Accent);
            }
            if (_resultDetail != null) _resultDetail.text = prize.Description + ".";
            if (_refundLine != null)
            {
                _refundLine.text = _session.PartsRefunded > 0 ? RefundTextFromLog() : string.Empty;
            }
        }

        private string RefundTextFromLog()
        {
            var log = _session.Log;
            for (var index = log.Count - 1; index >= 0; index--)
            {
                if (!string.IsNullOrEmpty(log[index])) return log[index];
            }
            return string.Empty;
        }

        private void OnContinue()
        {
            var finished = _session;
            _session = null;
            _stage = Stage.Idle;
            SetVisible(false);
            CeremonyComplete?.Invoke(finished);
        }

        private void SetWagerRowInteractable(bool interactable)
        {
            if (_improveOddsButton != null) _improveOddsButton.interactable = interactable;
            if (_raiseStakesButton != null) _raiseStakesButton.interactable = interactable;
            if (_spinButton != null) _spinButton.interactable = interactable;
        }

        private static Color ParseAccent(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var color)
                ? color
                : UITheme.CyanPale;
        }
    }
}