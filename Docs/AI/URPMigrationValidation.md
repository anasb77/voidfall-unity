# VoidFall URP migration validation

Date: 2026-08-20
Worktree: `voidfall-unity/.worktrees/urp-migration`
Branch: `migration/urp`
Validation head before this report: `8d54550a1a2817e1315a4bae076c3c5d952e2e4c`

## Scope and pipeline

- Unity Editor: `6000.5.7f1`.
- URP package: `com.unity.render-pipelines.universal` `17.5.0`.
- Active asset: `Assets/VoidFall/Rendering/URP/VoidFallURP.asset`.
- Renderer: `VoidFallUniversalRenderer.asset` (`UniversalRendererData` / Universal Renderer), not the 2D Renderer.
- Render Graph compatibility mode is disabled; the migration tests verify `RenderGraphSettings.enableRenderCompatibilityMode == false`.
- Post-processing, arena beautification, arena art replacement, and renderer features remain deferred by design.

The worktree contained substantial pre-existing tracked and untracked user work before validation, including `Assets/VoidFall/Editor/BuildScript.cs`, `Assets/VoidFall/UI/`, `Docs/AI/`, and parity/content changes. No production code or build script was changed for Task 5. Unity import/test/build activity also produced local serialization changes in existing URP/project-settings files; these remain unstaged and are not part of this report commit.

## EditMode suite

Command (Unity runner-managed exit; do not add a trailing `-quit`, which can exit before the test runner starts):

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.7f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\Users\anasb\Desktop\voidfall\voidfall-unity\.worktrees\urp-migration' -runTests -testPlatform editmode -testResults 'C:\Users\anasb\Desktop\voidfall\voidfall-unity\.worktrees\urp-migration\TestResults\urp-editmode.xml' -logFile 'C:\Users\anasb\Desktop\voidfall\voidfall-unity\.worktrees\urp-migration\TestResults\urp-editmode-full.log'
```

Result: Unity process exit `0`; `TestResults/urp-editmode.xml` reports `total=10`, `passed=10`, `failed=0`, `skipped=0`, `inconclusive=0`. All ten cases are in `VoidFall.Tests.Editor.UrpMigrationTests` (including material-resource, renderer, global-settings, camera, volume, and Render Graph checks). The earlier command with `-quit` produced no result XML because it terminated during project refresh; it was not used as the gate result.

## Windows player build

Command:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.7f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\Users\anasb\Desktop\voidfall\voidfall-unity\.worktrees\urp-migration' -executeMethod VoidFall.EditorTools.BuildScript.BuildWindows -logFile 'C:\Users\anasb\Desktop\voidfall\voidfall-unity\.worktrees\urp-migration\TestResults\urp-windows-build.log'
```

`BuildPipeline.BuildPlayer` logged `Build Finished, Result: Success` and `Build succeeded: 124254830 bytes`; `BuildScript` calls `EditorApplication.Exit(0)` on this result. The hard-coded output was intentionally left unchanged:

- Reported player build size: `124,254,830` bytes.
- Executable: `C:\Users\anasb\Desktop\voidfall\Builds\VoidFall.exe`.
- Executable file size: `667,648` bytes; the corresponding `VoidFall_Data` directory and Windows player files are present.
- Existing context documentation cites a prior `117,606,213`-byte player; against that non-reproducible pre-migration reference, the reported build-size delta is `+6,648,617` bytes (`+5.65%`). No equivalent prior executable-size measurement was available.

## Automated captures

All evidence is outside `Assets/` under `semantic-review/urp-migration-20260820/`. Each player exited `0`; each PNG was checked for the requested dimensions and visually inspected. Each capture has matching `.player.log`, `.stdout.log`, and `.stderr.log` files.

| Screen/state | 1280x720 | 1920x1080 | Observation |
|---|---|---|---|
| Menu | `menu-1280x720.png` (521,556 bytes) | `menu-1920x1080.png` (982,323 bytes) | Arena backdrop, title, controls, status strip, and navigation render. The opaque status-strip divider blocks are also present in pre-migration `audit-captures-20260820/menu-visible-1920x1080.png`; no URP-specific difference is asserted. The older `menu-1920x1080.png` baseline is black and was not used for parity. |
| Settings | `settings-1280x720.png` (431,192 bytes) | `settings-1920x1080.png` (783,452 bytes) | The stacked labels/sliders/buttons overlap in both captures. This is visibly the same defect as pre-migration `audit-captures-20260820/settings-1280x720.png`, so it is classified pre-existing rather than migration-introduced. |
| Workshop | `workshop-1280x720.png` (409,190 bytes) | `workshop-1920x1080.png` (731,464 bytes) | Panel, upgrade rows, scrollbar, icons, text, and backdrop render without pink/missing assets. |
| Records | `records-1280x720.png` (425,022 bytes) | `records-1920x1080.png` (769,541 bytes) | Records panel and values render; top-row labels overlap. No pre-migration Records image was available, so parity or causality is not claimed for that layout issue. |
| Gameplay | `gameplay-1280x720.png` (490,271 bytes) | `gameplay-1920x1080.png` (926,815 bytes) | Player, arena, HUD, pause/music controls, and transparent world elements render. These cold captures are at `0:00` and do not exercise dense effects. |
| Stress gameplay | `stress-productionMax-seed424242-1280x720.png` (688,874 bytes) | — | Isolated `productionMax` run shows two bosses, additive glows/projectiles/arcs, glowing pickups, hostile shots, blast-wave rings, transparent sorting, HUD, and world/UI compositing. No pink or missing geometry was observed. Filament-specific masking was not independently isolated, but no mask/error artifact appeared in this populated frame. |

