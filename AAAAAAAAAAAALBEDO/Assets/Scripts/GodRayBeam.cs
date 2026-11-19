using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Controlador principal do God Ray volumétrico vertical ou angular/diagonal.
/// Gerencia a malha cônica, o shader, as partículas de poeira suspensa com controle de preenchimento,
/// rotação e direção, o anel de demarcação no chão/parede e a física de colisão/raycast.
/// </summary>
[ExecuteAlways, SelectionBase]
public class GodRayBeam : MonoBehaviour
{
    [System.Serializable] public class EntityBeamEvent : UnityEvent<Collider, Color> { }

    [Header("Aparência e Cor")]
    [ColorUsage(true, true)] [SerializeField] private Color rayColor = new Color(0.3f, 0.8f, 1.0f, 1.0f);
    [Range(0.1f, 10.0f)] [SerializeField] private float intensity = 2.0f;
    [SerializeField] private bool enablePulsing = false;
    [SerializeField] private float pulseSpeed = 1.5f, pulseAmplitude = 0.2f;

    [Header("Geometria e Dimensões")]
    [SerializeField] private float topRadius = 0.35f, bottomRadius = 2.2f, maxHeight = 25.0f;
    [SerializeField] private int coneSegments = 32;

    [Header("Partículas de Poeira - Cor")]
    [SerializeField] private bool overrideDustColor = false;
    [ColorUsage(true, true)] [SerializeField] private Color dustCustomColor = new Color(1.0f, 1.0f, 1.0f, 0.6f);
    [Range(0f, 1f)] [SerializeField] private float dustOpacity = 0.6f;

    [Header("Partículas de Poeira - Direção e Rotação")]
    [SerializeField] private Vector3 dustEmitterRotation = new Vector3(90f, 0f, 0f), dustDriftDirection = Vector3.zero;
    [SerializeField] private float dustSpeed = 0.2f;
    [SerializeField] private bool randomStartRotation = true;
    [Range(0f, 360f)] [SerializeField] private float dustStartRotation = 0f;
    [SerializeField] private float dustAngularVelocity = 15f;

    [Header("Partículas de Poeira - Preenchimento no Cone")]
    [SerializeField] private bool dustVolumeFill = true;
    [Range(0f, 1f)] [SerializeField] private float dustRadiusFill = 1.0f;
    [Range(0.05f, 1.0f)] [SerializeField] private float dustHeightFill = 1.0f;
    [Range(0f, 1f)] [SerializeField] private float dustHeightOffset = 0f;
    [Range(0.01f, 0.5f)] [SerializeField] private float dustParticleSize = 0.08f;
    [Range(0f, 200f)] [SerializeField] private float dustEmissionRate = 12f;
    [SerializeField] private int dustMaxParticles = 80;

    [Header("Física e Ancoragem")]
    [SerializeField] private LayerMask groundLayerMask = ~0;
    [SerializeField] private bool continuousHeightCheck = false, matchGroundSlopeProjection = true;
    [SerializeField] private float raycastOffset = 0.05f;
    [SerializeField] private bool useCapsuleTrigger = false;

    [Header("Referências Internas")]
    [SerializeField] private MeshFilter beamMeshFilter;
    [SerializeField] private MeshRenderer beamMeshRenderer;
    [SerializeField] private ParticleSystem dustParticles;
    [SerializeField] private Transform groundRingTransform;
    [SerializeField] private Renderer groundRingRenderer;
    [SerializeField] private CapsuleCollider triggerCollider;

    [Header("Eventos de Física")]
    public EntityBeamEvent onEntityEnter = new EntityBeamEvent();
    public UnityEvent<Collider> onEntityExit = new UnityEvent<Collider>();

    private MaterialPropertyBlock propBlock;
    private float currentHeight = 8.0f;
    private float minProjectedHeight = 0f;
    private Mesh generatedConeMesh;
    private Coroutine colorTransitionCoroutine;
    private LightSource attachedLightSource;
    private float suppressionMultiplier = 1f;
    private static readonly int TintColorId = Shader.PropertyToID("_TintColor"), IntensityId = Shader.PropertyToID("_Intensity");

