using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VoidFall.UI
{
    /// <summary>One permanent-upgrade row, supplied by the runtime.</summary>
    public struct WorkshopItemData
    {
        public string Id;
        public string Name;
        public string Description;
        public int CurrentRank;
        public int MaxRank;
        public int Cost;
        public bool CanAfford;
    }

    /// <summary>
    /// The permanent upgrade store, rebuilt from .workshop-panel.
    ///
    /// The browser build pairs the list with a live canvas that redraws the ship
    /// with each upgrade's visual rank. That render belongs to the gameplay
    /// renderer rather than the UI layer, so the left column here is an
    /// information panel: what is focused, what the next rank costs, and the
    /// current rank of every track.
    /// </summary>
    public sealed class WorkshopView : UIViewBase
    {
        private readonly List<RowWidgets> _rows = new List<RowWidgets>();
        private readonly Dictionary<string, Text> _rankStrip =
            new Dictionary<string, Text>();

        private Text _partsBadge;
        private Text _previewTitle;
        private Text _previewDetail;
        private Text _previewRank;
        private RectTransform _listContent;
        private RectTransform _rankStripRow;
        private RectTransform _previewStage;
        private string _focusedId;

        /// <summary>
        /// The rect the runtime mounts the live frame preview on.
        /// Requires <see cref="BuildPreviewColumn"/> to have run.
        /// </summary>
        public RectTransform PreviewStage => _previewStage;

        private sealed class RowWidgets
        {
            public string Id;
            public RectTransform Root;
            public Image Surface;
            public Text Name;
            public Text Description;
            public Button Buy;
            public Text BuyLabel;
            public RectTransform PipRow;
            public Sprite Rest;
            public Sprite Focused;
        }

        protected override void Build()
        {
            UIBuilder.CreateScrim(Root, "Blocker", new Color(0f, 0f, 0f, 0.0001f));

            var content = UIBuilder.CreateProfilePanel(
                Root,
                "Panel",
                new Vector2(880f, 660f),
                "Permanent upgrades",
                "Workshop",
                () => Callbacks?.CloseMenuPage?.Invoke(),
                out var headerSlot);

            _partsBadge = UIBuilder.CreatePartsBadge(headerSlot, "PartsBalance");

            var intro = UIBuilder.CreateParagraph(
                content,
                "Intro",
                "Focus an upgrade to preview its next visual rank.",
                12f,
                UITheme.TextIntro);
            intro.rectTransform.anchorMin = new Vector2(0f, 1f);
            intro.rectTransform.anchorMax = new Vector2(1f, 1f);
            intro.rectTransform.pivot = new Vector2(0.5f, 1f);
            intro.rectTransform.sizeDelta = new Vector2(0f, 20f);
            intro.rectTransform.anchoredPosition = new Vector2(0f, -4f);

            var layout = UIBuilder.CreateRect(content, "Layout");
            layout.anchorMin = Vector2.zero;
            layout.anchorMax = Vector2.one;
            layout.offsetMin = Vector2.zero;
            layout.offsetMax = new Vector2(0f, -32f);

            BuildPreviewColumn(layout);
            BuildListColumn(layout);
        }

        /// <summary>
        /// The left .workshop-preview column: a sticky panel describing the
        /// focused upgrade, with the per-track rank strip along the bottom.
        /// </summary>
        private void BuildPreviewColumn(RectTransform parent)
        {
            var column = UIBuilder.CreateRect(parent, "Preview");
            column.anchorMin = new Vector2(0f, 0f);
            column.anchorMax = new Vector2(0f, 1f);
            column.pivot = new Vector2(0f, 0.5f);
            column.sizeDelta = new Vector2(360f, 0f);
            column.anchoredPosition = Vector2.zero;

            var body = UIBuilder.CreateSurface(column, "Body", UISprites.Rounded(
                UITheme.RadiusCard,
                UITheme.PreviewFill,
                UITheme.PreviewFill,
                UITheme.BorderPreview));
            UIBuilder.Stretch(body.rectTransform);

            var header = UIBuilder.CreateRect(body.rectTransform, "Header");
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.sizeDelta = new Vector2(0f, 49f);

            var headerFill = UIBuilder.CreateSurface(header, "Fill", UISprites.Rounded(
                UITheme.RadiusCard,
                UITheme.Rgba(15, 28, 54, 0.78f),
                UITheme.Rgba(7, 12, 27, 0.88f),
                default(Color)));
            UIBuilder.Stretch(headerFill.rectTransform);
            headerFill.rectTransform.SetAsFirstSibling();

            var kicker = UIBuilder.CreateText(
                header,
                "Kicker",
                "FRAME PREVIEW",
                8f,
                UITheme.CyanLight,
                TextAnchor.LowerLeft,
                true,
                FontStyle.Bold,
                0.22f);
            kicker.rectTransform.offsetMin = new Vector2(13f, 24f);
            kicker.rectTransform.offsetMax = new Vector2(-13f, -8f);

            _previewTitle = UIBuilder.CreateText(
                header,
                "Title",
                "Current configuration",
                12f,
                UITheme.TextBody,
                TextAnchor.UpperLeft,
                true,
                FontStyle.Bold);
            _previewTitle.rectTransform.offsetMin = new Vector2(13f, 8f);
            _previewTitle.rectTransform.offsetMax = new Vector2(-13f, -26f);

            UIBuilder.CreateRule(header, "Rule", UITheme.WithAlpha(UITheme.CyanLight, 0.12f))
                .rectTransform.anchoredPosition = Vector2.zero;

            // The middle of the column carries the live frame preview; the
            // runtime mounts PlayerFramePreview on this stage.
            _previewStage = UIBuilder.CreateRect(body.rectTransform, "FramePreviewStage");
            _previewStage.anchorMin = new Vector2(0.5f, 1f);
            _previewStage.anchorMax = new Vector2(0.5f, 1f);
            _previewStage.pivot = new Vector2(0.5f, 1f);
            _previewStage.sizeDelta = new Vector2(320f, 186f);
            _previewStage.anchoredPosition = new Vector2(0f, -55f);

            // The readout (rank caption + focused detail) sits below the stage.
            var readout = UIBuilder.CreateRect(body.rectTransform, "Readout");
            readout.anchorMin = Vector2.zero;
            readout.anchorMax = Vector2.one;
            readout.offsetMin = new Vector2(18f, 48f);
            readout.offsetMax = new Vector2(-18f, -238f);

            _previewRank = UIBuilder.CreateText(
                readout,
                "Rank",
                "\u2014",
                52f,
                UITheme.WithAlpha(UITheme.CyanBright, 0.9f),
                TextAnchor.MiddleCenter,
                true,
                FontStyle.Bold);
            _previewRank.rectTransform.anchorMin = new Vector2(0f, 0.42f);
            _previewRank.rectTransform.anchorMax = new Vector2(1f, 0.86f);
            _previewRank.rectTransform.offsetMin = Vector2.zero;
            _previewRank.rectTransform.offsetMax = Vector2.zero;

            var glow = UIBuilder.CreateSurface(readout, "Glow", UISprites.Glow(192));
            glow.type = Image.Type.Simple;
            glow.rectTransform.anchorMin = new Vector2(0.5f, 0.64f);
            glow.rectTransform.anchorMax = new Vector2(0.5f, 0.64f);
            glow.rectTransform.sizeDelta = new Vector2(260f, 260f);
            glow.color = UITheme.WithAlpha(UITheme.Cyan, 0.10f);
            glow.rectTransform.SetAsFirstSibling();

            _previewDetail = UIBuilder.CreateParagraph(
                readout,
                "Detail",
                "Hover an upgrade to inspect it.",
                11f,
                UITheme.TextRowDetail,
                TextAnchor.UpperCenter);
            _previewDetail.rectTransform.anchorMin = new Vector2(0f, 0.06f);
            _previewDetail.rectTransform.anchorMax = new Vector2(1f, 0.4f);
            _previewDetail.rectTransform.offsetMin = Vector2.zero;
            _previewDetail.rectTransform.offsetMax = Vector2.zero;

            // .preview-ranks: one cell per track, highlighting the focused one.
            _rankStripRow = UIBuilder.CreateRect(body.rectTransform, "RankStrip");
            _rankStripRow.anchorMin = new Vector2(0f, 0f);
            _rankStripRow.anchorMax = new Vector2(1f, 0f);
            _rankStripRow.pivot = new Vector2(0.5f, 0f);
            _rankStripRow.sizeDelta = new Vector2(0f, 34f);
            var stripLayout = UIBuilder.AddHorizontalLayout(_rankStripRow, 1f, null, TextAnchor.MiddleCenter);
            stripLayout.childForceExpandWidth = true;

            // The rule belongs to the panel body, not the strip: a child of the
            // strip would be laid out as another cell by the layout group.
            var stripRule = UIBuilder.CreateRule(
                body.rectTransform,
                "RankStripRule",
                UITheme.WithAlpha(UITheme.CyanLight, 0.10f));
            stripRule.rectTransform.anchoredPosition = new Vector2(0f, 34f);

            var refund = UIBuilder.CreateSecondaryAction(
                column,
                "Refund",
                "Refund all",
                null,
                () => Callbacks?.RefundWorkshop?.Invoke(),
                34f);
            var refundRect = refund.GetComponent<RectTransform>();
            refundRect.anchorMin = new Vector2(0f, 0f);
            refundRect.anchorMax = new Vector2(1f, 0f);
            refundRect.pivot = new Vector2(0.5f, 0f);
            refundRect.sizeDelta = new Vector2(0f, 34f);
            refundRect.anchoredPosition = new Vector2(0f, -42f);
        }

        private void BuildListColumn(RectTransform parent)
        {
            var column = UIBuilder.CreateRect(parent, "List");
            column.anchorMin = Vector2.zero;
            column.anchorMax = Vector2.one;
            column.offsetMin = new Vector2(374f, 0f);
            column.offsetMax = Vector2.zero;

            _listContent = UIBuilder.CreateScrollView(column, "Scroll", out _);
            UIBuilder.AddVerticalLayout(_listContent, 8f);
        }

        /// <summary>
        /// Rebuilds the list. Called on open and after every purchase, since a
        /// purchase changes ranks, costs and affordability across the board.
        /// </summary>
        public void Populate(int totalParts, IReadOnlyList<WorkshopItemData> items)
        {
            if (_partsBadge != null) _partsBadge.text = FormatNumber(totalParts);

            if (items != null && CanReuseRows(items))
            {
                for (var index = 0; index < items.Count; index++)
                {
                    UpdateRow(_rows[index], items[index]);
                    UpdateRankCell(items[index]);
                }

                ApplyFocus(_focusedId, true);
                return;
            }

            ClearChildren(_listContent);
            ClearChildren(_rankStripRow);
            _rows.Clear();
            _rankStrip.Clear();

            if (items == null) return;

            for (var index = 0; index < items.Count; index++)
            {
                BuildRow(items[index]);
                BuildRankCell(items[index]);
            }

            ApplyFocus(_focusedId, true);
        }

        private bool CanReuseRows(IReadOnlyList<WorkshopItemData> items)
        {
            if (_rows.Count != items.Count || _listContent == null || _rankStripRow == null)
                return false;
            for (var index = 0; index < items.Count; index++)
            {
                var row = _rows[index];
                if (row == null || row.Root == null || row.Id != items[index].Id)
                    return false;
                if (!_rankStrip.ContainsKey(items[index].Id))
                    return false;
            }
            return true;
        }

        private void UpdateRow(RowWidgets row, WorkshopItemData item)
        {
            if (row.Name != null)
                row.Name.text = item.Name + "   " + item.CurrentRank + "/" + item.MaxRank;
            if (row.Description != null)
                row.Description.text = item.Description;
            if (row.PipRow != null)
            {
                ClearChildren(row.PipRow);
                UIBuilder.CreateRankPips(row.PipRow, "PipRow", item.MaxRank, item.CurrentRank);
            }
            var maxed = item.Cost < 0;
            if (row.BuyLabel != null)
            {
                row.BuyLabel.text = maxed ? "Complete" : FormatNumber(item.Cost);
                row.BuyLabel.color = maxed
                    ? UITheme.TextDisabledDeep
                    : (item.CanAfford ? UITheme.CyanPale : UITheme.TextDisabledDeep);
            }
            if (row.Buy != null)
                row.Buy.interactable = !maxed && item.CanAfford;
        }

        private void UpdateRankCell(WorkshopItemData item)
        {
            if (_rankStrip.TryGetValue(item.Id, out var label) && label != null)
                label.text = item.CurrentRank.ToString();
        }

        private void BuildRow(WorkshopItemData item)
        {
            var row = UIBuilder.CreateRect(_listContent, "Row." + item.Id);
            row.sizeDelta = new Vector2(0f, 74f);
            UIBuilder.SetHeight(row, 74f);

            var rest = UISprites.Rounded(UITheme.RadiusRow, UITheme.RowFill, UITheme.RowFill, UITheme.BorderRow);
            var focused = UISprites.Rounded(
                UITheme.RadiusRow,
                UITheme.RowFillActive,
                UITheme.RowFillActive,
                UITheme.WithAlpha(UITheme.CyanLight, 0.54f));

            var surface = UIBuilder.CreateSurface(row, "Body", rest, true);
            UIBuilder.Stretch(surface.rectTransform);

            var frame = UIBuilder.CreateSurface(row, "IconFrame", UISprites.Rounded(
                UITheme.RadiusSmall,
                UITheme.IconFrame,
                UITheme.IconFrame,
                default(Color)));
            frame.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            frame.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            frame.rectTransform.pivot = new Vector2(0f, 0.5f);
            frame.rectTransform.sizeDelta = new Vector2(36f, 36f);
            frame.rectTransform.anchoredPosition = new Vector2(11f, 0f);

            var icon = UIIcons.CreateHomeIcon(frame.rectTransform, WorkshopIconId(item.Id), UITheme.CyanLabel, 19f);
            if (icon != null)
            {
                icon.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                icon.rectTransform.anchoredPosition = Vector2.zero;
            }

            var name = UIBuilder.CreateText(
                row,
                "Name",
                item.Name + "   " + item.CurrentRank + "/" + item.MaxRank,
                13f,
                UITheme.TextBody,
                TextAnchor.LowerLeft,
                true,
                FontStyle.Bold);
            name.rectTransform.anchorMin = new Vector2(0f, 0.58f);
            name.rectTransform.anchorMax = new Vector2(1f, 1f);
            name.rectTransform.offsetMin = new Vector2(58f, 0f);
            name.rectTransform.offsetMax = new Vector2(-96f, -10f);

            var description = UIBuilder.CreateText(
                row,
                "Detail",
                item.Description,
                11f,
                UITheme.TextRowDetail,
                TextAnchor.UpperLeft,
                false);
            description.rectTransform.anchorMin = new Vector2(0f, 0.3f);
            description.rectTransform.anchorMax = new Vector2(1f, 0.6f);
            description.rectTransform.offsetMin = new Vector2(58f, 0f);
            description.rectTransform.offsetMax = new Vector2(-96f, 0f);

            var pipHost = UIBuilder.CreateRect(row, "Pips");
            pipHost.anchorMin = new Vector2(0f, 0f);
            pipHost.anchorMax = new Vector2(0f, 0f);
            pipHost.pivot = new Vector2(0f, 0f);
            pipHost.sizeDelta = new Vector2(200f, 12f);
            pipHost.anchoredPosition = new Vector2(58f, 12f);
            UIBuilder.CreateRankPips(pipHost, "PipRow", item.MaxRank, item.CurrentRank);

            var maxed = item.Cost < 0;
            var buy = UIBuilder.CreateBuyButton(row, "Buy", () =>
            {
                Callbacks?.BuyWorkshop?.Invoke(item.Id);
            }, out var buyLabel);
            var buyRect = buy.GetComponent<RectTransform>();
            buyRect.anchorMin = new Vector2(1f, 0.5f);
            buyRect.anchorMax = new Vector2(1f, 0.5f);
            buyRect.pivot = new Vector2(1f, 0.5f);
            buyRect.anchoredPosition = new Vector2(-11f, 0f);

            if (maxed)
            {
                buyLabel.text = "Complete";
                buyLabel.color = UITheme.TextDisabledDeep;
                buy.interactable = false;
            }
            else
            {
                buyLabel.text = FormatNumber(item.Cost);
                buyLabel.color = item.CanAfford ? UITheme.CyanPale : UITheme.TextDisabledDeep;
                buy.interactable = item.CanAfford;
            }

            // Hover and click both focus the row, matching the browser build's
            // pointerenter / click / focus handling.
            var hover = surface.gameObject.AddComponent<UIFocusTrigger>();
            hover.Bind(() => ApplyFocus(item.Id, false), () => ApplyFocus(null, false));

            var click = surface.gameObject.AddComponent<Button>();
            click.transition = Selectable.Transition.None;
            click.onClick.AddListener(() => ApplyFocus(item.Id, true));

            _rows.Add(new RowWidgets
            {
                Id = item.Id,
                Root = row,
                Surface = surface,
                Name = name,
                Description = description,
                Buy = buy,
                BuyLabel = buyLabel,
                PipRow = pipHost,
                Rest = rest,
                Focused = focused
            });
        }

        private void BuildRankCell(WorkshopItemData item)
        {
            var cell = UIBuilder.CreateRect(_rankStripRow, "Rank." + item.Id);
            UIBuilder.SetHeight(cell, 31f);

            var fill = UIBuilder.CreateSurface(cell, "Fill", UISprites.Rounded(
                2f,
                UITheme.Rgba(5, 10, 22, 0.96f),
                UITheme.Rgba(5, 10, 22, 0.96f),
                default(Color)));
            UIBuilder.Stretch(fill.rectTransform);

            var label = UIBuilder.CreateText(
                cell,
                "Value",
                item.CurrentRank.ToString(),
                10f,
                UITheme.TextInactive,
                TextAnchor.MiddleCenter,
                true,
                FontStyle.Bold);
            _rankStrip[item.Id] = label;
        }

        /// <summary>
        /// Highlights a row and mirrors it into the preview column. Passing null
        /// clears the focus unless it was set by a click.
        /// </summary>
        private void ApplyFocus(string id, bool sticky)
        {
            if (!sticky && id == null && _focusedId != null)
            {
                // Leaving a row returns to the resting state, as in the browser.
                _focusedId = null;
            }
            else if (id != null)
            {
                _focusedId = id;
            }

            for (var index = 0; index < _rows.Count; index++)
            {
                var row = _rows[index];
                if (row.Surface == null) continue;
                var active = row.Id == _focusedId;
                row.Surface.sprite = active ? row.Focused : row.Rest;
            }

            foreach (var pair in _rankStrip)
            {
                if (pair.Value == null) continue;
                pair.Value.color = pair.Key == _focusedId ? UITheme.CyanBright : UITheme.TextInactive;
            }

            RefreshPreview();
            // Mirror focus into the runtime whether or not a row is focused:
            // null releases the +1 next-rank preview, matching the browser.
            Callbacks?.PreviewWorkshop?.Invoke(_focusedId);
        }

        private void RefreshPreview()
        {
            RowWidgets focused = null;
            for (var index = 0; index < _rows.Count; index++)
            {
                if (_rows[index].Id == _focusedId) focused = _rows[index];
            }

            if (focused == null)
            {
                if (_previewTitle != null) _previewTitle.text = "Current configuration";
                if (_previewDetail != null) _previewDetail.text = "Hover an upgrade to inspect it.";
                if (_previewRank != null) _previewRank.text = "\u2014";
                return;
            }

            if (_previewTitle != null) _previewTitle.text = focused.Name.text;
            if (_previewDetail != null) _previewDetail.text = focused.Description.text;
            if (_rankStrip.TryGetValue(focused.Id, out var rankLabel) && _previewRank != null)
            {
                _previewRank.text = rankLabel.text;
            }
        }

        /// <summary>
        /// Maps a workshop track onto the closest glyph in the shipped home atlas.
        /// The browser build uses a dedicated Lucide icon per track; the atlas only
        /// carries five, so tracks without a match fall back to the wrench.
        /// </summary>
        private static string WorkshopIconId(string id)
        {
            switch (id)
            {
                case "precision":
                case "power": return "trophy";
                case "magnet":
                case "arsenal": return "settings";
                case "recovery":
                case "protocol": return "skull";
                case "integrity":
                case "mobility":
                default: return "wrench";
            }
        }
    }

    /// <summary>
    /// Reports pointer enter and exit, used by the workshop rows to drive the
    /// preview the same way the browser build's pointer handlers do.
    /// </summary>
    public sealed class UIFocusTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private System.Action _onEnter;
        private System.Action _onExit;

        public void Bind(System.Action onEnter, System.Action onExit)
        {
            _onEnter = onEnter;
            _onExit = onExit;
        }

        public void OnPointerEnter(PointerEventData eventData) => _onEnter?.Invoke();

        public void OnPointerExit(PointerEventData eventData) => _onExit?.Invoke();
    }
}
