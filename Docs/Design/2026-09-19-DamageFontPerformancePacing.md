# Damage font, performance investigation and pacing — 19 September 2026

Scope: apply Chakra Petch Bold to pooled damage numbers; investigate the last two owner runs; propose pacing changes without applying them. Baseline source: 4e871f0. The owner's final deaths were voluntary and are excluded as difficulty evidence.

## Real run evidence

| Run | Director | Route | Length | Frames >50 ms | Worst frame |
|---|---:|---|---:|---:|---:|
| 90f35e7488ae4a0f812f77b914b09826 (latest) | 6 | Abyss → Red Nebula → Hydra I | 13:43 | 358 / 52,733 | 533.41 ms |
| efcd209eae57433b92d0899e3c3fc9e5 | 5 | Abyss → Eon Sea → Red Nebula | 14:14 | 87 / 55,492 | 649.98 ms |

Both reports record i7-7700HQ / GTX 1060 6 GB, 1920×1080, DX11. They have different builds, routes, weapons and focus histories, so these are not controlled before/after performance tests. Both complete journals report no dropped events. Use frame durations: the prior FPS conversion multiplied results by 1000.

Latest run: sampled frames repeatedly hit 83–150 ms at combat time roughly 4:06–4:15 with 509–610 enemies. Spiky is not introduced until 7:00, so its shove cannot explain that earlier interval. Rank-I Clock acquisition at 33.644 s precedes the 533 ms sample at 33.69; rank II at 90.509 precedes the 383 ms sample at 90.56. Isolated cold raster measurements in the Editor on this machine take 423 ms for Clock I and 414 ms for Clock II (ranks III–VI: 472, 482, 499, 546 ms). That establishes costly synchronous sprite work in the upgrade path; matching timestamps support it as a contributor to those spikes, without proving every millisecond's cause.

## Implemented

- Pooled combat floaters use the HUD timer's bundled Chakra Petch Bold, retaining existing size, color, movement and lifetime.
- All 49 arsenal sprite variants are baked through the existing prepared sprite catalog. Production lookups return persistent assets, including the alternate Clock hand. No art geometry, weapon behavior or director tuning changes. RawImage consumers retain standalone textures. Cleanup preserves Resources ownership.
- FPS uses 1/dt; context.frameTimingVersion=2 distinguishes corrected exports. Existing frame-duration evidence stays valid.
- Existing one-second telemetry now reports bounded CPU script phase windows, managed heap size and GC collection counts. No new logger or per-frame file writes. These distinguish script work from unexplained frame time; they are not GPU profiler timings.

## Pacing findings

Samples, excluding bosses: final Abyss minute (5:00–6:00) versus the first 60 seconds after next-void arrival grace:

| Run | Late Abyss visible enemies | Next void visible enemies | Late Abyss total | Next void total |
|---|---:|---:|---:|---:|
| latest | 204 | 40 | 567 | 80 |
| previous | 190 | 57 | 295 | 78 |

These are unweighted means of once-per-wall-second samples, not exact continuous occupancy. Windows in raw data: latest 300–360 and 407.622–467.622 combat seconds; previous 300–360 and 390.83–450.83. Across run minute seven (7:00–8:00), visible populations are 45 and 65. Higher player level and weapon ranks carry across, while the ambient batch/interval resets with local arena time: roughly 46.9 arrivals/sec before late-boss throttling → 10/sec at the next opening. Final 30 seconds block tactical beats/swarms/elites; final 15 seconds throttle ambient arrivals to 2/sec. This stacks an extended quiet section, boss/escape/crossing, arrival grace and a fresh early-game ramp.

The latest final Abyss minute averages 363 enemies off screen. More total actors alone can spend CPU without improving immediate pressure. Late-Abyss spawns are dominated by the basic chaser. New enemy reveals are deliberately spread out, but repeated combinations can add novelty without prematurely revealing the roster.

Regular tactical encounters are firing: the latest run records 19 selected encounters and 26 circle deployments; the previous records 23 and 31. In the latest late-Abyss section, Hunt, Flank and Pursuit all deploy before the final countdown. The issue is not an entirely inactive director. The proposal is more distinct, escalating combinations and a stronger post-transition floor, rather than blindly adding another timer.

Each run got one major incident: Eclipse. The implementation waits 240–300 seconds after completion. Eon Sea is ineligible for the generic incident set. The latest Red Nebula opportunities were repeatedly deferred by pacing/safety or meteor-attention checks. Do not remove meteor safety gates just to force incidents; offer a different kind of beat when the hazard budget is occupied.

## Recommended next tuning pass — proposals only

