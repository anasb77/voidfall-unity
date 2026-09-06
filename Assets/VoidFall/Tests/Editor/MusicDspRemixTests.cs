using System;
using NUnit.Framework;
using VoidFall.Runtime;

namespace VoidFall.Tests.Editor
{
    public sealed class MusicDspRemixTests
    {
        [TestCase("bass", 0.001f)]
        [TestCase("bass", 1f)]
        [TestCase("width", 1.001f)]
        [TestCase("width", 1.2f)]
        [TestCase("echo", 1f)]
        public void NearFullScale_EffectOnsetAndEndRemainContinuous(string effect, float amount)
        {
            var processor = new MusicSampleProcessor(48000);
            var right = effect == "width" ? -0.99f : 0.99f;
            var block = new float[48000 * 2];
            FillStereo(block, 0.99f, right);
            processor.Process(block, 2);
            Assert.That(block[block.Length - 2], Is.EqualTo(0.99f));
            if (effect == "bass") processor.SetBassBoost(amount);
            if (effect == "width") processor.SetStereoWidth(amount);
            if (effect == "echo") processor.RequestBombEcho(amount);
            FillStereo(block, 0.99f, right);
            processor.Process(block, 2);
            AssertContinuousAndBounded(block, 0.99f);
            var previous = block[block.Length - 2];
            processor.SetBassBoost(0f);
            processor.SetStereoWidth(1f);
            FillStereo(block, 0.99f, right);
            processor.Process(block, 2);
            AssertContinuousAndBounded(block, previous);
            Assert.That(block[block.Length - 2], Is.EqualTo(0.99f), "Effect expiry must restore exact bypass.");
        }

        [Test]
        public void BassAndEchoTogether_ReinforceTheDelayedLowTone()
        {
            var echoOnly = new MusicSampleProcessor(48000);
            var combined = new MusicSampleProcessor(48000);
            combined.SetBassBoost(1f);
            combined.Process(new float[48000], 1);
            echoOnly.RequestBombEcho(1f);
            combined.RequestBombEcho(1f);
            var data = new float[18000];
            for (var frame = 0; frame < 2400; frame++)
                data[frame] = 0.2f * (float)Math.Sin(2 * Math.PI * 60 * frame / 48000);
            var bassData = (float[])data.Clone();
            echoOnly.Process(data, 1);
            combined.Process(bassData, 1);
            double echoPower = 0, bassPower = 0;
            for (var frame = 6760; frame < 8160; frame++)
            {
                echoPower += data[frame] * data[frame];
                bassPower += bassData[frame] * bassData[frame];
            }
            Assert.That(Math.Sqrt(bassPower / echoPower), Is.GreaterThan(1.2));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(6)]
        public void Bypass_PreservesFiniteSamplesExactly(int channels)
        {
            var processor = new MusicSampleProcessor(48000);
            var data = Tone(48000, 900, 0.2f, channels);
            var original = (float[])data.Clone();
            processor.Process(data, channels);
            Assert.That(data, Is.EqualTo(original));
        }

        [TestCase(22050)]
        [TestCase(48000)]
        [TestCase(96000)]
        public void Bass_ReinforcesLowToneMoreThanHighToneWithHeadroom(int sampleRate)
        {
            var lowGain = ToneGain(sampleRate, 60);
            var highGain = ToneGain(sampleRate, 5000);
            Assert.That(lowGain, Is.GreaterThan(1.2));
            Assert.That(lowGain / highGain, Is.GreaterThan(1.5));
            Assert.That(highGain, Is.LessThan(1.05));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(6)]
        public void Bass_DoesNotLeakBetweenChannelsAndStaysBounded(int channels)
        {
            var processor = new MusicSampleProcessor(48000);
            processor.SetBassBoost(1f);
            var data = new float[48000 * channels];
            for (var frame = 0; frame < 48000; frame++) data[frame * channels] = 0.99f;
            processor.Process(data, channels);
            for (var frame = 0; frame < 48000; frame++)
            {
                Assert.That(Math.Abs(data[frame * channels]), Is.LessThanOrEqualTo(1f));
                for (var channel = 1; channel < channels; channel++)
                    Assert.That(data[frame * channels + channel], Is.Zero);
            }
        }

        [Test]
        public void StereoWidth_CanWidenAndLeavesSurroundChannelsUnchanged()
        {
            var processor = new MusicSampleProcessor(48000);
            processor.SetStereoWidth(1.2f);
            processor.Process(new float[48000 * 6], 6);
            var data = new[] { 0.2f, -0.2f, 0.3f, 0.4f, 0.5f, 0.6f };
            processor.Process(data, 6);
            Assert.That(data[0], Is.EqualTo(0.24f).Within(0.001f));
            Assert.That(data[1], Is.EqualTo(-0.24f).Within(0.001f));
            Assert.That(data[2], Is.EqualTo(0.3f));
            Assert.That(data[5], Is.EqualTo(0.6f));
        }

