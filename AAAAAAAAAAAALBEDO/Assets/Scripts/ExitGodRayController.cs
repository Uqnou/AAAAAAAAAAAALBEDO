using UnityEngine;

/// <summary>
/// Controlador visual do God Ray guia de fim de fase (Exit God Ray).
/// Cria e sincroniza duas camadas de feixes volumétricos animados + partículas de poeira brilhante
/// + luz opcional no solo para indicar claramente ao jogador onde fica a saída da fase.
/// Não registra em AlbedoWallLightGlobals para não interferir nos puzzles de luz/sombra.
/// </summary>
[ExecuteAlways, SelectionBase]
public class ExitGodRayController : MonoBehaviour
{
    [Header("Aparência e Cor")]
    [ColorUsage(true, true)]
    [SerializeField] private Color rayColor = new Color(1.0f, 0.88f, 0.52f, 1.0f);
    [Range(0.1f, 8.0f)] [SerializeField] private float intensity = 2.0f;
    [Range(0.0f, 1.0f)] [SerializeField] private float baseAlpha = 0.34f;

    [Header("Dimensões e Inclinação")]
    [SerializeField] private float topRadius = 0.55f;
    [SerializeField] private float bottomRadius = 2.6f;
    [SerializeField] private float beamHeight = 12.0f;
    [Range(8, 64)] [SerializeField] private int coneSegments = 36;

    [Header("Animação dos Feixes (Luzes Mexendo)")]
    [Range(2f, 28f)] [SerializeField] private float rayCount = 9f;
    [Range(-2f, 2f)] [SerializeField] private float raySwaySpeed = 0.25f;
    [Range(-3f, 3f)] [SerializeField] private float lightFlowSpeed = 0.5f;
    [Range(0f, 5f)] [SerializeField] private float shimmerSpeed = 1.4f;

    [Header("Partículas de Poeira Brilhante")]
    [SerializeField] private bool enableDustMotes = true;
    [Range(0f, 80f)] [SerializeField] private float dustEmissionRate = 18f;
    [Range(0.02f, 0.4f)] [SerializeField] private float dustSize = 0.1f;
    [Range(0.05f, 2.0f)] [SerializeField] private float dustSpeed = 0.35f;

    [Header("Luz Auxiliar no Solo (Opcional)")]
    [SerializeField] private bool enableFloorLight = true;
    [Range(0f, 10f)] [SerializeField] private float floorLightIntensity = 2.5f;
    [Range(1f, 15f)] [SerializeField] private float floorLightRange = 6.0f;

    [Header("Referências Internas")]
    [SerializeField] private MeshFilter outerBeamFilter;
    [SerializeField] private MeshRenderer outerBeamRenderer;
    [SerializeField] private MeshFilter innerBeamFilter;
    [SerializeField] private MeshRenderer innerBeamRenderer;
    [SerializeField] private ParticleSystem dustParticles;
    [SerializeField] private Light floorLight;

    private MaterialPropertyBlock outerProps;
    private MaterialPropertyBlock innerProps;
    private Mesh outerMesh;
    private Mesh innerMesh;

    private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int BaseAlphaId = Shader.PropertyToID("_BaseAlpha");
    private static readonly int RayCountId = Shader.PropertyToID("_RayCount");
    private static readonly int RayScrollSpeedId = Shader.PropertyToID("_RayScrollSpeed");
    private static readonly int FlowSpeedId = Shader.PropertyToID("_FlowSpeed");
    private static readonly int ShimmerSpeedId = Shader.PropertyToID("_ShimmerSpeed");

    private void OnEnable()
    {
        EnsureHierarchy();
        RebuildMeshes();
        ApplyVisuals();
    }

    private void OnValidate()
    {
        topRadius = Mathf.Max(0.05f, topRadius);
        bottomRadius = Mathf.Max(topRadius + 0.05f, bottomRadius);
        beamHeight = Mathf.Max(1.0f, beamHeight);
        coneSegments = Mathf.Clamp(coneSegments, 8, 64);

        EnsureHierarchy();
        RebuildMeshes();
        ApplyVisuals();
    }

    private void OnDestroy()
    {
        if (outerMesh != null) DestroyImmediateOrRuntime(outerMesh);
        if (innerMesh != null) DestroyImmediateOrRuntime(innerMesh);
    }

    public void ConfigureReferences(
        MeshFilter outerFilter, MeshRenderer outerRend,
        MeshFilter innerFilter, MeshRenderer innerRend,
        ParticleSystem dust, Light lightComp)
    {
        outerBeamFilter = outerFilter;
        outerBeamRenderer = outerRend;
        innerBeamFilter = innerFilter;
        innerBeamRenderer = innerRend;
        dustParticles = dust;
        floorLight = lightComp;

        RebuildMeshes();
        ApplyVisuals();
    }

