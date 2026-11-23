using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Controla o comportamento de uma sombra no cenário (Barreira por Cor ou Passarela Caminhável).
/// - Barrier: bloqueia o caminho quando escura; fica invisível e atravessável quando atingida pela cor de luz exigida.
/// - WalkableBridge: passarela caminhável quando na escuridão; quebrada se atingida por qualquer LightSource ativa ou feixe.
/// </summary>
[ExecuteAlways, SelectionBase, DisallowMultipleComponent]
public class SombraController : MonoBehaviour
{
    [Header("Modo e Cor Exigida")]
    [SerializeField] private ShadowMode mode = ShadowMode.Barrier;
    [SerializeField] private AlbedoColorPreset requiredColorPreset = AlbedoColorPreset.White;
    [SerializeField] private Color requiredColor = Color.white;
    [Tooltip("Se ativo, exige a cor configurada da sombra (ou Luz Branca, que é a junção de todas as cores).")]
    [SerializeField] private bool requireExactColor = false;
    [SerializeField] private float colorTolerance = AlbedoColorUtility.DefaultColorTolerance;

    [Header("Passarela (WalkableBridge)")]
    [SerializeField] private List<LightSource> linkedLights = new List<LightSource>();
    [SerializeField] private float lightOverlapHeight = 12f;
    [Tooltip("Se desativado (padrão), a luz emitida por Prismas não abre buracos nem quebra esta sombra do chão (WalkableBridge).")]
    [SerializeField] private bool allowPrismToOpenBridge = false;

    [Header("Indicador Visual de Cor")]
    [SerializeField] private bool syncVisualColorIndicator = true;
    [Tooltip("Cor visual da sombra quando ela exige especificamente a luz Branca (requireExactColor = true).")]
    [SerializeField] private Color whiteOnlyVisualColor = new Color(1f, 1f, 1f, 1f);
    [Tooltip("Cor opcional para sombras que abrem com qualquer cor (alpha = 0 usa a cor escura neutra padrão do shader).")]
    [SerializeField] private Color anyColorVisualTint = new Color(0f, 0f, 0f, 0f);

    [Header("Tamanho do Buraco da Luz")]
    [Range(0.1f, 4.0f)] [SerializeField] private float holeSizeMultiplier = 1.0f;
    [Range(-2.0f, 5.0f)] [SerializeField] private float holeRadiusOffset = 0.0f;
    [Range(0.2f, 2.0f)] [SerializeField] private float holeSoftnessMultiplier = 1.0f;

    private static readonly int TintColorId = Shader.PropertyToID("_TintColor"), RequiredColorId = Shader.PropertyToID("_RequiredColor");
    private static readonly int ColorToleranceId = Shader.PropertyToID("_ColorTolerance"), InstanceCarveParamsId = Shader.PropertyToID("_InstanceCarveParams");
    private static readonly int MatCarveScaleId = Shader.PropertyToID("_CarveRadiusScale"), MatCarveOffsetId = Shader.PropertyToID("_CarveRadiusOffset");

    [Header("Eventos")]
    public UnityEvent onShadowHidden = new UnityEvent();
    public UnityEvent onShadowRevealed = new UnityEvent();

    private readonly HashSet<PrismaController> activeIlluminatingPrisms = new HashSet<PrismaController>();
    private readonly List<Color> prismColorBuffer = new List<Color>();
    private readonly List<Collider> dynamicActors = new List<Collider>();
    private readonly Dictionary<Collider, bool> originalTriggerStates = new Dictionary<Collider, bool>();
    private readonly Dictionary<Collider, bool> originalEnabledStates = new Dictionary<Collider, bool>();
    private Renderer[] renderers;
    private Collider[] colliders;
    private float nextActorRefreshTime = -1f;
    private bool isHidden = false;

    public ShadowMode Mode { get => mode; set => mode = value; }
    public AlbedoColorPreset RequiredColorPreset { get => requiredColorPreset; set { requiredColorPreset = value; SyncPresetColor(); } }
    public bool RequireExactColor { get => requireExactColor; set { requireExactColor = value; ApplyVisualColorIndicator(); } }
    public bool AllowPrismToOpenBridge { get => allowPrismToOpenBridge; set { allowPrismToOpenBridge = value; ApplyVisualColorIndicator(); } }
    public float HoleSizeMultiplier { get => holeSizeMultiplier; set { holeSizeMultiplier = Mathf.Clamp(value, 0.1f, 4f); ApplyVisualColorIndicator(); } }
    public float HoleRadiusOffset { get => holeRadiusOffset; set { holeRadiusOffset = Mathf.Clamp(value, -2f, 5f); ApplyVisualColorIndicator(); } }
    public Color EffectiveRequiredColor => AlbedoColorUtility.PresetToColor(requiredColorPreset, requiredColor);
    public bool IsHidden => isHidden;
    public bool IsBridgeWalkable => mode == ShadowMode.WalkableBridge && !isHidden;

