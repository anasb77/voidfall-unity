# Loot and notice readability follow-up

Owner approved: map shortcut text30% smaller; event notices last3seconds
longer; Black Hole label contains only its name; merchant always above the
crossing platform; XP has2seconds to appear before merging.

The map hint uses0.63 HUD text units instead of0.9. Danger notices gain3seconds
and priority protection from ordinary reward churn. Major incident notices
last8seconds (previously5). Pause/menu reading-time protection remains.

Live crossings always use dealer anchor(0,70) and Zack entry(0,-210). Stock,
face variations, animation, legal interaction distance and purchase rules are
unchanged. This supersedes the older approval of occasional lower placement.
Existing dealer export snapshots record `placement=top`.

## Loot policy3

Merging previously happened directly inside SpawnPickup when the ordinary XP
pool filled: a new kill could produce no new visible gem. Fresh XP now has a
separate2-second MergeDelay, decremented with simulation time. Visual Age
retains its random animation phase. Collection and magnets work during the
delay; newborns are excluded from capacity consolidation.

The ordinary256 XP positions,24 reserved special positions and one original
XP-only overflow slot remain.1024 additional bounded XP-only slots accommodate
newborns during a full horde clear. Under this pressure, the remainder of one
enemy's XP appears as one gem with its full value. Mature gems consolidate
back toward257 piles, at most8 merges per simulation step. No new per-pickup
MonoBehaviours, frame allocations or delayed XP grants are introduced.

If the entire1305-slot pool is exhausted by births before any gem matures,
the existing value-conserving overflow path remains as an emergency fallback;
it explicitly exports reason `fresh_reserve_exhausted`. The750-enemy clear
fixture verifies that normal maximum-population kills do not need that path.
Export metadata records policy3, the2-second delay and1024 reserve slots;
`drop_consolidated` reason `merge_delay_elapsed` links both pickup identities.

Pickup array length, a new reflected MergeDelay field, and changed overflow
birth/collection behavior intentionally affect the simulation golden hash.
Any baseline update requires the32-seed determinism sweep, alongside focused
conservation, early collection, full-capacity and generation-safety tests.

68 distinct focused PlayMode checks passed. The 32-seed replay sweep passed
before the intentional pin update; the updated single-seed test then passed.
Measured legacy-meteor hash8893401275390364667 and full hash4792395221124045609.
The full-clear test drops6000XP from750 enemies without emergency merging;
the timing test retains300 gems at1.9seconds and consolidates only after2seconds.

Canonical Windows build d67d4031bb3c4547957c062527b0febd completed successfully
at 2026-09-20 16:33 +01:00. Native 1920x1080 captures on monitor 2 verified
the reduced hint, BLACK HOLE still visible at 5.5 seconds of controlled
notification time, and the merchant above the crossing. Native pickup counts
were 300 at birth, 300 at 1.9 seconds, and 257 after two seconds, retaining
300 XP. The isolated capture profile produced no logged exceptions/errors.
Screenmanager preferences were restored and verified after capture.

The real save hash changed during Editor validation before the native capture:
comparison with its matching pre-test backup showed only an undiscovered
court-knight-original roster entry added by concurrent Court work. Existing
progress was unchanged; this legitimate roster normalization was preserved.
