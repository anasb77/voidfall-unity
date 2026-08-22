using System.Collections.Generic;
using NUnit.Framework;
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
            public bool TryPersistProfile() { PersistCalls++; return PersistSucceeds; }
        }

        private static WorkshopEntry Entry(string id, int rank) => new WorkshopEntry { id = id, rank = rank };

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
            var entries = new List<WorkshopEntry> { Entry("integrity", 0) };
            var parts = 35;

            var ok = controller.TryPurchase(entries, ref parts, "integrity", out var notice);

            Assert.That(ok, Is.True);
            Assert.That(parts, Is.EqualTo(0));
            Assert.That(entries[0].rank, Is.EqualTo(1));
            Assert.That(bridge.PersistCalls, Is.EqualTo(1));
            Assert.That(notice, Does.Contain("rank 1"));
        }

        [Test]
        public void Insufficient_parts_reports_the_shortfall_without_spending()
        {
            var bridge = new FakeBridge();
            var controller = new WorkshopController(bridge);
            var entries = new List<WorkshopEntry> { Entry("power", 0) };
            var parts = 44;

            var ok = controller.TryPurchase(entries, ref parts, "power", out var notice);

            Assert.That(ok, Is.False);
            Assert.That(parts, Is.EqualTo(44), "balance must not change on a rejected purchase");
            Assert.That(entries[0].rank, Is.EqualTo(0));
            Assert.That(bridge.PersistCalls, Is.EqualTo(0), "nothing to persist when the purchase is rejected");
            Assert.That(notice, Does.Contain("Need 1 more Parts."));
        }

        [Test]
        public void Max_rank_reports_completion()
        {
            var bridge = new FakeBridge();
            var controller = new WorkshopController(bridge);
            var entries = new List<WorkshopEntry> { Entry("integrity", 3) };
            var parts = 999;

            var ok = controller.TryPurchase(entries, ref parts, "integrity", out var notice);

            Assert.That(ok, Is.False);
            Assert.That(parts, Is.EqualTo(999));
            Assert.That(notice, Does.Contain("maximum rank"));
        }

        [Test]
        public void Storage_failure_rolls_back_parts_and_rank()
        {
            var bridge = new FakeBridge { PersistSucceeds = false };
            var controller = new WorkshopController(bridge);
            var entries = new List<WorkshopEntry> { Entry("magnet", 1) };
            var parts = 60;

            var ok = controller.TryPurchase(entries, ref parts, "magnet", out var notice);

            Assert.That(ok, Is.False);
            Assert.That(parts, Is.EqualTo(60), "Parts must be rolled back when storage fails");
            Assert.That(entries[0].rank, Is.EqualTo(1), "rank must be rolled back when storage fails");
            Assert.That(notice, Does.Contain("could not be saved"));
        }

        [Test]
        public void Refund_all_returns_original_costs_and_zeroes_ranks()
        {
            var bridge = new FakeBridge();
            var controller = new WorkshopController(bridge);
            // integrity rank 2 (35+75) + power rank 1 (45) + protocol rank 1 (120)
            var entries = new List<WorkshopEntry> { Entry("integrity", 2), Entry("power", 1), Entry("protocol", 1) };
            var parts = 10;

            var refunded = controller.RefundAll(entries, ref parts);

            Assert.That(refunded, Is.EqualTo(275));
            Assert.That(parts, Is.EqualTo(285));
            foreach (var entry in entries) Assert.That(entry.rank, Is.EqualTo(0));
            Assert.That(bridge.PersistCalls, Is.EqualTo(1));
        }

        [Test]
        public void Refund_with_no_workshop_data_is_a_no_op()
        {
            var bridge = new FakeBridge();
            var controller = new WorkshopController(bridge);
            var parts = 10;

            var refunded = controller.RefundAll(null, ref parts);

            Assert.That(refunded, Is.EqualTo(0));
            Assert.That(parts, Is.EqualTo(10));
            Assert.That(bridge.PersistCalls, Is.EqualTo(0));
        }

        [Test]
        public void Rows_project_order_affordability_and_protocol_single_rank()
        {
            var bridge = new FakeBridge();
            var controller = new WorkshopController(bridge);
            var entries = new List<WorkshopEntry> { Entry("integrity", 2), Entry("protocol", 0) };

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
