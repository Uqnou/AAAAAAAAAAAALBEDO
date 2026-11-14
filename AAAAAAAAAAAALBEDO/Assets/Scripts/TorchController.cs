using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Controla a Tocha de Fogo Negro.
/// Uma vez acesa no Braseiro de Fogo Negro, permanece permanentemente acesa (com suporte opcional a Extinguish() para mecânicas futuras)
/// e suprime qualquer LightSource dentro do seu raio de alcance, tanto segurada quanto solta no chão.
/// </summary>
[SelectionBase]
[DisallowMultipleComponent]
public class TorchController : MonoBehaviour
{
    [Header("Fogo Negro")]
    [Tooltip("Indica se a tocha já está acesa com Fogo Negro.")]
    [SerializeField] private bool isLit = false;

    [Tooltip("Objeto visual da chama de Fogo Negro (ativado ao acender).")]
    [SerializeField] private GameObject blackFireVisual;

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
        UpdateFlameVisual();
    }

    private void Start()
    {
        UpdateFlameVisual();
        if (isLit)
        {
            UpdateLightSuppression();
        }
    }

    private void Update()
    {
        if (isLit)
        {
            UpdateLightSuppression();
        }
    }

    /// <summary>
    /// Acende permanentemente a tocha com Fogo Negro.
    /// </summary>
    public void Ignite()
    {
        if (isLit) return;

        isLit = true;
        UpdateFlameVisual();
        UpdateLightSuppression();
        onIgnited?.Invoke();
    }

    /// <summary>
    /// Apaga a tocha e restaura imediatamente as luzes suprimidas (gancho para mecânicas futuras).
    /// </summary>
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

    private void UpdateFlameVisual()
    {
        if (blackFireVisual != null)
        {
            blackFireVisual.SetActive(isLit);
        }
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
            if (!newlySuppressed.Contains(ls))
            {
                toRelease.Add(ls);
            }
        }

        for (int i = 0; i < toRelease.Count; i++)
        {
            LightSource ls = toRelease[i];
            if (ls != null)
            {
                ls.SetSuppressed(false, this);
            }
            currentlySuppressedLights.Remove(ls);
        }

        foreach (var ls in newlySuppressed)
        {
            currentlySuppressedLights.Add(ls);
        }
    }

    private void ReleaseAllSuppressedLights()
    {
        foreach (var ls in currentlySuppressedLights)
        {
            if (ls != null)
            {
                ls.SetSuppressed(false, this);
            }
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
