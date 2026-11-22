using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Representa uma fonte ou volume de luz no cenário (incluindo luzes do teto e GodRays).
/// Pode ser suprimida por uma tocha com Fogo Negro (<see cref="TorchController"/>),
/// desligando tanto a luz real da Unity e os visuais de GodRay quanto a alimentação de prismas e bloqueio de passarelas.
/// </summary>
[SelectionBase, DisallowMultipleComponent]
public class LightSource : MonoBehaviour
{
    [Header("Configuração de Luz")]
    [Tooltip("Cor da luz emitida por esta fonte.")]
    [SerializeField] private Color lightColor = Color.white;

    [Tooltip("Sincroniza automaticamente a cor com o componente Light da Unity, se existir.")]
    [SerializeField] private bool syncWithUnityLight = true;

    [Tooltip("Garante que o Collider deste objeto seja um Trigger para detectar prismas, tochas e passarelas.")]
    [SerializeField] private bool ensureTriggerCollider = true;

    [Header("Eventos de Fogo Negro")]
    [SerializeField, Tooltip("Duração (em segundos) para apagar ou reacender gradualmente.")]
    private float suppressionFadeDuration = 1.35f;
    public UnityEvent onLightSuppressed = new UnityEvent();
    public UnityEvent onLightRestored = new UnityEvent();

    private readonly HashSet<TorchController> activeSuppressors = new HashSet<TorchController>();
    private Collider col;
    private Light unityLight;
    private GodRayBeam[] linkedGodRays;
    private LightSource[] childLightSources;
    private bool isSuppressed = false;
    private bool originalLightEnabled = true;
    private float baseUnityLightIntensity = 1f;
    private bool cachedUnityLightDefaults = false;
    private float currentLightFactor = 1f;
    private float lastAppliedFactor = 1f;

    public Color LightColor => lightColor;
    public bool IsSuppressed => isSuppressed;
    public float LightFactor => currentLightFactor;
    public bool IsActive => enabled && gameObject.activeInHierarchy && currentLightFactor > 0.08f;

    private void Awake()
    {
        Initialize();
    }

    private void OnValidate()
    {
        Initialize();
    }

    private void Initialize()
    {
        col = GetComponent<Collider>();
        if (col != null)
        {
            if (ensureTriggerCollider) col.isTrigger = true;
            if (col is BoxCollider box)
            {
                Vector3 sz = box.size;
                if (sz.x < 0f || sz.y < 0f || sz.z < 0f)
                    box.size = new Vector3(Mathf.Abs(sz.x), Mathf.Abs(sz.y), Mathf.Abs(sz.z));
            }
        }

        if (GetComponent<PickableObject>() == null)
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
                if (Application.isPlaying) Destroy(rb);
            }
        }

        unityLight = GetComponent<Light>();
        if (unityLight != null)
        {
            if (!cachedUnityLightDefaults)
            {
                originalLightEnabled = unityLight.enabled;
                baseUnityLightIntensity = unityLight.intensity;
                cachedUnityLightDefaults = true;
            }
            if (syncWithUnityLight)
            {
                lightColor = unityLight.color;
            }
        }

        linkedGodRays = GetComponentsInChildren<GodRayBeam>(true);
        childLightSources = GetComponentsInChildren<LightSource>(true);
        if (syncWithUnityLight && linkedGodRays != null)
        {
            foreach (var gr in linkedGodRays)
            {
                if (gr != null && gr.gameObject != gameObject)
                {
                    gr.SetColor(lightColor);
                }
            }
        }
    }

    public void SetLightColor(Color newColor)
    {
        lightColor = newColor;
        if (unityLight != null)
        {
            unityLight.color = newColor;
        }
    }

    /// <summary>
    /// Ativa ou remove a supressão desta fonte de luz por uma tocha com Fogo Negro.
    /// </summary>
    public void SetSuppressed(bool suppressed, TorchController source)
    {
        if (source == null) return;

        if (suppressed)
        {
            activeSuppressors.Add(source);
            if (!isSuppressed)
            {
                ApplySuppressedState(true);
            }
        }
        else
        {
            activeSuppressors.Remove(source);
            CleanupDeadSuppressors();
            if (activeSuppressors.Count == 0 && isSuppressed)
            {
                ApplySuppressedState(false);
            }
        }

        if (childLightSources != null)
        {
            for (int i = 0; i < childLightSources.Length; i++)
            {
                LightSource child = childLightSources[i];
                if (child != null && child != this && child.transform.parent == transform)
                {
                    child.SetSuppressed(suppressed, source);
                }
            }
        }
    }

    public void ForceClearSuppression()
    {
        activeSuppressors.Clear();
        if (isSuppressed)
        {
            ApplySuppressedState(false);
        }
    }

    private void CleanupDeadSuppressors()
    {
        activeSuppressors.RemoveWhere(t => t == null || !t.gameObject.activeInHierarchy || !t.IsLit);
    }

    private void ApplySuppressedState(bool suppressed)
    {
        isSuppressed = suppressed;
        if (!Application.isPlaying)
        {
            currentLightFactor = suppressed ? 0f : 1f;
            ApplyLightFactor();
        }
        if (suppressed) onLightSuppressed?.Invoke();
        else onLightRestored?.Invoke();
    }

    private void ApplyLightFactor()
    {
        lastAppliedFactor = currentLightFactor;
        if (unityLight == null) unityLight = GetComponent<Light>();
        if (unityLight != null)
        {
            if (!cachedUnityLightDefaults)
            {
                originalLightEnabled = unityLight.enabled;
                baseUnityLightIntensity = unityLight.intensity;
                cachedUnityLightDefaults = true;
            }
            unityLight.intensity = baseUnityLightIntensity * currentLightFactor;
            unityLight.enabled = originalLightEnabled && currentLightFactor > 0.005f;
        }

        if (linkedGodRays == null || linkedGodRays.Length == 0)
            linkedGodRays = GetComponentsInChildren<GodRayBeam>(true);

        if (linkedGodRays != null)
        {
            for (int i = 0; i < linkedGodRays.Length; i++)
                if (linkedGodRays[i] != null) linkedGodRays[i].SetSuppressionMultiplier(currentLightFactor);
        }
    }

    private void Update()
    {
        if (isSuppressed)
        {
            CleanupDeadSuppressors();
            if (activeSuppressors.Count == 0) ApplySuppressedState(false);
        }

        float target = isSuppressed ? 0f : 1f;
        if (Mathf.Abs(currentLightFactor - target) > 0.0005f)
        {
            float step = Time.deltaTime / Mathf.Max(0.05f, suppressionFadeDuration);
            currentLightFactor = Mathf.MoveTowards(currentLightFactor, target, step);
            ApplyLightFactor();
        }
        else if (Mathf.Abs(lastAppliedFactor - currentLightFactor) > 0.0005f)
        {
            ApplyLightFactor();
        }
    }

    private void OnDisable()
    {
        activeSuppressors.Clear();
        isSuppressed = false;
        currentLightFactor = 1f;
        ApplyLightFactor();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = isSuppressed
            ? new Color(0.15f, 0.15f, 0.15f, 0.55f)
            : new Color(lightColor.r, lightColor.g, lightColor.b, 0.45f);

        Collider c = GetComponent<Collider>();
        if (c != null) Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
        else Gizmos.DrawWireSphere(transform.position, 0.5f);
    }
}
