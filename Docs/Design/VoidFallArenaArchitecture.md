# VoidFall Arena Architecture

Status: recommended design, ready for implementation planning  
Scope: Unity project only; the deprecated browser project is out of scope

## The decision

VoidFall will ship arenas as prebuilt asset packages. The player build will not generate large arena textures while it is running.

A run uses a seeded branching graph:

```text
Abyss -> choice -> choice -> choice -> Void Heart
```

The initial catalog contains ten arenas:

- one fixed starting arena: Abyss;
- eight intermediate arenas;
- one fixed final arena: Void Heart.

A normal run visits five arenas total: Abyss, three intermediate arenas, and Void Heart. The graph is generated once from the run seed. Before the player makes a choice, only the current arena package and its two possible exits are resident in memory.

Each arena has three visual recipes. A recipe changes composition and motion without changing the arena's identity. Each recipe uses:

- exactly three 4K identity layers;
- exactly three 1440p support layers;
- zero to four optional 1080p utility layers, but only when they serve a named purpose such as fog, distortion, distant debris, or a gameplay-readable mask.

Extra full-screen layers are not permitted as decoration. Three premium layers is the default and the limit.

## Terms in plain language

- **Asset**: data saved before the game ships, such as a texture, sound, material, or arena definition.
- **Baking**: running the expensive procedural generator in the Unity Editor and saving its result as assets. It is preprocessing, similar to compiling source code before running a program.
- **Prefab**: a saved template for a Unity object tree. It is not an image. For example, an arena prefab can remember its background sprites, particle systems, render layers, and components so Unity can instantiate the complete setup later.
- **ScriptableObject**: a Unity data object saved as an asset. It is useful for arena names, IDs, recipes, references, state parameters, and gameplay modifiers.
- **Addressables**: Unity's asset loader. It loads an arena package asynchronously and returns a handle. Releasing that handle decrements a reference count so Unity can unload assets that are no longer needed.
- **Resident**: currently loaded and occupying RAM and/or VRAM.
- **VRAM**: memory on the graphics card. Textures, render targets, geometry, and some graphics buffers consume it.
- **Recipe**: one authored visual arrangement for an arena. It is not a complete duplicate of every shared asset.

The Minecraft comparison is partly correct: the current game creates much of an arena procedurally. The new pipeline still lets code create the art, but it runs before shipping. The result is then loaded like any other game asset. Runtime shaders, particles, landmarks, enemies, and state changes keep the arena alive; the shipped player does not paint millions of pixels during its first frames.

## Why this is the permanent fix

The current startup stutter is caused by expensive content-authoring work running on the player's main thread. Moving the same work behind a loading screen would hide the pause but keep the architectural mistake.

The permanent ownership split is:

```text
Unity Editor                       Player build
------------                       ------------
Generate expensive pixels          Load prepared arena package
Validate dimensions/imports        Animate shaders and particles
Build atlases and bundles           Simulate gameplay
Reject invalid content              Release obsolete package handles
```

This makes startup cost independent of procedural texture complexity. It also makes arena content inspectable, testable, compressible, and reusable.

## Run graph

### Shape and timing

The first version targets a 45-to-55-minute successful run:

| Chapter | Target time | Purpose |
| --- | ---: | --- |
| Abyss | 7-9 min | establish build and teach the run's threat pattern |
| Depth 1 | 9-11 min | first meaningful route choice |
| Depth 2 | 9-11 min | build specialization and stronger arena identity |
| Depth 3 | 9-11 min | hostile/rupture pressure and final preparation |
| Void Heart | 9-12 min | final boss and escape attempt |

These are tuning ranges, not hard timers. An arena transition is caused by completing its objective, not by the current random 600-to-900-second automatic switch.

### Generation rules

`RunGraphGenerator` is pure deterministic C# code. Given the same seed and content version, it produces the same graph.

The initial rules are:

1. Abyss is always the root and Void Heart is always the final node.
2. The three route choices happen after Abyss, Depth 1, and Depth 2. Depth 3 leads to Void Heart.
3. Sibling choices cannot be the same arena.
4. A route cannot repeat an intermediate arena.
5. Choices should contrast using arena tags such as control-heavy versus mobility-heavy, or safe reward versus volatile reward.
6. Every branch must reach the final node.
7. The chosen recipe is deterministic from the run seed and node identity.
8. Save data stores the run seed, content version, selected node path, current node, and recipe selections. It does not serialize textures.

