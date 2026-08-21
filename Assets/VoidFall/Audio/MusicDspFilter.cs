using System;
using System.Threading;
using UnityEngine;

namespace VoidFall.Runtime
{
    /// <summary>
    /// Allocation-free, music-only audio-thread processor. It keeps a short
    /// history of Unity's already decoded stream so damage can play a genuine
    /// reverse slip while the AudioSource timeline continues underneath.
    /// </summary>
    public sealed class MusicDspFilter : MonoBehaviour
    {
        // Half a second of stereo at 48 kHz. Unity sends interleaved samples.
        private readonly float[] _history = new float[48000];
        private int _write;
        private int _recorded;
        private int _requestedScratchMilliseconds;
        private int _resetRequested;
        private int _widthThousand = 1000;
        private int _scratchSamplesRemaining;
        private int _scratchSamplesTotal;
        private int _scratchRead;
        private int _sampleRate = 48000;

        private void Awake()
        {
            _sampleRate = Math.Max(8000, AudioSettings.outputSampleRate);
        }

        public void SetStereoWidth(float width)
        {
            Volatile.Write(ref _widthThousand, Mathf.RoundToInt(Mathf.Clamp(width, 0.2f, 1f) * 1000f));
        }

        public void RequestBackspin(float seconds)
        {
            var milliseconds = Mathf.RoundToInt(Mathf.Clamp(seconds, 0.04f, 0.4f) * 1000f);
            Interlocked.Exchange(ref _requestedScratchMilliseconds, milliseconds);
        }

        public void ResetHistory()
        {
            Interlocked.Exchange(ref _resetRequested, 1);
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (data == null || data.Length == 0 || channels <= 0) return;
            if (Interlocked.Exchange(ref _resetRequested, 0) != 0)
            {
                _write = 0;
                _recorded = 0;
                _scratchSamplesRemaining = 0;
            }

            var requestMs = Interlocked.Exchange(ref _requestedScratchMilliseconds, 0);
            if (requestMs > 0 && _recorded > channels * 128)
            {
                var requested = Math.Max(channels, _sampleRate * channels * requestMs / 1000);
                _scratchSamplesTotal = Math.Min(requested, _recorded - channels * 64);
                _scratchSamplesRemaining = _scratchSamplesTotal;
                _scratchRead = Wrap(_write - channels * 64);
            }

            var width = Volatile.Read(ref _widthThousand) * 0.001f;
            for (var frame = 0; frame < data.Length; frame += channels)
            {
                // Record the live stream first. Scratch playback never changes
                // AudioSource.time/timeSamples, so it returns to the exact live
                // position once the wet window closes.
                for (var channel = 0; channel < channels; channel++)
                {
                    _history[_write] = data[frame + channel];
                    _write = Wrap(_write + 1);
                    if (_recorded < _history.Length) _recorded++;
                }

                if (_scratchSamplesRemaining > 0)
                {
                    var elapsed = _scratchSamplesTotal - _scratchSamplesRemaining;
                    var edge = Math.Max(channels * 24, _scratchSamplesTotal / 8);
                    var wetIn = Math.Min(1f, elapsed / (float)edge);
                    var wetOut = Math.Min(1f, _scratchSamplesRemaining / (float)edge);
                    var wet = Math.Min(wetIn, wetOut);
                    for (var channel = 0; channel < channels; channel++)
                    {
                        var read = Wrap(_scratchRead + channel);
                        data[frame + channel] = data[frame + channel] * (1f - wet) + _history[read] * wet;
                    }
                    _scratchRead = Wrap(_scratchRead - channels);
                    _scratchSamplesRemaining = Math.Max(0, _scratchSamplesRemaining - channels);
                }

                if (channels >= 2 && width < 0.999f)
                {
                    var left = data[frame];
                    var right = data[frame + 1];
                    var mid = (left + right) * 0.5f;
                    var side = (left - right) * 0.5f * width;
                    data[frame] = mid + side;
                    data[frame + 1] = mid - side;
                }
            }
        }

        private int Wrap(int index)
        {
            while (index < 0) index += _history.Length;
            while (index >= _history.Length) index -= _history.Length;
            return index;
        }
    }
}
