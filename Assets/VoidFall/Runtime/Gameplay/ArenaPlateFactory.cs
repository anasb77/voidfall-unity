using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    /// <summary>
    /// Bakes the browser arena field into one bounded deterministic texture per
    /// arena. It keeps noise out of the frame loop while preserving the source
    /// palette, off-centre falloff, cloud tint, and sweeps. Grain is emitted as
    /// a separate tiled sprite because the browser anchors that pass to the
    /// screen after drawing the moving backdrop.
    /// 
    /// Callers are responsible for calling <see cref="Cleanup"/> to release created native objects.
    /// </summary>
    public static class ArenaPlateFactory
    {
        private static readonly System.Collections.Generic.List<Texture2D> _createdTextures = new System.Collections.Generic.List<Texture2D>();
        private static readonly System.Collections.Generic.List<Sprite> _createdSprites = new System.Collections.Generic.List<Sprite>();

        /// <summary>
        /// Cleans up all tracked native textures and sprites. Callers must invoke this when they are done with the created assets.
        /// </summary>
        public static void Cleanup()
        {
            foreach (var sprite in _createdSprites)
            {
                if (sprite != null) UnityEngine.Object.Destroy(sprite);
            }
            _createdSprites.Clear();

            foreach (var texture in _createdTextures)
            {
                if (texture != null) UnityEngine.Object.Destroy(texture);
            }
            _createdTextures.Clear();
        }

        // At the browser's 1600x900 high-quality reference, bakeSize caps the
        // viewport at 1366x769 and buildArenaLayers applies the 1.18x sky
        // overscan before drawing. Keep that same overscanned backing size so
        // the field/cloud plate is not downsampled before it reaches the
        // screen.
        public const int DefaultWidth = 1612;
        public const int DefaultHeight = 907;
        private const int MaxBakePixels = 1_600_000;
        private const float SkyOverscan = 1.18f;

        /// <summary>
        /// Returns source-equivalent sky backing dimensions for a live
        /// viewport and quality detail level. Unity uses the source DPR=1
        /// path here, then applies the capped bake and sky overscan.
        /// </summary>
        public static Vector2Int SkyBakeDimensions(int viewportWidth, int viewportHeight, int detail)
        {
            var safeWidth = Mathf.Max(64, viewportWidth);
            var safeHeight = Mathf.Max(64, viewportHeight);
            var quality = Mathf.Clamp(detail, 0, 2);
            var scale = quality == 0 ? 0.7f : quality == 1 ? 0.85f : 1f;
            var pixels = safeWidth * safeHeight * scale * scale;
            if (pixels > MaxBakePixels)
                scale *= Mathf.Sqrt(MaxBakePixels / pixels);

            var bakedWidth = Mathf.Max(48, Mathf.RoundToInt(safeWidth * scale));
            var bakedHeight = Mathf.Max(48, Mathf.RoundToInt(safeHeight * scale));
            return new Vector2Int(
                Mathf.Max(64, Mathf.RoundToInt(bakedWidth * SkyOverscan)),
                Mathf.Max(36, Mathf.RoundToInt(bakedHeight * SkyOverscan)));
        }

        private sealed class VisualSpec
        {
            public Color FieldCentre;
            public Color FieldMid;
            public Color FieldOuter;
            public float BiasX;
            public float BiasY;
            public int NoiseSeed;
            public float Clouding;
            public Color CloudTint;
            public float CloudAnisotropy;
            public Color SweepTint;
            public float Grain;
            public Vector3 GrainTint;
            public float FilamentAngle;
            public int FilamentCount;
            public Color[] FilamentColors;
            public bool Pale;
            public float FarRockAlpha;
            public Color FarRockBody;
            public Color FarRockRim;
            public float LandmarkLightAngle;
        }

        private static readonly float[][] BakedRockOutlines =
        {
            new[] { 1f, 0.97f, 0.72f, 0.86f, 0.84f, 1.02f, 0.66f, 0.9f, 0.93f },
            new[] { 0.9f, 0.92f, 1.04f, 0.79f, 0.62f, 0.83f, 0.86f, 1f, 0.74f, 0.88f },
            new[] { 1.03f, 1f, 0.81f, 0.58f, 0.87f, 0.9f, 0.89f, 0.7f, 0.95f, 0.98f, 0.76f },
            new[] { 0.84f, 0.98f, 0.95f, 1.05f, 0.68f, 0.8f, 0.79f, 0.63f, 0.91f },
            new[] { 0.96f, 0.7f, 0.88f, 0.9f, 1.01f, 0.98f, 0.6f, 0.82f, 0.85f, 0.94f, 0.72f, 0.9f },
            new[] { 1f, 0.86f, 0.84f, 0.66f, 0.94f, 1.03f, 0.75f, 0.71f, 0.89f, 0.92f },
        };

        private struct Stream
        {
            private uint _state;

            public Stream(uint seed)
            {
                _state = seed == 0 ? 0x9e3779b9u : seed;
            }

            public float Next()
            {
                _state += 0x6d2b79f5u;
                var value = _state;
                value = (value ^ (value >> 15)) * (value | 1u);
                value ^= value + ((value ^ (value >> 7)) * (value | 61u));
                return (value ^ (value >> 14)) / 4294967296f;
            }
        }

        public static Sprite Create(
            ArenaId arena,
            int width = DefaultWidth,
            int height = DefaultHeight)
        {
            return CreatePlate(arena, width, height, true);
        }

        public static Sprite CreateBase(
            ArenaId arena,
            int width = DefaultWidth,
            int height = DefaultHeight)
        {
            return CreatePlate(arena, width, height, false);
        }

        public static Sprite CreateBakedDetails(
            ArenaId arena,
            int width = DefaultWidth,
            int height = DefaultHeight)
        {
            width = Mathf.Max(64, width);
            height = Mathf.Max(36, height);
            return SpriteFromPixels(
                BuildDetailPixels(arena, width, height),
                width,
                height,
                "VoidFall Arena Baked Details " + arena);
        }

        /// <summary>
        /// Pure pixel work: safe to call from a background thread once
        /// <see cref="WarmSpecs"/> has run. Pair with <see cref="SpriteFromPixels"/>
        /// on the main thread to publish the result.
        /// </summary>
        public static Color32[] BuildDetailPixels(ArenaId arena, int width, int height)
        {
            width = Mathf.Max(64, width);
            height = Mathf.Max(36, height);
            if (arena == ArenaId.MonochromeCourt)
                return BuildMonochromeDetailPixels(width, height);
            var pixels = new Color32[width * height];
            var spec = SpecFor(arena);
            PaintBakedEdgeRocks(pixels, width, height, spec);
            PaintBakedPetals(pixels, width, height, spec);
            if (arena == ArenaId.Hydra) PaintHydraBoneStructure(pixels, width, height);
            return pixels;
        }

        /// <summary>
        /// Pure pixel work for the arena field plate. Safe to call from a
        /// background thread once <see cref="WarmSpecs"/> has run.
        /// </summary>
        public static Color32[] BuildBasePixels(ArenaId arena, int width, int height)
        {
            return BuildPlatePixels(
                arena,
                Mathf.Max(64, width),
                Mathf.Max(36, height),
                false);
        }

        /// <summary>
        /// Main-thread only: allocates the texture and uploads.
        /// </summary>
        public static Sprite SpriteFromPixels(Color32[] pixels, int width, int height, string name)
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 1f);
            sprite.name = texture.name + " Sprite";
            _createdTextures.Add(texture);
            _createdSprites.Add(sprite);
            return sprite;
        }

        private static Sprite CreatePlate(
            ArenaId arena,
            int width,
            int height,
            bool includeBakedDetails)
        {
            width = Mathf.Max(64, width);
            height = Mathf.Max(36, height);
            return SpriteFromPixels(
                BuildPlatePixels(arena, width, height, includeBakedDetails),
                width,
                height,
                "VoidFall Arena Plate " + (includeBakedDetails ? "" : "Base ") + arena);
        }

        private static Color32[] BuildPlatePixels(
            ArenaId arena,
            int width,
            int height,
            bool includeBakedDetails)
        {
            if (arena == ArenaId.MonochromeCourt)
            {
                var monochrome = BuildMonochromeBasePixels(width, height);
                if (includeBakedDetails)
                {
                    var details = BuildMonochromeDetailPixels(width, height);
                    for (var index = 0; index < monochrome.Length; index++)
                        monochrome[index] = Blend(monochrome[index], details[index], details[index].a / 255f);
                }
                return monochrome;
            }
            var spec = SpecFor(arena);
            var pixels = new Color32[width * height];
            // paintField seeds its three value sweeps from the arena noise
            // seed, independently of the gameplay run seed.
            var stream = new Stream((uint)(spec.NoiseSeed ^ 0x51ed));
            var sweepAngles = new float[3];
            var sweepThickness = new float[3];
            var sweepOffsets = new float[3];
            var sweepAlpha = new float[3];
            for (var sweep = 0; sweep < 3; sweep++)
            {
                sweepAngles[sweep] = -0.5f + stream.Next() * 1.2f;
                sweepThickness[sweep] = 0.18f + stream.Next() * 0.4f;
                sweepOffsets[sweep] = (stream.Next() - 0.35f) * 1.0f;
                // The browser serializes this stop through toFixed(3) before
                // Canvas parses the rgba() string.
                sweepAlpha[sweep] = QuantizeCanvasGradientStop(
                    0.1f + stream.Next() * 0.12f);
            }

            // The browser paints clouding into a small image (one fifth of
            // the plate) and lets canvas smoothing enlarge it. Sampling FBM
            // at every final pixel makes Unity's field too crisp and busy.
            var cloudWidth = Mathf.Max(12, Mathf.RoundToInt(width / 5f));
            var cloudHeight = Mathf.Max(12, Mathf.RoundToInt(height / 5f));
            var cloudValues = new float[cloudWidth * cloudHeight];
            if (spec.Clouding > 0)
            {
                var axis = spec.FilamentCount > 0 ? spec.FilamentAngle : -0.5f;
                var axisCosine = Mathf.Cos(axis);
                var axisSine = Mathf.Sin(axis);
                var acrossScale = 9.5f;
                var alongScale = acrossScale / Mathf.Max(1f, spec.CloudAnisotropy);
                for (var cloudY = 0; cloudY < cloudHeight; cloudY++)
                {
                    var fy = cloudY / (float)cloudHeight;
                    for (var cloudX = 0; cloudX < cloudWidth; cloudX++)
                    {
                        var fx = cloudX / (float)cloudWidth;
                        var u = fx * axisCosine + fy * axisSine;
                        var v = -fx * axisSine + fy * axisCosine;
                        var noise = Fbm(
                            u * alongScale + 3.1f,
                            v * acrossScale,
                            spec.NoiseSeed,
                            4);
                        var ridged = 1f - Mathf.Abs(noise - 0.5f) * 2f;
                        var raw = noise * 0.42f + ridged * 0.58f;
                        var t = Mathf.Clamp01((raw - 0.36f) / 0.4f);
                        cloudValues[cloudY * cloudWidth + cloudX] = t * t * (3f - 2f * t);
                    }
                }
            }

            for (var y = 0; y < height; y++)
            {
                var v = (y + 0.5f) / height;
                for (var x = 0; x < width; x++)
                {
                    var u = (x + 0.5f) / width;
                    // Match paintField's translated/scaled radial gradient:
                    // its radius is 0.72 times the plate diagonal and the
                    // first stop deliberately sits at 5%, leaving a calm
                    // centre instead of a pinprick.
                    var dx = (u - spec.BiasX) * width;
                    var dy = (v - spec.BiasY) * height / 0.82f;
                    var reach = Mathf.Sqrt(width * width + height * height) * 0.72f;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy) / Mathf.Max(0.0001f, reach);
                    var field = distance <= 0.05f
                        ? spec.FieldCentre
                        : distance <= 0.54f
                            ? Color.Lerp(spec.FieldCentre, spec.FieldMid, (distance - 0.05f) / 0.49f)
                            : Color.Lerp(spec.FieldMid, spec.FieldOuter, Mathf.Clamp01((distance - 0.54f) / 0.46f));

                    var cloudRamp = SampleCloud(cloudValues, cloudWidth, cloudHeight, u, v);
                    // paintClouding writes ImageData alpha as a rounded
                    // 8-bit byte before drawImage performs the enlargement.
                    var cloudAlpha = QuantizeCanvasAlphaByte(
                        cloudRamp * (spec.Pale ? 132f : 150f) * spec.Clouding / 255f);
                    field = Blend(field, spec.CloudTint, cloudAlpha);

                    for (var sweep = 0; sweep < 3; sweep++)
                    {
                        var sweepCos = Mathf.Cos(sweepAngles[sweep]);
                        var sweepSin = Mathf.Sin(sweepAngles[sweep]);
                        // paintField rotates a pixel-space Canvas2D rectangle.
                        // Keep x and y in their source pixel dimensions before
                        // projecting onto the sweep's local vertical axis;
                        // normalizing both axes makes angled bands too narrow
                        // on the 16:9 plate.
                        var across = SweepAcross(u, v, width, height, sweepSin, sweepCos, sweepOffsets[sweep]);
                        var band = Mathf.Clamp01(
                            1f - Mathf.Abs(across) /
                            Mathf.Max(0.01f, height * sweepThickness[sweep]));
                        field = Blend(field, spec.SweepTint, band * sweepAlpha[sweep]);
                    }

                    pixels[y * width + x] = field;
                }
            }

            if (includeBakedDetails)
            {
                // The public combined factory remains useful for tests and
                // callers that need one browser-shaped sky image. Gameplay
                // uses CreateBase plus CreateBakedDetails so these passes can
                // sit after the landmark, matching buildArenaLayers order.
                PaintBakedEdgeRocks(pixels, width, height, spec);
                PaintBakedPetals(pixels, width, height, spec);
            }

            return pixels;
        }

        public static Sprite CreateGrainTile(ArenaId arena, int size = 256)
        {
            size = Mathf.Clamp(size, 16, 512);
            var spec = SpecFor(arena);
            var pixels = BuildGrainPixels(spec, size, size);
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "VoidFall Arena Grain " + arena,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            var sprite = Sprite.Create(
                texture,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                1f,
                0,
                SpriteMeshType.FullRect,
                Vector4.zero,
                false);
            sprite.name = texture.name + " Sprite";
            _createdTextures.Add(texture);
            _createdSprites.Add(sprite);
            return sprite;
        }

        public static float GrainStrength(ArenaId arena)
        {
            return SpecFor(arena).Grain;
        }

        private static void PaintBakedEdgeRocks(Color32[] pixels, int width, int height, VisualSpec spec)
        {
            if (spec.FarRockAlpha <= 0) return;

            var stream = new Stream((uint)(spec.NoiseSeed ^ 0x2c94));
            // paintBakedRocks receives the actual Canvas backing dimensions.
            // Keep its normalized edge anchors and radius in those same pixel
            // coordinates; a fixed 1510x850 design space shrinks or enlarges
            // the silhouettes when the live viewport changes.
            var sourceScaleX = 1f;
            var sourceScaleY = 1f;
            var shorter = Mathf.Min(width, height);
            for (var rock = 0; rock < 2; rock++)
            {
                var sourceCentreX = rock == 0 ? -shorter * 0.03f : width + shorter * 0.025f;
                var sourceCentreY = height *
                    (rock == 0 ? 0.18f + stream.Next() * 0.16f : 0.62f + stream.Next() * 0.2f);
                var radius = shorter * (0.07f + stream.Next() * 0.035f);
                var rotation = stream.Next() * Mathf.PI * 2f;
                var centre = new Vector2(sourceCentreX * sourceScaleX, sourceCentreY * sourceScaleY);
                PaintBakedRockFill(
                    pixels,
                    width,
                    height,
                    centre,
                    radius,
                    rotation,
                    sourceScaleX,
                    sourceScaleY,
                    BakedRockOutlines[(rock + 2) % BakedRockOutlines.Length],
                    spec.FarRockBody,
                    spec.FarRockAlpha);
                PaintBakedRockRim(
                    pixels,
                    width,
                    height,
                    centre,
                    radius,
                    sourceScaleX,
                    sourceScaleY,
                    spec.FarRockRim,
                    spec.LandmarkLightAngle);
            }
        }

        private static void PaintBakedRockFill(
            Color32[] pixels,
            int width,
            int height,
            Vector2 centre,
            float radius,
            float rotation,
            float scaleX,
            float scaleY,
            float[] outline,
            Color tint,
            float alpha)
        {
            var boundX = radius * 1.1f * scaleX;
            var boundY = radius * 1.1f * scaleY;
            var minX = Mathf.Max(0, Mathf.FloorToInt(centre.x - boundX - 1f));
            var maxX = Mathf.Min(width - 1, Mathf.CeilToInt(centre.x + boundX + 1f));
            var minY = Mathf.Max(0, Mathf.FloorToInt(centre.y - boundY - 1f));
            var maxY = Mathf.Min(height - 1, Mathf.CeilToInt(centre.y + boundY + 1f));
            var cosine = Mathf.Cos(rotation);
            var sine = Mathf.Sin(rotation);
            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var sourceX = (x + 0.5f - centre.x) / Mathf.Max(0.0001f, scaleX);
                    var sourceY = (y + 0.5f - centre.y) / Mathf.Max(0.0001f, scaleY);
                    var localX = sourceX * cosine + sourceY * sine;
                    var localY = -sourceX * sine + sourceY * cosine;
                    var coverage = RockCoverage(
                        new Vector2(localX, localY),
                        new Vector2(cosine / Mathf.Max(0.0001f, scaleX), -sine / Mathf.Max(0.0001f, scaleX)),
                        new Vector2(sine / Mathf.Max(0.0001f, scaleY), cosine / Mathf.Max(0.0001f, scaleY)),
                        radius,
                        outline);
                    if (coverage <= 0) continue;
                    BlendPixel(pixels, y * width + x, tint, alpha * coverage);
                }
            }
        }

        private static void PaintBakedRockRim(
            Color32[] pixels,
            int width,
            int height,
            Vector2 centre,
            float radius,
            float scaleX,
            float scaleY,
            Color tint,
            float lightAngle)
        {
            var stroke = Mathf.Max(1.2f, radius * 0.025f);
            var boundX = (radius + stroke) * scaleX;
            var boundY = (radius + stroke) * scaleY;
            var minX = Mathf.Max(0, Mathf.FloorToInt(centre.x - boundX - 1f));
            var maxX = Mathf.Min(width - 1, Mathf.CeilToInt(centre.x + boundX + 1f));
            var minY = Mathf.Max(0, Mathf.FloorToInt(centre.y - boundY - 1f));
            var maxY = Mathf.Min(height - 1, Mathf.CeilToInt(centre.y + boundY + 1f));
            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var sourceX = (x + 0.5f - centre.x) / Mathf.Max(0.0001f, scaleX);
                    var sourceY = (y + 0.5f - centre.y) / Mathf.Max(0.0001f, scaleY);
                    var coverage = RockRimCoverage(
                        new Vector2(sourceX, sourceY),
                        new Vector2(1f / Mathf.Max(0.0001f, scaleX), 0),
                        new Vector2(0, 1f / Mathf.Max(0.0001f, scaleY)),
                        radius,
                        stroke,
                        lightAngle);
                    if (coverage <= 0) continue;
                    BlendPixel(pixels, y * width + x, tint, 0.16f * coverage);
                }
            }
        }

        private static float RockRimCoverage(
            Vector2 centre,
            Vector2 pixelAxisX,
            Vector2 pixelAxisY,
            float radius,
            float stroke,
            float lightAngle)
        {
            const int samplesPerAxis = 4;
            var covered = 0;
            var targetRadius = radius * 0.94f;
            var halfWidth = stroke * 0.65f;
            for (var sampleY = 0; sampleY < samplesPerAxis; sampleY++)
            {
                var offsetY = (sampleY + 0.5f) / samplesPerAxis - 0.5f;
                for (var sampleX = 0; sampleX < samplesPerAxis; sampleX++)
                {
                    var offsetX = (sampleX + 0.5f) / samplesPerAxis - 0.5f;
                    var point = centre + pixelAxisX * offsetX + pixelAxisY * offsetY;
                    var distance = point.magnitude;
                    if (Mathf.Abs(distance - targetRadius) > halfWidth) continue;
                    var angle = Mathf.Atan2(point.y, point.x);
                    var delta = Mathf.Repeat(angle - lightAngle + Mathf.PI, Mathf.PI * 2f) - Mathf.PI;
                    if (Mathf.Abs(delta) <= 0.7f) covered++;
                }
            }
            return covered / (float)(samplesPerAxis * samplesPerAxis);
        }

        private static bool PointInsideRock(float x, float y, float radius, float[] outline)
        {
            var inside = false;
            for (int index = 0, previous = outline.Length - 1; index < outline.Length; previous = index++)
            {
                var angle = index / (float)outline.Length * Mathf.PI * 2f - Mathf.PI * 0.5f;
                var previousAngle = previous / (float)outline.Length * Mathf.PI * 2f - Mathf.PI * 0.5f;
                var pointX = Mathf.Cos(angle) * radius * outline[index];
                var pointY = Mathf.Sin(angle) * radius * outline[index];
                var previousX = Mathf.Cos(previousAngle) * radius * outline[previous];
                var previousY = Mathf.Sin(previousAngle) * radius * outline[previous];
                var crosses = (pointY > y) != (previousY > y) &&
                    x < (previousX - pointX) * (y - pointY) / (previousY - pointY) + pointX;
                if (crosses) inside = !inside;
            }
            return inside;
        }

        private static float RockCoverage(
            Vector2 centre,
            Vector2 pixelAxisX,
            Vector2 pixelAxisY,
            float radius,
            float[] outline)
        {
            const int samplesPerAxis = 4;
            var inside = 0;
            for (var sampleY = 0; sampleY < samplesPerAxis; sampleY++)
            {
                var offsetY = (sampleY + 0.5f) / samplesPerAxis - 0.5f;
                for (var sampleX = 0; sampleX < samplesPerAxis; sampleX++)
                {
                    var offsetX = (sampleX + 0.5f) / samplesPerAxis - 0.5f;
                    var point = centre + pixelAxisX * offsetX + pixelAxisY * offsetY;
                    if (PointInsideRock(point.x, point.y, radius, outline)) inside++;
                }
            }
            return inside / (float)(samplesPerAxis * samplesPerAxis);
        }

        private static void BlendPixel(Color32[] pixels, int index, Color tint, float alpha)
        {
            alpha = Mathf.Clamp01(alpha);
            var existing = pixels[index];
            var destinationAlpha = existing.a / 255f;
            var outputAlpha = alpha + destinationAlpha * (1f - alpha);
            if (outputAlpha <= 0f)
            {
                pixels[index] = new Color32(0, 0, 0, 0);
                return;
            }
            var destinationWeight = destinationAlpha * (1f - alpha);
            existing.r = (byte)Mathf.Clamp(Mathf.RoundToInt(
                (tint.r * alpha + existing.r / 255f * destinationWeight) / outputAlpha * 255f), 0, 255);
            existing.g = (byte)Mathf.Clamp(Mathf.RoundToInt(
                (tint.g * alpha + existing.g / 255f * destinationWeight) / outputAlpha * 255f), 0, 255);
            existing.b = (byte)Mathf.Clamp(Mathf.RoundToInt(
                (tint.b * alpha + existing.b / 255f * destinationWeight) / outputAlpha * 255f), 0, 255);
            existing.a = (byte)Mathf.Clamp(Mathf.RoundToInt(outputAlpha * 255f), 0, 255);
            pixels[index] = existing;
        }

        private static Color32[] BuildMonochromeBasePixels(int width, int height)
        {
            var pixels = new Color32[width * height];
            var ivory = Parse("#e5e3db");
            var ink = Parse("#0a0b0d");
            var shorter = Mathf.Min(width, height);
            for (var y = 0; y < height; y++)
            {
                var dy = y + 0.5f - height * 0.5f;
                for (var x = 0; x < width; x++)
                {
                    var dx = x + 0.5f - width * 0.5f;
                    var radius = Mathf.Sqrt(dx * dx + dy * dy);
                    var ring = Mathf.FloorToInt(radius / Mathf.Max(4f, shorter / 9f));
                    var sector = Mathf.FloorToInt((Mathf.Atan2(dy, dx) + Mathf.PI) /
                                                  (Mathf.PI * 2f / 24f));
                    var light = ((ring + sector) & 1) == 0;
                    var baseColor = light ? ivory : ink;
                    var scratch = Fbm(x * 0.035f, y * 0.11f, 0x6c31, 3) - 0.5f;
                    var vignette = Mathf.Clamp01(radius / Mathf.Max(1f, shorter * 0.72f));
                    var adjusted = new Color(
                        Mathf.Clamp01(baseColor.r + scratch * (light ? 0.11f : 0.045f)),
                        Mathf.Clamp01(baseColor.g + scratch * (light ? 0.105f : 0.045f)),
                        Mathf.Clamp01(baseColor.b + scratch * (light ? 0.095f : 0.05f)),
                        1f);
                    pixels[y * width + x] = Color.Lerp(adjusted, Parse("#030405"), vignette * 0.16f);
                }
            }
            return pixels;
        }

        private static Color32[] BuildMonochromeDetailPixels(int width, int height)
        {
            var pixels = new Color32[width * height];
            var gridX = Mathf.Max(1, width / 12);
            var gridY = Mathf.Max(1, height / 7);
            var lineWidth = Mathf.Max(1, Mathf.RoundToInt(Mathf.Min(width, height) * 0.004f));
            var seamWidth = Mathf.Max(2, Mathf.RoundToInt(width * 0.012f));
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var grid = x % gridX < lineWidth || y % gridY < lineWidth;
                    var wave = Mathf.Sin(y * 0.065f) * width * 0.012f +
                               Mathf.Sin(y * 0.017f + 1.4f) * width * 0.008f;
                    var seam = Mathf.Abs(x - (width * 0.5f + wave)) <= seamWidth;
                    var scratch = ((x * 31 + y * 17 + 11) % 211) < 2;
                    if (seam)
                        pixels[y * width + x] = new Color32(238, 238, 232, 186);
                    else if (grid)
                        pixels[y * width + x] = new Color32(126, 130, 132, 96);
                    else if (scratch)
                        pixels[y * width + x] = new Color32(224, 224, 218, 72);
                }
            }
            return pixels;
        }

        private static void PaintHydraBoneStructure(Color32[] pixels, int width, int height)
        {
            var shadow = Parse("#07120b");
            var bone = Parse("#d8d7b2");
            var marrow = Parse("#f4f0d2");
            var rim = Parse("#78ff5a");
            var outer = new[]
            {
                new Vector2(0.035f, 0.96f), new Vector2(0.075f, 0.66f),
                new Vector2(0.16f, 0.36f), new Vector2(0.31f, 0.12f),
                new Vector2(0.44f, 0.035f),
            };
            var inner = new[]
            {
                new Vector2(0.14f, 0.98f), new Vector2(0.18f, 0.68f),
                new Vector2(0.27f, 0.41f), new Vector2(0.39f, 0.16f),
                new Vector2(0.47f, 0.07f),
            };
            PaintHydraMirroredChain(pixels, width, height, outer, shadow, bone, marrow, rim, 0.019f);
            PaintHydraMirroredChain(pixels, width, height, inner, shadow, bone, marrow, rim, 0.015f);

            var shorter = Mathf.Min(width, height);
            for (var index = 0; index < 15; index++)
            {
                var t = index / 14f;
                var centre = new Vector2(width * 0.5f, height * Mathf.Lerp(0.08f, 0.92f, t));
                var radiusX = shorter * Mathf.Lerp(0.021f, 0.012f, t);
                var radiusY = shorter * Mathf.Lerp(0.014f, 0.009f, t);
                PaintHydraEllipse(pixels, width, height, centre, radiusX * 1.7f, radiusY * 1.8f, shadow, 0.78f);
                PaintHydraEllipse(pixels, width, height, centre, radiusX * 1.28f, radiusY * 1.35f, bone, 0.88f);
                PaintHydraEllipse(pixels, width, height, centre, radiusX, radiusY, marrow, 0.82f);
            }
        }

        private static void PaintHydraMirroredChain(
            Color32[] pixels,
            int width,
            int height,
            Vector2[] normalized,
            Color shadow,
            Color bone,
            Color marrow,
            Color rim,
            float normalizedWidth)
        {
            var shorter = Mathf.Min(width, height);
            for (var mirror = 0; mirror < 2; mirror++)
            {
                for (var index = 0; index < normalized.Length - 1; index++)
                {
                    var from = normalized[index];
                    var to = normalized[index + 1];
                    if (mirror == 1)
                    {
                        from.x = 1f - from.x;
                        to.x = 1f - to.x;
                    }
                    var a = new Vector2(from.x * width, from.y * height);
                    var b = new Vector2(to.x * width, to.y * height);
                    var radius = shorter * normalizedWidth;
                    PaintHydraCapsule(pixels, width, height, a, b, radius * 1.7f, shadow, 0.82f);
                    PaintHydraCapsule(pixels, width, height, a, b, radius * 1.25f, rim, 0.22f);
                    PaintHydraCapsule(pixels, width, height, a, b, radius, bone, 0.9f);
                    PaintHydraCapsule(pixels, width, height, a, b, radius * 0.5f, marrow, 0.72f);
                }
            }
        }

        private static void PaintHydraCapsule(
            Color32[] pixels,
            int width,
            int height,
            Vector2 a,
            Vector2 b,
            float radius,
            Color tint,
            float alpha)
        {
            var minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - radius - 1));
            var maxX = Mathf.Min(width - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + radius + 1));
            var minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - radius - 1));
            var maxY = Mathf.Min(height - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + radius + 1));
            var delta = b - a;
            var lengthSquared = Mathf.Max(0.0001f, delta.sqrMagnitude);
            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var point = new Vector2(x + 0.5f, y + 0.5f);
                    var t = Mathf.Clamp01(Vector2.Dot(point - a, delta) / lengthSquared);
                    var distance = Vector2.Distance(point, a + delta * t);
                    if (distance > radius + 0.75f) continue;
                    var coverage = Mathf.Clamp01(radius + 0.75f - distance);
                    BlendPixel(pixels, y * width + x, tint, alpha * coverage);
                }
            }
        }

        private static void PaintHydraEllipse(
            Color32[] pixels,
            int width,
            int height,
            Vector2 centre,
            float radiusX,
            float radiusY,
            Color tint,
            float alpha)
        {
            var minX = Mathf.Max(0, Mathf.FloorToInt(centre.x - radiusX - 1));
            var maxX = Mathf.Min(width - 1, Mathf.CeilToInt(centre.x + radiusX + 1));
            var minY = Mathf.Max(0, Mathf.FloorToInt(centre.y - radiusY - 1));
            var maxY = Mathf.Min(height - 1, Mathf.CeilToInt(centre.y + radiusY + 1));
            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var nx = (x + 0.5f - centre.x) / Mathf.Max(0.001f, radiusX);
                    var ny = (y + 0.5f - centre.y) / Mathf.Max(0.001f, radiusY);
                    var distance = nx * nx + ny * ny;
                    if (distance > 1.08f) continue;
                    BlendPixel(pixels, y * width + x, tint, alpha * Mathf.Clamp01((1.08f - distance) * 8f));
                }
            }
        }

        private static void PaintBakedPetals(Color32[] pixels, int width, int height, VisualSpec spec)
        {
            if (!spec.Pale) return;

            var stream = new Stream((uint)(spec.NoiseSeed ^ 0x6b1d));
            for (var petal = 0; petal < 34; petal++)
            {
                var centreX = stream.Next() * width;
                var centreY = stream.Next() * height;
                var size = 5f + stream.Next() * 9f;
                var alpha = 0.1f + stream.Next() * 0.16f;
                var tint = stream.Next() < 0.5f ? Parse("#e9c7d6") : Parse("#efe6dc");
                var angle = stream.Next() * Mathf.PI * 2f;
                var cosine = Mathf.Cos(angle);
                var sine = Mathf.Sin(angle);
                const int curveSteps = 12;
                var petalOutline = new Vector2[curveSteps * 2 + 1];
                var top = new Vector2(0, -size);
                var bottom = new Vector2(0, size);
                petalOutline[0] = top;
                for (var step = 1; step <= curveSteps; step++)
                {
                    var t = step / (float)curveSteps;
                    petalOutline[step] = CubicBezier(
                        top,
                        new Vector2(size * 0.62f, -size * 0.7f),
                        new Vector2(size * 0.74f, size * 0.1f),
                        bottom,
                        t);
                }
                for (var step = 1; step <= curveSteps; step++)
                {
                    var t = step / (float)curveSteps;
                    petalOutline[curveSteps + step] = CubicBezier(
                        bottom,
                        new Vector2(-size * 0.74f, size * 0.1f),
                        new Vector2(-size * 0.62f, -size * 0.7f),
                        top,
                        t);
                }
                // The browser's petal size is already in backing-store pixels;
                // it does not scale with the viewport dimensions.
                var radiusX = size * 0.74f;
                var radiusY = size;
                var minX = Mathf.Max(0, Mathf.FloorToInt(centreX - radiusX - 1f));
                var maxX = Mathf.Min(width - 1, Mathf.CeilToInt(centreX + radiusX + 1f));
                var minY = Mathf.Max(0, Mathf.FloorToInt(centreY - radiusY - 1f));
                var maxY = Mathf.Min(height - 1, Mathf.CeilToInt(centreY + radiusY + 1f));
                for (var y = minY; y <= maxY; y++)
                {
                    for (var x = minX; x <= maxX; x++)
                    {
                        var dx = x + 0.5f - centreX;
                        var dy = y + 0.5f - centreY;
                        var localX = dx * cosine + dy * sine;
                        var localY = -dx * sine + dy * cosine;
                        var localCentre = new Vector2(localX, localY);
                        var localPixelX = new Vector2(cosine, -sine);
                        var localPixelY = new Vector2(sine, cosine);
                        var coverage = PolygonCoverage(
                            petalOutline,
                            localCentre,
                            localPixelX,
                            localPixelY);
                        if (coverage <= 0) continue;

                        BlendPixel(pixels, y * width + x, tint, alpha * coverage);
                    }
                }
            }
        }

        private static Vector2 CubicBezier(
            Vector2 p0,
            Vector2 p1,
            Vector2 p2,
            Vector2 p3,
            float t)
        {
            var inverse = 1f - t;
            var inverseSquared = inverse * inverse;
            var tSquared = t * t;
            return inverseSquared * inverse * p0 +
                3f * inverseSquared * t * p1 +
                3f * inverse * tSquared * p2 +
                tSquared * t * p3;
        }

        private static bool PointInsidePolygon(Vector2 point, Vector2[] polygon)
        {
            var inside = false;
            for (int index = 0, previous = polygon.Length - 1; index < polygon.Length; previous = index++)
            {
                var current = polygon[index];
                var prior = polygon[previous];
                var crosses = (current.y > point.y) != (prior.y > point.y) &&
                    point.x < (prior.x - current.x) * (point.y - current.y) /
                        (prior.y - current.y) + current.x;
                if (crosses) inside = !inside;
            }
            return inside;
        }

        private static float PolygonCoverage(
            Vector2[] polygon,
            Vector2 centre,
            Vector2 pixelAxisX,
            Vector2 pixelAxisY)
        {
            const int samplesPerAxis = 4;
            var inside = 0;
            for (var sampleY = 0; sampleY < samplesPerAxis; sampleY++)
            {
                var offsetY = (sampleY + 0.5f) / samplesPerAxis - 0.5f;
                for (var sampleX = 0; sampleX < samplesPerAxis; sampleX++)
                {
                    var offsetX = (sampleX + 0.5f) / samplesPerAxis - 0.5f;
                    var point = centre + pixelAxisX * offsetX + pixelAxisY * offsetY;
                    if (PointInsidePolygon(point, polygon)) inside++;
                }
            }
            return inside / (float)(samplesPerAxis * samplesPerAxis);
        }

        // Specs are immutable once built and are read by the pixel builders,
        // which may run off the main thread. BuildSpec calls ColorUtility, which
        // Unity does not document as thread-safe, so cache the results and prime
        // them from the main thread via WarmSpecs before dispatching any bake.
        private static readonly VisualSpec[] SpecCache = new VisualSpec[ContentOrder.PreparedArenas.Length];

        /// <summary>
        /// Main-thread only. Primes the spec cache so <see cref="BuildBasePixels"/>
        /// and <see cref="BuildDetailPixels"/> can run on a background thread.
        /// </summary>
        public static void WarmSpecs()
        {
            for (var index = 0; index < SpecCache.Length; index++)
                SpecFor((ArenaId)index);
        }

        private static VisualSpec SpecFor(ArenaId arena)
        {
            var index = (int)arena;
            if (index < 0 || index >= SpecCache.Length) return BuildSpec(arena);
            var cached = SpecCache[index];
            if (cached != null) return cached;
            cached = BuildSpec(arena);
            SpecCache[index] = cached;
            return cached;
        }

        private static VisualSpec BuildSpec(ArenaId arena)
        {
            switch (arena)
            {
                case ArenaId.RedNebula:
                    return new VisualSpec
                    {
                        FieldCentre = Parse("#1b0a10"), FieldMid = Parse("#30111a"), FieldOuter = Parse("#4a1a1e"),
                        BiasX = 0.34f, BiasY = 0.62f, NoiseSeed = 0x7c1f, Clouding = 0.62f,
                        CloudTint = ParseRgb("176,74,52"), CloudAnisotropy = 4f, SweepTint = ParseRgb("60,18,22"),
                        Grain = 0.2f, GrainTint = new Vector3(0, -16, -22), FilamentAngle = -0.62f,
                        FilamentCount = 4, FilamentColors = new[] { Parse("#7f1d2e"), Parse("#98301f"), Parse("#5b1a2c"), Parse("#6d2233") }, Pale = false,
                        FarRockAlpha = 0.94f, FarRockBody = Parse("#0d0709"), FarRockRim = Parse("#8a3a22"), LandmarkLightAngle = Mathf.PI * 0.86f,
                    };
                case ArenaId.WhiteSakura:
                    return new VisualSpec
                    {
                        FieldCentre = Parse("#8a8a89"), FieldMid = Parse("#a5a5a1"), FieldOuter = Parse("#c7c5bd"),
                        BiasX = 0.62f, BiasY = 0.36f, NoiseSeed = 0x2ad9, Clouding = 0.55f,
                        CloudTint = ParseRgb("84,86,96"), CloudAnisotropy = 3.4f, SweepTint = ParseRgb("236,230,218"),
                        Grain = 0.24f, GrainTint = new Vector3(-6, -4, 0), FilamentAngle = 0.5f,
                        FilamentCount = 3, FilamentColors = new[] { Parse("#c4c3c6"), Parse("#cbc8c6"), Parse("#bdbcc2") }, Pale = true,
                        FarRockAlpha = 0.42f, FarRockBody = Parse("#94919a"), FarRockRim = Parse("#f3ede4"), LandmarkLightAngle = -0.72f,
                    };
                case ArenaId.Hydra:
                    return new VisualSpec
                    {
                        FieldCentre = Parse("#123c20"), FieldMid = Parse("#06180c"), FieldOuter = Parse("#010503"),
                        BiasX = 0.5f, BiasY = 0.42f, NoiseSeed = 0x4f31, Clouding = 0.72f,
                        CloudTint = ParseRgb("24,142,61"), CloudAnisotropy = 5.2f, SweepTint = ParseRgb("4,44,18"),
                        Grain = 0.28f, GrainTint = new Vector3(-10, 10, -8), FilamentAngle = -0.2f,
                        FilamentCount = 5, FilamentColors = new[] { Parse("#0d5a2b"), Parse("#197a35"), Parse("#0b4321"), Parse("#2b8f3f"), Parse("#123d24") }, Pale = false,
                        FarRockAlpha = 0, FarRockBody = Color.black, FarRockRim = Parse("#78ff5a"), LandmarkLightAngle = -0.35f,
                    };
                case ArenaId.MonochromeCourt:
                    return new VisualSpec
                    {
                        FieldCentre = Parse("#777774"), FieldMid = Parse("#292a2c"), FieldOuter = Parse("#050607"),
                        BiasX = 0.5f, BiasY = 0.5f, NoiseSeed = 0x6c31, Clouding = 0.18f,
                        CloudTint = ParseRgb("196,196,190"), CloudAnisotropy = 1f, SweepTint = ParseRgb("20,20,22"),
                        Grain = 0.26f, GrainTint = new Vector3(-5, -5, -4), FilamentAngle = 0,
                        FilamentCount = 0, FilamentColors = new[] { Color.white }, Pale = false,
                        FarRockAlpha = 0, FarRockBody = Color.black, FarRockRim = Color.white, LandmarkLightAngle = 0,
                    };
                default:
                    return new VisualSpec
                    {
                        FieldCentre = Parse("#10162a"), FieldMid = Parse("#080b17"), FieldOuter = Parse("#04060b"),
                        BiasX = 0.44f, BiasY = 0.39f, NoiseSeed = 0x51a3, Clouding = 0.14f,
                        CloudTint = ParseRgb("84,104,158"), CloudAnisotropy = 1.15f, SweepTint = ParseRgb("26,38,72"),
                        Grain = 0.16f, GrainTint = new Vector3(-8, -4, 0), FilamentAngle = 0,
                        FilamentCount = 0, FilamentColors = new[] { Color.white }, Pale = false,
                        FarRockAlpha = 0, FarRockBody = Color.black, FarRockRim = Color.black, LandmarkLightAngle = 0,
                    };
            }
        }

        private static Color Blend(Color baseColor, Color overlay, float alpha)
        {
            return Color.Lerp(baseColor, overlay, Mathf.Clamp01(alpha));
        }

        private static float QuantizeCanvasGradientStop(float value)
        {
            return Mathf.Floor(value * 1000f + 0.5f) / 1000f;
        }

        private static float QuantizeCanvasAlphaByte(float value)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(value) * 255f) / 255f;
        }

        private static float SweepAcross(
            float u,
            float v,
            int width,
            int height,
            float sine,
            float cosine,
            float offset)
        {
            return -(u - 0.5f) * width * sine
                + (v - 0.5f - offset) * height * cosine;
        }

        private static float SampleCloud(float[] values, int width, int height, float u, float v)
        {
            if (values == null || values.Length == 0) return 0;
            var x = Mathf.Clamp(u * width - 0.5f, 0, width - 1);
            var y = Mathf.Clamp(v * height - 0.5f, 0, height - 1);
            var x0 = Mathf.FloorToInt(x);
            var y0 = Mathf.FloorToInt(y);
            var x1 = Mathf.Min(width - 1, x0 + 1);
            var y1 = Mathf.Min(height - 1, y0 + 1);
            var tx = x - x0;
            var ty = y - y0;
            var top = Mathf.Lerp(values[y0 * width + x0], values[y0 * width + x1], tx);
            var bottom = Mathf.Lerp(values[y1 * width + x0], values[y1 * width + x1], tx);
            return Mathf.Lerp(top, bottom, ty);
        }

        private static Color32[] BuildGrainPixels(VisualSpec spec, int width, int height)
        {
            var pixels = new Color32[width * height];
            var pale = spec.Pale;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var fibre = ValueNoise(x * 0.13f, y * 0.37f, spec.NoiseSeed + 3);
                    var speck = Hash2(x, y, spec.NoiseSeed + 9);
                    var value = fibre * 0.28f + speck * 0.72f;
                    var tone = (pale ? 152 : 96) + Mathf.RoundToInt(value * (pale ? 86f : 74f));
                    var index = y * width + x;
                    var red = Mathf.Clamp(tone + Mathf.RoundToInt(spec.GrainTint.x), 0, 255);
                    var green = Mathf.Clamp(tone + Mathf.RoundToInt(spec.GrainTint.y), 0, 255);
                    var blue = Mathf.Clamp(tone + Mathf.RoundToInt(spec.GrainTint.z), 0, 255);
                    var alpha = 10 + Mathf.RoundToInt(value * (pale ? 20f : 18f));
                    pixels[index] = new Color32((byte)red, (byte)green, (byte)blue, (byte)alpha);
                }
            }
            return pixels;
        }

        private static Color Parse(string value)
        {
            return ColorUtility.TryParseHtmlString(value, out var color) ? color : Color.magenta;
        }

        private static Color ParseRgb(string value)
        {
            var parts = value.Split(',');
            if (parts.Length != 3) return Color.magenta;
            return new Color(
                Mathf.Clamp01(float.Parse(parts[0]) / 255f),
                Mathf.Clamp01(float.Parse(parts[1]) / 255f),
                Mathf.Clamp01(float.Parse(parts[2]) / 255f),
                1);
        }

        private static float Fbm(float x, float y, int seed, int octaves)
        {
            var sum = 0f;
            var amplitude = 1f;
            var total = 0f;
            var frequency = 1f;
            for (var octave = 0; octave < octaves; octave++)
            {
                sum += ValueNoise(x * frequency, y * frequency, seed + octave * 8191) * amplitude;
                total += amplitude;
                amplitude *= 0.52f;
                frequency *= 2.07f;
            }
            return total > 0 ? sum / total : 0;
        }

        private static float ValueNoise(float x, float y, int seed)
        {
            var xi = Mathf.FloorToInt(x);
            var yi = Mathf.FloorToInt(y);
            var xf = Smooth(x - xi);
            var yf = Smooth(y - yi);
            var a = Hash2(xi, yi, seed);
            var b = Hash2(xi + 1, yi, seed);
            var c = Hash2(xi, yi + 1, seed);
            var d = Hash2(xi + 1, yi + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, xf), Mathf.Lerp(c, d, xf), yf);
        }

        private static float Smooth(float value)
        {
            return value * value * (3f - 2f * value);
        }

        private static float Hash2(int x, int y, int seed)
        {
            unchecked
            {
                uint hash = (uint)(x * 374761393 + y * 668265263 + seed * 362437);
                hash = (hash ^ (hash >> 13)) * 1274126177u;
                hash ^= hash >> 16;
                return hash / 4294967296f;
            }
        }
    }
}
