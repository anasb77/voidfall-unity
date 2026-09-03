using System;

namespace VoidFall.Core
{
    /// <summary>
    /// Hand-authored Unity-first support cards appended after the generated
    /// parity catalog (spec section 46 - the experimental/missing supports).
    /// The generated file stays untouched; everything that used to index
    /// ContentCatalog.Supports now goes through AllSupports(), which keeps
    /// the ten parity entries first - their indices are unchanged, so saved
    /// run records, telemetry, and the browser import/export stay stable.
    /// </summary>
    public static class ExtendedCatalog
    {
        public static readonly SupportDefinition[] ExtraSupports =
        {
            new SupportDefinition
            {
                Id = "dodge",
                Name = "Reflex Matrix",
                MaxRank = 3,
                Accent = "#facc15",
                Weight = 7,
                Descriptions = new[]
                {
                    "4% chance to dodge incoming hits",
                    "8% chance to dodge incoming hits",
                    "12% chance to dodge incoming hits"
                },
            },
            new SupportDefinition
            {
                Id = "scholar",
                Name = "Scholar",
                MaxRank = 4,
                Accent = "#a5f3fc",
                Weight = 8,
                Descriptions = new[]
                {
                    "+8% experience gained", "+8% experience gained",
                    "+8% experience gained", "+8% experience gained"
                },
            },
            new SupportDefinition
            {
                Id = "fortune",
                Name = "Fortune Magnet",
                MaxRank = 4,
                Accent = "#fcd34d",
                Weight = 6,
                Descriptions = new[]
                {
                    "+5% power-up drop chance", "+5% power-up drop chance",
                    "+5% power-up drop chance", "+5% power-up drop chance"
                },
            },
            new SupportDefinition
            {
                Id = "projectileSpeed",
                Name = "Velocity Coils",
                MaxRank = 3,
                Accent = "#7dd3fc",
                Weight = 7,
                Descriptions = new[]
                {
                    "+10% projectile speed", "+10% projectile speed", "+10% projectile speed"
                },
            },
            new SupportDefinition
            {
                Id = "spatialAwareness",
                Name = "Spatial Awareness",
                MaxRank = 3,
                Accent = "#c4b5fd",
                Weight = 5,
                Descriptions = new[]
                {
                    "+5% camera dezoom", "+5% camera dezoom", "+5% camera dezoom"
                },
            },
        };

        private static SupportDefinition[] _all;

        /// <summary>The full support list: parity entries first, then extras.</summary>
        public static SupportDefinition[] AllSupports()
        {
            if (_all != null) return _all;
            var baseSupports = ContentCatalog.Supports;
            var merged = new SupportDefinition[baseSupports.Length + ExtraSupports.Length];
            Array.Copy(baseSupports, merged, baseSupports.Length);
            Array.Copy(ExtraSupports, 0, merged, baseSupports.Length, ExtraSupports.Length);
            _all = merged;
            return _all;
        }

        public static int SupportCount => AllSupports().Length;
    }
}
