using System.Collections.Generic;
using VoidFall.Persistence;

namespace VoidFall.UI
{
    /// <summary>Write-side contract so the controller can be tested without uGUI.</summary>
    public interface IRecordsSink
    {
        void PopulateHighScores(IReadOnlyList<HighScoreRow> rows);
        void PopulateLifetime(UILifetimeStats stats);
    }

    /// <summary>
    /// Projects the persisted profile into the records screen: lifetime
    /// metrics grid over the high-score table. Wave 2 of the
    /// menu-controllers migration; mapping is verbatim from the runtime.
    /// </summary>
    public sealed class RecordsController
    {
        private readonly IRecordsSink _view;

        public RecordsController(IRecordsSink view)
        {
            _view = view;
        }

        /// <summary>
        /// Null guards mirror the original flow: an absent high-score list
        /// still populates an empty table, but absent stats skip the metric
        /// grid entirely.
        /// </summary>
        public void Refresh(IReadOnlyList<HighScoreEntry> scores, LifetimeStats stats)
        {
            var rows = new List<HighScoreRow>();
            if (scores != null)
            {
                foreach (var entry in scores)
                {
                    if (entry == null) continue;
                    rows.Add(new HighScoreRow
                    {
                        Score = entry.score,
                        Time = entry.time,
                        Level = entry.level,
                        Kills = entry.kills,
                        BossKills = entry.bossKills
                    });
                }
            }
            _view.PopulateHighScores(rows);

            if (stats == null) return;
            _view.PopulateLifetime(new UILifetimeStats
            {
                TotalRuns = stats.totalRuns,
                TotalKills = stats.totalKills,
                BestScore = stats.bestScore,
                BestTime = stats.bestTime,
                TotalBossKills = stats.totalBossKills,
                TotalEliteKills = stats.totalEliteKills,
                TotalPlaySeconds = stats.totalPlaySeconds,
                TotalPartsEarned = stats.totalPartsEarned,
                BestKills = stats.bestKills,
                HighestLevel = stats.highestLevel,
                TotalDamageDealt = stats.totalDamageDealt,
                TotalDamageTaken = stats.totalDamageTaken
            });
        }
    }
}
