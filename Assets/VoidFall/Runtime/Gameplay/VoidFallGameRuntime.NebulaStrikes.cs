using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    /// <summary>
    /// Red Nebula lane strikes: occasional fast meteors that streak across
    /// the arena on a cardinal axis (left-right / up-down). Telegraph ~1s,
    /// then a heavy head travels ~2400 units hitting EVERYTHING (enemies,
    /// bosses, meteors, player) via the shared DamageArea path. Dodge it or
    /// get hit hard. Deterministic: all rolls use the run Rng stream.
    /// </summary>
    public sealed partial class VoidFallGameRuntime
    {
        private const int NebulaStrikeMax = 2;
        private const float NebulaStrikeTelegraphSeconds = 1.15f;
        private const float NebulaStrikeSpeed = 780f;
        private const float NebulaStrikeLength = 2400f;
        private const float NebulaStrikeHitRadius = 60f;
        private const float NebulaStrikeDamageTick = 0.25f;
        private const float NebulaStrikeFirstDelay = 9f;
        private const float NebulaStrikeMinInterval = 15f;
        private const float NebulaStrikeIntervalSpan = 8f;
        private const float NebulaStrikePlayerDamage = 28f;

        private struct NebulaStrike
        {
            public bool Active;
            public bool Telegraphing;
            public Vector2 Origin;
            public Vector2 Direction;
            public Vector2 Head;
            public float TelegraphRemaining;
            public float TelegraphFxTick;
            public float Traveled;
            public float DamageTick;
            public float TrailFxTick;
        }

        private readonly NebulaStrike[] _nebulaStrikes = new NebulaStrike[NebulaStrikeMax];
        private float _nebulaStrikeTimer = NebulaStrikeFirstDelay;

        private void ClearNebulaStrikes()
        {
            for (var index = 0; index < _nebulaStrikes.Length; index++)
                _nebulaStrikes[index] = default;
            _nebulaStrikeTimer = NebulaStrikeFirstDelay;
        }

        private void UpdateNebulaStrikes(float dt)
        {
            if (!ArenaHasFeature("meteors") || IsArenaFolding(_arenaTransitionState.Phase))
                return;
            if (_gameOver || _paused || _revivePending || _levelUpActive)
                return;

            _nebulaStrikeTimer -= Mathf.Max(0f, dt);
            if (_nebulaStrikeTimer <= 0f)
            {
                TrySpawnNebulaStrike();
                _nebulaStrikeTimer = NebulaStrikeMinInterval +
                    (float)_gameSim.Rng.Next() * NebulaStrikeIntervalSpan;
            }

            for (var index = 0; index < _nebulaStrikes.Length; index++)
            {
                var strike = _nebulaStrikes[index];
                if (!strike.Active) continue;

                if (strike.Telegraphing)
                {
                    strike.TelegraphRemaining -= Mathf.Max(0f, dt);
                    strike.TelegraphFxTick -= Mathf.Max(0f, dt);
                    if (strike.TelegraphFxTick <= 0f)
                    {
                        strike.TelegraphFxTick = 0.3f;
                        var mid = strike.Origin + strike.Direction * (NebulaStrikeLength * 0.5f);
                        SpawnRingWave(strike.Origin, 12f, 220f, 0.3f,
                            new Color(1f, 0.46f, 0.12f, 0.6f));
                        SpawnRingWave(mid, 12f, 220f, 0.3f,
                            new Color(1f, 0.46f, 0.12f, 0.4f));
                    }
                    if (strike.TelegraphRemaining <= 0f)
                    {
                        strike.Telegraphing = false;
                        strike.Head = strike.Origin;
                        strike.Traveled = 0f;
                        strike.DamageTick = 0f;
                        strike.TrailFxTick = 0f;
                        AddCameraShake(0.3f);
                        _audio?.Play(ProceduralAudio.Cue.ExploderBlast, 0.7f);
                    }
                    _nebulaStrikes[index] = strike;
                    continue;
                }

                strike.Head += strike.Direction * (NebulaStrikeSpeed * Mathf.Max(0f, dt));
                strike.Traveled += NebulaStrikeSpeed * Mathf.Max(0f, dt);

                strike.DamageTick -= Mathf.Max(0f, dt);
                if (strike.DamageTick <= 0f)
                {
                    strike.DamageTick = NebulaStrikeDamageTick;
                    var enemyDamage = 240f + (float)_time * 0.3f;
                    DamageArea(strike.Head, NebulaStrikeHitRadius, enemyDamage, -1);
                    if (_gameSim.Player.Health > 0 && !_gameOver && !_revivePending &&
                        _gameSim.Player.DyingTimer <= 0 &&
                        Vector2.Distance(_gameSim.Player.Position, strike.Head) <
                        NebulaStrikeHitRadius + PlayerRadius)
                    {
                        DamagePlayer(NebulaStrikePlayerDamage, strike.Direction);
                    }
                }

                strike.TrailFxTick -= Mathf.Max(0f, dt);
                if (strike.TrailFxTick <= 0f)
                {
                    strike.TrailFxTick = 0.07f;
                    BurstFx(strike.Head, SourceDotColor("orange"), 4, 260, 0.35f, 0.6f);
                    BurstFx(strike.Head, SourceDotColor("yellow"), 2, 180, 0.25f, 0.5f);
                }

                if (strike.Traveled >= NebulaStrikeLength)
                    strike.Active = false;
                _nebulaStrikes[index] = strike;
            }
        }

        private void TrySpawnNebulaStrike()
        {
            var slot = -1;
            for (var index = 0; index < _nebulaStrikes.Length; index++)
            {
                if (_nebulaStrikes[index].Active) continue;
                slot = index;
                break;
            }
            if (slot < 0) return;

            var player = _gameSim.Player.Position;
            var laneOffset = ((float)_gameSim.Rng.Next() * 2f - 1f) * 420f;
            var axis = (int)(_gameSim.Rng.Next() * 4) % 4;
            Vector2 origin;
            Vector2 direction;
            switch (axis)
            {
                case 0:
                    origin = player + new Vector2(-1100f, laneOffset);
                    direction = Vector2.right;
                    break;
                case 1:
                    origin = player + new Vector2(1100f, laneOffset);
                    direction = Vector2.left;
                    break;
                case 2:
                    origin = player + new Vector2(laneOffset, 800f);
                    direction = Vector2.down;
                    break;
                default:
                    origin = player + new Vector2(laneOffset, -800f);
                    direction = Vector2.up;
                    break;
            }

            _nebulaStrikes[slot] = new NebulaStrike
            {
                Active = true,
                Telegraphing = true,
                Origin = origin,
                Direction = direction,
                Head = origin,
                TelegraphRemaining = NebulaStrikeTelegraphSeconds,
                TelegraphFxTick = 0f,
                Traveled = 0f,
                DamageTick = 0f,
                TrailFxTick = 0f,
            };
            ShowArenaToast("METEOR STORM INCOMING", 1.6f, ToastKind.Danger);
            _audio?.Play(ProceduralAudio.Cue.Warning, 0.9f);
        }
    }
}
