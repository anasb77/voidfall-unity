using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoidFall.Core;
using VoidFall.Persistence;
using VoidFall.Runtime;

namespace VoidFall.Tests.PlayMode
{
    public sealed class EscapeWindowTests
    {
        private VoidFallGameRuntime _runtime;
        private SaveStore _originalStore;
        private SaveData _originalProfile;
        private bool _originalEnabled;
        private bool _originalInactive;
        private uint _originalSeed;
        private string _directory;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return null;
            _runtime = UnityEngine.Object.FindAnyObjectByType<VoidFallGameRuntime>();
            Assert.That(_runtime, Is.Not.Null);
            _originalEnabled = _runtime.enabled;
            _runtime.enabled = false;
            _originalStore = (SaveStore)Get("_saveStore");
            _originalProfile = (SaveData)Get("_saveData");
            _originalInactive = (bool)Get("_applicationInactive");
            _originalSeed = (uint)Get("_diagnosticRunSeedOverride");
            _directory = Path.Combine(Path.GetTempPath(), "voidfall-escape-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            Set("_saveStore", new SaveStore(Path.Combine(_directory, "profile.json")));
            Set("_saveData", SaveStore.CreateDefault());
            Set("_runSaved", true);
            Set("_gameOver", false);
            Set("_applicationInactive", false);
            Set("_diagnosticRunSeedOverride", 2848592627u);
            Call("StartRun");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Set("_runSaved", true);
            Set("_gameOver", false);
            Set("_saveStore", _originalStore);
            Set("_saveData", _originalProfile);
            Call("EnterMainMenu");
            Set("_applicationInactive", _originalInactive);
            Set("_diagnosticRunSeedOverride", _originalSeed);
            _runtime.enabled = _originalEnabled;
            Directory.Delete(_directory, true);
            yield return null;
        }

        [Test]
        public void Clear_keeps_far_loot_on_the_ground_and_does_not_move_the_player()
        {
            var position = new Vector2(2200f, 1300f);
            SetPlayerPosition(position);
            Call("SpawnPickup", position + Vector2.right * 600f, 3f);
            Call("OnVoidObjectiveCompleted");
            Assert.That(PlayerPosition(), Is.EqualTo(position));
            Assert.That(FarXpValue(), Is.EqualTo(3f));
            Assert.That(Get("_xp"), Is.EqualTo(0f));
            Assert.That(Get("_objectiveLine"), Does.Contain("INITIATING ESCAPE"));
            Call("UpdateJourneyFlow", 0.1f);
            Assert.That(FarXpValue(), Is.EqualTo(3f), "The grace period must use normal pickup range.");
        }

        [Test]
        public void Escape_waits_25_seconds_then_counts_down_ten_seconds_before_the_crossing()
        {
            Call("OnVoidObjectiveCompleted");
            Call("StepVoidCompletionDelay", 24.9f);
            Assert.That(_runtime.JourneyStatus, Is.EqualTo("Rewards"));
            Assert.That(Get("_objectiveLine"), Does.Contain("INITIATING ESCAPE"));
            Call("StepVoidCompletionDelay", 0.2f);
            Assert.That(Get("_objectiveLine"), Does.Contain("ESCAPING IN 10"));
            Call("StepVoidCompletionDelay", 8.8f);
            Assert.That(Get("_objectiveLine"), Does.Contain("ESCAPING IN 2"));
            Call("StepVoidCompletionDelay", 0.2f);
            Assert.That(Get("_objectiveLine"), Does.Contain("ESCAPING IN 1"));
            Assert.That(_runtime.JourneyStatus, Is.EqualTo("Rewards"));
            Call("StepVoidCompletionDelay", 1f);
            Assert.That(_runtime.JourneyStatus, Is.EqualTo("Junction"));
            Assert.That(((string[])Get("_junctionDestinations")).Length, Is.EqualTo(2));
        }

