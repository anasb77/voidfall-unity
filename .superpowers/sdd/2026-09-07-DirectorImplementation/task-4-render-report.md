# Task 4: isolated incident rules and render preparation

Prepared only the assigned new files; parent owns runtime admission, enemies, forces, diagnostics, native rendering and integration. No Unity launches or pipeline/package changes by this worker.

## Core API and semantics

`VoidFall.Core.MajorIncidentKind`: `None`, `BlackHole`, `DestroyerRaid`, `Eclipse`.
`MajorIncidentPhase`: `None`, `Warning`, `Active`, `Release`.

`MajorIncidentState`: read-only `Kind`, `Phase`, `Elapsed`, `Remaining`, `Strength`; methods `Begin(kind)`, `Step(double dt)`, `Reset()`.

- Elapsed/Remaining are **whole-incident** seconds, not phase-local clocks. Completion resets every property to zero/None. Begin resets any previous event; None/invalid kinds leave a clean state.
- Warning is 2.5 seconds. Active is 10 seconds for BlackHole, 32 for DestroyerRaid, 19.5 for Eclipse. Release is respectively 1.5, 3, 2 seconds. Total lifetimes: 14, 37.5, 24 seconds.
- Strength is zero during warning, smoothstep 0→1 over the first 1.5 Active seconds for every kind, holds 1, then smoothstep 1→0 over Release. Activation is included in Active duration. Parent approved these semantics during preparation.
- A positive finite Step crosses phase boundaries using total time; dt >= Remaining resets cleanly before addition can overflow. Zero, negative, NaN and infinity steps do nothing.
- `MajorIncidentRules.IsArenaEligible(string)` is a case-sensitive stable-ID allowlist containing `abyss` and `void` only.
- `CanBegin(kind, survivalSecondsRemaining, bossActive, safeState, otherIncidentActive)` rejects exclusions, unknown kinds, and nonfinite remaining time; requires full lifetime plus 15 seconds boss lead. Parent retains minimum 240-second spacing, RNG/seed and encounter admission.
- `BlackHolePullScale(double distance, double radius, double strength)` returns `4*u*(1-u)*clamp01(strength)` within the radius, zero at center/outside/invalid input. This is a scale, never a stored force. Parent applies inward baselineSpeed/3 for the player and 3x for ordinary enemies, with elite/boss exclusions.
- Public duration helpers: `ActiveDuration(kind)`, `ReleaseDuration(kind)`, `TotalDuration(kind)`; constants `WarningSeconds`, `ActivationSeconds`, `BossLeadSeconds`.

## Presentation API

Namespace `VoidFall.Runtime`, sealed ordinary class `DirectorIncidentPresentation : IDisposable`:

```csharp
DirectorIncidentPresentation(Transform worldRoot, Camera camera);
void Render(MajorIncidentKind kind, MajorIncidentPhase phase, Vector2 center,
    float radius, float strength, bool reducedEffects, bool highContrast,
    Texture backdrop = null, Vector4 backdropWorldRect = default);
float EnvironmentExposure { get; }
void Hide();
void Dispose();
```

Both constructor arguments must be non-null. Camera is accepted to match the proposed ownership API; coordinates never derive from its position. The caller supplies the stationary event center every frame.

- One lazy MeshRenderer/quad, material and property block; static property IDs; no managed allocations during normal Render/Hide calls. Only BlackHole creates resources (including its Warning phase). Missing/unsupported shader logs once and leaves the view absent.
- `Resources.Load<Shader>("VoidFall/DirectorBlackHole")` ensures a scoped resource shader; sorting order -80 on the default sorting layer, same GameObject layer as worldRoot. Existing backdrop -110, details -106, grid -95 and vignette -90 lie beneath it. Parent must verify telegraphs, bodies, pickups and shots remain above it in real captures.
- Assumes an XY world with unrotated, nonzero-scaled worldRoot; cancels its X/Y scale for world-unit radius. Quad is camera-independent, world-positioned at center and worldRoot Z. Radius marks the visible warning/active boundary. The dark core is deliberately smaller than attraction extent.
- BlackHole Warning draws a static ring and six-direction markers. Active BlackHole shows a dark core, restrained luminous rim, background-only lensing and faint radius boundary. No timers, combat RNG, collision, entity destruction or forces in this class/shader.
- DestroyerRaid draws no geometry here in any phase. Parent owns global warning toast, raid-entry telegraphs, approved black-and-white destroyer bodies and attacks.
- Eclipse draws no geometry; its warning toast belongs to parent. Its multiplier remains 1 during Warning, then sets EnvironmentExposure from 1 down to .16 at full strength (.32 in high contrast), restoring through Release. Parent must multiply only actual arena background/environment owners and restore their base exposure on Hide/reset. No screen overlay or camera grade is applied here. Eclipse exposure does not require a valid center/radius, texture or created view.
- Reduced effects disables refraction completely, preserving stationary core, rim and warning. High contrast uses a brighter blue-white rim and gentler Eclipse darkness. No shader time animation to leak motion while paused.
- Hide restores EnvironmentExposure=1, disables the renderer and clears its texture property block before a package release. Dispose does this and destroys owned mesh/material/GameObject; repeated Dispose is harmless. Never disposes the caller's texture.

