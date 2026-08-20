# Voidfall URP Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make URP 17.5.x with the Universal Renderer Voidfall's active render pipeline while preserving the current Windows build, runtime behavior, transparent effects, and recoverable pre-migration state.

**Architecture:** Install URP while Built-in remains active, add dual-pipeline shaders and explicit resource-backed materials, then create and activate one deterministic URP asset across Graphics and all quality levels. Validate the actual Unity assets, player build, automated captures, and benchmarks before treating the migration as complete; keep Built-in fallback passes for a later contract phase.

**Tech Stack:** Unity `6000.5.7f1`, URP `17.5.x`, Universal Renderer, ShaderLab/HLSL, C#, Unity Test Framework, PowerShell, Windows 64 player.

## Global Constraints

- Work only in `C:/Users/anasb/Desktop/voidfall/voidfall-unity/.worktrees/urp-migration` on branch `migration/urp`.
- Preserve all tracked and untracked user work copied into the isolated worktree; stage only files named by the current task.
- `voidfall.io` is out of scope.
- Use the Universal Renderer, not the 2D Renderer.
- Keep Render Graph enabled; do not enable compatibility mode.
- Do not add Bloom, Volumes, lights, renderer features, post-processing, arena art, or UI redesign.
- Preserve linear color, orthographic camera behavior, sorting order, blend equations, material property names, and gameplay behavior.
- Do not run whole-project automatic material conversion.
- Keep temporary Built-in shader fallback subshaders until a separately approved contract phase.
- Every Unity/package/settings mutation must be followed by fresh compilation and targeted diff inspection.
- Never accept a subagent completion report without controller diff inspection and independent validation.

---

### Task 1: Resolve the Editor-Matched URP Package

**Files:**
- Modify: `Packages/manifest.json`
- Modify after Unity resolves it: `Packages/packages-lock.json`

**Interfaces:**
- Consumes: Unity Editor `6000.5.7f1` and the current package graph.
- Produces: `com.unity.render-pipelines.universal` and matching Core/ShaderGraph package dependencies on the `17.5.x` line for later shader and editor code.

- [ ] **Step 1: Record the package precondition**

Run:

```powershell
$manifest = Get-Content -Raw Packages/manifest.json | ConvertFrom-Json
if ($manifest.dependencies.'com.unity.render-pipelines.universal') { throw 'URP already present' }
```

Expected: exit `0`, proving the copied baseline is still Built-in-only.

- [ ] **Step 2: Add the exact Unity-6.5 URP dependency**

Use a targeted JSON edit that adds:

```json
"com.unity.render-pipelines.universal": "17.5.0"
```

Do not reorder or modify unrelated direct dependencies.

- [ ] **Step 3: Let Unity Package Manager resolve the graph**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.7f1\Editor\Unity.exe' `
  -batchmode -nographics -quit `
  -projectPath 'C:\Users\anasb\Desktop\voidfall\voidfall-unity\.worktrees\urp-migration' `
  -logFile 'urp-package-import.log'
```

Expected: Unity exits `0`, `Packages/packages-lock.json` contains URP `17.5.0`, and the log contains no package-resolution or compilation error.

- [ ] **Step 4: Verify the resolved package family**

Run:

```powershell
$lock = Get-Content -Raw Packages/packages-lock.json | ConvertFrom-Json
$urp = $lock.dependencies.'com.unity.render-pipelines.universal'.version
$core = $lock.dependencies.'com.unity.render-pipelines.core'.version
if ($urp -notlike '17.5.*') { throw "Unexpected URP $urp" }
if ($core -notlike '17.5.*') { throw "Unexpected Core $core" }
```

Expected: exit `0`, with URP and Core both on `17.5.x`.

- [ ] **Step 5: Inspect and commit only package changes**

Run:

```powershell
git diff -- Packages/manifest.json Packages/packages-lock.json
git add Packages/manifest.json Packages/packages-lock.json
git commit -m "build: add URP 17.5"
```

### Task 2: Add Dual-Pipeline Voidfall Shaders

**Files:**
- Create: `Assets/VoidFall/Resources/VoidFall/DefaultUnlit.shader`
- Create: `Assets/VoidFall/Resources/VoidFall/DefaultUnlit.shader.meta`
- Modify: `Assets/VoidFall/Resources/VoidFall/AdditiveSprite.shader`
- Modify: `Assets/VoidFall/Resources/VoidFall/BlastWaveScreen.shader`
- Modify: `Assets/VoidFall/Resources/VoidFall/FilamentGas.shader`
- Modify: `Assets/VoidFall/Resources/VoidFall/ParticleAdditive.shader`

**Interfaces:**
- Consumes: URP `Core.hlsl`, existing shader property names, runtime vertex color/UV streams, and current blend semantics.
- Produces: shaders named `VoidFall/DefaultUnlit`, `VoidFall/AdditiveSprite`, `VoidFall/ScreenBlend`, `VoidFall/FilamentGas`, and `VoidFall/ParticleAdditive`, each renderable by Universal Renderer with a temporary Built-in fallback.

- [ ] **Step 1: Add the URP shader structure without changing properties**

For every shader, place the URP `SubShader` first and retain a Built-in `SubShader` second. The URP subshader must use:

```hlsl
Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
HLSLPROGRAM
#pragma vertex Vert
#pragma fragment Frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
```

Use `TransformObjectToHClip(input.positionOS.xyz)`, `TEXTURE2D`, `SAMPLER`, and `SAMPLE_TEXTURE2D`. Give the forward pass `Tags { "LightMode"="UniversalForward" }`. Keep `ZWrite Off` and `Cull Off`.

- [ ] **Step 2: Implement the standard unlit shader**

`VoidFall/DefaultUnlit` accepts `_MainTex` and `_Color`, multiplies sampled RGBA by vertex color and `_Color`, and outputs conventional source alpha using:

```shaderlab
Blend SrcAlpha OneMinusSrcAlpha
```

It must work for `SpriteRenderer`, `LineRenderer`, and dynamic `MeshRenderer` geometry.

- [ ] **Step 3: Preserve each effect equation exactly**

Implement these fragment outputs and states in both pipelines:

```hlsl
// AdditiveSprite
half alpha = sample.a * input.color.a;
return half4(sample.rgb * input.color.rgb * alpha, alpha);

