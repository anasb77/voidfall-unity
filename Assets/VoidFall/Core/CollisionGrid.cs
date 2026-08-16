using System;

namespace VoidFall.Core
{
    /// <summary>
    /// Allocation-free uniform grid for broad-phase enemy queries. The browser
    /// engine uses a pooled spatial grid; this keeps the same bounded behavior
    /// for bullets, area damage, and separation while exact nearest-target
    /// scans remain in enemy-array order like the browser.
    /// </summary>
    public sealed class CollisionGrid
    {
        // Browser engine: const CELL = 72.
        public const int CellSize = 72;
        // Keep the fixed grid aligned to the browser's floor(position / CELL)
        // boundaries. The old -2048 origin was not a 72-unit boundary, so
        // entities near every cell edge could be assigned to different cells
        // than the source engine.
        private const int MinimumCell = -32;
        // Keep the fixed grid bounded while covering the full Unity arena and
        // the browser's off-screen spawn/recycle margin at the smaller cell size.
        private const int Width = 64;
        private const int Height = 64;

        private readonly int[] _heads = new int[Width * Height];
        private readonly int[] _tails = new int[Width * Height];
        private readonly int[] _next;

        public CollisionGrid(int capacity)
        {
            _next = new int[Math.Max(1, capacity)];
            Clear();
        }

        public void Clear()
        {
            for (var index = 0; index < _heads.Length; index++)
            {
                _heads[index] = -1;
                _tails[index] = -1;
            }
        }

        public void Insert(int itemIndex, float x, float y)
        {
            if (itemIndex < 0 || itemIndex >= _next.Length) return;
            var cell = CellIndex(x, y);
            _next[itemIndex] = -1;
            if (_heads[cell] < 0)
            {
                _heads[cell] = itemIndex;
                _tails[cell] = itemIndex;
            }
            else
            {
                _next[_tails[cell]] = itemIndex;
                _tails[cell] = itemIndex;
            }
        }

        public int Query(float x, float y, float radius, int[] output)
        {
            if (output == null || output.Length == 0) return 0;
            var safeRadius = Math.Max(0, radius);
            var minX = CellCoordinate(x - safeRadius);
            var maxX = CellCoordinate(x + safeRadius);
            var minY = CellCoordinate(y - safeRadius);
            var maxY = CellCoordinate(y + safeRadius);
            return QueryCells(minX, maxX, minY, maxY, output);
        }

        /// <summary>
        /// Queries the exact integer cell neighborhood used by the browser.
        /// For example, a browser loop of cellX - 1 through cellX + 1 maps to
        /// QueryNeighborhood(..., 1), independent of world-space padding.
        /// </summary>
        public int QueryNeighborhood(float x, float y, int cellRadius, int[] output)
        {
            if (output == null || output.Length == 0) return 0;
            var safeRadius = Math.Max(0, cellRadius);
            var centerX = CellCoordinate(x);
            var centerY = CellCoordinate(y);
            return QueryCells(
                centerX - safeRadius,
                centerX + safeRadius,
                centerY - safeRadius,
                centerY + safeRadius,
                output);
        }

        private int QueryCells(int minX, int maxX, int minY, int maxY, int[] output)
        {
            minX = Math.Max(0, minX);
            maxX = Math.Min(Width - 1, maxX);
            minY = Math.Max(0, minY);
            maxY = Math.Min(Height - 1, maxY);
            if (minX > maxX || minY > maxY) return 0;
            var count = 0;
            // Browser loops gx first, then gy. Preserve that order because
            // projectile/blade first-hit resolution is intentionally ordered.
            for (var cellX = minX; cellX <= maxX; cellX++)
            {
                for (var cellY = minY; cellY <= maxY; cellY++)
                {
                    var item = _heads[cellY * Width + cellX];
                    while (item >= 0)
                    {
                        if (count >= output.Length) return count;
                        output[count++] = item;
                        item = _next[item];
                    }
                }
            }
            return count;
        }

        private static int CellIndex(float x, float y)
        {
            return CellCoordinate(y) * Width + CellCoordinate(x);
        }

        private static int CellCoordinate(float position)
        {
            var coordinate = (int)Math.Floor(position / CellSize) - MinimumCell;
            return Math.Max(0, Math.Min(Width - 1, coordinate));
        }
    }
}
