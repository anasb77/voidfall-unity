using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VoidFall.UI
{
    /// <summary>
    /// Audio and display preferences, rebuilt from .settings-panel.
    ///
    /// Two adaptations from the browser build, both deliberate:
    ///   * Graphics quality is a segmented control rather than a native select.
    ///     The IMGUI version hand-rolled a popup that pushed the rest of the list
    ///     down as it opened; a segment row shows every option at once and never
    ///     reflows the panel.
    ///   * Values are applied from the runtime via <see cref="Apply"/>. The
    ///     previous attempt hard-coded slider positions, so opening the screen
    ///     misrepresented the saved preferences.
    /// </summary>
    public sealed class SettingsView : UIViewBase
    {
        private static readonly string[] QualityOptions = { "auto", "low", "balanced", "high" };
        private static readonly string[] QualityLabels = { "Auto", "Low power", "Balanced", "High" };

        private sealed class SliderRow
        {
            public Slider Slider;
            public Text Value;
            public bool Percentage;
        }

        private sealed class ToggleRow
        {
            public RectTransform Knob;
            public Image Track;
            public bool Value;
        }

        private readonly Dictionary<string, SliderRow> _sliders = new Dictionary<string, SliderRow>();
        private readonly Dictionary<string, ToggleRow> _toggles = new Dictionary<string, ToggleRow>();
        private readonly List<Image> _qualitySegments = new List<Image>();
        private readonly List<Text> _qualityLabels = new List<Text>();

        private RectTransform _content;
        private Text _muteLabel;
        private Text _resetLabel;
        private Image _resetSurface;
        private string _quality = "auto";
        private bool _applying;
        private bool _resetArmed;
        private float _resetArmedUntil;

        protected override void Build()
        {
            UIBuilder.CreateScrim(Root, "Blocker", new Color(0f, 0f, 0f, 0.0001f));

            var area = UIBuilder.CreateProfilePanel(
                Root,
                "Panel",
                new Vector2(620f, 660f),
                "Local preferences",
                "Settings",
                () => Callbacks?.CloseMenuPage?.Invoke(),
                out _);

            _content = UIBuilder.CreateScrollView(area, "Scroll", out _);
            UIBuilder.AddVerticalLayout(_content, 8f, new RectOffset(0, 0, 8, 8));

            AddSlider("master", "Master volume", 0f, 1f, v => Callbacks?.SetMasterVolume?.Invoke(v), true);
            AddSlider("effects", "Effects volume", 0f, 1f, v => Callbacks?.SetEffectsVolume?.Invoke(v), true);
            AddSlider("music", "Music volume", 0f, 1f, v => Callbacks?.SetMusicVolume?.Invoke(v), true);
            AddSlider("shake", "Screen shake", 0f, 1f, v => Callbacks?.SetScreenShake?.Invoke(v), true);
            AddSlider("touch", "Touch control size", 0.75f, 1.35f, v => Callbacks?.SetTouchSize?.Invoke(v), true);

            AddQualityRow();

            AddToggle(
                "reducedMotion",
                "Reduced motion",
                "Cuts shake, flashes, and particle volume.",
                v => Callbacks?.SetReducedMotion?.Invoke(v));
            AddToggle(
                "highContrast",
                "High-contrast shots",
                "Adds a white edge to player projectiles.",
                v => Callbacks?.SetHighContrast?.Invoke(v));

            AddMuteRow();
            AddExportRow();
            AddResetRow();
        }

        /// <summary>
        /// The .setting-row shell: a label column on the left and a control slot
        /// on the right.
        /// </summary>
        private RectTransform CreateRow(string name, string label, string detail, out RectTransform control)
        {
            var row = UIBuilder.CreateRect(_content, "Row." + name);
            UIBuilder.SetHeight(row, 58f);

            var surface = UIBuilder.CreateSurface(row, "Body", UISprites.Rounded(
                UITheme.RadiusRow,
                UITheme.SettingRowFill,
                UITheme.SettingRowFill,
                UITheme.BorderSettingRow));
            UIBuilder.Stretch(surface.rectTransform);

            var hasDetail = !string.IsNullOrEmpty(detail);
            var title = UIBuilder.CreateText(
                row,
                "Label",
                label,
                12f,
                UITheme.TextStrong,
                hasDetail ? TextAnchor.LowerLeft : TextAnchor.MiddleLeft,
                false);
            title.rectTransform.anchorMin = new Vector2(0f, hasDetail ? 0.5f : 0f);
            title.rectTransform.anchorMax = new Vector2(0.55f, 1f);
            title.rectTransform.offsetMin = new Vector2(11f, 0f);
            title.rectTransform.offsetMax = new Vector2(0f, hasDetail ? -8f : 0f);

            if (hasDetail)
            {
                var sub = UIBuilder.CreateText(
                    row,
                    "Detail",
                    detail,
                    10f,
                    UITheme.TextNavDetail,
                    TextAnchor.UpperLeft,
                    false);
                sub.rectTransform.anchorMin = new Vector2(0f, 0f);
                sub.rectTransform.anchorMax = new Vector2(0.62f, 0.5f);
                sub.rectTransform.offsetMin = new Vector2(11f, 8f);
                sub.rectTransform.offsetMax = Vector2.zero;
                sub.horizontalOverflow = HorizontalWrapMode.Wrap;
            }

            control = UIBuilder.CreateRect(row, "Control");
            control.anchorMin = new Vector2(0.58f, 0.5f);
            control.anchorMax = new Vector2(1f, 0.5f);
            control.pivot = new Vector2(0.5f, 0.5f);
            control.sizeDelta = new Vector2(-22f, 24f);
            control.anchoredPosition = new Vector2(-6f, 0f);
            return row;
        }

        private void AddSlider(string key, string label, float min, float max, Action<float> onChange, bool percentage)
        {
            CreateRow(key, label, null, out var control);

            var readout = UIBuilder.CreateText(
                control.parent,
                "Readout",
                "100%",
                10f,
                UITheme.TextNavDetail,
                TextAnchor.UpperLeft,
                false);
            readout.rectTransform.anchorMin = new Vector2(0f, 0f);
            readout.rectTransform.anchorMax = new Vector2(0.55f, 0.5f);
            readout.rectTransform.offsetMin = new Vector2(11f, 8f);
            readout.rectTransform.offsetMax = Vector2.zero;

            var slider = BuildSlider(control, min, max);
            var row = new SliderRow { Slider = slider, Value = readout, Percentage = percentage };
            _sliders[key] = row;

            slider.onValueChanged.AddListener(value =>
            {
                readout.text = Mathf.RoundToInt(value * 100f).ToString() + "%";
                // Suppress the callback while Apply() seeds the control, or the
                // view would immediately write the value it was just handed back.
                if (_applying) return;
                onChange?.Invoke(value);
            });
        }

        /// <summary>Builds a themed slider: rounded track, cyan fill, round handle.</summary>
        private static Slider BuildSlider(RectTransform parent, float min, float max)
        {
            var rt = UIBuilder.CreateRect(parent, "Slider");
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(0f, 18f);

            var slider = rt.gameObject.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = false;

            var track = UIBuilder.CreateSurface(rt, "Track", UISprites.Rounded(
                UITheme.RadiusSmall,
                UITheme.Rgba(2, 6, 12, 0.78f),
                UITheme.Rgba(2, 6, 12, 0.78f),
                UITheme.BorderSettingRow), true);
            track.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            track.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            track.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            track.rectTransform.sizeDelta = new Vector2(0f, 6f);

            var fillArea = UIBuilder.CreateRect(rt, "Fill Area");
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.pivot = new Vector2(0.5f, 0.5f);
            fillArea.sizeDelta = new Vector2(-10f, 6f);

            var fill = UIBuilder.CreateSurface(fillArea, "Fill", UISprites.Rounded(
                UITheme.RadiusSmall,
                UITheme.Cyan,
                UITheme.Cyan,
                default(Color)));
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;
            slider.fillRect = fill.rectTransform;

            var handleArea = UIBuilder.CreateRect(rt, "Handle Slide Area");
            handleArea.anchorMin = new Vector2(0f, 0f);
            handleArea.anchorMax = new Vector2(1f, 1f);
            handleArea.offsetMin = new Vector2(9f, 0f);
            handleArea.offsetMax = new Vector2(-9f, 0f);

            var handle = UIBuilder.CreateRect(handleArea, "Handle");
            handle.sizeDelta = new Vector2(18f, 18f);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.sprite = UISprites.Circle(48);
            handleImage.color = UITheme.CyanPale;
            handleImage.raycastTarget = true;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;

            return slider;
        }

        /// <summary>
        /// The graphics-quality segmented control. Each option is a small button
        /// and the active one carries the cyan treatment.
        /// </summary>
        private void AddQualityRow()
        {
            CreateRow(
                "quality",
                "Graphics quality",
                "Auto lowers cosmetic load before gameplay accuracy.",
                out var control);

            // Four segments need more room than a slider, so this row's control
            // column starts further left than the shared default.
            control.anchorMin = new Vector2(0.42f, 0.5f);
            control.sizeDelta = new Vector2(-22f, 30f);
            var layout = UIBuilder.AddHorizontalLayout(control, 4f, null, TextAnchor.MiddleRight);

            for (var index = 0; index < QualityOptions.Length; index++)
            {
                var option = QualityOptions[index];
                var segment = UIBuilder.CreateRect(control, "Segment." + option);

                var image = segment.gameObject.AddComponent<Image>();
                image.type = Image.Type.Sliced;
                image.sprite = QualitySprite(false);
                image.raycastTarget = true;

                var button = segment.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() =>
                {
                    _quality = option;
                    RefreshQuality();
                    Callbacks?.SetQuality?.Invoke(option);
                });

                var label = UIBuilder.CreateText(
                    segment,
                    "Label",
                    QualityLabels[index],
                    10f,
                    UITheme.TextNavDetail,
                    TextAnchor.MiddleCenter,
                    true,
                    FontStyle.Bold);

                _qualitySegments.Add(image);
                _qualityLabels.Add(label);
            }
        }

        private static Sprite QualitySprite(bool active)
        {
            return active
                ? UISprites.Rounded(UITheme.RadiusSmall, UITheme.BuyFill, UITheme.BuyFill, UITheme.BorderSelect)
                : UISprites.Rounded(UITheme.RadiusSmall, UITheme.SelectFill, UITheme.SelectFill, UITheme.BorderSettingRow);
        }

        private void RefreshQuality()
        {
            for (var index = 0; index < _qualitySegments.Count; index++)
            {
                var active = QualityOptions[index] == _quality;
                if (_qualitySegments[index] != null) _qualitySegments[index].sprite = QualitySprite(active);
                if (_qualityLabels[index] != null)
                {
                    _qualityLabels[index].color = active ? UITheme.CyanPale : UITheme.TextNavDetail;
                }
            }
        }

        /// <summary>A .toggle-track switch: a pill with a knob that slides 19px.</summary>
        private void AddToggle(string key, string label, string detail, Action<bool> onChange)
        {
            CreateRow(key, label, detail, out var control);

            var track = UIBuilder.CreateSurface(control, "Track", UISprites.Rounded(
                12f, UITheme.ToggleOff, UITheme.ToggleOff, default(Color)), true);
            track.rectTransform.anchorMin = new Vector2(1f, 0.5f);
            track.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            track.rectTransform.pivot = new Vector2(1f, 0.5f);
            track.rectTransform.sizeDelta = new Vector2(43f, 24f);
            track.rectTransform.anchoredPosition = Vector2.zero;

            var knob = UIBuilder.CreateRect(track.rectTransform, "Knob");
            knob.anchorMin = new Vector2(0f, 0.5f);
            knob.anchorMax = new Vector2(0f, 0.5f);
            knob.pivot = new Vector2(0f, 0.5f);
            knob.sizeDelta = new Vector2(18f, 18f);
            knob.anchoredPosition = new Vector2(3f, 0f);

            var knobImage = knob.gameObject.AddComponent<Image>();
            knobImage.sprite = UISprites.Circle(48);
            knobImage.color = UITheme.ToggleKnobOff;
            knobImage.raycastTarget = false;

            var state = new ToggleRow { Knob = knob, Track = track, Value = false };
            _toggles[key] = state;

            var button = track.gameObject.AddComponent<Button>();
            button.targetGraphic = track;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                state.Value = !state.Value;
                ApplyToggleVisual(state, knobImage);
                onChange?.Invoke(state.Value);
            });

            ApplyToggleVisual(state, knobImage);
        }

        private static void ApplyToggleVisual(ToggleRow state, Image knobImage)
        {
            state.Knob.anchoredPosition = new Vector2(state.Value ? 22f : 3f, 0f);
            state.Track.sprite = UISprites.Rounded(
                12f,
                state.Value ? UITheme.ToggleOn : UITheme.ToggleOff,
                state.Value ? UITheme.ToggleOn : UITheme.ToggleOff,
                default(Color));
            knobImage.color = state.Value ? UITheme.CyanPale : UITheme.ToggleKnobOff;
        }

        private void AddMuteRow()
        {
            CreateRow("mute", "Audio", "Silences music and effects.", out var control);

            var button = UIBuilder.CreateSecondaryAction(
                control.parent as RectTransform,
                "MuteToggle",
                "Mute audio  [M]",
                null,
                () =>
                {
                    Callbacks?.ToggleMute?.Invoke();
                    Manager?.RefreshMuteGlyph();
                    RefreshMuteLabel();
                },
                34f);

            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.58f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(-22f, 34f);
            rect.anchoredPosition = new Vector2(-6f, 0f);

            _muteLabel = rect.Find("Label")?.GetComponent<Text>();
            RefreshMuteLabel();
        }

        private void RefreshMuteLabel()
        {
            if (_muteLabel == null) return;
            var muted = Callbacks?.IsMuted != null && Callbacks.IsMuted();
            _muteLabel.text = muted ? "Unmute audio  [M]" : "Mute audio  [M]";
        }

        private void AddExportRow()
        {
            CreateRow(
                "export",
                "Browser-compatible save",
                "Writes a JSON profile next to the player data.",
                out var control);

            var button = UIBuilder.CreateSecondaryAction(
                control.parent as RectTransform,
                "ExportSave",
                "Export save",
                null,
                () => Callbacks?.ExportSave?.Invoke(),
                34f);

            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.58f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(-22f, 34f);
            rect.anchoredPosition = new Vector2(-6f, 0f);
        }

        /// <summary>
        /// The two-stage reset. First press arms it and relabels; it disarms itself
        /// after five seconds, matching the browser build.
        /// </summary>
        private void AddResetRow()
        {
            var spacer = UIBuilder.CreateRect(_content, "ResetSpacer");
            UIBuilder.SetHeight(spacer, 6f);

            UIBuilder.CreateDangerButton(
                _content,
                "ResetProgress",
                "Reset local progress",
                () =>
                {
                    if (!_resetArmed)
                    {
                        _resetArmed = true;
                        _resetArmedUntil = Time.unscaledTime + 5f;
                        RefreshResetVisual();
                        return;
                    }
                    _resetArmed = false;
                    RefreshResetVisual();
                    Callbacks?.ResetProgress?.Invoke();
                },
                out _resetLabel,
                out _resetSurface);

            UIBuilder.SetHeight((RectTransform)_resetLabel.transform.parent, 42f);
        }

        private void RefreshResetVisual()
        {
            if (_resetLabel != null)
            {
                _resetLabel.text = _resetArmed
                    ? "Tap again to reset all local progress"
                    : "Reset local progress";
                _resetLabel.color = _resetArmed ? UITheme.RoseBright : UITheme.RoseLight;
            }
            if (_resetSurface != null)
            {
                _resetSurface.sprite = _resetArmed
                    ? UISprites.Rounded(UITheme.RadiusRow, UITheme.ResetFillArmed, UITheme.ResetFillArmed, UITheme.Red)
                    : UISprites.Rounded(UITheme.RadiusRow, UITheme.ResetFill, UITheme.ResetFill, UITheme.BorderReset);
            }
        }

        private void Update()
        {
            if (!_resetArmed || Time.unscaledTime < _resetArmedUntil) return;
            _resetArmed = false;
            RefreshResetVisual();
        }

        /// <summary>
        /// Seeds every control from the saved preferences. Guarded so the
        /// onValueChanged handlers do not write the values straight back.
        /// </summary>
        public void Apply(UISettingsState state)
        {
            _applying = true;
            try
            {
                SetSlider("master", state.MasterVolume);
                SetSlider("effects", state.EffectsVolume);
                SetSlider("music", state.MusicVolume);
                SetSlider("shake", state.ScreenShake);
                SetSlider("touch", state.TouchSize);

                SetToggle("reducedMotion", state.ReducedMotion);
                SetToggle("highContrast", state.HighContrast);

                _quality = string.IsNullOrEmpty(state.Quality) ? "auto" : state.Quality;
                RefreshQuality();
                RefreshMuteLabel();
            }
            finally
            {
                _applying = false;
            }
        }

        private void SetSlider(string key, float value)
        {
            if (!_sliders.TryGetValue(key, out var row) || row.Slider == null) return;
            row.Slider.value = Mathf.Clamp(value, row.Slider.minValue, row.Slider.maxValue);
            if (row.Value != null)
            {
                row.Value.text = Mathf.RoundToInt(row.Slider.value * 100f).ToString() + "%";
            }
        }

        private void SetToggle(string key, bool value)
        {
            if (!_toggles.TryGetValue(key, out var row) || row.Knob == null) return;
            row.Value = value;
            var knobImage = row.Knob.GetComponent<Image>();
            if (knobImage != null) ApplyToggleVisual(row, knobImage);
        }

        protected override void OnShown()
        {
            RefreshMuteLabel();
            _resetArmed = false;
            RefreshResetVisual();
        }
    }
}
