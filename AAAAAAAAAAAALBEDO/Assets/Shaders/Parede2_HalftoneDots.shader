Shader "Custom/URP_Parede2_HalftoneDots"
{
    Properties
    {
        _DotColor ("Dot Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _BackgroundColor ("Background Color", Color) = (0.0, 0.0, 0.0, 1.0)
        _Frequency ("Dot Frequency / Grid Size", Float) = 100.0
        _AspectRatio ("Aspect Ratio (X/Y)", Float) = 1.7777778
        _FromMin ("Light From Min", Float) = -1.5
        _FromMax ("Light From Max", Float) = 2.1
        _Steps ("Quantization Steps", Float) = 4.0
        _LightIntensityScale ("Light Sensitivity Scale", Float) = 1.0
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
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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

                // Total diffuse lighting: replicate Principled BSDF with base color 0.8
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float totalLight = saturate(dot(normalWS, mainLight.direction)) * mainLight.shadowAttenuation;

                #if defined(_ADDITIONAL_LIGHTS)
                uint numAdditionalLights = GetAdditionalLightsCount();
                for (uint i = 0u; i < numAdditionalLights; ++i)
                {
                    Light addLight = GetAdditionalLight(i, input.positionWS);
                    float NdotL = saturate(dot(normalWS, addLight.direction));
                    totalLight += NdotL * addLight.distanceAttenuation * addLight.shadowAttenuation;
                }
                #endif

                // Radiance reaching the Shader to RGB node
                float radiance = totalLight * 0.8 * _LightIntensityScale;

                // Blender Node Map Range (Linear):
                // In Blender, when unlit (radiance = 0), mapRange = (0 - (-1.5)) / (2.1 - (-1.5)) = 0.416667
                float mapRangeResult = saturate((radiance - _FromMin) / (_FromMax - _FromMin));
                if (_Steps > 0.0)
                {
                    mapRangeResult = floor(mapRangeResult * _Steps) / _Steps;
                }

                // Window coordinates (screen-space UV)
                float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 0.00001);

                // Multiply with Combine XYZ(Value * 1.7777778, Value, 0.0)
                float2 gridUV = screenUV * float2(_Frequency * _AspectRatio, _Frequency);
                float2 fractUV = frac(gridUV);

                // Blender Vector Math.002: Distance(fractUV, (0.5, 0.5, 0.5))
                // fractUV has Z=0, so distance in 3D to (0.5, 0.5, 0.5) is:
                // sqrt((fx - 0.5)^2 + (fy - 0.5)^2 + 0.25)
                // Minimum distance at center of cell is sqrt(0.25) = 0.5!
                float dist = sqrt(dot(fractUV - 0.5, fractUV - 0.5) + 0.25);

                // Blender Math.001: Less Than(dist, mapRangeResult)
                // When unlit, mapRangeResult <= 0.41667 < 0.5 <= dist everywhere, so it is 100% black!
                // As light hits the surface, mapRangeResult exceeds 0.5, opening white circular dots!
                float delta = fwidth(dist);
                float isDot = 1.0 - smoothstep(mapRangeResult - delta, mapRangeResult + delta, dist);

                half3 col = lerp(_BackgroundColor.rgb, _DotColor.rgb, isDot);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
