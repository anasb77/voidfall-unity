using UnityEngine;

namespace VoidFall.Runtime
{
    /// <summary>Unity handoff for the allocation-free music audio processor.</summary>
    public sealed class MusicDspFilter : MonoBehaviour
    {
        private MusicSampleProcessor _processor;

        private void Awake()
        {
            _processor = new MusicSampleProcessor(AudioSettings.outputSampleRate);
        }

        public void SetStereoWidth(float width) => _processor?.SetStereoWidth(width);
        public void SetBassBoost(float intensity) => _processor?.SetBassBoost(intensity);
        public void RequestBackspin(float seconds) => _processor?.RequestBackspin(seconds);
        public void RequestBombEcho(float playbackRate) => _processor?.RequestBombEcho(playbackRate);
        public void ResetHistory() => _processor?.ResetHistory();

        private void OnAudioFilterRead(float[] data, int channels)
        {
            _processor?.Process(data, channels);
        }
    }
}