    public Color CurrentColor => rayColor;
    public Color RayColor => rayColor;
    public Color LightColor => attachedLightSource != null ? attachedLightSource.LightColor : rayColor;
    public float CurrentHeight => currentHeight;
    public float TopRadius => topRadius;
    public float BottomRadius => bottomRadius;
    public float MaxHeight => maxHeight;
    public float Intensity => intensity;
    public float EffectiveStrength => intensity * suppressionMultiplier;
    public LightSource AttachedLightSource => attachedLightSource;
    public Vector3 BeamDirection => -transform.up;
    public Color EffectiveDustColor => overrideDustColor ? dustCustomColor : new Color(rayColor.r, rayColor.g, rayColor.b, dustOpacity);
    public Renderer GroundRingRenderer => groundRingRenderer; public ParticleSystem DustParticles => dustParticles; public MeshRenderer BeamMeshRenderer => beamMeshRenderer;
    private void Awake() { EnsureComponents(); UpdateHeightWithRaycast(); ApplyVisualProperties(); }
    private void OnEnable() { AlbedoWallLightGlobals.Register(this); EnsureComponents(); UpdateHeightWithRaycast(); ApplyVisualProperties(); }
    private void OnDisable() => AlbedoWallLightGlobals.Unregister(this);

    private void Update()
    {
        if (continuousHeightCheck || !Application.isPlaying) UpdateHeightWithRaycast();
        else if (transform.hasChanged) { UpdateShaderProperties(intensity); transform.hasChanged = false; }
        if (enablePulsing && Application.isPlaying) UpdateShaderProperties(intensity * (1.0f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmplitude));
    }

    private void OnValidate()
    {
        topRadius = Mathf.Max(0.01f, topRadius); bottomRadius = Mathf.Max(0.05f, bottomRadius); maxHeight = Mathf.Max(0.5f, maxHeight);
        coneSegments = Mathf.Clamp(coneSegments, 8, 64); dustRadiusFill = Mathf.Clamp01(dustRadiusFill); dustHeightFill = Mathf.Clamp(dustHeightFill, 0.05f, 1.0f);
        dustHeightOffset = Mathf.Clamp01(dustHeightOffset); dustOpacity = Mathf.Clamp01(dustOpacity);
        dustEmissionRate = Mathf.Max(0f, dustEmissionRate); dustMaxParticles = Mathf.Max(1, dustMaxParticles);
        UpdateHeightWithRaycast(); ApplyVisualProperties();
    }

    public void SetDimensions(float topR, float botR, float maxH)
    {
        topRadius = Mathf.Max(0.01f, topR); bottomRadius = Mathf.Max(0.05f, botR); maxHeight = Mathf.Max(0.5f, maxH);
        UpdateHeightWithRaycast();
    }

    public void UpdateHeightWithRaycast()
    {
        var coneHits = GodRayProjectionHelper.EvaluateConeHits(transform, topRadius, bottomRadius, maxHeight, raycastOffset, groundLayerMask);
        float targetHeight = coneHits.hasHit ? coneHits.maxHitDistance + raycastOffset : maxHeight;
        minProjectedHeight = GodRayProjectionHelper.ConfigureProjectedGroundRing(
            groundRingTransform, groundRingRenderer, coneHits, topRadius, bottomRadius);

        if (Mathf.Abs(currentHeight - targetHeight) > 0.01f || generatedConeMesh == null)
        {
            currentHeight = targetHeight; RebuildConeMesh(); UpdateTriggerCollider(); UpdateParticleBounds();
        }
        UpdateShaderProperties(intensity);
    }

