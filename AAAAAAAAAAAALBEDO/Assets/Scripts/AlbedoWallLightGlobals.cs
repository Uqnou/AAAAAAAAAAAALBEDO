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

    private static readonly HashSet<PrismaController> ActivePrisms = new HashSet<PrismaController>();
    private static readonly HashSet<GodRayBeam> ActiveGodRays = new HashSet<GodRayBeam>();

    private static readonly Vector4[] BeamStarts = new Vector4[MaxBeams];
    private static readonly Vector4[] BeamDirs = new Vector4[MaxBeams];
    private static readonly Vector4[] BeamColors = new Vector4[MaxBeams];
    private static readonly Vector4[] BeamParams = new Vector4[MaxBeams];

    private static readonly int BeamCountId = Shader.PropertyToID("_AlbedoBeamCount");
    private static readonly int BeamStartsId = Shader.PropertyToID("_AlbedoBeamStarts");
    private static readonly int BeamDirsId = Shader.PropertyToID("_AlbedoBeamDirs");
    private static readonly int BeamColorsId = Shader.PropertyToID("_AlbedoBeamColors");
    private static readonly int BeamParamsId = Shader.PropertyToID("_AlbedoBeamParams");

    private static bool arraysInitialized = false;

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

    public static bool IsPassThroughCollider(Collider col, Transform self, Transform ignoreTarget = null) =>
        col == null || col.transform.IsChildOf(self) || (ignoreTarget != null && col.transform.IsChildOf(ignoreTarget)) ||
        col.GetComponentInParent<SombraController>() != null || col.gameObject.name.StartsWith("Sombra") ||
        col.GetComponentInParent<PrismaController>() != null || col.GetComponentInParent<PlayerController>() != null;

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
            Vector3 toP = p.GetBeamWorldOrigin() - origin; float tProj = Vector3.Dot(toP, dir);
            if (tProj <= 0.05f || tProj > hitDist + 0.5f) continue;
            float coneR = Mathf.Lerp(topR, botR, Mathf.Clamp01(tProj / Mathf.Max(0.01f, maxDist)));
            if ((toP - dir * tProj).magnitude <= coneR + 0.45f && !IsBlockedByWall(origin, p.GetBeamWorldOrigin(), source.transform, p.transform, obstacleLayers))
            {
                newlyHitPrisms.Add(p);
                p.ReceivePrismBeam(true, source);
            }
        }
    }
    public static void Register(GodRayBeam g) { if (g != null) ActiveGodRays.Add(g); }
    public static void Unregister(GodRayBeam g) { if (g != null) ActiveGodRays.Remove(g); }

    public static void PushToShaderGlobals()
    {
        if (!arraysInitialized)
        {
            Shader.SetGlobalVectorArray(BeamStartsId, BeamStarts);
            Shader.SetGlobalVectorArray(BeamDirsId, BeamDirs);
            Shader.SetGlobalVectorArray(BeamColorsId, BeamColors);
            Shader.SetGlobalVectorArray(BeamParamsId, BeamParams);
            arraysInitialized = true;
        }

        int count = 0;

        foreach (PrismaController p in ActivePrisms)
        {
            if (count >= MaxBeams) break;
            if (p == null || !p.isActiveAndEnabled || !p.IsEmitting) continue;

            Vector3 origin = p.GetBeamWorldOrigin();
            Vector3 dir = p.GetBeamWorldDirection();
            Color c = p.ActiveEmittedColor;

            BeamStarts[count] = new Vector4(origin.x, origin.y, origin.z, p.GodRayTopRadius);
            BeamDirs[count] = new Vector4(dir.x, dir.y, dir.z, p.CurrentHitDistance);
            BeamColors[count] = new Vector4(c.r, c.g, c.b, p.GodRayBottomRadius);
            BeamParams[count] = new Vector4(p.EffectiveMaxLength, 1f, 1.35f, 0f);
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

            Vector3 origin = g.transform.position;
            Vector3 dir = g.BeamDirection.normalized;
            Color c = g.CurrentColor;

            BeamStarts[count] = new Vector4(origin.x, origin.y, origin.z, g.TopRadius);
            BeamDirs[count] = new Vector4(dir.x, dir.y, dir.z, g.CurrentHeight);
            BeamColors[count] = new Vector4(c.r, c.g, c.b, g.BottomRadius);
            BeamParams[count] = new Vector4(Mathf.Max(g.CurrentHeight, 0.5f), 0f, Mathf.Clamp(g.Intensity * 0.25f, 0.45f, 1.25f), 0f);
            count++;
        }

        Shader.SetGlobalInt(BeamCountId, count);
        if (count > 0)
        {
            Shader.SetGlobalVectorArray(BeamStartsId, BeamStarts);
            Shader.SetGlobalVectorArray(BeamDirsId, BeamDirs);
            Shader.SetGlobalVectorArray(BeamColorsId, BeamColors);
            Shader.SetGlobalVectorArray(BeamParamsId, BeamParams);
        }
    }
}

internal sealed class AlbedoWallLightRunner : MonoBehaviour
{
    private void LateUpdate() => AlbedoWallLightGlobals.PushToShaderGlobals();
}
