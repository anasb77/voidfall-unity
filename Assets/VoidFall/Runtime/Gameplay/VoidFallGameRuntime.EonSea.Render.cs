using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private readonly SpriteRenderer[] _eonGroundViews = new SpriteRenderer[25];
        private readonly SpriteRenderer[] _eonIceViews = new SpriteRenderer[EonSeaTerrain.MaxIce];
        private readonly SpriteRenderer[] _eonSlipViews = new SpriteRenderer[EonSeaTerrain.MaxPatches];
        private readonly LineRenderer[] _eonWarningViews = new LineRenderer[EonSeaTerrain.MaxIce];
        private readonly LineRenderer[] _eonPulseViews = new LineRenderer[EonSeaTerrain.MaxIce];
        private readonly LineRenderer[] _eonSnowViews = new LineRenderer[40];
        private LineRenderer _eonPlayerFrostView;
        private EonSeaTerrain _eonPreviewTerrain;
        private bool _eonPresentationVisible;

        private EonSeaVisualAsset EonSeaVisuals => _preparedArenaPlateAssets[(int)ArenaId.EonSea] != null
            ? _preparedArenaPlateAssets[(int)ArenaId.EonSea].EonSeaVisuals : null;

        private void HideEonSeaPresentation()
        {
            for (var i = 0; i < _eonGroundViews.Length; i++) ClearNullCityProp(_eonGroundViews[i]);
            for (var i = 0; i < _eonIceViews.Length; i++) ClearNullCityProp(_eonIceViews[i]);
            for (var i = 0; i < _eonSlipViews.Length; i++) ClearNullCityProp(_eonSlipViews[i]);
            HideNullCityViews(_eonWarningViews); HideNullCityViews(_eonPulseViews); HideNullCityViews(_eonSnowViews);
            if (_eonPlayerFrostView != null) _eonPlayerFrostView.enabled = false;
            _eonPresentationVisible = false;
        }

        private void SyncEonSeaPresentation()
        {
            if (_arenaId != ArenaId.EonSea || (!_mainMenuBrowsing && (_journeyStage == JourneyStage.Junction || _journeyStage == JourneyStage.Travel)))
            { if (_eonPresentationVisible) HideEonSeaPresentation(); return; }
            EnsureArenaPlate(ArenaId.EonSea); var visuals = EonSeaVisuals; if (visuals == null || !visuals.IsValid) return;
            HideLegacyCityDecor(); if (_backdropView != null) _backdropView.enabled = false;
            if (_arenaBakedDetailView != null) _arenaBakedDetailView.enabled = false;
            _eonPresentationVisible = true;
            EonSeaTerrain terrain;
            if (_mainMenuBrowsing)
            {
                if (_eonPreviewTerrain == null) { _eonPreviewTerrain = new EonSeaTerrain(831); _eonPreviewTerrain.Stream(0, 0); }
                terrain = _eonPreviewTerrain;
            }
            else { EnsureEonSeaTerrain(); terrain = _eonSeaTerrain; }
            var centre = RenderCameraCentre(); var clock = _mainMenuBrowsing ? _ambientClock : terrain.Time;
            var cycle = Mathf.Repeat(clock, 69); var snow = cycle >= 25 && cycle < 49;
            var tint = snow ? new Color(.82f, .88f, 1f, 1f) : Color.white;
            var tx = Mathf.FloorToInt(centre.x / 1024); var ty = Mathf.FloorToInt(centre.y / 1024); var slot = 0;
            for (var y = ty - 2; y <= ty + 2; y++) for (var x = tx - 2; x <= tx + 2; x++)
            {
                var sprite = visuals.Ground((x * 13 + y * 7) & 3);
                EonSprite(ref _eonGroundViews[slot++], "Eon Sea Ice Sheet", sprite, new Vector2(x * 1024 + 512, y * 1024 + 512), new Vector2(1024.4f / sprite.bounds.size.x, 1024.4f / sprite.bounds.size.y), 0, tint, -100);
            }
            for (var i = 0; i < _eonSlipViews.Length; i++)
            {
                if (i >= terrain.Patches.Count) { ClearNullCityProp(_eonSlipViews[i]); continue; }
                var p = terrain.Patches[i]; var sprite = visuals.Slippery;
                EonSprite(ref _eonSlipViews[i], "Eon Sea Slippery Ice", sprite, new Vector2(p.X, p.Y), new Vector2(p.RadiusX * 2 / sprite.bounds.size.x, p.RadiusY * 2 / sprite.bounds.size.y), p.Angle * Mathf.Rad2Deg, Color.white, -95);
            }
            for (var i = 0; i < _eonIceViews.Length; i++)
            {
                if (i >= terrain.Ice.Count || terrain.Ice[i].Broken) { ClearNullCityProp(_eonIceViews[i]); if (_eonWarningViews[i] != null) _eonWarningViews[i].enabled = false; continue; }
                var ice = terrain.Ice[i]; var width = ice.Length + ice.Radius * 2; var sy = ice.Radius / (ice.Kind == EonSeaIceKind.Wall ? 31f : ice.Kind == EonSeaIceKind.Mass ? 65f : 50f) * (1f - ice.Melt * .46f);
                var angle = ice.Angle * Mathf.Rad2Deg; var origin = new Vector2(ice.X, ice.Y) + (Vector2)(Quaternion.Euler(0, 0, angle) * new Vector3(0, 17 * sy, 0));
                var color = Color.Lerp(tint, new Color(.68f, .89f, 1f), ice.Melt * .35f);
                if (ice.Flash > 0) color = Color.white;
                EonSprite(ref _eonIceViews[i], "Eon Sea Glacier", visuals.Ice((int)ice.Kind, ice.Variant), origin, new Vector2(width / 280f, sy), angle, color, -10);
                if (ice.Warning) NullCityCircle(ref _eonWarningViews[i], "Eon Sea Freeze Warning", new Vector2(ice.X, ice.Y), 140 + ice.Radius, new Color(.64f, .84f, 1f, .28f));
                else if (_eonWarningViews[i] != null) _eonWarningViews[i].enabled = false;
            }
            for (var i = 0; i < _eonPulseViews.Length; i++)
            {
                if (i >= terrain.Pulses.Count) { if (_eonPulseViews[i] != null) _eonPulseViews[i].enabled = false; continue; }
                var p = terrain.Pulses[i]; var progress = 1 - p.Life / EonSeaPulse.Duration;
                NullCityCircle(ref _eonPulseViews[i], "Eon Sea Freezing Pulse", new Vector2(p.X, p.Y), p.Radius * Mathf.Min(1, progress * 2), new Color(.7f, .93f, 1f, 1 - progress));
            }
            if (!_mainMenuBrowsing && _eonSeaPlayerFreeze.Scale(1, terrain.Time) < 1)
                NullCityCircle(ref _eonPlayerFrostView, "Eon Sea Player Frost", _gameSim.Player.Position, PlayerRadius + 10, new Color(.7f, .9f, 1f, .85f));
            else if (_eonPlayerFrostView != null) _eonPlayerFrostView.enabled = false;
            var half = RenderViewportHalfExtent(); var reduced = _saveData?.settings != null && _saveData.settings.reducedMotion;
            for (var i = 0; i < _eonSnowViews.Length; i++)
            {
                if (reduced || !snow && i >= 15) { if (_eonSnowViews[i] != null) _eonSnowViews[i].enabled = false; continue; }
                var point = centre + new Vector2(Mathf.Repeat(i * 137.8f + clock * (snow ? 48 : 12), half.x * 2) - half.x, Mathf.Repeat(i * 89.7f - clock * 8, half.y * 2) - half.y);
                NullCityLine(ref _eonSnowViews[i], "Eon Sea Snow", point, point + new Vector2(snow ? 9 : 3, 1), .8f, new Color(.8f, .91f, 1f, .18f), 25);
            }
        }

        private void EonSprite(ref SpriteRenderer view, string name, Sprite sprite, Vector2 position, Vector2 scale, float rotation, Color color, int order)
        {
            if (view == null) view = CreateView(name, sprite, order);
            view.sprite = sprite; view.transform.position = position; view.transform.localScale = new Vector3(scale.x, scale.y, 1);
            view.transform.rotation = Quaternion.Euler(0, 0, rotation); view.color = color; view.enabled = sprite != null;
        }
    }
}