    public void ApplyVisuals()
    {
        outerProps ??= new MaterialPropertyBlock();
        innerProps ??= new MaterialPropertyBlock();

        if (outerBeamRenderer != null)
        {
            outerBeamRenderer.GetPropertyBlock(outerProps);
            outerProps.SetColor(TintColorId, rayColor);
            outerProps.SetFloat(IntensityId, intensity);
            outerProps.SetFloat(BaseAlphaId, baseAlpha);
            outerProps.SetFloat(RayCountId, rayCount);
            outerProps.SetFloat(RayScrollSpeedId, raySwaySpeed);
            outerProps.SetFloat(FlowSpeedId, lightFlowSpeed);
            outerProps.SetFloat(ShimmerSpeedId, shimmerSpeed);
            outerBeamRenderer.SetPropertyBlock(outerProps);
        }

        if (innerBeamRenderer != null)
        {
            innerBeamRenderer.GetPropertyBlock(innerProps);
            innerProps.SetColor(TintColorId, rayColor);
            innerProps.SetFloat(IntensityId, intensity * 1.25f);
            innerProps.SetFloat(BaseAlphaId, baseAlpha * 0.85f);
            innerProps.SetFloat(RayCountId, Mathf.Max(2f, rayCount * 0.7f));
            innerProps.SetFloat(RayScrollSpeedId, -raySwaySpeed * 0.8f);
            innerProps.SetFloat(FlowSpeedId, lightFlowSpeed * 1.2f);
            innerProps.SetFloat(ShimmerSpeedId, shimmerSpeed * 1.15f);
            innerBeamRenderer.SetPropertyBlock(innerProps);
        }

        UpdateDustParticles();
        UpdateFloorLight();
    }

    public void RebuildMeshes()
    {
        if (outerBeamFilter != null)
        {
            outerMesh = BuildConeMesh(outerMesh, "ExitGodRay_OuterMesh", topRadius, bottomRadius, beamHeight, coneSegments);
            outerBeamFilter.sharedMesh = outerMesh;
        }

        if (innerBeamFilter != null)
        {
            innerMesh = BuildConeMesh(innerMesh, "ExitGodRay_InnerMesh", topRadius * 0.65f, bottomRadius * 0.62f, beamHeight, coneSegments);
            innerBeamFilter.sharedMesh = innerMesh;
        }
    }

    private void EnsureHierarchy()
    {
        if (outerBeamFilter == null) outerBeamFilter = GetComponent<MeshFilter>();
        if (outerBeamRenderer == null) outerBeamRenderer = GetComponent<MeshRenderer>();

        if (innerBeamFilter == null)
        {
            Transform inner = transform.Find("InnerShafts");
            if (inner != null)
            {
                innerBeamFilter = inner.GetComponent<MeshFilter>();
                innerBeamRenderer = inner.GetComponent<MeshRenderer>();
            }
        }

        if (dustParticles == null)
        {
            Transform dust = transform.Find("DustMotes");
            if (dust != null) dustParticles = dust.GetComponent<ParticleSystem>();
        }

        if (floorLight == null)
        {
            Transform lightTf = transform.Find("ExitFloorLight");
            if (lightTf != null) floorLight = lightTf.GetComponent<Light>();
        }
    }

    private void UpdateDustParticles()
    {
        if (dustParticles == null) return;

        dustParticles.gameObject.SetActive(enableDustMotes);
        if (!enableDustMotes) return;

        dustParticles.transform.localPosition = new Vector3(0f, -beamHeight * 0.5f, 0f);
        dustParticles.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

        var main = dustParticles.main;
        Color dustCol = new Color(rayColor.r, rayColor.g, rayColor.b, 0.85f);
        main.startColor = dustCol;
        main.startSize = new ParticleSystem.MinMaxCurve(dustSize * 0.45f, dustSize);
        main.startSpeed = new ParticleSystem.MinMaxCurve(dustSpeed * 0.5f, dustSpeed);
        main.startLifetime = new ParticleSystem.MinMaxCurve(3.0f, 5.5f);

        var emission = dustParticles.emission;
        emission.rateOverTime = dustEmissionRate;

        var shape = dustParticles.shape;
        shape.shapeType = ParticleSystemShapeType.ConeVolume;
        shape.radius = bottomRadius * 0.55f;
        shape.angle = Mathf.Clamp(Mathf.Atan2(bottomRadius - topRadius, beamHeight) * Mathf.Rad2Deg, 2f, 45f);
        shape.length = beamHeight * 0.85f;
    }

    private void UpdateFloorLight()
    {
        if (floorLight == null) return;

        floorLight.gameObject.SetActive(enableFloorLight);
        if (!enableFloorLight) return;

        floorLight.transform.localPosition = new Vector3(0f, -beamHeight + 1.2f, 0f);
        floorLight.color = rayColor;
        floorLight.intensity = floorLightIntensity;
        floorLight.range = floorLightRange;
    }

    private static Mesh BuildConeMesh(Mesh existing, string meshName, float topR, float botR, float height, int segments)
    {
        Mesh mesh = existing != null ? existing : new Mesh();
        mesh.name = meshName;
        mesh.Clear();

        int vertCount = (segments + 1) * 2;
        Vector3[] vertices = new Vector3[vertCount];
        Vector3[] normals = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];
        int[] triangles = new int[segments * 6];

        float slope = (botR - topR) / Mathf.Max(0.01f, height);

        for (int i = 0; i <= segments; i++)
        {
            float u = (float)i / segments;
            float angle = u * Mathf.PI * 2f;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            Vector3 normal = new Vector3(cos, slope, sin).normalized;

            int topIdx = i * 2;
            int botIdx = topIdx + 1;

            vertices[topIdx] = new Vector3(cos * topR, 0f, sin * topR);
            vertices[botIdx] = new Vector3(cos * botR, -height, sin * botR);

            normals[topIdx] = normal;
            normals[botIdx] = normal;

            uvs[topIdx] = new Vector2(u, 1f);
            uvs[botIdx] = new Vector2(u, 0f);

            if (i < segments)
            {
                int tri = i * 6;
                triangles[tri] = topIdx;
                triangles[tri + 1] = botIdx;
                triangles[tri + 2] = topIdx + 2;

                triangles[tri + 3] = topIdx + 2;
                triangles[tri + 4] = botIdx;
                triangles[tri + 5] = botIdx + 2;
            }
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void DestroyImmediateOrRuntime(Object obj)
    {
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }
}
