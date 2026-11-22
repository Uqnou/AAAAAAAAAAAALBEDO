using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Renderiza silhueta estilo Minecraft (contorno colorido através de paredes/obstáculos)
/// quando o jogador se aproxima de um item coletável (<see cref="PickableObject"/>).
/// </summary>
[DisallowMultipleComponent]
public class ItemProximityGlow : MonoBehaviour
{
    [SerializeField] private float detectRadius = 22.0f;
    [SerializeField] private float fullGlowRadius = 10.0f;
    [SerializeField] private float outlineWidth = 0.065f;
    [SerializeField, Range(0f, 1f)] private float fillOpacity = 0.58f;
    [SerializeField] private float fadeSpeed = 4.0f;
    [SerializeField] private Color overrideGlowColor = Color.clear;

    [Header("Cores da Tocha (Destaque)")]
    [SerializeField] private Color unlitTorchGlowColor = new Color(1.0f, 0.48f, 0.04f, 1f);
    [SerializeField] private Color litTorchGlowColor = new Color(0.74f, 0.18f, 1.0f, 1f);

    private static Material sharedFillMaterial;
    private static Material sharedOutlineMaterial;
    private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
    private static readonly int GlowModeId = Shader.PropertyToID("_GlowMode");
    private static readonly int ProximityAlphaId = Shader.PropertyToID("_ProximityAlpha");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
    private static readonly int FillOpacityId = Shader.PropertyToID("_FillOpacity");
    private static readonly int RimStrengthId = Shader.PropertyToID("_RimStrength");
    private static readonly int MeshCenterId = Shader.PropertyToID("_MeshCenterOS");
    private static readonly int StencilRefId = Shader.PropertyToID("_StencilRef");
    private static readonly int StencilCompId = Shader.PropertyToID("_StencilComp");
    private static readonly int StencilPassId = Shader.PropertyToID("_StencilPass");

    private MeshRenderer fillRenderer;
    private MeshRenderer outlineRenderer;
    private Vector3 meshCenterOS = Vector3.zero;
    private float meshWorldRadius = 0.35f;
    private PickableObject pickable;
    private PlayerController cachedPlayer;
    private MaterialPropertyBlock propBlock;
    private Color resolvedColor = Color.white;
    private float resolvedGlowMode = 0f;
    private float currentAlpha = 0f;

    public float DetectRadius { get => detectRadius; set => detectRadius = Mathf.Max(0.5f, value); }
    public float FullGlowRadius { get => fullGlowRadius; set => fullGlowRadius = Mathf.Max(0.1f, value); }
    public float FillOpacity { get => fillOpacity; set => fillOpacity = Mathf.Clamp01(value); }
    public Color ResolvedColor => resolvedColor;
    public float ResolvedGlowMode => resolvedGlowMode;

    private void Awake()
    {
        pickable = GetComponent<PickableObject>();
        EnsureOverlayReady();
        resolvedColor = ResolveItemColor(out resolvedGlowMode);
        ApplyVisualState(0f);
    }

