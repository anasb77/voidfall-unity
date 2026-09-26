using System;
using System.Collections.Generic;
using VoidFall.Core;
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

        public IReadOnlyList<WorkshopFormData> BuildForms(SaveData profile)
        {
            var forms = new List<WorkshopFormData>(PlayerForms.All.Length);
            foreach (var form in PlayerForms.All)
            {
                var speedPercent = (int)Math.Round((PlayerForms.MoveSpeedMultiplier(form.Id) - 1f) * 100f);
                forms.Add(new WorkshopFormData
                {
                    Id = form.Id,
                    Name = form.Name,
                    Stats = PlayerForms.BaseMaxHealth(form.Id) + " HP · " +
                        (speedPercent == 0 ? "Normal speed" : (speedPercent > 0 ? "+" : "") + speedPercent + "% speed"),
                    StartingWeapon = UpgradeRules.WeaponDisplayName(PlayerForms.StartingWeapon(form.Id)),
                    UnlockHint = form.UnlockHint,
                    Unlocked = PlayerForms.IsUnlocked(profile?.unlockedForms, form.Id),
                    Selected = PlayerForms.NormaliseId(profile?.form) == form.Id
                });
            }
            return forms;
        }

        public bool TrySelectForm(SaveData profile, string id, out string notice)
        {
            notice = null;
            if (profile == null || !PlayerForms.IsKnown(id)) return false;
            if (!PlayerForms.IsUnlocked(profile.unlockedForms, id))
            {
                notice = PlayerForms.Form(id).UnlockHint;
                return false;
            }
            if (profile.form == id) return true;

            var candidate = SaveStore.Clone(profile);
            candidate.form = id;
            if (_bridge.TryCommitProfile(candidate)) return true;
            notice = "Form selection could not be saved. Your previous form is still selected.";
            return false;
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

        /// <summary>Commits the rank and its complete wallet debit together.</summary>
        public bool TryPurchase(SaveData profile, string id, out string notice)
        {
            var entry = FindEntry(profile?.workshop, id);
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
            if (profile.parts < cost)
            {
                notice = $"Need {cost - profile.parts} more Scraps.";
                return false;
            }

            var candidate = SaveStore.Clone(profile);
            candidate.parts -= cost;
            var purchased = FindEntry(candidate.workshop, id);
            purchased.rank++;
            if (_bridge.TryCommitProfile(candidate))
            {
                notice = $"{NameFor(id)} upgraded to rank {purchased.rank}. Applies next run.";
                return true;
            }
            notice = "Purchase could not be saved. Scraps were not spent.";
            return false;
        }

        /// <summary>Refunds original costs in the same commit that removes the ranks.</summary>
        public bool TryRefundAll(SaveData profile, out int refundedParts, out string notice)
        {
            refundedParts = 0;
            notice = null;
            if (profile?.workshop == null) return true;
            var candidate = SaveStore.Clone(profile);
            foreach (var entry in candidate.workshop)
            {
                if (entry == null) continue;
                for (var rank = 0; rank < entry.rank; rank++)
                {
                    var cost = CostFor(entry.id, rank);
                    if (cost > 0) refundedParts += cost;
                }
                entry.rank = 0;
            }
            if (refundedParts == 0) return true;
            candidate.parts = (int)Math.Min(999_999_999L, (long)candidate.parts + refundedParts);
            if (_bridge.TryCommitProfile(candidate)) return true;
            refundedParts = 0;
            notice = "Refund could not be saved. Your upgrades and Scraps are unchanged.";
            return false;
        }
    }
}
