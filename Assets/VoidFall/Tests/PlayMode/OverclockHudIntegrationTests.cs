using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VoidFall.Core;
using VoidFall.Persistence;
using VoidFall.Runtime;

namespace VoidFall.Tests.PlayMode
{
    public sealed class OverclockHudIntegrationTests
    {
        private VoidFallGameRuntime _runtime;
        private object _previousStore, _previousProfile;
        private bool _previousEnabled;
        private uint _previousSeed;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return null;
            _runtime = UnityEngine.Object.FindAnyObjectByType<VoidFallGameRuntime>();
            Assert.That(_runtime, Is.Not.Null);
            _previousStore = Get(_runtime, "_saveStore");
            _previousProfile = Get(_runtime, "_saveData");
            _previousEnabled = _runtime.enabled;
            _previousSeed = (uint)Get(_runtime, "_diagnosticRunSeedOverride");
            _runtime.enabled = false;
            Set(_runtime, "_runSaved", true);
            Set(_runtime, "_saveStore", new SaveStore(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "vf-overclock-" + Guid.NewGuid().ToString("N"), "profile.json")));
            Set(_runtime, "_saveData", SaveStore.CreateDefault());
            Set(_runtime, "_diagnosticRunSeedOverride", 2848592627u);
            Call("StartRun");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_runtime != null)
            {
                Set(_runtime, "_runSaved", true);
                Call("EnterMainMenu");
                Set(_runtime, "_saveStore", _previousStore);
                Set(_runtime, "_saveData", _previousProfile);
                Set(_runtime, "_diagnosticRunSeedOverride", _previousSeed);
                _runtime.enabled = _previousEnabled;
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Visible_integrity_number_updates_even_with_the_legacy_label_disabled()
        {
            var sim = Get(_runtime, "_gameSim");
            var player = Get(sim, "Player");
            Set(player, "Health", 37f);
            Set(player, "MaxHealth", 100f);
            Set(sim, "Player", player);
            Call("UpdateHud");
            Assert.That(((Text)Get(_runtime, "_healthValueText")).text, Is.EqualTo("37/100"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Counter_grows_ten_percent_per_stack_and_stays_below_the_boss_bar()
        {
            Call("StepObjectiveTracker", 300d);
            Call("StepObjectiveTracker", 0d);
            Assert.That(_runtime.ActiveBossesCount, Is.GreaterThan(0));
            var state = new OverclockState();
            state.ApplyPickup();
            Set(_runtime, "_overclock", state);
            Call("UpdateHud");
            var counter = (RectTransform)Get(_runtime, "_overclockHudRoot");
            Assert.That(counter.localScale.x, Is.EqualTo(1f).Within(.001));
            state.ApplyPickup();
            state.ApplyPickup();
            Set(_runtime, "_overclock", state);
            Call("UpdateHud");
            Assert.That(counter.localScale.x, Is.EqualTo(1.2f).Within(.001));
            Canvas.ForceUpdateCanvases();
            var boss = (Image)Get(_runtime, "_bossHudPanel");
            var counterCorners = new Vector3[4];
            var bossCorners = new Vector3[4];
            counter.GetWorldCorners(counterCorners);
            boss.rectTransform.GetWorldCorners(bossCorners);
            Assert.That(counterCorners[1].y, Is.LessThan(bossCorners[0].y));
            Assert.That(((Image)Get(_runtime, "_boostPanel")).enabled, Is.False);
            Assert.That(((Image)Get(_runtime, "_overclockLineTrack")).enabled, Is.True);
            Assert.That(((Image)Get(_runtime, "_overclockLineGlow")).enabled, Is.True);
            Assert.That(((Text)Get(_runtime, "_boostText")).text, Is.EqualTo("OVERCLOCKED ×3"));
            state.Step(state.RemainingSeconds);
            Set(_runtime, "_overclock", state);
            Call("UpdateHud");
            Assert.That(counter.gameObject.activeSelf, Is.False);
            yield return null;
        }

        private static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        private void Call(string name, params object[] args) => _runtime.GetType().GetMethod(name, Flags).Invoke(_runtime, args);
    }
}
