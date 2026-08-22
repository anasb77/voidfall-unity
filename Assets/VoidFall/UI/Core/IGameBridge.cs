using System;
using System.Collections.Generic;
using VoidFall.Persistence;

namespace VoidFall.UI
{
    /// <summary>
    /// Game services the menu controllers may use. Owned by VoidFall.UI,
    /// implemented by the runtime; grows one wave at a time alongside the
    /// menu-controllers migration (Docs/Design/MenuControllersMigration.md).
    /// </summary>
    public interface IGameBridge
    {
        /// <summary>Snapshot of the live settings object.</summary>
        SaveSettings CloneLiveSettings();

        /// <summary>Replaces the live settings object with a prior snapshot.</summary>
        void RestoreSettings(SaveSettings snapshot);

        /// <summary>
        /// Persists the current profile (sanitize + save-first contract).
        /// Returns false and surfaces the notice itself when storage fails.
        /// </summary>
        bool TryPersistSettings();

        /// <summary>Persists the whole current profile. Same primitive as TryPersistSettings.</summary>
        bool TryPersistProfile();

        /// <summary>Rebuilds live audio/quality state from current settings.</summary>
        void ApplyLiveSettings();

        /// <summary>Persisted high-score rows; empty when the profile has none.</summary>
        IReadOnlyList<HighScoreEntry> GetHighScores();

        /// <summary>Persisted lifetime stats; null when the profile has none.</summary>
        LifetimeStats GetLifetimeStats();
    }
}
