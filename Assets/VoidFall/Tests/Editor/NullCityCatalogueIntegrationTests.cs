using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using VoidFall.Core;

namespace VoidFall.Tests.Editor
{
    public sealed class NullCityCatalogueIntegrationTests
    {
        [Test]
        public void Null_city_appends_to_arena_ids_without_renumbering_existing_values()
        {
            Assert.That((int)ArenaId.Void, Is.EqualTo(0));
            Assert.That((int)ArenaId.RedNebula, Is.EqualTo(1));
            Assert.That((int)ArenaId.WhiteSakura, Is.EqualTo(2));
            Assert.That((int)ArenaId.Hydra, Is.EqualTo(3));
            Assert.That((int)ArenaId.MonochromeCourt, Is.EqualTo(4));
            Assert.That((int)ArenaId.NullCity, Is.EqualTo(5));
        }

        [Test]
        public void Null_city_stable_id_round_trips_through_the_legacy_bridge()
        {
            Assert.That(ArenaCatalogRules.StableId(ArenaId.NullCity),
                Is.EqualTo(NullCityContent.StableId));
            Assert.That(ArenaCatalogRules.LegacyArena(NullCityContent.StableId),
                Is.EqualTo(ArenaId.NullCity));
        }

        [Test]
        public void Null_city_is_prepared_without_entering_the_legacy_endless_rotation()
        {
            Assert.That(ContentOrder.Arenas, Is.EqualTo(new[]
            {
                ArenaId.Void,
                ArenaId.RedNebula,
                ArenaId.WhiteSakura,
                ArenaId.Hydra,
            }));
            Assert.That(ContentOrder.PreparedArenas, Is.EqualTo(new[]
            {
                ArenaId.Void,
                ArenaId.RedNebula,
                ArenaId.WhiteSakura,
                ArenaId.Hydra,
                ArenaId.MonochromeCourt,
                ArenaId.NullCity,
            }));
        }

        [Test]
        public void Menu_residency_keeps_one_package_for_all_six_prepared_arenas()
        {
            var packages = ContentOrder.PreparedArenas
                .Select(arena => new ArenaPackageKey(ArenaCatalogRules.StableId(arena), 0))
                .ToArray();

            var resident = ArenaResidencyPlanner.MenuCatalogue(packages);

            Assert.That(resident.Count, Is.EqualTo(6));
            Assert.That(resident.Items.Select(item => item.StableArenaId), Is.EqualTo(new[]
            {
                "abyss",
                "red-nebula",
                "white-sakura",
                "hydra",
                "monochrome-court",
                "null-city",
            }));
        }

        [Test]
        public void Null_city_has_a_survival_then_motherload_objective()
        {
            var objective = VoidObjectives.ForArena(NullCityContent.StableId);

            Assert.That(objective, Is.Not.Null);
            Assert.That(objective, Is.TypeOf<MultiPhaseObjective>());
            var phases = (MultiPhaseObjective)objective;
            Assert.That(phases.PhaseCount, Is.EqualTo(2));
            phases.BeginObjective();
            Assert.That(phases.GetObjectiveText(), Does.Contain("Survive"));
        }

        [TestCase(0u)]
        [TestCase(1u)]
        [TestCase(42u)]
        [TestCase(2848592627u)]
        [TestCase(uint.MaxValue)]
        public void Generated_routes_make_null_city_reachable_and_only_use_valid_objectives(uint seed)
        {
            var route = PlayableVoidRoutes.Create(seed);
            var reachable = ReachableFromStart(route);

            Assert.That(reachable, Does.Contain(NullCityContent.StableId));
            Assert.That(reachable.Count, Is.EqualTo(route.Nodes.Count));
            foreach (var node in route.Nodes)
                Assert.That(VoidObjectives.ForArena(node.Id), Is.Not.Null, node.Id);
        }

        [Test]
        public void Motherload_stays_out_of_the_shared_boss_rotation()
        {
            Assert.That(ContentCatalog.Bosses.Select(boss => boss.Id),
                Has.None.EqualTo(NullCityContent.MotherloadId));
            Assert.That(ContentCatalog.Bosses, Has.None.SameAs(NullCityContent.Motherload));
        }

        private static HashSet<string> ReachableFromStart(VoidRouteRun route)
        {
            var reachable = new HashSet<string>();
            var pending = new Queue<string>();
            pending.Enqueue(route.StartId);
            while (pending.Count > 0)
            {
                var id = pending.Dequeue();
                if (!reachable.Add(id)) continue;
                foreach (var child in route.Node(id).Outgoing) pending.Enqueue(child);
            }

            return reachable;
        }
    }
}
