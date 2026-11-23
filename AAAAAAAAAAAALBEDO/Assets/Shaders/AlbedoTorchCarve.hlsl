#ifndef ALBEDO_TORCH_CARVE_INCLUDED
#define ALBEDO_TORCH_CARVE_INCLUDED

#define ALBEDO_MAX_TORCHES 16

int _AlbedoTorchCount;
float4 _AlbedoTorchSpheres[ALBEDO_MAX_TORCHES]; // xyz = centerWS, w = suppressionRadius
float4 _AlbedoTorchParams[ALBEDO_MAX_TORCHES];  // x = edgeSoftness (0.05..0.95), y = strength (0..1)

float EvaluateTorchHoleMask(float3 positionWS, float3 center, float radius, float softness, float strength, float dist)
{
    float3 dir = dist > 0.0001 ? normalize(positionWS - center) : float3(0.0, 1.0, 0.0);
    float rippleScale = saturate(dist / max(radius * 0.2, 0.001));
    float ripple = sin(dir.x * 5.0 + _Time.y * 2.2) * cos(dir.z * 5.0 - _Time.y * 1.8) * 0.05 * radius * rippleScale;
    float innerR = radius * (1.0 - softness);
    float holeMask = 1.0 - smoothstep(innerR, radius, dist + ripple);
    return holeMask * strength;
}

float EvaluateTorchShadowCarve(float3 positionWS, bool volumetricViewCompensation)
{
    int torchCount = min(_AlbedoTorchCount, ALBEDO_MAX_TORCHES);
    if (torchCount <= 0) return 0.0;

    float maxCarve = 0.0;
    [loop] for (int i = 0; i < torchCount; i++)
    {
        float3 center = _AlbedoTorchSpheres[i].xyz;
        float radius = max(_AlbedoTorchSpheres[i].w, 0.05);
        float softness = clamp(_AlbedoTorchParams[i].x, 0.05, 0.95);
        float strength = saturate(_AlbedoTorchParams[i].y);
        if (strength <= 0.001) continue;

        float dist;
        if (volumetricViewCompensation)
        {
            float3 delta = positionWS - center;
            float3 viewFwd = GetViewForwardDir();
            float dView = dot(delta, viewFwd);
            float dPerp = length(delta - viewFwd * dView);
            dist = length(float2(dPerp, dView * 0.45));
        }
        else
        {
            dist = distance(positionWS, center);
        }

        maxCarve = max(maxCarve, EvaluateTorchHoleMask(positionWS, center, radius, softness, strength, dist));
    }

    return saturate(maxCarve);
}

float EvaluateTorchLightMultiplier(float3 positionWS, bool volumetricViewCompensation)
{
    return 1.0 - EvaluateTorchShadowCarve(positionWS, volumetricViewCompensation);
}

float EvaluateTorchBeamCarve(
    float3 positionWS,
    float3 beamOrigin,
    float3 beamDir,
    float topRadius,
    float bottomRadius,
    float fullConeLen,
    bool isHorizontalPrismBeam)
{
    int torchCount = min(_AlbedoTorchCount, ALBEDO_MAX_TORCHES);
    if (torchCount <= 0) return 0.0;

    float3 toP = positionWS - beamOrigin;
    float tP = dot(toP, beamDir);
    float3 perpP = toP - beamDir * tP;
    float safeFullLen = max(fullConeLen, 0.05);
    float coneR_P = max(0.05, lerp(topRadius, bottomRadius, saturate(tP / safeFullLen)));

    float maxCarve = 0.0;
    [loop] for (int i = 0; i < torchCount; i++)
    {
        float3 center = _AlbedoTorchSpheres[i].xyz;
        float radius = max(_AlbedoTorchSpheres[i].w, 0.05);
        float softness = clamp(_AlbedoTorchParams[i].x, 0.05, 0.95);
        float strength = saturate(_AlbedoTorchParams[i].y);
        if (strength <= 0.001) continue;

        float3 toT = center - beamOrigin;
        float tT = dot(toT, beamDir);
        float3 perpT = toT - beamDir * tT;

        float dist;
        if (tT > -radius * 0.85 && tP > tT)
        {
            // Ponto atrás da tocha ao longo do feixe: projeta sombra até o fim do feixe
            float coneR_T = max(0.05, lerp(topRadius, bottomRadius, saturate(max(0.0, tT) / safeFullLen)));
            float scaleWake = max(1.0, coneR_P / max(coneR_T, 0.1));
            float3 perpDelta = (perpP / scaleWake) - perpT;
            if (isHorizontalPrismBeam) perpDelta.y *= 0.55;
            dist = length(perpDelta);
        }
        else
        {
            float3 delta = positionWS - center;
            if (isHorizontalPrismBeam) delta.y *= 0.55;
            dist = length(delta);
        }

        maxCarve = max(maxCarve, EvaluateTorchHoleMask(positionWS, center, radius, softness, strength, dist));
    }

    return saturate(maxCarve);
}

float EvaluateTorchBeamMultiplier(
    float3 positionWS,
    float3 beamOrigin,
    float3 beamDir,
    float topRadius,
    float bottomRadius,
    float fullConeLen,
    bool isHorizontalPrismBeam)
{
    return 1.0 - EvaluateTorchBeamCarve(positionWS, beamOrigin, beamDir, topRadius, bottomRadius, fullConeLen, isHorizontalPrismBeam);
}

#endif