Before entering an exit, the player sees:

- arena name and strong visual silhouette;
- one truthful risk/modifier hint;
- one reward-category hint.

Exact enemy waves, events, and recipe details remain hidden. This supports informed decisions without turning the route into a solved spreadsheet.

With eight intermediate arenas, an unrestricted ordered selection of three distinct arenas already has `8 x 7 x 6 = 336` routes. Three recipes at each visited intermediate raise that to `336 x 3^3 = 9,072` route-and-recipe combinations before encounter seeds, enemy rosters, rewards, and state timing. Graph-balancing rules will reduce the exact count but not the practical variety.

## Arena identity and emotional states

Each arena owns a stable identity: palette, silhouettes, landmark, movement language, audio motif, gameplay pressure, and reward bias. Recipes vary the arrangement without contradicting that identity.

Every visit progresses through four emotional states:

| State | Intended feeling | Typical changes |
| --- | --- | --- |
| Dormant | watchful, uncanny | slow background motion, sparse particles, clear navigation |
| Breathing | arena is awakening | stronger parallax, landmark motion, denser ambience, musical layer added |
| Hostile | arena fights the player | faster motion, threat accents, environmental events, sharper music |
| Rupture | reality is failing | portal/final objective, controlled distortion, peak landmark animation |

State transitions are driven by arena-local objective progress and encounter events, not by a decorative loop detached from gameplay. A default tuning curve may begin near 0%, 20%, 55%, and 85% progress, but each arena can override it.

The state controller emits parameters. It does not swap four complete texture sets:

- shader intensity, hue shift, emission, distortion, and parallax speed;
- particle emission and turbulence;
- landmark animation state;
- music stem and ambience mix;
- environmental-event permission;
- spawn-director pressure multiplier within a content-approved range.

This keeps the feeling dynamic without multiplying texture memory by four. Reduced-motion mode decreases camera/background movement and particle amplitude while preserving state timing and gameplay information.

The existing arena cycles are migrated into this model first. They are not deleted until each existing visual behavior has an equivalent state parameter.

## Data model

The current `ArenaId` enum and arena-specific branches are acceptable for three arenas but become a maintenance trap at ten or more. Migration is staged so save compatibility is preserved.

### Authored data

`ArenaDefinitionAsset` is a ScriptableObject with:

- stable string ID and content version;
- display name, description, risk hint, reward hint, and route tags;
- gameplay modifier data;
- exactly three `ArenaRecipeAsset` references;
- one `ArenaStateProfile`;
- audio and landmark references;
- three recipe package references;
- per-quality mip-streaming, particle, and shader settings;
- estimated texture-memory metadata produced by the baker.

`ArenaRecipeAsset` describes:

- the three 4K identity layers;
- the three 1440p support layers;
- optional named 1080p utility layers;
- materials and shader parameter ranges;
- particle and landmark configuration;
- deterministic recipe seed/salt.

The stable string ID becomes the long-term content identity. During migration, a bridge maps the three existing enum values to their stable IDs so old saves and tests keep working. New arena content does not require a growing chain of `switch` statements in `VoidFallGameRuntime`.

### Runtime components

Responsibilities are separated into small components:

- `RunGraphGenerator`: produces a deterministic graph; no Unity scene dependencies.
- `RunRouteState`: records current node, choices, seed, and progression.
- `ArenaCatalog`: resolves stable IDs to definitions.
- `ArenaResidencyManager`: owns Addressables handles and the resident-set policy.
- `ArenaStateController`: converts objective/encounter progress into emotional-state parameters.
- `ArenaRenderer`: displays the loaded recipe and applies state parameters.
- `ArenaBakePipeline` in an Editor-only assembly: produces and validates shipped assets.

`VoidFallGameRuntime` remains the integration point initially, but these responsibilities move out one at a time. This is not a full rewrite.

## Rendering and the Sakura problem