The original four-screen batches were launched concurrently, so their wall times are not performance-comparable; their images/logs are valid and complete. The menu 1280 capture was run serially before that batch (`~51.5 s` to capture); the missing menu 1920 capture was rerun serially (`~44.4 s` in this wrapper; an independent process-to-PNG reading was ~43.18 s versus a cited pre-migration ~43.8 s). These are rough launch-to-frame-30 capture wall times, not instrumented time-to-interactive values; the startup stall remains.

Player logs show no shader/material exceptions, missing-shader messages, or pink-render diagnostics. Repeated environment/pre-existing warnings are `d3d12: failed to query info queue interface (0x80004002)` and `GarbageCollector disposing of ComputeBuffer...`; neither appeared as a URP migration-specific failure. The stress log confirms the capture and benchmark hooks both completed.

## Stress benchmark

No pre-migration benchmark JSON was present in the checkout. Per Task 5, the current run used an explicit fixed window rather than the catalog defaults:

```powershell
& 'C:\Users\anasb\Desktop\voidfall\Builds\VoidFall.exe' -screen-width 1280 -screen-height 720 -screen-fullscreen 0 -vfbench=1 -vfscenario=productionMax -vfseed=424242 -vfwarmup=10 -vfmeasure=30 -vfoutput='C:\Users\anasb\Desktop\voidfall\voidfall-unity\.worktrees\urp-migration\semantic-review\urp-migration-20260820\benchmark-productionMax-seed424242-w10-m30.json' -logFile 'C:\Users\anasb\Desktop\voidfall\voidfall-unity\.worktrees\urp-migration\semantic-review\urp-migration-20260820\benchmark-productionMax-seed424242-w10-m30.player.log'
```

The hook wrote to its default `%LOCALAPPDATA%\Low\DefaultCompany\VoidFall\voidfall-unity-bench-productionMax.json` despite the absolute `-vfoutput` argument. That exact JSON was copied (not edited) to `semantic-review/urp-migration-20260820/benchmark-productionMax-seed424242-w10-m30.json`. Exit was `0`; the report contains four samples at `t=0`, `10.037`, `20.035`, and `30.034` seconds:

| Metric | Current values | Baseline/delta |
|---|---:|---|
| Frame EMA | `16.66796–16.66808 ms` | unavailable; no prior JSON |
| Managed bytes | `12,554,240–12,804,096` (12.55–12.80 MB) | unavailable |
| Allocated bytes | `192,873,693–193,753,974` | unavailable |
| Reserved bytes | `289,374,208` | unavailable |
| Enemies / bosses | `173–192` / `2` | unavailable |
| Bullets / hostile shots | `13–32` / `39–90` | unavailable |
| Pickups / meteors | `280–281` / `0` | unavailable |

The JSON does not instrument time-to-interactive, Unity object count, or material count; those metrics are unsupported and are not inferred. The benchmark process exited `0` after writing its report. A second combined benchmark/stress-capture run also completed with exit `0` and produced the populated stress frame above.

## Acceptance status and limitations

- Package/version gate: **passed by project resolution and tests** (`6000.5.7f1`, URP `17.5.0`).
- Compile/EditMode gate: **passed**, 10/10 tests.
- Windows x64 build gate: **passed**, Unity build report success and executable/data output present.
- Launch/capture gate: **passed**, all requested screen states and both resolutions have exit-0 captures; stress capture added for effect coverage.
- Visual/effect gate: **passed with documented pre-existing UI limitations**; no pink/missing rendering observed. The normal gameplay captures alone were at `0:00` and insufficient for effect coverage; the isolated stress capture provides the additive/ring/arc evidence. Filament and transition-specific states were not independently triggered.
- Runtime benchmark gate: **passed for hook execution**, but no numeric pre-URP baseline exists and TTI/object/material metrics are not instrumented.
- Rollback gate: **preserved**; the original checkout at `C:\Users\anasb\Desktop\voidfall\voidfall-unity` was not overwritten.

Arena beautification/post-processing (Bloom, color grading, tone mapping, distortion), authored arena art, and the URP 2D Renderer remain explicitly deferred.

## Rollback

The migration is isolated on `migration/urp` in `.worktrees/urp-migration`; the original checkout remains the recovery source. To review or roll back migration code, use the migration branch history and revert the migration commits as a group. Do not reset or clean the original checkout: it contains user-owned dirty/untracked work. Evidence and this report are disposable review artifacts; the benchmark/capture output remains untracked outside `Assets/`.
