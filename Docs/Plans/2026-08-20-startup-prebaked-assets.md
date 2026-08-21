# Startup Prebaked Assets Implementation Plan

> **For agentic workers:** Execute task-by-task with test-first red/green cycles and fresh Unity validation after every independently testable task.

**Goal:** Remove expensive procedural arena and gameplay-sprite generation from the interactive menu and normal player startup while preserving the current three arenas and their visuals.

**Architecture:** An Editor-only baker converts the existing deterministic procedural output into imported Unity assets under one generated tree. Runtime code loads a small catalog of prepared sprites before scene composition and asynchronously resolves prepared arena plates, retaining the existing procedural implementation only as an Editor/development recovery path during this migration. The later graph-aware Addressables phase will replace the temporary local Resources transport without reintroducing runtime generation.

**Tech Stack:** Unity 6000.5.7f1, C#, URP 17.5, uGUI, Unity Test Framework, Editor `AssetDatabase`, imported compressed textures, Windows standalone.

## Global constraints

- Work only in `voidfall-unity`; `voidfall.io` stays untouched.
- Preserve all pre-existing dirty working-tree changes; never reset or clean the repository.
- Do not add a second project, worktree, dependency-injection framework, or scene.
- Keep procedural art algorithms as the source for the Editor baker until visual parity is proven.
- Do not synchronously generate arena pixels or warm hundreds of sprites after the menu becomes visible.
- Generated runtime assets live only under `Assets/VoidFall/Generated/`.
- Editor-only code lives only under `Assets/VoidFall/Editor/`.
- Existing save IDs, `ArenaId` values, gameplay rules, and balance remain unchanged.
- No task commit may accidentally stage unrelated dirty files. Because the main runtime files are already dirty, final integration is validated before deciding how to commit them.

---

### Task 1: Prepared arena asset contract

**Files:**

- Create: `Assets/VoidFall/Runtime/Gameplay/ArenaPlateAsset.cs`
- Create: `Assets/VoidFall/Runtime/Gameplay/ArenaPlateProvider.cs`
- Modify: `Assets/VoidFall/Tests/Editor/GameplayRegressionTests.cs`

**Interfaces:**

- Produces: `ArenaPlateAsset` with arena, base/detail sprites, dimensions, and schema version.
- Produces: `ArenaPlateProvider.ResourcePath(ArenaId)` and `Load(ArenaId)`.
- Provider returns `null` for a missing prepared asset; it never invokes `ArenaPlateFactory`.

- [ ] Add a reflection-based failing test proving the runtime assembly does not yet expose `ArenaPlateAsset`.
- [ ] Run the EditMode suite and confirm that exact assertion fails.
- [ ] Add `ArenaPlateAsset` and `ArenaPlateProvider` with the following public contract:

```csharp
public sealed class ArenaPlateAsset : ScriptableObject
{
    public const int CurrentSchema = 1;
    public ArenaId Arena { get; }
    public Sprite BaseSprite { get; }
    public Sprite DetailSprite { get; }
    public int Width { get; }
    public int Height { get; }
    public bool IsValidFor(ArenaId arena);
}

public static class ArenaPlateProvider
{
    public static string ResourcePath(ArenaId arena);
    public static ArenaPlateAsset Load(ArenaId arena);
}
```

- [ ] Add direct tests for exact paths, mismatched arena rejection, and missing-asset `null` behavior.
- [ ] Run EditMode tests and confirm all pass.

### Task 2: Editor arena baker and validation

**Files:**

- Create: `Assets/VoidFall/Editor/ArenaContentBaker.cs`
- Create: `Assets/VoidFall/Tests/Editor/ArenaBakeTests.cs`
- Generate: `Assets/VoidFall/Generated/Arenas/<ArenaId>/Base.png`
- Generate: `Assets/VoidFall/Generated/Arenas/<ArenaId>/Details.png`
- Generate: `Assets/VoidFall/Generated/Resources/VoidFall/Generated/Arenas/<ArenaId>/Plate.asset`

