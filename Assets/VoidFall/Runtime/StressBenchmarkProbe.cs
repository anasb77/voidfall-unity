using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    /// <summary>Opt-in, isolated-profile measurement of advancing production combat.</summary>
    [DefaultExecutionOrder(-700)]
    public sealed class StressBenchmarkProbe : MonoBehaviour
    {
        [Serializable]
        private sealed class Sample
        {
            public float elapsedSeconds;
            public float combatSeconds;
            public long simulationTicks;
            public string pauseReason;
            public int kills;
            public double damageDealt;
            public float frameEmaMilliseconds;
            public int enemies, bosses, bullets, hostileShots, pickups, meteors;
            public long managedBytes, allocatedBytes, reservedBytes;
        }

        [Serializable]
        private sealed class Report
        {
            public bool valid;
            public string error, scenario, scenarioName, sourceCommit, unityEditor, cpu, gpu;
            public int seed, width, height, frameCount, timingFrames;
            public float warmupSeconds, measureSeconds, combatSecondsAdvanced;
            public long simulationTicksAdvanced, gcBytes;
            public bool gcRecorderAvailable;
            public bool hold750Requested;
            public int directorCapacityTarget, directorCapacitySteps;
            public int directorCapacityMinimum, directorCapacityMaximum;
            public int directorCapacityMinimumBeforeRefill, directorCapacityFailures;
            public double medianFrameMs, p95FrameMs, p99FrameMs, maximumFrameMs;
            public double meanSimulationCpuMs, meanMainThreadMs, meanRenderThreadMs, meanGpuMs;
            public Sample[] samples;
        }

        private const float SampleIntervalSeconds = 5f;
        private const int FrameCapacity = 72000;
        private readonly float[] _frameTimes = new float[FrameCapacity];
        private readonly FrameTiming[] _timings = new FrameTiming[1];
        private readonly List<Sample> _samples = new List<Sample>(64);
        private VoidFallGameRuntime _runtime;
        private string _scenarioId, _outputPath;
        private uint _seed;
        private float _warmupSeconds, _measureSeconds, _phaseElapsed, _sampleElapsed, _stalledSeconds;
        private double _lastRealtime, _simulationCpuTotal, _mainTotal, _renderTotal, _gpuTotal;
        private float _measureStartCombat;
        private long _lastTicks, _measureStartTicks, _gcBytes;
        private double _measureStartDamage;
        private int _measureStartKills, _frameCount, _timingFrames;
        private bool _started, _measuring, _finished;
        private bool _hold750Requested;
        private ProfilerRecorder _gcRecorder;

        // Resolve before the runtime loads any real progression. Benchmark output
        // and its fresh profile are adjacent, just like the existing capture probes.
        public static string ProfilePath
        {
            get
            {
                if (!HasArgument("-vfbench")) return null;
                var output = GetArgumentValue("-vfoutput");
                var directory = string.IsNullOrWhiteSpace(output)
                    ? Path.Combine(Application.persistentDataPath, "benchmarks")
                    : Path.GetDirectoryName(Path.GetFullPath(output));
                return Path.Combine(directory, "benchmark-profile.json");
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateIfRequested()
        {
            if (!HasArgument("-vfbench")) return;
            var root = new GameObject("VoidFall Stress Benchmark");
            DontDestroyOnLoad(root);
            root.AddComponent<StressBenchmarkProbe>();
        }

        private void Awake()
        {
            Application.runInBackground = true;
            _scenarioId = GetArgumentValue("-vfscenario") ?? "productionMax";
            _hold750Requested = HasArgument("-vfhold750");
            _seed = ParseUInt(GetArgumentValue("-vfseed"), 0x5f1dc0deu);
            _outputPath = GetArgumentValue("-vfoutput");
            _warmupSeconds = ParseFloat(GetArgumentValue("-vfwarmup"), -1f);
            _measureSeconds = ParseFloat(GetArgumentValue("-vfmeasure"), -1f);
            _lastRealtime = Time.realtimeSinceStartupAsDouble;
            _gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        }

        private void Update()
        {
            if (_finished) return;
            var now = Time.realtimeSinceStartupAsDouble;
            var frameSeconds = (float)Math.Max(0, now - _lastRealtime);
            _lastRealtime = now;
            if (_runtime == null)
            {
                _runtime = FindAnyObjectByType<VoidFallGameRuntime>();
                return;
            }

            if (!_started)
            {
                var definition = FindScenario(_scenarioId == "directorI" ? "productionMax" : _scenarioId);
                if (definition == null) { Finish("Unknown stress scenario: " + _scenarioId); return; }
                _warmupSeconds = _warmupSeconds >= 0 ? _warmupSeconds : (float)definition.WarmupSeconds;
                _measureSeconds = _measureSeconds > 0 ? _measureSeconds : (float)definition.MeasureSeconds;
                if (!(_scenarioId == "directorI" ? _runtime.ApplyDirectorPlaytest(_seed) : _runtime.ApplyStressScenario(_scenarioId, _seed)))
                { Finish("Stress scenario could not be applied."); return; }
                _started = true;
                _lastTicks = _runtime.DiagnosticSimulationTicks;
                Debug.Log($"[VoidFallStress] START scenario={_scenarioId} seed={_seed} warmup={_warmupSeconds} measure={_measureSeconds}");
                return;
            }

            if (_scenarioId == "directorI" && _runtime.DiagnosticPauseReason == "game-over") { Finish(null); return; }
            if (_runtime.DiagnosticSimulationTicks == _lastTicks) _stalledSeconds += frameSeconds;
            else _stalledSeconds = 0;
            _lastTicks = _runtime.DiagnosticSimulationTicks;
            if (_stalledSeconds > 5)
            { Finish("Combat stalled for five seconds: " + _runtime.DiagnosticPauseReason); return; }

            if (_measuring)
            {
                if (_frameCount >= FrameCapacity) { Finish("Frame sample capacity exceeded."); return; }
                _frameTimes[_frameCount++] = frameSeconds * 1000f;
                _simulationCpuTotal += _runtime.DiagnosticSimulationCpuMilliseconds;
                if (_gcRecorder.Valid) _gcBytes += Math.Max(0, _gcRecorder.LastValue);
                if (FrameTimingManager.GetLatestTimings(1, _timings) > 0)
                {
                    _timingFrames++;
                    _mainTotal += _timings[0].cpuMainThreadFrameTime;
                    _renderTotal += _timings[0].cpuRenderThreadFrameTime;
                    _gpuTotal += _timings[0].gpuFrameTime;
                }
            }
            FrameTimingManager.CaptureFrameTimings();
            _runtime.PrepareBenchmarkFrame();
            _phaseElapsed += frameSeconds;
            if (!_measuring)
            {
                if (_phaseElapsed < _warmupSeconds) return;
                _measuring = true;
                _phaseElapsed = _sampleElapsed = 0;
                _measureStartCombat = _runtime.DiagnosticCombatSeconds;
                _measureStartTicks = _runtime.DiagnosticSimulationTicks;
                _measureStartDamage = _runtime.DiagnosticDamageDealt;
                _measureStartKills = _runtime.DiagnosticKills;
                CaptureSample();
                var screenshot = GetArgumentValue("-vfscreenshot");
                if (!string.IsNullOrWhiteSpace(screenshot))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(screenshot)));
                    ScreenCapture.CaptureScreenshot(screenshot);
                }
                return;
            }
            _sampleElapsed += frameSeconds;
            if (_sampleElapsed >= SampleIntervalSeconds)
            {
                _sampleElapsed = 0;
                CaptureSample();
            }
            if (_phaseElapsed >= _measureSeconds) Finish(null);
        }

        private void CaptureSample()
        {
            if (_runtime == null) return;
            _samples.Add(new Sample
            {
                elapsedSeconds = _phaseElapsed,
                combatSeconds = _runtime.DiagnosticCombatSeconds,
                simulationTicks = _runtime.DiagnosticSimulationTicks,
                pauseReason = _runtime.DiagnosticPauseReason,
                kills = _runtime.DiagnosticKills,
                damageDealt = _runtime.DiagnosticDamageDealt,
                frameEmaMilliseconds = _runtime.FrameEmaMilliseconds,
                enemies = _runtime.ActiveEnemiesCount,
                bosses = _runtime.ActiveBossesCount,
                bullets = _runtime.ActiveBulletsCount,
                hostileShots = _runtime.ActiveHostileShotsCount,
                pickups = _runtime.ActivePickupsCount,
                meteors = _runtime.ActiveMeteorsCount,
                managedBytes = GC.GetTotalMemory(false),
                allocatedBytes = Profiler.GetTotalAllocatedMemoryLong(),
                reservedBytes = Profiler.GetTotalReservedMemoryLong()
            });
            Debug.Log($"[VoidFallStress] SAMPLE elapsed={_phaseElapsed:0.00} combat={_runtime.DiagnosticCombatSeconds:0.00} ticks={_runtime.DiagnosticSimulationTicks} enemies={_runtime.ActiveEnemiesCount} kills={_runtime.DiagnosticKills} state={_runtime.DiagnosticPauseReason}");
        }

        private void Finish(string error)
        {
            if (_finished) return;
            _finished = true;
            CaptureSample();
            var combatAdvanced = _measuring && _runtime != null ? _runtime.DiagnosticCombatSeconds - _measureStartCombat : 0;
            var ticksAdvanced = _measuring && _runtime != null ? _runtime.DiagnosticSimulationTicks - _measureStartTicks : 0;
            if (error == null && (combatAdvanced <= 0.1f || ticksAdvanced == 0 ||
                (_runtime.DiagnosticDamageDealt <= _measureStartDamage && _runtime.DiagnosticKills <= _measureStartKills)))
                error = "No advancing attacking combat was measured.";
            if (error == null && _hold750Requested && (_runtime == null ||
                _runtime.DirectorCapacityTarget != 750 || _runtime.DirectorCapacitySteps == 0 ||
                _runtime.DirectorCapacityMinimum != 750 || _runtime.DirectorCapacityMaximum != 750 ||
                _runtime.DirectorCapacityFailures > 0))
                error = "The requested 750-enemy hold was not maintained at every recorded post-refill simulation boundary.";
            var sorted = new float[_frameCount];
            Array.Copy(_frameTimes, sorted, _frameCount);
            Array.Sort(sorted);
            var report = new Report
            {
                valid = error == null, error = error,
                scenario = _scenarioId, scenarioName = FindScenario(_scenarioId)?.Name ?? _scenarioId,
                sourceCommit = GetArgumentValue("-vfsourcecommit") ?? "unrecorded",
                unityEditor = Application.unityVersion,
                cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName,
                width = Screen.width, height = Screen.height, seed = unchecked((int)_seed),
                warmupSeconds = _warmupSeconds, measureSeconds = _phaseElapsed,
                combatSecondsAdvanced = combatAdvanced, simulationTicksAdvanced = ticksAdvanced,
                frameCount = _frameCount, timingFrames = _timingFrames,
                gcBytes = _gcBytes, gcRecorderAvailable = _gcRecorder.Valid,
                hold750Requested = _hold750Requested,
                directorCapacityTarget = _runtime != null ? _runtime.DirectorCapacityTarget : 0,
                directorCapacitySteps = _runtime != null ? _runtime.DirectorCapacitySteps : 0,
                directorCapacityMinimum = _runtime != null ? _runtime.DirectorCapacityMinimum : 0,
                directorCapacityMaximum = _runtime != null ? _runtime.DirectorCapacityMaximum : 0,
                directorCapacityMinimumBeforeRefill = _runtime != null ? _runtime.DirectorCapacityMinimumBeforeRefill : 0,
                directorCapacityFailures = _runtime != null ? _runtime.DirectorCapacityFailures : 0,
                medianFrameMs = Percentile(sorted, .5), p95FrameMs = Percentile(sorted, .95),
                p99FrameMs = Percentile(sorted, .99),
                maximumFrameMs = sorted.Length > 0 ? sorted[sorted.Length - 1] : 0,
                meanSimulationCpuMs = _frameCount > 0 ? _simulationCpuTotal / _frameCount : 0,
                meanMainThreadMs = _timingFrames > 0 ? _mainTotal / _timingFrames : 0,
                meanRenderThreadMs = _timingFrames > 0 ? _renderTotal / _timingFrames : 0,
                meanGpuMs = _timingFrames > 0 ? _gpuTotal / _timingFrames : 0,
                samples = _samples.ToArray()
            };
            _runtime?.ClearStressScenario();
            var output = ResolveOutputPath();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                File.WriteAllText(output, JsonUtility.ToJson(report, true));
            }
            catch (Exception exception) { error = "Cannot write benchmark report: " + exception.Message; }
            if (error == null) Debug.Log($"[VoidFallStress] COMPLETE valid=true p95={report.p95FrameMs:0.00}ms progress={combatAdvanced:0.00}s report={output}");
            else Debug.LogError("[VoidFallStress] INVALID " + error);
            Application.Quit(error == null ? 0 : 1);
        }

        private void OnDestroy() { _gcRecorder.Dispose(); }

        private static double Percentile(float[] sorted, double fraction) =>
            sorted.Length == 0 ? 0 : sorted[Math.Min(sorted.Length - 1, (int)Math.Ceiling(sorted.Length * fraction) - 1)];

        private string ResolveOutputPath() => !string.IsNullOrWhiteSpace(_outputPath)
            ? Path.GetFullPath(_outputPath)
            : Path.Combine(Application.persistentDataPath, "benchmarks", "voidfall-unity-bench-" + _scenarioId + ".json");

        private static StressScenarioDefinition FindScenario(string id)
        {
            for (var i = 0; i < ContentCatalog.StressScenarios.Length; i++)
                if (ContentCatalog.StressScenarios[i].Id == id) return ContentCatalog.StressScenarios[i];
            return null;
        }

        private static bool HasArgument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return true;
                if (args[i].StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
                    return !string.Equals(args[i].Substring(name.Length + 1), "0", StringComparison.Ordinal);
            }
            return false;
        }
        private static string GetArgumentValue(string name) => GetArgumentValue(Environment.GetCommandLineArgs(), name);
        private static string GetArgumentValue(string[] args, string name)
        {
            if (args == null || string.IsNullOrEmpty(name)) return null;
            var prefix = name + "=";
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return args[i].Substring(prefix.Length);
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) return args[i + 1];
            }
            return null;
        }
        private static float ParseFloat(string value, float fallback) =>
            float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) &&
            !float.IsNaN(parsed) && !float.IsInfinity(parsed) ? parsed : fallback;
        private static uint ParseUInt(string value, uint fallback) =>
            uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }
}
