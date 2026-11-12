using System.Collections.Generic;
using UnityEngine;

public enum PrismColorMode { PrismTint, Multiply, PrismOverrides, LightColorDirect }

/// <summary>
/// Controla o Prisma no ALBEDO. Quando intercepta objetos na layer 'Light' ou fontes de luz,
/// instancia o prefab GodRay_Mystic, permite ajustar suas dimensões e projeta o anel de luz
/// em qualquer objeto/parede impactado.
/// </summary>
[SelectionBase, DisallowMultipleComponent]
public class PrismaController : MonoBehaviour
{
    [Header("Cores")]
    [SerializeField] private Color prismColor = Color.white;
    [SerializeField] private PrismColorMode colorMode = PrismColorMode.PrismTint;

    [Header("Emissão de God Ray (Prefab)")]
    [SerializeField] private bool emitGodRay = true;
    [Tooltip("Prefab do God Ray (Assets/GodRay_Mystic.prefab).")]
    [SerializeField] private GameObject godRayPrefab;
    [SerializeField] private GodRayBeam prismGodRay;
    [Tooltip("LayerMask para detecção de objetos de luz (camada 'Light').")]
    [SerializeField] private LayerMask lightLayerMask;

    [Header("Dimensões do God Ray")]
    [SerializeField] private bool customizeGodRaySize = true;
    [Range(0.01f, 5f)] [SerializeField] private float godRayTopRadius = 0.35f;
    [Range(0.1f, 10f)] [SerializeField] private float godRayBottomRadius = 2.14f;
    [Range(1f, 100f)] [SerializeField] private float godRayLength = 20f;

    [Header("Feixe Laser (LineRenderer)")]
    [SerializeField] private Transform emissionPoint;
    [SerializeField] private Vector3 emissionDirection = Vector3.forward;
    [SerializeField] private float beamRange = 30f, beamRadius = 0.35f, visualBeamWidth = 0.25f;
    [SerializeField] private bool useLineRendererWithGodRay = false, enableDynamicLight = false;
    [SerializeField] private Material beamMaterial;
    [SerializeField] private LayerMask obstacleLayers = ~0;

    private readonly HashSet<LightSource> activeLights = new HashSet<LightSource>();
    private readonly HashSet<SombraController> currentlyHitSombras = new HashSet<SombraController>();
    private readonly List<SombraController> toRemoveSombras = new List<SombraController>();
    private readonly Dictionary<Collider, Color> lightLayerHits = new Dictionary<Collider, Color>();

    private LineRenderer lineRenderer;
    private Light spotlight;
    private Color activeEmittedColor = Color.white;
    private bool isEmitting = false;
    private GodRayBeam activeSourceGodRay = null;

    public bool IsEmitting => isEmitting;
    public Color PrismColor { get => prismColor; set => prismColor = value; }
    public Color ActiveEmittedColor => activeEmittedColor;
    public GodRayBeam PrismGodRay => prismGodRay;
    public GameObject GodRayPrefab { get => godRayPrefab; set => godRayPrefab = value; }

    private void Awake()
    {
        if (emissionPoint == null) emissionPoint = transform;
        if (lightLayerMask.value == 0) { int ll = LayerMask.NameToLayer("Light"); if (ll != -1) lightLayerMask = 1 << ll; }
        AutoAssignPrefab(); SetupBeamVisuals(); EnsureGodRayInstance();
    }

    private void OnValidate()
    {
        if (lightLayerMask.value == 0) { int ll = LayerMask.NameToLayer("Light"); if (ll != -1) lightLayerMask = 1 << ll; }
        AutoAssignPrefab();
        if (customizeGodRaySize && prismGodRay != null) prismGodRay.SetDimensions(godRayTopRadius, godRayBottomRadius, godRayLength);
    }

    private void AutoAssignPrefab()
    {
#if UNITY_EDITOR
        if (godRayPrefab == null) godRayPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GodRay_Mystic.prefab");
#endif
    }

