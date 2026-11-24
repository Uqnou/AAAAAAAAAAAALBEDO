using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controla o Prisma no ALBEDO. Une a emissão visual volumétrica do GodRay_Mystic
/// com as mecânicas do GDD: presets e mistura aditiva RGB de múltiplas fontes de luz
/// e feixes encadeados de outros prismas, mantendo o feixe horizontal mesmo em malhas FBX inclinadas.
/// </summary>
[SelectionBase, DisallowMultipleComponent]
public class PrismaController : MonoBehaviour
{
    private static GameObject sharedGodRayPrefabCache;
    [Header("Cores")]
    [SerializeField] private AlbedoColorPreset colorPreset = AlbedoColorPreset.Custom;
    [SerializeField] private Color prismColor = Color.white;
    [SerializeField] private PrismColorMode colorMode = PrismColorMode.PrismTint;
    [SerializeField] private bool syncVisualIndicator = false;
    [SerializeField] private int visualMaterialSlot = 1;
    [Header("Emissão e Dimensões do God Ray (Prefab)")]
    [SerializeField] private bool emitGodRay = true, customizeGodRaySize = true;
    [SerializeField] private GameObject godRayPrefab;
    [SerializeField] private GodRayBeam prismGodRay;
    [SerializeField] private LayerMask lightLayerMask;
    [Range(0.01f, 5f)] [SerializeField] private float godRayTopRadius = 0.35f;
    [Range(0.1f, 10f)] [SerializeField] private float godRayBottomRadius = 2.14f;
    [Range(1f, 100f)] [SerializeField] private float godRayLength = 30f;

    [Header("Feixe e Direção")]
    [SerializeField] private Transform emissionPoint;
    [SerializeField] private Vector3 emissionDirection = Vector3.forward;
    [SerializeField] private bool keepBeamHorizontal = true, useLineRendererWithGodRay = false, enableDynamicLight = false;
    [SerializeField] private float beamRange = 30f, beamRadius = 0.35f, visualBeamWidth = 0.25f, spotlightIntensity = 3.5f;
    [SerializeField] private Material beamMaterial;
    [SerializeField] private LayerMask obstacleLayers = ~0;

    private readonly HashSet<LightSource> activeLights = new HashSet<LightSource>();
    private readonly HashSet<PrismaController> incomingPrisms = new HashSet<PrismaController>(), currentlyHitPrisms = new HashSet<PrismaController>();
    private readonly HashSet<ChromaticFilterGate> currentlyHitGates = new HashSet<ChromaticFilterGate>();
    private readonly HashSet<SombraController> currentlyHitSombras = new HashSet<SombraController>();
    private readonly List<Object> tempRemoveBuffer = new List<Object>();
    private readonly Dictionary<Collider, Color> lightLayerHits = new Dictionary<Collider, Color>();
    private readonly List<Color> incomingColorBuffer = new List<Color>();
    private LineRenderer lineRenderer;
    private Light spotlight;
    private Color activeEmittedColor = Color.white, syntheticColor = Color.white;
    private float currentHitDistance = 20f, currentLightStrength = 1f, syntheticStrength = 0f;
    private bool isEmitting = false, isEvaluating = false, hasSyntheticInput = false;
    private GodRayBeam activeSourceGodRay = null;
    public bool IsEmitting => isEmitting; public Color ActiveEmittedColor => activeEmittedColor;
    public float CurrentHitDistance => currentHitDistance; public float CurrentLightStrength => isEmitting ? currentLightStrength : 0f;
    public float GodRayTopRadius => godRayTopRadius; public float GodRayBottomRadius => godRayBottomRadius;
    public float EffectiveMaxLength => customizeGodRaySize ? godRayLength : beamRange;
    public GodRayBeam PrismGodRay => prismGodRay; public GameObject GodRayPrefab { get => godRayPrefab; set => godRayPrefab = value; }
    public Color EffectivePrismColor => AlbedoColorUtility.PresetToColor(colorPreset, prismColor);
    public AlbedoColorPreset ColorPreset { get => colorPreset; set { colorPreset = value; SyncPresetColor(); } }
    public Color PrismColor { get => prismColor; set { prismColor = value; colorPreset = AlbedoColorUtility.InferPreset(value); } }
    public static Color ComputeEmittedColor(Color intrinsicColor, Color incomingColor, PrismColorMode mode) => AlbedoColorUtility.ComputeEmittedColor(intrinsicColor, incomingColor, mode);
    private void OnEnable() => AlbedoWallLightGlobals.Register(this);
    private void Awake()
    {
        if (emissionPoint == null) emissionPoint = transform;
        if (lightLayerMask.value == 0) { int ll = LayerMask.NameToLayer("Light"); if (ll != -1) lightLayerMask = 1 << ll; }
        if (colorPreset == AlbedoColorPreset.Custom && AlbedoColorUtility.ColorsMatch(prismColor, Color.white, true, 0.05f) && AlbedoColorUtility.TryDetectPrismIntrinsicColor(gameObject, out Color matCol)) prismColor = matCol;
        SyncPresetColor(); AutoAssignPrefab(); SetupBeamVisuals(); EnsureGodRayInstance();
    }
    private void OnValidate()
    {
        if (lightLayerMask.value == 0) { int ll = LayerMask.NameToLayer("Light"); if (ll != -1) lightLayerMask = 1 << ll; }
        SyncPresetColor(); AutoAssignPrefab();
        if (customizeGodRaySize && prismGodRay != null) prismGodRay.SetDimensions(godRayTopRadius, godRayBottomRadius, godRayLength);
    }

