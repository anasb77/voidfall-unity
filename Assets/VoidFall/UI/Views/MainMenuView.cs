using UnityEngine;
using UnityEngine.UI;

namespace VoidFall.UI
{
    /// <summary>
    /// The landing screen, rebuilt from the browser build's .main-menu.
    ///
    /// Note this screen deliberately carries no panel chrome. In the stylesheet
    /// .main-menu is not a .menu-panel, so it has no fill, border or shadow and
    /// floats directly over the arena. Reproducing that is most of what makes the
    /// home page feel like the original.
    /// </summary>
    public sealed class MainMenuView : UIViewBase
    {
        private const float ContentWidth = 620f;

        private Text _bestScoreValue;
        private Text _partsValue;
        private Text _runsValue;
        private Text _workshopDetail;
        private Text _recordsDetail;
        private Text _arenaName;

        protected override void Build()
        {
            var content = UIBuilder.CreateRect(Root, "Content");
            content.anchorMin = new Vector2(0.5f, 0.5f);
            content.anchorMax = new Vector2(0.5f, 0.5f);
            content.pivot = new Vector2(0.5f, 0.5f);
            content.sizeDelta = new Vector2(ContentWidth, 430f);

            // The stack below follows the stylesheet's desktop rhythm:
            // title, 29 gap, start action, 22 gap, status strip, 11 gap, nav grid.
            var cursor = 0f;

            cursor += BuildTitle(content, cursor);
            cursor += 29f;
            cursor += BuildStartAction(content, cursor);
            cursor += 22f;
            cursor += BuildStatusStrip(content, cursor);
            cursor += 11f;
            cursor += BuildNavGrid(content, cursor);
            cursor += 12f;
            BuildArenaSelector(content, cursor);

            content.sizeDelta = new Vector2(ContentWidth, cursor + 40f);

            BuildQuitButton();
        }

        /// <summary>
        /// The corner close control. An icon-only X rather than a labelled
        /// button, mirroring the mute control in the opposite corner; it opens
        /// the modal exit confirmation instead of quitting immediately.
        /// </summary>
        private void BuildQuitButton()
        {
            var quit = UIBuilder.CreateIconButton(
                Root,
                "QuitButton",
                "\u2715",
                () => Manager?.QuitConfirm?.Show(),
                28.6f);

            var rect = quit.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            // Top-right corner, mirrored vertically from the mute button's
            // (-14, 14) bottom-right placement.
            rect.anchoredPosition = new Vector2(-14f, -14f);

            // The icon button's 17px glyph is sized for the 44px control; scale
            // it with the 35%-reduced frame so the X keeps its proportions.
            var glyph = quit.transform.Find("Glyph")?.GetComponent<Text>();
            if (glyph != null) glyph.fontSize = 11;
        }

        /// <summary>Places a child at a vertical offset from the container's top.</summary>
        private static RectTransform Place(RectTransform parent, string name, float top, float height, float width = 0f)
        {
            var rt = UIBuilder.CreateRect(parent, name);
            if (width > 0f)
            {
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(width, height);
                rt.anchoredPosition = new Vector2(0f, -top);
            }
            else
            {
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(0f, height);
                rt.anchoredPosition = new Vector2(0f, -top);
            }
            return rt;
        }

        private float BuildTitle(RectTransform parent, float top)
        {
            const float height = 104f;
            var block = Place(parent, "TitleBlock", top, height);

            // filter: drop-shadow(0 0 22px rgba(34, 211, 238, 0.22))
            var bloom = UIBuilder.CreateSurface(block, "Bloom", UISprites.Glow(256));
            bloom.type = Image.Type.Simple;
            UIBuilder.Stretch(bloom.rectTransform, -70f, -40f, -70f, -40f);
            bloom.color = UITheme.WithAlpha(UITheme.Cyan, 0.22f);

            // Keep the wordmark stable. The legacy Text component cannot apply
            // real tracking, and synthetic Unicode spaces plus best-fit sizing
            // made the title look uneven across installed fonts.
            var title = UIBuilder.CreateText(
                block,
                "Title",
                "VOIDFALL",
                96f,
                UITheme.CyanLight,
                TextAnchor.MiddleCenter,
                true,
                FontStyle.Bold,
                0f);
            title.resizeTextForBestFit = false;
            return height;
        }

