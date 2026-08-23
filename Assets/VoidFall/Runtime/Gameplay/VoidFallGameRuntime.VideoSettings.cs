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
    /// to the asset's values (bloom 1.2, chromatic 0.12) when the save holds
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
        private int _appliedResolutionWidth = -1;
        private int _appliedResolutionHeight = -1;
        private int _appliedFullscreenMode = -1;

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
            if (_videoBloom == null) _videoBloom = _videoVolumeProfile.Add<Bloom>(true);
            if (_videoChromatic == null) _videoVolumeProfile.TryGet(out _videoChromatic);
            if (_videoChromatic == null) _videoChromatic = _videoVolumeProfile.Add<ChromaticAberration>(true);
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
            _videoVolume = null;
        }
    }
}
