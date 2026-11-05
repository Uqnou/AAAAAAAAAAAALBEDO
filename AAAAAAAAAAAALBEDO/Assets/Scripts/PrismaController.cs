using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Modo de combinação da cor do feixe emitido pelo prisma.
/// </summary>
public enum PrismColorMode
{
    [Tooltip("Se o prisma tiver cor diferente de branco, ela tinge a luz. Se for branco, usa a cor da luz.")]
    PrismTint,

    [Tooltip("Multiplicação física de cores.")]
    Multiply,

    [Tooltip("Usa sempre a cor do prisma se diferente de branco, caso contrário a cor da luz.")]
    PrismOverrides,

    [Tooltip("Usa diretamente a cor da luz que atingiu o prisma.")]
    LightColorDirect
}

/// <summary>
/// Controla o comportamento do Prisma.
/// Deve ser adicionado MANUALMENTE pelo desenvolvedor aos objetos de prisma desejados.
/// - Quando encosta no collider de uma fonte com o script LightSource, emite um feixe de luz para frente.
/// - Só emite luz enquanto estiver em contato direto com a luz.
/// - Ao sair de perto da luz, apaga o feixe imediatamente.
/// - Quando o feixe atinge uma SombraController, muda o collider dela para IsTrigger = true e a oculta.
/// </summary>
[SelectionBase]
[DisallowMultipleComponent]
public class PrismaController : MonoBehaviour
{
    [Header("Cores")]
    [Tooltip("Cor intrínseca do prisma.")]
    [SerializeField] private Color prismColor = Color.white;

    [Tooltip("Modo de combinação entre a cor do prisma e a cor da luz.")]
    [SerializeField] private PrismColorMode colorMode = PrismColorMode.PrismTint;

    [Header("Feixe de Luz")]
    [Tooltip("Ponto de saída do feixe (se nulo, usa a posição do prisma).")]
    [SerializeField] private Transform emissionPoint;

    [Tooltip("Direção local de emissão (padrão: Vector3.forward / para frente).")]
    [SerializeField] private Vector3 emissionDirection = Vector3.forward;

    [Tooltip("Alcance máximo do feixe de luz.")]
    [SerializeField] private float beamRange = 30f;

    [Tooltip("Raio de espessura do feixe para detecção de sombras.")]
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

    // Fontes de luz atualmente em contato físico
    private readonly HashSet<LightSource> activeLights = new HashSet<LightSource>();
    private readonly HashSet<SombraController> currentlyHitSombras = new HashSet<SombraController>();
    private readonly List<SombraController> toRemoveSombras = new List<SombraController>();

    private LineRenderer lineRenderer;
    private Light spotlight;
    private Color activeEmittedColor = Color.white;
    private bool isEmitting = false;

    public bool IsEmitting => isEmitting;
    public Color PrismColor { get => prismColor; set => prismColor = value; }

    private void Awake()
    {
        if (emissionPoint == null)
        {
            emissionPoint = transform;
        }

        SetupBeamVisuals();
    }

    private void SetupBeamVisuals()
    {
        GameObject beamObj = new GameObject("PrismLightBeam");
        beamObj.transform.SetParent(emissionPoint);
        beamObj.transform.localPosition = Vector3.zero;
        beamObj.transform.localRotation = Quaternion.LookRotation(emissionDirection);

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

            beamMaterial = new Material(shader);
            beamMaterial.name = "PrismBeam_Mat";

            if (beamMaterial.HasProperty("_Surface")) beamMaterial.SetFloat("_Surface", 1);
            if (beamMaterial.HasProperty("_Blend")) beamMaterial.SetFloat("_Blend", 1);
        }

