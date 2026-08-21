# VoidFall Unity — Migration Status

Last updated: 2026-08-21

## 2026-08-21 addendum — architecture refactor in progress

This addendum supersedes conflicting statements below it. The document body
still describes the 2026-08-16 state and is being revised.

What changed since 2026-08-16, all verified from this checkout:

- **The working tree was committed.** The entire uGUI layer, the prepared
  arena/sprite assets under `Assets/VoidFall/Generated/`, six EditMode test
  files, audit captures, and the URP settings are now in git (commit
  `63b6ce5`). The VF-008 "untracked UI" risk is closed.
- **URP 17.5 is installed and active** (`com.unity.render-pipelines.universal`
  in `Packages/manifest.json`, activated in commits through `ef9f9a9`). The
  body's "no scriptable render pipeline / no bloom" statements are obsolete.
- **There is a sixth assembly, `VoidFall.UI`** (uGUI + TextMeshPro). The body's
  "UI/Mobile/Editor folders are empty" and "UI is legacy IMGUI" statements are
  obsolete. `Mobile` is still empty.
- **Tests exist in-repo**: 57 EditMode tests across six files under
  `Assets/VoidFall/Tests/Editor/` (run: Unity batchmode `-runTests
  -testPlatform EditMode`; current result 57/57), plus one PlayMode
  golden-master determinism test under `Assets/VoidFall/Tests/PlayMode/`.
- **`VoidFallGameRuntime.cs` is split into partial class files by concern**
  (main state/lifecycle file plus `Sim`/`Render`/`UI`/`Arena`/`Fx`/`Persist`/
  `Audio` partials). It is still one class — the structural problem described
  below remains until extraction completes — but it is navigable again.
- **Extraction started**: gameplay device polling lives in
  `Runtime/Input/InputReader.cs`; the six byte-identical slot-order field
  trios are unified into `Runtime/Gameplay/SlotOrder.cs`; all fourteen HUD
  methods are consolidated into `Runtime/Gameplay/VoidFallGameRuntime.Hud.cs`.
  The cosmetic-FX simulation has moved out of the runtime entirely
  (`Runtime/Gameplay/FxSim.cs`): its state arrays, shared insertion-order
  bookkeeping, FX random stream, update loops, and spawn insertion logic are
  now plain C#; only view syncing and the ParticleSystem emission calls
  remain on the runtime. Every step was verified behavior-neutral: rebuild
  0 errors, EditMode 57/57, and the PlayMode golden-master hash
  (`15261090775683682834`) unchanged throughout.
- **VF-002 and VF-006 from `Docs/AI/UnityProjectHealth.md` are fixed**:
  vertical layouts now honor child preferred heights, and main-menu status
  dividers keep their 1px contract. Verified with committed player captures
  (home/settings/workshop/records at 1280x720 and 1920x1080) under
  `semantic-review/captures-20260821-ui-layout-fix/`, EditMode 57/57.
  **VF-009 is fixed**: HUD labels only rewrite when their source value
  changes, removing per-frame string allocations. The PlayMode test asmdef is
  constrained to `UNITY_INCLUDE_TESTS` so player builds exclude it.
- **The golden master is the regression net for further extraction.** It boots
  the real runtime, applies `productionMax` with seed `0x5f1dc0de`, steps
  `Simulate` 600 fixed ticks, and hashes all gameplay state bit-exactly. Any
  refactor that changes simulation behavior fails it. Intentional behavior
  changes must regenerate the constant in a separate, clearly described commit.

Remaining extraction order (agreed design): HudPresenter, ArenaRenderer,
GameSim behind a facade, menu controllers into `VoidFall.UI`, then shrink the
runtime to a composition root. See `Docs/AI/UnityProjectHealth.md` for the
audit findings (VF-001..VF-014) that motivate this order.

## What this document is

An honest description of what the Unity port currently is, what is verified, and
what is not. It replaces an earlier version that had grown into a long log of
"gates," each asserting a pixel-level parity claim as verified. That log was
removed for two reasons:

