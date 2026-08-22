using System;
using System.Collections.Generic;
using VoidFall.Persistence;

namespace VoidFall.UI
{
    /// <summary>
    /// Owns the workshop domain: catalog lookups, row projection for the
    /// screen, and the guarded purchase/refund transactions over the live
    /// profile. Wave 3 of the menu-controllers migration; tables are verbatim
    /// from the runtime originals.
    /// </summary>
    public sealed class WorkshopController
    {
        private readonly IGameBridge _bridge;

        public WorkshopController(IGameBridge bridge)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        public static string NameFor(string id)
        {
            switch (id)
            {
                case "integrity": return "Integrity";
                case "power": return "Power";
                case "mobility": return "Mobility";
                case "recovery": return "Recovery";
                case "magnet": return "Magnet";
                case "precision": return "Precision";
                case "arsenal": return "Arsenal";
                case "protocol": return "Revival Protocol";
                default: return id;
            }
        }

        public static string DescriptionFor(string id)
        {
            switch (id)
            {
                case "integrity": return "+5 maximum health per rank.";
                case "power": return "+4% weapon damage per rank.";
                case "mobility": return "+3% movement speed per rank.";
                case "recovery": return "Restore 3 health after each level per rank.";
                case "magnet": return "+8 pickup radius per rank.";
                case "precision": return "+2% critical chance per rank.";
                case "arsenal": return "Weapons recover 3% faster per rank.";
                case "protocol": return "+1 revive per run. Maximum one in this slice.";
                default: return "Permanent upgrade.";
            }
        }

        public static int CostFor(string id, int rank)
        {
            switch (id)
            {
                case "integrity": return rank == 0 ? 35 : rank == 1 ? 75 : rank == 2 ? 130 : -1;
                case "power": return rank == 0 ? 45 : rank == 1 ? 95 : rank == 2 ? 165 : -1;
                case "mobility": return rank == 0 ? 40 : rank == 1 ? 85 : rank == 2 ? 145 : -1;
                case "recovery": return rank == 0 ? 30 : rank == 1 ? 70 : rank == 2 ? 120 : -1;
                case "magnet": return rank == 0 ? 25 : rank == 1 ? 60 : rank == 2 ? 105 : -1;
                case "precision": return rank == 0 ? 50 : rank == 1 ? 110 : rank == 2 ? 190 : -1;
                case "arsenal": return rank == 0 ? 90 : rank == 1 ? 150 : rank == 2 ? 195 : -1;
                case "protocol": return rank == 0 ? 120 : -1;
                default: return -1;
            }
        }

        public static int MaxRankFor(string id)
        {
            return id == "protocol" ? 1 : SaveStore.WorkshopMaxRank;
        }

        private static WorkshopEntry FindEntry(IList<WorkshopEntry> entries, string id)
        {
            if (entries == null) return null;
            foreach (var entry in entries)
            {
                if (entry != null && entry.id == id) return entry;
            }
            return null;
        }

        /// <summary>
        /// Projects the eight upgrade rows in source order, including current
        /// affordability against the passed balance.
        /// </summary>
        public IReadOnlyList<WorkshopItemData> BuildRows(
            IReadOnlyList<string> order,
            int partsBalance,
            IList<WorkshopEntry> entries)
        {
            var list = new List<WorkshopItemData>();
            foreach (var id in order)
            {
                var entry = FindEntry(entries, id);
                var rank = entry?.rank ?? 0;
                var maxRank = MaxRankFor(id);
                var cost = CostFor(id, rank);
                list.Add(new WorkshopItemData
                {
                    Id = id,
                    Name = NameFor(id),
                    Description = DescriptionFor(id),
                    CurrentRank = rank,
                    MaxRank = maxRank,
                    Cost = cost,
                    CanAfford = partsBalance >= cost && cost >= 0
                });
            }
            return list;
        }

        /// <summary>
        /// Guarded purchase: validates cost and balance, deducts Parts,
        /// increments the rank, persists, and rolls both back when storage
        /// fails. Returns false with a player-facing notice on every failure
        /// path.
        /// </summary>
        public bool TryPurchase(IList<WorkshopEntry> entries, ref int parts, string id, out string notice)
        {
            var entry = FindEntry(entries, id);
            if (entry == null)
            {
                notice = null;
                return false;
            }

            var cost = CostFor(id, entry.rank);
            if (cost < 0)
            {
                notice = "That upgrade is already at maximum rank.";
                return false;
            }
            if (parts < cost)
            {
                notice = $"Need {cost - parts} more Parts.";
                return false;
            }

            parts -= cost;
            entry.rank++;
            if (_bridge.TryPersistProfile())
            {
                notice = $"{NameFor(id)} upgraded to rank {entry.rank}. Applies next run.";
                return true;
            }

            // Storage failed: roll the purchase back so the shown balance
            // matches what is on disk.
            parts += cost;
            entry.rank--;
            notice = "Purchase could not be saved. Parts were not spent.";
            return false;
        }

        /// <summary>
        /// Refunds every purchased rank at its original cost, zeroes all
        /// ranks, and persists. Returns the refunded Part total (0 when there
        /// is nothing to refund or no workshop data exists).
        /// </summary>
        public int RefundAll(IList<WorkshopEntry> entries, ref int parts)
        {
            if (entries == null) return 0;
            var refundedParts = 0;
            foreach (var entry in entries)
            {
                if (entry == null) continue;
                for (var r = 0; r < entry.rank; r++)
                {
                    var cost = CostFor(entry.id, r);
                    if (cost > 0) refundedParts += cost;
                }
                entry.rank = 0;
            }
            parts += refundedParts;
            _bridge.TryPersistProfile();
            return refundedParts;
        }
    }
}
