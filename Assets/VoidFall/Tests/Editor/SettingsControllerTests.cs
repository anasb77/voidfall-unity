using NUnit.Framework;
using VoidFall.Persistence;
using VoidFall.UI;

namespace VoidFall.Tests.Editor
{
    /// <summary>
    /// Covers the extracted settings transaction lifecycle: debounced
    /// continuous changes, immediate commits with rollback on persistence
    /// failure, and external mark-clean.
    /// </summary>
    public sealed class SettingsControllerTests
    {
        private sealed class FakeBridge : IGameBridge
        {
            public int PersistCalls;
            public int ApplyCalls;
            public int RestoreCalls;
            public SaveSettings LastRestored;
            public bool PersistSucceeds = true;

            public SaveSettings CloneLiveSettings() => new SaveSettings { quality = "high" };
            public void RestoreSettings(SaveSettings snapshot) { RestoreCalls++; LastRestored = snapshot; }
            public bool TryPersistSettings() { PersistCalls++; return PersistSucceeds; }
            public void ApplyLiveSettings() { ApplyCalls++; }
            public System.Collections.Generic.IReadOnlyList<HighScoreEntry> GetHighScores() => System.Array.Empty<HighScoreEntry>();
            public LifetimeStats GetLifetimeStats() => null;
            public bool TryPersistProfile() => TryPersistSettings();
        }

        private static SaveSettings LiveProfile() => new SaveSettings { quality = "high", touchSize = 1f };

        [Test]
        public void Continuous_change_debounces_persistence_until_the_window_elapses()
        {
            var bridge = new FakeBridge();
            var controller = new SettingsController(bridge);

            controller.StageContinuousChange(LiveProfile());
            controller.Tick(0.25f);
            Assert.That(bridge.PersistCalls, Is.EqualTo(0), "persisted before the debounce window elapsed");

            controller.Tick(0.3f);
            Assert.That(bridge.PersistCalls, Is.EqualTo(1), "did not persist once the window elapsed");

            controller.Tick(5f);
            Assert.That(bridge.PersistCalls, Is.EqualTo(1), "a consumed debounce must not commit twice");
        }

        [Test]
        public void Debounced_persistence_failure_keeps_rollback_out_of_the_path()
        {
            var bridge = new FakeBridge { PersistSucceeds = false };
            var controller = new SettingsController(bridge);

            controller.StageContinuousChange(LiveProfile());
            controller.Tick(1f);
            Assert.That(bridge.PersistCalls, Is.EqualTo(1));
            Assert.That(bridge.RestoreCalls, Is.EqualTo(0), "the debounced path never rolls back");

            // A later discrete change still reaches storage once it succeeds.
            bridge.PersistSucceeds = true;
            controller.CommitImmediateWithRollback(LiveProfile());
            Assert.That(bridge.PersistCalls, Is.EqualTo(2));
        }

        [Test]
        public void Immediate_commit_success_applies_without_restoring()
        {
            var bridge = new FakeBridge();
            var controller = new SettingsController(bridge);

            var previous = controller.StageContinuousChange(LiveProfile());
            controller.CommitImmediateWithRollback(previous);

            Assert.That(bridge.PersistCalls, Is.EqualTo(1));
            Assert.That(bridge.ApplyCalls, Is.EqualTo(1));
            Assert.That(bridge.RestoreCalls, Is.EqualTo(0));
        }

        [Test]
        public void Immediate_commit_failure_restores_the_staged_previous_values()
        {
            var bridge = new FakeBridge { PersistSucceeds = false };
            var controller = new SettingsController(bridge);

            var previous = controller.StageContinuousChange(LiveProfile());
            controller.CommitImmediateWithRollback(previous);

            Assert.That(bridge.PersistCalls, Is.EqualTo(1));
            Assert.That(bridge.RestoreCalls, Is.EqualTo(1));
            Assert.That(bridge.LastRestored, Is.SameAs(previous));
            Assert.That(bridge.ApplyCalls, Is.EqualTo(1), "live state must be reapplied from the restored values");
        }

        [Test]
        public void MarkClean_suppresses_a_pending_debounced_commit()
        {
            var bridge = new FakeBridge();
            var controller = new SettingsController(bridge);

            controller.StageContinuousChange(LiveProfile());
            controller.MarkClean();
            controller.Tick(10f);

            Assert.That(bridge.PersistCalls, Is.EqualTo(0));
        }
    }
}
