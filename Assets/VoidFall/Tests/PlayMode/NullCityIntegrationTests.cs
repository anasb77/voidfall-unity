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
    public sealed class NullCityIntegrationTests
    {
        private VoidFallGameRuntime _runtime;
        private SaveStore _previousStore;
        private SaveData _previousProfile;
        private string _temporaryDirectory;
        private bool _previousEnabled;
        private bool _previousApplicationInactive;
        private bool _previousRunSaved;
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
            _previousRunSaved = (bool)Get(_runtime, "_runSaved");
            _previousSeedOverride = (uint)Get(_runtime, "_diagnosticRunSeedOverride");

            _temporaryDirectory = Path.Combine(Path.GetTempPath(),
                "voidfall-null-city-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temporaryDirectory);
            var testStore = new SaveStore(Path.Combine(_temporaryDirectory, "profile.json"));
            var profile = SaveStore.CreateDefault();
            testStore.Save(profile);
            Set(_runtime, "_saveStore", testStore);
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
                Set(_runtime, "_runSaved", true);
                Set(_runtime, "_gameOver", false);
                Set(_runtime, "_saveData", _previousProfile);
                Set(_runtime, "_saveStore", _previousStore);
                Invoke(_runtime, "EnterMainMenu");
                Set(_runtime, "_runSaved", _previousRunSaved);
                Set(_runtime, "_diagnosticRunSeedOverride", _previousSeedOverride);
                Set(_runtime, "_applicationInactive", _previousApplicationInactive);
                _runtime.enabled = _previousEnabled;
            }
            if (!string.IsNullOrEmpty(_temporaryDirectory) && Directory.Exists(_temporaryDirectory))
                Directory.Delete(_temporaryDirectory, true);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Entering_city_resets_encounter_state_and_captures_fixed_origin()
        {
            var expectedOrigin = new Vector2(37f, -82f);
            SetPlayer("Position", expectedOrigin);
            Set(_runtime, "_voidRoute", null);
            Set(_runtime, "_arenaId", ArenaId.NullCity);
            Set(_runtime, "_nullCityElapsed", 91f);
            Set(_runtime, "_nullCityBossElapsed", 14f);
            Set(_runtime, "_nullCityBossActive", true);
            Set(_runtime, "_nullCityBossSpawned", true);
            Set(_runtime, "_nullCityCleared", true);

            Invoke(_runtime, "BeginObjectiveForCurrentArena");
            SetPlayer("Position", expectedOrigin + Vector2.one * 20f);

            Assert.That(Get(_runtime, "_nullCityOrigin"), Is.EqualTo(expectedOrigin));
            Assert.That(Get(_runtime, "_nullCityElapsed"), Is.EqualTo(0f));
            Assert.That(Get(_runtime, "_nullCityBossElapsed"), Is.EqualTo(0f));
            Assert.That(Get(_runtime, "_nullCityBossActive"), Is.False);
            Assert.That(Get(_runtime, "_nullCityBossSpawned"), Is.False);
            Assert.That(Get(_runtime, "_nullCityCleared"), Is.False);
            Assert.That(Get(_runtime, "_nullCityBossSlot"), Is.EqualTo(-1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator City_unit_spawner_creates_only_the_exclusive_roster()
        {
            EnterNullCity();
            for (var type = 0; type < NullCityContent.Enemies.Length; type++)
            {
                var position = new Vector2(type * 20f, 0f);
                Assert.That(Invoke(_runtime, "SpawnNullCityUnit", type, position, false, false),
                    Is.EqualTo(true), NullCityContent.Enemies[type].Id);
            }

            var active = ActiveItems("Enemies");
            Assert.That(active, Has.Count.EqualTo(NullCityContent.Enemies.Length));
            foreach (var enemy in active)
            {
                var id = (string)Get(enemy, "Id");
                Assert.That(id, Does.StartWith("null-"));
                Assert.That(NullCityContent.FindEnemy(id), Is.Not.Null, id);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Broodmother_death_releases_exactly_four_crawlers_without_reviving_parent()
        {
            EnterNullCity();
            Assert.That(Invoke(_runtime, "SpawnNullCityUnit", 7, Vector2.zero, false, false), Is.EqualTo(true));
            var parentSlot = FindActiveEnemySlot("null-broodmother");
            var parentSpawnId = (int)Get(((Array)Get(GameSim, "Enemies")).GetValue(parentSlot), "SpawnId");

            Invoke(_runtime, "KillEnemy", parentSlot);
            Assert.That(Get(_runtime, "_nullCityBirthCount"), Is.EqualTo(4));
            Invoke(_runtime, "ProcessNullCityBirths");

            Assert.That(ActiveEnemyCount("null-crawler"), Is.EqualTo(4));
            Assert.That(ActiveEnemyCount("null-broodmother"), Is.Zero);
            foreach (var enemy in ActiveItems("Enemies"))
                Assert.That(Get(enemy, "SpawnId"), Is.Not.EqualTo(parentSpawnId));
            Assert.That(Get(_runtime, "_nullCityBirthCount"), Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Volatile_chain_reactions_queue_each_new_blast_for_the_next_tick()
        {
            EnterNullCity();
            SetPlayer("Position", new Vector2(1000f, 1000f));
            Assert.That(Invoke(_runtime, "SpawnNullCityUnit", 4, Vector2.zero, false, false), Is.EqualTo(true));
            Assert.That(Invoke(_runtime, "SpawnNullCityUnit", 4, new Vector2(50f, 0f), false, false), Is.EqualTo(true));

            Invoke(_runtime, "KillEnemy", FindActiveEnemySlot("null-volatile"));
            Assert.That(Get(_runtime, "_nullCityBlastCount"), Is.EqualTo(1));

            Invoke(_runtime, "ProcessNullCityBlasts");

            Assert.That(ActiveEnemyCount("null-volatile"), Is.Zero);
            Assert.That(Get(_runtime, "_nullCityBlastCount"), Is.EqualTo(1),
                "The chained volatile must remain queued instead of resolving recursively.");
            Invoke(_runtime, "ProcessNullCityBlasts");
            Assert.That(Get(_runtime, "_nullCityBlastCount"), Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Environment_blast_preserves_logical_enemy_order_after_slot_reuse()
        {
            EnterNullCity();
            SetPlayer("Position", new Vector2(1000f, 1000f));
            Assert.That(Invoke(_runtime, "SpawnNullCityUnit", 7, new Vector2(500f, 0f), false, false), Is.EqualTo(true));
            var aSlot = FindActiveEnemySlot("null-broodmother");
            Assert.That(Invoke(_runtime, "SpawnNullCityUnit", 7, new Vector2(20f, 0f), false, false), Is.EqualTo(true));
            var bSlot = FindLastActiveEnemySlot("null-broodmother");
            Assert.That(Invoke(_runtime, "SpawnNullCityUnit", 3, new Vector2(500f, 100f), false, false), Is.EqualTo(true));

            Invoke(_runtime, "KillEnemy", aSlot);
            Set(_runtime, "_nullCityBirthCount", 0);
            Assert.That(Invoke(_runtime, "SpawnNullCityUnit", 7, new Vector2(-20f, 0f), false, false), Is.EqualTo(true));
            var dSlot = aSlot;
            Assert.That(Get(((Array)Get(GameSim, "Enemies")).GetValue(dSlot), "Id"),
                Is.EqualTo("null-broodmother"));
            Assert.That(dSlot, Is.EqualTo(aSlot));
            Assert.That(dSlot, Is.LessThan(bSlot));

            var bPosition = new Vector2(20f, 0f);
            var dPosition = new Vector2(-20f, 0f);
            SetEnemy(bSlot, "Health", 1f);
            SetEnemy(bSlot, "Position", bPosition);
            SetEnemy(dSlot, "Health", 1f);
            SetEnemy(dSlot, "Position", dPosition);
            var blasts = (Array)Get(_runtime, "_nullCityBlastQueue");
            blasts.SetValue(Vector2.zero, 0);
            Set(_runtime, "_nullCityBlastCount", 1);

            Invoke(_runtime, "ProcessNullCityBlasts");

            var births = (Array)Get(_runtime, "_nullCityBirthQueue");
            Assert.That(Get(_runtime, "_nullCityBirthCount"), Is.EqualTo(8));
            Assert.That((Vector2)births.GetValue(0), Is.EqualTo(bPosition + Vector2.right * 64f),
                "Older B must queue its brood before newer D even though D reused the lower slot.");
            Assert.That((Vector2)births.GetValue(4), Is.EqualTo(dPosition + Vector2.right * 64f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Motherload_begin_activates_a_permanent_lockdown_encounter()
        {
            EnterNullCity();
            Invoke(_runtime, "BeginNullCityBossEncounter");

            Assert.That(Get(_runtime, "_nullCityBossActive"), Is.True);
            Assert.That(Get(_runtime, "_nullCityBossSpawned"), Is.True);
            Assert.That(Get(_runtime, "_nullCityBossElapsed"), Is.EqualTo(0f));
            Assert.That(Get(_runtime, "NullCityLockdown"), Is.True);
            Assert.That(ActiveBossCount(NullCityContent.MotherloadId), Is.EqualTo(1));

            Set(_runtime, "_nullCityElapsed", 10000f);
            Set(_runtime, "_nullCityBossElapsed", 10000f);
            Assert.That(Get(_runtime, "NullCityLockdown"), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Natural_null_city_objective_spawns_motherload_and_enters_reward_flow_on_death()
        {
            EnterNullCity(withRoute: true);
            var route = (VoidRouteRun)Get(_runtime, "_voidRoute");

            Invoke(_runtime, "StepObjectiveTracker", 300d);
            Invoke(_runtime, "StepObjectiveTracker", 0d);

            Assert.That(ActiveBossCount(NullCityContent.MotherloadId), Is.EqualTo(1));
            Assert.That(Get(_runtime, "_nullCityBossActive"), Is.True);
            var bossSlot = (int)Get(_runtime, "_nullCityBossSlot");

            Invoke(_runtime, "KillBoss", bossSlot);
            Invoke(_runtime, "StepObjectiveTracker", 0d);

            Assert.That(Get(Get(_runtime, "_objectives"), "IsComplete"), Is.True);
            Assert.That(Get(_runtime, "_objectivesCompletionHandled"), Is.True);
            Assert.That(route.HasEscaped, Is.True);
            Assert.That(_runtime.JourneyStatus, Is.EqualTo("Rewards"));
            Assert.That(Get(_runtime, "_voidCompletionPending"), Is.True);
            Assert.That(Get(_runtime, "_rouletteChestActive"), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Motherload_death_preserves_dissolution_and_clears_city_hostiles_and_hazards()
        {
            EnterNullCity();
            Invoke(_runtime, "BeginNullCityBossEncounter");
            var bossSlot = (int)Get(_runtime, "_nullCityBossSlot");

            Assert.That(Invoke(_runtime, "SpawnNullCityUnit", 3, Vector2.zero, false, false), Is.EqualTo(true));
            Invoke(_runtime, "SpawnHostileShot", Vector2.zero, Vector2.right,
                12f, 250f, 0f, false, -1, 0f);
            Invoke(_runtime, "QueueNullCityBrood", Vector2.zero, 2, 64f);
            Assert.That(Invoke(_runtime, "SpawnNullCityUnit", 4, new Vector2(300f, 0f), false, false), Is.EqualTo(true));
            Invoke(_runtime, "KillEnemy", FindActiveEnemySlot("null-volatile"));
            PrimeFirstBomb();

            Invoke(_runtime, "KillBoss", bossSlot);

            var defeated = ((Array)Get(GameSim, "Bosses")).GetValue(bossSlot);
            Assert.That(Get(defeated, "Active"), Is.False);
            Assert.That(Get(defeated, "DeathTimer"), Is.EqualTo(1.4f));
            Assert.That(Get(_runtime, "_nullCityCleared"), Is.True);
            Assert.That(Get(_runtime, "_nullCityBossActive"), Is.False);
            Assert.That(ActiveItems("Enemies"), Is.Empty);
            Assert.That(ActiveItems("HostileShots"), Is.Empty);
            Assert.That(Get(_runtime, "_nullCityBirthCount"), Is.Zero);
            Assert.That(Get(_runtime, "_nullCityBlastCount"), Is.Zero);
            foreach (var bomb in (Array)Get(_runtime, "_nullCityBombs"))
                Assert.That(Get(bomb, "Active"), Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator City_dash_grants_a_short_iframe_and_clamps_movement_to_the_city_floor()
        {
            EnterNullCity();
            var origin = (Vector2)Get(_runtime, "_nullCityOrigin");
            var nearRightEdge = origin + new Vector2(615f, 0f);
            SetPlayer("Position", nearRightEdge);
            SetPlayer("Iframes", 0f);
            Set(_runtime, "_nullCityDashRequested", true);

            Invoke(_runtime, "ApplyNullCityMovement", 0.15f, Vector2.right);

            var player = Get(GameSim, "Player");
            Assert.That(Get(player, "Iframes"), Is.EqualTo(0.25f));
            Assert.That(Get(player, "Position"), Is.EqualTo(origin + new Vector2(620f, 0f)));
            Assert.That((float)Get(_runtime, "_nullCityDashCooldown"), Is.GreaterThan(0f));
            Assert.That(Get(_runtime, "_nullCityDashRemaining"), Is.EqualTo(0f));
            yield return null;
        }

        private void EnterNullCity(bool withRoute = false)
        {
            Invoke(_runtime, "ClearHydraBossArena");
            Set(_runtime, "_voidRoute", withRoute ? NullCityRoute() : null);
            Set(_runtime, "_arenaId", ArenaId.NullCity);
            Invoke(_runtime, "BeginObjectiveForCurrentArena");
        }

        private static VoidRouteRun NullCityRoute()
        {
            return new VoidRouteRun(new[]
            {
                new VoidRouteNode(NullCityContent.StableId, NullCityContent.Arena.Name, 0, 1,
                    "PURGE LANES", NullCityContent.Arena.Description,
                    "Survive, then defeat Motherload", "Boss rewards"),
            }, NullCityContent.StableId);
        }

        private object GameSim => Get(_runtime, "_gameSim");

        private ArrayList ActiveItems(string poolName)
        {
            var result = new ArrayList();
            foreach (var item in (Array)Get(GameSim, poolName))
                if ((bool)Get(item, "Active")) result.Add(item);
            return result;
        }

        private int ActiveEnemyCount(string id)
        {
            var count = 0;
            foreach (var enemy in (Array)Get(GameSim, "Enemies"))
                if ((bool)Get(enemy, "Active") && (string)Get(enemy, "Id") == id) count++;
            return count;
        }

        private int ActiveBossCount(string id)
        {
            var count = 0;
            foreach (var boss in (Array)Get(GameSim, "Bosses"))
                if ((bool)Get(boss, "Active") && (string)Get(boss, "Id") == id) count++;
            return count;
        }

        private int FindActiveEnemySlot(string id)
        {
            var enemies = (Array)Get(GameSim, "Enemies");
            for (var index = 0; index < enemies.Length; index++)
            {
                var enemy = enemies.GetValue(index);
                if ((bool)Get(enemy, "Active") && (string)Get(enemy, "Id") == id) return index;
            }
            Assert.Fail("Missing active enemy '" + id + "'.");
            return -1;
        }

        private int FindLastActiveEnemySlot(string id)
        {
            var enemies = (Array)Get(GameSim, "Enemies");
            for (var index = enemies.Length - 1; index >= 0; index--)
            {
                var enemy = enemies.GetValue(index);
                if ((bool)Get(enemy, "Active") && (string)Get(enemy, "Id") == id) return index;
            }
            Assert.Fail("Missing active enemy '" + id + "'.");
            return -1;
        }

        private void SetEnemy(int slot, string name, object value)
        {
            var enemies = (Array)Get(GameSim, "Enemies");
            var enemy = enemies.GetValue(slot);
            Set(enemy, name, value);
            enemies.SetValue(enemy, slot);
        }

        private void PrimeFirstBomb()
        {
            var bombs = (Array)Get(_runtime, "_nullCityBombs");
            var bomb = bombs.GetValue(0);
            Set(bomb, "Active", true);
            Set(bomb, "Position", Vector2.zero);
            Set(bomb, "Remaining", 1f);
            bombs.SetValue(bomb, 0);
        }

        private void SetPlayer(string name, object value)
        {
            var player = Get(GameSim, "Player");
            Set(player, name, value);
            Set(GameSim, "Player", player);
        }

        private static object Get(object target, string name)
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            var field = target.GetType().GetField(name, flags);
            if (field != null) return field.GetValue(target);
            var property = target.GetType().GetProperty(name, flags);
            Assert.That(property, Is.Not.Null, "Missing field or property '" + name + "'.");
            return property.GetValue(target);
        }

        private static void Set(object target, string name, object value)
        {
            var field = target.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
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