    public void RebuildConeMesh()
    {
        if (beamMeshFilter == null) return;
        if (generatedConeMesh == null) generatedConeMesh = new Mesh { name = "Procedural_GodRay_Cone" };
        else generatedConeMesh.Clear();

        int segs = coneSegments; float step = (Mathf.PI * 2.0f) / segs;
        Vector3[] v = new Vector3[(segs + 1) * 2], n = new Vector3[v.Length];
        Vector2[] uv = new Vector2[v.Length]; int[] tri = new int[segs * 6];

        for (int i = 0; i <= segs; i++)
        {
            float a = i * step, cos = Mathf.Cos(a), sin = Mathf.Sin(a); int top = i * 2, bot = top + 1;
            v[top] = new Vector3(cos * topRadius, 0f, sin * topRadius); n[top] = new Vector3(cos, 0.2f, sin).normalized; uv[top] = new Vector2((float)i / segs, 1.0f);
            v[bot] = new Vector3(cos * bottomRadius, -currentHeight, sin * bottomRadius); n[bot] = n[top]; uv[bot] = new Vector2((float)i / segs, 0.0f);
        }
        int ti = 0;
        for (int i = 0; i < segs; i++)
        {
            int t = i * 2, b = t + 1, nt = (i + 1) * 2, nb = nt + 1;
            tri[ti++] = t; tri[ti++] = b; tri[ti++] = nt; tri[ti++] = nt; tri[ti++] = b; tri[ti++] = nb;
        }
        generatedConeMesh.vertices = v; generatedConeMesh.normals = n; generatedConeMesh.uv = uv; generatedConeMesh.triangles = tri;
        generatedConeMesh.RecalculateBounds(); beamMeshFilter.sharedMesh = generatedConeMesh;
    }

    private void UpdateTriggerCollider()
    {
        if (triggerCollider == null) return;
        triggerCollider.isTrigger = true; triggerCollider.direction = 1; triggerCollider.height = currentHeight;
        triggerCollider.radius = Mathf.Max(topRadius, bottomRadius); triggerCollider.center = new Vector3(0f, -currentHeight * 0.5f, 0f);
    }

    public void UpdateParticleBounds()
    {
        if (dustParticles == null) return;
        dustParticles.transform.localPosition = Vector3.zero; dustParticles.transform.localRotation = Quaternion.Euler(dustEmitterRotation);
        var shape = dustParticles.shape; shape.enabled = true; shape.shapeType = dustVolumeFill ? ParticleSystemShapeType.ConeVolume : ParticleSystemShapeType.Cone;
        shape.radius = topRadius; shape.angle = Mathf.Atan2(bottomRadius - topRadius, currentHeight) * Mathf.Rad2Deg;
        shape.length = currentHeight * Mathf.Clamp01(dustHeightFill); shape.radiusThickness = Mathf.Clamp01(dustRadiusFill);
        shape.position = new Vector3(0f, 0f, currentHeight * Mathf.Clamp01(dustHeightOffset));

        var main = dustParticles.main; main.startSpeed = dustSpeed; main.startSize = new ParticleSystem.MinMaxCurve(dustParticleSize * 0.5f, dustParticleSize * 1.5f);
        main.maxParticles = dustMaxParticles;
        main.startRotation = randomStartRotation ? new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad) : new ParticleSystem.MinMaxCurve(dustStartRotation * Mathf.Deg2Rad);

        var vel = dustParticles.velocityOverLifetime; vel.enabled = dustDriftDirection != Vector3.zero;
        if (vel.enabled) { vel.space = ParticleSystemSimulationSpace.Local; vel.x = new ParticleSystem.MinMaxCurve(dustDriftDirection.x); vel.y = new ParticleSystem.MinMaxCurve(dustDriftDirection.y); vel.z = new ParticleSystem.MinMaxCurve(dustDriftDirection.z); }

