using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VoidFall.EditorTools
{
    public sealed class ApprovedMapAssetImporter : AssetPostprocessor
    {
        [Serializable] private sealed class Entry { public string name; public float ppu; }
        [Serializable] private sealed class Manifest { public Entry[] entries; }
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/VoidFall/Generated/ApprovedMaps/", StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            var name = Path.GetFileNameWithoutExtension(assetPath);
            var ppu = name == "hydra-base" ? 2f : 4f;
            var manifest = JsonUtility.FromJson<Manifest>("{\"entries\":" + File.ReadAllText("Tools/ApprovedMaps/manifest.json") + "}");
            foreach (var entry in manifest.entries) if (entry.name == name) ppu = entry.ppu;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 8192;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = false;
            // Preserve original PNGs, world scale and mips. Desktop BC7 keeps
            // RGBA at eight bits per pixel instead of uncompressed RGBA32.
            var desktop = importer.GetPlatformTextureSettings("Standalone");
            desktop.overridden = true;
            desktop.maxTextureSize = 8192;
            desktop.format = TextureImporterFormat.BC7;
            desktop.textureCompression = TextureImporterCompression.CompressedHQ;
            desktop.compressionQuality = 100;
            desktop.crunchedCompression = false;
            importer.SetPlatformTextureSettings(desktop);
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
        }
        public static void Bake()
        {
            foreach (var path in Directory.GetFiles("Assets/VoidFall/Generated/ApprovedMaps", "*.png"))
                AssetDatabase.ImportAsset(path.Replace('\\','/'), ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            VoidFall.Editor.ApprovedMapAssetMigration.MigrateAndConfigure();
            VoidFall.Editor.ArenaAddressableMigration.MigrateAndConfigure(false);
        }
    }
}
