using VoidFall.Core;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private const float RunOpeningSeconds = 1.5f;
        private DirectorProfileId _selectedDirectorProfile = DirectorProfileId.Standard;
        private DirectorProfileId _runDirectorProfile = DirectorProfileId.Standard;
        private readonly RunPressureState _runPressure = new RunPressureState();

        public int PressureHundredths => _runPressure.PressureHundredths;
        public DirectorProfileId RunDirectorProfile => _runDirectorProfile;
        private float DirectorProgressSeconds => (float)_runPressure.CreditedProgressSeconds;

        private void SeedDiagnosticDirectorProgress(float seconds)
        {
            // Probe inputs are raw time: six-minute survival plus an assumed minute of boss combat.
            // ObserveStage still converts these fractions to the canonical 300+60 scoring units.
            var survivalSeconds = (float)VoidProgressionRules.SurvivalSeconds;
            const float diagnosticBossSeconds = 60f;
            for (var stage = 0; stage < 6; stage++)
            {
                var local = seconds - stage * (survivalSeconds + diagnosticBossSeconds);
                _runPressure.ObserveStage(stage, System.Math.Max(0, System.Math.Min(1, local / survivalSeconds)),
                    System.Math.Max(0, System.Math.Min(1, (local - survivalSeconds) / diagnosticBossSeconds)));
            }
        }
    }
}
