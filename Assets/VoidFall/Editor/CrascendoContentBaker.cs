using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using VoidFall.Core;
using VoidFall.Runtime;

namespace VoidFall.Editor
{
    public static class CrascendoContentBaker
    {
        private const string Art = "Assets/VoidFall/Art/Crascendo";
        private const string Package = "Assets/VoidFall/Generated/ArenaPackages/Crascendo";
        [MenuItem("Tools/VoidFall/Bake Crascendo")]
        public static void Bake()
        {
            Directory.CreateDirectory(Package);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var visuals = LoadOrCreate<CrascendoVisualAsset>(Package + "/CrascendoVisuals.asset");
            var data = new SerializedObject(visuals);
            var ground = data.FindProperty("_ground"); ground.arraySize = 3;
            for (var i = 0; i < 3; i++) ground.GetArrayElementAtIndex(i).objectReferenceValue = Import(Art + "/Ground" + i + ".png", 1024, 1024, 1);
            var wash = data.FindProperty("_wash"); wash.arraySize = 3;
            for (var i = 0; i < 3; i++) wash.GetArrayElementAtIndex(i).objectReferenceValue = Import(Art + "/Wash" + i + ".png", 1600, 900, 1);
            data.FindProperty("_tears").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(Art + "/Tears.json");
            data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(visuals);

            var plate = LoadOrCreate<ArenaPlateAsset>(Package + "/Plate.asset");
            data = new SerializedObject(plate);
            data.FindProperty("_arena").enumValueIndex = (int)ArenaId.Crascendo;
            data.FindProperty("_baseSprite").objectReferenceValue = Import(Art + "/Base.png", 3840, 2160, 1);
            data.FindProperty("_detailSprite").objectReferenceValue = Import(Art + "/Details.png", 2560, 1440, 1);
            data.FindProperty("_width").intValue = 3840; data.FindProperty("_height").intValue = 2160;
            data.FindProperty("_detailWidth").intValue = 2560; data.FindProperty("_detailHeight").intValue = 1440;
            data.FindProperty("_schema").intValue = ArenaPlateAsset.CurrentSchema;
            data.FindProperty("_crascendoVisuals").objectReferenceValue = visuals;
            data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(plate);

            AssetDatabase.SaveAssets();
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            Directory.CreateDirectory(Path.GetDirectoryName(path)); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); return asset;
        }

        private static Sprite Import(string path, int width, int height, float ppu)
        {
            if (!File.Exists(path)) throw new InvalidOperationException("Missing authored Crascendo asset: " + path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.GetSourceTextureWidthAndHeight(out var sourceWidth, out var sourceHeight);
            if (sourceWidth != width || sourceHeight != height) throw new InvalidOperationException("Unexpected authored dimensions: " + path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu; importer.alphaIsTransparency = true; importer.isReadable = false;
            importer.mipmapEnabled = true; importer.streamingMipmaps = true; importer.maxTextureSize = 4096;
            importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None; importer.textureCompression = TextureImporterCompression.CompressedHQ;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings); settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
            var platform = importer.GetPlatformTextureSettings("Standalone"); platform.overridden = true; platform.maxTextureSize = 4096; platform.format = TextureImporterFormat.BC7; importer.SetPlatformTextureSettings(platform);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? throw new InvalidOperationException("Sprite import failed: " + path);
        }

        public static void BakeBatch()
        {
            try { Bake(); ArenaAddressableMigration.MigrateAndConfigure(false); var errors = ArenaContentBaker.ValidateAll(); if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors)); Debug.Log("Crascendo palettes baked and registered."); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void BuildValidationPlayer()
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Builds/CrascendoValidation/VoidFall.exe")); Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { "Assets/Scenes/SampleScene.unity" }, locationPathName = path, target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
            Debug.Log("Crascendo build: " + report.summary.result + " / " + report.summary.totalErrors + " errors / " + path);
            EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded && report.summary.totalErrors == 0 ? 0 : 1);
        }
    }
}
