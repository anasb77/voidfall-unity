using NUnit.Framework;
using VoidFall.Core;
using VoidFall.Persistence;

namespace VoidFall.Tests
{
    public sealed class DirectorPersistenceTests
    {
        [Test]
        public void VersionFiveUpgradeDoesNotRepeatHistoricalProtocolRefund()
        {
            var save = SaveStore.CreateDefault();
            save.version = 5;
            save.parts = 40;
            save.workshop = new[] { new WorkshopEntry { id = "protocol", rank = 3 } };
            SaveStore.Sanitize(save);
            Assert.That(save.parts, Is.EqualTo(40));
            save.version = 4;
            save.workshop = new[] { new WorkshopEntry { id = "protocol", rank = 3 } };
            SaveStore.Sanitize(save);
            Assert.That(save.parts, Is.EqualTo(400));
            SaveStore.Sanitize(save);
            Assert.That(save.parts, Is.EqualTo(400));
        }

        [Test]
        public void NewScoresRankAndRoundTripWithoutLosingLongPrecision()
        {
            const long value = 9007199254740993L;
            var save = SaveStore.CreateDefault();
            save.directorId = (int)DirectorProfileId.Extreme;
            save.directorOnboardingSeen = true;
            save.highScores = new[] {
                new HighScoreEntry { score = 900, scoringVersion = 0 },
                new HighScoreEntry { score = 1, baseScore = value, finalScore = value, scoringVersion = 1,
                    directorId = 2, pressureHundredths = 50, multiplierHundredths = 100 }
            };
            var json = BrowserSaveExporter.Export(save);
            Assert.That(BrowserSaveImporter.TryConvert(json, out var loaded), Is.True);
            SaveStore.Sanitize(loaded);
            Assert.That(loaded.highScores[0].finalScore, Is.EqualTo(value));
            Assert.That(loaded.highScores[0].baseScore, Is.EqualTo(value));
            Assert.That(loaded.highScores[1].score, Is.EqualTo(900));
            Assert.That(loaded.highScores[1].scoringVersion, Is.Zero);
            Assert.That(loaded.directorId, Is.EqualTo(2));
            Assert.That(loaded.directorOnboardingSeen, Is.True);
        }
    }
}
