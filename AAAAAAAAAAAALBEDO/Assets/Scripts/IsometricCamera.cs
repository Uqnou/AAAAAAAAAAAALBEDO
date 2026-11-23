using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Configura e gerencia a Main Camera em projeção isométrica ortográfica.
/// Suporta transição suave de parâmetros quando disparado por triggers de zona.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class IsometricCamera : MonoBehaviour
{
    public static IsometricCamera Instance { get; private set; }

    [Header("Configuração Isométrica Atual")]
    [Tooltip("Ângulo de inclinação vertical (Pitch)")]
    [SerializeField] private float pitchAngle = 30f; // 30° clássico ou 35.264° para isometria matemática pura

    [Tooltip("Ângulo horizontal (Yaw)")]
    [SerializeField] private float yawAngle = 45f;

    [Tooltip("Tamanho do enquadramento ortográfico")]
    [SerializeField] private float orthographicSize = 8f;

    [Tooltip("Posição central do cenário para onde a câmera está apontada")]
    [SerializeField] private Vector3 lookTarget = Vector3.zero;

    [Tooltip("Distância da câmera até o centro do cenário")]
    [SerializeField] private float cameraDistance = 25f;

    [Header("Transição de Nível (Camera Distance)")]
    [Tooltip("Distância da câmera no início e término do nível.")]
    [SerializeField] private float transitionDistance = -72f;

    [Tooltip("Duração da transição ao iniciar a fase (indo de -72 para a distância normal).")]
    [SerializeField] private float introDuration = 1.4f;

    [Tooltip("Duração da transição ao concluir a fase (indo da distância normal para -72).")]
    [SerializeField] private float outroDuration = 1.4f;

    [SerializeField] private AnimationCurve levelTransitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Camera cam;
    private Coroutine transitionCoroutine;
    private float configuredNormalDistance;

    // Propriedades de leitura
    public float PitchAngle => pitchAngle;
    public float YawAngle => yawAngle;
    public float OrthographicSize => orthographicSize;
    public Vector3 LookTarget => lookTarget;
    public float CameraDistance => cameraDistance;
    public float ConfiguredNormalDistance => configuredNormalDistance;
    public float TransitionDistance => transitionDistance;

    private void Awake()
    {
        if (Application.isPlaying)
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        cam = GetComponent<Camera>();
        configuredNormalDistance = cameraDistance;
        ApplyHDQualitySettings();
        ApplyIsometricSettings();
    }

    private void Start()
    {
        if (Application.isPlaying)
        {
            PlayIntroTransition();
        }
    }

    public void PlayIntroTransition()
    {
        cameraDistance = transitionDistance;
        ApplyIsometricSettings();
        if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
        transitionCoroutine = StartCoroutine(TransitionDistanceRoutine(transitionDistance, configuredNormalDistance, introDuration, null));
    }

    public void PlayOutroTransition(System.Action onComplete = null)
    {
        if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
        transitionCoroutine = StartCoroutine(TransitionDistanceRoutine(cameraDistance, transitionDistance, outroDuration, onComplete));
    }

    private IEnumerator TransitionDistanceRoutine(float startDist, float targetDist, float duration, System.Action onComplete)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float evaluatedT = levelTransitionCurve != null ? levelTransitionCurve.Evaluate(t) : Mathf.SmoothStep(0f, 1f, t);
            cameraDistance = Mathf.Lerp(startDist, targetDist, evaluatedT);
            ApplyIsometricSettings();
            yield return null;
        }

        cameraDistance = targetDist;
        ApplyIsometricSettings();
        transitionCoroutine = null;
        onComplete?.Invoke();
    }

    private void ApplyHDQualitySettings()
    {
        if (cam == null) return;
        cam.allowMSAA = true;
        cam.allowDynamicResolution = false;

        var urpCam = GetComponent<UniversalAdditionalCameraData>();
        if (urpCam != null)
        {
            // Usa None para nao borrar o padrao de halftone das paredes; a suavizacao vem do MSAA de hardware (igual a Scene View)
            urpCam.antialiasing = AntialiasingMode.None;
        }

        if (Application.isPlaying)
        {
            QualitySettings.antiAliasing = 8;
            if (UniversalRenderPipeline.asset != null)
            {
                if (UniversalRenderPipeline.asset.renderScale < 1f)
                    UniversalRenderPipeline.asset.renderScale = 1f;
                if (UniversalRenderPipeline.asset.msaaSampleCount < 8)
                    UniversalRenderPipeline.asset.msaaSampleCount = 8;
            }
        }
    }

    private void OnValidate()
    {
        cam = GetComponent<Camera>();
        ApplyHDQualitySettings();
        ApplyIsometricSettings();
    }

    /// <summary>
    /// Aplica instantaneamente os parâmetros calculados na câmera.
    /// </summary>
    public void ApplyIsometricSettings()
    {
        if (cam == null) cam = GetComponent<Camera>();

        cam.orthographic = true;
        cam.orthographicSize = orthographicSize;

        Quaternion isoRotation = Quaternion.Euler(pitchAngle, yawAngle, 0f);
        transform.rotation = isoRotation;

        Vector3 offset = isoRotation * new Vector3(0, 0, -cameraDistance);
        transform.position = lookTarget + offset;
    }

    /// <summary>
    /// Altera as configurações da câmera, com opção de transição suave (interpolação).
    /// </summary>
    public void SetCameraSettings(float newPitch, float newYaw, float newSize, Vector3 newTarget, float newDistance, float duration = 1.0f, AnimationCurve curve = null)
    {
        if (!Application.isPlaying || duration <= 0f)
        {
            pitchAngle = newPitch;
            yawAngle = newYaw;
            orthographicSize = newSize;
            lookTarget = newTarget;
            cameraDistance = newDistance;
            ApplyIsometricSettings();
            return;
        }

        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
        }

        transitionCoroutine = StartCoroutine(TransitionRoutine(newPitch, newYaw, newSize, newTarget, newDistance, duration, curve));
    }

    private IEnumerator TransitionRoutine(float targetPitch, float targetYaw, float targetSize, Vector3 targetLook, float targetDist, float duration, AnimationCurve curve)
    {
        float startPitch = pitchAngle;
        float startYaw = yawAngle;
        float startSize = orthographicSize;
        Vector3 startLook = lookTarget;
        float startDist = cameraDistance;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float evaluatedT = curve != null ? curve.Evaluate(t) : Mathf.SmoothStep(0f, 1f, t);

            pitchAngle = Mathf.Lerp(startPitch, targetPitch, evaluatedT);
            yawAngle = Mathf.LerpAngle(startYaw, targetYaw, evaluatedT);
            orthographicSize = Mathf.Lerp(startSize, targetSize, evaluatedT);
            lookTarget = Vector3.Lerp(startLook, targetLook, evaluatedT);
            cameraDistance = Mathf.Lerp(startDist, targetDist, evaluatedT);

            ApplyIsometricSettings();
            yield return null;
        }

        pitchAngle = targetPitch;
        yawAngle = targetYaw;
        orthographicSize = targetSize;
        lookTarget = targetLook;
        cameraDistance = targetDist;
        ApplyIsometricSettings();
        transitionCoroutine = null;
    }
}
