using UnityEngine;

namespace VoidFall.Runtime
{
    public static class HydraRuntimeRules
    {
        public const float RibCageHalfWidth = 500f;
        public const float RibCageHalfHeight = 330f;

        // The authored 1024px Hydra Prime plate contains transparent breathing
        // room for the toxic tendrils. Reusing the old procedural boss scale
        // made the visible head less than half the approved v13 footprint.
        public const float BossPresentationScale = 2.3f;
        public const float EvasionPresentationScale = 1.04f;

        public static Vector2 ClampPlayerToRibCage(
            Vector2 position,
            Vector2 centre,
            float playerRadius,
            bool collisionActive)
        {
            if (!collisionActive) return position;
            var halfWidth = Mathf.Max(1f, RibCageHalfWidth - Mathf.Max(0f, playerRadius));
            var halfHeight = Mathf.Max(1f, RibCageHalfHeight - Mathf.Max(0f, playerRadius));
            var local = position - centre;
            var normalized = local.x * local.x / (halfWidth * halfWidth) +
                             local.y * local.y / (halfHeight * halfHeight);
            if (normalized <= 1f) return position;
            return centre + local / Mathf.Sqrt(normalized);
        }

        public static bool SuppressAmbientSpawns(string voidId, bool bossPhaseActive) =>
            bossPhaseActive && string.Equals(voidId, "hydra", System.StringComparison.Ordinal);

        public static int MarrowBombsDue(float elapsedSeconds, double[] intervals)
        {
            if (intervals == null || intervals.Length == 0) return 0;
            var elapsed = Mathf.Max(0f, elapsedSeconds);
            var dueAt = 0.0;
            var count = 0;
            for (var index = 0; index < intervals.Length; index++)
            {
                dueAt += System.Math.Max(0, intervals[index]);
                if (elapsed + 0.000001f < dueAt) break;
                count++;
            }
            return count;
        }

        public static bool BossCanTakeDamage(string bossId, string attackId, int state) =>
            !(bossId == "hydra-prime" && attackId == "hydra-evasion" && state == 2);

        public static int EvasionStep(float elapsedSeconds, float activeSeconds)
        {
            var duration = Mathf.Max(0.001f, activeSeconds);
            var stepDuration = duration / VoidFall.Core.HydraEncounterRules.EvasionSocketCount;
            return Mathf.Clamp(
                Mathf.FloorToInt(Mathf.Max(0f, elapsedSeconds) / stepDuration),
                0,
                VoidFall.Core.HydraEncounterRules.EvasionSocketCount - 1);
        }

        public static VoidFall.Core.MutationGene RollMutation(
            VoidFall.Core.Rng rng,
            VoidFall.Core.ArenaId arena,
            bool bossPhaseActive,
            bool elite,
            int recombinationStage,
            string chassisBehavior)
        {
            if (arena != VoidFall.Core.ArenaId.Hydra || bossPhaseActive) return VoidFall.Core.MutationGene.None;
            var stage = Mathf.Clamp(recombinationStage, 0, 2);
            if (elite && !VoidFall.Core.MutationRules.EliteHybridAllowed(stage))
                return VoidFall.Core.MutationGene.None;
            return VoidFall.Core.MutationRules.RollHybrid(rng, chassisBehavior, stage);
        }
    }
}
