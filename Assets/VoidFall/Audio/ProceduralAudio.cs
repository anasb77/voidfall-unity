using System;
using System.Threading;
using UnityEngine;

namespace VoidFall.Runtime
{
    /// <summary>
    /// Small zero-asset SFX bank. The browser reference synthesizes its cues;
    /// Unity keeps the same constraint and generates short cached clips once.
    /// </summary>
    public sealed class ProceduralAudio : MonoBehaviour
    {
        public enum Cue
        {
            Fire,
            Hit,
            Die,
            Crit,
            Pickup,
            Gem,
            Harvest,
            LevelUp,
            Evolution,
            Warning,
            Dash,
            Bomb,
            Explosion,
            Elite,
            Boss,
            BossCharge,
            BossSlam,
            BossDeath,
            GameOver,
            Ui,
            Railgun,
            Scattergun,
            Arc,
            Rusher,
            Seeker,
            FuseWarning,
            ExploderBlast,
            GunnerShot,
            BladeLaunch,
            ShieldBreak,
            Currency,
            Milestone,
            MilestoneMajor,
            Hurt,
            Pause,
        }

        private const int DefaultSampleRate = 44100;
        private const int EffectVoiceCount = 16;
        private static int SampleRate = DefaultSampleRate;
        private static readonly double[] MusicPadFrequencies = { 55d, 82.41d, 110d, 164.81d };
        private static float[] SharedNoiseBuffer = CreateSharedNoiseBuffer(DefaultSampleRate);
        private readonly AudioClip[] _clips = new AudioClip[Enum.GetValues(typeof(Cue)).Length];
        private readonly AudioClip[] _fireClips = new AudioClip[17];
        private readonly AudioClip[] _hitClips = new AudioClip[17];
        private readonly AudioClip[] _dieClips = new AudioClip[17];
        private readonly AudioClip[] _currencyClips = new AudioClip[7];
        private readonly AudioClip[] _gemClips = new AudioClip[25];
        private readonly AudioClip[] _fuseWarningClips = new AudioClip[6];
        private AudioSource[] _effectSources;
        private int _nextEffectSource;
        private AudioSource _musicSource;
        private AudioClip _musicClip;
        private bool _muted;
        // Overlap-driven bus limiter: dense fights stack up to 16 voices, so
        // each new voice is ducked by how many started in the last 80ms.
        // Quiet moments play untouched; only pile-ups get pulled back, which
        // keeps the requested loudness without hard clipping.
        private readonly float[] _voiceStartTimes = new float[16];
        private int _voiceStartIndex;
        // Headroom reserved on top of the master setting. The browser used 0.58;
        // raised 35% because the port's SFX bed sat too quietly against the
        // authored soundtrack, then a further 20% on top of that (0.783 -> 0.9396).
        //
        // Per-voice output is now 0.9396 * master(0.8) * effects(0.9) = 0.677.
        // A single voice has headroom, but sixteen can sound at once, so dense
        // fights now sum much closer to the ceiling. If that starts to crunch the
        // answer is a limiter on the effect bus rather than pulling this back
        // down, since lowering it just undoes the requested loudness.
        private const float EffectsMasterGain = 0.9396f;

        // The gain the procedural ambience pad was balanced against, before the
        // latest boost.
        private const float PadReferenceGain = 0.783f;

        // The pad hangs off the same master as the effect voices, so raising the
        // effects bed would drag the ambience up with it. Scaling the pad's own
        // coefficient by the pre-boost gain holds it exactly where it was, which
        // is what "louder game, same music" asks for.
        private const float MusicPadGain = 0.024f * PadReferenceGain / EffectsMasterGain;

        private float _effectsVolume = 0.9f;
        private float _effectsTargetVolume = 0.9f;
        private float _masterSettingVolume = 0.8f;
        private float _masterVolume = EffectsMasterGain * 0.8f;
        private float _masterTargetVolume = EffectsMasterGain * 0.8f;
        private float _musicVolume = 0.7f;
        private float _musicTargetVolume;
        private float _musicBlendTimeConstant = 1.6f;
        private float _musicStopAt = -1f;
        // The browser keeps the pad as live oscillators -> low-pass filter ->
        // gain nodes. A baked looping clip would reset those phases every
        // loop, so the streaming reader carries them continuously instead.
        private int _musicPadResetRequested;
        private long _musicPadSampleCursor;
        private float _musicPadFilterX1;
        private float _musicPadFilterX2;
        private float _musicPadFilterY1;
        private float _musicPadFilterY2;
        private readonly float[] _lastPlayedAt = new float[Enum.GetValues(typeof(Cue)).Length];
        private readonly bool[] _hasPlayed = new bool[Enum.GetValues(typeof(Cue)).Length];

        public bool Muted => _muted;

        private void Awake()
        {
            // The browser creates its AudioContext at the active device sample
            // rate. Match that rate for generated clips and the shared noise
            // buffer so the procedural graph is not needlessly resampled on
            // 48 kHz (or other supported) output devices.
            var outputSampleRate = AudioSettings.outputSampleRate;
            SampleRate = outputSampleRate > 0 ? outputSampleRate : DefaultSampleRate;
            SharedNoiseBuffer = CreateSharedNoiseBuffer(SampleRate);
            var savedMute = PlayerPrefs.GetInt(
                "voidfall_muted",
                PlayerPrefs.GetInt("service_yard_muted", 0));
            _muted = savedMute == 1;
            _effectSources = new AudioSource[EffectVoiceCount];
            for (var index = 0; index < _effectSources.Length; index++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0;
                source.ignoreListenerPause = true;
                source.volume = _masterVolume * _effectsVolume;
                _effectSources[index] = source;
            }
            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.playOnAwake = false;
            _musicSource.loop = true;
            _musicSource.spatialBlend = 0;
            _musicSource.ignoreListenerPause = true;
            BuildClips();
            BuildParameterizedClips();
            _musicClip = BuildMusicPad("vf_music_pad", 8f);
            _masterVolume = _muted ? 0 : _masterTargetVolume;
            _masterTargetVolume = _muted ? 0 : EffectsMasterGain * _masterSettingVolume;
            _musicTargetVolume = _muted ? 0 : MusicVolume();
            _musicSource.volume = 0;
        }

