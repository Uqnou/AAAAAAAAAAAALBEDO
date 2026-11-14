using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Modo de combinação da cor do feixe emitido pelo prisma.
/// </summary>
public enum PrismColorMode
{
    [Tooltip("Se o prisma tiver cor diferente de branco, ela tinge a luz. Se for branco, usa a cor recebida.")]
    PrismTint,

    [Tooltip("Multiplicação física de cores.")]
    Multiply,

    [Tooltip("Usa sempre a cor do prisma se diferente de branco, caso contrário a cor recebida.")]
    PrismOverrides,

    [Tooltip("Usa diretamente a cor da luz ou prismas que o atingiram.")]
    LightColorDirect
}

/// <summary>
/// Controla o comportamento do Prisma.
/// - Aceita múltiplas LightSources ativas (não suprimidas) e feixes de outros Prismas, somando cores (RGB aditivo).
/// - Emite feixe para frente até atingir obstáculos sólidos ou atravessar/ativar Sombras e outros Prismas.
/// - Permite selecionar cor facilmente via preset ou cor customizada no Inspector.
/// </summary>
[SelectionBase]
[DisallowMultipleComponent]
public class PrismaController : MonoBehaviour
{
    [Header("Cores")]
    [Tooltip("Preset rápido de cor do prisma (se Custom, utiliza o campo Prism Color abaixo).")]
    [SerializeField] private AlbedoColorPreset colorPreset = AlbedoColorPreset.Custom;

    [Tooltip("Cor intrínseca do prisma.")]
    [SerializeField] private Color prismColor = Color.white;

    [Tooltip("Modo de combinação entre a cor do prisma e a luz recebida.")]
    [SerializeField] private PrismColorMode colorMode = PrismColorMode.PrismTint;

    [Header("Feixe de Luz")]
    [Tooltip("Ponto de saída do feixe (se nulo, usa a posição do prisma).")]
    [SerializeField] private Transform emissionPoint;

    [Tooltip("Direção local de emissão.")]
    [SerializeField] private Vector3 emissionDirection = Vector3.forward;

    [Tooltip("Mantém o feixe paralelo ao chão mesmo se o modelo FBX estiver rotacionado.")]
    [SerializeField] private bool keepBeamHorizontal = true;

    [Tooltip("Alcance máximo do feixe de luz.")]
    [SerializeField] private float beamRange = 30f;

    [Tooltip("Raio de espessura do feixe para detecção de sombras e outros prismas.")]
    [SerializeField] private float beamRadius = 0.35f;

    [Tooltip("Espessura visual do feixe de luz.")]
    [SerializeField] private float visualBeamWidth = 0.25f;

    [Tooltip("Camadas de obstáculos sólidos (paredes, chão) que bloqueiam o feixe.")]
    [SerializeField] private LayerMask obstacleLayers = ~0;

    [Header("Efeitos")]
    [Tooltip("Material do feixe (se vazio, usa material aditivo URP unlit).")]
    [SerializeField] private Material beamMaterial;

    [Tooltip("Gera um Spotlight real em tempo de execução para iluminar a cena.")]
    [SerializeField] private bool enableDynamicLight = true;

    [Tooltip("Intensidade do Spotlight dinâmico emitido pelo prisma.")]
    [SerializeField] private float spotlightIntensity = 35f;

    [Header("Indicador Visual de Cor")]
    [Tooltip("Tinge automaticamente o material do prisma conforme o Preset/Cor escolhido.")]
    [SerializeField] private bool syncVisualIndicator = true;

    [Tooltip("Índice do slot de material colorido do prisma (no FBX atual: slot 1 = Vidro colorido).")]
    [SerializeField] private int visualMaterialSlot = 1;

    private readonly HashSet<LightSource> activeLights = new HashSet<LightSource>();
    private readonly HashSet<PrismaController> incomingPrisms = new HashSet<PrismaController>();
    private readonly HashSet<SombraController> currentlyHitSombras = new HashSet<SombraController>();
    private readonly HashSet<PrismaController> currentlyHitPrisms = new HashSet<PrismaController>();
    private readonly List<SombraController> toRemoveSombras = new List<SombraController>();
    private readonly List<PrismaController> toRemovePrisms = new List<PrismaController>();
    private readonly List<Color> incomingColorBuffer = new List<Color>();

    private LineRenderer lineRenderer;
    private Light spotlight;
    private Color activeEmittedColor = Color.white;
    private bool isEmitting = false;
    private bool isEvaluating = false;

