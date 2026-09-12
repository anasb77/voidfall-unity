# Loot reachability and six-minute survival

Owner approved: repair the loot/map loop and make survival **six minutes per
void**. Owner clarified that physical arena boundaries are not part of this
pass. Weapon-slot changes, a new opening ramp and further difficulty tuning
remain separate work. Preserve concurrent UI/audio/roulette changes.

## Loot policy v2

The existing281-slot pool remains bounded. XP uses256 ordinary positions plus
the existing final XP-only overflow slot;24 ordinary positions are reserved
for special pickups. Specials use their reserve first and can use free ordinary
space. This separates currency crowding from recovery-drop admission.

When XP cannot allocate a slot, a nearby gem can merge value. If no nearby gem
exists, the selected distant gem is moved to the current drop location before
value is added. It no longer receives new XP while remaining several screens
away. Existing value and identity survive; relocation does not grant XP.

When specials need a slot in a full pool, consolidate two XP piles or two Parts
piles without losing value. If every ordinary slot contains non-currency
power-ups, consolidate duplicate kinds into charges. One charge is consumed per
physical collection; the remainder is restored deterministically as a derived
pickup, without extra combat RNG for scatter. No new health/XP is manufactured.
Roulette's pre-claim capacity check recognizes this reclaimable space without
mutating the pool before the player claims the reward.

At most one unpulled distant pickup is returned into view per .25 simulation
seconds, beyond twice the larger gameplay viewport half-extent. Already-earned
specials take priority over currency. Normal pickup/magnet rules still apply;
Greed cancels existing magnetic pulls and requires physical collection.
Null City uses its existing bounds for fresh and recovered loot. No arena
boundary or camera-size changes were introduced.

Pickup iteration snapshots game-owned slot generations before stepping. Pool
compaction during a Bomb callback cannot move a newborn reward into the current
iteration, apply a stack twice, or overwrite a newly reused slot. Telemetry IDs
are not used to drive gameplay.

## Six-minute timing

All eight arena survival objectives read `VoidProgressionRules.SurvivalSeconds`
(360). Local director time is actual survival time. Beats stop at330s; boss
lead-in/ambient softening and incident cutoff begin at345s. The existing arrival
ramp reaches full strength by300s and continues through the additional minute.
The initial enemy introduction schedule was not changed in this pass.

Pressure keeps its existing80/20 survival/boss weighting and canonical300+60
credit units; six-minute duration does not alter the score multiplier contract.
Shared boss HP/tier evaluation uses `DurationAdjustedDifficultySeconds`, which
subtracts only added survival time using the captured entry stage. Boss-fight
elapsed time remains intact. This avoids silently boosting boss stats solely
because of the extra minute; it is not general enemy-health normalization.
Stress/no-route cases retain their explicit raw clock. Diagnostic progress
seeding now maps360+60 raw stage time into the existing canonical credits.

## Collection and verification

Exports identify survival duration, loot policy, pickup capacity and special
reserve. Samples include XP/special/distant pickup counts and survival/remaining/
boss-difficulty clocks. New `drop_relocated` and `drop_consolidated` events retain
identity/value links; `stack_remainder` births are derived from existing charges.

Tests cover far XP overflow, full XP/Parts/special pools, charge conservation,
Greed, Null City bounds, Bomb callback compaction, roulette claims, real Warden
HP/tier at six minutes, all eight objective boundaries and330/345s scheduling.
The deterministic golden change is intentional for the new loot policy and
six-minute diagnostic progression, and requires the existing32-seed sweep.
