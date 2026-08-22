# Menu Controllers Migration — Agreed Design

Status: agreed 2026-08-22, ready for wave-by-wave implementation
Scope: moving menu controller logic from the runtime partial (`VoidFallGameRuntime.UI.cs`, ~4.2k lines) into `VoidFall.UI`

## The decision

Menu controller logic moves into `VoidFall.UI` behind a dependency inversion.
`VoidFall.UI` owns a services interface; the runtime implements it and injects
it once. No circular assembly reference is created, because UI already
references Core, Content, and Persistence - which covers nearly all menu data.

The seam already exists in practice: the runtime constructs its UI through
`UIManager.Create(new UICallbacks { ... })`. This migration formalizes that
delegation into an interface both sides can rely on.

## The bridge: two directions

```text
VoidFall.UI declares:   IGameBridge
Runtime implements it;  injected once where UIManager is created
```

**Commands** (game-flow actions the menus trigger): start/restart run,
resume/pause, abort to menu, open/close pages, reroll/select level options,
accept/decline revive, toggle mute, export/import save.

**Queries** return small immutable structs, never live state:

| Query | Source |
| --- | --- |
| Workshop rows (cost, rank, affordability, description) | Persistence + Content |
| Lifetime stats + high-score rows | Persistence |
| Current settings values | Persistence |
| Best score / parts / runs strip | Persistence |
| In-run score (result card) | Runtime (narrow getter) |

Most pulls route through Persistence, which UI can already see; the genuinely
runtime-side queries are few and must stay narrow. If a query wants broad sim
state, that is a signal to extend GameSim's public counters instead of the
bridge.

## Migration waves

| Wave | Screen(s) | Why this order |
| --- | --- | --- |
| Pilot | **Settings** | Simplest commit path, self-contained, known-good layout after VF-002 fix |
| 2 | **Records** | Read-only over save data - zero progression risk |
| 3 | **Workshop** | Purchases/refunds touch player progression; highest stakes, runs only after the pattern is proven twice |
| Last | Home / Pause / Revive / GameOver | Interleave run lifecycle; move together with the composition-root shrink |

Per-wave rule: one screen fully moved, verified, and committed before the next
starts. Existing carefully-built contracts must be preserved verbatim -
settings save-first transaction with rollback on failure, workshop purchase
guard and part/rank rollback, browser-save import/export losslessness.

## Verification protocol (per wave)

1. Compile gate: `dotnet build VoidFall.Runtime.csproj -t:Rebuild` - 0 errors.
2. EditMode suite green **plus new controller tests**: settings commit
   transaction, records formatting, workshop affordability math. Controllers
   become plain classes - test them as such.
3. PlayMode golden master: pinned hash unchanged (menus are outside the sim;
   any drift means something touched gameplay accidentally).
4. Screenshots via `-vfcapture*`: each migrated screen at 1280x720 and
   1920x1080, before/after compared. The golden master is blind to view-side
   regressions - the shake-hook incident proved compile+hash is not enough.

## Explicit non-goals

- Views are NOT rebuilt - uGUI construction stays as-is; only controller
  logic moves.
- HudPresenter/ArenaRenderer promotion is a separate track (needs the
  snapshot design); do not couple it to these waves.
- Composition-root shrink waits until this AND the presenter/renderer tracks
  finish - a bootstrap should not wire half-migrated pieces.
- No new features inside migrated controllers; behavior parity only.

## Rollback story

Each wave is an independent commit set. Wrappers keep the old runtime-side
entry points during transition, so reverting one wave never strands another.
