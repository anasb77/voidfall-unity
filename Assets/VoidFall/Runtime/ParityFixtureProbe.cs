using System;
using System.IO;
using UnityEngine;

namespace VoidFall.Runtime
{

[Serializable]
internal sealed class ParitySourceInfo
{
    public string repository;
    public string sourceCommit;
    public string unityEditor;
}

[Serializable]
internal sealed class ParityIdentifiers
{
    public string[] arenas;
    public string[] weapons;
    public string[] enemies;
    public string[] bosses;
    public string[] supportIds;
    public string[] lateUpgradeIds;
    public string[] evolutionWeaponIds;
    public string[] stressScenarios;
}

[Serializable]
internal sealed class ParityFixtureEnvelope
{
    public int schema;
    public ParitySourceInfo source;
    public ParityIdentifiers identifiers;
}

[DefaultExecutionOrder(-1000)]
public sealed class ParityFixtureProbe : MonoBehaviour
{
    private const string FixtureRelativePath = "VoidFall/web-parity.json";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateProbe()
    {
        var root = new GameObject("VoidFall");
        DontDestroyOnLoad(root);
            root.AddComponent<ParityFixtureProbe>();
            root.AddComponent<FixedGameLoop>();
            root.AddComponent<VoidFallGameRuntime>();
    }

    private void Awake()
    {
        var path = Path.Combine(Application.streamingAssetsPath, FixtureRelativePath);
        if (!File.Exists(path))
        {
            Debug.LogError($"VoidFall parity fixture missing: {path}");
            return;
        }

        try
        {
            var fixture = JsonUtility.FromJson<ParityFixtureEnvelope>(File.ReadAllText(path));
            if (fixture == null || fixture.identifiers == null || fixture.source == null)
            {
                Debug.LogError("VoidFall parity fixture could not be parsed.");
                return;
            }

            Debug.Log(
                $"VoidFall parity fixture loaded: {fixture.source.sourceCommit}; " +
                $"arenas={fixture.identifiers.arenas?.Length ?? 0}, " +
                $"weapons={fixture.identifiers.weapons?.Length ?? 0}, " +
                $"enemies={fixture.identifiers.enemies?.Length ?? 0}, " +
                $"bosses={fixture.identifiers.bosses?.Length ?? 0}");
        }
        catch (Exception exception)
        {
            Debug.LogError($"VoidFall parity fixture parse failed: {exception.Message}");
        }
    }
}
}
