using UnityEngine;

/// <summary>
/// Indicador visual unificado (badge [Tecla] + Acao + anel no chao) voltado para a camera isometrica.
/// Mantem escala constante no mundo independente da escala ou inclinacao do objeto pai.
/// </summary>
public class InteractionPrompt : MonoBehaviour
{
    [Header("Configuracao de Exibicao")]
    [SerializeField] private string keyLabel = "E";
    [SerializeField] private string actionLabel = "Pegar";
    [SerializeField] private Color accentColor = new Color(1f, 0.84f, 0.28f, 1f);
    [SerializeField] private float heightOffset = 0.55f;
    [SerializeField] private float badgeWorldScale = 1.0f;
    [SerializeField] private bool showGroundRing = true;
    [SerializeField] private float groundRingRadius = 0.68f;

    private GameObject promptHolder;
    private Transform keyChipTf;
    private Transform bgQuadTf;
    private Transform borderQuadTf;
    private TextMesh keyTextMesh;
    private TextMesh actionTextMesh;
    private Material borderMat;
    private Material bgMat;
    private Material chipMat;

    private GameObject groundRingObj;
    private LineRenderer groundRing;
    private Material ringMat;

    private Vector3? worldAnchorOverride;
    private bool isVisible = false;
    private float animProgress = 0f;
    private Collider cachedCollider;

    private static readonly Color BackdropColor = new Color(0.06f, 0.08f, 0.12f, 0.88f);

    public void Configure(string key, string action, Color accent, bool enableGroundRing = true, float customHeightOffset = -1f)
    {
        if (!string.IsNullOrEmpty(key)) keyLabel = key;
        if (!string.IsNullOrEmpty(action)) actionLabel = action;
        accentColor = EnsureBrightAccent(accent);
        showGroundRing = enableGroundRing;
        if (customHeightOffset >= 0f) heightOffset = customHeightOffset;

        if (promptHolder != null) RefreshLayoutAndColors();
    }

    public void SetWorldAnchorOverride(Vector3? anchor) => worldAnchorOverride = anchor;

    public void ConfigureForPickable(GameObject target)
    {
        if (target == null) return;
        PrismaController prisma = target.GetComponent<PrismaController>() ?? target.GetComponentInChildren<PrismaController>();
        if (prisma != null)
        {
            Color c = prisma.EffectivePrismColor;
            Configure("E", "Pegar Prisma", AlbedoColorUtility.IsNeutralLight(c) ? new Color(1f, 0.88f, 0.42f, 1f) : c);
            return;
        }
        TorchController torch = target.GetComponent<TorchController>() ?? target.GetComponentInChildren<TorchController>();
        if (torch != null || target.name.IndexOf("Tocha", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            Configure("E", "Pegar Tocha", (torch != null && torch.IsLit) ? new Color(0.76f, 0.30f, 1f, 1f) : new Color(1f, 0.52f, 0.12f, 1f));
            return;
        }
        Configure("E", "Pegar", new Color(1f, 0.84f, 0.28f, 1f));
    }

    private static Color EnsureBrightAccent(Color c)
    {
        float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        if (max < 0.15f) return new Color(1f, 0.84f, 0.28f, 1f);
        if (max < 0.75f)
        {
            float boost = 0.85f / max;
            return new Color(Mathf.Clamp01(c.r * boost), Mathf.Clamp01(c.g * boost), Mathf.Clamp01(c.b * boost), 1f);
        }
        return new Color(c.r, c.g, c.b, 1f);
    }

    private void Awake() { cachedCollider = GetComponent<Collider>() ?? GetComponentInChildren<Collider>(); BuildVisuals(); ApplyHiddenImmediate(); }
    private void OnDisable() => ApplyHiddenImmediate();
    private void OnDestroy()
    {
        if (promptHolder != null) Destroy(promptHolder);
        if (groundRingObj != null) Destroy(groundRingObj);
        if (borderMat != null) Destroy(borderMat);
        if (bgMat != null) Destroy(bgMat);
        if (chipMat != null) Destroy(chipMat);
        if (ringMat != null) Destroy(ringMat);
    }

    private Material CreateOverlayMaterial(Color color, int queue)
    {
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
        Material mat = new Material(shader) { color = color, renderQueue = queue };
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        return mat;
    }

    private Transform CreateQuadChild(string objName, Transform parent, Material mat)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = objName;
        Destroy(quad.GetComponent<Collider>());
        quad.transform.SetParent(parent, false);
        MeshRenderer mr = quad.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return quad.transform;
    }

