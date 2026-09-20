Shader "VoidFall/CourtTile"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _HazardStage ("Hazard Stage", Float) = 0
        _WhiteActive ("White Active", Float) = 1
        _Pulse ("Pulse", Range(0,1)) = .55
        _BurstProgress ("Burst progress", Range(0,1)) = 0
        _ReducedMotion ("Reduced motion", Float) = 0
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
                float _BurstProgress;
                float _ReducedMotion;
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
                // Matte stone: broad lighting, fine grain, inset scoring and chipped seams.
                float grain=frac(sin(dot(floor(uv*220),float2(12.9898,78.233)))*43758.5453);
                color+=(grain-.5)*.003;
                float seam=1-smoothstep(.002,.007,abs(d-.07));
                color=lerp(color,base*.7,seam*.23);
                float scratch=1-smoothstep(.001,.005,abs(uv.y-.18-uv.x*.025));
                color=lerp(color,base*.6,scratch*.10);
                if (_HazardStage > .5)
                {
                    // Per-cell property blocks authorize this fill; there is no global color toggle.
                    float warning = _HazardStage < 1.5 ? 1 : 0;
                    float fill = warning > .5 ? .23 + .27 * _Pulse : .79;
                    color = lerp(color, half3(.91,.18,.16), fill);
                    float2 centre = uv - .5;
                    float radius = length(centre);
                    float angle = atan2(centre.y,centre.x) + 3.14159265;
                    float segment = frac(angle * (6.0 / 6.2831853));
                    float radialAA = max(fwidth(radius), .002);
                    float ring = (1-smoothstep(.009,.009+radialAA,abs(radius-.34))) * step(.095,segment) * step(segment,.76);
                    float cross = max((1-step(.055,abs(centre.x)))*(1-step(.011,abs(centre.y))),
                                      (1-step(.011,abs(centre.x)))*(1-step(.055,abs(centre.y))));
                    half3 marker = lerp(half3(.984,.573,.235),half3(1,.969,.929),step(.8,_Pulse));
                    color = lerp(color,marker,saturate(ring+cross)*warning*.95);
                    float burst = (1-smoothstep(.025,.045,abs(radius-.34)))* (1-warning);
                    color = lerp(color,half3(1,.78,.51),burst*.85);
                    float border=1-smoothstep(.018,.045,d);
                    color=lerp(color,half3(1,.48,.19),border*(warning>.5?.6+.4*_Pulse:1));
                    float crack=1-smoothstep(.007,.017,abs(centre.x-sin(centre.y*28)*.07));
                    crack=max(crack,1-smoothstep(.005,.012,abs(centre.y-centre.x*.65-sin(centre.x*35)*.035)));
                    color=lerp(color,half3(1,.73,.40),crack*(warning>.5?.25+.5*_Pulse:1));
                    float squareDistance=max(abs(centre.x),abs(centre.y));
                    float shock=1-smoothstep(.016,.04,abs(squareDistance-lerp(.06,.62,_BurstProgress)));
                    color=lerp(color,half3(1,.91,.73),shock*(1-warning)*(1-_ReducedMotion));
                    float flash=(1-smoothstep(0,.17,_BurstProgress))*(1-warning)*(1-_ReducedMotion);
                    color=lerp(color,half3(1,.94,.79),flash*.85);
                }
                return half4(color,i.color.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
