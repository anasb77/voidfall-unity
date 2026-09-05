using UnityEngine;
using UnityEngine.UI;
using VoidFall.Core;

namespace VoidFall.UI
{
    /// <summary>Four static edge strips. The shader animates rails, spectrum and two opposing groups of five runners.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MusicPerimeterGraphic : MaskableGraphic
    {
        private const float FrameDepth = 112f;
        private static readonly int BandsId = Shader.PropertyToID("_Bands");
        private static readonly int StateId = Shader.PropertyToID("_State");
        private static readonly int AccentId = Shader.PropertyToID("_Accent");
        private static readonly int RectId = Shader.PropertyToID("_FrameRect");
        private static readonly int MotionId = Shader.PropertyToID("_Motion");
        private static readonly int MappingId = Shader.PropertyToID("_BandMapping");
        private static readonly int SpectrumId = Shader.PropertyToID("_Spectrum");
        private readonly Vector4[] _spectrum = new Vector4[6];
        private Material _instanceMaterial;
        private MusicPerimeterRunLayout _layout;
        private int _runSeed, _detail = -1, _lastStreak, _lastLayout = -1;
        private bool _configured, _reducedMotion, _wasActive, _newActivationRequested, _pickupRequested;
        private float _displayIntensity, _travel, _lap = 1f, _variation;

        public int SegmentCount => _detail <= 0 ? 24 : _detail == 1 ? 36 : 48;
        // Keep the existing conservative public bound; the edge-strip mesh uses only 16 vertices.
        public int MaximumVertexCount => SegmentCount * 8;
        public int ActivationIndex { get; private set; }
        public int PatternIndex => _layout.LayoutIndex;
        public float TravelDistance => _travel;

        protected override void Awake() { base.Awake(); raycastTarget = false; EnsureMaterial(); }
        protected override void OnEnable() { base.OnEnable(); EnsureMaterial(); }

        public void ResetRun(int seed, int detail, bool reducedMotion)
        {
            _configured = false;
            Configure(seed, detail, reducedMotion);
        }

        public void Configure(int runSeed, int detail, bool reducedMotion)
        {
            EnsureMaterial();
            if (!_configured || _runSeed != runSeed)
            {
                _runSeed = runSeed;
                ActivationIndex = 0;
                _lastLayout = -1;
                _lastStreak = 0;
                _wasActive = _newActivationRequested = _pickupRequested = false;
                _travel = _displayIntensity = 0;
                _lap = 1;
                _layout = MusicPerimeterRules.CreateRunLayout(runSeed);
                _configured = true;
            }
            _detail = Mathf.Clamp(detail, 0, 2);
            _reducedMotion = reducedMotion;
            SetVerticesDirty();
        }

        public void NotifyPickup(bool newActivation)
        {
            _newActivationRequested |= newActivation;
            _pickupRequested = true;
        }

        public void SetSpectrum(float[] bands)
        {
            for (var group = 0; group < _spectrum.Length; group++)
            {
                var value = Vector4.zero;
                for (var index = 0; index < 4; index++)
                {
                    var sourceIndex = group * 4 + index;
                    value[index] = bands != null && sourceIndex < bands.Length ? Mathf.Clamp01(bands[sourceIndex]) : 0;
                }
                _spectrum[group] = value;
            }
            if (_instanceMaterial != null) _instanceMaterial.SetVectorArray(SpectrumId, _spectrum);
        }

        public void SetPresentation(float bass, float mids, float treble, float ambientIntensity,
            int overclockTier, int overclockStreak, float surge, bool critical, float magnetIntensity,
            float visualDamping, float unscaledDeltaTime, float transient = 0f)
        {
            EnsureMaterial();
            if (_instanceMaterial == null) return;
            var dt = Mathf.Clamp(unscaledDeltaTime, 0f, 0.1f);
            var active = overclockTier > 0 && overclockStreak > 0;
            if (active && (_newActivationRequested || !_wasActive || overclockStreak < _lastStreak))
            {
                ActivationIndex++;
                _layout = MusicPerimeterRules.CreateActivationLayout(_runSeed, ActivationIndex, _lastLayout);
                _lastLayout = _layout.LayoutIndex;
                _variation = Mathf.Repeat((_runSeed & 0xffff) * 0.00037f + ActivationIndex * 0.173f, 1f) * 4096f;
                _travel = 0;
                _lap = 0;
            }
            else if (active && (_pickupRequested || overclockStreak > _lastStreak)) _lap = 0;
            _newActivationRequested = _pickupRequested = false;
            _wasActive = active;
            _lastStreak = active ? overclockStreak : 0;
            var stack = Mathf.Max(1, overclockStreak);
            if (!_reducedMotion && active)
            {
                var surgeAmount = Mathf.Clamp01(1f - _lap * 1.5f);
                _travel += dt * 1.65f * (350f + Mathf.Min(stack, 12) * 85f + Mathf.Clamp01(transient) * 175f + surgeAmount * 1250f);
                _lap = Mathf.Min(1, _lap + dt / (1.35f - Mathf.Min(5, stack) * 0.08f));
            }
            if (_reducedMotion) _lap = 1;
            var target = Mathf.Max(Mathf.Clamp01(ambientIntensity), active ? MusicPerimeterRules.OverclockIntensity(overclockTier) : 0f);
            _displayIntensity = Mathf.Lerp(_displayIntensity, target, 1f - Mathf.Exp(-dt / (target > _displayIntensity ? 0.065f : 0.4f)));
            canvasRenderer.SetAlpha(_displayIntensity > 0.001f ? 1 : 0);
            var rect = rectTransform.rect;
            _instanceMaterial.SetVector(RectId, new Vector4(rect.width, rect.height, FrameDepth, _detail));
            _instanceMaterial.SetVector(BandsId, new Vector4(Mathf.Clamp01(bass), Mathf.Clamp01(mids), Mathf.Clamp01(treble), _displayIntensity));
            _instanceMaterial.SetVector(StateId, new Vector4(_reducedMotion ? 0 : Mathf.Clamp01(surge), 0, 0, Mathf.Clamp01(visualDamping)));
            _instanceMaterial.SetVector(AccentId, new Vector4(overclockTier, Mathf.Max(0, overclockStreak), _reducedMotion ? 1 : 0, _layout.LayoutIndex));
            _instanceMaterial.SetVector(MappingId, new Vector4(_layout.LongBand, _layout.CornerBand, _layout.FragmentBand, 0));
            _instanceMaterial.SetVector(MotionId, new Vector4(_travel, _lap, _variation, active ? 1 : 0));
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            var rect = rectTransform.rect;
            const float depth = FrameDepth;
            for (var side = 0; side < 4; side++)
            {
                Vector2 start, end, inward;
                switch (side)
                {
                    case 0: start = new Vector2(rect.xMin, rect.yMax); end = new Vector2(rect.xMax, rect.yMax); inward = Vector2.down; break;
                    case 1: start = new Vector2(rect.xMax, rect.yMax); end = new Vector2(rect.xMax, rect.yMin); inward = Vector2.left; break;
                    case 2: start = new Vector2(rect.xMax, rect.yMin); end = new Vector2(rect.xMin, rect.yMin); inward = Vector2.up; break;
                    default: start = new Vector2(rect.xMin, rect.yMin); end = new Vector2(rect.xMin, rect.yMax); inward = Vector2.right; break;
                }
                var offset = helper.currentVertCount;
                AddVertex(helper, start, new Vector2(0, 0), side);
                AddVertex(helper, end, new Vector2(1, 0), side);
                AddVertex(helper, end + inward * depth, new Vector2(1, depth), side);
                AddVertex(helper, start + inward * depth, new Vector2(0, depth), side);
                helper.AddTriangle(offset, offset + 1, offset + 2);
                helper.AddTriangle(offset, offset + 2, offset + 3);
            }
        }

        private static void AddVertex(VertexHelper helper, Vector2 position, Vector2 uv, int side)
        {
            var vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.uv0 = uv;
            vertex.uv1 = new Vector2(side, 0);
            helper.AddVert(vertex);
        }

        private void EnsureMaterial()
        {
            if (canvas != null) canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
            if (_instanceMaterial != null) return;
            var shader = Shader.Find("UI/VoidFallMusicPerimeter");
            if (shader == null) return;
            _instanceMaterial = new Material(shader) { name = "VoidFall Music Perimeter (Runtime)", hideFlags = HideFlags.HideAndDontSave };
            material = _instanceMaterial;
        }

        protected override void OnDestroy()
        {
            if (_instanceMaterial != null)
            {
                if (Application.isPlaying) Destroy(_instanceMaterial); else DestroyImmediate(_instanceMaterial);
            }
            base.OnDestroy();
        }
    }
}
