using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using VoidFall.Core;
using VoidFall.Runtime;

namespace VoidFall.Editor
{
    public static class ArenaAddressableMigration
    {
        private const string GroupName = "VoidFall Arena Packages";
        private const string PackageRoot = "Assets/VoidFall/Generated/ArenaPackages";
        private const string LegacyRoot =
            "Assets/VoidFall/Generated/Resources/VoidFall/Generated/Arenas";

        private static readonly ArenaId[] CurrentArenas =
        {
            ArenaId.Void,
            ArenaId.RedNebula,
            ArenaId.WhiteSakura,
            ArenaId.Hydra,
            ArenaId.MonochromeCourt,
            ArenaId.NullCity,
        };

        [MenuItem("Tools/VoidFall/Migrate Arenas To Addressables")]
        public static void MigrateAndConfigure()
        {
            MigrateAndConfigure(true);
        }

        public static void MigrateAndConfigure(bool logResult)
        {
            EnsureFolderTree(PackageRoot);
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null)
                throw new InvalidOperationException("Unity could not create Addressables settings.");
            settings.BuildAddressablesWithPlayerBuild =
                AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
            ProjectConfigData.GenerateBuildLayout = true;

            var group = settings.FindGroup(GroupName) ?? settings.CreateGroup(
                GroupName,
                false,
                false,
                false,
                null,
                typeof(BundledAssetGroupSchema),
                typeof(ContentUpdateGroupSchema));
            var bundled = group.GetSchema<BundledAssetGroupSchema>();
            if (bundled != null)
            {
                bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel;
                bundled.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            }

            foreach (var arena in CurrentArenas)
                ConfigureArena(settings, group, arena);

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            PreparedContentBuildSetup.Configure();

            var errors = ValidateAll();
            if (errors.Count > 0)
                throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
            if (logResult)
                Debug.Log("VoidFall arena Addressables migration completed. Three recipes are configured per current arena.");
        }

        [MenuItem("Tools/VoidFall/Build Arena Addressables")]
        public static void BuildContent()
        {
            var errors = ValidateAll();
            if (errors.Count > 0)
                throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            if (!string.IsNullOrEmpty(result.Error))
                throw new InvalidOperationException(result.Error);
            Debug.Log("VoidFall arena Addressables content build completed: " + result.OutputPath);
        }

        public static List<string> ValidateAll()
        {
            var errors = new List<string>();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                errors.Add("Addressables settings are missing.");
                return errors;
            }

            foreach (var arena in CurrentArenas)
            {
                var plate = AssetDatabase.LoadAssetAtPath<ArenaPlateAsset>(PlatePath(arena));
                if (plate == null || !plate.IsValidFor(arena))
                    errors.Add("Missing or invalid arena plate: " + PlatePath(arena));
                if (AssetDatabase.LoadMainAssetAtPath(LegacyPlatePath(arena)) != null)
                    errors.Add("Legacy Resources arena plate still exists: " + LegacyPlatePath(arena));

                for (var recipeIndex = 0; recipeIndex < ArenaCatalogRules.RecipesPerArena; recipeIndex++)
                {
                    var key = new ArenaPackageKey(ArenaCatalogRules.StableId(arena), recipeIndex);
                    var path = RecipePath(arena, recipeIndex);
                    var recipe = AssetDatabase.LoadAssetAtPath<ArenaRecipeAsset>(path);
                    if (recipe == null || !recipe.IsValidFor(key))
                        errors.Add("Missing or invalid arena recipe: " + path);
                    var guid = AssetDatabase.AssetPathToGUID(path);
                    var entry = settings.FindAssetEntry(guid);
                    if (entry == null || entry.address != ArenaCatalogRules.PackageAddress(key))
                        errors.Add("Missing Addressables entry: " + ArenaCatalogRules.PackageAddress(key));
                }
            }
            return errors;
        }

        private static void ConfigureArena(
            AddressableAssetSettings settings,
            AddressableAssetGroup group,
            ArenaId arena)
        {
            var folder = PackageRoot + "/" + arena;
            EnsureFolderTree(folder);
            var oldPath = LegacyPlatePath(arena);
            var newPath = PlatePath(arena);
            var oldAsset = AssetDatabase.LoadMainAssetAtPath(oldPath);
            var newAsset = AssetDatabase.LoadMainAssetAtPath(newPath);
            if (oldAsset != null && newAsset != null)
                throw new InvalidOperationException("Both legacy and migrated plates exist for " + arena + ".");
            if (oldAsset != null)
            {
                var moveError = AssetDatabase.MoveAsset(oldPath, newPath);
                if (!string.IsNullOrEmpty(moveError))
                    throw new InvalidOperationException("Could not migrate " + oldPath + ": " + moveError);
            }

            var plate = AssetDatabase.LoadAssetAtPath<ArenaPlateAsset>(newPath);
            if (plate == null || !plate.IsValidFor(arena))
                throw new InvalidOperationException("Bake a valid plate before migration: " + newPath);

            for (var recipeIndex = 0; recipeIndex < ArenaCatalogRules.RecipesPerArena; recipeIndex++)
            {
                var path = RecipePath(arena, recipeIndex);
                var recipe = AssetDatabase.LoadAssetAtPath<ArenaRecipeAsset>(path);
                if (recipe == null)
                {
                    recipe = ScriptableObject.CreateInstance<ArenaRecipeAsset>();
                    recipe.name = arena + " Recipe " + (recipeIndex + 1);
                    AssetDatabase.CreateAsset(recipe, path);
                }

                var serialized = new SerializedObject(recipe);
                serialized.FindProperty("_schema").intValue = ArenaRecipeAsset.CurrentSchema;
                serialized.FindProperty("_stableArenaId").stringValue = ArenaCatalogRules.StableId(arena);
                serialized.FindProperty("_legacyArena").enumValueIndex = (int)arena;
                serialized.FindProperty("_recipeIndex").intValue = recipeIndex;
                serialized.FindProperty("_plate").objectReferenceValue = plate;
                serialized.FindProperty("_estimatedTextureBytes").longValue = EstimateTextureBytes(plate);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(recipe);

                var guid = AssetDatabase.AssetPathToGUID(path);
                var entry = settings.CreateOrMoveEntry(guid, group, false, false);
                entry.address = ArenaCatalogRules.PackageAddress(
                    new ArenaPackageKey(ArenaCatalogRules.StableId(arena), recipeIndex));
                entry.SetLabel("vf-arena-" + ArenaCatalogRules.StableId(arena), true, true, false);
            }
        }

        private static long EstimateTextureBytes(ArenaPlateAsset plate)
        {
            // BC7 is one byte per pixel. Two current full-screen plates plus a
            // complete mip chain cost approximately 4/3 of the top mip payload.
            return (long)plate.Width * plate.Height * 2L * 4L / 3L;
        }

        public static string PlatePath(ArenaId arena) =>
            PackageRoot + "/" + arena + "/Plate.asset";

        private static string RecipePath(ArenaId arena, int recipeIndex) =>
            PackageRoot + "/" + arena + "/Recipe" + (recipeIndex + 1) + ".asset";

        private static string LegacyPlatePath(ArenaId arena) =>
            LegacyRoot + "/" + arena + "/Plate.asset";

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
