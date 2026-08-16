using System;
using System.Collections.Generic;

namespace VoidFall.Core
{
    public static class PickupRules
    {
        public const double OverdriveDurationSeconds = 15;
        public const double OverdriveSpeedMultiplier = 2;
        public const double OverdriveFireRateMultiplier = 1.35;
        public const int MaxActiveHarvesters = 3;

        public static HarvesterXpLimits HarvesterXpLimits(double nextLevelXp)
        {
            var need = !double.IsNaN(nextLevelXp) && !double.IsInfinity(nextLevelXp)
                ? Math.Max(1, Math.Floor(nextLevelXp))
                : 1;
            return new HarvesterXpLimits(
                Math.Max(30, Math.Min(60, SourceRound(need * 0.12))),
                Math.Max(60, Math.Min(180, Math.Ceiling(need * 0.3))));
        }

        private static double SourceRound(double value)
        {
            // Browser authority uses Math.round for this non-negative limit.
            return Math.Floor(Math.Max(0, value) + 0.5);
        }

        public static double HarvesterAbsorptionAmount(
            double pickupXp,
            double storedByHarvester,
            double storedGlobally,
            double nextLevelXp)
        {
            var limits = HarvesterXpLimits(nextLevelXp);
            return Math.Max(
                0,
                Math.Min(
                    Math.Max(0, Math.Floor(pickupXp)),
                    Math.Min(
                        limits.Individual - Math.Max(0, Math.Floor(storedByHarvester)),
                        limits.Global - Math.Max(0, Math.Floor(storedGlobally))
                    )));
        }

        public static double OverdriveCooldownMultiplier(bool active)
        {
            return active ? 1 / OverdriveFireRateMultiplier : 1;
        }

        public static int[] XpDropValues(int total, Func<double> random, int maxDrops = 20)
        {
            var remaining = Math.Max(0, total);
            var limit = Math.Max(1, maxDrops);
            var drops = new List<int>();

            while (remaining > 0 && drops.Count < limit)
            {
                var slotsLeft = limit - drops.Count;
                var minimumToFinish = (int)Math.Ceiling((double)remaining / slotsLeft);
                var roll = random();
                var preferred = remaining >= 10 && roll < 0.28
                    ? 10
                    : remaining >= 4 && roll < 0.52
                        ? 4
                        : 1;
                var value = Math.Min(remaining, Math.Max(preferred, minimumToFinish));
                drops.Add(value);
                remaining -= value;
            }

            return drops.ToArray();
        }
    }

    public readonly struct HarvesterXpLimits
    {
        public HarvesterXpLimits(double individual, double global)
        {
            Individual = individual;
            Global = global;
        }

        public double Individual { get; }
        public double Global { get; }
    }
}
