using System;

namespace VoidFall.Core
{
    /// <summary>
    /// Hand-authored Unity-first support cards appended after the generated
    /// parity catalog (spec section 46 - the experimental/missing supports).
    /// The generated file stays untouched; everything that used to index
    /// ContentCatalog.Supports now goes through AllSupports(), which keeps
    /// the ten parity entries first. Extra cards are identified by stable IDs;
    /// retired IDs are folded into their surviving cards when records load.
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
                    "+8% experience; +5% special drops",
                    "+16% experience; +10% special drops",
                    "+24% experience; +15% special drops",
                    "+32% experience; +20% special drops"
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
                    "+10% projectile/orbit speed; +5% camera dezoom",
                    "+20% projectile/orbit speed; +10% camera dezoom",
                    "+30% projectile/orbit speed; +15% camera dezoom"
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
            _all = SurvivalSupportCatalog.Append(LegacyRestorationRules.AppendSupports(merged));
            return _all;
        }

        public static int SupportCount => AllSupports().Length;

        public static string CanonicalSupportId(string id)
        {
            if (id == "fortune") return "scholar";
            if (id == "spatialAwareness") return "projectileSpeed";
            return id;
        }
    }
}
