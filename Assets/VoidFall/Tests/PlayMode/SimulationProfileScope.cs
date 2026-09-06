using System;
using System.IO;
using System.Reflection;
using VoidFall.Persistence;
using VoidFall.Runtime;

namespace VoidFall.Tests.PlayMode
{
    /// <summary>Golden fixtures must not inherit purchased ranks or write to a player's profile.</summary>
    internal sealed class SimulationProfileScope : IDisposable
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private readonly VoidFallGameRuntime _runtime;
        private readonly object _profile;
        private readonly object _store;
        private readonly object _seedOverride;
        private readonly string _directory;

        public SimulationProfileScope(VoidFallGameRuntime runtime)
        {
            _runtime = runtime;
            _profile = Field("_saveData").GetValue(runtime);
            _store = Field("_saveStore").GetValue(runtime);
            _seedOverride = Field("_diagnosticRunSeedOverride").GetValue(runtime);
            _directory = Path.Combine(Path.GetTempPath(), "voidfall-golden-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            Field("_saveData").SetValue(runtime, SaveStore.CreateDefault());
            Field("_saveStore").SetValue(runtime, new SaveStore(Path.Combine(_directory, "profile.json")));
        }

        public void Dispose()
        {
            _runtime.ClearStressScenario();
            Field("_runSaved").SetValue(_runtime, true);
            Field("_gameOver").SetValue(_runtime, false);
            Field("_saveData").SetValue(_runtime, _profile);
            Field("_saveStore").SetValue(_runtime, _store);
            typeof(VoidFallGameRuntime).GetMethod("EnterMainMenu", Flags).Invoke(_runtime, null);
            Field("_diagnosticRunSeedOverride").SetValue(_runtime, _seedOverride);
            Directory.Delete(_directory, true);
        }

        private static FieldInfo Field(string name) => typeof(VoidFallGameRuntime).GetField(name, Flags);
    }
}
