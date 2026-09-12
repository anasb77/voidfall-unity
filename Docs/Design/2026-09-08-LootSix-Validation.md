# Loot v2 / six-minute survival — validation

Owner scope: fix loot reachability, not physical arena boundaries; make every
void survival phase six minutes. Weapon-slot and further difficulty changes
remain separate. See `2026-09-08-LootReachability-SixMinutes.md` for the contract.

## Test evidence

- `Logs/loot-six-red.xml`: old behavior reproduced the loot/timer failures.
- `Logs/loot-reentrant-red.xml`: the new compaction path initially collected
  four pickups instead of one in a Bomb-style callback. Generation snapshots
  corrected this same-tick processing bug; the regression now passes.
- `Logs/loot-six-final-playmode.xml`: **247 passed, zero failed, one graphics
  skip** (248 total). Includes the 32-seed repeatability sweep, loot value/
  charge conservation, reserved capacity, callback reentrancy, Greed, Null City
  bounds, roulette claims, all six-minute runtime boundary checks and real
  Warden HP/tier evaluation at six minutes.
- `Logs/loot-six-editmode.xml`: 565 passed; the one old Crascendo midpoint test
  still used 150 seconds. It now uses half the configured survival duration;
  `Logs/loot-six-editor-final.xml` confirms both Crascendo tests pass. No
  production behavior was altered to satisfy that outdated expectation.

The golden update is intentional for loot allocation/collection and six-minute
diagnostic progression. Before re-pinning, the 32-seed sweep passed. Measured
legacy hash5768066926572862399 / full hash8219498908681263610 are documented in
`SimulationGoldenMasterTests.cs`.

## Built player evidence

`Logs/loot-six-build.log`: successful Windows build, exit0,256836765 bytes at
`../Builds/VoidFall.exe`. Observed build GUID:
`62d4c50b460f42868e17244165a9dfe7`.

Rendered 1920×1080 diagnostics used isolated profiles and export directories.
The 750-enemy workload (`Logs/LootSixLoad/benchmark.json`) held min/max750
across1153 steps with zero refill failures. Its journal contained11558 records,
zero logging drops/errors and zero XP-accounting gap. It recorded238 loot
relocations,76 consolidations and **zero rejected pickup spawns**. These are
synthetic load results, not owner difficulty feedback.

The normal Director I capture (`Logs/LootSixNormal/benchmark.json`) advanced
66.64 run seconds with116 kills and49 face-value XP collected. Its1513 journal
records had zero drops/errors and zero XP-accounting gap. Metadata identifies
loot policy2 and survival360. `six-minute-hud.png` visibly shows the live
`Survive ... / 06:00` objective. This short capture does not replace the runtime
tests that verify the actual360-second boss boundary.

No test player or Editor was left running. Real user profiles and existing
exports were not used by the diagnostic players. Unrelated concurrent work was
preserved. Automatic player exports still go to `RunExports` beside the game.
