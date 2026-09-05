using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using VoidFall.Core;
using VoidFall.Runtime;

namespace VoidFall.Editor
{
    public static class NullCityContentBaker
    {
        public const string BasePath = "Assets/VoidFall/Art/NullCity/NullCityBase.png";
        public const string DetailPath = "Assets/VoidFall/Art/NullCity/NullCityDetails.png";

        private const string ArtRoot = "Assets/VoidFall/Art/NullCity";
        private const string UnitRoot = ArtRoot + "/Units";
        private const string PropRoot = ArtRoot + "/Props";
        private const string PackageRoot = "Assets/VoidFall/Generated/ArenaPackages/NullCity";
        private const string PlatePath = PackageRoot + "/Plate.asset";
        private const string VisualPath = PackageRoot + "/NullCityVisuals.asset";
        private const float AuthoredSpritePixelsPerUnit = 4f;

        private sealed class UnitDefinition
        {
            public readonly string Id;
            public readonly int Width;
            public readonly int Height;
            public readonly bool Motherload;

            public UnitDefinition(string id, int width, int height, bool motherload = false)
            {
                Id = id;
                Width = width;
                Height = height;
                Motherload = motherload;
            }
        }

        private static readonly UnitDefinition[] Units =
        {
            new UnitDefinition("null-patrol", 64, 64),
            new UnitDefinition("null-enforcer", 80, 80),
            new UnitDefinition("null-sentinel", 96, 72),
            new UnitDefinition("null-crawler", 80, 80),
            new UnitDefinition("null-volatile", 112, 112),
            new UnitDefinition("null-gunship", 136, 120),
            new UnitDefinition("null-mech", 128, 128),
            new UnitDefinition("null-broodmother", 200, 184),
            new UnitDefinition("null-light-gunship", 112, 96),
            new UnitDefinition("null-interceptor", 80, 80),
            new UnitDefinition("null-marshal", 104, 104),
            new UnitDefinition("null-suppressor", 96, 88),
            new UnitDefinition("null-motherload", 440, 320, true),
        };

        [MenuItem("Tools/VoidFall/Bake And Register Null City")]
        public static void BakeAndRegister()
        {
            Bake();
            ArenaAddressableMigration.MigrateAndConfigure(false);
            var errors = ArenaContentBaker.ValidateAll();
            if (errors.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
            Debug.Log("Null City authored content and three Addressables recipes validated.");
        }

        public static void BakeAndRegisterBatch()
        {
            try { BakeAndRegister(); EditorApplication.Exit(0); }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }

        public static void BuildValidationPlayer()
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Builds/NullCityValidation/VoidFall.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/SampleScene.unity" },
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            Debug.Log("Null City validation build: " + report.summary.result + " / " + report.summary.totalErrors + " errors / " + path);
            EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded && report.summary.totalErrors == 0 ? 0 : 1);
        }

        [MenuItem("Tools/VoidFall/Bake Null City Authored Content")]
        public static void Bake()
        {
            EnsureFolderTree(PackageRoot);
            ImportTexture(BasePath, 1f, 4096);
            ImportTexture(DetailPath, 1f, 4096);

            var visual = AssetDatabase.LoadAssetAtPath<NullCityVisualAsset>(VisualPath);
            if (visual == null)
            {
                visual = ScriptableObject.CreateInstance<NullCityVisualAsset>();
                visual.name = "Null City Authored Visuals";
                AssetDatabase.CreateAsset(visual, VisualPath);
            }

            ConfigureVisualAsset(visual);

            var plate = AssetDatabase.LoadAssetAtPath<ArenaPlateAsset>(PlatePath);
            if (plate == null)
            {
                plate = ScriptableObject.CreateInstance<ArenaPlateAsset>();
                plate.name = "NullCity Prepared Arena Plate";
                AssetDatabase.CreateAsset(plate, PlatePath);
            }

            var baseSprite = RequireSprite(BasePath);
            var detailSprite = RequireSprite(DetailPath);
            var serialized = new SerializedObject(plate);
            serialized.FindProperty("_arena").enumValueIndex = (int)ArenaId.NullCity;
            serialized.FindProperty("_baseSprite").objectReferenceValue = baseSprite;
            serialized.FindProperty("_detailSprite").objectReferenceValue = detailSprite;
            serialized.FindProperty("_width").intValue = ArenaContentBaker.BakeWidth;
            serialized.FindProperty("_height").intValue = ArenaContentBaker.BakeHeight;
            serialized.FindProperty("_detailWidth").intValue = ArenaContentBaker.DetailBakeWidth;
            serialized.FindProperty("_detailHeight").intValue = ArenaContentBaker.DetailBakeHeight;
            serialized.FindProperty("_nullCityVisuals").objectReferenceValue = visual;
            serialized.FindProperty("_schema").intValue = ArenaPlateAsset.CurrentSchema;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(plate);
            AssetDatabase.SaveAssets();
        }

