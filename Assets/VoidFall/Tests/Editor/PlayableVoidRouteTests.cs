using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using VoidFall.Core;

namespace VoidFall.Tests.Editor
{
    public sealed class PlayableVoidRouteTests
    {
        [TestCase(0u)]
        [TestCase(42u)]
        [TestCase(2848592627u)]
        [TestCase(uint.MaxValue)]
        public void Seed_recreates_the_same_route_and_mysteries(uint seed)
        {
            Assert.That(Snapshot(PlayableVoidRoutes.Create(seed)),
                Is.EqualTo(Snapshot(PlayableVoidRoutes.Create(seed))));
        }

        [Test]
        public void Different_seeds_change_the_arena_order()
        {
            var orders = new HashSet<string>();
            for (uint seed = 1; seed <= 64; seed++)
            {
                var order = new StringBuilder();
                foreach (var node in PlayableVoidRoutes.Create(seed).Nodes) order.Append(node.Id).Append('|');
                orders.Add(order.ToString());
            }
            Assert.That(orders.Count, Is.GreaterThan(8));
        }

        [Test]
        public void Current_catalogue_gives_one_branch_and_five_arenas_per_path()
        {
            var run = PlayableVoidRoutes.Create(42u);
            var widths = new int[5];
            Assert.That(run.StartId, Is.EqualTo("abyss"));
            Assert.That(run.CurrentVoidId, Is.EqualTo("abyss"));
            Assert.That(run.Nodes.Count, Is.EqualTo(6));
            foreach (var node in run.Nodes)
            {
                Assert.That(node.Depth, Is.InRange(0, 4));
                widths[node.Depth]++;
            }
            Assert.That(widths, Is.EqualTo(new[] { 1, 2, 1, 1, 1 }));
        }

        [Test]
        public void Generated_routes_only_offer_unique_prepared_arenas_with_objectives()
        {
            var prepared = new HashSet<string>();
            foreach (var arena in ContentOrder.PreparedArenas) prepared.Add(ArenaCatalogRules.StableId(arena));
            for (uint seed = 0; seed < 128; seed++)
            {
                var run = PlayableVoidRoutes.Create(seed);
                var ids = new HashSet<string>();
                foreach (var node in run.Nodes)
                {
                    Assert.That(ids.Add(node.Id), Is.True, "Repeated arena: " + node.Id);
                    Assert.That(prepared, Does.Contain(node.Id));
                    Assert.That(VoidObjectives.ForArena(node.Id), Is.Not.Null, node.Id);
                    Assert.That(node.Depth, Is.InRange(0, 5));
                    Assert.That(node.Outgoing.Count, Is.LessThanOrEqualTo(2));
                    Assert.That(run.ThreatOf(node.Id), Is.EqualTo(1), "Route does not introduce combat modifiers");
                }
            }
        }

        [Test]
        public void Every_branch_is_reachable_and_escapes_without_repeating_an_arena()
        {
            for (uint seed = 0; seed < 128; seed++)
            {
                var run = PlayableVoidRoutes.Create(seed);
                var reachable = new HashSet<string>();
                var paths = new List<List<string>>();
                FindPaths(run, run.StartId, new List<string>(), reachable, paths);
                Assert.That(reachable.Count, Is.EqualTo(run.Nodes.Count));
                Assert.That(paths.Count, Is.GreaterThan(1));
                foreach (var path in paths)
                {
                    var journey = PlayableVoidRoutes.Create(seed);
                    Assert.That(path.Count, Is.EqualTo(5));
                    Assert.That(new HashSet<string>(path).Count, Is.EqualTo(path.Count));
                    for (var index = 0; index < path.Count; index++)
                    {
                        Assert.That(journey.CurrentVoidId, Is.EqualTo(path[index]));
                        Assert.That(journey.HasEscaped, Is.False);
                        Assert.That(journey.NotifyVoidCompleted(path[index]), Is.True);
                        if (index + 1 < path.Count)
                            Assert.That(journey.SelectNextVoid(path[index + 1]), Is.True);
                    }
                    Assert.That(journey.HasEscaped, Is.True);
                    Assert.That(journey.History, Is.EqualTo(path));
                }
            }
        }