Sakura currently feels as if the arena is rendered on top of the player because a full-screen uGUI vignette overlays gameplay. The arena's color treatment therefore changes the apparent player, enemy, pickup, and text colors.

The structural fix is render-layer separation:

```text
Arena camera/layer    -> arena sprites, landmarks, arena-only particles/effects
Gameplay camera/layer -> player, enemies, pickups, projectiles
Screen-space UI       -> HUD and text
```

Arena grading, fog, bloom masks, and Sakura's vignette belong to the arena layer. Damage flashes and accessibility overlays may affect gameplay intentionally, but arena identity effects must not tint the player or HUD. URP camera stacking is used only where it provides this isolation; effects that can be expressed by arena materials stay on a single camera to avoid unnecessary render-target cost.

Acceptance is visual, not subjective wording: capture the same player/enemy/HUD test scene in Abyss, Sakura, and Red Nebula. Their reference colors must stay within an agreed tolerance while only the arena changes.

## Asset loading and release

Use Addressables 2.7 for arena packages only. Existing UI, audio, and gameplay resources are not migrated merely for consistency.

Addressables is justified here because arena residency is dynamic and explicit. Every load has one stored operation handle and every completed/failed route change has a matching release path. The residency manager is the only class allowed to own those handles.

Each arena recipe has one independently loadable package root containing only that recipe's references. Loading Sakura recipe 2 must not pull Sakura recipes 1 and 3 into memory. PC quality presets select mip levels and effect settings from the same package; they do not ship three duplicate texture sets. Shared materials, shaders, and truly shared textures are placed in a deliberate common dependency group. Addressables Build Layout reports are checked for accidental duplication before a catalog build is accepted. The exact bundle packing mode is selected from those reports rather than assuming that more or fewer bundles is automatically better.

### Lifecycle

```text
Before choice:
  resident = Current + Exit A + Exit B

Player chooses A:
  release Exit B
  transition Current -> A

After transition:
  release old Current
  acquire A1 + A2

Steady state:
  three arena packages

Conservative transition peak:
  four arena packages while an asynchronous release/load overlaps
```

The C# garbage collector is not the loader. It cleans managed objects; it does not define when GPU textures should leave memory. Arena memory is controlled by Addressables handles, package references, and Unity's asset/bundle lifecycle.

When the main menu becomes interactive, a new run gets a pending seed. That seed determines the exact first two exits, allowing the menu to preload the fixed Abyss package and those exits asynchronously. Continuing a saved run uses its saved seed and node instead. Because all large procedural work was already baked, this is disk I/O and asset upload rather than runtime image generation. Loading is time-sliced and measured; it must not turn the visible menu into a ten-FPS loading screen. If the player starts unusually quickly, an intentional short portal transition waits for the three required handles rather than exposing a frozen menu.

On load failure, the manager tries another valid recipe for the same arena. If that also fails, it loads a tiny built-in fallback arena and logs the exact missing address. It never runs the procedural baker in a player build.

## Texture and GPU-memory budget

The estimates below assume BC7 at 8 bits per pixel plus a full mip chain, rounded to MiB. They are planning estimates, not profiler measurements.

| Layer type | Count | Approx. each | Subtotal |
| --- | ---: | ---: | ---: |
| 4K, 3840x2160 | 3 | 10.55 MiB | 31.65 MiB |
| 1440p, 2560x1440 | 3 | 4.69 MiB | 14.07 MiB |
| Optional 1080p, 1920x1080 | 0-4 | 2.64 MiB | 0-10.56 MiB |
| **One maximum arena package** |  |  | **56.28 MiB** |

Therefore:

- normal three-package residency: about 169 MiB of arena textures;
- conservative four-package transition peak: about 225 MiB;
- arena-texture budget: 256 MiB peak.

This estimate excludes render targets, frame buffers, geometry, particles, shared gameplay atlases, UI, fonts, shaders, driver allocation, and bundle metadata. The whole-game engineering target is less than 1 GiB of measured GPU allocation on the PC High preset. We will not claim that target is achieved until the Memory Profiler and target-hardware build prove it.

Ten catalog arenas do not mean ten arena packages in VRAM. The other seven stay on disk. Adding arenas primarily increases build/install size; active VRAM follows the resident graph neighborhood.