1. Several of its most recent claims described behaviour that no longer exists.
   It asserted the fullscreen overlay backdrop-filter approximation, the
   card-local blur, and dynamic render-scale as implemented and passing. All
   three have since been removed or disabled (see **Rendering**).
2. The test runs it cited as evidence live outside this repository, so none of
   them can be reproduced from a clone. Numbers that cannot be re-derived are
   not evidence.

Where this document says something is verified, it names the command that
verifies it.

## Authority

The React/TypeScript game in the sibling `voidfall.io` repository remains the
behavioural and content authority. This port is expected to match its content
and simulation exactly. It is **not** expected to match its rasterization.

That second point is a deliberate change of direction. A large amount of earlier
effort went into reproducing Canvas2D output by hand — rasterizing CSS radial
gradients, `box-shadow` alpha falloff, and `backdrop-filter` blur into
`Texture2D`, and faking `text-shadow` by drawing the same label 24–36 times at
ring offsets. That work made the port slower without making it look better, and
in the case of render-scale it actively made it look worse. Visual work should
now target "correct and sharp in Unity's idiom," not "bit-identical to a
browser."

## Architecture (as built)

Five assembly definitions under `Assets/VoidFall/`:

| Assembly | Contents |
| --- | --- |
| `VoidFall.Core` | Engine-free simulation rules, collision grid, RNG, quality/balance/meteor/pickup rules |
| `VoidFall.Content` | Generated content catalog plus hand-written elite, roster, upgrade, evolution rules |
| `VoidFall.Runtime` | All Unity behaviour: simulation driver, rendering, HUD, menus, sprite/plate factories, telemetry |
| `VoidFall.Persistence` | Save store, browser save import/export |
| `VoidFall.Audio` | Procedural audio |

Things worth knowing before working in here:

- `Runtime/Gameplay/VoidFallGameRuntime.cs` is a single ~25,000-line class holding
  the simulation, every enemy behaviour, all rendering, the HUD, every menu, the
  settings flow, and telemetry. It is the project's main structural problem and
  the reason most changes are riskier than they should be.
- The UI is legacy IMGUI (`OnGUI`), roughly half the monolith. IMGUI re-runs
  layout and draw every frame, allocates per frame, cannot be batched, and has
  no SDF text. This is the largest remaining cause of poor visual quality.
- There is no scriptable render pipeline. `Packages/manifest.json` contains
  `com.unity.feature.2d` and no URP package, so there is no post-processing
  stack and therefore no bloom. For a neon-heavy game this is the single
  largest available visual improvement and is not yet done.
- One scene (`Assets/Scenes/SampleScene.unity`), zero prefabs. Every object is
  constructed in code at runtime.
- `Assets/VoidFall/UI`, `Mobile`, and `Editor` are empty. `Rendering` holds only
  shaders and icon assets. Earlier documentation described these as assemblies;
  they are not.

## Content parity — verified, and the strongest part of the port

`Assets/VoidFall/Content/ContentCatalog.Generated.cs` is machine-generated from
the source repository by `scripts/generate-unity-content.ts` and carries its
provenance in code:

```csharp
public const string SourceCommit = "4d5e955";
```

A field-by-field comparison against the TypeScript source found no numeric
drift anywhere:

- 6 weapons × 6 ranks × 15 stat fields (540 values)
- 14 enemies, emitted in `ENEMY_ORDER` (required, since the runtime indexes
  these arrays positionally)
- 4 bosses with 8 attacks, the scheduled Elite, 3 elite variants
- 10 support upgrades, 3 late upgrades, 6 evolutions with matching gates
- 13 spawn bands, 3 arenas, operative stats, XP curve, Roster II curve

Floats are emitted at full double precision (`Cooldown = 0.41999999999999998` is
the exact IEEE-754 double for `0.42`, not drift).

**The maintenance rule is a provenance check, not a value diff.** If
`voidfall.io` HEAD moves past `4d5e955` with changes under `src/game/`, re-run
the generator. The only numbers that can drift independently are the
hand-written ones: `EliteRules.cs`, `EnemyRosterRules.cs`, `BalanceRules.cs`,
`EvolutionRules.cs`, `ProgressionRules.cs`, and the pool weights in
`UpgradeRules.cs`. All were correct as of this update.

