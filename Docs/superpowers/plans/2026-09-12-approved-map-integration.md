# Approved Map Integration Implementation Plan

Owner approved browser study integration, reaffirmed September12. Hydra II must
retain the OLD Unity map/boss encounter; browser lair changes are superseded.

## Ownership and constraints

Existing working tree contains Director/exporter/weapon/audio/freeze work.
Preserve it. No broad enemy-health rebalance, mines/events or freeze redesign.
Parent owns shared runtime/renderer/simulation and Hydra population. Independent
agents own NullCity partials, Court partials/rules, Hydra phase transition.
Only parent launches Unity tests/build. Keep existing IDs/save/pooled architecture.

## Tasks

1. NullCity: preserve authored map; fourfold world coordinate mapping with
   consistent authored/world conversions, follow POV, visual-only player size,
   existing purge timings/enemy damage, active-road shake, surveillance/lockdown
   sign. Tests cover geometry, native mechanics, reset and exported events.
2. Court: single stable soft monochrome board and readable old chess sprites;
   spaced seeded living/fallen rook sentinels, targetable100k+HP, contact-only,
   bars without numeric labels, exact death notice and5–6 roster-one children.
   Retain Grandmaster sprites/native attacks. Local snapshotted alternating
   floor cells arm sequentially over2s, detonate after warning, damage all
   appropriate actors. Six-segment mortar marker. Tests cover timing, scope,
   targeting, death idempotency, spacing and exporter.
3. Hydra:360s survival without nodes/bone detail, fast downward original glyph
   surface; existing collapse/swap/settle into old solo Hydra boss, same route
   visit and scoring lifecycle. Tests protect boss behavior and transition.
4. Hydra population: five parent-derived swamp-colored hybrids and five early
   Viruses, bounded pooled behavior and authored geometric silhouettes. Keep
   identity sidecars keyed to spawn IDs; integrate damage/children/XP behavior
   with existing gameplay and telemetry. No per-enemy MonoBehaviours.
5. Integration gate: baseline compile and relevant EditMode/PlayMode,32-seed
   determinism as needed, independent review, rendered direct-arena captures,
   canonical Windows build with updated timestamp/build information. Keep
   user saves/exports isolated from diagnostics. Update AGENTS/REPO_MAP.

## Stop condition

All approved mechanics are integrated and verified, or any real external
blocker is documented with exact unfinished scope. Never call browser tests
proof of Unity behavior. Never repin golden hashes without explained behavior
change and passing repeatability sweep.
