using System.Collections.Generic;
using NUnit.Framework;
using VoidFall.Core;
using VoidFall.Persistence;
using VoidFall.UI;

namespace VoidFall.Tests.Editor
{
    /// <summary>
    /// Covers the workshop domain: catalog tables, guarded purchase with
    /// rollback, insufficient/max-rank notices, refund accumulation, and row
    /// projection affordability.
    /// </summary>
    public sealed class WorkshopControllerTests
    {
        private sealed class FakeBridge : IGameBridge
        {
            public int PersistCalls;
            public bool PersistSucceeds = true;
            public SaveSettings CloneLiveSettings() => new SaveSettings();
            public void RestoreSettings(SaveSettings snapshot) { }
            public bool TryPersistSettings() { PersistCalls++; return PersistSucceeds; }
            public void ApplyLiveSettings() { }
            public System.Collections.Generic.IReadOnlyList<HighScoreEntry> GetHighScores() => System.Array.Empty<HighScoreEntry>();
            public LifetimeStats GetLifetimeStats() => null;
            public SaveData CandidateSeen;
            public SaveData CommittedProfile;
            public bool TryCommitProfile(SaveData candidate)
            {
                PersistCalls++;
                CandidateSeen = SaveStore.Clone(candidate);
                if (PersistSucceeds) CommittedProfile = CandidateSeen;
                return PersistSucceeds;
            }
        }

        private static WorkshopEntry Entry(string id, int rank) => new WorkshopEntry { id = id, rank = rank };

        [Test]
        public void Forms_include_locked_choices_and_current_starting_stats()
        {
            var forms = new WorkshopController(new FakeBridge()).BuildForms(SaveStore.CreateDefault());
            Assert.That(forms.Count, Is.EqualTo(3));
            Assert.That(forms[0].Id, Is.EqualTo(PlayerForms.DefaultId));
            Assert.That(forms[0].Selected && forms[0].Unlocked, Is.True);
            Assert.That(forms[1].Stats, Is.EqualTo("70 HP · +25% speed"));
            Assert.That(forms[1].StartingWeapon, Is.EqualTo(UpgradeRules.WeaponDisplayName("arc")));
            Assert.That(forms[1].Unlocked, Is.False);
            Assert.That(forms[1].UnlockHint, Is.Not.Empty);
            Assert.That(forms[2].Stats, Is.EqualTo("150 HP · -20% speed"));
            Assert.That(forms[2].StartingWeapon, Is.EqualTo(UpgradeRules.WeaponDisplayName("seeker")));
        }

        [Test]
        public void Selecting_a_form_saves_immediately_without_changing_workshop_or_scraps()
        {
            var bridge = new FakeBridge();
            var controller = new WorkshopController(bridge);
            var profile = SaveStore.CreateDefault();
            profile.unlockedForms = new[] { PlayerForms.DefaultId, PlayerForms.DasherId };
            profile.parts = 42;
            profile.workshop[0].rank = 2;
            Assert.That(controller.TrySelectForm(profile, PlayerForms.DasherId, out _), Is.True);
            Assert.That(profile.form, Is.EqualTo(PlayerForms.DefaultId), "The source is never mutated during staging.");
            profile = bridge.CommittedProfile;
            Assert.That(profile.form, Is.EqualTo(PlayerForms.DasherId));
            Assert.That(bridge.PersistCalls, Is.EqualTo(1));
            Assert.That(profile.parts, Is.EqualTo(42));
            Assert.That(profile.workshop[0].rank, Is.EqualTo(2));
            Assert.That(controller.TrySelectForm(profile, PlayerForms.DasherId, out _), Is.True);
            Assert.That(bridge.PersistCalls, Is.EqualTo(1), "Selecting the same form is a no-op.");
        }

        [Test]
        public void Locked_or_unknown_forms_cannot_be_selected()
        {
            var bridge = new FakeBridge();
            var controller = new WorkshopController(bridge);
            var profile = SaveStore.CreateDefault();
            Assert.That(controller.TrySelectForm(profile, PlayerForms.BruteId, out var notice), Is.False);
            Assert.That(notice, Is.EqualTo(PlayerForms.Form(PlayerForms.BruteId).UnlockHint));
            Assert.That(controller.TrySelectForm(profile, "unknown", out _), Is.False);
            Assert.That(profile.form, Is.EqualTo(PlayerForms.DefaultId));
            Assert.That(bridge.PersistCalls, Is.Zero);
        }

