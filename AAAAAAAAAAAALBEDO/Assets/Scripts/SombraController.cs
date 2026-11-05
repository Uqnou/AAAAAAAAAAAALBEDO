using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Controla o comportamento de uma sombra específica no cenário.
/// Deve ser adicionado MANUALMENTE pelo desenvolvedor aos objetos de sombra desejados.
/// - Quando atingida pelo feixe do prisma:
///     - Seus colliders tornam-se IsTrigger = true (o jogador atravessa livremente, mas continua detectando o prisma).
///     - Seus renderers são ocultados.
/// - Quando o feixe sai de cima dela:
///     - Seus colliders voltam a ser sólidos (IsTrigger = false, bloqueando passagem).
///     - Seus renderers são reativados.
/// </summary>
[SelectionBase]
[DisallowMultipleComponent]
public class SombraController : MonoBehaviour
{
    [Header("Eventos")]
    public UnityEvent onShadowHidden;
    public UnityEvent onShadowRevealed;

    private readonly HashSet<PrismaController> activeIlluminatingPrisms = new HashSet<PrismaController>();
    private Renderer[] renderers;
    private Collider[] colliders;
    private readonly Dictionary<Collider, bool> originalTriggerStates = new Dictionary<Collider, bool>();
    private bool isHidden = false;

    public bool IsHidden => isHidden;

    private void Awake()
    {
        CacheComponents();
    }

    private void OnValidate()
    {
        CacheComponents();
    }

    private void CacheComponents()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);

        originalTriggerStates.Clear();
        foreach (var col in colliders)
        {
            if (col == null) continue;
            originalTriggerStates[col] = col.isTrigger;

            // MeshColliders precisam ser Convex para suportar isTrigger = true sem erros
            if (col is MeshCollider mc && !mc.convex)
            {
                mc.convex = true;
            }
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
            if (!isHidden)
            {
                ApplyState(true);
            }
        }
        else
        {
            activeIlluminatingPrisms.Remove(source);
            if (activeIlluminatingPrisms.Count == 0 && isHidden)
            {
                ApplyState(false);
            }
        }
    }

    private void ApplyState(bool hide)
    {
        isHidden = hide;

        // Oculta ou mostra os renderers visuais
        if (renderers == null) renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r != null) r.enabled = !hide;
        }

        // Quando iluminada (oculta): isTrigger = true (jogador atravessa livremente)
        // Quando sem luz (sólida): isTrigger = false (bloqueia o jogador)
        if (colliders == null) colliders = GetComponentsInChildren<Collider>(true);
        foreach (var c in colliders)
        {
            if (c == null) continue;

            if (hide)
            {
                c.isTrigger = true;
            }
            else
            {
                if (originalTriggerStates.TryGetValue(c, out bool orig))
                {
                    c.isTrigger = orig;
                }
                else
                {
                    c.isTrigger = false;
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
