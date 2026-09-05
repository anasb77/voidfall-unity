using System;
using UnityEngine;
using UnityEngine.UI;
using VoidFall.Core;
using VoidFall.UI;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private enum JourneyStage { Combat, Rewards, Junction, Travel, Complete }
        private JourneyStage _journeyStage;
        private bool _routeMapOpen;
        private bool _pausedBeforeRouteMap;
        private string _plannedRouteId;
        private bool _runVictory;
        private bool _returnToMenuAfterRun;
        private bool _journeyLoadFailed;
        private GameObject _junctionRoot;
        private Canvas _junctionCanvas;
        private Sprite _junctionFloorSprite;
        private readonly SpriteRenderer[] _junctionPortals = new SpriteRenderer[2];
        private readonly Text[] _junctionLabels = new Text[2];
        private string[] _junctionDestinations = Array.Empty<string>();
        private float _junctionAge;
        private readonly SpriteRenderer[] _junctionRims = new SpriteRenderer[4];

        public string JourneyStatus => _journeyStage.ToString();
        public string CurrentVoidId => _voidRoute?.CurrentVoidId;
        private bool JourneyStopsCombat => _stressScenario == null && _journeyStage != JourneyStage.Combat;

        private void ResetJourney()
        {
            _journeyStage = JourneyStage.Combat;
            _routeMapOpen = false;
            _pausedBeforeRouteMap = false;
            _plannedRouteId = null;
            _runVictory = false;
            _returnToMenuAfterRun = false;
            _journeyLoadFailed = false;
            HideJunction();
        }

        private void ToggleRouteMap()
        {
            if (_routeMapOpen) { CloseRouteMap(); return; }
            if (_mainMenuBrowsing || _gameOver || _voidRoute == null || _ui?.RouteMap == null ||
                _rouletteActive || _prizeRevealActive || _revivePending || _levelUpActive ||
                _levelUpTimer >= 0 || _riftTransitionActive || _menuPage != MenuPage.None) return;
            _pausedBeforeRouteMap = _paused;
            _routeMapOpen = true;
            _paused = true;
            _ui.RouteMap.Show(_voidRoute, _plannedRouteId, PlanRoute, CloseRouteMap);
            SyncUiScreen();
        }

        private void PlanRoute(string id)
        {
            // Planning never mutates the route. Only a physical portal commits it.
            _plannedRouteId = id;
        }

        private void CloseRouteMap()
        {
            if (!_routeMapOpen) return;
            _routeMapOpen = false;
            _paused = _pausedBeforeRouteMap || _applicationInactive;
            SyncUiScreen();
        }

        private void ClearCombatForJourney()
        {
            DestroyEnemiesForVoidTransition();
            ClearTransitionProjectiles();
            ClearMeteors();
            ClearNebulaStrikes();
            ResetHydraEncounterState();
            ResetMonochromeEncounterState();
            _directorActive = false;
            _directorWarned = false;
            _bossWarned = false;
            _nextBossTime = float.PositiveInfinity;
        }

        private void CollectJourneyPickups()
        {
            if (_gameSim.Player.Health <= 0) return;
            for (var index = 0; index < _gameSim.Pickups.Length; index++)
            {
                if (!_gameSim.Pickups[index].Active) continue;
                var pickup = _gameSim.Pickups[index];
                pickup.Position = _gameSim.Player.Position;
                _gameSim.Pickups[index] = pickup;
            }
            UpdatePickups(0f);
        }

        private void UpdateJourneyFlow(float deltaTime)
        {
            if (_mainMenuBrowsing || _stressScenario != null) return;
            var dt = Mathf.Clamp(deltaTime, 0f, 0.1f);
            if (_returnToMenuAfterRun)
            {
                if (_runSaved) ReturnToMenuAfterResult();
                return;
            }
            if (_journeyStage == JourneyStage.Combat) return;
            if (_gameSim.Player.Health <= 0)
            {
                if (_revivePending || _gameOver) return;
                _gameSim.Player.DyingTimer = Mathf.Max(0, _gameSim.Player.DyingTimer - dt);
                if (_gameSim.Player.DyingTimer > 0) return;
                if (_revivesRemaining > 0)
                {
                    _revivePending = true;
                    _paused = true;
                    _ui?.Revive?.Show(_revivesRemaining);
                }
                else EndRun();
                return;
            }
            if (_paused || _routeMapOpen || _rouletteActive || _prizeRevealActive || _revivePending) return;
            if (_journeyStage == JourneyStage.Rewards)
            {
                MovePlayer(dt);
                CollectJourneyPickups();
                StepVoidCompletionDelay(dt);
            }
            else if (_journeyStage == JourneyStage.Junction)
            {
                _junctionAge += dt;
                MovePlayer(dt);
                var position = _gameSim.Player.Position;
                position.x = Mathf.Clamp(position.x, -490f, 490f);
                position.y = Mathf.Clamp(position.y, -235f, 235f);
                _gameSim.Player.Position = position;
                _cameraFollowPosition = Vector2.zero;
                CollectJourneyPickups();
                for (var index = 0; index < _junctionDestinations.Length; index++)
                {
                    if (_junctionAge >= 0.5f && Vector2.Distance(position, _junctionPortals[index].transform.position) < 48f)
                    {
                        OnRouteVoidChosen(_junctionDestinations[index]);
                        break;
                    }
                }
            }
            else if (_journeyStage == JourneyStage.Travel)
            {
                StepRiftTransition(dt);
                if (!_riftTransitionActive)
                {
                    _journeyStage = JourneyStage.Combat;
                    _gameSim.Player.Iframes = Mathf.Max(_gameSim.Player.Iframes, 1.5f);
                    Debug.Log($"VOIDFLOW travel-complete void={CurrentVoidId} t={_time:F1}");
                }
            }
        }

        private void BeginPortalJunction()
        {
            var available = _voidRoute.NodesInState(RouteNodeState.Available);
            if (available.Count == 0) { FinishJourney(); return; }
            if (available.Count == 1) { OnRouteVoidChosen(available[0]); return; }
            _journeyStage = JourneyStage.Junction;
            _routeSelectOpen = false;
            _junctionDestinations = available.ToArray();
            _junctionAge = 0;
            ClearCombatForJourney();
            // The outgoing arena is finished: corpses may no longer occupy the safe room.
            for (var index = 0; index < _gameSim.Bosses.Length; index++)
            {
                _gameSim.Bosses[index] = default;
                Hide(_bossViews[index]);
                Hide(_bossTelegraphFillRenderers[index]);
                Hide(_bossTelegraphOutlineViews[index]);
                Hide(_bossShieldFillViews[index]);
            }
            ResetBossOrder();
            // Dropping the order tables does not hide their pooled renderers.
            // Retire the outgoing fight's effects before presenting a clean crossing.
            for (var index = 0; index < _fxSim.SourceParticles.Length; index++)
            {
                _fxSim.SourceParticles[index] = default;
                Hide(_sourceParticleViews[index]);
            }
            for (var index = 0; index < _fxSim.MeteorShards.Length; index++)
            {
                _fxSim.MeteorShards[index] = default;
                Hide(_meteorShardViews[index]);
            }
            for (var index = 0; index < _fxSim.RingWaves.Length; index++)
            {
                _fxSim.RingWaves[index] = default;
                Hide(_ringWaveViews[index]);
                Hide(_ringWaveGlowViews[index]);
                Hide(_ringWaveSpriteViews[index]);
            }
            ResetSourceFxOrder();
            _gameSim.Player.Position = new Vector2(0, -155f);
            _gameSim.Player.Velocity = Vector2.zero;
            _cameraFollowPosition = Vector2.zero;
            _paused = false;
            EnsureJunctionVisuals();
            var variant = (int)((_runSeed ^ (uint)_completedVoids * 0x9e3779b9u) % 3u);
            var accent = variant == 0 ? UITheme.Cyan : variant == 1 ? new Color(0.64f, 0.49f, 1f) : new Color(1f, 0.67f, 0.35f);
            foreach (var rim in _junctionRims) rim.color = new Color(accent.r, accent.g, accent.b, 0.65f);
            _junctionRoot.SetActive(true);
            _junctionCanvas.gameObject.SetActive(true);
            _objectiveLine = "VOID CLEARED  /  WALK INTO A PORTAL  /  TAB: MAP";
            for (var index = 0; index < _junctionPortals.Length; index++)
            {
                var visible = index < _junctionDestinations.Length;
                _junctionPortals[index].enabled = visible;
                _junctionLabels[index].gameObject.SetActive(visible);
                if (!visible) continue;
                var node = _voidRoute.Node(_junctionDestinations[index]);
                var portalX = 250f + variant * 25f;
                _junctionPortals[index].transform.localPosition = new Vector3(index == 0 ? -portalX : portalX, 45f + variant * 25f, 0);
                _junctionLabels[index].text = (node.IsMystery ? "?  UNKNOWN VOID" : node.DisplayName.ToUpperInvariant()) +
                    "\n<size=14>" + node.ThreatLabel + "</size>";
            }
            SyncUiScreen();
            Debug.Log($"VOIDFLOW junction void={CurrentVoidId} exits={string.Join(",", _junctionDestinations)} t={_time:F1}");
        }

        private void EnsureJunctionVisuals()
        {
            if (_junctionRoot != null) return;
            // Reuse the project's sliced portal animation; the room itself is a small utility surface.
            ShowRiftPortal();
            HideRiftPortal();
            _junctionRoot = new GameObject("Void Crossing");
            _junctionRoot.transform.SetParent(_worldRoot, false);
            _junctionFloorSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);
            var floor = new GameObject("Crossing Floor").AddComponent<SpriteRenderer>();
            floor.transform.SetParent(_junctionRoot.transform, false);
            floor.sprite = _junctionFloorSprite;
            floor.color = new Color(0.015f, 0.031f, 0.052f, 1f);
            floor.sortingOrder = 8;
            floor.transform.localScale = new Vector3(1110f, 550f, 1f);
            // A stable layout with seed-derived accent provides variation without changing controls.
            var accent = (_runSeed & 1) == 0 ? new Color(0.15f, 0.8f, 0.95f) : new Color(0.65f, 0.48f, 1f);
            for (var edge = 0; edge < 4; edge++)
            {
                var rim = new GameObject("Crossing Rim " + edge).AddComponent<SpriteRenderer>();
                rim.transform.SetParent(_junctionRoot.transform, false);
                rim.sprite = _junctionFloorSprite;
                rim.color = accent * new Color(1, 1, 1, 0.65f);
                rim.sortingOrder = 9;
                _junctionRims[edge] = rim;
                rim.transform.localPosition = edge < 2 ? new Vector3(0, edge == 0 ? -275f : 275f) : new Vector3(edge == 2 ? -555f : 555f, 0);
                rim.transform.localScale = edge < 2 ? new Vector3(1110, 2, 1) : new Vector3(2, 550, 1);
            }
            _junctionCanvas = UIBuilder.CreateCanvas("Void Crossing Labels", 120);
            _junctionCanvas.transform.SetParent(transform, false);
            for (var index = 0; index < 2; index++)
            {
                var portal = new GameObject("Destination Portal " + index).AddComponent<SpriteRenderer>();
                portal.transform.SetParent(_junctionRoot.transform, false);
                portal.sprite = _riftPortalFrames.Length > 0 ? _riftPortalFrames[0] : _junctionFloorSprite;
                portal.sortingOrder = 400;
                _junctionPortals[index] = portal;
                _junctionLabels[index] = UIBuilder.CreateText(_junctionCanvas.transform, "Destination " + index,
                    string.Empty, 21f, Color.white, TextAnchor.MiddleCenter, true, FontStyle.Bold);
                var rect = _junctionLabels[index].rectTransform;
                rect.anchorMin = rect.anchorMax = Vector2.one * 0.5f;
                rect.sizeDelta = new Vector2(480, 90);
            }
        }

        private void RenderJunction()
        {
            if (_journeyStage != JourneyStage.Junction || _junctionRoot == null) return;
            var frame = _riftPortalFrames.Length == 0 ? 0 : (int)(_junctionAge / RiftPortalFrameSeconds) % _riftPortalFrames.Length;
            var root = (RectTransform)_junctionCanvas.transform;
            for (var index = 0; index < _junctionDestinations.Length; index++)
            {
                if (_riftPortalFrames.Length > 0) _junctionPortals[index].sprite = _riftPortalFrames[frame];
                var point = _camera.WorldToScreenPoint(_junctionPortals[index].transform.position + Vector3.down * 100f);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, point, null, out var local);
                _junctionLabels[index].rectTransform.anchoredPosition = local;
            }
            _junctionCanvas.enabled = !_routeMapOpen;
        }

        private void HideJunction()
        {
            if (_junctionRoot != null) _junctionRoot.SetActive(false);
            if (_junctionCanvas != null) _junctionCanvas.gameObject.SetActive(false);
            _junctionDestinations = Array.Empty<string>();
        }

        private void FinishJourney()
        {
            if (_gameOver || _returnToMenuAfterRun) return;
            _journeyStage = JourneyStage.Complete;
            _runVictory = true;
            _routeMapOpen = false;
            HideJunction();
            EndRun();
        }

        private void RetryJourneyArenaLoad()
        {
            if (!_journeyLoadFailed || _riftTransitionVoidId == null) return;
            var incoming = ArenaIdForVoidId(_riftTransitionVoidId);
            _arenaResidency?.Release(ArenaPackageFor(incoming));
            BeginArenaPackageLoad(incoming);
            _journeyLoadFailed = false;
        }

        private void ReturnToMenuAfterResult()
        {
            if (!_runSaved) return;
            var notice = _runVictory ? "ESCAPED — " + _completedVoids + " Voids cleared. Progress saved." : "Run ended. Progress saved.";
            Debug.Log($"VOIDFLOW run-end victory={_runVictory} voids={_completedVoids} t={_time:F1} saved={_runSaved}");
            EnterMainMenu();
            SetMenuNotice(notice);
        }

        private void DestroyJourneyVisuals()
        {
            if (_junctionFloorSprite != null) Destroy(_junctionFloorSprite);
            if (_riftPortalFrames != null)
                foreach (var frame in _riftPortalFrames) if (frame != null) Destroy(frame);
            _riftPortalFrames = null;
        }
    }
}
