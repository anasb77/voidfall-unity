# Mine cadence and control balance

Owner request: reduce mine placement frequency and control uptime, then retest
the Warden, Matriarch and Reaver matchups. The change is isolated from blast
damage, blast radius, arming time, lifetime, Clock and Boomerang behavior.

## Intended behavior

| Rule | Before | After |
| --- | --- | --- |
| Rank I–VI base placement delay, seconds | 1.6 / 1.6 / 1.35 / 1.35 / 1.15 / 1 | 2.4 / 2.4 / 2.1 / 2.1 / 1.9 / 1.8 |
| Actual placement delay after recovery and Overclock | Unbounded by mine-specific minimum | At least 0.9 seconds |
| Evolved freeze | 2.4 seconds, refreshed by overlapping explosions | 1.2 seconds, cannot refresh |
| Mobile recovery after freeze | None | 1.2 seconds before another freeze |

Freeze and recovery belong to an enemy spawn identity. A replacement in the
same pool slot gets fresh eligibility. Stale callbacks cannot decrement the
replacement's control timers. Travel and new-run reset clear both timers.
Bosses remain immune to the mine freeze; actual boss health, attack and movement
controllers remain unchanged by this weapon balance change.

## Telemetry

The existing run-history recorder receives bounded mine lifecycle events.
`mine_placement` uses reason `placed`, `nearby` or `pool_full`; successful
placement has an instance ID joining `mine_detonated` or `mine_expired`.
All use `id` and `sourceId` = `mines`. Rejected attempts have no instance.
Detonations use reason `base` or `evolved`, `amount` = newly frozen enemy count,
`blockedAttempts` = already frozen/recovering enemy count, and `durationSeconds`
= applied freeze duration (zero for base mines). Freeze counts describe control
applications before blast damage, so a target can subsequently die in that blast.
Boss damage remains available through the existing damage events.

These are pool-lifecycle and placement-cadence observations, not per-frame or
per-enemy history. Their frequency is bounded by the placement interval and
fixed 26-mine capacity. Immunity consumes no combat or cosmetic RNG draws.

## Verification fixture

`MineBalanceIntegrationTests` covers actual placement intervals at all six
ranks, maximum cycling/adrenal support with tier-III Overclock, repeated blast
control, spawn reuse, stale callbacks, transition reset and exported JSONL.
The boss cases spawn each real boss with seed 74821 and advance its controller,
adds, hostile shots and mines at 120 Hz for up to 30 seconds. They use only
rank-VI evolved mines with those recovery sources and a stationary target whose
health is replenished for diagnostic survival. They record boss health, damage,
placement/detonation counts and simulated time in `MINE_MATCHUP` test output.

This controlled exposure test is a regression and relative damage comparison;
it is not evidence of player survival, normal movement difficulty, or a full
six-minute encounter balance. Both profiles and exports use isolated temporary
directories and are removed after each test.

## Baseline evidence

Parent-owned Unity PlayMode run `Logs/mine-balance-red.xml`: 3 passed and
10 failed as expected before implementation. All three actual boss diagnostics
passed. The cadence/control/export regressions failed on the old behavior:
26 mines placed in six seconds at maximum tested recovery, and all 280 ticks
frozen under repeated blasts. Rank delays measured approximately 1.608, 1.608,
1.358, 1.358, 1.158 and 1.008 seconds, including fixed-step rounding.

| Boss, 30-second fixture | Starting HP | Baseline remaining HP | Baseline boss damage |
| --- | ---: | ---: | ---: |
| Warden | 3,200 | 2,080 | 1,120 |
| Matriarch | 4,300 | 800 | 3,500 |
| Reaver | 5,600 | 2,380 | 3,220 |

Baseline mine placement/detonation values in the test output are zero because
those history events did not yet exist; they do not mean no mines fired.
Matriarch's baseline total weapon damage was approximately 3,911, including
damage to adds; use 3,500 for damage to the boss itself.

## Post-change evidence

Parent-owned Unity PlayMode run `Logs/mine-incident-focused.xml`: all 13
`MineBalanceIntegrationTests` cases passed, checked directly in the XML. The
combined focused run passed 34 cases with zero failures. The six rank cadence
tests, maximum-recovery minimum interval, unrefreshable freeze/mobile recovery,
identity reuse/reset and actual exported JSONL assertions all passed.

| Boss, same 30-second fixture | New remaining HP | New boss damage | Change from baseline | Placements / detonations |
| --- | ---: | ---: | ---: | ---: |
| Warden | 2,220 | 980 | −12.5% | 8 / 7 |
| Matriarch | 1,780 | 2,520 | −28.0% | 21 / 20 |
| Reaver | 2,380 | 3,220 | Unchanged | 24 / 23 |

Matriarch's new total mine damage was approximately 2,931 including adds.
The same three real boss controllers continued moving in the new run. The
measured result supports lower mine output in the Warden and Matriarch fixture;
it does not support a claim that damage falls in every boss matchup. Reaver
still receives the same 23 rank-VI mine hits in this stationary exposure test.
Player movement and encounter timing can change which mines actually connect.

Scoped `git diff --check` passed. No standalone Editor or build was launched
by the mine implementation agent. Integrated checks and player delivery remain
owned by the parent task.
