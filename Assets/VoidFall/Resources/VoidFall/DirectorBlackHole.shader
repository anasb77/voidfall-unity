Shader "VoidFall/DirectorBlackHole"
{
    Properties
    {
        _Backdrop ("Background Only", 2D) = "black" {}
        _BackdropWorldRect ("Background World Rect", Vector) = (0,0,1,1)
        _Center ("World Center", Vector) = (0,0,0,0)
        _HasBackdrop ("Has Background", Float) = 0
        _Strength ("Incident Strength", Range(0,1)) = 0
        _Warning ("Warning Ring", Float) = 0
        _ReducedEffects ("Reduced Effects", Float) = 0
        _HighContrast ("High Contrast", Float) = 0
        _RimColor ("Rim", Color) = (.49,.7,1,1)
        _HoleSize ("Source Hole Size", Range(.1,.9)) = .65
        _Smoothness ("Source Transition Percent", Range(1,15)) = 5
        _DistortionStrength ("Source Distortion Fresnel Power", Range(.1,4)) = .8
        _OuterRing ("Source Outer Ring Power", Range(1,12)) = 6
        _RadiusPower ("Source Radius Fresnel Power", Range(.05,1)) = .15
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Blend One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_Backdrop);
            SAMPLER(sampler_Backdrop);
            CBUFFER_START(UnityPerMaterial)
                float4 _BackdropWorldRect;
                float4 _Center;
                half4 _RimColor;
                float _HasBackdrop, _Strength, _Warning, _ReducedEffects, _HighContrast;
                float _HoleSize, _Smoothness, _DistortionStrength, _OuterRing, _RadiusPower;
            CBUFFER_END

            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; float2 worldXY:TEXCOORD1; };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(i.positionOS);
                o.positionHCS = TransformWorldToHClip(world);
                o.worldXY = world.xy;
                o.uv = i.uv;
                return o;
            }

            half4 Frag(Varyings i):SV_Target
            {
                float r = length(i.uv * 2 - 1);
                float aa = max(fwidth(r), .001);
                float edgeFade = 1 - smoothstep(.94, 1, r);
                if (_Warning > .5)
                {
                    // A static, bounded ring marks the actual attraction extent before any pull.
                    float ring = 1 - smoothstep(.009, .009 + aa * 1.5, abs(r - .91));
                    float ticks = pow(abs(cos(atan2(i.uv.y - .5, i.uv.x - .5 + .000001) * 6)), 24);
                    float brackets = ticks * (1 - smoothstep(.026, .026 + aa, abs(r - .87)));
                    float alpha = saturate(ring * .88 + brackets * .52);
                    return half4(_RimColor.rgb * alpha, alpha);
                }

                // Adapted from the supplied BlackHole.shadergraph (retained as source text).
                // Its Fresnel requires a curved surface: synthesize a hemisphere's NdotV on this XY quad.
                float ndotv = sqrt(saturate(1 - r * r));
                float radiusFresnel = pow(saturate(1 - ndotv), max(_RadiusPower, .01));
                float width = max(_Smoothness / 100, aa);
                // Source inverted smoothstep(1-HoleSize+width, 1-HoleSize-width, 1-Fresnel)
                // is expressed with ascending edges, which is well-defined on every backend.
                float outsideCore = smoothstep(_HoleSize - width, _HoleSize + width, radiusFresnel);
                float core = 1 - outsideCore;
                float distortionFresnel = pow(saturate(1 - ndotv), max(_DistortionStrength, .01));
                float distortionMask = pow(saturate(1 - distortionFresnel), max(_OuterRing, 1));
                float2 size = max(_BackdropWorldRect.zw, float2(.0001, .0001));
                float2 uv = (i.worldXY - _BackdropWorldRect.xy) / size;
                float2 centerUV = (_Center.xy - _BackdropWorldRect.xy) / size;
                // Source UV + mask*(1-2*UV), replacing .5 screen center with the event's world center.
                // A bounded .12 gain restrains the original distortion; reduced effects removes it.
                float2 distortedUV = uv + 2 * (centerUV - uv) * distortionMask * .12 * _Strength * (1 - _ReducedEffects);
                half3 backdrop = SAMPLE_TEXTURE2D(_Backdrop, sampler_Backdrop, saturate(distortedUV)).rgb;
                float sampleInside = step(0, distortedUV.x) * step(0, distortedUV.y) *
                    step(distortedUV.x, 1) * step(distortedUV.y, 1);
                float distortionAlpha = _HasBackdrop * sampleInside * distortionMask * outsideCore *
                    edgeFade * _Strength * (1 - _ReducedEffects);
                float coreAlpha = core * .98 * _Strength;
                float rim = 4 * outsideCore * (1 - outsideCore);
                float rimAlpha = rim * lerp(.68, .94, _HighContrast) * _Strength;
                // Compose premultiplied layers; only supplied background samples can be refracted.
                half3 color = backdrop * distortionAlpha;
                float alpha = distortionAlpha;
                color = color * (1 - coreAlpha) + half3(.002, .003, .009) * coreAlpha;
                alpha = alpha + coreAlpha * (1 - alpha);
                color = color * (1 - rimAlpha) + _RimColor.rgb * rimAlpha;
                alpha = alpha + rimAlpha * (1 - alpha);
                // Retain a faint radius boundary during pull, so the small visual core is not mistaken for its extent.
                float boundary = (1 - smoothstep(.004, .004 + aa, abs(r - .91))) * .16 * _Strength;
                color = color * (1 - boundary) + _RimColor.rgb * boundary;
                alpha = alpha + boundary * (1 - alpha);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
