using NUnit.Framework;
using VoidFall.Core;
namespace VoidFall.Tests
{
    public sealed class LegendaryRulesTests
    {
        [Test]
        public void SoundBladeLimitsTurningAndTracksHeldAttack()
        {
            var state = new LegendaryState(); state.Step(.01, true, true, LegendaryWeaponId.SoundBlade, 1, 2);
            Assert.That(state.Angle, Is.EqualTo(.06).Within(.00001)); Assert.That(state.Attacking, Is.True);
            state.Step(.01, false, true, LegendaryWeaponId.SoundBlade, 1, 2); Assert.That(state.Attacking, Is.False);
        }
        [Test]
        public void RifleChargesIndependentlyOfAimAndFiresOnce()
        {
            var state = new LegendaryState();
            for (var i = 0; i < 120; i++) state.Step(1.0 / 60, true, true, LegendaryWeaponId.ChargedRifle, 1, -2);
            var shot = state.Step(1.0 / 60, false, true, LegendaryWeaponId.ChargedRifle, 1, 2);
            Assert.That(shot.Fired, Is.True); Assert.That(shot.Damage, Is.EqualTo(310).Within(.001));
            Assert.That(shot.Angle, Is.GreaterThan(2));
            Assert.That(state.Step(.016, false, true, LegendaryWeaponId.ChargedRifle, 1, 0).Fired, Is.False);
        }
        [Test]
        public void OverloadAndModalCancellationCannotReleaseStoredShots()
        {
            var state = new LegendaryState();
            for (var i = 0; i < 200; i++) state.Step(1.0 / 60, true, true, LegendaryWeaponId.ChargedRifle, 1, 0);
            Assert.That(state.Overloads, Is.EqualTo(1));
            Assert.That(state.Step(.016, false, true, LegendaryWeaponId.ChargedRifle, 1, 0).Fired, Is.False);
            for (var i = 0; i < 120; i++) state.Step(1.0 / 60, true, true, LegendaryWeaponId.ChargedRifle, 1, 0);
            state.Cancel();
            Assert.That(state.Step(.016, false, true, LegendaryWeaponId.ChargedRifle, 1, 0).Fired, Is.False);
        }
        [Test]
        public void PauseFreezesRecoveryAndRankValuesMatchTheApprovedLab()
        {
            var state = new LegendaryState { Recovery = .5 };
            state.Step(.1, true, false, LegendaryWeaponId.ChargedRifle, 1, 0);
            Assert.That(state.Recovery, Is.EqualTo(.5));
            Assert.That(LegendaryRules.Stats(LegendaryWeaponId.SoundBlade, 3).Damage, Is.EqualTo(64));
            Assert.That(LegendaryRules.Stats(LegendaryWeaponId.ChargedRifle, 3).Damage, Is.EqualTo(515));
        }
        [Test]
        public void BeamSegmentExcludesTargetsBehindTheMuzzle()
        {
            Assert.That(LegendaryRules.SegmentDistance(50, 5, 0, 0, 100, 0), Is.EqualTo(5));
            Assert.That(LegendaryRules.SegmentDistance(-20, 0, 0, 0, 100, 0), Is.EqualTo(20));
        }
    }
}