        private float BuildStartAction(RectTransform parent, float top)
        {
            const float height = 60f;
            var slot = Place(parent, "StartAction", top, height, 330f);
            var button = UIBuilder.CreateStartButton(
                slot,
                "StartRun",
                "Start run",
                () => Callbacks?.StartRun?.Invoke(),
                new Vector2(330f, height));
            UIBuilder.Stretch(button.GetComponent<RectTransform>());
            return height;
        }

        /// <summary>
        /// The .menu-status card: three metrics separated by 1x24 rules. Unlike
        /// the title this one does carry panel chrome, at slightly lower opacity.
        /// </summary>
        private float BuildStatusStrip(RectTransform parent, float top)
        {
            const float height = 58f;
            var strip = Place(parent, "StatusStrip", top, height);

            var body = UIBuilder.CreateSurface(strip, "Body", UISprites.Rounded(
                UITheme.RadiusCard,
                UITheme.StatusTop,
                UITheme.StatusBottom,
                UITheme.BorderPanel,
                1f,
                UITheme.PanelGradientAngle,
                true));
            UIBuilder.Stretch(body.rectTransform);

            var row = UIBuilder.Stretch(UIBuilder.CreateRect(strip, "Cells"), 14f, 9f, 14f, 9f);
            var layout = UIBuilder.AddHorizontalLayout(row, 0f, null, TextAnchor.MiddleCenter);
            layout.childControlWidth = true;
            // Forced expansion would stretch the 1px dividers into wide blocks;
            // cells claim the free width through flexibleWidth instead.
            layout.childForceExpandWidth = false;

            _bestScoreValue = BuildStatusCell(row, "BestScore", "trophy", "Best score", "0");
            BuildStatusDivider(row, "DividerA");
            _partsValue = BuildStatusCell(row, "Parts", "coins", "Parts", "0");
            BuildStatusDivider(row, "DividerB");
            _runsValue = BuildStatusCell(row, "Runs", "skull", "Runs", "0");
            return height;
        }

        private static Text BuildStatusCell(
            RectTransform parent,
            string name,
            string iconId,
            string label,
            string value)
        {
            var cell = UIBuilder.CreateRect(parent, name);
            var cellElement = cell.gameObject.AddComponent<LayoutElement>();
            cellElement.flexibleWidth = 1f;
            cellElement.flexibleHeight = 1f;

            var icon = UIIcons.CreateHomeIcon(cell, iconId, UITheme.CyanBright, 14f);
            var textLeft = 0f;
            if (icon != null)
            {
                icon.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                icon.rectTransform.pivot = new Vector2(0f, 0.5f);
                icon.rectTransform.anchoredPosition = new Vector2(6f, 0f);
                textLeft = 26f;
            }

            var caption = UIBuilder.CreateText(
                cell,
                "Label",
                label.ToUpperInvariant(),
                8f,
                UITheme.TextStatusLabel,
                TextAnchor.LowerLeft,
                false,
                FontStyle.Bold,
                0.12f);
            caption.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            caption.rectTransform.anchorMax = new Vector2(1f, 1f);
            caption.rectTransform.offsetMin = new Vector2(textLeft, 0f);
            caption.rectTransform.offsetMax = new Vector2(-4f, -2f);

            var readout = UIBuilder.CreateText(
                cell,
                "Value",
                value,
                13f,
                UITheme.StatusValue,
                TextAnchor.UpperLeft,
                false,
                FontStyle.Bold);
            readout.rectTransform.anchorMin = new Vector2(0f, 0f);
            readout.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            readout.rectTransform.offsetMin = new Vector2(textLeft, 2f);
            readout.rectTransform.offsetMax = new Vector2(-4f, 0f);
            return readout;
        }

        private static void BuildStatusDivider(RectTransform parent, string name)
        {
            var divider = UIBuilder.CreateFill(parent, name, UITheme.Divider);
            divider.rectTransform.sizeDelta = new Vector2(1f, 24f);
            var element = divider.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 1f;
            element.flexibleWidth = 0f;
            element.preferredHeight = 24f;
            element.flexibleHeight = 0f;
        }

