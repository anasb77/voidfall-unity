using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VoidFall.UI
{
    /// <summary>The approved 1200 x 760 crossing composition, independent of combat camera zoom/post effects.</summary>
    public sealed class DealerRoomView : MonoBehaviour
    {
        public RectTransform Stage { get; private set; }
        public DealerPortraitView Portrait { get; private set; }
        private RectTransform _canvas, _rings, _shadow;
        private DealerRoomDust _dust;
        private Text _wallet, _prompt, _from;
        private readonly RawImage[] _portals = new RawImage[2], _pads = new RawImage[2];
        private readonly Text[] _names = new Text[2], _next = new Text[2];
        private readonly Dictionary<SpriteRenderer, RawImage> _player = new Dictionary<SpriteRenderer, RawImage>();
        private RectTransform _playerLayer;
        private static Font _displayFont, _bodyFont;

        public static DealerRoomView Create(Canvas canvas)
        {
            var view = canvas.gameObject.AddComponent<DealerRoomView>(); view.Build(canvas); return view;
        }
        private void Build(Canvas canvas)
        {
            _canvas = (RectTransform)canvas.transform;
            // RawImage's plain white texture avoids inherited combat tonemapping and generated sprite tinting.
            var night = UIBuilder.Stretch(UIBuilder.CreateRect(transform, "Crossing Night")).gameObject.AddComponent<RawImage>();
            night.texture = Texture2D.whiteTexture; night.color = UITheme.Hex("#05070c"); night.raycastTarget = false;
            Stage = UIBuilder.CreateRect(transform, "Preview Coordinate Space");
            Stage.anchorMin = Stage.anchorMax = Vector2.one * .5f; Stage.sizeDelta = new Vector2(1200,760);
            _rings = Art(Stage,"Mist Orbits","room-rings",new Vector2(1400,1400),new Vector2(0,20)).rectTransform;
            _dust = UIBuilder.CreateRect(Stage,"Orbital Stars and Floating Edges").gameObject.AddComponent<DealerRoomDust>();
            _dust.rectTransform.sizeDelta = new Vector2(1200,760); _dust.raycastTarget = false;
            Art(Stage,"Authored Crossing Floor","room-floor",new Vector2(1200,760),Vector2.zero);
            _shadow = Art(Stage,"Dealer Shadow","room-shadow",new Vector2(240,100),Vector2.zero).rectTransform;
            for(var i=0;i<2;i++)
            {
                var position = new Vector2(i == 0 ? -310 : 310,-25);
                _pads[i] = Art(Stage,"Portal Pad "+i,"room-portal-pad",new Vector2(300,300),position);
                _portals[i] = Art(Stage,"Portal Animation "+i,null,new Vector2(167,167),position);
                _names[i] = Label(Stage,"Destination "+i,"",20,Color.white,new Vector2(position.x,-145),new Vector2(240,30));
                _next[i] = Label(Stage,"Destination Hint "+i,"N E X T   V O I D",10,UITheme.Hex("#83919f"),new Vector2(position.x,-174),new Vector2(240,20),false);
            }
            Portrait = DealerPortraitView.Create(Stage,"Hovering Dealer",9.96f,new Vector2(422.4f,390));
            Portrait.rectTransform.pivot = new Vector2(.5f,1);
            _playerLayer = UIBuilder.CreateRect(Stage,"Zack Above Dealer"); _playerLayer.sizeDelta = new Vector2(1200,760);
            _prompt = Label(Stage,"Browse Prompt","<b>[ E ]</b>   Browse",13,UITheme.Hex("#dbeef4"),Vector2.zero,new Vector2(300,30),false);
            _from = Label(transform,"Cleared Void","",10,UITheme.Hex("#6b8293"),Vector2.zero,new Vector2(400,20));
            TopLeft(_from.rectTransform,36,30);
            var title=Label(transform,"Crossing Title","BETWEEN VOIDS",25,UITheme.TextBody,Vector2.zero,new Vector2(430,36));
            TopLeft(title.rectTransform,36,48);
            _wallet=Label(transform,"Run Scraps","",23,UITheme.GoldLight,Vector2.zero,new Vector2(250,40));
            _wallet.alignment=TextAnchor.UpperRight; _wallet.rectTransform.anchorMin=_wallet.rectTransform.anchorMax=Vector2.one;
            _wallet.rectTransform.pivot=Vector2.one; _wallet.rectTransform.anchoredPosition=new Vector2(-36,-30);
            var help=Label(transform,"Crossing Controls","<b>WASD / arrows</b> to move  ·  <b>E</b> to browse\nWalk into a portal to choose your next Void.",12,UITheme.Hex("#8293a2"),Vector2.zero,new Vector2(500,50),false);
            help.alignment=TextAnchor.LowerLeft; help.rectTransform.anchorMin=help.rectTransform.anchorMax=Vector2.zero;
            help.rectTransform.pivot=Vector2.zero; help.rectTransform.anchoredPosition=new Vector2(36,26);
        }
        private static void TopLeft(RectTransform r,float x,float y)
        { r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y); r.GetComponent<Text>().alignment=TextAnchor.UpperLeft; }
        private static Text Label(Transform parent,string name,string value,int size,Color color,Vector2 position,Vector2 dimensions,bool display=true)
        {
            _displayFont ??= Font.CreateDynamicFontFromOSFont("Bahnschrift",24);
            _bodyFont ??= Font.CreateDynamicFontFromOSFont("Segoe UI",16);
            var text=UIBuilder.CreateText(parent,name,value,size,color,TextAnchor.MiddleCenter,display);
            text.font=display?_displayFont:_bodyFont; text.fontSize=size;
            text.rectTransform.anchorMin=text.rectTransform.anchorMax=Vector2.one*.5f;
            text.rectTransform.sizeDelta=dimensions; text.rectTransform.anchoredPosition=position; return text;
        }
        private static RawImage Art(Transform parent,string name,string asset,Vector2 size,Vector2 position)
        {
            var rect=UIBuilder.CreateRect(parent,name); rect.anchorMin=rect.anchorMax=Vector2.one*.5f;
            rect.sizeDelta=size; rect.anchoredPosition=position;
            var image=rect.gameObject.AddComponent<RawImage>(); image.raycastTarget=false;
            if(asset!=null)image.texture=Resources.Load<Texture2D>("VoidFall/Dealer/"+asset); return image;
        }
        private static void SetSprite(RawImage image,Sprite sprite)
        {
            if(sprite==null){image.enabled=false;return;} var rect=sprite.textureRect;var texture=sprite.texture;
            image.texture=texture;image.uvRect=new Rect(rect.x/texture.width,rect.y/texture.height,rect.width/texture.width,rect.height/texture.height);image.enabled=true;
        }
        public void Draw(Vector2 player,Vector2 dealer,int variation,bool smiling,bool reduced,bool canBrowse,int wallet,string from,string[] names,Color[] colors,Sprite[] portals,float time)
        {
            Stage.localScale=Vector3.one*Mathf.Min(_canvas.rect.width/1200,_canvas.rect.height/760);
            var bottom=dealer.y<0;
            Portrait.rectTransform.anchoredPosition=new Vector2(0,380-(bottom?330:8)+(reduced?0:Mathf.Sin(time*1.2f)*5));
            Portrait.SetPose(Mathf.Clamp((player.x-dealer.x)/400,-.65f,.65f),smiling,reduced,variation);
            _shadow.anchoredPosition=new Vector2(0,380-(bottom?715:370));
            _prompt.rectTransform.anchoredPosition=new Vector2(0,380-(bottom?740:434));
            _prompt.gameObject.SetActive(canBrowse);
            _wallet.text=wallet+" <size=11><color=#9d9780>Scraps</color></size>";
            _from.text=from.ToUpperInvariant()+" CLEARED / TRAVEL";
            _rings.localRotation=Quaternion.Euler(0,0,-(reduced?0:time*.055f*Mathf.Rad2Deg));
            _dust.TimeValue=reduced?0:time; _dust.SetVerticesDirty();
            for(var i=0;i<2;i++)
            {
                var visible=i<names.Length;
                _portals[i].gameObject.SetActive(visible);_pads[i].gameObject.SetActive(visible);_names[i].gameObject.SetActive(visible);_next[i].gameObject.SetActive(visible);
                if(!visible)continue;
                SetSprite(_portals[i],portals[i]);
                _portals[i].color=new Color(colors[i].r,colors[i].g,colors[i].b,.82f);_pads[i].color=colors[i];
                var near=Vector2.Distance(player,new Vector2(i==0?-310:310,-25))<125;
                _portals[i].rectTransform.sizeDelta=Vector2.one*(near?181:167);
                _names[i].text=names[i];_names[i].color=Color.Lerp(colors[i],Color.white,.25f);
            }
        }
        public void PresentPlayer(SpriteRenderer source)
        {
            if(source==null)return;
            if(!_player.TryGetValue(source,out var image))
            {image=Art(_playerLayer,source.name,null,Vector2.one,Vector2.zero);_player.Add(source,image);}
            image.gameObject.SetActive(source.enabled&&source.gameObject.activeInHierarchy&&source.sprite!=null);
            if(!image.gameObject.activeSelf)return;
            SetSprite(image,source.sprite);image.color=source.color;
            image.rectTransform.anchoredPosition=source.transform.position;
            // Match the preview's smaller eye while preserving purchased artwork and relative offsets.
            var size=source.sprite.bounds.size;var scale=source.transform.lossyScale;
            image.rectTransform.sizeDelta=new Vector2(size.x*Mathf.Abs(scale.x),size.y*Mathf.Abs(scale.y))*.5f;
            image.rectTransform.localRotation=source.transform.rotation;
        }
    }
    public sealed class DealerRoomDust : MaskableGraphic
    {
        public float TimeValue;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color32 tint)
            {var n=mesh.currentVertCount;mesh.AddVert(a,tint,Vector2.zero);mesh.AddVert(b,tint,Vector2.zero);mesh.AddVert(c,tint,Vector2.zero);mesh.AddVert(d,tint,Vector2.zero);mesh.AddTriangle(n,n+1,n+2);mesh.AddTriangle(n,n+2,n+3);}
            for(var i=0;i<125;i++)
            {
                var radius=145+(i*97.31f)%725;var angle=i*2.39996f+TimeValue*.055f*(.65f+i%4*.12f);
                var point=new Vector2(Mathf.Cos(angle)*radius,20-Mathf.Sin(angle)*radius);var r=i%7==0?1.3f:.6f;
                var tint=new Color32(197,233,255,(byte)((.1f+i%9*.025f)*255));
                Quad(point+new Vector2(-r,0),point+new Vector2(0,r),point+new Vector2(r,0),point+new Vector2(0,-r),tint);
            }
            for(var i=0;i<9;i++)
            {
                var side=i%2==1?-1:1;var p=new Vector2(side*(410+i*7),380-(570+i*12+Mathf.Sin(TimeValue*.6f+i)*4));
                Quad(p,p+new Vector2(side*20,-4),p+new Vector2(side*29,-19),p+new Vector2(side*8,-15),new Color32(24,42,55,255));
            }
        }
    }
}
