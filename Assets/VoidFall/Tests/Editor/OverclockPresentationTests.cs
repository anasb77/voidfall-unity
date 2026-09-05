using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using VoidFall.Core;
using VoidFall.UI;

namespace VoidFall.Tests.Editor
{
    public sealed class OverclockPresentationTests
    {
        [TestCase(1, 1f)]
        [TestCase(2, 1.1f)]
        [TestCase(3, 1.2f)]
        [TestCase(5, 1.4f)]
        [TestCase(10, 1.9f)]
        public void Each_additional_stack_grows_the_counter_by_ten_percent(int stack, float expected)
        {
            var rules = typeof(OverclockRules).Assembly.GetType("VoidFall.Core.OverclockPresentationRules");
            Assert.That(rules, Is.Not.Null, "The HUD needs a shared presentation rule for stack growth.");
            var method = rules.GetMethod("StackScale");
            Assert.That(method, Is.Not.Null);
            Assert.That((float)method.Invoke(null, new object[] { stack }), Is.EqualTo(expected).Within(0.0001));
        }

        [Test]
        public void Stacking_keeps_the_activation_pattern_and_expiry_allows_a_new_one()
        {
            var host = new GameObject("Overclock pattern test", typeof(RectTransform));
            try
            {
                var graphic = host.AddComponent<MusicPerimeterGraphic>();
                graphic.Configure(12345, 2, false);
                var property = typeof(MusicPerimeterGraphic).GetProperty("ActivationIndex");
                Assert.That(property, Is.Not.Null, "Patterns must be scoped to an activation, not only a run.");
                graphic.SetPresentation(.4f, .3f, .2f, 0, 1, 1, 1, false, 0, 1, .016f);
                var first = (int)property.GetValue(graphic);
                graphic.SetPresentation(.5f, .4f, .3f, 0, 3, 5, 1, false, 0, 1, .016f);
                Assert.That((int)property.GetValue(graphic), Is.EqualTo(first));
                graphic.SetPresentation(0, 0, 0, 0, 0, 0, 0, false, 0, 1, .016f);
                graphic.SetPresentation(.4f, .3f, .2f, 0, 1, 1, 1, false, 0, 1, .016f);
                Assert.That((int)property.GetValue(graphic), Is.EqualTo(first + 1));
            }
            finally { Object.DestroyImmediate(host); }
        }
    }
}
