# VoidFall architecture

Last inspected: 2026-09-04, Unity `6000.5.7f1`.

## Boot and lifetime

`Assets/Scenes/SampleScene.unity` is the only build scene. Before it loads,
`ParityFixtureProbe.CreateProbe` creates a persistent root with `FixedGameLoop`
and `VoidFallGameRuntime`. The runtime constructs the camera, world renderers,
pooled views, uGUI hierarchy, audio services, save store, and diagnostics.

The composition is code-authored: there are no gameplay prefabs. This keeps
the scene simple but makes tests and documentation important because most
wiring is not visible in the Inspector.

## Assembly boundaries

```text
VoidFall.Core          deterministic engine-free rules
    ^
    +-- VoidFall.Content
    +-- VoidFall.Persistence

VoidFall.UI ----------> Core + Content + Persistence
VoidFall.Audio -------> Core
VoidFall.Runtime -----> Core + Content + Persistence + UI + Audio
                       Input System + Addressables + URP
```

`VoidFall.Core` and `VoidFall.Content` do not reference UnityEngine.

## Simulation

`FixedGameLoop` drives `VoidFallGameRuntime.Simulate(double)`. `GameSim` owns
combat arrays, order tables, spatial scratch buffers, player state, meteors and
the deterministic combat RNG. `FxSim` owns cosmetic effect state and a separate
FX RNG. Unity view synchronization remains in runtime partials.

The game uses fixed-capacity pools instead of one MonoBehaviour per enemy,
projectile or pickup. This is a deliberate bullet-heaven performance contract.

The 32-seed PlayMode sweep proves repeated runs are bit-stable. The canonical
`productionMax` run is pinned by `SimulationGoldenMasterTests`; the prepared
Monochrome Court catalogue and route-owned roster intentionally re-pin it to
`14713629958221367877` after subsequent documented combat changes. See the
test's change history; audit fixes must not silently update this hash.

## Runtime composition status

`VoidFallGameRuntime` is still a large partial class. Current extractions:

- Complete: input polling, combat state, cosmetic FX state
- Active UI controllers: Settings, Records and Workshop
- Present but not fully promoted: `HudPresenter`
- Still runtime-owned: arena rendering, much of HUD/view synchronization,
  lifecycle orchestration, route/rift presentation, roulette integration

This is maintainability debt, not a reason for a rewrite. Future extractions
must preserve the golden master and visual captures.

## UI

All screens use runtime-authored uGUI. `UIManager` owns the screen hierarchy;
`UIViewBase` owns one `CanvasGroup` per view. Gameplay HUD remains on its own
canvas below modal menus. The music perimeter is one bounded custom Graphic,
and danger indicators render above it.

## Rendering

URP 17.5 is active through `VoidFallURP.asset` and
`VoidFallUniversalRenderer.asset`. The current PC presentation uses:

- Linear color space
- 2x MSAA, HDR disabled, automatic intermediate texture
- Bloom and chromatic aberration exposed in video settings
- World-space arena vignette below gameplay actors
- Unlit/additive custom shaders for sprites and effects

Arena identity plates are imported, non-readable, mip-streamed textures.
Addressables owns their asynchronous lifetime.

## Arenas and route

The route state machine currently contains ten conceptual Voids. Five have
prepared visual packages: Abyss, Red Nebula, White Sakura, Hydra and Monochrome
Court. A normal
player run is forced to begin in Abyss;
finishing its objective opens the first route choice.

Hydra Prime is a route-owned stationary boss and never enters the normal
endless boss rotation. Its encounter reuses the fixed boss/hostile-shot pools,
suppresses ambient spawning, keeps the central vertebral chain non-colliding,
and enables collision only at the outer rib-cage boundary. Marrow Barrage,
Evasion, Rib Guillotine and Optic Rupture use deterministic gameplay RNG;
ordered pixel disintegration remains a visual-only shader effect. Hydra's
approved v13 base, bone and boss layers are authored assets; runtime rendering
must not substitute procedural approximations for them.

Monochrome Court uses the same fixed enemy, boss and hostile-shot pools. Its
survival phase alternates black units from the left and white units from the
right: Pawn pressure, hybrid-Rook cardinal charges, Bishop sniper fire, Knight
L-path harassment and Queen line control/promotion. Its two stationary
Grandmasters share health and remain simultaneously active and vulnerable.
White and Black alternate
control of their matching floor tiles through warning, burning and recovery
stages; the opposite color is safe. Ambient spawns remain suppressed.

Abyss, Red Nebula, White Sakura, Hydra and Monochrome Court have objective
implementations. Other Layer-II objectives, Last Gate, route threat scaling
and the Final Void escape remain unfinished. The rift swap clears enemies,
shots and meteors; automatic single-exit travel now commits the route selection
before initializing the incoming objective.

## Persistence

`SaveStore` owns schema-v5 JSON under `Application.persistentDataPath`.
Writes use a temporary file, forced flush, atomic replacement and one backup.
Missing/corrupt primary files recover that backup before stale legacy profiles;
recovery never rotates corrupt primary data over the good backup, including
when the initial recovery write fails. Bestiary IDs include Unity-authored content.
Unreadable storage is latched so a blank runtime profile cannot overwrite
progression that failed to load. The live route is not yet serialized.

## Validation contract

- Build the generated solution: compilation gate
- EditMode: pure rules, persistence, content, UI controllers and assets
- PlayMode: runtime lifecycle, route regression tests and golden masters
- Windows player: Addressables/build integration and stress smoke
- Visual changes: 720p/1080p screenshots and manual readability checks

Current known warnings are fourteen JsonUtility fixture-field warnings and two
Unity 6.5 `Resolution.refreshRate` deprecations.
