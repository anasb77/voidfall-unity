using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoidFall.UI
{
    /// <summary>
    /// Design tokens ported from the browser build's stylesheet
    /// (voidfall.io/src/index.css). Every value here has a counterpart in that
    /// file so the Unity menus read as the same product as the web original.
    ///
    /// Two conventions carried over from the web:
    ///   * All type sizes are authored in CSS pixels and multiplied by
    ///     <see cref="TextScale"/> (the stylesheet's --ui-text-scale: 1.15).
    ///   * The canvas reference resolution is 1600x900 matched on height, so one
    ///     CSS pixel is one reference pixel and the numbers transfer directly.
    /// </summary>
    public static class UITheme
    {
        public const float ReferenceWidth = 1600f;
        public const float ReferenceHeight = 900f;

        /// <summary>Stylesheet --ui-text-scale.</summary>
        public const float TextScale = 1.15f;

        /// <summary>
        /// Converts a CSS pixel type size into a rendered font size, applying the
        /// stylesheet's text scale. Named FontSize rather than Font so it does not
        /// shadow the UnityEngine.Font type inside this class.
        /// </summary>
        public static float FontSize(float cssPixels) => Mathf.Round(cssPixels * TextScale);

        // ------------------------------------------------------------------
        // Surfaces
        // ------------------------------------------------------------------

        /// <summary>#05070c - the app background.</summary>
        public static readonly Color Void = Hex("#05070c");

        /// <summary>rgba(2, 5, 15, 0.59) - the scrim behind gameplay overlays.</summary>
        public static readonly Color OverlayScrim = Rgba(2, 5, 15, 0.59f);

        /// <summary>rgba(2, 5, 15, 0.95) - the near-opaque scrim behind the quit confirmation, which must fully cover the menu.</summary>
        public static readonly Color MenuDialogScrim = Rgba(2, 5, 15, 0.95f);

        /// <summary>rgba(3, 6, 11, 0.10) - the much lighter menu-layer scrim.</summary>
        public static readonly Color MenuScrim = Rgba(3, 6, 11, 0.10f);

        /// <summary>Panel gradient top: rgba(13, 17, 38, 0.90).</summary>
        public static readonly Color PanelTop = Rgba(13, 17, 38, 0.90f);

        /// <summary>Panel gradient bottom: rgba(7, 9, 22, 0.94).</summary>
        public static readonly Color PanelBottom = Rgba(7, 9, 22, 0.94f);

        /// <summary>Status-strip gradient top: rgba(13, 17, 38, 0.82).</summary>
        public static readonly Color StatusTop = Rgba(13, 17, 38, 0.82f);

        /// <summary>Status-strip gradient bottom: rgba(7, 9, 22, 0.90).</summary>
        public static readonly Color StatusBottom = Rgba(7, 9, 22, 0.90f);

        /// <summary>Nav card rest gradient top: rgba(13, 17, 38, 0.78).</summary>
        public static readonly Color NavCardTop = Rgba(13, 17, 38, 0.78f);

        /// <summary>Nav card rest gradient bottom: rgba(7, 9, 22, 0.88).</summary>
        public static readonly Color NavCardBottom = Rgba(7, 9, 22, 0.88f);

        /// <summary>Nav card hover gradient top: rgba(17, 27, 53, 0.90).</summary>
        public static readonly Color NavCardHoverTop = Rgba(17, 27, 53, 0.90f);

        /// <summary>Nav card hover gradient bottom: rgba(8, 13, 29, 0.94).</summary>
        public static readonly Color NavCardHoverBottom = Rgba(8, 13, 29, 0.94f);

        /// <summary>Upgrade card gradient top: rgba(16, 21, 46, 0.95).</summary>
        public static readonly Color UpgradeCardTop = Rgba(16, 21, 46, 0.95f);

        /// <summary>Upgrade card gradient bottom: rgba(8, 10, 26, 0.97).</summary>
        public static readonly Color UpgradeCardBottom = Rgba(8, 10, 26, 0.97f);

        /// <summary>Evolution card gradient top: rgba(19, 25, 51, 0.98).</summary>
        public static readonly Color EvolutionCardTop = Rgba(19, 25, 51, 0.98f);

        /// <summary>Evolution card gradient bottom: rgba(7, 9, 24, 0.99).</summary>
        public static readonly Color EvolutionCardBottom = Rgba(7, 9, 24, 0.99f);

        /// <summary>Row fill: rgba(8, 13, 21, 0.56) - workshop rows and metric tiles.</summary>
        public static readonly Color RowFill = Rgba(8, 13, 21, 0.56f);

        /// <summary>Row hover fill: rgba(10, 19, 29, 0.72).</summary>
        public static readonly Color RowFillHover = Rgba(10, 19, 29, 0.72f);

        /// <summary>Settings row fill: rgba(8, 13, 21, 0.54).</summary>
        public static readonly Color SettingRowFill = Rgba(8, 13, 21, 0.54f);

        /// <summary>Selected/previewing row fill: rgba(11, 29, 43, 0.80).</summary>
        public static readonly Color RowFillActive = Rgba(11, 29, 43, 0.80f);

        /// <summary>Inner panel fill: rgba(2, 6, 18, 0.34) - damage + build recap.</summary>
        public static readonly Color InnerPanel = Rgba(2, 6, 18, 0.34f);

        /// <summary>Table header fill: rgba(15, 23, 35, 0.72).</summary>
        public static readonly Color TableHeader = Rgba(15, 23, 35, 0.72f);

        /// <summary>Workshop preview fill: rgba(3, 7, 18, 0.80).</summary>
        public static readonly Color PreviewFill = Rgba(3, 7, 18, 0.80f);

        /// <summary>Evolution reveal text plate: rgba(3, 7, 18, 0.82).</summary>
        public static readonly Color RevealPlate = Rgba(3, 7, 18, 0.82f);

        /// <summary>Chip fill: rgba(5, 9, 16, 0.62).</summary>
        public static readonly Color ChipFill = Rgba(5, 9, 16, 0.62f);

        /// <summary>Diagnostics readout fill: rgba(2, 6, 12, 0.82).</summary>
        public static readonly Color DebugFill = Rgba(2, 6, 12, 0.82f);

        /// <summary>Corner control button fill: rgba(7, 11, 19, 0.78).</summary>
        public static readonly Color ControlFill = Rgba(7, 11, 19, 0.78f);

        /// <summary>Back button fill: rgba(8, 13, 21, 0.72).</summary>
        public static readonly Color BackFill = Rgba(8, 13, 21, 0.72f);

        // ------------------------------------------------------------------
        // Accents
        // ------------------------------------------------------------------

        /// <summary>#22d3ee - the primary identity colour.</summary>
        public static readonly Color Cyan = Hex("#22d3ee");

        /// <summary>#67e8f9 - borders, focus rings, default item accent.</summary>
        public static readonly Color CyanLight = Hex("#67e8f9");

        /// <summary>#a5f3fc - icons and emphasis text.</summary>
        public static readonly Color CyanBright = Hex("#a5f3fc");

        /// <summary>#cffafe - brightest cyan text.</summary>
        public static readonly Color CyanPale = Hex("#cffafe");

        /// <summary>#7dd3fc - kickers and small caps labels.</summary>
        public static readonly Color CyanLabel = Hex("#7dd3fc");

        /// <summary>#38bdf8 - filled rank pips.</summary>
        public static readonly Color PipFilled = Hex("#38bdf8");

        /// <summary>#bae6fd - the score column in the records table.</summary>
        public static readonly Color ScoreValue = Hex("#bae6fd");

        /// <summary>#dff8fd - primary action label.</summary>
        public static readonly Color PrimaryLabel = Hex("#dff8fd");

        /// <summary>#8edcf0 - overlay card kicker.</summary>
        public static readonly Color OverlayKicker = Hex("#8edcf0");

        /// <summary>#edf8fc - status strip values.</summary>
        public static readonly Color StatusValue = Hex("#edf8fc");

        /// <summary>#a78bfa - violet, the title shimmer's third stop.</summary>
        public static readonly Color Violet = Hex("#a78bfa");

        /// <summary>#f0fdff - the title shimmer highlight.</summary>
        public static readonly Color ShimmerHighlight = Hex("#f0fdff");

        /// <summary>#34d399 - XP and reward green.</summary>
        public static readonly Color Green = Hex("#34d399");

        /// <summary>#6ee7b7 - the "LEVEL UP" kicker and reward toasts.</summary>
        public static readonly Color GreenLight = Hex("#6ee7b7");

        /// <summary>#86efac - the diagnostics readout.</summary>
        public static readonly Color GreenDebug = Hex("#86efac");

        /// <summary>#bbf7d0 - diagnostics button label.</summary>
        public static readonly Color GreenDebugLabel = Hex("#bbf7d0");

        /// <summary>#facc15 - gold, the overclock meter fill.</summary>
        public static readonly Color Gold = Hex("#facc15");

        /// <summary>#fde68a - parts balance and the "new best" badge.</summary>
        public static readonly Color GoldLight = Hex("#fde68a");

        /// <summary>#ef4444 - boss health and the armed reset border.</summary>
        public static readonly Color Red = Hex("#ef4444");

        /// <summary>#fb7185 - danger toasts and the integrity heart.</summary>
        public static readonly Color Rose = Hex("#fb7185");

        /// <summary>#fca5a5 - reset button label.</summary>
        public static readonly Color RoseLight = Hex("#fca5a5");

        /// <summary>#fecaca - boss label and the save warning.</summary>
        public static readonly Color RosePale = Hex("#fecaca");

        /// <summary>#fee2e2 - armed reset label.</summary>
        public static readonly Color RoseBright = Hex("#fee2e2");

        // ------------------------------------------------------------------
        // Text ramp
        // ------------------------------------------------------------------

        /// <summary>#f8fafc</summary>
        public static readonly Color TextBrightest = Hex("#f8fafc");

        /// <summary>#f1f5f9 - headings.</summary>
        public static readonly Color TextHeading = Hex("#f1f5f9");

        /// <summary>#e5edf4 - default body copy.</summary>
        public static readonly Color TextBody = Hex("#e5edf4");

        /// <summary>#dbe5ee</summary>
        public static readonly Color TextStrong = Hex("#dbe5ee");

        /// <summary>#d7e0e8 - nav card label.</summary>
        public static readonly Color TextNav = Hex("#d7e0e8");

        /// <summary>#cbd5e1 - chips and table cells.</summary>
        public static readonly Color TextChip = Hex("#cbd5e1");

        /// <summary>#b8c5d2 - HUD and back-button labels.</summary>
        public static readonly Color TextLabel = Hex("#b8c5d2");

        /// <summary>#b4c0cc - upgrade card description.</summary>
        public static readonly Color TextDescription = Hex("#b4c0cc");

        /// <summary>#9fb0c1 - section labels.</summary>
        public static readonly Color TextSection = Hex("#9fb0c1");

        /// <summary>#94a3b8 - panel sub-headings.</summary>
        public static readonly Color TextSubtle = Hex("#94a3b8");

        /// <summary>#8898a9 - panel intro copy.</summary>
        public static readonly Color TextIntro = Hex("#8898a9");

        /// <summary>#8090a1 - workshop row description.</summary>
        public static readonly Color TextRowDetail = Hex("#8090a1");

        /// <summary>#7f94a6 - action button sub-label.</summary>
        public static readonly Color TextActionDetail = Hex("#7f94a6");

        /// <summary>#78889a - metric labels and table headers.</summary>
        public static readonly Color TextMetricLabel = Hex("#78889a");

        /// <summary>#748496 - nav card detail.</summary>
        public static readonly Color TextNavDetail = Hex("#748496");

        /// <summary>#718399 - inactive preview rank.</summary>
        public static readonly Color TextInactive = Hex("#718399");

        /// <summary>#718096 - empty-state copy.</summary>
        public static readonly Color TextEmpty = Hex("#718096");

        /// <summary>#667382 - disabled action label.</summary>
        public static readonly Color TextDisabled = Hex("#667382");

        /// <summary>#657689 - status strip label.</summary>
        public static readonly Color TextStatusLabel = Hex("#657689");

        /// <summary>#657486 - upgrade card index badge.</summary>
        public static readonly Color TextIndex = Hex("#657486");

        /// <summary>#5f6b77 - disabled buy button label.</summary>
        public static readonly Color TextDisabledDeep = Hex("#5f6b77");

        // ------------------------------------------------------------------
        // Borders
        // ------------------------------------------------------------------

        /// <summary>rgba(103, 232, 249, 0.16) - the canonical panel hairline.</summary>
        public static readonly Color BorderPanel = Rgba(103, 232, 249, 0.16f);

        /// <summary>rgba(103, 232, 249, 0.18) - workshop preview border.</summary>
        public static readonly Color BorderPreview = Rgba(103, 232, 249, 0.18f);

        /// <summary>rgba(103, 232, 249, 0.15) - nav card rest border.</summary>
        public static readonly Color BorderNavCard = Rgba(103, 232, 249, 0.15f);

        /// <summary>rgba(103, 232, 249, 0.54) - nav card hover border.</summary>
        public static readonly Color BorderNavCardHover = Rgba(103, 232, 249, 0.54f);

        /// <summary>rgba(103, 232, 249, 0.14) - inner panel border.</summary>
        public static readonly Color BorderInner = Rgba(103, 232, 249, 0.14f);

        /// <summary>rgba(103, 217, 243, 0.72) - primary action border.</summary>
        public static readonly Color BorderPrimary = Rgba(103, 217, 243, 0.72f);

        /// <summary>rgba(103, 232, 249, 0.56) - the hero Start Run border.</summary>
        public static readonly Color BorderStart = Rgba(103, 232, 249, 0.56f);

        /// <summary>rgba(165, 243, 252, 0.82) - Start Run hover border.</summary>
        public static readonly Color BorderStartHover = Rgba(165, 243, 252, 0.82f);

        /// <summary>rgba(103, 232, 249, 0.30) - buy button border.</summary>
        public static readonly Color BorderBuy = Rgba(103, 232, 249, 0.30f);

        /// <summary>rgba(148, 163, 184, 0.23) - secondary action border.</summary>
        public static readonly Color BorderSecondary = Rgba(148, 163, 184, 0.23f);

        /// <summary>rgba(148, 163, 184, 0.24) - control button border.</summary>
        public static readonly Color BorderControl = Rgba(148, 163, 184, 0.24f);

        /// <summary>rgba(148, 163, 184, 0.20) - back button border.</summary>
        public static readonly Color BorderBack = Rgba(148, 163, 184, 0.20f);

        /// <summary>rgba(148, 163, 184, 0.16) - health block border.</summary>
        public static readonly Color BorderBlock = Rgba(148, 163, 184, 0.16f);

        /// <summary>rgba(148, 163, 184, 0.14) - row and metric border.</summary>
        public static readonly Color BorderRow = Rgba(148, 163, 184, 0.14f);

        /// <summary>rgba(148, 163, 184, 0.13) - settings row border.</summary>
        public static readonly Color BorderSettingRow = Rgba(148, 163, 184, 0.13f);

        /// <summary>rgba(148, 163, 184, 0.15) - panel header rule.</summary>
        public static readonly Color BorderRule = Rgba(148, 163, 184, 0.15f);

        /// <summary>rgba(148, 163, 184, 0.10) - table row divider.</summary>
        public static readonly Color BorderDivider = Rgba(148, 163, 184, 0.10f);

        /// <summary>rgba(148, 163, 184, 0.14) - the 1x24 rules in .menu-status.</summary>
        public static readonly Color Divider = Rgba(148, 163, 184, 0.14f);

        /// <summary>rgba(125, 211, 252, 0.27) - row hover border.</summary>
        public static readonly Color BorderRowHover = Rgba(125, 211, 252, 0.27f);

        /// <summary>rgba(125, 211, 252, 0.24) - select and settings hover border.</summary>
        public static readonly Color BorderSelect = Rgba(125, 211, 252, 0.24f);

        /// <summary>rgba(100, 116, 139, 0.28) - empty rank pip.</summary>
        public static readonly Color PipEmpty = Rgba(100, 116, 139, 0.28f);

        /// <summary>rgba(250, 204, 21, 0.22) - parts balance border.</summary>
        public static readonly Color BorderParts = Rgba(250, 204, 21, 0.22f);

        /// <summary>rgba(250, 204, 21, 0.32) - new-best badge border.</summary>
        public static readonly Color BorderBest = Rgba(250, 204, 21, 0.32f);

        /// <summary>rgba(248, 113, 113, 0.32) - save warning border.</summary>
        public static readonly Color BorderWarning = Rgba(248, 113, 113, 0.32f);

        /// <summary>rgba(248, 113, 113, 0.23) - reset button border.</summary>
        public static readonly Color BorderReset = Rgba(248, 113, 113, 0.23f);

        // ------------------------------------------------------------------
        // Tinted fills
        // ------------------------------------------------------------------

        /// <summary>rgba(8, 29, 39, 0.84) - primary action fill.</summary>
        public static readonly Color PrimaryFill = Rgba(8, 29, 39, 0.84f);

        /// <summary>rgba(10, 43, 55, 0.92) - primary action hover fill.</summary>
        public static readonly Color PrimaryFillHover = Rgba(10, 43, 55, 0.92f);

        /// <summary>rgba(15, 23, 34, 0.72) - secondary action fill.</summary>
        public static readonly Color SecondaryFill = Rgba(15, 23, 34, 0.72f);

        /// <summary>rgba(20, 32, 46, 0.82) - secondary action hover fill.</summary>
        public static readonly Color SecondaryFillHover = Rgba(20, 32, 46, 0.82f);

        /// <summary>The Start Run gradient: rgba(34, 211, 238, 0.18) to 0.06.</summary>
        public static readonly Color StartFillTop = Rgba(34, 211, 238, 0.18f);
        public static readonly Color StartFillBottom = Rgba(34, 211, 238, 0.06f);
        public static readonly Color StartFillTopHover = Rgba(34, 211, 238, 0.30f);
        public static readonly Color StartFillBottomHover = Rgba(34, 211, 238, 0.10f);

        /// <summary>rgba(14, 116, 144, 0.16) - workshop icon frame.</summary>
        public static readonly Color IconFrame = Rgba(14, 116, 144, 0.16f);

        /// <summary>rgba(14, 116, 144, 0.18) - buy button fill and active preview rank.</summary>
        public static readonly Color BuyFill = Rgba(14, 116, 144, 0.18f);

        /// <summary>rgba(30, 41, 59, 0.18) - disabled buy fill.</summary>
        public static readonly Color BuyFillDisabled = Rgba(30, 41, 59, 0.18f);

        /// <summary>rgba(113, 63, 18, 0.14) - parts balance fill.</summary>
        public static readonly Color PartsFill = Rgba(113, 63, 18, 0.14f);

        /// <summary>rgba(113, 63, 18, 0.18) - new-best badge fill.</summary>
        public static readonly Color BestFill = Rgba(113, 63, 18, 0.18f);

        /// <summary>rgba(127, 29, 29, 0.20) - save warning fill.</summary>
        public static readonly Color WarningFill = Rgba(127, 29, 29, 0.20f);

        /// <summary>rgba(127, 29, 29, 0.10) - reset button fill.</summary>
        public static readonly Color ResetFill = Rgba(127, 29, 29, 0.10f);

        /// <summary>rgba(127, 29, 29, 0.30) - armed reset fill.</summary>
        public static readonly Color ResetFillArmed = Rgba(127, 29, 29, 0.30f);

        /// <summary>#263342 - toggle track, off.</summary>
        public static readonly Color ToggleOff = Hex("#263342");

        /// <summary>#0e7490 - toggle track, on.</summary>
        public static readonly Color ToggleOn = Hex("#0e7490");

        /// <summary>#9aa9b9 - toggle knob, off.</summary>
        public static readonly Color ToggleKnobOff = Hex("#9aa9b9");

        /// <summary>#0b1420 - select background.</summary>
        public static readonly Color SelectFill = Hex("#0b1420");

        /// <summary>#dbeafe - select label.</summary>
        public static readonly Color SelectLabel = Hex("#dbeafe");

        /// <summary>#415166 - scrollbar thumb.</summary>
        public static readonly Color ScrollThumb = Hex("#415166");

        /// <summary>rgba(8, 13, 21, 0.35) - scrollbar track.</summary>
        public static readonly Color ScrollTrack = Rgba(8, 13, 21, 0.35f);

        // ------------------------------------------------------------------
        // Corner radii (CSS px)
        // ------------------------------------------------------------------

        public const float RadiusPanel = 12f;
        public const float RadiusCard = 9f;
        public const float RadiusControl = 8f;
        public const float RadiusRow = 7f;
        public const float RadiusSmall = 6f;
        public const float RadiusChip = 5f;
        public const float RadiusBadge = 4f;
        public const float RadiusPip = 3f;
        public const float RadiusBar = 2f;

        /// <summary>The stylesheet's 160deg panel gradient angle.</summary>
        public const float PanelGradientAngle = 160f;

        /// <summary>The stylesheet's 165deg upgrade card gradient angle.</summary>
        public const float CardGradientAngle = 165f;

        // ------------------------------------------------------------------
        // Motion (seconds, from the stylesheet's keyframes)
        // ------------------------------------------------------------------

        public const float MenuFadeSeconds = 0.30f;
        public const float OverlayFadeSeconds = 0.30f;
        public const float PanelRiseSeconds = 0.45f;
        public const float PanelRiseOffset = 18f;
        public const float CardRiseSeconds = 0.34f;
        public const float CardRiseStagger = 0.07f;
        public const float CardRiseOffset = 26f;
        public const float TitleDriftSeconds = 5f;
        public const float TitleDriftOffset = 8f;
        public const float ShimmerSeconds = 6f;
        public const float NeonBreatheSeconds = 2.4f;
        public const float BestPulseSeconds = 1.6f;
        public const float EvolutionRevealSeconds = 2.6f;

        // ------------------------------------------------------------------
        // Fonts
        // ------------------------------------------------------------------

        private static Font _displayFont;
        private static Font _bodyFont;
        private static bool _displayResolved;
        private static bool _bodyResolved;

        // The interface draws with UnityEngine.UI.Text and dynamic OS fonts
        // rather than TextMeshPro, and that is a deliberate correction rather
        // than a preference.
        //
        // TextMeshPro needs a font asset. This project has no imported TMP
        // essential resources (no TMP Settings asset and no LiberationSans SDF
        // anywhere under Assets), so TMP_Settings has no default asset to fall
        // back on, and TMP_FontAsset.CreateFontAsset cannot build one from a
        // font produced by CreateDynamicFontFromOSFont because such a font
        // exposes no readable file for TMP to rasterise from in a player build.
        // The result was menus whose panels and buttons drew correctly while
        // every label rendered nothing at all.
        //
        // Dynamic OS fonts with the legacy text component are already proven in
        // this project: the gameplay HUD and the previous IMGUI menus both use
        // exactly this path and both render. So the UI layer uses it too, and
        // depends on no imported font asset.

        /// <summary>The stylesheet's --font-display ("Bahnschrift").</summary>
        public static Font DisplayFont
        {
            get
            {
                if (!_displayResolved)
                {
                    _displayResolved = true;
                    _displayFont = ResolveFont("Bahnschrift", "Segoe UI Variable Display", "Segoe UI");
                }
                return _displayFont;
            }
        }

        /// <summary>The stylesheet's --font-body ("Segoe UI Variable").</summary>
        public static Font BodyFont
        {
            get
            {
                if (!_bodyResolved)
                {
                    _bodyResolved = true;
                    _bodyFont = ResolveFont("Segoe UI Variable Text", "Segoe UI", "Tahoma");
                }
                return _bodyFont;
            }
        }

        /// <summary>
        /// Resolves the first installed font from the candidate list, falling
        /// back to the engine's bundled font so text always has something to
        /// draw with.
        /// </summary>
        private static Font ResolveFont(params string[] candidates)
        {
            if (HasCommandLineArgument("-vfno-system-fonts")) return BuiltinFont();
            foreach (var candidate in candidates)
            {
                try
                {
                    var font = Font.CreateDynamicFontFromOSFont(candidate, 48);
                    // CreateDynamicFontFromOSFont substitutes silently when the
                    // family is absent, so confirm we got what we asked for
                    // before accepting it.
                    if (font != null && FontMatches(font, candidate)) return font;
                }
                catch (Exception)
                {
                    // A missing family is expected on machines without the
                    // browser build's fonts installed.
                }
            }

            return BuiltinFont();
        }

        private static bool HasCommandLineArgument(string expected)
        {
            var args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length; index++)
                if (string.Equals(args[index], expected, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static bool FontMatches(Font font, string candidate)
        {
            var name = font.name;
            if (string.IsNullOrEmpty(name)) return false;
            return name.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// The engine's bundled font. Always present, so this is the guarantee
        /// that labels are never invisible.
        /// </summary>
        private static Font BuiltinFont()
        {
            try
            {
                var builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (builtin != null) return builtin;
            }
            catch (Exception)
            {
                // Fall through to the last resort below.
            }

            try
            {
                // Any installed family will do at this point; the OS default is
                // better than no text.
                return Font.CreateDynamicFontFromOSFont("Arial", 48);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Returns the source string unchanged. Unity's legacy Text component
        /// has no true letter-spacing property; inserting Unicode spaces changes
        /// glyph widths and kerning, which made menu labels look uneven across
        /// fallback fonts. Keeping the API lets the UI builder remain shared
        /// without corrupting the displayed text.
        /// </summary>
        public static string Track(string value, float em)
        {
            return value ?? string.Empty;
        }

        // ------------------------------------------------------------------
        // Colour helpers
        // ------------------------------------------------------------------

        /// <summary>Parses "#rrggbb" or "#rrggbbaa".</summary>
        public static Color Hex(string value)
        {
            if (string.IsNullOrEmpty(value)) return Color.white;
            var span = value[0] == '#' ? value.Substring(1) : value;
            if (span.Length == 3)
            {
                return new Color(
                    HexDigit(span[0]) / 15f,
                    HexDigit(span[1]) / 15f,
                    HexDigit(span[2]) / 15f,
                    1f);
            }
            if (span.Length < 6) return Color.white;
            var r = (HexDigit(span[0]) * 16 + HexDigit(span[1])) / 255f;
            var g = (HexDigit(span[2]) * 16 + HexDigit(span[3])) / 255f;
            var b = (HexDigit(span[4]) * 16 + HexDigit(span[5])) / 255f;
            var a = span.Length >= 8
                ? (HexDigit(span[6]) * 16 + HexDigit(span[7])) / 255f
                : 1f;
            return new Color(r, g, b, a);
        }

        private static int HexDigit(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return 0;
        }

        /// <summary>CSS rgba() with 0-255 channels.</summary>
        public static Color Rgba(int r, int g, int b, float a)
        {
            return new Color(r / 255f, g / 255f, b / 255f, a);
        }

        /// <summary>Returns the colour with a replaced alpha.</summary>
        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        /// <summary>Returns the colour with its alpha multiplied.</summary>
        public static Color Fade(Color color, float multiplier)
        {
            color.a *= multiplier;
            return color;
        }

        /// <summary>
        /// The stylesheet leans on color-mix(in srgb, accent N%, other) to derive
        /// per-item borders and fills from an item's own accent. This reproduces
        /// that blend so accent-driven tinting behaves the same way here.
        /// </summary>
        public static Color Mix(Color accent, float accentPercent, Color other)
        {
            var t = Mathf.Clamp01(accentPercent * 0.01f);
            return new Color(
                Mathf.Lerp(other.r, accent.r, t),
                Mathf.Lerp(other.g, accent.g, t),
                Mathf.Lerp(other.b, accent.b, t),
                Mathf.Lerp(other.a, accent.a, t));
        }

        /// <summary>
        /// color-mix against transparent, which the stylesheet uses constantly.
        /// Only alpha scales; the visible hue stays the accent.
        /// </summary>
        public static Color MixTransparent(Color accent, float accentPercent)
        {
            return WithAlpha(accent, accent.a * Mathf.Clamp01(accentPercent * 0.01f));
        }

        /// <summary>
        /// Parses an accent supplied by content data, falling back to the
        /// stylesheet's default #67e8f9 when the value is missing or malformed.
        /// </summary>
        public static Color Accent(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return CyanLight;
            var parsed = Hex(hex);
            return parsed.a <= 0f ? CyanLight : parsed;
        }

        // ------------------------------------------------------------------
        // Legacy aliases
        // ------------------------------------------------------------------
        // Kept so existing call sites in the runtime keep compiling while the
        // views move onto the named tokens above.

        public static readonly Color CyanAccent = Cyan;
        public static readonly Color PurpleEvolution = Violet;

        private static Sprite _pixelSprite;

        /// <summary>A 1x1 opaque white sprite, tinted per Image.</summary>
        public static Sprite PixelSprite
        {
            get
            {
                if (_pixelSprite == null)
                {
                    var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                    {
                        name = "VoidFall UI Pixel",
                        hideFlags = HideFlags.HideAndDontSave,
                        wrapMode = TextureWrapMode.Clamp,
                        filterMode = FilterMode.Bilinear
                    };
                    texture.SetPixel(0, 0, Color.white);
                    texture.Apply();
                    _pixelSprite = Sprite.Create(
                        texture,
                        new Rect(0, 0, 1, 1),
                        new Vector2(0.5f, 0.5f),
                        100f,
                        0,
                        SpriteMeshType.FullRect);
                    _pixelSprite.name = "VoidFall UI Pixel";
                    _pixelSprite.hideFlags = HideFlags.HideAndDontSave;
                }
                return _pixelSprite;
            }
        }
    }
}
