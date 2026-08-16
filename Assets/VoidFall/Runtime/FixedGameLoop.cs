using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{

[DefaultExecutionOrder(-900)]
public sealed class FixedGameLoop : MonoBehaviour
{
    private readonly FixedStepClock _clock = new FixedStepClock();
    private double _elapsedSeconds;

    public double ElapsedSeconds => _elapsedSeconds;
    public int FixedTicks { get; private set; }

    private void Update()
    {
        _clock.Consume(Time.unscaledDeltaTime, Step);
    }

    private void Step(double dt)
    {
        _elapsedSeconds += dt;
        FixedTicks++;
    }
}
}
