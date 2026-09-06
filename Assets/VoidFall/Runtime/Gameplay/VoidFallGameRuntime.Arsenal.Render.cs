using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private readonly SpriteRenderer[] _arsenalMineViews = new SpriteRenderer[ArsenalMineCapacity];
        private readonly LineRenderer[] _arsenalMineRanges = new LineRenderer[ArsenalMineCapacity];
        private readonly SpriteRenderer[] _arsenalSummonViews = new SpriteRenderer[ArsenalSummonCapacity];
        private readonly SpriteRenderer[] _arsenalBoomerangViews = new SpriteRenderer[ArsenalBoomerangCapacity];
        private readonly SpriteRenderer[] _arsenalClockHands = new SpriteRenderer[2];
        private SpriteRenderer _arsenalClockFace;

        private void WarmArsenalVisuals()
        {
            // Recalculate runs during the paused upgrade commit; first fire never rasterizes these assets.
            for (var index = 6; index < ContentCatalog.Weapons.Length; index++)
            {
                var rank = ArsenalRank(index);
                if (rank <= 0) continue;
                ProceduralSpriteFactory.ArsenalWeapon(ContentCatalog.Weapons[index].Id, rank, ArsenalEvolved(index));
                if (index != 8) continue;
                ProceduralSpriteFactory.ArsenalClockFace();
                ProceduralSpriteFactory.ArsenalWeapon("clock", rank, false);
            }
        }

        private void HideArsenalViews()
        {
            foreach (var view in _arsenalMineViews) Hide(view);
            foreach (var view in _arsenalMineRanges) Hide(view);
            foreach (var view in _arsenalSummonViews) Hide(view);
            foreach (var view in _arsenalBoomerangViews) Hide(view);
            foreach (var view in _arsenalClockHands) Hide(view);
            Hide(_arsenalClockFace);
        }

        private SpriteRenderer ArsenalView(ref SpriteRenderer view, string id, int rank, bool evolved, Vector2 position, float angle, float size, float opacity = 1)
        {
            var sprite = ProceduralSpriteFactory.ArsenalWeapon(id, rank, evolved);
            if (view == null) view = CreateView("Arsenal_" + id, sprite, 27);
            view.sprite = sprite;
            view.transform.position = position;
            view.transform.rotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg);
            view.transform.localScale = Vector3.one * size;
            view.color = new Color(1, 1, 1, opacity);
            view.enabled = true;
            return view;
        }

        private void RenderArsenalWeapons()
        {
            if (_gameSim == null || _worldRoot == null) return;
            var size = ArsenalSizeMultiplier();
            for (var i = 0; i < _arsenalMines.Length; i++)
            {
                var mine = _arsenalMines[i];
                if (!mine.Active) { Hide(_arsenalMineViews[i]); Hide(_arsenalMineRanges[i]); continue; }
                var armed = mine.Age >= ArsenalContent.MineArmingSeconds;
                ArsenalView(ref _arsenalMineViews[i], "mines", mine.Rank, mine.Evolved, mine.Position, 0, 48 * size, armed ? 1 : .4f);
                if (_arsenalMineRanges[i] == null) _arsenalMineRanges[i] = CreateLineView("MineRange_" + i, 9);
                var radius = (float)ArsenalStats(6, mine.Rank).BlastRadius * _areaMultiplier;
                var color = ParseColor(mine.Evolved ? "#8ceaff" : "#ffb75e", Color.white);
                color.a = .118f * (float)ArsenalContent.MineRangeOpacity;
                SetArcLine(_arsenalMineRanges[i], mine.Position, radius, 0, Mathf.PI * 2, 1, color);
                _arsenalMineRanges[i].enabled = armed;
            }
            for (var i = 0; i < _arsenalSummons.Length; i++)
            {
                var summon = _arsenalSummons[i];
                if (!summon.Active) { Hide(_arsenalSummonViews[i]); continue; }
                ArsenalView(ref _arsenalSummonViews[i], "summons", summon.Rank, summon.Evolved, summon.Position, summon.Angle, 48 * size);
            }
            for (var i = 0; i < _arsenalBoomerangs.Length; i++)
            {
                var shot = _arsenalBoomerangs[i];
                if (!shot.Active) { Hide(_arsenalBoomerangViews[i]); continue; }
                ArsenalView(ref _arsenalBoomerangViews[i], "boomerang", shot.Rank, shot.Evolved, shot.Position, shot.Age * 19, 48 * size, shot.Returning ? .5f : 1);
            }
            var clockRank = ArsenalRank(8);
            if (clockRank > 0)
            {
                if (_arsenalClockFace == null) _arsenalClockFace = CreateView("Arsenal_ClockFace", ProceduralSpriteFactory.ArsenalClockFace(), 9);
                _arsenalClockFace.transform.position = _gameSim.Player.Position;
                _arsenalClockFace.transform.localScale = Vector3.one * (300 * _areaMultiplier);
                _arsenalClockFace.color = new Color(1, 1, 1, (float)ArsenalContent.ClockFaceOpacity);
                _arsenalClockFace.enabled = true;
                var hands = ArsenalEvolved(8) ? 2 : 1;
                for (var i = 0; i < _arsenalClockHands.Length; i++)
                {
                    if (i >= hands) { Hide(_arsenalClockHands[i]); continue; }
                    var angle = i == 0 ? _arsenalClockAngle : -_arsenalClockAngle + Mathf.PI;
                    var view = ArsenalView(ref _arsenalClockHands[i], "clock", clockRank, i > 0, _gameSim.Player.Position, angle, 300 * _areaMultiplier, (float)ArsenalContent.ClockOpacity);
                    view.transform.localScale = new Vector3(300 * _areaMultiplier, 300 * size, 1);
                }
            }
            else { Hide(_arsenalClockFace); foreach (var hand in _arsenalClockHands) Hide(hand); }
            for (var i = 0; i < _arsenalFreeze.Length; i++)
            {
                if (_arsenalFreeze[i] <= 0 || _enemyViews[i] == null) continue;
                var enemy = _gameSim.Enemies[i];
                if (enemy.Active && _arsenalFreezeIds[i] == EnemyIdentity(enemy, i)) _enemyViews[i].color = new Color(.6f, .88f, 1, _enemyViews[i].color.a);
            }
        }
    }
}