    private TextMesh CreateLabel(string objName, Transform parent, int fontSize, float charSize, TextAnchor anchor, FontStyle style, Color color)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(parent, false);
        TextMesh tm = go.AddComponent<TextMesh>();
        tm.fontSize = fontSize;
        tm.characterSize = charSize;
        tm.anchor = anchor;
        tm.alignment = anchor == TextAnchor.MiddleCenter ? TextAlignment.Center : TextAlignment.Left;
        tm.fontStyle = style;
        tm.color = color;
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return tm;
    }

    private void BuildVisuals()
    {
        if (promptHolder != null) return;

        promptHolder = new GameObject($"InteractionBadge_{name}");
        promptHolder.hideFlags = HideFlags.DontSave;

        borderMat = CreateOverlayMaterial(accentColor, 2995);
        bgMat = CreateOverlayMaterial(BackdropColor, 2996);
        chipMat = CreateOverlayMaterial(accentColor, 2998);

        borderQuadTf = CreateQuadChild("AccentBorder", promptHolder.transform, borderMat);
        bgQuadTf = CreateQuadChild("Backdrop", promptHolder.transform, bgMat);
        keyChipTf = CreateQuadChild("KeyChip", promptHolder.transform, chipMat);

        keyTextMesh = CreateLabel("KeyText", promptHolder.transform, 44, 0.055f, TextAnchor.MiddleCenter, FontStyle.Bold, new Color(0.05f, 0.06f, 0.09f, 1f));
        actionTextMesh = CreateLabel("ActionText", promptHolder.transform, 40, 0.050f, TextAnchor.MiddleLeft, FontStyle.Bold, new Color(0.96f, 0.97f, 1f, 1f));

        BuildGroundRing();
        RefreshLayoutAndColors();
    }

    private void BuildGroundRing()
    {
        groundRingObj = new GameObject($"FocusRing_{name}");
        groundRingObj.hideFlags = HideFlags.DontSave;
        groundRing = groundRingObj.AddComponent<LineRenderer>();
        groundRing.useWorldSpace = false;
        groundRing.loop = true;
        groundRing.positionCount = 32;
        groundRing.startWidth = 0.055f;
        groundRing.endWidth = 0.055f;
        groundRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        groundRing.receiveShadows = false;
        ringMat = CreateOverlayMaterial(new Color(accentColor.r, accentColor.g, accentColor.b, 0.85f), 2994);
        groundRing.sharedMaterial = ringMat;

        for (int i = 0; i < 32; i++)
        {
            float ang = (i / 32f) * Mathf.PI * 2f;
            groundRing.SetPosition(i, new Vector3(Mathf.Cos(ang) * groundRingRadius, 0f, Mathf.Sin(ang) * groundRingRadius));
        }
    }

    private void RefreshLayoutAndColors()
    {
        if (promptHolder == null) return;

        string cleanAction = string.IsNullOrEmpty(actionLabel) ? "Interagir" : actionLabel.Trim();
        float textWidth = Mathf.Clamp(cleanAction.Length * 0.135f, 0.72f, 2.35f);
        float totalWidth = 0.62f + textWidth;
        float totalHeight = 0.46f;

        borderQuadTf.localPosition = new Vector3(0f, 0f, 0.015f);
        borderQuadTf.localScale = new Vector3(totalWidth + 0.07f, totalHeight + 0.07f, 1f);

        bgQuadTf.localPosition = Vector3.zero;
        bgQuadTf.localScale = new Vector3(totalWidth, totalHeight, 1f);

        float chipX = -totalWidth * 0.5f + 0.26f;
        keyChipTf.localPosition = new Vector3(chipX, 0f, -0.012f);
        keyChipTf.localScale = new Vector3(0.36f, 0.34f, 1f);

        keyTextMesh.text = string.IsNullOrEmpty(keyLabel) ? "E" : keyLabel;
        keyTextMesh.transform.localPosition = new Vector3(chipX, 0f, -0.025f);

        actionTextMesh.text = cleanAction;
        actionTextMesh.transform.localPosition = new Vector3(chipX + 0.27f, 0f, -0.025f);

        Color bright = EnsureBrightAccent(accentColor);
        if (borderMat != null) borderMat.color = new Color(bright.r, bright.g, bright.b, 0.92f);
        if (chipMat != null) chipMat.color = bright;
        if (ringMat != null) ringMat.color = new Color(bright.r, bright.g, bright.b, 0.85f);
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || promptHolder == null) return;

        float target = isVisible ? 1f : 0f;
        if (Mathf.Approximately(animProgress, target) && !isVisible)
        {
            if (promptHolder.activeSelf) promptHolder.SetActive(false);
            if (groundRingObj != null && groundRingObj.activeSelf) groundRingObj.SetActive(false);
            return;
        }

        animProgress = Mathf.MoveTowards(animProgress, target, Time.deltaTime * 10f);
        bool active = animProgress > 0.02f;
        if (promptHolder.activeSelf != active) promptHolder.SetActive(active);
        if (groundRingObj != null)
        {
            bool ringActive = active && showGroundRing;
            if (groundRingObj.activeSelf != ringActive) groundRingObj.SetActive(ringActive);
        }
        if (!active) return;

        Vector3 basePos = ResolveWorldBasePosition(out float topY, out float floorY);
        float bob = Mathf.Sin(Time.time * 3.6f) * 0.05f;
        Camera cam = Camera.main;
        Vector3 towardCam = cam != null ? -cam.transform.forward * 0.32f : Vector3.zero;

        promptHolder.transform.position = new Vector3(basePos.x, topY + heightOffset + bob, basePos.z) + towardCam;
        if (cam != null) promptHolder.transform.rotation = cam.transform.rotation;

        float eased = Mathf.SmoothStep(0f, 1f, animProgress);
        float s = Mathf.Lerp(0.65f, 1f, eased) * badgeWorldScale;
        promptHolder.transform.localScale = new Vector3(s, s, s);

        if (groundRingObj != null && showGroundRing)
        {
            float pulse = 1f + Mathf.Sin(Time.time * 4.2f) * 0.06f;
            groundRingObj.transform.position = new Vector3(basePos.x, floorY + 0.04f, basePos.z);
            groundRingObj.transform.localScale = new Vector3(pulse * eased, 1f, pulse * eased);
        }
    }

    private Vector3 ResolveWorldBasePosition(out float topY, out float floorY)
    {
        if (worldAnchorOverride.HasValue)
        {
            Vector3 p = worldAnchorOverride.Value;
            topY = p.y + 0.45f;
            floorY = p.y - 0.15f;
            if (Physics.Raycast(p + Vector3.up * 0.3f, Vector3.down, out RaycastHit hit, 2.5f, ~0, QueryTriggerInteraction.Ignore))
                floorY = hit.point.y;
            return p;
        }

        if (cachedCollider != null && cachedCollider.enabled)
        {
            Bounds b = cachedCollider.bounds;
            topY = b.max.y;
            floorY = b.min.y;
            return b.center;
        }

        Vector3 pos = transform.position;
        topY = pos.y + 0.45f;
        floorY = pos.y;
        return pos;
    }

    public void Show()
    {
        if (promptHolder == null) BuildVisuals();
        isVisible = true;
        animProgress = Mathf.Max(animProgress, 0.25f);
        if (promptHolder != null) promptHolder.SetActive(true);
        if (groundRingObj != null && showGroundRing) groundRingObj.SetActive(true);
    }

    public void Hide() => isVisible = false;

    private void ApplyHiddenImmediate()
    {
        isVisible = false;
        animProgress = 0f;
        if (promptHolder != null) promptHolder.SetActive(false);
        if (groundRingObj != null) groundRingObj.SetActive(false);
    }
}
