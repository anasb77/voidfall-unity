using System;
using System.Collections.Generic;

namespace VoidFall.Core
{
    public enum RouteNodeState
    {
        Hidden = 0,
        Revealed = 1,
        Available = 2,
        Selected = 3,
        Completed = 4,
        Locked = 5
    }

    /// <summary>
    /// One Void in the labyrinth (spec §4). Plain data so definitions can be
    /// authored as ScriptableObjects, JSON, or the built-in prototype graph
    /// without the graph logic caring.
    /// </summary>
    public sealed class VoidRouteNode
    {
        public string Id;
        public string DisplayName;
        public int Depth;
        public double ThreatMultiplier;
        public string ThreatLabel;
        public string Description;
        public string ObjectiveSummary;
        public string RewardSummary;
        public List<string> Outgoing = new List<string>();

        public VoidRouteNode(
            string id,
            string displayName,
            int depth,
            double threatMultiplier,
            string threatLabel,
            string description,
            string objectiveSummary,
            string rewardSummary,
            params string[] outgoing)
        {
            Id = id;
            DisplayName = displayName;
            Depth = depth;
            ThreatMultiplier = threatMultiplier;
            ThreatLabel = threatLabel;
            Description = description;
            ObjectiveSummary = objectiveSummary;
            RewardSummary = rewardSummary;
            if (outgoing != null && outgoing.Length > 0) Outgoing.AddRange(outgoing);
        }
    }

    /// <summary>
    /// Tracks node states for one run over a fixed route graph (spec §7).
    /// All mutations go through NotifyVoidCompleted/SelectNextVoid so the
    /// reveal and sibling-locking rules cannot be bypassed.
    /// </summary>
    public sealed class VoidRouteRun
    {
        private readonly Dictionary<string, VoidRouteNode> _nodes =
            new Dictionary<string, VoidRouteNode>(StringComparer.Ordinal);
        private readonly Dictionary<string, RouteNodeState> _states =
            new Dictionary<string, RouteNodeState>(StringComparer.Ordinal);
        private readonly List<string> _history = new List<string>();

        public VoidRouteRun(IEnumerable<VoidRouteNode> nodes, string startId)
        {
            foreach (var node in nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.Id))
                    throw new ArgumentException("route node without id");
                _nodes.Add(node.Id, node);
            }
            if (!_nodes.ContainsKey(startId))
                throw new ArgumentException("start node '" + startId + "' not in graph");
            StartId = startId;

            foreach (var pair in _nodes) _states[pair.Key] = RouteNodeState.Hidden;