1. Carry momentum between voids: preserve the 2.5-second arrival grace, then test a later-void minimum of about 20–25 ambient arrivals/sec, ramping toward 30–35 over the first 30–45 seconds. Use journey/arena progression, not hidden scaling against player DPS. Keep Abyss's first-run opening unchanged.
2. Give late Abyss two authored combinations around 4:30 and 5:15: for example an elite with a broad fodder escort, then a warned rusher flank while another group holds a lane. Use known enemies, existing attack limits and a readable escape lane. Short recovery beats should make the next push distinct.
3. Shorten the pre-boss wind-down to a deliberate 5–8 seconds, tuning threat types before adding bodies. Revisit the current final-30/final-15 throttles together; do not accidentally overlap an unwarned special attack with the boss introduction.
4. Schedule a meaningful encounter opportunity in each eligible void, with a bounded retry and an alternate formation when meteor/incident budgets block the chosen event. Keep hazards safe; do not simply stack more visual noise.

Judge the next pass by visible occupancy, time to renewed contact after a clear/arrival, distinct beats experienced, and slow-frame counts—not kills or intentional final deaths alone. Preserve six-minute survival and the delayed roster reveals.

## Validation

Prepared lookup verification: all 49 asset identities match the persistent catalog and survive runtime cleanup. The 49 reflection lookups plus assertions took 5.55 ms total in the Editor; this is not a full-frame or native GPU measurement. Distinct-rank pixel validation now reads baked PNGs in EditMode, since production textures intentionally have no CPU-readable duplicate.

Passing coverage: 13 arsenal gameplay checks, 19 run-export integration checks, 2 golden tests (including the 32-seed sweep), and 7 prepared-asset checks. Gameplay hashes were not repinned. Canonical Windows build succeeded: GUID 838808b56fd24c00b608094affb77dd8, 877,041,460 bytes, 23:48:01 +01:00. Native capture confirms ChakraPetch-Bold on every pooled floater, including readable normal and critical values at 1920×1080. Monitor check passed on secondary DISPLAY1. The capture probe completed. Concurrent owner map-sizing edits appeared during this task and are preserved; build provenance is the shared working tree, not a pristine isolated A/B patch. Diagnostics live outside the project in the task visualization folder; canonical output remains ../Builds/VoidFall.exe. Real profiles and run exports are preserved.


## Native performance follow-up

Identical stress arguments: productionMax, seed 1595785438, hold750, 8 s warmup, 20 s measurement, 1920×1080 DX11/high. Both captures advanced simulation and maintained 750 actors; no screenshots were requested in the timed runs.

| Measurement | Prior build | Updated build |
|---|---:|---:|
| Median frame | 16.60 ms | 16.52 ms |
| p95 | 24.46 ms | 25.48 ms |
| p99 | 31.08 ms | 35.01 ms |
| Maximum | 64.36 ms | 323.59 ms |
| Mean measured simulation CPU | 5.68 ms | 6.89 ms |
| Simulation ticks advanced | 873 | 857 |

These results do NOT demonstrate an overall FPS improvement. Timed workloads diverge as auto-picked upgrades, kills and hit-stop advance, and the shared build includes concurrent map work. GPU/render-thread counters were unavailable; a zero counter is not zero cost. The first baseline with a screenshot at measurement start is excluded: capture itself contaminated its maximum frame.

The new timings isolate an additional stress hitch to CPU-side update work: at combat time 1510.68 s the sample reports a 316.6 ms frame and its preceding window peaks at 318.05 ms in Update, with 22 generation-zero GC collections. The 1510.4–1510.85 s journal contains a boss defeat, 819 enemy deaths, 819 spawns, 833 drop merges and 583 loot relocations (the artificial hold750 fixture immediately replenishes deaths). This is a mass-clear/refill allocation/CPU hotspot to investigate next; event counts alone do not prove logging, loot, or GC is the dominant cause. It also does not reproduce or explain conclusively the owner's sustained four-minute slowdown. Startup/warmup spikes remain visible in the full export and are excluded from the timed benchmark table.

Follow-up should profile a controlled mass-clear separately from steady dense combat, splitting death/drop processing, exporter serialization and allocations, while preserving earned loot and exporter completeness. Do not hide the problem by lowering resolution or deleting enemies. Fresh real-run CPU windows now provide the same bounded evidence automatically.

Native diagnostic profile paths were isolated; the real profile SHA-256 stayed 06514F7E7CF3A863A866AD6B62E5A4D93CFAFD3C2C91B93BF733F1DCBF06F1F1 across final checks. Screenmanager preferences were restored after diagnostics.
