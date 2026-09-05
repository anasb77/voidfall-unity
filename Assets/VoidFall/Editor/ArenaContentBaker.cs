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
        public const int BakeWidth = 3840;
        public const int BakeHeight = 2160;
        public const int DetailBakeWidth = 2560;
        public const int DetailBakeHeight = 1440;

        private const string GeneratedRoot = "Assets/VoidFall/Generated";
        private const string ArenaTextureRoot = GeneratedRoot + "/Arenas";
        private const string LegacyArenaResourceRoot =
            GeneratedRoot + "/Resources/VoidFall/Generated/Arenas";
        private const string ArenaPackageRoot = GeneratedRoot + "/ArenaPackages";
        private const string HydraBasePath = "Assets/VoidFall/Art/Hydra/HydraBase.png";
        private const string HydraDetailPath = "Assets/VoidFall/Art/Hydra/HydraDetails.png";
        private const string HydraBossPath = "Assets/VoidFall/Resources/VoidFall/Hydra/HydraPrime.png";

        private static readonly ArenaId[] RequiredArenas =
        {
            ArenaId.Void,
            ArenaId.RedNebula,
            ArenaId.WhiteSakura,
            ArenaId.Hydra,
            ArenaId.MonochromeCourt,
            ArenaId.NullCity,
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
                if (asset.DetailWidth != DetailBakeWidth || asset.DetailHeight != DetailBakeHeight)
                    errors.Add("Unexpected prepared arena detail dimensions: " + assetPath);

                ValidateTexture(asset.BaseSprite, arena + " base", errors);
                ValidateTexture(asset.DetailSprite, arena + " details", errors);
                if (arena == ArenaId.NullCity) NullCityContentBaker.Validate(asset, errors);
            }
            return errors;
        }

        private static void BakeArena(ArenaId arena)
        {
            if (arena == ArenaId.NullCity)
            {
                NullCityContentBaker.Bake();
                return;
            }

            var textureFolder = ArenaTextureRoot + "/" + arena;
            var resourceFolder = ArenaPackageRoot + "/" + arena;
            if (arena != ArenaId.Hydra) EnsureFolderTree(textureFolder);
            EnsureFolderTree(resourceFolder);

            var authoredHydra = arena == ArenaId.Hydra;
            var basePath = authoredHydra ? HydraBasePath : textureFolder + "/Base.png";
            var detailPath = authoredHydra ? HydraDetailPath : textureFolder + "/Details.png";
            if (authoredHydra)
            {
                if (!File.Exists(basePath) || !File.Exists(detailPath) || !File.Exists(HydraBossPath))
                    throw new InvalidOperationException("Hydra authored art is incomplete. Re-render the approved reference layers.");
                if (AssetDatabase.IsValidFolder(textureFolder) && !AssetDatabase.DeleteAsset(textureFolder))
                    throw new InvalidOperationException("Could not remove obsolete procedural Hydra art: " + textureFolder);
            }
            else
            {
                WritePng(basePath, BakeWidth, BakeHeight,
                    ArenaPlateFactory.BuildBasePixels(arena, BakeWidth, BakeHeight));
                WritePng(detailPath, DetailBakeWidth, DetailBakeHeight,
                    ArenaPlateFactory.BuildDetailPixels(arena, DetailBakeWidth, DetailBakeHeight));
            }
            ImportArenaTexture(basePath);
            ImportArenaTexture(detailPath);
            if (authoredHydra) ImportHydraBossTexture();

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
            serialized.FindProperty("_detailWidth").intValue = DetailBakeWidth;
            serialized.FindProperty("_detailHeight").intValue = DetailBakeHeight;
            serialized.FindProperty("_schema").intValue = ArenaPlateAsset.CurrentSchema;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        private static void WritePng(string assetPath, int width, int height, Color32[] pixels)
        {
            if (pixels == null || pixels.Length != width * height)
                throw new InvalidOperationException("Arena baker returned an invalid pixel buffer for " + assetPath + ".");

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
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

        private static void ImportHydraBossTexture()
        {
            AssetDatabase.ImportAsset(HydraBossPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(HydraBossPath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("No TextureImporter for Hydra Prime art: " + HydraBossPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 1024f;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            var standalone = importer.GetPlatformTextureSettings("Standalone");
            standalone.overridden = true;
            standalone.maxTextureSize = 1024;
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
