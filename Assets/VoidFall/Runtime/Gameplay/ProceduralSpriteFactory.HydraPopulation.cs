using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    internal static partial class ProceduralSpriteFactory
    {
        private static readonly Sprite[] HydraPopulationSprites = new Sprite[HydraPopulationRules.Count];
        private static readonly Color HydraBody = new Color(.09f,.12f,.07f,1);
        private static readonly Color HydraEdge = new Color(.60f,.64f,.42f,1);
        private static readonly Color HydraCore = new Color(.82f,.78f,.55f,1);

        public static Sprite HydraPopulation(int kind)
        {
            if (!HydraPopulationRules.IsValid(kind)) return null;
            if (HydraPopulationSprites[kind] != null) return HydraPopulationSprites[kind];
            // Approved browser anatomical geometry, normalized at an authored radius of48.
            var c = new RasterCanvas(58, 0, 128);
            switch ((HydraPopulationKind)kind)
            {
                case HydraPopulationKind.Cleft:
                    for (var side = -1; side <= 1; side += 2)
                    {
                        HydraPolygon(c, side, .08f * side, 0, .05f,-.52f,.29f,-.83f,.36f,-.39f,.79f,-.49f,.57f,-.12f,.87f,.16f,.46f,.25f,.40f,.73f,.15f,.44f,.02f,.52f,.13f,.12f,.02f,-.13f);
                        HydraEye(c, side * .37f, 0, .12f);
                    }
                    break;
                case HydraPopulationKind.Hook:
                    HydraPolygon(c,1,0,0,-.2f,-.5f,.03f,-.72f,.21f,-.34f,.4f,-.22f,.22f,.13f,.32f,.43f,-.03f,.32f,-.3f,.68f,-.32f,.2f,-.56f,.03f,-.32f,-.15f);
                    HydraPolygon(c,1,.19f,-.2f,0,0,.16f,-.4f,.58f,-.62f,.8f,-.22f,.71f,.27f,.39f,.43f,.55f,.06f,.45f,-.18f,.32f,-.2f,.27f,.08f);
                    HydraStroke(c,-.26f,-.07f,-.68f,-.32f); HydraStroke(c,-.68f,-.32f,-.81f,-.16f);
                    HydraEye(c,-.04f,-.07f,.13f); break;
                case HydraPopulationKind.Rachis:
                    for (var side = -1; side <= 1; side += 2)
                        for (var i = 0; i < 3; i++)
                            HydraPolygon(c,side,0,-.3f+i*.3f,.1f,-.09f,.48f,-.24f,.76f,-.03f,.31f,.03f,.12f,.14f);
                    HydraStroke(c,0,-.71f,0,.72f);
                    for (var i=0;i<4;i++) HydraPolygon(c,1,0,-.35f+i*.25f,0,-.11f,.13f,0,0,.12f,-.13f,0);
                    HydraPolygon(c,1,0,0,0,-.89f,.21f,-.58f,.12f,-.42f,-.12f,-.42f,-.21f,-.58f);
                    HydraPolygon(c,1,0,0,0,.93f,.1f,.64f,0,.54f,-.1f,.64f);
                    HydraEye(c,0,-.59f,.09f); break;
                case HydraPopulationKind.Bloat:
                    HydraPolygon(c,1,0,0,-.35f,-.21f,-.67f,.04f,-.64f,.49f,-.32f,.75f,.21f,.79f,.58f,.51f,.67f,.07f,.38f,-.26f,0,-.38f);
                    HydraStroke(c,-.38f,-.08f,-.45f,.25f); HydraStroke(c,-.45f,.25f,-.24f,.55f);
                    HydraStroke(c,.34f,-.04f,.43f,.3f); HydraStroke(c,.43f,.3f,.26f,.56f);
                    HydraEye(c,0,.22f,.2f);
                    HydraPolygon(c,1,0,0,-.3f,-.39f,-.4f,-.69f,-.15f,-.6f,0,-.92f,.16f,-.61f,.4f,-.71f,.29f,-.37f,0,-.25f);
                    HydraEye(c,0,-.52f,.085f); break;
                case HydraPopulationKind.Graft:
                    for(var i=0;i<5;i++)
                    {
                        c.SetRotation(i*Mathf.PI*2/5);
                        HydraStroke(c,0,-.27f,.06f,-.57f); HydraStroke(c,.06f,-.57f,.01f,-.91f);
                        HydraPolygon(c,1,0,0,-.1f,-.33f,.11f,-.32f,.15f,-.54f,.02f,-.63f,-.11f,-.5f);
                        HydraPolygon(c,1,.02f,-.57f,-.08f,-.06f,.1f,-.03f,.11f,-.24f,-.01f,-.36f,-.07f,-.2f);
                    }
                    c.SetRotation(0);
                    HydraPolygon(c,1,0,0,0,-.43f,.15f,-.2f,.4f,-.13f,.25f,.1f,.25f,.35f,0,.25f,-.25f,.35f,-.25f,.1f,-.4f,-.13f,-.15f,-.2f);
                    HydraEye(c,0,0,.13f); break;
                case HydraPopulationKind.Bastion:
                    HydraPolygon(c,1,0,0,-.6f,-.45f,-.22f,-.68f,.4f,-.64f,.69f,-.2f,.58f,.45f,.08f,.68f,-.55f,.48f,-.72f,0);
                    HydraPolygon(c,1,0,0,-.15f,-.55f,-.15f,-1,.15f,-1,.15f,-.55f);
                    HydraStroke(c,-.46f,-.3f,-.3f,-.48f); HydraStroke(c,.38f,.32f,.25f,.48f);
                    HydraEye(c,0,0,.14f); break;
                case HydraPopulationKind.Aegis:
                    HydraPolygon(c,1,0,0,0,-.72f,.35f,-.12f,.22f,.52f,0,.32f,-.22f,.52f,-.35f,-.12f);
                    for(var i=0;i<24;i++)
                    {
                        var a=Mathf.PI*(1.1f+.8f*i/24); var b=Mathf.PI*(1.1f+.8f*(i+1)/24);
                        c.DrawLine(HydraPoint(Mathf.Cos(a)*.79f,-.04f+Mathf.Sin(a)*.79f),HydraPoint(Mathf.Cos(b)*.79f,-.04f+Mathf.Sin(b)*.79f),5.5f,HydraEdge);
                    }
                    HydraStroke(c,0,.4f,0,.85f); HydraEye(c,0,-.03f,.14f); break;
                case HydraPopulationKind.Riftkin:
                    for(var side=-1;side<=1;side+=2)
                    {
                        HydraPolygon(c,side,side*.05f,0,.04f,-.8f,.53f,.47f,.13f,.32f,.05f,.62f);
                        HydraEye(c,side*.25f,.1f,.14f);
                    }
                    HydraStroke(c,-.37f,.63f,-.55f,.82f); HydraStroke(c,.37f,.63f,.55f,.82f); break;
                case HydraPopulationKind.Reclaimer:
                    HydraPolygon(c,1,0,0,-.48f,-.65f,-.13f,-.39f,.15f,-.39f,.49f,-.65f,.61f,.25f,.34f,.65f,-.36f,.65f,-.62f,.24f);
                    HydraPolygon(c,1,0,0,-.14f,.15f,-.18f,-.88f,.18f,-.88f,.14f,.15f);
                    HydraEye(c,0,.33f,.14f);
                    for(var i=0;i<3;i++) HydraStroke(c,-.13f+i*.13f,.75f,-.13f+i*.13f,.9f); break;
                case HydraPopulationKind.Broodsmith:
                    HydraPolygon(c,1,0,0,-.57f,-.37f,0,-.64f,.57f,-.3f,.59f,.45f,0,.65f,-.6f,.31f);
                    HydraPolygon(c,1,0,0,-.78f,-.05f,-.32f,-.05f,-.32f,.42f,-.78f,.42f);
                    HydraPolygon(c,1,0,0,.32f,-.05f,.78f,-.05f,.78f,.42f,.32f,.42f);
                    HydraStroke(c,0,-.45f,0,-.95f); HydraEye(c,0,-.05f,.14f); break;
            }
            return HydraPopulationSprites[kind] = c.ToSprite("VoidFall_"+HydraPopulationRules.StableId(kind));
        }
        public static void DestroyHydraPopulationSprites()
        {
            for (var i = 0; i < HydraPopulationSprites.Length; i++)
            {
                var sprite = HydraPopulationSprites[i];
                if (sprite == null) continue;
                var texture = sprite.texture;
                if (Application.isPlaying) { Object.Destroy(sprite); Object.Destroy(texture); }
                else { Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
                HydraPopulationSprites[i] = null;
            }
        }
        private static Vector2 HydraPoint(float x,float y) => new Vector2(x*48f,-y*48f);
        private static void HydraPolygon(RasterCanvas c,float mirror,float x,float y,params float[] points)
        {
            var polygon=new Vector2[points.Length/2];
            for(var i=0;i<polygon.Length;i++) polygon[i]=HydraPoint(x+points[i*2]*mirror,y+points[i*2+1]);
            c.FillPolygon(polygon,HydraBody); c.StrokePolygon(polygon,HydraEdge,2.4f);
        }
        private static void HydraStroke(RasterCanvas c,float x,float y,float a,float b) => c.DrawLine(HydraPoint(x,y),HydraPoint(a,b),2.2f,HydraEdge);
        private static void HydraEye(RasterCanvas c,float x,float y,float radius)
        {
            c.FillCircle(HydraPoint(x,y),radius*48f,HydraEdge);
            c.FillCircle(HydraPoint(x-radius*.22f,y-radius*.22f),radius*48f*.5f,HydraCore);
        }
    }
}