    /// <summary>
    /// Verifica se esta barreira de sombra está aberta e esculpida pela luz no ponto informado.
    /// </summary>
    public bool IsPassableAtPoint(Vector3 worldPoint, float marginRadius = 0.35f)
    {
        if (mode == ShadowMode.WalkableBridge || activeIlluminatingPrisms.Count == 0) return false;
        Color reqColor = EffectiveRequiredColor;
        bool enforceExact = ShouldEnforceExactColor(reqColor), ignorePrism = ShouldIgnorePrismLight();
        GetEffectiveCarveOffsets(out float effScale, out float effOffset);
        if (AlbedoWallLightGlobals.EvaluateCarveAtPoint(worldPoint, marginRadius, reqColor, enforceExact, colorTolerance, effScale, effOffset, ignorePrism)) return true;
        Bounds b = ComputeWorldBounds();
        Vector3 lowProbe = new Vector3(worldPoint.x, Mathf.Lerp(b.min.y, b.max.y, 0.35f), worldPoint.z);
        if (AlbedoWallLightGlobals.EvaluateCarveAtPoint(lowProbe, marginRadius, reqColor, enforceExact, colorTolerance, effScale, effOffset, ignorePrism)) return true;
        return AlbedoWallLightGlobals.EvaluateCarveAtPoint(new Vector3(worldPoint.x, 1.95f, worldPoint.z), marginRadius, reqColor, enforceExact, colorTolerance, effScale, effOffset, ignorePrism);
    }

    private void GetEffectiveCarveOffsets(out float effScale, out float effOffset)
    {
        Material sharedMat = (renderers != null && renderers.Length > 0 && renderers[0] != null) ? renderers[0].sharedMaterial : null;
        effScale = holeSizeMultiplier * (sharedMat != null && sharedMat.HasProperty(MatCarveScaleId) ? sharedMat.GetFloat(MatCarveScaleId) : 1f);
        effOffset = holeRadiusOffset + (sharedMat != null && sharedMat.HasProperty(MatCarveOffsetId) ? sharedMat.GetFloat(MatCarveOffsetId) : 0f);
    }

    private void Awake() { SyncPresetColor(); if (Application.isPlaying) CacheComponents(); }
    private void OnEnable() => SyncPresetColor();
    private void Start() { if (Application.isPlaying && mode == ShadowMode.WalkableBridge) ReevaluateState(true); }
    private void OnValidate() { SyncPresetColor(); if (Application.isPlaying) CacheComponents(); }
    private void SyncPresetColor() { if (requiredColorPreset != AlbedoColorPreset.Custom) requiredColor = AlbedoColorUtility.PresetToColor(requiredColorPreset, requiredColor); ApplyVisualColorIndicator(); }
    public void ConfigureRequiredColor(AlbedoColorPreset preset, bool exactColor = true) { requiredColorPreset = preset; requireExactColor = exactColor; SyncPresetColor(); }
    private bool IsWhiteRequiredColor(Color effective) => requiredColorPreset == AlbedoColorPreset.White || AlbedoColorUtility.ColorsMatch(effective, Color.white, true, 0.05f);
    private bool ShouldEnforceExactColor(Color effective) => mode == ShadowMode.Barrier && (requireExactColor || !IsWhiteRequiredColor(effective));
    private bool ShouldIgnorePrismLight() => mode == ShadowMode.WalkableBridge && !allowPrismToOpenBridge;