        public static void Validate(ArenaPlateAsset plate, List<string> errors)
        {
            if (plate == null)
            {
                errors.Add("Null City plate is missing: " + PlatePath);
                return;
            }

            var visuals = plate.NullCityVisuals;
            if (visuals == null || !visuals.IsValid())
                errors.Add("Null City plate has no valid authored visual package: " + PlatePath);
            if (AssetDatabase.GetAssetPath(plate.BaseSprite) != BasePath)
                errors.Add("Null City plate does not use its authored base: " + BasePath);
            if (AssetDatabase.GetAssetPath(plate.DetailSprite) != DetailPath)
                errors.Add("Null City plate does not use its authored details: " + DetailPath);

            ValidateDimensions(plate.BaseSprite, ArenaContentBaker.BakeWidth,
                ArenaContentBaker.BakeHeight, "Null City base", errors);
            ValidateDimensions(plate.DetailSprite, ArenaContentBaker.DetailBakeWidth,
                ArenaContentBaker.DetailBakeHeight, "Null City details", errors);

            if (visuals != null)
            {
                for (var index = 0; index < Units.Length; index++)
                    ValidateUnitVisuals(visuals, Units[index], errors);

                ValidateSpriteBounds(visuals.Transit, 190, 80, "Null City transit", errors);
                ValidateSpriteBounds(visuals.HangarClosed, 410, 180,
                    "Null City closed hangar", errors);
                ValidateSpriteBounds(visuals.HangarOpen, 410, 180,
                    "Null City open hangar", errors);
                ValidateSpriteBounds(visuals.Traffic, 58, 48,
                    "Null City traffic", errors);
                ValidateSpriteBounds(visuals.TrafficLockdown, 58, 48,
                    "Null City lockdown traffic", errors);
                ValidateSpriteBounds(visuals.LcdSurveillance, 315, 85,
                    "Null City surveillance LCD", errors);
                ValidateSpriteBounds(visuals.LcdLockdown, 315, 85,
                    "Null City lockdown LCD", errors);

                var marshal = Units[10];
                for (var frame = 0; frame < 4; frame++)
                {
                    var elapsed = frame / NullCityVisualAsset.AnimationFramesPerSecond;
                    ValidateSpriteBounds(
                        visuals.MarshalBracedSprite(elapsed),
                        marshal.Width,
                        marshal.Height,
                        marshal.Id + " braced frame " + frame,
                        errors);
                }
            }

            ValidateAuthoredImports(errors);
        }

