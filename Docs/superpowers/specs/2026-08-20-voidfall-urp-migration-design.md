# Voidfall URP Migration Design

Date: 2026-08-20
Status: Approved
Owner: Migration lead

## Objective

Migrate `voidfall-unity` from Unity's Built-in Render Pipeline to the Universal Render Pipeline without changing gameplay, UI behavior, arena content, or the intended appearance of existing effects. Establish a rendering foundation that can later support higher-fidelity arenas, post-processing, and platform-specific quality profiles.

The deprecated browser project `voidfall.io` is out of scope.

## Current State

- Unity Editor: `6000.5.7f1`.
- Active pipeline: Built-in Render Pipeline.
- One saved build scene: `Assets/Scenes/SampleScene.unity`.
- The scene contains an orthographic Main Camera; gameplay and UI are created by code.
- No prefabs are present.
- No `Light2D` components, authored 2D-light materials, or active post-processing exist.
- Rendering uses `SpriteRenderer`, `LineRenderer`, `MeshRenderer`, one disabled `ParticleSystemRenderer`, runtime-created textures/sprites, and runtime-created materials.
- Four first-party legacy shaders exist: `AdditiveSprite`, `BlastWaveScreen`, `FilamentGas`, and `ParticleAdditive`.
- Runtime code depends on custom shader names plus Built-in names such as `Sprites/Default` and `Particles/Standard Unlit`.
- The project has substantial pre-existing tracked and untracked work. Migration work must not destroy or silently replace it.

## Selected Architecture

Use URP 17.5.x with a Universal Renderer Data asset. Unity Package Manager must resolve the Editor-compatible package for Unity `6000.5.7f1`; do not use `@latest` or an older manually selected URP line.

Use the Universal Renderer, not the 2D Renderer, for the initial migration. Voidfall currently mixes sprites, lines, dynamic meshes, transparent custom shaders, and framebuffer-sensitive blending but does not use 2D lights. The Universal Renderer minimizes simultaneous behavioral changes. A 2D Renderer can be evaluated later as an intentional lighting feature.

Keep Render Graph enabled. Do not enable URP compatibility mode as a new foundation.

The migration follows expand, migrate, verify, contract:

1. Expand: install URP and add inactive URP-compatible assets/shaders while Built-in remains usable.
2. Migrate: replace hidden runtime shader dependencies, activate the URP asset, and add the required camera data.
3. Verify: compile, build, run capture hooks, compare representative visuals, and measure runtime behavior.
4. Contract: remove temporary Built-in shader passes and obsolete compatibility code only in a separately approved later change.

## Package and Pipeline Assets

Add `com.unity.render-pipelines.universal` at the Unity-6.5-compatible `17.5.x` version resolved by Package Manager.

Create assets below `Assets/VoidFall/Rendering/URP/`:

- `VoidFallUniversalRenderer.asset`: `UniversalRendererData`.
- `VoidFallURP.asset`: `UniversalRenderPipelineAsset` using the renderer above.
- Unity's required URP Global Settings asset if Unity does not create it automatically.

Initially assign the same `VoidFallURP.asset` as the Graphics default and to all six existing quality levels. This produces one deterministic migration baseline. Desktop/mobile render-scale and feature variations are a later quality-profile task after visual parity is established.

Preserve:

- Linear color space.
- Orthographic camera projection, size, viewport, clip planes, and target backbuffer.
- Current transparency sorting and sorting orders.
- Camera HDR state unless evidence requires an explicit URP equivalent.
- Current MSAA behavior during parity validation.
- Existing screen-space overlay and screen-space camera canvas order.

Do not add Bloom, Volumes, renderer features, lights, camera stacking, or visual enhancements during the compatibility migration.

## Shader Migration

Manually rewrite the four custom shaders. The Render Pipeline Converter must not be trusted to convert them.

Each visible custom shader must include:

- A URP `SubShader` tagged `RenderPipeline=UniversalPipeline`.
- URP HLSL using `Core.hlsl`, `TransformObjectToHClip`, and URP texture/sampler macros.
- An unlit forward pass compatible with the Universal Renderer.
- Existing property names so serialized and runtime assignments remain valid.
- Existing queue, blend, culling, depth-write, color-mask, and output semantics.
- A temporary Built-in fallback `SubShader` during the expand/migrate phases.

Effect invariants:

