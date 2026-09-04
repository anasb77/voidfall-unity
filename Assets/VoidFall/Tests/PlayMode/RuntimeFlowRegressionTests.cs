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
        public IEnumerator Expired_post_boss_delay_delivers_an_unclaimed_roulette_before_route_selection()
        {
            var runtime = UnityEngine.Object.FindAnyObjectByType<VoidFallGameRuntime>();
            Assert.That(runtime, Is.Not.Null);
            yield return null;

            Invoke(runtime, "StartRun");
            Invoke(runtime, "OnVoidObjectiveCompleted");
            Invoke(runtime, "SpawnRouletteChest", Vector2.zero);
            SetField(runtime, "_voidCompletionDelayRemaining", 0f);

            Invoke(runtime, "StepVoidCompletionDelay", 0f);

            Assert.That(GetField(runtime, "_rouletteChestActive"), Is.False);
            Assert.That(GetField(runtime, "_rouletteActive"), Is.True);
            Assert.That(GetField(runtime, "_openRouteAfterRoulette"), Is.True);

            var session = GetField(runtime, "_rouletteSession");
            Invoke(runtime, "OnRouletteComplete", session);
            Invoke(runtime, "ClosePrizeReveal");

            Assert.That(GetField(runtime, "_openRouteAfterRoulette"), Is.False);
            Assert.That(GetField(runtime, "_paused"), Is.True);
            var ui = (UIManager)GetField(runtime, "_ui");
            Assert.That(ui.CurrentScreen, Is.EqualTo(UIScreen.RouteSelect));
        }

        [UnityTest]
        public IEnumerator Single_exit_travel_advances_route_before_initializing_the_next_objective()
        {
            var runtime = UnityEngine.Object.FindAnyObjectByType<VoidFallGameRuntime>();
            Assert.That(runtime, Is.Not.Null);
            yield return null;
            Invoke(runtime, "StartRun");
            // Use two implemented arenas so this isolates travel from the
            // unfinished objectives later in the production route.
            var route = new VoidRouteRun(new[]
            {
                new VoidRouteNode("abyss", "Abyss", 0, 1, "", "", "", "", "hydra"),
                new VoidRouteNode("hydra", "Hydra", 1, 1, "", "", "", ""),
            }, "abyss");
            SetField(runtime, "_voidRoute", route);
            Invoke(runtime, "OnVoidObjectiveCompleted");
            SetField(runtime, "_voidCompletionDelayRemaining", 0f);

            Invoke(runtime, "StepVoidCompletionDelay", 0f);
            Invoke(runtime, "CommitRiftTransitionSwap");

            Assert.That(route.CurrentVoidId, Is.EqualTo("hydra"));
            Assert.That(route.StateOf("hydra"), Is.EqualTo(RouteNodeState.Selected));
            Assert.That(route.History, Is.EqualTo(new[] { "abyss", "hydra" }));
            var tracker = (VoidObjectiveTracker)GetField(runtime, "_objectives");
            Assert.That(tracker.Text, Does.Contain("HYDRA"));
            Invoke(runtime, "OnVoidObjectiveCompleted");
            Assert.That(route.HasEscaped, Is.True);
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

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, "Missing field '" + name + "'.");
            field.SetValue(target, value);
        }

        private static void Invoke(object target, string name)
        {
            Invoke(target, name, Array.Empty<object>());
        }

        private static void Invoke(object target, string name, params object[] arguments)
        {
            var method = target.GetType().GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                null,
                Array.ConvertAll(arguments, argument => argument?.GetType() ?? typeof(object)),
                null);
            if (method == null)
            {
                foreach (var candidate in target.GetType().GetMethods(
                             BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    if (candidate.Name == name && candidate.GetParameters().Length == arguments.Length)
                    {
                        method = candidate;
                        break;
                    }
                }
            }
            Assert.That(method, Is.Not.Null, "Missing method '" + name + "'.");
            try
            {
                method.Invoke(target, arguments);
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException ?? exception;
            }
        }
    }
}
