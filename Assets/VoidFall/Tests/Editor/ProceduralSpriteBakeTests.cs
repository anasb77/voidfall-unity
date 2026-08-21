using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;
using VoidFall.Core;
using VoidFall.Runtime;

namespace VoidFall.Tests.Editor
{
    public sealed class ProceduralSpriteBakeTests
    {
        private const string ResourcePath = "VoidFall/Generated/ProceduralSpriteCatalog";

        [Test]
        public void Prepared_sprite_catalog_is_imported_and_complete()
        {
            var catalog = Resources.Load<ProceduralSpriteCatalog>(ResourcePath);
            Assert.That(catalog, Is.Not.Null,
                "Run Tools/VoidFall/Bake Prepared Procedural Sprites before building.");
            Assert.That(catalog.IsUsable(), Is.True);

            var byKey = new Dictionary<string, Sprite>();
            foreach (var entry in catalog.Entries) byKey[entry.Key] = entry.Sprite;

            Assert.That(byKey, Does.ContainKey("fixed|circle"));
            Assert.That(byKey, Does.ContainKey("fixed|operative"));
            Assert.That(byKey, Does.ContainKey("gem|2"));
            Assert.That(byKey, Does.ContainKey("arena-vignette|2"));
            Assert.That(byKey, Does.ContainKey("workshop-layer|protocol/1"));
            Assert.That(byKey, Does.ContainKey("projectile-frame|pistol|31"));

            foreach (var pair in byKey)
            {
                Assert.That(EditorUtility.IsPersistent(pair.Value), Is.True,
                    pair.Key + " still points at a temporary runtime sprite.");
                Assert.That(pair.Value.texture.isReadable, Is.False,
                    pair.Key + " keeps a duplicate CPU-readable texture allocation.");
            }
        }

        [Test]
        public void Prepared_content_is_registered_for_splash_preload()
        {
            var expected = new Object[]
            {
                Resources.Load<ProceduralSpriteCatalog>(ResourcePath),
                Resources.Load<SpriteAtlas>("VoidFall/Generated/ProceduralSpriteAtlas"),
            };
            Assert.That(expected, Has.None.Null, "A required prepared asset is missing.");

            var preloaded = PlayerSettings.GetPreloadedAssets();
            foreach (var asset in expected)
                Assert.That(preloaded.Contains(asset), Is.True,
                    AssetDatabase.GetAssetPath(asset) + " is not loaded during the splash phase.");
            Assert.That(preloaded.OfType<ArenaPlateAsset>(), Is.Empty,
                "Arena plates must be loaded by the residency manager, not during the splash phase.");
        }
    }
}
