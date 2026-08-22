using System.Collections.Generic;
using NUnit.Framework;
using VoidFall.Persistence;
using VoidFall.UI;

namespace VoidFall.Tests.Editor
{
    /// <summary>
    /// Covers the records projection: null guards and the field-by-field copy
    /// from the persisted profile into the screen's view models.
    /// </summary>
    public sealed class RecordsControllerTests
    {
        private sealed class FakeSink : IRecordsSink
        {
            public IReadOnlyList<HighScoreRow> Rows;
            public int LifetimeCalls;
            public UILifetimeStats LastStats;

            public void PopulateHighScores(IReadOnlyList<HighScoreRow> rows) => Rows = rows;
            public void PopulateLifetime(UILifetimeStats stats) { LifetimeCalls++; LastStats = stats; }
        }

        private static HighScoreEntry Entry(int score, int kills, int time, int level, int bossKills)
        {
            return new HighScoreEntry
            {
                score = score,
                kills = kills,
                time = time,
                level = level,
                bossKills = bossKills
            };
        }

        [Test]
        public void Null_high_score_list_still_populates_an_empty_table()
        {
            var sink = new FakeSink();
            var controller = new RecordsController(sink);

            controller.Refresh(null, new LifetimeStats());

            Assert.That(sink.Rows, Is.Not.Null);
            Assert.That(sink.Rows.Count, Is.EqualTo(0));
        }

        [Test]
        public void Null_entries_inside_the_list_are_skipped()
        {
            var sink = new FakeSink();
            var controller = new RecordsController(sink);
            var scores = new List<HighScoreEntry> { Entry(10, 1, 5, 2, 0), null, Entry(20, 2, 6, 3, 1) };

            controller.Refresh(scores, new LifetimeStats());

            Assert.That(sink.Rows.Count, Is.EqualTo(2));
            Assert.That(sink.Rows[0].Score, Is.EqualTo(10));
            Assert.That(sink.Rows[1].Score, Is.EqualTo(20));
        }

        [Test]
        public void Stats_null_skips_the_lifetime_grid()
        {
            var sink = new FakeSink();
            var controller = new RecordsController(sink);

            controller.Refresh(new List<HighScoreEntry>(), null);

            Assert.That(sink.LifetimeCalls, Is.EqualTo(0));
        }

        [Test]
        public void Lifetime_stats_are_copied_field_by_field()
        {
            var sink = new FakeSink();
            var controller = new RecordsController(sink);
            var stats = new LifetimeStats
            {
                totalRuns = 14,
                totalKills = 38528,
                bestScore = 141721,
                bestTime = 1348,
                totalBossKills = 28,
                totalEliteKills = 61,
                totalPlaySeconds = 8876,
                totalPartsEarned = 2323,
                bestKills = 12097,
                highestLevel = 44,
                totalDamageDealt = 9817594L,
                totalDamageTaken = 8013L
            };

            controller.Refresh(new List<HighScoreEntry>(), stats);

            Assert.That(sink.LifetimeCalls, Is.EqualTo(1));
            var s = sink.LastStats;
            Assert.That(s.TotalRuns, Is.EqualTo(14));
            Assert.That(s.TotalKills, Is.EqualTo(38528));
            Assert.That(s.BestScore, Is.EqualTo(141721));
            Assert.That(s.BestTime, Is.EqualTo(1348f));
            Assert.That(s.TotalBossKills, Is.EqualTo(28));
            Assert.That(s.TotalEliteKills, Is.EqualTo(61));
            Assert.That(s.TotalPlaySeconds, Is.EqualTo(8876));
            Assert.That(s.TotalPartsEarned, Is.EqualTo(2323));
            Assert.That(s.BestKills, Is.EqualTo(12097));
            Assert.That(s.HighestLevel, Is.EqualTo(44));
            Assert.That(s.TotalDamageDealt, Is.EqualTo(9817594d).Within(1d));
            Assert.That(s.TotalDamageTaken, Is.EqualTo(8013d).Within(1d));
        }
    }
}
