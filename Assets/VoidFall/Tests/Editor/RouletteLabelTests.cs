using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using VoidFall.Core;
using VoidFall.UI;

namespace VoidFall.Tests.Editor
{
    public sealed class RouletteLabelTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

        [TestCase(RoulettePrizeKind.NewRandomCard, "Random New Card")]
        [TestCase(RoulettePrizeKind.UpgradeRandomOwned, "Random Card Upgrade +1 Rank")]
        [TestCase(RoulettePrizeKind.WeaponUpgradeQuality, "Random Weapon Upgrade +2 Ranks")]
        [TestCase(RoulettePrizeKind.SupportUpgradeQuality, "Random Support Upgrade +2 Ranks")]
        [TestCase(RoulettePrizeKind.PowerUp, "Random Power-Up Drop")]
        [TestCase(RoulettePrizeKind.RareBoon, "500 Parts")]
        public void Reward_names_state_the_actual_random_target_and_quantity(RoulettePrizeKind kind, string expected)
        {
            var wedge = Array.Find(RouletteRules.DefaultTable(), item => item.Kind == kind);
            Assert.That(wedge.Name, Is.EqualTo(expected));
        }

        [Test]
        public void Legacy_boon_slot_advertises_parts_without_changing_its_odds_or_id()
        {
            var wedge = Array.Find(RouletteRules.DefaultTable(), item => item.Kind == RoulettePrizeKind.RareBoon);
            Assert.That((int)wedge.Kind, Is.EqualTo(7));
            Assert.That(wedge.Weight, Is.EqualTo(6));
            Assert.That(wedge.Tier, Is.EqualTo(RouletteTier.Legendary));
            Assert.That(RoulettePresentationRules.Effect(wedge), Does.Contain("500 Parts"));
            Assert.That(RoulettePresentationRules.Effect(wedge).ToLowerInvariant(), Does.Not.Contain("integrity").And.Not.Contain("score"));
            Assert.That(RoulettePresentationRules.ShortEffect(wedge), Does.Contain("500"));
        }