        lineRenderer.material = beamMaterial;
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
            spotlight.spotAngle = 40f;
            spotlight.innerSpotAngle = 15f;
            spotlight.intensity = 3.5f;
            spotlight.enabled = false;
        }
    }

    private void Update()
    {
        // Limpa referências nulas ou inativas
        activeLights.RemoveWhere(l => l == null || !l.gameObject.activeInHierarchy || !l.enabled);

        if (activeLights.Count > 0)
        {
            if (!isEmitting)
            {
                isEmitting = true;
            }
            UpdateEmissionColor();
            UpdateBeamAndShadows();
        }
        else
        {
            if (isEmitting)
            {
                StopEmissionImmediately();
            }
        }
    }

    #region Detecção de Contato com LightSource

    private void OnTriggerEnter(Collider other)
    {
        LightSource ls = other.GetComponent<LightSource>() ?? other.GetComponentInParent<LightSource>();
        if (ls != null)
        {
            activeLights.Add(ls);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        LightSource ls = other.GetComponent<LightSource>() ?? other.GetComponentInParent<LightSource>();
        if (ls != null)
        {
            activeLights.Remove(ls);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        LightSource ls = collision.collider.GetComponent<LightSource>() ?? collision.collider.GetComponentInParent<LightSource>();
        if (ls != null)
        {
            activeLights.Add(ls);
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        LightSource ls = collision.collider.GetComponent<LightSource>() ?? collision.collider.GetComponentInParent<LightSource>();
        if (ls != null)
        {
            activeLights.Remove(ls);
        }
    }

    #endregion

    private void UpdateEmissionColor()
    {
        Color incomingColor = Color.white;
        foreach (var l in activeLights)
        {
            if (l != null)
            {
                incomingColor = l.LightColor;
                break;
            }
        }

        switch (colorMode)
        {
            case PrismColorMode.PrismOverrides:
                activeEmittedColor = prismColor != Color.white ? prismColor : incomingColor;
                break;
            case PrismColorMode.LightColorDirect:
                activeEmittedColor = incomingColor;
                break;
            case PrismColorMode.Multiply:
                activeEmittedColor = new Color(incomingColor.r * prismColor.r, incomingColor.g * prismColor.g, incomingColor.b * prismColor.b, 1f);
                break;
            case PrismColorMode.PrismTint:
            default:
                if (prismColor == Color.white) activeEmittedColor = incomingColor;
                else if (incomingColor == Color.white) activeEmittedColor = prismColor;
                else activeEmittedColor = Color.Lerp(incomingColor, prismColor, 0.7f);
                break;
        }
    }

    private void UpdateBeamAndShadows()
    {
        Vector3 origin = emissionPoint != null ? emissionPoint.position : transform.position;
        Vector3 direction = emissionPoint != null ? emissionPoint.TransformDirection(emissionDirection).normalized : transform.forward;

        float hitDistance = beamRange;
        Vector3 endPoint = origin + direction * beamRange;

        // 1. Raycast para encontrar paredes sólidas (ignorando triggers e sombras)
        Ray ray = new Ray(origin, direction);
        if (Physics.Raycast(ray, out RaycastHit wallHit, beamRange, obstacleLayers, QueryTriggerInteraction.Ignore))
        {
            // Se o obstáculo atingido NÃO for uma sombra, limita o feixe na parede
            if (wallHit.collider.GetComponentInParent<SombraController>() == null)
            {
                hitDistance = wallHit.distance;
                endPoint = wallHit.point;
            }
        }

        // 2. Detecção precisa de sombras ao longo do feixe via SphereCastAll (incluindo Triggers)
        HashSet<SombraController> newlyHit = new HashSet<SombraController>();
        RaycastHit[] hits = Physics.SphereCastAll(origin, beamRadius, direction, hitDistance, ~0, QueryTriggerInteraction.Collide);

        foreach (var hit in hits)
        {
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform)) continue;

            SombraController sombra = hit.collider.GetComponentInParent<SombraController>();
            if (sombra != null && sombra.gameObject.activeInHierarchy)
            {
                // Garante que a sombra está estritamente à frente do prisma na direção do feixe
                Vector3 toCenter = sombra.transform.position - origin;
                float dot = Vector3.Dot(direction, toCenter.normalized);

                // Se o centro da sombra ou o ponto de impacto estiver na frente do feixe
                if (dot > 0.05f || hit.distance > 0.01f)
                {
                    newlyHit.Add(sombra);
                    sombra.SetIlluminated(true, this);
                }
            }
        }

        // Sombras que deixaram de ser atingidas pelo feixe de luz
        toRemoveSombras.Clear();
        foreach (var s in currentlyHitSombras)
        {
            if (!newlyHit.Contains(s))
            {
                toRemoveSombras.Add(s);
            }
        }

        foreach (var s in toRemoveSombras)
        {
            if (s != null) s.SetIlluminated(false, this);
            currentlyHitSombras.Remove(s);
        }

        foreach (var s in newlyHit)
        {
            currentlyHitSombras.Add(s);
        }

        // 3. Atualiza LineRenderer visual
        if (lineRenderer != null)
        {
            lineRenderer.enabled = true;
            lineRenderer.SetPosition(0, origin);
            lineRenderer.SetPosition(1, endPoint);

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(activeEmittedColor, 0.0f), new GradientColorKey(activeEmittedColor, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.85f, 0.0f), new GradientAlphaKey(0.4f, 1.0f) }
            );
            lineRenderer.colorGradient = gradient;
        }

        // 4. Atualiza Spotlight
        if (spotlight != null)
        {
            spotlight.enabled = true;
            spotlight.color = activeEmittedColor;
            spotlight.range = hitDistance + 2f;
            spotlight.transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    /// <summary>
    /// Desliga o feixe e reativa imediatamente todas as sombras atingidas.
    /// </summary>
    public void StopEmissionImmediately()
    {
        isEmitting = false;

        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }

        if (spotlight != null)
        {
            spotlight.enabled = false;
        }

        // Libera imediatamente todas as sombras
        foreach (var sombra in currentlyHitSombras)
        {
            if (sombra != null)
            {
                sombra.SetIlluminated(false, this);
            }
        }
        currentlyHitSombras.Clear();
    }

    private void OnDisable()
    {
        activeLights.Clear();
        StopEmissionImmediately();
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = emissionPoint != null ? emissionPoint.position : transform.position;
        Vector3 direction = emissionPoint != null ? emissionPoint.TransformDirection(emissionDirection).normalized : transform.forward;

        Gizmos.color = isEmitting ? activeEmittedColor : prismColor;
        Gizmos.DrawRay(origin, direction * 3f);
        Gizmos.DrawWireSphere(origin, beamRadius);
    }
}
