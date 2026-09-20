using UnityEngine;
using VoidFall.Core;
using VoidFall.UI;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private readonly SpriteRenderer[] _hydraSurvivalSurfaces = new SpriteRenderer[9];
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
        private Vector2 GameplayCameraCentre()
        {
            if (_mainMenuBrowsing) return _arenaId == ArenaId.NullCity ? _nullCityOrigin : _cameraFollowPosition;
            Vector2 centre, size;
            if (_arenaId == ArenaId.NullCity)
            {
                centre = _nullCityOrigin;
                size = new Vector2(NullCityRules.WorldWidth, NullCityRules.WorldHeight);
            }
            else if (_arenaId == ArenaId.MonochromeCourt && _courtFieldReady)
            {
                size = new Vector2(CourtBoardColumns, CourtBoardRows) * (float)MonochromeEncounterRules.TileSize;
                centre = _monochromeBoardOrigin + size * .5f;
            }
            else return _cameraFollowPosition;
            // A finite map smaller than the viewport stays centred. Larger maps
            // retain the shared smooth follow until the viewport reaches an edge.
            var travel = Vector2.Max(Vector2.zero, size * .5f - GameplayViewportHalfExtent());
            return new Vector2(
                Mathf.Clamp(_cameraFollowPosition.x, centre.x - travel.x, centre.x + travel.x),
                Mathf.Clamp(_cameraFollowPosition.y, centre.y - travel.y, centre.y + travel.y));
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
            var sprite = ApprovedMapSprite("hydra-base");
            if (sprite == null) return true;
            var centre = RenderCameraCentre();
            var tileX = Mathf.FloorToInt(centre.x / 1600f);
            var tileY = Mathf.FloorToInt(centre.y / 900f);
            for (var i = 0; i < _hydraSurvivalSurfaces.Length; i++)
            {
                if (_hydraSurvivalSurfaces[i] == null) _hydraSurvivalSurfaces[i] = CreateView("Hydra stationary ground " + i, sprite, -130);
                var view = _hydraSurvivalSurfaces[i];
                view.sprite = sprite; view.color = Color.white; view.enabled = true;
                view.transform.position = new Vector2((tileX + i % 3 - 1 + .5f) * 1600f, (tileY + i / 3 - 1 + .5f) * 900f);
                view.transform.localScale = new Vector3(1600f / sprite.bounds.size.x, 900f / sprite.bounds.size.y, 1);
            }
            return true;
        }
    }
}
