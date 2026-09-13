using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using VoidFall.Core;
using VoidFall.Persistence;
using VoidFall.Runtime;
using VoidFall.Runtime.Rendering;
using VoidFall.UI;

namespace VoidFall.Tests.PlayMode
{
    public sealed class WorkshopCosmeticsIntegrationTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const float PreviewToWorld = 74f / 94f;
        private VoidFallGameRuntime _runtime;
        private object _previousStore, _previousProfile;
        private bool _previousEnabled;
        private string _profileDirectory;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return null;
            _runtime = UnityEngine.Object.FindAnyObjectByType<VoidFallGameRuntime>();
            Assert.That(_runtime, Is.Not.Null);
            _previousEnabled = _runtime.enabled;
            _runtime.enabled = false;
            _previousStore = Get(_runtime, "_saveStore");
            _previousProfile = Get(_runtime, "_saveData");
            _profileDirectory = Path.Combine(Path.GetTempPath(), "voidfall-workshop-" + Guid.NewGuid().ToString("N"));
            Set("_saveStore", new SaveStore(Path.Combine(_profileDirectory, "profile.json")));
            var profile = SaveStore.CreateDefault();
            profile.parts = 10000;
            profile.settings.reducedMotion = true;
            Set("_saveData", profile);
            Set("_runSaved", true);
            Call("StartRunInternal", false, false);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_runtime != null)
            {
                Set("_runSaved", true);
                Call("EnterMainMenu");
                Set("_saveStore", _previousStore);
                Set("_saveData", _previousProfile);
                _runtime.enabled = _previousEnabled;
            }
            if (_profileDirectory != null && Directory.Exists(_profileDirectory))
                Directory.Delete(_profileDirectory, true);
            yield return null;
        }

        [TestCase(PlayerForms.DasherId)]
        [TestCase(PlayerForms.BruteId)]
        public void Workshop_lists_all_forms_and_selects_an_unlocked_form_for_the_next_run(string selectedId)
        {
            var profile = (SaveData)Get(_runtime, "_saveData");
            profile.unlockedForms = new[] { PlayerForms.DefaultId, PlayerForms.DasherId, PlayerForms.BruteId };
            Call("EnterMainMenu");
            var ui = (UIManager)Get(_runtime, "_ui");
            ui.Callbacks.OpenWorkshop();
            foreach (var form in PlayerForms.All)
            {
                var button = FindFormButton(ui.Workshop, form.Id);
                Assert.That(button, Is.Not.Null, form.Name + " must be visible in Workshop.");
                Assert.That(button.gameObject.activeInHierarchy, Is.True);
            }

            FindFormButton(ui.Workshop, selectedId).onClick.Invoke();
            Assert.That(((SaveData)Get(_runtime, "_saveData")).form, Is.EqualTo(selectedId));
            Assert.That(((SaveStore)Get(_runtime, "_saveStore")).Load().form, Is.EqualTo(selectedId),
                "Selection must survive closing the game before playing a run.");
            Assert.That(ui.CurrentScreen, Is.EqualTo(UIScreen.Workshop));
            Call("StartRunInternal", false, false);
            Assert.That(Get(_runtime, "_formId"), Is.EqualTo(selectedId));
            var progress = (UpgradeProgress)Get(_runtime, "_upgradeProgress");
            var starter = UpgradeRules.StartingWeaponIndex(PlayerForms.StartingWeapon(selectedId));
            Assert.That(progress.WeaponRanks[starter], Is.EqualTo(1));
            Assert.That((float)Get(Get(Get(_runtime, "_gameSim"), "Player"), "MaxHealth"),
                Is.EqualTo(PlayerForms.BaseMaxHealth(selectedId)));
        }

        [Test]
        public void Locked_forms_stay_visible_and_reject_selection_even_if_the_callback_is_invoked()
        {
            Call("EnterMainMenu");
            var ui = (UIManager)Get(_runtime, "_ui");
            ui.Callbacks.OpenWorkshop();
            foreach (var id in new[] { PlayerForms.DasherId, PlayerForms.BruteId })
            {
                var button = FindFormButton(ui.Workshop, id);
                Assert.That(button, Is.Not.Null);
                Assert.That(button.gameObject.activeInHierarchy, Is.True);
                Assert.That(button.interactable, Is.False);
                Assert.That(button.transform.Find("State").GetComponent<Text>().text,
                    Does.Contain(PlayerForms.Form(id).UnlockHint));
                button.onClick.Invoke();
                Assert.That(((SaveData)Get(_runtime, "_saveData")).form, Is.EqualTo(PlayerForms.DefaultId));
            }
        }

        [Test]
        public void Form_selection_cannot_change_a_live_run()
        {
            var profile = (SaveData)Get(_runtime, "_saveData");
            profile.unlockedForms = new[] { PlayerForms.DefaultId, PlayerForms.DasherId };
            ((UIManager)Get(_runtime, "_ui")).Callbacks.SelectForm(PlayerForms.DasherId);
            Assert.That(profile.form, Is.EqualTo(PlayerForms.DefaultId));
            Assert.That(Get(_runtime, "_formId"), Is.EqualTo(PlayerForms.DefaultId));
        }

        [Test]
        public void Navigation_focus_is_visible_without_equipping_the_form()
        {
            var profile = (SaveData)Get(_runtime, "_saveData");
            profile.unlockedForms = new[] { PlayerForms.DefaultId, PlayerForms.DasherId };
            Call("EnterMainMenu");
            var ui = (UIManager)Get(_runtime, "_ui");
            ui.Callbacks.OpenWorkshop();
            var button = FindFormButton(ui.Workshop, PlayerForms.DasherId);
            EventSystem.current.SetSelectedGameObject(null);
            var surface = button.GetComponent<Image>();
            var restingSprite = surface.overrideSprite;
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            Assert.That(surface.overrideSprite, Is.Not.SameAs(restingSprite));
            Assert.That(((SaveData)Get(_runtime, "_saveData")).form, Is.EqualTo(PlayerForms.DefaultId));
            EventSystem.current.SetSelectedGameObject(null);
        }

        private static Button FindFormButton(WorkshopView workshop, string id)
        {
            foreach (var button in workshop.GetComponentsInChildren<Button>(true))
                if (button.name == "Form." + id) return button;
            return null;
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Purchased_cosmetics_match_the_workshop_preview_at_gameplay_scale(int rank)
        {
            PurchaseRanks(rank);
            Call("StartRunInternal", false, false);
            Call("UpdatePlayerCosmetics", true);
            var preview = Preview();
            Invoke(preview, "Update");
            var images = (Image[])Get(preview, "_cosmeticImages");
            var views = (SpriteRenderer[])Get(_runtime, "_playerCosmeticViews");
            for (var kind = PlayerCosmeticKind.Magnet; kind < PlayerCosmeticKind.Count; kind++)
            {
                var image = images[(int)kind];
                var view = views[(int)kind];
                Assert.That(image.enabled, Is.True, kind.ToString());
                Assert.That(view.enabled, Is.True, kind.ToString());
                Assert.That(view.sprite, Is.SameAs(image.sprite), kind.ToString());
                Assert.That(view.bounds.size.x, Is.EqualTo(image.rectTransform.rect.width * PreviewToWorld).Within(.02f),
                    kind + " must retain the preview's artwork width, not collapse below one world unit.");
                Assert.That(view.bounds.size.y, Is.EqualTo(image.rectTransform.rect.height * PreviewToWorld).Within(.02f), kind.ToString());
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Mobility_purchase_displays_the_same_trails_as_the_workshop_preview(int rank)
        {
            for (var i = 0; i < rank; i++) Call("TryBuyWorkshop", "mobility");
            Call("StartRunInternal", false, false);
            Call("UpdatePlayerCosmetics", true);
            var preview = Preview();
            Invoke(preview, "Update");
            var images = (Image[])Get(preview, "_trailImages");
            var views = (SpriteRenderer[])Get(_runtime, "_playerTrailViews");
            for (var i = 0; i < views.Length; i++)
            {
                Assert.That(views[i].enabled, Is.EqualTo(i < rank));
                if (i >= rank) continue;
                Assert.That(views[i].sprite, Is.Not.Null, "An enabled renderer without a sprite cannot display a trail.");
                Assert.That(views[i].sprite, Is.SameAs(images[i].sprite));
                Assert.That(views[i].bounds.size.x, Is.EqualTo(images[i].rectTransform.rect.width * PreviewToWorld).Within(.02f));
                Assert.That(views[i].bounds.size.y, Is.EqualTo(images[i].rectTransform.rect.height * PreviewToWorld).Within(.02f));
                Assert.That(Vector2.Distance(views[i].transform.position,
                    images[i].rectTransform.anchoredPosition * PreviewToWorld), Is.LessThan(.02f));
            }
        }

        [Test]
        public void Refund_and_hidden_player_remove_all_workshop_decorations()
        {
            PurchaseRanks(3);
            Call("StartRunInternal", false, false);
            Call("UpdatePlayerCosmetics", true);
            Call("UpdatePlayerCosmetics", false);
            AssertAllHidden();
            Call("RefundAllWorkshop");
            Call("StartRunInternal", false, false);
            Call("UpdatePlayerCosmetics", true);
            AssertAllHidden();
        }

        [Test]
        public void Purchased_upgrades_visibly_change_the_character_outside_the_base_eye()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("Requires a graphics device for the native character capture.");

            var original = CaptureCharacter("origin");
            PurchaseRanks(3);
            Call("StartRunInternal", false, false);
            var upgraded = CaptureCharacter("upgraded");
            var changedPixels = 0;
            for (var y = 0; y < 512; y++)
                for (var x = 0; x < 512; x++)
                {
                    // Exclude the eye, aura and base ring: the upgrade artwork
                    // must actually render around them, not beneath their center.
                    if ((x - 256) * (x - 256) + (y - 256) * (y - 256) < 112 * 112) continue;
                    var a = original[y * 512 + x];
                    var b = upgraded[y * 512 + x];
                    if (Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b) > 30)
                        changedPixels++;
                }
            Assert.That(changedPixels, Is.GreaterThan(256), "Purchased decorations must be visible around Zack's eye.");
        }

        private Color32[] CaptureCharacter(string name)
        {
            Call("Render");
            typeof(VoidFallGameRuntime).Assembly.GetType("VoidFall.Runtime.ProceduralSpriteFactory")
                .GetMethod("FlushAtlas", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            var views = new List<SpriteRenderer>
            {
                (SpriteRenderer)Get(_runtime, "_playerView"),
                (SpriteRenderer)Get(_runtime, "_playerRingView"),
                (SpriteRenderer)Get(_runtime, "_playerAuraView"),
            };
            views.AddRange((SpriteRenderer[])Get(_runtime, "_playerCosmeticViews"));
            views.AddRange((SpriteRenderer[])Get(_runtime, "_playerTrailViews"));
            var layers = new Dictionary<GameObject, int>();
            var cameraObject = new GameObject("Workshop regression camera", typeof(Camera));
            var target = new RenderTexture(512, 512, 24);
            var image = new Texture2D(512, 512, TextureFormat.RGB24, false);
            var previousTarget = RenderTexture.active;
            try
            {
                foreach (var view in views)
                {
                    if (view == null) continue;
                    layers[view.gameObject] = view.gameObject.layer;
                    view.gameObject.layer = 31;
                }
                var camera = cameraObject.GetComponent<Camera>();
                camera.enabled = false;
                camera.transform.position = new Vector3(0, 0, -100);
                camera.orthographic = true;
                camera.orthographicSize = 115;
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.035f, .05f, .08f);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                image.Apply();
                var directory = Path.GetFullPath("Logs/WorkshopCosmetics");
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, name + ".png"), image.EncodeToPNG());
                return image.GetPixels32();
            }
            finally
            {
                foreach (var pair in layers) pair.Key.layer = pair.Value;
                RenderTexture.active = previousTarget;
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(image);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private void PurchaseRanks(int rank)
        {
            foreach (var entry in ((SaveData)Get(_runtime, "_saveData")).workshop)
                for (var i = 0; i < (entry.id == "protocol" ? 1 : rank); i++)
                    Call("TryBuyWorkshop", entry.id);
        }

        private PlayerFramePreview Preview() => ((UIManager)Get(_runtime, "_ui")).Workshop.PreviewStage.GetComponent<PlayerFramePreview>();

        private void AssertAllHidden()
        {
            foreach (var field in new[] { "_playerCosmeticViews", "_playerTrailViews" })
                foreach (var view in (SpriteRenderer[])Get(_runtime, field))
                    if (view != null) Assert.That(view.enabled, Is.False, view.name);
        }

        private static object Get(object owner, string field) => owner.GetType().GetField(field, Flags).GetValue(owner);
        private void Set(string field, object value) => _runtime.GetType().GetField(field, Flags).SetValue(_runtime, value);
        private static object Invoke(object owner, string method, params object[] args) => owner.GetType().GetMethod(method, Flags).Invoke(owner, args);
        private object Call(string method, params object[] args) => Invoke(_runtime, method, args);
    }
}
