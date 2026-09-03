using System;
using System.Reflection;
using NUnit.Framework;
using VoidFall.Core;
using VoidFall.Runtime;

namespace VoidFall.Tests.Editor
{
    public sealed class BossAndEnemyTweakTests
    {
        [Test]
        public void Matriarch_opens_with_reusable_bodyguards_and_warden_charge_travels_three_times_farther()
        {
            BossDefinition matriarch = null;
            BossDefinition warden = null;
            foreach (var boss in ContentCatalog.Bosses)
            {
                if (boss.Id == "matriarch") matriarch = boss;
                if (boss.Id == "warden") warden = boss;
            }

            Assert.That(matriarch, Is.Not.Null);
            Assert.That(matriarch.Attacks.Length, Is.EqualTo(3));
            Assert.That(matriarch.Attacks[0].Id, Is.EqualTo("bodyguard"));
            Assert.That(matriarch.Attacks[0].SummonCount, Is.EqualTo(8));
            Assert.That(warden, Is.Not.Null);
            Assert.That(warden.Attacks[0].Id, Is.EqualTo("charge"));
            Assert.That(warden.Attacks[0].ActiveSeconds, Is.EqualTo(1.26).Within(1e-9));
        }

        [Test]
        public void Combat_presentation_multipliers_match_the_locked_tweaks()
        {
            var rules = typeof(VoidFallGameRuntime).Assembly.GetType(
                "VoidFall.Runtime.CombatTweakRules",
                false);
            Assert.That(rules, Is.Not.Null);

            Assert.That(Invoke<double>(rules, "RusherPreviewAlpha", 0.5), Is.EqualTo(0.4).Within(1e-9));
            Assert.That(Invoke<double>(rules, "RangedPreviewAlpha", 0.4), Is.EqualTo(0.3).Within(1e-9));
            Assert.That(Invoke<double>(rules, "StandardEliteSpinMultiplier", true), Is.EqualTo(5.5).Within(1e-9));
            Assert.That(Invoke<double>(rules, "StandardEliteSpinMultiplier", false), Is.EqualTo(1.0).Within(1e-9));
            Assert.That(Invoke<string>(rules, "RosterTwoRusherAccent"), Is.EqualTo("#a855f7"));
            Assert.That(Invoke<bool>(rules, "ShowStandardEliteOverlay"), Is.False);
            Assert.That(
                Invoke<double>(rules, "MatriarchBodyguardOrbitAngle", 0.0, 1) -
                Invoke<double>(rules, "MatriarchBodyguardOrbitAngle", 0.0, 0),
                Is.EqualTo(Math.PI / 4.0).Within(1e-9));
            Assert.That(
                Invoke<double>(rules, "WardenRushRotationDegrees", 0.63, 1.26),
                Is.EqualTo(720.0).Within(1e-9));
        }

        [Test]
        public void Every_upgrade_target_resolves_to_one_icon_cell()
        {
            var method = typeof(VoidFallGameRuntime).GetMethod(
                "BuildChipIconSlot",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            foreach (var support in ExtendedCatalog.AllSupports())
            {
                var slot = (int)method.Invoke(null, new object[] { support.Id });
                Assert.That(slot, Is.InRange(0, 14), support.Id + " has no card icon");
            }
        }

        private static T Invoke<T>(Type type, string methodName, params object[] arguments)
        {
            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "Missing behavior: " + methodName);
            return (T)method.Invoke(null, arguments);
        }
    }
}
