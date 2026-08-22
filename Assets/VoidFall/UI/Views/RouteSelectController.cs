using System;
using System.Collections.Generic;
using System.Text;
using VoidFall.Core;

namespace VoidFall.UI
{
    /// <summary>One route card, supplied to the view by the controller.</summary>
    public struct RouteCardData
    {
        public string Id;
        public string DisplayName;
        public string ThreatLabel;
        public double ThreatMultiplier;
        public string Description;
        public string ObjectiveSummary;
        public string RewardSummary;
        public int Depth;
        public bool Selectable;
        public string StateLabel;
    }

    /// <summary>
    /// Owns the route-selection domain (spec §6, §7, §30): projects the
    /// labyrinth into route cards, banners, and route history lines, and
    /// performs the guarded choice over the live <see cref="VoidRouteRun"/>.
    /// Pure projection plus mutation through the route run's own rules — no
    /// second state machine that could disagree with it.
    /// </summary>
    public sealed class RouteSelectController
    {
        /// <summary>
        /// Cards for the current choice set: every revealed, available, or
        /// sibling-locked Void that is part of the pending decision. Empty
        /// while a Void is in progress (no decision is pending).
        /// </summary>
        public IReadOnlyList<RouteCardData> BuildCards(VoidRouteRun run)
        {
            var cards = new List<RouteCardData>();
            if (run == null) return cards;

            var available = run.NodesInState(RouteNodeState.Available);
            if (available.Count == 0) return cards;

            foreach (var id in available)
                cards.Add(CardFor(run, id, run.StateOf(id)));

            // Sibling-locked cards ride along greyed out so the player sees
            // the road not taken; hidden Voids never appear (spec §7).
            foreach (var id in run.NodesInState(RouteNodeState.Locked))
                if (IsSiblingOf(run, id, available))
                    cards.Add(CardFor(run, id, RouteNodeState.Locked));

            return cards;
        }

        /// <summary>
        /// Guarded selection. Only available Voids can be chosen; every other
        /// state fails with a player-facing notice and no state change.
        /// </summary>
        public bool Confirm(VoidRouteRun run, string voidId, out string notice)
        {
            if (run == null || string.IsNullOrEmpty(voidId))
            {
                notice = null;
                return false;
            }

            var state = run.StateOf(voidId);
            if (state == RouteNodeState.Hidden)
            {
                notice = "That Void is still beyond the veil.";
                return false;
            }
            if (state == RouteNodeState.Revealed)
            {
                notice = "The rift is not open yet. Complete the current Void first.";
                return false;
            }
            if (state == RouteNodeState.Locked)
            {
                notice = "That path is sealed for this run.";
                return false;
            }
            if (state != RouteNodeState.Available)
            {
                notice = "That Void has already been conquered.";
                return false;
            }

            if (!run.SelectNextVoid(voidId))
            {
                notice = "The Void refused. Choose again.";
                return false;
            }
            notice = "Entering " + run.Node(voidId).DisplayName + ".";
            return true;
        }

        /// <summary>
        /// Banner over the choice set: which layer the offered Voids belong
        /// to. Falls back to the in-progress Void when no choice is pending.
        /// </summary>
        public string BuildBanner(VoidRouteRun run)
        {
            if (run == null) return string.Empty;
            var available = run.NodesInState(RouteNodeState.Available);
            var depth = available.Count > 0
                ? run.Node(available[0]).Depth
                : run.Node(run.CurrentVoidId).Depth;
            switch (depth)
            {
                case 0: return "ABYSS — THE DESCENT BEGINS";
                case 1: return "LAYER I — CHOOSE YOUR VOID";
                case 2: return "LAYER II — THE LABYRINTH DEEPENS";
                case 3: return "THE LAST GATE AWAITS";
                case 4: return "THE FINAL VOID";
                default: return "DEPTH " + depth;
            }
        }

        /// <summary>Route history for HUD/records: "ABYSS → HYDRA → NULL CITY".</summary>
        public string BuildRouteLine(VoidRouteRun run)
        {
            if (run == null || run.History.Count == 0) return string.Empty;
            var builder = new StringBuilder();
            for (var index = 0; index < run.History.Count; index++)
            {
                if (index > 0) builder.Append(" → ");
                builder.Append(run.Node(run.History[index]).DisplayName.ToUpperInvariant());
            }
            return builder.ToString();
        }

        private static RouteCardData CardFor(VoidRouteRun run, string id, RouteNodeState state)
        {
            var node = run.Node(id);
            return new RouteCardData
            {
                Id = id,
                DisplayName = node.DisplayName,
                ThreatLabel = node.ThreatLabel,
                ThreatMultiplier = node.ThreatMultiplier,
                Description = node.Description,
                ObjectiveSummary = node.ObjectiveSummary,
                RewardSummary = node.RewardSummary,
                Depth = node.Depth,
                Selectable = state == RouteNodeState.Available,
                StateLabel = StateLabelFor(state)
            };
        }

        private static string StateLabelFor(RouteNodeState state)
        {
            switch (state)
            {
                case RouteNodeState.Available: return "AVAILABLE";
                case RouteNodeState.Revealed: return "REVEALED";
                case RouteNodeState.Locked: return "SEALED";
                case RouteNodeState.Selected: return "ENTERED";
                case RouteNodeState.Completed: return "CLEARED";
                default: return "HIDDEN";
            }
        }

        /// <summary>
        /// True when the locked node shares a parent with any available node —
        /// i.e. it was part of the choice set before the player narrowed it.
        /// </summary>
        private static bool IsSiblingOf(VoidRouteRun run, string lockedId, List<string> available)
        {
            foreach (var availableId in available)
                if (run.SharesParent(lockedId, availableId)) return true;
            return false;
        }
    }
}