## Background mapping and source adaptation

`backdropWorldRect = (minimumWorldX, minimumWorldY, worldWidth, worldHeight)`. Full texture UV (0,0) maps to the minimum corner. Caller updates this mapping when the native backdrop follows/parallaxes with the camera; **event center stays stationary**. Use a correctly oriented background-only capture for rotated, packed/atlas-subrect or multilayer backgrounds. This helper does not construct captures or infer packed sprite UVs. Missing texture/invalid rect safely leaves core/rim without lensing. RenderTexture callers must supply the agreed UV orientation.

Lensing samples only that texture. It cannot refract actors, bullets, pickups or HUD. A single texture does not reproduce separately rendered grid/gas/vignette layers; parent must assess whether a background-only capture is needed before approval.

Inspected all 109 objects and decoded edges of the actual supplied `BlackHole.shadergraph`, plus material values. Original data is retained under `Art/Director/BlackHoleSource/` as `.txt` files to avoid importing an unused legacy Lit graph or GUID dependencies:

- Shader graph SHA-256: `5CE83910877C6ADD1C24BE584DC2F4FCC2FBAB779270EE358B69AB36EB3D55BF`.
- Material SHA-256: `95314E7D4E69B95CD2B2889F910B582F0860F7944D575E7F00C92A17139B4B47`.

Source controls retain material defaults HoleSize=.65, transition Smoothness=5/100, DistortionStrength=.8, OuterRing=6, RadiusPower=.15. The adaptation synthesizes hemisphere NdotV from planar radial distance and preserves `pow(1-NdotV, power)`, the inverted smooth transition around HoleSize, and `pow(1-Fresnel, OuterRing)` distortion mask. Reversed source smoothstep edges are expressed with ascending edges. Source `UV + mask*(1-2*UV)` becomes `UV + 2*(centerUV-UV)*mask`, with bounded .12 visual gain and explicit event strength. Lit scene-color sampling is replaced with supplied background sampling; premultiplied composition adds the restrained rim/core and transparent exterior.

This is an explicit mathematical ShaderLab adaptation, **not** a drop-in-compatible imported Shader Graph. No opaque/depth texture enablement or renderer feature changes were made.

## Verification and remaining native gates

- Tests written first in `Tests/Editor/MajorIncidentRulesTests.cs`; a temporary API-only no-op scaffold produced 22 expected failures/22 default-case passes. Replacing that scaffold with actual production Core gave **44 passed, 0 failed** in a temporary .NET 10 harness invoking the retained NUnit 3.14 tests. The scaffold/harness are outside the repository and not shipped.
- Retained tests cover every kind's full lifecycle, smooth activation/release, large-step vs partitioned time, invalid/paused steps, reset/rebegin, allowlist, full-duration boss margin and admission exclusions, finite bounded radial pull with center/boundary/invalid cases.
- Separate compile of production Core + renderer against installed Unity 6000.5.7f1 `UnityEngine.CoreModule.dll`: **0 warnings, 0 errors**. This does not replace native Unity assembly compilation.
- Checked the installed URP17.5 Core include and its D3D11 macros: `TransformObjectToWorld`, `TransformWorldToHClip`, `TEXTURE2D`, `SAMPLER`, `SAMPLE_TEXTURE2D` exist. Shader follows existing project `UniversalForward`/transparent conventions. Native shader compile and GPU rendering remain parent gates.
- Parent still needs native EditMode/PlayMode, camera-follow/off-center screenshots, actual backdrop mapping/orientation, sorting/readability, reduced-effects/high-contrast, warning→active→release and safe-state/arena-swap cleanup checks. This report does not claim those gates passed.
- REPO_MAP update is intentionally delegated to parent to preserve this worker's exact owned-file scope; add rules/presentation ownership if integrated.
