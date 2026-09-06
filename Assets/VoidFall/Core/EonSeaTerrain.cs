using System;
using System.Collections.Generic;

namespace VoidFall.Core
{
    public enum EonSeaIceKind { Mass, Brittle, Wall, Wave }

    public sealed class EonSeaIce
    {
        public string Id;
        public float X, Y, Radius, Length, Angle, Melt, Duration, Flash;
        public int Variant, Cracks, ExplosionCracks;
        public EonSeaIceKind Kind;
        internal float LastAt;
        public bool Broken => Melt >= 1f;
        public bool Warning => !Broken && Melt >= 0.88f;
    }

    public struct EonSeaPatch
    {
        public float X, Y, RadiusX, RadiusY, Angle;
    }

    public struct EonSeaPulse
    {
        public float X, Y, Radius, Life;
        public int Variant;
        public const float Duration = 1.6f;
    }

    public struct EonSeaFreeze
    {
        private int _identity;
        private float _until;
        public void Refresh(int identity, float now) { _identity = identity; _until = now + 20f; }
        public float Scale(int identity, float now) => identity == _identity && now < _until ? 0.5f : 1f;
    }

    /// <summary>Visit-local, seeded streamed terrain. No engine or combat RNG dependencies.</summary>
    public sealed class EonSeaTerrain
    {
        public const int ChunkSize = 1000;
        public const int MaxIce = 81;
        public const int MaxPatches = 27;
        private readonly uint _seed;
        private readonly float _entryX, _entryY;
        private readonly Dictionary<string, EonSeaIce> _visited = new Dictionary<string, EonSeaIce>();
        private readonly List<EonSeaIce> _ice = new List<EonSeaIce>(MaxIce);
        private readonly List<EonSeaPatch> _patches = new List<EonSeaPatch>(MaxPatches);
        private readonly List<EonSeaPulse> _pulses = new List<EonSeaPulse>(MaxIce);
        private readonly List<EonSeaPulse> _collapses = new List<EonSeaPulse>(MaxIce);
        private int _chunkX = int.MinValue, _chunkY = int.MinValue;
        public float Time { get; private set; }
        public IReadOnlyList<EonSeaIce> Ice => _ice;
        public IReadOnlyList<EonSeaPatch> Patches => _patches;
        public IReadOnlyList<EonSeaPulse> Pulses => _pulses;
        public IReadOnlyList<EonSeaPulse> Collapses => _collapses;

        public EonSeaTerrain(uint seed, float entryX = 0, float entryY = 0) { _seed = seed; _entryX = entryX; _entryY = entryY; }

        private float Hash(int x, int y, int n)
        {
            unchecked
            {
                var h = ((uint)x ^ (uint)n ^ _seed) * 374761393u ^ (uint)y * 668265263u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h ^ (h >> 16)) / 4294967296f;
            }
        }

