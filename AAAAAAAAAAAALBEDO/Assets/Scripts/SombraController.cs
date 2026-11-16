using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Controla o comportamento de uma sombra no cenário (Barreira por Cor ou Passarela Caminhável).
/// - Barrier: bloqueia o caminho quando escura; fica invisível e atravessável quando atingida pela cor de luz exigida.
/// - WalkableBridge: passarela caminhável quando na escuridão; quebrada se atingida por qualquer LightSource ativa ou feixe.
/// </summary>
[SelectionBase, DisallowMultipleComponent]
public class SombraController : MonoBehaviour
{
    [Header("Modo e Cor Exigida")]
    [SerializeField] private ShadowMode mode = ShadowMode.Barrier;
    [SerializeField] private AlbedoColorPreset requiredColorPreset = AlbedoColorPreset.White;
    [SerializeField] private Color requiredColor = Color.white;
    [Tooltip("Se ativo, exige exatamente a cor configurada (luz branca não abre sombra amarela).")]
    [SerializeField] private bool requireExactColor = false;
    [SerializeField] private float colorTolerance = AlbedoColorUtility.DefaultColorTolerance;

    [Header("Passarela (WalkableBridge)")]
    [SerializeField] private List<LightSource> linkedLights = new List<LightSource>();
    [SerializeField] private float lightOverlapHeight = 12f;

    [Header("Indicador Visual de Cor")]
    [SerializeField] private bool syncVisualColorIndicator = false;

    [Header("Eventos")]
    public UnityEvent onShadowHidden = new UnityEvent();
    public UnityEvent onShadowRevealed = new UnityEvent();

    private readonly HashSet<PrismaController> activeIlluminatingPrisms = new HashSet<PrismaController>();
    private readonly List<Color> prismColorBuffer = new List<Color>();
    private Renderer[] renderers;
    private Collider[] colliders;
    private readonly Dictionary<Collider, bool> originalTriggerStates = new Dictionary<Collider, bool>();
    private readonly Dictionary<Collider, bool> originalEnabledStates = new Dictionary<Collider, bool>();
    private bool isHidden = false;

    public ShadowMode Mode { get => mode; set => mode = value; }
    public AlbedoColorPreset RequiredColorPreset { get => requiredColorPreset; set { requiredColorPreset = value; SyncPresetColor(); } }
    public Color EffectiveRequiredColor => AlbedoColorUtility.PresetToColor(requiredColorPreset, requiredColor);
    public bool IsHidden => isHidden;
    public bool IsBridgeWalkable => mode == ShadowMode.WalkableBridge && !isHidden;

    private void Awake()
    {
        SyncPresetColor();
        CacheComponents();
    }

    private void Start()
    {
        if (mode == ShadowMode.WalkableBridge) ReevaluateState(true);
    }

    private void OnValidate()
    {
        SyncPresetColor();
        CacheComponents();
    }

    private void SyncPresetColor()
    {
        if (requiredColorPreset != AlbedoColorPreset.Custom)
            requiredColor = AlbedoColorUtility.PresetToColor(requiredColorPreset, requiredColor);
        ApplyVisualColorIndicator();
    }

    public void ConfigureRequiredColor(AlbedoColorPreset preset, bool exactColor = true)
    {
        requiredColorPreset = preset;
        requireExactColor = exactColor;
        SyncPresetColor();
    }

    private void ApplyVisualColorIndicator()
    {
        if (!syncVisualColorIndicator) return;
        Renderer r = GetComponent<Renderer>();
        if (r == null) return;
        Color displayColor = mode == ShadowMode.WalkableBridge
            ? new Color(0.08f, 0.08f, 0.08f, 1f)
            : (requiredColorPreset == AlbedoColorPreset.White ? new Color(0.12f, 0.12f, 0.12f, 1f) : EffectiveRequiredColor);
        AlbedoColorUtility.ApplyMaterialIndicator(r, 0, displayColor, false);
    }

