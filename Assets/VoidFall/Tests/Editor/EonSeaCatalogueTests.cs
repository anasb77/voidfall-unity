using NUnit.Framework;
using UnityEditor;
using VoidFall.Core;
using VoidFall.Runtime;

namespace VoidFall.Tests.Editor
{
    public sealed class EonSeaCatalogueTests
    {
        [Test]
        public void Appended_arena_round_trips_and_uses_a_shared_boss_objective()
        {
            Assert.That((int)ArenaId.EonSea, Is.EqualTo(6));
            Assert.That(ArenaCatalogRules.StableId(ArenaId.EonSea), Is.EqualTo("eon-sea"));
            Assert.That(ArenaCatalogRules.LegacyArena("eon-sea"), Is.EqualTo(ArenaId.EonSea));
            Assert.That(VoidObjectives.ForArena("eon-sea"), Is.TypeOf<MultiPhaseObjective>());
            Assert.That(PlayableVoidRoutes.Create(42).Node("eon-sea").ObjectiveSummary, Does.Contain("random boss"));
        }

        [Test]
        public void Prepared_glacier_package_contains_every_authored_terrain_form()
        {
            var plate = AssetDatabase.LoadAssetAtPath<ArenaPlateAsset>("Assets/VoidFall/Generated/ArenaPackages/EonSea/Plate.asset");
            Assert.That(plate, Is.Not.Null);
            Assert.That(plate.IsValidFor(ArenaId.EonSea), Is.True);
            Assert.That(plate.EonSeaVisuals.IsValid, Is.True);
            for (var kind = 0; kind < 4; kind++) for (var variant = 0; variant < 8; variant++) Assert.That(plate.EonSeaVisuals.Ice(kind, variant), Is.Not.Null);
            for (var variant = 0; variant < 4; variant++) Assert.That(plate.EonSeaVisuals.Ground(variant), Is.Not.Null);
        }

        [Test]
        public void Imported_roster_contains_all_sixty_eight_forms_and_no_extra_dasher_variant()
        {
            var visuals = AssetDatabase.LoadAssetAtPath<RosterProgressionVisualAsset>("Assets/VoidFall/Resources/VoidFall/RosterProgressionVisuals.asset");
            Assert.That(visuals.Count, Is.EqualTo(68));
            foreach (var enemy in ContentCatalog.Enemies) if (EnemyRosterRules.RosterTwoEligible(enemy.Id)) for (var tier = 1; tier <= 4; tier++) Assert.That(visuals.Find(enemy.Id, tier, false), Is.Not.Null, enemy.Id + tier);
            foreach (var kind in EliteRules.EliteVariantOrder) for (var tier = 1; tier <= 4; tier++) Assert.That(visuals.Find(EliteRules.EliteVariantDef(kind).BaseId, tier, true), Is.Not.Null);
        }
    }
}
