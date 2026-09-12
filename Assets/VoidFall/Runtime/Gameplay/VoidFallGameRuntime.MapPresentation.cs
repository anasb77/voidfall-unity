using UnityEngine;
using VoidFall.Core;
using VoidFall.UI;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private readonly SpriteRenderer[] _hydraSurvivalSurfaces = new SpriteRenderer[3];
        private Vector2 ApprovedMapSizeWorld()
        {
            if (CurrentVoidIsNullCity) return new Vector2(
                (float)(NullCityRules.ArenaRight - NullCityRules.ArenaLeft) * NullCityRules.WorldScale,
                (float)(NullCityRules.ArenaBottom - NullCityRules.ArenaTop) * NullCityRules.WorldScale);
            if (_arenaId == ArenaId.MonochromeCourt && _courtFieldReady)
                return new Vector2(CourtBoardColumns, CourtBoardRows) * (float)MonochromeEncounterRules.TileSize;
            if (_hydraBossEncounterActive)
                return new Vector2(HydraRuntimeRules.RibCageHalfWidth * 2, HydraRuntimeRules.RibCageHalfHeight * 2);
            return Vector2.zero;
        }
        private void ApplyApprovedMapVideoEffects()
        {
            if (_videoChromatic != null)
                _videoChromatic.intensity.value = _arenaId == ArenaId.MonochromeCourt && !_mainMenuBrowsing
                    ? 0f : VideoSettingsRules.EffectiveChromatic(_saveData?.settings?.chromatic ?? -1f);
        }
        private float ApprovedMapEnemyRecycleDistance()
        {
            if (_arenaId != ArenaId.MonochromeCourt && !CurrentVoidIsNullCity && !HydraSurvivalPresentationActive) return 1750f;
            return Mathf.Max(1750f, GameplayViewportHalfExtent().magnitude + 450f);
        }
        private void ConstrainCourtEnemy(ref EnemyState enemy)
        {
            if (_arenaId != ArenaId.MonochromeCourt || !_courtFieldReady) return;
            enemy.Position = MonochromeRuntimeRules.ClampToBoard(enemy.Position, _monochromeBoardOrigin,
                new Vector2(CourtBoardColumns, CourtBoardRows) * (float)MonochromeEncounterRules.TileSize, enemy.Radius);
        }
        private float ApprovedMapPlayerVisualScale()
        {
            if (_mainMenuBrowsing) return 1f;
            if (CurrentVoidIsNullCity) return NullCityRules.PlayerRenderMultiplier;
            if (_arenaId == ArenaId.MonochromeCourt && _camera != null)
                return Mathf.Max(1f, 10f * _camera.orthographicSize * 2f /
                    Mathf.Max(1f, Screen.height) / Mathf.Max(1f, PlayerRadius));
            return 1f;
        }
        private void ApplyApprovedMapPlayerPresentation()
        {
            var scale = ApprovedMapPlayerVisualScale();
            if (_playerView != null) _playerView.transform.localScale = Vector3.one * ProceduralSpriteFactory.OperativeCanvasSize() * scale;
            if (_playerAuraView != null) _playerAuraView.transform.localScale *= scale;
            if (_playerRingView != null) _playerRingView.transform.localScale *= scale;
            ScaleMapCosmetics(_playerCosmeticViews, scale);
            ScaleMapCosmetics(_playerTrailViews, scale);
        }
        private void ScaleMapCosmetics(SpriteRenderer[] views, float scale)
        {
            if (views == null || Mathf.Approximately(scale, 1)) return;
            foreach (var view in views)
            {
                if (view == null || !view.enabled) continue;
                view.transform.position = (Vector3)_gameSim.Player.Position + (view.transform.position - (Vector3)_gameSim.Player.Position) * scale;
                view.transform.localScale *= scale;
            }
        }
        private bool RenderApprovedMapSurface()
        {
            var hydraI = HydraSurvivalPresentationActive && !_mainMenuBrowsing;
            if ((hydraI || _arenaId == ArenaId.MonochromeCourt) && _nullCityPresentationVisible)
                HideNullCityPresentation();
            if (!hydraI) foreach (var view in _hydraSurvivalSurfaces)
                if (view != null) { view.enabled = false; view.sprite = null; }
            if (_arenaId == ArenaId.MonochromeCourt && !_mainMenuBrowsing)
            {
                HideLegacyCityDecor(); Hide(_backdropView); Hide(_arenaBakedDetailView);
                UpdateGameplayCameraViewport();
                return true;
            }
            if (!hydraI) return false; // Hydra II retains the original renderer.
            HideLegacyCityDecor(); Hide(_backdropView); Hide(_arenaBakedDetailView);
            UpdateGameplayCameraViewport();
            var sprite = _arenaPlateSprites[(int)ArenaId.Hydra];
            if (sprite == null) return true;
            var half = GameplayViewportHalfExtent();
            var height = half.y * 2f;
            var centre = RenderCameraCentre();
            var reduced = _saveData?.settings != null && _saveData.settings.reducedMotion;
            var offset = reduced ? 0 : -Mathf.Repeat(-HydraSurvivalGlyphOffset, height);
            for (var i = 0; i < _hydraSurvivalSurfaces.Length; i++)
            {
                if (_hydraSurvivalSurfaces[i] == null) _hydraSurvivalSurfaces[i] = CreateView("Hydra I Original Surface " + i, sprite, -130);
                var view = _hydraSurvivalSurfaces[i];
                view.sprite = sprite; view.color = Color.white; view.enabled = true;
                view.transform.position = centre + new Vector2(0, offset + (i - 1) * height);
                view.transform.localScale = new Vector3(half.x * 2f / sprite.bounds.size.x, height / sprite.bounds.size.y, 1);
            }
            return true;
        }
    }
}
