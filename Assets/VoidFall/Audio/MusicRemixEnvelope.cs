using System;

namespace VoidFall.Runtime
{
    /// <summary>Gameplay-time memory for musical events. No combat or audio-thread state.</summary>
    public sealed class MusicRemixEnvelope
    {
        public const float MagnetTailSeconds = 25f;
        private bool _collecting, _wasCritical;
        private float _charge, _tail, _release, _releaseStrength;
        private float _criticalSeconds, _recovery, _accent, _accentCooldown;

        public float MagnetIntensity => _charge * (_collecting ? 1f : _tail / MagnetTailSeconds);
        public float MagnetRelease => _releaseStrength * Math.Min(1f, _release / .9f);
        public float Recovery => _recovery / 5f;
        public float StackAccent => _accent / .45f;

        public void BeginMagnet()
        {
            // Another magnet builds on the audible remainder, never resurrecting
            // the full intensity of an almost-expired collection.
            _charge = MagnetIntensity;
            _collecting = true;
            _tail = MagnetTailSeconds;
            _release = 0;
        }

        public void CollectMagnetGem(float value)
        {
            if (!_collecting || value <= 0f || float.IsNaN(value)) return;
            _charge = Math.Min(1f, _charge + (1f - _charge) * .018f);
            _tail = MagnetTailSeconds;
        }

        public void NotifyOverclockStreak(int previous, int current)
        {
            if (previous < 1 || current <= previous || _accentCooldown > 0f) return;
            _accent = .45f;
            _accentCooldown = .8f;
        }

        public void Step(float dt, bool critical, bool gameplayActive, int pulledCount)
        {
            if (!gameplayActive) { Reset(); return; }
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            _release = Math.Max(0f, _release - dt);
            _recovery = Math.Max(0f, _recovery - dt);
            _accent = Math.Max(0f, _accent - dt);
            _accentCooldown = Math.Max(0f, _accentCooldown - dt);
            if (!_collecting)
            {
                _tail = Math.Max(0f, _tail - dt);
                if (_tail <= 0f) _charge = 0f;
            }
            else if (pulledCount <= 0)
            {
                _collecting = false;
                _releaseStrength = _charge;
                _release = _charge > 0f ? .9f : 0f;
                _tail = MagnetTailSeconds;
            }

            if (critical)
            {
                _criticalSeconds = Math.Min(10f, _criticalSeconds + dt);
                _recovery = 0f;
            }
            else
            {
                if (_wasCritical && _criticalSeconds >= 2f) _recovery = 5f;
                _criticalSeconds = 0f;
            }
            _wasCritical = critical;
        }

        public void Reset()
        {
            _collecting = _wasCritical = false;
            _charge = _tail = _release = _releaseStrength = 0f;
            _criticalSeconds = _recovery = _accent = _accentCooldown = 0f;
        }
    }
}
