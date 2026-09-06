using System;
using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private bool _ordinaryEnemyShotContext;
        private Vector2 _orbitalStartPlayerPosition;
        private float _orbitalBladeStartAngle;
        private float _orbitalClockStartAngle;
        private Vector2 _orbitalHollowStartPosition;
        private Vector2 _orbitalHollowEndPosition;
        private bool _orbitalHollowWasActive;
        private bool _orbitalHollowHistoryValid;
        private Func<int, Vector2, Vector2, float, bool> _hostileShotInterceptHandler;

        private float OrbitalRotationSpeedScale(float recovery)
            => (float)SupportEffectRules.ProjectileSpeedMultiplier(SupportRank("projectileSpeed")) / Mathf.Max(.05f, recovery);

        private void ResetOrbitalDefense()
        {
            _ordinaryEnemyShotContext = false;
            _orbitalStartPlayerPosition = Vector2.zero;
            _orbitalBladeStartAngle = 0;
            _orbitalClockStartAngle = 0;
            _orbitalHollowStartPosition = _orbitalHollowEndPosition = Vector2.zero;
            _orbitalHollowWasActive = _orbitalHollowHistoryValid = false;
            if (_gameSim != null) Array.Clear(_gameSim.HostileShotBlockable, 0, _gameSim.HostileShotBlockable.Length);
        }

        private bool TryInterceptHostileShot(int slot, Vector2 from, Vector2 to, float shotRadius)
        {
            if (!_gameSim.HostileShotBlockable[slot] || _gameSim.Player.Health <= 0 || _gameOver || _revivePending) return false;
            var player = _gameSim.Player.Position;
            var impactFraction = 0f;
            var intercepted = false;
            var bladeRank = ArsenalRank(3);
            if (bladeRank > 0)
            {
                var stats = ContentCatalog.Weapons[3].Ranks[Mathf.Clamp(bladeRank, 1, 6) - 1].Stats;
                var count = Mathf.Min(stats.OrbitCount, MaxBladeViews);
                var radius = (float)stats.OrbitRadius * _areaMultiplier;
                var collisionRadius = shotRadius + (float)stats.ProjectileRadius;
                for (var blade = 0; blade < count; blade++)
                {
                    var offset = blade * Mathf.PI * 2 / Mathf.Max(1, count);
                    var a = _orbitalBladeStartAngle + offset;
                    var b = _bladeAngle + offset;
                    var before = _orbitalStartPlayerPosition + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                    var after = player + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius;
                    var relativeFrom = from - before;
                    var relativeTo = to - after;
                    if (PointSegmentDistanceSquared(Vector2.zero, relativeFrom, relativeTo, out impactFraction) > collisionRadius * collisionRadius) continue;
                    intercepted = true; break;
                }
                if (!intercepted && _hollowBladeActive && ArsenalEvolved(3) && _orbitalHollowHistoryValid)
                {
                    var hollowRadius = shotRadius + (float)stats.ProjectileRadius * 1.25f;
                    intercepted = PointSegmentDistanceSquared(Vector2.zero, from - _orbitalHollowStartPosition, to - _orbitalHollowEndPosition, out impactFraction) <= hollowRadius * hollowRadius;
                }
            }
            var clockRank = ArsenalRank(8);
            if (!intercepted && clockRank > 0)
            {
                var reach = (float)ArsenalStats(8, clockRank).OrbitRadius * _areaMultiplier;
                var width = shotRadius + 7 * ArsenalSizeMultiplier();
                var hands = ArsenalEvolved(8) ? 2 : 1;
                for (var hand = 0; hand < hands && !intercepted; hand++)
                {
                    var startAngle = hand == 0 ? _orbitalClockStartAngle : -_orbitalClockStartAngle + Mathf.PI;
                    var endAngle = hand == 0 ? _arsenalClockAngle : -_arsenalClockAngle + Mathf.PI;
                    var angularDelta = Mathf.DeltaAngle(startAngle * Mathf.Rad2Deg, endAngle * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                    // Transform synchronized motion into the rotating hand's frame. Comparing a whole
                    // bullet path against each old hand pose would incorrectly turn its sweep into a shield.
                    var samples = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(angularDelta) / .025f), 1, 128);
                    var localFrom = OrbitalLocalPoint(from - _orbitalStartPlayerPosition, startAngle);
                    for (var sample = 1; sample <= samples; sample++)
                    {
                        var t = sample / (float)samples;
                        var center = Vector2.Lerp(_orbitalStartPlayerPosition, player, t);
                        var angle = startAngle + angularDelta * t;
                        var localTo = OrbitalLocalPoint(Vector2.Lerp(from, to, t) - center, angle);
                        if (SegmentsWithinRadius(localFrom, localTo, Vector2.zero, Vector2.right * reach, width, out var localFraction))
                        {
                            impactFraction = (sample - 1 + localFraction) / samples;
                            intercepted = true; break;
                        }
                        localFrom = localTo;
                    }
                }
            }
            if (!intercepted) return false;
            BurstFx(Vector2.Lerp(from, to, impactFraction), DotCyan, 3, 80, .18f, .3f);
            return true;
        }

        private static float PointSegmentDistanceSquared(Vector2 point, Vector2 a, Vector2 b, out float fraction)
        {
            var delta = b - a;
            fraction = delta.sqrMagnitude > .000001f ? Mathf.Clamp01(Vector2.Dot(point - a, delta) / delta.sqrMagnitude) : 0;
            return (point - (a + delta * fraction)).sqrMagnitude;
        }

        private static float OrbitalCross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private static Vector2 OrbitalLocalPoint(Vector2 delta, float angle)
        {
            var cosine = Mathf.Cos(angle); var sine = Mathf.Sin(angle);
            return new Vector2(delta.x * cosine + delta.y * sine, -delta.x * sine + delta.y * cosine);
        }

        private static bool SegmentsWithinRadius(Vector2 shotA, Vector2 shotB, Vector2 handA, Vector2 handB, float radius, out float impactFraction)
        {
            var r = shotB - shotA;
            var s = handB - handA;
            var denominator = OrbitalCross(r, s);
            if (Mathf.Abs(denominator) > .000001f)
            {
                var difference = handA - shotA;
                var t = OrbitalCross(difference, s) / denominator;
                var u = OrbitalCross(difference, r) / denominator;
                if (t >= 0 && t <= 1 && u >= 0 && u <= 1) { impactFraction = t; return true; }
            }
            var best = PointSegmentDistanceSquared(shotA, handA, handB, out _);
            impactFraction = 0;
            var candidate = PointSegmentDistanceSquared(shotB, handA, handB, out _);
            if (candidate < best) { best = candidate; impactFraction = 1; }
            candidate = PointSegmentDistanceSquared(handA, shotA, shotB, out var fraction);
            if (candidate < best) { best = candidate; impactFraction = fraction; }
            candidate = PointSegmentDistanceSquared(handB, shotA, shotB, out fraction);
            if (candidate < best) { best = candidate; impactFraction = fraction; }
            return best <= radius * radius;
        }
    }
}
