using UnityEngine;

/// <summary>
/// Componente para qualquer objeto que possa ser pego, rotacionado ([Q]/[R]) e solto com a tecla [E].
/// Preserva a inclinação original de malhas FBX ao segurar e girar no eixo Y global.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PickableObject : MonoBehaviour
{
    [Header("Segurar e Posicionar")]
    [Tooltip("Trava rotação X/Z ao soltar para manter prismas e tochas na posição apontada.")]
    [SerializeField] private bool lockTiltWhenDropped = true;

    [Tooltip("Zera velocidades ao soltar para o objeto não sair rolando após mirar.")]
    [SerializeField] private bool settleOnDrop = true;

    [Tooltip("Se ativo, mantém o objeto reto em pé (eixo Y local para cima) na mão do jogador, ignorando a inclinação inicial da cena.")]
    [SerializeField] private bool standUprightWhenHeld = false;

    public bool StandUprightWhenHeld
    {
        get => standUprightWhenHeld;
        set { standUprightWhenHeld = value; if (value) uprightTiltOffset = Quaternion.identity; }
    }

    private Rigidbody rb;
    private Collider col;
    private InteractionPrompt prompt;
    private Quaternion uprightTiltOffset = Quaternion.identity;
    private float holdYawOffset = 0f;
    private float heldWorldYaw = 0f;
    private bool maintainWorldYawWhileHeld = false;
    private Transform currentHoldPoint;
    private bool tiltCaptured = false;
    private Vector3 safeRespawnPosition;
    private Quaternion safeRespawnRotation;

    public bool IsPickedUp { get; private set; } = false;
    public PrismSnapSlot CurrentSlot { get; private set; }
    public bool IsSnapped => CurrentSlot != null;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        safeRespawnPosition = transform.position;
        safeRespawnRotation = transform.rotation;
        if (GetComponent<TorchController>() != null
            || gameObject.name.IndexOf("Tocha", System.StringComparison.OrdinalIgnoreCase) >= 0
            || string.Equals(gameObject.name, "Cylinder.003", System.StringComparison.OrdinalIgnoreCase))
        {
            standUprightWhenHeld = true;
        }
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

        if (GetComponent<ItemProximityGlow>() == null)
        {
            gameObject.AddComponent<ItemProximityGlow>();
        }

        if (Application.isPlaying)
        {
            if (gameObject.name.StartsWith("Prisma", System.StringComparison.OrdinalIgnoreCase) && GetComponent<PrismaController>() == null)
            {
                gameObject.AddComponent<PrismaController>();
            }
            else if ((string.Equals(gameObject.name, "Cylinder.003", System.StringComparison.OrdinalIgnoreCase)
                || gameObject.name.IndexOf("Fogo", System.StringComparison.OrdinalIgnoreCase) >= 0)
                && GetComponent<TorchController>() == null)
            {
                var tc = gameObject.AddComponent<TorchController>();
                if (gameObject.name.IndexOf("Fogo", System.StringComparison.OrdinalIgnoreCase) >= 0) tc.Ignite();
            }
        }
    }

    private void CaptureInitialTilt()
    {
        if (tiltCaptured) return;
        tiltCaptured = true;

        if (standUprightWhenHeld)
        {
            uprightTiltOffset = Quaternion.identity;
            return;
        }

        float currentYaw = transform.rotation.eulerAngles.y;
        uprightTiltOffset = Quaternion.Inverse(Quaternion.Euler(0f, currentYaw, 0f)) * transform.rotation;
    }

    public void OnPlayerApproach() { if (!IsPickedUp && prompt != null) prompt.Show(); }
    public void OnPlayerLeave() { if (prompt != null) prompt.Hide(); }

    private void ReleaseCurrentSlot()
    {
        if (CurrentSlot == null) return;
        CurrentSlot.ReleaseObject(this);
        CurrentSlot = null;
    }

    private void FreezeRigidbodyMotion(bool kinematic)
    {
        if (rb == null) return;
        if (!rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        rb.isKinematic = kinematic;
        rb.detectCollisions = true;
    }

    public void PickUp(Transform holdPoint)
    {
        ReleaseCurrentSlot();
        CaptureInitialTilt();
        currentHoldPoint = holdPoint;
        holdYawOffset = 0f;
        maintainWorldYawWhileHeld = GetComponent<PrismaController>() != null;
        heldWorldYaw = holdPoint != null ? holdPoint.eulerAngles.y : transform.eulerAngles.y;
        IsPickedUp = true;

        if (prompt != null) prompt.Hide();
        FreezeRigidbodyMotion(true);

        transform.SetParent(holdPoint);
        transform.localPosition = Vector3.zero;
        ApplyHoldRotation();
    }

    /// <summary>
    /// Rotaciona o objeto no eixo vertical (Y global) enquanto estiver segurado (teclas Q / R ou roda do mouse).
    /// </summary>
    public void RotateY(float deltaDegrees)
    {
        if (IsPickedUp && currentHoldPoint != null)
        {
            if (maintainWorldYawWhileHeld) heldWorldYaw += deltaDegrees;
            else holdYawOffset += deltaDegrees;
            ApplyHoldRotation();
        }
        else
        {
            transform.Rotate(Vector3.up, deltaDegrees, Space.World);
        }
    }

    private void Update()
    {
        if (!Application.isPlaying || IsPickedUp) return;

        if (transform.position.y < -3.5f)
        {
            if (rb != null && !rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            transform.SetPositionAndRotation(safeRespawnPosition + Vector3.up * 0.25f, safeRespawnRotation);
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
        float yaw = maintainWorldYawWhileHeld ? heldWorldYaw : (currentHoldPoint.eulerAngles.y + holdYawOffset);
        Quaternion tilt = standUprightWhenHeld ? Quaternion.identity : uprightTiltOffset;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f) * tilt;
    }

    public void SnapToSlot(PrismSnapSlot slot, Vector3 targetPosition, Quaternion targetRotation, bool preserveUprightTilt = true)
    {
        CaptureInitialTilt();
        IsPickedUp = false;
        currentHoldPoint = null;
        CurrentSlot = slot;
        if (prompt != null) prompt.Hide();
        transform.SetParent(null);

        Quaternion finalRot = targetRotation;
        if (preserveUprightTilt)
        {
            Quaternion tilt = standUprightWhenHeld ? Quaternion.identity : uprightTiltOffset;
            finalRot = Quaternion.Euler(0f, targetRotation.eulerAngles.y, 0f) * tilt;
        }

        safeRespawnPosition = targetPosition;
        safeRespawnRotation = finalRot;
        FreezeRigidbodyMotion(true);

        transform.SetPositionAndRotation(targetPosition, finalRot);
        Physics.SyncTransforms();
    }

    public void Drop(Vector3 throwForce)
    {
        PrismSnapSlot nearbySlot = PrismSnapSlot.FindClosestAvailableSlot(transform.position, this);
        if (nearbySlot != null && nearbySlot.AutoSnapOnDropNearby)
        {
            nearbySlot.SnapObject(this);
            return;
        }

        IsPickedUp = false;
        currentHoldPoint = null;
        ReleaseCurrentSlot();
        transform.SetParent(null);

        if (Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, out RaycastHit hit, 3.0f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider != null && hit.collider.GetComponentInParent<SombraController>() == null && hit.point.y > 0.5f)
            {
                safeRespawnPosition = hit.point + Vector3.up * 0.2f;
                safeRespawnRotation = transform.rotation;
            }
        }

        if (rb != null)
        {
            if (lockTiltWhenDropped)
            {
                rb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                if (GetComponent<PrismaController>() != null)
                {
                    rb.constraints |= RigidbodyConstraints.FreezeRotationY;
                }
            }

            rb.isKinematic = false;
            rb.detectCollisions = true;

            if (settleOnDrop || throwForce == Vector3.zero)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            if (throwForce != Vector3.zero && !settleOnDrop)
            {
                rb.AddForce(throwForce, ForceMode.Impulse);
            }
        }
    }

    private void OnDestroy() => ReleaseCurrentSlot();
}
