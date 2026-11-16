#ifndef ALBEDO_HALFTONE_LIGHTING_INCLUDED
#define ALBEDO_HALFTONE_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

#define ALBEDO_MAX_BEAMS 24

int _AlbedoBeamCount;
float4 _AlbedoBeamStarts[ALBEDO_MAX_BEAMS]; // xyz = originWS, w = topRadius
float4 _AlbedoBeamDirs[ALBEDO_MAX_BEAMS];   // xyz = dirWS, w = hitLength
float4 _AlbedoBeamColors[ALBEDO_MAX_BEAMS]; // rgb = color, w = bottomRadius
float4 _AlbedoBeamParams[ALBEDO_MAX_BEAMS]; // x = fullConeLength, y = isPrismBeam, z = strength

void AccumulateUnityLight(Light lt, float3 normalWS, float weight, inout float totalIntensity, inout half3 accumColor)
{
    if (weight <= 0.0001) return;
    float NdotL = saturate(dot(normalWS, lt.direction));
    float atten = NdotL * lt.distanceAttenuation * lt.shadowAttenuation;
    if (atten <= 0.0001) return;

    float maxCol = max(lt.color.r, max(lt.color.g, lt.color.b));
    if (maxCol <= 0.0001) return;

    // Compressao suave evita saturar a parede em branco mesmo com luzes de intensidade alta (ex: 2000)
    float contrib = (1.0 - exp(-atten * maxCol * 0.18)) * weight;
    half3 normalizedColor = lt.color / (half)maxCol;
    totalIntensity += contrib;
    accumColor += normalizedColor * (half)contrib;
}

float EvaluateAlbedoWallLight(
    float3 positionWS,
    float3 normalWS,
    float4 positionCS,
    float mainLightWeight,
    float unityLightWeight,
    float beamIntensity,
    out half3 outLightColor)
{
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.normalWS = normalWS;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);

    float totalIntensity = 0.0;
    half3 accumColor = half3(0.0h, 0.0h, 0.0h);

    Light mainLight = GetMainLight(TransformWorldToShadowCoord(positionWS));
    AccumulateUnityLight(mainLight, normalWS, mainLightWeight, totalIntensity, accumColor);

    #if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();
    #if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint dirIdx = 0; dirIdx < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); dirIdx++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light addLight = GetAdditionalLight(dirIdx, positionWS, half4(1, 1, 1, 1));
        AccumulateUnityLight(addLight, normalWS, unityLightWeight, totalIntensity, accumColor);
    }
    #endif
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light addLight = GetAdditionalLight(lightIndex, positionWS, half4(1, 1, 1, 1));
        AccumulateUnityLight(addLight, normalWS, unityLightWeight, totalIntensity, accumColor);
    LIGHT_LOOP_END
    #endif

    int beamCount = min(_AlbedoBeamCount, ALBEDO_MAX_BEAMS);
    [loop] for (int i = 0; i < beamCount; i++)
    {
        float3 origin = _AlbedoBeamStarts[i].xyz;
        float topRadius = _AlbedoBeamStarts[i].w;
        float3 dir = _AlbedoBeamDirs[i].xyz;
        float hitLen = _AlbedoBeamDirs[i].w;
        half3 beamCol = (half3)_AlbedoBeamColors[i].rgb;
        float bottomRadius = _AlbedoBeamColors[i].w;
        float fullLen = max(_AlbedoBeamParams[i].x, 0.01);
        float strength = _AlbedoBeamParams[i].z;

        float3 toP = positionWS - origin;
        float tProj = dot(toP, dir);
        if (tProj < -0.15 || tProj > hitLen + 0.65)
            continue;

        float coneRadius = lerp(topRadius, bottomRadius, saturate(tProj / fullLen));
        float radialDist = length(toP - dir * tProj);
        float radialMask = 1.0 - smoothstep(coneRadius * 0.35, coneRadius * 1.15, radialDist);
        if (radialMask <= 0.001)
            continue;

        float longMask = smoothstep(-0.15, 0.25, tProj) * (1.0 - smoothstep(max(0.0, hitLen - 0.15), hitLen + 0.65, tProj));
        float backCheck = dot(normalWS, -dir);
        float3 toOrigin = normalize(origin - positionWS + float3(0.0, 0.0001, 0.0));
        float facing = saturate(dot(normalWS, normalize(lerp(-dir, toOrigin, 0.45))) * 0.9 + 0.25)
                     * smoothstep(-0.25, 0.05, backCheck);

        float contrib = radialMask * longMask * facing * strength * beamIntensity;
        totalIntensity += contrib;
        accumColor += beamCol * (half)contrib;
    }

    half maxC = max(accumColor.r, max(accumColor.g, accumColor.b));
    outLightColor = maxC > 0.0001h ? (accumColor / maxC) : half3(1.0h, 1.0h, 1.0h);
    return totalIntensity;
}

#endif
