using NUnit.Framework;
using VoidFall.Core;

namespace VoidFall.Tests.Editor
{
    /// <summary>
    /// Covers the director formation layer: geometry (walls span the
    /// cross-axis, wedges point at the player, arcs close like a jaw),
    /// type identity (the exploder wall is exploder-only), unlock gating,
    /// determinism, and the director rotation that schedules them.
    /// </summary>
    public sealed class FormationRulesTests
    {
        private const double HalfWidth = 512;
        private const double HalfHeight = 288;

        private static FormationSpawn[] Compose(FormationKind kind, double time, uint hash, int cap = 64)
        {
            return FormationRules.Compose(kind, time, hash, 0, 0, HalfWidth, HalfHeight, cap);
        }

        [Test]
        public void Exploder_wall_is_exploder_only_and_spans_the_cross_axis()
        {
            var spawns = Compose(FormationKind.ExploderWall, 600, 0x1234u);

            Assert.That(spawns.Length, Is.GreaterThanOrEqualTo(8));
            foreach (var spawn in spawns)
                Assert.That(spawn.EnemyId, Is.EqualTo("exploder"),
                    "the signature wall must be exploder-only");

            // hash % 4 == 0: sweeps +X, so the line runs vertically and all
            // members sit off-screen at the left with identical depth.
            foreach (var spawn in spawns)
            {
                Assert.That(spawn.X, Is.LessThan(-HalfWidth + 0.001),
                    "every wall member must spawn beyond the left edge");
                Assert.That(spawn.Y, Is.InRange(
                    -HalfHeight - FormationRules.OffscreenMargin - 0.001,
                    HalfHeight + FormationRules.OffscreenMargin + 0.001));
            }
            var minY = spawns[0].Y;
            var maxY = minY;
            foreach (var spawn in spawns)
            {
                minY = System.Math.Min(minY, spawn.Y);
                maxY = System.Math.Max(maxY, spawn.Y);
            }
            Assert.That(maxY - minY, Is.GreaterThanOrEqualTo(
                (HalfHeight + FormationRules.OffscreenMargin) * 2 - 0.001),
                "the wall must span past the whole visible cross-axis");
        }

        [Test]
        public void Wall_type_follows_the_roster_clock()
        {
            foreach (var spawn in Compose(FormationKind.WallSweep, 120, 0x24u))
                Assert.That(spawn.EnemyId, Is.EqualTo("chaser"));
            foreach (var spawn in Compose(FormationKind.WallSweep, 240, 0x24u))
                Assert.That(spawn.EnemyId, Is.EqualTo("runner"));
        }

        [Test]
        public void Exploder_wall_waits_for_the_exploder_unlock()
        {
            Assert.That(
                FormationRules.PickKind(1u << 0, 60),
                Is.EqualTo(FormationKind.WallSweep),
                "before unlock the seeded exploder wall degrades to a plain wall");
            Assert.That(
                FormationRules.PickKind(1u << 0, FormationRules.ExploderWallUnlockSeconds + 1),
                Is.EqualTo(FormationKind.ExploderWall));
        }

        [Test]
        public void Wedge_apex_leads_and_arms_widen_behind_it()
        {
            // hash % 4 == 2: sweeps +Y from the bottom; depth grows downward.
            var spawns = Compose(FormationKind.VeeWedge, 300, 6u);
            Assert.That(spawns.Length, Is.GreaterThanOrEqualTo(7));

            var apex = spawns[0];
            Assert.That(apex.Y, Is.GreaterThan(spawns[1].Y),
                "the apex must be the member closest to the player");
            Assert.That(apex.X, Is.EqualTo(0).Within(0.001),
                "the apex sits on the player's cross-axis");

            // Arm pairs are symmetric around the apex column and widen with depth.
            Assert.That(spawns[1].X, Is.EqualTo(-spawns[2].X).Within(0.001));
            Assert.That(spawns[1].Y, Is.EqualTo(spawns[2].Y).Within(0.001));
            Assert.That(System.Math.Abs(spawns[3].X), Is.GreaterThan(System.Math.Abs(spawns[1].X)),
                "arms widen behind the apex");

            foreach (var spawn in spawns)
                Assert.That(spawn.EnemyId, Is.EqualTo("dasher"));
        }

