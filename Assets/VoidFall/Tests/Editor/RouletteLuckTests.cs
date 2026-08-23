using NUnit.Framework;
using VoidFall.Core;

namespace VoidFall.Tests.Editor
{
    /// <summary>
    /// Covers the roulette luck and protection layers: pity escalation
    /// across ceremonies, the first-ceremony floor, repeat protection, and
    /// the hard guarantee that legacy spins stay bit-identical.
    /// </summary>
    public sealed class RouletteLuckTests
    {
        [Test]
        public void Luck_tilts_the_table_upward_and_is_bounded()
        {
            var plain = RouletteRules.DefaultTable();
            var untouched = RouletteRules.ApplyLuck(plain, 0);
            for (var index = 0; index < plain.Length; index++)
                Assert.That(untouched[index].Weight, Is.EqualTo(plain[index].Weight).Within(1e-9),
                    "zero ceremonies must leave every weight untouched");

            var lucky = RouletteRules.ApplyLuck(RouletteRules.DefaultTable(), 3);
            for (var index = 0; index < plain.Length; index++)
            {
                var ratio = lucky[index].Weight / plain[index].Weight;
                if (plain[index].Tier == RouletteTier.Mediocre)
                    Assert.That(ratio, Is.EqualTo(0.85 * 0.85 * 0.85).Within(1e-9),
                        "mediocre slices fade with each ceremony");
                else if (plain[index].Tier == RouletteTier.Legendary)
                    Assert.That(ratio, Is.EqualTo(1.0 + 0.15 * 3).Within(1e-9),
                        "legendary slices grow with each ceremony");
                else if (plain[index].Tier == RouletteTier.Premium)
                    Assert.That(ratio, Is.EqualTo(1.0 + 0.10 * 3).Within(1e-9));
                else
                    Assert.That(ratio, Is.EqualTo(1.0).Within(1e-9),
                        "standard slices stay flat");
            }

            var capped = RouletteRules.ApplyLuck(RouletteRules.DefaultTable(), 99);
            var cappedMediocre = capped[0].Weight / plain[0].Weight;
            Assert.That(cappedMediocre,
                Is.EqualTo(System.Math.Pow(0.85, RouletteRules.LuckCapCeremonies)).Within(1e-9),
                "the pity stops escalating past the cap");
        }

        [Test]
        public void Default_context_spins_remain_bit_identical_to_legacy()
        {
            for (uint seed = 1; seed <= 50; seed++)
            {
                var legacy = new RouletteSession(seed, 0, RouletteRules.DefaultTable());
                var modern = new RouletteSession(seed, 0, RouletteRules.DefaultTable());
                RouletteRules.Spin(legacy, new Rng(seed));
                RouletteRules.Spin(modern, new Rng(seed), default(RouletteSpinContext));
                Assert.That(modern.ResultIndex, Is.EqualTo(legacy.ResultIndex),
                    "seed " + seed + " drifted: the default context must not protect");
            }
        }

        [Test]
        public void First_ceremony_floor_doubles_the_escape_odds_exactly_once()
        {
            // A 90/10 mediocre table: unprotected spins escape mediocre ~10%
            // of the time. The floor's single bounded re-sample raises that
            // to ~19% (1 - 0.9^2). The integrity rule caps it there - the
            // wheel never re-rolls twice to force a win.
            var table = new RouletteWedgeDefinition[]
            {
                new RouletteWedgeDefinition(RoulettePrizeKind.Parts, RouletteTier.Mediocre,
                    90, "PARTS CACHE", "A modest pile of Parts.", "#7f8ea8"),
                new RouletteWedgeDefinition(RoulettePrizeKind.RareBoon, RouletteTier.Legendary,
                    10, "RARE BOON", "A powerful run-only boon.", "#ce93d8"),
            };

            var escapedProtected = 0;
            var escapedUnprotected = 0;
            for (uint seed = 1; seed <= 400; seed++)
            {
                var protectedSession = new RouletteSession(seed, 0, table);
                RouletteRules.Spin(protectedSession, new Rng(seed), new RouletteSpinContext
                {
                    CeremoniesSeen = 0,
                    ProtectionsEnabled = true
                });
                if (protectedSession.Result.Tier != RouletteTier.Mediocre) escapedProtected++;

                var plainSession = new RouletteSession(seed, 0, table);
                RouletteRules.Spin(plainSession, new Rng(seed));
                if (plainSession.Result.Tier != RouletteTier.Mediocre) escapedUnprotected++;
            }

            Assert.That(escapedUnprotected, Is.InRange(24, 56),
                "sanity: ~10% of 400 unprotected spins escape mediocre");
            Assert.That(escapedProtected, Is.EqualTo(2 * escapedUnprotected).Within(24),
                "one re-sample roughly doubles the escape rate (1 - 0.9^2 = 19%)");
            Assert.That(escapedProtected, Is.GreaterThan(escapedUnprotected),
                "the floor strictly helps");
        }

        [Test]
        public void Repeat_protection_re_samples_once_and_never_fishes()
        {
            var table = RouletteRules.DefaultTable();
            var context = new RouletteSpinContext
            {
                CeremoniesSeen = 3,
                ProtectionsEnabled = true,
                HasPrevious = true,
                PreviousKind = RoulettePrizeKind.UpgradeRandomOwned,
                PreviousTier = RouletteTier.Standard
            };

            for (uint seed = 1; seed <= 200; seed++)
            {
                var protectedSession = new RouletteSession(seed, 1, table);
                var unprotected = new RouletteSession(seed, 1, table);
                var protectedRng = new Rng(seed);
                var unprotectedRng = new Rng(seed);
                RouletteRules.Spin(protectedSession, protectedRng, context);
                RouletteRules.Spin(unprotected, unprotectedRng);

                var firstWasRepeat = unprotected.Result.Kind == context.PreviousKind &&
                    unprotected.Result.Tier <= RouletteTier.Standard;
                if (!firstWasRepeat)
                {
                    Assert.That(protectedSession.ResultIndex, Is.EqualTo(unprotected.ResultIndex),
                        "no protection should fire when the first sample is fine");
                    Assert.That(protectedRng.Draws, Is.EqualTo(unprotectedRng.Draws));
                }
                else
                {
                    Assert.That(protectedRng.Draws, Is.EqualTo(unprotectedRng.Draws + 1),
                        "a triggered protection consumes exactly one extra draw");
                }
            }
        }

        [Test]
        public void Ceremonies_seen_is_what_separates_floor_from_repeat()
        {
            // With ceremonies seen > 0 the mediocre floor no longer fires:
            // a mediocre landing stays put unless it repeats the previous.
            var table = new RouletteWedgeDefinition[]
            {
                new RouletteWedgeDefinition(RoulettePrizeKind.Parts, RouletteTier.Mediocre,
                    100, "PARTS CACHE", "A modest pile of Parts.", "#7f8ea8"),
            };
            var session = new RouletteSession(1u, 1, table);
            RouletteRules.Spin(session, new Rng(1u), new RouletteSpinContext
            {
                CeremoniesSeen = 2,
                ProtectionsEnabled = true
            });
            Assert.That(session.Result.Tier, Is.EqualTo(RouletteTier.Mediocre),
                "later ceremonies accept mediocre results: pity is weight-side only");
        }
    }
}
