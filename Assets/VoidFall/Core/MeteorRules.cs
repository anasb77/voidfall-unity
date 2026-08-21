using System;

namespace VoidFall.Core
{
    public readonly struct CircleDefinition
    {
        public CircleDefinition(double x, double y, double radius)
        {
            X = x;
            Y = y;
            Radius = radius;
        }

        public double X { get; }
        public double Y { get; }
        public double Radius { get; }
    }

    public sealed class MeteorPlacementContext
    {
        public double PlayerX;
        public double PlayerY;
        public double PlayerRadius = MeteorRules.PlayerCollisionRadius;
        public CircleDefinition[] Enemies = new CircleDefinition[0];
        public CircleDefinition[] Meteors = new CircleDefinition[0];
        // -1 means use the full array length. Runtime spawning supplies counts
        // so its reusable max-capacity buffers do not scan unused entries.
        public int EnemyCount = -1;
        public int MeteorCount = -1;
    }

    public readonly struct MeteorPushResult
    {
        public MeteorPushResult(double pushX, double pushY, bool slow)
        {
            PushX = pushX;
            PushY = pushY;
            Slow = slow;
        }

        public double PushX { get; }
        public double PushY { get; }
        public bool Slow { get; }
    }

    public static class MeteorRules
    {
        public const double PlayerCollisionRadius = 15;
        public const double PlayerVisibleDiameter = 62;
        public const double MeteorCollisionRatio = 0.7;
        public const int MinOrdinaryMeteors = 3;
        public const int MaxOrdinaryMeteors = 5;
        public const int MaxExplosiveMeteors = 2;
        public const double MeteorPlayerClearance = 150;
        public const double MeteorEnemyClearance = 10;
        public const double MeteorSpacing = 58;
        public const int EscapeSamples = 12;
        public const double EscapeProbeDistance = 240;
        public const int MinOpenEscapeDirections = 5;
        public const double EscapeCorridorPadding = 12;
        public const double MaxMeteorPushPerStep = 2.4;
        public const double MeteorSlowFactor = 0.82;
        public const double ExplosiveFlashSeconds = 0.35;
        public const double ExplosiveChainDelayStepSeconds = 0.18;
        public const double ExplosiveBlastRadius = 128;
        public const double ExplosiveEnemyDamage = 180;
        public const double ExplosivePlayerDamageRatio = 0.34;
        public const int ExplosiveShardCount = 6;
        public const double ExplosiveShardSpeed = 220;
        public const double ExplosivePlayerDamageBase = 26;
        public const double ExplosivePlayerDamageCapUnits = 1.35;

        private static readonly int[] OrdinaryMeteorDiameters = { 48, 54, 58, 64 };
        private static readonly int[] ExplosiveMeteorDiameters = { 72, 80, 88 };

        public static int MeteorVisibleDiameter(int variant, bool explosive = false)
        {
            var table = explosive ? ExplosiveMeteorDiameters : OrdinaryMeteorDiameters;
            var index = Math.Abs((int)Math.Floor((double)variant)) % table.Length;
            return table[index];
        }

        public static double MeteorVisibleRadius(int variant, bool explosive = false)
        {
            return MeteorVisibleDiameter(variant, explosive) / 2.0;
        }

        public static double MeteorCollisionRadius(int variant, bool explosive = false)
        {
            return MeteorVisibleRadius(variant, explosive) * MeteorCollisionRatio;
        }

        public static int MeteorVariantCount(bool explosive = false)
        {
            return explosive ? ExplosiveMeteorDiameters.Length : OrdinaryMeteorDiameters.Length;
        }

        public static double MeteorRadius()
        {
            var total = 0.0;
            for (var index = 0; index < OrdinaryMeteorDiameters.Length; index++) total += MeteorCollisionRadius(index);
            return total / OrdinaryMeteorDiameters.Length;
        }

