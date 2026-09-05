using UnityEngine;
using UnityEngine.UI;
using VoidFall.Core;

namespace VoidFall.UI
{
    /// <summary>Resolution-independent annular segments. Geometry is rebuilt only when the wager changes.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RouletteWheelGraphic : MaskableGraphic
    {
        private RouletteWedgeDefinition[] _wedges;
        private RouletteSpinContext _context;
        private bool _frame;
        private int _selected = -1;
        public static readonly Color Gold = new Color(0.875f, 0.725f, 0.431f);

        public void Configure(RouletteWedgeDefinition[] wedges, RouletteSpinContext context, bool frame = false, int selected = -1)
        {
            _wedges = wedges;
            _context = context;
            _frame = frame;
            _selected = selected;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * 0.5f;
            if (radius <= 0) return;
            if (_frame)
            {
                Ring(mesh, radius * 0.88f, radius * 0.887f, 0, 360, Gold * new Color(1, 1, 1, 0.85f));
                Ring(mesh, radius * 0.96f, radius * 0.963f, 0, 360, Gold * new Color(1, 1, 1, 0.3f));
                for (var i = 0; i < 96; i++)
                    Ring(mesh, radius * 0.91f, radius * (i % 4 == 0 ? 0.95f : 0.925f), i * 3.75f, 0.35f, UITheme.WithAlpha(Gold, i % 4 == 0 ? 0.8f : 0.35f));
                Ring(mesh, 0, radius * 0.29f, 0, 360, new Color(0.018f, 0.024f, 0.045f));
                Ring(mesh, radius * 0.29f, radius * 0.296f, 0, 360, Gold);
                Ring(mesh, radius * 0.27f, radius * 0.273f, 0, 360, UITheme.WithAlpha(Gold, 0.25f));
                Triangle(mesh, new Vector2(-9, radius * 1.01f), new Vector2(9, radius * 1.01f), new Vector2(0, radius * 0.92f), new Color(1, 0.9f, 0.66f));
                return;
            }
            if (_wedges == null) return;
            for (var i = 0; i < _wedges.Length; i++)
            {
                var start = (float)RoulettePresentationRules.StartDegrees(_wedges, i, _context);
                var arc = (float)RoulettePresentationRules.Probability(_wedges, i, _context) * 360f;
                var accent = Accent(_wedges[i]);
                var gap = Mathf.Min(1.2f, arc * 0.1f);
                var selected = i == _selected;
                // Concentric bands give the dark metal a lit outer edge without a texture allocation.
                for (var band = 0; band < 12; band++)
                {
                    var inner = Mathf.Lerp(0.32f, 0.855f, band / 12f) * radius;
                    var outer = Mathf.Lerp(0.32f, 0.855f, (band + 1) / 12f) * radius;
                    var fill = Color.Lerp(new Color(0.045f, 0.055f, 0.085f), accent, (selected ? 0.22f : 0.04f) + band * 0.015f);
                    var outerFill = Color.Lerp(new Color(0.045f, 0.055f, 0.085f), accent, (selected ? 0.22f : 0.04f) + (band + 1) * 0.015f);
                    Ring(mesh, inner, outer, start + gap, arc - gap * 2, fill, outerFill);
                }
                Ring(mesh, radius * 0.85f, radius * 0.86f, start + gap, arc - gap * 2, UITheme.WithAlpha(accent, selected ? 1 : 0.6f));
                Ring(mesh, radius * 0.32f, radius * 0.86f, start + gap, 0.2f, UITheme.WithAlpha(accent, 0.5f));
            }
        }

        private static Vector2 Point(float degrees, float radius)
        {
            var angle = (90 - degrees) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        private static void Ring(VertexHelper mesh, float inner, float outer, float start, float arc, Color tint, Color? outerTint = null)
        {
            var steps = Mathf.Max(1, Mathf.CeilToInt(arc / 3));
            for (var i = 0; i < steps; i++)
            {
                var a = start + arc * i / steps;
                var b = start + arc * (i + 1) / steps;
                var offset = mesh.currentVertCount;
                mesh.AddVert(Point(a, inner), tint, Vector2.zero);
                mesh.AddVert(Point(a, outer), outerTint ?? tint, Vector2.zero);
                mesh.AddVert(Point(b, outer), outerTint ?? tint, Vector2.zero);
                mesh.AddVert(Point(b, inner), tint, Vector2.zero);
                mesh.AddTriangle(offset, offset + 1, offset + 2);
                mesh.AddTriangle(offset, offset + 2, offset + 3);
            }
        }

        private static void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color tint)
        {
            var offset = mesh.currentVertCount;
            mesh.AddVert(a, tint, Vector2.zero);
            mesh.AddVert(b, tint, Vector2.zero);
            mesh.AddVert(c, tint, Vector2.zero);
            mesh.AddTriangle(offset, offset + 1, offset + 2);
        }

        public static Color Accent(RouletteWedgeDefinition wedge)
        {
            switch (wedge.Tier)
            {
                case RouletteTier.Mediocre: return new Color(0.53f, 0.58f, 0.67f);
                case RouletteTier.Standard: return new Color(0.54f, 0.79f, 0.87f);
                case RouletteTier.Premium: return Gold;
                default: return wedge.Kind == RoulettePrizeKind.WildCard
                    ? new Color(0.94f, 0.64f, 0.47f) : new Color(0.78f, 0.64f, 0.98f);
            }
        }

        /// <summary>Owned by the runtime drop; caller destroys both sprite and texture on teardown.</summary>
        public static Sprite CreateRelicSprite()
        {
            const int size = 256;
            var table = RouletteRules.DefaultTable();
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f) / (size / 2f);
                var r = p.magnitude;
                var angle = Mathf.Repeat(90 - Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg, 360);
                var color = Color.clear;
                if (r < 0.29f) color = new Color(0.015f, 0.02f, 0.035f);
                else if (r < 0.305f || r > 0.88f && r < 0.91f || r > 0.95f && r < 0.963f) color = Gold;
                else if (r > 0.33f && r < 0.86f)
                {
                    for (var i = 0; i < table.Length; i++)
                    {
                        var start = (float)RoulettePresentationRules.StartDegrees(table, i, default);
                        var end = start + (float)RoulettePresentationRules.Probability(table, i, default) * 360;
                        if (angle < start + 1 || angle > end - 1) continue;
                        color = Color.Lerp(new Color(0.035f, 0.045f, 0.075f), Accent(table[i]), r > 0.83f ? 0.9f : 0.35f);
                        break;
                    }
                }
                pixels[y * size + x] = color;
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Void Roulette Relic", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
        }
    }
}
