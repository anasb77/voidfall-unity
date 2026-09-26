using System.Collections.Generic;
using UnityEngine;

namespace VoidFall.Runtime
{
    internal static partial class ProceduralSpriteFactory
    {
        // Uses the existing prepared sprite cache and teardown ownership.
        public static Sprite Scrap(int variant) => Pickup(variant == 1 ? "part-frame" : variant == 2 ? "part-cell" : "part");

        private static Sprite BuildScrap(int variant)
        {
            // Exact polygon coordinates from approved browser glyphs 02, 07, 10.
            // 42 source units preserves pickup scale and collection radius.
            var c = new RasterCanvas(18, 3, 84);
            var gold = ParseColor("#efbd65");
            var dark = ParseColor("#281f17");
            var bright = ParseColor("#ffedbd");
            c.Glow(16, gold, .1f);
            if (variant == 0)
            {
                ScrapPolygon(c, dark, gold, new[] { new Vector2(-8,-8),new Vector2(3,-9),new Vector2(6,3),new Vector2(-5,6) });
                ScrapPolygon(c, dark, gold, new[] { new Vector2(-1,-3),new Vector2(9,-5),new Vector2(10,8),new Vector2(0,10) });
                c.DrawLine(new Vector2(-8,-8),new Vector2(3,-9),.85f,bright);
            }
            else if (variant == 1)
            {
                ScrapPolygon(c, dark, gold, new[] { new Vector2(-10,-10),new Vector2(2,-10),new Vector2(2,-5),new Vector2(-5,-5),new Vector2(-5,6),new Vector2(6,6),new Vector2(6,0),new Vector2(11,0),new Vector2(11,11),new Vector2(-10,11) });
                ScrapPolygon(c, dark, gold, new[] { new Vector2(6,-12),new Vector2(12,-12),new Vector2(12,-5),new Vector2(7,-5) });
                c.DrawLine(new Vector2(-10,-10),new Vector2(2,-10),.85f,bright);
            }
            else
            {
                ScrapPolygon(c, dark, gold, new[] { new Vector2(0,-11),new Vector2(10,-5),new Vector2(10,5),new Vector2(4,9),new Vector2(0,5),new Vector2(-5,9),new Vector2(-10,4),new Vector2(-10,-5) });
                c.StrokePolygon(new[] { new Vector2(0,-5),new Vector2(4,-2),new Vector2(4,3),new Vector2(-2,5),new Vector2(-5,1),new Vector2(-4,-3) },gold,1.65f);
                c.DrawLine(new Vector2(0,-11),new Vector2(10,-5),.85f,bright);
            }
            return c.ToAtlasSprite("VoidFall_Scrap_" + variant);
        }

        private static void ScrapPolygon(RasterCanvas c, Color dark, Color gold, Vector2[] points)
        {
            c.FillPolygon(points, dark);
            c.StrokePolygon(points, new Color(gold.r, gold.g, gold.b, .18f), 3.2f);
            c.StrokePolygon(points, gold, 1.65f);
        }

#if UNITY_EDITOR
        public static ProceduralSpriteCatalog BuildScrapCatalogSnapshot()
        {
            var catalog = ScriptableObject.CreateInstance<ProceduralSpriteCatalog>();
            catalog.ReplaceEntries(new List<ProceduralSpriteCatalogEntry>
            {
                new ProceduralSpriteCatalogEntry("pickup|part", BuildScrap(0)),
                new ProceduralSpriteCatalogEntry("pickup|part-frame", BuildScrap(1)),
                new ProceduralSpriteCatalogEntry("pickup|part-cell", BuildScrap(2))
            });
            return catalog;
        }
#endif
    }
}
