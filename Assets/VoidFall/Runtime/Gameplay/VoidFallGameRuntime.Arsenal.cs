using System;
using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private const int ArsenalMineCapacity = 26;
        private const int ArsenalSummonCapacity = 24;
        private const int ArsenalBoomerangCapacity = 32;
        private struct ArsenalEntity
        {
            public bool Active, Evolved, Returning, Idle;
            public Vector2 Position;
            public float Age, Angle;
            public int Rank, Hits, Hit0, Hit1, Hit2, Hit3, Hit4, MineIdentity;
        }
        private readonly ArsenalEntity[] _arsenalMines = new ArsenalEntity[ArsenalMineCapacity];
        private readonly ArsenalEntity[] _arsenalSummons = new ArsenalEntity[ArsenalSummonCapacity];
        private readonly ArsenalEntity[] _arsenalBoomerangs = new ArsenalEntity[ArsenalBoomerangCapacity];
        private readonly float[] _arsenalFreeze = new float[MaxEnemies];
        private readonly float[] _arsenalFreezeRecovery = new float[MaxEnemies];
        private readonly int[] _arsenalFreezeIds = new int[MaxEnemies];
        private readonly float[] _clockEnemyHits = new float[MaxEnemies * ArsenalContent.ClockHandCapacity];
        private readonly int[] _clockEnemyIds = new int[MaxEnemies];
        private readonly float[] _clockBossHits = new float[MaxBosses * ArsenalContent.ClockHandCapacity];
        private readonly int[] _clockBossIds = new int[MaxBosses];
        private readonly int[] _arsenalVisited = new int[5];
        private float _arsenalClockAngle;
        private int _arsenalGeneration;
        private int _nextArsenalMineIdentity;

        private int ArsenalRank(int index) => _upgradeProgress != null && index < _upgradeProgress.WeaponRanks.Length ? _upgradeProgress.WeaponRanks[index] : 0;
        private bool ArsenalEvolved(int index) => _upgradeProgress != null && index < _upgradeProgress.Evolved.Length && _upgradeProgress.Evolved[index];
        private WeaponStatsDefinition ArsenalStats(int index, int rank) => ContentCatalog.Weapons[index].Ranks[Mathf.Clamp(rank, 1, 6) - 1].Stats;
        private float ArsenalSizeMultiplier() => (float)SupportEffectRules.ProjectileSizeMultiplier(SupportRank("amplifier")) * (HasWildCard(WildCardId.ColossusArsenal) ? 2f : 1f);

        private void ResetArsenalWeapons()
        {
            ResetOrbitalDefense();
            _arsenalGeneration++;
            Array.Clear(_arsenalMines, 0, _arsenalMines.Length);
            Array.Clear(_arsenalSummons, 0, _arsenalSummons.Length);
            Array.Clear(_arsenalBoomerangs, 0, _arsenalBoomerangs.Length);
            Array.Clear(_arsenalFreeze, 0, _arsenalFreeze.Length);
            Array.Clear(_arsenalFreezeRecovery, 0, _arsenalFreezeRecovery.Length);
            Array.Clear(_arsenalFreezeIds, 0, _arsenalFreezeIds.Length);
            Array.Clear(_clockEnemyIds, 0, _clockEnemyIds.Length);
            Array.Clear(_clockBossIds, 0, _clockBossIds.Length);
            _arsenalClockAngle = 0;
            HideArsenalViews();
        }

        private bool AdvanceArsenalFrozenEnemy(int slot, EnemyState enemy, float dt)
        {
            if (_arsenalFreezeIds[slot] != EnemyIdentity(enemy, slot)) return false;
            // This eligibility timer includes the freeze and the subsequent mobile
            // recovery. Never refresh either timer when another mine overlaps.
            _arsenalFreezeRecovery[slot] = Mathf.Max(0, _arsenalFreezeRecovery[slot] - dt);
            if (_arsenalFreeze[slot] <= 0) return false;
            _arsenalFreeze[slot] = Mathf.Max(0, _arsenalFreeze[slot] - dt);
            return true;
        }

        private void UpdateArsenalWeapons(float dt)
        {
            if (_upgradeProgress == null || _gameSim.Player.Health <= 0 || _gameOver || _revivePending) return;
            // No RNG draws or legacy state mutations when these weapons are unowned.
            if (ArsenalRank(6) <= 0 && ArsenalRank(7) <= 0 && ArsenalRank(8) <= 0 && ArsenalRank(9) <= 0) return;
            var recovery = WeaponRecoveryScale();
            var generation = _arsenalGeneration;
            var player = _gameSim.Player.Position;
            for (var weapon = 6; weapon <= 9; weapon++)
            {
                var rank = ArsenalRank(weapon);
                if (rank <= 0 || weapon == 8) continue;
                _weaponCooldowns[weapon] -= dt;
                if (_weaponCooldowns[weapon] > 0) continue;
                var stats = ArsenalStats(weapon, rank);
                var evolved = ArsenalEvolved(weapon);
                if (weapon == 6)
                {
                    var free = -1;
                    var nearby = false;
                    for (var i = 0; i < _arsenalMines.Length; i++)
                    {
                        if (!_arsenalMines[i].Active) { if (free < 0) free = i; }
                        else if ((_arsenalMines[i].Position - player).sqrMagnitude < 24 * 24) nearby = true;
                    }
                    if (free >= 0 && !nearby)
                    {
                        _audio?.Play(ProceduralAudio.Cue.MineDrop);
                        var identity = ++_nextArsenalMineIdentity;
                        _arsenalMines[free] = new ArsenalEntity { Active = true, Position = player, Rank = rank, Evolved = evolved, MineIdentity = identity };
                        RecordRunHistory("mine_placement", "mines", "placed", sourceId: "mines", instanceId: identity, amount: 1);
                    }
                    else RecordRunHistory("mine_placement", "mines", nearby ? "nearby" : "pool_full", sourceId: "mines");
                }
                else if (weapon == 7)
                {
                    var target = FindNearestHostile((float)stats.Range);
                    var active = 0;
                    foreach (var summon in _arsenalSummons) if (summon.Active) active++;
                    var available = target.Valid ? ArsenalSummonCapacity - active : Mathf.Max(0, stats.ProjectileCount - active);
                    var count = Mathf.Min(stats.ProjectileCount, available);
                    for (var i = 0; i < _arsenalSummons.Length && count > 0; i++)
                    {
                        if (_arsenalSummons[i].Active) continue;
                        var spawnAngle = count * Mathf.PI * 2 / stats.ProjectileCount;
                        var spawnOffset = new Vector2(Mathf.Cos(spawnAngle), Mathf.Sin(spawnAngle)) * 24;
                        _arsenalSummons[i] = new ArsenalEntity { Active = true, Position = player + spawnOffset, Rank = rank, Evolved = evolved, Idle = !target.Valid };
                        count--;
                        _audio?.Play(ProceduralAudio.Cue.SummonSpawn);
                    }
                }
                else
                {
                    var target = FindNearestHostile((float)stats.Range);
                    if (!target.Valid) continue;
                    var count = evolved ? 3 : 1;
                    var shot = 0;
                    for (var i = 0; i < _arsenalBoomerangs.Length && shot < count; i++)
                    {
                        if (_arsenalBoomerangs[i].Active) continue;
                        var delta = target.Position - player;
                        _arsenalBoomerangs[i] = new ArsenalEntity { Active = true, Position = player, Rank = rank, Evolved = evolved, Angle = Mathf.Atan2(delta.y, delta.x) + (shot - (count - 1) * .5f) * .23f };
                        shot++;
                    }
                    if (shot > 0) _audio?.Play(ProceduralAudio.Cue.BoomerangThrow);
                }
                _weaponCooldowns[weapon] = (float)stats.Cooldown * recovery;
                if (weapon == 6) _weaponCooldowns[weapon] = Mathf.Max((float)ArsenalContent.MineMinimumPlacementSeconds, _weaponCooldowns[weapon]);
            }
            StepArsenalMines(dt);
            if (generation != _arsenalGeneration) return;
            StepArsenalSummons(dt);
            if (generation != _arsenalGeneration) return;
            StepArsenalClock(dt, recovery);
            if (generation != _arsenalGeneration) return;
            StepArsenalBoomerangs(dt);
        }

        private void StepArsenalMines(float dt)
        {
            var generation = _arsenalGeneration;
            for (var i = 0; i < _arsenalMines.Length; i++)
            {
                var mine = _arsenalMines[i];
                if (!mine.Active) continue;
                mine.Age += dt;
                if (mine.Age >= ArsenalContent.MineLifetimeSeconds)
                {
                    mine.Active = false;
                    RecordRunHistory("mine_expired", "mines", "lifetime", sourceId: "mines", instanceId: mine.MineIdentity);
                }
                else if (mine.Age >= ArsenalContent.MineArmingSeconds && ArsenalMineTriggered(mine.Position))
                {
                    // Free before damage: death callbacks may reuse pooled combat slots.
                    mine.Active = false;
                    _arsenalMines[i] = mine;
                    var stats = ArsenalStats(6, mine.Rank);
                    var radius = (float)stats.BlastRadius * _areaMultiplier;
                    var frozen = 0;
                    var resisted = 0;
                    if (mine.Evolved)
                    {
                        for (var order = 0; order < _gameSim.EnemyOrderCount; order++)
                        {
                            var slot = _gameSim.EnemyOrder[order];
                            var enemy = _gameSim.Enemies[slot];
                            if (!enemy.Active || enemy.Age < .15f || (enemy.Position - mine.Position).sqrMagnitude > (radius + enemy.Radius) * (radius + enemy.Radius)) continue;
                            var identity = EnemyIdentity(enemy, slot);
                            if (_arsenalFreezeIds[slot] == identity && _arsenalFreezeRecovery[slot] > 0)
                            {
                                resisted++;
                                continue;
                            }
                            _arsenalFreezeIds[slot] = identity;
                            _arsenalFreeze[slot] = (float)ArsenalContent.MineFreezeSeconds;
                            _arsenalFreezeRecovery[slot] = (float)(ArsenalContent.MineFreezeSeconds + ArsenalContent.MineFreezeRecoverySeconds);
                            frozen++;
                        }
                    }
                    // Aggregate control outcomes once per explosion, before damage
                    // callbacks can kill or replace targets or reset the arsenal.
                    RecordRunHistory("mine_detonated", "mines", mine.Evolved ? "evolved" : "base", sourceId: "mines",
                        instanceId: mine.MineIdentity, amount: frozen, blockedAttempts: resisted,
                        durationSeconds: mine.Evolved ? (float)ArsenalContent.MineFreezeSeconds : 0);
                    ArsenalBlast(mine.Position, radius, (float)stats.Damage, 6, mine.Evolved ? "#8ceaff" : "#ffb75e");
                    _audio?.Play(ProceduralAudio.Cue.MineBoom);
                    if (generation != _arsenalGeneration) return;
                }
                _arsenalMines[i] = mine;
            }
        }

        private bool ArsenalMineTriggered(Vector2 position)
        {
            var range = 34f * ArsenalSizeMultiplier();
            for (var order = 0; order < _gameSim.EnemyOrderCount; order++)
            {
                var e = _gameSim.Enemies[_gameSim.EnemyOrder[order]];
                if (e.Active && e.Age >= .15f && (e.Position - position).sqrMagnitude <= (range + e.Radius) * (range + e.Radius)) return true;
            }
            EnsureBossOrderEntries();
            for (var order = 0; order < _gameSim.BossOrderCount; order++)
            {
                var b = _gameSim.Bosses[_gameSim.BossOrder[order]];
                if (b.Active && b.State != 4 && (b.Position - position).sqrMagnitude <= (range + b.Radius) * (range + b.Radius)) return true;
            }
            return false;
        }

        private void StepArsenalSummons(float dt)
        {
            var generation = _arsenalGeneration;
            var player = _gameSim.Player.Position;
            var idleSlot = 0;
            for (var i = 0; i < _arsenalSummons.Length; i++)
            {
                var unit = _arsenalSummons[i];
                if (!unit.Active) continue;
                unit.Age += dt;
                var stats = ArsenalStats(7, unit.Rank);
                var target = FindNearestHostile((float)stats.Range);
                unit.Idle = !target.Valid;
                var side = idleSlot % 2 == 0 ? -1f : 1f;
                var row = idleSlot / 2;
                var hoverOffset = idleSlot == 2 ? new Vector2(0, -43) : new Vector2(side * (37 + Mathf.Min(row, 4) * 9), -16 - row * 7);
                hoverOffset.y += Mathf.Sin((float)_time * 2.3f + idleSlot * 1.7f) * 4;
                var destination = target.Valid ? target.Position : player + hoverOffset;
                if (!target.Valid) idleSlot++;
                var delta = destination - unit.Position;
                unit.Angle = target.Valid ? Mathf.Atan2(delta.y, delta.x) : Mathf.PI * .5f + Mathf.Sin((float)_time * 1.5f + idleSlot) * .09f;
                var speed = (float)stats.ProjectileSpeed * (float)SupportEffectRules.ProjectileSpeedMultiplier(SupportRank("projectileSpeed"));
                if (!target.Valid) speed = Mathf.Max(speed, delta.magnitude * 7);
                unit.Position = Vector2.MoveTowards(unit.Position, destination, speed * dt);
                if (target.Valid && (unit.Position - target.Position).sqrMagnitude <= Mathf.Pow(ArsenalTargetRadius(target) + 7 * ArsenalSizeMultiplier(), 2))
                {
                    unit.Active = false;
                    _arsenalSummons[i] = unit;
                    if (unit.Evolved) ArsenalBlast(unit.Position, (float)stats.BlastRadius * _areaMultiplier, (float)stats.Damage, 7, "#87f5ab");
                    else
                    {
                        ArsenalHit(target, (float)stats.Damage, 7, unit.Position);
                        BurstFx(unit.Position, ParseColor("#87f5ab", Color.white), 5, 110, .25f, .45f);
                    }
                    _audio?.Play(ProceduralAudio.Cue.SummonBlast);
                    if (generation != _arsenalGeneration) return;
                }
                _arsenalSummons[i] = unit;
            }
        }

        private static float ArsenalClockHandAngle(int hand, float angle)
            => hand == 1 ? -angle + Mathf.PI : angle * (float)ArsenalContent.ClockHandSpeed(hand);

        private void StepArsenalClock(float dt, float recovery)
        {
            var generation = _arsenalGeneration;
            var rank = ArsenalRank(8);
            if (rank <= 0) return;
            var stats = ArsenalStats(8, rank);
            _orbitalClockStartAngle = _arsenalClockAngle;
            var advance = (float)stats.OrbitSpeed * dt * OrbitalRotationSpeedScale(recovery);
            _arsenalClockAngle = Mathf.Repeat(_arsenalClockAngle - advance, Mathf.PI * 2);
            for (var hand = 0; hand < ArsenalContent.ClockHandCapacity; hand++)
            {
                if (!ArsenalContent.ClockHandActive(hand, rank, ArsenalEvolved(8))) continue;
                var scale = (float)ArsenalContent.ClockHandScale(hand);
                var reach = (float)stats.OrbitRadius * _areaMultiplier * scale;
                var handAdvance = advance * (float)ArsenalContent.ClockHandSpeed(hand);
                var angle = ArsenalClockHandAngle(hand, _arsenalClockAngle);
                // Walk the initial order length backwards; identity sidecars prevent a recycled slot inheriting a hit timer.
                for (var order = _gameSim.EnemyOrderCount - 1; order >= 0; order--)
                {
                    if (order >= _gameSim.EnemyOrderCount) continue;
                    var slot = _gameSim.EnemyOrder[order];
                    var enemy = _gameSim.Enemies[slot];
                    if (!enemy.Active || enemy.Age < .15f) continue;
                    var id = EnemyIdentity(enemy, slot);
                    if (_clockEnemyIds[slot] != id)
                    {
                        _clockEnemyIds[slot] = id;
                        for (var h = 0; h < ArsenalContent.ClockHandCapacity; h++) _clockEnemyHits[slot * ArsenalContent.ClockHandCapacity + h] = -999;
                    }
                    var hitIndex = slot * ArsenalContent.ClockHandCapacity + hand;
                    if ((float)_time - _clockEnemyHits[hitIndex] < stats.HitCooldown * recovery) continue;
                    if (!ArsenalClockTouches(enemy.Position, enemy.Radius, angle, handAdvance, reach, scale)) continue;
                    _clockEnemyHits[hitIndex] = (float)_time;
                    ArsenalHit(new HostileTarget { Valid = true, Index = slot, Identity = id, Position = enemy.Position }, (float)stats.Damage * scale, 8, _gameSim.Player.Position);
                    if (generation != _arsenalGeneration) return;
                }
                EnsureBossOrderEntries();
                for (var order = 0; order < _gameSim.BossOrderCount; order++)
                {
                    var slot = _gameSim.BossOrder[order];
                    var boss = _gameSim.Bosses[slot];
                    if (!boss.Active || boss.State == 4) continue;
                    var id = BossIdentity(boss, slot);
                    if (_clockBossIds[slot] != id)
                    {
                        _clockBossIds[slot] = id;
                        for (var h = 0; h < ArsenalContent.ClockHandCapacity; h++) _clockBossHits[slot * ArsenalContent.ClockHandCapacity + h] = -999;
                    }
                    var hitIndex = slot * ArsenalContent.ClockHandCapacity + hand;
                    if ((float)_time - _clockBossHits[hitIndex] < stats.HitCooldown * recovery || !ArsenalClockTouches(boss.Position, boss.Radius, angle, handAdvance, reach, scale)) continue;
                    _clockBossHits[hitIndex] = (float)_time;
                    ArsenalHit(new HostileTarget { Valid = true, Boss = true, Index = slot, Identity = id, Position = boss.Position }, (float)stats.Damage * scale, 8, _gameSim.Player.Position);
                    if (generation != _arsenalGeneration) return;
                }
            }
        }

        private bool ArsenalClockTouches(Vector2 position, float radius, float angle, float advance, float reach, float scale)
        {
            var delta = position - _gameSim.Player.Position;
            var distance = delta.magnitude;
            if (distance > reach + radius) return false;
            var width = Mathf.Asin(Mathf.Min(1, (radius + 7 * ArsenalSizeMultiplier() * scale) / Mathf.Max(1, distance)));
            return Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg)) * Mathf.Deg2Rad <= width + advance;
        }

        private void StepArsenalBoomerangs(float dt)
        {
            var generation = _arsenalGeneration;
            for (var i = 0; i < _arsenalBoomerangs.Length; i++)
            {
                var shot = _arsenalBoomerangs[i];
                if (!shot.Active) continue;
                shot.Age += dt;
                var stats = ArsenalStats(9, shot.Rank);
                _arsenalVisited[0] = shot.Hit0; _arsenalVisited[1] = shot.Hit1; _arsenalVisited[2] = shot.Hit2; _arsenalVisited[3] = shot.Hit3; _arsenalVisited[4] = shot.Hit4;
                var target = shot.Returning ? default : FindNearestHostileFrom(shot.Position, shot.Hits == 0 ? (float)stats.Range : 360, default, false, null, _arsenalVisited, shot.Hits);
                if (!target.Valid || shot.Age > 3.5f || shot.Hits >= stats.ChainCount) shot.Returning = true;
                var destination = shot.Returning ? _gameSim.Player.Position : target.Position;
                var speed = (float)stats.ProjectileSpeed * (float)SupportEffectRules.ProjectileSpeedMultiplier(SupportRank("projectileSpeed"));
                if (shot.Age < .13f && !shot.Returning) shot.Position += new Vector2(Mathf.Cos(shot.Angle), Mathf.Sin(shot.Angle)) * speed * dt;
                else shot.Position = Vector2.MoveTowards(shot.Position, destination, speed * dt);
                if (shot.Returning)
                {
                    if ((shot.Position - destination).sqrMagnitude < 18 * 18 || shot.Age > 8) shot.Active = false;
                }
                else if ((shot.Position - destination).sqrMagnitude <= Mathf.Pow(ArsenalTargetRadius(target) + (float)stats.ProjectileRadius * ArsenalSizeMultiplier(), 2))
                {
                    var identity = target.Boss ? -target.Identity : target.Identity;
                    switch (shot.Hits) { case 0: shot.Hit0 = identity; break; case 1: shot.Hit1 = identity; break; case 2: shot.Hit2 = identity; break; case 3: shot.Hit3 = identity; break; case 4: shot.Hit4 = identity; break; }
                    shot.Hits++;
                    _arsenalBoomerangs[i] = shot;
                    ArsenalHit(target, (float)stats.Damage, 9, shot.Position);
                    if (generation != _arsenalGeneration) return;
                    if (shot.Hits >= stats.ChainCount) shot.Returning = true;
                }
                _arsenalBoomerangs[i] = shot;
            }
        }

        private float ArsenalTargetRadius(HostileTarget target) => target.Boss ? _gameSim.Bosses[target.Index].Radius : _gameSim.Enemies[target.Index].Radius;
        private void ArsenalHit(HostileTarget target, float damage, int weapon, Vector2 origin)
        {
            if (!target.Valid) return;
            var critical = _gameSim.Rng.Next() < _critChance;
            damage *= _damageMultiplier * (critical ? 2.1f : 1);
            if (target.Boss)
            {
                var boss = _gameSim.Bosses[target.Index];
                if (boss.Active && BossIdentity(boss, target.Index) == target.Identity) ApplyBossDamage(target.Index, damage, weapon, critical);
            }
            else
            {
                var enemy = _gameSim.Enemies[target.Index];
                if (enemy.Active && EnemyIdentity(enemy, target.Index) == target.Identity) ApplyEnemyDamage(target.Index, damage, target.Position - origin, 30, critical, weapon);
            }
        }
        private void ArsenalBlast(Vector2 position, float radius, float damage, int weapon, string color)
        {
            var critical = _gameSim.Rng.Next() < _critChance;
            DamageArea(position, radius, damage * _damageMultiplier * (critical ? 2.1f : 1), -1, weapon, critical);
            SpawnRingWave(position, 8, radius * 2, .4f, ParseColor(color, Color.white));
        }
    }
}
