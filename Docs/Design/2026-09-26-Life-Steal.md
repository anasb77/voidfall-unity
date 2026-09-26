# Life Steal — implemented

Owner-approved September 26, 2026. The five-rank card and values below are
locked. The owner subsequently authorized implementation directly in the main
project alongside the unified drops, Scavenger and approved HUD/art revision.

| Rank | Healing | Kill threshold |
|---|---:|---:|
| I | 0.2 HP | 200 kills |
| II | 0.4 HP | 200 kills |
| III | 0.5 HP | 200 kills |
| IV | 0.7 HP | 200 kills |
| V | 0.9 HP | 200 kills |

Each rank replaces the preceding rank's effect. These are flat HP amounts,
not percentages of maximum health or damage dealt.

## Owner's balance direction

Life Steal supplements the complete healing stack. Long runs can eventually
acquire and fully upgrade every support card; do not balance it as an exclusive
alternative to Regenerator. Preserve scarce healing, health tension, and reasons
to seek healing pickups as other healing and shield cards are introduced.

The four-run September 26 review found approximately 2,000–2,080 player combat
kills per simulation minute in the latest run's later arenas. At 2,000 kills
per minute, these ranks offer 2 / 4 / 5 / 7 / 9 HP per minute before overhealing.
Those runs predate the 0.20-second hit-immunity and 1/500 ordinary power-up-drop
changes. The earlier 1–5 HP proposal was withdrawn and is not approved.

## Runtime contract

`SurvivalSupportCatalog` appends `lifeSteal` after existing support IDs. Only
rewardable player-finished enemy combat deaths count; escape cleanup, rival
kills and unowned kills do not. Progress begins at acquisition, survives rank
upgrades and arena transitions, and resets at a new run. Every 200 eligible
kills consumes a threshold, including at full health. Healing is fractional,
capped at maximum HP, with no stored overhealing.

`VoidFallGameRuntime.SurvivalSupports.cs` owns counters, healing and telemetry.
`support_proc` exports actual HP restored and requested healing, rank, threshold,
remainder and triggering enemy identity. Context/progress fields identify the
rules and counters. Focused tests inspect actual exported JSON/JSONL.
