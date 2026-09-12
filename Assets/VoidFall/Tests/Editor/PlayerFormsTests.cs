using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using VoidFall.Core;
using VoidFall.Persistence;

namespace VoidFall.Tests.Editor
{
    /// <summary>
    /// Player form rules (spec §05): the owner's prototype values, the form
    /// starter assignment, the proposed unlock gates, and the save contract
    /// that keeps legacy profiles on the original form.
    /// </summary>
    public sealed class PlayerFormsTests
    {
        [Test]
        public void Default_form_mirrors_the_operative_catalogue()
        {
            Assert.That(PlayerForms.BaseMaxHealth(PlayerForms.DefaultId),
                Is.EqualTo(ContentCatalog.Operative.MaxHealth));
            Assert.That(PlayerForms.MoveSpeedMultiplier(PlayerForms.DefaultId), Is.EqualTo(1f));
            Assert.That(PlayerForms.StartingWeapon(PlayerForms.DefaultId),
                Is.EqualTo(ContentCatalog.Operative.StartingWeapon));
        }

        [Test]
        public void Owner_prototype_values_are_retained()
        {
            Assert.That(PlayerForms.BaseMaxHealth(PlayerForms.DasherId), Is.EqualTo(70));
            Assert.That(PlayerForms.MoveSpeedMultiplier(PlayerForms.DasherId), Is.EqualTo(1.25f));
            Assert.That(PlayerForms.BaseMaxHealth(PlayerForms.BruteId), Is.EqualTo(150));
            Assert.That(PlayerForms.MoveSpeedMultiplier(PlayerForms.BruteId), Is.EqualTo(0.8f));
        }

        [Test]
        public void Forms_start_with_their_owner_assigned_weapons()
        {
            // Owner assignment: the Dasher starts with the Arc Lash and the
            // Brute with the Seeker Launcher; the default keeps the pistol.
            Assert.That(PlayerForms.StartingWeapon(PlayerForms.DasherId), Is.EqualTo("arc"));
            Assert.That(PlayerForms.StartingWeapon(PlayerForms.BruteId), Is.EqualTo("seeker"));
            Assert.That(UpgradeRules.StartingWeaponIndex(PlayerForms.StartingWeapon(PlayerForms.DasherId)),
                Is.GreaterThanOrEqualTo(0), "starter must exist in the catalogue");
            Assert.That(UpgradeRules.StartingWeaponIndex(PlayerForms.StartingWeapon(PlayerForms.BruteId)),
                Is.GreaterThanOrEqualTo(0), "starter must exist in the catalogue");
        }

        [Test]
        public void Unknown_or_empty_ids_fall_back_to_the_default_form()
        {
            Assert.That(PlayerForms.Form("not-a-form").Id, Is.EqualTo(PlayerForms.DefaultId));
            Assert.That(PlayerForms.Form(null).Id, Is.EqualTo(PlayerForms.DefaultId));
            Assert.That(PlayerForms.Form(string.Empty).Id, Is.EqualTo(PlayerForms.DefaultId));
            Assert.That(PlayerForms.NormaliseId("nonsense"), Is.EqualTo(PlayerForms.DefaultId));
        }

        [Test]
        public void Dasher_unlocks_after_the_first_guardian()
        {
            Assert.That(PlayerForms.EvaluateUnlocks(new[] { PlayerForms.DefaultId }, 0, 0),
                Is.Empty);
            Assert.That(PlayerForms.EvaluateUnlocks(new[] { PlayerForms.DefaultId }, 1, 0),
                Is.EqualTo(new[] { PlayerForms.DasherId }));
            Assert.That(PlayerForms.EvaluateUnlocks(new[] { PlayerForms.DefaultId }, 4, 0),
                Is.EqualTo(new[] { PlayerForms.DasherId }), "still only the Dasher gate fires on kills");
        }

        [Test]
        public void Brute_unlocks_after_three_distinct_void_clears()
        {
            Assert.That(PlayerForms.EvaluateUnlocks(new[] { PlayerForms.DefaultId }, 0, 2), Is.Empty);
            Assert.That(PlayerForms.EvaluateUnlocks(new[] { PlayerForms.DefaultId }, 0, 3),
                Is.EqualTo(new[] { PlayerForms.BruteId }));
        }

        [Test]
        public void Both_gates_can_fire_together_and_never_repeat()
        {
            Assert.That(PlayerForms.EvaluateUnlocks(new[] { PlayerForms.DefaultId }, 1, 3),
                Is.EqualTo(new[] { PlayerForms.DasherId, PlayerForms.BruteId }));
            var both = new[] { PlayerForms.DefaultId, PlayerForms.DasherId, PlayerForms.BruteId };
            Assert.That(PlayerForms.EvaluateUnlocks(both, 9, 9), Is.Empty);
        }

        [Test]
        public void Default_form_is_always_unlocked_even_without_a_stored_list()
        {
            Assert.That(PlayerForms.IsUnlocked(null, PlayerForms.DefaultId), Is.True);
            Assert.That(PlayerForms.IsUnlocked(new[] { PlayerForms.DefaultId }, PlayerForms.DasherId), Is.False);
            Assert.That(PlayerForms.IsUnlocked(new[] { PlayerForms.DefaultId, PlayerForms.BruteId }, PlayerForms.BruteId), Is.True);
        }

        [Test]
        public void Save_round_trip_preserves_form_state()
        {
            var directory = Path.Combine(Path.GetTempPath(), "VoidFall-form-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var path = Path.Combine(directory, SaveStore.SaveKey + ".json");
                var store = new SaveStore(path);
                var profile = SaveStore.CreateDefault();
                profile.form = PlayerForms.DasherId;
                profile.unlockedForms = new[] { PlayerForms.DefaultId, PlayerForms.DasherId };
                profile.voidsCleared = new[] { "abyss", "void" };
                store.Save(profile);

                var loaded = store.Load();
                Assert.That(loaded.form, Is.EqualTo(PlayerForms.DasherId));
                Assert.That(loaded.unlockedForms, Is.EqualTo(new[] { PlayerForms.DefaultId, PlayerForms.DasherId }));
                Assert.That(loaded.voidsCleared, Is.EqualTo(new[] { "abyss", "void" }));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void Legacy_save_without_form_fields_defaults_to_the_original_form()
        {
            var directory = Path.Combine(Path.GetTempPath(), "VoidFall-form-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var path = Path.Combine(directory, SaveStore.SaveKey + ".json");
                File.WriteAllText(path, "{\"version\":5,\"parts\":10}");
                var loaded = new SaveStore(path).Load();
                Assert.That(loaded.form, Is.EqualTo(PlayerForms.DefaultId));
                Assert.That(loaded.unlockedForms, Is.EqualTo(new[] { PlayerForms.DefaultId }));
                Assert.That(loaded.voidsCleared, Is.Empty);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void Sanitize_repairs_unknown_form_ids_and_deduplicates()
        {
            var data = SaveStore.CreateDefault();
            data.form = "nonsense";
            data.unlockedForms = new[] { PlayerForms.DefaultId, PlayerForms.DasherId, PlayerForms.DasherId, "nonsense", null };
            data.voidsCleared = new[] { "abyss", "abyss", "", "void" };
            var clean = SaveStore.Sanitize(data);
            Assert.That(clean.form, Is.EqualTo(PlayerForms.DefaultId));
            Assert.That(clean.unlockedForms, Is.EqualTo(new[] { PlayerForms.DefaultId, PlayerForms.DasherId }));
            Assert.That(clean.voidsCleared, Is.EqualTo(new[] { "abyss", "void" }));
        }
    }
}
