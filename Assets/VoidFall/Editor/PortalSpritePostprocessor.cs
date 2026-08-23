using UnityEditor;
using UnityEngine;

namespace VoidFall.EditorTools
{
    /// <summary>
    /// Forces the rift portal frames (Assets/VoidFall/Resources/VoidFall/
    /// Portals) to plain readable textures: the portal composites them
    /// additively over their black background and builds full-frame Sprites
    /// at runtime with Sprite.Create, sidestepping sprite-sheet import
    /// entirely (the editor's automatic slicing kept fragmenting these
    /// frames no matter the importer mode).
    /// </summary>
    public sealed class PortalSpritePostprocessor : AssetPostprocessor
    {
        private const string Folder = "Assets/VoidFall/Resources/VoidFall/Portals";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder, System.StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.spriteImportMode = SpriteImportMode.None;
            importer.mipmapEnabled = false;
            importer.isReadable = true;
            importer.maxTextureSize = 512;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = false;
            importer.wrapMode = TextureWrapMode.Clamp;
        }
    }
}
