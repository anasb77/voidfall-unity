using UnityEngine;
using UnityEngine.UI;
using VoidFall.Core;

namespace VoidFall.UI
{
    /// <summary>
    /// One CanvasRenderer and one bounded mesh for the entire reactive border.
    /// Geometry changes only with quality/layout/resolution; music updates are
    /// material uniforms and never rebuild the Canvas mesh.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MusicPerimeterGraphic : MaskableGraphic
    {
        private static readonly int BandsId = Shader.PropertyToID("_Bands");
        private static readonly int StateId = Shader.PropertyToID("_State");
        private static readonly int AccentId = Shader.PropertyToID("_Accent");
        private static readonly int TimeId = Shader.PropertyToID("_TimeValue");

        private Material _instanceMaterial;
        private MusicPerimeterRunLayout _layout;
        private int _detail = -1;
        private bool _reducedMotion;
        private float _clock;
        private float _displayIntensity;

        public int SegmentCount => _detail <= 0 ? 24 : _detail == 1 ? 36 : 48;
        public int MaximumVertexCount => SegmentCount * 8;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
            EnsureMaterial();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureMaterial();
        }

        public void Configure(int runSeed, int detail, bool reducedMotion)
        {
            var nextLayout = MusicPerimeterRules.CreateRunLayout(runSeed);
            var nextDetail = Mathf.Clamp(detail, 0, 2);
            if (_detail == nextDetail && _reducedMotion == reducedMotion &&
                _layout.LayoutIndex == nextLayout.LayoutIndex &&
                _layout.LongBand == nextLayout.LongBand &&
                _layout.CornerBand == nextLayout.CornerBand &&
                _layout.FragmentBand == nextLayout.FragmentBand) return;
            _layout = nextLayout;
            _detail = nextDetail;
            _reducedMotion = reducedMotion;
            SetVerticesDirty();
        }

        public void SetPresentation(
            float bass,
            float mids,
            float treble,
            float ambientIntensity,
            int overclockTier,
            int overclockStreak,
            float surge,
            bool critical,
            float magnetIntensity,
            float visualDamping,
            float unscaledDeltaTime)
        {
            EnsureMaterial();
            if (_instanceMaterial == null) return;
            _clock += Mathf.Max(0f, unscaledDeltaTime);
            var target = Mathf.Max(
                Mathf.Clamp01(ambientIntensity),
                MusicPerimeterRules.OverclockIntensity(overclockTier));
            var responseSeconds = target > _displayIntensity
                ? (_reducedMotion ? 0.22f : 0.08f)
                : (_reducedMotion ? 0.75f : 0.50f);
            _displayIntensity = Mathf.Lerp(
                _displayIntensity,
                target,
                1f - Mathf.Exp(-Mathf.Max(0f, unscaledDeltaTime) / responseSeconds));
            var longValue = Band(_layout.LongBand, bass, mids, treble);
            var cornerValue = Band(_layout.CornerBand, bass, mids, treble);
            var fragmentValue = Band(_layout.FragmentBand, bass, mids, treble);
            if (_reducedMotion)
            {
                longValue = Mathf.Min(longValue, 0.48f);
                cornerValue = Mathf.Min(cornerValue, 0.48f);
                fragmentValue = Mathf.Min(fragmentValue, 0.48f);
                surge = 0f;
            }
            _instanceMaterial.SetVector(BandsId, new Vector4(longValue, cornerValue, fragmentValue, _displayIntensity));
            _instanceMaterial.SetVector(StateId, new Vector4(
                Mathf.Clamp01(surge),
                critical ? 1f : 0f,
                Mathf.Clamp01(magnetIntensity),
                Mathf.Clamp01(visualDamping)));
            _instanceMaterial.SetVector(AccentId, new Vector4(
                Mathf.Clamp(overclockTier, 0, 3),
                Mathf.Max(0, overclockStreak),
                _reducedMotion ? 1f : 0f,
                _layout.LayoutIndex));
            _instanceMaterial.SetFloat(TimeId, _clock);
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            var rect = GetPixelAdjustedRect();
            var count = SegmentCount;
            var perSide = count / 4;
            var core = _detail <= 0 ? 1.4f : _detail == 1 ? 1.8f : 2.2f;
            var halo = _detail <= 0 ? 4.5f : _detail == 1 ? 6f : 8f;
            for (var index = 0; index < count; index++)
            {
                var side = Mathf.Min(3, index / perSide);
                var local = index - side * perSide;
                var steps = side == 3 ? count - perSide * 3 : perSide;
                var t = (local + 0.5f) / Mathf.Max(1, steps);
                var wave = ((index * 37 + _layout.LayoutIndex * 11) % 7) / 6f;
                var length = Mathf.Lerp(20f, 58f, wave);
                if ((index + _layout.LayoutIndex) % 5 == 0) length *= 1.45f;
                var group = index % 5 == 0 ? 1f : index % 3 == 0 ? 2f : 0f;
                AddSegment(helper, rect, side, t, length, halo, group, false);
                AddSegment(helper, rect, side, t, length, core, group, true);
            }
        }

        private static void AddSegment(
            VertexHelper helper,
            Rect rect,
            int side,
            float t,
            float length,
            float thickness,
            float group,
            bool core)
        {
            Vector2 minimum;
            Vector2 maximum;
            if (side == 0 || side == 2)
            {
                var x = Mathf.Lerp(rect.xMin, rect.xMax, side == 0 ? t : 1f - t);
                var y = side == 0 ? rect.yMax : rect.yMin;
                minimum = new Vector2(x - length * 0.5f, y - thickness * 0.5f);
                maximum = new Vector2(x + length * 0.5f, y + thickness * 0.5f);
            }
            else
            {
                var x = side == 1 ? rect.xMax : rect.xMin;
                var y = Mathf.Lerp(rect.yMax, rect.yMin, side == 1 ? t : 1f - t);
                minimum = new Vector2(x - thickness * 0.5f, y - length * 0.5f);
                maximum = new Vector2(x + thickness * 0.5f, y + length * 0.5f);
            }

            var first = helper.currentVertCount;
            var vertex = UIVertex.simpleVert;
            vertex.color = core
                ? new Color32(255, 255, 255, 255)
                : new Color32(90, 220, 255, 82);
            vertex.uv1 = new Vector2(group, core ? 1f : 0f);
            vertex.position = new Vector3(minimum.x, minimum.y);
            helper.AddVert(vertex);
            vertex.position = new Vector3(minimum.x, maximum.y);
            helper.AddVert(vertex);
            vertex.position = new Vector3(maximum.x, maximum.y);
            helper.AddVert(vertex);
            vertex.position = new Vector3(maximum.x, minimum.y);
            helper.AddVert(vertex);
            helper.AddTriangle(first, first + 1, first + 2);
            helper.AddTriangle(first, first + 2, first + 3);
        }

        private static float Band(int index, float bass, float mids, float treble)
        {
            return index == 0 ? bass : index == 1 ? mids : treble;
        }

        private void EnsureMaterial()
        {
            if (_instanceMaterial != null) return;
            var shader = Shader.Find("UI/VoidFallMusicPerimeter");
            if (shader == null) return;
            _instanceMaterial = new Material(shader)
            {
                name = "VoidFall Music Perimeter (Runtime)",
                hideFlags = HideFlags.HideAndDontSave,
            };
            material = _instanceMaterial;
        }

        protected override void OnDestroy()
        {
            if (_instanceMaterial != null)
            {
                if (Application.isPlaying) Destroy(_instanceMaterial);
                else DestroyImmediate(_instanceMaterial);
            }
            base.OnDestroy();
        }
    }
}