        [TestCase(0, 0, false, -1, false)]
        [TestCase(0, 0, false, -1, true)]
        [TestCase(0, 0, true, -1, true)]
        [TestCase(0, 2, true, -1, true)]
        [TestCase(8, 0, false, -1, true)]
        [TestCase(8, 2, false, -1, true)]
        [TestCase(8, 2, true, -1, true)]
        [TestCase(8, 2, false, (int)RoulettePrizeKind.NewRandomCard, true)]
        [TestCase(8, 2, false, (int)RoulettePrizeKind.PowerUp, true)]
        [TestCase(8, 2, true, (int)RoulettePrizeKind.UpgradeRandomOwned, true)]
        public void Full_wheel_labels_fit_slices_or_readable_callouts_and_remain_upright(int luck, int raises, bool improve, int previousKind, bool protections)
        {
            var host = new GameObject("Roulette label test", typeof(RectTransform));
            try
            {
                ((RectTransform)host.transform).sizeDelta = new Vector2(1600, 900);
                var view = host.AddComponent<RouletteView>();
                view.Initialize(null);
                var table = RouletteRules.ApplyLuck(RouletteRules.DefaultTable(), luck);
                if (improve) table = RouletteRules.ApplyImproveOdds(table);
                for (var i = 0; i < raises; i++) table = RouletteRules.ApplyRaiseStakes(table);
                var context = new RouletteSpinContext
                {
                    ProtectionsEnabled = protections, CeremoniesSeen = luck,
                    HasPrevious = previousKind >= 0, PreviousKind = (RoulettePrizeKind)previousKind,
                };
                view.Present(new RouletteSession(1, 0, table), new Rng(21), 500, context);
                var markers = (List<RectTransform>)typeof(RouletteView).GetField("_markers", Flags).GetValue(view);
                var wheel = (RectTransform)typeof(RouletteView).GetField("_wheel", Flags).GetValue(view);
                for (var index = 0; index < table.Length; index++)
                {
                    var labels = markers[index].GetComponentsInChildren<Text>();
                    var combined = string.Join(" ", Array.ConvertAll(labels, label => label.text.Replace('\n', ' ')));
                    var expected = table[index].Kind == RoulettePrizeKind.Parts
                        ? RouletteRules.PartsReward(table[index].Tier) + " Parts" : table[index].Name;
                    Assert.That(combined, Is.EqualTo(expected).IgnoreCase, "Missing full reward meaning on slice " + index);
                    if (expected.StartsWith("Random", StringComparison.Ordinal))
                    {
                        Assert.That(labels, Has.Length.EqualTo(2));
                        Assert.That(labels[0].text, Is.EqualTo("Random"));
                        Assert.That(labels[0].fontStyle, Is.EqualTo(FontStyle.Bold));
                        Assert.That(labels[1].fontStyle, Is.EqualTo(FontStyle.Normal));
                        Assert.That(labels[1].fontSize, Is.EqualTo(labels[0].fontSize * .8f));
                        var headingBottom = labels[0].rectTransform.anchoredPosition.y - labels[0].rectTransform.rect.height * .5f;
                        var bodyTop = labels[1].rectTransform.anchoredPosition.y + labels[1].rectTransform.rect.height * .5f;
                        Assert.That(headingBottom, Is.GreaterThan(bodyTop), "Random must sit above its full description.");
                    }
                    foreach (var label in labels)
                    {
                        Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(12), label.text);
                        Assert.That(label.resizeTextForBestFit, Is.False);
                        Assert.That(label.preferredWidth, Is.LessThanOrEqualTo(label.rectTransform.rect.width + .1f), label.text);
                        Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + .1f), label.text);
                    }
                }
                typeof(RouletteView).GetField("_openElapsed", Flags).SetValue(view, 10f);
                typeof(RouletteView).GetMethod("OnSpinPressed", Flags).Invoke(view, null);
                var holder = (RectTransform)wheel.parent;
                foreach (var elapsed in new[] { 0f, .4f, 1.5f, 3.4f, 5.8f, 6.7f })
                {
                    typeof(RouletteView).GetField("_spinElapsed", Flags).SetValue(view, elapsed);
                    typeof(RouletteView).GetMethod("Update", Flags).Invoke(view, null);
                    for (var index = 0; index < table.Length; index++)
                    {
                        var marker = markers[index];
                        var outside = marker.parent != wheel;
                        if (outside)
                        {
                            var rect = BoundsIn(marker, holder);
                            Assert.That(Mathf.Min(Mathf.Abs(rect.xMin), Mathf.Abs(rect.xMax)), Is.GreaterThanOrEqualTo(299f));
                            Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(-200.1f));
                            Assert.That(rect.yMax, Is.LessThanOrEqualTo(200.1f));
                            Assert.That(holder.Find("Wedge " + index + " connector 0"), Is.Not.Null);
                            Assert.That(holder.Find("Wedge " + index + " connector 2"), Is.Not.Null);
                        }
                        var start = RoulettePresentationRules.StartDegrees(table, index, context);
                        var arc = RoulettePresentationRules.Probability(table, index, context) * 360;
                        foreach (var label in markers[index].GetComponentsInChildren<Text>())
                        {
                            Assert.That(Mathf.Abs(Mathf.DeltaAngle(label.transform.eulerAngles.z, 0)), Is.LessThan(.01f));
                            if (outside) continue;
                            var corners = new Vector3[4];
                            label.rectTransform.GetWorldCorners(corners);
                            foreach (var corner in corners)
                            {
                                var local = (Vector2)wheel.InverseTransformPoint(corner);
                                var angle = Mathf.Repeat(90 - Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg - (float)start, 360);
                                Assert.That(local.magnitude, Is.InRange(84f, 223f), label.text);
                                Assert.That(angle, Is.InRange(0f, (float)arc), label.text);
                            }
                        }
                    }
                    for (var a = 0; a < markers.Count; a++)
                    {
                        var content = (RectTransform)holder.parent;
                        var bounds = BoundsIn(markers[a], content);
                        Assert.That(content.rect.Contains(bounds.min), Is.True, "Label outside viewport");
                        Assert.That(content.rect.Contains(bounds.max), Is.True, "Label outside viewport");
                        for (var b = a + 1; b < markers.Count; b++)
                            Assert.That(bounds.Overlaps(BoundsIn(markers[b], content)), Is.False, "Reward labels overlap");
                        foreach (var name in new[] { "Title", "Improve Odds", "Raise Stakes", "Parts", "Rewards" })
                            Assert.That(bounds.Overlaps(BoundsIn((RectTransform)content.Find(name), content)), Is.False, "Label overlaps " + name);
                        Assert.That(bounds.Overlaps(BoundsIn((RectTransform)holder.Find("Spin"), content)), Is.False, "Label overlaps Spin");
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        private static Rect BoundsIn(RectTransform rect, RectTransform parent)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var corner in corners)
            {
                var local = (Vector2)parent.InverseTransformPoint(corner);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
