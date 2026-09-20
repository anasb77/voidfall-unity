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
        // Open-addressed world cells: storage is bounded by actor capacity,
        // not distance travelled. Exact keys prevent far-away cell aliasing.
        private readonly int[] _heads, _tails, _cellX, _cellY;
        private readonly int[] _next;
        private int _minX, _maxX, _minY, _maxY;

        public CollisionGrid(int capacity)
        {
            _next = new int[Math.Max(1, capacity)];
            var buckets = 1;
            while (buckets < _next.Length * 2) buckets <<= 1;
            _heads = new int[buckets]; _tails = new int[buckets];
            _cellX = new int[buckets]; _cellY = new int[buckets];
            Clear();
        }

        public void Clear()
        {
            _minX = _minY = int.MaxValue; _maxX = _maxY = int.MinValue;
            for (var index = 0; index < _heads.Length; index++)
            {
                _heads[index] = -1;
                _tails[index] = -1;
            }
        }

        public void Insert(int itemIndex, float x, float y)
        {
            if (itemIndex < 0 || itemIndex >= _next.Length) return;
            var cx = CellCoordinate(x); var cy = CellCoordinate(y);
            var cell = FindCell(cx, cy);
            _minX = Math.Min(_minX, cx); _maxX = Math.Max(_maxX, cx);
            _minY = Math.Min(_minY, cy); _maxY = Math.Max(_maxY, cy);
            _next[itemIndex] = -1;
            if (_heads[cell] < 0)
            {
                _cellX[cell] = cx; _cellY[cell] = cy;
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
            minX = Math.Max(_minX, minX);
            maxX = Math.Min(_maxX, maxX);
            minY = Math.Max(_minY, minY);
            maxY = Math.Min(_maxY, maxY);
            if (minX > maxX || minY > maxY) return 0;
            var count = 0;
            // Browser loops gx first, then gy. Preserve that order because
            // projectile/blade first-hit resolution is intentionally ordered.
            for (var cellX = minX; cellX <= maxX; cellX++)
            {
                for (var cellY = minY; cellY <= maxY; cellY++)
                {
                    var item = _heads[FindCell(cellX, cellY)];
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

        private int FindCell(int x, int y)
        {
            var bucket = (int)(unchecked((uint)x * 73856093u ^ (uint)y * 19349663u) & (uint)(_heads.Length - 1));
            while (_heads[bucket] >= 0 && (_cellX[bucket] != x || _cellY[bucket] != y))
                bucket = (bucket + 1) & (_heads.Length - 1);
            return bucket;
        }

        private static int CellCoordinate(float position)
        {
            return (int)Math.Floor(position / CellSize);
        }
    }
}
