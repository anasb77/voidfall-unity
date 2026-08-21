using System;
using UnityEngine;
using UnityEngine.UI;

namespace VoidFall.UI
{
    /// <summary>
    /// The local diagnostics readout, rebuilt from .perf-readout: a small
    /// monospace-feel panel with an export action, toggled with F3.
    /// </summary>
    public sealed class DebugOverlayView : UIViewBase
    {
        private Text _readout;
        private bool _shown;
        private float _smoothedFrameMs;
        private int _enemies;
        private int _projectiles;
        private int _pickups;

        protected override void Build()
        {
            var panel = UIBuilder.CreateRect(Root, "Panel");
            panel.anchorMin = new Vector2(0f, 1f);
            panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.sizeDelta = new Vector2(250f, 96f);
            panel.anchoredPosition = new Vector2(12f, -120f);

            var surface = UIBuilder.CreateSurface(panel, "Body", UISprites.Rounded(
                UITheme.RadiusBadge,
                UITheme.DebugFill,
                UITheme.DebugFill,
                UITheme.WithAlpha(UITheme.GreenDebug, 0.18f)));
            UIBuilder.Stretch(surface.rectTransform);

            _readout = UIBuilder.CreateText(
                panel,
                "Readout",
                string.Empty,
                10f,
                UITheme.GreenDebug,
                TextAnchor.UpperLeft,
                false);
            _readout.rectTransform.offsetMin = new Vector2(7f, 30f);
            _readout.rectTransform.offsetMax = new Vector2(-7f, -5f);
            _readout.lineSpacing = 6f;

            var export = UIBuilder.CreateRect(panel, "Export");
            export.anchorMin = new Vector2(0f, 0f);
            export.anchorMax = new Vector2(1f, 0f);
            export.pivot = new Vector2(0.5f, 0f);
            export.sizeDelta = new Vector2(-14f, 22f);
            export.anchoredPosition = new Vector2(0f, 6f);

            var buttonImage = export.gameObject.AddComponent<Image>();
            buttonImage.type = Image.Type.Sliced;
            buttonImage.sprite = UISprites.Rounded(
                UITheme.RadiusPip,
                UITheme.Rgba(20, 83, 45, 0.38f),
                UITheme.Rgba(20, 83, 45, 0.38f),
                UITheme.WithAlpha(UITheme.GreenDebug, 0.35f));

            var button = export.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => Callbacks?.ExportTelemetry?.Invoke());

            UIBuilder.CreateText(
                export,
                "Label",
                "Export run data  [F2]",
                10f,
                UITheme.GreenDebugLabel,
                TextAnchor.MiddleCenter,
                false);
        }

        /// <summary>Flips visibility, mirroring the runtime's F3 handler.</summary>
        public void Toggle()
        {
            _shown = !_shown;
            gameObject.SetActive(_shown);
        }

        public bool IsShown => _shown;

        /// <summary>Receives the per-frame counts the runtime already tracks.</summary>
        public void UpdateDiagnostics(int enemies, int projectiles, int pickups)
        {
            _enemies = enemies;
            _projectiles = projectiles;
            _pickups = pickups;
        }

        private void Update()
        {
            if (!_shown || _readout == null) return;

            var frameMs = Time.unscaledDeltaTime * 1000f;
            // A light smoothing keeps the number readable without hiding spikes.
            _smoothedFrameMs = _smoothedFrameMs <= 0f
                ? frameMs
                : Mathf.Lerp(_smoothedFrameMs, frameMs, 0.1f);
            var fps = _smoothedFrameMs > 0.0001f ? Mathf.RoundToInt(1000f / _smoothedFrameMs) : 0;
            var memory = GC.GetTotalMemory(false) / (1024f * 1024f);

            _readout.text =
                fps.ToString() + " FPS  " + _smoothedFrameMs.ToString("0.0") + " ms\n" +
                "Enemies " + _enemies.ToString() + "   Shots " + _projectiles.ToString() + "\n" +
                "Pickups " + _pickups.ToString() + "   Managed " + memory.ToString("0") + " MB";
        }

        public override void SetVisible(bool visible)
        {
            // Owned by the F3 toggle rather than screen state, so screen changes
            // must not close it.
            if (!visible && !_shown) gameObject.SetActive(false);
        }
    }
}
