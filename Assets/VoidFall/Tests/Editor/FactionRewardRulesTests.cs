using NUnit.Framework;
using VoidFall.Core;

namespace VoidFall.Tests.Editor
{
    public sealed class FactionRewardRulesTests
    {
        [Test]
        public void Three_factions_are_reciprocally_hostile_and_same_side_immune()
        {
            foreach (CombatFaction a in System.Enum.GetValues(typeof(CombatFaction)))
                foreach (CombatFaction b in System.Enum.GetValues(typeof(CombatFaction)))
                    Assert.That(FactionRewardRules.Hostile(a, b), Is.EqualTo(a != b));
        }
        [Test]
        public void Damage_budget_counts_effective_health_once_even_after_healing()
        {
            var contribution = new RewardContribution(100);
            contribution.RecordDamage(25, true); contribution.RecordDamage(50, false);
            contribution.RecordDamage(1000, true);
            Assert.That(contribution.Fraction, Is.EqualTo(.5));
            contribution.RecordDamage(1000, true);
            Assert.That(contribution.Fraction, Is.EqualTo(.5));
        }
        [Test]
        public void Descendants_share_one_finite_allowance_and_stale_handles_never_rebind()
        {
            var roots = new RewardRootLedger(2); var root = roots.Create(3);
            Assert.That(roots.Retain(root), Is.True);
            for (var i = 0; i < 3; i++) Assert.That(roots.ClaimChild(root), Is.True);
            Assert.That(roots.ClaimChild(root), Is.False);
            roots.Release(root); Assert.That(roots.IsValid(root), Is.True);
            roots.Release(root); Assert.That(roots.IsValid(root), Is.False);
            var replacement = roots.Create(8);
            Assert.That(replacement, Is.Not.EqualTo(root));
            Assert.That(roots.ClaimChild(root), Is.False);
        }
        [Test]
        public void Capacity_exhaustion_and_repeated_boss_births_do_not_mint_rewards()
        {
            var roots = new RewardRootLedger(1); var boss = roots.Create(FactionRewardRules.BossOffspringAllowance);
            Assert.That(roots.Create(99), Is.Zero);
            var rewarded = 0;
            for (var i = 0; i < 10000; i++) if (roots.ClaimChild(boss)) rewarded++;
            Assert.That(rewarded, Is.EqualTo(FactionRewardRules.BossOffspringAllowance));
        }
        [Test]
        public void Five_approved_archetypes_have_distinct_native_attack_data()
        {
            Assert.That(DestroyerContent.Enemies.Length, Is.EqualTo(5));
            Assert.That(DestroyerContent.Find("destroyer-maw").TelegraphSeconds, Is.EqualTo(.95));
            Assert.That(DestroyerContent.Find("destroyer-razor").RecoverySeconds, Is.EqualTo(2.05));
            Assert.That(DestroyerContent.Find("destroyer-husk").Health, Is.EqualTo(510));
            Assert.That(DestroyerContent.Find("destroyer-grasp").BlastRadius, Is.EqualTo(165));
            Assert.That(DestroyerContent.Find("destroyer-spite").ProjectileSpeed, Is.EqualTo(215));
            Assert.That(DestroyerContent.Find("court-white-pawn"), Is.Null);
        }
    }
}
