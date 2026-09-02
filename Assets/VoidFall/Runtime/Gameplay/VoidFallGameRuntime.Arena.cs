using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using VoidFall.Core;
using VoidFall.Persistence;
using VoidFall.Runtime.Rendering;
using VoidFall.UI;
namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {

        private void CycleNextArenaFromUi()
        {
            var arenas = ContentOrder.PreparedArenas;
            var currentIdx = Array.IndexOf(arenas, _arenaId);
            if (currentIdx < 0) currentIdx = 0;
            var nextIdx = (currentIdx + 1) % arenas.Length;
            _arenaId = arenas[nextIdx];
            SelectRecipeForCurrentArena();
            PrepareMenuArenaCatalogue();
            if (_saveData != null) _saveData.arena = ArenaIdName(_arenaId);
            RefreshMenuProfileUi();
            _audio?.Play(ProceduralAudio.Cue.Ui, 1f);
        }

        private void CyclePrevArenaFromUi()
        {
            var arenas = ContentOrder.PreparedArenas;
            var currentIdx = Array.IndexOf(arenas, _arenaId);
            if (currentIdx < 0) currentIdx = 0;
            var prevIdx = (currentIdx - 1 + arenas.Length) % arenas.Length;
            _arenaId = arenas[prevIdx];
            SelectRecipeForCurrentArena();
            PrepareMenuArenaCatalogue();
            if (_saveData != null) _saveData.arena = ArenaIdName(_arenaId);
            RefreshMenuProfileUi();
            _audio?.Play(ProceduralAudio.Cue.Ui, 1f);
        }

        /// <summary>
        /// Leaves the UI backdrop transparent so the live arena renderer remains
        /// visible behind the menus. The old baked RawImage hid the actual Void
        /// decor, making the home background appear static.
        /// </summary>
        private void PushUiBackdrop()
        {
            _ui?.SetBackdrop(null);
        }

        private static ProceduralAudio.Cue? ArenaTransitionCueFor(ArenaTransitionEvent eventType)
        {
            switch (eventType)
            {
                case ArenaTransitionEvent.Warn: return ProceduralAudio.Cue.Warning;
                case ArenaTransitionEvent.Swap: return ProceduralAudio.Cue.Ui;
                default: return null;
            }
        }

        private bool ArenaTransitionBlocked()
        {
            return IsArenaTransitionBlocked(
                ActiveBosses(),
                _time < _bossRecoveryUntil,
                _levelUpActive || _levelUpTimer >= 0,
                _paused,
                _revivePending);
        }

        private static bool IsArenaFolding(ArenaPhase phase)
        {
            return phase == ArenaPhase.Collapse || phase == ArenaPhase.Settle;
        }

        private static bool IsArenaTransitionBlocked(
            int activeBosses,
            bool bossRecovery,
            bool levelUp,
            bool paused,
            bool revive)
        {
            return activeBosses > 0 || bossRecovery || levelUp || paused || revive;
        }

        private bool ArenaHasFeature(string feature)
        {
            var arena = FindArena(ArenaIdName(_arenaId));
            if (arena?.Features == null) return false;
            for (var index = 0; index < arena.Features.Length; index++)
            {
                if (arena.Features[index] == feature) return true;
            }

            return false;
        }

        private bool TryInstallPreparedArenaPlate(ArenaId arena)
        {
            var index = (int)arena;
            if (index < 0 || index >= _arenaPlateSprites.Length) return false;

            ArenaPlateAsset asset = null;
            var key = ArenaPackageFor(arena);
            if (_arenaResidency != null &&
                _arenaResidency.TryGet(key, out var recipe) &&
                recipe != null)
            {
                asset = recipe.Plate;
            }

            if (asset == null) return false;

            _preparedArenaPlateAssets[index] = asset;
            _preparedArenaPlateKeys[index] = key;
            _arenaPlateSprites[index] = asset.BaseSprite;
            _arenaPlateDetailSprites[index] = asset.DetailSprite;
            _arenaPlateBakeWidth = asset.Width;
            _arenaPlateBakeHeight = asset.Height;
            _arenaPlateDetailBakeWidth = asset.DetailWidth;
            _arenaPlateDetailBakeHeight = asset.DetailHeight;
            return true;
        }

        private void EnsureArenaPlate(ArenaId arena)
        {
            var index = (int)arena;
            if (index < 0 || index >= _arenaPlateSprites.Length) return;
            if (_arenaPlateSprites[index] != null && _arenaPlateDetailSprites[index] != null) return;

            TryInstallPreparedArenaPlate(arena);
        }

        private void BeginArenaPackageLoad(ArenaId arena)
        {
            _arenaResidency?.Acquire(ArenaPackageFor(arena));
        }

        private void EnsureArenaPlateViewport()
        {
            EnsureArenaPlate(_arenaId);
        }

        private void SetupBackdrop()
        {
            var backdrop = new GameObject("VoidFall Arena Backdrop");
            backdrop.transform.SetParent(_worldRoot, false);
            var renderer = backdrop.AddComponent<SpriteRenderer>();
            _backdropView = renderer;
            // The sprite arrives from the arena package asynchronously. Until
            // then the camera's clear color is the intentional lightweight
            // fallback; player builds never generate arena pixels here.
            renderer.color = Color.white;
            var viewportHalf = GameplayViewportHalfExtent();
            renderer.transform.localScale = new Vector3(
                viewportHalf.x * 2f * ArenaSkyOverscan / Mathf.Max(1, _arenaPlateBakeWidth),
                viewportHalf.y * 2f * ArenaSkyOverscan / Mathf.Max(1, _arenaPlateBakeHeight),
                1);
            // Keep every backdrop layer below the browser's grid pass. The
            // source draws the complete arena backdrop first, then the grid.
            renderer.sortingOrder = -110;
            // Sprite is assigned by RenderArena once the plate for the active
            // arena has been baked.
            _arenaBakedDetailView = CreateView(
                "Arena Baked Edge Details",
                null,
                -106);
            _arenaBakedDetailView.color = Color.white;
            _arenaBakedDetailView.transform.localScale = new Vector3(
                viewportHalf.x * 2f * ArenaSkyOverscan / Mathf.Max(1, _arenaPlateDetailBakeWidth),
                viewportHalf.y * 2f * ArenaSkyOverscan / Mathf.Max(1, _arenaPlateDetailBakeHeight),
                1);

            // Arena colour grading belongs to the arena, not to the player.
            // The old fullscreen overlay lived on the HUD canvas and therefore
            // tinted the operative, projectiles and UI—most visibly in Sakura.
            _arenaVignetteView = CreateView(
                "Arena World Vignette",
                ProceduralSpriteFactory.ArenaVignette(ArenaId.Void),
                -90);
            _arenaVignetteView.color = Color.white;

            _arenaGridView = CreateMeshView("Arena Void Grid", -95, out _arenaGridRenderer);
            _arenaGridMesh = _arenaGridView.sharedMesh;
            _arenaGridMesh.MarkDynamic();
            // Keep the vertex colour at the browser's source alpha (the
            // parity test and source both use 0.065). Sprites/Default on this
            // linear mesh path renders that alpha too strongly, so attenuate
            // the material once instead of changing the source data.
            if (_arenaGridRenderer.material != null)
                _arenaGridRenderer.material.color = new Color(1f, 1f, 1f, 0.1f);
            _arenaGridRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _arenaGridRenderer.receiveShadows = false;
            _arenaGridRenderer.enabled = false;

            for (var index = 0; index < MaxArenaMotes; index++)
            {
                var mote = CreateView(
                    "Arena Mote_" + index,
                    ProceduralSpriteFactory.ArenaDot(Color.white),
                    -97);
                mote.enabled = true;
                _arenaMoteViews[index] = mote;
            }
            for (var index = 0; index < MaxArenaStars; index++)
                _arenaStarViews[index] = CreateView(
                    "Arena Star_" + index,
                    ProceduralSpriteFactory.ArenaDot(Color.white),
                    -101);
            _arenaCurrentGlowView = CreateView(
                "Arena Current Glow",
                ProceduralSpriteFactory.ArenaCurrentGlow(),
                -96);

            for (var index = 0; index < MaxArenaRocks; index++)
            {
                var rock = CreateView("Arena Rock_" + index, ProceduralSpriteFactory.Rock(), -100);
                _arenaRockViews[index] = rock;
                _arenaRockPlaneViews[index] = CreateView(
                    "Arena Rock Plane_" + index,
                    ProceduralSpriteFactory.Circle(),
                    -99);
                _arenaRockRimViews[index] = CreateLineView("Arena Rock Rim_" + index, -98);
            }

            _arenaLandmarkBodyView = CreateView(
                "Arena Landmark Body",
                ProceduralSpriteFactory.Circle(),
                -108);
            for (var index = 0; index < MaxArenaStellarRimSegments; index++)
            {
                _arenaStellarRimViews[index] = CreateLineView("Arena Stellar Rim_" + index, -107);
                ConfigureRoundLine(_arenaStellarRimViews[index]);
            }
            for (var index = 0; index < MaxArenaLandmarkSegments; index++)
            {
                _arenaLandmarkViews[index] = CreateLineView("Arena Landmark Slab_" + index, -108);
                _arenaLandmarkRimViews[index] = CreateLineView("Arena Landmark Rim_" + index, -107);
                _arenaRingSlabFillViews[index] = CreateMeshView(
                    "Arena Landmark Slab Fill_" + index,
                    -108,
                    out _arenaRingSlabFillRenderers[index]);
                var vertices = new Vector3[ArenaRingSlabVertexCount];
                var colors = new Color[ArenaRingSlabVertexCount];
                var slabColor = new Color(0.227f, 0.216f, 0.259f, 0.9f);
                for (var vertex = 0; vertex < colors.Length; vertex++) colors[vertex] = slabColor;
                var triangles = new int[ArenaRingSlabSteps * 6];
                for (var step = 0; step < ArenaRingSlabSteps; step++)
                {
                    var triangle = step * 6;
                    var outer = step;
                    var nextOuter = step + 1;
                    var inner = ArenaRingSlabSteps + 1 + step;
                    var nextInner = inner + 1;
                    triangles[triangle] = outer;
                    triangles[triangle + 1] = nextOuter;
                    triangles[triangle + 2] = nextInner;
                    triangles[triangle + 3] = outer;
                    triangles[triangle + 4] = nextInner;
                    triangles[triangle + 5] = inner;
                }
                var mesh = _arenaRingSlabFillViews[index].sharedMesh;
                mesh.vertices = vertices;
                mesh.colors = colors;
                mesh.triangles = triangles;
                mesh.RecalculateBounds();
                _arenaRingSlabVertices[index] = vertices;
            }
            for (var index = 0; index < MaxArenaRingDebris; index++)
                _arenaRingDebrisViews[index] = CreateView(
                    "Arena Ring Debris_" + index,
                    ProceduralSpriteFactory.Rock(),
                    -108);
            for (var index = 0; index < MaxArenaOrbitViews; index++)
                _arenaOrbitViews[index] = CreateLineView("Arena Orbital Run_" + index, -104);
            for (var index = 0; index < MaxArenaOrbitFractures; index++)
                _arenaOrbitFractureViews[index] = CreateLineView("Arena Orbital Fracture_" + index, -104);

            _arenaFilamentPlateViews[0] = CreateView("Arena Red Near Filament Plate", null, -103);
            _arenaFilamentPlateViews[1] = CreateView("Arena White Near Filament Plate", null, -103);
            _arenaFilamentPlateViews[2] = CreateView("Arena Far Filament Plate", null, -109);

            for (var index = 0; index < _arenaNearFilamentOuterViews.Length; index++)
            {
                var far = index >= 4;
                var prefix = far ? "Arena Far Filament " : "Arena Near Filament ";
                var bandOrder = far ? -109 : -103;
                var strandOrder = far ? -109 : -102;
                _arenaNearFilamentOuterViews[index] = CreateMeshView(
                    prefix + "Band_" + index,
                    bandOrder,
                    out _arenaNearFilamentOuterRenderers[index]);
                ConfigureArenaFilamentMaterial(index);
                _arenaNearFilamentInnerViews[index] = CreateLineView(prefix + "Inner_" + index, strandOrder);
                _arenaNearFilamentInnerViews[index].widthCurve = CreateFilamentStrandWidthCurve();
                _arenaNearFilamentStrandViews[index] = CreateMeshView(
                    prefix + "Strand_" + index,
                    strandOrder,
                    out _arenaNearFilamentStrandRenderers[index]);
            }
            // Filament geometry depends on the settled viewport and selected
            // arena. Build it lazily from RenderArena instead of blocking Awake.
        }

        private void ConfigureArenaFilamentMaterial(int slot)
        {
            if (slot < 0 || slot >= _arenaNearFilamentMaterials.Length) return;
            var renderer = _arenaNearFilamentOuterRenderers[slot];
            if (renderer == null) return;

            var material = VoidFallRenderMaterials.CreateFilamentInstance();
            material.name = "VoidFall Filament Gas " + slot;
            material.hideFlags = HideFlags.HideAndDontSave;
            material.SetFloat("_PassCount", ArenaNearFilamentPasses);
            renderer.sharedMaterial = material;
            _arenaNearFilamentMaterials[slot] = material;
            _dynamicMaterials.Add(material);
        }

        private static AnimationCurve CreateFilamentStrandWidthCurve()
        {
            var curve = new AnimationCurve();
            curve.AddKey(0f, 0f);
            curve.AddKey(0.12f, 1f);
            curve.AddKey(0.88f, 1f);
            curve.AddKey(1f, 0f);
            return curve;
        }

        private void ConfigureArenaMoteSeeds(bool reducedMotion)
        {
            var detail = _qualityPreset.Detail;
            var moteScale = detail >= 2 ? 1f : detail == 1 ? 0.55f : 0.28f;
            var sourceBudget = _arenaId == ArenaId.WhiteSakura ? 80 : 70;
            _arenaMoteSeedCount = Mathf.Min(
                MaxArenaMotes,
                SourceRound(sourceBudget * moteScale * (reducedMotion ? 0.45f : 1f)));
            var petal = _arenaId == ArenaId.WhiteSakura;
            var stream = _runSeed ^ 0x5bf03635u ^ (uint)ArenaRockNoiseSeed(_arenaId) ^
                ArenaCatalogRules.RecipeLayout(_arenaRecipeIndex).DecorSalt;
            for (var index = 0; index < _arenaMoteSeedCount; index++)
            {
                var roll = ArenaDecorStreamNext(ref stream);
                var depth = roll < 0.5f ? 0 : roll < 0.88f ? 1 : 2;
                var x = ArenaDecorStreamNext(ref stream) * ArenaDecorField;
                var y = ArenaDecorStreamNext(ref stream) * ArenaDecorField;
                var baseSize = petal
                    ? depth == 2 ? 15f : depth == 1 ? 10f : 6.5f
                    : depth == 2 ? 5.5f : depth == 1 ? 3.6f : 2.4f;
                var size = baseSize * (petal
                    ? 0.75f + ArenaDecorStreamNext(ref stream) * 0.5f
                    : 0.7f + ArenaDecorStreamNext(ref stream) * 0.6f);
                var rotation = ArenaDecorStreamNext(ref stream) * Mathf.PI * 2f;
                var spin = (ArenaDecorStreamNext(ref stream) - 0.5f) * (petal ? 1.1f : 0.3f);
                var rate = 0.55f + ArenaDecorStreamNext(ref stream) * 0.9f;
                var phase = ArenaDecorStreamNext(ref stream) * Mathf.PI * 2f;
                _arenaMoteSeeds[index] = new Vector4(x, y, rotation, phase);
                _arenaMoteSizes[index] = size;
                _arenaMoteSpins[index] = spin;
                _arenaMoteRates[index] = rate;
                _arenaMoteParallax[index] = depth == 2 ? 0.72f : depth == 1 ? 0.45f : 0.26f;
                _arenaMoteDepths[index] = depth;
            }
            for (var index = _arenaMoteSeedCount; index < MaxArenaMotes; index++)
            {
                _arenaMoteSeeds[index] = Vector4.zero;
                _arenaMoteSizes[index] = 0;
                _arenaMoteSpins[index] = 0;
                _arenaMoteRates[index] = 0;
                _arenaMoteParallax[index] = 0;
                _arenaMoteDepths[index] = 0;
            }
            _arenaMoteSeedArena = _arenaId;
            _arenaMoteSeedDetail = detail;
            _arenaMoteSeedReducedMotion = reducedMotion;
            _arenaMoteSeedsReady = true;
        }

        private static float WrapArenaMote(float value, float span, float margin)
        {
            if (span <= 0f) return value - margin;
            var wrapped = ((value % span) + span) % span;
            return wrapped - margin;
        }

        private static float ArenaDecorScreenCoordinate(
            float fieldCoordinate,
            float cameraCentre,
            float halfExtent,
            float parallax)
        {
            return ArenaWrappedScreenCoordinate(
                fieldCoordinate,
                ArenaDecorField,
                cameraCentre,
                halfExtent,
                parallax);
        }

        private static float ArenaWrappedScreenCoordinate(
            float fieldCoordinate,
            float span,
            float cameraCentre,
            float halfExtent,
            float parallax)
        {
            // Browser decorative drawing receives camera top-left coordinates:
            // cam = cameraCentre - viewportHalfExtent. Preserve that mapping
            // before converting the screen coordinate back to Unity world space.
            var margin = (span - halfExtent * 2f) * 0.5f;
            var cameraTopLeft = cameraCentre - halfExtent;
            return WrapArenaMote(
                fieldCoordinate - cameraTopLeft * parallax,
                span,
                margin);
        }

        private static Color ArenaStarColor(ArenaId arena)
        {
            // The browser passes sprites.dot.white as starSprite for every
            // arena; the arena palette does not retint this source sprite.
            return ParseColor("#e2e8f0", Color.white);
        }

        private static Color ArenaCloudTint(ArenaId arena)
        {
            switch (arena)
            {
                case ArenaId.RedNebula: return ParseColor("#b04a34", Color.white);
                case ArenaId.WhiteSakura: return ParseColor("#545660", Color.white);
                default: return ParseColor("#54689e", Color.white);
            }
        }

        private void ConfigureArenaRockSeeds()
        {
            var detail = _qualityPreset.Detail;
            var reducedMotion = _saveData?.settings != null && _saveData.settings.reducedMotion;
            if (!_arenaMoteSeedsReady ||
                _arenaMoteSeedArena != _arenaId ||
                _arenaMoteSeedDetail != detail ||
                _arenaMoteSeedReducedMotion != reducedMotion)
            {
                ConfigureArenaMoteSeeds(reducedMotion);
            }
            var rockScale = detail >= 2 ? 1f : detail == 1 ? 0.75f : 0.5f;
            var sourceFarCount = _arenaId == ArenaId.RedNebula ? 4 :
                _arenaId == ArenaId.WhiteSakura ? 3 : 0;
            var sourceMidCount = _arenaId == ArenaId.RedNebula ? 6 :
                _arenaId == ArenaId.WhiteSakura ? 5 : 0;
            _arenaRockFarCount = sourceFarCount > 0
                ? Mathf.Max(1, SourceRound(sourceFarCount * rockScale))
                : 0;
            _arenaRockTotalCount = _arenaRockFarCount +
                SourceRound(sourceMidCount * rockScale);

            var stream = _runSeed ^ 0x5bf03635u ^ (uint)ArenaRockNoiseSeed(_arenaId) ^
                ArenaCatalogRules.RecipeLayout(_arenaRecipeIndex).DecorSalt;
            // React's createArenaDecor keeps one stream alive: all mote
            // records are consumed before makeRocks starts reading it. Unity
            // used to restart here, which was deterministic but not source
            // parity. Each mote consumes eight values in ConfigureArenaMoteSeeds.
            for (var mote = 0; mote < _arenaMoteSeedCount; mote++)
            {
                for (var value = 0; value < 8; value++) ArenaDecorStreamNext(ref stream);
            }
            for (var index = 0; index < _arenaRockTotalCount; index++)
            {
                // This is the same seven-value record as createArenaDecor's
                // makeRocks: x, y, size roll, rotation, spin, shape, tone.
                _arenaRockSeeds[index] = new Vector4(
                    ArenaDecorStreamNext(ref stream),
                    ArenaDecorStreamNext(ref stream),
                    ArenaDecorStreamNext(ref stream),
                    ArenaDecorStreamNext(ref stream));
                _arenaRockSpins[index] = (ArenaDecorStreamNext(ref stream) - 0.5f) * 0.045f;
                _arenaRockShapes[index] = Mathf.FloorToInt(ArenaDecorStreamNext(ref stream) * 6f);
                _arenaRockTones[index] = ArenaDecorStreamNext(ref stream);
            }
            for (var index = _arenaRockTotalCount; index < MaxArenaRocks; index++)
            {
                _arenaRockSeeds[index] = Vector4.zero;
                _arenaRockSpins[index] = 0;
                _arenaRockShapes[index] = 0;
                _arenaRockTones[index] = 0;
            }
            _arenaRockSeedArena = _arenaId;
            _arenaRockSeedDetail = detail;
            _arenaRockSeedReducedMotion = reducedMotion;
            _arenaRockSeedsReady = true;
        }

        private static int ArenaRockNoiseSeed(ArenaId arena)
        {
            switch (arena)
            {
                case ArenaId.RedNebula: return 0x7c1f;
                case ArenaId.WhiteSakura: return 0x2ad9;
                default: return 0x51a3;
            }
        }

        private static float ArenaDecorStreamNext(ref uint state)
        {
            state += 0x6d2b79f5u;
            var value = state;
            value = (value ^ (value >> 15)) * (value | 1u);
            value ^= value + ((value ^ (value >> 7)) * (value | 61u));
            return (value ^ (value >> 14)) / 4294967296f;
        }

        private void HideArenaFilamentCompatibilityViews(int startSlot, int count)
        {
            var endSlot = Mathf.Min(startSlot + Mathf.Max(0, count), _arenaNearFilamentOuterRenderers.Length);
            for (var slot = Mathf.Max(0, startSlot); slot < endSlot; slot++)
            {
                Hide(_arenaNearFilamentOuterRenderers[slot]);
                Hide(_arenaNearFilamentInnerViews[slot]);
                Hide(_arenaNearFilamentStrandRenderers[slot]);
            }
        }

        private void BuildArenaNearFilamentData()
        {
            BuildArenaFilament(0, 0, -0.44f, 0.085f, ArenaNearOverscan, 0x7c1f + 991, 0.34f, ParseColor("#ad4028", Color.white), ParseColor("#d98750", Color.white));
            BuildArenaFilament(1, 1, -0.44f, 0.085f, ArenaNearOverscan, 0x7c1f + 991, 0.34f, ParseColor("#7d341a", Color.white), ParseColor("#d98750", Color.white));
            BuildArenaFilament(2, 0, 0.66f, 0.06f, ArenaNearOverscan, 0x2ad9 + 991, 0.32f, ParseColor("#e3b9cd", Color.white), ParseColor("#f6dfe9", Color.white));
            BuildArenaFilament(3, 1, 0.66f, 0.06f, ArenaNearOverscan, 0x2ad9 + 991, 0.32f, ParseColor("#f0cbdd", Color.white), ParseColor("#f6dfe9", Color.white));
            var viewportHalf = RenderViewportHalfExtent();
            BuildArenaFilamentGroupNotchMask(
                0,
                0,
                2,
                viewportHalf.x * 2f * ArenaNearOverscan,
                viewportHalf.y * 2f * ArenaNearOverscan);
            BuildArenaFilamentGroupNotchMask(
                1,
                2,
                2,
                viewportHalf.x * 2f * ArenaNearOverscan,
                viewportHalf.y * 2f * ArenaNearOverscan);
            _arenaFilamentViewportWidth = viewportHalf.x * 2f;
            _arenaFilamentViewportHeight = viewportHalf.y * 2f;
        }

        private void EnsureArenaFilamentViewport()
        {
            var viewportHalf = RenderViewportHalfExtent();
            var width = viewportHalf.x * 2f;
            var height = viewportHalf.y * 2f;
            if (Mathf.Approximately(_arenaFilamentViewportWidth, width) &&
                Mathf.Approximately(_arenaFilamentViewportHeight, height))
                return;

            var started = Time.realtimeSinceStartupAsDouble;
            BuildArenaNearFilamentData();
            ConfigureArenaFarFilaments();
            var elapsedMs = (Time.realtimeSinceStartupAsDouble - started) * 1000.0;
            if (elapsedMs >= 10.0)
                Debug.Log(
                    "VOIDFALL_ARENA_FILAMENT_BUILD arena=" + _arenaId +
                    " width=" + width.ToString("F0") +
                    " height=" + height.ToString("F0") +
                    " milliseconds=" + elapsedMs.ToString("F1"));
        }

        private void ConfigureArenaFarFilaments()
        {
            var isRed = _arenaId == ArenaId.RedNebula;
            var isWhite = _arenaId == ArenaId.WhiteSakura;
            var count = isRed ? 4 : isWhite ? 3 : 0;
            var angle = isRed ? -0.62f : 0.5f;
            var widthFraction = isRed ? 0.17f : 0.13f;
            var seed = (isRed ? 0x7c1f : 0x2ad9) ^
                unchecked((int)ArenaCatalogRules.RecipeLayout(_arenaRecipeIndex).DecorSalt);
            var alpha = isRed ? 0.5f : 0.3f;
            var breakTint = isWhite ? ParseColor("#efe6ea", Color.white) : ParseColor("#c9713f", Color.white);
            var colors = isRed
                ? new[] { ParseColor("#7f1d2e", Color.white), ParseColor("#98301f", Color.white), ParseColor("#5b1a2c", Color.white), ParseColor("#6d2233", Color.white) }
                : new[] { ParseColor("#c4c3c6", Color.white), ParseColor("#cbc8c6", Color.white), ParseColor("#bdbcc2", Color.white) };

            for (var index = 0; index < 4; index++)
            {
                var slot = 4 + index;
                if (index < count)
                {
                    BuildArenaFilament(
                        slot,
                        index,
                        angle,
                        widthFraction,
                        ArenaSkyOverscan,
                        seed,
                        alpha,
                        colors[index],
                        breakTint);
                }
                else
                {
                    _arenaNearFilamentPoints[slot] = null;
                    Hide(_arenaNearFilamentOuterRenderers[slot]);
                    Hide(_arenaNearFilamentStrandRenderers[slot]);
                }
            }

            var viewportHalf = RenderViewportHalfExtent();
            BuildArenaFilamentGroupNotchMask(
                2,
                4,
                4,
                viewportHalf.x * 2f * ArenaSkyOverscan,
                viewportHalf.y * 2f * ArenaSkyOverscan);
            _arenaFarFilamentCount = count;
            _arenaFarFilamentSeedArena = _arenaId;
            _arenaFarFilamentSeedsReady = true;
        }

        private void BuildArenaFilament(
            int slot,
            int sourceIndex,
            float angle,
            float widthFraction,
            float overscan,
            int seed,
            float alpha,
            Color color,
            Color coreColor)
        {
            var viewportHalf = RenderViewportHalfExtent();
            var width = viewportHalf.x * 2f * overscan;
            var height = viewportHalf.y * 2f * overscan;
            var shorter = Mathf.Min(width, height);
            var stream = unchecked((uint)(seed + sourceIndex * 7919));
            var actualAngle = angle + (NearStreamNext(ref stream) - 0.5f) * 1.15f;
            var across = (0.1f + NearStreamNext(ref stream) * 0.86f) * height;
            var span = Mathf.Sqrt(width * width + height * height) * 1.35f;
            var cosine = Mathf.Cos(actualAngle);
            var sine = Mathf.Sin(actualAngle);
            var originX = width * 0.5f - cosine * span * 0.5f;
            var originY = across - sine * span * 0.5f;
            var baseWidth = widthFraction * shorter * (0.62f + NearStreamNext(ref stream) * 0.85f);
            var points = new Vector2[37];
            var pointWidths = new float[points.Length];
            for (var step = 0; step < points.Length; step++)
            {
                var t = step / (float)(points.Length - 1);
                var along = t * span;
                var noiseSeed = unchecked(seed + sourceIndex * 104729);
                var wander = (NearFbm(t * 1.9f, sourceIndex * 3.3f, noiseSeed, 3) - 0.5f) * baseWidth * 3.4f +
                    (NearValueNoise(t * 6.1f, sourceIndex * 5.1f, noiseSeed + 33) - 0.5f) * baseWidth * 0.8f;
                var taper = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
                var breathe = 0.45f + 0.55f * NearFbm(t * 3.4f + 11f, sourceIndex * 2.7f, noiseSeed + 77, 2);
                pointWidths[step] = Mathf.Max(1f, baseWidth * breathe * (0.35f + taper * 0.75f));
                points[step] = new Vector2(
                    originX + cosine * along - sine * wander,
                    originY + sine * along + cosine * wander);
            }
            _arenaNearFilamentPoints[slot] = points;
            _arenaNearFilamentPointWidths[slot] = pointWidths;
            _arenaNearFilamentPointSpacings[slot] = span / (points.Length - 1);
            _arenaNearFilamentWidths[slot] = baseWidth;
            _arenaNearFilamentColors[slot] = color;
            _arenaNearFilamentCoreColors[slot] = coreColor;
            _arenaNearFilamentAlphas[slot] = alpha;

            var notchStream = unchecked((uint)(seed + sourceIndex * 5381));
            var notches = new Vector4[7];
            var notchHeights = new float[notches.Length];
            for (var notch = 0; notch < notches.Length; notch++)
            {
                var at = 1 + Mathf.FloorToInt(NearStreamNext(ref notchStream) * (points.Length - 2));
                var pointWidth = pointWidths[at];
                var reach = pointWidth * (0.5f + NearStreamNext(ref notchStream) * 0.9f);
                var cut = 0.22f + NearStreamNext(ref notchStream) * 0.34f;
                var lateral = (NearStreamNext(ref notchStream) - 0.5f) * pointWidth * 1.1f;
                var notchHeight = pointWidth * (0.14f + NearStreamNext(ref notchStream) * 0.26f);
                notches[notch] = new Vector4(at, reach, cut, lateral);
                notchHeights[notch] = notchHeight;
            }
            _arenaNearFilamentNotches[slot] = notches;
            _arenaNearFilamentNotchHeights[slot] = notchHeights;

            var strandFrom = 2 + Mathf.FloorToInt(NearStreamNext(ref notchStream) * (points.Length * 0.32f));
            var strandTo = Mathf.Min(
                points.Length - 2,
                strandFrom + SourceRound(points.Length * (0.28f + NearStreamNext(ref notchStream) * 0.3f)));
            _arenaNearFilamentStrandFrom[slot] = strandFrom;
            _arenaNearFilamentStrandTo[slot] = strandTo;
            _arenaNearFilamentStrandShifts[slot] = NearStreamNext(ref notchStream) < 0.5f ? -0.2f : 0.2f;
            BuildArenaNearFilamentBand(slot, width, height);
        }

        private void BuildArenaFilamentPlate(
            int group,
            int startSlot,
            int slotCount,
            float width,
            float height,
            Color breakTint,
            float peakMultiplier)
        {
            if (group < 0 || group >= _arenaFilamentPlateViews.Length) return;
            FilamentTextureDimensions(width, height, out var pixelWidth, out var pixelHeight);
            var endSlot = Mathf.Min(
                startSlot + Mathf.Max(0, slotCount),
                _arenaNearFilamentPoints.Length);
            var hasFilament = false;
            var plate = new FilamentRasterPlate(width, height, pixelWidth, pixelHeight);
            var halfLayer = new Vector2(width * 0.5f, height * 0.5f);
            for (var slot = Mathf.Max(0, startSlot); slot < endSlot; slot++)
            {
                var points = _arenaNearFilamentPoints[slot];
                var pointWidths = _arenaNearFilamentPointWidths[slot];
                var notches = _arenaNearFilamentNotches[slot];
                var notchHeights = _arenaNearFilamentNotchHeights[slot];
                if (points == null || pointWidths == null || notches == null) continue;
                hasFilament = true;

                var localPoints = new Vector2[points.Length];
                for (var point = 0; point < points.Length; point++)
                    localPoints[point] = points[point] - halfLayer;

                var peak = Mathf.Clamp01(_arenaNearFilamentAlphas[slot] * peakMultiplier);
                var perPass = 1f - Mathf.Pow(
                    1f - peak,
                    1f / ArenaNearFilamentPasses);
                for (var pass = 0; pass < ArenaNearFilamentPasses; pass++)
                {
                    plate.FillBand(
                        localPoints,
                        pointWidths,
                        1.6f - pass * 0.13f,
                        0f,
                        _arenaNearFilamentColors[slot],
                        perPass);
                }

                for (var notch = 0; notch < notches.Length; notch++)
                {
                    var data = notches[notch];
                    var pointIndex = Mathf.Clamp(
                        Mathf.RoundToInt(data.x),
                        0,
                        points.Length - 1);
                    var normal = NearFilamentNormal(points, pointIndex);
                    var rotation = Mathf.Atan2(-normal.x, normal.y);
                    var reach = Mathf.Max(0.75f, data.y);
                    var notchHeight = notchHeights != null && notch < notchHeights.Length
                        ? Mathf.Max(0.5f, notchHeights[notch])
                        : Mathf.Max(0.5f, pointWidths[pointIndex] * 0.24f);
                    var centre = localPoints[pointIndex] + normal * data.w;
                    plate.EraseEllipse(
                        centre,
                        reach,
                        notchHeight,
                        rotation,
                        Mathf.Clamp01(data.z));
                }

                var strandFrom = Mathf.Clamp(
                    _arenaNearFilamentStrandFrom[slot],
                    0,
                    points.Length - 1);
                var strandTo = Mathf.Clamp(
                    _arenaNearFilamentStrandTo[slot],
                    strandFrom,
                    points.Length);
                var strandCount = strandTo - strandFrom;
                if (strandCount <= 3) continue;

                var strandPoints = new Vector2[strandCount];
                var strandWidths = new float[strandCount];
                var strandShift = _arenaNearFilamentStrandShifts[slot];
                for (var localPoint = 0; localPoint < strandCount; localPoint++)
                {
                    var point = strandFrom + localPoint;
                    strandPoints[localPoint] = localPoints[point];
                    strandWidths[localPoint] = pointWidths[point] * Mathf.Sin(
                        ((localPoint + 0.5f) / strandCount) * Mathf.PI);
                }

                var strandPasses = ArenaNearStrandPasses;
                var strandAlpha = 1f - Mathf.Pow(
                    1f - Mathf.Clamp01(peak * 0.6f),
                    1f / strandPasses);
                for (var pass = 0; pass < strandPasses; pass++)
                {
                    plate.FillBand(
                        strandPoints,
                        strandWidths,
                        0.86f - pass * 0.094f,
                        strandShift,
                        breakTint,
                        strandAlpha);
                }
            }

            var view = _arenaFilamentPlateViews[group];
            if (!hasFilament)
            {
                ReplaceArenaFilamentPlate(group, null, null, width, height, pixelWidth, pixelHeight);
                if (view != null) view.enabled = false;
                return;
            }

            var texture = plate.ToTexture("VoidFall Filament Plate " + group);
            var sprite = Sprite.Create(
                texture,
                new Rect(0, 0, pixelWidth, pixelHeight),
                new Vector2(0.5f, 0.5f),
                1f);
            sprite.name = "VoidFall Filament Plate Sprite " + group;
            ReplaceArenaFilamentPlate(group, sprite, texture, width, height, pixelWidth, pixelHeight);
            if (view != null)
            {
                view.color = Color.white;
                view.enabled = false;
            }
        }

        private void ReplaceArenaFilamentPlate(
            int group,
            Sprite sprite,
            Texture2D texture,
            float width,
            float height,
            int pixelWidth,
            int pixelHeight)
        {
            if (_arenaFilamentPlateSprites[group] != null)
                Destroy(_arenaFilamentPlateSprites[group]);
            if (_arenaFilamentPlateTextures[group] != null)
                Destroy(_arenaFilamentPlateTextures[group]);
            _arenaFilamentPlateSprites[group] = sprite;
            _arenaFilamentPlateTextures[group] = texture;
            var view = _arenaFilamentPlateViews[group];
            if (view == null) return;
            view.sprite = sprite;
            view.transform.localScale = new Vector3(
                width / Mathf.Max(1, pixelWidth),
                height / Mathf.Max(1, pixelHeight),
                1f);
        }

        private static void FilamentTextureDimensions(
            float width,
            float height,
            out int pixelWidth,
            out int pixelHeight)
        {
            var longest = Mathf.Max(width, height);
            var scale = ArenaFilamentMaskMaxDimension / Mathf.Max(1f, longest);
            pixelWidth = Mathf.Max(
                ArenaFilamentMaskMinDimension,
                Mathf.RoundToInt(width * scale));
            pixelHeight = Mathf.Max(
                ArenaFilamentMaskMinDimension,
                Mathf.RoundToInt(height * scale));
        }

        private void BuildArenaFilamentGroupNotchMask(
            int group,
            int startSlot,
            int slotCount,
            float width,
            float height)
        {
            FilamentTextureDimensions(width, height, out var maskWidth, out var maskHeight);
            var factors = new float[maskWidth * maskHeight];
            for (var pixel = 0; pixel < factors.Length; pixel++) factors[pixel] = 1f;

            var hasFilament = false;
            var halfLayer = new Vector2(width * 0.5f, height * 0.5f);
            var endSlot = Mathf.Min(startSlot + Mathf.Max(0, slotCount), _arenaNearFilamentPoints.Length);
            for (var slot = Mathf.Max(0, startSlot); slot < endSlot; slot++)
            {
                var points = _arenaNearFilamentPoints[slot];
                var notches = _arenaNearFilamentNotches[slot];
                var notchHeights = _arenaNearFilamentNotchHeights[slot];
                if (points == null || notches == null || notches.Length == 0) continue;
                hasFilament = true;
                for (var notch = 0; notch < notches.Length; notch++)
                {
                    var data = notches[notch];
                    var pointIndex = Mathf.Clamp(Mathf.RoundToInt(data.x), 0, points.Length - 1);
                    var normal = NearFilamentNormal(points, pointIndex);
                    var tangent = new Vector2(normal.y, -normal.x);
                    if (tangent.sqrMagnitude < 0.000001f)
                    {
                        // centrelineNormal() returns zero for a zero-length source
                        // segment; Canvas atan2(-0, 0) still leaves the major axis
                        // horizontal in that degenerate case.
                        normal = Vector2.up;
                        tangent = Vector2.right;
                    }
                    else
                    {
                        tangent.Normalize();
                        normal.Normalize();
                    }

                    var centre = points[pointIndex] - halfLayer + normal * data.w;
                    var reach = Mathf.Max(0.75f, data.y);
                    var notchHeight = notchHeights != null && notch < notchHeights.Length
                        ? Mathf.Max(0.5f, notchHeights[notch])
                        : Mathf.Max(0.5f, _arenaNearFilamentPointWidths[slot][pointIndex] * 0.24f);
                    var extent = Mathf.Sqrt(reach * reach + notchHeight * notchHeight) + 1f;
                    var minX = Mathf.Max(
                        0,
                        Mathf.FloorToInt(((centre.x - extent) / width + 0.5f) * maskWidth) - 1);
                    var maxX = Mathf.Min(
                        maskWidth - 1,
                        Mathf.CeilToInt(((centre.x + extent) / width + 0.5f) * maskWidth) + 1);
                    var minY = Mathf.Max(
                        0,
                        Mathf.FloorToInt(((centre.y - extent) / height + 0.5f) * maskHeight) - 1);
                    var maxY = Mathf.Min(
                        maskHeight - 1,
                        Mathf.CeilToInt(((centre.y + extent) / height + 0.5f) * maskHeight) + 1);
                    var cut = Mathf.Clamp01(data.z);
                    for (var y = minY; y <= maxY; y++)
                    {
                        for (var x = minX; x <= maxX; x++)
                        {
                            var coverage = 0f;
                            for (var sampleY = 0; sampleY < 4; sampleY++)
                            {
                                var localY = ((y + (sampleY + 0.5f) * 0.25f) / maskHeight) * height - height * 0.5f;
                                for (var sampleX = 0; sampleX < 4; sampleX++)
                                {
                                    var localX = ((x + (sampleX + 0.5f) * 0.25f) / maskWidth) * width - width * 0.5f;
                                    var delta = new Vector2(localX, localY) - centre;
                                    var along = Vector2.Dot(delta, tangent) / reach;
                                    var lateral = Vector2.Dot(delta, normal) / notchHeight;
                                    if (along * along + lateral * lateral < 1f)
                                        coverage += 0.0625f;
                                }
                            }

                            if (coverage > 0f)
                            {
                                var pixel = y * maskWidth + x;
                                // Every notch from every filament in the layer
                                // cuts the same isolated plate, matching the
                                // browser's destination-out order at overlaps.
                                factors[pixel] *= 1f - cut * coverage;
                            }
                        }
                    }
                }
            }

            var pixels = new Color32[factors.Length];
            for (var pixel = 0; pixel < factors.Length; pixel++)
            {
                var alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(factors[pixel]) * 255f);
                pixels[pixel] = new Color32(255, 255, 255, alpha);
            }

            if (_arenaFilamentGroupNotchMasks[group] != null)
                Destroy(_arenaFilamentGroupNotchMasks[group]);
            if (!hasFilament)
            {
                _arenaFilamentGroupNotchMasks[group] = null;
                for (var slot = Mathf.Max(0, startSlot); slot < endSlot; slot++)
                {
                    _arenaNearFilamentNotchMasks[slot] = null;
                    var material = _arenaNearFilamentMaterials[slot];
                    if (material != null) material.SetTexture("_MaskTex", null);
                }
                return;
            }

            var mask = new Texture2D(maskWidth, maskHeight, TextureFormat.RGBA32, false)
            {
                name = "VoidFall Filament Group Notch Mask " + group,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            mask.SetPixels32(pixels);
            mask.Apply(false, true);
            _arenaFilamentGroupNotchMasks[group] = mask;
            for (var slot = Mathf.Max(0, startSlot); slot < endSlot; slot++)
            {
                _arenaNearFilamentNotchMasks[slot] = mask;
                var material = _arenaNearFilamentMaterials[slot];
                if (material != null) material.SetTexture("_MaskTex", mask);
            }
        }

        private void BuildArenaNearFilamentBand(int slot, float width, float height)
        {
            var points = _arenaNearFilamentPoints[slot];
            var filter = _arenaNearFilamentOuterViews[slot];
            if (points == null || filter == null || filter.sharedMesh == null) return;

            var halfLayer = new Vector2(width * 0.5f, height * 0.5f);
            var vertices = new Vector3[points.Length * 2 * ArenaNearFilamentPasses];
            var uvs = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            var triangles = new int[(points.Length - 1) * 6 * ArenaNearFilamentPasses];
            for (var pass = 0; pass < ArenaNearFilamentPasses; pass++)
            {
                var spread = 1.6f - pass * 0.13f;
                for (var point = 0; point < points.Length; point++)
                {
                    var normal = NearFilamentNormal(points, point);
                    var half = _arenaNearFilamentPointWidths[slot][point] * spread * 0.5f;
                    var centre = points[point] - halfLayer;
                    var vertex = (pass * points.Length + point) * 2;
                    vertices[vertex] = new Vector3(centre.x + normal.x * half, centre.y + normal.y * half, 0);
                    vertices[vertex + 1] = new Vector3(centre.x - normal.x * half, centre.y - normal.y * half, 0);
                    uvs[vertex] = FilamentMaskUv(vertices[vertex], width, height);
                    uvs[vertex + 1] = FilamentMaskUv(vertices[vertex + 1], width, height);
                    colors[vertex] = Color.white;
                    colors[vertex + 1] = Color.white;
                    if (point >= points.Length - 1) continue;
                    var triangle = (pass * (points.Length - 1) + point) * 6;
                    triangles[triangle] = vertex;
                    triangles[triangle + 1] = vertex + 2;
                    triangles[triangle + 2] = vertex + 1;
                    triangles[triangle + 3] = vertex + 1;
                    triangles[triangle + 4] = vertex + 2;
                    triangles[triangle + 5] = vertex + 3;
                }
            }

            var mesh = filter.sharedMesh;
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            _arenaNearFilamentBandVertices[slot] = vertices;
            _arenaNearFilamentBandColors[slot] = colors;
            BuildArenaNearFilamentStrand(slot, width, height);
        }

        private static Vector2 FilamentMaskUv(Vector3 localPosition, float width, float height)
        {
            return new Vector2(
                localPosition.x / Mathf.Max(1f, width) + 0.5f,
                localPosition.y / Mathf.Max(1f, height) + 0.5f);
        }

        private void BuildArenaNearFilamentStrand(int slot, float width, float height)
        {
            var points = _arenaNearFilamentPoints[slot];
            var filter = _arenaNearFilamentStrandViews[slot];
            if (points == null || filter == null || filter.sharedMesh == null) return;

            var strandFrom = Mathf.Clamp(_arenaNearFilamentStrandFrom[slot], 0, points.Length - 1);
            var strandTo = Mathf.Clamp(_arenaNearFilamentStrandTo[slot], strandFrom, points.Length);
            var strandCount = strandTo - strandFrom;
            if (strandCount < 4) return;

            var halfLayer = new Vector2(width * 0.5f, height * 0.5f);
            var vertices = new Vector3[strandCount * 2 * ArenaNearStrandPasses];
            var colors = new Color[vertices.Length];
            var triangles = new int[(strandCount - 1) * 6 * ArenaNearStrandPasses];
            var shift = _arenaNearFilamentStrandShifts[slot];
            for (var pass = 0; pass < ArenaNearStrandPasses; pass++)
            {
                var spread = 0.86f - pass * 0.094f;
                for (var localPoint = 0; localPoint < strandCount; localPoint++)
                {
                    var point = strandFrom + localPoint;
                    var normal = NearFilamentNormal(points, point);
                    var taper = Mathf.Sin(((localPoint + 0.5f) / strandCount) * Mathf.PI);
                    var half = _arenaNearFilamentPointWidths[slot][point] * taper * spread * 0.5f;
                    var centre = points[point] - halfLayer + normal * (_arenaNearFilamentPointWidths[slot][point] * shift);
                    var vertex = (pass * strandCount + localPoint) * 2;
                    vertices[vertex] = new Vector3(centre.x + normal.x * half, centre.y + normal.y * half, 0);
                    vertices[vertex + 1] = new Vector3(centre.x - normal.x * half, centre.y - normal.y * half, 0);
                    colors[vertex] = Color.white;
                    colors[vertex + 1] = Color.white;
                    if (localPoint >= strandCount - 1) continue;
                    var triangle = (pass * (strandCount - 1) + localPoint) * 6;
                    triangles[triangle] = vertex;
                    triangles[triangle + 1] = vertex + 2;
                    triangles[triangle + 2] = vertex + 1;
                    triangles[triangle + 3] = vertex + 1;
                    triangles[triangle + 4] = vertex + 2;
                    triangles[triangle + 5] = vertex + 3;
                }
            }

            var mesh = filter.sharedMesh;
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            _arenaNearFilamentStrandVertices[slot] = vertices;
            _arenaNearFilamentStrandColors[slot] = colors;
        }

        private static Vector2 NearFilamentNormal(Vector2[] points, int index)
        {
            var previous = points[Mathf.Max(0, index - 1)];
            var next = points[Mathf.Min(points.Length - 1, index + 1)];
            var normal = new Vector2(-(next.y - previous.y), next.x - previous.x);
            // Browser centrelineNormal() uses Math.hypot(...) || 1: exact
            // zero returns a zero normal, while every non-zero segment,
            // including a tiny one, is normalized.
            var length = normal.magnitude;
            return normal / (length > 0f ? length : 1f);
        }

        private static float NearFilamentNotchFactor(
            int pointIndex,
            float lateral,
            float pointWidth,
            Vector4[] notches,
            float[] notchHeights,
            float pointSpacing)
        {
            if (notches == null || notches.Length == 0) return 1f;
            var factor = 1f;
            var spacing = Mathf.Max(1f, pointSpacing);
            for (var notch = 0; notch < notches.Length; notch++)
            {
                var data = notches[notch];
                var height = notchHeights != null && notch < notchHeights.Length
                    ? Mathf.Max(0.5f, notchHeights[notch])
                    : Mathf.Max(0.5f, pointWidth * 0.24f);
                // Source paintFilaments() erases a rotated ellipse from the
                // isolated gas plate. Evaluate that ellipse in centreline
                // distance/lateral space instead of using independent linear
                // profiles; the mesh then interpolates the same binary
                // inside/outside coverage across its ribbon triangles.
                var along = (pointIndex - data.x) * spacing;
                var normalizedAlong = along / Mathf.Max(0.75f, data.y);
                var normalizedLateral = (lateral - data.w) / height;
                var ellipseDistanceSquared = normalizedAlong * normalizedAlong +
                    normalizedLateral * normalizedLateral;
                if (ellipseDistanceSquared < 1f)
                    factor *= 1f - Mathf.Clamp01(data.z);
            }
            return Mathf.Clamp01(factor);
        }

        private static float NearFilamentPassAlpha(float peak, float notchFactor, int passCount)
        {
            // The browser first stacks the band to `peak`, then destination-out
            // multiplies that coverage by the notch factor. Solve the inverse
            // stacking equation for each Unity mesh pass so the final alpha is
            // peak * notchFactor rather than a different exponent-shaped value.
            var target = Mathf.Clamp01(peak) * Mathf.Clamp01(notchFactor);
            return 1f - Mathf.Pow(1f - target, 1f / Mathf.Max(1, passCount));
        }

        private float ArenaOrbitPhase()
        {
            var rate = _mainMenuBrowsing ? MenuOrbitPhaseRate : GameplayOrbitPhaseRate;
            return _arenaDecorClock * rate;
        }

        private float ArenaRingPhase()
        {
            var rate = _mainMenuBrowsing ? MenuRingPhaseRate : GameplayRingPhaseRate;
            return _arenaDecorClock * rate;
        }

        private double ArenaCycleElapsedSeconds()
        {
            // Gameplay cycles follow simulation time. The paused menu uses the
            // render clock so browsing an arena previews the same loop without
            // advancing a saved run.
            return _mainMenuBrowsing ? _arenaDecorClock * MenuCyclePreviewRate : _time;
        }

        private static ArenaCycleVisualState ArenaCycleVisual(string cycleId, float progress)
        {
            var eased = Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI);
            switch (cycleId)
            {
                case "steady":
                    return new ArenaCycleVisualState
                    {
                        Definition = 0.22f, Current = 0.03f, Rim = 0.24f, Density = 0.78f, EdgeBias = 0,
                    };
                case "drift":
                    return new ArenaCycleVisualState
                    {
                        Definition = 0.34f, Current = 0.2f, Rim = 0.32f, Density = 0.88f, EdgeBias = 0.05f,
                    };
                case "eclipse":
                    return new ArenaCycleVisualState
                    {
                        Definition = 0.52f, Current = 0.45f, Rim = 0.48f, Density = 0.94f, EdgeBias = 0.14f,
                    };
                case "rupture":
                    return new ArenaCycleVisualState
                    {
                        Definition = 0.78f, Current = 0.72f, Rim = 0.66f + eased * 0.25f,
                        Density = 1f, EdgeBias = 0.28f,
                    };
                case "quiet":
                    return new ArenaCycleVisualState
                    {
                        Definition = 0.35f, Current = 0.1f, Rim = 0.35f, Density = 0.8f, EdgeBias = 0,
                    };
                case "ionized":
                    return new ArenaCycleVisualState
                    {
                        Definition = 0.9f, Current = 0.85f, Rim = 0.5f, Density = 1f, EdgeBias = 0.15f,
                    };
                case "flare":
                    return new ArenaCycleVisualState
                    {
                        Definition = 0.6f, Current = 0.45f, Rim = 0.55f + eased * 0.45f,
                        Density = 1f, EdgeBias = 0.1f,
                    };
                case "corona":
                    return new ArenaCycleVisualState
                    {
                        Definition = 0.72f, Current = 0.68f, Rim = 0.65f + eased * 0.35f,
                        Density = 1f, EdgeBias = 0.24f,
                    };
                case "still":
                    return new ArenaCycleVisualState
                    {
                        Definition = 0.3f, Current = 0.08f, Rim = 0.4f, Density = 0.7f, EdgeBias = 0,
                    };
                case "cross":
                    return new ArenaCycleVisualState
                    {
                        Definition = 0.55f, Current = 0.9f, Rim = 0.45f, Density = 0.95f, EdgeBias = 0.2f,
                    };
                case "bloom":
                    return new ArenaCycleVisualState
                    {
                        Definition = 0.45f, Current = 0.4f, Rim = 0.5f, Density = 1f,
                        EdgeBias = 0.55f + eased * 0.45f,
                    };
                case "afterglow":
                    return new ArenaCycleVisualState
                    {
                        Definition = 0.66f, Current = 0.62f, Rim = 0.58f + eased * 0.32f,
                        Density = 0.94f, EdgeBias = 0.38f,
                    };
                case "dormant":
                    return new ArenaCycleVisualState
                    {
                        Definition = 0.42f, Current = 0.12f, Rim = 0.48f, Density = 0.82f, EdgeBias = 0.08f,
                    };
                case "breathing":
                    return new ArenaCycleVisualState
                    {
                        Definition = 0.58f, Current = 0.38f, Rim = 0.62f + eased * 0.12f,
                        Density = 0.92f, EdgeBias = 0.18f,
                    };
                case "hostile":
                    return new ArenaCycleVisualState
                    {
                        Definition = 0.76f, Current = 0.68f, Rim = 0.78f + eased * 0.18f,
                        Density = 1f, EdgeBias = 0.32f,
                    };
                default:
                    return new ArenaCycleVisualState
                    {
                        Definition = 0.4f, Current = 0.2f, Rim = 0.4f, Density = 1f, EdgeBias = 0,
                    };
            }
        }

        private static float ArenaCycleDefinition(string cycleId, float progress)
        {
            return ArenaCycleVisual(cycleId, progress).Definition;
        }

        private static float ArenaMoteAngle(ArenaId arena)
        {
            switch (arena)
            {
                case ArenaId.RedNebula: return -0.5f;
                case ArenaId.WhiteSakura: return 0.72f;
                case ArenaId.Hydra: return -0.18f;
                default: return 0;
            }
        }

        private static float ArenaMoteSpeed(ArenaId arena)
        {
            switch (arena)
            {
                case ArenaId.RedNebula: return 13f;
                case ArenaId.WhiteSakura: return 17f;
                case ArenaId.Hydra: return 11f;
                default: return 0;
            }
        }

        private static float ArenaCycleFlashRate(string arenaId, string cycleId)
        {
            var arena = FindArena(arenaId);
            if (arena?.Cycles == null) return 0;
            for (var index = 0; index < arena.Cycles.Length; index++)
            {
                if (arena.Cycles[index].Id == cycleId) return (float)arena.Cycles[index].FlashRate;
            }
            return 0;
        }

        private static float ArenaLightAngle(ArenaId arena)
        {
            switch (arena)
            {
                case ArenaId.RedNebula: return Mathf.PI * 0.86f;
                case ArenaId.WhiteSakura: return -0.72f;
                case ArenaId.Hydra: return -0.35f;
                default: return 0;
            }
        }

        private static float StellarRimRipple(float t)
        {
            return 0.55f + 0.45f * NearFbm(t * 5.5f, 3.1f, 0x7c1f + 5, 3);
        }

        private Vector2 ArenaScreenPoint(float x, float y)
        {
            var viewportHalf = RenderViewportHalfExtent();
            return new Vector2(
                (x - 0.5f) * viewportHalf.x * 2f,
                (0.5f - y) * viewportHalf.y * 2f);
        }

        private Vector2 ArenaParallaxOffsetForViewport(Vector2 camera, float rate, float overscan)
        {
            var viewportHalf = RenderViewportHalfExtent();
            var slackX = viewportHalf.x * (overscan - 1f);
            var slackY = viewportHalf.y * (overscan - 1f);
            return new Vector2(
                ParallaxOffset(camera.x, rate, slackX),
                ParallaxOffset(camera.y, rate, slackY));
        }

        private static Vector2 ArenaParallaxOffset(Vector2 camera, float rate, float overscan)
        {
            var slackX = WorldHalfWidth * (overscan - 1f);
            var slackY = WorldHalfHeight * (overscan - 1f);
            return new Vector2(
                ParallaxOffset(camera.x, rate, slackX),
                ParallaxOffset(camera.y, rate, slackY));
        }

        private static float ParallaxOffset(float camera, float rate, float slack)
        {
            if (slack <= 0.0001f) return 0;
            return slack * (float)Math.Tanh(camera * rate / slack);
        }

        private static float StableArenaValue(int index, int salt)
        {
            var value = Mathf.Sin((index + 1) * 12.9898f + salt * 78.233f) * 43758.5453f;
            return Mathf.Repeat(value, 1f);
        }

        private void SelectRecipeForCurrentArena()
        {
            _arenaRecipeIndex = ArenaCatalogRules.RecipeIndex(
                _runSeed,
                ArenaCatalogRules.StableId(_arenaId));
            _arenaMoteSeedsReady = false;
            _arenaRockSeedsReady = false;
            _arenaFarFilamentSeedsReady = false;
        }

        private ArenaPackageKey ArenaPackageFor(ArenaId arena)
        {
            var stableId = ArenaCatalogRules.StableId(arena);
            return new ArenaPackageKey(
                stableId,
                ArenaCatalogRules.RecipeIndex(_runSeed, stableId));
        }

        private void PrepareArenaNeighborhood()
        {
            if (_arenaResidency == null) return;
            var current = ArenaPackageFor(_arenaId);
            var exitA = default(ArenaPackageKey);
            var exitB = default(ArenaPackageKey);
            var exitCount = 0;
            var arenas = ContentOrder.PreparedArenas;
            for (var index = 0; index < arenas.Length && exitCount < 2; index++)
            {
                if (arenas[index] == _arenaId) continue;
                if (exitCount++ == 0) exitA = ArenaPackageFor(arenas[index]);
                else exitB = ArenaPackageFor(arenas[index]);
            }
            ReconcileArenaResidency(ArenaResidencyPlanner.Steady(current, exitA, exitB));
        }

        private void PrepareMenuArenaCatalogue()
        {
            if (_arenaResidency == null) return;
            var arenas = ContentOrder.PreparedArenas;
            var packages = new ArenaPackageKey[arenas.Length];
            for (var index = 0; index < arenas.Length; index++)
                packages[index] = ArenaPackageFor(arenas[index]);
            ReconcileArenaResidency(ArenaResidencyPlanner.MenuCatalogue(packages));
        }

        private void ReconcileArenaResidency(ArenaResidentSet target)
        {
            if (!_arenaResidency.Reconcile(target))
                Debug.LogWarning(_arenaResidency.LastFailure);
            for (var index = 0; index < _preparedArenaPlateKeys.Length; index++)
            {
                var installed = _preparedArenaPlateKeys[index];
                if (!installed.IsValid || target.Contains(installed)) continue;
                _preparedArenaPlateAssets[index] = null;
                _preparedArenaPlateKeys[index] = default;
                _arenaPlateSprites[index] = null;
                _arenaPlateDetailSprites[index] = null;
            }
        }

        private static string ArenaIdName(ArenaId arena)
        {
            switch (arena)
            {
                case ArenaId.RedNebula: return "redNebula";
                case ArenaId.WhiteSakura: return "whiteSakura";
                case ArenaId.Hydra: return "hydra";
                case ArenaId.MonochromeCourt: return "monochrome-court";
                default: return "void";
            }
        }

        private static ArenaId ArenaIdFromName(string id)
        {
            switch (id)
            {
                case "redNebula": return ArenaId.RedNebula;
                case "whiteSakura": return ArenaId.WhiteSakura;
                case "hydra": return ArenaId.Hydra;
                case "monochrome-court": return ArenaId.MonochromeCourt;
                default: return ArenaId.Void;
            }
        }

        private static string ArenaName(ArenaId arena)
        {
            switch (arena)
            {
                case ArenaId.RedNebula: return "Red Nebula";
                case ArenaId.WhiteSakura: return "White Sakura";
                case ArenaId.Hydra: return "Hydra";
                case ArenaId.MonochromeCourt: return "Monochrome Court";
                default: return "Abyss";
            }
        }

        private static ArenaDefinition FindArena(string id)
        {
            foreach (var definition in ContentCatalog.Arenas) if (definition.Id == id) return definition;
            return HydraContent.FindArena(id) ?? MonochromeContent.FindArena(id);
        }
    }
}
