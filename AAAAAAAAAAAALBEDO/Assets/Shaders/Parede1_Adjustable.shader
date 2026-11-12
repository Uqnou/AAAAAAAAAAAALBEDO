Shader "Custom/URP_Parede1_Adjustable"
{
    Properties
    {
        _LightColor ("Light Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _DarkColor ("Dark Color", Color) = (0.0, 0.0, 0.0, 1.0)
        _Scale ("Scale Factor", Float) = 0.07
        _AspectRatio ("Aspect Ratio (X/Y)", Float) = 0.55
        _VoronoiScale ("Voronoi Scale", Float) = 5.0
        _RampStart ("Ramp Start", Float) = 0.2681818
        _RampEnd ("Ramp End", Float) = 0.3000000
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
                half4 _LightColor;
                half4 _DarkColor;
                float _Scale;
                float _AspectRatio;
                float _VoronoiScale;
                float _RampStart;
                float _RampEnd;
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

                // Radiance reaching Shader to RGB node
                float radiance = totalLight * 0.8 * _LightIntensityScale;

                // Screen coordinates (Window Coordinates in Blender)
                float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 0.00001);

                // Blender Mapping (Texture Mode): divides UV by Vector Math scale
                // scale = Vector Math([0.55, 1.0, 1.0] * Scale) = [0.55 * Scale, Scale, Scale]
                // p.x = (screenUV.x / (0.55 * _Scale)) * _VoronoiScale;
                // p.y = (screenUV.y / _Scale) * _VoronoiScale;
                // p.z = 0.0;
                float2 uv = float2(screenUV.x / max(_AspectRatio * _Scale, 0.0001), screenUV.y / max(_Scale, 0.0001));
                float2 p = uv * _VoronoiScale;

                // Blender 3D Voronoi F1 Euclidean with Randomness = 0.0:
                // Lattice points are at (gx + 0.5, gy + 0.5, gz + 0.5)
                // With p.z = 0, gz = 0 -> (p.z - 0.5)^2 = 0.25
                float2 fractP = frac(p);
                float voronoiDist = sqrt(dot(fractP - 0.5, fractP - 0.5) + 0.25);

                // Blender Node Math Chain:
                // Math.001 = 1.0 - voronoiDist
                // Math.002 = radiance - Math.001 = radiance + voronoiDist - 1.0
                // Math.003 = Math.002 + 0.5 = radiance + voronoiDist - 0.5
                float factor = radiance + voronoiDist - 0.5;

                // Blender Color Ramp: stop 0.2681818 -> Black, stop 0.3000000 -> White
                float delta = fwidth(factor);
                float ramp = smoothstep(_RampStart, _RampEnd, factor);

                half3 col = lerp(_DarkColor.rgb, _LightColor.rgb, ramp);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
