using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Modo físico e visual de uma Sombra no cenário.
/// - Barrier: bloqueia o caminho quando escura; fica invisível e atravessável quando atingida pela cor de luz exigida.
/// - WalkableBridge: passarela caminhável quando na escuridão; quebrada/intransponível se atingida por qualquer LightSource ativa ou feixe.
/// </summary>
public enum ShadowMode
{
    Barrier = 0,
    WalkableBridge = 1
}

/// <summary>
/// Controla o comportamento de uma sombra no cenário (Barreira por Cor ou Passarela Caminhável).
/// </summary>
[SelectionBase]
[DisallowMultipleComponent]
public class SombraController : MonoBehaviour
{
    [Header("Modo e Cor Exigida")]
    [Tooltip("Barrier = barreira quebrada pela luz da cor certa. WalkableBridge = passarela que só fica sólida no escuro.")]
    [SerializeField] private ShadowMode mode = ShadowMode.Barrier;

    [Tooltip("Preset de cor exigida para abrir esta sombra (usado no modo Barrier).")]
    [SerializeField] private AlbedoColorPreset requiredColorPreset = AlbedoColorPreset.White;

    [Tooltip("Cor customizada exigida caso Preset seja Custom.")]
    [SerializeField] private Color requiredColor = Color.white;

    [Tooltip("Se ativo, exige exatamente a cor configurada (luz branca não abre sombra amarela).")]
    [SerializeField] private bool requireExactColor = true;

    [Tooltip("Tolerância de comparação por canal RGB.")]
    [SerializeField] private float colorTolerance = AlbedoColorUtility.DefaultColorTolerance;

    [Header("Passarela (WalkableBridge)")]
    [Tooltip("Fontes de luz específicas vinculadas a esta passarela (opcional, além da detecção automática por bounds).")]
    [SerializeField] private List<LightSource> linkedLights = new List<LightSource>();

    [Tooltip("Margem vertical usada para detectar LightSources sobre a passarela automaticamente.")]
    [SerializeField] private float lightOverlapHeight = 12f;

    [Header("Indicador Visual de Cor")]
    [Tooltip("Sinaliza visualmente na malha da sombra a cor necessária para abri-la.")]
    [SerializeField] private bool syncVisualColorIndicator = true;

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

    public ShadowMode Mode
    {
        get => mode;
        set => mode = value;
    }

    public AlbedoColorPreset RequiredColorPreset
    {
        get => requiredColorPreset;
        set
        {
            requiredColorPreset = value;
            SyncPresetColor();
        }
    }

    public Color EffectiveRequiredColor => AlbedoColorUtility.PresetToColor(requiredColorPreset, requiredColor);

    /// <summary>
    /// No modo Barrier: true = sombra aberta/oculta (atravessável).
    /// No modo WalkableBridge: true = passarela destruída pela luz (não caminhável).
    /// </summary>
    public bool IsHidden => isHidden;

    /// <summary>
    /// Para WalkableBridge: verdadeiro quando está sólida e segura para o jogador pisar.
    /// </summary>
    public bool IsBridgeWalkable => mode == ShadowMode.WalkableBridge && !isHidden;

    private void Awake()
    {
        SyncPresetColor();
        CacheComponents();
    }

    private void Start()
    {
        ReevaluateState(true);
    }

    private void OnValidate()
    {
        SyncPresetColor();
        CacheComponents();
    }

    private void SyncPresetColor()
    {
        if (requiredColorPreset != AlbedoColorPreset.Custom)
        {
            requiredColor = AlbedoColorUtility.PresetToColor(requiredColorPreset, requiredColor);
        }
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

        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        r.GetPropertyBlock(mpb);
        Color displayColor;
        if (mode == ShadowMode.WalkableBridge)
        {
            displayColor = new Color(0.08f, 0.08f, 0.08f, 1f);
        }
        else if (requiredColorPreset == AlbedoColorPreset.White)
        {
            // Barreira branca tem aparência escura de sombra com leve contorno neutro
            displayColor = new Color(0.12f, 0.12f, 0.12f, 1f);
        }
        else
        {
            displayColor = EffectiveRequiredColor;
        }

        mpb.SetColor("_BaseColor", displayColor);
        mpb.SetColor("_Color", displayColor);
        r.SetPropertyBlock(mpb);
    }

