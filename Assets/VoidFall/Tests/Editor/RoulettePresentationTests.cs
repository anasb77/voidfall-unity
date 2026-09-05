using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using VoidFall.Core;
using VoidFall.UI;

namespace VoidFall.Tests.Editor
{
    public sealed class RoulettePresentationTests
    {
        private GameObject _host;
        private RouletteView _view;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("Roulette presentation test", typeof(RectTransform));
            ((RectTransform)_host.transform).sizeDelta = new Vector2(1600, 900);
            _view = _host.AddComponent<RouletteView>();
            _view.Initialize(null);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_host);

        [Test]
        public void Weighted_landing_places_the_sampled_segment_under_the_top_pointer()
        {
            var table = new[]
            {
                new RouletteWedgeDefinition(RoulettePrizeKind.Parts, RouletteTier.Standard, 1, "PARTS", "+60 Parts", "#8895ac"),
                new RouletteWedgeDefinition(RoulettePrizeKind.RareBoon, RouletteTier.Legendary, 3, "BOON", "Restore integrity", "#c7a4fa"),
            };
            var session = new RouletteSession(1, 0, table);
            _view.Present(session, new Rng(100), 180);
            typeof(RouletteView).GetMethod("OnSpinPressed", Flags).Invoke(_view, null);
            var target = (float)typeof(RouletteView).GetField("_targetRotation", Flags).GetValue(_view);
            // Clockwise segment centres: 45 and 225 degrees. Positive UI rotation brings them to top.
            var centre = session.ResultIndex == 0 ? 45f : 225f;
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(target, centre)), Is.LessThan(0.01f));
            Assert.That(target, Is.GreaterThanOrEqualTo(1440f));
        }

        [Test]
        public void Spin_advances_through_revolutions_instead_of_shortest_path_rotation()
        {
            var session = new RouletteSession(1, 0, RouletteRules.DefaultTable());
            _view.Present(session, new Rng(200), 180);
            typeof(RouletteView).GetMethod("OnSpinPressed", Flags).Invoke(_view, null);
            typeof(RouletteView).GetField("_spinElapsed", Flags).SetValue(_view, 1f);
            typeof(RouletteView).GetMethod("Update", Flags).Invoke(_view, null);
            var wheel = (RectTransform)typeof(RouletteView).GetField("_wheel", Flags).GetValue(_view);
            // At one second the new six-to-seven second spin is still gathering speed.
            // Its accumulated rotation must be stored without modulo loss for ticks and landing.
            var field = typeof(RouletteView).GetField("_currentRotation", Flags);
            Assert.That(field, Is.Not.Null, "The spin must retain its travelled angle, not just a wrapped transform.");
            Assert.That((float)field.GetValue(_view), Is.GreaterThan(180f));
            Assert.That(wheel, Is.Not.Null);
        }

        [Test]
        public void Landing_automatically_completes_once_without_an_extra_continue()
        {
            var session = new RouletteSession(3, 0, RouletteRules.DefaultTable());
            var calls = 0;
            RouletteSession completed = null;
            _view.CeremonyComplete += result => { calls++; completed = result; };
            _view.Present(session, new Rng(200), 180);
            typeof(RouletteView).GetMethod("OnSpinPressed", Flags).Invoke(_view, null);
            typeof(RouletteView).GetField("_spinElapsed", Flags).SetValue(_view, 10f);
            typeof(RouletteView).GetMethod("Update", Flags).Invoke(_view, null);
            Assert.That(calls, Is.Zero, "The landing needs a brief hold before the card appears.");
            typeof(RouletteView).GetField("_landingElapsed", Flags).SetValue(_view, 2f);
            typeof(RouletteView).GetMethod("Update", Flags).Invoke(_view, null);
            typeof(RouletteView).GetMethod("Update", Flags).Invoke(_view, null);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(completed, Is.SameAs(session));
            Assert.That(completed.Spun, Is.True);
        }

        [Test]
        public void Displayed_odds_include_the_single_protection_resample()
        {
            var table = new[]
            {
                new RouletteWedgeDefinition(RoulettePrizeKind.Parts, RouletteTier.Mediocre, 90, "", "", ""),
                new RouletteWedgeDefinition(RoulettePrizeKind.RareBoon, RouletteTier.Legendary, 10, "", "", ""),
            };
            var context = new RouletteSpinContext { ProtectionsEnabled = true, CeremoniesSeen = 0 };
            Assert.That(RoulettePresentationRules.Probability(table, 0, context), Is.EqualTo(0.81).Within(1e-9));
            Assert.That(RoulettePresentationRules.Probability(table, 1, context), Is.EqualTo(0.19).Within(1e-9));
        }

        [Test]
        public void Overlapping_first_and_repeat_protection_do_not_double_count_a_segment()
        {
            var table = new[]
            {
                new RouletteWedgeDefinition(RoulettePrizeKind.Parts, RouletteTier.Mediocre, 1, "", "", ""),
                new RouletteWedgeDefinition(RoulettePrizeKind.RareBoon, RouletteTier.Legendary, 1, "", "", ""),
            };
            var context = new RouletteSpinContext { ProtectionsEnabled = true, HasPrevious = true, PreviousKind = RoulettePrizeKind.Parts };
            Assert.That(RoulettePresentationRules.Probability(table, 0, context), Is.EqualTo(0.25).Within(1e-9));
            Assert.That(RoulettePresentationRules.Probability(table, 1, context), Is.EqualTo(0.75).Within(1e-9));
        }
    }
}
