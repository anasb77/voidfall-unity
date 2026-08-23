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

        private VoidRouteRun _voidRoute;
        private RouteSelectController _routeController;
        private bool _objectivesCompletionHandled;

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
            HideRiftPortal();
        }

        /// <summary>Edge fired once per Void when its objective completes.</summary>
        private void OnVoidObjectiveCompleted()
        {
            if (_voidRoute == null) return;
            if (_voidRoute.NodesInState(RouteNodeState.Available).Count == 0)
            {
                // Terminal Voids (or mid-flow states with no pending choice)
                // complete without a rift; escape handling arrives with the
                // Final Void.
                return;
            }
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

        /// <summary>The 2-4 second transition (§23), compressed to a beat.</summary>
        private void EnterVoidThroughRift(string voidId)
        {
            var node = _voidRoute.Node(voidId);
            ShowArenaToast("ENTERING " + node.DisplayName.ToUpperInvariant(), 2.5f);
            _arenaFlash = Mathf.Max(_arenaFlash, 0.45f);
            _cyanFlash = Mathf.Max(_cyanFlash, 0.34f);
            SpawnRingWave(
                _gameSim.Player.Position, 26f, 620f, 0.95f,
                new Color(0.133f, 0.827f, 0.933f, 0.95f));
            BurstFx(
                _gameSim.Player.Position, SourceDotColor("cyan"),
                14, 260, 0.5f, 0.8f);

            // Spec §23: dangerous projectiles resolve before the new Void.
            for (var i = 0; i < _gameSim.Bullets.Length; i++)
            {
                _gameSim.Bullets[i].Active = false;
                Hide(_bulletViews[i]);
                Hide(_bulletContrastViews[i]);
            }
            ResetBulletOrder();
            for (var i = 0; i < _gameSim.HostileShots.Length; i++)
            {
                _gameSim.HostileShots[i].Active = false;
                Hide(_hostileShotViews[i]);
            }
            ResetHostileShotOrder();

            var arena = ArenaIdForVoidId(voidId);
            if (_arenaId != arena)
            {
                _arenaId = arena;
                SelectRecipeForCurrentArena();
                PrepareArenaNeighborhood();
                ClearMeteors();
                _meteorSpawnTimer = 2.2f;
                _meteorTarget = MeteorRules.MinOrdinaryMeteors;
            }

            HideRiftPortal();
            BeginObjectiveForCurrentArena();
            _ui.SetScreen(UIScreen.None);
            _paused = false;
        }

        /// <summary>
        /// Void id to arena. Hydra reuses the Abyss arena as a placeholder
        /// until its own arena exists; the mutation genes arrive separately.
        /// </summary>
        private static ArenaId ArenaIdForVoidId(string voidId)
        {
            switch (voidId)
            {
                case "red-nebula": return ArenaId.RedNebula;
                case "white-sakura": return ArenaId.WhiteSakura;
                default: return ArenaId.Void;
            }
        }

        private void ShowRiftPortal()
        {
            if (_riftPortalFrames == null || _riftPortalFrames.Length == 0)
                _riftPortalFrames = Resources.LoadAll<Sprite>(RiftPortalResourcePath);
            if (_riftPortalFrames == null || _riftPortalFrames.Length == 0) return;

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
            // World size from the source frame; the 2400x1440 frames carry
            // generous padding, so the visual lands near RiftPortalWorldSize.
            var frame = _riftPortalFrames[0].rect;
            var scale = RiftPortalWorldSize / Mathf.Max(1f, frame.height);
            _riftPortal.transform.localScale = new Vector3(scale, scale, 1f);
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
