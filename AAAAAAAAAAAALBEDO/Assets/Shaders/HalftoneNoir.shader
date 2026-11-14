Shader "ALBEDO/HalftoneNoir"
{
    Properties
    {
        _BaseColor ("Lit Base Color", Color) = (1, 1, 1, 1)
        _ShadowColor ("Shadow Color", Color) = (0, 0, 0, 1)
        _MainLightWeight ("Main Light Weight", Range(0, 2)) = 0.18
        _LightIntensityScale ("Additional Light Gain", Float) = 1.0
        _DotScale ("Halftone Dot Frequency", Float) = 7.0
        _DotSharpness ("Halftone Smoothness", Range(0.002, 0.25)) = 0.04
        _ThresholdMin ("Shadow Cutoff", Range(0, 1)) = 0.03
        _ThresholdMax ("Full Lit Threshold", Range(0.05, 3)) = 0.65
        _AdditiveTintStrength ("Colored Light Tint", Range(0, 1)) = 0.88
        _RimColor ("Rim Outline Color", Color) = (1, 1, 1, 1)
        _RimPower ("Rim Outline Strength", Range(0, 1)) = 0.0
        _RimThreshold ("Rim Angle Cutoff", Range(0.05, 0.95)) = 0.62
        [HDR] _EmissionColor ("Unlit Emission", Color) = (0, 0, 0, 1)
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

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float fogCoord : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _ShadowColor;
                half _MainLightWeight;
                half _LightIntensityScale;
                float _DotScale;
                half _DotSharpness;
                half _ThresholdMin;
                half _ThresholdMax;
                half _AdditiveTintStrength;
                half4 _RimColor;
                half _RimPower;
                half _RimThreshold;
                half4 _EmissionColor;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = NormalizeNormalPerVertex(normInputs.normalWS);
                output.fogCoord = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            half SampleHalftoneGrid(float2 uvCoord, half coverage)
            {
                float2 cell = frac(uvCoord * _DotScale) - 0.5;
                half dist = (half)length(cell);
                // Rotated staggered subgrid radius so 1.0 fully covers the unit square (half-diagonal = 0.7071)
                half radius = sqrt(saturate(coverage)) * 0.70710678h;
                return 1.0h - smoothstep(radius - _DotSharpness, radius + _DotSharpness, dist);
            }

            half ComputeTriplanarHalftone(float3 positionWS, half3 normalWS, half coverage)
            {
                half3 blend = pow(abs(normalWS), 4.0h);
                blend /= max(dot(blend, half3(1.0h, 1.0h, 1.0h)), 0.0001h);

                half dotX = SampleHalftoneGrid(positionWS.zy, coverage);
                half dotY = SampleHalftoneGrid(positionWS.xz, coverage);
                half dotZ = SampleHalftoneGrid(positionWS.xy, coverage);

                return dotX * blend.x + dotY * blend.y + dotZ * blend.z;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 normalWS = normalize(input.normalWS);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);

                half3 lightAccum = half3(0.0h, 0.0h, 0.0h);

                // Main Directional Light
                Light mainLight = GetMainLight(inputData.shadowCoord);
                half mainNdotL = saturate(dot(normalWS, mainLight.direction));
                lightAccum += mainLight.color * (mainNdotL * mainLight.distanceAttenuation * mainLight.shadowAttenuation * _MainLightWeight);

                // Additional Lights (Spots / Points, supporting Forward and Forward+ Cluster Loop)
                AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData.normalizedScreenSpaceUV, 1.0h);
                uint pixelLightCount = GetAdditionalLightsCount();

                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light addLight = GetAdditionalLight(lightIndex, inputData, half4(1, 1, 1, 1), aoFactor);
                    half addNdotL = saturate(dot(normalWS, addLight.direction));
                    lightAccum += addLight.color * (addNdotL * addLight.distanceAttenuation * addLight.shadowAttenuation * _LightIntensityScale);
                LIGHT_LOOP_END

                half luminance = dot(lightAccum, half3(0.2126h, 0.7152h, 0.0722h));
                half coverage = smoothstep(_ThresholdMin, _ThresholdMax, luminance);

                half halftoneMask = ComputeTriplanarHalftone(input.positionWS, normalWS, coverage);
                // Guarantee clean extremes at pure dark and full bright
                halftoneMask = saturate(lerp(halftoneMask, step(0.5h, coverage), step(0.98h, coverage) + step(coverage, 0.01h)));

                half maxChannel = max(lightAccum.r, max(lightAccum.g, lightAccum.b));
                half3 normalizedLightTint = maxChannel > 0.001h ? saturate(lightAccum / maxChannel) : half3(1.0h, 1.0h, 1.0h);
                half3 litColor = _BaseColor.rgb * lerp(half3(1.0h, 1.0h, 1.0h), normalizedLightTint, _AdditiveTintStrength);

                half3 finalRGB = lerp(_ShadowColor.rgb, litColor, halftoneMask);

                // Optional Noir white rim outline
                half fresnel = 1.0h - saturate(dot(normalWS, inputData.viewDirectionWS));
                half rimMask = smoothstep(_RimThreshold - 0.05h, _RimThreshold + 0.05h, fresnel) * _RimPower;
                finalRGB = lerp(finalRGB, _RimColor.rgb, saturate(rimMask));

                finalRGB += _EmissionColor.rgb;
                finalRGB = MixFog(finalRGB, input.fogCoord);

                return half4(finalRGB, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                return 0;
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
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