    public bool IsEmitting => isEmitting;
    public Color ActiveEmittedColor => activeEmittedColor;

    public AlbedoColorPreset ColorPreset
    {
        get => colorPreset;
        set
        {
            colorPreset = value;
            SyncPresetColor();
        }
    }

    public Color PrismColor
    {
        get => prismColor;
        set
        {
            prismColor = value;
            colorPreset = AlbedoColorUtility.InferPreset(value);
        }
    }

    public Color EffectivePrismColor => AlbedoColorUtility.PresetToColor(colorPreset, prismColor);

    private void Awake()
    {
        SyncPresetColor();
        if (emissionPoint == null)
        {
            emissionPoint = transform;
        }

        SetupBeamVisuals();
    }

    private void OnValidate()
    {
        SyncPresetColor();
    }

    private void SyncPresetColor()
    {
        if (colorPreset != AlbedoColorPreset.Custom)
        {
            prismColor = AlbedoColorUtility.PresetToColor(colorPreset, prismColor);
        }
        ApplyVisualColorIndicator();
    }

    public void SetColorPreset(AlbedoColorPreset preset)
    {
        colorPreset = preset;
        SyncPresetColor();
    }

    private void ApplyVisualColorIndicator()
    {
        if (!syncVisualIndicator) return;
        Renderer r = GetComponent<Renderer>();
        if (r == null) return;

        Material[] shared = r.sharedMaterials;
        if (shared == null || shared.Length == 0) return;
        int slot = Mathf.Clamp(visualMaterialSlot, 0, shared.Length - 1);

        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        r.GetPropertyBlock(mpb, slot);
        Color target = EffectivePrismColor;
        mpb.SetColor("_BaseColor", target);
        mpb.SetColor("_Color", target);
        mpb.SetColor("_EmissionColor", target * 0.65f);
        r.SetPropertyBlock(mpb, slot);
    }

    private void SetupBeamVisuals()
    {
        GameObject beamObj = new GameObject("PrismLightBeam");
        beamObj.transform.SetParent(emissionPoint);
        beamObj.transform.localPosition = Vector3.zero;
        beamObj.transform.localRotation = Quaternion.identity;

        lineRenderer = beamObj.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = visualBeamWidth;
        lineRenderer.endWidth = visualBeamWidth;
        lineRenderer.useWorldSpace = true;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;

        if (beamMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            if (shader != null)
            {
                beamMaterial = new Material(shader);
                beamMaterial.name = "PrismBeam_Mat";

                if (beamMaterial.HasProperty("_Surface")) beamMaterial.SetFloat("_Surface", 1);
                if (beamMaterial.HasProperty("_Blend")) beamMaterial.SetFloat("_Blend", 1);
            }
        }

        if (beamMaterial != null)
        {
            lineRenderer.material = beamMaterial;
        }
        lineRenderer.enabled = false;

        if (enableDynamicLight)
        {
            GameObject lightObj = new GameObject("PrismSpotlight");
            lightObj.transform.SetParent(beamObj.transform);
            lightObj.transform.localPosition = Vector3.zero;
            lightObj.transform.localRotation = Quaternion.identity;

            spotlight = lightObj.AddComponent<Light>();
            spotlight.type = LightType.Spot;
            spotlight.range = beamRange;
            spotlight.spotAngle = 42f;
            spotlight.innerSpotAngle = 18f;
            spotlight.intensity = spotlightIntensity;
            spotlight.enabled = false;
        }
    }

    private void Update()
    {
        if (isEvaluating) return;
        isEvaluating = true;

        try
        {
            activeLights.RemoveWhere(l => l == null || !l.gameObject.activeInHierarchy);
            incomingPrisms.RemoveWhere(p => p == null || !p.gameObject.activeInHierarchy || !p.IsEmitting);

            bool hasActiveLight = false;
            incomingColorBuffer.Clear();

            foreach (var l in activeLights)
            {
                if (l != null && l.IsActive)
                {
                    hasActiveLight = true;
                    incomingColorBuffer.Add(l.LightColor);
                }
            }

            foreach (var p in incomingPrisms)
            {
                if (p != null && p != this && p.IsEmitting)
                {
                    hasActiveLight = true;
                    incomingColorBuffer.Add(p.ActiveEmittedColor);
                }
            }

            if (hasActiveLight)
            {
                isEmitting = true;
                Color combinedIncoming = AlbedoColorUtility.CombineAdditive(incomingColorBuffer);
                activeEmittedColor = ComputeEmittedColor(EffectivePrismColor, combinedIncoming, colorMode);
                UpdateBeamAndTargets();
            }
            else if (isEmitting)
            {
                StopEmissionImmediately();
            }
        }
        finally
        {
            isEvaluating = false;
        }
    }

