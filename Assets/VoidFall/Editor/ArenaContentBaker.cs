using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using VoidFall.Core;
using VoidFall.Runtime;

namespace VoidFall.Editor
{
    public static class ArenaContentBaker
    {
        public const int BakeWidth = 3021;
        public const int BakeHeight = 1699;

        private const string GeneratedRoot = "Assets/VoidFall/Generated";
        private const string ArenaTextureRoot = GeneratedRoot + "/Arenas";
        private const string LegacyArenaResourceRoot =
            GeneratedRoot + "/Resources/VoidFall/Generated/Arenas";
        private const string ArenaPackageRoot = GeneratedRoot + "/ArenaPackages";

        private static readonly ArenaId[] RequiredArenas =
        {
            ArenaId.Void,
            ArenaId.RedNebula,
            ArenaId.WhiteSakura,
        };

        [MenuItem("Tools/VoidFall/Bake Prepared Arena Content")]
        public static void BakeAll()
        {
            EnsureFolderTree(ArenaTextureRoot);
            EnsureFolderTree(ArenaPackageRoot);
            ArenaPlateFactory.WarmSpecs();

            try
            {
                for (var index = 0; index < RequiredArenas.Length; index++)
                {
                    var arena = RequiredArenas[index];
                    EditorUtility.DisplayProgressBar(
                        "VoidFall arena bake",
                        "Baking " + arena,
                        index / (float)RequiredArenas.Length);
                    BakeArena(arena);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                ArenaAddressableMigration.MigrateAndConfigure(false);

                var errors = ValidateAll();
                if (errors.Count > 0)
                    throw new InvalidOperationException(string.Join(Environment.NewLine, errors));

                PreparedContentBuildSetup.ConfigureIfComplete();

                Debug.Log("VoidFall prepared arena bake completed for " + RequiredArenas.Length + " arenas.");
            }
            finally
            {
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

        public static List<string> ValidateAll()
        {
            var errors = new List<string>();
            foreach (var arena in RequiredArenas)
            {
                var assetPath = PlateAssetPath(arena);
                var asset = AssetDatabase.LoadAssetAtPath<ArenaPlateAsset>(assetPath);
                if (asset == null)
                {
                    errors.Add("Missing prepared arena asset: " + assetPath);
                    continue;
                }

                if (!asset.IsValidFor(arena))
                    errors.Add("Invalid prepared arena asset: " + assetPath);
                if (asset.Width != BakeWidth || asset.Height != BakeHeight)
                    errors.Add("Unexpected prepared arena dimensions: " + assetPath);

                ValidateTexture(asset.BaseSprite, arena + " base", errors);
                ValidateTexture(asset.DetailSprite, arena + " details", errors);
            }
            return errors;
        }

        private static void BakeArena(ArenaId arena)
        {
            var textureFolder = ArenaTextureRoot + "/" + arena;
            var resourceFolder = ArenaPackageRoot + "/" + arena;
            EnsureFolderTree(textureFolder);
            EnsureFolderTree(resourceFolder);

            var basePath = textureFolder + "/Base.png";
            var detailPath = textureFolder + "/Details.png";
            WritePng(basePath, ArenaPlateFactory.BuildBasePixels(arena, BakeWidth, BakeHeight));
            WritePng(detailPath, ArenaPlateFactory.BuildDetailPixels(arena, BakeWidth, BakeHeight));
            ImportArenaTexture(basePath);
            ImportArenaTexture(detailPath);

            var baseSprite = AssetDatabase.LoadAssetAtPath<Sprite>(basePath);
            var detailSprite = AssetDatabase.LoadAssetAtPath<Sprite>(detailPath);
            if (baseSprite == null || detailSprite == null)
                throw new InvalidOperationException("Unity did not import both arena sprites for " + arena + ".");

            var assetPath = ExistingOrTargetPlateAssetPath(arena);
            var asset = AssetDatabase.LoadAssetAtPath<ArenaPlateAsset>(assetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<ArenaPlateAsset>();
                asset.name = arena + " Prepared Arena Plate";
                AssetDatabase.CreateAsset(asset, assetPath);
            }

            var serialized = new SerializedObject(asset);
            serialized.FindProperty("_arena").enumValueIndex = (int)arena;
            serialized.FindProperty("_baseSprite").objectReferenceValue = baseSprite;
            serialized.FindProperty("_detailSprite").objectReferenceValue = detailSprite;
            serialized.FindProperty("_width").intValue = BakeWidth;
            serialized.FindProperty("_height").intValue = BakeHeight;
            serialized.FindProperty("_schema").intValue = ArenaPlateAsset.CurrentSchema;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        private static void WritePng(string assetPath, Color32[] pixels)
        {
            if (pixels == null || pixels.Length != BakeWidth * BakeHeight)
                throw new InvalidOperationException("Arena baker returned an invalid pixel buffer for " + assetPath + ".");

            var texture = new Texture2D(BakeWidth, BakeHeight, TextureFormat.RGBA32, false)
            {
                name = Path.GetFileNameWithoutExtension(assetPath),
            };
            try
            {
                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                File.WriteAllBytes(assetPath, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void ImportArenaTexture(string assetPath)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("No TextureImporter for generated arena texture: " + assetPath);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 1f;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.streamingMipmapsPriority = 2;
            importer.isReadable = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;

            var standalone = importer.GetPlatformTextureSettings("Standalone");
            standalone.overridden = true;
            standalone.maxTextureSize = 4096;
            standalone.format = TextureImporterFormat.BC7;
            standalone.textureCompression = TextureImporterCompression.CompressedHQ;
            standalone.compressionQuality = 100;
            importer.SetPlatformTextureSettings(standalone);
            importer.SaveAndReimport();
        }

        private static void ValidateTexture(Sprite sprite, string label, List<string> errors)
        {
            if (sprite == null)
            {
                errors.Add("Missing generated sprite: " + label);
                return;
            }

            if (sprite.texture.isReadable)
                errors.Add("Generated texture kept a CPU-readable copy: " + label);
            var path = AssetDatabase.GetAssetPath(sprite.texture);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || !importer.mipmapEnabled || !importer.streamingMipmaps)
                errors.Add("Generated texture is not configured for mip streaming: " + label);
        }

        private static string PlateAssetPath(ArenaId arena)
        {
            return ArenaPackageRoot + "/" + arena + "/Plate.asset";
        }

        private static string ExistingOrTargetPlateAssetPath(ArenaId arena)
        {
            var target = PlateAssetPath(arena);
            if (AssetDatabase.LoadMainAssetAtPath(target) != null) return target;
            var legacy = LegacyArenaResourceRoot + "/" + arena + "/Plate.asset";
            return AssetDatabase.LoadMainAssetAtPath(legacy) != null ? legacy : target;
        }

        private static void EnsureFolderTree(string folderPath)
        {
            var parts = folderPath.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }
    }
}
