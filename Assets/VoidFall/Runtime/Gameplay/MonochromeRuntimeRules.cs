using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public static class MonochromeRuntimeRules
    {
        public const int MaxQueenPromotions = 2;

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
