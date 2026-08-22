# HudPresenter Promotion — Agreed Design

Status: decided 2026-08-22 (owner chose immutable snapshots); ready to implement
Scope: promoting `.Hud.cs` partial content (~1.2k lines, ~40 view identifiers)
into a testable `HudPresenter` class

## The decision

**Immutable snapshots per frame.** The runtime builds one small readonly
struct (`HudSnapshot`) after each fixed tick, and the presenter is a pure
consumer of snapshots plus its own animation state. The presenter never
references `VoidFallGameRuntime`, `GameSim`, or any live object.

Why this option won here specifically:
- `UpdateHud` already behaves like a snapshot consumer — it re-reads values
  every frame and writes views through change-detection caches
  (`_lastHudSeconds`, `_lastHudHealth`, ...). Snapshots formalize what exists.
- The presenter becomes constructible in EditMode tests without booting the
  game: feed a snapshot sequence, assert on captured view mutations via a
  fake view sink (same `IRecordsSink` trick as wave 2).
- One builder defines the entire HUD data contract; when sim fields migrate,
  there is exactly one place to update.

## The contract

```csharp
// Built by the runtime once per rendered frame (cheap: ~25 assignments).
public readonly struct HudSnapshot
{
    public float Health, MaxHealth;          // from GameSim.Player
    public float TimeSeconds;                // run clock
    public int Level, Kills, PartsEarned;
    public int Xp, XpNeed;
    public int Score;
    public bool OverclockActive; public int OverclockPowerTier, OverclockStreak;
    public float OverclockRemainingSeconds;
    public int ActiveBossCount; public float BossHealth, BossMaxHealth;
    public string FirstBossName;             // resolved builder-side
    public float ArenaBannerRemaining; public string ArenaBannerIncomingName;
    public bool HudVisible;                  // ShouldShowHud() computed builder-side
    // Phase 2 additions (loadout/chips) may append fields - additive only.
}

public sealed class HudPresenter
{
    public HudPresenter(IHudViewSink views);     // uGUI refs injected
    public void Tick(in HudSnapshot s, float unscaledDeltaTime);
}
```

## Ownership moves

| Item | From | To |
| --- | --- | --- |
| ~40 uGUI field references | runtime partials | HudPresenter |
| Ghost-bar fractions, overclock punch, flash overlays, loadout refresh timestamps, `_hudLayout*`, toast timers/views | runtime | HudPresenter |
| `_redFlash/_cyanFlash/_amberFlash` accumulators | runtime | HudPresenter |
| Sim values feeding the above | runtime/GameSim fields | HudSnapshot (built per frame) |

Stay on the runtime: `SetupHud`'s construction calls (uGUI building), input
pause-button callbacks (flow commands), touch joystick image positioning
(reads InputReader directly - may join the snapshot later if convenient).

## Implementation steps (one session)

1. Define `HudSnapshot` + builder filling it from current sources.
2. Create `HudPresenter` with an `IHudViewSink` interface over the view
   fields; `RecordsView`-style concrete implementation wraps existing views.
3. Convert `.Hud.cs` methods block-by-block to presenter methods reading
   `in HudSnapshot`; delete change-detection caches in favour of comparing
   against previous snapshot (presenter keeps the last one).
4. Runtime keeps `SetupHud` construction and calls
   `_hudPresenter.Tick(BuildSnapshot(), Time.unscaledDeltaTime)` each frame.
5. Tests: feed synthetic snapshot sequences; assert label rewrite behavior
   (the VF-009 contracts), boss bar visibility, toast ordering.
6. Verify per playbook: rebuild, EditMode incl. new presenter tests, GM hash
   unchanged, settings/home screenshots at both resolutions.

## Non-goals

- No visual redesign; pixel parity with current captures is the bar.
- Loadout/build-chip text stays runtime-built strings inside the snapshot
  (phase 2 may promote them properly).
- ArenaRenderer promotion follows the same pattern separately with an
  `ArenaSnapshot` (arena id, cycle state, parallax parameters).
