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
    public UnityEvent onLightSuppressed = new UnityEvent();
    public UnityEvent onLightRestored = new UnityEvent();

    private readonly HashSet<TorchController> activeSuppressors = new HashSet<TorchController>();
    private Collider col;
    private Light unityLight;
    private GodRayBeam[] linkedGodRays;
    private bool isSuppressed = false;
    private bool originalLightEnabled = true;

    public Color LightColor => lightColor;
    public bool IsSuppressed => isSuppressed;
    public bool IsActive => enabled && gameObject.activeInHierarchy && !isSuppressed;

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
            if (!isSuppressed)
            {
                originalLightEnabled = unityLight.enabled;
            }
            if (syncWithUnityLight)
            {
                lightColor = unityLight.color;
            }
        }

        linkedGodRays = GetComponentsInChildren<GodRayBeam>(true);
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

        if (unityLight == null) unityLight = GetComponent<Light>();
        if (unityLight != null)
        {
            if (suppressed)
            {
                originalLightEnabled = unityLight.enabled;
                unityLight.enabled = false;
            }
            else
            {
                unityLight.enabled = originalLightEnabled;
            }
        }

        if (linkedGodRays == null || linkedGodRays.Length == 0)
            linkedGodRays = GetComponentsInChildren<GodRayBeam>(true);

        if (linkedGodRays != null)
        {
            foreach (var gr in linkedGodRays)
            {
                if (gr == null) continue;
                if (gr.BeamMeshRenderer != null) gr.BeamMeshRenderer.enabled = !suppressed;
                if (gr.GroundRingRenderer != null) gr.GroundRingRenderer.enabled = !suppressed;
                if (gr.DustParticles != null)
                {
                    if (suppressed) gr.DustParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    else if (gr.gameObject.activeInHierarchy) gr.DustParticles.Play(true);
                }
            }
        }

        if (suppressed) onLightSuppressed?.Invoke();
        else onLightRestored?.Invoke();
    }

    private void Update()
    {
        if (isSuppressed)
        {
            CleanupDeadSuppressors();
            if (activeSuppressors.Count == 0)
            {
                ApplySuppressedState(false);
            }
        }
    }

    private void OnDisable()
    {
        activeSuppressors.Clear();
        if (isSuppressed)
        {
            ApplySuppressedState(false);
        }
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
