using System;
using UnityEngine;
using VoidFall.Core;
using VoidFall.UI;

namespace VoidFall.Runtime
{
    /// <summary>
    /// The rift between Voids (spec §23): when the current Void's objective
    /// completes, a portal opens and the route choice appears; the chosen
    /// branch performs the transition ceremony and switches the arena.
    ///
    /// Everything here is player-triggered presentation on top of the live
    /// run - none of it executes in the stress scenarios, so the golden
    /// master is untouched. Route/locking rules stay in VoidRouteRun; this
    /// partial only orchestrates.
    /// </summary>
    public sealed partial class VoidFallGameRuntime
    {
        private const string RiftPortalResourcePath = "VoidFall/Portals";
        private const float RiftPortalFrameSeconds = 0.1f;
        private const float RiftPortalWorldSize = 120f;
        private const float RiftCollapseSeconds = 0.72f;
        private const float RiftSettleSeconds = 1.1f;
        // Each portal texture is a sheet of cells (columns x rows), not one
        // frame. Sheets a-e are color variants; the animation loops the
        // first sheet's cells in row-major order.
        private const int RiftPortalSheetColumns = 5;
        private const int RiftPortalSheetRows = 3;

        private VoidRouteRun _voidRoute;
        private RouteSelectController _routeController;
        private bool _objectivesCompletionHandled;
        private int _completedVoids;
        private bool _voidBossEncounterSpawned;
        private bool _voidCompletionPending;
        private float _voidCompletionDelayRemaining;
        private bool _riftTransitionActive;
        private bool _riftTransitionSwapped;
        private string _riftTransitionVoidId;
        private bool _openRouteAfterRoulette;
        // True while the route-choice overlay owns the run: pause toggles
        // must not dismiss it (dismissing strands the run with a completed
        // objective, no boss scheduling, and no way forward).
        private bool _routeSelectOpen;
        // Single-destination voids teleport directly with no portal or cards.
        private string _riftAutoVoidId;
        // Set when the objective completes; the safety net re-fires the rift
        // if nothing owns the run 45s later. -1 while a Void is in progress.
        private float _objectiveCompleteAt = -1f;

        private Sprite[] _riftPortalFrames;
        private SpriteRenderer _riftPortal;
        private float _riftPortalFrameTimer;
        private int _riftPortalFrameIndex;

        /// <summary>Called from run start; the prototype graph is fixed (§3).</summary>
        private void EnsureVoidRouteForRun()
        {
            ResetJourney();
            _voidRoute = PlayableVoidRoutes.Create(_runSeed);
            _routeController = new RouteSelectController();
            _objectivesCompletionHandled = false;
            _completedVoids = 0;
            _voidBossEncounterSpawned = false;
            _voidCompletionPending = false;
            _voidCompletionDelayRemaining = 0f;
            _riftTransitionActive = false;
            _riftTransitionSwapped = false;
            _riftTransitionVoidId = null;
            _openRouteAfterRoulette = false;
            _routeSelectOpen = false;
            _riftAutoVoidId = null;
            _objectiveCompleteAt = -1f;
            HideRiftPortal();
        }

        /// <summary>Edge fired once per Void when its objective completes.</summary>
        private void OnVoidObjectiveCompleted()
        {
            if (_voidRoute == null || _gameOver || _stressScenario != null) return;
            if (!_voidRoute.NotifyVoidCompleted(_voidRoute.CurrentVoidId)) return;
            var completedName = _voidRoute.Node(_voidRoute.CurrentVoidId).DisplayName.ToUpperInvariant();
            Debug.Log($"VOIDFLOW objective-complete void={_voidRoute.CurrentVoidId} t={_time:F1}");
            _objectiveLine = completedName + " COMPLETE";
            _lastObjectiveLine = null;
            ShowArenaToast(completedName + " COMPLETE", 3.2f, ToastKind.Reward);
            _completedVoids++;
            _journeyStage = JourneyStage.Rewards;
            _routeMapOpen = false;
            ClearCombatForJourney();
            CollectJourneyPickups();
            var available = _voidRoute.NodesInState(RouteNodeState.Available);
            // One exit: teleport straight there when the delay expires. No
            // portal, no cards - the portal is the multi-destination choice.
            _riftAutoVoidId = available.Count == 1 ? available[0] : null;
            _voidCompletionDelayRemaining = 1.2f;
            _voidCompletionPending = true;
        }

