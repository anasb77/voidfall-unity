using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VoidFall.Core;
using VoidFall.Runtime;

namespace VoidFall.Editor
{
    public static class ApprovedMapAssetMigration
    {
        public const string AssetRoot = "Assets/VoidFall/Generated/ApprovedMaps";
        private const string LegacyRoot = "Assets/VoidFall/Resources/VoidFall/ApprovedMaps";
        private const string ManifestPath = "Tools/ApprovedMaps/manifest.json";
        private static readonly ArenaId[] Arenas = { ArenaId.Hydra, ArenaId.NullCity, ArenaId.MonochromeCourt };
        [Serializable] private sealed class ManifestEntry { public string name; public float ppu; }
        [Serializable] private sealed class Manifest { public ManifestEntry[] entries; }

        [MenuItem("Tools/VoidFall/Migrate Approved Maps To Arena Packages")]
        public static void MigrateAndConfigure()
        {
            if (AssetDatabase.IsValidFolder(LegacyRoot))
            {
                if (AssetDatabase.IsValidFolder(AssetRoot))
                    throw new InvalidOperationException("Both approved-map roots exist; resolve the duplicate before migration.");
                var error = AssetDatabase.MoveAsset(LegacyRoot, AssetRoot);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            }
            if (!AssetDatabase.IsValidFolder(AssetRoot)) throw new InvalidOperationException("Export approved maps first.");
            var manifest = ReadManifest();
            foreach (var arena in Arenas)
            {
                var expected = manifest.Where(entry => Owner(entry.name) == arena).ToArray();
                var path = CatalogPath(arena);
                var catalog = AssetDatabase.LoadAssetAtPath<ApprovedMapVisualAsset>(path);
                if (catalog == null)
                {
                    catalog = ScriptableObject.CreateInstance<ApprovedMapVisualAsset>();
                    catalog.name = arena + " Approved Maps";
                    AssetDatabase.CreateAsset(catalog, path);
                }
                var serialized = new SerializedObject(catalog);
                serialized.FindProperty("_arena").enumValueIndex = (int)arena;
                var entries = serialized.FindProperty("_entries");
                entries.arraySize = expected.Length;
                for (var i = 0; i < expected.Length; i++)
                {
                    var spritePath = AssetRoot + "/" + expected[i].name + ".png";
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                    if (sprite == null) throw new InvalidOperationException("Missing approved sprite: " + spritePath);
                    entries.GetArrayElementAtIndex(i).FindPropertyRelative("Id").stringValue = expected[i].name;
                    entries.GetArrayElementAtIndex(i).FindPropertyRelative("Sprite").objectReferenceValue = sprite;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(catalog);
                var plate = AssetDatabase.LoadAssetAtPath<ArenaPlateAsset>(ArenaAddressableMigration.PlatePath(arena));
                if (plate == null) throw new InvalidOperationException("Missing arena plate: " + arena);
                var plateData = new SerializedObject(plate);
                plateData.FindProperty("_approvedMapVisuals").objectReferenceValue = catalog;
                plateData.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(plate);
            }
            AssetDatabase.SaveAssets();
            var errors = ValidateAll();
            if (errors.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
            Debug.Log("VoidFall approved maps now belong to their three arena packages; sprite GUIDs and pixels are preserved.");
        }

        public static List<string> ValidateAll()
        {
            var errors = new List<string>();
            if (Directory.Exists(LegacyRoot)) errors.Add("Approved map art must not remain in Resources: " + LegacyRoot);
            var manifest = ReadManifest();
            foreach (var arena in Arenas)
            {
                var catalog = AssetDatabase.LoadAssetAtPath<ApprovedMapVisualAsset>(CatalogPath(arena));
                var plate = AssetDatabase.LoadAssetAtPath<ArenaPlateAsset>(ArenaAddressableMigration.PlatePath(arena));
                if (catalog == null || !catalog.IsValidFor(arena) || plate == null || plate.ApprovedMapVisuals != catalog)
                {
                    errors.Add("Missing, invalid or unattached approved map catalogue: " + arena);
                    continue;
                }
                var expected = manifest.Where(entry => Owner(entry.name) == arena).ToArray();
                if (catalog.Entries.Count != expected.Length) errors.Add("Approved map entry count differs from manifest: " + arena);
                foreach (var entry in expected)
                {
                    var sprite = catalog.Find(entry.name);
                    var path = AssetRoot + "/" + entry.name + ".png";
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    var desktop = importer?.GetPlatformTextureSettings("Standalone");
                    if (desktop == null || !desktop.overridden || desktop.format != TextureImporterFormat.BC7)
                        errors.Add("Approved map desktop import must use BC7: " + entry.name);
                    if (sprite == null || AssetDatabase.GetAssetPath(sprite) != path)
                        errors.Add("Missing or misplaced approved map sprite: " + entry.name);
                    else if (Mathf.Abs(sprite.pixelsPerUnit - entry.ppu) > .001f)
                        errors.Add("Approved map world scale differs from manifest: " + entry.name);
                }
            }
            return errors;
        }

        private static ManifestEntry[] ReadManifest() => JsonUtility.FromJson<Manifest>(
            "{\"entries\":" + File.ReadAllText(ManifestPath) + "}").entries;

        private static ArenaId Owner(string id)
        {
            if (!ApprovedMapVisualAsset.TryGetOwner(id, out var arena))
                throw new InvalidOperationException("Approved map has no declared arena owner: " + id);
            return arena;
        }

        public static string CatalogPath(ArenaId arena) =>
            "Assets/VoidFall/Generated/ArenaPackages/" + arena + "/ApprovedMaps.asset";
    }
}
