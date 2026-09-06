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
    public sealed class RosterProgressionIntegrationTests
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
                "voidfall-roster-tests-" + Guid.NewGuid().ToString("N"));
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

        private object Sim => Get(_runtime, "_gameSim");
        private Array Enemies => (Array)Get(Sim, "Enemies");
        private void Spawn(string id, EnemyRoster tier, Vector2 position, EliteVariantId? elite = null)
        {
            var method = Array.Find(typeof(VoidFallGameRuntime).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic),
                m => m.Name == "SpawnEnemy" && m.GetParameters().Length == 10);
            Assert.That(method.Invoke(_runtime, new object[] { id, (Vector2?)position, elite, false, false, 0, 1f, (EnemyRoster?)tier, -1, null }), Is.True);
        }
        private int Find(string id)
        {
            var newestSlot = -1;
            var newestIdentity = -1;
            for (var n = 0; n < Enemies.Length; n++)
            {
                var e = Enemies.GetValue(n);
                if (!(bool)Get(e, "Active") || (string)Get(e, "Id") != id) continue;
                var identity = (int)Get(e, "SpawnId");
                if (identity <= newestIdentity) continue;
                newestIdentity = identity;
                newestSlot = n;
            }
            if (newestSlot >= 0) return newestSlot;
            Assert.Fail("Missing " + id); return -1;
        }
        private void EnemyField(int slot, string name, object value)
        {
            var e = Enemies.GetValue(slot); Set(e, name, value); Enemies.SetValue(e, slot);
        }
        [UnityTest]
        public IEnumerator RegularIVAlwaysPursuesWithoutPincer()
        {
            var playerPosition = (Vector2)Get(Get(Sim, "Player"), "Position");
            var spawnPosition = playerPosition + new Vector2(300, 0);
            Spawn("chaser", EnemyRoster.Four, spawnPosition);
            var slot = Find("chaser");
            Assert.That((EnemyRoster)Get(Enemies.GetValue(slot), "Roster"), Is.EqualTo(EnemyRoster.Four));
            Invoke(_runtime, "UpdateEnemies", .02f);
            var enemy = Enemies.GetValue(slot);
            var velocity = (Vector2)Get(enemy, "Velocity");
            var expected = (playerPosition - spawnPosition).normalized;
            Assert.That(Vector2.Dot(velocity.normalized, expected), Is.EqualTo(1).Within(.0001));
            Assert.That((int)Get(enemy, "State"), Is.Zero);
            yield return null;
        }
        [UnityTest]
        public IEnumerator TechnicianIVRepairsSixWoundedAllies()
        {
            Spawn("technician", EnemyRoster.Four, new Vector2(100, 0));
            for (var n = 0; n < 7; n++) Spawn("chaser", EnemyRoster.One, new Vector2(110 + n, 0));
            for (var n = 0; n < Enemies.Length; n++)
            {
                var e = Enemies.GetValue(n);
                if ((bool)Get(e, "Active") && (string)Get(e, "Id") == "chaser") EnemyField(n, "Health", 1f);
            }
            EnemyField(Find("technician"), "AttackCooldown", 0f);
            Invoke(_runtime, "UpdateEnemies", .01f);
            var healed = 0;
            for (var n = 0; n < Enemies.Length; n++)
            {
                var e = Enemies.GetValue(n);
                if ((bool)Get(e, "Active") && (string)Get(e, "Id") == "chaser" && (float)Get(e, "Health") > 1) healed++;
            }
            Assert.That(healed, Is.EqualTo(6));
            yield return null;
        }
        [UnityTest]
        public IEnumerator SplitterIVReleasesFiveTierIIIRunners()
        {
            Spawn("splitter", EnemyRoster.Four, new Vector2(400, 0));
            Invoke(_runtime, "KillEnemy", Find("splitter"));
            var children = 0;
            for (var n = 0; n < Enemies.Length; n++)
            {
                var e = Enemies.GetValue(n);
                if (!(bool)Get(e, "Active") || (string)Get(e, "Id") != "runner") continue;
                Assert.That((EnemyRoster)Get(e, "Roster"), Is.EqualTo(EnemyRoster.Three)); children++;
            }
            Assert.That(children, Is.EqualTo(5));
            yield return null;
        }
        [UnityTest]
        public IEnumerator CurvedIVVolleyKeepsFourShotsAndPoolCap()
        {
            Spawn("gunner", EnemyRoster.Four, new Vector2(300, 0), EliteVariantId.Gunner);
            var slot = Find("gunner");
            EnemyField(slot, "State", 1); EnemyField(slot, "StateTimer", 0f); EnemyField(slot, "DashDirection", Vector2.left);
            Invoke(_runtime, "UpdateEnemies", .01f);
            var shots = (Array)Get(Sim, "HostileShots"); var active = 0;
            foreach (var shot in shots) if ((bool)Get(shot, "Active")) active++;
            Assert.That(active, Is.EqualTo(4));
            Set(Sim, "CurvedShotCount", EliteRules.MaxCurvedProjectiles - 3);
            EnemyField(slot, "State", 1); EnemyField(slot, "StateTimer", 0f);
            Invoke(_runtime, "UpdateEnemies", .01f);
            var after = 0; foreach (var shot in shots) if ((bool)Get(shot, "Active")) after++;
            Assert.That(after, Is.EqualTo(active));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DasherIVWarnsBetweenBothCommittedCharges()
        {
            Spawn("dasher", EnemyRoster.Four, new Vector2(300, 0));
            var slot = Find("dasher");
            EnemyField(slot, "AttackCooldown", 0f);
            Invoke(_runtime, "UpdateEnemies", .01f);
            Assert.That((int)Get(Enemies.GetValue(slot), "State"), Is.EqualTo(1));
            EnemyField(slot, "StateTimer", 0f);
            Invoke(_runtime, "UpdateEnemies", .01f);
            Assert.That((int)Get(Enemies.GetValue(slot), "State"), Is.EqualTo(2));
            EnemyField(slot, "StateTimer", 0f);
            Invoke(_runtime, "UpdateEnemies", .01f);
            Assert.That((int)Get(Enemies.GetValue(slot), "State"), Is.EqualTo(1));
            Assert.That((float)Get(Enemies.GetValue(slot), "StateTimer"), Is.EqualTo(.75f));
            EnemyField(slot, "StateTimer", 0f);
            Invoke(_runtime, "UpdateEnemies", .01f);
            EnemyField(slot, "StateTimer", 0f);
            Invoke(_runtime, "UpdateEnemies", .01f);
            Assert.That((int)Get(Enemies.GetValue(slot), "State"), Is.Zero);
            yield return null;
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
