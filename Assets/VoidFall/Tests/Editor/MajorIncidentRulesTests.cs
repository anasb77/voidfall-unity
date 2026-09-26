using NUnit.Framework;
using VoidFall.Core;

namespace VoidFall.Tests.Editor
{
    public sealed class MajorIncidentRulesTests
    {
        [Test]
        public void Rounded_final_step_completes_in_the_same_tick()
        {
            var state = new MajorIncidentState();
            state.Begin(MajorIncidentKind.BlackHole);
            state.Step(20.5);
            state.Step(1 - 1e-16);
            Assert.That(state.Phase, Is.EqualTo(MajorIncidentPhase.None));
            Assert.That(state.Kind, Is.EqualTo(MajorIncidentKind.None));
        }
        [TestCase(MajorIncidentKind.BlackHole, 10, .5, 10, 1.5, 21.5)]
        [TestCase(MajorIncidentKind.DestroyerRaid, 10, 1.5, 32, 3, 45)]
        [TestCase(MajorIncidentKind.Eclipse, 10, 1.5, 19.5, 2, 31.5)]
        public void Lifecycle_preserves_warning_active_release_and_cleans_up(
            MajorIncidentKind kind, double warning, double activation, double active, double release, double total)
        {
            var state = new MajorIncidentState();
            state.Begin(kind);
            Assert.That(state.Kind, Is.EqualTo(kind));
            Assert.That(state.Phase, Is.EqualTo(MajorIncidentPhase.Warning));
            Assert.That(state.Remaining, Is.EqualTo(total));
            Assert.That(state.Strength, Is.Zero);
            state.Step(warning);
            Assert.That(state.Phase, Is.EqualTo(MajorIncidentPhase.Active));
            Assert.That(state.Strength, Is.Zero);
            state.Step(activation / 2);
            Assert.That(state.Strength, Is.EqualTo(0.5).Within(1e-10));
            state.Step(activation / 2);
            Assert.That(state.Strength, Is.EqualTo(1));
            state.Step(active - activation);
            Assert.That(state.Phase, Is.EqualTo(MajorIncidentPhase.Release));
            Assert.That(state.Strength, Is.EqualTo(1));
            state.Step(release / 2);
            Assert.That(state.Strength, Is.EqualTo(0.5).Within(1e-10));
            state.Step(release / 2);
            AssertReset(state);
        }

