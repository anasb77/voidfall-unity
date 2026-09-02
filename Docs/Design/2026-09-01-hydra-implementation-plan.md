# Hydra Arena and Boss Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship Hydra as a prepared, selectable arena and a route-owned stationary boss encounter with mutated survival enemies, four readable attack states, non-colliding central vertebrae, colliding outer ribs, toxic tentacles, and ordered pixel disintegration.

**Architecture:** Extend the existing enum/catalogue, prepared-arena bake, fixed-pool boss state machine, runtime-authored views, and objective tracker. Engine-free `HydraEncounterRules` owns deterministic shuffle-bag and damage-region rules; the existing runtime partial owns Unity state and pooled presentation. Hydra Prime is a route-only extended boss definition and never enters the normal endless boss schedule.

**Tech Stack:** Unity 6000.5.7f1, URP 17.5, Addressables 2.8.1, code-authored uGUI/runtime views, NUnit EditMode and PlayMode tests.

## Global Constraints

- Windows Standalone is the validated target; mobile remains a later project.
- Do not add per-enemy, per-projectile, per-bomb, or per-tentacle MonoBehaviours.
- Preserve separate deterministic gameplay and cosmetic RNG streams.
- Do not edit `ContentCatalog.Generated.cs`; append Unity-first content through a hand-authored catalogue.
- Hydra’s central vertebral chain never collides with the player.
- Outer rib collision exists only while Hydra Prime is active.
- Hydra Prime’s Evasion uses a shuffled six-socket bag with no immediate repeat.
- Marrow Barrage drops exactly four sequential bombs at the player’s sampled position; each next target must require a meaningful dodge.
- Rib projectiles use 80% of the approved preview size.
- No ambient enemies, elites, meteors, or scheduled bosses may spawn during the Hydra Prime phase.
- Boss damage disintegrates in this order: crown, upper-right, upper-left, lower-right/mouth, lower-left, eye last.
- Arena and boss identity effects must not tint the player or HUD.

---

### Task 1: Deterministic Hydra rules and content

**Files:**
- Create: `Assets/VoidFall/Core/HydraEncounterRules.cs`
- Create: `Assets/VoidFall/Content/HydraContent.cs`
- Modify: `Assets/VoidFall/Core/Ids.cs`
- Modify: `Assets/VoidFall/Core/ArenaCatalogRules.cs`
- Modify: `Assets/VoidFall/Content/ArenaCycleRules.cs`
- Test: `Assets/VoidFall/Tests/Editor/HydraEncounterRulesTests.cs`

**Interfaces:**
- Produces: `HydraEncounterRules.BuildEvasionOrder(Rng)`, `HydraEncounterRules.BuildMarrowOffsets(Rng)`, `HydraEncounterRules.DamageRegion(double)`, `HydraContent.Arena`, and `HydraContent.Boss`.
- Preserves: generated catalogue order and standard boss rotation.

- [ ] Write failing EditMode tests proving a six-element Evasion permutation, no duplicate socket, no immediate first-socket repeat, four Marrow targets with minimum displacement, ordered damage regions, Hydra stable-id round-trip, and route-only boss data.
- [ ] Run the targeted tests and confirm they fail because Hydra types and enum values do not exist.
- [ ] Add `ArenaId.Hydra` after the existing three values and append it to `ContentOrder.Arenas` without reordering legacy entries.
- [ ] Implement engine-free Hydra encounter rules using fixed arrays and the existing `Rng`.
- [ ] Add the Hydra arena cycles (`dormant`, `breathing`, `hostile`, `rupture`) and Hydra Prime attacks (`hydra-marrow`, `hydra-evasion`, `hydra-ribs`, `hydra-optic`) in `HydraContent`.
- [ ] Route arena lookup through the extended catalogue while leaving `ContentCatalog.Generated.cs` untouched.
- [ ] Run the targeted tests and confirm they pass.

### Task 2: Prepared Hydra arena and menu catalogue

**Files:**
- Modify: `Assets/VoidFall/Runtime/Gameplay/ArenaPlateFactory.cs`
- Modify: `Assets/VoidFall/Runtime/Gameplay/VoidFallGameRuntime.Arena.cs`
- Modify: `Assets/VoidFall/Runtime/Gameplay/VoidFallGameRuntime.Rift.cs`
- Modify: `Assets/VoidFall/Editor/ArenaContentBaker.cs`
- Modify: `Assets/VoidFall/Editor/ArenaAddressableMigration.cs`
- Modify: `Assets/VoidFall/Editor/PreparedContentBuildSetup.cs`
- Test: `Assets/VoidFall/Tests/Editor/ArenaBakeTests.cs`
- Test: `Assets/VoidFall/Tests/Editor/ArenaCatalogueTests.cs`

