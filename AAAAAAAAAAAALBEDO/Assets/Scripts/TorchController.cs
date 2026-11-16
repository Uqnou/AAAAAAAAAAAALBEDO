using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Controla a Tocha de Fogo Negro.
/// Uma vez acesa no Braseiro de Fogo Negro, permanece acesa e suprime qualquer LightSource
/// (incluindo luzes do teto e GodRays) dentro do seu raio de alcance.
/// </summary>
[SelectionBase, DisallowMultipleComponent]
public class TorchController : MonoBehaviour
{
    [Header("Fogo Negro")]
    [Tooltip("Indica se a tocha já está acesa com Fogo Negro.")]
    [SerializeField] private bool isLit = false;

    [Tooltip("Objeto visual da chama de Fogo Negro (ativado ao acender).")]
    [SerializeField] private GameObject blackFireVisual;

    [Tooltip("Material compartilhado do Fogo Negro (Assets/Materials/BlackFire.mat).")]
    [SerializeField] private Material blackFireMaterial;

    [Tooltip("Raio em que a tocha apaga/suprime fontes de luz (LightSource).")]
    [SerializeField] private float suppressionRadius = 7.5f;

    [Header("Eventos")]
    public UnityEvent onIgnited = new UnityEvent();
    public UnityEvent onExtinguished = new UnityEvent();

    private readonly HashSet<LightSource> currentlySuppressedLights = new HashSet<LightSource>();
    private readonly List<LightSource> toRelease = new List<LightSource>();

    public bool IsLit => isLit;
    public float SuppressionRadius { get => suppressionRadius; set => suppressionRadius = Mathf.Max(0.1f, value); }

    private void Awake()
    {
        EnsureFlameVisual();
        UpdateFlameVisual();
    }

    private void Start()
    {
        UpdateFlameVisual();
        if (isLit) UpdateLightSuppression();
    }

    private void Update()
    {
        if (isLit) UpdateLightSuppression();
    }

    public void Ignite()
    {
        if (isLit) return;
        isLit = true;
        EnsureFlameVisual();
        UpdateFlameVisual();
        UpdateLightSuppression();
        onIgnited?.Invoke();
    }

    public void Extinguish()
    {
        if (!isLit) return;
        isLit = false;
        UpdateFlameVisual();
        ReleaseAllSuppressedLights();
        onExtinguished?.Invoke();
    }

    public void SetBlackFireVisual(GameObject visual)
    {
        blackFireVisual = visual;
        UpdateFlameVisual();
    }

    private void EnsureFlameVisual()
    {
        if (blackFireVisual != null) return;
        Transform existing = transform.Find("BlackFireFlame");
        if (existing != null) { blackFireVisual = existing.gameObject; return; }
        if (!Application.isPlaying) return;

        Material mat = blackFireMaterial;
        if (mat == null)
        {
            Shader s = Shader.Find("ALBEDO/BlackFire");
            if (s == null) return;
            mat = new Material(s);
        }
        GameObject flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flame.name = "BlackFireFlame";
        Collider c = flame.GetComponent<Collider>();
        if (c != null) Destroy(c);
        flame.transform.SetParent(transform, false);
        flame.transform.localPosition = new Vector3(0f, 1.35f, 0f);
        flame.transform.localScale = new Vector3(1.1f, 1.8f, 1.1f);
        Renderer r = flame.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = mat;
        blackFireVisual = flame;
    }

    private void UpdateFlameVisual()
    {
        if (blackFireVisual != null) blackFireVisual.SetActive(isLit);
    }

    private void UpdateLightSuppression()
    {
        HashSet<LightSource> newlySuppressed = new HashSet<LightSource>();
        Collider[] hits = Physics.OverlapSphere(transform.position, suppressionRadius, ~0, QueryTriggerInteraction.Collide);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null || hit.transform.IsChildOf(transform)) continue;

            LightSource ls = hit.GetComponent<LightSource>() ?? hit.GetComponentInParent<LightSource>();
            if (ls != null && ls.gameObject.activeInHierarchy)
            {
                newlySuppressed.Add(ls);
                ls.SetSuppressed(true, this);
            }
        }

        toRelease.Clear();
        foreach (var ls in currentlySuppressedLights)
        {
            if (!newlySuppressed.Contains(ls)) toRelease.Add(ls);
        }

        for (int i = 0; i < toRelease.Count; i++)
        {
            if (toRelease[i] != null) toRelease[i].SetSuppressed(false, this);
            currentlySuppressedLights.Remove(toRelease[i]);
        }

        foreach (var ls in newlySuppressed) currentlySuppressedLights.Add(ls);
    }

    private void ReleaseAllSuppressedLights()
    {
        foreach (var ls in currentlySuppressedLights)
        {
            if (ls != null) ls.SetSuppressed(false, this);
        }
        currentlySuppressedLights.Clear();
    }

    private void OnDisable()
    {
        ReleaseAllSuppressedLights();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isLit ? new Color(0.1f, 0.1f, 0.1f, 0.85f) : new Color(0.5f, 0.5f, 0.5f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, suppressionRadius);
    }
}