        public void Stream(float x, float y)
        {
            var cx = (int)Math.Floor(x / ChunkSize); var cy = (int)Math.Floor(y / ChunkSize);
            if (cx == _chunkX && cy == _chunkY) return;
            _chunkX = cx; _chunkY = cy; _ice.Clear(); _patches.Clear();
            for (var j = cy - 1; j <= cy + 1; j++)
            for (var i = cx - 1; i <= cx + 1; i++)
            {
                for (var k = 0; k < 9; k++)
                {
                    var id = i + ":" + j + ":" + k;
                    if (!_visited.TryGetValue(id, out var ice))
                    {
                        var px = i * ChunkSize + 120 + k % 3 * 290 + Hash(i, j, k + 12) * 100 - 50;
                        var py = j * ChunkSize + 120 + k / 3 * 290 + Hash(i, j, k + 77) * 100 - 50;
                        // A clear entry area, followed by widely spaced capsules and open routes.
                        if ((px - _entryX) * (px - _entryX) + (py - _entryY) * (py - _entryY) < 170 * 170) continue;
                        var roll = (int)(Hash(i, j, k + 200) * 6);
                        var kind = roll == 0 ? EonSeaIceKind.Mass : roll == 3 ? EonSeaIceKind.Wave : roll == 1 || roll == 4 ? EonSeaIceKind.Brittle : EonSeaIceKind.Wall;
                        ice = new EonSeaIce { Id = id, X = px, Y = py, Kind = kind,
                            Radius = kind == EonSeaIceKind.Mass ? 48 : kind == EonSeaIceKind.Wave ? 33 : kind == EonSeaIceKind.Wall ? 20 : 28,
                            Length = kind == EonSeaIceKind.Brittle ? 55 : 90 + Hash(i, j, k + 90) * 100,
                            Angle = Hash(i, j, k + 177) * (float)Math.PI,
                            Variant = Math.Min(7, (int)(Hash(i, j, k + 88) * 8)),
                            Melt = Hash(i, j, k + 215) * 0.3f,
                            Duration = (kind == EonSeaIceKind.Mass ? 135 : kind == EonSeaIceKind.Wave ? 115 : kind == EonSeaIceKind.Wall ? 80 : 95) + Hash(i, j, k + 213) * 30,
                            LastAt = Time };
                        _visited.Add(id, ice);
                    }
                    Age(ice, Time - ice.LastAt);
                    // Offscreen collapse is retained silently; returning never replays a freeze.
                    if (!ice.Broken) _ice.Add(ice);
                }
                for (var k = 0; k < 3; k++) _patches.Add(new EonSeaPatch {
                    X = i * ChunkSize + 140 + Hash(i, j, k + 500) * 720,
                    Y = j * ChunkSize + 140 + Hash(i, j, k + 502) * 720,
                    RadiusX = 90 + Hash(i, j, k + 503) * 90,
                    RadiusY = 55 + Hash(i, j, k + 504) * 45,
                    Angle = Hash(i, j, k + 505) * (float)Math.PI });
            }
        }

        private void Age(EonSeaIce ice, float dt)
        {
            ice.Melt = Math.Min(1f, ice.Melt + Math.Max(0, dt) * (1f + ice.ExplosionCracks * 1.1f) / ice.Duration);
            ice.Cracks = Math.Max(ice.ExplosionCracks, Math.Min(3, (int)(ice.Melt * 4)));
            ice.LastAt = Time;
        }

        public void Step(float dt, bool melt = true)
        {
            dt = Math.Max(0, dt); _collapses.Clear();
            for (var i = _pulses.Count - 1; i >= 0; i--)
            {
                var pulse = _pulses[i]; pulse.Life -= dt;
                if (pulse.Life <= 0) _pulses.RemoveAt(i); else _pulses[i] = pulse;
            }
            if (!melt) return;
            Time += dt;
            for (var i = 0; i < _ice.Count; i++)
            {
                var ice = _ice[i]; ice.Flash = Math.Max(0, ice.Flash - dt);
                if (ice.Broken) continue;
                Age(ice, dt);
                if (!ice.Broken) continue;
                var pulse = new EonSeaPulse { X = ice.X, Y = ice.Y, Radius = 140 + ice.Radius, Life = EonSeaPulse.Duration, Variant = ice.Variant };
                if (_pulses.Count == MaxIce) _pulses.RemoveAt(0);
                _pulses.Add(pulse); _collapses.Add(pulse);
            }
        }

        public void Explode(float x, float y, float radius)
        {
            foreach (var ice in _ice)
            {
                if (ice.Broken || !Inside(ice, x, y, radius)) continue;
                ice.ExplosionCracks = Math.Min(3, ice.ExplosionCracks + 1);
                ice.Cracks = Math.Max(ice.Cracks, ice.ExplosionCracks); ice.Flash = 0.5f;
            }
        }

        public static void Nearest(EonSeaIce ice, float x, float y, out float qx, out float qy)
        {
            var dx = (float)Math.Cos(ice.Angle) * ice.Length; var dy = (float)Math.Sin(ice.Angle) * ice.Length;
            var ax = ice.X - dx * 0.5f; var ay = ice.Y - dy * 0.5f;
            var t = Math.Max(0f, Math.Min(1f, ((x - ax) * dx + (y - ay) * dy) / Math.Max(0.0001f, dx * dx + dy * dy)));
            qx = ax + dx * t; qy = ay + dy * t;
        }

