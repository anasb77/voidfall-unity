# VoidFall Unity — Architecture

Last updated: 2026-08-21 (commit `c8914bd`)

This document describes the codebase as it actually is. For migration history
and honest caveats about what is verified, see `MIGRATION_STATUS.md`.

## Product

Endless space-survival shooter. Unity 6 (`6000.5.7f1`), URP 17.5, Windows
Standalone target. One scene (`Assets/Scenes/SampleScene.unity`, camera only);
the application bootstraps itself from code via
`ParityFixtureProbe.CreateProbe`. Zero prefabs; all uGUI views are built at
runtime from `Assets/VoidFall/UI/`.

## Assembly map

```text
VoidFall.Core        engine-free simulation rules, RNG, collision grid, balance math
VoidFall.Content     generated catalog + hand-written elite/roster/upgrade/evolution rules
                     (rootNamespace is VoidFall.Core - its types live in that namespace)
VoidFall.Persistence schema-v5 JSON saves, atomic writes, browser import/export
VoidFall.Audio       procedural SFX + streamed reactive soundtrack
VoidFall.UI          uGUI + TextMeshPro views (UIBuilder/UITheme/UIManager + 12 views)
VoidFall.Runtime     gameplay driver: simulation logic, rendering, HUD coordination,
                     input, telemetry, stress probes
```

Dependency direction: Runtime → {Core, Content, Persistence, Audio, UI};
Content → Core; Persistence → Core+Content; Core depends on nothing.

## The runtime class and its extraction state

`VoidFallGameRuntime` remains one class (partial across several files), but its
contents are being extracted into plain-C# owner classes. Status:

| Concern | Owner | State |
| --- | --- | --- |
| Gameplay input polling | `Runtime/Input/InputReader.cs` | fully extracted |
| Cosmetic FX (particles, shards, ring waves) | `Runtime/Gameplay/FxSim.cs` | fully extracted (state + pure logic); view sync + ParticleSystem emission stay on runtime |
| Combat state (enemies, bullets, shots, pickups, bosses, meteors, orders, grid buffers, combat RNG) | `Runtime/Gameplay/GameSim.cs` | v0: state ownership complete; method bodies still on runtime |
| HUD construction/update | partial file `VoidFallGameRuntime.Hud.cs` | consolidated; presenter extraction pending |
| Menus/settings/workshop/records | partial file `...UI.cs` + `VoidFall.UI` views | views migrated; controller logic still on runtime |
| Arena rendering | partial file `...Arena.cs` + `ArenaPlateFactory`, `ArenaResidencyManager` | plates/residency extracted; renderer promotion pending |
| Data types | `Runtime/Gameplay/CombatStateTypes.cs` | combat structs promoted to namespace-level |

Partial files of the runtime class live beside it:
main state/lifecycle, `.Sim`, `.Render`, `.UI`, `.Hud`, `.Arena`, `.Fx`,
`.Persist`, `.Audio`.

## Deterministic simulation contract

- Fixed-step simulation driven by `FixedGameLoop` calling `Simulate(double dt)`.
- Two independent random streams, seeded per run:
  - `GameSim.Rng` — combat draws;
  - `FxSim.FxRng` — cosmetic draws.
- Pooled slot arrays with insertion-order bookkeeping (`SlotOrder` where six
  families shared exact semantics; specialized bookkeeping preserved for
  enemy/boss/meteor/pickup whose browser semantics genuinely differ).
- Quality presets scale cosmetic budgets only, never resolution or gameplay.

## Regression safety net

1. **Golden-master PlayMode test**
   (`Assets/VoidFall/Tests/PlayMode/SimulationGoldenMasterTests.cs`): boots the
   real runtime, applies the `productionMax` scenario with seed `0x5f1dc0de`,
   steps `Simulate` 600 fixed ticks, hashes every gameplay state array
   bit-exactly against a pinned constant (`15261090775683682834`).
2. **EditMode suite** — 57 tests under `Assets/VoidFall/Tests/Editor/`.
3. **Compile gate** — `dotnet build VoidFall.Runtime.csproj -t:Rebuild`
   (0 errors; 14 known CS0649 warnings in `ParityFixtureProbe`).
4. **Visual captures** — `-vfcapture*` player flags write screenshots;
   baselines under `semantic-review/captures-*/`.

Commands (from repo root):

```text
dotnet build VoidFall.Runtime.csproj -t:Rebuild
"C:\Program Files\Unity\Hub\Editor\6000.5.7f1\Editor\Unity.exe" -batchmode
  -projectPath <repo> -runTests -testPlatform EditMode  -testResults <out.xml>
  ... and -testPlatform PlayMode for the golden master.
```

## Known open work

Tracked in `MIGRATION_STATUS.md` and `Docs/AI/UnityProjectHealth.md`:
migrate `GameSim` method bodies inward family by family; extract HudPresenter
and ArenaRenderer as real classes; move menu controllers into `VoidFall.UI`;
shrink the runtime MonoBehaviour to a composition root; VF-010..VF-012 release/
input/accessibility gaps.