        [Test]
        public void Mystery_is_occasional_nonterminal_and_inspection_does_not_reveal_or_reroll_it()
        {
            var runsWithMystery = 0;
            for (uint seed = 0; seed < 128; seed++)
            {
                var run = PlayableVoidRoutes.Create(seed);
                var before = Snapshot(run);
                var count = 0;
                foreach (var node in run.Nodes)
                {
                    if (!node.IsMystery) continue;
                    count++;
                    Assert.That(node.Id, Is.Not.EqualTo(run.StartId));
                    Assert.That(run.IsFinalVoid(node.Id), Is.False);
                    Assert.That(node.ThreatLabel, Is.Not.Empty, "A concealed name still has a mechanic hint");
                    Assert.That(run.StateOf(node.Id), Is.Not.EqualTo(RouteNodeState.Selected));
                    Assert.That(run.Node(node.Id).IsMystery, Is.True);
                }
                Assert.That(count, Is.LessThanOrEqualTo(1));
                if (count > 0) runsWithMystery++;
                Assert.That(Snapshot(run), Is.EqualTo(before));
                Assert.That(run.CurrentVoidId, Is.EqualTo("abyss"));
                Assert.That(run.History, Is.EqualTo(new[] { "abyss" }));
            }
            Assert.That(runsWithMystery, Is.GreaterThan(0));
            Assert.That(runsWithMystery, Is.LessThan(128));
        }

        [Test]
        public void Route_copy_uses_current_catalogue_instead_of_prototype_promises()
        {
            var run = PlayableVoidRoutes.Create(42u);
            foreach (var node in run.Nodes)
            {
                var arena = CatalogueArena(node.Id);
                Assert.That(arena, Is.Not.Null, node.Id);
                Assert.That(node.DisplayName, Is.EqualTo(arena.Name));
                Assert.That(node.Description, Is.EqualTo(arena.Description));
                Assert.That(node.ObjectiveSummary, Does.Contain("Survive"));
                Assert.That(node.ObjectiveSummary, Does.Not.Contain("Anchors").And.Not.Contain("Zones").And.Not.Contain("Gene Nodes"));
                Assert.That(node.RewardSummary, Does.Not.Contain("Boon"));
            }
            Assert.That(run.Node("hydra").ObjectiveSummary, Does.Contain("Hydra Prime"));
            Assert.That(run.Node("monochrome-court").ObjectiveSummary, Does.Contain("Grandmasters"));
            foreach (var id in new[] { "abyss", "red-nebula", "white-sakura" })
                Assert.That(run.Node(id).ObjectiveSummary, Does.Contain("random").And.Contain("boss"));
        }

        [Test]
        public void Runs_own_their_nodes_so_route_changes_do_not_leak_to_another_run()
        {
            var first = PlayableVoidRoutes.Create(42u);
            var second = PlayableVoidRoutes.Create(42u);
            var before = Snapshot(second);
            first.Node("abyss").Outgoing.Clear();
            first.Node("abyss").IsMystery = true;
            Assert.That(Snapshot(second), Is.EqualTo(before));
        }

        [Test]
        public void Route_generation_and_inspection_leave_an_existing_combat_stream_unchanged()
        {
            var combat = new Rng(2848592627u);
            var reference = new Rng(2848592627u);
            for (uint seed = 0; seed < 64; seed++)
            {
                Snapshot(PlayableVoidRoutes.Create(seed));
                Assert.That(combat.Draws, Is.EqualTo((int)seed));
                Assert.That(combat.Next(), Is.EqualTo(reference.Next()));
            }
        }

        private static void FindPaths(VoidRouteRun run, string id, List<string> prefix,
            HashSet<string> reachable, List<List<string>> paths)
        {
            Assert.That(prefix, Does.Not.Contain(id), "Cycle at " + id);
            var path = new List<string>(prefix) { id };
            reachable.Add(id);
            var node = run.Node(id);
            if (node.Outgoing.Count == 0)
            {
                paths.Add(path);
                return;
            }
            foreach (var child in node.Outgoing)
            {
                Assert.That(run.Node(child).Depth, Is.EqualTo(node.Depth + 1));
                FindPaths(run, child, path, reachable, paths);
            }
        }

        private static ArenaDefinition CatalogueArena(string id)
        {
            if (id == "hydra") return HydraContent.Arena;
            if (id == "monochrome-court") return MonochromeContent.Arena;
            if (id == "null-city") return NullCityContent.Arena;
            var catalogueId = id == "abyss" ? "void" : id == "red-nebula" ? "redNebula" : "whiteSakura";
            foreach (var arena in ContentCatalog.Arenas)
                if (arena.Id == catalogueId) return arena;
            return null;
        }

        private static string Snapshot(VoidRouteRun run)
        {
            var result = new StringBuilder();
            foreach (var node in run.Nodes)
            {
                result.Append(node.Id).Append(':').Append(node.Depth).Append(':').Append(node.IsMystery)
                    .Append(':').Append(node.DisplayName).Append(':').Append(node.ThreatMultiplier)
                    .Append(':').Append(node.ThreatLabel).Append(':').Append(node.Description)
                    .Append(':').Append(node.ObjectiveSummary).Append(':').Append(node.RewardSummary)
                    .Append(':').Append(run.StateOf(node.Id)).Append(':');
                foreach (var child in node.Outgoing) result.Append(child).Append(',');
                result.Append('|');
            }
            return result.ToString();
        }
    }
}
