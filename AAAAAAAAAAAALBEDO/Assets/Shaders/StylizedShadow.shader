Shader "Custom/URP_StylizedShadow"
{
    Properties
    {
        _ShadowColor ("Shadow Color", Color) = (0.002, 0.002, 0.002, 0.92)
        _VoronoiScale ("Voronoi Scale", Float) = 1.25
        _NoiseOffset ("Noise Offset", Vector) = (0.0, 0.0, 0.2, 0.0)
        _Cutoff ("Threshold", Range(0.0, 1.0)) = 0.72
        _MinAlpha ("Min Opacity", Range(0.0, 1.0)) = 0.55
        _FlowSpeed ("Liquid Flow Speed", Float) = 0.65
        _WaveSpeed ("Mesh Wave Speed", Float) = 1.8
        _WaveAmplitude ("Mesh Wave Amplitude", Float) = 0.045
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
                float _MinAlpha;
                float _FlowSpeed;
                float _WaveSpeed;
                float _WaveAmplitude;
            CBUFFER_END

            float3 Hash33(float3 p)
            {
                p = float3(dot(p, float3(127.1, 311.7, 74.7)),
                           dot(p, float3(269.5, 183.3, 246.1)),
                           dot(p, float3(113.5, 271.9, 124.6)));
                return frac(sin(p) * 43758.5453123);
            }

            float Voronoi3D(float3 p, float timePhase)
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
                            float3 rand = Hash33(g + lattice);
                            float3 offset = 0.5 + 0.42 * sin(timePhase + 6.2831 * rand);
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
                float3 pos = input.positionOS.xyz;
                float t = _Time.y * _WaveSpeed;
                float ripple = sin(pos.x * 3.5 + t) * cos(pos.z * 3.5 - t * 0.8)
                             + sin(pos.y * 4.0 + t * 1.2) * 0.5;
                pos += input.normalOS * (ripple * _WaveAmplitude);

                VertexPositionInputs vertexInputs = GetVertexPositionInputs(pos);
                output.positionCS = vertexInputs.positionCS;
                output.positionOS = pos;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float t = _Time.y * _FlowSpeed;
                float3 warp = float3(
                    sin(input.positionOS.y * 2.2 + t * 1.1),
                    cos(input.positionOS.x * 2.2 - t * 0.9),
                    sin(input.positionOS.z * 2.2 + t * 0.8)
                ) * 0.25;

                float3 objCoord = input.positionOS + _NoiseOffset.xyz + warp + float3(0.0, t * 0.35, t * 0.15);
                float dist = Voronoi3D(objCoord * _VoronoiScale, t * 1.4);
                float voronoiMask = smoothstep(_Cutoff, 0.0, dist);
                float alpha = lerp(_MinAlpha, 1.0, voronoiMask) * _ShadowColor.a;

                return half4(_ShadowColor.rgb, saturate(alpha));
            }
            ENDHLSL
        }
    }
    FallBack Off
}