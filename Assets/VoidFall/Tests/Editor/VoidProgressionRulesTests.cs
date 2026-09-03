using System;
using System.Reflection;
using NUnit.Framework;
using VoidFall.Core;

namespace VoidFall.Tests.Editor
{
    public sealed class VoidProgressionRulesTests
    {
        [Test]
        public void Double_boss_chance_starts_at_twenty_five_percent_and_adds_six_points_per_clear()
        {
            var rules = typeof(ArenaRules).Assembly.GetType("VoidFall.Core.VoidProgressionRules", false);
            Assert.That(rules, Is.Not.Null);
            Assert.That(Invoke<double>(rules, "DoubleBossChance", 0), Is.EqualTo(0.25).Within(1e-9));
            Assert.That(Invoke<double>(rules, "DoubleBossChance", 1), Is.EqualTo(0.31).Within(1e-9));
            Assert.That(Invoke<double>(rules, "DoubleBossChance", 2), Is.EqualTo(0.37).Within(1e-9));
            Assert.That(Invoke<double>(rules, "DoubleBossChance", 99), Is.EqualTo(1.0).Within(1e-9));
        }

        [Test]
        public void Post_boss_delay_is_deterministic_and_always_between_fourteen_and_twenty_two_seconds()
        {
            var rules = typeof(ArenaRules).Assembly.GetType("VoidFall.Core.VoidProgressionRules", false);
            Assert.That(rules, Is.Not.Null);
            for (uint seed = 1; seed <= 128; seed++)
            {
                var first = Invoke<int>(rules, "PostBossDelaySeconds", seed, 3);
                var second = Invoke<int>(rules, "PostBossDelaySeconds", seed, 3);
                Assert.That(first, Is.InRange(14, 22));
                Assert.That(second, Is.EqualTo(first));
            }
        }

        [Test]
        public void Special_voids_keep_their_authored_boss_encounters()
        {
            var rules = typeof(ArenaRules).Assembly.GetType("VoidFall.Core.VoidProgressionRules", false);
            Assert.That(rules, Is.Not.Null);
            Assert.That(Invoke<string>(rules, "SpecialBossId", "hydra"), Is.EqualTo("hydra-prime"));
            Assert.That(Invoke<string>(rules, "SpecialBossId", "monochrome-court"), Is.EqualTo("court-grandmasters"));
            Assert.That(Invoke<string>(rules, "SpecialBossId", "abyss"), Is.Null);
        }

        private static T Invoke<T>(Type type, string methodName, params object[] arguments)
        {
            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "Missing behavior: " + methodName);
            return (T)method.Invoke(null, arguments);
        }
    }
}
