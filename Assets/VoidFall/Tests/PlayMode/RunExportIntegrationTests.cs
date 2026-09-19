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
        public void Growth_shoves_and_director_repopulation_and_elite_admissions_are_exported()
        {
            Set(_runtime, "_time", 60f);
            ((VoidObjectiveTracker)Get(_runtime, "_objectives")).Step(60);
            Call("UpdateSustainedElites", false, 60f);
            Call("SpawnEnemy", "spiky"); Call("SpawnEnemy", "exploder");
            var enemies = (Array)Get(Get(_runtime, "_gameSim"), "Enemies");
            var spiky = enemies.GetValue(1); Set(spiky, "Position", new Vector2(300, 0));
            Set(spiky, "SpawnId", 17); Set(spiky, "Age", .51f); Set(spiky, "Speed", 0f); enemies.SetValue(spiky, 1);
            var other = enemies.GetValue(2); Set(other, "Position", new Vector2(340, 0)); Set(other, "Speed", 0f); enemies.SetValue(other, 2);
            Call("UpdateEnemies", .06f); Call("ApplySpikyGrowthPushes");
            Call("UpdateEnemies", .04f); Call("ApplySpikyGrowthPushes");
            Call("DestroyEnemiesForVoidTransition");
            Set(_runtime, "_clearObservedPopulation", 60); Set(_runtime, "_clearObservedKills", 0); Set(_runtime, "_kills", 60);
            Call("UpdateSustainedRefill", .01f, false); Call("UpdateSustainedRefill", 1.3f, false);
            Call("FinishRunExport", "test_finished");
            var history = ReadHistory();
            Assert.That(history.Any(e => e.kind == "spiky_growth_push" && e.instanceId == 17 && e.amount > 0 && e.detail.Contains("contactSteps=")), Is.True);
            Assert.That(history.Count(e => e.kind == "spiky_growth_push" && e.instanceId == 17), Is.GreaterThanOrEqualTo(2), "Arena removal flushes the unfinished shove window.");
            Assert.That(history.Any(e => e.kind == "director_elite_cadence" && e.id == "elite" && e.reason == "admitted"), Is.True);
            Assert.That(history.Any(e => e.kind == "director_repopulation" && e.reason == "breather"), Is.True);
            Assert.That(history.Any(e => e.kind == "director_repopulation" && e.reason == "batch" && e.amount > 0), Is.True);
            Assert.That(ReadReport().context.directorVersion, Is.EqualTo(6));
        }

        [Test]
        public void Cpu_windows_are_exported_and_reset_without_per_frame_history()
        {
            Call("ResetPerformanceWindow");
            Call("RecordPerformancePhase", "simulation", 3d);
            Call("RecordPerformancePhase", "render", 2d);
            Call("RecordPerformancePhase", "hud", 1d);
            Call("RecordPerformancePhase", "update-total", 10d);
            Call("RecordPerformancePhase", "simulation", 5d);
            Call("RecordPerformancePhase", "update-total", 20d);
            Assert.That(Get(_runtime, "_cpuFrames"), Is.EqualTo(2));
            Set(_runtime, "_paused", true);
            Call("RecordPerformancePhase", "update-total", 100d);
            Set(_runtime, "_paused", false);
            Call("RecordTelemetrySample", .02f);
            Call("RecordTelemetrySample", .02f);
            Call("FinishRunExport", "test_finished");
            var report = ReadReport();
            var window = report.samples.Single(s => s.cpu != null && s.cpu.frames == 2).cpu;
            Assert.That(window.frames, Is.EqualTo(2));
            Assert.That(window.simulationMeanMs, Is.EqualTo(4));
            Assert.That(window.renderMeanMs, Is.EqualTo(2));
            Assert.That(window.hudMeanMs, Is.EqualTo(1));
            Assert.That(window.updateMeanMs, Is.EqualTo(15));
            Assert.That(window.updateMaxMs, Is.EqualTo(20));
            Assert.That(window.managedBytes, Is.GreaterThan(0));
            Assert.That(report.samples.Last().cpu.frames, Is.Zero);
            Assert.That(report.context.frameTimingVersion, Is.EqualTo(2));
            Assert.That(ReadHistory().Any(e => e.kind == "sample" && e.sample != null && e.sample.cpu.frames == 2), Is.True);
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
            Assert.That(ReadReport().samples.Any(s => Math.Abs(s.frameMs - 20) < .001 && s.fps == 50), Is.True,
                "The explicit 0.02 second sample must export 50 FPS; finalization also appends an EMA sample.");
            Assert.That(ReadReport().context.directorVersion, Is.EqualTo(6));
            Assert.That(ReadReport().samples.Last().specialAttackLimit, Is.EqualTo(2));
        }

        [Test]
        public void Uniform_legacy_ring_records_its_actual_composition_and_admitted_count()
        {
            Set(_runtime, "_time", 30f);
            ((bool[])Get(_runtime, "_restorationIntroduced"))[1] = true;
            Call("TryDeployLegacySwarm");
            Call("FinishRunExport", "quit");
            var ring = ReadHistory().Single(e => e.kind == "director_circle_deployed");
            Assert.That(ring.id, Is.EqualTo("legacy-rush-circle"));
            Assert.That(ring.amount, Is.EqualTo(11));
            Assert.That(ring.detail, Does.Contain("composition=runner"));
            Assert.That(ring.detail, Does.Contain("nextAt=64"));
            Assert.That(ReadReport().context.spikyPhaseSeconds, Is.EqualTo(.5));
        }

        [Test]
        public void Restoration_healing_and_chain_decisions_are_exported_with_policy_context()
        {
            var progress = (UpgradeProgress)Get(_runtime, "_upgradeProgress");
            progress.SupportRanks[Array.FindIndex(ExtendedCatalog.AllSupports(), s => s.Id == "secondWind")] = 1;
            Set(_runtime, "_level", 9);
            var sim = Get(_runtime, "_gameSim");
            var player = Get(sim, "Player"); Set(player, "Health", 5f); Set(sim, "Player", player);
            Call("TrySecondWind");
            Call("SpawnEnemy", "spiky");
            Call("ApplyEnemyDamage", 0, 100000f);
            Call("StepLegacyRestoration", .1f);
            Call("FinishRunExport", "quit");
            var heal = ReadHistory().Single(e => e.kind == "second_wind");
            Assert.That(heal.amount, Is.EqualTo(9));
            Assert.That(heal.durationSeconds, Is.EqualTo(180));
            Assert.That(ReadHistory().Any(e => e.kind == "spiky_chain_burst" && e.instanceId > 0), Is.True);
            Assert.That(ReadReport().context.restorationVersion, Is.EqualTo(LegacyRestorationRules.Version));
            Assert.That(ReadReport().context.escapeSeconds, Is.EqualTo(10));
            Assert.That(ReadReport().context.arrivalRateMultiplier, Is.EqualTo(2.5));
            Assert.That(ReadReport().context.startingPressureHundredths, Is.EqualTo(100));
            Assert.That(ReadReport().context.spikyBaseRadius, Is.EqualTo(19.5f));
            Assert.That(ReadReport().context.spikyExpandedScale, Is.EqualTo(3));
            Assert.That(ReadReport().context.shurikenSpinRadians, Is.EqualTo(14));
            Assert.That(ReadReport().context.swarmIntervalSeconds, Is.EqualTo(34));
            Assert.That(ReadReport().context.ordinaryRareDropChance, Is.EqualTo(1d / 300).Within(.0000001));
            Assert.That(ReadReport().context.overclockMaximumBankedSeconds, Is.EqualTo(30));
            Assert.That(ReadReport().context.xpMultiplierAfterLevelFive, Is.EqualTo(1.25));
            Assert.That(ReadReport().context.boomerangSizeScale, Is.EqualTo(.675));
            Assert.That(ReadReport().context.clockFaceOpacity, Is.EqualTo(.126));
        }

        private string ExportDirectory => Path.Combine(_directory, "RunExports");

        [Test]
        public void Run_export_identifies_the_committed_form_and_starting_weapon()
        {
            var profile = (SaveData)Get(_runtime, "_saveData");
            profile.form = PlayerForms.BruteId;
            profile.unlockedForms = new[] { PlayerForms.DefaultId, PlayerForms.BruteId };
            Call("StartRunInternal", true, false);
            Call("FinishRunExport", "quit");
            var report = Directory.GetFiles(ExportDirectory, "*.json")
                .Select(path => JsonUtility.FromJson<UnityTelemetryReport>(File.ReadAllText(path)))
                .Single(item => item.context.formId == PlayerForms.BruteId);
            Assert.That(report.context.formId, Is.EqualTo(PlayerForms.BruteId));
            Assert.That(report.context.startingWeaponId, Is.EqualTo("seeker"));
        }

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
        [TestCase(ArenaId.MonochromeCourt, "monochrome-court", 1f)]
        [TestCase(ArenaId.MonochromeCourt, "monochrome-court", 1.15f)]
        [TestCase(ArenaId.Hydra, "hydra", 1f)]
        [TestCase(ArenaId.Hydra, "hydra", 1.15f)]
        [TestCase(ArenaId.NullCity, "null-city", 1f)]
        [TestCase(ArenaId.NullCity, "null-city", 1.15f)]
        public void Maps_share_gameplay_framing_player_size_and_export_camera_context(ArenaId arena, string id, float zoom)
        {
            var normalPlayerScale = ((SpriteRenderer)Get(_runtime, "_playerView")).transform.localScale;
            Set(_runtime, "_voidRoute", new VoidRouteRun(new[] { new VoidRouteNode(id, id, 0, 1, "", "", "", "") }, id));
            Set(_runtime, "_arenaId", arena);
            Call("BeginObjectiveForCurrentArena");
            if (arena == ArenaId.MonochromeCourt) Call("EnsureCourtField");
            var zoomField = typeof(VoidFallGameRuntime).GetField("_spatialZoomScale", BindingFlags.Static | BindingFlags.NonPublic);
            var previousZoom = zoomField.GetValue(null);
            try
            {
                zoomField.SetValue(null, zoom);
                Call("UpdateGameplayCameraViewport");
                var camera = (Camera)Get(_runtime, "_camera");
                Assert.That(camera.orthographicSize, Is.EqualTo(486f * zoom).Within(.001f));
                Call("Render");
                Assert.That(((SpriteRenderer)Get(_runtime, "_playerView")).transform.localScale, Is.EqualTo(normalPlayerScale));
                Set(_runtime, "_cameraFollowPosition", new Vector2(9000f, -9000f));
                var centre = (Vector2)Call("GameplayCameraCentre");
                var half = (Vector2)Call("GameplayViewportHalfExtent");
                var map = (Vector2)Call("ApprovedMapSizeWorld");
                if (arena == ArenaId.MonochromeCourt)
                {
                    var travel = Vector2.Max(Vector2.zero, map * .5f - half);
                    Assert.That(centre.x, Is.EqualTo(travel.x).Within(.001f));
                    Assert.That(centre.y, Is.EqualTo(-travel.y).Within(.001f));
                }
                if (arena == ArenaId.NullCity)
                {
                    var travel = Vector2.Max(Vector2.zero, new Vector2(800, 450) - half);
                    Assert.That(centre.x, Is.EqualTo(travel.x).Within(.001f));
                    Assert.That(centre.y, Is.EqualTo(-travel.y).Within(.001f));
                    Assert.That(map, Is.EqualTo(new Vector2(1240, 526)));
                }
                Call("RecordTelemetrySample", .02f);
                Call("FinishRunExport", "test_finished");
                var report = ReadReport();
                Assert.That(report.context.mapPresentationVersion, Is.EqualTo(2));
                var sample = report.samples.Last();
                Assert.That(sample.viewportHeight, Is.EqualTo(972f * zoom).Within(.01f));
                Assert.That(sample.cameraX, Is.EqualTo(centre.x).Within(.01f));
                Assert.That(sample.cameraY, Is.EqualTo(centre.y).Within(.01f));
                Assert.That(sample.arenaWidth, Is.EqualTo(map.x).Within(.01f));
                Assert.That(ReadHistory().Last(e => e.kind == "sample").sample.cameraX, Is.EqualTo(sample.cameraX));
            }
            finally { zoomField.SetValue(null, previousZoom); }
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
