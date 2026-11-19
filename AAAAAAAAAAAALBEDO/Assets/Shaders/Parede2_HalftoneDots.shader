Shader "Custom/URP_Parede2_HalftoneDots"
{
    Properties
    {
        _DotColor ("Dot Tint Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _BackgroundColor ("Background Color", Color) = (0.0, 0.0, 0.0, 1.0)
        _Frequency ("Dot Frequency / Grid Size", Float) = 100.0
        _AspectRatio ("Aspect Ratio (X/Y)", Float) = 1.7777778
        _FromMin ("Light From Min", Float) = -1.5
        _FromMax ("Light From Max", Float) = 2.1
        _Steps ("Quantization Steps", Float) = 4.0
        _LightIntensityScale ("Light Sensitivity Scale", Float) = 1.0
        _MainLightWeight ("Directional Light Weight", Range(0.0, 1.0)) = 0.0
        _UnityLightWeight ("Scene Light Weight", Range(0.0, 2.0)) = 0.35
        _BeamIntensity ("Prism/GodRay Beam Weight", Range(0.0, 3.0)) = 0.95
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
                half4 _DotColor;
                half4 _BackgroundColor;
                float _Frequency;
                float _AspectRatio;
                float _FromMin;
                float _FromMax;
                float _Steps;
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

                // Parede 2: 100% preta no escuro; halftone quantizado em degraus (_Steps), menos sensível que Parede 1
                // e com raio limitado (< 0.45) para nunca chapar completamente branco.
                float rawLum = saturate((lightStrength * _LightIntensityScale - 0.03) * 0.85);
                float qLum = (_Steps > 0.0 && rawLum > 0.001) ? (ceil(rawLum * _Steps) / _Steps) : rawLum;

                float2 gridUV = screenUV * float2(_Frequency * _AspectRatio, _Frequency);
                float dCenter = length(frac(gridUV) - 0.5);

                float dotRadius = qLum * 0.44;
                float delta = fwidth(dCenter);
                float isDot = (qLum > 0.005) ? (1.0 - smoothstep(dotRadius - delta, dotRadius + delta, dCenter)) : 0.0;

                half3 dotCol = _DotColor.rgb * lightColor;
                half3 col = lerp(_BackgroundColor.rgb, dotCol, (half)isDot);
                return half4(col, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #pragma multi_compile_instancing
            #include "Assets/Shaders/AlbedoDepthPasses.hlsl"

            AlbedoDepthVaryings vertDepth(AlbedoDepthAttributes input) { return AlbedoDepthVert(input, 0.0); }
            half fragDepth(AlbedoDepthVaryings input) : SV_Target { return DepthOnlyFrag(input); }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vertDN
            #pragma fragment fragDN
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS
            #include "Assets/Shaders/AlbedoDepthPasses.hlsl"

            AlbedoDepthVaryings vertDN(AlbedoDepthAttributes input) { return AlbedoDepthVert(input, 0.0); }
            void fragDN(AlbedoDepthVaryings input, out half4 outNormalWS : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                DepthNormalsFrag(input, outNormalWS
                #ifdef _WRITE_RENDERING_LAYERS
                    , outRenderingLayers
                #endif
                );
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
