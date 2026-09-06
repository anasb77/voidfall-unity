using System;
using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private EonSeaTerrain _eonSeaTerrain;
        private GameSim.TerrainProjectileCollision _eonSeaProjectileCollisionHook;
        private Action<int> _eonSeaBulletCoverHook;
        private EonSeaFreeze _eonSeaPlayerFreeze;
        private readonly EonSeaFreeze[] _eonSeaEnemyFreeze = new EonSeaFreeze[MaxEnemies];
        private readonly EonSeaFreeze[] _eonSeaBossFreeze = new EonSeaFreeze[MaxBosses];
        private readonly Vector2[] _eonSeaSeparationOrigins = new Vector2[MaxEnemies];
        private bool CurrentVoidIsEonSea => _arenaId == ArenaId.EonSea && !_mainMenuBrowsing;
        private bool EonSeaTerrainActive => CurrentVoidIsEonSea &&
            (_journeyStage == JourneyStage.Combat || _journeyStage == JourneyStage.Rewards);

        private void ResetEonSeaState()
        {
            _eonSeaTerrain = null;
            ClearEonSeaFreeze();
        }

        private void ClearEonSeaFreeze()
        {
            _eonSeaPlayerFreeze = default;
            Array.Clear(_eonSeaEnemyFreeze, 0, _eonSeaEnemyFreeze.Length);
            Array.Clear(_eonSeaBossFreeze, 0, _eonSeaBossFreeze.Length);
        }

        private void EnsureEonSeaTerrain()
        {
            if (_eonSeaTerrain == null)
                _eonSeaTerrain = new EonSeaTerrain(_runSeed, _gameSim.Player.Position.x, _gameSim.Player.Position.y);
            _eonSeaTerrain.Stream(_gameSim.Player.Position.x, _gameSim.Player.Position.y);
        }

        private void StepEonSea(float dt)
        {
            if (!EonSeaTerrainActive || _gameSim.Player.Health <= 0)
            {
                if (_eonSeaTerrain != null) ResetEonSeaState();
                return;
            }
            EnsureEonSeaTerrain();
            // Reward collection retains cover/slip but has no new freezes or stale slow timer.
            var combat = _journeyStage == JourneyStage.Combat;
            if (!combat) ClearEonSeaFreeze();
            _eonSeaTerrain.Step(dt, combat);
            if (!combat) return;
            foreach (var pulse in _eonSeaTerrain.Collapses)
            {
                var centre = new Vector2(pulse.X, pulse.Y);
                if ((_gameSim.Player.Position - centre).sqrMagnitude < Square(pulse.Radius + PlayerRadius))
                    _eonSeaPlayerFreeze.Refresh(1, _eonSeaTerrain.Time);
                for (var order = 0; order < _gameSim.EnemyOrderCount; order++)
                {
                    var slot = _gameSim.EnemyOrder[order]; var enemy = _gameSim.Enemies[slot];
                    if (enemy.Active && enemy.Health > 0 && (enemy.Position - centre).sqrMagnitude < Square(pulse.Radius + enemy.Radius))
                        _eonSeaEnemyFreeze[slot].Refresh(enemy.SpawnId, _eonSeaTerrain.Time);
                }
                for (var order = 0; order < _gameSim.BossOrderCount; order++)
                {
                    var slot = _gameSim.BossOrder[order]; var boss = _gameSim.Bosses[slot];
                    if (boss.Active && boss.Health > 0 && (boss.Position - centre).sqrMagnitude < Square(pulse.Radius + boss.Radius))
                        _eonSeaBossFreeze[slot].Refresh(boss.TelemetryInstanceId, _eonSeaTerrain.Time);
                }
            }
        }

        private static float Square(float value) => value * value;

        private bool MoveEonSeaPlayer(float dt, Vector2 input, float speed)
        {
            if (!EonSeaTerrainActive) return false;
            EnsureEonSeaTerrain();
            if (_journeyStage != JourneyStage.Combat) _eonSeaPlayerFreeze = default;
            var position = _gameSim.Player.Position;
            speed *= _eonSeaPlayerFreeze.Scale(1, _eonSeaTerrain.Time);
            var target = Vector2.ClampMagnitude(input, 1f) * speed;
            var slippery = _eonSeaTerrain.SlipperyAt(position.x, position.y);
            var rate = slippery ? (input.sqrMagnitude > 0.0001f ? 1.8f : 0.3f) : 14f;
            var velocity = Vector2.Lerp(_gameSim.Player.Velocity, target, 1f - Mathf.Exp(-rate * dt));
            velocity = Vector2.ClampMagnitude(velocity, speed);
            var x = position.x; var y = position.y;
            var blocked = _eonSeaTerrain.Move(ref x, ref y, velocity.x * dt, velocity.y * dt, PlayerRadius);
            _gameSim.Player.Position = new Vector2(x, y);
            _gameSim.Player.Velocity = blocked && dt > 0 ? (_gameSim.Player.Position - position) / dt : velocity;
            // A capsule correction must never leave frozen momentum above the cap.
            _gameSim.Player.Velocity = Vector2.ClampMagnitude(_gameSim.Player.Velocity, speed);
            _eonSeaTerrain.Stream(x, y);
            return true;
        }

        private void ResolveEonSeaEnemyMovement(int slot, ref EnemyState enemy, Vector2 oldPosition)
        {
            if (!EonSeaTerrainActive || _eonSeaTerrain == null || !enemy.Active) return;
            var scale = _eonSeaEnemyFreeze[slot].Scale(enemy.SpawnId, _eonSeaTerrain.Time);
            var displacement = (enemy.Position - oldPosition) * scale;
            var x = oldPosition.x; var y = oldPosition.y;
            _eonSeaTerrain.Move(ref x, ref y, displacement.x, displacement.y, enemy.Radius);
            enemy.Position = new Vector2(x, y);
        }

        private void SeparateEonSeaEnemies()
        {
            for (var order = 0; order < _gameSim.EnemyOrderCount; order++)
            {
                var slot = _gameSim.EnemyOrder[order];
                _eonSeaSeparationOrigins[slot] = _gameSim.Enemies[slot].Position;
            }
            _gameSim.SeparateEnemies();
            // Pair relaxation is another displacement, after ordinary movement.
            // Sweep from the pre-pass position so crowd pressure cannot cross cover.
            // This is collision correction, not movement: don't apply freeze twice.
            for (var order = 0; order < _gameSim.EnemyOrderCount; order++)
            {
                var slot = _gameSim.EnemyOrder[order];
                var enemy = _gameSim.Enemies[slot];
                if (!enemy.Active) continue;
                var origin = _eonSeaSeparationOrigins[slot];
                var displacement = enemy.Position - origin;
                var x = origin.x; var y = origin.y;
                _eonSeaTerrain.Move(ref x, ref y, displacement.x, displacement.y, enemy.Radius);
                enemy.Position = new Vector2(x, y);
                _gameSim.Enemies[slot] = enemy;
            }
        }

        private void ResolveEonSeaBossMovement(int slot, ref BossState boss, Vector2 oldPosition)
        {
            if (!EonSeaTerrainActive || _eonSeaTerrain == null || !boss.Active) return;
            var scale = _eonSeaBossFreeze[slot].Scale(boss.TelemetryInstanceId, _eonSeaTerrain.Time);
            var displacement = (boss.Position - oldPosition) * scale;
            var x = oldPosition.x; var y = oldPosition.y;
            _eonSeaTerrain.Move(ref x, ref y, displacement.x, displacement.y, boss.Radius);
            boss.Position = new Vector2(x, y);
        }

        private bool EonSeaBlocksProjectile(Vector2 from, Vector2 to, float radius, out Vector2 hit)
        {
            hit = to;
            if (!EonSeaTerrainActive || _eonSeaTerrain == null) return false;
            var blocked = _eonSeaTerrain.FirstHit(from.x, from.y, to.x, to.y, radius, out var x, out var y);
            hit = new Vector2(x, y); return blocked;
        }

        private void ConfigureEonSeaProjectileHooks()
        {
            if (_eonSeaProjectileCollisionHook == null) _eonSeaProjectileCollisionHook = EonSeaBlocksProjectile;
            if (_eonSeaBulletCoverHook == null) _eonSeaBulletCoverHook = OnEonSeaBulletCoverHit;
            _gameSim.TerrainProjectileCollisionHook = EonSeaTerrainActive ? _eonSeaProjectileCollisionHook : null;
            _gameSim.BulletTerrainHitHook = EonSeaTerrainActive ? _eonSeaBulletCoverHook : null;
        }

        private void OnEonSeaBulletCoverHit(int slot)
        {
            var bullet = _gameSim.Bullets[slot];
            if (bullet.BlastRadius <= 0) return;
            var weaponId = ContentCatalog.Weapons[Mathf.Clamp(bullet.WeaponIndex, 0, ContentCatalog.Weapons.Length - 1)].Id;
            DamageArea(bullet.Position, bullet.BlastRadius,
                bullet.Damage * (weaponId == "seeker" ? 0.8f : 0.35f), -1, bullet.WeaponIndex);
            if (bullet.Cluster)
            {
                bullet.Cluster = false;
                _gameSim.Bullets[slot] = bullet;
                SpawnClusterCharges(bullet);
            }
        }

        private void StressEonSeaIce(Vector2 position, float radius)
        {
            if (!EonSeaTerrainActive || _journeyStage != JourneyStage.Combat || _eonSeaTerrain == null) return;
            _eonSeaTerrain.Explode(position.x, position.y, radius);
        }
    }
}
