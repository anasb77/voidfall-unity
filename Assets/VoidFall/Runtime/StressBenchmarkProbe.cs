using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    /// <summary>
    /// Opt-in benchmark driver for the source-defined stress scenarios.
    /// Enable with -vfbench=1. It is not created for normal player sessions.
    /// </summary>
    [DefaultExecutionOrder(-700)]
    public sealed class StressBenchmarkProbe : MonoBehaviour
    {
        [Serializable]
        private sealed class Sample
        {
            public float elapsedSeconds;
            public float frameEmaMilliseconds;
            public int enemies;
            public int bosses;
            public int bullets;
            public int hostileShots;
            public int pickups;
            public int meteors;
            public long managedBytes;
            public long allocatedBytes;
            public long reservedBytes;
        }

        [Serializable]
        private sealed class Report
        {
            public string scenario;
            public string scenarioName;
            public string sourceCommit = "4d5e955";
            public string unityEditor;
            public int seed;
            public float warmupSeconds;
            public float measureSeconds;
            public Sample[] samples;
        }

        private const float SampleIntervalSeconds = 5f;
        private VoidFallGameRuntime _runtime;
        private string _scenarioId;
        private string _outputPath;
        private uint _seed;
        private float _warmupSeconds;
        private float _measureSeconds;
        private float _phaseElapsed;
        private float _sampleElapsed;
        private float _lastRealtime;
        private bool _started;
        private bool _measuring;
        private bool _finished;
        private readonly List<Sample> _samples = new List<Sample>(64);

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
            _seed = ParseUInt(GetArgumentValue("-vfseed"), 0x5f1dc0deu);
            _outputPath = GetArgumentValue("-vfoutput");
            _warmupSeconds = ParseFloat(GetArgumentValue("-vfwarmup"), -1f);
            _measureSeconds = ParseFloat(GetArgumentValue("-vfmeasure"), -1f);
            _lastRealtime = Time.realtimeSinceStartup;
        }

        private void Update()
        {
            if (_finished) return;
            var now = Time.realtimeSinceStartup;
            var frameSeconds = Mathf.Max(0f, now - _lastRealtime);
            _lastRealtime = now;
            if (_runtime == null)
            {
                _runtime = FindAnyObjectByType<VoidFallGameRuntime>();
                return;
            }

            if (!_started)
            {
                var definition = FindScenario(_scenarioId);
                if (definition == null)
                {
                    FinishWithError("Unknown stress scenario: " + _scenarioId);
                    return;
                }

                _warmupSeconds = _warmupSeconds >= 0 ? _warmupSeconds : (float)definition.WarmupSeconds;
                _measureSeconds = _measureSeconds >= 0 ? _measureSeconds : (float)definition.MeasureSeconds;
                if (!_runtime.ApplyStressScenario(_scenarioId, _seed))
                {
                    FinishWithError("Stress scenario could not be applied: " + _scenarioId);
                    return;
                }

                _started = true;
                _phaseElapsed = 0;
                Debug.Log(
                    $"[VoidFallStress] START scenario={_scenarioId} seed={_seed} " +
                    $"warmup={_warmupSeconds:0.###} measure={_measureSeconds:0.###}");
                return;
            }

            _phaseElapsed += frameSeconds;
            if (!_measuring)
            {
                if (_phaseElapsed < _warmupSeconds) return;
                _measuring = true;
                _phaseElapsed = 0;
                _sampleElapsed = 0;
                CaptureSample();
                return;
            }

            _sampleElapsed += frameSeconds;
            _phaseElapsed += frameSeconds;
            if (_sampleElapsed >= SampleIntervalSeconds)
            {
                _sampleElapsed -= SampleIntervalSeconds;
                CaptureSample();
            }

            if (_phaseElapsed >= _measureSeconds)
                Finish();
        }

        private void CaptureSample()
        {
            _samples.Add(new Sample
            {
                elapsedSeconds = _phaseElapsed,
                frameEmaMilliseconds = _runtime.FrameEmaMilliseconds,
                enemies = _runtime.ActiveEnemiesCount,
                bosses = _runtime.ActiveBossesCount,
                bullets = _runtime.ActiveBulletsCount,
                hostileShots = _runtime.ActiveHostileShotsCount,
                pickups = _runtime.ActivePickupsCount,
                meteors = _runtime.ActiveMeteorsCount,
                managedBytes = GC.GetTotalMemory(false),
                allocatedBytes = Profiler.GetTotalAllocatedMemoryLong(),
                reservedBytes = Profiler.GetTotalReservedMemoryLong(),
            });
            Debug.Log(
                $"[VoidFallStress] SAMPLE t={_phaseElapsed:0.###} " +
                $"frame={_runtime.FrameEmaMilliseconds:0.###}ms " +
                $"enemies={_runtime.ActiveEnemiesCount} bosses={_runtime.ActiveBossesCount} " +
                $"shots={_runtime.ActiveBulletsCount + _runtime.ActiveHostileShotsCount} " +
                $"pickups={_runtime.ActivePickupsCount}");
        }

        private void Finish()
        {
            if (_finished) return;
            _finished = true;
            if (_samples.Count == 0 || _samples[_samples.Count - 1].elapsedSeconds < _phaseElapsed)
                CaptureSample();
            _runtime.ClearStressScenario();
            var report = new Report
            {
                scenario = _scenarioId,
                scenarioName = FindScenario(_scenarioId)?.Name ?? _scenarioId,
                unityEditor = Application.unityVersion,
                seed = unchecked((int)_seed),
                warmupSeconds = _warmupSeconds,
                measureSeconds = _measureSeconds,
                samples = _samples.ToArray(),
            };
            var path = ResolveOutputPath();
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(path, JsonUtility.ToJson(report, true));
                Debug.Log(
                    $"[VoidFallStress] COMPLETE scenario={_scenarioId} samples={_samples.Count} " +
                    $"report={path}");
            }
            catch (Exception exception)
            {
                Debug.LogError("[VoidFallStress] Report write failed: " + exception.Message);
            }

            // A benchmark invocation is a finite diagnostic command. Quit so a
            // scripted player run cannot be mistaken for a hung process.
            Application.Quit(0);
        }

        private void FinishWithError(string message)
        {
            if (_finished) return;
            _finished = true;
            Debug.LogError("[VoidFallStress] " + message);
            Application.Quit(1);
        }

        private string ResolveOutputPath()
        {
            if (!string.IsNullOrWhiteSpace(_outputPath)) return Path.GetFullPath(_outputPath);
            return Path.Combine(
                Application.persistentDataPath,
                "voidfall-unity-bench-" + _scenarioId + ".json");
        }

        private static StressScenarioDefinition FindScenario(string id)
        {
            for (var index = 0; index < ContentCatalog.StressScenarios.Length; index++)
                if (ContentCatalog.StressScenarios[index].Id == id)
                    return ContentCatalog.StressScenarios[index];
            return null;
        }

        private static bool HasArgument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length; index++)
            {
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase)) return true;
                if (args[index].StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
                    return !string.Equals(args[index].Substring(name.Length + 1), "0", StringComparison.Ordinal);
            }
            return false;
        }

        private static string GetArgumentValue(string name)
        {
            return GetArgumentValue(Environment.GetCommandLineArgs(), name);
        }

        private static string GetArgumentValue(string[] args, string name)
        {
            if (args == null || string.IsNullOrEmpty(name)) return null;
            var prefix = name + "=";
            for (var index = 0; index < args.Length; index++)
            {
                if (args[index].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return args[index].Substring(prefix.Length);
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase) &&
                    index + 1 < args.Length)
                    return args[index + 1];
            }
            return null;
        }

        private static float ParseFloat(string value, float fallback)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : fallback;
        }

        private static uint ParseUInt(string value, uint fallback)
        {
            return uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : fallback;
        }
    }
}
