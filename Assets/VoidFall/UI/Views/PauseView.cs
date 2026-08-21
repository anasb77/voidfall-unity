using UnityEngine;
using UnityEngine.UI;

namespace VoidFall.UI
{
    /// <summary>A compact snapshot of the run in progress, shown while paused.</summary>
    public struct UIRunSnapshot
    {
        public int Score;
        public float ElapsedSeconds;
        public int Kills;
        public int Level;
        public int PartsEarned;
        public int BossKills;
    }

    /// <summary>
    /// The pause overlay, rebuilt from .pause-card: Resume, Restart, Main menu.
    ///
    /// It also carries a small run summary. The IMGUI build had a separate
    /// Tab-key "overview" page for that, which has no counterpart in the browser
    /// original; folding the figures in here keeps the information without a
    /// second screen to navigate.
    /// </summary>
    public sealed class PauseView : UIViewBase
    {
        private Text _score;
        private Text _time;
        private Text _kills;
        private Text _level;
        private Text _parts;
        private Text _bosses;

        protected override void Build()
        {
            UIBuilder.CreateScrim(Root, "Scrim", UITheme.OverlayScrim);

            var content = UIBuilder.CreateOverlayCard(
                Root,
                "Card",
                new Vector2(430f, 520f),
                "Run paused",
                "Paused",
                out _);

            BuildSummary(content);
            BuildActions(content);
        }

        private void BuildSummary(RectTransform parent)
        {
            var grid = UIBuilder.CreateRect(parent, "Summary");
            grid.anchorMin = new Vector2(0f, 1f);
            grid.anchorMax = new Vector2(1f, 1f);
            grid.pivot = new Vector2(0.5f, 1f);
            grid.sizeDelta = new Vector2(0f, 140f);

            const int columns = 3;
            var cellWidth = (380f - 8f * (columns - 1)) / columns;
            UIBuilder.AddGrid(grid, new Vector2(cellWidth, 66f), new Vector2(8f, 8f), columns);

            _score = UIBuilder.CreateMetricTile(grid, "Score", "Score", "0");
            _time = UIBuilder.CreateMetricTile(grid, "Time", "Time", "0:00");
            _kills = UIBuilder.CreateMetricTile(grid, "Kills", "Kills", "0");
            _level = UIBuilder.CreateMetricTile(grid, "Level", "Level", "1");
            _parts = UIBuilder.CreateMetricTile(grid, "Parts", "Parts", "+0");
            _bosses = UIBuilder.CreateMetricTile(grid, "Bosses", "Bosses", "0");
        }

        private void BuildActions(RectTransform parent)
        {
            var stack = UIBuilder.CreateRect(parent, "Actions");
            stack.anchorMin = new Vector2(0f, 0f);
            stack.anchorMax = new Vector2(1f, 1f);
            stack.offsetMin = Vector2.zero;
            stack.offsetMax = new Vector2(0f, -152f);
            UIBuilder.AddVerticalLayout(stack, 9f);

            var resume = UIBuilder.CreatePrimaryAction(
                stack, "Resume", "Resume", null, () => Callbacks?.ResumeRun?.Invoke(), 52f);
            UIBuilder.SetHeight(resume.GetComponent<RectTransform>(), 52f);

            var restart = UIBuilder.CreateSecondaryAction(
                stack, "Restart", "Restart", null, () => Callbacks?.RestartRun?.Invoke(), 46f);
            UIBuilder.SetHeight(restart.GetComponent<RectTransform>(), 46f);

            var menu = UIBuilder.CreateSecondaryAction(
                stack, "MainMenu", "Main menu", null, () => Callbacks?.AbortToMenu?.Invoke(), 46f);
            UIBuilder.SetHeight(menu.GetComponent<RectTransform>(), 46f);
        }

        /// <summary>Refreshes the summary figures. Called as the overlay opens.</summary>
        public void UpdateSnapshot(UIRunSnapshot snapshot)
        {
            if (_score != null) _score.text = FormatNumber(snapshot.Score);
            if (_time != null) _time.text = FormatTime(snapshot.ElapsedSeconds);
            if (_kills != null) _kills.text = FormatNumber(snapshot.Kills);
            if (_level != null) _level.text = FormatNumber(snapshot.Level);
            if (_parts != null) _parts.text = "+" + FormatNumber(snapshot.PartsEarned);
            if (_bosses != null) _bosses.text = FormatNumber(snapshot.BossKills);
        }
    }
}
