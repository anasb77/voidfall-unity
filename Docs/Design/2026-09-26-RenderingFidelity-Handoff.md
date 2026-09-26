# Rendering fidelity handoff (September 26)

The owner requested this work. Its goal is to make VoidFall look as good as possible in screenshots and video for Twitter, Reddit and store pages. Every finding below was checked in the source. Paths are relative to `Assets/VoidFall/` unless they start with `Assets/`, `ProjectSettings/` or `Docs/`. Line numbers were taken on September 26 and may drift.

**Scope:** rendering, camera and texture-import settings only. No gameplay, simulation, RNG or content changes. The golden-master hash must not change. If it does, something outside scope was touched.

**Approval:** the owner must approve before/after captures before anything is promoted to `../Builds/VoidFall.exe`. Build into a staging folder with `VOIDFALL_BUILD_OUTPUT` (see AGENTS.md). Then promote the verified payload and remove the staging copy.

## Summary (ranked by visual impact)

| # | Problem | Effect in screenshots and video | Fix size |
|---|---|---|---|
| R1 | No anti-aliasing: the camera disables MSAA and post AA is off | Jagged, crawling edges on rings, lines, telegraphs, filaments and the Court board | Small |
| R2 | Dithering off on an 8-bit buffer | Banding in dark gradients (Abyss, glows, vignettes). Twitter and Reddit compression turns it into blocks | One line |
| R3 | HDR off, LDR grading, no tonemapping, bloom threshold 0.9 | The art direction's "luminous centres" barely glow. The image looks flat | Medium, needs tuning |
| R4 | Arena grade darkens everything (`postExposure -0.16`) | Duller image that pushes more pixels under the bloom threshold | One line, after R3 |
| R5 | No mipmaps on the 510 baked procedural sprites and on HydraPrime | Shimmer and aliasing on minified sprites in motion | Small, then a rebake |
| R6 | Screenshots captured at 1× with the HUD visible | Soft marketing stills | Small feature |
| R7 | The "balanced" quality mode turns vsync off | Tearing in recordings | One line |
| R8 | Procedural sprites are baked at about 1 px per world unit | Soft at 1440p and 4K | Larger, optional |

Correct already, leave alone:
- **Linear colour space:** `ProjectSettings/ProjectSettings.asset` L49.
- **Arena plate format:** BC7 at quality 100 with mips.
- **Resolution:** native, render scale 1, dynamic resolution off.
- **Canvases:** HUD and menus are `ScreenSpaceOverlay` (`UI/Core/UIBuilder.cs` L397, `Runtime/Gameplay/VoidFallGameRuntime.Hud.cs` L534), so post-processing doesn't touch them. That keeps the "arena effects must not tint the HUD" contract safe.

---

## R1: Anti-aliasing is effectively off

Evidence:
- `Assets/Scenes/SampleScene.unity` L194: `m_AllowMSAA: 0`. The camera opts out of MSAA, so the pipeline's MSAA never applies.
- The same file, L239: `m_Antialiasing: 0`, so there is no post AA (FXAA/SMAA).
- `Rendering/URP/VoidFallURP.asset` L28: `m_MSAA: 2`. This value is ignored because of the camera flag.
- No runtime code sets `allowMSAA` or `antialiasing`. `SetupCamera` is at `Runtime/Gameplay/VoidFallGameRuntime.cs` L5229.

Fix:
1. In `SetupCamera` (after `_camera = Camera.main`), set `_camera.allowMSAA = true`. Do it at runtime rather than in the scene: `Editor/UrpPipelineSetup.cs` `ConfigureSampleSceneCamera` (≈L259-296) rewrites scene camera data, and most wiring in this project is code-authored.
2. Set `m_MSAA: 4` in `VoidFallURP.asset`. Use the Inspector or a `SerializedObject` edit.
3. Don't enable SMAA/FXAA by default. They soften the thin 1–2 px neon outlines. Only try SMAA High in an A/B capture if MSAA 4× leaves visible aliasing.
4. MSAA smooths geometry edges: meshes, LineRenderers and Tight sprite-mesh edges. Texture edges inside sprites also need R5.

Watch for:
- **HDR:** MSAA 4× combined with HDR (R3) costs memory bandwidth. Measure frame time (see Verification).
- **Screen-grab passes:** any shader or renderer feature that samples the camera colour texture must still work. There are no renderer features today; the opaque texture is off.

## R2: Dithering is off

Evidence:
- `SampleScene.unity` L242: `m_Dithering: 0`.
- Nothing enables it at runtime.

