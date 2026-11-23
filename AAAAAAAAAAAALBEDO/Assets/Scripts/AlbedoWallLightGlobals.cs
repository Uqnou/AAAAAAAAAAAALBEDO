using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Sincroniza feixes dos Prismas e GodRays ativos com os shaders das paredes via arrays globais da GPU,
/// sem alocar memória por frame e sem criar Unity Lights extras.
/// </summary>
#if UNITY_EDITOR
[InitializeOnLoad]
#endif
public static class AlbedoWallLightGlobals
{
    public const int MaxBeams = 24;
    public const int MaxTorches = 16;

    private static readonly HashSet<PrismaController> ActivePrisms = new HashSet<PrismaController>();
    private static readonly HashSet<GodRayBeam> ActiveGodRays = new HashSet<GodRayBeam>();
    private static readonly HashSet<ChromaticFilterGate> ActiveGates = new HashSet<ChromaticFilterGate>();
    private static readonly HashSet<TorchController> ActiveTorches = new HashSet<TorchController>();

    private static readonly Vector4[] BeamStarts = new Vector4[MaxBeams], BeamDirs = new Vector4[MaxBeams];
    private static readonly Vector4[] BeamColors = new Vector4[MaxBeams], BeamParams = new Vector4[MaxBeams];
    private static readonly Vector4[] TorchSpheres = new Vector4[MaxTorches], TorchParams = new Vector4[MaxTorches];
    private static readonly Vector4[] TorchFacing = new Vector4[MaxTorches], TorchPlayerPos = new Vector4[MaxTorches];

    private static readonly int BeamCountId = Shader.PropertyToID("_AlbedoBeamCount");
    private static readonly int BeamStartsId = Shader.PropertyToID("_AlbedoBeamStarts");
    private static readonly int BeamDirsId = Shader.PropertyToID("_AlbedoBeamDirs");
    private static readonly int BeamColorsId = Shader.PropertyToID("_AlbedoBeamColors");
    private static readonly int BeamParamsId = Shader.PropertyToID("_AlbedoBeamParams");
    private static readonly int TorchCountId = Shader.PropertyToID("_AlbedoTorchCount");
    private static readonly int TorchSpheresId = Shader.PropertyToID("_AlbedoTorchSpheres");
    private static readonly int TorchParamsId = Shader.PropertyToID("_AlbedoTorchParams");
    private static readonly int TorchFacingId = Shader.PropertyToID("_AlbedoTorchFacing");
    private static readonly int TorchPlayerPosId = Shader.PropertyToID("_AlbedoTorchPlayerPos");

    private static bool arraysInitialized = false;
    private static int activeBeamCount = 0, activeTorchCount = 0, lastPushFrame = -1;

#if UNITY_EDITOR
    static AlbedoWallLightGlobals()
    {
        EditorApplication.update += OnEditorUpdate;
    }

    private static void OnEditorUpdate()
    {
        if (!Application.isPlaying) PushToShaderGlobals();
    }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitRuntimeRunner()
    {
        if (Object.FindFirstObjectByType<AlbedoWallLightRunner>() == null)
        {
            GameObject go = new GameObject("[ALBEDO_WallLightSync]") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<AlbedoWallLightRunner>();
        }
    }

    public static IReadOnlyCollection<PrismaController> Prisms => ActivePrisms;
    public static void Register(PrismaController p) { if (p != null) ActivePrisms.Add(p); }
    public static void Unregister(PrismaController p) { if (p != null) ActivePrisms.Remove(p); }
    public static void RegisterGate(ChromaticFilterGate g) { if (g != null) ActiveGates.Add(g); }
    public static void UnregisterGate(ChromaticFilterGate g) { if (g != null) ActiveGates.Remove(g); }
    public static void RegisterTorch(TorchController t) { if (t != null) ActiveTorches.Add(t); }
    public static void UnregisterTorch(TorchController t) { if (t != null) ActiveTorches.Remove(t); }

    public static bool IsPassThroughCollider(Collider col, Transform self, Transform ignoreTarget = null)
    {
        if (col == null || col.transform.IsChildOf(self) || self.IsChildOf(col.transform)) return true;
        if (ignoreTarget != null && col.transform.IsChildOf(ignoreTarget)) return true;
        if (col.GetComponentInParent<SombraController>() != null || col.gameObject.name.StartsWith("Sombra")) return true;
        if (col.GetComponentInParent<PrismaController>() != null || col.GetComponentInParent<PlayerController>() != null) return true;
        ChromaticFilterGate gate = col.GetComponentInParent<ChromaticFilterGate>();
        PrismaController selfPrism = self != null ? self.GetComponent<PrismaController>() : null;
        return gate != null && selfPrism != null && gate.OwnsEmitter(selfPrism);
    }