        public void SetVolumes(float master, float effects, float music = 0.7f)
        {
            _masterSettingVolume = Mathf.Clamp01(master);
            _masterTargetVolume = _muted ? 0 : EffectsMasterGain * _masterSettingVolume;
            _effectsTargetVolume = Mathf.Clamp01(effects);
            _musicVolume = Mathf.Clamp01(music);
            if (_musicStopAt < 0)
            {
                _musicTargetVolume = _muted ? 0 : MusicVolume(_masterTargetVolume);
                _musicBlendTimeConstant = 0.4f;
            }
            if (_musicSource != null && !_musicSource.isPlaying)
                _musicSource.volume = 0;
        }

        public void SetMuted(bool muted)
        {
            _muted = muted;
            PlayerPrefs.SetInt("voidfall_muted", muted ? 1 : 0);
            PlayerPrefs.Save();
            _masterTargetVolume = muted ? 0 : EffectsMasterGain * _masterSettingVolume;
            if (_musicStopAt < 0)
                _musicTargetVolume = muted ? 0 : MusicVolume(_masterTargetVolume);
        }

        public void Suspend()
        {
            if (_effectSources != null)
            {
                for (var index = 0; index < _effectSources.Length; index++)
                    if (_effectSources[index] != null) _effectSources[index].Pause();
            }
            _musicSource?.Pause();
        }

        public void Resume()
        {
            if (_effectSources != null)
            {
                for (var index = 0; index < _effectSources.Length; index++)
                    if (_effectSources[index] != null) _effectSources[index].UnPause();
            }
            _musicSource?.UnPause();
        }

        public void StartPad()
        {
            if (_musicSource == null || _musicClip == null) return;
            if (_musicStopAt >= 0 && _musicSource.isPlaying)
            {
                _musicSource.Stop();
                _musicSource.volume = 0;
            }
            _musicTargetVolume = MusicVolume();
            _musicBlendTimeConstant = 1.6f;
            _musicStopAt = -1f;
            if (!_musicSource.isPlaying)
            {
                ResetMusicPadState();
                _musicSource.volume = 0;
            }
            if (!_musicSource.isPlaying) _musicSource.Play();
        }

        public void StopPad()
        {
            if (_musicSource == null || !_musicSource.isPlaying) return;
            _musicTargetVolume = 0;
            _musicBlendTimeConstant = 0.45f;
            _musicStopAt = Time.unscaledTime + 1.8f;
        }

        private void Update()
        {
            var gainBlend = 1f - Mathf.Exp(-Time.unscaledDeltaTime / 0.02f);
            _masterVolume = Mathf.Lerp(_masterVolume, _masterTargetVolume, gainBlend);
            _effectsVolume = Mathf.Lerp(_effectsVolume, _effectsTargetVolume, gainBlend);
            if (_effectSources != null)
            {
                var effectGain = _masterVolume * _effectsVolume;
                for (var index = 0; index < _effectSources.Length; index++)
                {
                    if (_effectSources[index] != null) _effectSources[index].volume = effectGain;
                }
            }
            if (_musicSource == null || !_musicSource.isPlaying) return;
            if (_musicStopAt < 0) _musicTargetVolume = MusicVolume();
            var timeConstant = _musicBlendTimeConstant;
            var blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime / timeConstant);
            _musicSource.volume = Mathf.Lerp(_musicSource.volume, _musicTargetVolume, blend);
            if (_musicStopAt >= 0 && Time.unscaledTime >= _musicStopAt)
            {
                _musicSource.Stop();
                _musicSource.volume = 0;
                _musicStopAt = -1f;
                ResetMusicPadState();
            }
        }

        private float MusicVolume(float master = -1f)
        {
            return (_muted ? 0 : master >= 0 ? master : _masterVolume) * _musicVolume * MusicPadGain;
        }

        public void Play(Cue cue, float pitch = 1f)
        {
            PlayPrepared(cue, _clips[(int)cue], pitch, true);
        }

        public void PlayGem(int step)
        {
            var index = Mathf.Clamp(step, 0, _gemClips.Length - 1);
            PlayPrepared(Cue.Gem, _gemClips[index], 1f, false);
        }

        public void PlayFuseWarning(int stage)
        {
            var index = Mathf.Clamp(stage, 0, _fuseWarningClips.Length - 1);
            PlayPrepared(Cue.FuseWarning, _fuseWarningClips[index], 1f, false);
        }

        private void PlayPrepared(Cue cue, AudioClip clip, float pitch, bool applyVariation)
        {
            if (!PassesGate(cue)) return;
            // The browser updates its cooldown key before tone/noise returns
            // for mute, missing context, or a zero gain. Keep that ordering so
            // toggling audio cannot change the source event timeline.
            if (_muted || _effectSources == null || _effectSources.Length == 0 || _masterVolume <= 0 || _effectsVolume <= 0) return;
            if (clip == null) return;
            var variationClip = applyVariation ? SelectSourceVariation(cue) : null;
            var source = _effectSources[_nextEffectSource];
            _nextEffectSource = (_nextEffectSource + 1) % _effectSources.Length;
            // Browser pitch changes are oscillator-frequency choices; they do
            // not stretch the cue's duration. Runtime pitch hints are legacy
            // call-site metadata, so source-backed variation uses same-length
            // generated clips instead of AudioSource.pitch.
            source.pitch = 1f;
            source.PlayOneShot(variationClip ?? clip, CueVolume(cue) * BusLimiterGain());
        }