    public bool IsSyntheticEmitter => transform.parent != null && transform.parent.GetComponent<ChromaticFilterGate>() != null;
    public void SetColorPreset(AlbedoColorPreset preset) { colorPreset = preset; SyncPresetColor(); }
    public void SetSyntheticInput(bool active, Color emittedColor, float strength)
    {
        bool changed = (hasSyntheticInput != active) || (syntheticColor != emittedColor);
        hasSyntheticInput = active; syntheticColor = emittedColor; syntheticStrength = Mathf.Clamp01(strength);
        if (active && syntheticStrength > 0.01f) { isEmitting = true; activeEmittedColor = emittedColor; currentLightStrength = syntheticStrength; if (Application.isPlaying && !isEvaluating && changed) Update(); }
        else if (!active && isEmitting && (IsSyntheticEmitter || (activeLights.Count == 0 && incomingPrisms.Count == 0 && lightLayerHits.Count == 0)))
        { if (IsSyntheticEmitter) { activeLights.Clear(); incomingPrisms.Clear(); lightLayerHits.Clear(); } StopEmissionImmediately(); }
    }
    private void SyncPresetColor()
    {
        if (colorPreset != AlbedoColorPreset.Custom) prismColor = AlbedoColorUtility.PresetToColor(colorPreset, prismColor);
        if (syncVisualIndicator) AlbedoColorUtility.ApplyMaterialIndicator(GetComponent<Renderer>(), visualMaterialSlot, EffectivePrismColor, true);
    }
    private void AutoAssignPrefab()
    {
        if (godRayPrefab != null) { sharedGodRayPrefabCache = godRayPrefab; return; }
#if UNITY_EDITOR
        if ((godRayPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GodRay_Mystic.prefab")) != null) sharedGodRayPrefabCache = godRayPrefab;
#endif
        if (godRayPrefab == null && sharedGodRayPrefabCache != null) godRayPrefab = sharedGodRayPrefabCache;
    }
    private void SetupBeamVisuals()
    {
        GameObject bObj = new GameObject("PrismLightBeam");
        bObj.transform.SetParent(emissionPoint != null ? emissionPoint : transform, false);
        lineRenderer = bObj.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 2; lineRenderer.startWidth = lineRenderer.endWidth = visualBeamWidth;
        lineRenderer.useWorldSpace = true; lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lineRenderer.receiveShadows = false;
        if (beamMaterial == null)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (s != null) { beamMaterial = new Material(s) { name = "PrismBeam_Mat" }; if (beamMaterial.HasProperty("_Surface")) beamMaterial.SetFloat("_Surface", 1); if (beamMaterial.HasProperty("_Blend")) beamMaterial.SetFloat("_Blend", 1); }
        }
        if (beamMaterial != null) lineRenderer.material = beamMaterial;
        lineRenderer.enabled = false;
        if (enableDynamicLight)
        {
            GameObject lObj = new GameObject("PrismSpotlight"); lObj.transform.SetParent(bObj.transform, false);
            spotlight = lObj.AddComponent<Light>(); spotlight.type = LightType.Spot;
            spotlight.range = beamRange; spotlight.spotAngle = 40f; spotlight.intensity = spotlightIntensity; spotlight.enabled = false;
        }
    }
    private bool EnsureGodRayInstance()
    {
        if (prismGodRay != null || (prismGodRay = GetComponentInChildren<GodRayBeam>(true)) != null)
        {
            prismGodRay.enabled = true; NormalizeGodRayScale();
            if (customizeGodRaySize) prismGodRay.SetDimensions(godRayTopRadius, godRayBottomRadius, godRayLength);
            return true;
        }
        AutoAssignPrefab(); if (godRayPrefab == null) return false;
        GameObject rayObj = Instantiate(godRayPrefab, emissionPoint != null ? emissionPoint : transform);
        rayObj.name = $"{gameObject.name}_GodRay"; rayObj.transform.localPosition = Vector3.zero;
        prismGodRay = rayObj.GetComponent<GodRayBeam>() ?? rayObj.GetComponentInChildren<GodRayBeam>();
        NormalizeGodRayScale();
        if (prismGodRay != null) { prismGodRay.enabled = true; if (customizeGodRaySize) prismGodRay.SetDimensions(godRayTopRadius, godRayBottomRadius, godRayLength); prismGodRay.SetColor(activeEmittedColor); }
        rayObj.SetActive(false);
        return prismGodRay != null;
    }
    private void NormalizeGodRayScale()
    {
        if (prismGodRay == null || prismGodRay.transform.parent == null) return;
        Transform parent = prismGodRay.transform.parent;
        Quaternion r = prismGodRay.transform.localRotation;
        float sx = parent.TransformVector(r * Vector3.right).magnitude;
        float sy = parent.TransformVector(r * Vector3.up).magnitude;
        float sz = parent.TransformVector(r * Vector3.forward).magnitude;
        prismGodRay.transform.localScale = new Vector3(1f / Mathf.Max(0.001f, sx), 1f / Mathf.Max(0.001f, sy), 1f / Mathf.Max(0.001f, sz));
    }
    private void Update()
    {
        if (isEvaluating) return;
        isEvaluating = true;
        try
        {
            activeLights.RemoveWhere(l => l == null || !l.gameObject.activeInHierarchy || !l.enabled);
            incomingPrisms.RemoveWhere(p => p == null || !p.gameObject.activeInHierarchy || !p.IsEmitting);
            if (lightLayerHits.Count > 0) { tempRemoveBuffer.Clear(); foreach (var c in lightLayerHits.Keys) if (c == null || !c.gameObject.activeInHierarchy) tempRemoveBuffer.Add(c); for (int i = 0; i < tempRemoveBuffer.Count; i++) lightLayerHits.Remove((Collider)tempRemoveBuffer[i]); }
            incomingColorBuffer.Clear(); float maxStrength = 0f;
            if (!IsSyntheticEmitter)
            {
                foreach (var l in activeLights) if (l != null && l.IsActive) { incomingColorBuffer.Add(l.LightColor); maxStrength = Mathf.Max(maxStrength, l.LightFactor); }
                foreach (var p in incomingPrisms) if (p != null && p != this && p.IsEmitting) { incomingColorBuffer.Add(p.ActiveEmittedColor); maxStrength = Mathf.Max(maxStrength, p.CurrentLightStrength); }
                if (incomingColorBuffer.Count == 0 && lightLayerHits.Count > 0)
                    foreach (var kvp in lightLayerHits) if (kvp.Key != null && kvp.Key.gameObject.activeInHierarchy)
                    { LightSource ls = kvp.Key.GetComponent<LightSource>() ?? kvp.Key.GetComponentInParent<LightSource>(); if (ls == null || ls.IsActive) { incomingColorBuffer.Add(AlbedoColorUtility.TryExtractObjectColor(kvp.Key.gameObject, out Color c) ? c : kvp.Value); maxStrength = Mathf.Max(maxStrength, ls != null ? ls.LightFactor : 1f); } }
            }
            if (hasSyntheticInput && syntheticStrength > 0.01f) { incomingColorBuffer.Add(syntheticColor); maxStrength = Mathf.Max(maxStrength, syntheticStrength); }
            if (!IsSyntheticEmitter) maxStrength *= AlbedoWallLightGlobals.EvaluateTorchLightMultiplier(GetBeamWorldOrigin());
            if (incomingColorBuffer.Count > 0 && maxStrength > 0.01f) { isEmitting = true; currentLightStrength = Mathf.Clamp01(maxStrength); RefreshSourceGodRay(); activeEmittedColor = hasSyntheticInput ? syntheticColor : ComputeEmittedColor(EffectivePrismColor, AlbedoColorUtility.CombineAdditive(incomingColorBuffer), colorMode); UpdateBeamAndTargets(); }
            else if (isEmitting) StopEmissionImmediately();
        }
        finally { isEvaluating = false; }
    }

    private void RefreshSourceGodRay()
    {
        if (activeSourceGodRay != null && activeSourceGodRay.gameObject.activeInHierarchy) return;
        activeSourceGodRay = null;
        foreach (var ls in activeLights) if (ls != null) { var gr = ls.GetComponent<GodRayBeam>() ?? ls.GetComponentInParent<GodRayBeam>(); if (gr != null) { activeSourceGodRay = gr; break; } }
    }

    public Vector3 GetBeamWorldOrigin() => emissionPoint != null ? emissionPoint.position : transform.position;

    public Vector3 GetBeamWorldDirection()
    {
        Transform src = emissionPoint != null ? emissionPoint : transform;
        Vector3 dir = src.TransformDirection(emissionDirection);
        if (keepBeamHorizontal)
        {
            Vector3 flat = new Vector3(dir.x, 0f, dir.z); if (flat.sqrMagnitude > 0.0001f) return flat.normalized;
            Vector3 alt = new Vector3(src.up.x, 0f, src.up.z); if (alt.sqrMagnitude > 0.0001f) return alt.normalized;
        }
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
    }

    private void UpdateBeamAndTargets()
    {
        Vector3 origin = GetBeamWorldOrigin(), dir = GetBeamWorldDirection();
        float maxDist = customizeGodRaySize ? godRayLength : beamRange, hitDist = maxDist;
        Vector3 endPoint = origin + dir * maxDist;
        ChromaticFilterGate blockingGate = null;
        foreach (var h in Physics.RaycastAll(origin, dir, maxDist, obstacleLayers, QueryTriggerInteraction.Ignore))
            if (!AlbedoWallLightGlobals.IsPassThroughCollider(h.collider, transform) && h.distance < hitDist)
            { hitDist = h.distance; endPoint = h.point; blockingGate = h.collider.GetComponentInParent<ChromaticFilterGate>(); }
        currentHitDistance = hitDist;
        var newlyHitSombras = new HashSet<SombraController>(); var newlyHitPrisms = new HashSet<PrismaController>(); var newlyHitGates = new HashSet<ChromaticFilterGate>();
        if (blockingGate != null && !blockingGate.OwnsEmitter(this) && blockingGate.gameObject.activeInHierarchy &&
            AlbedoWallLightGlobals.EvaluateTorchBeamMultiplier(endPoint, origin, dir, godRayTopRadius, godRayBottomRadius, maxDist, true, true) >= 0.25f)
        { newlyHitGates.Add(blockingGate); blockingGate.ReceivePrismBeam(true, this, dir); }
        foreach (var hit in Physics.SphereCastAll(origin, beamRadius, dir, hitDist, ~0, QueryTriggerInteraction.Collide))
        {
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform)) continue;
            if (AlbedoWallLightGlobals.EvaluateTorchBeamMultiplier(hit.point, origin, dir, godRayTopRadius, godRayBottomRadius, maxDist, true, true) < 0.25f) continue;
            SombraController s = hit.collider.GetComponentInParent<SombraController>();
            if (s == null && hit.collider.gameObject.name.StartsWith("Sombra")) s = hit.collider.gameObject.AddComponent<SombraController>();
            if (s != null && s.gameObject.activeInHierarchy && (Vector3.Dot(dir, (s.transform.position - origin).normalized) > 0.05f || hit.distance > 0.01f)) { newlyHitSombras.Add(s); s.SetIlluminated(true, this); }
            PrismaController p = hit.collider.GetComponentInParent<PrismaController>();
            if (p != null && p != this && p.gameObject.activeInHierarchy && !HasUpstreamSource(p, 8)) { newlyHitPrisms.Add(p); p.ReceivePrismBeam(true, this); }
            ChromaticFilterGate g = hit.collider.GetComponentInParent<ChromaticFilterGate>();
            if (g != null && !g.OwnsEmitter(this) && g.gameObject.activeInHierarchy) { newlyHitGates.Add(g); g.ReceivePrismBeam(true, this, dir); }
        }
        AlbedoWallLightGlobals.CollectPrismsInCone(this, origin, dir, hitDist, maxDist, godRayTopRadius, godRayBottomRadius, obstacleLayers, newlyHitPrisms);
        AlbedoWallLightGlobals.CollectGatesInCone(this, origin, dir, hitDist, maxDist, godRayTopRadius, godRayBottomRadius, obstacleLayers, newlyHitGates);
        SyncHitTargets(currentlyHitSombras, newlyHitSombras, s => s.SetIlluminated(false, this));
        SyncHitTargets(currentlyHitPrisms, newlyHitPrisms, p => p.ReceivePrismBeam(false, this));
        SyncHitTargets(currentlyHitGates, newlyHitGates, g => g.ReceivePrismBeam(false, this, Vector3.zero));
        if (emitGodRay && EnsureGodRayInstance() && prismGodRay != null)
        {
            if (!prismGodRay.gameObject.activeSelf) prismGodRay.gameObject.SetActive(true);
            prismGodRay.transform.position = origin; prismGodRay.transform.rotation = Quaternion.FromToRotation(Vector3.down, dir);
            NormalizeGodRayScale();
            if (customizeGodRaySize)
            {
                bool capAtGate = blockingGate != null && !blockingGate.OwnsEmitter(this);
                float visualLen = capAtGate ? Mathf.Max(0.2f, hitDist) : godRayLength;
                float visualBotR = capAtGate ? Mathf.Lerp(godRayTopRadius, godRayBottomRadius, Mathf.Clamp01(visualLen / Mathf.Max(0.1f, godRayLength))) : godRayBottomRadius;
                prismGodRay.SetDimensions(godRayTopRadius, visualBotR, visualLen);
            }
            prismGodRay.SetColor(activeEmittedColor); prismGodRay.SetSuppressionMultiplier(currentLightStrength); prismGodRay.UpdateHeightWithRaycast();
        }
        if (lineRenderer != null && (lineRenderer.enabled = !emitGodRay || useLineRendererWithGodRay))
        {
            lineRenderer.SetPosition(0, origin); lineRenderer.SetPosition(1, endPoint);
            lineRenderer.colorGradient = new Gradient { colorKeys = new[] { new GradientColorKey(activeEmittedColor, 0f), new GradientColorKey(activeEmittedColor, 1f) }, alphaKeys = new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0.4f, 1f) } };
        }
        if (spotlight != null) { spotlight.enabled = true; spotlight.color = activeEmittedColor; spotlight.intensity = spotlightIntensity * currentLightStrength; spotlight.range = hitDist + 2f; spotlight.transform.rotation = Quaternion.LookRotation(dir); }
    }

    public void StopEmissionImmediately()
    {
        isEmitting = false; currentLightStrength = 0f;
        if (prismGodRay != null) prismGodRay.gameObject.SetActive(false);
        if (lineRenderer != null) lineRenderer.enabled = false;
        if (spotlight != null) spotlight.enabled = false;
        foreach (var s in currentlyHitSombras) if (s != null) s.SetIlluminated(false, this);
        foreach (var p in currentlyHitPrisms) if (p != null) p.ReceivePrismBeam(false, this);
        foreach (var g in currentlyHitGates) if (g != null) g.ReceivePrismBeam(false, this, Vector3.zero);
        currentlyHitSombras.Clear(); currentlyHitPrisms.Clear(); currentlyHitGates.Clear();
    }

    private void SyncHitTargets<T>(HashSet<T> current, HashSet<T> next, System.Action<T> onExit) where T : Object
    {
        tempRemoveBuffer.Clear();
        foreach (var item in current) if (!next.Contains(item)) tempRemoveBuffer.Add(item);
        for (int i = 0; i < tempRemoveBuffer.Count; i++) { var item = (T)tempRemoveBuffer[i]; if (item != null) onExit(item); current.Remove(item); }
        foreach (var item in next) current.Add(item);
    }

    public bool HasUpstreamSource(PrismaController candidate, int depth)
    {
        if (depth <= 0 || candidate == null) return false;
        if (incomingPrisms.Contains(candidate)) return true;
        foreach (var src in incomingPrisms) if (src != null && src.HasUpstreamSource(candidate, depth - 1)) return true;
        ChromaticFilterGate gate = transform.parent != null ? transform.parent.GetComponent<ChromaticFilterGate>() : null;
        return gate != null && gate.OwnsEmitter(this) && gate.HasUpstreamSource(candidate, depth - 1);
    }

    public void ReceivePrismBeam(bool illuminated, PrismaController source) { if (!IsSyntheticEmitter && source != null && source != this) { if (illuminated) incomingPrisms.Add(source); else incomingPrisms.Remove(source); } }
    public void RegisterDirectGodRay(GodRayBeam gr) => ToggleLightRegistration(gr != null ? gr.GetComponent<Collider>() : null, gr, true);
    public void UnregisterDirectGodRay(GodRayBeam gr) => ToggleLightRegistration(gr != null ? gr.GetComponent<Collider>() : null, gr, false);
    private bool IsLightLayer(GameObject go) { int mask = lightLayerMask.value != 0 ? lightLayerMask.value : (1 << LayerMask.NameToLayer("Light")); return go != null && ((mask & (1 << go.layer)) != 0 || (go.transform.parent != null && (mask & (1 << go.transform.parent.gameObject.layer)) != 0)); }

    private void ToggleLightRegistration(Collider col, GodRayBeam directGr, bool add)
    {
        if (IsSyntheticEmitter) return;
        Transform refT = col != null ? col.transform : (directGr != null ? directGr.transform : null);
        if (refT == null || (add && refT.IsChildOf(transform))) return;
        if (!add && col != null) lightLayerHits.Remove(col);
        PrismaController owner = refT.GetComponentInParent<PrismaController>();
        if (owner != null && owner != this) { if (add) { if (!currentlyHitPrisms.Contains(owner) && !HasUpstreamSource(owner, 8)) incomingPrisms.Add(owner); } else incomingPrisms.Remove(owner); return; }
        if (add && col != null && IsLightLayer(col.gameObject)) lightLayerHits[col] = AlbedoColorUtility.TryExtractObjectColor(col.gameObject, out Color c) ? c : Color.white;
        LightSource ls = refT.GetComponent<LightSource>() ?? refT.GetComponentInParent<LightSource>();
        GodRayBeam gr = directGr ?? refT.GetComponent<GodRayBeam>() ?? refT.GetComponentInParent<GodRayBeam>();
        if (add) { if (ls != null && !ls.transform.IsChildOf(transform)) activeLights.Add(ls); else if (gr != null && !gr.transform.IsChildOf(transform)) activeLights.Add(gr.GetComponent<LightSource>() ?? gr.gameObject.AddComponent<LightSource>()); if (gr != null && !gr.transform.IsChildOf(transform)) activeSourceGodRay = gr; }
        else { if (ls != null) activeLights.Remove(ls); if (gr != null && gr == activeSourceGodRay) { activeSourceGodRay = null; RefreshSourceGodRay(); } if (activeLights.Count == 0 && lightLayerHits.Count == 0) activeSourceGodRay = null; }
    }
    private void OnTriggerEnter(Collider o) => ToggleLightRegistration(o, null, true);
    private void OnTriggerStay(Collider o) => ToggleLightRegistration(o, null, true);
    private void OnTriggerExit(Collider o) => ToggleLightRegistration(o, null, false);
    private void OnCollisionEnter(Collision c) => ToggleLightRegistration(c.collider, null, true);
    private void OnCollisionExit(Collision c) => ToggleLightRegistration(c.collider, null, false);
    private void OnDisable() { AlbedoWallLightGlobals.Unregister(this); lightLayerHits.Clear(); activeLights.Clear(); incomingPrisms.Clear(); StopEmissionImmediately(); }
}
