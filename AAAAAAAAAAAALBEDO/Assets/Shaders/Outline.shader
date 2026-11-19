Shader "Custom/URP_Outline"
{
    Properties
    {
        [HDR] _Color ("Outline Color", Color) = (1.5, 1.5, 1.5, 1.0)
        _Brightness ("White Intensity", Range(0.0, 10.0)) = 2.8
        _Extrude ("Extra Normal Extrude", Range(-0.03, 0.03)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry+10"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual
            Offset -2, -2

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
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Brightness;
                float _Extrude;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 posOS = input.positionOS.xyz + input.normalOS * _Extrude;
                output.positionCS = TransformObjectToHClip(posOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return half4(_Color.rgb * _Brightness, _Color.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            Cull Back
            ZWrite On
            ColorMask R
            Offset -2, -2

            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #pragma multi_compile_instancing
            #include "Assets/Shaders/AlbedoDepthPasses.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Brightness;
                float _Extrude;
            CBUFFER_END

            AlbedoDepthVaryings vertDepth(AlbedoDepthAttributes input) { return AlbedoDepthVert(input, _Extrude); }
            half fragDepth(AlbedoDepthVaryings input) : SV_Target { return DepthOnlyFrag(input); }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            Cull Back
            ZWrite On
            Offset -2, -2

            HLSLPROGRAM
            #pragma vertex vertDN
            #pragma fragment fragDN
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS
            #include "Assets/Shaders/AlbedoDepthPasses.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Brightness;
                float _Extrude;
            CBUFFER_END

            AlbedoDepthVaryings vertDN(AlbedoDepthAttributes input) { return AlbedoDepthVert(input, _Extrude); }
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
    FallBack Off
}
