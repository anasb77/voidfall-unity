using System;
using System.Linq;
using NUnit.Framework;
using VoidFall.Core;

namespace VoidFall.Tests.Editor
{
    public sealed class NullCityRulesTests
    {
        [TestCase(0, NullCityCycle.Surveillance, 0)]
        [TestCase(11, NullCityCycle.Surveillance, 0.5)]
        [TestCase(21.999, NullCityCycle.Surveillance, 21.999 / 22)]
        [TestCase(22, NullCityCycle.Lockdown, 0)]
        [TestCase(34, NullCityCycle.Lockdown, 0.5)]
        [TestCase(46, NullCityCycle.Surveillance, 0)]
        public void Normal_cycle_wraps_between_surveillance_and_lockdown(
            double elapsed,
            NullCityCycle expectedCycle,
            double expectedProgress)
        {
            Assert.That(NullCityRules.CycleAt(elapsed, false), Is.EqualTo(expectedCycle));
            Assert.That(NullCityRules.CycleProgress(elapsed, false), Is.EqualTo(expectedProgress).Within(0.000001));
        }

        [TestCase(0)]
        [TestCase(23.999)]
        [TestCase(24)]
        [TestCase(49)]
        public void Motherload_encounter_remains_in_lockdown(double elapsed)
        {
            Assert.That(NullCityRules.CycleAt(elapsed, true), Is.EqualTo(NullCityCycle.Lockdown));
            Assert.That(NullCityRules.CycleProgress(elapsed, true), Is.InRange(0, 1));
        }

        [Test]
        public void Invalid_elapsed_is_safely_treated_as_zero()
        {
            Assert.That(NullCityRules.CycleAt(double.NaN, false), Is.EqualTo(NullCityCycle.Surveillance));
            Assert.That(NullCityRules.CycleProgress(double.PositiveInfinity, true), Is.Zero);
            Assert.That(NullCityRules.PurgeAt(-5, false).Visible, Is.False);
        }

        [Test]
        public void Purge_uses_two_horizontal_then_double_and_solo_vertical_lanes()
        {
            var first = NullCityRules.PurgeAt(22, false);
            var second = NullCityRules.PurgeAt(28, false);
            var third = NullCityRules.PurgeAt(34, false);
            var fourth = NullCityRules.PurgeAt(40, false);

            AssertPurge(first, 0, 180, 311, 1240, 68);
            AssertPurge(second, 1, 180, 558, 1240, 68);
            AssertPurge(third, 2, 956, 218, 54, 527);
            AssertPurge(fourth, 3, 488, 218, 54, 527);
        }

        [Test]
        public void Double_vertical_lane_alternates_each_normal_lockdown_pass()
        {
            Assert.That(NullCityRules.PurgeAt(34, false).X, Is.EqualTo(956));
            Assert.That(NullCityRules.PurgeAt(80, false).X, Is.EqualTo(1030));
            Assert.That(NullCityRules.PurgeAt(126, false).X, Is.EqualTo(956));
        }

        [Test]
        public void Double_vertical_lane_alternates_each_boss_lockdown_pass()
        {
            Assert.That(NullCityRules.PurgeAt(12, true).X, Is.EqualTo(956));
            Assert.That(NullCityRules.PurgeAt(36, true).X, Is.EqualTo(1030));
            Assert.That(NullCityRules.PurgeAt(60, true).X, Is.EqualTo(956));
        }

        [Test]
        public void Purge_warns_then_activates_then_disappears_for_each_beat()
        {
            var warning = NullCityRules.PurgeAt(23, false);
            var active = NullCityRules.PurgeAt(24.4, false);
            var gone = NullCityRules.PurgeAt(26.301, false);

            Assert.That(warning.Visible, Is.True);
            Assert.That(warning.Active, Is.False);
            Assert.That(warning.WarningRemaining, Is.EqualTo(1.4).Within(0.000001));
            Assert.That(active.Visible, Is.True);
            Assert.That(active.Active, Is.True);
            Assert.That(active.WarningRemaining, Is.Zero);
            Assert.That(gone.Visible, Is.False);
        }

        [Test]
        public void Purge_is_unavailable_during_surveillance()
        {
            Assert.That(NullCityRules.PurgeAt(0, false).Visible, Is.False);
            Assert.That(NullCityRules.PurgeAt(21.999, false).Visible, Is.False);
        }

        [Test]
        public void Tractor_cone_accepts_center_and_rejects_sideways_escape()
        {
            Assert.That(NullCityRules.IsInsideTractor(300, 0, 0), Is.True);
            Assert.That(NullCityRules.IsInsideTractor(300, 130, 0), Is.False);
            Assert.That(NullCityRules.IsInsideTractor(0, 300, Math.PI / 2), Is.True);
        }

