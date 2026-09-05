using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using VoidFall.Core;
using VoidFall.Persistence;
using VoidFall.UI;

namespace VoidFall.Runtime
{
    /// <summary>Opt-in player validation and captures with an isolated profile. Never starts in ordinary play.</summary>
    public sealed class OverclockHudProbe : MonoBehaviour
    {
        private VoidFallGameRuntime _runtime;
        private string _output;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        // Queried by the composition root before its first profile load, not after Awake.
        internal static string ProfilePath
        {
            get
            {
                foreach (var argument in Environment.GetCommandLineArgs())
                    if (argument.StartsWith("-vfoverclock-check=", StringComparison.Ordinal))
                    {
                        var directory = argument.Substring("-vfoverclock-check=".Length);
                        if (!string.IsNullOrEmpty(directory)) return Path.Combine(Path.GetFullPath(directory), "profile.json");
                    }
                return null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateIfRequested()
        {
            string output = null;
            foreach (var argument in Environment.GetCommandLineArgs())
                if (argument.StartsWith("-vfoverclock-check=", StringComparison.Ordinal))
                    output = argument.Substring("-vfoverclock-check=".Length);
            if (string.IsNullOrEmpty(output)) return;
            var host = new GameObject("Overclock HUD Check");
            DontDestroyOnLoad(host);
            host.AddComponent<OverclockHudProbe>()._output = output;
        }

        private IEnumerator Start()
        {
            var run = Run();
            while (true)
            {
                bool next;
                object value;
                try { next = run.MoveNext(); value = next ? run.Current : null; }
                catch (Exception error)
                {
                    Debug.LogError("OVERCLOCK CHECK FAILED " + error);
                    File.WriteAllText(Path.Combine(_output, "failure.txt"), error.ToString());
                    Application.Quit(1);
                    yield break;
                }
                if (!next) yield break;
                yield return value;
            }
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(_output);
            Application.runInBackground = true;
            yield return null;
            yield return null;
            _runtime = FindAnyObjectByType<VoidFallGameRuntime>();
            if (_runtime == null) throw new InvalidOperationException("Runtime did not start.");
            Set("_diagnosticRunSeedOverride", 2848592627u);
            Call("SetApplicationActive", true);
            Call("StartRun");
            var sim = (GameSim)Get("_gameSim");
            sim.Player.Iframes = 9999;
            sim.Player.Health = 37;
            Call("StepObjectiveTracker", 300d);
            Call("StepObjectiveTracker", 0d);
            yield return new WaitForSecondsRealtime(1.2f);
            var music = (MusicDirector)Get("_music");
            var source = (AudioSource)typeof(MusicDirector).GetField("_source", Flags).GetValue(music);
            if (source.clip == null || !source.isPlaying) throw new InvalidOperationException("No live soundtrack to validate.");
            if (source.clip != null) source.time = Mathf.Min(32, source.clip.length * .25f);
            var state = new OverclockState();
            state.ApplyPickup();
            Set("_overclock", state);
            Call("RegisterOverclockPickup", 0);
            yield return new WaitForSecondsRealtime(.9f);
            var peak = 0f;
            for (var frame = 0; frame < 30; frame++)
            {
                peak = Mathf.Max(peak, music.AnalysisFrame.Energy);
                yield return null;
            }
            if (peak < .001f) throw new InvalidOperationException("Live soundtrack produced no analysis signal.");
            var graphic = (MusicPerimeterGraphic)Get("_musicPerimeter");
            var firstPattern = graphic.ActivationIndex;
            CheckLayout(1);
            if (Mathf.Abs(music.CurrentMixTargets.PlaybackRate - 2f) > .01f)
                throw new InvalidOperationException("Overclock did not select 2x music.");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(_output, "01-stack-one.png"));
            yield return new WaitForSecondsRealtime(.3f);
            state = (OverclockState)Get("_overclock");
            var previous = state.Streak;
            state.ApplyPickup();
            state.ApplyPickup();
            Set("_overclock", state);
            Call("RegisterOverclockPickup", previous);
            yield return new WaitForSecondsRealtime(.9f);
            CheckLayout(3);
            if (graphic.ActivationIndex != firstPattern) throw new InvalidOperationException("A stack reshuffled the activation pattern.");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(_output, "02-stack-three.png"));
            state = (OverclockState)Get("_overclock");
            state.Step(state.RemainingSeconds * .82f);
            Set("_overclock", state);
            yield return new WaitForSecondsRealtime(.65f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(_output, "03-low-charge.png"));
            var bar = (Image)Get("_boostBar");
            if (bar.color.r <= bar.color.b) throw new InvalidOperationException("Low charge did not move to the warm palette.");
            Set("_overclock", default(OverclockState));
            yield return null;
            yield return null;
            state = new OverclockState();
            for (var index = 0; index < 12; index++) state.ApplyPickup();
            Set("_overclock", state);
            Call("RegisterOverclockPickup", 0);
            yield return new WaitForSecondsRealtime(.9f);
            CheckLayout(12);
            if (graphic.ActivationIndex != firstPattern + 1) throw new InvalidOperationException("The next activation did not create a new pattern.");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(_output, "04-stack-twelve.png"));
            yield return new WaitForSecondsRealtime(.35f);
            File.WriteAllText(Path.Combine(_output, "result.txt"),
                "PASS: 2x music; 10% text growth; boss clearance; stable stacking pattern; new activation; warm low-time palette. Track: " + music.CurrentTrackName);
            Debug.Log("OVERCLOCK PLAYER CHECK PASSED " + music.CurrentTrackName);
            Application.Quit(0);
        }

        private void CheckLayout(int stack)
        {
            var root = (RectTransform)Get("_overclockHudRoot");
            if (!root.gameObject.activeSelf) throw new InvalidOperationException("Counter is hidden.");
            if (Mathf.Abs(root.localScale.x - OverclockPresentationRules.StackScale(stack)) > .01f)
                throw new InvalidOperationException("Counter size does not match stack growth.");
            var boss = (Image)Get("_bossHudPanel");
            if (!boss.enabled) throw new InvalidOperationException("Boss HUD was not exercised.");
            Canvas.ForceUpdateCanvases();
            var counterCorners = new Vector3[4];
            var bossCorners = new Vector3[4];
            root.GetWorldCorners(counterCorners);
            boss.rectTransform.GetWorldCorners(bossCorners);
            if (counterCorners[1].y >= bossCorners[0].y) throw new InvalidOperationException("Counter overlaps boss HP.");
            if (((Text)Get("_healthValueText")).text != "37/100") throw new InvalidOperationException("Visible integrity number is stale.");
        }

        private object Get(string field) => _runtime.GetType().GetField(field, Flags).GetValue(_runtime);
        private void Set(string field, object value) => _runtime.GetType().GetField(field, Flags).SetValue(_runtime, value);
        private void Call(string method, params object[] args) => _runtime.GetType().GetMethod(method, Flags).Invoke(_runtime, args);
    }
}
