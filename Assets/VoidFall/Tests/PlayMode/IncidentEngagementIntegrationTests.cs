using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoidFall.Core;
using VoidFall.Runtime;

namespace VoidFall.Tests.PlayMode
{
    public sealed class IncidentEngagementIntegrationTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private VoidFallGameRuntime _runtime;
        private SimulationProfileScope _profile;
        private object _sim;
        private Array _enemies;
        private bool _enabled;
        private string _exports;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return null;
            _runtime = UnityEngine.Object.FindAnyObjectByType<VoidFallGameRuntime>();
            Assert.That(_runtime, Is.Not.Null);
            _enabled = _runtime.enabled; _runtime.enabled = false;
            _profile = new SimulationProfileScope(_runtime);
            Put(_runtime, "_diagnosticRunSeedOverride", 1700u);
            Call("StartRunInternal", false, false);
            _sim = Get(_runtime, "_gameSim"); _enemies = (Array)Get(_sim, "Enemies");
            Array.Clear(_enemies, 0, _enemies.Length);
            _sim.GetType().GetMethod("ResetEnemyOrder").Invoke(_sim, null);
            Player("Position", Vector2.zero);
            _exports = Path.Combine(Path.GetTempPath(), "voidfall-incident-" + Guid.NewGuid().ToString("N"));
            Put(_runtime, "_runExportDirectoryOverride", _exports);
            Call("BeginRunExport", true, true);
        }

        [TearDown]
        public void TearDown()
        {
            Call("StopMajorIncident"); Call("FinishRunExport", "test_finished");
            Put(_runtime, "_runExportDirectoryOverride", null);
            _profile?.Dispose(); _runtime.enabled = _enabled;
            if (Directory.Exists(_exports)) Directory.Delete(_exports, true);
        }

        [Test]
        public void Black_hole_warns_inside_zone_then_pulls_noticeably_and_remains_escapable()
        {
            Assert.That(_runtime.ForceMajorIncidentForDiagnostics("black-hole"), Is.True);
            var center = (Vector2)Get(_runtime, "_incidentCenter");
            Assert.That(center.magnitude, Is.InRange(160f, 170f));
            Call("ApplyMajorIncidentPlayerDisplacement", 1f);
            Assert.That(Position, Is.EqualTo(Vector2.zero), "warning has no force");
            var state = (MajorIncidentState)Get(_runtime, "_majorIncident"); state.Step(MajorIncidentRules.BlackHoleWarningSeconds + MajorIncidentRules.BlackHoleActivationSeconds);
            Call("ApplyMajorIncidentPlayerDisplacement", .25f);
            var baseSpeed = (float)ContentCatalog.Operative.MoveSpeed;
            Assert.That(Position.x, Is.GreaterThan(baseSpeed * .5f * .25f));
            // The existing diagnostic steering chases nearby pickups. A fixed outward goal
            // gives a unit movement axis without depending on native-device focus in batch mode.
            var previousPlaytest = (bool)Get(_runtime, "_directorPlaytestActive");
            Call("SpawnPickup", new Vector2(-400f, 0), 1f);
            Put(_runtime, "_directorPlaytestActive", true);
            try
            {
                for (var tick = 0; tick < 240; tick++)
                {
                    Assert.That((Vector2)Call("DirectorPlaytestInput"), Is.EqualTo(Vector2.left));
                    Call("MovePlayer", 1f / 120);
                }
                Assert.That(Vector2.Distance(Position, center), Is.GreaterThan(230f), "ordinary movement escapes full-strength attraction through the real acceleration and force ordering");
            }
            finally { Put(_runtime, "_directorPlaytestActive", previousPlaytest); }
            Player("Position", center + Vector2.right * .01f);
            Call("ApplyMajorIncidentPlayerDisplacement", 1f);
            Assert.That(Position.x, Is.GreaterThanOrEqualTo(center.x), "a large step cannot overshoot the core");
            Player("Position", center + Vector2.right * 231f);
            var outside = Position; Call("ApplyMajorIncidentPlayerDisplacement", 1f);
            Assert.That(Position, Is.EqualTo(outside));
        }

        [Test]
        public void Black_hole_pauses_cancels_and_renders_the_actual_radius()
        {
            _runtime.ForceMajorIncidentForDiagnostics("black-hole");
            Call("RenderMajorIncidents");
            var presentation = Get(_runtime, "_incidentPresentation");
            var view = (GameObject)Get(presentation, "_view");
            Assert.That(view.transform.lossyScale.x * .91f / 2, Is.EqualTo(230f).Within(.01f));
            var state = (MajorIncidentState)Get(_runtime, "_majorIncident"); state.Step(MajorIncidentRules.BlackHoleWarningSeconds + MajorIncidentRules.BlackHoleActivationSeconds);
            Put(_runtime, "_paused", true);
            Call("StepMajorIncidents", 1f); Call("ApplyMajorIncidentPlayerDisplacement", 1f);
            Assert.That(state.Elapsed, Is.EqualTo(MajorIncidentRules.BlackHoleWarningSeconds + MajorIncidentRules.BlackHoleActivationSeconds)); Assert.That(Position, Is.EqualTo(Vector2.zero));
            Put(_runtime, "_paused", false); Call("StopMajorIncident");
            Call("ApplyMajorIncidentPlayerDisplacement", 1f);
            Assert.That(Position, Is.EqualTo(Vector2.zero));
            Assert.That(view.GetComponent<MeshRenderer>().enabled, Is.False);
        }

        [TestCase(235f, false, false)]
        [TestCase(260f, false, false)]
        [TestCase(260f, true, false)]
        [TestCase(235f, false, true)]
        [TestCase(260f, false, true)]
        public void Running_black_hole_warning_can_be_escaped_but_active_entry_pulls(float speed, bool juke, bool enterActive)
        {
            Player("Velocity", Vector2.right * speed);
            Put(_runtime, "_moveSpeedMultiplier", speed / (float)ContentCatalog.Operative.MoveSpeed);
            Assert.That(_runtime.ForceMajorIncidentForDiagnostics("black-hole"), Is.True);
            var center = (Vector2)Get(_runtime, "_incidentCenter");
            if (enterActive)
                ((MajorIncidentState)Get(_runtime, "_majorIncident")).Step(
                    MajorIncidentRules.BlackHoleWarningSeconds + MajorIncidentRules.BlackHoleActivationSeconds);
            var direction = juke ? Vector2.left : Vector2.right;
            Call("SpawnPickup", Position + direction * 400f, 1f);
            var pickups = (Array)Get(_sim, "Pickups");
            var goalSlot = Enumerable.Range(0, pickups.Length).Single(i => (bool)Get(pickups.GetValue(i), "Active"));
            var previousPlaytest = (bool)Get(_runtime, "_directorPlaytestActive");
            Put(_runtime, "_directorPlaytestActive", true);
            try
            {
                for (var tick = 1; tick <= 480; tick++)
                {
                    // Keep a moving diagnostic target ahead so the existing steering supplies
                    // steady running input. Only the real MovePlayer path changes the player.
                    var goal = pickups.GetValue(goalSlot); Put(goal, "Position", Position + direction * 400f); pickups.SetValue(goal, goalSlot);
                    Put(_runtime, "_time", tick / 120f);
                    Call("StepMajorIncidents", 1f / 120);
                    Call("MovePlayer", 1f / 120);
                }
            }
            finally { Put(_runtime, "_directorPlaytestActive", previousPlaytest); }
            Assert.That((Vector2)Get(_runtime, "_incidentCenter"), Is.EqualTo(center), "the warned center stays locked");
            Assert.That(Vector2.Distance(Position, center), Is.GreaterThan(230f));
            Call("StopMajorIncident"); Call("FinishRunExport", "test_finished");
            var pulls = History().Where(e => e.kind == "black_hole_pull").ToArray();
            var displacement = pulls.Sum(e => e.amount);
            TestContext.WriteLine($"Running Black Hole speed={speed:F0}; juke={juke}; actual pull={displacement:F2}; final distance={Vector2.Distance(Position, center):F2}");
            if (!enterActive) Assert.That(displacement, Is.Zero, "the ten-second warning gives a moving player time to leave the locked zone");
            else Assert.That(displacement, Is.GreaterThan(20f), "entering the active zone produces real attraction before escape");
        }

        [Test]
        public void Moving_black_hole_places_its_locked_warning_ahead_of_vertical_velocity()
        {
            Player("Velocity", Vector2.up * 235f);
            Assert.That(_runtime.ForceMajorIncidentForDiagnostics("black-hole"), Is.True);
            var center = (Vector2)Get(_runtime, "_incidentCenter");
            Assert.That(center, Is.EqualTo(Vector2.up * 165f));
            Player("Velocity", Vector2.left * 235f);
            Call("StepMajorIncidents", .5f);
            Assert.That((Vector2)Get(_runtime, "_incidentCenter"), Is.EqualTo(center), "the ring never follows a later change of direction");
            Assert.That(_runtime.CurrentMajorIncidentPhase, Is.EqualTo("Warning"));
        }

        [Test]
        public void Incident_exports_record_committed_phases_and_actual_pull_without_frame_spam()
        {
            _runtime.ForceMajorIncidentForDiagnostics("black-hole");
            for (var tick = 0; tick < (MajorIncidentRules.BlackHoleWarningSeconds + 5) * 120; tick++)
            {
                Put(_runtime, "_time", tick / 120f);
                Call("StepMajorIncidents", 1f / 120);
                Call("ApplyMajorIncidentPlayerDisplacement", 1f / 120);
            }
            Call("StopMajorIncident"); Call("FinishRunExport", "test_finished");
            var events = History();
            Assert.That(events.Any(e => e.kind == "incident_phase" && e.reason == "Active"), Is.True);
            var pulls = events.Where(e => e.kind == "black_hole_pull").ToArray();
            Assert.That(pulls.Length, Is.InRange(4, 5));
            Assert.That(pulls.Sum(e => e.amount), Is.GreaterThan(40));
            Assert.That(pulls.All(e => e.durationSeconds > 0 && e.detail.Contains("distance")), Is.True);
        }

        [Test]
        public void Late_raid_has_an_observable_attack_under_owner_five_evolved_VI_build_and_is_defeatable()
        {
            // Controlled combat probe: recorded checkpoint build, zero Workshop, fixed position/seed.
            // It is deliberately stronger than an early build, not a reconstruction of owner movement.
            var progress = new UpgradeProgress();
            foreach (var weapon in new[] { 0, 1, 4, 6, 8 })
            { progress.WeaponRanks[weapon] = 6; progress.Evolved[weapon] = true; }
            var supportIds = new[] { "calibration", "cycling", "mobility", "collector", "optics", "overload", "adrenal", "amplifier", "dodge", "scholar", "projectileSpeed" };
            var ranks = new[] { 4, 4, 1, 1, 3, 3, 1, 3, 2, 3, 3 };
            var supports = ExtendedCatalog.AllSupports();
            for (var i = 0; i < supportIds.Length; i++)
                for (var s = 0; s < supports.Length; s++) if (supports[s].Id == supportIds[i]) progress.SupportRanks[s] = ranks[i];
            Put(_runtime, "_upgradeProgress", progress); Call("RecalculatePlayerStats", false);
            Player("Health", 100000f); Player("MaxHealth", 100000f);
            Put(_runtime, "_time", 1262.922f); Put(_runtime, "_encounterInitialized", true);
            var pressure = (RunPressureState)Get(_runtime, "_runPressure");
            pressure.Reset(DirectorProfiles.For((DirectorProfileId)Get(_runtime, "_runDirectorProfile")).PressureCeilingHundredths, 5);
            pressure.ObserveStage(0, 1, 1); pressure.ObserveStage(1, 1, 1); pressure.ObserveStage(2, .875, 0);
            Call("StartDestroyerRaid", Vector2.zero);
            var initialHealth = _enemies.Cast<object>().Where(e => (bool)Get(e, "Active")).Sum(e => (float)Get(e, "Health"));
            var mawSlots = Enumerable.Range(0, _enemies.Length).Where(i => (bool)Get(_enemies.GetValue(i), "Active") && (string)Get(_enemies.GetValue(i), "Id") == "destroyer-maw").ToArray();
            var mawSweepDistances = new float[mawSlots.Length];
            var mawBefore = new object[mawSlots.Length];
            var aliveAtOldRaidEnd = 0;
            var defeatTime = 0f;
            for (var tick = 1; tick <= 3840; tick++)
            {
                var dt = 1f / 120; Put(_runtime, "_time", 1262.922f + tick * dt);
                Call("StepDestroyerRaid", dt, false); Call("RebuildEnemyGrid"); Call("UpdateWeapons", dt);
                for (var m = 0; m < mawSlots.Length; m++) mawBefore[m] = _enemies.GetValue(mawSlots[m]);
                Call("UpdateEnemies", dt);
                for (var m = 0; m < mawSlots.Length; m++)
                {
                    var mawAfter = _enemies.GetValue(mawSlots[m]);
                    if ((bool)Get(mawBefore[m], "Active") && (bool)Get(mawAfter, "Active") &&
                        (bool)Get(((Array)Get(_runtime, "_destroyers")).GetValue(mawSlots[m]), "SweepThisStep"))
                        mawSweepDistances[m] += Vector2.Distance((Vector2)Get(mawBefore[m], "Position"), (Vector2)Get(mawAfter, "Position"));
                }
                Call("RebuildEnemyGrid"); Call("UpdateBlades", dt);
                Call("UpdateArsenalWeapons", dt); Call("UpdateBullets", dt); Call("UpdateRailTrails", dt); Call("UpdateHostileShots", dt);
                if (tick == 376) aliveAtOldRaidEnd = _runtime.ActiveEnemiesCount;
                if (_runtime.ActiveEnemiesCount == 0) { defeatTime = tick * dt; Call("StepDestroyerRaid", dt, false); break; }
            }
            Call("FinishRunExport", "test_finished");
            var events = History(); var first = events.Where(e => e.kind == "destroyer_attack" && e.reason == "first").ToArray();
            var mawSweepDistance = mawSweepDistances.Max();
            TestContext.WriteLine($"Raid HP={initialHealth:F0}; alive at 3.13s={aliveAtOldRaidEnd}; first attacks={first.Length}; defeated at={defeatTime:F2}s; Maw sweep={mawSweepDistance:F2}; first=" + string.Join(",", first.Select(e => e.id + "@" + e.durationSeconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture))));
            Assert.That(first.Length, Is.GreaterThan(0));
            Assert.That(first.Any(e => e.id == "destroyer-maw"), Is.True, "Maw commits its first charge against the strong build");
            Assert.That(first.Any(e => e.id == "destroyer-spite"), Is.True, "Spite retains its ranged opening");
            Assert.That(mawSweepDistance, Is.GreaterThanOrEqualTo(100f), "the live charge must visibly travel, not only commit an attack before dying/freezing");
            Assert.That(first.Min(e => e.durationSeconds), Is.LessThan(3.13f));
            Assert.That(aliveAtOldRaidEnd, Is.GreaterThan(0));
            Assert.That(defeatTime, Is.InRange(3.13f, 32f));
            Assert.That(events.Any(e => e.kind == "destroyer_raid_resolved" && e.reason == "defeated"), Is.True);
        }

        [Test]
        public void Frozen_destroyer_keeps_its_warning_takes_damage_then_completes_attack_after_thaw()
        {
            Call("StartDestroyerRaid", Vector2.zero);
            var slot = Enumerable.Range(0, _enemies.Length).First(i => (bool)Get(_enemies.GetValue(i), "Active") && (string)Get(_enemies.GetValue(i), "Id") == "destroyer-spite");
            // Eight raiders share the same attention budget; wait for this Spite's turn.
            for (var tick = 0; tick < 300 && (int)Get(_enemies.GetValue(slot), "State") != 1; tick++)
            { Call("RebuildEnemyGrid"); Call("UpdateEnemies", .02f); }
            var enemy = _enemies.GetValue(slot); Assert.That((int)Get(enemy, "State"), Is.EqualTo(1));
            Call("TryRenderDestroyer", slot, enemy);
            var warning = ((LineRenderer[])Get(_runtime, "_destroyerWarnings"))[slot];
            Assert.That(warning.enabled, Is.True); Assert.That(warning.positionCount, Is.GreaterThan(1));
            var timer = (float)Get(enemy, "StateTimer");
            ((int[])Get(_runtime, "_arsenalFreezeIds"))[slot] = (int)Call("EnemyIdentity", enemy, slot);
            ((float[])Get(_runtime, "_arsenalFreeze"))[slot] = .5f;
            var health = (float)Get(enemy, "Health"); Call("ApplyEnemyDamage", slot, 10f);
            Call("RebuildEnemyGrid"); Call("UpdateEnemies", .25f);
            Assert.That((float)Get(_enemies.GetValue(slot), "StateTimer"), Is.EqualTo(timer));
            Assert.That((float)Get(_enemies.GetValue(slot), "Health"), Is.LessThan(health));
            for (var tick = 0; tick < 120; tick++) { Call("RebuildEnemyGrid"); Call("UpdateEnemies", .02f); }
            Call("FinishRunExport", "test_finished");
            Assert.That(History().Any(e => e.kind == "destroyer_attack" && e.id == "destroyer-spite" && e.reason == "first"), Is.True);
            Call("EndDestroyerRaid"); Assert.That(warning.enabled, Is.False);
        }

        [Test]
        public void First_void_pistol_loadout_raid_releases_and_clears_all_bodies_and_shots()
        {
            // Invulnerability isolates finite incident lifecycle; this does not assert early-build balance.
            var progress = new UpgradeProgress(); progress.WeaponRanks[0] = 1;
            Put(_runtime, "_upgradeProgress", progress); Call("RecalculatePlayerStats", false);
            Player("Iframes", float.PositiveInfinity);
            Put(_runtime, "_time", 85f);
            Assert.That(_runtime.ForceMajorIncidentForDiagnostics("raid"), Is.True);
            var sawRelease = false; var bodiesAtRelease = 0;
            for (var tick = 1; tick <= (MajorIncidentRules.TotalDuration(MajorIncidentKind.DestroyerRaid) + 1) * 120; tick++)
            {
                const float dt = 1f / 120;
                Put(_runtime, "_time", 85f + tick * dt);
                Call("StepMajorIncidents", dt); Call("RebuildEnemyGrid");
                Call("UpdateWeapons", dt); Call("UpdateEnemies", dt); Call("RebuildEnemyGrid");
                Call("UpdateBullets", dt); Call("UpdateHostileShots", dt);
                if (!sawRelease && _runtime.CurrentMajorIncidentPhase == "Release")
                { sawRelease = true; bodiesAtRelease = _runtime.ActiveEnemiesCount; }
            }
            Assert.That(sawRelease, Is.True); Assert.That(bodiesAtRelease, Is.GreaterThan(0), "the weak loadout leaves survivors to exercise withdrawal");
            Assert.That(_runtime.CurrentMajorIncident, Is.EqualTo("None"));
            Assert.That(_runtime.ActiveEnemiesCount, Is.Zero);
            Assert.That(((Array)Get(_sim, "HostileShots")).Cast<object>().Any(s => (bool)Get(s, "Active")), Is.False);
            Call("FinishRunExport", "test_finished");
            var resolution = History().Single(e => e.kind == "destroyer_raid_resolved");
            Assert.That(resolution.reason, Is.EqualTo("release")); Assert.That(resolution.amount, Is.GreaterThan(0));
            TestContext.WriteLine($"Pistol I lifecycle: survivors at release={bodiesAtRelease}; final bodies=0; final hostile shots=0; player invulnerable (no balance claim)");
        }

        private Vector2 Position => (Vector2)Get(Get(_sim, "Player"), "Position");
        private void Player(string field, object value) { var player = Get(_sim, "Player"); Put(player, field, value); Put(_sim, "Player", player); }
        private UnityTelemetryHistoryEvent[] History() => File.ReadAllLines(Directory.GetFiles(_exports, "*.jsonl").Single()).Select(JsonUtility.FromJson<UnityTelemetryHistoryEvent>).ToArray();
        private static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
        private static void Put(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        private object Call(string name, params object[] args)
        {
            foreach (var method in _runtime.GetType().GetMethods(Flags))
                if (method.Name == name && method.GetParameters().Length == args.Length)
                    try { return method.Invoke(_runtime, args); } catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
            throw new MissingMethodException(name);
        }
    }
}
