using NUnit.Framework;
using VoidFall.Core;

namespace VoidFall.Tests.Editor
{
    public sealed class RouletteRulesTests
    {
        [Test]
        public void Default_table_has_positive_weights_and_names()
        {
            var table = RouletteRules.DefaultTable();
            Assert.That(table.Length, Is.GreaterThanOrEqualTo(6), "wheel needs enough slices to read as a wheel");
            foreach (var wedge in table)
            {
                Assert.That(wedge.Weight, Is.GreaterThan(0), wedge.Name + " has no weight");
                Assert.That(wedge.Name, Is.Not.Empty);
                Assert.That(wedge.Description, Is.Not.Empty);
                Assert.That(wedge.Accent, Is.Not.Empty);
            }

            // Mixed quality is a presentation requirement: the player must see
            // mediocre wedges for Improve Odds to be a meaningful purchase.
            bool hasMediocre = false, hasLegendary = false;
            foreach (var wedge in table)
            {
                hasMediocre |= wedge.Tier == RouletteTier.Mediocre;
                hasLegendary |= wedge.Tier == RouletteTier.Legendary;
            }
            Assert.That(hasMediocre, Is.True, "table lost its mediocre band");
            Assert.That(hasLegendary, Is.True, "table lost its legendary band");
        }

        [Test]
        public void Spin_is_deterministic_for_a_seed()
        {
            var first = new RouletteSession(1234u, 0, RouletteRules.DefaultTable());
            var second = new RouletteSession(1234u, 0, RouletteRules.DefaultTable());
            RouletteRules.Spin(first, new Rng(7777u));
            RouletteRules.Spin(second, new Rng(7777u));
            Assert.That(second.ResultIndex, Is.EqualTo(first.ResultIndex));
        }

        [Test]
        public void Spin_stays_in_range_across_many_seeds()
        {
            for (uint seed = 1; seed <= 500; seed++)
            {
                var session = new RouletteSession(seed, 0, RouletteRules.DefaultTable());
                RouletteRules.Spin(session, new Rng(seed * 31 + 7));
                Assert.That(session.ResultIndex, Is.InRange(0, session.Wedges.Length - 1));
                Assert.That(session.Result, Is.Not.Null);
            }
        }

        [Test]
        public void Costs_escalate_and_cap_out()
        {
            Assert.That(RouletteRules.ImproveOddsCost(0), Is.EqualTo(25));
            Assert.That(RouletteRules.ImproveOddsCost(1), Is.EqualTo(50));
            Assert.That(RouletteRules.RaiseStakesCost(0), Is.EqualTo(40));

            var session = new RouletteSession(5u, 0, RouletteRules.DefaultTable());
            var rng = new Rng(9999u);
            int spent = 0;
            string line;

            Assert.That(RouletteRules.Purchase(session, true, 10_000, rng, out _, out line), Is.True);
            spent += 25;
            Assert.That(RouletteRules.Purchase(session, true, 10_000, rng, out _, out line), Is.True);
            spent += 50;
            Assert.That(session.ImproveOddsUses, Is.EqualTo(RouletteRules.MaxUsesPerPurchase));
            Assert.That(RouletteRules.Purchase(session, true, 10_000, rng, out _, out line),
                Is.False, "purchase cap was not enforced");

            // Unaffordable purchases are rejected without state changes.
            var poor = new RouletteSession(6u, 0, RouletteRules.DefaultTable());
            Assert.That(RouletteRules.Purchase(poor, false, 39, new Rng(1u), out _, out line), Is.False);
            Assert.That(poor.RaiseStakesUses, Is.EqualTo(0));
            Assert.That(poor.PartsSpent, Is.EqualTo(0));
        }

        [Test]
        public void Refund_keeps_the_effect_and_returns_the_cost()
        {
            // Find a seed whose first draw lands inside the 30% refund band.
            uint seed = 1;
            for (; seed < 5_000; seed++)
            {
                if (new Rng(seed).Next() < RouletteRules.VoidRefundChance) break;
            }

            var session = new RouletteSession(11u, 2, RouletteRules.DefaultTable());
            string line;
            var purchased = RouletteRules.Purchase(
                session, true, 1_000, new Rng(seed), out var cost, out line);

            Assert.That(purchased, Is.True);
            Assert.That(cost, Is.EqualTo(RouletteRules.ImproveOddsCost(0)));
            Assert.That(session.PartsRefunded, Is.EqualTo(cost), "refund did not return the wager");
            Assert.That(line, Is.Not.Null.And.Not.Empty);
            Assert.That(session.ImproveOddsUses, Is.EqualTo(1), "refund must keep the purchased effect");
        }

        [Test]
        public void No_refund_when_roll_misses_the_band()
        {
            // Find a seed whose first draw is outside the refund band.
            uint seed = 1;
            for (; seed < 5_000; seed++)
            {
                if (new Rng(seed).Next() >= RouletteRules.VoidRefundChance) break;
            }

            var session = new RouletteSession(12u, 0, RouletteRules.DefaultTable());
            string line;
            Assert.That(RouletteRules.Purchase(
                session, false, 1_000, new Rng(seed), out var cost, out line), Is.True);
            Assert.That(line, Is.Null);
            Assert.That(session.PartsRefunded, Is.EqualTo(0));
        }

        [Test]
        public void Improve_odds_promotes_every_mediocre_wedge()
        {
            var table = RouletteRules.ApplyImproveOdds(RouletteRules.DefaultTable());
            foreach (var wedge in table)
            {
                Assert.That(wedge.Tier, Is.Not.EqualTo(RouletteTier.Mediocre),
                    wedge.Name + " survived Improve Odds as mediocre");
            }
        }

        [Test]
        public void Raise_stakes_doubles_legendary_and_guarantees_a_wild_card()
        {
            var table = RouletteRules.ApplyRaiseStakes(RouletteRules.DefaultTable());
            RouletteWedgeDefinition wild = null;
            foreach (var wedge in table)
            {
                if (wedge.Kind != RoulettePrizeKind.WildCard) continue;
                wild = wedge;
                if (wedge.Name == "WILD CARD" && !wedge.Description.Contains("rule-breaking")) continue;
                break;
            }

            Assert.That(wild, Is.Not.Null);
            double legendaryWeight = 0;
            foreach (var wedge in table)
            {
                if (wedge.Tier == RouletteTier.Legendary && wedge.Kind == RoulettePrizeKind.RareBoon)
                {
                    legendaryWeight = wedge.Weight;
                }
            }
            Assert.That(legendaryWeight, Is.EqualTo(12.0), "legendary weight was not doubled");
        }
    }
}