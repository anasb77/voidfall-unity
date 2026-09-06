using System;
using UnityEngine;
using VoidFall.Core;
namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        [Serializable] private sealed class CrascendoTear { public float x, y, length, phase; }
        [Serializable] private sealed class CrascendoTearSet { public CrascendoTear[] tears; }
        private readonly SpriteRenderer[] _crascendoGround = new SpriteRenderer[52];
        private readonly LineRenderer[] _crascendoTears = new LineRenderer[320];
        private CrascendoTearSet _crascendoTearData;
        private bool _crascendoVisible;
        private void HideCrascendoPresentation()
        {
            foreach (var view in _crascendoGround) ClearNullCityProp(view);
            HideNullCityViews(_crascendoTears); _crascendoVisible = false; _crascendoTearData = null;
        }
        private void SyncCrascendoPresentation()
        {
            if (!CurrentVoidIsCrascendo || (!_mainMenuBrowsing && (_journeyStage == JourneyStage.Junction || _journeyStage == JourneyStage.Travel)))
            { if (_crascendoVisible) HideCrascendoPresentation(); return; }
            EnsureArenaPlate(ArenaId.Crascendo);
            var plate = _preparedArenaPlateAssets[(int)ArenaId.Crascendo];
            var visuals = plate != null ? plate.CrascendoVisuals : null;
            if (visuals == null || !visuals.IsValid) return;
            HideLegacyCityDecor(); if (_backdropView != null) _backdropView.enabled = false;
            if (_arenaBakedDetailView != null) _arenaBakedDetailView.enabled = false;
            _crascendoVisible = true;
            if (_crascendoTearData == null) _crascendoTearData = JsonUtility.FromJson<CrascendoTearSet>(visuals.Tears);
            var intensity = CrascendoRules.Intensity(_mainMenuBrowsing ? 0 : _crascendoElapsed, !_mainMenuBrowsing && (ActiveBosses() > 0 || _journeyStage == JourneyStage.Rewards));
            var stage = intensity < .5f ? 0 : 1; var mix = CrascendoRules.Blend(intensity);
            var centre = RenderCameraCentre(); var tx = Mathf.FloorToInt(centre.x / 1024); var ty = Mathf.FloorToInt(centre.y / 1024); var slot = 0;
            for (var y = ty - 2; y <= ty + 2; y++) for (var x = tx - 2; x <= tx + 2; x++)
                for (var layer = 0; layer < 2; layer++)
                {
                    var sprite = visuals.Ground(stage + layer);
                    EonSprite(ref _crascendoGround[slot++], "Crascendo Obsidian", sprite, new Vector2(x * 1024 + 512, y * 1024 + 512), new Vector2(1024.4f / sprite.bounds.size.x, 1024.4f / sprite.bounds.size.y), 0, new Color(1, 1, 1, layer == 0 ? 1 : mix), -100 + layer);
                }
            var viewport = RenderViewportHalfExtent();
            for (var layer = 0; layer < 2; layer++)
            {
                var sprite = visuals.Wash(stage + layer);
                EonSprite(ref _crascendoGround[50 + layer], "Crascendo Atmosphere", sprite, centre, new Vector2(viewport.x * 2 / sprite.bounds.size.x, viewport.y * 2 / sprite.bounds.size.y), 0, new Color(1, 1, 1, layer == 0 ? 1 - mix : mix), -80 + layer);
            }
            var amber = new Color(.87f, .725f, .47f); var coral = new Color(.92f, .58f, .495f); var violet = new Color(.757f, .486f, .945f);
            var color = Color.Lerp(stage == 0 ? amber : coral, stage == 0 ? coral : violet, mix);
            var half = RenderViewportHalfExtent(); var clock = _mainMenuBrowsing ? _ambientClock : _crascendoElapsed;
            var reduced = _saveData?.settings != null && _saveData.settings.reducedMotion;
            slot = 0;
            for (var y = ty - 2; y <= ty + 2; y++) for (var x = tx - 2; x <= tx + 2; x++)
                foreach (var tear in _crascendoTearData.tears)
                {
                    var phase = Mathf.Repeat((reduced ? 0 : clock) * .19f + tear.phase, 1); if (phase > .62f) continue;
                    var point = new Vector2(x * 1024 + tear.x, y * 1024 + 1024 - tear.y - phase * tear.length);
                    if (Mathf.Abs(point.x - centre.x) > half.x + 20 || Mathf.Abs(point.y - centre.y) > half.y + 20 || slot >= _crascendoTears.Length) continue;
                    color.a = Mathf.Sin(phase / .62f * Mathf.PI) * .65f;
                    NullCityLine(ref _crascendoTears[slot++], "Crascendo Crying Tear", point + Vector2.up * 6, point, 1.6f, color, -90);
                }
            while (slot < _crascendoTears.Length) { if (_crascendoTears[slot] != null) _crascendoTears[slot].enabled = false; slot++; }
        }
    }
}
