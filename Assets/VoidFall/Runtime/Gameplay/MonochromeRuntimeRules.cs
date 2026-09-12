using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public static class MonochromeRuntimeRules
    {
        public const int MaxQueenPromotions = 2;

        public static Vector2 ClampToBoard(Vector2 position, Vector2 origin, Vector2 size, float radius)
        {
            var insetX = Mathf.Clamp(radius, 0f, size.x * .5f);
            var insetY = Mathf.Clamp(radius, 0f, size.y * .5f);
            return new Vector2(Mathf.Clamp(position.x, origin.x + insetX, origin.x + size.x - insetX),
                Mathf.Clamp(position.y, origin.y + insetY, origin.y + size.y - insetY));
        }

        public static bool IsSplitCycle(string cycleId) => cycleId == "black-rule" || cycleId == "white-rule";

        public static float SplitSpawnX(CourtFaction faction, float centreX, float halfWidth) =>
            centreX + (faction == CourtFaction.White ? -Mathf.Abs(halfWidth) : Mathf.Abs(halfWidth));

        public static float HazardPulse(float elapsed, bool reducedMotion) =>
            reducedMotion ? .55f : .18f + .67f * (.5f + .5f * Mathf.Sin(elapsed * Mathf.PI * 10f));

        public static float SpawnX(CourtFaction faction, float centreX, float halfWidth) =>
            centreX + (faction == CourtFaction.Black ? -Mathf.Abs(halfWidth) : Mathf.Abs(halfWidth));

        public static string NextSpawnId(double roll)
        {
            var value = double.IsNaN(roll) || double.IsInfinity(roll)
                ? 0
                : System.Math.Max(0, System.Math.Min(1, roll));
            return value < 0.47 ? "court-pawn" :
                value < 0.68 ? "court-knight" :
                value < 0.84 ? "court-bishop" :
                value < 0.95 ? "court-rook" : "court-queen";
        }

        public static CourtFaction FactionAtWorldPosition(
            Vector2 position,
            Vector2 origin,
            Vector2 tileSize)
        {
            var width = Mathf.Max(1f, Mathf.Abs(tileSize.x));
            var height = Mathf.Max(1f, Mathf.Abs(tileSize.y));
            var column = Mathf.FloorToInt((position.x - origin.x) / width);
            var row = Mathf.FloorToInt((position.y - origin.y) / height);
            return ((column + row) & 1) == 0 ? CourtFaction.White : CourtFaction.Black;
        }

        public static bool ShouldApplyFloorDamage(
            CourtHazardState hazard,
            CourtFaction tileFaction,
            float cooldown) =>
            cooldown <= 0f && MonochromeEncounterRules.IsTileDangerous(hazard, tileFaction);

        public static Vector2 RookChargeVelocity(Vector2 direction, float baseSpeed)
        {
            var axis = Mathf.Abs(direction.x) >= Mathf.Abs(direction.y)
                ? new Vector2(Mathf.Sign(direction.x == 0 ? 1 : direction.x), 0)
                : new Vector2(0, Mathf.Sign(direction.y == 0 ? 1 : direction.y));
            return axis * Mathf.Max(0, baseSpeed) * 2.65f;
        }

        public static bool SuppressAmbientSpawns(string voidId, bool bossPhaseActive) =>
            bossPhaseActive && string.Equals(
                voidId,
                "monochrome-court",
                System.StringComparison.Ordinal);

        public static float ApplySharedDamage(float currentHealth, float damage) =>
            Mathf.Max(0, Mathf.Max(0, currentHealth) - Mathf.Max(0, damage));

        public static int PromotionsAfterCast(int activePromotions) =>
            Mathf.Clamp(activePromotions, 0, MaxQueenPromotions);
    }
}
