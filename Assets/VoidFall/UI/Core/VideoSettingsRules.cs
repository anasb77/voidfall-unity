using System;
using System.Collections.Generic;
using UnityEngine;
using VoidFall.Persistence;

namespace VoidFall.UI
{
    /// <summary>
    /// Pure rules for the settings screen's VIDEO section: the resolution
    /// list distilled from the monitor's modes, the &lt; / &gt; cycling math,
    /// and the mapping between saved effect intensities and live values.
    ///
    /// Kept free of UnityEngine.Screen state so the EditMode suite can drive
    /// every rule from plain arrays, exactly like the browser build's
    /// settings helpers.
    /// </summary>
    public static class VideoSettingsRules
    {
        /// <summary>Shipped intensities from VoidFallDefaultVolumeProfile.</summary>
        public const float DefaultBloom = 1.2f;
        public const float DefaultChromatic = 0.12f;

        public const float MaxBloom = SaveSettings.MaxBloom;
        public const float MaxChromatic = SaveSettings.MaxChromatic;

        /// <summary>The cycler's first resolution entry: keep the native size.</summary>
        public const string AutoResolutionLabel = "AUTO (native)";

        /// <summary>
        /// Display-mode cycle in UI order. Values are FullScreenMode ints:
        /// 0 exclusive fullscreen, 1 fullscreen window (borderless),
        /// 3 windowed. 2 (MaximizedWindow) is a macOS dock mode and is
        /// deliberately not offered; sanitize folds it to the default.
        /// </summary>
        public static readonly int[] DisplayModeValues = { 0, 1, 3 };
        public static readonly string[] DisplayModeLabels = { "FULLSCREEN", "BORDERLESS", "WINDOWED" };

        /// <summary>Distinct sizes with their highest refresh, largest first.</summary>
        public static List<Vector2Int> BuildResolutionSizes(Resolution[] available)
        {
            var bestRefresh = new Dictionary<Vector2Int, int>();
            foreach (var mode in available ?? Array.Empty<Resolution>())
            {
                if (mode.width <= 0 || mode.height <= 0) continue;
                var size = new Vector2Int(mode.width, mode.height);
                if (bestRefresh.TryGetValue(size, out var refresh) && refresh >= mode.refreshRate) continue;
                bestRefresh[size] = mode.refreshRate;
            }

            var sizes = new List<Vector2Int>(bestRefresh.Keys);
            sizes.Sort((left, right) => left.x != right.x
                ? right.x.CompareTo(left.x)
                : right.y.CompareTo(left.y));
            return sizes;
        }

        /// <summary>
        /// Cycles an index over count entries with wrap-around in both
        /// directions. Index 0 is the AUTO entry for resolutions, so the
        /// wrap is what carries the cycle from the smallest mode back to
        /// AUTO and vice versa.
        /// </summary>
        public static int CycleIndex(int index, int count, int step)
        {
            if (count <= 1) return 0;
            var normalized = ((index % count) + count) % count;
            return ((normalized + step) % count + count) % count;
        }

        public static string ResolutionLabel(int width, int height)
        {
            return width <= 0 || height <= 0 ? AutoResolutionLabel : width + " x " + height;
        }

        /// <summary>Folds any non-cyclable FullScreenMode to the default (1).</summary>
        public static int SanitizeDisplayMode(int value)
        {
            return Array.IndexOf(DisplayModeValues, value) >= 0 ? value : 1;
        }

        /// <summary>The cycle position whose entry stores this mode value.</summary>
        public static int DisplayModeIndex(int value)
        {
            return Math.Max(0, Array.IndexOf(DisplayModeValues, SanitizeDisplayMode(value)));
        }

        /// <summary>The stored mode value at a cycle position, wrapping out-of-range input.</summary>
        public static int DisplayModeValue(int cycleIndex)
        {
            return DisplayModeValues[CycleIndex(cycleIndex, DisplayModeValues.Length, 0)];
        }

        /// <summary>Saved -1 (or any negative) means the shipped default intensity.</summary>
        public static float EffectiveBloom(float saved)
        {
            return saved < 0f ? DefaultBloom : Mathf.Clamp(saved, 0f, MaxBloom);
        }

        public static float EffectiveChromatic(float saved)
        {
            return saved < 0f ? DefaultChromatic : Mathf.Clamp(saved, 0f, MaxChromatic);
        }
    }
}
