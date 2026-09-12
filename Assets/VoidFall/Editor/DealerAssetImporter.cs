using UnityEditor;
using UnityEngine;
namespace VoidFall.EditorTools
{
    public sealed class DealerAssetImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/VoidFall/Resources/VoidFall/Dealer/")) return;
            var texture = (TextureImporter)assetImporter;
            texture.textureType = TextureImporterType.Default;
            texture.npotScale = TextureImporterNPOTScale.None;
            texture.mipmapEnabled = false; texture.alphaIsTransparency = true;
            texture.wrapMode = TextureWrapMode.Clamp; texture.filterMode = FilterMode.Bilinear;
            texture.maxTextureSize = assetPath.Contains("room-") ? 4096 : 1024;
            texture.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