**Interfaces:**

- Produces: `VoidFall.Editor.ArenaContentBaker.BakeAll()` batch/menu entry.
- Produces: `ValidateAll(bool throwOnError)` returning validation messages.
- Uses one fixed 3021x1699 High source per current arena, preserving 2560x1440 visible coverage under 1.18x overscan.

- [ ] Add a failing EditMode validation test expecting all three generated `ArenaPlateAsset` resources.
- [ ] Run the test and confirm failure names the first missing arena asset.
- [ ] Implement deterministic PNG generation by calling `ArenaPlateFactory.BuildBasePixels` and `BuildDetailPixels` in the Editor.
- [ ] Configure importers as Sprite, bilinear, clamp, mipmapped, mip-streaming enabled, non-readable, and Standalone BC7 where Unity accepts it.
- [ ] Create/update the three `ArenaPlateAsset` resources without deleting unrelated assets.
- [ ] Run `ArenaContentBaker.BakeAll` through Unity batch mode.
- [ ] Run EditMode tests and confirm dimensions, arena IDs, sprite references, non-readable textures, and schema pass.

### Task 3: Runtime arena integration

**Files:**

- Modify: `Assets/VoidFall/Runtime/Gameplay/VoidFallGameRuntime.cs`
- Modify: `Assets/VoidFall/Tests/Editor/GameplayRegressionTests.cs`

**Interfaces:**

- `TryInstallPreparedArenaPlate(ArenaId)` resolves one baked plate and installs both sprites.
- `EnsureArenaPlateViewport` scales/crops prepared sprites; changing window size does not regenerate pixels.
- Development/Editor missing assets retain the existing fallback with one warning. Valid player builds are prevented from shipping without assets by Task 6.

- [ ] Add a failing test that installs a real generated plate into a disabled runtime host and verifies both arena sprite slots reference imported assets.
- [ ] Run it and confirm failure occurs because prepared-plate installation is absent.
- [ ] Implement the minimal installation path and retain sprite ownership with Unity assets rather than destroying imported sprites/textures.
- [ ] Remove viewport invalidation of prepared assets and remove the menu background bake task from the normal prepared-asset path.
- [ ] Keep current visual scale/overscan and detail ordering unchanged.
- [ ] Run EditMode tests and Unity compilation.

### Task 4: Prepared procedural-sprite catalog

**Files:**

- Create: `Assets/VoidFall/Runtime/Gameplay/ProceduralSpriteCatalog.cs`
- Modify: `Assets/VoidFall/Runtime/Gameplay/ProceduralSpriteFactory.cs`
- Modify: `Assets/VoidFall/Tests/Editor/GameplayRegressionTests.cs`

**Interfaces:**

- `ProceduralSpriteCatalog` stores flat stable-key-to-Sprite entries.
- `ProceduralSpriteFactory.InstallBakedCatalog(ProceduralSpriteCatalog)` hydrates the existing fixed fields, arrays, and dictionaries.
- `ProceduralSpriteFactory.BuildCatalogSnapshot()` is used only by the Editor baker to warm and enumerate every supported sprite key.

- [ ] Add a failing test where a synthetic catalog installs known Circle, Gem, enemy, projectile-frame, and arena-vignette sprites and each existing getter returns the exact prepared reference.
- [ ] Run it and confirm catalog installation is missing.
- [ ] Implement stable keys for fixed sprites, indexed arrays, enemy/boss color keys, projectile frames, pickups, meteors, arena dots, and workshop layers.
- [ ] Implement catalog installation without changing existing getter signatures.
- [ ] Implement complete Editor snapshot coverage for fixed startup sprites, all catalog enemies/bosses, five 32-frame weapon sets, pickups, meteors, arena variants, and workshop layer ranks.
- [ ] Run EditMode tests and confirm installing a catalog bypasses raster builders for covered keys.

### Task 5: Editor procedural-sprite baker

**Files:**

