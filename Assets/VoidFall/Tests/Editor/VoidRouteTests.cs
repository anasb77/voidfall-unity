using System.Collections.Generic;
using NUnit.Framework;
using VoidFall.Core;

namespace VoidFall.Tests.Editor
{
    public sealed class VoidRouteTests
    {
        [Test]
        public void Legacy_node_constructor_uses_the_node_id_as_its_arena_identity()
        {
            var node = new VoidRouteNode("hydra", "Hydra", 1, 1, "MUTATION", "desc", "objective", "reward");

            var property = typeof(VoidRouteNode).GetProperty("ArenaId");
            Assert.That(property, Is.Not.Null, "VoidRouteNode must expose stable arena identity");
            Assert.That(property.GetValue(node), Is.EqualTo("hydra"));
        }

        [Test]
        public void Run_exposes_current_node_and_arena_identity_independently()
        {
            var constructor = typeof(VoidRouteNode).GetConstructor(new[]
            {
                typeof(string), typeof(string), typeof(string), typeof(int), typeof(double),
                typeof(string), typeof(string), typeof(string), typeof(string), typeof(string[])
            });
            Assert.That(constructor, Is.Not.Null, "Generated nodes need separate node and arena ids");
            var start = (VoidRouteNode)constructor.Invoke(new object[]
            {
                "start-node", "abyss", "Abyss", 0, 1d, "BASELINE", "desc", "objective", "reward",
                new[] { "branch-node" }
            });
            var branch = (VoidRouteNode)constructor.Invoke(new object[]
            {
                "branch-node", "hydra", "Hydra", 1, 1d, "MUTATION", "desc", "objective", "reward",
                new string[0]
            });
            var run = new VoidRouteRun(new[] { start, branch }, start.Id);

            Assert.That(run.CurrentVoidId, Is.EqualTo("start-node"));
            Assert.That(CurrentArenaId(run), Is.EqualTo("abyss"));
            Assert.That(run.NotifyVoidCompleted(start.Id), Is.True);
            Assert.That(run.SelectNextVoid(branch.Id), Is.True);
            Assert.That(run.CurrentVoidId, Is.EqualTo("branch-node"));
            Assert.That(CurrentArenaId(run), Is.EqualTo("hydra"));
        }

        [Test]
        public void Planning_uses_the_first_unlocked_route_through_a_target_without_mutating_the_run()
        {
            var run = new VoidRouteRun(new[]
            {
                Node("start", 0, "left", "right"),
                Node("left", 1, "target"),
                Node("right", 1, "target"),
                Node("target", 2, "terminal-b", "terminal-a"),
                Node("terminal-a", 3),
                Node("terminal-b", 3),
            }, "start");
            var method = typeof(VoidRouteRun).GetMethod("PlannedPathThrough", new[] { typeof(string) });
            Assert.That(method, Is.Not.Null, "VoidRouteRun must share deterministic planned-path rules");
            var before = RouteState(run);

            Assert.That((IReadOnlyList<string>)method.Invoke(run, new object[] { "target" }),
                Is.EqualTo(new[] { "start", "left", "target", "terminal-b" }));
            Assert.That(RouteState(run), Is.EqualTo(before), "Planning must not change route state");

            run.NotifyVoidCompleted("start");
            run.SelectNextVoid("right");
            Assert.That((IReadOnlyList<string>)method.Invoke(run, new object[] { "right" }), Is.Empty);
            Assert.That((IReadOnlyList<string>)method.Invoke(run, new object[] { "start" }), Is.Empty);
            Assert.That((IReadOnlyList<string>)method.Invoke(run, new object[] { "left" }), Is.Empty);
            Assert.That((IReadOnlyList<string>)method.Invoke(run, new object[] { "target" }),
                Is.EqualTo(new[] { "right", "target", "terminal-b" }));
        }

        private static VoidRouteNode Node(string id, int depth, params string[] outgoing)
        {
            return new VoidRouteNode(id, id, depth, 1, "THREAT", "desc", "objective", "reward", outgoing);
        }

        private static string RouteState(VoidRouteRun run)
        {
            var result = run.CurrentVoidId + ":" + string.Join(",", run.History) + ":";
            foreach (var node in run.Nodes) result += node.Id + "=" + run.StateOf(node.Id) + ";";
            return result;
        }