        private float BusLimiterGain()
        {
            var now = Time.unscaledTime;
            _voiceStartTimes[_voiceStartIndex] = now;
            _voiceStartIndex = (_voiceStartIndex + 1) % _voiceStartTimes.Length;
            var overlapping = 0;
            for (var index = 0; index < _voiceStartTimes.Length; index++)
                if (now - _voiceStartTimes[index] < 0.08f) overlapping++;
            // 1 voice: 1.0. 4 voices: ~0.7. 8+: ~0.45 floor.
            return 1f / (1f + 0.15f * Mathf.Max(0, overlapping - 1));
        }

        // Per-cue loudness on top of the shared SFX bus: everything +20%,
        // hitmarker (Hit) +50%, notifications (Milestone/MilestoneMajor) +65%.
        private static float CueVolume(Cue cue)
        {
            switch (cue)
            {
                case Cue.Hit: return 1.5f;
                case Cue.Milestone:
                case Cue.MilestoneMajor: return 1.65f;
                default: return 1.2f;
            }
        }

        private AudioClip SelectSourceVariation(Cue cue)
        {
            switch (cue)
            {
                case Cue.Fire:
                    return _fireClips[UnityEngine.Random.Range(0, _fireClips.Length)];
                case Cue.Hit:
                    return _hitClips[UnityEngine.Random.Range(0, _hitClips.Length)];
                case Cue.Die:
                    return _dieClips[UnityEngine.Random.Range(0, _dieClips.Length)];
                case Cue.Currency:
                    return _currencyClips[UnityEngine.Random.Range(0, _currencyClips.Length)];
                default:
                    return null;
            }
        }

        private bool PassesGate(Cue cue)
        {
            var minimumSeconds = GateSeconds(cue);
            if (minimumSeconds <= 0f) return true;

            var index = GateIndex(cue);
            var now = Time.unscaledTime;
            if (_hasPlayed[index] && now - _lastPlayedAt[index] < minimumSeconds) return false;
            _hasPlayed[index] = true;
            _lastPlayedAt[index] = now;
            return true;
        }

        private static int GateIndex(Cue cue)
        {
            // Browser Sfx.milestone() gates both minor and major variants
            // through the single "milestone" key.
            return cue == Cue.MilestoneMajor ? (int)Cue.Milestone : (int)cue;
        }

        // Mirrors the browser Sfx.gate() windows. Long-form UI and encounter
        // cues intentionally remain ungated; dense combat cues are bounded.
        private static float GateSeconds(Cue cue)
        {
            switch (cue)
            {
                case Cue.Fire: return 0.045f;
                case Cue.Hit: return 0.040f;
                case Cue.Crit: return 0.090f;
                case Cue.Die: return 0.050f;
                case Cue.Gem: return 0.030f;
                case Cue.Harvest: return 0.110f;
                case Cue.Evolution: return 0.500f;
                case Cue.Milestone:
                case Cue.MilestoneMajor: return 0.500f;
                case Cue.Hurt: return 0.150f;
                case Cue.Ui: return 0.060f;
                case Cue.Warning: return 0.400f;
                case Cue.Rusher: return 0.900f;
                case Cue.Dash: return 0.120f;
                case Cue.Scattergun: return 0.090f;
                case Cue.Railgun: return 0.240f;
                case Cue.BladeLaunch: return 0.220f;
                case Cue.ShieldBreak: return 0.150f;
                case Cue.GunnerShot: return 0.110f;
                case Cue.Arc: return 0.120f;
                case Cue.Seeker: return 0.160f;
                case Cue.FuseWarning: return 0.120f;
                case Cue.ExploderBlast: return 0.090f;
                case Cue.Bomb: return 0.500f;
                case Cue.Boss: return 1.800f;
                case Cue.BossCharge: return 0.420f;
                case Cue.BossSlam: return 0.360f;
                case Cue.BossDeath: return 1.200f;
                case Cue.Currency: return 0.042f;
                default: return 0f;
            }
        }

