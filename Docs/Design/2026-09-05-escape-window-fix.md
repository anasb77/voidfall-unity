# Escape collection window and crossing handoff

Owner request: fix the empty-Abyss departure experience; retain a 20–30-second
loot collection opportunity, then show a ten-second escape countdown.

## Behavior

- Completing the Void stops combat while keeping the player in place.
- For 25 seconds, display **INITIATING ESCAPE**. Movement, camera follow and
  normal pickup range remain active. XP and Parts are not vacuumed remotely.
- After that, display **ESCAPING IN 10**, then 9 through 1. The player can still
  collect nearby loot during the countdown.
- Pause, map, upgrade choices and roulette/prize screens stop the escape clock.
  Collecting the relic early does not reset or skip the remaining time.
- The boss relic remains at the boss's actual world position. If still unclaimed
  after the collection period, deliver its ceremony before the final countdown,
  preventing a permanent wait for an off-screen relic.
- At departure, ordinary uncollected pickups are left behind. A fork opens the
  existing visible portal crossing; a single exit travels directly. Neither
  path bypasses the collection/countdown window. Terminal reward/save behavior
  continues to use the existing journey flow.

## Confirmed mechanisms

The local player log recorded objective completion but no crossing or
arena swap. The reward phase previously waited indefinitely for the relic,
whose position was clamped to coordinates near world origin. Separately,
early prize Continue set the timer to zero, the grace delay was only 1.2 seconds,
the reward phase vacuumed loot and did not advance camera follow. These paths
explain the cleared-arena wait and premature departure without assuming every
reported empty view was a successfully loaded new arena.

Focused regressions reproduced six failures before edits. An additional test
reproduced the objective display overwriting the escape message. The escape
status now reasserts its cached display when the old objective text updates.

## Files and boundaries

Runtime changes are limited to the Journey, Rift, Roulette and RouletteChest
partials under `Assets/VoidFall/Runtime/Gameplay/`. Existing arena/visual/combat
edits were already present and were preserved. `EscapeWindowTests.cs` adds the
new tests; the existing relic-flow regression now expects the preserved timer.

`Runtime/RouteJourneyProbe.cs` supports `-vfjourney=collection|countdown` captures
away from world origin and allows 75 seconds per departure in the existing
whole-route check. These diagnostics use isolated profiles. It also enables
background updates and suppresses focus-loss pause only for the opt-in probe;
normal gameplay keeps its focus-loss pause. Five-second diagnostic state
records distinguish a paused check from a reward or travel stall.

## Verification

- Red regressions: `Logs/EscapeTiming/red.xml` and `status-red.xml`.
- Full current PlayMode suite: `Logs/EscapeTiming/playmode.xml`, 52 passed,
  0 failed, including the current golden master and seed sweep.
- Windows build: `Logs/EscapeTiming/build.log`.
- Player captures and full journey result are under `Logs/EscapeTiming/`.
- A snapshot of prior working-tree changes and the previous build are retained
  there. Current verification is scoped to this change, not a repeat repository audit.

The collection/countdown windows run in real active play time. Diagnostic
journey checks still fast-forward survival and defeat spawned bosses directly;
they do not prove full-length balance or performance.

### Final results

- Final Windows build: `build-final.log`, succeeded (207,148,195 bytes).
- Collection and countdown visual checks: `collection.png`, `countdown.png`.
- Visible crossing after the full window: `junction-debug.png` and its state log.
- Complete standalone check: `check-headless.json`, success, seed 2848592627;
  Abyss → Red Nebula → Monochrome Court → White Sakura → Null City, one saved run
  and Home reached. Collection/countdown timing ran normally at every stop;
  only survival/boss combat was accelerated by the diagnostic.
- The initial graphical check timed out while its background updates were
  disabled. The traced rerun returned from every reward call and reached the
  crossing. A later graphical run exited without a complete result and was not
  counted as a pass; the managed background/headless check completed with exit 0.
- The generated Addressables link metadata GUID was restored after validation.
  Parts, lifetime stats and recent runs matched the original profile after tests.

All artifact names in this subsection are under `Logs/EscapeTiming/`.
