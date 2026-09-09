using System;
using NUnit.Framework;
using VoidFall.Core;
using VoidFall.Persistence;

namespace VoidFall.Tests.Editor
{
    public sealed class SupportMergeTests
    {
        [Test]
        public void Removed_cards_are_absent_and_survivors_describe_both_effects()
        {
            var supports = ExtendedCatalog.AllSupports();
            Assert.That(supports.Length, Is.EqualTo(13));
            Assert.That(Array.Exists(supports, s => s.Id == "fortune" || s.Id == "spatialAwareness"), Is.False);
            var options = UpgradeRules.RollProgressionOptions(new UpgradeProgress(), new Rng(7), 100);
            var scholar = Array.Find(options, o => o.TargetId == "scholar");
            var speed = Array.Find(options, o => o.TargetId == "projectileSpeed");
            Assert.That(scholar.Description, Does.Contain("Experience").And.Contain("Special drops"));
            Assert.That(speed.Description, Does.Contain("Projectile speed").And.Contain("Camera dezoom"));
        }

        [Test]
        public void Legacy_saved_supports_merge_idempotently_without_double_counting()
        {
            var save = SaveStore.CreateDefault();
            save.recentRuns = new[] { new RunRecordEntry { date = 1, supports = new[]
            {
                new WorkshopEntry { id = "scholar", rank = 2 }, new WorkshopEntry { id = "fortune", rank = 4 },
                new WorkshopEntry { id = "projectileSpeed", rank = 1 }, new WorkshopEntry { id = "spatialAwareness", rank = 2 }
            } } };
            var clean = SaveStore.Sanitize(save);
            for (var pass = 0; pass < 2; pass++)
            {
                var entries = clean.recentRuns[0].supports;
                Assert.That(entries.Length, Is.EqualTo(2));
                Assert.That(Array.Find(entries, e => e.id == "scholar").rank, Is.EqualTo(4));
                Assert.That(Array.Find(entries, e => e.id == "projectileSpeed").rank, Is.EqualTo(2));
                clean = SaveStore.Sanitize(clean);
            }
        }

        [TestCase("blades")]
        [TestCase("clock")]
        public void Orbital_weapon_cards_explain_projectile_interception(string id)
        {
            var progress = new UpgradeProgress();
            var options = UpgradeRules.RollProgressionOptions(progress, new Rng(7), 100);
            var card = Array.Find(options, o => o.TargetId == id && o.Kind == UpgradeOptionKind.Weapon);
            Assert.That(card.Description, Does.Contain("Can stop enemy projectiles").And.Contain("boss").And.Contain("elite"));
        }
    }
}
