using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoidFall.Core;
using VoidFall.Persistence;
using VoidFall.Runtime;

namespace VoidFall.Tests.PlayMode
{
    public sealed class FoundationGameplayTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private VoidFallGameRuntime _runtime;
        private SaveData _previousProfile;
        private SaveStore _previousStore, _store;
        private object _previousExport, _previousSeed;
        private bool _enabled, _inactive;
        private string _directory;
        private SaveData Profile => (SaveData)Get("_saveData");

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return null;
            _runtime = UnityEngine.Object.FindAnyObjectByType<VoidFallGameRuntime>();
            Assert.That(_runtime, Is.Not.Null);
            _enabled = _runtime.enabled;
            _runtime.enabled = false;
            _previousProfile = Profile;
            _previousStore = (SaveStore)Get("_saveStore");
            _previousExport = Get("_runExportDirectoryOverride");
            _previousSeed = Get("_diagnosticRunSeedOverride");
            _inactive = (bool)Get("_applicationInactive");
            _directory = Path.Combine(Path.GetTempPath(), "voidfall-foundation-gameplay-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _store = new SaveStore(Path.Combine(_directory, "profile.json"));
            var profile = SaveStore.CreateDefault();
            profile.parts = 100;
            profile.directorOnboardingSeen = true;
            _store.Save(profile);
            Set("_saveStore", _store);
            Set("_saveData", profile);
            Set("_runExportDirectoryOverride", Path.Combine(_directory, "exports"));
            Set("_runSaved", true);
            Set("_applicationInactive", false);
            Set("_diagnosticRunSeedOverride", 2848592627u);
            Call("StartRunInternal", false, false);
            Call("DestroyEnemiesForVoidTransition");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_runtime != null)
            {
                Call("FinishRunExport", "test_complete");
                Call("StopMajorIncident");
                Set("_runSaved", true);
                Set("_gameOver", false);
                Call("EnterMainMenu");
                Set("_saveData", _previousProfile);
                Set("_saveStore", _previousStore);
                Set("_runExportDirectoryOverride", _previousExport);
                Set("_diagnosticRunSeedOverride", _previousSeed);
                Set("_applicationInactive", _inactive);
                _runtime.enabled = _enabled;
            }
            if (_directory != null && Directory.Exists(_directory)) Directory.Delete(_directory, true);
            yield return null;
        }

        [Test]
        public void Every_between_void_crossing_enters_the_dealer_including_single_exits()
        {
            var route = (VoidRouteRun)Get("_voidRoute");
            var singles = 0;
            for (var crossing = 1; crossing <= 5; crossing++)
            {
                Call("OnVoidObjectiveCompleted");
                var exits = route.NodesInState(RouteNodeState.Available);
                if (exits.Count == 1) singles++;
                var outgoing = route.CurrentVoidId;
                Call("OpenCompletedVoidRift");
                Assert.That(Get("_junctionTransition"), Is.True, "Crossing " + crossing);
                Assert.That(route.CurrentVoidId, Is.EqualTo(outgoing), "The room precedes destination commitment.");
                for (var step = 0; step < 40 && _runtime.JourneyStatus != "Junction"; step++)
                    Call("UpdateJourneyFlow", .1f);
                Assert.That(_runtime.JourneyStatus, Is.EqualTo("Junction"));
                Assert.That(Get("_dealerSession"), Is.Not.Null);
                Assert.That((string[])Get("_junctionDestinations"), Is.EqualTo(exits));
                // Route iteration is separate from arena loading, covered by
                // JourneyIntegrationTests. Keep this regression on the crossing edge.
                Assert.That(route.SelectNextVoid(exits[0]), Is.True);
                Call("HideJunction");
                Set("_journeyStage", Enum.Parse(Get("_journeyStage").GetType(), "Combat"));
            }
            Assert.That(singles, Is.EqualTo(3));
            Assert.That(route.History.Count, Is.EqualTo(6));
        }

        [TestCase("ClearTransitionProjectiles")]
        [TestCase("ClearNullCityHostiles")]
        [TestCase("ClearCourtBossArena")]
        public void Bulk_shot_clear_reclaims_curved_capacity_and_provenance(string clearMethod)
        {
            var sim = Get("_gameSim");
            for (var cycle = 0; cycle < 2; cycle++)
            {
                for (var shot = 0; shot < EliteRules.MaxCurvedProjectiles; shot++)
                    Assert.That(InsertShot(sim), Is.GreaterThanOrEqualTo(0));
                Assert.That(InsertShot(sim), Is.EqualTo(-1));
                Call(clearMethod);
                Assert.That(Read(sim, "CurvedShotCount"), Is.EqualTo(0));
                Assert.That(RuntimeTestReflection.Invoke(sim, "ActiveHostileShots"), Is.EqualTo(0));
                Assert.That(((string[])Read(sim, "HostileShotKillerIds")).All(string.IsNullOrEmpty), Is.True);
                Assert.That(((bool[])Read(sim, "HostileShotElite")).Any(value => value), Is.False);
                var shots = (Array)Read(sim, "HostileShots");
                var empty = Activator.CreateInstance(shots.GetType().GetElementType());
                foreach (var shot in shots) Assert.That(shot, Is.EqualTo(empty), "a new run cannot inherit inactive projectile state");
                Call(clearMethod);
                Assert.That(Read(sim, "CurvedShotCount"), Is.EqualTo(0), "Repeated clears are idempotent.");
            }
            Assert.That(InsertShot(sim), Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void Enemy_ticks_reuse_all_stable_callback_instances()
        {
            var sim = Get("_gameSim");
            Call("UpdateEnemies", 1f / 60f);
            var callbacks = sim.GetType().GetFields(Flags)
                .Where(field => field.Name.StartsWith("Enemy") && typeof(Delegate).IsAssignableFrom(field.FieldType))
                .Where(field => field.GetValue(sim) != null)
                .ToDictionary(field => field, field => field.GetValue(sim));
            Assert.That(callbacks.Count, Is.GreaterThanOrEqualTo(23));
            Call("UpdateEnemies", 1f / 60f);
            foreach (var callback in callbacks)
                Assert.That(callback.Key.GetValue(sim), Is.SameAs(callback.Value), callback.Key.Name);
        }

        private int InsertShot(object sim) => (int)RuntimeTestReflection.Invoke(sim, "TryInsertHostileShot",
            new Vector2(500, 500), Vector2.right, 1f, 100f, .5f, false, -1, "test-source", true);
        private static object Read(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
        private object Get(string name) => Read(_runtime, name);
        private void Set(string name, object value) => typeof(VoidFallGameRuntime).GetField(name, Flags).SetValue(_runtime, value);
        private object Call(string name, params object[] args) => RuntimeTestReflection.Invoke(_runtime, name, args);
    }
}