- Create: `Assets/VoidFall/Editor/ProceduralSpriteBaker.cs`
- Create: `Assets/VoidFall/Tests/Editor/ProceduralSpriteBakeTests.cs`
- Generate: `Assets/VoidFall/Generated/Sprites/*.png`
- Generate: `Assets/VoidFall/Generated/Resources/VoidFall/Generated/ProceduralSpriteCatalog.asset`

**Interfaces:**

- `VoidFall.Editor.ProceduralSpriteBaker.BakeAll()` writes imported sprite textures and catalog references.
- Generated texture groups preserve source rect, pivot, PPU, filter, wrap, and alpha.
- Imported textures are non-readable and use Standalone BC7 where accepted.

- [ ] Add a failing validation test requiring the generated catalog and representative keys from each sprite family.
- [ ] Run the test and confirm the catalog is missing.
- [ ] Implement texture-group export, assigning unique imported sprite names even when two source sprites share a display name.
- [ ] Configure each generated texture importer, then rebuild catalog references from imported Sprite sub-assets.
- [ ] Bake through Unity batch mode.
- [ ] Run tests that verify required keys, non-null sprites, visible alpha bounds, shared atlas textures for packed gameplay families, and non-readable imported textures.

### Task 6: Startup catalog hydration and build gate

**Files:**

- Modify: `Assets/VoidFall/Runtime/Gameplay/VoidFallGameRuntime.cs`
- Modify: `Assets/VoidFall/Editor/BuildScript.cs`
- Create: `Assets/VoidFall/Editor/PreparedContentBuildValidator.cs`
- Modify: `Assets/VoidFall/Tests/Editor/GameplayRegressionTests.cs`

**Interfaces:**

- Runtime loads `VoidFall/Generated/ProceduralSpriteCatalog` before `SetupBackdrop`, `SetupHud`, and `SetupPlayer`.
- A valid catalog means `_spriteWarmSteps` remains `null`; Start Run has nothing to drain.
- Build validation rejects missing/stale arena or sprite assets before packaging.

- [ ] Add a failing test proving a runtime with a valid catalog has no pending warm iterator after startup asset hydration.
- [ ] Run and confirm failure against current initialization.
- [ ] Load/install the catalog before visual object creation and create the warm iterator only when the catalog is missing in Editor/development recovery.
- [ ] Add the build preprocessor validator and call the same validation from `BuildScript.BuildWindows`.
- [ ] Run EditMode tests and Unity compilation.

### Task 7: Standalone proof and handoff

**Files:**

- Modify only if evidence requires: implementation files from Tasks 1-6.
- Produce outside source control: `Logs/prebake-*.log`, `Logs/prebake-tests.xml`, Windows player under `Builds/`.

- [ ] Run the full EditMode suite and read the XML for test/failure counts.
- [ ] Run Unity batch compilation and inspect errors/warnings.
- [ ] Build Windows standalone through the repository build method.
- [ ] Cold-launch the player, capture Player.log, and measure process start to visible/interactable menu using the existing capture hook where available.
- [ ] Confirm logs contain no arena pixel bake and no procedural sprite warm during the interactive menu.
- [ ] Compare representative XP, enemy, projectile, Sakura, Red Nebula, and Abyss visuals against the current build.
- [ ] Inspect `git diff` and generated asset inventory; ensure no unrelated file was changed by Unity import.
- [ ] Report exact test counts, build result, startup timing, remaining risks, and the next Addressables migration step.

## Self-review

- Spec coverage: this phase permanently moves current arena and sprite authoring out of interactive runtime, preserves visuals, gates missing assets, and measures the result. Seeded graph, ten-arena content, Addressables residency, and four-state behavior deliberately remain separate later phases.
- Placeholder scan: no TODO/TBD/FIXME or unnamed error-handling step remains.
- Type consistency: arena resource paths and sprite catalog resource paths are produced by the Editor bakers and consumed by runtime/build validation with the exact same constants.
- Rollback: runtime fallback remains available in Editor/development during migration. Generated assets are isolated under one directory and existing saves/scenes are unchanged.

