Shader "VoidFall/ParticleAdditive"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }
        Blend One One
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_particles
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;

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
                output.color = input.color * _Color;
                output.texcoord = input.texcoord;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 sample = tex2D(_MainTex, input.texcoord);
                float radius = saturate(distance(input.texcoord, float2(0.5, 0.5)) * 2.0);
                float tintStart = 0.5 / 12.0;
                float tintEnd = 3.6 / 12.0;
                float tintAmount = saturate((radius - tintStart) / (tintEnd - tintStart));
                fixed3 dotColor = lerp(fixed3(1, 1, 1), input.color.rgb, tintAmount);
                fixed alpha = sample.a * input.color.a;
                // Blend One One expects premultiplied colour. This matches the
                // browser canvas "lighter" contribution at the soft edge.
                return fixed4(dotColor * alpha, alpha);
            }
            ENDCG
        }
    }
}
