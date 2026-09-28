Shader "JapanMarket/Shelf Placement Celebration"
{
    Properties
    {
        [HDR] _GlowColorA("Fresh Glow", Color) = (0.26, 1.0, 0.72, 1.0)
        [HDR] _GlowColorB("Golden Spark", Color) = (1.0, 0.58, 0.14, 1.0)
        _Intensity("Intensity", Range(0, 1)) = 0
        _EffectProgress("Effect Progress", Range(0, 1)) = 0
        _Impact("Impact", Range(0, 1)) = 0
        _SweepY("Sweep World Y", Float) = 0
        _SweepWidth("Sweep Width", Float) = 0.1
        _RimPower("Rim Power", Range(1, 8)) = 3.5
        _ShellWidth("Shell Width", Range(0, 0.01)) = 0.0007
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+20"
            "RenderPipeline" = "UniversalPipeline"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
        };

        struct Varyings
        {
            float4 positionHCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
        };

        CBUFFER_START(UnityPerMaterial)
            half4 _GlowColorA;
            half4 _GlowColorB;
            float _Intensity;
            float _EffectProgress;
            float _Impact;
            float _SweepY;
            float _SweepWidth;
            float _RimPower;
            float _ShellWidth;
        CBUFFER_END

        float Hash31(float3 value)
        {
            value = frac(value * 0.1031);
            value += dot(value, value.yzx + 33.33);
            return frac((value.x + value.y) * value.z);
        }

        Varyings Vert(Attributes input)
        {
            Varyings output;
            float expansion = _ShellWidth * (1.0 + _Impact);
            float3 expandedPositionOS = input.positionOS.xyz + input.normalOS * expansion;
            output.positionWS = TransformObjectToWorld(expandedPositionOS);
            output.positionHCS = TransformWorldToHClip(output.positionWS);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            return output;
        }

        half4 Frag(Varyings input) : SV_Target
        {
            half3 normalWS = normalize(input.normalWS);
            half3 viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));

            half rim = pow(1.0h - saturate(dot(normalWS, viewDirectionWS)), _RimPower);
            float width = max(_SweepWidth, 0.0001);
            float sweepDistance = (input.positionWS.y - _SweepY) / width;
            half sweep = pow(saturate(1.0 - abs(sweepDistance)), 3.0);
            half softTrail = pow(saturate(1.0 - abs(sweepDistance + 1.5) * 0.55), 2.0) * 0.12h;

            float3 sparkleCell = floor(input.positionWS * 55.0 + _EffectProgress * float3(5.0, 13.0, 7.0));
            half sparkle = step(0.975, Hash31(sparkleCell));
            sparkle *= saturate(sweep * 1.3 + rim * 0.18);

            half goldMix = saturate(sweep * 0.72 + softTrail * 0.2);
            half3 glowColor = lerp(_GlowColorA.rgb, _GlowColorB.rgb, goldMix);

            half glow = (rim * 0.24h + sweep * 1.05h + softTrail + sparkle * 1.1h
                + _Impact * 0.08h) * _Intensity;

            return half4(glowColor * (0.42h + glow), saturate(glow * 0.72h));
        }
        ENDHLSL

        Pass
        {
            Name "ShelfPlacementGlow"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend SrcAlpha One
            Cull Back
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0
            ENDHLSL
        }
    }

    FallBack Off
}
