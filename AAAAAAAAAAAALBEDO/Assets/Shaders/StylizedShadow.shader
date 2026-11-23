Shader "Custom/URP_StylizedShadow"
{
    Properties
    {
        _ShadowColor ("Shadow Color", Color) = (0.055, 0.06, 0.075, 0.94)
        _NeutralBodyColor ("Neutral Body Color", Color) = (0.025, 0.028, 0.038, 1.0)
        _NeutralVeinColor ("Neutral Vein Color", Color) = (0.15, 0.165, 0.21, 1.0)
        _RimStrength ("Rim Edge Visibility", Range(0.0, 1.0)) = 0.45
        _TintColor ("Required Color Tint", Color) = (0.0, 0.0, 0.0, 0.0)
        _ColorTintStrength ("Color Body Strength", Range(0.0, 1.0)) = 0.45
        _VeinTintStrength ("Color Vein Strength", Range(0.0, 1.0)) = 0.92
        [PerRendererData] _RequiredColor ("Required Light Color (A=EnforceExact)", Color) = (1.0, 1.0, 1.0, 0.0)
        [PerRendererData] _ColorTolerance ("Color Match Tolerance", Float) = 0.28
        _CarveRadiusScale ("Hole Size Multiplier", Range(0.1, 4.0)) = 1.0
        _CarveRadiusOffset ("Hole Radius Offset (m)", Range(-2.0, 5.0)) = 0.2
        _CarveEdgeSoftness ("Hole Edge Softness", Range(0.05, 0.95)) = 0.38
        [PerRendererData] _InstanceCarveParams ("Instance Carve Params", Vector) = (1.0, 0.0, 1.0, 0.0)
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
            #include "Assets/Shaders/AlbedoTorchCarve.hlsl"

            #define ALBEDO_MAX_BEAMS 24

            int _AlbedoBeamCount;
            float4 _AlbedoBeamStarts[ALBEDO_MAX_BEAMS]; // xyz = originWS, w = topRadius
            float4 _AlbedoBeamDirs[ALBEDO_MAX_BEAMS];   // xyz = dirWS, w = hitLength
            float4 _AlbedoBeamColors[ALBEDO_MAX_BEAMS]; // rgb = color, w = bottomRadius
            float4 _AlbedoBeamParams[ALBEDO_MAX_BEAMS]; // x = fullConeLength, y = isPrismBeam, z = strength

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _ShadowColor;
                half4 _NeutralBodyColor;
                half4 _NeutralVeinColor;
                float _RimStrength;
                half4 _TintColor;
                half _ColorTintStrength;
                half _VeinTintStrength;
                half4 _RequiredColor;
                float _ColorTolerance;
                float _CarveRadiusScale;
                float _CarveRadiusOffset;
                float _CarveEdgeSoftness;
                float4 _InstanceCarveParams;
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

            float EvaluateShadowCarve(float3 positionWS, out float3 outLightColor)
            {
                float maxWeight = 0.0;
                float3 accumColor = float3(0.0, 0.0, 0.0);
                int beamCount = min(_AlbedoBeamCount, ALBEDO_MAX_BEAMS);
                bool ignorePrismBeams = _InstanceCarveParams.w > 1.5;

                [loop] for (int i = 0; i < beamCount; i++)
                {
                    if (ignorePrismBeams && _AlbedoBeamParams[i].y > 0.5) continue;
                    float strength = _AlbedoBeamParams[i].z;
                    if (strength <= 0.001) continue;

                    float3 origin = _AlbedoBeamStarts[i].xyz;
                    float topRadius = _AlbedoBeamStarts[i].w;
                    float3 dir = _AlbedoBeamDirs[i].xyz;
                    float hitLen = max(_AlbedoBeamDirs[i].w, 0.001);
                    float3 beamCol = _AlbedoBeamColors[i].rgb;
                    float bottomRadius = _AlbedoBeamColors[i].w;
                    float fullLen = max(_AlbedoBeamParams[i].x, 0.01);

                    float3 toP = positionWS - origin;
                    float tProj = dot(toP, dir);
                    bool isHoriz = _AlbedoBeamParams[i].y > 0.5;
                    float endPad = isHoriz ? 0.25 : 3.0;
                    if (tProj < -0.45 || tProj > hitLen + endPad) continue;

                    float scaleMult = _CarveRadiusScale * (_InstanceCarveParams.w > 0.5 ? _InstanceCarveParams.x : 1.0);
                    float radOffset = _CarveRadiusOffset + (_InstanceCarveParams.w > 0.5 ? _InstanceCarveParams.y : 0.0);
                    float softness = clamp(_CarveEdgeSoftness * (_InstanceCarveParams.w > 0.5 ? _InstanceCarveParams.z : 1.0), 0.04, 0.95);
                    float baseConeRadius = lerp(topRadius, bottomRadius, saturate(tProj / fullLen));
                    float coneRadius = max(0.05, baseConeRadius * scaleMult + radOffset);
                    float radialDist = length(toP - dir * tProj);
                    float radialMask = 1.0 - smoothstep(coneRadius * (1.0 - softness), coneRadius + 0.2, radialDist);
                    if (radialMask <= 0.001) continue;

                    float fadeStart = isHoriz ? max(0.0, hitLen - 0.10) : (hitLen + 1.8);
                    float longMask = smoothstep(-0.45, 0.08, tProj)
                                   * (1.0 - smoothstep(fadeStart, hitLen + endPad, tProj));
                    float beamTorchMult = EvaluateTorchBeamMultiplier(positionWS, origin, dir, topRadius, bottomRadius, fullLen, _AlbedoBeamParams[i].y > 0.5);
                    float w = radialMask * longMask * saturate(strength) * beamTorchMult;

                    accumColor += beamCol * w;
                    maxWeight = max(maxWeight, w);
                }

                if (maxWeight <= 0.001)
                {
                    outLightColor = float3(0.0, 0.0, 0.0);
                    return 0.0;
                }

                float maxAccum = max(accumColor.r, max(accumColor.g, accumColor.b));
                float3 mixedColor = maxAccum > 0.0001 ? (accumColor / maxAccum) : float3(1.0, 1.0, 1.0);
                outLightColor = mixedColor;

                if (_RequiredColor.a > 0.5)
                {
                    float maxReq = max(_RequiredColor.r, max(_RequiredColor.g, _RequiredColor.b));
                    float3 reqNorm = maxReq > 0.0001 ? (_RequiredColor.rgb / maxReq) : float3(1.0, 1.0, 1.0);
                    float distExact = distance(mixedColor, reqNorm) / 1.73205;
                    float distWhite = distance(mixedColor, float3(1.0, 1.0, 1.0)) / 1.73205;
                    float dist = min(distExact, distWhite);
                    float tol = max(_ColorTolerance, 0.05);
                    float colorMatch = 1.0 - smoothstep(tol * 0.75, tol * 1.15, dist);
                    return saturate(maxWeight * colorMatch);
                }

                return saturate(maxWeight);
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
                output.positionWS = vertexInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
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
                float baseAlpha = lerp(_MinAlpha, 1.0, voronoiMask) * _ShadowColor.a;

                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 nWS = normalize(input.normalWS);
                float fresnel = pow(1.0 - saturate(abs(dot(nWS, viewDirWS))), 2.2);

                half3 neutralBody = lerp(_NeutralBodyColor.rgb, _NeutralVeinColor.rgb, (half)(voronoiMask * 0.85));
                neutralBody += _NeutralVeinColor.rgb * (half)(fresnel * _RimStrength);

                half3 darkBody = lerp(_ShadowColor.rgb, _TintColor.rgb * 0.35h, _ColorTintStrength);
                half3 coloredVeins = _TintColor.rgb * _VeinTintStrength;
                half3 tintedRGB = lerp(darkBody, coloredVeins, (half)voronoiMask)
                                + _TintColor.rgb * (half)(fresnel * _RimStrength * 0.85);

                half3 finalRGB = lerp(neutralBody, tintedRGB, saturate(_TintColor.a));

                float3 lightCol;
                float carve = EvaluateShadowCarve(input.positionWS, lightCol);
                float noisyCarve = smoothstep(0.12, 0.78, carve + (dist - 0.45) * 0.22 * step(0.02, carve));
                float holeEdge = smoothstep(0.05, 0.35, noisyCarve) * (1.0 - smoothstep(0.62, 0.95, noisyCarve));

                finalRGB += (half3)max(lightCol, float3(0.45, 0.5, 0.6)) * (half)(holeEdge * 0.85);
                float finalAlpha = baseAlpha * (1.0 - noisyCarve);
                clip(finalAlpha - 0.02);

                return half4(finalRGB, saturate(finalAlpha));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
