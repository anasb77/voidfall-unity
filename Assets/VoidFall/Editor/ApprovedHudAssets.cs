using UnityEditor;
using UnityEngine;

namespace VoidFall.EditorTools
{
    public static class ApprovedHudAssets
    {
        public static void Configure()
        {
            // RawImage icons render down to 8px. Mips retain the thin SVG strokes
            // under minification; uncompressed alpha avoids broken skull/heart edges.
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/VoidFall/Resources/VoidFall/ApprovedHud" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                if (importer.textureType == TextureImporterType.Default && importer.mipmapEnabled &&
                    importer.textureCompression == TextureImporterCompression.Uncompressed && importer.filterMode == FilterMode.Trilinear) continue;
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = true;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Trilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }
        }
    }
}