Fix: in `SetupVideoVolume` (`Runtime/Gameplay/VoidFallGameRuntime.VideoSettings.cs` ≈L43-51), next to `additional.renderPostProcessing = true`, add `additional.dithering = true`.

This is the cheapest meaningful fix. Dithering only runs while post-processing is on, and post is always enabled there.

## R3: HDR, tonemapping and bloom that actually glows

Evidence:
- **`VoidFallURP.asset`:**
  - L26 `m_SupportsHDR: 0`
  - L27 `m_HDRColorBufferPrecision: 0`
  - L79 `m_ColorGradingMode: 0` (LDR)
  - L80 LUT size 32
- **`Rendering/URP/VoidFallDefaultVolumeProfile.asset`:**
  - Tonemapping block ≈L719-725: `mode: 0` (None).
  - Bloom block ≈L759-771: `threshold 0.9`, `intensity 1.2`, scatter 0.7, high-quality filtering on.
  - This profile is the registered default through `VoidFallURPGlobalSettings.asset`.
- **Runtime overrides:** a priority-10 global Volume (`VideoSettings.cs` L43-78) sets bloom intensity from `UI/Core/VideoSettingsRules.cs` `DefaultBloom = 1.2f` (L20). The other bloom parameters come from the profile.
- **Result:** with an 8-bit buffer nothing exceeds 1.0, so bloom only catches near-white pixels. Mid-saturation cores and projectiles contribute almost nothing.

Fix, applied as one change and tuned together with R4:
1. **Enable HDR and HDR grading.** Set `m_SupportsHDR: 1` and `m_ColorGradingMode: 1` (HighDynamicRange). Start with 32-bit precision (`m_HDRColorBufferPrecision: 0`, R11G11B10). If Abyss's dark navy gradients band or shift hue, switch to 64-bit (`1`) and re-measure.
2. **Add tonemapping.** Set it to **Neutral** (`mode: 1`) first, then capture an A/B against ACES (`mode: 2`). ACES adds contrast but shifts saturated hues (cyan toward blue-white, reds toward orange), and the art relies on exact neon hues. Neutral is the safer default.
3. **Retune bloom** to threshold **1.0** (with HDR, only real emissive values above 1 bloom), intensity **0.7–1.0** as a start, scatter 0.65–0.75, and keep high-quality filtering. Update `VideoSettingsRules.DefaultBloom` to match the new default so the settings slider keeps its meaning. Check `SaveSettings.MaxBloom`.
4. **Give emissives real HDR intensity.** Otherwise HDR plus tonemapping makes the image slightly *less* bright.
   - Sprite vertex colours are 8-bit and clamp at 1. The multiplier has to come from the material.
   - `Resources/VoidFall/AdditiveSprite.shader` multiplies vertex colour by the per-material `_Color` (L36-66), and `_Color` is a `half4`, so values above 1 pass through. `ParticleAdditive.shader` is similar; verify it.
   - Create boosted material instances (or a MaterialPropertyBlock on the few views that need it) with `_Color` around (2, 2, 2, 1) to (3, 3, 3, 1) for:
     - Zack's core, aura and ring
     - player projectiles and rail lines
     - hit sparks and burst particles (the `BurstFx` views)
     - explosions and blast rings
     - boss and elite charge VFX
   - Don't boost arena plates, filaments, UI or enemy bodies. Enemy *cores* only if they look flat after tonemapping.
   - `Runtime/Rendering/VoidFallRenderMaterials.cs` owns shared materials. Add the boosted variants there, and keep material ownership and cleanup rules.

Watch for:
- **Blast waves:** `Resources/VoidFall/BlastWaveScreen.shader` ("VoidFall/ScreenBlend") uses `Blend OneMinusDstColor One`. With an HDR target, a destination above 1 makes `OneMinusDstColor` negative, which darkens or produces artifacts wherever a blast wave overlaps bright pixels. Clamp in the shader (for example saturate the output and use additive, or write `1 - saturate(dst)` via a different blend). Verify this with an explosion over a bright core.
- **Colour stability:** tonemapping applies equally to every arena, so the "player, enemies, projectiles and pickups remain colour-stable across arenas" rule holds. It still shifts them relative to today, so compare player and enemy sprites in the captures.
- **Arena and screen-specific shaders:** the Court tile shader (`CourtTile.shader`), `HydraDisintegrate`, `DirectorBlackHole` (premultiplied), `FilamentGas` and the Null City world-space LCD sign all live in the camera's image and will change. Capture each.
- **Unaffected:** the dealer room and music perimeter are UI-canvas (overlay) content, so post doesn't affect them. Still spot-check them with their probes.

