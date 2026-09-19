using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VoidFall.UI;

namespace VoidFall.Runtime
{
    /// <summary>
    /// VIDEO settings application: the code-built global volume that drives
    /// bloom and chromatic aberration, plus resolution/display-mode changes.
    ///
    /// The volume is assembled in code rather than referencing
    /// VoidFallDefaultVolumeProfile.asset so the shipped asset stays the
    /// render identity of record; a priority-10 global volume with its own
    /// profile simply overrides the two intensities the sliders own, defaulting
    /// to the asset's values (bloom 1.2, chromatic disabled) when the save holds
    /// the -1 sentinel.
    ///
    /// Everything here is render/system side: none of it touches the sim or
    /// its Rng, so the golden master fixtures are unaffected.
    /// </summary>
    public sealed partial class VoidFallGameRuntime
    {
        private Volume _videoVolume;
        private VolumeProfile _videoVolumeProfile;
        private Bloom _videoBloom;
        private ChromaticAberration _videoChromatic;
        private ColorAdjustments _arenaColorGrade;
        private int _appliedResolutionWidth = -1;
        private int _appliedResolutionHeight = -1;
        private int _appliedFullscreenMode = -1;
        private Coroutine _monitorMove;
        private int _appliedMonitorIndex = -2;
        private readonly List<DisplayInfo> _displayLayout = new List<DisplayInfo>();

        /// <summary>
        /// Attaches the runtime video volume to the gameplay camera. Called
        /// from SetupCamera, before the save is loaded; intensities are set
        /// later by ApplyVideoSettings once settings exist.
        /// </summary>
        private void SetupVideoVolume()
        {
            if (_camera == null) return;

            // The scene serializes the camera with post-processing off, so the
            // volume would be inert without flipping it at runtime.
            var additional = _camera.GetUniversalAdditionalCameraData();
            if (additional != null) additional.renderPostProcessing = true;

            if (_videoVolume == null)
            {
                _videoVolume = _camera.GetComponent<Volume>();
                if (_videoVolume == null)
                {
                    _videoVolume = _camera.gameObject.AddComponent<Volume>();
                    _videoVolume.isGlobal = true;
                    _videoVolume.priority = 10;
                }
            }

            if (_videoVolumeProfile == null)
            {
                _videoVolumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                _videoVolumeProfile.name = "VoidFallVideoVolume";
                _videoVolume.sharedProfile = _videoVolumeProfile;
            }

            if (_videoBloom == null) _videoVolumeProfile.TryGet(out _videoBloom);
            if (_videoBloom == null) _videoBloom = _videoVolumeProfile.Add<Bloom>(false);
            if (_videoChromatic == null) _videoVolumeProfile.TryGet(out _videoChromatic);
            if (_videoChromatic == null) _videoChromatic = _videoVolumeProfile.Add<ChromaticAberration>(false);
            _videoBloom.intensity.overrideState = true;
            _videoChromatic.intensity.overrideState = true;
            if (_arenaColorGrade == null) _arenaColorGrade = _videoVolumeProfile.Add<ColorAdjustments>(false);
        }

        private void ApplyArenaColorGrade()
        {
            if (_arenaColorGrade == null) return;
            // Sakura retains its existing palette. The screen-space HUD is outside this camera grade.
            var enabled = _arenaId != VoidFall.Core.ArenaId.WhiteSakura;
            _arenaColorGrade.active = enabled;
            if (!enabled) return;
            _arenaColorGrade.contrast.Override(9f);
            _arenaColorGrade.saturation.Override(12f);
            _arenaColorGrade.postExposure.Override(-.16f);
        }

        /// <summary>
        /// Applies the persisted VIDEO preferences: display mode/resolution and
        /// post-effect intensities. Runs at boot (from ApplySettings) and after
        /// every video control change.
        /// </summary>
        internal void ApplyVideoSettings()
        {
            var settings = _saveData?.settings;
            if (settings == null) return;
            if (!Application.isEditor && settings.monitorIndex != _appliedMonitorIndex)
            {
                if (_monitorMove == null) _monitorMove = StartCoroutine(ApplyMonitorPreference());
            }
            else if (_monitorMove == null)
                ApplyResolution(settings.resolutionWidth, settings.resolutionHeight, settings.fullscreenMode);
            ApplyVideoEffects();
        }

