using System;
using System.Collections.Generic;

namespace VoidFall.Core
{
    /// <summary>What a landed roulette wedge grants. Spec section 43.2.</summary>
    public enum RoulettePrizeKind
    {
        PowerUp,
        UpgradeRandomOwned,
        NewRandomCard,
        WeaponUpgradeQuality,
        SupportUpgradeQuality,
        Parts,
        WildCard,
        RareBoon,
    }

    /// <summary>
    /// Reward quality band. Improve Odds removes/upgrades the bottom band;
    /// Raise Stakes widens the top bands. The wheel visibly mixes desirable
    /// and mediocre results, so tiers are part of the presentation.
    /// </summary>
    public enum RouletteTier
    {
        Mediocre,
        Standard,
        Premium,
        Legendary,
    }

    public sealed class RouletteWedgeDefinition
    {
        public RouletteWedgeDefinition(
            RoulettePrizeKind kind,
            RouletteTier tier,
            double weight,
            string name,
            string description,
            string accent)
        {
            Kind = kind;
            Tier = tier;
            Weight = weight;
            Name = name;
            Description = description;
            Accent = accent;
        }

        public RoulettePrizeKind Kind { get; }
        public RouletteTier Tier { get; private set; }
        public double Weight { get; private set; }
        public string Name { get; }
        public string Description { get; }
        public string Accent { get; }

        /// <summary>
        /// Returns a copy with a new weight. Tables are rebuilt rather than
        /// mutated so a displayed table stays a stable snapshot while the spin
        /// animation runs.
        /// </summary>
        public RouletteWedgeDefinition WithWeight(double weight)
        {
            return new RouletteWedgeDefinition(Kind, Tier, weight, Name, Description, Accent);
        }

        /// <summary>
        /// Improve Odds conversion: a low-quality wedge is promoted one tier.
        /// Promotion alone would make rescued wedges strictly better than
        /// native wedges of the new tier; the small weight trim keeps the
        /// table's overall shape recognizable after two purchases.
        /// </summary>
        public RouletteWedgeDefinition Promoted()
        {
            var promotedTier = Tier == RouletteTier.Legendary
                ? RouletteTier.Legendary
                : (RouletteTier)((int)Tier + 1);
            return new RouletteWedgeDefinition(
                Kind,
                promotedTier,
                Weight * 0.8,
                Name,
                Description,
                Accent);
        }

        internal void ScaleWeight(double multiplier)
        {
            Weight = Math.Max(0, Weight * (double.IsNaN(multiplier) || double.IsInfinity(multiplier) ? 1 : Math.Abs(multiplier)));
        }
    }

    /// <summary>The full ceremony state for one boss kill. Spec section 43.</summary>
    public sealed class RouletteSession
    {
        private readonly List<string> _log;

        public RouletteSession(uint seed, int bossIndex, RouletteWedgeDefinition[] wedges)
        {
            Seed = seed;
            BossIndex = Math.Max(0, bossIndex);
            Wedges = wedges ?? Array.Empty<RouletteWedgeDefinition>();
            _log = new List<string>();
        }

        public uint Seed { get; }
        public int BossIndex { get; }
        public RouletteWedgeDefinition[] Wedges { get; internal set; }

        /// <summary>Wedge the wheel landed on. Sampled at Spin, revealed by animation.</summary>
        public int ResultIndex { get; internal set; } = -1;

        public RouletteWedgeDefinition Result =>
            ResultIndex >= 0 && ResultIndex < Wedges.Length ? Wedges[ResultIndex] : null;

        public int PartsSpent { get; internal set; }
        public int PartsRefunded { get; internal set; }
        public int ImproveOddsUses { get; internal set; }
        public int RaiseStakesUses { get; internal set; }
        public bool Spun => ResultIndex >= 0;

        public IReadOnlyList<string> Log => _log;

        internal void Note(string line)
        {
            if (!string.IsNullOrEmpty(line)) _log.Add(line);
        }
    }

