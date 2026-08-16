using System;

namespace VoidFall.Core
{
    public static class HazardRules
    {
        public const double SweepSegmentStart = 36;
        public const double SweepSegmentLength = 68;
        public const double SweepSegmentGap = 34;

        public static bool SegmentedSweepContains(
            double along,
            double across,
            double length,
            double width,
            double targetRadius = 0)
        {
            if (along < SweepSegmentStart || along > length ||
                Math.Abs(across) > width / 2 + Math.Max(0, targetRadius)) return false;
            var offset = (along - SweepSegmentStart) % (SweepSegmentLength + SweepSegmentGap);
            return offset <= SweepSegmentLength;
        }
    }
}
