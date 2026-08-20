using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Rendering;
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
            var changed = EnsureFolderTree(UrpFolder);
            changed |= EnsureGlobalSettings();

            bool defaultProfileChanged;
            MoveOrCreateDefaultVolumeProfile(out defaultProfileChanged);
            changed |= defaultProfileChanged;

            bool rendererDataChanged;
            var rendererData = EnsureRendererData(out rendererDataChanged);
            changed |= rendererDataChanged;

            bool pipelineChanged;
            var pipeline = EnsurePipeline(rendererData, out pipelineChanged);
            changed |= pipelineChanged;

            if (GraphicsSettings.defaultRenderPipeline != pipeline)
            {
                GraphicsSettings.defaultRenderPipeline = pipeline;
                changed = true;
            }

            changed |= AssignAllQualityLevels(pipeline);
            changed |= ConfigureSampleSceneCamera();

            if (changed)
                AssetDatabase.SaveAssets();
        }

        private static bool EnsureGlobalSettings()
        {
            var renderPipelineType = typeof(UniversalRenderPipeline);
            var globalSettingsType = renderPipelineType.Assembly.GetType(
                "UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings", true);
            var changed = false;

            changed |= MoveAssetIfNeeded(
                "Assets/UniversalRenderPipelineGlobalSettings.asset", GlobalSettingsPath);

            var mainAsset = AssetDatabase.LoadMainAssetAtPath(GlobalSettingsPath);
            var settings = mainAsset as RenderPipelineGlobalSettings;
            if (mainAsset != null && settings == null)
                throw new InvalidOperationException(
                    $"The URP global settings path contains {mainAsset.GetType().FullName}, not {nameof(RenderPipelineGlobalSettings)}.");

            if (settings == null)
            {
                settings = RenderPipelineGlobalSettingsUtils.Create(globalSettingsType, GlobalSettingsPath);
                if (settings == null)
                    throw new InvalidOperationException("Unable to create VoidFall URP global settings.");

                changed = true;
            }

            if (settings.name != "VoidFallURPGlobalSettings")
            {
                settings.name = "VoidFallURPGlobalSettings";
                EditorUtility.SetDirty(settings);
                changed = true;
            }

            if (GraphicsSettings.GetSettingsForRenderPipeline(renderPipelineType) != settings)
            {
                EditorGraphicsSettings.SetRenderPipelineGlobalSettingsAsset(renderPipelineType, settings);
                changed = true;
            }

            return changed;
        }

        private static VolumeProfile MoveOrCreateDefaultVolumeProfile(out bool changed)
        {
            changed = MoveAssetIfNeeded("Assets/DefaultVolumeProfile.asset", DefaultVolumeProfilePath);
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(DefaultVolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "VoidFallDefaultVolumeProfile";
                AssetDatabase.CreateAsset(profile, DefaultVolumeProfilePath);
                changed = true;
            }
            else if (profile.name != "VoidFallDefaultVolumeProfile")
            {
                profile.name = "VoidFallDefaultVolumeProfile";
                EditorUtility.SetDirty(profile);
                changed = true;
            }

            return profile;
        }

        private static UniversalRendererData EnsureRendererData(out bool changed)
        {
            changed = false;
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
                changed = true;
            }

            if (renderer == null)
                throw new InvalidOperationException("Unable to create VoidFall Universal Renderer data.");

            if (renderer.name != "VoidFallUniversalRenderer")
            {
                renderer.name = "VoidFallUniversalRenderer";
                EditorUtility.SetDirty(renderer);
                changed = true;
            }

            return renderer;
        }

        private static UniversalRenderPipelineAsset EnsurePipeline(
            UniversalRendererData rendererData, out bool changed)
        {
            changed = false;
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                pipeline.name = "VoidFallURP";
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
                changed = true;
            }

            var serialized = new SerializedObject(pipeline);
            var renderers = serialized.FindProperty("m_RendererDataList");
            if (renderers.arraySize != 1)
            {
                renderers.arraySize = 1;
                changed = true;
            }

            var rendererReference = renderers.GetArrayElementAtIndex(0);
            if (rendererReference.objectReferenceValue != rendererData)
            {
                rendererReference.objectReferenceValue = rendererData;
                changed = true;
            }

            var defaultRendererIndex = serialized.FindProperty("m_DefaultRendererIndex");
            if (defaultRendererIndex != null && defaultRendererIndex.intValue != 0)
            {
                defaultRendererIndex.intValue = 0;
                changed = true;
            }

            if (serialized.ApplyModifiedPropertiesWithoutUndo())
            {
                EditorUtility.SetDirty(pipeline);
                changed = true;
            }

            if (pipeline.name != "VoidFallURP")
            {
                pipeline.name = "VoidFallURP";
                EditorUtility.SetDirty(pipeline);
                changed = true;
            }

            return pipeline;
        }

        private static bool AssignAllQualityLevels(UniversalRenderPipelineAsset pipeline)
        {
            var changed = false;
            var originalLevel = QualitySettings.GetQualityLevel();
            try
            {
                for (var level = 0; level < QualitySettings.names.Length; level++)
                {
                    QualitySettings.SetQualityLevel(level, false);
                    if (QualitySettings.renderPipeline != pipeline)
                    {
                        QualitySettings.renderPipeline = pipeline;
                        changed = true;
                    }
                }
            }
            finally
            {
                QualitySettings.SetQualityLevel(originalLevel, false);
            }

            return changed;
        }

        private static bool ConfigureSampleSceneCamera()
        {
            var scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
            var camera = GameObject.Find("Main Camera");
            if (camera == null)
                throw new InvalidOperationException("SampleScene is missing Main Camera.");

            var additionalData = camera.GetComponent<UniversalAdditionalCameraData>();
            var changed = false;
            if (additionalData == null)
            {
                additionalData = camera.AddComponent<UniversalAdditionalCameraData>();
                changed = true;
            }

            var serialized = new SerializedObject(additionalData);
            var rendererIndex = serialized.FindProperty("m_RendererIndex");
            if (rendererIndex != null && rendererIndex.intValue != 0)
            {
                additionalData.SetRenderer(0);
                changed = true;
            }

            if (additionalData.renderPostProcessing)
            {
                additionalData.renderPostProcessing = false;
                changed = true;
            }

            if (changed)
                EditorUtility.SetDirty(additionalData);

            var sceneChanged = changed || scene.isDirty;
            if (scene.isDirty)
                EditorSceneManager.SaveScene(scene);

            return sceneChanged;
        }

        private static bool MoveAssetIfNeeded(string sourcePath, string destinationPath)
        {
            if (AssetDatabase.LoadMainAssetAtPath(destinationPath) != null)
                return false;
            if (AssetDatabase.LoadMainAssetAtPath(sourcePath) == null)
                return false;

            var error = AssetDatabase.MoveAsset(sourcePath, destinationPath);
            if (!string.IsNullOrEmpty(error))
                throw new InvalidOperationException($"Failed to move {sourcePath} to {destinationPath}: {error}");

            return true;
        }

        private static bool EnsureFolderTree(string folderPath)
        {
            var changed = false;
            var parts = folderPath.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                    changed = true;
                }
                current = next;
            }

            return changed;
        }
    }
}
