using NUnit.Framework;
using UnityEngine;
using VoidFall.Persistence;
using VoidFall.UI;

namespace VoidFall.Tests.Editor
{
    /// <summary>
    /// Covers the VIDEO settings section's pure rules: the resolution list
    /// distilled from monitor modes, cycler wrap-around, display-mode value
    /// mapping, old-save defaults, and effect intensity clamping.
    /// </summary>
    public sealed class VideoSettingsTests
    {
        private static Resolution Mode(int width, int height, int refresh)
        {
            return new Resolution { width = width, height = height, refreshRate = refresh };
        }

        [Test]
        public void Resolution_list_collapses_duplicate_sizes_to_one_entry()
        {
            var sizes = VideoSettingsRules.BuildResolutionSizes(new[]
            {
                Mode(1920, 1080, 60),
                Mode(1920, 1080, 144),
                Mode(1920, 1080, 120),
            });

            Assert.That(sizes.Count, Is.EqualTo(1), "duplicate sizes must collapse");
            Assert.That(sizes[0], Is.EqualTo(new Vector2Int(1920, 1080)));
        }

        [Test]
        public void Resolution_list_ignores_unusable_modes()
        {
            var sizes = VideoSettingsRules.BuildResolutionSizes(new[]
            {
                Mode(0, 0, 60),
                Mode(0, 1080, 60),
                Mode(-1920, 1080, 60),
                Mode(1280, 720, 60),
            });

            Assert.That(sizes.Count, Is.EqualTo(1));
            Assert.That(sizes[0], Is.EqualTo(new Vector2Int(1280, 720)));
        }

        [Test]
        public void Resolution_list_sorts_sizes_descending()
        {
            var sizes = VideoSettingsRules.BuildResolutionSizes(new[]
            {
                Mode(1280, 720, 60),
                Mode(3840, 2160, 60),
                Mode(2560, 1440, 60),
                Mode(1920, 1080, 144),
                Mode(1920, 1200, 60),
            });

            Assert.That(sizes, Is.EqualTo(new[]
            {
                new Vector2Int(3840, 2160),
                new Vector2Int(2560, 1440),
                new Vector2Int(1920, 1200),
                new Vector2Int(1920, 1080),
                new Vector2Int(1280, 720),
            }));
        }

        [Test]
        public void Cycle_index_wraps_in_both_directions()
        {
            // Four entries: AUTO plus three sizes.
            Assert.That(VideoSettingsRules.CycleIndex(0, 4, -1), Is.EqualTo(3), "back from the first wraps to the last");
            Assert.That(VideoSettingsRules.CycleIndex(3, 4, 1), Is.EqualTo(0), "forward from the last wraps to AUTO");
            Assert.That(VideoSettingsRules.CycleIndex(1, 4, 1), Is.EqualTo(2));
            Assert.That(VideoSettingsRules.CycleIndex(1, 4, -1), Is.EqualTo(0));
            Assert.That(VideoSettingsRules.CycleIndex(4, 4, 0), Is.EqualTo(0), "out-of-range input normalizes");
            Assert.That(VideoSettingsRules.CycleIndex(0, 0, 1), Is.EqualTo(0), "an empty cycle stays at zero");
        }

        [Test]
        public void Display_mode_cycle_offers_three_values_and_folds_invalid_ones()
        {
            Assert.That(VideoSettingsRules.DisplayModeValues, Is.EqualTo(new[] { 0, 1, 3 }));
            Assert.That(VideoSettingsRules.DisplayModeLabels.Length, Is.EqualTo(VideoSettingsRules.DisplayModeValues.Length));

            Assert.That(VideoSettingsRules.DisplayModeIndex(0), Is.EqualTo(0), "exclusive fullscreen");
            Assert.That(VideoSettingsRules.DisplayModeIndex(1), Is.EqualTo(1), "borderless fullscreen window");
            Assert.That(VideoSettingsRules.DisplayModeIndex(3), Is.EqualTo(2), "windowed");
            Assert.That(VideoSettingsRules.DisplayModeIndex(2), Is.EqualTo(1), "maximized window folds to the default");
            Assert.That(VideoSettingsRules.DisplayModeIndex(99), Is.EqualTo(1));

            Assert.That(VideoSettingsRules.DisplayModeValue(0), Is.EqualTo(0));
            Assert.That(VideoSettingsRules.DisplayModeValue(1), Is.EqualTo(1));
            Assert.That(VideoSettingsRules.DisplayModeValue(2), Is.EqualTo(3));
            Assert.That(VideoSettingsRules.DisplayModeValue(3), Is.EqualTo(0), "out-of-range cycle positions wrap");
        }