        [TestCase(MajorIncidentKind.BlackHole, 20.75)]
        [TestCase(MajorIncidentKind.DestroyerRaid, 43.5)]
        [TestCase(MajorIncidentKind.Eclipse, 30.5)]
        public void Large_step_crosses_multiple_phases_with_same_result_as_partitioned_steps(
            MajorIncidentKind kind, double elapsed)
        {
            var whole = new MajorIncidentState();
            var split = new MajorIncidentState();
            whole.Begin(kind);
            split.Begin(kind);
            whole.Step(elapsed);
            for (var i = 0; i < (int)(elapsed * 4); i++) split.Step(0.25);
            Assert.That(whole.Phase, Is.EqualTo(MajorIncidentPhase.Release));
            Assert.That(whole.Strength, Is.EqualTo(0.5).Within(1e-10));
            Assert.That(whole.Elapsed, Is.EqualTo(elapsed));
            Assert.That(split.Elapsed, Is.EqualTo(whole.Elapsed));
            Assert.That(split.Remaining, Is.EqualTo(whole.Remaining));
            Assert.That(split.Strength, Is.EqualTo(whole.Strength));
            whole.Step(double.MaxValue);
            AssertReset(whole);
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void Invalid_or_paused_steps_do_not_advance_or_corrupt_state(double dt)
        {
            var state = new MajorIncidentState();
            state.Begin(MajorIncidentKind.BlackHole);
            state.Step(10.25);
            state.Step(dt);
            Assert.That(state.Elapsed, Is.EqualTo(10.25));
            Assert.That(state.Remaining, Is.EqualTo(11.25));
            Assert.That(state.Phase, Is.EqualTo(MajorIncidentPhase.Active));
            Assert.That(state.Strength, Is.EqualTo(0.5));
        }

        [Test]
        public void Reset_invalid_begin_and_rebegin_cannot_leak_old_incident_strength()
        {
            var state = new MajorIncidentState();
            state.Begin(MajorIncidentKind.Eclipse);
            state.Step(8);
            state.Reset();
            AssertReset(state);
            state.Begin(MajorIncidentKind.BlackHole);
            state.Step(8);
            state.Begin(MajorIncidentKind.DestroyerRaid);
            Assert.That(state.Elapsed, Is.Zero);
            Assert.That(state.Strength, Is.Zero);
            Assert.That(state.Remaining, Is.EqualTo(45));
            state.Begin((MajorIncidentKind)99);
            AssertReset(state);
            state.Begin(MajorIncidentKind.None);
            state.Step(100);
            AssertReset(state);
        }

        [TestCase("abyss", true)]
        [TestCase("void", true)]
        [TestCase("sakura", false)]
        [TestCase("nebula", false)]
        [TestCase("hydra", false)]
        [TestCase("monochrome", false)]
        [TestCase("null-city", false)]
        [TestCase("eon-sea", false)]
        [TestCase("crascendo", false)]
        [TestCase("unknown", false)]
        [TestCase(null, false)]
        public void Arena_admission_is_explicitly_allowlisted(string arenaId, bool eligible)
        {
            Assert.That(MajorIncidentRules.IsArenaEligible(arenaId), Is.EqualTo(eligible));
        }

        [TestCase(MajorIncidentKind.BlackHole, 36.5)]
        [TestCase(MajorIncidentKind.DestroyerRaid, 60)]
        [TestCase(MajorIncidentKind.Eclipse, 46.5)]
        public void Admission_reserves_complete_incident_and_boss_lead_time(MajorIncidentKind kind, double required)
        {
            Assert.That(MajorIncidentRules.CanBegin(kind, required, false, false, false), Is.True);
            Assert.That(MajorIncidentRules.CanBegin(kind, required - 0.001, false, false, false), Is.False);
            Assert.That(MajorIncidentRules.CanBegin(kind, 300, true, false, false), Is.False);
            Assert.That(MajorIncidentRules.CanBegin(kind, 300, false, true, false), Is.False);
            Assert.That(MajorIncidentRules.CanBegin(kind, 300, false, false, true), Is.False);
            Assert.That(MajorIncidentRules.CanBegin(kind, double.NaN, false, false, false), Is.False);
            Assert.That(MajorIncidentRules.CanBegin(kind, double.PositiveInfinity, false, false, false), Is.False);
            Assert.That(MajorIncidentRules.CanBegin(MajorIncidentKind.None, 300, false, false, false), Is.False);
        }

        [TestCase(0, 10, 1, 0)]
        [TestCase(2.5, 10, 1, 1)]
        [TestCase(5, 10, 1, 1)]
        [TestCase(7.5, 10, 1, 1)]
        [TestCase(10, 10, 1, 0)]
        [TestCase(20, 10, 1, 0)]
        [TestCase(5, 10, 0.5, 0.5)]
        [TestCase(5, 10, 2, 1)]
        [TestCase(5, 10, -1, 0)]
        [TestCase(-1, 10, 1, 0)]
        [TestCase(0, 0, 1, 0)]
        [TestCase(1, -10, 1, 0)]
        [TestCase(double.NaN, 10, 1, 0)]
        [TestCase(5, double.NaN, 1, 0)]
        [TestCase(5, 10, double.NaN, 0)]
        [TestCase(double.PositiveInfinity, 10, 1, 0)]
        [TestCase(5, double.PositiveInfinity, 1, 0)]
        [TestCase(5, 10, double.PositiveInfinity, 0)]
        public void Pull_is_finite_bounded_and_zero_at_center_and_outside(
            double distance, double radius, double strength, double expected)
        {
            Assert.That(MajorIncidentRules.BlackHolePullScale(distance, radius, strength),
                Is.EqualTo(expected).Within(1e-10));
        }

        [Test]
        public void Pull_softens_only_near_core_and_rim_and_player_can_outswim_peak_force()
        {
            Assert.That(MajorIncidentRules.BlackHolePullScale(1, 230, 1), Is.InRange(0.001, .02));
            Assert.That(MajorIncidentRules.BlackHolePullScale(165, 230, 1), Is.EqualTo(1).Within(1e-10));
            Assert.That(MajorIncidentRules.BlackHolePullScale(229, 230, 1), Is.LessThan(.01));
            Assert.That(MajorIncidentRules.BlackHolePlayerPullFraction, Is.InRange(.6f, .7f));
        }

        [Test]
        public void Raid_health_preserves_roles_and_caps_late_bonus_without_early_late_build_budget()
        {
            Assert.That(DestroyerContent.RaidHealthMultiplier(85), Is.LessThan(1.15f));
            Assert.That(DestroyerContent.RaidHealthMultiplier(1296), Is.EqualTo(2.5f));
            Assert.That(DestroyerContent.RaidHealthMultiplier(10000), Is.EqualTo(2.5f));
            Assert.That(DestroyerContent.Find("destroyer-husk").Health, Is.GreaterThan(DestroyerContent.Find("destroyer-razor").Health * 2));
            foreach (var enemy in DestroyerContent.Enemies) Assert.That(enemy.TelegraphSeconds, Is.GreaterThanOrEqualTo(.85));
        }

        private static void AssertReset(MajorIncidentState state)
        {
            Assert.That(state.Kind, Is.EqualTo(MajorIncidentKind.None));
            Assert.That(state.Phase, Is.EqualTo(MajorIncidentPhase.None));
            Assert.That(state.Elapsed, Is.Zero);
            Assert.That(state.Remaining, Is.Zero);
            Assert.That(state.Strength, Is.Zero);
        }
    }
}
