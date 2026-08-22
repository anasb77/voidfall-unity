using System;
using VoidFall.Persistence;

namespace VoidFall.UI
{
    /// <summary>
    /// Owns the settings transaction lifecycle: staged rollback snapshot,
    /// dirty flag, and the 0.5 s debounce so a slider drag does not write the
    /// profile every frame. Continuous controls debounce; discrete controls
    /// commit immediately and revert themselves if the write fails, matching
    /// the browser build's updateSettings contract.
    ///
    /// Extracted verbatim from VoidFallGameRuntime's settings flow as the
    /// wave-1 pilot of the menu-controllers migration.
    /// </summary>
    public sealed class SettingsController
    {
        private readonly IGameBridge _bridge;
        private bool _dirty;
        private float _debounceRemaining;
        private SaveSettings _stagedPrevious;

        public SettingsController(IGameBridge bridge)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        /// <summary>
        /// Stages a rollback snapshot of the live settings before mutation,
        /// then marks the profile dirty with a fresh debounce window. Call
        /// BEFORE mutating the live object.
        /// </summary>
        public SaveSettings StageContinuousChange(SaveSettings liveSettings)
        {
            var snapshot = liveSettings == null ? null : _bridge.CloneLiveSettings();
            _stagedPrevious = snapshot;
            _dirty = true;
            _debounceRemaining = 0.5f;
            return snapshot;
        }

        /// <summary>Runs the debounced commit when its window elapses.</summary>
        public void Tick(float unscaledDeltaTime)
        {
            if (_debounceRemaining <= 0) return;
            _debounceRemaining -= unscaledDeltaTime;
            if (_debounceRemaining > 0) return;
            CommitDebounced();
        }

        private void CommitDebounced()
        {
            if (!_dirty) return;
            // Failure keeps the dirty flag set exactly like the original
            // flow: no rollback on the debounced path, notice already shown
            // by the bridge.
            _bridge.TryPersistSettings();
            _stagedPrevious = null;
        }

        /// <summary>
        /// Immediate commit used by discrete controls. On persistence failure
        /// the staged previous values are restored into the runtime and the
        /// live state is reapplied from them.
        /// </summary>
        public void CommitImmediateWithRollback(SaveSettings previous)
        {
            _dirty = true;
            if (_bridge.TryPersistSettings())
            {
                _bridge.ApplyLiveSettings();
                _stagedPrevious = null;
                return;
            }

            if (previous != null) _bridge.RestoreSettings(previous);
            _dirty = false;
            _bridge.ApplyLiveSettings();
        }

        /// <summary>
        /// Clears the dirty flag after an external full-profile save (import,
        /// reset) that already persisted current state.
        /// </summary>
        public void MarkClean()
        {
            _dirty = false;
            _debounceRemaining = 0;
            _stagedPrevious = null;
        }
    }
}
