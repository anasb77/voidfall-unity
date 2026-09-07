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
            for (var stage = 0; stage < 6; stage++)
            {
                var local = seconds - stage * 360f;
                _runPressure.ObserveStage(stage, System.Math.Max(0, System.Math.Min(1, local / 300)),
                    System.Math.Max(0, System.Math.Min(1, (local - 300) / 60)));
            }
        }
    }
}
