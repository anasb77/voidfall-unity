using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private SpriteRenderer _nullCityTransitView;
        private SpriteRenderer _nullCityHangarView;
        private SpriteRenderer _nullCityLcdView;
        private readonly SpriteRenderer[] _nullCityTrafficViews = new SpriteRenderer[8];
        private LineRenderer _nullCityPurgeFill;
        private LineRenderer _nullCityPurgeBorder;
        private LineRenderer _nullCityPurgeBeam;
        private readonly LineRenderer[] _nullCityPurgeStripes = new LineRenderer[64];
        private readonly LineRenderer[] _nullCityEnemyWarnings = new LineRenderer[MaxEnemies];
        private readonly LineRenderer[] _nullCityBombWarnings = new LineRenderer[3];
        private LineRenderer _nullCityTractorFill;
        private readonly LineRenderer[] _nullCityTractorEdges = new LineRenderer[2];
        private readonly LineRenderer[] _nullCityTractorFlow = new LineRenderer[10];
        private readonly LineRenderer[] _nullCityCannonWarnings = new LineRenderer[8];
        private readonly LineRenderer[] _nullCityRain = new LineRenderer[36];
        private readonly LineRenderer[] _nullCitySearchlights = new LineRenderer[2];
        private float _nullCityDarkness;
        private bool _nullCityPresentationVisible;

        private NullCityVisualAsset NullCityVisuals
        {
            get
            {
                var index = (int)ArenaId.NullCity;
                return index < _preparedArenaPlateAssets.Length && _preparedArenaPlateAssets[index] != null
                    ? _preparedArenaPlateAssets[index].NullCityVisuals : null;
            }
        }

        private Sprite NullCityUnitSprite(string id, float elapsed, bool hit = false)
        {
            var visuals = NullCityVisuals;
            if (visuals == null) return ProceduralSpriteFactory.Enemy("chaser");
            if (id == "null-marshal" && elapsed % 6f < 3f && !hit) return visuals.MarshalBracedSprite(elapsed);
            if (IsMotherload(id) && _nullCityMove == MotherloadMove.Tractor && _nullCityWarnClock > 0f)
                return visuals.MotherloadTractorWarningSprite(elapsed);
            return visuals.UnitSprite(id, elapsed, hit && (!IsMotherload(id) || _nullCityVentClock <= 0f && _nullCityTractorClock <= 0f),
                IsMotherload(id) && _nullCityVentClock > 0f,
                IsMotherload(id) && _nullCityTractorClock > 0f);
        }

        private float NullCityUnitScale(string id, Sprite sprite)
        {
            if (sprite == null) return 1f;
            var visuals = NullCityVisuals;
            var size = visuals != null ? visuals.UnitWorldSize(id).x : (float)(NullCityContent.FindEnemy(id)?.Radius ?? 114) * 4f;
            return size / Mathf.Max(.01f, sprite.bounds.size.x);
        }

        private static void HideNullCityViews(Renderer[] views)
        {
            for (var i = 0; i < views.Length; i++) if (views[i] != null) views[i].enabled = false;
        }

        private void HideNullCityCombatTelegraphs()
        {
            if (_nullCityPurgeFill != null) _nullCityPurgeFill.enabled = false;
            if (_nullCityPurgeBorder != null) _nullCityPurgeBorder.enabled = false;
            if (_nullCityPurgeBeam != null) _nullCityPurgeBeam.enabled = false;
            if (_nullCityTractorFill != null) _nullCityTractorFill.enabled = false;
            HideNullCityViews(_nullCityPurgeStripes);
            HideNullCityViews(_nullCityEnemyWarnings);
            HideNullCityViews(_nullCityBombWarnings);
            HideNullCityViews(_nullCityTractorEdges);
            HideNullCityViews(_nullCityTractorFlow);
            HideNullCityViews(_nullCityCannonWarnings);
        }

        private void HideNullCityPresentation()
        {
            HideNullCityCombatTelegraphs();
            ClearNullCityProp(_nullCityTransitView);
            ClearNullCityProp(_nullCityHangarView);
            ClearNullCityProp(_nullCityLcdView);
            for (var i = 0; i < _nullCityTrafficViews.Length; i++) ClearNullCityProp(_nullCityTrafficViews[i]);
            HideNullCityViews(_nullCityRain);
            HideNullCityViews(_nullCitySearchlights);
            _nullCityDarkness = 0f;
            for (var i = 0; i < _deathGhosts.Length; i++)
                if (IsNullCityEnemy(_deathGhosts[i].Id)) ClearNullCityProp(_deathGhostViews[i]);
            _nullCityPresentationVisible = false;
        }

        private void DetachNullCityAssetConsumers()
        {
            var visuals = NullCityVisuals;
            if (visuals != null)
            {
                DetachNullCitySprites(_enemyViews, visuals);
                DetachNullCitySprites(_bossViews, visuals);
                DetachNullCitySprites(_deathGhostViews, visuals);
            }
            var index = (int)ArenaId.NullCity;
            if (_backdropView != null && _backdropView.sprite == _arenaPlateSprites[index]) _backdropView.sprite = null;
            if (_arenaBakedDetailView != null && _arenaBakedDetailView.sprite == _arenaPlateDetailSprites[index]) _arenaBakedDetailView.sprite = null;
            HideNullCityPresentation();
        }

        private static void DetachNullCitySprites(SpriteRenderer[] views, NullCityVisualAsset visuals)
        {
            for (var i = 0; i < views.Length; i++)
                if (views[i] != null && visuals.OwnsSprite(views[i].sprite)) views[i].sprite = null;
        }

        private static void ClearNullCityProp(SpriteRenderer view)
        {
            if (view == null) return;
            view.enabled = false;
            view.sprite = null;
        }

        private void HideLegacyCityDecor()
        {
            if (_arenaVignetteView != null) _arenaVignetteView.enabled = false;
            if (_arenaGridRenderer != null) _arenaGridRenderer.enabled = false;
            if (_arenaCurrentGlowView != null) _arenaCurrentGlowView.enabled = false;
            if (_arenaLandmarkBodyView != null) _arenaLandmarkBodyView.enabled = false;
            HideNullCityViews(_arenaMoteViews); HideNullCityViews(_arenaStarViews);
            HideNullCityViews(_arenaRockViews); HideNullCityViews(_arenaRockPlaneViews);
            HideNullCityViews(_arenaNearFilamentOuterRenderers); HideNullCityViews(_arenaNearFilamentInnerViews);
            HideNullCityViews(_arenaNearFilamentStrandRenderers); HideNullCityViews(_arenaFilamentPlateViews);
            HideNullCityViews(_arenaRockRimViews); HideNullCityViews(_arenaStellarRimViews);
            HideNullCityViews(_arenaLandmarkViews); HideNullCityViews(_arenaLandmarkRimViews);
            HideNullCityViews(_arenaRingSlabFillRenderers); HideNullCityViews(_arenaRingDebrisViews);
            HideNullCityViews(_arenaOrbitViews); HideNullCityViews(_arenaOrbitFractureViews);
        }

        private void NullCityLine(ref LineRenderer view, string name, Vector2 a, Vector2 b, float width, Color color, int order = 20)
        {
            if (view == null) view = CreateLineView(name, order);
            view.positionCount = 2;
            view.SetPosition(0, a);
            view.SetPosition(1, b);
            view.startWidth = view.endWidth = width;
            view.startColor = view.endColor = color;
            view.enabled = true;
        }

        private void NullCityCircle(ref LineRenderer view, string name, Vector2 position, float radius, Color color)
        {
            if (view == null) view = CreateLineView(name, 20);
            view.positionCount = 49;
            for (var i = 0; i <= 48; i++)
            {
                var angle = i * Mathf.PI * 2f / 48;
                view.SetPosition(i, position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
            view.startWidth = view.endWidth = 1.6f;
            view.startColor = view.endColor = color;
            view.enabled = true;
        }

        private void NullCityProp(ref SpriteRenderer view, string name, Sprite sprite, Vector2 position, Color color, int order)
        {
            if (view == null) view = CreateView(name, sprite, order);
            view.sprite = sprite;
            view.transform.position = position;
            view.transform.rotation = Quaternion.identity;
            view.transform.localScale = Vector3.one;
            view.color = color;
            view.enabled = sprite != null;
        }

        private void RenderNullCityArena()
        {
            EnsureArenaPlate(ArenaId.NullCity);
            if (_mainMenuBrowsing) _nullCityOrigin = Vector2.zero;
            HideLegacyCityDecor();
            _nullCityPresentationVisible = true;
            var clock = _mainMenuBrowsing ? _ambientClock : _nullCityElapsed;
            var boss = !_mainMenuBrowsing && _nullCityBossActive;
            var cleared = !_mainMenuBrowsing && _nullCityCleared;
            var lockdown = NullCityRules.CycleAt(clock, boss) == NullCityCycle.Lockdown;
            _nullCityDarkness = Mathf.MoveTowards(_nullCityDarkness, lockdown ? 1f : 0f, Time.unscaledDeltaTime * .9f);
            var index = (int)ArenaId.NullCity;
            _backdropView.sprite = _arenaPlateSprites[index];
            _backdropView.transform.position = _nullCityOrigin;
            _backdropView.transform.rotation = Quaternion.identity;
            _backdropView.color = Color.Lerp(Color.white, new Color(.53f, .57f, .72f, 1f), _nullCityDarkness);
            _backdropView.enabled = _backdropView.sprite != null;
            if (_backdropView.sprite != null)
            {
                var size = _backdropView.sprite.bounds.size;
                _backdropView.transform.localScale = new Vector3(1600f / size.x, 900f / size.y, 1f);
            }
            if (_arenaBakedDetailView != null)
            {
                _arenaBakedDetailView.sprite = _arenaPlateDetailSprites[index];
                _arenaBakedDetailView.transform.position = _nullCityOrigin;
                _arenaBakedDetailView.transform.rotation = Quaternion.identity;
                _arenaBakedDetailView.color = new Color(1f, 1f, 1f, Mathf.Lerp(.9f, .22f, _nullCityDarkness));
                _arenaBakedDetailView.enabled = _arenaBakedDetailView.sprite != null;
                if (_arenaBakedDetailView.sprite != null)
                {
                    var size = _arenaBakedDetailView.sprite.bounds.size;
                    _arenaBakedDetailView.transform.localScale = new Vector3(1600f / size.x, 900f / size.y, 1f);
                }
            }
            var visuals = NullCityVisuals;
            if (visuals != null)
            {
                // Recipe seeds vary moving compositions without moving the authored collision lanes.
                var decor = clock + _arenaRecipeIndex * 4.25f;
                NullCityProp(ref _nullCityTransitView, "Null City Transit", visuals.Transit,
                    NullCityWorld(180f + Mathf.Repeat(decor * 78f, 1280f), 235f), Color.white, -88);
                NullCityProp(ref _nullCityHangarView, "Null City Hangar", lockdown && !cleared ? visuals.HangarOpen : visuals.HangarClosed,
                    NullCityWorld(805f, 800f), Color.white, -87);
                NullCityProp(ref _nullCityLcdView, "Null City LCD", lockdown ? visuals.LcdLockdown : visuals.LcdSurveillance,
                    NullCityWorld(1172.5f, 107.5f), Color.white, -86);
                for (var i = 0; i < _nullCityTrafficViews.Length; i++)
                {
                    var x = lockdown ? 1400f - Mathf.Repeat(decor * 27f + (i % 4) * 297f, 1190f)
                        : 198f + Mathf.Repeat(decor * 42f + (i % 4) * 295f, 1200f);
                    var y = (i < 4 ? 345f : 592f) + (i % 2 == 0 ? -16f : 16f);
                    NullCityProp(ref _nullCityTrafficViews[i], "Null City Traffic",
                        lockdown ? visuals.TrafficLockdown : visuals.Traffic,
                        NullCityWorld(x, y), new Color(1f, 1f, 1f, lockdown ? .55f : .8f), -85);
                }
            }
            var reducedMotion = _saveData?.settings != null && _saveData.settings.reducedMotion;
            for (var i = 0; i < _nullCitySearchlights.Length; i++)
            {
                if (lockdown) { if (_nullCitySearchlights[i] != null) _nullCitySearchlights[i].enabled = false; continue; }
                var source = NullCityWorld(i == 0 ? 421f : 1288f, 180f);
                var angle = -(i == 0 ? 1f : 2.1f) - (reducedMotion ? 0f : Mathf.Sin(clock * .23f + i) * .45f);
                var end = source + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 510f;
                NullCityLine(ref _nullCitySearchlights[i], "Null City Searchlight", source, end, 1f, new Color(.65f, .92f, .94f, .07f), -94);
                _nullCitySearchlights[i].startWidth = 1f;
                _nullCitySearchlights[i].endWidth = 154f;
                _nullCitySearchlights[i].endColor = new Color(.4f, .75f, .85f, .018f);
            }
            for (var i = 0; i < _nullCityRain.Length; i++)
            {
                if (reducedMotion) { if (_nullCityRain[i] != null) _nullCityRain[i].enabled = false; continue; }
                var x = Mathf.Repeat(i * 197.3f + clock * 17f, 1600f);
                var y = Mathf.Repeat(i * 89.7f + clock * 127f, 900f);
                NullCityLine(ref _nullCityRain[i], "Null City Rain", NullCityWorld(x, y), NullCityWorld(x - 2f, y + 7f), .65f,
                    new Color(.6f, .73f, .85f, .16f), -84);
            }
            HideNullCityCombatTelegraphs();
            if (!cleared)
            {
                RenderNullCityPurge(NullCityRules.PurgeAt(boss ? _nullCityBossElapsed : clock, boss));
                if (!_mainMenuBrowsing) RenderNullCityCombatTelegraphs();
            }
            if (_camera != null)
            {
                var half = NullCityViewportHalfExtent();
                _camera.orthographicSize = half.y;
                _camera.aspect = half.x / half.y;
                var shake = CameraShakeOffset();
                _camera.transform.position = new Vector3(_nullCityOrigin.x + shake.x, _nullCityOrigin.y + shake.y, -10f);
            }
            UpdateTransitionOverlay();
        }

        private Vector2 NullCityViewportHalfExtent()
        {
            var aspect = Screen.height > 0 ? Mathf.Max(.5f, (float)Screen.width / Screen.height) : 16f / 9f;
            var halfHeight = Mathf.Max(450f, 800f / aspect) * _spatialZoomScale;
            return new Vector2(halfHeight * aspect, halfHeight);
        }

        private void RenderNullCityPurge(NullCityPurge h)
        {
            if (!h.Visible) return;
            var x = (float)h.X; var y = (float)h.Y; var w = (float)h.Width; var height = (float)h.Height;
            var a = NullCityWorld(x, y + height * .5f);
            var b = NullCityWorld(x + w, y + height * .5f);
            NullCityLine(ref _nullCityPurgeFill, "Null City Purge Fill", a, b, height,
                new Color(1f, .58f, .3f, h.Active ? .22f : .075f), -74);
            if (_nullCityPurgeBorder == null) _nullCityPurgeBorder = CreateLineView("Null City Purge Border", -72);
            _nullCityPurgeBorder.positionCount = 5;
            _nullCityPurgeBorder.SetPosition(0, NullCityWorld(x, y));
            _nullCityPurgeBorder.SetPosition(1, NullCityWorld(x + w, y));
            _nullCityPurgeBorder.SetPosition(2, NullCityWorld(x + w, y + height));
            _nullCityPurgeBorder.SetPosition(3, NullCityWorld(x, y + height));
            _nullCityPurgeBorder.SetPosition(4, NullCityWorld(x, y));
            _nullCityPurgeBorder.startWidth = _nullCityPurgeBorder.endWidth = 1.5f;
            _nullCityPurgeBorder.startColor = _nullCityPurgeBorder.endColor = new Color(1f, .75f, .49f, h.Active ? .95f : .65f);
            _nullCityPurgeBorder.enabled = true;
            var stripe = 0;
            for (var k = -height; k < w && stripe < _nullCityPurgeStripes.Length; k += 36f)
            {
                var startY = Mathf.Max(0f, -k); var endY = Mathf.Min(height, w - k);
                if (endY <= startY) continue;
                NullCityLine(ref _nullCityPurgeStripes[stripe], "Null City Purge Stripe",
                    NullCityWorld(x + k + startY, y + startY), NullCityWorld(x + k + endY, y + endY), 4f,
                    new Color(1f, .77f, .52f, h.Active ? .27f : .15f), -73);
                stripe++;
            }
            if (h.Active)
            {
                if (h.Lane >= 2) { a = NullCityWorld(x + w * .5f, y); b = NullCityWorld(x + w * .5f, y + height); }
                NullCityLine(ref _nullCityPurgeBeam, "Null City Purge Discharge", a, b, 4f, new Color(1f, .94f, .77f, .95f), -71);
            }
        }

        private void RenderNullCityCombatTelegraphs()
        {
            for (var i = 0; i < _gameSim.Enemies.Length; i++)
            {
                var e = _gameSim.Enemies[i];
                if (!e.Active || !IsNullCityEnemy(e.Id)) continue;
                var color = ParseColor(NullCityContent.FindEnemy(e.Id).Color, Color.cyan);
                color.a = .75f;
                if (e.State == 1)
                {
                    if (e.Id == "null-volatile" || e.Id == "null-mech")
                        NullCityCircle(ref _nullCityEnemyWarnings[i], "Null City Area Warning", e.Position, e.Id == "null-volatile" ? 124f : 128f, color);
                    else
                        NullCityLine(ref _nullCityEnemyWarnings[i], "Null City Aim", e.Position, e.Position + e.Facing * (e.Id == "null-sentinel" ? 620f : 240f), 1.2f, color);
                }
                else if (e.Id == "null-marshal" && e.Age % 6f < 3f)
                {
                    var right = new Vector2(-e.Facing.y, e.Facing.x);
                    NullCityLine(ref _nullCityEnemyWarnings[i], "Null City Marshal Shield",
                        e.Position + e.Facing * 30f - right * 23f, e.Position + e.Facing * 30f + right * 23f, 3f, color);
                }
            }
            for (var i = 0; i < _nullCityBombs.Length; i++)
                if (_nullCityBombs[i].Active)
                    NullCityCircle(ref _nullCityBombWarnings[i], "Null City Bomb Warning", _nullCityBombs[i].Position, 70f, new Color(1f, .77f, .48f, .8f));
            if (!_nullCityBossActive || _nullCityBossSlot < 0) return;
            var boss = _gameSim.Bosses[_nullCityBossSlot];
            if (_nullCityMove == MotherloadMove.Cannons && _nullCityWarnClock > 0f)
            {
                for (var i = 0; i < _nullCityCannonWarnings.Length; i++)
                {
                    var angle = _nullCityAim + (i - 3.5f) * .15f;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    var muzzle = MotherloadMuzzle(boss, i);
                    NullCityLine(ref _nullCityCannonWarnings[i], "Motherload Cannon Aim",
                        muzzle, muzzle + direction * 500f, 1f, new Color(1f, .85f, .55f, .24f));
                }
            }
            if (_nullCityTractorClock <= 0f && !(_nullCityMove == MotherloadMove.Tractor && _nullCityWarnClock > 0f)) return;
            var forward = new Vector2(Mathf.Cos(_nullCityAim), Mathf.Sin(_nullCityAim));
            var side = new Vector2(-forward.y, forward.x);
            var spread = Mathf.Tan(.38f);
            var active = _nullCityTractorClock > 0f;
            NullCityLine(ref _nullCityTractorFill, "Motherload Event Horizon", boss.Position + forward * 145f, boss.Position + forward * 640f,
                100f, new Color(.6f, .86f, 1f, active ? .15f : .035f), 18);
            _nullCityTractorFill.startWidth = 290f * spread;
            _nullCityTractorFill.endWidth = 1280f * spread;
            _nullCityTractorFill.endColor = new Color(.4f, .75f, 1f, active ? .05f : .012f);
            for (var i = 0; i < 2; i++)
            {
                var sign = i == 0 ? -1f : 1f;
                NullCityLine(ref _nullCityTractorEdges[i], "Motherload Capture Edge",
                    boss.Position + forward * 145f + side * (145f * spread * sign),
                    boss.Position + forward * 640f + side * (640f * spread * sign), 1.4f, new Color(.7f, .91f, 1f, active ? .6f : .3f));
            }
            if (active)
                for (var i = 0; i < _nullCityTractorFlow.Length; i++)
                {
                    var d = 640f - Mathf.Repeat(_nullCityBossElapsed * 180f + i * 49f, 495f);
                    var center = boss.Position + forward * d;
                    NullCityLine(ref _nullCityTractorFlow[i], "Motherload Capture Flow",
                        center - side * (d * spread * .8f), center + side * (d * spread * .8f), .8f, new Color(.75f, .92f, 1f, .22f));
                }
        }
    }
}
