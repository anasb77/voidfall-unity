using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VoidFall.Core;
using VoidFall.Persistence;
using VoidFall.Runtime;
using VoidFall.Runtime.Rendering;
using VoidFall.UI;

namespace VoidFall.EditorTools
{
    /// <summary>Captures the actual Workshop view without loading or writing a player profile.</summary>
    public static class WorkshopFormsCapture
    {
        public static void Capture()
        {
            try
            {
                var catalog = Resources.Load<ProceduralSpriteCatalog>("VoidFall/Generated/ProceduralSpriteCatalog");
                var factory = typeof(PlayerFramePreview).Assembly.GetType("VoidFall.Runtime.ProceduralSpriteFactory");
                if (!(bool)factory.GetMethod("InstallBakedCatalog", BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new object[] { catalog }))
                    throw new InvalidOperationException("Workshop capture requires the prepared sprite catalogue.");
                CaptureAt(1280, 720, false);
                CaptureAt(1280, 720, true);
                CaptureAt(1024, 768, true);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void CaptureAt(int width, int height, bool unlocked)
        {
            var host = new GameObject("Workshop QA", typeof(RectTransform), typeof(Canvas));
            var cameraObject = new GameObject("Workshop QA camera", typeof(Camera));
            var target = new RenderTexture(width, height, 24);
            var previous = RenderTexture.active;
            Texture2D image = null;
            try
            {
                var referenceWidth = width * 900f / height;
                var camera = cameraObject.GetComponent<Camera>();
                camera.transform.position = new Vector3(0, 0, -100);
                camera.orthographic = true;
                camera.orthographicSize = 450f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.02f, .03f, .05f);
                camera.targetTexture = target;
                var canvas = host.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                ((RectTransform)host.transform).sizeDelta = new Vector2(referenceWidth, 900f);
                var root = UIBuilder.Stretch(UIBuilder.CreateRect(host.transform, "Workshop"));
                var view = root.gameObject.AddComponent<WorkshopView>();
                view.Initialize(null);
                var profile = SaveStore.CreateDefault();
                if (unlocked)
                {
                    profile.unlockedForms = new[] { PlayerForms.DefaultId, PlayerForms.DasherId, PlayerForms.BruteId };
                    profile.form = PlayerForms.DasherId;
                }
                profile.parts = 500;
                var controller = new WorkshopController(new PreviewBridge());
                var order = new[] { "integrity", "power", "mobility", "recovery", "magnet", "precision", "arsenal", "protocol" };
                view.Populate(profile.parts, controller.BuildRows(order, profile.parts, profile.workshop));
                view.PopulateForms(controller.BuildForms(profile));
                var preview = view.PreviewStage.gameObject.AddComponent<PlayerFramePreview>();
                preview.Bind(_ => 0, () => true);
                typeof(PlayerFramePreview).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(preview, null);
                typeof(PlayerFramePreview).Assembly.GetType("VoidFall.Runtime.ProceduralSpriteFactory")
                    .GetMethod("FlushAtlas", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
                view.SetVisible(true);
                var panel = (RectTransform)typeof(WorkshopView).GetField("_panel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                panel.sizeDelta = new Vector2(Mathf.Min(1188f, referenceWidth * .94f), 846f);
                foreach (var rise in host.GetComponentsInChildren<UIRiseIn>(true))
                {
                    typeof(UIRiseIn).GetField("_elapsed", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(rise, 2f);
                    typeof(UIRiseIn).GetMethod("Apply", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(rise, new object[] { 1f });
                }
                foreach (var graphic in host.GetComponentsInChildren<Graphic>()) graphic.SetAllDirty();
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                Directory.CreateDirectory("Logs/WorkshopForms");
                File.WriteAllBytes("Logs/WorkshopForms/" + width + "x" + height + (unlocked ? "-selected" : "-locked") + ".png", image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private sealed class PreviewBridge : IGameBridge
        {
            public SaveSettings CloneLiveSettings() => new SaveSettings();
            public void RestoreSettings(SaveSettings settings) { }
            public bool TryPersistSettings() => false;
            public void ApplyLiveSettings() { }
            public System.Collections.Generic.IReadOnlyList<HighScoreEntry> GetHighScores() => Array.Empty<HighScoreEntry>();
            public LifetimeStats GetLifetimeStats() => null;
            public bool TryPersistProfile() => false;
        }
    }
}
