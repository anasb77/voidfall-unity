using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VoidFall.Editor
{
    public static class UrpPipelineSetup
    {
        private const string UrpFolder = "Assets/VoidFall/Rendering/URP";
        private const string RendererPath = UrpFolder + "/VoidFallUniversalRenderer.asset";
        private const string PipelinePath = UrpFolder + "/VoidFallURP.asset";
        private const string GlobalSettingsPath = UrpFolder + "/VoidFallURPGlobalSettings.asset";
        private const string DefaultVolumeProfilePath = UrpFolder + "/VoidFallDefaultVolumeProfile.asset";
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

        public static void Configure()
        {
            EnsureFolderTree(UrpFolder);

            MoveAssetIfNeeded("Assets/UniversalRenderPipelineGlobalSettings.asset", GlobalSettingsPath);
            var defaultProfile = MoveOrCreateDefaultVolumeProfile();
            if (defaultProfile != null)
                EditorUtility.SetDirty(defaultProfile);

            var rendererData = EnsureRendererData();
            var pipeline = EnsurePipeline(rendererData);

            if (GraphicsSettings.defaultRenderPipeline != pipeline)
                GraphicsSettings.defaultRenderPipeline = pipeline;

            AssignAllQualityLevels(pipeline);
            ConfigureSampleSceneCamera(pipeline);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static VolumeProfile MoveOrCreateDefaultVolumeProfile()
        {
            MoveAssetIfNeeded("Assets/DefaultVolumeProfile.asset", DefaultVolumeProfilePath);
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(DefaultVolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "VoidFallDefaultVolumeProfile";
                AssetDatabase.CreateAsset(profile, DefaultVolumeProfilePath);
            }
            else if (profile.name != "VoidFallDefaultVolumeProfile")
            {
                profile.name = "VoidFallDefaultVolumeProfile";
                EditorUtility.SetDirty(profile);
            }

            return profile;
        }

        private static UniversalRendererData EnsureRendererData()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                var createMethod = typeof(UniversalRenderPipelineAsset).GetMethod(
                    "CreateRendererAsset",
                    BindingFlags.Static | BindingFlags.NonPublic);
                if (createMethod == null)
                    throw new MissingMethodException(typeof(UniversalRenderPipelineAsset).FullName, "CreateRendererAsset");

                renderer = (UniversalRendererData)createMethod.Invoke(
                    null,
                    new object[] { RendererPath, RendererType.UniversalRenderer, false, "Renderer" });
            }

            if (renderer == null)
                throw new InvalidOperationException("Unable to create VoidFall Universal Renderer data.");

            if (renderer.name != "VoidFallUniversalRenderer")
            {
                renderer.name = "VoidFallUniversalRenderer";
                EditorUtility.SetDirty(renderer);
            }

            return renderer;
        }

        private static UniversalRenderPipelineAsset EnsurePipeline(UniversalRendererData rendererData)
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                pipeline.name = "VoidFallURP";
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            var serialized = new SerializedObject(pipeline);
            var renderers = serialized.FindProperty("m_RendererDataList");
            if (renderers.arraySize != 1)
            {
                renderers.arraySize = 1;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                serialized.Update();
            }

            var rendererReference = renderers.GetArrayElementAtIndex(0);
            if (rendererReference.objectReferenceValue != rendererData)
                rendererReference.objectReferenceValue = rendererData;

            var defaultRendererIndex = serialized.FindProperty("m_DefaultRendererIndex");
            if (defaultRendererIndex != null && defaultRendererIndex.intValue != 0)
                defaultRendererIndex.intValue = 0;

            if (serialized.ApplyModifiedPropertiesWithoutUndo())
                EditorUtility.SetDirty(pipeline);

            if (pipeline.name != "VoidFallURP")
            {
                pipeline.name = "VoidFallURP";
                EditorUtility.SetDirty(pipeline);
            }

            return pipeline;
        }

        private static void AssignAllQualityLevels(UniversalRenderPipelineAsset pipeline)
        {
            var originalLevel = QualitySettings.GetQualityLevel();
            try
            {
                for (var level = 0; level < QualitySettings.names.Length; level++)
                {
                    QualitySettings.SetQualityLevel(level, false);
                    if (QualitySettings.renderPipeline != pipeline)
                        QualitySettings.renderPipeline = pipeline;
                }
            }
            finally
            {
                QualitySettings.SetQualityLevel(originalLevel, false);
            }
        }

        private static void ConfigureSampleSceneCamera(UniversalRenderPipelineAsset pipeline)
        {
            var scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
            var camera = GameObject.Find("Main Camera");
            if (camera == null)
                throw new InvalidOperationException("SampleScene is missing Main Camera.");

            var additionalData = camera.GetComponent<UniversalAdditionalCameraData>();
            if (additionalData == null)
                additionalData = camera.AddComponent<UniversalAdditionalCameraData>();

            additionalData.SetRenderer(0);
            additionalData.renderPostProcessing = false;
            EditorUtility.SetDirty(additionalData);

            if (scene.isDirty)
                EditorSceneManager.SaveScene(scene);
        }

        private static void MoveAssetIfNeeded(string sourcePath, string destinationPath)
        {
            if (AssetDatabase.LoadMainAssetAtPath(destinationPath) != null)
                return;
            if (AssetDatabase.LoadMainAssetAtPath(sourcePath) == null)
                return;

            var error = AssetDatabase.MoveAsset(sourcePath, destinationPath);
            if (!string.IsNullOrEmpty(error))
                throw new InvalidOperationException($"Failed to move {sourcePath} to {destinationPath}: {error}");
        }

        private static void EnsureFolderTree(string folderPath)
        {
            var parts = folderPath.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
