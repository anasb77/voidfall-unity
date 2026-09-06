using System;
using System.Collections.Generic;

namespace VoidFall.Runtime
{
    /// <summary>
    /// Measured energetic entry points for Track Shift. Unknown clips start at
    /// zero so adding or renaming soundtrack files remains safe.
    /// </summary>
    public static class MusicTrackEntries
    {
        private static readonly Dictionary<string, float[]> Entries =
            new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "Boss Battle", new[] { 29.5f, 78f } },
                { "Cyber Alley", new[] { 29.5f, 56.25f } },
                { "Drone Patrol", new[] { 79.75f, 168f } },
                { "Hidden Lab", new[] { 11f, 77.5f } },
                { "Holo Arcade", new[] { 15.75f, 92.75f } },
                { "Holo Bazaar", new[] { 25.5f, 73.25f } },
                { "Neon Escape", new[] { 26.75f, 59.75f } },
                { "Neon Rain", new[] { 19.25f, 32.5f } },
                { "Neon Street", new[] { 49.25f, 102.25f } },
                { "Rooftop Chase", new[] { 18f, 25f } },
                { "Synth Syndicate", new[] { 67.5f, 80.75f } },
            };

        public static float Pick(string clipName, System.Random rng)
        {
            if (string.IsNullOrEmpty(clipName) ||
                !Entries.TryGetValue(clipName, out var options) ||
                options == null || options.Length == 0)
            {
                return 0f;
            }

            return options.Length == 1 ? options[0] : options[rng.Next(options.Length)];
        }
    }
}