        private void StepVoidCompletionDelay(float dt)
        {
            if (!_voidCompletionPending || _riftTransitionActive) return;
            if (_gameOver || _revivePending || _rouletteActive || _prizeRevealActive ||
                _routeMapOpen || _levelUpActive || _paused || _menuPage != MenuPage.None) return;
            _voidCompletionDelayRemaining -= Mathf.Max(0f, dt);
            if (_voidCompletionDelayRemaining > 0f) return;

            if (_rouletteChestActive)
            {
                _openRouteAfterRoulette = true;
                // The relic remains at the defeated boss. The safe reward phase
                // allows the player to approach it; a timer never claims it remotely.
                return;
            }
            _voidCompletionPending = false;
            // A simultaneous boss/player death postpones pickup collection until revive.
            // Sweep before draining upgrades, including on the terminal node.
            CollectJourneyPickups();
            AdvanceRunLevelUps(Mathf.Max(0f, dt));
            if (_levelUpActive || _levelUpTimer >= 0)
            {
                _voidCompletionPending = true;
                return;
            }
            OpenCompletedVoidRift();
        }

        private void OpenCompletedVoidRift()
        {
            if (_riftTransitionActive || _journeyStage == JourneyStage.Junction) return;
            if (_voidRoute.HasEscaped) { FinishJourney(); return; }
            if (!string.IsNullOrEmpty(_riftAutoVoidId))
            {
                Debug.Log($"VOIDFLOW auto-teleport to={_riftAutoVoidId} t={_time:F1}");
                // Automatic travel must commit the route choice just like a
                // card click, before the swap initializes its objective.
                OnRouteVoidChosen(_riftAutoVoidId);
                return;
            }
            Debug.Log($"VOIDFLOW rift-open t={_time:F1}");
            ShowArenaToast("THE RIFT OPENS", 3f);
            SpawnRingWave(
                _gameSim.Player.Position, 30f, 640f, 0.95f,
                new Color(0.133f, 0.827f, 0.933f, 0.95f));
            BurstFx(
                _gameSim.Player.Position, SourceDotColor("cyan"),
                16, 300, 0.55f, 0.9f);
            BeginPortalJunction();
        }

        private void SyncVoidBossEncounterWithObjective()
        {
            if (_voidBossEncounterSpawned || _objectives == null || _objectives.IsComplete) return;
            if (!(_objectives.Objective is MultiPhaseObjective phases) || phases.PhaseIndex < 1) return;

            _voidBossEncounterSpawned = true;
            var voidId = _voidRoute?.CurrentVoidId ?? ArenaCatalogRules.StableId(_arenaId);
            if (voidId == "hydra")
            {
                BeginHydraBossEncounter();
                return;
            }
            if (voidId == "monochrome-court")
            {
                BeginMonochromeBossEncounter();
                return;
            }
            if (voidId == "null-city")
            {
                BeginNullCityBossEncounter();
                return;
            }

            var doubleBoss = VoidProgressionRules.ShouldSpawnDoubleBoss(_runSeed, _completedVoids);
            var first = DirectorRules.BossEncounter(_runSeed, _bossSequence++);
            Debug.Log($"VOIDFLOW encounter void={voidId} double={doubleBoss} first={first.Id} t={_time:F1}");
            SpawnBoss(first.Id, first.HealthScale, first.DamageScale, first.Cycle);
            string secondName = null;
            if (doubleBoss)
            {
                var second = DirectorRules.BossEncounter(_runSeed, _bossSequence++);
                SpawnBoss(second.Id, second.HealthScale, second.DamageScale, second.Cycle);
                secondName = FindBoss(second.Id)?.Name ?? second.Id;
            }
            if (ActiveBosses() == 0)
            {
                // Boss pool was full: retry next tick instead of softlocking
                // the encounter objective, which needs at least one spawn.
                _voidBossEncounterSpawned = false;
                return;
            }

            var firstName = FindBoss(first.Id)?.Name ?? first.Id;
            if (doubleBoss)
            {
                ShowArenaToast(firstName + " + " + secondName + " ENTER THE VOID", 3f, ToastKind.Danger);
            }
            else
            {
                ShowArenaToast(firstName + " ENTERS THE VOID", 3f, ToastKind.Danger);
            }
            _audio?.Play(ProceduralAudio.Cue.Boss, 0.9f);
        }

