using System;

namespace VoidFall.Core
{
    public enum FormationKind
    {
        WallSweep = 0,
        ExploderWall = 1,
        VeeWedge = 2,
        Column = 3,
        Phalanx = 4,
        ArcClose = 5
    }

    /// <summary>One formation member: a world position and the enemy to place there.</summary>
    public struct FormationSpawn
    {
        public double X;
        public double Y;
        public string EnemyId;
    }

    /// <summary>
    /// Director formations (the "fun shapes" layer over ambient spawning).
    /// Every formation is a pure function of (kind, elapsed, hash, player
    /// position, viewport) - no RNG streams are consumed, so a fixed seed
    /// reproduces the exact battlefield geometry. Members spawn off-screen
    /// and march in with their normal movement behavior; depth stacking
    /// replaces spawn timers, so the runtime materializes a whole formation
    /// in one tick with zero new director timers.
    ///
    /// ExploderWall is the signature VS-style moment: a solid line of
    /// exploders spanning the whole cross-axis, sweeping across the arena.
    /// It is gated until the roster has actually unlocked exploders.
    /// </summary>
    public static class FormationRules
    {
        public const double ExploderWallUnlockSeconds = 180;
        public const double OffscreenMargin = 90;
        public const double DepthSpacing = 60;

        public static int WallCount(double elapsedSeconds)
        {
            return Clamp(8 + (int)Math.Floor(Math.Max(0, elapsedSeconds) / 150) * 2, 8, 22);
        }

        public static int WedgeCount(double elapsedSeconds)
        {
            return Clamp(7 + (int)Math.Floor(Math.Max(0, elapsedSeconds) / 200) * 2, 7, 15);
        }

        public static int ColumnCount(double elapsedSeconds)
        {
            return Clamp(6 + (int)Math.Floor(Math.Max(0, elapsedSeconds) / 150), 6, 12);
        }

        public static int PhalanxColumns(double elapsedSeconds)
        {
            return Clamp(4 + (int)Math.Floor(Math.Max(0, elapsedSeconds) / 240), 4, 6);
        }

        public const int PhalanxRows = 3;

        public static int ArcCount(double elapsedSeconds)
        {
            return Clamp(9 + (int)Math.Floor(Math.Max(0, elapsedSeconds) / 120), 9, 16);
        }

        /// <summary>
        /// Deterministic kind pick. ExploderWall only rolls once unlocked;
        /// before that the same hash degrades to a plain wall.
        /// </summary>
        public static FormationKind PickKind(uint hash, double elapsedSeconds)
        {
            var kind = (FormationKind)(hash % 6);
            if (kind == FormationKind.ExploderWall && elapsedSeconds < ExploderWallUnlockSeconds)
                return FormationKind.WallSweep;
            return kind;
        }