    /// <summary>
    /// Boss Roulette ceremony rules: the wedge table, the two Parts purchases,
    /// the Void's refund roll, and the weighted landing sample.
    ///
    /// Integrity rule (spec 43.3): the result is sampled once, up front, from
    /// the run's Rng stream. The spin animation only reveals it — the wheel
    /// never re-rolls or fabricates a near-miss.
    /// </summary>
    public static class RouletteRules
    {
        /// <summary>Chance the Void refunds a wager while keeping the effect.</summary>
        public const double VoidRefundChance = 0.30;

        /// <summary>Each purchase type may be used at most this many times per ceremony.</summary>
        public const int MaxUsesPerPurchase = 2;

        /// <summary>Base Parts cost; each additional use of the same purchase doubles it.</summary>
        public const int ImproveOddsBaseCost = 25;
        public const int RaiseStakesBaseCost = 40;

        public static readonly string[] RefundLines =
        {
            "The Void is amused by your courage. Your Parts have been returned.",
            "The wager pleases the Void. No tribute required.",
            "Something beyond the veil laughs. Your Parts return.",
            "The Void accepts your audacity. Keep your offering.",
            "Fortune bends. The price is forgiven.",
        };

        /// <summary>
        /// The prototype wedge table. Deliberately mixed quality: mediocre
        /// wedges must be visible for the Parts purchases to matter. Weights
        /// are relative, not percentages.
        /// </summary>
        public static RouletteWedgeDefinition[] DefaultTable()
        {
            return new RouletteWedgeDefinition[]
            {
                new RouletteWedgeDefinition(RoulettePrizeKind.Parts, RouletteTier.Mediocre,
                    16, "PARTS CACHE", "A modest pile of Parts.", "#7f8ea8"),
                new RouletteWedgeDefinition(RoulettePrizeKind.UpgradeRandomOwned, RouletteTier.Standard,
                    20, "RANDOM UPGRADE", "A random owned card gains a rank.", "#4fc3f7"),
                new RouletteWedgeDefinition(RoulettePrizeKind.NewRandomCard, RouletteTier.Standard,
                    14, "NEW CARD", "Gain one random card you do not own.", "#4fc3f7"),
                new RouletteWedgeDefinition(RoulettePrizeKind.WeaponUpgradeQuality, RouletteTier.Premium,
                    12, "WEAPON FORGE", "One weapon gains two ranks.", "#ffd54f"),
                new RouletteWedgeDefinition(RoulettePrizeKind.SupportUpgradeQuality, RouletteTier.Premium,
                    10, "SYSTEMS TUNE", "One support gains two ranks.", "#ffd54f"),
                new RouletteWedgeDefinition(RoulettePrizeKind.PowerUp, RouletteTier.Standard,
                    14, "VOID GIFT", "A pickable power-up appears.", "#81c784"),
                new RouletteWedgeDefinition(RoulettePrizeKind.RareBoon, RouletteTier.Legendary,
                    6, "RARE BOON", "A powerful run-only boon.", "#ce93d8"),
                new RouletteWedgeDefinition(RoulettePrizeKind.WildCard, RouletteTier.Legendary,
                    4, "WILD CARD", "A rule-breaking card.", "#ff7043"),
            };
        }

        public static int ImproveOddsCost(int usesAlreadyMade)
        {
            return CostFor(ImproveOddsBaseCost, usesAlreadyMade);
        }

        public static int RaiseStakesCost(int usesAlreadyMade)
        {
            return CostFor(RaiseStakesBaseCost, usesAlreadyMade);
        }

        private static int CostFor(int baseCost, int usesAlreadyMade)
        {
            var uses = Math.Max(0, usesAlreadyMade);
            var cost = baseCost;
            for (var index = 0; index < uses; index++) cost *= 2;
            return cost;
        }

        /// <summary>
        /// Improve Odds: every Mediocre wedge is promoted one tier. Expected
        /// reward quality rises unambiguously — no filler wedges are added.
        /// </summary>
        public static RouletteWedgeDefinition[] ApplyImproveOdds(RouletteWedgeDefinition[] table)
        {
            if (table == null || table.Length == 0) return DefaultTable();
            var next = new RouletteWedgeDefinition[table.Length];
            for (var index = 0; index < table.Length; index++)
            {
                next[index] = table[index].Tier == RouletteTier.Mediocre
                    ? table[index].Promoted()
                    : table[index];
            }

            return next;
        }

