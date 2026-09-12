using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoidFall.Core;
using VoidFall.Runtime;

namespace VoidFall.Tests.PlayMode
{
    public sealed class SixMinuteSurvivalIntegrationTests
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private VoidFallGameRuntime _runtime;
        private SimulationProfileScope _profile;
        private bool _enabled;
        private VoidObjectiveTracker Tracker => (VoidObjectiveTracker)Get("_objectives");

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _runtime = UnityEngine.Object.FindAnyObjectByType<VoidFallGameRuntime>();
            Assert.That(_runtime, Is.Not.Null);
            _enabled = _runtime.enabled;
            _runtime.enabled = false;
            _profile = new SimulationProfileScope(_runtime);
            Invoke("StartRunInternal", true, false);
            yield return null;
        }

        [TearDown]
        public void TearDown() { _profile?.Dispose(); _runtime.enabled = _enabled; }

        [TestCase(1.5f)]
        [TestCase(45f)]
        [TestCase(300f)]
        [TestCase(359f)]
        public void Director_local_clock_reports_actual_survival_seconds(float seconds)
        {
            Tracker.Step(seconds);
            Assert.That(Property("LocalDirectorSurvivalSeconds"), Is.EqualTo(seconds).Within(.001));
        }

        [TestCase(329f, true)]
        [TestCase(330f, false)]
        public void New_beats_stop_only_in_the_last_thirty_seconds(float seconds, bool begins)
        {
            Tracker.Step(seconds);
            Set("_time", seconds);
            Set("_nextEncounterTime", 0f);
            Invoke("UpdateSpawns", .016f);
            Assert.That(_runtime.CurrentEncounterPhase == "Flow", Is.EqualTo(!begins));
        }

        [TestCase(344f, 6)]
        [TestCase(345f, 1)]
        public void Ambient_arrivals_soften_only_in_the_last_fifteen_seconds(float seconds, int count)
        {
            Tracker.Step(seconds);
            Set("_time", seconds);
            Set("_spawnTimer", 0f);
            Set("_nextEncounterTime", float.MaxValue);
            Set("_nextEliteVariantTime", float.MaxValue);
            Invoke("UpdateSpawns", .016f);
            Assert.That(_runtime.ActiveEnemiesCount, Is.EqualTo(count));
        }

        [Test]
        public void Active_incident_survives_old_cutoff_and_stops_at_new_lead_in()
        {
            Tracker.Step(300);
            Set("_time", 300f);
            Assert.That(_runtime.ForceMajorIncidentForDiagnostics("eclipse"), Is.True);
            Invoke("StepMajorIncidents", .016f);
            Assert.That(_runtime.CurrentMajorIncident, Is.Not.EqualTo("None"));
            Tracker.Step(45);
            Set("_time", 345f);
            Invoke("StepMajorIncidents", .016f);
            Assert.That(_runtime.CurrentMajorIncident, Is.EqualTo("None"));
        }

        [TestCase(300f, true)]
        [TestCase(344f, false)]
        public void Incident_selection_reserves_its_duration_against_actual_time_remaining(float seconds, bool begins)
        {
            Tracker.Step(seconds);
            Set("_time", seconds);
            Set("_nextIncidentOpportunity", 0f);
            for (var i = 0; i < 9; i++) Invoke("SpawnEnemy", "chaser");
            Invoke("StepMajorIncidents", .016f);
            Assert.That(_runtime.CurrentMajorIncident != "None", Is.EqualTo(begins));
        }

        [TestCase(344f, false)]
        [TestCase(345f, true)]
        public void Other_director_profiles_begin_boss_lead_in_with_fifteen_seconds_remaining(float seconds, bool leadIn)
        {
            Tracker.Step(seconds);
            Set("_time", seconds);
            Set("_runDirectorProfile", DirectorProfileId.Veteran);
            Set("_nextEncounterTime", float.MaxValue);
            Invoke("UpdateSpawns", .016f);
            Assert.That(_runtime.LastSpawnBlockReason == "boss lead-in", Is.EqualTo(leadIn));
        }

        [Test]
        public void Difficulty_clock_removes_added_survival_but_preserves_boss_elapsed_and_captured_visit()
        {
            Set("_pressureStageIndex", 2);
            Tracker.Step(180);
            Set("_time", 1020f); // Two 360-second survivals, 120 boss seconds, half current survival.
            Assert.That(Property("DurationAdjustedDifficultySeconds"), Is.EqualTo(870f));
            Tracker.Step(180);
            Set("_time", 1200f);
            Assert.That(Property("DurationAdjustedDifficultySeconds"), Is.EqualTo(1020f));
            Tracker.Step(40);
            Set("_time", 1240f);
            Assert.That(Property("DurationAdjustedDifficultySeconds"), Is.EqualTo(1060f));
            Set("_completedVoids", 3); // Completion increments this before the incoming arena begins.
            Assert.That(Property("DurationAdjustedDifficultySeconds"), Is.EqualTo(1060f));
        }

        [Test]
        public void First_void_boss_spawn_at_six_minutes_keeps_five_minute_health_and_pressure_tier()
        {
            Tracker.Step(360);
            Set("_time", 360f);
            Invoke("SpawnBoss", "warden", 1d, 1d, 0);
            var game = Get("_gameSim");
            var bosses = (Array)game.GetType().GetField("Bosses", Flags).GetValue(game);
            var boss = bosses.Cast<object>().Single(b => (bool)b.GetType().GetField("Active", Flags).GetValue(b));
            var definition = ContentCatalog.Bosses.Single(b => b.Id == "warden");
            var expectedHealth = (float)(definition.Health * DirectorRules.BossHealthScaleAt(300, "warden"));
            Assert.That(boss.GetType().GetField("MaxHealth", Flags).GetValue(boss), Is.EqualTo(expectedHealth).Within(.001));
            Assert.That(boss.GetType().GetField("PressureTier", Flags).GetValue(boss), Is.EqualTo(DirectorRules.BossPressureTierAt(300)));
        }

        [Test]
        public void Difficulty_clock_keeps_legacy_no_route_time()
        {
            Tracker.Step(360);
            Set("_time", 420f);
            Set("_voidRoute", null);
            Assert.That(Property("DurationAdjustedDifficultySeconds"), Is.EqualTo(420f));
        }

        [Test]
        public void Difficulty_clock_keeps_stress_fixture_time()
        {
            Tracker.Step(360);
            Set("_time", 420f);
            Set("_stressScenario", new StressScenarioDefinition());
            Assert.That(Property("DurationAdjustedDifficultySeconds"), Is.EqualTo(420f));
        }

        [Test]
        public void Diagnostic_raw_clock_uses_six_minute_stages_and_canonical_score_credit()
        {
            Invoke("SeedDiagnosticDirectorProgress", 360f);
            var pressure = (RunPressureState)Get("_runPressure");
            Assert.That(pressure.PressureHundredths, Is.EqualTo(40));
            Assert.That(pressure.CreditedProgressSeconds, Is.EqualTo(300));
            Invoke("SeedDiagnosticDirectorProgress", 420f);
            Assert.That(pressure.PressureHundredths, Is.EqualTo(50));
            Assert.That(pressure.CreditedProgressSeconds, Is.EqualTo(360));
        }

        private object Get(string name) => _runtime.GetType().GetField(name, Flags).GetValue(_runtime);
        private object Property(string name) => _runtime.GetType().GetProperty(name, Flags).GetValue(_runtime);
        private void Set(string name, object value) => _runtime.GetType().GetField(name, Flags).SetValue(_runtime, value);
        private object Invoke(string name, params object[] args) => _runtime.GetType().GetMethods(Flags)
            .Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(_runtime, args);
    }
}
