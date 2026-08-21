using NUnit.Framework;
using VoidFall.Core;
using VoidFall.Runtime;
using VoidFall.UI;
using UnityEngine;
using System.Reflection;

namespace VoidFall.Tests.Editor
{
    public sealed class MusicReactiveFeatureTests
    {
        [Test]
        public void Overclock_PickupsAccumulateTimeAndCapOnlyPowerTier()
        {
            var state = default(OverclockState);

            for (var pickup = 1; pickup <= 7; pickup++)
            {
                state.ApplyPickup();
                Assert.That(state.PowerTier, Is.EqualTo(System.Math.Min(3, pickup)));
                Assert.That(state.Streak, Is.EqualTo(pickup));
                Assert.That(state.RemainingSeconds, Is.EqualTo(15f * pickup).Within(0.001f));
            }
        }

        [Test]
        public void Overclock_ExpiryClearsTierAndStreak()
        {
            var state = default(OverclockState);
            state.ApplyPickup();
            state.ApplyPickup();

            state.Step(29.5f);
            Assert.That(state.Active, Is.True);
            state.Step(0.5f);

            Assert.That(state.Active, Is.False);
            Assert.That(state.PowerTier, Is.Zero);
            Assert.That(state.Streak, Is.Zero);
            Assert.That(state.RemainingSeconds, Is.Zero);
        }

        [TestCase(0, 1.00, 1.00, 1.00)]
        [TestCase(1, 2.00, 1.35, 1.40)]
        [TestCase(2, 2.30, 1.70, 1.48)]
        [TestCase(3, 2.60, 2.15, 1.56)]
        [TestCase(8, 2.60, 2.15, 1.56)]
        public void Overclock_MultipliersAreTableDriven(
            int tier,
            double expectedMovement,
            double expectedFireRate,
            double expectedMusicRate)
        {
            Assert.That(OverclockRules.MovementMultiplier(tier), Is.EqualTo(expectedMovement).Within(0.0001));
            Assert.That(OverclockRules.FireRateMultiplier(tier), Is.EqualTo(expectedFireRate).Within(0.0001));
            Assert.That(OverclockRules.MusicRate(tier), Is.EqualTo(expectedMusicRate).Within(0.0001));
            Assert.That(
                OverclockRules.CooldownMultiplier(tier),
                Is.EqualTo(1.0 / expectedFireRate).Within(0.0001));
        }