The disk cost is still real. Thirty maximum-size recipe texture sets (ten arenas x three recipes) have about 1.65 GiB of GPU-format payload before file compression and asset sharing. Actual installed size can be lower or higher depending on the texture compressor, bundle compression, and duplicated dependencies, so the Addressables Build Layout report is the source of truth. We avoid tripling that cost again for PC presets by shipping one mipmapped texture set per recipe.

Enemies and projectiles also do not receive a private copy of their texture for every instance. Their sprite atlas is normally shared. Large enemy counts primarily threaten CPU simulation, physics queries, draw calls, transparency overdraw, particle cost, and garbage allocations. They will use object pools and measured budgets independent of arena-texture residency.

## Quality presets

Art is authored from a 4K-or-better master. The PC build ships one mipmapped texture set per recipe. Unity's mipmap-streaming system loads only the mip levels needed by the camera and quality budget; presets also cap particle and shader cost. Platform import overrides produce physically smaller variants for mobile or other constrained targets.

| Preset | Arena policy | Intended use |
| --- | --- | --- |
| Low | aggressive mip reduction, tight streaming budget, fewer particles and weaker distortion | minimum-spec PC |
| Medium | camera-selected mips with a moderate streaming budget | default for most 1080p/1440p players |
| High | permits top mips for the three 4K identity layers; full authored effects within measured limits | strong PC GPUs |

Texture resolution is not the same thing as monitor resolution. A 1080p player can still benefit from a 4K source through cleaner rotation, zoom, parallax, and supersampled detail, but every layer does not deserve 4K. High-frequency identity art does; fog masks and soft gradients generally do not.

No 8K runtime arena textures ship in the first version. They quadruple the pixels of 4K and spend memory/bandwidth where screen resolution, motion, particles, and top-down camera distance hide most of the gain.

Mobile remains the same gameplay project unless profiling proves otherwise. Unity platform import overrides can produce smaller ASTC-compressed variants and lower particle/shader presets without forking game logic into a second repository.

## Offline bake pipeline

The Editor command produces arena packages before a build:

```text
Tools > VoidFall > Bake Arena Content
```

The pipeline:

1. reads an arena definition and its three recipes;
2. generates or imports source layers;
3. creates one mipmapped PC texture set and any required per-platform import variants;
4. configures compression, mipmap streaming/priority, wrap/filter mode, and non-readable player textures;
5. creates/updates prefabs and ScriptableObjects without changing stable GUIDs unnecessarily;
6. assigns arena packages to Addressables groups;
7. writes measured dimensions and estimated memory metadata;
8. produces a validation report and a 120-image review sheet for ten arenas x three recipes x four states.

Generated runtime files live under one owned tree:

```text
Assets/VoidFall/Generated/Arenas/
```

Editor-only generator code lives under:

```text
Assets/VoidFall/Editor/ArenaBake/
```

The player assembly cannot reference the pixel-generation implementation. A build validation step fails if a required recipe package is missing, a texture exceeds its allowed resolution, a player texture is CPU-readable without an exemption, or the projected resident arena budget exceeds 256 MiB.

## Performance contract

Initial engineering target hardware is a GTX 1050-class 2 GiB GPU, a sixth-generation desktop i5-class CPU, 8 GiB system RAM, Windows, and an SSD. This is a validation target, not yet a published minimum specification.

Targets at 1920x1080 Low/Medium:

- 60 FPS target with CPU and GPU frame time each below 13 ms in representative combat, leaving margin under the 16.67 ms frame budget;
- no recurring managed allocations in the steady-state gameplay loop;
- no synchronous procedural texture generation in a player build;
- interactive main menu within three seconds after Unity hands control to game code on the current development machine;
- after the first visible menu frame, no frame over 50 ms and a 95th-percentile menu frame below 16.67 ms during background preloading;
- arena texture peak no greater than 256 MiB;
- whole-game measured GPU allocation below 1 GiB on a 2 GiB card.

The last two numbers must be measured in a standalone build. Editor measurements are useful diagnostics but are not acceptance evidence.

## Validation

### Deterministic tests

- identical seed/content version produces identical graph and recipes;
- sibling exits differ;
- no intermediate arena repeats along a route;
- every branch reaches Void Heart;
- save/load restores the same current node and future choices;
- old enum-based arena saves map to stable IDs.

