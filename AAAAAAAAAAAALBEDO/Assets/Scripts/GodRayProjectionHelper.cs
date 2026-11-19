using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Resultado da amostragem de raios dentro do cone de um GodRayBeam.
/// </summary>
public struct GodRayConeHitResult
{
    public bool hasHit;
    public float minHitDistance;
    public float maxHitDistance;
    public float centerHitDistance;
}

/// <summary>
/// Calcula intersecoes de todo o cone de luz e posiciona o volume 3D do GroundRing projetado.
/// </summary>
public static class GodRayProjectionHelper
{
    private static readonly int BeamOriginId = Shader.PropertyToID("_BeamOrigin");
    private static readonly int BeamDirId = Shader.PropertyToID("_BeamDir");
    private static readonly int BeamDimsId = Shader.PropertyToID("_BeamDims");
    private static Mesh cachedCubeMesh;

    public static GodRayConeHitResult EvaluateConeHits(
        Transform beamTransform, float topRadius, float bottomRadius, float maxHeight, float raycastOffset, LayerMask layerMask)
    {
        var result = new GodRayConeHitResult { hasHit = false, minHitDistance = maxHeight, maxHitDistance = maxHeight, centerHitDistance = maxHeight };
        if (beamTransform == null) return result;

        Vector3 origin = beamTransform.position;
        Vector3 dir = -beamTransform.up;
        float minH = float.MaxValue, maxH = 0f;

        if (TryCastFirstValidHit(beamTransform, origin, dir, maxHeight, raycastOffset, layerMask, out RaycastHit centerHit))
        {
            float along = Vector3.Dot(centerHit.point - origin, dir);
            if (along > 0.05f)
            {
                result.hasHit = true;
                result.centerHitDistance = along;
                minH = along;
                maxH = along;
            }
        }

        float refDist = result.hasHit ? result.centerHitDistance : Mathf.Min(maxHeight, 10f);
        Vector3 right = beamTransform.right, forward = beamTransform.forward;

        SampleConeRing(beamTransform, origin, dir, right, forward, topRadius, bottomRadius, refDist, maxHeight, raycastOffset, layerMask, 0.5f, 4, ref result.hasHit, ref minH, ref maxH);
        SampleConeRing(beamTransform, origin, dir, right, forward, topRadius, bottomRadius, refDist, maxHeight, raycastOffset, layerMask, 0.95f, 8, ref result.hasHit, ref minH, ref maxH);

        if (result.hasHit)
        {
            result.minHitDistance = Mathf.Clamp(minH, 0.05f, maxHeight);
            result.maxHitDistance = Mathf.Clamp(maxH, result.minHitDistance, maxHeight);
        }
        return result;
    }

    private static void SampleConeRing(
        Transform beamTransform, Vector3 origin, Vector3 dir, Vector3 right, Vector3 forward,
        float topR, float botR, float refDist, float maxHeight, float offset, LayerMask mask,
        float frac, int rayCount, ref bool hasHit, ref float minH, ref float maxH)
    {
        float step = (Mathf.PI * 2f) / rayCount;
        for (int i = 0; i < rayCount; i++)
        {
            float a = i * step;
            Vector3 radial = right * Mathf.Cos(a) + forward * Mathf.Sin(a);
            Vector3 rOrigin = origin + radial * (topR * frac);
            Vector3 refTarget = origin + dir * refDist + radial * (botR * frac);
            Vector3 rDir = (refTarget - rOrigin).normalized;
            float maxRayLen = maxHeight / Mathf.Max(0.25f, Vector3.Dot(rDir, dir));

            if (TryCastFirstValidHit(beamTransform, rOrigin, rDir, maxRayLen, offset, mask, out RaycastHit hit))
            {
                float along = Vector3.Dot(hit.point - origin, dir);
                if (along > 0.05f && along <= maxHeight + 0.05f)
                {
                    hasHit = true;
                    if (along < minH) minH = along;
                    if (along > maxH) maxH = along;
                }
            }
        }
    }

    private static bool TryCastFirstValidHit(
        Transform beamTransform, Vector3 origin, Vector3 dir, float distance, float offset, LayerMask mask, out RaycastHit validHit)
    {
        validHit = default;
        RaycastHit[] hits = Physics.RaycastAll(origin + dir * offset, dir, distance, mask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return false;
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var h in hits)
        {
            if (h.collider == null || h.collider.isTrigger || h.collider.transform.IsChildOf(beamTransform)) continue;
            if (beamTransform.parent != null && (h.collider.transform.IsChildOf(beamTransform.root) || h.collider.transform == beamTransform.parent)) continue;
            if (h.collider.GetComponentInParent<PlayerController>() != null || h.collider.GetComponentInParent<PrismaController>() != null) continue;
            validHit = h;
            return true;
        }
        return false;
    }

    public static void EnsureCubeVolumeMesh(Transform ringTransform, Renderer ringRenderer)
    {
        if (ringTransform == null) return;
        MeshFilter mf = ringTransform.GetComponent<MeshFilter>();
        if (mf != null)
        {
            Mesh cube = GetCubeMesh();
            if (cube != null && (mf.sharedMesh == null || mf.sharedMesh.name == "Quad")) mf.sharedMesh = cube;
        }
        if (ringRenderer != null)
        {
            ringRenderer.shadowCastingMode = ShadowCastingMode.Off;
            ringRenderer.receiveShadows = false;
        }
    }

    public static float ConfigureProjectedGroundRing(
        Transform ringTransform, Renderer ringRenderer, GodRayConeHitResult coneHits, float topRadius, float bottomRadius)
    {
        EnsureCubeVolumeMesh(ringTransform, ringRenderer);
        if (ringTransform == null) return 0f;

        if (!coneHits.hasHit)
        {
            if (ringRenderer != null) ringRenderer.enabled = false;
            return 0f;
        }

        if (ringRenderer != null) ringRenderer.enabled = true;

        float boxStartH = Mathf.Max(0.15f, coneHits.minHitDistance - Mathf.Max(1.6f, bottomRadius));
        float boxEndH = coneHits.maxHitDistance + 0.55f;
        float boxHeight = Mathf.Max(0.6f, boxEndH - boxStartH);
        float midH = (boxStartH + boxEndH) * 0.5f;
        float boxWidth = Mathf.Max(topRadius, bottomRadius) * 2.15f;

        ringTransform.localPosition = new Vector3(0f, -midH, 0f);
        ringTransform.localRotation = Quaternion.identity;
        ringTransform.localScale = new Vector3(boxWidth, boxHeight, boxWidth);
        return boxStartH;
    }

    public static void ApplyProjectionProperties(
        MaterialPropertyBlock pb, Transform beamTransform, float topRadius, float bottomRadius, float currentHeight, float minProjectedH)
    {
        if (pb == null || beamTransform == null) return;
        Vector3 origin = beamTransform.position;
        Vector3 dir = -beamTransform.up;
        pb.SetVector(BeamOriginId, new Vector4(origin.x, origin.y, origin.z, 1f));
        pb.SetVector(BeamDirId, new Vector4(dir.x, dir.y, dir.z, 0f));
        pb.SetVector(BeamDimsId, new Vector4(topRadius, bottomRadius, currentHeight, minProjectedH));
    }

    public static Mesh GetCubeMesh()
    {
        if (cachedCubeMesh != null) return cachedCubeMesh;
        cachedCubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        if (cachedCubeMesh == null)
        {
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cachedCubeMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) Object.Destroy(temp);
            else Object.DestroyImmediate(temp);
        }
        return cachedCubeMesh;
    }
}
