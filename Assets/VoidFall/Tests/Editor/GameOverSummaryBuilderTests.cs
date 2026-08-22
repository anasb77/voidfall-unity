using System.Collections.Generic;
using NUnit.Framework;
using VoidFall.Core;
using VoidFall.UI;

namespace VoidFall.Tests.Editor
{
    /// <summary>
    /// Covers the game-over projection: scalar copies, weapon skipping and
    /// ordering, damage-percentage math including the zero-damage floor.
    /// </summary>
    public sealed class GameOverSummaryBuilderTests
    {
        [Test]
        public void Scalars_and_chips_are_copied_into_the_summary()
        {
            var chips = new List<UIBuildChip>();

            var summary = GameOverSummaryBuilder.Build(
                victory: false, score: 141721, elapsedSeconds: 1348f,
                kills: 12097, eliteKills: 61, bossKills: 7,
                level: 44, partsEarned: 2323, isBest: true, saved: true,
                weaponRanks: null, weaponDamage: null, totalDamageDealt: 1d,
                buildChips: chips);

            Assert.That(summary.Victory, Is.False);
            Assert.That(summary.Score, Is.EqualTo(141721));
            Assert.That(summary.ElapsedSeconds, Is.EqualTo(1348f));
            Assert.That(summary.Kills, Is.EqualTo(12097));
            Assert.That(summary.EliteKills, Is.EqualTo(61));
            Assert.That(summary.BossKills, Is.EqualTo(7));
            Assert.That(summary.Level, Is.EqualTo(44));
            Assert.That(summary.PartsEarned, Is.EqualTo(2323));
            Assert.That(summary.IsBest, Is.True);
            Assert.That(summary.Saved, Is.True);
            Assert.That(summary.Weapons, Is.Empty);
            Assert.That(summary.BuildChips, Is.SameAs(chips));
        }

        [Test]
        public void Unowned_weapons_are_skipped_and_percentages_split_by_total_damage()
        {
            // Catalog order: index 0 pistol, 1 scattergun, 2 railgun...
            var summary = GameOverSummaryBuilder.Build(
                victory: false, score: 10, elapsedSeconds: 60f,
                kills: 3, eliteKills: 0, bossKills: 0,
                level: 2, partsEarned: 4, isBest: false, saved: true,
                weaponRanks: new[] { 1, 0, 1 },
                weaponDamage: new double[] { 75, 999, 25 },
                totalDamageDealt: 100d,
                buildChips: new List<UIBuildChip>());

            Assert.That(summary.Weapons.Count, Is.EqualTo(2));
            Assert.That(summary.Weapons[0].Name, Is.EqualTo(ContentCatalog.Weapons[0].Name));
            Assert.That(summary.Weapons[0].Rank, Is.EqualTo(1));
            Assert.That(summary.Weapons[0].Damage, Is.EqualTo(75L));
            Assert.That(summary.Weapons[0].DamagePercent, Is.EqualTo(0.75f).Within(0.0001f));
            Assert.That(summary.Weapons[1].Name, Is.EqualTo(ContentCatalog.Weapons[2].Name));
            Assert.That(summary.Weapons[1].DamagePercent, Is.EqualTo(0.25f).Within(0.0001f));
        }

        [Test]
        public void Percentages_clamp_when_one_weapon_exceeds_total_damage()
        {
            var summary = GameOverSummaryBuilder.Build(
                victory: false, score: 1, elapsedSeconds: 1f,
                kills: 1, eliteKills: 0, bossKills: 0, level: 1, partsEarned: 0,
                isBest: false, saved: true,
                weaponRanks: new[] { 1 },
                weaponDamage: new double[] { 500 },
                totalDamageDealt: 100d,   // e.g. melee/contact dealt the rest
                buildChips: new List<UIBuildChip>());

            Assert.That(summary.Weapons[0].DamagePercent, Is.EqualTo(1f));
        }

        [Test]
        public void Zero_total_damage_floors_the_denominator_instead_of_dividing_by_zero()
        {
            var summary = GameOverSummaryBuilder.Build(
                victory: false, score: 1, elapsedSeconds: 1f,
                kills: 1, eliteKills: 0, bossKills: 0, level: 1, partsEarned: 0,
                isBest: false, saved: true,
                weaponRanks: new[] { 1 },
                weaponDamage: new double[] { 40 },
                totalDamageDealt: 0d,
                buildChips: new List<UIBuildChip>());

            Assert.That(summary.Weapons[0].DamagePercent, Is.EqualTo(1f));
        }

        [Test]
        public void Damage_array_shorter_than_ranks_treats_missing_entries_as_zero()
        {
            var summary = GameOverSummaryBuilder.Build(
                victory: false, score: 1, elapsedSeconds: 1f,
                kills: 1, eliteKills: 0, bossKills: 0, level: 1, partsEarned: 0,
                isBest: false, saved: true,
                weaponRanks: new[] { 1, 1, 1 },
                weaponDamage: new double[] { 10 },
                totalDamageDealt: 40d,
                buildChips: new List<UIBuildChip>());

            Assert.That(summary.Weapons.Count, Is.EqualTo(3));
            Assert.That(summary.Weapons[0].DamagePercent, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(summary.Weapons[1].Damage, Is.EqualTo(0L));
            Assert.That(summary.Weapons[1].DamagePercent, Is.EqualTo(0f));
        }
    }
}
