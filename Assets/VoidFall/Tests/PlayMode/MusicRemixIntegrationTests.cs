using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoidFall.Core;
using VoidFall.Persistence;
using VoidFall.Runtime;

namespace VoidFall.Tests.PlayMode
{
    public sealed class MusicRemixIntegrationTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private VoidFallGameRuntime _runtime;
        private MusicDirector _music;
        private object _sim;
        private SaveStore _previousStore;
        private SaveData _previousProfile;
        private bool _previousEnabled, _previousInactive;
        private AudioClip[] _previousGameplayClips;
        private List<int> _previousGameplayBag;
        private int _previousLastGameplayIndex;
        private readonly List<AudioClip> _temporaryClips = new List<AudioClip>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return null;
            _runtime = UnityEngine.Object.FindAnyObjectByType<VoidFallGameRuntime>();
            Assert.That(_runtime, Is.Not.Null);
            _previousEnabled = _runtime.enabled;
            _runtime.enabled = false;
            _previousStore = (SaveStore)Get("_saveStore");
            _previousProfile = (SaveData)Get("_saveData");
            _previousInactive = (bool)Get("_applicationInactive");
            Set("_visualCaptureIssued", true);
            var path = Path.Combine(Path.GetTempPath(), "voidfall-music-" + Guid.NewGuid().ToString("N"), "profile.json");
            Set("_saveStore", new SaveStore(path));
            Set("_saveData", SaveStore.CreateDefault());
            Set("_runSaved", true);
            Set("_applicationInactive", false);
            Call("StartRun");
            _sim = Get("_gameSim");
            _music = (MusicDirector)Get("_music");
            Assert.That(_music, Is.Not.Null);
            _music.SetApplicationActive(true);
            _music.SetReactiveState(State());
            var until = Time.realtimeSinceStartup + 3f;
            while (_music.CurrentChannel != MusicDirector.Channel.Gameplay && Time.realtimeSinceStartup < until)
                yield return null;
            Assert.That(_music.CurrentChannel, Is.EqualTo(MusicDirector.Channel.Gameplay));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_runtime != null)
            {
                RestoreGameplayClips();
                Set("_runSaved", true);
                Call("EnterMainMenu"); // Complete navigation while the isolated profile still owns writes.
                Set("_saveStore", _previousStore);
                Set("_saveData", _previousProfile);
                Set("_applicationInactive", _previousInactive);
                _runtime.enabled = _previousEnabled;
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Real_magnet_collections_charge_music_ordinary_xp_does_not()
        {
            Spawn("Xp", Vector2.zero);
            Call("UpdatePickups", 1f / 60f);
            Assert.That(_music.MagnetIntensity, Is.Zero);
            for (var i = 0; i < 100; i++) Spawn("Xp", Vector2.right * (100 + i));
            Spawn("Magnet", Vector2.zero);
            Call("UpdatePickups", 1f / 60f);
            Assert.That(_music.MagnetIntensity, Is.Zero, "Gems in flight do not count as collected.");
            for (var tick = 0; tick < 120; tick++) Call("UpdatePickups", 1f / 60f);
            Assert.That(_music.MagnetIntensity, Is.GreaterThan(.7f).And.LessThanOrEqualTo(1));
            Assert.That((int)Get("_musicPendingMagnetGems"), Is.Zero);
            _music.SetReactiveState(State(), false, 0);
            yield return null;
            yield return null;
            Assert.That(_music.CurrentMixTargets.BassBoost, Is.GreaterThan(.3f).And.LessThanOrEqualTo(.45f));
            Assert.That(_music.CurrentMixTargets.LowPassHz, Is.EqualTo(22000f).Within(.01f));
        }

        [UnityTest]
        public IEnumerator Greed_blocks_magnet_music_and_new_run_clears_tagged_slots()
        {
            ((HashSet<WildCardId>)Get("_activeWildCards")).Add(WildCardId.Greed);
            Spawn("Xp", Vector2.right * 100);
            Spawn("Magnet", Vector2.zero);
            for (var tick = 0; tick < 120; tick++) Call("UpdatePickups", 1f / 60f);
            Assert.That(_music.MagnetIntensity, Is.Zero);
            Assert.That((int)Get("_musicPendingMagnetGems"), Is.Zero);
            ((HashSet<WildCardId>)Get("_activeWildCards")).Clear();
            Spawn("Magnet", Vector2.zero);
            Call("UpdatePickups", 1f / 60f);
            Assert.That((int)Get("_musicPendingMagnetGems"), Is.GreaterThan(0));
            Call("StartRun");
            Assert.That((int)Get("_musicPendingMagnetGems"), Is.Zero);
            Assert.That(_music.MagnetIntensity, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Pause_and_application_suspension_hold_the_collected_remix()
        {
            ChargeMagnet();
            _music.SetReactiveState(State(), false, 0);
            yield return null;
            yield return null;
            _music.SetReactiveState(State(), true, 0);
            var held = _music.MagnetIntensity;
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(_music.MagnetIntensity, Is.EqualTo(held).Within(.00001f));
            _music.SetReactiveState(State(), false, 0);
            _music.SetApplicationActive(false);
            held = _music.MagnetIntensity;
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(_music.MagnetIntensity, Is.EqualTo(held).Within(.00001f));
            _music.SetApplicationActive(true);
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(_music.MagnetIntensity, Is.LessThan(held));
        }

        [UnityTest]
        public IEnumerator Track_shift_retains_combined_effects_and_uses_a_combat_entry()
        {
            ChargeMagnet();
            _music.SetReactiveState(State(2, true), false, 0);
            yield return new WaitForSecondsRealtime(.2f);
            var previous = _music.CurrentTrackName;
            var charge = _music.MagnetIntensity;
            _music.ShiftToNextCombatTrack();
            var until = Time.realtimeSinceStartup + 4f;
            while (_music.CurrentTrackName == previous && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(_music.CurrentTrackName, Is.Not.EqualTo(previous));
            yield return null;
            Assert.That(_music.MagnetIntensity, Is.GreaterThan(charge * .7f));
            Assert.That(_music.CurrentMixTargets.PlaybackRate, Is.InRange(1f, 1.28f));
            Assert.That(_music.CurrentMixTargets.BassBoost, Is.GreaterThan(.3f).And.LessThanOrEqualTo(.45f));
            var source = (AudioSource)Field(_music, "_source");
            var hasCuratedEntry = MusicTrackEntries.Pick(_music.CurrentTrackName, new System.Random(0)) > 0f;
            Assert.That(source.time, hasCuratedEntry ? Is.GreaterThan(5f) : Is.LessThan(1f));
            Assert.That(source.clip.name, Is.EqualTo(_music.CurrentTrackName));
        }

        [Test]
        public void Stopped_gameplay_after_a_hitch_restarts_same_track_from_zero_and_retains_remix()
        {
            ConfigureGameplaySequence();
            ChargeMagnet();
            var charge = _music.MagnetIntensity;
            var source = (AudioSource)Field(_music, "_source");
            var previous = source.clip;

            SimulateCompletedPlayback();

            Assert.That(source.clip, Is.SameAs(previous));
            Assert.That(source.loop, Is.True);
            Assert.That(source.isPlaying, Is.True);
            Assert.That(source.time, Is.LessThan(.01f));
            Assert.That(_music.MagnetIntensity, Is.GreaterThan(charge * .99f));
        }

        [Test]
        public void Stopped_source_before_playback_is_observed_does_not_advance_the_bag()
        {
            ConfigureGameplaySequence();
            var previous = _music.CurrentTrackName;
            ((AudioSource)Field(_music, "_source")).Stop();

            CallMusic("Update");

            Assert.That(_music.CurrentTrackName, Is.EqualTo(previous));
        }

        [Test]
        public void Focus_suspension_does_not_advance_an_observed_track()
        {
            ConfigureGameplaySequence();
            var previous = _music.CurrentTrackName;
            var source = (AudioSource)Field(_music, "_source");
            source.time = 1f;
            source.Play();
            CallMusic("Update");

            _music.SetApplicationActive(false);
            CallMusic("Update");

            Assert.That(_music.CurrentTrackName, Is.EqualTo(previous));
            _music.SetApplicationActive(true);
        }

        [Test]
        public void Unexpected_gameplay_stop_restarts_same_track_from_zero_and_keeps_active_mix()
        {
            var source = (AudioSource)Field(_music, "_source");
            var previous = source.clip;
            typeof(MusicDirector).GetField("_fadeVolume", Flags).SetValue(_music, .1f);
            typeof(MusicDirector).GetField("_duckElapsed", Flags).SetValue(_music, .08f);
            typeof(MusicDirector).GetField("_mixGain", Flags).SetValue(_music, .95f);
            typeof(MusicDirector).GetField("_startOffset", Flags).SetValue(_music, 10f);
            typeof(MusicDirector).GetField("_playbackObserved", Flags).SetValue(_music, true);
            typeof(MusicDirector).GetField("_notPlayingElapsed", Flags).SetValue(_music, 1f);
            source.volume = .0114f;
            source.Stop();

            CallMusic("HandlePlaybackCompletion", 1f);

            Assert.That(source.clip, Is.SameAs(previous));
            Assert.That(source.time, Is.LessThan(.01f));
            Assert.That(source.isPlaying, Is.True);
            Assert.That(source.volume, Is.EqualTo(.0114f).Within(.0001f), "Recovery must not unduck the song for one frame.");
        }

        [Test]
        public void Critical_health_hysteresis_requires_recovery_and_resets_when_dead()
        {
            SetHealth(.19f);
            Assert.That(Call("UpdateMusicCriticalHealth", true), Is.True);
            SetHealth(.22f);
            Assert.That(Call("UpdateMusicCriticalHealth", true), Is.True);
            SetHealth(.27f);
            Assert.That(Call("UpdateMusicCriticalHealth", true), Is.False);
            SetHealth(.19f);
            Call("UpdateMusicCriticalHealth", true);
            Assert.That(Call("UpdateMusicCriticalHealth", false), Is.False);
        }

        [UnityTest]
        public IEnumerator Real_streamed_soundtrack_loops_same_track_after_natural_end()
        {
            _previousGameplayClips = (AudioClip[])Field(_music, "_gameplayClips");
            var bag = (List<int>)Field(_music, "_gameplayBag");
            _previousGameplayBag = new List<int>(bag);
            _previousLastGameplayIndex = (int)Field(_music, "_lastGameplayIndex");
            bag.Clear();
            for (var index = 0; index < _previousGameplayClips.Length; index++) bag.Add(index);
            CallMusic("BeginChannel", MusicDirector.Channel.Gameplay);
            var source = (AudioSource)Field(_music, "_source");
            var visited = new HashSet<string>();
            for (var index = 0; index < _previousGameplayClips.Length; index++)
            {
                // Exercise real streaming completion at normal, overclock and critical rates.
                _music.SetReactiveState(State(index % 3 == 0 ? 2 : 0, index % 3 == 2));
                var readyUntil = Time.realtimeSinceStartup + 5f;
                while ((!source.isPlaying || (bool)Field(_music, "_switching")) && Time.realtimeSinceStartup < readyUntil)
                    yield return null;
                Assert.That(source.isPlaying, Is.True, "The actual imported soundtrack must start: " + _music.CurrentTrackName);
                var finished = source.clip;
                visited.Add(finished.name);
                Assert.That(finished.loadType, Is.EqualTo(AudioClipLoadType.Streaming));
                Assert.That(source.loop, Is.True);
                source.time = finished.length - .6f;
                yield return new WaitForSecondsRealtime(1.3f);
                Assert.That(source.clip, Is.SameAs(finished), "Gameplay must keep the same song until Track Shift: " + finished.name);
                Assert.That(source.isPlaying, Is.True, "The looped stream must still be playing: " + finished.name);
                Assert.That(source.time, Is.LessThan(3f), "A Track Shift entry offset must loop back to the full track start: " + finished.name);
                Debug.Log("REAL STREAMED TRACK LOOP PASSED " + finished.name);

                if (index >= _previousGameplayClips.Length - 1) continue;
                _music.ShiftToNextCombatTrack();
                var shiftUntil = Time.realtimeSinceStartup + 5f;
                while ((source.clip == finished || (bool)Field(_music, "_switching")) && Time.realtimeSinceStartup < shiftUntil)
                    yield return null;
                Assert.That(source.clip, Is.Not.SameAs(finished), "Track Shift must select a different song: " + finished.name);
            }
            Assert.That(visited.Count, Is.EqualTo(_previousGameplayClips.Length));
        }

        [UnityTest]
        public IEnumerator Real_streamed_menu_theme_continues_after_natural_end()
        {
            _music.PlayMainMenu();
            var source = (AudioSource)Field(_music, "_source");
            var until = Time.realtimeSinceStartup + 5f;
            while ((_music.CurrentChannel != MusicDirector.Channel.MainMenu || !source.isPlaying) && Time.realtimeSinceStartup < until)
                yield return null;
            Assert.That(_music.CurrentChannel, Is.EqualTo(MusicDirector.Channel.MainMenu));
            Assert.That(source.isPlaying, Is.True);
            var clip = source.clip;
            var nativeLoop = source.loop;
            source.time = clip.length - .4f;
            yield return new WaitForSecondsRealtime(1.3f);
            // Main's later menu-shuffle change advances non-looping offset
            // entries; zero-offset themes still loop in the audio engine.
            if (nativeLoop) Assert.That(source.clip, Is.SameAs(clip));
            else Assert.That(source.clip, Is.Not.SameAs(clip));
            Assert.That(source.isPlaying, Is.True);
            Assert.That(source.time, Is.LessThan(source.clip.length - 2f));
        }

        private static MusicReactiveState State(int tier = 0, bool critical = false) =>
            new MusicReactiveState(tier, tier, critical, false, 0f, true);
        private void ChargeMagnet()
        {
            _music.NotifyMagnetStarted();
            for (var i = 0; i < 100; i++) _music.NotifyMagnetGem(1);
        }
        private void ConfigureGameplaySequence()
        {
            if (_previousGameplayClips == null)
            {
                _previousGameplayClips = (AudioClip[])Field(_music, "_gameplayClips");
                _previousGameplayBag = new List<int>((List<int>)Field(_music, "_gameplayBag"));
                _previousLastGameplayIndex = (int)Field(_music, "_lastGameplayIndex");
            }

            var first = AudioClip.Create("NaturalCompletionFirst", 441000, 1, 44100, false);
            var second = AudioClip.Create("NaturalCompletionSecond", 441000, 1, 44100, false);
            _temporaryClips.Add(first);
            _temporaryClips.Add(second);
            typeof(MusicDirector).GetField("_gameplayClips", Flags).SetValue(_music, new[] { first, second });
            var bag = (List<int>)Field(_music, "_gameplayBag");
            bag.Clear();
            bag.Add(1);
            bag.Add(0);
            typeof(MusicDirector).GetField("_lastGameplayIndex", Flags).SetValue(_music, -1);
            typeof(MusicDirector).GetField("_combatEntryRequested", Flags).SetValue(_music, false);
            CallMusic("BeginChannel", MusicDirector.Channel.Gameplay);
        }
        private void SimulateCompletedPlayback()
        {
            var source = (AudioSource)Field(_music, "_source");
            source.time = 1f;
            source.Play();
            CallMusic("Update");
            source.Stop();
            typeof(MusicDirector).GetField("_notPlayingElapsed", Flags).SetValue(_music, 1f);
            CallMusic("Update");
        }
        private void RestoreGameplayClips()
        {
            if (_previousGameplayClips == null) return;
            CallMusic("BeginChannel", MusicDirector.Channel.None);
            typeof(MusicDirector).GetField("_gameplayClips", Flags).SetValue(_music, _previousGameplayClips);
            var bag = (List<int>)Field(_music, "_gameplayBag");
            bag.Clear();
            bag.AddRange(_previousGameplayBag);
            typeof(MusicDirector).GetField("_lastGameplayIndex", Flags).SetValue(_music, _previousLastGameplayIndex);
            foreach (var clip in _temporaryClips) UnityEngine.Object.Destroy(clip);
            _temporaryClips.Clear();
            _previousGameplayClips = null;
        }
        private void Spawn(string kind, Vector2 position)
        {
            var type = typeof(VoidFallGameRuntime).Assembly.GetType("VoidFall.Runtime.PickupKind");
            Assert.That(Call("SpawnSpecialPickup", position, 1f, Enum.Parse(type, kind)), Is.True);
        }
        private void SetHealth(float fraction)
        {
            var player = Field(_sim, "Player");
            player.GetType().GetField("Health").SetValue(player, (float)Field(player, "MaxHealth") * fraction);
            _sim.GetType().GetField("Player").SetValue(_sim, player);
        }
        private static object Field(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
        private object Get(string name) => Field(_runtime, name);
        private void Set(string name, object value) => typeof(VoidFallGameRuntime).GetField(name, Flags).SetValue(_runtime, value);
        private object Call(string name, params object[] args)
        {
            foreach (var method in typeof(VoidFallGameRuntime).GetMethods(Flags))
                if (method.Name == name && method.GetParameters().Length == args.Length)
                    return method.Invoke(_runtime, args);
            throw new MissingMethodException(name);
        }
        private object CallMusic(string name, params object[] args)
        {
            foreach (var method in typeof(MusicDirector).GetMethods(Flags))
                if (method.Name == name && method.GetParameters().Length == args.Length)
                    return method.Invoke(_music, args);
            throw new MissingMethodException(name);
        }
    }
}