    public static bool IsBlockedByWall(Vector3 origin, Vector3 target, Transform self, Transform targetTransform, LayerMask obstacleLayers)
    {
        Vector3 delta = target - origin; float dist = delta.magnitude;
        if (dist <= 0.05f) return false;
        foreach (var h in Physics.RaycastAll(origin, delta / dist, dist, obstacleLayers, QueryTriggerInteraction.Ignore))
            if (!IsPassThroughCollider(h.collider, self, targetTransform)) return true;
        return false;
    }

    public static void CollectPrismsInCone(
        PrismaController source, Vector3 origin, Vector3 dir, float hitDist, float maxDist,
        float topR, float botR, LayerMask obstacleLayers, HashSet<PrismaController> newlyHitPrisms)
    {
        foreach (var p in ActivePrisms)
        {
            if (p == null || p == source || !p.isActiveAndEnabled || newlyHitPrisms.Contains(p) || source.HasUpstreamSource(p, 8)) continue;
            Vector3 targetPos = p.GetBeamWorldOrigin();
            Vector3 toP = targetPos - origin; float tProj = Vector3.Dot(toP, dir);
            if (tProj <= 0.05f || tProj > hitDist + 0.5f) continue;
            float coneR = Mathf.Lerp(topR, botR, Mathf.Clamp01(tProj / Mathf.Max(0.01f, maxDist)));
            if ((toP - dir * tProj).magnitude <= coneR + 0.45f &&
                EvaluateTorchBeamMultiplier(targetPos, origin, dir, topR, botR, maxDist, true) >= 0.25f &&
                !IsBlockedByWall(origin, targetPos, source.transform, p.transform, obstacleLayers))
            { newlyHitPrisms.Add(p); p.ReceivePrismBeam(true, source); }
        }
    }

    public static void CollectGatesInCone(
        PrismaController source, Vector3 origin, Vector3 dir, float hitDist, float maxDist,
        float topR, float botR, LayerMask obstacleLayers, HashSet<ChromaticFilterGate> newlyHitGates)
    {
        foreach (var g in ActiveGates)
        {
            if (g == null || !g.isActiveAndEnabled || g.OwnsEmitter(source) || newlyHitGates.Contains(g)) continue;
            Collider col = g.GetComponent<Collider>() ?? g.GetComponentInChildren<Collider>();
            Vector3 targetPos = col != null ? col.ClosestPoint(origin + dir * Mathf.Min(hitDist, Vector3.Distance(origin, g.transform.position))) : g.transform.position;
            Vector3 toG = targetPos - origin; float tProj = Vector3.Dot(toG, dir);
            if (tProj <= 0.05f || tProj > hitDist + 0.6f) continue;
            float coneR = Mathf.Lerp(topR, botR, Mathf.Clamp01(tProj / Mathf.Max(0.01f, maxDist)));
            if ((toG - dir * tProj).magnitude <= coneR + 0.45f &&
                EvaluateTorchBeamMultiplier(targetPos, origin, dir, topR, botR, maxDist, true) >= 0.25f &&
                !IsBlockedByWall(origin, targetPos, source.transform, g.transform, obstacleLayers))
            { newlyHitGates.Add(g); g.ReceivePrismBeam(true, source, dir); }
        }
    }
    public static void Register(GodRayBeam g) { if (g != null) ActiveGodRays.Add(g); }
    public static void Unregister(GodRayBeam g) { if (g != null) ActiveGodRays.Remove(g); }

