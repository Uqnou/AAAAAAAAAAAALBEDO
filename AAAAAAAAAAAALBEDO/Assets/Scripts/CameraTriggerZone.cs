using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gatilho que altera os parâmetros da IsometricCamera quando o Player entra ou sai da área.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CameraTriggerZone : MonoBehaviour
{
    private static readonly List<CameraTriggerZone> activeZones = new List<CameraTriggerZone>();

    [Header("Identificação do Player")]
    [Tooltip("Tag para identificar o jogador. Deixe vazio para detectar pelo PlayerController.")]
    [SerializeField] private string playerTag = "Player";

    [Header("Configurações Alvo da Câmera")]
    [Tooltip("Ângulo de inclinação vertical (Pitch)")]
    [SerializeField] private float targetPitchAngle = 30f;

    [Tooltip("Ângulo horizontal (Yaw)")]
    [SerializeField] private float targetYawAngle = 45f;

    [Tooltip("Tamanho do enquadramento ortográfico")]
    [SerializeField] private float targetOrthographicSize = 8f;

    [Tooltip("Transform opcional como foco central do enquadramento. Se não definido, usa targetLookPosition.")]
    [SerializeField] private Transform targetFocusTransform;

    [Tooltip("Posição central do cenário para onde a câmera apontará.")]
    [SerializeField] private Vector3 targetLookPosition = Vector3.zero;

    [Tooltip("Distância da câmera até o centro do cenário.")]
    [SerializeField] private float targetCameraDistance = 25f;

    [Header("Transição")]
    [Tooltip("Tempo em segundos para transição suave.")]
    [SerializeField] private float transitionDuration = 1.2f;

    [Tooltip("Curva de interpolação personalizada (opcional).")]
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Comportamento ao Sair")]
    [Tooltip("Se verdadeiro, restaura as configurações da câmera padrão/Sala1 ao sair da zona.")]
    [SerializeField] private bool revertOnExit = false;

    [Tooltip("Tempo de transição ao restaurar na saída.")]
    [SerializeField] private float exitTransitionDuration = 1.2f;

    private readonly HashSet<Collider> playerCollidersInside = new HashSet<Collider>();

    public float TargetPitchAngle => targetPitchAngle;
    public float TargetYawAngle => targetYawAngle;
    public float TargetOrthographicSize => targetOrthographicSize;
    public Vector3 TargetLookPosition => targetFocusTransform != null ? targetFocusTransform.position : targetLookPosition;
    public float TargetCameraDistance => targetCameraDistance;
    public bool IsSala1Zone => string.Equals(gameObject.name, "Sala1", System.StringComparison.OrdinalIgnoreCase);

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            col.isTrigger = true;
        }
    }

    private void OnDisable()
    {
        playerCollidersInside.Clear();
        activeZones.Remove(this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;

        playerCollidersInside.Add(other);
        if (playerCollidersInside.Count > 1) return;

        activeZones.Remove(this);
        activeZones.Add(this);
        ApplyZoneCamera(transitionDuration);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other)) return;

        playerCollidersInside.Remove(other);
        playerCollidersInside.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
        if (playerCollidersInside.Count > 0) return;

        activeZones.Remove(this);
        if (IsSala1Zone) return;

        bool isTutorialScene = gameObject.scene.IsValid()
            && gameObject.scene.name.IndexOf("Tutorial", System.StringComparison.OrdinalIgnoreCase) >= 0;
        if (!revertOnExit && !isTutorialScene) return;

        IsometricCamera isoCam = GetIsometricCamera();
        if (isoCam == null) return;

        activeZones.RemoveAll(z => z == null || !z.isActiveAndEnabled || z.gameObject.scene != gameObject.scene);
        for (int i = activeZones.Count - 1; i >= 0; i--)
        {
            CameraTriggerZone fallbackZone = activeZones[i];
            if (fallbackZone != null && !fallbackZone.IsSala1Zone)
            {
                fallbackZone.ApplyZoneCamera(exitTransitionDuration);
                return;
            }
        }

        isoCam.ResetToDefaultCameraSettings(exitTransitionDuration, transitionCurve);
    }

    public void ApplyZoneCamera(float duration)
    {
        IsometricCamera isoCam = GetIsometricCamera();
        if (isoCam == null) return;

        isoCam.SetCameraSettings(
            targetPitchAngle,
            targetYawAngle,
            targetOrthographicSize,
            TargetLookPosition,
            targetCameraDistance,
            duration,
            transitionCurve);
    }

    private static IsometricCamera GetIsometricCamera()
    {
        return IsometricCamera.Instance != null ? IsometricCamera.Instance : FindFirstObjectByType<IsometricCamera>();
    }

    private bool IsPlayer(Collider other)
    {
        if (other == null) return false;
        if (other.GetComponentInParent<PickableObject>() != null) return false;
        if (other.GetComponent<PlayerController>() != null) return true;
        if (!string.IsNullOrEmpty(playerTag) && other.CompareTag(playerTag)) return true;

        PlayerController pc = other.GetComponentInParent<PlayerController>();
        return pc != null && other.transform == pc.transform;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        Collider col = GetComponent<Collider>();
        if (col is BoxCollider box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
