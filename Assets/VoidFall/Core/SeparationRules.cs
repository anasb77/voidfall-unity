namespace VoidFall.Core
{
    /// <summary>
    /// Numeric constants used by the browser engine's bounded enemy separation
    /// pass. Keeping the formula pure makes the runtime port auditable and
    /// prevents visual/collision code from inventing a second rule set.
    /// </summary>
    public static class SeparationRules
    {
        public const float Skin = 3f;
        public const float PushCoefficient = 0.42f;

        /// <summary>
        /// Relaxation passes run per tick. The browser runs exactly one, which
        /// cannot resolve a dense clump: a single push per pair leaves interior
        /// bodies still overlapping, so sustained pressure compacts into a shell
        /// instead of spreading. Iterating the same bounded pair rule converges
        /// the pile without changing the rule itself. Deliberate divergence from
        /// the browser engine, not a parity bug.
        /// </summary>
        public const int Passes = 3;

        public static float MinimumDistance(float firstRadius, float secondRadius)
        {
            return firstRadius + secondRadius - Skin;
        }

        public static float PushMagnitude(float minimumDistance, float distance)
        {
            return distance > 0.0001f
                ? (minimumDistance - distance) / distance * PushCoefficient
                : 0;
        }

        public static float OtherWeight(float firstRadius, float secondRadius)
        {
            var total = firstRadius + secondRadius;
            return total > 0.0001f ? secondRadius / total : 0.5f;
        }
    }
}
