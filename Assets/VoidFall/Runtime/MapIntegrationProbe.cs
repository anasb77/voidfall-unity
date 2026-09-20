using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using VoidFall.Core;
using VoidFall.Persistence;

namespace VoidFall.Runtime
{
    /// <summary>Opt-in packaged-player visual check. Synthetic poses are diagnostic, never balance samples.</summary>
    public sealed class MapIntegrationProbe : MonoBehaviour
    {
        [Serializable] private sealed class PhaseReport
        {
            public string id, screenshot;
            public float entryMilliseconds, meanFrameMilliseconds, maxFrameMilliseconds, meanRenderCpuMilliseconds;
            public float simulationSecondsBefore, simulationSecondsAfter;
            public int enemies, bosses, visibleSprites, width, height;
            public float viewportWidth, viewportHeight, cameraX, cameraY, playerScale;
        }
        [Serializable] private sealed class Report
        {
            public bool success;
            public string captureKind = "diagnostic", error;
            public string method = "Fixed 1/60 simulation steps; stationary invulnerable player with weapon cooldowns held; manually selected arena clocks and warning poses. Frame timings include presentation/VSync, renderCpu is reflected Render call only; not normal balance evidence.";
            public float maximumEntryMilliseconds;
            public PhaseReport[] phases;
        }
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private VoidFallGameRuntime _runtime;
        private string _output;
        private readonly List<PhaseReport> _phases = new List<PhaseReport>();
        private readonly List<InputDevice> _disabledDevices = new List<InputDevice>();
        private float _entryMilliseconds;
        private bool _isolated;
        private Vector2 _playerPosition;
        private float _pinnedNullClock = -1, _pinnedCourtClock = -1, _pinnedCourtSurvivalClock = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateIfRequested()
        {
            const string prefix = "-vfmapcheck=";
            var argument = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            if (argument == null) return;
            var root = new GameObject("VoidFall Map Integration Check");
            DontDestroyOnLoad(root);
            var probe = root.AddComponent<MapIntegrationProbe>();
            probe._output = Path.GetFullPath(argument.Substring(prefix.Length));
            Application.runInBackground = true;
        }