Two known hardcoded lists that duplicate catalog data and can go stale silently:
`SaveStore.BestiaryOrder` / `SaveStore.IsArena`, and
`ProceduralSpriteFactory.SourceEnemyColor` / `SourceBossColor`. Adding content
without updating these will silently drop bestiary flags or bake unused sprite
cache keys.

## Rendering — current model

**The world renders straight to the backbuffer at native resolution.**

The previous path rendered the world into a downscaled `RenderTexture` and
upscaled it through a canvas `RawImage`. That resampled every frame and was a
major cause of the softness the port was criticised for. Quality presets now
scale cosmetic budgets (particle counts, floater counts, death ghosts) only;
they never change the resolution the world is rasterized at.
`QualityRules.EffectiveRenderScale` returns `1` for the High preset.

**Film grain is removed, deliberately.** The grain overlay cost clarity and, at
non-1:1 scale, read as blur rather than texture. The game's visual identity is
better served by a clean, sharp image. `ArenaPlateFactory.CreateGrainTile` still
exists and still produces a tile, but nothing enables it.

Consequences of those two decisions, now reflected in the code:

- The fullscreen overlay backdrop blur was deleted. It could only sample the
  world through the render texture, so with the render texture gone it was a
  permanent no-op. Every former call site already drew an explicit fullscreen
  dim immediately afterwards, so overlays are unchanged on screen.
- The card-local blur is now a flat scrim rather than eight offset copies of the
  frame. Cheaper and cleaner.
- The `-vfno-grain` capture flag was removed. With grain permanently off it had
  nothing to suppress and silently did nothing.
- `EnsureWorldRenderTarget`, `ReleaseWorldRenderTarget`, the render-target
  fields, and the canvas `RawImage` that displayed it were all deleted as
  unreachable.

Reinstating a real blur or any glow/bloom requires a render pipeline with
post-processing. It should not be re-attempted with multi-sample IMGUI draws.

**Open visual work, in rough priority order:** add URP + the 2D renderer and
enable bloom; replace IMGUI with UI Toolkit or uGUI + TextMeshPro; then revisit
per-effect fidelity.

## Gameplay feel deviations from source

One deliberate deviation, recorded because it is a design decision and not a bug
fix: `TriggerFreeze` clamps hitstop to `seconds * 0.4`, capped at 35 ms, where
the source applies the full requested duration. This was done to reduce
perceived stutter. It makes impacts read softer than the browser. If frame
pacing improves, this is worth revisiting, because it may have been compensating
for frame spikes rather than for the hitstop itself.

## Persistence

Schema version 5, key `voidfall_save_v4`, matching the source. Stored as JSON at
`Application.persistentDataPath`. No version-keyed migration steps on either
side; both clamp the incoming version and apply a single legacy fix-up (the
Revival Protocol refund for `0 < version < 5`). Field clamping mirrors the
source's `sanitizeSave` closely.

Recent correctness work in this area:

- `Load()` no longer performs writes inside the block whose `catch` classifies
  the file as corrupt. Previously an I/O failure — antivirus lock, cloud-sync
  hold, disk full — was indistinguishable from malformed JSON, so a valid
  profile could be backed up as "corrupt", discarded, and overwritten with
  defaults. Reading and parsing are now separate, and a read failure returns a
  default profile **without writing anything**.
- A read failure latches `StorageUnreadable`, and ordinary `Save()` calls then
  refuse to overwrite the file. This closes the deferred version of the same
  data loss, where the next successful run-save would clobber a profile the
  session simply could not see. Explicit destructive actions (reset progress,
  import browser save) pass `allowOverwriteUnreadable: true`.
- `Save()` writes through a `FileStream` with `Flush(true)` before renaming, so
  the replacement's bytes reach the device before the rename is committed.
  `File.Replace` keeps a `.bak` one-generation backup. The previous truncating
  `File.Copy(overwrite: true)` fallback was removed; a failed save now leaves
  the last good profile intact and surfaces the exception.
