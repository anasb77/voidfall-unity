using System;

namespace VoidFall.Core
{

/// <summary>
/// Port of src/game/rng.ts. Keep arithmetic unsigned and unchecked so the
/// JavaScript Math.imul/uint32 behavior remains stable across platforms.
/// </summary>
public sealed class Rng
{
    private const uint DefaultState = 0x6d2b79f5u;
    private uint _state;

    public Rng(uint seed)
    {
        _state = seed == 0 ? DefaultState : seed;
    }

    public int Draws { get; private set; }

    public double Next()
    {
        unchecked
        {
            _state += DefaultState;
            var value = _state;
            value = (value ^ (value >> 15)) * (value | 1u);
            value ^= value + ((value ^ (value >> 7)) * (value | 61u));
            Draws++;
            return (value ^ (value >> 14)) / 4294967296.0;
        }
    }

    public double Range(double min, double max) => min + (max - min) * Next();

    public int Int(int maxExclusive)
    {
        if (maxExclusive <= 0) return 0;
        return (int)Math.Floor(Next() * maxExclusive);
    }
}
}
