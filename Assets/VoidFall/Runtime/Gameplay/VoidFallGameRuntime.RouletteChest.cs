using UnityEngine;
using VoidFall.Core;
using VoidFall.UI;

namespace VoidFall.Runtime
{
    /// <summary>
    /// The boss chest (VS-style, spec 43 reworked): the boss's death drops a
    /// shiny gold chest where it fell; walking over it opens the roulette
    /// ceremony. The chest ignores pickup magnetism on purpose - it is a
    /// moment, not loot to be vacuumed - and only one exists at a time.
    /// </summary>
    public sealed partial class VoidFallGameRuntime
    {
        private SpriteRenderer _rouletteChestBody;
        private SpriteRenderer _rouletteChestGlow;
        private bool _rouletteChestActive;
        private float _rouletteChestPulse;
        private float _rouletteChestSparkleTimer;

        private const float RouletteChestCollectRadius = 52f;

        /// <summary>Spawns the chest at the boss's death site.</summary>
        private void SpawnRouletteChest(Vector2 position)
        {
            if (_rouletteChestBody == null)
            {
                _rouletteChestBody = CreateView(
                    "Boss Chest", ProceduralSpriteFactory.Square(), 34);
                _rouletteChestBody.color = new Color(0.98f, 0.76f, 0.18f, 1f);
                _rouletteChestBody.transform.localScale = Vector3.one * 34f;

                _rouletteChestGlow = CreateView(
                    "Boss Chest Glow", ProceduralSpriteFactory.Circle(), 33);
                if (_additiveSpriteMaterial != null)
                    _rouletteChestGlow.material = _additiveSpriteMaterial;
                _rouletteChestGlow.color = new Color(1f, 0.82f, 0.25f, 0.55f);
                _rouletteChestGlow.transform.localScale = Vector3.one * 92f;
            }
            _rouletteChestActive = true;
            _rouletteChestPulse = 0;
            _rouletteChestSparkleTimer = 0;
            position.x = Mathf.Clamp(position.x, -600f, 600f);
            position.y = Mathf.Clamp(position.y, -330f, 330f);
            _rouletteChestBody.transform.position = position;
            _rouletteChestGlow.transform.position = position;
            _rouletteChestBody.enabled = true;
            _rouletteChestGlow.enabled = true;
            SpawnRingWave(position, 22f, 420f, 0.6f, new Color(1f, 0.82f, 0.25f, 0.9f));
        }

        private void HideRouletteChest()
        {
            _rouletteChestActive = false;
            if (_rouletteChestBody != null) _rouletteChestBody.enabled = false;
            if (_rouletteChestGlow != null) _rouletteChestGlow.enabled = false;
        }

        /// <summary>
        /// Per-frame: the chest breathes and sparkles, and the player picks
        /// it up by touching it. Render/flow only - no simulation state.
        /// </summary>
        private void UpdateRouletteChest(float deltaTime)
        {
            if (!_rouletteChestActive || _rouletteChestBody == null) return;

            _rouletteChestPulse += deltaTime;
            var breath = 1f + Mathf.Sin(_rouletteChestPulse * 3.4f) * 0.08f;
            _rouletteChestBody.transform.localScale = Vector3.one * (34f * breath);
            _rouletteChestBody.transform.rotation = Quaternion.Euler(
                0, 0, Mathf.Sin(_rouletteChestPulse * 1.7f) * 6f);
            _rouletteChestGlow.transform.localScale = Vector3.one *
                (92f * (1f + Mathf.Sin(_rouletteChestPulse * 3.4f) * 0.16f));
            _rouletteChestGlow.color = new Color(
                1f, 0.82f, 0.25f, 0.4f + Mathf.Sin(_rouletteChestPulse * 3.4f) * 0.18f);

            _rouletteChestSparkleTimer -= deltaTime;
            if (_rouletteChestSparkleTimer <= 0)
            {
                _rouletteChestSparkleTimer = 0.45f;
                BurstFx(
                    (Vector2)_rouletteChestBody.transform.position +
                        new Vector2(Random.Range(-18f, 18f), Random.Range(-12f, 16f)),
                    new Color(1f, 0.9f, 0.4f), 3, 60, 0.4f, 0.6f);
            }

            if (Vector2.Distance(_gameSim.Player.Position,
                    _rouletteChestBody.transform.position) < RouletteChestCollectRadius)
            {
                CollectRouletteChest();
            }
        }

        private void CollectRouletteChest()
        {
            var position = (Vector2)_rouletteChestBody.transform.position;
            HideRouletteChest();
            SpawnRingWave(position, 30f, 620f, 0.8f, new Color(1f, 0.85f, 0.3f, 0.95f));
            BurstFx(position, new Color(1f, 0.85f, 0.3f), 20, 320, 0.7f, 0.95f);
            _arenaFlash = Mathf.Max(_arenaFlash, 0.22f);
            OpenBossRoulette();
        }
    }
}