        private static string CurrentArenaId(VoidRouteRun run)
        {
            var property = typeof(VoidRouteRun).GetProperty("CurrentArenaId");
            Assert.That(property, Is.Not.Null, "VoidRouteRun must expose the selected arena identity");
            return (string)property.GetValue(run);
        }

        [Test]
        public void Initial_view_shows_start_selected_and_layer_one_revealed()
        {
            var run = VoidRouteRun.PrototypeGraph();

            Assert.That(run.CurrentVoidId, Is.EqualTo("abyss"));
            Assert.That(run.StateOf("abyss"), Is.EqualTo(RouteNodeState.Selected));
            foreach (var child in new[] { "red-nebula", "white-sakura", "hydra" })
                Assert.That(run.StateOf(child), Is.EqualTo(RouteNodeState.Revealed),
                    child + " should be visible as a preview");

            // Layer II is hidden until a Layer I void completes (spec §7).
            foreach (var hidden in new[] { "dead-orbit", "graveyard", "monochrome-court", "null-city", "last-gate", "final-void" })
                Assert.That(run.StateOf(hidden), Is.EqualTo(RouteNodeState.Hidden));
        }

        [Test]
        public void Completing_a_void_reveals_and_unlocks_only_its_children()
        {
            var run = VoidRouteRun.PrototypeGraph();

            Assert.That(run.NotifyVoidCompleted("abyss"), Is.True);
            Assert.That(run.StateOf("abyss"), Is.EqualTo(RouteNodeState.Completed));
            foreach (var child in new[] { "red-nebula", "white-sakura", "hydra" })
                Assert.That(run.StateOf(child), Is.EqualTo(RouteNodeState.Available));
            Assert.That(run.StateOf("null-city"), Is.EqualTo(RouteNodeState.Hidden));

            // Only the current void can be completed.
            Assert.That(run.NotifyVoidCompleted("abyss"), Is.False);
        }

        [Test]
        public void Selecting_a_node_locks_its_siblings()
        {
            var run = VoidRouteRun.PrototypeGraph();
            run.NotifyVoidCompleted("abyss");

            Assert.That(run.SelectNextVoid("hydra"), Is.True);
            Assert.That(run.StateOf("hydra"), Is.EqualTo(RouteNodeState.Selected));
            Assert.That(run.StateOf("red-nebula"), Is.EqualTo(RouteNodeState.Locked));
            Assert.That(run.StateOf("white-sakura"), Is.EqualTo(RouteNodeState.Locked));

            // Locked and hidden nodes are not selectable, no backtracking.
            Assert.That(run.SelectNextVoid("red-nebula"), Is.False);
            Assert.That(run.SelectNextVoid("abyss"), Is.False);
            Assert.That(run.SelectNextVoid("null-city"), Is.False);
        }

        [Test]
        public void Hydra_completion_makes_its_shared_children_available()
        {
            var run = VoidRouteRun.PrototypeGraph();
            run.NotifyVoidCompleted("abyss");
            run.SelectNextVoid("hydra");
            run.NotifyVoidCompleted("hydra");

            // null-city is reachable from red-nebula and hydra; the hydra
            // route must still offer both of hydra's children.
            Assert.That(run.StateOf("null-city"), Is.EqualTo(RouteNodeState.Available));
            Assert.That(run.StateOf("monochrome-court"), Is.EqualTo(RouteNodeState.Available));
            // The Layer I siblings stay locked, and nothing else unlocks.
            Assert.That(run.StateOf("red-nebula"), Is.EqualTo(RouteNodeState.Locked));
            Assert.That(run.StateOf("dead-orbit"), Is.EqualTo(RouteNodeState.Hidden));
        }

