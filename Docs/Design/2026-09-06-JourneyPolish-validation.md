# Journey polish validation — September 6, 2026

Implemented in the active Unity project and rebuilt `../Builds/VoidFall.exe`.

## Verified behavior

- Fifteen active seconds, staggered harmless enemy explosions, and complete XP/Parts settlement before travel; pending upgrades are retained.
- Overclock remains at its full charge through escape and resumes on arrival.
- Animated `Escaping...`, three escalating shake patterns, native destination portal colors and names, and no redundant post-roulette confirmation.
- Minimal thumbnail map and connected route planning, with explicit arena identity separate from route-node IDs.
- Correct Eon Sea/Crascendo arrival and fullscreen-fold retirement. Fold panels use non-crossing strips. Newborn enemy appearances continue during the safe escape phase.

## Evidence

All paths below are under `Logs/JourneyPolish/` in the active project.

| Check | Result | Evidence |
|---|---|---|
| Full isolated EditMode | 438 passed | `isolated-editmode.xml` |
| Full isolated PlayMode | 125 passed, 1 known baseline failure, 1 graphics-only skip | `isolated-playmode.xml` |
| Integrated journey, runtime and 32-seed sweep | 63 passed | `integrated-playmode.xml` |
| Arrival regressions and related runtime checks | 43 passed | `arrival-green.xml` |
| Final appearance/overlay regressions | 4 passed | `visual-green.xml` |
| Left route | Six Voids, Home, one save | `route-left.json` |
| Right route | Six Voids, Home, one save | `route-right.json` |
| Native map | Inspected centered title, thumbnails/names and current marker | `map.png` |
| Native escape | 59, 29, then 0 enemies at captures; charge stayed at 15 seconds | `player-delivery.log`, `delivery-escape-0.png` through `delivery-escape-2.png` |
| Native arrival | Correct Eon Sea, clean view, charge resumes | `delivery.json`, `delivery-arrival.png`, `delivery-arrival-settled.png` |
| Windows build | Succeeded, 233638540 bytes | `delivery-build.log` |

Runtime DLL SHA-256: `351670A3F3143288CFED3A04CAE985575DD17CFBFE53F162C28ED7EEED72BA37`.

## Existing gate

The pinned simulation check expects `14088908808337278323`, but the pre-task snapshot `e649703` and the journey implementation both produce `16175583525682867059`. This was independently reproduced by restoring the pre-task C# and running the check (`pre-change-golden.xml`). The reference hash was not changed. The separate 32-seed determinism sweep passes. Full-suite success is therefore not claimed.

Native journey probes accelerate survival and boss defeat, and automate reward/upgrade choices. They verify handoffs, assets, recovery and terminal saving, not full-length combat balance. All probes use isolated profiles.

