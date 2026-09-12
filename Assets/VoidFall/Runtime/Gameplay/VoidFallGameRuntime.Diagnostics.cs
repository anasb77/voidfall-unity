using System.Diagnostics;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private long _diagnosticSimulationTicks;
        private double _diagnosticSimulationCpuMilliseconds;
        private bool _benchmarkDriving;

        public long DiagnosticSimulationTicks => _diagnosticSimulationTicks;
        public float DiagnosticCombatSeconds => _time;
        public double DiagnosticDamageDealt => _damageDealt;
        public int DiagnosticKills => _kills;
        public double DiagnosticSimulationCpuMilliseconds => _diagnosticSimulationCpuMilliseconds;
        public string DiagnosticPauseReason => _mainMenuBrowsing ? "menu" : _gameOver ? "game-over" :
            _levelUpActive ? "upgrade" : _rouletteActive ? "roulette" : _paused ? "paused" :
            JourneyStopsCombat ? "journey" : "combat";

        public void PrepareBenchmarkFrame()
        {
            if (_stressScenario == null && !_directorPlaytestActive) return;
            _benchmarkDriving = true;
            _diagnosticSimulationCpuMilliseconds = 0;
            // A diagnostic run chooses an offered upgrade through the real grant path.
            // Normal runs never call this; unknown/manual pause ownership is not overridden.
            if (_levelUpActive && _levelOptions != null && _levelOptions.Length > 0)
                SelectLevelOption(0);
            if (_directorPlaytestActive && _revivePending) AcceptRevive();
        }

        private void ResetDiagnosticCounters()
        {
            _diagnosticSimulationTicks = 0;
            _diagnosticSimulationCpuMilliseconds = 0;
            _benchmarkDriving = false;
        }

        private long BeginDiagnosticStep(float dt)
        {
            if (dt > 0) _diagnosticSimulationTicks++;
            return _benchmarkDriving ? Stopwatch.GetTimestamp() : 0;
        }

        private void EndDiagnosticStep(long started)
        {
            if (started == 0) return;
            _diagnosticSimulationCpuMilliseconds +=
                (Stopwatch.GetTimestamp() - started) * (1000.0 / Stopwatch.Frequency);
        }
    }
}
