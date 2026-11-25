Shader "Custom/ExitGodRay"
{
    Properties
    {
        [HDR] _TintColor ("Tint Color", Color) = (1.0, 0.88, 0.55, 1.0)
        _Intensity ("Intensity", Range(0.1, 10.0)) = 2.0
        _BaseAlpha ("Base Alpha", Range(0.0, 1.0)) = 0.35

        [Header(Volumetric Falloff)]
        _VerticalFalloff ("Vertical Falloff", Range(0.2, 5.0)) = 1.35
        _TopFade ("Top Edge Fade", Range(0.0, 0.5)) = 0.08
        _RimPower ("Rim Softness Power", Range(0.1, 8.0)) = 1.8
        _DepthFadeDistance ("Depth Fade Distance", Range(0.01, 3.0)) = 0.65

        [Header(Animated Light Shafts)]
        _RayCount ("Ray Shafts Count", Range(2.0, 32.0)) = 9.0
        _RaySharpness ("Ray Contrast", Range(0.5, 6.0)) = 2.2
        _RayScrollSpeed ("Ray Sway Speed", Range(-2.0, 2.0)) = 0.25
        _FlowSpeed ("Light Flow Speed", Range(-3.0, 3.0)) = 0.5
        _ShimmerSpeed ("Shimmer Speed", Range(0.0, 5.0)) = 1.4
        _ShimmerAmount ("Shimmer Amount", Range(0.0, 0.8)) = 0.25
        _MinRayFloor ("Min Ambient Glow", Range(0.0, 1.0)) = 0.22
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+5"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        LOD 100
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ExitGodRayPass"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float4 clipPosCopy : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _TintColor;
                float _Intensity;
                float _BaseAlpha;
                float _VerticalFalloff;
                float _TopFade;
                float _RimPower;
                float _DepthFadeDistance;
                float _RayCount;
                float _RaySharpness;
                float _RayScrollSpeed;
                float _FlowSpeed;
                float _ShimmerSpeed;
                float _ShimmerAmount;
                float _MinRayFloor;
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
                output.clipPosCopy = output.positionCS;
                return output;
            }

            float EvaluateMovingShafts(float2 uv, float t)
            {
                float angle = uv.x * 6.2831853;
                float countPrimary = max(2.0, round(_RayCount));
                float countSecondary = max(2.0, round(_RayCount * 0.5));
                float countFine = max(4.0, round(_RayCount * 2.0));

                float sway = sin(t * 0.65 + uv.y * 2.2) * 0.45;
                float wave1 = sin(angle * countPrimary + t * _RayScrollSpeed * 2.2 + sway);
                float wave2 = sin(angle * countSecondary - t * _RayScrollSpeed * 1.4 + 1.57);
                float wave3 = sin(angle * countFine + t * _RayScrollSpeed * 3.1 - uv.y * 1.8);

                float combined = saturate(wave1 * 0.52 + wave2 * 0.30 + wave3 * 0.18 + 0.5);
                float shafts = pow(combined, _RaySharpness);

                float flowWave = 0.5 + 0.5 * sin(uv.y * 7.5 + t * _FlowSpeed * 3.2 + angle * 2.0);
                float shimmer = lerp(1.0 - _ShimmerAmount, 1.0 + _ShimmerAmount, flowWave);
                float breathe = 1.0 + sin(t * _ShimmerSpeed) * (_ShimmerAmount * 0.45);

                return lerp(_MinRayFloor, 1.0, shafts) * shimmer * breathe;
            }

            float EvaluateDepthFade(float4 positionCS, float4 clipPosCopy, float3 positionWS)
            {
                float sceneRawDepth = LoadSceneDepth(uint2(positionCS.xy));
                #if UNITY_REVERSED_Z
                    bool isSky = sceneRawDepth <= 0.00001;
                #else
                    bool isSky = sceneRawDepth >= 0.99999;
                #endif

                if (isSky) return 1.0;

                #if !UNITY_REVERSED_Z
                    float deviceDepth = sceneRawDepth * 2.0 - 1.0;
                #else
                    float deviceDepth = sceneRawDepth;
                #endif

                float2 clipXY = clipPosCopy.xy / max(abs(clipPosCopy.w), 0.00001);
                float3 scenePosWS = ComputeWorldSpacePosition(float4(clipXY, deviceDepth, 1.0), UNITY_MATRIX_I_VP);
                float depthDiff = max(0.0, dot(scenePosWS - positionWS, GetViewForwardDir()));
                return saturate(depthDiff / max(_DepthFadeDistance, 0.001));
            }

            half4 frag(Varyings input) : SV_Target
            {
                float t = _Time.y;

                // 1. Gradiente vertical (brilha mais no topo e dissolve suavemente embaixo)
                float bottomFade = pow(saturate(input.uv.y), _VerticalFalloff);
                float topFade = _TopFade > 0.001 ? smoothstep(1.0, 1.0 - _TopFade, input.uv.y) : 1.0;
                float verticalGrad = bottomFade * topFade;

                // 2. Rim / Fresnel suave para bordas sem quinas visíveis
                float3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);
                float fresnel = saturate(abs(dot(normalize(input.normalWS), viewDirWS)));
                float rim = pow(fresnel, _RimPower);

                // 3. Feixes de luz animados ("luzes mexendo")
                float movingShafts = EvaluateMovingShafts(input.uv, t);

                // 4. Depth Fade compatível com câmera ortográfica e perspectiva
                float depthFade = EvaluateDepthFade(input.positionCS, input.clipPosCopy, input.positionWS);

                float finalAlpha = saturate(verticalGrad * rim * movingShafts * depthFade * _BaseAlpha);
                half3 finalColor = _TintColor.rgb * _Intensity * finalAlpha;

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