- `AdditiveSprite`: preserve premultiplied color contribution and `Blend One One`.
- `BlastWaveScreen`: preserve `Blend OneMinusDstColor One`, `ColorMask RGB`, and destination-sensitive ordering.
- `FilamentGas`: preserve `_MaskTex`, `_Peak`, `_PassCount`, alpha reconstruction, and source-over blending.
- `ParticleAdditive`: either make it compile under URP or prove the disabled renderer path can be removed without changing compatibility probes. Removal is not part of the initial migration unless covered by a regression test.

Add a URP-compatible unlit vertex-color/texture shader or equivalent explicit material for runtime lines and dynamic meshes. Do not rely on `Sprites/Default` under URP.

## Material Ownership

Replace scattered runtime shader-name fallback construction with a small explicit render-material provider owned by the runtime rendering layer.

Requirements:

- Material assets live under `Assets/VoidFall/Resources/VoidFall/Materials/` so the current code-created bootstrap can load them without scene or prefab wiring.
- Default unlit, additive sprite, screen blend, and filament template materials are explicit assets.
- The existing `BlastWaveScreen.mat` GUID is preserved when practical; update its shader in place rather than deleting and recreating it.
- Shared immutable materials use `sharedMaterial`.
- Per-instance mutable filament properties use a cloned material or `MaterialPropertyBlock` with ownership and cleanup made explicit.
- Shader stripping cannot remove required shaders from player builds.
- No material is created per frame.
- Required materials fail with a clear diagnostic rather than silently falling through a chain of incompatible Built-in shader names.

## Camera and UI

Add or configure `UniversalAdditionalCameraData` on the Main Camera while preserving its existing behavior. Post-processing remains disabled.

Validate separately:

- World sprites, lines, and dynamic meshes.
- Screen-space overlay uGUI.
- The screen-space camera grain/legacy path, even if currently disabled.
- `ArenaTransitionGraphic` ordering.
- Remaining director-warning IMGUI compositing.

No UI redesign or IMGUI removal is included in this migration.

## Conversion Policy

The Render Pipeline Converter may be used only after the isolated baseline exists and only for a narrowly selected, inspected converter operation. Do not run a whole-project material conversion blindly. Custom shaders and runtime-created materials are always handled manually.

Every converter-produced diff must be inspected before it is accepted.

## Rollback

Migration executes in an ignored linked worktree on branch `migration/urp`. The original checkout remains the recovery source for all pre-existing tracked and untracked work.

Rollback is accomplished by discarding the isolated worktree or reverting the migration commits. Do not attempt to reverse an automatic asset conversion in place.

## Validation Gates

The migration is not complete until all applicable gates have fresh evidence:

1. Package gate: URP/Core packages resolve to the Unity-6.5-compatible `17.5.x` line without mixed versions.
2. Compile gate: Unity batch compilation exits successfully with no new C# or shader errors.
3. Asset gate: all required `.asset`, `.mat`, shader, and `.meta` files exist with no missing references.
4. Build gate: Windows 64 player build succeeds.
5. Launch gate: the player reaches menu/runtime and exits through existing automation hooks.
6. Visual gate: no pink or missing rendering; menu, settings, workshop, records, and gameplay captures are identical or have explicitly documented pipeline-only differences at 1280x720 and 1920x1080.
7. Effect gate: additive projectiles/particles/trails, blast waves, filament masking, transparent sorting, transitions, uGUI, and IMGUI are inspected.
8. Runtime gate: capture hooks, fixture loading, input, save/load, and stress benchmark still run.
9. Performance gate: time-to-interactive, frame EMA, managed memory, reserved memory, and build size are compared with the existing baseline. A regression must be reported, not hidden.
10. Rollback gate: the original checkout remains recoverable and its pre-migration state is not overwritten.

## Explicit Non-Goals

- Arena beautification or 1440p asset production.
- Bloom, color grading, tone mapping, distortion, or other post-processing.
- Adopting URP 2D lights.
- Replacing runtime procedural content with prefabs.
- UI redesign or TMP migration.
- Fixing unrelated startup, gameplay, save, or content-catalogue issues.
- Removing temporary Built-in compatibility before URP parity is approved.

## Acceptance Result

The accepted result is a Windows-buildable Unity project whose active pipeline is URP Universal Renderer, whose existing visuals and behavior remain intact, whose rendering dependencies are explicit, and whose migration can be reviewed or rolled back without losing the user's pre-existing work.