    public void RegisterLinkedLight(LightSource lightSource)
    {
        if (lightSource != null && !linkedLights.Contains(lightSource))
        {
            linkedLights.Add(lightSource);
        }
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

            if (col is MeshCollider mc && !mc.convex)
            {
                mc.convex = true;
            }
        }
    }

    private void Update()
    {
        if (mode == ShadowMode.WalkableBridge)
        {
            ReevaluateState(false);
        }
    }

    /// <summary>
    /// Notifica que um prisma começou ou parou de iluminar esta sombra.
    /// </summary>
    public void SetIlluminated(bool illuminated, PrismaController source)
    {
        if (source == null) return;

        if (illuminated)
        {
            activeIlluminatingPrisms.Add(source);
        }
        else
        {
            activeIlluminatingPrisms.Remove(source);
        }

        ReevaluateState(false);
    }

    public void ReevaluateState(bool forceApply)
    {
        activeIlluminatingPrisms.RemoveWhere(p => p == null || !p.gameObject.activeInHierarchy || !p.IsEmitting);

        bool shouldHide;
        if (mode == ShadowMode.Barrier)
        {
            shouldHide = ShouldBarrierOpen();
        }
        else
        {
            shouldHide = IsBridgeExposedToActiveLight();
        }

        if (forceApply || shouldHide != isHidden)
        {
            ApplyState(shouldHide);
        }
    }

    private bool ShouldBarrierOpen()
    {
        if (activeIlluminatingPrisms.Count == 0)
            return false;

        prismColorBuffer.Clear();
        foreach (var prism in activeIlluminatingPrisms)
        {
            if (prism != null && prism.IsEmitting)
            {
                prismColorBuffer.Add(prism.ActiveEmittedColor);
            }
        }

        if (prismColorBuffer.Count == 0)
            return false;

        Color combined = AlbedoColorUtility.CombineAdditive(prismColorBuffer);
        return AlbedoColorUtility.ColorsMatch(combined, EffectiveRequiredColor, requireExactColor, colorTolerance);
    }

    private bool IsBridgeExposedToActiveLight()
    {
        // Qualquer feixe de prisma ativo quebra a passarela de sombra
        if (activeIlluminatingPrisms.Count > 0)
            return true;

        // Luzes explicitamente vinculadas no Inspector
        for (int i = 0; i < linkedLights.Count; i++)
        {
            LightSource ls = linkedLights[i];
            if (ls != null && ls.IsActive)
            {
                return true;
            }
        }

        // Detecção automática via sobreposição com colliders de LightSource
        Bounds bridgeBounds = ComputeWorldBounds();
        Vector3 center = bridgeBounds.center + Vector3.up * (lightOverlapHeight * 0.35f);
        Vector3 halfExtents = new Vector3(
            Mathf.Max(0.4f, bridgeBounds.extents.x),
            Mathf.Max(lightOverlapHeight * 0.5f, bridgeBounds.extents.y),
            Mathf.Max(0.4f, bridgeBounds.extents.z));

        Collider[] hits = Physics.OverlapBox(center, halfExtents, Quaternion.identity, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null || hit.transform.IsChildOf(transform)) continue;

            LightSource ls = hit.GetComponent<LightSource>() ?? hit.GetComponentInParent<LightSource>();
            if (ls != null && ls.IsActive)
            {
                return true;
            }
        }

        return false;
    }

    private Bounds ComputeWorldBounds()
    {
        if (colliders != null && colliders.Length > 0)
        {
            Bounds b = default;
            bool initialized = false;
            foreach (var c in colliders)
            {
                if (c == null) continue;
                if (!initialized)
                {
                    b = c.bounds;
                    initialized = true;
                }
                else
                {
                    b.Encapsulate(c.bounds);
                }
            }
            if (initialized) return b;
        }

        return new Bounds(transform.position, Vector3.one);
    }

    private void ApplyState(bool hide)
    {
        isHidden = hide;

        if (renderers == null) renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r != null) r.enabled = !hide;
        }

        if (colliders == null) colliders = GetComponentsInChildren<Collider>(true);
        foreach (var c in colliders)
        {
            if (c == null) continue;

            if (mode == ShadowMode.Barrier)
            {
                if (hide)
                {
                    c.isTrigger = true;
                }
                else
                {
                    c.isTrigger = originalTriggerStates.TryGetValue(c, out bool origTrig) && origTrig;
                }
            }
            else
            {
                // WalkableBridge: quando quebrada pela luz (hide = true), isTrigger = true impede pisar;
                // quando escura/suprimida (hide = false), volta a ser chão sólido.
                if (hide)
                {
                    c.isTrigger = true;
                }
                else
                {
                    c.isTrigger = originalTriggerStates.TryGetValue(c, out bool origTrig) && origTrig;
                    c.enabled = !originalEnabledStates.TryGetValue(c, out bool origEn) || origEn;
                }
            }
        }

        if (hide)
        {
            onShadowHidden?.Invoke();
        }
        else
        {
            onShadowRevealed?.Invoke();
        }
    }

    private void OnDisable()
    {
        activeIlluminatingPrisms.Clear();
        ApplyState(false);
    }
}
