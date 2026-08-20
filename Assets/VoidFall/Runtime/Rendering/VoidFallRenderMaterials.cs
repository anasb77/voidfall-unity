using System;
using UnityEngine;

namespace VoidFall.Runtime.Rendering
{
    /// <summary>
    /// Explicit material resources used by the VoidFall runtime renderer.
    /// Required resources are loaded once and fail loudly when authoring is incomplete.
    /// </summary>
    public static class VoidFallRenderMaterials
    {
        private const string DefaultUnlitPath = "VoidFall/Materials/DefaultUnlit";
        private const string AdditiveSpritePath = "VoidFall/Materials/AdditiveSprite";
        private const string FilamentGasPath = "VoidFall/Materials/FilamentGas";
        private const string ScreenBlendPath = "VoidFall/BlastWaveScreen";

        private static Material _defaultUnlit;
        private static Material _additiveSprite;
        private static Material _filamentTemplate;
        private static Material _screenBlend;

        public static Material DefaultUnlit => _defaultUnlit ??= LoadRequired(DefaultUnlitPath);

        public static Material AdditiveSprite => _additiveSprite ??= LoadRequired(AdditiveSpritePath);

        public static Material FilamentTemplate => _filamentTemplate ??= LoadRequired(FilamentGasPath);

        public static Material ScreenBlend => _screenBlend ??= LoadRequired(ScreenBlendPath);

        public static Material CreateFilamentInstance()
        {
            return new Material(FilamentTemplate);
        }

        private static Material LoadRequired(string resourcePath)
        {
            var material = Resources.Load<Material>(resourcePath);
            if (material == null)
            {
                throw new InvalidOperationException(
                    "Required VoidFall material resource is missing: " + resourcePath);
            }

            return material;
        }
    }
}
