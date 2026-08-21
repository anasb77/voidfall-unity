# VoidFall Unity Project Health Report

> Last analyzed: 2026-08-20
> Commit: `e33e29c357baea69c8c971147340f86fb2d5dfb0`
> Scope: current dirty working tree; Unity project only
> Project root: `C:\Users\anasb\Desktop\voidfall\voidfall-unity`

## Overall assessment

Working prototype, not release-ready. Compilation and packaging are healthy, persistence is unusually careful, and the core assembly split is sound. The dominant risks are a confirmed multi-second first-frame workload, confirmed broken uGUI layouts, zero repository-owned tests, and a 1.18 MB runtime monolith. No Critical issue or confirmed save-corruption path was found.

## Coverage

### Checked

- Unity version, render pipeline, packages, Player/Quality/Graphics/Input settings.
- Scenes, build settings, prefabs, asmdefs, metadata/GUID integrity.
- Runtime initialization and lifecycle.
- uGUI construction, layout, input/navigation, text, accessibility, safe area.
- Static performance, allocation, asset-loading, texture/audio generation paths.
- Persistence, browser import/export, telemetry writes.
- Windows build script/log, player launch, player logs, Unity batch compile.
- Static secret scan, repository hygiene, documentation/test evidence.

### Not checked

- Unity Profiler CPU/GPU/Memory captures; conclusions marked Likely/Possible need those captures.
- Full gameplay balance, every menu interaction, every arena transition, or a long stress run.
- Physical gamepad/touch devices, Android, WebGL, localization, screen readers.
- Package currency/security advisories or third-party license review.
- The deprecated `voidfall.io` implementation beyond identifying it as out of scope.

## Priority findings

| ID | Severity | Confidence | Domain | Finding |
|---|---|---|---|---|
| VF-001 | High | Confirmed | Startup | First interactive frame synchronously bakes one or two 2.23M-pixel arena bases plus full-size detail plates. |
| VF-002 | High | Confirmed | UI | Shared vertical layout disables child height control, collapsing Settings and other stacked screens. |
| VF-003 | High | Confirmed | Validation | Repository has zero tests; external historical suites do not validate this working tree. |
| VF-004 | High | Confirmed | Architecture | One 1.18 MB MonoBehaviour owns most gameplay, render, HUD, input, menu, and persistence coordination. |
| VF-005 | High | Likely | Memory | Runtime plates keep CPU-readable texture copies; a two-arena menu can retain roughly 71 MB across CPU/GPU before overhead. |
| VF-006 | Medium | Confirmed | UI | Status-strip dividers are forced to expand into wide grey blocks. |
| VF-007 | Medium | Confirmed | Startup | All uGUI views, procedural audio clips, soundtrack metadata, and disabled grain assets are built eagerly. |
| VF-008 | Medium | Confirmed | Workflow | Entire uGUI migration and build script are untracked; a clean checkout loses them. |
| VF-009 | Medium | Likely | Performance | Gameplay HUD creates formatted strings every frame and enemy render repeats catalog/color resolution per active enemy. |
| VF-010 | Medium | Confirmed | Input/UI | No explicit menu focus handoff/navigation; most screens lack a retained-mode safe-area root. |
| VF-011 | Medium | Confirmed | Release | Default company/app ID, hard-coded build output, no CI, broad unused template packages. |
| VF-012 | Medium | Possible | Security | A committed console passcode field exists in ProjectSettings; value is intentionally redacted here. |
| VF-013 | Medium | Confirmed | Documentation | README/MIGRATION_STATUS still describe IMGUI and five assemblies; parity evidence lives outside the repo. |
| VF-014 | Low | Possible | Resource lifecycle | Player shutdown reports an undisposed ComputeBuffer; no first-party owner was found statically. |

## Detailed findings

### VF-001 — first frame performs synchronous multi-million-pixel bakes

