using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VoidFall.Core;
using VoidFall.Runtime;

namespace VoidFall.Tests.Editor
{
    public sealed class ArenaBakeTests
    {
        private static readonly ArenaId[] RequiredArenas =
        {
            ArenaId.Void,
            ArenaId.RedNebula,
            ArenaId.WhiteSakura,
        };

        [Test]
        public void Every_current_arena_has_a_valid_imported_plate()
        {
            foreach (var arena in RequiredArenas)
            {
                var path = "Assets/VoidFall/Generated/ArenaPackages/" + arena + "/Plate.asset";
                var asset = AssetDatabase.LoadAssetAtPath<ArenaPlateAsset>(path);
                Assert.That(asset, Is.Not.Null,
                    "Missing prepared arena package plate: " + path);
                Assert.That(asset.IsValidFor(arena), Is.True, "Invalid prepared arena: " + arena);
                Assert.That(asset.Width, Is.EqualTo(3021));
                Assert.That(asset.Height, Is.EqualTo(1699));

                AssertImported(asset.BaseSprite, arena + " base");
                AssertImported(asset.DetailSprite, arena + " details");

                for (var recipeIndex = 0; recipeIndex < ArenaCatalogRules.RecipesPerArena; recipeIndex++)
                {
                    var recipePath = "Assets/VoidFall/Generated/ArenaPackages/" + arena +
                        "/Recipe" + (recipeIndex + 1) + ".asset";
                    var recipe = AssetDatabase.LoadAssetAtPath<ArenaRecipeAsset>(recipePath);
                    var key = new ArenaPackageKey(ArenaCatalogRules.StableId(arena), recipeIndex);
                    Assert.That(recipe, Is.Not.Null, "Missing arena recipe: " + recipePath);
                    Assert.That(recipe.IsValidFor(key), Is.True, "Invalid arena recipe: " + recipePath);
                    Assert.That(recipe.Plate, Is.SameAs(asset),
                        "Recipe duplicated or bypassed the arena's shared identity plate.");
                }
            }
        }

        private static void AssertImported(Sprite sprite, string label)
        {
            Assert.That(sprite, Is.Not.Null, label + " sprite is missing.");
            Assert.That(sprite.texture.isReadable, Is.False, label + " texture kept a CPU copy.");

            var path = AssetDatabase.GetAssetPath(sprite.texture);
            Assert.That(path, Is.Not.Empty, label + " texture is not an imported asset.");
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null, label + " has no texture importer.");
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.mipmapEnabled, Is.True, label + " has no mip chain.");
            Assert.That(importer.streamingMipmaps, Is.True, label + " does not stream mip levels.");
        }
    }
}
