using System.Collections.Generic;
using UnityEngine;

namespace VoidFall.Runtime
{
    internal static partial class ProceduralSpriteFactory
    {
        private static readonly Dictionary<int, Sprite> ArsenalSprites = new Dictionary<int, Sprite>();

        public static Sprite ArsenalWeapon(string id, int rank, bool evolved)
        {
            rank = Mathf.Clamp(rank, 1, 6);
            // Integer keys keep the per-entity render lookup allocation-free.
            var weapon = id == "mines" ? 0 : id == "summons" ? 1 : id == "clock" ? 2 : 3;
            var key = weapon * 12 + (rank - 1) * 2 + (evolved ? 1 : 0);
            if (ArsenalSprites.TryGetValue(key, out var sprite) && sprite != null) return sprite;
            var extent = id == "clock" ? 150f : 24f;
            var canvas = new RasterCanvas(extent, 0, id == "clock" ? 384 : 96);
            if (id == "mines") DrawArsenalMine(canvas, rank, evolved);
            else if (id == "summons") DrawArsenalSummon(canvas, rank, evolved);
            else if (id == "clock") DrawArsenalClockHand(canvas, rank, evolved);
            else DrawArsenalBoomerang(canvas, rank);
            var name = "VoidFall_" + id + "_" + rank + (evolved ? "_evolved" : "");
            sprite = canvas.ToSprite(name, true);
            sprite.name = name;
            ArsenalSprites[key] = sprite;
            return sprite;
        }

        public static Sprite ArsenalClockFace()
        {
            const int key = 48;
            if (ArsenalSprites.TryGetValue(key, out var sprite) && sprite != null) return sprite;
            var c = new RasterCanvas(150, 0, 512);
            c.StrokeCircle(Vector2.zero, 137, new Color(.72f, .91f, .97f, .3f), 1.2f);
            c.StrokeCircle(Vector2.zero, 142, new Color(.72f, .91f, .97f, .105f), 1);
            for (var i = 0; i < 60; i++)
            {
                var a = i * Mathf.PI / 30;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var major = i % 5 == 0;
                c.DrawLine(dir * (major ? 123 : 129), dir * 132, major ? 1.8f : 1, new Color(.84f, .97f, 1, major ? .75f : .255f));
            }
            ClockNumeral(c, "XII", new Vector2(0, 107));
            ClockNumeral(c, "III", new Vector2(107, 0));
            ClockNumeral(c, "VI", new Vector2(0, -107));
            ClockNumeral(c, "IX", new Vector2(-107, 0));
            sprite = c.ToSprite("VoidFall_ClockFace", true);
            sprite.name = "VoidFall_ClockFace";
            ArsenalSprites[key] = sprite;
            return sprite;
        }

        private static void ClockNumeral(RasterCanvas c, string text, Vector2 center)
        {
            var color = new Color(.84f, .96f, 1, .85f);
            for (var i = 0; i < text.Length; i++)
            {
                var p = center + Vector2.right * ((i - (text.Length - 1) * .5f) * 6);
                if (text[i] == 'I')
                {
                    c.DrawLine(p + Vector2.down * 5, p + Vector2.up * 5, 1.3f, color);
                    c.DrawLine(p + new Vector2(-2, 5), p + new Vector2(2, 5), 1, color);
                    c.DrawLine(p + new Vector2(-2, -5), p + new Vector2(2, -5), 1, color);
                }
                else if (text[i] == 'V')
                {
                    c.DrawLine(p + new Vector2(-2.5f, 5), p + new Vector2(0, -5), 1.3f, color);
                    c.DrawLine(p + new Vector2(0, -5), p + new Vector2(2.5f, 5), 1.3f, color);
                }
                else
                {
                    c.DrawLine(p + new Vector2(-2.5f, 5), p + new Vector2(2.5f, -5), 1.3f, color);
                    c.DrawLine(p + new Vector2(-2.5f, -5), p + new Vector2(2.5f, 5), 1.3f, color);
                }
            }
        }

        private static Vector2[] ArsenalPoints(params float[] coordinates)
        {
            var points = new Vector2[coordinates.Length / 2];
            for (var i = 0; i < points.Length; i++) points[i] = new Vector2(coordinates[i * 2], coordinates[i * 2 + 1]);
            return points;
        }
        private static void ArsenalPlate(RasterCanvas c, Vector2[] points, Color fill, Color edge, float width = 1)
        {
            c.FillPolygon(points, fill);
            c.StrokePolygon(points, edge, width);
        }
        private static Vector2[] ArsenalHex(float radius)
        {
            var points = new Vector2[6];
            for (var i = 0; i < 6; i++) points[i] = new Vector2(Mathf.Cos(i * Mathf.PI / 3 + Mathf.PI / 6), Mathf.Sin(i * Mathf.PI / 3 + Mathf.PI / 6)) * radius;
            return points;
        }

        private static void DrawArsenalMine(RasterCanvas c, int rank, bool evolved)
        {
            var color = ParseColor(evolved ? "#8ceaff" : "#ffb75e");
            var fill = ParseColor("#27322e");
            c.Glow(23, color, .15f);
            if (rank >= 3)
            {
                var count = rank >= 4 ? 6 : 3;
                for (var i = 0; i < count; i++)
                {
                    c.SetRotation(i * Mathf.PI * 2 / count);
                    ArsenalPlate(c, ArsenalPoints(7, -2, 14, -2, 16, 0, 14, 2, 7, 2), fill, color, .9f);
                }
                c.SetRotation(0);
            }
            ArsenalPlate(c, ArsenalHex(10), fill, color, 1.3f);
            if (rank >= 2) c.StrokePolygon(ArsenalHex(6.5f), color, .8f);
            if (rank >= 5) { c.FillCircle(new Vector2(-2.7f, 0), 2, color); c.FillCircle(new Vector2(2.7f, 0), 2, color); }
            else c.FillCircle(Vector2.zero, 3, color);
            if (rank == 6)
            {
                for (var i = 0; i < 3; i++) { c.SetRotation(i * Mathf.PI * 2 / 3); ArsenalPlate(c, ArsenalPoints(-3, -10, 0, -15, 3, -10), ParseColor("#fff0d5"), color, .8f); }
                c.SetRotation(0);
            }
        }

