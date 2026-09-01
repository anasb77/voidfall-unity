using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoidFall.Core;
using VoidFall.Runtime;
using VoidFall.UI;

namespace VoidFall.Tests.PlayMode
{
    public sealed class RuntimeFlowRegressionTests
    {
        [UnityTest]
        public IEnumerator Fresh_run_always_starts_in_abyss_even_when_menu_preview_differs()
        {
            var runtime = UnityEngine.Object.FindAnyObjectByType<VoidFallGameRuntime>();
            Assert.That(runtime, Is.Not.Null);
            yield return null;

            var save = GetField(runtime, "_saveData");
            var arenaField = save.GetType().GetField("arena");
            Assert.That(arenaField, Is.Not.Null);
            var previousArena = (string)arenaField.GetValue(save);
            try
            {
                arenaField.SetValue(save, "redNebula");
                Invoke(runtime, "StartRun");

                Assert.That(GetField(runtime, "_arenaId"), Is.EqualTo(ArenaId.Void));
                var route = (VoidRouteRun)GetField(runtime, "_voidRoute");
                Assert.That(route.CurrentVoidId, Is.EqualTo("abyss"));
            }
            finally
            {
                arenaField.SetValue(save, previousArena);
            }
        }

        [UnityTest]
        public IEnumerator Completing_current_void_makes_its_exits_available()
        {
            var runtime = UnityEngine.Object.FindAnyObjectByType<VoidFallGameRuntime>();
            Assert.That(runtime, Is.Not.Null);
            yield return null;

            Invoke(runtime, "StartRun");
            var route = (VoidRouteRun)GetField(runtime, "_voidRoute");
            Assert.That(route.NodesInState(RouteNodeState.Available), Is.Empty);

            Invoke(runtime, "OnVoidObjectiveCompleted");

            Assert.That(
                route.NodesInState(RouteNodeState.Available),
                Is.EquivalentTo(new[] { "hydra", "red-nebula", "white-sakura" }));
        }

        [UnityTest]
        public IEnumerator Roulette_view_initializes_with_one_live_canvas_group()
        {
            var root = new GameObject("Roulette Regression", typeof(RectTransform));
            try
            {
                var view = root.AddComponent<RouletteView>();
                Assert.DoesNotThrow(() => view.Initialize(null));
                yield return null;
                Assert.That(root.GetComponents<CanvasGroup>(), Has.Length.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static object GetField(object target, string name)
        {
            var field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, "Missing field '" + name + "'.");
            return field.GetValue(target);
        }

        private static void Invoke(object target, string name)
        {
            var method = target.GetType().GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, "Missing method '" + name + "'.");
            try
            {
                method.Invoke(target, null);
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException ?? exception;
            }
        }
    }
}
