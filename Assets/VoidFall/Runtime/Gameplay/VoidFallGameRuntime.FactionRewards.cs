using System;
using System.Buffers;
using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private struct FactionActor
        {
            public int Identity, TargetSlot, TargetIdentity;
            public CombatFaction Faction;
            public Vector2 TargetPosition, TargetVelocity;
            public float SearchTimer;
            public long Root;
            public bool Rewardable;
            public RewardContribution Contribution;
        }
        private readonly FactionActor[] _factionActors = new FactionActor[MaxEnemies];
        private readonly int[] _factionCandidates = new int[MaxEnemies];
        private readonly RewardRootLedger _rewardRoots = new RewardRootLedger(MaxEnemies + 128);
        private readonly long[] _bossRewardRoots = new long[64];
        private readonly int[] _bossRewardIdentities = new int[64];
        private int _factionControllerSlot = -1, _factionControllerIdentity;
        private CombatFaction _damageFaction = CombatFaction.Player;
        private long _spawnRewardRoot;
        private bool _spawnFromActor;
        private double _fractionalFactionScore;
        private int _rivalOnlyDefeats, _assistedDefeats, _directFactionDefeats;
        public int RivalOnlyDefeats => _rivalOnlyDefeats;
        public int AssistedDefeats => _assistedDefeats;
        public int DirectFactionDefeats => _directFactionDefeats;

        private struct FactionScope : IDisposable
        {
            private readonly VoidFallGameRuntime _runtime;
            private readonly int _slot, _identity;
            private readonly CombatFaction _damage;
            private readonly long _root;
            private readonly bool _fromActor;
            public FactionScope(VoidFallGameRuntime runtime, int slot, int identity, CombatFaction damage, long root, bool descendantSource = false)
            {
                _runtime = runtime; _slot = runtime._factionControllerSlot; _identity = runtime._factionControllerIdentity;
                _damage = runtime._damageFaction; _root = runtime._spawnRewardRoot;
                _fromActor = runtime._spawnFromActor;
                runtime._factionControllerSlot = slot; runtime._factionControllerIdentity = identity;
                runtime._damageFaction = damage; runtime._spawnRewardRoot = root;
                runtime._spawnFromActor = descendantSource || slot >= 0 || root != 0;
                runtime._rewardRoots.Retain(root);
            }
            public void Dispose()
            {
                _runtime._rewardRoots.Release(_runtime._spawnRewardRoot);
                _runtime._factionControllerSlot = _slot; _runtime._factionControllerIdentity = _identity;
                _runtime._damageFaction = _damage; _runtime._spawnRewardRoot = _root;
                _runtime._spawnFromActor = _fromActor;
            }
        }
        private CombatFaction FactionOf(EnemyState enemy) => DestroyerContent.Find(enemy.Id) != null ? CombatFaction.Destroyer : CombatFaction.Enemy;
        private FactionScope EnemyFactionScope(EnemyState enemy)
            => new FactionScope(this, enemy.View, enemy.SpawnId, FactionOf(enemy), _factionActors[enemy.View].Root);
        private FactionScope EnemyDeathScope(EnemyState enemy)
            => new FactionScope(this, _factionControllerSlot, _factionControllerIdentity, _damageFaction, _factionActors[enemy.View].Root, true);
        private long BossRewardRoot(int identity)
        {
            if (identity <= 0) return 0;
            for (var i = 0; i < _bossRewardRoots.Length; i++)
            {
                if (_bossRewardIdentities[i] == identity) return _bossRewardRoots[i];
                if (_bossRewardIdentities[i] != 0) continue;
                _bossRewardIdentities[i] = identity;
                return _bossRewardRoots[i] = _rewardRoots.Create(FactionRewardRules.BossOffspringAllowance);
            }
            return 0;
        }
        private FactionScope BossFactionScope(BossState boss)
            => new FactionScope(this, -1, 0, CombatFaction.Enemy, BossRewardRoot(boss.TelemetryInstanceId), true);

        private void RegisterFactionActor(int slot, EnemyState enemy)
        {
            _rewardRoots.Release(_factionActors[slot].Root);
            var root = _spawnRewardRoot;
            if (root == 0 && enemy.SummonedByBossTelemetryId > 0) root = BossRewardRoot(enemy.SummonedByBossTelemetryId);
            var descendant = _spawnFromActor || root != 0 || enemy.CarrierDrone || enemy.SplitterFragment || enemy.SummonedByBossTelemetryId > 0;
            var rewardable = descendant ? _rewardRoots.ClaimChild(root) : true;
            if (descendant) _rewardRoots.Retain(root);
            else root = _rewardRoots.Create(FactionRewardRules.AmbientOffspringAllowance);
            _factionActors[slot] = new FactionActor { Identity = enemy.SpawnId, Faction = FactionOf(enemy),
                TargetSlot = -1, TargetPosition = _gameSim.Player.Position, Root = root,
                Rewardable = rewardable && root != 0, Contribution = new RewardContribution(enemy.MaxHealth),
                SearchTimer = (enemy.SpawnId % 13) * .025f };
        }
        // Queues retain the actual source before a dead actor's slot can be reused.
        private long CaptureFactionBirthRoot() { _rewardRoots.Retain(_spawnRewardRoot); return _spawnRewardRoot; }
        private void ReleaseFactionBirthRoot(long root) => _rewardRoots.Release(root);
        private FactionScope FactionBirthScope(long root) => new FactionScope(this, -1, 0, CombatFaction.Enemy, root, true);

        private void ResetFactionAndRewardArena()
        {
            EndDestroyerRaid();
            Array.Clear(_factionActors, 0, _factionActors.Length);
            Array.Clear(_bossRewardRoots, 0, _bossRewardRoots.Length);
            Array.Clear(_bossRewardIdentities, 0, _bossRewardIdentities.Length);
            _rewardRoots.Clear(); _spawnRewardRoot = 0; _factionControllerSlot = -1; _factionControllerIdentity = 0;
            _damageFaction = CombatFaction.Player;
            _spawnFromActor = false;
            if (_gameSim != null)
            {
                Array.Clear(_gameSim.HostileShotSources, 0, _gameSim.HostileShotSources.Length);
                Array.Clear(_gameSim.MeteorDamageIdentities, 0, _gameSim.MeteorDamageIdentities.Length);
            }
        }
        private void ResetFactionRunDiagnostics()
        {
            _fractionalFactionScore = 0; _rivalOnlyDefeats = _assistedDefeats = _directFactionDefeats = 0;
        }
        private bool IsLiveFactionTarget(FactionActor actor)
            => actor.TargetSlot >= 0 && actor.TargetSlot < _gameSim.Enemies.Length &&
                _gameSim.Enemies[actor.TargetSlot].Active && _gameSim.Enemies[actor.TargetSlot].Health > 0 &&
                _gameSim.Enemies[actor.TargetSlot].SpawnId == actor.TargetIdentity;
        private Vector2 EnemyControllerTargetPosition => _factionControllerSlot >= 0 &&
            _factionActors[_factionControllerSlot].Identity == _factionControllerIdentity
            ? _factionActors[_factionControllerSlot].TargetPosition : _gameSim.Player.Position;
        private Vector2 EnemyControllerTargetVelocity => _factionControllerSlot >= 0 &&
            _factionActors[_factionControllerSlot].Identity == _factionControllerIdentity
            ? _factionActors[_factionControllerSlot].TargetVelocity : _gameSim.Player.Velocity;

        private Vector2 SelectFactionOpponent(EnemyState enemy, float dt)
        {
            ref var actor = ref _factionActors[enemy.View];
            if (actor.Identity != enemy.SpawnId) RegisterFactionActor(enemy.View, enemy);
            // Preserve the warning point and identity through the entire committed attack.
            if (enemy.State != 0 && (_destroyerRaidActive || actor.TargetIdentity != 0)) return actor.TargetPosition;
            actor.SearchTimer -= dt;
            if (!_destroyerRaidActive)
            {
                actor.TargetSlot = -1; actor.TargetIdentity = 0;
            }
            else if (actor.SearchTimer <= 0 || (actor.TargetSlot >= 0 && !IsLiveFactionTarget(actor)))
            {
                actor.SearchTimer = .28f + (enemy.SpawnId % 7) * .017f;
                var playerDistance = Vector2.Distance(enemy.Position, _gameSim.Player.Position);
                var bestDistance = Mathf.Min(600f, playerDistance);
                var best = -1;
                if (IsLiveFactionTarget(actor))
                {
                    var currentDistance = Vector2.Distance(enemy.Position, _gameSim.Enemies[actor.TargetSlot].Position);
                    if (currentDistance < 720f && currentDistance < playerDistance * 1.25f)
                    { best = actor.TargetSlot; bestDistance = currentDistance * .78f; }
                }
                var count = _gameSim.QueryEnemyNeighborhood(enemy.Position.x, enemy.Position.y, 9, _factionCandidates);
                for (var n = 0; n < count; n++)
                {
                    var slot = _factionCandidates[n]; var candidate = _gameSim.Enemies[slot];
                    if (!candidate.Active || candidate.Health <= 0 || !_gameSim.IsCurrentGridEnemy(slot) ||
                        !FactionRewardRules.Hostile(actor.Faction, FactionOf(candidate))) continue;
                    var distance = Vector2.Distance(enemy.Position, candidate.Position);
                    if (distance >= bestDistance) continue;
                    best = slot; bestDistance = distance;
                }
                actor.TargetSlot = best; actor.TargetIdentity = best >= 0 ? _gameSim.Enemies[best].SpawnId : 0;
            }
            if (IsLiveFactionTarget(actor))
            {
                actor.TargetPosition = _gameSim.Enemies[actor.TargetSlot].Position;
                actor.TargetVelocity = _gameSim.Enemies[actor.TargetSlot].Velocity;
            }
            else { actor.TargetPosition = _gameSim.Player.Position; actor.TargetVelocity = _gameSim.Player.Velocity; }
            return actor.TargetPosition;
        }
        private void EnemyControllerBlast(Vector2 position, float radius, float damage)
            => FactionBlast(position, radius, damage, Vector2.zero, -1f);
        private void FactionBlast(Vector2 position, float radius, float damage, Vector2 forward, float minimumDot)
        {
            var playerDelta = _gameSim.Player.Position - position;
            if (FactionRewardRules.Hostile(_damageFaction, CombatFaction.Player) &&
                playerDelta.magnitude < radius + AttackPlayerRadius &&
                (minimumDot < 0 || Vector2.Dot(playerDelta.normalized, forward) >= minimumDot)) DamagePlayer(damage, playerDelta);
            if (!_destroyerRaidActive) return;
            // Snapshot identities before recursive death effects can recycle any slot.
            var slots = ArrayPool<int>.Shared.Rent(MaxEnemies * 2);
            try
            {
                var count = 0;
                for (var n = 0; n < _gameSim.EnemyOrderCount; n++)
                {
                    var slot = _gameSim.EnemyOrder[n]; var target = _gameSim.Enemies[slot];
                    var delta = target.Position - position;
                    if (!target.Active || !FactionRewardRules.Hostile(_damageFaction, FactionOf(target)) ||
                        delta.magnitude >= radius + target.Radius ||
                        (minimumDot >= 0 && Vector2.Dot(delta.normalized, forward) < minimumDot)) continue;
                    slots[count * 2] = slot; slots[count * 2 + 1] = target.SpawnId; count++;
                }
                for (var n = 0; n < count; n++)
                {
                    var slot = slots[n * 2]; var target = _gameSim.Enemies[slot];
                    if (target.Active && target.SpawnId == slots[n * 2 + 1])
                        ApplyEnemyDamage(slot, damage, target.Position - position, 0, false, -1);
                }
            }
            finally { ArrayPool<int>.Shared.Return(slots); }
        }
        private void FactionContact(ref EnemyState enemy)
        {
            if (!_destroyerRaidActive || enemy.Age <= .4f || enemy.ContactCooldown > 0 || enemy.Id == "exploder" ||
                FactionOf(enemy) == CombatFaction.Destroyer) return;
            ref var actor = ref _factionActors[enemy.View];
            if (!IsLiveFactionTarget(actor)) return;
            var other = _gameSim.Enemies[actor.TargetSlot]; var delta = other.Position - enemy.Position;
            if (delta.magnitude >= enemy.Radius + other.Radius) return;
            ApplyEnemyDamage(actor.TargetSlot, enemy.Damage, delta, 0, false, -1);
            enemy.ContactCooldown = .72f;
        }
        private void AwardFactionScore(double score)
        {
            _fractionalFactionScore += Math.Max(0, score);
            var whole = (int)Math.Floor(_fractionalFactionScore);
            _score += whole; _fractionalFactionScore -= whole;
        }
    }
}