        [TestCase(179.9f, 0f)]
        [TestCase(180f, 0.01f)]
        [TestCase(240f, 0.02f)]
        [TestCase(720f, 0.10f)]
        [TestCase(1800f, 0.10f)]
        public void Perimeter_AmbientIntensityFadesInByRunMinute(float seconds, float expected)
        {
            Assert.That(MusicPerimeterRules.AmbientIntensity(seconds), Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void Perimeter_SeededMappingIsStableAndARealPermutation()
        {
            var first = MusicPerimeterRules.CreateRunLayout(unchecked((int)0x5f1dc0de));
            var second = MusicPerimeterRules.CreateRunLayout(unchecked((int)0x5f1dc0de));

            Assert.That(second.LayoutIndex, Is.EqualTo(first.LayoutIndex));
            Assert.That(second.LongBand, Is.EqualTo(first.LongBand));
            Assert.That(second.CornerBand, Is.EqualTo(first.CornerBand));
            Assert.That(second.FragmentBand, Is.EqualTo(first.FragmentBand));
            Assert.That(first.LayoutIndex, Is.InRange(0, MusicPerimeterRules.LayoutCount - 1));
            Assert.That(first.LongBand, Is.InRange(0, 2));
            Assert.That(first.CornerBand, Is.InRange(0, 2));
            Assert.That(first.FragmentBand, Is.InRange(0, 2));
            Assert.That(first.LongBand, Is.Not.EqualTo(first.CornerBand));
            Assert.That(first.LongBand, Is.Not.EqualTo(first.FragmentBand));
            Assert.That(first.CornerBand, Is.Not.EqualTo(first.FragmentBand));
        }

        [Test]
        public void Audio_ComposesOverclockCriticalUpgradeAndMagnetWithoutPriorityLoss()
        {
            var state = new MusicReactiveState(
                overclockTier: 3,
                overclockStreak: 5,
                criticalHealth: true,
                levelUpOpen: false,
                magnetIntensity: 0.75f,
                gameplayActive: true);

            var mix = MusicStateComposer.Compose(state, criticalPulse: 0.5f);

            Assert.That(mix.PlaybackRate, Is.GreaterThan(1.3f));
            Assert.That(mix.CriticalWarp, Is.GreaterThan(0f));
            Assert.That(mix.StereoWidth, Is.LessThan(1f));
            Assert.That(mix.LowPassHz, Is.LessThan(22000f));

            var levelUp = MusicStateComposer.Compose(
                new MusicReactiveState(3, 5, true, true, 0.75f, true),
                criticalPulse: 0.5f);
            Assert.That(levelUp.Submersion, Is.EqualTo(1f));
            Assert.That(levelUp.VisualDamping, Is.LessThan(0.5f));
        }

        [Test]
        public void SpectrumReducer_SeparatesBandsAndSmoothsWithoutOvershoot()
        {
            var reducer = new MusicSpectrumReducer(48000, 512);
            var spectrum = new float[512];
            spectrum[2] = 1f;
            var bass = reducer.Reduce(spectrum, 1f / 30f);
            Assert.That(bass.Bass, Is.GreaterThan(bass.Mids));
            Assert.That(bass.Bass, Is.GreaterThan(bass.Treble));

            System.Array.Clear(spectrum, 0, spectrum.Length);
            spectrum[85] = 1f;
            var treble = reducer.Reduce(spectrum, 1f / 30f);
            Assert.That(treble.Treble, Is.GreaterThan(0f));
            Assert.That(treble.Bass, Is.InRange(0f, 1f));
            Assert.That(treble.Energy, Is.InRange(0f, 1f));
            Assert.That(treble.Transient, Is.InRange(0f, 1f));
        }

        [TestCase(0, 0f)]
        [TestCase(8, 0.18f)]
        [TestCase(100, 0.67f)]
        [TestCase(280, 1f)]
        public void MagnetIntensity_IsBoundedAndScalesSublinearly(int pulledShards, float minimumExpected)
        {
            var intensity = MusicReactiveMath.MagnetTarget(pulledShards, pulledShards);
            Assert.That(intensity, Is.InRange(0f, 1f));
            Assert.That(intensity, Is.GreaterThanOrEqualTo(minimumExpected));
        }

        [TestCase(0.02f, false, 0.05f, 0.08f)]
        [TestCase(0.10f, false, 0.08f, 0.13f)]
        [TestCase(0.30f, false, 0.13f, 0.20f)]
        [TestCase(0.01f, true, 0.30f, 0.40f)]
        public void DamageScratch_UsesBoundedDamageBands(
            float healthFraction,
            bool lethal,
            float minimum,
            float maximum)
        {
            Assert.That(
                MusicReactiveMath.DamageScratchSeconds(healthFraction, lethal),
                Is.InRange(minimum, maximum));
        }

        [Test]
        public void Perimeter_UsesOneBoundedGraphicAcrossQualityLevels()
        {
            var host = new GameObject("Perimeter Test", typeof(RectTransform), typeof(CanvasRenderer));
            try
            {
                var graphic = host.AddComponent<MusicPerimeterGraphic>();
                graphic.Configure(12345, 0, false);
                Assert.That(graphic.SegmentCount, Is.EqualTo(24));
                Assert.That(graphic.MaximumVertexCount, Is.EqualTo(192));
                graphic.Configure(12345, 2, false);
                Assert.That(graphic.SegmentCount, Is.EqualTo(48));
                Assert.That(graphic.MaximumVertexCount, Is.EqualTo(384));
                Assert.That(host.GetComponentsInChildren<MusicPerimeterGraphic>(true), Has.Length.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Runtime_OwnsTieredOverclockAndWorldSpaceArenaVignette()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var runtime = typeof(VoidFallGameRuntime);
            Assert.That(runtime.GetField("_overclock", flags)?.FieldType, Is.EqualTo(typeof(OverclockState)));
            Assert.That(runtime.GetField("_overdriveTimer", flags), Is.Null);
            Assert.That(runtime.GetField("_arenaVignetteView", flags)?.FieldType, Is.EqualTo(typeof(SpriteRenderer)));
            Assert.That(runtime.GetField("_arenaVignetteOverlay", flags), Is.Null);
        }
    }
}
