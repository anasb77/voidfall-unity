Shader "UI/VoidFallMusicPerimeter"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Bands ("Bands", Vector) = (0,0,0,0)
        _State ("State", Vector) = (0,0,0,1)
        _Accent ("Accent", Vector) = (0,0,0,0)
        _TimeValue ("Time", Float) = 0
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
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
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
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 group : TEXCOORD1;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 group : TEXCOORD0;
                float2 screen : TEXCOORD1;
            };

            float4 _Bands;
            float4 _State;
            float4 _Accent;
            float _TimeValue;

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color;
                output.group = input.group;
                output.screen = ComputeScreenPos(output.vertex).xy;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float group = input.group.x;
                float response = group < 0.5 ? _Bands.x : group < 1.5 ? _Bands.y : _Bands.z;
                float intensity = saturate(_Bands.w * _State.w);
                float tier = _Accent.x;
                float reduced = _Accent.z;
                float motion = lerp(1.0, 0.18, reduced);
                float travel = 0.5 + 0.5 * sin((_TimeValue * 4.0 + input.screen.x * 9.0 + input.screen.y * 7.0) * motion);
                float peak = lerp(0.28, 1.0, response) + _State.x * lerp(0.25, travel, motion);
                float3 cyan = float3(0.02, 0.88, 1.0);
                float3 magenta = float3(1.0, 0.025, 0.68);
                float3 violet = float3(0.54, 0.12, 1.0);
                float3 whiteHot = float3(1.0, 0.94, 0.82);
                float tier2 = saturate(tier - 1.0);
                float tier3 = saturate(tier - 2.0);
                float3 groupPalette = group < 0.5 ? cyan : group < 1.5 ? magenta : violet;
                float3 palette = lerp(cyan, groupPalette, tier2 * 0.92);
                palette = lerp(palette, whiteHot, tier3 * saturate(response * 0.52 + _State.x * 0.24));
                if (group > 1.5 && tier3 > 0.5)
                    palette = lerp(palette, float3(1.0, 0.56, 0.08), 0.12 + response * 0.08);
                palette = lerp(palette, float3(0.72, 0.025, 0.08), _State.y * (0.16 + travel * 0.16));
                float inward = lerp(1.0, 0.78 + travel * 0.12, _State.z);
                float alpha = input.color.a * intensity * peak * inward;
                alpha *= input.group.y > 0.5 ? 1.0 : lerp(0.34, 0.7, intensity);
                return fixed4(palette * (0.65 + intensity * 1.2), saturate(alpha));
            }
            ENDCG
        }
    }
}