        private void BuildClips()
        {
            _clips[(int)Cue.Fire] = BuildTone("vf_fire", 0.07f, 840, 340, 0.045f, Waveform.Square);
            _clips[(int)Cue.Hit] = BuildSequence(
                "vf_hit",
                0.08f,
                Array.Empty<SequenceNote>(),
                new[] { new SequenceNoise(1800f, 0.06f, 0.08f, 0f, 1.4f) });
            _clips[(int)Cue.Die] = BuildSequence(
                "vf_die",
                0.22f,
                new[] { new SequenceNote(240f, 60f, 0.18f, 0f, 0.09f, Waveform.Triangle) },
                new[] { new SequenceNoise(850f, 0.16f, 0.14f, 0f, 0.8f) });
            _clips[(int)Cue.Crit] = BuildSequence(
                "vf_crit",
                0.12f,
                new[] { new SequenceNote(1400f, 500f, 0.1f, 0f, 0.07f, Waveform.Saw) },
                new[] { new SequenceNoise(2400f, 0.09f, 0.09f, 0f, 2f) });
            _clips[(int)Cue.Pickup] = BuildTone("vf_pickup", 0.1f, 660, 990, 0.1f, Waveform.Triangle);
            _clips[(int)Cue.Gem] = BuildTone("vf_gem", 0.09f, 540, 729, 0.075f, Waveform.Sine);
            _clips[(int)Cue.Harvest] = BuildSequence(
                "vf_harvest",
                0.14f,
                new[] { new SequenceNote(520f, 145f, 0.14f, 0f, 0.065f, Waveform.Triangle) },
                new[] { new SequenceNoise(1050f, 0.09f, 0.045f, 0.015f, 1.5f) });
            _clips[(int)Cue.LevelUp] = BuildSequence(
                "vf_level",
                0.56f,
                new[]
                {
                    new SequenceNote(523.25f, 523.25f, 0.22f, 0f, 0.12f, Waveform.Triangle),
                    new SequenceNote(659.25f, 659.25f, 0.22f, 0.07f, 0.12f, Waveform.Triangle),
                    new SequenceNote(783.99f, 783.99f, 0.22f, 0.14f, 0.12f, Waveform.Triangle),
                    new SequenceNote(1046.5f, 1046.5f, 0.22f, 0.21f, 0.12f, Waveform.Triangle),
                },
                new[] { new SequenceNoise(3000f, 0.4f, 0.05f, 0.1f, 0.5f) });
            _clips[(int)Cue.Evolution] = BuildSequence(
                "vf_evolution",
                0.96f,
                new[]
                {
                    new SequenceNote(261.63f, 266.86f, 0.34f, 0f, 0.12f, Waveform.Triangle),
                    new SequenceNote(392f, 399.84f, 0.34f, 0.075f, 0.12f, Waveform.Triangle),
                    new SequenceNote(523.25f, 533.72f, 0.34f, 0.15f, 0.12f, Waveform.Sine),
                    new SequenceNote(783.99f, 799.67f, 0.34f, 0.225f, 0.12f, Waveform.Sine),
                    new SequenceNote(1046.5f, 1067.43f, 0.34f, 0.3f, 0.12f, Waveform.Sine),
                    new SequenceNote(92f, 46f, 0.62f, 0.08f, 0.12f, Waveform.Saw),
                },
                new[] { new SequenceNoise(2800f, 0.5f, 0.07f, 0.14f) });
            _clips[(int)Cue.Warning] = BuildSequence(
                "vf_warning",
                0.42f,
                new[]
                {
                    new SequenceNote(196f, 196f, 0.14f, 0f, 0.1f, Waveform.Square),
                    new SequenceNote(196f, 196f, 0.14f, 0.18f, 0.1f, Waveform.Square),
                });
            _clips[(int)Cue.Dash] = BuildSequence(
                "vf_dash",
                0.16f,
                Array.Empty<SequenceNote>(),
                new[] { new SequenceNoise(900f, 0.14f, 0.07f, 0f, 0.8f) });
            _clips[(int)Cue.Explosion] = BuildNoise("vf_explosion", 0.24f, 0.22f);
            _clips[(int)Cue.Elite] = BuildSequence(
                "vf_elite",
                0.62f,
                new[]
                {
                    new SequenceNote(160f, 34f, 0.55f, 0f, 0.22f, Waveform.Saw),
                    new SequenceNote(520f, 90f, 0.4f, 0.03f, 0.1f, Waveform.Square),
                },
                new[] { new SequenceNoise(320f, 0.5f, 0.3f, 0f, 0.6f) });
            _clips[(int)Cue.Boss] = BuildSequence(
                "vf_boss",
                0.62f,
                new[]
                {
                    new SequenceNote(82f, 54f, 0.5f, 0f, 0.13f, Waveform.Saw),
                    new SequenceNote(123f, 72f, 0.42f, 0.12f, 0.075f, Waveform.Triangle),
                },
                new[] { new SequenceNoise(180f, 0.42f, 0.07f, 0f, 0.7f) });
            _clips[(int)Cue.BossCharge] = BuildSequence(
                "vf_boss_charge",
                0.32f,
                new[] { new SequenceNote(72f, 270f, 0.28f, 0f, 0.085f, Waveform.Saw) },
                new[] { new SequenceNoise(480f, 0.22f, 0.055f, 0.04f, 1.2f) });
            _clips[(int)Cue.BossSlam] = BuildSequence(
                "vf_boss_slam",
                0.42f,
                new[] { new SequenceNote(94f, 28f, 0.38f, 0f, 0.16f, Waveform.Sine) },
                new[]
                {
                    new SequenceNoise(150f, 0.32f, 0.15f, 0f, 0.7f),
                    new SequenceNoise(860f, 0.12f, 0.055f, 0f, 1.4f),
                });
            _clips[(int)Cue.BossDeath] = BuildSequence(
                "vf_boss_death",
                0.82f,
                new[]
                {
                    new SequenceNote(180f, 32f, 0.65f, 0f, 0.16f, Waveform.Saw),
                    new SequenceNote(360f, 48f, 0.5f, 0.045f, 0.075f, Waveform.Square),
                },
                new[] { new SequenceNoise(260f, 0.72f, 0.17f, 0f, 0.8f) });
            _clips[(int)Cue.GameOver] = BuildSequence(
                "vf_gameover",
                1.08f,
                new[]
                {
                    new SequenceNote(392f, 380.24f, 0.34f, 0f, 0.14f, Waveform.Triangle),
                    new SequenceNote(311.1f, 301.77f, 0.34f, 0.16f, 0.14f, Waveform.Triangle),
                    new SequenceNote(261.6f, 253.75f, 0.34f, 0.32f, 0.14f, Waveform.Triangle),
                    new SequenceNote(196f, 190.12f, 0.34f, 0.48f, 0.14f, Waveform.Triangle),
                },
                new[] { new SequenceNoise(240f, 0.9f, 0.16f, 0.1f, 0.5f) });
            _clips[(int)Cue.Ui] = BuildTone("vf_ui", 0.07f, 700, 980, 0.08f, Waveform.Sine);
            _clips[(int)Cue.Bomb] = BuildSequence(
                "vf_bomb",
                0.58f,
                new[]
                {
                    new SequenceNote(92f, 24f, 0.52f, 0f, 0.2f, Waveform.Sine),
                    new SequenceNote(420f, 66f, 0.3f, 0.018f, 0.08f, Waveform.Saw),
                },
                new[]
                {
                    new SequenceNoise(130f, 0.46f, 0.18f, 0f, 0.62f),
                    new SequenceNoise(1400f, 0.15f, 0.065f, 0.02f),
                });
            _clips[(int)Cue.Railgun] = BuildSequence(
                "vf_railgun",
                0.32f,
                new[]
                {
                    new SequenceNote(1180f, 310f, 0.075f, 0f, 0.055f, Waveform.Square),
                    new SequenceNote(105f, 34f, 0.28f, 0.018f, 0.13f, Waveform.Saw),
                },
                new[] { new SequenceNoise(520f, 0.16f, 0.11f, 0.012f, 1.8f) });
            _clips[(int)Cue.Scattergun] = BuildSequence(
                "vf_scattergun",
                0.13f,
                new[]
                {
                    new SequenceNote(170f, 72f, 0.11f, 0f, 0.09f, Waveform.Saw),
                    new SequenceNote(580f, 420f, 0.045f, 0.045f, 0.025f, Waveform.Square),
                },
                new[] { new SequenceNoise(720f, 0.09f, 0.12f, 0f, 0.85f) });
            _clips[(int)Cue.Arc] = BuildSequence(
                "vf_arc",
                0.12f,
                new[]
                {
                    new SequenceNote(1560f, 240f, 0.09f, 0f, 0.05f, Waveform.Saw),
                    new SequenceNote(2200f, 480f, 0.05f, 0.02f, 0.03f, Waveform.Square),
                },
                new[] { new SequenceNoise(3200f, 0.09f, 0.07f, 0f, 3.5f) });
            _clips[(int)Cue.Rusher] = BuildSequence(
                "vf_rusher",
                0.44f,
                new[]
                {
                    new SequenceNote(147f, 147f, 0.1f, 0f, 0.09f, Waveform.Square),
                    new SequenceNote(196f, 196f, 0.1f, 0.12f, 0.09f, Waveform.Square),
                    new SequenceNote(247f, 247f, 0.14f, 0.24f, 0.09f, Waveform.Square),
                });
            _clips[(int)Cue.Seeker] = BuildSequence(
                "vf_seeker",
                0.2f,
                new[] { new SequenceNote(280f, 620f, 0.14f, 0f, 0.045f, Waveform.Triangle) },
                new[] { new SequenceNoise(640f, 0.18f, 0.06f, 0f, 0.9f) });
            _clips[(int)Cue.FuseWarning] = BuildSequence(
                "vf_fuse_warning",
                0.08f,
                new[] { new SequenceNote(330f, 310.2f, 0.055f, 0f, 0.065f, Waveform.Square) },
                new[] { new SequenceNoise(2200f, 0.035f, 0.025f, 0f, 3f) });
            _clips[(int)Cue.ExploderBlast] = BuildSequence(
                "vf_exploder_blast",
                0.27f,
                new[] { new SequenceNote(150f, 42f, 0.24f, 0f, 0.11f, Waveform.Saw) },
                new[]
                {
                    new SequenceNoise(240f, 0.2f, 0.1f, 0f, 0.75f),
                    new SequenceNoise(1100f, 0.08f, 0.045f, 0f, 1.6f),
                });
            _clips[(int)Cue.GunnerShot] = BuildSequence(
                "vf_gunner_shot",
                0.09f,
                new[] { new SequenceNote(420f, 190f, 0.075f, 0f, 0.055f, Waveform.Square) },
                new[] { new SequenceNoise(1250f, 0.055f, 0.045f, 0f, 2f) });
            _clips[(int)Cue.BladeLaunch] = BuildSequence(
                "vf_blade_launch",
                0.18f,
                new[] { new SequenceNote(520f, 980f, 0.12f, 0f, 0.05f, Waveform.Triangle) },
                new[] { new SequenceNoise(1900f, 0.16f, 0.055f, 0f, 2.4f) });
            _clips[(int)Cue.ShieldBreak] = BuildSequence(
                "vf_shield_break",
                0.2f,
                new[]
                {
                    new SequenceNote(1480f, 460f, 0.13f, 0f, 0.07f, Waveform.Triangle),
                    new SequenceNote(920f, 260f, 0.16f, 0.025f, 0.04f, Waveform.Square),
                },
                new[] { new SequenceNoise(2600f, 0.12f, 0.065f, 0f, 3f) });
            _clips[(int)Cue.Currency] = BuildTone("vf_currency", 0.055f, 920, 1260, 0.045f, Waveform.Sine);
            _clips[(int)Cue.Milestone] = BuildSequence(
                "vf_milestone",
                0.48f,
                new[]
                {
                    new SequenceNote(523.25f, 544.18f, 0.2f, 0f, 0.075f, Waveform.Triangle),
                    new SequenceNote(659.25f, 685.62f, 0.2f, 0.075f, 0.075f, Waveform.Triangle),
                });
            _clips[(int)Cue.MilestoneMajor] = BuildSequence(
                "vf_milestone_major",
                0.72f,
                new[]
                {
                    new SequenceNote(392f, 407.68f, 0.3f, 0f, 0.11f, Waveform.Triangle),
                    new SequenceNote(523.25f, 544.18f, 0.3f, 0.075f, 0.11f, Waveform.Triangle),
                    new SequenceNote(659.25f, 685.62f, 0.3f, 0.15f, 0.11f, Waveform.Triangle),
                    new SequenceNote(783.99f, 815.35f, 0.3f, 0.225f, 0.11f, Waveform.Triangle),
                },
                new[] { new SequenceNoise(2200f, 0.34f, 0.05f, 0.08f, 1.4f) });
            _clips[(int)Cue.Hurt] = BuildSequence(
                "vf_hurt",
                0.32f,
                new[] { new SequenceNote(190f, 55f, 0.28f, 0f, 0.2f, Waveform.Saw) },
                new[] { new SequenceNoise(380f, 0.22f, 0.2f, 0f, 0.7f) });
            _clips[(int)Cue.Pause] = BuildTone("vf_pause", 0.12f, 440, 330, 0.09f, Waveform.Sine);
        }

