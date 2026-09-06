Shader "VoidFall/FilamentGas"
{
    Properties
    {
        _MaskTex ("Destination-Out Mask", 2D) = "white" {}
        _Peak ("Source Peak Alpha", Range(0, 1)) = 0.34
        _PassCount ("Stacked Fill Passes", Float) = 11
        _Continuous ("Continuous Nebula", Float) = 0
        _GasColor ("Linear Gas Color", Vector) = (0.5,0.12,0.2,1)
        _CoreColor ("Linear Lit Filaments", Vector) = (0.8,0.4,0.2,1)
        _FlowPhase ("Ribbon Variation", Float) = 0
        _FlowTime ("Flow Time", Float) = 0
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
            #include "NebulaGas.hlsl"

            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);

            CBUFFER_START(UnityPerMaterial)
                half _Peak;
                half _PassCount;
                half _Continuous;
                half4 _GasColor;
                half4 _CoreColor;
                float _FlowPhase;
                float _FlowTime;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 flow : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 flow : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                output.flow = input.flow;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half remainingCoverage = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv).a;
                if (_Continuous > .5h)
                    return NebulaGas(input.flow, remainingCoverage, _Peak, _GasColor.rgb, _CoreColor.rgb, _FlowPhase, _FlowTime);
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
            #include "NebulaGas.hlsl"

            sampler2D _MaskTex;
            float _Peak;
            float _PassCount;
            float _Continuous;
            half4 _GasColor;
            half4 _CoreColor;
            float _FlowPhase;
            float _FlowTime;

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float2 flow : TEXCOORD1;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float2 flow : TEXCOORD1;
            };

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color;
                output.texcoord = input.texcoord;
                output.flow = input.flow;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                half remainingCoverage = tex2D(_MaskTex, input.texcoord).a;
                if (_Continuous > .5h)
                    return NebulaGas(input.flow, remainingCoverage, _Peak, _GasColor.rgb, _CoreColor.rgb, _FlowPhase, _FlowTime);
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