    /// <summary>
    /// Resolve a cor e o modo de destaque (sólido ou arco-íris estilo DVD para prismas brancos).
    /// </summary>
    public Color ResolveItemColor(out float glowMode)
    {
        glowMode = 0f;
        if (overrideGlowColor.a > 0.05f) return BoostGlowColor(overrideGlowColor);

        PrismaController prisma = GetComponent<PrismaController>()
            ?? GetComponentInParent<PrismaController>()
            ?? GetComponentInChildren<PrismaController>();
        if (prisma != null)
        {
            Color c = prisma.EffectivePrismColor;
            bool isWhite = prisma.ColorPreset == AlbedoColorPreset.White || AlbedoColorUtility.IsNeutralLight(c);
            if (isWhite)
            {
                glowMode = 1f;
                return Color.white;
            }

            Color boosted = BoostGlowColor(c);
            if (boosted.b > 0.85f && boosted.r < 0.2f && boosted.g < 0.2f)
                return new Color(0.10f, 0.42f, 1.0f, 1f);
            if (boosted.r > 0.85f && boosted.g < 0.2f && boosted.b < 0.2f)
                return new Color(1.0f, 0.10f, 0.10f, 1f);
            if (boosted.g > 0.85f && boosted.r < 0.2f && boosted.b < 0.2f)
                return new Color(0.08f, 1.0f, 0.20f, 1f);
            return boosted;
        }

        TorchController torch = GetComponent<TorchController>()
            ?? GetComponentInParent<TorchController>()
            ?? GetComponentInChildren<TorchController>();
        if (torch != null
            || gameObject.name.IndexOf("Tocha", System.StringComparison.OrdinalIgnoreCase) >= 0
            || string.Equals(gameObject.name, "Cylinder.003", System.StringComparison.OrdinalIgnoreCase))
        {
            return (torch != null && torch.IsLit) ? litTorchGlowColor : unlitTorchGlowColor;
        }

        Renderer r = GetComponent<Renderer>() ?? GetComponentInChildren<Renderer>();
        if (r != null && r.sharedMaterials != null)
        {
            for (int i = r.sharedMaterials.Length - 1; i >= 0; i--)
            {
                Material m = r.sharedMaterials[i];
                if (m == null) continue;
                if (m.HasProperty("_EmissionColor"))
                {
                    Color c = m.GetColor("_EmissionColor");
                    if (c.maxColorComponent > 0.25f) return BoostGlowColor(c);
                }
                if (m.HasProperty("_BaseColor"))
                {
                    Color c = m.GetColor("_BaseColor");
                    if (c.maxColorComponent > 0.25f) return BoostGlowColor(c);
                }
            }
        }

        return Color.white;
    }

    private static Color BoostGlowColor(Color c)
    {
        float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        if (max <= 0.05f) return new Color(0.75f, 0.75f, 0.78f, 1f);
        return new Color(Mathf.Clamp01(c.r / max), Mathf.Clamp01(c.g / max), Mathf.Clamp01(c.b / max), 1f);
    }

    private static void EnsureSharedMaterials()
    {
        Shader shader = Shader.Find("Custom/URP_ItemOccludedGlow");
        if (shader == null) return;

        if (sharedFillMaterial == null)
        {
            sharedFillMaterial = new Material(shader) { name = "ItemOccludedGlow_Fill", renderQueue = 3200 };
            sharedFillMaterial.SetFloat(OutlineWidthId, 0f);
            sharedFillMaterial.SetFloat(FillOpacityId, 0.58f);
            sharedFillMaterial.SetFloat(RimStrengthId, 0.75f);
            sharedFillMaterial.SetFloat(StencilRefId, 44f);
            sharedFillMaterial.SetFloat(StencilCompId, (float)CompareFunction.Always);
            sharedFillMaterial.SetFloat(StencilPassId, (float)StencilOp.Replace);
        }

        if (sharedOutlineMaterial == null)
        {
            sharedOutlineMaterial = new Material(shader) { name = "ItemOccludedGlow_Outline", renderQueue = 3205 };
            sharedOutlineMaterial.SetFloat(OutlineWidthId, 0.065f);
            sharedOutlineMaterial.SetFloat(FillOpacityId, 1f);
            sharedOutlineMaterial.SetFloat(RimStrengthId, 0f);
            sharedOutlineMaterial.SetFloat(StencilRefId, 44f);
            sharedOutlineMaterial.SetFloat(StencilCompId, (float)CompareFunction.NotEqual);
            sharedOutlineMaterial.SetFloat(StencilPassId, (float)StencilOp.Keep);
        }
    }

