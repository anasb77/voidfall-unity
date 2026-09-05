using System;
using UnityEngine;

namespace VoidFall.Runtime
{
    public readonly struct MusicAnalysisFrame
    {
        public MusicAnalysisFrame(float bass, float mids, float treble, float energy, float transient)
        {
            Bass = bass;
            Mids = mids;
            Treble = treble;
            Energy = energy;
            Transient = transient;
        }

        public float Bass { get; }
        public float Mids { get; }
        public float Treble { get; }
        public float Energy { get; }
        public float Transient { get; }
        public static MusicAnalysisFrame Zero => new MusicAnalysisFrame(0f, 0f, 0f, 0f, 0f);
    }

    public sealed class MusicSpectrumReducer
    {
        private readonly int _sampleRate;
        private readonly int _binCount;
        private MusicAnalysisFrame _smoothed;
        private float _normalizer = 0.0001f;
        private float _previousEnergy;
        private float _bassPeak = 0.0001f, _midPeak = 0.0001f, _treblePeak = 0.0001f;

        public MusicSpectrumReducer(int sampleRate, int binCount)
        {
            _sampleRate = Math.Max(8000, sampleRate);
            _binCount = Math.Max(64, binCount);
        }

        public MusicAnalysisFrame Reduce(float[] spectrum, float dt)
        {
            if (spectrum == null || spectrum.Length == 0) return MusicAnalysisFrame.Zero;
            var bass = 0f;
            var mids = 0f;
            var treble = 0f;
            var bassBins = 0;
            var midBins = 0;
            var trebleBins = 0;
            var count = Math.Min(spectrum.Length, _binCount);
            for (var index = 1; index < count; index++)
            {
                var hz = index * (_sampleRate * 0.5f) / _binCount;
                var value = float.IsNaN(spectrum[index]) || float.IsInfinity(spectrum[index]) ? 0 : Math.Max(0f, spectrum[index]);
                if (hz < 260f) { bass += value; bassBins++; }
                else if (hz < 2600f) { mids += value; midBins++; }
                else if (hz < 12000f) { treble += value; trebleBins++; }
            }

            bass = bassBins > 0 ? bass / bassBins : 0f;
            mids = midBins > 0 ? mids / midBins : 0f;
            treble = trebleBins > 0 ? treble / trebleBins : 0f;
            var rawEnergy = bass * 0.48f + mids * 0.34f + treble * 0.18f;
            _normalizer = Math.Max(rawEnergy, ExpApproach(_normalizer, 0.0001f, dt, 4f));
            var scale = 1f / Math.Max(0.0001f, _normalizer);
            _bassPeak = Math.Max(bass, ExpApproach(_bassPeak, 0.0001f, dt, 5f));
            _midPeak = Math.Max(mids, ExpApproach(_midPeak, 0.0001f, dt, 5f));
            _treblePeak = Math.Max(treble, ExpApproach(_treblePeak, 0.0001f, dt, 5f));
            // Independent band peaks keep a strong bass line from pinning itself at 1
            // merely because the global weighted mean is smaller than the bass band.
            var targetBass = Clamp01((float)Math.Pow(bass / Math.Max(0.0001f, _bassPeak), 1.4));
            var targetMids = Clamp01((float)Math.Pow(mids / Math.Max(0.0001f, _midPeak), 1.4));
            var targetTreble = Clamp01((float)Math.Pow(treble / Math.Max(0.0001f, _treblePeak), 1.4));
            var targetEnergy = Clamp01((float)Math.Sqrt(rawEnergy * scale));
            var transient = Clamp01((targetEnergy - _previousEnergy) * 3.8f);
            _previousEnergy = targetEnergy;
            _smoothed = new MusicAnalysisFrame(
                Smooth(_smoothed.Bass, targetBass, dt),
                Smooth(_smoothed.Mids, targetMids, dt),
                Smooth(_smoothed.Treble, targetTreble, dt),
                Smooth(_smoothed.Energy, targetEnergy, dt),
                Smooth(_smoothed.Transient, transient, dt));
            return _smoothed;
        }

        public void Reset()
        {
            _smoothed = MusicAnalysisFrame.Zero;
            _normalizer = 0.0001f;
            _previousEnergy = 0f;
            _bassPeak = _midPeak = _treblePeak = 0.0001f;
        }

        private static float Smooth(float current, float target, float dt)
        {
            var time = target > current ? 0.025f : 0.11f;
            return ExpApproach(current, target, dt, time);
        }

        private static float ExpApproach(float current, float target, float dt, float time)
        {
            return current + (target - current) * (1f - (float)Math.Exp(-Math.Max(0f, dt) / time));
        }

        private static float Clamp01(float value) => Math.Max(0f, Math.Min(1f, value));
    }

    public sealed class MusicSpectrumAnalyzer
    {
        private const int BinCount = 1024;
        private const float SampleInterval = 1f / 30f;
        private readonly float[] _spectrum = new float[BinCount];
        private readonly MusicSpectrumReducer _reducer;
        private AudioSource _source;
        private float _untilSample;

        public MusicSpectrumAnalyzer(AudioSource source)
        {
            _source = source;
            _reducer = new MusicSpectrumReducer(AudioSettings.outputSampleRate, BinCount);
        }

        public MusicAnalysisFrame Current { get; private set; }
        public float[] Bands { get; } = new float[24];

        public void SetSource(AudioSource source) => _source = source;

        public void Update(float unscaledDeltaTime)
        {
            _untilSample -= Math.Max(0f, unscaledDeltaTime);
            if (_untilSample > 0f) return;
            _untilSample += SampleInterval;
            if (_source == null || !_source.isPlaying)
            {
                Current = MusicAnalysisFrame.Zero;
                Array.Clear(Bands, 0, Bands.Length);
                return;
            }
            _source.GetSpectrumData(_spectrum, 0, FFTWindow.BlackmanHarris);
            Current = _reducer.Reduce(_spectrum, Math.Max(SampleInterval, unscaledDeltaTime));
            for (var index = 0; index < Bands.Length; index++)
            {
                var hz = 70f * Mathf.Pow(120f, index / 23f);
                var bin = Mathf.Clamp(Mathf.RoundToInt(hz / (AudioSettings.outputSampleRate * 0.5f) * BinCount), 1, BinCount - 2);
                var magnitude = (_spectrum[bin] + _spectrum[bin + 1]) * 0.5f;
                var decibels = 20f * Mathf.Log10(Mathf.Max(0.00001f, magnitude));
                var level = Mathf.Clamp01((decibels + 76f) / 65f);
                Bands[index] = Mathf.Lerp(Bands[index], level, level > Bands[index] ? 0.8f : 0.4f);
            }
        }

        public void Reset()
        {
            Current = MusicAnalysisFrame.Zero;
            _untilSample = 0f;
            _reducer.Reset();
            Array.Clear(_spectrum, 0, _spectrum.Length);
            Array.Clear(Bands, 0, Bands.Length);
        }
    }
}