    /// <summary>
    /// Cálculo puro da cor emitida pelo prisma a partir da sua cor e da luz recebida combinada.
    /// </summary>
    public static Color ComputeEmittedColor(Color intrinsicColor, Color incomingColor, PrismColorMode mode)
    {
        bool intrinsicIsWhite = AlbedoColorUtility.ColorsMatch(intrinsicColor, Color.white, true, 0.05f);
        bool incomingIsWhite = AlbedoColorUtility.ColorsMatch(incomingColor, Color.white, true, 0.05f);

        switch (mode)
        {
            case PrismColorMode.PrismOverrides:
                return !intrinsicIsWhite ? intrinsicColor : incomingColor;
            case PrismColorMode.LightColorDirect:
                return incomingColor;
            case PrismColorMode.Multiply:
                return new Color(
                    Mathf.Clamp01(incomingColor.r * intrinsicColor.r),
                    Mathf.Clamp01(incomingColor.g * intrinsicColor.g),
                    Mathf.Clamp01(incomingColor.b * intrinsicColor.b),
                    1f);
            case PrismColorMode.PrismTint:
            default:
                if (intrinsicIsWhite) return incomingColor;
                if (incomingIsWhite) return intrinsicColor;
                return AlbedoColorUtility.AddColors(incomingColor * 0.5f, intrinsicColor * 0.5f);
        }
    }

    #region Detecção de Fontes de Luz e Encadeamento entre Prismas

    public void ReceivePrismBeam(bool illuminated, PrismaController source)
    {
        if (source == null || source == this) return;

        if (illuminated)
        {
            incomingPrisms.Add(source);
        }
        else
        {
            incomingPrisms.Remove(source);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        LightSource ls = other.GetComponent<LightSource>() ?? other.GetComponentInParent<LightSource>();
        if (ls != null) activeLights.Add(ls);
    }

    private void OnTriggerExit(Collider other)
    {
        LightSource ls = other.GetComponent<LightSource>() ?? other.GetComponentInParent<LightSource>();
        if (ls != null) activeLights.Remove(ls);
    }

    private void OnCollisionEnter(Collision collision)
    {
        LightSource ls = collision.collider.GetComponent<LightSource>() ?? collision.collider.GetComponentInParent<LightSource>();
        if (ls != null) activeLights.Add(ls);
    }

    private void OnCollisionExit(Collision collision)
    {
        LightSource ls = collision.collider.GetComponent<LightSource>() ?? collision.collider.GetComponentInParent<LightSource>();
        if (ls != null) activeLights.Remove(ls);
    }

    #endregion

    public Vector3 GetBeamWorldOrigin()
    {
        return emissionPoint != null ? emissionPoint.position : transform.position;
    }

    public Vector3 GetBeamWorldDirection()
    {
        Transform src = emissionPoint != null ? emissionPoint : transform;
        Vector3 dir = src.TransformDirection(emissionDirection);

        if (keepBeamHorizontal)
        {
            Vector3 flat = new Vector3(dir.x, 0f, dir.z);
            if (flat.sqrMagnitude > 0.0001f)
            {
                return flat.normalized;
            }

            // Fallback se local emissionDirection apontar para Y/Z vertical por rotação de importação FBX
            Vector3 alt = src.up;
            Vector3 flatAlt = new Vector3(alt.x, 0f, alt.z);
            if (flatAlt.sqrMagnitude > 0.0001f)
            {
                return flatAlt.normalized;
            }
        }

        return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
    }

    private void UpdateBeamAndTargets()
    {
        Vector3 origin = GetBeamWorldOrigin();
        Vector3 direction = GetBeamWorldDirection();

        float hitDistance = beamRange;
        Vector3 endPoint = origin + direction * beamRange;

        // 1. Raycast para encontrar paredes sólidas (ignorando triggers, sombras e o próprio prisma)
        Ray ray = new Ray(origin, direction);
        RaycastHit[] wallHits = Physics.RaycastAll(ray, beamRange, obstacleLayers, QueryTriggerInteraction.Ignore);
        float closestWallDist = beamRange;
        Vector3 closestWallPoint = endPoint;

        foreach (var hit in wallHits)
        {
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.collider.GetComponentInParent<SombraController>() != null) continue;
            if (hit.collider.GetComponentInParent<PlayerController>() != null) continue;

            if (hit.distance < closestWallDist)
            {
                closestWallDist = hit.distance;
                closestWallPoint = hit.point;
            }
        }

        hitDistance = closestWallDist;
        endPoint = closestWallPoint;

        // 2. SphereCastAll ao longo do feixe para Sombras e outros Prismas
        HashSet<SombraController> newlyHitSombras = new HashSet<SombraController>();
        HashSet<PrismaController> newlyHitPrisms = new HashSet<PrismaController>();

        RaycastHit[] hits = Physics.SphereCastAll(origin, beamRadius, direction, hitDistance, ~0, QueryTriggerInteraction.Collide);
        foreach (var hit in hits)
        {
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform)) continue;

