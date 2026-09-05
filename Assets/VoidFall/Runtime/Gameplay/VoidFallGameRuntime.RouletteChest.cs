using UnityEngine;
using VoidFall.Core;
using VoidFall.UI;

namespace VoidFall.Runtime
{
    /// <summary>
    /// The defeated guardian leaves a miniature Void wheel. It emerges after
    /// the death burst, stays at the drop site, and unfolds on physical pickup.
    /// The existing internal chest names are retained for runtime callers.
    /// </summary>
    public sealed partial class VoidFallGameRuntime
    {
        private SpriteRenderer _rouletteChestBody;
        private SpriteRenderer _rouletteChestGlow;
        private bool _rouletteChestActive;
        private float _rouletteChestPulse;
        private float _rouletteChestSparkleTimer;
        private Sprite _rouletteRelicSprite;
        private Vector2 _rouletteRelicPosition;

        private const float RouletteChestCollectRadius = 52f;

        /// <summary>Spawns the chest at the boss's death site.</summary>
        private void SpawnRouletteChest(Vector2 position)
        {
            if (_rouletteChestBody == null)
            {
                _rouletteRelicSprite = RouletteWheelGraphic.CreateRelicSprite();
                _rouletteChestBody = CreateView(
                    "Void Roulette Relic", _rouletteRelicSprite, 34);
                _rouletteChestBody.transform.localScale = Vector3.one * 64f;

                _rouletteChestGlow = CreateView(
                    "Void Relic Glow", UISprites.Glow(128), 33);
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
            _rouletteRelicPosition = position;
            _rouletteChestBody.transform.position = position;
            _rouletteChestGlow.transform.position = position;
            _rouletteChestBody.enabled = true;
            _rouletteChestGlow.enabled = true;
            _rouletteChestBody.color = Color.clear;
            _rouletteChestGlow.color = Color.clear;
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
            var emergence = Mathf.Clamp01((_rouletteChestPulse - 1.1f) / 0.7f);
            var breath = 1f + Mathf.Sin(_rouletteChestPulse * 2.2f) * 0.025f;
            _rouletteChestBody.transform.localScale = Vector3.one * (64f * breath * Mathf.Lerp(0.2f, 1, emergence));
            _rouletteChestBody.color = new Color(1, 1, 1, emergence);
            _rouletteChestBody.transform.position = _rouletteRelicPosition + Vector2.up * (Mathf.Sin(_rouletteChestPulse * 2) * 4f);
            _rouletteChestBody.transform.rotation = Quaternion.Euler(
                0, 0, _rouletteChestPulse * 12f);
            _rouletteChestGlow.transform.localScale = Vector3.one *
                (92f * (1f + Mathf.Sin(_rouletteChestPulse * 3.4f) * 0.16f));
            _rouletteChestGlow.color = new Color(
                1f, 0.82f, 0.25f, emergence * (0.22f + Mathf.Sin(_rouletteChestPulse * 3.4f) * 0.06f));

            _rouletteChestSparkleTimer -= deltaTime;
            if (_rouletteChestSparkleTimer <= 0 && emergence >= 1)
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
            if (!_rouletteChestActive || _rouletteChestBody == null || _rouletteChestPulse < 1.8f ||
                _gameOver || _revivePending || _paused || _routeMapOpen || _levelUpActive ||
                _menuPage != MenuPage.None || _gameSim.Player.Health <= 0 || _ui == null) return;
            var position = (Vector2)_rouletteChestBody.transform.position;
            if (_voidCompletionPending) _openRouteAfterRoulette = true;
            HideRouletteChest();
            SpawnRingWave(position, 30f, 620f, 0.8f, new Color(1f, 0.85f, 0.3f, 0.95f));
            BurstFx(position, new Color(1f, 0.85f, 0.3f), 20, 320, 0.7f, 0.95f);
            _arenaFlash = Mathf.Max(_arenaFlash, 0.22f);
            OpenBossRoulette();
        }

        private void DestroyRouletteRelic()
        {
            if (_rouletteRelicSprite == null) return;
            Destroy(_rouletteRelicSprite.texture);
            Destroy(_rouletteRelicSprite);
            _rouletteRelicSprite = null;
        }
    }
}
