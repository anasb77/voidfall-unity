using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace VoidFall.EditorTools
{
    public static class CourtPerformanceBuild
    {
        public static void Build()
        {
            PlayerSettings.enableFrameTimingStats=true;
            var report=BuildPipeline.BuildPlayer(new[]{"Assets/Scenes/SampleScene.unity"},
                "../CourtPerfPlayer/VoidFall.exe",BuildTarget.StandaloneWindows64,BuildOptions.None);
            Debug.Log("COURTPERF BUILD " + report.summary.result);
            EditorApplication.Exit(report.summary.result==BuildResult.Succeeded?0:1);
        }
    }
}
