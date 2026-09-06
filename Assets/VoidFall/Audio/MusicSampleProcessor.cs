using System;
using System.Threading;

namespace VoidFall.Runtime
{
    /// <summary>
    /// Single audio-thread owner of preallocated sample history. Control methods
    /// publish atomic targets/requests; Process never allocates or locks.
    /// </summary>
    public sealed class MusicSampleProcessor
    {
        private const int MaxChannels = 8;
        private readonly int _sampleRate;
        private readonly int _historyFrames;
        private readonly float[] _history;
        private readonly float[] _bassHistory = new float[MaxChannels];
        private readonly float _lowPassAlpha;
        private readonly float _smoothingAlpha;
        private int _widthThousand = 1000;
        private int _bassThousand;
        private int _requestedScratchMilliseconds;
        private int _requestedEchoRateThousand;
        private int _resetRequested;
        private int _channels;
        private int _writeFrame;
        private int _recordedFrames;
        private int _scratchRemaining;
        private int _scratchTotal;
        private int _scratchReadFrame;
        private int _echoRemaining;
        private int _echoTotal;
        private int _echoDelay;
        private int _echoDesiredDelay;
        private int _echoNextDelay;
        private int _echoTransitionRemaining;
        private float _echoWet;
        private float _width = 1f;
        private float _bass;

        public MusicSampleProcessor(int sampleRate)
        {
            _sampleRate = Math.Max(8000, Math.Min(192000, sampleRate));
            // More than twice the longest reverse window prevents the live write
            // head from overtaking samples still needed by reverse playback.
            _historyFrames = _sampleRate * 9 / 10;
            _history = new float[_historyFrames * MaxChannels];
            _lowPassAlpha = (float)(1.0 - Math.Exp(-2.0 * Math.PI * 180.0 / _sampleRate));
            _smoothingAlpha = (float)(1.0 - Math.Exp(-1.0 / (0.012 * _sampleRate)));
        }

        public void SetBassBoost(float intensity)
        {
            Volatile.Write(ref _bassThousand, Quantize(intensity, 0f, 1f, 0f));
        }

        public void SetStereoWidth(float width)
        {
            Volatile.Write(ref _widthThousand, Quantize(width, 0.2f, 1.2f, 1f));
        }

        public void RequestBombEcho(float playbackRate)
        {
            Interlocked.Exchange(ref _requestedEchoRateThousand, Quantize(playbackRate, 0.25f, 4f, 1f));
        }

        public void RequestBackspin(float seconds)
        {
            Interlocked.Exchange(ref _requestedScratchMilliseconds, Quantize(seconds, 0.04f, 0.4f, 0.04f));
        }

        public void ResetHistory()
        {
            Volatile.Write(ref _widthThousand, 1000);
            Volatile.Write(ref _bassThousand, 0);
            Interlocked.Exchange(ref _requestedScratchMilliseconds, 0);
            Interlocked.Exchange(ref _requestedEchoRateThousand, 0);
            Interlocked.Exchange(ref _resetRequested, 1);
        }