        private IEnumerator Start()
        {
            // Flatten nested routines so failures in package waits/captures also reach the report.
            var routines = new Stack<IEnumerator>();
            routines.Push(Run());
            while (routines.Count > 0)
            {
                object current = null;
                var failed = false;
                try
                {
                    var routine = routines.Peek();
                    if (!routine.MoveNext()) { routines.Pop(); continue; }
                    current = routine.Current;
                    if (current is IEnumerator nested) { routines.Push(nested); continue; }
                }
                catch (Exception exception)
                {
                    failed = true;
                    Complete(exception.ToString());
                }
                if (failed) yield break;
                yield return current;
            }
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(_output);
            if (Environment.GetCommandLineArgs().Any(a => a.StartsWith("-vfrunexports=", StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Map check owns its isolated export destination; omit -vfrunexports.");
            yield return null;
            yield return null;
            _runtime = FindAnyObjectByType<VoidFallGameRuntime>();
            if (_runtime == null) throw new InvalidOperationException("Gameplay runtime did not initialize.");
            _runtime.enabled = false;
            // Set both destinations before StartRunInternal can save or emit run_start.
            Set("_saveStore", new SaveStore(Path.Combine(_output, "mapcheck.profile.json")));
            Set("_saveData", SaveStore.CreateDefault());
            Set("_runExportDirectoryOverride", Path.Combine(_output, "RunExports"));
            Set("_diagnosticRunSeedOverride", 2848592627u);
            Set("_runSaved", true);
            foreach (var device in InputSystem.devices)
                if (device.enabled) { _disabledDevices.Add(device); InputSystem.DisableDevice(device); }
            Call("SetApplicationActive", true);
            _isolated = true;
            Call("StartRunInternal", false, true);
            Set("_xpNeed", 1000000);
            var cooldowns = (float[])Get("_weaponCooldowns");
            for (var i = 0; i < cooldowns.Length; i++) cooldowns[i] = 10000f;

            yield return EnterArena("null-city", ArenaId.NullCity);
            _playerPosition = new Vector2(300f, 100f); // A legal pose on the restored native-scale city floor.
            for (var i = 0; i < 9; i++)
                Call("SpawnNullCityUnit", i, _playerPosition + new Vector2((i % 3 - 1) * 210f, (i / 3 - 1) * 170f));
            _pinnedNullClock = 6f;
            yield return Capture("01-null-surveillance-sign");
            _pinnedNullClock = (float)(NullCityRules.SurveillanceSeconds + NullCityRules.PurgeWarningSeconds + .4);
            yield return Capture("02-null-laser-sign");
            for(var i=12;i<NullCityContent.Enemies.Length;i++)
                Call("SpawnNullCityUnit",i,_playerPosition+new Vector2((i%5-2)*160,(i<17?1:-1)*210));
            _pinnedNullClock=7;
            yield return Capture("02b-null-expanded-roster");

            yield return EnterArena("monochrome-court", ArenaId.MonochromeCourt);
            Call("EnsureCourtField");
            yield return Capture("03-court-board-rooks");
            for(var type=0;type<6;type++)for(var rank=0;rank<3;rank++)
                Call("SpawnEnemy",ApprovedMapContent.CourtId(type,rank),(Vector2?)new Vector2((type-2.5f)*180,(rank-1)*230));
            Call("SpawnEnemy",ApprovedMapContent.OriginalKnightId,(Vector2?)new Vector2(0,-350));
            yield return Capture("03a-court-approved-roster");
            var sentinel = ((Array)Get("_courtRooks")).GetValue(0);
            _playerPosition = (Vector2)sentinel.GetType().GetField("Position", Flags).GetValue(sentinel) + Vector2.right * 200;
            _pinnedCourtSurvivalClock = 2.8f;
            yield return Capture("03c-court-sentinel-warning");
            _pinnedCourtSurvivalClock = 3.15f;
            yield return Capture("03d-court-sentinel-burst");
            _pinnedCourtSurvivalClock = 17.15f;
            yield return Capture("03e-court-sentinel-white-burst");
            _pinnedCourtSurvivalClock = -1;
            _playerPosition = new Vector2(3550f, 3550f);
            yield return Capture("03b-court-edge-framing");
            _playerPosition = Vector2.zero;
            PinPlayer(); // Restore the actual pose before the bosses choose their arena centre.
            var tracker = (VoidObjectiveTracker)Get("_objectives");
            tracker.Step(VoidProgressionRules.SurvivalSeconds);
            Call("SyncVoidBossEncounterWithObjective");
            _pinnedCourtClock = .8f;
            yield return Capture("04-court-grandmaster-warning");
            _pinnedCourtClock = 3.55f;
            yield return Capture("04b-wingwang-white-burst");
            _pinnedCourtClock = 8.55f;
            yield return Capture("04c-wingwang-black-burst");

            yield return EnterArena("hydra", ArenaId.Hydra);
            Set("_hydraSurvivalElapsed", 35f);
            for (var i = 0; i < HydraPopulationRules.Count; i++)
            {
                var position = new Vector2((i % 5 - 2) * 225f, i < 5 ? 340f : -340f);
                if (!(bool)Call("SpawnEnemy", "chaser", (Vector2?)position))
                    throw new InvalidOperationException("Hydra specimen spawn rejected at index " + i);
            }
            yield return Capture("05-hydra-i-specimens");
            Call("StepApprovedHydra",3f);
            for(var i=0;i<5;i++)Call("SpawnEnemy",ApprovedMapContent.InsectIds[i],(Vector2?)new Vector2((i-2)*150,130));
            Call("SpawnEnemy","hydra-mantis-matriarch",(Vector2?)new Vector2(-280,-200));
            Call("SpawnEnemy","hydra-iron-carapace",(Vector2?)new Vector2(280,-200));
            yield return Capture("05b-hydra-hive-and-insects");
            tracker = (VoidObjectiveTracker)Get("_objectives");
            tracker.Step(VoidProgressionRules.SurvivalSeconds);
            var route = (VoidRouteRun)Get("_voidRoute");
            var visits = route.History.Count;
            var transitionStart = Time.realtimeSinceStartupAsDouble;
            Call("SyncVoidBossEncounterWithObjective");
            var deadline = transitionStart + 30;
            while ((bool)Get("_riftTransitionActive"))
            {
                if (Time.realtimeSinceStartupAsDouble > deadline) throw new TimeoutException("Hydra teleport did not settle.");
                Call("StepRiftTransition", 1f / 60f);
                Call("Render");
                yield return null;
            }
            Call("UpdateJourneyFlow", 0f);
            if (!(bool)Get("_hydraBossEncounterActive") || route.History.Count != visits)
                throw new InvalidOperationException("Hydra teleport lost its boss or changed the route visit.");
            _entryMilliseconds = (float)((Time.realtimeSinceStartupAsDouble - transitionStart) * 1000);
            _playerPosition = Vector2.zero;
            yield return Capture("06-hydra-ii-original-boss");
            Complete(null);
        }

        private IEnumerator EnterArena(string id, ArenaId arena)
        {
            var started = Time.realtimeSinceStartupAsDouble;
            _pinnedNullClock = _pinnedCourtClock = _pinnedCourtSurvivalClock = -1;
            Call("ClearCombatForJourney");
            Call("ClearHydraBossArena");
            Call("ResetJourney");
            Set("_riftTransitionActive", false);
            Set("_arenaTransitionState", new ArenaTransitionState(0, double.PositiveInfinity, ArenaPhase.Idle, 0, null));
            Set("_voidRoute", new VoidRouteRun(new[] { new VoidRouteNode(id, id, 0, 1, "", "", "", "") }, id));
            Set("_arenaId", arena);
            _playerPosition = Vector2.zero;
            PinPlayer();
            Call("BeginObjectiveForCurrentArena");
            Call("SelectRecipeForCurrentArena");
            Call("BeginArenaPackageLoad", arena);
            while (!(bool)Call("TryInstallPreparedArenaPlate", arena))
            {
                if (Time.realtimeSinceStartupAsDouble - started > 30) throw new TimeoutException("Authored arena package unavailable: " + id);
                yield return null;
            }
            Call("UpdateGameplayCameraViewport");
            Call("Render");
            _entryMilliseconds = (float)((Time.realtimeSinceStartupAsDouble - started) * 1000);
        }

        private void PinPlayer()
        {
            var game = Get("_gameSim");
            var field = game.GetType().GetField("Player", Flags);
            var player = field.GetValue(game);
            SetField(player, "Position", _playerPosition);
            SetField(player, "Velocity", Vector2.zero);
            SetField(player, "Iframes", 10000f);
            field.SetValue(game, player);
            Set("_cameraFollowPosition", _playerPosition);
            Set("_paused", false);
            Set("_applicationInactive", false);
            Set("_timeScale", 1f);
            Set("_freezeTimer", 0f);
        }

        private double StepAndRender()
        {
            PinPlayer();
            if (_pinnedNullClock >= 0) Set("_nullCityElapsed", _pinnedNullClock);
            if (_pinnedCourtClock >= 0) Set("_monochromeBossElapsed", _pinnedCourtClock);
            if (_pinnedCourtSurvivalClock >= 0) Set("_monochromeSurvivalElapsed", _pinnedCourtSurvivalClock);
            Set("_ambientClock", (float)Get("_ambientClock") + 1f / 60f);
            Call("Simulate", 1d / 60d);
            PinPlayer();
            var started = Stopwatch.GetTimestamp();
            Call("UpdateHud"); Call("SyncUiScreen");
            Call("Render");
            return (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
        }

        private IEnumerator Capture(string id)
        {
            for (var i = 0; i < 30; i++) { StepAndRender(); yield return null; }
            var phase = new PhaseReport { id = id, entryMilliseconds = _entryMilliseconds,
                screenshot = Path.Combine(_output, id + ".png"), simulationSecondsBefore = (float)Get("_time") };
            double total = 0, render = 0;
            var previousFrameEnd = Time.realtimeSinceStartupAsDouble;
            for (var i = 0; i < 60; i++)
            {
                render += StepAndRender();
                yield return new WaitForEndOfFrame();
                var frameEnd = Time.realtimeSinceStartupAsDouble;
                var elapsed = (float)((frameEnd - previousFrameEnd) * 1000);
                previousFrameEnd = frameEnd;
                total += elapsed;
                phase.maxFrameMilliseconds = Mathf.Max(phase.maxFrameMilliseconds, elapsed);
                yield return null;
            }
            phase.meanFrameMilliseconds = (float)(total / 60);
            phase.meanRenderCpuMilliseconds = (float)(render / 60);
            phase.simulationSecondsAfter = (float)Get("_time");
            phase.enemies = (int)Call("ActiveEnemies"); phase.bosses = (int)Call("ActiveBosses");
            if (phase.bosses > 0 && ((SpriteRenderer[])Get("_bossViews"))
                .Count(view => view != null && view.enabled && view.isVisible) < phase.bosses)
                throw new InvalidOperationException(id + ": active bosses are outside the captured view.");
            phase.width = Screen.width; phase.height = Screen.height;
            var player = (SpriteRenderer)Get("_playerView");
            if (player == null || !player.enabled || player.sprite == null)
                throw new InvalidOperationException(id + ": player presentation missing.");
            var camera = (Camera)Get("_camera");
            phase.viewportHeight = camera.orthographicSize * 2;
            phase.viewportWidth = phase.viewportHeight * camera.aspect;
            phase.cameraX = camera.transform.position.x; phase.cameraY = camera.transform.position.y;
            phase.playerScale = player.transform.localScale.x;
            phase.visibleSprites = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
                .Count(s => s.enabled && s.gameObject.activeInHierarchy && s.sprite != null && s.isVisible);
            if (phase.visibleSprites < 4) throw new InvalidOperationException(id + ": no visible gameplay surface.");
            if (phase.simulationSecondsAfter <= phase.simulationSecondsBefore)
                throw new InvalidOperationException(id + ": simulation did not advance.");
            Call("Render");
            yield return new WaitForEndOfFrame();
            if (File.Exists(phase.screenshot)) File.Delete(phase.screenshot);
            ScreenCapture.CaptureScreenshot(phase.screenshot);
            var deadline = Time.realtimeSinceStartupAsDouble + 10;
            while (!File.Exists(phase.screenshot) || new FileInfo(phase.screenshot).Length == 0)
            {
                if (Time.realtimeSinceStartupAsDouble > deadline) throw new TimeoutException("Screenshot missing: " + phase.screenshot);
                yield return null;
            }
            yield return new WaitForSecondsRealtime(.2f);
            _phases.Add(phase);
            WriteReport(null, false);
        }

        private void Complete(string error)
        {
            try
            {
                if (_runtime != null && _isolated) Call("FinishRunExport", error == null ? "diagnostic_complete" : "diagnostic_failed");
                WriteReport(error, error == null);
            }
            catch (Exception reportError) { UnityEngine.Debug.LogError("MAPCHECK reporting failed: " + reportError); error = error ?? reportError.ToString(); }
            foreach (var device in _disabledDevices) if (device.added) InputSystem.EnableDevice(device);
            _disabledDevices.Clear();
            UnityEngine.Debug.Log(error == null ? "MAPCHECK PASSED " + _output : "MAPCHECK FAILED " + error);
            Application.Quit(error == null ? 0 : 1);
        }

        private void WriteReport(string error, bool success)
        {
            Directory.CreateDirectory(_output);
            File.WriteAllText(Path.Combine(_output, "mapcheck.json"), JsonUtility.ToJson(new Report
            {
                success = success, error = error, phases = _phases.ToArray(),
                maximumEntryMilliseconds = _phases.Count == 0 ? 0 : _phases.Max(p => p.entryMilliseconds)
            }, true));
        }
        private object Get(string name) => _runtime.GetType().GetField(name, Flags).GetValue(_runtime);
        private void Set(string name, object value) => SetField(_runtime, name, value);
        private static void SetField(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        private object Call(string name, params object[] args)
        {
            var candidates = _runtime.GetType().GetMethods(Flags).Where(m => m.Name == name).OrderBy(m => m.GetParameters().Length);
            foreach (var method in candidates)
            {
                var parameters = method.GetParameters();
                if (parameters.Length < args.Length || parameters.Skip(args.Length).Any(p => !p.IsOptional)) continue;
                if (parameters.Take(args.Length).Where((p, i) => args[i] != null && !p.ParameterType.IsInstanceOfType(args[i])).Any()) continue;
                var values = new object[parameters.Length]; Array.Copy(args, values, args.Length);
                for (var i = args.Length; i < values.Length; i++) values[i] = parameters[i].DefaultValue;
                return method.Invoke(_runtime, values);
            }
            throw new MissingMethodException("Diagnostic runtime hook not found: " + name + " / " + args.Length);
        }
    }
}
