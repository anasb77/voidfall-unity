using System;

namespace VoidFall.Core
{
    public enum HydraDamageRegion
    {
        Crown = 0,
        UpperRight = 1,
        UpperLeft = 2,
        LowerRight = 3,
        LowerLeft = 4,
        Eye = 5,
    }

    /// <summary>
    /// Engine-free timing and ordering rules for Hydra Prime. Runtime samples
    /// the player's live position for every Marrow drop; this class owns only
    /// the bounded cadence and socket shuffle so combat RNG remains explicit.
    /// </summary>
    public static class HydraEncounterRules
    {
        public const int EvasionSocketCount = 6;
        public const int MarrowBombCount = 4;
        public const double MinMarrowIntervalSeconds = 0.42;
        public const double MaxMarrowIntervalSeconds = 0.66;
        public const double RibProjectileBaselineRadius = 18.0;
        public const double RibProjectileVisualScale = 0.8;
        public const double RibProjectileRadius =
            RibProjectileBaselineRadius * RibProjectileVisualScale;

        private static readonly double[] MarrowIntervals =
        {
            0.42,
            0.50,
            0.58,
            0.66,
        };

        public static int[] BuildEvasionOrder(Rng rng, int previousSocket = -1)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            var order = new int[EvasionSocketCount];
            for (var index = 0; index < order.Length; index++) order[index] = index;
            Shuffle(order, rng);
            if (order.Length > 1 && order[0] == previousSocket)
                (order[0], order[1]) = (order[1], order[0]);
            return order;
        }

        public static double[] BuildMarrowIntervals(Rng rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            var result = (double[])MarrowIntervals.Clone();
            Shuffle(result, rng);
            return result;
        }

        public static HydraDamageRegion DamageRegion(double x01, double y01)
        {
            var x = Clamp01(x01);
            var y = Clamp01(y01);
            var eyeX = (x - 0.5) / 0.13;
            var eyeY = (y - 0.52) / 0.17;
            if (eyeX * eyeX + eyeY * eyeY <= 1.0) return HydraDamageRegion.Eye;
            if (y >= 0.84) return HydraDamageRegion.Crown;
            if (y >= 0.48)
                return x >= 0.5 ? HydraDamageRegion.UpperRight : HydraDamageRegion.UpperLeft;
            return x >= 0.5 ? HydraDamageRegion.LowerRight : HydraDamageRegion.LowerLeft;
        }

        private static void Shuffle<T>(T[] values, Rng rng)
        {
            for (var index = values.Length - 1; index > 0; index--)
            {
                var swap = rng.Int(index + 1);
                (values[index], values[swap]) = (values[swap], values[index]);
            }
        }

        private static double Clamp01(double value) =>
            double.IsNaN(value) ? 0 : Math.Max(0, Math.Min(1, value));
    }
}