        [Test]
        public void Column_is_a_single_file_with_geometric_depth()
        {
            // hash % 4 == 0: sweeps +X; every member shares the player's Y.
            var spawns = Compose(FormationKind.Column, 300, 4u);
            Assert.That(spawns.Length, Is.GreaterThanOrEqualTo(6));
            for (var index = 0; index < spawns.Length; index++)
            {
                Assert.That(spawns[index].Y, Is.EqualTo(0).Within(0.001));
                Assert.That(spawns[index].X, Is.LessThanOrEqualTo(-HalfWidth - 0.001));
                if (index > 0)
                    Assert.That(spawns[index].X,
                        Is.LessThan(spawns[index - 1].X - FormationRules.DepthSpacing + 0.001),
                        "the column trails behind its leader at fixed spacing");
            }
            foreach (var spawn in spawns)
                Assert.That(spawn.EnemyId, Is.EqualTo("runner"));
        }

        [Test]
        public void Phalanx_is_a_uniform_guard_block()
        {
            // hash % 4 == 1: sweeps -X from the right.
            var spawns = Compose(FormationKind.Phalanx, 400, 9u);
            Assert.That(spawns.Length, Is.EqualTo(FormationRules.PhalanxRows * 5),
                "default columns at 400s: 3 rows x 5");
            foreach (var spawn in spawns)
                Assert.That(spawn.EnemyId, Is.EqualTo("guard"));
            foreach (var spawn in spawns)
                Assert.That(spawn.X, Is.GreaterThan(HalfWidth),
                    "the block must spawn beyond the right edge");
        }

        [Test]
        public void Arc_is_a_half_circle_opening_away_from_the_player()
        {
            var spawns = Compose(FormationKind.ArcClose, 300, 12u);
            Assert.That(spawns.Length, Is.GreaterThanOrEqualTo(9));
            var radius = System.Math.Sqrt(HalfWidth * HalfWidth + HalfHeight * HalfHeight) + 25;
            foreach (var spawn in spawns)
            {
                Assert.That(spawn.EnemyId, Is.EqualTo("gunner"));
                var distance = System.Math.Sqrt(spawn.X * spawn.X + spawn.Y * spawn.Y);
                Assert.That(distance, Is.EqualTo(radius).Within(0.001));
            }
        }

        [Test]
        public void Compose_is_deterministic_and_respects_the_field_cap()
        {
            var first = Compose(FormationKind.ExploderWall, 600, 0xfeedu);
            var second = Compose(FormationKind.ExploderWall, 600, 0xfeedu);
            Assert.That(second.Length, Is.EqualTo(first.Length));
            for (var index = 0; index < first.Length; index++)
            {
                Assert.That(second[index].X, Is.EqualTo(first[index].X).Within(0.001));
                Assert.That(second[index].Y, Is.EqualTo(first[index].Y).Within(0.001));
                Assert.That(second[index].EnemyId, Is.EqualTo(first[index].EnemyId));
            }

            var capped = Compose(FormationKind.ExploderWall, 600, 0xfeedu, 4);
            Assert.That(capped.Length, Is.EqualTo(4), "a busy field trims the wall, never overfills it");
        }

        [Test]
        public void Director_schedules_a_formation_every_fifth_event()
        {
            for (var index = 0; index < 20; index++)
            {
                var directorEvent = DirectorRules.Event(0x5f1dc0deu, index);
                var expected = index % 5 == 4;
                Assert.That(directorEvent.Id == "formation", Is.EqualTo(expected),
                    "event " + index + " formation scheduling drifted");
                if (expected)
                {
                    Assert.That(directorEvent.FormationSeed, Is.Not.EqualTo(0u));
                    Assert.That(directorEvent.DurationSeconds, Is.EqualTo(6));
                }
            }

            // Same event index reproduces the same formation seed.
            Assert.That(
                DirectorRules.Event(0x5f1dc0deu, 9).FormationSeed,
                Is.EqualTo(DirectorRules.Event(0x5f1dc0deu, 9).FormationSeed));
        }
    }
}