        var rot = dustParticles.rotationOverLifetime; rot.enabled = Mathf.Abs(dustAngularVelocity) > 0.01f;
        if (rot.enabled) rot.z = new ParticleSystem.MinMaxCurve(-dustAngularVelocity * Mathf.Deg2Rad, dustAngularVelocity * Mathf.Deg2Rad);
        var em = dustParticles.emission; em.rateOverTime = dustEmissionRate;
    }

    public void ApplyVisualProperties()
    {
        UpdateShaderProperties(intensity);
        if (dustParticles != null) { var main = dustParticles.main; main.startColor = new ParticleSystem.MinMaxGradient(EffectiveDustColor); UpdateParticleBounds(); }
        if (attachedLightSource == null) attachedLightSource = GetComponent<LightSource>() ?? gameObject.AddComponent<LightSource>();
        if (attachedLightSource != null) attachedLightSource.SetLightColor(rayColor);
    }

    private void UpdateShaderProperties(float currentIntensity)
    {
        if (propBlock == null) propBlock = new MaterialPropertyBlock();
        UpdateRendererBlock(beamMeshRenderer, currentIntensity); UpdateRendererBlock(groundRingRenderer, currentIntensity);
    }

    private void UpdateRendererBlock(Renderer r, float currentIntensity)
    {
        if (r == null) return;
        propBlock.Clear();
        r.GetPropertyBlock(propBlock);
        propBlock.SetColor(TintColorId, rayColor);
        propBlock.SetFloat(IntensityId, currentIntensity * suppressionMultiplier);
        if (r == groundRingRenderer)
            GodRayProjectionHelper.ApplyProjectionProperties(propBlock, transform, topRadius, bottomRadius, currentHeight, minProjectedHeight);
        r.SetPropertyBlock(propBlock);
    }

    public void SetSuppressionMultiplier(float mult)
    {
        suppressionMultiplier = Mathf.Clamp01(mult);
        UpdateShaderProperties(intensity);
        bool visible = suppressionMultiplier > 0.005f;
        if (beamMeshRenderer != null) beamMeshRenderer.enabled = visible;
        if (groundRingRenderer != null) groundRingRenderer.enabled = visible;
        if (dustParticles != null)
        {
            if (!visible && dustParticles.isPlaying) dustParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            else if (visible && !dustParticles.isPlaying && gameObject.activeInHierarchy) dustParticles.Play(true);
        }
    }

    public void CopyVisualSettingsFrom(GodRayBeam src)
    {
        if (src == null || src == this) return;
        intensity = src.intensity; enablePulsing = src.enablePulsing; pulseSpeed = src.pulseSpeed; pulseAmplitude = src.pulseAmplitude;
        topRadius = src.topRadius; bottomRadius = src.bottomRadius; maxHeight = src.maxHeight; coneSegments = src.coneSegments;
        matchGroundSlopeProjection = src.matchGroundSlopeProjection; groundLayerMask = src.groundLayerMask; raycastOffset = src.raycastOffset;
        overrideDustColor = src.overrideDustColor; dustCustomColor = src.dustCustomColor; dustOpacity = src.dustOpacity;
        dustEmitterRotation = src.dustEmitterRotation; dustDriftDirection = src.dustDriftDirection; dustSpeed = src.dustSpeed;
        randomStartRotation = src.randomStartRotation; dustStartRotation = src.dustStartRotation; dustAngularVelocity = src.dustAngularVelocity;
        dustVolumeFill = src.dustVolumeFill; dustRadiusFill = src.dustRadiusFill; dustHeightFill = src.dustHeightFill;
        dustHeightOffset = src.dustHeightOffset; dustParticleSize = src.dustParticleSize; dustEmissionRate = src.dustEmissionRate;
        dustMaxParticles = src.dustMaxParticles; continuousHeightCheck = src.continuousHeightCheck;
        if (src.beamMeshRenderer != null && beamMeshRenderer != null) beamMeshRenderer.sharedMaterial = src.beamMeshRenderer.sharedMaterial;
        if (src.groundRingRenderer != null && groundRingRenderer != null) groundRingRenderer.sharedMaterial = src.groundRingRenderer.sharedMaterial;
        if (src.dustParticles != null && dustParticles != null)
        {
            var srcR = src.dustParticles.GetComponent<ParticleSystemRenderer>(); var dstR = dustParticles.GetComponent<ParticleSystemRenderer>();
            if (srcR != null && dstR != null) dstR.sharedMaterial = srcR.sharedMaterial;
            var dM = dustParticles.main; dM.startLifetime = src.dustParticles.main.startLifetime;
            var sN = src.dustParticles.noise; var dN = dustParticles.noise; dN.enabled = sN.enabled; if (sN.enabled) { dN.strength = sN.strength; dN.frequency = sN.frequency; }
            var sC = src.dustParticles.colorOverLifetime; var dC = dustParticles.colorOverLifetime; dC.enabled = sC.enabled; if (sC.enabled) dC.color = sC.color;
        }
        RebuildConeMesh(); ApplyVisualProperties();
    }

    public void SetColor(Color newColor, float duration = 0f)
    {
        if (duration <= 0f || !Application.isPlaying) { rayColor = newColor; ApplyVisualProperties(); return; }
        if (colorTransitionCoroutine != null) StopCoroutine(colorTransitionCoroutine);
        colorTransitionCoroutine = StartCoroutine(TransitionColorRoutine(newColor, duration));
    }

    private IEnumerator TransitionColorRoutine(Color targetColor, float duration)
    {
        Color startColor = rayColor; float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime; rayColor = Color.Lerp(startColor, targetColor, Mathf.Clamp01(elapsed / duration));
            ApplyVisualProperties(); yield return null;
        }
        rayColor = targetColor; ApplyVisualProperties(); colorTransitionCoroutine = null;
    }

    public void SetDustColor(Color customColor) { overrideDustColor = true; dustCustomColor = customColor; ApplyVisualProperties(); }
    public void SetDustFill(float radiusFill, float heightFill) { dustRadiusFill = Mathf.Clamp01(radiusFill); dustHeightFill = Mathf.Clamp(heightFill, 0.05f, 1.0f); UpdateParticleBounds(); }
    public void SetDustRotationAndDirection(Vector3 emitterRot, Vector3 driftDir) { dustEmitterRotation = emitterRot; dustDriftDirection = driftDir; UpdateParticleBounds(); }
    public void NotifyTriggerEnter(Collider other) { if (other != null) { (other.GetComponent<PrismaController>() ?? other.GetComponentInParent<PrismaController>())?.RegisterDirectGodRay(this); onEntityEnter?.Invoke(other, rayColor); } }
    public void NotifyTriggerExit(Collider other) { if (other != null) { (other.GetComponent<PrismaController>() ?? other.GetComponentInParent<PrismaController>())?.UnregisterDirectGodRay(this); onEntityExit?.Invoke(other); } }

    private void EnsureComponents()
    {
        if (beamMeshFilter == null) beamMeshFilter = GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
        if (beamMeshRenderer == null) beamMeshRenderer = GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();
        if (useCapsuleTrigger)
        {
            if (triggerCollider == null) triggerCollider = GetComponent<CapsuleCollider>() ?? gameObject.AddComponent<CapsuleCollider>();
            triggerCollider.isTrigger = true;
            Rigidbody rb = GetComponent<Rigidbody>() ?? gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true; rb.useGravity = false;
            (GetComponent<GodRayTriggerZone>() ?? gameObject.AddComponent<GodRayTriggerZone>()).Initialize(this);
        }
        else triggerCollider = null;
        if (dustParticles == null) { Transform p = transform.Find("DustParticles"); if (p != null) dustParticles = p.GetComponent<ParticleSystem>(); }
        if (groundRingTransform == null) { Transform r = transform.Find("GroundRing"); if (r != null) { groundRingTransform = r; groundRingRenderer = r.GetComponent<Renderer>(); } }
        GodRayProjectionHelper.EnsureCubeVolumeMesh(groundRingTransform, groundRingRenderer);
        if (groundRingRenderer != null && groundRingRenderer.sharedMaterial == null)
        {
            Shader s = Shader.Find("Custom/GodRayGroundRing") ?? Shader.Find("Universal Render Pipeline/Unlit");
            groundRingRenderer.sharedMaterial = new Material(s);
        }
        if (attachedLightSource == null) attachedLightSource = GetComponent<LightSource>() ?? gameObject.AddComponent<LightSource>();
    }
}