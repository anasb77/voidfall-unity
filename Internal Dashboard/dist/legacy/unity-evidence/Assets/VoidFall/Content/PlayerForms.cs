using System;

namespace VoidFall.Core
{
    /// <summary>
    /// Player forms (design spec §05): one eye, three ways to fight. Forms
    /// change starting strengths and the silhouette, never the protagonist.
    ///
    /// The owner's prototype values are retained: 100/70/150 base health and
    /// normal/+25%/−20% movement. The default form deliberately mirrors
    /// ContentCatalog.Operative instead of restating its numbers, so legacy
    /// profiles and the golden-master path behave exactly as before. The
    /// Dasher starts with the Arc Lash and the Brute with the Seeker Launcher
    /// (owner's assignment); the default keeps the Operative's pistol.
    ///
    /// Unlock gates are the spec's implementation proposals [P]: one guardian
    /// defeat for the Dasher, three distinct Void clears across runs for the
    /// Brute. Form access is separate from legendary fragments and from the
    /// shared Workshop ranks.
    /// </summary>
    public static class PlayerForms
    {
        public const string DefaultId = "default";
        public const string DasherId = "dasher";
        public const string BruteId = "brute";

        /// <summary>Guardian defeats that open the Dasher form.</summary>
        public const int DasherGuardianKills = 1;

        /// <summary>Distinct Void clears, across runs, that open the Brute form.</summary>
        public const int BruteDistinctVoids = 3;

        public sealed class FormDefinition
        {
            public string Id;
            public string Name;
            public int MaxHealth;
            public float MoveSpeedMultiplier;
            /// <summary>Null keeps the Operative's authored starting weapon.</summary>
            public string StartingWeapon;
            public string Blurb;
            /// <summary>Null when available from the start.</summary>
            public string UnlockHint;
            public bool AvailableFromStart;
        }

        public static readonly FormDefinition[] All =
        {
            new FormDefinition
            {
                Id = DefaultId,
                Name = "Default",
                MaxHealth = 0, // resolved through ContentCatalog.Operative
                MoveSpeedMultiplier = 1f,
                StartingWeapon = null,
                Blurb = "The existing blue eye; balanced, flexible starting point.",
                UnlockHint = null,
                AvailableFromStart = true
            },
            new FormDefinition
            {
                Id = DasherId,
                Name = "Dasher",
                MaxHealth = 70,
                MoveSpeedMultiplier = 1.25f,
                StartingWeapon = "arc",
                Blurb = "Cursor-like silhouette inspired by the Dasher enemy; fast repositioning, fragile.",
                UnlockHint = "Defeat the first guardian.",
                AvailableFromStart = false
            },
            new FormDefinition
            {
                Id = BruteId,
                Name = "Brute",
                MaxHealth = 150,
                MoveSpeedMultiplier = 0.8f,
                StartingWeapon = "seeker",
                Blurb = "Heavier Brute-inspired silhouette; more staying power, slower repositioning.",
                UnlockHint = "Clear 3 distinct Voids across runs.",
                AvailableFromStart = false
            },
        };

        public static FormDefinition Default => All[0];

        /// <summary>Resolves any stored id to a form; unknown or empty falls back to the default so old saves keep the original form.</summary>
        public static FormDefinition Form(string id)
        {
            for (var index = 0; index < All.Length; index++)
            {
                if (string.Equals(All[index].Id, id, StringComparison.Ordinal)) return All[index];
            }
            return Default;
        }

        public static bool IsKnown(string id) => FindExact(id) != null;

        public static string NormaliseId(string id) => FindExact(id)?.Id ?? DefaultId;

        public static int IndexOf(string id)
        {
            for (var index = 0; index < All.Length; index++)
            {
                if (string.Equals(All[index].Id, id, StringComparison.Ordinal)) return index;
            }
            return 0;
        }

        public static bool IsDefault(string id) => NormaliseId(id) == DefaultId;

        /// <summary>
        /// The form's base maximum health. The default form defers to the
        /// Operative catalogue entry so the two can never drift apart.
        /// </summary>
        public static int BaseMaxHealth(string id)
        {
            var form = FindExact(id);
            return form != null && form.Id != DefaultId ? form.MaxHealth : (int)ContentCatalog.Operative.MaxHealth;
        }

        /// <summary>
        /// The form's movement factor. Applied exactly once, alongside the
        /// normal support and Workshop buffs in stat recalculation.
        /// </summary>
        public static float MoveSpeedMultiplier(string id)
        {
            var form = FindExact(id);
            return form != null && form.Id != DefaultId ? form.MoveSpeedMultiplier : 1f;
        }

        /// <summary>The weapon this form starts a run with, at rank I.</summary>
        public static string StartingWeapon(string id)
        {
            var form = FindExact(id);
            return form != null && !string.IsNullOrEmpty(form.StartingWeapon)
                ? form.StartingWeapon
                : ContentCatalog.Operative.StartingWeapon;
        }

        /// <summary>
        /// Returns form ids that should newly unlock given lifetime progress.
        /// Pure and allocation-light: an empty array means nothing changed.
        /// </summary>
        public static string[] EvaluateUnlocks(string[] unlocked, int guardianKills, int distinctVoidsCleared)
        {
            Func<string, bool> has = id => IndexOfUnlocked(unlocked, id) >= 0;
            var dasher = !has(DasherId) && guardianKills >= DasherGuardianKills;
            var brute = !has(BruteId) && distinctVoidsCleared >= BruteDistinctVoids;
            if (!dasher && !brute) return Array.Empty<string>();
            if (dasher && brute) return new[] { DasherId, BruteId };
            return dasher ? new[] { DasherId } : new[] { BruteId };
        }

        public static bool IsUnlocked(string[] unlocked, string id)
        {
            if (Form(id).AvailableFromStart) return true;
            return IndexOfUnlocked(unlocked, NormaliseId(id)) >= 0;
        }

        private static int IndexOfUnlocked(string[] unlocked, string id)
        {
            if (unlocked == null) return -1;
            for (var index = 0; index < unlocked.Length; index++)
            {
                if (string.Equals(unlocked[index], id, StringComparison.Ordinal)) return index;
            }
            return -1;
        }

        private static FormDefinition FindExact(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (var index = 0; index < All.Length; index++)
            {
                if (string.Equals(All[index].Id, id, StringComparison.Ordinal)) return All[index];
            }
            return null;
        }
    }
}
