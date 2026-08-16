using System.Collections.Generic;

namespace VoidFall.Core
{
    public static class EvolutionRules
    {
        public const double RailLanceTrailSeconds = 2.25;
        public const double RailLanceDamageSeconds = 1.05;

        public static bool IsReady(string weaponId, int weaponRank, int supportRank, bool evolved)
        {
            if (evolved || weaponRank < ProgressionRules.MaxWeaponRank) return false;
            foreach (var evolution in ContentCatalog.Evolutions)
            {
                if (evolution.WeaponId == weaponId)
                {
                    foreach (var support in ContentCatalog.Supports)
                    {
                        if (support.Id == evolution.SupportId)
                        {
                            return supportRank >= support.MaxRank;
                        }
                    }
                }
            }

            return false;
        }

        public static EvolutionDefinition[] Ready(
            IReadOnlyDictionary<string, int> weaponRanks,
            IReadOnlyDictionary<string, int> supportRanks,
            IReadOnlyDictionary<string, bool> evolved)
        {
            var ready = new List<EvolutionDefinition>();
            foreach (var evolution in ContentCatalog.Evolutions)
            {
                var weaponRank = weaponRanks.TryGetValue(evolution.WeaponId, out var wr) ? wr : 0;
                var supportRank = supportRanks.TryGetValue(evolution.SupportId, out var sr) ? sr : 0;
                var isEvolved = evolved.TryGetValue(evolution.WeaponId, out var ev) && ev;
                if (IsReady(evolution.WeaponId, weaponRank, supportRank, isEvolved)) ready.Add(evolution);
            }

            return ready.ToArray();
        }
    }
}
