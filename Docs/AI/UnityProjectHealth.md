# VoidFall project health

Last audited: 2026-09-04, source `049500d` plus the current working tree.

The owner now targets a **full-game release on October 15**. The detailed
current findings, repaired defects, evidence and release gates are in
[ReleaseReadiness-2026-09-04.md](ReleaseReadiness-2026-09-04.md).

## Overall status

The current Windows prototype passes automated tests, but is not ready for
full release. Five reachable route nodes lack objectives and the runtime has
no victory/escape resolution. Save recovery and automatic-rift defects were
reproduced and repaired during this audit.

## Verified baseline

- Unity batch EditMode: 259/259 tests passed, including nine save regression tests
- Unity batch PlayMode: 7/7 tests passed, including automatic travel
- 32 deterministic seeds reproduce exactly across consecutive runs
- Pinned hash remains `14713629958221367877`
- Current Windows build/smoke evidence is recorded in the detailed release report
- No duplicate GUIDs or missing file metadata in the 658-file metadata scan

## Priority work

### HF-001 — complete the Void route

Severity: High

Confidence: Confirmed

Only Abyss, Red Nebula, White Sakura, Hydra and Monochrome Court have objectives.
Dead Orbit, Graveyard, Null City, Last Gate and Final Void do not. Final Void
has no runtime escape resolution and route threat multipliers are not consumed.
Transition reset now clears enemies, projectiles and meteors; automatic travel
also advances the route correctly. Full branch completion remains unverified.

### HF-002 — finish visual arena catalogue

Severity: High

Confidence: Confirmed

Five route identities have prepared packages: Abyss, Red Nebula, White Sakura,
Hydra and Monochrome Court. Lost City is the next requested package; other
graph nodes still reuse Abyss.

### HF-003 — runtime ownership remains concentrated

Severity: Medium

Confidence: Confirmed

`VoidFallGameRuntime` remains about 28k lines across partials. GameSim and FxSim
are meaningful improvements, but arena renderer, HUD synchronization and flow
coordination still share one class.

### HF-004 — release configuration is unfinished

Severity: Medium

Confidence: Confirmed

Company/application identifiers are now set. CI Unity execution is conditional;
its PlayMode filter now includes all tests, but there is no hosted player-build
job. Windows is the current target. Controller pause/navigation support is
incomplete, and actual device and minimum-spec validation remain outstanding.

### HF-005 — local workspace cache dominates disk usage

Severity: Low

Confidence: Confirmed

The tracked source remains small relative to Unity's local cache. `Library` and
old profiler/test logs account for most workspace disk usage and are
ignored/regenerable.

### HF-006 — minor compile warnings

Severity: Low

Confidence: Confirmed

`VideoSettingsRules` and its tests still use obsolete `Resolution.refreshRate`.
Migrate to `refreshRateRatio` during a focused settings pass. Old fixture-field
warnings should be distinguished from gameplay errors when reading historical logs.

### Repaired and newly tracked findings

- HF-007: backup recovery, legacy priority and failed-recovery preservation — fixed.
- HF-008: Unity-authored bestiary discoveries missing from saves — fixed.
- HF-009: automatic rift route-state mismatch — fixed.
- HF-010: CI omitted flow tests and seed sweep — filter fixed; hosted execution unverified.
- HF-011: controller menu/pause flow incomplete — open.
- HF-012: no live-run resume; restart can abandon failed terminal saves — open.
- HF-013: route objective/reward copy and threat display exceed implemented behavior — open.
- HF-014: remaining HUD formatting and post-effect performance risks — needs measurement.
- HF-015: future save versions are silently downgraded — open before public rollback testing.
- HF-016: stress probe can report completion without proving simulation advancement — open.
- HF-017: opaque square effects in the stress capture — visual investigation needed.

## Healthy areas

- Core and Content remain engine-free.
- Pools avoid per-entity Unity component overhead.
- Save writes are atomic; missing/corrupt primaries now recover backups under regression coverage.
- Addressables owns prepared arena lifetime.
- Arena textures are non-readable and mip-streamed.
- Music is streamed and reactive processing uses preallocated buffers.
- Regression tests cover route state alignment and uGUI CanvasGroup setup.

## Historical deferrals and current release scope

Earlier notes parked startup profiling and deferred unreleased Void mechanics.
The current full-release target makes startup behavior and every shipping
Void's completion explicit gates. Mobile is still outside the inspected Windows
target. No decision to remove unfinished routes from the full game was made.
