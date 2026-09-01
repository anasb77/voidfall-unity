# VoidFall arena architecture

Last updated: 2026-09-01.

## Current model

Arena art is generated or imported in the Unity Editor and saved as regular
assets. Player builds never paint full-screen arena textures at runtime.

Each prepared arena currently contains:

- one 3840x2160 identity plate;
- one 2560x1440 detail plate;
- three lightweight deterministic composition recipes;
- runtime particles, landmarks, parallax and emotional-state animation.

The three recipes share the expensive textures and vary mirroring, framing,
detail offset and deterministic decoration seeds. Recipe variety therefore does
not triple texture memory.

## Loading

`ArenaResidencyManager` is the only Addressables handle owner. It reconciles a
bounded resident set and releases obsolete handles. The hard cap is four arena
package roots during overlap. Failed handles remain observable through status
and `LastFailure`; a complete alternative-recipe fallback remains future work.

Current Addressables labels and bundles are grouped per arena identity:

- `vf-arena-abyss`
- `vf-arena-red-nebula`
- `vf-arena-white-sakura`

## Prepared catalogue

| Arena | Stable route identity | Status |
| --- | --- | --- |
| Abyss | `abyss` | prepared, three recipes |
| Red Nebula | `red-nebula` | prepared, three recipes |
| White Sakura | `white-sakura` | prepared, three recipes |
| Hydra | `hydra` | next visual package; currently reuses Abyss |
| Monochrome Court | `monochrome-court` | next visual package |
| Lost City | `null-city` | next visual package; stable ID retained |
| Dead Orbit | `dead-orbit` | graph/data only |
| Graveyard | `graveyard` | graph/data only |
| Last Gate | `last-gate` | graph/data only |
| Final Void | `final-void` | graph/data only |

Prepared arenas appear in the main-menu carousel through `ContentOrder.Arenas`.
Adding a map requires extending the stable ID bridge, bake specifications,
addressable migration/validation and render identity rules together.

## Route relationship

Every successful run begins in Abyss. Completing a Void marks the current route
node complete, reveals its children and opens the route-select screen. Route
identity and arena identity are related but not interchangeable: placeholder
Voids may temporarily reuse a prepared visual arena while keeping their own
objective and future content ID.

## Rendering layers

Arena plates and arena-only effects render behind actors. Player, enemies,
projectiles and pickups remain color-stable across arenas. HUD and text render
in screen-space UI above both. The music perimeter is decorative and always
below danger indicators.

## Quality and memory

PC ships one mipmapped texture set. Low/Medium/High control mip limits,
particles and shader cost rather than duplicating assets. Current arena bundles
are approximately 1.6-2.3 MiB on disk after bundle compression; runtime GPU
cost must be measured from imported formats and resident mips, not PNG size.

Art is authored from 4K masters where identity detail benefits. Soft masks,
fog and utility layers should remain 1440p or 1080p. No 8K runtime textures are
planned.

## Bake and validation

Editor tooling under `Assets/VoidFall/Editor`:

- bakes plates and procedural sprites;
- applies non-readable, mip-streamed import settings;
- creates three recipe assets;
- registers Addressables entries;
- validates required packages before player builds.

Required validation for a new arena:

1. Bake completes and all three recipes validate.
2. Addressables content builds without duplicate dependencies.
3. Menu carousel can display the arena.
4. Three deterministic recipes produce distinct compositions.
5. Player/enemy/HUD colors remain stable.
6. Low, Medium and High remain readable at 720p, 1080p and 1440p.
7. Standalone memory and frame-time measurements stay inside project budgets.
