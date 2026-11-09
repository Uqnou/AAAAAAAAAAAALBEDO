Shader "Custom/URP_Parede1_Adjustable"
{
    Properties
    {
        _LightColor ("Light Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _DarkColor ("Dark Color", Color) = (0.0, 0.0, 0.0, 1.0)
        _Scale ("Scale Factor", Float) = 0.07
        _AspectRatio ("Aspect Ratio (X/Y)", Float) = 0.55
        _VoronoiScale ("Voronoi Scale", Float) = 5.0
        _RampStart ("Ramp Start", Float) = 0.268
        _RampEnd ("Ramp End", Float) = 0.300
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
                half4 _LightColor;
                half4 _DarkColor;
                float _Scale;
                float _AspectRatio;
                float _VoronoiScale;
                float _RampStart;
                float _RampEnd;
            CBUFFER_END

            // Procedural 2D Voronoi Distance Function (matching Blender F1 Euclidean)
            float2 Hash22(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            float Voronoi2D(float2 uv)
            {
                float2 g = floor(uv);
                float2 f = frac(uv);
                float minDist = 1.0;

                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 lattice = float2(x, y);
                        // In Blender, randomness = 0.0 for Parede1!
                        float2 offset = float2(0.5, 0.5); 
                        float2 d = lattice + offset - f;
                        minDist = min(minDist, length(d));
                    }
                }
                return minDist;
            }

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

                // Diffuse lighting intensity from scene lights
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

                // Screen coordinates
                float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 0.00001);
                
                // Blender Mapping: Vector Math Multiply (0.55 * Scale, 1.0 * Scale)
                float2 uv = screenUV * float2(_AspectRatio * _Scale, _Scale);
                float voronoiDist = Voronoi2D(uv * _VoronoiScale);

                // Blender Math node exact formula:
                // Math.001 = 1.0 - voronoiDist
                // Math.002 = totalLight - (1.0 - voronoiDist) = totalLight + voronoiDist - 1.0
                // Math.003 = Math.002 + 0.5 = totalLight + voronoiDist - 0.5
                float factor = (totalLight - (1.0 - voronoiDist)) + 0.5;

                // Color Ramp: stop 0.268 -> Black, stop 0.300 -> White
                float ramp = smoothstep(_RampStart, _RampEnd, factor);

                half3 col = lerp(_DarkColor.rgb, _LightColor.rgb, ramp);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