        [TestCase(0.2f)]
        [TestCase(1.2f)]
        public void Mono_WidthOnlyModulationPreservesExactBypass(float width)
        {
            var processor = new MusicSampleProcessor(48000);
            processor.SetStereoWidth(width);
            processor.Process(new float[48000], 1);
            var data = new[] { 0.99f, -0.99f, 2f, -2f, 5f, -5f };
            var original = (float[])data.Clone();
            processor.Process(data, 1);
            Assert.That(data, Is.EqualTo(original));
        }

        [TestCase(22050, 1, 1f, 2646)]
        [TestCase(48000, 2, 2f, 2880)]
        [TestCase(96000, 6, 0.5f, 23040)]
        public void Echo_UsesPlaybackScaledDelayAndNeverFeedsBack(int sampleRate, int channels, float rate, int delay)
        {
            var processor = new MusicSampleProcessor(sampleRate);
            processor.RequestBombEcho(rate);
            var data = new float[sampleRate * channels];
            data[0] = 0.5f;
            processor.Process(data, channels);
            Assert.That(data[delay * channels], Is.GreaterThan(0.07f));
            for (var frame = 1; frame < sampleRate; frame++)
            {
                if (frame != delay) Assert.That(data[frame * channels], Is.Zero);
                for (var channel = 1; channel < channels; channel++)
                    Assert.That(data[frame * channels + channel], Is.Zero);
            }
        }

        [Test]
        public void RepeatedEchoRequests_RetriggerOneBoundedVoice()
        {
            var single = new MusicSampleProcessor(48000);
            var repeated = new MusicSampleProcessor(48000);
            var input = Tone(48000, 90, 0.3f, 2);
            var once = (float[])input.Clone();
            var many = (float[])input.Clone();
            single.RequestBombEcho(1f);
            for (var i = 0; i < 1000; i++) repeated.RequestBombEcho(1f);
            single.Process(once, 2);
            repeated.Process(many, 2);
            Assert.That(many, Is.EqualTo(once));
        }

        [Test]
        public void Echo_RetriggerDuringPlaybackReplacesOldDelay()
        {
            var processor = new MusicSampleProcessor(48000);
            processor.RequestBombEcho(1f);
            processor.Process(new float[1000], 1);
            processor.RequestBombEcho(2f);
            var data = new float[12000];
            data[0] = 0.5f;
            processor.Process(data, 1);
            Assert.That(data[2880], Is.GreaterThan(0.07f));
            Assert.That(data[5760], Is.Zero, "The previous delay must not remain as a second echo voice.");
        }

        [TestCase(1f)]
        [TestCase(2f)]
        public void Echo_RetriggerPreservesWetLevelAndCrossfadesRateChanges(float nextRate)
        {
            var processor = new MusicSampleProcessor(48000);
            var history = new float[48000 * 2];
            FillStereo(history, 0.5f, 0.5f);
            processor.Process(history, 2);
            processor.RequestBombEcho(1f);
            var active = new float[3000 * 2];
            FillStereo(active, 0.2f, 0.2f);
            processor.Process(active, 2);
            Assert.That(active[active.Length - 2], Is.GreaterThan(0.3f));
            processor.RequestBombEcho(nextRate);
            var retrigger = new float[2000 * 2];
            FillStereo(retrigger, 0.2f, 0.2f);
            processor.Process(retrigger, 2);
            AssertContinuousAndBounded(retrigger, active[active.Length - 2]);
        }

        [Test]
        public void ChannelCountChange_InvalidatesOldBassAndEchoHistory()
        {
            var processor = new MusicSampleProcessor(48000);
            processor.SetBassBoost(1f);
            processor.RequestBombEcho(1f);
            processor.Process(Tone(48000, 60, 0.3f, 2), 2);
            var silence = new float[48000 * 6];
            processor.Process(silence, 6);
            Assert.That(silence, Is.All.Zero);
        }

        [Test]
        public void Reset_DropsPendingRequestsAndAllAudioHistory()
        {
            var processor = new MusicSampleProcessor(48000);
            processor.SetBassBoost(1f);
            processor.SetStereoWidth(0.2f);
            processor.Process(Tone(48000, 60, 0.3f, 2), 2);
            processor.RequestBackspin(0.2f);
            processor.RequestBombEcho(1f);
            processor.ResetHistory();
            var silence = new float[48000 * 2];
            processor.Process(silence, 2);
            Assert.That(silence, Is.All.Zero);
            var data = new[] { 0.2f, -0.2f };
            processor.Process(data, 2);
            Assert.That(data, Is.EqualTo(new[] { 0.2f, -0.2f }));
        }

