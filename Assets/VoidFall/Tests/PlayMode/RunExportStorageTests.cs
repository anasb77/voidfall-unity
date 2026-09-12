using System;
using System.IO;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using VoidFall.Runtime;

namespace VoidFall.Tests.PlayMode
{
    public sealed class RunExportStorageTests
    {
        private string _directory;
        private RunTelemetryRecorder _recorder;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "voidfall-run-export-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _recorder = new RunTelemetryRecorder();
            _recorder.Begin(42);
        }

        [TearDown]
        public void TearDown()
        {
            _recorder.CloseHistory();
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        private string Export(string status = "active", int score = 25)
        {
            return _recorder.Export(status, 12, score, 3, 1, 0, 2, 120, 5, 7, 0, 2, 1,
                outputDirectory: _directory);
        }

        [Test]
        public void Same_seed_and_duration_have_unique_run_ids_and_summary_paths()
        {
            var firstId = _recorder.RunId;
            Assert.That(Guid.TryParse(firstId, out _), Is.True);
            var first = Export();
            _recorder.Begin(42);
            Assert.That(_recorder.RunId, Is.Not.EqualTo(firstId));
            var second = Export();
            Assert.That(second, Is.Not.EqualTo(first));
            Assert.That(File.Exists(first), Is.True);
            Assert.That(File.Exists(second), Is.True);
        }

        [Test]
        public void Journal_flushes_parseable_ordered_snapshots_and_close_drains_final_records()
        {
            _recorder.ConfigureHistory(_directory, new UnityTelemetryContext { buildVersion = "fixture", captureKind = "test" });
            var progress = new UnityTelemetryProgress { evolved = new[] { "before" } };
            _recorder.RecordHistory(new UnityTelemetryHistoryEvent { kind = "choice", timeSeconds = 2, progress = progress });
            progress.evolved[0] = "after";
            _recorder.FlushHistory();
            Assert.That(SpinWait.SpinUntil(() => _recorder.HistoryInfo.flushed >= 2, 5000), Is.True);
            _recorder.RecordHistory(new UnityTelemetryHistoryEvent { kind = "run_end", reason = "defeat", timeSeconds = 3 });
            _recorder.CloseHistory();
            var info = _recorder.HistoryInfo;
            Assert.That(info.closed, Is.True);
            Assert.That(info.drainTimedOut, Is.False);
            Assert.That(info.dropped, Is.Zero);
            Assert.That(info.pending, Is.Zero);
            Assert.That(info.flushed, Is.EqualTo(3));
            var lines = File.ReadAllLines(info.file);
            Assert.That(lines.Length, Is.EqualTo(3));
            for (var index = 0; index < lines.Length; index++)
                Assert.That(JsonUtility.FromJson<UnityTelemetryHistoryEvent>(lines[index]).sequence, Is.EqualTo(index + 1));
            Assert.That(JsonUtility.FromJson<UnityTelemetryHistoryEvent>(lines[1]).progress.evolved[0], Is.EqualTo("before"));
            Assert.That(JsonUtility.FromJson<UnityTelemetryHistoryEvent>(lines[2]).reason, Is.EqualTo("defeat"));
        }

        [Test]
        public void Legacy_collections_remain_bounded_while_journal_keeps_older_progression()
        {
            _recorder.ConfigureHistory(_directory, new UnityTelemetryContext { captureKind = "test" });
            for (var index = 0; index < 2100; index++) _recorder.RecordLevel(index, index + 1, 10, 0);
            _recorder.RecordBossSpawn("boss", 123, 2, 10, 50, 1);
            _recorder.RecordBossDefeat(123, 12);
            _recorder.RecordArenaWarning(1, "void", "abyss", 8);
            _recorder.RecordArenaSwap(1, 9);
            _recorder.RecordArenaComplete(1, 10);
            _recorder.CloseHistory();
            var report = JsonUtility.FromJson<UnityTelemetryReport>(File.ReadAllText(Export("defeat")));
            Assert.That(report.progression.levels.Length, Is.EqualTo(2048));
            Assert.That(report.droppedRecords.upgrades, Is.EqualTo(52));
            Assert.That(report.history.dropped, Is.Zero);
            var lines = File.ReadAllLines(report.history.file);
            Assert.That(lines.Length, Is.EqualTo(2106));
            Assert.That(JsonUtility.FromJson<UnityTelemetryHistoryEvent>(lines[2100]).level, Is.EqualTo(2100));
            Assert.That(report.bosses[0].fightSeconds, Is.EqualTo(2));
            Assert.That(report.arenas.transitions[0].completedAtSeconds, Is.EqualTo(10));
        }

        [Test]
        public void Summary_replaces_same_run_atomically_preserving_legacy_and_new_fields()
        {
            _recorder.RecordSample(new UnityTelemetrySample
            {
                timeSeconds = 1.234f, hp = 25, maxHp = 30, level = 2, pressureHundredths = 2345,
                challengeSeconds = 1.2f, directorId = 2, encounterPhase = "combat", encounterKind = "shared",
                spawnReason = "wave", playerX = -5, playerY = 8, viewportWidth = 12, viewportHeight = 9,
                arenaWidth = 36, arenaHeight = 24, xp = 7, baseScore = 12345678901L,
            });
            var firstPath = Export();
            var secondPath = Export("defeat", 100);
            Assert.That(secondPath, Is.EqualTo(firstPath));
            var report = JsonUtility.FromJson<UnityTelemetryReport>(File.ReadAllText(secondPath));
            Assert.That(report.schemaVersion, Is.EqualTo(4));
            Assert.That(report.seed, Is.EqualTo(42));
            Assert.That(report.summary.status, Is.EqualTo("defeat"));
            Assert.That(report.summary.score, Is.EqualTo(100));
            Assert.That(report.summary.kills, Is.EqualTo(3));
            Assert.That(report.summary.damageDealt, Is.EqualTo(120));
            Assert.That(report.samples[0].timeSeconds, Is.EqualTo(1.23f));
            Assert.That(report.samples[0].pressureHundredths, Is.EqualTo(2345));
            Assert.That(report.samples[0].playerX, Is.EqualTo(-5));
            Assert.That(report.samples[0].baseScore, Is.EqualTo(12345678901L));
            Assert.That(report.samples[0].encounterPhase, Is.EqualTo("combat"));
            Assert.That(Directory.GetFiles(_directory, "*.tmp"), Is.Empty);
            Assert.That(Directory.GetFiles(_directory, "*.json").Length, Is.EqualTo(1));
        }

        [Test]
        public void Active_checkpoint_is_written_by_worker_and_final_summary_reports_drained_journal()
        {
            _recorder.ConfigureHistory(_directory, new UnityTelemetryContext { captureKind = "test" });
            _recorder.RecordLevel(2, 2, 20, 0);
            var path = Export();
            _recorder.CloseHistory();
            Assert.That(File.Exists(path), Is.True);
            Assert.That(JsonUtility.FromJson<UnityTelemetryReport>(File.ReadAllText(path)).summary.status, Is.EqualTo("active"));
            Assert.That(Export("abandoned"), Is.EqualTo(path));
            var report = JsonUtility.FromJson<UnityTelemetryReport>(File.ReadAllText(path));
            Assert.That(report.context.captureKind, Is.EqualTo("test"));
            Assert.That(report.history.closed, Is.True);
            Assert.That(report.history.flushed, Is.EqualTo(2));
        }

        [Test]
        public void Unwritable_directory_and_readonly_summary_fail_silently_and_preserve_last_good_file()
        {
            var blocked = Path.Combine(_directory, "file-not-directory");
            File.WriteAllText(blocked, "keep");
            _recorder.ConfigureHistory(blocked, new UnityTelemetryContext { captureKind = "test" });
            _recorder.RecordLevel(1, 2, 10, 0);
            _recorder.CloseHistory();
            Assert.That(_recorder.HistoryInfo.lastError, Is.Not.Null.And.Not.Empty);
            Assert.That(_recorder.HistoryInfo.dropped, Is.GreaterThanOrEqualTo(1));
            Assert.That(_recorder.HistoryCaptureEnabled, Is.False);
            var failed = _recorder.Export("active", 1, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, outputDirectory: blocked);
            Assert.That(failed, Is.Null);
            Assert.That(_recorder.LastExportError, Is.Not.Null.And.Not.Empty);
            var path = Export();
            var original = File.ReadAllText(path);
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
            try
            {
                Assert.That(Export("defeat", 999), Is.Null);
                Assert.That(File.ReadAllText(path), Is.EqualTo(original));
                Assert.That(Directory.GetFiles(_directory, "*.tmp"), Is.Empty);
            }
            finally { File.SetAttributes(path, FileAttributes.Normal); }
        }

        [Test]
        public void Context_enrichment_preserves_explicit_zero_historical_time_and_boss_encounter_identity()
        {
            _recorder.ConfigureHistory(_directory, new UnityTelemetryContext { captureKind = "test" });
            _recorder.SetHistoryContext(40, 8, "abyss", 3, 250, 2, 25);
            _recorder.RecordLevel(10, 4, 20, 0);
            _recorder.RecordBossSpawn("boss", 7, 12, 11, 100, 1);
            _recorder.RecordHistory(new UnityTelemetryHistoryEvent
            {
                kind = "historical", timeSeconds = 0, wallTimeSeconds = 0, directorId = 0,
                pressureHundredths = 0, challengeSeconds = 0, visitIndex = 1, arenaId = "void",
            });
            _recorder.CloseHistory();
            var lines = File.ReadAllLines(_recorder.HistoryInfo.file);
            var level = JsonUtility.FromJson<UnityTelemetryHistoryEvent>(lines[1]);
            Assert.That(level.timeSeconds, Is.EqualTo(10));
            Assert.That(level.level, Is.EqualTo(4));
            Assert.That(level.wallTimeSeconds, Is.EqualTo(40));
            Assert.That(level.arenaId, Is.EqualTo("abyss"));
            Assert.That(level.visitIndex, Is.EqualTo(3));
            var boss = JsonUtility.FromJson<UnityTelemetryHistoryEvent>(lines[2]);
            Assert.That(boss.visitIndex, Is.EqualTo(3));
            Assert.That(boss.encounterIndex, Is.EqualTo(12));
            var historical = JsonUtility.FromJson<UnityTelemetryHistoryEvent>(lines[3]);
            Assert.That(historical.timeSeconds, Is.Zero);
            Assert.That(historical.wallTimeSeconds, Is.Zero);
            Assert.That(historical.directorId, Is.Zero);
            Assert.That(historical.pressureHundredths, Is.Zero);
            Assert.That(historical.challengeSeconds, Is.Zero);
            Assert.That(historical.visitIndex, Is.EqualTo(1));
            Assert.That(historical.arenaId, Is.EqualTo("void"));
        }

        [Test]
        public void Performance_histogram_tracks_nearest_rank_percentiles_thresholds_mean_and_reset()
        {
            for (var index = 0; index < 90; index++) _recorder.ObserveFrame(100, 10);
            for (var index = 0; index < 5; index++) _recorder.ObserveFrame(50, 20);
            for (var index = 0; index < 4; index++) _recorder.ObserveFrame(25, 40);
            _recorder.ObserveFrame(10, 100);
            _recorder.ObserveFrame(float.NaN, 10);
            var performance = JsonUtility.FromJson<UnityTelemetryReport>(File.ReadAllText(Export())).performance;
            Assert.That(performance.framesObserved, Is.EqualTo(100));
            Assert.That(performance.meanFrameMs, Is.EqualTo(12.6f));
            Assert.That(performance.p50FrameMs, Is.EqualTo(10));
            Assert.That(performance.p95FrameMs, Is.EqualTo(20));
            Assert.That(performance.p99FrameMs, Is.EqualTo(40));
            Assert.That(performance.framesOver16_67Ms, Is.EqualTo(10));
            Assert.That(performance.framesOver33_33Ms, Is.EqualTo(5));
            Assert.That(performance.framesOver50Ms, Is.EqualTo(1));
            _recorder.Begin(42);
            _recorder.ObserveFrame(1, 1500);
            performance = JsonUtility.FromJson<UnityTelemetryReport>(File.ReadAllText(Export())).performance;
            Assert.That(performance.framesObserved, Is.EqualTo(1));
            Assert.That(performance.meanFrameMs, Is.EqualTo(1500));
            Assert.That(performance.p95FrameMs, Is.EqualTo(1500));
        }

        [Test]
        public void Oversized_history_record_is_dropped_explicitly_without_stalling_following_records()
        {
            _recorder.ConfigureHistory(_directory, new UnityTelemetryContext { captureKind = "test" });
            _recorder.RecordHistory(new UnityTelemetryHistoryEvent { kind = "oversized", detail = new string('x', 200000) });
            _recorder.RecordHistory(new UnityTelemetryHistoryEvent { kind = "survives" });
            _recorder.CloseHistory();
            Assert.That(_recorder.HistoryInfo.dropped, Is.EqualTo(1));
            Assert.That(_recorder.HistoryInfo.submitted, Is.EqualTo(3));
            Assert.That(_recorder.HistoryInfo.flushed, Is.EqualTo(2));
            var lines = File.ReadAllLines(_recorder.HistoryInfo.file);
            Assert.That(JsonUtility.FromJson<UnityTelemetryHistoryEvent>(lines[1]).sequence, Is.EqualTo(3));
        }
    }
}