- Browser export is lossless. Per-run `supports`, `late`, and `evolved` are now
  emitted. React's `sanitizeRunRecord` rebuilds records from known keys and
  ignores unrecognized ones, so the document stays browser-readable while
  export → import no longer destroys those arrays across all 12 retained runs.
- `TryImportBrowserSave` writes a `.pre-import.bak` before overwriting.
- `BrowserSaveExporter.Export` operates on a detached clone.
  `SaveStore.Sanitize` clamps in place and returns the same reference, so
  exporting previously mutated the live in-memory profile.
- The workshop purchase save is guarded and rolls back parts and rank on
  failure. It was the only unguarded `Save()` call site.
- `NormalizeDate` returns `0` for unrepresentable dates instead of a far-future
  sentinel that sorted to the front of `recentRuns` and evicted a genuine run at
  the 12-entry cap.

## Verification

What can be verified from a clone of this repository:

```
dotnet build VoidFall.Runtime.csproj -t:Rebuild
```

This transitively builds all five assemblies. Current result: **0 errors, 14
warnings.** All 14 are `CS0649` in `Runtime/ParityFixtureProbe.cs` — fields
populated by `JsonUtility.FromJson`, which the compiler cannot see. They are
pre-existing and harmless.

Use `-t:Rebuild`. A plain incremental `dotnet build` can report 0 warnings from
cached results and is not a trustworthy check.

## What is not verified

Be direct about this when planning work.

- **There are no tests in this repository.** Earlier documentation cited 377
  PlayMode and 63 EditMode tests, but they live in a separate validation clone
  outside the repo. Nothing here runs them. Several of them were written against
  the render-target and backdrop-blur paths that have since been removed, so an
  unknown number now fail. Getting a test project in-repo is the highest-value
  next task, because until then no change to this codebase can be regression
  checked.
- **Runtime performance is unmeasured.** No profiling claim in this document,
  because there is no trustworthy measurement. Earlier reported frame times
  ranged from 2.7 ms to 22.1 ms for the same build and were attributed to
  "launch-mode artifacts"; they were also taken on different hardware than the
  current target. A short boot smoke with a full enemy field is a crash check,
  not a performance result.
- No Android/device coverage. The Android SDK/NDK/JDK components are not present
  in the current Unity install.
- No WebAudio equivalence testing.
- Physical touch input, safe-area behaviour, and app lifecycle on device.

## Known performance issues, not yet addressed

Identified by reading the code, not by profiling. Listed so they are not
rediscovered from scratch:

- `ProceduralSpriteFactory.Enemy` builds its cache key by string concatenation
  (`id + "/" + ColorUtility.ToHtmlStringRGBA(accent) + "/" + hit`) on every call.
  `Render()` calls it once per enemy per frame, so a full field allocates tens of
  thousands of short-lived strings per second to look up sprites that are already
  cached. `EnemySpriteAccent` compounds this with a linear string scan
  (`FindEnemy`) and a hex re-parse per enemy per frame. All three should resolve
  to an integer index and a cached `Color` at spawn.
- Eight `static readonly Dictionary<string, Texture2D>` GUI caches are never
  cleared and never `Destroy`ed. Being static, they survive scene reloads and
  accumulate for the process lifetime.
- Roughly 650 lines of near-identical `Reset/Append/Remove/Ensure*Order`
  bookkeeping is duplicated across eleven entity types. One generic order-list
  type replaces all of it and removes eleven independent chances of getting
  swap-removal index math wrong.
- `CollisionGrid.QueryCells` silently truncates when the caller's output buffer
  fills. Buffers are currently sized to `MaxEnemies` so it is likely
  unreachable, but a silent early return in collision broad-phase means missed
  hits rather than a visible failure. It should assert.

## Related documents

`PARITY_MATRIX.md` holds the detailed source-to-Unity mapping. **Its rendering
rows are stale.** Any row describing overlay backdrop-filter, card-local blur,
render-scale, or film grain no longer reflects the code. Content, simulation, and
progression rows are still accurate.
