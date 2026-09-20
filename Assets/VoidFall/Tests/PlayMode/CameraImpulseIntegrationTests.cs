using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoidFall.Core;
using VoidFall.Persistence;
using VoidFall.Runtime;

namespace VoidFall.Tests.PlayMode
{
    public sealed class CameraImpulseIntegrationTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private VoidFallGameRuntime _runtime;
        private SimulationProfileScope _profile;
        private bool _enabled;
        private object Get(string name) => typeof(VoidFallGameRuntime).GetField(name, Flags).GetValue(_runtime);
        private void Set(string name, object value) => typeof(VoidFallGameRuntime).GetField(name, Flags).SetValue(_runtime, value);
        private object Call(string name, params object[] args) => typeof(VoidFallGameRuntime).GetMethod(name, Flags).Invoke(_runtime, args);
        [UnitySetUp] public IEnumerator SetUp()
        {
            _runtime = Object.FindAnyObjectByType<VoidFallGameRuntime>();
            _enabled = _runtime.enabled; _runtime.enabled = false;
            _profile = new SimulationProfileScope(_runtime);
            Call("StartRunInternal", false, false);
            yield return null;
        }
        [TearDown] public void TearDown() { _profile.Dispose(); _runtime.enabled = _enabled; }

        [Test]
        public void Live_camera_honors_slider_pause_reduced_motion_and_settles()
        {
            var settings = ((SaveData)Get("_saveData")).settings;
            settings.shake = 1; settings.reducedMotion = false;
            Call("AddCameraShake", .12f);
            var full = (Vector2)Call("CameraShakeOffset");
            Assert.That(full.magnitude, Is.GreaterThan(0));
            settings.shake = .5f;
            Assert.That(((Vector2)Call("CameraShakeOffset")).magnitude, Is.EqualTo(full.magnitude * .5f).Within(.0001));
            settings.reducedMotion = true; Assert.That(Call("CameraShakeOffset"), Is.EqualTo(Vector2.zero));
            settings.reducedMotion = false; Set("_paused", true);
            Assert.That(Call("CameraShakeOffset"), Is.EqualTo(Vector2.zero));
            Set("_paused", false);
            ((CameraImpulse)Get("_cameraImpulse")).Advance(.4);
            Assert.That(Call("CameraShakeOffset"), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void Kill_cluster_cannot_extend_camera_motion_or_change_gameplay_freeze()
        {
            Set("_freezeTimer", .085f);
            for (var i = 0; i < 300; i++) Call("AddCameraShake", .055f);
            Assert.That(((Vector2)Call("CameraShakeOffset")).magnitude, Is.LessThanOrEqualTo(4.5f));
            ((CameraImpulse)Get("_cameraImpulse")).Advance(.17);
            Assert.That(Call("CameraShakeOffset"), Is.EqualTo(Vector2.zero));
            Assert.That(Get("_freezeTimer"), Is.EqualTo(.085f));
        }
    }
}
