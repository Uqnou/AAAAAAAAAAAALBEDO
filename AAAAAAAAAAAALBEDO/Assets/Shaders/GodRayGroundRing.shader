Shader "Custom/GodRayGroundRing"
{
    Properties
    {
        [HDR] _TintColor ("Tint Color", Color) = (1.0, 0.9, 0.6, 1.0)
        _Intensity ("Intensity", Range(0.1, 10.0)) = 2.0
        _InnerRadius ("Inner Radius", Range(0.0, 1.0)) = 0.65
        _Feather ("Edge Feather", Range(0.01, 0.5)) = 0.15
        _PulseSpeed ("Pulse Speed", Range(0.0, 10.0)) = 2.0
        _PulseAmount ("Pulse Amount", Range(0.0, 1.0)) = 0.25
        _BaseAlpha ("Base Alpha", Range(0.0, 1.0)) = 0.55
        _WhiteAlphaScale ("White Ring Transparency Scale", Range(0.05, 1.0)) = 0.42
        _SurfaceNormalFade ("Surface Angle Falloff", Range(0.0, 1.0)) = 0.45
        [HideInInspector] _BeamOrigin ("Beam Origin", Vector) = (0.0, 0.0, 0.0, 0.0)
        [HideInInspector] _BeamDir ("Beam Direction", Vector) = (0.0, -1.0, 0.0, 0.0)
        [HideInInspector] _BeamDims ("Beam Dims", Vector) = (0.35, 2.2, 0.0, 0.0)
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent+10" 
            "RenderPipeline" = "UniversalPipeline" 
            "IgnoreProjector" = "True"
        }

        LOD 100
        Blend SrcAlpha One
        ZWrite Off
        Cull Front
        ZTest Always

        Pass
        {
            Name "GroundRingPass"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Assets/Shaders/AlbedoTorchCarve.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float4 clipPosCopy  : TEXCOORD1;
                float3 positionWS   : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _TintColor;
                float _Intensity;
                float _InnerRadius;
                float _Feather;
                float _PulseSpeed;
                float _PulseAmount;
                float _BaseAlpha;
                float _WhiteAlphaScale;
                float _SurfaceNormalFade;
                float4 _BeamOrigin;
                float4 _BeamDir;
                float4 _BeamDims;
            CBUFFER_END

            bool IsSkyDepth(float rawDepth)
            {
                #if UNITY_REVERSED_Z
                    return rawDepth <= 0.00001;
                #else
                    return rawDepth >= 0.99999;
                #endif
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                output.clipPosCopy = output.positionCS;
                return output;
            }

            float3 ReconstructWorldPosAtPixel(float2 pixelXY, float2 clipXY, float rawDepth)
            {
                #if !UNITY_REVERSED_Z
                    rawDepth = rawDepth * 2.0 - 1.0;
                #endif
                return ComputeWorldSpacePosition(float4(clipXY, rawDepth, 1.0), UNITY_MATRIX_I_VP);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float dist = 0.0;
                float angleAtten = 1.0;
                float heightFade = 1.0;
                float3 surfacePosWS = input.positionWS;

                // Modo projetor 3D via depth buffer (funciona em ortografica e perspectiva)
                if (_BeamDims.z > 0.001)
                {
                    float2 pixelXY = input.positionCS.xy;
                    float sceneRawDepth = LoadSceneDepth(uint2(pixelXY));
                    if (IsSkyDepth(sceneRawDepth)) clip(-1);

                    float2 clipXY = input.clipPosCopy.xy / max(abs(input.clipPosCopy.w), 0.00001);
                    float3 posWS = ReconstructWorldPosAtPixel(pixelXY, clipXY, sceneRawDepth);
                    surfacePosWS = posWS;
                    float3 posOS = TransformWorldToObject(posWS);
                    clip(0.5 - abs(posOS));

                    float3 beamDir = normalize(_BeamDir.xyz);
                    float3 toPos = posWS - _BeamOrigin.xyz;
                    float h = dot(toPos, beamDir);
                    float coneH = max(_BeamDims.z, 0.01);
                    if (h < _BeamDims.w || h > coneH + 0.55) clip(-1);

                    float coneT = saturate(h / coneH);
                    float coneRadius = lerp(_BeamDims.x, _BeamDims.y, coneT);
                    float radialDist = length(toPos - beamDir * h);
                    dist = radialDist / max(coneRadius, 0.001);
                    if (dist > 1.0) clip(-1);

                    // Reconstrucao de normal com menor variacao de profundidade (evita artefato em bordas)
                    float2 clipStep = float2(ddx(clipXY.x), ddy(clipXY.y));
                    float dL = LoadSceneDepth(uint2(max(float2(0.0, 0.0), pixelXY - float2(1.0, 0.0))));
                    float dR = LoadSceneDepth(uint2(min(_ScreenSize.xy - 1.0, pixelXY + float2(1.0, 0.0))));
                    float dD = LoadSceneDepth(uint2(max(float2(0.0, 0.0), pixelXY - float2(0.0, 1.0))));
                    float dU = LoadSceneDepth(uint2(min(_ScreenSize.xy - 1.0, pixelXY + float2(0.0, 1.0))));

                    float3 posX = abs(dR - sceneRawDepth) < abs(sceneRawDepth - dL)
                        ? ReconstructWorldPosAtPixel(pixelXY + float2(1.0, 0.0), clipXY + float2(clipStep.x, 0.0), dR) - posWS
                        : posWS - ReconstructWorldPosAtPixel(pixelXY - float2(1.0, 0.0), clipXY - float2(clipStep.x, 0.0), dL);
                    float3 posY = abs(dU - sceneRawDepth) < abs(sceneRawDepth - dD)
                        ? ReconstructWorldPosAtPixel(pixelXY + float2(0.0, 1.0), clipXY + float2(0.0, clipStep.y), dU) - posWS
                        : posWS - ReconstructWorldPosAtPixel(pixelXY - float2(0.0, 1.0), clipXY - float2(0.0, clipStep.y), dD);

                    float3 rawCross = cross(posY, posX);
                    float crossLen = length(rawCross);
                    float3 normalWS = crossLen > 1e-6 ? (rawCross / crossLen) : -beamDir;
                    if (dot(normalWS, (float3)GetWorldSpaceNormalizeViewDir(posWS)) < 0.0)
                        normalWS = -normalWS;

                    float radiusDiff = max(_BeamDims.y - _BeamDims.x, 0.01);
                    float3 apexWS = _BeamOrigin.xyz - beamDir * (_BeamDims.x * coneH / radiusDiff);
                    float3 lightIncoming = normalize(apexWS - posWS);
                    float NdotL = dot(normalWS, lightIncoming);
                    float surfaceMask = smoothstep(-0.08, 0.22, NdotL);
                    angleAtten = lerp(1.0, saturate(NdotL * 1.35 + 0.15), _SurfaceNormalFade) * surfaceMask;
                    heightFade = smoothstep(_BeamDims.w, _BeamDims.w + 0.35, h) * (1.0 - smoothstep(coneH + 0.02, coneH + 0.55, h));
                }
                else
                {
                    dist = length((input.uv - 0.5) * 2.0);
                }

                float outerFade = 1.0 - smoothstep(1.0 - _Feather, 1.0, dist);
                float innerFade = smoothstep(_InnerRadius - _Feather, _InnerRadius, dist);
                float ringAlpha = outerFade * innerFade;
                float innerDisc = (1.0 - smoothstep(0.0, 0.95, dist)) * 0.15;

                half maxC = max(_TintColor.r, max(_TintColor.g, _TintColor.b));
                half minC = min(_TintColor.r, min(_TintColor.g, _TintColor.b));
                half whiteness = saturate(1.0h - (maxC - minC) / max(maxC, 0.001h));
                float alphaScale = _BaseAlpha * lerp(1.0, _WhiteAlphaScale, (float)whiteness);

                float3 bDir = normalize(_BeamDir.xyz);
                bool isHoriz = abs(bDir.y) < 0.35;
                float torchMult = _BeamDims.z > 0.001
                    ? EvaluateTorchBeamMultiplier(surfacePosWS, _BeamOrigin.xyz, bDir, _BeamDims.x, _BeamDims.y, max(_BeamDims.z, 0.01), isHoriz)
                    : EvaluateTorchLightMultiplier(surfacePosWS, false);
                float totalAlpha = saturate(ringAlpha + innerDisc) * alphaScale * angleAtten * heightFade * torchMult;
                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;

                half3 finalColor = _TintColor.rgb * _Intensity * pulse * totalAlpha;
                return half4(finalColor, totalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
