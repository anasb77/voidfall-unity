using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using VoidFall.Core;
using VoidFall.Persistence;

namespace VoidFall.Runtime
{
    /// <summary>Opt-in finite player validation. Uses an isolated profile, never the player's progress.</summary>
    public sealed class RouteJourneyProbe : MonoBehaviour
    {
        [Serializable]
        private sealed class Report
        {
            public bool success;
            public uint seed;
            public string[] visited;
            public int savedRuns;
            public string error;
        }

        private VoidFallGameRuntime _runtime;
        private string _output;
        private string _mode;
        private readonly List<string> _visited = new List<string>();
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateIfRequested()
        {
            var mode = Argument("-vfjourney");
            if (string.IsNullOrEmpty(mode)) return;
            var root = new GameObject("VoidFall Journey Check");
            DontDestroyOnLoad(root);
            var probe = root.AddComponent<RouteJourneyProbe>();
            probe._mode = mode;
            probe._output = Argument("-vfoutput");
        }

        private IEnumerator Start()
        {
            var routine = Run();
            while (true)
            {
                var more = false;
                object current = null;
                try
                {
                    more = routine.MoveNext();
                    if (more) current = routine.Current;
                }
                catch (Exception exception)
                {
                    Debug.LogError("VOIDJOURNEY FAILED " + exception);
                    if (!string.IsNullOrEmpty(_output))
                        File.WriteAllText(_output + ".failure.json", JsonUtility.ToJson(new Report { error = exception.ToString() }, true));
                    Application.Quit(1);
                }
                if (!more) yield break;
                yield return current;
            }
        }

