using UnityEngine;

namespace VoidFall.Runtime
{
    internal static partial class ProceduralSpriteFactory
    {
        // Appended prepared keys preserve all previously shipped catalog identities.
        // One unrotated sprite per rank; the renderer rotates the cached artwork.
        public static Sprite ApprovedProjectile(string id, int rank, bool evolved)
        {
            var rail = id == "railgun";
            rank = Mathf.Clamp(rank, 1, 6);
            var key = 49 + (rail ? 7 : 0) + (evolved ? 6 : rank - 1);
            if (ArsenalSprites.TryGetValue(key, out var sprite) && sprite != null) return sprite;
            if (evolved) rank = 6;
            var c = new RasterCanvas(rail ? 56 : 32, 0, rail ? 224 : 128);
            if (rail) DrawApprovedRail(c, rank, evolved);
            else DrawApprovedPulse(c, rank, evolved);
            var name = "VoidFall_Approved_" + id + "_" + rank + (evolved ? "_evolved" : "");
            sprite = c.ToSprite(name, true);
            sprite.name = name;
            ArsenalSprites[key] = sprite;
            return sprite;
        }

        private static void WarmApprovedProjectiles()
        {
            foreach (var id in new[] { "pistol", "railgun" })
            {
                for (var rank = 1; rank <= 6; rank++) ApprovedProjectile(id, rank, false);
                ApprovedProjectile(id, 6, true);
            }
        }

        private static void DrawApprovedPulse(RasterCanvas c, int rank, bool evolved)
        {
            var color = ParseColor("#58e8fa");
            var length = 10 + rank * 1.4f;
            c.Glow(25, color, .22f);
            c.FillPolygon(HydraEllipsePoints(Vector2.zero, length * .52f, 2.5f, 32), Color.white);
            if (rank == 1) c.StrokePolygon(ArsenalPoints(-11, 0, -5, -3, 8, -2, 12, 0, 8, 2, -5, 3), color, 1.3f);
            if (rank >= 2) c.StrokePolygon(HydraEllipsePoints(Vector2.zero, length, 4 + rank * .35f, 40), color, 1.5f);
            if (rank >= 3)
                for (var side = -1; side <= 1; side += 2)
                    ArsenalPlate(c, ArsenalPoints(-8, side * 5, -15, side * 8, -5, side * 6), ParseColor("#092c38"), color);
            if (rank >= 4) c.StrokePolygon(HydraEllipsePoints(new Vector2(2, 0), length * .72f, 3.6f, 40), ParseColor("#a2fbff"), 1);
            if (rank >= 5)
                for (var side = -1; side <= 1; side += 2)
                    ArsenalPlate(c, ArsenalPoints(2, side * 7, -6, side * 12, 10, side * 6), ParseColor("#09202b"), color);
            if (rank >= 6)
                for (var side = -1; side <= 1; side += 2)
                    c.DrawLine(new Vector2(-18, side * 6), new Vector2(-23, side * 2), 1.5f, ParseColor("#e8ffff"));
            if (evolved)
            {
                c.StrokeCircle(Vector2.zero, 14, new Color(color.r, color.g, color.b, .75f), 1);
                ArsenalPlate(c, ArsenalPoints(length + 4, 0, length, -4, length, 4), Color.white, Color.white);
            }
        }

        private static void DrawApprovedRail(RasterCanvas c, int rank, bool evolved)
        {
            var color = ParseColor("#bd99ff");
            var length = 28 + rank * 2;
            ArsenalPlate(c, ArsenalPoints(-length, -2, length - 7, -3, length, 0, length - 7, 3, -length, 2), ParseColor("#211b39"), color, 1.4f);
            c.DrawLine(new Vector2(-length + 10, 0), new Vector2(length - 3, 0), 1.5f, Color.white);
            if (rank >= 2)
                for (var side = -1; side <= 1; side += 2)
                    c.DrawLine(new Vector2(-length + 4, side * 5), new Vector2(length - 14, side * 5), 1, color);
            if (rank >= 3) ArsenalPlate(c, ArsenalPoints(-7, 0, 4, -6, 16, 0, 4, 6), ParseColor("#090b1b"), ParseColor("#e0c8ff"), 1.3f);
            if (rank >= 4)
                for (var i = 0; i < 3; i++) c.DrawLine(new Vector2(-30 + i * 8, -5), new Vector2(-34 + i * 8, 5), 1, color);
            if (rank >= 5)
                for (var side = -1; side <= 1; side += 2)
                    ArsenalPlate(c, ArsenalPoints(-26, side * 7, -38, side * 12, -12, side * 7), ParseColor("#131429"), color);
            if (rank >= 6)
                for (var side = -1; side <= 1; side += 2)
                    c.DrawLine(new Vector2(-23, side * 11), new Vector2(22, side * 7), 1, ParseColor("#d2bdff"));
            if (evolved)
            {
                c.StrokePolygon(ArsenalPoints(-48, 0, -27, -15, 12, -9, 43, 0, 12, 9, -27, 15), color, 1);
                c.DrawLine(new Vector2(-50, 0), new Vector2(44, 0), 2, Color.white);
            }
        }
    }
}
