using System;
using UnityEngine;
using VoidFall.Core;
using VoidFall.UI;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private readonly MajorIncidentState _majorIncident = new MajorIncidentState();
        private DirectorIncidentPresentation _incidentPresentation;
        private Vector2 _incidentCenter;
        private const float IncidentRadius = 290f;
        private float _nextIncidentOpportunity;
        private int _incidentSequence, _incidentCount;
        private MajorIncidentKind _lastIncidentKind;
        private bool _incidentRaidStarted, _directorDiagnosticRun;

        private struct IncidentSpriteTint { public SpriteRenderer View; public Color Color; }
        private struct IncidentLineTint { public LineRenderer View; public Color Start, End; }
        private struct IncidentMaterialTint { public Material Material; public Color Gas, Core; }
        private readonly IncidentSpriteTint[] _incidentSpriteTints = new IncidentSpriteTint[MaxArenaMotes + MaxArenaStars + MaxArenaRocks * 2 + MaxArenaRingDebris + ArenaFilamentPlateCount + 8];
        private readonly IncidentLineTint[] _incidentLineTints = new IncidentLineTint[MaxArenaFilamentSlots + MaxArenaRocks + MaxArenaStellarRimSegments + MaxArenaLandmarkSegments * 2 + MaxArenaOrbitViews + MaxArenaOrbitFractures];
        private readonly IncidentMaterialTint[] _incidentMaterialTints = new IncidentMaterialTint[MaxArenaFilamentSlots];
        private int _incidentSpriteTintCount, _incidentLineTintCount, _incidentMaterialTintCount;
        private static readonly int IncidentGasColorId = Shader.PropertyToID("_GasColor");
        private static readonly int IncidentCoreColorId = Shader.PropertyToID("_CoreColor");

        public string CurrentMajorIncident => _majorIncident.Kind.ToString();
        public string CurrentMajorIncidentPhase => _majorIncident.Phase.ToString();
        public int MajorIncidentsSeen => _incidentCount;

        private void ResetMajorIncidentRun()
        {
            StopMajorIncident();
            _incidentSequence = _incidentCount = 0;
            _lastIncidentKind = MajorIncidentKind.None;
            _directorDiagnosticRun = false;
            _nextIncidentOpportunity = 85 + _runSeed % 36;
        }

        private void StopMajorIncident()
        {
            _majorIncident.Reset();
            _incidentRaidStarted = false;
            EndDestroyerRaid();
            _incidentPresentation?.Hide();
            RestoreIncidentEnvironment();
        }

        private bool IncidentArenaEligible() => _arenaId == ArenaId.Void || _arenaId == ArenaId.WhiteSakura || _arenaId == ArenaId.RedNebula;

        private void StepMajorIncidents(float dt)
        {
            if (dt <= 0 || _mainMenuBrowsing || _gameOver || _paused || JourneyStopsCombat) return;
            if (ActiveBosses() > 0 || !IncidentArenaEligible() || LocalDirectorSurvivalSeconds >= 285)
            {
                if (_majorIncident.Kind != MajorIncidentKind.None) StopMajorIncident();
                _nextIncidentOpportunity = Mathf.Max(_nextIncidentOpportunity, _time + 30);
                return;
            }
            if (_majorIncident.Kind != MajorIncidentKind.None)
            {
                var kind = _majorIncident.Kind;
                _majorIncident.Step(dt);
                if (kind == MajorIncidentKind.DestroyerRaid)
                {
                    if (!_incidentRaidStarted && _majorIncident.Phase == MajorIncidentPhase.Active)
                    { StartDestroyerRaid(_incidentCenter); _incidentRaidStarted = true; }
                    StepDestroyerRaid(dt, _majorIncident.Phase == MajorIncidentPhase.Release);
                }
                if (_majorIncident.Kind == MajorIncidentKind.None)
                {
                    StopMajorIncident();
                    _nextIncidentOpportunity = _time + 240 + (_runSeed + (uint)_incidentSequence) % 61;
                    _spawnTimer = .65f;
                }
                return;
            }
            if (_stressScenario != null || _incidentCount >= 4 || _time < _nextIncidentOpportunity) return;
            _incidentSequence++;
            _nextIncidentOpportunity = _time + 30 + (_runSeed ^ (uint)(_incidentSequence * 7919)) % 21;
            if (_encounter.Phase != CombatEncounterPhase.Flow || LocalDirectorSurvivalSeconds < 45 ||
                ActiveDemandingEnemies() > 0 || ActiveEnemies() < 8 || ActiveEnemies() > DirectorBodyLimit() - 5 || !DirectorOpeningSafe()) return;
            // Native meteors already spend attention; do not stack a generic incident onto a storm.
            if (_arenaId == ArenaId.RedNebula && ActiveMeteorsCountForIncident() > 2) return;
            var kindChoice = (MajorIncidentKind)(1 + ((_runSeed ^ (uint)(_incidentSequence * 104729)) % 3));
            if (kindChoice == _lastIncidentKind) kindChoice = (MajorIncidentKind)(1 + (int)kindChoice % 3);
            if (!MajorIncidentRules.CanBegin(kindChoice, 300 - LocalDirectorSurvivalSeconds, false, false, false)) return;
            if (kindChoice == MajorIncidentKind.DestroyerRaid && ActiveEnemyThreat() > DirectorBodyLimit() * 1.55f - 45) return;
            BeginMajorIncident(kindChoice);
        }

        private int ActiveMeteorsCountForIncident()
        {
            var count = 0;for (var i = 0; i < _gameSim.Meteors.Length; i++) if (_gameSim.Meteors[i].Active) count++;return count;
        }

        private void BeginMajorIncident(MajorIncidentKind kind)
        {
            _incidentCenter = _gameSim.Player.Position;
            if (kind == MajorIncidentKind.BlackHole)
            {
                // Choose a nearby enemy direction, then lock a center outside the player's body.
                var direction = Vector2.right;
                var nearest = float.MaxValue;
                for (var i = 0; i < _gameSim.Enemies.Length; i++)
                {
                    var enemy = _gameSim.Enemies[i];if (!enemy.Active || enemy.Elite) continue;
                    var delta = enemy.Position - _gameSim.Player.Position;
                    if (delta.sqrMagnitude > 180 * 180 && delta.sqrMagnitude < nearest)
                    { nearest = delta.sqrMagnitude;direction = delta.normalized; }
                }
                _incidentCenter += direction * 330;
            }
            _incidentRaidStarted = false;
            _majorIncident.Begin(kind);
            _incidentCount++;
            _lastIncidentKind = kind;
            _spawnTimer = .65f;
            ShowArenaToast(kind == MajorIncidentKind.BlackHole ? "BLACK HOLE · ATTRACTION INCOMING" :
                kind == MajorIncidentKind.DestroyerRaid ? "THE DESTROYERS HAVE ARRIVED" : "ECLIPSE", 2.5f, ToastKind.Danger);
        }

        public bool ForceMajorIncidentForDiagnostics(string name)
        {
            if (!IncidentArenaEligible() || ActiveBosses() > 0 || JourneyStopsCombat) return false;
            var kind = name == "black-hole" ? MajorIncidentKind.BlackHole : name == "raid" ? MajorIncidentKind.DestroyerRaid :
                name == "eclipse" ? MajorIncidentKind.Eclipse : MajorIncidentKind.None;
            if (kind == MajorIncidentKind.None) return false;
            StopMajorIncident();CancelEncounterDirector();
            _directorDiagnosticRun = true;
            BeginMajorIncident(kind);return true;
        }

        private void ApplyMajorIncidentPlayerDisplacement(float dt)
        {
            if (!BlackHoleAttractionActive() || dt <= 0) return;
            var delta = _incidentCenter - _gameSim.Player.Position;
            var distance = delta.magnitude;if (distance <= .0001f) return;
            var pull = (float)MajorIncidentRules.BlackHolePullScale(distance, IncidentRadius, _majorIncident.Strength);
            _gameSim.Player.Position += delta / distance * ((float)ContentCatalog.Operative.MoveSpeed / 3 * pull * dt);
        }

        private void ApplyMajorIncidentEnemyDisplacement(ref EnemyState enemy, float dt)
        {
            if (!enemy.Active || enemy.Elite || !BlackHoleAttractionActive() || dt <= 0) return;
            var delta = _incidentCenter - enemy.Position;
            var distance = delta.magnitude;if (distance <= .0001f) return;
            var pull = (float)MajorIncidentRules.BlackHolePullScale(distance, IncidentRadius, _majorIncident.Strength);
            enemy.Position += delta / distance * ((float)ContentCatalog.Operative.MoveSpeed * pull * dt);
        }

        private bool BlackHoleAttractionActive() => _majorIncident.Kind == MajorIncidentKind.BlackHole &&
            _majorIncident.Phase != MajorIncidentPhase.Warning && !_mainMenuBrowsing && !_gameOver && !_paused &&
            !JourneyStopsCombat && IncidentArenaEligible() && ActiveBosses() == 0;

        private void RenderMajorIncidents()
        {
            if (_majorIncident.Kind == MajorIncidentKind.None || _mainMenuBrowsing || _gameOver || JourneyStopsCombat)
            { _incidentPresentation?.Hide(); return; }
            if (_worldRoot == null || _camera == null) return;
            if (_incidentPresentation == null) _incidentPresentation = new DirectorIncidentPresentation(_worldRoot, _camera);
            Texture backdrop = null;var rect = Vector4.zero;
            if (_backdropView != null && _backdropView.sprite != null)
            {
                var sprite = _backdropView.sprite;
                if (!sprite.packed && sprite.rect.width == sprite.texture.width && sprite.rect.height == sprite.texture.height)
                {
                    backdrop = sprite.texture;var bounds = _backdropView.bounds;
                    rect = new Vector4(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y);
                }
            }
            _incidentPresentation.Render(_majorIncident.Kind, _majorIncident.Phase, _incidentCenter, IncidentRadius,
                (float)_majorIncident.Strength, _saveData?.settings?.reducedMotion == true,
                _saveData?.settings?.highContrast == true, backdrop, rect);
            if (_majorIncident.Kind == MajorIncidentKind.Eclipse) ApplyIncidentEnvironment(_incidentPresentation.EnvironmentExposure);
        }

        private void ApplyIncidentEnvironment(float exposure)
        {
            if (exposure >= .999f) return;
            TintIncidentSprite(_backdropView, exposure);TintIncidentSprite(_arenaBakedDetailView, exposure);
            TintIncidentSprite(_arenaCurrentGlowView, exposure);TintIncidentSprite(_arenaLandmarkBodyView, exposure);
            TintIncidentSprites(_arenaMoteViews, exposure);TintIncidentSprites(_arenaStarViews, exposure);
            TintIncidentSprites(_arenaRockViews, exposure);TintIncidentSprites(_arenaRockPlaneViews, exposure);
            TintIncidentSprites(_arenaFilamentPlateViews, exposure);TintIncidentSprites(_arenaRingDebrisViews, exposure);
            TintIncidentLines(_arenaNearFilamentInnerViews, exposure);TintIncidentLines(_arenaRockRimViews, exposure);
            TintIncidentLines(_arenaStellarRimViews, exposure);TintIncidentLines(_arenaLandmarkViews, exposure);
            TintIncidentLines(_arenaLandmarkRimViews, exposure);TintIncidentLines(_arenaOrbitViews, exposure);
            TintIncidentLines(_arenaOrbitFractureViews, exposure);
            for (var i = 0; i < _arenaNearFilamentMaterials.Length; i++)
            {
                var material = _arenaNearFilamentMaterials[i];if (material == null || _incidentMaterialTintCount >= _incidentMaterialTints.Length) continue;
                var gas = material.GetColor(IncidentGasColorId);var core = material.GetColor(IncidentCoreColorId);
                _incidentMaterialTints[_incidentMaterialTintCount++] = new IncidentMaterialTint { Material = material, Gas = gas, Core = core };
                material.SetColor(IncidentGasColorId, ExposedColor(gas, exposure));material.SetColor(IncidentCoreColorId, ExposedColor(core, exposure));
            }
        }

        private static Color ExposedColor(Color color, float exposure) => new Color(color.r * exposure, color.g * exposure, color.b * exposure, color.a);
        private void TintIncidentSprites(SpriteRenderer[] views,float exposure) { for(var i=0;i<views.Length;i++)TintIncidentSprite(views[i],exposure); }
        private void TintIncidentSprite(SpriteRenderer view,float exposure)
        {
            if(view==null||!view.enabled||_incidentSpriteTintCount>=_incidentSpriteTints.Length)return;
            _incidentSpriteTints[_incidentSpriteTintCount++]=new IncidentSpriteTint{View=view,Color=view.color};view.color=ExposedColor(view.color,exposure);
        }
        private void TintIncidentLines(LineRenderer[] views,float exposure)
        {
            for(var i=0;i<views.Length;i++){
                var view=views[i];if(view==null||!view.enabled||_incidentLineTintCount>=_incidentLineTints.Length)continue;
                _incidentLineTints[_incidentLineTintCount++]=new IncidentLineTint{View=view,Start=view.startColor,End=view.endColor};
                view.startColor=ExposedColor(view.startColor,exposure);view.endColor=ExposedColor(view.endColor,exposure);
            }
        }
        private void RestoreIncidentEnvironment()
        {
            for(var i=0;i<_incidentSpriteTintCount;i++){var item=_incidentSpriteTints[i];if(item.View!=null)item.View.color=item.Color;_incidentSpriteTints[i]=default;}
            for(var i=0;i<_incidentLineTintCount;i++){var item=_incidentLineTints[i];if(item.View!=null){item.View.startColor=item.Start;item.View.endColor=item.End;}_incidentLineTints[i]=default;}
            for(var i=0;i<_incidentMaterialTintCount;i++){var item=_incidentMaterialTints[i];if(item.Material!=null){item.Material.SetColor(IncidentGasColorId,item.Gas);item.Material.SetColor(IncidentCoreColorId,item.Core);}_incidentMaterialTints[i]=default;}
            _incidentSpriteTintCount=_incidentLineTintCount=_incidentMaterialTintCount=0;
        }
    }
}