        private static void ValidateUnitVisuals(
            NullCityVisualAsset visuals,
            UnitDefinition definition,
            List<string> errors)
        {
            var expected = new Vector2(definition.Width, definition.Height);
            if (visuals.UnitWorldSize(definition.Id) != expected)
                errors.Add("Null City unit has unexpected authored bounds: " + definition.Id + ".");

            for (var frame = 0; frame < 4; frame++)
            {
                var elapsed = frame / NullCityVisualAsset.AnimationFramesPerSecond;
                ValidateSpriteBounds(
                    visuals.UnitSprite(definition.Id, elapsed),
                    definition.Width,
                    definition.Height,
                    definition.Id + " frame " + frame,
                    errors);
            }

            ValidateSpriteBounds(
                visuals.UnitSprite(definition.Id, 0f, true),
                definition.Width,
                definition.Height,
                definition.Id + " hit frame",
                errors);

            if (!definition.Motherload) return;
            for (var frame = 0; frame < 4; frame++)
            {
                var elapsed = frame / NullCityVisualAsset.AnimationFramesPerSecond;
                ValidateSpriteBounds(
                    visuals.UnitSprite(definition.Id, elapsed, false, true),
                    definition.Width,
                    definition.Height,
                    definition.Id + " exposed frame " + frame,
                    errors);
                ValidateSpriteBounds(
                    visuals.UnitSprite(definition.Id, elapsed, false, false, true),
                    definition.Width,
                    definition.Height,
                    definition.Id + " tractor frame " + frame,
                    errors);
                ValidateSpriteBounds(
                    visuals.MotherloadTractorWarningSprite(elapsed),
                    definition.Width,
                    definition.Height,
                    definition.Id + " tractor-warning frame " + frame,
                    errors);
            }

        }

        private static void ConfigureVisualAsset(NullCityVisualAsset visual)
        {
            var serialized = new SerializedObject(visual);
            serialized.FindProperty("_schema").intValue = NullCityVisualAsset.CurrentSchema;
            var unitArray = serialized.FindProperty("_units");
            unitArray.arraySize = Units.Length;

            for (var index = 0; index < Units.Length; index++)
            {
                var definition = Units[index];
                var element = unitArray.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("_id").stringValue = definition.Id;

                var frames = LoadFrames(definition.Id, null);
                SetSprites(element.FindPropertyRelative("_frames"), frames);
                element.FindPropertyRelative("_worldSize").vector2Value = frames[0].bounds.size;
                element.FindPropertyRelative("_hitFrame").objectReferenceValue =
                    ImportAndRequire(UnitRoot + "/" + definition.Id + "-hit.png");

                var exposed = element.FindPropertyRelative("_exposedFrames");
                var tractor = element.FindPropertyRelative("_tractorFrames");
                if (definition.Motherload)
                {
                    SetSprites(exposed, LoadFrames(definition.Id, "exposed"));
                    SetSprites(tractor, LoadFrames(definition.Id, "tractor"));
                }
                else
                {
                    exposed.arraySize = 0;
                    tractor.arraySize = 0;
                }
            }

            serialized.FindProperty("_transit").objectReferenceValue =
                ImportAndRequire(PropRoot + "/Transit.png");
            serialized.FindProperty("_hangarOpen").objectReferenceValue =
                ImportAndRequire(PropRoot + "/HangarOpen.png");
            serialized.FindProperty("_hangarClosed").objectReferenceValue =
                ImportAndRequire(PropRoot + "/HangarClosed.png");
            serialized.FindProperty("_traffic").objectReferenceValue =
                ImportAndRequire(PropRoot + "/Traffic.png");
            serialized.FindProperty("_trafficLockdown").objectReferenceValue =
                ImportAndRequire(PropRoot + "/TrafficLockdown.png");
            serialized.FindProperty("_lcdSurveillance").objectReferenceValue =
                ImportAndRequire(PropRoot + "/LcdSurveillance.png");
            serialized.FindProperty("_lcdLockdown").objectReferenceValue =
                ImportAndRequire(PropRoot + "/LcdLockdown.png");
            SetSprites(serialized.FindProperty("_motherloadTractorWarningFrames"),
                LoadFrames("null-motherload", "tractor-warning"));
            SetSprites(serialized.FindProperty("_marshalBracedFrames"),
                LoadFrames("null-marshal", "braced"));

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(visual);
        }

