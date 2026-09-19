# September 19 — Spiky crowd shove, readable content and Director I v6

## Owner request and implementation

- Spiky expansion pushes nearby mobile enemies radially outward, including Exploders. Shrink phases never attract neighbors. Growth is still 19.5 to 58.5 radius on alternating 0.5-second phases, with the existing high-resolution sprite. A fixed identity-keyed sidecar tracks positive radius change; a spatial-grid pass applies displacement and capped added knockback after movement, before ordinary separation. Anchored units, Court sentinels and Matriarch bodyguards remain anchored. Arena constraints are applied. No new direct player damage or combat RNG.
- HUD objective uses bold bundled Chakra Petch at 1.45 study units (previously .7), brighter near-white with a shadow and wrapping below HP. Arsenal/passive/legendary labels use bold Chakra Petch at 1.15 (previously .66). The overall HUD remains .66 scale.
- Main-menu button labels, card content, pause and result bodies use the bundled timer font family. Existing card/screen titles and icon glyph faces remain intact. Workshop and dealer art are not rethemed.
- A large clear (at least 35 bodies observed, at least 65% killed within the observation window, at most 35% remaining) buys 1.25 seconds of rest, then up to 120 familiar fodder arrivals over at most three seconds, eight every .12 seconds from alternating offscreen edges. A ten-second retrigger cooldown prevents chained bombs from constantly resetting the director. Existing admission limits and safety windows still apply; no on-screen spawn or health/damage scaling is added.
- Standard charging Elites are scheduled independently of ambient batches and population targets. First due at authored 55 seconds, repeat about 85 seconds initially, tapering to 55; safe-window deferrals retry every three seconds without a backlog. At most two standard Elites active. Variant cadence now uses the real Director I threat budget and introduced-family gates; unsuccessful admission does not consume the next interval.

## Evidence for the elite bug

Owner run efcd209eae57433b92d0899e3c3fc9e5 lasted 854.24 seconds with 16,153 kills, five elite kills and two boss kills. Its JSONL contains no enemy_spawn with id=elite. Five elite variants spawned: Exploder at 356.95s, Mortar at 548.41s and 608.45s, Exploder at 669.27s and 729.69s. The standard scheduler was below the sustained-director early return; variant selection used an older, smaller threat budget. This confirms the complaint despite the summary's five elite kills.

The first recorded bomb at 190.916s reduced a roughly 470-body population to zero. Samples show 15/43/59/75/97 bodies at 191.62/192.50/193.36/194.32/195.23s, with 4/25/37/44/71 on screen. The new bounded reinforcement burst is intended to shorten this rebuild while preserving a clear reward/breathing beat; its subjective feel remains a playtest question.

## Validation

First PlayMode pass: 46/46 passed, including shove direction/anchors/shrink, post-clear rest/admission/offscreen locations, standard cadence and exported JSON/JSONL. Existing pinned legacy and full-state golden hashes passed unchanged.

Final PlayMode pass: **48/48 passed**, including the dense-fodder variant regression and the 32-seed productionMax sweep (each seed simulated twice). Both pinned hashes remain unchanged. XML: Logs/spiky-readability-final.xml. A final cosmetic addition applies the same readable font to the level-up header kicker and footer controls, preserving the title; compilation/native captures cover it. The first canonical native build succeeded and the HUD, cards, pause, result and main-menu screenshots were inspected. Native Spiky growth produced outward displacement and a matching history event. A follow-up transition-export test exposed/closed a final partial-window gap: RecordEnemyRemoval now flushes growth totals before clearing the slot; its focused test passed (1/1, Logs/spiky-transition-export.xml). The final build includes this telemetry-only follow-up.

Native checks use isolated profile/run exports and monitor two (\\.\DISPLAY1). Gameplay/HUD/card/pause captures are 1280x720; the diagnostic monitor-selection callback applies the isolated profile’s native 1920x1080 mode for settings/result/menu captures. Windows display preferences are restored and compared with their pre-capture snapshot afterward. The owner still needs to judge the long-run difficulty and subjective pacing in ordinary play.

## Final handoff

Canonical player rebuilt successfully at 2026-09-19 22:44:59 +01:00; GUID **1e14fc76f90e4b7689e936cabf555cc1**, 874,607,732 bytes. Build log: Logs/spiky-readability-build-final.log. The exact replacement player completed a second isolated native capture with matching GUID and Director I v6. No error/exception/failed entries appeared in the native log. Final screenshots and export evidence: C:/Users/AB/.codex/visualizations/2026/09/19/01a0b9b9-c6e3-7cc3-9856-b08efaa53c68/spiky-readability/native-verified/.

Native shove evidence includes the initial 7.39 units of aggregate displacement plus 285.88 units flushed on arena removal (contact-step counts 8 and 60; these are totals across neighbors, not one enemy’s travel). Monitor check passed on secondary display DISPLAY1. Screenmanager preferences were restored and verified against the original snapshot after the player exited. User profiles and ordinary RunExports were not used for diagnostics. No alternate release build was created.

The second launch’s hud-full-build capture includes the pause overlay; the unobstructed HUD composition was visually checked in native-final/hud-full-build.png before the telemetry-only follow-up. The visual code is identical between those builds.
