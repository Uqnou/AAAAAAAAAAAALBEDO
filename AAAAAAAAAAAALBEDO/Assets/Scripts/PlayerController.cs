using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controlador de movimentação WASD em visão isométrica e interação de pegar/soltar objetos com [E].
/// Utiliza o novo Input System do Unity via Keyboard.current.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movimentação")]
    [SerializeField] private float moveSpeed = 6.0f;
    [SerializeField] private float rotationSpeed = 12.0f;
    [SerializeField] private float gravity = 20.0f;

    [Header("Interação (Pegar / Soltar)")]
    [SerializeField] private float interactionRadius = 2.5f;
    [SerializeField] private Transform holdPoint;
    [SerializeField] private float dropForwardForce = 1.5f;

    private CharacterController characterController;
    private float verticalVelocity = 0f;
    private PickableObject heldObject = null;
    private PickableObject nearestObject = null;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        // Cria o ponto de segurar à frente da cápsula caso não tenha sido atribuído no Inspector
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
        HandleInteractionInput();
    }

    private void HandleMovement()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        float h = 0f;
        float v = 0f;

        // Leitura WASD e Setas pelo novo Input System
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) h -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) h += 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) v += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) v -= 1f;

        Vector3 inputDir = new Vector3(h, 0f, v).normalized;

        // Converte o input para a perspectiva da câmera isométrica (45 graus de rotação)
        Vector3 moveDir = Vector3.zero;
        if (inputDir.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg;

            // Se existir câmera, alinha com a rotação Y da câmera (45° na isométrica)
            if (Camera.main != null)
            {
                targetAngle += Camera.main.transform.eulerAngles.y;
            }

            Vector3 worldDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            moveDir = worldDirection.normalized;

            // Rotação suave da cápsula em direção ao movimento
            Quaternion targetRotation = Quaternion.LookRotation(worldDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // Gravidade
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
        // Se já estiver segurando um objeto, não precisa destacar outros
        if (heldObject != null)
        {
            if (nearestObject != null)
            {
                nearestObject.OnPlayerLeave();
                nearestObject = null;
            }
            return;
        }

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, interactionRadius);
        PickableObject closest = null;
        float minDistance = float.MaxValue;

        foreach (var col in hitColliders)
        {
            PickableObject pickable = col.GetComponent<PickableObject>();
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

        // Se mudou o objeto mais próximo em alcance
        if (closest != nearestObject)
        {
            if (nearestObject != null)
                nearestObject.OnPlayerLeave();

            nearestObject = closest;

            if (nearestObject != null)
                nearestObject.OnPlayerApproach();
        }
    }

    private void HandleInteractionInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        // Tecla E via novo Input System (wasPressedThisFrame)
        if (keyboard.eKey.wasPressedThisFrame)
        {
            if (heldObject != null)
            {
                // Soltar o objeto
                Vector3 throwForce = transform.forward * dropForwardForce + Vector3.up * 0.5f;
                heldObject.Drop(throwForce);
                heldObject = null;
            }
            else if (nearestObject != null)
            {
                // Pegar o objeto
                heldObject = nearestObject;
                nearestObject = null;
                heldObject.PickUp(holdPoint);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Desenha raio de alcance de interação no Scene View
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
