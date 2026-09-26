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

- A six-void escape journey starting in Abyss, with five dealer crossings
- Eight arenas: Abyss, Red Nebula, White Sakura, Hydra, Monochrome Court,
  Crascendo, Eon Sea, and Null City
- Automatic weapons and evolutions, support cards, Boss Roulette and Wild Cards
- Permanent Workshop progression, forms, and two separate manual legendaries:
  Sound Blade and Charged Rifle
- Arena-specific rosters and encounters, including the same-visit Hydra I/II
  transition and the Court's Wingwang encounter
- Six-minute survival phases, bounded enemy pools and automatic run exports

Approved map and weapon art is integrated. New map and asset studies remain
prototypes until explicitly approved; the internal dashboard's legacy inventory
does not establish approval to restore old browser content.

## Controls

- Move: WASD, arrows, or left stick
- Combat automatically targets nearby enemies and fires equipped weapons.
- Pause: Escape, P, or controller Start
- Void map: Tab; planning a route does not commit travel
- Dealer: E or controller South to browse
- Manual legendary: aim with the mouse and hold LMB/F, or right stick and RT
- Menus support keyboard/controller focus; the active modal owns submit/cancel

## One current Windows build

The canonical player is `../Builds/VoidFall.exe`, one directory above this
Unity project. Keep its accompanying data and runtime files together. Read
`../Builds/BUILD_INFO.txt` for the exact source revision and build time.
`../START_HERE.md` contains local launch instructions.

Temporary validation players are removed after a verified replacement is
promoted. Preserve `../Builds/RunExports/` and any explicitly designated
`../Builds/Archive/` releases. Player binaries and local run data are not
committed to GitHub; this repository contains the reproducible Unity source,
required baked assets, tests, tools and internal dashboard.

## Architecture

First-party assemblies:

- `VoidFall.Core`: engine-free rules, deterministic state machines and RNG
- `VoidFall.Content`: catalogue, spawning, upgrades, formations and rewards
- `VoidFall.Persistence`: versioned JSON saves and browser-save import
- `VoidFall.Audio`: procedural SFX and streamed reactive soundtrack
- `VoidFall.UI`: uGUI views, controllers and HUD presentation contracts
- `VoidFall.Runtime`: Unity composition, simulation bridges and rendering

Start with [AGENTS.md](AGENTS.md) and [Docs/REPO_MAP.md](Docs/REPO_MAP.md).
See also [Docs/Architecture.md](Docs/Architecture.md) and
[Docs/Design/VoidFallArenaArchitecture.md](Docs/Design/VoidFallArenaArchitecture.md).

## Validation

Use the Unity Editor for authoritative compilation, test and player validation.
Only one Editor/test/build/bake operation may use this checkout at a time.
With current generated project files, a quick supplementary compile check is:

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
  -batchmode -force-d3d11 -projectPath $PWD `
  -runTests -testPlatform PlayMode -testResults Logs/playmode.xml `
  -logFile Logs/playmode.log
```

The September 26 foundation validation passed 679 EditMode and 441 PlayMode
tests, the unchanged golden masters and 32-seed sweep, 27 asset checks,
8 final HUD checks, and both complete native journey branches. The Windows
player build passes. Exact evidence and remaining limits are recorded in
[FoundationRepairStatus.md](Docs/AI/FoundationRepairStatus.md).
Visible-player rendering/FPS, physical audio/device switching and the reported
fullscreen/focus hang are not certified by automated test or headless smoke
results. Hosted CI also requires external Unity licensing/image configuration.

The build command is:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.7f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath $PWD `
  -executeMethod VoidFall.EditorTools.BuildScript.BuildWindows `
  -logFile Logs/windows-build.log
```

It writes `../Builds/VoidFall.exe`. The same pipeline supports
`VOIDFALL_BUILD_OUTPUT` for temporary validation and `VOIDFALL_SOURCE_REVISION`
for provenance. Validate a staged replacement before promoting it, then remove
the temporary player. A save-isolated, advancing-combat smoke check is:

```powershell
./Tools/Validation/Smoke-Player.ps1 `
  -PlayerPath ../Builds/VoidFall.exe -OutputDirectory Logs/player-smoke
```

This headless check does not measure rendering quality or FPS.

## Internal dashboard

`Internal Dashboard/` is the read-only content and artwork inventory, including
legacy comparison evidence. It ships with the project source. Read its
[Tool Description](Internal%20Dashboard/Tool%20Description.md) for launching
and refreshing the inventory after content changes.

## Repository rules

- The browser/React version is deprecated and out of scope.
- Never commit `Library`, `Logs`, `TestResults`, `.vs`, or player builds.
- Preserve gameplay RNG draw order unless a behavior change is intentional.
- Intentional simulation changes require a separately explained golden-master
  hash update and a passing 32-seed determinism sweep.
- Generated arena textures are authored in the Editor, never painted at runtime.

Audio credits are recorded in [Docs/AudioCredits.md](Docs/AudioCredits.md).
