using NUnit.Framework;
using VoidFall.Core;

namespace VoidFall.Tests.Editor
{
    public sealed class WorldGridAndCameraImpulseTests
    {
        [TestCase(-3600, -5040)] [TestCase(72000, -144000)] [TestCase(-72000, 144000)]
        public void Translation_preserves_all_neighborhood_results_and_order(int dx, int dy)
        {
            var origin = new CollisionGrid(750); var distant = new CollisionGrid(750);
            var a = new int[750]; var b = new int[750];
            for (var i = 0; i < 750; i++)
            {
                var x = (i % 30 - 15) * 48; var y = (i / 30 - 12) * 48;
                origin.Insert(i, x, y); distant.Insert(i, x + dx, y + dy);
            }
            for (var i = 0; i < 750; i++)
            {
                var x = (i % 30 - 15) * 48; var y = (i / 30 - 12) * 48;
                var count = origin.QueryNeighborhood(x, y, 1, a);
                Assert.That(distant.QueryNeighborhood(x + dx, y + dy, 1, b), Is.EqualTo(count));
                Assert.That(count, Is.LessThan(30), "Separated far-world bodies must not share one clamped bucket");
                for (var n = 0; n < count; n++) Assert.That(b[n], Is.EqualTo(a[n]));
            }
        }

        [Test]
        public void Sparse_world_cells_clear_and_reuse_without_aliasing()
        {
            var grid = new CollisionGrid(750); var found = new int[750];
            for (var pass = 0; pass < 3; pass++)
            {
                grid.Clear();
                for (var i = 0; i < 750; i++) grid.Insert(i, -72000 + i * 144, 144000 + pass * 72);
                for (var i = 0; i < 750; i++)
                {
                    Assert.That(grid.Query(-72000 + i * 144, 144000 + pass * 72, 1, found), Is.EqualTo(1));
                    Assert.That(found[0], Is.EqualTo(i));
                }
            }
            grid.Clear(); Assert.That(grid.QueryNeighborhood(0, 0, 1, found), Is.Zero);
        }

        [Test]
        public void Sustained_hundred_kills_per_second_leave_settling_gaps_and_cannot_reach_major_strength()
        {
            var shake = new CameraImpulse(); var settled = 0;
            for (var tick = 0; tick < 4000; tick++)
            {
                shake.Advance(.01); shake.Request(.055, false);
                Assert.That(shake.AmplitudeNow, Is.LessThanOrEqualTo(4.5));
                if (shake.AmplitudeNow < .1) settled++;
            }
            Assert.That(settled, Is.GreaterThan(500), "40 seconds of kills must retain quiet gaps");
            shake.Advance(.3); Assert.That(shake.AmplitudeNow, Is.Zero);
        }

        [Test]
        public void Cluster_strengthens_without_extending_lifetime_and_boss_overrides_minor_pulse()
        {
            var shake = new CameraImpulse(); shake.Request(.055, false);
            var single = shake.AmplitudeNow;
            for (var i = 0; i < 40; i++) shake.Request(.055, false);
            Assert.That(shake.AmplitudeNow, Is.GreaterThan(single));
            Assert.That(shake.AmplitudeNow, Is.LessThanOrEqualTo(4.5));
            shake.Advance(.17); Assert.That(shake.AmplitudeNow, Is.Zero);
            shake.Request(1, true); Assert.That(shake.MajorAmplitude, Is.GreaterThan(10));
            shake.Advance(.3); Assert.That(shake.AmplitudeNow, Is.Zero);
        }

        [Test]
        public void Shake_is_time_based_directional_and_resettable()
        {
            var a = new CameraImpulse(); var b = new CameraImpulse();
            a.Request(.3, false, 0, 1); b.Request(.3, false, 0, 1);
            a.Offset(out var startX, out var startY);
            Assert.That(startX, Is.EqualTo(0).Within(.0001)); Assert.That(startY, Is.GreaterThan(0));
            a.Advance(.1); for (var i = 0; i < 10; i++) b.Advance(.01);
            a.Offset(out var ax, out var ay); b.Offset(out var bx, out var by);
            Assert.That(bx, Is.EqualTo(ax).Within(.000001)); Assert.That(by, Is.EqualTo(ay).Within(.000001));
            a.Reset(); Assert.That(a.AmplitudeNow, Is.Zero);
        }
    }
}
