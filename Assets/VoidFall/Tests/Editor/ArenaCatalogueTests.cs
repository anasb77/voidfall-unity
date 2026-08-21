using System.Collections.Generic;
using NUnit.Framework;
using VoidFall.Core;

namespace VoidFall.Tests.Editor
{
    public sealed class ArenaCatalogueTests
    {
        [TestCase(ArenaId.Void, "abyss")]
        [TestCase(ArenaId.RedNebula, "red-nebula")]
        [TestCase(ArenaId.WhiteSakura, "white-sakura")]
        public void StableIdsBridgeExistingArenaEnum(ArenaId arena, string expected)
        {
            Assert.That(ArenaCatalogRules.StableId(arena), Is.EqualTo(expected));
            Assert.That(ArenaCatalogRules.LegacyArena(expected), Is.EqualTo(arena));
        }

        [Test]
        public void RecipeSelectionIsDeterministicAndBounded()
        {
            for (uint seed = 1; seed < 100; seed++)
            {
                var first = ArenaCatalogRules.RecipeIndex(seed, "white-sakura");
                var second = ArenaCatalogRules.RecipeIndex(seed, "white-sakura");
                Assert.That(second, Is.EqualTo(first));
                Assert.That(first, Is.InRange(0, ArenaCatalogRules.RecipesPerArena - 1));
            }
        }

        [Test]
        public void ThreeRecipesHaveDistinctCuratedLayoutsAndStableAddresses()
        {
            var salts = new HashSet<uint>();
            for (var index = 0; index < ArenaCatalogRules.RecipesPerArena; index++)
            {
                var layout = ArenaCatalogRules.RecipeLayout(index);
                Assert.That(layout.Index, Is.EqualTo(index));
                Assert.That(salts.Add(layout.DecorSalt), Is.True);
                Assert.That(layout.DetailScale, Is.InRange(1f, 1.1f));
                Assert.That(
                    ArenaCatalogRules.PackageAddress(new ArenaPackageKey("white-sakura", index)),
                    Is.EqualTo("VoidFall/Arenas/white-sakura/recipe-" + (index + 1)));
            }
        }

        [Test]
        public void SteadyResidencyContainsCurrentAndTwoDistinctExits()
        {
            var current = new ArenaPackageKey("abyss", 0);
            var exitA = new ArenaPackageKey("red-nebula", 1);
            var exitB = new ArenaPackageKey("white-sakura", 2);

            var plan = ArenaResidencyPlanner.Steady(current, exitA, exitB);

            Assert.That(plan.Count, Is.EqualTo(3));
            Assert.That(plan.Contains(current), Is.True);
            Assert.That(plan.Contains(exitA), Is.True);
            Assert.That(plan.Contains(exitB), Is.True);
        }

        [Test]
        public void TransitionReleasesRejectedExitBeforeLoadingNextNeighborhood()
        {
            var oldCurrent = new ArenaPackageKey("abyss", 0);
            var chosen = new ArenaPackageKey("red-nebula", 1);
            var rejected = new ArenaPackageKey("white-sakura", 2);
            var nextA = new ArenaPackageKey("hydra-reach", 0);
            var nextB = new ArenaPackageKey("anubis-gate", 2);
            var steady = ArenaResidencyPlanner.Steady(oldCurrent, chosen, rejected);

            var transition = ArenaResidencyPlanner.Transition(
                steady,
                oldCurrent,
                chosen,
                rejected,
                nextA,
                nextB);

            Assert.That(transition.ReleaseBeforeAcquire, Is.EqualTo(new[] { rejected }));
            Assert.That(transition.Acquire, Is.EqualTo(new[] { nextA, nextB }));
            Assert.That(transition.ReleaseAfterTransition, Is.EqualTo(new[] { oldCurrent }));

            var resident = new HashSet<ArenaPackageKey>(steady.Items);
            resident.Remove(transition.ReleaseBeforeAcquire[0]);
            var peak = resident.Count;
            foreach (var key in transition.Acquire)
            {
                resident.Add(key);
                peak = System.Math.Max(peak, resident.Count);
            }
            Assert.That(peak, Is.LessThanOrEqualTo(4));
        }

        [Test]
        public void DuplicateOrMissingExitsDoNotInflateResidentSet()
        {
            var current = new ArenaPackageKey("void-heart", 0);
            var plan = ArenaResidencyPlanner.Steady(current, current, default);
            Assert.That(plan.Count, Is.EqualTo(1));
            Assert.That(plan.Items[0], Is.EqualTo(current));
        }
    }
}
