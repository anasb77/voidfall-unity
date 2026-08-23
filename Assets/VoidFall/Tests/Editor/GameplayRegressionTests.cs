using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VoidFall.Core;
using VoidFall.Runtime;

namespace VoidFall.Tests.Editor
{
    public sealed class GameplayRegressionTests
    {
        [Test]
        public void Runtime_exposes_a_prepared_arena_plate_asset_contract()
        {
            var assetType = typeof(VoidFallGameRuntime).Assembly.GetType(
                "VoidFall.Runtime.ArenaPlateAsset",
                false);

            Assert.That(assetType, Is.Not.Null,
                "The runtime has no prepared arena asset contract, so it can only generate arena pixels while running.");
            Assert.That(typeof(ScriptableObject).IsAssignableFrom(assetType), Is.True,
                "Prepared arena data must be a Unity asset that can be baked before the player runs.");
        }

        [Test]
        public void Runtime_exposes_a_prepared_procedural_sprite_catalog_contract()
        {
            var runtimeAssembly = typeof(VoidFallGameRuntime).Assembly;
            var catalogType = runtimeAssembly.GetType(
                "VoidFall.Runtime.ProceduralSpriteCatalog",
                false);
            var factoryType = runtimeAssembly.GetType(
                "VoidFall.Runtime.ProceduralSpriteFactory",
                true);

            Assert.That(catalogType, Is.Not.Null,
                "The runtime has no prepared procedural-sprite catalog asset.");
            Assert.That(typeof(ScriptableObject).IsAssignableFrom(catalogType), Is.True);
            Assert.That(
                factoryType.GetMethod("InstallBakedCatalog", BindingFlags.Public | BindingFlags.Static),
                Is.Not.Null,
                "The procedural factory cannot hydrate its caches from prepared assets.");
        }

