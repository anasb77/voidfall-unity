using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoidFall.Runtime
{
    /// <summary>
    /// Plays the authored soundtrack. This is separate from
    /// <see cref="ProceduralAudio"/>, which synthesizes SFX and a very quiet
    /// ambient pad; when real tracks are present the pad is suppressed by the
    /// caller so the two do not layer.
    ///
    /// Tracks are discovered from Resources rather than listed in code, so
    /// dropping a new file into the folder is enough to put it in rotation.
    /// </summary>
    public sealed class MusicDirector : MonoBehaviour
    {
        public enum Channel
        {
            None,
            MainMenu,
            Gameplay,
        }

        private const string GameplayResourcePath = "VoidFall/Music/OST";
        private const string MainMenuResourcePath = "VoidFall/Music/MainMenu";

        /// <summary>
        /// Menu tracks whose opening section is an intro we skip into. More than
        /// one value means an entry point is chosen at random each time the
        /// track starts, including on loop. Keys are matched against the clip
        /// name, so a track with no entry here simply starts at zero.
        /// </summary>
        private static readonly Dictionary<string, float[]> MenuStartOffsets =
            new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "Stars", new[] { 35f } },
                { "NoGravity", new[] { 30f, 112f } },
            };

        /// <summary>
        /// TRACK SHIFT combat entry points for gameplay OST tracks (spec
        /// 50.2): authored seconds that skip quiet intros so a shifted track
        /// starts hot. Keys match clip names; add entries as tracks are
        /// curated.
        /// </summary>
        private static readonly Dictionary<string, float[]> GameplayStartOffsets =
            new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase)
            {
                // Example: { "FracturedHeaven", new[] { 18f, 111f } },
            };

        // Time constant for cross-fades, not a duration.
        private const float FadeTimeConstant = 0.32f;
        // Final gain applied on top of master x music. ProceduralAudio's pad
        // uses 0.024 because it is meant to sit almost below hearing; authored
        // tracks need real level. Started at 0.55, cut 40% to 0.33 after the
        // first playtest, then 25% to 0.2475, then 15% to 0.2104 once the SFX
        // bed was raised, then back up 10% to keep the track present.
        private const float MusicGain = 0.2314f;
        // Menu tracks get a 20% lift on top of MusicGain. The home screen has
        // no combat SFX bed competing for attention, so the theme can carry the
        // landing screen; the OST keeps the tuned level under gameplay audio.
        private const float MainMenuGainBoost = 1.2f;

        // Bomb duck. The pickup drops the track out and lets it swell back, so
        // the detonation reads in the music and not just the SFX. Fast attack
        // so the drop lands on the blast, short hold so it reads as a stumble
        // rather than a stop, and a longer eased release so the return feels
        // like the track picking itself back up.
        private const float DuckAttackSeconds = 0.045f;
        private const float DuckHoldSeconds = 0.10f;
        private const float DuckReleaseSeconds = 0.40f;
        // Not fully silent: leaving a little through keeps it a duck rather
        // than a gap, which is what stops it sounding like a bug.
        private const float DuckFloor = 0.12f;

        // Reactive playback rates. AudioSource.pitch resamples, so these shift
        // tempo and pitch together like a tape speed change, which is the effect
        // being asked for rather than a tempo-only stretch.
        private const float NormalRate = 1f;

        // Low-pass cutoffs for the submerged upgrade-screen effect. 22 kHz is
        // effectively bypassed. 390 Hz leaves only the bass and the very low
        // mids, roughly twice the muffling of the 780 Hz first pass: cutting the
        // cutoff in half removes one further octave of content, which is the
        // meaningful unit here rather than a linear Hz delta.
        private const float FilterOpenHz = 22000f;
        private const float FilterSubmergedHz = 390f;
        private const float FilterOpenResonance = 1f;
        private const float FilterSubmergedResonance = 2f;

        // Rate glide stays quick so popping overclock feels connected to the
        // input. The filter sweep is deliberately much slower: it is a mood
        // change rather than a response, and a fast dive reads as a glitch.
        private const float RateTimeConstant = 0.22f;
        private const float FilterTimeConstant = 0.6f;

        private AudioSource _source;
        private AudioClip[] _gameplayClips = Array.Empty<AudioClip>();
        private AudioClip[] _menuClips = Array.Empty<AudioClip>();

        private readonly List<int> _gameplayBag = new List<int>();
        private readonly List<int> _menuBag = new List<int>();
        private int _lastGameplayIndex = -1;
        private int _lastMenuIndex = -1;

        private readonly System.Random _rng = new System.Random();

        private Channel _channel = Channel.None;
        private Channel _pendingChannel = Channel.None;
        private bool _switching;
        private AudioClip _current;
        private float _startOffset;

        private bool _muted;
        private bool _suspended;
        private float _masterVolume = 0.8f;
        private float _musicVolume = 0.7f;

        private AudioLowPassFilter _lowPass;
        // The cross-faded level, kept separate from _source.volume so the duck
        // can multiply on top of it without the fade chasing the ducked value
        // back up and flattening the envelope.
        private float _fadeVolume;
        // Seconds into the current duck, or -1 when idle.
        private float _duckElapsed = -1f;
        // Eased 0..1 submersion. Driving one scalar and deriving cutoff and
        // resonance from it keeps the two in lockstep through the sweep.
        private float _submersion;
        private MusicReactiveState _reactiveState;
        // The quit dialog on the main menu muffles the theme like the upgrade
        // screen submerges the OST; toggled by the UI through SetMenuDialog.
        private bool _menuDialogMuffle;
        private MusicMixTargets _mixTargets = MusicStateComposer.Compose(default, 0f);
        private MusicSpectrumAnalyzer _spectrumAnalyzer;
        private MusicDspFilter _dspFilter;
        private float _criticalPulseClock;
        private float _rateSurge;

        public bool HasGameplayTracks => _gameplayClips.Length > 0;
        public bool HasMenuTracks => _menuClips.Length > 0;
        public Channel CurrentChannel => _channel;
        public string CurrentTrackName => _current != null ? _current.name : null;
        public MusicAnalysisFrame AnalysisFrame => _spectrumAnalyzer != null
            ? _spectrumAnalyzer.Current
            : MusicAnalysisFrame.Zero;
        public MusicMixTargets CurrentMixTargets => _mixTargets;
        public float[] SpectrumBands => _spectrumAnalyzer?.Bands;

        private void Awake()
        {
            // The music source lives on its own child object. Unity's audio
            // filter components apply to every AudioSource on their GameObject,
            // and ProceduralAudio puts sixteen effect voices plus its pad on the
            // runtime object, so a low-pass added there would muffle all the SFX
            // as well as the track.
            var host = new GameObject("VoidFall Music");
            host.transform.SetParent(transform, false);

            _source = host.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = true;
            _source.spatialBlend = 0f;
            // Music is not affected by AudioListener.pause; suspension is driven
            // explicitly from application focus instead.
            _source.ignoreListenerPause = true;
            _source.volume = 0f;

            _lowPass = host.AddComponent<AudioLowPassFilter>();
            _lowPass.cutoffFrequency = FilterOpenHz;
            _lowPass.lowpassResonanceQ = FilterOpenResonance;
            _dspFilter = host.AddComponent<MusicDspFilter>();
            _spectrumAnalyzer = new MusicSpectrumAnalyzer(_source);

            _gameplayClips = LoadSorted(GameplayResourcePath);
            _menuClips = LoadSorted(MainMenuResourcePath);
        }

        /// <summary>
        /// Drives the reactive layer. Safe to call every frame.
        /// </summary>
        /// <param name="upgradeScreenOpen">
        /// Submerges the track under a low-pass while the upgrade choice is up.
        /// </param>
        /// <param name="overclocked">Runs the track at 2x.</param>
        /// <param name="criticalHealth">Drags the track to 0.5x.</param>
        public void SetReactiveState(in MusicReactiveState state)
        {
            _reactiveState = state;
        }

        public void NotifyOverclockStreak(int previousStreak, int currentStreak)
        {
            if (currentStreak <= previousStreak) return;
            // Pickups now surge through the visual frame. Stacking preserves the approved 2x tape rate.
            _rateSurge = 0f;
        }

        public void NotifyPlayerDamage(float healthFraction, bool lethal)
        {
            if (_channel != Channel.Gameplay || _dspFilter == null) return;
            _dspFilter.RequestBackspin(MusicReactiveMath.DamageScratchSeconds(healthFraction, lethal));
        }

        public void ResetReactiveState()
        {
            _reactiveState = default;
            _mixTargets = MusicStateComposer.Compose(default, 0f);
            _criticalPulseClock = 0f;
            _rateSurge = 0f;
            _dspFilter?.SetStereoWidth(1f);
            _dspFilter?.ResetHistory();
            _spectrumAnalyzer?.Reset();
        }

        /// <summary>
        /// Drops the track out briefly so a bomb detonation lands in the music.
        /// Retriggerable: a second bomb restarts the envelope rather than
        /// stacking, so chained pickups cannot hold the music down.
        /// </summary>
        public void DuckForBomb()
        {
            _duckElapsed = 0f;
        }

        /// <summary>
        /// Advances the duck envelope and returns the gain multiplier. Must be
        /// called exactly once per frame.
        /// </summary>
        private float AdvanceDuck(float dt)
        {
            if (_duckElapsed < 0f) return 1f;

            _duckElapsed += dt;
            const float total = DuckAttackSeconds + DuckHoldSeconds + DuckReleaseSeconds;
            if (_duckElapsed >= total)
            {
                _duckElapsed = -1f;
                return 1f;
            }

            if (_duckElapsed < DuckAttackSeconds)
                return Mathf.Lerp(1f, DuckFloor, _duckElapsed / DuckAttackSeconds);

            if (_duckElapsed < DuckAttackSeconds + DuckHoldSeconds) return DuckFloor;

            var released = (_duckElapsed - DuckAttackSeconds - DuckHoldSeconds) / DuckReleaseSeconds;
            // Ease out, so the track swells back rather than ramping linearly.
            return Mathf.Lerp(DuckFloor, 1f, 1f - (1f - released) * (1f - released));
        }

        private static AudioClip[] LoadSorted(string resourcePath)
        {
            var loaded = Resources.LoadAll<AudioClip>(resourcePath);
            if (loaded == null || loaded.Length == 0) return Array.Empty<AudioClip>();

            var clips = new List<AudioClip>(loaded.Length);
            foreach (var clip in loaded)
            {
                if (clip != null) clips.Add(clip);
            }

            // Resources.LoadAll order is not specified. Sort so the shuffle bag
            // is built over a stable index space.
            clips.Sort((left, right) =>
                string.Compare(left.name, right.name, StringComparison.OrdinalIgnoreCase));
            return clips.ToArray();
        }

        public void SetVolumes(float master, float music)
        {
            _masterVolume = Mathf.Clamp01(master);
            _musicVolume = Mathf.Clamp01(music);
        }

        /// <summary>
        /// Engages or releases the quit-dialog mix: the menu theme is submerged
        /// the same way the upgrade screen muffles the OST, at 20% more
        /// intensity. Release glides back through the same exponential
        /// smoothing as every other mix change, so it never clicks.
        /// </summary>
        public void SetMenuDialog(bool open)
        {
            _menuDialogMuffle = open;
        }

        public void SetMuted(bool muted)
        {
            _muted = muted;
        }

        /// <summary>
        /// Suspends playback while the application is not focused. A paused game
        /// deliberately keeps its music running.
        /// </summary>
        public void SetApplicationActive(bool active)
        {
            if (_source == null) return;
            if (active)
            {
                if (!_suspended) return;
                _suspended = false;
                _source.UnPause();
            }
            else
            {
                if (_suspended) return;
                _suspended = true;
                _source.Pause();
            }
        }

        /// <summary>Rolls a fresh track for a new run and loops it.</summary>
        public void PlayGameplay()
        {
            RequestChannel(Channel.Gameplay);
        }

        public void PlayMainMenu()
        {
            RequestChannel(Channel.MainMenu);
        }

        public void Stop()
        {
            RequestChannel(Channel.None);
        }

        /// <summary>
        /// TRACK SHIFT pickup (spec section 50): crossfades the gameplay
        /// soundtrack to a different track at an authored combat entry point.
        /// Combat never pauses; one shift at a time.
        /// </summary>
        public void ShiftToNextCombatTrack()
        {
            if (!HasGameplayTracks || _channel != Channel.Gameplay || _switching) return;
            RequestChannel(Channel.Gameplay);
        }

        private void RequestChannel(Channel channel)
        {
            if (channel == Channel.Gameplay && !HasGameplayTracks) return;
            if (channel == Channel.MainMenu && !HasMenuTracks) return;

            // Menu music continues undisturbed if it is already the active
            // channel. Gameplay always re-rolls, because a run is supposed to
            // pick its own track.
            if (channel == _channel && channel != Channel.Gameplay &&
                _source != null && _source.isPlaying)
            {
                return;
            }

            if (_source == null || _channel == Channel.None || !_source.isPlaying)
            {
                BeginChannel(channel);
                return;
            }

            _pendingChannel = channel;
            _switching = true;
        }

        private void BeginChannel(Channel channel)
        {
            _pendingChannel = Channel.None;
            _switching = false;
            _channel = channel;
            _dspFilter?.ResetHistory();
            _spectrumAnalyzer?.Reset();

            if (channel == Channel.None)
            {
                _current = null;
                if (_source != null)
                {
                    _source.Stop();
                    _source.clip = null;
                    _source.volume = 0f;
                    _fadeVolume = 0f;
                }
                return;
            }

            var clips = channel == Channel.Gameplay ? _gameplayClips : _menuClips;
            var index = channel == Channel.Gameplay
                ? NextFromBag(_gameplayBag, clips.Length, ref _lastGameplayIndex)
                : NextFromBag(_menuBag, clips.Length, ref _lastMenuIndex);

            if (index < 0 || index >= clips.Length)
            {
                _channel = Channel.None;
                _current = null;
                return;
            }

            _current = clips[index];
            _startOffset = PickStartOffset(_current.name);
            StartCurrent(0f);
        }

        private void StartCurrent(float initialVolume)
        {
            if (_source == null || _current == null) return;

            _source.Stop();
            _source.clip = _current;
            // A track that starts at zero can use the engine's seamless loop.
            // One that skips an intro has to be restarted manually so it returns
            // to the offset rather than to the intro.
            _source.loop = _startOffset <= 0.01f;
            var latestStart = Mathf.Max(0f, _current.length - 1f);
            _source.time = Mathf.Clamp(_startOffset, 0f, latestStart);
            // initialVolume is the pre-duck level. Update re-applies the duck
            // multiplier next frame, so seeding both with it is correct.
            _fadeVolume = initialVolume;
            _source.volume = initialVolume;
            _source.Play();
            if (_suspended) _source.Pause();
        }

        private void RestartCurrent()
        {
            // Re-roll the entry point so a track with several of them does not
            // settle onto one for the rest of the session.
            if (_channel == Channel.MainMenu && _current != null)
                _startOffset = PickStartOffset(_current.name);

            // Preserve level so a loop boundary is not audible as a dip. Reads
            // the pre-duck level, not _source.volume: restarting mid-duck would
            // otherwise capture the ducked value as the new base and leave the
            // track quiet for good.
            StartCurrent(_fadeVolume);
        }

        private float PickStartOffset(string clipName)
        {
            if (string.IsNullOrEmpty(clipName)) return 0f;
            if (MenuStartOffsets.TryGetValue(clipName, out var options) &&
                options != null && options.Length > 0)
            {
                return options.Length == 1 ? options[0] : options[_rng.Next(options.Length)];
            }
            // TRACK SHIFT combat entry points for OST tracks (spec 50.2):
            // authored seconds that skip quiet intros so the next track
            // starts hot. A track with no entry here starts at zero.
            if (GameplayStartOffsets.TryGetValue(clipName, out var combatOptions) &&
                combatOptions != null && combatOptions.Length > 0)
            {
                return combatOptions.Length == 1
                    ? combatOptions[0]
                    : combatOptions[_rng.Next(combatOptions.Length)];
            }
            return 0f;
        }

        /// <summary>
        /// Shuffle bag: every track plays once per cycle in random order, and a
        /// refill cannot repeat the track that just finished. With a single
        /// track it degenerates to that track; with two it alternates.
        /// </summary>
        private int NextFromBag(List<int> bag, int count, ref int lastIndex)
        {
            if (count <= 0) return -1;
            if (count == 1)
            {
                lastIndex = 0;
                return 0;
            }

            if (bag.Count == 0)
            {
                for (var index = 0; index < count; index++) bag.Add(index);

                for (var index = bag.Count - 1; index > 0; index--)
                {
                    var swap = _rng.Next(index + 1);
                    var held = bag[index];
                    bag[index] = bag[swap];
                    bag[swap] = held;
                }

                // The bag is drained from the end, so the tail is what plays
                // next. Push it away from the previous cycle's last track.
                var tail = bag.Count - 1;
                if (bag[tail] == lastIndex)
                {
                    var swap = _rng.Next(tail);
                    var held = bag[tail];
                    bag[tail] = bag[swap];
                    bag[swap] = held;
                }
            }

            var pick = bag[bag.Count - 1];
            bag.RemoveAt(bag.Count - 1);
            lastIndex = pick;
            return pick;
        }

        private float ResolveVolume()
        {
            if (_muted || _channel == Channel.None) return 0f;
            // Per-channel gain: menu tracks ride 20% hotter, gameplay keeps
            // the tuned MusicGain under the SFX bed. The cross-fade glides
            // toward whichever level the active channel resolves, so the
            // menu-to-run transition stays smooth.
            var gain = _channel == Channel.MainMenu ? MusicGain * MainMenuGainBoost : MusicGain;
            return _masterVolume * _musicVolume * gain;
        }

        /// <summary>
        /// Glides rate and filter toward their targets. Stepping straight to a
        /// new value would click, and an instant tempo jump reads as a glitch
        /// rather than a reaction.
        /// </summary>
        private void ApplyReactiveState()
        {
            var dt = Time.unscaledDeltaTime;
            _criticalPulseClock += dt * 1.6f;
            var pulse = _reactiveState.CriticalHealth
                ? 0.5f + Mathf.Sin(_criticalPulseClock * Mathf.PI * 2f) * 0.5f
                : 0f;
            _mixTargets = _menuDialogMuffle
                ? MusicStateComposer.ComposeMenuDialog()
                : MusicStateComposer.Compose(_reactiveState, pulse);
            _rateSurge = Mathf.MoveTowards(_rateSurge, 0f, dt * 0.22f);
            _source.pitch = Mathf.Lerp(
                _source.pitch,
                _mixTargets.PlaybackRate + _rateSurge,
                1f - Mathf.Exp(-dt / RateTimeConstant));

            _dspFilter?.SetStereoWidth(_mixTargets.StereoWidth);

            if (_lowPass == null) return;

            _submersion = Mathf.Lerp(
                _submersion,
                _mixTargets.Submersion,
                1f - Mathf.Exp(-dt / FilterTimeConstant));

            // Swept in log space. Hearing maps frequency logarithmically, so a
            // linear ramp in Hz burns most of its travel in the top octaves
            // where nothing audible changes, then lurches through the low end
            // at the finish. Interpolating the exponent instead spreads the
            // dive evenly across the octaves you can actually hear it in.
            _lowPass.cutoffFrequency = Mathf.Lerp(
                _lowPass.cutoffFrequency,
                _mixTargets.LowPassHz,
                1f - Mathf.Exp(-dt / FilterTimeConstant));
            _lowPass.lowpassResonanceQ = Mathf.Lerp(
                _lowPass.lowpassResonanceQ,
                _mixTargets.LowPassResonance,
                1f - Mathf.Exp(-dt / FilterTimeConstant));
        }

        private void Update()
        {
            if (_source == null) return;

            // Runs before the switching early-out so the filter and rate keep
            // settling through a cross-fade.
            ApplyReactiveState();
            _spectrumAnalyzer?.Update(Time.unscaledDeltaTime);

            var blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime / FadeTimeConstant);
            // Advanced once per frame regardless of branch, so a duck that
            // overlaps a channel switch still resolves instead of sticking.
            var duck = AdvanceDuck(Time.unscaledDeltaTime);

            if (_switching)
            {
                _fadeVolume = Mathf.Lerp(_fadeVolume, 0f, blend);
                _source.volume = _fadeVolume * duck;
                if (_fadeVolume <= 0.005f || !_source.isPlaying)
                    BeginChannel(_pendingChannel);
                return;
            }

            _fadeVolume = Mathf.Lerp(_fadeVolume, ResolveVolume(), blend);
            _source.volume = _fadeVolume * duck;

            if (_channel == Channel.None || _source.loop || _suspended) return;

            // Manual loop for tracks that skip an intro. isPlaying also reads
            // false while suspended, which the guard above excludes, so reaching
            // here with a stopped source means the track ran to its end.
            if (!_source.isPlaying && _current != null) RestartCurrent();
        }
    }
}
