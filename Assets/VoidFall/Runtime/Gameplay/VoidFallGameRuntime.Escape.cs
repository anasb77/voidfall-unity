using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VoidFall.Core;
using VoidFall.UI;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private const float EscapeDurationSeconds = 10f;
        private const float EscapeEnemyClearSeconds = 5f;
        private const float EscapeLootSweepSeconds = 6f;
        private readonly int[] _escapeEnemySlots = new int[MaxEnemies];
        private readonly int[] _escapeEnemySpawnIds = new int[MaxEnemies];
        private readonly Text[] _escapeDots = new Text[3];
        private int _escapeEnemyCount, _escapeEnemyCursor, _escapeShakePattern;
        private readonly bool[] _junctionPlannedPortals = new bool[2];
        private float EscapeElapsed => Mathf.Clamp(EscapeDurationSeconds - _voidCompletionDelayRemaining, 0f, EscapeDurationSeconds);

        private void BeginEscapeEnemyRetirement()
        {
            _escapeEnemyCount = _escapeEnemyCursor = 0;
            _escapeShakePattern = (int)((_runSeed % 3u + (uint)Mathf.Max(0, _completedVoids - 1)) % 3u);
            for (var order = 0; order < _gameSim.EnemyOrderCount; order++)
            {
                var slot = _gameSim.EnemyOrder[order];
                if (_gameSim.Enemies[slot].Active) _escapeEnemySlots[_escapeEnemyCount++] = slot;
            }
            var origin = _rouletteChestActive ? _rouletteRelicPosition : _gameSim.Player.Position;
            Array.Sort(_escapeEnemySlots, 0, _escapeEnemyCount, Comparer<int>.Create((a, b) =>
            {
                var distance = (_gameSim.Enemies[a].Position - origin).sqrMagnitude.CompareTo(
                    (_gameSim.Enemies[b].Position - origin).sqrMagnitude);
                return distance != 0 ? distance : a.CompareTo(b);
            }));
            for (var index = 0; index < _escapeEnemyCount; index++)
            {
                var slot = _escapeEnemySlots[index];
                _escapeEnemySpawnIds[index] = _gameSim.Enemies[slot].SpawnId;
                // Attack geometry disappears immediately; the enemy body remains until its burst.
                Hide(_enemyTelegraphRingViews[slot]); Hide(_enemyTelegraphLineViews[slot]);
                Hide(_enemyTelegraphSecondaryLineViews[slot]); Hide(_enemyTelegraphTertiaryLineViews[slot]);
                Hide(_enemyTelegraphFillRenderers[slot]); Hide(_enemyTelegraphArrowFillRenderers[slot]);
                Hide(_enemyExploderWarningViews[slot]); Hide(_enemyTelegraphExploderFillViews[slot]);
                Hide(_eliteChargeLaneViews[slot]); Hide(_eliteChargeArrowViews[slot]);
                Hide(_eliteChargeFillRenderers[slot]); Hide(_eliteChargeArrowFillRenderers[slot]);
            }
        }

        private void StepEscapeEnemyRetirement()
        {
            var fraction = Mathf.Clamp01((EscapeElapsed - 0.25f) / (EscapeEnemyClearSeconds - 0.25f));
            // Accelerating retirement makes the collapse build without changing combat's order/RNG.
            var target = EscapeElapsed >= EscapeEnemyClearSeconds ? _escapeEnemyCount
                : Mathf.FloorToInt(_escapeEnemyCount * fraction * fraction);
            while (_escapeEnemyCursor < target)
            {
                var cursor = _escapeEnemyCursor++;
                var slot = _escapeEnemySlots[cursor];
                var enemy = _gameSim.Enemies[slot];
                if (!enemy.Active || enemy.SpawnId != _escapeEnemySpawnIds[cursor]) continue;
                KillEnemy(slot);
                // Harvester-held gems were already earned before the clear.
                // Other enemy families may reuse StoredXp as non-currency state.
                if (enemy.Id == "harvester" && enemy.StoredXp > 0f)
                    SpawnPickup(enemy.Position, enemy.StoredXp);
                SpawnRingWave(enemy.Position, enemy.Radius, enemy.Radius + 95f, 0.45f,
                    new Color(0.6f, 0.9f, 1f, 0.65f));
            }
        }

        private void RecoverEscapeLoot(float dt, bool final)
        {
            if (!final && EscapeElapsed < EscapeLootSweepSeconds) return;
            for (var order = 0; order < _gameSim.PickupOrderCount; order++)
            {
                var slot = _gameSim.PickupOrder[order];
                var pickup = _gameSim.Pickups[slot];
                if (!pickup.Active || (pickup.Kind != PickupKind.Xp && pickup.Kind != PickupKind.Part)) continue;
                var distance = Vector2.Distance(pickup.Position, _gameSim.Player.Position);
                pickup.Position = final ? _gameSim.Player.Position : Vector2.MoveTowards(
                    pickup.Position, _gameSim.Player.Position, (650f + distance * 3f) * Mathf.Max(0, dt));
                pickup.Velocity = Vector2.zero;
                // This is the outgoing arena's reward settlement, not a Magnet power-up.
                // The existing callback still applies Greed/Scholar and frees every slot once.
                _gameSim.Pickups[slot] = pickup;
            }
            UpdatePickups(0f);
        }

        private Vector2 EscapeCameraShakeOffset()
        {
            if (_paused || _routeMapOpen || _rouletteActive || _prizeRevealActive || _levelUpActive ||
                _saveData?.settings == null || _saveData.settings.reducedMotion) return Vector2.zero;
            var t = EscapeElapsed;
            var progress = t / EscapeDurationSeconds;
            var amplitude = (0.7f + 10.3f * progress * progress) * Mathf.Clamp01(_saveData.settings.shake);
            float x, y;
            switch (_escapeShakePattern)
            {
                case 0: // Fracture: alternating lateral jolts.
                    x = Mathf.Sin(t * 29f) * 0.7f + Mathf.Sin(t * 47f) * 0.3f;
                    y = Mathf.Sin(t * 37f + 0.7f) * 0.5f;
                    break;
                case 1: // Tremor: a rough vertical shudder.
                    x = Mathf.Sin(t * 41f + 1.1f) * 0.45f;
                    y = Mathf.Sin(t * 26f) * 0.65f + Mathf.Sin(t * 59f) * 0.35f;
                    break;
                default: // Undertow: a rolling drift with fine vibration.
                    x = Mathf.Sin(t * 12f) * 0.6f + Mathf.Sin(t * 53f) * 0.4f;
                    y = Mathf.Cos(t * 15f) * 0.6f + Mathf.Sin(t * 43f) * 0.4f;
                    break;
            }
            return new Vector2(x, y) * amplitude;
        }

        private void RefreshEscapeDots(bool visible)
        {
            if (visible && _escapeDots[0] == null)
            {
                for (var index = 0; index < _escapeDots.Length; index++)
                {
                    var dot = UIBuilder.CreateText(_canvas.transform, "Escape Dot " + index, ".",
                        25f, UITheme.CyanPale, TextAnchor.MiddleCenter, true, FontStyle.Bold);
                    dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                    dot.rectTransform.pivot = new Vector2(0.5f, 1f);
                    dot.rectTransform.sizeDelta = new Vector2(12f, 60f);
                    _escapeDots[index] = dot;
                }
            }
            for (var index = 0; index < _escapeDots.Length; index++)
            {
                var dot = _escapeDots[index];
                if (dot == null) continue;
                dot.gameObject.SetActive(visible);
                if (!visible) continue;
                var bounce = _saveData?.settings?.reducedMotion == true ? 0f :
                    Mathf.Max(0f, Mathf.Sin(EscapeElapsed * 7f - index * 0.85f)) * 5f;
                dot.rectTransform.anchoredPosition = new Vector2(
                    _escapeStatusText.preferredWidth * 0.5f - 20f + 7f + index * 11f, -100f + bounce);
            }
        }

        private Color PortalDestinationColor(string nodeId)
        {
            var arena = FindArena(ArenaIdName(ArenaIdForRouteNode(nodeId)));
            return ParseColor(arena?.StarTint, UITheme.Cyan);
        }

        private void RefreshJunctionPlan()
        {
            Array.Clear(_junctionPlannedPortals, 0, _junctionPlannedPortals.Length);
            if (_voidRoute == null || string.IsNullOrEmpty(_plannedRouteId)) return;
            var path = _voidRoute.PlannedPathThrough(_plannedRouteId);
            if (path.Count < 2) return;
            for (var index = 0; index < _junctionDestinations.Length; index++)
                _junctionPlannedPortals[index] = _junctionDestinations[index] == path[1];
        }
    }
}