- Severity: High
- Confidence: Confirmed
- Evidence: Visible Windows-player captures took 43.8–48.1 seconds to reach/capture frame 30; a hidden run took 52.1 seconds. `VoidFallGameRuntime.RenderArena` reaches `EnsureArenaPlateViewport`; lines 20173–20187 synchronously call `EnsureArenaPlate` for the saved arena and, in the menu, Void. `ArenaPlateFactory` runs per-pixel gradients, noise, trig, and detail construction. The code itself records about 2.23M pixels for one 1080p base plate.
- Impact: The Unity splash appears hung. On a non-Void save, the menu waits for two bases and two detail plates before it can render.
- Recommendation: Render an authored/lightweight menu backdrop immediately. Build immutable pixel data in a cancellable worker or, preferably, bake/import textures before runtime. Publish Texture2D/Sprite objects on the main thread. Do not require both current-arena and Void plates for the first menu frame.
- Verification: Add startup profiler markers. Test cold launch with Void and non-Void saves at 1280x720 and 1920x1080. Gate time-to-interactive at an agreed budget, suggested <=2 seconds after splash.

### VF-002 — shared uGUI vertical layout collapses row heights

- Severity: High
- Confidence: Confirmed
- Evidence: `UIBuilder.AddVerticalLayout` lines 1412–1426 sets `childControlHeight = false`. Views create rows with `LayoutElement` heights through `SetHeight`, but the parent layout does not control those heights. The Settings capture at 1280x720 shows five slider rows, quality, toggles, mute, and export content piled at the top. The same helper is used by Workshop, Records, Pause, Revive, Game Over, and Toast stacks.
- Impact: Core UI is unreadable/unusable at a common resolution.
- Recommendation: Correct the shared layout contract first, then remove any view-specific compensation it makes obsolete. Add content sizing/scroll constraints where required.
- Verification: Capture every screen at 1280x720 and 1920x1080; assert ordered, non-overlapping child rectangles and scrollable overflow.

### VF-003 — no repository-owned tests

- Severity: High
- Confidence: Confirmed
- Evidence: Unity EditMode discovery returned `testcasecount="0"`. No Test asmdef, `[Test]`, or `[UnityTest]` exists under Assets. Historical documentation cites 60+ EditMode and 300+ PlayMode tests in `D:/VoidFall-builds`, outside this project.
- Impact: Current uGUI defects compile and package cleanly. Core rules, save migration, startup, and UI regressions have no reproducible gate.
- Recommendation: Bring the external suites into the repository if trustworthy. First add focused tests for save round-trips/recovery, deterministic Core rules, startup composition, and uGUI layout geometry.
- Verification: Clean checkout can run EditMode and PlayMode suites locally and in CI, with failures blocking packaging.

### VF-004 — runtime monolith and invisible composition root

- Severity: High
- Confidence: Confirmed
- Evidence: `VoidFallGameRuntime.cs` is 1,185,331 bytes and about 26k physical lines. It owns simulation, rendering, runtime texture generation, input, HUD, menu state, save coordination, audio setup, quality, and telemetry. The single scene contains only Main Camera; there are zero prefabs; `ParityFixtureProbe` is the production bootstrap.
- Impact: Changes have a large regression radius. Editor hierarchy, serialized dependencies, menu layout, and startup order are invisible until Play Mode.
- Recommendation: After tests exist, extract bounded services in order: startup/composition, arena rendering, HUD presenter, input reader, and run/session coordinator. Author stable UI roots/panels as prefabs or a dedicated bootstrap scene where visual inspection helps.
- Verification: Each extraction preserves current tests, player captures, and stress benchmark behavior.

### VF-005 — plate texture memory and abandoned pre-bakes

- Severity: High
- Confidence: Likely
- Evidence: At 1920x1080, one plate is about 1,989x1,120. Base and detail textures use `Apply(false, false)`, retaining CPU-readable copies. A non-Void menu owns four such textures: about 8.9M texels, roughly 35.7 MB CPU plus 35.7 MB GPU before mip/driver/object overhead; temporary Color32 buffers raise peak memory further. `DiscardArenaPlatePrebake` drops the Task reference without cancellation, so repeated invalidation can leave expensive workers running to completion.
- Impact: High launch allocation/GC pressure and avoidable steady memory; quality/viewport churn can overlap obsolete work.
- Recommendation: Use non-readable imported/prebaked textures where possible. If runtime generation remains, release CPU copies with `Apply(false, true)` after upload and add cancellation/versioning to pre-bake jobs.
- Verification: Unity Memory Profiler snapshot before/after menu entry and repeated quality changes; confirm no stale workers and expected texture memory.

