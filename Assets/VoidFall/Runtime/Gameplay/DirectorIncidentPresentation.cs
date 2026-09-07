using System;
using UnityEngine;
using UnityEngine.Rendering;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    /// <summary>Owns one reusable world-space incident view, never gameplay forces or camera effects.</summary>
    public sealed class DirectorIncidentPresentation : IDisposable
    {
        private static readonly int StrengthId = Shader.PropertyToID("_Strength");
        private static readonly int WarningId = Shader.PropertyToID("_Warning");
        private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
        private static readonly int ReducedEffectsId = Shader.PropertyToID("_ReducedEffects");
        private static readonly int HighContrastId = Shader.PropertyToID("_HighContrast");
        private static readonly int BackdropId = Shader.PropertyToID("_Backdrop");
        private static readonly int HasBackdropId = Shader.PropertyToID("_HasBackdrop");
        private static readonly int BackdropWorldRectId = Shader.PropertyToID("_BackdropWorldRect");
        private static readonly int CenterId = Shader.PropertyToID("_Center");

        private readonly Transform _worldRoot;
        private GameObject _view;
        private MeshRenderer _renderer;
        private Mesh _mesh;
        private Material _material;
        private MaterialPropertyBlock _properties;
        private bool _creationAttempted;
        private bool _disposed;

        // The runtime multiplies only its arena environment owners by this value.
        public float EnvironmentExposure { get; private set; } = 1;

        public DirectorIncidentPresentation(Transform worldRoot, Camera camera)
        {
            if (worldRoot == null) throw new ArgumentNullException(nameof(worldRoot));
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            _worldRoot = worldRoot;
            // World coordinates deliberately do not follow the supplied camera.
        }

        /// <param name="backdropWorldRect">Unrotated full-texture world bounds: minX, minY, width, height.
        /// UV (0,0) maps to the minimum corner. A packed sprite subrect needs a background-only capture.</param>
        public void Render(MajorIncidentKind kind, MajorIncidentPhase phase, Vector2 center,
            float radius, float strength, bool reducedEffects, bool highContrast,
            Texture backdrop = null, Vector4 backdropWorldRect = default)
        {
            if (_disposed) return;
            strength = Finite(strength) ? Mathf.Clamp01(strength) : 0;
            if (kind == MajorIncidentKind.None || phase == MajorIncidentPhase.None)
            {
                Hide();
                return;
            }

            var warning = phase == MajorIncidentPhase.Warning;
            EnvironmentExposure = kind == MajorIncidentKind.Eclipse && !warning
                ? Mathf.Lerp(1, highContrast ? 0.32f : 0.16f, strength) : 1;
            // Destroyer bodies/telegraphs belong to the existing encounter presentation.
            // Eclipse warning is a parent-owned toast; only the environment multiplier changes here.
            if (kind != MajorIncidentKind.BlackHole)
            {
                HideView();
                return;
            }
            if (!Finite(center.x) || !Finite(center.y) || !Finite(radius) || radius <= 0)
            {
                Hide();
                return;
            }
            if (!EnsureView()) return;

            var rootScale = _worldRoot.lossyScale;
            if (Mathf.Abs(rootScale.x) < 0.0001f || Mathf.Abs(rootScale.y) < 0.0001f)
            {
                Hide();
                return;
            }
            _view.transform.position = new Vector3(center.x, center.y, _worldRoot.position.z);
            _view.transform.rotation = Quaternion.identity;
            // The shader boundary is at normalized radius .91; map it to the supplied gameplay radius.
            var diameter = radius * (2 / 0.91f);
            _view.transform.localScale = new Vector3(diameter / rootScale.x, diameter / rootScale.y, 1);
            var hasBackdrop = backdrop != null && ValidRect(backdropWorldRect);
            _properties.SetFloat(StrengthId, strength);
            _properties.SetFloat(WarningId, warning ? 1 : 0);
            _properties.SetFloat(ReducedEffectsId, reducedEffects ? 1 : 0);
            _properties.SetFloat(HighContrastId, highContrast ? 1 : 0);
            _properties.SetColor(RimColorId, highContrast ? new Color(0.84f, 0.94f, 1)
                : new Color(0.49f, 0.7f, 1));
            _properties.SetTexture(BackdropId, hasBackdrop ? backdrop : Texture2D.blackTexture);
            _properties.SetFloat(HasBackdropId, hasBackdrop ? 1 : 0);
            _properties.SetVector(BackdropWorldRectId, hasBackdrop ? backdropWorldRect : new Vector4(0, 0, 1, 1));
            _properties.SetVector(CenterId, new Vector4(center.x, center.y, 0, 0));
            _renderer.SetPropertyBlock(_properties);
            _renderer.enabled = warning || strength > 0;
        }

        public void Hide()
        {
            EnvironmentExposure = 1;
            HideView();
        }

        public void Dispose()
        {
            if (_disposed) return;
            Hide();
            _disposed = true;
            DestroyOwned(_view);
            DestroyOwned(_mesh);
            DestroyOwned(_material);
            _view = null;
            _renderer = null;
            _mesh = null;
            _material = null;
            _properties = null;
        }

        private bool EnsureView()
        {
            if (_renderer != null) return true;
            if (_creationAttempted || _worldRoot == null) return false;
            _creationAttempted = true;
            var shader = Resources.Load<Shader>("VoidFall/DirectorBlackHole");
            if (shader == null || !shader.isSupported)
            {
                Debug.LogWarning("Director incident shader is missing or unsupported; world incident view unavailable.");
                return false;
            }
            _material = new Material(shader) { name = "Director Incident Material" };
            _properties = new MaterialPropertyBlock();
            _mesh = new Mesh
            {
                name = "Director Incident Quad",
                vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f),
                    new Vector3(0.5f, 0.5f), new Vector3(-0.5f, 0.5f) },
                uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            _mesh.RecalculateBounds();
            _mesh.UploadMeshData(true);
            _view = new GameObject("Director Incident");
            _view.layer = _worldRoot.gameObject.layer;
            _view.transform.SetParent(_worldRoot, false);
            _view.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = _view.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = _material;
            // Existing backdrop -110, grid -95, vignette -90; actor/telegraph layers are above -80.
            _renderer.sortingOrder = -80;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _renderer.enabled = false;
            return true;
        }

        private void HideView()
        {
            if (_renderer == null) return;
            _renderer.enabled = false;
            // Release references to Addressables-owned backgrounds before the parent releases their package.
            _properties.Clear();
            _renderer.SetPropertyBlock(_properties);
        }

        private static bool ValidRect(Vector4 rect) => Finite(rect.x) && Finite(rect.y) &&
            Finite(rect.z) && Finite(rect.w) && rect.z > 0 && rect.w > 0;

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void DestroyOwned(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