// ScreenBlend
half4 result = sample * input.color;
result.rgb *= result.a;
return half4(result.rgb, 1.0h);

// FilamentGas
half remainingCoverage = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv).a;
half target = saturate(_Peak * remainingCoverage);
half passAlpha = 1.0h - pow(max(0.0h, 1.0h - target), 1.0h / max(1.0h, _PassCount));
half alpha = saturate(input.color.a * passAlpha);
return half4(input.color.rgb, alpha);
```

`AdditiveSprite` and `ParticleAdditive` keep `Blend One One`; `ScreenBlend` keeps `Blend OneMinusDstColor One` and `ColorMask RGB`; `FilamentGas` keeps `Blend SrcAlpha OneMinusSrcAlpha`.

- [ ] **Step 4: Compile shaders under Unity**

Run the Unity batch command from Task 1 with log file `urp-shader-compile.log`.

Expected: Unity exits `0`; the log contains no `Shader error`, `failed to compile`, missing include, or C# compilation error.

- [ ] **Step 5: Inspect and commit only shader changes**

Run:

```powershell
git diff -- Assets/VoidFall/Resources/VoidFall
git add Assets/VoidFall/Resources/VoidFall/*.shader Assets/VoidFall/Resources/VoidFall/*.shader.meta
git commit -m "feat: add URP-compatible shaders"
```

### Task 3: Replace Hidden Shader Lookups with Explicit Materials

**Files:**
- Create: `Assets/VoidFall/Runtime/Rendering/VoidFallRenderMaterials.cs`
- Create: `Assets/VoidFall/Runtime/Rendering/VoidFallRenderMaterials.cs.meta`
- Create: `Assets/VoidFall/Editor/UrpMaterialAssetSetup.cs`
- Create: `Assets/VoidFall/Editor/UrpMaterialAssetSetup.cs.meta`
- Create: `Assets/VoidFall/Tests/Editor/VoidFall.URP.Tests.Editor.asmdef`
- Create: `Assets/VoidFall/Tests/Editor/VoidFall.URP.Tests.Editor.asmdef.meta`
- Create: `Assets/VoidFall/Tests/Editor/UrpMigrationTests.cs`
- Create: `Assets/VoidFall/Tests/Editor/UrpMigrationTests.cs.meta`
- Create through Unity: `Assets/VoidFall/Resources/VoidFall/Materials/DefaultUnlit.mat`
- Create through Unity: `Assets/VoidFall/Resources/VoidFall/Materials/AdditiveSprite.mat`
- Create through Unity: `Assets/VoidFall/Resources/VoidFall/Materials/FilamentGas.mat`
- Modify in place: `Assets/VoidFall/Resources/VoidFall/BlastWaveScreen.mat`
- Modify: `Assets/VoidFall/Runtime/Gameplay/VoidFallGameRuntime.cs`

**Interfaces:**
- Consumes: resource paths `VoidFall/Materials/DefaultUnlit`, `VoidFall/Materials/AdditiveSprite`, `VoidFall/Materials/FilamentGas`, and `VoidFall/BlastWaveScreen`.
- Produces: `VoidFallRenderMaterials.DefaultUnlit`, `.AdditiveSprite`, `.ScreenBlend`, and `CreateFilamentInstance()`; no visible runtime path depends on Built-in shader-name fallbacks.

- [ ] **Step 1: Write EditMode tests for observable material loading**

Create an Editor test assembly and tests if no suitable test assembly exists. Tests must load the actual resources and assert:

```csharp
Assert.That(VoidFallRenderMaterials.DefaultUnlit.shader.name, Is.EqualTo("VoidFall/DefaultUnlit"));
Assert.That(VoidFallRenderMaterials.AdditiveSprite.shader.name, Is.EqualTo("VoidFall/AdditiveSprite"));
Assert.That(VoidFallRenderMaterials.ScreenBlend.shader.name, Is.EqualTo("VoidFall/ScreenBlend"));
var filament = VoidFallRenderMaterials.CreateFilamentInstance();
try
{
    Assert.That(filament.shader.name, Is.EqualTo("VoidFall/FilamentGas"));
    Assert.That(filament, Is.Not.SameAs(VoidFallRenderMaterials.FilamentTemplate));
}
finally
{
    UnityEngine.Object.DestroyImmediate(filament);
}
```

Run the targeted EditMode tests before creating the provider/assets. Expected: compilation or test failure because the required API/assets do not exist.

- [ ] **Step 2: Implement the material provider**

Implement one cached required-resource loader. A missing required asset throws `InvalidOperationException` containing its resource path. `CreateFilamentInstance()` returns `new Material(FilamentTemplate)`; no accessor creates materials repeatedly except that explicit owned-instance factory.

- [ ] **Step 3: Create material assets through an idempotent Editor method**

Implement:

```csharp
namespace VoidFall.Editor
{
    public static class UrpMaterialAssetSetup
    {
        public static void Configure();
    }
}
```

`Configure()` creates missing folders/assets, updates existing assets in place, assigns the exact shader names above, calls `EditorUtility.SetDirty`, `AssetDatabase.SaveAssets`, and exits nonzero by throwing if a shader cannot be found. Preserve the existing `BlastWaveScreen.mat` asset and GUID.

Execute:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.7f1\Editor\Unity.exe' `
  -batchmode -nographics -quit `
  -projectPath 'C:\Users\anasb\Desktop\voidfall\voidfall-unity\.worktrees\urp-migration' `
  -executeMethod VoidFall.Editor.UrpMaterialAssetSetup.Configure `
  -logFile 'urp-material-setup.log'
```

- [ ] **Step 4: Integrate runtime call sites narrowly**

Replace material creation in `SetupFx`, `GetBlastWaveScreenMaterial`, `GetDefaultSpriteMaterial`, `GetAdditiveSpriteMaterial`, `EnsureRailTrailView`, `EnsureArcView`, `EnsureArcCoreView`, `CreateMeshView`, `CreateFilamentMeshView`, and `CreateLineView` with the provider. Preserve existing public APIs, sorting orders, textures, colors, and ownership. Ensure owned filament materials are destroyed by the existing teardown path or add focused teardown there.

- [ ] **Step 5: Run red-to-green EditMode tests and compilation**

Run the targeted tests and then the Unity batch compile.

Expected: all new EditMode tests pass; Unity exits `0`; logs contain no missing material, missing shader, or compilation error.

- [ ] **Step 6: Inspect and commit only material/runtime/test changes**

Commit with:

```powershell
git commit -m "refactor: make render materials explicit"
```

### Task 4: Create and Activate the Universal Renderer

**Files:**
- Create: `Assets/VoidFall/Editor/UrpPipelineSetup.cs`
- Create: `Assets/VoidFall/Editor/UrpPipelineSetup.cs.meta`
- Create through Unity: `Assets/VoidFall/Rendering/URP/VoidFallUniversalRenderer.asset`
- Create through Unity: `Assets/VoidFall/Rendering/URP/VoidFallURP.asset`
- Create through Unity if required: `Assets/VoidFall/Rendering/URP/VoidFallURPGlobalSettings.asset`
- Modify: `ProjectSettings/GraphicsSettings.asset`
- Modify: `ProjectSettings/QualitySettings.asset`
- Modify: `Assets/Scenes/SampleScene.unity`
- Test: the Editor test assembly created in Task 3

**Interfaces:**
- Consumes: `UniversalRendererData`, `UniversalRenderPipelineAsset`, and the URP-compatible materials/shaders from Tasks 2-3.
- Produces: active URP in Graphics and every quality level; Main Camera with `UniversalAdditionalCameraData`, post-processing off, renderer index `0`.

- [ ] **Step 1: Write failing configuration tests**

Add tests that assert:

```csharp
Assert.That(GraphicsSettings.defaultRenderPipeline, Is.TypeOf<UniversalRenderPipelineAsset>());
Assert.That(GraphicsSettings.defaultRenderPipeline.name, Is.EqualTo("VoidFallURP"));
```

Iterate through all quality levels, restore the original level in `finally`, and assert `QualitySettings.renderPipeline` references the same asset. Open `Assets/Scenes/SampleScene.unity`, find the Main Camera, assert `UniversalAdditionalCameraData` exists, `renderPostProcessing` is false, and `additionalData.scriptableRenderer` is the same instance as `pipeline.GetRenderer(0)`.

Run the targeted tests before configuration. Expected: failures because URP is inactive and the camera data does not exist.

- [ ] **Step 2: Implement an idempotent pipeline setup method**

Implement:

```csharp
namespace VoidFall.Editor
{
    public static class UrpPipelineSetup
    {
        public static void Configure();
    }
}
```

Use the installed URP 17.5 API discovered from `Library/PackageCache` to create or reuse one `UniversalRendererData` and one `UniversalRenderPipelineAsset`. Assign renderer index `0`, keep compatibility mode disabled, assign `GraphicsSettings.defaultRenderPipeline`, assign every quality level explicitly, open only `SampleScene.unity`, add/configure `UniversalAdditionalCameraData`, and save only when changed. Create/associate Global Settings using the supported URP API when Unity has not already done so.

- [ ] **Step 3: Execute configuration and inspect serialized diffs**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.7f1\Editor\Unity.exe' `
  -batchmode -nographics -quit `
  -projectPath 'C:\Users\anasb\Desktop\voidfall\voidfall-unity\.worktrees\urp-migration' `
  -executeMethod VoidFall.Editor.UrpPipelineSetup.Configure `
  -logFile 'urp-pipeline-setup.log'
```

Inspect every changed `.asset`, `.meta`, `.unity`, and `ProjectSettings` file. Reject unrelated serialization churn.

- [ ] **Step 4: Run configuration tests and URP compilation**

Expected: all targeted EditMode tests pass; batch compilation exits `0`; no shader errors, missing renderer, missing global-settings, or compatibility-mode warning appears.

- [ ] **Step 5: Commit the pipeline activation atomically**

Commit only the setup script, generated URP assets and metadata, scene, Graphics settings, Quality settings, and updated tests:

```powershell
git commit -m "feat: activate URP Universal Renderer"
```

### Task 5: Prove Player, Visual, and Runtime Parity

**Files:**
- Create: `Docs/AI/URPMigrationValidation.md`
- Modify only if a verified migration defect requires it: files already owned by Tasks 2-4

**Interfaces:**
- Consumes: active URP project, existing `BuildScript`, visual capture hooks, stress benchmark hooks, pre-migration captures/logs.
- Produces: evidence-backed migration result and documented residual differences/risks.

- [ ] **Step 1: Run the complete EditMode suite**

Run Unity Test Framework in batch mode and write XML under `TestResults/urp-editmode.xml`.

Expected: exit `0`, zero failed tests, and the URP migration tests are present rather than a zero-test result.

- [ ] **Step 2: Build the Windows 64 player**

Invoke the repository `BuildScript` through Unity batch mode. Override or fix its output argument narrowly if its current hard-coded path prevents an isolated build; do not commit unrelated build-script changes.

Expected: `BuildPipeline.BuildPlayer` succeeds and the player executable exists.

- [ ] **Step 3: Launch automated captures**

Run the existing capture hooks for menu, settings, workshop, records, and gameplay at `1280x720` and `1920x1080`. Store migration evidence outside tracked assets.

Inspect images for pink/missing objects, incorrect alpha, changed additive intensity, filament mask errors, blast-wave blend errors, sorting changes, transitions, uGUI, and IMGUI warnings.

- [ ] **Step 4: Run the existing stress benchmark**

Use a fixed scenario/seed and the same warmup/measure durations as the pre-migration evidence. Record time-to-interactive, frame EMA, managed memory, reserved memory, object/material counts, and build size. Do not claim improvement; report deltas.

- [ ] **Step 5: Write the validation report**

`Docs/AI/URPMigrationValidation.md` must include:

- Exact Unity/URP versions.
- Commands and exit codes.
- Test counts.
- Build output path and size.
- Capture paths/resolutions and visual observations.
- Benchmark baseline/current/delta.
- Known warnings separated into pre-existing and migration-introduced.
- Rollback branch/worktree/commit instructions.
- Explicit statement that arena beautification and post-processing remain deferred.

- [ ] **Step 6: Run final diff and whole-branch review**

Review all migration commits against the design spec. Any Critical/Important finding receives one fix wave followed by a scoped re-review. Re-run every gate affected by a fix.

- [ ] **Step 7: Commit validation documentation**

```powershell
git add Docs/AI/URPMigrationValidation.md
git commit -m "docs: record URP migration validation"
```
