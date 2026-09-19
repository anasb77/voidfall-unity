using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
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
            Assert.That(byKey, Does.ContainKey("projectile-frame|pulse|31"));
            Assert.That(byKey, Does.ContainKey("projectile-frame|pulse-bright|31"));
            for (var key = 0; key < 49; key++) Assert.That(byKey, Does.ContainKey("arsenal|" + key));
            foreach (var id in new[] { "swarmer", "spiky", "shuriken" })
                Assert.That(byKey.Keys.Any(k => k.StartsWith("enemy|" + id + "|")), Is.True, id + " needs baked art");

            foreach (var spiky in byKey.Where(p => p.Key.StartsWith("enemy|spiky|")))
            {
                Assert.That(spiky.Value.rect.width, Is.GreaterThanOrEqualTo(330), "Giant Spiky needs its supersampled raster.");
                Assert.That(spiky.Value.bounds.size.x, Is.EqualTo(1).Within(.001), "Raster quality must not change world size.");
            }

            foreach (var pair in byKey)
            {
                Assert.That(EditorUtility.IsPersistent(pair.Value), Is.True,
                    pair.Key + " still points at a temporary runtime sprite.");
                Assert.That(pair.Value.texture.isReadable, Is.False,
                    pair.Key + " keeps a duplicate CPU-readable texture allocation.");
            }
        }

        [Test]
        public void Arsenal_lookup_uses_prepared_assets_and_cleanup_preserves_them()
        {
            var factory = typeof(ProceduralSpriteCatalog).Assembly.GetType("VoidFall.Runtime.ProceduralSpriteFactory", true);
            var catalog = Resources.Load<ProceduralSpriteCatalog>(ResourcePath);
            var flags = BindingFlags.Public | BindingFlags.Static;
            Assert.That(factory.GetMethod("InstallBakedCatalog", flags).Invoke(null, new object[] { catalog }), Is.True);
            var lookup = factory.GetMethod("ArsenalWeapon", flags);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (var weapon = 0; weapon < 4; weapon++)
                for (var rank = 1; rank <= 6; rank++)
                    for (var evolved = 0; evolved < 2; evolved++)
                    {
                        var id = new[] { "mines", "summons", "clock", "boomerang" }[weapon];
                        var sprite = lookup.Invoke(null, new object[] { id, rank, evolved == 1 });
                        var key = "arsenal|" + (weapon * 12 + (rank - 1) * 2 + evolved);
                        Assert.That(sprite, Is.SameAs(catalog.Entries.Single(e => e.Key == key).Sprite));
                    }
            var face = factory.GetMethod("ArsenalClockFace", flags).Invoke(null, null);
            Assert.That(face, Is.SameAs(catalog.Entries.Single(e => e.Key == "arsenal|48").Sprite));
            TestContext.WriteLine("Prepared arsenal lookup (49 sprites, including reflection/assertions): " + watch.Elapsed.TotalMilliseconds + " ms");
            factory.GetMethod("DestroyArsenalSprites", flags).Invoke(null, null);
            foreach (var entry in catalog.Entries.Where(e => e.Key.StartsWith("arsenal|")))
            {
                Assert.That(entry.Sprite != null && entry.Sprite.texture != null, Is.True);
                Assert.That(EditorUtility.IsPersistent(entry.Sprite), Is.True);
            }
            factory.GetMethod("InstallBakedCatalog", flags).Invoke(null, new object[] { catalog });
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

        [TestCase(0)] // Mines
        [TestCase(1)] // Summons
        [TestCase(2)] // Clock
        [TestCase(3)] // Boomerang
        public void Every_arsenal_rank_has_distinct_authored_pixels(int weapon)
        {
            // Production textures intentionally have no duplicate CPU pixel buffer.
            // Inspect the baked source PNG here, rather than making shipped assets readable.
            var catalog = Resources.Load<ProceduralSpriteCatalog>(ResourcePath);
            var previous = 0UL;
            for (var rank = 1; rank <= 6; rank++)
            {
                var key = "arsenal|" + (weapon * 12 + (rank - 1) * 2);
                var sprite = catalog.Entries.Single(e => e.Key == key).Sprite;
                var decoded = new Texture2D(2, 2);
                try
                {
                    Assert.That(decoded.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite))), Is.True);
                    var hash = 14695981039346656037UL;
                    foreach (var pixel in decoded.GetPixels32())
                    { hash = (hash ^ pixel.r) * 1099511628211UL; hash = (hash ^ pixel.g) * 1099511628211UL; hash = (hash ^ pixel.b) * 1099511628211UL; hash = (hash ^ pixel.a) * 1099511628211UL; }
                    Assert.That(hash, Is.Not.EqualTo(previous), "Rank " + rank + " should change weapon appearance");
                    previous = hash;
                }
                finally { Object.DestroyImmediate(decoded); }
            }
        }
    }
}
