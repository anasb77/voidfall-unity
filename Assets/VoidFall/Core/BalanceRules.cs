using System;

namespace VoidFall.Core
{

public static class BalanceRules
{
    public static int XpNeededForLevel(int level)
    {
        var safeLevel = Math.Max(1, level);
        return (int)Math.Floor(7 + safeLevel * 4 + Math.Pow(safeLevel, 1.62) * 1.7);
    }

    public static int CumulativeXpToReachLevel(int level)
    {
        var target = Math.Max(1, level);
        var total = 0;
        for (var current = 1; current < target; current++)
        {
            total += XpNeededForLevel(current);
        }

        return total;
    }
}
}
