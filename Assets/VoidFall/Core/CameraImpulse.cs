using System;

namespace VoidFall.Core
{
    // Presentation-only impulses. Requests inside the group window strengthen
    // an existing impact, never extend its lifetime. No combat or FX RNG.
    public sealed class CameraImpulse
    {
        private struct Pulse
        {
            public double Started, Energy, X, Y;
        }
        private Pulse _ordinary, _major;
        private double _time;
        public const double OrdinaryDuration = .16, OrdinaryInterval = .21;
        public const double MajorDuration = .28, MajorInterval = .34;
        public double OrdinaryAmplitude => Amplitude(_ordinary, OrdinaryDuration, 4.5, 9);
        public double MajorAmplitude => Amplitude(_major, MajorDuration, 13, 2.5);
        public double AmplitudeNow => Math.Min(14, OrdinaryAmplitude + MajorAmplitude);

        public CameraImpulse() { Reset(); }
        public void Reset()
        {
            _time = 0;
            _ordinary = _major = new Pulse { Started = -10 };
        }
        public void Advance(double seconds) { _time += Math.Max(0, seconds); }
        public void Request(double strength, bool major, double x = 1, double y = 0)
        {
            if (strength <= 0) return;
            var length = Math.Sqrt(x * x + y * y);
            if (length < .001) { x = 1; y = 0; } else { x /= length; y /= length; }
            if (major) Request(ref _major, strength, MajorInterval, x, y);
            else Request(ref _ordinary, strength, OrdinaryInterval, x, y);
        }
        private void Request(ref Pulse pulse, double strength, double interval, double x, double y)
        {
            var age = _time - pulse.Started;
            if (age >= interval) pulse = new Pulse { Started = _time, Energy = strength, X = x, Y = y };
            else if (age <= .075) pulse.Energy = Math.Min(2, pulse.Energy + strength);
            // A bomb/boss may interrupt a smaller major accent, once. Equal
            // repeated blasts cannot continually restart the envelope.
            else if (interval == MajorInterval && strength >= .85 && pulse.Energy < .85)
                pulse = new Pulse { Started = _time, Energy = strength, X = x, Y = y };
        }
        private double Amplitude(Pulse pulse, double duration, double cap, double response)
        {
            var age = _time - pulse.Started;
            if (age < 0 || age >= duration || pulse.Energy <= 0) return 0;
            var decay = 1 - age / duration;
            return cap * (1 - Math.Exp(-pulse.Energy * response)) * decay * decay;
        }
        public void Offset(out double x, out double y)
        {
            // Continuous oscillation at an authored frequency, independent of
            // render rate. Initial kick biases X; the envelope settles to zero.
            Rotated(_ordinary, OrdinaryDuration, 4.5, 9, out x, out y);
            Rotated(_major, MajorDuration, 13, 2.5, out var mx, out var my);
            x += mx; y += my;
            var limit = Math.Max(1, Math.Sqrt(x * x + y * y) / 14);
            x /= limit; y /= limit;
        }
        private void Rotated(Pulse pulse, double duration, double cap, double response, out double x, out double y)
        {
            var a = Wave(pulse, duration, cap, response, false);
            var b = Wave(pulse, duration, cap, response, true);
            x = a * pulse.X - b * pulse.Y; y = a * pulse.Y + b * pulse.X;
        }
        private double Wave(Pulse pulse, double duration, double cap, double response, bool vertical)
        {
            var age = _time - pulse.Started;
            var amplitude = Amplitude(pulse, duration, cap, response);
            return amplitude * (vertical
                ? .55 * Math.Sin(age * 113) + .2 * Math.Sin(age * 173)
                : .75 * Math.Cos(age * 101) + .25 * Math.Cos(age * 157));
        }
    }
}
