using System.Collections.Generic;
using NUnit.Framework;
using VoidFall.Core;

namespace VoidFall.Tests.Editor
{
    /// <summary>
    /// Covers the Hydra hybridization rules (spec §14): the one-gene
    /// prototype rule, chassis compatibility bans, the escalation ladder by
    /// Gene Nodes destroyed, the Split gate, readability language, and
    /// deterministic rolls.
    /// </summary>
    public sealed class MutationRulesTests
    {
        [Test]
        public void Every_gene_carries_one_distinct_visual_language()
        {
            var cues = new HashSet<string>();
            foreach (var info in MutationRules.ActiveGenes())
            {
                Assert.That(info.DisplayName, Is.Not.Empty);
                Assert.That(info.VisualCue, Is.Not.Empty,
                    info.DisplayName + " needs its readability cue (§14.3)");
                Assert.That(cues.Add(info.VisualCue), Is.True,
                    "two genes share a visual cue: unreadable");
                Assert.That(info.AbilitySummary, Is.Not.Empty);
            }
        }

        [Test]
        public void Split_gene_is_gated_off_for_the_first_prototype()
        {
            MutationRules.SplitGeneEnabled = false;
            try
            {
                for (uint seed = 1; seed <= 200; seed++)
                {
                    var gene = MutationRules.RollHybrid(new Rng(seed), "direct", 2);
                    Assert.That(gene, Is.Not.EqualTo(MutationGene.Split),
                        "Split must not roll while gated (§14.2)");
                }
                Assert.That(MutationRules.ActiveGenes().Count, Is.EqualTo(4));

                MutationRules.SplitGeneEnabled = true;
                Assert.That(MutationRules.ActiveGenes().Count, Is.EqualTo(5));
            }
            finally
            {
                MutationRules.SplitGeneEnabled = false;
            }
        }

        [Test]
        public void A_gene_never_lands_on_a_chassis_that_already_owns_it()
        {
            Assert.That(MutationRules.IsCompatible(MutationGene.Volatile, "explode"), Is.False);
            Assert.That(MutationRules.IsCompatible(MutationGene.Rush, "zigzag"), Is.False);
            Assert.That(MutationRules.IsCompatible(MutationGene.Ballistic, "ranged"), Is.False);
            Assert.That(MutationRules.IsCompatible(MutationGene.Regenerative, "support"), Is.False);
            Assert.That(MutationRules.IsCompatible(MutationGene.Split, "split"), Is.False);

            // Cross-combinations are the point of the system.
            Assert.That(MutationRules.IsCompatible(MutationGene.Volatile, "zigzag"), Is.True);
            Assert.That(MutationRules.IsCompatible(MutationGene.Rush, "direct"), Is.True);
            Assert.That(MutationRules.IsCompatible(MutationGene.Ballistic, "explode"), Is.True);
            Assert.That(MutationRules.IsCompatible(MutationGene.Regenerative, "direct"), Is.True);
        }

        [Test]
        public void Hybrid_chance_escalates_with_gene_nodes_destroyed()
        {
            Assert.That(MutationRules.HybridChance(0), Is.EqualTo(0.25));
            Assert.That(MutationRules.HybridChance(1), Is.EqualTo(0.40));
            Assert.That(MutationRules.HybridChance(2), Is.EqualTo(0.60));
            Assert.That(MutationRules.HybridChance(3), Is.EqualTo(0.60),
                "phase 4 is the boss; the cap holds");

            Assert.That(MutationRules.PhaseFor(0), Is.EqualTo(1));
            Assert.That(MutationRules.PhaseFor(1), Is.EqualTo(2));
            Assert.That(MutationRules.PhaseFor(2), Is.EqualTo(3));
            Assert.That(MutationRules.PhaseFor(3), Is.EqualTo(4), "Hydra Prime emerges");
        }

        [Test]
        public void Elite_hybrids_join_at_recombination()
        {
            Assert.That(MutationRules.EliteHybridAllowed(0), Is.False);
            Assert.That(MutationRules.EliteHybridAllowed(1), Is.False);
            Assert.That(MutationRules.EliteHybridAllowed(2), Is.True);
            Assert.That(MutationRules.EliteHybridAllowed(3), Is.True);
        }

        [Test]
        public void Roll_is_deterministic_and_matches_its_chance_band()
        {
            var first = MutationRules.RollHybrid(new Rng(4242u), "direct", 2);
            var second = MutationRules.RollHybrid(new Rng(4242u), "direct", 2);
            Assert.That(second, Is.EqualTo(first));

            // Distribution sanity: 60% hybrid band on a compatible chassis.
            var hybrids = 0;
            const int rolls = 20000;
            for (uint seed = 1; seed <= rolls; seed++)
                if (MutationRules.RollHybrid(new Rng(seed), "direct", 2) != MutationGene.None)
                    hybrids++;
            Assert.That((double)hybrids / rolls, Is.EqualTo(0.60).Within(0.02));
        }

        [Test]
        public void Rolls_only_produce_compatible_genes()
        {
            for (uint seed = 1; seed <= 5000; seed++)
            {
                var gene = MutationRules.RollHybrid(new Rng(seed), "explode", 2);
                Assert.That(gene, Is.Not.EqualTo(MutationGene.Volatile),
                    "an Exploder must never roll its own Volatile trait");
                Assert.That(gene, Is.Not.EqualTo(MutationGene.Split));
            }
        }

        [Test]
        public void Hybrid_names_and_modifiers_project_the_spec_examples()
        {
            Assert.That(MutationRules.HybridName("Rusher", MutationGene.Volatile),
                Is.EqualTo("Volatile Rusher"));
            Assert.That(MutationRules.HybridName("Gunner", MutationGene.Rush),
                Is.EqualTo("Rush Gunner"));
            Assert.That(MutationRules.HybridName("Brute", MutationGene.None),
                Is.EqualTo("Brute"));

            var rush = MutationRules.ModifiersFor(MutationGene.Rush);
            Assert.That(rush.RushBursts, Is.True);
            Assert.That(rush.SpeedMultiplier, Is.GreaterThan(1.0));

            var regen = MutationRules.ModifiersFor(MutationGene.Regenerative);
            Assert.That(regen.RegenPerSecond, Is.GreaterThan(0.0));

            var volatileMods = MutationRules.ModifiersFor(MutationGene.Volatile);
            Assert.That(volatileMods.DetonatesOnDeath, Is.True);

            var none = MutationRules.ModifiersFor(MutationGene.None);
            Assert.That(none.HealthMultiplier, Is.EqualTo(1.0));
            Assert.That(none.SpeedMultiplier, Is.EqualTo(1.0));
            Assert.That(none.DetonatesOnDeath, Is.False);
        }
    }
}