        /// <summary>Last-resort re-opener: if the objective has been complete
        /// for 60s but no route is open, no transition runs, and no ceremony
        /// owns the run, force the delay to zero so the normal flow fires.
        /// A stranded run must be impossible.</summary>
        private void StepRiftSafetyNet()
        {
            if (_objectiveCompleteAt < 0 || _objectives == null || !_objectives.IsComplete) return;
            if (_routeSelectOpen || _riftTransitionActive) return;
            if (_rouletteActive || _rouletteChestActive || _openRouteAfterRoulette) return;
            if (_gameOver || _revivePending || _levelUpActive || _paused) return;
            if (_voidRoute == null ||
                _voidRoute.NodesInState(RouteNodeState.Available).Count == 0) return;
            if (_time - _objectiveCompleteAt < 60f) return;
            Debug.Log($"VOIDFLOW safety-force pending={_voidCompletionPending} remaining={_voidCompletionDelayRemaining:F1} t={_time:F1}");
            _objectiveCompleteAt = -1f;
            OpenCompletedVoidRift();
        }

        private void OpenRouteSelection()
        {
            var cards = _routeController.BuildCards(_voidRoute);
            if (cards.Count == 0) return;
            Debug.Log($"VOIDFLOW route-open cards={cards.Count} t={_time:F1}");
            _routeSelectOpen = true;
            _paused = true;
            _ui.SetScreen(UIScreen.RouteSelect);
            _ui.RouteSelect.Show(
                cards,
                _routeController.BuildBanner(_voidRoute),
                _routeController.BuildRouteLine(_voidRoute),
                OnRouteVoidChosen);
        }

        private void OnRouteVoidChosen(string voidId)
        {
            if (!_routeController.Confirm(_voidRoute, voidId, out var notice))
            {
                // The view only offers available cards; this is defense in
                // depth against a stale screen.
                ShowArenaToast(notice, 2f);
                OpenRouteSelection();
                return;
            }
            EnterVoidThroughRift(voidId);
        }

        /// <summary>Starts the playable fold between the selected Voids.</summary>
        private void EnterVoidThroughRift(string voidId)
        {
            if (_riftTransitionActive) return;
            HideJunction();
            _journeyStage = JourneyStage.Travel;
            _routeMapOpen = false;
            _plannedRouteId = null;
            _gameSim.Player.Velocity = Vector2.zero;
            _gameSim.Player.Position = Vector2.zero;
            _cameraFollowPosition = Vector2.zero;
            _telemetry.RecordArenaWarning(_completedVoids - 1,
                ArenaIdName(_arenaId), ArenaIdName(ArenaIdForVoidId(voidId)), (float)_time);
            Debug.Log($"VOIDFLOW choice void={voidId} t={_time:F1}");
            _routeSelectOpen = false;
            _riftAutoVoidId = null;
            var node = _voidRoute.Node(voidId);
            ShowArenaToast("ENTERING " + node.DisplayName.ToUpperInvariant(), 2.5f);
            _arenaFlash = Mathf.Max(_arenaFlash, 0.62f);
            _cyanFlash = Mathf.Max(_cyanFlash, 0.48f);
            SpawnRingWave(
                _gameSim.Player.Position, 26f, 760f, 1.05f,
                new Color(0.133f, 0.827f, 0.933f, 0.95f));
            BurstFx(
                _gameSim.Player.Position, SourceDotColor("cyan"),
                24, 340, 0.62f, 0.92f);
            BurstFx(
                _gameSim.Player.Position, SourceDotColor("white"),
                12, 250, 0.46f, 0.82f);

            _riftTransitionActive = true;
            _riftTransitionSwapped = false;
            _riftTransitionVoidId = voidId;
            var incoming = ArenaIdForVoidId(voidId);
            _arenaTransitionState = new ArenaTransitionState(
                Mathf.Max(0, _completedVoids - 1),
                _time,
                ArenaPhase.Collapse,
                RiftCollapseSeconds,
                incoming);
            BeginArenaPackageLoad(incoming);
            HideRiftPortal();
            _ui.SetScreen(UIScreen.None);
            _paused = false;
            _audio?.Play(ProceduralAudio.Cue.BossCharge, 0.96f);
            AddCameraShake(0.5f);
        }

