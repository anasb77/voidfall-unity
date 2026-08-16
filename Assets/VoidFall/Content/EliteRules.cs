using System;
using System.Collections.Generic;

namespace VoidFall.Core
{
    public enum EliteVariantId
    {
        Exploder,
        Mortar,
        Gunner,
    }

    public sealed class EliteVariantDefinition
    {
        public EliteVariantDefinition(
            EliteVariantId id,
            string baseId,
            string name,
            double healthMultiplier,
            double speedMultiplier,
            double sizeMultiplier,
            double damageMultiplier,
            double radiusMultiplier,
            double telegraphSeconds,
            double attackCooldownSeconds,
            double threatCost,
            int concurrentCap,
            double xp,
            double parts,
            double score,
            string accent)
        {
            Id = id;
            BaseId = baseId;
            Name = name;
            HealthMultiplier = healthMultiplier;
            SpeedMultiplier = speedMultiplier;
            SizeMultiplier = sizeMultiplier;
            DamageMultiplier = damageMultiplier;
            RadiusMultiplier = radiusMultiplier;
            TelegraphSeconds = telegraphSeconds;
            AttackCooldownSeconds = attackCooldownSeconds;
            ThreatCost = threatCost;
            ConcurrentCap = concurrentCap;
            Xp = xp;
            Parts = parts;
            Score = score;
            Accent = accent;
        }

        public EliteVariantId Id { get; }
        public string BaseId { get; }
        public string Name { get; }
        public double HealthMultiplier { get; }
        public double SpeedMultiplier { get; }
        public double SizeMultiplier { get; }
        public double DamageMultiplier { get; }
        public double RadiusMultiplier { get; }
        public double TelegraphSeconds { get; }
        public double AttackCooldownSeconds { get; }
        public double ThreatCost { get; }
        public int ConcurrentCap { get; }
        public double Xp { get; }
        public double Parts { get; }
        public double Score { get; }
        public string Accent { get; }
    }

    public readonly struct EliteVariantStats
    {
        public EliteVariantStats(
            double health,
            double speed,
            double radius,
            double contactDamage,
            double blastRadius,
            double telegraphSeconds,
            double attackCooldownSeconds)
        {
            Health = health;
            Speed = speed;
            Radius = radius;
            ContactDamage = contactDamage;
            BlastRadius = blastRadius;
            TelegraphSeconds = telegraphSeconds;
            AttackCooldownSeconds = attackCooldownSeconds;
        }

        public double Health { get; }
        public double Speed { get; }
        public double Radius { get; }
        public double ContactDamage { get; }
        public double BlastRadius { get; }
        public double TelegraphSeconds { get; }
        public double AttackCooldownSeconds { get; }
    }

    public readonly struct EliteVariantReward
    {
        public EliteVariantReward(int xp, int parts, int score)
        {
            Xp = xp;
            Parts = parts;
            Score = score;
        }

        public int Xp { get; }
        public int Parts { get; }
        public int Score { get; }
    }

    public sealed class EliteCadenceContext
    {
        public double ElapsedSeconds;
        public double PickRoll;
        public Dictionary<EliteVariantId, int> Active = new Dictionary<EliteVariantId, int>();
        public int ActiveTotal;
        public int ActiveCap;
        public double ThreatHeadroom;
        public double ReplacedThreatCost;
        public EliteVariantId[] AllowedKinds;
    }

    public static class EliteRules
    {
        public const double EliteCadenceStartSeconds = 150;
        public const double EliteCadenceMinIntervalSeconds = 45;
        public const double EliteCadenceMaxIntervalSeconds = 75;
        public const double EliteCadenceBlockRetrySeconds = 12;
        public const int EliteCadenceDefaultActiveCap = 2;
        public const int EliteCadenceSurgeActiveCap = 3;
        public const double SiegeMortarLockSeconds = 0.45;
        public const double SiegeMortarDriftRadius = 26;
        public const int CurvedVolleySize = 4;
        public const int CurvedVolleySlots = 5;
        public const double CurvedLateralAcceleration = 210;
        public const int MaxCurvedProjectiles = 24;

