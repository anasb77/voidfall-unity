using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace VoidFall.EditorTools
{
    public static class NebulaVisualValidation
    {
        public static void BuildPlayer()
        {
            BuildAt("../Builds/VisualRemaster/VoidFall.exe");
        }

        public static void BuildDeliveryPlayer()
        {
            BuildAt("../Builds/VisualDelivery/VoidFall.exe");
        }

        private static void BuildAt(string location)
        {
            var output = Path.GetFullPath(location);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/SampleScene.unity" },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            Debug.Log("NEBULA BUILD " + report.summary.result + " errors=" + report.summary.totalErrors);
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
