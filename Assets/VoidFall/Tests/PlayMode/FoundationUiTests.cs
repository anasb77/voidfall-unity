using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using VoidFall.UI;
using VoidFall.Core;
using VoidFall.Persistence;
using VoidFall.Runtime;

namespace VoidFall.Tests.PlayMode
{
    public sealed class FoundationUiTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private VoidFallGameRuntime _runtime;
        private SaveData _previousProfile;
        private SaveStore _previousStore, _store;
        private object _previousExport, _previousSeed;
        private bool _enabled, _inactive;
        private string _directory;
        private SaveData Profile => (SaveData)Get("_saveData");

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return null;
            _runtime = UnityEngine.Object.FindAnyObjectByType<VoidFallGameRuntime>();
            Assert.That(_runtime, Is.Not.Null);
            _enabled = _runtime.enabled;
            _runtime.enabled = false;
            _previousProfile = Profile;
            _previousStore = (SaveStore)Get("_saveStore");
            _previousExport = Get("_runExportDirectoryOverride");
            _previousSeed = Get("_diagnosticRunSeedOverride");
            _inactive = (bool)Get("_applicationInactive");
            _directory = Path.Combine(Path.GetTempPath(), "voidfall-foundation-ui-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _store = new SaveStore(Path.Combine(_directory, "profile.json"));
            var profile = SaveStore.CreateDefault();
            profile.parts = 100;
            profile.directorOnboardingSeen = true;
            _store.Save(profile);
            Set("_saveStore", _store);
            Set("_saveData", profile);
            Set("_runExportDirectoryOverride", Path.Combine(_directory, "exports"));
            Set("_runSaved", true);
            Set("_applicationInactive", false);
            Set("_diagnosticRunSeedOverride", 2848592627u);
            Call("StartRunInternal", false, false);
            Call("DestroyEnemiesForVoidTransition");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_runtime != null)
            {
                Call("FinishRunExport", "test_complete");
                Call("StopMajorIncident");
                Set("_runSaved", true);
                Set("_gameOver", false);
                Call("EnterMainMenu");
                Set("_saveData", _previousProfile);
                Set("_saveStore", _previousStore);
                Set("_runExportDirectoryOverride", _previousExport);
                Set("_diagnosticRunSeedOverride", _previousSeed);
                Set("_applicationInactive", _inactive);
                _runtime.enabled = _enabled;
            }
            if (_directory != null && Directory.Exists(_directory)) Directory.Delete(_directory, true);
            yield return null;
        }

        private UIManager Ui => (UIManager)Get("_ui");

        [Test]
        public void Gameplay_camera_allows_the_authored_pipeline_multisampling()
        {
            var camera = (Camera)Get("_camera");
            Assert.That(camera.allowMSAA, Is.True);
        }

        [Test]
        public void Home_and_quit_modal_keep_focus_on_interactable_controls()
        {
            EventSystem.current.SetSelectedGameObject(null);
            Call("EnterMainMenu");
            Assert.That(EventSystem.current.currentSelectedGameObject.transform.parent.name, Is.EqualTo("StartRun"));
            var start = EventSystem.current.currentSelectedGameObject.GetComponent<Button>();
            Ui.QuitConfirm.Show();
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("Cancel"));
            Assert.That(start.IsInteractable(), Is.False);
            var mute = (Button)typeof(UIManager).GetField("_muteButton", Flags).GetValue(Ui);
            Assert.That(mute.IsInteractable(), Is.False, "modal navigation must not escape to global chrome");
            ExecuteEvents.Execute(EventSystem.current.currentSelectedGameObject,
                new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            Assert.That(Ui.QuitConfirm.IsVisible, Is.False);
            Assert.That(Get("_mainMenuBrowsing"), Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(start.gameObject));
            Assert.That(start.IsInteractable(), Is.True);
            Assert.That(mute.IsInteractable(), Is.True);
        }

        [Test]
        public void Gamepad_pause_map_and_cancel_respect_modal_ownership()
        {
            var settings = InputSystem.settings;
            var previousMode = settings.updateMode;
            var previousBackground = settings.backgroundBehavior;
            var previousEditorBehavior = settings.editorInputBehaviorInPlayMode;
            settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var pad = InputSystem.AddDevice<Gamepad>();
            try
            {
                Press(pad, GamepadButton.Start);
                Assert.That(Get("_paused"), Is.True);
                Assert.That(Ui.CurrentScreen, Is.EqualTo(UIScreen.Pause));
                Press(pad, GamepadButton.Start);
                Assert.That(Get("_paused"), Is.False);
                Press(pad, GamepadButton.Select);
                Assert.That(Get("_routeMapOpen"), Is.True);
                Press(pad, GamepadButton.East);
                Assert.That(Get("_routeMapOpen"), Is.False);
                Assert.That(Get("_paused"), Is.False);
                Set("_revivePending", true);
                Press(pad, GamepadButton.Start);
                Press(pad, GamepadButton.Select);
                Assert.That(Get("_revivePending"), Is.True);
                Assert.That(Get("_routeMapOpen"), Is.False);
                Set("_revivePending", false);
            }
            finally
            {
                InputSystem.RemoveDevice(pad);
                settings.updateMode = previousMode;
                settings.backgroundBehavior = previousBackground;
                settings.editorInputBehaviorInPlayMode = previousEditorBehavior;
            }
        }

        private void Press(Gamepad pad, GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState());
            InputSystem.Update();
            _ = pad[button].wasPressedThisFrame;
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button));
            InputSystem.Update();
            Assert.That(pad[button].wasPressedThisFrame, Is.True, "Input fixture must deliver a real pressed edge.");
            Call("ReadNavigationShortcuts");
            InputSystem.QueueStateEvent(pad, new GamepadState());
            InputSystem.Update();
        }

        [Test]
        public void Workshop_selection_has_the_same_preview_as_pointer_hover()
        {
            var host = new GameObject("Focus test", typeof(RectTransform));
            try
            {
                var entered = 0; var exited = 0;
                var focus = host.AddComponent<UIFocusTrigger>();
                focus.Bind(() => entered++, () => exited++);
                ExecuteEvents.Execute(host, new BaseEventData(EventSystem.current), ExecuteEvents.selectHandler);
                Assert.That(entered, Is.EqualTo(1));
                ExecuteEvents.Execute(host, new BaseEventData(EventSystem.current), ExecuteEvents.deselectHandler);
                Assert.That(exited, Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [Test]
        public void Workshop_refund_is_inside_the_panel_and_record_lines_have_a_gap()
        {
            Call("EnterMainMenu");
            Ui.Callbacks.OpenWorkshop();
            Canvas.ForceUpdateCanvases();
            var refund = Ui.Workshop.GetComponentsInChildren<Button>().Single(button => button.name == "Refund");
            var panel = (RectTransform)Ui.Workshop.transform.Find("Panel");
            var corners = new Vector3[4]; ((RectTransform)refund.transform).GetWorldCorners(corners);
            Assert.That(corners.All(corner => panel.rect.Contains(panel.InverseTransformPoint(corner))), Is.True);
            Ui.Callbacks.OpenRecords();
            Canvas.ForceUpdateCanvases();
            var metric = Ui.Records.GetComponentsInChildren<Text>().Single(text => text.transform.parent.name == "Metric.runs" && text.name == "Label");
            var value = (RectTransform)metric.transform.parent.Find("Value");
            var captionCorners = new Vector3[4]; var valueCorners = new Vector3[4];
            metric.rectTransform.GetWorldCorners(captionCorners); value.GetWorldCorners(valueCorners);
            Assert.That(captionCorners[0].y - valueCorners[1].y, Is.GreaterThan(2f));
        }

        [Test]
        public void Essential_hud_backings_are_visible_and_high_contrast_strengthens_them()
        {
            var backing = (Image)Get("_approvedClockBacking");
            Assert.That(backing.enabled, Is.True);
            Assert.That(backing.raycastTarget, Is.False);
            Assert.That(backing.color.a, Is.GreaterThanOrEqualTo(.8f));
            var alpha = backing.color.a;
            Profile.settings.highContrast = true;
            Call("RefreshApprovedHudContrast");
            Assert.That(backing.color.a, Is.GreaterThan(alpha));
            var scoreBacking = (Image)Get("_approvedScoreBacking");
            Assert.That(scoreBacking.color.a, Is.EqualTo(backing.color.a));
            var score = ((Text[])Get("_metricValues"))[2];
            Assert.That(scoreBacking.transform.parent, Is.EqualTo(score.transform.parent));
            Assert.That(scoreBacking.transform.GetSiblingIndex(), Is.LessThan(score.transform.GetSiblingIndex()),
                "the contrast panel must not paint over the score it protects");
        }

        [Test]
        public void Starting_early_closes_startup_logging_without_waiting_for_a_menu_frame()
        {
            var before = Get("_startupMenuReportLogged");
            var ready = Get("_startupMenuReadyRealtime");
            try
            {
                Set("_startupMenuReportLogged", false);
                Set("_startupMenuReadyRealtime", Time.realtimeSinceStartupAsDouble - .5);
                Call("RecordStartupMenuFrame");
                Assert.That(Get("_startupMenuReportLogged"), Is.True);
            }
            finally { Set("_startupMenuReportLogged", before); Set("_startupMenuReadyRealtime", ready); }
        }

        [Test]
        public void Grid_material_instance_has_an_explicit_destroy_owner()
        {
            var renderer = (MeshRenderer)Get("_arenaGridRenderer");
            var materials = (System.Collections.Generic.List<Material>)Get("_dynamicMaterials");
            Assert.That(materials, Does.Contain(renderer.sharedMaterial));
            Assert.That(renderer.sharedMaterial.color.a, Is.EqualTo(.1f));
        }

        private object Get(string name) => typeof(VoidFallGameRuntime).GetField(name, Flags).GetValue(_runtime);
        private void Set(string name, object value) => typeof(VoidFallGameRuntime).GetField(name, Flags).SetValue(_runtime, value);
        private object Call(string name, params object[] args) => RuntimeTestReflection.Invoke(_runtime, name, args);
    }
}
