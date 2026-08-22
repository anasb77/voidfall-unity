using System;

namespace VoidFall.Core
{
    /// <summary>
    /// The prototype Hydra genes (spec §14). One enemy chassis + one gene
    /// maximum — the one-gene rule is the readability guardrail, so it lives
    /// here rather than at call sites.
    /// </summary>
    public enum MutationGene
    {
        None = 0,
        Volatile = 1,
        Rush = 2,
        Ballistic = 3,
        Regenerative = 4,
        Split = 5
    }

    /// <summary>The stat/behavior deltas a gene grafts onto its chassis.</summary>
    public struct MutationModifiers
    {
        public double HealthMultiplier;
        public double SpeedMultiplier;
        public double RegenPerSecond;
        public bool DetonatesOnDeath;
        public bool RushBursts;
        public bool FiresBursts;
        public bool SplitsOnDeath;
    }

    /// <summary>Static definition of one gene: identity, visual language, tuning.</summary>
    public sealed class MutationGeneInfo
    {
        public MutationGene Gene;
        public string DisplayName;
        public string VisualCue;
        public string OriginBehavior;
        public string AbilitySummary;
        public MutationModifiers Modifiers;
    }

    /// <summary>
    /// Hydra hybridization rules (spec §14). Hybrid chance escalates with
    /// Gene Nodes destroyed (§14.4), every gene carries exactly one visual
    /// language across every chassis (§14.3), and a gene never lands on a
    /// chassis that already owns that behavior — a Volatile Exploder is
    /// unreadable noise, not a new threat.
    /// </summary>
    public static class MutationRules
    {
        /// <summary>
        /// Split stays locked for the first prototype pass (§14.2: do not
        /// enable until the first four traits are stable). The host flips
        /// this only after that playtest call.
        /// </summary>
        public static bool SplitGeneEnabled;

        private static readonly MutationGeneInfo[] Genes =
        {
            new MutationGeneInfo
            {
                Gene = MutationGene.Volatile,
                DisplayName = "Volatile",
                VisualCue = "pulsing swollen core",
                OriginBehavior = "explode",
                AbilitySummary = "Detonates on death or contact.",
                Modifiers = new MutationModifiers
                {
                    HealthMultiplier = 1.0,
                    SpeedMultiplier = 1.0,
                    DetonatesOnDeath = true
                }
            },
            new MutationGeneInfo
            {
                Gene = MutationGene.Rush,
                DisplayName = "Rush",
                VisualCue = "stretched motion streak, leg glow",
                OriginBehavior = "zigzag",
                AbilitySummary = "Periodic acceleration burst toward the player.",
                Modifiers = new MutationModifiers
                {
                    HealthMultiplier = 1.0,
                    SpeedMultiplier = 1.15,
                    RushBursts = true
                }
            },
            new MutationGeneInfo
            {
                Gene = MutationGene.Ballistic,
                DisplayName = "Ballistic",
                VisualCue = "visible weapon node",
                OriginBehavior = "ranged",
                AbilitySummary = "Periodically stops to fire a short projectile burst.",
                Modifiers = new MutationModifiers
                {
                    HealthMultiplier = 1.0,
                    SpeedMultiplier = 0.9,
                    FiresBursts = true
                }
            },
            new MutationGeneInfo
            {
                Gene = MutationGene.Regenerative,
                DisplayName = "Regenerative",
                VisualCue = "green biological pulse",
                OriginBehavior = "support",
                AbilitySummary = "Regenerates HP unless recently damaged.",
                Modifiers = new MutationModifiers
                {
                    HealthMultiplier = 1.1,
                    SpeedMultiplier = 1.0,
                    RegenPerSecond = 2.0
                }
            },
            new MutationGeneInfo
            {
                Gene = MutationGene.Split,
                DisplayName = "Split",
                VisualCue = "duplicated body segments",
                OriginBehavior = "split",
                AbilitySummary = "Death creates smaller versions.",
                Modifiers = new MutationModifiers
                {
                    HealthMultiplier = 0.9,
                    SpeedMultiplier = 1.0,
                    SplitsOnDeath = true
                }
            }
        };

        /// <summary>Hybrid share of spawns per Gene Nodes destroyed (§14.4).</summary>
        public static double HybridChance(int geneNodesDestroyed)
        {
            switch (geneNodesDestroyed)
            {
                case 0: return 0.25; // Phase 1 — Contamination
                case 1: return 0.40; // Phase 2 — Adaptation
                default: return 0.60; // Phase 3 — Recombination
            }
        }

        /// <summary>Hydra's escalation phase for the current node count (§14.4).</summary>
        public static int PhaseFor(int geneNodesDestroyed)
        {
            return Math.Min(4, Math.Max(1, geneNodesDestroyed + 1));
        }

        /// <summary>Elite hybrids join at Phase 3 — Recombination (§14.4).</summary>
        public static bool EliteHybridAllowed(int geneNodesDestroyed)
        {
            return PhaseFor(geneNodesDestroyed) >= 3;
        }

        public static MutationGeneInfo InfoFor(MutationGene gene)
        {
            foreach (var info in Genes)
                if (info.Gene == gene) return info;
            return null;
        }

        /// <summary>All active genes for rolling, honoring the Split gate.</summary>
        public static System.Collections.Generic.List<MutationGeneInfo> ActiveGenes()
        {
            var active = new System.Collections.Generic.List<MutationGeneInfo>();
            foreach (var info in Genes)
                if (SplitGeneEnabled || info.Gene != MutationGene.Split)
                    active.Add(info);
            return active;
        }

        /// <summary>
        /// A gene is incompatible with a chassis that already owns that
        /// gene's origin behavior: the hybrid would be unreadable, not new.
        /// </summary>
        public static bool IsCompatible(MutationGene gene, string chassisBehavior)
        {
            var info = InfoFor(gene);
            if (info == null) return false;
            return !string.Equals(info.OriginBehavior, chassisBehavior,
                StringComparison.Ordinal);
        }

        /// <summary>
        /// Deterministic spawn-time roll: with HybridChance probability the
        /// chassis becomes a hybrid carrying one compatible gene. The chance
        /// and the gene both draw from the passed stream, so a fixed seed
        /// reproduces the exact hybrid composition of a run.
        /// </summary>
        public static MutationGene RollHybrid(Rng rng, string chassisBehavior,
            int geneNodesDestroyed)
        {
            if (rng == null || rng.Next() >= HybridChance(geneNodesDestroyed))
                return MutationGene.None;

            var active = ActiveGenes();
            var compatible = new System.Collections.Generic.List<MutationGeneInfo>();
            foreach (var info in active)
                if (IsCompatible(info.Gene, chassisBehavior)) compatible.Add(info);
            if (compatible.Count == 0) return MutationGene.None;
            return compatible[rng.Int(compatible.Count)].Gene;
        }

        /// <summary>Player-facing hybrid name: "Volatile Rusher" (§14.2).</summary>
        public static string HybridName(string chassisName, MutationGene gene)
        {
            var info = InfoFor(gene);
            if (info == null || gene == MutationGene.None) return chassisName;
            return info.DisplayName + " " + chassisName;
        }

        /// <summary>Modifiers a gene grafts onto its chassis; None is identity.</summary>
        public static MutationModifiers ModifiersFor(MutationGene gene)
        {
            var info = InfoFor(gene);
            if (info == null) return Identity;
            return info.Modifiers;
        }

        public static readonly MutationModifiers Identity = new MutationModifiers
        {
            HealthMultiplier = 1.0,
            SpeedMultiplier = 1.0
        };
    }
}