    private void EnsureOverlayReady()
    {
        if (propBlock == null) propBlock = new MaterialPropertyBlock();
        if (fillRenderer == null || outlineRenderer == null)
        {
            MeshFilter mf = GetComponent<MeshFilter>();
            if (mf == null) mf = GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                meshCenterOS = mf.sharedMesh.bounds.center;
                Renderer mrSrc = mf.GetComponent<Renderer>();
                if (mrSrc != null)
                {
                    meshWorldRadius = Mathf.Max(0.08f, mrSrc.bounds.extents.magnitude * 0.62f);
                }
                EnsureSharedMaterials();
                if (sharedFillMaterial != null && sharedOutlineMaterial != null)
                {
                    fillRenderer = CreateOverlayChild(mf.transform, "__OccludedGlowFill", mf.sharedMesh, sharedFillMaterial);
                    outlineRenderer = CreateOverlayChild(mf.transform, "__OccludedGlowOutline", mf.sharedMesh, sharedOutlineMaterial);
                }
            }
        }
    }

    private MeshRenderer CreateOverlayChild(Transform parentT, string childName, Mesh mesh, Material mat)
    {
        Transform existing = parentT.Find(childName);
        if (existing != null) DestroyImmediate(existing.gameObject);
        GameObject go = new GameObject(childName);
        go.hideFlags = HideFlags.DontSave;
        go.transform.SetParent(parentT, false);

        MeshFilter childFilter = go.GetComponent<MeshFilter>();
        if (childFilter == null) childFilter = go.AddComponent<MeshFilter>();
        childFilter.sharedMesh = mesh;

        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        if (mr == null) mr = go.AddComponent<MeshRenderer>();
        int subCount = Mathf.Max(1, mesh.subMeshCount);
        Material[] mats = new Material[subCount];
        for (int i = 0; i < subCount; i++) mats[i] = mat;
        mr.sharedMaterials = mats;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return mr;
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        if (cachedPlayer == null) cachedPlayer = FindFirstObjectByType<PlayerController>();

        float targetAlpha = 0f;
        if (cachedPlayer != null && (pickable == null || !pickable.IsPickedUp))
        {
            float dist = Vector3.Distance(cachedPlayer.transform.position, transform.position);
            float t = Mathf.Clamp01((dist - fullGlowRadius) / Mathf.Max(0.01f, detectRadius - fullGlowRadius));
            targetAlpha = 1f - (t * t * (3f - 2f * t));
        }

        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, Time.deltaTime * fadeSpeed);
        ApplyVisualState(currentAlpha);
    }

    public void ApplyVisualState(float alpha)
    {
        EnsureOverlayReady();
        resolvedColor = ResolveItemColor(out resolvedGlowMode);
        bool active = alpha > 0.005f;
        float targetOutlineWidth = resolvedGlowMode > 0.5f
            ? Mathf.Clamp(outlineWidth * 2.0f, 0.08f, 0.22f)
            : Mathf.Clamp(outlineWidth, 0.045f, 0.12f);
        ApplyRendererState(fillRenderer, active, 0f, alpha);
        ApplyRendererState(outlineRenderer, active, targetOutlineWidth, alpha);
    }

    private void ApplyRendererState(MeshRenderer mr, bool active, float width, float alpha)
    {
        if (mr == null) return;
        mr.enabled = active;
        if (!active) return;
        Renderer srcR = GetComponent<Renderer>() ?? GetComponentInChildren<Renderer>();
        if (srcR != null)
        {
            meshWorldRadius = Mathf.Max(0.08f, srcR.bounds.extents.magnitude * 0.62f);
        }
        propBlock.Clear();
        propBlock.SetColor(GlowColorId, resolvedColor);
        propBlock.SetFloat(GlowModeId, resolvedGlowMode);
        propBlock.SetFloat(ProximityAlphaId, alpha);
        propBlock.SetFloat(OutlineWidthId, width);
        propBlock.SetFloat(FillOpacityId, width > 0.0001f ? 1f : fillOpacity);
        propBlock.SetVector(MeshCenterId, new Vector4(meshCenterOS.x, meshCenterOS.y, meshCenterOS.z, meshWorldRadius));
        mr.SetPropertyBlock(propBlock);
    }
}
