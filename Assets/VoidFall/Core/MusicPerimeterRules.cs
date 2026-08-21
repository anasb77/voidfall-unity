using System;

namespace VoidFall.Core
{
    public readonly struct MusicPerimeterRunLayout
    {
        public MusicPerimeterRunLayout(int layoutIndex, int longBand, int cornerBand, int fragmentBand)
        {
            LayoutIndex = layoutIndex;
            LongBand = longBand;
            CornerBand = cornerBand;
            FragmentBand = fragmentBand;
        }

        public int LayoutIndex { get; }
        public int LongBand { get; }
        public int CornerBand { get; }
        public int FragmentBand { get; }
    }

    public static class MusicPerimeterRules
    {
        public const int LayoutCount = 4;

        public static float AmbientIntensity(float runSeconds)
        {
            if (runSeconds < 180f) return 0f;
            return Math.Min(0.10f, 0.01f + Math.Max(0f, runSeconds - 180f) / 60f * 0.01f);
        }

        public static float OverclockIntensity(int tier)
        {
            switch (Math.Max(0, Math.Min(3, tier)))
            {
                case 1: return 0.58f;
                case 2: return 0.78f;
                case 3: return 0.94f;
                default: return 0f;
            }
        }

        public static MusicPerimeterRunLayout CreateRunLayout(int runSeed)
        {
            var hash = Mix(unchecked((uint)runSeed) ^ 0x9e3779b9u);
            var layout = (int)(hash % LayoutCount);
            var permutation = (int)((hash >> 8) % 6u);
            // The six permutations of bass/mids/treble. Designed mappings,
            // chosen from the run seed without consuming gameplay RNG.
            var a = permutation < 2 ? 0 : permutation < 4 ? 1 : 2;
            var b = permutation == 0 || permutation == 4 ? 1 : permutation == 1 || permutation == 2 ? 2 : 0;
            var c = 3 - a - b;
            return new MusicPerimeterRunLayout(layout, a, b, c);
        }

        private static uint Mix(uint value)
        {
            value ^= value >> 16;
            value *= 0x7feb352du;
            value ^= value >> 15;
            value *= 0x846ca68bu;
            value ^= value >> 16;
            return value;
        }
    }
}
