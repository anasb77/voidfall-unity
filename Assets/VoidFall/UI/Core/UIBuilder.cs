using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VoidFall.UI
{
    /// <summary>
    /// Records the emulated letter spacing a label was built with, so its text
    /// can be rewritten later without losing the effect.
    /// </summary>
    public sealed class UITracking : MonoBehaviour
    {
        public float Amount;
    }

    /// <summary>
    /// Bakes the sprites the menus are drawn with.
    ///
    /// The browser build gets rounded corners, gradients, hairline borders and
    /// glows straight from CSS. uGUI has no equivalent, so each of those becomes
    /// a small texture baked once and stretched with a nine-slice. This mirrors
    /// what the runtime's IMGUI path already did (RoundedGradientGuiTexture), so
    /// the two look identical during the migration.
    ///
    /// Everything is cached by its parameters: a panel style costs one texture no
    /// matter how many panels use it.
    /// </summary>
    public static class UISprites
    {
        private readonly struct RoundedKey : IEquatable<RoundedKey>
        {
            public readonly float Radius;
            public readonly Color First;
            public readonly Color Second;
            public readonly Color Border;
            public readonly float BorderWidth;
            public readonly float Angle;
            public readonly bool TopHighlight;

            public RoundedKey(
                float radius,
                Color first,
                Color second,
                Color border,
                float borderWidth,
                float angle,
                bool topHighlight)
            {
                Radius = radius;
                First = first;
                Second = second;
                Border = border;
                BorderWidth = borderWidth;
                Angle = angle;
                TopHighlight = topHighlight;
            }

            public bool Equals(RoundedKey other)
            {
                return Radius == other.Radius &&
                    ColorsEqual(First, other.First) &&
                    ColorsEqual(Second, other.Second) &&
                    ColorsEqual(Border, other.Border) &&
                    BorderWidth == other.BorderWidth &&
                    Angle == other.Angle &&
                    TopHighlight == other.TopHighlight;
            }

            private static bool ColorsEqual(Color left, Color right)
            {
                return left.r == right.r &&
                    left.g == right.g &&
                    left.b == right.b &&
                    left.a == right.a;
            }

            public override bool Equals(object obj) => obj is RoundedKey key && Equals(key);

            public override int GetHashCode()
            {
                var hash = Radius.GetHashCode();
                hash = (hash * 397) ^ First.GetHashCode();
                hash = (hash * 397) ^ Second.GetHashCode();
                hash = (hash * 397) ^ Border.GetHashCode();
                hash = (hash * 397) ^ BorderWidth.GetHashCode();
                hash = (hash * 397) ^ Angle.GetHashCode();
                hash = (hash * 397) ^ TopHighlight.GetHashCode();
                return hash;
            }
        }

        private static readonly Dictionary<RoundedKey, Sprite> RoundedCache =
            new Dictionary<RoundedKey, Sprite>();

        private static readonly Dictionary<int, Sprite> ShadowCache = new Dictionary<int, Sprite>();
        private static readonly Dictionary<int, Sprite> GlowCache = new Dictionary<int, Sprite>();
        private static readonly Dictionary<int, Sprite> CircleCache = new Dictionary<int, Sprite>();

        /// <summary>
        /// A nine-sliced rounded rectangle with an angled two-stop gradient and a
        /// hairline border, which together cover almost every surface in the
        /// stylesheet.
        /// </summary>
        public static Sprite Rounded(
            float radius,
            Color first,
            Color second,
            Color border,
            float borderWidth = 1f,
            float angle = UITheme.PanelGradientAngle,
            bool topHighlight = false)
        {
            var key = new RoundedKey(radius, first, second, border, borderWidth, angle, topHighlight);
            if (RoundedCache.TryGetValue(key, out var cached) && cached != null) return cached;

            // A nine-slice only needs enough pixels to hold two corners plus a
            // one-pixel stretchable centre.
            var safeRadius = Mathf.Max(0f, radius);
            var size = Mathf.Max(8, Mathf.CeilToInt(safeRadius) * 2 + 4);
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "VoidFall UI Rounded",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color[size * size];
            var half = size * 0.5f;
            var inner = half - safeRadius;
            var radians = angle * Mathf.Deg2Rad;
            var dirX = Mathf.Sin(radians);
            var dirY = Mathf.Cos(radians);
            var lineLength = Mathf.Max(0.0001f, Mathf.Abs(size * dirX) + Mathf.Abs(size * dirY));
            var safeBorder = Mathf.Max(0.0001f, borderWidth);

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var px = x + 0.5f - half;
                    var py = y + 0.5f - half;

                    // Signed distance to the rounded rectangle: negative inside.
                    var qx = Mathf.Abs(px) - inner;
                    var qy = Mathf.Abs(py) - inner;
                    var outsideX = Mathf.Max(qx, 0f);
                    var outsideY = Mathf.Max(qy, 0f);
                    var distance = Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY) +
                        Mathf.Min(Mathf.Max(qx, qy), 0f) - safeRadius;

                    var coverage = Mathf.Clamp01(0.5f - distance);
                    if (coverage <= 0f)
                    {
                        pixels[y * size + x] = new Color(0f, 0f, 0f, 0f);
                        continue;
                    }

                    var t = Mathf.Clamp01((px * dirX + py * dirY) / lineLength + 0.5f);
                    var fill = Color.Lerp(first, second, t);

                    // Blend toward the border colour across the outermost band.
                    if (border.a > 0f)
                    {
                        var borderMix = Mathf.Clamp01((distance + safeBorder) / safeBorder);
                        fill = Color.Lerp(fill, border, borderMix);
                    }

                    // The stylesheet's inset 0 1px 0 rgba(255,255,255,0.05).
                    if (topHighlight && py > 0f)
                    {
                        var fromTop = half - py;
                        if (fromTop <= 2f)
                        {
                            var strength = Mathf.Clamp01(1f - Mathf.Abs(fromTop - 1.2f)) * 0.05f;
                            fill = Color.Lerp(fill, Color.white, strength);
                        }
                    }

                    fill.a *= coverage;
                    pixels[y * size + x] = fill;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);

            var sliceBorder = Mathf.Min(size * 0.5f - 1f, safeRadius + 1f);
            var sprite = Sprite.Create(
                texture,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(sliceBorder, sliceBorder, sliceBorder, sliceBorder));
            sprite.name = "VoidFall UI Rounded";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            RoundedCache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// A nine-sliced soft drop shadow, standing in for the stylesheet's
        /// 0 24px 80px rgba(0,0,0,0.6) panel shadow.
        /// </summary>
        public static Sprite Shadow(int spread)
        {
            spread = Mathf.Clamp(spread, 4, 96);
            if (ShadowCache.TryGetValue(spread, out var cached) && cached != null) return cached;

            var radius = UITheme.RadiusPanel;
            var size = Mathf.CeilToInt(radius) * 2 + spread * 2 + 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "VoidFall UI Shadow",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color[size * size];
            var half = size * 0.5f;
            var inner = half - spread - radius;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var px = x + 0.5f - half;
                    var py = y + 0.5f - half;
                    var qx = Mathf.Abs(px) - inner;
                    var qy = Mathf.Abs(py) - inner;
                    var outsideX = Mathf.Max(qx, 0f);
                    var outsideY = Mathf.Max(qy, 0f);
                    var distance = Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY) +
                        Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;

                    var falloff = 1f - Mathf.Clamp01(distance / spread);
                    // Squaring keeps the core dense and the edge soft, which is
                    // closer to a gaussian than a linear ramp.
                    var alpha = falloff * falloff;
                    pixels[y * size + x] = new Color(0f, 0f, 0f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);

            var sliceBorder = Mathf.Min(size * 0.5f - 1f, radius + spread);
            var sprite = Sprite.Create(
                texture,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(sliceBorder, sliceBorder, sliceBorder, sliceBorder));
            sprite.name = "VoidFall UI Shadow";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            ShadowCache[spread] = sprite;
            return sprite;
        }

        /// <summary>
        /// A radial glow, used where the stylesheet reaches for box-shadow or
        /// text-shadow bloom.
        /// </summary>
        public static Sprite Glow(int size = 128)
        {
            size = Mathf.Clamp(size, 16, 256);
            if (GlowCache.TryGetValue(size, out var cached) && cached != null) return cached;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "VoidFall UI Glow",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color[size * size];
            var half = size * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var px = (x + 0.5f - half) / half;
                    var py = (y + 0.5f - half) / half;
                    var radial = Mathf.Clamp01(1f - Mathf.Sqrt(px * px + py * py));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, radial * radial);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);

            var sprite = Sprite.Create(
                texture,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            sprite.name = "VoidFall UI Glow";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            GlowCache[size] = sprite;
            return sprite;
        }

        /// <summary>
        /// A filled circle with a hairline ring, for the upgrade card icon frames
        /// (border-radius: 50%). Not nine-sliced: circles cannot be stretched
        /// without distorting, so callers keep it square.
        /// </summary>
        public static Sprite Circle(int size = 64)
        {
            size = Mathf.Clamp(size, 8, 256);
            if (CircleCache.TryGetValue(size, out var cached) && cached != null) return cached;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "VoidFall UI Circle",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color[size * size];
            var half = size * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var px = x + 0.5f - half;
                    var py = y + 0.5f - half;
                    var distance = Mathf.Sqrt(px * px + py * py) - (half - 1f);
                    var coverage = Mathf.Clamp01(0.5f - distance);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, coverage);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);

            var sprite = Sprite.Create(
                texture,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            sprite.name = "VoidFall UI Circle";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            CircleCache[size] = sprite;
            return sprite;
        }

        /// <summary>A 1x1 white sprite for flat fills, rules and bars.</summary>
        public static Sprite Solid => UITheme.PixelSprite;

        /// <summary>The canonical panel surface from .menu-panel / .overlay-card.</summary>
        public static Sprite Panel()
        {
            return Rounded(
                UITheme.RadiusPanel,
                UITheme.PanelTop,
                UITheme.PanelBottom,
                UITheme.BorderPanel,
                1f,
                UITheme.PanelGradientAngle,
                true);
        }
    }

    /// <summary>
    /// Constructs the widget vocabulary the views assemble screens from. Keeping
    /// every surface, button and row in one place is what stops the menus from
    /// drifting apart visually the way a per-view approach would.
    /// </summary>
    public static class UIBuilder
    {
        // ------------------------------------------------------------------
        // Structure
        // ------------------------------------------------------------------

        /// <summary>
        /// Creates an overlay canvas scaled against the stylesheet's reference
        /// size, and guarantees an EventSystem exists so clicks land.
        /// </summary>
        public static Canvas CreateCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(UITheme.ReferenceWidth, UITheme.ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            // Matching on height keeps one CSS pixel equal to one reference pixel
            // regardless of aspect ratio, which is what the ported sizes assume.
            scaler.matchWidthOrHeight = 1f;

            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return canvas;
        }

        /// <summary>
        /// The runtime's HUD setup also creates an EventSystem. Checking for any
        /// existing instance rather than EventSystem.current avoids a second one
        /// appearing when both run inside the same Awake, before OnEnable has set
        /// current.
        /// </summary>
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
#if UNITY_2023_1_OR_NEWER
            var existing = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
#else
            var existing = UnityEngine.Object.FindObjectOfType<EventSystem>();
#endif
            if (existing != null) return;

            var go = new GameObject("VoidFall UI EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        public static RectTransform CreateRect(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.layer = parent != null ? parent.gameObject.layer : 0;
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            // A RectTransform added from script does not arrive with the anchors
            // the editor would give it, so pin them explicitly. Centre-anchored
            // with a zero size is the least surprising default: anything that
            // needs to stretch or corner-anchor says so at its call site.
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        /// <summary>Anchors a rect to fill its parent, with optional padding.</summary>
        public static RectTransform Stretch(RectTransform rt, float padding = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
            return rt;
        }

        /// <summary>Anchors a rect to fill its parent with per-edge padding.</summary>
        public static RectTransform Stretch(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>A full-screen flat fill, used for scrims.</summary>
        public static Image CreateScrim(Transform parent, string name, Color color, bool blockRaycasts = true)
        {
            var rt = Stretch(CreateRect(parent, name));
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = UISprites.Solid;
            image.color = color;
            image.raycastTarget = blockRaycasts;
            return image;
        }

        /// <summary>A flat rectangle, for rules, bars and plates.</summary>
        public static Image CreateFill(Transform parent, string name, Color color)
        {
            var rt = CreateRect(parent, name);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = UISprites.Solid;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>A nine-sliced surface driven by one of the baked sprites.</summary>
        public static Image CreateSurface(Transform parent, string name, Sprite sprite, bool raycast = false)
        {
            var rt = CreateRect(parent, name);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            image.raycastTarget = raycast;
            return image;
        }

        /// <summary>
        /// The canonical panel: soft shadow, gradient body with a hairline cyan
        /// border and top highlight, and the signature short accent tick inset
        /// from the left edge (.menu-panel::before).
        /// </summary>
        public static RectTransform CreatePanel(
            Transform parent,
            string name,
            Vector2 size,
            bool accentTick = true,
            Sprite surface = null)
        {
            var root = CreateRect(parent, name);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = size;

            var shadow = CreateSurface(root, "Shadow", UISprites.Shadow(40));
            Stretch(shadow.rectTransform, -18f);
            shadow.color = new Color(0f, 0f, 0f, 0.6f);
            shadow.rectTransform.SetAsFirstSibling();

            var body = CreateSurface(root, "Body", surface ?? UISprites.Panel(), true);
            Stretch(body.rectTransform);

            if (accentTick)
            {
                var tick = CreateFill(body.rectTransform, "AccentTick", UITheme.Cyan);
                tick.rectTransform.anchorMin = new Vector2(0.07f, 1f);
                tick.rectTransform.anchorMax = new Vector2(0.44f, 1f);
                tick.rectTransform.pivot = new Vector2(0.5f, 1f);
                tick.rectTransform.sizeDelta = new Vector2(0f, 2f);
                tick.rectTransform.anchoredPosition = Vector2.zero;

                // 0 0 18px rgba(34, 211, 238, 0.36)
                var bloom = CreateSurface(tick.rectTransform, "Bloom", UISprites.Glow(64));
                bloom.type = Image.Type.Simple;
                Stretch(bloom.rectTransform, -14f);
                bloom.color = UITheme.WithAlpha(UITheme.Cyan, 0.36f);
                bloom.rectTransform.SetAsFirstSibling();
            }

            return root;
        }

        /// <summary>A one pixel horizontal rule, as used under panel headers.</summary>
        public static Image CreateRule(Transform parent, string name, Color color)
        {
            var rule = CreateFill(parent, name, color);
            rule.rectTransform.anchorMin = new Vector2(0f, 0f);
            rule.rectTransform.anchorMax = new Vector2(1f, 0f);
            rule.rectTransform.pivot = new Vector2(0.5f, 0f);
            rule.rectTransform.sizeDelta = new Vector2(0f, 1f);
            return rule;
        }

        // ------------------------------------------------------------------
        // Type
        // ------------------------------------------------------------------

        /// <summary>
        /// Text sized in CSS pixels. Sizes are passed through
        /// <see cref="UITheme.FontSize"/> so the stylesheet's 1.15 scale is applied
        /// exactly once, here.
        ///
        /// Legacy Text does not expose true letter spacing. The spacing argument
        /// is retained for callers and UITracking metadata, but the text is kept
        /// literal so fallback fonts do not receive synthetic Unicode spaces.
        /// </summary>
        public static Text CreateText(
            Transform parent,
            string name,
            string text,
            float cssSize,
            Color color,
            TextAnchor alignment = TextAnchor.MiddleLeft,
            bool display = true,
            FontStyle style = FontStyle.Normal,
            float letterSpacing = 0f)
        {
            var rt = Stretch(CreateRect(parent, name));
            var label = rt.gameObject.AddComponent<Text>();

            var font = display ? UITheme.DisplayFont : UITheme.BodyFont;
            if (font != null) label.font = font;

            label.text = UITheme.Track(text ?? string.Empty, letterSpacing);
            label.fontSize = Mathf.Max(1, Mathf.RoundToInt(UITheme.FontSize(cssSize)));
            label.color = color;
            label.alignment = alignment;
            label.fontStyle = style;
            label.raycastTarget = false;
            label.supportRichText = true;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;

            if (letterSpacing >= 0.1f)
            {
                // Remembered so SetText can reproduce the spacing on updates.
                var tracker = rt.gameObject.AddComponent<UITracking>();
                tracker.Amount = letterSpacing;
            }
            return label;
        }

        /// <summary>
        /// Replaces a label's text while preserving the literal text behavior
        /// used by the legacy UI.Text implementation.
        /// </summary>
        public static void SetText(Text label, string value)
        {
            if (label == null) return;
            var tracker = label.GetComponent<UITracking>();
            label.text = tracker != null
                ? UITheme.Track(value ?? string.Empty, tracker.Amount)
                : value ?? string.Empty;
        }

        /// <summary>Wrapping body copy, for descriptions and intros.</summary>
        public static Text CreateParagraph(
            Transform parent,
            string name,
            string text,
            float cssSize,
            Color color,
            TextAnchor alignment = TextAnchor.UpperLeft)
        {
            var label = CreateText(parent, name, text, cssSize, color, alignment, false);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.lineSpacing = 1.15f;
            return label;
        }

        /// <summary>
        /// The uppercase wide-tracked kicker that sits above every heading
        /// (.menu-panel header p, .overlay-card > p).
        /// </summary>
        public static Text CreateKicker(
            Transform parent,
            string name,
            string text,
            Color color,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            return CreateText(
                parent,
                name,
                (text ?? string.Empty).ToUpperInvariant(),
                10f,
                color,
                alignment,
                true,
                FontStyle.Bold,
                0.18f);
        }

        /// <summary>A panel or overlay heading (h2).</summary>
        public static Text CreateHeading(
            Transform parent,
            string name,
            string text,
            TextAnchor alignment = TextAnchor.MiddleLeft,
            float cssSize = 27f)
        {
            return CreateText(
                parent,
                name,
                text,
                cssSize,
                UITheme.TextHeading,
                alignment,
                true,
                FontStyle.Bold);
        }

        /// <summary>An uppercase section divider label (.section-label).</summary>
        public static Text CreateSectionLabel(Transform parent, string name, string text)
        {
            return CreateText(
                parent,
                name,
                (text ?? string.Empty).ToUpperInvariant(),
                11f,
                UITheme.TextSection,
                TextAnchor.MiddleLeft,
                true,
                FontStyle.Bold,
                0.12f);
        }

        /// <summary>
        /// Scales a label down to fit its rect, replacing TMP's auto-sizing.
        /// </summary>
        public static void FitText(Text label, float minCssSize, float maxCssSize)
        {
            if (label == null) return;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = Mathf.Max(1, Mathf.RoundToInt(UITheme.FontSize(minCssSize)));
            label.resizeTextMaxSize = Mathf.Max(2, Mathf.RoundToInt(UITheme.FontSize(maxCssSize)));
            label.verticalOverflow = VerticalWrapMode.Truncate;
        }

        // ------------------------------------------------------------------
        // Buttons
        // ------------------------------------------------------------------

        /// <summary>
        /// Wires a Button to swap between two baked sprites on hover, which keeps
        /// the stylesheet's exact hover fill and border rather than approximating
        /// them with a colour multiply.
        /// </summary>
        private static Button ConfigureSpriteSwap(
            GameObject go,
            Image image,
            Sprite normal,
            Sprite hover,
            Sprite pressed,
            Sprite disabled,
            Action onClick)
        {
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;

            var state = button.spriteState;
            state.highlightedSprite = hover;
            state.selectedSprite = hover;
            state.pressedSprite = pressed ?? hover;
            state.disabledSprite = disabled ?? normal;
            button.spriteState = state;

            image.sprite = normal;
            if (onClick != null) button.onClick.AddListener(() => onClick());
            return button;
        }

        /// <summary>
        /// The hero Start Run button: cyan gradient fill, bright border, and the
        /// neon-breathe glow pulse.
        /// </summary>
        public static Button CreateStartButton(
            Transform parent,
            string name,
            string label,
            Action onClick,
            Vector2 size)
        {
            var rt = CreateRect(parent, name);
            rt.sizeDelta = size;

            var glow = CreateSurface(rt, "Breathe", UISprites.Glow(128));
            glow.type = Image.Type.Simple;
            Stretch(glow.rectTransform, -24f);
            glow.color = UITheme.WithAlpha(UITheme.Cyan, 0.25f);

            var image = CreateSurface(rt, "Body", null, true);
            Stretch(image.rectTransform);
            var normal = UISprites.Rounded(
                UITheme.RadiusSmall,
                UITheme.StartFillTop,
                UITheme.StartFillBottom,
                UITheme.BorderStart,
                1f,
                180f);
            var hover = UISprites.Rounded(
                UITheme.RadiusSmall,
                UITheme.StartFillTopHover,
                UITheme.StartFillBottomHover,
                UITheme.BorderStartHover,
                1f,
                180f);

            var button = ConfigureSpriteSwap(image.gameObject, image, normal, hover, hover, normal, onClick);

            var text = CreateText(
                image.rectTransform,
                "Label",
                (label ?? string.Empty).ToUpperInvariant(),
                18f,
                UITheme.CyanBright,
                TextAnchor.MiddleCenter,
                true,
                FontStyle.Bold,
                0.14f);
            text.rectTransform.offsetMin = new Vector2(12f, 0f);
            text.rectTransform.offsetMax = new Vector2(-12f, 0f);

            var breathe = rt.gameObject.AddComponent<UIBreathe>();
            breathe.Bind(glow, 0.19f, 0.38f, UITheme.NeonBreatheSeconds);

            var press = image.gameObject.AddComponent<UIPressFeedback>();
            press.Bind(rt, 0.98f, new Vector2(0f, -1f), new Vector2(0f, 1f));
            return button;
        }

        /// <summary>
        /// A .primary-action button: label, optional sub-label, cyan border and
        /// inner glow. Used for Resume, Revive and Play again.
        /// </summary>
        public static Button CreatePrimaryAction(
            Transform parent,
            string name,
            string label,
            string detail,
            Action onClick,
            float height = 52f)
        {
            return CreateAction(
                parent,
                name,
                label,
                detail,
                onClick,
                height,
                UISprites.Rounded(UITheme.RadiusRow, UITheme.PrimaryFill, UITheme.PrimaryFill, UITheme.BorderPrimary),
                UISprites.Rounded(UITheme.RadiusRow, UITheme.PrimaryFillHover, UITheme.PrimaryFillHover, UITheme.CyanBright),
                UITheme.PrimaryLabel,
                true);
        }

        /// <summary>A .secondary-action button.</summary>
        public static Button CreateSecondaryAction(
            Transform parent,
            string name,
            string label,
            string detail,
            Action onClick,
            float height = 46f)
        {
            return CreateAction(
                parent,
                name,
                label,
                detail,
                onClick,
                height,
                UISprites.Rounded(UITheme.RadiusRow, UITheme.SecondaryFill, UITheme.SecondaryFill, UITheme.BorderSecondary),
                UISprites.Rounded(UITheme.RadiusRow, UITheme.SecondaryFillHover, UITheme.SecondaryFillHover, UITheme.WithAlpha(UITheme.CyanLight, 0.43f)),
                UITheme.TextStrong,
                false);
        }

        private static Button CreateAction(
            Transform parent,
            string name,
            string label,
            string detail,
            Action onClick,
            float height,
            Sprite normal,
            Sprite hover,
            Color labelColor,
            bool primary)
        {
            var rt = CreateRect(parent, name);
            rt.sizeDelta = new Vector2(0f, height);

            var image = rt.gameObject.AddComponent<Image>();
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;

            var disabled = UISprites.Rounded(
                UITheme.RadiusRow,
                UITheme.SecondaryFill,
                UITheme.SecondaryFill,
                UITheme.WithAlpha(UITheme.BorderSecondary, 0.12f));
            var button = ConfigureSpriteSwap(rt.gameObject, image, normal, hover, hover, disabled, onClick);

            var hasDetail = !string.IsNullOrEmpty(detail);
            var title = CreateText(
                rt,
                "Label",
                label,
                15f,
                labelColor,
                TextAnchor.MiddleCenter,
                true,
                FontStyle.Bold);

            if (hasDetail)
            {
                title.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                title.rectTransform.anchorMax = new Vector2(1f, 1f);
                title.rectTransform.offsetMin = new Vector2(14f, 0f);
                title.rectTransform.offsetMax = new Vector2(-14f, -8f);
                title.alignment = TextAnchor.LowerLeft;

                var sub = CreateText(
                    rt,
                    "Detail",
                    detail,
                    11f,
                    UITheme.TextActionDetail,
                    TextAnchor.UpperLeft,
                    false);
                sub.rectTransform.anchorMin = new Vector2(0f, 0f);
                sub.rectTransform.anchorMax = new Vector2(1f, 0.5f);
                sub.rectTransform.offsetMin = new Vector2(14f, 8f);
                sub.rectTransform.offsetMax = new Vector2(-14f, 0f);
            }
            else
            {
                title.rectTransform.offsetMin = new Vector2(14f, 0f);
                title.rectTransform.offsetMax = new Vector2(-14f, 0f);
            }

            if (primary)
            {
                var glow = CreateSurface(rt, "Glow", UISprites.Glow(96));
                glow.type = Image.Type.Simple;
                Stretch(glow.rectTransform, -14f);
                glow.color = UITheme.WithAlpha(UITheme.Cyan, 0.12f);
                glow.rectTransform.SetAsFirstSibling();
            }

            var press = rt.gameObject.AddComponent<UIPressFeedback>();
            press.Bind(rt, 0.988f, Vector2.zero, Vector2.zero);
            return button;
        }

        /// <summary>
        /// A .menu-grid nav card: title, detail and a spring hover lift. These are
        /// the Workshop / Records / Settings entries on the home screen.
        /// </summary>
        public static Button CreateNavCard(
            Transform parent,
            string name,
            string title,
            string detail,
            Action onClick,
            out Text detailLabel)
        {
            var rt = CreateRect(parent, name);

            var image = rt.gameObject.AddComponent<Image>();
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;

            var normal = UISprites.Rounded(
                UITheme.RadiusCard,
                UITheme.NavCardTop,
                UITheme.NavCardBottom,
                UITheme.BorderNavCard,
                1f,
                UITheme.PanelGradientAngle,
                true);
            var hover = UISprites.Rounded(
                UITheme.RadiusCard,
                UITheme.NavCardHoverTop,
                UITheme.NavCardHoverBottom,
                UITheme.BorderNavCardHover,
                1f,
                UITheme.PanelGradientAngle,
                true);
            var button = ConfigureSpriteSwap(rt.gameObject, image, normal, hover, hover, normal, onClick);

            var titleLabel = CreateText(
                rt,
                "Title",
                (title ?? string.Empty).ToUpperInvariant(),
                12f,
                UITheme.TextNav,
                TextAnchor.LowerLeft,
                true,
                FontStyle.Bold,
                0.09f);
            titleLabel.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            titleLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            titleLabel.rectTransform.offsetMin = new Vector2(14f, 0f);
            titleLabel.rectTransform.offsetMax = new Vector2(-12f, -12f);

            detailLabel = CreateText(
                rt,
                "Detail",
                detail,
                10f,
                UITheme.TextNavDetail,
                TextAnchor.UpperLeft,
                false);
            detailLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
            detailLabel.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            detailLabel.rectTransform.offsetMin = new Vector2(14f, 12f);
            detailLabel.rectTransform.offsetMax = new Vector2(-12f, 0f);

            var lift = rt.gameObject.AddComponent<UIHoverLift>();
            lift.Bind(rt, 3f);
            return button;
        }

        /// <summary>The panel header's Back button.</summary>
        public static Button CreateBackButton(Transform parent, string name, Action onClick)
        {
            var rt = CreateRect(parent, name);
            rt.sizeDelta = new Vector2(84f, 40f);

            var image = rt.gameObject.AddComponent<Image>();
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;

            var normal = UISprites.Rounded(UITheme.RadiusSmall, UITheme.BackFill, UITheme.BackFill, UITheme.BorderBack);
            var hover = UISprites.Rounded(UITheme.RadiusSmall, UITheme.RowFillHover, UITheme.RowFillHover, UITheme.BorderSelect);
            var button = ConfigureSpriteSwap(rt.gameObject, image, normal, hover, hover, normal, onClick);

            CreateText(
                rt,
                "Label",
                "\u2190  BACK",
                12f,
                UITheme.TextLabel,
                TextAnchor.MiddleCenter,
                true,
                FontStyle.Bold,
                0.06f);
            return button;
        }

        /// <summary>A square icon-only control, like the corner mute button.</summary>
        public static Button CreateIconButton(
            Transform parent,
            string name,
            string glyph,
            Action onClick,
            float size = 44f)
        {
            var rt = CreateRect(parent, name);
            rt.sizeDelta = new Vector2(size, size);

            var image = rt.gameObject.AddComponent<Image>();
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;

            var normal = UISprites.Rounded(UITheme.RadiusControl, UITheme.ControlFill, UITheme.ControlFill, UITheme.BorderControl);
            var hover = UISprites.Rounded(UITheme.RadiusControl, UITheme.ControlFill, UITheme.ControlFill, UITheme.WithAlpha(UITheme.CyanLight, 0.5f));
            var button = ConfigureSpriteSwap(rt.gameObject, image, normal, hover, hover, normal, onClick);

            var label = CreateText(rt, "Glyph", glyph, 17f, UITheme.TextChip, TextAnchor.MiddleCenter, true, FontStyle.Bold);
            label.name = "Glyph";

            var press = rt.gameObject.AddComponent<UIPressFeedback>();
            press.Bind(rt, 0.96f, Vector2.zero, Vector2.zero);
            return button;
        }

        /// <summary>
        /// A compact button carrying a cost, used by the workshop rows. Disabled
        /// styling matches .workshop-row > button:disabled.
        /// </summary>
        public static Button CreateBuyButton(
            Transform parent,
            string name,
            Action onClick,
            out Text label)
        {
            var rt = CreateRect(parent, name);
            rt.sizeDelta = new Vector2(76f, 38f);

            var image = rt.gameObject.AddComponent<Image>();
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;

            var normal = UISprites.Rounded(UITheme.RadiusSmall, UITheme.BuyFill, UITheme.BuyFill, UITheme.BorderBuy);
            var hover = UISprites.Rounded(UITheme.RadiusSmall, UITheme.RowFillActive, UITheme.RowFillActive, UITheme.CyanLight);
            var disabled = UISprites.Rounded(
                UITheme.RadiusSmall,
                UITheme.BuyFillDisabled,
                UITheme.BuyFillDisabled,
                UITheme.WithAlpha(UITheme.PipEmpty, 0.15f));

            var button = ConfigureSpriteSwap(rt.gameObject, image, normal, hover, hover, disabled, onClick);

            label = CreateText(
                rt,
                "Label",
                string.Empty,
                11f,
                UITheme.CyanPale,
                TextAnchor.MiddleCenter,
                true,
                FontStyle.Bold);
            return button;
        }

        /// <summary>The destructive full-width reset control in Settings.</summary>
        public static Button CreateDangerButton(
            Transform parent,
            string name,
            string label,
            Action onClick,
            out Text textLabel,
            out Image surface)
        {
            var rt = CreateRect(parent, name);
            rt.sizeDelta = new Vector2(0f, 42f);

            var image = rt.gameObject.AddComponent<Image>();
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;
            surface = image;

            var normal = UISprites.Rounded(UITheme.RadiusRow, UITheme.ResetFill, UITheme.ResetFill, UITheme.BorderReset);
            var hover = UISprites.Rounded(UITheme.RadiusRow, UITheme.ResetFillArmed, UITheme.ResetFillArmed, UITheme.WithAlpha(UITheme.Red, 0.6f));
            ConfigureSpriteSwap(rt.gameObject, image, normal, hover, hover, normal, onClick);

            textLabel = CreateText(
                rt,
                "Label",
                label,
                11f,
                UITheme.RoseLight,
                TextAnchor.MiddleCenter,
                false);
            return rt.GetComponent<Button>();
        }

        // ------------------------------------------------------------------
        // Composite widgets
        // ------------------------------------------------------------------

        /// <summary>
        /// A .metric tile: small uppercase label over a large value. Shared by the
        /// records grid and the run result grid.
        /// </summary>
        public static Text CreateMetricTile(Transform parent, string name, string label, string value)
        {
            var rt = CreateRect(parent, name);

            var body = CreateSurface(rt, "Body", UISprites.Rounded(
                UITheme.RadiusSmall,
                UITheme.RowFill,
                UITheme.RowFill,
                UITheme.BorderRow));
            Stretch(body.rectTransform);

            var caption = CreateText(
                rt,
                "Label",
                (label ?? string.Empty).ToUpperInvariant(),
                10f,
                UITheme.TextMetricLabel,
                TextAnchor.LowerLeft,
                false,
                FontStyle.Normal,
                0.09f);
            caption.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            caption.rectTransform.anchorMax = new Vector2(1f, 1f);
            caption.rectTransform.offsetMin = new Vector2(10f, 0f);
            caption.rectTransform.offsetMax = new Vector2(-10f, -10f);

            var readout = CreateText(
                rt,
                "Value",
                value,
                17f,
                UITheme.TextBody,
                TextAnchor.UpperLeft,
                true,
                FontStyle.Bold);
            readout.rectTransform.anchorMin = new Vector2(0f, 0f);
            readout.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            readout.rectTransform.offsetMin = new Vector2(10f, 10f);
            readout.rectTransform.offsetMax = new Vector2(-10f, 2f);
            return readout;
        }

        /// <summary>
        /// A row of rank pips (.rank-pips). Filled pips use the stylesheet's
        /// #38bdf8 unless an accent is supplied, matching .upgrade-ranks.
        /// </summary>
        public static Image[] CreateRankPips(
            Transform parent,
            string name,
            int maxRank,
            int filled,
            float pipWidth = 17f,
            Color? accent = null)
        {
            // The row fills its host so the layout group has real bounds to work
            // with; callers that want it centred re-anchor it afterwards.
            var row = Stretch(CreateRect(parent, name));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var safeMax = Mathf.Clamp(maxRank, 0, 24);
            var pips = new Image[safeMax];
            for (var index = 0; index < safeMax; index++)
            {
                var pip = CreateFill(row, "Pip" + index.ToString(), index < filled
                    ? (accent ?? UITheme.PipFilled)
                    : UITheme.PipEmpty);
                // A 3px tall bar cannot carry a nine-slice, so these stay flat.
                pip.rectTransform.sizeDelta = new Vector2(pipWidth, 3f);

                var element = pip.gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = pipWidth;
                element.preferredHeight = 3f;
                pips[index] = pip;
            }
            return pips;
        }

        /// <summary>
        /// A build/weapon chip (.build-chip): accent-tinted border with a rank
        /// badge, brightened when the weapon has evolved.
        /// </summary>
        public static RectTransform CreateChip(
            Transform parent,
            string name,
            string label,
            string rank,
            Color accent,
            bool evolved)
        {
            var rt = CreateRect(parent, name);
            rt.sizeDelta = new Vector2(0f, 28f);

            var border = evolved
                ? UITheme.MixTransparent(accent, 62f)
                : UITheme.MixTransparent(accent, 32f);
            var body = CreateSurface(rt, "Body", UISprites.Rounded(
                UITheme.RadiusChip,
                UITheme.ChipFill,
                UITheme.ChipFill,
                border));
            Stretch(body.rectTransform);

            if (evolved)
            {
                // 0 0 10px color-mix(accent 26%)
                var glow = CreateSurface(rt, "Glow", UISprites.Glow(64));
                glow.type = Image.Type.Simple;
                Stretch(glow.rectTransform, -8f);
                glow.color = UITheme.MixTransparent(accent, 26f);
                glow.rectTransform.SetAsFirstSibling();

                // inset 3px 0 0 accent
                var rail = CreateFill(body.rectTransform, "Rail", accent);
                rail.rectTransform.anchorMin = new Vector2(0f, 0f);
                rail.rectTransform.anchorMax = new Vector2(0f, 1f);
                rail.rectTransform.pivot = new Vector2(0f, 0.5f);
                rail.rectTransform.sizeDelta = new Vector2(3f, -6f);
                rail.rectTransform.anchoredPosition = new Vector2(1f, 0f);
            }

            var text = CreateText(
                body.rectTransform,
                "Label",
                label,
                10f,
                UITheme.TextChip,
                TextAnchor.MiddleLeft,
                false);
            text.rectTransform.offsetMin = new Vector2(8f, 0f);
            text.rectTransform.offsetMax = new Vector2(-26f, 0f);

            var badge = CreateSurface(body.rectTransform, "Badge", UISprites.Rounded(
                UITheme.RadiusPip,
                UITheme.MixTransparent(accent, 14f),
                UITheme.MixTransparent(accent, 14f),
                default(Color)));
            badge.rectTransform.anchorMin = new Vector2(1f, 0.5f);
            badge.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            badge.rectTransform.pivot = new Vector2(1f, 0.5f);
            badge.rectTransform.sizeDelta = new Vector2(16f, 15f);
            badge.rectTransform.anchoredPosition = new Vector2(-6f, 0f);

            CreateText(
                badge.rectTransform,
                "Rank",
                rank,
                9f,
                evolved ? UITheme.TextBrightest : accent,
                TextAnchor.MiddleCenter,
                true,
                FontStyle.Bold);

            var fitter = rt.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var element = rt.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 28f;
            element.preferredWidth = Mathf.Max(72f, UITheme.FontSize(10f) * 0.62f * (label?.Length ?? 0) + 46f);
            return rt;
        }

        /// <summary>A small uppercase status badge (.new-best, .save-warning).</summary>
        public static RectTransform CreateBadge(
            Transform parent,
            string name,
            string label,
            Color textColor,
            Color fill,
            Color border,
            bool pulse)
        {
            var rt = CreateRect(parent, name);
            rt.sizeDelta = new Vector2(0f, 28f);

            var body = CreateSurface(rt, "Body", UISprites.Rounded(UITheme.RadiusBadge, fill, fill, border));
            Stretch(body.rectTransform);

            CreateText(
                body.rectTransform,
                "Label",
                (label ?? string.Empty).ToUpperInvariant(),
                10f,
                textColor,
                TextAnchor.MiddleCenter,
                true,
                FontStyle.Bold,
                0.08f);

            if (pulse)
            {
                var glow = CreateSurface(rt, "Glow", UISprites.Glow(96));
                glow.type = Image.Type.Simple;
                Stretch(glow.rectTransform, -14f);
                glow.color = UITheme.WithAlpha(UITheme.Gold, 0.32f);
                glow.rectTransform.SetAsFirstSibling();

                var breathe = rt.gameObject.AddComponent<UIBreathe>();
                breathe.Bind(glow, 0.32f, 0.68f, UITheme.BestPulseSeconds);
            }
            return rt;
        }

        /// <summary>
        /// A vertically scrolling region with a thin themed scrollbar. Returns the
        /// content transform, which callers fill with a layout group.
        /// </summary>
        public static RectTransform CreateScrollView(
            Transform parent,
            string name,
            out ScrollRect scrollRect)
        {
            // The root must fill its host. CreateRect deliberately defaults to
            // centre anchors with a zero size, so leaving it alone gave the
            // ScrollRect a 0x0 rect: the viewport and content stretch to that,
            // and every scrolling screen rendered nothing.
            var root = Stretch(CreateRect(parent, name));

            scrollRect = root.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 26f;

            var viewport = CreateRect(root, "Viewport");
            Stretch(viewport);
            viewport.offsetMax = new Vector2(-8f, 0f);
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.sprite = UISprites.Solid;
            viewportImage.color = new Color(1f, 1f, 1f, 0.0001f);
            viewport.gameObject.AddComponent<RectMask2D>();
            scrollRect.viewport = viewport;

            var content = CreateRect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = content;

            // 6px thumb on a translucent track, matching the panel scrollbar.
            var barRect = CreateRect(root, "Scrollbar");
            barRect.anchorMin = new Vector2(1f, 0f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(1f, 0.5f);
            barRect.sizeDelta = new Vector2(6f, 0f);
            barRect.anchoredPosition = Vector2.zero;

            var barImage = barRect.gameObject.AddComponent<Image>();
            barImage.sprite = UISprites.Solid;
            barImage.color = UITheme.ScrollTrack;

            var scrollbar = barRect.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            var slidingArea = CreateRect(barRect, "Sliding Area");
            Stretch(slidingArea);
            var handle = CreateSurface(slidingArea, "Handle", UISprites.Rounded(
                UITheme.RadiusPip,
                UITheme.ScrollThumb,
                UITheme.ScrollThumb,
                default(Color)), true);
            Stretch(handle.rectTransform);
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;

            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scrollRect.verticalScrollbarSpacing = 2f;
            return content;
        }

        /// <summary>Adds a vertical layout group with the given spacing/padding.</summary>
        public static VerticalLayoutGroup AddVerticalLayout(
            RectTransform target,
            float spacing,
            RectOffset padding = null,
            TextAnchor alignment = TextAnchor.UpperLeft)
        {
            var layout = target.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset();
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            // Rows declare their heights through LayoutElement (SetHeight) or
            // text preferred sizes. Without height control the parent ignores
            // those contracts and stacked screens collapse to the top.
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        /// <summary>Adds a horizontal layout group with the given spacing/padding.</summary>
        public static HorizontalLayoutGroup AddHorizontalLayout(
            RectTransform target,
            float spacing,
            RectOffset padding = null,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var layout = target.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset();
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return layout;
        }

        /// <summary>Adds a uniform grid, used by the metric and nav grids.</summary>
        public static GridLayoutGroup AddGrid(
            RectTransform target,
            Vector2 cellSize,
            Vector2 spacing,
            int columns)
        {
            var layout = target.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = cellSize;
            layout.spacing = spacing;
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = Mathf.Max(1, columns);
            layout.childAlignment = TextAnchor.UpperLeft;
            return layout;
        }

        /// <summary>Fixes an element's height inside a layout group.</summary>
        public static LayoutElement SetHeight(RectTransform target, float height)
        {
            var element = target.GetComponent<LayoutElement>() ??
                target.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            return element;
        }

        /// <summary>Adds a CanvasGroup, creating it only once.</summary>
        public static CanvasGroup EnsureGroup(GameObject target)
        {
            return target.GetComponent<CanvasGroup>() ?? target.AddComponent<CanvasGroup>();
        }

        /// <summary>
        /// The shell shared by Workshop, Records and Settings: a panel with a
        /// Back button, a kicker over a heading, an optional right-hand slot and a
        /// bottom rule, exactly like .menu-panel > header.
        /// </summary>
        /// <returns>The padded content area below the header.</returns>
        public static RectTransform CreateProfilePanel(
            Transform parent,
            string name,
            Vector2 size,
            string kicker,
            string title,
            Action onBack,
            out RectTransform headerSlot)
        {
            var panel = CreatePanel(parent, name, size);
            var body = panel.Find("Body") as RectTransform ?? panel;

            var inner = Stretch(CreateRect(body, "Inner"), 22f);

            var header = CreateRect(inner, "Header");
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.sizeDelta = new Vector2(0f, 62f);

            var back = CreateBackButton(header, "Back", onBack);
            var backRect = back.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0f, 0.5f);
            backRect.anchorMax = new Vector2(0f, 0.5f);
            backRect.pivot = new Vector2(0f, 0.5f);
            backRect.anchoredPosition = Vector2.zero;

            var titleBlock = CreateRect(header, "TitleBlock");
            titleBlock.anchorMin = new Vector2(0f, 0f);
            titleBlock.anchorMax = new Vector2(1f, 1f);
            titleBlock.offsetMin = new Vector2(98f, 0f);
            titleBlock.offsetMax = new Vector2(-140f, 0f);

            var kickerLabel = CreateKicker(titleBlock, "Kicker", kicker, UITheme.CyanLabel);
            kickerLabel.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            kickerLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            kickerLabel.rectTransform.offsetMin = new Vector2(0f, 2f);
            kickerLabel.rectTransform.offsetMax = new Vector2(0f, -6f);
            kickerLabel.alignment = TextAnchor.LowerLeft;

            var heading = CreateHeading(titleBlock, "Title", title);
            heading.rectTransform.anchorMin = new Vector2(0f, 0f);
            heading.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            heading.rectTransform.offsetMin = new Vector2(0f, 4f);
            heading.rectTransform.offsetMax = new Vector2(0f, 2f);
            heading.alignment = TextAnchor.UpperLeft;

            headerSlot = CreateRect(header, "HeaderSlot");
            headerSlot.anchorMin = new Vector2(1f, 0.5f);
            headerSlot.anchorMax = new Vector2(1f, 0.5f);
            headerSlot.pivot = new Vector2(1f, 0.5f);
            headerSlot.sizeDelta = new Vector2(130f, 34f);
            headerSlot.anchoredPosition = Vector2.zero;

            var rule = CreateRule(header, "Rule", UITheme.BorderRule);
            rule.rectTransform.anchoredPosition = Vector2.zero;

            var content = CreateRect(inner, "Content");
            content.anchorMin = Vector2.zero;
            content.anchorMax = new Vector2(1f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = new Vector2(0f, -70f);
            return content;
        }

        /// <summary>
        /// The gameplay overlay card shared by pause, revive and the run result:
        /// centred panel, kicker, glowing heading, and a padded content column
        /// below. Matches .overlay-card, including the rise-in entrance.
        /// </summary>
        /// <returns>The content area under the heading.</returns>
        public static RectTransform CreateOverlayCard(
            Transform parent,
            string name,
            Vector2 size,
            string kicker,
            string title,
            out CanvasGroup group)
        {
            var panel = CreatePanel(parent, name, size);
            group = EnsureGroup(panel.gameObject);

            var body = panel.Find("Body") as RectTransform ?? panel;
            var inner = Stretch(CreateRect(body, "Inner"), 25f);

            var kickerLabel = CreateKicker(inner, "Kicker", kicker, UITheme.OverlayKicker, TextAnchor.UpperCenter);
            kickerLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            kickerLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            kickerLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
            kickerLabel.rectTransform.sizeDelta = new Vector2(0f, 16f);

            var heading = CreateHeading(inner, "Title", title, TextAnchor.UpperCenter);
            heading.rectTransform.anchorMin = new Vector2(0f, 1f);
            heading.rectTransform.anchorMax = new Vector2(1f, 1f);
            heading.rectTransform.pivot = new Vector2(0.5f, 1f);
            heading.rectTransform.sizeDelta = new Vector2(0f, 38f);
            heading.rectTransform.anchoredPosition = new Vector2(0f, -20f);

            // text-shadow: 0 0 12px rgba(34,211,238,0.58), 0 0 42px rgba(34,211,238,0.25)
            var bloom = CreateSurface(heading.rectTransform, "Bloom", UISprites.Glow(256));
            bloom.type = Image.Type.Simple;
            Stretch(bloom.rectTransform, -90f, -26f, -90f, -18f);
            bloom.color = UITheme.WithAlpha(UITheme.Cyan, 0.28f);
            bloom.rectTransform.SetAsFirstSibling();

            var content = CreateRect(inner, "Content");
            content.anchorMin = Vector2.zero;
            content.anchorMax = new Vector2(1f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = new Vector2(0f, -68f);

            panel.gameObject.AddComponent<UIRiseIn>()
                .Bind(panel, group, UITheme.PanelRiseSeconds, UITheme.PanelRiseOffset);
            return content;
        }

        /// <summary>
        /// The parts balance chip that sits in the Workshop header slot.
        /// </summary>
        public static Text CreatePartsBadge(Transform parent, string name)
        {
            var rt = CreateRect(parent, name);
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(96f, 32f);

            var body = CreateSurface(rt, "Body", UISprites.Rounded(
                UITheme.RadiusSmall,
                UITheme.PartsFill,
                UITheme.PartsFill,
                UITheme.BorderParts));
            Stretch(body.rectTransform);

            var icon = UIIcons.CreateHomeIcon(body.rectTransform, "coins", UITheme.Gold, 15f);
            var textLeft = 10f;
            if (icon != null)
            {
                icon.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                icon.rectTransform.pivot = new Vector2(0f, 0.5f);
                icon.rectTransform.anchoredPosition = new Vector2(9f, 0f);
                textLeft = 30f;
            }

            var label = CreateText(
                body.rectTransform,
                "Value",
                "0",
                12f,
                UITheme.GoldLight,
                TextAnchor.MiddleLeft,
                true,
                FontStyle.Bold);
            label.rectTransform.offsetMin = new Vector2(textLeft, 0f);
            label.rectTransform.offsetMax = new Vector2(-9f, 0f);
            return label;
        }
    }

    // ----------------------------------------------------------------------
    // Motion helpers
    // ----------------------------------------------------------------------
    // These stand in for the stylesheet's keyframes. They all run on unscaled
    // time because every screen that uses them appears while the simulation is
    // paused, where Time.deltaTime is zero.

    /// <summary>Fades and rises a card in, matching @keyframes panel-rise-in.</summary>
    public sealed class UIRiseIn : MonoBehaviour
    {
        private RectTransform _target;
        private CanvasGroup _group;
        private float _duration = UITheme.PanelRiseSeconds;
        private float _offset = UITheme.PanelRiseOffset;
        private float _delay;
        private float _scaleFrom = 1f;
        private float _elapsed;
        private Vector2 _restPosition;

        public void Bind(
            RectTransform target,
            CanvasGroup group,
            float duration,
            float offset,
            float delay = 0f,
            float scaleFrom = 1f)
        {
            _target = target;
            _group = group;
            _duration = Mathf.Max(0.0001f, duration);
            _offset = offset;
            _delay = Mathf.Max(0f, delay);
            _scaleFrom = scaleFrom;
            _restPosition = target != null ? target.anchoredPosition : Vector2.zero;
            Replay();
        }

        /// <summary>Restarts the entrance, called each time a screen is shown.</summary>
        public void Replay()
        {
            _elapsed = 0f;
            Apply(0f);
        }

        private void OnEnable() => Replay();

        private void Update()
        {
            if (_elapsed >= _delay + _duration) return;
            _elapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01((_elapsed - _delay) / _duration);
            Apply(t);
        }

        private void Apply(float t)
        {
            // cubic-bezier(0.22, 1, 0.36, 1) is very close to a quintic ease-out.
            var eased = 1f - Mathf.Pow(1f - t, 5f);
            if (_group != null) _group.alpha = Mathf.Clamp01(_elapsed < _delay ? 0f : eased);
            if (_target == null) return;
            _target.anchoredPosition = _restPosition + new Vector2(0f, _offset * (1f - eased));
            if (!Mathf.Approximately(_scaleFrom, 1f))
            {
                _target.localScale = Vector3.one * Mathf.Lerp(_scaleFrom, 1f, eased);
            }
        }
    }

    /// <summary>Oscillates an image's alpha, matching @keyframes neon-breathe.</summary>
    public sealed class UIBreathe : MonoBehaviour
    {
        private Graphic _target;
        private float _min;
        private float _max;
        private float _period = 2.4f;
        private float _clock;

        public void Bind(Graphic target, float min, float max, float period)
        {
            _target = target;
            _min = min;
            _max = max;
            _period = Mathf.Max(0.05f, period);
        }

        private void Update()
        {
            if (_target == null) return;
            _clock += Time.unscaledDeltaTime;
            var phase = 0.5f - 0.5f * Mathf.Cos(_clock / _period * Mathf.PI * 2f);
            var color = _target.color;
            color.a = Mathf.Lerp(_min, _max, phase);
            _target.color = color;
        }
    }

    /// <summary>Drifts a transform vertically, matching @keyframes title-drift.</summary>
    public sealed class UIDrift : MonoBehaviour
    {
        private RectTransform _target;
        private float _amplitude = UITheme.TitleDriftOffset;
        private float _period = UITheme.TitleDriftSeconds;
        private Vector2 _rest;
        private float _clock;

        public void Bind(RectTransform target, float amplitude, float period)
        {
            _target = target;
            _amplitude = amplitude;
            _period = Mathf.Max(0.1f, period);
            _rest = target != null ? target.anchoredPosition : Vector2.zero;
        }

        private void Update()
        {
            if (_target == null) return;
            _clock += Time.unscaledDeltaTime;
            var phase = 0.5f - 0.5f * Mathf.Cos(_clock / _period * Mathf.PI * 2f);
            _target.anchoredPosition = _rest + new Vector2(0f, -_amplitude * phase);
        }
    }

    /// <summary>
    /// Sweeps a colour gradient across text, standing in for the CSS
    /// background-clip: text shimmer on the VOIDFALL wordmark.
    /// </summary>
    public sealed class UIShimmer : MonoBehaviour
    {
        private Text _target;
        private float _period = UITheme.ShimmerSeconds;
        private float _clock;

        public void Bind(Text target, float period)
        {
            _target = target;
            _period = Mathf.Max(0.1f, period);
        }

        private void Update()
        {
            if (_target == null) return;
            _clock += Time.unscaledDeltaTime;
            var phase = Mathf.Repeat(_clock / _period, 1f);

            // The stylesheet clips a moving gradient to the glyphs. The legacy
            // text component has no per-vertex colour, so the wordmark cycles
            // through the same stops as a whole instead of carrying a travelling
            // highlight. It reads as the same slow chromatic drift.
            _target.color = SampleStops(phase);
        }

        private static Color SampleStops(float t)
        {
            var cyan = UITheme.CyanLight;
            var white = UITheme.ShimmerHighlight;
            var violet = UITheme.Violet;
            if (t < 0.25f) return Color.Lerp(cyan, white, t / 0.25f);
            if (t < 0.5f) return Color.Lerp(white, violet, (t - 0.25f) / 0.25f);
            if (t < 0.75f) return Color.Lerp(violet, cyan, (t - 0.5f) / 0.25f);
            return Color.Lerp(cyan, cyan, (t - 0.75f) / 0.25f);
        }
    }

    /// <summary>
    /// Lifts a card on hover, matching the stylesheet's translateY(-3px) with a
    /// cubic-bezier(0.34, 1.56, 0.64, 1) overshoot.
    /// </summary>
    public sealed class UIHoverLift : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private RectTransform _target;
        private float _lift = 3f;
        private Vector2 _rest;
        private bool _hovering;
        private float _progress;
        private bool _bound;

        public void Bind(RectTransform target, float lift)
        {
            _target = target;
            _lift = lift;
            _rest = target != null ? target.anchoredPosition : Vector2.zero;
            _bound = true;
        }

        public void OnPointerEnter(PointerEventData eventData) => _hovering = true;

        public void OnPointerExit(PointerEventData eventData) => _hovering = false;

        private void Update()
        {
            if (!_bound || _target == null) return;
            // Layout groups own this rect's position, so re-read the rest pose
            // whenever the pointer is away rather than caching it once.
            if (!_hovering && Mathf.Approximately(_progress, 0f))
            {
                _rest = _target.anchoredPosition;
                return;
            }
            var goal = _hovering ? 1f : 0f;
            _progress = Mathf.MoveTowards(_progress, goal, Time.unscaledDeltaTime * 7f);
            var overshoot = _progress <= 0f
                ? 0f
                : 1f + 0.56f * Mathf.Sin(_progress * Mathf.PI) * (1f - _progress);
            _target.anchoredPosition = _rest + new Vector2(0f, _lift * _progress * overshoot);
        }
    }

    /// <summary>
    /// Scales and nudges a control while held, reproducing the :active states.
    /// </summary>
    public sealed class UIPressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private RectTransform _target;
        private float _scale = 0.98f;
        private Vector2 _pressOffset;
        private Vector2 _hoverOffset;
        private Vector2 _rest;
        private bool _pressed;
        private bool _bound;

        public void Bind(RectTransform target, float scale, Vector2 pressOffset, Vector2 hoverOffset)
        {
            _target = target;
            _scale = scale;
            _pressOffset = pressOffset;
            _hoverOffset = hoverOffset;
            _rest = target != null ? target.anchoredPosition : Vector2.zero;
            _bound = true;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_bound || _target == null) return;
            _rest = _target.anchoredPosition;
            _pressed = true;
            _target.localScale = Vector3.one * _scale;
            _target.anchoredPosition = _rest + _pressOffset;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_bound || _target == null || !_pressed) return;
            _pressed = false;
            _target.localScale = Vector3.one;
            _target.anchoredPosition = _rest;
        }

        private void OnDisable()
        {
            if (!_bound || _target == null || !_pressed) return;
            _pressed = false;
            _target.localScale = Vector3.one;
            _target.anchoredPosition = _rest;
        }
    }
}

namespace VoidFall.UI
{
    /// <summary>
    /// Shared plumbing for every screen: a CanvasGroup for fading, a build-once
    /// guard, and a show/hide that replays entrance animations.
    /// </summary>
    public abstract class UIViewBase : MonoBehaviour
    {
        protected UIManager Manager { get; private set; }
        protected UICallbacks Callbacks => Manager != null ? Manager.Callbacks : null;
        protected CanvasGroup Group { get; private set; }
        protected RectTransform Root { get; private set; }

        private bool _built;
        private UIRiseIn[] _entrances = Array.Empty<UIRiseIn>();

        /// <summary>Builds the screen. Safe to call more than once.</summary>
        public void Initialize(UIManager manager)
        {
            if (_built) return;
            _built = true;
            Manager = manager;
            Root = (RectTransform)transform;
            Group = UIBuilder.EnsureGroup(gameObject);
            Build();
            _entrances = GetComponentsInChildren<UIRiseIn>(true);
            SetVisible(false);
        }

        /// <summary>Assembles the screen's hierarchy.</summary>
        protected abstract void Build();

        /// <summary>Called every time the screen becomes visible.</summary>
        protected virtual void OnShown() { }

        public bool IsVisible => gameObject.activeSelf;

        public virtual void SetVisible(bool visible)
        {
            if (gameObject.activeSelf == visible)
            {
                if (visible) Replay();
                return;
            }
            gameObject.SetActive(visible);
            if (Group != null)
            {
                Group.interactable = visible;
                Group.blocksRaycasts = visible;
            }
            if (visible)
            {
                Replay();
                OnShown();
            }
        }

        private void Replay()
        {
            for (var index = 0; index < _entrances.Length; index++)
            {
                if (_entrances[index] != null) _entrances[index].Replay();
            }
        }

        /// <summary>Removes every child of a container, used when repopulating.</summary>
        protected static void ClearChildren(Transform container)
        {
            if (container == null) return;
            for (var index = container.childCount - 1; index >= 0; index--)
            {
                var child = container.GetChild(index);
                // Destroy is deferred to the end of the frame, so a repopulate in
                // the same frame would leave the layout group measuring the old
                // and new children together. Detaching first takes them out of
                // the layout immediately.
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }
        }

        /// <summary>Formats seconds as the M:SS the browser build uses.</summary>
        protected static string FormatTime(float seconds)
        {
            var whole = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (whole / 60).ToString() + ":" + (whole % 60).ToString("00");
        }

        /// <summary>Thousands-separated integers, matching toLocaleString().</summary>
        protected static string FormatNumber(long value)
        {
            return value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}

namespace VoidFall.UI
{
    /// <summary>
    /// Reuses the icon atlases the runtime already ships in Resources, so the
    /// rebuilt menus keep the exact glyphs the IMGUI screens and the gameplay HUD
    /// use. The UV maths mirrors the runtime's HomeIconUv / ControlIconUv.
    ///
    /// Every accessor tolerates a missing atlas: callers get null and simply lay
    /// out without an icon rather than failing.
    /// </summary>
    public static class UIIcons
    {
        private static Texture2D _home;
        private static Texture2D _control;
        private static bool _homeLoaded;
        private static bool _controlLoaded;

        private static Texture2D HomeAtlas
        {
            get
            {
                if (_homeLoaded) return _home;
                _homeLoaded = true;
                _home = Resources.Load<Texture2D>("VoidFall/HomeIconsRaster");
                if (_home != null) return _home;
                var sprite = Resources.Load<Sprite>("VoidFall/HomeIcons");
                _home = sprite != null ? sprite.texture : Resources.Load<Texture2D>("VoidFall/HomeIcons");
                return _home;
            }
        }

        private static Texture2D ControlAtlas
        {
            get
            {
                if (_controlLoaded) return _control;
                _controlLoaded = true;
                _control = Resources.Load<Texture2D>("VoidFall/ControlIconsRaster");
                if (_control != null) return _control;
                var sprite = Resources.Load<Sprite>("VoidFall/ControlIcons");
                _control = sprite != null ? sprite.texture : Resources.Load<Texture2D>("VoidFall/ControlIcons");
                return _control;
            }
        }

        /// <summary>Slot order of the 3x2 home atlas.</summary>
        private static int HomeSlot(string id)
        {
            switch (id)
            {
                case "wrench": return 0;
                case "trophy": return 1;
                case "settings": return 2;
                case "coins": return 3;
                case "skull": return 4;
                default: return -1;
            }
        }

        /// <summary>Slot order of the 1x10 control strip.</summary>
        private static int ControlSlot(string id)
        {
            switch (id)
            {
                case "arrow-left": return 0;
                case "play": return 1;
                case "pause": return 2;
                case "rotate-ccw": return 3;
                case "house": return 4;
                case "volume-2": return 5;
                case "volume-x": return 6;
                case "download": return 7;
                case "heart": return 8;
                case "skull": return 9;
                default: return -1;
            }
        }

        /// <summary>
        /// Adds an icon from the 3x2 home atlas, or returns null when the atlas or
        /// the requested glyph is unavailable.
        /// </summary>
        public static RawImage CreateHomeIcon(Transform parent, string id, Color tint, float size)
        {
            var slot = HomeSlot(id);
            var atlas = HomeAtlas;
            if (atlas == null || slot < 0) return null;

            var column = slot % 3;
            var row = slot / 3;
            return CreateIcon(
                parent,
                "Icon." + id,
                atlas,
                new Rect(column / 3f, 1f - (row + 1) / 2f, 1f / 3f, 1f / 2f),
                tint,
                size);
        }

        /// <summary>
        /// Adds an icon from the 1x10 control strip, or returns null when the
        /// atlas or the requested glyph is unavailable.
        /// </summary>
        public static RawImage CreateControlIcon(Transform parent, string id, Color tint, float size)
        {
            var slot = ControlSlot(id);
            var atlas = ControlAtlas;
            if (atlas == null || slot < 0) return null;

            return CreateIcon(
                parent,
                "Icon." + id,
                atlas,
                new Rect(slot / 10f, 0f, 1f / 10f, 1f),
                tint,
                size);
        }

        private static RawImage CreateIcon(
            Transform parent,
            string name,
            Texture2D atlas,
            Rect uv,
            Color tint,
            float size)
        {
            var rt = UIBuilder.CreateRect(parent, name);
            rt.sizeDelta = new Vector2(size, size);

            var image = rt.gameObject.AddComponent<RawImage>();
            image.texture = atlas;
            image.uvRect = uv;
            image.color = tint;
            image.raycastTarget = false;
            return image;
        }
    }
}
