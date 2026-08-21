using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VoidFall.UI
{
    /// <summary>Per-weapon damage contribution shown in the result breakdown.</summary>
    public struct WeaponStatSummary
    {
        public string Name;
        public int Rank;
        public long Damage;
        public float DamagePercent;
    }

    /// <summary>One entry in the final build recap.</summary>
    public struct UIBuildChip
    {
        public string Name;
        public int Rank;
        public Color Accent;
        public bool Evolved;
    }

    /// <summary>Everything the run result screen displays.</summary>
    public struct GameOverSummary
    {
        public bool Victory;
        public int Score;
        public float ElapsedSeconds;
        public int Kills;
        public int EliteKills;
        public int BossKills;
        public int Level;
        public int PartsEarned;
        public List<WeaponStatSummary> Weapons;

        /// <summary>Shows the pulsing "new best" badge.</summary>
        public bool IsBest;

        /// <summary>False shows the "progress was not saved" warning.</summary>
        public bool Saved;

        /// <summary>Weapons, supports and late upgrades held at the end.</summary>
        public List<UIBuildChip> BuildChips;
    }

    /// <summary>
    /// The run result, rebuilt from .result-card: metric grid, final build recap,
    /// damage breakdown and the two actions.
    /// </summary>
    public sealed class GameOverView : UIViewBase
    {
        private const float HeaderHeight = 32f;
        private const int ChipColumns = 3;

        private RectTransform _chipPanel;
        private RectTransform _damagePanel;
        private Text _kicker;
        private Text _title;
        private RectTransform _badgeRow;
        private RectTransform _content;
        private RectTransform _metricGrid;
        private RectTransform _chipGrid;
        private RectTransform _damageList;
        private Text _score;
        private Text _time;
        private Text _kills;
        private Text _parts;
        private Text _level;
        private Text _bosses;

        protected override void Build()
        {
            UIBuilder.CreateScrim(Root, "Scrim", UITheme.OverlayScrim);

            var card = UIBuilder.CreatePanel(Root, "Card", new Vector2(520f, 700f));
            var group = UIBuilder.EnsureGroup(card.gameObject);
            var body = card.Find("Body") as RectTransform ?? card;
            var inner = UIBuilder.Stretch(UIBuilder.CreateRect(body, "Inner"), 25f);

            _kicker = UIBuilder.CreateKicker(inner, "Kicker", "Run ended", UITheme.OverlayKicker, TextAnchor.UpperCenter);
            _kicker.rectTransform.anchorMin = new Vector2(0f, 1f);
            _kicker.rectTransform.anchorMax = new Vector2(1f, 1f);
            _kicker.rectTransform.pivot = new Vector2(0.5f, 1f);
            _kicker.rectTransform.sizeDelta = new Vector2(0f, 16f);

            _title = UIBuilder.CreateHeading(inner, "Title", "Try another build", TextAnchor.UpperCenter);
            _title.rectTransform.anchorMin = new Vector2(0f, 1f);
            _title.rectTransform.anchorMax = new Vector2(1f, 1f);
            _title.rectTransform.pivot = new Vector2(0.5f, 1f);
            _title.rectTransform.sizeDelta = new Vector2(0f, 38f);
            _title.rectTransform.anchoredPosition = new Vector2(0f, -20f);

            var bloom = UIBuilder.CreateSurface(_title.rectTransform, "Bloom", UISprites.Glow(256));
            bloom.type = Image.Type.Simple;
            UIBuilder.Stretch(bloom.rectTransform, -90f, -26f, -90f, -18f);
            bloom.color = UITheme.WithAlpha(UITheme.Cyan, 0.28f);
            bloom.rectTransform.SetAsFirstSibling();

            _badgeRow = UIBuilder.CreateRect(inner, "Badges");
            _badgeRow.anchorMin = new Vector2(0f, 1f);
            _badgeRow.anchorMax = new Vector2(1f, 1f);
            _badgeRow.pivot = new Vector2(0.5f, 1f);
            _badgeRow.sizeDelta = new Vector2(0f, 30f);
            _badgeRow.anchoredPosition = new Vector2(0f, -62f);
            var badgeLayout = UIBuilder.AddHorizontalLayout(_badgeRow, 8f, null, TextAnchor.MiddleCenter);
            badgeLayout.childForceExpandWidth = false;
            badgeLayout.childControlWidth = false;

            var scrollHost = UIBuilder.CreateRect(inner, "ScrollHost");
            scrollHost.anchorMin = Vector2.zero;
            scrollHost.anchorMax = Vector2.one;
            scrollHost.offsetMin = new Vector2(0f, 112f);
            scrollHost.offsetMax = new Vector2(0f, -98f);

            _content = UIBuilder.CreateScrollView(scrollHost, "Scroll", out _);
            UIBuilder.AddVerticalLayout(_content, 8f);

            BuildMetricGrid(_content);
            BuildRecap(_content);
            BuildDamage(_content);
            BuildActions(inner);

            card.gameObject.AddComponent<UIRiseIn>()
                .Bind(card, group, UITheme.PanelRiseSeconds, UITheme.PanelRiseOffset);
        }

        private void BuildMetricGrid(RectTransform parent)
        {
            _metricGrid = UIBuilder.CreateRect(parent, "ResultGrid");
            const int columns = 3;
            var cellWidth = (462f - 8f * (columns - 1)) / columns;
            UIBuilder.AddGrid(_metricGrid, new Vector2(cellWidth, 66f), new Vector2(8f, 8f), columns);
            UIBuilder.SetHeight(_metricGrid, 140f);

            _score = UIBuilder.CreateMetricTile(_metricGrid, "Score", "Score", "0");
            _time = UIBuilder.CreateMetricTile(_metricGrid, "Time", "Time", "0:00");
            _kills = UIBuilder.CreateMetricTile(_metricGrid, "Kills", "Kills", "0");
            _parts = UIBuilder.CreateMetricTile(_metricGrid, "Parts", "Parts", "+0");
            _level = UIBuilder.CreateMetricTile(_metricGrid, "Level", "Level", "1");
            _bosses = UIBuilder.CreateMetricTile(_metricGrid, "Bosses", "Bosses", "0");
        }

        /// <summary>The .build-recap panel holding one chip per owned upgrade.</summary>
        private void BuildRecap(RectTransform parent)
        {
            _chipPanel = CreateInnerPanel(parent, "BuildRecap", "Final build", out var content);
            _chipGrid = content;
            var layout = _chipGrid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(146f, 28f);
            layout.spacing = new Vector2(6f, 6f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = ChipColumns;
            layout.childAlignment = TextAnchor.UpperLeft;
        }

        /// <summary>The .damage-breakdown panel, one row per weapon.</summary>
        private void BuildDamage(RectTransform parent)
        {
            _damagePanel = CreateInnerPanel(parent, "DamageBreakdown", "Damage by weapon", out var content);
            _damageList = content;
            UIBuilder.AddVerticalLayout(_damageList, 4f);
        }

        private static RectTransform CreateInnerPanel(
            RectTransform parent,
            string name,
            string heading,
            out RectTransform content)
        {
            var panel = UIBuilder.CreateRect(parent, name);

            var surface = UIBuilder.CreateSurface(panel, "Body", UISprites.Rounded(
                UITheme.RadiusSmall,
                UITheme.InnerPanel,
                UITheme.InnerPanel,
                UITheme.BorderInner));
            UIBuilder.Stretch(surface.rectTransform);

            var label = UIBuilder.CreateText(
                panel,
                "Heading",
                heading.ToUpperInvariant(),
                10f,
                UITheme.TextSubtle,
                TextAnchor.UpperLeft,
                true,
                FontStyle.Bold,
                0.10f);
            label.rectTransform.anchorMin = new Vector2(0f, 1f);
            label.rectTransform.anchorMax = new Vector2(1f, 1f);
            label.rectTransform.pivot = new Vector2(0.5f, 1f);
            label.rectTransform.sizeDelta = new Vector2(-24f, 14f);
            label.rectTransform.anchoredPosition = new Vector2(0f, -12f);

            // Anchored to the top with an explicit height set after populating.
            // Letting a ContentSizeFitter drive this fought the parent layout
            // group, which owns the panel's height.
            content = UIBuilder.CreateRect(panel, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(-24f, 0f);
            content.anchoredPosition = new Vector2(0f, -32f);

            UIBuilder.SetHeight(panel, HeaderHeight + 12f);
            return panel;
        }

        /// <summary>
        /// Resizes an inner panel and its content to hold the given number of
        /// stacked rows.
        /// </summary>
        private static void SizeInnerPanel(
            RectTransform panel,
            RectTransform content,
            int rows,
            float rowHeight,
            float rowSpacing)
        {
            if (panel == null || content == null) return;
            var visibleRows = Mathf.Max(rows, 1);
            var contentHeight = visibleRows * rowHeight + Mathf.Max(0, visibleRows - 1) * rowSpacing;
            content.sizeDelta = new Vector2(content.sizeDelta.x, contentHeight);
            UIBuilder.SetHeight(panel, HeaderHeight + contentHeight + 12f);
        }

        private void BuildActions(RectTransform parent)
        {
            var stack = UIBuilder.CreateRect(parent, "Actions");
            stack.anchorMin = new Vector2(0f, 0f);
            stack.anchorMax = new Vector2(1f, 0f);
            stack.pivot = new Vector2(0.5f, 0f);
            stack.sizeDelta = new Vector2(0f, 104f);
            UIBuilder.AddVerticalLayout(stack, 8f);

            var again = UIBuilder.CreatePrimaryAction(
                stack, "PlayAgain", "Play again", null, () => Callbacks?.RestartRun?.Invoke(), 50f);
            UIBuilder.SetHeight(again.GetComponent<RectTransform>(), 50f);

            var menu = UIBuilder.CreateSecondaryAction(
                stack, "MainMenu", "Main menu", null, () => Callbacks?.AbortToMenu?.Invoke(), 44f);
            UIBuilder.SetHeight(menu.GetComponent<RectTransform>(), 44f);
        }

        /// <summary>Populates and opens the result screen.</summary>
        public void Show(GameOverSummary summary)
        {
            // The kicker carries emulated tracking, so it goes through SetText.
            UIBuilder.SetText(_kicker, summary.Victory ? "RUN COMPLETE" : "RUN ENDED");
            if (_title != null)
            {
                _title.text = summary.Victory ? "Abyss held" : "Try another build";
                _title.color = summary.Victory ? UITheme.CyanPale : UITheme.TextHeading;
            }

            if (_score != null) _score.text = FormatNumber(summary.Score);
            if (_time != null) _time.text = FormatTime(summary.ElapsedSeconds);
            if (_kills != null) _kills.text = FormatNumber(summary.Kills);
            if (_parts != null) _parts.text = "+" + FormatNumber(summary.PartsEarned);
            if (_level != null) _level.text = FormatNumber(summary.Level);
            if (_bosses != null) _bosses.text = FormatNumber(summary.BossKills);

            PopulateBadges(summary);
            PopulateChips(summary.BuildChips);
            PopulateDamage(summary.Weapons);

            SetVisible(true);
        }

        private void PopulateBadges(GameOverSummary summary)
        {
            ClearChildren(_badgeRow);

            if (summary.IsBest)
            {
                var badge = UIBuilder.CreateBadge(
                    _badgeRow,
                    "NewBest",
                    "New best",
                    UITheme.GoldLight,
                    UITheme.BestFill,
                    UITheme.BorderBest,
                    true);
                badge.sizeDelta = new Vector2(120f, 28f);
                var element = badge.gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = 120f;
                element.preferredHeight = 28f;
            }

            if (!summary.Saved)
            {
                var badge = UIBuilder.CreateBadge(
                    _badgeRow,
                    "SaveWarning",
                    "Progress was not saved",
                    UITheme.RosePale,
                    UITheme.WarningFill,
                    UITheme.BorderWarning,
                    false);
                badge.sizeDelta = new Vector2(230f, 28f);
                var element = badge.gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = 230f;
                element.preferredHeight = 28f;
            }
        }

        private void PopulateChips(List<UIBuildChip> chips)
        {
            ClearChildren(_chipGrid);
            var count = chips?.Count ?? 0;

            for (var index = 0; index < count; index++)
            {
                var chip = chips[index];
                UIBuilder.CreateChip(
                    _chipGrid,
                    "Chip" + index.ToString(),
                    chip.Name,
                    chip.Rank.ToString(),
                    chip.Accent.a <= 0f ? UITheme.CyanLight : chip.Accent,
                    chip.Evolved);
            }

            var rows = Mathf.CeilToInt(count / (float)ChipColumns);
            SizeInnerPanel(_chipPanel, _chipGrid, rows, 28f, 6f);
        }

        private void PopulateDamage(List<WeaponStatSummary> weapons)
        {
            ClearChildren(_damageList);
            var count = weapons?.Count ?? 0;
            SizeInnerPanel(_damagePanel, _damageList, count, 26f, 4f);
            if (count == 0) return;

            for (var index = 0; index < weapons.Count; index++)
            {
                var weapon = weapons[index];
                var row = UIBuilder.CreateRect(_damageList, "Weapon" + index.ToString());
                UIBuilder.SetHeight(row, 26f);

                var name = UIBuilder.CreateText(
                    row,
                    "Name",
                    weapon.Name,
                    13f,
                    UITheme.TextChip,
                    TextAnchor.MiddleLeft,
                    false);
                name.rectTransform.offsetMax = new Vector2(-104f, 0f);

                var value = UIBuilder.CreateText(
                    row,
                    "Value",
                    FormatNumber(weapon.Damage),
                    13f,
                    UITheme.TextBrightest,
                    TextAnchor.MiddleRight,
                    true,
                    FontStyle.Bold);
                value.rectTransform.offsetMin = new Vector2(0f, 0f);

                // A subtle share bar under the row, in place of the browser's
                // tabular-nums alignment cue.
                var track = UIBuilder.CreateFill(row, "Track", UITheme.WithAlpha(UITheme.PipEmpty, 0.35f));
                track.rectTransform.anchorMin = new Vector2(0f, 0f);
                track.rectTransform.anchorMax = new Vector2(1f, 0f);
                track.rectTransform.pivot = new Vector2(0f, 0f);
                track.rectTransform.sizeDelta = new Vector2(0f, 2f);
                track.rectTransform.anchoredPosition = Vector2.zero;

                var fill = UIBuilder.CreateFill(track.rectTransform, "Fill", UITheme.WithAlpha(UITheme.CyanLight, 0.55f));
                fill.rectTransform.anchorMin = Vector2.zero;
                fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(weapon.DamagePercent), 1f);
                fill.rectTransform.offsetMin = Vector2.zero;
                fill.rectTransform.offsetMax = Vector2.zero;
            }
        }
    }
}
