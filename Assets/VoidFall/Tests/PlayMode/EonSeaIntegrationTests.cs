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
    public sealed class EonSeaIntegrationTests
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
                "voidfall-eon-sea-tests-" + Guid.NewGuid().ToString("N"));
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

        [TestCase(1)]
        [TestCase(3)]
        public void Crowded_enemies_remain_outside_glaciers_after_separation(int passes)
        {
            EnterTerrain();
            var terrain = (EonSeaTerrain)Get(_runtime, "_eonSeaTerrain");
            foreach (var piece in terrain.Ice) piece.Melt = 1;
            var ice = terrain.Ice[0];
            ice.X = 0; ice.Y = 0; ice.Angle = 0; ice.Length = 100; ice.Radius = 20; ice.Melt = 0;
            var enemies = (Array)Get(GameSim, "Enemies");
            Array.Clear(enemies, 0, enemies.Length);
            for (var index = 0; index < 2; index++)
            {
                var enemy = enemies.GetValue(index);
                Set(enemy, "Active", true);
                Set(enemy, "Id", "chaser");
                Set(enemy, "SpawnId", index + 1);
                Set(enemy, "Health", 10f);
                Set(enemy, "Radius", 13f);
                Set(enemy, "Position", new Vector2(0, 33.01f + index * 10f));
                enemies.SetValue(enemy, index);
            }
            Invoke(_runtime, "ResetEnemyOrder");
            Invoke(_runtime, "AppendEnemyOrder", 0);
            Invoke(_runtime, "AppendEnemyOrder", 1);
            for (var pass = 0; pass < passes; pass++)
            {
                Invoke(_runtime, "RebuildEnemyGrid");
                Invoke(_runtime, "SeparateEnemies");
                for (var index = 0; index < 2; index++)
                {
                    var position = (Vector2)Get(enemies.GetValue(index), "Position");
                    Assert.That(EonSeaTerrain.Inside(ice, position.x, position.y, 13f), Is.False,
                        "Separation pushed an enemy inside cover on pass " + pass);
                }
            }
            Assert.That(((Vector2)Get(enemies.GetValue(1), "Position")).y, Is.GreaterThan(43.01f),
                "The fix must preserve separation rather than disable it.");
        }

        [Test]
        public void Collapse_freezes_without_damage_and_rewards_clear_frost()
        {
            EnterTerrain();
            var terrain = (EonSeaTerrain)Get(_runtime, "_eonSeaTerrain");
            var ice = terrain.Ice[0];
            SetPlayer("Position", new Vector2(ice.X, ice.Y));
            var health = (float)Get(Get(GameSim, "Player"), "Health");
            ice.Melt = 0.9999f;
            Invoke(_runtime, "StepEonSea", 0.1f);
            var frost = (EonSeaFreeze)Get(_runtime, "_eonSeaPlayerFreeze");
            Assert.That(frost.Scale(1, terrain.Time), Is.EqualTo(0.5f));
            Assert.That((float)Get(Get(GameSim, "Player"), "Health"), Is.EqualTo(health));
            SetStage("Rewards");
            Invoke(_runtime, "StepEonSea", 0.1f);
            frost = (EonSeaFreeze)Get(_runtime, "_eonSeaPlayerFreeze");
            Assert.That(frost.Scale(1, terrain.Time), Is.EqualTo(1f));
        }

        [Test]
        public void Slippery_momentum_uses_actual_speed_and_respects_frozen_cap()
        {
            EnterTerrain();
            var terrain = (EonSeaTerrain)Get(_runtime, "_eonSeaTerrain");
            // Remove cover only in this fixture so it measures steering and the speed cap.
            foreach (var ice in terrain.Ice) ice.Melt = 1;
            var patch = terrain.Patches[13];
            SetPlayer("Position", new Vector2(patch.X, patch.Y));
            SetPlayer("Velocity", new Vector2(300, 0));
            var frost = new EonSeaFreeze(); frost.Refresh(1, terrain.Time);
            Set(_runtime, "_eonSeaPlayerFreeze", frost);
            Invoke(_runtime, "MoveEonSeaPlayer", 0.02f, Vector2.up, 300f);
            var velocity = (Vector2)Get(Get(GameSim, "Player"), "Velocity");
            Assert.That(velocity.x, Is.GreaterThan(100f), "Retained horizontal inertia");
            Assert.That(velocity.magnitude, Is.LessThanOrEqualTo(150.001f));
        }

        [Test]
        public void Freeze_scales_charge_displacement_and_pool_reuse_is_clean()
        {
            EnterTerrain();
            var terrain = (EonSeaTerrain)Get(_runtime, "_eonSeaTerrain");
            foreach (var ice in terrain.Ice) ice.Melt = 1;
            var freezes = (EonSeaFreeze[])Get(_runtime, "_eonSeaEnemyFreeze");
            freezes[0].Refresh(23, terrain.Time);
            var enemies = (Array)Get(GameSim, "Enemies");
            var enemy = enemies.GetValue(0);
            Set(enemy, "Active", true); Set(enemy, "SpawnId", 23); Set(enemy, "Radius", 13f);
            Set(enemy, "Position", new Vector2(20, 0));
            var arguments = new object[] { 0, enemy, Vector2.zero };
            InvokeArgs(_runtime, "ResolveEonSeaEnemyMovement", arguments);
            Assert.That(((Vector2)Get(arguments[1], "Position")).x, Is.EqualTo(10f));
            enemy = arguments[1]; Set(enemy, "SpawnId", 24); Set(enemy, "Position", new Vector2(20, 0));
            arguments[1] = enemy;
            InvokeArgs(_runtime, "ResolveEonSeaEnemyMovement", arguments);
            Assert.That(((Vector2)Get(arguments[1], "Position")).x, Is.EqualTo(20f));
        }

        [Test]
        public void Ordinary_bullets_and_hostile_shots_expire_at_cover_without_stress()
        {
            EnterTerrain();
            Invoke(_runtime, "ConfigureEonSeaProjectileHooks");
            var terrain = (EonSeaTerrain)Get(_runtime, "_eonSeaTerrain");
            var ice = terrain.Ice[0]; ice.Angle = 0;
            var bullets = (Array)Get(GameSim, "Bullets");
            var bullet = bullets.GetValue(0);
            Set(bullet, "Active", true); Set(bullet, "Life", 2f); Set(bullet, "Radius", 3f);
            Set(bullet, "Position", new Vector2(ice.X, ice.Y - 100));
            Set(bullet, "Velocity", new Vector2(0, 1000)); Set(bullet, "BlastRadius", 0f);
            bullets.SetValue(bullet, 0);
            var arguments = new object[] { 0.2f, new int[512], 0 };
            InvokeArgs(GameSim, "AdvanceBullets", arguments);
            Assert.That((bool)Get(bullets.GetValue(0), "Active"), Is.False);
            Assert.That(ice.ExplosionCracks, Is.Zero);
            var shots = (Array)Get(GameSim, "HostileShots");
            var shot = shots.GetValue(0);
            Set(shot, "Active", true); Set(shot, "Life", 2f); Set(shot, "Radius", 3f);
            Set(shot, "Position", new Vector2(ice.X, ice.Y - 100)); Set(shot, "Velocity", new Vector2(0, 1000));
            shots.SetValue(shot, 0);
            arguments = new object[] { 0.2f, 15f, new int[512], 0 };
            InvokeArgs(GameSim, "AdvanceHostileShots", arguments);
            Assert.That((bool)Get(shots.GetValue(0), "Active"), Is.False);
            Assert.That(ice.ExplosionCracks, Is.Zero);
        }

        [Test]
        public void Arena_exit_and_death_release_visit_and_frozen_state()
        {
            EnterTerrain();
            Set(_runtime, "_arenaId", ArenaId.Void);
            Invoke(_runtime, "StepEonSea", 0.1f);
            Assert.That(Get(_runtime, "_eonSeaTerrain"), Is.Null);
            EnterTerrain(); SetPlayer("Health", 0f);
            Invoke(_runtime, "StepEonSea", 0.1f);
            Assert.That(Get(_runtime, "_eonSeaTerrain"), Is.Null);
        }

        private object GameSim => Get(_runtime, "_gameSim");
        private void EnterTerrain()
        {
            Set(_runtime, "_arenaId", ArenaId.EonSea);
            Set(_runtime, "_mainMenuBrowsing", false);
            SetStage("Combat");
            Invoke(_runtime, "ResetEonSeaState");
            Invoke(_runtime, "StepEonSea", 0f);
        }
        private void SetStage(string name)
        {
            var field = _runtime.GetType().GetField("_journeyStage", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(_runtime, Enum.Parse(field.FieldType, name));
        }
        private void SetPlayer(string name, object value)
        {
            var player = Get(GameSim, "Player"); Set(player, name, value); Set(GameSim, "Player", player);
        }
        private static object Get(object target, string name)
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            var field = target.GetType().GetField(name, flags);
            if (field != null) return field.GetValue(target);
            return target.GetType().GetProperty(name, flags).GetValue(target);
        }
        private static void Set(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).SetValue(target, value);
        }
        private static object Invoke(object target, string name, params object[] arguments) => InvokeArgs(target, name, arguments);
        private static object InvokeArgs(object target, string name, object[] arguments)
        {
            try { return target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(target, arguments); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
    }
}
