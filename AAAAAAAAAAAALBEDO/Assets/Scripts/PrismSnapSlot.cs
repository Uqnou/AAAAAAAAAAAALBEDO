using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Define uma posição e direção específica na mesa onde qualquer prisma pode grudar com [E].
/// </summary>
[ExecuteAlways, SelectionBase]
public class PrismSnapSlot : MonoBehaviour
{
    public static readonly List<PrismSnapSlot> AllSlots = new List<PrismSnapSlot>();

    [Header("Posição e Direção de Encaixe")]
    [Tooltip("Transform opcional que define a posição/rotação exatas. Se vazio, usa este próprio objeto + offsets.")]
    [SerializeField] private Transform snapPoint;
    [Tooltip("Deslocamento local da posição onde o prisma vai grudar.")]
    [SerializeField] private Vector3 positionOffset = Vector3.zero;
    [Tooltip("Rotação em graus (Euler) para onde o feixe do prisma deve apontar (ex: Y = 180 aponta para -Z).")]
    [SerializeField] private Vector3 rotationEulerOffset = Vector3.zero;
    [Tooltip("Se verdadeiro, usa rotationEulerOffset como rotação global absoluta em vez de somar à rotação deste objeto.")]
    [SerializeField] private bool useAbsoluteWorldRotation = false;
    [Tooltip("Preserva a inclinação original da malha FBX do prisma em pé (recomendado para prismas do Blender com X=90°), aplicando apenas a direção horizontal (Yaw) do slot.")]
    [SerializeField] private bool preservePrismUprightTilt = true;

    [Header("Alcance e Regras")]
    [Tooltip("Distância máxima para encaixar o prisma ao apertar [E].")]
    [SerializeField] private float snapRadius = 2.6f;
    [Tooltip("Se verdadeiro, encaixa apenas objetos que possuem PrismaController.")]
    [SerializeField] private bool onlyPrisms = true;
    [Tooltip("Se verdadeiro, encaixa automaticamente quando o prisma é solto dentro do raio.")]
    [SerializeField] private bool autoSnapOnDropNearby = true;

    [Header("Indicador Visual")]
    [SerializeField] private bool showPromptWhenHolding = true;
    [SerializeField] private string promptText = "[E] Encaixar";
    [SerializeField] private float promptHeightOffset = 0.85f;
    [SerializeField] private Vector3 promptScale = new Vector3(0.25f, 0.25f, 0.25f);

    private InteractionPrompt interactionPrompt;

    public PickableObject OccupiedObject { get; private set; }
    public bool IsOccupied => OccupiedObject != null && !OccupiedObject.IsPickedUp && OccupiedObject.CurrentSlot == this;
    public float SnapRadius => snapRadius;
    public bool AutoSnapOnDropNearby => autoSnapOnDropNearby;
    public bool PreservePrismUprightTilt => preservePrismUprightTilt;

    private void OnEnable()
    {
        if (!AllSlots.Contains(this)) AllSlots.Add(this);
    }

    private void Start()
    {
        if (!Application.isPlaying || IsOccupied) return;
        Vector3 pos = GetTargetPosition();
        foreach (var col in Physics.OverlapSphere(pos, 0.4f, ~0, QueryTriggerInteraction.Collide))
        {
            PickableObject pick = col != null ? (col.GetComponent<PickableObject>() ?? col.GetComponentInParent<PickableObject>()) : null;
            if (pick != null && !pick.IsPickedUp && pick.CurrentSlot == null && CanAccept(pick))
            {
                SnapObject(pick);
                break;
            }
        }
    }

    private void OnDisable()
    {
        AllSlots.Remove(this);
        HidePrompt();
    }

    private void OnDestroy()
    {
        AllSlots.Remove(this);
    }

    public bool CanAccept(PickableObject obj) =>
        obj != null && !IsOccupied &&
        (!onlyPrisms || obj.GetComponent<PrismaController>() != null || obj.name.StartsWith("Prisma", System.StringComparison.OrdinalIgnoreCase));

