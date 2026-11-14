using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controlador de movimentação WASD em visão isométrica, pegar/soltar objetos com [E]
/// e rotação fina de prismas/tochas segurados com [Q] e [R].
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movimentação")]
    [SerializeField] private float moveSpeed = 6.0f;
    [SerializeField] private float rotationSpeed = 12.0f;
    [SerializeField] private float gravity = 20.0f;

    [Header("Interação (Pegar / Soltar / Rotacionar)")]
    [SerializeField] private float interactionRadius = 2.5f;
    [SerializeField] private Transform holdPoint;
    [SerializeField] private float dropForwardForce = 0f;
    [SerializeField] private float heldObjectRotateSpeed = 110f;

    private CharacterController characterController;
    private float verticalVelocity = 0f;
    private PickableObject heldObject = null;
    private PickableObject nearestObject = null;
    private BlackFireBrazier nearestBrazier = null;

    public PickableObject HeldObject => heldObject;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (holdPoint == null)
        {
            GameObject hp = new GameObject("HoldPoint");
            hp.transform.SetParent(transform);
            hp.transform.localPosition = new Vector3(0, 0.4f, 1.1f);
            holdPoint = hp.transform;
        }
    }

    private void Update()
    {
        HandleMovement();
        CheckInteractionProximity();
        HandleHeldObjectRotation();
        HandleInteractionInput();
    }

    private void HandleMovement()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        float h = 0f;
        float v = 0f;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) h -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) h += 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) v += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) v -= 1f;

        Vector3 inputDir = new Vector3(h, 0f, v).normalized;

        Vector3 moveDir = Vector3.zero;
        if (inputDir.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg;

            if (Camera.main != null)
            {
                targetAngle += Camera.main.transform.eulerAngles.y;
            }

            Vector3 worldDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            moveDir = worldDirection.normalized;

            Quaternion targetRotation = Quaternion.LookRotation(worldDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        if (characterController.isGrounded)
        {
            verticalVelocity = -0.5f;
        }
        else
        {
            verticalVelocity -= gravity * Time.deltaTime;
        }

        Vector3 finalVelocity = moveDir * moveSpeed;
        finalVelocity.y = verticalVelocity;

        characterController.Move(finalVelocity * Time.deltaTime);
    }

    private void CheckInteractionProximity()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, interactionRadius, ~0, QueryTriggerInteraction.Collide);

        // Verifica braseiro próximo para acendimento direto com [E] caso esteja segurando tocha apagada
        BlackFireBrazier closestBrazier = null;
        float minBrazierDist = float.MaxValue;
        foreach (var col in hitColliders)
        {
            if (col == null) continue;
            BlackFireBrazier brazier = col.GetComponent<BlackFireBrazier>() ?? col.GetComponentInParent<BlackFireBrazier>();
            if (brazier != null)
            {
                float d = Vector3.Distance(transform.position, brazier.transform.position);
                if (d < minBrazierDist)
                {
                    minBrazierDist = d;
                    closestBrazier = brazier;
                }
            }
        }
        nearestBrazier = closestBrazier;

        if (heldObject != null)
        {
            if (nearestObject != null)
            {
                nearestObject.OnPlayerLeave();
                nearestObject = null;
            }
            return;
        }

        PickableObject closest = null;
        float minDistance = float.MaxValue;

        foreach (var col in hitColliders)
        {
            if (col == null) continue;
            PickableObject pickable = col.GetComponent<PickableObject>() ?? col.GetComponentInParent<PickableObject>();
            if (pickable != null && !pickable.IsPickedUp)
            {
                float dist = Vector3.Distance(transform.position, pickable.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closest = pickable;
                }
            }
        }

        if (closest != nearestObject)
        {
            if (nearestObject != null)
                nearestObject.OnPlayerLeave();

            nearestObject = closest;

            if (nearestObject != null)
                nearestObject.OnPlayerApproach();
        }
    }

    private void HandleHeldObjectRotation()
    {
        if (heldObject == null) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        float rotateInput = 0f;
        if (keyboard.qKey.isPressed) rotateInput -= 1f;
        if (keyboard.rKey.isPressed) rotateInput += 1f;

        if (Mathf.Abs(rotateInput) > 0.01f)
        {
            heldObject.RotateY(rotateInput * heldObjectRotateSpeed * Time.deltaTime);
        }
    }

    private void HandleInteractionInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.eKey.wasPressedThisFrame)
        {
            if (heldObject != null)
            {
                // Se está segurando tocha apagada ao lado de um braseiro, acende a tocha antes de soltar
                if (nearestBrazier != null)
                {
                    TorchController heldTorch = heldObject.GetComponent<TorchController>();
                    if (heldTorch != null && !heldTorch.IsLit)
                    {
                        nearestBrazier.TryIgniteTorch(heldTorch);
                        return;
                    }
                }

                Vector3 throwForce = dropForwardForce > 0.001f
                    ? transform.forward * dropForwardForce
                    : Vector3.zero;
                heldObject.Drop(throwForce);
                heldObject = null;
            }
            else if (nearestObject != null)
            {
                heldObject = nearestObject;
                nearestObject = null;
                heldObject.PickUp(holdPoint);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
