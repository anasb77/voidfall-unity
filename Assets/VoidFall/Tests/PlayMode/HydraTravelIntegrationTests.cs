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
    public sealed class HydraTravelIntegrationTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private VoidFallGameRuntime _runtime;
        private object _oldStore, _oldProfile, _oldExportDirectory;
        private bool _oldEnabled;
        private string _directory;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _runtime = UnityEngine.Object.FindAnyObjectByType<VoidFallGameRuntime>();
            Assert.That(_runtime, Is.Not.Null);
            _oldEnabled = _runtime.enabled;
            _runtime.enabled = false;
            _oldStore = Get(_runtime, "_saveStore");
            _oldProfile = Get(_runtime, "_saveData");
            _oldExportDirectory = Get(_runtime, "_runExportDirectoryOverride");
            _directory = Path.Combine(Path.GetTempPath(), "voidfall-hydra-travel-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            Set("_saveStore", new SaveStore(Path.Combine(_directory, "profile.json")));
            Set("_saveData", SaveStore.CreateDefault());
            Set("_runExportDirectoryOverride", Path.Combine(_directory, "RunExports"));
            Call("StartRunInternal", true, false);
            Set("_voidRoute", new VoidRouteRun(new[]
            {
                new VoidRouteNode("hydra", "Hydra", 0, 1, "", "", "", "")
            }, "hydra"));
            Set("_arenaId", ArenaId.Hydra);
            Call("BeginObjectiveForCurrentArena");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_runtime != null)
            {
                Call("FinishRunExport", "test_finished");
                Set("_runSaved", true);
                Call("EnterMainMenu");
                Set("_saveStore", _oldStore);
                Set("_saveData", _oldProfile);
                Set("_runExportDirectoryOverride", _oldExportDirectory);
                _runtime.enabled = _oldEnabled;
            }
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Survival_completes_into_teleport_then_original_boss_in_same_visit_and_journal()
        {
            var tracker = (VoidObjectiveTracker)Get(_runtime, "_objectives");
            tracker.Step(359);
            Call("SyncVoidBossEncounterWithObjective");
            Assert.That(Get(_runtime, "_riftTransitionActive"), Is.False);
            Assert.That(Property("HydraSurvivalPresentationActive"), Is.True);
            var survivalViewport = (Vector2)Call("GameplayViewportHalfExtent");
            Assert.That(survivalViewport.y, Is.EqualTo(454f).Within(.001f));
            tracker.Step(1);
            var route = (VoidRouteRun)Get(_runtime, "_voidRoute");
            var historyBefore = route.History.ToArray();
            var pressure = (RunPressureState)Get(_runtime, "_runPressure");
            var pressureBefore = pressure.PressureHundredths;
            var creditBefore = pressure.CreditedProgressSeconds;
            var visitBefore = Get(_runtime, "_pressureStageIndex");
            Call("SyncVoidBossEncounterWithObjective");
            Call("SyncVoidBossEncounterWithObjective");
            Assert.That(Get(_runtime, "_riftTransitionActive"), Is.True);
            Assert.That(Get(_runtime, "_hydraBossEncounterActive"), Is.False);
            Assert.That(((ArenaTransitionState)Get(_runtime, "_arenaTransitionState")).Phase, Is.EqualTo(ArenaPhase.Collapse));
            Assert.That(tracker.IsComplete, Is.False);
            var expectedHealth = (float)(HydraContent.Boss.Health * DirectorRules.BossHealthScaleAt(
                (float)Property("DurationAdjustedDifficultySeconds"), "hydra-prime"));
            for (var frame = 0; frame < 300 && !(bool)Get(_runtime, "_riftTransitionSwapped"); frame++)
            {
                Call("StepRiftTransition", .8f);
                yield return null;
            }
            Assert.That(((ArenaTransitionState)Get(_runtime, "_arenaTransitionState")).Phase, Is.EqualTo(ArenaPhase.Settle));
            Call("CommitRiftTransitionSwap");
            Assert.That(Get(_runtime, "_hydraBossEncounterActive"), Is.True);
            Assert.That(Property("HydraSurvivalPresentationActive"), Is.False);
            Assert.That((Vector2)Call("GameplayViewportHalfExtent"), Is.EqualTo(survivalViewport),
                "Entering the original boss arena must not change the gameplay zoom.");
            var game = Get(_runtime, "_gameSim");
            var bosses = ((Array)Get(game, "Bosses")).Cast<object>().Where(b => (bool)Get(b, "Active")).ToArray();
            Assert.That(bosses, Has.Length.EqualTo(1));
            Assert.That(Get(bosses[0], "Id"), Is.EqualTo("hydra-prime"));
            Assert.That(Get(bosses[0], "MaxHealth"), Is.EqualTo(expectedHealth).Within(.01));
            Assert.That(Get(bosses[0], "Speed"), Is.EqualTo(0f));
            Assert.That(Get(bosses[0], "Position"), Is.EqualTo(Vector2.up * 245f));
            Assert.That(route.History, Is.EqualTo(historyBefore));
            Assert.That(Get(_runtime, "_pressureStageIndex"), Is.EqualTo(visitBefore));
            Assert.That(pressure.PressureHundredths, Is.EqualTo(pressureBefore));
            Assert.That(pressure.CreditedProgressSeconds, Is.EqualTo(creditBefore));
            Assert.That(Get(_runtime, "_objectives"), Is.SameAs(tracker));
            Assert.That(tracker.IsComplete, Is.False);
            Assert.That(Get(_runtime, "_voidCompletionPending"), Is.False);
            Call("StepRiftTransition", 1.2f);
            Assert.That(Get(_runtime, "_riftTransitionActive"), Is.False);
            Assert.That(((ArenaTransitionState)Get(_runtime, "_arenaTransitionState")).Phase, Is.EqualTo(ArenaPhase.Idle));
            Call("FinishRunExport", "test_finished");
            var events = File.ReadAllLines(Directory.GetFiles(Path.Combine(_directory, "RunExports"), "*.jsonl").Single())
                .Select(JsonUtility.FromJson<UnityTelemetryHistoryEvent>).ToArray();
            var phases = events.Where(e => e.kind == "hydra_phase").ToArray();
            Assert.That(phases.Select(e => e.id), Is.EqualTo(new[] { "hydra-i", "hydra-ii", "hydra-ii" }));
            Assert.That(phases[1].sourceId, Is.EqualTo("hydra-i"));
            Assert.That(phases[1].reason, Is.EqualTo("teleport_swap"));
            Assert.That(phases[1].visitIndex, Is.EqualTo(phases[0].visitIndex));
            Assert.That(events.Any(e => e.kind == "boss_reward"), Is.False);
            tracker.NotifyNamedKilled("hydra-prime");
            tracker.Step(0);
            Assert.That(tracker.IsComplete, Is.True);
        }

        private static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
        private void Set(string name, object value) => _runtime.GetType().GetField(name, Flags).SetValue(_runtime, value);
        private object Property(string name) => _runtime.GetType().GetProperty(name, Flags).GetValue(_runtime);
        private object Call(string name, params object[] args) => _runtime.GetType().GetMethods(Flags)
            .Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(_runtime, args);
    }
}
