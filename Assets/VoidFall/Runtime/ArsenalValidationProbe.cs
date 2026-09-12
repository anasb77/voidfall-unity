using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace VoidFall.Runtime
{
    /// <summary>Explicit opt-in captures or a playable weapon loadout, always using an isolated profile.</summary>
    public sealed class ArsenalValidationProbe : MonoBehaviour
    {
        internal static string Argument(string prefix)
        {
            foreach (var value in Environment.GetCommandLineArgs())
                if (value.StartsWith(prefix, StringComparison.Ordinal)) return value.Substring(prefix.Length).Trim('"');
            return null;
        }
        internal static string ProfilePath
        {
            get
            {
                var output = Argument("-vfarsenal-check=");
                if (!string.IsNullOrEmpty(output)) return Path.Combine(Path.GetFullPath(output), "profile.json");
                if (!string.IsNullOrEmpty(Argument("-vfarsenal="))) return Path.Combine(Application.persistentDataPath, "arsenal-validation-profile.json");
                return null;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            if (ProfilePath == null) return;
            var host = new GameObject("Arsenal Validation"); DontDestroyOnLoad(host); host.AddComponent<ArsenalValidationProbe>();
        }
        private IEnumerator Start()
        {
            Application.runInBackground = true;
            yield return null; yield return null;
            var runtime = FindAnyObjectByType<VoidFallGameRuntime>();
            if (runtime == null) { Debug.LogError("ARSENAL runtime unavailable"); Application.Quit(1); yield break; }
            var output = Argument("-vfarsenal-check=");
            if (!string.IsNullOrEmpty(output)) yield return runtime.CaptureArsenalValidation(Path.GetFullPath(output));
            else
            {
                var rank = int.TryParse(Argument("-vfarsenal-rank="), out var value) ? Mathf.Clamp(value, 1, 6) : 1;
                runtime.BeginArsenalValidation(Argument("-vfarsenal="), rank, Argument("-vfarsenal-evolved=") == "1");
                var incident = Argument("-vfincident=");
                if (!string.IsNullOrEmpty(incident) && !runtime.ForceMajorIncidentForDiagnostics(incident))
                    Debug.LogError("ARSENAL incident unavailable: use black-hole, raid or eclipse for -vfincident.");
            }
        }
    }
}
