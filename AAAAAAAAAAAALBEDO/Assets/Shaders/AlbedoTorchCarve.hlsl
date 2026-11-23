#ifndef ALBEDO_TORCH_CARVE_INCLUDED
#define ALBEDO_TORCH_CARVE_INCLUDED

#define ALBEDO_MAX_TORCHES 16

int _AlbedoTorchCount;
float4 _AlbedoTorchSpheres[ALBEDO_MAX_TORCHES];   // xyz = centerWS, w = suppressionRadius
float4 _AlbedoTorchParams[ALBEDO_MAX_TORCHES];    // x = edgeSoftness (0.05..0.95), y = strength (0..1), z = isHeld (0 ou 1)
float4 _AlbedoTorchFacing[ALBEDO_MAX_TORCHES];    // xyz = playerForward (plano XZ), w = isHeld (1.0 se segurada, 0.0 se solta)
float4 _AlbedoTorchPlayerPos[ALBEDO_MAX_TORCHES]; // xyz = playerBodyCenterWS, w = bodyRadius

float EvaluatePlayerBodyTorchOcclusion(float3 positionWS, int torchIdx)
{
    float isHeld = _AlbedoTorchFacing[torchIdx].w;
    if (isHeld < 0.5) return 1.0; // Tocha no chão emite em 360 graus livremente

    float3 playerFwd = _AlbedoTorchFacing[torchIdx].xyz;
    float3 playerPos = _AlbedoTorchPlayerPos[torchIdx].xyz;
    float bodyRadius = max(_AlbedoTorchPlayerPos[torchIdx].w, 0.45);

    // Vetores no plano horizontal XZ relativos ao jogador
    float2 toPtFromPlayer = positionWS.xz - playerPos.xz;
    float distFromPlayer = length(toPtFromPlayer);
    float2 dirFromPlayer = distFromPlayer > 0.001 ? (toPtFromPlayer / distFromPlayer) : playerFwd.xz;

    // Zona segura dos pés: o chão sob os pés do jogador e ao redor do corpo
    // é 100% preservado com sombra para o jogador poder andar nas pontes de sombra.
    float footSafeRadius = max(0.95, bodyRadius * 1.85);
    float footSafeZone = 1.0 - smoothstep(footSafeRadius * 0.75, footSafeRadius, distFromPlayer);
    if (footSafeZone >= 0.999) return 1.0;

    // Fora da zona dos pés, o corpo bloqueia a "escuridão" apenas nas costas do personagem (dot < 0):
    float dotPlayer = dot(dirFromPlayer, playerFwd.xz);
    float backBlock = (1.0 - smoothstep(-0.35, 0.15, dotPlayer)) * (1.0 - footSafeZone);

    return saturate(1.0 - backBlock);
}

float EvaluateTorchHoleMask(float3 positionWS, float3 center, float radius, float softness, float strength, float dist, int torchIdx)
{
    float3 dir = dist > 0.0001 ? normalize(positionWS - center) : float3(0.0, 1.0, 0.0);
    float rippleScale = saturate(dist / max(radius * 0.2, 0.001));
    float ripple = sin(dir.x * 5.0 + _Time.y * 2.2) * cos(dir.z * 5.0 - _Time.y * 1.8) * 0.05 * radius * rippleScale;
    float innerR = radius * (1.0 - softness);
    float holeMask = 1.0 - smoothstep(innerR, radius, dist + ripple);
    float bodyOcc = EvaluatePlayerBodyTorchOcclusion(positionWS, torchIdx);
    return holeMask * strength * bodyOcc;
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

        maxCarve = max(maxCarve, EvaluateTorchHoleMask(positionWS, center, radius, softness, strength, dist, i));
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

        maxCarve = max(maxCarve, EvaluateTorchHoleMask(positionWS, center, radius, softness, strength, dist, i));
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