        private void StepRiftTransition(float dt)
        {
            if (!_riftTransitionActive) return;
            var phase = _arenaTransitionState.Phase;
            var remaining = (float)_arenaTransitionState.PhaseT - Mathf.Max(0f, dt);
            if (phase == ArenaPhase.Collapse)
            {
                if (remaining > 0f)
                {
                    _arenaTransitionState = new ArenaTransitionState(
                        _arenaTransitionState.Index,
                        _arenaTransitionState.DueAt,
                        ArenaPhase.Collapse,
                        remaining,
                        _arenaTransitionState.Incoming);
                    return;
                }

                var incoming = _arenaTransitionState.Incoming ?? _arenaId;
                if (_arenaResidency != null && !TryInstallPreparedArenaPlate(incoming))
                {
                    if (_arenaResidency.Status(ArenaPackageFor(incoming)) == ArenaPackageLoadStatus.Failed && !_journeyLoadFailed)
                    {
                        _journeyLoadFailed = true;
                        _paused = true;
                        _objectiveLine = "ARENA LOAD FAILED — RESUME TO RETRY";
                        Debug.LogWarning("VoidFall arena load failed: " + _riftTransitionVoidId);
                    }
                    else _objectiveLine = "ENTERING VOID — LOADING ARENA";
                    return;
                }
                CommitRiftTransitionSwap();
                _arenaTransitionState = new ArenaTransitionState(
                    _arenaTransitionState.Index,
                    _arenaTransitionState.DueAt,
                    ArenaPhase.Settle,
                    RiftSettleSeconds,
                    _arenaTransitionState.Incoming);
                return;
            }

            if (phase != ArenaPhase.Settle) return;
            if (remaining > 0f)
            {
                _arenaTransitionState = new ArenaTransitionState(
                    _arenaTransitionState.Index,
                    _arenaTransitionState.DueAt,
                    ArenaPhase.Settle,
                    remaining,
                    _arenaTransitionState.Incoming);
                return;
            }

            _riftTransitionActive = false;
            _riftTransitionVoidId = null;
            _arenaTransitionState = new ArenaTransitionState(
                _completedVoids,
                double.PositiveInfinity,
                ArenaPhase.Idle,
                0,
                null);
            _telemetry.RecordArenaComplete(Mathf.Max(0, _completedVoids - 1), (float)_time);
        }

        private void CommitRiftTransitionSwap()
        {
            if (_riftTransitionSwapped || string.IsNullOrEmpty(_riftTransitionVoidId)) return;
            Debug.Log($"VOIDFLOW swap to={_riftTransitionVoidId} t={_time:F1}");
            _riftTransitionSwapped = true;
            DestroyEnemiesForVoidTransition();
            ClearTransitionProjectiles();
            ClearMeteors();

            _arenaId = ArenaIdForVoidId(_riftTransitionVoidId);
            SelectRecipeForCurrentArena();
            PrepareArenaNeighborhood();
            _meteorSpawnTimer = 2.2f;
            _meteorTarget = MeteorRules.MinOrdinaryMeteors;
            ResetDirectorAfterVoidTransition();
            BeginObjectiveForCurrentArena();

            _arenaFlash = Mathf.Max(_arenaFlash, 0.85f);
            _cyanFlash = Mathf.Max(_cyanFlash, 0.72f);
            SpawnRingWave(_gameSim.Player.Position, 18f, 900f, 1.15f,
                new Color(0.55f, 0.95f, 1f, 0.95f));
            BurstFx(_gameSim.Player.Position, SourceDotColor("cyan"), 34, 440f, 0.72f, 1f);
            BurstFx(_gameSim.Player.Position, SourceDotColor("white"), 18, 320f, 0.56f, 0.9f);
            AddCameraShake(0.78f);
            _audio?.Play(ProceduralAudio.Cue.BossDeath, 0.86f);
            var arena = FindArena(ArenaIdName(_arenaId));
            ShowArenaToast(
                arena?.Name ?? ArenaName(_arenaId),
                2.8f,
                ToastKind.Info,
                arena?.Modifier);
            _telemetry.RecordArenaSwap(Mathf.Max(0, _completedVoids - 1), (float)_time);
        }

