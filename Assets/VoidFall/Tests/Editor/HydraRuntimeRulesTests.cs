using NUnit.Framework;
using UnityEngine;
using VoidFall.Core;
using VoidFall.Runtime;

namespace VoidFall.Tests.Editor
{
    public sealed class HydraRuntimeRulesTests
    {
        [Test]
        public void Hydra_objective_rejects_matriarch_and_completes_for_hydra_prime()
        {
            var objective = VoidObjectives.ForArena("hydra");
            objective.BeginObjective();
            objective.TickObjective(300, new VoidObjectiveFeed());
            objective.TickObjective(0, new VoidObjectiveFeed());

            objective.TickObjective(0, new VoidObjectiveFeed { KilledId = "matriarch" });
            Assert.That(objective.IsComplete, Is.False);
            objective.TickObjective(0, new VoidObjectiveFeed { KilledId = "hydra-prime" });
            Assert.That(objective.IsComplete, Is.True);
        }

        [Test]
        public void Rib_cage_collision_is_active_only_during_the_boss_phase()
        {
            var centre = new Vector2(20f, -10f);
            var outside = centre + new Vector2(700f, 0f);

            Assert.That(
                HydraRuntimeRules.ClampPlayerToRibCage(outside, centre, 12f, false),
                Is.EqualTo(outside));
            var constrained = HydraRuntimeRules.ClampPlayerToRibCage(outside, centre, 12f, true);
            Assert.That(constrained.x, Is.LessThan(centre.x + HydraRuntimeRules.RibCageHalfWidth));
        }

        [Test]
        public void Central_vertebral_chain_never_changes_player_position()
        {
            var centre = Vector2.zero;
            var onSpine = new Vector2(0f, 70f);
            Assert.That(
                HydraRuntimeRules.ClampPlayerToRibCage(onSpine, centre, 12f, true),
                Is.EqualTo(onSpine));
        }

        [TestCase("hydra", true, true)]
        [TestCase("hydra", false, false)]
        [TestCase("abyss", true, false)]
        public void Ambient_suppression_is_owned_by_the_live_hydra_boss_phase(
            string voidId,
            bool bossPhase,
            bool expected)
        {
            Assert.That(HydraRuntimeRules.SuppressAmbientSpawns(voidId, bossPhase), Is.EqualTo(expected));
        }

        [Test]
        public void Hydra_prime_is_stationary()
        {
            Assert.That(HydraContent.Boss.Speed, Is.Zero);
        }

        [Test]
        public void Authored_hydra_head_keeps_the_approved_large_screen_footprint()
        {
            Assert.That(HydraRuntimeRules.BossPresentationScale, Is.EqualTo(2.3f));
            Assert.That(HydraRuntimeRules.EvasionPresentationScale, Is.EqualTo(1.04f));
            Assert.That(
                HydraRuntimeRules.BossPresentationScale,
                Is.GreaterThan(HydraRuntimeRules.EvasionPresentationScale * 2f));
        }

        [Test]
        public void Marrow_barrage_releases_exactly_four_bombs_from_cumulative_intervals()
        {
            var intervals = new[] { 0.42, 0.50, 0.58, 0.66 };
            Assert.That(HydraRuntimeRules.MarrowBombsDue(0.41f, intervals), Is.Zero);
            Assert.That(HydraRuntimeRules.MarrowBombsDue(0.42f, intervals), Is.EqualTo(1));
            Assert.That(HydraRuntimeRules.MarrowBombsDue(0.91f, intervals), Is.EqualTo(1));
            Assert.That(HydraRuntimeRules.MarrowBombsDue(0.92f, intervals), Is.EqualTo(2));
            Assert.That(HydraRuntimeRules.MarrowBombsDue(99f, intervals), Is.EqualTo(4));
        }

        [TestCase("hydra-prime", "hydra-evasion", 2, false)]
        [TestCase("hydra-prime", "hydra-evasion", 1, true)]
        [TestCase("hydra-prime", "hydra-marrow", 2, true)]
        [TestCase("warden", "hydra-evasion", 2, true)]
        public void Only_active_hydra_evasion_is_invulnerable(
            string bossId,
            string attackId,
            int state,
            bool expectedDamageable)
        {
            Assert.That(
                HydraRuntimeRules.BossCanTakeDamage(bossId, attackId, state),
                Is.EqualTo(expectedDamageable));
        }

        [Test]
        public void Evasion_step_visits_all_six_slots_and_never_overruns()
        {
            for (var step = 0; step < HydraEncounterRules.EvasionSocketCount; step++)
            {
                var elapsed = step * 0.4f + 0.01f;
                Assert.That(
                    HydraRuntimeRules.EvasionStep(elapsed, 2.4f),
                    Is.EqualTo(step));
            }
            Assert.That(HydraRuntimeRules.EvasionStep(99f, 2.4f), Is.EqualTo(5));
        }
    }
}