        /// <summary>
        /// Composes the formation's spawn list. Directions: 0 sweeps in
        /// moving +X (spawned at the left), 1 -X (right), 2 +Y (bottom),
        /// 3 -Y (top). The cap trims symmetrically from the outermost
        /// members so walls keep their line even when the field is busy.
        /// </summary>
        public static FormationSpawn[] Compose(
            FormationKind kind,
            double elapsedSeconds,
            uint hash,
            double playerX,
            double playerY,
            double viewportHalfWidth,
            double viewportHalfHeight,
            int capacityCap)
        {
            var direction = (int)(hash % 4);
            var time = Math.Max(0, elapsedSeconds);
            var list = new System.Collections.Generic.List<FormationSpawn>();
            var halfWidth = Math.Max(80, viewportHalfWidth);
            var halfHeight = Math.Max(45, viewportHalfHeight);

            switch (kind)
            {
                case FormationKind.ExploderWall:
                case FormationKind.WallSweep:
                {
                    var id = kind == FormationKind.ExploderWall
                        ? "exploder"
                        : time >= 180 ? "runner" : "chaser";
                    var count = Math.Min(WallCount(time), capacityCap);
                    // A line perpendicular to the sweep, spanning past the
                    // visible cross-axis so there is no free corner to hide in.
                    var span = direction < 2
                        ? (halfHeight + OffscreenMargin) * 2
                        : (halfWidth + OffscreenMargin) * 2;
                    var spacing = count > 1 ? span / (count - 1) : 0;
                    for (var index = 0; index < count; index++)
                    {
                        var across = (index - (count - 1) / 2.0) * spacing;
                        AddWallSlot(list, id, direction, playerX, playerY,
                            halfWidth, halfHeight, across, 0);
                    }
                    break;
                }
                case FormationKind.VeeWedge:
                {
                    var id = "dasher";
                    var count = Math.Min(WedgeCount(time), capacityCap);
                    // Apex first, then widening arm pairs behind it: the V
                    // visibly points at the player as it closes.
                    var row = 0;
                    var placed = 0;
                    while (placed < count)
                    {
                        var depth = (direction < 2 ? halfWidth : halfHeight) +
                            OffscreenMargin + row * DepthSpacing;
                        var halfSpread = 40 + row * 55;
                        if (row == 0)
                        {
                            AddWallSlot(list, id, direction, playerX, playerY,
                                halfWidth, halfHeight, 0, depth);
                            placed++;
                        }
                        else
                        {
                            if (placed < count)
                            {
                                AddWallSlot(list, id, direction, playerX, playerY,
                                    halfWidth, halfHeight, -halfSpread, depth);
                                placed++;
                            }
                            if (placed < count)
                            {
                                AddWallSlot(list, id, direction, playerX, playerY,
                                    halfWidth, halfHeight, halfSpread, depth);
                                placed++;
                            }
                        }
                        row++;
                    }
                    break;
                }
                case FormationKind.Column:
                {
                    var id = "runner";
                    var count = Math.Min(ColumnCount(time), capacityCap);
                    for (var index = 0; index < count; index++)
                    {
                        AddWallSlot(list, id, direction, playerX, playerY,
                            halfWidth, halfHeight, 0, index * DepthSpacing);
                    }
                    break;
                }
                case FormationKind.Phalanx:
                {
                    var id = "guard";
                    var columns = Math.Min(PhalanxColumns(time), Math.Max(1, capacityCap / PhalanxRows));
                    for (var row = 0; row < PhalanxRows; row++)
                    {
                        for (var column = 0; column < columns; column++)
                        {
                            var across = (column - (columns - 1) / 2.0) * 52;
                            AddWallSlot(list, id, direction, playerX, playerY,
                                halfWidth, halfHeight, across, row * DepthSpacing);
                        }
                    }
                    break;
                }
                case FormationKind.ArcClose:
                {
                    var id = "gunner";
                    var count = Math.Min(ArcCount(time), capacityCap);
                    var radius = Math.Sqrt(halfWidth * halfWidth + halfHeight * halfHeight) + 25;
                    // A half-circle whose opening faces away from the player,
                    // so the arc closes like a jaw instead of a full ring.
                    var facing = FacingAngle(direction);
                    for (var index = 0; index < count; index++)
                    {
                        var angle = facing - Math.PI / 2 + Math.PI * ((index + 0.5) / count);
                        list.Add(new FormationSpawn
                        {
                            X = playerX + Math.Cos(angle) * radius,
                            Y = playerY + Math.Sin(angle) * radius,
                            EnemyId = id
                        });
                    }
                    break;
                }
            }

            return list.ToArray();
        }

        /// <summary>
        /// Places a slot for wall-family shapes: depth along the sweep
        /// direction (0 = the leading line), across on the perpendicular
        /// axis relative to the player.
        /// </summary>
        private static void AddWallSlot(
            System.Collections.Generic.List<FormationSpawn> list,
            string id,
            int direction,
            double playerX,
            double playerY,
            double halfWidth,
            double halfHeight,
            double across,
            double depth)
        {
            double x;
            double y;
            switch (direction)
            {
                case 0: // sweeps +X: spawned at the left
                    x = playerX - halfWidth - OffscreenMargin - depth;
                    y = playerY + across;
                    break;
                case 1: // sweeps -X: spawned at the right
                    x = playerX + halfWidth + OffscreenMargin + depth;
                    y = playerY + across;
                    break;
                case 2: // sweeps +Y: spawned at the bottom
                    x = playerX + across;
                    y = playerY - halfHeight - OffscreenMargin - depth;
                    break;
                default: // sweeps -Y: spawned at the top
                    x = playerX + across;
                    y = playerY + halfHeight + OffscreenMargin + depth;
                    break;
            }
            list.Add(new FormationSpawn { X = x, Y = y, EnemyId = id });
        }

        private static double FacingAngle(int direction)
        {
            switch (direction)
            {
                case 0: return 0;      // incoming from the left, facing +X
                case 1: return Math.PI;
                case 2: return Math.PI / 2; // from the bottom, facing +Y
                default: return -Math.PI / 2;
            }
        }

        private static int Clamp(int value, int min, int max)
        {
            return value < min ? min : value > max ? max : value;
        }
    }
}