### VF-006 — status dividers expand into grey blocks

- Severity: Medium
- Confidence: Confirmed
- Evidence: `MainMenuView.BuildStatusStrip` enables `childForceExpandWidth`; divider children request 1 px but are still expanded. The 1920x1080 capture shows two opaque grey rectangles across the strip.
- Impact: Main menu looks broken despite being interactive.
- Recommendation: Disable forced expansion for the row and give the three cells flexible width, or explicitly ignore divider children in sizing.
- Verification: Capture 16:9 and narrower widths; dividers remain 1x24 px.

### VF-007 — eager startup work remains broad

- Severity: Medium
- Confidence: Confirmed
- Evidence: `UIManager.Build` constructs every menu/overlay view in Awake. `ProceduralAudio.Awake` builds all effect clips and parameter variants sample-by-sample. `MusicDirector` calls `Resources.LoadAll<AudioClip>` for both soundtrack groups. `SetupBackdrop` creates three 256x256 grain sprites and a grain canvas even though grain is permanently disabled.
- Impact: Extra splash-to-menu CPU, allocations, objects, and memory; harder startup attribution.
- Recommendation: Build only the home screen and essential audio path before first interaction. Lazy-create secondary views and clips. Remove disabled grain creation.
- Verification: Startup profiler markers show each category and a materially lower time/GC allocation.

### VF-008 — current UI is not version-controlled

- Severity: Medium
- Confidence: Confirmed
- Evidence: `git status` reports the entire `Assets/VoidFall/UI/` directory and `Assets/VoidFall/Editor/BuildScript.cs` as untracked.
- Impact: A clean checkout, branch switch, or teammate clone omits the new UI and build path.
- Recommendation: Review generated metadata, then commit the migration as a coherent change with its tests/captures. Preserve this dirty tree until then.
- Verification: Fresh clone contains six asmdefs, compiles, and reproduces the accepted screens.

### VF-009 — likely steady gameplay CPU/GC waste

- Severity: Medium
- Confidence: Likely
- Evidence: `UpdateHud` formats and assigns health, clock, level, metric, and score strings each frame. The enemy render loop resolves sprite IDs, catalog entries, and colors for active enemies each frame before assigning cached sprites.
- Impact: Avoidable managed allocations and repeated lookups during dense runs; may worsen frame pacing.
- Recommendation: Update HUD only when source values change or at a fixed low frequency. Cache immutable enemy visual data at spawn/state change. Confirm with Profiler before broad optimization.
- Verification: Profile a productionMax run; compare GC.Alloc/frame and main-thread time before/after.

### VF-010 — retained UI navigation and safe-area gaps

- Severity: Medium
- Confidence: Confirmed
- Evidence: No explicit `EventSystem.SetSelectedGameObject` or `Selectable.navigation` configuration exists. Screen transitions do not assign focus. Most new menu views use fixed 1280x720-oriented panel sizes and no shared `Screen.safeArea` root; only Level Up references safe-area width in the new UI.
- Impact: Keyboard/gamepad menu navigation is unreliable; mobile cutouts and short/narrow displays can obscure controls.
- Recommendation: Add a shared safe-area container, explicit first-selectable focus per screen, and deterministic navigation. Test device changes while a menu is open.
- Verification: Keyboard-only, controller-only, and 16:9/notched-device layout matrix.

### VF-011 — release/build hygiene is unfinished

- Severity: Medium
- Confidence: Confirmed
- Evidence: `DefaultCompany`, `com.DefaultCompany.2D-Project`, one scene named SampleScene, non-resizable Windows window, no CI workflow, and an untracked build script with an absolute user-machine output path. Manifest includes broad template packages such as Visual Scripting, Timeline, Multiplayer Center, Collaborate, and the full 2D feature set without first-party references found for several of them.
- Impact: Non-reproducible team builds, wrong application identity, avoidable package/import/build weight.
- Recommendation: Set production identity/version/icons, make output path argument-driven and repo-relative, add CI compile/test/build gates, then remove unused packages one at a time with verification.
- Verification: Fresh-machine command produces the same player without editing the script.