        [Test]
        public void Old_save_json_deserializes_to_auto_video_defaults()
        {
            // A pre-VIDEO profile has none of the new fields; JsonUtility
            // leaves them at their initializers, which are the auto values.
            var legacy = JsonUtility.FromJson<SaveData>(
                "{\"version\":5,\"parts\":10,\"settings\":{\"masterVolume\":0.8,\"quality\":\"high\"}}");

            Assert.That(legacy, Is.Not.Null);
            Assert.That(legacy.settings.resolutionWidth, Is.Zero);
            Assert.That(legacy.settings.resolutionHeight, Is.Zero);
            Assert.That(legacy.settings.fullscreenMode, Is.EqualTo(1));
            Assert.That(legacy.settings.bloom, Is.EqualTo(-1f));
            Assert.That(legacy.settings.chromatic, Is.EqualTo(-1f));

            // Sanitize keeps the auto meaning instead of inventing a mode.
            var sanitized = SaveStore.Sanitize(legacy);
            Assert.That(sanitized.settings.resolutionWidth, Is.Zero);
            Assert.That(sanitized.settings.resolutionHeight, Is.Zero);
            Assert.That(sanitized.settings.fullscreenMode, Is.EqualTo(1));
            Assert.That(sanitized.settings.bloom, Is.EqualTo(-1f));
            Assert.That(sanitized.settings.chromatic, Is.EqualTo(-1f));
        }

        [Test]
        public void Sanitize_preserves_valid_video_preferences()
        {
            var data = SaveStore.CreateDefault();
            data.settings.resolutionWidth = 2560;
            data.settings.resolutionHeight = 1440;
            data.settings.fullscreenMode = 3;
            data.settings.bloom = 0.4f;
            data.settings.chromatic = 0.2f;

            var sanitized = SaveStore.Sanitize(data);

            Assert.That(sanitized.settings.resolutionWidth, Is.EqualTo(2560));
            Assert.That(sanitized.settings.resolutionHeight, Is.EqualTo(1440));
            Assert.That(sanitized.settings.fullscreenMode, Is.EqualTo(3));
            Assert.That(sanitized.settings.bloom, Is.EqualTo(0.4f));
            Assert.That(sanitized.settings.chromatic, Is.EqualTo(0.2f));
        }

        [Test]
        public void Sanitize_repairs_half_written_resolutions_and_out_of_range_values()
        {
            var data = SaveStore.CreateDefault();
            data.settings.resolutionWidth = -640;
            data.settings.resolutionHeight = 1080;
            data.settings.fullscreenMode = 42;
            data.settings.bloom = 9f;
            data.settings.chromatic = 3f;

            var sanitized = SaveStore.Sanitize(data);

            Assert.That(sanitized.settings.resolutionWidth, Is.Zero, "a half-written pair collapses to auto");
            Assert.That(sanitized.settings.resolutionHeight, Is.Zero);
            Assert.That(sanitized.settings.fullscreenMode, Is.EqualTo(1));
            Assert.That(sanitized.settings.bloom, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(sanitized.settings.chromatic, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void Sanitize_normalizes_any_negative_effect_to_the_default_sentinel()
        {
            var data = SaveStore.CreateDefault();
            data.settings.bloom = -5f;
            data.settings.chromatic = float.NaN;

            var sanitized = SaveStore.Sanitize(data);

            Assert.That(sanitized.settings.bloom, Is.EqualTo(-1f));
            Assert.That(sanitized.settings.chromatic, Is.EqualTo(-1f));
        }

        [Test]
        public void Effective_intensities_substitute_defaults_and_clamp()
        {
            Assert.That(VideoSettingsRules.EffectiveBloom(-1f), Is.EqualTo(1.2f).Within(0.0001f), "sentinel means the shipped 1.2");
            Assert.That(VideoSettingsRules.EffectiveBloom(0f), Is.Zero);
            Assert.That(VideoSettingsRules.EffectiveBloom(0.75f), Is.EqualTo(0.75f).Within(0.0001f));
            Assert.That(VideoSettingsRules.EffectiveBloom(9f), Is.EqualTo(2f).Within(0.0001f), "the slider range tops out at 2");

            Assert.That(VideoSettingsRules.EffectiveChromatic(-1f), Is.EqualTo(0.12f).Within(0.0001f), "sentinel means the shipped 0.12");
            Assert.That(VideoSettingsRules.EffectiveChromatic(0.25f), Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(VideoSettingsRules.EffectiveChromatic(2f), Is.EqualTo(0.5f).Within(0.0001f), "the slider range tops out at 0.5");
        }

        [Test]
        public void Resolution_label_marks_auto_and_formats_explicit_sizes()
        {
            Assert.That(VideoSettingsRules.ResolutionLabel(0, 0), Is.EqualTo("AUTO (native)"));
            Assert.That(VideoSettingsRules.ResolutionLabel(2560, 1440), Is.EqualTo("2560 x 1440"));
        }
    }
}
