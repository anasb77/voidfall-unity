using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace VoidFall.EditorTools
{
    public static class DirectorContentBaker
    {
        private const string SpriteRoot = "Assets/VoidFall/Resources/VoidFall/Destroyers";

        [MenuItem("Tools/VoidFall/Bake Director Event Art")]
        public static void Bake()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var file in Directory.GetFiles(SpriteRoot, "*.png", SearchOption.AllDirectories))
            {
                var assetPath = file.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Missing event sprite importer: " + assetPath);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 1;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.isReadable = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 256;
                importer.SaveAndReimport();
                if (AssetDatabase.LoadAssetAtPath<Sprite>(assetPath) == null)
                    throw new InvalidOperationException("Event sprite failed to import: " + assetPath);
            }
            AssetDatabase.SaveAssets();
        }

        public static void BakeBatch()
        {
            try { Bake(); Debug.Log("Director event art imported: 5 approved creatures, 8 poses each."); EditorApplication.Exit(0); }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }

        public static void BuildBaselinePlayer() => BuildScript.BuildWindows();

        public static void BuildValidationPlayer() => BuildScript.BuildWindows();
    }
}
