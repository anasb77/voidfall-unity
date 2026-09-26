using NUnit.Framework;
using VoidFall.Core;

namespace VoidFall.Tests
{
    public class RosterProgressionTests
    {
        [TestCase(540, 0, EnemyRoster.One)]
        [TestCase(720, .49, EnemyRoster.Two)]
        [TestCase(720, .51, EnemyRoster.One)]
        [TestCase(900, 1, EnemyRoster.Two)]
        [TestCase(1440, 0, EnemyRoster.Two)]
        [TestCase(1620, .49, EnemyRoster.Three)]
        [TestCase(1800, 1, EnemyRoster.Three)]
        [TestCase(2040, 0, EnemyRoster.Three)]
        [TestCase(2220, .49, EnemyRoster.Four)]
        [TestCase(2400, 1, EnemyRoster.Four)]
        public void GlobalRunClockTransitions(double seconds, double roll, EnemyRoster expected)
        {
            Assert.That(EnemyRosterRules.EnemyRosterForSpawn("runner", seconds, roll), Is.EqualTo(expected));
        }

        [Test]
        public void SharedDirectorTierPolicyHonorsLearningGraceAndVisitRamp()
        {
            Assert.That(EnemyRosterRules.SharedRosterForSpawn("runner", 0, 300, 59, 1200, 0), Is.EqualTo(EnemyRoster.One));
            Assert.That(EnemyRosterRules.SharedRosterForSpawn("runner", 0, 300, 60, 1200, .06), Is.EqualTo(EnemyRoster.Two));
            Assert.That(EnemyRosterRules.SharedRosterForSpawn("runner", 1, 0, 60, 1200, .14), Is.EqualTo(EnemyRoster.Two));
            Assert.That(EnemyRosterRules.SharedRosterForSpawn("runner", 1, 0, 60, 1200, .16), Is.EqualTo(EnemyRoster.One));
            Assert.That(EnemyRosterRules.SharedRosterForSpawn("runner", 1, 360, 60, 1200, .49), Is.EqualTo(EnemyRoster.Two));
            Assert.That(EnemyRosterRules.SharedRosterForSpawn("runner", 2, 0, 60, 900, 0), Is.EqualTo(EnemyRoster.Two));
        }

        [TestCase("hydra")][TestCase("pawn")][TestCase("null-marshal")][TestCase("elite")]
        public void ExclusiveAndStandardEliteIdsNeverEnterSharedProgression(string id)
        {
            Assert.That(EnemyRosterRules.EnemyRosterForSpawn(id, 3000, 0), Is.EqualTo(EnemyRoster.One));
        }

        [Test]
        public void ApprovedSupportAndChildContracts()
        {
            Assert.That(RosterProgressionTraits.Get("technician", EnemyRoster.Four).HealCount, Is.EqualTo(6));
            Assert.That(RosterProgressionTraits.Get("splitter", EnemyRoster.Three).ChildTier, Is.EqualTo(2));
            Assert.That(RosterProgressionTraits.Get("carrier", EnemyRoster.Four).ChildTier, Is.EqualTo(3));
            Assert.That(RosterProgressionTraits.Get("chaser", EnemyRoster.Four).DashCount, Is.Zero);
            Assert.That(RosterProgressionTraits.Get("gunner", EnemyRoster.Four, true).CurveGapSlots, Is.EqualTo(5));
            Assert.That(RosterProgressionTraits.Get("gunner", EnemyRoster.Four, true).ShotCount, Is.EqualTo(4));
        }

        [TestCase("chaser")][TestCase("runner")][TestCase("gunner")][TestCase("twinGunner")]
        [TestCase("dasher")][TestCase("brute")][TestCase("exploder")][TestCase("guard")]
        [TestCase("technician")][TestCase("mortar")][TestCase("splitter")][TestCase("bulwark")]
        [TestCase("harvester")][TestCase("carrier")]
        public void EverySharedFamilyReachesFinalTier(string id)
        {
            Assert.That(EnemyRosterRules.EnemyRosterForSpawn(id, 2400, 1), Is.EqualTo(EnemyRoster.Four));
        }

        [Test]
        public void RegularDisplayNamePreservesHistoricalIdentity()
        {
            var regular = System.Array.Find(ContentCatalog.Enemies, e => e.Id == "chaser");
            Assert.That(regular.Name, Is.EqualTo("Regular"));
            Assert.That(regular.Id, Is.EqualTo("chaser"));
        }
    }
}