        [Test]
        public void Failed_form_save_restores_the_previous_selection()
        {
            var bridge = new FakeBridge { PersistSucceeds = false };
            var profile = SaveStore.CreateDefault();
            profile.unlockedForms = new[] { PlayerForms.DefaultId, PlayerForms.DasherId };
            Assert.That(new WorkshopController(bridge).TrySelectForm(profile, PlayerForms.DasherId, out var notice), Is.False);
            Assert.That(profile.form, Is.EqualTo(PlayerForms.DefaultId));
            Assert.That(notice, Does.Contain("could not be saved"));
        }

        private static readonly string[] Order =
        {
            "integrity", "power", "mobility", "recovery",
            "magnet", "precision", "arsenal", "protocol"
        };

        [Test]
        public void Cost_table_matches_the_source_values()
        {
            Assert.That(WorkshopController.CostFor("integrity", 0), Is.EqualTo(35));
            Assert.That(WorkshopController.CostFor("integrity", 2), Is.EqualTo(130));
            Assert.That(WorkshopController.CostFor("integrity", 3), Is.EqualTo(-1));
            Assert.That(WorkshopController.CostFor("protocol", 0), Is.EqualTo(120));
            Assert.That(WorkshopController.CostFor("protocol", 1), Is.EqualTo(-1));
        }

        [Test]
        public void Purchase_success_deducts_parts_and_increments_rank()
        {
            var bridge = new FakeBridge();
            var controller = new WorkshopController(bridge);
            var entries = new[] { Entry("integrity", 0) };
            var profile = SaveStore.CreateDefault();
            profile.parts = 35;

            profile.workshop = entries;
            var ok = controller.TryPurchase(profile, "integrity", out var notice);

            Assert.That(ok, Is.True);
            Assert.That(bridge.CandidateSeen.parts, Is.Zero, "Persistence sees the debit, not the old wallet.");
            Assert.That(profile.parts, Is.EqualTo(35));
            Assert.That(bridge.CandidateSeen.workshop[0].rank, Is.EqualTo(1));
            Assert.That(entries[0].rank, Is.Zero);
            Assert.That(bridge.PersistCalls, Is.EqualTo(1));
            Assert.That(notice, Does.Contain("rank 1"));
        }

        [Test]
        public void Insufficient_parts_reports_the_shortfall_without_spending()
        {
            var bridge = new FakeBridge();
            var controller = new WorkshopController(bridge);
            var entries = new[] { Entry("power", 0) };
            var profile = SaveStore.CreateDefault();
            profile.parts = 44;

            profile.workshop = entries;
            var ok = controller.TryPurchase(profile, "power", out var notice);

            Assert.That(ok, Is.False);
            Assert.That(profile.parts, Is.EqualTo(44), "balance must not change on a rejected purchase");
            Assert.That(entries[0].rank, Is.EqualTo(0));
            Assert.That(bridge.PersistCalls, Is.EqualTo(0), "nothing to persist when the purchase is rejected");
            Assert.That(notice, Does.Contain("Need 1 more Scraps."));
        }

        [Test]
        public void Max_rank_reports_completion()
        {
            var bridge = new FakeBridge();
            var controller = new WorkshopController(bridge);
            var entries = new[] { Entry("integrity", 3) };
            var profile = SaveStore.CreateDefault();
            profile.parts = 999;

            profile.workshop = entries;
            var ok = controller.TryPurchase(profile, "integrity", out var notice);

            Assert.That(ok, Is.False);
            Assert.That(profile.parts, Is.EqualTo(999));
            Assert.That(notice, Does.Contain("maximum rank"));
        }

