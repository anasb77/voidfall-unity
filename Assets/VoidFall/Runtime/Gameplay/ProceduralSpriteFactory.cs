using System;
using System.Collections.Generic;
using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    internal static class ProceduralSpriteFactory
    {
        public static bool InstallBakedCatalog(ProceduralSpriteCatalog catalog)
        {
            if (catalog == null || !catalog.IsUsable()) return false;

            ClearSpriteCachesForCatalogInstall();
            var installed = 0;
            for (var index = 0; index < catalog.Entries.Count; index++)
            {
                var entry = catalog.Entries[index];
                if (InstallBakedEntry(entry.Key, entry.Sprite)) installed++;
            }
            return installed == catalog.Entries.Count;
        }

        private static void ClearSpriteCachesForCatalogInstall()
        {
            _circle = null;
            _touchJoystickBase = null;
            _blastWaveDisc = null;
            _arenaStellarLimb = null;
            _diamond = null;
            _square = null;
            _operative = null;
            _playerAura = null;
            _playerAuraAdrenal = null;
            _playerRing = null;
            _ring = null;
            _rock = null;
            _petal = null;
            _gemSmall = null;
            _gemMedium = null;
            _gemLarge = null;
            _blade = null;
            _hollowBlade = null;
            _eliteRing = null;
            _eliteMark = null;
            _workshopPreviewBackdrop = null;
            _workshopPreviewWideBackdrop = null;
            _workshopPreviewMobilityTrail = null;
            _meteorCore = null;
            _impactMark = null;
            _damageIndicator = null;
            _dot = null;
            _particleDot = null;
            _arenaCurrentGlow = null;
            _redHealthVignette = null;

            Array.Clear(WorkshopPreviewCoreSprites, 0, WorkshopPreviewCoreSprites.Length);
            Array.Clear(MeteorShardSprites, 0, MeteorShardSprites.Length);
            Array.Clear(ArenaVignettes, 0, ArenaVignettes.Length);
            Array.Clear(ArenaRockVariants, 0, ArenaRockVariants.Length);
            EnemySprites.Clear();
            RosterTwoEnemySprites.Clear();
            BossSprites.Clear();
            PickupSprites.Clear();
            ProjectileSprites.Clear();
            ProjectileFrameSets.Clear();
            MeteorSprites.Clear();
            WorkshopPreviewSprites.Clear();
            WorkshopPreviewLayerSprites.Clear();
            ArenaDotSprites.Clear();
        }

        private static bool InstallBakedEntry(string key, Sprite sprite)
        {
            if (string.IsNullOrEmpty(key) || sprite == null) return false;
            var parts = key.Split('|');
            if (parts.Length < 2) return false;

            switch (parts[0])
            {
                case "fixed":
                    return InstallFixedSprite(parts[1], sprite);
                case "gem":
                    if (!TryIndex(parts, 1, 3, out var gem)) return false;
                    if (gem == 0) _gemSmall = sprite;
                    else if (gem == 1) _gemMedium = sprite;
                    else _gemLarge = sprite;
                    return true;
                case "arena-rock":
                    return InstallArraySprite(parts, ArenaRockVariants, sprite);
                case "meteor-shard":
                    return InstallArraySprite(parts, MeteorShardSprites, sprite);
                case "arena-vignette":
                    return InstallArraySprite(parts, ArenaVignettes, sprite);
                case "workshop-core":
                    return InstallArraySprite(parts, WorkshopPreviewCoreSprites, sprite);
                case "pickup":
                    PickupSprites[parts[1]] = sprite;
                    return true;
                case "projectile":
                    ProjectileSprites[parts[1]] = sprite;
                    return true;
                case "projectile-frame":
                    if (parts.Length != 3 || !int.TryParse(parts[2], out var frame) ||
                        frame < 0 || frame >= ProjectileFrameCount)
                        return false;
                    if (!ProjectileFrameSets.TryGetValue(parts[1], out var frames))
                    {
                        frames = new Sprite[ProjectileFrameCount];
                        ProjectileFrameSets[parts[1]] = frames;
                    }
                    frames[frame] = sprite;
                    return true;
                case "roster":
                    if (parts.Length != 3) return false;
                    RosterTwoEnemySprites[new RosterTwoCacheKey(parts[1], parts[2] == "1")] = sprite;
                    return true;
                case "enemy":
                case "boss":
                    if (parts.Length != 7 ||
                        !byte.TryParse(parts[2], out var r) ||
                        !byte.TryParse(parts[3], out var g) ||
                        !byte.TryParse(parts[4], out var b) ||
                        !byte.TryParse(parts[5], out var a))
                        return false;
                    var enemyKey = new EnemyCacheKey(
                        parts[1],
                        new Color32(r, g, b, a),
                        parts[6] == "1");
                    if (parts[0] == "enemy") EnemySprites[enemyKey] = sprite;
                    else BossSprites[enemyKey] = sprite;
                    return true;
                case "meteor":
                    MeteorSprites[parts[1]] = sprite;
                    return true;
                case "workshop-preview":
                    WorkshopPreviewSprites[parts[1]] = sprite;
                    return true;
                case "workshop-layer":
                    WorkshopPreviewLayerSprites[parts[1]] = sprite;
                    return true;
                case "arena-dot":
                    if (parts.Length != 5 ||
                        !byte.TryParse(parts[1], out var dotR) ||
                        !byte.TryParse(parts[2], out var dotG) ||
                        !byte.TryParse(parts[3], out var dotB) ||
                        !byte.TryParse(parts[4], out var dotA))
                        return false;
                    ArenaDotSprites[new Color32(dotR, dotG, dotB, dotA)] = sprite;
                    return true;
                default:
                    return false;
            }
        }

        private static bool InstallFixedSprite(string id, Sprite sprite)
        {
            switch (id)
            {
                case "circle": _circle = sprite; return true;
                case "touch-base": _touchJoystickBase = sprite; return true;
                case "blast-wave": _blastWaveDisc = sprite; return true;
                case "stellar-limb": _arenaStellarLimb = sprite; return true;
                case "diamond": _diamond = sprite; return true;
                case "square": _square = sprite; return true;
                case "rock": _rock = sprite; return true;
                case "petal": _petal = sprite; return true;
                case "blade": _blade = sprite; return true;
                case "hollow-blade": _hollowBlade = sprite; return true;
                case "elite-ring": _eliteRing = sprite; return true;
                case "elite-mark": _eliteMark = sprite; return true;
                case "operative": _operative = sprite; return true;
                case "ring": _ring = sprite; return true;
                case "player-ring": _playerRing = sprite; return true;
                case "player-aura": _playerAura = sprite; return true;
                case "player-aura-adrenal": _playerAuraAdrenal = sprite; return true;
                case "workshop-backdrop": _workshopPreviewBackdrop = sprite; return true;
                case "workshop-wide-backdrop": _workshopPreviewWideBackdrop = sprite; return true;
                case "workshop-mobility-trail": _workshopPreviewMobilityTrail = sprite; return true;
                case "meteor-core": _meteorCore = sprite; return true;
                case "impact-mark": _impactMark = sprite; return true;
                case "damage-indicator": _damageIndicator = sprite; return true;
                case "dot": _dot = sprite; return true;
                case "particle-dot": _particleDot = sprite; return true;
                case "arena-current-glow": _arenaCurrentGlow = sprite; return true;
                case "red-health-vignette": _redHealthVignette = sprite; return true;
                default: return false;
            }
        }

        private static bool InstallArraySprite(string[] parts, Sprite[] target, Sprite sprite)
        {
            if (!TryIndex(parts, 1, target.Length, out var index)) return false;
            target[index] = sprite;
            return true;
        }

        private static bool TryIndex(string[] parts, int part, int length, out int index)
        {
            index = -1;
            return parts.Length > part &&
                   int.TryParse(parts[part], out index) &&
                   index >= 0 && index < length;
        }

#if UNITY_EDITOR
        public static ProceduralSpriteCatalog BuildCatalogSnapshot()
        {
            ClearSpriteCachesForCatalogInstall();
            SpriteAtlasPacker.ResetForBake();
            WarmCatalogSprites();
            FlushAtlas();

            var entries = new List<ProceduralSpriteCatalogEntry>();
            AddFixedCatalogEntries(entries);
            AddArrayEntries(entries, "arena-rock", ArenaRockVariants);
            AddArrayEntries(entries, "meteor-shard", MeteorShardSprites);
            AddArrayEntries(entries, "arena-vignette", ArenaVignettes);
            AddArrayEntries(entries, "workshop-core", WorkshopPreviewCoreSprites);
            AddCatalogEntry(entries, "gem|0", _gemSmall);
            AddCatalogEntry(entries, "gem|1", _gemMedium);
            AddCatalogEntry(entries, "gem|2", _gemLarge);

            foreach (var pair in EnemySprites)
                AddCatalogEntry(entries, EnemyCatalogKey("enemy", pair.Key), pair.Value);
            foreach (var pair in BossSprites)
                AddCatalogEntry(entries, EnemyCatalogKey("boss", pair.Key), pair.Value);
            foreach (var pair in RosterTwoEnemySprites)
                AddCatalogEntry(entries, "roster|" + pair.Key.Id + "|" + (pair.Key.Hit ? "1" : "0"), pair.Value);
            foreach (var pair in PickupSprites)
                AddCatalogEntry(entries, "pickup|" + pair.Key, pair.Value);
            foreach (var pair in ProjectileSprites)
                AddCatalogEntry(entries, "projectile|" + pair.Key, pair.Value);
            foreach (var pair in ProjectileFrameSets)
            {
                for (var frame = 0; frame < pair.Value.Length; frame++)
                    AddCatalogEntry(entries, "projectile-frame|" + pair.Key + "|" + frame, pair.Value[frame]);
            }
            foreach (var pair in MeteorSprites)
                AddCatalogEntry(entries, "meteor|" + pair.Key, pair.Value);
            foreach (var pair in WorkshopPreviewSprites)
                AddCatalogEntry(entries, "workshop-preview|" + pair.Key, pair.Value);
            foreach (var pair in WorkshopPreviewLayerSprites)
                AddCatalogEntry(entries, "workshop-layer|" + pair.Key, pair.Value);
            foreach (var pair in ArenaDotSprites)
            {
                var color = pair.Key;
                AddCatalogEntry(
                    entries,
                    "arena-dot|" + color.r + "|" + color.g + "|" + color.b + "|" + color.a,
                    pair.Value);
            }

            entries.Sort((left, right) =>
            {
                var leftHydra = IsHydraCatalogKey(left.Key);
                var rightHydra = IsHydraCatalogKey(right.Key);
                if (leftHydra != rightHydra) return leftHydra ? 1 : -1;
                return string.CompareOrdinal(left.Key, right.Key);
            });
            var catalog = ScriptableObject.CreateInstance<ProceduralSpriteCatalog>();
            catalog.name = "VoidFall Prepared Procedural Sprites";
            catalog.ReplaceEntries(entries);
            return catalog;
        }

        public static void ReleaseCatalogSnapshot(ProceduralSpriteCatalog catalog)
        {
            var sprites = new HashSet<Sprite>();
            var textures = new HashSet<Texture2D>();
            if (catalog != null)
            {
                foreach (var entry in catalog.Entries)
                {
                    if (entry == null || entry.Sprite == null) continue;
                    sprites.Add(entry.Sprite);
                    if (entry.Sprite.texture != null) textures.Add(entry.Sprite.texture);
                }
            }

            ClearSpriteCachesForCatalogInstall();
            foreach (var sprite in sprites) UnityEngine.Object.DestroyImmediate(sprite);
            foreach (var texture in textures) UnityEngine.Object.DestroyImmediate(texture);
            SpriteAtlasPacker.ForgetAfterBakeCleanup();
            if (catalog != null) UnityEngine.Object.DestroyImmediate(catalog);
        }

        private static void WarmCatalogSprites()
        {
            Circle();
            TouchJoystickBase();
            BlastWaveDisc();
            ArenaStellarLimb();
            Diamond();
            Square();
            Rock();
            for (var index = 0; index < ArenaRockVariants.Length; index++) ArenaRock(index);
            Petal();
            for (var tier = 0; tier < 3; tier++) Gem(tier);
            Blade(false);
            Blade(true);
            EliteRing();
            EliteMark();
            Operative();
            Ring();
            PlayerRing();
            PlayerAura(false);
            PlayerAura(true);
            WorkshopPreviewBackdrop();
            WorkshopPreviewWideBackdrop();
            WorkshopPreviewMobilityTrail();
            foreach (var id in new[] { "magnet", "integrity", "recovery", "power", "precision", "arsenal" })
                for (var rank = 1; rank <= 3; rank++) WorkshopPreviewLayer(id, rank);
            WorkshopPreviewLayer("protocol", 1);
            for (var power = 0; power < WorkshopPreviewCoreSprites.Length; power++)
                WorkshopPreviewCore(power);
            MeteorCore();
            Dot();
            ArenaDot(Color.white);
            ArenaDot(ParseColor("#fb923c"));
            ArenaDot(ParseColor("#e2e8f0"));
            ParticleDot();
            ArenaCurrentGlow();
            for (var arena = 0; arena < ArenaVignettes.Length; arena++)
                ArenaVignette((ArenaId)arena);
            RedHealthVignette();
            ImpactMark();
            DamageIndicator();
            WarmAllSprites();
        }

        private static void AddFixedCatalogEntries(List<ProceduralSpriteCatalogEntry> entries)
        {
            AddCatalogEntry(entries, "fixed|circle", _circle);
            AddCatalogEntry(entries, "fixed|touch-base", _touchJoystickBase);
            AddCatalogEntry(entries, "fixed|blast-wave", _blastWaveDisc);
            AddCatalogEntry(entries, "fixed|stellar-limb", _arenaStellarLimb);
            AddCatalogEntry(entries, "fixed|diamond", _diamond);
            AddCatalogEntry(entries, "fixed|square", _square);
            AddCatalogEntry(entries, "fixed|rock", _rock);
            AddCatalogEntry(entries, "fixed|petal", _petal);
            AddCatalogEntry(entries, "fixed|blade", _blade);
            AddCatalogEntry(entries, "fixed|hollow-blade", _hollowBlade);
            AddCatalogEntry(entries, "fixed|elite-ring", _eliteRing);
            AddCatalogEntry(entries, "fixed|elite-mark", _eliteMark);
            AddCatalogEntry(entries, "fixed|operative", _operative);
            AddCatalogEntry(entries, "fixed|ring", _ring);
            AddCatalogEntry(entries, "fixed|player-ring", _playerRing);
            AddCatalogEntry(entries, "fixed|player-aura", _playerAura);
            AddCatalogEntry(entries, "fixed|player-aura-adrenal", _playerAuraAdrenal);
            AddCatalogEntry(entries, "fixed|workshop-backdrop", _workshopPreviewBackdrop);
            AddCatalogEntry(entries, "fixed|workshop-wide-backdrop", _workshopPreviewWideBackdrop);
            AddCatalogEntry(entries, "fixed|workshop-mobility-trail", _workshopPreviewMobilityTrail);
            AddCatalogEntry(entries, "fixed|meteor-core", _meteorCore);
            AddCatalogEntry(entries, "fixed|impact-mark", _impactMark);
            AddCatalogEntry(entries, "fixed|damage-indicator", _damageIndicator);
            AddCatalogEntry(entries, "fixed|dot", _dot);
            AddCatalogEntry(entries, "fixed|particle-dot", _particleDot);
            AddCatalogEntry(entries, "fixed|arena-current-glow", _arenaCurrentGlow);
            AddCatalogEntry(entries, "fixed|red-health-vignette", _redHealthVignette);
        }

        private static void AddArrayEntries(
            List<ProceduralSpriteCatalogEntry> entries,
            string family,
            Sprite[] sprites)
        {
            for (var index = 0; index < sprites.Length; index++)
                AddCatalogEntry(entries, family + "|" + index, sprites[index]);
        }

        private static void AddCatalogEntry(
            List<ProceduralSpriteCatalogEntry> entries,
            string key,
            Sprite sprite)
        {
            if (sprite == null)
                throw new InvalidOperationException("Procedural sprite bake produced no sprite for " + key + ".");
            entries.Add(new ProceduralSpriteCatalogEntry(key, sprite));
        }

        private static string EnemyCatalogKey(string family, EnemyCacheKey key)
        {
            return family + "|" + key.Id + "|" + key.Accent.r + "|" + key.Accent.g + "|" +
                   key.Accent.b + "|" + key.Accent.a + "|" + (key.Hit ? "1" : "0");
        }

        private static bool IsHydraCatalogKey(string key) =>
            key != null && (key.Contains("hydra-rib") ||
                            key == "arena-vignette|3");
#endif

        private static Sprite _circle;
        private static Sprite _touchJoystickBase;
        private static Sprite _blastWaveDisc;
        private static Sprite _arenaStellarLimb;
        private static Sprite _diamond;
        private static Sprite _square;
        private static Sprite _operative;
        private static Sprite _playerAura;
        private static Sprite _playerAuraAdrenal;
        private static Sprite _playerRing;
        private static Sprite _ring;
        private static Sprite _rock;
        private static Sprite _petal;
        private static Sprite _gemSmall;
        private static Sprite _gemMedium;
        private static Sprite _gemLarge;
        private static Sprite _blade;
        private static Sprite _hollowBlade;
        private static Sprite _eliteRing;
        private static Sprite _eliteMark;
        private struct EnemyCacheKey : IEquatable<EnemyCacheKey>
        {
            public readonly string Id;
            public readonly Color32 Accent;
            public readonly bool Hit;

            public EnemyCacheKey(string id, Color32 accent, bool hit)
            {
                Id = id;
                Accent = accent;
                Hit = hit;
            }

            public bool Equals(EnemyCacheKey other)
            {
                return string.Equals(Id, other.Id, StringComparison.Ordinal) &&
                       Accent.r == other.Accent.r &&
                       Accent.g == other.Accent.g &&
                       Accent.b == other.Accent.b &&
                       Accent.a == other.Accent.a &&
                       Hit == other.Hit;
            }

            public override bool Equals(object obj)
            {
                return obj is EnemyCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = Id != null ? StringComparer.Ordinal.GetHashCode(Id) : 0;
                    hash = (hash * 397) ^ (int)Accent.r;
                    hash = (hash * 397) ^ (int)Accent.g;
                    hash = (hash * 397) ^ (int)Accent.b;
                    hash = (hash * 397) ^ (int)Accent.a;
                    hash = (hash * 397) ^ (Hit ? 1 : 0);
                    return hash;
                }
            }
        }

        private struct RosterTwoCacheKey : IEquatable<RosterTwoCacheKey>
        {
            public readonly string Id;
            public readonly bool Hit;

            public RosterTwoCacheKey(string id, bool hit)
            {
                Id = id;
                Hit = hit;
            }

            public bool Equals(RosterTwoCacheKey other)
            {
                return string.Equals(Id, other.Id, StringComparison.Ordinal) && Hit == other.Hit;
            }

            public override bool Equals(object obj)
            {
                return obj is RosterTwoCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = Id != null ? StringComparer.Ordinal.GetHashCode(Id) : 0;
                    hash = (hash * 397) ^ (Hit ? 1 : 0);
                    return hash;
                }
            }
        }

        private static readonly Dictionary<EnemyCacheKey, Sprite> EnemySprites = new Dictionary<EnemyCacheKey, Sprite>();
        private static readonly Dictionary<RosterTwoCacheKey, Sprite> RosterTwoEnemySprites = new Dictionary<RosterTwoCacheKey, Sprite>();
        private static readonly Dictionary<EnemyCacheKey, Sprite> BossSprites = new Dictionary<EnemyCacheKey, Sprite>();
        private static readonly Dictionary<string, Sprite> PickupSprites = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Sprite> ProjectileSprites = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Sprite[]> ProjectileFrameSets =
            new Dictionary<string, Sprite[]>();
        private const int ProjectileFrameCount = 32;
        private static readonly Dictionary<string, Sprite> MeteorSprites = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Sprite> WorkshopPreviewSprites = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Sprite> WorkshopPreviewLayerSprites = new Dictionary<string, Sprite>();
        private static Sprite _workshopPreviewBackdrop;
        private static Sprite _workshopPreviewWideBackdrop;
        private static Sprite _workshopPreviewMobilityTrail;
        private static readonly Sprite[] WorkshopPreviewCoreSprites = new Sprite[4];
        private static readonly Sprite[] MeteorShardSprites = new Sprite[4];
        private static Sprite _meteorCore;
        private static Sprite _impactMark;
        private static Sprite _damageIndicator;
        private static Sprite _dot;
        private static Sprite _particleDot;
        private static readonly Dictionary<Color32, Sprite> ArenaDotSprites =
            new Dictionary<Color32, Sprite>();
        private static Sprite _arenaCurrentGlow;
        private static readonly Sprite[] ArenaVignettes = new Sprite[ContentOrder.PreparedArenas.Length];
        private static Sprite _redHealthVignette;
        // Red Nebula's red giant is the shared light direction used by the
        // browser meteor sprites and the arena landmark.
        private const float MeteorLightAngle = 2.70176983f;
        private static readonly Sprite[] ArenaRockVariants = new Sprite[6];
        private static readonly float[][] ArenaRockOutlines =
        {
            new[] { 1f, 0.97f, 0.72f, 0.86f, 0.84f, 1.02f, 0.66f, 0.9f, 0.93f },
            new[] { 0.9f, 0.92f, 1.04f, 0.79f, 0.62f, 0.83f, 0.86f, 1f, 0.74f, 0.88f },
            new[] { 1.03f, 1f, 0.81f, 0.58f, 0.87f, 0.9f, 0.89f, 0.7f, 0.95f, 0.98f, 0.76f },
            new[] { 0.84f, 0.98f, 0.95f, 1.05f, 0.68f, 0.8f, 0.79f, 0.63f, 0.91f },
            new[] { 0.96f, 0.7f, 0.88f, 0.9f, 1.01f, 0.98f, 0.6f, 0.82f, 0.85f, 0.94f, 0.72f, 0.9f },
            new[] { 1f, 0.86f, 0.84f, 0.66f, 0.94f, 1.03f, 0.75f, 0.71f, 0.89f, 0.92f },
        };

        public static Sprite Circle()
        {
            return _circle ?? (_circle = Create(32, (x, y, radius) =>
            {
                var dx = x - radius;
                var dy = y - radius;
                return dx * dx + dy * dy <= radius * radius;
            }));
        }

        // Browser Input.render() draws the touch base as a translucent cyan
        // fill plus a separate 2px cyan outline. Keep that source-over result
        // in one readable sprite instead of substituting a filled circle.
        public static Sprite TouchJoystickBase()
        {
            if (_touchJoystickBase != null) return _touchJoystickBase;
            const float radius = 64f;
            var canvas = new RasterCanvas(radius, 0f, 256);
            canvas.FillCircle(
                Vector2.zero,
                radius,
                new Color(0.133f, 0.827f, 0.933f, 0.08f));
            canvas.StrokeCircle(
                Vector2.zero,
                radius,
                new Color(0.404f, 0.91f, 0.976f, 0.38f),
                2f);
            _touchJoystickBase = canvas.ToSprite("VoidFall_Touch_Joystick_Base", true);
            return _touchJoystickBase;
        }

        /// <summary>
        /// Blast-wave fills are drawn directly as large Canvas2D disks in the
        /// browser. Keep a separate high-resolution mask so scaling a bomb or
        /// explosive wave does not magnify the generic 32px utility circle.
        /// </summary>
        public static Sprite BlastWaveDisc()
        {
            if (_blastWaveDisc != null) return _blastWaveDisc;

            // Canvas2D arc().fill() contributes partial coverage at the disk
            // edge. Keep the same high-resolution mask, but rasterize through
            // the shared supersampled helper instead of a binary predicate.
            var canvas = new RasterCanvas(1f, 0f, 256);
            canvas.FillCircle(Vector2.zero, 1f, Color.white);
            _blastWaveDisc = canvas.ToSprite("VoidFall_Blast_Wave_Disc", true);
            return _blastWaveDisc;
        }

        public static Sprite ArenaStellarLimb()
        {
            if (_arenaStellarLimb != null) return _arenaStellarLimb;

            const float radius = 1f;
            // The limb is enlarged to most of the viewport, so a 256px mask
            // exposes stepped alpha coverage along the planet silhouette.
            var canvas = new RasterCanvas(radius, 0.04f, 1024);
            canvas.FillCircle(Vector2.zero, radius, ParseColor("#12060a"));

            // Match the browser's bounded 26-patch stellar surface stream.
            // The mask keeps soft edge patches inside the cropped limb.
            var state = 0x7c1fu ^ 0x1d3au;
            for (var patch = 0; patch < 26; patch++)
            {
                var angle = StellarNext(ref state) * Mathf.PI * 2f;
                var distance = Mathf.Sqrt(StellarNext(ref state)) * radius * 0.98f;
                var centre = new Vector2(
                    Mathf.Cos(angle) * distance,
                    Mathf.Sin(angle) * distance);
                var patchRadius = radius * (0.06f + StellarNext(ref state) * 0.2f);
                var warm = StellarNext(ref state) < 0.34f;
                canvas.Glow(
                    centre,
                    patchRadius,
                    warm ? ParseColor("#96341c") : ParseColor("#0a0406"),
                    warm ? 0.24f : 0.4f);
            }
            canvas.MaskCircle(Vector2.zero, radius);
            _arenaStellarLimb = canvas.ToSprite("VoidFall_Arena_Stellar_Limb_1024", true);
            return _arenaStellarLimb;
        }

        public static Sprite Diamond()
        {
            return _diamond ?? (_diamond = Create(32, (x, y, radius) =>
            {
                return Mathf.Abs(x - radius) + Mathf.Abs(y - radius) <= radius;
            }));
        }

        public static Sprite Square()
        {
            return _square ?? (_square = Create(2, (_, _, _) => true));
        }

        public static Sprite Rock()
        {
            if (_rock != null) return _rock;
            var canvas = new RasterCanvas(1f, 0.08f, 96);
            var points = new[]
            {
                new Vector2(-0.92f, -0.22f), new Vector2(-0.55f, -0.82f),
                new Vector2(0.16f, -0.94f), new Vector2(0.86f, -0.52f),
                new Vector2(0.98f, 0.24f), new Vector2(0.5f, 0.82f),
                new Vector2(-0.24f, 0.9f), new Vector2(-0.86f, 0.54f),
            };
            canvas.FillPolygon(points, Color.white);
            canvas.FillPolygon(new[]
            {
                new Vector2(-0.52f, -0.54f), new Vector2(0.25f, -0.68f),
                new Vector2(0.64f, -0.18f), new Vector2(0.02f, 0.14f),
                new Vector2(-0.66f, 0.04f),
            }, new Color(0.62f, 0.62f, 0.68f, 1));
            canvas.FillPolygon(new[]
            {
                new Vector2(-0.82f, 0.02f), new Vector2(-0.22f, -0.24f),
                new Vector2(0.04f, 0.72f), new Vector2(-0.5f, 0.62f),
            }, new Color(0.28f, 0.29f, 0.34f, 1));
            canvas.StrokePolygon(points, new Color(1f, 1f, 1f, 0.28f), 0.045f);
            canvas.DrawArc(Vector2.zero, 0.92f, -0.9f, 0.2f, 0.035f, new Color(1f, 1f, 1f, 0.34f));
            _rock = canvas.ToSprite("VoidFall_Arena_Rock");
            return _rock;
        }

        /// <summary>
        /// Browser arena decor uses one of four authored traceRock outlines.
        /// Keep these variants separate from the generic chip sprite used by
        /// fractured-ring debris, which has a different interior treatment.
        /// </summary>
        public static Sprite ArenaRock(int shape)
        {
            var slot = Mathf.Abs(shape) % ArenaRockOutlines.Length;
            if (ArenaRockVariants[slot] != null) return ArenaRockVariants[slot];

            var outline = ArenaRockOutlines[slot];
            var points = new Vector2[outline.Length];
            for (var index = 0; index < outline.Length; index++)
            {
                var angle = index / (float)outline.Length * Mathf.PI * 2f - Mathf.PI * 0.5f;
                points[index] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * outline[index];
            }
            var canvas = new RasterCanvas(1f, 0.08f, 96);
            canvas.FillPolygon(points, Color.white);
            ArenaRockVariants[slot] = canvas.ToSprite("VoidFall_Arena_Rock_" + slot);
            return ArenaRockVariants[slot];
        }

        public static Sprite Petal()
        {
            if (_petal != null) return _petal;
            // petalSprite() uses cv(30), matching the 11-unit outline and
            // four-pixel source padding without oversampling the raster.
            var canvas = new RasterCanvas(11f, 4f, 30);
            var points = PetalOutlinePoints();
            canvas.FillPolygon(points, ParseColor("#fbe8ef"));
            canvas.StrokePolygon(points, ParseColor("#f7d9e4"), 1f);
            canvas.DrawLine(new Vector2(-0.4f, -8), new Vector2(0.6f, 7), 0.9f, PetalMidribColor());
            _petal = canvas.ToSprite("VoidFall_Arena_Petal", true);
            return _petal;
        }

        private static Color PetalMidribColor()
        {
            return new Color(233f / 255f, 199f / 255f, 214f / 255f, 0.85f);
        }

        private static Vector2[] PetalOutlinePoints()
        {
            var points = new List<Vector2>(31) { new Vector2(0, -11) };
            AppendCubicBezier(
                points,
                new Vector2(0, -11),
                new Vector2(7, -8),
                new Vector2(8.5f, 1),
                new Vector2(1.5f, 10),
                10);
            AppendCubicBezier(
                points,
                new Vector2(1.5f, 10),
                new Vector2(-1, 11),
                new Vector2(-2.5f, 10),
                new Vector2(-3, 8.5f),
                10);
            AppendCubicBezier(
                points,
                new Vector2(-3, 8.5f),
                new Vector2(-8, 0.5f),
                new Vector2(-6, -7.5f),
                new Vector2(0, -11),
                10);
            return points.ToArray();
        }

        private static void AppendCubicBezier(
            List<Vector2> points,
            Vector2 start,
            Vector2 controlA,
            Vector2 controlB,
            Vector2 end,
            int segments)
        {
            for (var segment = 1; segment <= segments; segment++)
            {
                var t = segment / (float)segments;
                var inverse = 1f - t;
                points.Add(
                    inverse * inverse * inverse * start +
                    3f * inverse * inverse * t * controlA +
                    3f * inverse * t * t * controlB +
                    t * t * t * end);
            }
        }

        public static Sprite Gem(int tier)
        {
            tier = Mathf.Clamp(tier, 0, 2);
            if (tier == 0 && _gemSmall != null) return _gemSmall;
            if (tier == 1 && _gemMedium != null) return _gemMedium;
            if (tier == 2 && _gemLarge != null) return _gemLarge;

            var radius = tier == 0 ? 7f : tier == 1 ? 9f : 12f;
            var color = tier == 0 ? ParseColor("#34d399") : tier == 1 ? ParseColor("#4ade80") : ParseColor("#a3e635");
            // gemSprite() uses cv((r + pad) * 2), where pad is r + 12.
            // Keep the browser's exact source canvas instead of oversampling
            // the three tiers into one 96px raster.
            var canvas = new RasterCanvas(
                radius,
                radius + 12f,
                Mathf.RoundToInt((radius + (radius + 12f)) * 2f));
            // gemSprite() sets pad = r + 12 and then passes r + pad to glow,
            // so the outer glow radius is 2r + 12, not merely r + 12.
            canvas.Glow(GemGlowRadius(tier), color, 0.55f);
            var diamond = new[]
            {
                new Vector2(0, -radius), new Vector2(radius * 0.72f, 0),
                new Vector2(0, radius), new Vector2(-radius * 0.72f, 0),
            };
            canvas.FillPolygonVerticalGradient(
                diamond,
                -radius,
                radius,
                Color.white,
                color,
                new Color(color.r, color.g, color.b, 0.55f),
                0.35f);
            canvas.StrokePolygon(diamond, new Color(1f, 1f, 1f, 0.8f), 1.5f);
            var sprite = canvas.ToAtlasSprite("VoidFall_XP_Gem_" + tier);
            if (tier == 0) _gemSmall = sprite;
            else if (tier == 1) _gemMedium = sprite;
            else _gemLarge = sprite;
            return sprite;
        }

        public static float GemGlowRadius(int tier)
        {
            tier = Mathf.Clamp(tier, 0, 2);
            var radius = tier == 0 ? 7f : tier == 1 ? 9f : 12f;
            return radius + (radius + 12f);
        }

        public static Sprite Blade(bool hollow)
        {
            if (hollow && _hollowBlade != null) return _hollowBlade;
            if (!hollow && _blade != null) return _blade;
            var sourceSize = hollow ? 48 : 44;
            var sourceHeight = hollow ? 24 : 22;
            var canvas = new RasterCanvas(sourceSize * 0.5f, 0f, sourceSize);
            var outer = hollow
                ? new[]
                {
                    new Vector2(-21, 3), new Vector2(-13, -6), new Vector2(15, -7),
                    new Vector2(23, -2), new Vector2(12, 3), new Vector2(-15, 7),
                }
                : new[]
                {
                    new Vector2(-19, 2), new Vector2(-12, -4), new Vector2(15, -6),
                    new Vector2(21, -2), new Vector2(11, 2), new Vector2(-13, 6),
                };
            if (hollow)
            {
                canvas.FillPolygon(outer, new Color(94f / 255f, 234f / 255f, 212f / 255f, 0.3f));
                canvas.StrokePolygon(outer, ParseColor("#ccfbf1"), 1.8f);
                var inner = new[]
                {
                    new Vector2(-12, 2), new Vector2(-7, -2), new Vector2(12, -3),
                    new Vector2(16, -1), new Vector2(8, 1), new Vector2(-8, 4),
                };
                // Browser hollowBladeSprite() uses destination-out here: the
                // inner plate is transparent, not a dark fill over the blade.
                canvas.ErasePolygon(inner);
                canvas.StrokePolygon(inner, ParseColor("#2dd4bf"), 1.2f);
                canvas.FillRect(new Vector2(-18, 3.5f), 5, 2, ParseColor("#99f6e4"));
                _hollowBlade = canvas.ToSprite(
                    "VoidFall_Hollow_Blade",
                    true,
                    (sourceSize - sourceHeight) / 2,
                    sourceHeight,
                    sourceSize);
                return _hollowBlade;
            }

            canvas.FillPolygon(outer, new Color(0.75f, 0.92f, 1f, 0.26f));
            canvas.FillPolygon(new[]
            {
                new Vector2(-14, 1), new Vector2(-8, -2), new Vector2(16, -4),
                new Vector2(20, -2), new Vector2(10, 1), new Vector2(-9, 4),
            }, new Color(0.86f, 0.97f, 1f, 0.95f));
            canvas.StrokePolygon(new[]
            {
                new Vector2(-14, 1), new Vector2(-8, -2), new Vector2(16, -4),
                new Vector2(20, -2), new Vector2(10, 1), new Vector2(-9, 4),
            }, Color.white, 1.2f);
            canvas.FillCircle(new Vector2(-9, 1.2f), 1.8f, new Color(0.06f, 0.09f, 0.15f, 1));
            _blade = canvas.ToSprite(
                "VoidFall_Blade",
                true,
                (sourceSize - sourceHeight) / 2,
                sourceHeight,
                sourceSize);
            return _blade;
        }

        public static float BladeCanvasSize(bool hollow)
        {
            // engine.ts draws the source blade frames at their natural widths:
            // blade is 44x22 and hollowBlade is 48x24.
            return hollow ? 48f : 44f;
        }

        public static Sprite EliteRing()
        {
            if (_eliteRing != null) return _eliteRing;
            var canvas = new RasterCanvas(70f, 10f, 160);
            var color = ParseColor("#f87171");
            // sprites.ts sets lineCap = "butt" for every elite-ring arc.
            canvas.DrawArcButt(Vector2.zero, 64f, -0.02f * Mathf.PI, 0.54f * Mathf.PI, 5f, new Color(color.r, color.g, color.b, 0.72f));
            canvas.DrawArcButt(Vector2.zero, 64f, 0.69f * Mathf.PI, 1.19f * Mathf.PI, 5f, new Color(color.r, color.g, color.b, 0.72f));
            canvas.DrawArcButt(Vector2.zero, 64f, 1.36f * Mathf.PI, 1.91f * Mathf.PI, 5f, new Color(color.r, color.g, color.b, 0.72f));
            canvas.DrawArcButt(Vector2.zero, 70f, 0.18f * Mathf.PI, 1.72f * Mathf.PI, 1.5f, new Color(1f, 0.8f, 0.8f, 0.4f));
            _eliteRing = canvas.ToSprite("VoidFall_Elite_Ring");
            return _eliteRing;
        }

        public static Sprite EliteMark()
        {
            if (_eliteMark != null) return _eliteMark;
            var canvas = new RasterCanvas(63f, 10f, 148);
            var color = ParseColor("#facc15");
            canvas.DrawArc(Vector2.zero, 58f, -0.06f * Mathf.PI, 0.4f * Mathf.PI, 2.4f, new Color(color.r, color.g, color.b, 0.62f));
            canvas.DrawArc(Vector2.zero, 58f, 0.62f * Mathf.PI, 1.06f * Mathf.PI, 2.4f, new Color(color.r, color.g, color.b, 0.62f));
            canvas.DrawArc(Vector2.zero, 58f, 1.28f * Mathf.PI, 1.72f * Mathf.PI, 2.4f, new Color(color.r, color.g, color.b, 0.62f));
            canvas.DrawArc(Vector2.zero, 63f, 0.2f * Mathf.PI, 1.68f * Mathf.PI, 1f, new Color(1f, 0.95f, 0.55f, 0.3f));
            _eliteMark = canvas.ToSprite("VoidFall_Elite_Mark");
            return _eliteMark;
        }

        public static Sprite Operative()
        {
            if (_operative != null) return _operative;
            // playerSprite() uses cv((15 + 22) * 2), so preserve its 74px
            // source canvas while keeping the existing 74-unit runtime size.
            var canvas = new RasterCanvas(15f, 22f, 74);
            canvas.Glow(37f, new Color(0.13f, 0.83f, 0.95f, 1), 0.6f);
            canvas.RadialTwoPointColorGradient(
                new Vector2(-4f, 5f),
                2f,
                new Color(0.925f, 0.996f, 1f, 1),
                Vector2.zero,
                15f,
                new Color(0.055f, 0.455f, 0.565f, 1),
                Vector2.zero,
                15f);
            canvas.StrokeCircle(Vector2.zero, 15f, new Color(0.647f, 0.953f, 0.988f, 1), 2.5f);
            canvas.FillCircle(Vector2.zero, 6.75f, new Color(0.02f, 0.024f, 0.06f, 1));
            // Browser playerSprite(): the small light core shares the exact
            // centre of the dark void eye; it is not an offset highlight.
            canvas.FillCircle(Vector2.zero, 3f, new Color(0.878f, 0.98f, 1f, 1));
            _operative = canvas.ToSprite("VoidFall_Operative", true);
            return _operative;
        }

        public static float OperativeCanvasSize()
        {
            return (15f + 22f) * 2f;
        }

        // Browser ringSprite(): one cached 128 px white sprite containing the
        // bright 7 px stroke and the softer 16 px bloom stroke. Keep both
        // strokes in one texture so their overlap is source-composited before
        // the runtime lighter pass applies the particle lifetime alpha.
        public static Sprite Ring()
        {
            if (_ring != null) return _ring;
            var canvas = new RasterCanvas(64f, 0, 128);
            canvas.DrawArc(
                Vector2.zero,
                52f,
                0,
                Mathf.PI * 2f,
                7f,
                new Color(1f, 1f, 1f, 0.9f));
            canvas.DrawArc(
                Vector2.zero,
                52f,
                0,
                Mathf.PI * 2f,
                16f,
                new Color(1f, 1f, 1f, 0.22f));
            _ring = canvas.ToSprite("VoidFall_Fx_Ring", true);
            return _ring;
        }

        public static Sprite PlayerRing()
        {
            if (_playerRing != null) return _playerRing;
            const float radius = 25f;
            // playerRingSprite() uses cv(r * 2 + 12), which is 62px for
            // r=25; Sprite PPU keeps the 62-unit runtime size unchanged.
            var canvas = new RasterCanvas(radius, 6f, 62);
            for (var index = 0; index < 10; index++)
            {
                var angle = index / 10f * Mathf.PI * 2f;
                var position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                var dotRadius = index % 2 == 0 ? 2.2f : 1.2f;
                var alpha = index % 2 == 0 ? 0.9f : 0.5f;
                canvas.FillCircle(
                    position,
                    dotRadius,
                    new Color(0.404f, 0.91f, 0.976f, alpha));
            }
            canvas.StrokeCircle(
                Vector2.zero,
                radius,
                new Color(0.133f, 0.827f, 0.933f, 0.28f),
                1f);
            _playerRing = canvas.ToSprite("VoidFall_Player_Ring", true);
            return _playerRing;
        }

        public static Sprite PlayerAura()
        {
            return PlayerAura(false);
        }

        public static Sprite PlayerAura(bool adrenalLit)
        {
            if (adrenalLit && _playerAuraAdrenal != null) return _playerAuraAdrenal;
            if (!adrenalLit && _playerAura != null) return _playerAura;

            // Browser drawPlayer uses a radial gradient from a 6 px inner
            // radius to the breathing 32-36 px outer radius. The outer stop
            // stays cyan even when Adrenal changes the centre to amber, so
            // this must be colour-baked rather than a white alpha mask tinted
            // by SpriteRenderer at runtime.
            // The browser gradient reaches its requested outer radius. Keep
            // the generated square edge at that radius so the runtime
            // auraRadius * 2 scale maps 1:1 to the source 32-36 px pulse.
            var canvas = new RasterCanvas(1f, 0f, 128);
            var inner = adrenalLit
                ? new Color(251f / 255f, 146f / 255f, 60f / 255f, 0.2f)
                : new Color(34f / 255f, 211f / 255f, 238f / 255f, 0.14f);
            var outer = new Color(34f / 255f, 211f / 255f, 238f / 255f, 0f);
            canvas.RadialColorGradient(Vector2.zero, 0.1875f, inner, 1f, outer);
            var sprite = canvas.ToSprite(
                adrenalLit ? "VoidFall_Player_Aura_Adrenal" : "VoidFall_Player_Aura",
                true);
            if (adrenalLit) _playerAuraAdrenal = sprite;
            else _playerAura = sprite;
            return sprite;
        }

        public static Sprite WorkshopPreview(
            int integrity,
            int power,
            int mobility,
            int recovery,
            int magnet,
            int precision,
            int arsenal,
            int protocol)
        {
            integrity = Mathf.Clamp(integrity, 0, 3);
            power = Mathf.Clamp(power, 0, 3);
            mobility = Mathf.Clamp(mobility, 0, 3);
            recovery = Mathf.Clamp(recovery, 0, 3);
            magnet = Mathf.Clamp(magnet, 0, 3);
            precision = Mathf.Clamp(precision, 0, 3);
            arsenal = Mathf.Clamp(arsenal, 0, 3);
            protocol = Mathf.Clamp(protocol, 0, 1);
            var key = string.Concat(integrity, "/", power, "/", mobility, "/", recovery, "/", magnet, "/", precision, "/", arsenal, "/", protocol);
            if (WorkshopPreviewSprites.TryGetValue(key, out var cached)) return cached;

            const float frameRadius = 150f;
            var canvas = new RasterCanvas(frameRadius, 0, 360);
            canvas.FillRect(Vector2.zero, 300, 300, new Color(0.012f, 0.025f, 0.065f, 1));
            canvas.Glow(122, new Color(0.055f, 0.28f, 0.38f, 1), 0.3f);
            for (var axis = -140; axis <= 140; axis += 40)
            {
                canvas.DrawLine(new Vector2(axis, -150), new Vector2(axis, 150), 0.8f, new Color(0.4f, 0.9f, 1f, 0.055f));
                canvas.DrawLine(new Vector2(-150, axis), new Vector2(150, axis), 0.8f, new Color(0.4f, 0.9f, 1f, 0.055f));
            }
            for (var index = 0; index < 18; index++)
            {
                var starX = Mathf.Repeat(index * 83f + 29f, 300f) - 150f;
                var starY = Mathf.Repeat(index * 47f + 17f, 300f) - 150f;
                canvas.FillRect(new Vector2(starX, starY), index % 3 == 0 ? 2f : 1f, 1f, new Color(0.65f, 0.95f, 1f, 0.18f + (index % 5) * 0.035f));
            }

            for (var index = 0; index < mobility; index++)
            {
                var offset = (index - (mobility - 1) * 0.5f) * 18f;
                var length = 35f + mobility * 9f;
                canvas.DrawGradientLine(
                    new Vector2(offset, -27f),
                    new Vector2(offset * 1.14f, -28f - length),
                    4f,
                    new Color(0.404f, 0.91f, 0.976f, 0.78f),
                    new Color(0.133f, 0.827f, 0.933f, 0f));
            }

            if (magnet > 0)
            {
                var radius = 76f + magnet * 9f;
                canvas.DrawDashedArc(Vector2.zero, radius, 0f, Mathf.PI * 2f, 2f,
                    new Color(0.655f, 0.545f, 0.98f, 0.22f + magnet * 0.08f), 5f, 12f);
                for (var index = 0; index < magnet * 2; index++)
                {
                    var angle = index / (float)(magnet * 2) * Mathf.PI * 2f;
                    var marker = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    var markerColor = index % 2 == 0
                        ? ParseColor("#c4b5fd")
                        : ParseColor("#67e8f9");
                    canvas.Glow(marker, 12f, new Color(0.655f, 0.545f, 0.98f, 1f), 0.38f);
                    canvas.FillPolygon(new[]
                    {
                        marker + new Vector2(0, 5), marker + new Vector2(4, 0),
                        marker + new Vector2(0, -5), marker + new Vector2(-4, 0),
                    }, markerColor);
                }
            }

            if (integrity > 0)
            {
                var segmentCount = 3 + integrity * 2;
                for (var index = 0; index < segmentCount; index++)
                {
                    var start = index / (float)segmentCount * Mathf.PI * 2f;
                    var width = 3f + integrity * 0.6f;
                    canvas.DrawArc(Vector2.zero, 52f, start, start + 0.42f, width + 6f,
                        new Color(0.22f, 0.74f, 0.97f, 0.14f));
                    canvas.DrawArc(Vector2.zero, 52f, start, start + 0.42f, width,
                        new Color(0.49f, 0.83f, 0.99f, 0.42f + integrity * 0.13f));
                }
            }

            for (var index = 0; index < recovery; index++)
            {
                var angle = index / (float)Mathf.Max(1, recovery) * Mathf.PI * 2f;
                var marker = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 42f;
                canvas.Glow(marker, 13f, new Color(0.204f, 0.827f, 0.6f, 1f), 0.42f);
                canvas.FillRect(marker, 8f, 8f, ParseColor("#6ee7b7"));
                canvas.FillRect(marker + new Vector2(0, -0.1f), 2f, 6f, ParseColor("#052e2b"));
                canvas.FillRect(marker, 6f, 2f, ParseColor("#052e2b"));
            }

            for (var sideIndex = -1; sideIndex <= 1; sideIndex += 2)
            {
                for (var index = 0; index < power; index++)
                {
                    var y = (index - (power - 1) * 0.5f) * 13f;
                    var block = new Vector2(sideIndex * (31f + index * 7f), y);
                    canvas.Glow(block, 13f, new Color(0.984f, 0.573f, 0.235f, 1f), 0.32f);
                    canvas.FillRect(block, 12f, 10f, new Color(0.98f, 0.57f, 0.24f, 0.18f));
                    canvas.DrawLine(new Vector2(sideIndex * (32f + index * 7f), y), new Vector2(sideIndex * (48f + index * 8f), y), 3f, new Color(0.984f, 0.573f, 0.235f, 1f));
                }
            }

            if (precision > 0)
            {
                var tickCount = 2 + precision * 2;
                var precisionColor = WorkshopPrecisionColor();
                for (var index = 0; index < tickCount; index++)
                {
                    var angle = index / (float)tickCount * Mathf.PI * 2f;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    canvas.DrawLine(direction * 58f, direction * 66f, 2.5f,
                        new Color(precisionColor.r, precisionColor.g, precisionColor.b,
                            0.4f + precision * 0.14f));
                }
            }

            for (var index = 0; index < arsenal; index++)
            {
                var angle = index / (float)Mathf.Max(1, arsenal) * Mathf.PI * 2f;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var centre = direction * 58f;
                DrawWorkshopBlade(canvas, centre, angle);
            }

            if (protocol > 0)
            {
                canvas.DrawDashedArc(Vector2.zero, 74f, 0f, Mathf.PI * 2f, 2.5f,
                    WithAlpha(WorkshopProtocolColor(), 0.32f), 7f, 9f);
            }

            canvas.Glow(37f, new Color(0.13f, 0.83f, 0.95f, 1), 0.58f);
            for (var index = 0; index < 3; index++)
            {
                var start = index / 3f * Mathf.PI * 2f;
                canvas.DrawArc(Vector2.zero, 43f, start, start + Mathf.PI * 0.7f, 1.6f, new Color(0.65f, 0.95f, 0.98f, 0.5f));
            }
            canvas.FillCircle(Vector2.zero, 15f, new Color(0.12f, 0.78f, 0.9f, 1));
            canvas.FillCircle(new Vector2(-4f, 5f), 7.6f, new Color(0.82f, 0.98f, 1f, 0.72f));
            canvas.StrokeCircle(Vector2.zero, 15f, new Color(0.65f, 0.95f, 0.98f, 1), 2.5f);
            canvas.FillCircle(Vector2.zero, 6.8f, new Color(0.02f, 0.024f, 0.06f, 1));
            canvas.FillCircle(new Vector2(1.2f, 1.4f), 3f, new Color(0.88f, 0.98f, 1f, 1));
            if (power > 0)
            {
                canvas.Glow(10f, new Color(0.98f, 0.57f, 0.24f, 1), 0.55f);
                canvas.FillCircle(Vector2.zero, 3f + power, new Color(1f, 0.93f, 0.84f, 0.35f + power * 0.12f));
            }

            var sprite = canvas.ToSprite("VoidFall_Workshop_Preview_" + key.Replace('/', '_'));
            WorkshopPreviewSprites[key] = sprite;
            return sprite;
        }

        public static Sprite WorkshopPreviewBackdrop()
        {
            if (_workshopPreviewBackdrop != null) return _workshopPreviewBackdrop;
            const float frameRadius = 150f;
            var canvas = new RasterCanvas(frameRadius, 0, 360);
            canvas.FillRect(Vector2.zero, 300, 300, new Color(0.012f, 0.025f, 0.065f, 1));
            canvas.Glow(122, new Color(0.055f, 0.28f, 0.38f, 1), 0.3f);
            for (var axis = -140; axis <= 140; axis += 40)
            {
                canvas.DrawLine(new Vector2(axis, -150), new Vector2(axis, 150), 0.8f, new Color(0.4f, 0.9f, 1f, 0.055f));
                canvas.DrawLine(new Vector2(-150, axis), new Vector2(150, axis), 0.8f, new Color(0.4f, 0.9f, 1f, 0.055f));
            }
            for (var index = 0; index < 18; index++)
            {
                var starX = Mathf.Repeat(index * 83f + 29f, 300f) - 150f;
                var starY = Mathf.Repeat(index * 47f + 17f, 300f) - 150f;
                canvas.FillRect(new Vector2(starX, starY), index % 3 == 0 ? 2f : 1f, 1f,
                    new Color(0.65f, 0.95f, 1f, 0.18f + (index % 5) * 0.035f));
            }
            _workshopPreviewBackdrop = canvas.ToSprite("VoidFall_Workshop_Preview_Backdrop");
            return _workshopPreviewBackdrop;
        }

        public static Sprite WorkshopPreviewWideBackdrop()
        {
            if (_workshopPreviewWideBackdrop != null) return _workshopPreviewWideBackdrop;

            const int width = 600;
            const int height = 340;
            var pixels = new Color32[width * height];
            var centre = new Vector2(width * 0.5f, height * 0.5f);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), centre);
                    pixels[y * width + x] = WorkshopBackdropGradientColor(distance);
                }
            }

            var gridColor = new Color(0.404f, 0.91f, 0.976f, 0.055f);
            for (var x = 0; x <= width; x += 40)
            {
                for (var y = 0; y < height; y++)
                    BlendWorkshopPixel(pixels, width, x, y, gridColor);
            }
            for (var y = 0; y <= height; y += 40)
            {
                for (var x = 0; x < width; x++)
                    BlendWorkshopPixel(pixels, width, x, y, gridColor);
            }
            for (var index = 0; index < 18; index++)
            {
                var x = (index * 83 + 29) % width;
                var y = (index * 47 + 17) % height;
                var color = new Color(0.647f, 0.953f, 0.988f, 0.16f + (index % 5) * 0.035f);
                BlendWorkshopPixel(pixels, width, x, y, color);
                if (index % 3 == 0) BlendWorkshopPixel(pixels, width, x + 1, y, color);
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "VoidFall_Workshop_Preview_Wide_Backdrop_Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            _workshopPreviewWideBackdrop = Sprite.Create(
                texture,
                new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f),
                1f);
            _workshopPreviewWideBackdrop.name = "VoidFall_Workshop_Preview_Wide_Backdrop";
            return _workshopPreviewWideBackdrop;
        }

        public static Color WorkshopBackdropGradientColor(float distance)
        {
            // React uses createRadialGradient(center, 20, center, width * .55)
            // with stops at 0, .48, and 1. Preserve the solid 20px inner stop
            // instead of normalizing the distance from the centre to zero.
            const float innerRadius = 20f;
            const float outerRadius = 330f;
            var inner = new Color(0.055f, 0.259f, 0.345f, 0.34f);
            var middle = new Color(0.027f, 0.051f, 0.118f, 0.92f);
            var outer = new Color(0.012f, 0.024f, 0.063f, 1f);
            var normalized = Mathf.InverseLerp(innerRadius, outerRadius, distance);
            return normalized <= 0.48f
                ? Color.Lerp(inner, middle, normalized / 0.48f)
                : Color.Lerp(middle, outer, Mathf.InverseLerp(0.48f, 1f, normalized));
        }

        public static Sprite WorkshopPreviewMobilityTrail()
        {
            if (_workshopPreviewMobilityTrail != null) return _workshopPreviewMobilityTrail;

            const int width = 16;
            const int height = 256;
            var pixels = new Color32[width * height];
            var start = new Color(0.404f, 0.91f, 0.976f, 0.78f);
            var end = new Color(0.133f, 0.827f, 0.933f, 0f);
            for (var y = 0; y < height; y++)
            {
                var color = Color.Lerp(start, end, y / (float)(height - 1));
                for (var x = 0; x < width; x++)
                {
                    var edge = Mathf.Clamp01(Mathf.Abs(x - (width - 1) * 0.5f) / (width * 0.5f));
                    var edgeAlpha = Mathf.SmoothStep(1f, 0f, edge * edge);
                    pixels[y * width + x] = new Color(color.r, color.g, color.b, color.a * edgeAlpha);
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "VoidFall_Workshop_Mobility_Trail_Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            _workshopPreviewMobilityTrail = Sprite.Create(
                texture,
                new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f),
                1f);
            _workshopPreviewMobilityTrail.name = "VoidFall_Workshop_Mobility_Trail";
            return _workshopPreviewMobilityTrail;
        }

        public static Sprite WorkshopPreviewLayer(string id, int rank)
        {
            var safeId = string.IsNullOrEmpty(id) ? string.Empty : id;
            var maxRank = safeId == "protocol" ? 1 : 3;
            var safeRank = Mathf.Clamp(rank, 0, maxRank);
            if (safeRank <= 0) return null;
            var key = safeId + "/" + safeRank;
            if (WorkshopPreviewLayerSprites.TryGetValue(key, out var cached)) return cached;

            const float frameRadius = 150f;
            var canvas = new RasterCanvas(frameRadius, 0, 360);
            switch (safeId)
            {
                case "mobility":
                    for (var index = 0; index < safeRank; index++)
                    {
                        var offset = (index - (safeRank - 1) * 0.5f) * 18f;
                        var length = 35f + safeRank * 9f;
                        canvas.DrawGradientLine(
                            new Vector2(offset, -27f),
                            new Vector2(offset * 1.14f, -28f - length),
                            4f,
                            new Color(0.404f, 0.91f, 0.976f, 0.78f),
                            new Color(0.133f, 0.827f, 0.933f, 0f));
                    }
                    break;
                case "magnet":
                    {
                        var radius = 76f + safeRank * 9f;
                        canvas.DrawDashedArc(Vector2.zero, radius, 0f, Mathf.PI * 2f, 2f,
                            new Color(0.655f, 0.545f, 0.98f, 0.22f + safeRank * 0.08f), 5f, 12f);
                        for (var index = 0; index < safeRank * 2; index++)
                        {
                            var angle = index / (float)(safeRank * 2) * Mathf.PI * 2f;
                            var marker = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                            var markerColor = index % 2 == 0
                                ? ParseColor("#c4b5fd")
                                : ParseColor("#67e8f9");
                            canvas.Glow(marker, 12f, new Color(0.655f, 0.545f, 0.98f, 1f), 0.38f);
                            canvas.FillPolygon(new[]
                            {
                                marker + new Vector2(0, 5), marker + new Vector2(4, 0),
                                marker + new Vector2(0, -5), marker + new Vector2(-4, 0),
                            }, markerColor);
                        }
                    }
                    break;
                case "integrity":
                    {
                        var segmentCount = 3 + safeRank * 2;
                        for (var index = 0; index < segmentCount; index++)
                        {
                            var start = index / (float)segmentCount * Mathf.PI * 2f;
                            var width = 3f + safeRank * 0.6f;
                            canvas.DrawArc(Vector2.zero, 52f, start, start + 0.42f,
                                width + 6f, new Color(0.22f, 0.74f, 0.97f, 0.14f));
                            canvas.DrawArc(Vector2.zero, 52f, start, start + 0.42f,
                                width,
                                new Color(0.49f, 0.83f, 0.99f, 0.42f + safeRank * 0.13f));
                        }
                    }
                    break;
                case "recovery":
                    for (var index = 0; index < safeRank; index++)
                    {
                        var angle = index / (float)Mathf.Max(1, safeRank) * Mathf.PI * 2f;
                        var marker = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 42f;
                        canvas.Glow(marker, 13f, new Color(0.204f, 0.827f, 0.6f, 1f), 0.42f);
                        canvas.FillRect(marker, 8f, 8f, ParseColor("#6ee7b7"));
                        canvas.FillRect(marker + new Vector2(0, -0.1f), 2f, 6f, ParseColor("#052e2b"));
                        canvas.FillRect(marker, 6f, 2f, ParseColor("#052e2b"));
                    }
                    break;
                case "power":
                    for (var sideIndex = -1; sideIndex <= 1; sideIndex += 2)
                    {
                        for (var index = 0; index < safeRank; index++)
                        {
                            var y = (index - (safeRank - 1) * 0.5f) * 13f;
                            var block = new Vector2(sideIndex * (31f + index * 7f), y);
                            canvas.Glow(block, 13f, new Color(0.984f, 0.573f, 0.235f, 1f), 0.32f);
                            canvas.FillRect(block, 12f, 10f,
                                new Color(0.98f, 0.57f, 0.24f, 0.2f));
                            canvas.DrawLine(new Vector2(sideIndex * (32f + index * 7f), y),
                                new Vector2(sideIndex * (48f + index * 8f), y), 3f,
                                new Color(0.984f, 0.573f, 0.235f, 1f));
                        }
                    }
                    break;
                case "precision":
                    {
                        var tickCount = 2 + safeRank * 2;
                        var precisionColor = WorkshopPrecisionColor();
                        for (var index = 0; index < tickCount; index++)
                        {
                            var angle = index / (float)tickCount * Mathf.PI * 2f;
                            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                            canvas.DrawLine(direction * 58f, direction * 66f, 2.5f,
                                WithAlpha(precisionColor, 0.4f + safeRank * 0.14f));
                        }
                    }
                    break;
                case "arsenal":
                    for (var index = 0; index < safeRank; index++)
                    {
                        var angle = index / (float)Mathf.Max(1, safeRank) * Mathf.PI * 2f;
                        var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                        var centre = direction * 58f;
                        DrawWorkshopBlade(canvas, centre, angle);
                    }
                    break;
                case "protocol":
                    canvas.DrawDashedArc(Vector2.zero, 74f, 0f, Mathf.PI * 2f, 2.5f,
                        WithAlpha(WorkshopProtocolColor(), 0.32f), 7f, 9f);
                    break;
            }

            var sprite = canvas.ToSprite("VoidFall_Workshop_Preview_Layer_" + key.Replace('/', '_'));
            WorkshopPreviewLayerSprites[key] = sprite;
            return sprite;
        }

        public static Color WorkshopPrecisionColor()
        {
            return ParseColor("#fde68a");
        }

        public static Color WorkshopProtocolColor()
        {
            return ParseColor("#fb7185");
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static void DrawWorkshopBlade(RasterCanvas canvas, Vector2 centre, float radialAngle)
        {
            // GameUI draws the real 44x22 blade sprite at 32x16, then rotates
            // it by radialAngle + PI/2. Preserve that tangential orientation,
            // two-tone silhouette, outline, and dark emitter instead of using
            // a single radial polygon approximation.
            const float sourceScale = 32f / 44f;
            var radial = new Vector2(Mathf.Cos(radialAngle), Mathf.Sin(radialAngle));
            var tangent = new Vector2(-radial.y, radial.x);
            Vector2 Transform(Vector2 source)
            {
                var scaled = source * sourceScale;
                return centre + tangent * scaled.x - radial * scaled.y;
            }

            var outer = new[]
            {
                Transform(new Vector2(-19, 2)), Transform(new Vector2(-12, -4)),
                Transform(new Vector2(15, -6)), Transform(new Vector2(21, -2)),
                Transform(new Vector2(11, 2)), Transform(new Vector2(-13, 6)),
            };
            var inner = new[]
            {
                Transform(new Vector2(-14, 1)), Transform(new Vector2(-8, -2)),
                Transform(new Vector2(16, -4)), Transform(new Vector2(20, -2)),
                Transform(new Vector2(10, 1)), Transform(new Vector2(-9, 4)),
            };
            canvas.Glow(centre, 15f, ParseColor("#bae6fd"), 0.3f);
            canvas.FillPolygon(outer, new Color(34f / 255f, 211f / 255f, 238f / 255f, 0.22f));
            canvas.FillPolygon(inner, ParseColor("#bae6fd"));
            canvas.StrokePolygon(inner, Color.white, 1.2f);
            canvas.FillCircle(Transform(new Vector2(-9, 1.2f)), 1.8f * sourceScale, ParseColor("#0f172a"));
        }

        public static Sprite WorkshopPreviewCore(int power)
        {
            var safePower = Mathf.Clamp(power, 0, 3);
            if (WorkshopPreviewCoreSprites[safePower] != null) return WorkshopPreviewCoreSprites[safePower];
            const float frameRadius = 150f;
            var canvas = new RasterCanvas(frameRadius, 0, 360);
            canvas.Glow(37f, new Color(0.13f, 0.83f, 0.95f, 1), 0.58f);
            for (var index = 0; index < 3; index++)
            {
                var start = index / 3f * Mathf.PI * 2f;
                canvas.DrawArc(Vector2.zero, 43f, start, start + Mathf.PI * 0.7f, 1.6f,
                    new Color(0.65f, 0.95f, 0.98f, 0.5f));
            }
            canvas.RadialTwoPointColorGradient(
                new Vector2(-4f, 5f),
                2f,
                new Color(0.925f, 0.996f, 1f, 1),
                Vector2.zero,
                15f,
                new Color(0.055f, 0.455f, 0.565f, 1),
                Vector2.zero,
                15f);
            canvas.StrokeCircle(Vector2.zero, 15f, new Color(0.647f, 0.953f, 0.988f, 1), 2.5f);
            canvas.FillCircle(Vector2.zero, 6.75f, new Color(0.02f, 0.024f, 0.06f, 1));
            canvas.FillCircle(new Vector2(1.2f, 1.4f), 3f, new Color(0.878f, 0.98f, 1f, 1));
            if (safePower > 0)
            {
                canvas.Glow(10f, new Color(0.98f, 0.57f, 0.24f, 1), 0.55f);
                canvas.FillCircle(Vector2.zero, 3f + safePower,
                    new Color(1f, 0.93f, 0.84f, 0.35f + safePower * 0.12f));
            }
            WorkshopPreviewCoreSprites[safePower] = canvas.ToSprite("VoidFall_Workshop_Preview_Core_" + safePower);
            return WorkshopPreviewCoreSprites[safePower];
        }

        public static Sprite Enemy(string id)
        {
            return Enemy(id, SourceEnemyColor(id), false);
        }

        public static Sprite Enemy(string id, Color accent, bool hit)
        {
            var safeId = string.IsNullOrEmpty(id) ? "unknown" : id;
            var key = new EnemyCacheKey(safeId, (Color32)accent, hit);
            if (EnemySprites.TryGetValue(key, out var sprite)) return sprite;
            sprite = BuildEnemy(safeId, accent, hit);
            EnemySprites[key] = sprite;
            return sprite;
        }

        public static Sprite RosterTwoEnemy(string id, bool hit)
        {
            var safeId = string.IsNullOrEmpty(id) ? "unknown" : id;
            var key = new RosterTwoCacheKey(safeId, hit);
            if (RosterTwoEnemySprites.TryGetValue(key, out var sprite)) return sprite;
            sprite = BuildRosterTwoEnemy(safeId, hit);
            RosterTwoEnemySprites[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// Returns the browser enemy canvas width in world-pixel units. The
        /// runtime Sprite uses one Unity unit for the generated canvas, so the
        /// renderer applies this value as its transform scale.
        /// </summary>
        public static float EnemyCanvasSize(string id)
        {
            var radius = EnemyRadius(string.IsNullOrEmpty(id) ? "unknown" : id);
            return (radius + radius * 1.1f + 14f) * 2f;
        }

        /// <summary>
        /// Browser enemySprite() calls glow() with the complete source canvas
        /// radius, r + pad, rather than only the body radius plus its fixed
        /// 14px term. Keep the contract explicit so the standard and Roster II
        /// builders cannot silently shorten the authored halo.
        /// </summary>
        public static float EnemyGlowRadius(string id)
        {
            var radius = EnemyRadius(string.IsNullOrEmpty(id) ? "unknown" : id);
            return radius + radius * 1.1f + 14f;
        }

        public static float RosterTwoEnemyCanvasSize(string id)
        {
            var radius = string.IsNullOrEmpty(id)
                ? 15f
                : id == "guard" ? 18f : id == "gunner" ? 14f : 15f;
            return (radius + radius * 1.1f + 14f) * 2f;
        }

        public static float RosterTwoEnemyGlowRadius(string id)
        {
            var radius = string.IsNullOrEmpty(id)
                ? 15f
                : id == "guard" ? 18f : id == "gunner" ? 14f : 15f;
            return radius + radius * 1.1f + 14f;
        }

        public static float BossCanvasSize(string id)
        {
            switch (id)
            {
                case "hydra-prime": return 240f;
                case "court-grandmaster-black":
                case "court-grandmaster-white": return 210f;
                case "herald": return 152f;
                case "warden": return 176f;
                case "matriarch": return 184f;
                case "reaver": return 168f;
                default: return 156f;
            }
        }

        public static Sprite Boss(string id)
        {
            return Boss(id, SourceBossColor(id), false);
        }

        public static Sprite Boss(string id, Color accent, bool hit)
        {
            var safeId = string.IsNullOrEmpty(id) ? "unknown" : id;
            var key = new EnemyCacheKey(safeId, (Color32)accent, hit);
            if (BossSprites.TryGetValue(key, out var sprite)) return sprite;
            sprite = BuildBoss(safeId, accent, hit);
            BossSprites[key] = sprite;
            return sprite;
        }

        public static Sprite Pickup(string kind)
        {
            var safeKind = string.IsNullOrEmpty(kind) ? "xp" : kind;
            if (PickupSprites.TryGetValue(safeKind, out var sprite)) return sprite;
            sprite = BuildPickup(safeKind);
            PickupSprites[safeKind] = sprite;
            return sprite;
        }

        public static Sprite Projectile(string kind)
        {
            var safeKind = string.IsNullOrEmpty(kind) ? "hostile" : kind;
            if (ProjectileSprites.TryGetValue(safeKind, out var sprite)) return sprite;
            sprite = BuildProjectile(safeKind);
            ProjectileSprites[safeKind] = sprite;
            return sprite;
        }

        public static Sprite ProjectileFrame(string kind, int frame)
        {
            var safeKind = string.IsNullOrEmpty(kind) ? "hostile" : kind;
            if (!ProjectileFrameSets.TryGetValue(safeKind, out var frames))
            {
                frames = BuildProjectileFrames(safeKind);
                ProjectileFrameSets[safeKind] = frames;
            }

            var safeFrame = Mathf.Abs(frame) % ProjectileFrameCount;
            return frames[safeFrame];
        }

        public static void WarmProjectileFrames()
        {
            foreach (var kind in new[] { "pistol", "scattergun", "railgun", "seeker", "gunner" })
                ProjectileFrame(kind, 0);
        }

        /// <summary>
        /// Pre-bakes every procedural sprite the render path can request, so
        /// gameplay never pays texture-rasterization cost on first sighting.
        ///
        /// The ids and accents are driven from <see cref="ContentCatalog"/>
        /// rather than hardcoded lists for two reasons. Hardcoded lists silently
        /// go stale when content is added. More importantly, the cache key must
        /// match what the render path passes: VoidFallGameRuntime resolves enemy
        /// and boss accents through ContentCatalog, so warming with the local
        /// SourceEnemyColor/SourceBossColor switches would bake an unused key
        /// and leave the real one cold the moment the two disagree.
        /// </summary>
        public static void WarmAllSprites()
        {
            var steps = WarmAllSpritesSteps();
            while (steps.MoveNext())
            {
            }

            // One page upload for everything baked above, rather than one per
            // sprite. Apply always re-uploads the whole page.
            FlushAtlas();
        }

        /// <summary>
        /// The warm work, split into resumable steps. Yields the number of
        /// sprites rasterized by each step so a caller can spend a fixed time
        /// budget per frame instead of blocking startup for the whole set.
        ///
        /// This is the single definition of what gets warmed;
        /// <see cref="WarmAllSprites"/> just drains it. Keeping one body means
        /// the incremental and blocking paths cannot drift in coverage.
        ///
        /// The caller is responsible for calling <see cref="FlushAtlas"/> once
        /// the sequence is exhausted. Flushing per step would re-upload the
        /// whole atlas page every frame.
        /// </summary>
        public static IEnumerator<int> WarmAllSpritesSteps()
        {
            // Each call builds all ProjectileFrameCount frames for the kind, so
            // this is five large steps rather than one enormous one.
            foreach (var kind in new[] { "pistol", "scattergun", "railgun", "seeker", "gunner" })
            {
                ProjectileFrame(kind, 0);
                yield return ProjectileFrameCount;
            }

            foreach (var definition in ContentCatalog.Enemies)
            {
                var accent = ParseColor(definition.Color);
                Enemy(definition.Id, accent, false);
                Enemy(definition.Id, accent, true);
                var built = 2;

                // Elite variants keep their base enemy body and base accent, so
                // the two calls above already cover them. Roster II silhouettes
                // are a separate cache keyed only by id and hit state.
                if (EnemyRosterRules.RosterTwoEligible(definition.Id))
                {
                    RosterTwoEnemy(definition.Id, false);
                    RosterTwoEnemy(definition.Id, true);
                    built += 2;
                }

                yield return built;
            }

            foreach (var definition in MonochromeContent.Enemies)
            {
                Enemy(definition.Id + "-black", Color.white, false);
                Enemy(definition.Id + "-black", Color.white, true);
                Enemy(definition.Id + "-white", Color.black, false);
                Enemy(definition.Id + "-white", Color.black, true);
                yield return 4;
            }

            // The scheduled charging Elite is the only entity that uses the
            // generic "elite" silhouette instead of a base enemy body.
            var eliteAccent = ParseColor(ContentCatalog.Elite.Color);
            Enemy("elite", eliteAccent, false);
            Enemy("elite", eliteAccent, true);
            yield return 2;

            // The harvester-full and exploder-armed overlays request a white
            // accent rather than the catalog colour, so they are separate cache
            // keys from the bodies warmed above. Without these two the overlays
            // would be the only sprites baked mid-run, and since the atlas is
            // flushed at the start of Render they would be invisible for the
            // single frame they were created on.
            Enemy("harvester", Color.white, true);
            Enemy("exploder", Color.white, true);
            RosterTwoEnemy("exploder", true);
            yield return 3;

            foreach (var boss in ContentCatalog.Bosses)
            {
                var accent = ParseColor(boss.Color);
                Boss(boss.Id, accent, false);
                Boss(boss.Id, accent, true);
                yield return 2;
            }
            foreach (var boss in new[] { MonochromeContent.BlackBoss, MonochromeContent.WhiteBoss })
            {
                var accent = ParseColor(boss.Color);
                Boss(boss.Id, accent, false);
                Boss(boss.Id, accent, true);
                yield return 2;
            }
            for (var tier = 0; tier < 3; tier++)
                Gem(tier);
            yield return 3;

            foreach (var kind in new[] { "xp", "part", "magnet", "repair", "bomb", "overdrive" })
                Pickup(kind);
            yield return 6;

            Projectile("curved");
            Projectile("hydra-rib");
            for (var shard = 0; shard < 4; shard++)
                MeteorShard(shard);
            yield return 6;

            for (var variant = 0; variant < 4; variant++)
            {
                Meteor(variant, false);
                Meteor(variant, true);
                yield return 2;
            }

            Blade(false);
            Blade(true);
            EliteRing();
            BlastWaveDisc();
            yield return 4;

            PlayerAura(false);
            PlayerAura(true);
            yield return 2;
        }

        public static float ProjectileCanvasSize(string kind)
        {
            switch (kind)
            {
                case "hydra-rib": return 30f;
                case "pistol": return 33f;
                case "scattergun": return 26f;
                case "railgun": return 64f;
                case "seeker": return 48f;
                case "gunner": return 36f;
                case "curved": return 20f;
                default: return 36f;
            }
        }

        public static Sprite MeteorShard(int variant)
        {
            var safeVariant = Mathf.Abs(variant) % MeteorShardSprites.Length;
            if (MeteorShardSprites[safeVariant] != null) return MeteorShardSprites[safeVariant];
            MeteorShardSprites[safeVariant] = BuildMeteorShard(safeVariant);
            return MeteorShardSprites[safeVariant];
        }

        public static Sprite Meteor(int variant, bool explosive)
        {
            var safeVariant = Mathf.Max(0, variant);
            var key = safeVariant + "/" + explosive;
            if (MeteorSprites.TryGetValue(key, out var sprite)) return sprite;
            sprite = BuildMeteor(safeVariant, explosive);
            MeteorSprites[key] = sprite;
            return sprite;
        }

        public static float MeteorCanvasSize(int variant, bool explosive)
        {
            var diameter = explosive
                ? new[] { 72f, 80f, 88f }[Mathf.Abs(variant) % 3]
                : new[] { 48f, 54f, 58f, 64f }[Mathf.Abs(variant) % 4];
            return diameter + (explosive ? 20f : 14f);
        }

        public static Sprite MeteorCore()
        {
            if (_meteorCore != null) return _meteorCore;
            // meteorHotCoreSprite() uses cv(r * 2) with r = 46, so preserve
            // the browser's 92px source canvas instead of oversampling it into
            // a different raster contract. Sprite PPU keeps runtime world
            // size unchanged.
            var canvas = new RasterCanvas(46f, 0, 92);
            // Browser meteorHotCoreSprite(): one radial gradient with four
            // source stops, later drawn with globalCompositeOperation=lighter.
            canvas.RadialFourStopGradient(
                Vector2.zero,
                46f,
                new Color(1f, 237f / 255f, 182f / 255f, 0.95f),
                new Color(249f / 255f, 146f / 255f, 50f / 255f, 0.5f),
                0.34f,
                new Color(180f / 255f, 60f / 255f, 20f / 255f, 0.16f),
                0.7f,
                new Color(120f / 255f, 30f / 255f, 10f / 255f, 0f));
            _meteorCore = canvas.ToSprite("VoidFall_Meteor_Core", true);
            return _meteorCore;
        }

        public static Sprite Dot()
        {
            if (_dot != null) return _dot;
            // dotSprite() uses cv(24).
            var canvas = new RasterCanvas(12f, 0, 24);
            canvas.Glow(12f, Color.white, 0.9f);
            canvas.FillCircle(Vector2.zero, 4.5f, Color.white);
            _dot = canvas.ToSprite("VoidFall_Fx_Dot");
            return _dot;
        }

        // Browser dotSprite(color): white-hot centre, arena-tinted middle, and
        // a transparent outer falloff. Arena stars and non-petal motes use
        // colour-baked sprites because Canvas2D receives the colour in the
        // sprite itself, then applies only the animated global alpha.
        public static Sprite ArenaDot(Color tint)
        {
            var key = (Color32)tint;
            key.a = 255;
            if (ArenaDotSprites.TryGetValue(key, out var cached)) return cached;

            var colour = new Color(
                key.r / 255f,
                key.g / 255f,
                key.b / 255f,
                1f);
            // Arena motes use the same dotSprite() source canvas (24px).
            var canvas = new RasterCanvas(12f, 0, 24);
            canvas.RadialThreeStopGradient(
                Vector2.zero,
                0.5f,
                new Color(1f, 1f, 1f, 0.95f),
                3.6f,
                new Color(colour.r, colour.g, colour.b, 0.9f),
                12f,
                new Color(colour.r, colour.g, colour.b, 0f));
            var name = "VoidFall_Fx_Arena_Dot_" + key.r + "_" + key.g + "_" + key.b;
            cached = canvas.ToSprite(name, true);
            ArenaDotSprites.Add(key, cached);
            return cached;
        }

        // Browser dotSprite() uses a white-hot centre, a tinted middle stop,
        // and a transparent outer falloff. The additive particle shader applies
        // the per-particle tint between those stops; this texture carries the
        // source alpha profile without baking one burst colour into the atlas.
        public static Sprite ParticleDot()
        {
            if (_particleDot != null) return _particleDot;
            // Source particle dots also use the 24px dotSprite() alpha box.
            var canvas = new RasterCanvas(12f, 0, 24);
            canvas.RadialAlphaGradient(0.5f, 0.95f, 3.6f, 0.9f, 12f);
            _particleDot = canvas.ToSprite("VoidFall_Fx_Particle_Dot", true);
            return _particleDot;
        }

        public static Sprite ArenaCurrentGlow()
        {
            if (_arenaCurrentGlow != null) return _arenaCurrentGlow;
            var canvas = new RasterCanvas(1f, 0.15f, 96);
            // Browser drawMotes() uses a two-stop radial gradient for the
            // travelling current: opaque at the centre, transparent at reach.
            // This is intentionally not the shared three-stop glow profile.
            canvas.RadialGradient(0f, 1f, Color.white, 1f);
            _arenaCurrentGlow = canvas.ToSprite("VoidFall_Arena_Current_Glow", true);
            return _arenaCurrentGlow;
        }

        public static Sprite ArenaVignette(ArenaId arena)
        {
            var slot = Mathf.Clamp((int)arena, 0, ArenaVignettes.Length - 1);
            if (ArenaVignettes[slot] != null) return ArenaVignettes[slot];

            var width = 256;
            var height = 144;
            var pixels = new Color32[width * height];
            var vignetteAlpha = arena == ArenaId.Void ? 1f : arena == ArenaId.RedNebula ? 0.95f : 0.62f;
            var centreWellAlpha = arena == ArenaId.Void ? 0f : 0.3f;
            var pale = arena == ArenaId.WhiteSakura;
            var edgeShade = pale ? new Color(58f / 255f, 52f / 255f, 64f / 255f, 1) :
                new Color(2f / 255f, 3f / 255f, 8f / 255f, 1);
            var wellShade = new Color(6f / 255f, 4f / 255f, 8f / 255f, 1);
            var reach = Mathf.Sqrt(width * width + height * height) * 0.62f;
            var centre = new Vector2(width * 0.5f, height * 0.5f);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var distance = (new Vector2(x + 0.5f, y + 0.5f) - centre).magnitude;
                    var edgeT = Mathf.Clamp01((distance - reach * 0.32f) / (reach * 0.68f));
                    var edgeAlpha = distance <= reach * 0.32f
                        ? 0
                        : distance >= reach
                            ? 0.72f * vignetteAlpha
                            : Mathf.Lerp(0, 0.72f * vignetteAlpha, edgeT);
                    if (distance > reach) edgeAlpha = 0.72f * vignetteAlpha;

                    var wellT = Mathf.Clamp01(distance / (reach * 0.52f));
                    var wellAlpha = centreWellAlpha <= 0
                        ? 0
                        : wellT <= 0.62f
                            ? Mathf.Lerp(centreWellAlpha, centreWellAlpha * 0.45f, wellT / 0.62f)
                            : Mathf.Lerp(centreWellAlpha * 0.45f, 0, (wellT - 0.62f) / 0.38f);
                    var outputAlpha = edgeAlpha + wellAlpha * (1f - edgeAlpha);
                    if (outputAlpha <= 0.0001f)
                    {
                        pixels[y * width + x] = new Color32(255, 255, 255, 0);
                        continue;
                    }
                    var edgeWeight = edgeAlpha / outputAlpha;
                    var wellWeight = wellAlpha * (1f - edgeAlpha) / outputAlpha;
                    var output = edgeShade * edgeWeight + wellShade * wellWeight;
                    pixels[y * width + x] = new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt(output.r * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(output.g * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(output.b * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(outputAlpha * 255f), 0, 255));
                }
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "VoidFall_Arena_Vignette_" + arena + "_Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            ArenaVignettes[slot] = Sprite.Create(
                texture,
                new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f),
                1f);
            ArenaVignettes[slot].name = "VoidFall_Arena_Vignette_" + arena;
            return ArenaVignettes[slot];
        }

        public static Sprite RedHealthVignette()
        {
            if (_redHealthVignette != null) return _redHealthVignette;

            // Browser buildGradients() creates a 512x288 radial image with
            // transparent red at radius 80, a 0.20 red stop at 0.8 of the
            // 310 px reach, and a dark-red 0.52 stop at the edge. Keep this
            // as a colour-and-alpha texture so the UI overlay's global alpha
            // scales the entire source image like Canvas drawImage().
            const int width = 512;
            const int height = 288;
            const float centreX = 256f;
            const float centreY = 144f;
            const float innerRadius = 80f;
            const float outerRadius = 310f;
            var pixels = new Color32[width * height];
            var bright = new Color(239f / 255f, 68f / 255f, 68f / 255f, 1f);
            var dark = new Color(153f / 255f, 27f / 255f, 27f / 255f, 1f);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var distance = new Vector2(x + 0.5f - centreX, y + 0.5f - centreY).magnitude;
                    var t = Mathf.Clamp01((distance - innerRadius) / (outerRadius - innerRadius));
                    var color = t <= 0.8f
                        ? bright
                        : Color.Lerp(bright, dark, (t - 0.8f) / 0.2f);
                    var alpha = t <= 0.8f
                        ? Mathf.Lerp(0f, 0.2f, t / 0.8f)
                        : Mathf.Lerp(0.2f, 0.52f, (t - 0.8f) / 0.2f);
                    pixels[y * width + x] = new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt(color.r * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(color.g * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(color.b * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255));
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "VoidFall_Red_Health_Vignette_Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            _redHealthVignette = Sprite.Create(
                texture,
                new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f),
                1f);
            _redHealthVignette.name = "VoidFall_Red_Health_Vignette";
            return _redHealthVignette;
        }

        private static float StellarNext(ref uint state)
        {
            unchecked
            {
                state += 0x6d2b79f5u;
                var value = state;
                value = (value ^ (value >> 15)) * (value | 1u);
                value ^= value + ((value ^ (value >> 7)) * (value | 61u));
                return (value ^ (value >> 14)) / 4294967296f;
            }
        }

        public static Sprite ImpactMark()
        {
            if (_impactMark != null) return _impactMark;
            // The browser draws the irregular ground shape at mark.radius and
            // applies fade * 0.72 at draw time. Keep the baked texture opaque
            // and unpadded so the runtime scale preserves that source radius
            // and opacity instead of multiplying both down a second time.
            var canvas = new RasterCanvas(1f, 0f, 64);
            var points = new Vector2[10];
            var scales = new[] { 0.78f, 0.7f, 0.62f, 0.7f, 0.78f, 0.7f, 0.62f, 0.7f, 0.78f, 0.7f };
            for (var index = 0; index < points.Length; index++)
            {
                var angle = index / (float)points.Length * Mathf.PI * 2f;
                points[index] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * scales[index];
            }
            canvas.FillPolygon(points, new Color(0.008f, 0.02f, 0.04f, 1f));
            _impactMark = canvas.ToSprite("VoidFall_Impact_Mark");
            return _impactMark;
        }

        public static Sprite DamageIndicator()
        {
            if (_damageIndicator != null) return _damageIndicator;
            // Browser drawDamageIndicators uses a 30x30 triangle with points
            // (-24,0), (6,-15), and (6,15), filled with no extra stroke.
            // Build a 48-unit frame so the HUD image renders that triangle at
            // the same pixel size instead of shrinking it into a padded box.
            var canvas = new RasterCanvas(24f, 0f, 96);
            canvas.FillPolygon(new[]
            {
                new Vector2(-24f, 0),
                new Vector2(6f, -15f),
                new Vector2(6f, 15f),
            }, Color.white);
            _damageIndicator = canvas.ToSprite("VoidFall_Damage_Indicator");
            return _damageIndicator;
        }

        private static Sprite BuildEnemy(string id, Color accent, bool hit)
        {
            var sourceId = CourtBaseEnemyId(id);
            var whiteCourt = CourtWhiteSprite(id);
            var radius = EnemyRadius(sourceId);
            // React enemySprite() uses cv((r + r * 1.1 + 14) * 2), whose
            // canvas is ceil-sized. Preserve each authored source raster;
            // the runtime renderer still applies SourceEnemySpriteWorldSize.
            var canvas = new RasterCanvas(
                radius,
                radius * 1.1f + 14f,
                Mathf.CeilToInt(EnemyCanvasSize(sourceId)));
            if (!hit) canvas.Glow(EnemyGlowRadius(id), accent, 0.3f);

            // Browser enemySprite(): hit #f8fafc, normal #080c18.
            var body = hit ? ParseColor("#f8fafc") : whiteCourt ? ParseColor("#f1f0ea") : ParseColor("#080c18");
            var outline = hit ? Color.white : whiteCourt ? ParseColor("#080c18") : accent;
            FillEnemyShape(canvas, sourceId, radius, body);
            StrokeEnemyShape(canvas, sourceId, radius, outline, Mathf.Max(2f, radius * 0.14f));
            canvas.FillCircle(new Vector2(-radius * 0.08f, radius * 0.04f), radius * 0.52f,
                hit ? ParseColor("#dbeafe") : whiteCourt ? ParseColor("#d5d4ce") : ParseColor("#111827"));
            DrawEnemyDetails(canvas, sourceId, radius, outline, hit);
            return canvas.ToAtlasSprite("VoidFall_Enemy_" + id + (hit ? "_Hit" : ""));
        }

        private static Sprite BuildRosterTwoEnemy(string id, bool hit)
        {
            var radius = id == "guard" ? 18f : id == "gunner" ? 14f : 15f;
            var accent = id == "gunner"
                ? ParseColor("#f59e0b")
                : id == "guard"
                    ? ParseColor("#38bdf8")
                    : id == "exploder"
                        ? ParseColor("#fb923c")
                        : ParseColor("#fb7185");
            // Roster II reuses the browser enemySprite() canvas contract with
            // its own authored radius (15/14/18/15px).
            var canvas = new RasterCanvas(
                radius,
                radius * 1.1f + 14f,
                Mathf.CeilToInt(RosterTwoEnemyCanvasSize(id)));
            if (!hit) canvas.Glow(RosterTwoEnemyGlowRadius(id), accent, 0.3f);

            Vector2[] points;
            switch (id)
            {
                case "chaser":
                    points = new[]
                    {
                        new Vector2(-radius * 1.02f, -radius * 0.42f),
                        new Vector2(radius * 0.2f, -radius * 0.62f),
                        new Vector2(radius * 1.24f, -radius * 1.02f),
                        new Vector2(radius * 0.76f, -radius * 0.18f),
                        new Vector2(radius * 0.34f, 0),
                        new Vector2(radius * 0.78f, radius * 0.2f),
                        new Vector2(radius * 1.08f, radius * 0.82f),
                        new Vector2(radius * 0.12f, radius * 0.58f),
                        new Vector2(-radius * 1.12f, radius * 0.24f),
                    };
                    break;
                case "gunner":
                    points = new[]
                    {
                        new Vector2(-radius, -radius * 0.68f),
                        new Vector2(radius * 0.12f, -radius * 0.84f),
                        new Vector2(radius * 0.48f, -radius * 0.48f),
                        new Vector2(radius * 1.38f, -radius * 0.55f),
                        new Vector2(radius * 1.42f, -radius * 0.25f),
                        new Vector2(radius * 0.5f, -radius * 0.08f),
                        new Vector2(radius * 1.3f, radius * 0.14f),
                        new Vector2(radius * 1.18f, radius * 0.46f),
                        new Vector2(radius * 0.42f, radius * 0.28f),
                        new Vector2(radius * 0.02f, radius * 0.72f),
                        new Vector2(-radius * 0.92f, radius * 0.58f),
                    };
                    break;
                case "guard":
                    points = new[]
                    {
                        new Vector2(-radius * 1.02f, -radius * 0.48f),
                        new Vector2(-radius * 0.28f, -radius * 0.88f),
                        new Vector2(radius * 0.62f, -radius),
                        new Vector2(radius * 1.12f, -radius * 0.58f),
                        new Vector2(radius * 1.28f, radius * 0.08f),
                        new Vector2(radius * 0.82f, radius * 0.86f),
                        new Vector2(radius * 0.05f, radius),
                        new Vector2(-radius * 0.82f, radius * 0.5f),
                    };
                    break;
                case "exploder":
                    points = new[]
                    {
                        new Vector2(-radius * 0.48f, -radius),
                        new Vector2(radius * 0.5f, -radius * 0.9f),
                        new Vector2(radius, -radius * 0.28f),
                        new Vector2(radius * 0.88f, radius * 0.58f),
                        new Vector2(radius * 0.24f, radius),
                        new Vector2(-radius * 0.62f, radius * 0.78f),
                        new Vector2(-radius, radius * 0.08f),
                        new Vector2(-radius * 0.86f, -radius * 0.62f),
                    };
                    break;
                default:
                    return BuildEnemy(id, accent, hit);
            }

            // Keep Roster II on the same shared browser enemySprite palette.
            var body = hit ? ParseColor("#f8fafc") : ParseColor("#080c18");
            canvas.FillPolygon(points, body);
            canvas.StrokePolygon(points, hit ? Color.white : accent, Mathf.Max(2f, radius * 0.14f));
            canvas.FillCircle(
                new Vector2(-radius * 0.08f, radius * 0.04f),
                radius * 0.52f,
                hit ? ParseColor("#dbeafe") : ParseColor("#111827"));

            if (id == "chaser")
            {
                var detail = hit ? Color.white : ParseColor("#fecdd3");
                canvas.DrawLine(
                    new Vector2(radius * 0.18f, -radius * 0.26f),
                    new Vector2(radius * 0.88f, -radius * 0.64f),
                    2f,
                    detail);
                canvas.DrawLine(
                    new Vector2(radius * 0.18f, radius * 0.24f),
                    new Vector2(radius * 0.75f, radius * 0.48f),
                    2f,
                    detail);
                DrawRosterCore(canvas, new Vector2(-radius * 0.36f, -radius * 0.03f), radius * 0.24f, accent, hit);
            }
            else if (id == "gunner")
            {
                var detail = hit ? Color.white : ParseColor("#fde68a");
                foreach (var y in new[] { -0.42f, 0f, 0.35f })
                {
                    canvas.FillRect(
                        new Vector2(radius * 0.32f, radius * y),
                        radius * 0.86f,
                        2.4f,
                        detail);
                }
                DrawRosterCore(canvas, new Vector2(-radius * 0.34f, radius * 0.04f), radius * 0.22f, accent, hit);
                canvas.FillRect(new Vector2(-radius * 0.05f, radius * 0.49f), 2.4f, 2.4f, detail);
            }
            else if (id == "guard")
            {
                var detail = hit ? Color.white : ParseColor("#bae6fd");
                canvas.DrawArc(new Vector2(radius * 0.18f, 0), radius * 0.64f, -1.28f, 1.16f, 3f, detail);
                canvas.DrawLine(
                    new Vector2(-radius * 0.54f, -radius * 0.45f),
                    new Vector2(-radius * 0.28f, radius * 0.34f),
                    3f,
                    detail);
                DrawRosterCore(canvas, new Vector2(-radius * 0.38f, -radius * 0.04f), radius * 0.2f, accent, hit);
            }
            else
            {
                var detail = hit ? Color.white : ParseColor("#fed7aa");
                for (var index = 0; index < 6; index++)
                {
                    var angle = index / 6f * Mathf.PI * 2f;
                    var outer = index == 4 ? 0.66f : 0.82f;
                    canvas.DrawLine(
                        new Vector2(Mathf.Cos(angle) * radius * 0.42f, Mathf.Sin(angle) * radius * 0.42f),
                        new Vector2(Mathf.Cos(angle) * radius * outer, Mathf.Sin(angle) * radius * outer),
                        2.2f,
                        detail);
                }
                DrawRosterCore(canvas, new Vector2(-radius * 0.04f, -radius * 0.03f), radius * 0.27f, accent, hit);
            }

            return canvas.ToAtlasSprite("VoidFall_RosterII_" + id + (hit ? "_Hit" : ""));
        }

        private static void DrawRosterCore(RasterCanvas canvas, Vector2 centre, float radius, Color accent, bool hit)
        {
            canvas.FillCircle(centre, radius, hit ? Color.white : accent);
            canvas.FillCircle(
                centre + new Vector2(radius * 0.18f, -radius * 0.14f),
                Mathf.Max(1f, radius * 0.34f),
                hit ? ParseColor("#dbeafe") : ParseColor("#f8fafc"));
        }

        private static float EnemyRadius(string id)
        {
            id = CourtBaseEnemyId(id);
            switch (id)
            {
                case "runner": return 10;
                case "dasher": return 12;
                case "brute": return 24;
                case "gunner": return 14;
                case "twinGunner": return 18;
                case "guard": return 18;
                case "exploder": return 15;
                case "technician": return 16;
                case "mortar": return 18;
                case "splitter": return 18;
                case "bulwark": return 26;
                case "harvester": return 18;
                case "carrier": return 30;
                case "elite": return 38;
                case "court-pawn": return 15;
                case "court-rook": return 30;
                case "court-bishop": return 18;
                case "court-knight": return 17;
                case "court-queen": return 26;
                default: return 15;
            }
        }

        private static string CourtBaseEnemyId(string id)
        {
            if (string.IsNullOrEmpty(id)) return id;
            if (id.EndsWith("-black", StringComparison.Ordinal))
                return id.Substring(0, id.Length - 6);
            if (id.EndsWith("-white", StringComparison.Ordinal))
                return id.Substring(0, id.Length - 6);
            return id;
        }

        private static bool CourtWhiteSprite(string id) =>
            !string.IsNullOrEmpty(id) && id.EndsWith("-white", StringComparison.Ordinal);

        private static readonly Color EnemyColorChaser = ParseColor("#fb7185");
        private static readonly Color EnemyColorRunner = ParseColor("#a78bfa");
        private static readonly Color EnemyColorDasher = ParseColor("#e879f9");
        private static readonly Color EnemyColorBrute = ParseColor("#fb923c");
        private static readonly Color EnemyColorGunner = ParseColor("#f87171");
        private static readonly Color EnemyColorTwinGunner = ParseColor("#dc5a45");
        private static readonly Color EnemyColorGuard = ParseColor("#60a5fa");
        private static readonly Color EnemyColorExploder = ParseColor("#f59e0b");
        private static readonly Color EnemyColorTechnician = ParseColor("#2dd4bf");
        private static readonly Color EnemyColorMortar = ParseColor("#f97316");
        private static readonly Color EnemyColorSplitter = ParseColor("#f472b6");
        private static readonly Color EnemyColorBulwark = ParseColor("#38bdf8");
        private static readonly Color EnemyColorHarvester = ParseColor("#34d399");
        private static readonly Color EnemyColorCarrier = ParseColor("#eab308");
        private static readonly Color EnemyColorElite = ParseColor("#ef4444");
        private static readonly Color EnemyColorDefault = ParseColor("#e879f9");

        private static Color SourceEnemyColor(string id)
        {
            id = CourtBaseEnemyId(id);
            switch (id)
            {
                case "chaser": return EnemyColorChaser;
                case "runner": return EnemyColorRunner;
                case "dasher": return EnemyColorDasher;
                case "brute": return EnemyColorBrute;
                case "gunner": return EnemyColorGunner;
                case "twinGunner": return EnemyColorTwinGunner;
                case "guard": return EnemyColorGuard;
                case "exploder": return EnemyColorExploder;
                case "technician": return EnemyColorTechnician;
                case "mortar": return EnemyColorMortar;
                case "splitter": return EnemyColorSplitter;
                case "bulwark": return EnemyColorBulwark;
                case "harvester": return EnemyColorHarvester;
                case "carrier": return EnemyColorCarrier;
                case "elite": return EnemyColorElite;
                case "court-pawn":
                case "court-rook":
                case "court-bishop":
                case "court-knight":
                case "court-queen": return Color.white;
                default: return EnemyColorDefault;
            }
        }

        private static void FillEnemyShape(RasterCanvas canvas, string id, float r, Color color)
        {
            if (id == "exploder")
            {
                canvas.FillCircle(new Vector2(0, r * 0.08f), r * 0.92f, color);
                return;
            }
            canvas.FillPolygon(EnemyPoints(id, r), color);
        }

        private static void StrokeEnemyShape(RasterCanvas canvas, string id, float r, Color color, float width)
        {
            if (id == "exploder")
            {
                canvas.StrokeCircle(new Vector2(0, r * 0.08f), r * 0.92f, color, width);
                return;
            }
            canvas.StrokePolygon(EnemyPoints(id, r), color, width);
        }

        private static Vector2[] EnemyPoints(string id, float r)
        {
            switch (id)
            {
                case "chaser":
                {
                    var points = new List<Vector2>(14);
                    var outer = new[] { 1f, 0.92f, 1.04f, 0.96f, 0.82f, 1.02f, 0.94f };
                    for (var index = 0; index < 14; index++)
                    {
                        var pointRadius = index % 2 == 0
                            ? r * outer[index / 2]
                            : r * 0.6f * (index == 7 ? 0.82f : 1f);
                        var angle = index / 14f * Mathf.PI * 2f - Mathf.PI / 2f;
                        points.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * pointRadius);
                    }
                    return points.ToArray();
                }
                case "runner":
                    return new[]
                    {
                        new Vector2(r * 1.16f, 0), new Vector2(r * 0.16f, -r * 0.72f),
                        new Vector2(-r * 0.68f, -r * 0.5f), new Vector2(-r * 0.38f, -r * 0.08f),
                        new Vector2(-r, r * 0.25f), new Vector2(-r * 0.3f, r * 0.58f),
                        new Vector2(r * 0.2f, r * 0.64f),
                    };
                case "dasher":
                    return new[]
                    {
                        new Vector2(r * 1.18f, 0), new Vector2(-r * 0.62f, r * 0.82f),
                        new Vector2(-r * 0.28f, r * 0.08f), new Vector2(-r * 0.78f, -r * 0.68f),
                        new Vector2(r * 0.08f, -r * 0.48f),
                    };
                case "brute":
                    return new[]
                    {
                        new Vector2(-r * 0.18f, -r), new Vector2(r * 0.62f, -r * 0.82f),
                        new Vector2(r, -r * 0.2f), new Vector2(r * 0.8f, r * 0.62f),
                        new Vector2(r * 0.12f, r), new Vector2(-r * 0.7f, r * 0.74f),
                        new Vector2(-r, -r * 0.1f), new Vector2(-r * 0.55f, -r * 0.78f),
                    };
                case "gunner":
                    return new[]
                    {
                        new Vector2(-r * 0.82f, -r * 0.72f), new Vector2(r * 0.34f, -r * 0.62f),
                        new Vector2(r * 0.34f, -r * 0.2f), new Vector2(r * 1.3f, -r * 0.15f),
                        new Vector2(r * 1.3f, r * 0.18f), new Vector2(r * 0.3f, r * 0.24f),
                        new Vector2(r * 0.3f, r * 0.64f), new Vector2(-r * 0.76f, r * 0.72f),
                        new Vector2(-r, r * 0.12f),
                    };
                case "twinGunner":
                    return new[]
                    {
                        new Vector2(-r * 0.92f, -r * 0.62f), new Vector2(-r * 0.2f, -r * 0.88f),
                        new Vector2(r * 0.48f, -r * 0.58f), new Vector2(r * 0.48f, r * 0.62f),
                        new Vector2(-r * 0.3f, r * 0.86f), new Vector2(-r, r * 0.38f),
                    };
                case "guard":
                    return new[]
                    {
                        new Vector2(-r * 0.22f, -r), new Vector2(r * 0.62f, -r * 0.76f),
                        new Vector2(r, -r * 0.22f), new Vector2(r * 0.92f, r * 0.56f),
                        new Vector2(r * 0.38f, r), new Vector2(-r * 0.65f, r * 0.76f),
                        new Vector2(-r, r * 0.18f), new Vector2(-r * 0.88f, -r * 0.66f),
                    };
                case "technician":
                    return new[]
                    {
                        new Vector2(-r * 0.82f, -r * 0.48f), new Vector2(-r * 0.2f, -r * 0.9f),
                        new Vector2(r * 0.58f, -r * 0.62f), new Vector2(r * 0.88f, r * 0.08f),
                        new Vector2(r * 0.42f, r * 0.74f), new Vector2(-r * 0.38f, r * 0.9f),
                        new Vector2(-r, r * 0.26f),
                    };
                case "mortar":
                    return new[]
                    {
                        new Vector2(-r, -r * 0.64f), new Vector2(r * 0.26f, -r * 0.8f),
                        new Vector2(r * 0.78f, -r * 0.35f), new Vector2(r, r * 0.48f),
                        new Vector2(r * 0.18f, r * 0.82f), new Vector2(-r * 0.78f, r * 0.66f),
                    };
                case "splitter":
                    return new[]
                    {
                        new Vector2(0, -r), new Vector2(r * 0.84f, -r * 0.45f),
                        new Vector2(r, r * 0.22f), new Vector2(r * 0.32f, r),
                        new Vector2(-r * 0.18f, r * 0.72f), new Vector2(-r, r * 0.3f),
                        new Vector2(-r * 0.72f, -r * 0.58f),
                    };
                case "bulwark":
                    return new[]
                    {
                        new Vector2(-r * 0.96f, -r * 0.55f), new Vector2(-r * 0.28f, -r * 0.86f),
                        new Vector2(r * 0.54f, -r * 0.7f), new Vector2(r * 0.92f, -r * 0.22f),
                        new Vector2(r * 0.82f, r * 0.58f), new Vector2(r * 0.08f, r * 0.9f),
                        new Vector2(-r, r * 0.5f),
                    };
                case "harvester":
                    return new[]
                    {
                        new Vector2(-r * 0.94f, -r * 0.5f), new Vector2(-r * 0.28f, -r * 0.9f),
                        new Vector2(r * 0.42f, -r * 0.68f), new Vector2(r * 1.08f, -r * 0.45f),
                        new Vector2(r * 0.62f, 0), new Vector2(r * 1.05f, r * 0.5f),
                        new Vector2(r * 0.28f, r * 0.72f), new Vector2(-r * 0.16f, r * 0.9f),
                        new Vector2(-r * 0.86f, r * 0.5f),
                    };
                case "carrier":
                    return new[]
                    {
                        new Vector2(-r, -r * 0.42f), new Vector2(-r * 0.58f, -r * 0.82f),
                        new Vector2(r * 0.52f, -r * 0.74f), new Vector2(r, -r * 0.18f),
                        new Vector2(r * 0.8f, r * 0.62f), new Vector2(r * 0.16f, r * 0.84f),
                        new Vector2(-r * 0.74f, r * 0.64f),
                    };
                case "elite":
                    return new[]
                    {
                        new Vector2(-r * 0.18f, -r), new Vector2(r * 0.66f, -r * 0.72f),
                        new Vector2(r, r * 0.04f), new Vector2(r * 0.52f, r * 0.86f),
                        new Vector2(-r * 0.3f, r), new Vector2(-r, r * 0.28f),
                        new Vector2(-r * 0.76f, -r * 0.62f),
                    };
                case "court-pawn":
                    return new[]
                    {
                        new Vector2(0, -r), new Vector2(r * 0.36f, -r * 0.52f),
                        new Vector2(r * 0.92f, -r * 0.42f), new Vector2(r * 0.58f, r * 0.06f),
                        new Vector2(r * 0.78f, r * 0.7f), new Vector2(r * 0.2f, r * 0.58f),
                        new Vector2(0, r), new Vector2(-r * 0.25f, r * 0.58f),
                        new Vector2(-r * 0.82f, r * 0.75f), new Vector2(-r * 0.62f, r * 0.05f),
                        new Vector2(-r, -r * 0.4f), new Vector2(-r * 0.38f, -r * 0.52f),
                    };
                case "court-rook":
                    return new[]
                    {
                        new Vector2(-r * 0.9f, -r * 0.45f), new Vector2(-r * 0.72f, -r * 0.95f),
                        new Vector2(-r * 0.4f, -r * 0.88f), new Vector2(-r * 0.34f, -r * 0.58f),
                        new Vector2(-r * 0.08f, -r * 0.78f), new Vector2(0, -r),
                        new Vector2(r * 0.28f, -r * 0.75f), new Vector2(r * 0.58f, -r * 0.88f),
                        new Vector2(r * 0.82f, -r * 0.55f), new Vector2(r, -r * 0.1f),
                        new Vector2(r * 0.78f, r * 0.28f), new Vector2(r * 0.95f, r * 0.65f),
                        new Vector2(r * 0.38f, r * 0.78f), new Vector2(0, r),
                        new Vector2(-r * 0.42f, r * 0.76f), new Vector2(-r * 0.92f, r * 0.7f),
                        new Vector2(-r * 0.76f, r * 0.2f), new Vector2(-r, -r * 0.18f),
                    };
                case "court-bishop":
                    return new[]
                    {
                        new Vector2(-r, -r * 0.7f), new Vector2(-r * 0.28f, -r),
                        new Vector2(r * 0.48f, -r * 0.72f), new Vector2(r * 0.7f, -r * 0.28f),
                        new Vector2(r * 1.55f, -r * 0.18f), new Vector2(r * 1.55f, r * 0.2f),
                        new Vector2(r * 0.62f, r * 0.3f), new Vector2(r * 0.34f, r * 0.82f),
                        new Vector2(-r * 0.42f, r), new Vector2(-r, r * 0.58f),
                    };
                case "court-knight":
                    return new[]
                    {
                        new Vector2(-r * 0.8f, -r * 0.42f), new Vector2(-r * 0.18f, -r),
                        new Vector2(r * 0.46f, -r * 0.78f), new Vector2(r, -r * 0.28f),
                        new Vector2(r * 0.42f, -r * 0.02f), new Vector2(r * 1.05f, r * 0.52f),
                        new Vector2(r * 0.28f, r * 0.58f), new Vector2(0, r),
                        new Vector2(-r * 0.58f, r * 0.7f), new Vector2(-r, r * 0.2f),
                        new Vector2(-r * 0.56f, -r * 0.05f),
                    };
                case "court-queen":
                    return Ngon(16, r, Mathf.PI / 16f);
                default:
                    return Ngon(6, r * 0.9f, Mathf.PI / 6f);
            }
        }

        private static void DrawEnemyDetails(RasterCanvas canvas, string id, float r, Color accent, bool hit)
        {
            var bright = hit ? Color.white : accent;
            switch (id)
            {
                case "chaser":
                    DrawCore(canvas, r * 0.06f, -r * 0.04f, r * 0.3f, accent, hit);
                    break;
                case "runner":
                    canvas.DrawLine(new Vector2(-r * 0.48f, r * 0.12f), new Vector2(r * 0.48f, -r * 0.03f), 1.5f, bright);
                    DrawCore(canvas, r * 0.18f, 0, r * 0.22f, accent, hit);
                    break;
                case "dasher":
                    canvas.FillPolygon(new[]
                    {
                        new Vector2(-r * 0.35f, -r * 0.13f), new Vector2(r * 0.62f, -r * 0.08f),
                        new Vector2(r * 0.25f, r * 0.15f), new Vector2(-r * 0.48f, r * 0.19f),
                    }, bright);
                    break;
                case "brute":
                    canvas.DrawLine(new Vector2(-r * 0.58f, -r * 0.25f), new Vector2(-r * 0.18f, -r * 0.53f), 2.2f, bright);
                    canvas.DrawLine(new Vector2(r * 0.22f, r * 0.5f), new Vector2(r * 0.62f, r * 0.24f), 2.2f, bright);
                    DrawCore(canvas, -r * 0.08f, r * 0.03f, r * 0.25f, accent, hit);
                    break;
                case "gunner":
                    canvas.FillRect(new Vector2(r * 0.7f, r * 0.0f), r, r * 0.18f, bright);
                    canvas.FillRect(new Vector2(r * 0.13f, -r * 0.66f), r * 0.22f, r * 0.24f, bright);
                    DrawCore(canvas, -r * 0.25f, r * 0.02f, r * 0.22f, accent, hit);
                    break;
                case "twinGunner":
                    canvas.FillRect(new Vector2(r * 0.77f, -r * 0.355f), r * 1.18f, r * 0.25f, bright);
                    canvas.FillRect(new Vector2(r * 0.67f, r * 0.355f), r * 0.98f, r * 0.25f, bright);
                    canvas.FillRect(new Vector2(r * 0.67f, -r * 0.355f), r * 0.18f, r * 0.11f, hit ? new Color(1, 0.97f, 0.92f) : new Color(0.26f, 0.08f, 0.02f, 1));
                    canvas.FillRect(new Vector2(r * 0.59f, r * 0.355f), r * 0.18f, r * 0.11f, hit ? new Color(1, 0.97f, 0.92f) : new Color(0.26f, 0.08f, 0.02f, 1));
                    DrawCore(canvas, -r * 0.28f, -r * 0.04f, r * 0.24f, ParseColor("#fb923c"), hit);
                    canvas.FillCircle(new Vector2(-r * 0.62f, r * 0.34f), r * 0.08f, bright);
                    break;
                case "guard":
                    canvas.DrawArc(new Vector2(r * 0.06f, 0), r * 0.62f, -Mathf.PI * 0.52f, Mathf.PI * 0.5f, 3f, bright);
                    canvas.FillRect(new Vector2(r * 0.68f, -r * 0.06f), r * 0.26f, r * 0.2f, hit ? new Color(0.86f, 0.91f, 0.98f, 1) : new Color(0.02f, 0.024f, 0.06f, 1));
                    DrawCore(canvas, -r * 0.28f, 0, r * 0.2f, accent, hit);
                    break;
                case "exploder":
                    canvas.FillRect(new Vector2(0, -r * 1.02f + r * 0.13f), r * 0.52f, r * 0.26f, hit ? Color.white : new Color(0.16f, 0.145f, 0.13f, 1));
                    canvas.StrokeRect(new Vector2(0, -r * 0.89f), r * 0.52f, r * 0.26f, bright, 1.6f);
                    canvas.DrawLine(new Vector2(0, -r * 1.02f), new Vector2(r * 0.42f, -r * 1.42f), 2f, hit ? Color.white : new Color(0.63f, 0.38f, 0.03f, 1));
                    canvas.DrawLine(new Vector2(r * 0.42f, -r * 1.42f), new Vector2(r * 0.6f, -r * 1.2f), 2f, hit ? Color.white : new Color(0.63f, 0.38f, 0.03f, 1));
                    DrawCore(canvas, r * 0.6f, -r * 1.2f, r * 0.16f, ParseColor("#fde047"), hit);
                    DrawCore(canvas, 0, r * 0.08f, r * 0.42f, ParseColor("#ef4444"), hit);
                    canvas.DrawLine(new Vector2(-r * 0.52f, -r * 0.3f), new Vector2(-r * 0.16f, -r * 0.02f), 1.7f, bright);
                    canvas.DrawLine(new Vector2(-r * 0.16f, -r * 0.02f), new Vector2(-r * 0.42f, r * 0.34f), 1.7f, bright);
                    canvas.DrawLine(new Vector2(r * 0.5f, -r * 0.34f), new Vector2(r * 0.18f, r * 0.02f), 1.7f, bright);
                    canvas.DrawLine(new Vector2(r * 0.18f, r * 0.02f), new Vector2(r * 0.46f, r * 0.42f), 1.7f, bright);
                    break;
                case "technician":
                    canvas.FillRect(new Vector2(r * 0.675f, -r * 0.13f), r * 0.95f, r * 0.18f, bright);
                    canvas.FillRect(new Vector2(r * 0.91f, -r * 0.12f), r * 0.18f, r * 0.68f, bright);
                    canvas.FillRect(new Vector2(-r * 0.42f, r * 0.32f), r * 0.52f, r * 0.16f, hit ? new Color(0.86f, 0.91f, 0.98f, 1) : new Color(0.06f, 0.46f, 0.43f, 1));
                    DrawCore(canvas, -r * 0.18f, -r * 0.04f, r * 0.22f, accent, hit);
                    break;
                case "mortar":
                    canvas.FillRect(new Vector2(r * 0.51f, -r * 0.06f), r * 1.42f, r * 0.52f, hit ? Color.white : new Color(0.49f, 0.18f, 0.07f, 1));
                    canvas.FillRect(new Vector2(r * 1.01f, -r * 0.07f), r * 0.7f, r * 0.36f, bright);
                    canvas.FillRect(new Vector2(r * 1.26f, -r * 0.07f), r * 0.18f, r * 0.26f, hit ? Color.white : new Color(0.99f, 0.84f, 0.67f, 1));
                    DrawCore(canvas, -r * 0.42f, r * 0.08f, r * 0.2f, accent, hit);
                    break;
                case "splitter":
                    canvas.DrawLine(new Vector2(-r * 0.16f, -r * 0.72f), new Vector2(r * 0.12f, -r * 0.18f), 2.4f, bright);
                    canvas.DrawLine(new Vector2(r * 0.12f, -r * 0.18f), new Vector2(-r * 0.22f, r * 0.18f), 2.4f, bright);
                    canvas.DrawLine(new Vector2(-r * 0.22f, r * 0.18f), new Vector2(r * 0.18f, r * 0.74f), 2.4f, bright);
                    canvas.FillRect(new Vector2(r * 0.52f, -r * 0.08f), r * 0.28f, r * 0.16f, hit ? new Color(0.99f, 0.91f, 0.95f, 1) : new Color(0.62f, 0.09f, 0.3f, 1));
                    DrawCore(canvas, -r * 0.45f, r * 0.05f, r * 0.17f, accent, hit);
                    break;
                case "bulwark":
                    var shield = new[]
                    {
                        new Vector2(r * 0.42f, -r * 0.78f), new Vector2(r * 1.15f, -r * 0.54f),
                        new Vector2(r * 1.18f, r * 0.48f), new Vector2(r * 0.38f, r * 0.75f),
                    };
                    canvas.FillPolygon(shield, hit ? Color.white : new Color(0.02f, 0.35f, 0.52f, 1));
                    canvas.StrokePolygon(shield, bright, 3f);
                    canvas.FillRect(new Vector2(r * 0.88f, -r * 0.025f), r * 0.32f, r * 0.19f, hit ? new Color(0.88f, 0.95f, 1f, 1) : new Color(0.49f, 0.83f, 0.99f, 1));
                    DrawCore(canvas, -r * 0.34f, 0, r * 0.2f, accent, hit);
                    break;
                case "harvester":
                    var intake = new[]
                    {
                        new Vector2(r * 0.08f, -r * 0.36f), new Vector2(r * 0.88f, -r * 0.4f),
                        new Vector2(r * 0.54f, 0), new Vector2(r * 0.88f, r * 0.42f),
                        new Vector2(r * 0.06f, r * 0.34f), new Vector2(r * 0.34f, 0),
                    };
                    canvas.FillPolygon(intake, hit ? Color.white : new Color(0.01f, 0.17f, 0.13f, 1));
                    canvas.FillPolygon(new[] { new Vector2(r * 0.34f, -r * 0.32f), new Vector2(r * 0.52f, -r * 0.28f), new Vector2(r * 0.42f, -r * 0.08f) }, hit ? Color.white : new Color(0.82f, 0.98f, 0.9f, 1));
                    canvas.FillPolygon(new[] { new Vector2(r * 0.5f, r * 0.3f), new Vector2(r * 0.68f, r * 0.32f), new Vector2(r * 0.55f, r * 0.08f) }, hit ? Color.white : new Color(0.82f, 0.98f, 0.9f, 1));
                    canvas.FillPolygon(new[] { new Vector2(r * 0.14f, r * 0.3f), new Vector2(r * 0.28f, r * 0.3f), new Vector2(r * 0.2f, r * 0.12f) }, hit ? Color.white : new Color(0.82f, 0.98f, 0.9f, 1));
                    canvas.FillRect(new Vector2(-r * 0.57f, -r * 0.11f), r * 0.34f, r * 0.18f, hit ? new Color(0.93f, 1f, 0.97f, 1) : new Color(0.02f, 0.59f, 0.4f, 1));
                    DrawCore(canvas, -r * 0.3f, r * 0.04f, r * 0.2f, accent, hit);
                    break;
                case "carrier":
                    canvas.FillRect(new Vector2(-r * 0.31f, -r * 0.325f), r * 0.62f, r * 0.31f, hit ? Color.white : new Color(0.44f, 0.25f, 0.07f, 1));
                    canvas.FillRect(new Vector2(-r * 0.13f, r * 0.35f), r * 0.74f, r * 0.34f, hit ? Color.white : new Color(0.44f, 0.25f, 0.07f, 1));
                    canvas.FillRect(new Vector2(-r * 0.34f, -r * 0.33f), r * 0.28f, r * 0.14f, bright);
                    canvas.FillRect(new Vector2(-r * 0.19f, r * 0.345f), r * 0.3f, r * 0.15f, bright);
                    DrawCore(canvas, r * 0.36f, -r * 0.04f, r * 0.24f, accent, hit);
                    break;
                case "elite":
                    canvas.DrawLine(new Vector2(-r * 0.55f, -r * 0.18f), new Vector2(-r * 0.15f, -r * 0.55f), 3f, bright);
                    canvas.DrawLine(new Vector2(r * 0.24f, r * 0.52f), new Vector2(r * 0.62f, r * 0.22f), 3f, bright);
                    DrawCore(canvas, -r * 0.06f, r * 0.02f, r * 0.25f, accent, hit);
                    break;
                case "court-pawn":
                    DrawCore(canvas, 0, 0, r * 0.31f, accent, hit);
                    break;
                case "court-rook":
                    canvas.StrokePolygon(new[]
                    {
                        new Vector2(-r * 0.58f, -r * 0.42f), new Vector2(-r * 0.2f, -r * 0.58f),
                        new Vector2(r * 0.22f, -r * 0.46f), new Vector2(r * 0.58f, -r * 0.2f),
                        new Vector2(r * 0.48f, r * 0.42f), new Vector2(0, r * 0.62f),
                        new Vector2(-r * 0.52f, r * 0.38f),
                    }, accent, 2.5f);
                    DrawCore(canvas, -r * 0.05f, 0, r * 0.24f, accent, hit);
                    canvas.DrawLine(new Vector2(-r * 0.65f, r * 0.55f), new Vector2(-r * 0.35f, r * 0.32f), 2.2f, accent);
                    canvas.DrawLine(new Vector2(r * 0.32f, r * 0.48f), new Vector2(r * 0.62f, r * 0.62f), 2.2f, accent);
                    break;
                case "court-bishop":
                    canvas.FillRect(new Vector2(r * 1.22f, 0), r * 1.05f, r * 0.22f, accent);
                    DrawCore(canvas, -r * 0.25f, 0, r * 0.23f, accent, hit);
                    break;
                case "court-knight":
                    canvas.FillPolygon(new[]
                    {
                        new Vector2(r * 0.12f, -r * 0.5f), new Vector2(r * 0.88f, -r * 0.24f),
                        new Vector2(r * 0.42f, r * 0.06f),
                    }, accent);
                    DrawCore(canvas, -r * 0.18f, -r * 0.08f, r * 0.22f, accent, hit);
                    break;
                case "court-queen":
                    canvas.StrokeCircle(Vector2.zero, r * 0.72f, accent, 2.2f);
                    canvas.FillPolygon(new[]
                    {
                        new Vector2(-r * 0.42f, -r * 0.72f), new Vector2(-r * 0.2f, -r * 1.08f),
                        new Vector2(0, -r * 0.8f), new Vector2(r * 0.22f, -r * 1.08f),
                        new Vector2(r * 0.45f, -r * 0.7f),
                    }, accent);
                    DrawCore(canvas, 0, 0, r * 0.28f, accent, hit);
                    break;
            }
        }

        private static Sprite BuildBoss(string id, Color accent, bool hit)
        {
            var court = id == "court-grandmaster-black" || id == "court-grandmaster-white";
            var radius = id == "hydra-prime" ? 88f : court ? 66f : id == "warden" ? 56f : id == "matriarch" ? 62f : id == "reaver" ? 54f : 48f;
            // The browser boss sprites are authored on fixed canvases, not on
            // one shared padded texture. Keeping the same logical half-size
            // preserves both the silhouette scale and the authored glow falloff.
            var canvasSize = Mathf.RoundToInt(BossCanvasSize(id));
            var canvas = new RasterCanvas(canvasSize * 0.5f, 0f, canvasSize);
            if (!hit) canvas.Glow(BossGlowRadius(id), accent, BossGlowAlpha(id));
            var body = BossBodyColor(id, hit);
            switch (id)
            {
                case "hydra-prime":
                    DrawHydraPrime(canvas, radius, accent, hit, body);
                    break;
                case "warden":
                    DrawWarden(canvas, radius, accent, hit, body);
                    break;
                case "herald":
                    DrawHerald(canvas, radius, accent, hit, body);
                    break;
                case "matriarch":
                    DrawMatriarch(canvas, radius, accent, hit, body);
                    break;
                case "reaver":
                    DrawReaver(canvas, radius, accent, hit, body);
                    break;
                case "court-grandmaster-black":
                case "court-grandmaster-white":
                    DrawCourtGrandmaster(canvas, radius, id == "court-grandmaster-white", hit);
                    break;
                default:
                    canvas.FillCircle(Vector2.zero, radius * 0.9f, body);
                    canvas.StrokeCircle(Vector2.zero, radius * 0.9f, hit ? Color.white : accent, 4f);
                    DrawCore(canvas, 0, 0, radius * 0.2f, accent, hit);
                    break;
            }
            return canvas.ToAtlasSprite("VoidFall_Boss_" + id + (hit ? "_Hit" : ""));
        }

        /// <summary>
        /// Body fills are part of each authored browser boss sprite. They are
        /// intentionally independent from the encounter definition accent.
        /// </summary>
        public static Color BossBodyColor(string id, bool hit)
        {
            if (hit) return ParseColor("#f8fafc");
            switch (id)
            {
                case "hydra-prime": return ParseColor("#020805");
                case "warden": return ParseColor("#080b13");
                case "herald": return ParseColor("#080a12");
                case "matriarch": return ParseColor("#07110f");
                case "reaver": return ParseColor("#070b14");
                case "court-grandmaster-black": return ParseColor("#050607");
                case "court-grandmaster-white": return ParseColor("#f1f0ea");
                default: return ParseColor("#080a12");
            }
        }

        public static float BossGlowRadius(string id)
        {
            switch (id)
            {
                case "hydra-prime": return 104f;
                case "warden": return 82f;
                case "herald": return 68f;
                case "matriarch": return 78f;
                case "reaver": return 74f;
                case "court-grandmaster-black":
                case "court-grandmaster-white": return 92f;
                default: return 74f;
            }
        }

        public static float BossGlowAlpha(string id)
        {
            switch (id)
            {
                case "hydra-prime": return 0.3f;
                case "warden": return 0.24f;
                case "herald": return 0.2f;
                case "matriarch": return 0.18f;
                case "reaver": return 0.19f;
                case "court-grandmaster-black":
                case "court-grandmaster-white": return 0.24f;
                default: return 0.2f;
            }
        }

        private static readonly Color BossColorWarden = ParseColor("#ef4444");
        private static readonly Color BossColorHerald = ParseColor("#a78bfa");
        private static readonly Color BossColorMatriarch = ParseColor("#34d399");
        private static readonly Color BossColorReaver = ParseColor("#60a5fa");
        private static readonly Color BossColorHydra = ParseColor("#78ff5a");
        private static readonly Color BossColorCourtBlack = ParseColor("#f3f4f6");
        private static readonly Color BossColorCourtWhite = ParseColor("#111827");
        private static readonly Color BossColorDefault = ParseColor("#e879f9");

        private static Color SourceBossColor(string id)
        {
            switch (id)
            {
                case "hydra-prime": return BossColorHydra;
                case "court-grandmaster-black": return BossColorCourtBlack;
                case "court-grandmaster-white": return BossColorCourtWhite;
                case "warden": return BossColorWarden;
                case "herald": return BossColorHerald;
                case "matriarch": return BossColorMatriarch;
                case "reaver": return BossColorReaver;
                default: return BossColorDefault;
            }
        }

        private static void DrawCourtGrandmaster(
            RasterCanvas canvas,
            float radius,
            bool white,
            bool hit)
        {
            var body = hit ? Color.white : white ? ParseColor("#f1f0ea") : ParseColor("#050607");
            var outline = hit ? Color.white : white ? ParseColor("#080c18") : ParseColor("#f1f0ea");
            var opposite = white ? ParseColor("#050607") : ParseColor("#f1f0ea");
            var crown = new[]
            {
                new Vector2(0, -radius * 1.28f),
                new Vector2(-radius * 0.88f, -radius * 0.25f),
                new Vector2(-radius * 0.38f, -radius * 0.42f),
                new Vector2(-radius * 0.86f, radius * 0.54f),
                new Vector2(-radius * 0.24f, radius * 0.2f),
                new Vector2(-radius * 0.55f, radius * 1.05f),
                new Vector2(radius * 0.55f, radius * 1.05f),
                new Vector2(radius * 0.24f, radius * 0.2f),
                new Vector2(radius * 0.86f, radius * 0.54f),
                new Vector2(radius * 0.38f, -radius * 0.42f),
                new Vector2(radius * 0.88f, -radius * 0.25f),
            };
            canvas.FillPolygon(crown, body);
            canvas.StrokePolygon(crown, outline, 4.5f);
            canvas.DrawArc(Vector2.zero, radius * 1.18f, 0, Mathf.PI * 2f, 7f, outline);
            canvas.DrawArc(Vector2.zero, radius * 0.93f, 0, Mathf.PI * 2f, 4f, opposite);
            canvas.FillCircle(Vector2.zero, radius * 0.42f, opposite);
            canvas.FillCircle(Vector2.zero, radius * 0.23f, body);
            canvas.FillPolygon(new[]
            {
                new Vector2(-radius * 0.55f, radius * 0.72f),
                new Vector2(radius * 0.55f, radius * 0.72f),
                new Vector2(radius * 0.48f, radius * 1.28f),
                new Vector2(-radius * 0.48f, radius * 1.28f),
            }, body);
            canvas.StrokePolygon(new[]
            {
                new Vector2(-radius * 0.55f, radius * 0.72f),
                new Vector2(radius * 0.55f, radius * 0.72f),
                new Vector2(radius * 0.48f, radius * 1.28f),
                new Vector2(-radius * 0.48f, radius * 1.28f),
            }, outline, 4f);
        }

        private static void DrawHydraPrime(
            RasterCanvas canvas,
            float radius,
            Color accent,
            bool hit,
            Color body)
        {
            var outline = hit ? Color.white : ParseColor("#78ff75");
            var tissue = hit ? ParseColor("#f8fafc") : ParseColor("#16743a");
            var tissueLight = hit ? Color.white : ParseColor("#65e85f");
            var lobeCentres = new[]
            {
                new Vector2(0, -40), new Vector2(-38, -31), new Vector2(38, -31),
                new Vector2(-57, 0), new Vector2(57, 0),
                new Vector2(-41, 37), new Vector2(41, 37), new Vector2(0, 48),
            };
            foreach (var centre in lobeCentres)
            {
                canvas.FillCircle(centre, 39f, body);
                canvas.StrokeCircle(centre, 39f, outline, 5f);
            }
            canvas.FillCircle(Vector2.zero, radius * 0.72f, tissue);
            foreach (var centre in lobeCentres)
                canvas.FillCircle(centre * 0.86f, 29f, tissue);

            for (var line = -2; line <= 2; line++)
            {
                var offset = line * 16f;
                canvas.DrawLine(
                    new Vector2(-62f, offset - 10f),
                    new Vector2(62f, -offset * 0.45f + 8f),
                    2.2f,
                    hit ? Color.white : new Color(0.76f, 1f, 0.52f, 0.52f));
            }

            var eyeOuter = HydraEllipsePoints(Vector2.zero + Vector2.down * 4f, 24f, 38f, 30);
            var eyeInner = HydraEllipsePoints(Vector2.zero + Vector2.down * 4f, 11f, 29f, 24);
            canvas.FillPolygon(eyeOuter, ParseColor("#031007"));
            canvas.StrokePolygon(eyeOuter, hit ? Color.white : ParseColor("#c8ff55"), 5f);
            canvas.FillPolygon(eyeInner, hit ? Color.white : ParseColor("#d6ff4b"));
            canvas.FillPolygon(
                HydraEllipsePoints(Vector2.down * 4f, 4f, 22f, 20),
                ParseColor("#041008"));
            canvas.FillCircle(new Vector2(-4f, -19f), 3.5f, Color.white);

            var mouth = new[]
            {
                new Vector2(-45f, -42f), new Vector2(45f, -42f),
                new Vector2(34f, -76f), new Vector2(0f, -84f), new Vector2(-34f, -76f),
            };
            canvas.FillPolygon(mouth, ParseColor("#010403"));
            canvas.StrokePolygon(mouth, hit ? Color.white : ParseColor("#49ef68"), 4f);
            for (var tooth = 0; tooth < 7; tooth++)
            {
                var x = -34f + tooth * 11.3f;
                var top = tooth % 2 == 0;
                canvas.FillPolygon(new[]
                {
                    new Vector2(x - 5f, top ? -44f : -73f),
                    new Vector2(x + 5f, top ? -44f : -73f),
                    new Vector2(x, top ? -68f : -50f),
                }, hit ? Color.white : ParseColor("#efeccf"));
            }

            for (var pore = 0; pore < 13; pore++)
            {
                var angle = pore * 2.399963f;
                var distance = 34f + (pore % 3) * 12f;
                canvas.FillCircle(
                    new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance,
                    2.2f + pore % 2,
                    hit ? Color.white : new Color(tissueLight.r, tissueLight.g, tissueLight.b, 0.58f));
            }
        }

        private static Vector2[] HydraEllipsePoints(
            Vector2 centre,
            float radiusX,
            float radiusY,
            int count)
        {
            var points = new Vector2[Mathf.Max(8, count)];
            for (var index = 0; index < points.Length; index++)
            {
                var angle = index / (float)points.Length * Mathf.PI * 2f;
                points[index] = centre + new Vector2(
                    Mathf.Cos(angle) * radiusX,
                    Mathf.Sin(angle) * radiusY);
            }
            return points;
        }

        private static void DrawWarden(RasterCanvas c, float r, Color accent, bool hit, Color body)
        {
            c.FillPolygon(new[]
            {
                new Vector2(-18, -r), new Vector2(24, -52), new Vector2(51, -30), new Vector2(r, 13),
                new Vector2(39, 47), new Vector2(4, r), new Vector2(-42, 44), new Vector2(-r, 10), new Vector2(-49, -31),
            }, body);
            c.StrokePolygon(new[]
            {
                new Vector2(-18, -r), new Vector2(24, -52), new Vector2(51, -30), new Vector2(r, 13),
                new Vector2(39, 47), new Vector2(4, r), new Vector2(-42, 44), new Vector2(-r, 10), new Vector2(-49, -31),
            }, hit ? Color.white : accent, 5f);
            var left = new[] { new Vector2(-49, -25), new Vector2(-24, -39), new Vector2(-20, -13), new Vector2(-46, 0) };
            var right = new[] { new Vector2(24, -37), new Vector2(50, -24), new Vector2(44, 5), new Vector2(18, -10) };
            c.FillPolygon(left, hit ? ParseColor("#e2e8f0") : ParseColor("#1f2937"));
            c.StrokePolygon(left, hit ? Color.white : ParseColor("#f87171"), 3f);
            c.FillPolygon(right, hit ? ParseColor("#e2e8f0") : ParseColor("#111827"));
            c.StrokePolygon(right, hit ? Color.white : ParseColor("#f87171"), 3f);
            c.FillPolygon(Ngon(6, 25, Mathf.PI / 10f), hit ? ParseColor("#dbeafe") : ParseColor("#111827"));
            c.StrokePolygon(Ngon(6, 25, Mathf.PI / 10f), hit ? Color.white : ParseColor("#fb7185"), 3f);
            DrawCore(c, -3, 2, 10, accent, hit);
            c.FillPolygon(new[] { new Vector2(-31, 28), new Vector2(-13, 20), new Vector2(-10, 40), new Vector2(-24, 45) }, hit ? ParseColor("#cbd5e1") : ParseColor("#05060f"));
        }

        private static void DrawHerald(RasterCanvas c, float r, Color accent, bool hit, Color body)
        {
            var points = new[]
            {
                new Vector2(0, -48), new Vector2(15, -20), new Vector2(43, -28), new Vector2(29, -3),
                new Vector2(47, 18), new Vector2(12, 16), new Vector2(-4, 48), new Vector2(-13, 14),
                new Vector2(-45, 23), new Vector2(-29, -5), new Vector2(-39, -31), new Vector2(-12, -20),
            };
            c.FillPolygon(points, body);
            c.StrokePolygon(points, hit ? Color.white : accent, 4f);
            c.FillPolygon(Ngon(4, 21, Mathf.PI / 4f), hit ? ParseColor("#dbeafe") : ParseColor("#161226"));
            c.StrokePolygon(Ngon(4, 21, Mathf.PI / 4f), hit ? Color.white : ParseColor("#c4b5fd"), 2.5f);
            DrawCore(c, 3, -2, 8, accent, hit);
            c.FillPolygon(new[] { new Vector2(-13, 14), new Vector2(-27, 32), new Vector2(-18, 36), new Vector2(-6, 18) }, hit ? ParseColor("#e2e8f0") : ParseColor("#30264d"));
        }

        private static void DrawMatriarch(RasterCanvas c, float r, Color accent, bool hit, Color body)
        {
            var points = new[]
            {
                new Vector2(-59, -24), new Vector2(-30, -46), new Vector2(22, -48), new Vector2(57, -22),
                new Vector2(52, 27), new Vector2(24, 48), new Vector2(-35, 43), new Vector2(-62, 17),
            };
            c.FillPolygon(points, body);
            c.StrokePolygon(points, hit ? Color.white : accent, 4.5f);
            foreach (var side in new[] { -1f, 1f })
            {
                c.DrawLine(new Vector2(side * 42, -8), new Vector2(side * 70, side < 0 ? -24 : -17), 5f, hit ? Color.white : ParseColor("#6ee7b7"));
                c.DrawLine(new Vector2(side * 70, side < 0 ? -24 : -17), new Vector2(side * 78, side < 0 ? -5 : 7), 5f, hit ? Color.white : ParseColor("#6ee7b7"));
                c.DrawLine(new Vector2(side * 38, 18), new Vector2(side * (side < 0 ? 63 : 70), 39), 5f, hit ? Color.white : ParseColor("#6ee7b7"));
            }
            c.FillPolygon(Ngon(6, 25, Mathf.PI / 6f), hit ? ParseColor("#dbeafe") : ParseColor("#0d211d"));
            c.StrokePolygon(Ngon(6, 25, Mathf.PI / 6f), hit ? Color.white : ParseColor("#6ee7b7"), 2.5f);
            DrawCore(c, -5, 2, 10, accent, hit);
        }

        private static void DrawReaver(RasterCanvas c, float r, Color accent, bool hit, Color body)
        {
            var points = new[]
            {
                new Vector2(-47, -38), new Vector2(-5, -51), new Vector2(18, -31), new Vector2(51, -20),
                new Vector2(28, 2), new Vector2(49, 35), new Vector2(10, 26), new Vector2(-15, 51),
                new Vector2(-25, 15), new Vector2(-53, 7), new Vector2(-28, -11),
            };
            c.FillPolygon(points, body);
            c.StrokePolygon(points, hit ? Color.white : accent, 4f);
            var inner = new[]
            {
                new Vector2(-24, -13), new Vector2(8, -28), new Vector2(29, -5), new Vector2(15, 24), new Vector2(-18, 19),
            };
            c.FillPolygon(inner, hit ? ParseColor("#dbeafe") : ParseColor("#111c30"));
            c.StrokePolygon(inner, hit ? Color.white : ParseColor("#93c5fd"), 2.5f);
            DrawCore(c, 5, -1, 9, accent, hit);
            c.FillRect(new Vector2(-27, 32.5f), 12, 9, hit ? ParseColor("#e2e8f0") : ParseColor("#1e3a5f"));
        }

        private static Sprite BuildPickup(string kind)
        {
            var color = kind == "part" ? ParseColor("#facc15")
                : kind == "magnet" ? ParseColor("#22d3ee")
                : kind == "repair" ? ParseColor("#4ade80")
                : kind == "overdrive" ? ParseColor("#facc15")
                : kind == "xp" ? ParseColor("#34d399")
                : ParseColor("#fb923c");
            // pickupSprite() uses cv(42), so preserve its authored source
            // canvas while retaining the existing 42-unit runtime size.
            var c = new RasterCanvas(18, 3, 42);
            c.Glow(19, color, 0.18f);
            if (kind == "part")
            {
                // The browser rotates a 14px square by 45 degrees; rotating
                // the source square preserves its exact corners and inner
                // square instead of approximating both with a diamond.
                c.SetRotation(Mathf.PI / 4f);
                c.FillRect(Vector2.zero, 14, 14, ParseColor("#070b12"));
                c.StrokeRect(Vector2.zero, 14, 14, color, 3f);
                c.FillRect(Vector2.zero, 4, 4, color);
            }
            else if (kind == "magnet")
            {
                // sprites.ts sets lineCap = "butt" for the horseshoe path.
                c.DrawLineButt(new Vector2(-8, -6), new Vector2(-8, 1), 5.5f, color);
                c.DrawArcButt(new Vector2(0, 1), 8, Mathf.PI, 0, 5.5f, color);
                c.DrawLineButt(new Vector2(8, 1), new Vector2(8, -6), 5.5f, color);
                c.FillRect(new Vector2(-7.75f, -9.5f), 6.5f, 7, ParseColor("#ecfeff"));
                c.FillRect(new Vector2(7.75f, -9.5f), 6.5f, 7, ParseColor("#ecfeff"));
                c.FillRect(new Vector2(-7.75f, -12f), 6.5f, 2, color);
                c.FillRect(new Vector2(7.75f, -12f), 6.5f, 2, color);
            }
            else if (kind == "repair")
            {
                var repair = new[]
                {
                    new Vector2(-11, -5), new Vector2(-5, -5), new Vector2(-5, -12), new Vector2(5, -12),
                    new Vector2(5, -5), new Vector2(12, -5), new Vector2(12, 5), new Vector2(5, 5),
                    new Vector2(5, 11), new Vector2(-5, 11), new Vector2(-5, 5), new Vector2(-11, 5),
                };
                c.FillPolygon(repair, ParseColor("#070b12"));
                c.StrokePolygon(repair, color, 3f);
            }
            else if (kind == "bomb")
            {
                c.FillCircle(new Vector2(-1, 2), 10, new Color(0.03f, 0.04f, 0.07f, 1));
                c.StrokeCircle(new Vector2(-1, 2), 10, color, 3f);
                c.DrawLine(new Vector2(4, -8), new Vector2(10, -14), 2.5f, color);
                c.DrawLine(new Vector2(10, -14), new Vector2(13, -10), 2.5f, color);
                c.FillRect(new Vector2(13, -11), 4, 4, color);
            }
            else if (kind == "overdrive")
            {
                var bolt = new[]
                {
                    new Vector2(2, -15), new Vector2(-9, 2), new Vector2(-2, 2), new Vector2(-7, 15),
                    new Vector2(11, -5), new Vector2(3, -5),
                };
                c.FillPolygon(bolt, color);
                c.StrokePolygon(bolt, new Color(1, 0.99f, 0.88f, 1), 1.5f);
            }
            else
            {
                c.FillPolygon(new[] { new Vector2(0, -10), new Vector2(8, 0), new Vector2(0, 10), new Vector2(-8, 0) }, color);
                c.FillCircle(new Vector2(2, 2), 2.5f, new Color(0.88f, 1f, 0.95f, 1));
            }
            return c.ToAtlasSprite("VoidFall_Pickup_" + kind);
        }

        private static Sprite[] BuildProjectileFrames(string kind)
        {
            var frames = new Sprite[ProjectileFrameCount];
            for (var index = 0; index < ProjectileFrameCount; index++)
            {
                frames[index] = BuildProjectile(
                    kind,
                    index / (float)ProjectileFrameCount * Mathf.PI * 2f,
                    "_Frame" + index);
            }

            return frames;
        }

        private static Sprite BuildProjectile(
            string kind,
            float rotation = 0f,
            string nameSuffix = "")
        {
            var radius = kind == "railgun" ? 30f : kind == "seeker" ? 20f : kind == "pistol" ? 13f : kind == "scattergun" ? 9f : kind == "curved" ? 10f : kind == "hydra-rib" ? 13f : 14f;
            var padding = kind == "pistol" ? 3.5f
                : kind == "scattergun" ? 4f
                : kind == "railgun" ? 2f
                : kind == "seeker" ? 4f
                : kind == "curved" ? 0f
                : kind == "hydra-rib" ? 2f
                : 4f;
            // orientedFrames() creates one square canvas from the source
            // image's diagonal plus four pixels. Preserve that source raster
            // contract instead of oversampling every projectile into a
            // different texture size; the existing world-size contract keeps
            // runtime dimensions unchanged.
            var canvas = new RasterCanvas(
                radius,
                padding,
                Mathf.RoundToInt(ProjectileCanvasSize(kind)));
            canvas.SetRotation(rotation);
            if (kind == "hydra-rib")
            {
                var shard = new[]
                {
                    new Vector2(-12f, -4f), new Vector2(-5f, -7f),
                    new Vector2(12f, -2f), new Vector2(15f, 0f),
                    new Vector2(12f, 2f), new Vector2(-5f, 7f),
                };
                canvas.FillPolygon(shard, ParseColor("#d8d7b2"));
                canvas.StrokePolygon(shard, ParseColor("#78ff5a"), 1.6f);
                canvas.FillPolygon(new[]
                {
                    new Vector2(-8f, -2f), new Vector2(10f, -1f),
                    new Vector2(13f, 0f), new Vector2(10f, 1f), new Vector2(-8f, 2f),
                }, ParseColor("#f4f0d2"));
            }
            else if (kind == "pistol")
            {
                canvas.FillPolygon(new[]
                {
                    new Vector2(-11, -2), new Vector2(5, -3), new Vector2(12, 0),
                    new Vector2(5, 3), new Vector2(-11, 2), new Vector2(-7, 0),
                }, ParseColor("#0891b2"));
                canvas.FillPolygon(new[]
                {
                    new Vector2(-5, -1), new Vector2(6, -1.5f), new Vector2(10, 0),
                    new Vector2(5, 1.5f), new Vector2(-5, 1),
                }, ParseColor("#67e8f9"));
                canvas.FillRect(new Vector2(7, 0), 4, 1.4f, ParseColor("#ecfeff"));
            }
            else if (kind == "scattergun")
            {
                canvas.FillPolygon(new[]
                {
                    new Vector2(-8, -2), new Vector2(1, -3), new Vector2(8, -1),
                    new Vector2(9, 1), new Vector2(2, 3), new Vector2(-6, 2),
                }, ParseColor("#c2410c"));
                canvas.FillPolygon(new[]
                {
                    new Vector2(-3, -1), new Vector2(4, -1.5f), new Vector2(8, 0),
                    new Vector2(3, 1.5f), new Vector2(-3, 1),
                }, ParseColor("#fb923c"));
                canvas.FillRect(new Vector2(5.5f, 0), 3, 1.2f, ParseColor("#ffedd5"));
            }
            else if (kind == "railgun")
            {
                canvas.FillRect(new Vector2(-22, 0), 8, 2, ParseColor("#6d28d9"));
                canvas.FillRect(new Vector2(-10.5f, 0), 7, 3, ParseColor("#6d28d9"));
                var body = new[]
                {
                    new Vector2(-4, -2.5f), new Vector2(20, -3), new Vector2(28, 0),
                    new Vector2(19, 3), new Vector2(-4, 2.5f), new Vector2(2, 0),
                };
                canvas.FillPolygon(body, ParseColor("#8b5cf6"));
                canvas.FillPolygon(new[]
                {
                    new Vector2(2, -1), new Vector2(21, -1.4f), new Vector2(26, 0),
                    new Vector2(20, 1.4f), new Vector2(2, 1),
                }, ParseColor("#ddd6fe"));
                canvas.FillRect(new Vector2(23, 0), 4, 1.3f, Color.white);
            }
            else if (kind == "seeker")
            {
                canvas.FillPolygon(new[] { new Vector2(-18, -1), new Vector2(-10, -3), new Vector2(-8, -1), new Vector2(-11, 0) }, ParseColor("#65a30d"));
                canvas.FillPolygon(new[] { new Vector2(-15, 1), new Vector2(-9, 0), new Vector2(-8, 2), new Vector2(-10, 3) }, ParseColor("#bef264"));
                var body = new[]
                {
                    new Vector2(-9, -3), new Vector2(8, -2.4f), new Vector2(16, 0),
                    new Vector2(8, 2.4f), new Vector2(-9, 3),
                };
                canvas.FillPolygon(body, ParseColor("#3f6212"));
                canvas.StrokePolygon(body, ParseColor("#a3e635"), 1.4f);
                canvas.FillPolygon(new[] { new Vector2(8, -2.4f), new Vector2(16, 0), new Vector2(8, 2.4f) }, ParseColor("#ecfccb"));
                canvas.FillRect(new Vector2(-0.5f, 0), 9, 2, ParseColor("#84cc16"));
                canvas.FillPolygon(new[] { new Vector2(-5, 2), new Vector2(2, 3), new Vector2(-3, 5) }, ParseColor("#4d7c0f"));
            }
            else if (kind == "curved")
            {
                canvas.FillCircle(Vector2.zero, 8.5f, new Color(0.1f, 0.04f, 0.06f, 1));
                canvas.StrokeCircle(Vector2.zero, 8.5f, ParseColor("#f87171"), 2f);
                canvas.FillCircle(new Vector2(-1, 1), 3, ParseColor("#fecaca"));
            }
            else if (kind == "gunner")
            {
                var body = new[]
                {
                    new Vector2(-12, -2), new Vector2(5, -3), new Vector2(12, 0),
                    new Vector2(5, 3), new Vector2(-12, 2),
                };
                canvas.FillPolygon(body, new Color(0.984f, 0.443f, 0.522f, 0.82f));
                canvas.FillRect(new Vector2(7, 0), 6, 2, ParseColor("#ffe4e6"));
            }
            else if (kind == "meteorShard")
            {
                var shard = new[]
                {
                    new Vector2(-8, -2), new Vector2(-2, -7), new Vector2(8, -4),
                    new Vector2(6, 5), new Vector2(-3, 8),
                };
                canvas.FillPolygon(shard, new Color(0.165f, 0.13f, 0.14f, 1));
                canvas.StrokePolygon(shard, new Color(0.42f, 0.29f, 0.23f, 1), 1.2f);
                canvas.FillCircle(new Vector2(-2, 1), 2.7f, new Color(0.29f, 0.23f, 0.2f, 1));
            }
            else
            {
                canvas.FillCircle(Vector2.zero, 7f, new Color(1f, 0.48f, 0.28f, 1));
                canvas.FillCircle(new Vector2(2, 1), 2.2f, new Color(1f, 0.9f, 0.78f, 1));
            }
            // The 32-frame orientation sets make this the largest family by far:
            // five weapon kinds x 32 frames is 160 of the distinct textures.
            return canvas.ToAtlasSprite("VoidFall_Projectile_" + kind + nameSuffix);
        }

        private static Sprite BuildMeteorShard(int variant)
        {
            const float radius = 9f;
            // React meteorShardSprite() uses cv(r * 2 + 4), so keep the
            // authored 22px source canvas instead of an upscaled 64px cache.
            var canvas = new RasterCanvas(radius, 2f, 22);
            var random = new System.Random(0x7d31 + variant * 3571);
            var rotation = (float)(random.NextDouble() * Mathf.PI * 2f);
            var points = 4 + variant % 3;
            var polygon = new Vector2[points];
            for (var index = 0; index < points; index++)
            {
                var angle = rotation + index / (float)points * Mathf.PI * 2f;
                var pointRadius = radius * (0.5f + (float)random.NextDouble() * 0.5f);
                polygon[index] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * pointRadius;
            }
            canvas.FillPolygon(polygon, new Color(0.165f, 0.13f, 0.14f, 1));
            canvas.StrokePolygon(polygon, new Color(0.42f, 0.29f, 0.23f, 1), 1.2f);
            canvas.FillCircle(new Vector2(-radius * 0.2f, -radius * 0.15f), radius * 0.3f, new Color(0.29f, 0.23f, 0.2f, 1));
            return canvas.ToAtlasSprite("VoidFall_Meteor_Shard_" + variant);
        }

        private static Sprite BuildMeteor(int variant, bool explosive)
        {
            var diameter = explosive
                ? new[] { 72f, 80f, 88f }[Mathf.Abs(variant) % 3]
                : new[] { 48f, 54f, 58f, 64f }[Mathf.Abs(variant) % 4];
            var radius = diameter * 0.5f;
            // meteorSprite()/explosiveMeteorSprite() use cv((r + pad) * 2),
            // so preserve each authored source canvas (ordinary 62/68/72/78,
            // explosive 92/100/108) instead of oversampling all variants to
            // one 128px raster. Runtime world size remains source-driven.
            var canvas = new RasterCanvas(
                radius,
                explosive ? 10f : 7f,
                Mathf.RoundToInt(MeteorCanvasSize(variant, explosive)));
            // sprites.ts rotates each baked variant before tracing its authored
            // rock outline, so the same silhouette never presents one fixed
            // face in every hazard slot.
            canvas.SetRotation(variant * (explosive ? 0.91f : 1.37f));
            var points = MeteorPoints(radius, variant + (explosive ? 3 : 0));
            if (!explosive)
            {
                // Browser meteorSprite(): exact body, clipped light/shadow
                // planes, deterministic off-centre craters, one fracture, and
                // two clipped directional rim strokes.
                canvas.FillPolygon(points, ParseColor("#241d20"));
                var lit = MeteorLightAngle;
                var lx = Mathf.Cos(lit);
                var ly = Mathf.Sin(lit);
                var meteorStream = unchecked((uint)(0x51d0 + variant * 7919));
                canvas.BeginClip(points);
                canvas.FillPolygon(new[]
                {
                    new Vector2(lx * radius * 1.2f - ly * radius * 1.2f, ly * radius * 1.2f + lx * radius * 1.2f),
                    new Vector2(lx * radius * 1.2f + ly * radius * 1.2f, ly * radius * 1.2f - lx * radius * 1.2f),
                    new Vector2(lx * radius * 0.05f + ly * radius * 1.2f, ly * radius * 0.05f - lx * radius * 1.2f),
                    new Vector2(lx * radius * 0.1f - ly * radius * 1.2f, ly * radius * 0.1f + lx * radius * 1.2f),
                }, ParseColor("#3b2f2c"));
                canvas.FillPolygon(new[]
                {
                    new Vector2(-lx * radius * 1.3f - ly * radius * 1.3f, -ly * radius * 1.3f + lx * radius * 1.3f),
                    new Vector2(-lx * radius * 1.3f + ly * radius * 1.3f, -ly * radius * 1.3f - lx * radius * 1.3f),
                    new Vector2(-lx * radius * 0.28f + ly * radius * 1.3f, -ly * radius * 0.28f - lx * radius * 1.3f),
                    new Vector2(-lx * radius * 0.34f - ly * radius * 1.3f, -ly * radius * 0.34f + lx * radius * 1.3f),
                }, ParseColor("#171316"));
                var craters = 1 + Mathf.Abs(variant) % 2;
                for (var craterIndex = 0; craterIndex < craters; craterIndex++)
                {
                    var angle = MeteorStreamNext(ref meteorStream) * Mathf.PI * 2f;
                    var distance = radius * (0.18f + MeteorStreamNext(ref meteorStream) * 0.42f);
                    var craterCenter = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                    var craterRadius = radius * (0.16f + MeteorStreamNext(ref meteorStream) * 0.14f);
                    var craterHeight = craterRadius * (0.62f + MeteorStreamNext(ref meteorStream) * 0.3f);
                    canvas.FillPolygon(
                        EllipsePoints(craterCenter, craterRadius, craterHeight, angle, 24),
                        ParseColor("#120f12"));
                    canvas.DrawArc(
                        craterCenter,
                        craterRadius * 1.02f,
                        lit - 1f,
                        lit + 1f,
                        Mathf.Max(1f, radius * 0.035f),
                        ParseColor("#4a3a34"));
                }
                canvas.DrawLine(new Vector2(-radius * 0.7f, -radius * 0.1f), new Vector2(-radius * 0.1f, radius * 0.16f), Mathf.Max(1, radius * 0.05f), ParseColor("#0f0c0f"));
                canvas.DrawLine(new Vector2(-radius * 0.1f, radius * 0.16f), new Vector2(radius * 0.42f, -radius * 0.02f), Mathf.Max(1, radius * 0.05f), ParseColor("#0f0c0f"));
                canvas.EndClip();
                canvas.BeginClip(points);
                canvas.DrawArc(
                    Vector2.zero,
                    radius * 0.97f,
                    lit - 1.05f,
                    lit + 0.72f,
                    Mathf.Max(1.6f, radius * 0.1f),
                    new Color(0.79f, 0.4f, 0.184f, 0.62f));
                canvas.DrawArc(
                    Vector2.zero,
                    radius * 0.95f,
                    lit - 0.62f,
                    lit + 0.34f,
                    Mathf.Max(1.1f, radius * 0.05f),
                    new Color(0.886f, 0.51f, 0.247f, 0.9f));
                canvas.EndClip();
            }
            else
            {
                // Browser explosiveMeteorSprite(): body, clipped angular
                // shadow, cooler crust patch, three round-capped fractures,
                // seeded embers, and a lit-side rim. The armed pulse is a
                // separate MeteorCore sprite, so do not bake a generic glow
                // into the meteor body.
                canvas.FillPolygon(points, ParseColor("#1a0d0d"));
                canvas.FillPolygon(new[]
                {
                    new Vector2(-radius * 1.3f, -radius * 0.2f), new Vector2(radius * 0.2f, -radius * 1.3f),
                    new Vector2(radius * 1.3f, -radius * 0.4f), new Vector2(radius * 0.1f, radius * 0.3f),
                }, ParseColor("#0e0708"));
                canvas.FillPolygon(
                    EllipsePoints(
                        new Vector2(-radius * 0.3f, radius * 0.34f),
                        radius * 0.5f,
                        radius * 0.3f,
                        0.6f,
                        24),
                    ParseColor("#2a1614"));

                var fractures = new[]
                {
                    new[] { -0.72f, -0.18f, -0.16f, 0.06f, 0.34f, -0.16f, 0.78f, 0.04f },
                    new[] { -0.3f, 0.6f, -0.04f, 0.18f, 0.16f, -0.2f },
                    new[] { 0.5f, 0.52f, 0.2f, 0.16f, -0.1f, -0.34f },
                };
                for (var fracture = 0; fracture < fractures.Length; fracture++)
                    DrawFracture(canvas, radius, fractures[fracture]);

                // Keep the source's deterministic makeStream consumption and
                // place five small specks on fracture nodes, not a random
                // particle field.
                var emberStream = unchecked((uint)(0x2f81 + variant * 6151));
                for (var ember = 0; ember < 5; ember++)
                {
                    var path = fractures[ember % fractures.Length];
                    var node = Mathf.Min(
                        path.Length / 2 - 1,
                        Mathf.FloorToInt(MeteorStreamNext(ref emberStream) * (path.Length / 2f)));
                    var alpha = 0.7f + MeteorStreamNext(ref emberStream) * 0.3f;
                    var emberX = path[node * 2] * radius +
                        (MeteorStreamNext(ref emberStream) - 0.5f) * radius * 0.16f;
                    var emberY = path[node * 2 + 1] * radius +
                        (MeteorStreamNext(ref emberStream) - 0.5f) * radius * 0.16f;
                    var emberRadius = Mathf.Max(
                        1.1f,
                        radius * (0.02f + MeteorStreamNext(ref emberStream) * 0.025f));
                    var emberColor = ember % 2 == 0
                        ? ParseColor("#fde68a")
                        : ParseColor("#fca94a");
                    emberColor.a = alpha;
                    canvas.FillCircle(new Vector2(emberX, emberY), emberRadius, emberColor);
                }

                canvas.DrawArc(
                    Vector2.zero,
                    radius * 0.96f,
                    MeteorLightAngle - 1.05f,
                    MeteorLightAngle + 1.05f,
                    Mathf.Max(2, radius * 0.075f),
                    new Color(0.94f, 0.53f, 0.23f, 0.95f));
            }
            return canvas.ToAtlasSprite("VoidFall_Meteor_" + variant + (explosive ? "_Explosive" : ""));
        }

        private static Vector2[] MeteorPoints(float radius, int variant)
        {
            var outline = ArenaRockOutlines[Mathf.Abs(variant) % ArenaRockOutlines.Length];
            var points = new Vector2[outline.Length];
            for (var index = 0; index < points.Length; index++)
            {
                var angle = index / (float)outline.Length * Mathf.PI * 2f - Mathf.PI * 0.5f;
                points[index] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * outline[index];
            }
            return points;
        }

        private static Vector2[] EllipsePoints(Vector2 centre, float radiusX, float radiusY, float rotation, int steps)
        {
            var points = new Vector2[Mathf.Max(12, steps)];
            var cosine = Mathf.Cos(rotation);
            var sine = Mathf.Sin(rotation);
            for (var index = 0; index < points.Length; index++)
            {
                var angle = index / (float)points.Length * Mathf.PI * 2f;
                var local = new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
                points[index] = centre + new Vector2(
                    local.x * cosine - local.y * sine,
                    local.x * sine + local.y * cosine);
            }
            return points;
        }

        private static float MeteorStreamNext(ref uint state)
        {
            state += 0x6d2b79f5u;
            var value = state;
            value = (value ^ (value >> 15)) * (value | 1u);
            value ^= value + ((value ^ (value >> 7)) * (value | 61u));
            return (value ^ (value >> 14)) / 4294967296f;
        }

        private static void DrawFracture(RasterCanvas canvas, float radius, float[] values)
        {
            if (values == null || values.Length < 4) return;
            var points = new Vector2[values.Length / 2];
            for (var index = 0; index < points.Length; index++)
                points[index] = new Vector2(values[index * 2] * radius, values[index * 2 + 1] * radius);
            canvas.DrawLineRound(points[0], points[1], radius * 0.15f, new Color(0.49f, 0.18f, 0.07f, 1));
            for (var index = 1; index < points.Length; index++)
                canvas.DrawLineRound(points[index - 1], points[index], radius * 0.08f, new Color(0.89f, 0.44f, 0.12f, 1));
            for (var index = 1; index < points.Length; index++)
                canvas.DrawLineRound(points[index - 1], points[index], radius * 0.034f, new Color(0.98f, 0.75f, 0.14f, 1));
        }

        private static void DrawCore(RasterCanvas canvas, float x, float y, float radius, Color accent, bool hit)
        {
            canvas.FillCircle(new Vector2(x, y), radius, hit ? Color.white : accent);
            // Browser drawCore(): hit #dbeafe, normal #f8fafc.
            canvas.FillCircle(new Vector2(x + radius * 0.18f, y - radius * 0.14f), Mathf.Max(1, radius * 0.34f), hit ? ParseColor("#dbeafe") : ParseColor("#f8fafc"));
        }

        private static Vector2[] Ngon(int count, float radius, float rotation)
        {
            var points = new Vector2[count];
            for (var index = 0; index < count; index++)
            {
                var angle = index / (float)count * Mathf.PI * 2f + rotation - Mathf.PI / 2f;
                points[index] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            return points;
        }

        private static Color ParseColor(string value)
        {
            return ColorUtility.TryParseHtmlString(value, out var color) ? color : Color.white;
        }

        private static Sprite Create(int size, Func<int, int, int, bool> filled)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "VoidFall_ProceduralSprite",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[size * size];
            var radius = size / 2;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    pixels[y * size + x] = filled(x, y, radius)
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(255, 255, 255, 0);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static void BlendWorkshopPixel(Color32[] pixels, int width, int x, int y, Color source)
        {
            if (pixels == null || x < 0 || y < 0 || x >= width || y >= pixels.Length / width || source.a <= 0f) return;
            var index = y * width + x;
            var destination = pixels[index];
            var sourceAlpha = Mathf.Clamp01(source.a);
            var destinationAlpha = destination.a / 255f;
            var outputAlpha = sourceAlpha + destinationAlpha * (1f - sourceAlpha);
            if (outputAlpha <= 0.0001f)
            {
                pixels[index] = new Color32(255, 255, 255, 0);
                return;
            }
            var destinationColor = new Color(
                destination.r / 255f,
                destination.g / 255f,
                destination.b / 255f,
                destinationAlpha);
            var output = new Color(
                (source.r * sourceAlpha + destinationColor.r * destinationAlpha * (1f - sourceAlpha)) / outputAlpha,
                (source.g * sourceAlpha + destinationColor.g * destinationAlpha * (1f - sourceAlpha)) / outputAlpha,
                (source.b * sourceAlpha + destinationColor.b * destinationAlpha * (1f - sourceAlpha)) / outputAlpha,
                outputAlpha);
            pixels[index] = output;
        }

        /// <summary>
        /// Packs small runtime-baked sprites into shared atlas pages.
        ///
        /// Every sprite used to own a private Texture2D. SpriteRenderer can only
        /// batch renderers that share a texture, so ~230 distinct sprites meant
        /// ~230 unbatchable draw calls. Packing them collapses that to one call
        /// per page.
        ///
        /// This is opt-in per sprite family rather than applied to every sprite,
        /// because a handful of sprites are consumed as raw textures rather than
        /// through SpriteRenderer: the particle system assigns
        /// ParticleDot().texture to its material, and the workshop preview draws
        /// Operative/PlayerRing/Dot/WorkshopPreviewLayer through
        /// GUI.DrawTexture. Those would render the whole page, so they stay
        /// standalone. The families that are packed are only ever assigned to
        /// SpriteRenderer.sprite.
        /// </summary>
        private static class SpriteAtlasPacker
        {
            private const int PageSize = 2048;
            // Bilinear filtering can reach half a texel outside the sprite rect.
            // A standalone texture with Clamp wrapping extended its own edge
            // pixel there; inside an atlas the neighbour would bleed in instead,
            // so the edge extension is baked into a padded border.
            private const int Padding = 2;

            private sealed class Page
            {
                public Texture2D Texture;
                public int CursorX;
                public int CursorY;
                public int RowHeight;
                public bool Dirty;
            }

            private static readonly List<Page> Pages = new List<Page>();

#if UNITY_EDITOR
            public static void ResetForBake()
            {
                for (var index = 0; index < Pages.Count; index++)
                {
                    var texture = Pages[index].Texture;
                    if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
                }
                Pages.Clear();
            }

            public static void ForgetAfterBakeCleanup()
            {
                Pages.Clear();
            }
#endif

            public static Sprite Add(
                Color32[] pixels,
                int width,
                int height,
                float pixelsPerUnit,
                string name)
            {
                if (pixels == null || width <= 0 || height <= 0) return null;

                var blockWidth = width + Padding * 2;
                var blockHeight = height + Padding * 2;
                if (blockWidth > PageSize || blockHeight > PageSize)
                    return Standalone(pixels, width, height, pixelsPerUnit, name);

                var page = Acquire(blockWidth, blockHeight, out var blockX, out var blockY);
                page.Texture.SetPixels32(
                    blockX,
                    blockY,
                    blockWidth,
                    blockHeight,
                    ExpandWithEdgeClamp(pixels, width, height));
                page.Dirty = true;

                var sprite = Sprite.Create(
                    page.Texture,
                    new Rect(blockX + Padding, blockY + Padding, width, height),
                    new Vector2(0.5f, 0.5f),
                    pixelsPerUnit);
                sprite.name = name;
                return sprite;
            }

            /// <summary>
            /// Uploads pages written since the last call. Deferred because Apply
            /// re-uploads the whole page, so doing it per sprite would cost one
            /// full-page upload per bake.
            /// </summary>
            public static void Flush()
            {
                for (var index = 0; index < Pages.Count; index++)
                {
                    var page = Pages[index];
                    if (!page.Dirty) continue;
                    // Keep the page readable: later sprites write into it.
                    page.Texture.Apply(false, false);
                    page.Dirty = false;
                }
            }

            private static Color32[] ExpandWithEdgeClamp(Color32[] pixels, int width, int height)
            {
                var blockWidth = width + Padding * 2;
                var blockHeight = height + Padding * 2;
                var block = new Color32[blockWidth * blockHeight];
                for (var y = 0; y < blockHeight; y++)
                {
                    var sourceY = Mathf.Clamp(y - Padding, 0, height - 1);
                    var sourceRow = sourceY * width;
                    var targetRow = y * blockWidth;
                    for (var x = 0; x < blockWidth; x++)
                    {
                        var sourceX = Mathf.Clamp(x - Padding, 0, width - 1);
                        block[targetRow + x] = pixels[sourceRow + sourceX];
                    }
                }
                return block;
            }

            // Shelf allocator: fill a row left to right, then start a new row
            // above it. Good enough for sprites of similar height and it never
            // needs to move an entry once placed.
            private static Page Acquire(int blockWidth, int blockHeight, out int x, out int y)
            {
                for (var index = 0; index < Pages.Count; index++)
                {
                    var page = Pages[index];
                    if (page.CursorX + blockWidth > PageSize)
                    {
                        page.CursorX = 0;
                        page.CursorY += page.RowHeight;
                        page.RowHeight = 0;
                    }
                    if (page.CursorY + blockHeight > PageSize) continue;

                    x = page.CursorX;
                    y = page.CursorY;
                    page.CursorX += blockWidth;
                    if (blockHeight > page.RowHeight) page.RowHeight = blockHeight;
                    return page;
                }

                var created = NewPage();
                Pages.Add(created);
                x = 0;
                y = 0;
                created.CursorX = blockWidth;
                created.RowHeight = blockHeight;
                return created;
            }

            private static Page NewPage()
            {
                var texture = new Texture2D(PageSize, PageSize, TextureFormat.RGBA32, false)
                {
                    name = "VoidFall_SpriteAtlas_" + Pages.Count,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
                var clear = new Color32[PageSize * PageSize];
                texture.SetPixels32(clear);
                texture.Apply(false, false);
                return new Page { Texture = texture };
            }

            private static Sprite Standalone(
                Color32[] pixels,
                int width,
                int height,
                float pixelsPerUnit,
                string name)
            {
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    name = name + "_Texture",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                var sprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, width, height),
                    new Vector2(0.5f, 0.5f),
                    pixelsPerUnit);
                sprite.name = name;
                return sprite;
            }
        }

        /// <summary>
        /// Uploads any atlas pages that have pending writes. Cheap when clean.
        /// </summary>
        public static void FlushAtlas()
        {
            SpriteAtlasPacker.Flush();
        }

        private sealed class RasterCanvas
        {
            private readonly int _size;
            private readonly float _center;
            private readonly float _scale;
            private readonly Color32[] _pixels;
            private const int CoverageSamplesPerAxis = 4;
            private const int CoverageSampleCount = CoverageSamplesPerAxis * CoverageSamplesPerAxis;
            private const float CanvasMiterLimit = 10f;
            private float _rotation;
            private float _rotationCos = 1f;
            private float _rotationSin;
            private Vector2[] _clipPolygon;

            public RasterCanvas(float radius, float padding, int size)
            {
                _size = size;
                _center = size * 0.5f;
                _scale = _center / Mathf.Max(1f, radius + padding);
                _pixels = new Color32[size * size];
                for (var index = 0; index < _pixels.Length; index++)
                    _pixels[index] = new Color32(255, 255, 255, 0);
            }

            public void SetRotation(float radians)
            {
                _rotation = radians;
                _rotationCos = Mathf.Cos(radians);
                _rotationSin = Mathf.Sin(radians);
            }

            public void BeginClip(Vector2[] polygon)
            {
                _clipPolygon = polygon;
            }

            public void EndClip()
            {
                _clipPolygon = null;
            }

            public void Glow(float radius, Color color, float alpha)
            {
                Glow(Vector2.zero, radius, color, alpha);
            }

            public void Glow(Vector2 centre, float radius, Color color, float alpha)
            {
                // Browser sprites.ts glow(): radial stops at 10%, 55%, and
                // 100%, with the middle stop at 28% of requested alpha.
                RadialThreeStopGradient(
                    centre,
                    radius * 0.1f,
                    new Color(color.r, color.g, color.b, alpha),
                    radius * 0.55f,
                    new Color(color.r, color.g, color.b, alpha * 0.28f),
                    radius,
                    new Color(color.r, color.g, color.b, 0));
            }

            public void RadialGradient(float innerRadius, float outerRadius, Color color, float alpha)
            {
                var inner = Mathf.Max(0, innerRadius);
                var outer = Mathf.Max(inner + 0.0001f, outerRadius);
                var min = Mathf.Max(0, Mathf.FloorToInt(_center - outer * _scale - 1));
                var max = Mathf.Min(_size - 1, Mathf.CeilToInt(_center + outer * _scale + 1));
                for (var y = min; y <= max; y++)
                {
                    for (var x = min; x <= max; x++)
                    {
                        var sourceAlpha = 0f;
                        var sourceRed = 0f;
                        var sourceGreen = 0f;
                        var sourceBlue = 0f;
                        for (var sampleY = 0; sampleY < CoverageSamplesPerAxis; sampleY++)
                        {
                            for (var sampleX = 0; sampleX < CoverageSamplesPerAxis; sampleX++)
                            {
                                var distance = SampleWorld(x, y, sampleX, sampleY).magnitude;
                                if (distance > outer) continue;
                                var falloff = distance <= inner
                                    ? 1f
                                    : 1f - Mathf.Clamp01((distance - inner) / (outer - inner));
                                AccumulateSample(
                                    new Color(color.r, color.g, color.b, alpha * falloff),
                                    ref sourceAlpha,
                                    ref sourceRed,
                                    ref sourceGreen,
                                    ref sourceBlue);
                            }
                        }
                        BlendAccumulated(x, y, sourceAlpha, sourceRed, sourceGreen, sourceBlue);
                    }
                }
            }

            public void RadialColorGradient(
                Vector2 center,
                float innerRadius,
                Color innerColor,
                float outerRadius,
                Color outerColor)
            {
                var inner = Mathf.Max(0f, innerRadius);
                var outer = Mathf.Max(inner + 0.0001f, outerRadius);
                var minX = Mathf.Max(0, Mathf.FloorToInt(_center + (center.x - outer) * _scale - 1));
                var maxX = Mathf.Min(_size - 1, Mathf.CeilToInt(_center + (center.x + outer) * _scale + 1));
                var minY = Mathf.Max(0, Mathf.FloorToInt(_center + (center.y - outer) * _scale - 1));
                var maxY = Mathf.Min(_size - 1, Mathf.CeilToInt(_center + (center.y + outer) * _scale + 1));
                for (var y = minY; y <= maxY; y++)
                {
                    for (var x = minX; x <= maxX; x++)
                    {
                        var sourceAlpha = 0f;
                        var sourceRed = 0f;
                        var sourceGreen = 0f;
                        var sourceBlue = 0f;
                        for (var sampleY = 0; sampleY < CoverageSamplesPerAxis; sampleY++)
                        {
                            for (var sampleX = 0; sampleX < CoverageSamplesPerAxis; sampleX++)
                            {
                                var distance = (SampleWorld(x, y, sampleX, sampleY) - center).magnitude;
                                if (distance > outer) continue;
                                var color = Color.Lerp(innerColor, outerColor,
                                    Mathf.InverseLerp(inner, outer, distance));
                                AccumulateSample(color, ref sourceAlpha, ref sourceRed, ref sourceGreen, ref sourceBlue);
                            }
                        }
                        BlendAccumulated(x, y, sourceAlpha, sourceRed, sourceGreen, sourceBlue);
                    }
                }
            }

            /// <summary>
            /// Reproduces CanvasRenderingContext2D.createRadialGradient(x0,
            /// y0, r0, x1, y1, r1) for a separately clipped source path.
            /// React's operative uses a shifted inner circle and a body-centred
            /// outer circle; reducing that to one centre changes both the
            /// highlight shape and the pixels outside the body path.
            /// </summary>
            public void RadialTwoPointColorGradient(
                Vector2 startCenter,
                float startRadius,
                Color startColor,
                Vector2 endCenter,
                float endRadius,
                Color endColor,
                Vector2 clipCenter,
                float clipRadius)
            {
                var clip = Mathf.Max(0f, clipRadius);
                var minX = Mathf.Max(0, Mathf.FloorToInt(_center + (clipCenter.x - clip) * _scale - 1));
                var maxX = Mathf.Min(_size - 1, Mathf.CeilToInt(_center + (clipCenter.x + clip) * _scale + 1));
                var minY = Mathf.Max(0, Mathf.FloorToInt(_center + (clipCenter.y - clip) * _scale - 1));
                var maxY = Mathf.Min(_size - 1, Mathf.CeilToInt(_center + (clipCenter.y + clip) * _scale + 1));
                for (var y = minY; y <= maxY; y++)
                {
                    for (var x = minX; x <= maxX; x++)
                    {
                        var sourceAlpha = 0f;
                        var sourceRed = 0f;
                        var sourceGreen = 0f;
                        var sourceBlue = 0f;
                        for (var sampleY = 0; sampleY < CoverageSamplesPerAxis; sampleY++)
                        {
                            for (var sampleX = 0; sampleX < CoverageSamplesPerAxis; sampleX++)
                            {
                                var point = SampleWorld(x, y, sampleX, sampleY);
                                var clipDelta = point - clipCenter;
                                var clipDistance = clipDelta.magnitude;
                                if (clipDistance > clip) continue;
                                var t = RadialGradientParameter(
                                    point,
                                    startCenter,
                                    Mathf.Max(0f, startRadius),
                                    endCenter,
                                    Mathf.Max(0f, endRadius));
                                var color = Color.Lerp(startColor, endColor, t);
                                AccumulateSample(color, ref sourceAlpha, ref sourceRed, ref sourceGreen, ref sourceBlue);
                            }
                        }
                        BlendAccumulated(x, y, sourceAlpha, sourceRed, sourceGreen, sourceBlue);
                    }
                }
            }

            private static float RadialGradientParameter(
                Vector2 point,
                Vector2 startCenter,
                float startRadius,
                Vector2 endCenter,
                float endRadius)
            {
                var centreDelta = endCenter - startCenter;
                var radiusDelta = endRadius - startRadius;
                var pointDelta = point - startCenter;
                var a = Vector2.Dot(centreDelta, centreDelta) - radiusDelta * radiusDelta;
                var b = -2f * Vector2.Dot(pointDelta, centreDelta) - 2f * startRadius * radiusDelta;
                var c = Vector2.Dot(pointDelta, pointDelta) - startRadius * startRadius;
                if (Mathf.Abs(a) < 0.0001f)
                {
                    if (Mathf.Abs(b) < 0.0001f) return 0f;
                    return Mathf.Clamp01(-c / b);
                }

                var discriminant = b * b - 4f * a * c;
                if (discriminant <= 0f) return 0f;
                var root = (-b - Mathf.Sqrt(discriminant)) / (2f * a);
                return Mathf.Clamp01(root);
            }

            public void RadialFourStopGradient(
                Vector2 center,
                float outerRadius,
                Color innerColor,
                Color firstColor,
                float firstStop,
                Color secondColor,
                float secondStop,
                Color outerColor)
            {
                var outer = Mathf.Max(0.0001f, outerRadius);
                var first = Mathf.Clamp(firstStop, 0.0001f, 0.9999f);
                var second = Mathf.Clamp(secondStop, first + 0.0001f, 0.9999f);
                var minX = Mathf.Max(0, Mathf.FloorToInt(_center + (center.x - outer) * _scale - 1));
                var maxX = Mathf.Min(_size - 1, Mathf.CeilToInt(_center + (center.x + outer) * _scale + 1));
                var minY = Mathf.Max(0, Mathf.FloorToInt(_center + (center.y - outer) * _scale - 1));
                var maxY = Mathf.Min(_size - 1, Mathf.CeilToInt(_center + (center.y + outer) * _scale + 1));
                for (var y = minY; y <= maxY; y++)
                {
                    for (var x = minX; x <= maxX; x++)
                    {
                        var sourceAlpha = 0f;
                        var sourceRed = 0f;
                        var sourceGreen = 0f;
                        var sourceBlue = 0f;
                        for (var sampleY = 0; sampleY < CoverageSamplesPerAxis; sampleY++)
                        {
                            for (var sampleX = 0; sampleX < CoverageSamplesPerAxis; sampleX++)
                            {
                                var distance = (SampleWorld(x, y, sampleX, sampleY) - center).magnitude;
                                if (distance > outer) continue;
                                var t = Mathf.Clamp01(distance / outer);
                                var color = t <= first
                                    ? Color.Lerp(innerColor, firstColor, t / first)
                                    : t <= second
                                        ? Color.Lerp(firstColor, secondColor, (t - first) / (second - first))
                                        : Color.Lerp(secondColor, outerColor, (t - second) / (1f - second));
                                AccumulateSample(color, ref sourceAlpha, ref sourceRed, ref sourceGreen, ref sourceBlue);
                            }
                        }
                        BlendAccumulated(x, y, sourceAlpha, sourceRed, sourceGreen, sourceBlue);
                    }
                }
            }

            public void RadialThreeStopGradient(
                Vector2 center,
                float innerRadius,
                Color innerColor,
                float middleRadius,
                Color middleColor,
                float outerRadius,
                Color outerColor)
            {
                var inner = Mathf.Max(0f, innerRadius);
                var middle = Mathf.Max(inner + 0.0001f, middleRadius);
                var outer = Mathf.Max(middle + 0.0001f, outerRadius);
                var minX = Mathf.Max(0, Mathf.FloorToInt(_center + (center.x - outer) * _scale - 1));
                var maxX = Mathf.Min(_size - 1, Mathf.CeilToInt(_center + (center.x + outer) * _scale + 1));
                var minY = Mathf.Max(0, Mathf.FloorToInt(_center + (center.y - outer) * _scale - 1));
                var maxY = Mathf.Min(_size - 1, Mathf.CeilToInt(_center + (center.y + outer) * _scale + 1));
                for (var y = minY; y <= maxY; y++)
                {
                    for (var x = minX; x <= maxX; x++)
                    {
                        var sourceAlpha = 0f;
                        var sourceRed = 0f;
                        var sourceGreen = 0f;
                        var sourceBlue = 0f;
                        for (var sampleY = 0; sampleY < CoverageSamplesPerAxis; sampleY++)
                        {
                            for (var sampleX = 0; sampleX < CoverageSamplesPerAxis; sampleX++)
                            {
                                var distance = (SampleWorld(x, y, sampleX, sampleY) - center).magnitude;
                                if (distance > outer) continue;
                                var color = distance <= inner
                                    ? innerColor
                                    : distance <= middle
                                        ? Color.Lerp(innerColor, middleColor, (distance - inner) / (middle - inner))
                                        : Color.Lerp(middleColor, outerColor, (distance - middle) / (outer - middle));
                                AccumulateSample(color, ref sourceAlpha, ref sourceRed, ref sourceGreen, ref sourceBlue);
                            }
                        }
                        BlendAccumulated(x, y, sourceAlpha, sourceRed, sourceGreen, sourceBlue);
                    }
                }
            }

            public void RadialAlphaGradient(
                float innerRadius,
                float innerAlpha,
                float middleRadius,
                float middleAlpha,
                float outerRadius)
            {
                var inner = Mathf.Max(0, innerRadius);
                var middle = Mathf.Max(inner + 0.0001f, middleRadius);
                var outer = Mathf.Max(middle + 0.0001f, outerRadius);
                var min = Mathf.Max(0, Mathf.FloorToInt(_center - outer * _scale - 1));
                var max = Mathf.Min(_size - 1, Mathf.CeilToInt(_center + outer * _scale + 1));
                for (var y = min; y <= max; y++)
                {
                    for (var x = min; x <= max; x++)
                    {
                        var sourceAlpha = 0f;
                        var sourceRed = 0f;
                        var sourceGreen = 0f;
                        var sourceBlue = 0f;
                        for (var sampleY = 0; sampleY < CoverageSamplesPerAxis; sampleY++)
                        {
                            for (var sampleX = 0; sampleX < CoverageSamplesPerAxis; sampleX++)
                            {
                                var distance = SampleWorld(x, y, sampleX, sampleY).magnitude;
                                if (distance > outer) continue;
                                var alpha = distance <= inner
                                    ? innerAlpha
                                    : distance <= middle
                                        ? Mathf.Lerp(innerAlpha, middleAlpha, (distance - inner) / (middle - inner))
                                        : Mathf.Lerp(middleAlpha, 0f, (distance - middle) / (outer - middle));
                                AccumulateSample(
                                    new Color(1f, 1f, 1f, Mathf.Clamp01(alpha)),
                                    ref sourceAlpha,
                                    ref sourceRed,
                                    ref sourceGreen,
                                    ref sourceBlue);
                            }
                        }
                        BlendAccumulated(x, y, sourceAlpha, sourceRed, sourceGreen, sourceBlue);
                    }
                }
            }

            public void MaskCircle(Vector2 centre, float radius)
            {
                var radiusSquared = radius * radius;
                for (var y = 0; y < _size; y++)
                {
                    for (var x = 0; x < _size; x++)
                    {
                        var world = ToWorld(x + 0.5f, y + 0.5f);
                        var coverage = (world - centre).sqrMagnitude > radiusSquared
                            ? CircleCoverage(centre, radius, x, y)
                            : 1f;
                        if (coverage <= 0f) _pixels[y * _size + x].a = 0;
                        else if (coverage < 1f)
                        {
                            var pixel = _pixels[y * _size + x];
                            pixel.a = (byte)Mathf.RoundToInt(pixel.a * coverage);
                            _pixels[y * _size + x] = pixel;
                        }
                    }
                }
            }

            public void FillCircle(Vector2 center, float radius, Color color)
            {
                var minX = Mathf.Max(0, Mathf.FloorToInt(_center + (center.x - radius) * _scale - 1));
                var maxX = Mathf.Min(_size - 1, Mathf.CeilToInt(_center + (center.x + radius) * _scale + 1));
                var minY = Mathf.Max(0, Mathf.FloorToInt(_center + (center.y - radius) * _scale - 1));
                var maxY = Mathf.Min(_size - 1, Mathf.CeilToInt(_center + (center.y + radius) * _scale + 1));
                for (var y = minY; y <= maxY; y++)
                {
                    for (var x = minX; x <= maxX; x++)
                    {
                        BlendCoverage(x, y, color, CircleCoverage(center, radius, x, y));
                    }
                }
            }

            public void StrokeCircle(Vector2 center, float radius, Color color, float width)
            {
                var steps = Mathf.Max(16, Mathf.CeilToInt(radius * _scale * 2.5f));
                var previous = center + new Vector2(Mathf.Cos(0), Mathf.Sin(0)) * radius;
                for (var index = 1; index <= steps; index++)
                {
                    var angle = index / (float)steps * Mathf.PI * 2f;
                    var next = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    DrawLine(previous, next, width, color);
                    previous = next;
                }
            }

            public void FillPolygon(Vector2[] points, Color color)
            {
                if (points == null || points.Length < 3) return;
                var minX = _size - 1;
                var minY = _size - 1;
                var maxX = 0;
                var maxY = 0;
                foreach (var point in points)
                {
                    var pixel = ToPixel(point);
                    minX = Mathf.Min(minX, Mathf.FloorToInt(pixel.x));
                    maxX = Mathf.Max(maxX, Mathf.CeilToInt(pixel.x));
                    minY = Mathf.Min(minY, Mathf.FloorToInt(pixel.y));
                    maxY = Mathf.Max(maxY, Mathf.CeilToInt(pixel.y));
                }
                minX = Mathf.Clamp(minX, 0, _size - 1);
                maxX = Mathf.Clamp(maxX, 0, _size - 1);
                minY = Mathf.Clamp(minY, 0, _size - 1);
                maxY = Mathf.Clamp(maxY, 0, _size - 1);
                for (var y = minY; y <= maxY; y++)
                {
                    for (var x = minX; x <= maxX; x++)
                    {
                        BlendCoverage(x, y, color, PolygonCoverage(points, x, y));
                    }
                }
            }

            public void ErasePolygon(Vector2[] points)
            {
                if (points == null || points.Length < 3) return;
                var minX = _size - 1;
                var minY = _size - 1;
                var maxX = 0;
                var maxY = 0;
                foreach (var point in points)
                {
                    var pixel = ToPixel(point);
                    minX = Mathf.Min(minX, Mathf.FloorToInt(pixel.x));
                    maxX = Mathf.Max(maxX, Mathf.CeilToInt(pixel.x));
                    minY = Mathf.Min(minY, Mathf.FloorToInt(pixel.y));
                    maxY = Mathf.Max(maxY, Mathf.CeilToInt(pixel.y));
                }
                minX = Mathf.Clamp(minX, 0, _size - 1);
                maxX = Mathf.Clamp(maxX, 0, _size - 1);
                minY = Mathf.Clamp(minY, 0, _size - 1);
                maxY = Mathf.Clamp(maxY, 0, _size - 1);
                for (var y = minY; y <= maxY; y++)
                {
                    for (var x = minX; x <= maxX; x++)
                    {
                        var coverage = PolygonCoverage(points, x, y);
                        if (coverage <= 0f) continue;
                        var index = y * _size + x;
                        var pixel = _pixels[index];
                        pixel.a = (byte)Mathf.RoundToInt(pixel.a * (1f - coverage));
                        _pixels[index] = pixel;
                    }
                }
            }

            public void FillPolygonVerticalGradient(
                Vector2[] points,
                float top,
                float bottom,
                Color topColor,
                Color middleColor,
                Color bottomColor,
                float middleStop)
            {
                if (points == null || points.Length < 3) return;
                var minX = _size - 1;
                var maxX = 0;
                var minY = _size - 1;
                var maxY = 0;
                for (var index = 0; index < points.Length; index++)
                {
                    var pixel = ToPixel(points[index]);
                    minX = Mathf.Min(minX, Mathf.FloorToInt(pixel.x) - 1);
                    maxX = Mathf.Max(maxX, Mathf.CeilToInt(pixel.x) + 1);
                    minY = Mathf.Min(minY, Mathf.FloorToInt(pixel.y) - 1);
                    maxY = Mathf.Max(maxY, Mathf.CeilToInt(pixel.y) + 1);
                }
                minX = Mathf.Clamp(minX, 0, _size - 1);
                maxX = Mathf.Clamp(maxX, 0, _size - 1);
                minY = Mathf.Clamp(minY, 0, _size - 1);
                maxY = Mathf.Clamp(maxY, 0, _size - 1);
                var safeMiddle = Mathf.Clamp01(middleStop);
                for (var y = minY; y <= maxY; y++)
                {
                    for (var x = minX; x <= maxX; x++)
                    {
                        var sourceAlpha = 0f;
                        var sourceRed = 0f;
                        var sourceGreen = 0f;
                        var sourceBlue = 0f;
                        for (var sampleY = 0; sampleY < CoverageSamplesPerAxis; sampleY++)
                        {
                            for (var sampleX = 0; sampleX < CoverageSamplesPerAxis; sampleX++)
                            {
                                var world = SampleWorld(x, y, sampleX, sampleY);
                                if (!Contains(points, world)) continue;
                                var t = Mathf.InverseLerp(top, bottom, world.y);
                                var color = t <= safeMiddle
                                    ? Color.Lerp(topColor, middleColor, t / Mathf.Max(0.0001f, safeMiddle))
                                    : Color.Lerp(middleColor, bottomColor, (t - safeMiddle) / Mathf.Max(0.0001f, 1f - safeMiddle));
                                AccumulateSample(color, ref sourceAlpha, ref sourceRed, ref sourceGreen, ref sourceBlue);
                            }
                        }
                        BlendAccumulated(x, y, sourceAlpha, sourceRed, sourceGreen, sourceBlue);
                    }
                }
            }

            public void StrokePolygon(Vector2[] points, Color color, float width)
            {
                if (points == null || points.Length < 2) return;
                if (points.Length == 2)
                {
                    DrawLine(points[0], points[1], width, color);
                    return;
                }

                // Canvas2D strokes a closed path once. Rasterizing each edge
                // independently would double-blend translucent overlaps and
                // loses the browser's default miter joins at convex corners.
                var half = Mathf.Max(0.5f, width * 0.5f);
                var pixelHalf = half * CanvasMiterLimit * _scale;
                var minX = _size - 1;
                var minY = _size - 1;
                var maxX = 0;
                var maxY = 0;
                for (var index = 0; index < points.Length; index++)
                {
                    var pixel = ToPixel(points[index]);
                    minX = Mathf.Min(minX, Mathf.FloorToInt(pixel.x - pixelHalf - 1));
                    maxX = Mathf.Max(maxX, Mathf.CeilToInt(pixel.x + pixelHalf + 1));
                    minY = Mathf.Min(minY, Mathf.FloorToInt(pixel.y - pixelHalf - 1));
                    maxY = Mathf.Max(maxY, Mathf.CeilToInt(pixel.y + pixelHalf + 1));
                }
                minX = Mathf.Max(0, minX);
                minY = Mathf.Max(0, minY);
                maxX = Mathf.Min(_size - 1, maxX);
                maxY = Mathf.Min(_size - 1, maxY);
                for (var y = minY; y <= maxY; y++)
                {
                    for (var x = minX; x <= maxX; x++)
                        BlendCoverage(x, y, color, StrokePolygonCoverage(points, half, x, y));
                }
            }

            public void DrawLine(Vector2 from, Vector2 to, float width, Color color)
            {
                // CanvasRenderingContext2D defaults lineCap to "butt".
                DrawLineInternal(from, to, width, color, false);
            }

            public void DrawLineButt(Vector2 from, Vector2 to, float width, Color color)
            {
                DrawLineInternal(from, to, width, color, false);
            }

            public void DrawLineRound(Vector2 from, Vector2 to, float width, Color color)
            {
                DrawLineInternal(from, to, width, color, true);
            }

            private void DrawLineInternal(Vector2 from, Vector2 to, float width, Color color, bool roundCaps)
            {
                var half = Mathf.Max(0.5f, width * 0.5f);
                var pixelHalf = half * _scale;
                var fromPixel = ToPixel(from);
                var toPixel = ToPixel(to);
                var minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(fromPixel.x, toPixel.x) - pixelHalf - 1));
                var maxX = Mathf.Min(_size - 1, Mathf.CeilToInt(Mathf.Max(fromPixel.x, toPixel.x) + pixelHalf + 1));
                var minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(fromPixel.y, toPixel.y) - pixelHalf - 1));
                var maxY = Mathf.Min(_size - 1, Mathf.CeilToInt(Mathf.Max(fromPixel.y, toPixel.y) + pixelHalf + 1));
                for (var y = minY; y <= maxY; y++)
                {
                    for (var x = minX; x <= maxX; x++)
                    {
                        BlendCoverage(x, y, color, LineCoverage(from, to, half, x, y, roundCaps));
                    }
                }
            }

            public void DrawGradientLine(
                Vector2 from,
                Vector2 to,
                float width,
                Color startColor,
                Color endColor,
                int segments = 16)
            {
                var safeSegments = Mathf.Max(1, segments);
                var previous = from;
                for (var index = 1; index <= safeSegments; index++)
                {
                    var startT = (index - 1) / (float)safeSegments;
                    var endT = index / (float)safeSegments;
                    var next = Vector2.Lerp(from, to, endT);
                    var colour = Color.Lerp(startColor, endColor, (startT + endT) * 0.5f);
                    DrawLine(previous, next, width, colour);
                    previous = next;
                }
            }

            public void DrawArc(Vector2 center, float radius, float start, float end, float width, Color color)
            {
                // CanvasRenderingContext2D defaults lineCap to "butt".
                DrawArcInternal(center, radius, start, end, width, color, false);
            }

            public void DrawArcButt(Vector2 center, float radius, float start, float end, float width, Color color)
            {
                DrawArcInternal(center, radius, start, end, width, color, false);
            }

            public void DrawArcRound(Vector2 center, float radius, float start, float end, float width, Color color)
            {
                DrawArcInternal(center, radius, start, end, width, color, true);
            }

            private void DrawArcInternal(Vector2 center, float radius, float start, float end, float width, Color color, bool roundCaps)
            {
                var delta = end - start;
                if (Mathf.Abs(delta) < 0.001f) delta = Mathf.PI * 2f;
                var steps = Mathf.Max(12, Mathf.CeilToInt(Mathf.Abs(delta) * radius * _scale * 0.8f));
                var previous = center + new Vector2(Mathf.Cos(start), Mathf.Sin(start)) * radius;
                for (var index = 1; index <= steps; index++)
                {
                    var angle = start + delta * index / steps;
                    var next = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    DrawLineInternal(previous, next, width, color, roundCaps);
                    previous = next;
                }
            }

            public void DrawDashedArc(
                Vector2 center,
                float radius,
                float start,
                float end,
                float width,
                Color color,
                float dashLength,
                float gapLength)
            {
                if (radius <= 0.0001f) return;
                var delta = end - start;
                if (Mathf.Abs(delta) < 0.001f) delta = Mathf.PI * 2f;
                var direction = Mathf.Sign(delta);
                var totalLength = Mathf.Abs(delta) * radius;
                var safeDash = Mathf.Max(0.1f, dashLength);
                var safeGap = Mathf.Max(0.1f, gapLength);
                for (var distance = 0f; distance < totalLength; distance += safeDash + safeGap)
                {
                    var dashEnd = Mathf.Min(totalLength, distance + safeDash);
                    var dashStartAngle = start + direction * distance / radius;
                    var dashEndAngle = start + direction * dashEnd / radius;
                    DrawArc(center, radius, dashStartAngle, dashEndAngle, width, color);
                }
            }

            public void FillRect(Vector2 center, float width, float height, Color color)
            {
                FillPolygon(new[]
                {
                    center + new Vector2(-width * 0.5f, -height * 0.5f),
                    center + new Vector2(width * 0.5f, -height * 0.5f),
                    center + new Vector2(width * 0.5f, height * 0.5f),
                    center + new Vector2(-width * 0.5f, height * 0.5f),
                }, color);
            }

            public void StrokeRect(Vector2 center, float width, float height, Color color, float lineWidth)
            {
                var corners = new[]
                {
                    center + new Vector2(-width * 0.5f, -height * 0.5f),
                    center + new Vector2(width * 0.5f, -height * 0.5f),
                    center + new Vector2(width * 0.5f, height * 0.5f),
                    center + new Vector2(-width * 0.5f, height * 0.5f),
                };
                StrokePolygon(corners, color, lineWidth);
            }

            public Sprite ToSprite(string name)
            {
                return ToSprite(name, false);
            }

            /// <summary>
            /// Packs into a shared atlas page instead of allocating a private
            /// texture, so renderers using different sprites can batch. Only for
            /// sprites consumed through SpriteRenderer.sprite; anything that
            /// reads the raw texture must use <see cref="ToSprite(string, bool)"/>.
            /// Geometry is unchanged: the rect is still the full canvas and
            /// pixelsPerUnit is still the canvas size, so world size is identical.
            /// </summary>
            public Sprite ToAtlasSprite(string name)
            {
                return SpriteAtlasPacker.Add(_pixels, _size, _size, _size, name);
            }

            public Sprite ToSprite(string name, bool keepReadable)
            {
                var texture = new Texture2D(_size, _size, TextureFormat.RGBA32, false)
                {
                    name = name + "_Texture",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
                texture.SetPixels32(_pixels);
                texture.Apply(false, !keepReadable);
                return Sprite.Create(texture, new Rect(0, 0, _size, _size), new Vector2(0.5f, 0.5f), _size);
            }

            public Sprite ToSprite(
                string name,
                bool keepReadable,
                int cropY,
                int cropHeight,
                float pixelsPerUnit)
            {
                cropY = Mathf.Clamp(cropY, 0, _size - 1);
                cropHeight = Mathf.Clamp(cropHeight, 1, _size - cropY);
                var pixels = new Color32[_size * cropHeight];
                for (var row = 0; row < cropHeight; row++)
                    Array.Copy(_pixels, (cropY + row) * _size, pixels, row * _size, _size);
                var texture = new Texture2D(_size, cropHeight, TextureFormat.RGBA32, false)
                {
                    name = name + "_Texture",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
                texture.SetPixels32(pixels);
                texture.Apply(false, !keepReadable);
                return Sprite.Create(
                    texture,
                    new Rect(0, 0, _size, cropHeight),
                    new Vector2(0.5f, 0.5f),
                    pixelsPerUnit);
            }

            private Vector2 ToPixel(Vector2 world)
            {
                var rotated = new Vector2(
                    world.x * _rotationCos - world.y * _rotationSin,
                    world.x * _rotationSin + world.y * _rotationCos);
                return new Vector2(_center + rotated.x * _scale, _center + rotated.y * _scale);
            }

            private Vector2 ToWorld(float pixelX, float pixelY)
            {
                var rotated = new Vector2((pixelX - _center) / _scale, (pixelY - _center) / _scale);
                return new Vector2(
                    rotated.x * _rotationCos + rotated.y * _rotationSin,
                    -rotated.x * _rotationSin + rotated.y * _rotationCos);
            }

            private Vector2 SampleWorld(int x, int y, int sampleX, int sampleY)
            {
                return ToWorld(
                    x + (sampleX + 0.5f) / CoverageSamplesPerAxis,
                    y + (sampleY + 0.5f) / CoverageSamplesPerAxis);
            }

            private float CircleCoverage(Vector2 centre, float radius, int x, int y)
            {
                if (radius <= 0f) return 0f;
                var radiusSquared = radius * radius;
                var covered = 0;
                for (var sampleY = 0; sampleY < CoverageSamplesPerAxis; sampleY++)
                {
                    for (var sampleX = 0; sampleX < CoverageSamplesPerAxis; sampleX++)
                    {
                        var delta = SampleWorld(x, y, sampleX, sampleY) - centre;
                        if (delta.sqrMagnitude <= radiusSquared) covered++;
                    }
                }
                return covered / (float)CoverageSampleCount;
            }

            private float PolygonCoverage(Vector2[] points, int x, int y)
            {
                if (points == null || points.Length < 3) return 0f;
                var covered = 0;
                for (var sampleY = 0; sampleY < CoverageSamplesPerAxis; sampleY++)
                {
                    for (var sampleX = 0; sampleX < CoverageSamplesPerAxis; sampleX++)
                    {
                        if (Contains(points, SampleWorld(x, y, sampleX, sampleY))) covered++;
                    }
                }
                return covered / (float)CoverageSampleCount;
            }

            private float StrokePolygonCoverage(Vector2[] points, float half, int x, int y)
            {
                var covered = 0;
                for (var sampleY = 0; sampleY < CoverageSamplesPerAxis; sampleY++)
                {
                    for (var sampleX = 0; sampleX < CoverageSamplesPerAxis; sampleX++)
                    {
                        if (PointInStrokePolygon(
                            points,
                            half,
                            SampleWorld(x, y, sampleX, sampleY))) covered++;
                    }
                }
                return covered / (float)CoverageSampleCount;
            }

            private static bool PointInStrokePolygon(Vector2[] points, float half, Vector2 point)
            {
                for (var index = 0; index < points.Length; index++)
                {
                    var from = points[index];
                    var to = points[(index + 1) % points.Length];
                    if (PointInButtSegment(point, from, to, half)) return true;
                }

                for (var index = 0; index < points.Length; index++)
                {
                    var previous = points[(index + points.Length - 1) % points.Length];
                    var current = points[index];
                    var next = points[(index + 1) % points.Length];
                    if (PointInMiterJoin(point, previous, current, next, half)) return true;
                }
                return false;
            }

            private static bool PointInButtSegment(Vector2 point, Vector2 from, Vector2 to, float half)
            {
                var segment = to - from;
                var lengthSquared = segment.sqrMagnitude;
                if (lengthSquared <= 0.0001f)
                    return (point - from).sqrMagnitude <= half * half;
                var projection = Vector2.Dot(point - from, segment) / lengthSquared;
                if (projection < 0f || projection > 1f) return false;
                var closest = from + segment * projection;
                return (point - closest).sqrMagnitude <= half * half;
            }

            private static bool PointInMiterJoin(
                Vector2 point,
                Vector2 previous,
                Vector2 current,
                Vector2 next,
                float half)
            {
                var incoming = current - previous;
                var outgoing = next - current;
                if (incoming.sqrMagnitude <= 0.0001f || outgoing.sqrMagnitude <= 0.0001f) return false;
                incoming.Normalize();
                outgoing.Normalize();
                var turn = Cross(incoming, outgoing);
                if (Mathf.Abs(turn) <= 0.0001f) return false;

                var incomingNormal = new Vector2(-incoming.y, incoming.x);
                var outgoingNormal = new Vector2(-outgoing.y, outgoing.x);
                if (turn > 0f)
                {
                    incomingNormal = -incomingNormal;
                    outgoingNormal = -outgoingNormal;
                }
                var offsetIncoming = current + incomingNormal * half;
                var offsetOutgoing = current + outgoingNormal * half;
                if (!LineIntersection(
                    offsetIncoming,
                    incoming,
                    offsetOutgoing,
                    outgoing,
                    out var miter)) return false;

                if ((miter - current).magnitude > half * CanvasMiterLimit)
                    return PointInTriangle(point, current, offsetIncoming, offsetOutgoing);
                return PointInTriangle(point, offsetIncoming, miter, offsetOutgoing);
            }

            private static bool LineIntersection(
                Vector2 pointA,
                Vector2 directionA,
                Vector2 pointB,
                Vector2 directionB,
                out Vector2 intersection)
            {
                var denominator = Cross(directionA, directionB);
                if (Mathf.Abs(denominator) <= 0.0001f)
                {
                    intersection = default;
                    return false;
                }
                var t = Cross(pointB - pointA, directionB) / denominator;
                intersection = pointA + directionA * t;
                return true;
            }

            private static bool PointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
            {
                var ab = Cross(b - a, point - a);
                var bc = Cross(c - b, point - b);
                var ca = Cross(a - c, point - c);
                var hasNegative = ab < -0.0001f || bc < -0.0001f || ca < -0.0001f;
                var hasPositive = ab > 0.0001f || bc > 0.0001f || ca > 0.0001f;
                return !(hasNegative && hasPositive);
            }

            private static float Cross(Vector2 left, Vector2 right)
            {
                return left.x * right.y - left.y * right.x;
            }

            private float LineCoverage(Vector2 from, Vector2 to, float half, int x, int y, bool roundCaps)
            {
                var segment = to - from;
                var lengthSquared = segment.sqrMagnitude;
                var covered = 0;
                for (var sampleY = 0; sampleY < CoverageSamplesPerAxis; sampleY++)
                {
                    for (var sampleX = 0; sampleX < CoverageSamplesPerAxis; sampleX++)
                    {
                        var point = SampleWorld(x, y, sampleX, sampleY);
                        var projection = lengthSquared > 0.0001f
                            ? Vector2.Dot(point - from, segment) / lengthSquared
                            : 0f;
                        if (!roundCaps && (projection < 0f || projection > 1f)) continue;
                        var t = roundCaps ? Mathf.Clamp01(projection) : projection;
                        var closest = from + segment * t;
                        if ((point - closest).sqrMagnitude <= half * half) covered++;
                    }
                }
                return covered / (float)CoverageSampleCount;
            }

            private void BlendCoverage(int x, int y, Color color, float coverage)
            {
                if (coverage <= 0f || color.a <= 0f) return;
                if (_clipPolygon != null)
                    coverage *= PolygonCoverage(_clipPolygon, x, y);
                if (coverage <= 0f) return;
                color.a *= Mathf.Clamp01(coverage);
                Blend(x, y, color);
            }

            private static void AccumulateSample(
                Color color,
                ref float alpha,
                ref float red,
                ref float green,
                ref float blue)
            {
                var sampleAlpha = Mathf.Clamp01(color.a) / CoverageSampleCount;
                alpha += sampleAlpha;
                red += color.r * sampleAlpha;
                green += color.g * sampleAlpha;
                blue += color.b * sampleAlpha;
            }

            private void BlendAccumulated(
                int x,
                int y,
                float alpha,
                float red,
                float green,
                float blue)
            {
                if (alpha <= 0.0001f) return;
                Blend(x, y, new Color(red / alpha, green / alpha, blue / alpha, alpha));
            }

            private void Blend(int x, int y, Color color)
            {
                if (x < 0 || x >= _size || y < 0 || y >= _size || color.a <= 0) return;
                var index = y * _size + x;
                var destination = _pixels[index];
                var sourceAlpha = Mathf.Clamp01(color.a);
                var destinationAlpha = destination.a / 255f;
                var outputAlpha = sourceAlpha + destinationAlpha * (1f - sourceAlpha);
                if (outputAlpha <= 0.0001f)
                {
                    _pixels[index] = new Color32(255, 255, 255, 0);
                    return;
                }
                var destinationColor = new Color(destination.r / 255f, destination.g / 255f, destination.b / 255f, destinationAlpha);
                var output = new Color(
                    (color.r * sourceAlpha + destinationColor.r * destinationAlpha * (1f - sourceAlpha)) / outputAlpha,
                    (color.g * sourceAlpha + destinationColor.g * destinationAlpha * (1f - sourceAlpha)) / outputAlpha,
                    (color.b * sourceAlpha + destinationColor.b * destinationAlpha * (1f - sourceAlpha)) / outputAlpha,
                    outputAlpha);
                _pixels[index] = output;
            }

            private static bool Contains(Vector2[] polygon, Vector2 point)
            {
                var inside = false;
                for (var index = 0; index < polygon.Length; index++)
                {
                    var previous = index == 0 ? polygon.Length - 1 : index - 1;
                    var a = polygon[index];
                    var b = polygon[previous];
                    if ((a.y > point.y) == (b.y > point.y)) continue;
                    var denominator = b.y - a.y;
                    if (Mathf.Abs(denominator) < 0.00001f) continue;
                    var crossing = (b.x - a.x) * (point.y - a.y) / denominator + a.x;
                    if (point.x < crossing) inside = !inside;
                }
                return inside;
            }
        }
    }
}
