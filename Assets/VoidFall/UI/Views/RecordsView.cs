using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VoidFall.UI
{
    /// <summary>One row of the high-score table.</summary>
    public struct HighScoreRow
    {
        public int Score;
        public float Time;
        public int Level;
        public int Kills;
        public int BossKills;
    }

    /// <summary>The twelve lifetime figures shown above the high-score table.</summary>
    public struct UILifetimeStats
    {
        public int TotalRuns;
        public int TotalKills;
        public int BestScore;
        public float BestTime;
        public int TotalBossKills;
        public int TotalEliteKills;
        public float TotalPlaySeconds;
        public int TotalPartsEarned;
        public int BestKills;
        public int HighestLevel;
        public double TotalDamageDealt;
        public double TotalDamageTaken;
    }

    /// <summary>
    /// The local profile screen: a lifetime metric grid over the high-score
    /// table, rebuilt from .lifetime-grid and .score-table.
    ///
    /// The table is built as real aligned columns. The previous attempt rendered
    /// it as a single space-padded text blob, which cannot line up in a
    /// proportional font.
    /// </summary>
    public sealed class RecordsView : UIViewBase, IRecordsSink
    {
        private static readonly string[] ColumnHeaders = { "#", "Score", "Kills", "Time", "Bosses" };
        private static readonly float[] ColumnWidths = { 38f, 130f, 82f, 82f, 82f };

        private readonly Dictionary<string, Text> _metrics =
            new Dictionary<string, Text>();

        private RectTransform _content;
        private RectTransform _tableBody;
        private Text _emptyLabel;

        protected override void Build()
        {
            UIBuilder.CreateScrim(Root, "Blocker", new Color(0f, 0f, 0f, 0.0001f));

            var area = UIBuilder.CreateProfilePanel(
                Root,
                "Panel",
                new Vector2(660f, 700f),
                "Local profile",
                "Records",
                () => Callbacks?.CloseMenuPage?.Invoke(),
                out _);

            _content = UIBuilder.CreateScrollView(area, "Scroll", out _);
            UIBuilder.AddVerticalLayout(_content, 8f, new RectOffset(0, 0, 8, 8));

            BuildMetricGrid(_content);
            BuildTable(_content);
        }

        private void BuildMetricGrid(RectTransform parent)
        {
            var grid = UIBuilder.CreateRect(parent, "LifetimeGrid");
            const int columns = 3;
            const float cellHeight = 66f;
            var cellWidth = (616f - 8f * (columns - 1)) / columns;
            UIBuilder.AddGrid(grid, new Vector2(cellWidth, cellHeight), new Vector2(8f, 8f), columns);

            // Four rows of three.
            UIBuilder.SetHeight(grid, cellHeight * 4f + 8f * 3f);

            AddMetric(grid, "runs", "Runs");
            AddMetric(grid, "kills", "Kills");
            AddMetric(grid, "bestScore", "Best score");
            AddMetric(grid, "bestTime", "Longest run");
            AddMetric(grid, "bosses", "Bosses");
            AddMetric(grid, "elites", "Elites");
            AddMetric(grid, "totalTime", "Total time");
            AddMetric(grid, "parts", "Parts earned");
            AddMetric(grid, "bestKills", "Best kills");
            AddMetric(grid, "bestLevel", "Best level");
            AddMetric(grid, "damageDealt", "Damage dealt");
            AddMetric(grid, "damageTaken", "Damage taken");
        }

        private void AddMetric(RectTransform parent, string key, string label)
        {
            _metrics[key] = UIBuilder.CreateMetricTile(parent, "Metric." + key, label, "0");
        }

        private void BuildTable(RectTransform parent)
        {
            UIBuilder.SetHeight(
                UIBuilder.CreateSectionLabel(parent, "SectionLabel", "High scores").rectTransform,
                24f);

            var wrap = UIBuilder.CreateRect(parent, "TableWrap");
            var surface = UIBuilder.CreateSurface(wrap, "Body", UISprites.Rounded(
                UITheme.RadiusRow,
                new Color(0f, 0f, 0f, 0f),
                new Color(0f, 0f, 0f, 0f),
                UITheme.BorderRow));
            UIBuilder.Stretch(surface.rectTransform);

            var stack = UIBuilder.Stretch(UIBuilder.CreateRect(wrap, "Rows"), 1f);
            UIBuilder.AddVerticalLayout(stack, 0f);

            BuildHeaderRow(stack);

            _tableBody = UIBuilder.CreateRect(stack, "Body");
            UIBuilder.AddVerticalLayout(_tableBody, 0f);

            _emptyLabel = UIBuilder.CreateText(
                stack,
                "Empty",
                "No runs recorded yet.",
                12f,
                UITheme.TextEmpty,
                TextAnchor.MiddleCenter,
                false);
            UIBuilder.SetHeight(_emptyLabel.rectTransform, 64f);

            // Header plus eight rows plus the empty-state line.
            UIBuilder.SetHeight(wrap, 30f + 8f * 34f + 4f);
        }

        private static void BuildHeaderRow(RectTransform parent)
        {
            var row = UIBuilder.CreateRect(parent, "Header");
            UIBuilder.SetHeight(row, 30f);

            var fill = UIBuilder.CreateFill(row, "Fill", UITheme.TableHeader);
            UIBuilder.Stretch(fill.rectTransform);

            var cursor = 0f;
            for (var index = 0; index < ColumnHeaders.Length; index++)
            {
                var cell = UIBuilder.CreateText(
                    row,
                    "Column" + index.ToString(),
                    ColumnHeaders[index].ToUpperInvariant(),
                    9f,
                    UITheme.TextMetricLabel,
                    TextAnchor.MiddleLeft,
                    true,
                    FontStyle.Bold,
                    0.10f);
                cell.rectTransform.anchorMin = new Vector2(0f, 0f);
                cell.rectTransform.anchorMax = new Vector2(0f, 1f);
                cell.rectTransform.pivot = new Vector2(0f, 0.5f);
                cell.rectTransform.sizeDelta = new Vector2(ColumnWidths[index], 0f);
                cell.rectTransform.anchoredPosition = new Vector2(cursor + 10f, 0f);
                cursor += ColumnWidths[index];
            }
        }

        /// <summary>Applies the lifetime figures.</summary>
        public void PopulateLifetime(UILifetimeStats stats)
        {
            Set("runs", FormatNumber(stats.TotalRuns));
            Set("kills", FormatNumber(stats.TotalKills));
            Set("bestScore", FormatNumber(stats.BestScore));
            Set("bestTime", FormatTime(stats.BestTime));
            Set("bosses", FormatNumber(stats.TotalBossKills));
            Set("elites", FormatNumber(stats.TotalEliteKills));
            Set("totalTime", FormatTime(stats.TotalPlaySeconds));
            Set("parts", FormatNumber(stats.TotalPartsEarned));
            Set("bestKills", FormatNumber(stats.BestKills));
            Set("bestLevel", FormatNumber(stats.HighestLevel));
            Set("damageDealt", FormatNumber((long)stats.TotalDamageDealt));
            Set("damageTaken", FormatNumber((long)stats.TotalDamageTaken));
        }

        private void Set(string key, string value)
        {
            if (_metrics.TryGetValue(key, out var label) && label != null) label.text = value;
        }

        /// <summary>Rebuilds the high-score table, capped at the top eight runs.</summary>
        public void PopulateHighScores(IReadOnlyList<HighScoreRow> scores)
        {
            ClearChildren(_tableBody);

            var count = scores?.Count ?? 0;
            if (_emptyLabel != null) _emptyLabel.gameObject.SetActive(count == 0);
            if (count == 0) return;

            var shown = Mathf.Min(8, count);
            for (var index = 0; index < shown; index++)
            {
                BuildScoreRow(index, scores[index], index > 0);
            }
        }

        private void BuildScoreRow(int index, HighScoreRow score, bool divider)
        {
            var row = UIBuilder.CreateRect(_tableBody, "Row" + index.ToString());
            UIBuilder.SetHeight(row, 34f);

            if (divider)
            {
                UIBuilder.CreateRule(row, "Divider", UITheme.BorderDivider)
                    .rectTransform.anchoredPosition = new Vector2(0f, 34f);
            }

            var values = new[]
            {
                (index + 1).ToString(),
                FormatNumber(score.Score),
                FormatNumber(score.Kills),
                FormatTime(score.Time),
                FormatNumber(score.BossKills)
            };

            var cursor = 0f;
            for (var column = 0; column < values.Length; column++)
            {
                // The score column is emphasised, matching td:nth-child(2).
                var isScore = column == 1;
                var cell = UIBuilder.CreateText(
                    row,
                    "Cell" + column.ToString(),
                    values[column],
                    11f,
                    isScore ? UITheme.ScoreValue : UITheme.TextChip,
                    TextAnchor.MiddleLeft,
                    isScore,
                    isScore ? FontStyle.Bold : FontStyle.Normal);
                cell.rectTransform.anchorMin = new Vector2(0f, 0f);
                cell.rectTransform.anchorMax = new Vector2(0f, 1f);
                cell.rectTransform.pivot = new Vector2(0f, 0.5f);
                cell.rectTransform.sizeDelta = new Vector2(ColumnWidths[column], 0f);
                cell.rectTransform.anchoredPosition = new Vector2(cursor + 10f, 0f);
                cursor += ColumnWidths[column];
            }
        }
    }
}
