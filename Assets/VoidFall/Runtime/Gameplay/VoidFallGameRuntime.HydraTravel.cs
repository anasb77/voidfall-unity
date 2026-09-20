using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private bool _hydraPhaseTransition;

        // Hydra II retains the original arena plate, geometry and boss camera.
        // Keep Hydra I's base-only presentation through collapse until the swap.
        private bool HydraSurvivalPresentationActive => CurrentVoidIsHydra &&
            !_hydraBossSpawnedForVoid &&
            ((_objectives?.Objective is MultiPhaseObjective phases && phases.PhaseIndex == 0) ||
             (_hydraPhaseTransition && !_riftTransitionSwapped));

        private float HydraSurvivalGlyphOffset => 0f;

        private void BeginHydraPhaseTransition()
        {
            if (_hydraBossSpawnedForVoid || _hydraPhaseTransition || _riftTransitionActive) return;
            _hydraPhaseTransition = true;
            _riftTransitionActive = true;
            _riftTransitionSwapped = false;
            _riftTransitionVoidId = _voidRoute.CurrentVoidId;
            _journeyStage = JourneyStage.Travel;
            _routeMapOpen = false;
            CancelEncounterDirector();
            StopMajorIncident();
            _gameSim.Player.Velocity = Vector2.zero;
            _arenaTransitionState = new ArenaTransitionState(
                Mathf.Max(0, _completedVoids), _time, ArenaPhase.Collapse,
                RiftCollapseSeconds, ArenaId.Hydra);
            BeginArenaPackageLoad(ArenaId.Hydra);
            HideRiftPortal();
            _objectiveLine = "HYDRA I COMPLETE — ENTERING HYDRA II";
            _lastObjectiveLine = null;
            _arenaFlash = Mathf.Max(_arenaFlash, 0.62f);
            _cyanFlash = Mathf.Max(_cyanFlash, 0.48f);
            SpawnRingWave(_gameSim.Player.Position, 26f, 760f, 1.05f,
                new Color(0.133f, 0.827f, 0.933f, 0.95f));
            _audio?.Play(ProceduralAudio.Cue.BossCharge, 0.96f);
            AddCameraShake(0.5f);
            RecordRunHistory("hydra_phase", "hydra-i", reason: "survival_complete",
                sourceId: "hydra-survival", durationSeconds: (float)VoidProgressionRules.SurvivalSeconds);
        }

        private void CommitHydraPhaseTransitionSwap()
        {
            // No route selection, new objective, director clock or pressure reset:
            // this is the boss half of the very same Hydra visit.
            _gameSim.Player.Position = Vector2.zero;
            _gameSim.Player.Velocity = Vector2.zero;
            _cameraFollowPosition = Vector2.zero;
            ClearTransitionProjectiles();
            BeginHydraBossEncounter();
            _arenaFlash = Mathf.Max(_arenaFlash, 0.85f);
            _cyanFlash = Mathf.Max(_cyanFlash, 0.72f);
            SpawnRingWave(_gameSim.Player.Position, 18f, 900f, 1.15f,
                new Color(0.55f, 0.95f, 1f, 0.95f));
            BurstFx(_gameSim.Player.Position, SourceDotColor("cyan"), 34, 440f, 0.72f, 1f);
            BurstFx(_gameSim.Player.Position, SourceDotColor("white"), 18, 320f, 0.56f, 0.9f);
            AddCameraShake(0.78f);
            _audio?.Play(ProceduralAudio.Cue.BossDeath, 0.86f);
            _objectiveLine = "HYDRA II | HYDRA PRIME — ENGAGED";
            _lastObjectiveLine = null;
            RecordRunHistory("hydra_phase", "hydra-ii", reason: "teleport_swap",
                sourceId: "hydra-i", detail: "Original Hydra Prime arena and encounter");
        }
    }
}