        private static Sprite[] LoadFrames(string id, string state)
        {
            var frames = new Sprite[4];
            var infix = string.IsNullOrEmpty(state) ? string.Empty : "-" + state;
            for (var frame = 0; frame < frames.Length; frame++)
                frames[frame] = ImportAndRequire(
                    UnitRoot + "/" + id + infix + "-" + frame + ".png");
            return frames;
        }

        private static Sprite ImportAndRequire(string path)
        {
            ImportTexture(path, AuthoredSpritePixelsPerUnit, 2048);
            return RequireSprite(path);
        }

        private static void SetSprites(SerializedProperty property, Sprite[] sprites)
        {
            property.arraySize = sprites.Length;
            for (var index = 0; index < sprites.Length; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = sprites[index];
        }

        private static void ImportTexture(string assetPath, float pixelsPerUnit, int maxTextureSize)
        {
            if (!File.Exists(assetPath))
                throw new InvalidOperationException(
                    "Missing Null City authored source. Run node Tools/NullCity/export-null-city.cjs: " +
                    assetPath);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("No TextureImporter for Null City art: " + assetPath);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(textureSettings);
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.streamingMipmapsPriority = 2;
            importer.isReadable = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = maxTextureSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;

            var standalone = importer.GetPlatformTextureSettings("Standalone");
            standalone.overridden = true;
            standalone.maxTextureSize = maxTextureSize;
            standalone.format = TextureImporterFormat.BC7;
            standalone.textureCompression = TextureImporterCompression.CompressedHQ;
            standalone.compressionQuality = 100;
            importer.SetPlatformTextureSettings(standalone);
            importer.SaveAndReimport();
        }

        private static Sprite RequireSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                throw new InvalidOperationException("Unity did not import Null City sprite: " + path);
            return sprite;
        }

        private static void ValidateDimensions(
            Sprite sprite,
            int width,
            int height,
            string label,
            List<string> errors)
        {
            if (sprite == null)
            {
                errors.Add(label + " sprite is missing.");
                return;
            }
            if (sprite.texture.width != width || sprite.texture.height != height)
                errors.Add(label + " has unexpected dimensions.");
        }

        private static void ValidateSpriteBounds(
            Sprite sprite,
            int expectedWidth,
            int expectedHeight,
            string label,
            List<string> errors)
        {
            if (sprite == null)
            {
                errors.Add(label + " sprite is missing.");
                return;
            }

            var size = sprite.bounds.size;
            if (Mathf.Abs(size.x - expectedWidth) > 0.001f ||
                Mathf.Abs(size.y - expectedHeight) > 0.001f)
            {
                errors.Add(
                    label + " has unexpected authored bounds " + size.x + " x " + size.y +
                    " (expected " + expectedWidth + " x " + expectedHeight + ").");
            }
        }

        private static void ValidateAuthoredImports(List<string> errors)
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot });
            if (guids.Length != 90)
                errors.Add("Null City authored export should contain exactly 90 PNG textures.");

            for (var index = 0; index < guids.Length; index++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[index]);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (texture == null || importer == null)
                {
                    errors.Add("Null City authored texture failed to import: " + path);
                    continue;
                }
                if (texture.isReadable)
                    errors.Add("Null City authored texture kept a CPU copy: " + path);
                if (!importer.mipmapEnabled || !importer.streamingMipmaps)
                    errors.Add("Null City authored texture does not stream mip levels: " + path);
                var textureSettings = new TextureImporterSettings();
                importer.ReadTextureSettings(textureSettings);
                if (textureSettings.spriteMeshType != SpriteMeshType.FullRect)
                    errors.Add("Null City authored texture must use full-rect sprite bounds: " + path);
                var expectedPixelsPerUnit = path == BasePath || path == DetailPath
                    ? 1f
                    : AuthoredSpritePixelsPerUnit;
                if (Mathf.Abs(importer.spritePixelsPerUnit - expectedPixelsPerUnit) > 0.001f)
                    errors.Add(
                        "Null City authored texture has unexpected pixels per unit (expected " +
                        expectedPixelsPerUnit + "): " + path);
            }
        }

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
