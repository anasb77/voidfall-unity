using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using VoidFall.Runtime;
using Object = UnityEngine.Object;

namespace VoidFall.Editor
{
    public static class ProceduralSpriteBaker
    {
        private const string GeneratedRoot = "Assets/VoidFall/Generated";
        private const string SpriteRoot = GeneratedRoot + "/ProceduralSprites";
        private const string ResourceRoot = GeneratedRoot + "/Resources/VoidFall/Generated";
        private const string CatalogPath = ResourceRoot + "/ProceduralSpriteCatalog.asset";
        private const string AtlasPath = ResourceRoot + "/ProceduralSpriteAtlas.spriteatlas";

        [MenuItem("Tools/VoidFall/Bake Prepared Procedural Sprites")]
        public static void BakeAll()
        {
            EnsureFolderTree(SpriteRoot);
            EnsureFolderTree(ResourceRoot);

            var snapshot = BuildCatalogSnapshot();
            try
            {
                if (snapshot == null || !snapshot.IsUsable())
                    throw new InvalidOperationException("Procedural sprite snapshot is empty or invalid.");

                var importedBySource = new Dictionary<Sprite, Sprite>();
                var generatedPaths = new HashSet<string>(StringComparer.Ordinal);
                var safeForAtlas = new List<Object>();
                var importedEntries = new List<ProceduralSpriteCatalogEntry>(snapshot.Count);
                var sourceIndex = 0;

                for (var entryIndex = 0; entryIndex < snapshot.Entries.Count; entryIndex++)
                {
                    var entry = snapshot.Entries[entryIndex];
                    var source = entry.Sprite;
                    if (!importedBySource.TryGetValue(source, out var imported))
                    {
                        var filename = "Sprite_" + sourceIndex.ToString("D4") + "_" +
                                       SafeFilename(entry.Key) + ".png";
                        var path = SpriteRoot + "/" + filename;
                        generatedPaths.Add(path);
                        EditorUtility.DisplayProgressBar(
                            "VoidFall sprite bake",
                            "Importing " + entry.Key,
                            entryIndex / (float)snapshot.Entries.Count);
                        WriteSpritePng(path, source);
                        var atlasSafe = IsAtlasSafe(source);
                        ImportSprite(path, source, atlasSafe);
                        imported = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                        if (imported == null)
                            throw new InvalidOperationException("Unity did not import generated sprite " + path + ".");

                        importedBySource.Add(source, imported);
                        if (atlasSafe) safeForAtlas.Add(imported);
                        sourceIndex++;
                    }

                    importedEntries.Add(new ProceduralSpriteCatalogEntry(entry.Key, imported));
                }

                DeleteOrphanedSpriteAssets(generatedPaths);

                WriteCatalog(importedEntries);
                WriteSpriteAtlas(safeForAtlas);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                var errors = ValidatePreparedAssets();
                if (errors.Count > 0)
                    throw new InvalidOperationException(string.Join(Environment.NewLine, errors));

                PreparedContentBuildSetup.ConfigureIfComplete();

                Debug.Log(
                    "VoidFall prepared sprite bake completed: " + importedEntries.Count +
                    " catalog keys, " + importedBySource.Count + " unique sprites, " +
                    safeForAtlas.Count + " atlas-safe sprites.");
            }
            finally
            {
                ReleaseCatalogSnapshot(snapshot);
                EditorUtility.ClearProgressBar();
            }
        }