        private void BuildParameterizedClips()
        {
            for (var index = 0; index < _fireClips.Length; index++)
            {
                var ratio = Mathf.Lerp(0.92f, 1.08f, index / (float)(_fireClips.Length - 1));
                _fireClips[index] = BuildTone(
                    "vf_fire_" + index,
                    0.07f,
                    840f * ratio,
                    340f * ratio,
                    0.045f,
                    Waveform.Square);
            }

            for (var index = 0; index < _hitClips.Length; index++)
            {
                var center = Mathf.Lerp(1500f, 2100f, index / (float)(_hitClips.Length - 1));
                _hitClips[index] = BuildSequence(
                    "vf_hit_" + index,
                    0.08f,
                    Array.Empty<SequenceNote>(),
                    new[] { new SequenceNoise(center, 0.06f, 0.08f, 0f, 1.4f) });
            }

            for (var index = 0; index < _dieClips.Length; index++)
            {
                var center = Mathf.Lerp(700f, 1000f, index / (float)(_dieClips.Length - 1));
                _dieClips[index] = BuildSequence(
                    "vf_die_" + index,
                    0.22f,
                    new[] { new SequenceNote(240f, 60f, 0.18f, 0f, 0.09f, Waveform.Triangle) },
                    new[] { new SequenceNoise(center, 0.16f, 0.14f, 0f, 0.8f) });
            }

            for (var index = 0; index < _currencyClips.Length; index++)
            {
                var ratio = Mathf.Lerp(0.97f, 1.03f, index / (float)(_currencyClips.Length - 1));
                _currencyClips[index] = BuildTone(
                    "vf_currency_" + index,
                    0.055f,
                    920f * ratio,
                    1260f * ratio,
                    0.045f,
                    Waveform.Sine);
            }

            for (var step = 0; step < _gemClips.Length; step++)
            {
                var ratio = Mathf.Pow(2f, step / 12f);
                _gemClips[step] = BuildTone(
                    "vf_gem_" + step,
                    0.09f,
                    540f * ratio,
                    729f * ratio,
                    0.075f,
                    Waveform.Sine);
            }

            for (var stage = 0; stage < _fuseWarningClips.Length; stage++)
            {
                var frequency = 330f + stage * 48f;
                _fuseWarningClips[stage] = BuildSequence(
                    "vf_fuse_warning_" + stage,
                    0.08f,
                    new[]
                    {
                        new SequenceNote(
                            frequency,
                            frequency * 0.94f,
                            0.055f,
                            0f,
                            0.065f,
                            Waveform.Square),
                    },
                    new[] { new SequenceNoise(2200f, 0.035f, 0.025f, 0f, 3f) });
            }
        }

