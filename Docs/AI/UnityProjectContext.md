# VoidFall Unity project context

Last analyzed: 2026-09-01

Source state: `f97a180` plus the verified route/UI working tree

## Product

`voidfall-unity` is the active game. The browser project is deprecated.
Current target is Windows Standalone; mobile remains a future platform pass.

## Environment

- Unity `6000.5.7f1`
- URP `17.5.0`
- Addressables `2.8.1`
- Input System `1.20.0`
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
- Do not add per-enemy or per-projectile MonoBehaviours.

## Current arena state

Prepared and selectable in the main-menu carousel:

- Abyss
- Red Nebula
- White Sakura

Next visual-only packages requested:

- Hydra, including existing mutation behavior
- Monochrome Court
- Lost City, using the existing `null-city` route identity for compatibility

## Validation baseline

- C# build: zero errors
- EditMode: 168 project tests passing
- PlayMode: 5 tests passing
- Windows release player: build and `productionMax` smoke passing
- No duplicate GUIDs or missing `.meta` files

## Known gaps

- Route content after Layer I is incomplete.
- Hydra mutation rules exist but are not wired into live spawning yet.
- Monochrome Court and Lost City are graph nodes without visual packages.
- HudPresenter is present but not wired as the sole HUD owner.
- CI Unity tests run only when the repository variable is enabled.
- Product identity still uses Unity's default company/application identifier.
- Android/mobile, controller navigation and safe-area behavior need device work.

## Workspace hygiene

`Library`, `Logs`, `TestResults`, `.vs`, generated project files and player
builds are local artifacts. They must not be committed. `Assets` is roughly
50 MiB; a multi-gigabyte local folder is generated cache, not source size.
