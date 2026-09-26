using NUnit.Framework;
using VoidFall.Core;

namespace VoidFall.Tests.Editor
{
    public sealed class NullCityPacingRulesTests
    {
        [Test]
        public void Native_targets_keep_quiet_and_lockdown_density_bounded()
        {
            Assert.That(NullCityPacingRules.QuietOrdinaryArrivalsPerSecond, Is.InRange(4f, 6f));
            Assert.That(NullCityPacingRules.LockdownOrdinaryArrivalsPerSecond, Is.InRange(8f, 10f));
            Assert.That(NullCityPacingRules.QuietActiveTarget, Is.InRange(35, 50));
            Assert.That(NullCityPacingRules.LockdownActiveTarget, Is.InRange(60, 90));
            Assert.That(NullCityPacingRules.QuietActiveCap, Is.InRange(35, 50));
            Assert.That(NullCityPacingRules.LockdownActiveCap, Is.InRange(60, 90));
        }

        [Test]
        public void Heavy_limit_is_zero_early_then_allows_two_late_without_dps_matching()
        {
            Assert.That(NullCityPacingRules.HeavyLimit(0, false), Is.Zero);
            Assert.That(NullCityPacingRules.HeavyLimit(89.99f, false), Is.Zero);
            Assert.That(NullCityPacingRules.HeavyLimit(90, false), Is.EqualTo(1));
            Assert.That(NullCityPacingRules.HeavyLimit(180, false), Is.EqualTo(2));
            Assert.That(NullCityPacingRules.HeavyLimit(0, true), Is.EqualTo(2));
        }

        [Test]
        public void Ordinary_interval_is_the_inverse_of_authored_arrival_rate()
        {
            Assert.That(NullCityPacingRules.OrdinaryInterval(false), Is.EqualTo(.2f).Within(.0001f));
            Assert.That(NullCityPacingRules.OrdinaryInterval(true), Is.EqualTo(1f / 9f).Within(.0001f));
        }
    }
}
