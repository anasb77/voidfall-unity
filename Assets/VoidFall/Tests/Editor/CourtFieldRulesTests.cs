using NUnit.Framework;
using VoidFall.Core;

namespace VoidFall.Tests.Editor
{
    public sealed class CourtFieldRulesTests
    {
        [TestCase(0, 50, 0)]
        [TestCase(.04, 50, 1)]
        [TestCase(1, 50, 25)]
        [TestCase(2, 50, 50)]
        [TestCase(3.4, 50, 50)]
        [TestCase(-1, 50, 0)]
        [TestCase(1, 0, 0)]
        public void Local_cells_arm_in_order_over_two_seconds(double age, int total, int expected) =>
            Assert.That(MonochromeEncounterRules.ArmedCellCount(age, total), Is.EqualTo(expected));

        [TestCase(3, 1, true)]
        [TestCase(3, 2, false)]
        [TestCase(4, 0, false)]
        [TestCase(-3, -1, true)]
        public void Scope_is_limited_to_approved_three_tile_neighborhood(int dx, int dy, bool expected) =>
            Assert.That(MonochromeEncounterRules.InLocalScope(dx, dy), Is.EqualTo(expected));

        [TestCase(0, 100000)]
        [TestCase(.5, 125000)]
        [TestCase(1, 150000)]
        public void Sentinel_health_uses_separate_approved_range(double roll, int expected) =>
            Assert.That(MonochromeEncounterRules.RookHealth(roll), Is.EqualTo(expected));

        [Test]
        public void Warning_is_longer_than_arming_and_cycle_alternates_every_five_seconds()
        {
            Assert.That(MonochromeEncounterRules.TileSize, Is.EqualTo(129.6));
            Assert.That(MonochromeEncounterRules.RookMinimumSpacing, Is.EqualTo(630));
            Assert.That(MonochromeEncounterRules.HazardAt(2, false).Stage, Is.EqualTo(CourtHazardStage.Warning));
            Assert.That(MonochromeEncounterRules.HazardAt(3.4, true).Stage, Is.EqualTo(CourtHazardStage.Burning));
            Assert.That(MonochromeEncounterRules.HazardAt(5, true).Faction, Is.EqualTo(CourtFaction.Black));
        }
    }
}
