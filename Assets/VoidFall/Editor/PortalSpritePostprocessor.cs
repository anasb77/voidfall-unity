using UnityEditor;
using UnityEngine;

namespace VoidFall.EditorTools
{
    /// <summary>
    /// Forces the rift portal frames (Assets/VoidFall/Resources/VoidFall/
    /// Portals) to sprite import settings suitable for additive world
    /// rendering: sprites, no mipmaps, capped at 512 px, non-POT scaling
    /// off so the 2400x1440 frames are not squashed, and no alpha handling
    /// since the frames composite additively over their black background.
    /// </summary>
    public sealed class PortalSpritePostprocessor : AssetPostprocessor
    {
        private const string Folder = "Assets/VoidFall/Resources/VoidFall/Portals";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder, System.StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 512;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.alphaIsTransparency = false;
        }
    }
}