        public void Process(float[] data, int channels)
        {
            if (data == null || data.Length == 0 || channels <= 0) return;
            if (Interlocked.Exchange(ref _resetRequested, 0) != 0 || _channels != channels)
            {
                _writeFrame = 0;
                _recordedFrames = 0;
                _scratchRemaining = 0;
                _echoRemaining = 0;
                _echoWet = 0f;
                _echoTransitionRemaining = 0;
                _echoDelay = 0;
                _echoDesiredDelay = 0;
                _width = 1f;
                _bass = 0f;
                _channels = channels;
                Array.Clear(_bassHistory, 0, _bassHistory.Length);
                // Validity counters invalidate the ring without a large callback clear.
            }
            if (channels > MaxChannels || data.Length % channels != 0)
            {
                for (var i = 0; i < data.Length; i++) data[i] = Finite(data[i]);
                return;
            }

            var requestMs = Interlocked.Exchange(ref _requestedScratchMilliseconds, 0);
            if (requestMs > 0 && _recordedFrames > 128)
            {
                _scratchTotal = Math.Min(_sampleRate * requestMs / 1000, _recordedFrames - 64);
                _scratchRemaining = _scratchTotal;
                _scratchReadFrame = Wrap(_writeFrame - 64);
            }
            var echoRate = Interlocked.Exchange(ref _requestedEchoRateThousand, 0);
            if (echoRate > 0)
            {
                _echoDesiredDelay = Math.Max(1, (int)Math.Round(_sampleRate * 120.0 / echoRate));
                if (_echoWet == 0f) _echoDelay = _echoDesiredDelay;
                _echoTotal = Math.Max(_echoDesiredDelay + 1, (int)(_sampleRate * Math.Min(0.9, 450.0 / echoRate)));
                _echoRemaining = _echoTotal;
            }

            var targetWidth = Volatile.Read(ref _widthThousand) * 0.001f;
            var targetBass = Volatile.Read(ref _bassThousand) * 0.001f;
            var echoEdge = Math.Max(1, _sampleRate / 200);
            for (var frame = 0; frame < data.Length; frame += channels)
            {
                _width = Smooth(_width, targetWidth);
                _bass = Smooth(_bass, targetBass);
                var scratchWet = 0f;
                if (_scratchRemaining > 0)
                {
                    var edge = Math.Max(24, _scratchTotal / 8);
                    scratchWet = Math.Min(1f, Math.Min((_scratchTotal - _scratchRemaining) / (float)edge, _scratchRemaining / (float)edge));
                }
                // Retriggers refresh duration while retaining the current wet
                // gain. A changed rate crossfades two dry taps over five ms.
                // Repeated changes during that fade retain its endpoints and
                // queue the latest target, so no discontinuous tap is introduced.
                if (_echoTransitionRemaining == 0 && _echoDelay != _echoDesiredDelay)
                {
                    _echoNextDelay = _echoDesiredDelay;
                    _echoTransitionRemaining = echoEdge;
                }
                var echoBlend = _echoTransitionRemaining > 0 ? 1f - _echoTransitionRemaining / (float)echoEdge : 0f;
                var echoRead = Wrap(_writeFrame - _echoDelay) * MaxChannels;
                var echoNextRead = Wrap(_writeFrame - _echoNextDelay) * MaxChannels;
                var release = _echoRemaining / (float)Math.Max(1, _echoTotal / 3);
                var targetEchoWet = _echoRemaining > 0 ? 0.24f * Math.Min(1f, release) : 0f;
                var echoStep = 0.24f / echoEdge;
                _echoWet += Math.Max(-echoStep, Math.Min(echoStep, targetEchoWet - _echoWet));
                var echoWet = _echoWet;
                var write = _writeFrame * MaxChannels;
                var scratchRead = _scratchReadFrame * MaxChannels;
                var bassHeadroom = 1f / (1f + 0.3f * _bass);
                for (var channel = 0; channel < channels; channel++)
                {
                    var dry = Finite(data[frame + channel]);
                    var bounded = Math.Max(-4f, Math.Min(4f, dry));
                    // Store only dry live music; echo cannot enter its own history.
                    // Internal headroom also prevents corrupt finite input from
                    // overflowing recursive filter state. Exact bypass is retained.
                    _history[write + channel] = bounded;
                    var sample = _bass > 0f || scratchWet > 0f || echoWet > 0f || (channels >= 2 && _width != 1f) ? bounded : dry;
                    if (scratchWet > 0f) sample += (_history[scratchRead + channel] - sample) * scratchWet;
                    if (echoWet > 0f)
                    {
                        var delayed = _recordedFrames >= _echoDelay ? _history[echoRead + channel] : 0f;
                        if (_echoTransitionRemaining > 0)
                        {
                            var next = _recordedFrames >= _echoNextDelay ? _history[echoNextRead + channel] : 0f;
                            delayed += (next - delayed) * echoBlend;
                        }
                        sample += delayed * echoWet;
                    }
                    // Echo shares the current bass treatment; only the original
                    // dry stream was recorded, preserving feed-forward ownership.
                    _bassHistory[channel] += _lowPassAlpha * (Math.Max(-4f, Math.Min(4f, sample)) - _bassHistory[channel]);
                    if (_bass > 0f) sample = (sample + 0.8f * _bass * _bassHistory[channel]) * bassHeadroom;
                    data[frame + channel] = sample;
                }
                if (channels >= 2 && _width != 1f)
                {
                    var mid = (data[frame] + data[frame + 1]) * 0.5f;
                    var side = (data[frame] - data[frame + 1]) * (0.5f * _width);
                    data[frame] = mid + side;
                    data[frame + 1] = mid - side;
                }
                var wideningStrength = channels >= 2 ? (_width - 1f) * 5f : 0f;
                var ceilingStrength = Math.Min(1f, Math.Max(_bass, Math.Max(echoWet / 0.24f, wideningStrength)));
                if (ceilingStrength > 0f)
                    for (var channel = 0; channel < channels; channel++)
                        data[frame + channel] = SoftCeiling(data[frame + channel], ceilingStrength);
                _writeFrame = Wrap(_writeFrame + 1);
                if (_recordedFrames < _historyFrames) _recordedFrames++;
                if (_scratchRemaining > 0)
                {
                    _scratchReadFrame = Wrap(_scratchReadFrame - 1);
                    _scratchRemaining--;
                }
                if (_echoRemaining > 0) _echoRemaining--;
                if (_echoTransitionRemaining > 0 && --_echoTransitionRemaining == 0)
                    _echoDelay = _echoNextDelay;
            }
        }

        private float Smooth(float current, float target)
        {
            var next = current + (target - current) * _smoothingAlpha;
            // Float rounding can stall an asymptotic ramp several ulps from 1;
            // snap that inaudible remainder so width can regain exact bypass.
            return next == current || Math.Abs(next - target) < 0.00001f ? target : next;
        }

        private int Wrap(int frame)
        {
            if (frame < 0) return frame + _historyFrames;
            if (frame >= _historyFrames) return frame - _historyFrames;
            return frame;
        }

        private static int Quantize(float value, float minimum, float maximum, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) value = fallback;
            return (int)Math.Round(Math.Max(minimum, Math.Min(maximum, value)) * 1000f);
        }

        private static float Finite(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }

        private static float SoftCeiling(float value, float strength)
        {
            value = Finite(value);
            var magnitude = Math.Abs(value);
            // The knee approaches full scale continuously as the effect fades.
            // This retains the peak bound without toggling fixed compression on
            // near-full-scale dry music at the first/last nonzero effect sample.
            var headroom = 0.15f * strength;
            var knee = 1f - headroom;
            if (magnitude <= knee) return value;
            var excess = magnitude - knee;
            var limited = knee + headroom * (excess / (headroom + excess));
            return value < 0f ? -limited : limited;
        }
    }
}