        private static AudioClip BuildTone(string name, float seconds, float startHz, float endHz, float amplitude, Waveform waveform)
        {
            var count = Mathf.Max(1, Mathf.RoundToInt(seconds * SampleRate));
            var samples = new float[count];
            for (var index = 0; index < count; index++)
            {
                // WebAudio schedules both ramps in seconds from the tone's
                // start time. Do not feed the normalized envelope fraction
                // into IntegratedPhase: for a 70 ms cue that would clamp the
                // pitch sweep at 70 ms and leave the remaining samples on the
                // terminal frequency.
                var elapsed = index / (float)SampleRate;
                var t = elapsed / Mathf.Max(0.000001f, seconds);
                var phase = IntegratedPhase(startHz, endHz, seconds, elapsed);
                var raw = waveform == Waveform.Sine
                    ? Mathf.Sin(phase)
                    : waveform == Waveform.Triangle
                        ? 2f * Mathf.Abs(2f * (phase / (2f * Mathf.PI) - Mathf.Floor(phase / (2f * Mathf.PI) + 0.5f))) - 1f
                        : waveform == Waveform.Square
                            // WebAudio's square oscillator is already in its
                            // positive half-cycle at phase zero. Mathf.Sign
                            // would return 0 at that exact sample, creating a
                            // one-sample discontinuity in every generated cue.
                            ? Mathf.Sin(phase) >= 0f ? 1f : -1f
                            : 2f * (phase / (2f * Mathf.PI) - Mathf.Floor(phase / (2f * Mathf.PI) + 0.5f));
                samples[index] = raw * amplitude * Envelope(t);
            }
            return MakeClip(name, samples);
        }

        private static AudioClip BuildChime(string name, float seconds, float lowHz, float highHz, float amplitude)
        {
            var count = Mathf.Max(1, Mathf.RoundToInt(seconds * SampleRate));
            var samples = new float[count];
            for (var index = 0; index < count; index++)
            {
                var t = index / (float)count;
                var first = Mathf.Sin(2 * Mathf.PI * lowHz * index / SampleRate);
                var second = Mathf.Sin(2 * Mathf.PI * highHz * index / SampleRate);
                samples[index] = (first * 0.6f + second * Mathf.Clamp01(t * 2f) * 0.4f) * amplitude * Envelope(t);
            }
            return MakeClip(name, samples);
        }