    private void SetupBeamVisuals()
    {
        GameObject bObj = new GameObject("PrismLightBeam");
        bObj.transform.SetParent(emissionPoint != null ? emissionPoint : transform);
        bObj.transform.localPosition = Vector3.zero; bObj.transform.localRotation = Quaternion.LookRotation(emissionDirection);
        lineRenderer = bObj.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 2; lineRenderer.startWidth = lineRenderer.endWidth = visualBeamWidth;
        lineRenderer.useWorldSpace = true; lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        if (beamMaterial == null)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            beamMaterial = new Material(s) { name = "PrismBeam_Mat" };
            if (beamMaterial.HasProperty("_Surface")) beamMaterial.SetFloat("_Surface", 1);
            if (beamMaterial.HasProperty("_Blend")) beamMaterial.SetFloat("_Blend", 1);
        }
        lineRenderer.material = beamMaterial; lineRenderer.enabled = false;

        if (enableDynamicLight)
        {
            GameObject lObj = new GameObject("PrismSpotlight"); lObj.transform.SetParent(bObj.transform); lObj.transform.localPosition = Vector3.zero;
            spotlight = lObj.AddComponent<Light>(); spotlight.type = LightType.Spot; spotlight.range = beamRange; spotlight.spotAngle = 40f; spotlight.intensity = 3.5f; spotlight.enabled = false;
        }
    }

    private void EnsureGodRayInstance()
    {
        if (prismGodRay != null || (prismGodRay = GetComponentInChildren<GodRayBeam>(true)) != null)
        {
            prismGodRay.enabled = true; if (customizeGodRaySize) prismGodRay.SetDimensions(godRayTopRadius, godRayBottomRadius, godRayLength); return;
        }
        AutoAssignPrefab(); if (godRayPrefab == null) return;
        Transform parent = emissionPoint != null ? emissionPoint : transform;
        GameObject rayObj = Instantiate(godRayPrefab, parent);
        rayObj.name = $"{gameObject.name}_GodRay"; rayObj.transform.localPosition = Vector3.zero;
        rayObj.transform.localRotation = Quaternion.FromToRotation(Vector3.down, emissionDirection);
        prismGodRay = rayObj.GetComponent<GodRayBeam>() ?? rayObj.GetComponentInChildren<GodRayBeam>();
        if (prismGodRay != null)
        {
            prismGodRay.enabled = true; if (customizeGodRaySize) prismGodRay.SetDimensions(godRayTopRadius, godRayBottomRadius, godRayLength);
            prismGodRay.SetColor(activeEmittedColor);
        }
        rayObj.SetActive(false);
    }

    private void Update()
    {
        activeLights.RemoveWhere(l => l == null || !l.gameObject.activeInHierarchy || !l.enabled);
        if (lightLayerHits.Count > 0)
        {
            List<Collider> dead = null;
            foreach (var c in lightLayerHits.Keys) if (c == null || !c.gameObject.activeInHierarchy) (dead ?? (dead = new List<Collider>())).Add(c);
            if (dead != null) foreach (var c in dead) lightLayerHits.Remove(c);
        }
        if (activeLights.Count > 0 || lightLayerHits.Count > 0) { isEmitting = true; RefreshSourceGodRay(); UpdateEmissionColor(); UpdateBeamAndShadows(); }
        else if (isEmitting) StopEmissionImmediately();
    }

    private void RefreshSourceGodRay()
    {
        if (activeSourceGodRay != null && activeSourceGodRay.gameObject.activeInHierarchy) return;
        activeSourceGodRay = null;
        foreach (var ls in activeLights) if (ls != null) { var gr = ls.GetComponent<GodRayBeam>() ?? ls.GetComponentInParent<GodRayBeam>(); if (gr != null) { activeSourceGodRay = gr; break; } }
    }

    private bool IsLightLayer(GameObject go)
    {
        if (go == null) return false;
        int mask = lightLayerMask.value != 0 ? lightLayerMask.value : (1 << LayerMask.NameToLayer("Light"));
        return (mask & (1 << go.layer)) != 0 || (go.transform.parent != null && (mask & (1 << go.transform.parent.gameObject.layer)) != 0);
    }

    private bool TryGetColorFromObject(GameObject obj, out Color color)
    {
        color = Color.white; if (obj == null) return false;
        var gr = obj.GetComponent<GodRayBeam>() ?? obj.GetComponentInParent<GodRayBeam>() ?? obj.GetComponentInChildren<GodRayBeam>();
        if (gr != null) { color = gr.CurrentColor; return true; }
        var ls = obj.GetComponent<LightSource>() ?? obj.GetComponentInParent<LightSource>() ?? obj.GetComponentInChildren<LightSource>();
        if (ls != null) { color = ls.LightColor; return true; }
        var l = obj.GetComponent<Light>() ?? obj.GetComponentInParent<Light>() ?? obj.GetComponentInChildren<Light>();
        if (l != null) { color = l.color; return true; }
        var r = obj.GetComponent<Renderer>() ?? obj.GetComponentInParent<Renderer>() ?? obj.GetComponentInChildren<Renderer>();
        if (r != null && r.sharedMaterial != null)
        {
            var m = r.sharedMaterial;
            if (m.HasProperty("_TintColor")) { color = m.GetColor("_TintColor"); return true; }
            if (m.HasProperty("_BaseColor")) { color = m.GetColor("_BaseColor"); return true; }
            if (m.HasProperty("_Color")) { color = m.GetColor("_Color"); return true; }
            color = m.color; return true;
        }
        return false;
    }

    private void UpdateEmissionColor()
    {
        Color incoming = Color.white; bool fromLightLayer = false;
        if (lightLayerHits.Count > 0)
        {
            foreach (var kvp in lightLayerHits)
                if (kvp.Key != null && kvp.Key.gameObject.activeInHierarchy) { incoming = TryGetColorFromObject(kvp.Key.gameObject, out Color c) ? c : kvp.Value; fromLightLayer = true; break; }
        }
        if (!fromLightLayer && activeSourceGodRay != null) incoming = activeSourceGodRay.CurrentColor;
        else if (!fromLightLayer) foreach (var l in activeLights) if (l != null) { incoming = l.LightColor; break; }

        if (fromLightLayer || colorMode == PrismColorMode.LightColorDirect) activeEmittedColor = incoming;
        else if (colorMode == PrismColorMode.PrismOverrides) activeEmittedColor = prismColor != Color.white ? prismColor : incoming;
        else if (colorMode == PrismColorMode.Multiply) activeEmittedColor = new Color(incoming.r * prismColor.r, incoming.g * prismColor.g, incoming.b * prismColor.b, 1f);
        else activeEmittedColor = (prismColor == Color.white) ? incoming : ((incoming == Color.white) ? prismColor : Color.Lerp(incoming, prismColor, 0.7f));
    }

    private void UpdateBeamAndShadows()
    {
        Vector3 origin = emissionPoint != null ? emissionPoint.position : transform.position;
        Vector3 dir = emissionPoint != null ? emissionPoint.TransformDirection(emissionDirection).normalized : transform.forward;
        float hitDist = customizeGodRaySize ? godRayLength : beamRange; Vector3 endPoint = origin + dir * hitDist;

        if (Physics.Raycast(new Ray(origin, dir), out RaycastHit wallHit, hitDist, obstacleLayers, QueryTriggerInteraction.Ignore))
            if (wallHit.collider.GetComponentInParent<SombraController>() == null) { hitDist = wallHit.distance; endPoint = wallHit.point; }

        HashSet<SombraController> newlyHit = new HashSet<SombraController>();
        foreach (var hit in Physics.SphereCastAll(origin, beamRadius, dir, hitDist, ~0, QueryTriggerInteraction.Collide))
        {
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform)) continue;
            SombraController s = hit.collider.GetComponentInParent<SombraController>();
            if (s != null && s.gameObject.activeInHierarchy && (Vector3.Dot(dir, (s.transform.position - origin).normalized) > 0.05f || hit.distance > 0.01f))
            { newlyHit.Add(s); s.SetIlluminated(true, this); }
        }

        toRemoveSombras.Clear();
        foreach (var s in currentlyHitSombras) if (!newlyHit.Contains(s)) toRemoveSombras.Add(s);
        foreach (var s in toRemoveSombras) { if (s != null) s.SetIlluminated(false, this); currentlyHitSombras.Remove(s); }
        foreach (var s in newlyHit) currentlyHitSombras.Add(s);

        if (emitGodRay)
        {
            EnsureGodRayInstance();
            if (prismGodRay != null)
            {
                if (!prismGodRay.gameObject.activeSelf) prismGodRay.gameObject.SetActive(true);
                if (customizeGodRaySize) prismGodRay.SetDimensions(godRayTopRadius, godRayBottomRadius, godRayLength);
                prismGodRay.transform.position = origin; prismGodRay.transform.rotation = Quaternion.FromToRotation(Vector3.down, dir);
                prismGodRay.SetColor(activeEmittedColor); prismGodRay.UpdateHeightWithRaycast();
            }
        }

        if (lineRenderer != null)
        {
            lineRenderer.enabled = !emitGodRay || useLineRendererWithGodRay;
            if (lineRenderer.enabled)
            {
                lineRenderer.SetPosition(0, origin); lineRenderer.SetPosition(1, endPoint);
                lineRenderer.colorGradient = new Gradient { colorKeys = new[] { new GradientColorKey(activeEmittedColor, 0f), new GradientColorKey(activeEmittedColor, 1f) }, alphaKeys = new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0.4f, 1f) } };
            }
        }
        if (spotlight != null) { spotlight.enabled = true; spotlight.color = activeEmittedColor; spotlight.range = hitDist + 2f; spotlight.transform.rotation = Quaternion.LookRotation(dir); }
    }

    public void StopEmissionImmediately()
    {
        isEmitting = false;
        if (prismGodRay != null) prismGodRay.gameObject.SetActive(false);
        if (lineRenderer != null) lineRenderer.enabled = false;
        if (spotlight != null) spotlight.enabled = false;
        foreach (var s in currentlyHitSombras) if (s != null) s.SetIlluminated(false, this);
        currentlyHitSombras.Clear();
    }

    public void RegisterDirectGodRay(GodRayBeam gr)
    {
        if (gr == null || gr.transform.IsChildOf(transform)) return;
        LightSource ls = gr.GetComponent<LightSource>() ?? gr.gameObject.AddComponent<LightSource>();
        activeLights.Add(ls); activeSourceGodRay = gr;
    }

    public void UnregisterDirectGodRay(GodRayBeam gr)
    {
        if (gr == null) return;
        LightSource ls = gr.GetComponent<LightSource>(); if (ls != null) activeLights.Remove(ls);
        if (activeSourceGodRay == gr) { activeSourceGodRay = null; RefreshSourceGodRay(); }
    }

    private void RegisterLight(Collider col)
    {
        if (col == null || col.transform.IsChildOf(transform)) return;
        if (IsLightLayer(col.gameObject)) lightLayerHits[col] = TryGetColorFromObject(col.gameObject, out Color c) ? c : Color.white;
        LightSource ls = col.GetComponent<LightSource>() ?? col.GetComponentInParent<LightSource>();
        GodRayBeam gr = col.GetComponent<GodRayBeam>() ?? col.GetComponentInParent<GodRayBeam>();
        if (ls != null && !ls.transform.IsChildOf(transform)) { activeLights.Add(ls); if (gr == null) gr = ls.GetComponent<GodRayBeam>() ?? ls.GetComponentInParent<GodRayBeam>(); }
        else if (gr != null && !gr.transform.IsChildOf(transform)) { LightSource gLs = gr.GetComponent<LightSource>() ?? gr.gameObject.AddComponent<LightSource>(); activeLights.Add(gLs); }
        if (gr != null && !gr.transform.IsChildOf(transform)) activeSourceGodRay = gr;
    }

    private void UnregisterLight(Collider col)
    {
        if (col == null) return;
        lightLayerHits.Remove(col);
        LightSource ls = col.GetComponent<LightSource>() ?? col.GetComponentInParent<LightSource>();
        GodRayBeam gr = col.GetComponent<GodRayBeam>() ?? col.GetComponentInParent<GodRayBeam>();
        if (ls != null) activeLights.Remove(ls);
        if (gr != null && gr == activeSourceGodRay) { activeSourceGodRay = null; RefreshSourceGodRay(); }
        if (activeLights.Count == 0 && lightLayerHits.Count == 0) activeSourceGodRay = null;
    }

    private void OnTriggerEnter(Collider o) => RegisterLight(o);
    private void OnTriggerExit(Collider o) => UnregisterLight(o);
    private void OnCollisionEnter(Collision c) => RegisterLight(c.collider);
    private void OnCollisionExit(Collision c) => UnregisterLight(c.collider);
    private void OnDisable() { lightLayerHits.Clear(); activeLights.Clear(); StopEmissionImmediately(); }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = emissionPoint != null ? emissionPoint.position : transform.position;
        Vector3 dir = emissionPoint != null ? emissionPoint.TransformDirection(emissionDirection).normalized : transform.forward;
        Gizmos.color = isEmitting ? activeEmittedColor : prismColor;
        Gizmos.DrawRay(origin, dir * 3f); Gizmos.DrawWireSphere(origin, beamRadius);
    }
}