using NUnit.Framework;
using VoidFall.Core;

namespace VoidFall.Tests.Editor
{
    public sealed class WildCardRulesTests
    {
        [Test]
        public void Standstill_stance_activates_inside_the_spec_window()
        {
            // Spec 44.2 recommends a 0.35-0.5 second hold; the rule sits at
            // the midpoint so both window edges behave predictably.
            Assert.That(WildCardRules.StandstillActivationSeconds,
                Is.InRange(0.35, 0.5));
            Assert.That(WildCardRules.StandstillActive(0.39), Is.False);
            Assert.That(WildCardRules.StandstillActive(0.41), Is.True);
        }

        [Test]
        public void Standstill_multiplier_is_double()
        {
            Assert.That(WildCardRules.StandstillDamageMultiplier, Is.EqualTo(2.0));
        }

        [Test]
        public void Greed_doubles_xp()
        {
            Assert.That(WildCardRules.GreedXpMultiplier, Is.EqualTo(2));
        }

        [Test]
        public void Second_life_grants_exactly_one_revive()
        {
            Assert.That(WildCardRules.SecondLifeBonusRevives, Is.EqualTo(1));
        }

        [Test]
        public void Every_enum_card_is_marked_implemented_except_none()
        {
            Assert.That(WildCardRules.IsImplemented(WildCardId.None), Is.False);
            Assert.That(WildCardRules.IsImplemented(WildCardId.Standstill), Is.True);
            Assert.That(WildCardRules.IsImplemented(WildCardId.Greed), Is.True);
            Assert.That(WildCardRules.IsImplemented(WildCardId.SecondLife), Is.True);
            Assert.That(WildCardRules.IsImplemented(WildCardId.Overclocker), Is.True);
        }

        [Test]
        public void Negative_or_nan_still_time_never_activates_the_stance()
        {
            Assert.That(WildCardRules.StandstillActive(-1), Is.False);
            Assert.That(WildCardRules.StandstillActive(double.NaN), Is.False);
        }
    }
}