        /// <summary>
        /// Raise Stakes: doubles every Legendary weight and guarantees a Wild
        /// Card slice exists on the wheel, so the purchase always adds
        /// high-tier potential rather than only inflating what is there.
        /// </summary>
        public static RouletteWedgeDefinition[] ApplyRaiseStakes(RouletteWedgeDefinition[] table)
        {
            var source = table == null || table.Length == 0 ? DefaultTable() : table;
            var next = new List<RouletteWedgeDefinition>(source.Length + 1);
            var addedWild = false;
            for (var index = 0; index < source.Length; index++)
            {
                var wedge = source[index];
                if (wedge.Tier == RouletteTier.Legendary)
                {
                    wedge = wedge.WithWeight(wedge.Weight * 2);
                    if (wedge.Kind == RoulettePrizeKind.WildCard) addedWild = true;
                }

                next.Add(wedge);
            }

            if (!addedWild)
            {
                next.Add(new RouletteWedgeDefinition(
                    RoulettePrizeKind.WildCard,
                    RouletteTier.Legendary,
                    4,
                    "WILD CARD",
                    "A rule-breaking card.",
                    "#ff7043"));
            }

            return next.ToArray();
        }

        /// <summary>
        /// Spends Parts on one purchase. The Void's refund rolls first: on a
        /// refund the effect is kept and the cost returned, so the session's
        /// net spend — not the button press — is what the economy sees.
        /// Returns false when unaffordable, capped out, or already spun.
        /// </summary>
        public static bool Purchase(
            RouletteSession session,
            bool improveOdds,
            int availableParts,
            Rng random,
            out int cost,
            out string refundLine)
        {
            cost = 0;
            refundLine = null;
            if (session == null || session.Spun) return false;

            var uses = improveOdds ? session.ImproveOddsUses : session.RaiseStakesUses;
            if (uses >= MaxUsesPerPurchase) return false;

            cost = improveOdds ? ImproveOddsCost(uses) : RaiseStakesCost(uses);
            if (availableParts < cost) return false;

            if (improveOdds) session.ImproveOddsUses++;
            else session.RaiseStakesUses++;

            session.Wedges = improveOdds
                ? ApplyImproveOdds(session.Wedges)
                : ApplyRaiseStakes(session.Wedges);

            session.PartsSpent += cost;

            var roll = random != null ? random.Next() : 1;
            if (roll < VoidRefundChance)
            {
                session.PartsRefunded += cost;
                var lineIndex = random != null
                    ? (int)(random.Next() * RefundLines.Length) % RefundLines.Length
                    : 0;
                refundLine = RefundLine(lineIndex);
                session.Note(refundLine);
            }

            return true;
        }

        public static string RefundLine(int index)
        {
            return RefundLines[Math.Abs(index) % RefundLines.Length];
        }

        /// <summary>
        /// Samples the landing wedge once from the current table weights.
        /// Called at ceremony open before any animation; the view only
        /// reveals what was already chosen.
        /// </summary>
        public static void Spin(RouletteSession session, Rng random)
        {
            if (session == null || session.Spun) return;
            var wedges = session.Wedges;
            if (wedges.Length == 0)
            {
                session.ResultIndex = 0;
                return;
            }

            var total = 0.0;
            for (var index = 0; index < wedges.Length; index++)
            {
                total += Math.Max(0, wedges[index].Weight);
            }

            if (total <= 0)
            {
                session.ResultIndex = (int)(Roll(random) * wedges.Length) % wedges.Length;
                return;
            }

            var roll = Roll(random) * total;
            var cursor = 0.0;
            for (var index = 0; index < wedges.Length; index++)
            {
                cursor += Math.Max(0, wedges[index].Weight);
                if (roll < cursor)
                {
                    session.ResultIndex = index;
                    return;
                }
            }

            session.ResultIndex = wedges.Length - 1;
        }

        private static double Roll(Rng random)
        {
            // A missing stream must still be bounded and neutral; 0.5 lands
            // mid-table instead of biasing toward the first wedge.
            return random != null ? random.Next() : 0.5;
        }
    }
}