using UnityEngine;
using UnityEngine.UI;

namespace VoidFall.Runtime
{
    public sealed class LegacyLevelBadge : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var r = rectTransform.rect;
            var x = r.center.x;
            var top = r.yMax - 51;
            Quad(mesh, x - 35, top - 22, 70, 22, new Color(.03f, .14f, .1f, .75f));
            Quad(mesh, x - 35, top, 70, 1, color);
            Quad(mesh, x - 35, top - 22, 70, 1, color);
            Quad(mesh, x - 35, top - 22, 1, 23, color);
            Quad(mesh, x + 34, top - 22, 1, 23, color);
        }
        private static void Quad(VertexHelper mesh, float x, float y, float w, float h, Color c)
        {
            var i = mesh.currentVertCount;
            mesh.AddVert(new Vector3(x, y), c, Vector2.zero);
            mesh.AddVert(new Vector3(x, y + h), c, Vector2.zero);
            mesh.AddVert(new Vector3(x + w, y + h), c, Vector2.zero);
            mesh.AddVert(new Vector3(x + w, y), c, Vector2.zero);
            mesh.AddTriangle(i, i + 1, i + 2); mesh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
