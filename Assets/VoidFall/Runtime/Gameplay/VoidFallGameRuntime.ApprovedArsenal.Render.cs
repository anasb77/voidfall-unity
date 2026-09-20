using System;
using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private const int SummonTrailPoints = 8;
        private readonly Vector2[] _summonTrailPositions = new Vector2[ArsenalSummonCapacity * SummonTrailPoints];
        private readonly float[] _summonTrailTimers = new float[ArsenalSummonCapacity];
        private readonly LineRenderer[] _summonTrails = new LineRenderer[ArsenalSummonCapacity];
        private readonly LineRenderer[] _summonTargetRings = new LineRenderer[ArsenalSummonCapacity];
        private readonly LineRenderer[] _mineArmingRings = new LineRenderer[ArsenalMineCapacity];
        private readonly LineRenderer[] _mineChainLinks = new LineRenderer[ArsenalMineCapacity];
        private struct MineImpact { public Vector2 Position; public float Age, Radius; public bool Active, Evolved, Summon; }
        private readonly MineImpact[] _mineImpacts = new MineImpact[32];
        private readonly LineRenderer[] _mineImpactRings = new LineRenderer[32 * 4];
        private int _nextMineImpact;

        private void ResetSummonTrail(int slot, Vector2 position)
        {
            _summonTrailTimers[slot] = 0;
            for (var i = 0; i < SummonTrailPoints; i++) _summonTrailPositions[slot * SummonTrailPoints + i] = position;
        }

        private void UpdateSummonTrail(int slot, Vector2 position, float dt)
        {
            _summonTrailTimers[slot] += dt;
            if (_summonTrailTimers[slot] < 1f / 24) return;
            _summonTrailTimers[slot] %= 1f / 24;
            var offset = slot * SummonTrailPoints;
            for (var i = SummonTrailPoints - 1; i > 0; i--) _summonTrailPositions[offset + i] = _summonTrailPositions[offset + i - 1];
            _summonTrailPositions[offset] = position;
        }

        private void SpawnMineImpact(Vector2 position, float radius, bool evolved, bool summon = false)
        {
            if (_saveData?.settings != null && _saveData.settings.reducedMotion || _qualityPreset.ParticleScale <= .01f) return;
            _mineImpacts[_nextMineImpact] = new MineImpact { Active = true, Position = position, Radius = radius, Evolved = evolved, Summon = summon };
            _nextMineImpact = (_nextMineImpact + 1) % _mineImpacts.Length;
            BurstFx(position, ParseColor(summon ? "#87f5ab" : evolved ? "#8ceaff" : "#ffb75e", Color.white), 20, 190, .3f, .55f);
        }

        private void StepMineImpacts(float dt)
        {
            for (var i = 0; i < _mineImpacts.Length; i++)
            {
                if (!_mineImpacts[i].Active) continue;
                _mineImpacts[i].Age += dt;
                if (_mineImpacts[i].Age >= .62f) _mineImpacts[i].Active = false;
            }
        }

        private void HideApprovedArsenalViews()
        {
            foreach (var view in _summonTrails) Hide(view);
            foreach (var view in _summonTargetRings) Hide(view);
            foreach (var view in _mineArmingRings) Hide(view);
            foreach (var view in _mineChainLinks) Hide(view);
            foreach (var view in _mineImpactRings) Hide(view);
            Array.Clear(_mineImpacts, 0, _mineImpacts.Length);
        }

        private void RenderApprovedArsenalViews()
        {
            var reduced = _saveData?.settings != null && _saveData.settings.reducedMotion;
            for (var i = 0; i < _arsenalMines.Length; i++)
            {
                var mine = _arsenalMines[i];
                if (!mine.Active) { Hide(_mineArmingRings[i]); Hide(_mineChainLinks[i]); continue; }
                var color = ParseColor(mine.Evolved ? "#8ceaff" : "#ffb75e", Color.white);
                if (_mineArmingRings[i] == null) _mineArmingRings[i] = CreateLineView("MineArming_" + i, 28);
                color.a = mine.ChainDueAge > 0 ? .9f : .5f;
                var progress = Mathf.Clamp01(mine.Age / (float)ArsenalContent.MineArmingSeconds);
                SetArcLine(_mineArmingRings[i], mine.Position, 17 * ArsenalSizeMultiplier(), -Mathf.PI / 2, -Mathf.PI / 2 + Mathf.PI * 2 * progress, 1.5f, color);
                if (mine.ChainDueAge <= 0) { Hide(_mineChainLinks[i]); continue; }
                if (_mineChainLinks[i] == null) _mineChainLinks[i] = CreateLineView("MineChain_" + i, 25);
                var link = _mineChainLinks[i];
                link.positionCount = 2;
                link.SetPosition(0, mine.ChainOrigin); link.SetPosition(1, mine.Position);
                link.startColor = link.endColor = color;
                link.startWidth = link.endWidth = 1.5f;
                link.enabled = true;
            }
            for (var i = 0; i < _arsenalSummons.Length; i++)
            {
                var unit = _arsenalSummons[i];
                if (!unit.Active || unit.Idle || reduced) { Hide(_summonTrails[i]); Hide(_summonTargetRings[i]); continue; }
                if (_summonTrails[i] == null) _summonTrails[i] = CreateLineView("SummonPursuit_" + i, 26);
                var line = _summonTrails[i];
                line.positionCount = SummonTrailPoints;
                line.SetPosition(0, unit.Position);
                for (var p = 1; p < SummonTrailPoints; p++) line.SetPosition(p, _summonTrailPositions[i * SummonTrailPoints + p]);
                line.startColor = new Color(.53f, .96f, .67f, .4f); line.endColor = new Color(.53f, .96f, .67f, 0);
                line.startWidth = 1.3f; line.endWidth = .2f; line.enabled = true;
                var target = unit.Target;
                if (!RefreshSummonTarget(ref target)) { Hide(_summonTargetRings[i]); continue; }
                if (_summonTargetRings[i] == null) _summonTargetRings[i] = CreateLineView("SummonTarget_" + i, 10);
                SetArcLine(_summonTargetRings[i], target.Position, ArsenalTargetRadius(target) + 5, 0, Mathf.PI * 2, .8f, new Color(.53f, .96f, .67f, .25f));
            }
            for (var i = 0; i < _mineImpacts.Length; i++)
            {
                var impact = _mineImpacts[i];
                for (var ring = 0; ring < 4; ring++)
                {
                    var slot = i * 4 + ring;
                    if (!impact.Active || reduced) { Hide(_mineImpactRings[slot]); continue; }
                    if (_mineImpactRings[slot] == null) _mineImpactRings[slot] = CreateLineView("MineImpact_" + slot, 30);
                    var line = _mineImpactRings[slot];
                    var t = Mathf.Clamp01(impact.Age / .62f);
                    var radius = impact.Radius * Mathf.Sqrt(t) * (1 - ring * .16f);
                    var color = ParseColor(impact.Summon ? "#87f5ab" : impact.Evolved ? "#8ceaff" : "#ffb75e", Color.white);
                    color.a = (1 - t) * (ring == 0 ? .85f : .35f);
                    if (ring == 3)
                    {
                        SetArcLine(line, impact.Position, impact.Radius * Mathf.Sqrt(t) * .72f, 0, Mathf.PI * 2, 1.3f, color);
                        continue;
                    }
                    line.positionCount = 7;
                    for (var p = 0; p <= 6; p++)
                    {
                        var a = p * Mathf.PI / 3 + ring * .13f;
                        line.SetPosition(p, impact.Position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
                    }
                    line.startColor = line.endColor = color;
                    line.startWidth = line.endWidth = ring == 0 ? 2.2f : 1;
                    line.enabled = true;
                }
            }
        }
    }
}