        [Test]
        public void Storage_failure_rolls_back_parts_and_rank()
        {
            var bridge = new FakeBridge { PersistSucceeds = false };
            var controller = new WorkshopController(bridge);
            var entries = new[] { Entry("magnet", 1) };
            var profile = SaveStore.CreateDefault();
            profile.parts = 60;

            profile.workshop = entries;
            var ok = controller.TryPurchase(profile, "magnet", out var notice);

            Assert.That(ok, Is.False);
            Assert.That(profile.parts, Is.EqualTo(60), "Parts must be rolled back when storage fails");
            Assert.That(entries[0].rank, Is.EqualTo(1), "rank must be rolled back when storage fails");
            Assert.That(notice, Does.Contain("could not be saved"));
        }

        [Test]
        public void Refund_all_returns_original_costs_and_zeroes_ranks()
        {
            var bridge = new FakeBridge();
            var controller = new WorkshopController(bridge);
            // integrity rank 2 (35+75) + power rank 1 (45) + protocol rank 1 (120)
            var entries = new[] { Entry("integrity", 2), Entry("power", 1), Entry("protocol", 1) };
            var profile = SaveStore.CreateDefault();
            profile.parts = 10;

            profile.workshop = entries;
            Assert.That(controller.TryRefundAll(profile, out var refunded, out _), Is.True);

            Assert.That(refunded, Is.EqualTo(275));
            Assert.That(profile.parts, Is.EqualTo(10));
            Assert.That(bridge.CandidateSeen.parts, Is.EqualTo(285));
            foreach (var entry in bridge.CandidateSeen.workshop) Assert.That(entry.rank, Is.EqualTo(0));
            Assert.That(bridge.PersistCalls, Is.EqualTo(1));
        }

        [Test]
        public void Refund_with_no_workshop_data_is_a_no_op()
        {
            var bridge = new FakeBridge();
            var controller = new WorkshopController(bridge);
            var profile = SaveStore.CreateDefault();
            profile.parts = 10;

            Assert.That(controller.TryRefundAll(null, out var refunded, out _), Is.True);

            Assert.That(refunded, Is.EqualTo(0));
            Assert.That(profile.parts, Is.EqualTo(10));
            Assert.That(bridge.PersistCalls, Is.EqualTo(0));
        }

        [Test]
        public void Failed_refund_retains_the_entire_profile_and_does_not_report_scraps()
        {
            var bridge = new FakeBridge { PersistSucceeds = false };
            var profile = SaveStore.CreateDefault();
            profile.parts = 65;
            profile.workshop[0].rank = 1;
            Assert.That(new WorkshopController(bridge).TryRefundAll(profile, out var refunded, out var notice), Is.False);
            Assert.That(refunded, Is.Zero);
            Assert.That(profile.parts, Is.EqualTo(65));
            Assert.That(profile.workshop[0].rank, Is.EqualTo(1));
            Assert.That(bridge.CandidateSeen.parts, Is.EqualTo(100));
            Assert.That(bridge.CandidateSeen.workshop[0].rank, Is.Zero);
            Assert.That(bridge.CommittedProfile, Is.Null);
            Assert.That(notice, Does.Contain("could not be saved"));
        }

        [Test]
        public void Rows_project_order_affordability_and_protocol_single_rank()
        {
            var bridge = new FakeBridge();
            var controller = new WorkshopController(bridge);
            var entries = new[] { Entry("integrity", 2), Entry("protocol", 0) };

            var rows = controller.BuildRows(Order, 50, entries);

            Assert.That(rows.Count, Is.EqualTo(8));
            Assert.That(rows[0].Id, Is.EqualTo("integrity"));
            Assert.That(rows[0].CurrentRank, Is.EqualTo(2));
            Assert.That(rows[0].MaxRank, Is.EqualTo(SaveStore.WorkshopMaxRank));
            Assert.That(rows[0].Cost, Is.EqualTo(130), "rank 2 is not yet maxed (max = 3)");
            Assert.That(rows[0].CanAfford, Is.False, "balance of 50 cannot afford 130");
            Assert.That(rows[7].Id, Is.EqualTo("protocol"));
            Assert.That(rows[7].MaxRank, Is.EqualTo(1));
            Assert.That(rows[7].CanAfford, Is.False, "balance of 50 cannot afford 120");
            Assert.That(rows[4].Id, Is.EqualTo("magnet"));
            Assert.That(rows[4].Cost, Is.EqualTo(25));
            Assert.That(rows[4].CanAfford, Is.True);
        }
    }
}
