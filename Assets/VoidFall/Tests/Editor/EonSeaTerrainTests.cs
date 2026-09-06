using System.Linq;
using NUnit.Framework;
using VoidFall.Core;

namespace VoidFall.Tests.Editor
{
    public sealed class EonSeaTerrainTests
    {
        [Test]
        public void Seeded_stream_is_bounded_and_seed_changes_layout()
        {
            var first = new EonSeaTerrain(42); var same = new EonSeaTerrain(42); var other = new EonSeaTerrain(43);
            first.Stream(0, 0); same.Stream(0, 0); other.Stream(0, 0);
            Assert.That(first.Ice.Count, Is.LessThanOrEqualTo(81));
            Assert.That(first.Patches.Count, Is.EqualTo(27));
            Assert.That(first.Ice.Select(i => i.X), Is.EqualTo(same.Ice.Select(i => i.X)));
            Assert.That(first.Ice[0].X, Is.Not.EqualTo(other.Ice[0].X));
            Assert.That(first.Ice.All(i => i.Duration >= 80 && i.Duration <= 165), Is.True);
            first.Stream(100000, -100000);
            Assert.That(first.Ice.Count, Is.LessThanOrEqualTo(81));
        }

        [Test]
        public void Explosion_stress_is_capped_and_accelerates_without_direct_damage()
        {
            var terrain = new EonSeaTerrain(42); terrain.Stream(0, 0);
            var ice = terrain.Ice[0]; var melt = ice.Melt;
            for (var i = 0; i < 10; i++) terrain.Explode(ice.X, ice.Y, 0);
            Assert.That(ice.ExplosionCracks, Is.EqualTo(3));
            Assert.That(ice.Melt, Is.EqualTo(melt));
            terrain.Step(1);
            Assert.That(ice.Melt - melt, Is.EqualTo(4.3f / ice.Duration).Within(0.00001f));
        }

        [Test]
        public void Offscreen_ice_ages_and_destruction_persists_for_visit()
        {
            var terrain = new EonSeaTerrain(42); terrain.Stream(0, 0);
            var ice = terrain.Ice[0]; var id = ice.Id;
            terrain.Stream(10000, 10000); terrain.Step(200); terrain.Stream(0, 0);
            Assert.That(terrain.Ice.Any(i => i.Id == id), Is.False);
            Assert.That(terrain.Pulses.Count, Is.LessThanOrEqualTo(81));
            terrain.Stream(10000, 10000); terrain.Stream(0, 0);
            Assert.That(terrain.Ice.Any(i => i.Id == id), Is.False);
        }

        [Test]
        public void Collapse_warns_then_pulses_once_without_touch_damage()
        {
            var terrain = new EonSeaTerrain(42); terrain.Stream(0, 0);
            var ice = terrain.Ice[0]; ice.Melt = 0.88f;
            Assert.That(ice.Warning, Is.True);
            ice.Melt = 0.9999f;
            terrain.Step(0.1f);
            Assert.That(terrain.Pulses.Any(p => p.X == ice.X && p.Radius == 140 + ice.Radius), Is.True);
            var count = terrain.Pulses.Count;
            terrain.Step(0);
            Assert.That(terrain.Pulses.Count, Is.EqualTo(count));
        }

        [Test]
        public void Swept_projectile_and_actor_cannot_tunnel_through_capsule()
        {
            var terrain = new EonSeaTerrain(42); terrain.Stream(0, 0);
            var ice = terrain.Ice[0]; ice.Angle = 0;
            float hitX, hitY;
            Assert.That(terrain.FirstHit(ice.X, ice.Y - 200, ice.X, ice.Y + 200, 3, out hitX, out hitY), Is.True);
            Assert.That(hitY, Is.LessThan(ice.Y));
            var x = ice.X; var y = ice.Y - 200;
            terrain.Move(ref x, ref y, 0, 400, 13);
            Assert.That(y, Is.LessThan(ice.Y));
        }

        [Test]
        public void Freeze_refreshes_twenty_seconds_without_stacking_or_identity_leak()
        {
            var frost = new EonSeaFreeze(); frost.Refresh(12, 0);
            Assert.That(frost.Scale(12, 19.99f), Is.EqualTo(0.5f));
            frost.Refresh(12, 10);
            Assert.That(frost.Scale(12, 29.99f), Is.EqualTo(0.5f));
            Assert.That(frost.Scale(12, 30), Is.EqualTo(1));
            Assert.That(frost.Scale(13, 11), Is.EqualTo(1));
        }
    }
}
