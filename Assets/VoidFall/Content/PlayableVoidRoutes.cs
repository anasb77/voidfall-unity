using System;
using System.Collections.Generic;

namespace VoidFall.Core
{
    /// <summary>Finite routes built only from prepared arenas with supported objectives and metadata.</summary>
    public static class PlayableVoidRoutes
    {
        private const int MaximumRowsAfterStart = 5;
        private const int MaximumArenasAfterStart = MaximumRowsAfterStart * 2 - 1;

        public static VoidRouteRun Create(uint seed)
        {
            VoidRouteNode start = null;
            var candidates = new List<VoidRouteNode>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var arena in ContentOrder.PreparedArenas)
            {
                var id = ArenaCatalogRules.StableId(arena);
                if (!seen.Add(id) || VoidObjectives.ForArena(id) == null) continue;
                var node = CreateNode(id);
                if (node == null) continue;
                if (id == "abyss") start = node;
                else candidates.Add(node);
            }
            if (start == null) throw new InvalidOperationException("A playable route requires prepared Abyss content.");

            // Own the random stream: generating or inspecting a map never advances combat RNG.
            var random = new Rng(seed ^ 0x524f5554u);
            for (var index = candidates.Count - 1; index > 0; index--)
            {
                var swap = random.Int(index + 1);
                var candidate = candidates[index];
                candidates[index] = candidates[swap];
                candidates[swap] = candidate;
            }

            var count = Math.Min(candidates.Count, MaximumArenasAfterStart);
            // Keep a singleton terminal, and offer a branch once three later arenas exist.
            // The current four later arenas form widths 2/1/1; larger pools fill up to five rows.
            var rowCount = Math.Min(MaximumRowsAfterStart, count >= 3 ? count - 1 : count);
            var doubledRows = count - rowCount;
            var nodes = new List<VoidRouteNode> { start };
            var previousRow = new List<VoidRouteNode> { start };
            var cursor = 0;
            for (var depth = 1; depth <= rowCount; depth++)
            {
                var width = doubledRows > 0 ? 2 : 1;
                if (width == 2) doubledRows--;
                var row = new List<VoidRouteNode>();
                for (var index = 0; index < width; index++)
                {
                    var node = candidates[cursor++];
                    node.Depth = depth;
                    nodes.Add(node);
                    row.Add(node);
                }
                foreach (var parent in previousRow)
                    foreach (var child in row)
                        parent.Outgoing.Add(child.Id);
                previousRow = row;
            }

            // Conceal at most one intermediate destination, resolved once from this run's seed.
            if (nodes.Count > 2 && random.Int(3) == 0)
                nodes[1 + random.Int(nodes.Count - 2)].IsMystery = true;

            return new VoidRouteRun(nodes, start.Id);
        }

        private static VoidRouteNode CreateNode(string id)
        {
            ArenaDefinition arena;
            string hint;
            string encounter;
            switch (id)
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
                    encounter = "defeat the Twin Grandmasters";
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
            return new VoidRouteNode(id, arena.Name, 0, 1, hint, arena.Description,
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
