Shader "Custom/URP_CelShadingHalftone"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.8, 0.8, 0.8, 1.0)
        _ShadowColor ("Shadow Color", Color) = (0.0, 0.0, 0.0, 1.0)
        _PatternScale ("Pattern Scale", Float) = 25.0
        _PatternThreshold ("Pattern Threshold", Range(0.0, 1.0)) = 0.28
        _PatternSoftness ("Pattern Softness", Range(0.001, 0.2)) = 0.03
        _LightCutoff ("Light Cutoff", Range(0.0, 1.0)) = 0.5
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
                half4 _BaseColor;
                half4 _ShadowColor;
                float _PatternScale;
                float _PatternThreshold;
                float _PatternSoftness;
                float _LightCutoff;
            CBUFFER_END

            // Procedural 2D Voronoi Distance Function
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
                        float2 offset = Hash22(g + lattice);
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
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                
                // Diffuse lighting intensity
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float lightIntensity = NdotL * mainLight.shadowAttenuation;

                // Screen coordinates
                float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 0.0001);
                float aspect = _ScreenParams.x / _ScreenParams.y;
                screenUV.x *= aspect;

                // Screen-space Voronoi texture replication
                float voronoi = Voronoi2D(screenUV * _PatternScale);

                // Blend lighting and voronoi pattern (matching Blender Shader to RGB -> Math -> Color Ramp)
                float patternFactor = voronoi * (1.0 - lightIntensity * 0.7);
                float ramp = smoothstep(_PatternThreshold - _PatternSoftness, _PatternThreshold + _PatternSoftness, patternFactor);

                half3 col = lerp(_BaseColor.rgb * mainLight.color, _ShadowColor.rgb, ramp);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
