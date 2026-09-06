Shader "VoidFall/CourtTile"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _HazardStage ("Hazard Stage", Float) = 0
        _WhiteActive ("White Active", Float) = 1
        _Pulse ("Pulse", Range(0,1)) = .55
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _HazardStage;
                float _WhiteActive;
                float _Pulse;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionHCS:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes i)
            {
                Varyings o; o.positionHCS=TransformObjectToHClip(i.positionOS); o.color=i.color; o.uv=i.uv; return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float2 uv=i.uv;
                float d=min(min(uv.x,1-uv.x),min(uv.y,1-uv.y));
                float aa=max(fwidth(d),.001);
                float white=step(.3,i.color.r);
                half3 base=i.color.rgb;
                // Subtle bevels retain the graphic black/white language.
                float edge=1-smoothstep(.008,.018,d);
                half3 color=base*(.94+.06*(1-uv.y));
                color=lerp(color,base*.53,edge*.75);
                float lip=smoothstep(.018-aa,.018+aa,d)*(1-smoothstep(.026-aa,.026+aa,d));
                color+=lip*.045;
                bool active=_HazardStage>.5 && abs(white-_WhiteActive)<.5;
                if(active)
                {
                    // No red fill or inversion: limited brightness pulses plus persistent danger markings.
                    color=lerp(color,half3(.83,.87,.89),_Pulse*.045);
                    half3 signal=lerp(half3(.72,.8,.86),half3(.07,.11,.15),white);
                    float border=smoothstep(.042-aa,.042+aa,d)*(1-smoothstep(.061-aa,.061+aa,d));
                    color=lerp(color,signal,border*.85);
                    float pulseBorder=smoothstep(.075-aa,.075+aa,d)*(1-smoothstep(.091-aa,.091+aa,d));
                    color=lerp(color,signal,pulseBorder*(.2+.65*_Pulse));
                    if(_HazardStage>1.5)
                    {
                        float pattern=frac((uv.x-uv.y)*5);
                        float hatch=(1-smoothstep(.04,.08,pattern))*step(.37,uv.y)*step(uv.y,.6)*step(.15,uv.x)*step(uv.x,.85);
                        color=lerp(color,signal,hatch*.55);
                    }
                }
                return half4(color,i.color.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
