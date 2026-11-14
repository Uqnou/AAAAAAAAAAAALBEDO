using UnityEngine;

/// <summary>
/// Componente para qualquer objeto que possa ser pego, rotacionado ([Q]/[R]) e solto com a tecla [E].
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PickableObject : MonoBehaviour
{
    [Header("Segurar e Posicionar")]
    [Tooltip("Trava rotação X/Z ao soltar para manter prismas e tochas na posição apontada.")]
    [SerializeField] private bool lockTiltWhenDropped = true;

    [Tooltip("Zera velocidades ao soltar para o objeto não sair rolando após mirar.")]
    [SerializeField] private bool settleOnDrop = true;

    private Rigidbody rb;
    private Collider col;
    private InteractionPrompt prompt;
    private Quaternion uprightTiltOffset = Quaternion.identity;
    private float holdYawOffset = 0f;
    private Transform currentHoldPoint;
    private bool tiltCaptured = false;

    public bool IsPickedUp { get; private set; } = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        CaptureInitialTilt();

        if (lockTiltWhenDropped && rb != null)
        {
            rb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        prompt = GetComponent<InteractionPrompt>();
        if (prompt == null)
        {
            prompt = gameObject.AddComponent<InteractionPrompt>();
        }
    }

    private void CaptureInitialTilt()
    {
        if (tiltCaptured) return;
        tiltCaptured = true;

        float currentYaw = transform.rotation.eulerAngles.y;
        uprightTiltOffset = Quaternion.Inverse(Quaternion.Euler(0f, currentYaw, 0f)) * transform.rotation;
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
        CaptureInitialTilt();
        currentHoldPoint = holdPoint;
        holdYawOffset = 0f;
        IsPickedUp = true;

        if (prompt != null)
            prompt.Hide();

        if (rb != null)
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = true;
            rb.detectCollisions = true;
        }

        transform.SetParent(holdPoint);
        transform.localPosition = Vector3.zero;
        ApplyHoldRotation();
    }

    /// <summary>
    /// Rotaciona o objeto no eixo vertical (Y global) enquanto estiver segurado (teclas Q / R).
    /// </summary>
    public void RotateY(float deltaDegrees)
    {
        if (IsPickedUp && currentHoldPoint != null)
        {
            holdYawOffset += deltaDegrees;
            ApplyHoldRotation();
        }
        else
        {
            transform.Rotate(Vector3.up, deltaDegrees, Space.World);
        }
    }

    private void LateUpdate()
    {
        if (IsPickedUp && currentHoldPoint != null)
        {
            ApplyHoldRotation();
        }
    }

    private void ApplyHoldRotation()
    {
        if (currentHoldPoint == null) return;
        float baseYaw = currentHoldPoint.eulerAngles.y;
        transform.rotation = Quaternion.Euler(0f, baseYaw + holdYawOffset, 0f) * uprightTiltOffset;
    }

    public void Drop(Vector3 throwForce)
    {
        IsPickedUp = false;
        currentHoldPoint = null;
        transform.SetParent(null);

        if (rb != null)
        {
            if (lockTiltWhenDropped)
            {
                rb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            }

            rb.isKinematic = false;
            rb.detectCollisions = true;

            if (settleOnDrop || throwForce == Vector3.zero)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            if (throwForce != Vector3.zero)
            {
                rb.AddForce(throwForce, ForceMode.Impulse);
            }
        }
    }
}
