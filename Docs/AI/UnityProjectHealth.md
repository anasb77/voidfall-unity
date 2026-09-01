# VoidFall project health

Last verified: 2026-09-01.

## Overall status

The current Windows prototype compiles, tests and builds. Core simulation,
persistence and arena asset loading have useful safety boundaries. The main
release risk is incomplete content/lifecycle beyond the first rift, not a
broken build pipeline.

## Verified baseline

- `dotnet build VoidFall.Runtime.csproj -t:Rebuild`: zero errors
- EditMode: 168/168 first-party tests passed
- PlayMode: 5/5 passed
- 32 deterministic seeds reproduce exactly across consecutive runs
- Windows release build completed
- `productionMax` standalone smoke completed with 192 enemies and two bosses
- No duplicate asset GUIDs or missing metadata

## Priority work

### HF-001 — complete the Void route

Severity: High

Confidence: Confirmed

The first Abyss completion and route choice work, but Layer-II nodes have no
objectives, Final Void has no escape resolution, route threat multipliers are
not consumed, and the transition does not yet implement the full fresh-Void
entity reset described by the product design.

### HF-002 — finish visual arena catalogue

Severity: High

Confidence: Confirmed

Only three route identities have prepared packages. Hydra, Monochrome Court
and Lost City are the next requested packages; other graph nodes still reuse
Abyss.

### HF-003 — runtime ownership remains concentrated

Severity: Medium

Confidence: Confirmed

`VoidFallGameRuntime` remains about 25k lines across partials. GameSim and FxSim
are meaningful improvements, but arena renderer, HUD synchronization and flow
coordination still share one class.

### HF-004 — release configuration is unfinished

Severity: Medium

Confidence: Confirmed

Company/application identifiers are still Unity defaults, CI Unity execution
is conditional, Windows is the only tested platform, and physical controller/
mobile safe-area validation is outstanding.

### HF-005 — local workspace cache dominates disk usage

Severity: Low

Confidence: Confirmed

The tracked source is about 54 MiB. Unity `Library` and old profiler/test logs
account for more than 4 GiB locally and are ignored/regenerable.

### HF-006 — minor compile warnings

Severity: Low

Confidence: Confirmed

Fourteen JsonUtility fixture fields intentionally appear unassigned to the C#
compiler. `VideoSettingsRules` also uses the obsolete
`Resolution.refreshRate`; migrate to `refreshRateRatio` during the next settings
pass.

## Healthy areas

- Core and Content remain engine-free.
- Pools avoid per-entity Unity component overhead.
- Save writes are atomic and corruption-aware.
- Addressables owns prepared arena lifetime.
- Arena textures are non-readable and mip-streamed.
- Music is streamed and reactive processing uses preallocated buffers.
- Regression tests cover route state alignment and uGUI CanvasGroup setup.

## Deferred by product choice

- Startup profiling remains parked.
- Mobile is not a current release target.
- Full enemy/mechanic identity for unreleased Voids follows their visual maps.