        [Test]
        public void Prepared_sprite_catalog_hydrates_existing_factory_getters()
        {
            var catalog = ScriptableObject.CreateInstance<ProceduralSpriteCatalog>();
            var createdSprites = new List<Sprite>();
            var createdTextures = new List<Texture2D>();
            var entries = new[]
            {
                new KeyValuePair<string, Sprite>("fixed|circle", CreateTestSprite("prepared-circle", Color.white, createdSprites, createdTextures)),
                new KeyValuePair<string, Sprite>("gem|2", CreateTestSprite("prepared-gem", Color.cyan, createdSprites, createdTextures)),
                new KeyValuePair<string, Sprite>("pickup|xp", CreateTestSprite("prepared-pickup", Color.green, createdSprites, createdTextures)),
                new KeyValuePair<string, Sprite>("projectile-frame|pistol|7", CreateTestSprite("prepared-frame", Color.yellow, createdSprites, createdTextures)),
                new KeyValuePair<string, Sprite>("arena-vignette|2", CreateTestSprite("prepared-vignette", Color.gray, createdSprites, createdTextures)),
                new KeyValuePair<string, Sprite>("enemy|chaser|10|20|30|255|0", CreateTestSprite("prepared-enemy", Color.red, createdSprites, createdTextures)),
            };

            try
            {
                var serialized = new SerializedObject(catalog);
                var schema = serialized.FindProperty("_schema");
                var storedEntries = serialized.FindProperty("_entries");
                Assert.That(schema, Is.Not.Null, "Prepared sprite catalogs have no schema.");
                Assert.That(storedEntries, Is.Not.Null, "Prepared sprite entries are not serialized.");

                schema.intValue = 1;
                storedEntries.arraySize = entries.Length;
                for (var index = 0; index < entries.Length; index++)
                {
                    var stored = storedEntries.GetArrayElementAtIndex(index);
                    stored.FindPropertyRelative("_key").stringValue = entries[index].Key;
                    stored.FindPropertyRelative("_sprite").objectReferenceValue = entries[index].Value;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var factory = typeof(VoidFallGameRuntime).Assembly.GetType(
                    "VoidFall.Runtime.ProceduralSpriteFactory",
                    true);
                factory.GetMethod("InstallBakedCatalog", BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new object[] { catalog });

                Assert.That(InvokeFactory(factory, "Circle", Type.EmptyTypes), Is.SameAs(entries[0].Value));
                Assert.That(InvokeFactory(factory, "Gem", new[] { typeof(int) }, 2), Is.SameAs(entries[1].Value));
                Assert.That(InvokeFactory(factory, "Pickup", new[] { typeof(string) }, "xp"), Is.SameAs(entries[2].Value));
                Assert.That(InvokeFactory(factory, "ProjectileFrame", new[] { typeof(string), typeof(int) }, "pistol", 7), Is.SameAs(entries[3].Value));
                Assert.That(InvokeFactory(factory, "ArenaVignette", new[] { typeof(ArenaId) }, ArenaId.WhiteSakura), Is.SameAs(entries[4].Value));
                Assert.That(
                    InvokeFactory(
                        factory,
                        "Enemy",
                        new[] { typeof(string), typeof(Color), typeof(bool) },
                        "chaser",
                        (Color)new Color32(10, 20, 30, 255),
                        false),
                    Is.SameAs(entries[5].Value));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                foreach (var sprite in createdSprites) UnityEngine.Object.DestroyImmediate(sprite);
                foreach (var texture in createdTextures) UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void Sprite_bake_snapshot_covers_startup_and_gameplay_families()
        {
            var factory = typeof(VoidFallGameRuntime).Assembly.GetType(
                "VoidFall.Runtime.ProceduralSpriteFactory",
                true);
            var snapshotMethod = factory.GetMethod(
                "BuildCatalogSnapshot",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(snapshotMethod, Is.Not.Null,
                "The Editor baker has no complete procedural-sprite snapshot entry point.");

            var catalog = snapshotMethod.Invoke(null, null) as ProceduralSpriteCatalog;
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.IsUsable(), Is.True);

            var keys = new HashSet<string>();
            foreach (var entry in catalog.Entries) keys.Add(entry.Key);
            Assert.That(keys, Does.Contain("fixed|circle"));
            Assert.That(keys, Does.Contain("fixed|operative"));
            Assert.That(keys, Does.Contain("fixed|particle-dot"));
            Assert.That(keys, Does.Contain("gem|2"));
            Assert.That(keys, Does.Contain("arena-rock|5"));
            Assert.That(keys, Does.Contain("arena-vignette|2"));
            Assert.That(keys, Does.Contain("workshop-layer|protocol/1"));
            Assert.That(keys, Does.Contain("projectile-frame|pistol|31"));

            var pistolFrames = 0;
            foreach (var key in keys)
                if (key.StartsWith("projectile-frame|pistol|", StringComparison.Ordinal)) pistolFrames++;
            Assert.That(pistolFrames, Is.EqualTo(32),
                "A missing orientation frame would force a procedural build during combat.");

            factory.GetMethod("ReleaseCatalogSnapshot", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { catalog });
        }

        [Test]
        public void Runtime_startup_uses_baked_sprite_catalog_without_a_warm_iterator()
        {
            var host = new GameObject("Prepared Sprite Runtime");
            host.SetActive(false);
            try
            {
                var runtime = host.AddComponent<VoidFallGameRuntime>();
                var configured = (bool)InvokeExact(
                    runtime,
                    "ConfigurePreparedSpritesForStartup",
                    Type.EmptyTypes);

                Assert.That(configured, Is.True,
                    "Startup did not install the prepared sprite catalog.");
                Assert.That(GetField(runtime, "_spriteWarmSteps"), Is.Null,
                    "Startup still scheduled procedural drawing across menu frames.");

                var catalog = Resources.Load<ProceduralSpriteCatalog>(
                    "VoidFall/Generated/ProceduralSpriteCatalog");
                var circle = Array.Find(
                    new List<ProceduralSpriteCatalogEntry>(catalog.Entries).ToArray(),
                    entry => entry.Key == "fixed|circle").Sprite;
                var factory = typeof(VoidFallGameRuntime).Assembly.GetType(
                    "VoidFall.Runtime.ProceduralSpriteFactory",
                    true);
                Assert.That(
                    InvokeFactory(factory, "Circle", Type.EmptyTypes),
                    Is.SameAs(circle),
                    "The existing factory getter was not hydrated with the imported sprite.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Prepared_arena_asset_rejects_the_wrong_arena_identity()
        {
            var asset = ScriptableObject.CreateInstance<ArenaPlateAsset>();
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(
                texture,
                new Rect(0, 0, 2, 2),
                new Vector2(0.5f, 0.5f),
                1f);

            try
            {
                var serialized = new SerializedObject(asset);
                var arena = serialized.FindProperty("_arena");
                var baseSprite = serialized.FindProperty("_baseSprite");
                var detailSprite = serialized.FindProperty("_detailSprite");
                var width = serialized.FindProperty("_width");
                var height = serialized.FindProperty("_height");
                var schema = serialized.FindProperty("_schema");

                Assert.That(arena, Is.Not.Null, "Prepared arena identity is not serialized.");
                Assert.That(baseSprite, Is.Not.Null, "Prepared base sprite is not serialized.");
                Assert.That(detailSprite, Is.Not.Null, "Prepared detail sprite is not serialized.");
                Assert.That(width, Is.Not.Null, "Prepared width is not serialized.");
                Assert.That(height, Is.Not.Null, "Prepared height is not serialized.");
                Assert.That(schema, Is.Not.Null, "Prepared schema version is not serialized.");

                arena.enumValueIndex = (int)ArenaId.WhiteSakura;
                baseSprite.objectReferenceValue = sprite;
                detailSprite.objectReferenceValue = sprite;
                width.intValue = 3840;
                height.intValue = 2160;
                schema.intValue = 2;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var validity = typeof(ArenaPlateAsset).GetMethod("IsValidFor");
                Assert.That(validity, Is.Not.Null, "Prepared arena assets have no validity boundary.");
                Assert.That(validity.Invoke(asset, new object[] { ArenaId.WhiteSakura }), Is.True);
                Assert.That(validity.Invoke(asset, new object[] { ArenaId.Void }), Is.False,
                    "An asset for Sakura must not be silently installed as the Void arena.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void Arena_packages_use_a_stable_address_contract()
        {
            Assert.That(
                ArenaCatalogRules.PackageAddress(new ArenaPackageKey("white-sakura", 1)),
                Is.EqualTo("VoidFall/Arenas/white-sakura/recipe-2"));
        }

        [Test]
        public void Prepared_arena_recipes_reference_the_imported_plate_without_copying_it()
        {
            const string root = "Assets/VoidFall/Generated/ArenaPackages/WhiteSakura/";
            var prepared = AssetDatabase.LoadAssetAtPath<ArenaPlateAsset>(root + "Plate.asset");
            var recipe = AssetDatabase.LoadAssetAtPath<ArenaRecipeAsset>(root + "Recipe2.asset");
            Assert.That(prepared, Is.Not.Null);
            Assert.That(recipe, Is.Not.Null);
            Assert.That(recipe.Plate, Is.SameAs(prepared));
            Assert.That(recipe.Plate.BaseSprite.texture.isReadable, Is.False);
        }

        [Test]
        public void Arena_resolution_never_generates_a_runtime_fallback()
        {
            var host = new GameObject("Prepared Arena Resolution");
            host.SetActive(false);
            try
            {
                var runtime = host.AddComponent<VoidFallGameRuntime>();
                InvokeExact(
                    runtime,
                    "EnsureArenaPlate",
                    new[] { typeof(ArenaId) },
                    ArenaId.RedNebula);

                var bases = (Sprite[])GetField(runtime, "_arenaPlateSprites");
                var details = (Sprite[])GetField(runtime, "_arenaPlateDetailSprites");
                Assert.That(bases[(int)ArenaId.RedNebula], Is.Null,
                    "EnsureArenaPlate generated pixels instead of waiting for its package handle.");
                Assert.That(details[(int)ArenaId.RedNebula], Is.Null,
                    "EnsureArenaPlate generated details instead of waiting for its package handle.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Xp_drop_creates_a_visible_nonempty_sprite_renderer()
        {
            var world = new GameObject("XP Drop Test World");
            var host = new GameObject("XP Drop Test Runtime");
            host.SetActive(false);

            try
            {
                var runtime = host.AddComponent<VoidFallGameRuntime>();
                SetField(runtime, "_worldRoot", world.transform);
                var spawned = (bool)InvokeExact(
                    runtime,
                    "SpawnEnemy",
                    new[] { typeof(string) },
                    "chaser");
                Assert.That(spawned, Is.True, "The test enemy could not be spawned.");
                InvokeExact(
                    runtime,
                    "ResolveEnemyDeath",
                    new[] { typeof(int), typeof(bool) },
                    0,
                    false);

                var factory = typeof(VoidFallGameRuntime).Assembly.GetType(
                    "VoidFall.Runtime.ProceduralSpriteFactory",
                    true);
                factory.GetMethod("FlushAtlas", BindingFlags.Public | BindingFlags.Static)
                    ?.Invoke(null, null);

                Assert.That(runtime.ActivePickupsCount, Is.EqualTo(1),
                    "The gameplay state did not retain the XP drop.");

                var renderers = (SpriteRenderer[])GetField(runtime, "_pickupViews");
                var renderer = Array.Find(renderers, candidate => candidate != null);
                Assert.That(renderer, Is.Not.Null, "The XP drop did not create a renderer.");
                Assert.That(renderer.enabled, Is.True, "The XP renderer was created disabled.");
                Assert.That(renderer.sprite, Is.Not.Null, "The XP renderer has no sprite.");
                Assert.That(renderer.sharedMaterial, Is.Not.Null, "The XP renderer has no material.");

                var sprite = renderer.sprite;
                var sourceTexture = sprite.texture;
                Texture2D readableCopy = null;
                if (!sourceTexture.isReadable)
                {
                    var assetPath = AssetDatabase.GetAssetPath(sprite);
                    Assert.That(assetPath, Is.Not.Empty,
                        "The imported XP sprite has no source asset path.");
                    readableCopy = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    Assert.That(
                        ImageConversion.LoadImage(
                            readableCopy,
                            File.ReadAllBytes(Path.GetFullPath(assetPath)),
                            false),
                        Is.True,
                        "The imported XP sprite PNG could not be decoded for validation.");
                    sourceTexture = readableCopy;
                }

                var pixels = sourceTexture.GetPixels32();
                var rect = readableCopy == null
                    ? sprite.textureRect
                    : new Rect(0, 0, sourceTexture.width, sourceTexture.height);
                var minX = Mathf.FloorToInt(rect.xMin);
                var minY = Mathf.FloorToInt(rect.yMin);
                var maxX = Mathf.CeilToInt(rect.xMax);
                var maxY = Mathf.CeilToInt(rect.yMax);
                var hasVisiblePixel = false;
                for (var y = minY; y < maxY && !hasVisiblePixel; y++)
                {
                    for (var x = minX; x < maxX; x++)
                    {
                        if (pixels[y * sourceTexture.width + x].a == 0) continue;
                        hasVisiblePixel = true;
                        break;
                    }
                }

                Assert.That(hasVisiblePixel, Is.True, "The XP sprite contains no visible pixels.");
                if (readableCopy != null) UnityEngine.Object.DestroyImmediate(readableCopy);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(world);
            }
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing field: " + name);
            field.SetValue(target, value);
        }

        private static Sprite CreateTestSprite(
            string name,
            Color color,
            List<Sprite> sprites,
            List<Texture2D> textures)
        {
            var texture = new Texture2D(2, 2);
            texture.SetPixels(new[] { color, color, color, color });
            texture.Apply();
            var sprite = Sprite.Create(
                texture,
                new Rect(0, 0, 2, 2),
                new Vector2(0.5f, 0.5f),
                2f);
            sprite.name = name;
            textures.Add(texture);
            sprites.Add(sprite);
            return sprite;
        }

        private static object InvokeFactory(
            Type factory,
            string name,
            Type[] parameterTypes,
            params object[] args)
        {
            var method = factory.GetMethod(
                name,
                BindingFlags.Public | BindingFlags.Static,
                null,
                parameterTypes,
                null);
            Assert.That(method, Is.Not.Null, "Missing factory method: " + name);
            return method.Invoke(null, args);
        }

        private static object GetField(object target, string name)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing field: " + name);
            return field.GetValue(target);
        }

        private static object InvokeExact(
            object target,
            string name,
            Type[] parameterTypes,
            params object[] args)
        {
            var method = target.GetType().GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                parameterTypes,
                null);
            Assert.That(method, Is.Not.Null, "Missing method: " + name);
            return method.Invoke(target, args);
        }
    }
}
