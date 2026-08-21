using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    [CreateAssetMenu(menuName = "VoidFall/Arena Recipe", fileName = "ArenaRecipe")]
    public sealed class ArenaRecipeAsset : ScriptableObject
    {
        public const int CurrentSchema = 1;

        [SerializeField] private int _schema = CurrentSchema;
        [SerializeField] private string _stableArenaId;
        [SerializeField] private ArenaId _legacyArena;
        [SerializeField, Range(0, ArenaCatalogRules.RecipesPerArena - 1)]
        private int _recipeIndex;
        [SerializeField] private ArenaPlateAsset _plate;
        [SerializeField] private long _estimatedTextureBytes;

        public string StableArenaId => _stableArenaId;
        public ArenaId LegacyArena => _legacyArena;
        public int RecipeIndex => _recipeIndex;
        public ArenaPlateAsset Plate => _plate;
        public long EstimatedTextureBytes => _estimatedTextureBytes;

        public bool IsValidFor(ArenaPackageKey key)
        {
            return _schema == CurrentSchema &&
                   key.IsValid &&
                   string.Equals(_stableArenaId, key.StableArenaId, System.StringComparison.Ordinal) &&
                   _recipeIndex == key.RecipeIndex &&
                   _plate != null &&
                   _plate.IsValidFor(_legacyArena) &&
                   _estimatedTextureBytes > 0;
        }
    }
}
