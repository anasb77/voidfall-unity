using System;
using System.Linq;
using NUnit.Framework;
using VoidFall.Core;

namespace VoidFall.Tests.Editor
{
    public sealed class SurvivalSupportRulesTests
    {
        [TestCase(0, 0)] [TestCase(1, .2f)] [TestCase(2, .4f)]
        [TestCase(3, .5f)] [TestCase(4, .7f)] [TestCase(5, .9f)]
        public void Locked_healing_values_are_flat_hp(int rank, float expected)
            => Assert.That(SurvivalSupportRules.LifeStealHealing(rank), Is.EqualTo(expected));

        [Test]
        public void Small_grants_cannot_raise_capacity_but_large_sources_can()
        {
            float shield = 0, capacity = 20;
            for (var i = 0; i < 100; i++) SurvivalSupportRules.GrantShield(ref shield, ref capacity, 5);
            Assert.That(shield, Is.EqualTo(20)); Assert.That(capacity, Is.EqualTo(20));
            SurvivalSupportRules.GrantShield(ref shield, ref capacity, 50);
            Assert.That(shield, Is.EqualTo(50)); Assert.That(capacity, Is.EqualTo(50));
            Assert.That(DealerRules.Absorb(ref shield, 55), Is.EqualTo(5));
            Assert.That(shield, Is.Zero);
        }

        [Test]
        public void New_cards_enter_the_pool_and_upgrade_to_their_rank_caps()
        {
            var progress = new UpgradeProgress(); progress.WeaponRanks[0] = 1;
            foreach (var id in new[] { "lifeSteal", "scavenger" })
            {
                var index = Array.FindIndex(ExtendedCatalog.AllSupports(), x => x.Id == id);
                var cap = id == "lifeSteal" ? 5 : 4;
                for (var rank = 1; rank <= cap; rank++)
                {
                    var option = UpgradeRules.RollProgressionOptions(progress, new Rng(7), 100).Single(x => x.TargetId == id);
                    Assert.That(UpgradeRules.Apply(progress, option), Is.True);
                    Assert.That(progress.SupportRanks[index], Is.EqualTo(rank));
                }
                Assert.That(UpgradeRules.RollProgressionOptions(progress, new Rng(7), 100).Any(x => x.TargetId == id), Is.False);
            }
            Assert.That(SurvivalSupportRules.ScrapChance(4), Is.EqualTo(.024).Within(.000001));
            Assert.That(SurvivalSupportRules.HealthBarWidth(125, 37), Is.EqualTo(23.75f));
            Assert.That(SurvivalSupportRules.HealthBarWidth(1000, 37), Is.EqualTo(37));
        }
    }
}