**Interfaces:**
- Consumes: `ArenaId.Hydra`, stable id `hydra`, `HydraContent.Arena`.
- Produces: three addressable Hydra recipes at `VoidFall/Arenas/hydra/recipe-1..3`.

- [ ] Add failing tests for Hydra plate generation, green identity, prepared dimensions, three recipes, package addresses, and menu catalogue inclusion.
- [ ] Run the targeted arena tests and confirm the failures are Hydra-specific.
- [ ] Add a cached Hydra plate spec with black-green field, toxic scale detail, ivory bone staining, and no player/HUD tinting.
- [ ] Extend runtime arena naming, save-name conversion, cycle visuals, recipe selection, and route mapping to `ArenaId.Hydra`.
- [ ] Extend baker, validation, preload setup, and Addressables migration lists to include Hydra.
- [ ] Run the targeted arena tests and confirm they pass before baking large assets.

### Task 3: Route-owned Hydra Prime lifecycle

**Files:**
- Modify: `Assets/VoidFall/Core/VoidObjectives.cs`
- Modify: `Assets/VoidFall/Runtime/Gameplay/CombatStateTypes.cs`
- Modify: `Assets/VoidFall/Runtime/Gameplay/VoidFallGameRuntime.Objectives.cs`
- Modify: `Assets/VoidFall/Runtime/Gameplay/VoidFallGameRuntime.Sim.cs`
- Create: `Assets/VoidFall/Runtime/Gameplay/VoidFallGameRuntime.Hydra.cs`
- Test: `Assets/VoidFall/Tests/Editor/VoidObjectiveTests.cs`
- Test: `Assets/VoidFall/Tests/Editor/HydraRuntimeRulesTests.cs`

**Interfaces:**
- Consumes: Hydra objective phase, `HydraContent.Boss`, existing `BossState` pool.
- Produces: one `hydra-prime` boss spawned only when Hydra reaches its boss phase.

- [ ] Add failing tests proving the objective targets `hydra-prime`, the encounter is stationary, ambient spawns are blocked, cleanup resets all Hydra state, and the central spine is excluded from collision.
- [ ] Run the targeted tests and verify the expected failures.
- [ ] Spawn Hydra Prime at a stable arena-relative home position when phase two of the Hydra objective begins.
- [ ] Despawn existing ordinary enemies without rewards, clear hostile hazards, suppress the director and regular boss schedule, and prevent new enemies/meteors until Hydra Prime dies.
- [ ] Clamp the player inside the outer rib cage only while the boss is active; never collide against the central chain.
- [ ] Reset encounter state on arena transition, run restart, menu entry, player death, and boss defeat.
- [ ] Run the targeted tests and confirm they pass.

### Task 4: Hydra attacks

**Files:**
- Modify: `Assets/VoidFall/Runtime/Gameplay/VoidFallGameRuntime.Hydra.cs`
- Modify: `Assets/VoidFall/Runtime/Gameplay/VoidFallGameRuntime.Sim.cs`
- Modify: `Assets/VoidFall/Runtime/Gameplay/GameSim.cs`
- Test: `Assets/VoidFall/Tests/Editor/HydraRuntimeRulesTests.cs`

**Interfaces:**
- Consumes: existing boss states 1/2/3 for windup/active/recovery and fixed hostile-shot pool.
- Produces: deterministic Marrow, Evasion, Rib Guillotine, and Optic Rupture behavior.

- [ ] Add failing tests for exactly four delayed Marrow bombs, sampled-player targets, six randomized Evasion sockets, Evasion invulnerability, 20%-smaller rib shot radius, optic sweep containment, and attack cleanup.
- [ ] Run the targeted tests and verify each failure is behavior-specific.
- [ ] Implement four pooled Marrow bomb states with visible warning, delayed impact, one damage application, and minimum-distance randomized sequencing.
- [ ] Implement Evasion as a six-socket shuffled route, boss invulnerability during the active state, and safe return to the home socket.
- [ ] Spawn Rib Guillotine projectiles from both cage sides using radius and visual scale `0.8` of the approved baseline.
- [ ] Reuse the existing beam containment rules for Optic Rupture with a Hydra-specific continuous sweep and cooldown.
- [ ] Run the targeted tests and confirm they pass.

### Task 5: Hydra presentation and ordered disintegration