        private void DestroyEnemiesForVoidTransition()
        {
            for (var index = 0; index < _gameSim.Enemies.Length; index++)
            {
                var enemy = _gameSim.Enemies[index];
                if (enemy.Active)
                {
                    SpawnDeathGhost(enemy, index);
                    BurstFx(enemy.Position, SourceDotColor("cyan"), enemy.Elite ? 5 : 2, 150f, 0.3f, 0.62f);
                }
                _gameSim.Enemies[index] = default;
                HideEnemyPresentationForTransition(index);
            }
            ResetEnemyOrder();
            RebuildEnemyGrid();
        }

        private void HideEnemyPresentationForTransition(int index)
        {
            Hide(_enemyViews[index]);
            Hide(_enemyHarvesterFullViews[index]);
            Hide(_enemyExploderWarningViews[index]);
            Hide(_eliteMarkViews[index]);
            Hide(_eliteChargeLaneViews[index]);
            Hide(_eliteChargeArrowViews[index]);
            Hide(_eliteChargeFillRenderers[index]);
            Hide(_eliteChargeArrowFillRenderers[index]);
            Hide(_enemyTelegraphRingViews[index]);
            Hide(_enemyTelegraphLineViews[index]);
            Hide(_enemyTelegraphSecondaryLineViews[index]);
            Hide(_enemyTelegraphTertiaryLineViews[index]);
            Hide(_enemyHarvesterCapacityRingViews[index]);
            Hide(_enemyTelegraphSiegeDashRenderers[index]);
            Hide(_enemyTelegraphMortarFillViews[index]);
            Hide(_enemyTelegraphExploderFillViews[index]);
            Hide(_enemyTelegraphFillRenderers[index]);
            Hide(_enemyTelegraphArrowFillRenderers[index]);
            Hide(_enemyHealthArcViews[index]);
            Hide(_enemyShieldArcViews[index]);
            Hide(_enemyHealthBackgroundViews[index]);
            Hide(_enemyHealthFillViews[index]);
            for (var segment = 0; segment < ExploderTelegraphSegmentCount; segment++)
                Hide(_enemyTelegraphExploderSegmentViews[index * ExploderTelegraphSegmentCount + segment]);
            for (var segment = 0; segment < MortarTelegraphSegmentCount; segment++)
                Hide(_enemyTelegraphMortarSegmentViews[index * MortarTelegraphSegmentCount + segment]);
        }

        private void ClearTransitionProjectiles()
        {
            for (var index = 0; index < _gameSim.Bullets.Length; index++)
            {
                _gameSim.Bullets[index].Active = false;
                Hide(_bulletViews[index]);
                Hide(_bulletContrastViews[index]);
            }
            ResetBulletOrder();
            for (var index = 0; index < _gameSim.HostileShots.Length; index++)
            {
                _gameSim.HostileShots[index].Active = false;
                Hide(_hostileShotViews[index]);
            }
            ResetHostileShotOrder();
        }

        private void ResetDirectorAfterVoidTransition()
        {
            _directorActive = false;
            _directorWarned = false;
            _directorTimer = 0f;
            _directorRecoveryTimer = 0f;
            _directorSpawnTimer = 0f;
            _directorSpawned = 0;
            while (_nextDirectorEvent.StartsAtSeconds <= _time + 4f)
            {
                _directorIndex++;
                _nextDirectorEvent = DirectorRules.Event(_runSeed, _directorIndex);
            }
            _spawnTimer = 0.35f;
        }

