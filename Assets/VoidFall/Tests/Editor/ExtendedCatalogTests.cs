using System.Collections.Generic;
using NUnit.Framework;
using VoidFall.Core;

namespace VoidFall.Tests.Editor
{
    /// <summary>
    /// Covers the section 46 support cards: catalog integrity (parity
    /// indices unchanged, unique ids), pool inclusion and rank application,
    /// and the rank-gated effect math that keeps the golden master safe.
    /// </summary>
    public sealed class ExtendedCatalogTests
    {
        [Test]
        public void Parity_supports_keep_their_indices_and_extras_append()
        {
            var all = ExtendedCatalog.AllSupports();
            Assert.That(all.Length, Is.EqualTo(ContentCatalog.Supports.Length + 6));
            for (var index = 0; index < ContentCatalog.Supports.Length; index++)
            {
                Assert.That(all[index], Is.SameAs(ContentCatalog.Supports[index]),
                    "parity entry " + index + " must keep its index for saves and telemetry");
            }
            Assert.That(ExtendedCatalog.SupportCount, Is.EqualTo(all.Length));

            var ids = new HashSet<string>();
            foreach (var support in all)
            {
                Assert.That(ids.Add(support.Id), Is.True, "duplicate support id " + support.Id);
                Assert.That(support.Name, Is.Not.Empty);
                Assert.That(support.Weight, Is.GreaterThan(0));
                Assert.That(support.MaxRank, Is.InRange(1, 4));
                Assert.That(support.Accent, Is.Not.Empty);
                Assert.That(support.Descriptions, Is.Not.Null);
                Assert.That(support.Descriptions.Length, Is.EqualTo(support.MaxRank),
                    support.Id + " needs a description per rank");
            }
        }

        [Test]
        public void Effect_rules_are_rank_gated_and_clamped()
        {
            Assert.That(SupportEffectRules.ScholarXpMultiplier(0), Is.EqualTo(1.0));
            Assert.That(SupportEffectRules.ScholarXpMultiplier(4), Is.EqualTo(1.32).Within(1e-9));
            Assert.That(SupportEffectRules.ScholarXpMultiplier(99), Is.EqualTo(1.32).Within(1e-9));

            Assert.That(SupportEffectRules.DodgeChance(0), Is.EqualTo(0.0));
            Assert.That(SupportEffectRules.DodgeChance(3), Is.EqualTo(0.12).Within(1e-9));

            Assert.That(SupportEffectRules.ProjectileSizeMultiplier(2), Is.EqualTo(1.2).Within(1e-9));
            Assert.That(SupportEffectRules.ProjectileSpeedMultiplier(3), Is.EqualTo(1.3).Within(1e-9));
            Assert.That(SupportEffectRules.FortuneDropBonus(4), Is.EqualTo(0.20).Within(1e-9));
            Assert.That(SupportEffectRules.SpatialAwarenessZoom(0), Is.EqualTo(1.0));
            Assert.That(SupportEffectRules.SpatialAwarenessZoom(3), Is.EqualTo(1.15).Within(1e-9));
        }

        [Test]
        public void New_supports_roll_into_the_level_up_pool()
        {
            var progress = new UpgradeProgress();
            var seen = new HashSet<string>();
            for (uint seed = 1; seed <= 400; seed++)
            {
                foreach (var option in UpgradeRules.RollProgressionOptions(progress, new Rng(seed), 3))
                    seen.Add(option.Id);
            }

            foreach (var id in new[] { "dodge", "scholar", "fortune", "projectileSize", "projectileSpeed", "spatialAwareness" })
            {
                Assert.That(seen.Contains("support:" + id), Is.True,
                    id + " never appears in the level-up pool");
            }
            // The parity pool is untouched.
            Assert.That(seen.Contains("support:calibration"), Is.True);
        }

        [Test]
        public void New_supports_rank_up_through_the_normal_apply_path()
        {
            var progress = new UpgradeProgress();
            var dodgeIndex = -1;
            var all = ExtendedCatalog.AllSupports();
            for (var index = 0; index < all.Length; index++)
                if (all[index].Id == "dodge") dodgeIndex = index;
            Assert.That(dodgeIndex, Is.GreaterThanOrEqualTo(ContentCatalog.Supports.Length),
                "dodge must live in the extended range");

            var option = new UpgradeOptionDefinition
            {
                Id = "support:dodge",
                TargetId = "dodge",
                Kind = UpgradeOptionKind.Support,
                CurrentRank = 0,
                NextRank = 1,
            };
            Assert.That(UpgradeRules.Apply(progress, option), Is.True);
            Assert.That(progress.SupportRanks[dodgeIndex], Is.EqualTo(1));

            // Stale-rank double applies are rejected like every other card.
            Assert.That(UpgradeRules.Apply(progress, option), Is.False);
        }

        [Test]
        public void Core_progression_now_requires_the_extended_cards_too()
        {
            var progress = new UpgradeProgress();
            // Own and max every weapon + evolution precondition placeholders;
            // parity supports maxed. Core completion must still be false
            // while an extended support is unranked.
            for (var index = 0; index < progress.WeaponRanks.Length; index++)
                progress.WeaponRanks[index] = ProgressionRules.MaxWeaponRank;
            for (var index = 0; index < ContentCatalog.Supports.Length; index++)
                progress.SupportRanks[index] = ContentCatalog.Supports[index].MaxRank;
            progress.Evolved = new bool[progress.WeaponRanks.Length];
            for (var index = 0; index < progress.Evolved.Length; index++) progress.Evolved[index] = true;

            Assert.That(UpgradeRules.CoreProgressionComplete(progress), Is.False,
                "late upgrades must stay locked until the new cards are also maxed");

            var all = ExtendedCatalog.AllSupports();
            for (var index = ContentCatalog.Supports.Length; index < all.Length; index++)
                progress.SupportRanks[index] = all[index].MaxRank;
            Assert.That(UpgradeRules.CoreProgressionComplete(progress), Is.True);
        }
    }
}