        private static void DrawArsenalSummon(RasterCanvas c, int rank, bool evolved)
        {
            var green = ParseColor("#a7f3b5");
            c.Glow(18, green, .13f);
            if (rank >= 3)
                foreach (var side in new[] { -1, 1 }) ArsenalPlate(c, ArsenalPoints(-1, side * 3, -7, side * (rank >= 4 ? 8 : 6), -4, side), new Color(.32f, .71f, .52f, .53f), green, .7f);
            ArsenalPlate(c, ArsenalPoints(8, 0, -6, -5, -3, 0, -6, 5), green, ParseColor("#dcffe6"), .8f);
            c.DrawLine(new Vector2(-2, 0), new Vector2(3, 0), 2, ParseColor("#0b3329"));
            if (rank >= 2) ArsenalPlate(c, ArsenalPoints(5, 0, -1, -2, -2, 0, -1, 2), ParseColor("#163d32"), ParseColor("#e3ffed"), .6f);
            if (rank >= 5) { c.DrawLine(new Vector2(-7, -3), new Vector2(-10, -3), 1.5f, green); c.DrawLine(new Vector2(-7, 3), new Vector2(-10, 3), 1.5f, green); }
            if (rank == 6) c.FillPolygon(ArsenalPoints(1, -3, 5, -2, 8, 0, 5, 2, 1, 3, 3, 0), ParseColor("#f0fff3"));
            if (evolved) c.StrokeCircle(Vector2.zero, 11, new Color(.84f, .98f, .6f, .4f), .8f);
        }

        private static void DrawArsenalClockHand(RasterCanvas c, int rank, bool evolved)
        {
            var color = ParseColor(evolved ? "#99f6e4" : "#dcf8ff");
            ArsenalPlate(c, ArsenalPoints(-25, 0, -15, -7, 21, -3, 99, -5, 125, 0, 99, 5, 21, 3, -15, 7), new Color(.62f, .87f, .92f, .33f), color, 1.4f);
            c.FillPolygon(ArsenalPoints(-19, 0, -13, -3, 120, 0, 20, 2), color);
            c.FillCircle(new Vector2(-14, 0), 2.5f, ParseColor("#0e1b28"));
            if (rank >= 2) c.DrawLine(new Vector2(22, -1.5f), new Vector2(101, -1.5f), 1.1f, ParseColor("#157f98"));
            if (rank >= 3) ArsenalPlate(c, ArsenalPoints(32, 0, 49, -2.5f, 96, 0, 49, 2.5f), ParseColor("#113b4d"), color, .7f);
            if (rank >= 4)
                for (var i = 0; i < 3; i++) { var n = 53 + i * 15; c.DrawLine(new Vector2(n, -4), new Vector2(n + 7, 0), .9f, color); c.DrawLine(new Vector2(n, 4), new Vector2(n + 7, 0), .9f, color); }
            if (rank >= 5) ArsenalPlate(c, ArsenalPoints(23, 0, 30, -4, 37, 0, 30, 4), ParseColor(evolved ? "#2dd4bf" : "#22d3ee"), Color.white);
            if (rank == 6) { c.StrokePolygon(ArsenalPoints(38, -2, 49, -6, 61, -2), color, 1); c.StrokePolygon(ArsenalPoints(38, 2, 49, 6, 61, 2), color, 1); }
        }

        private static void DrawArsenalBoomerang(RasterCanvas c, int rank)
        {
            var color = ParseColor("#63dfff");
            c.Glow(21, color, .12f);
            ArsenalPlate(c, ArsenalPoints(-11, -8, -8, -8, 0, 1, 8, -8, 11, -8, 2, 6, -2, 6), ParseColor("#16475c"), color, 1.4f);
            if (rank >= 2) { c.DrawLine(new Vector2(-10, -7), new Vector2(0, 4), 1.4f, Color.white); c.DrawLine(new Vector2(0, 4), new Vector2(10, -7), 1.4f, Color.white); }
            if (rank >= 3) { c.FillPolygon(ArsenalPoints(-7, -3, -11, -1, -6, 3), color); c.FillPolygon(ArsenalPoints(7, -3, 11, -1, 6, 3), color); }
            if (rank >= 4) { c.DrawLine(new Vector2(-7, -4), new Vector2(0, 3), 1.2f, ParseColor("#0ea5c9")); c.DrawLine(new Vector2(0, 3), new Vector2(7, -4), 1.2f, ParseColor("#0ea5c9")); }
            if (rank >= 5) { ArsenalPlate(c, ArsenalPoints(-11, -8, -8, -12, -7, -5), color, Color.white, .6f); ArsenalPlate(c, ArsenalPoints(11, -8, 8, -12, 7, -5), color, Color.white, .6f); }
            if (rank == 6) ArsenalPlate(c, ArsenalPoints(0, -2, 3, 1, 0, 5, -3, 1), Color.white, color, .9f);
        }

        public static void DestroyArsenalSprites()
        {
            foreach (var sprite in ArsenalSprites.Values)
            {
                if (sprite == null) continue;
                var texture = sprite.texture;
                if (Application.isPlaying) { Object.Destroy(sprite); Object.Destroy(texture); }
                else { Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
            }
            ArsenalSprites.Clear();
        }
    }
}
