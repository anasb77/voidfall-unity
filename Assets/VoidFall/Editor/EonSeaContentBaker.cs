using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using VoidFall.Core;
using VoidFall.Runtime;

namespace VoidFall.Editor
{
    public static class EonSeaContentBaker
    {
        private const string Art = "Assets/VoidFall/Art/EonSea";
        private const string Package = "Assets/VoidFall/Generated/ArenaPackages/EonSea";
        private const string RosterPath = "Assets/VoidFall/Resources/VoidFall/RosterProgressionVisuals.asset";
        private static readonly string[] Kinds = { "mass", "brittle", "wall", "wave" };
        private static readonly string[] Types = { "chaser", "runner", "gunner", "twinGunner", "dasher", "brute", "exploder", "guard", "technician", "mortar", "splitter", "bulwark", "harvester", "carrier" };

        [MenuItem("Tools/VoidFall/Bake Eon Sea And Roster Progression")]
        public static void Bake()
        {
            Directory.CreateDirectory(Package);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var visuals = LoadOrCreate<EonSeaVisualAsset>(Package + "/EonSeaVisuals.asset");
            var data = new SerializedObject(visuals);
            var ground = data.FindProperty("_ground"); ground.arraySize = 4;
            for (var i = 0; i < 4; i++) ground.GetArrayElementAtIndex(i).objectReferenceValue = Import(Art + "/Ground" + i + ".png", 1024, 1024, 1);
            var ice = data.FindProperty("_ice"); ice.arraySize = 32;
            for (var kind = 0; kind < 4; kind++) for (var variant = 0; variant < 8; variant++)
                ice.GetArrayElementAtIndex(kind * 8 + variant).objectReferenceValue = Import(Art + "/Ice-" + Kinds[kind] + variant + ".png", 440, 300, 1);
            data.FindProperty("_slippery").objectReferenceValue = Import(Art + "/Slippery.png", 512, 320, 1);
            data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(visuals);

            var plate = LoadOrCreate<ArenaPlateAsset>(Package + "/Plate.asset");
            data = new SerializedObject(plate);
            data.FindProperty("_arena").enumValueIndex = (int)ArenaId.EonSea;
            data.FindProperty("_baseSprite").objectReferenceValue = Import(Art + "/Base.png", 3840, 2160, 1);
            data.FindProperty("_detailSprite").objectReferenceValue = Import(Art + "/Details.png", 2560, 1440, 1);
            data.FindProperty("_width").intValue = 3840; data.FindProperty("_height").intValue = 2160;
            data.FindProperty("_detailWidth").intValue = 2560; data.FindProperty("_detailHeight").intValue = 1440;
            data.FindProperty("_schema").intValue = ArenaPlateAsset.CurrentSchema;
            data.FindProperty("_eonSeaVisuals").objectReferenceValue = visuals;
            data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(plate);

            var roster = LoadOrCreate<RosterProgressionVisualAsset>(RosterPath);
            data = new SerializedObject(roster); var entries = data.FindProperty("_entries"); entries.arraySize = 68; var index = 0;
            foreach (var type in Types) for (var tier = 1; tier <= 4; tier++) SetRoster(entries.GetArrayElementAtIndex(index++), type, tier, false);
            foreach (var type in new[] { "exploder", "mortar", "gunner" }) for (var tier = 1; tier <= 4; tier++) SetRoster(entries.GetArrayElementAtIndex(index++), type, tier, true);
            data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(roster);
            AssetDatabase.SaveAssets();
        }

        private static void SetRoster(SerializedProperty item, string id, int tier, bool elite)
        {
            item.FindPropertyRelative("Id").stringValue = id;
            item.FindPropertyRelative("Tier").intValue = tier;
            item.FindPropertyRelative("Elite").boolValue = elite;
            item.FindPropertyRelative("Sprite").objectReferenceValue = Import("Assets/VoidFall/Art/RosterProgression/" + (elite ? "elite-" : "shared-") + id + "-" + tier + ".png", 256, 256, 4);
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
            if (!File.Exists(path)) throw new InvalidOperationException("Missing authored Eon Sea asset: " + path);
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
            try { Bake(); ArenaAddressableMigration.MigrateAndConfigure(false); var errors = ArenaContentBaker.ValidateAll(); if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors)); Debug.Log("Eon Sea and 68 roster sprites baked and registered."); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void BuildValidationPlayer()
        {
            VoidFall.EditorTools.BuildScript.BuildWindows();
        }
    }
}
