Shader "Custom/GodRayVolumetric"
{
    Properties
    {
        [HDR] _TintColor ("Tint Color", Color) = (1.0, 0.9, 0.6, 1.0)
        _Intensity ("Intensity", Range(0.1, 10.0)) = 1.5
        _RimPower ("Rim Softness Power", Range(0.1, 8.0)) = 2.0
        _VerticalFalloff ("Vertical Falloff Exponent", Range(0.2, 5.0)) = 1.2
        _DepthFadeDistance ("Depth Fade Distance", Range(0.01, 3.0)) = 0.6
        _BaseAlpha ("Base Alpha", Range(0.0, 1.0)) = 0.32
        _WhiteAlphaScale ("White Light Transparency Scale", Range(0.1, 1.0)) = 0.65
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline" 
            "IgnoreProjector" = "True"
        }

        LOD 100
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "GodRayPass"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
                float3 normalWS     : TEXCOORD2;
                float4 screenPos    : TEXCOORD3;
                float4 clipPosCopy  : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _TintColor;
                float _Intensity;
                float _RimPower;
                float _VerticalFalloff;
                float _DepthFadeDistance;
                float _BaseAlpha;
                float _WhiteAlphaScale;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normInputs.normalWS;
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.clipPosCopy = output.positionCS;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. Gradiente Vertical (mais concentrado e brilhante no topo do feixe)
                float verticalGrad = pow(saturate(input.uv.y), _VerticalFalloff);

                // 2. Rim / Fresnel suave (suaviza bordas poligonais em ângulos rasantes)
                float3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);
                float rim = 1.0 - saturate(abs(dot(normalize(input.normalWS), viewDirWS)));
                rim = pow(rim, _RimPower);

                // 3. Soft Depth Fade com o chão e obstáculos (compatível com câmera ortográfica e perspectiva)
                float sceneRawDepth = LoadSceneDepth(uint2(input.positionCS.xy));
                #if UNITY_REVERSED_Z
                    bool isSky = sceneRawDepth <= 0.00001;
                #else
                    bool isSky = sceneRawDepth >= 0.99999;
                #endif
                float depthFade = 1.0;
                if (!isSky)
                {
                    #if !UNITY_REVERSED_Z
                        float deviceDepth = sceneRawDepth * 2.0 - 1.0;
                    #else
                        float deviceDepth = sceneRawDepth;
                    #endif
                    float2 clipXY = input.clipPosCopy.xy / max(abs(input.clipPosCopy.w), 0.00001);
                    float3 scenePosWS = ComputeWorldSpacePosition(float4(clipXY, deviceDepth, 1.0), UNITY_MATRIX_I_VP);
                    float depthDiff = max(0.0, dot(scenePosWS - input.positionWS, GetViewForwardDir()));
                    depthFade = saturate(depthDiff / max(_DepthFadeDistance, 0.001));
                }

                // 4. Combinação Final (suaviza mais quando a cor é branca/neutra)
                half maxC = max(_TintColor.r, max(_TintColor.g, _TintColor.b));
                half minC = min(_TintColor.r, min(_TintColor.g, _TintColor.b));
                half whiteness = saturate(1.0h - (maxC - minC) / max(maxC, 0.001h));
                float alphaScale = lerp(1.0, _WhiteAlphaScale, (float)whiteness);

                float combinedAlpha = verticalGrad * rim * depthFade * _BaseAlpha * alphaScale;
                half3 finalColor = _TintColor.rgb * _Intensity * combinedAlpha;

                return half4(finalColor, combinedAlpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
