using System;
using System.Linq;
using NUnit.Framework;
using VoidFall.Core;

namespace VoidFall.Tests
{
    public sealed class LegacyRestorationRulesTests
    {
        [Test]
        public void Xp_keeps_the_opening_levels_and_slows_later_upgrades()
        {
            for (var level = 1; level <= 30; level++)
            {
                var previous = (int)Math.Floor((7 + level * 4 + Math.Pow(level, 1.62) * 1.7) * 1.18);
                Assert.That(BalanceRules.XpNeededForLevel(level), Is.EqualTo(level <= 5 ? previous : (int)Math.Ceiling(previous * 1.25)));
            }
            Assert.That(LegacyRestorationRules.RevealSeconds("exploder"), Is.EqualTo(60));
        }

        [Test]
        public void Rapid_overclock_pickups_cannot_bank_minutes_but_keep_the_earned_streak()
        {
            var state = new OverclockState();
            for (var i = 0; i < 18; i++) state.ApplyPickup();
            Assert.That(state.RemainingSeconds, Is.EqualTo(30));
            Assert.That(state.Streak, Is.EqualTo(18));
            Assert.That(state.PowerTier, Is.EqualTo(3));
            state.Step(20); state.ApplyPickup();
            Assert.That(state.RemainingSeconds, Is.EqualTo(25));
            state.Step(25); Assert.That(state.Active, Is.False);
        }

        [Test]
        public void Clock_cannot_receive_projectile_cards_and_owned_weapon_is_named_before_selection()
        {
            var progress = new UpgradeProgress();
            progress.WeaponRanks[UpgradeRules.StartingWeaponIndex("clock")] = 1;
            var options = UpgradeRules.RollProgressionOptions(progress, new Rng(7), 100);
            Assert.That(options.Any(o => o.TargetId == "phaseRounds" || o.TargetId.StartsWith("split-")), Is.False);
            progress.WeaponRanks[0] = 1;
            options = UpgradeRules.RollProgressionOptions(progress, new Rng(7), 100);
            var split = options.Single(o => o.TargetId == "split-pistol");
            Assert.That(split.Name, Does.Contain(ContentCatalog.Weapons[0].Name));
            Assert.That(split.Description, Does.Contain(ContentCatalog.Weapons[0].Name));
            Assert.That(options.Any(o => o.TargetId == "split-seeker"), Is.False);
            Assert.That(options.Any(o => o.TargetId == "phaseRounds"), Is.True);
        }

        [Test]
        public void Piercing_has_three_cumulative_ranks_and_cannot_skip_a_rank()
        {
            var progress = new UpgradeProgress(); progress.WeaponRanks[0] = 1;
            var index = Array.FindIndex(ExtendedCatalog.AllSupports(), s => s.Id == "phaseRounds");
            for (var rank = 1; rank <= 3; rank++)
            {
                var offer = UpgradeRules.RollProgressionOptions(progress, new Rng(3), 100).Single(o => o.TargetId == "phaseRounds");
                UpgradeRules.Apply(progress, offer);
                Assert.That(progress.SupportRanks[index], Is.EqualTo(rank));
            }
            Assert.That(UpgradeRules.RollProgressionOptions(progress, new Rng(3), 100).Any(o => o.TargetId == "phaseRounds"), Is.False);
        }

        [TestCase(10, 0, true)] [TestCase(10.1, 0, false)]
        [TestCase(0, 0, false)] [TestCase(5, 1, false)]
        public void Emergency_healing_requires_living_player_at_threshold_and_ready_cooldown(double hp, double cooldown, bool ready)
            => Assert.That(LegacyRestorationRules.CanHeal(hp, 100, 12, cooldown), Is.EqualTo(ready));

        [Test]
        public void Roster_reveal_retains_new_families_through_fifteen_minutes()
        {
            Assert.That(LegacyRestorationRules.RevealSeconds("guard"), Is.EqualTo(150));
            Assert.That(LegacyRestorationRules.RevealSeconds("brute"), Is.EqualTo(210));
            Assert.That(LegacyRestorationRules.RevealSeconds("technician"), Is.EqualTo(270));
            Assert.That(LegacyRestorationRules.RevealSeconds("shuriken"), Is.EqualTo(30));
            Assert.That(LegacyRestorationRules.RevealSeconds("spiky"), Is.EqualTo(50));
            Assert.That(LegacyRestorationRules.SharedRevealSeconds("twinGunner", 1), Is.EqualTo(30));
            Assert.That(LegacyRestorationRules.SharedRevealSeconds("splitter", 1), Is.EqualTo(90));
            Assert.That(LegacyRestorationRules.SharedRevealSeconds("mortar", 1), Is.EqualTo(150));
            Assert.That(double.IsPositiveInfinity(LegacyRestorationRules.SharedRevealSeconds("carrier", 1)), Is.True);
            Assert.That(ContentCatalog.Enemies.Count(e => LegacyRestorationRules.IsNewEnemy(e.Id)), Is.EqualTo(3));
            Assert.That(LegacyRestorationRules.GiantSlayerMultiplier(3), Is.EqualTo(1.45).Within(.001));
        }

        [Test]
        public void Shared_tier_preview_keeps_late_Abyss_small_and_second_visit_ramps()
        {
            Assert.That(LegacyRestorationRules.SharedTierTwoShare(0, 269, 100), Is.Zero);
            Assert.That(LegacyRestorationRules.SharedTierTwoShare(0, 270, 100), Is.EqualTo(.05).Within(.0001));
            Assert.That(LegacyRestorationRules.SharedTierTwoShare(0, 360, 100), Is.EqualTo(.10).Within(.0001));
            Assert.That(LegacyRestorationRules.SharedTierTwoShare(1, 0, 59), Is.Zero);
            Assert.That(LegacyRestorationRules.SharedTierTwoShare(1, 0, 60), Is.EqualTo(.15).Within(.0001));
            Assert.That(LegacyRestorationRules.SharedTierTwoShare(1, 360, 60), Is.EqualTo(.50).Within(.0001));
        }
    }
}
