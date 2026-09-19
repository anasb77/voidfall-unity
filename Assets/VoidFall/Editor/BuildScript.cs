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
            ApprovedHudAssets.Configure();
            // Prefer DX11 on Windows: the owner hit a hard hang while leaving
            // DX12 exclusive fullscreen. DX12 remains an explicit diagnostic option.
            // This is a renderer workaround; the original hang has no captured stack.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[]
            {
                UnityEngine.Rendering.GraphicsDeviceType.Direct3D11,
                UnityEngine.Rendering.GraphicsDeviceType.Direct3D12
            });
            AssetDatabase.SaveAssets();
            var scenes = new[] { "Assets/Scenes/SampleScene.unity" };
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var outputOverride = Environment.GetEnvironmentVariable("VOIDFALL_BUILD_OUTPUT");
            var buildRoot = string.IsNullOrWhiteSpace(outputOverride)
                ? Path.GetFullPath(Path.Combine(projectRoot, "..", "Builds")) : Path.GetFullPath(outputOverride);
            if (buildRoot.StartsWith(Application.dataPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Build output cannot be inside Assets.");
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
                // Unity may reuse its launcher; stamp only after a successful complete player build.
                var builtAt = DateTime.UtcNow;
                File.SetLastWriteTimeUtc(summary.outputPath, builtAt);
                File.WriteAllText(Path.Combine(buildRoot, "BUILD_INFO.txt"),
                    "VoidFall — canonical Windows build\n" +
                    "Built (local): " + builtAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz") + "\n" +
                    "Built (UTC): " + builtAt.ToString("O") + "\n" +
                    "Build GUID: " + summary.guid + "\n" +
                    "Source project: " + projectRoot + "\n" +
                    "Source revision: " + (Environment.GetEnvironmentVariable("VOIDFALL_SOURCE_REVISION") ?? "working tree") + "\n" +
                    "Unity: " + Application.unityVersion + "\n" +
                    "Windows renderer: Direct3D11 (Direct3D12 opt-in diagnostics)\n" +
                    "Launch: " + summary.outputPath + "\n" +
                    "Automatic run history: RunExports/\n" +
                    "Survival seconds: " + VoidFall.Core.VoidProgressionRules.SurvivalSeconds + "\n" +
                    "Weapon slots: " + VoidFall.Core.ProgressionRules.BaseWeaponSlots +
                    " (" + VoidFall.Core.ProgressionRules.ExpandedWeaponSlots + " after two rank-VI weapons)\n" +
                    "Clock face opacity: " + VoidFall.Core.ArsenalContent.ClockFaceOpacity.ToString("P0", System.Globalization.CultureInfo.InvariantCulture) + "\n" +
                    "Clock: rank III adds a small seconds attack hand\n" +
                    "Boomerang: 35% larger projectile/contact radius (0.675 scale)\n" +
                    "Mines: slower placement, 0.9s floor, 1.2s freeze / 1.2s recovery\n" +
                    "Incidents: smaller stronger Black Hole and revised Destroyer opening\n" +
                    "Maps: approved Court / original Null City 4x / Hydra I populations and teleport\n" +
                    "Hydra II: original Unity map and boss encounter preserved\n" +
                    "Legacy restoration: " + VoidFall.Core.LegacyRestorationRules.Version + "\n" +
                    "Director I v5: 2.5x ordinary arrivals; independent uniform V1 rings every 34s; early Exploder\n" +
                    "HUD: approved study 02 at 66%, bundled Chakra Petch, custom arsenal/passive/manual slots\n" +
                    "Pressure starts at 1x; Spiky 19.5/58.5 radius every 0.5s, 4x raster resolution; Shuriken spin 14 rad/s\n" +
                    "Rewards: ordinary rare drops 1/300; Overclock bank capped at 30s; XP +25% from level 6\n" +
                    "Video: saved monitor selection; smaller mute control below score\n" +
                    "Cards: Phase Rounds, weapon-bound Split Shot, Giant Slayer, Second Wind\n" +
                    "Escape: 10 active seconds, covered crossing, 2.5s arrival grace\n");
                File.AppendAllText(Path.Combine(buildRoot, "BUILD_INFO.txt"),
                    "Dealer: safe crossing, one purchase per visit, 100 run Scraps per offer\n" +
                    "Dealer art: four same-face expressions, upper/lower encounters, animated hair\n" +
                    "Legendaries: Sound Blade / Charged Rifle, saved fragments and separate manual slot\n" +
                    "Controls: E / controller South browses; mouse + hold LMB/F or right stick + RT attacks\n");
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