        public static double ExplosiveMeteorRadius()
        {
            var total = 0.0;
            for (var index = 0; index < ExplosiveMeteorDiameters.Length; index++) total += MeteorCollisionRadius(index, true);
            return total / ExplosiveMeteorDiameters.Length;
        }

        public static int MeteorMaxHealth(double elapsedSeconds)
        {
            var time = IsFinite(elapsedSeconds) ? Math.Max(0, elapsedSeconds) : 0;
            return (int)Math.Round(36 + time * 0.09, MidpointRounding.AwayFromZero);
        }

        public static int ExplosiveMeteorMaxHealth(double elapsedSeconds)
        {
            var time = IsFinite(elapsedSeconds) ? Math.Max(0, elapsedSeconds) : 0;
            return (int)Math.Round(28 + time * 0.06, MidpointRounding.AwayFromZero);
        }

        public static int ExplosivePlayerDamage(double enemyDamage = ExplosiveEnemyDamage, double damageMultiplier = 1)
        {
            var scale = IsFinite(damageMultiplier) ? Math.Max(1, damageMultiplier) : 1;
            var ceiling = ExplosivePlayerDamageBase * ExplosivePlayerDamageCapUnits * scale;
            return (int)Math.Round(
                Math.Min(ceiling, Math.Max(0, enemyDamage) * ExplosivePlayerDamageRatio),
                MidpointRounding.AwayFromZero);
        }

        public static int ExplosiveShardDamage(double elapsedSeconds, double damageMultiplier = 1)
        {
            var time = IsFinite(elapsedSeconds) ? Math.Max(0, elapsedSeconds) : 0;
            var scale = IsFinite(damageMultiplier) ? Math.Max(1, Math.Min(4.5, damageMultiplier)) : 1;
            var baseDamage = 9 + Math.Min(6, time / 300);
            return (int)Math.Round(baseDamage * Math.Sqrt(scale), MidpointRounding.AwayFromZero);
        }

        public static double ExplosiveChainDelaySeconds(int index)
        {
            var safeIndex = Math.Max(0, (int)Math.Floor((double)index));
            return ExplosiveFlashSeconds + safeIndex * ExplosiveChainDelayStepSeconds;
        }

        public static int OpenEscapeDirections(
            double playerX,
            double playerY,
            CircleDefinition[] meteors,
            double playerRadius = PlayerCollisionRadius,
            int samples = EscapeSamples,
            double probeDistance = EscapeProbeDistance)
        {
            return OpenEscapeDirections(
                playerX,
                playerY,
                meteors,
                meteors == null ? 0 : meteors.Length,
                playerRadius,
                samples,
                probeDistance);
        }

        public static int OpenEscapeDirections(
            double playerX,
            double playerY,
            CircleDefinition[] meteors,
            int meteorCount,
            double playerRadius = PlayerCollisionRadius,
            int samples = EscapeSamples,
            double probeDistance = EscapeProbeDistance)
        {
            var count = meteors == null
                ? 0
                : Math.Max(0, Math.Min(meteorCount, meteors.Length));
            var open = 0;
            for (var index = 0; index < samples; index++)
            {
                var angle = index / (double)samples * Math.PI * 2;
                var dirX = Math.Cos(angle);
                var dirY = Math.Sin(angle);
                var blocked = false;
                for (var meteorIndex = 0; meteorIndex < count; meteorIndex++)
                {
                    var meteor = meteors[meteorIndex];
                    if (RayHitsCircle(playerX, playerY, dirX, dirY, probeDistance, meteor,
                        playerRadius + EscapeCorridorPadding))
                    {
                        blocked = true;
                        break;
                    }
                }

                if (!blocked) open++;
            }

            return open;
        }

        public static bool IsSafeMeteorPlacement(CircleDefinition candidate, MeteorPlacementContext context)
        {
            return IsSafeMeteorPlacement(candidate, context, null);
        }

