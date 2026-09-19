using System;

namespace VoidFall.Core
{
    public enum DirectorProfileId
    {
        Standard = 0,
        Veteran = 1,
        Extreme = 2
    }

    public sealed class RunPressureState
    {
        private const double IntegerTolerance = 1e-9;

        private double[] _survivalHighWater;
        private double[] _bossHighWater;
        private int _ceilingHundredths;
        private int _startingHundredths;
        private int _visitCount;
        private double _survivalProgress;
        private double _bossProgress;

        public int PressureHundredths { get; private set; }
        public int ProgressionPressureHundredths => _visitCount <= 0 ? 0 : (int)Math.Floor(SnapNearInteger(
            _ceilingHundredths * (_survivalProgress * .8 + _bossProgress * .2) / _visitCount));
        public double CreditedProgressSeconds => SnapNearInteger(
            _survivalProgress * 300 + _bossProgress * 60);
        public bool IsFrozen { get; private set; }

        public void Reset(int ceilingHundredths, int visitCount, int startingHundredths = 0)
        {
            _ceilingHundredths = Math.Max(0, ceilingHundredths);
            _startingHundredths = Math.Min(_ceilingHundredths, Math.Max(0, startingHundredths));
            _visitCount = Math.Max(1, visitCount);
            _survivalHighWater = new double[_visitCount];
            _bossHighWater = new double[_visitCount];
            _survivalProgress = 0;
            _bossProgress = 0;
            PressureHundredths = _startingHundredths;
            IsFrozen = false;
        }

        public void ObserveStage(int stageIndex, double survivalFraction, double bossFraction)
        {
            if (IsFrozen || _survivalHighWater == null ||
                stageIndex < 0 || stageIndex >= _visitCount)
                return;

            var survival = IsFinite(survivalFraction)
                ? Clamp01(survivalFraction)
                : _survivalHighWater[stageIndex];
            var boss = IsFinite(bossFraction)
                ? Clamp01(bossFraction)
                : _bossHighWater[stageIndex];
            var survivalIncrease = Math.Max(0, survival - _survivalHighWater[stageIndex]);
            var bossIncrease = Math.Max(0, boss - _bossHighWater[stageIndex]);
            if (survivalIncrease <= 0 && bossIncrease <= 0)
                return;

            if (survivalIncrease > 0)
                _survivalHighWater[stageIndex] = survival;
            if (bossIncrease > 0)
                _bossHighWater[stageIndex] = boss;
            _survivalProgress += survivalIncrease;
            _bossProgress += bossIncrease;

            var weightedProgress = _survivalProgress * 0.8 + _bossProgress * 0.2;
            var rawPressure = _startingHundredths + (_ceilingHundredths - _startingHundredths) * weightedProgress / _visitCount;
            var nextPressure = (int)Math.Floor(SnapNearInteger(rawPressure));
            PressureHundredths = Math.Max(
                PressureHundredths,
                Math.Min(_ceilingHundredths, nextPressure));
        }

        public void Freeze()
        {
            IsFrozen = true;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static double Clamp01(double value)
        {
            return value < 0 ? 0 : value > 1 ? 1 : value;
        }

        private static double SnapNearInteger(double value)
        {
            var nearest = Math.Round(value);
            return Math.Abs(value - nearest) <= IntegerTolerance ? nearest : value;
        }
    }

    public readonly struct FrozenRunScore
    {
        public long BaseScore { get; }
        public int PressureHundredths { get; }
        public int MultiplierHundredths => Math.Max(100, PressureHundredths);
        public long FinalScore { get; }

        public FrozenRunScore(long baseScore, int pressureHundredths)
        {
            BaseScore = Math.Max(0, baseScore);
            PressureHundredths = Math.Max(0, pressureHundredths);
            FinalScore = RunScoreRules.FinalScore(BaseScore, PressureHundredths);
        }
    }

    public static class RunScoreRules
    {
        public const int Version = 1;

        public static long FinalScore(long baseScore, int pressureHundredths)
        {
            var safeBaseScore = Math.Max(0, baseScore);
            var multiplierHundredths = Math.Max(100, pressureHundredths);
            var wholeHundreds = safeBaseScore / 100;
            var remainder = safeBaseScore % 100;

            if (wholeHundreds > long.MaxValue / multiplierHundredths)
                return long.MaxValue;

            var scaledWhole = wholeHundreds * multiplierHundredths;
            var scaledRemainder = remainder * (long)multiplierHundredths;
            var roundedRemainder = (scaledRemainder + 50) / 100;
            if (scaledWhole > long.MaxValue - roundedRemainder)
                return long.MaxValue;

            return scaledWhole + roundedRemainder;
        }
    }
}
