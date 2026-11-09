Shader "Custom/URP_StylizedFire"
{
    Properties
    {
        _EmissionColor ("Emission Color", Color) = (1.0, 0.05, 0.0, 1.0)
        _EmissionStrength ("Emission Strength", Float) = 2.0
        _VoronoiScale ("Voronoi Scale", Float) = 0.85
        _NoiseOffset ("Noise Offset", Vector) = (0.0, 0.0, -0.4, 0.0)
        _GradientScale ("Gradient Scale", Vector) = (0.9, 2.0, 1.6, 1.0)
        _Cutoff ("Cutoff / Alpha Threshold", Range(0.0, 1.0)) = 0.39
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent+100" 
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 200
        Blend One One  // Additive blending for rich vibrant laser/fire beam
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _EmissionColor;
                float _EmissionStrength;
                float _VoronoiScale;
                float4 _NoiseOffset;
                float4 _GradientScale;
                float _Cutoff;
            CBUFFER_END

            float3 Hash33(float3 p)
            {
                p = float3(dot(p, float3(127.1, 311.7, 74.7)),
                           dot(p, float3(269.5, 183.3, 246.1)),
                           dot(p, float3(113.5, 271.9, 124.6)));
                return frac(sin(p) * 43758.5453123);
            }

            float Voronoi3D(float3 p)
            {
                float3 g = floor(p);
                float3 f = frac(p);
                float minDist = 1.0;

                for (int z = -1; z <= 1; z++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        for (int x = -1; x <= 1; x++)
                        {
                            float3 lattice = float3(x, y, z);
                            float3 offset = Hash33(g + lattice);
                            float3 d = lattice + offset - f;
                            minDist = min(minDist, length(d));
                        }
                    }
                }
                return minDist;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInputs.positionCS;
                output.positionOS = input.positionOS.xyz;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Replicating Blender Mapping node + Voronoi Object Coordinates
                float3 objCoord = (input.positionOS + _NoiseOffset.xyz) * _GradientScale.xyz;
                float dist = Voronoi3D(objCoord * _VoronoiScale);

                // Blender Color Ramp on Voronoi: 0.005 -> White, 0.391 -> Black
                float voronoiFactor = 1.0 - smoothstep(0.005, _Cutoff, dist);

                // Vertical / Longitudinal gradient
                float grad = saturate(input.positionOS.y + 0.5);

                // Beam core intensity
                half3 col = _EmissionColor.rgb * _EmissionStrength * voronoiFactor;

                return half4(col, voronoiFactor);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
