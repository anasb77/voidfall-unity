# Run export delivery evidence

Validated September 8, 2026 in `voidfall-director-worktree`, based on commit
`8f1fe6807fb68ce53393f946c8dfe5c105cb1e8f` plus uncommitted telemetry work and
concurrent owner-authorized audio/roulette changes. No gameplay population,
director pacing, arena-size or enemy-health tuning was performed by this task.

## Automated evidence

- `Logs/runexports-delivery.xml`: **23/23 passed**, comprising storage, runtime
  exporter integration and the parallel task's roulette-claim integration.
  Covers unique run files, silent automatic checkpoints/finalization, spawn/
  damage/death/drop joins, offers/selections, revive context, outgoing visit
  identity, score previews without freezing, death/victory and failed profile
  storage independent of export storage.
- `Logs/runexports-regression.xml`: initial **53/53 passed**, including journey,
  pressure and deterministic simulation/32-seed sweep.
- `Logs/runexports-final.xml`: later broader run **55/57 passed** after parallel
  roulette work introduced explicit prize claiming. Remaining old journey tests
  `Map_shortcuts_cannot_dismiss_roulette_and_its_completion_releases_navigation`
  and `Upgraded_parts_cache_awards_the_displayed_amount_exactly_once` still
  expected immediate rewards. Do not describe that run as all green or change
  reward behavior to satisfy these old expectations. The subsequent focused
  23-test delivery run verifies exports through the new claim flow.
- `Logs/runexports-enabled-golden.xml`: fingerprint passes with journal writing
  enabled; its actual report has 621 records, zero reported drops/errors.

## Windows player evidence

`Logs/runexports-windows-build.log`: `BuildScript.BuildWindows` succeeded,
exit 0, 256815133 bytes, at `../Builds/VoidFall.exe`. Runtime build GUID captured
by the player: `17155a27ee1c45c095fabc2575f9cc86`.

An isolated `productionMax` diagnostic player ran with `-vfbench`, one-second
warmup and five-second measurement. No exporter path override was used: the
normal destination was verified beside the executable at `../Builds/RunExports`.
Profile isolation came from the existing benchmark probe, not a new save system.

Actual paired files:

`../Builds/RunExports/voidfall-run-5e6c61b8125848e299ed57ae02487b2c.json`

`../Builds/RunExports/voidfall-run-5e6c61b8125848e299ed57ae02487b2c.jsonl`

Report: `diagnostic`, `quit`, **831 written, 0 dropped, 0 errors, 0 pending,
closed=true**. Every line parses and sequence order is increasing. The journal
contains actual director choices, rejected spawns, 218 enemy spawns, 33 enemy
deaths, damage windows, drops/merges/collections, XP credit, upgrade offers and
applied upgrades, boss states/damage, and exactly one run_end. This is a
synthetic workload, not owner balance feedback or GTX 1060 performance proof.

The longer diagnostic in `Logs/RunExportsInterrupted` completed normally before
the attempted termination, writing **4848 records, 0 drops/errors**. It is soak
evidence, not forced-interruption evidence.

## Forced-close retention

A separate, isolated, agent-owned probe was forcibly stopped after its journal
began writing. In `Logs/RunExportsForced`, run
`a7990092e0214a5186ad66d054a2f1fb` retained **416 complete parseable lines**,
followed by one interrupted partial line. All lines before that last line parse.
Summary stays `active`; there is no fabricated run_end. See `Docs/RunExports.md`
for how readers handle this explicitly incomplete run. Recent buffered data
cannot be guaranteed after forced termination/power loss.

No test/diagnostic player remains running. Real progression was not used by
the player probes. Unrelated background edits were preserved.