    public static void PushToShaderGlobals()
    {
        if (!arraysInitialized)
        {
            Shader.SetGlobalVectorArray(BeamStartsId, BeamStarts); Shader.SetGlobalVectorArray(BeamDirsId, BeamDirs);
            Shader.SetGlobalVectorArray(BeamColorsId, BeamColors); Shader.SetGlobalVectorArray(BeamParamsId, BeamParams);
            Shader.SetGlobalVectorArray(TorchSpheresId, TorchSpheres); Shader.SetGlobalVectorArray(TorchParamsId, TorchParams);
            Shader.SetGlobalVectorArray(TorchFacingId, TorchFacing); Shader.SetGlobalVectorArray(TorchPlayerPosId, TorchPlayerPos);
            arraysInitialized = true;
        }

        int count = 0;

        foreach (PrismaController p in ActivePrisms)
        {
            if (count >= MaxBeams) break;
            if (p == null || !p.isActiveAndEnabled || !p.IsEmitting) continue;
            float strength = 1.35f * p.CurrentLightStrength;
            if (strength <= 0.005f) continue;

            Vector3 origin = p.GetBeamWorldOrigin();
            Vector3 dir = p.GetBeamWorldDirection();
            Color c = p.ActiveEmittedColor;

            BeamStarts[count] = new Vector4(origin.x, origin.y, origin.z, p.GodRayTopRadius);
            BeamDirs[count] = new Vector4(dir.x, dir.y, dir.z, p.CurrentHitDistance);
            BeamColors[count] = new Vector4(c.r, c.g, c.b, p.GodRayBottomRadius);
            BeamParams[count] = new Vector4(p.EffectiveMaxLength, 1f, strength, 0f);
            count++;
        }

        foreach (GodRayBeam g in ActiveGodRays)
        {
            if (count >= MaxBeams) break;
            if (g == null || !g.isActiveAndEnabled || g.GetComponentInParent<PrismaController>() != null) continue;
            if (g.BeamMeshRenderer != null && !g.BeamMeshRenderer.enabled) continue;

            LightSource ls = g.AttachedLightSource;
            LightSource parentLs = g.transform.parent != null ? g.transform.parent.GetComponentInParent<LightSource>() : null;
            if ((ls != null && !ls.IsActive) || (parentLs != null && !parentLs.IsActive)) continue;

            float eff = g.EffectiveStrength;
            if (eff <= 0.005f) continue;
            float mult = g.Intensity > 0.001f ? (eff / g.Intensity) : 0f;
            float strength = Mathf.Clamp(g.Intensity * 0.25f, 0.45f, 1.25f) * mult;

            Vector3 origin = g.transform.position;
            Vector3 dir = g.BeamDirection.normalized;
            Color c = g.CurrentColor;

            BeamStarts[count] = new Vector4(origin.x, origin.y, origin.z, g.TopRadius);
            BeamDirs[count] = new Vector4(dir.x, dir.y, dir.z, g.CurrentHeight);
            BeamColors[count] = new Vector4(c.r, c.g, c.b, g.BottomRadius);
            BeamParams[count] = new Vector4(Mathf.Max(g.CurrentHeight, 0.5f), 0f, strength, 0f);
            count++;
        }

        activeBeamCount = count;
        int tCount = 0;
        foreach (TorchController t in ActiveTorches)
        {
            if (tCount >= MaxTorches) break;
            if (t == null || !t.isActiveAndEnabled || !t.IsLit || t.SuppressionRadius <= 0.05f) continue;
            Vector3 p = t.transform.position;
            TorchSpheres[tCount] = new Vector4(p.x, p.y, p.z, t.SuppressionRadius);
            TorchParams[tCount] = new Vector4(t.HoleEdgeSoftness, 1f, t.IsHeldByPlayer ? 1f : 0f, 0f);

            PlayerController hp = t.HoldingPlayer;
            if (t.IsHeldByPlayer && hp != null)
            {
                Vector3 fwd = hp.transform.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude > 0.0001f) fwd.Normalize(); else fwd = Vector3.forward;
                TorchFacing[tCount] = new Vector4(fwd.x, 0f, fwd.z, 1f);
                Vector3 playerBody = hp.transform.position + Vector3.up * 0.9f;
                TorchPlayerPos[tCount] = new Vector4(playerBody.x, playerBody.y, playerBody.z, 0.45f);
            }
            else
            {
                TorchFacing[tCount] = Vector4.zero;
                TorchPlayerPos[tCount] = Vector4.zero;
            }

            tCount++;
        }
        activeTorchCount = tCount;
        if (Application.isPlaying) lastPushFrame = Time.frameCount;

