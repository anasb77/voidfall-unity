using System;

namespace VoidFall.Core
{
    public static class MilestoneRules
    {
        public static readonly int[] KillMilestones =
        {
            50, 100, 200, 500, 1_000,
            2_500, 5_000, 7_500, 10_000, 12_500, 15_000, 17_500, 20_000,
            22_500, 25_000, 27_500, 30_000, 32_500, 35_000, 37_500, 40_000,
            42_500, 45_000, 47_500, 50_000, 52_500, 55_000, 57_500, 60_000,
            62_500, 65_000, 67_500, 70_000, 72_500, 75_000, 77_500, 80_000,
            82_500, 85_000, 87_500, 90_000, 92_500, 95_000, 97_500, 100_000,
            250_000, 500_000, 1_000_000,
        };

        public static readonly int[] ScoreMilestones =
        {
            5_000, 10_000, 25_000, 50_000, 100_000, 250_000, 500_000,
            1_000_000, 2_000_000, 5_000_000,
        };

        public static MilestoneCrossing Crossed(int[] milestones, int startIndex, int current)
        {
            var index = Math.Max(0, startIndex);
            int? value = null;
            while (index < milestones.Length && current >= milestones[index])
            {
                value = milestones[index];
                index++;
            }

            return new MilestoneCrossing(index, value);
        }

        public static bool IsMajor(string kind, int value)
        {
            if (kind == "kills")
            {
                return value == 1_000 || value == 10_000 || value == 25_000 || value == 50_000 ||
                    value == 100_000 || value == 250_000 || value == 500_000 || value == 1_000_000;
            }

            return value == 10_000 || value == 50_000 || value == 100_000 || value == 500_000 ||
                value == 1_000_000 || value == 5_000_000;
        }
    }

    public readonly struct MilestoneCrossing
    {
        public MilestoneCrossing(int index, int? value)
        {
            Index = index;
            Value = value;
        }

        public int Index { get; }
        public int? Value { get; }
    }
}
