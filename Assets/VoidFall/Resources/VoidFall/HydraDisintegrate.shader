Shader "VoidFall/HydraDisintegrate"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _DamageProgress ("Damage Progress", Range(0, 1)) = 0
        _PixelCells ("Pixel Cells", Range(16, 128)) = 64
        _ToxicColor ("Toxic Edge", Color) = (0.45, 1, 0.22, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _ToxicColor;
                float _DamageProgress;
                float _PixelCells;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS);
                output.color = input.color * _Color;
                output.uv = input.uv;
                return output;
            }

            float CellHash(float2 cell)
            {
                return frac(sin(dot(cell, float2(12.9898, 78.233))) * 43758.5453);
            }

            float RegionThreshold(float2 uv, float noise)
            {
                float2 eye = (uv - float2(0.5, 0.52)) / float2(0.13, 0.17);
                if (dot(eye, eye) <= 1.0) return 0.92 + noise * 0.08;
                if (uv.y >= 0.84) return noise * 0.18;
                if (uv.y >= 0.48)
                    return uv.x >= 0.5 ? 0.18 + noise * 0.20 : 0.38 + noise * 0.20;
                return uv.x >= 0.5 ? 0.58 + noise * 0.17 : 0.75 + noise * 0.17;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 sample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                clip(sample.a - 0.001);
                float cells = max(1.0, _PixelCells);
                float2 cell = floor(input.uv * cells);
                float threshold = RegionThreshold(input.uv, CellHash(cell));
                float remaining = threshold - saturate(_DamageProgress);
                clip(remaining);
                float edge = 1.0 - smoothstep(0.0, 0.035, remaining);
                sample.rgb = lerp(sample.rgb, _ToxicColor.rgb, edge * 0.85);
                return sample;
            }
            ENDHLSL
        }
    }
}
