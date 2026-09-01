# VoidFall

VoidFall is a Unity 6 survivor-shooter built around escaping a branching chain
of hostile Voids. Combat is deterministic and pool-based; presentation uses
runtime-authored uGUI, streamed reactive music, baked arena packages, and URP.

## Current environment

- Unity `6000.5.7f1`
- Universal Render Pipeline `17.5.0`
- Addressables `2.8.1`
- Input System `1.20.0`
- Windows Standalone x86-64, Mono scripting backend
- One enabled scene: `Assets/Scenes/SampleScene.unity`

The scene is intentionally small. `ParityFixtureProbe` creates the persistent
runtime before scene load, and `VoidFallGameRuntime` composes the game world,
simulation, UI, audio, rendering, persistence, and diagnostics.

## Current gameplay

- Six weapons, evolutions, supports, late upgrades, and workshop progression
- Fourteen enemy types, elite variants, four bosses, formation events, meteors
- Boss Roulette, Wild Cards, rare pickups, reactive soundtrack and neon border
- Objective-driven rift opening and route selection
- Three prepared arenas: Abyss, Red Nebula, and White Sakura
- Three deterministic visual recipes per prepared arena

Hydra, Monochrome Court, and Lost City are the next visual arena packages.
Their full arena-specific objectives and enemy catalogues remain separate
future work, except Hydra will reuse the existing mutation rules.

## Controls

- Move: WASD, arrows, or left stick
- Aim: mouse or right stick
- Fire: left mouse, Space, or right trigger
- Pause: Escape, P, or controller Start

## Architecture

First-party assemblies:

- `VoidFall.Core`: engine-free rules, deterministic state machines and RNG
- `VoidFall.Content`: catalogue, spawning, upgrades, formations and rewards
- `VoidFall.Persistence`: versioned JSON saves and browser-save import
- `VoidFall.Audio`: procedural SFX and streamed reactive soundtrack
- `VoidFall.UI`: uGUI views, controllers and HUD presentation contracts
- `VoidFall.Runtime`: Unity composition, simulation bridges and rendering

See [Docs/Architecture.md](Docs/Architecture.md) and
[Docs/Design/VoidFallArenaArchitecture.md](Docs/Design/VoidFallArenaArchitecture.md).

## Validation

From the repository root:

```powershell
dotnet build VoidFall.Runtime.csproj -t:Rebuild
```

Unity test commands must not include `-quit`; the test runner exits on its own.

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.7f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath $PWD `
  -runTests -testPlatform EditMode -testResults Logs/editmode.xml `
  -logFile Logs/editmode.log

& 'C:\Program Files\Unity\Hub\Editor\6000.5.7f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath $PWD `
  -runTests -testPlatform PlayMode -testResults Logs/playmode.xml `
  -logFile Logs/playmode.log
```

Validated on 2026-09-01:

- C# compilation: zero errors
- Project EditMode tests: 168 passed
- PlayMode tests: 5 passed
- Windows release build and `productionMax` smoke run: passed

The build command is:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.7f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath $PWD `
  -executeMethod VoidFall.EditorTools.BuildScript.BuildWindows `
  -logFile Logs/windows-build.log
```

It writes `../Builds/VoidFall.exe`.

## Repository rules

- The browser/React version is deprecated and out of scope.
- Never commit `Library`, `Logs`, `TestResults`, `.vs`, or player builds.
- Preserve gameplay RNG draw order unless a behavior change is intentional.
- Intentional simulation changes require a separately explained golden-master
  hash update and a passing 32-seed determinism sweep.
- Generated arena textures are authored in the Editor, never painted at runtime.

Audio credits are recorded in [Docs/AudioCredits.md](Docs/AudioCredits.md).
