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
    [SerializeField] private Transform offHandPoint;
    [SerializeField] private float dropForwardForce = 0f;
    [SerializeField] private float heldObjectRotateSpeed = 75f;
    [SerializeField] private float rotationStep = 7.5f;

    private CharacterController characterController;
    private float verticalVelocity = 0f;
    private Vector3 lastSafeGroundedPosition;
    private PickableObject heldObject = null;
    private PickableObject offHandObject = null;
    private PickableObject nearestObject = null;
    private BlackFireBrazier nearestBrazier = null;
    private PrismSnapSlot nearestSlot = null;
    private bool torchDirectionLocked = false;

    public PickableObject HeldObject => heldObject;
    public PickableObject OffHandObject => offHandObject;
    public PickableObject OffHandTorch => IsTorch(offHandObject) ? offHandObject : (IsTorch(heldObject) ? heldObject : null);
    public bool HasTorchEquipped => IsTorch(heldObject) || IsTorch(offHandObject);

    private bool HasUnlitTorch()
    {
        TorchController t1 = heldObject != null ? heldObject.GetComponent<TorchController>() : null;
        TorchController t2 = offHandObject != null ? offHandObject.GetComponent<TorchController>() : null;
        return (t1 != null && !t1.IsLit) || (t2 != null && !t2.IsLit);
    }

    public static bool IsTorch(PickableObject obj)
    {
        if (obj == null) return false;
        return obj.GetComponent<TorchController>() != null || obj.name.IndexOf("Tocha", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        lastSafeGroundedPosition = transform.position;
        if (holdPoint == null)
        {
            GameObject hp = new GameObject("HoldPoint");
            hp.transform.SetParent(transform);
            hp.transform.localPosition = new Vector3(0, 0.4f, 1.05f);
            holdPoint = hp.transform;
        }
        if (offHandPoint == null)
        {
            GameObject ohp = new GameObject("OffHandPoint");
            ohp.transform.SetParent(transform);
            ohp.transform.localPosition = new Vector3(-0.40f, 0.40f, 0.95f);
            offHandPoint = ohp.transform;
        }
        UpdateHoldPointsPositions();
        if (Application.isPlaying) EnsureSceneInteractiveObjects();
    }

    private void EnsureSceneInteractiveObjects()
    {
        if (Object.FindFirstObjectByType<BlackFireBrazier>() == null)
        {
            GameObject brazierHost = GameObject.Find("Chama.Pira") ?? GameObject.Find("Pira");
            if (brazierHost != null)
            {
                if (brazierHost.GetComponent<Collider>() == null) { var sc = brazierHost.AddComponent<SphereCollider>(); sc.isTrigger = true; sc.radius = 1.6f; }
                brazierHost.AddComponent<BlackFireBrazier>();
            }
        }
        GameObject exitObj = GameObject.Find("Cylinder");
        if (exitObj != null && exitObj.GetComponent<LevelExitButton>() == null)
        {
            if (exitObj.GetComponent<Collider>() == null) { var bc = exitObj.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(2.2f, 4.0f, 2.2f); }
            exitObj.AddComponent<LevelExitButton>();
        }
        foreach (Transform tr in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (tr != null && tr.name.IndexOf("Fogo", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                TorchController tc = tr.GetComponent<TorchController>() ?? tr.gameObject.AddComponent<TorchController>();
                tc.Ignite();
            }
        }

        int lightLayer = LayerMask.NameToLayer("Light");
        foreach (Light lt in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (lt == null || lt.type == LightType.Directional) continue;
            if (lt.GetComponentInParent<PrismaController>() != null || lt.GetComponentInParent<GodRayBeam>() != null || lt.GetComponentInParent<ExitGodRayController>() != null) continue;
            if (lightLayer != -1) lt.gameObject.layer = lightLayer;
            BoxCollider box = lt.GetComponent<BoxCollider>();
            if (box == null && lt.GetComponent<Collider>() == null) { box = lt.gameObject.AddComponent<BoxCollider>(); box.size = new Vector3(3.5f, 3.5f, 22f); box.center = new Vector3(0f, 0f, 10f); }
            if (box != null) { box.isTrigger = true; box.size = new Vector3(Mathf.Abs(box.size.x), Mathf.Abs(box.size.y), Mathf.Abs(box.size.z)); }
            foreach (var cap in lt.GetComponentsInChildren<CapsuleCollider>(true)) if (cap != null) Destroy(cap);
            foreach (var childRb in lt.GetComponentsInChildren<Rigidbody>(true)) if (childRb != null && childRb.GetComponent<PickableObject>() == null) { childRb.isKinematic = true; childRb.useGravity = false; Destroy(childRb); }
            if (lt.GetComponent<LightSource>() == null) lt.gameObject.AddComponent<LightSource>();
        }
    }

    private void Update()
    {
        HandleMovement();
        SyncPrismOrientationWhenDualWielding();
        CheckInteractionProximity();
        HandleHeldObjectRotation();
        HandleInteractionInput();
    }

    private void SyncPrismOrientationWhenDualWielding()
    {
        bool p1 = heldObject != null && heldObject.GetComponent<PrismaController>() != null;
        bool p2 = offHandObject != null && offHandObject.GetComponent<PrismaController>() != null;

        // Se ambos forem prismas (2 prismas): se comportam como prismas normais girados juntos com Q/R
        if (p1 && p2)
        {
            heldObject.MaintainWorldYawWhileHeld = true;
            offHandObject.MaintainWorldYawWhileHeld = true;
            return;
        }

        // Se há 2 itens e um deles é prisma (ex: Tocha + Prisma):
        // o prisma fica mirado para a frente e sua luz segue o jogador
        if ((heldObject != null && offHandObject != null) && (p1 || p2))
        {
            if (p1)
            {
                heldObject.MaintainWorldYawWhileHeld = false;
                heldObject.ResetHoldYawOffset();
            }
            if (p2)
            {
                offHandObject.MaintainWorldYawWhileHeld = false;
                offHandObject.ResetHoldYawOffset();
            }
        }
    }

    private void HandleMovement()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        float h = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
        float v = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f)
                - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);

        Vector3 inputDir = new Vector3(h, 0f, v).normalized;
        Vector3 moveDir = Vector3.zero;
        if (inputDir.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg;
            if (Camera.main != null) targetAngle += Camera.main.transform.eulerAngles.y;

            Vector3 worldDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            moveDir = worldDirection.normalized;

            // Se mudou a direção com Q/R enquanto segura a tocha, mantém a direção fixa sem girar pelo WASD
            if (!HasTorchEquipped || !torchDirectionLocked)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(worldDirection), rotationSpeed * Time.deltaTime);
            }
        }

        verticalVelocity = characterController.isGrounded ? -0.5f : (verticalVelocity - gravity * Time.deltaTime);
        characterController.Move(new Vector3(moveDir.x * moveSpeed, verticalVelocity, moveDir.z * moveSpeed) * Time.deltaTime);

        if (characterController.isGrounded && transform.position.y > 1.2f &&
            Physics.Raycast(transform.position, Vector3.down, out RaycastHit groundHit, 2.0f, ~0, QueryTriggerInteraction.Ignore) &&
            groundHit.collider != null && groundHit.collider.GetComponentInParent<SombraController>() == null)
            lastSafeGroundedPosition = transform.position;

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
        Vector3 delta = (targetCollider.bounds.center + Vector3.up * 0.1f) - origin;
        float dist = delta.magnitude;
        if (dist <= 0.05f) return true;

        RaycastHit[] hits = Physics.RaycastAll(origin, delta / dist, dist, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            Collider c = hits[i].collider;
            if (c == null || c.isTrigger || c.transform.IsChildOf(transform)) continue;
            if (c.transform.IsChildOf(targetCollider.transform) || targetCollider.transform.IsChildOf(c.transform)) continue;
            SombraController sombra = c.GetComponentInParent<SombraController>();
            if (sombra != null)
            {
                if (sombra.Mode != ShadowMode.WalkableBridge && !sombra.IsPassableAtPoint(hits[i].point, 0.45f)) return false;
                continue;
            }
            if (c.gameObject.name == "Pira" || c.bounds.max.y <= targetCollider.bounds.min.y + 0.35f) continue;
            return false;
        }
        return true;
    }

    private T FindClosestVisible<T>(Collider[] hitColliders, System.Func<T, bool> predicate = null) where T : Component
    {
        T closest = null;
        float minDistance = float.MaxValue;
        foreach (var col in hitColliders)
        {
            if (col == null) continue;
            T comp = col.GetComponent<T>() ?? col.GetComponentInParent<T>();
            if (comp == null || (predicate != null && !predicate(comp))) continue;
            if (!HasInteractionLineOfSight(comp.GetComponent<Collider>() ?? col)) continue;
            float dist = Vector3.Distance(transform.position, comp.transform.position);
            if (dist < minDistance) { minDistance = dist; closest = comp; }
        }
        return closest;
    }

    private PrismSnapSlot FindNearestSnapSlot(PickableObject obj)
    {
        Vector3 checkPos = holdPoint != null ? holdPoint.position : transform.position;
        return PrismSnapSlot.FindClosestAvailableSlot(checkPos, obj)
            ?? PrismSnapSlot.FindClosestAvailableSlot(transform.position, obj);
    }

    private void CheckInteractionProximity()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, interactionRadius, ~0, QueryTriggerInteraction.Collide);
        BlackFireBrazier prevBrazier = nearestBrazier;
        nearestBrazier = FindClosestVisible<BlackFireBrazier>(hitColliders);
        bool canIgniteHeldTorch = HasUnlitTorch();
        if (prevBrazier != null && (prevBrazier != nearestBrazier || !canIgniteHeldTorch)) prevBrazier.HidePrompt();
        if (nearestBrazier != null)
        {
            if (canIgniteHeldTorch) nearestBrazier.ShowPrompt();
            else nearestBrazier.HidePrompt();
        }

        // Procura slot compatível para a mão principal ou secundária
        PrismSnapSlot slotForMain = heldObject != null ? FindNearestSnapSlot(heldObject) : null;
        PrismSnapSlot slotForOff = offHandObject != null ? FindNearestSnapSlot(offHandObject) : null;
        PrismSnapSlot closestSlot = slotForMain ?? slotForOff;
        if (closestSlot != nearestSlot)
        {
            if (nearestSlot != null) nearestSlot.HidePrompt();
            nearestSlot = closestSlot;
            if (nearestSlot != null) nearestSlot.ShowPrompt();
        }

        // Se ambas as mãos estiverem cheias (2 itens segurados), não pega um 3º item
        if (heldObject != null && offHandObject != null)
        {
            if (nearestObject != null) { nearestObject.OnPlayerLeave(); nearestObject = null; }
            return;
        }

        // Se tem pelo menos uma mão livre, procura objetos no chão
        PickableObject closest = FindClosestVisible<PickableObject>(hitColliders, p => !p.IsPickedUp && p != heldObject && p != offHandObject);
        if (closest != nearestObject)
        {
            if (nearestObject != null) nearestObject.OnPlayerLeave();
            nearestObject = closest;
            if (nearestObject != null) nearestObject.OnPlayerApproach();
        }
    }

    private void HandleHeldObjectRotation()
    {
        bool hasTorch = HasTorchEquipped;
        if (heldObject == null && offHandObject == null) return;

        Keyboard keyboard = Keyboard.current;
        float rotateInput = 0f;
        if (keyboard != null)
        {
            rotateInput = (keyboard.rKey.isPressed ? 1f : 0f) - (keyboard.qKey.isPressed ? 1f : 0f);
        }
        Mouse mouse = Mouse.current;
        float mouseScroll = (mouse != null && Mathf.Abs(mouse.scroll.ReadValue().y) > 0.01f)
            ? Mathf.Sign(mouse.scroll.ReadValue().y) : 0f;

        float fineMult = (keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed)) ? 0.35f : 1f;

        if (Mathf.Abs(rotateInput) > 0.01f)
        {
            float delta = rotateInput * heldObjectRotateSpeed * fineMult * Time.deltaTime;
            if (hasTorch)
            {
                // Trava e mantém a direção escolhida no Q/R enquanto segurar a tocha
                torchDirectionLocked = true;
                // Vira o corpo do próprio personagem de lado ao girar com Q/R
                transform.Rotate(Vector3.up, delta, Space.World);
            }
            else
            {
                if (heldObject != null) heldObject.RotateY(delta);
                if (offHandObject != null) offHandObject.RotateY(delta);
            }
        }
        else if (Mathf.Abs(mouseScroll) > 0.01f)
        {
            float delta = mouseScroll * rotationStep;
            if (hasTorch)
            {
                // Trava e mantém a direção escolhida no scroll enquanto segurar a tocha
                torchDirectionLocked = true;
                // Vira o corpo do próprio personagem de lado ao girar com o mouse
                transform.Rotate(Vector3.up, delta, Space.World);
            }
            else
            {
                if (heldObject != null) heldObject.RotateY(delta);
                if (offHandObject != null) offHandObject.RotateY(delta);
            }
        }
    }

    private void HandleInteractionInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        // TECLA [F]: Dropar / Soltar itens segurados (mão principal primeiro)
        if (keyboard.fKey.wasPressedThisFrame)
        {
            if (heldObject != null)
            {
                DropHeldObject();
                return;
            }
            if (offHandObject != null)
            {
                DropOffHandObject();
                return;
            }
        }

        // TECLA [E]: Interagir (Braseiro / Encaixar em Pedestal / Pegar do Chão)
        if (keyboard.eKey.wasPressedThisFrame)
        {
            // 1. Braseiro: acende tochas apagadas seguradas em qualquer uma das mãos
            if (nearestBrazier != null)
            {
                TorchController torchInMain = heldObject != null ? heldObject.GetComponent<TorchController>() : null;
                TorchController torchInOff = offHandObject != null ? offHandObject.GetComponent<TorchController>() : null;
                if (torchInMain != null && !torchInMain.IsLit) { nearestBrazier.TryIgniteTorch(torchInMain); return; }
                if (torchInOff != null && !torchInOff.IsLit) { nearestBrazier.TryIgniteTorch(torchInOff); return; }
            }

            // 2. Se houver um slot próximo e tivermos item compatível para encaixar
            if (nearestSlot != null)
            {
                if (heldObject != null && nearestSlot.CanAccept(heldObject))
                {
                    PickableObject toSnap = heldObject;
                    heldObject = null;
                    toSnap.GetComponent<TorchController>()?.SetHoldingPlayer(null);
                    nearestSlot.SnapObject(toSnap);
                    nearestSlot = null;
                    PromoteOffHandToMain();
                    UpdateHoldPointsPositions();
                    return;
                }
                if (offHandObject != null && nearestSlot.CanAccept(offHandObject))
                {
                    PickableObject toSnap = offHandObject;
                    offHandObject = null;
                    toSnap.GetComponent<TorchController>()?.SetHoldingPlayer(null);
                    nearestSlot.SnapObject(toSnap);
                    nearestSlot = null;
                    UpdateHoldPointsPositions();
                    return;
                }
            }

            // 3. Se houver um objeto próximo no chão e tivermos mão livre, pega o objeto!
            if (nearestObject != null && (heldObject == null || offHandObject == null))
            {
                PickableObject target = nearestObject;
                nearestObject = null;
                target.OnPlayerLeave();

                if (heldObject == null)
                {
                    heldObject = target;
                    UpdateHoldPointsPositions();
                    heldObject.PickUp(holdPoint);
                    if (IsTorch(heldObject)) heldObject.GetComponent<TorchController>()?.SetHoldingPlayer(this);
                }
                else
                {
                    offHandObject = target;
                    UpdateHoldPointsPositions();
                    offHandObject.PickUp(offHandPoint);
                    if (IsTorch(offHandObject)) offHandObject.GetComponent<TorchController>()?.SetHoldingPlayer(this);

                    // Se ambos forem prismas, sincroniza a mira do segundo com o primeiro para girarem juntos
                    if (heldObject.GetComponent<PrismaController>() != null && offHandObject.GetComponent<PrismaController>() != null)
                    {
                        offHandObject.SetHeldWorldYaw(heldObject.HeldWorldYaw);
                    }
                }
                SyncPrismOrientationWhenDualWielding();
                return;
            }
        }
    }

    private void UpdateHoldPointsPositions()
    {
        if (holdPoint == null || offHandPoint == null) return;
        if (heldObject != null && offHandObject != null)
        {
            // Ambas as mãos ocupadas: separa uma na mão direita e outra na mão esquerda
            holdPoint.localPosition = new Vector3(0.40f, 0.40f, 0.95f);
            offHandPoint.localPosition = new Vector3(-0.40f, 0.40f, 0.95f);
        }
        else
        {
            // Apenas 1 item (ou nenhum): mantém o holdPoint centralizado na frente do jogador
            holdPoint.localPosition = new Vector3(0f, 0.40f, 1.05f);
            offHandPoint.localPosition = new Vector3(-0.40f, 0.40f, 0.95f);
        }
    }

    private void PromoteOffHandToMain()
    {
        if (offHandObject != null && heldObject == null)
        {
            heldObject = offHandObject;
            offHandObject = null;
            UpdateHoldPointsPositions();
            heldObject.PickUp(holdPoint);
        }
    }

    private void DropHeldObject()
    {
        if (heldObject == null) return;
        heldObject.GetComponent<TorchController>()?.SetHoldingPlayer(null);
        SafelyPositionForDrop(heldObject, holdPoint != null ? holdPoint.position : heldObject.transform.position);
        heldObject.Drop(dropForwardForce > 0.001f ? transform.forward * dropForwardForce : Vector3.zero);
        heldObject = null;
        PromoteOffHandToMain();
        UpdateHoldPointsPositions();
        if (!HasTorchEquipped) torchDirectionLocked = false;
    }

    private void DropOffHandObject()
    {
        if (offHandObject == null) return;
        offHandObject.GetComponent<TorchController>()?.SetHoldingPlayer(null);
        SafelyPositionForDrop(offHandObject, offHandPoint != null ? offHandPoint.position : offHandObject.transform.position);
        offHandObject.Drop(Vector3.zero);
        offHandObject = null;
        UpdateHoldPointsPositions();
        if (!HasTorchEquipped) torchDirectionLocked = false;
    }

    private void SafelyPositionForDrop(PickableObject obj, Vector3 dest)
    {
        if (obj == null) return;
        Vector3 origin = transform.position + Vector3.up * 0.4f;
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
                if (c == null || c.transform.IsChildOf(transform) || c.transform.IsChildOf(obj.transform)) continue;
                SombraController sombra = c.GetComponentInParent<SombraController>();
                if (sombra != null && (sombra.Mode == ShadowMode.WalkableBridge || (sombra.Mode == ShadowMode.Barrier && sombra.IsPassableAtPoint(hits[i].point, 0.45f)))) continue;
                obj.transform.SetParent(null);
                obj.transform.position = origin + dir * Mathf.Max(0.15f, hits[i].distance - 0.35f);
                break;
            }
        }
    }

    private void OnDrawGizmosSelected() { Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, interactionRadius); }
}
