# Director redesign — approved September 7, 2026

The owner approved the complete redesign proposal and browser event studies: "i approve everything start implement it it all on unity". Approval covers the complete incremental implementation, not just the first prototype. No repeated permission gate is needed for these slices.

Primary rationale/evidence: sibling ../Director-Redesign-Assessment-2026-09-06.md (relative to repo root). Original owner brief remains the governing constraints. Baseline: acf510334716d14d4acaa27dcb5be7dca3ada928. Active implementation worktree: ../voidfall-director-worktree on codex/director-redesign-2026-09-07. Baseline EditMode 444/444 passed on September 7; PlayMode is in progress.

## Approved scope

- One shared director with I/II/III profiles; real recovery and destroyable fodder; finite crossing packs and firing cycles; boss engagement windows; no spawn debt; explicit body/attention/reinforcement budgets.
- Visible pressure starts at 0.00 under timer after brief empty arrival, never decreases, survives travel. Current six-visit run: 80% survival/20% irreversible encounter progress per visit; caps 3/5/9 initially. Use authoritative scaled combat progression, no safe/menu/loading/pause growth or boss-wait farming.
- Final score is all earned base score times frozen end multiplier, floored at1.00 below displayed1.00. Hundredth precision, 64-bit calculation, nearest whole point once (halves up). Finite progression bonus and descendant reward provenance prevent endless XP/score/Parts farming. Preserve approved escape loot/reward settlement.
- First Play DirectorI; completed win OR loss introduces selection, then remember choice. Existing completed players get the introduction. Save schema changes must preserve old records and avoid re-triggering historical refunds. Frozen result drives UI/save/telemetry consistently.
- Black Hole: inspect/adapt supplied Free Blackhole Shader Unity URP.unitypackage. Fixed telegraphed world center; bounded attraction only player/ordinary enemy bodies; enemies ~3x player; baseline escape; no damage/consumption/stun or slingshot. STANDSTILL retains input-based semantics. Prototype2.5s warning/10s active/1.5s release. No bosses/elites initially.
- Destroyer faction raid: actual reciprocal damage and local stable targeting with player/ordinary/Destroyer sides. Finite squad and withdrawal, once-only death, collectible rival XP, player contribution score. Preserve identity/order and provenance through projectiles/children/effects. Native Court labels are not faction implementation.
- Latest approved Destroyer art is ../Prototypes/incident-preview/destroyer-art.js and artwork-v3 exports, AFTER restoring Razor and making Husk/Grasp Maw variants. Five: Maw rusher (22 teeth); original hooked-wing Razor rusher (NO spider); broad armored Maw-like Husk brute; four-toothed-jaw Grasp brute (NO hand/round eye); Spite creature with bow-like mandibles/throat attack (NO literal crossbow). Black interiors/white edges, restrained detail. Browser values are starting points, not permission to replace native Unity combat.
- Atmospheric Eclipse only: environment darkening (~80% initial), warning, 18s active, smooth fade; never tint/obscure player, enemies, projectiles, telegraphs, HUD. No visibility restriction. Respect reduced effects/contrast.
- Major incidents one-at-a-time, occasional2–4 full-run hypothesis, no quota/debt. Admission in validated open survival arenas; begin Abyss and validate Sakura/Nebula individually. Exclude specialized Court/Hydra/NullCity/EonSea/Crascendo initially, all bosses/lead-ins/rewards/travel. Native roster/hazard ownership remains.
- Representative advancing performance probes; physical capacity candidates300/500/1000 only benchmarks, not launch requirements. No FPS-dependent gameplay scaling. Reference laptop Ryzen5900HX/RTX3080Laptop; minimum spec still unspecified, so no minimum-spec certification.

## Preserved boundaries

Finite six-visit player-chosen route across the eight prepared arenas starts in Abyss. Preserve fifteen-second escape, earned XP/Parts, Overclock freeze, roulette, saves, maps, pause ownership, current terminal escape. No additional arena/ending/story project. Core/Content engine-free; fixed step, struct pools, order tables, spawn identities and combat/FX RNG separation. No per-enemy MonoBehaviours.

Online ranking backend, restricted Eclipse, unapproved events, new trees, co-op and1000-enemy launch requirement remain out of scope despite the broad approval. Record local facts for future ranking and label incompatible score versions.

## Delivery rules

Implement all approved slices without asking again; each slice receives focused tests and review. Keep implementation/status in 2026-09-07-DirectorImplementation.md and its ledger. Parent controls Unity test/build executions; do not run multiple Editors on one project. Explain any golden-master drift and pass32-seed sweep before re-pinning. Build and visually inspect actual effects and final player. Report human fun/balance and missing minimum hardware as limits, not successes.
