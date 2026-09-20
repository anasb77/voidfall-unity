using System;

namespace VoidFall.Core
{
    /// <summary>September 20 approved browser rules, expressed in native combat world units.</summary>
    public static class ApprovedMapRules
    {
        public const int Version = 3;
        public const float CityLayoutScale = 1.6f;
        public const float HivePeriod = 3f;
        public const int HiveBroodSize = 5;
        public const float TerritoryShield = 24f;
        public static float CameraHeight(string arena) => arena == "null-city" ? 860f :
            arena == "hydra" || arena == "monochrome-court" ? 908f : 0f;
        public static int CourtTier(double age, double roll) => age < 35 ? 0 : age < 80 ?
            (roll < .55 ? 0 : 1) : roll < .3 ? 0 : roll < .8 ? 1 : 2;
        public static int CityAmbient(double roll)
        {
            if (roll < .4) return 17 + Math.Min(4, (int)(roll / .08));
            if (roll < .6) return 3;
            if (roll < .75) return 12 + Math.Min(4, (int)((roll-.6)/.03));
            var p=(roll-.75)/.25;
            return p<.45?3:p<.62?0:p<.74?2:p<.84?1:p<.92?4:8;
        }
        public static bool InTerritory(int cellX,int cellY,int rookX,int rookY) =>
            cellX >= rookX-2 && cellX < rookX+2 && cellY >= rookY-2 && cellY < rookY+2;
        public static float SentinelAge(float elapsed,int slot) => (elapsed + slot*2.31f) % 14f;
    }
}
