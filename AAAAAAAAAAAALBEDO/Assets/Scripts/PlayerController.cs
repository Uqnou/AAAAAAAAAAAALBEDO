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
    [SerializeField] private float heldObjectRotateSpeed = 75f;
    [SerializeField] private float rotationStep = 7.5f;

    private CharacterController characterController;
    private float verticalVelocity = 0f;
    private Vector3 lastSafeGroundedPosition;
    private PickableObject heldObject = null;
    private PickableObject nearestObject = null;
    private BlackFireBrazier nearestBrazier = null;

    public PickableObject HeldObject => heldObject;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        lastSafeGroundedPosition = transform.position;

        if (holdPoint == null)
        {
            GameObject hp = new GameObject("HoldPoint");
            hp.transform.SetParent(transform);
            hp.transform.localPosition = new Vector3(0, 0.4f, 1.1f);
            holdPoint = hp.transform;
        }

        if (Application.isPlaying)
        {
            EnsureSceneInteractiveObjects();
        }
    }

    private void EnsureSceneInteractiveObjects()
    {
        if (Object.FindFirstObjectByType<BlackFireBrazier>() == null)
        {
            GameObject brazierHost = GameObject.Find("Chama.Pira") ?? GameObject.Find("Pira");
            if (brazierHost != null)
            {
                if (brazierHost.GetComponent<Collider>() == null)
                {
                    SphereCollider sc = brazierHost.AddComponent<SphereCollider>();
                    sc.isTrigger = true;
                    sc.radius = 1.6f;
                }
                brazierHost.AddComponent<BlackFireBrazier>();
            }
        }

        GameObject exitObj = GameObject.Find("Cylinder");
        if (exitObj != null && exitObj.GetComponent<LevelExitButton>() == null)
        {
            if (exitObj.GetComponent<Collider>() == null)
            {
                BoxCollider bc = exitObj.AddComponent<BoxCollider>();
                bc.isTrigger = true;
                bc.size = new Vector3(2.2f, 4.0f, 2.2f);
            }
            exitObj.AddComponent<LevelExitButton>();
        }

        int lightLayer = LayerMask.NameToLayer("Light");
        foreach (Light lt in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (lt == null || lt.type == LightType.Directional) continue;
            if (lt.GetComponentInParent<PrismaController>() != null || lt.GetComponentInParent<GodRayBeam>() != null) continue;
            if (lightLayer != -1) lt.gameObject.layer = lightLayer;
            BoxCollider box = lt.GetComponent<BoxCollider>();
            if (box == null && lt.GetComponent<Collider>() == null)
            {
                box = lt.gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(3.5f, 3.5f, 22f);
                box.center = new Vector3(0f, 0f, 10f);
            }
            if (box != null)
            {
                box.isTrigger = true;
                box.size = new Vector3(Mathf.Abs(box.size.x), Mathf.Abs(box.size.y), Mathf.Abs(box.size.z));
            }
            if (lt.GetComponent<PickableObject>() == null)
            {
                Rigidbody rb = lt.GetComponent<Rigidbody>();
                if (rb != null) { rb.isKinematic = true; rb.useGravity = false; Destroy(rb); }
            }
            foreach (CapsuleCollider cap in lt.GetComponentsInChildren<CapsuleCollider>(true))
            {
                if (cap != null) Destroy(cap);
            }
            foreach (Rigidbody childRb in lt.GetComponentsInChildren<Rigidbody>(true))
            {
                if (childRb != null && childRb.GetComponent<PickableObject>() == null)
                {
                    childRb.isKinematic = true;
                    childRb.useGravity = false;
                    Destroy(childRb);
                }
            }
            if (lt.GetComponent<LightSource>() == null) lt.gameObject.AddComponent<LightSource>();
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

        float h = 0f, v = 0f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) h -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) h += 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) v += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) v -= 1f;

        Vector3 inputDir = new Vector3(h, 0f, v).normalized;
        Vector3 moveDir = Vector3.zero;

        if (inputDir.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg;
            if (Camera.main != null) targetAngle += Camera.main.transform.eulerAngles.y;

            Vector3 worldDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            moveDir = worldDirection.normalized;

            Quaternion targetRotation = Quaternion.LookRotation(worldDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        if (characterController.isGrounded) verticalVelocity = -0.5f;
        else verticalVelocity -= gravity * Time.deltaTime;

        Vector3 finalVelocity = moveDir * moveSpeed;
        finalVelocity.y = verticalVelocity;
        characterController.Move(finalVelocity * Time.deltaTime);

        if (characterController.isGrounded && transform.position.y > 1.2f)
        {
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit groundHit, 2.0f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (groundHit.collider != null && groundHit.collider.GetComponentInParent<SombraController>() == null)
                {
                    lastSafeGroundedPosition = transform.position;
                }
            }
        }

        if (transform.position.y < -3.5f)
        {
            characterController.enabled = false;
            transform.position = lastSafeGroundedPosition + Vector3.up * 0.4f;
            verticalVelocity = 0f;
            characterController.enabled = true;
        }
    }

    /// <summary>
    /// Garante linha de visão física entre o jogador e o alvo para evitar coleta/interação através de paredes ou sombras fechadas.
    /// </summary>
    public bool HasInteractionLineOfSight(Collider targetCollider)
    {
        if (targetCollider == null) return false;
        Vector3 origin = transform.position + Vector3.up * 0.55f;
        Vector3 target = targetCollider.bounds.center + Vector3.up * 0.1f;
        Vector3 delta = target - origin;
        float dist = delta.magnitude;
        if (dist <= 0.05f) return true;

        RaycastHit[] hits = Physics.RaycastAll(origin, delta / dist, dist, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            Collider c = hits[i].collider;
            if (c == null || c.isTrigger) continue;
            if (c.transform.IsChildOf(transform)) continue;
            if (c.transform.IsChildOf(targetCollider.transform) || targetCollider.transform.IsChildOf(c.transform)) continue;

            SombraController sombra = c.GetComponentInParent<SombraController>();
            if (sombra != null)
            {
                if (sombra.Mode == ShadowMode.WalkableBridge) continue;
                if (!sombra.IsPassableAtPoint(hits[i].point, 0.45f)) return false;
                continue;
            }

            if (c.gameObject.name == "Pira" || c.bounds.max.y <= targetCollider.bounds.min.y + 0.35f) continue;
            return false;
        }
        return true;
    }

    private void CheckInteractionProximity()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, interactionRadius, ~0, QueryTriggerInteraction.Collide);

        BlackFireBrazier closestBrazier = null;
        float minBrazierDist = float.MaxValue;
        foreach (var col in hitColliders)
        {
            if (col == null) continue;
            BlackFireBrazier brazier = col.GetComponent<BlackFireBrazier>() ?? col.GetComponentInParent<BlackFireBrazier>();
            if (brazier != null)
            {
                Collider brazierCol = brazier.GetComponent<Collider>() ?? col;
                if (!HasInteractionLineOfSight(brazierCol)) continue;
                float d = Vector3.Distance(transform.position, brazier.transform.position);
                if (d < minBrazierDist) { minBrazierDist = d; closestBrazier = brazier; }
            }
        }
        nearestBrazier = closestBrazier;

        if (heldObject != null)
        {
            if (nearestObject != null) { nearestObject.OnPlayerLeave(); nearestObject = null; }
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
                Collider pickableCol = pickable.GetComponent<Collider>() ?? col;
                if (!HasInteractionLineOfSight(pickableCol)) continue;
                float dist = Vector3.Distance(transform.position, pickable.transform.position);
                if (dist < minDistance) { minDistance = dist; closest = pickable; }
            }
        }

        if (closest != nearestObject)
        {
            if (nearestObject != null) nearestObject.OnPlayerLeave();
            nearestObject = closest;
            if (nearestObject != null) nearestObject.OnPlayerApproach();
        }
    }

    private void HandleHeldObjectRotation()
    {
        if (heldObject == null) return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            float rotateInput = 0f;
            if (keyboard.qKey.isPressed) rotateInput -= 1f;
            if (keyboard.rKey.isPressed) rotateInput += 1f;

            if (Mathf.Abs(rotateInput) > 0.01f)
            {
                float fineMult = (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed) ? 0.35f : 1f;
                heldObject.RotateY(rotateInput * heldObjectRotateSpeed * fineMult * Time.deltaTime);
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            float scrollY = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scrollY) > 0.01f)
            {
                heldObject.RotateY(Mathf.Sign(scrollY) * rotationStep);
            }
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
                if (nearestBrazier != null)
                {
                    TorchController heldTorch = heldObject.GetComponent<TorchController>();
                    if (heldTorch != null && !heldTorch.IsLit)
                    {
                        nearestBrazier.TryIgniteTorch(heldTorch);
                        return;
                    }
                }

                DropHeldObject();
            }
            else if (nearestObject != null)
            {
                heldObject = nearestObject;
                nearestObject = null;
                heldObject.PickUp(holdPoint);
            }
        }
    }

    private void DropHeldObject()
    {
        if (heldObject == null) return;

        Vector3 origin = transform.position + Vector3.up * 0.4f;
        Vector3 dest = holdPoint != null ? holdPoint.position : heldObject.transform.position;
        Vector3 delta = dest - origin;
        float dist = delta.magnitude;
        if (dist > 0.05f)
        {
            Vector3 dir = delta / dist;
            RaycastHit[] hits = Physics.RaycastAll(origin, dir, dist + 0.15f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider c = hits[i].collider;
                if (c == null || c.transform.IsChildOf(transform) || c.transform.IsChildOf(heldObject.transform)) continue;
                SombraController sombra = c.GetComponentInParent<SombraController>();
                if (sombra != null && sombra.Mode == ShadowMode.WalkableBridge) continue;
                if (sombra != null && sombra.Mode == ShadowMode.Barrier && sombra.IsPassableAtPoint(hits[i].point, 0.45f)) continue;

                float safeDist = Mathf.Max(0.15f, hits[i].distance - 0.35f);
                heldObject.transform.SetParent(null);
                heldObject.transform.position = origin + dir * safeDist;
                break;
            }
        }

        Vector3 throwForce = dropForwardForce > 0.001f ? transform.forward * dropForwardForce : Vector3.zero;
        heldObject.Drop(throwForce);
        heldObject = null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
