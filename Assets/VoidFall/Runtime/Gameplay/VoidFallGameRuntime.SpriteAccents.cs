using System.Collections.Generic;
using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    /// <summary>
    /// Per-frame enemy sprite accent resolution, memoized.
    ///
    /// EnemySpriteAccent resolves through a linear catalog scan plus a hex
    /// ParseColor on every call, and the render loop calls it up to three
    /// times per enemy per frame (body, exploder warning, harvester
    /// overlay). The accent is a pure function of spawn-time identity —
    /// (Id, Elite, EliteKind, MutationGene) never mutate during an enemy's
    /// life — so a memo over exactly those inputs cannot change what is drawn.
    /// The key space is bounded (roster ids × elite flags × elite variants),
    /// so the static cache holds for the process lifetime by design, like
    /// the ProceduralSpriteFactory caches it feeds.
    /// </summary>
    public sealed partial class VoidFallGameRuntime
    {
        private struct AccentCacheKey
        {
            public string Id;
            public int EliteKind;
            public bool Elite;
            public MutationGene MutationGene;

            public AccentCacheKey(string id, int eliteKind, bool elite, MutationGene mutationGene)
            {
                Id = id;
                EliteKind = eliteKind;
                Elite = elite;
                MutationGene = mutationGene;
            }
        }

        private class AccentKeyComparer : IEqualityComparer<AccentCacheKey>
        {
            public static readonly AccentKeyComparer Instance = new AccentKeyComparer();

            public bool Equals(AccentCacheKey x, AccentCacheKey y)
            {
                return x.Elite == y.Elite && x.EliteKind == y.EliteKind &&
                       x.MutationGene == y.MutationGene &&
                       string.Equals(x.Id, y.Id, System.StringComparison.Ordinal);
            }

            public int GetHashCode(AccentCacheKey key)
            {
                unchecked
                {
                    return ((key.Id != null ? key.Id.GetHashCode() : 0) * 397) ^
                           (key.EliteKind * 7) ^ ((int)key.MutationGene * 31) ^ (key.Elite ? 1 : 0);
                }
            }
        }

        private static readonly Dictionary<AccentCacheKey, Color> AccentCache =
            new Dictionary<AccentCacheKey, Color>(AccentKeyComparer.Instance);

        /// <summary>
        /// Allocation-free accent lookup for the render loop. Falls back to
        /// the resolving implementation on the first miss per distinct
        /// (Id, Elite, EliteKind, MutationGene).
        /// </summary>
        private Color CachedEnemySpriteAccent(EnemyState enemy)
        {
            var key = new AccentCacheKey(
                enemy.Id,
                enemy.EliteKind.HasValue ? (int)enemy.EliteKind.Value : -1,
                enemy.Elite,
                enemy.MutationGene);
            if (AccentCache.TryGetValue(key, out var accent)) return accent;
            accent = EnemySpriteAccent(enemy);
            AccentCache[key] = accent;
            return accent;
        }
    }
}
