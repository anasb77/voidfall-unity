using System;

namespace VoidFall.Core
{
    public readonly struct CombatVector
    {
        public CombatVector(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }
        public double Y { get; }
    }

    public static class CombatRules
    {
        public static CombatVector NormalizedDirection(double x, double y)
        {
            var length = Math.Sqrt(x * x + y * y);
            if (!IsFinite(length) || length < 0.0001) return new CombatVector(0, 0);
            return new CombatVector(x / length, y / length);
        }

        public static double[] ProjectileAngles(double baseAngle, int projectileCount, double spreadDegrees)
        {
            var count = Math.Max(1, projectileCount);
            if (count == 1) return new[] { baseAngle };
            var spread = Math.Max(0, spreadDegrees) * Math.PI / 180;
            var angles = new double[count];
            for (var index = 0; index < count; index++)
            {
                angles[index] = baseAngle + (index / (double)(count - 1) - 0.5) * spread;
            }

            return angles;
        }

        public static double WeaponRecoveryMultiplier(
            double globalCooldownMultiplier,
            int adrenalRank,
            double temporaryCooldownMultiplier = 1)
        {
            var global = IsFinite(globalCooldownMultiplier)
                ? Math.Max(0.05, globalCooldownMultiplier)
                : 1;
            var adrenal = Math.Max(0, adrenalRank);
            var temporary = IsFinite(temporaryCooldownMultiplier)
                ? Math.Max(0.05, temporaryCooldownMultiplier)
                : 1;
            return global * Math.Max(0.1, 1 - adrenal * 0.1) * temporary;
        }

        public static double HollowBladeReach(
            double weaponRange,
            double orbitRadius,
            double areaMultiplier)
        {
            var range = IsFinite(weaponRange) ? Math.Max(0, weaponRange) : 0;
            var orbit = IsFinite(orbitRadius) ? Math.Max(0, orbitRadius) : 0;
            var area = IsFinite(areaMultiplier) ? Math.Max(0.1, areaMultiplier) : 1;
            return Math.Max(360, Math.Max(range * 4.25, orbit + 180)) * area;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
