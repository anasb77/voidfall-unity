using System;
using NUnit.Framework;
using VoidFall.Core;

namespace VoidFall.Tests.Editor
{
    public sealed class NewWeaponCatalogTests
    {
        [Test]
        public void New_weapons_append_without_changing_existing_ids()
        {
            var expected = new[] { "pistol", "scattergun", "railgun", "blades", "arc", "seeker", "mines", "summons", "clock", "boomerang" };
            Assert.That(ContentCatalog.Weapons.Length, Is.EqualTo(expected.Length));
            Assert.That(ContentOrder.Weapons.Length, Is.EqualTo(expected.Length));
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.That(ContentCatalog.Weapons[i].Id, Is.EqualTo(expected[i]));
                Assert.That(ContentOrder.Weapons[i].ToString().ToLowerInvariant(), Is.EqualTo(expected[i]));
                Assert.That(ContentCatalog.Weapons[i].Ranks.Length, Is.EqualTo(6));
            }
        }

        [TestCase("mines", 60, 140, 2.4, 1.8)]
        [TestCase("summons", 40, 100, 3.0, 2.0)]
        [TestCase("clock", 20, 60, 5.5, 3.0)]
        [TestCase("boomerang", 24, 60, 1.8, 1.3)]
        public void Approved_rank_endpoints_are_authored(string id, double first, double last, double firstDelay, double lastDelay)
        {
            var weapon = Array.Find(ContentCatalog.Weapons, item => item.Id == id);
            Assert.That(weapon, Is.Not.Null);
            Assert.That(weapon.Ranks[0].Stats.Damage, Is.EqualTo(first));
            Assert.That(weapon.Ranks[5].Stats.Damage, Is.EqualTo(last));
            var a = weapon.Ranks[0].Stats;
            var b = weapon.Ranks[5].Stats;
            Assert.That(id == "clock" ? Math.PI * 2 / a.OrbitSpeed : a.Cooldown, Is.EqualTo(firstDelay).Within(1e-6));
            Assert.That(id == "clock" ? Math.PI * 2 / b.OrbitSpeed : b.Cooldown, Is.EqualTo(lastDelay).Within(1e-6));
        }

        [TestCase("mines", "amplifier")]
        [TestCase("summons", "dodge")]
        [TestCase("clock", "cycling")]
        [TestCase("boomerang", "projectileSpeed")]
        public void Evolution_is_offered_with_its_extended_support(string weaponId, string supportId)
        {
            var progress = new UpgradeProgress();
            var index = Array.FindIndex(ContentCatalog.Weapons, w => w.Id == weaponId);
            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            progress.WeaponRanks[index] = 6;
            var support = Array.FindIndex(ExtendedCatalog.AllSupports(), s => s.Id == supportId);
            progress.SupportRanks[support] = ExtendedCatalog.AllSupports()[support].MaxRank;
            var offers = UpgradeRules.RollProgressionOptions(progress, new Rng(7), 3);
            Assert.That(Array.Exists(offers, o => o.Kind == UpgradeOptionKind.Evolution && o.TargetId == weaponId), Is.True);
        }
    }
}
