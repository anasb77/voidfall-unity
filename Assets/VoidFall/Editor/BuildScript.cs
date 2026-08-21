using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace VoidFall.EditorTools
{
    public static class BuildScript
    {
        public static void BuildWindows()
        {
            var scenes = new[] { "Assets/Scenes/SampleScene.unity" };
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var buildRoot = Path.GetFullPath(Path.Combine(projectRoot, "..", "Builds"));
            Directory.CreateDirectory(buildRoot);
            var development = string.Equals(
                Environment.GetEnvironmentVariable("VOIDFALL_DEVELOPMENT_BUILD"),
                "1",
                StringComparison.Ordinal);
            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(buildRoot, "VoidFall.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = development ? BuildOptions.Development : BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            var summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"Build succeeded: {summary.totalSize} bytes at {summary.outputPath}");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"Build failed with result: {summary.result}, errors: {summary.totalErrors}");
                EditorApplication.Exit(1);
            }
        }
    }
}
