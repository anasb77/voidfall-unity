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
        private const float IncidentRadius = MajorIncidentRules.BlackHoleRadius;
        private float _incidentPullSampleSeconds, _incidentPullDistance, _incidentPullPeakSpeed;
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
            FlushBlackHolePullObservation();
            if (_majorIncident.Kind != MajorIncidentKind.None)
                RecordRunHistory("incident_stopped", _majorIncident.Kind.ToString(), _majorIncident.Phase.ToString());
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
            if (ActiveBosses() > 0 || !IncidentArenaEligible() || DirectorSurvivalSecondsRemaining <= MajorIncidentRules.BossLeadSeconds)
            {
                if (_majorIncident.Kind != MajorIncidentKind.None) StopMajorIncident();
                _nextIncidentOpportunity = Mathf.Max(_nextIncidentOpportunity, _time + 30);
                return;
            }
            if (_majorIncident.Kind != MajorIncidentKind.None)
            {
                var kind = _majorIncident.Kind;
                var phase = _majorIncident.Phase;
                _majorIncident.Step(dt);
                if (_majorIncident.Phase != phase)
                    RecordRunHistory("incident_phase", kind.ToString(), _majorIncident.Phase.ToString(),
                        instanceId: _incidentSequence, amount: (float)_majorIncident.Strength,
                        durationSeconds: _majorIncident.Kind == MajorIncidentKind.None
                            ? (float)MajorIncidentRules.TotalDuration(kind) : (float)_majorIncident.Elapsed);
                if (kind == MajorIncidentKind.DestroyerRaid)
                {
                    if (!_incidentRaidStarted && _majorIncident.Phase == MajorIncidentPhase.Active)
                    { StartDestroyerRaid(_incidentCenter); _incidentRaidStarted = true; }
                    StepDestroyerRaid(dt, _majorIncident.Phase == MajorIncidentPhase.Release);
                }
                if (_majorIncident.Kind == MajorIncidentKind.None)
                {
                    StopMajorIncident();
                    _nextIncidentOpportunity = _time + (UsesSustainedDirector ? 100 : 240 + (_runSeed + (uint)_incidentSequence) % 61);
                    _spawnTimer = .65f;
                }
                return;
            }
            if (_stressScenario != null) return;
            if (UsesSustainedDirector) { TrySustainedIncidentOpportunity(); return; }
            if (_incidentCount >= 4 || _time < _nextIncidentOpportunity) return;
            _incidentSequence++;
            _nextIncidentOpportunity = _time + 30 + (_runSeed ^ (uint)(_incidentSequence * 7919)) % 21;
            var attentionBlocked = UsesSustainedDirector
                ? _pressureReliefTimer > 0 || !SustainedIncidentOpeningSafe()
                : ActiveDemandingEnemies() > 0 || !DirectorOpeningSafe();
            if (_encounter.Phase != CombatEncounterPhase.Flow || LocalDirectorSurvivalSeconds < 45 ||
                attentionBlocked || ActiveEnemies() < 8 || ActiveEnemies() > DirectorBodyLimit() - 5)
            {
                RecordRunHistory("incident_deferred", reason: "pacing_population_or_unsafe_opening", instanceId: _incidentSequence);
                return;
            }
            // Native meteors already spend attention; do not stack a generic incident onto a storm.
            if (_arenaId == ArenaId.RedNebula && ActiveMeteorsCountForIncident() > 2)
            {
                RecordRunHistory("incident_deferred", reason: "meteor_attention", instanceId: _incidentSequence);
                return;
            }
            var kindChoice = (MajorIncidentKind)(1 + ((_runSeed ^ (uint)(_incidentSequence * 104729)) % 3));
            if (kindChoice == _lastIncidentKind) kindChoice = (MajorIncidentKind)(1 + (int)kindChoice % 3);
            if (!MajorIncidentRules.CanBegin(kindChoice, DirectorSurvivalSecondsRemaining, false, false, false))
            {
                RecordRunHistory("incident_deferred", kindChoice.ToString(), "insufficient_remaining_time", instanceId: _incidentSequence);
                return;
            }
            if (kindChoice == MajorIncidentKind.DestroyerRaid && ActiveEnemyThreat() > DirectorBodyLimit() * 1.55f - 45)
            {
                RecordRunHistory("incident_deferred", kindChoice.ToString(), "threat_budget", instanceId: _incidentSequence);
                return;
            }
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
                // Lead current movement so continuing straight crosses the warned force zone.
                // Once selected the center stays locked, preserving the option to juke away.
                var moving = _gameSim.Player.Velocity.sqrMagnitude > 1;
                var direction = moving ? _gameSim.Player.Velocity.normalized : Vector2.right;
                if (!moving)
                {
                    var nearest = float.MaxValue;
                    for (var i = 0; i < _gameSim.Enemies.Length; i++)
                    {
                        var enemy = _gameSim.Enemies[i];if (!enemy.Active || enemy.Elite) continue;
                        var delta = enemy.Position - _gameSim.Player.Position;
                        if (delta.sqrMagnitude > 180 * 180 && delta.sqrMagnitude < nearest)
                        { nearest = delta.sqrMagnitude;direction = delta.normalized; }
                    }
                }
                _incidentCenter += direction * MajorIncidentRules.BlackHoleCenterOffset;
            }
            _incidentRaidStarted = false;
            _majorIncident.Begin(kind);
            _incidentPullSampleSeconds = _incidentPullDistance = _incidentPullPeakSpeed = 0;
            RecordRunHistory("incident_selected", kind.ToString(), "eligible", instanceId: _incidentSequence);
            RecordRunHistory("incident_phase", kind.ToString(), MajorIncidentPhase.Warning.ToString(),
                instanceId: _incidentSequence);
            _incidentCount++;
            _lastIncidentKind = kind;
            _spawnTimer = .65f;
            ShowArenaToast(kind == MajorIncidentKind.BlackHole ? "BLACK HOLE · MOVE BEYOND THE RING" :
                kind == MajorIncidentKind.DestroyerRaid ? "THE DESTROYERS HAVE ARRIVED" : "ECLIPSE",
                (float)MajorIncidentRules.WarningDuration(kind), ToastKind.Danger);
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
            var distance = delta.magnitude;
            var pull = (float)MajorIncidentRules.BlackHolePullScale(distance, IncidentRadius, _majorIncident.Strength);
            var speed = (float)ContentCatalog.Operative.MoveSpeed * MajorIncidentRules.BlackHolePlayerPullFraction * pull;
            var displacement = Mathf.Min(distance, speed * dt);
            if (distance > .0001f) _gameSim.Player.Position += delta / distance * displacement;
            if (_runExportActive)
            {
                _incidentPullSampleSeconds += dt; _incidentPullDistance += displacement;
                _incidentPullPeakSpeed = Mathf.Max(_incidentPullPeakSpeed, speed);
                if (_incidentPullSampleSeconds >= 1f) FlushBlackHolePullObservation();
            }
        }

        private void FlushBlackHolePullObservation()
        {
            if (_incidentPullSampleSeconds <= 0) return;
            RecordRunHistory("black_hole_pull", MajorIncidentKind.BlackHole.ToString(), _majorIncident.Phase.ToString(),
                instanceId: _incidentSequence, amount: _incidentPullDistance, durationSeconds: _incidentPullSampleSeconds,
                detail: string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "distance={0:F2};radius={1:F2};peakPullSpeed={2:F2};centerX={3:F2};centerY={4:F2}",
                    Vector2.Distance(_gameSim.Player.Position, _incidentCenter), IncidentRadius, _incidentPullPeakSpeed,
                    _incidentCenter.x, _incidentCenter.y));
            _incidentPullSampleSeconds = _incidentPullDistance = _incidentPullPeakSpeed = 0;
        }

        private void ApplyMajorIncidentEnemyDisplacement(ref EnemyState enemy, float dt)
        {
            if (!enemy.Active || enemy.Elite || !BlackHoleAttractionActive() || dt <= 0) return;
            var delta = _incidentCenter - enemy.Position;
            var distance = delta.magnitude;if (distance <= .0001f) return;
            var pull = (float)MajorIncidentRules.BlackHolePullScale(distance, IncidentRadius, _majorIncident.Strength);
            enemy.Position += delta / distance * Mathf.Min(distance, (float)ContentCatalog.Operative.MoveSpeed * pull * dt);
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
