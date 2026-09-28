Shader "JapanMarket/Shelf Placement Burst"
{
    Properties
    {
        [HDR] _GlowColorA("Fresh Glow", Color) = (0.2, 1.0, 0.72, 1.0)
        [HDR] _GlowColorB("Golden Spark", Color) = (1.0, 0.55, 0.08, 1.0)
        _Intensity("Intensity", Range(0, 1)) = 0
        _EffectProgress("Effect Progress", Range(0, 1)) = 0
        _Impact("Impact", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+15"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ShelfPlacementBurst"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend SrcAlpha One
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _GlowColorA;
                half4 _GlowColorB;
                float _Intensity;
                float _EffectProgress;
                float _Impact;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.uv = input.uv;
                return output;
            }

            half Ring(float radius, float target, float width)
            {
                return 1.0h - smoothstep(width * 0.35h, width, abs(radius - target));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 centered = input.uv * 2.0 - 1.0;
                float radius = length(centered);
                float angle = atan2(centered.y, centered.x);

                half outerRing = Ring(radius, 0.76, lerp(0.065, 0.028, _EffectProgress));
                half glitter = pow(saturate(sin(angle * 17.0 + radius * 31.0
                    - _EffectProgress * 18.0) * 0.5 + 0.5), 42.0);
                glitter *= outerRing * 0.22h;

                half3 color = lerp(_GlowColorA.rgb, _GlowColorB.rgb, saturate(radius * 0.72));

                half energy = outerRing * 0.72h + glitter;
                energy *= _Intensity;
                return half4(color * (0.46h + energy), saturate(energy * 0.7h));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
