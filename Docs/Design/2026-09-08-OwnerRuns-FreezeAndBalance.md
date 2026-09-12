# Owner runs: surviving export, freeze triage and balance

Read-only investigation of player build `17127f1e74df473daccec32f1c0666fd`.
No gameplay changes or new player launches during investigation. Original
Player.log and Player-prev.log preserved under `Logs/OwnerRuns-2026-09-08-Freeze`.
The streaming analysis script and derived `analysis.json` are in that folder.
Original exports remain untouched in `../Builds/RunExports`.

## Recovery

- `c63bdcc8c31149e991bad408919b70d0`: complete gameover in Monochrome Court at
  520.354 seconds (8:40), 2,848 kills, 29,506 consecutive valid journal records.
- `adf1700df08445efb56587c63dcf0bb5`: interrupted run; checkpoint at 1750.16
  seconds, journal through 1753.103 seconds (29:13). **157,772 consecutive valid
  JSONL records, no malformed lines or sequence gaps**. Checkpoint reports no
  queue drops/errors. No terminal event. Unknown unflushed tail must not be
  reconstructed as facts; preserve status active/unfinished and nonfinal score.
- Both runs confirm all Workshop ranks zero and the new 4/5-slot build.
- The second checkpoint has five rank-VI weapons, all five evolved: Pistol,
  Scattergun, Arc, Mines and Clock. Level 59, 14,149 kills at checkpoint.
- XP accounting gap is zero in both reports.

## Balance evidence

First run visits Abyss then Court. Court pawn median spawned HP is 199,
rook 1,435, queen 1,339; rooks that died took median 24.17 seconds after their
first recorded hit. Only one of twelve queens died. Court inflicted 260 damage
in 151 seconds after entry, compared with 116 across 369 seconds in Abyss.

Second route: Abyss → Eon Sea → Crascendo → White Sakura → Null City (visit 5).
Hydra was not visited in either run, so its reported problems remain owner
evidence requiring their own reproduction.

Median active populations decline from 83.5 in Abyss to 60 in Eon Sea, 39.5 in
Crascendo, 36 in White Sakura and 9 in Null City. Killed shared chasers have
median first-hit-to-death of 0–0.07 seconds. City crawlers have median HP887;
heavy gunships HP15,246 and median first-hit-to-death37.49 seconds (two kills).
These are different roles, not claims that equal health is appropriate. The
problem is incompatible durability/introduction/pacing between arena systems.
First-hit-to-death is elapsed exposure, not continuous focused-fire DPS, and
excludes surviving enemies.

The second run was not uniformly safe: it used a revive in Crascendo; the final
checkpoint sample has49/100HP. This is compatible with weak sustained pressure
and intermittent damage spikes. It does not justify a universal difficulty buff.

Mines account for353k recorded damage (~18% of total); Arc630k (~33%) and
Pistol587k (~30%) exceed them. Attribution is imperfect (~10% unattributed),
and damage share does not measure control strength. Rank-VI Mines have a1s
base cooldown before recovery/Overclock,15s lifetime,26-mine pool and2.4s
ordinary-enemy freeze when evolved. Cadence and sustained immobilization are
better first tuning targets than a blanket damage nerf.

BlackHole selected at1:49 in second run. Source places center330 units away
with radius290; the player begins outside its influence. Pull is at most one
third of base movement speed, and fades to zero at both rim and center. This
explains weak possible attraction, but exports lack continuous pull vectors
to measure exactly what the owner felt. Review placement, falloff and visual
radius together.

Destroyers spawned together at1262.922s in White Sakura and all died by
1266.049s:3.13 seconds for the whole raid. Razor died0.47s after spawn,
Husk1.09s. First-hit-to-death ranges0.27–2.93s. There is little opportunity
for their attack identities to become legible.

## Freeze: unresolved

Last journal event: director_attack_budget, Null City,7 active enemies.
Last checkpoint sample frameMs16.67; Null City observed maximum33.33ms before
the freeze. No matching VoidFall Application error/hang report returned from
the queried Windows event log. Preserved Player.log has no crash stack and
stops at normal City entry logging. No application_focus record survives in
either journal. Absence does not prove a callback was never entered.

Static trace: focus loss pauses gameplay and audio, queues asynchronous
history flush. runInBackground is disabled. Regaining focus leaves gameplay
paused until explicit Resume. This explains an inactive still image, not a
confirmed unresponsive Windows window after returning. Focus flush does not
perform synchronous disk I/O; writer locking is bounded around queue access.
Fixed-step catch-up is capped at three steps, weakening runaway catch-up.
Native render/audio/input stall remains a hypothesis, not a diagnosis.

Best next experiment: preserve these logs, reproduce cursor-only monitor
crossing separately from clicking the other application, return and test
Resume/Escape. Capture a process dump before killing a genuinely hung player;
thread stacks discriminate main/render/audio/writer waits. Do not claim the
freeze fixed by enabling background simulation alone.

## Recommended order

1. Resolve/reproduce the hard freeze before another long validation run.
2. Repair Court/City/Hydra route eligibility, role-based durability and native
   spawning against the same run progression budget; test each map directly.
3. Tune sustained Director I pressure for a five-weapon build while preserving
   the improved build choice. Adjust attack opportunities and time in danger,
   not only enemy count or HP.
4. Reduce mine placement frequency/control uptime, then retest boss matchups.
5. Make BlackHole physically noticeable and tune Destroyer engagements so a
   skilled strong build can defeat them while their first attack is observable.

Existing exports need a compact event encoding follow-up: the surviving
second journal is337MB because empty nested sample/context data is repeated.
This is a storage cost; no evidence currently identifies it as the hang cause.