        /// <summary>
        /// Stable Void id to prepared arena identity.
        /// </summary>
        private static ArenaId ArenaIdForVoidId(string voidId)
        {
            switch (voidId)
            {
                case "red-nebula": return ArenaId.RedNebula;
                case "white-sakura": return ArenaId.WhiteSakura;
                case "hydra": return ArenaId.Hydra;
                case "monochrome-court": return ArenaId.MonochromeCourt;
                case "null-city": return ArenaId.NullCity;
                default: return ArenaId.Void;
            }
        }

        private void ShowRiftPortal()
        {
            if (_riftPortalFrames == null || _riftPortalFrames.Length == 0)
            {
                // Each portal texture is a sheet of cells; slice the first
                // sheet into per-cell Sprites so the animation loops frames
                // instead of flashing whole sheets. (Editor sprite slicing
                // fragmented these, so cells are cut here once at runtime.)
                var textures = Resources.LoadAll<Texture2D>(RiftPortalResourcePath);
                _riftPortalFrames = BuildRiftPortalFrames(textures);
                if (_riftPortalFrames.Length == 0) return;
            }

            if (_riftPortal == null)
            {
                var go = new GameObject("Rift Portal");
                go.transform.SetParent(_worldRoot != null ? _worldRoot : transform, false);
                _riftPortal = go.AddComponent<SpriteRenderer>();
                if (_additiveSpriteMaterial != null)
                    _riftPortal.material = _additiveSpriteMaterial;
                _riftPortal.sortingOrder = 400;
            }
            _riftPortalFrameIndex = 0;
            _riftPortalFrameTimer = 0;
            _riftPortal.sprite = _riftPortalFrames[0];
            _riftPortal.transform.localScale = Vector3.one;
            var position = _gameSim.Player.Position + new Vector2(0f, 130f);
            position.x = Mathf.Clamp(position.x, -540f, 540f);
            position.y = Mathf.Clamp(position.y, -280f, 280f);
            _riftPortal.transform.position = position;
            _riftPortal.enabled = true;
        }

        private static Sprite[] BuildRiftPortalFrames(Texture2D[] textures)
        {
            if (textures == null || textures.Length == 0) return new Sprite[0];
            Array.Sort(textures, (a, b) => string.CompareOrdinal(
                a != null ? a.name : null, b != null ? b.name : null));
            var sheet = textures[0];
            var cols = Mathf.Max(1, RiftPortalSheetColumns);
            var rows = Mathf.Max(1, RiftPortalSheetRows);
            if (sheet == null || sheet.width < cols || sheet.height < rows)
                return new Sprite[0];
            var cellWidth = sheet.width / cols;
            var cellHeight = sheet.height / rows;
            // One gate fills RiftPortalWorldSize world units vertically.
            var pixelsPerUnit = Mathf.Max(1f, cellHeight / RiftPortalWorldSize);
            var frames = new Sprite[cols * rows];
            for (var row = 0; row < rows; row++)
            {
                for (var col = 0; col < cols; col++)
                {
                    frames[row * cols + col] = Sprite.Create(
                        sheet,
                        new Rect(
                            col * cellWidth,
                            sheet.height - (row + 1) * cellHeight,
                            cellWidth,
                            cellHeight),
                        new Vector2(0.5f, 0.5f),
                        pixelsPerUnit);
                }
            }
            return frames;
        }

        private void HideRiftPortal()
        {
            if (_riftPortal != null) _riftPortal.enabled = false;
        }

        /// <summary>Cycles the portal frames; render-only, called per frame.</summary>
        private void AnimateRiftPortal(float deltaTime)
        {
            if (_riftPortal == null || !_riftPortal.enabled) return;
            _riftPortalFrameTimer += deltaTime;
            if (_riftPortalFrameTimer < RiftPortalFrameSeconds) return;
            _riftPortalFrameTimer = 0;
            _riftPortalFrameIndex =
                (_riftPortalFrameIndex + 1) % _riftPortalFrames.Length;
            _riftPortal.sprite = _riftPortalFrames[_riftPortalFrameIndex];
        }
    }
}