        [Test]
        public void Full_route_to_escape_and_threat_escalates_with_depth()
        {
            var run = VoidRouteRun.PrototypeGraph();
            var runState = VoidRunState.Begin(0x5f1dc0deu, "abyss", 0, 100);
            Assert.That(run.ThreatOf(runState.CurrentVoidId), Is.EqualTo(1.00));

            void CompleteCurrent()
            {
                run.NotifyVoidCompleted(run.CurrentVoidId);
                runState.CompleteVoid();
            }
            void Choose(string next)
            {
                Assert.That(run.SelectNextVoid(next), Is.True, "select " + next);
                runState.EnterVoid(next, run.Node(next).Depth);
            }

            CompleteCurrent();
            Choose("white-sakura");
            Assert.That(run.ThreatOf("white-sakura"), Is.EqualTo(1.20));
            CompleteCurrent();
            Choose("graveyard");
            Assert.That(run.ThreatOf("graveyard"), Is.EqualTo(1.50));
            CompleteCurrent();
            Choose("last-gate");
            Assert.That(run.ThreatOf("last-gate"), Is.EqualTo(1.85));
            CompleteCurrent();
            Choose("final-void");
            Assert.That(run.ThreatOf("final-void"), Is.EqualTo(2.20));
            Assert.That(run.IsFinalVoid("final-void"), Is.True);

            Assert.That(run.HasEscaped, Is.False);
            CompleteCurrent();
            Assert.That(run.HasEscaped, Is.True);
            Assert.That(runState.CompletedVoids, Is.EqualTo(new List<string>
                { "abyss", "white-sakura", "graveyard", "last-gate", "final-void" }));
            Assert.That(runState.RouteHistory, Is.EqualTo(new List<string>
                { "abyss", "white-sakura", "graveyard", "last-gate", "final-void" }));
        }

        [Test]
        public void Prototype_graph_is_well_formed()
        {
            var nodes = VoidRouteRun.PrototypeNodes();
            var ids = new HashSet<string>();
            foreach (var node in nodes)
            {
                Assert.That(ids.Add(node.Id), Is.True, "duplicate id " + node.Id);
                Assert.That(node.DisplayName, Is.Not.Empty);
                Assert.That(node.ObjectiveSummary, Is.Not.Empty, node.Id + " needs route-card copy");
                Assert.That(node.RewardSummary, Is.Not.Empty, node.Id + " needs reward copy");
            }
            foreach (var node in nodes)
                foreach (var target in node.Outgoing)
                {
                    Assert.That(ids.Contains(target), Is.True,
                        node.Id + " points at unknown " + target);
                    var targetNode = nodes.Find(n => n.Id == target);
                    Assert.That(targetNode.Depth, Is.EqualTo(node.Depth + 1),
                        target + " must sit exactly one layer below " + node.Id);
                }

            // Every Layer II void reaches the Last Gate; the graph matches §3.
            foreach (var layerTwo in new[] { "dead-orbit", "graveyard", "monochrome-court", "null-city" })
            {
                var node = nodes.Find(n => n.Id == layerTwo);
                Assert.That(node.Outgoing, Is.EqualTo(new List<string> { "last-gate" }));
            }
            Assert.That(nodes.Find(n => n.Id == "final-void").Outgoing.Count, Is.EqualTo(0));
        }

        [Test]
        public void Run_state_build_snapshot_round_trips_ranks()
        {
            var state = VoidRunState.Begin(42u, "abyss", 0, 100);

            state.SetWeaponRank("pistol", 3);
            state.SetWeaponRank("arc-lash", 2);
            state.SetSupportRank("amplifier", 1);
            state.SetWeaponRank("pistol", 4); // re-rank, not duplicate
            state.Evolutions.Add("pulse-repeater");

            Assert.That(state.Weapons.Count, Is.EqualTo(2));
            Assert.That(state.WeaponRank("pistol"), Is.EqualTo(4));
            Assert.That(state.WeaponRank("arc-lash"), Is.EqualTo(2));
            Assert.That(state.WeaponRank("seeker"), Is.EqualTo(0));
            Assert.That(state.SupportRank("amplifier"), Is.EqualTo(1));
            Assert.That(state.HasEvolution("pulse-repeater"), Is.True);

            state.EnterVoid("hydra", 1);
            Assert.That(state.RouteHistory, Is.EqualTo(new List<string> { "abyss", "hydra" }));
            Assert.That(state.LocalVoidTime, Is.EqualTo(0));
            Assert.That(state.Depth, Is.EqualTo(1));
        }

        [Test]
        public void Unknown_void_ids_are_rejected()
        {
            var run = VoidRouteRun.PrototypeGraph();
            Assert.That(() => run.SelectNextVoid("atlantis"), Throws.ArgumentException);
            Assert.That(() => run.NotifyVoidCompleted("atlantis"), Throws.ArgumentException);
            Assert.That(run.StateOf("atlantis"), Is.EqualTo(RouteNodeState.Hidden));
        }
    }
}
