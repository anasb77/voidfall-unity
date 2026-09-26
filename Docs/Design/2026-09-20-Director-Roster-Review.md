# Director roster and Null City review

Evidence and approved tuning record. The shared-director roster and tier
package below is implemented in Director I version 8; native-arena pacing and
reward adjustments are recorded separately in their owning runtime paths.
Ordinary runs f1779bf105f8472ebed98accc0852cff (1628.54s)
and cda430e0e2a3454ba6b63dd835874a9d (302.06s), build
12014dd1e7874cb6b3648c3afe7fa1ca, Director I version7.

## Observations

Both runs introduced Gunner around90–93s and no new family until Brute at
270–273s. This is an authored gap: LegacyRestorationRules.RevealSeconds puts
Brute at270, Guard480, Technician540, Twin Gunner600, Splitter660, Mortar720,
Bulwark780, Harvester840 and Carrier900. Safety/beat deferrals add further delay.

The longer run introduced Guard496s, Technician550s, Twin Gunner601s,
Splitter664s and Mortar723s in Eon Sea. Three remaining common families
appeared only after Null City, at1224/1236/1248s in Red Nebula. Thus novelty
exists in the second void but remains diluted; native arena scheduling also
interrupts common-family introductions.

Across two-minute spawn bins, chaser/runner/swarmer comprised86–87% of Abyss
admissions, and78–85% in Eon Sea. ChooseRestorationAmbient reserves70% for
those families. Per-family active limits fall back to chaser; repeated beat
compositions add mostly those same bodies. Spawn counts are not visible-body
shares or proof of how often a specialist successfully attacked.

EnemyRosterRules.TierAt begins upgrading eligible families after540s (9min)
and reaches full tierII at900s. SpawnEnemy also holds recently introduced
families at tierI for60s. The longer run left Abyss at405.23s/level28, so its
first portion of void2 still had no normal tierII. Later Eon Sea accumulated
1784 tierII admissions among11183 total admissions.

Null City was the third void: entered777.27s atlevel44, exited1198.06s
atlevel45. Its sample mean was9 active enemies (6.13 on screen), peak20;
Abyss mean306.78/peak700, Eon Sea mean158.10/peak391. Means include sampled
boss/flow windows, not exclusively survival. City recorded457 admissions over
420.19s, all tierI and no elite admissions.

UpdateSpawns routes City directly to UpdateNullCitySpawns and returns before
the shared director. Ambient City arrivals are one body per1.2s, shortening
toward0.65s, with a30-active admission ceiling. Heavy arrivals require no live
heavy and use a19s clock/40-active limit; police waves have separate bounds.
The measured peak20 never reached the ordinary ceiling, so raising only the
ceiling cannot solve the low arrival rate. All City units are forced to tierI.
City HP already scales through the general health formula: recorded Crawlers
averaged about492HP, Siege Mechs about10059HP. No evidence supports claiming
City health is unscaled. Review spawning/combined threats before another
blanket HP increase.

## Approved first tuning package (implemented targets)

Keep the approved early0–90s introductions and overall horde pressure.
Guard enters at150s, Brute at210s and Technician at270s, each with a small
safe introduction and a subsequent recognizable formation. On the second
shared visit, Twin Gunner enters at local30s, Splitter at90s and Mortar at150s;
Bulwark, Harvester and Carrier remain reserved for later shared visits. The
runtime uses shared-visit/local progress, preserves a 60-second tier-I learning
grace for each newly introduced family, and advances one overdue family at a
time after safety deferrals. Native arenas keep their own families and
behaviors rather than importing the generic roster.

Begin a5–10% tierII preview late in Abyss, and enter the second shared visit
with15% of eligible familiar enemies at tierII, ramping to50% by local360s.
Preserve the learning period for newly introduced families and retain the
existing later tiers. This is authored route progression, not hidden player-DPS
matching.

Give formations a changing composition: Guards escort shooters, Technician
supports a push, ranged pressure creates a dodge while runners approach from
another side. Reduce basic-family dominance gradually toward60–70% late in
Abyss and50–60% in later shared arenas. When one family is capped, choose
another eligible role; preserve bounded simultaneous attack commitments. More
visible variety must not imply every specialist fires simultaneously.

Integrate City with shared pacing/arrival/recovery ownership while retaining
its native roster, surveillance, lockdown and purge lanes. The implemented
playtest targets are4–6 ordinary units/sec, rising toward8–10 during lockdown;
roughly35–50 active bodies in quieter phases and60–90 in busy phases remain
evaluation targets, not mandatory player-DPS-dependent refill orders. Permit
two heavies later, pair them with escorts, and use warned police reinforcements.
Ordinary native City bodies award0.75× XP, use a2% Scraps chance and half the
existing ordinary rare-drop chance; elites, bosses and summoned child bodies
retain their guarantees and authored rewards. Respect fixed-arena exits, body
size, attack reservations and boss safety.

Measure actual type shares, introduced-to-first-attack time, specialist attack
success, quiet intervals, stage-relative density, received damage, level/XP
pace, reward income after the City modifiers and CPU/frame costs. Keep rare-drop
limits tied to the documented policy while reviewing the combined pacing result.