        [Test]
        public void Slow_frames_do_not_stretch_the_escape_window_and_pause_still_holds_it()
        {
            Call("OnVoidObjectiveCompleted");
            for (var frame = 0; frame < 50; frame++) Call("UpdateJourneyFlow", 0.5f);
            Assert.That((float)Get("_voidCompletionDelayRemaining"), Is.EqualTo(10f).Within(0.01f));
            Assert.That(Get("_objectiveLine"), Does.Contain("ESCAPING IN 10"));
            Set("_paused", true);
            Call("UpdateJourneyFlow", 2f);
            Assert.That((float)Get("_voidCompletionDelayRemaining"), Is.EqualTo(10f).Within(0.01f));
            Set("_paused", false);
            for (var frame = 0; frame < 20; frame++) Call("UpdateJourneyFlow", 0.5f);
            Assert.That(_runtime.JourneyStatus, Is.EqualTo("Junction"));
        }

        [Test]
        public void Early_relic_continue_preserves_the_remaining_escape_window()
        {
            Call("OnVoidObjectiveCompleted");
            Call("StepVoidCompletionDelay", 4f);
            Call("SpawnRouletteChest", PlayerPosition());
            Set("_rouletteChestPulse", 2f);
            Call("CollectRouletteChest");
            FinishRoulette();
            Assert.That(_runtime.JourneyStatus, Is.EqualTo("Rewards"));
            Assert.That((float)Get("_voidCompletionDelayRemaining"), Is.EqualTo(31f).Within(0.01f));
        }

        [Test]
        public void An_unclaimed_relic_is_delivered_before_the_final_countdown()
        {
            Call("OnVoidObjectiveCompleted");
            Call("SpawnRouletteChest", PlayerPosition() + Vector2.right * 800f);
            Set("_rouletteChestPulse", 2f);
            Call("StepVoidCompletionDelay", 25f);
            Assert.That(Get("_rouletteActive"), Is.True);
            Assert.That(Get("_rouletteChestActive"), Is.False);
            FinishRoulette();
            Assert.That(_runtime.JourneyStatus, Is.EqualTo("Rewards"));
            Assert.That((float)Get("_voidCompletionDelayRemaining"), Is.EqualTo(10f).Within(0.01f));
            Assert.That(Get("_objectiveLine"), Does.Contain("ESCAPING IN 10"));
        }

        [Test]
        public void The_relic_stays_at_a_boss_death_site_far_from_world_origin()
        {
            var position = new Vector2(5100f, -2300f);
            Call("SpawnRouletteChest", position);
            Assert.That(Get("_rouletteRelicPosition"), Is.EqualTo(position));
            Assert.That((Vector2)((SpriteRenderer)Get("_rouletteChestBody")).transform.position, Is.EqualTo(position));
        }

        [Test]
        public void The_camera_follows_the_player_during_loot_collection()
        {
            SetPlayerPosition(new Vector2(2200f, 1200f));
            Set("_cameraFollowPosition", Vector2.zero);
            Call("OnVoidObjectiveCompleted");
            var before = Vector2.Distance(PlayerPosition(), (Vector2)Get("_cameraFollowPosition"));
            Call("UpdateJourneyFlow", 0.1f);
            Assert.That(Vector2.Distance(PlayerPosition(), (Vector2)Get("_cameraFollowPosition")), Is.LessThan(before));
        }

        [Test]
        public void Completed_objective_refresh_cannot_replace_the_escape_status()
        {
            Call("StepObjectiveTracker", 300d);
            Call("StepObjectiveTracker", 0d);
            Call("KillBoss", 1);
            Call("KillBoss", 0);
            Call("StepObjectiveTracker", 0.5d);
            Call("UpdateJourneyFlow", 0.1f);
            Assert.That(Get("_objectiveLine"), Does.Contain("INITIATING ESCAPE"));
        }

        [Test]
        public void Collection_uses_normal_range_for_xp_and_parts()
        {
            var drop = new Vector2(900f, 0f);
            Call("SpawnPickup", drop, 3f);
            var sim = Get("_gameSim");
            var pickups = (Array)sim.GetType().GetField("Pickups", Flags).GetValue(sim);
            var kindType = pickups.GetType().GetElementType().GetField("Kind", Flags).FieldType;
            Call("SpawnSpecialPickup", drop, 4f, Enum.Parse(kindType, "Part"));
            Call("OnVoidObjectiveCompleted");
            Call("UpdateJourneyFlow", 0.1f);
            Assert.That(Get("_xp"), Is.EqualTo(0f));
            Assert.That(Get("_partsEarned"), Is.EqualTo(0));
            SetPlayerPosition(drop);
            Call("UpdateJourneyFlow", 0.1f);
            Assert.That(Get("_xp"), Is.EqualTo(3f));
            Assert.That(Get("_partsEarned"), Is.EqualTo(4));
            Assert.That(_runtime.JourneyStatus, Is.EqualTo("Rewards"));
        }

