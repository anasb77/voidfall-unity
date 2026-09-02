# Monochrome Court Floor Hazard Design

## Goal

Replace the Twin Grandmasters' alternating vulnerability, line attacks, and Checkmate window with a readable alternating chess-tile hazard while both bosses fight simultaneously.

## Encounter Rules

- Both Grandmasters are active and damageable throughout the fight.
- They retain one shared health pool.
- The White Grandmaster controls white tiles; the Black Grandmaster controls black tiles.
- Each color pulse has three stages: 0.9 seconds of warning, 2.2 seconds of lethal floor, then 0.5 seconds with the full board safe.
- White pulses first, followed by Black, and the cycle repeats.
- Below 50% shared health, warning time becomes 0.7 seconds and lethal time becomes 2.4 seconds. The safe recovery remains 0.5 seconds.
- Warning tiles visibly pulse before ignition. Burning tiles use a high-contrast emissive treatment without hiding projectiles, bosses, or the player.
- Standing on an active tile damages the player through the existing damage and invulnerability pipeline. Damage is not applied once per rendered frame.
- The old Checkmate state, rank/file and diagonal boss patterns, alternating vulnerability, and active/inactive boss contrast are removed.

## Spawn Isolation

- Before the boss phase, Monochrome Court spawns only `court-pawn`, `court-rook`, `court-bishop`, `court-knight`, and `court-queen`.
- The normal director, generic enemies, and non-Court boss schedule do not run in Monochrome Court.
- No ambient enemies spawn during the Twin Grandmaster fight.

## Validation

- Engine-free tests cover the hazard phase sequence, active tile color, phase-two timing, and boundary behavior.
- Runtime-rule tests cover board-color lookup and damage cadence.
- Existing Court spawn-isolation tests are strengthened to prove only the five chess enemies can be selected.
- Run the complete EditMode and PlayMode suites, rebuild Addressables if required, build the Windows player, and visually inspect both warning and lethal phases.

