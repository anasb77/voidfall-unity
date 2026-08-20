using NUnit.Framework;
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
                Object.DestroyImmediate(filament);
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
