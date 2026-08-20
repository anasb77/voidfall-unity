using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VoidFall.Runtime.Rendering;

namespace VoidFall.Tests.Editor
{
    public sealed class UrpMigrationTests
    {
        [Test]
        public void Explicit_material_resources_use_the_expected_urp_shaders()
        {
            Assert.That(VoidFallRenderMaterials.DefaultUnlit.shader.name, Is.EqualTo("VoidFall/DefaultUnlit"));
            Assert.That(VoidFallRenderMaterials.AdditiveSprite.shader.name, Is.EqualTo("VoidFall/AdditiveSprite"));
            Assert.That(VoidFallRenderMaterials.ScreenBlend.shader.name, Is.EqualTo("VoidFall/ScreenBlend"));
        }

        [Test]
        public void Filament_instances_are_copies_of_the_explicit_template()
        {
            var filament = VoidFallRenderMaterials.CreateFilamentInstance();
            try
            {
                Assert.That(filament.shader.name, Is.EqualTo("VoidFall/FilamentGas"));
                Assert.That(filament, Is.Not.SameAs(VoidFallRenderMaterials.FilamentTemplate));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(filament);
            }
        }

        [Test]
        public void Graphics_uses_the_named_voidfall_urp_pipeline()
        {
            Assert.That(GraphicsSettings.defaultRenderPipeline, Is.TypeOf<UniversalRenderPipelineAsset>());
            Assert.That(GraphicsSettings.defaultRenderPipeline.name, Is.EqualTo("VoidFallURP"));
        }

        [Test]
        public void Every_quality_level_uses_the_same_voidfall_urp_pipeline()
        {
            var pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.defaultRenderPipeline;
            var originalLevel = QualitySettings.GetQualityLevel();
            try
            {
                for (var level = 0; level < QualitySettings.names.Length; level++)
                {
                    QualitySettings.SetQualityLevel(level, false);
                    Assert.That(QualitySettings.renderPipeline, Is.SameAs(pipeline), QualitySettings.names[level]);
                }
            }
            finally
            {
                QualitySettings.SetQualityLevel(originalLevel, false);
            }
        }

        [Test]
        public void Graphics_uses_the_voidfall_global_settings_asset()
        {
            var expected = AssetDatabase.LoadAssetAtPath<RenderPipelineGlobalSettings>(
                "Assets/VoidFall/Rendering/URP/VoidFallURPGlobalSettings.asset");
            var actual = GraphicsSettings.GetSettingsForRenderPipeline<UniversalRenderPipeline>();

            Assert.That(expected, Is.Not.Null);
            Assert.That(actual, Is.SameAs(expected));
        }

        [Test]
        public void Configure_registers_the_voidfall_global_settings_asset_when_missing()
        {
            var expected = AssetDatabase.LoadAssetAtPath<RenderPipelineGlobalSettings>(
                "Assets/VoidFall/Rendering/URP/VoidFallURPGlobalSettings.asset");
            Assert.That(expected, Is.Not.Null);
            var pipelineType = typeof(UniversalRenderPipeline);
            var original = GraphicsSettings.GetSettingsForRenderPipeline(pipelineType);

            try
            {
                EditorGraphicsSettings.SetRenderPipelineGlobalSettingsAsset(pipelineType, null);
                var setupType = Type.GetType("VoidFall.Editor.UrpPipelineSetup, Assembly-CSharp-Editor");
                Assert.That(setupType, Is.Not.Null);
                setupType.GetMethod("Configure", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);

                var actual = GraphicsSettings.GetSettingsForRenderPipeline(pipelineType);
                Assert.That(actual, Is.SameAs(expected));
            }
            finally
            {
                EditorGraphicsSettings.SetRenderPipelineGlobalSettingsAsset(pipelineType, original);
                Assert.That(GraphicsSettings.GetSettingsForRenderPipeline(pipelineType), Is.SameAs(original));
            }
        }

        [Test]
        public void Urp_render_graph_compatibility_mode_is_disabled()
        {
#pragma warning disable CS0618
            var settings = GraphicsSettings.GetRenderPipelineSettings<RenderGraphSettings>();
            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.enableRenderCompatibilityMode, Is.False);
#pragma warning restore CS0618
        }

        [Test]
        public void Urp_pipeline_has_one_renderer_at_default_index_zero()
        {
            var pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.defaultRenderPipeline;
            var serialized = new SerializedObject(pipeline);
            var rendererList = serialized.FindProperty("m_RendererDataList");
            var defaultRendererIndex = serialized.FindProperty("m_DefaultRendererIndex");

            Assert.That(rendererList, Is.Not.Null);
            Assert.That(rendererList.arraySize, Is.EqualTo(1));
            Assert.That(defaultRendererIndex, Is.Not.Null);
            Assert.That(defaultRendererIndex.intValue, Is.EqualTo(0));
            Assert.That(pipeline.GetRenderer(0), Is.Not.Null);
        }

        [Test]
        public void Urp_default_volume_profile_is_empty()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(
                "Assets/VoidFall/Rendering/URP/VoidFallDefaultVolumeProfile.asset");

            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.components, Is.Empty);
        }

        [Test]
        public void Sample_scene_main_camera_uses_urp_renderer_zero_without_post_processing()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            var cameraObject = GameObject.Find("Main Camera");
            Assert.That(cameraObject, Is.Not.Null);

            var additionalData = cameraObject.GetComponent<UniversalAdditionalCameraData>();
            var pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.defaultRenderPipeline;
            Assert.That(additionalData, Is.Not.Null);
            Assert.That(additionalData.renderPostProcessing, Is.False);
            Assert.That(additionalData.scriptableRenderer, Is.SameAs(pipeline.GetRenderer(0)));
            Assert.That(scene.IsValid(), Is.True);
        }
    }
}
