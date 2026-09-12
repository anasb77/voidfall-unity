using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoidFall.Persistence;
using VoidFall.Runtime;
using VoidFall.Core;

namespace VoidFall.Tests.PlayMode
{
    public sealed class RunExportIntegrationTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private VoidFallGameRuntime _runtime;
        private object _oldStore, _oldProfile;
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
            _directory = Path.Combine(Path.GetTempPath(), "voidfall-run-export-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            Set(_runtime, "_saveStore", new SaveStore(Path.Combine(_directory, "profile.json")));
            Set(_runtime, "_saveData", SaveStore.CreateDefault());
            Set(_runtime, "_runExportDirectoryOverride", Path.Combine(_directory, "RunExports"));
            Call("StartRunInternal", true, false);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_runtime != null)
            {
                Call("FinishRunExport", "test_finished");
                Set(_runtime, "_runExportDirectoryOverride", null);
                Set(_runtime, "_saveStore", _oldStore);
                Set(_runtime, "_saveData", _oldProfile);
                Set(_runtime, "_runSaved", true);
                Call("EnterMainMenu");
                _runtime.enabled = _oldEnabled;
            }
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
            yield return null;
        }

        [Test]
        public void Spawn_damage_death_and_collected_drops_are_joinable_without_manual_export()
        {
            Set(_runtime, "_time", 2f);
            Assert.That(Call("SpawnEnemy", "chaser"), Is.True);
            var game = Get(_runtime, "_gameSim");
            var enemies = (Array)Get(game, "Enemies");
            var slot = Enumerable.Range(0, enemies.Length).First(i => (bool)Get(enemies.GetValue(i), "Active"));
            var enemy = enemies.GetValue(slot);
            Set(enemy, "Position", Vector2.zero);
            enemies.SetValue(enemy, slot);
            Call("ApplyEnemyDamage", slot, 100000f);
            var pickups = (Array)Get(game, "Pickups");
            for (var i = 0; i < pickups.Length; i++)
            {
                var pickup = pickups.GetValue(i);
                if (!(bool)Get(pickup, "Active")) continue;
                Set(pickup, "Position", Vector2.zero);
                Set(pickup, "Velocity", Vector2.zero);
                pickups.SetValue(pickup, i);
            }
            Call("UpdatePickups", .016f);
            Call("FinishRunExport", "quit");
            var history = ReadHistory();
            var spawn = history.Single(e => e.kind == "enemy_spawn");
            Assert.That(spawn.maxHp, Is.GreaterThan(0));
            Assert.That(history.Any(e => e.kind == "enemy_death" && e.instanceId == spawn.instanceId), Is.True);
            Assert.That(history.Any(e => e.kind == "enemy_damage_window" && e.amount > 0), Is.True);
            var drop = history.First(e => e.kind == "drop_spawn");
            Assert.That(drop.relatedInstanceId, Is.EqualTo(spawn.instanceId));
            Assert.That(drop.sourceId, Is.EqualTo("enemy"));
            Assert.That(history.Any(e => e.kind == "drop_collected" && e.instanceId == drop.instanceId), Is.True);
            Assert.That(ReadReport().summary.status, Is.EqualTo("quit"));
        }

        [Test]
        public void Menu_abandonment_finishes_once_and_does_not_start_a_fake_run()
        {
            Call("EnterMainMenu");
            Assert.That(ReadReport().summary.status, Is.EqualTo("abandoned"));
            Assert.That(ReadHistory().Count(e => e.kind == "run_end"), Is.EqualTo(1));
            Assert.That(Get(_runtime, "_runExportActive"), Is.False);
            Assert.That(Directory.GetFiles(ExportDirectory, "*.json"), Has.Length.EqualTo(1));
        }

        [Test]
        public void Retry_preserves_previous_run_and_creates_unique_new_files()
        {
            Call("StartRunInternal", true, false);
            Call("FinishRunExport", "quit");
            var reports = Directory.GetFiles(ExportDirectory, "*.json")
                .Select(p => JsonUtility.FromJson<UnityTelemetryReport>(File.ReadAllText(p))).ToArray();
            Assert.That(reports, Has.Length.EqualTo(2));
            Assert.That(reports.Select(r => r.runId).Distinct().Count(), Is.EqualTo(2));
            Assert.That(reports.Select(r => r.summary.status), Is.EquivalentTo(new[] { "restarted", "quit" }));
        }

        [Test]
        public void Checkpoint_and_final_export_do_not_change_menu_notice_or_add_toasts()
        {
            var notice = Get(_runtime, "_menuNotice");
            var toasts = ((Array)Get(_runtime, "_toastStates")).Cast<object>().Select(t => JsonUtility.ToJson(t)).ToArray();
            Call("UpdateRunExport", 31f);
            Call("FinishRunExport", "quit");
            Assert.That(Get(_runtime, "_menuNotice"), Is.EqualTo(notice));
            Assert.That(((Array)Get(_runtime, "_toastStates")).Cast<object>().Select(t => JsonUtility.ToJson(t)), Is.EqualTo(toasts));
            Assert.That(ReadReport().summary.status, Is.EqualTo("quit"));
            Assert.That(ReadHistory().Count(e => e.kind == "run_end"), Is.EqualTo(1));
            Call("FinishRunExport", "interrupted");
            Assert.That(ReadReport().summary.status, Is.EqualTo("quit"));
        }

        [Test]
        public void Offered_and_applied_upgrades_and_director_decisions_reach_history()
        {
            Set(_runtime, "_levelUpTimer", .001f);
            Call("AdvanceRunLevelUps", .02f);
            Call("SelectLevelOption", 0);
            Call("ForceEncounterForDiagnostics", "volley");
            Call("RecordTelemetrySample", .02f);
            Call("FinishRunExport", "quit");
            var history = ReadHistory();
            Assert.That(history.Any(e => e.kind == "upgrade_offered" && e.options.Length > 0), Is.True);
            Assert.That(history.Any(e => e.kind == "upgrade_applied" && e.progress != null), Is.True);
            Assert.That(history.Any(e => e.kind == "encounter_selected"), Is.True);
            Assert.That(ReadReport().samples.Last().viewportWidth, Is.GreaterThan(0));
            Assert.That(ReadReport().context.directorVersion, Is.EqualTo(2));
            Assert.That(ReadReport().samples.Last().specialAttackLimit, Is.EqualTo(2));
        }

        private string ExportDirectory => Path.Combine(_directory, "RunExports");

        [Test]
        public void Second_rank_six_weapon_exports_the_fifth_slot_unlock()
        {
            var progress = (UpgradeProgress)Get(_runtime, "_upgradeProgress");
            Array.Clear(progress.WeaponRanks, 0, progress.WeaponRanks.Length);
            progress.WeaponRanks[0] = 6;
            progress.WeaponRanks[1] = 5;
            progress.WeaponRanks[2] = 1;
            progress.WeaponRanks[3] = 1;
            var option = UpgradeRules.RollProgressionOptions(progress, new Rng(7), 100)
                .Single(o => o.Kind == UpgradeOptionKind.Weapon && o.TargetId == ContentCatalog.Weapons[1].Id);
            Set(_runtime, "_levelOptions", new[] { option });
            Set(_runtime, "_levelUpActive", true);
            Call("SelectLevelOption", 0);
            Call("FinishRunExport", "quit");
            var report = ReadReport();
            Assert.That(report.context.startingProgress.weaponSlotLimit, Is.EqualTo(4));
            Assert.That(report.context.baseWeaponSlots, Is.EqualTo(4));
            Assert.That(report.context.expandedWeaponSlots, Is.EqualTo(5));
            Assert.That(report.context.maxedWeaponsForExtraSlot, Is.EqualTo(2));
            Assert.That(report.context.arsenalBalanceVersion, Is.EqualTo("2026-09-08-mine-control-v2"));
            Assert.That(report.context.incidentBalanceVersion, Is.EqualTo(2));
            var applied = ReadHistory().Single(e => e.kind == "upgrade_applied");
            Assert.That(applied.progress.weaponSlotLimit, Is.EqualTo(5));
            Assert.That(applied.progress.weapons.Count(w => w.value == 6), Is.EqualTo(2));
            Assert.That(report.summary.progress.weaponSlotLimit, Is.EqualTo(5));
        }

        [Test]
        public void Focus_round_trip_records_completion_and_graphics_context_without_resuming_combat()
        {
            var wasInactive = (bool)Get(_runtime, "_applicationInactive");
            var wasPaused = (bool)Get(_runtime, "_paused");
            try
            {
                Set(_runtime, "_applicationInactive", false); Set(_runtime, "_paused", false);
                Call("SetApplicationActive", false); Call("SetApplicationActive", false);
                Assert.That((bool)Get(_runtime, "_paused"), Is.True);
                Call("SetApplicationActive", true); Call("SetApplicationActive", true);
                Assert.That((bool)Get(_runtime, "_paused"), Is.True, "Returning focus requires explicit Resume.");
                Call("FinishRunExport", "test_finished");
                var report = ReadReport();
                Assert.That(report.context.graphicsApi, Is.EqualTo(SystemInfo.graphicsDeviceType.ToString()));
                Assert.That(report.context.fullscreenMode, Is.EqualTo(Screen.fullScreenMode.ToString()));
                var completed = ReadHistory().Where(e => e.kind == "application_focus_completed").ToArray();
                Assert.That(completed.Length, Is.EqualTo(2), "Duplicate native callbacks must not duplicate transitions.");
                Assert.That(completed.Select(e => e.reason), Is.EqualTo(new[] { "lost", "resumed" }));
                Assert.That(completed.All(e => e.durationSeconds >= 0 && e.detail.Contains("graphicsApi=")), Is.True);
            }
            finally
            {
                if ((bool)Get(_runtime, "_applicationInactive")) Call("SetApplicationActive", true);
                Set(_runtime, "_applicationInactive", wasInactive); Set(_runtime, "_paused", wasPaused);
            }
        }

        [Test]
        public void Profile_only_diagnostics_isolate_storage_without_changing_background_policy()
        {
            var method = typeof(VoidFallGameRuntime).GetMethod("DiagnosticProfilePath", BindingFlags.Static | BindingFlags.NonPublic);
            var path = Path.Combine(_directory, "focus-profile.json");
            var background = Application.runInBackground;
            Assert.That(method.Invoke(null, new object[] { new[] { "VoidFall.exe", "-vfprofile=" + path } }), Is.EqualTo(Path.GetFullPath(path)));
            Assert.That(Application.runInBackground, Is.EqualTo(background));
            Assert.That(method.Invoke(null, new object[] { new[] { "VoidFall.exe" } }), Is.Null);
            var exception = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, new object[] { new[] { "-vfprofile=" } }));
            Assert.That(exception.InnerException, Is.TypeOf<ArgumentException>(), "An invalid diagnostic flag must not fall back to the real profile.");
        }

        [TestCase(false, false, "gameover")]
        [TestCase(true, false, "escaped")]
        [TestCase(false, true, "gameover")]
        public void Terminal_run_exports_even_when_profile_save_is_unavailable(bool victory, bool noStore, string status)
        {
            Set(_runtime, "_runVictory", victory);
            if (noStore) Set(_runtime, "_saveStore", null);
            Call("EndRun");
            Assert.That(ReadReport().summary.status, Is.EqualTo(status));
            Assert.That(ReadReport().summary.scoreIsFinal, Is.True);
            Assert.That(ReadHistory().Count(e => e.kind == "run_end"), Is.EqualTo(1));
            Assert.That(Get(_runtime, "_runExportActive"), Is.False);
        }

        [Test]
        public void Roulette_records_actual_spin_and_grant_exactly_once()
        {
            Set(_runtime, "_partsEarned", 500);
            Call("OpenBossRoulette");
            var ui = Get(_runtime, "_ui");
            var view = ui.GetType().GetProperty("Roulette", Flags).GetValue(ui);
            view.GetType().GetMethod("OnSpinPressed", Flags).Invoke(view, null);
            var session = Get(_runtime, "_rouletteSession");
            Call("OnRouletteComplete", session);
            // Exercise the actual claim UI when the current reward flow has
            // one or more cards; landing alone is not necessarily a grant.
            for (var claim = 0; claim < 16 && (bool)Get(_runtime, "_prizeRevealActive"); claim++)
            {
                var claimView = ui.GetType().GetProperty("LevelUp", Flags).GetValue(ui);
                Set(claimView, "_rewardElapsed", 1f);
                RouletteClaimTestActions.ClaimOne(_runtime);
            }
            Call("OnRouletteComplete", session);
            Call("FinishRunExport", "quit");
            var history = ReadHistory();
            var rolled = history.Single(e => e.kind == "roulette_rolled");
            var granted = history.Single(e => e.kind == "roulette_granted");
            Assert.That(rolled.instanceId, Is.EqualTo(granted.instanceId));
            Assert.That(rolled.sequence, Is.LessThan(granted.sequence));
            Assert.That(granted.detail, Does.Contain("partsAfter"));
        }

        [Test]
        public void Interrupted_summary_keeps_pressure_without_freezing_gameplay()
        {
            var pressure = (RunPressureState)Get(_runtime, "_runPressure");
            pressure.ObserveStage(0, .5, 0);
            Call("ExportTelemetrySnapshot", "active");
            Assert.That(pressure.IsFrozen, Is.False);
            Call("FinishRunExport", "quit");
            var report = ReadReport();
            Assert.That(report.summary.pressureHundredths, Is.EqualTo(pressure.PressureHundredths));
            Assert.That(report.summary.pressureHundredths, Is.GreaterThan(0));
            Assert.That(report.summary.scoreIsFinal, Is.False);
            Assert.That(pressure.IsFrozen, Is.False);
        }

        [Test]
        public void Outgoing_rewards_keep_current_visit_and_revive_records_explain_recovery()
        {
            Set(_runtime, "_completedVoids", 1);
            Set(_runtime, "_pressureStageIndex", 0);
            Call("ObserveRunExportState");
            ((RunTelemetryRecorder)Get(_runtime, "_telemetry")).RecordArenaWarning(0, "void", "nullCity", 0);
            Set(_runtime, "_revivePending", true);
            Set(_runtime, "_revivesRemaining", 1);
            Call("AcceptRevive");
            Call("FinishRunExport", "quit");
            var accepted = ReadHistory().Single(e => e.kind == "revive_accepted");
            Assert.That(accepted.visitIndex, Is.EqualTo(1));
            Assert.That(accepted.amount, Is.Zero);
            Assert.That(accepted.hp, Is.GreaterThan(0));
            var warning = ReadHistory().Single(e => e.kind == "arena_warning");
            Assert.That(warning.arenaId, Is.EqualTo("void"));
            Assert.That(warning.id, Is.EqualTo("nullCity"));
            Assert.That(warning.transitionIndex, Is.Zero);
            Assert.That(warning.visitIndex, Is.EqualTo(1));
        }
        private UnityTelemetryReport ReadReport() => JsonUtility.FromJson<UnityTelemetryReport>(
            File.ReadAllText(Directory.GetFiles(ExportDirectory, "*.json").Single()));
        private UnityTelemetryHistoryEvent[] ReadHistory() => File.ReadAllLines(
            Directory.GetFiles(ExportDirectory, "*.jsonl").Single())
            .Select(JsonUtility.FromJson<UnityTelemetryHistoryEvent>).ToArray();
        private static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        private object Call(string name, params object[] args)
        {
            var method = _runtime.GetType().GetMethods(Flags).Single(m => m.Name == name &&
                m.GetParameters().Length == args.Length && m.GetParameters().Select((p, i) =>
                    args[i] == null || p.ParameterType.IsInstanceOfType(args[i])).All(x => x));
            return method.Invoke(_runtime, args);
        }
    }
}
