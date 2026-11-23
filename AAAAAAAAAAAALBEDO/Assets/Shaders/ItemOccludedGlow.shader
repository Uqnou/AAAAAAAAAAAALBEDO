Shader "Custom/URP_ItemOccludedGlow"
{
    Properties
    {
        [HDR] _GlowColor ("Glow Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _GlowMode ("Glow Mode (0=Solid, 1=DVD Rainbow)", Float) = 0.0
        _ProximityAlpha ("Proximity Alpha", Range(0.0, 1.0)) = 0.0
        _OutlineWidth ("Outline Screen Thickness", Range(0.0, 5.0)) = 1.0
        _FillOpacity ("Fill Interior Opacity", Range(0.0, 1.0)) = 0.58
        _RimStrength ("Rim Edge Strength", Range(0.0, 2.0)) = 0.75
        _DepthBias ("Occlusion Depth Margin", Float) = 0.06
        [HideInInspector] _MeshCenterOS ("Mesh Bounds Center OS", Vector) = (0.0, 0.0, 0.0, 0.0)
        _StencilRef ("Stencil Ref", Float) = 44
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comp", Float) = 8
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilPass ("Stencil Pass", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+200"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "OccludedGlowPass"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            Stencil
            {
                Ref [_StencilRef]
                Comp [_StencilComp]
                Pass [_StencilPass]
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float eyeDepth : TEXCOORD2;
                float2 radialDir : TEXCOORD3;
                float3 positionOS : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _GlowColor;
                float _GlowMode;
                float _ProximityAlpha;
                float _OutlineWidth;
                float _FillOpacity;
                float _RimStrength;
                float _DepthBias;
                float4 _MeshCenterOS;
            CBUFFER_END

            bool IsSkyDepth(float rawDepth)
            {
                #if UNITY_REVERSED_Z
                    return rawDepth <= 0.00001;
                #else
                    return rawDepth >= 0.99999;
                #endif
            }

            half3 SamplePrismaticSpectrum(float t)
            {
                t = saturate(t);
                half3 c0 = half3(1.00h, 0.05h, 0.08h); // Red
                half3 c1 = half3(1.00h, 0.45h, 0.02h); // Orange
                half3 c2 = half3(1.00h, 0.92h, 0.04h); // Yellow
                half3 c3 = half3(0.10h, 0.98h, 0.25h); // Green
                half3 c4 = half3(0.02h, 0.90h, 1.00h); // Cyan
                half3 c5 = half3(0.10h, 0.30h, 1.00h); // Royal Blue
                half3 c6 = half3(0.65h, 0.10h, 0.98h); // Violet

                half3 col = c0;
                col = lerp(col, c1, smoothstep(0.00, 0.18, t));
                col = lerp(col, c2, smoothstep(0.16, 0.34, t));
                col = lerp(col, c3, smoothstep(0.32, 0.52, t));
                col = lerp(col, c4, smoothstep(0.50, 0.70, t));
                col = lerp(col, c5, smoothstep(0.68, 0.85, t));
                col = lerp(col, c6, smoothstep(0.83, 1.00, t));
                return col;
            }

            half4 EvaluatePrismaticRainbowHalo(float2 normUV, float3 normalWS, float3 viewDirWS, bool isOutline)
            {
                float r = length(normUV);
                float2 safeDir = r > 1e-4 ? (normUV / r) : float2(0.0, 1.0);
                float angle = atan2(safeDir.y, safeDir.x);
                float tAnim = _Time.y * 0.55;
                float viewShift = dot(normalize(normalWS), viewDirWS) * 0.45;

                // Diffraction striations, starburst rays and DVD reflection lobes
                float fineStriation = 0.5 + 0.5 * sin(angle * 30.0 + tAnim * 1.6);
                float spikePrimary  = pow(0.5 + 0.5 * cos(angle * 7.0 - tAnim * 0.65 + viewShift), 2.4);
                float spikeSecond   = pow(0.5 + 0.5 * cos(angle * 13.0 + 1.15 + tAnim * 0.40), 3.0);
                float dvdLobe       = 0.58 + 0.42 * cos(angle * 2.0 - tAnim * 0.50 + 0.4);
                float rayMask       = saturate(0.32 + 0.44 * spikePrimary + 0.34 * spikeSecond + 0.14 * fineStriation) * dvdLobe;

                float rayPush = spikePrimary * 0.22 + spikeSecond * 0.14 + fineStriation * 0.04;
                float rEff = max(0.0, r - rayPush * 0.65);

                // Chromatic aberration RGB ring separation
                float2 offsetR = float2(0.055, 0.040);
                float2 offsetB = float2(-0.055, -0.040);
                float rChromaR = max(0.0, length(normUV + offsetR) - rayPush * 0.60);
                float rChromaG = max(0.0, length(normUV) - rayPush * 0.60);
                float rChromaB = max(0.0, length(normUV + offsetB) - rayPush * 0.60);

                float ringCenter = 0.72;
                float ringWidth  = 0.15;
                half3 chromaRgb = half3(
                    exp(-pow((rChromaR - ringCenter) / ringWidth, 2.0)),
                    exp(-pow((rChromaG - ringCenter) / ringWidth, 2.0)),
                    exp(-pow((rChromaB - ringCenter) / ringWidth, 2.0))
                );

                // Concentric inside-out spectrum: Red -> Orange -> Yellow -> Green -> Cyan -> Blue -> Violet
                float tSpec = saturate((rEff - 0.48) / 0.78);
                half3 specColor = SamplePrismaticSpectrum(tSpec);

                // Thin bright refraction crest along inner ring edge
                float crest = exp(-pow((rEff - 0.64) / 0.11, 2.0));
                half3 combinedHalo = lerp(specColor * (0.65h + 0.65h * (half)rayMask), chromaRgb * 1.05h, 0.38h);
                combinedHalo = saturate(combinedHalo + (half)(crest * 0.38) * half3(1.0h, 0.96h, 0.92h));

                if (!isOutline)
                {
                    // Dark silhouette core contrasted by bright rainbow rim & RGB chromatic edge fringes
                    float coreFade = smoothstep(0.32, 0.72, rEff);
                    float innerRayGlow = smoothstep(0.24, 0.82, rEff) * (0.25 + 0.75 * rayMask);
                    half3 darkCore = half3(0.025h, 0.025h, 0.045h);
                    half3 interiorRgb = lerp(darkCore, combinedHalo, saturate((half)(coreFade * 0.85 + innerRayGlow * 0.35)));
                    interiorRgb = saturate(interiorRgb + chromaRgb * (half)(smoothstep(0.40, 0.85, r) * 0.35));
                    float fillAlpha = lerp(0.76, 0.94, saturate(coreFade + innerRayGlow * 0.4));
                    return half4(interiorRgb, fillAlpha);
                }
                else
                {
                    float outerFade = smoothstep(1.75, 0.82, r - rayPush * 0.55);
                    float rayBody = saturate(0.55 + 0.55 * rayMask);
                    half3 outRgb = saturate(combinedHalo * (half)(0.85 + 0.35 * rayBody));
                    float outAlpha = saturate(outerFade * lerp(0.55, 1.0, rayMask));
                    return half4(outRgb, outAlpha);
                }
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 posCS = TransformWorldToHClip(posWS);

                float3 centerWS = TransformObjectToWorld(_MeshCenterOS.xyz);
                float3 posVS = TransformWorldToView(posWS);
                float3 centerVS = TransformWorldToView(centerWS);

                if (_OutlineWidth > 0.0001)
                {
                    float4 centerCS = TransformWorldToHClip(centerWS);
                    float2 centerNDC = centerCS.xy / max(abs(centerCS.w), 0.0001);
                    float2 vertNDC = posCS.xy / max(abs(posCS.w), 0.0001);
                    float2 radial2D = vertNDC - centerNDC;
                    float2 rDir = length(radial2D) > 0.0001 ? normalize(radial2D) : float2(0.0, 1.0);
                    float3 viewNormal = TransformWorldToViewDir(normalWS, true);
                    float2 nDir = length(viewNormal.xy) > 0.0001 ? normalize(viewNormal.xy) : rDir;
                    float2 dir2D = normalize(lerp(nDir, rDir, 0.65) + float2(1e-5, 1e-5));
                    float aspect = _ScreenParams.y / max(_ScreenParams.x, 1.0);
                    float pxClip = clamp(_OutlineWidth, 0.035, 0.25) * 0.22;
                    posCS.xy += float2(dir2D.x * aspect, dir2D.y) * (pxClip * abs(posCS.w));

                    float4 extrudedVS = mul(UNITY_MATRIX_I_P, posCS);
                    posVS.xy = extrudedVS.xy / max(abs(extrudedVS.w), 0.0001);
                }

                float worldRad = max(_MeshCenterOS.w, 0.08);
                output.positionWS = posWS;
                output.normalWS = normalWS;
                output.positionCS = posCS;
                output.eyeDepth = -TransformWorldToView(posWS).z;
                output.radialDir = (posVS.xy - centerVS.xy) / worldRad;
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            float ComputeSceneEyeDepth(float rawDepth)
            {
                if (unity_OrthoParams.w > 0.5)
                {
                    #if UNITY_REVERSED_Z
                        return lerp(_ProjectionParams.z, _ProjectionParams.y, rawDepth);
                    #else
                        return lerp(_ProjectionParams.y, _ProjectionParams.z, rawDepth);
                    #endif
                }
                return LinearEyeDepth(rawDepth, _ZBufferParams);
            }

            half4 frag(Varyings input) : SV_Target
            {
                clip(_ProximityAlpha - 0.005);

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawSceneDepth = SampleSceneDepth(screenUV);
                if (IsSkyDepth(rawSceneDepth))
                    discard;

                float sceneEyeDepth = ComputeSceneEyeDepth(rawSceneDepth);

                // Discard pixels that are NOT strictly behind an opaque occluder
                clip(input.eyeDepth - sceneEyeDepth - _DepthBias);

                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 safeNormalWS = normalize(input.normalWS);

                if (_GlowMode > 0.5)
                {
                    half4 rainbow = EvaluatePrismaticRainbowHalo(input.radialDir, safeNormalWS, viewDirWS, _OutlineWidth > 0.0001);
                    rainbow.a = saturate(rainbow.a * _ProximityAlpha);
                    clip(rainbow.a - 0.004h);
                    return rainbow;
                }

                float ndotv = saturate(abs(dot(safeNormalWS, viewDirWS)));
                float rim = pow(1.0 - ndotv, 1.65);

                half3 baseRgb = saturate(_GlowColor.rgb);

                float alpha;
                half3 rgb;
                if (_OutlineWidth > 0.0001)
                {
                    // Avoid high HDR boost that desaturates colors into white under URP Bloom
                    rgb = baseRgb * 1.08h;
                    alpha = _ProximityAlpha;
                }
                else
                {
                    rgb = lerp(baseRgb * 0.72h, baseRgb * 1.05h, rim);
                    alpha = saturate(_FillOpacity * (0.68 + 0.42 * rim * max(_RimStrength, 0.5))) * _ProximityAlpha;
                }

                return half4(rgb, saturate(alpha));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