**Files:**
- Modify: `Assets/VoidFall/Runtime/Gameplay/ProceduralSpriteFactory.cs`
- Modify: `Assets/VoidFall/Runtime/Gameplay/VoidFallGameRuntime.Render.cs`
- Modify: `Assets/VoidFall/Runtime/Gameplay/VoidFallGameRuntime.Hydra.cs`
- Create: `Assets/VoidFall/Resources/VoidFall/HydraDisintegrate.shader`
- Test: `Assets/VoidFall/Tests/Editor/GameplayRegressionTests.cs`
- Test: `Assets/VoidFall/Tests/Editor/UrpMigrationTests.cs`

**Interfaces:**
- Produces: Hydra Prime brain/eye/teeth sprite, ten pooled thin toxic tentacles, ghost/active rib cage, vertebral chain, attack telegraphs, and shader damage progression.

- [ ] Add failing static/editor tests for shader availability, required property names, Hydra sprite generation, ten tentacle slots, continuous optic beam geometry, and rib projectile scale `0.8`.
- [ ] Run the targeted tests and confirm the failures.
- [ ] Add the asymmetric Hydra Prime sprite and one cached Hydra material instance; never allocate materials in the render loop.
- [ ] Build ten bounded line-renderer tentacles at the approved 40% width, animate them from the presentation clock, and keep them behind the head.
- [ ] Render the rib cage at dormant opacity with no collision, active opacity with outer collision, and a permanently non-colliding central vertebral chain.
- [ ] Implement the URP unlit disintegration shader with quantized pixel cells and the approved region order, including eye-last death.
- [ ] Emit detached toxic pixel bursts from the currently eroding region and apply aggressive strike shake versus subtle idle tremor.
- [ ] Render Marrow bomb warnings/impacts, Evasion sockets, 20%-smaller rib shards, and a layered white-core Optic laser.
- [ ] Run the targeted rendering/static tests and confirm they pass.

### Task 6: Hydra mutation survival phase

**Files:**
- Modify: `Assets/VoidFall/Runtime/Gameplay/CombatStateTypes.cs`
- Modify: `Assets/VoidFall/Runtime/Gameplay/VoidFallGameRuntime.Sim.cs`
- Modify: `Assets/VoidFall/Runtime/Gameplay/VoidFallGameRuntime.Render.cs`
- Test: `Assets/VoidFall/Tests/Editor/MutationRulesTests.cs`

**Interfaces:**
- Consumes: existing `MutationRules` and gameplay RNG.
- Produces: one compatible Hydra gene per eligible survival-phase enemy, with stored modifiers and readable accent.

- [ ] Add failing tests proving Hydra-only mutation rolls, incompatibility filtering, stable RNG use, modifier application, and boss-phase suppression.
- [ ] Run the mutation tests and verify the new cases fail.
- [ ] Store the selected gene in pooled enemy state, apply modifiers once at spawn, and render a bounded gene accent without changing base silhouettes.
- [ ] Confirm non-Hydra spawn behavior and golden-master seeds remain unchanged.
- [ ] Run mutation and regression tests and confirm they pass.

### Task 7: Bake, validate, and document

**Files:**
- Generate: `Assets/VoidFall/Generated/Arenas/Hydra/Base.png`
- Generate: `Assets/VoidFall/Generated/Arenas/Hydra/Details.png`
- Generate: `Assets/VoidFall/Generated/ArenaPackages/Hydra/Plate.asset`
- Generate: `Assets/VoidFall/Generated/ArenaPackages/Hydra/Recipe1.asset`
- Generate: `Assets/VoidFall/Generated/ArenaPackages/Hydra/Recipe2.asset`
- Generate: `Assets/VoidFall/Generated/ArenaPackages/Hydra/Recipe3.asset`
- Modify: `Assets/AddressableAssetsData/*` only through Unity's Addressables APIs
- Modify: `Docs/Architecture.md`
- Modify: `Docs/Design/VoidFallArenaArchitecture.md`
- Modify: `Docs/AI/UnityProjectContext.md`

**Interfaces:**
- Produces: imported, mip-streamed, non-readable Hydra content and validation evidence.

- [ ] Run the editor bake and Addressables migration; confirm Hydra produces one 4K base, one 1440p detail plate, and three valid recipes.
- [ ] Inspect generated/imported assets, `.meta` files, Addressables labels, and serialized references.
- [ ] Run `dotnet build VoidFall.Runtime.csproj -t:Rebuild`.
- [ ] Run all EditMode tests and record exact pass/fail totals.
- [ ] Run all PlayMode tests and record exact pass/fail totals.
- [ ] Build Windows Release and launch a Hydra visual/runtime smoke capture at 720p and 1080p.
- [ ] Confirm Marrow forces four dodges, Evasion changes order between cycles without immediate repeats, outer ribs collide, the spine does not, the optic beam is readable, and boss death removes the eye last.
- [ ] Inspect the final diff for unrelated changes and update the three project documents with exact validated status and limitations.