## R4: Arena colour grade darkens the image

Evidence: `VideoSettings.cs` `ApplyArenaColorGrade` L79-90 sets contrast 9, saturation 12 and **postExposure −0.16** on every arena except White Sakura. The Sakura exclusion is an existing decision; keep it.

Fix: after R3, set `postExposure` to 0 and re-evaluate contrast and saturation against the new tonemapper. Per-arena values are fine if one global value doesn't suit every arena, but keep the Sakura exclusion.

## R5: No mipmaps on minified sprites

Evidence:
- The gameplay view is about 1728×972 world units at 16:9, which is about 1.11 screen px per world unit at 1080p.
- `Editor/ProceduralSpriteBaker.cs` L353: `importer.mipmapEnabled = false`. This covers the 510 baked sprites, Zack and the whole shared roster.
- `Editor/ArenaContentBaker.cs` `ImportHydraBossTexture` ≈L262-280: `HydraPrime.png` is 1024², PPU 1024, `mipmapEnabled = false`. It is drawn at a few hundred pixels, so it is minified 2–3× with no mip chain.
- Also mip-less, but lower priority (check whether each is drawn smaller than native before changing): `DirectorContentBaker.cs` L26, `JourneyVisualBaker.cs` L46 (portals), `RouteMapThumbnailBaker.cs` L196, `PortalSpritePostprocessor.cs` L24.
- **Leave `DealerAssetImporter.cs` alone:** dealer fidelity is owner-locked and drawn at reference scale.
- Already correct: `ApprovedMapAssetImporter` (L24), the arena plates and the Null City, Eon Sea and Crascendo bakers all enable mips.

Fix, through the authoring path only. Never hand-edit `.meta` files or generated assets (AGENTS.md):
1. In `ProceduralSpriteBaker` set `importer.mipmapEnabled = true`, `importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter` (keeps thin outlines crisper than Box), `importer.mipMapsPreserveCoverage = false`, and keep `alphaIsTransparency = true` (colour dilation prevents dark fringes in lower mips).
2. Do the same in `ImportHydraBossTexture`.
3. Reimport through the existing baker entry points so asset paths and **GUIDs stay the same**. REPO_MAP lists a full bake plus targeted batches (`BakeArsenal`, `BakeSurvivalSpritesBatch`, `RepairCorruptedSpritesBatch`). An importer-only change can reimport the existing PNGs without redrawing them.
4. Run `Tests/Editor/ArenaBakeTests.cs` and the `PreparedContentBuildGate` (`Editor/PreparedContentBuildSetup.cs`). The existing mip assertion (ArenaBakeTests L140) covers plates only, so add one for procedural sprites.

Also verify: runtime `new Texture2D(..., TextureFormat.RGBA32, false)` has no mips. Examples:
- atlas pages in `Runtime/Gameplay/ProceduralSpriteFactory.cs` ≈L4060
- `Runtime/Gameplay/ArenaPlateFactory.cs` L192 and L358
- the filament mask at `VoidFallGameRuntime.Arena.cs` ≈L986

This only matters for textures drawn smaller than native. Confirm whether live enemies use baked catalogue sprites (fine after R5) or runtime atlas pages. Add `mipChain: true` and `Apply(true)` only where the texture is actually minified. Soft gradients (vignettes, filament plates) don't need it.

## R6: Marketing photo mode and supersampled capture

Evidence: every capture probe calls `ScreenCapture.CaptureScreenshot(path)` without `superSize`:
- `Runtime/VisualDeliveryProbe.cs` L80
- `Runtime/RouteJourneyProbe.cs` L137-300
- `Runtime/DealerIntegrationProbe.cs` L74
- `Runtime/StressBenchmarkProbe.cs` L168

Fix: add an owner-only photo mode, gated behind a command-line flag (for example `-vfphoto`) so it can't reach players by accident.
- A hotkey toggles hiding the HUD canvas and the menu canvas (the gameplay mute/pause chrome too).
- Another key freezes presentation. Reuse the existing pause ownership rather than adding a competing state machine: the runtime owns flow (AGENTS.md), and `SyncUiScreen` must stay authoritative.
- Optional: free camera pan and zoom while frozen, presentation-only.
- A capture key writes `ScreenCapture.CaptureScreenshot(path, 2)` (3840×2160 from 1080p) to a folder beside the executable, not inside `Assets/`.
- Must not consume combat or FX RNG, touch the simulation, or change saves. Keep it out of telemetry, or record only a "photo mode used" flag if the exporter contract calls for one.

## R7: Vsync off in the "balanced" preset