        public static bool Inside(EonSeaIce ice, float x, float y, float radius)
        {
            Nearest(ice, x, y, out var qx, out var qy);
            var dx = x - qx; var dy = y - qy; var limit = ice.Radius + radius;
            return dx * dx + dy * dy < limit * limit;
        }

        public bool SlipperyAt(float x, float y)
        {
            foreach (var patch in _patches)
            {
                var dx = x - patch.X; var dy = y - patch.Y;
                var c = (float)Math.Cos(patch.Angle); var s = (float)Math.Sin(patch.Angle);
                var u = (dx * c + dy * s) / patch.RadiusX; var v = (-dx * s + dy * c) / patch.RadiusY;
                if (u * u + v * v < 1) return true;
            }
            return false;
        }

        public bool Move(ref float x, ref float y, float dx, float dy, float radius)
        {
            var steps = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(dx * dx + dy * dy) / 8));
            var blocked = false;
            for (var step = 0; step < steps; step++)
            {
                x += dx / steps; y += dy / steps;
                foreach (var ice in _ice)
                {
                    if (ice.Broken || Math.Abs(x - ice.X) > ice.Length + ice.Radius + radius || Math.Abs(y - ice.Y) > ice.Length + ice.Radius + radius) continue;
                    Nearest(ice, x, y, out var qx, out var qy);
                    var ox = x - qx; var oy = y - qy; var distance = (float)Math.Sqrt(ox * ox + oy * oy); var limit = ice.Radius + radius;
                    if (distance >= limit) continue;
                    blocked = true;
                    var nx = distance > 0.0001f ? ox / distance : -(float)Math.Sin(ice.Angle);
                    var ny = distance > 0.0001f ? oy / distance : (float)Math.Cos(ice.Angle);
                    x = qx + nx * (limit + 0.01f); y = qy + ny * (limit + 0.01f);
                }
            }
            return blocked;
        }

        public bool FirstHit(float ax, float ay, float bx, float by, float radius, out float hitX, out float hitY)
        {
            var dx = bx - ax; var dy = by - ay; var best = 2f;
            foreach (var ice in _ice)
            {
                if (ice.Broken) continue;
                var reach = ice.Length * 0.5f + ice.Radius + radius;
                if (Math.Min(ax, bx) > ice.X + reach || Math.Max(ax, bx) < ice.X - reach ||
                    Math.Min(ay, by) > ice.Y + reach || Math.Max(ay, by) < ice.Y - reach) continue;
                var c = (float)Math.Cos(ice.Angle); var s = (float)Math.Sin(ice.Angle);
                var x = (ax - ice.X) * c + (ay - ice.Y) * s;
                var y = -(ax - ice.X) * s + (ay - ice.Y) * c;
                var vx = dx * c + dy * s; var vy = -dx * s + dy * c;
                var half = ice.Length * 0.5f; var r = ice.Radius + radius;
                var enter = 0f; var leave = 1f;
                if (ClipSlab(x, vx, -half, half, ref enter, ref leave) &&
                    ClipSlab(y, vy, -r, r, ref enter, ref leave)) best = Math.Min(best, enter);
                best = Math.Min(best, CircleHit(x - half, y, vx, vy, r));
                best = Math.Min(best, CircleHit(x + half, y, vx, vy, r));
            }
            if (best > 1) { hitX = bx; hitY = by; return false; }
            hitX = ax + dx * best; hitY = ay + dy * best; return true;
        }

        private static bool ClipSlab(float start, float direction, float min, float max, ref float enter, ref float leave)
        {
            if (Math.Abs(direction) < 0.000001f) return start >= min && start <= max;
            var a = (min - start) / direction; var b = (max - start) / direction;
            enter = Math.Max(enter, Math.Min(a, b)); leave = Math.Min(leave, Math.Max(a, b));
            return enter <= leave;
        }

        private static float CircleHit(float x, float y, float dx, float dy, float radius)
        {
            var c = x * x + y * y - radius * radius;
            if (c <= 0) return 0;
            var a = dx * dx + dy * dy; if (a < 0.000001f) return 2;
            var b = x * dx + y * dy; var discriminant = b * b - a * c;
            if (discriminant < 0) return 2;
            var t = (-b - (float)Math.Sqrt(discriminant)) / a;
            return t >= 0 && t <= 1 ? t : 2;
        }
    }
}
