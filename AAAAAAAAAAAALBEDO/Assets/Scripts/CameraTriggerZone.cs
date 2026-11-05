using UnityEngine;

/// <summary>
/// Gatilho que altera os parâmetros da IsometricCamera quando o Player entra ou sai da área.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CameraTriggerZone : MonoBehaviour
{
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
    [Tooltip("Se verdadeiro, restaura as configurações que a câmera possuía antes de entrar na zona.")]
    [SerializeField] private bool revertOnExit = false;

    [Tooltip("Tempo de transição ao restaurar na saída.")]
    [SerializeField] private float exitTransitionDuration = 1.2f;

    // Estado prévio salvo para restauração
    private float prevPitch;
    private float prevYaw;
    private float prevSize;
    private Vector3 prevLook;
    private float prevDist;
    private bool hasSavedState = false;

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

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;

        IsometricCamera isoCam = IsometricCamera.Instance != null ? IsometricCamera.Instance : FindFirstObjectByType<IsometricCamera>();
        if (isoCam == null) return;

        // Salva estado para restauração futura
        if (revertOnExit && !hasSavedState)
        {
            prevPitch = isoCam.PitchAngle;
            prevYaw = isoCam.YawAngle;
            prevSize = isoCam.OrthographicSize;
            prevLook = isoCam.LookTarget;
            prevDist = isoCam.CameraDistance;
            hasSavedState = true;
        }

        Vector3 targetPos = targetFocusTransform != null ? targetFocusTransform.position : targetLookPosition;
        isoCam.SetCameraSettings(targetPitchAngle, targetYawAngle, targetOrthographicSize, targetPos, targetCameraDistance, transitionDuration, transitionCurve);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!revertOnExit || !hasSavedState) return;
        if (!IsPlayer(other)) return;

        IsometricCamera isoCam = IsometricCamera.Instance != null ? IsometricCamera.Instance : FindFirstObjectByType<IsometricCamera>();
        if (isoCam == null) return;

        isoCam.SetCameraSettings(prevPitch, prevYaw, prevSize, prevLook, prevDist, exitTransitionDuration, transitionCurve);
        hasSavedState = false;
    }

    private bool IsPlayer(Collider other)
    {
        if (!string.IsNullOrEmpty(playerTag) && other.CompareTag(playerTag))
        {
            return true;
        }

        return other.GetComponentInParent<PlayerController>() != null;
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
