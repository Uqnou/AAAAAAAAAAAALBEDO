using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Renderiza silhueta estilo Minecraft (contorno colorido através de paredes/obstáculos)
/// quando o jogador se aproxima de um item coletável (<see cref="PickableObject"/>).
/// </summary>
[DisallowMultipleComponent]
public class ItemProximityGlow : MonoBehaviour
{
    [SerializeField] private float detectRadius = 11.5f;
    [SerializeField] private float fullGlowRadius = 6.5f;
    [SerializeField] private float outlineWidth = 0.055f;
    [SerializeField] private float fadeSpeed = 4.0f;
    [SerializeField] private Color overrideGlowColor = Color.clear;

    private static Material sharedFillMaterial;
    private static Material sharedOutlineMaterial;
    private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
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
    private PickableObject pickable;
    private PlayerController cachedPlayer;
    private MaterialPropertyBlock propBlock;
    private Color resolvedColor = Color.white;
    private float currentAlpha = 0f;

    private void Awake()
    {
        pickable = GetComponent<PickableObject>();
        EnsureOverlayReady();
        resolvedColor = ResolveItemColor();
        ApplyVisualState(0f);
    }

    private Color ResolveItemColor()
    {
        if (overrideGlowColor.a > 0.05f) return BoostGlowColor(overrideGlowColor);

        PrismaController prisma = GetComponent<PrismaController>();
        if (prisma != null) return BoostGlowColor(prisma.EffectivePrismColor);

        if (GetComponent<TorchController>() != null ||
            gameObject.name.IndexOf("Tocha", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return new Color(0.82f, 0.86f, 0.92f, 1f);
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
            sharedFillMaterial.SetFloat(FillOpacityId, 0.34f);
            sharedFillMaterial.SetFloat(RimStrengthId, 0.75f);
            sharedFillMaterial.SetFloat(StencilRefId, 44f);
            sharedFillMaterial.SetFloat(StencilCompId, (float)CompareFunction.Always);
            sharedFillMaterial.SetFloat(StencilPassId, (float)StencilOp.Replace);
        }

        if (sharedOutlineMaterial == null)
        {
            sharedOutlineMaterial = new Material(shader) { name = "ItemOccludedGlow_Outline", renderQueue = 3205 };
            sharedOutlineMaterial.SetFloat(OutlineWidthId, 0.055f);
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
        resolvedColor = ResolveItemColor();
        ApplyVisualState(currentAlpha);
    }

    public void ApplyVisualState(float alpha)
    {
        EnsureOverlayReady();
        resolvedColor = ResolveItemColor();
        bool active = alpha > 0.005f;
        ApplyRendererState(fillRenderer, active, 0f, alpha);
        ApplyRendererState(outlineRenderer, active, Mathf.Clamp(outlineWidth, 0.045f, 0.12f), alpha);
    }

    private void ApplyRendererState(MeshRenderer mr, bool active, float width, float alpha)
    {
        if (mr == null) return;
        mr.enabled = active;
        if (!active) return;
        propBlock.Clear();
        propBlock.SetColor(GlowColorId, resolvedColor);
        propBlock.SetFloat(ProximityAlphaId, alpha);
        propBlock.SetFloat(OutlineWidthId, width);
        propBlock.SetVector(MeshCenterId, meshCenterOS);
        mr.SetPropertyBlock(propBlock);
    }
}
