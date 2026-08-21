# VoidFall Unity Project Context

> Last analyzed: 2026-08-20
> Commit: `e33e29c357baea69c8c971147340f86fb2d5dfb0`
> Scope: current dirty working tree in `voidfall-unity`

## Product status

- `voidfall-unity` is the active product.
- The sibling React/browser project `voidfall.io` is deprecated and was treated as read-only historical reference.
- Current configured/tested target is Windows Standalone. Android and WebGL are not validated.
- Current Unity version is 6000.5.7f1.

## Runtime shape

- Render pipeline: Built-in Render Pipeline, Linear color space.
- Input: Input System package 1.20.0, with gameplay polling `Keyboard.current`, `Gamepad.current`, and `Touchscreen.current` directly.
- UI: runtime-created uGUI. Most labels use legacy `UnityEngine.UI.Text`; TextMeshPro is present but not the main text path.
- Scene count: one build scene, `Assets/Scenes/SampleScene.unity`.
- Prefab count: zero.
- The scene contains only a Main Camera. The playable application is created before scene load by `ParityFixtureProbe.CreateProbe`.

## Startup flow

1. `ParityFixtureProbe.CreateProbe` creates a persistent `VoidFall` object.
2. It adds `ParityFixtureProbe`, `FixedGameLoop`, and `VoidFallGameRuntime`.
3. `VoidFallGameRuntime.Awake` creates world, camera, backdrop, HUD, player, audio, effects, save state, and every uGUI view.
4. The first rendered frame calls `EnsureArenaPlateViewport`.
5. The current arena is baked synchronously. While browsing the menu, the Void arena is also baked synchronously when the saved arena is not Void.
6. Procedural sprite warming continues across menu frames; starting a run drains any remaining work synchronously.

## Assembly map

```text
VoidFall.Core
  <- VoidFall.Content
  <- VoidFall.Persistence

VoidFall.Audio

VoidFall.UI
  -> Core, Content, Persistence, uGUI, TMP, Input System

VoidFall.Runtime
  -> Core, Content, Persistence, Audio, UI, Input System
```

The Core and Content split is a useful boundary. The main architectural concentration is `VoidFallGameRuntime.cs`, which owns simulation, rendering, runtime asset generation, UI/HUD coordination, input, persistence orchestration, and telemetry.

## Main code areas

- `Assets/VoidFall/Core`: deterministic rules and shared model types.
- `Assets/VoidFall/Content`: generated catalog and gameplay content rules.
- `Assets/VoidFall/Persistence`: schema-v5 JSON saves, browser import/export, atomic replacement and backup handling.
- `Assets/VoidFall/Audio`: generated effects plus streamed soundtrack control.
- `Assets/VoidFall/UI`: current runtime-authored uGUI migration.
- `Assets/VoidFall/Runtime/Gameplay`: main runtime, procedural sprites, arena plates.
- `Assets/VoidFall/Runtime/Telemetry`: run telemetry and stress benchmark support.

## Persistence

- Saves use `Application.persistentDataPath`.
- Writes use a temporary file, `Flush(true)`, atomic replacement where available, and `.bak` recovery.
- An unreadable-save latch protects damaged data from being overwritten.
- Browser import creates a pre-import backup.

## Build and validation

- Build scene: `Assets/Scenes/SampleScene.unity` only.
- Current local build script targets a machine-specific absolute path.
- Latest inspected Windows build log reports success and a 117,606,213-byte player.
- Unity batch compilation succeeded on 2026-08-20.
- EditMode test discovery succeeded but found zero tests.
- The repository's older parity documents point to test suites in an external validation clone; those tests are not reproducible from this checkout.

## Working-tree warning

The audit covered the current dirty working tree, not just commit `e33e29c`. The entire `Assets/VoidFall/UI` directory and the local build script are currently untracked. Do not clean or reset the tree without first preserving them.

## Recommended operating rules

- Profile startup and gameplay in a Development Build with Profiler markers before tuning.
- Keep procedural pixel generation off the first interactive frame.
- Add tests around pure Core/Content/Persistence behavior before splitting the runtime monolith.
- Add screenshot/layout smoke tests for each uGUI screen at 1280x720 and 1920x1080.
- Treat the current UI migration as in-progress until it is tracked, tested, and visually accepted.

