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
                // The browser stacks eleven source-over fills, then applies
                // destination-out. Invert that stack per fragment so the
                // mask reaches peak * remainingCoverage exactly.
                fixed remainingCoverage = tex2D(_MaskTex, input.texcoord).a;
                fixed target = saturate(_Peak * remainingCoverage);
                fixed passCount = max(1.0, _PassCount);
                fixed passAlpha = 1.0 - pow(max(0.0, 1.0 - target), 1.0 / passCount);
                fixed alpha = saturate(input.color.a * passAlpha);
                return fixed4(input.color.rgb, alpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
