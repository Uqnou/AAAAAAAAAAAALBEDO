Shader "Custom/URP_StylizedShadow"
{
    Properties
    {
        _ShadowColor ("Shadow Color", Color) = (0.002, 0.002, 0.002, 0.8)
        _VoronoiScale ("Voronoi Scale", Float) = 0.85
        _NoiseOffset ("Noise Offset", Vector) = (0.0, 0.0, 0.2, 0.0)
        _Cutoff ("Threshold", Range(0.0, 1.0)) = 0.518
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 200
        Blend SrcAlpha OneMinusSrcAlpha
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
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _ShadowColor;
                float _VoronoiScale;
                float4 _NoiseOffset;
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
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 objCoord = input.positionOS + _NoiseOffset.xyz;
                float dist = Voronoi3D(objCoord * _VoronoiScale);
                float alpha = smoothstep(_Cutoff, 0.0, dist) * _ShadowColor.a;

                return half4(_ShadowColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
