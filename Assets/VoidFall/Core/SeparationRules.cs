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
