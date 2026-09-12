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
        public static void BuildPlayer() => BuildScript.BuildWindows();

        public static void BuildRevision() => BuildScript.BuildWindows();

        public static void Capture()
        {
            CaptureAt(1280, 820, Path.GetFullPath("Logs/RoulettePreview"));
        }

        public static void CaptureRevision()
        {
            try
            {
                CaptureAt(1280, 820, Path.GetFullPath("Logs/RouletteMusicRevision/Captures/1280x820"));
                CaptureAt(1920, 1080, Path.GetFullPath("Logs/RouletteMusicRevision/Captures/1920x1080"));
                EditorApplication.Exit(0);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void CaptureClaims()
        {
            try
            {
                CaptureAt(1280, 820, Path.GetFullPath("Logs/RouletteClaims/Captures/1280x820"));
                CaptureAt(1920, 1080, Path.GetFullPath("Logs/RouletteClaims/Captures/1920x1080"));
                EditorApplication.Exit(0);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void CaptureAt(int width, int height, string output)
        {
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
                CaptureFrame(camera, target, host.transform, output + "/wheel.png");
                Invoke(view, "SetRewardsOpen", true);
                CaptureFrame(camera, target, host.transform, output + "/rewards-drawer.png");
                Invoke(view, "DescribePrize", 0);
                CaptureFrame(camera, target, host.transform, output + "/reward-details.png");
                Invoke(view, "SetRewardsOpen", false);
                Invoke(view, "OnRaiseStakes");
                CaptureFrame(camera, target, host.transform, output + "/wager.png");
                view.SetVisible(false);
                var revealRoot = UIBuilder.Stretch(UIBuilder.CreateRect(host.transform, "Upgrade Reward"));
                var reveal = revealRoot.gameObject.AddComponent<LevelUpView>();
                reveal.Initialize(null);
                var weapon = ContentCatalog.Weapons[0];
                var describe = typeof(VoidFall.Runtime.VoidFallGameRuntime).GetMethod("DescribeClaimWeaponRank", BindingFlags.Static | BindingFlags.NonPublic);
                var first = new UpgradeCardData { Title = weapon.Name, Category = "Weapon",
                    Description = (string)describe.Invoke(null, new object[] { weapon, 2, 3 }),
                    CurrentRank = 2, MaxRank = 6, LevelText = "RANK 2 → 3", AccentColor = UITheme.CyanLight };
                var second = first;
                second.CurrentRank = 3;
                second.LevelText = "RANK 3 → 4";
                second.Description = (string)describe.Invoke(null, new object[] { weapon, 3, 4 });
                reveal.ShowReward(first, () => reveal.ShowReward(second, null, null, 2, 2), null, 1, 2);
                SettleUpgradeReward(reveal);
                CaptureFrame(camera, target, host.transform, output + "/claim-one.png");
                reveal.transform.Find("Content/Grid/Card0").GetComponent<Button>().onClick.Invoke();
                SettleUpgradeReward(reveal);
                CaptureFrame(camera, target, host.transform, output + "/claim-two.png");
                reveal.SetVisible(false);
                reveal.ShowReward(new UpgradeCardData { Title = "500 Parts", Category = "Reward",
                    Description = "Add 500 Parts to your run earnings for the Workshop.", AccentColor = UITheme.CyanLight }, null);
                SettleUpgradeReward(reveal);
                CaptureFrame(camera, target, host.transform, output + "/claim-parts.png");
                reveal.SetVisible(false);
                reveal.ShowReward(new UpgradeCardData { Title = WildCardRules.DisplayName(WildCardId.Greed), Category = "Wild Card",
                    Description = WildCardRules.Description(WildCardId.Greed), AccentColor = new Color(.94f, .64f, .47f) }, () => { }, () => { });
                SettleUpgradeReward(reveal);
                CaptureFrame(camera, target, host.transform, output + "/wild-card.png");
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

        public static void CaptureCorrection()
        {
            try
            {
                CaptureAt(1280, 820, Path.GetFullPath("Logs/RewardMenuCorrection/Captures/1280x820"));
                CaptureAt(1920, 1080, Path.GetFullPath("Logs/RewardMenuCorrection/Captures/1920x1080"));
                EditorApplication.Exit(0);
            }
            catch (System.Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        private static void SettleUpgradeReward(LevelUpView view)
        {
            Set(view, "_rewardElapsed", 2f);
            Invoke(view, "Update");
            Canvas.ForceUpdateCanvases();
            foreach (var rise in view.GetComponentsInChildren<UIRiseIn>())
            {
                Set(rise, "_elapsed", 2f);
                Invoke(rise, "Apply", 1f);
            }
        }

        private static void CaptureFrame(Camera camera, RenderTexture target, Transform root, string path)
        {
            // Multiple synchronous Editor renders need a full canvas submission
            // after text atlas or drawer changes, not just the changed text mesh.
            foreach (var graphic in root.GetComponentsInChildren<Graphic>()) graphic.SetAllDirty();
            Canvas.ForceUpdateCanvases();
            foreach (var graphic in root.GetComponentsInChildren<RouletteWheelGraphic>())
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
        private static void Invoke(object target, string method, params object[] arguments)
            => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
    }
}
