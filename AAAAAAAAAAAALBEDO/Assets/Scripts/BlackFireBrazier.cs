using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Braseiro de Fogo Negro que acende qualquer <see cref="TorchController"/> ao entrar no alcance ou interagir com [E].
/// </summary>
[SelectionBase]
[DisallowMultipleComponent]
public class BlackFireBrazier : MonoBehaviour
{
    [Header("Configuração do Braseiro")]
    [Tooltip("Garante que o collider deste objeto atue como trigger para acender tochas ao tocar.")]
    [SerializeField] private bool ensureTriggerCollider = true;

    [Header("Eventos")]
    public UnityEvent onTorchIgnited = new UnityEvent();

    private void Awake()
    {
        EnsureTrigger();
    }

    private void OnValidate()
    {
        EnsureTrigger();
    }

    private void EnsureTrigger()
    {
        if (!ensureTriggerCollider) return;
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    public bool TryIgniteTorch(TorchController torch)
    {
        if (torch == null || torch.IsLit) return false;

        torch.Ignite();
        onTorchIgnited?.Invoke();
        return true;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryIgniteFromCollider(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryIgniteFromCollider(other);
    }

    private void TryIgniteFromCollider(Collider other)
    {
        if (other == null) return;

        TorchController torch = other.GetComponent<TorchController>() ?? other.GetComponentInParent<TorchController>();
        if (torch != null)
        {
            PickableObject pickable = torch.GetComponent<PickableObject>();
            // Evita auto-acendimento no frame inicial caso a tocha já comece encostada no pedestal do braseiro
            if (pickable != null && !pickable.IsPickedUp && Time.timeSinceLevelLoad < 0.5f)
            {
                return;
            }
            TryIgniteTorch(torch);
            return;
        }

        // Caso o Player entre segurando a tocha
        PlayerController player = other.GetComponent<PlayerController>() ?? other.GetComponentInParent<PlayerController>();
        if (player != null && player.HeldObject != null)
        {
            TorchController heldTorch = player.HeldObject.GetComponent<TorchController>();
            if (heldTorch != null)
            {
                TryIgniteTorch(heldTorch);
            }
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.05f, 0.05f, 0.05f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, 0.9f);
    }
}
