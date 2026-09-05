using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VoidFall.Core;
using VoidFall.UI;

namespace VoidFall.EditorTools
{
    /// <summary>Render the actual ceremony views for visual QA without starting or saving a run.</summary>
    public static class RoulettePreviewCapture
    {
        public static void BuildPlayer()
        {
            var output = Path.GetFullPath("../Builds/RoulettePreview/VoidFall.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/SampleScene.unity" },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            var success = report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
            Debug.Log("ROULETTE BUILD " + report.summary.result + " errors=" + report.summary.totalErrors + " path=" + output);
            EditorApplication.Exit(success ? 0 : 1);
        }

        public static void Capture()
        {
            const int width = 1280, height = 820;
            var output = Path.GetFullPath("Logs/RoulettePreview");
            Directory.CreateDirectory(output);
            var host = new GameObject("Roulette QA", typeof(RectTransform), typeof(Canvas));
            var cameraObject = new GameObject("Roulette QA camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            var target = new RenderTexture(width, height, 24);
            try
            {
                camera.transform.position = new Vector3(0, 0, -100);
                camera.orthographic = true;
                camera.orthographicSize = height / 2f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.025f, 0.035f, 0.06f);
                camera.targetTexture = target;
                var canvas = host.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                ((RectTransform)host.transform).sizeDelta = new Vector2(width, height);
                var root = UIBuilder.Stretch(UIBuilder.CreateRect(host.transform, "Roulette"));
                var view = root.gameObject.AddComponent<RouletteView>();
                view.Initialize(null);
                view.Present(new RouletteSession(17, 0, RouletteRules.DefaultTable()), new Rng(987), 180,
                    new RouletteSpinContext { ProtectionsEnabled = true });
                Set(view, "_openElapsed", 2f);
                Invoke(view, "Update");
                CaptureFrame(camera, target, output + "/wheel.png");
                Invoke(view, "OnRaiseStakes");
                CaptureFrame(camera, target, output + "/wager.png");
                view.SetVisible(false);
                var revealRoot = UIBuilder.Stretch(UIBuilder.CreateRect(host.transform, "Prize"));
                var reveal = revealRoot.gameObject.AddComponent<PrizeRevealView>();
                reveal.Initialize(null);
                reveal.Show("ORBIT BLADES +2", "2 ranks applied to Orbit Blades.", RouletteTier.Premium, null);
                Set(reveal, "_revealElapsed", 2f);
                Invoke(reveal, "Update");
                CaptureFrame(camera, target, output + "/reward.png");
                Debug.Log("ROULETTE VISUAL QA " + output);
            }
            finally
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(target);
            }
        }

        private static void CaptureFrame(Camera camera, RenderTexture target, string path)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var graphic in Object.FindObjectsByType<RouletteWheelGraphic>(FindObjectsSortMode.None))
            {
                var mesh = graphic.canvasRenderer.GetMesh();
                if (mesh == null || mesh.vertexCount == 0)
                    throw new System.InvalidOperationException("Roulette graphic has no renderable mesh: " + graphic.name);
            }
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
        }

        private static void Set(object target, string field, object value)
            => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Invoke(object target, string method)
            => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
