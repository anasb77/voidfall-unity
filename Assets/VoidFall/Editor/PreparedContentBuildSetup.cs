using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using VoidFall.Runtime;

namespace VoidFall.Editor
{
    public static class PreparedContentBuildSetup
    {
        private static readonly string[] RequiredAssetPaths =
        {
            "Assets/VoidFall/Generated/Resources/VoidFall/Generated/ProceduralSpriteCatalog.asset",
            "Assets/VoidFall/Generated/Resources/VoidFall/Generated/ProceduralSpriteAtlas.spriteatlas",
        };

        private static readonly string[] LegacyPreloadedArenaPaths =
        {
            "Assets/VoidFall/Generated/Resources/VoidFall/Generated/Arenas/Void/Plate.asset",
            "Assets/VoidFall/Generated/Resources/VoidFall/Generated/Arenas/RedNebula/Plate.asset",
            "Assets/VoidFall/Generated/Resources/VoidFall/Generated/Arenas/WhiteSakura/Plate.asset",
        };

        [MenuItem("Tools/VoidFall/Configure Prepared Content Preload")]
        public static void Configure()
        {
            var required = LoadRequiredAssets(true);
            var preloaded = new List<UnityEngine.Object>(PlayerSettings.GetPreloadedAssets());
            preloaded.RemoveAll(asset => asset is ArenaPlateAsset);
            for (var index = 0; index < LegacyPreloadedArenaPaths.Length; index++)
            {
                var legacy = AssetDatabase.LoadMainAssetAtPath(LegacyPreloadedArenaPaths[index]);
                if (legacy != null) preloaded.Remove(legacy);
            }
            for (var index = 0; index < required.Count; index++)
                if (!preloaded.Contains(required[index])) preloaded.Add(required[index]);
            PlayerSettings.SetPreloadedAssets(preloaded.ToArray());
            AssetDatabase.SaveAssets();
            Debug.Log("VoidFall prepared content registered for splash-phase preload.");
        }

        public static void ConfigureIfComplete()
        {
            var required = LoadRequiredAssets(false);
            if (required.Count == RequiredAssetPaths.Length) Configure();
        }

        public static List<string> ValidateAll()
        {
            var errors = new List<string>();
            errors.AddRange(ArenaContentBaker.ValidateAll());
            errors.AddRange(ArenaAddressableMigration.ValidateAll());
            errors.AddRange(ProceduralSpriteBaker.ValidatePreparedAssets());

            var required = LoadRequiredAssets(false);
            if (required.Count != RequiredAssetPaths.Length)
            {
                for (var index = 0; index < RequiredAssetPaths.Length; index++)
                    if (AssetDatabase.LoadMainAssetAtPath(RequiredAssetPaths[index]) == null)
                        errors.Add("Missing required prepared asset: " + RequiredAssetPaths[index]);
                return errors;
            }

            var preloaded = new List<UnityEngine.Object>(PlayerSettings.GetPreloadedAssets());
            for (var index = 0; index < preloaded.Count; index++)
                if (preloaded[index] is ArenaPlateAsset)
                    errors.Add("Arena plates must be owned by Addressables, not PlayerSettings preload: " +
                               AssetDatabase.GetAssetPath(preloaded[index]));
            for (var index = 0; index < required.Count; index++)
                if (!preloaded.Contains(required[index]))
                    errors.Add("Prepared asset is not registered for splash preload: " +
                               AssetDatabase.GetAssetPath(required[index]));
            return errors;
        }

        private static List<UnityEngine.Object> LoadRequiredAssets(bool throwWhenMissing)
        {
            var assets = new List<UnityEngine.Object>(RequiredAssetPaths.Length);
            for (var index = 0; index < RequiredAssetPaths.Length; index++)
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(RequiredAssetPaths[index]);
                if (asset != null)
                {
                    assets.Add(asset);
                    continue;
                }

                if (throwWhenMissing)
                    throw new InvalidOperationException(
                        "Bake prepared content before configuring preload: " + RequiredAssetPaths[index]);
            }
            return assets;
        }
    }

    public sealed class PreparedContentBuildGate : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            var errors = PreparedContentBuildSetup.ValidateAll();
            if (errors.Count > 0)
                throw new BuildFailedException(
                    "VoidFall prepared-content validation failed:" + Environment.NewLine +
                    string.Join(Environment.NewLine, errors));
        }
    }
}
