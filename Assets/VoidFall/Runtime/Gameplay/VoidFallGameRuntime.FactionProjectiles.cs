using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        // Entry fraction, rather than closest point, makes near-body interception deterministic.
        private static float SegmentBodyFraction(Vector2 from, Vector2 to, Vector2 center, float radius)
        {
            var offset = from - center; var motion = to - from;
            var c = Vector2.Dot(offset, offset) - radius * radius;
            if (c <= 0) return 0;
            var a = Vector2.Dot(motion, motion); if (a < .000001f) return float.PositiveInfinity;
            var b = Vector2.Dot(offset, motion); var discriminant = b * b - a * c;
            if (discriminant < 0) return float.PositiveInfinity;
            var t = (-b - Mathf.Sqrt(discriminant)) / a;
            return t >= 0 && t <= 1 ? t : float.PositiveInfinity;
        }
        private bool QueryFactionShotBody(int slot, Vector2 from, Vector2 to, float radius,
            out float fraction, out int target, out int identity)
        {
            fraction = float.PositiveInfinity; target = -2; identity = 0;
            var source = _gameSim.HostileShotSources[slot];
            if (FactionRewardRules.Hostile(source.Faction, CombatFaction.Player) && _gameSim.Player.Health > 0)
            {
                fraction = SegmentBodyFraction(from, to, _gameSim.Player.Position, radius + AttackPlayerRadius);
                if (fraction <= 1) target = -1;
            }
            var midpoint = (from + to) * .5f;
            var cells = Mathf.CeilToInt(((to - from).magnitude * .5f + radius + 90) / CollisionGrid.CellSize);
            var count = _gameSim.QueryEnemyNeighborhood(midpoint.x, midpoint.y, cells, _factionCandidates);
            for (var n = 0; n < count; n++)
            {
                var candidateSlot = _factionCandidates[n]; var candidate = _gameSim.Enemies[candidateSlot];
                if (!candidate.Active || candidate.Health <= 0 || !_gameSim.IsCurrentGridEnemy(candidateSlot) ||
                    !FactionRewardRules.Hostile(source.Faction, FactionOf(candidate))) continue;
                var entry = SegmentBodyFraction(from, to, candidate.Position, radius + candidate.Radius);
                if (entry >= fraction) continue;
                fraction = entry; target = candidateSlot; identity = candidate.SpawnId;
            }
            return target != -2;
        }
        private void ImpactFactionShotBody(int slot, int target, int identity)
        {
            var shot = _gameSim.HostileShots[slot]; var source = _gameSim.HostileShotSources[slot];
            using (new FactionScope(this, source.Slot, source.SpawnId, source.Faction, 0))
            {
                if (target < 0) { DamagePlayer(shot.Damage, shot.Velocity); return; }
                var enemy = _gameSim.Enemies[target];
                if (enemy.Active && enemy.SpawnId == identity) ApplyEnemyDamage(target, shot.Damage, shot.Velocity, 0, false, -1);
            }
        }
    }
}
