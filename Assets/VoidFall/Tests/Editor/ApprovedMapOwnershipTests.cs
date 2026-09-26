using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VoidFall.Core;
using VoidFall.Runtime;

namespace VoidFall.Tests.Editor
{
    public sealed class ApprovedMapOwnershipTests
    {
        [Serializable] private sealed class Entry { public string name; public float ppu; }
        [Serializable] private sealed class Manifest { public Entry[] entries; }

        [TestCase(ArenaId.Hydra, 40)]
        [TestCase(ArenaId.NullCity, 41)]
        [TestCase(ArenaId.MonochromeCourt, 64)]
        public void Every_approved_sprite_is_a_dependency_of_its_own_arena(ArenaId arena, int count)
        {
            var root = "Assets/VoidFall/Generated/ArenaPackages/" + arena + "/";
            var plate = AssetDatabase.LoadAssetAtPath<ArenaPlateAsset>(root + "Plate.asset");
            Assert.That(plate.IsValidFor(arena), Is.True);
            var catalog = plate.ApprovedMapVisuals;
            Assert.That(catalog.IsValidFor(arena), Is.True);
            Assert.That(catalog.Entries.Count, Is.EqualTo(count));
            var manifest = JsonUtility.FromJson<Manifest>("{\"entries\":" + File.ReadAllText("Tools/ApprovedMaps/manifest.json") + "}");
            foreach (var expected in manifest.entries.Where(e => ApprovedMapVisualAsset.TryGetOwner(e.name, out var owner) && owner == arena))
            {
                var sprite = catalog.Find(expected.name);
                Assert.That(sprite, Is.Not.Null, expected.name);
                Assert.That(sprite.pixelsPerUnit, Is.EqualTo(expected.ppu).Within(.001f));
                Assert.That(catalog.OwnsSprite(sprite), Is.True);
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite));
                var desktop = importer.GetPlatformTextureSettings("Standalone");
                Assert.That(desktop.overridden, Is.True);
                Assert.That(desktop.format, Is.EqualTo(TextureImporterFormat.BC7));
                for (var recipe = 1; recipe <= 3; recipe++)
                    Assert.That(AssetDatabase.GetDependencies(root + "Recipe" + recipe + ".asset", true),
                        Does.Contain(AssetDatabase.GetAssetPath(sprite)));
            }
            Assert.That(Directory.Exists("Assets/VoidFall/Resources/VoidFall/ApprovedMaps"), Is.False);
        }
    }
}