        Shader.SetGlobalInt(BeamCountId, count);
        if (count > 0)
        {
            Shader.SetGlobalVectorArray(BeamStartsId, BeamStarts); Shader.SetGlobalVectorArray(BeamDirsId, BeamDirs);
            Shader.SetGlobalVectorArray(BeamColorsId, BeamColors); Shader.SetGlobalVectorArray(BeamParamsId, BeamParams);
        }
        Shader.SetGlobalInt(TorchCountId, tCount);
        if (tCount > 0)
        {
            Shader.SetGlobalVectorArray(TorchSpheresId, TorchSpheres);
            Shader.SetGlobalVectorArray(TorchParamsId, TorchParams);
            Shader.SetGlobalVectorArray(TorchFacingId, TorchFacing);
            Shader.SetGlobalVectorArray(TorchPlayerPosId, TorchPlayerPos);
        }
    }

    private static float ComputeTorchMask(int idx, float dist, Vector3 worldPos)
    {
        float radius = Mathf.Max(TorchSpheres[idx].w, 0.05f), softness = Mathf.Clamp(TorchParams[idx].x, 0.05f, 0.95f);
        if (dist >= radius) return 0f;
        float innerR = radius * (1f - softness), t = Mathf.Clamp01((dist - innerR) / Mathf.Max(0.0001f, radius - innerR));
        float mask = (1f - t * t * (3f - 2f * t)) * Mathf.Clamp01(TorchParams[idx].y);

        if (TorchFacing[idx].w > 0.5f)
        {
            Vector2 fwd = new Vector2(TorchFacing[idx].x, TorchFacing[idx].z);
            Vector2 pPos = new Vector2(TorchPlayerPos[idx].x, TorchPlayerPos[idx].z);
            Vector2 wPos = new Vector2(worldPos.x, worldPos.z);
            float bodyRadius = Mathf.Max(TorchPlayerPos[idx].w, 0.45f);

            Vector2 toPtP = wPos - pPos;
            float dP = toPtP.magnitude;
            Vector2 dirP = dP > 0.001f ? toPtP / dP : fwd;

            // Zona segura dos pés: garante que a sombra sob os pés do jogador nunca seja tirada
            float footSafeRadius = Mathf.Max(0.95f, bodyRadius * 1.85f);
            float tFoot = Mathf.Clamp01((dP - footSafeRadius * 0.75f) / Mathf.Max(0.0001f, footSafeRadius * 0.25f));
            float footSafeZone = 1f - tFoot * tFoot * (3f - 2f * tFoot);

            if (footSafeZone < 0.999f)
            {
                // Fora dos pés, bloqueia a tocha apenas nas costas do personagem (dot < 0)
                float dotP = Vector2.Dot(dirP, fwd);
                float tDot = Mathf.Clamp01((dotP - (-0.35f)) / (0.15f - (-0.35f)));
                float dotSmooth = tDot * tDot * (3f - 2f * tDot);

                float backBlock = (1f - dotSmooth) * (1f - footSafeZone);
                mask *= Mathf.Clamp01(1f - backBlock);
            }
        }

        return mask;
    }

    public static float EvaluateTorchLightMultiplier(Vector3 worldPos)
    {
        if (Application.isPlaying && lastPushFrame != Time.frameCount) PushToShaderGlobals();
        if (activeTorchCount <= 0) return 1f;
        float maxCarve = 0f;
        for (int i = 0; i < activeTorchCount; i++)
            maxCarve = Mathf.Max(maxCarve, ComputeTorchMask(i, Vector3.Distance(worldPos, new Vector3(TorchSpheres[i].x, TorchSpheres[i].y, TorchSpheres[i].z)), worldPos));
        return 1f - Mathf.Clamp01(maxCarve);
    }

    public static float EvaluateTorchBeamMultiplier(
        Vector3 worldPos, Vector3 beamOrigin, Vector3 beamDir, float topRadius, float bottomRadius, float fullConeLen, bool isHorizontalPrismBeam = true, bool ignoreHeldTorches = false)
    {
        if (Application.isPlaying && lastPushFrame != Time.frameCount) PushToShaderGlobals();
        if (activeTorchCount <= 0) return 1f;
        Vector3 dir = beamDir.sqrMagnitude > 0.0001f ? beamDir.normalized : Vector3.forward;
        Vector3 toP = worldPos - beamOrigin; float tP = Vector3.Dot(toP, dir); Vector3 perpP = toP - dir * tP;
        float safeFullLen = Mathf.Max(fullConeLen, 0.05f), coneRP = Mathf.Max(0.05f, Mathf.Lerp(topRadius, bottomRadius, Mathf.Clamp01(tP / safeFullLen)));
        float maxCarve = 0f;
        for (int i = 0; i < activeTorchCount; i++)
        {
            if (ignoreHeldTorches && TorchParams[i].z > 0.5f) continue;
            Vector3 center = new Vector3(TorchSpheres[i].x, TorchSpheres[i].y, TorchSpheres[i].z); float radius = Mathf.Max(TorchSpheres[i].w, 0.05f);
            Vector3 toT = center - beamOrigin; float tT = Vector3.Dot(toT, dir);
            Vector3 delta;
            if (tT > -radius * 0.85f && tP > tT)
            {
                float coneRT = Mathf.Max(0.05f, Mathf.Lerp(topRadius, bottomRadius, Mathf.Clamp01(Mathf.Max(0f, tT) / safeFullLen)));
                delta = (perpP / Mathf.Max(1f, coneRP / Mathf.Max(coneRT, 0.1f))) - (toT - dir * tT);
            }
            else delta = worldPos - center;
            if (isHorizontalPrismBeam) delta.y *= 0.55f;
            maxCarve = Mathf.Max(maxCarve, ComputeTorchMask(i, delta.magnitude, worldPos));
        }
        return 1f - Mathf.Clamp01(maxCarve);
    }

    public static bool EvaluateCarveAtPoint(
        Vector3 worldPos, float marginRadius, Color requiredColor, bool enforceExactColor, float colorTolerance,
        float holeScale = 1f, float holeOffset = 0f, bool ignorePrismBeams = false, bool ignoreCeilingBeams = false)
    {
        if (Application.isPlaying && lastPushFrame != Time.frameCount) PushToShaderGlobals();
        if (activeBeamCount <= 0) return false;
        float maxWeight = 0f; Vector3 accumColor = Vector3.zero;
        for (int i = 0; i < activeBeamCount; i++)
        {
            if (ignorePrismBeams && BeamParams[i].y > 0.5f) continue;
            if (ignoreCeilingBeams && BeamParams[i].y < 0.5f) continue;
            float strength = BeamParams[i].z; if (strength <= 0.05f) continue;
            Vector3 origin = new Vector3(BeamStarts[i].x, BeamStarts[i].y, BeamStarts[i].z), dir = new Vector3(BeamDirs[i].x, BeamDirs[i].y, BeamDirs[i].z);
            float hitLen = Mathf.Max(BeamDirs[i].w, 0.001f); Vector3 toP = worldPos - origin; float tProj = Vector3.Dot(toP, dir);
            float endPad = BeamParams[i].y > 0.5f ? 0.20f : 3.0f;
            if (tProj < -marginRadius || tProj > hitLen + endPad) continue;
            float baseConeR = Mathf.Lerp(BeamStarts[i].w, BeamColors[i].w, Mathf.Clamp01(tProj / Mathf.Max(BeamParams[i].x, 0.01f)));
            float coneRadius = Mathf.Max(0.05f, baseConeR * holeScale + holeOffset) + marginRadius, radialDist = (toP - dir * tProj).magnitude;
            if (radialDist > coneRadius + 0.1f) continue;
            float tSmooth = Mathf.Clamp01((radialDist - coneRadius * 0.58f) / Mathf.Max(0.0001f, (coneRadius + 0.1f) - coneRadius * 0.58f));
            float torchBeamMult = EvaluateTorchBeamMultiplier(worldPos, origin, dir, BeamStarts[i].w, BeamColors[i].w, BeamParams[i].x, BeamParams[i].y > 0.5f, !ignorePrismBeams);
            float w = Mathf.Clamp01((1f - tSmooth * tSmooth * (3f - 2f * tSmooth)) * Mathf.Clamp01(strength)) * torchBeamMult;
            accumColor += new Vector3(BeamColors[i].x, BeamColors[i].y, BeamColors[i].z) * w;
            if (w > maxWeight) maxWeight = w;
        }
        if (maxWeight < 0.25f) return false;
        if (!enforceExactColor) return true;
        float maxAccum = Mathf.Max(accumColor.x, Mathf.Max(accumColor.y, accumColor.z)), maxReq = Mathf.Max(requiredColor.r, Mathf.Max(requiredColor.g, requiredColor.b));
        Vector3 mixed = maxAccum > 0.0001f ? accumColor / maxAccum : Vector3.one, reqNorm = maxReq > 0.0001f ? new Vector3(requiredColor.r, requiredColor.g, requiredColor.b) / maxReq : Vector3.one;
        float dist = Mathf.Min(Vector3.Distance(mixed, reqNorm), Vector3.Distance(mixed, Vector3.one)) / 1.73205f;
        return dist <= Mathf.Max(0.05f, colorTolerance);
    }
}

internal sealed class AlbedoWallLightRunner : MonoBehaviour
{
    private void LateUpdate() => AlbedoWallLightGlobals.PushToShaderGlobals();
}
