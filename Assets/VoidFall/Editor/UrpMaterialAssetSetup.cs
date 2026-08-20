using System;
using UnityEditor;
using UnityEngine;

namespace VoidFall.Editor
{
    public static class UrpMaterialAssetSetup
    {
        private const string MaterialFolder = "Assets/VoidFall/Resources/VoidFall/Materials";

        public static void Configure()
        {
            EnsureFolder("Assets/VoidFall/Resources/VoidFall", "Materials");

            ConfigureMaterial(
                MaterialFolder + "/DefaultUnlit.mat",
                "DefaultUnlit",
                "VoidFall/DefaultUnlit");
            ConfigureMaterial(
                MaterialFolder + "/AdditiveSprite.mat",
                "AdditiveSprite",
                "VoidFall/AdditiveSprite");
            ConfigureMaterial(
                MaterialFolder + "/FilamentGas.mat",
                "FilamentGas",
                "VoidFall/FilamentGas");
            ConfigureMaterial(
                "Assets/VoidFall/Resources/VoidFall/BlastWaveScreen.mat",
                "BlastWaveScreen",
                "VoidFall/ScreenBlend");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void ConfigureMaterial(string assetPath, string materialName, string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "Required VoidFall shader is missing: " + shaderName);
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = materialName,
                };
                AssetDatabase.CreateAsset(material, assetPath);
            }
            else
            {
                material.shader = shader;
                material.name = materialName;
            }

            EditorUtility.SetDirty(material);
        }

        private static void EnsureFolder(string parentFolder, string childName)
        {
            var folder = parentFolder + "/" + childName;
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder(parentFolder, childName);
            }
        }
    }
}