            SombraController sombra = hit.collider.GetComponentInParent<SombraController>();
            if (sombra != null && sombra.gameObject.activeInHierarchy)
            {
                Vector3 toCenter = sombra.transform.position - origin;
                float dot = toCenter.sqrMagnitude > 0.0001f ? Vector3.Dot(direction, toCenter.normalized) : 1f;
                if (dot > 0.05f || hit.distance > 0.01f)
                {
                    newlyHitSombras.Add(sombra);
                    sombra.SetIlluminated(true, this);
                }
            }

            PrismaController targetPrism = hit.collider.GetComponentInParent<PrismaController>();
            if (targetPrism != null && targetPrism != this && targetPrism.gameObject.activeInHierarchy)
            {
                // Evita ciclo direto se targetPrism já estiver nos iluminando
                if (!incomingPrisms.Contains(targetPrism))
                {
                    newlyHitPrisms.Add(targetPrism);
                    targetPrism.ReceivePrismBeam(true, this);
                }
            }
        }

        toRemoveSombras.Clear();
        foreach (var s in currentlyHitSombras)
        {
            if (!newlyHitSombras.Contains(s)) toRemoveSombras.Add(s);
        }
        foreach (var s in toRemoveSombras)
        {
            if (s != null) s.SetIlluminated(false, this);
            currentlyHitSombras.Remove(s);
        }
        foreach (var s in newlyHitSombras) currentlyHitSombras.Add(s);

        toRemovePrisms.Clear();
        foreach (var p in currentlyHitPrisms)
        {
            if (!newlyHitPrisms.Contains(p)) toRemovePrisms.Add(p);
        }
        foreach (var p in toRemovePrisms)
        {
            if (p != null) p.ReceivePrismBeam(false, this);
            currentlyHitPrisms.Remove(p);
        }
        foreach (var p in newlyHitPrisms) currentlyHitPrisms.Add(p);

        // 3. Atualiza LineRenderer visual
        if (lineRenderer != null)
        {
            lineRenderer.enabled = true;
            lineRenderer.SetPosition(0, origin);
            lineRenderer.SetPosition(1, endPoint);

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(activeEmittedColor, 0.0f),
                    new GradientColorKey(activeEmittedColor, 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.9f, 0.0f),
                    new GradientAlphaKey(0.45f, 1.0f)
                }
            );
            lineRenderer.colorGradient = gradient;
        }

        // 4. Atualiza Spotlight
        if (spotlight != null)
        {
            spotlight.enabled = true;
            spotlight.color = activeEmittedColor;
            spotlight.intensity = spotlightIntensity;
            spotlight.range = hitDistance + 2.5f;
            spotlight.transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    /// <summary>
    /// Desliga o feixe e libera imediatamente sombras e prismas encadeados.
    /// </summary>
    public void StopEmissionImmediately()
    {
        isEmitting = false;

        if (lineRenderer != null)
            lineRenderer.enabled = false;

        if (spotlight != null)
            spotlight.enabled = false;

        foreach (var sombra in currentlyHitSombras)
        {
            if (sombra != null)
                sombra.SetIlluminated(false, this);
        }
        currentlyHitSombras.Clear();

        foreach (var targetPrism in currentlyHitPrisms)
        {
            if (targetPrism != null)
                targetPrism.ReceivePrismBeam(false, this);
        }
        currentlyHitPrisms.Clear();
    }

    private void OnDisable()
    {
        activeLights.Clear();
        incomingPrisms.Clear();
        StopEmissionImmediately();
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = GetBeamWorldOrigin();
        Vector3 direction = GetBeamWorldDirection();

        Gizmos.color = isEmitting ? activeEmittedColor : EffectivePrismColor;
        Gizmos.DrawRay(origin, direction * 3f);
        Gizmos.DrawWireSphere(origin, beamRadius);
    }
}
