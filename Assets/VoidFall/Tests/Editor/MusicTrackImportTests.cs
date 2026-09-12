using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VoidFall.Tests.Editor
{
    public sealed class MusicTrackImportTests
    {
        [TestCase("8 Bit atmosphere - 002 - Beyond the Pixelated Horizon")]
        [TestCase("8 Bit atmosphere - 003 - The Gentle Descent into the Cosmic Ruin")]
        [TestCase("8 Bit atmosphere - 007 - The Surveyor's Quiet, Amiga Reverie")]
        [TestCase("Cyberpunk Theme 1")]
        public void Added_soundtrack_is_streamed_stereo_and_discoverable_by_the_director(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/VoidFall/Resources/VoidFall/Music/OST/" + name + ".mp3");
            Assert.That(clip, Is.Not.Null, "The source-folder file must actually be installed in runtime Resources.");
            Assert.That(clip.loadType, Is.EqualTo(AudioClipLoadType.Streaming));
            Assert.That(clip.channels, Is.EqualTo(2));
            Assert.That(clip.length, Is.InRange(60f, 300f));
            Assert.That(Array.Exists(Resources.LoadAll<AudioClip>("VoidFall/Music/OST"), entry => entry == clip), Is.True);
        }
    }
}