        [Test]
        public void Backspin_ReversesFramesWithoutSwappingChannelsAndReturnsToLive()
        {
            var processor = new MusicSampleProcessor(8000);
            var history = new float[3000 * 6];
            for (var frame = 0; frame < 3000; frame++)
            {
                history[frame * 6] = frame * 0.0001f;
                history[frame * 6 + 1] = -frame * 0.0001f;
                history[frame * 6 + 5] = 0.05f;
            }
            processor.Process(history, 6);
            processor.RequestBackspin(0.1f);
            var scratch = new float[1600 * 6];
            processor.Process(scratch, 6);
            Assert.That(scratch[200 * 6], Is.GreaterThan(scratch[300 * 6]));
            Assert.That(scratch[200 * 6 + 1], Is.EqualTo(-scratch[200 * 6]));
            Assert.That(scratch[200 * 6 + 5], Is.EqualTo(0.05f));
            Assert.That(scratch[1000 * 6], Is.Zero);
        }

        [Test]
        public void LongBackspin_DoesNotReadHistoryOverwrittenByLiveSilence()
        {
            var processor = new MusicSampleProcessor(48000);
            var history = new float[48000 * 2];
            for (var frame = 0; frame < 48000; frame++)
            {
                history[frame * 2] = frame * 0.000005f;
                history[frame * 2 + 1] = -history[frame * 2];
            }
            processor.Process(history, 2);
            processor.RequestBackspin(0.4f);
            var data = new float[24000 * 2];
            processor.Process(data, 2);
            Assert.That(data[14400 * 2], Is.EqualTo(0.16768f).Within(0.00001f));
            Assert.That(data[14400 * 2 + 1], Is.EqualTo(-0.16768f).Within(0.00001f));
            Assert.That(data[22000 * 2], Is.Zero);
        }

        [Test]
        public void InvalidInput_CannotPoisonLaterAudio()
        {
            var processor = new MusicSampleProcessor(48000);
            processor.SetBassBoost(float.NaN);
            processor.SetStereoWidth(float.PositiveInfinity);
            processor.RequestBombEcho(float.NaN);
            var invalid = new[] { float.NaN, float.PositiveInfinity };
            processor.Process(invalid, 2);
            Assert.That(invalid, Is.All.Zero);
            var data = Tone(48000, 60, 0.2f, 2);
            processor.Process(data, 2);
            foreach (var sample in data)
                Assert.That(float.IsNaN(sample) || float.IsInfinity(sample), Is.False);
        }

        [Test]
        public void ExtremeFiniteSamples_CannotOverflowOrPoisonFilterState()
        {
            var processor = new MusicSampleProcessor(48000);
            processor.SetBassBoost(1f);
            processor.SetStereoWidth(0.2f);
            processor.Process(new float[48000], 2);
            var extreme = new[] { float.MaxValue, float.MaxValue, -float.MaxValue, -float.MaxValue };
            processor.Process(extreme, 2);
            foreach (var sample in extreme)
                Assert.That(float.IsNaN(sample) || float.IsInfinity(sample), Is.False);
            processor.Process(new float[48000], 2);
            var audible = new[] { 0.2f, 0.2f };
            processor.Process(audible, 2);
            Assert.That(audible[0], Is.GreaterThan(0.1f));
        }

        private static double ToneGain(int sampleRate, double frequency)
        {
            var processor = new MusicSampleProcessor(sampleRate);
            processor.SetBassBoost(1f);
            var data = Tone(sampleRate, frequency, 0.2f, 1);
            var original = (float[])data.Clone();
            processor.Process(data, 1);
            double inputPower = 0, outputPower = 0;
            for (var i = sampleRate / 2; i < sampleRate; i++)
            {
                inputPower += original[i] * original[i];
                outputPower += data[i] * data[i];
            }
            return Math.Sqrt(outputPower / inputPower);
        }

        private static void FillStereo(float[] data, float left, float right)
        {
            for (var i = 0; i < data.Length; i += 2)
            {
                data[i] = left;
                data[i + 1] = right;
            }
        }

        private static void AssertContinuousAndBounded(float[] data, float previous)
        {
            var largestStep = 0f;
            var largestSample = 0f;
            for (var i = 0; i < data.Length; i += 2)
            {
                largestStep = Math.Max(largestStep, Math.Abs(data[i] - previous));
                largestSample = Math.Max(largestSample, Math.Max(Math.Abs(data[i]), Math.Abs(data[i + 1])));
                previous = data[i];
            }
            Assert.That(largestStep, Is.LessThan(0.003f), "Near-full-scale transitions must not toggle the limiter abruptly.");
            Assert.That(largestSample, Is.LessThanOrEqualTo(1f));
        }

        private static float[] Tone(int sampleRate, double frequency, float amplitude, int channels)
        {
            var data = new float[sampleRate * channels];
            for (var frame = 0; frame < sampleRate; frame++)
                for (var channel = 0; channel < channels; channel++)
                    data[frame * channels + channel] = amplitude * (float)Math.Sin(2 * Math.PI * frequency * frame / sampleRate + channel);
            return data;
        }
    }
}