        public static void BakeAllBatch()
        {
            try
            {
                BakeAll();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static List<string> ValidatePreparedAssets()
        {
            var errors = new List<string>();
            var catalog = AssetDatabase.LoadAssetAtPath<ProceduralSpriteCatalog>(CatalogPath);
            if (catalog == null)
            {
                errors.Add("Missing prepared sprite catalog: " + CatalogPath);
                return errors;
            }
            if (!catalog.IsUsable()) errors.Add("Prepared sprite catalog is invalid: " + CatalogPath);

            var keys = new HashSet<string>();
            foreach (var entry in catalog.Entries)
            {
                if (!keys.Add(entry.Key)) errors.Add("Duplicate prepared sprite key: " + entry.Key);
                if (entry.Sprite == null) continue;
                if (!EditorUtility.IsPersistent(entry.Sprite))
                    errors.Add("Temporary sprite in prepared catalog: " + entry.Key);
                if (entry.Sprite.texture.isReadable)
                    errors.Add("CPU-readable texture in prepared catalog: " + entry.Key);
            }

            foreach (var required in new[]
                     {
                         "fixed|circle",
                         "fixed|operative",
                         "fixed|particle-dot",
                         "gem|2",
                         "arena-rock|5",
                         "arena-vignette|2",
                         "workshop-layer|protocol/1",
                         "projectile-frame|pistol|31",
                     })
            {
                if (!keys.Contains(required)) errors.Add("Missing prepared sprite key: " + required);
            }

            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasPath);
            if (atlas == null) errors.Add("Missing prepared sprite atlas: " + AtlasPath);
            return errors;
        }

        private static void WriteSpritePng(string assetPath, Sprite source)
        {
            var rect = source.rect;
            var width = Mathf.RoundToInt(rect.width);
            var height = Mathf.RoundToInt(rect.height);
            var readable = ReadSpriteTexture(source, width, height);
            try
            {
                File.WriteAllBytes(assetPath, readable.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(readable);
            }
        }

        private static Texture2D ReadSpriteTexture(Sprite source, int width, int height)
        {
            var texture = source.texture;
            var x = Mathf.RoundToInt(source.rect.x);
            var y = Mathf.RoundToInt(source.rect.y);
            var output = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
            {
                name = source.name + "_BakeCopy",
            };

            if (texture.isReadable)
            {
                output.SetPixels32(texture.GetPixels32(0).Crop(texture.width, x, y, width, height));
                output.Apply(false, false);
                return output;
            }

            var renderTexture = RenderTexture.GetTemporary(
                texture.width,
                texture.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(texture, renderTexture);
                RenderTexture.active = renderTexture;
                output.ReadPixels(new Rect(x, y, width, height), 0, 0, false);
                output.Apply(false, false);
                return output;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(renderTexture);
            }
        }

        private static void ImportSprite(string assetPath, Sprite source, bool atlasSafe)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("No TextureImporter for generated sprite " + assetPath + ".");

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = source.pixelsPerUnit;
            importer.spritePivot = new Vector2(
                source.pivot.x / source.rect.width,
                source.pivot.y / source.rect.height);
            importer.spriteBorder = source.border;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = source.texture.filterMode;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.textureCompression = atlasSafe
                ? TextureImporterCompression.Uncompressed
                : TextureImporterCompression.CompressedHQ;

            var standalone = importer.GetPlatformTextureSettings("Standalone");
            standalone.overridden = true;
            standalone.maxTextureSize = 2048;
            standalone.format = atlasSafe
                ? TextureImporterFormat.RGBA32
                : TextureImporterFormat.BC7;
            standalone.textureCompression = atlasSafe
                ? TextureImporterCompression.Uncompressed
                : TextureImporterCompression.CompressedHQ;
            standalone.compressionQuality = 100;
            importer.SetPlatformTextureSettings(standalone);
            importer.SaveAndReimport();
        }

        private static void WriteCatalog(List<ProceduralSpriteCatalogEntry> entries)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ProceduralSpriteCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ProceduralSpriteCatalog>();
                catalog.name = "VoidFall Prepared Procedural Sprite Catalog";
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var serialized = new SerializedObject(catalog);
            serialized.FindProperty("_schema").intValue = ProceduralSpriteCatalog.CurrentSchema;
            var list = serialized.FindProperty("_entries");
            list.arraySize = entries.Count;
            for (var index = 0; index < entries.Count; index++)
            {
                var element = list.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("_key").stringValue = entries[index].Key;
                element.FindPropertyRelative("_sprite").objectReferenceValue = entries[index].Sprite;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static void WriteSpriteAtlas(List<Object> packables)
        {
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasPath);
            if (atlas == null)
            {
                atlas = new SpriteAtlas { name = "VoidFall Prepared Procedural Sprite Atlas" };
                AssetDatabase.CreateAsset(atlas, AtlasPath);
            }

            var previous = atlas.GetPackables();
            if (previous.Length > 0) atlas.Remove(previous);
            atlas.Add(packables.ToArray());
            atlas.SetIncludeInBuild(true);

            var packing = atlas.GetPackingSettings();
            packing.enableRotation = false;
            packing.enableTightPacking = false;
            packing.padding = 4;
            atlas.SetPackingSettings(packing);

            var texture = atlas.GetTextureSettings();
            texture.filterMode = FilterMode.Bilinear;
            texture.generateMipMaps = false;
            texture.readable = false;
            texture.sRGB = true;
            atlas.SetTextureSettings(texture);

            var standalone = atlas.GetPlatformSettings("Standalone");
            standalone.overridden = true;
            standalone.maxTextureSize = 2048;
            standalone.format = TextureImporterFormat.BC7;
            standalone.textureCompression = TextureImporterCompression.CompressedHQ;
            standalone.compressionQuality = 100;
            atlas.SetPlatformSettings(standalone);
            EditorUtility.SetDirty(atlas);
        }

        private static bool IsAtlasSafe(Sprite source)
        {
            return source.texture != null &&
                   source.texture.name.StartsWith("VoidFall_SpriteAtlas_", StringComparison.Ordinal);
        }

        private static ProceduralSpriteCatalog BuildCatalogSnapshot()
        {
            return InvokeFactory("BuildCatalogSnapshot", null) as ProceduralSpriteCatalog;
        }

        private static void ReleaseCatalogSnapshot(ProceduralSpriteCatalog catalog)
        {
            InvokeFactory("ReleaseCatalogSnapshot", new object[] { catalog });
        }

        private static object InvokeFactory(string methodName, object[] arguments)
        {
            var factory = typeof(ProceduralSpriteCatalog).Assembly.GetType(
                "VoidFall.Runtime.ProceduralSpriteFactory",
                true);
            var method = factory.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            if (method == null)
                throw new MissingMethodException(factory.FullName, methodName);
            return method.Invoke(null, arguments);
        }

        private static string SafeFilename(string key)
        {
            var chars = key.ToCharArray();
            for (var index = 0; index < chars.Length; index++)
            {
                var value = chars[index];
                if (!char.IsLetterOrDigit(value) && value != '-' && value != '_') chars[index] = '_';
            }
            var safe = new string(chars);
            return safe.Length <= 80 ? safe : safe.Substring(0, 80);
        }

        private static void DeleteOrphanedSpriteAssets(HashSet<string> generatedPaths)
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteRoot });
            for (var index = 0; index < guids.Length; index++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[index]);
                if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                    generatedPaths.Contains(path)) continue;
                if (!AssetDatabase.DeleteAsset(path))
                    throw new InvalidOperationException("Could not remove orphaned generated sprite: " + path);
            }
        }

        private static void EnsureFolderTree(string folderPath)
        {
            var parts = folderPath.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }

        private static Color32[] Crop(
            this Color32[] source,
            int sourceWidth,
            int x,
            int y,
            int width,
            int height)
        {
            var output = new Color32[width * height];
            for (var row = 0; row < height; row++)
                Array.Copy(source, (y + row) * sourceWidth + x, output, row * width, width);
            return output;
        }
    }
}
