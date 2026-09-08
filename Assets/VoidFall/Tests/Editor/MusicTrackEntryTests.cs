using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VoidFall.Runtime;
using Random = System.Random;

namespace VoidFall.Tests.Editor
{
    public sealed class MusicTrackEntryTests
    {
        private static readonly string[] TrackNames =
        {
            "Boss Battle",
            "Cyber Alley",
            "Drone Patrol",
            "Hidden Lab",
            "Holo Arcade",
            "Holo Bazaar",
            "Neon Escape",
            "Neon Rain",
            "Neon Street",
            "Rooftop Chase",
            "Synth Syndicate",
            "8 Bit atmosphere - 002 - Beyond the Pixelated Horizon",
            "8 Bit atmosphere - 003 - The Gentle Descent into the Cosmic Ruin",
            "8 Bit atmosphere - 007 - The Surveyor's Quiet, Amiga Reverie",
            "Cyberpunk Theme 1",
        };


        [Test]
        public void Every_ost_track_picks_varied_positive_entries_before_its_final_second()
        {
            foreach (var trackName in TrackNames)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/VoidFall/Resources/VoidFall/Music/OST/" + trackName + ".mp3");
                Assert.That(clip, Is.Not.Null, trackName + " is missing");

                var entries = new HashSet<float>();
                var rng = new Random(7231);
                for (var draw = 0; draw < 64; draw++)
                {
                    var entry = MusicTrackEntries.Pick(trackName, rng);
                    Assert.That(entry, Is.GreaterThan(0f), trackName);
                    Assert.That(entry, Is.LessThanOrEqualTo(clip.length - 1f), trackName);
                    entries.Add(entry);
                }

                Assert.That(entries.Count, Is.GreaterThan(1),
                    trackName + " should not always shift to the same section");
            }
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not-an-ost-track")]
        public void Missing_track_names_fall_back_to_the_start(string trackName)
        {
            Assert.That(MusicTrackEntries.Pick(trackName, new Random(1)), Is.Zero);
        }

        [Test]
        public void Track_names_are_matched_without_case_sensitivity()
        {
            var exact = MusicTrackEntries.Pick("Neon Escape", new Random(19));
            var alternateCase = MusicTrackEntries.Pick("neon escape", new Random(19));

            Assert.That(alternateCase, Is.EqualTo(exact));
        }

        [Test]
        public void Main_menu_includes_beyond_the_pixelated_horizon()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/VoidFall/Resources/VoidFall/Music/MainMenu/8 Bit atmosphere - 002 - Beyond the Pixelated Horizon.mp3");
            Assert.That(clip, Is.Not.Null, "Main menu Beyond the Pixelated Horizon clip is missing");
            Assert.That(clip.length, Is.GreaterThan(200f));
        }

        [Test]
        public void All_ost_and_menu_tracks_use_streaming_audio_presets()
        {
            foreach (var trackName in TrackNames)
            {
                var path = "Assets/VoidFall/Resources/VoidFall/Music/OST/" + trackName + ".mp3";
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                Assert.That(importer, Is.Not.Null, path + " importer missing");
                Assert.That(importer.defaultSampleSettings.loadType, Is.EqualTo(AudioClipLoadType.Streaming), path + " loadType");
                Assert.That(importer.defaultSampleSettings.compressionFormat, Is.EqualTo(AudioCompressionFormat.Vorbis), path + " compressionFormat");
                Assert.That(importer.loadInBackground, Is.True, path + " loadInBackground");
            }

            var menuPath = "Assets/VoidFall/Resources/VoidFall/Music/MainMenu/8 Bit atmosphere - 002 - Beyond the Pixelated Horizon.mp3";
            var menuImporter = AssetImporter.GetAtPath(menuPath) as AudioImporter;
            Assert.That(menuImporter, Is.Not.Null, menuPath + " importer missing");
            Assert.That(menuImporter.defaultSampleSettings.loadType, Is.EqualTo(AudioClipLoadType.Streaming), menuPath + " loadType");
            Assert.That(menuImporter.defaultSampleSettings.compressionFormat, Is.EqualTo(AudioCompressionFormat.Vorbis), menuPath + " compressionFormat");
        }
    }
}

