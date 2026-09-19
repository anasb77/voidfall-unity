using UnityEngine;
using UnityEngine.UI;

namespace VoidFall.Runtime
{
    public sealed class LegacyHudGradient : BaseMeshEffect
    {
        private Color _left = Color.white, _right = Color.white;
        public void Configure(Color left, Color right) { _left = left; _right = right; graphic.SetVerticesDirty(); }
        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive() || mesh.currentVertCount == 0) return;
            var vertex = new UIVertex();
            var min = float.MaxValue;
            var max = float.MinValue;
            for (var i = 0; i < mesh.currentVertCount; i++)
            { mesh.PopulateUIVertex(ref vertex, i); min = Mathf.Min(min, vertex.position.x); max = Mathf.Max(max, vertex.position.x); }
            for (var i = 0; i < mesh.currentVertCount; i++)
            {
                mesh.PopulateUIVertex(ref vertex, i);
                vertex.color = (Color)vertex.color * Color.Lerp(_left, _right, Mathf.InverseLerp(min, max, vertex.position.x));
                mesh.SetUIVertex(vertex, i);
            }
        }
    }
}
