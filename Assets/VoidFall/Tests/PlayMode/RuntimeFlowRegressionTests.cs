using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoidFall.Core;
using VoidFall.Runtime;
using VoidFall.UI;
using VoidFall.Persistence;

namespace VoidFall.Tests.PlayMode
{
    public sealed class RuntimeFlowRegressionTests
    {
        private VoidFallGameRuntime _isolatedRuntime;
        private object _previousStore, _previousProfile;
        private bool _previousEnabled;
        private bool _previousApplicationInactive;

        [Test]
        public void Court_split_and_boss_hazards_switch_without_leaking_views()
        {
            var runtime = _isolatedRuntime;
            Invoke(runtime, "StartRun");
            SetField(runtime, "_arenaId", ArenaId.MonochromeCourt);
            SetField(runtime, "_time", 20f);
            Invoke(runtime, "RenderMonochromePresentation");
            var split = (SpriteRenderer[])GetField(runtime, "_courtSplitViews");
            Assert.That(split[0].enabled && split[1].enabled, Is.True);
            Assert.That(split[0].color.grayscale, Is.LessThan(split[1].color.grayscale));

            SetField(runtime, "_monochromeBossEncounterActive", true);
            SetField(runtime, "_monochromeBoardTileSize", new Vector2(100, 100));
            SetField(runtime, "_monochromeHazard", new CourtHazardState(CourtFaction.White, CourtHazardStage.Warning));
            Invoke(runtime, "RenderMonochromePresentation");
            Assert.That(split[0].enabled || split[1].enabled, Is.False);
            var tiles = (SpriteRenderer[])GetField(runtime, "_courtBoardTiles");
            var material = (Material)GetField(runtime, "_courtTileMaterial");
            Assert.That(tiles[0].enabled, Is.True);
            Assert.That(material.GetFloat("_HazardStage"), Is.EqualTo(1));
            Assert.That(material.GetFloat("_WhiteActive"), Is.EqualTo(1));
            SetField(runtime, "_monochromeHazard", new CourtHazardState(CourtFaction.Black, CourtHazardStage.Burning));
            Invoke(runtime, "RenderMonochromePresentation");
            Assert.That(material.GetFloat("_HazardStage"), Is.EqualTo(2));
            Assert.That(material.GetFloat("_WhiteActive"), Is.Zero);
            SetField(runtime, "_monochromeBossEncounterActive", false);
            SetField(runtime, "_arenaId", ArenaId.Hydra);
            Invoke(runtime, "RenderMonochromePresentation");
            foreach (var view in tiles) Assert.That(view.enabled, Is.False);
            foreach (var view in split) Assert.That(view.enabled, Is.False);
        }

        [UnitySetUp]
        public IEnumerator IsolateProfile()
        {
            yield return null;
            _isolatedRuntime = UnityEngine.Object.FindAnyObjectByType<VoidFallGameRuntime>();
            Assert.That(_isolatedRuntime, Is.Not.Null);
            _previousEnabled = _isolatedRuntime.enabled;
            _previousApplicationInactive = (bool)GetField(_isolatedRuntime, "_applicationInactive");
            SetField(_isolatedRuntime, "_applicationInactive", false);
            _isolatedRuntime.enabled = false;
            _previousStore = GetField(_isolatedRuntime, "_saveStore");
            _previousProfile = GetField(_isolatedRuntime, "_saveData");
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "voidfall-roulette-flow-" + Guid.NewGuid().ToString("N"), "profile.json");
            SetField(_isolatedRuntime, "_saveStore", new SaveStore(path));
            SetField(_isolatedRuntime, "_saveData", SaveStore.CreateDefault());
            SetField(_isolatedRuntime, "_runSaved", true);
        }

        [UnityTearDown]
        public IEnumerator RestoreProfile()
        {
            if (_isolatedRuntime != null)
            {
                SetField(_isolatedRuntime, "_runSaved", true);
                Invoke(_isolatedRuntime, "EnterMainMenu");
                SetField(_isolatedRuntime, "_saveStore", _previousStore);
                SetField(_isolatedRuntime, "_saveData", _previousProfile);
                _isolatedRuntime.enabled = _previousEnabled;
                SetField(_isolatedRuntime, "_applicationInactive", _previousApplicationInactive);
            }
            yield return null;
        }