        private static AudioClip BuildNoise(string name, float seconds, float amplitude)
        {
            var count = Mathf.Max(1, Mathf.RoundToInt(seconds * SampleRate));
            var samples = new float[count];
            for (var index = 0; index < count; index++)
            {
                var t = index / (float)count;
                var noise = SharedNoiseBuffer[index % SharedNoiseBuffer.Length];
                samples[index] = noise * amplitude * Envelope(t);
            }
            return MakeClip(name, samples);
        }

        private static AudioClip BuildSequence(string name, float seconds, SequenceNote[] notes, SequenceNoise[] noises = null)
        {
            var count = Mathf.Max(1, Mathf.RoundToInt(seconds * SampleRate));
            var samples = new float[count];
            var noiseX1 = noises == null ? Array.Empty<float>() : new float[noises.Length];
            var noiseX2 = noises == null ? Array.Empty<float>() : new float[noises.Length];
            var noiseY1 = noises == null ? Array.Empty<float>() : new float[noises.Length];
            var noiseY2 = noises == null ? Array.Empty<float>() : new float[noises.Length];
            var noiseB0 = noises == null ? Array.Empty<float>() : new float[noises.Length];
            var noiseB1 = noises == null ? Array.Empty<float>() : new float[noises.Length];
            var noiseB2 = noises == null ? Array.Empty<float>() : new float[noises.Length];
            var noiseA1 = noises == null ? Array.Empty<float>() : new float[noises.Length];
            var noiseA2 = noises == null ? Array.Empty<float>() : new float[noises.Length];
            if (noises != null)
            {
                for (var noiseIndex = 0; noiseIndex < noises.Length; noiseIndex++)
                {
                    var noiseSpec = noises[noiseIndex];
                    var center = Mathf.Clamp(noiseSpec.CenterHz, 20f, SampleRate * 0.45f);
                    var omega = 2f * Mathf.PI * center / SampleRate;
                    var alpha = Mathf.Sin(omega) / (2f * Mathf.Max(0.1f, noiseSpec.Q));
                    var a0 = 1f + alpha;
                    noiseB0[noiseIndex] = WebAudioBandpassNumerator(center, noiseSpec.Q);
                    noiseB1[noiseIndex] = 0f;
                    noiseB2[noiseIndex] = -noiseB0[noiseIndex];
                    noiseA1[noiseIndex] = (-2f * Mathf.Cos(omega)) / a0;
                    noiseA2[noiseIndex] = (1f - alpha) / a0;
                }
            }
            for (var index = 0; index < count; index++)
            {
                var time = index / (float)SampleRate;
                var value = 0f;
                for (var noteIndex = 0; noteIndex < notes.Length; noteIndex++)
                {
                    var note = notes[noteIndex];
                    var local = time - note.Delay;
                    if (local < 0 || local >= note.Duration) continue;
                    var localT = local / note.Duration;
                    var phase = IntegratedPhase(note.StartHz, note.EndHz, note.Duration, local);
                    value += WaveValue(phase, note.Waveform) * note.Amplitude * Envelope(localT);
                }

                if (noises != null)
                {
                    for (var noiseIndex = 0; noiseIndex < noises.Length; noiseIndex++)
                    {
                        var noiseSpec = noises[noiseIndex];
                        var local = time - noiseSpec.Delay;
                        if (local < 0 || local >= noiseSpec.Duration) continue;
                        var localT = local / noiseSpec.Duration;
                        var sharedIndex = Mathf.FloorToInt(local * SampleRate) % SharedNoiseBuffer.Length;
                        var white = SharedNoiseBuffer[sharedIndex];
                        var filtered = noiseB0[noiseIndex] * white +
                            noiseB1[noiseIndex] * noiseX1[noiseIndex] +
                            noiseB2[noiseIndex] * noiseX2[noiseIndex] -
                            noiseA1[noiseIndex] * noiseY1[noiseIndex] -
                            noiseA2[noiseIndex] * noiseY2[noiseIndex];
                        noiseX2[noiseIndex] = noiseX1[noiseIndex];
                        noiseX1[noiseIndex] = white;
                        noiseY2[noiseIndex] = noiseY1[noiseIndex];
                        noiseY1[noiseIndex] = filtered;
                        value += filtered * noiseSpec.Amplitude * Envelope(localT);
                    }
                }

                samples[index] = Mathf.Clamp(value, -1f, 1f);
            }
            return MakeClip(name, samples);
        }

        private static float WebAudioBandpassNumerator(float centerHz, float q)
        {
            var center = Mathf.Clamp(centerHz, 20f, SampleRate * 0.45f);
            var omega = 2f * Mathf.PI * center / SampleRate;
            var alpha = Mathf.Sin(omega) / (2f * Mathf.Max(0.1f, q));
            return alpha / (1f + alpha);
        }

        private static double WebAudioLowpassAlpha(double omega, double qDb)
        {
            return Math.Sin(omega) / (2d * Math.Pow(10d, qDb / 20d));
        }

        private static float[] CreateSharedNoiseBuffer(int sampleRate)
        {
            var buffer = new float[Mathf.Max(1, sampleRate / 2)];
            // The browser fills its shared WebAudio buffer with Math.random()
            // when the audio context is created. Keep the game simulation RNG
            // isolated and use a runtime-random seed for this audio-only state.
            var rng = new System.Random();
            for (var index = 0; index < buffer.Length; index++)
                buffer[index] = (float)(rng.NextDouble() * 2 - 1);
            return buffer;
        }

        private AudioClip BuildMusicPad(string name, float seconds)
        {
            var count = Mathf.Max(1, Mathf.RoundToInt(seconds * SampleRate));
            // The length is only the streaming reader's scheduling window. The
            // reader's cursor and filter state continue across AudioSource.loop,
            // so no oscillator or filter phase is reset at the loop boundary.
            return AudioClip.Create(name, count, 1, SampleRate, true, ReadMusicPad);
        }

