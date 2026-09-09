using System.Collections.Generic;
using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private const int NebulaStrikeMax = 4;
        private const float NebulaStrikeSpeed = 625f; // Half the approved browser wave's 1250 units/s.
        private const float NebulaStrikeLength = 2600f;
        private const float NebulaStrikeHitRadius = 28f;
        private float _nebulaStrikeTimer = 9f;
        private sealed class NebulaStrike
        {
            public bool Active;
            public Vector2 Origin, Direction, Head;
            public float Warning, Traveled, TrailTick;
            public bool PlayerHit;
            public readonly HashSet<int> Enemies = new HashSet<int>();
            public readonly HashSet<int> Bosses = new HashSet<int>();
            public readonly HashSet<int> Meteors = new HashSet<int>();
        }
        private readonly NebulaStrike[] _nebulaStrikes = new NebulaStrike[NebulaStrikeMax];
        private readonly LineRenderer[] _nebulaStrikeLanes = new LineRenderer[NebulaStrikeMax];
        private readonly LineRenderer[] _nebulaStrikeTrails = new LineRenderer[NebulaStrikeMax];
        private readonly SpriteRenderer[] _nebulaStrikeHeads = new SpriteRenderer[NebulaStrikeMax];

        private void ClearNebulaStrikes()
        {
            for (var i = 0; i < NebulaStrikeMax; i++)
            {
                if (_nebulaStrikes[i] != null) _nebulaStrikes[i].Active = false;
                Hide(_nebulaStrikeLanes[i]); Hide(_nebulaStrikeTrails[i]); Hide(_nebulaStrikeHeads[i]);
            }
            _nebulaStrikeTimer = 9f;
        }

        private void UpdateNebulaStrikes(float dt)
        {
            if (_arenaId != ArenaId.RedNebula || !ArenaHasFeature("meteors") || IsArenaFolding(_arenaTransitionState.Phase))
            {
                ClearNebulaStrikes();
                return;
            }
            if (_gameOver || _paused || _revivePending || _levelUpActive) return;
            dt = Mathf.Max(0, dt);
            _nebulaStrikeTimer -= dt;
            if (_nebulaStrikeTimer <= 0)
            {
                TrySpawnNebulaStrike();
                _nebulaStrikeTimer = 17f + (float)_gameSim.Rng.Next() * 4f;
            }
            for (var i = 0; i < NebulaStrikeMax; i++)
            {
                var strike = _nebulaStrikes[i];
                if (strike == null || !strike.Active) continue;
                EnsureNebulaStrikeViews(i);
                if (strike.Warning > 0)
                {
                    strike.Warning = Mathf.Max(0, strike.Warning - dt);
                    var lane = _nebulaStrikeLanes[i];
                    lane.SetPosition(0, strike.Origin);
                    lane.SetPosition(1, strike.Origin + strike.Direction * NebulaStrikeLength);
                    lane.enabled = true;
                    continue;
                }
                Hide(_nebulaStrikeLanes[i]);
                var previous = strike.Head;
                strike.Head += strike.Direction * (NebulaStrikeSpeed * dt);
                strike.Traveled += NebulaStrikeSpeed * dt;
                DamageNebulaStrike(strike, previous, strike.Head);
                var head = _nebulaStrikeHeads[i];
                head.transform.position = strike.Head;
                head.transform.Rotate(0, 0, dt * 140f);
                head.enabled = true;
                var trail = _nebulaStrikeTrails[i];
                trail.SetPosition(0, strike.Head - strike.Direction * 160f);
                trail.SetPosition(1, strike.Head);
                trail.enabled = true;
                strike.TrailTick -= dt;
                if (strike.TrailTick <= 0)
                {
                    strike.TrailTick = .10f;
                    BurstFx(strike.Head, SourceDotColor("orange"), 2, 95, .25f, .5f);
                }
                if (strike.Traveled < NebulaStrikeLength) continue;
                strike.Active = false;
                Hide(head); Hide(trail);
            }
        }

        private static float NebulaSegmentDistance(Vector2 point, Vector2 from, Vector2 to)
        {
            var delta = to - from;
            var t = delta.sqrMagnitude > .00001f ? Mathf.Clamp01(Vector2.Dot(point - from, delta) / delta.sqrMagnitude) : 0;
            return Vector2.Distance(point, from + delta * t);
        }

        private void DamageNebulaStrike(NebulaStrike strike, Vector2 from, Vector2 to)
        {
            var damage = 240f + (float)_time * .3f;
            // Swept checks prevent a fast head skipping targets between simulation frames.
            var snapshot = CaptureEnemyEffectSnapshot(out var count);
            try
            {
                for (var i = 0; i < count; i++)
                {
                    var target = snapshot[i];
                    var enemy = target.State;
                    if (!IsLiveEnemyEffectTarget(target) || NebulaSegmentDistance(enemy.Position, from, to) >= NebulaStrikeHitRadius + enemy.Radius) continue;
                    if (strike.Enemies.Add(EnemyIdentity(enemy, target.Slot))) ApplyEnemyDamage(target.Slot, damage, strike.Direction, 90, false, -1);
                }
            }
            finally { ReleaseEnemyEffectSnapshot(snapshot); }
            for (var i = 0; i < _gameSim.Bosses.Length; i++)
            {
                var boss = _gameSim.Bosses[i];
                if (!boss.Active || boss.State == 4 || NebulaSegmentDistance(boss.Position, from, to) >= NebulaStrikeHitRadius + boss.Radius) continue;
                if (strike.Bosses.Add(GameSim.BossIdentity(boss, i))) ApplyBossDamage(i, damage, -1);
            }
            for (var i = 0; i < _gameSim.Meteors.Length; i++)
            {
                var meteor = _gameSim.Meteors[i];
                if (!meteor.Active || NebulaSegmentDistance(meteor.Position, from, to) >= NebulaStrikeHitRadius + meteor.Radius) continue;
                if (strike.Meteors.Add(meteor.Identity)) DamageMeteor(i, damage);
            }
            if (!strike.PlayerHit && _gameSim.Player.Health > 0 && _gameSim.Player.DyingTimer <= 0 &&
                NebulaSegmentDistance(_gameSim.Player.Position, from, to) < NebulaStrikeHitRadius + PlayerRadius)
            {
                strike.PlayerHit = true;
                DamagePlayer(28f, strike.Direction, "lane-strike");
            }
        }

        private void EnsureNebulaStrikeViews(int i)
        {
            if (_nebulaStrikeHeads[i] != null) return;
            _nebulaStrikeHeads[i] = CreateView("Nebula Wave Meteor " + i, NebulaRockSprite(true), 7);
            _nebulaStrikeHeads[i].transform.localScale = Vector3.one * 76f;
            var lane = _nebulaStrikeLanes[i] = CreateLineView("Nebula Wave Lane " + i, -2);
            lane.positionCount = 2;
            lane.startWidth = lane.endWidth = NebulaStrikeHitRadius * 2f;
            lane.startColor = lane.endColor = new Color(1f, .44f, .25f, .14f);
            var trail = _nebulaStrikeTrails[i] = CreateLineView("Nebula Wave Trail " + i, 6);
            trail.positionCount = 2;
            trail.startWidth = 1f; trail.endWidth = 30f;
            trail.startColor = new Color(1f, .3f, .08f, 0f);
            trail.endColor = new Color(1f, .68f, .3f, .65f);
        }

        private void TrySpawnNebulaStrike()
        {
            foreach (var active in _nebulaStrikes) if (active != null && active.Active) return;
            var axis = (int)(_gameSim.Rng.Next() * 4) % 4;
            var count = _gameSim.Rng.Next() < .5 ? 3 : 4;
            var direction = axis == 0 ? Vector2.right : axis == 1 ? Vector2.left : axis == 2 ? Vector2.down : Vector2.up;
            var normal = new Vector2(-direction.y, direction.x);
            var centre = _gameSim.Player.Position + normal * (((float)_gameSim.Rng.Next() - .5f) * 260f);
            for (var i = 0; i < count; i++)
            {
                var strike = _nebulaStrikes[i] ?? (_nebulaStrikes[i] = new NebulaStrike());
                strike.Active = true;
                strike.Direction = direction;
                strike.Origin = centre - direction * (1100f + i * 90f) + normal * ((i - (count - 1) * .5f) * 72f);
                strike.Head = strike.Origin;
                strike.Warning = 1.4f; strike.Traveled = strike.TrailTick = 0;
                strike.PlayerHit = false;
                strike.Enemies.Clear(); strike.Bosses.Clear(); strike.Meteors.Clear();
            }
            ShowArenaToast("METEOR WAVE — " + (axis == 0 ? "FROM LEFT" : axis == 1 ? "FROM RIGHT" : axis == 2 ? "FROM TOP" : "FROM BELOW"), 1.4f, ToastKind.Danger);
            _audio?.Play(ProceduralAudio.Cue.Warning, .9f);
        }
    }
}