        private IEnumerator Run()
        {
            yield return null;
            yield return null;
            if (string.IsNullOrEmpty(_output)) throw new ArgumentException("-vfoutput is required for journey diagnostics.");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_output)));
            _runtime = FindAnyObjectByType<VoidFallGameRuntime>();
            if (_runtime == null) throw new InvalidOperationException("Runtime did not initialize.");
            var store = new SaveStore(_output + ".profile.json");
            Set("_saveStore", store);
            Set("_saveData", SaveStore.CreateDefault());
            Set("_diagnosticRunSeedOverride", 2848592627u);
            Call("SetApplicationActive", true);
            Call("StartRun");

            if (_mode == "roulette")
            {
                Call("StepObjectiveTracker", 300d);
                Call("StepObjectiveTracker", 0d);
                var simulation = Get("_gameSim");
                var bossStates = (Array)simulation.GetType().GetField("Bosses", Flags).GetValue(simulation);
                for (var index = 0; index < bossStates.Length; index++)
                {
                    var boss = bossStates.GetValue(index);
                    if ((bool)boss.GetType().GetField("Active", Flags).GetValue(boss)) Call("KillBoss", index);
                }
                Call("StepObjectiveTracker", 0d);
                var playerField = simulation.GetType().GetField("Player", Flags);
                var player = playerField.GetValue(simulation);
                player.GetType().GetField("Position", Flags).SetValue(player, new Vector2(-430f, -220f));
                playerField.SetValue(simulation, player);
                Set("_partsEarned", 180);
                yield return new WaitForSecondsRealtime(2.2f);
                if (!(bool)Get("_rouletteChestActive")) throw new InvalidOperationException("Boss relic was not preserved for pickup.");
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(_output + "-drop.png");
                yield return new WaitForSecondsRealtime(0.3f);
                Set("_rouletteChestPulse", 2f);
                Call("CollectRouletteChest");
                yield return new WaitForSecondsRealtime(1.2f);
                if (!(bool)Get("_rouletteActive")) throw new InvalidOperationException("Relic pickup did not open the wheel.");
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(_output + "-wheel.png");
                var ui = (VoidFall.UI.UIManager)Get("_ui");
                typeof(VoidFall.UI.RouletteView).GetMethod("OnRaiseStakes", Flags).Invoke(ui.Roulette, null);
                yield return new WaitForSecondsRealtime(0.4f);
                typeof(VoidFall.UI.RouletteView).GetMethod("OnSpinPressed", Flags).Invoke(ui.Roulette, null);
                yield return new WaitForSecondsRealtime(9f);
                if (!(bool)Get("_prizeRevealActive")) throw new InvalidOperationException("Spin did not automatically reveal its reward.");
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(_output + "-prize.png");
                yield return new WaitForSecondsRealtime(0.4f);
                Call("ClosePrizeReveal");
                Debug.Log("ROULETTE PLAYER CHECK PASSED");
                Application.Quit(0);
                yield break;
            }

            if (_mode == "map")
            {
                Call("ToggleRouteMap");
                for (var frame = 0; frame < 10; frame++) yield return null;
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(_output);
                yield return new WaitForSecondsRealtime(0.5f);
                Debug.Log("VOIDJOURNEY MAP CAPTURE " + _output);
                Application.Quit(0);
                yield break;
            }

            var branch = Argument("-vfbranch") == "right" ? 1 : 0;
            for (var room = 0; room < 10; room++)
            {
                var source = _runtime.CurrentVoidId;
                _visited.Add(source);
                Set("_time", (float)_runtime.ElapsedSeconds + 300f);
                Call("StepObjectiveTracker", 300d);
                Call("StepObjectiveTracker", 0d);
                var sim = Get("_gameSim");
                var bosses = (Array)sim.GetType().GetField("Bosses", Flags).GetValue(sim);
                for (var index = 0; index < bosses.Length; index++)
                {
                    var boss = bosses.GetValue(index);
                    if ((bool)boss.GetType().GetField("Active", Flags).GetValue(boss)) Call("KillBoss", index);
                }
                Call("StepObjectiveTracker", 0d);
                var deadline = Time.realtimeSinceStartup + 25f;
                while (Time.realtimeSinceStartup < deadline)
                {
                    if ((bool)Get("_rouletteChestActive"))
                    {
                        Set("_rouletteChestPulse", 2f);
                        Call("CollectRouletteChest");
                    }
                    if ((bool)Get("_rouletteActive"))
                    {
                        var session = (RouletteSession)Get("_rouletteSession");
                        RouletteRules.Spin(session, (Rng)Get("_rouletteRng"));
                        Call("OnRouletteComplete", session);
                    }
                    if ((bool)Get("_prizeRevealActive")) Call("ClosePrizeReveal");
                    if ((bool)Get("_levelUpActive"))
                    {
                        Set("_levelUpPromptOpenedAt", -100f);
                        Call("SelectLevelOption", 0);
                    }
                    if ((bool)Get("_mainMenuBrowsing"))
                    {
                        var saved = store.Load();
                        if (saved.stats.totalRuns != 1) throw new InvalidOperationException("Terminal run was not saved exactly once.");
                        File.WriteAllText(_output, JsonUtility.ToJson(new Report
                        {
                            success = true, seed = 2848592627u,
                            visited = _visited.ToArray(), savedRuns = saved.stats.totalRuns,
                        }, true));
                        Debug.Log("VOIDJOURNEY COMPLETE " + string.Join(" -> ", _visited));
                        Application.Quit(0);
                        yield break;
                    }
                    if (_runtime.JourneyStatus == "Junction")
                    {
                        if (_runtime.ActiveEnemiesCount != 0 || _runtime.ActiveHostileShotsCount != 0)
                            throw new InvalidOperationException("Junction is not safe.");
                        if (_mode == "junction")
                        {
                            for (var frame = 0; frame < 15; frame++) yield return null;
                            yield return new WaitForEndOfFrame();
                            ScreenCapture.CaptureScreenshot(_output);
                            yield return new WaitForSecondsRealtime(0.5f);
                            Debug.Log("VOIDJOURNEY JUNCTION CAPTURE " + _output);
                            Application.Quit(0);
                            yield break;
                        }
                        var portals = (SpriteRenderer[])Get("_junctionPortals");
                        Set("_junctionAge", 1f);
                        var playerField = sim.GetType().GetField("Player", Flags);
                        var player = playerField.GetValue(sim);
                        player.GetType().GetField("Position", Flags).SetValue(player, (Vector2)portals[branch].transform.position);
                        playerField.SetValue(sim, player);
                    }
                    if (_runtime.JourneyStatus == "Combat" && _runtime.CurrentVoidId != source) break;
                    yield return null;
                }
                if (Time.realtimeSinceStartup >= deadline)
                    throw new TimeoutException("Journey stalled after " + source + " at " + _runtime.JourneyStatus);
            }
            throw new InvalidOperationException("Route failed to terminate.");
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
        private static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith(name + "=", StringComparison.OrdinalIgnoreCase)) return args[i].Substring(name.Length + 1);
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) return args[i + 1];
            }
            return null;
        }
    }
}
