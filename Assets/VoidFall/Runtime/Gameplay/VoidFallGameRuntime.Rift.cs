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

        private Sprite[] _riftPortalFrames;
        private SpriteRenderer _riftPortal;
        private float _riftPortalFrameTimer;
        private int _riftPortalFrameIndex;

        /// <summary>Called from run start; the prototype graph is fixed (§3).</summary>
        private void EnsureVoidRouteForRun()
        {
            _voidRoute = VoidRouteRun.PrototypeGraph();
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
            HideRiftPortal();
        }

        /// <summary>Edge fired once per Void when its objective completes.</summary>
        private void OnVoidObjectiveCompleted()
        {
            if (_voidRoute == null) return;
            if (!_voidRoute.NotifyVoidCompleted(_voidRoute.CurrentVoidId)) return;
            var completedName = _voidRoute.Node(_voidRoute.CurrentVoidId).DisplayName.ToUpperInvariant();
            _objectiveLine = completedName + " COMPLETE";
            _lastObjectiveLine = null;
            ShowArenaToast(completedName + " COMPLETE", 3.2f, ToastKind.Reward);
            _completedVoids++;
            if (_voidRoute.NodesInState(RouteNodeState.Available).Count == 0)
            {
                // Terminal Voids (or mid-flow states with no pending choice)
                // complete without a rift; escape handling arrives with the
                // Final Void.
                return;
            }
            _voidCompletionDelayRemaining = VoidProgressionRules.PostBossDelaySeconds(
                _runSeed,
                _completedVoids - 1);
            _voidCompletionPending = true;
        }

        private void StepVoidCompletionDelay(float dt)
        {
            if (!_voidCompletionPending || _riftTransitionActive) return;
            _voidCompletionDelayRemaining -= Mathf.Max(0f, dt);
            if (_voidCompletionDelayRemaining > 0f) return;

            _voidCompletionPending = false;
            if (_rouletteChestActive)
            {
                _openRouteAfterRoulette = true;
                CollectRouletteChest();
                if (_rouletteActive) return;
                _openRouteAfterRoulette = false;
            }
            OpenCompletedVoidRift();
        }

        private void OpenCompletedVoidRift()
        {
            ShowArenaToast("THE RIFT OPENS", 3f);
            SpawnRingWave(
                _gameSim.Player.Position, 30f, 640f, 0.95f,
                new Color(0.133f, 0.827f, 0.933f, 0.95f));
            BurstFx(
                _gameSim.Player.Position, SourceDotColor("cyan"),
                16, 300, 0.55f, 0.9f);
            ShowRiftPortal();
            OpenRouteSelection();
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

            var doubleBoss = VoidProgressionRules.ShouldSpawnDoubleBoss(_runSeed, _completedVoids);
            var first = DirectorRules.BossEncounter(_runSeed, _bossSequence++);
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

        private void OpenRouteSelection()
        {
            var cards = _routeController.BuildCards(_voidRoute);
            if (cards.Count == 0) return;
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
                default: return ArenaId.Void;
            }
        }

        private void ShowRiftPortal()
        {
            if (_riftPortalFrames == null || _riftPortalFrames.Length == 0)
            {
                // The frames import as plain textures (the editor's sprite
                // slicing fragmented them); full-frame Sprites are created
                // here once, sized directly in world units.
                var textures = Resources.LoadAll<Texture2D>(RiftPortalResourcePath);
                if (textures == null || textures.Length == 0)
                {
                    _riftPortalFrames = new Sprite[0];
                    return;
                }
                _riftPortalFrames = new Sprite[textures.Length];
                for (var i = 0; i < textures.Length; i++)
                {
                    var texture = textures[i];
                    _riftPortalFrames[i] = Sprite.Create(
                        texture,
                        new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f),
                        Mathf.Max(1f, texture.height / RiftPortalWorldSize));
                }
            }
            if (_riftPortalFrames.Length == 0) return;

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
