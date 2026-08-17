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

        // Time constant for cross-fades, not a duration.
        private const float FadeTimeConstant = 0.32f;
        // Final gain applied on top of master x music. ProceduralAudio's pad
        // uses 0.024 because it is meant to sit almost below hearing; authored
        // tracks need real level. Started at 0.55, cut 40% to 0.33 after the
        // first playtest, then 25% to 0.2475, then a further 15% because the
        // track still sat over the SFX bed.
        private const float MusicGain = 0.2104f;

        // Reactive playback rates. AudioSource.pitch resamples, so these shift
        // tempo and pitch together like a tape speed change, which is the effect
        // being asked for rather than a tempo-only stretch.
        private const float NormalRate = 1f;
        private const float OverclockRate = 1.4f;
        private const float CriticalRate = 0.5f;

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
        // Eased 0..1 submersion. Driving one scalar and deriving cutoff and
        // resonance from it keeps the two in lockstep through the sweep.
        private float _submersion;
        private bool _upgradeScreenOpen;
        private bool _overclocked;
        private bool _criticalHealth;

        public bool HasGameplayTracks => _gameplayClips.Length > 0;
        public bool HasMenuTracks => _menuClips.Length > 0;
        public Channel CurrentChannel => _channel;
        public string CurrentTrackName => _current != null ? _current.name : null;

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

            _gameplayClips = LoadSorted(GameplayResourcePath);
            _menuClips = LoadSorted(MainMenuResourcePath);
        }

        /// <summary>
        /// Drives the reactive layer. Safe to call every frame.
        /// </summary>
        /// <param name="upgradeScreenOpen">
        /// Submerges the track under a low-pass while the upgrade choice is up.
        /// </param>
        /// <param name="overclocked">Runs the track at 1.5x.</param>
        /// <param name="criticalHealth">Drags the track to 0.5x.</param>
        public void SetReactiveState(bool upgradeScreenOpen, bool overclocked, bool criticalHealth)
        {
            _upgradeScreenOpen = upgradeScreenOpen;
            _overclocked = overclocked;
            _criticalHealth = criticalHealth;
        }

        private float TargetRate()
        {
            // Menu music is never modulated.
            if (_channel != Channel.Gameplay) return NormalRate;

            // The upgrade screen is a deliberate lull, so it settles the rate to
            // neutral and lets the filter carry the whole effect. Without this a
            // player who levels up mid-overclock would get 1.5x and submerged at
            // once, which just sounds broken.
            if (_upgradeScreenOpen) return NormalRate;

            // Overclock outranks critical health on purpose: it is the short-lived
            // state the player just triggered, so it has to feel responsive.
            // Critical health is a sustained condition and reads fine once the
            // burst ends. Swap these two lines to invert that.
            if (_overclocked) return OverclockRate;
            if (_criticalHealth) return CriticalRate;
            return NormalRate;
        }

        private float TargetSubmersion()
        {
            return _channel == Channel.Gameplay && _upgradeScreenOpen ? 1f : 0f;
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

            if (channel == Channel.None)
            {
                _current = null;
                if (_source != null)
                {
                    _source.Stop();
                    _source.clip = null;
                    _source.volume = 0f;
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
            _startOffset = channel == Channel.MainMenu ? PickStartOffset(_current.name) : 0f;
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

            // Preserve level so a loop boundary is not audible as a dip.
            StartCurrent(_source != null ? _source.volume : 0f);
        }

        private float PickStartOffset(string clipName)
        {
            if (string.IsNullOrEmpty(clipName)) return 0f;
            if (!MenuStartOffsets.TryGetValue(clipName, out var options)) return 0f;
            if (options == null || options.Length == 0) return 0f;
            if (options.Length == 1) return options[0];
            return options[_rng.Next(options.Length)];
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
            return _masterVolume * _musicVolume * MusicGain;
        }

        /// <summary>
        /// Glides rate and filter toward their targets. Stepping straight to a
        /// new value would click, and an instant tempo jump reads as a glitch
        /// rather than a reaction.
        /// </summary>
        private void ApplyReactiveState()
        {
            var dt = Time.unscaledDeltaTime;
            _source.pitch = Mathf.Lerp(
                _source.pitch,
                TargetRate(),
                1f - Mathf.Exp(-dt / RateTimeConstant));

            if (_lowPass == null) return;

            _submersion = Mathf.Lerp(
                _submersion,
                TargetSubmersion(),
                1f - Mathf.Exp(-dt / FilterTimeConstant));

            // Swept in log space. Hearing maps frequency logarithmically, so a
            // linear ramp in Hz burns most of its travel in the top octaves
            // where nothing audible changes, then lurches through the low end
            // at the finish. Interpolating the exponent instead spreads the
            // dive evenly across the octaves you can actually hear it in.
            _lowPass.cutoffFrequency = Mathf.Exp(
                Mathf.Lerp(Mathf.Log(FilterOpenHz), Mathf.Log(FilterSubmergedHz), _submersion));
            _lowPass.lowpassResonanceQ = Mathf.Lerp(
                FilterOpenResonance,
                FilterSubmergedResonance,
                _submersion);
        }

        private void Update()
        {
            if (_source == null) return;

            // Runs before the switching early-out so the filter and rate keep
            // settling through a cross-fade.
            ApplyReactiveState();

            var blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime / FadeTimeConstant);

            if (_switching)
            {
                _source.volume = Mathf.Lerp(_source.volume, 0f, blend);
                if (_source.volume <= 0.005f || !_source.isPlaying)
                    BeginChannel(_pendingChannel);
                return;
            }

            _source.volume = Mathf.Lerp(_source.volume, ResolveVolume(), blend);

            if (_channel == Channel.None || _source.loop || _suspended) return;

            // Manual loop for tracks that skip an intro. isPlaying also reads
            // false while suspended, which the guard above excludes, so reaching
            // here with a stopped source means the track ran to its end.
            if (!_source.isPlaying && _current != null) RestartCurrent();
        }
    }
}
