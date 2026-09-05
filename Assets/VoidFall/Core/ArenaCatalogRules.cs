using System;

namespace VoidFall.Core
{
    public static class ArenaCatalogRules
    {
        public const int RecipesPerArena = 3;

        private static readonly ArenaRecipeLayout[] Layouts =
        {
            new ArenaRecipeLayout(0, 0x68bc21ebu, false, -0.018f, 0.012f, 1.015f),
            new ArenaRecipeLayout(1, 0x02e5be93u, true, 0.022f, -0.008f, 1.035f),
            new ArenaRecipeLayout(2, 0x9e3779b9u, false, 0.006f, 0.024f, 1.055f),
        };

        public static string StableId(ArenaId arena)
        {
            switch (arena)
            {
                case ArenaId.RedNebula: return "red-nebula";
                case ArenaId.WhiteSakura: return "white-sakura";
                case ArenaId.Hydra: return "hydra";
                case ArenaId.MonochromeCourt: return "monochrome-court";
                case ArenaId.NullCity: return "null-city";
                default: return "abyss";
            }
        }

        public static ArenaId LegacyArena(string stableId)
        {
            switch (stableId)
            {
                case "red-nebula": return ArenaId.RedNebula;
                case "white-sakura": return ArenaId.WhiteSakura;
                case "hydra": return ArenaId.Hydra;
                case "monochrome-court": return ArenaId.MonochromeCourt;
                case "null-city": return ArenaId.NullCity;
                default: return ArenaId.Void;
            }
        }

        public static int RecipeIndex(uint runSeed, string stableArenaId)
        {
            unchecked
            {
                var hash = runSeed ^ 0x9e3779b9u;
                if (!string.IsNullOrEmpty(stableArenaId))
                {
                    for (var index = 0; index < stableArenaId.Length; index++)
                    {
                        hash ^= stableArenaId[index];
                        hash *= 16777619u;
                    }
                }
                hash ^= hash >> 16;
                hash *= 0x7feb352du;
                hash ^= hash >> 15;
                return (int)(hash % RecipesPerArena);
            }
        }

        public static ArenaRecipeLayout RecipeLayout(int recipeIndex)
        {
            return Layouts[Math.Max(0, Math.Min(RecipesPerArena - 1, recipeIndex))];
        }

        public static string PackageAddress(ArenaPackageKey key)
        {
            return key.IsValid
                ? "VoidFall/Arenas/" + key.StableArenaId + "/recipe-" + (key.RecipeIndex + 1)
                : string.Empty;
        }
    }

    public readonly struct ArenaRecipeLayout
    {
        public ArenaRecipeLayout(
            int index,
            uint decorSalt,
            bool mirrorX,
            float detailOffsetX,
            float detailOffsetY,
            float detailScale)
        {
            Index = index;
            DecorSalt = decorSalt;
            MirrorX = mirrorX;
            DetailOffsetX = detailOffsetX;
            DetailOffsetY = detailOffsetY;
            DetailScale = detailScale;
        }

        public int Index { get; }
        public uint DecorSalt { get; }
        public bool MirrorX { get; }
        public float DetailOffsetX { get; }
        public float DetailOffsetY { get; }
        public float DetailScale { get; }
    }

    public readonly struct ArenaPackageKey : IEquatable<ArenaPackageKey>
    {
        public ArenaPackageKey(string stableArenaId, int recipeIndex)
        {
            StableArenaId = stableArenaId;
            RecipeIndex = Math.Max(0, Math.Min(ArenaCatalogRules.RecipesPerArena - 1, recipeIndex));
        }

        public string StableArenaId { get; }
        public int RecipeIndex { get; }
        public bool IsValid => !string.IsNullOrEmpty(StableArenaId);

        public bool Equals(ArenaPackageKey other)
        {
            return RecipeIndex == other.RecipeIndex &&
                   string.Equals(StableArenaId, other.StableArenaId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is ArenaPackageKey other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                return ((StableArenaId != null ? StableArenaId.GetHashCode() : 0) * 397) ^ RecipeIndex;
            }
        }

        public override string ToString() => IsValid ? StableArenaId + "/recipe-" + RecipeIndex : "<none>";
        public static bool operator ==(ArenaPackageKey left, ArenaPackageKey right) => left.Equals(right);
        public static bool operator !=(ArenaPackageKey left, ArenaPackageKey right) => !left.Equals(right);
    }
}