        [Test]
        public void Nebula_surface_switches_back_to_sakura_material_and_geometry()
        {
            var runtime = _isolatedRuntime;
            var originalArena = GetField(runtime, "_arenaId");
            try
            {
                foreach (var arena in new[] { ArenaId.RedNebula, ArenaId.WhiteSakura, ArenaId.RedNebula })
                {
                    SetField(runtime, "_arenaId", arena);
                    Invoke(runtime, "ConfigureArenaFarFilaments");
                    var materials = (Material[])GetField(runtime, "_arenaNearFilamentMaterials");
                    var views = (MeshFilter[])GetField(runtime, "_arenaNearFilamentOuterViews");
                    Assert.That(materials[4].GetFloat("_Continuous"), Is.EqualTo(arena == ArenaId.RedNebula ? 1f : 0f));
                    var mesh = views[4].sharedMesh;
                    Assert.That(mesh.vertexCount, Is.GreaterThan(0));
                    if (arena == ArenaId.RedNebula)
                        Assert.That(mesh.triangles.Length, Is.EqualTo((mesh.vertexCount / 2 - 1) * 6));
                    else
                        Assert.That(mesh.uv2, Is.Empty, "Sakura must regain the original layered geometry.");
                    Assert.That(materials[4].GetTexture("_MaskTex"), Is.Not.Null);
                }
            }
            finally
            {
                SetField(runtime, "_arenaId", originalArena);
                Invoke(runtime, "ConfigureArenaFarFilaments");
            }
        }

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
                Is.EquivalentTo(route.Node("abyss").Outgoing));
            Assert.That(route.NodesInState(RouteNodeState.Available), Has.Count.EqualTo(2));
        }

        [Test]
        public void Nebula_wave_hits_reused_meteor_slots_but_not_the_same_meteor_twice()
        {
            var runtime = _isolatedRuntime;
            Invoke(runtime, "StartRun");
            Invoke(runtime, "ClearNebulaStrikes");
            Invoke(runtime, "TrySpawnNebulaStrike");
            var waves = (Array)GetField(runtime, "_nebulaStrikes");
            var active = 0;
            foreach (var wave in waves) if (wave != null && (bool)GetField(wave, "Active")) active++;
            Assert.That(active, Is.InRange(3, 4));
            var strike = waves.GetValue(0);
            var sim = GetField(runtime, "_gameSim");
            var player = GetField(sim, "Player");
            SetField(player, "Position", new Vector2(1000, 1000));
            SetField(sim, "Player", player);
            var meteors = (Array)GetField(sim, "Meteors");
            Array.Clear(meteors, 0, meteors.Length);
            var insert = sim.GetType().GetMethod("TryInsertMeteor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var slot = (int)insert.Invoke(sim, new object[] { Vector2.zero, 20f, 0, false, 0d });
            var first = meteors.GetValue(slot);
            SetField(first, "Health", 1000f); SetField(first, "MaxHealth", 1000f); meteors.SetValue(first, slot);
            Invoke(runtime, "DamageNebulaStrike", strike, new Vector2(-100, 0), new Vector2(100, 0));
            var damaged = (float)GetField(meteors.GetValue(slot), "Health");
            Assert.That(damaged, Is.LessThan(1000));
            Invoke(runtime, "DamageNebulaStrike", strike, new Vector2(-100, 0), new Vector2(100, 0));
            Assert.That(GetField(meteors.GetValue(slot), "Health"), Is.EqualTo(damaged));
            var retired = meteors.GetValue(slot); SetField(retired, "Active", false); meteors.SetValue(retired, slot);
            var replacement = (int)insert.Invoke(sim, new object[] { Vector2.zero, 20f, 0, false, 0d });
            Assert.That(replacement, Is.EqualTo(slot));
            var next = meteors.GetValue(slot); SetField(next, "Health", 1000f); SetField(next, "MaxHealth", 1000f); meteors.SetValue(next, slot);
            Assert.That(GetField(next, "Identity"), Is.Not.EqualTo(GetField(first, "Identity")));
            Invoke(runtime, "DamageNebulaStrike", strike, new Vector2(-100, 0), new Vector2(100, 0));
            Assert.That((float)GetField(meteors.GetValue(slot), "Health"), Is.LessThan(1000));
            Invoke(runtime, "ClearNebulaStrikes");
        }

        [Test]
        public void Compact_boss_hud_keeps_hp_inside_bar_without_backing_panel()
        {
            Invoke(_isolatedRuntime, "UpdateBossHudRemaster", 1, .1f);
            var backing = (UnityEngine.UI.Image)GetField(_isolatedRuntime, "_bossHudPanel");
            var bar = (UnityEngine.UI.Image)GetField(_isolatedRuntime, "_bossBarFill");
            var hp = (UnityEngine.UI.Text)GetField(_isolatedRuntime, "_bossHealthText");
            Assert.That(backing.enabled, Is.False);
            Assert.That(bar.rectTransform.sizeDelta.x, Is.EqualTo(432f));
            Assert.That(hp.alignment, Is.EqualTo(TextAnchor.MiddleCenter));
            Assert.That(hp.rectTransform.rect.center.y + hp.rectTransform.anchoredPosition.y,
                Is.EqualTo(bar.rectTransform.rect.center.y + bar.rectTransform.anchoredPosition.y).Within(.01f));
        }

        [Test]
        public void Sakura_bypasses_the_new_camera_grade()
        {
            var arena = GetField(_isolatedRuntime, "_arenaId");
            try
            {
                SetField(_isolatedRuntime, "_arenaId", ArenaId.WhiteSakura);
                Invoke(_isolatedRuntime, "ApplyArenaColorGrade");
                Assert.That(GetField(GetField(_isolatedRuntime, "_arenaColorGrade"), "active"), Is.EqualTo(false));
                SetField(_isolatedRuntime, "_arenaId", ArenaId.RedNebula);
                Invoke(_isolatedRuntime, "ApplyArenaColorGrade");
                Assert.That(GetField(GetField(_isolatedRuntime, "_arenaColorGrade"), "active"), Is.EqualTo(true));
            }
            finally { SetField(_isolatedRuntime, "_arenaId", arena); Invoke(_isolatedRuntime, "ApplyArenaColorGrade"); }
        }

        [Test]
        public void Nebula_orbits_and_grouped_waves_repeat_for_the_same_seed()
        {
            SimulationGoldenMasterTests.PinHermeticPresentationState(_isolatedRuntime);
            foreach (var seed in new[] { 0x5f1dc0deu, 0x923af124u })
            {
                ulong first = 0;
                for (var run = 0; run < 2; run++)
                {
                    Invoke(_isolatedRuntime, "ApplyStressScenario", "productionMax", seed);
                    SetField(_isolatedRuntime, "_arenaId", ArenaId.RedNebula);
                    Invoke(_isolatedRuntime, "SelectRecipeForCurrentArena");
                    Invoke(_isolatedRuntime, "ClearMeteors");
                    Invoke(_isolatedRuntime, "ClearNebulaStrikes");
                    for (var i = 0; i < 3; i++) Invoke(_isolatedRuntime, "TrySpawnMeteor", false);
                    for (var i = 0; i < 2; i++) Invoke(_isolatedRuntime, "TrySpawnMeteor", true);
                    var meteors = (Array)GetField(GetField(_isolatedRuntime, "_gameSim"), "Meteors");
                    var orbit = false;
                    foreach (var m in meteors) if ((bool)GetField(m, "Active") && (bool)GetField(m, "Orbital")) orbit = true;
                    Assert.That(orbit, Is.True);
                    for (var tick = 0; tick < 720; tick++) Invoke(_isolatedRuntime, "Simulate", 1d / 60d);
                    var hash = SimulationGoldenMasterTests.HashRuntimeState(_isolatedRuntime);
                    if (run == 0) first = hash;
                    else Assert.That(hash, Is.EqualTo(first), "Nebula must reproduce orbital terrain and grouped-wave damage.");
                }
            }
        }

        [UnityTest]
        public IEnumerator Physical_relic_pickup_preserves_the_window_before_portal_junction()
        {
            var runtime = UnityEngine.Object.FindAnyObjectByType<VoidFallGameRuntime>();
            Assert.That(runtime, Is.Not.Null);
            yield return null;

            Invoke(runtime, "StartRun");
            Invoke(runtime, "DestroyEnemiesForVoidTransition");
            Invoke(runtime, "OnVoidObjectiveCompleted");
            Invoke(runtime, "SpawnRouletteChest", Vector2.zero);
            SetField(runtime, "_voidCompletionDelayRemaining", 11f);

            Invoke(runtime, "StepVoidCompletionDelay", 0f);

            Assert.That(GetField(runtime, "_rouletteChestActive"), Is.True);
            Assert.That(GetField(runtime, "_rouletteActive"), Is.False);
            Assert.That(GetField(runtime, "_openRouteAfterRoulette"), Is.False);

            var sim = GetField(runtime, "_gameSim");
            var playerField = sim.GetType().GetField("Player", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var player = playerField.GetValue(sim);
            player.GetType().GetField("Position").SetValue(player, Vector2.zero);
            playerField.SetValue(sim, player);
            Invoke(runtime, "UpdateRouletteChest", 2f);
            Assert.That(GetField(runtime, "_rouletteChestActive"), Is.False);
            Assert.That(GetField(runtime, "_rouletteActive"), Is.True);

            var session = GetField(runtime, "_rouletteSession");
            RouletteRules.Spin((RouletteSession)session, new Rng(200));
            Invoke(runtime, "OnRouletteComplete", session);
            Invoke(runtime, "ClosePrizeReveal");

            Assert.That(GetField(runtime, "_openRouteAfterRoulette"), Is.False);
            Assert.That(GetField(runtime, "_paused"), Is.False);
            Assert.That(runtime.JourneyStatus, Is.EqualTo("Rewards"));
            Assert.That(GetField(runtime, "_voidCompletionDelayRemaining"), Is.EqualTo(11f));
            Invoke(runtime, "StepVoidCompletionDelay", 11f);
            var ui = (UIManager)GetField(runtime, "_ui");
            Assert.That(ui.CurrentScreen, Is.EqualTo(UIScreen.None));
            Assert.That(runtime.JourneyStatus, Is.EqualTo("Junction"));
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
        public IEnumerator Double_boss_completion_resumes_escape_after_roulette_without_a_second_confirmation()
        {
            var runtime = UnityEngine.Object.FindAnyObjectByType<VoidFallGameRuntime>();
            Assert.That(runtime, Is.Not.Null);
            yield return null;
            SetField(runtime, "_diagnosticRunSeedOverride", 2848592627u);
            Invoke(runtime, "StartRun");
            Invoke(runtime, "StepObjectiveTracker", 300d);
            Invoke(runtime, "StepObjectiveTracker", 0d);
            Assert.That(runtime.ActiveBossesCount, Is.EqualTo(2));
            Invoke(runtime, "KillBoss", 1);
            Invoke(runtime, "StepObjectiveTracker", 0d);
            Assert.That(((VoidObjectiveTracker)GetField(runtime, "_objectives")).IsComplete, Is.False);
            Invoke(runtime, "KillBoss", 0);
            Invoke(runtime, "StepObjectiveTracker", 0d);
            Assert.That(((VoidObjectiveTracker)GetField(runtime, "_objectives")).IsComplete, Is.True);
            SetField(runtime, "_rouletteChestPulse", 2f);
            Invoke(runtime, "CollectRouletteChest");
            Assert.That(GetField(runtime, "_rouletteActive"), Is.True);
            RouletteRules.Spin((RouletteSession)GetField(runtime, "_rouletteSession"), new Rng(200));
            Invoke(runtime, "OnRouletteComplete", GetField(runtime, "_rouletteSession"));
            Invoke(runtime, "SyncUiScreen");
            Assert.That(GetField(runtime, "_prizeRevealActive"), Is.False);
            Assert.That(GetField(runtime, "_paused"), Is.False);
            Assert.That(runtime.JourneyStatus, Is.EqualTo("Rewards"));
            Assert.That((float)GetField(runtime, "_voidCompletionDelayRemaining"), Is.GreaterThan(0f));
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
