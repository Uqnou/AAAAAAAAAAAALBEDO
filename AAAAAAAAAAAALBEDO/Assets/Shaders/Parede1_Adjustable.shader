Shader "Custom/URP_Parede1_Adjustable"
{
    Properties
    {
        _LightColor ("Dot Tint Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _DarkColor ("Dark Background Color", Color) = (0.0, 0.0, 0.0, 1.0)
        _Scale ("Scale Factor", Float) = 0.07
        _AspectRatio ("Aspect Ratio (X/Y)", Float) = 0.55
        _VoronoiScale ("Voronoi Scale", Float) = 5.0
        _RampStart ("Ramp Start", Float) = 0.2681818
        _RampEnd ("Ramp End", Float) = 0.3000000
        _LightIntensityScale ("Light Sensitivity Scale", Float) = 1.0
        _MainLightWeight ("Directional Light Weight", Range(0.0, 1.0)) = 0.0
        _UnityLightWeight ("Scene Light Weight", Range(0.0, 2.0)) = 0.45
        _BeamIntensity ("Prism/GodRay Beam Weight", Range(0.0, 3.0)) = 1.15
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Assets/Shaders/AlbedoHalftoneLighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _LightColor;
                half4 _DarkColor;
                float _Scale;
                float _AspectRatio;
                float _VoronoiScale;
                float _RampStart;
                float _RampEnd;
                float _LightIntensityScale;
                float _MainLightWeight;
                float _UnityLightWeight;
                float _BeamIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInputs.positionCS;
                output.positionWS = vertexInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.screenPos = ComputeScreenPos(vertexInputs.positionCS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 0.00001);

                half3 lightColor;
                float lightStrength = EvaluateAlbedoWallLight(
                    input.positionWS,
                    normalWS,
                    input.positionCS,
                    _MainLightWeight,
                    _UnityLightWeight,
                    _BeamIntensity,
                    lightColor);

                // Parede 1: 100% preta no escuro; ao receber luz, abre bolinhas suaves mais fortes/sensíveis
                float lum = saturate(lightStrength * _LightIntensityScale * 1.25);

                float2 uv = float2(screenUV.x / max(_AspectRatio * _Scale, 0.0001), screenUV.y / max(_Scale, 0.0001));
                float2 p = uv * _VoronoiScale;
                float dCenter = length(frac(p) - 0.5);

                float maxDotRadius = 0.44;
                float dotRadius = lum * maxDotRadius;
                float softness = clamp((_RampEnd - _RampStart) * 0.9, 0.015, 0.12);
                float delta = max(fwidth(dCenter), softness);

                float isDot = smoothstep(0.008, 0.03, lum) * (1.0 - smoothstep(dotRadius - delta, dotRadius + delta, dCenter));
                half3 dotCol = _LightColor.rgb * lightColor;
                half3 col = lerp(_DarkColor.rgb, dotCol, (half)isDot);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