        private void ReadMusicPad(float[] data)
        {
            if (Interlocked.Exchange(ref _musicPadResetRequested, 0) == 1)
            {
                _musicPadSampleCursor = 0;
                _musicPadFilterX1 = 0;
                _musicPadFilterX2 = 0;
                _musicPadFilterY1 = 0;
                _musicPadFilterY2 = 0;
            }

            for (var index = 0; index < data.Length; index++)
            {
                var time = _musicPadSampleCursor / (double)SampleRate;
                var cutoff = 300d + 130d * Math.Sin(2d * Math.PI * 0.045d * time);
                var omega = 2d * Math.PI * cutoff / SampleRate;
                // WebAudio's lowpass branch uses its Q value as a dB-style
                // resonance parameter (alpha_QdB), not the bandpass
                // alpha_Q formula used by the cue filters below.
                var alpha = WebAudioLowpassAlpha(omega, 0.9d);
                var cosine = Math.Cos(omega);
                var a0 = 1d + alpha;
                var b0 = ((1d - cosine) * 0.5d) / a0;
                var b1 = (1d - cosine) / a0;
                var b2 = b0;
                var a1 = (-2d * cosine) / a0;
                var a2 = (1d - alpha) / a0;
                var input = 0d;
                for (var voice = 0; voice < MusicPadFrequencies.Length; voice++)
                {
                    var detuned = MusicPadFrequencies[voice] * Math.Pow(
                        2d,
                        ((voice - 1.5d) * 7d) / 1200d);
                    var phase = 2d * Math.PI * detuned * time;
                    var phaseCycles = phase / (2d * Math.PI);
                    var triangle = 2d * Math.Abs(
                        2d * (phaseCycles - Math.Floor(phaseCycles + 0.5d))) - 1d;
                    var saw = 2d * (phaseCycles - Math.Floor(phaseCycles + 0.5d));
                    input += voice % 2 == 0 ? saw : triangle;
                }

                var filtered = b0 * input + b1 * _musicPadFilterX1 +
                    b2 * _musicPadFilterX2 - a1 * _musicPadFilterY1 -
                    a2 * _musicPadFilterY2;
                _musicPadFilterX2 = _musicPadFilterX1;
                _musicPadFilterX1 = (float)input;
                _musicPadFilterY2 = _musicPadFilterY1;
                _musicPadFilterY1 = (float)filtered;
                data[index] = (float)filtered;
                _musicPadSampleCursor++;
            }
        }

        private void ResetMusicPadState()
        {
            _musicPadResetRequested = 1;
        }

        private static float Envelope(float t)
        {
            // WebAudio starts the gain at the requested amplitude and applies
            // an exponential ramp to 0.0001 at the end of the cue.
            return Mathf.Pow(0.0001f, Mathf.Clamp01(t));
        }

        private static float ExponentialFrequency(float startHz, float endHz, float t)
        {
            var start = Mathf.Max(20f, startHz);
            var end = Mathf.Max(20f, endHz);
            return start * Mathf.Pow(end / start, Mathf.Clamp01(t));
        }

        private static float IntegratedPhase(float startHz, float endHz, float duration, float elapsed)
        {
            var start = Mathf.Max(20f, startHz);
            var end = Mathf.Max(20f, endHz);
            var time = Mathf.Clamp(elapsed, 0f, Mathf.Max(0.000001f, duration));
            var safeDuration = Mathf.Max(0.000001f, duration);
            var ratio = end / start;
            if (Mathf.Abs(ratio - 1f) < 0.000001f)
                return 2f * Mathf.PI * start * time;

            var logRatio = Mathf.Log(ratio);
            var normalized = time / safeDuration;
            var cycles = start * safeDuration * (Mathf.Exp(logRatio * normalized) - 1f) / logRatio;
            return 2f * Mathf.PI * cycles;
        }

        private static float WaveValue(float phase, Waveform waveform)
        {
            return waveform == Waveform.Sine
                ? Mathf.Sin(phase)
                : waveform == Waveform.Triangle
                    ? 2f * Mathf.Abs(2f * (phase / (2f * Mathf.PI) - Mathf.Floor(phase / (2f * Mathf.PI) + 0.5f))) - 1f
                    : waveform == Waveform.Square
                        ? Mathf.Sin(phase) >= 0f ? 1f : -1f
                        : 2f * (phase / (2f * Mathf.PI) - Mathf.Floor(phase / (2f * Mathf.PI) + 0.5f));
        }

        private static AudioClip MakeClip(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private enum Waveform
        {
            Sine,
            Triangle,
            Square,
            Saw,
        }

        private readonly struct SequenceNote
        {
            public readonly float StartHz;
            public readonly float EndHz;
            public readonly float Duration;
            public readonly float Delay;
            public readonly float Amplitude;
            public readonly Waveform Waveform;

            public SequenceNote(float startHz, float endHz, float duration, float delay, float amplitude, Waveform waveform)
            {
                StartHz = startHz;
                EndHz = endHz;
                Duration = duration;
                Delay = delay;
                Amplitude = amplitude;
                Waveform = waveform;
            }
        }

        private readonly struct SequenceNoise
        {
            public readonly float CenterHz;
            public readonly float Duration;
            public readonly float Amplitude;
            public readonly float Delay;
            public readonly float Q;

            public SequenceNoise(float centerHz, float duration, float amplitude, float delay)
                : this(centerHz, duration, amplitude, delay, 1f)
            {
            }

            public SequenceNoise(float centerHz, float duration, float amplitude, float delay, float q)
            {
                CenterHz = centerHz;
                Duration = duration;
                Amplitude = amplitude;
                Delay = delay;
                Q = q;
            }
        }
    }
}
