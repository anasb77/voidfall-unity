using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoidFall.Core;
using VoidFall.Persistence;
using VoidFall.Runtime;

namespace VoidFall.Tests.PlayMode
{
    public sealed class NullCityPacingIntegrationTests
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
                "voidfall-null-city-pacing-" + Guid.NewGuid().ToString("N"));
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

        [Test]
        public void Export_records_committed_city_pacing_and_spawn_outcomes()
        {
            Set(_runtime, "_arenaId", ArenaId.NullCity);
            Invoke(_runtime, "BeginObjectiveForCurrentArena");
            var recorder = (RunTelemetryRecorder)Get(_runtime, "_telemetry");
            recorder.ConfigureHistory(_temporaryDirectory, new UnityTelemetryContext());
            Set(_runtime, "_runExportActive", true);
            Set(_runtime, "_nullCitySpawnClock", 0f);

            Invoke(_runtime, "UpdateNullCitySpawns", .25f);
            recorder.CloseHistory();

            var rows = File.ReadAllLines(Directory.GetFiles(_temporaryDirectory, "*.jsonl").Single())
                .Select(JsonUtility.FromJson<UnityTelemetryHistoryEvent>).ToArray();
            var pacing = rows.Single(e => e.kind == "null_city_pacing");
            Assert.That(pacing.id, Is.EqualTo("surveillance"));
            Assert.That(pacing.sourceId, Is.EqualTo("null-city"));
            Assert.That(pacing.budgetLimit, Is.EqualTo(NullCityPacingRules.QuietActiveCap));
            Assert.That(pacing.detail, Does.Contain("ordinaryArrivalsPerSecond=5"));

            var spawns = rows.Where(e => e.kind == "null_city_spawn").ToArray();
            Assert.That(spawns.Length, Is.GreaterThanOrEqualTo(1));
            Assert.That(spawns.Select(e => e.sourceId), Has.All.EqualTo("null-city"));
            Assert.That(spawns.Select(e => e.reason), Has.All.EqualTo("ordinary"));
            Assert.That(spawns.Any(e => e.instanceId > 0), Is.True);
        }

        [Test]
        public void Ordinary_city_spawn_commits_reduced_xp_before_enemy_telemetry()
        {
            Set(_runtime, "_arenaId", ArenaId.NullCity);
            Invoke(_runtime, "BeginObjectiveForCurrentArena");
            var recorder = (RunTelemetryRecorder)Get(_runtime, "_telemetry");
            recorder.ConfigureHistory(_temporaryDirectory, new UnityTelemetryContext());
            Set(_runtime, "_runExportActive", true);

            Assert.That(Invoke(_runtime, "SpawnEnemy", "null-enforcer"), Is.True);
            var enemies = (Array)Get(Get(_runtime, "_gameSim"), "Enemies");
            var enemy = enemies.Cast<object>().Single(e => (bool)Get(e, "Active") && (string)Get(e, "Id") == "null-enforcer");
            Assert.That(Get(enemy, "Xp"), Is.EqualTo(5f), "6 base XP × .75, rounded with the existing source-rounding rule.");

            recorder.CloseHistory();
            var rows = File.ReadAllLines(Directory.GetFiles(_temporaryDirectory, "*.jsonl").Single())
                .Select(JsonUtility.FromJson<UnityTelemetryHistoryEvent>).ToArray();
            Assert.That(rows.Single(e => e.kind == "enemy_spawn" && e.id == "null-enforcer").amount, Is.EqualTo(5f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Brood_births_defer_at_native_cap_with_original_reward_roots(bool lockdown)
        {
            Set(_runtime, "_arenaId", ArenaId.NullCity);
            Invoke(_runtime, "BeginObjectiveForCurrentArena");
            Invoke(_runtime, "DestroyEnemiesForVoidTransition");
            Set(_runtime, "_nullCityBossActive", lockdown);
            var cap = NullCityPacingRules.ActiveCap(lockdown);
            for (var i = 0; i < cap - 1; i++)
                Assert.That(Invoke(_runtime, "SpawnNullCityUnit", 0, new Vector2(200 + i, 200), false, false), Is.True);
            Assert.That(Invoke(_runtime, "SpawnNullCityUnit", 7, new Vector2(100, 200), false, false), Is.True);
            var sim = Get(_runtime, "_gameSim");
            var enemies = (Array)Get(sim, "Enemies");
            var mother = Enumerable.Range(0, enemies.Length).Single(i => (bool)Get(enemies.GetValue(i), "Active") &&
                (string)Get(enemies.GetValue(i), "Id") == "null-broodmother");
            Invoke(_runtime, "KillEnemy", mother);
            Invoke(_runtime, "ProcessNullCityBirths");
            Assert.That(Invoke(_runtime, "ActiveEnemies"), Is.EqualTo(cap));
            Assert.That(Get(_runtime, "_nullCityBirthCount"), Is.EqualTo(3));
            var roots = ((long[])Get(_runtime, "_nullCityBirthRoots")).Take(3).ToArray();
            Assert.That(roots.All(value => value > 0), Is.True, "The dying parent's root remains retained.");
            var positions = ((Vector2[])Get(_runtime, "_nullCityBirthQueue")).Take(3).ToArray();
            Invoke(_runtime, "ProcessNullCityBirths");
            Assert.That(Get(_runtime, "_nullCityBirthCount"), Is.EqualTo(3));
            Assert.That(((long[])Get(_runtime, "_nullCityBirthRoots")).Take(3), Is.EqualTo(roots));
            Assert.That(((Vector2[])Get(_runtime, "_nullCityBirthQueue")).Take(3), Is.EqualTo(positions));
            Assert.That(Invoke(_runtime, "SpawnNullCityUnit", 0, Vector2.zero, false, false), Is.False);
            Invoke(_runtime, "KillEnemy", 0);
            Invoke(_runtime, "ProcessNullCityBirths");
            Assert.That(Invoke(_runtime, "ActiveEnemies"), Is.EqualTo(cap));
            Assert.That(Get(_runtime, "_nullCityBirthCount"), Is.EqualTo(2));
            Assert.That(((long[])Get(_runtime, "_nullCityBirthRoots")).Take(2), Is.EqualTo(roots.Take(2)));
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

        private static object Invoke(object target, string name, params object[] arguments) =>
            RuntimeTestReflection.Invoke(target, name, arguments);
    }
}