    public Vector3 GetTargetPosition()
    {
        Transform reference = snapPoint != null ? snapPoint : transform;
        return reference.position + reference.TransformVector(positionOffset);
    }

    public Quaternion GetTargetRotation()
    {
        if (useAbsoluteWorldRotation) return Quaternion.Euler(rotationEulerOffset);
        Transform reference = snapPoint != null ? snapPoint : transform;
        return reference.rotation * Quaternion.Euler(rotationEulerOffset);
    }

    public float GetTargetYaw() => GetTargetRotation().eulerAngles.y;

    public static PrismSnapSlot FindClosestAvailableSlot(Vector3 referencePosition, PickableObject candidate = null, float maxRangeOverride = -1f)
    {
        PrismSnapSlot closest = null;
        float minDistance = float.MaxValue;
        for (int i = AllSlots.Count - 1; i >= 0; i--)
        {
            PrismSnapSlot slot = AllSlots[i];
            if (slot == null) { AllSlots.RemoveAt(i); continue; }
            if (!slot.gameObject.activeInHierarchy || !slot.enabled || slot.IsOccupied) continue;
            if (candidate != null && !slot.CanAccept(candidate)) continue;

            float allowedRange = maxRangeOverride > 0f ? maxRangeOverride : slot.SnapRadius;
            float dist = Vector3.Distance(referencePosition, slot.GetTargetPosition());
            if (dist <= allowedRange && dist < minDistance)
            {
                minDistance = dist;
                closest = slot;
            }
        }
        return closest;
    }

    public void SnapObject(PickableObject obj)
    {
        if (obj == null) return;
        if (obj.CurrentSlot != null && obj.CurrentSlot != this) obj.CurrentSlot.ReleaseObject(obj);
        OccupiedObject = obj;
        HidePrompt();
        obj.SnapToSlot(this, GetTargetPosition(), GetTargetRotation(), preservePrismUprightTilt);
    }

    public void ReleaseObject(PickableObject obj)
    {
        if (OccupiedObject == obj) OccupiedObject = null;
    }

    public void ShowPrompt()
    {
        if (!Application.isPlaying || !showPromptWhenHolding || IsOccupied) return;
        if (interactionPrompt == null)
        {
            interactionPrompt = GetComponent<InteractionPrompt>() ?? gameObject.AddComponent<InteractionPrompt>();
        }
        string cleanAction = string.IsNullOrEmpty(promptText) ? "Encaixar" : promptText.Replace("[E]", "").Trim();
        interactionPrompt.Configure("E", string.IsNullOrEmpty(cleanAction) ? "Encaixar" : cleanAction, new Color(0.25f, 0.92f, 1f, 1f), true, promptHeightOffset);
        interactionPrompt.SetWorldAnchorOverride(GetTargetPosition());
        interactionPrompt.Show();
    }

    public void HidePrompt()
    {
        if (interactionPrompt != null) interactionPrompt.Hide();
    }

    private void OnDrawGizmos()
    {
        Vector3 pos = GetTargetPosition();
        if (Vector3.Distance(transform.position, pos) > 0.01f)
        {
            Gizmos.color = new Color(0.5f, 0.9f, 1f, 0.45f);
            Gizmos.DrawLine(transform.position, pos);
        }

        Gizmos.color = IsOccupied ? new Color(1f, 0.65f, 0.1f, 0.95f) : new Color(0.1f, 0.95f, 1f, 0.95f);
        Gizmos.DrawWireSphere(pos, 0.35f);

        Vector3 forward = Quaternion.Euler(0f, GetTargetYaw(), 0f) * Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 tip = pos + forward * 1.8f;
        Gizmos.DrawLine(pos, tip);
        Gizmos.DrawLine(tip, tip - forward * 0.35f + right * 0.2f);
        Gizmos.DrawLine(tip, tip - forward * 0.35f - right * 0.2f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.5f, 0.4f);
        Gizmos.DrawWireSphere(GetTargetPosition(), snapRadius);
    }
}