        [Test]
        public void Uncollected_loot_is_not_carried_into_the_portal_room()
        {
            Call("SpawnPickup", new Vector2(2000f, 0f), 3f);
            Call("OnVoidObjectiveCompleted");
            Call("StepVoidCompletionDelay", 35f);
            Assert.That(_runtime.JourneyStatus, Is.EqualTo("Junction"));
            Assert.That(_runtime.ActivePickupsCount, Is.Zero);
            Assert.That(Get("_xp"), Is.EqualTo(0f));
        }

        [Test]
        public void A_single_exit_also_waits_for_the_full_countdown()
        {
            var route = new VoidRouteRun(new[]
            {
                new VoidRouteNode("abyss", "Abyss", 0, 1, "", "", "", "", "hydra"),
                new VoidRouteNode("hydra", "Hydra", 1, 1, "", "", "", ""),
            }, "abyss");
            Set("_voidRoute", route);
            Call("OnVoidObjectiveCompleted");
            Call("StepVoidCompletionDelay", 34.9f);
            Assert.That(route.CurrentVoidId, Is.EqualTo("abyss"));
            Call("StepVoidCompletionDelay", 0.2f);
            Assert.That(route.CurrentVoidId, Is.EqualTo("hydra"));
            Assert.That(_runtime.JourneyStatus, Is.EqualTo("Travel"));
        }

        [Test]
        public void Pause_freezes_the_escape_clock()
        {
            Call("OnVoidObjectiveCompleted");
            var before = (float)Get("_voidCompletionDelayRemaining");
            Call("ToggleRouteMap");
            for (var frame = 0; frame < 50; frame++) Call("UpdateJourneyFlow", 0.1f);
            Assert.That(Get("_voidCompletionDelayRemaining"), Is.EqualTo(before));
        }

        private void FinishRoulette()
        {
            var session = (RouletteSession)Get("_rouletteSession");
            RouletteRules.Spin(session, (Rng)Get("_rouletteRng"));
            Call("OnRouletteComplete", session);
            Call("ClosePrizeReveal");
        }
        private Vector2 PlayerPosition()
        {
            var sim = Get("_gameSim");
            var player = sim.GetType().GetField("Player", Flags).GetValue(sim);
            return (Vector2)player.GetType().GetField("Position", Flags).GetValue(player);
        }
        private float FarXpValue()
        {
            var sim = Get("_gameSim");
            var pickups = (Array)sim.GetType().GetField("Pickups", Flags).GetValue(sim);
            var total = 0f;
            foreach (var pickup in pickups)
            {
                var type = pickup.GetType();
                if (!(bool)type.GetField("Active", Flags).GetValue(pickup) ||
                    type.GetField("Kind", Flags).GetValue(pickup).ToString() != "Xp") continue;
                var position = (Vector2)type.GetField("Position", Flags).GetValue(pickup);
                if (Vector2.Distance(position, PlayerPosition()) > 300f)
                    total += (float)type.GetField("Value", Flags).GetValue(pickup);
            }
            return total;
        }
        private void SetPlayerPosition(Vector2 position)
        {
            var sim = Get("_gameSim");
            var field = sim.GetType().GetField("Player", Flags);
            var player = field.GetValue(sim);
            player.GetType().GetField("Position", Flags).SetValue(player, position);
            field.SetValue(sim, player);
        }
        private object Get(string name) => _runtime.GetType().GetField(name, Flags).GetValue(_runtime);
        private void Set(string name, object value) => _runtime.GetType().GetField(name, Flags).SetValue(_runtime, value);
        private void Call(string name, params object[] args)
        {
            foreach (var method in _runtime.GetType().GetMethods(Flags))
            {
                if (method.Name != name || method.GetParameters().Length != args.Length) continue;
                try { method.Invoke(_runtime, args); }
                catch (TargetInvocationException exception) { throw exception.InnerException ?? exception; }
                return;
            }
            throw new MissingMethodException(name);
        }
    }
}
