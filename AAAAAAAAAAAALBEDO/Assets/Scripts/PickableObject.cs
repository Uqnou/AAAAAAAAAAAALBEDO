using UnityEngine;

/// <summary>
/// Componente para qualquer objeto que possa ser pego e solto com a tecla [E].
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PickableObject : MonoBehaviour
{
    private Rigidbody rb;
    private Collider col;
    private InteractionPrompt prompt;

    public bool IsPickedUp { get; private set; } = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        // Adiciona ou obtém o indicador de interação [E]
        prompt = GetComponent<InteractionPrompt>();
        if (prompt == null)
        {
            prompt = gameObject.AddComponent<InteractionPrompt>();
        }
    }

    public void OnPlayerApproach()
    {
        if (!IsPickedUp && prompt != null)
        {
            prompt.Show();
        }
    }

    public void OnPlayerLeave()
    {
        if (prompt != null)
        {
            prompt.Hide();
        }
    }

    public void PickUp(Transform holdPoint)
    {
        IsPickedUp = true;
        if (prompt != null)
            prompt.Hide();

        // Desativa física para seguir as mãos/frente do personagem, mantendo colisões/triggers ativas
        rb.isKinematic = true;
        rb.detectCollisions = true;

        transform.SetParent(holdPoint);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    public void Drop(Vector3 throwForce)
    {
        IsPickedUp = false;
        transform.SetParent(null);

        // Reativa física e gravidade
        rb.isKinematic = false;
        rb.detectCollisions = true;

        if (throwForce != Vector3.zero)
        {
            rb.AddForce(throwForce, ForceMode.Impulse);
        }
    }
}