### Residency tests

- at a choice point, steady state owns the current node and exactly two exits;
- Depth 3 and Void Heart do not keep imaginary extra exits resident;
- choosing an exit releases the rejected sibling;
- completing transition releases the previous arena;
- overlap never exceeds four packages;
- every successful or failed Addressables load has a matching release;
- a missing recipe package falls back to another recipe or the built-in fallback, never to runtime generation.

### Visual tests

- screenshot matrix covers all 120 arena/recipe/state combinations;
- player, enemy, pickup, and HUD reference colors remain stable across arenas;
- Sakura effects stay behind gameplay;
- Low/Medium/High comparisons justify visible cost differences;
- reduced-motion preserves gameplay cues.

### Player tests

- cold launch and warm launch timing;
- first ten seconds of interactive menu while preloading;
- transition between all current graph edges;
- Memory Profiler snapshots at menu, steady combat, choice, transition peak, and after release;
- worst-case enemy/projectile stress scenario at each quality preset;
- Addressables Build Layout review for duplicated dependencies.

## Implementation order

The safe sequence is:

1. **Remove runtime art generation from startup.** Build the Editor-only baker for the existing procedural gameplay sprites and current three arenas. Make the menu smooth before adding content.
2. **Introduce data assets with a compatibility bridge.** Add stable arena IDs, definitions, recipes, and state profiles while preserving current saves and `ArenaId` callers.
3. **Add Addressables only at the arena boundary.** Install the Unity-6-supported package, create independently loadable arena/recipe package roots plus a deliberate shared-dependency group, and implement the tested residency manager using the current three arenas.
4. **Fix rendering ownership.** Move Sakura and other arena-only overlays behind gameplay; establish the arena/gameplay/UI render boundary.
5. **Add the four-state controller.** Map existing cycles to state parameters and make gameplay animation intensity match the intended identity.
6. **Add the seeded graph and choice transition.** Replace automatic random time-based switching once saving, loading, and resident-set tests pass.
7. **Prove one vertical slice.** Abyss, Sakura, and a temporary Void Heart must pass startup, visual, memory, and stress budgets in a standalone build.
8. **Scale the catalog.** Only then author the remaining seven arenas through the proven pipeline.

This order prevents ten beautiful arenas from being built on an unproven loading and rendering system.

## Explicit non-goals for this work

- no 8K runtime arena catalog;
- no loading the entire ten-arena catalog into RAM/VRAM;
- no ECS rewrite;
- no migration of every existing resource to Addressables;
- no second mobile codebase now;
- no unrelated balance or enemy-AI rewrite;
- no deletion of compatibility code until old saves and the existing three arenas pass tests.

## Acceptance criteria for the architecture

The arena system is ready to scale when all of the following are true:

1. A player build contains no callable large procedural arena/sprite bake path.
2. The first visible menu remains smooth while initial graph assets preload.
3. At each choice point, the resident set follows current plus two exits and has a proven four-package maximum overlap.
4. The measured arena texture peak is at most 256 MiB.
5. Sakura and other arena effects cannot unintentionally tint the player or HUD.
6. Existing saves and current arena behavior remain valid through the compatibility bridge.
7. A seeded five-arena run survives save/load deterministically.
8. The three-arena vertical slice passes the standalone startup, memory, visual, and combat stress tests before catalog expansion.

## Sources used for technical decisions

- Unity 6 lists Addressables 2.7.6 as a released package and describes asynchronous loading with dependency management: <https://docs.unity3d.com/6000.0/Manual/com.unity.addressables.html>
- Unity's Addressables memory guidance describes reference-counted load/release ownership and notes that release does not guarantee immediate physical unloading: <https://docs.unity3d.com/Packages/com.unity.addressables@1.21/manual/MemoryManagement.html>
- Unity 6 mipmap streaming loads only the texture mip levels required by configured cameras and budgets: <https://docs.unity3d.com/6000.0/Documentation/Manual/TextureStreaming-use.html>
- Unity URP camera stacking: <https://docs.unity3d.com/6000.0/Documentation/Manual/urp/camera-stacking.html>
