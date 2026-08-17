using System;
using UnityEditor;
// AudioClipLoadType and AudioCompressionFormat are UnityEngine types even though
// AudioImporter and AudioImporterSampleSettings are UnityEditor ones.
using UnityEngine;

namespace VoidFall.EditorTools
{
    /// <summary>
    /// Forces streaming import settings on soundtrack clips.
    ///
    /// Unity's default for audio is Decompress On Load, which would keep all
    /// thirteen tracks fully decoded in memory for the whole session. Music is
    /// long and only ever has one voice playing, so it should stream from disk.
    ///
    /// This runs on import rather than being baked into .meta files so that a
    /// track dropped into the folder later gets the same treatment without
    /// anyone remembering to set it by hand.
    /// </summary>
    public sealed class MusicImportSettings : AssetPostprocessor
    {
        private const string MusicFolder = "/Resources/VoidFall/Music/";

        private void OnPreprocessAudio()
        {
            if (string.IsNullOrEmpty(assetPath)) return;

            var normalized = assetPath.Replace('\\', '/');
            if (normalized.IndexOf(MusicFolder, StringComparison.OrdinalIgnoreCase) < 0) return;

            var importer = assetImporter as AudioImporter;
            if (importer == null) return;

            // Keep stereo; these are authored music beds, not positional cues.
            importer.forceToMono = false;
            importer.loadInBackground = true;

            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            importer.defaultSampleSettings = settings;
        }
    }
}