    private void ApplyVisualColorIndicator()
    {
        if (!syncVisualColorIndicator) return;
        Renderer[] targetRenderers = GetComponentsInChildren<Renderer>(true);
        if (targetRenderers == null || targetRenderers.Length == 0) return;
        Color effective = EffectiveRequiredColor;
        bool isWhiteColor = IsWhiteRequiredColor(effective), isWhiteOnly = mode == ShadowMode.Barrier && isWhiteColor && requireExactColor;
        bool isColoredShadow = mode == ShadowMode.Barrier && !isWhiteColor, enforceExact = ShouldEnforceExactColor(effective);

        Color shaderTint, fallbackColor;
        if (isColoredShadow) { shaderTint = new Color(effective.r, effective.g, effective.b, 1f); fallbackColor = effective; }
        else if (isWhiteOnly) { Color wt = whiteOnlyVisualColor.a > 0.01f ? whiteOnlyVisualColor : Color.white; shaderTint = new Color(wt.r, wt.g, wt.b, 1f); fallbackColor = new Color(0.88f, 0.90f, 0.95f, 1f); }
        else { shaderTint = anyColorVisualTint; fallbackColor = mode == ShadowMode.WalkableBridge ? new Color(0.08f, 0.08f, 0.08f, 1f) : new Color(0.12f, 0.12f, 0.12f, 1f); }

        Color reqProp = new Color(effective.r, effective.g, effective.b, enforceExact ? 1f : 0f);
        float instanceFlag = ShouldIgnorePrismLight() ? 2f : 1f;
        var mpb = new MaterialPropertyBlock();
        foreach (Renderer r in targetRenderers)
        {
            if (r == null || r.sharedMaterials == null) continue;
            for (int i = 0; i < r.sharedMaterials.Length; i++)
            {
                Material mat = r.sharedMaterials[i];
                if (mat != null && mat.HasProperty(TintColorId))
                {
                    r.GetPropertyBlock(mpb, i); mpb.SetColor(TintColorId, shaderTint); mpb.SetColor(RequiredColorId, reqProp);
                    mpb.SetFloat(ColorToleranceId, colorTolerance); mpb.SetVector(InstanceCarveParamsId, new Vector4(holeSizeMultiplier, holeRadiusOffset, holeSoftnessMultiplier, instanceFlag));
                    r.SetPropertyBlock(mpb, i);
                }
                else AlbedoColorUtility.ApplyMaterialIndicator(r, i, fallbackColor, false);
            }
        }
    }

    public void RegisterLinkedLight(LightSource lightSource) { if (lightSource != null && !linkedLights.Contains(lightSource)) linkedLights.Add(lightSource); }

