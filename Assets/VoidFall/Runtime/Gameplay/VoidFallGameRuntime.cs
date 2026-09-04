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
    /// <summary>
    /// First playable Unity vertical slice. One manager owns the simulation;
    /// renderer views are pooled and contain no per-entity MonoBehaviours.
    /// The React game remains the behavior authority while this slice expands.
    /// </summary>
    [DefaultExecutionOrder(-800)]
    public sealed partial class VoidFallGameRuntime : MonoBehaviour
    {
        private UIManager _ui;
        private const uint FixtureRunSeed = 0x5f1dc0deu;
        [SerializeField] private uint runSeedOverride;
        private static uint _runSeedCounter;
        private uint _diagnosticRunSeedOverride;
        private StressScenarioDefinition _stressScenario;
        private float _stressTopUpTimer;

        private const int MaxEnemies = DirectorRules.MaxActiveEnemies;
        private const int MaxBullets = SimulationRules.MaxBullets;
        private const int MaxHostileShots = SimulationRules.MaxHostileShots;
        private const int MaxPickups = SimulationRules.MaxPickups;
        // The browser permits one XP gem beyond the normal cap when a full
        // pickup set contains no XP gem that can absorb the drop.
        private const int MaxPickupSlots = MaxPickups + 1;
        private const int MaxMeteors = MeteorRules.MaxOrdinaryMeteors + MeteorRules.MaxExplosiveMeteors;
        private const int MaxMeteorShards = MaxMeteors * 6;
        private const int MaxBosses = 3;
        private const int MaxBladeViews = 5;
        private const int MaxArcEffects = 12;
        private const int MaxRailTrails = 4;
        private const int RailTrailSegmentCount = 7;
        private const int MaxArenaMotes = 80;
        private const int MaxArenaStars = 42;
        private const int MaxArenaFilamentSlots = 8;
        private const int BossBeamMaxSegments = 16;
        private const int ArenaNearFilamentPasses = 11;
        private const int ArenaNearStrandPasses = 9;
        private const int ArenaFilamentMaskMaxDimension = 256;
        private const int ArenaFilamentMaskMinDimension = 64;
        private const int ArenaFilamentPlateCount = 3;
        private const int MaxArenaRocks = 10;
        private const int MaxArenaLandmarkSegments = 11;
        private const int MaxArenaStellarRimSegments = 48;
        private const int MaxArenaRingDebris = 11;
        private const int ArenaRingSlabSteps = 6;
        private const int ArenaRingSlabVertexCount = (ArenaRingSlabSteps + 1) * 2;
        private const int MaxArenaOrbitViews = 21;
        private const int MaxArenaOrbitFractures = 5;
        private const int MaxImpactMarks = 14;
        private const int ImpactHeatSegmentCount = 5;
        private const int MortarTelegraphSegmentCount = 6;
        private const int ExploderTelegraphSegmentCount = 6;
        private const float SiegeMortarDashOnLength = 7f;
        private const float SiegeMortarDashOffLength = 6f;
        // Browser ringWave() has no separate wave cap; rings consume the same
        // 280-entry cosmetic budget as particles. Keep the custom pool at that
        // source ceiling so it cannot replace a live ring at an arbitrary 32.
        private const int MaxRingWaves = 280;
        private const int MaxBlastWaves = 4;
        private const int MaxFloaters = 42;
        private const int MaxDeathGhosts = 80;
        private const int MaxDamageIndicators = 5;
        private const int MaxSourceParticles = 280;
        private const int MaxToasts = 3;
        private const float ToastFadeSeconds = 0.16f;
        private const float ToastIntroEnd = 0.18f;
        private const float ToastSettleEnd = 0.28f;
        private const float ToastOutroStart = 0.82f;
        private const float ToastStackTopPercent = 0.18f;
        private const float ToastStackTopInset = 82f;
        private const float ToastStackRowSpacing = 42f;
        private const float OverlayFadeSeconds = 0.30f;
        private const float OverlayCardRiseSeconds = 0.45f;
        private const float OverlayCardRiseOffset = 18f;
        private static readonly EliteVariantId[] StressEliteVariantOrder =
        {
            EliteVariantId.Exploder,
            EliteVariantId.Mortar,
            EliteVariantId.Gunner,
        };
        private static readonly string[] StressRosterTwoTypes =
        {
            "chaser",
            "gunner",
            "exploder",
            "guard",
        };
        // Pre-allocated spread/side arrays used in hot combat loops to avoid
        // per-frame heap allocations (audit #19).
        internal static readonly float[] GunnerRosterTwoSpreads = { -0.23f, 0f, 0.2f };
        internal static readonly float[] TwinGunnerSides = { -1f, 1f };
        private static readonly float[] BossPressureSpreads = { -0.58f, -0.38f, -0.2f, 0.2f, 0.38f, 0.58f };
        // Source burst calls carry a final size argument. Every current
        // source-mapped call site passes that value explicitly; keep tuple
        // lookup only as a safe fallback for internal/default calls.
        private const float DefaultBurstParticleSize = 0.8f;
        private const float MeteorShardDrag = 3.2f;
        private const float SourceFloatingTextScale = 1.15f;
        // Browser canvas Y grows downward; Unity world/UI Y grows upward. The
        // source floatText(y - 8) therefore maps to a +8 world-space offset.
        private const float SourceFloatingTextAnchorOffset = 8f;
        internal const float PlayerRadius = 15f;
        internal static float AttackPlayerRadius =>
            _instance != null && _instance.HasWildCard(WildCardId.ColossusArsenal)
                ? 14f * (float)WildCardRules.ColossusHitboxMultiplier
                : 14f;
        private const float WorldHalfWidth = 640f;
        private const float WorldHalfHeight = 360f;

        // Approach fan. See ApproachBias. Full bias beyond FullDistance, tapering
        // to none by CommitDistance so the kill is still a straight line.
        private const float ApproachBiasMaxRadians = 0.55f;
        private const float ApproachBiasCommitDistance = 165f;
        private const float ApproachBiasFullDistance = 520f;

        // Boss commitment. Inside StartDistance nothing changes; past it the
        // pursuit floor and the ambient penalty both ramp to full by
        // FullDistance. FloorSpeed sits above the 235 player base so a fleeing
        // player loses ground except while overdrive is up.
        private const float BossPursuitStartDistance = 620f;
        private const float BossPursuitFullDistance = 1500f;
        private const float BossPursuitFloorSpeed = 300f;
        private const float BossEngagementDistance = 900f;
        private const float BossEngagedIntensity = 0.55f;
        private const float BossAbandonedIntensity = 1.25f;
        private const float ArenaSkyParallax = 0.05f;
        private const float ArenaNearParallax = 0.14f;
        private const float ArenaNearOverscan = 1.34f;
        private const float ArenaOrbitalParallax = 0.26f;
        private const float ArenaSkyOverscan = 1.18f;
        private const float ArenaOrbitalOverscan = 1.4f;
        private const float ArenaDecorField = 2600f;
        private const float ArenaGridSpacing = 96f;
        private const float QualityGameplayWarmupSeconds = 2f;
        private const float BrowserResultCardMaxWidth = 390f;
        private const float BrowserResultCardViewportInset = 36f;
        private const float BrowserResultCardMaxHeight = 720f;
        private static readonly int[] QuadTriangles = { 0, 1, 2, 0, 2, 3 };
        private static readonly int[] TriangleIndices = { 0, 1, 2 };
        private static readonly string[] WorkshopOrder =
        {
            "integrity",
            "power",
            "mobility",
            "recovery",
            "magnet",
            "precision",
            "arsenal",
            "protocol",
        };

        private sealed class TelegraphQuadBuffer
        {
            public readonly Vector3[] Vertices = new Vector3[4];
            public readonly Color[] Colors = new Color[4];
        }

        private struct ArcEffectState
        {
            public bool Active;
            public Vector2[] Points;
            public float Life;
            public float MaxLife;
            public int Sequence;
            public int View;
        }

        private struct RailTrailState
        {
            public bool Active;
            public Vector2 Start;
            public Vector2 End;
            public float Life;
            public float DamageLife;
            public float Tick;
            public float Damage;
            public int WeaponIndex;
            public int Sequence;
            public int View;
        }

        private struct ImpactMarkState
        {
            public bool Active;
            public Vector2 Position;
            public float Radius;
            public float Rotation;
            public float Age;
            public float Life;
            public int View;
        }

        private struct BlastWaveState
        {
            public bool Active;
            public Vector2 Position;
            public float MaxRadius;
            public float Age;
            public float Life;
            public bool Bomb;
            public int View;
        }

        private struct FloaterState
        {
            public bool Active;
            public Vector2 Position;
            public float Life;
            public float MaxLife;
            public int TargetKey;
            public int Value;
            public bool Critical;
            public string Text;
            public Color Color;
            public int FontSize;
            public int View;
        }

        private struct DeathGhostState
        {
            public bool Active;
            public Vector2 Position;
            public float Radius;
            public float VisualSize;
            public float Rotation;
            public float Life;
            public float MaxLife;
            public string Id;
            public Color Accent;
            public bool Elite;
            public EliteVariantId? EliteKind;
            public int View;
        }

        private struct DamageIndicatorState
        {
            public bool Active;
            public float Angle;
            public float Life;
            public float MaxLife;
            public int View;
        }

        private enum ToastKind
        {
            Info,
            Danger,
            Reward,
        }

        private struct ToastState
        {
            public bool Active;
            public string Text;
            public string Detail;
            public float Remaining;
            public float Duration;
            public ToastKind Kind;
            // Pre-formatted at enqueue so per-frame views never concatenate.
            public string Formatted;
        }

        private enum MenuPage
        {
            None,
            Home,
            Main,
            Workshop,
            Records,
            Settings,
        }

        private static VoidFallGameRuntime _instance;
        private bool _ownsGlobalResources;
        private readonly FixedStepClock _clock = new FixedStepClock();
        // Browser particles, ring waves, and meteor shards share one compact
        // forward-drawn array. Keep that logical order independent of Unity's
        // reusable view slots so overlap and replacement cannot reorder FX.
        private readonly ImpactMarkState[] _impactMarks = new ImpactMarkState[MaxImpactMarks];
        private readonly BlastWaveState[] _blastWaves = new BlastWaveState[MaxBlastWaves];
        private readonly FloaterState[] _floaters = new FloaterState[MaxFloaters];
        private readonly DeathGhostState[] _deathGhosts = new DeathGhostState[MaxDeathGhosts];
        private readonly DamageIndicatorState[] _damageIndicators = new DamageIndicatorState[MaxDamageIndicators];
        // Impact marks are a bounded browser array. Its expiry path uses
        // stable splice removal, so a fixed Unity view slot must not determine
        // the overlap order after a middle mark expires.
        private readonly int[] _impactMarkOrder = new int[MaxImpactMarks];
        private readonly int[] _impactMarkOrderPosition = new int[MaxImpactMarks];
        private int _impactMarkOrderCount;
        // Blast waves and death ghosts use the browser's swap-pop expiry path.
        // Keep that logical array order separate from their reusable Unity
        // view slots so overlapping effects retain source draw order.
        // Cosmetic-FX simulation state lives in FxSim; see the class comment.
        private readonly FxSim _fxSim = new FxSim(MaxSourceParticles, MaxMeteorShards, MaxRingWaves, FixtureRunSeed ^ 0xa5a5a5a5u);
        private readonly SlotOrder _blastWaveOrder = new SlotOrder(MaxBlastWaves);
        private readonly SlotOrder _deathGhostOrder = new SlotOrder(MaxDeathGhosts);
        private readonly SlotOrder _floaterOrder = new SlotOrder(MaxFloaters);
        private readonly SlotOrder _damageIndicatorOrder = new SlotOrder(MaxDamageIndicators);
        private int _floaterSiblingBase;
        private int _damageIndicatorSiblingBase;
        // The browser keeps bosses in an append-only array until their defeat
        // fade completes. Fixed Unity slots need a separate logical order so a
        // later boss cannot move ahead of an older surviving boss after slot
        // reuse.
        private readonly SpriteRenderer[] _enemyViews = new SpriteRenderer[MaxEnemies];
        private readonly SpriteRenderer[] _enemyHarvesterFullViews = new SpriteRenderer[MaxEnemies];
        private readonly SpriteRenderer[] _enemyExploderWarningViews = new SpriteRenderer[MaxEnemies];
        private readonly SpriteRenderer[] _eliteMarkViews = new SpriteRenderer[MaxEnemies];
        private readonly LineRenderer[] _eliteChargeLaneViews = new LineRenderer[MaxEnemies];
        private readonly LineRenderer[] _eliteChargeArrowViews = new LineRenderer[MaxEnemies];
        private readonly MeshFilter[] _eliteChargeFillViews = new MeshFilter[MaxEnemies];
        private readonly MeshRenderer[] _eliteChargeFillRenderers = new MeshRenderer[MaxEnemies];
        private readonly MeshFilter[] _eliteChargeArrowFillViews = new MeshFilter[MaxEnemies];
        private readonly MeshRenderer[] _eliteChargeArrowFillRenderers = new MeshRenderer[MaxEnemies];
        private readonly TelegraphQuadBuffer[] _eliteChargeFillBuffers = new TelegraphQuadBuffer[MaxEnemies];
        private readonly TelegraphQuadBuffer[] _eliteChargeArrowFillBuffers = new TelegraphQuadBuffer[MaxEnemies];
        private readonly LineRenderer[] _enemyTelegraphRingViews = new LineRenderer[MaxEnemies];
        private readonly LineRenderer[] _enemyTelegraphLineViews = new LineRenderer[MaxEnemies];
        private readonly LineRenderer[] _enemyTelegraphSecondaryLineViews = new LineRenderer[MaxEnemies];
        private readonly LineRenderer[] _enemyTelegraphTertiaryLineViews = new LineRenderer[MaxEnemies];
        private readonly LineRenderer[] _enemyHarvesterCapacityRingViews = new LineRenderer[MaxEnemies];
        private readonly MeshFilter[] _enemyTelegraphSiegeDashViews = new MeshFilter[MaxEnemies];
        private readonly MeshRenderer[] _enemyTelegraphSiegeDashRenderers = new MeshRenderer[MaxEnemies];
        private readonly List<Vector3>[] _enemyTelegraphSiegeDashVertices = new List<Vector3>[MaxEnemies];
        private readonly List<int>[] _enemyTelegraphSiegeDashTriangles = new List<int>[MaxEnemies];
        private readonly List<Color>[] _enemyTelegraphSiegeDashColors = new List<Color>[MaxEnemies];
        private readonly SpriteRenderer[] _enemyTelegraphExploderFillViews = new SpriteRenderer[MaxEnemies];
        private readonly LineRenderer[] _enemyTelegraphExploderSegmentViews =
            new LineRenderer[MaxEnemies * ExploderTelegraphSegmentCount];
        private readonly SpriteRenderer[] _enemyTelegraphMortarFillViews = new SpriteRenderer[MaxEnemies];
        private readonly LineRenderer[] _enemyTelegraphMortarSegmentViews =
            new LineRenderer[MaxEnemies * MortarTelegraphSegmentCount];
        private readonly MeshFilter[] _enemyTelegraphFillViews = new MeshFilter[MaxEnemies];
        private readonly MeshRenderer[] _enemyTelegraphFillRenderers = new MeshRenderer[MaxEnemies];
        private readonly MeshFilter[] _enemyTelegraphArrowFillViews = new MeshFilter[MaxEnemies];
        private readonly MeshRenderer[] _enemyTelegraphArrowFillRenderers = new MeshRenderer[MaxEnemies];
        private readonly TelegraphQuadBuffer[] _enemyTelegraphFillBuffers = new TelegraphQuadBuffer[MaxEnemies];
        private readonly TelegraphQuadBuffer[] _enemyTelegraphArrowFillBuffers = new TelegraphQuadBuffer[MaxEnemies];
        private readonly LineRenderer[] _enemyHealthArcViews = new LineRenderer[MaxEnemies];
        private readonly LineRenderer[] _enemyShieldArcViews = new LineRenderer[MaxEnemies];
        private readonly SpriteRenderer[] _enemyHealthBackgroundViews = new SpriteRenderer[MaxEnemies];
        private readonly SpriteRenderer[] _enemyHealthFillViews = new SpriteRenderer[MaxEnemies];
        private readonly SpriteRenderer[] _bulletViews = new SpriteRenderer[MaxBullets];
        private readonly SpriteRenderer[] _bulletContrastViews = new SpriteRenderer[MaxBullets];
        private readonly SpriteRenderer[] _railAfterimageFarViews = new SpriteRenderer[MaxBullets];
        private readonly SpriteRenderer[] _railAfterimageNearViews = new SpriteRenderer[MaxBullets];
        private readonly SpriteRenderer[] _hostileShotViews = new SpriteRenderer[MaxHostileShots];
        private readonly SpriteRenderer[] _meteorViews = new SpriteRenderer[MaxMeteors];
        private readonly SpriteRenderer[] _meteorHitViews = new SpriteRenderer[MaxMeteors];
        private readonly SpriteRenderer[] _meteorCoreViews = new SpriteRenderer[MaxMeteors];
        private readonly SpriteRenderer[] _meteorShardViews = new SpriteRenderer[MaxMeteorShards];
        private readonly SpriteRenderer[] _sourceParticleViews = new SpriteRenderer[MaxSourceParticles];
        private readonly SpriteRenderer[] _impactMarkViews = new SpriteRenderer[MaxImpactMarks];
        private readonly LineRenderer[] _meteorDangerArcViews = new LineRenderer[MaxMeteors];
        private readonly LineRenderer[] _meteorDangerRingViews = new LineRenderer[MaxMeteors];
        private readonly LineRenderer[] _meteorHealthArcViews = new LineRenderer[MaxMeteors];
        private readonly LineRenderer[] _impactHeatViews = new LineRenderer[MaxImpactMarks * ImpactHeatSegmentCount];
        private readonly LineRenderer[] _ringWaveViews = new LineRenderer[MaxRingWaves];
        private readonly LineRenderer[] _ringWaveGlowViews = new LineRenderer[MaxRingWaves];
        private readonly SpriteRenderer[] _ringWaveSpriteViews = new SpriteRenderer[MaxRingWaves];
        private readonly SpriteRenderer[] _blastWaveFillViews = new SpriteRenderer[MaxBlastWaves];
        private readonly LineRenderer[] _blastWaveRimViews = new LineRenderer[MaxBlastWaves];
        private readonly LineRenderer[] _blastWaveArcViews = new LineRenderer[MaxBlastWaves];
        private static Material _blastWaveScreenMaterial;
        private static Material _additiveSpriteMaterial;
        private static Material _defaultSpriteMaterial;
        private Material _fxMaterial;
        private readonly List<Mesh> _dynamicMeshes = new List<Mesh>();
        private readonly List<Material> _dynamicMaterials = new List<Material>();
        private readonly Text[] _floaterViews = new Text[MaxFloaters];
        private readonly Image[] _damageIndicatorViews = new Image[MaxDamageIndicators];
        private readonly SpriteRenderer[] _deathGhostViews = new SpriteRenderer[MaxDeathGhosts];
        private readonly SpriteRenderer[] _pickupViews = new SpriteRenderer[MaxPickupSlots];
        private readonly SpriteRenderer[] _bossViews = new SpriteRenderer[MaxBosses];
        private readonly MeshFilter[] _bossTelegraphFillViews = new MeshFilter[MaxBosses];
        private readonly MeshRenderer[] _bossTelegraphFillRenderers = new MeshRenderer[MaxBosses];
        private readonly LineRenderer[] _bossTelegraphOutlineViews = new LineRenderer[MaxBosses];
        private readonly SpriteRenderer[] _bossShieldFillViews = new SpriteRenderer[MaxBosses];
        private readonly List<Vector3>[] _bossTelegraphVertices = new[]
        {
            new List<Vector3>(96),
            new List<Vector3>(96),
            new List<Vector3>(96),
        };
        private readonly List<int>[] _bossTelegraphTriangles = new[]
        {
            new List<int>(144),
            new List<int>(144),
            new List<int>(144),
        };
        private readonly List<Color>[] _bossTelegraphColors = new[]
        {
            new List<Color>(96),
            new List<Color>(96),
            new List<Color>(96),
        };
        private readonly SpriteRenderer[] _bladeViews = new SpriteRenderer[MaxBladeViews];
        private readonly LineRenderer[] _arcViews = new LineRenderer[MaxArcEffects];
        private readonly LineRenderer[] _arcCoreViews = new LineRenderer[MaxArcEffects];
        private readonly ArcEffectState[] _arcEffects = new ArcEffectState[MaxArcEffects];
        private readonly MeshRenderer[] _railTrailViews = new MeshRenderer[MaxRailTrails];
        private readonly MeshFilter[] _railTrailMeshViews = new MeshFilter[MaxRailTrails];
        private readonly Vector3[][] _railTrailVertices = new Vector3[MaxRailTrails][];
        private readonly Color[][] _railTrailColors = new Color[MaxRailTrails][];
        private readonly int[][] _railTrailTriangles = new int[MaxRailTrails][];
        private readonly RailTrailState[] _railTrails = new RailTrailState[MaxRailTrails];
        private readonly SpriteRenderer[] _arenaMoteViews = new SpriteRenderer[MaxArenaMotes];
        private readonly Vector4[] _arenaMoteSeeds = new Vector4[MaxArenaMotes];
        private readonly float[] _arenaMoteSizes = new float[MaxArenaMotes];
        private readonly float[] _arenaMoteSpins = new float[MaxArenaMotes];
        private readonly float[] _arenaMoteRates = new float[MaxArenaMotes];
        private readonly float[] _arenaMoteParallax = new float[MaxArenaMotes];
        private readonly int[] _arenaMoteDepths = new int[MaxArenaMotes];
        private int _arenaMoteSeedCount;
        private int _arenaMoteSeedDetail = -1;
        private ArenaId _arenaMoteSeedArena;
        private bool _arenaMoteSeedReducedMotion;
        private bool _arenaMoteSeedsReady;
        private readonly SpriteRenderer[] _arenaStarViews = new SpriteRenderer[MaxArenaStars];
        private SpriteRenderer _arenaCurrentGlowView;
        private readonly SpriteRenderer[] _arenaRockViews = new SpriteRenderer[MaxArenaRocks];
        private readonly SpriteRenderer[] _arenaRockPlaneViews = new SpriteRenderer[MaxArenaRocks];
        private readonly Vector4[] _arenaRockSeeds = new Vector4[MaxArenaRocks];
        private readonly float[] _arenaRockSpins = new float[MaxArenaRocks];
        private readonly int[] _arenaRockShapes = new int[MaxArenaRocks];
        private readonly float[] _arenaRockTones = new float[MaxArenaRocks];
        private int _arenaRockFarCount;
        private int _arenaRockTotalCount;
        private int _arenaRockSeedDetail = -1;
        private ArenaId _arenaRockSeedArena;
        private bool _arenaRockSeedReducedMotion;
        private bool _arenaRockSeedsReady;
        private readonly MeshFilter[] _arenaNearFilamentOuterViews = new MeshFilter[MaxArenaFilamentSlots];
        private readonly MeshRenderer[] _arenaNearFilamentOuterRenderers = new MeshRenderer[MaxArenaFilamentSlots];
        private readonly LineRenderer[] _arenaNearFilamentInnerViews = new LineRenderer[MaxArenaFilamentSlots];
        private readonly MeshFilter[] _arenaNearFilamentStrandViews = new MeshFilter[MaxArenaFilamentSlots];
        private readonly MeshRenderer[] _arenaNearFilamentStrandRenderers = new MeshRenderer[MaxArenaFilamentSlots];
        private readonly Vector2[][] _arenaNearFilamentPoints = new Vector2[MaxArenaFilamentSlots][];
        private readonly float[][] _arenaNearFilamentPointWidths = new float[MaxArenaFilamentSlots][];
        private readonly Vector3[][] _arenaNearFilamentBandVertices = new Vector3[MaxArenaFilamentSlots][];
        private readonly Color[][] _arenaNearFilamentBandColors = new Color[MaxArenaFilamentSlots][];
        private readonly Vector3[][] _arenaNearFilamentStrandVertices = new Vector3[MaxArenaFilamentSlots][];
        private readonly Color[][] _arenaNearFilamentStrandColors = new Color[MaxArenaFilamentSlots][];
        private readonly Vector4[][] _arenaNearFilamentNotches = new Vector4[MaxArenaFilamentSlots][];
        private readonly float[][] _arenaNearFilamentNotchHeights = new float[MaxArenaFilamentSlots][];
        private readonly Texture2D[] _arenaNearFilamentNotchMasks = new Texture2D[MaxArenaFilamentSlots];
        private readonly Texture2D[] _arenaFilamentGroupNotchMasks = new Texture2D[ArenaFilamentPlateCount];
        private readonly SpriteRenderer[] _arenaFilamentPlateViews = new SpriteRenderer[ArenaFilamentPlateCount];
        private readonly Sprite[] _arenaFilamentPlateSprites = new Sprite[ArenaFilamentPlateCount];
        private readonly Texture2D[] _arenaFilamentPlateTextures = new Texture2D[ArenaFilamentPlateCount];
        private readonly Material[] _arenaNearFilamentMaterials = new Material[MaxArenaFilamentSlots];
        private readonly float[] _arenaNearFilamentPointSpacings = new float[MaxArenaFilamentSlots];
        private readonly int[] _arenaNearFilamentStrandFrom = new int[MaxArenaFilamentSlots];
        private readonly int[] _arenaNearFilamentStrandTo = new int[MaxArenaFilamentSlots];
        private readonly float[] _arenaNearFilamentStrandShifts = new float[MaxArenaFilamentSlots];
        private readonly float[] _arenaNearFilamentWidths = new float[MaxArenaFilamentSlots];
        private readonly Color[] _arenaNearFilamentColors = new Color[MaxArenaFilamentSlots];
        private readonly Color[] _arenaNearFilamentCoreColors = new Color[MaxArenaFilamentSlots];
        private readonly float[] _arenaNearFilamentAlphas = new float[MaxArenaFilamentSlots];
        private float _arenaFilamentViewportWidth = float.NaN;
        private float _arenaFilamentViewportHeight = float.NaN;
        private int _arenaFarFilamentCount;
        private ArenaId _arenaFarFilamentSeedArena;
        private bool _arenaFarFilamentSeedsReady;
        private readonly Sprite[] _arenaPlateSprites = new Sprite[ContentOrder.PreparedArenas.Length];
        private readonly Sprite[] _arenaPlateDetailSprites = new Sprite[ContentOrder.PreparedArenas.Length];
        private readonly ArenaPlateAsset[] _preparedArenaPlateAssets = new ArenaPlateAsset[ContentOrder.PreparedArenas.Length];
        private readonly ArenaPackageKey[] _preparedArenaPlateKeys = new ArenaPackageKey[ContentOrder.PreparedArenas.Length];
        private ArenaResidencyManager _arenaResidency;
        private int _arenaPlateBakeWidth = ArenaPlateFactory.DefaultWidth;
        private int _arenaPlateBakeHeight = ArenaPlateFactory.DefaultHeight;
        private int _arenaPlateDetailBakeWidth = 2560;
        private int _arenaPlateDetailBakeHeight = 1440;
        private readonly LineRenderer[] _arenaRockRimViews = new LineRenderer[MaxArenaRocks];
        private readonly LineRenderer[] _arenaStellarRimViews = new LineRenderer[MaxArenaStellarRimSegments];
        private readonly LineRenderer[] _arenaLandmarkViews = new LineRenderer[MaxArenaLandmarkSegments];
        private readonly LineRenderer[] _arenaLandmarkRimViews = new LineRenderer[MaxArenaLandmarkSegments];
        private readonly MeshFilter[] _arenaRingSlabFillViews = new MeshFilter[MaxArenaLandmarkSegments];
        private readonly MeshRenderer[] _arenaRingSlabFillRenderers = new MeshRenderer[MaxArenaLandmarkSegments];
        private readonly Vector3[][] _arenaRingSlabVertices = new Vector3[MaxArenaLandmarkSegments][];
        private readonly SpriteRenderer[] _arenaRingDebrisViews = new SpriteRenderer[MaxArenaRingDebris];
        private readonly LineRenderer[] _arenaOrbitViews = new LineRenderer[MaxArenaOrbitViews];
        private readonly LineRenderer[] _arenaOrbitFractureViews = new LineRenderer[MaxArenaOrbitFractures];
        private readonly float[] _weaponCooldowns = new float[ContentOrder.Weapons.Length];
        private readonly double[] _weaponDamage = new double[ContentOrder.Weapons.Length];
        // The browser keeps enemies in a compact array and removes by moving
        // the last item into the removed slot. These arrays mirror that order
        // while the render/simulation storage remains pooled by slot.
        // The browser keeps meteors in a compact array and recycles by moving
        // the last meteor into the removed slot. Mirror that logical order
        // while the renderer/simulation storage remains pooled by slot.
        // Browser projectiles also use compact arrays and reverse iteration;
        // keep their logical order independent from pooled Unity slots.
        // Seeker cluster targeting is a frequent projectile-hit path. The
        // source uses a short-lived visited Set; keep the same identity list
        // in fixed storage so target selection stays allocation-free.
        private readonly int[] _clusterVisited = new int[8];
        private readonly HostileTarget[] _clusterTargets = new HostileTarget[3];
        private readonly int[] _arcVisited = new int[MaxEnemies + MaxBosses];
        private readonly CircleDefinition[] _meteorPlacementEnemyCircles =
            new CircleDefinition[MaxEnemies];
        private readonly CircleDefinition[] _meteorPlacementCircles =
            new CircleDefinition[MaxMeteors];
        private readonly CircleDefinition[] _meteorPlacementProjectedCircles =
            new CircleDefinition[MaxMeteors + 1];
        private readonly MeteorPlacementContext _meteorPlacementContext =
            new MeteorPlacementContext();
        private readonly RunTelemetryRecorder _telemetry = new RunTelemetryRecorder();
        private uint _runSeed = FixtureRunSeed;
        private Transform _worldRoot;
        private SpriteRenderer _playerView;
        private SpriteRenderer _playerAuraView;
        private SpriteRenderer _playerRingView;
        private Camera _camera;
        private Vector2 _cameraFollowPosition;
        private float _cameraTrauma;
        private float _redFlash;
        private float _cyanFlash;
        private float _amberFlash;
        private float _arenaFlash;
        private float _arenaFlashT = 1.5f;
        private const float HudFadeSeconds = 0.18f;
        // Menu motion is intentionally faster than gameplay ambient motion so
        // the live Void reads as animated behind the home UI. This clock is
        // separate from _ambientClock and never changes gameplay FX timing.
        private const float MenuVoidMotionSpeed = 18f;
        private const float MenuCyclePreviewRate = 0.2f;
        private const float MenuOrbitPhaseRate = 0.095f;
        private const float GameplayOrbitPhaseRate = 0.018f;
        private const float MenuRingPhaseRate = 0.04f;
        private const float GameplayRingPhaseRate = 0.008f;
        private float _arenaDecorClock;
        private float _ambientClock;
        private Vector2 _arenaDecorDrift;
        private ProceduralAudio _audio;
        private MusicDirector _music;
        // Development fallback only. Shipping builds hydrate the factory from
        // the prepared catalog before any view asks for a sprite.
        private IEnumerator<int> _spriteWarmSteps;

        // Wall-time slice given to the background sprite warm each frame. Small
        // enough to stay invisible at 60 Hz, large enough that the set finishes
        // within the first couple of seconds of menu idle.
        private const float SpriteWarmBudgetSeconds = 0.004f;
        private ParticleSystem _fx;
        private float _fxSimulationSpeed = 1f;
        private readonly ParticleSystem.Particle[] _fxParticleScratch =
            new ParticleSystem.Particle[MaxSourceParticles];
        private Canvas _canvas;
        private CanvasGroup _hudGroup;
        private Text _hudText;
        private Image _xpBarBackground;
        private Image _xpBarFill;
        private Image _healthPanel;
        private Image _healthBarBackground;
        private Image _healthBarGhost;
        private Image _healthBarFill;
        private Text _healthText;
        private RawImage _healthIcon;
        private Text _healthLabelText;
        private Text _healthValueText;
        private Text _timeText;
        private Text _levelText;
        private Image _clockPanel;
        private Text _metricsText;
        private Image _metricsPanel;
        private readonly RawImage[] _metricIcons = new RawImage[3];
        private readonly Text[] _metricValues = new Text[3];
        private readonly Image[] _metricDividers = new Image[2];
        private Button _pauseButton;
        private Text _pauseButtonText;
        private RawImage _pauseButtonIcon;
        private Image _boostPanel;
        private Image _boostBar;
        private RawImage _boostIcon;
        private Text _boostText;
        private Text _boostSecondsText;
        private Text _boostGhostA;
        private Text _boostGhostB;
        private Text _loadoutText;
        private Text _supportStripText;
        private Text _lateStripText;
        private readonly Image[] _weaponChipBackgrounds = new Image[6];
        private readonly Image[] _weaponChipAccentBars = new Image[6];
        private readonly RawImage[] _weaponChipIcons = new RawImage[6];
        private readonly Text[] _weaponChipNames = new Text[6];
        private readonly Text[] _weaponChipRanks = new Text[6];
        private readonly Image[] _supportChipBackgrounds = new Image[10];
        private readonly Image[] _supportChipAccentBars = new Image[10];
        private readonly RawImage[] _supportChipIcons = new RawImage[10];
        private readonly Text[] _supportChipNames = new Text[10];
        private readonly Text[] _supportChipRanks = new Text[10];
        private readonly Image[] _lateChipBackgrounds = new Image[3];
        private readonly Image[] _lateChipAccentBars = new Image[3];
        private readonly RawImage[] _lateChipIcons = new RawImage[3];
        private readonly Text[] _lateChipNames = new Text[3];
        private readonly Text[] _lateChipRanks = new Text[3];
        private float _nextLoadoutHudRefresh;
        private string _lastLoadoutHudText;
        private bool _hudLayoutInitialized;
        private bool _hudNarrow;
        private int _hudLayoutWidth;
        private int _hudLayoutHeight;
        private Rect _hudLayoutSafeArea;
        private Text _helpText;
        private Text _bossText;
        private Text _bossNameText;
        private Text _bossHealthText;
        // Change-gate cache: boss HUD text rewrites only when the visible
        // value actually changes (was: string allocs + rebuilds every frame).
        private string _bossHudName = string.Empty;
        private int _bossHudHp = -1;
        private int _bossHudCount = -1;
        private int _toastFontSize = -1;
        private Image _bossBarBackground;
        private Image _bossBarFill;
        private Text _toastText;
        private readonly ToastState[] _toastStates = new ToastState[MaxToasts];
        private readonly Text[] _toastViews = new Text[MaxToasts];
        private readonly Shadow[] _toastShadows = new Shadow[MaxToasts];
        private Image _redFlashOverlay;
        private Image _cyanFlashOverlay;
        private Image _amberFlashOverlay;
        private SpriteRenderer _arenaVignetteView;
        private MusicPerimeterGraphic _musicPerimeter;
        private Image _arenaBannerPanel;
        private Outline _arenaBannerOutline;
        private Text _arenaBannerTitle;
        private Text _arenaBannerDetail;
        private SpriteRenderer _backdropView;
        private SpriteRenderer _arenaBakedDetailView;
        private MeshFilter _arenaGridView;
        private MeshRenderer _arenaGridRenderer;
        private Mesh _arenaGridMesh;
        private float _arenaGridFirstX = float.NaN;
        private float _arenaGridFirstY = float.NaN;
        private float _arenaGridWidth = float.NaN;
        private float _arenaGridHeight = float.NaN;
        private int _arenaGridVerticalCount;
        private int _arenaGridHorizontalCount;
        private SpriteRenderer _arenaLandmarkBodyView;
        private ArenaTransitionGraphic _transitionOverlay;
        private Image _touchBaseImage;
        private Image _touchKnobImage;
        private ArenaTransitionState _arenaTransitionState;
        private float _arenaBannerRemaining;
        private ArenaId _arenaBannerIncoming;
        private ArenaId _arenaId;
        private int _arenaRecipeIndex;
        private float _healthGhostFraction = 1f;
        private OverclockState _overclock;
        private float _overclockHudPunch;
        private float _overclockVisualSurge;
        private int _lastOverclockHudStreak = -1;
        private int _lastOverclockHudSecond = -1;
        // Per-frame HUD text is only rewritten when its source value changes,
        // so dense runs do not allocate formatted strings every frame.
        private float _lastHudHealth = -1f;
        private float _lastHudMaxHealth = -1f;
        private int _lastHudSeconds = -1;
        private int _lastHudLevel = -1;
        private int _lastHudKills = -1;
        private int _lastHudParts = -1;
        private int _lastHudScore = -1;
        private float _magnetIntensity;
        private float _magnetTarget;
        private float _adrenalTimer;
        private float _playerTrailTimer;
        private float _levelUpTimer = -1f;
        private float _levelUpPromptOpenedAt = -1f;
        private Vector2 _levelUpScroll;
        private string _overlayAnimationKey;
        private float _overlayAnimationOpenedAt = -1f;
        private float _mainMenuAnimationOpenedAt = -1f;
        private float _evolutionRevealTimer;
        private string _evolutionRevealPreviousName;
        private string _evolutionRevealName;
        private string _evolutionRevealWeaponId;
        private Color _evolutionRevealAccent = new Color(0.35f, 0.9f, 1f, 1f);
        private float _timeScale = 1f;
        private float _targetTimeScale = 1f;
        private float _freezeTimer;
        private float _time;
        private float _spawnTimer;
        private float _bladeAngle;
        private float _nextBossTime;
        private float _bossRecoveryUntil;
        private float _nextEliteTime;
        private float _nextEliteVariantTime;
        private float _meteorSpawnTimer;
        private int _meteorTarget;
        // Scratch buffers for GameSim.AdvanceMeteors: fuse-expired and
    // distance-culled slots needing view hides. No allocation per step.
    private readonly int[] _meteorExpiredSlots = new int[MaxMeteors];
        private readonly int[] _fxClearedScratch = new int[MaxMeteorShards];
    private readonly int[] _meteorCulledSlots = new int[MaxMeteors];
        private DirectorEventDefinition _nextDirectorEvent;
        private bool _directorActive;
        private bool _directorWarned;
        private float _directorTimer;
        private float _directorRecoveryTimer;
        private float _directorSpawnTimer;
        private float _pressureReliefTimer;
        private int _directorSpawned;
        private int _directorIndex;
        private SpriteRenderer _hollowBladeView;
        private SpriteRenderer _hollowBladeFarView;
        private SpriteRenderer _hollowBladeNearView;
        private bool _hollowBladeActive;
        private float _hollowBladeAngle;
        private float _hollowBladeAge;
        private float _hollowBladeCooldown;
        private int _pulseBurstShots;
        private float _pulseBurstTimer;
        private float _xp;
        private int _xpNeed;
        private int _pickupStep;
        private float _pickupStepTimer;
        private int _level;
        private int _pistolRank;
        private int _calibrationRank;
        private UpgradeProgress _upgradeProgress;
        private UpgradeOptionDefinition[] _levelOptions;
        private int _rerollsRemaining;
        private bool _levelUpActive;
        private int _kills;
        private int _eliteKills;
        private int _bossKills;
        private int _partsEarned;
        // Browser authority keeps fractional totals during the run and rounds
        // only when serializing the run/telemetry summary.
        private double _damageDealt;
        private double _damageTaken;
        private int _score;
        private int _killMilestoneIndex;
        private int _scoreMilestoneIndex;
        private float _telemetrySampleTimer;
        private string _lastTelemetryPath;
        private InputReader _input;
        private int _bossCycle;
        private int _bossSequence;
        private bool _bossWarned;
        private bool _pendingDoubleBoss;
        private int _nextBossTelemetryId;
        private int _nextEnemyId;
        // Cached delegates for GameSim.AdvanceHostileShots (no per-step allocation)
    // plus the expired-slot scratch buffer for view hides.
    private Func<bool> _hostileShotVulnerableQuery;
    private Action<int, Vector2> _hostileShotImpactHandler;
    private readonly int[] _hostileShotExpiredSlots = new int[MaxHostileShots];
    // Cached hooks for GameSim.AdvanceBullets plus its scratch buffer.
    private Action<int> _bulletTrailHook;
    private Action<int, int> _bulletEnemyHitHook;
    private Action<int, int> _bulletBossHitHook;
    private Func<int, int, bool> _bulletMeteorHitHook;
    private Func<int, bool> _bulletRicochetHook;
    private readonly int[] _bulletExpiredSlots = new int[MaxBullets];
    // Cached hook for GameSim.AdvancePickups.
    private Action<int, int, bool> _pickupCollectedHook;
        private int _nextArcEffectSequence;
        private int _nextRailTrailSequence;
        private bool _paused;
        private bool _applicationInactive;
        private bool _gameOver;
        private bool _revivePending;
        private int _revivesRemaining;
        private bool _runSaved;
        private bool _lastRunSaved;
        private bool _lastRunIsBest;
        private int _lastRunRank = -1;
        private SaveStore _saveStore;
        private SaveData _saveData;
        private int _workshopIntegrity;
        private int _workshopPower;
        private int _workshopMobility;
        private int _workshopMagnet;
        private float _damageMultiplier = 1;
        private float _cooldownMultiplier = 1;
        private float _moveSpeedMultiplier = 1;
        private float _pickupRadius;
        private float _areaMultiplier = 1;
        private float _critChance = 0.05f;
        private MenuPage _menuPage;
        private Vector2 _menuScroll;
        private Vector2 _gameOverScroll;
        private bool _mainMenuBrowsing;
        private string _menuNotice;
        private float _menuNoticeTimer;
        private string _workshopPreviewId;
        private string _workshopFocusedId;
        private bool _workshopFocusVisible;
        private bool _settingsQualityMenuOpen;
        private static readonly string[] SettingsQualityOptions =
        {
            "auto",
            "low",
            "balanced",
            "high",
        };
        private string _browserSaveImportText = string.Empty;
        private bool _resetProgressArmed;
        private float _resetProgressTimer;
        private AdaptiveQualityController _qualityController;
        private float _qualityWarmupTimer = QualityGameplayWarmupSeconds;
        private QualityPreset _qualityPreset = QualityRules.Preset(QualityPresetId.High);
        private string _qualityModeApplied;
        private bool _qualityAuto;
        private int _renderResolutionWidth;
        private int _renderResolutionHeight;
        private int _renderResolutionDpi;
        private GUISkin _menuSkin;
        private bool _debugOverlay;
        private float _debugFrameEmaMs = 16f;
        private double _startupMenuReadyRealtime;
        private double _startupMenuSampleSeconds;
        private float _startupMenuWorstFrameSeconds;
        private double _startupMenuWorstFrameElapsed;
        private int _startupMenuFrameCount;
        private bool _startupMenuSkipNextFrame;
        private bool _startupMenuReportLogged;
        private GUIStyle _debugReadoutStyle;
        private GUIStyle _debugButtonStyle;
        private string _visualCapturePath;
        private int _visualCaptureFramesRemaining = -1;
        private bool _visualCaptureRun;
        private bool _visualCaptureHydraBoss;
        private string _visualCaptureHydraAttack;
        private bool _visualCaptureCourtBoss;
        private string _visualCaptureCourtHazard;
        private bool _visualCaptureWorkshop;
        private bool _visualCaptureSettings;
        private bool _visualCaptureRecords;
        private bool _visualCaptureQuit;
        private bool _visualCaptureNoGrid;
        private bool _visualCaptureIssued;
        private int _visualCaptureOverclockStreak;
        private float _visualCaptureRunSeconds;
        private bool _visualCaptureCritical;
        private string _visualCaptureArena;
        private GUIStyle _menuPanelShadowStyle;
        private static Texture2D _rusherChevronTexture;
        private GUIStyle _profilePageHeaderStyle;
        private GUIStyle _profilePageKickerStyle;
        private GUIStyle _profilePageTitleStyle;
        private GUIStyle _profilePartsBalanceStyle;
        private GUIStyle _profilePartsBalanceTextStyle;
        private GUIStyle _menuTitleStyle;
        private GUIStyle _menuSectionStyle;
        private GUIStyle _menuBodyStyle;
        private GUIStyle _menuValueStyle;
        private GUIStyle _homeTitleStyle;
        private GUIStyle _homeStartStyle;
        private GUIStyle _homeStartButtonStyle;
        private GUIStyle _homeMetricLabelStyle;
        private GUIStyle _homeMetricValueStyle;
        private GUIStyle _homeStatusStyle;
        private GUIStyle _homeStatusCompactStyle;
        private GUIStyle _homeCardTitleStyle;
        private GUIStyle _homeCardDetailStyle;
        private GUIStyle _homeCardButtonStyle;
        private GUIStyle _recordMetricBoxStyle;
        private GUIStyle _recordMetricLabelStyle;
        private GUIStyle _recordMetricValueStyle;
        private GUIStyle _recordTableWrapStyle;
        private GUIStyle _recordTableHeaderStyle;
        private GUIStyle _recordTableHeaderTextStyle;
        private GUIStyle _recordTableRowStyle;
        private GUIStyle _recordTableCellStyle;
        private GUIStyle _recordTableScoreStyle;
        private GUIStyle _resultCardStyle;
        private GUIStyle _resultKickerStyle;
        private GUIStyle _resultTitleStyle;
        private GUIStyle _resultTitleGlowStyle;
        private GUIStyle _resultDetailPanelStyle;
        private GUIStyle _resultDetailHeaderStyle;
        private GUIStyle _resultDamageLabelStyle;
        private GUIStyle _resultDamageValueStyle;
        private GUIStyle _resultBestBadgeStyle;
        private GUIStyle _resultSaveWarningStyle;
        private GUIStyle _resultBuildChipNameStyle;
        private GUIStyle _resultActionPrimaryStyle;
        private GUIStyle _resultActionSecondaryStyle;
        private GUIStyle _resultActionPrimaryLabelStyle;
        private GUIStyle _resultActionSecondaryLabelStyle;
        private GUIStyle _resultActionPrimaryIconStyle;
        private GUIStyle _settingsRowStyle;
        private GUIStyle _settingsLabelStyle;
        private GUIStyle _settingsDetailStyle;
        private GUIStyle _settingsValueStyle;
        private GUIStyle _settingsSelectStyle;
        private GUIStyle _settingsSelectValueStyle;
        private GUIStyle _settingsSelectArrowStyle;
        private GUIStyle _settingsSelectPopupStyle;
        private GUIStyle _settingsSelectOptionStyle;
        private static Texture2D _settingsToggleTrackOffTexture;
        private static Texture2D _settingsToggleTrackOnTexture;
        private static Texture2D _settingsToggleKnobOffTexture;
        private static Texture2D _settingsToggleKnobOnTexture;
        private static Texture2D _settingsSliderTrackTexture;
        private static Texture2D _settingsSliderFillTexture;
        private static Texture2D _settingsSliderThumbTexture;
        private GUIStyle _workshopRowStyle;
        private GUIStyle _workshopPreviewRowStyle;
        private GUIStyle _workshopPreviewPanelStyle;
        private GUIStyle _workshopPreviewHeaderStyle;
        private GUIStyle _workshopPreviewKickerStyle;
        private GUIStyle _workshopPreviewTitleStyle;
        private GUIStyle _workshopPreviewRankStyle;
        private GUIStyle _workshopPreviewRankActiveStyle;
        private GUIStyle _workshopPreviewRankStripStyle;
        private GUIStyle _workshopPreviewRankTextStyle;
        private GUIStyle _workshopPreviewRankActiveTextStyle;
        private GUIStyle _workshopIconFrameStyle;
        private GUIStyle _workshopNameStyle;
        private GUIStyle _workshopDetailStyle;
        private GUIStyle _workshopPipFilledStyle;
        private GUIStyle _workshopPipEmptyStyle;
        private GUIStyle _muteButtonStyle;
        private GUIStyle _evolutionRevealKickerStyle;
        private GUIStyle _evolutionRevealPreviousStyle;
        private GUIStyle _evolutionRevealTitleStyle;
        private GUIStyle _evolutionRevealTitleGlowStyle;
        private GUIStyle _levelUpKickerStyle;
        private GUIStyle _levelUpTitleStyle;
        private GUIStyle _reviveCardStyle;
        private GUIStyle _reviveKickerStyle;
        private GUIStyle _reviveTitleStyle;
        private GUIStyle _reviveTitleGlowStyle;
        private GUIStyle _revivePrimaryButtonStyle;
        private GUIStyle _reviveSecondaryButtonStyle;
        private GUIStyle _reviveButtonLabelStyle;
        private GUIStyle _upgradeCardMetaStyle;
        private GUIStyle _upgradeCardNameStyle;
        private GUIStyle _upgradeCardDescriptionStyle;
        private GUIStyle _upgradeCardIndexStyle;
        private GUIStyle _upgradeCardMobileMetaStyle;
        private GUIStyle _upgradeCardMobileNameStyle;
        private GUIStyle _upgradeCardMobileDescriptionStyle;
        private GUIStyle _rerollButtonLabelStyle;
        private GUIStyle _rerollKeyStyle;
        private GUIStyle _rerollButtonStyle;
        private GUIStyle _rerollKeycapStyle;
        private static GUIStyle _workshopPurchaseStyle;
        private readonly Dictionary<string, GUIStyle> _resultBuildChipStyleCache =
            new Dictionary<string, GUIStyle>();
        private readonly Dictionary<string, GUIStyle> _resultBuildChipRankStyleCache =
            new Dictionary<string, GUIStyle>();
        private readonly Dictionary<string, GUIStyle> _upgradeCardStyleCache =
            new Dictionary<string, GUIStyle>();
        private readonly Dictionary<string, GUIStyle> _upgradeIconStyleCache =
            new Dictionary<string, GUIStyle>();
        private readonly Dictionary<string, GUIStyle> _evolutionMarkStyleCache =
            new Dictionary<string, GUIStyle>();
        private static readonly Dictionary<string, Texture2D> _evolutionMarkRingCache =
            new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Texture2D> _evolutionMarkGlowCache =
            new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Texture2D> _evolutionCrossLineCache =
            new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Texture2D> _overlayCardShadowCache =
            new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Texture2D> _primaryActionOuterShadowCache =
            new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Texture2D> _primaryActionInsetShadowCache =
            new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Texture2D> _menuStartOuterShadowCache =
            new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Texture2D> _menuStartInsetShadowCache =
            new Dictionary<string, Texture2D>();

        private static void ClearStaticTextureCaches()
        {
            DestroyTextureCache(_evolutionMarkRingCache);
            DestroyTextureCache(_evolutionMarkGlowCache);
            DestroyTextureCache(_evolutionCrossLineCache);
            DestroyTextureCache(_overlayCardShadowCache);
            DestroyTextureCache(_primaryActionOuterShadowCache);
            DestroyTextureCache(_primaryActionInsetShadowCache);
            DestroyTextureCache(_menuStartOuterShadowCache);
            DestroyTextureCache(_menuStartInsetShadowCache);
        }

        private static void DestroyTextureCache(Dictionary<string, Texture2D> cache)
        {
            if (cache == null) return;
            foreach (var kvp in cache)
            {
                if (kvp.Value != null) Destroy(kvp.Value);
            }
            cache.Clear();
        }

        private static void CacheTextureBounded(Dictionary<string, Texture2D> cache, string key, Texture2D texture, int maxEntries = 12)
        {
            if (cache.Count >= maxEntries)
            {
                string oldestKey = null;
                foreach (var k in cache.Keys)
                {
                    oldestKey = k;
                    break;
                }
                if (oldestKey != null)
                {
                    if (cache.TryGetValue(oldestKey, out var oldTex) && oldTex != null)
                        Destroy(oldTex);
                    cache.Remove(oldestKey);
                }
            }
            cache[key] = texture;
        }

        private static Font _browserBodyFont;
        private static Font _browserDisplayFont;
        private static Texture2D _homeBackdropTexture;
        private static Texture2D _buildChipIconTexture;
        private static Texture2D _upgradeHeartIconTexture;
        private static Texture2D _rerollIconTexture;
        private static Texture2D _workshopIconTexture;
        private static Texture2D _workshopCoinsTexture;
        private static Texture2D _homeIconTexture;
        private static Texture2D _controlIconTexture;
        private static Sprite _weaponChipHudBackgroundSprite;
        private static Sprite _weaponChipHudBorderSprite;
        private static Sprite _weaponChipHudRankBackgroundSprite;

        public double ElapsedSeconds => _time;
        public int Kills => _kills;
        public int Level => _level;
        public int ActiveEnemiesCount => ActiveEnemies();
        public int ActiveBossesCount => ActiveBosses();
        public int ActiveBulletsCount => ActiveBullets();
        public int ActiveHostileShotsCount => ActiveHostileShots();
        public int ActivePickupsCount => ActivePickups();
        public int ActiveMeteorsCount => ActiveMeteors();
        public float FrameEmaMilliseconds => _debugFrameEmaMs;
        public string ActiveStressScenarioId => _stressScenario?.Id;
        public string NextDirectorEventId => _nextDirectorEvent.Id;
        public int RevivesLeft => _revivesRemaining;
        public bool RevivePending => _revivePending;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _ownsGlobalResources = true;
            _input = new InputReader();
            _arenaResidency = new ArenaResidencyManager();
            DontDestroyOnLoad(gameObject);
            _worldRoot = new GameObject("VoidFall World").transform;
            _worldRoot.SetParent(transform, false);
            ConfigurePreparedSpritesForStartup();
            SetupCamera();
            SetupBackdrop();
            SetupHud();
            SetupPlayer();
            SetupAudio();
            SetupFx();
            SetupHydraPresentation();
            _saveStore = new SaveStore();
            _saveData = _saveStore.Load();
            _gameBridge = new RuntimeGameBridge(this);
            _settingsController = new SettingsController(_gameBridge);
            ApplySettings();
            // Resolve the saved arena while the application is still composing
            // its first scene. Imported textures replace the old multi-million-
            // pixel first-render bake and are ready before the menu is shown.
            _arenaId = ArenaIdFromName(_saveData?.arena);
            SelectRecipeForCurrentArena();
            PrepareArenaNeighborhood();
            TryInstallPreparedArenaPlate(_arenaId);
            _ui = UIManager.Create(new UICallbacks
            {
                StartRun = StartRun,
                RestartRun = StartRun,
                ResumeRun = ResumeRunFromUi,
                AbortToMenu = EnterMainMenu,

                OpenWorkshop = () => OpenMenuPageFromUi(MenuPage.Workshop),
                OpenRecords = () => OpenMenuPageFromUi(MenuPage.Records),
                OpenSettings = () => OpenMenuPageFromUi(MenuPage.Settings),
                CloseMenuPage = CloseMenu,

                PrevArena = CyclePrevArenaFromUi,
                NextArena = CycleNextArenaFromUi,

                BuyWorkshop = TryBuyWorkshopFromUi,
                PreviewWorkshop = id => _workshopPreviewId = id,
                RefundWorkshop = RefundAllWorkshopFromUi,

                SetMasterVolume = v => ApplySettingFromUi(s => s.masterVolume = v, false),
                SetEffectsVolume = v => ApplySettingFromUi(s => s.effectsVolume = v, false),
                SetMusicVolume = v => ApplySettingFromUi(s => s.musicVolume = v, false),
                SetScreenShake = v => ApplySettingFromUi(s => s.shake = v, false),
                SetTouchSize = v => ApplySettingFromUi(s => s.touchSize = QuantizeTouchSize(v), false),
                SetQuality = v => ApplySettingFromUi(s => s.quality = v, true),
                SetReducedMotion = v => ApplySettingFromUi(s => s.reducedMotion = v, true),
                SetHighContrast = v => ApplySettingFromUi(s => s.highContrast = v, true),

                SetResolution = (w, h) => ApplySettingFromUi(
                    s => { s.resolutionWidth = w; s.resolutionHeight = h; }, true),
                SetDisplayMode = v => ApplySettingFromUi(s => s.fullscreenMode = v, true),
                SetBloom = v => ApplySettingFromUi(s => s.bloom = v, false),
                SetChromatic = v => ApplySettingFromUi(s => s.chromatic = v, false),

                ToggleMute = ToggleMute,
                IsMuted = () => _audio != null && _audio.Muted,

                ResetProgress = ResetLocalProgress,
                ExportSave = ExportBrowserSave,
                ExportTelemetry = () => ExportTelemetrySnapshot(_gameOver ? "gameover" : "active"),

                AcceptRevive = AcceptRevive,
                DeclineRevive = DeclineRevive,
                RerollUpgrades = RerollLevelOptions,

                QuitGame = QuitGameFromUi,
                SetQuitDialogOpen = open => _music?.SetMenuDialog(open)
            });
            AttachWorkshopFramePreview();
            ConfigureVisualCapture();
            EnterMainMenu();
            if (_visualCaptureWorkshop) _menuPage = MenuPage.Workshop;
            if (_visualCaptureSettings) _menuPage = MenuPage.Settings;
            if (_visualCaptureRecords) _menuPage = MenuPage.Records;
            if (_visualCaptureRun)
            {
                StartRunInternal(false);
                for (var stack = 0; stack < _visualCaptureOverclockStreak; stack++)
                    _overclock.ApplyPickup();
                if (_visualCaptureOverclockStreak > 0)
                {
                    _overclockHudPunch = 1f;
                    _overclockVisualSurge = 1f;
                }
                if (_visualCaptureRunSeconds > 0f) _time = _visualCaptureRunSeconds;
                if (_visualCaptureCritical) _gameSim.Player.Health = _gameSim.Player.MaxHealth * 0.15f;
                if (!string.IsNullOrEmpty(_visualCaptureArena))
                {
                    _arenaId = ArenaIdFromName(_visualCaptureArena);
                    SelectRecipeForCurrentArena();
                    TryInstallPreparedArenaPlate(_arenaId);
                }
                if (_visualCaptureHydraBoss) BeginHydraBossEncounterForCapture();
                if (_visualCaptureCourtBoss) BeginMonochromeBossEncounterForCapture();
            }
            _startupMenuReadyRealtime = Time.realtimeSinceStartupAsDouble;
            _startupMenuSkipNextFrame = true;
            Debug.Log(
                "VOIDFALL_STARTUP_READY engineSeconds=" +
                _startupMenuReadyRealtime.ToString("F3") +
                " preparedSprites=" + (_spriteWarmSteps == null) +
                " arena=" + _arenaId);
        }

        private void OnApplicationQuit()
        {
            CommitSettings();
            SaveRun();
        }

        /// <summary>
        /// Confirmed exit from the home screen's close control. Persistence is
        /// owned by OnApplicationQuit, which runs on a normal engine quit, so
        /// nothing else is needed here — the confirm dialog has already gated
        /// the intent.
        /// </summary>
        private void QuitGameFromUi()
        {
            Application.Quit();
        }

        /// <summary>
        /// Mounts the live frame preview inside the Workshop's preview column.
        /// Ranks are queried live (including the focused row's +1 preview), so
        /// hovering and buying instantly update the frame shown.
        /// </summary>
        private void AttachWorkshopFramePreview()
        {
            var stage = _ui?.Workshop?.PreviewStage;
            if (stage == null) return;
            var preview = stage.GetComponent<PlayerFramePreview>();
            if (preview == null)
            {
                preview = stage.gameObject.AddComponent<PlayerFramePreview>();
            }
            preview.Bind(
                id => PreviewWorkshopRank(id, _workshopPreviewId),
                () => _saveData?.settings?.reducedMotion == true);
        }

        private void OnDestroy()
        {
            // A duplicate runtime is destroyed by Awake before it owns any
            // shared/static resources. Only the singleton owner may perform
            // cleanup that also belongs to the surviving runtime.
            if (!_ownsGlobalResources || _instance != this) return;
            _ownsGlobalResources = false;
            DestroyVideoVolumeResources();
            _arenaResidency?.Dispose();
            _arenaResidency = null;

            for (var group = 0; group < _arenaFilamentGroupNotchMasks.Length; group++)
            {
                if (_arenaFilamentGroupNotchMasks[group] != null)
                    Destroy(_arenaFilamentGroupNotchMasks[group]);
                if (_arenaFilamentPlateSprites[group] != null)
                    Destroy(_arenaFilamentPlateSprites[group]);
                if (_arenaFilamentPlateTextures[group] != null)
                    Destroy(_arenaFilamentPlateTextures[group]);
            }
            for (var index = 0; index < _arenaNearFilamentNotchMasks.Length; index++)
            {
                if (_arenaNearFilamentNotchMasks[index] != null)
                    Destroy(_arenaNearFilamentNotchMasks[index]);
                if (index < _arenaNearFilamentMaterials.Length && _arenaNearFilamentMaterials[index] != null)
                    Destroy(_arenaNearFilamentMaterials[index]);
            }
            for (var index = 0; index < _arenaPlateSprites.Length; index++)
            {
                if (_preparedArenaPlateAssets[index] != null)
                {
                    _preparedArenaPlateAssets[index] = null;
                    _preparedArenaPlateKeys[index] = default;
                    _arenaPlateSprites[index] = null;
                    _arenaPlateDetailSprites[index] = null;
                    continue;
                }
                if (_arenaPlateSprites[index] != null)
                {
                    var texture = _arenaPlateSprites[index].texture;
                    Destroy(_arenaPlateSprites[index]);
                    if (texture != null) Destroy(texture);
                }
                if (_arenaPlateDetailSprites[index] != null)
                {
                    var detailTexture = _arenaPlateDetailSprites[index].texture;
                    Destroy(_arenaPlateDetailSprites[index]);
                    if (detailTexture != null) Destroy(detailTexture);
                }
            }
            for (var i = 0; i < _dynamicMeshes.Count; i++)
            {
                if (_dynamicMeshes[i] != null) Destroy(_dynamicMeshes[i]);
            }
            _dynamicMeshes.Clear();

            for (var i = 0; i < _dynamicMaterials.Count; i++)
            {
                if (_dynamicMaterials[i] != null) Destroy(_dynamicMaterials[i]);
            }
            _dynamicMaterials.Clear();

            if (_blastWaveScreenMaterial != null) { Destroy(_blastWaveScreenMaterial); _blastWaveScreenMaterial = null; }
            if (_defaultSpriteMaterial != null) { Destroy(_defaultSpriteMaterial); _defaultSpriteMaterial = null; }
            if (_additiveSpriteMaterial != null) { Destroy(_additiveSpriteMaterial); _additiveSpriteMaterial = null; }
            if (_fxMaterial != null) { Destroy(_fxMaterial); _fxMaterial = null; }

            ArenaPlateFactory.Cleanup();
            ClearStaticTextureCaches();

            if (_instance == this) _instance = null;
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            SetApplicationActive(!pauseStatus);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            SetApplicationActive(hasFocus);
        }

        private void SetApplicationActive(bool active)
        {
            if (!active)
            {
                if (_applicationInactive) return;
                _applicationInactive = true;
                _input?.ResetTouch();
                _audio?.Suspend();
                _music?.SetApplicationActive(false);
                if (!_paused && !_gameOver && !_revivePending && !_levelUpActive && _menuPage == MenuPage.None)
                    _paused = true;
                return;
            }

            if (!_applicationInactive) return;
            _applicationInactive = false;
            RestartQualitySession();
            _audio?.Resume();
            _music?.SetApplicationActive(true);
        }

        // Combat simulation state lives in GameSim (see class comment).
        private readonly GameSim _gameSim = new GameSim(
            MaxEnemies, MaxBullets, MaxHostileShots, MaxPickupSlots, MaxBosses, MaxMeteors, FixtureRunSeed);

        // Cosmetic-FX state lives in FxSim; these keep the historical call
        // surface while update/spawn bodies migrate there piece by piece.
        private readonly int[] _fxExpiryScratch =
            new int[Mathf.Max(MaxSourceParticles, Mathf.Max(MaxMeteorShards, MaxRingWaves))];

        private void ResetSourceFxOrder() => _fxSim.ResetSourceFxOrder();

        private void AppendSourceFxOrder(SourceFxKind kind, int slot) =>
            _fxSim.AppendSourceFxOrder(kind, slot);

        private void RemoveSourceFxOrder(SourceFxKind kind, int slot) =>
            _fxSim.RemoveSourceFxOrder(kind, slot);

        private void EnsureSourceFxOrderEntries() => _fxSim.EnsureSourceFxOrderEntries();

        private bool SourceFxEntryActive(SourceFxKind kind, int slot) =>
            _fxSim.SourceFxEntryActive(kind, slot);
        private void RestartQualitySession()
        {
            _qualityController?.BeginSession();
            _qualityWarmupTimer = QualityGameplayWarmupSeconds;
        }

        private SettingsController _settingsController;
        private IGameBridge _gameBridge;
        private WorkshopController _workshopController;
        private RecordsController _recordsController;

        private void Update()
        {
            RecordStartupMenuFrame();
            var startupUpdateStarted = Time.realtimeSinceStartupAsDouble;
            // Debounce settings disk write so slider drags don't save every pixel (audit #14).
            _settingsController?.Tick(Time.unscaledDeltaTime);

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.f3Key.wasPressedThisFrame || keyboard.backquoteKey.wasPressedThisFrame)
                    ToggleDebugOverlay();
                if (keyboard.f2Key.wasPressedThisFrame && _debugOverlay)
                    ExportTelemetrySnapshot(_gameOver ? "gameover" : "active");

                if (keyboard.tabKey.wasPressedThisFrame)
                {
                    ToggleMenu();
                }
                if (keyboard.mKey.wasPressedThisFrame)
                    ToggleMute();

                if (_revivePending)
                {
                    if (keyboard.yKey.wasPressedThisFrame) AcceptRevive();
                    else if (keyboard.nKey.wasPressedThisFrame) DeclineRevive();
                }

                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    if (_revivePending)
                    {
                        // Revive is an explicit choice and cannot be dismissed as a pause.
                    }
                    else if (_menuPage != MenuPage.None)
                    {
                        CloseMenu();
                    }
                    else if (!_gameOver && !_levelUpActive && !_rouletteActive)
                    {
                        TogglePause();
                    }
                }

                if (keyboard.pKey.wasPressedThisFrame &&
                    !_revivePending && !_gameOver && !_levelUpActive &&
                    _menuPage == MenuPage.None)
                {
                    TogglePause();
                }

                if ((keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame) &&
                    !_revivePending && !_levelUpActive && !_rouletteActive)
                {
                    if (_menuPage == MenuPage.Home)
                    {
                        StartRun();
                    }
                    else if (_gameOver)
                    {
                        StartRun();
                    }
                    else if (_paused && _menuPage == MenuPage.None)
                    {
                        TogglePause();
                    }
                }

                if (keyboard.rKey.wasPressedThisFrame && _gameOver)
                {
                    StartRun();
                }

                if (_levelUpActive)
                {
                    if (keyboard.qKey.wasPressedThisFrame) RerollLevelOptions();
                    else if (keyboard.digit1Key.wasPressedThisFrame) SelectLevelOption(0);
                    else if (keyboard.digit2Key.wasPressedThisFrame) SelectLevelOption(1);
                    else if (keyboard.digit3Key.wasPressedThisFrame) SelectLevelOption(2);
                }
            }

            if (!_paused && !_gameOver)
            {
                if (_stressScenario != null)
                    DriveStress(Time.unscaledDeltaTime);
                _clock.Consume(Time.unscaledDeltaTime, Simulate);
            }
            ApplyFxSimulationSpeed();

            var frameDt = Time.unscaledDeltaTime;
            if ((_paused || _gameOver) && !_mainMenuBrowsing)
                UpdatePhaseFx(frameDt);
            var frameMs = Mathf.Clamp(frameDt * 1000f, 0.1f, 100f);
            _ambientClock += Mathf.Clamp(frameDt, 0f, 0.1f);
            if (_mainMenuBrowsing)
                _cameraFollowPosition = MainMenuCameraPosition(_ambientClock);
            _debugFrameEmaMs = _debugFrameEmaMs * 0.96f + frameMs * 0.04f;
            _qualityWarmupTimer = Mathf.Max(0, _qualityWarmupTimer - frameDt);
            if (_qualityAuto && !_paused && !_gameOver && _qualityController != null &&
                _qualityWarmupTimer <= 0 &&
                _qualityController.Update(frameDt * 1000f, frameDt))
            {
                ApplyQualityPreset(_qualityController.CurrentPreset);
            }
            if (_camera != null &&
                (Screen.width != _renderResolutionWidth ||
                 Screen.height != _renderResolutionHeight ||
                 Mathf.RoundToInt(Mathf.Max(0, Screen.dpi) * 10f) != _renderResolutionDpi))
            {
                ApplyRenderResolution();
            }
            _cameraTrauma = Mathf.Max(0, _cameraTrauma - frameDt * 1.7f);
            _redFlash = Mathf.Max(0, _redFlash - frameDt * 2.4f);
            _cyanFlash = Mathf.Max(0, _cyanFlash - frameDt * 2.2f);
            _amberFlash = Mathf.Max(0, _amberFlash - frameDt * 3.1f);
            // Chips away at the procedural sprite warm while the menu is idle.
            // Self-terminating; a no-op once the set is complete.
            PumpSpriteWarm();

            // Reactive soundtrack. Critical health only counts while the player
            // is actually alive and in a run, so the drag does not persist into
            // the death sequence or the game-over screen.
            var playerAliveInRun = !_gameOver && !_mainMenuBrowsing && _gameSim.Player.Health > 0;
            var reducedMotion = _saveData?.settings != null && _saveData.settings.reducedMotion;
            var criticalHealth = playerAliveInRun && _gameSim.Player.MaxHealth > 0 &&
                _gameSim.Player.Health / _gameSim.Player.MaxHealth <= 0.2f;
            var magnetTime = _magnetTarget > _magnetIntensity ? 0.10f : 0.36f;
            _magnetIntensity = Mathf.Lerp(
                _magnetIntensity,
                playerAliveInRun ? _magnetTarget : 0f,
                1f - Mathf.Exp(-frameDt / magnetTime));
            _overclockVisualSurge = Mathf.MoveTowards(_overclockVisualSurge, 0f, frameDt * 1.7f);
            var musicState = new MusicReactiveState(
                playerAliveInRun ? _overclock.PowerTier : 0,
                playerAliveInRun ? _overclock.Streak : 0,
                criticalHealth,
                _levelUpActive,
                _magnetIntensity,
                playerAliveInRun);
            _music?.SetReactiveState(musicState);
            if (_musicPerimeter != null)
            {
                var analysis = _music != null ? _music.AnalysisFrame : MusicAnalysisFrame.Zero;
                _musicPerimeter.SetPresentation(
                    analysis.Bass,
                    analysis.Mids,
                    analysis.Treble,
                    playerAliveInRun ? MusicPerimeterRules.AmbientIntensity(_time) : 0f,
                    playerAliveInRun ? _overclock.PowerTier : 0,
                    playerAliveInRun ? _overclock.Streak : 0,
                    _overclockVisualSurge,
                    criticalHealth,
                    _magnetIntensity,
                    _music != null ? _music.CurrentMixTargets.VisualDamping : 1f,
                    frameDt);
            }

            var startupPhaseStarted = Time.realtimeSinceStartupAsDouble;
            UpdateArenaDecor(frameDt, reducedMotion);
            LogSlowStartupPhase("arena-decor", startupPhaseStarted);
            startupPhaseStarted = Time.realtimeSinceStartupAsDouble;
            Render();
            LogSlowStartupPhase("render", startupPhaseStarted);
            startupPhaseStarted = Time.realtimeSinceStartupAsDouble;
            UpdateHud();
            LogSlowStartupPhase("hud", startupPhaseStarted);
            startupPhaseStarted = Time.realtimeSinceStartupAsDouble;
            if (_ui != null)
            {
                if (!_mainMenuBrowsing && !_gameOver)
                {
                    _ui.HUD?.UpdateHealth(_gameSim.Player.Health, _gameSim.Player.MaxHealth);
                    _ui.HUD?.UpdateShield(0f, 0f);
                    _ui.HUD?.UpdateXP((int)_xp, _xpNeed, _level);
                    _ui.HUD?.UpdateStats(CurrentScore(), _kills, _time);
                    _ui.HUD?.SetBossWarning(_bossWarned);
                    _ui.HUD?.SetRusherWarning(_directorWarned && _nextDirectorEvent.Id == "rushers");
                }
                _ui.DebugOverlay?.UpdateDiagnostics(ActiveEnemies(), ActiveBullets(), ActivePickups());
                // The plate for a newly selected arena is baked lazily by the
                // render path, so refresh the handoff while browsing rather than
                // once on entry.
                if (_mainMenuBrowsing) PushUiBackdrop();
                // Reconcile once per frame rather than at every mutation site:
                // pause, level-up, revive and game-over are set from a dozen
                // places, and a single reconciliation cannot fall out of step.
                SyncUiScreen();
            }
            LogSlowStartupPhase("ui-reconcile", startupPhaseStarted);
            ObserveTelemetryFrame(frameDt);
            if (!_paused && !_gameOver)
            {
                _telemetrySampleTimer -= frameDt;
                if (_telemetrySampleTimer <= 0)
                {
                    do { _telemetrySampleTimer += 10f; } while (_telemetrySampleTimer <= 0);
                    RecordTelemetrySample(frameDt);
                }
            }
            _menuNoticeTimer = Mathf.Max(0, _menuNoticeTimer - frameDt);
            UpdateToastTimers(frameDt);
            _evolutionRevealTimer = Mathf.Max(0, _evolutionRevealTimer - frameDt);
            if (_resetProgressArmed)
            {
                _resetProgressTimer -= frameDt;
                if (_resetProgressTimer <= 0)
                {
                    _resetProgressArmed = false;
                    _resetProgressTimer = 0;
                }
            }
            LogSlowStartupPhase("update-total", startupUpdateStarted);
        }

        private void LogSlowStartupPhase(string phase, double started)
        {
            if (_startupMenuReportLogged || _startupMenuReadyRealtime <= 0) return;
            var milliseconds = (Time.realtimeSinceStartupAsDouble - started) * 1000.0;
            if (milliseconds < 20.0) return;
            Debug.Log(
                "VOIDFALL_STARTUP_PHASE phase=" + phase +
                " atSeconds=" +
                (Time.realtimeSinceStartupAsDouble - _startupMenuReadyRealtime).ToString("F2") +
                " milliseconds=" + milliseconds.ToString("F1"));
        }

        private void LateUpdate()
        {
            ApplySourceParticleDrag(Time.unscaledDeltaTime * _fxSimulationSpeed);
            UpdateVisualCapture();
        }

        private void ApplyFxSimulationSpeed()
        {
            var speed = _gameOver
                ? 0.35f
                : _paused || _levelUpActive || _levelUpTimer >= 0
                    ? 0.12f
                    : _freezeTimer > 0
                        ? 0f
                        : Mathf.Clamp(_timeScale, 0f, 1f);
            _fxSimulationSpeed = speed;
            if (_fx == null) return;
            var main = _fx.main;
            main.simulationSpeed = speed;
        }

        private void ResumeRunFromUi()
        {
            if (_paused && !_gameOver)
            {
                _paused = false;
                RestartQualitySession();
                _audio?.Resume();
                _ui?.SwitchToGameplay();
            }
        }

        /// <summary>
        /// Collects the run's final loadout for the result screen's build recap:
        /// owned weapons (evolutions carrying their evolved name and accent),
        /// supports, then late upgrades, in the browser build's order.
        /// </summary>
        private List<UIBuildChip> BuildRecapChips()
        {
            var chips = new List<UIBuildChip>();
            if (_upgradeProgress == null) return chips;

            var weaponCount = Mathf.Min(ContentCatalog.Weapons.Length, _upgradeProgress.WeaponRanks.Length);
            for (var index = 0; index < weaponCount; index++)
            {
                var rank = _upgradeProgress.WeaponRanks[index];
                if (rank <= 0) continue;
                var evolved = index < _upgradeProgress.Evolved.Length && _upgradeProgress.Evolved[index];
                chips.Add(new UIBuildChip
                {
                    Name = WeaponDisplayName(index, evolved),
                    Rank = rank,
                    Accent = ParseColor(WeaponDisplayAccent(index, evolved), UITheme.CyanLight),
                    Evolved = evolved
                });
            }

            var supportCount = Mathf.Min(ExtendedCatalog.SupportCount, _upgradeProgress.SupportRanks.Length);
            for (var index = 0; index < supportCount; index++)
            {
                var rank = _upgradeProgress.SupportRanks[index];
                if (rank <= 0) continue;
                chips.Add(new UIBuildChip
                {
                    Name = ExtendedCatalog.AllSupports()[index].Name,
                    Rank = rank,
                    Accent = ParseColor(ExtendedCatalog.AllSupports()[index].Accent, UITheme.CyanLight)
                });
            }

            var lateCount = Mathf.Min(ContentCatalog.LateUpgrades.Length, _upgradeProgress.LateRanks.Length);
            for (var index = 0; index < lateCount; index++)
            {
                var rank = _upgradeProgress.LateRanks[index];
                if (rank <= 0) continue;
                chips.Add(new UIBuildChip
                {
                    Name = ContentCatalog.LateUpgrades[index].Name,
                    Rank = rank,
                    Accent = ParseColor(ContentCatalog.LateUpgrades[index].Accent, UITheme.CyanLight)
                });
            }

            return chips;
        }

        /// <summary>
        /// Applies a preference change coming from the interface.
        ///
        /// Continuous controls debounce through the existing dirty timer so a
        /// slider drag does not write the profile every frame; discrete controls
        /// commit immediately and revert themselves if the write fails, which is
        /// the contract the browser build's updateSettings has.
        /// </summary>
        private void ApplySettingFromUi(Action<SaveSettings> mutate, bool immediate)
        {
            var settings = _saveData?.settings;
            if (settings == null || mutate == null) return;

            if (immediate)
            {
                var previous = _settingsController.StageContinuousChange(settings);
                mutate(settings);
                _settingsController.CommitImmediateWithRollback(previous);
                return;
            }

            _settingsController.StageContinuousChange(settings);
            mutate(settings);
            ApplySettings();
        }

        private int CurrentBestScore()
        {
            return _saveData?.highScores != null && _saveData.highScores.Length > 0 && _saveData.highScores[0] != null
                ? _saveData.highScores[0].score
                : 0;
        }

        private void ConfigureVisualCapture()
        {
            var args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length; index++)
            {
                var argument = args[index];
                if (argument.StartsWith("-vfcapture=", StringComparison.OrdinalIgnoreCase))
                {
                    var inlinePath = argument.Substring("-vfcapture=".Length).Trim('"');
                    if (string.IsNullOrWhiteSpace(inlinePath) && index + 1 < args.Length)
                        inlinePath = args[++index];
                    _visualCapturePath = inlinePath.Trim('"');
                    _visualCaptureFramesRemaining = 30;
                }
                else if (string.Equals(argument, "-vfcapture", StringComparison.OrdinalIgnoreCase) &&
                         index + 1 < args.Length)
                {
                    _visualCapturePath = args[++index].Trim('"');
                    _visualCaptureFramesRemaining = 30;
                }
                else if (string.Equals(argument, "-vfcapture-run", StringComparison.OrdinalIgnoreCase))
                {
                    _visualCaptureRun = true;
                }
                else if (string.Equals(argument, "-vfhydra-boss", StringComparison.OrdinalIgnoreCase))
                {
                    _visualCaptureRun = true;
                    _visualCaptureHydraBoss = true;
                    _visualCaptureArena = "hydra";
                }
                else if (argument.StartsWith("-vfhydra-attack=", StringComparison.OrdinalIgnoreCase))
                {
                    _visualCaptureRun = true;
                    _visualCaptureHydraBoss = true;
                    _visualCaptureArena = "hydra";
                    _visualCaptureHydraAttack = argument.Substring("-vfhydra-attack=".Length);
                }
                else if (string.Equals(argument, "-vfcourt-boss", StringComparison.OrdinalIgnoreCase))
                {
                    _visualCaptureRun = true;
                    _visualCaptureCourtBoss = true;
                    _visualCaptureArena = "monochrome-court";
                }
                else if (argument.StartsWith("-vfcourt-hazard=", StringComparison.OrdinalIgnoreCase))
                {
                    _visualCaptureRun = true;
                    _visualCaptureCourtBoss = true;
                    _visualCaptureArena = "monochrome-court";
                    _visualCaptureCourtHazard = argument.Substring("-vfcourt-hazard=".Length);
                }
                else if (string.Equals(argument, "-vfcapture-workshop", StringComparison.OrdinalIgnoreCase))
                {
                    _visualCaptureWorkshop = true;
                }
                else if (string.Equals(argument, "-vfcapture-settings", StringComparison.OrdinalIgnoreCase))
                {
                    _visualCaptureSettings = true;
                }
                else if (string.Equals(argument, "-vfcapture-records", StringComparison.OrdinalIgnoreCase))
                {
                    _visualCaptureRecords = true;
                }
                else if (string.Equals(argument, "-vfcapture-quit", StringComparison.OrdinalIgnoreCase))
                {
                    _visualCaptureQuit = true;
                }
                else if (argument.StartsWith("-vfoverclock=", StringComparison.OrdinalIgnoreCase) &&
                         int.TryParse(argument.Substring("-vfoverclock=".Length), out var streak))
                {
                    _visualCaptureOverclockStreak = Mathf.Clamp(streak, 0, 99);
                }
                else if (argument.StartsWith("-vftime=", StringComparison.OrdinalIgnoreCase) &&
                         float.TryParse(
                             argument.Substring("-vftime=".Length),
                             System.Globalization.NumberStyles.Float,
                             System.Globalization.CultureInfo.InvariantCulture,
                             out var runSeconds))
                {
                    _visualCaptureRunSeconds = Mathf.Max(0f, runSeconds);
                }
                else if (string.Equals(argument, "-vfcritical", StringComparison.OrdinalIgnoreCase))
                {
                    _visualCaptureCritical = true;
                }
                else if (argument.StartsWith("-vfarena=", StringComparison.OrdinalIgnoreCase))
                {
                    _visualCaptureArena = argument.Substring("-vfarena=".Length);
                }
                // -vfno-grain was removed with the film grain itself. Grain is
                // now permanently off, so the flag had nothing left to suppress
                // and silently did nothing for any capture tooling passing it.
                else if (string.Equals(argument, "-vfno-grid", StringComparison.OrdinalIgnoreCase))
                {
                    _visualCaptureNoGrid = true;
                }
            }

            if (string.IsNullOrWhiteSpace(_visualCapturePath)) return;
            if (_visualCaptureHydraBoss || _visualCaptureCourtBoss) _visualCaptureFramesRemaining = 180;
            Application.runInBackground = true;
            Debug.Log(
                $"VoidFall visual capture armed: path={_visualCapturePath}, " +
                $"state={(_visualCaptureRun ? "run" : _visualCaptureWorkshop ? "workshop" : _visualCaptureSettings ? "settings" : _visualCaptureRecords ? "records" : "menu")}");
        }

        private void QuitAfterVisualCapture()
        {
            Application.Quit(0);
        }

        private uint SelectRunSeed()
        {
            if (_diagnosticRunSeedOverride != 0)
            {
                var diagnosticSeed = _diagnosticRunSeedOverride;
                _diagnosticRunSeedOverride = 0;
                return diagnosticSeed;
            }
            if (runSeedOverride != 0) return runSeedOverride;
            // Batch test runs keep fixture evidence reproducible. Normal player
            // sessions use a fresh seed, matching browser runSeed().
            if (Application.isBatchMode) return FixtureRunSeed;
            unchecked
            {
                var ticks = DateTime.UtcNow.Ticks;
                var seed = (uint)ticks ^ (uint)(ticks >> 32) ^ (uint)Environment.TickCount ^
                    ++_runSeedCounter;
                return seed == 0 ? FixtureRunSeed : seed;
            }
        }

        private void StartRun()
        {
            StartRunInternal(true);
        }

        private void StartRunInternal(bool playStartCue, bool ensureSpritesWarmed = true)
        {
            // Anything the menu-time warm has not reached yet is finished here,
            // so a run never rasterizes a sprite on first sighting.
            if (ensureSpritesWarmed) DrainSpriteWarm();
            _stressScenario = null;
            _stressTopUpTimer = 0;
            _runSeed = SelectRunSeed();
            _gameSim.Rng = new Rng(_runSeed);
            _fxSim.FxRng = new Rng(_runSeed ^ 0xa5a5a5a5u);
            ResetRouletteLuck();
            _spatialZoomScale = 1f;
            HideRouletteChest();
            _musicPerimeter?.Configure(
                unchecked((int)_runSeed),
                _qualityPreset.Detail,
                _saveData?.settings != null && _saveData.settings.reducedMotion);
            _mainMenuBrowsing = false;
            if (_worldRoot != null) _worldRoot.gameObject.SetActive(true);
            if (_canvas != null) _canvas.enabled = true;
            if (playStartCue)
            {
                // Browser start() resumes audio and emits the UI cue before
                // opening a fresh run; menu bootstrap intentionally skips it.
                _audio?.Resume();
                _audio?.Play(ProceduralAudio.Cue.Ui, 1f);
            }
            // Authored tracks replace the procedural ambient pad rather than
            // layering over it; the pad stays as the fallback when soundtrack
            // assets are absent. EnterMainMenu() also routes through here with
            // playStartCue false, so keying on it is what stops a menu entry
            // from rolling a gameplay track it is about to discard.
            var soundtrackDrivesMusic = _music != null && _music.HasGameplayTracks;
            if (playStartCue && soundtrackDrivesMusic) _music.PlayGameplay();
            if (!soundtrackDrivesMusic) _audio?.StartPad();
            for (var i = 0; i < _gameSim.Enemies.Length; i++)
            {
                _gameSim.Enemies[i] = default;
                Hide(_enemyViews[i]);
                Hide(_enemyHarvesterFullViews[i]);
                Hide(_enemyExploderWarningViews[i]);
                Hide(_eliteMarkViews[i]);
                Hide(_eliteChargeLaneViews[i]);
                Hide(_eliteChargeArrowViews[i]);
                Hide(_eliteChargeFillRenderers[i]);
                Hide(_eliteChargeArrowFillRenderers[i]);
                Hide(_enemyTelegraphRingViews[i]);
                Hide(_enemyTelegraphLineViews[i]);
                Hide(_enemyTelegraphSecondaryLineViews[i]);
                Hide(_enemyTelegraphTertiaryLineViews[i]);
                Hide(_enemyHarvesterCapacityRingViews[i]);
                Hide(_enemyTelegraphMortarFillViews[i]);
                Hide(_enemyTelegraphExploderFillViews[i]);
                for (var segment = 0; segment < ExploderTelegraphSegmentCount; segment++)
                    Hide(_enemyTelegraphExploderSegmentViews[i * ExploderTelegraphSegmentCount + segment]);
                for (var segment = 0; segment < MortarTelegraphSegmentCount; segment++)
                    Hide(_enemyTelegraphMortarSegmentViews[i * MortarTelegraphSegmentCount + segment]);
                Hide(_enemyTelegraphFillRenderers[i]);
                Hide(_enemyTelegraphArrowFillRenderers[i]);
                Hide(_enemyHealthArcViews[i]);
                Hide(_enemyShieldArcViews[i]);
                Hide(_enemyHealthBackgroundViews[i]);
                Hide(_enemyHealthFillViews[i]);
            }
            ResetEnemyOrder();
            for (var i = 0; i < _gameSim.Bullets.Length; i++)
            {
                _gameSim.Bullets[i] = default;
                Hide(_bulletViews[i]);
                Hide(_bulletContrastViews[i]);
                Hide(_railAfterimageFarViews[i]);
                Hide(_railAfterimageNearViews[i]);
            }
            ResetBulletOrder();
            for (var i = 0; i < _gameSim.HostileShots.Length; i++)
            {
                _gameSim.HostileShots[i] = default;
                Hide(_hostileShotViews[i]);
            }
            ResetHostileShotOrder();
            for (var i = 0; i < _gameSim.Meteors.Length; i++)
            {
                _gameSim.Meteors[i] = default;
                Hide(_meteorViews[i]);
                Hide(_meteorHitViews[i]);
                Hide(_meteorCoreViews[i]);
                Hide(_meteorDangerArcViews[i]);
                Hide(_meteorDangerRingViews[i]);
                Hide(_meteorHealthArcViews[i]);
            }
            ResetMeteorOrder();
            for (var i = 0; i < _fxSim.MeteorShards.Length; i++)
            {
                _fxSim.MeteorShards[i] = default;
                Hide(_meteorShardViews[i]);
            }
            for (var i = 0; i < _fxSim.SourceParticles.Length; i++)
            {
                _fxSim.SourceParticles[i] = default;
                Hide(_sourceParticleViews[i]);
            }
            ResetSourceFxOrder();
            for (var i = 0; i < _impactMarks.Length; i++)
            {
                _impactMarks[i] = default;
                Hide(_impactMarkViews[i]);
                for (var segment = 0; segment < ImpactHeatSegmentCount; segment++)
                    Hide(_impactHeatViews[ImpactHeatSlot(i, segment)]);
            }
            ResetImpactMarkOrder();
            for (var i = 0; i < _fxSim.RingWaves.Length; i++)
            {
                _fxSim.RingWaves[i] = default;
                Hide(_ringWaveViews[i]);
                Hide(_ringWaveGlowViews[i]);
                Hide(_ringWaveSpriteViews[i]);
            }
            for (var i = 0; i < _blastWaves.Length; i++)
            {
                _blastWaves[i] = default;
                Hide(_blastWaveFillViews[i]);
                Hide(_blastWaveRimViews[i]);
                Hide(_blastWaveArcViews[i]);
            }
            ResetBlastWaveOrder();
            for (var i = 0; i < _gameSim.Pickups.Length; i++)
            {
                _gameSim.Pickups[i] = default;
                Hide(_pickupViews[i]);
            }
            ResetPickupOrder();
            for (var i = 0; i < _gameSim.Bosses.Length; i++)
            {
                _gameSim.Bosses[i] = default;
                Hide(_bossViews[i]);
                Hide(_bossTelegraphFillRenderers[i]);
                Hide(_bossTelegraphOutlineViews[i]);
                Hide(_bossShieldFillViews[i]);
            }
            ResetBossOrder();
            Hide(_bossBarBackground);
            Hide(_bossBarFill);
            for (var i = 0; i < _arcEffects.Length; i++)
            {
                _arcEffects[i] = default;
                Hide(_arcViews[i]);
                Hide(_arcCoreViews[i]);
            }
            for (var i = 0; i < _railTrails.Length; i++)
            {
                _railTrails[i] = default;
                Hide(_railTrailViews[i]);
            }
            for (var i = 0; i < _bladeViews.Length; i++) Hide(_bladeViews[i]);
            Hide(_hollowBladeView);
            Hide(_hollowBladeFarView);
            Hide(_hollowBladeNearView);
            if (_fx != null) _fx.Clear();
            _hollowBladeActive = false;
            _hollowBladeAge = 0;
            _hollowBladeCooldown = 0;
            _pulseBurstShots = 0;
            _pulseBurstTimer = 0;
            for (var i = 0; i < _weaponCooldowns.Length; i++) _weaponCooldowns[i] = 0;

            _gameSim.Player.Position = Vector2.zero;
            _gameSim.Player.Velocity = Vector2.zero;
            _cameraFollowPosition = Vector2.zero;
            RefreshWorkshopCosmeticRanks();
            _gameSim.Player.MaxHealth = (float)ContentCatalog.Operative.MaxHealth + _workshopIntegrity * 5;
            _gameSim.Player.Health = _gameSim.Player.MaxHealth;
            _healthGhostFraction = 1f;
            _gameSim.Player.Iframes = 0;
            _overclock.Reset();
            _overclockHudPunch = 0f;
            _overclockVisualSurge = 0f;
            _lastOverclockHudStreak = -1;
            _lastOverclockHudSecond = -1;
            _magnetIntensity = 0f;
            _magnetTarget = 0f;
            _music?.ResetReactiveState();
            _adrenalTimer = 0;
            _playerTrailTimer = 0;
            _gameSim.Player.DyingTimer = 0;
            _levelUpTimer = -1f;
            _levelUpPromptOpenedAt = -1f;
            _levelUpScroll = Vector2.zero;
            _evolutionRevealTimer = 0;
            _evolutionRevealPreviousName = null;
            _evolutionRevealName = null;
            _evolutionRevealWeaponId = null;
            _timeScale = 1f;
            _targetTimeScale = 1f;
            _freezeTimer = 0;
            _time = 0;
            _spawnTimer = 0.9f;
            _bladeAngle = 0;
            _nextBossTime = DirectorRules.BossIntervalSeconds(_runSeed, 0);
            _bossRecoveryUntil = 0;
            _nextEliteTime = (float)ContentCatalog.Elite.FirstAtSeconds;
            _nextEliteVariantTime = (float)EliteRules.EliteCadenceStartSeconds;
            _meteorSpawnTimer = 3f;
            _meteorTarget = MeteorRules.MinOrdinaryMeteors;
            _gameSim.PendingMeteorDetonationCount = 0;
            _directorIndex = 0;
            _nextDirectorEvent = DirectorRules.Event(_runSeed, _directorIndex);
            _directorActive = false;
            _directorWarned = false;
            _directorTimer = 0;
            _directorRecoveryTimer = 0;
            _directorSpawnTimer = 0;
            _pressureReliefTimer = 0;
            _directorSpawned = 0;
            _xp = 0;
            _pickupStep = 0;
            _pickupStepTimer = 0;
            _level = 1;
            _xpNeed = BalanceRules.XpNeededForLevel(_level);
            _upgradeProgress = new UpgradeProgress();
            var startingWeaponIndex = UpgradeRules.StartingWeaponIndex();
            if (startingWeaponIndex < 0)
                throw new InvalidOperationException("Operative starting weapon is missing from the generated catalog.");
            _upgradeProgress.WeaponRanks[startingWeaponIndex] = 1;
            _lastLoadoutHudText = null;
            _nextLoadoutHudRefresh = 0;
            _levelOptions = null;
            _rerollsRemaining = 1;
            _levelUpActive = false;
            _rouletteActive = false;
            _rouletteSession = null;
            _rouletteRng = null;
            _activeWildCards.Clear();
            _standstillSeconds = 0;
            _pistolRank = 1;
            _calibrationRank = 0;
            RecalculatePlayerStats(false);
            _kills = 0;
            _eliteKills = 0;
            _bossKills = 0;
            _partsEarned = 0;
            _damageDealt = 0;
            _damageTaken = 0;
            _score = 0;
            for (var i = 0; i < _weaponDamage.Length; i++) _weaponDamage[i] = 0;
            for (var i = 0; i < _floaters.Length; i++)
            {
                _floaters[i] = new FloaterState { View = i };
                Hide(_floaterViews[i]);
            }
            ResetFloaterOrder();
            for (var i = 0; i < _deathGhosts.Length; i++)
            {
                _deathGhosts[i] = new DeathGhostState { View = i };
                Hide(_deathGhostViews[i]);
            }
            ResetDeathGhostOrder();
            for (var i = 0; i < _damageIndicators.Length; i++)
            {
                _damageIndicators[i] = new DamageIndicatorState { View = i };
                Hide(_damageIndicatorViews[i]);
            }
            ResetDamageIndicatorOrder();
            _killMilestoneIndex = 0;
            _scoreMilestoneIndex = 0;
            ClearToasts();
            _cameraTrauma = 0;
            _redFlash = 0;
            _cyanFlash = 0;
            _amberFlash = 0;
            _arenaFlash = 0;
            _arenaFlashT = 1.5f;
            _arenaDecorClock = 0;
            _ambientClock = 0;
            _arenaDecorDrift = Vector2.zero;
            _arenaMoteSeedsReady = false;
            _arenaMoteSeedDetail = -1;
            _arenaRockSeedsReady = false;
            _arenaRockSeedDetail = -1;
            _fxSim.FxRng = new Rng(_runSeed ^ 0xa5a5a5a5u);
            _telemetry.Begin(_runSeed);
            _telemetrySampleTimer = 10f;
            _lastTelemetryPath = null;
            if (_qualityAuto && _qualityController != null)
            {
                RestartQualitySession();
                ApplyQualityPreset(_qualityController.CurrentPreset);
            }
            else
            {
                _qualityWarmupTimer = QualityGameplayWarmupSeconds;
            }
            _bossSequence = 0;
            _bossCycle = 0;
            _bossWarned = false;
            _pendingDoubleBoss = false;
            _nextBossTelemetryId = 1;
            _nextEnemyId = 1;
            _gameSim.CurvedShotCount = 0;
            _nextArcEffectSequence = 0;
            _nextRailTrailSequence = 0;
            _paused = false;
            _gameOver = false;
            _revivePending = false;
            _revivesRemaining = 1 + WorkshopRank("protocol");
            _runSaved = false;
            _lastRunSaved = true;
            _lastRunIsBest = false;
            _lastRunRank = -1;
            _menuPage = MenuPage.None;
            _menuNotice = null;
            _menuNoticeTimer = 0;
            EnqueueToast("Run started", null, 2.2f, ToastKind.Info);
            // The menu selector previews the prepared arena catalogue. The
            // authored route itself always begins in Abyss.
            _arenaId = playStartCue ? ArenaId.Void : ArenaIdFromName(_saveData?.arena);
            SelectRecipeForCurrentArena();
            EnsureVoidRouteForRun();
            _nextBossTime = float.PositiveInfinity;
            BeginObjectiveForCurrentArena();
            PrepareArenaNeighborhood();
            _arenaTransitionState = ArenaRules.CreateTransitionState(_runSeed);
            _telemetry.RecordLevel(0, _level, _xpNeed, 0);

            for (var i = 0; i < 6; i++) SpawnEnemy("chaser");
            RebuildEnemyGrid();
            // Browser reset() records the initial six-enemy snapshot before the
            // first presented frame; keep that sample in the Unity report too.
            RecordTelemetrySample(Mathf.Max(0.0001f, _debugFrameEmaMs / 1000f));
            if (_ui != null && !_mainMenuBrowsing)
            {
                _ui.SwitchToGameplay();
                _ui.HUD?.UpdateHealth(_gameSim.Player.Health, _gameSim.Player.MaxHealth);
                _ui.HUD?.UpdateShield(0f, 0f);
                _ui.HUD?.UpdateXP((int)_xp, _xpNeed, _level);
                _ui.HUD?.UpdateStats(_score, _kills, 0);
            }
        }

        /// <summary>
        /// Reproduce the browser's opt-in benchmark shortcut through the same
        /// run, upgrade, spawn, and arena primitives used by normal play.
        /// This is diagnostic-only; a normal run never sets _stressScenario.
        /// </summary>
        public bool ApplyStressScenario(string id, uint seed = FixtureRunSeed)
        {
            var scenario = FindStressScenario(id);
            if (scenario == null) return false;

            _diagnosticRunSeedOverride = seed == 0 ? FixtureRunSeed : seed;
            StartRunInternal(false);
            _stressScenario = scenario;
            _stressTopUpTimer = 0;
            _time = Mathf.Max(_time, (float)scenario.TimeSeconds);
            _bossCycle = Mathf.Max(_bossCycle, Mathf.FloorToInt(_time / (4f * 180f)));

            ApplyStressRanks(scenario);
            RecalculatePlayerStats(false);
            _gameSim.Player.Health = _gameSim.Player.MaxHealth;
            _gameSim.Player.Iframes = float.PositiveInfinity;

            var stressArena = ArenaIdFromName(scenario.Arena);
            if (_arenaId != stressArena)
            {
                _arenaId = stressArena;
                SelectRecipeForCurrentArena();
                PrepareArenaNeighborhood();
                ClearMeteors();
                _meteorSpawnTimer = 2.2f;
                _meteorTarget = MeteorRules.MinOrdinaryMeteors;
            }

            for (var round = 0; round < scenario.EliteVariantRounds; round++)
            {
                for (var kindIndex = 0; kindIndex < StressEliteVariantOrder.Length; kindIndex++)
                {
                    var kind = StressEliteVariantOrder[kindIndex];
                    var angle = (float)(_gameSim.Rng.Next() * Math.PI * 2);
                    var distance = 260f + (float)_gameSim.Rng.Next() * 220f;
                    SpawnEnemy(
                        EliteRules.EliteVariantDef(kind).BaseId,
                        _gameSim.Player.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance,
                        kind,
                        false,
                        false);
                }
            }

            for (var round = 0; round < scenario.RosterTwoRounds; round++)
            {
                for (var index = 0; index < StressRosterTwoTypes.Length; index++)
                {
                    var angle = index / (float)StressRosterTwoTypes.Length * Mathf.PI * 2f +
                        round * 0.31f + (float)(_gameSim.Rng.Next() * Mathf.PI * 2);
                    var distance = 240f + (float)_gameSim.Rng.Next() * 260f;
                    SpawnEnemy(
                        StressRosterTwoTypes[index],
                        _gameSim.Player.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance,
                        null,
                        false,
                        false,
                        0,
                        1f,
                        EnemyRoster.Two);
                }
            }

            for (var index = 0; index < scenario.Harvesters; index++)
            {
                var angle = index / (float)Mathf.Max(1, scenario.Harvesters) * Mathf.PI * 2f;
                SpawnEnemy(
                    "harvester",
                    _gameSim.Player.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 280f,
                    null,
                    false,
                    false);
            }

            while (ActiveBosses() < scenario.Bosses)
            {
                var before = ActiveBosses();
                var encounter = DirectorRules.BossEncounter(_runSeed, _bossSequence++);
                SpawnBoss(encounter.Id, encounter.HealthScale, encounter.DamageScale, encounter.Cycle);
                if (ActiveBosses() == before) break;
            }

            if (scenario.MeteorStorm)
            {
                for (var index = 0; index < MeteorRules.MaxExplosiveMeteors; index++)
                    TrySpawnMeteor(true);
                for (var index = 0; index < MeteorRules.MaxOrdinaryMeteors; index++)
                    TrySpawnMeteor(false);
            }

            TopUpStress(scenario);
            return true;
        }

        /// <summary>Stop the diagnostic top-up loop and release its invulnerability.</summary>
        public void ClearStressScenario()
        {
            _stressScenario = null;
            _stressTopUpTimer = 0;
            if (float.IsInfinity(_gameSim.Player.Iframes)) _gameSim.Player.Iframes = 0;
        }

        private void DriveStress(float frameSeconds)
        {
            var scenario = _stressScenario;
            if (scenario == null) return;
            _gameSim.Player.Iframes = float.PositiveInfinity;
            _stressTopUpTimer -= frameSeconds;
            if (_stressTopUpTimer > 0) return;
            _stressTopUpTimer = Mathf.Max(0.25f, (float)scenario.TopUpSeconds);
            TopUpStress(scenario);
        }

        private void TopUpStress(StressScenarioDefinition scenario)
        {
            var enemyTarget = Mathf.Min(
                Mathf.RoundToInt(MaxEnemies * Mathf.Clamp01((float)scenario.EnemyFill)),
                MaxEnemies);
            var enemyGuard = MaxEnemies * 6;
            while (ActiveEnemies() < enemyTarget && enemyGuard-- > 0)
                SpawnEnemy(ChooseAmbientEnemy());

            var pickupTarget = Mathf.RoundToInt(MaxPickups * Mathf.Clamp01((float)scenario.PickupFill));
            var pickupGuard = MaxPickups * 2;
            while (ActivePickups() < pickupTarget && pickupGuard-- > 0)
            {
                var angle = (float)(_gameSim.Rng.Next() * Math.PI * 2);
                var radius = 120f + (float)_gameSim.Rng.Next() * 560f;
                var position = _gameSim.Player.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (ActivePickups() % 8 == 7) SpawnRarePickup(position);
                else SpawnPickup(position, 1 + Mathf.FloorToInt((float)(_gameSim.Rng.Next() * 10)));
            }

            var shotTarget = Mathf.RoundToInt(MaxHostileShots * Mathf.Clamp01((float)scenario.HostileShotFill));
            var shotGuard = MaxHostileShots * 2;
            while (ActiveHostileShots() < shotTarget && shotGuard-- > 0)
            {
                var angle = (float)(_gameSim.Rng.Next() * Math.PI * 2);
                var radius = 320f + (float)_gameSim.Rng.Next() * 300f;
                var direction = new Vector2(-Mathf.Cos(angle), -Mathf.Sin(angle));
                SpawnHostileShot(
                    _gameSim.Player.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius,
                    direction,
                    1f,
                    260f,
                    ActiveHostileShots() % 4 == 3 ? 0.6f : 0f,
                    false,
                    -1);
            }
        }

        private void ApplyStressRanks(StressScenarioDefinition scenario)
        {
            if (_upgradeProgress == null) return;
            for (var index = 0; index < scenario.WeaponRanks.Length; index++)
            {
                var value = scenario.WeaponRanks[index];
                for (var weaponIndex = 0; weaponIndex < ContentCatalog.Weapons.Length; weaponIndex++)
                    if (ContentCatalog.Weapons[weaponIndex].Id == value.Id)
                        _upgradeProgress.WeaponRanks[weaponIndex] = value.Rank;
            }
            for (var index = 0; index < scenario.SupportRanks.Length; index++)
            {
                var value = scenario.SupportRanks[index];
                for (var supportIndex = 0; supportIndex < ExtendedCatalog.SupportCount; supportIndex++)
                    if (ExtendedCatalog.AllSupports()[supportIndex].Id == value.Id)
                        _upgradeProgress.SupportRanks[supportIndex] = value.Rank;
            }
            for (var index = 0; index < scenario.LateRanks.Length; index++)
            {
                var value = scenario.LateRanks[index];
                for (var lateIndex = 0; lateIndex < ContentCatalog.LateUpgrades.Length; lateIndex++)
                    if (ContentCatalog.LateUpgrades[lateIndex].Id == value.Id)
                        _upgradeProgress.LateRanks[lateIndex] = value.Rank;
            }
            for (var index = 0; index < scenario.Evolve.Length; index++)
            {
                for (var weaponIndex = 0; weaponIndex < ContentCatalog.Weapons.Length; weaponIndex++)
                {
                    if (ContentCatalog.Weapons[weaponIndex].Id != scenario.Evolve[index]) continue;
                    _upgradeProgress.Evolved[weaponIndex] =
                        _upgradeProgress.WeaponRanks[weaponIndex] >= ProgressionRules.MaxWeaponRank;
                }
            }
            _pistolRank = _upgradeProgress.WeaponRanks.Length > 0 ? _upgradeProgress.WeaponRanks[0] : 0;
            _calibrationRank = SupportRank("calibration");
        }

        private static StressScenarioDefinition FindStressScenario(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (var index = 0; index < ContentCatalog.StressScenarios.Length; index++)
                if (ContentCatalog.StressScenarios[index].Id == id)
                    return ContentCatalog.StressScenarios[index];
            return null;
        }

        private void Simulate(double fixedDt)
        {
            var realDt = (float)fixedDt;
            var frozen = _freezeTimer > 0;
            if (frozen) _freezeTimer = Mathf.Max(0, _freezeTimer - realDt);
            var dt = frozen ? 0 : realDt * _timeScale;
            _time += dt;
            if (_gameSim.Player.Health > 0)
            {
                _gameSim.Player.Health = Mathf.Min(
                    _gameSim.Player.MaxHealth,
                    _gameSim.Player.Health + 0.6f * SupportRank("regenerator") * dt);
            }
            RebuildEnemyGrid();
            MovePlayer(dt);
            ApplyHydraRibCageCollision();
            // The browser applies current-step movement/iframes/boost effects
            // first, then expires their timers before the remaining systems run.
            _gameSim.Player.Iframes = Mathf.Max(0, _gameSim.Player.Iframes - dt);
            _overclock.Step(dt);
            // OVERCLOCKER wild card: a permanent tier-1 floor on the boost.
            if (!_overclock.Active && HasWildCard(WildCardId.Overclocker))
                _overclock.HoldTier1();
            _adrenalTimer = Mathf.Max(0, _adrenalTimer - dt);
            UpdateCameraFollow(dt);
            UpdateWeapons(dt);

            if (_voidRoute != null) StepRiftTransition(dt);
            if (_voidRoute != null) StepRiftSafetyNet();
            var arenaStep = _voidRoute == null
                ? ArenaRules.Step(
                    _arenaTransitionState,
                    dt,
                    _time,
                    _runSeed,
                    _arenaId,
                    ArenaTransitionBlocked())
                : new ArenaStepResult(_arenaTransitionState, ArenaTransitionEvent.None);
            var previousArena = _arenaId;
            _arenaTransitionState = arenaStep.State;
            var transitionCue = ArenaTransitionCueFor(arenaStep.Event);
            if (transitionCue.HasValue) _audio?.Play(transitionCue.Value);
            if (arenaStep.Event == ArenaTransitionEvent.Warn)
            {
                ShowArenaToast("ARENA SHIFT IN 6s", 6f);
                _arenaBannerRemaining = (float)ArenaRules.WarningSeconds;
                _arenaBannerIncoming = arenaStep.State.Incoming ?? previousArena;
                // The warning lead starts/continues the asynchronous package
                // load. Runtime never generates arena pixels.
                BeginArenaPackageLoad(_arenaBannerIncoming);
                _telemetry.RecordArenaWarning(
                    arenaStep.State.Index,
                    ArenaIdName(previousArena),
                    arenaStep.State.Incoming.HasValue ? ArenaIdName(arenaStep.State.Incoming.Value) : ArenaIdName(previousArena),
                    (float)_time);
            }
            if (arenaStep.Event == ArenaTransitionEvent.Swap && arenaStep.State.Incoming.HasValue)
            {
                _arenaId = arenaStep.State.Incoming.Value;
                // Endless-clock rotation re-keys the objective only without a
                // route; with a route the Void's objective must survive an
                // incidental arena rotation without resetting its progress.
                if (_voidRoute == null) BeginObjectiveForCurrentArena();
                SelectRecipeForCurrentArena();
                PrepareArenaNeighborhood();
                ClearMeteors();
                _meteorSpawnTimer = 2.2f;
                _meteorTarget = MeteorRules.MinOrdinaryMeteors;
                _arenaFlash = Mathf.Max(_arenaFlash, 0.34f);
                _cyanFlash = Mathf.Max(_cyanFlash, 0.28f);
                SpawnRingWave(_cameraFollowPosition, 18f, 520f, 0.7f,
                    new Color(0.133f, 0.827f, 0.933f, 0.9f));
                BurstFx(_cameraFollowPosition, SourceDotColor("cyan"),
                    10, 190, 0.46f, 0.72f);
                AddCameraShake(0.16f);
                var arena = FindArena(ArenaIdName(_arenaId));
                ShowArenaToast(
                    arena?.Name ?? ArenaName(_arenaId),
                    2.5f,
                    ToastKind.Info,
                    arena?.Modifier);
                _telemetry.RecordArenaSwap(arenaStep.State.Index, (float)_time);
            }
            else if (arenaStep.Event == ArenaTransitionEvent.Complete)
            {
                _telemetry.RecordArenaComplete(arenaStep.State.Index - 1, (float)_time);
            }
            else if (arenaStep.Event == ArenaTransitionEvent.Deferred)
            {
                _telemetry.RecordArenaDeferred();
                _arenaBannerRemaining = 0;
            }
            if (_arenaBannerRemaining > 0)
                _arenaBannerRemaining = AdvanceArenaBanner(_arenaBannerRemaining, dt);
            UpdateArenaCycleFlash(dt);
            _telemetry.RecordArenaTime(ArenaIdName(_arenaId), dt);
            StepHydraSurvival(dt);
            StepMonochromeSurvival(dt);
            UpdateSpawns(dt);
            UpdateEnemies(dt);
            // Relax separation over several passes, rebuilding the grid between
            // each so a body that moved cells is still paired correctly. One
            // pass (the browser behavior) cannot unpack a dense clump.
            for (var pass = 0; pass < SeparationRules.Passes; pass++)
            {
                RebuildEnemyGrid();
                SeparateEnemies();
            }
            RebuildEnemyGrid();
            UpdateMeteors(dt);
            UpdateNebulaStrikes(dt);
            UpdateBlades(dt);
            UpdateBullets(dt);
            UpdateRailTrails(dt);
            UpdateHostileShots(dt);
            UpdateBosses(dt);
            StepHydraAttackState(dt);
            UpdatePickups(dt);
            // Keep the browser updateFx() lifecycle order after all gameplay
            // systems have emitted their effects for this fixed step.
            UpdateArcEffects(dt);
            UpdateDamageIndicators(dt);
            UpdateImpactMarks(dt);
            UpdateBlastWaves(dt);
            UpdateDeathGhosts(dt);
            UpdateSourceParticles(dt);
            UpdateMeteorShards(dt);
            UpdateRingWaves(dt);
            UpdateFloaters(dt);
            CheckMilestones();
            while (!_levelUpActive && _levelUpTimer < 0 && _xp >= _xpNeed)
            {
                _xp -= _xpNeed;
                _level++;
                _xpNeed = BalanceRules.XpNeededForLevel(_level);
                ApplyLevelRecovery();
                _telemetry.RecordLevel((float)_time, _level, _xpNeed, Mathf.FloorToInt(_xp));
                OpenLevelUp();
            }

            // The browser gives the level-up burst a short real-time slowdown
            // before it opens the choice screen. Gameplay continues at the
            // eased time scale during that window; only the prompt transition
            // uses real fixed time.
            if (_levelUpTimer >= 0)
            {
                _levelUpTimer -= realDt;
                if (_levelUpTimer <= 0)
                {
                    _levelUpTimer = -1;
                    _levelOptions = RollLevelOptions();
                    if (_levelOptions.Length == 0)
                    {
                        _partsEarned += 2;
                        _score += 150;
                        _gameSim.Player.Health = Mathf.Min(_gameSim.Player.MaxHealth, _gameSim.Player.Health + 12);
                        _targetTimeScale = 1;
                        if (_xp >= _xpNeed)
                        {
                            _xp -= _xpNeed;
                            _level++;
                            _xpNeed = BalanceRules.XpNeededForLevel(_level);
                            ApplyLevelRecovery();
                            _telemetry.RecordLevel((float)_time, _level, _xpNeed, Mathf.FloorToInt(_xp));
                            OpenLevelUp();
                        }
                    }
                    else
                    {
                        _levelUpPromptOpenedAt = Time.realtimeSinceStartup;
                        _levelUpScroll = Vector2.zero;
                        _levelUpActive = true;
                        _paused = true;
                        if (_ui != null && _levelOptions != null)
                        {
                            _ui.LevelUp?.ShowUpgrades(
                                BuildUpgradeCards(_levelOptions),
                                _rerollsRemaining,
                                SelectLevelOption);
                        }
                    }
                }
            }

            // The browser resolves the defeat/revive transition after the
            // current simulation systems, so damage dealt in this step still
            // consumes the current step before the defeat timer advances.
            if (_gameSim.Player.DyingTimer > 0)
            {
                _gameSim.Player.DyingTimer -= realDt;
                if (_gameSim.Player.DyingTimer <= 0)
                {
                    _gameSim.Player.DyingTimer = 0;
                    if (_revivesRemaining > 0)
                    {
                        _revivePending = true;
                        _paused = true;
                        _ui?.Revive?.Show(_revivesRemaining);
                    }
                    else
                    {
                        EndRun();
                    }
                }
            }

            if (_gameSim.Player.Health <= 0 && !_revivePending && _gameSim.Player.DyingTimer <= 0 && !_gameOver)
                EndRun();

            _timeScale += (_targetTimeScale - _timeScale) *
                (1 - Mathf.Exp(-9f * realDt));

            StepObjectiveTracker(dt);
        }

        private static readonly Color DotCyan = ParseColor("#22d3ee", Color.white);
        private static readonly Color DotPink = ParseColor("#fb7185", Color.white);
        private static readonly Color DotViolet = ParseColor("#a78bfa", Color.white);
        private static readonly Color DotFuchsia = ParseColor("#e879f9", Color.white);
        private static readonly Color DotOrange = ParseColor("#fb923c", Color.white);
        private static readonly Color DotRed = ParseColor("#ef4444", Color.white);
        private static readonly Color DotEmerald = ParseColor("#34d399", Color.white);
        private static readonly Color DotLime = ParseColor("#a3e635", Color.white);
        private static readonly Color DotBlue = ParseColor("#60a5fa", Color.white);
        private static readonly Color DotAmber = ParseColor("#f59e0b", Color.white);
        private static readonly Color DotWhite = ParseColor("#e2e8f0", Color.white);
        private static readonly Color DotYellow = ParseColor("#facc15", Color.white);

        internal static Color SourceDotColor(string dot)
        {
            // These are the browser sprites.dot palette entries. Burst and
            // trail particles use the color-baked dot sprite, so keeping this
            // mapping centralized prevents hand-tuned RGB drift at call sites.
            switch (dot)
            {
                case "cyan": return DotCyan;
                case "pink": return DotPink;
                case "violet": return DotViolet;
                case "fuchsia": return DotFuchsia;
                case "orange": return DotOrange;
                case "red": return DotRed;
                case "emerald": return DotEmerald;
                case "lime": return DotLime;
                case "blue": return DotBlue;
                case "amber": return DotAmber;
                case "white": return DotWhite;
                case "yellow": return DotYellow;
                default: return Color.white;
            }
        }

        private float SafestEscapeAngle(float fallback)
        {
            var movementSpeed = _gameSim.Player.Velocity.magnitude;
            var preferred = movementSpeed > 35f
                ? Mathf.Atan2(_gameSim.Player.Velocity.y, _gameSim.Player.Velocity.x)
                : fallback;
            var bestAngle = preferred;
            var bestScore = float.PositiveInfinity;
            for (var sample = 0; sample < 16; sample++)
            {
                var angle = preferred + sample / 16f * Mathf.PI * 2f;
                var score = sample == 0 ? -0.025f : 0f;
                foreach (var enemy in _gameSim.Enemies)
                {
                    if (!enemy.Active) continue;
                    var delta = enemy.Position - _gameSim.Player.Position;
                    var distance = delta.magnitude;
                    if (distance > 440f) continue;
                    var enemyAngle = Mathf.Atan2(delta.y, delta.x);
                    var difference = Mathf.Atan2(
                        Mathf.Sin(enemyAngle - angle),
                        Mathf.Cos(enemyAngle - angle));
                    if (Mathf.Abs(difference) > Mathf.PI / 5f) continue;
                    score += EnemyThreat(enemy) / Mathf.Max(55f, distance);
                }
                if (score < bestScore)
                {
                    bestScore = score;
                    bestAngle = angle;
                }
            }
            return bestAngle;
        }

        private int AmbientTypeLimit(string id)
        {
            var stage = Mathf.FloorToInt(_time / 120f);
            switch (id)
            {
                case "runner": return Mathf.Min(18, 8 + stage * 2);
                case "dasher": return Mathf.Min(12, 4 + stage);
                case "gunner": return Mathf.Min(9, 4 + Mathf.FloorToInt(stage / 2f));
                case "twinGunner": return Mathf.Min(4, 1 + Mathf.FloorToInt(stage / 3f));
                case "brute": return Mathf.Min(10, 4 + stage);
                case "exploder": return Mathf.Min(6, 2 + Mathf.FloorToInt(stage / 2f));
                case "guard": return Mathf.Min(6, 2 + Mathf.FloorToInt(stage / 2f));
                case "technician": return 2;
                case "mortar": return 3;
                case "splitter": return Mathf.Min(9, 3 + Mathf.FloorToInt(stage / 2f));
                case "bulwark": return 4;
                case "harvester": return 3;
                case "carrier": return 2;
                default: return int.MaxValue;
            }
        }

        private bool AmbientTypeAllowed(string id)
        {
            var limit = AmbientTypeLimit(id);
            var active = 0;
            foreach (var enemy in _gameSim.Enemies)
            {
                if (enemy.Active && enemy.Id == id && ++active >= limit) return false;
            }
            return true;
        }

        private void PlayFuseWarning(int stage = 0)
        {
            _audio?.PlayFuseWarning(Mathf.Clamp(stage, 0, 5));
        }


        private void ResetHostileShotOrder() => _gameSim.HostileShotOrder.Reset();

        private void AppendHostileShotOrder(int slot) => _gameSim.HostileShotOrder.Append(slot);

        private void RemoveHostileShotOrder(int slot) => _gameSim.HostileShotOrder.Remove(slot);

        private void EnsureHostileShotOrderEntries()
        {
            for (var index = 0; index < _gameSim.HostileShots.Length; index++)
            {
                if (_gameSim.HostileShots[index].Active) AppendHostileShotOrder(index);
            }
            for (var order = _gameSim.HostileShotOrder.Count - 1; order >= 0; order--)
            {
                var slot = _gameSim.HostileShotOrder.SlotAt(order);
                if (slot < 0 || !_gameSim.HostileShots[slot].Active) RemoveHostileShotOrder(slot);
            }
        }

        private void SeparateEnemies() => _gameSim.SeparateEnemies();
        private HostileTarget FindNearestHostileFrom(
            Vector2 origin,
            float range,
            BulletState bullet,
            bool excludeHitHistory = true,
            HashSet<int> visited = null,
            int[] visitedBuffer = null,
            int visitedCount = 0)
        {
            var target = new HostileTarget
            {
                Valid = false,
                Index = -1,
                DistanceSquared = range * range,
            };
            // Browser nearestHostile scans the enemy array in insertion order;
            // keep that order so equal-distance ties resolve identically.
            for (var order = 0; order < _gameSim.EnemyOrderCount; order++)
            {
                var index = _gameSim.EnemyOrder[order];
                var enemy = _gameSim.Enemies[index];
                if (!enemy.Active || enemy.Age < 0.15f ||
                    IsVisited(visited, visitedBuffer, visitedCount, EnemyIdentity(enemy, index)) ||
                    (excludeHitHistory && BulletAlreadyHitEnemy(bullet, index))) continue;
                var distance = (enemy.Position - origin).sqrMagnitude;
                if (distance >= target.DistanceSquared) continue;
                target = new HostileTarget
                {
                    Valid = true,
                    Boss = false,
                    Index = index,
                    Identity = EnemyIdentity(enemy, index),
                    Position = enemy.Position,
                    DistanceSquared = distance,
                };
            }
            EnsureBossOrderEntries();
            for (var bossOrder = 0; bossOrder < _gameSim.BossOrderCount; bossOrder++)
            {
                var index = _gameSim.BossOrder[bossOrder];
                var boss = _gameSim.Bosses[index];
                if (!boss.Active || boss.State == 4 ||
                    IsVisited(visited, visitedBuffer, visitedCount, -BossIdentity(boss, index)) ||
                    (excludeHitHistory && BossAlreadyHit(bullet, boss, index))) continue;
                var distance = (boss.Position - origin).sqrMagnitude;
                if (distance >= target.DistanceSquared) continue;
                target = new HostileTarget
                {
                    Valid = true,
                    Boss = true,
                    Index = index,
                    Identity = BossIdentity(boss, index),
                    Position = boss.Position,
                    DistanceSquared = distance,
                };
            }
            return target;
        }

        private static bool IsVisited(
            HashSet<int> visited,
            int[] visitedBuffer,
            int visitedCount,
            int identity)
        {
            if (visited != null) return visited.Contains(identity);
            for (var index = 0; index < visitedCount; index++)
            {
                if (visitedBuffer[index] == identity) return true;
            }
            return false;
        }

        private static void AddVisited(int[] visitedBuffer, ref int visitedCount, int identity)
        {
            if (IsVisited(null, visitedBuffer, visitedCount, identity)) return;
            if (visitedCount < visitedBuffer.Length) visitedBuffer[visitedCount++] = identity;
        }

        private bool RetargetRicochet(ref BulletState bullet)
        {
            var target = FindNearestHostileFrom(bullet.Position, 420, bullet);
            if (!target.Valid) return false;
            var direction = target.Position - bullet.Position;
            var angle = Mathf.Atan2(direction.y, direction.x);
            var speed = bullet.Velocity.magnitude;
            bullet.Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            return true;
        }

        private HostileTarget FindNearestHostile(float range)
        {
            var target = new HostileTarget
            {
                Valid = false,
                Index = -1,
                DistanceSquared = range * range,
            };
            // Browser nearestHostile scans every enemy, rather than a spatial
            // candidate list. Preserve its exact candidate/tie order here.
            for (var order = 0; order < _gameSim.EnemyOrderCount; order++)
            {
                var index = _gameSim.EnemyOrder[order];
                var enemy = _gameSim.Enemies[index];
                if (!enemy.Active || enemy.Age < 0.15f) continue;
                var distance = (enemy.Position - _gameSim.Player.Position).sqrMagnitude;
                if (distance >= target.DistanceSquared) continue;
                target = new HostileTarget
                {
                    Valid = true,
                    Boss = false,
                    Index = index,
                    Identity = EnemyIdentity(enemy, index),
                    Position = enemy.Position,
                    DistanceSquared = distance,
                };
            }
            EnsureBossOrderEntries();
            for (var bossOrder = 0; bossOrder < _gameSim.BossOrderCount; bossOrder++)
            {
                var index = _gameSim.BossOrder[bossOrder];
                var boss = _gameSim.Bosses[index];
                if (!boss.Active || boss.State == 4) continue;
                var distance = (boss.Position - _gameSim.Player.Position).sqrMagnitude;
                if (distance >= target.DistanceSquared) continue;
                target = new HostileTarget
                {
                    Valid = true,
                    Boss = true,
                    Index = index,
                    Identity = BossIdentity(boss, index),
                    Position = boss.Position,
                    DistanceSquared = distance,
                };
            }
            return target;
        }

        private HostileTarget FindNearestUnvisitedHostile(
            Vector2 origin,
            float range,
            int[] visited,
            int visitedCount)
        {
            var target = new HostileTarget
            {
                Valid = false,
                Index = -1,
                DistanceSquared = range * range,
            };
            // Browser chain retargeting also walks the enemy array directly.
            for (var order = 0; order < _gameSim.EnemyOrderCount; order++)
            {
                var index = _gameSim.EnemyOrder[order];
                var enemy = _gameSim.Enemies[index];
                if (!enemy.Active || enemy.Age < 0.15f ||
                    IsVisited(null, visited, visitedCount, EnemyIdentity(enemy, index))) continue;
                var distance = (enemy.Position - origin).sqrMagnitude;
                if (distance >= target.DistanceSquared) continue;
                target = new HostileTarget
                {
                    Valid = true,
                    Boss = false,
                    Index = index,
                    Identity = EnemyIdentity(enemy, index),
                    Position = enemy.Position,
                    DistanceSquared = distance,
                };
            }
            EnsureBossOrderEntries();
            for (var bossOrder = 0; bossOrder < _gameSim.BossOrderCount; bossOrder++)
            {
                var index = _gameSim.BossOrder[bossOrder];
                var boss = _gameSim.Bosses[index];
                if (!boss.Active || boss.State == 4 ||
                    IsVisited(null, visited, visitedCount, -BossIdentity(boss, index))) continue;
                var distance = (boss.Position - origin).sqrMagnitude;
                if (distance >= target.DistanceSquared) continue;
                target = new HostileTarget
                {
                    Valid = true,
                    Boss = true,
                    Index = index,
                    Identity = BossIdentity(boss, index),
                    Position = boss.Position,
                    DistanceSquared = distance,
                };
            }
            return target;
        }

        private float HollowBladeReach(WeaponStatsDefinition stats)
        {
            return Mathf.Max(360f, (float)stats.Range * 4.25f, (float)stats.OrbitRadius + 180f) * _areaMultiplier;
        }

        private void HideBlades(int start)
        {
            for (var index = start; index < _bladeViews.Length; index++) Hide(_bladeViews[index]);
            if (start == 0)
            {
                Hide(_hollowBladeView);
                Hide(_hollowBladeFarView);
                Hide(_hollowBladeNearView);
            }
        }

        private void CreateArcEffect(Vector2[] points, bool evolved)
        {
            var slot = SelectArcEffectSlot(_arcEffects);
            var jaggedPoints = JaggedArcPoints(points);
            var view = EnsureArcView(slot);
            view.positionCount = jaggedPoints.Length;
            for (var index = 0; index < jaggedPoints.Length; index++)
                view.SetPosition(index, jaggedPoints[index]);
            var outerColor = evolved
                ? new Color(250f / 255f, 204f / 255f, 21f / 255f, 1f)
                : new Color(147f / 255f, 197f / 255f, 253f / 255f, 1f);
            var innerColor = evolved
                ? new Color(254f / 255f, 249f / 255f, 195f / 255f, 1f)
                : new Color(248f / 255f, 250f / 255f, 252f / 255f, 1f);
            var outerWidth = evolved ? 5.5f : 4.5f;
            var innerWidth = evolved ? 2f : 1.6f;
            view.startColor = outerColor;
            view.endColor = view.startColor;
            view.startWidth = outerWidth;
            view.endWidth = outerWidth;
            ConfigureRoundLine(view);
            view.enabled = true;
            var core = EnsureArcCoreView(slot);
            core.positionCount = jaggedPoints.Length;
            for (var index = 0; index < jaggedPoints.Length; index++)
                core.SetPosition(index, jaggedPoints[index]);
            core.startColor = innerColor;
            core.endColor = innerColor;
            core.startWidth = innerWidth;
            core.endWidth = innerWidth;
            ConfigureRoundLine(core);
            core.enabled = true;
            var maxLife = evolved ? 0.22f : 0.16f;
            _arcEffects[slot] = new ArcEffectState
            {
                Active = true,
                Points = jaggedPoints,
                Life = maxLife,
                MaxLife = maxLife,
                Sequence = _nextArcEffectSequence++,
                View = slot,
            };
        }

        private Vector2[] JaggedArcPoints(Vector2[] points)
        {
            if (points == null || points.Length < 2) return points ?? Array.Empty<Vector2>();
            var jagged = new List<Vector2>(1 + (points.Length - 1) * 3)
            {
                points[0],
            };
            for (var index = 1; index < points.Length; index++)
            {
                var previous = jagged[jagged.Count - 1];
                var next = points[index];
                var delta = next - previous;
                var length = SourceLengthOrOne(delta);
                var normal = new Vector2(-delta.y / length, delta.x / length);
                for (var tIndex = 0; tIndex < 2; tIndex++)
                {
                    var t = tIndex == 0 ? 0.33f : 0.66f;
                    var offset = ((float)_fxSim.FxRng.Next() - 0.5f) * Mathf.Min(28f, length * 0.3f);
                    jagged.Add(previous + delta * t + normal * offset);
                }
                jagged.Add(next);
            }
            return jagged.ToArray();
        }

        private static int SelectArcEffectSlot(ArcEffectState[] effects)
        {
            var slot = -1;
            var oldestSequence = int.MaxValue;
            for (var index = 0; index < effects.Length; index++)
            {
                if (!effects[index].Active) return index;
                if (effects[index].Sequence < oldestSequence)
                {
                    oldestSequence = effects[index].Sequence;
                    slot = index;
                }
            }
            return slot < 0 ? 0 : slot;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            var segment = end - start;
            var lengthSquared = segment.sqrMagnitude;
            // Browser distanceToSegment() substitutes a unit denominator only
            // for an exactly zero-length segment. Do not widen that degenerate
            // case to tiny but valid segments.
            if (lengthSquared <= 0f) lengthSquared = 1f;
            var projection = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
            return Vector2.Distance(point, start + segment * projection);
        }

        internal static float HarvesterSpeedCapAt(float elapsedSeconds, int bossCycle)
        {
            return EnemySpeedScaleAt(elapsedSeconds, bossCycle) * 1.3f;
        }

        private int FindXpOverflowTarget(Vector2 dropPosition)
        {
            var nearbyIndex = -1;
            var nearbyDistanceSquared = 180f * 180f;
            var farthestIndex = -1;
            var farthestDistanceSquared = -1f;
            for (var index = 0; index < _gameSim.Pickups.Length; index++)
            {
                var pickup = _gameSim.Pickups[index];
                if (!pickup.Active || pickup.Kind != PickupKind.Xp) continue;
                var dropDistanceSquared = (pickup.Position - dropPosition).sqrMagnitude;
                if (dropDistanceSquared < nearbyDistanceSquared)
                {
                    nearbyDistanceSquared = dropDistanceSquared;
                    nearbyIndex = index;
                }
                var playerDistanceSquared = (pickup.Position - _gameSim.Player.Position).sqrMagnitude;
                if (playerDistanceSquared > farthestDistanceSquared)
                {
                    farthestDistanceSquared = playerDistanceSquared;
                    farthestIndex = index;
                }
            }
            return nearbyIndex >= 0 ? nearbyIndex : farthestIndex;
        }

        internal static float SourceLengthOrOne(Vector2 value)
        {
            var length = value.magnitude;
            return length > 0f ? length : 1f;
        }

        private static Vector2 SourceNormalizedDirection(Vector2 value)
        {
            var length = value.magnitude;
            // Mirrors combat.normalizedDirection(): tiny finite vectors are
            // treated as stationary, while larger vectors are normalized.
            if (float.IsNaN(length) || float.IsInfinity(length) || length < 0.0001f)
                return Vector2.zero;
            return value / length;
        }

        private static float SourceExploderArmedScale(
            float stateTimer,
            float telegraph,
            float ambientClock)
        {
            var progress = Mathf.Clamp01(1f - stateTimer / Mathf.Max(0.01f, telegraph));
            return 1f + progress * 0.16f +
                Mathf.Max(0f, Mathf.Sin(ambientClock * (16f + progress * 10f))) * 0.06f;
        }

        private static float SourceExploderWarningAlpha(
            float stateTimer,
            float telegraph,
            bool eliteExploder,
            float ambientClock)
        {
            var progress = Mathf.Clamp01(1f - stateTimer / Mathf.Max(0.01f, telegraph));
            var pulseRate = eliteExploder
                ? (float)EliteRules.EliteExploderFlashRate(stateTimer, telegraph)
                : 7f + progress * 20f;
            var pulse = 0.5f + Mathf.Sin(ambientClock * pulseRate) * 0.5f;
            var hardFlash = pulse > 0.58f - progress * 0.22f;
            return hardFlash
                ? 0.34f + progress * 0.24f
                : 0.08f + pulse * 0.1f;
        }

        internal static void ApplySourcePlayerKnockback(ref EnemyState enemy, Vector2 impactDirection)
        {
            var direction = SourceNormalizedDirection(impactDirection);
            if (direction == Vector2.zero) return;
            var resistance = enemy.Elite ? 0.12f : enemy.Id == "brute" ? 0.32f : 1f;
            enemy.Knockback -= direction * 150f * resistance;
        }

        private bool IsMatriarchShielded(BossState boss)
        {
            if (!boss.Active || boss.Id != "matriarch" || boss.TelemetryInstanceId <= 0) return false;
            var livingSummons = 0;
            for (var index = 0; index < _gameSim.Enemies.Length; index++)
            {
                var enemy = _gameSim.Enemies[index];
                if (!enemy.Active || !enemy.MatriarchBodyguard ||
                    enemy.SummonedByBossTelemetryId != boss.TelemetryInstanceId) continue;
                livingSummons++;
                if (livingSummons >= 3) return true;
            }
            return false;
        }

        private void DetonateBomb()
        {
            var viewportHalf = GameplayViewportHalfExtent();
            var waveRadius = new Vector2(viewportHalf.x * 2f, viewportHalf.y * 2f).magnitude * 0.68f + 80f;
            SpawnBlastWave(_gameSim.Player.Position, waveRadius, 0.72f, true);
            // The browser bomb is a screen-wide presentation event. Its
            // damage scope is enemies and bosses; meteors are intentionally
            // not included in that source path.
            SpawnRingWave(
                _gameSim.Player.Position,
                22f,
                1120f,
                0.78f,
                new Color(1f, 0.78f, 0.16f, 0.82f));
            SpawnRingWave(
                _gameSim.Player.Position,
                12f,
                760f,
                0.58f,
                new Color(1f, 0.46f, 0.12f, 0.78f));
            BurstFx(_gameSim.Player.Position, SourceDotColor("white"), 18, 360, 0.42f, 0.82f);
            BurstFx(_gameSim.Player.Position, SourceDotColor("yellow"), 24, 440, 0.64f, 0.95f);
            BurstFx(_gameSim.Player.Position, SourceDotColor("orange"), 34, 520, 0.78f, 1.08f);
            var normalDamage = 120f + _time * 0.4f;
            var enemySnapshot = CaptureEnemyEffectSnapshot(out var enemySnapshotCount);
            try
            {
                for (var target = 0; target < enemySnapshotCount; target++)
                {
                    var snapshot = enemySnapshot[target];
                    if (!IsLiveEnemyEffectTarget(snapshot)) continue;
                    var enemy = snapshot.State;
                    var damage = enemy.Elite
                        ? enemy.MaxHealth * 0.2f
                        : Mathf.Max(normalDamage, enemy.MaxHealth * 0.72f);
                    // Source detonateBomb uses the fixed (0, -1) direction for
                    // knockback/blocking; it is not aimed from the player.
                    ApplyEnemyDamage(snapshot.Slot, damage, Vector2.down, 260, false);
                }
            }
            finally
            {
                ReleaseEnemyEffectSnapshot(enemySnapshot);
            }
            EnsureBossOrderEntries();
            for (var bossOrder = 0; bossOrder < _gameSim.BossOrderCount; bossOrder++)
            {
                var index = _gameSim.BossOrder[bossOrder];
                // Browser detonateBomb skips bosses while their intro
                // telegraph is active; they become damageable only after the
                // intro state resolves.
                if (_gameSim.Bosses[index].Active && _gameSim.Bosses[index].State != 4)
                    ApplyBossDamage(index, _gameSim.Bosses[index].MaxHealth * 0.04f);
            }
            // Browser detonateBomb() emits the final flash, shake, freeze,
            // cue, and toast after all enemy and boss damage side effects.
            _amberFlash = 1f;
            AddCameraShake(0.96f);
            TriggerFreeze(0.12f);
            _audio?.Play(ProceduralAudio.Cue.Bomb, 0.9f);
            // Pull the track out from under the blast so the detonation lands in
            // the music as well as the SFX.
            _music?.DuckForBomb();
            ShowArenaToast("Bomb detonated", 2.5f);
        }

        private void EndRun()
        {
            if (_gameOver) return;
            _revivePending = false;
            _gameSim.Player.DyingTimer = 0;
            _gameOver = true;
            _gameOverScroll = Vector2.zero;
            _paused = true;
            _overclock.Reset();
            _magnetTarget = 0f;
            _magnetIntensity = 0f;
            _music?.ResetReactiveState();
            _audio?.StopPad();
            // Browser endRun() records the terminal state before exporting the
            // game-over report, including the final frame sample.
            RecordTelemetrySample(Mathf.Max(0.0001f, _debugFrameEmaMs / 1000f));
            SaveRun();
            if (_ui != null)
            if (_ui != null)
            {
                var summary = GameOverSummaryBuilder.Build(
                    victory: false,
                    score: CurrentScore(),
                    elapsedSeconds: _time,
                    kills: _kills,
                    eliteKills: _eliteKills,
                    bossKills: _bossKills,
                    level: _level,
                    partsEarned: _partsEarned,
                    isBest: _lastRunIsBest,
                    saved: _lastRunSaved,
                    weaponRanks: _upgradeProgress?.WeaponRanks,
                    weaponDamage: _weaponDamage,
                    totalDamageDealt: _damageDealt,
                    buildChips: BuildRecapChips());
                _ui.GameOver?.Show(summary);
            }
        }

        private static ProceduralAudio.Cue DefeatCueFor(int revivesRemaining)
        {
            return revivesRemaining > 0
                ? ProceduralAudio.Cue.Boss
                : ProceduralAudio.Cue.GameOver;
        }

        private static int AddCounter(int current, int amount)
        {
            var total = (long)Math.Max(0, current) + Math.Max(0, amount);
            return (int)Math.Min(999_999_999L, total);
        }

        private int CurrentScore()
        {
            return Mathf.FloorToInt(_score + _time * 5f + (_level - 1) * 35f + 0.5f);
        }

        private static WorkshopEntry[] BuildRankEntries(string[] ids, int[] ranks)
        {
            if (ids == null || ranks == null) return Array.Empty<WorkshopEntry>();
            var result = new List<WorkshopEntry>();
            for (var index = 0; index < Mathf.Min(ids.Length, ranks.Length); index++)
            {
                if (ranks[index] <= 0) continue;
                result.Add(new WorkshopEntry { id = ids[index], rank = ranks[index] });
            }
            return result.ToArray();
        }

        private UnityTelemetryProgress BuildTelemetryProgress()
        {
            var evolved = new List<string>();
            if (_upgradeProgress?.Evolved != null)
            {
                for (var index = 0; index < Mathf.Min(_upgradeProgress.Evolved.Length, ContentCatalog.Weapons.Length); index++)
                    if (_upgradeProgress.Evolved[index]) evolved.Add(ContentCatalog.Weapons[index].Id);
            }
            return new UnityTelemetryProgress
            {
                weapons = BuildTelemetryRanks(WeaponIds(), _upgradeProgress?.WeaponRanks),
                supports = BuildTelemetryRanks(SupportIds(), _upgradeProgress?.SupportRanks),
                late = BuildTelemetryRanks(LateIds(), _upgradeProgress?.LateRanks),
                evolved = evolved.ToArray(),
            };
        }

        private static UnityTelemetryNamedValue[] BuildTelemetryRanks(string[] ids, int[] ranks)
        {
            if (ids == null || ranks == null) return Array.Empty<UnityTelemetryNamedValue>();
            var result = new List<UnityTelemetryNamedValue>();
            for (var index = 0; index < Mathf.Min(ids.Length, ranks.Length); index++)
            {
                if (ranks[index] <= 0) continue;
                result.Add(new UnityTelemetryNamedValue { id = ids[index], value = ranks[index] });
            }
            return result.ToArray();
        }

        private float TelemetryQualityValue()
        {
            return _qualityPreset.Detail;
        }

        private static string[] SupportIds()
        {
            var ids = new string[ExtendedCatalog.SupportCount];
            for (var index = 0; index < ids.Length; index++) ids[index] = ExtendedCatalog.AllSupports()[index].Id;
            return ids;
        }

        private static string[] LateIds()
        {
            var ids = new string[ContentCatalog.LateUpgrades.Length];
            for (var index = 0; index < ids.Length; index++) ids[index] = ContentCatalog.LateUpgrades[index].Id;
            return ids;
        }

        private WorkshopEntry[] BuildEvolvedEntries()
        {
            if (_upgradeProgress?.Evolved == null) return Array.Empty<WorkshopEntry>();
            var result = new List<WorkshopEntry>();
            for (var index = 0; index < Mathf.Min(ContentOrder.Weapons.Length, _upgradeProgress.Evolved.Length); index++)
            {
                if (!_upgradeProgress.Evolved[index]) continue;
                result.Add(new WorkshopEntry { id = ContentCatalog.Weapons[index].Id, rank = 1 });
            }
            return result.ToArray();
        }

        private void ApplyLevelRecovery()
        {
            var recovery = WorkshopRank("recovery") * 3;
            if (recovery > 0)
                _gameSim.Player.Health = Mathf.Min(_gameSim.Player.MaxHealth, _gameSim.Player.Health + recovery);
        }

        private UpgradeOptionDefinition[] RollLevelOptions()
        {
            var options = UpgradeRules.RollProgressionOptions(_upgradeProgress, _gameSim.Rng, 3);
            if (_gameSim.Player.Health >= _gameSim.Player.MaxHealth * 0.45f) return options;

            var repair = new UpgradeOptionDefinition
            {
                Id = "repair",
                TargetId = "repair",
                Kind = UpgradeOptionKind.Repair,
                Name = "Field Repair",
                Description = "Restore 35 integrity now",
                CurrentRank = 0,
                NextRank = 1,
                MaxRank = 1,
                Accent = "#fb7185",
                Weight = 0,
            };
            var result = new List<UpgradeOptionDefinition>(options);
            var replaceIndex = -1;
            for (var index = result.Count - 1; index >= 0; index--)
            {
                if (result[index].Kind != UpgradeOptionKind.Evolution)
                {
                    replaceIndex = index;
                    break;
                }
            }

            if (result.Count < 3) result.Add(repair);
            else if (replaceIndex >= 0) result[replaceIndex] = repair;
            return result.ToArray();
        }

        private void SelectLevelOption(int index)
        {
            if (!_levelUpActive || _levelOptions == null || index < 0 || index >= _levelOptions.Length) return;
            var option = _levelOptions[index];
            var previousMaxHealth = _gameSim.Player.MaxHealth;
            var previousSlotLimit = UpgradeRules.WeaponSlotLimit(_upgradeProgress);
            if (!UpgradeRules.Apply(_upgradeProgress, option)) return;
            var expandedWeaponSlots = option.Kind == UpgradeOptionKind.Weapon &&
                UpgradeRules.WeaponSlotLimit(_upgradeProgress) > previousSlotLimit;
            if (option.Kind == UpgradeOptionKind.Evolution)
            {
                BeginEvolutionReveal(option);
                _audio?.Play(ProceduralAudio.Cue.Evolution);
                TriggerFreeze(0.12f);
                _cyanFlash = 0.85f;
                AddCameraShake(0.55f);
                SpawnRingWave(_gameSim.Player.Position, 26f, 680f, 0.72f, new Color(0.35f, 0.95f, 1f, 0.84f));
                SpawnRingWave(_gameSim.Player.Position, 14f, 430f, 0.52f, new Color(0.35f, 0.95f, 1f, 0.72f));
                BurstFx(_gameSim.Player.Position, SourceDotColor("white"), 18, 300, 0.65f, 0.9f);
                BurstFx(_gameSim.Player.Position, EvolutionAccent(option.TargetId), 24, 390, 0.78f, 1f);
            }
            else if (option.Kind == UpgradeOptionKind.Repair)
            {
                _gameSim.Player.Health = Mathf.Min(_gameSim.Player.MaxHealth, _gameSim.Player.Health + 35f);
            }
            if (expandedWeaponSlots)
            {
                ShowArenaToast("Fourth weapon slot unlocked", 2.5f, ToastKind.Reward);
                SpawnRingWave(_gameSim.Player.Position, 16f, 360f, 0.46f,
                    new Color(0.133f, 0.827f, 0.933f, 0.8f));
                BurstFx(_gameSim.Player.Position, SourceDotColor("cyan"),
                    12, 220, 0.42f, 0.7f);
            }
            _telemetry.RecordUpgrade((float)_time, _level, option.TargetId, option.Kind.ToString(), BuildTelemetryProgress());
            _pistolRank = _upgradeProgress.WeaponRanks[0];
            _calibrationRank = SupportRank("calibration");
            RecalculatePlayerStats(false);
            // Browser applyUpgrade() explicitly restores the max-health delta
            // after recalculate() when Plating or Frame raises the cap.
            if (_gameSim.Player.MaxHealth > previousMaxHealth)
            {
                _gameSim.Player.Health = Mathf.Min(
                    _gameSim.Player.MaxHealth,
                    _gameSim.Player.Health + (_gameSim.Player.MaxHealth - previousMaxHealth));
            }
            _levelOptions = null;
            _levelUpActive = false;
            _levelUpPromptOpenedAt = -1f;
            _paused = false;
            _targetTimeScale = 1;
            // Browser applyUpgrade() plays the pickup cue for every
            // non-evolution choice after the upgrade is committed.
            if (option.Kind != UpgradeOptionKind.Evolution)
                _audio?.Play(ProceduralAudio.Cue.Pickup, 1f);
        }

        private void RerollLevelOptions()
        {
            if (!_levelUpActive || _rerollsRemaining <= 0 || _upgradeProgress == null) return;
            var previous = _levelOptions == null
                ? string.Empty
                : string.Join("|", Array.ConvertAll(_levelOptions, option => option.Id));
            _rerollsRemaining--;
            var next = RollLevelOptions();
            for (var attempt = 0; attempt < 3 && next.Length > 0; attempt++)
            {
                var fingerprint = string.Join("|", Array.ConvertAll(next, option => option.Id));
                if (fingerprint != previous) break;
                next = RollLevelOptions();
            }
            _levelOptions = next;
            // The screen remains on UIScreen.LevelUp during a reroll, so the
            // normal screen reconciliation does not rebuild its cards. Push the
            // newly rolled options directly to the visible view instead.
            _ui?.LevelUp?.ShowUpgrades(
                BuildUpgradeCards(_levelOptions),
                _rerollsRemaining,
                SelectLevelOption);
            _levelUpPromptOpenedAt = Time.realtimeSinceStartup;
            _audio?.Play(ProceduralAudio.Cue.Ui, 0.95f);
            SetMenuNotice(_rerollsRemaining > 0 ? "Upgrade options rerolled." : "Upgrade options rerolled. No rerolls left.");
        }

        private int SupportRank(string id)
        {
            if (_upgradeProgress == null) return 0;
            for (var index = 0; index < ExtendedCatalog.SupportCount; index++)
            {
                if (ExtendedCatalog.AllSupports()[index].Id == id) return _upgradeProgress.SupportRanks[index];
            }

            return 0;
        }

        private int LateRank(string id)
        {
            if (_upgradeProgress == null) return 0;
            for (var index = 0; index < ContentCatalog.LateUpgrades.Length; index++)
            {
                if (ContentCatalog.LateUpgrades[index].Id == id) return _upgradeProgress.LateRanks[index];
            }

            return 0;
        }

        private void RecalculatePlayerStats(bool repairOnUpgrade)
        {
            var plating = SupportRank("plating");
            var frame = LateRank("frame");
            _gameSim.Player.MaxHealth = (float)ContentCatalog.Operative.MaxHealth + _workshopIntegrity * 5 + plating * 20 + frame * 8;
            _damageMultiplier = Mathf.Pow(1.12f, SupportRank("calibration")) *
                (1 + _workshopPower * 0.04f) * Mathf.Pow(1.05f, LateRank("output"));
            _cooldownMultiplier = Mathf.Pow(0.92f, SupportRank("cycling")) *
                Mathf.Pow(0.97f, LateRank("cooling")) *
                (1f - WorkshopRank("arsenal") * 0.03f);
            _moveSpeedMultiplier = Mathf.Pow(1.08f, SupportRank("mobility")) *
                (1 + _workshopMobility * 0.03f);
            _pickupRadius = (float)ContentCatalog.Operative.PickupRadius *
                Mathf.Pow(1.25f, SupportRank("collector"));
            _areaMultiplier = Mathf.Pow(1.12f, SupportRank("amplifier"));
            _critChance = Mathf.Clamp01(
                0.05f + SupportRank("optics") * 0.06f + WorkshopRank("precision") * 0.02f);
            // Browser recalculate() clamps an over-cap health value but does
            // not heal the player when Plating or Frame raises max health.
            _gameSim.Player.Health = Mathf.Min(_gameSim.Player.Health, _gameSim.Player.MaxHealth);
        }

        private static void ResizeOwnedChipViews(
            Image[] backgrounds,
            Text[] ranks,
            bool narrow,
            float width,
            Vector2 firstPosition,
            bool rightAligned,
            float viewportHeight)
        {
            var rows = OwnedUpgradeChipRows(viewportHeight);
            var visibleIndex = 0;
            for (var index = 0; index < backgrounds.Length; index++)
            {
                if (backgrounds[index] == null) continue;
                var chipWidth = width;
                if (narrow && backgrounds[index].enabled && ranks != null && index < ranks.Length && ranks[index] != null)
                {
                    chipWidth = BuildChipHudNarrowWidth(ranks[index].preferredWidth);
                    ConfigureOwnedUpgradeRankLayout(ranks[index], true, chipWidth - 35f);
                }
                else if (!narrow && ranks != null && index < ranks.Length && ranks[index] != null)
                {
                    ConfigureOwnedUpgradeRankLayout(ranks[index], false, 42f);
                }
                backgrounds[index].rectTransform.sizeDelta = new Vector2(chipWidth, 27f);
                var position = firstPosition;
                if (!backgrounds[index].enabled)
                {
                    backgrounds[index].rectTransform.anchoredPosition = position;
                    continue;
                }
                var column = visibleIndex / rows;
                var row = visibleIndex % rows;
                position.x += (rightAligned ? -1f : 1f) * column * (width + 5f);
                position.y -= row * 32f;
                backgrounds[index].rectTransform.anchoredPosition = position;
                visibleIndex++;
            }
        }

        private static void SetChipLabelVisibility(Text[] labels, Image[] backgrounds, bool visible)
        {
            for (var index = 0; index < labels.Length; index++)
                if (labels[index] != null)
                    labels[index].enabled = visible && backgrounds[index] != null && backgrounds[index].enabled;
        }

        private static void SetBuildChipView(
            Image background,
            Image accentBar,
            RawImage icon,
            Text name,
            Text rank,
            bool active,
            string id,
            string label,
            int currentRank,
            int maxRank,
            Color accent,
            bool showMaxRank,
            bool evolved)
        {
            if (background != null)
            {
                background.enabled = active;
                background.color = new Color(
                    0.02f,
                    0.035f,
                    0.063f,
                    BuildChipHudBackgroundAlpha(showMaxRank, evolved));
                var border = background.transform.Find("Chip Border")?.GetComponent<Image>();
                if (border != null)
                {
                    border.color = new Color(
                        accent.r,
                        accent.g,
                        accent.b,
                        evolved ? 0.70f : 0.35f);
                    border.enabled = BuildChipHudUsesFullBorder(active, showMaxRank);
                }
                var rankBackground = background.transform.Find("Rank Background")?.GetComponent<Image>();
                if (rankBackground != null)
                {
                    rankBackground.color = new Color(
                        accent.r,
                        accent.g,
                        accent.b,
                        evolved ? 1f : 0.13f);
                    rankBackground.enabled = active && !showMaxRank;
                }
            }
            if (accentBar != null)
            {
                accentBar.enabled = BuildChipHudUsesAccentBar(active, showMaxRank, evolved);
                accentBar.color = accent;
                accentBar.rectTransform.sizeDelta = new Vector2(
                    BuildChipHudAccentWidth(evolved),
                    Mathf.Max(11f, background == null ? 19f : background.rectTransform.sizeDelta.y - 8f));
            }
            if (icon != null)
            {
                icon.enabled = active;
                if (active)
                {
                    icon.texture = BuildChipIconTexture();
                    icon.uvRect = BuildChipIconUv(id);
                    icon.color = accent;
                    icon.rectTransform.sizeDelta = new Vector2(
                        BuildChipHudIconSize(showMaxRank),
                        BuildChipHudIconSize(showMaxRank));
                }
            }
            if (name != null)
            {
                name.enabled = active;
                if (active) name.text = label;
                name.color = BuildChipHudLabelColor(showMaxRank);
            }
            if (rank != null)
            {
                rank.enabled = active;
                if (active)
                    rank.text = showMaxRank ? $"{currentRank}/{maxRank}" : currentRank.ToString();
                rank.color = evolved ? Color.white : accent;
            }
        }

        private void CheckMilestones()
        {
            CheckKillMilestone();
            CheckScoreMilestone();
        }

        private void CheckKillMilestone()
        {
            var killCrossing = MilestoneRules.Crossed(
                MilestoneRules.KillMilestones,
                _killMilestoneIndex,
                _kills);
            _killMilestoneIndex = killCrossing.Index;
            if (killCrossing.Value.HasValue) ShowMilestoneToast("kills", killCrossing.Value.Value);
        }

        private void CheckScoreMilestone()
        {
            var scoreCrossing = MilestoneRules.Crossed(
                MilestoneRules.ScoreMilestones,
                _scoreMilestoneIndex,
                CurrentScore());
            _scoreMilestoneIndex = scoreCrossing.Index;
            if (scoreCrossing.Value.HasValue) ShowMilestoneToast("score", scoreCrossing.Value.Value);
        }

        private void ObserveTelemetryFrame(float frameDt)
        {
            if (_paused || _gameOver) return;
            _telemetry.ObserveFrame(
                ArenaIdName(_arenaId),
                TelemetryFpsForFrame(frameDt),
                frameDt * 1000f,
                ActiveEnemies(),
                ActiveBullets() + ActiveHostileShots(),
                ActivePickups());
        }

        private void ToggleMute()
        {
            if (_audio == null) return;
            _audio.SetMuted(!_audio.Muted);
            _music?.SetMuted(_audio.Muted);
            _audio.Play(ProceduralAudio.Cue.Ui, _audio.Muted ? 0.86f : 1.02f);
            SetMenuNotice(_audio.Muted ? "Audio muted." : "Audio unmuted.");
            // Keeps the corner control and the settings row in step when mute is
            // toggled from the keyboard rather than from either control.
            _ui?.RefreshMuteGlyph();
        }

        private void ApplyQualityPreset(QualityPreset preset)
        {
            _qualityPreset = preset;
            _musicPerimeter?.Configure(
                unchecked((int)_runSeed),
                preset.Detail,
                _saveData?.settings != null && _saveData.settings.reducedMotion);
            ApplyRenderResolution();
            var particleLimit = SourceParticleLimit(preset.ParticleScale);
            if (_fx != null)
            {
                var main = _fx.main;
                main.maxParticles = particleLimit;
            }
            TrimCosmeticBudgets(particleLimit, preset.FloaterScale, preset.DeathGhosts);
        }

        private void TrimCosmeticBudgets(int particleLimit, float floaterScale, bool keepDeathGhosts)
        {
            // The browser drops the newest cosmetic entries immediately when a
            // lower quality tier takes effect. Unity keeps some of those views
            // outside its ParticleSystem, so trim the shared visual budget and
            // the pooled floater/ghost arrays explicitly as well.
            if (_fx != null)
            {
                var particleCount = _fx.GetParticles(_fxParticleScratch);
                if (particleCount > particleLimit)
                    _fx.SetParticles(_fxParticleScratch, particleLimit);
                TrimSourceParticleViews(_fx.particleCount);
            }

            while (ActiveFxVisualCount() > particleLimit)
            {
                var removed = false;
                for (var index = _fxSim.RingWaves.Length - 1; index >= 0; index--)
                {
                    if (!_fxSim.RingWaves[index].Active) continue;
                    var wave = _fxSim.RingWaves[index];
                    wave.Active = false;
                    _fxSim.RingWaves[index] = wave;
                    RemoveSourceFxOrder(SourceFxKind.RingWave, index);
                    Hide(_ringWaveViews[index]);
                    Hide(_ringWaveGlowViews[index]);
                    Hide(_ringWaveSpriteViews[index]);
                    removed = true;
                    break;
                }
                if (removed) continue;

                for (var index = _fxSim.MeteorShards.Length - 1; index >= 0; index--)
                {
                    if (!_fxSim.MeteorShards[index].Active) continue;
                    var shard = _fxSim.MeteorShards[index];
                    shard.Active = false;
                    _fxSim.MeteorShards[index] = shard;
                    RemoveSourceFxOrder(SourceFxKind.MeteorShard, index);
                    Hide(_meteorShardViews[index]);
                    removed = true;
                    break;
                }
                if (!removed) break;
            }

            var floaterLimit = Mathf.Max(8, Mathf.RoundToInt(MaxFloaters * floaterScale));
            var activeFloaters = 0;
            for (var index = 0; index < _floaters.Length; index++)
                if (_floaters[index].Active) activeFloaters++;
            for (var index = _floaters.Length - 1; index >= 0 && activeFloaters > floaterLimit; index--)
            {
                if (!_floaters[index].Active) continue;
                var floater = _floaters[index];
                floater.Active = false;
                _floaters[index] = floater;
                RemoveFloaterOrder(index);
                Hide(_floaterViews[index]);
                activeFloaters--;
            }

            if (!keepDeathGhosts)
            {
                for (var index = 0; index < _deathGhosts.Length; index++)
                {
                    if (!_deathGhosts[index].Active) continue;
                    var ghost = _deathGhosts[index];
                    ghost.Active = false;
                    _deathGhosts[index] = ghost;
                    RemoveDeathGhostOrder(index);
                    Hide(_deathGhostViews[index]);
                }
            }
        }

        private int ActiveFxVisualCount()
        {
            var count = _fx == null ? 0 : _fx.particleCount;
            for (var index = 0; index < _fxSim.RingWaves.Length; index++)
                if (_fxSim.RingWaves[index].Active) count++;
            for (var index = 0; index < _fxSim.MeteorShards.Length; index++)
                if (_fxSim.MeteorShards[index].Active) count++;
            return count;
        }

        private void OnGUI()
        {
            var oldSkin = GUI.skin;
            var oldColor = GUI.color;
            var oldMatrix = GUI.matrix;

            try
            {
                // Every menu, overlay, notice and diagnostic panel now lives in the
                // retained-mode uGUI layer (VoidFall.UI), driven from SyncUiScreen.
                // What stays here is the in-run director telegraph: chevrons and an
                // edge pulse that are screen-space gameplay feedback rather than
                // interface chrome, and which have to draw over the world without
                // participating in menu navigation.
                //
                // IMGUI always composites after screen-space canvases, so nothing
                // else may be drawn here or it would cover the interface.
                if (_menuPage == MenuPage.None) DrawScreenWarnings();
            }
            finally
            {
                GUI.skin = oldSkin;
                GUI.color = oldColor;
                GUI.matrix = oldMatrix;
            }
        }

        private static bool RusherWarningVisible(
            bool playing,
            bool directorWarned,
            bool directorActive,
            string eventId)
        {
            // The browser keeps the warning visible through the active rusher
            // stream, but only while the run is actually playing.
            return playing && directorWarned && eventId == "rushers";
        }

        private static bool PressureBorderVisible(
            bool playing,
            bool directorWarned,
            bool directorActive,
            string eventId)
        {
            if (!playing || !directorWarned) return false;
            return eventId == "swarm" || eventId == "encircle";
        }

        private static Texture2D RusherChevronTexture()
        {
            if (_rusherChevronTexture != null) return _rusherChevronTexture;

            const int width = 40;
            const int height = 20;
            _rusherChevronTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "VoidFall Rusher Chevrons",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color[width * height];
            var first = new[]
            {
                new Vector2(20f, 10f),
                new Vector2(6f, 1f),
                new Vector2(6f, 19f),
            };
            var second = new[]
            {
                new Vector2(33f, 10f),
                new Vector2(19f, 1f),
                new Vector2(19f, 19f),
            };
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var point = new Vector2(x + 0.5f, y + 0.5f);
                    if (PointInTriangle(point, first) || PointInTriangle(point, second))
                        pixels[y * width + x] = Color.white;
                }
            }
            _rusherChevronTexture.SetPixels(pixels);
            _rusherChevronTexture.Apply(false, true);
            return _rusherChevronTexture;
        }

        private static bool PointInTriangle(Vector2 point, Vector2[] triangle)
        {
            var a = triangle[0];
            var b = triangle[1];
            var c = triangle[2];
            var ab = Cross(b - a, point - a);
            var bc = Cross(c - b, point - b);
            var ca = Cross(a - c, point - c);
            return (ab >= 0 && bc >= 0 && ca >= 0) ||
                (ab <= 0 && bc <= 0 && ca <= 0);
        }

        private static float Cross(Vector2 left, Vector2 right)
        {
            return left.x * right.y - left.y * right.x;
        }

        private static float RoundedRectSignedDistance(
            float x,
            float y,
            float halfWidth,
            float halfHeight,
            float radius)
        {
            var qx = Mathf.Abs(x) - (halfWidth - radius);
            var qy = Mathf.Abs(y) - (halfHeight - radius);
            var outsideX = Mathf.Max(qx, 0f);
            var outsideY = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY) +
                Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        private static Texture2D RoundedGradientGuiTexture(
            Color topColor,
            Color bottomColor,
            Color borderColor,
            int width,
            int height,
            float radius,
            string textureName,
            float angleDegrees)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = textureName,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color[width * height];
            var halfWidth = (width - 1) * 0.5f;
            var halfHeight = (height - 1) * 0.5f;
            var safeRadius = Mathf.Min(radius, Mathf.Min(halfWidth, halfHeight));
            var angleRadians = angleDegrees * Mathf.Deg2Rad;
            var gradientDirection = new Vector2(
                Mathf.Sin(angleRadians),
                Mathf.Cos(angleRadians));
            var gradientExtent = Mathf.Abs(gradientDirection.x) * halfWidth
                + Mathf.Abs(gradientDirection.y) * halfHeight;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var projection = (x - halfWidth) * gradientDirection.x
                        + (y - halfHeight) * gradientDirection.y;
                    var gradient = gradientExtent > 0f
                        ? Mathf.InverseLerp(-gradientExtent, gradientExtent, projection)
                        : 0.5f;
                    var fillColor = Color.Lerp(bottomColor, topColor, gradient);
                    var qx = Mathf.Abs(x - halfWidth) - (halfWidth - safeRadius);
                    var qy = Mathf.Abs(y - halfHeight) - (halfHeight - safeRadius);
                    var outsideX = Mathf.Max(qx, 0f);
                    var outsideY = Mathf.Max(qy, 0f);
                    var signedDistance = Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY)
                        + Mathf.Min(Mathf.Max(qx, qy), 0f)
                        - safeRadius;
                    var coverage = Mathf.Clamp01(0.5f - signedDistance);
                    var borderMix = Mathf.Clamp01((signedDistance + 1.5f) / 1.5f);
                    var color = Color.Lerp(fillColor, borderColor, borderMix);
                    color.a *= coverage;
                    pixels[y * width + x] = color;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D RoundedGradientGuiTexture(
            Color topColor,
            Color bottomColor,
            Color borderColor,
            int width,
            int height,
            float radius,
            string textureName)
        {
            return RoundedGradientGuiTexture(
                topColor,
                bottomColor,
                borderColor,
                width,
                height,
                radius,
                textureName,
                0f);
        }

        private static void SetGuiStyleState(GUIStyleState state, Texture2D background, Color textColor)
        {
            state.background = background;
            state.textColor = textColor;
        }

        private static int ResultMetricColumns(float width)
        {
            return width <= 720f ? 2 : 3;
        }

        private static float BrowserMetricGridGap()
        {
            return 8f;
        }

        private static float BrowserMetricContentGap()
        {
            return 5f;
        }

        private static float BrowserMetricMinHeight()
        {
            return 66f;
        }

        private static int BrowserMetricLabelFontSize()
        {
            // React computes the 10px source label at 11.5px through the
            // root UI scale; 12px is the nearest IMGUI integer size.
            return 12;
        }

        private static int BrowserMetricValueFontSize()
        {
            // React computes the 17px source value at 19.55px.
            return 20;
        }

        private struct BuildChipRecord
        {
            public string Id;
            public string Name;
            public int Rank;
            public string Accent;
            public bool Evolved;
        }

        private readonly struct ResultMetric
        {
            public readonly string Label;
            public readonly string Value;

            public ResultMetric(string label, string value)
            {
                Label = label;
                Value = value;
            }
        }

        private float BuildChipWidth(string name)
        {
            var labelWidth = ResultBuildChipNameStyle().CalcSize(
                new GUIContent(name ?? string.Empty)).x;
            return Mathf.Clamp(
                BuildChipWidthFromLabelWidth(labelWidth),
                BuildChipFixedChromeWidth(),
                260f);
        }

        private static float BuildChipWidthFromLabelWidth(float labelWidth)
        {
            return BuildChipFixedChromeWidth() + Mathf.Max(0f, labelWidth);
        }

        private static float BuildChipFixedChromeWidth()
        {
            // React layout: 13px icon + two 6px gaps + 15px rank badge +
            // 14px horizontal padding + 2px border.
            return 56f;
        }

        private static float BuildChipWidthEstimate(string name)
        {
            var characterCount = Mathf.Max(1, name == null ? 0 : name.Length);
            return Mathf.Clamp(
                BuildChipFixedChromeWidth() + characterCount * 6f,
                BuildChipFixedChromeWidth(),
                260f);
        }

        private static int BuildChipRowCount(float availableWidth, IList<string> names)
        {
            var safeWidth = Mathf.Max(1f, availableWidth);
            var rowWidth = 0f;
            var rows = 0;
            for (var index = 0; index < names.Count; index++)
            {
                var chipWidth = Mathf.Min(safeWidth, BuildChipWidthEstimate(names[index]));
                var gap = rowWidth > 0f ? 6f : 0f;
                if (rowWidth <= 0f || rowWidth + gap + chipWidth > safeWidth)
                {
                    rows++;
                    rowWidth = chipWidth;
                }
                else
                {
                    rowWidth += gap + chipWidth;
                }
            }
            return rows;
        }

        private GUIStyle ResultBuildChipStyle(string accentHex, bool evolved)
        {
            var accent = ParseColor(accentHex, new Color(0.4f, 0.9f, 1f, 1f));
            var key = ColorUtility.ToHtmlStringRGBA(accent) + (evolved ? "/evolved" : "/normal");
            if (_resultBuildChipStyleCache.TryGetValue(key, out var cached)) return cached;

            var style = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(7, 7, 4, 4),
                margin = new RectOffset(0, 0, 0, 0),
                border = new RectOffset(5, 5, 5, 5),
                fixedHeight = BuildChipMinHeight(),
            };
            var border = new Color(accent.r, accent.g, accent.b, BuildChipBorderAlpha(evolved));
            var background = RoundedGradientGuiTexture(
                new Color(0.0196f, 0.0353f, 0.0627f, 0.62f),
                new Color(0.0196f, 0.0353f, 0.0627f, 0.62f),
                border,
                96,
                32,
                5f,
                "VoidFall Result Build Chip " + key);
            SetGuiStyleState(style.normal, background, new Color(0.796f, 0.835f, 0.882f, 1f));
            SetGuiStyleState(style.hover, background, new Color(0.796f, 0.835f, 0.882f, 1f));
            _resultBuildChipStyleCache[key] = style;
            return style;
        }

        private GUIStyle ResultBuildChipNameStyle()
        {
            if (_resultBuildChipNameStyle == null)
            {
                _resultBuildChipNameStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    font = BrowserBodyFont(),
                    fontSize = BuildChipLabelFontSize(),
                    wordWrap = false,
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0),
                };
                _resultBuildChipNameStyle.normal.textColor = new Color(0.796f, 0.835f, 0.882f, 1f);
            }
            return _resultBuildChipNameStyle;
        }

        private GUIStyle ResultBuildChipRankStyle(string accentHex)
        {
            var accent = ParseColor(accentHex, new Color(0.4f, 0.9f, 1f, 1f));
            var key = ColorUtility.ToHtmlStringRGBA(accent);
            if (_resultBuildChipRankStyleCache.TryGetValue(key, out var cached)) return cached;

            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                font = BrowserDisplayFont(),
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                fixedWidth = BuildChipRankSize(),
                fixedHeight = BuildChipRankSize(),
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
            };
            var rankBackground = new Color(accent.r, accent.g, accent.b, BuildChipRankBackgroundAlpha());
            var background = RoundedGradientGuiTexture(
                rankBackground,
                rankBackground,
                Color.clear,
                16,
                15,
                3f,
                "VoidFall Result Build Chip Rank " + key);
            SetGuiStyleState(style.normal, background, accent);
            SetGuiStyleState(style.hover, background, accent);
            _resultBuildChipRankStyleCache[key] = style;
            return style;
        }

        private GUIStyle ResultActionLabelStyle(bool primary)
        {
            if (primary && _resultActionPrimaryLabelStyle != null) return _resultActionPrimaryLabelStyle;
            if (!primary && _resultActionSecondaryLabelStyle != null) return _resultActionSecondaryLabelStyle;

            var style = new GUIStyle(ResultActionButtonStyle(primary))
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(0, 0, 0, 0),
            };
            if (primary) _resultActionPrimaryLabelStyle = style;
            else _resultActionSecondaryLabelStyle = style;
            return style;
        }

        private static float ResultActionActiveScale()
        {
            return 0.988f;
        }

        private static Texture2D PrimaryActionOuterShadowTexture(
            int buttonWidth,
            int buttonHeight,
            bool hovered)
        {
            buttonWidth = Mathf.Max(16, Mathf.RoundToInt(buttonWidth / 16f) * 16);
            buttonHeight = Mathf.Max(16, Mathf.RoundToInt(buttonHeight / 16f) * 16);
            var margin = PrimaryActionShadowTextureMargin(hovered);
            var key = buttonWidth + "x" + buttonHeight + "-" + (hovered ? "hover" : "normal");
            if (_primaryActionOuterShadowCache.TryGetValue(key, out var cached)) return cached;

            var width = buttonWidth + margin * 2;
            var height = buttonHeight + margin * 2;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "VoidFall Primary Action Outer Shadow " + key,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color[width * height];
            var halfWidth = buttonWidth * 0.5f;
            var halfHeight = buttonHeight * 0.5f;
            var centerX = margin + halfWidth;
            var centerY = margin + halfHeight;
            var blurRadius = PrimaryActionOuterShadowBlurRadius(hovered);
            var blurDenominator = 2f * blurRadius * blurRadius;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var distance = RoundedRectSignedDistance(
                        x - centerX,
                        y - centerY,
                        halfWidth,
                        halfHeight,
                        PrimaryActionShadowCornerRadius());
                    var safeDistance = Mathf.Max(0f, distance);
                    var alpha = distance >= 0f
                        ? PrimaryActionOuterShadowAlpha(hovered) * Mathf.Exp(
                            -(safeDistance * safeDistance) / blurDenominator)
                        : 0f;
                    pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            CacheTextureBounded(_primaryActionOuterShadowCache, key, texture);
            return texture;
        }

        private static Texture2D PrimaryActionInsetShadowTexture(
            int buttonWidth,
            int buttonHeight,
            bool hovered)
        {
            buttonWidth = Mathf.Max(16, Mathf.RoundToInt(buttonWidth / 16f) * 16);
            buttonHeight = Mathf.Max(16, Mathf.RoundToInt(buttonHeight / 16f) * 16);
            var key = buttonWidth + "x" + buttonHeight + "-" + (hovered ? "hover" : "normal");
            if (_primaryActionInsetShadowCache.TryGetValue(key, out var cached)) return cached;

            var texture = new Texture2D(buttonWidth, buttonHeight, TextureFormat.RGBA32, false)
            {
                name = "VoidFall Primary Action Inset Shadow " + key,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color[buttonWidth * buttonHeight];
            var halfWidth = buttonWidth * 0.5f;
            var halfHeight = buttonHeight * 0.5f;
            var centerX = halfWidth;
            var centerY = halfHeight;
            var blurRadius = PrimaryActionInsetShadowBlurRadius(hovered);
            var blurDenominator = 2f * Mathf.Max(1f, blurRadius * 0.45f) * Mathf.Max(1f, blurRadius * 0.45f);
            for (var y = 0; y < buttonHeight; y++)
            {
                for (var x = 0; x < buttonWidth; x++)
                {
                    var distance = RoundedRectSignedDistance(
                        x - centerX,
                        y - centerY,
                        halfWidth,
                        halfHeight,
                        PrimaryActionShadowCornerRadius());
                    var edgeDepth = Mathf.Max(0f, -distance);
                    var alpha = distance <= 0f
                        ? PrimaryActionInsetShadowAlpha(hovered) * Mathf.Exp(
                            -(edgeDepth * edgeDepth) / blurDenominator)
                        : 0f;
                    pixels[y * buttonWidth + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            CacheTextureBounded(_primaryActionInsetShadowCache, key, texture);
            return texture;
        }

        private static float PrimaryActionOuterShadowBlurRadius(bool hovered)
        {
            return hovered ? 30f : 24f;
        }

        private static float PrimaryActionOuterShadowAlpha(bool hovered)
        {
            return hovered ? 0.20f : 0.12f;
        }

        private static float PrimaryActionInsetShadowBlurRadius(bool hovered)
        {
            return hovered ? 24f : 22f;
        }

        private static float PrimaryActionInsetShadowAlpha(bool hovered)
        {
            return hovered ? 0.08f : 0.055f;
        }

        private static float PrimaryActionShadowCornerRadius()
        {
            return 7f;
        }

        private static int PrimaryActionShadowTextureMargin(bool hovered)
        {
            return Mathf.CeilToInt(PrimaryActionOuterShadowBlurRadius(hovered) * 1.5f);
        }

        private static float ResultKickerBottomMargin()
        {
            return 4f;
        }

        private static float ResultTitleBottomMargin()
        {
            return 9f;
        }

        private static float ResultMetricGridMargin()
        {
            return 16f;
        }

        private static float ResultActionIconSize(string iconId)
        {
            return string.Equals(iconId, "rotate-ccw", StringComparison.Ordinal) ? 18f : 17f;
        }

        private static int ResultActionLabelFontSize()
        {
            // Result action text inherits body 16px at the 1.15 UI scale;
            // 18px is the nearest IMGUI integer size.
            return 18;
        }

        private static Color ResultActionPrimaryHoverFill()
        {
            return new Color(10f / 255f, 43f / 255f, 55f / 255f, 0.92f);
        }

        private static Color ResultActionPrimaryHoverBorder()
        {
            return new Color(165f / 255f, 243f / 255f, 252f / 255f, 1f);
        }

        private static Color ResultActionSecondaryHoverFill()
        {
            return new Color(20f / 255f, 32f / 255f, 46f / 255f, 0.82f);
        }

        private static Color ResultActionSecondaryHoverBorder()
        {
            return new Color(103f / 255f, 232f / 255f, 249f / 255f, 0.43f);
        }

        private static float ResultActionIconBoxSize(bool primary, string iconId)
        {
            return primary ? ResultActionIconSize(iconId) + 20f : ResultActionIconSize(iconId);
        }

        private GUIStyle ResultActionPrimaryIconStyle()
        {
            if (_resultActionPrimaryIconStyle == null)
            {
                _resultActionPrimaryIconStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0),
                    border = new RectOffset(5, 5, 5, 5),
                };
                var background = RoundedGradientGuiTexture(
                    new Color(34f / 255f, 211f / 255f, 238f / 255f, 0.08f),
                    new Color(34f / 255f, 211f / 255f, 238f / 255f, 0.08f),
                    new Color(103f / 255f, 232f / 255f, 249f / 255f, 0.30f),
                    38,
                    38,
                    5f,
                    "VoidFall Result Primary Action Icon");
                SetGuiStyleState(_resultActionPrimaryIconStyle.normal, background, Color.white);
                SetGuiStyleState(_resultActionPrimaryIconStyle.hover, background, Color.white);
            }
            return _resultActionPrimaryIconStyle;
        }

        private static float BuildChipMinHeight()
        {
            return 28f;
        }

        private static int BuildChipLabelFontSize()
        {
            // The browser's 10px source value is multiplied by the root
            // --ui-text-scale (1.15), giving a 11.5px computed size. Unity's
            // GUI text API takes integer sizes, so 12px is the nearest match.
            return 12;
        }

        private static float BuildChipRankSize()
        {
            return 15f;
        }

        private static float BuildChipIconSize()
        {
            return 13f;
        }

        private static float BuildChipBorderAlpha(bool evolved)
        {
            return evolved ? 0.62f : 0.32f;
        }

        private static float BuildChipRankBackgroundAlpha()
        {
            return 0.14f;
        }

        private static float ResultBestPulse(float time)
        {
            const float period = 1.6f;
            return 0.5f - 0.5f * Mathf.Cos(time / period * Mathf.PI * 2f);
        }

        private static float ResultBestGlowRadius(float time)
        {
            return Mathf.Lerp(18f, 38f, ResultBestPulse(time));
        }

        private static float ResultBestGlowAlpha(float time)
        {
            return Mathf.Lerp(0.32f, 0.68f, ResultBestPulse(time));
        }

        private static int ResultBestGlowLayerCount()
        {
            return 6;
        }

        private static float ResultBestGlowLayerSpread(float radius, int layer)
        {
            return radius * (layer + 1f) / ResultBestGlowLayerCount();
        }

        private static float ResultBestGlowLayerAlphaFactor(int layer)
        {
            var t = Mathf.Clamp01(layer / Mathf.Max(1f, ResultBestGlowLayerCount() - 1f));
            return Mathf.Lerp(0.12f, 0.012f, t);
        }

        private static Texture2D BuildChipIconTexture()
        {
            if (_buildChipIconTexture != null) return _buildChipIconTexture;

            _buildChipIconTexture = Resources.Load<Texture2D>("VoidFall/BuildChipIconsRaster");
            if (_buildChipIconTexture != null) return _buildChipIconTexture;

            var sprite = Resources.Load<Sprite>("VoidFall/BuildChipIcons");
            if (sprite != null)
            {
                _buildChipIconTexture = sprite.texture;
                return _buildChipIconTexture;
            }

            _buildChipIconTexture = Resources.Load<Texture2D>("VoidFall/BuildChipIcons");
            return _buildChipIconTexture;
        }

        private static Rect BuildChipIconUv(string id)
        {
            var slot = BuildChipIconSlot(id);
            if (slot < 0) return new Rect(0f, 0f, 1f, 1f);

            var column = slot % 5;
            var row = slot / 5;
            return new Rect(
                column / 5f,
                1f - ((row + 1) / 3f),
                1f / 5f,
                1f / 3f);
        }

        private static int BuildChipIconSlot(string id)
        {
            // Atlas order mirrors BuildChipIcons.svg and the browser Lucide map.
            switch (id)
            {
                case "pistol": return 0; // Crosshair
                case "scattergun": return 1; // Aperture
                case "railgun": return 2; // Zap
                case "blades": return 3; // Orbit
                case "arc": return 4; // Sparkles
                case "seeker": return 5; // Rocket
                case "calibration": return 6; // Wrench
                case "cycling": return 7; // Gauge
                case "plating": return 8; // Shield
                case "mobility": return 9; // HeartPulse
                case "collector": return 10; // Magnet
                case "adrenal": return 11; // Flame
                case "amplifier": return 12; // Expand
                case "regenerator": return 13; // HeartPlus
                case "cooling": return 14; // CircleGauge
                case "output": return 0; // Crosshair
                case "frame": return 8; // Shield
                case "optics": return 0; // Crosshair
                case "overload": return 2; // Zap
                case "dodge": return 8; // Shield
                case "scholar": return 4; // Sparkles / insight
                case "fortune": return 10; // Magnet
                case "projectileSpeed": return 5; // Rocket
                case "spatialAwareness": return 12; // Expand
                default: return -1;
            }
        }

        private static string BuildChipGlyph(string id)
        {
            // Last-resort fallback if the SVG resource cannot be imported.
            switch (id)
            {
                case "pistol": return "\u2316"; // Crosshair
                case "scattergun": return "\u25ce"; // Aperture
                case "railgun": return "\u26a1"; // Zap
                case "blades": return "\u25cc"; // Orbit
                case "arc": return "\u2726"; // Sparkles
                case "seeker": return "\u2191"; // Rocket
                case "calibration": return "\u2692"; // Wrench
                case "cycling": return "\u25c9"; // Gauge
                case "plating": return "\u25c7"; // Shield
                case "mobility": return "\u2665"; // HeartPulse
                case "collector": return "\u2328"; // Magnet
                case "optics": return "\u2316"; // Crosshair
                case "overload": return "\u26a1"; // Zap
                case "dodge": return "\u25c7"; // Shield
                case "scholar": return "\u2726"; // Sparkles / insight
                case "fortune": return "\u2328"; // Magnet
                case "projectileSpeed": return "\u2191"; // Rocket
                case "spatialAwareness": return "\u2194"; // Expand
                case "adrenal": return "\u2668"; // Flame
                case "amplifier": return "\u2194"; // Expand
                case "regenerator": return "\u2665"; // HeartPlus
                case "output": return "\u2316"; // Crosshair
                case "cooling": return "\u25c9"; // Gauge
                case "frame": return "\u25c7"; // Shield
                default: return "\u2022";
            }
        }

        private static int BrowserNearestFontSize(float value)
        {
            // CSS keeps the half-pixel value; IMGUI needs an integer. Use
            // conventional half-up rounding instead of Unity's tie-to-even
            // Mathf.RoundToInt behavior.
            return Mathf.FloorToInt(value + 0.5f);
        }

        private void EnsureRerollStyles()
        {
            if (_rerollButtonLabelStyle == null)
            {
                _rerollButtonLabelStyle = new GUIStyle(MenuBodyStyle())
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = RerollLabelFontSize(),
                    fontStyle = FontStyle.Normal,
                    wordWrap = false,
                };
                _rerollKeyStyle = new GUIStyle(MenuBodyStyle())
                {
                    font = BrowserDisplayFont(),
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = RerollKeyFontSize(),
                    fontStyle = FontStyle.Bold,
                    wordWrap = false,
                };
            }
            if (_rerollButtonStyle == null)
            {
                _rerollButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0),
                    border = new RectOffset(7, 7, 7, 7),
                };
                var normal = RoundedGradientGuiTexture(
                    new Color(0.058f, 0.090f, 0.133f, 0.72f),
                    new Color(0.058f, 0.090f, 0.133f, 0.72f),
                    new Color(0.58f, 0.64f, 0.72f, 0.23f),
                    190,
                    46,
                    7f,
                    "VoidFall Reroll Button");
                var hover = RoundedGradientGuiTexture(
                    new Color(0.078f, 0.125f, 0.180f, 0.82f),
                    new Color(0.078f, 0.125f, 0.180f, 0.82f),
                    new Color(0.40f, 0.84f, 0.95f, 0.43f),
                    190,
                    46,
                    7f,
                    "VoidFall Reroll Button Hover");
                var active = RoundedGradientGuiTexture(
                    new Color(0.078f, 0.125f, 0.180f, 0.88f),
                    new Color(0.078f, 0.125f, 0.180f, 0.88f),
                    new Color(0.40f, 0.84f, 0.95f, 0.58f),
                    190,
                    46,
                    7f,
                    "VoidFall Reroll Button Active");
                SetGuiStyleState(_rerollButtonStyle.normal, normal, Color.white);
                SetGuiStyleState(_rerollButtonStyle.hover, hover, Color.white);
                SetGuiStyleState(_rerollButtonStyle.active, active, Color.white);
                SetGuiStyleState(_rerollButtonStyle.focused, hover, Color.white);
            }
            if (_rerollKeycapStyle == null)
            {
                _rerollKeycapStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0),
                    border = new RectOffset(3, 3, 3, 3),
                };
                var keycap = RoundedGradientGuiTexture(
                    new Color(0.008f, 0.024f, 0.071f, 0.45f),
                    new Color(0.008f, 0.024f, 0.071f, 0.45f),
                    new Color(0.58f, 0.64f, 0.72f, 0.30f),
                    20,
                    22,
                    3f,
                    "VoidFall Reroll Keycap");
                SetGuiStyleState(_rerollKeycapStyle.normal, keycap, Color.white);
                SetGuiStyleState(_rerollKeycapStyle.hover, keycap, Color.white);
            }
        }

        private static float RerollRowMargin()
        {
            return 12f;
        }

        private static float RerollKeycapWidth()
        {
            return 20f;
        }

        private static int RerollLabelFontSize()
        {
            return BrowserNearestFontSize(16f * 1.15f);
        }

        private static int RerollKeyFontSize()
        {
            return BrowserNearestFontSize(10f * 1.15f);
        }

        private static Color RerollActionTextColor(bool enabled)
        {
            return enabled
                ? new Color(219f / 255f, 229f / 255f, 238f / 255f, 1f)
                : new Color(102f / 255f, 115f / 255f, 130f / 255f, 1f);
        }

        private static Texture2D RerollIconTexture()
        {
            if (_rerollIconTexture != null) return _rerollIconTexture;
            _rerollIconTexture = Resources.Load<Texture2D>("VoidFall/RerollRotateCcwRaster");
            if (_rerollIconTexture != null) return _rerollIconTexture;
            var sprite = Resources.Load<Sprite>("VoidFall/RerollRotateCcw");
            _rerollIconTexture = sprite != null
                ? sprite.texture
                : Resources.Load<Texture2D>("VoidFall/RerollRotateCcw");
            return _rerollIconTexture;
        }

        private static float CubicBezierEase(
            float x,
            float x1,
            float y1,
            float x2,
            float y2)
        {
            var low = 0f;
            var high = 1f;
            for (var iteration = 0; iteration < 12; iteration++)
            {
                var t = (low + high) * 0.5f;
                if (CubicBezierCoordinate(t, x1, x2) < x)
                    low = t;
                else
                    high = t;
            }
            return CubicBezierCoordinate((low + high) * 0.5f, y1, y2);
        }

        private static float CubicBezierCoordinate(float t, float first, float second)
        {
            var inverse = 1f - t;
            return 3f * inverse * inverse * t * first +
                3f * inverse * t * t * second +
                t * t * t;
        }

        private static Color WithAlpha(Color color, float opacity)
        {
            color.a *= Mathf.Clamp01(opacity);
            return color;
        }

        private static Texture2D ControlIconTexture()
        {
            if (_controlIconTexture != null) return _controlIconTexture;
            _controlIconTexture = Resources.Load<Texture2D>("VoidFall/ControlIconsRaster");
            if (_controlIconTexture != null) return _controlIconTexture;
            var sprite = Resources.Load<Sprite>("VoidFall/ControlIcons");
            _controlIconTexture = sprite != null
                ? sprite.texture
                : Resources.Load<Texture2D>("VoidFall/ControlIcons");
            return _controlIconTexture;
        }

        private static int ControlIconSlot(string iconId)
        {
            switch (iconId)
            {
                case "arrow-left": return 0;
                case "play": return 1;
                case "pause": return 2;
                case "rotate-ccw": return 3;
                case "house": return 4;
                case "volume-2": return 5;
                case "volume-x": return 6;
                case "download": return 7;
                case "heart": return 8;
                case "skull": return 9;
                default: return -1;
            }
        }

        private static Rect ControlIconUv(string iconId)
        {
            var slot = ControlIconSlot(iconId);
            if (slot < 0) return new Rect(0f, 0f, 1f, 1f);
            return new Rect(slot / 10f, 0f, 1f / 10f, 1f);
        }

        private readonly struct RecordMetric
        {
            public RecordMetric(string label, string value)
            {
                Label = label;
                Value = value;
            }

            public string Label { get; }
            public string Value { get; }
        }

        private static string FormatProfileNumber(long value)
        {
            return Math.Max(0, value).ToString("N0");
        }

        private void ResetLocalProgress()
        {
            if (_saveStore == null)
            {
                SetMenuNotice("Progress could not be reset.");
                return;
            }

            var fresh = SaveStore.CreateDefault();
            try
            {
                // Resetting is an explicit destructive request, so it is allowed
                // to overwrite a profile this session could not read.
                _saveStore.Save(fresh, true);
            }
            catch (System.Exception exception)
            {
                Debug.LogError("VoidFall local progress reset failed: " + exception.Message);
                SetMenuNotice("Progress could not be reset.");
                return;
            }

            _saveData = fresh;
            _settingsController.MarkClean();
            _resetProgressArmed = false;
            _resetProgressTimer = 0;
            ApplySettings();
            SetMenuNotice("Local progress reset.");
        }

        private static string SettingQualityOptionLabel(string quality)
        {
            switch (quality)
            {
                case "low": return "Low power";
                case "balanced": return "Balanced";
                case "high": return "High";
                default: return "Auto";
            }
        }

        private static float QuantizeTouchSize(float value)
        {
            const float min = 0.75f;
            const float max = 1.35f;
            const float step = 0.05f;
            var clamped = Mathf.Clamp(value, min, max);
            var stepped = min + Mathf.Floor((clamped - min) / step + 0.5f) * step;
            return Mathf.Clamp(stepped, min, max);
        }

        private static float QuantizeUnitSetting(float value)
        {
            const float step = 0.05f;
            var clamped = Mathf.Clamp01(value);
            return Mathf.Clamp(Mathf.Floor(clamped / step + 0.5f) * step, 0, 1);
        }

        private static string FormatRunTime(int seconds)
        {
            var minutes = Mathf.Max(0, seconds) / 60;
            var remainder = Mathf.Max(0, seconds) % 60;
            return $"{minutes}:{remainder:00}";
        }

        private GUIStyle ResultKickerStyle()
        {
            if (_resultKickerStyle == null)
            {
                _resultKickerStyle = new GUIStyle(GUI.skin.label)
                {
                    font = BrowserDisplayFont(),
                    fontSize = ResultKickerFontSize(),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    fixedHeight = 14f,
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0),
                };
                _resultKickerStyle.normal.textColor = new Color(0.557f, 0.863f, 0.941f, 1f);
            }
            return _resultKickerStyle;
        }

        private GUIStyle ResultTitleStyle()
        {
            if (_resultTitleStyle == null)
            {
                _resultTitleStyle = new GUIStyle(GUI.skin.label)
                {
                    font = BrowserDisplayFont(),
                    fontSize = ResultTitleFontSize(),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    fixedHeight = 33f,
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0),
                };
                _resultTitleStyle.normal.textColor = new Color(0.945f, 0.961f, 0.976f, 1f);
            }
            return _resultTitleStyle;
        }

        private GUIStyle ResultTitleGlowStyle()
        {
            if (_resultTitleGlowStyle == null)
            {
                _resultTitleGlowStyle = new GUIStyle(ResultTitleStyle());
                _resultTitleGlowStyle.normal.textColor = new Color(
                    34f / 255f,
                    211f / 255f,
                    238f / 255f,
                    1f);
            }
            return _resultTitleGlowStyle;
        }

        private static int ResultKickerFontSize()
        {
            // React computes the 10px source value at 11.5px through the
            // root UI scale; 12px is the nearest IMGUI integer size.
            return 12;
        }

        private static int ResultTitleFontSize()
        {
            // React computes the 27px source heading at 31.05px.
            return 31;
        }

        private static float ResultTitleShadowRadius(int ring)
        {
            return ring == 0 ? 12f : 42f;
        }

        private static float ResultTitleShadowAlpha(int ring)
        {
            return ring == 0 ? 0.58f : 0.25f;
        }

        private static int ResultDetailHeaderFontSize()
        {
            // React computes the 10px source detail heading at 11.5px.
            return 12;
        }

        private static float ResultHeadingGap()
        {
            return 9f;
        }

        private GUIStyle ResultDetailHeaderStyle()
        {
            if (_resultDetailHeaderStyle == null)
            {
                _resultDetailHeaderStyle = new GUIStyle(MenuSectionStyle())
                {
                    font = BrowserDisplayFont(),
                    fontSize = ResultDetailHeaderFontSize(),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(0, 0, 0, 0),
                    normal = { textColor = new Color(0.58f, 0.639f, 0.722f, 1f) },
                };
            }
            return _resultDetailHeaderStyle;
        }

        private GUIStyle ResultBestBadgeStyle()
        {
            if (_resultBestBadgeStyle == null)
            {
                _resultBestBadgeStyle = CreateResultBadgeStyle(
                    new Color(0.443f, 0.247f, 0.071f, 0.18f),
                    new Color(0.98f, 0.80f, 0.13f, 0.32f),
                new Color(0.992f, 0.902f, 0.529f, 1f),
                    "VoidFall Result New Best");
            }
            return _resultBestBadgeStyle;
        }

        private static GUIStyle CreateResultBadgeStyle(
            Color backgroundColor,
            Color borderColor,
            Color textColor,
            string textureName)
        {
            var style = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(
                    ResultBadgeHorizontalPadding(),
                    ResultBadgeHorizontalPadding(),
                    ResultBadgeVerticalPadding(),
                    ResultBadgeVerticalPadding()),
                margin = new RectOffset(0, 0, 0, 0),
                fixedHeight = ResultBadgeMinHeight(),
                font = BrowserDisplayFont(),
                fontSize = ResultBadgeFontSize(),
                fontStyle = FontStyle.Bold,
            };
            var background = RoundedGradientGuiTexture(
                backgroundColor,
                backgroundColor,
                borderColor,
                64,
                (int)ResultBadgeMinHeight(),
                4f,
                textureName);
            SetGuiStyleState(style.normal, background, textColor);
            SetGuiStyleState(style.hover, background, textColor);
            return style;
        }

        private static int ResultBadgeFontSize()
        {
            // React computes the 10px source badge text at 11.5px.
            return 12;
        }

        private static int ResultBadgeHorizontalPadding()
        {
            return 10;
        }

        private static int ResultBadgeVerticalPadding()
        {
            return 5;
        }

        private static Font BrowserBodyFont()
        {
            if (_browserBodyFont != null) return _browserBodyFont;
            // Match the browser body stack on Windows, where Segoe UI is the
            // resolved fallback for the source's Segoe UI Variable family.
            // Keep LegacyRuntime.ttf as the portable fallback for platforms
            // without the Windows system font.
            _browserBodyFont = TryCreateSystemFont("Segoe UI") ??
                Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _browserBodyFont;
        }

        private static Font BrowserDisplayFont()
        {
            if (_browserDisplayFont != null) return _browserDisplayFont;
            // The browser uses Bahnschrift for display labels, headings, and
            // numeric emphasis. It is present on the Windows target used by
            // this project; other platforms fall back to the body font.
            _browserDisplayFont = TryCreateSystemFont("Bahnschrift") ?? BrowserBodyFont();
            return _browserDisplayFont;
        }

        private static Font TryCreateSystemFont(string family)
        {
            if (string.IsNullOrEmpty(family)) return null;
            if (HasCommandLineArgument("-vfno-system-fonts")) return null;
            try
            {
                return Font.CreateDynamicFontFromOSFont(family, 32);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void SetupCamera()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                var cameraObject = new GameObject("VoidFall Camera");
                _camera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }
            _camera.orthographic = true;
            _camera.orthographicSize = ReferenceHalfHeight;
            UpdateGameplayCameraViewport();
            _camera.allowDynamicResolution = false;
            _camera.backgroundColor = new Color(0.015f, 0.025f, 0.07f, 1);
            if (_camera.GetComponent<AudioListener>() == null)
                _camera.gameObject.AddComponent<AudioListener>();
            SetupVideoVolume();
        }

        private Vector2 GameplayViewportHalfExtent()
        {
            if (Screen.width > 0 && Screen.height > 0)
                return GameplayViewportHalfExtent(Screen.width, Screen.height);
            if (_camera != null && _camera.orthographic)
                return RenderViewportHalfExtent(_camera.orthographicSize, _camera.aspect);
            return new Vector2(WorldHalfWidth, WorldHalfHeight);
        }

        /// <summary>
        /// The unified gameplay framing. The camera used to run 1 world unit
        /// per pixel, so the visible battlefield changed with the window
        /// resolution; every resolution now sees the same world. The size is
        /// the framing the 720p-era view had (WorldHalfHeight), dezoomed 35%.
        /// </summary>
        internal const float GameplayReferenceHalfHeight = WorldHalfHeight * 1.35f;

        /// <summary>Spatial Awareness widens the unified framing per rank.</summary>
        private static float _spatialZoomScale = 1f;
        private static float ReferenceHalfHeight => GameplayReferenceHalfHeight * _spatialZoomScale;

        private static Vector2 GameplayViewportHalfExtent(float viewportWidth, float viewportHeight)
        {
            var height = Mathf.Max(1f, viewportHeight);
            return new Vector2(
                ReferenceHalfHeight * Mathf.Max(0.5f, viewportWidth) / height,
                ReferenceHalfHeight);
        }

        private void SetupPlayer()
        {
            var player = new GameObject("Operative");
            player.transform.SetParent(_worldRoot, false);
            _playerView = player.AddComponent<SpriteRenderer>();
            _playerView.sprite = ProceduralSpriteFactory.Operative();
            _playerView.color = Color.white;
            _playerView.sortingOrder = 33;
            player.transform.localScale = Vector3.one * ProceduralSpriteFactory.OperativeCanvasSize();
            _playerAuraView = CreateView(
                "Operative Aura",
                ProceduralSpriteFactory.PlayerAura(),
                31);
            _playerRingView = CreateView(
                "Operative Ring",
                ProceduralSpriteFactory.PlayerRing(),
                32);

            SetupPlayerCosmetics();
        }

        private static bool HasCommandLineArgument(string expected)
        {
            var args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length; index++)
                if (string.Equals(args[index], expected, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private void SetupFx()
        {
            var fxObject = new GameObject("VoidFall FX");
            fxObject.transform.SetParent(_worldRoot, false);
            _fx = fxObject.AddComponent<ParticleSystem>();
            var main = _fx.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = MaxSourceParticles;
            main.startLifetime = 0.55f;
            main.startSpeed = 80f;
            main.startSize = 5f;
            main.startColor = Color.white;
            var emission = _fx.emission;
            emission.enabled = false;
            var shape = _fx.shape;
            shape.enabled = false;
            var renderer = _fx.GetComponent<ParticleSystemRenderer>();
            _fxMaterial = new Material(VoidFallRenderMaterials.AdditiveSprite);
            _fxMaterial.mainTexture = ProceduralSpriteFactory.ParticleDot().texture;
            renderer.sharedMaterial = _fxMaterial;
            renderer.sortingOrder = 40;
            // Keep the ParticleSystem as a compatibility/simulation shadow for
            // existing runtime probes. Visible particles are rendered from the
            // unified browser-order SpriteRenderer pool below.
            renderer.enabled = false;

            var sizeOverLifetime = _fx.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            var sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 1f);
            sizeCurve.AddKey(1f, 0.4f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var colorOverLifetime = _fx.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var alphaGradient = new Gradient();
            alphaGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f),
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(alphaGradient);
        }

        private static Text CreateText(Transform parent, Vector2 position, Vector2 anchor, int size, Color color)
        {
            var objectRoot = new GameObject("HUD Text");
            objectRoot.transform.SetParent(parent, false);
            var text = objectRoot.AddComponent<Text>();
            text.font = BrowserBodyFont();
            text.fontSize = size;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var rect = text.rectTransform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(800, 180);
            return text;
        }

        private SpriteRenderer EnsureRailAfterimageView(int index, bool near)
        {
            var views = near ? _railAfterimageNearViews : _railAfterimageFarViews;
            if (views[index] != null) return views[index];
            var view = CreateView(
                (near ? "Rail Afterimage Near_" : "Rail Afterimage Far_") + index,
                ProceduralSpriteFactory.Projectile("railgun"),
                34);
            var additiveMaterial = ResolveAdditiveSpriteMaterial();
            if (additiveMaterial != null) view.sharedMaterial = additiveMaterial;
            views[index] = view;
            return view;
        }

        private SpriteRenderer EnsureHostileShotView(int index)
        {
            if (_hostileShotViews[index] != null) return _hostileShotViews[index];
            _hostileShotViews[index] = CreateView("HostileShot_" + index, ProceduralSpriteFactory.Circle(), 55);
            return _hostileShotViews[index];
        }

        private static int SourceProjectileFrameIndex(Vector2 velocity)
        {
            var angle = Mathf.Atan2(velocity.y, velocity.x);
            if (angle < 0) angle += Mathf.PI * 2f;
            // Browser projectile and ordinary hostile-shot rendering selects
            // one of 32 pre-rendered oriented frames with Math.round.
            return Mathf.FloorToInt(angle / (Mathf.PI * 2f) * 32f + 0.5f) % 32;
        }

        private static float SourceVisualAngle(Vector2 direction)
        {
            // The browser derives presentation orientation directly from
            // Math.atan2. In particular, tiny non-zero vectors keep their
            // angle instead of being replaced by a right-facing fallback.
            return Mathf.Atan2(direction.y, direction.x);
        }

        private static Vector2 SourceVisualDirection(Vector2 direction)
        {
            var angle = SourceVisualAngle(direction);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        private static float SourceProjectileRotationDegrees(Vector2 velocity)
        {
            return SourceProjectileFrameIndex(velocity) * (360f / 32f);
        }

        private static void SetArcLine(
            LineRenderer view,
            Vector2 centre,
            float radius,
            float start,
            float end,
            float width,
            Color color)
        {
            if (view == null) return;
            var delta = end - start;
            var points = Mathf.Max(12, Mathf.CeilToInt(Mathf.Abs(delta) * Mathf.Max(1f, radius) * 0.08f));
            view.positionCount = points + 1;
            for (var index = 0; index <= points; index++)
            {
                var angle = start + delta * index / points;
                view.SetPosition(index, new Vector3(
                    centre.x + Mathf.Cos(angle) * radius,
                    centre.y + Mathf.Sin(angle) * radius,
                    0));
            }
            view.startColor = color;
            view.endColor = color;
            view.startWidth = width;
            view.endWidth = width;
            view.enabled = true;
        }

        private static void AddArcBand(
            List<Vector3> vertices,
            List<int> triangles,
            List<Color> colors,
            Vector2 centre,
            float radius,
            float width,
            float start,
            float end,
            Color color)
        {
            var innerRadius = Mathf.Max(0.001f, radius - width * 0.5f);
            var outerRadius = radius + width * 0.5f;
            var startCos = Mathf.Cos(start);
            var startSin = Mathf.Sin(start);
            var endCos = Mathf.Cos(end);
            var endSin = Mathf.Sin(end);
            var vertex = vertices.Count;
            vertices.Add(new Vector3(
                centre.x + startCos * innerRadius,
                centre.y + startSin * innerRadius,
                0));
            vertices.Add(new Vector3(
                centre.x + endCos * innerRadius,
                centre.y + endSin * innerRadius,
                0));
            vertices.Add(new Vector3(
                centre.x + endCos * outerRadius,
                centre.y + endSin * outerRadius,
                0));
            vertices.Add(new Vector3(
                centre.x + startCos * outerRadius,
                centre.y + startSin * outerRadius,
                0));
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            triangles.Add(vertex);
            triangles.Add(vertex + 1);
            triangles.Add(vertex + 2);
            triangles.Add(vertex);
            triangles.Add(vertex + 2);
            triangles.Add(vertex + 3);
        }

        private static void AddFan(
            List<Vector3> vertices,
            List<int> triangles,
            List<Color> colors,
            Vector2 centre,
            float radius,
            float startAngle,
            float endAngle,
            int segments,
            Color color)
        {
            var centreIndex = vertices.Count;
            vertices.Add(new Vector3(centre.x, centre.y, 0));
            colors.Add(color);
            for (var index = 0; index <= segments; index++)
            {
                var angle = Mathf.Lerp(startAngle, endAngle, index / (float)segments);
                vertices.Add(new Vector3(
                    centre.x + Mathf.Cos(angle) * radius,
                    centre.y + Mathf.Sin(angle) * radius,
                    0));
                colors.Add(color);
            }
            for (var index = 0; index < segments; index++)
            {
                triangles.Add(centreIndex);
                triangles.Add(centreIndex + 1 + index);
                triangles.Add(centreIndex + 2 + index);
            }
        }

        private SpriteRenderer EnsureBladeView(int index)
        {
            if (_bladeViews[index] != null) return _bladeViews[index];
            _bladeViews[index] = CreateView("Blade_" + index, ProceduralSpriteFactory.Blade(false), 27);
            return _bladeViews[index];
        }

        private SpriteRenderer EnsureHollowBladeView()
        {
            if (_hollowBladeView != null) return _hollowBladeView;
            _hollowBladeView = CreateView("Hollow Blade", ProceduralSpriteFactory.Blade(true), 30);
            var additiveMaterial = ResolveAdditiveSpriteMaterial();
            if (additiveMaterial != null) _hollowBladeView.sharedMaterial = additiveMaterial;
            _hollowBladeView.color = new Color(0.37f, 0.9f, 0.82f, 1);
            return _hollowBladeView;
        }

        private LineRenderer EnsureArcView(int index)
        {
            if (_arcViews[index] != null) return _arcViews[index];
            var objectRoot = new GameObject("Arc_" + index);
            objectRoot.transform.SetParent(_worldRoot, false);
            var line = objectRoot.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            var additiveMaterial = ResolveAdditiveSpriteMaterial();
            line.sharedMaterial = additiveMaterial;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.sortingOrder = 36;
            line.enabled = false;
            _arcViews[index] = line;
            return line;
        }

        private LineRenderer EnsureArcCoreView(int index)
        {
            if (_arcCoreViews[index] != null) return _arcCoreViews[index];
            var objectRoot = new GameObject("Arc Core_" + index);
            objectRoot.transform.SetParent(_worldRoot, false);
            var line = objectRoot.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            var additiveMaterial = ResolveAdditiveSpriteMaterial();
            line.sharedMaterial = additiveMaterial;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.sortingOrder = 37;
            line.enabled = false;
            _arcCoreViews[index] = line;
            return line;
        }

        private LineRenderer CreateLineView(string name, int sortingOrder)
        {
            var objectRoot = new GameObject(name);
            objectRoot.transform.SetParent(_worldRoot, false);
            var line = objectRoot.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.sharedMaterial = VoidFallRenderMaterials.DefaultUnlit;
            // CanvasRenderingContext2D defaults to butt caps and miter joins.
            // Explicit round paths opt in through ConfigureRoundLine.
            line.numCapVertices = 0;
            line.numCornerVertices = 0;
            line.sortingOrder = sortingOrder;
            line.enabled = false;
            return line;
        }

        private static void ConfigureRoundLine(LineRenderer line)
        {
            if (line == null) return;
            line.numCapVertices = 1;
            line.numCornerVertices = 1;
        }

        private SpriteRenderer CreateView(string name, Sprite sprite, int sortingOrder)
        {
            var view = new GameObject(name);
            view.transform.SetParent(_worldRoot, false);
            var renderer = view.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            renderer.enabled = false;
            return renderer;
        }

        private static void CreateBuildChipView(
            Transform parent,
            string name,
            Vector2 anchor,
            Vector2 position,
            Vector2 size,
            out Image background,
            out Image accentBar,
            out RawImage icon,
            out Text label,
            out Text rank)
        {
            background = CreateHudImage(parent, name);
            background.sprite = WeaponChipHudBackgroundSprite();
            background.color = new Color(0.02f, 0.035f, 0.063f, 0.74f);
            var backgroundRect = background.rectTransform;
            backgroundRect.anchorMin = anchor;
            backgroundRect.anchorMax = anchor;
            backgroundRect.pivot = anchor;
            backgroundRect.anchoredPosition = position;
            backgroundRect.sizeDelta = size;

            var borderObject = new GameObject("Chip Border");
            borderObject.transform.SetParent(background.transform, false);
            var border = borderObject.AddComponent<Image>();
            border.sprite = WeaponChipHudBorderSprite();
            border.color = new Color(0.4f, 0.9f, 1f, 0.35f);
            border.raycastTarget = false;
            border.rectTransform.anchorMin = Vector2.zero;
            border.rectTransform.anchorMax = Vector2.one;
            border.rectTransform.offsetMin = Vector2.zero;
            border.rectTransform.offsetMax = Vector2.zero;
            border.enabled = false;

            accentBar = CreateHudImage(background.transform, name + " Accent");
            accentBar.sprite = ProceduralSpriteFactory.Square();
            accentBar.color = new Color(0.4f, 0.9f, 1f, 1f);
            var accentRect = accentBar.rectTransform;
            accentRect.anchorMin = new Vector2(0, 0.5f);
            accentRect.anchorMax = new Vector2(0, 0.5f);
            accentRect.pivot = new Vector2(0, 0.5f);
            accentRect.anchoredPosition = Vector2.zero;
            accentRect.sizeDelta = new Vector2(2, Mathf.Max(11, size.y - 8));

            var iconObject = new GameObject(name + " Icon");
            iconObject.transform.SetParent(background.transform, false);
            icon = iconObject.AddComponent<RawImage>();
            icon.raycastTarget = false;
            icon.enabled = false;
            var iconRect = icon.rectTransform;
            iconRect.anchorMin = new Vector2(0, 0.5f);
            iconRect.anchorMax = new Vector2(0, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(15, 0);
            iconRect.sizeDelta = new Vector2(14, 14);

            label = CreateText(
                background.transform,
                new Vector2(29, 0),
                new Vector2(0, 0.5f),
                size.y <= 27 ? 9 : 10,
                new Color(0.796f, 0.835f, 0.882f, 1f));
            label.rectTransform.sizeDelta = new Vector2(Mathf.Max(36, size.x - 66), size.y);
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.enabled = false;

            var rankBackgroundObject = new GameObject("Rank Background");
            rankBackgroundObject.transform.SetParent(background.transform, false);
            var rankBackground = rankBackgroundObject.AddComponent<Image>();
            rankBackground.sprite = WeaponChipHudRankBackgroundSprite();
            rankBackground.color = new Color(0.4f, 0.9f, 1f, 0.13f);
            rankBackground.raycastTarget = false;
            var rankBackgroundRect = rankBackground.rectTransform;
            rankBackgroundRect.anchorMin = new Vector2(1, 0.5f);
            rankBackgroundRect.anchorMax = new Vector2(1, 0.5f);
            rankBackgroundRect.pivot = new Vector2(1, 0.5f);
            rankBackgroundRect.anchoredPosition = new Vector2(-8, 0);
            rankBackgroundRect.sizeDelta = new Vector2(size.y <= 27 ? 15 : 18, size.y <= 27 ? 15 : 18);
            rankBackground.enabled = false;

            rank = CreateText(
                background.transform,
                new Vector2(-8, 0),
                new Vector2(1, 0.5f),
                size.y <= 27 ? 9 : 10,
                new Color(0.4f, 0.9f, 1f, 1f));
            rank.rectTransform.sizeDelta = new Vector2(size.y <= 27 ? 42 : 18, size.y <= 27 ? size.y : 18);
            rank.alignment = TextAnchor.MiddleCenter;
            rank.raycastTarget = false;
            rank.enabled = false;
        }

        private static void ConfigureTopLeftBar(Image image, Vector2 position)
        {
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = 0;
            image.rectTransform.anchorMin = new Vector2(0, 1);
            image.rectTransform.anchorMax = new Vector2(0, 1);
            image.rectTransform.pivot = new Vector2(0, 1);
            image.rectTransform.anchoredPosition = position;
            image.rectTransform.sizeDelta = new Vector2(220, 10);
        }

        private static void Hide(SpriteRenderer renderer)
        {
            if (renderer != null) renderer.enabled = false;
        }

        private static void Hide(MeshRenderer renderer)
        {
            if (renderer != null) renderer.enabled = false;
        }

        private static void Hide(LineRenderer renderer)
        {
            if (renderer != null) renderer.enabled = false;
        }

        private static void Hide(Text text)
        {
            if (text != null) text.enabled = false;
        }

        private static void Hide(Image image)
        {
            if (image != null) image.enabled = false;
        }

        private static float BackOut(float value)
        {
            const float c = 1.70158f;
            var u = value - 1f;
            return 1f + (c + 1f) * u * u * u + c * u * u;
        }

        private static Color ParseColor(string value, Color fallback)
        {
            return ColorUtility.TryParseHtmlString(value, out var color) ? color : fallback;
        }

        // JavaScript Math.round is floor(value + 0.5) for these non-negative
        // decorative budgets; Mathf.RoundToInt uses midpoint-to-even instead.
        private static int SourceRound(float value)
        {
            return Mathf.FloorToInt(value + 0.5f);
        }

        private static int TelemetryFpsForFrame(float frameSeconds)
        {
            return frameSeconds > 0.0001f
                ? SourceRound(1000f / frameSeconds)
                : 0;
        }

        private sealed class FilamentRasterPlate
        {
            private const int CoverageSamplesPerAxis = 4;
            private const int CoverageSampleCount = CoverageSamplesPerAxis * CoverageSamplesPerAxis;
            private readonly float _worldWidth;
            private readonly float _worldHeight;
            private readonly int _pixelWidth;
            private readonly int _pixelHeight;
            private readonly Color[] _pixels;

            public FilamentRasterPlate(float worldWidth, float worldHeight, int pixelWidth, int pixelHeight)
            {
                _worldWidth = Mathf.Max(1f, worldWidth);
                _worldHeight = Mathf.Max(1f, worldHeight);
                _pixelWidth = Mathf.Max(1, pixelWidth);
                _pixelHeight = Mathf.Max(1, pixelHeight);
                _pixels = new Color[_pixelWidth * _pixelHeight];
                for (var index = 0; index < _pixels.Length; index++)
                    _pixels[index] = Color.clear;
            }

            public void FillBand(
                Vector2[] points,
                float[] widths,
                float spread,
                float shift,
                Color color,
                float alpha)
            {
                if (points == null || widths == null || points.Length < 2) return;
                var polygon = new Vector2[points.Length * 2];
                for (var index = 0; index < points.Length; index++)
                {
                    var normal = NearFilamentNormal(points, index);
                    var half = widths[Mathf.Min(index, widths.Length - 1)] * spread * 0.5f;
                    var centre = points[index] + normal * (widths[Mathf.Min(index, widths.Length - 1)] * shift);
                    polygon[index] = centre + normal * half;
                    polygon[polygon.Length - 1 - index] = centre - normal * half;
                }
                FillPolygon(polygon, color, alpha);
            }

            public void EraseEllipse(
                Vector2 centre,
                float radiusX,
                float radiusY,
                float rotation,
                float alpha)
            {
                var safeRadiusX = Mathf.Max(0.001f, radiusX);
                var safeRadiusY = Mathf.Max(0.001f, radiusY);
                var cosine = Mathf.Cos(rotation);
                var sine = Mathf.Sin(rotation);
                var extentX = Mathf.Sqrt(
                    safeRadiusX * safeRadiusX * cosine * cosine +
                    safeRadiusY * safeRadiusY * sine * sine);
                var extentY = Mathf.Sqrt(
                    safeRadiusX * safeRadiusX * sine * sine +
                    safeRadiusY * safeRadiusY * cosine * cosine);
                var minX = Mathf.Max(0, Mathf.FloorToInt(WorldToPixel(centre.x - extentX, true)) - 1);
                var maxX = Mathf.Min(_pixelWidth - 1, Mathf.CeilToInt(WorldToPixel(centre.x + extentX, true)) + 1);
                var minY = Mathf.Max(0, Mathf.FloorToInt(WorldToPixel(centre.y - extentY, false)) - 1);
                var maxY = Mathf.Min(_pixelHeight - 1, Mathf.CeilToInt(WorldToPixel(centre.y + extentY, false)) + 1);
                var cut = Mathf.Clamp01(alpha);
                for (var y = minY; y <= maxY; y++)
                {
                    for (var x = minX; x <= maxX; x++)
                    {
                        var coverage = 0f;
                        for (var sampleY = 0; sampleY < CoverageSamplesPerAxis; sampleY++)
                        {
                            for (var sampleX = 0; sampleX < CoverageSamplesPerAxis; sampleX++)
                            {
                                var delta = SampleWorld(x, y, sampleX, sampleY) - centre;
                                var localX = cosine * delta.x + sine * delta.y;
                                var localY = -sine * delta.x + cosine * delta.y;
                                var normalizedX = localX / safeRadiusX;
                                var normalizedY = localY / safeRadiusY;
                                if (normalizedX * normalizedX + normalizedY * normalizedY < 1f)
                                    coverage += 1f / CoverageSampleCount;
                            }
                        }

                        if (coverage <= 0f) continue;
                        var index = y * _pixelWidth + x;
                        var destination = _pixels[index];
                        var remaining = Mathf.Clamp01(1f - cut * coverage);
                        destination.a *= remaining;
                        _pixels[index] = destination;
                    }
                }
            }

            public Texture2D ToTexture(string name)
            {
                var texture = new Texture2D(_pixelWidth, _pixelHeight, TextureFormat.RGBA32, false)
                {
                    name = name + " Texture",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
                texture.SetPixels(_pixels);
                texture.Apply(false, true);
                return texture;
            }

            private void FillPolygon(Vector2[] points, Color color, float alpha)
            {
                if (points == null || points.Length < 3) return;
                var minX = _pixelWidth - 1;
                var minY = _pixelHeight - 1;
                var maxX = 0;
                var maxY = 0;
                for (var index = 0; index < points.Length; index++)
                {
                    var pixel = ToPixel(points[index]);
                    minX = Mathf.Min(minX, Mathf.FloorToInt(pixel.x));
                    maxX = Mathf.Max(maxX, Mathf.CeilToInt(pixel.x));
                    minY = Mathf.Min(minY, Mathf.FloorToInt(pixel.y));
                    maxY = Mathf.Max(maxY, Mathf.CeilToInt(pixel.y));
                }
                minX = Mathf.Clamp(minX, 0, _pixelWidth - 1);
                maxX = Mathf.Clamp(maxX, 0, _pixelWidth - 1);
                minY = Mathf.Clamp(minY, 0, _pixelHeight - 1);
                maxY = Mathf.Clamp(maxY, 0, _pixelHeight - 1);
                var sourceAlpha = Mathf.Clamp01(color.a * alpha);
                if (sourceAlpha <= 0f) return;
                for (var y = minY; y <= maxY; y++)
                {
                    for (var x = minX; x <= maxX; x++)
                    {
                        var coverage = 0;
                        for (var sampleY = 0; sampleY < CoverageSamplesPerAxis; sampleY++)
                        {
                            for (var sampleX = 0; sampleX < CoverageSamplesPerAxis; sampleX++)
                            {
                                if (Contains(points, SampleWorld(x, y, sampleX, sampleY)))
                                    coverage++;
                            }
                        }
                        if (coverage == 0) continue;
                        BlendSourceOver(
                            x,
                            y,
                            color,
                            sourceAlpha * coverage / CoverageSampleCount);
                    }
                }
            }

            private Vector2 ToPixel(Vector2 world)
            {
                return new Vector2(
                    (world.x / _worldWidth + 0.5f) * _pixelWidth,
                    (world.y / _worldHeight + 0.5f) * _pixelHeight);
            }

            private float WorldToPixel(float value, bool horizontal)
            {
                return (value / (horizontal ? _worldWidth : _worldHeight) + 0.5f) *
                    (horizontal ? _pixelWidth : _pixelHeight);
            }

            private Vector2 SampleWorld(int x, int y, int sampleX, int sampleY)
            {
                return new Vector2(
                    ((x + (sampleX + 0.5f) / CoverageSamplesPerAxis) / _pixelWidth - 0.5f) * _worldWidth,
                    ((y + (sampleY + 0.5f) / CoverageSamplesPerAxis) / _pixelHeight - 0.5f) * _worldHeight);
            }

            private void BlendSourceOver(int x, int y, Color color, float sourceAlpha)
            {
                if (sourceAlpha <= 0f) return;
                var index = y * _pixelWidth + x;
                var destination = _pixels[index];
                var destinationAlpha = Mathf.Clamp01(destination.a);
                var outputAlpha = sourceAlpha + destinationAlpha * (1f - sourceAlpha);
                if (outputAlpha <= 0.0001f)
                {
                    _pixels[index] = Color.clear;
                    return;
                }
                _pixels[index] = new Color(
                    (color.r * sourceAlpha + destination.r * destinationAlpha * (1f - sourceAlpha)) / outputAlpha,
                    (color.g * sourceAlpha + destination.g * destinationAlpha * (1f - sourceAlpha)) / outputAlpha,
                    (color.b * sourceAlpha + destination.b * destinationAlpha * (1f - sourceAlpha)) / outputAlpha,
                    outputAlpha);
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

        private struct ArenaCycleVisualState
        {
            public float Definition;
            public float Current;
            public float Rim;
            public float Density;
            public float EdgeBias;
        }

        private static float NearStreamNext(ref uint state)
        {
            state += 0x6d2b79f5u;
            var value = state;
            value = (value ^ (value >> 15)) * (value | 1u);
            value ^= value + ((value ^ (value >> 7)) * (value | 61u));
            return (value ^ (value >> 14)) / 4294967296f;
        }

        private static float NearFbm(float x, float y, int seed, int octaves)
        {
            var sum = 0f;
            var amplitude = 1f;
            var total = 0f;
            var frequency = 1f;
            for (var octave = 0; octave < octaves; octave++)
            {
                sum += NearValueNoise(x * frequency, y * frequency, seed + octave * 8191) * amplitude;
                total += amplitude;
                amplitude *= 0.52f;
                frequency *= 2.07f;
            }
            return total > 0 ? sum / total : 0;
        }

        private static float NearValueNoise(float x, float y, int seed)
        {
            var xi = Mathf.FloorToInt(x);
            var yi = Mathf.FloorToInt(y);
            var xf = NearSmooth(x - xi);
            var yf = NearSmooth(y - yi);
            var a = NearHash2(xi, yi, seed);
            var b = NearHash2(xi + 1, yi, seed);
            var c = NearHash2(xi, yi + 1, seed);
            var d = NearHash2(xi + 1, yi + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, xf), Mathf.Lerp(c, d, xf), yf);
        }

        private static float NearSmooth(float value)
        {
            return value * value * (3f - 2f * value);
        }

        private static float NearHash2(int x, int y, int seed)
        {
            unchecked
            {
                uint hash = (uint)(x * 374761393 + y * 668265263 + seed * 362437);
                hash = (hash ^ (hash >> 13)) * 1274126177u;
                hash ^= hash >> 16;
                return hash / 4294967296f;
            }
        }

        private int ActiveEnemies() => _gameSim.ActiveEnemies();
        private int ActiveHostileShots() => _gameSim.ActiveHostileShots();
        private static bool HasExplosiveShardCapacity(int activeHostileShots)
        {
            return MaxHostileShots - Mathf.Max(0, activeHostileShots) >= MeteorRules.ExplosiveShardCount;
        }

        private static int FindInactive(EnemyState[] states) => GameSim.FindInactive(states);
        private static int FindInactive(BulletState[] states) => GameSim.FindInactive(states);
        private static int FindInactive(HostileShotState[] states) => GameSim.FindInactive(states);
        private static int FindInactive(MeteorState[] states) => GameSim.FindInactive(states);
        private static int FindInactive(PickupState[] states) => GameSim.FindInactive(states);
        private static int FindInactive(PickupState[] states, int count) => GameSim.FindInactive(states, count);
        private static int FindInactive(BossState[] states) => GameSim.FindInactive(states);

        internal SaveSettings RestoreSettingsInternal(SaveSettings snapshot)
        {
            if (_saveData == null) return snapshot;
            _saveData.settings = snapshot;
            return snapshot;
        }

        /// <summary>Bridge adapter exposing the settings slice to VoidFall.UI.</summary>
        private sealed class RuntimeGameBridge : IGameBridge
        {
            private readonly VoidFallGameRuntime _rt;
            public RuntimeGameBridge(VoidFallGameRuntime rt) { _rt = rt; }
            public SaveSettings CloneLiveSettings() => VoidFallGameRuntime.CloneSettings(_rt._saveData?.settings);
            public void RestoreSettings(SaveSettings snapshot) => _rt.RestoreSettingsInternal(snapshot);
            public bool TryPersistSettings() => _rt.CommitSettings();
            public bool TryPersistProfile() => _rt.CommitSettings();
            public void ApplyLiveSettings() => _rt.ApplySettings();
            public IReadOnlyList<HighScoreEntry> GetHighScores()
                => _rt._saveData?.highScores ?? (IReadOnlyList<HighScoreEntry>)System.Array.Empty<HighScoreEntry>();
            public LifetimeStats GetLifetimeStats() => _rt._saveData?.stats;
        }
    }
}