        /// <summary>
        /// Sets the volume's effect intensities from the saved preferences,
        /// substituting the shipped defaults for the -1 sentinel. Rebuilt
        /// lazily so it also works when called before camera setup finished.
        /// </summary>
        internal void ApplyVideoEffects()
        {
            var settings = _saveData?.settings;
            if (settings == null) return;
            SetupVideoVolume();
            if (_videoBloom != null)
                _videoBloom.intensity.value = VideoSettingsRules.EffectiveBloom(settings.bloom);
            if (_videoChromatic != null)
                _videoChromatic.intensity.value = VideoSettingsRules.EffectiveChromatic(settings.chromatic);
        }

        private IEnumerator ApplyMonitorPreference()
        {
            // Yield before starting so the coroutine handle is valid even for AUTO.
            yield return null;
            while (_saveData?.settings != null && _appliedMonitorIndex != _saveData.settings.monitorIndex)
            {
                var requested = _saveData.settings.monitorIndex;
                Screen.GetDisplayLayout(_displayLayout);
                var reason = "auto";
                if (requested >= 0 && requested < _displayLayout.Count)
                {
                    var target = _displayLayout[requested];
                    // Reapply once per explicit choice/startup, even if the OS already chose this screen.
                    if (_appliedMonitorIndex != requested || !Screen.mainWindowDisplayInfo.Equals(target))
                    {
                        var position = new Vector2Int(Mathf.Max(0, (target.width - Screen.width) / 2),
                            Mathf.Max(0, (target.height - Screen.height) / 2));
                        AsyncOperation move = null;
                        try { move = Screen.MoveMainWindowTo(in target, position); }
                        catch (System.Exception error) { Debug.LogWarning("Monitor move failed: " + error.Message); }
                        if (move != null) yield return move;
                    }
                    reason = Screen.mainWindowDisplayInfo.Equals(target) ? "applied" : "move_failed";
                }
                else if (requested >= 0) reason = "disconnected_fallback";
                _appliedMonitorIndex = requested;
                RecordRunHistory("display_monitor_changed", reason: reason, amount: requested,
                    detail: "actual=" + Screen.mainWindowDisplayInfo.name + ";connected=" + _displayLayout.Count);
            }
            _monitorMove = null;
            _appliedResolutionWidth = _appliedResolutionHeight = -1;
            var settings = _saveData?.settings;
            if (settings != null) ApplyResolution(settings.resolutionWidth, settings.resolutionHeight, settings.fullscreenMode);
        }

        private void ApplyResolution(int width, int height, int fullscreenMode)
        {
            var mode = (FullScreenMode)VideoSettingsRules.SanitizeDisplayMode(fullscreenMode);
            if (width <= 0 || height <= 0)
            {
                // AUTO keeps the size Unity and the OS negotiated (native on a
                // fresh boot); only the display mode is enforced.
                width = Screen.width;
                height = Screen.height;
            }
            if (width == _appliedResolutionWidth && height == _appliedResolutionHeight &&
                (int)mode == _appliedFullscreenMode)
            {
                return;
            }

            // Screen.SetResolution is a system-side call; the change-detection
            // above keeps ApplySettings' frequent re-runs from re-issuing it.
            _appliedResolutionWidth = width;
            _appliedResolutionHeight = height;
            _appliedFullscreenMode = (int)mode;
            Screen.SetResolution(width, height, mode);
        }

        /// <summary>Releases the runtime-built volume profile on teardown.</summary>
        private void DestroyVideoVolumeResources()
        {
            if (_videoVolumeProfile != null)
            {
                Destroy(_videoVolumeProfile);
                _videoVolumeProfile = null;
            }
            _videoBloom = null;
            _videoChromatic = null;
            _arenaColorGrade = null;
            _videoVolume = null;
        }
    }
}
