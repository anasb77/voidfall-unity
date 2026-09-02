# Monochrome Court Floor Hazard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the Twin Grandmasters' old turn/checkmate system with alternating white/black floor hazards while keeping both bosses active, vulnerable, and linked to one health pool.

**Architecture:** `MonochromeEncounterRules` owns deterministic hazard timing without Unity dependencies. `MonochromeRuntimeRules` maps stable world-space board coordinates to tile colors and owns Court-only spawn selection. `VoidFallGameRuntime.Monochrome` applies damage through the existing player damage pipeline and renders warning/burning states using the existing pooled board renderers.

**Tech Stack:** Unity 6000.5.7f1, C#, URP 17.5, NUnit EditMode tests, existing pooled procedural runtime.

## Global Constraints

- Both Grandmasters fight and remain damageable simultaneously.
- White tiles warn for 0.9 seconds, burn for 2.2 seconds, then the board recovers for 0.5 seconds; Black follows and repeats.
- Below 50% shared boss health, warning is 0.7 seconds and burning is 2.4 seconds.
- Monochrome Court spawns only Pawn, Rook, Bishop, Knight, and Queen before the boss fight, and no ambient enemies during it.
- Preserve the approved arena, enemy silhouettes, enemy behaviors, shared boss health, and Addressables content.

---

### Task 1: Deterministic hazard and spawn rules

**Files:**
- Modify: `Assets/VoidFall/Core/MonochromeEncounterRules.cs`
- Modify: `Assets/VoidFall/Runtime/Gameplay/MonochromeRuntimeRules.cs`
- Modify: `Assets/VoidFall/Tests/Editor/MonochromeEncounterRulesTests.cs`
- Modify: `Assets/VoidFall/Tests/Editor/MonochromeRuntimeRulesTests.cs`

**Interfaces:**
- Produces: `CourtHazardStage`, `CourtHazardState`, `MonochromeEncounterRules.HazardAt(double, bool)`, `MonochromeEncounterRules.IsTileDangerous(CourtHazardState, CourtFaction)`, `MonochromeRuntimeRules.FactionAtWorldPosition(Vector2, Vector2, Vector2)`, and `MonochromeRuntimeRules.NextSpawnId(double)`.

- [x] **Step 1: Write failing tests**

```csharp
Assert.That(MonochromeEncounterRules.HazardAt(0, false),
    Is.EqualTo(new CourtHazardState(CourtFaction.White, CourtHazardStage.Warning)));
Assert.That(MonochromeEncounterRules.HazardAt(0.9, false).Stage,
    Is.EqualTo(CourtHazardStage.Burning));
Assert.That(MonochromeEncounterRules.HazardAt(3.6, false).Faction,
    Is.EqualTo(CourtFaction.Black));
Assert.That(MonochromeRuntimeRules.NextSpawnId(0.99), Is.EqualTo("court-queen"));
```

- [x] **Step 2: Run the focused EditMode tests and verify RED**

Run the Unity EditMode test batch filtered to `MonochromeEncounterRulesTests` and `MonochromeRuntimeRulesTests`.
Expected: compile failures for missing hazard types and methods.

- [x] **Step 3: Implement minimal pure rules**

```csharp
public static CourtHazardState HazardAt(double elapsedSeconds, bool phaseTwo)
{
    var warning = phaseTwo ? 0.7 : 0.9;
    var burning = phaseTwo ? 2.4 : 2.2;
    const double recovery = 0.5;
    var pulse = warning + burning + recovery;
    var index = (int)Math.Floor(Math.Max(0, elapsedSeconds) / pulse);
    var cursor = Math.Max(0, elapsedSeconds) % pulse;
    var stage = cursor < warning ? CourtHazardStage.Warning :
        cursor < warning + burning ? CourtHazardStage.Burning : CourtHazardStage.Recovery;
    return new CourtHazardState((index & 1) == 0 ? CourtFaction.White : CourtFaction.Black, stage);
}
```

Use `Mathf.FloorToInt` on stable world coordinates divided by tile size so checker parity does not move with the camera. Move the existing inline spawn thresholds into `NextSpawnId` and return only the five `court-*` IDs.

- [x] **Step 4: Run the focused tests and verify GREEN**

Expected: all focused Monochrome rule tests pass.

### Task 2: Simultaneous bosses and world-anchored lava board

**Files:**
- Modify: `Assets/VoidFall/Runtime/Gameplay/VoidFallGameRuntime.Monochrome.cs`
- Modify: `Assets/VoidFall/Runtime/Gameplay/VoidFallGameRuntime.Sim.cs`

**Interfaces:**
- Consumes: hazard and tile-color rules from Task 1.
- Produces: simultaneous Grandmaster damageability, warning/burning/recovery presentation, and floor damage gated by the existing player invulnerability system.

- [x] **Step 1: Replace state fields and old pattern dispatch**

Store the current `CourtHazardState`, a previous-state sentinel, and a floor-damage cooldown. Remove `CourtTurn`, Checkmate timing, `FireMonochromeBossPattern`, and alternating vulnerability checks.

- [x] **Step 2: Apply hazard damage through `DamagePlayer`**

```csharp
var tile = MonochromeRuntimeRules.FactionAtWorldPosition(
    _gameSim.Player.Position, _monochromeArenaCentre, tileSize);
if (MonochromeEncounterRules.IsTileDangerous(_monochromeHazard, tile) &&
    _monochromeFloorDamageCooldown <= 0f)
{
    DamagePlayer(phaseTwo ? 20f : 16f, Vector2.zero);
    _monochromeFloorDamageCooldown = 0.65f;
}
```

- [x] **Step 3: Anchor and render the repeating board**

Render the existing 14x9 pool around the camera, but calculate global row/column indices from `_monochromeArenaCentre`. Warning tiles pulse amber; burning tiles flicker orange/red; safe tiles retain readable black/ivory contrast.

- [x] **Step 4: Preserve encounter isolation**

Keep `UpdateSpawns` returning immediately for Monochrome Court, use `NextSpawnId` in `UpdateMonochromeSpawns`, and keep the boss phase clear of ambient enemies.

- [x] **Step 5: Compile and rerun focused tests**

Expected: zero compile errors and all focused Monochrome tests pass.

### Task 3: Regression and player validation

**Files:**
- Modify: `Docs/AI/UnityProjectContext.md`
- Modify: `README.md`

**Interfaces:**
- Consumes: completed runtime mechanic.
- Produces: validated Windows build and current project documentation.

- [x] **Step 1: Run all EditMode and PlayMode tests**

Expected: no regression from the current 233 EditMode and 5 PlayMode baseline, plus the new hazard tests.

- [x] **Step 2: Build and inspect the Windows player**

Capture White warning, White burning, Black warning, and Black burning states. Confirm projectiles and the player remain readable, the board does not follow the player's local center, and only the matching tile color causes damage.

- [x] **Step 3: Review the final diff**

Confirm no generated arena art, sprite silhouettes, Hydra files, packages, or unrelated user changes were altered.

- [x] **Step 4: Update documentation and report exact evidence**

Replace old Checkmate/turn documentation with the alternating floor-hazard rules and report exact test/build results plus remaining manual-playtest limits.

## Completion Evidence

- Focused post-review EditMode tests: 42/42 passed.
- Full EditMode tests: 242/242 passed.
- Full PlayMode tests: 5/5 passed.
- Windows player: 159,256,272-byte build succeeded.
- Real-player captures inspected at 1280x720 for White warning, White burning, Black warning, and Black burning.
- Independent follow-up review: no remaining Critical or Important issues.
