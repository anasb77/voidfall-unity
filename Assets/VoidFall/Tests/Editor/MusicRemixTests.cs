using NUnit.Framework;
using VoidFall.Runtime;



namespace VoidFall.Tests.Editor
{
    public sealed class MusicRemixTests
    {
        [Test]
        public void Critical_health_slows_the_already_overclocked_song()
        {
            var healthy = MusicStateComposer.Compose(new MusicReactiveState(2, 2, false, false, 0, true), .5f);
            var critical = MusicStateComposer.Compose(new MusicReactiveState(0, 0, true, false, 0, true), .5f);
            var combined = MusicStateComposer.Compose(new MusicReactiveState(2, 2, true, false, 0, true), .5f);
            Assert.That(combined.PlaybackRate, Is.LessThan(healthy.PlaybackRate));
            Assert.That(combined.PlaybackRate, Is.EqualTo(critical.PlaybackRate * 2f).Within(.001f));
            Assert.That(combined.LowPassHz, Is.LessThan(healthy.LowPassHz));
        }

        [Test]
        public void Magnet_darkens_and_concentrates_the_song_without_replacing_its_speed()
        {
            var normal = MusicStateComposer.Compose(new MusicReactiveState(2, 2, false, false, 0, true), 0);
            var magnet = MusicStateComposer.Compose(new MusicReactiveState(2, 2, false, false, 1, true), 0);
            Assert.That(magnet.LowPassHz, Is.LessThan(normal.LowPassHz));
            Assert.That(magnet.StereoWidth, Is.LessThan(normal.StereoWidth));
            Assert.That(magnet.PlaybackRate, Is.EqualTo(2));
        }

        [Test]
        public void Only_magnet_collections_accumulate_and_hundreds_remain_bounded()
        {
            var envelope = new MusicRemixEnvelope();
            envelope.CollectMagnetGem(1f);
            Assert.That(envelope.MagnetIntensity, Is.Zero);
            envelope.BeginMagnet();
            for (var i = 0; i < 20; i++) envelope.CollectMagnetGem(1f);
            var twenty = envelope.MagnetIntensity;
            Assert.That(twenty, Is.GreaterThan(.1f));
            for (var i = 0; i < 300; i++) envelope.CollectMagnetGem(1f);
            Assert.That(envelope.MagnetIntensity, Is.GreaterThan(twenty).And.LessThanOrEqualTo(1));
        }

        [Test]
        public void Last_magnet_wave_releases_then_tail_expires_and_pause_preserves_it()
        {
            var envelope = new MusicRemixEnvelope();
            envelope.BeginMagnet();
            for (var i = 0; i < 100; i++) envelope.CollectMagnetGem(1f);
            envelope.Step(.1f, false, true, 1);
            Assert.That(envelope.MagnetRelease, Is.Zero);
            envelope.Step(.1f, false, true, 0);
            Assert.That(envelope.MagnetRelease, Is.GreaterThan(.2f));
            var charged = envelope.MagnetIntensity;
            envelope.Step(0f, false, true, 0);
            Assert.That(envelope.MagnetIntensity, Is.EqualTo(charged));
            envelope.Step(20f, false, true, 0);
            Assert.That(envelope.MagnetIntensity, Is.GreaterThan(0).And.LessThan(charged));
            envelope.Step(5f, false, true, 0);
            Assert.That(envelope.MagnetIntensity, Is.Zero);
        }

        [Test]
        public void Recovery_requires_sustained_danger_and_reset_clears_all_layers()
        {
            var envelope = new MusicRemixEnvelope();
            envelope.Step(.1f, true, true, 0);
            envelope.Step(.1f, false, true, 0);
            Assert.That(envelope.Recovery, Is.Zero);
            envelope.Step(2.1f, true, true, 0);
            envelope.Step(.1f, false, true, 0);
            Assert.That(envelope.Recovery, Is.GreaterThan(.5f));
            envelope.NotifyOverclockStreak(1, 2);
            Assert.That(envelope.StackAccent, Is.GreaterThan(0));
            envelope.Reset();
            Assert.That(envelope.Recovery + envelope.StackAccent + envelope.MagnetIntensity, Is.Zero);
        }

        [Test]
        public void Release_and_recovery_change_the_mix_without_erasing_magnet_or_overclock()
        {
            var state = new MusicReactiveState(2, 2, false, false, .9f, true);
            var pulled = MusicStateComposer.Compose(state, 0);
            var released = MusicStateComposer.Compose(state, 0, magnetRelease: .9f, recovery: 1f, recoveryWave: 1f);
            Assert.That(released.StereoWidth, Is.GreaterThan(pulled.StereoWidth).And.LessThanOrEqualTo(1.2f));
            Assert.That(released.BassBoost, Is.EqualTo(.9f));
            Assert.That(released.PlaybackRate, Is.InRange(2f, 2.03f));
            Assert.That(released.LowPassHz, Is.GreaterThan(pulled.LowPassHz).And.LessThan(22000));
            var submerged = MusicStateComposer.Compose(new MusicReactiveState(2, 2, true, true, .9f, true), .5f,
                magnetRelease: 1f, recovery: 1f, stackAccent: 1f, recoveryWave: 1f);
            Assert.That(submerged.PlaybackRate, Is.EqualTo(1));
            Assert.That(submerged.LowPassHz, Is.EqualTo(390f).Within(.01f));
            Assert.That(submerged.BassBoost, Is.EqualTo(.9f));
        }

        [Test]
        public void Stack_accent_cannot_be_held_at_full_strength_by_repeated_pickups()
        {
            var envelope = new MusicRemixEnvelope();
            envelope.NotifyOverclockStreak(0, 1);
            Assert.That(envelope.StackAccent, Is.Zero);
            envelope.NotifyOverclockStreak(1, 2);
            envelope.Step(.2f, false, true, 0);
            var decayed = envelope.StackAccent;
            envelope.NotifyOverclockStreak(2, 3);
            Assert.That(envelope.StackAccent, Is.EqualTo(decayed));
            envelope.Step(.7f, false, true, 0);
            envelope.NotifyOverclockStreak(3, 4);
            Assert.That(envelope.StackAccent, Is.EqualTo(1));
            var mix = MusicStateComposer.Compose(new MusicReactiveState(3, 4, false, false, 0f, true), 0,
                stackAccent: envelope.StackAccent);
            Assert.That(mix.PlaybackRate, Is.EqualTo(2));
        }

        [Test]
        public void Empty_magnets_are_silent_and_retriggering_uses_only_the_remaining_charge()
        {
            var envelope = new MusicRemixEnvelope();
            envelope.BeginMagnet();
            envelope.Step(.1f, false, true, 0);
            Assert.That(envelope.MagnetRelease + envelope.MagnetIntensity, Is.Zero);
            envelope.BeginMagnet();
            for (var i = 0; i < 100; i++) envelope.CollectMagnetGem(1);
            envelope.Step(.1f, false, true, 0);
            envelope.Step(24f, false, true, 0);
            var remainder = envelope.MagnetIntensity;
            envelope.BeginMagnet();
            Assert.That(envelope.MagnetIntensity, Is.EqualTo(remainder).Within(.0001f));
            envelope.Step(.1f, false, false, 0);
            Assert.That(envelope.MagnetIntensity + envelope.MagnetRelease, Is.Zero);
        }

    }
}
