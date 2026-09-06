using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    // Opt-in visual validation, isolated before the runtime's first profile load.
    public sealed class VisualDeliveryProbe : MonoBehaviour
    {
        private static string Output
        {
            get
            {
                foreach (var arg in Environment.GetCommandLineArgs())
                    if (arg.StartsWith("-vfvisual-check=")) return Path.GetFullPath(arg.Substring(16));
                return null;
            }
        }
        internal static string ProfilePath => Output == null ? null : Path.Combine(Output, "profile.json");
        private VoidFallGameRuntime _runtime;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            if (Output != null) new GameObject("Visual Delivery Check").AddComponent<VisualDeliveryProbe>();
        }
        private IEnumerator Start()
        {
            Directory.CreateDirectory(Output);
            yield return null;
            _runtime = FindAnyObjectByType<VoidFallGameRuntime>();
            if (_runtime == null) { Application.Quit(1); yield break; }
            Set("_diagnosticRunSeedOverride", 0x5f1dc0deu);
            Call("SetApplicationActive", true);
            Call("StartRun");
            Set("_arenaId", ArenaId.RedNebula);
            Call("SelectRecipeForCurrentArena");
            Call("TryInstallPreparedArenaPlate", ArenaId.RedNebula);
            var sim = (GameSim)Get("_gameSim");
            sim.Player.Iframes = 9999;
            for (var i = 0; i < 3; i++) Call("TrySpawnMeteor", false);
            for (var i = 0; i < 2; i++) Call("TrySpawnMeteor", true);
            var at = 0;
            for (var i = 0; i < sim.Meteors.Length; i++)
            {
                var m = sim.Meteors[i];
                if (!m.Active) continue;
                m.Position = new Vector2(-470f + at * 220f, at % 2 == 0 ? -130f : 150f);
                m.OrbitCentre = m.Position - new Vector2(Mathf.Cos(m.OrbitPhase) * 48f, Mathf.Sin(m.OrbitPhase) * 32f);
                sim.Meteors[i] = m; at++;
            }
            Call("SpawnEnemy", "elite", new Vector2(220, -20), null, false, false);
            Call("SpawnImpactMark", new Vector2(-190, -30), 70f, .3f);
            yield return new WaitForSecondsRealtime(1f);
            yield return Capture("01-nebula.png");
            Call("ClearNebulaStrikes");
            Call("TrySpawnNebulaStrike");
            yield return new WaitForSecondsRealtime(.2f);
            yield return Capture("02-wave-warning.png");
            yield return new WaitForSecondsRealtime(2.65f);
            yield return Capture("03-wave.png");
            Call("ClearNebulaStrikes");
            Call("ClearMeteors");
            Call("BeginHydraBossEncounterForCapture");
            var clock = (OverclockState)Get("_overclock");
            clock.ApplyPickup(); clock.ApplyPickup();
            Set("_overclock", clock);
            yield return new WaitForSecondsRealtime(.7f);
            yield return Capture("04-boss-overclock.png");
            File.WriteAllText(Path.Combine(Output, "complete.txt"), "Visual delivery captures completed.");
            Debug.Log("VISUAL DELIVERY CHECK completed");
            Application.Quit(0);
        }
        private IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(Output, name));
            yield return new WaitForSecondsRealtime(.3f);
        }
        private object Get(string name) => _runtime.GetType().GetField(name, Flags).GetValue(_runtime);
        private void Set(string name, object value) => _runtime.GetType().GetField(name, Flags).SetValue(_runtime, value);
        private void Call(string name, params object[] args)
        {
            foreach (var method in _runtime.GetType().GetMethods(Flags))
            {
                if (method.Name != name || method.GetParameters().Length != args.Length) continue;
                method.Invoke(_runtime, args);
                return;
            }
            throw new MissingMethodException(name);
        }
    }
}
