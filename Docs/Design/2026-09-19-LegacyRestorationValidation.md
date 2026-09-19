# Legacy restoration validation — September 19, 2026

Target: canonical `../Builds/VoidFall.exe`, Unity 6000.5.7f1, Windows x64 / DX11. No second release build. Source was copied without Git metadata; the pre-implementation source snapshot is `C:/Users/AB/.codex/visualizations/2026/09/19/01a0b9b9-c6e3-7cc3-9856-b08efaa53c68/legacy-comparison/source-before-implementation.zip`.

## Asset validation

Final procedural bake succeeded: 445 unique catalogue entries/sprites; 377 atlas-safe entries. All 320 original sprite GUIDs match the source snapshot. Existing catalogue-key paths are preserved, and new sprites receive extra-key filenames. Log: `Logs/legacy-restoration-bake-final.log`.

## Simulation baseline diagnosis

The first full PlayMode run found a stale September 8 golden pin. A controlled Editor-only check temporarily restored all 331 original C# files from the snapshot, excluded the seven new C# files, and ran the exact golden fixture. All 338 current C# files were then restored. No baseline player was created.

Both original and updated source produced legacy meteor-schema hash **13284412825273198999** and full hash **4160117082910864886**. The stale expected pair was 5768066926572862399 / 8219498908681263610. The updated game therefore adds no further hash drift in this productionMax fixture. The full 32-seed repeatability sweep passed before correcting the constants. Original-source evidence: `Logs/restoration-baseline-golden.xml`; updated-source evidence: `Logs/restoration-playmode.xml`. The baseline log is in the outer workspace `../Logs/restoration-baseline-golden.log`.

## Test runs

Initial full EditMode: 633 total, 628 passed, 5 failed. Those failures covered the appended support count, new icon mappings, default chromatic setting, and imported roster-II catalogue scope. Fixes preserve old indices, limit the imported-forms assertion to eligible imported families, map the new card icons, and assert the new default.

Initial full PlayMode: 326 total, 322 passed, 3 failed, 1 skipped. Failures were the stale golden pin above; a new projectile test fixture whose zero-age enemy was correctly ignored by targeting; and an existing physical-relic flow test that expected instantaneous crossing entry. The revised tests use an eligible-age target and assert Travel followed by covered Junction entry. The skipped Workshop screenshot test requires a graphics device; headless checks do not validate Workshop appearance.

Final full EditMode rerun: **633/633 passed**, no skips (`Logs/restoration-editmode-final.xml`). PlayMode reruns: golden + runtime-flow + new integration filter yielded 19/20 passing with only the new projectile fixture still failing; its spawn-age assignment had mistakenly matched the similar shuriken fixture first. Corrected the exact projectile fixture and reran the full new integration class: **7/7 passed** (`Logs/restoration-integration-final.xml`). Together with the original full run, all 325 executable PlayMode tests have passing evidence; one existing graphics-only Workshop test remains skipped.

## Windows build and native inspection

Final canonical build succeeded: **873,172,036 bytes**, Unity 6000.5.7f1 / Windows x64 / DX11. Build log: `Logs/restoration-windows-build-final.log`. Provenance:

```text
VoidFall — canonical Windows build
Built (local): 2026-09-19 18:13:47 +01:00
Built (UTC): 2026-09-19T17:13:47.8178323Z
Build GUID: 053625603c6d4a19b972dfd0e989da5f
Source project: C:\Users\AB\Desktop\voidfall-unity-main\voidfall-unity-main
Source revision: working tree
Unity: 6000.5.7f1
Windows renderer: Direct3D11 (Direct3D12 opt-in diagnostics)
Launch: C:/Users/AB/Desktop/voidfall-unity-main/Builds/VoidFall.exe
```

The canonical player completed `-vfrestoration-check` with an isolated, muted, windowed profile. Requested `-monitor 2 -screen-fullscreen 0 -screen-width 1280 -screen-height 720`; secondary placement was requested but not independently confirmed because the Windows screenshot helper returned an interface error. Internal player screenshots worked. The initial hidden-window screenshots were black; repeating with a visible window resolved capture. The harness was corrected to wait for UI entrance animation and call RenderJunction, just as the production frame loop does. The final run logged three selected card offers and ended in Junction.

Visual inspection: six Pulse Pistol ranks; restored HP 100/100, timer/level/pressure and XP presentation; mixed five-family enemy layout; Second Wind, weapon-bound Split Shot and Phase Rounds text; covered relocation and settled crossing with original dealer and both destination portals. These are staged diagnostic poses, not a natural-run balance or performance benchmark.

Capture directory: `C:/Users/AB/.codex/visualizations/2026/09/19/01a0b9b9-c6e3-7cc3-9856-b08efaa53c68/legacy-comparison/native-restoration`. Final log: `player-final.log`; final screenshots: `pulse-rank-1.png` through `pulse-rank-6.png`, `cards.png`, `crossing-2.png`, `crossing-7.png`, `crossing-12.png`, `crossing-22.png`. The isolated run exports also completed. No native exceptions were logged. Shutdown logged a ComputeBuffer disposal warning; its source was not established in this pass.

## Remaining limits

Ready for owner playtesting. Full-run difficulty, 15-minute reveal pacing across route combinations, chain-reaction strength and hardware performance still require normal playtesting. This pass does not establish wishlist conversion or long-run performance. Existing graphics-only Workshop test was not executed. No alternate release executable was created.