        public static readonly EliteVariantId[] EliteVariantOrder =
        {
            EliteVariantId.Exploder,
            EliteVariantId.Mortar,
            EliteVariantId.Gunner,
        };

        public static readonly Dictionary<EliteVariantId, EliteVariantDefinition> EliteVariants =
            new Dictionary<EliteVariantId, EliteVariantDefinition>
            {
                {
                    EliteVariantId.Exploder,
                    new EliteVariantDefinition(
                        EliteVariantId.Exploder, "exploder", "Elite Exploder",
                        1.6, 1.2, 1.35, 1.15, 1.25, 1.1, 0, 4.5, 2, 14, 4, 150, "#fb923c")
                },
                {
                    EliteVariantId.Mortar,
                    new EliteVariantDefinition(
                        EliteVariantId.Mortar, "mortar", "Siege Mortar",
                        1.4, 0.92, 1.25, 1.1, 1.3, 1.5, 6, 5.5, 2, 18, 5, 190, "#fbbf24")
                },
                {
                    EliteVariantId.Gunner,
                    new EliteVariantDefinition(
                        EliteVariantId.Gunner, "gunner", "Curved Gunner",
                        1.4, 1.05, 1.3, 1, 1, 0.7, 4, 4.2, 3, 16, 5, 175, "#f87171")
                },
            };

        public static EliteVariantDefinition EliteVariantDef(EliteVariantId id)
        {
            return EliteVariants[id];
        }

        public static EliteVariantStats EliteVariantStatsFor(EliteVariantId id)
        {
            var definition = EliteVariants[id];
            var baseDefinition = FindEnemy(definition.BaseId);
            var baseBlast = baseDefinition.BlastRadius ?? 0;
            return new EliteVariantStats(
                baseDefinition.Health * definition.HealthMultiplier,
                baseDefinition.Speed * definition.SpeedMultiplier,
                baseDefinition.Radius * definition.SizeMultiplier,
                baseDefinition.ContactDamage * definition.DamageMultiplier,
                baseBlast * definition.RadiusMultiplier,
                Math.Max(definition.TelegraphSeconds, baseDefinition.TelegraphSeconds ?? 0),
                Math.Max(definition.AttackCooldownSeconds, baseDefinition.AttackCooldown ?? 0));
        }

        public static double EliteCadenceIntervalSeconds(double roll, double multiplier = 1)
        {
            var boundedRoll = IsFinite(roll) ? Math.Min(Math.Max(roll, 0), 1) : 0;
            var frequency = IsFinite(multiplier) ? Math.Min(1.5, Math.Max(0.5, multiplier)) : 1;
            var baseInterval = EliteCadenceMinIntervalSeconds +
                (EliteCadenceMaxIntervalSeconds - EliteCadenceMinIntervalSeconds) * boundedRoll;
            return baseInterval / frequency;
        }

        public static int EliteCadenceActiveCap(double multiplier = 1)
        {
            return multiplier >= 1.5 ? EliteCadenceSurgeActiveCap : EliteCadenceDefaultActiveCap;
        }

        public static EliteVariantId[] UnlockedEliteVariants(double elapsedSeconds)
        {
            var time = IsFinite(elapsedSeconds) ? elapsedSeconds : 0;
            var unlocked = new List<EliteVariantId>();
            foreach (var id in EliteVariantOrder)
            {
                if (time >= FindEnemy(EliteVariants[id].BaseId).NaturalStartSeconds) unlocked.Add(id);
            }

            return unlocked.ToArray();
        }