        [Test]
        public void Tractor_cone_uses_open_distance_and_angle_boundaries()
        {
            var insideSide = 300 * Math.Tan(0.38) - 0.001;
            var outsideSide = 300 * Math.Tan(0.38) + 0.001;

            Assert.That(NullCityRules.IsInsideTractor(145, 0, 0), Is.False);
            Assert.That(NullCityRules.IsInsideTractor(145.001, 0, 0), Is.True);
            Assert.That(NullCityRules.IsInsideTractor(640, 0, 0), Is.False);
            Assert.That(NullCityRules.IsInsideTractor(300, insideSide, 0), Is.True);
            Assert.That(NullCityRules.IsInsideTractor(300, outsideSide, 0), Is.False);
            Assert.That(NullCityRules.IsInsideTractor(double.NaN, 0, 0), Is.False);
        }

        [Test]
        public void Motherload_moves_repeat_in_the_approved_order()
        {
            Assert.That(
                Enumerable.Range(0, 7).Select(NullCityRules.NextMotherloadMove),
                Is.EqualTo(new[]
                {
                    MotherloadMove.Cannons,
                    MotherloadMove.Tractor,
                    MotherloadMove.Brood,
                    MotherloadMove.Bombardment,
                    MotherloadMove.Vent,
                    MotherloadMove.Cannons,
                    MotherloadMove.Tractor,
                }));
        }

        [Test]
        public void Catalogue_has_unique_stable_ids_and_route_only_motherload()
        {
            var enemyIds = NullCityContent.Enemies.Select(enemy => enemy.Id).ToArray();

            Assert.That(NullCityContent.Arena.Id, Is.EqualTo(NullCityContent.StableId));
            Assert.That(enemyIds, Is.EqualTo(new[]
            {
                "null-patrol", "null-enforcer", "null-sentinel", "null-crawler",
                "null-volatile", "null-gunship", "null-mech", "null-broodmother",
                "null-light-gunship", "null-interceptor", "null-marshal", "null-suppressor",
            }));
            Assert.That(enemyIds.Distinct().Count(), Is.EqualTo(enemyIds.Length));
            Assert.That(NullCityContent.Motherload.Id, Is.EqualTo(NullCityContent.MotherloadId));
            Assert.That(NullCityContent.Motherload.StartsAtSeconds, Is.LessThan(0));
            Assert.That(NullCityContent.FindBoss(NullCityContent.MotherloadId), Is.SameAs(NullCityContent.Motherload));
        }

        [Test]
        public void Catalogue_preserves_approved_prototype_dimensions_and_normal_damage()
        {
            Assert.That(
                NullCityContent.Enemies.Select(enemy => (enemy.Health, enemy.Speed, enemy.Radius)),
                Is.EqualTo(new[]
                {
                    (44d, 70d, 13d), (175d, 32d, 22d), (85d, 20d, 17d),
                    (36d, 85d, 15d), (90d, 58d, 25d), (630d, 25d, 43d),
                    (760d, 24d, 39d), (840d, 18d, 57d), (240d, 48d, 27d),
                    (95d, 90d, 17d), (310d, 31d, 28d), (170d, 35d, 21d),
                }));
            Assert.That(NullCityContent.Enemies.Select(enemy => enemy.Xp), Has.All.GreaterThan(0));
            Assert.That(NullCityContent.FindEnemy("null-volatile").BlastRadius, Is.EqualTo(124));
            Assert.That(NullCityContent.FindEnemy("null-volatile").ContactDamage, Is.EqualTo(27));
            Assert.That(NullCityContent.FindEnemy("null-mech").ContactDamage, Is.EqualTo(29));
            Assert.That(NullCityContent.Motherload.Radius, Is.EqualTo(114));
            Assert.That(NullCityContent.Motherload.Health, Is.EqualTo(12000));
            Assert.That(NullCityContent.Motherload.Attacks.Single(attack => attack.Id == "null-bombardment").Damage,
                Is.EqualTo(28));
        }

        [Test]
        public void Catalogue_lookup_returns_index_or_absence_without_fallbacks()
        {
            Assert.That(NullCityContent.EnemyIndex("null-patrol"), Is.Zero);
            Assert.That(NullCityContent.EnemyIndex("null-suppressor"), Is.EqualTo(11));
            Assert.That(NullCityContent.EnemyIndex("missing"), Is.EqualTo(-1));
            Assert.That(NullCityContent.FindEnemy(null), Is.Null);
            Assert.That(NullCityContent.FindBoss("missing"), Is.Null);
            Assert.That(NullCityContent.FindArena("missing"), Is.Null);
        }

        private static void AssertPurge(
            NullCityPurge purge,
            int lane,
            double x,
            double y,
            double width,
            double height)
        {
            Assert.That(purge.Visible, Is.True);
            Assert.That(purge.Lane, Is.EqualTo(lane));
            Assert.That(purge.X, Is.EqualTo(x));
            Assert.That(purge.Y, Is.EqualTo(y));
            Assert.That(purge.Width, Is.EqualTo(width));
            Assert.That(purge.Height, Is.EqualTo(height));
        }
    }
}
