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
        [TestCase("Sprite_0003_arena-rock_0.png")]
        [TestCase("Sprite_0085_fixed_elite-ring.png")]
        [TestCase("Sprite_0087_fixed_impact-mark.png")]
        public void Baked_silhouettes_have_transparent_corners_and_visible_shape(string file)
        {
            var texture = new Texture2D(2, 2);
            try
            {
                Assert.That(texture.LoadImage(System.IO.File.ReadAllBytes("Assets/VoidFall/Generated/ProceduralSprites/" + file)), Is.True);
                Assert.That(texture.GetPixel(0, 0).a, Is.LessThan(.01f), "A silhouette must not become a filled square.");
                var visible = false;
                foreach (var pixel in texture.GetPixels32()) if (pixel.a > 30) { visible = true; break; }
                Assert.That(visible, Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        [Test]
        public void Fast_nebula_strike_detects_targets_between_frames()
        {
            var distance = typeof(VoidFall.Runtime.VoidFallGameRuntime).GetMethod("NebulaSegmentDistance", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(distance, Is.Not.Null);
            Assert.That((float)distance.Invoke(null, new object[] { new Vector2(50, 4), Vector2.zero, new Vector2(100, 0) }), Is.EqualTo(4f).Within(.001f));
            Assert.That((float)distance.Invoke(null, new object[] { new Vector2(110, 0), Vector2.zero, new Vector2(100, 0) }), Is.EqualTo(10f).Within(.001f));
        }

        [Test]
        public void Nebula_ribbon_preserves_control_path_with_one_continuous_strip()
        {
            var build = typeof(VoidFall.Runtime.VoidFallGameRuntime).GetMethod(
                "BuildNebulaRibbon", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(build, Is.Not.Null, "Red Nebula needs a continuous ribbon mesh.");
            var mesh = new Mesh();
            var points = new[] { new Vector2(0, 0), new Vector2(10, 4), new Vector2(20, -2), new Vector2(30, 0) };
            try
            {
                build.Invoke(null, new object[] { mesh, points, new[] { 4f, 8f, 3f, 4f }, new Vector2(40, 20) });
                var vertices = mesh.vertices;
                Assert.That(vertices.Length, Is.GreaterThan(points.Length * 2));
                Assert.That(mesh.triangles.Length, Is.EqualTo((vertices.Length / 2 - 1) * 6));
                var steps = (vertices.Length / 2 - 1) / (points.Length - 1);
                for (var i = 0; i < points.Length; i++)
                    Assert.That(Vector2.Distance((vertices[i * steps * 2] + vertices[i * steps * 2 + 1]) * .5f,
                        points[i] - new Vector2(20, 10)), Is.LessThan(.001f));
                for (var i = 0; i < vertices.Length; i += 2)
                {
                    Assert.That(float.IsNaN(vertices[i].x) || float.IsInfinity(vertices[i].y), Is.False);
                    Assert.That(Vector3.Distance(vertices[i], vertices[i + 1]), Is.GreaterThan(0));
                    Assert.That(mesh.uv2[i].y, Is.EqualTo(1f));
                    Assert.That(mesh.uv2[i + 1].y, Is.EqualTo(-1f));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void Explicit_material_resources_use_the_expected_urp_shaders()
        {
            Assert.That(VoidFallRenderMaterials.DefaultUnlit.shader.name, Is.EqualTo("VoidFall/DefaultUnlit"));
            Assert.That(VoidFallRenderMaterials.AdditiveSprite.shader.name, Is.EqualTo("VoidFall/AdditiveSprite"));
            Assert.That(VoidFallRenderMaterials.ScreenBlend.shader.name, Is.EqualTo("VoidFall/ScreenBlend"));
        }

        [Test]
        public void Hydra_disintegration_shader_exposes_ordered_damage_controls()
        {
            var shader = Shader.Find("VoidFall/HydraDisintegrate");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            try
            {
                Assert.That(material.HasProperty("_DamageProgress"), Is.True);
                Assert.That(material.HasProperty("_PixelCells"), Is.True);
                Assert.That(material.HasProperty("_ToxicColor"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
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
        public void Urp_default_volume_profile_is_complete_and_visually_neutral()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(
                "Assets/VoidFall/Rendering/URP/VoidFallDefaultVolumeProfile.asset");

            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.components, Is.Not.Empty,
                "URP will rewrite an empty default profile during a build.");
            Assert.That(profile.TryGet(out Bloom bloom), Is.True);
            Assert.That(bloom.intensity.value, Is.EqualTo(1.2f),
                "the neon identity ships with its glow");
            Assert.That(profile.TryGet(out ChromaticAberration chromatic), Is.True);
            Assert.That(chromatic.intensity.value, Is.EqualTo(0.12f));
            Assert.That(profile.TryGet(out ColorAdjustments color), Is.True);
            Assert.That(color.postExposure.value, Is.Zero);
            Assert.That(color.contrast.value, Is.Zero);
            Assert.That(color.colorFilter.value, Is.EqualTo(Color.white));
            Assert.That(color.hueShift.value, Is.Zero);
            Assert.That(color.saturation.value, Is.Zero);
            Assert.That(profile.TryGet(out Tonemapping tonemapping), Is.True);
            Assert.That(tonemapping.mode.value, Is.EqualTo(TonemappingMode.None));
            Assert.That(profile.TryGet(out Vignette vignette), Is.True);
            Assert.That(vignette.intensity.value, Is.Zero);
            Assert.That(profile.TryGet(out WhiteBalance whiteBalance), Is.True);
            Assert.That(whiteBalance.temperature.value, Is.Zero);
            Assert.That(whiteBalance.tint.value, Is.Zero);
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
