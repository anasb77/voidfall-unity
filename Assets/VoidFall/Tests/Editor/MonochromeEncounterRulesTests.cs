using System.Linq;
using NUnit.Framework;
using VoidFall.Core;

namespace VoidFall.Tests.Editor
{
    public sealed class MonochromeEncounterRulesTests
    {
        [Test]
        public void Monochrome_stable_id_appends_after_hydra_without_reordering_legacy_arenas()
        {
            Assert.That(ContentOrder.Arenas.Take(4), Is.EqualTo(new[]
            {
                ArenaId.Void,
                ArenaId.RedNebula,
                ArenaId.WhiteSakura,
                ArenaId.Hydra,
            }));
            Assert.That(ContentOrder.Arenas.Length, Is.EqualTo(4));
            Assert.That(ContentOrder.PreparedArenas[4], Is.EqualTo(ArenaId.MonochromeCourt));
            Assert.That(ArenaCatalogRules.StableId(ArenaId.MonochromeCourt), Is.EqualTo("monochrome-court"));
            Assert.That(ArenaCatalogRules.LegacyArena("monochrome-court"), Is.EqualTo(ArenaId.MonochromeCourt));
            Assert.That(ContentOrder.Arenas, Has.None.EqualTo(ArenaId.MonochromeCourt));
        }

        [Test]
        public void Court_content_contains_the_approved_five_piece_roster_and_route_only_twins()
        {
            Assert.That(MonochromeContent.Arena.Id, Is.EqualTo("monochrome-court"));
            Assert.That(
                MonochromeContent.Enemies.Select(enemy => enemy.Id),
                Is.EqualTo(new[]
                {
                    "court-pawn", "court-rook", "court-bishop", "court-knight", "court-queen",
                }));
            Assert.That(MonochromeContent.BlackBoss.StartsAtSeconds, Is.LessThan(0));
            Assert.That(MonochromeContent.WhiteBoss.StartsAtSeconds, Is.LessThan(0));
        }

        [TestCase(true, 13, 4, 13, 9)]
        [TestCase(false, 13, 4, 4, 4)]
        public void Knight_path_has_one_visible_right_angle_corner(
            bool horizontalFirst,
            double targetX,
            double targetY,
            double expectedCornerX,
            double expectedCornerY)
        {
            var corner = MonochromeEncounterRules.KnightCorner(
                4,
                9,
                targetX,
                targetY,
                horizontalFirst);

            Assert.That(corner, Is.EqualTo(new MonochromePoint(expectedCornerX, expectedCornerY)));
        }

        [Test]
        public void Queen_always_claims_rank_file_and_one_deterministic_diagonal()
        {
            Assert.That(
                MonochromeEncounterRules.QueenAttackLines(2),
                Is.EqualTo(new[] { CourtLine.Rank, CourtLine.File, CourtLine.DiagonalRise }));
            Assert.That(
                MonochromeEncounterRules.QueenAttackLines(3),
                Is.EqualTo(new[] { CourtLine.Rank, CourtLine.File, CourtLine.DiagonalFall }));
        }

        [Test]
        public void Grandmasters_alternate_white_and_black_floor_hazards()
        {
            Assert.That(
                MonochromeEncounterRules.HazardAt(0, false),
                Is.EqualTo(new CourtHazardState(CourtFaction.White, CourtHazardStage.Warning)));
            Assert.That(
                MonochromeEncounterRules.HazardAt(0.9, false),
                Is.EqualTo(new CourtHazardState(CourtFaction.White, CourtHazardStage.Burning)));
            Assert.That(
                MonochromeEncounterRules.HazardAt(3.1, false),
                Is.EqualTo(new CourtHazardState(CourtFaction.White, CourtHazardStage.Recovery)));
            Assert.That(
                MonochromeEncounterRules.HazardAt(3.6, false),
                Is.EqualTo(new CourtHazardState(CourtFaction.Black, CourtHazardStage.Warning)));
            Assert.That(
                MonochromeEncounterRules.HazardAt(4.5, false),
                Is.EqualTo(new CourtHazardState(CourtFaction.Black, CourtHazardStage.Burning)));
            Assert.That(
                MonochromeEncounterRules.HazardAt(7.2, false),
                Is.EqualTo(new CourtHazardState(CourtFaction.White, CourtHazardStage.Warning)));
        }

        [Test]
        public void Phase_two_shortens_warning_without_removing_recovery()
        {
            Assert.That(MonochromeEncounterRules.HazardAt(0.69, true).Stage,
                Is.EqualTo(CourtHazardStage.Warning));
            Assert.That(MonochromeEncounterRules.HazardAt(0.7, true).Stage,
                Is.EqualTo(CourtHazardStage.Burning));
            Assert.That(MonochromeEncounterRules.HazardAt(3.1, true).Stage,
                Is.EqualTo(CourtHazardStage.Recovery));
        }

        [TestCase(CourtHazardStage.Warning, CourtFaction.White, false)]
        [TestCase(CourtHazardStage.Burning, CourtFaction.White, true)]
        [TestCase(CourtHazardStage.Burning, CourtFaction.Black, false)]
        [TestCase(CourtHazardStage.Recovery, CourtFaction.White, false)]
        public void Only_burning_tiles_of_the_controlled_color_are_dangerous(
            CourtHazardStage stage,
            CourtFaction tileFaction,
            bool expected)
        {
            var hazard = new CourtHazardState(CourtFaction.White, stage);
            Assert.That(MonochromeEncounterRules.IsTileDangerous(hazard, tileFaction), Is.EqualTo(expected));
        }
    }
}
