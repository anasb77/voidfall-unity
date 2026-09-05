using System;

namespace VoidFall.Core
{
    public readonly struct ArenaCycleResult
    {
        public ArenaCycleResult(string arenaId, string cycleId, double progress)
        {
            ArenaId = arenaId;
            CycleId = cycleId;
            Progress = progress;
        }

        public string ArenaId { get; }
        public string CycleId { get; }
        public double Progress { get; }
    }

    public static class ArenaCycleRules
    {
        public static ArenaCycleResult At(string arenaId, double elapsedSeconds)
        {
            var definition = FindArena(arenaId) ?? ContentCatalog.Arenas[0];
            var loop = 0.0;
            foreach (var cycle in definition.Cycles) loop += cycle.Seconds;
            if (loop <= 0) return new ArenaCycleResult(definition.Id, "steady", 0);

            var time = !double.IsNaN(elapsedSeconds) && !double.IsInfinity(elapsedSeconds)
                ? Math.Max(0, elapsedSeconds)
                : 0;
            var cursor = time % loop;
            foreach (var cycle in definition.Cycles)
            {
                if (cursor < cycle.Seconds)
                {
                    return new ArenaCycleResult(definition.Id, cycle.Id, cursor / cycle.Seconds);
                }
                cursor -= cycle.Seconds;
            }

            var last = definition.Cycles[definition.Cycles.Length - 1];
            return new ArenaCycleResult(definition.Id, last.Id, 0);
        }

        private static ArenaDefinition FindArena(string id)
        {
            foreach (var definition in ContentCatalog.Arenas)
            {
                if (definition.Id == id) return definition;
            }
            return HydraContent.FindArena(id) ?? MonochromeContent.FindArena(id) ?? NullCityContent.FindArena(id);
        }
    }
}