Evidence: `Runtime/Gameplay/VoidFallGameRuntime.UI.cs` L838-855. "low" and "balanced" set `QualitySettings.vSyncCount = 0` with 45 and 60 fps caps. "auto" and "high" use vsync 1.

Fix: use `vSyncCount = 1` for "balanced" too; `targetFrameRate` is ignored under vsync. Keep "low" as is, since it's a performance escape hatch. At minimum, record all marketing footage on "high" or "auto".

## R8 (optional, after R1–R7): sharper sprites at 1440p and 4K

Zack's baked sprite is 74×74 px at PPU 74 (`Generated/ProceduralSprites/Sprite_0089_fixed_operative.png`), and the shared roster is baked at similar density. At 4K, each world unit covers about 2.2 px, so these sprites are magnified and look soft.

A 2× bake with 2× PPU keeps world sizes identical. It must preserve REPO_MAP's cosmetic sizing invariant: renderers restore design-pixel dimensions from `sprite.pixelsPerUnit`, then apply 74/94. Also run `WorkshopCosmeticsIntegrationTests` (native rendering, not `-nographics`).

Only do this if the owner wants 4K trailer footage. R1–R7 matter more.

## Out of scope here (noted for later)

- **UI text:** legacy `UnityEngine.UI.Text` with OS dynamic fonts (`Font.CreateDynamicFontFromOSFont`, `VoidFallGameRuntime.cs` ≈L5195-5227, `UI/Core/UITheme.cs` ≈L470-560) blurs at small sizes and varies between PCs. A TextMeshPro SDF migration is a separate UI project.
- **Per-map visual remaster:** being prototyped separately in `../Prototypes/map-remaster-study`. It depends on R3 so that approved browser glows translate.

## Implementation order

1. **Baseline.** Build a staging player (`VOIDFALL_BUILD_OUTPUT`) from the unchanged source. Capture "before" images into `Logs/RenderingFidelity/Before/` using the existing probes, each with an isolated profile beside the output:
   - `-vfmapcheck=<dir>`: City, Court, Hydra and both native boss transitions
   - `-vfvisual-check=<dir>`: meteors, lane waves, boss/Overclock
   - `-vfeonsea=late -vfcapture=<path>` and `-vfcrascendo=late -vfcapture=<path>`
   - `-vfoverclock-check=<dir>`, `-vfdealer-check=<dir>`, `-vfjourney=map -vfoutput=<prefix>`
   - `-vfarsenal=all -vfarsenal-check=<dir>`: weapon FX
   - an ordinary Abyss capture, for example `-vfscenario=directorI -vfscreenshot=<path>`

   Capture at 1920×1080 and 2560×1440. Use a rendering player without `-batchmode`; headless captures come out black.
2. **R1 + R2.** Rebuild staging and capture "after-A".
3. **R3 + R4 together**, including the emissive boosts and the ScreenBlend clamp. Tune, then capture "after-B". Put comparable pairs side by side.
4. **R5.** Rebake, run the asset tests and build gate, capture. Record a 10-second clip of a moving crowd to show the shimmer reduction.
5. **R7, then R6.**
6. **Full verification** (the commands are in AGENTS.md):
   - EditMode and PlayMode tests. Read the XML. The golden master and the 32-seed sweep must pass **without re-pinning**.
   - A Windows build via `VoidFall.EditorTools.BuildScript.BuildWindows` into staging.
   - Performance: `-vfbench` and `-vfhold750` frame timings before vs after. Check advancing simulation, not just wall time. HDR plus MSAA 4× must stay within budget on the owner's machine. If it doesn't, fall back to MSAA 2× (with `allowMSAA = true`) before dropping HDR.
7. **Owner review.** Show the before/after pairs and the frame-time comparison, and promote only after approval.
8. **Docs.** Update the `Docs/REPO_MAP.md` rendering bullet (HDR, tonemapper, AA, dithering, emissive material variants, photo-mode flag). Don't turn it into a change log.

## Do not

- Hand-edit generated sprites, `.meta` files or Addressables output. Use the bakers.
- Change `UrpPipelineSetup.ConfigureSampleSceneCamera`'s post-processing reset. Runtime enables post intentionally.
- Apply post vignette, grain or chromatic aberration as part of this pass. The arena vignette sprites already exist (`ProceduralSpriteFactory.ArenaVignette`), and chromatic defaults to 0 by owner choice. Court forces chromatic to 0 (`VoidFallGameRuntime.MapPresentation.cs` ≈L24).
- Change the White Sakura grade exclusion, the dealer importer, or HUD layout and fonts.
- Re-pin the golden master. A hash change means non-rendering code was touched.
