using NUnit.Framework;
using VoidFall.Core;

namespace VoidFall.Tests.Editor
{
    public sealed class EncounterPacingRulesTests
    {
        [Test]
        public void Deployment_does_not_automatically_mean_resolution()
        {
            var clock = new CombatEncounterClock(); clock.Begin(CombatEncounterKind.Crossing, 6);
            clock.Step(2.5, 0, false, true);
            Assert.That(clock.Phase, Is.EqualTo(CombatEncounterPhase.Deployment));
            clock.CommitDeployment(12); clock.Step(1, 12, false, true);
            Assert.That(clock.Phase, Is.EqualTo(CombatEncounterPhase.ActiveThreat));
            clock.Step(1, 0, true, true);
            Assert.That(clock.Phase, Is.EqualTo(CombatEncounterPhase.ActiveThreat));
        }

        [Test]
        public void Recovery_requires_resolved_danger_and_keeps_its_full_minimum()
        {
            var clock = Started();
            clock.Step(1, 0, false, false);
            Assert.That(clock.Phase, Is.EqualTo(CombatEncounterPhase.Resolution));
            clock.Step(1, 0, false, false);
            Assert.That(clock.Phase, Is.EqualTo(CombatEncounterPhase.Resolution));
            clock.Step(.1, 0, false, true);
            Assert.That(clock.Phase, Is.EqualTo(CombatEncounterPhase.Recovery));
            clock.Step(5.9, 0, false, true);
            Assert.That(clock.Phase, Is.EqualTo(CombatEncounterPhase.Recovery));
            clock.Step(.1, 0, false, true);
            Assert.That(clock.Phase, Is.EqualTo(CombatEncounterPhase.Flow));
        }

        [Test]
        public void Distant_survivors_request_withdrawal_instead_of_stalling_forever()
        {
            var clock = Started(); clock.Step(18, 1, false, true);
            Assert.That(clock.NeedsWithdrawal, Is.True);
            Assert.That(clock.TimedOut, Is.True);
            Assert.That(clock.Phase, Is.EqualTo(CombatEncounterPhase.Resolution));
        }

        [Test]
        public void Blocked_deployment_does_not_create_a_rewarding_encounter()
        {
            var clock = new CombatEncounterClock();clock.Begin(CombatEncounterKind.Volley, 3);
            clock.Step(2.5,0,false,true);clock.CommitDeployment(0);
            Assert.That(clock.Admitted, Is.Zero);
            Assert.That(clock.Phase, Is.EqualTo(CombatEncounterPhase.Recovery));
        }

        [Test]
        public void Pause_invalid_time_and_reset_do_not_leave_debt()
        {
            var clock = Started();clock.Step(0,8,false,true);clock.Step(double.NaN,8,false,true);
            Assert.That(clock.PhaseSeconds, Is.Zero);
            Assert.That(clock.Phase, Is.EqualTo(CombatEncounterPhase.ActiveThreat));
            clock.Reset(); Assert.That(clock.Phase, Is.EqualTo(CombatEncounterPhase.Flow));
            Assert.That(clock.NeedsWithdrawal, Is.False);Assert.That(clock.Admitted, Is.Zero);
        }

        private static CombatEncounterClock Started()
        {
            var clock=new CombatEncounterClock();clock.Begin(CombatEncounterKind.Crossing,6);
            clock.Step(2.5,0,false,true);clock.CommitDeployment(8);return clock;
        }
    }
}
