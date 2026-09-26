using System;
using UnityEngine;
using VoidFall.Core;
namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private struct CrascendoGrowthState { public int Identity, Hits; public float BaseRadius, NaturalRadius; public bool Pulsed; }
        private readonly CrascendoGrowthState[] _crascendoEnemies = new CrascendoGrowthState[MaxEnemies];
        private CrascendoGrowthState[] _crascendoBosses;
        private Vector2[] _crascendoBossPush;
        private float _crascendoElapsed;
        private bool CurrentVoidIsCrascendo => _arenaId == ArenaId.Crascendo;
        private void ResetCrascendoState()
        {
            Array.Clear(_crascendoEnemies, 0, _crascendoEnemies.Length);
            _crascendoBosses = new CrascendoGrowthState[_gameSim.Bosses.Length];
            _crascendoBossPush = new Vector2[_gameSim.Bosses.Length]; _crascendoElapsed = 0;
            _gameSim.EnemyNaturalRadiusHook = CurrentVoidIsCrascendo ? CrascendoNaturalRadius : null;
            _gameSim.ExpandEnemyQueriesForRadius = CurrentVoidIsCrascendo;
            _gameSim.EnemyQueryPadding = 0;
        }
        private void StepCrascendo(float dt)
        {
            if (!CurrentVoidIsCrascendo) return;
            _crascendoElapsed += dt;
            if (_crascendoBossPush == null) return;
            for (var i = 0; i < _crascendoBossPush.Length; i++)
            {
                if (!_gameSim.Bosses[i].Active) { _crascendoBossPush[i] = Vector2.zero; continue; }
                _gameSim.Bosses[i].Position += _crascendoBossPush[i] * ((1 - Mathf.Exp(-6 * dt)) / 6);
                _crascendoBossPush[i] *= Mathf.Exp(-6 * dt);
            }
        }
        private void GrowCrascendoEnemy(int index, ref EnemyState enemy, float damage)
        {
            if (!CurrentVoidIsCrascendo || damage <= 0 || float.IsNaN(damage) || float.IsInfinity(damage)) return;
            ref var state = ref _crascendoEnemies[index];
            if (state.Identity != enemy.SpawnId || state.BaseRadius <= 0) state = new CrascendoGrowthState { Identity = enemy.SpawnId, BaseRadius = enemy.Radius, NaturalRadius = enemy.Radius };
            state.Hits = Mathf.Min(CrascendoRules.HitsToMaximum, state.Hits + 1);
            enemy.Radius = Mathf.Min(state.BaseRadius * CrascendoRules.MaximumGrowth, Mathf.Max(enemy.Radius + state.BaseRadius * CrascendoRules.GrowthPerHit, state.BaseRadius * CrascendoRules.Growth(state.Hits)));
            _gameSim.IncludeEnemyQueryRadius(enemy.Radius);
        }
        private float CrascendoNaturalRadius(EnemyState enemy, float naturalRadius)
        {
            ref var state = ref _crascendoEnemies[enemy.View];
            if (state.Identity != enemy.SpawnId || state.BaseRadius <= 0) state = new CrascendoGrowthState { Identity = enemy.SpawnId, BaseRadius = enemy.Radius, NaturalRadius = enemy.Radius };
            var increment = Mathf.Max(0, naturalRadius - state.NaturalRadius);
            state.NaturalRadius = Mathf.Max(state.NaturalRadius, naturalRadius);
            var radius = Mathf.Min(state.BaseRadius * CrascendoRules.MaximumGrowth, enemy.Radius + increment);
            _gameSim.IncludeEnemyQueryRadius(radius);
            return radius;
        }
        private void InitializeCrascendoBoss(int index, BossState boss)
        {
            if (!CurrentVoidIsCrascendo) return;
            if (_crascendoBosses == null) ResetCrascendoState();
            _crascendoBosses[index] = new CrascendoGrowthState { Identity = boss.TelemetryInstanceId, BaseRadius = boss.Radius };
            _crascendoBossPush[index] = Vector2.zero;
        }
        private void GrowCrascendoBoss(int index, ref BossState boss, float damage)
        {
            if (!CurrentVoidIsCrascendo || damage <= 0 || float.IsNaN(damage) || float.IsInfinity(damage)) return;
            if (_crascendoBosses == null) ResetCrascendoState();
            ref var state = ref _crascendoBosses[index];
            if (state.Identity != boss.TelemetryInstanceId || state.BaseRadius <= 0) { state = new CrascendoGrowthState { Identity = boss.TelemetryInstanceId, BaseRadius = boss.Radius }; _crascendoBossPush[index] = Vector2.zero; }
            state.Hits = Mathf.Min(CrascendoRules.HitsToMaximum, state.Hits + 1);
            boss.Radius = state.BaseRadius * CrascendoRules.Growth(state.Hits);
        }
        private void CrascendoEnemyDeath(int index, EnemyState enemy)
        {
            if (!CurrentVoidIsCrascendo) return;
            ref var state = ref _crascendoEnemies[index];
            if (state.Identity != enemy.SpawnId || state.BaseRadius <= 0 || enemy.Radius < state.BaseRadius * CrascendoRules.MaximumGrowth - .001f || state.Pulsed) return;
            state.Pulsed = true; CrascendoDeathPulse(enemy.Position, enemy.Radius, index, -1);
        }
        private void CrascendoBossDeath(int index, BossState boss)
        {
            if (!CurrentVoidIsCrascendo || _crascendoBosses == null) return;
            ref var state = ref _crascendoBosses[index];
            if (state.Identity != boss.TelemetryInstanceId || state.Hits < 20 || state.Pulsed) return;
            state.Pulsed = true; CrascendoDeathPulse(boss.Position, boss.Radius, -1, index);
        }
        private void CrascendoDeathPulse(Vector2 origin, float radius, int sourceEnemy, int sourceBoss)
        {
            var reach = radius + 210;
            SpawnRingWave(origin, radius, 210f / .9f, .9f, new Color(.8f, .65f, 1f, .85f));
            for (var i = 0; i < _gameSim.Enemies.Length; i++)
            {
                ref var target = ref _gameSim.Enemies[i];
                if (i == sourceEnemy || !target.Active || target.Health <= 0) continue;
                var delta = target.Position - origin; var distance = delta.magnitude;
                if (distance > reach + target.Radius) continue;
                target.Knockback += (distance > .001f ? delta / distance : Vector2.right) * (480 + 340 * Mathf.Max(0, 1 - distance / reach));
            }
            for (var i = 0; i < _gameSim.Bosses.Length; i++)
            {
                var target = _gameSim.Bosses[i];
                if (i == sourceBoss || !target.Active || target.Health <= 0) continue;
                var delta = target.Position - origin; var distance = delta.magnitude;
                if (distance > reach + target.Radius) continue;
                _crascendoBossPush[i] += (distance > .001f ? delta / distance : Vector2.right) * (480 + 340 * Mathf.Max(0, 1 - distance / reach));
            }
        }
    }
}
