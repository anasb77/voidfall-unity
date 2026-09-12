using NUnit.Framework;
using UnityEngine;
using VoidFall.Core;
using VoidFall.Runtime;

namespace VoidFall.Tests.Editor
{
    public sealed class MonochromeRuntimeRulesTests
    {
        [Test]
        public void Court_board_clamp_preserves_collision_radius_at_all_four_edges()
        {
            var origin = new Vector2(-100f,-200f); var size = new Vector2(3600f,3600f);
            Assert.That(MonochromeRuntimeRules.ClampToBoard(new Vector2(-9999f,-9999f),origin,size,14f),
                Is.EqualTo(new Vector2(-86f,-186f)));
            Assert.That(MonochromeRuntimeRules.ClampToBoard(new Vector2(9999f,9999f),origin,size,14f),
                Is.EqualTo(new Vector2(3486f,3386f)));
            Assert.That(MonochromeRuntimeRules.ClampToBoard(Vector2.zero,origin,size,14f),Is.EqualTo(Vector2.zero));
        }

        [TestCase(CourtFaction.White, -500f)]
        [TestCase(CourtFaction.Black, 700f)]
        public void Split_field_armies_enter_from_the_opposite_color(CourtFaction faction, float expected)
        {
            var method = typeof(MonochromeRuntimeRules).GetMethod("SplitSpawnX");
            Assert.That(method, Is.Not.Null);
            Assert.That((float)method.Invoke(null, new object[] { faction, 100f, 600f }), Is.EqualTo(expected));
        }

        [Test]
        public void Reduced_motion_keeps_the_tile_signal_steady()
        {
            var method = typeof(MonochromeRuntimeRules).GetMethod("HazardPulse");
            Assert.That(method, Is.Not.Null);
            var initial = (float)method.Invoke(null, new object[] { 0f, true });
            Assert.That(initial, Is.InRange(.1f, 1f));
            foreach (var time in new[] { .05f, .1f, .5f, 3f })
                Assert.That((float)method.Invoke(null, new object[] { time, true }), Is.EqualTo(initial));
            Assert.That((float)method.Invoke(null, new object[] { .05f, false }),
                Is.Not.EqualTo((float)method.Invoke(null, new object[] { .15f, false })));
        }
        [TestCase(CourtFaction.Black, -1)]
        [TestCase(CourtFaction.White, 1)]
        public void Factions_spawn_from_opposite_sides(CourtFaction faction, int expectedSign)
        {
            Assert.That(Mathf.Sign(MonochromeRuntimeRules.SpawnX(faction, 100, 600)), Is.EqualTo(expectedSign));
        }

        [TestCase(0.00, "court-pawn")]
        [TestCase(0.50, "court-knight")]
        [TestCase(0.75, "court-bishop")]
        [TestCase(0.90, "court-rook")]
        [TestCase(0.99, "court-queen")]
        public void Monochrome_spawn_selector_returns_only_chess_enemies(double roll, string expected)
        {
            Assert.That(MonochromeRuntimeRules.NextSpawnId(roll), Is.EqualTo(expected));
        }

        [Test]
        public void Board_color_is_anchored_to_world_space_and_alternates_on_both_axes()
        {
            var origin = new Vector2(100, -50);
            var tileSize = new Vector2(40, 30);

            Assert.That(
                MonochromeRuntimeRules.FactionAtWorldPosition(origin + new Vector2(5, 5), origin, tileSize),
                Is.EqualTo(CourtFaction.White));
            Assert.That(
                MonochromeRuntimeRules.FactionAtWorldPosition(origin + new Vector2(45, 5), origin, tileSize),
                Is.EqualTo(CourtFaction.Black));
            Assert.That(
                MonochromeRuntimeRules.FactionAtWorldPosition(origin + new Vector2(5, 35), origin, tileSize),
                Is.EqualTo(CourtFaction.Black));
            Assert.That(
                MonochromeRuntimeRules.FactionAtWorldPosition(origin + new Vector2(-5, 5), origin, tileSize),
                Is.EqualTo(CourtFaction.Black));
        }

        [TestCase(CourtHazardStage.Warning, CourtFaction.White, 0f, false)]
        [TestCase(CourtHazardStage.Burning, CourtFaction.Black, 0f, false)]
        [TestCase(CourtHazardStage.Burning, CourtFaction.White, 0.1f, false)]
        [TestCase(CourtHazardStage.Burning, CourtFaction.White, 0f, true)]
        public void Floor_damage_requires_an_active_matching_tile_and_an_expired_cooldown(
            CourtHazardStage stage,
            CourtFaction tileFaction,
            float cooldown,
            bool expected)
        {
            var hazard = new CourtHazardState(CourtFaction.White, stage);
            Assert.That(
                MonochromeRuntimeRules.ShouldApplyFloorDamage(hazard, tileFaction, cooldown),
                Is.EqualTo(expected));
        }

        [Test]
        public void Rook_charge_is_fast_but_committed_to_the_cardinal_axis()
        {
            var velocity = MonochromeRuntimeRules.RookChargeVelocity(
                new Vector2(5, 2),
                82);

            Assert.That(velocity.y, Is.Zero);
            Assert.That(velocity.x, Is.GreaterThan(82 * 2));
        }

        [Test]
        public void Bishop_uses_a_sniper_distance_and_long_readable_telegraph()
        {
            Assert.That(MonochromeContent.FindEnemy("court-bishop").PreferredDistance, Is.GreaterThanOrEqualTo(470));
            Assert.That(MonochromeContent.FindEnemy("court-bishop").TelegraphSeconds, Is.GreaterThanOrEqualTo(1));
        }

        [TestCase("monochrome-court", true, true)]
        [TestCase("monochrome-court", false, false)]
        [TestCase("hydra", true, false)]
        public void Ambient_suppression_is_owned_by_the_live_twin_boss_phase(
            string voidId,
            bool bossPhase,
            bool expected)
        {
            Assert.That(MonochromeRuntimeRules.SuppressAmbientSpawns(voidId, bossPhase), Is.EqualTo(expected));
        }

        [Test]
        public void Shared_health_never_underflows_and_is_identical_for_both_twins()
        {
            var remaining = MonochromeRuntimeRules.ApplySharedDamage(1200, 1750);
            Assert.That(remaining, Is.Zero);
            Assert.That(MonochromeRuntimeRules.ApplySharedDamage(1200, -40), Is.EqualTo(1200));
        }

        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(3, 2)]
        public void Queen_promotion_is_bounded_to_two_pawns(int activePromotions, int expected)
        {
            Assert.That(MonochromeRuntimeRules.PromotionsAfterCast(activePromotions), Is.EqualTo(expected));
        }
    }
}