    private void CacheComponents()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
        originalTriggerStates.Clear(); originalEnabledStates.Clear();
        foreach (var col in colliders)
        {
            if (col == null) continue;
            originalTriggerStates[col] = col.isTrigger; originalEnabledStates[col] = col.enabled;
            if (col is MeshCollider mc && !mc.convex) mc.convex = true;
        }
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        if (mode == ShadowMode.WalkableBridge) ReevaluateState(false);
        UpdatePartialCollisions();
    }

    private void RefreshDynamicActors()
    {
        dynamicActors.Clear();
        PlayerController player = Object.FindFirstObjectByType<PlayerController>();
        if (player != null) foreach (var c in player.GetComponentsInChildren<Collider>(true)) if (c != null) dynamicActors.Add(c);
        foreach (PickableObject item in Object.FindObjectsByType<PickableObject>(FindObjectsSortMode.None))
            if (item != null) foreach (var c in item.GetComponentsInChildren<Collider>(true)) if (c != null) dynamicActors.Add(c);
    }

    private void UpdatePartialCollisions()
    {
        if (colliders == null || colliders.Length == 0) CacheComponents();
        if (colliders == null || colliders.Length == 0) return;
        if (Time.time >= nextActorRefreshTime) { RefreshDynamicActors(); nextActorRefreshTime = Time.time + 0.5f; }
        Color reqColor = EffectiveRequiredColor;
        bool enforceExact = ShouldEnforceExactColor(reqColor), ignorePrism = ShouldIgnorePrismLight();
        GetEffectiveCarveOffsets(out float effScale, out float effOffset);

        for (int s = 0; s < colliders.Length; s++)
        {
            Collider shadowCol = colliders[s];
            if (shadowCol == null || !shadowCol.enabled || shadowCol.isTrigger) continue;
            Bounds sb = shadowCol.bounds;
            for (int a = 0; a < dynamicActors.Count; a++)
            {
                Collider actorCol = dynamicActors[a];
                if (actorCol == null || !actorCol.enabled || !actorCol.gameObject.activeInHierarchy) continue;
                Bounds ab = actorCol.bounds;
                Vector3 center = ab.center, low = new Vector3(center.x, Mathf.Lerp(ab.min.y, center.y, 0.4f), center.z);
                bool carved;
                if (mode == ShadowMode.WalkableBridge)
                {
                    Vector3 footPt = new Vector3(center.x, sb.max.y, center.z);
                    bool xzOver = ab.max.x >= sb.min.x - 0.1f && ab.min.x <= sb.max.x + 0.1f && ab.max.z >= sb.min.z - 0.1f && ab.min.z <= sb.max.z + 0.1f;
                    bool falling = xzOver && ab.min.y < sb.max.y - 0.15f && ab.max.y > sb.min.y - 0.2f;
                    carved = falling || AlbedoWallLightGlobals.EvaluateCarveAtPoint(footPt, 0.05f, reqColor, false, colorTolerance, effScale, effOffset, ignorePrism);
                }
                else
                {
                    Vector3 cPt = shadowCol.ClosestPoint(center), lPt = shadowCol.ClosestPoint(low);
                    carved = AlbedoWallLightGlobals.EvaluateCarveAtPoint(cPt, 0.45f, reqColor, enforceExact, colorTolerance, effScale, effOffset, ignorePrism)
                          || AlbedoWallLightGlobals.EvaluateCarveAtPoint(lPt, 0.45f, reqColor, enforceExact, colorTolerance, effScale, effOffset, ignorePrism)
                          || AlbedoWallLightGlobals.EvaluateCarveAtPoint(center, 0.35f, reqColor, enforceExact, colorTolerance, effScale, effOffset, ignorePrism)
                          || AlbedoWallLightGlobals.EvaluateCarveAtPoint(low, 0.35f, reqColor, enforceExact, colorTolerance, effScale, effOffset, ignorePrism);
                }
                Physics.IgnoreCollision(actorCol, shadowCol, carved);
            }
        }
    }

    public void SetIlluminated(bool illuminated, PrismaController source) { if (source == null) return; if (illuminated) activeIlluminatingPrisms.Add(source); else activeIlluminatingPrisms.Remove(source); ReevaluateState(false); }

    public void ReevaluateState(bool forceApply)
    {
        activeIlluminatingPrisms.RemoveWhere(p => p == null || !p.gameObject.activeInHierarchy || !p.IsEmitting);
        bool shouldHide = mode == ShadowMode.Barrier ? ShouldBarrierOpen() : IsBridgeExposedToActiveLight();
        if (forceApply || shouldHide != isHidden) ApplyState(shouldHide);
    }

    private bool ShouldBarrierOpen()
    {
        if (activeIlluminatingPrisms.Count == 0) return false;
        prismColorBuffer.Clear();
        foreach (var prism in activeIlluminatingPrisms) if (prism != null && prism.IsEmitting) prismColorBuffer.Add(prism.ActiveEmittedColor);
        if (prismColorBuffer.Count == 0) return false;
        Color effective = EffectiveRequiredColor;
        if (!ShouldEnforceExactColor(effective)) return true;
        Color combined = AlbedoColorUtility.CombineAdditive(prismColorBuffer);
        if (AlbedoColorUtility.IsNeutralLight(combined)) return true;
        return AlbedoColorUtility.ColorsMatch(combined, effective, true, colorTolerance);
    }

    private bool IsBridgeExposedToActiveLight()
    {
        if (allowPrismToOpenBridge && activeIlluminatingPrisms.Count > 0) return true;
        for (int i = 0; i < linkedLights.Count; i++) if (linkedLights[i] != null && linkedLights[i].IsActive) return true;
        Bounds b = ComputeWorldBounds();
        Vector3 center = b.center + Vector3.up * (lightOverlapHeight * 0.35f);
        Vector3 halfExtents = new Vector3(Mathf.Max(0.4f, b.extents.x), Mathf.Max(lightOverlapHeight * 0.5f, b.extents.y), Mathf.Max(0.4f, b.extents.z));
        foreach (var hit in Physics.OverlapBox(center, halfExtents, Quaternion.identity, ~0, QueryTriggerInteraction.Collide))
        {
            if (hit == null || hit.transform.IsChildOf(transform) || (!allowPrismToOpenBridge && hit.GetComponentInParent<PrismaController>() != null)) continue;
            LightSource ls = hit.GetComponent<LightSource>() ?? hit.GetComponentInParent<LightSource>();
            if (ls != null && ls.IsActive && (allowPrismToOpenBridge || ls.GetComponentInParent<PrismaController>() == null)) return true;
        }
        return false;
    }

    private Bounds ComputeWorldBounds()
    {
        if (colliders == null || colliders.Length == 0) return new Bounds(transform.position, Vector3.one);
        Bounds b = default; bool init = false;
        foreach (var c in colliders) { if (c == null) continue; if (!init) { b = c.bounds; init = true; } else b.Encapsulate(c.bounds); }
        return init ? b : new Bounds(transform.position, Vector3.one);
    }

    private void ApplyState(bool hide)
    {
        isHidden = hide;
        if (renderers == null) renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers) if (r != null) r.enabled = true;
        if (colliders == null) colliders = GetComponentsInChildren<Collider>(true);
        foreach (var c in colliders) { if (c == null) continue; c.isTrigger = originalTriggerStates.TryGetValue(c, out bool origTrig) && origTrig; c.enabled = !originalEnabledStates.TryGetValue(c, out bool origEn) || origEn; }
        if (hide) onShadowHidden?.Invoke(); else onShadowRevealed?.Invoke();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying) return;
        activeIlluminatingPrisms.Clear();
        if (colliders != null) for (int s = 0; s < colliders.Length; s++) for (int a = 0; a < dynamicActors.Count; a++) if (colliders[s] != null && dynamicActors[a] != null) Physics.IgnoreCollision(dynamicActors[a], colliders[s], false);
        ApplyState(false);
    }
}
