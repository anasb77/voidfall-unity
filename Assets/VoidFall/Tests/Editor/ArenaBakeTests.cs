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
            ArenaId.Hydra,
            ArenaId.MonochromeCourt,
        };

        [Test]
        public void Monochrome_pixels_are_balanced_black_white_and_keep_a_visible_board_grid()
        {
            ArenaPlateFactory.WarmSpecs();
            var field = ArenaPlateFactory.BuildBasePixels(ArenaId.MonochromeCourt, 160, 90);
            var detail = ArenaPlateFactory.BuildDetailPixels(ArenaId.MonochromeCourt, 160, 90);
            var dark = 0;
            var light = 0;
            var visibleDetail = 0;
            foreach (var pixel in field)
            {
                var luminance = pixel.r + pixel.g + pixel.b;
                if (luminance < 165) dark++;
                if (luminance > 570) light++;
            }
            foreach (var pixel in detail) if (pixel.a > 60) visibleDetail++;

            Assert.That(dark, Is.GreaterThan(field.Length * 0.28f));
            Assert.That(light, Is.GreaterThan(field.Length * 0.28f));
            Assert.That(visibleDetail, Is.GreaterThan(detail.Length * 0.08f));
        }

        [Test]
        public void Hydra_generated_pixels_have_toxic_green_field_and_opaque_bone_detail()
        {
            ArenaPlateFactory.WarmSpecs();
            var field = ArenaPlateFactory.BuildBasePixels(ArenaId.Hydra, 96, 54);
            var detail = ArenaPlateFactory.BuildDetailPixels(ArenaId.Hydra, 160, 90);
            var greenDominant = 0;
            foreach (var pixel in field)
                if (pixel.g > pixel.r * 1.35f && pixel.g > pixel.b * 1.15f) greenDominant++;
            Assert.That(greenDominant, Is.GreaterThan(field.Length * 0.55f));

            var ivoryDetail = 0;
            foreach (var pixel in detail)
                if (pixel.a > 80 && pixel.r > 150 && pixel.g > 145 && pixel.b > 105) ivoryDetail++;
            Assert.That(ivoryDetail, Is.GreaterThan(80),
                "Hydra detail plate must carry visible ivory rib/spine pixels.");
        }

        [Test]
        public void Hydra_plate_and_boss_use_the_approved_authored_reference_layers()
        {
            var plate = AssetDatabase.LoadAssetAtPath<ArenaPlateAsset>(
                "Assets/VoidFall/Generated/ArenaPackages/Hydra/Plate.asset");
            Assert.That(plate, Is.Not.Null);
            Assert.That(
                AssetDatabase.GetAssetPath(plate.BaseSprite.texture),
                Is.EqualTo("Assets/VoidFall/Art/Hydra/HydraBase.png"));
            Assert.That(
                AssetDatabase.GetAssetPath(plate.DetailSprite.texture),
                Is.EqualTo("Assets/VoidFall/Art/Hydra/HydraDetails.png"));
            var boss = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/VoidFall/Resources/VoidFall/Hydra/HydraPrime.png");
            Assert.That(boss, Is.Not.Null);
            Assert.That(boss.texture.width, Is.EqualTo(1024));
            Assert.That(boss.texture.height, Is.EqualTo(1024));
        }

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
                // Keep in sync with ArenaContentBaker's bake tiers: 4K sky
                // base, 1440p detail elements.
                Assert.That(asset.Width, Is.EqualTo(3840));
                Assert.That(asset.Height, Is.EqualTo(2160));
                Assert.That(asset.DetailWidth, Is.EqualTo(2560));
                Assert.That(asset.DetailHeight, Is.EqualTo(1440));

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