### VF-012 — credential-like console setting is committed

- Severity: Medium
- Confidence: Possible impact; confirmed presence
- Evidence: `ProjectSettings/ProjectSettings.asset` contains a non-empty `ps4Passcode`. Its value is intentionally not copied into this report. No other clear first-party secret was found by the static scan.
- Impact: If real, repository access exposes a console credential; if template/default, it still causes secret-scanner noise and ambiguity.
- Recommendation: Determine ownership. Rotate if real; otherwise clear it. Add a secret scan to CI and keep platform credentials outside version control.
- Verification: Repository history and current tree contain no live secret; console build ownership is documented.

### VF-013 — documentation describes the previous architecture

- Severity: Medium
- Confidence: Confirmed
- Evidence: README and MIGRATION_STATUS still say the UI is IMGUI and that five assemblies exist. Current tree has a sixth UI assembly and uGUI. PARITY_MATRIX is large and points to external artifacts that are not reproducible here.
- Impact: Contributors follow obsolete architecture and may trust validation that does not cover current code.
- Recommendation: Replace status prose with current architecture, reproducible commands, and links to repository-owned results. Archive historical parity logs separately.
- Verification: Docs match a clean checkout and its automated gates.

### VF-014 — shutdown reports an undisposed ComputeBuffer

- Severity: Low
- Confidence: Possible
- Evidence: Both visible player captures end with `GarbageCollector disposing of ComputeBuffer`. No `ComputeBuffer` use was found in first-party Assets code, so ownership may be Unity/package internals or a generated rendering path.
- Impact: Possible native resource lifecycle leak or harmless shutdown-only package warning.
- Recommendation: Reproduce in a Development Build with native allocation call stacks/Memory Profiler before assigning ownership.
- Verification: Warning disappears after the owning resource is explicitly disposed, or is documented as an engine/package issue.

## Healthy areas

- Unity native batch compilation succeeded with return code 0.
- Runtime/UI .NET compilation succeeded with 0 errors; 14 warnings are expected JsonUtility field-population warnings in the parity probe.
- Latest inspected Windows package build succeeded; visible players launched without gameplay exceptions.
- Save writes use flush, atomic replace, backup, and corruption-protection paths.
- Core/Content/Persistence assembly direction is clear; Core and Content avoid UnityEngine coupling.
- Runtime uses fixed-step simulation, arrays/pools, and avoids per-entity MonoBehaviours.
- Soundtrack clips are configured for streaming/background loading without preload.
- All inspected Assets have metadata; no orphan metadata or duplicate GUIDs were found.
- `.gitignore` correctly excludes Unity-generated Library, Temp, Logs, obj, and build artifacts.

## Recommended remediation order

1. Fix shared uGUI layout + status divider behavior; add screenshot/layout tests.
2. Remove synchronous menu plate baking; measure and gate time-to-interactive.
3. Bring tests into the repository, then split the runtime monolith safely.
4. Reduce texture memory/eager work and profile productionMax allocations/frame time.
5. Commit the UI, repair build identity/path/CI/docs, then close input/accessibility/release gaps.

## Validation baseline

- `dotnet build VoidFall.Runtime.csproj -t:Rebuild`: passed, 0 errors, 14 parity-probe warnings.
- `dotnet build VoidFall.UI.csproj -t:Rebuild`: passed, 0 errors, 0 warnings.
- Unity 6000.5.7f1 batch compile: passed, return code 0.
- Unity EditMode run: passed discovery, 0 tests found.
- Latest inspected Windows build log: `Build Finished, Result: Success`, 117,606,213 bytes.
- Visible 1920x1080 main-menu capture: 43.8 seconds process wall time to capture frame 30 and quit.
- Visible 1280x720 Settings capture: 48.1 seconds process wall time to capture frame 30 and quit.
- Player logs: no first-party exception/error; ComputeBuffer disposal warning on shutdown.

## Limitations

This is a static plus targeted runtime audit of an in-progress dirty working tree. Timing includes process launch, capture, frame 30, and automated quit; it is sufficient to confirm the stall but not a substitute for profiler markers. Likely/Possible performance findings must be validated with Unity Profiler and Memory Profiler before optimization work.

