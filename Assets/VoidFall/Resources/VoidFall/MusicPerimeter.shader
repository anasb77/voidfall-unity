Shader "UI/VoidFallMusicPerimeter"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Bands ("Bands", Vector) = (0,0,0,0)
        _State ("State", Vector) = (0,0,0,1)
        _Accent ("Accent", Vector) = (0,0,0,0)
        _FrameRect ("Frame size", Vector) = (1600,900,78,2)
        _Motion ("Travel, lap, seed, active", Vector) = (0,1,0,0)
        _BandMapping ("Band assignment", Vector) = (0,1,2,0)
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha One
        ColorMask [_ColorMask]
        Pass
        {
            Name "ReactivePerimeter"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            struct appdata_t { float4 vertex:POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float2 side:TEXCOORD1; };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float side:TEXCOORD1; };
            float4 _Bands, _State, _Accent, _FrameRect, _Motion, _BandMapping;
            float4 _Spectrum[6];
            v2f vert(appdata_t v)
            {
                v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.color=v.color; o.uv=v.uv; o.side=v.side.x; return o;
            }
            float hash(float x) { return frac(sin(x*12.9898+_Motion.z)*43758.5453); }
            float band(float group) { return group<.5?_Bands.x:group<1.5?_Bands.y:_Bands.z; }
            float spectrum(int index)
            {
                index=index%24; int pack=index/4; int component=index-pack*4;
                return _Spectrum[pack][component];
            }
            float rectDistance(float2 localPoint,float2 halfSize)
            {
                float2 q=abs(localPoint)-halfSize;
                return length(max(q,0))+min(max(q.x,q.y),0);
            }
            float3 neon(float distance,float3 tint,float energy,float intensity)
            {
                float outside=max(0,distance);
                float core=1-smoothstep(-.6,1.25,distance);
                float halo=exp(-outside/(3.5+energy*4.0))*.43;
                float bloom=exp(-outside/(11+energy*8))*.14;
                return (tint*(halo+bloom)+lerp(tint,float3(.96,.92,1),.7)*core)*intensity;
            }
            float centre(int index,int family)
            {
                if(family==1) return index==0?.24:.73;
                if(family==3) return index==0?.15:index==1?.36:index==2?.66:.85;
                if(family==2) return index==0?.16:index==1?.43:.77;
                return index==0?.17:index==1?.48:.81;
            }
            float3 runners(float position,float inward,float perimeter,float travel,float stack,float3 tint,float direction)
            {
                float spacing=perimeter/5;
                float offset=hash(direction>0?83:197)*perimeter;
                float behind=frac((direction*(offset+direction*travel-position)+spacing*.5)/spacing)*spacing-spacing*.5;
                float tail=100+stack*22;
                float brightness=pow(saturate(1-max(0,behind)/tail),2.0)*exp(-max(0,-behind)/8);
                float coreDistance=abs(inward-11)-(1.6+stack*.28);
                float3 result=neon(coreDistance,tint,_Bands.x,brightness*(.8+stack*.055));
                float head=exp(-abs(behind)/4);
                return result+neon(abs(inward-11)-2.3,float3(.92,.95,1),.5,head*.8);
            }
            fixed4 frag(v2f input):SV_Target
            {
                float intensity=saturate(_Bands.w*_State.w)*input.color.a;
                clip(intensity-.001);
                int side=(int)(input.side+.1);
                bool horizontal=side==0||side==2;
                float length=horizontal?_FrameRect.x:_FrameRect.y;
                float along=input.uv.x*length, inward=input.uv.y;
                float stack=min(12,max(1,_Accent.y));
                float reduced=_Accent.z;
                float gain=.60+(stack-1)*.34;
                float motion=lerp(1,.30,reduced);
                int family=(int)_Accent.w;
                float3 cyan=float3(.20,.78,1), pink=float3(.96,.16,1), violet=float3(.66,.27,1);
                float3 tint=side==0||side==1?pink:cyan;
                float3 color=neon(abs(inward-8)-.5,violet,_Bands.x,.13);
                int count=horizontal?2:family==1?2:family==3?4:3;
                [unroll] for(int i=0;i<4;i++)
                {
                    if(i>=count) continue;
                    float jitter=(hash(i+side*13)-.5)*.05;
                    float anchor=horizontal?(i==0?.28:.72):lerp(.13,.88,centre(i,family));
                    if(!horizontal&&family==2&&side==3) anchor+=.035;
                    float center=(anchor+jitter)*length;
                    float assigned=i%3==0?_BandMapping.x:i%3==1?_BandMapping.y:_BandMapping.z;
                    float response=min(1.6,band(assigned)*gain)*motion;
                    float baseLength=length*(horizontal?.21:family==1?.22:family==3?.105:.16);
                    baseLength*=lerp(.86,1.12,hash(i+side*29+7));
                    float railLength=baseLength*min(1,.42+response*.58);
                    float distance=rectDistance(float2(along-center,inward-11),float2(railLength*.5,1.5+response));
                    color+=neon(distance,tint,response,.72+response*.48);
                    // The analyser's real 24-bin spectrum forms short teeth attached to each rail.
                    float teeth=_FrameRect.w<.5?12:horizontal?24:20;
                    float local=along-center+railLength*.5;
                    float cell=railLength/teeth;
                    int index=(int)floor(local/max(1,cell));
                    if(local>=0&&local<railLength)
                    {
                        float level=spectrum((index+i*3+side*4+240)%24)*motion;
                        float height=2.97*(1.5+level*(3.5+stack*1.5));
                        float x=frac(local/max(1,cell))*cell-cell*.5;
                        float tooth=rectDistance(float2(x,inward-(18+height*.5)),float2(min(cell*.38,1.89),height*.5));
                        color+=neon(tooth,tint,level,.24+level*.66)*.82;
                    }
                    if(stack>=3)
                    {
                        float echo=rectDistance(float2(along-center,inward-(24+response*3)),float2(railLength*.36,.65));
                        color+=neon(echo,violet,response,.13+response*.15);
                    }
                }
                if(_Motion.w>.5)
                {
                    float perimeter=2*(_FrameRect.x+_FrameRect.y);
                    float offset=side==0?0:side==1?_FrameRect.x:side==2?_FrameRect.x+_FrameRect.y:2*_FrameRect.x+_FrameRect.y;
                    float position=offset+along;
                    color+=runners(position,inward,perimeter,_Motion.x,stack,cyan,1);
                    color+=runners(position,inward,perimeter,_Motion.x,stack,pink,-1);
                    if(_Motion.y<1&&reduced<.5)
                    {
                        float head=_Motion.y*perimeter;
                        float behind=frac((head-position)/perimeter)*perimeter;
                        float lap=pow(saturate(1-behind/(270+stack*20)),1.5);
                        lap*=saturate(_Motion.y*9)*saturate((1-_Motion.y)*6);
                        color+=neon(abs(inward-11)-3,float3(.9,.72,1),1,lap*1.4);
                    }
                }
                color*=1-smoothstep(_FrameRect.z-18,_FrameRect.z,inward);
                return fixed4(min(color,2.5),intensity);
            }
            ENDCG
        }
    }
}
