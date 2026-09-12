# Director I — approved sustained combat repair

Owner target: challenging 7/10, frequent readable decisions and satisfying
clears, occasional strain/relief. Player feedback, not a test, establishes this
subjective target. Execute Director I first; map size and health normalization
remain later tasks. Preserve other director behavior and concurrent changes.

1. Reuse CombatEncounterClock with sustained beats (Pursuit, Flank, Hunt,
   Breakthrough). Seeded recent-history exclusion replaces Crossing/Volley
   alternation for I. Baseline arrivals continue through beats and incidents;
   recovery lowers special demands rather than clearing bodies. No ordinary
   48/65-second expiry. No formation preview/toast; individual windups remain.
2. Raise common pool capacity to 750; I admissions use this hard ceiling.
   Authored arrival pacing and soft population targets remain distinct from
   capacity. Spawn outside the actual viewport, concentrating a beat on selected
   sides with space to move. No spawn debt or same-tick replacement of a clear.
3. Admission before special attack commitment, not based on living gunners.
   Existing movement continues on denial. Track actor identity, unfinished
   warning/attack and live projectile provenance; retain reservations through
   freeze, owner death and slot reuse until the threat finishes. Preserve already
   signaled attacks. First integration covers shared I–IV and elite controllers.
4. Extend automatic history with beat choices, arrival/attack budgets and
   admission outcomes. Preserve score, XP/drop formulas and health scaling.
   Compare actual arrivals, near/on-screen enemies, kills/XP and frame times.
5. Run behavioral tests, 750-slot collision/reuse checks, deterministic sweep
   with documented intentional golden update, Windows build, normal I capture
   and a rendered 750-body diagnostic with measured occupancy. No unsupported
   claim of GTX 1060 performance; native arena health cliffs remain deferred.

Initial tuning is a playtest candidate. Stop at a runnable, instrumented Director
I build with validation evidence; do not bundle the subsequent map/health work.

## Initial authored settings

| Control | Version 2 behavior |
|---|---|
| Opening | Existing 1.5-second empty opening, then2 actors per .55s; initial arrivals use nearer vertical viewport edges. |
| Baseline | Batch rises to6 and interval falls to .36s across arena survival. Arrival target is90 + .65×local survival seconds +60×completed entry stages (bounded650), with45 extra during active beats. These are soft arrival targets below the hard750 pool ceiling. |
| Beats | First opportunity24s after entry; .75s approach, up to14s active,5s recovery, then seeded16–28s flow gap. No repetition of the last two eligible situations. Ten natural actors per beat; specialist roles are a small subset. |
| Relief | Temporary2.5/5s existing damage relief,2 fodder per .65s; no permanent low-health mode. Incidents reduce ordinary batch sizes without suspending the fight. |
| Shared bosses / attacks | Existing enemies remain; four-second opening before slow2-per-3.5s fodder support below64 live bodies. Ordinary special-attack limit2/3/4 across run pressure, reduced to1 for boss/recovery/incident/recent damage; already committed threats finish. |

Court/Null City retain their native population/timing rules and all arenas retain
their existing health/stat curves at this stage, while native unit attack starts
participate in the I attack budget. Boss and arena hazards remain native. This
is not the later cross-world health-normalization fix.

Natural beat members use the existing distant-enemy repositioning rule rather
than becoming stranded offscreen. Repositions are exported with the same actor
identity. Actual incoming shots, queued city bursts, frozen/chained windups and
slot reuse are part of reservation retention. Incident selection checks an open
nearby quadrant and incoming shots, not the mere existence of a living gunner.

## Reproduce the two different diagnostics

Use the existing isolated-profile probe in a rendered Windows player:

```powershell
.\VoidFall.exe -vfbench -vfscenario=directorI -vfwarmup=3 -vfmeasure=120 -vfoutput=<absolute-report.json>
.\VoidFall.exe -vfbench -vfscenario=productionMax -vfhold750 -vfwarmup=8 -vfmeasure=20 -vfoutput=<absolute-report.json>
```

Add `-screen-width 1920 -screen-height 1080 -screen-fullscreen 0` and optional
`-vfscreenshot=<absolute.png>`. Do not use `-nographics` for rendered measurements
or screenshots. `-vfrunexports=<absolute-directory>` isolates diagnostic journals.
Without this override, ordinary owner runs still export beside the executable.

The first probe uses actual I scheduling, scripted collection/avoidance movement,
normal health and real first-offered upgrades. It is a mechanical playtest, not
a measurement of human flow/difficulty. It can die; it does not inflate health.
The second is deliberately synthetic: productionMax starts at25 minutes with
its authored test loadout and invulnerability, and the additional hold refills
after each simulation boundary. It proves capacity/workload, not I balance.
Read pre-refill occupancy as well as post-refill min/max/failures and advancing
combat. The stress workload bypasses I scheduling, as it did before this task.