    public void RegisterLinkedLight(LightSource lightSource)
    {
        if (lightSource != null && !linkedLights.Contains(lightSource)) linkedLights.Add(lightSource);
    }

    private void CacheComponents()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
        originalTriggerStates.Clear();
        originalEnabledStates.Clear();

        foreach (var col in colliders)
        {
            if (col == null) continue;
            originalTriggerStates[col] = col.isTrigger;
            originalEnabledStates[col] = col.enabled;
            if (col is MeshCollider mc && !mc.convex) mc.convex = true;
        }
    }

    private void Update()
    {
        if (mode == ShadowMode.WalkableBridge) ReevaluateState(false);
    }

    public void SetIlluminated(bool illuminated, PrismaController source)
    {
        if (source == null) return;
        if (illuminated) activeIlluminatingPrisms.Add(source);
        else activeIlluminatingPrisms.Remove(source);
        ReevaluateState(false);
    }

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
        foreach (var prism in activeIlluminatingPrisms)
        {
            if (prism != null && prism.IsEmitting) prismColorBuffer.Add(prism.ActiveEmittedColor);
        }
        if (prismColorBuffer.Count == 0) return false;

        if (!requireExactColor && requiredColorPreset == AlbedoColorPreset.White) return true;

        Color combined = AlbedoColorUtility.CombineAdditive(prismColorBuffer);
        if (requiredColorPreset == AlbedoColorPreset.White && AlbedoColorUtility.IsNeutralLight(combined)) return true;

        bool enforceExact = requireExactColor || requiredColorPreset != AlbedoColorPreset.White;
        return AlbedoColorUtility.ColorsMatch(combined, EffectiveRequiredColor, enforceExact, colorTolerance);
    }

    private bool IsBridgeExposedToActiveLight()
    {
        if (activeIlluminatingPrisms.Count > 0) return true;
        for (int i = 0; i < linkedLights.Count; i++)
        {
            if (linkedLights[i] != null && linkedLights[i].IsActive) return true;
        }

        Bounds b = ComputeWorldBounds();
        Vector3 center = b.center + Vector3.up * (lightOverlapHeight * 0.35f);
        Vector3 halfExtents = new Vector3(
            Mathf.Max(0.4f, b.extents.x),
            Mathf.Max(lightOverlapHeight * 0.5f, b.extents.y),
            Mathf.Max(0.4f, b.extents.z));

        foreach (var hit in Physics.OverlapBox(center, halfExtents, Quaternion.identity, ~0, QueryTriggerInteraction.Collide))
        {
            if (hit == null || hit.transform.IsChildOf(transform)) continue;
            LightSource ls = hit.GetComponent<LightSource>() ?? hit.GetComponentInParent<LightSource>();
            if (ls != null && ls.IsActive) return true;
        }
        return false;
    }

    private Bounds ComputeWorldBounds()
    {
        if (colliders != null && colliders.Length > 0)
        {
            Bounds b = default;
            bool init = false;
            foreach (var c in colliders)
            {
                if (c == null) continue;
                if (!init) { b = c.bounds; init = true; }
                else b.Encapsulate(c.bounds);
            }
            if (init) return b;
        }
        return new Bounds(transform.position, Vector3.one);
    }

    private void ApplyState(bool hide)
    {
        isHidden = hide;
        if (renderers == null) renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers) if (r != null) r.enabled = !hide;

        if (colliders == null) colliders = GetComponentsInChildren<Collider>(true);
        foreach (var c in colliders)
        {
            if (c == null) continue;
            if (hide) c.isTrigger = true;
            else
            {
                c.isTrigger = originalTriggerStates.TryGetValue(c, out bool origTrig) && origTrig;
                if (mode == ShadowMode.WalkableBridge)
                    c.enabled = !originalEnabledStates.TryGetValue(c, out bool origEn) || origEn;
            }
        }

        if (hide) onShadowHidden?.Invoke();
        else onShadowRevealed?.Invoke();
    }

    private void OnDisable()
    {
        activeIlluminatingPrisms.Clear();
        ApplyState(false);
    }
}