        public static EliteVariantId? SelectEliteVariantForCadence(EliteCadenceContext context)
        {
            if (context == null || context.ElapsedSeconds < EliteCadenceStartSeconds) return null;
            if (context.ActiveTotal >= context.ActiveCap) return null;

            var unlocked = new List<EliteVariantId>();
            foreach (var id in UnlockedEliteVariants(context.ElapsedSeconds))
            {
                if (context.AllowedKinds != null && Array.IndexOf(context.AllowedKinds, id) < 0) continue;
                var live = context.Active != null && context.Active.ContainsKey(id) ? context.Active[id] : 0;
                if (live >= EliteVariants[id].ConcurrentCap) continue;
                var extra = EliteVariants[id].ThreatCost - context.ReplacedThreatCost;
                if (extra <= context.ThreatHeadroom) unlocked.Add(id);
            }

            if (unlocked.Count == 0) return null;
            var bounded = IsFinite(context.PickRoll)
                ? Math.Min(Math.Max(context.PickRoll, 0), 1 - 2.2204460492503131e-16)
                : 0;
            return unlocked[(int)Math.Floor(bounded * unlocked.Count)];
        }

        public static EliteVariantReward EliteVariantRewardFor(EliteVariantId id, double rewardMultiplier = 1)
        {
            var definition = EliteVariants[id];
            var scale = IsFinite(rewardMultiplier) ? Math.Max(1, rewardMultiplier) : 1;
            return new EliteVariantReward(
                SourceRound(definition.Xp * scale),
                SourceRound(definition.Parts * scale),
                SourceRound(definition.Score * scale));
        }

        private static int SourceRound(double value)
        {
            // Browser authority uses Math.round for non-negative rewards.
            return (int)Math.Floor(Math.Max(0d, value) + 0.5d);
        }

        public static int CurvedVolleyGapSlot(int volleyIndex)
        {
            var safeIndex = Math.Max(0, volleyIndex);
            return safeIndex % CurvedVolleySlots;
        }

        public static double[] CurvedVolleyCurvatures(int volleyIndex)
        {
            var gap = CurvedVolleyGapSlot(volleyIndex);
            var middle = (CurvedVolleySlots - 1) / 2.0;
            var curvatures = new List<double>(CurvedVolleySize);
            for (var slot = 0; slot < CurvedVolleySlots; slot++)
            {
                if (slot == gap) continue;
                curvatures.Add(slot - middle);
            }

            return curvatures.ToArray();
        }

        public static CombatVector CurvedProjectileAcceleration(double angle, double curvature)
        {
            var magnitude = curvature * CurvedLateralAcceleration;
            return new CombatVector(
                Math.Cos(angle + Math.PI / 2) * magnitude,
                Math.Sin(angle + Math.PI / 2) * magnitude);
        }

        public static CombatVector CurvedProjectilePosition(
            double originX,
            double originY,
            double angle,
            double curvature,
            double speed,
            double seconds)
        {
            var time = Math.Max(0, seconds);
            var along = speed * time;
            var across = 0.5 * curvature * CurvedLateralAcceleration * time * time;
            var cos = Math.Cos(angle);
            var sin = Math.Sin(angle);
            return new CombatVector(
                originX + cos * along - sin * across,
                originY + sin * along + cos * across);
        }

        public static bool SiegeMortarImpactLocked(double remainingSeconds)
        {
            return remainingSeconds <= SiegeMortarLockSeconds;
        }

        public static double SiegeMortarDrift(double remainingSeconds)
        {
            var telegraph = EliteVariantStatsFor(EliteVariantId.Mortar).TelegraphSeconds;
            var remaining = Math.Max(0, Math.Min(telegraph, remainingSeconds));
            if (remaining <= SiegeMortarLockSeconds) return 0;
            var span = Math.Max(0.0001, telegraph - SiegeMortarLockSeconds);
            var progress = (remaining - SiegeMortarLockSeconds) / span;
            return SiegeMortarDriftRadius * progress;
        }

        public static double EliteExploderFlashRate(double remainingSeconds, double telegraphSeconds = 1.1)
        {
            var span = Math.Max(0.0001, telegraphSeconds);
            var remaining = Math.Max(0, Math.Min(span, remainingSeconds));
            var progress = 1 - remaining / span;
            return 3.2 + progress * 8.4;
        }

        private static EnemyDefinition FindEnemy(string id)
        {
            foreach (var definition in ContentCatalog.Enemies)
            {
                if (definition.Id == id) return definition;
            }

            throw new InvalidOperationException("Missing enemy definition: " + id);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
