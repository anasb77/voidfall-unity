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
    public sealed class EscapePolishTests
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
            Call("DestroyEnemiesForVoidTransition");
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

        [UnityTest]
        public IEnumerator New_voids_arrive_with_their_own_prepared_arena()
        {
            foreach (var id in new[] { "crascendo", "eon-sea" })
            {
                Call("StartRun");
                Call("DestroyEnemiesForVoidTransition");
                var overclock = new OverclockState(); overclock.ApplyPickup();
                Set("_overclock", overclock);
                var wanted = id == "crascendo" ? ArenaId.Crascendo : ArenaId.EonSea;
                var nodeId = id + "-repeat-2";
                var displayName = id == "crascendo" ? "Crascendo" : "Eon Sea";
                Set("_voidRoute", new VoidRouteRun(new[]
                {
                    new VoidRouteNode("abyss", "abyss", "Abyss", 0, 1, "", "", "", "", nodeId),
                    new VoidRouteNode(nodeId, id, displayName, 1, 1, "", "", "", "")
                }, "abyss"));
                Call("OnVoidObjectiveCompleted");
                Call("StepVoidCompletionDelay", 40f);
                var deadline = Time.realtimeSinceStartup + 30f;
                while (_runtime.JourneyStatus == "Travel" && Time.realtimeSinceStartup < deadline)
                {
                    Call("UpdateJourneyFlow", 0.1f);
                    yield return null;
                }
                Assert.That(_runtime.JourneyStatus, Is.EqualTo("Combat"));
                Assert.That(_runtime.CurrentVoidId, Is.EqualTo(nodeId));
                Assert.That(Get("_arenaId"), Is.EqualTo(wanted));
                var plates = (Array)Get("_preparedArenaPlateAssets");
                var plate = plates.GetValue((int)wanted);
                Assert.That(plate, Is.Not.Null);
                Assert.That(plate.GetType().GetProperty("Arena").GetValue(plate), Is.EqualTo(wanted));
                var tracker = (VoidObjectiveTracker)Get("_objectives");
                Assert.That(tracker.HasObjective, Is.True);
                Assert.That(tracker.Text, Does.StartWith(displayName.ToUpperInvariant() + " | Phase 1/2:"));
                Assert.That(((OverclockState)Get("_overclock")).RemainingSeconds, Is.EqualTo(15f));
                Set("_freezeTimer", 0f);
                Call("Simulate", 0.1d);
                Assert.That(((OverclockState)Get("_overclock")).RemainingSeconds, Is.LessThan(15f));
            }
        }

        [Test]
        public void Escape_departs_after_fifteen_active_seconds()
        {
            Call("OnVoidObjectiveCompleted");
            Call("StepVoidCompletionDelay", 14.9f);
            Assert.That(_runtime.JourneyStatus, Is.EqualTo("Rewards"));
            Call("StepVoidCompletionDelay", 0.2f);
            Assert.That(_runtime.JourneyStatus, Is.EqualTo("Junction"));
        }

        [Test]
        public void Distant_earned_xp_and_parts_are_recovered_once_before_departure()
        {
            Set("_xpNeed", 1000000);
            Call("SpawnPickup", new Vector2(8000, 5000), 7f);
            // PickupKind is nested in the runtime's simulation types in this project.
            var method = Array.Find(_runtime.GetType().GetMethods(Flags), item => item.Name == "SpawnSpecialPickup");
            var part = Enum.Parse(method.GetParameters()[2].ParameterType, "Part");
            method.Invoke(_runtime, new[] { (object)new Vector2(-6000, -9000), 9f, part });
            Call("OnVoidObjectiveCompleted");
            for (var i = 0; i < 155; i++) Call("UpdateJourneyFlow", 0.1f);
            Assert.That((float)Get("_xp"), Is.EqualTo(7f));
            Assert.That((int)Get("_partsEarned"), Is.EqualTo(9));
            Assert.That(_runtime.JourneyStatus, Is.EqualTo("Junction"));
            for (var i = 0; i < 10; i++) Call("UpdateJourneyFlow", 0.1f);
            Assert.That((float)Get("_xp"), Is.EqualTo(7f));
            Assert.That((int)Get("_partsEarned"), Is.EqualTo(9));
        }

        [Test]
        public void Enemies_retire_in_sequence_instead_of_disappearing_on_completion()
        {
            Set("_xpNeed", 1000000);
            for (var i = 0; i < 12; i++) Call("SpawnEnemy", "chaser");
            var sim = Get("_gameSim");
            var count = sim.GetType().GetField("EnemyOrderCount", Flags);
            var before = (int)count.GetValue(sim);
            Assert.That(before, Is.GreaterThanOrEqualTo(12));
            Call("OnVoidObjectiveCompleted");
            Assert.That((int)count.GetValue(sim), Is.EqualTo(before));
            for (var i = 0; i < 35; i++) Call("UpdateJourneyFlow", 0.1f);
            Assert.That((int)count.GetValue(sim), Is.InRange(1, before - 1));
            for (var i = 0; i < 70; i++) Call("UpdateJourneyFlow", 0.1f);
            Assert.That((int)count.GetValue(sim), Is.Zero);
        }

        [Test]
        public void Escape_render_keeps_enemy_bodies_but_hides_attack_presentation_every_frame()
        {
            Call("SpawnEnemy", "exploder");
            Call("SpawnEnemy", "mortar");
            Call("SpawnEnemy", "elite");
            var exploder = EnemySlotAt(0);
            var mortar = EnemySlotAt(1);
            var elite = EnemySlotAt(2);
            SetEnemyField(exploder, "State", 1);
            SetEnemyField(exploder, "StateTimer", 0.45f);
            SetEnemyField(exploder, "Age", 2f);
            SetEnemyField(mortar, "State", 1);
            SetEnemyField(mortar, "StateTimer", 0.5f);
            SetEnemyField(mortar, "AimPosition", new Vector2(80f, 40f));
            SetEnemyField(mortar, "Age", 2f);
            SetEnemyField(elite, "State", 1);
            SetEnemyField(elite, "StateTimer", 0.5f);
            SetEnemyField(elite, "DashDirection", Vector2.right);
            SetEnemyField(elite, "Age", 2f);

            Set("_ambientClock", 0.05f);
            Call("Render");
            Assert.That(RendererAt("_enemyExploderWarningViews", exploder).enabled, Is.True);
            Assert.That(RendererAt("_enemyTelegraphMortarFillViews", mortar).enabled, Is.True);
            Assert.That(RendererAt("_eliteChargeFillRenderers", elite).enabled, Is.True);

            Call("OnVoidObjectiveCompleted");
            Set("_ambientClock", 0f);
            Call("Render");
            var firstEscapeScale = RendererAt("_enemyViews", exploder).transform.localScale;
            Set("_ambientClock", 0.05f);
            Call("Render");

            Assert.That(RendererAt("_enemyViews", exploder).enabled, Is.True);
            Assert.That(RendererAt("_enemyViews", mortar).enabled, Is.True);
            Assert.That(RendererAt("_enemyViews", elite).enabled, Is.True);
            Assert.That(RendererAt("_enemyViews", exploder).transform.localScale, Is.EqualTo(firstEscapeScale));
            Assert.That(RendererAt("_enemyExploderWarningViews", exploder).enabled, Is.False);
            Assert.That(RendererAt("_enemyTelegraphMortarFillViews", mortar).enabled, Is.False);
            Assert.That(RendererAt("_eliteChargeFillRenderers", elite).enabled, Is.False);
            Assert.That(RendererAt("_eliteChargeArrowFillRenderers", elite).enabled, Is.False);
        }

        [Test]
        public void Mystery_portals_show_actual_names_and_destination_colors()
        {
            var crascendo = new VoidRouteNode("crascendo-repeat-2", "crascendo", "Crascendo", 1, 1, "", "", "", "")
            {
                IsMystery = true
            };
            var eonSea = new VoidRouteNode("eon-sea-repeat-2", "eon-sea", "Eon Sea", 1, 1, "", "", "", "")
            {
                IsMystery = true
            };
            var route = new VoidRouteRun(new[]
            {
                new VoidRouteNode("abyss", "abyss", "Abyss", 0, 1, "", "", "", "", crascendo.Id, eonSea.Id),
                crascendo,
                eonSea
            }, "abyss");
            Assert.That(route.NotifyVoidCompleted("abyss"), Is.True);
            Set("_voidRoute", route);

            Call("BeginPortalJunction");

            var labels = (UnityEngine.UI.Text[])Get("_junctionLabels");
            var portals = (SpriteRenderer[])Get("_junctionPortals");
            Assert.That(labels[0].text, Is.EqualTo("CRASCENDO"));
            Assert.That(labels[1].text, Is.EqualTo("EON SEA"));
            AssertColor(portals[0].color, new Color(186f / 255f, 123f / 255f, 237f / 255f, 1f));
            AssertColor(portals[1].color, new Color(167f / 255f, 210f / 255f, 235f / 255f, 1f));
        }

        [Test]
        public void Escape_part_drop_with_full_pool_uses_normal_grant_effects_once()
        {
            var spawnSpecial = Array.Find(_runtime.GetType().GetMethods(Flags), item => item.Name == "SpawnSpecialPickup");
            var part = Enum.Parse(spawnSpecial.GetParameters()[2].ParameterType, "Part");
            var pickups = (Array)GetGameSimField("Pickups");
            for (var slot = 0; slot < pickups.Length - 1; slot++)
                Assert.That(spawnSpecial.Invoke(_runtime, new[] { (object)new Vector2(9000f, 9000f), 1f, part }), Is.True);
            Call("SpawnEnemy", "chaser");
            var enemySlot = EnemySlotAt(0);
            SetEnemyField(enemySlot, "Xp", 0f);
            SetGameSimField("Rng", new Rng(7));
            var floatersBefore = OrderCount("_floaterOrder");

            Call("OnVoidObjectiveCompleted");
            Set("_voidCompletionDelayRemaining", 0f);
            Call("StepEscapeEnemyRetirement");
            Call("StepEscapeEnemyRetirement");

            Assert.That((int)Get("_partsEarned"), Is.EqualTo(1));
            Assert.That(OrderCount("_floaterOrder"), Is.EqualTo(floatersBefore + 1));
            var pickupRecord = TelemetryPickup("part");
            Assert.That(pickupRecord, Is.Not.Null);
            Assert.That((int)pickupRecord.GetType().GetField("count", Flags).GetValue(pickupRecord), Is.EqualTo(1));
            Assert.That((float)pickupRecord.GetType().GetField("totalValue", Flags).GetValue(pickupRecord), Is.EqualTo(1f));
        }

        [Test]
        public void Escape_recovers_stored_xp_only_from_harvesters()
        {
            Set("_xpNeed", 1000000);
            Call("SpawnEnemy", "court-pawn");
            Call("SpawnEnemy", "harvester");
            var pawn = EnemySlotAt(0);
            var harvester = EnemySlotAt(1);
            SetEnemyField(pawn, "Xp", 0f);
            SetEnemyField(pawn, "StoredXp", 1f);
            SetEnemyField(pawn, "Position", new Vector2(700f, 0f));
            SetEnemyField(harvester, "Xp", 0f);
            SetEnemyField(harvester, "StoredXp", 7f);
            SetEnemyField(harvester, "Position", new Vector2(800f, 0f));
            SetGameSimField("Rng", new Rng(2));

            Call("OnVoidObjectiveCompleted");
            Set("_voidCompletionDelayRemaining", 0f);
            Call("StepEscapeEnemyRetirement");

            Assert.That(GroundPickupValue("Xp"), Is.EqualTo(7f));
        }

        [Test]
        public void Roulette_completion_grants_once_and_resumes_without_an_extra_popup()
        {
            Call("OnVoidObjectiveCompleted");
            Call("OpenBossRoulette");
            var session = (RouletteSession)Get("_rouletteSession");
            RouletteRules.Spin(session, (Rng)Get("_rouletteRng"));
            Call("OnRouletteComplete", session);
            Assert.That((bool)Get("_prizeRevealActive"), Is.False);
            Assert.That((bool)Get("_paused"), Is.False);
            var count = (int)Get("_rouletteCeremoniesSeen");
            Call("OnRouletteComplete", session);
            Assert.That((int)Get("_rouletteCeremoniesSeen"), Is.EqualTo(count));
            Call("UpdateJourneyFlow", 1f);
            Assert.That((float)Get("_voidCompletionDelayRemaining"), Is.LessThan(15f));
        }

        [Test]
        public void Overclock_and_escape_clock_stay_frozen_under_the_map()
        {
            var state = new OverclockState(); state.ApplyPickup();
            Set("_overclock", state);
            Call("OnVoidObjectiveCompleted");
            Call("ToggleRouteMap");
            for (var i = 0; i < 100; i++) Call("UpdateJourneyFlow", 0.1f);
            Assert.That(((OverclockState)Get("_overclock")).RemainingSeconds, Is.EqualTo(15f));
            Assert.That((float)Get("_voidCompletionDelayRemaining"), Is.EqualTo(15f));
            Call("CloseRouteMap");
            for (var i = 0; i < 155; i++) Call("UpdateJourneyFlow", 0.1f);
            Assert.That(((OverclockState)Get("_overclock")).RemainingSeconds, Is.EqualTo(15f));
            Assert.That(_runtime.JourneyStatus, Is.EqualTo("Junction"));
        }

        [Test]
        public void Escape_shake_escalates_varies_and_honors_pause_and_motion_settings()
        {
            var settings = ((SaveData)Get("_saveData")).settings;
            settings.shake = 1f; settings.reducedMotion = false;
            Call("OnVoidObjectiveCompleted");
            var early = 0f; var late = 0f;
            var method = _runtime.GetType().GetMethod("CameraShakeOffset", Flags);
            for (var sample = 0; sample < 20; sample++)
            {
                Set("_voidCompletionDelayRemaining", 14f - sample * 0.03f);
                early += ((Vector2)method.Invoke(_runtime, null)).sqrMagnitude;
                Set("_voidCompletionDelayRemaining", 1f - sample * 0.03f);
                late += ((Vector2)method.Invoke(_runtime, null)).sqrMagnitude;
            }
            Assert.That(late, Is.GreaterThan(early * 4f));
            Set("_paused", true);
            Assert.That(method.Invoke(_runtime, null), Is.EqualTo(Vector2.zero));
            Set("_paused", false); settings.reducedMotion = true;
            Assert.That(method.Invoke(_runtime, null), Is.EqualTo(Vector2.zero));
            settings.reducedMotion = false; settings.shake = 0f;
            Assert.That(method.Invoke(_runtime, null), Is.EqualTo(Vector2.zero));
            var patterns = new System.Collections.Generic.HashSet<int> { (int)Get("_escapeShakePattern") };
            var route = (VoidRouteRun)Get("_voidRoute");
            for (var clear = 0; clear < 2; clear++)
            {
                Assert.That(route.SelectNextVoid(route.NodesInState(RouteNodeState.Available)[0]), Is.True);
                Call("OnVoidObjectiveCompleted");
                patterns.Add((int)Get("_escapeShakePattern"));
            }
            Assert.That(patterns.Count, Is.EqualTo(3));
        }

        [Test]
        public void Overclock_collected_during_escape_keeps_its_entire_charge()
        {
            Call("OnVoidObjectiveCompleted");
            var method = Array.Find(_runtime.GetType().GetMethods(Flags), item => item.Name == "SpawnSpecialPickup");
            method.Invoke(_runtime, new[] { (object)PlayerPosition(), 1f, Enum.Parse(method.GetParameters()[2].ParameterType, "Overdrive") });
            Call("UpdateJourneyFlow", 0.1f);
            Assert.That(((OverclockState)Get("_overclock")).RemainingSeconds, Is.EqualTo(15f));
            for (var i = 0; i < 100; i++) Call("UpdateJourneyFlow", 0.1f);
            Assert.That(((OverclockState)Get("_overclock")).RemainingSeconds, Is.EqualTo(15f));
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
        private int EnemySlotAt(int order)
        {
            var sim = Get("_gameSim");
            return ((int[])sim.GetType().GetField("EnemyOrder", Flags).GetValue(sim))[order];
        }
        private void SetEnemyField(int slot, string name, object value)
        {
            var enemies = (Array)GetGameSimField("Enemies");
            var enemy = enemies.GetValue(slot);
            enemy.GetType().GetField(name, Flags).SetValue(enemy, value);
            enemies.SetValue(enemy, slot);
        }
        private object GetGameSimField(string name)
        {
            var sim = Get("_gameSim");
            return sim.GetType().GetField(name, Flags).GetValue(sim);
        }
        private void SetGameSimField(string name, object value)
        {
            var sim = Get("_gameSim");
            sim.GetType().GetField(name, Flags).SetValue(sim, value);
        }
        private Renderer RendererAt(string field, int slot) => (Renderer)((Array)Get(field)).GetValue(slot);
        private int OrderCount(string field)
        {
            var order = Get(field);
            return (int)order.GetType().GetProperty("Count", Flags).GetValue(order);
        }
        private object TelemetryPickup(string id)
        {
            var telemetry = Get("_telemetry");
            var pickups = (IList)telemetry.GetType().GetField("_pickups", Flags).GetValue(telemetry);
            foreach (var pickup in pickups)
                if ((string)pickup.GetType().GetField("id", Flags).GetValue(pickup) == id) return pickup;
            return null;
        }
        private float GroundPickupValue(string kind)
        {
            var pickups = (Array)GetGameSimField("Pickups");
            var total = 0f;
            foreach (var pickup in pickups)
            {
                var type = pickup.GetType();
                if ((bool)type.GetField("Active", Flags).GetValue(pickup) &&
                    type.GetField("Kind", Flags).GetValue(pickup).ToString() == kind)
                    total += (float)type.GetField("Value", Flags).GetValue(pickup);
            }
            return total;
        }
        private static void AssertColor(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.001f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.001f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.001f));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.001f));
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
