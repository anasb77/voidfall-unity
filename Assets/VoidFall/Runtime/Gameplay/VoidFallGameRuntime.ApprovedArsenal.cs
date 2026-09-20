using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private void ScheduleMineChain(ArsenalEntity source, float radius)
        {
            for (var i = 0; i < _arsenalMines.Length; i++)
            {
                var next = _arsenalMines[i];
                if (!next.Active || next.Age < ArsenalContent.MineArmingSeconds || next.ChainDueAge > 0 ||
                    (next.Position - source.Position).sqrMagnitude > radius * radius) continue;
                next.ChainDueAge = next.Age + (float)ArsenalContent.MineChainSeconds;
                next.ChainParentIdentity = source.MineIdentity;
                next.ChainOrigin = source.Position;
                _arsenalMines[i] = next;
                RecordRunHistory("mine_chain", "mines", "scheduled", sourceId: "mines", instanceId: next.MineIdentity,
                    relatedInstanceId: source.MineIdentity, durationSeconds: (float)ArsenalContent.MineChainSeconds, position: next.Position);
            }
        }

        private bool RefreshSummonTarget(ref HostileTarget target)
        {
            if (!target.Valid) return false;
            if (target.Boss)
            {
                if (target.Index < 0 || target.Index >= _gameSim.Bosses.Length) return false;
                var boss = _gameSim.Bosses[target.Index];
                if (!boss.Active || boss.State == 4 || BossIdentity(boss, target.Index) != target.Identity) return false;
                target.Position = boss.Position;
            }
            else
            {
                if (target.Index < 0 || target.Index >= _gameSim.Enemies.Length) return false;
                var enemy = _gameSim.Enemies[target.Index];
                if (!enemy.Active || enemy.Age < .15f || EnemyIdentity(enemy, target.Index) != target.Identity) return false;
                target.Position = enemy.Position;
            }
            return true;
        }

        private HostileTarget AcquireSummonTarget(int slot, ref ArsenalEntity unit, float range)
        {
            var player = _gameSim.Player.Position;
            var leashSquared = (float)(ArsenalContent.SummonReturnLeash * ArsenalContent.SummonReturnLeash);
            var beyondLeash = (unit.Position - player).sqrMagnitude > leashSquared;
            var target = unit.Target;
            var valid = RefreshSummonTarget(ref target);
            if (target.Valid && (!valid || beyondLeash || (target.Position - player).sqrMagnitude > leashSquared))
            {
                RecordRunHistory("summon_target", "summons", valid ? "leash" : "lost", sourceId: target.Boss ? "boss" : "enemy",
                    instanceId: unit.SummonIdentity, relatedInstanceId: target.Identity, position: unit.Position);
                target = default;
            }
            if (beyondLeash) unit.Returning = true;
            if (unit.Returning && (unit.Position - player).sqrMagnitude <= 100 * 100) unit.Returning = false;
            if (!target.Valid && !unit.Returning)
            {
                // Prefer an unclaimed target, with normal insertion-order tie breaking.
                // Fall back to a shared target for isolated enemies and bosses.
                for (var pass = 0; pass < 2 && !target.Valid; pass++)
                {
                    var best = range * range;
                    for (var order = 0; order < _gameSim.EnemyOrderCount; order++)
                    {
                        var index = _gameSim.EnemyOrder[order];
                        var e = _gameSim.Enemies[index];
                        if (!e.Active || e.Age < .15f) continue;
                        ConsiderSummonTarget(slot, unit.Position, player, range, pass == 0,
                            new HostileTarget { Valid = true, Index = index, Identity = EnemyIdentity(e, index), Position = e.Position }, ref target, ref best);
                    }
                    EnsureBossOrderEntries();
                    for (var order = 0; order < _gameSim.BossOrderCount; order++)
                    {
                        var index = _gameSim.BossOrder[order];
                        var b = _gameSim.Bosses[index];
                        if (!b.Active || b.State == 4) continue;
                        ConsiderSummonTarget(slot, unit.Position, player, range, pass == 0,
                            new HostileTarget { Valid = true, Boss = true, Index = index, Identity = BossIdentity(b, index), Position = b.Position }, ref target, ref best);
                    }
                }
                if (target.Valid) RecordRunHistory("summon_target", "summons", "acquired", sourceId: target.Boss ? "boss" : "enemy",
                    instanceId: unit.SummonIdentity, relatedInstanceId: target.Identity, position: unit.Position);
            }
            unit.Target = target;
            return target;
        }

        private void ConsiderSummonTarget(int slot, Vector2 origin, Vector2 player, float range, bool unclaimed,
            HostileTarget candidate, ref HostileTarget target, ref float best)
        {
            var distance = (candidate.Position - origin).sqrMagnitude;
            if (distance >= best || (candidate.Position - player).sqrMagnitude > ArsenalContent.SummonReturnLeash * ArsenalContent.SummonReturnLeash) return;
            if (unclaimed)
                for (var i = 0; i < _arsenalSummons.Length; i++)
                {
                    var other = _arsenalSummons[i];
                    if (i != slot && other.Active && other.Target.Valid && other.Target.Boss == candidate.Boss && other.Target.Identity == candidate.Identity) return;
                }
            best = distance;
            candidate.DistanceSquared = distance;
            target = candidate;
        }

        private void RecordArsenalCancellations()
        {
            if (_gameSim == null) return;
            foreach (var mine in _arsenalMines)
                if (mine.Active) RecordRunHistory("mine_expired", "mines", "arsenal_reset", sourceId: "mines", instanceId: mine.MineIdentity,
                    relatedInstanceId: mine.ChainParentIdentity, position: mine.Position);
            foreach (var unit in _arsenalSummons)
                if (unit.Active) RecordRunHistory("summon_despawn", "summons", "arsenal_reset", sourceId: "summons", instanceId: unit.SummonIdentity, position: unit.Position);
        }
    }
}
