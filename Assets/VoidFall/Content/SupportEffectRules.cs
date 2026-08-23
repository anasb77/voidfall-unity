using System;

namespace VoidFall.Core
{
    /// <summary>
    /// Effect math for the section 46 support cards. Every multiplier is
    /// rank-gated: rank 0 is exactly 1 / 0 / no-op, which is why wiring
    /// these cannot disturb the golden master for runs that never roll
    /// them (the stress scenarios only grant the ten parity supports).
    /// </summary>
    public static class SupportEffectRules
    {
        // 46.5 Reflex Matrix (dodge)
        public const double DodgeChancePerRank = 0.04;
        public static double DodgeChance(int rank)
        {
            return Math.Max(0, Math.Min(3, rank)) * DodgeChancePerRank;
        }

        // 46.4 Scholar
        public const double ScholarXpPerRank = 0.08;
        public static double ScholarXpMultiplier(int rank)
        {
            return 1.0 + Math.Max(0, Math.Min(4, rank)) * ScholarXpPerRank;
        }

        // 46.2 Fortune Magnet
        public const double FortuneDropPerRank = 0.05;
        public static double FortuneDropBonus(int rank)
        {
            return Math.Max(0, Math.Min(4, rank)) * FortuneDropPerRank;
        }

        // 46.3 Mass Driver (projectile size)
        public const double ProjectileSizePerRank = 0.10;
        public static double ProjectileSizeMultiplier(int rank)
        {
            return 1.0 + Math.Max(0, Math.Min(3, rank)) * ProjectileSizePerRank;
        }

        // 46.6 Velocity Coils (projectile speed)
        public const double ProjectileSpeedPerRank = 0.10;
        public static double ProjectileSpeedMultiplier(int rank)
        {
            return 1.0 + Math.Max(0, Math.Min(3, rank)) * ProjectileSpeedPerRank;
        }

        // 46.1 Spatial Awareness (camera dezoom)
        public const double SpatialZoomPerRank = 0.05;
        public static double SpatialAwarenessZoom(int rank)
        {
            return 1.0 + Math.Max(0, Math.Min(3, rank)) * SpatialZoomPerRank;
        }
    }
}
