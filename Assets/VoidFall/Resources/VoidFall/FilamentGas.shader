Shader "VoidFall/FilamentGas"
{
    Properties
    {
        _MaskTex ("Destination-Out Mask", 2D) = "white" {}
        _Peak ("Source Peak Alpha", Range(0, 1)) = 0.34
        _PassCount ("Stacked Fill Passes", Float) = 11
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
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

            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);

            CBUFFER_START(UnityPerMaterial)
                half _Peak;
                half _PassCount;
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
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half remainingCoverage = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv).a;
                half target = saturate(_Peak * remainingCoverage);
                half passAlpha = 1.0h - pow(max(0.0h, 1.0h - target), 1.0h / max(1.0h, _PassCount));
                half alpha = saturate(input.color.a * passAlpha);
                return half4(input.color.rgb, alpha);
            }
            ENDHLSL
        }
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MaskTex;
            float _Peak;
            float _PassCount;

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color;
                output.texcoord = input.texcoord;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                half remainingCoverage = tex2D(_MaskTex, input.texcoord).a;
                half target = saturate(_Peak * remainingCoverage);
                half passAlpha = 1.0h - pow(max(0.0h, 1.0h - target), 1.0h / max(1.0h, _PassCount));
                half alpha = saturate(input.color.a * passAlpha);
                return half4(input.color.rgb, alpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
