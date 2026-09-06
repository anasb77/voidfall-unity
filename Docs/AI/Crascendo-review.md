# Crascendo focused review

Verdict: changes requested. Two P2 correctness/spec findings in the runtime-stable review snapshot. Review was limited to Crascendo code, integration hooks and immediate callers; earlier Eon/Null/roster changes were excluded. No source edits or test reruns were performed.

1. **[P2] A damaging hit can shrink a naturally grown harvester.** `Assets/VoidFall/Runtime/Gameplay/VoidFallGameRuntime.Crascendo.cs:41` assigns `spawnBase * Growth(hits)` unconditionally, while `GameSim.cs:1798` independently enlarges harvesters after absorbing XP. A tier-I harvester starts at radius18; after absorbing16 XP its radius is22, but its first positive hit changes it to21.6. More absorbed XP produces a larger visible shrink. This violates the per-hit enlargement behavior and leaves two competing radius owners. Reconcile natural harvester growth with the fixed spawn-radius increment and cap explicitly, so a hit never reduces its size. Add a regression using native harvesting followed by damage; the current generic chaser fixture cannot expose this.

2. **[P2] Animated tears freeze throughout the reward/escape window.** `Assets/VoidFall/Runtime/Gameplay/VoidFallGameRuntime.Crascendo.Render.cs:42` selects `_crascendoElapsed` as the tear animation clock outside the menu. That clock advances only in `StepCrascendo`, called by combat `Simulate`; `VoidFallGameRuntime.cs:1448` stops simulation when `JourneyStopsCombat`, and `VoidFallGameRuntime.Journey.cs:35` includes Rewards. After defeating the boss, the violet palette stays correct but every tear remains at its last position/alpha through loot collection and departure countdown despite reduced motion being disabled. Use a presentation clock that continues during Rewards while keeping progression intensity tied to combat elapsed time.

Static checks found the expected stable arena ID7, prepared catalogue/package integration, shared objective and ambient spawn paths, identity-keyed enemy growth/reset, native damage/death hooks, collision-query wrappers, visual scaling from radius, and presentation detachment before residency release. The pulse itself changes only knockback and cosmetic rings, not health or growth.

Validation limits: tests and player captures are owned by the implementation worker. Existing `Giant_death_pushes_survivors_once_without_damage_or_growth` directly invokes the death helper on a live actor, so it does not prove the native lethal-hit/death boundary, nested overload/exploder deaths, or the boss death pulse. Focused native lethal-flow coverage is advisable, including the twentieth hit killing its source. This is a coverage limitation, not an additional confirmed runtime defect.

## Fix re-review

Reviewed `Crascendo-fix-review-package.txt`, updated implementation notes, and the immediate spawn, radius-hook, reward-flow and wash-asset callers. Read the final PlayMode XML:70 total,70 passed,0 failed, completed2026-09-06 11:51:30Z. No tests rerun or production edits.

- **Finding1 — ADDRESSED.** `VoidFallGameRuntime.Crascendo.cs:40` now adds20% of spawn radius to current physical size, capped at5x. `CrascendoNaturalRadius` tracks natural increments separately and never subtracts size. Native spawn initializes both radius baselines (`VoidFallGameRuntime.Sim.cs:3199`), and arena reset removes the optional hook outside Crascendo. Native harvesting followed by damage now has passing regression coverage. Preserving the harvester's independent natural growth means that particular enemy can reach the cap before its twentieth hit; the fixed hit increment and maximum remain intact. The actual-size death predicate correctly handles this case.
- **Finding2 — ADDRESSED.** `VoidFallGameRuntime.Journey.cs:171` advances Crascendo during Rewards after the existing modal/pause gates. Tears therefore continue through ordinary reward/escape time, palette intensity remains clamped at maximum, and modal ownership still pauses this clock. The new reward-clock test passes.

The death-pulse regression now reaches the native lethal-hit path and verifies surviving enemy and boss health/radius remain unchanged while receiving push; its repeat-helper assertion still verifies once-only delivery. This closes the earlier direct-helper-only limitation for enemy deaths, though it is not exhaustive nested-death or boss-source coverage.

No additional correctness finding in the scoped fixes or wash integration. The two camera-local washes occupy dedicated slots50/51, interpolate the same three palette stages, render below actors, validate as plate-owned sprite dependencies, and detach through the existing52-slot presentation cleanup. Diagnostic capture mutations remain behind the explicit Crascendo capture flag.

**Final scoped verdict: both reported defects addressed; no remaining blocking code/spec finding.** Runtime spec review passes within the examined scope. Final rendered visual quality and Windows player/package acceptance remain pending the parent's build/capture inspection; this review does not claim those results.

## Final boss identity fix

**Pre-first-hit boss pulse loss — ADDRESSED.** Reviewed `Crascendo-boss-fix-review.txt` and native `SpawnBoss` at `VoidFallGameRuntime.Sim.cs:3274`. The new initialization records the just-created telemetry identity and original radius, and clears momentum at birth, before the boss is added to its order table. Consequently a later death pulse survives the first damaging hit: growth sees the already-matching identity and does not take the lazy reset branch. Recycled slots start with fresh hit/pulse state and zero old momentum; the initializer returns immediately outside Crascendo. No introduced regression found in this narrow change.

Read the refreshed `Logs/crascendo-playmode-final.xml`: **71/71 passed**,0 failed, completed2026-09-06 11:55:48Z. The new `Newly_spawned_boss_keeps_pulse_momentum_when_its_first_hit_arrives` regression passes and exercises native boss spawning, enemy lethal damage, then the boss's first damage. No tests rerun.

**Final code/spec verdict: pass within the reviewed scope; all identified defects addressed.** Final Windows build and rendered capture quality remain the parent's acceptance checks.
