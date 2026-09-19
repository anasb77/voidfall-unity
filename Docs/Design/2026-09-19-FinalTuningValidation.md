# September 19 final tuning validation

Canonical player: ../Builds/VoidFall.exe
Build GUID: 1fa1f41531f0427f90030bf4c5ba0518
Built: 2026-09-19 21:35:00 +01:00

## Automated evidence

- Logs/final-tuning-edit-final.xml: 53/53 passed, including save defaults/round-trip, XP, capped Overclock, audio behavior and prepared sprite resolution/world-size checks.
- Logs/final-tuning-play-final.xml: 60/61 passed. The sole failure was the new exporter test checking sourceId instead of the established id field. Corrected assertion passed in Logs/final-tuning-export-check.xml (1/1); production export data did not require a fix.
- Logs/final-tuning-mute-check.xml: 1/1 passed after using actual button dimensions so press feedback cannot reset the 35% reduction. Covers settings snapshot/restore and pointer press/release.
- Logs/final-tuning-play.xml: the 32-seed repeatability sweep passed. Initial Clock/Boomerang expectations were updated for the approved values and passed in the final run.
- Controlled baseline audit: temporarily restoring ONLY previous XP requirements and ordinary rare-drop odds exactly reproduced old legacy/full hashes 13284412825273198999 / 4160117082910864886 (Logs/final-tuning-baseline-audit.xml, 1/1). Final approved rules yield 4858828874924286091 / 10686876280106228379; the updated golden-master check passed in the final gameplay suite. Audit changes were restored before baking/building.
- Logs/final-tuning-bake.log: 445 catalogue keys / 445 unique sprites, 377 atlas-safe sprites. Existing key-based asset paths preserved.
- Logs/final-tuning-windows-final.log: canonical Windows build succeeded, 874601588 bytes.

## Native evidence

Captures: C:/Users/AB/.codex/visualizations/2026/09/19/01a0b9b9-c6e3-7cc3-9856-b08efaa53c68/final-tuning/native-final

Visible Direct3D11 player, isolated muted profile, 1280×720 window on secondary display \\.\DISPLAY1 (Unity Monitor 2). MainWindowPosition (320,180) is relative to that display. Monitor setting index 1 was saved in the first native check and loaded on the final startup. The same UI callback/async window-move path was exercised, with passed=True in monitor-check.txt. No test profile overwrote real progression; Screenmanager preferences restored after capture.

Inspected hud-full-build.png, clock-boomerang.png and graphics-monitor.png: smaller mute control below score with full support grid clear; Regular outline has no enclosing hard circle; restored seven-point Runner; normal/giant Spiky remain sharp; reduced Clock face preserves hand opacity; Boomerang artwork and settings monitor row render. Other diagnostic frames cover pulse ranks, low health, slot tooltip and covered travel. complete.txt confirms completion in Junction.

The first hidden-window capture produced black images and is NOT visual evidence. Only native-final captures are accepted. Final player log has no new exceptions; the pre-existing ComputeBuffer disposal warning at process shutdown remains outside this patch.

## Limits

Automated coverage establishes behavior and deterministic simulation, not that the new difficulty/XP/drop rates are subjectively balanced. A normal owner playtest remains the balance assessment. No FPS claim is made from this staged capture. Physical hot-unplug was not tested; disconnected-index fallback is implemented.
