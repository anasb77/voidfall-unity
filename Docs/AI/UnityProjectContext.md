# VoidFall Unity project context

Last analyzed: 2026-09-04

Source state: `049500d` plus existing audio/cosmetics edits and September 4 audit fixes.

## Product

`voidfall-unity` is the active game. The browser project is deprecated.
Current target is Windows Standalone; mobile remains a future platform pass.
The current goal is a full-game release on October 15, subject to readiness.
See `Docs/AI/ReleaseReadiness-2026-09-04.md` for release gates and remaining work.

## Environment

- Unity `6000.5.7f1`
- URP `17.5.0`
- Addressables `2.8.1`
- Input System `1.20.0`
- Unity Test Framework `1.7.0`; six first-party runtime assemblies and two test assemblies
- uGUI + TextMeshPro package, with many runtime labels still using uGUI Text
- GitHub: `https://github.com/anasb77/voidfall-unity`

## Startup

One scene is enabled: `Assets/Scenes/SampleScene.unity`. The scene contains a
camera; `ParityFixtureProbe` creates the persistent game root before scene load.
`VoidFallGameRuntime.Awake` composes all runtime services and views, loads the
save, prepares arena residency, and enters the menu.

## Main directories

- `Assets/VoidFall/Core`: deterministic rules and route/objective state
- `Assets/VoidFall/Content`: generated catalogue and authored content rules
- `Assets/VoidFall/Runtime`: Unity integration, GameSim/FxSim and rendering
- `Assets/VoidFall/UI`: runtime uGUI views, controllers and HUD contracts
- `Assets/VoidFall/Audio`: streamed music and procedural SFX
- `Assets/VoidFall/Persistence`: schema-v5 save/import code
- `Assets/VoidFall/Editor`: arena/sprite baking and Windows build commands
- `Assets/VoidFall/Generated`: baked sprites and arena source assets
- `Assets/AddressableAssetsData`: arena Addressables catalogue configuration

## Important invariants

- Combat and FX use separate deterministic RNG streams.
- Pool/order iteration semantics are gameplay behavior; do not reorder casually.
- New runs begin in Abyss regardless of the menu preview arena.
- Arena textures are generated in Editor code and loaded as packages at runtime.
- Arena identity effects must not tint the player or HUD.
- Save failures must never overwrite unreadable progression.
- Missing/corrupt primary saves recover the last valid backup before legacy files.
- Both card-selected and automatic rift travel must commit the route choice.
- Do not add per-enemy or per-projectile MonoBehaviours.

## Current arena state

Prepared and selectable in the main-menu carousel:

- Abyss
- Red Nebula
- White Sakura
- Hydra
- Monochrome Court

Next visual-only packages requested:

- Lost City, using the existing `null-city` route identity for compatibility

## Validation baseline

- September 4 EditMode: 259/259 passing, including nine save recovery tests.
- September 4 PlayMode: 7/7 passing, including the repaired automatic-travel
  regression, pinned hash and 32-seed sweep.
- Windows build and startup capture pass. Stress performance is not validated:
  combat counts stay identical while the probe reports elapsed wall time.
- Detailed verification and remaining visual risks are in the release report.
- Asset metadata scan: 658 metadata files, no duplicate GUIDs or missing file metadata.
- Unity Editor installed locally; no Unity MCP package or callable provider found.

## Known gaps

- Only Abyss, Red Nebula, White Sakura, Hydra and Monochrome Court have objectives.
  Dead Orbit, Graveyard, Null City, Last Gate and Final Void do not. Runtime
  victory/escape resolution is absent, despite `VoidRouteRun.HasEscaped`.
- Hydra mutation rules are wired during its survival phase; its boss phase
  suppresses ambient enemies and spawns route-only Hydra Prime.
- Hydra's production base, bone and boss visuals are authored from the approved
  v13 reference; procedural primitives are reserved for attacks and motion.
- Monochrome Court is prepared and owns its chess roster and route-only Twin
  Grandmaster encounter. Only its five chess enemy types spawn there; both
  bosses fight simultaneously and alternate white/black burning floor tiles.
  Lost City remains graph/data only.
- HudPresenter is present but not wired as the sole HUD owner.
- CI Unity tests run only when the repository variable is enabled.
- CI now selects all PlayMode tests; hosted execution and a hosted player build remain unverified.
- Product identity is `Voidfall` / `VoidFall`, application ID `com.voidfall.game`.
- Combat auto-targets and auto-fires. Gamepad left-stick movement exists; controller
  Start/pause handling and menu focus/navigation are incomplete.
- Runs are not resumable; live-run rewards are committed only on terminal game over.
- URP uses 2x MSAA, HDR off, automatic intermediate texture, and SRP Batcher.

## Workspace hygiene

`Library`, `Logs`, `TestResults`, `.vs`, generated project files and player
builds are local artifacts. They must not be committed. Audit evidence and a
copy of the preceding Windows build are under `Logs/Audit-2026-09-04/`.
Existing user edits to audio, player cosmetics, Workshop and Player Settings
were present before the audit and must be preserved.
