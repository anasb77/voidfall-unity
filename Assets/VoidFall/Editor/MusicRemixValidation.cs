using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VoidFall.UI;

namespace VoidFall.EditorTools
{
    /// <summary>Build and deterministic presentation-only visual QA for the music remix perimeter.</summary>
    public static class MusicRemixValidation
    {
        private const int CaptureWidth = 1280;
        private const int CaptureHeight = 720;
        private const int PerimeterWidth = 1200;
        private const int PerimeterHeight = 640;
        private const int CaptureLayer = 31;
        private const string ShaderName = "UI/VoidFallMusicPerimeter";
        private static readonly float[] SyntheticSpectrum =
        {
            .92f, .78f, .63f, .49f, .36f, .25f, .18f, .14f,
            .22f, .38f, .57f, .76f, .88f, .69f, .47f, .31f,
            .19f, .27f, .44f, .66f, .81f, .61f, .39f, .24f,
        };

        public static void BuildPlayer()
        {
            VoidFall.EditorTools.BuildScript.BuildWindows();
        }

        public static void CapturePerimeter()
        {
            try
            {
                CapturePerimeterPresentation();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void CapturePerimeterPresentation()
        {
            var output = Path.GetFullPath("Logs/MusicRemix/Captures");
            Directory.CreateDirectory(output);
            GameObject cameraObject = null;
            GameObject canvasObject = null;
            GameObject graphicObject = null;
            RenderTexture target = null;
            var previousTarget = RenderTexture.active;

            try
            {
                cameraObject = new GameObject("Music remix presentation QA camera", typeof(Camera))
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    layer = CaptureLayer,
                };
                canvasObject = new GameObject("Music remix presentation QA canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler))
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    layer = CaptureLayer,
                };
                graphicObject = new GameObject("Music perimeter fixture", typeof(RectTransform), typeof(CanvasRenderer))
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    layer = CaptureLayer,
                };
                target = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                {
                    name = "Music remix presentation QA target",
                    hideFlags = HideFlags.HideAndDontSave,
                    antiAliasing = 1,
                };
                target.Create();
                var camera = cameraObject.GetComponent<Camera>();
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.orthographic = true;
                camera.orthographicSize = CaptureHeight * .5f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.012f, .016f, .035f, 1f);
                camera.cullingMask = 1 << CaptureLayer;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 100f;
                camera.targetTexture = target;
                camera.enabled = false;

                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
                var scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(CaptureWidth, CaptureHeight);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0f;

                var rect = graphicObject.GetComponent<RectTransform>();
                rect.SetParent(canvasObject.transform, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(PerimeterWidth, PerimeterHeight);
                var graphic = graphicObject.AddComponent<MusicPerimeterGraphic>();
                graphic.color = Color.white;

                var shader = Shader.Find(ShaderName);
                if (shader == null || graphic.material == null || graphic.material.shader != shader)
                    throw new InvalidOperationException("The actual music perimeter shader could not be loaded: " + ShaderName);

                CaptureVariant(camera, target, graphic, output, "neutral.png", false,
                    .48f, .43f, .38f, .28f, 0, 0, 0f, false, 0f, 1f, .18f);
                CaptureVariant(camera, target, graphic, output, "full-magnet.png", false,
                    .90f, .55f, .34f, .28f, 0, 0, 0f, false, 1f, .72f, .42f);
                CaptureVariant(camera, target, graphic, output, "overclock.png", false,
                    .82f, .67f, .74f, .28f, 3, 6, 1f, false, 0f, 1f, .78f);
                CaptureVariant(camera, target, graphic, output, "overclock-magnet-critical.png", false,
                    .90f, .55f, .34f, .28f, 2, 2, .72f, true, 1f, .58f, .62f);
                CaptureVariant(camera, target, graphic, output, "reduced-motion-magnet.png", true,
                    .90f, .55f, .34f, .28f, 0, 0, 0f, false, 1f, .72f, .42f);

                File.WriteAllText(Path.Combine(output, "presentation-qa.txt"), BuildReport(graphic));
                Debug.Log("MUSIC REMIX PRESENTATION QA complete (synthetic fixture; not live gameplay) path=" + output);
            }
            finally
            {
                RenderTexture.active = previousTarget;
                var camera = cameraObject != null ? cameraObject.GetComponent<Camera>() : null;
                if (camera != null) camera.targetTexture = null;
                if (target != null && target.IsCreated()) target.Release();
                if (graphicObject != null) UnityEngine.Object.DestroyImmediate(graphicObject);
                if (canvasObject != null) UnityEngine.Object.DestroyImmediate(canvasObject);
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (target != null) UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void CaptureVariant(Camera camera, RenderTexture target, MusicPerimeterGraphic graphic,
            string output, string fileName, bool reducedMotion, float bass, float mids, float treble,
            float ambientIntensity, int overclockTier, int overclockStreak, float surge, bool critical,
            float magnetIntensity, float visualDamping, float transient)
        {
            graphic.ResetRun(90421, 2, reducedMotion);
            graphic.SetSpectrum(SyntheticSpectrum);
            if (overclockTier > 0) graphic.NotifyPickup(true);
            for (var frame = 0; frame < 30; frame++)
            {
                graphic.SetPresentation(bass, mids, treble, ambientIntensity, overclockTier, overclockStreak,
                    surge, critical, magnetIntensity, visualDamping, .1f, transient);
            }

            graphic.SetAllDirty();
            Canvas.ForceUpdateCanvases();
            ValidateBoundedMesh(graphic);
            camera.Render();
            WriteTarget(target, Path.Combine(output, fileName));
        }

        private static void ValidateBoundedMesh(MusicPerimeterGraphic graphic)
        {
            var rect = graphic.rectTransform.rect;
            if (!Mathf.Approximately(rect.width, PerimeterWidth) || !Mathf.Approximately(rect.height, PerimeterHeight)
                || rect.width > CaptureWidth || rect.height > CaptureHeight)
                throw new InvalidOperationException("Music perimeter fixture dimensions are invalid or unbounded: " + rect.size);
            var mesh = graphic.canvasRenderer.GetMesh();
            if (mesh == null || mesh.vertexCount == 0 || mesh.vertexCount > graphic.MaximumVertexCount)
                throw new InvalidOperationException("Music perimeter mesh is missing or exceeds its public vertex bound.");
        }

        private static void WriteTarget(RenderTexture target, string path)
        {
            var previousTarget = RenderTexture.active;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            try
            {
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply(false, false);
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousTarget;
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        private static string BuildReport(MusicPerimeterGraphic graphic)
        {
            var report = new StringBuilder();
            report.AppendLine("VoidFall music remix perimeter presentation QA");
            report.AppendLine("Synthetic presentation fixture only; this is not live gameplay or listening approval.");
            report.AppendLine("Resolution: 1280x720; camera-space Canvas; widescreen perimeter mesh: 1200x640.");
            report.AppendLine("Shader: " + ShaderName);
            report.AppendLine("Spectrum fixture (24 bins): " + string.Join(", ", SyntheticSpectrum));
            report.AppendLine("Settling: 30 deterministic SetPresentation calls at 0.1 seconds each per capture.");
            report.AppendLine("Mesh bound: " + graphic.MaximumVertexCount + " vertices maximum.");
            report.AppendLine("Captures: neutral.png, full-magnet.png, overclock.png, overclock-magnet-critical.png, reduced-motion-magnet.png");
            return report.ToString();
        }
    }
}
