namespace VoidFall.Core
{

public static class SimulationRules
{
    public const double FixedStepSeconds = 1.0 / 60.0;
    public const int MaxCatchUpSteps = 3;
    public const int MaxBullets = 280;
    public const int MaxHostileShots = 90;
    public const int MaxPickups = 280;
    public const int MaxParticles = 280;
    public const int MaxFloaters = 42;
    public const int MaxDeathGhosts = 80;
    public const int MaxActiveEnemies = 192;
}

public sealed class FixedStepClock
{
    private double _accumulator;

    public int Consume(double frameSeconds, System.Action<double> step)
    {
        var safeFrameSeconds = !double.IsNaN(frameSeconds) && !double.IsInfinity(frameSeconds)
            ? System.Math.Max(0, frameSeconds)
            : 0;
        _accumulator = System.Math.Min(
            SimulationRules.MaxCatchUpSteps * SimulationRules.FixedStepSeconds,
            _accumulator + safeFrameSeconds);

        var steps = 0;
        while (_accumulator >= SimulationRules.FixedStepSeconds && steps < SimulationRules.MaxCatchUpSteps)
        {
            step(SimulationRules.FixedStepSeconds);
            _accumulator -= SimulationRules.FixedStepSeconds;
            steps++;
        }

        if (steps >= SimulationRules.MaxCatchUpSteps)
        {
            _accumulator = 0;
        }

        return steps;
    }
}
}
