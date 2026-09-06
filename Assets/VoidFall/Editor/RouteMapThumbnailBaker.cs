using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using VoidFall.Core;
using VoidFall.Runtime;

namespace VoidFall.EditorTools
{
    public static class RouteMapThumbnailBaker
    {
        public const int Width = 256;
        public const int Height = 144;

        private const string OutputRoot = "Assets/VoidFall/Resources/VoidFall/RouteThumbnails";

        private static readonly ArenaId[] Arenas =
        {
            ArenaId.Void,
            ArenaId.RedNebula,
            ArenaId.WhiteSakura,
            ArenaId.Hydra,
            ArenaId.MonochromeCourt,
            ArenaId.NullCity,
            ArenaId.EonSea,
            ArenaId.Crascendo,
        };

        [MenuItem("Tools/VoidFall/Bake Route Map Thumbnails")]
        public static void BakeAll()
        {
            EnsureFolderTree(OutputRoot);
            try
            {
                for (var index = 0; index < Arenas.Length; index++)
                {
                    var arena = Arenas[index];
                    EditorUtility.DisplayProgressBar(
                        "VoidFall route thumbnails",
                        "Compositing " + arena,
                        index / (float)Arenas.Length);
                    Bake(arena);
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                foreach (var arena in Arenas) ConfigureImporter(OutputPath(arena));
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                var errors = ValidateAll();
                if (errors.Count > 0)
                    throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
                Debug.Log("VoidFall route map thumbnails baked for all eight arena identities.");
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
                EditorUtility.ClearProgressBar();
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static List<string> ValidateAll()
        {
            var errors = new List<string>();
            foreach (var arena in Arenas)
            {
                var path = OutputPath(arena);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    errors.Add("Missing route thumbnail: " + path);
                    continue;
                }
                if (sprite.texture.width != Width || sprite.texture.height != Height)
                    errors.Add("Unexpected route thumbnail dimensions: " + path);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null || importer.textureType != TextureImporterType.Sprite || importer.isReadable)
                    errors.Add("Invalid route thumbnail importer: " + path);
            }
            return errors;
        }

        private static void Bake(ArenaId arena)
        {
            var platePath = VoidFall.Editor.ArenaAddressableMigration.PlatePath(arena);
            var plate = AssetDatabase.LoadAssetAtPath<ArenaPlateAsset>(platePath);
            if (plate == null || !plate.IsValidFor(arena))
                throw new InvalidOperationException("Bake the prepared arena plate first: " + platePath);

            var basePixels = Sample(plate.BaseSprite);
            var detailPixels = Sample(plate.DetailSprite);
            for (var index = 0; index < basePixels.Length; index++)
                basePixels[index] = Composite(basePixels[index], detailPixels[index]);

            var output = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false)
            {
                name = ArenaCatalogRules.StableId(arena),
            };
            try
            {
                output.SetPixels32(basePixels);
                output.Apply(false, false);
                File.WriteAllBytes(OutputPath(arena), output.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(output);
            }
        }

        private static Color32[] Sample(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
                throw new InvalidOperationException("A prepared arena plate sprite is missing its texture.");

            var crop = CenterCrop16By9(sprite.textureRect);
            var texture = sprite.texture;
            var scale = new Vector2(crop.width / texture.width, crop.height / texture.height);
            var offset = new Vector2(crop.x / texture.width, crop.y / texture.height);
            var temporary = RenderTexture.GetTemporary(
                Width, Height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            var readable = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false);
            try
            {
                Graphics.Blit(texture, temporary, scale, offset);
                RenderTexture.active = temporary;
                readable.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0, false);
                readable.Apply(false, false);
                return readable.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
                UnityEngine.Object.DestroyImmediate(readable);
            }
        }

        private static Rect CenterCrop16By9(Rect source)
        {
            var targetAspect = Width / (float)Height;
            var sourceAspect = source.width / source.height;
            if (sourceAspect > targetAspect)
            {
                var width = source.height * targetAspect;
                source.x += (source.width - width) * 0.5f;
                source.width = width;
            }
            else if (sourceAspect < targetAspect)
            {
                var height = source.width / targetAspect;
                source.y += (source.height - height) * 0.5f;
                source.height = height;
            }
            return source;
        }

        private static Color32 Composite(Color32 background, Color32 foreground)
        {
            var foregroundAlpha = foreground.a / 255f;
            var backgroundAlpha = background.a / 255f;
            var outputAlpha = foregroundAlpha + backgroundAlpha * (1f - foregroundAlpha);
            if (outputAlpha <= 0f) return new Color32(0, 0, 0, 0);
            var backgroundWeight = backgroundAlpha * (1f - foregroundAlpha);
            return new Color32(
                (byte)Mathf.RoundToInt((foreground.r * foregroundAlpha + background.r * backgroundWeight) / outputAlpha),
                (byte)Mathf.RoundToInt((foreground.g * foregroundAlpha + background.g * backgroundWeight) / outputAlpha),
                (byte)Mathf.RoundToInt((foreground.b * foregroundAlpha + background.b * backgroundWeight) / outputAlpha),
                (byte)Mathf.RoundToInt(outputAlpha * 255f));
        }

        private static void ConfigureImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("No TextureImporter for route thumbnail: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = Width;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;

            var standalone = importer.GetPlatformTextureSettings("Standalone");
            standalone.overridden = true;
            standalone.maxTextureSize = Width;
            standalone.format = TextureImporterFormat.BC7;
            standalone.textureCompression = TextureImporterCompression.CompressedHQ;
            standalone.compressionQuality = 100;
            importer.SetPlatformTextureSettings(standalone);
            importer.SaveAndReimport();
        }

        private static string OutputPath(ArenaId arena) =>
            OutputRoot + "/" + ArenaCatalogRules.StableId(arena) + ".png";

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
    }
}
