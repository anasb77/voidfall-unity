using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoidFall.Core;
using VoidFall.Persistence;
using VoidFall.Runtime;
using VoidFall.UI;

namespace VoidFall.Tests.PlayMode
{
    public sealed class JourneyIntegrationTests
    {
        private VoidFallGameRuntime _runtime;
        private SaveStore _previousStore;
        private SaveData _previousProfile;
        private SaveStore _testStore;
        private string _temporaryDirectory;
        private bool _previousEnabled;
        private bool _previousApplicationInactive;
        private uint _previousSeedOverride;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _runtime = UnityEngine.Object.FindAnyObjectByType<VoidFallGameRuntime>();
            Assert.That(_runtime, Is.Not.Null);
            _previousEnabled = _runtime.enabled;
            _runtime.enabled = false;
            _previousStore = (SaveStore)Get(_runtime, "_saveStore");
            _previousProfile = (SaveData)Get(_runtime, "_saveData");
            _previousApplicationInactive = (bool)Get(_runtime, "_applicationInactive");
            _previousSeedOverride = (uint)Get(_runtime, "_diagnosticRunSeedOverride");
            _temporaryDirectory = Path.Combine(Path.GetTempPath(), "voidfall-journey-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temporaryDirectory);
            _testStore = new SaveStore(Path.Combine(_temporaryDirectory, "profile.json"));
            var profile = SaveStore.CreateDefault();
            profile.parts = 120;
            profile.stats.totalRuns = 7;
            _testStore.Save(profile);
            Set(_runtime, "_saveStore", _testStore);
            Set(_runtime, "_saveData", profile);
            Set(_runtime, "_runSaved", true);
            Set(_runtime, "_applicationInactive", false);
            Set(_runtime, "_diagnosticRunSeedOverride", 2848592627u);
            Invoke(_runtime, "StartRun");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_runtime != null)
            {
                _runtime.enabled = false;
                // Discard only the isolated test run; never let cleanup commit it to the real profile.
                Set(_runtime, "_runSaved", true);
                Set(_runtime, "_gameOver", false);
                Set(_runtime, "_saveData", _previousProfile);
                Set(_runtime, "_saveStore", _previousStore);
                Invoke(_runtime, "EnterMainMenu");
                Set(_runtime, "_diagnosticRunSeedOverride", _previousSeedOverride);
                Set(_runtime, "_applicationInactive", _previousApplicationInactive);
                _runtime.enabled = _previousEnabled;
            }
            if (!string.IsNullOrEmpty(_temporaryDirectory) && Directory.Exists(_temporaryDirectory))
                Directory.Delete(_temporaryDirectory, true);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Map_opened_from_combat_pauses_and_closing_restores_combat()
        {
            var route = (VoidRouteRun)Get(_runtime, "_voidRoute");
            Invoke(_runtime, "ToggleRouteMap");
            Assert.That(Get(_runtime, "_paused"), Is.True);
            Assert.That(Ui.CurrentScreen, Is.EqualTo(UIScreen.RouteMap));
            Assert.That(Ui.RouteMap.IsVisible, Is.True);

            Invoke(_runtime, "CloseRouteMap");

            Assert.That(Get(_runtime, "_paused"), Is.False);
            Assert.That(Get(_runtime, "_routeMapOpen"), Is.False);
            Assert.That(Ui.CurrentScreen, Is.EqualTo(UIScreen.None));
            Assert.That(route.History, Is.EqualTo(new[] { "abyss" }));
            Assert.That(route.StateOf("abyss"), Is.EqualTo(RouteNodeState.Selected));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Map_opened_from_pause_keeps_the_existing_pause_when_closed()
        {
            Invoke(_runtime, "TogglePause");
            Assert.That(Get(_runtime, "_paused"), Is.True);
            Invoke(_runtime, "ToggleRouteMap");
            Assert.That(Ui.CurrentScreen, Is.EqualTo(UIScreen.RouteMap));

            Invoke(_runtime, "CloseRouteMap");

            Assert.That(Get(_runtime, "_paused"), Is.True);
            Assert.That(Ui.CurrentScreen, Is.EqualTo(UIScreen.Pause));
            Assert.That(Ui.RouteMap.IsVisible, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Map_shortcuts_and_stale_close_cannot_dismiss_an_active_prize()
        {
            Invoke(_runtime, "OpenBossRoulette");
            Assert.That(Get(_runtime, "_rouletteActive"), Is.True);
            RouletteRules.Spin((RouletteSession)Get(_runtime, "_rouletteSession"), new Rng(200));
            Invoke(_runtime, "OnRouletteComplete", Get(_runtime, "_rouletteSession"));
            Assert.That(Ui.CurrentScreen, Is.EqualTo(UIScreen.PrizeReveal));

            Invoke(_runtime, "ToggleRouteMap");
            Invoke(_runtime, "CloseRouteMap");
            _runtime.enabled = true;
            yield return null;
            _runtime.enabled = false;

            Assert.That(Get(_runtime, "_routeMapOpen"), Is.False);
            Assert.That(Get(_runtime, "_prizeRevealActive"), Is.True);
            Assert.That(Get(_runtime, "_paused"), Is.True);
            Assert.That(Ui.CurrentScreen, Is.EqualTo(UIScreen.PrizeReveal));
            Assert.That(Ui.PrizeReveal.IsVisible, Is.True);
        }

        [UnityTest]
        public IEnumerator Relic_emerges_before_pickup_and_survives_pause()
        {
            SetPlayer("Position", new Vector2(140, -40));
            Invoke(_runtime, "SpawnRouletteChest", new Vector2(140, -40));
            Invoke(_runtime, "UpdateRouletteChest", 1f);
            Assert.That(Get(_runtime, "_rouletteActive"), Is.False, "Death burst must register before collection.");
            Assert.That(Get(_runtime, "_rouletteChestActive"), Is.True);
            Set(_runtime, "_paused", true);
            Invoke(_runtime, "UpdateRouletteChest", 2f);
            Assert.That(Get(_runtime, "_rouletteChestActive"), Is.True, "Pause cannot consume a reward.");
            Set(_runtime, "_paused", false);
            Invoke(_runtime, "UpdateRouletteChest", 0f);
            Assert.That(Get(_runtime, "_rouletteChestActive"), Is.False);
            Assert.That(Ui.CurrentScreen, Is.EqualTo(UIScreen.Roulette));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Reward_phase_finishes_defeated_boss_visuals_before_relic_pickup()
        {
            Invoke(_runtime, "StepObjectiveTracker", 300d);
            Invoke(_runtime, "StepObjectiveTracker", 0d);
            Assert.That(_runtime.ActiveBossesCount, Is.EqualTo(2));
            Invoke(_runtime, "KillBoss", 1);
            Invoke(_runtime, "KillBoss", 0);
            Invoke(_runtime, "StepObjectiveTracker", 0d);
            for (var frame = 0; frame < 20; frame++) Invoke(_runtime, "UpdatePhaseFx", 0.1f);
            var bosses = (Array)Get(Get(_runtime, "_gameSim"), "Bosses");
            foreach (var boss in bosses)
                Assert.That((float)Get(boss, "DeathTimer"), Is.Zero, "Combat being stopped must not freeze a defeated boss.");
            Assert.That(Get(_runtime, "_rouletteChestActive"), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Upgraded_parts_cache_awards_the_displayed_amount_exactly_once()
        {
            Set(_runtime, "_partsEarned", 180);
            Invoke(_runtime, "OpenBossRoulette");
            var table = new[] { new RouletteWedgeDefinition(RoulettePrizeKind.Parts, RouletteTier.Standard, 1, "PARTS CACHE", "", "#8bc9dd") };
            var session = new RouletteSession(17, 0, table);
            var rng = new Rng(100);
            Set(_runtime, "_rouletteSession", session);
            Set(_runtime, "_rouletteRng", rng);
            Invoke(_runtime, "OnRouletteComplete", session);
            Assert.That(Get(_runtime, "_partsEarned"), Is.EqualTo(180), "An unspun session cannot claim a prize.");
            RouletteRules.Spin(session, rng);
            Invoke(_runtime, "OnRouletteComplete", session);
            Invoke(_runtime, "OnRouletteComplete", session);
            Assert.That(Get(_runtime, "_partsEarned"), Is.EqualTo(270));
            Assert.That(Ui.CurrentScreen, Is.EqualTo(UIScreen.PrizeReveal));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Walking_into_the_left_portal_commits_its_offered_branch()
        {
            AssertPortalCommits(0);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Walking_into_the_right_portal_commits_its_offered_branch()
        {
            AssertPortalCommits(1);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Junction_clears_hostiles_and_advances_without_advancing_combat_time()
        {
            Assert.That(Invoke(_runtime, "SpawnEnemy", "chaser"), Is.EqualTo(true));
            Invoke(_runtime, "SpawnHostileShot", new Vector2(100f, 100f), Vector2.right,
                10f, 100f, 0f, false, -1, 0f);
            Assert.That(_runtime.ActiveEnemiesCount, Is.GreaterThan(0));
            Assert.That(ActiveHostileShots(), Is.GreaterThan(0));
            BeginJunction();
            Assert.That(_runtime.ActiveEnemiesCount, Is.Zero);
            Assert.That(ActiveHostileShots(), Is.Zero);
            var combatTime = (float)Get(_runtime, "_time");
            var junctionAge = (float)Get(_runtime, "_junctionAge");

            // Exercise both the fixed-step guard and the normal frame owner.
            Invoke(_runtime, "Simulate", 1d / 60d);
            _runtime.enabled = true;
            yield return null;
            yield return null;
            _runtime.enabled = false;

            Assert.That(_runtime.JourneyStatus, Is.EqualTo("Junction"));
            Assert.That((float)Get(_runtime, "_junctionAge"), Is.GreaterThan(junctionAge));
            Assert.That((float)Get(_runtime, "_time"), Is.EqualTo(combatTime));
            Assert.That(_runtime.ActiveEnemiesCount, Is.Zero);
            Assert.That(ActiveHostileShots(), Is.Zero);
            Assert.That(Get(_runtime, "_paused"), Is.False,
                "The junction should be an active safe room, not a frozen combat screen.");
        }

        [UnityTest]
        public IEnumerator Junction_portals_and_floor_have_renderable_materials()
        {
            BeginJunction();
            var root = (GameObject)Get(_runtime, "_junctionRoot");
            foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>())
            {
                Assert.That(renderer.sharedMaterial, Is.Not.Null, renderer.name);
                Assert.That(renderer.sprite, Is.Not.Null, renderer.name);
                Assert.That(renderer.enabled, Is.True, renderer.name);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Junction_removes_outgoing_boss_presentation()
        {
            Invoke(_runtime, "SpawnBoss", "warden", 1d, 1d);
            Invoke(_runtime, "Render");
            var views = (SpriteRenderer[])Get(_runtime, "_bossViews");
            Assert.That(views[0].enabled, Is.True);
            BeginJunction();
            foreach (var view in views)
                if (view != null) Assert.That(view.enabled, Is.False, view.name);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Completing_the_final_void_saves_once_and_returns_home_on_the_next_frame()
        {
            yield return AssertTerminalResult(true);
        }

        [UnityTest]
        public IEnumerator Death_saves_once_and_returns_home_on_the_next_frame()
        {
            yield return AssertTerminalResult(false);
        }

        [UnityTest]
        public IEnumerator Final_void_drains_earned_experience_before_saving_the_result()
        {
            var threshold = (int)Get(_runtime, "_xpNeed");
            Set(_runtime, "_xp", (float)threshold);
            FinishSingleVoidRoute();
            Assert.That(Get(_runtime, "_gameOver"), Is.False,
                "The final result cannot discard an earned upgrade.");
            for (var frame = 0; frame < 20 && !(bool)Get(_runtime, "_gameOver"); frame++)
            {
                Invoke(_runtime, "UpdateJourneyFlow", 0.1f);
                if ((bool)Get(_runtime, "_levelUpActive"))
                {
                    Set(_runtime, "_levelUpPromptOpenedAt", -100f);
                    Invoke(_runtime, "SelectLevelOption", 0);
                }
            }
            Assert.That(Get(_runtime, "_gameOver"), Is.True);
            Assert.That(_testStore.Load().recentRuns[0].level, Is.EqualTo(2));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Final_void_waits_for_an_already_pending_upgrade_prompt()
        {
            Set(_runtime, "_level", 2);
            Invoke(_runtime, "OpenLevelUp");
            FinishSingleVoidRoute();
            Assert.That(Get(_runtime, "_gameOver"), Is.False);
            for (var frame = 0; frame < 10; frame++) Invoke(_runtime, "UpdateJourneyFlow", 0.1f);
            Assert.That(Get(_runtime, "_levelUpActive"), Is.True);
            Set(_runtime, "_levelUpPromptOpenedAt", -100f);
            Invoke(_runtime, "SelectLevelOption", 0);
            Invoke(_runtime, "UpdateJourneyFlow", 0.1f);
            Assert.That(Get(_runtime, "_runSaved"), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator After_revive_final_void_collects_retained_xp_before_draining_upgrades()
        {
            Invoke(_runtime, "SpawnPickup", Vector2.zero, (float)(int)Get(_runtime, "_xpNeed"));
            SetPlayer("Health", 0f);
            Set(_runtime, "_revivesRemaining", 1);
            Set(_runtime, "_revivePending", true);
            Set(_runtime, "_paused", true);
            FinishSingleVoidRoute();
            Invoke(_runtime, "AcceptRevive");
            Invoke(_runtime, "StepVoidCompletionDelay", 0f);
            Assert.That(Get(_runtime, "_gameOver"), Is.False,
                "XP left during death must be collected before final upgrades and saving.");
            for (var frame = 0; frame < 20 && !(bool)Get(_runtime, "_gameOver"); frame++)
            {
                Invoke(_runtime, "UpdateJourneyFlow", 0.1f);
                if ((bool)Get(_runtime, "_levelUpActive"))
                {
                    Set(_runtime, "_levelUpPromptOpenedAt", -100f);
                    Invoke(_runtime, "SelectLevelOption", 0);
                }
            }
            Assert.That(_testStore.Load().recentRuns[0].level, Is.EqualTo(2));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Failed_terminal_save_retains_profile_and_result_until_a_successful_retry()
        {
            Set(_runtime, "_partsEarned", 17);
            Set(_runtime, "_kills", 4);
            var profileBefore = JsonUtility.ToJson(Get(_runtime, "_saveData"));
            var diskBefore = File.ReadAllText(_testStore.PathOnDisk);
            var blockedParent = Path.Combine(_temporaryDirectory, "blocked-parent");
            File.WriteAllText(blockedParent, "A file prevents creation of the save directory.");
            Set(_runtime, "_saveStore", new SaveStore(Path.Combine(blockedParent, "profile.json")));
            LogAssert.Expect(LogType.Error, new Regex("^VoidFall run save failed:"));

            FinishSingleVoidRoute();
            _runtime.enabled = true;
            yield return null;
            _runtime.enabled = false;

            Assert.That(Get(_runtime, "_runSaved"), Is.False);
            Assert.That(Get(_runtime, "_gameOver"), Is.True);
            Assert.That(Get(_runtime, "_mainMenuBrowsing"), Is.False);
            Assert.That(Ui.CurrentScreen, Is.EqualTo(UIScreen.GameOver));
            Assert.That(JsonUtility.ToJson(Get(_runtime, "_saveData")), Is.EqualTo(profileBefore));
            Assert.That(File.ReadAllText(_testStore.PathOnDisk), Is.EqualTo(diskBefore));
            Assert.That(Get(_runtime, "_partsEarned"), Is.EqualTo(17));
            Assert.That(Get(_runtime, "_kills"), Is.EqualTo(4));
            Assert.That(_runtime.JourneyStatus, Is.EqualTo("Complete"));

            Set(_runtime, "_saveStore", _testStore);
            Invoke(_runtime, "StartRun");

            Assert.That(Get(_runtime, "_mainMenuBrowsing"), Is.True,
                "A successful retry should resolve the preserved result before starting another run.");
            AssertSavedResult();
        }

        private IEnumerator AssertTerminalResult(bool victory)
        {
            Set(_runtime, "_partsEarned", 17);
            Set(_runtime, "_kills", 4);
            if (victory)
            {
                FinishSingleVoidRoute();
                Assert.That(Get(_runtime, "_runVictory"), Is.True);
            }
            else
            {
                SetPlayer("Health", 0f);
                SetPlayer("DyingTimer", 0f);
                Set(_runtime, "_revivesRemaining", 0);
                Invoke(_runtime, "EndRun");
                Assert.That(Get(_runtime, "_runVictory"), Is.False);
            }
            Assert.That(Get(_runtime, "_gameOver"), Is.True);
            Assert.That(Get(_runtime, "_mainMenuBrowsing"), Is.False);
            Assert.That(Get(_runtime, "_runSaved"), Is.True);
            AssertSavedResult();
            var savedOnce = File.ReadAllText(_testStore.PathOnDisk);
            Invoke(_runtime, "EndRun");
            Invoke(_runtime, "SaveRun");

            _runtime.enabled = true;
            yield return null;
            _runtime.enabled = false;

            Assert.That(Get(_runtime, "_mainMenuBrowsing"), Is.True);
            Assert.That(Ui.CurrentScreen, Is.EqualTo(UIScreen.Home));
            Invoke(_runtime, "UpdateJourneyFlow", 0.1f);
            Invoke(_runtime, "SaveRun");
            AssertSavedResult();
            Assert.That(File.ReadAllText(_testStore.PathOnDisk), Is.EqualTo(savedOnce),
                "Repeated terminal callbacks and returning Home must not write or award the result twice.");
        }

        private void FinishSingleVoidRoute()
        {
            var route = new VoidRouteRun(new[]
            {
                new VoidRouteNode("abyss", "Abyss", 0, 1d, "BASELINE", "", "Clear the Void", "")
            }, "abyss");
            Set(_runtime, "_voidRoute", route);
            Invoke(_runtime, "OnVoidObjectiveCompleted");
            Assert.That(route.HasEscaped, Is.True);
            Set(_runtime, "_voidCompletionDelayRemaining", 0f);
            Invoke(_runtime, "StepVoidCompletionDelay", 0f);
        }

        private void AssertSavedResult()
        {
            var saved = _testStore.Load();
            Assert.That(saved.stats.totalRuns, Is.EqualTo(8));
            Assert.That(saved.parts, Is.EqualTo(137));
            Assert.That(saved.recentRuns, Has.Length.EqualTo(1));
            Assert.That(saved.recentRuns[0].partsEarned, Is.EqualTo(17));
            Assert.That(saved.recentRuns[0].kills, Is.EqualTo(4));
        }

        private void BeginJunction()
        {
            Invoke(_runtime, "OnVoidObjectiveCompleted");
            Invoke(_runtime, "BeginPortalJunction");
            Assert.That(_runtime.JourneyStatus, Is.EqualTo("Junction"));
        }

        private void AssertPortalCommits(int index)
        {
            BeginJunction();
            var offered = (string[])Get(_runtime, "_junctionDestinations");
            var portals = (SpriteRenderer[])Get(_runtime, "_junctionPortals");
            Assert.That(offered, Has.Length.EqualTo(2));
            var selected = offered[index];
            var rejected = offered[1 - index];
            var route = (VoidRouteRun)Get(_runtime, "_voidRoute");
            Assert.That(route.CurrentVoidId, Is.EqualTo("abyss"));
            Set(_runtime, "_junctionAge", 0.5f);
            SetPlayer("Position", (Vector2)portals[index].transform.position);
            SetPlayer("Velocity", Vector2.zero);

            Invoke(_runtime, "UpdateJourneyFlow", 0.1f);

            Assert.That(_runtime.CurrentVoidId, Is.EqualTo(selected));
            Assert.That(route.History, Is.EqualTo(new[] { "abyss", selected }));
            Assert.That(route.StateOf(selected), Is.EqualTo(RouteNodeState.Selected));
            Assert.That(route.StateOf(rejected), Is.EqualTo(RouteNodeState.Locked));
            Assert.That(_runtime.JourneyStatus, Is.EqualTo("Travel"));
        }

        private int ActiveHostileShots()
        {
            return (int)Invoke(Get(_runtime, "_gameSim"), "ActiveHostileShots");
        }

        private void SetPlayer(string name, object value)
        {
            var simulation = Get(_runtime, "_gameSim");
            var player = Get(simulation, "Player");
            Set(player, name, value);
            Set(simulation, "Player", player);
        }

        private UIManager Ui => (UIManager)Get(_runtime, "_ui");

        private static object Get(object target, string name)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, "Missing field '" + name + "'.");
            return field.GetValue(target);
        }

        private static void Set(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, "Missing field '" + name + "'.");
            field.SetValue(target, value);
        }

        private static object Invoke(object target, string name, params object[] arguments)
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            var method = target.GetType().GetMethod(name, flags, null,
                Array.ConvertAll(arguments, argument => argument?.GetType() ?? typeof(object)), null);
            Assert.That(method, Is.Not.Null, "Missing method '" + name + "'.");
            try
            {
                return method.Invoke(target, arguments);
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException ?? exception;
            }
        }
    }
}