            // Initial view (spec §7): the mandatory beginning is available and
            // its children are revealed as the "?" preview of the next layer.
            CurrentVoidId = startId;
            _states[startId] = RouteNodeState.Selected;
            _history.Add(startId);
            foreach (var child in _nodes[startId].Outgoing)
                _states[child] = RouteNodeState.Revealed;
        }

        public string StartId { get; }
        public string CurrentVoidId { get; private set; }
        public bool HasEscaped { get; private set; }
        public IReadOnlyList<string> History => _history;

        public bool IsFinalVoid(string voidId) =>
            _nodes.TryGetValue(voidId, out var node) && node.Outgoing.Count == 0;

        public VoidRouteNode Node(string voidId) => _nodes[voidId];
        public RouteNodeState StateOf(string voidId) =>
            _states.TryGetValue(voidId, out var state) ? state : RouteNodeState.Hidden;

        /// <summary>Threat multiplier for spawn pressure in the given Void.</summary>
        public double ThreatOf(string voidId) =>
            _nodes.TryGetValue(voidId, out var node) ? node.ThreatMultiplier : 1;

        public List<string> NodesInState(RouteNodeState state)
        {
            var result = new List<string>();
            foreach (var pair in _states)
                if (pair.Value == state) result.Add(pair.Key);
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        /// <summary>
        /// True when some Void leads to both ids (or they are the same node) —
        /// i.e. the two nodes were ever presented as one choice set.
        /// </summary>
        public bool SharesParent(string a, string b)
        {
            if (string.Equals(a, b, StringComparison.Ordinal)) return true;
            foreach (var pair in _nodes)
                if (pair.Value.Outgoing.Contains(a) && pair.Value.Outgoing.Contains(b))
                    return true;
            return false;
        }

        /// <summary>
        /// Marks the current Void's objective complete, reveals its children,
        /// and makes them choosable. Returns false if the void is not the
        /// run's current void or was already completed.
        /// </summary>
        public bool NotifyVoidCompleted(string voidId)
        {
            if (!_nodes.ContainsKey(voidId))
                throw new ArgumentException("unknown void '" + voidId + "'");
            if (!string.Equals(voidId, CurrentVoidId, StringComparison.Ordinal) ||
                _states[voidId] != RouteNodeState.Selected)
                return false;

            _states[voidId] = RouteNodeState.Completed;
            var node = _nodes[voidId];
            if (node.Outgoing.Count == 0)
            {
                HasEscaped = true;
                return true;
            }
            foreach (var child in node.Outgoing)
            {
                if (_states[child] == RouteNodeState.Hidden) _states[child] = RouteNodeState.Revealed;
                _states[child] = RouteNodeState.Available;
            }
            return true;
        }

        /// <summary>
        /// Chooses the next Void among the available ones and locks its
        /// siblings (spec §7: selecting one node locks its siblings, and the
        /// prototype has no backtracking). Available nodes are always the
        /// children of the just-completed Void, so locking every other
        /// available node is exactly sibling locking — including for shared
        /// Layer II nodes reachable from two different parents.
        /// </summary>
        public bool SelectNextVoid(string voidId)
        {
            if (!_nodes.ContainsKey(voidId))
                throw new ArgumentException("unknown void '" + voidId + "'");
            if (_states[voidId] != RouteNodeState.Available) return false;

            var toLock = new List<string>();
            foreach (var pair in _states)
                if (pair.Value == RouteNodeState.Available && pair.Key != voidId)
                    toLock.Add(pair.Key);
            foreach (var sibling in toLock) _states[sibling] = RouteNodeState.Locked;
            _states[voidId] = RouteNodeState.Selected;
            CurrentVoidId = voidId;
            _history.Add(voidId);
            return true;
        }

        /// <summary>
        /// The prototype labyrinth from spec §3/§9: fixed topology, depth
        /// threat multipliers, and the route-card copy from §6/§30.
        /// </summary>
        public static VoidRouteRun PrototypeGraph()
        {
            return new VoidRouteRun(PrototypeNodes(), "abyss");
        }

        public static List<VoidRouteNode> PrototypeNodes()
        {
            return new List<VoidRouteNode>
            {
                new VoidRouteNode(
                    "abyss", "Abyss", 0, 1.00, "BASELINE",
                    "The baseline reality. Establish a build before the labyrinth begins.",
                    "Survive the escalation, kill the Gatekeeper",
                    "Normal upgrade",
                    "red-nebula", "white-sakura", "hydra"),
                new VoidRouteNode(
                    "red-nebula", "Red Nebula", 1, 1.20, "VOLATILE",
                    "The environment is a weapon. Meteor storms strike everything.",
                    "Destroy 3 Void Anchors",
                    "Volatile Boon (explosions)",
                    "dead-orbit", "null-city"),
                new VoidRouteNode(
                    "white-sakura", "White Sakura", 1, 1.20, "PRECISION",
                    "Hold your ground inside beautiful projectile hell.",
                    "Stabilize the moving Rift Zones",
                    "Precision Boon (crit)",
                    "monochrome-court", "graveyard"),
                new VoidRouteNode(
                    "hydra", "Hydra", 1, 1.20, "HIGH THREAT",
                    "Enemies evolve by stealing each other's traits.",
                    "Destroy 3 Gene Nodes, kill Hydra Prime",
                    "Spliced Boon (mutation)",
                    "null-city", "monochrome-court"),
                new VoidRouteNode(
                    "dead-orbit", "Dead Orbit", 2, 1.50, "HAZARD",
                    "The battlefield itself is moving. Debris sweeps the lanes.",
                    "Reactivate 3 Navigation Beacons",
                    "Arsenal Boon (weapons)",
                    "last-gate"),
                new VoidRouteNode(
                    "graveyard", "Graveyard", 2, 1.50, "ATTRITION",
                    "Dead enemies refuse to remain dead.",
                    "Kill 3 marked elites, kill the Gravekeeper",
                    "Sustain Boon (defense)",
                    "last-gate"),
                new VoidRouteNode(
                    "monochrome-court", "Monochrome Court", 2, 1.50, "HIGH THREAT",
                    "Learn the rules of two chess armies.",
                    "Survive both board cycles, reach Checkmate",
                    "White speed or Black durability",
                    "last-gate"),
                new VoidRouteNode(
                    "null-city", "Null City", 2, 1.50, "PROTOCOL",
                    "The simulation activates protocols against you.",
                    "Break the city's control network",
                    "Cyberware Boon (utility)",
                    "last-gate"),
                new VoidRouteNode(
                    "last-gate", "Last Gate", 3, 1.85, "FINAL TEST",
                    "Prove the build is ready.",
                    "Survive the gauntlet, kill the Gatekeeper",
                    "Final Boon",
                    "final-void"),
                new VoidRouteNode(
                    "final-void", "Final Void", 4, 2.20, "REALITY COLLAPSE",
                    "Break The Overseer, kill what was hidden inside, pay the price of escape.",
                    "Kill The Overseer, kill the True Boss",
                    "Escape"),
            };
        }
    }
}