        public static bool IsSafeMeteorPlacement(
            CircleDefinition candidate,
            MeteorPlacementContext context,
            CircleDefinition[] projectedBuffer)
        {
            if (context == null) return false;
            var playerRadius = context.PlayerRadius > 0 ? context.PlayerRadius : PlayerCollisionRadius;
            if (!IsFinite(candidate.X) || !IsFinite(candidate.Y) || !(candidate.Radius > 0)) return false;
            var playerDx = candidate.X - context.PlayerX;
            var playerDy = candidate.Y - context.PlayerY;
            var playerMin = playerRadius + candidate.Radius + MeteorPlayerClearance;
            if (playerDx * playerDx + playerDy * playerDy < playerMin * playerMin) return false;

            var enemyCount = BoundedCount(context.Enemies, context.EnemyCount);
            for (var enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
            {
                var enemy = context.Enemies[enemyIndex];
                var dx = candidate.X - enemy.X;
                var dy = candidate.Y - enemy.Y;
                var min = enemy.Radius + candidate.Radius + MeteorEnemyClearance;
                if (dx * dx + dy * dy < min * min) return false;
            }

            var meteorCount = BoundedCount(context.Meteors, context.MeteorCount);
            for (var meteorIndex = 0; meteorIndex < meteorCount; meteorIndex++)
            {
                var meteor = context.Meteors[meteorIndex];
                var dx = candidate.X - meteor.X;
                var dy = candidate.Y - meteor.Y;
                var min = meteor.Radius + candidate.Radius + MeteorSpacing;
                if (dx * dx + dy * dy < min * min) return false;
            }

            var projected = projectedBuffer;
            if (projected == null || projected.Length < meteorCount + 1)
            {
                // Preserve public-call behavior for callers without a scratch
                // buffer; runtime spawning always supplies one.
                projected = new CircleDefinition[meteorCount + 1];
            }
            if (meteorCount > 0)
            {
                Array.Copy(context.Meteors, projected, meteorCount);
            }
            projected[meteorCount] = candidate;
            return OpenEscapeDirections(
                    context.PlayerX,
                    context.PlayerY,
                    projected,
                    meteorCount + 1,
                    playerRadius) >=
                MinOpenEscapeDirections;
        }

        private static int BoundedCount(CircleDefinition[] values, int requested)
        {
            if (values == null) return 0;
            return requested < 0
                ? values.Length
                : Math.Max(0, Math.Min(requested, values.Length));
        }

        public static MeteorPushResult ResolveMeteorPush(
            double playerX,
            double playerY,
            CircleDefinition meteor,
            double playerRadius = PlayerCollisionRadius)
        {
            var dx = playerX - meteor.X;
            var dy = playerY - meteor.Y;
            var overlapDistance = playerRadius + meteor.Radius;
            var distanceSq = dx * dx + dy * dy;
            if (!(distanceSq < overlapDistance * overlapDistance)) return new MeteorPushResult(0, 0, false);
            var distance = Math.Sqrt(distanceSq);
            var nx = distance > 0.0001 ? dx / distance : 1;
            var ny = distance > 0.0001 ? dy / distance : 0;
            var overlap = overlapDistance - distance;
            var push = Math.Min(MaxMeteorPushPerStep, Math.Max(0, overlap));
            return new MeteorPushResult(nx * push, ny * push, true);
        }

        private static bool RayHitsCircle(
            double originX,
            double originY,
            double dirX,
            double dirY,
            double distance,
            CircleDefinition circle,
            double padding)
        {
            var toX = circle.X - originX;
            var toY = circle.Y - originY;
            var along = toX * dirX + toY * dirY;
            if (along < -circle.Radius || along > distance + circle.Radius) return false;
            var clamped = Math.Max(0, Math.Min(distance, along));
            var closestX = originX + dirX * clamped;
            var closestY = originY + dirY * clamped;
            var dx = circle.X - closestX;
            var dy = circle.Y - closestY;
            var reach = circle.Radius + padding;
            return dx * dx + dy * dy <= reach * reach;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
