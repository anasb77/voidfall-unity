using System.Linq;
using NUnit.Framework;
using VoidFall.Core;

namespace VoidFall.Tests.Editor
{
    public sealed class HydraEncounterRulesTests
    {
        [Test]
        public void Evasion_order_is_a_six_socket_shuffle_bag_without_immediate_repeat()
        {
            var first = HydraEncounterRules.BuildEvasionOrder(new Rng(1729u), -1);
            var second = HydraEncounterRules.BuildEvasionOrder(new Rng(1730u), first[5]);

            Assert.That(first.Length, Is.EqualTo(HydraEncounterRules.EvasionSocketCount));
            Assert.That(first.Distinct().Count(), Is.EqualTo(HydraEncounterRules.EvasionSocketCount));
            Assert.That(first, Is.All.InRange(0, HydraEncounterRules.EvasionSocketCount - 1));
            Assert.That(second[0], Is.Not.EqualTo(first[5]));
        }

        [Test]
        public void Evasion_order_is_deterministic_for_the_same_seed()
        {
            Assert.That(
                HydraEncounterRules.BuildEvasionOrder(new Rng(4242u), 2),
                Is.EqualTo(HydraEncounterRules.BuildEvasionOrder(new Rng(4242u), 2)));
        }

        [Test]
        public void Marrow_barrage_uses_four_fair_randomized_drop_intervals()
        {
            var intervals = HydraEncounterRules.BuildMarrowIntervals(new Rng(901u));

            Assert.That(intervals.Length, Is.EqualTo(HydraEncounterRules.MarrowBombCount));
            Assert.That(intervals.Distinct().Count(), Is.EqualTo(intervals.Length));
            Assert.That(intervals, Is.All.InRange(
                HydraEncounterRules.MinMarrowIntervalSeconds,
                HydraEncounterRules.MaxMarrowIntervalSeconds));
            Assert.That(
                HydraEncounterRules.BuildMarrowIntervals(new Rng(901u)),
                Is.EqualTo(intervals));
        }

        [TestCase(0.04, 0.94, HydraDamageRegion.Crown)]
        [TestCase(0.82, 0.72, HydraDamageRegion.UpperRight)]
        [TestCase(0.18, 0.72, HydraDamageRegion.UpperLeft)]
        [TestCase(0.82, 0.24, HydraDamageRegion.LowerRight)]
        [TestCase(0.18, 0.24, HydraDamageRegion.LowerLeft)]
        [TestCase(0.50, 0.52, HydraDamageRegion.Eye)]
        public void Damage_regions_match_the_approved_destruction_order(
            double x,
            double y,
            HydraDamageRegion expected)
        {
            Assert.That(HydraEncounterRules.DamageRegion(x, y), Is.EqualTo(expected));
        }

        [Test]
        public void Hydra_content_is_route_owned_and_uses_the_four_approved_states()
        {
            Assert.That(HydraContent.Arena.Id, Is.EqualTo("hydra"));
            Assert.That(HydraContent.Boss.Id, Is.EqualTo("hydra-prime"));
            Assert.That(HydraContent.Boss.StartsAtSeconds, Is.LessThan(0),
                "Hydra Prime must never join the global endless boss schedule.");
            Assert.That(
                HydraContent.Boss.Attacks.Select(attack => attack.Id),
                Is.EqualTo(new[] { "hydra-marrow", "hydra-evasion", "hydra-ribs", "hydra-optic" }));
            Assert.That(
                HydraContent.Boss.Attacks[2].Radius,
                Is.EqualTo(HydraEncounterRules.RibProjectileRadius).Within(1e-9));
            Assert.That(HydraEncounterRules.RibProjectileVisualScale, Is.EqualTo(0.8).Within(1e-9));
        }

        [Test]
        public void Hydra_stable_id_round_trips_without_reordering_legacy_arenas()
        {
            Assert.That(ContentOrder.Arenas[0], Is.EqualTo(ArenaId.Void));
            Assert.That(ContentOrder.Arenas[1], Is.EqualTo(ArenaId.RedNebula));
            Assert.That(ContentOrder.Arenas[2], Is.EqualTo(ArenaId.WhiteSakura));
            Assert.That(ContentOrder.Arenas[3], Is.EqualTo(ArenaId.Hydra));
            Assert.That(ArenaCatalogRules.StableId(ArenaId.Hydra), Is.EqualTo("hydra"));
            Assert.That(ArenaCatalogRules.LegacyArena("hydra"), Is.EqualTo(ArenaId.Hydra));
        }
    }
}
