using UnityEngine;
using UnityEngine.UI;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    /// <summary>
    /// Procedural screen-space version of the browser arena fold. The web
    /// renderer draws the same two dark horizons, accent seam, streaks, and
    /// settle ring directly into its canvas.
    /// </summary>
    internal sealed class ArenaTransitionGraphic : MaskableGraphic
    {
        private const float Tilt = -0.12f;
        private const int FoldSegments = 14;
        private const int RingSegments = 56;
        private const float CollapseSeconds = 0.72f;
        private const float SettleSeconds = 1.1f;

        private readonly Vector2[] _horizonPoints = new Vector2[FoldSegments + 1];
        private ArenaPhase _phase;
        private float _phaseT;
        private int _transitionIndex;
        private int _quality = 2;
        private float _ambientTime;
        private Color _accent = Color.cyan;
        private bool _reducedMotion;

        public void SetState(
            ArenaPhase phase,
            double phaseT,
            int transitionIndex,
            float ambientTime,
            Color accent,
            bool reducedMotion,
            int quality)
        {
            _phase = phase;
            _phaseT = Mathf.Max(0, (float)phaseT);
            _transitionIndex = transitionIndex;
            _quality = Mathf.Clamp(quality, 0, 2);
            _ambientTime = ambientTime;
            _accent = accent;
            _reducedMotion = reducedMotion;
            enabled = phase != ArenaPhase.Idle;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (_phase == ArenaPhase.Idle) return;

            var rect = rectTransform.rect;
            var width = Mathf.Max(1, rect.width);
            var height = Mathf.Max(1, rect.height);
            if (_phase == ArenaPhase.Warning)
            {
                // The browser warning is a HUD banner only. The fold mesh is
                // intentionally empty until collapse begins.
                return;
            }

            var intensity = _phase == ArenaPhase.Collapse
                ? 1f - Mathf.Clamp01(_phaseT / CollapseSeconds)
                : Mathf.Clamp01(_phaseT / SettleSeconds);
            var gap = Mathf.Max(3f, height * 0.58f * (1f - intensity));
            var centre = new Vector2(0, 0);
            for (var step = 0; step <= FoldSegments; step++)
            {
                var side = step % 2 == 0 ? -1 : 1;
                _horizonPoints[step] = FoldPoint(step / (float)FoldSegments, side, width, height, gap);
            }

            var darkAlpha = 0.55f + intensity * 0.4f;
            var dark = new Color(0.006f, 0.009f, 0.03f, darkAlpha);
            AddFoldPanel(vertexHelper, width, height, gap, -1, dark);
            AddFoldPanel(vertexHelper, width, height, gap, 1, dark);

            var seam = new Color(_accent.r, _accent.g, _accent.b, intensity * 0.8f);
            var whiteSeam = new Color(0.9f, 0.98f, 1f, intensity * intensity * 0.72f);
            for (var step = 1; step <= FoldSegments; step++)
            {
                var from = FoldSeamPoint(
                    (step - 1) / (float)FoldSegments,
                    step % 2 == 0 ? 1 : -1,
                    width,
                    height,
                    gap);
                var to = FoldSeamPoint(
                    step / (float)FoldSegments,
                    step % 2 == 0 ? -1 : 1,
                    width,
                    height,
                    gap);
                AddLine(vertexHelper, from, to, 1.5f + intensity * 2.5f, seam);
                var normal = new Vector2(-(to - from).y, (to - from).x).normalized;
                AddLine(
                    vertexHelper,
                    from + normal * 3f,
                    to + normal * 3f,
                    0.8f + intensity * 1.2f,
                    whiteSeam);
            }

            var flash = Mathf.Clamp01((intensity - 0.72f) / 0.28f);
            if (flash > 0f)
                AddRect(vertexHelper, width, height, new Color(_accent.r, _accent.g, _accent.b, flash * 0.16f));

            if (!_reducedMotion && _quality > 0)
            {
                var streakColor = new Color(0.86f, 0.92f, 1f, intensity * 0.34f);
                var diagonal = Mathf.Sqrt(width * width + height * height);
                var streakCount = _quality > 1 ? 36 : 20;
                for (var streak = 0; streak < streakCount; streak++)
                {
                    var angle = streak / (float)streakCount * Mathf.PI * 2f + _transitionIndex * 0.37f;
                    var reach = diagonal * (0.2f + (streak % 5) * 0.045f);
                    var inner = 34f + (1f - intensity) * reach * 0.58f;
                    var outer = inner + 28f + intensity * 95f;
                    var squash = 0.72f + (streak % 3) * 0.13f;
                    var from = new Vector2(Mathf.Cos(angle) * inner, Mathf.Sin(angle) * inner * squash);
                    var to = new Vector2(Mathf.Cos(angle) * outer, Mathf.Sin(angle) * outer * squash);
                    AddLine(vertexHelper, from, to, 0.8f + (streak % 3) * 0.35f, streakColor);
                }
            }

            if (_phase == ArenaPhase.Settle)
            {
                var progress = Mathf.Clamp01(1f - _phaseT / SettleSeconds);
                var reach = 28f + progress * Mathf.Sqrt(width * width + height * height) * 0.52f;
                var ringColor = new Color(_accent.r, _accent.g, _accent.b, (1f - progress) * 0.58f * (_reducedMotion ? 0.35f : 1f));
                AddEllipse(vertexHelper, reach, reach * 0.66f, Tilt, 4f - progress * 2.5f, ringColor);
                var innerColor = new Color(0.9f, 0.98f, 1f, ringColor.a * 0.55f);
                AddEllipse(vertexHelper, reach * 0.72f, reach * 0.48f, -Tilt, 2.5f - progress, innerColor);
            }
        }

        private Vector2 FoldSeamPoint(float t, int side, float width, float height, float gap)
        {
            var x = t * width;
            var localX = x - width * 0.5f;
            var horizon = FoldPoint(t, side, width, height, gap).y;
            var linear = -localX * Tilt;
            return new Vector2(localX, horizon * 0.035f + linear * 0.965f);
        }

        private Vector2 FoldPoint(float t, int side, float width, float height, float gap)
        {
            var canvasX = t * width;
            var canvasCentreX = width * 0.5f;
            var canvasCentreY = height * 0.5f;
            var slope = (canvasX - canvasCentreX) * Tilt;
            var ripple = Mathf.Sin(t * 19f + _transitionIndex * 2.7f) * 10f +
                         Mathf.Sin(t * 43f + _transitionIndex * 1.3f) * 4f;
            var canvasY = canvasCentreY + slope + ripple + gap * side;
            return new Vector2(canvasX - canvasCentreX, canvasCentreY - canvasY);
        }

        private void AddFoldPanel(VertexHelper vertexHelper, float width, float height, float gap, int side, Color color)
        {
            var pointCount = FoldSegments + 3;
            var centre = Vector2.zero;
            var topLeft = new Vector2(-width * 0.5f, height * 0.5f);
            var topRight = new Vector2(width * 0.5f, height * 0.5f);
            var bottomLeft = new Vector2(-width * 0.5f, -height * 0.5f);
            var bottomRight = new Vector2(width * 0.5f, -height * 0.5f);
            var first = side < 0 ? topLeft : bottomLeft;
            var second = side < 0 ? topRight : bottomRight;
            var points = new Vector2[pointCount];
            points[0] = first;
            points[1] = second;
            for (var step = 0; step <= FoldSegments; step++)
                points[step + 2] = FoldPoint(step / (float)FoldSegments, side, width, height, gap);
            for (var index = 0; index < pointCount; index++) centre += points[index];
            centre /= pointCount;
            var centreIndex = vertexHelper.currentVertCount;
            vertexHelper.AddVert(centre, color, Vector2.zero);
            var firstIndex = vertexHelper.currentVertCount;
            for (var index = 0; index < pointCount; index++)
                vertexHelper.AddVert(points[index], color, Vector2.zero);
            for (var index = 0; index < pointCount; index++)
            {
                var next = (index + 1) % pointCount;
                vertexHelper.AddTriangle(centreIndex, firstIndex + index, firstIndex + next);
            }
        }

        private static void AddRect(VertexHelper vertexHelper, float width, float height, Color color)
        {
            var start = vertexHelper.currentVertCount;
            vertexHelper.AddVert(new Vector2(-width * 0.5f, -height * 0.5f), color, Vector2.zero);
            vertexHelper.AddVert(new Vector2(-width * 0.5f, height * 0.5f), color, Vector2.zero);
            vertexHelper.AddVert(new Vector2(width * 0.5f, height * 0.5f), color, Vector2.zero);
            vertexHelper.AddVert(new Vector2(width * 0.5f, -height * 0.5f), color, Vector2.zero);
            vertexHelper.AddTriangle(start, start + 1, start + 2);
            vertexHelper.AddTriangle(start, start + 2, start + 3);
        }

        private static void AddLine(VertexHelper vertexHelper, Vector2 from, Vector2 to, float width, Color color)
        {
            var direction = to - from;
            if (direction.sqrMagnitude < 0.0001f) return;
            var normal = new Vector2(-direction.y, direction.x).normalized * (width * 0.5f);
            var start = vertexHelper.currentVertCount;
            vertexHelper.AddVert(from - normal, color, Vector2.zero);
            vertexHelper.AddVert(from + normal, color, Vector2.zero);
            vertexHelper.AddVert(to + normal, color, Vector2.zero);
            vertexHelper.AddVert(to - normal, color, Vector2.zero);
            vertexHelper.AddTriangle(start, start + 1, start + 2);
            vertexHelper.AddTriangle(start, start + 2, start + 3);
        }

        private static void AddEllipse(VertexHelper vertexHelper, float radiusX, float radiusY, float rotation, float width, Color color)
        {
            var previous = new Vector2(radiusX, 0);
            for (var index = 1; index <= RingSegments; index++)
            {
                var angle = index / (float)RingSegments * Mathf.PI * 2f;
                var next = new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
                previous = Rotate(previous, rotation);
                next = Rotate(next, rotation);
                AddLine(vertexHelper, previous, next, width, color);
                previous = new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
            }
        }

        private static Vector2 Rotate(Vector2 point, float radians)
        {
            var cosine = Mathf.Cos(radians);
            var sine = Mathf.Sin(radians);
            return new Vector2(point.x * cosine - point.y * sine, point.x * sine + point.y * cosine);
        }
    }
}
