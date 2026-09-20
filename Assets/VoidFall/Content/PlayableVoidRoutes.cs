using System;
using System.Collections.Generic;

namespace VoidFall.Core
{
    /// <summary>Finite routes built only from prepared arenas with supported objectives and metadata.</summary>
    public static class PlayableVoidRoutes
    {
        private const int ArenasAfterStart = 7;

        public static VoidRouteRun Create(uint seed)
        {
            VoidRouteNode start = null;
            var candidates = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var arena in ContentOrder.PreparedArenas)
            {
                var id = ArenaCatalogRules.StableId(arena);
                if (!seen.Add(id) || VoidObjectives.ForArena(id) == null) continue;
                var node = CreateNode(id, id);
                if (node == null) continue;
                if (id == "abyss") start = node;
                else candidates.Add(id);
            }
            if (start == null) throw new InvalidOperationException("A playable route requires prepared Abyss content.");
            if (candidates.Count < ArenasAfterStart)
                throw new InvalidOperationException("A playable route requires seven prepared arenas after Abyss.");

            // Own the random stream: generating or inspecting a map never advances combat RNG.
            var random = new Rng(seed ^ 0x524f5554u);
            for (var index = candidates.Count - 1; index > 0; index--)
            {
                var swap = random.Int(index + 1);
                var candidate = candidates[index];
                candidates[index] = candidates[swap];
                candidates[swap] = candidate;
            }

            var widths = random.Int(2) == 0
                ? new[] { 1, 2, 2, 1, 2, 1 }
                : new[] { 1, 2, 1, 2, 2, 1 };
            var exclusiveDepth = widths[2] == 2 ? 1 : 3;
            var swapExclusiveLanes = random.Int(2) == 1;
            var duplicateParentLane = random.Int(2);
            var duplicateChildLane = ConnectedLane(1 - duplicateParentLane, swapExclusiveLanes);
            var duplicateArenaIndex = random.Int(ArenasAfterStart);
            var duplicateArenaId = candidates[duplicateArenaIndex];
            var remainingArenaIds = new List<string>();
            for (var index = 0; index < ArenasAfterStart; index++)
                if (index != duplicateArenaIndex) remainingArenaIds.Add(candidates[index]);

            var rows = new List<List<VoidRouteNode>>();
            rows.Add(new List<VoidRouteNode> { start });
            var nodes = new List<VoidRouteNode> { start };
            var usedNodeIds = new HashSet<string>(StringComparer.Ordinal) { start.Id };
            var remainingCursor = 0;
            for (var depth = 1; depth < widths.Length; depth++)
            {
                var row = new List<VoidRouteNode>();
                for (var lane = 0; lane < widths[depth]; lane++)
                {
                    var duplicateSlot = depth == exclusiveDepth && lane == duplicateParentLane ||
                        depth == exclusiveDepth + 1 && lane == duplicateChildLane;
                    var arenaId = duplicateSlot ? duplicateArenaId : remainingArenaIds[remainingCursor++];
                    var nodeId = arenaId;
                    if (!usedNodeIds.Add(nodeId))
                    {
                        nodeId = arenaId + "@" + depth + "-" + lane;
                        usedNodeIds.Add(nodeId);
                    }
                    var node = CreateNode(nodeId, arenaId);
                    node.Depth = depth;
                    nodes.Add(node);
                    row.Add(node);
                }
                rows.Add(row);
            }

            for (var depth = 0; depth < rows.Count - 1; depth++)
            {
                var parents = rows[depth];
                var children = rows[depth + 1];
                if (parents.Count == 2 && children.Count == 2)
                {
                    for (var lane = 0; lane < parents.Count; lane++)
                        parents[lane].Outgoing.Add(children[ConnectedLane(lane, swapExclusiveLanes)].Id);
                    continue;
                }
                foreach (var parent in parents)
                    foreach (var child in children)
                        parent.Outgoing.Add(child.Id);
            }

            // Conceal at most one intermediate destination, resolved once from this run's seed.
            if (nodes.Count > 2 && random.Int(3) == 0)
                nodes[1 + random.Int(nodes.Count - 2)].IsMystery = true;

            return new VoidRouteRun(nodes, start.Id);
        }

        private static int ConnectedLane(int lane, bool swapped) => swapped ? 1 - lane : lane;

        private static VoidRouteNode CreateNode(string nodeId, string arenaId)
        {
            ArenaDefinition arena;
            string hint;
            string encounter;
            switch (arenaId)
            {
                case "abyss":
                    arena = FindCatalogueArena("void");
                    hint = "OPEN GROUND";
                    encounter = "clear a random boss encounter";
                    break;
                case "red-nebula":
                    arena = FindCatalogueArena("redNebula");
                    hint = "METEOR STORMS";
                    encounter = "clear a random boss encounter";
                    break;
                case "white-sakura":
                    arena = FindCatalogueArena("whiteSakura");
                    hint = "ELITE SURGE";
                    encounter = "clear a random boss encounter";
                    break;
                case "hydra":
                    arena = HydraContent.Arena;
                    hint = "MUTATED ENEMIES";
                    encounter = "defeat " + HydraContent.Boss.Name;
                    break;
                case "monochrome-court":
                    arena = MonochromeContent.Arena;
                    hint = "CHESS ARMIES / BURNING TILES";
                    encounter = "defeat Wingwang";
                    break;
                case "crascendo":
                    arena = CrascendoContent.Arena;
                    hint = "ENEMIES GROW WHEN HIT";
                    encounter = "clear a random boss encounter";
                    break;
                case "eon-sea":
                    arena = EonSeaContent.Arena;
                    hint = "MELTING GLACIERS / SLIPPERY ICE";
                    encounter = "clear a random boss encounter";
                    break;
                case "null-city":
                    arena = NullCityContent.Arena;
                    hint = "PURGE LANES / LAW ENFORCEMENT";
                    encounter = "defeat Motherload";
                    break;
                default:
                    return null;
            }
            if (arena == null) return null;
            return new VoidRouteNode(nodeId, arenaId, arena.Name, 0, 1, hint, arena.Description,
                "Survive " + VoidObjectives.FormatClock(VoidProgressionRules.SurvivalSeconds) + ", then " + encounter,
                "Boss rewards");
        }

        private static ArenaDefinition FindCatalogueArena(string id)
        {
            foreach (var arena in ContentCatalog.Arenas)
                if (arena.Id == id) return arena;
            return null;
        }
    }
}