        private float BuildNavGrid(RectTransform parent, float top)
        {
            const float height = 67f;
            var grid = Place(parent, "NavGrid", top, height);

            var cellWidth = (ContentWidth - 20f) / 3f;
            UIBuilder.AddGrid(grid, new Vector2(cellWidth, height), new Vector2(10f, 10f), 3);

            BuildNavCard(grid, "Workshop", "wrench", "Workshop", "0 Parts",
                () => Callbacks?.OpenWorkshop?.Invoke(), out _workshopDetail);
            BuildNavCard(grid, "Records", "trophy", "Records", "0 runs",
                () => Callbacks?.OpenRecords?.Invoke(), out _recordsDetail);
            BuildNavCard(grid, "Settings", "settings", "Settings", "Audio and display",
                () => Callbacks?.OpenSettings?.Invoke(), out _);
            return height;
        }

        private static void BuildNavCard(
            RectTransform parent,
            string name,
            string iconId,
            string title,
            string detail,
            System.Action onClick,
            out Text detailLabel)
        {
            var button = UIBuilder.CreateNavCard(parent, name, title, detail, onClick, out detailLabel);
            var rect = button.GetComponent<RectTransform>();

            var icon = UIIcons.CreateHomeIcon(rect, iconId, UITheme.CyanBright, 18f);
            if (icon == null) return;

            icon.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            icon.rectTransform.pivot = new Vector2(0f, 0.5f);
            icon.rectTransform.anchoredPosition = new Vector2(12f, 0f);

            // Shift the label column clear of the icon (.menu-grid is a 38px 1fr grid).
            foreach (var label in new[] { "Title", "Detail" })
            {
                var child = rect.Find(label) as RectTransform;
                if (child == null) continue;
                child.offsetMin = new Vector2(42f, child.offsetMin.y);
            }
        }

        /// <summary>
        /// The starting-arena selector. This has no counterpart in the browser
        /// build; it exists because the Unity port persists a chosen arena, so it
        /// is styled as a quiet footer rather than a headline control.
        /// </summary>
        private void BuildArenaSelector(RectTransform parent, float top)
        {
            var row = Place(parent, "ArenaSelector", top, 34f);

            var prev = UIBuilder.CreateIconButton(row, "PrevArena", "\u25C0",
                () => Callbacks?.PrevArena?.Invoke(), 28f);
            var prevRect = prev.GetComponent<RectTransform>();
            prevRect.anchorMin = new Vector2(0.5f, 0.5f);
            prevRect.anchorMax = new Vector2(0.5f, 0.5f);
            prevRect.anchoredPosition = new Vector2(-118f, 0f);

            _arenaName = UIBuilder.CreateText(
                row,
                "ArenaName",
                "Abyss",
                11f,
                UITheme.CyanLabel,
                TextAnchor.MiddleCenter,
                true,
                FontStyle.Bold,
                0.16f);
            _arenaName.rectTransform.offsetMin = new Vector2(150f, 0f);
            _arenaName.rectTransform.offsetMax = new Vector2(-150f, 0f);

            var next = UIBuilder.CreateIconButton(row, "NextArena", "\u25B6",
                () => Callbacks?.NextArena?.Invoke(), 28f);
            var nextRect = next.GetComponent<RectTransform>();
            nextRect.anchorMin = new Vector2(0.5f, 0.5f);
            nextRect.anchorMax = new Vector2(0.5f, 0.5f);
            nextRect.anchoredPosition = new Vector2(118f, 0f);
        }

        /// <summary>Retained signature used by the runtime's arena cycling.</summary>
        public void UpdateProfile(int parts, int bestScore, string arenaName)
        {
            // Use the common setter for every live label. Besides keeping the
            // text update path consistent, this preserves any tracking/fallback
            // component attached by the builder instead of bypassing it with a
            // raw Text.text assignment.
            UIBuilder.SetText(_partsValue, FormatNumber(parts));
            UIBuilder.SetText(_bestScoreValue, FormatNumber(bestScore));
            UIBuilder.SetText(_workshopDetail, FormatNumber(parts) + " Parts");
            UIBuilder.SetText(_arenaName, (arenaName ?? "Abyss").ToUpperInvariant());
        }

        /// <summary>Applies the full profile, including the lifetime run count.</summary>
        public void UpdateProfile(UIProfileState profile)
        {
            UpdateProfile(profile.Parts, profile.BestScore, profile.ArenaName);
            UIBuilder.SetText(_runsValue, FormatNumber(profile.TotalRuns));
            UIBuilder.SetText(_recordsDetail, FormatNumber(profile.TotalRuns) + " runs");
        }
    }
}
