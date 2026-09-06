using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VoidFall.EditorTools
{
    public static class JourneyVisualBaker
    {
        private const string SourcePortal = "Assets/VoidFall/Resources/VoidFall/Portals/pipo-gate01a.png";
        private const string NeutralPortal = "Assets/VoidFall/Resources/VoidFall/Portals/Neutral/portal.png";

        [MenuItem("Tools/VoidFall/Bake Journey Visuals")]
        public static void Bake()
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Texture2D output = null;
            try
            {
                if (!source.LoadImage(File.ReadAllBytes(SourcePortal))) throw new InvalidOperationException("Portal source could not be decoded.");
                var pixels = source.GetPixels32();
                var width = source.width / 2;
                var height = source.height / 2;
                var neutral = new Color32[width * height];
                for (var y = 0; y < height; y++)
                    for (var x = 0; x < width; x++)
                    {
                        var red = 0; var green = 0; var blue = 0; var alpha = 0;
                        for (var oy = 0; oy < 2; oy++)
                            for (var ox = 0; ox < 2; ox++)
                            {
                                var p = pixels[(y * 2 + oy) * source.width + x * 2 + ox];
                                red += p.r; green += p.g; blue += p.b; alpha += p.a;
                            }
                        var light = (byte)(Mathf.Max(red, Mathf.Max(green, blue)) / 4);
                        neutral[y * width + x] = new Color32(light, light, light, (byte)(alpha / 4));
                    }
                output = new Texture2D(width, height, TextureFormat.RGBA32, false);
                output.SetPixels32(neutral); output.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(NeutralPortal));
                File.WriteAllBytes(NeutralPortal, output.EncodeToPNG());
                AssetDatabase.ImportAsset(NeutralPortal, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(NeutralPortal);
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
                RouteMapThumbnailBaker.BakeAll();
                AssetDatabase.SaveAssets();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                if (output != null) UnityEngine.Object.DestroyImmediate(output);
            }
        }

        public static void BakeBatch()
        {
            try { Bake(); Debug.Log("Journey visuals baked."); EditorApplication.Exit(0); }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }
    }
}
