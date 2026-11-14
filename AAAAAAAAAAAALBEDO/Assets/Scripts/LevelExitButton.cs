using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Botão / plataforma final do nível. Dispara o evento de conclusão quando o jogador pisa sobre ele.
/// </summary>
[SelectionBase]
[DisallowMultipleComponent]
public class LevelExitButton : MonoBehaviour
{
    [Header("Conclusão do Nível")]
    [Tooltip("Tag do jogador (também detecta PlayerController na hierarquia).")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("Impede reativação após o nível já ter sido concluído.")]
    [SerializeField] private bool triggerOnce = true;

    [Tooltip("Se verdadeiro e houver próxima cena no Build Settings, carrega automaticamente após o atraso.")]
    [SerializeField] private bool loadNextSceneInBuild = false;

    [SerializeField] private float nextSceneDelay = 0.75f;

    [Header("Eventos")]
    public UnityEvent onLevelCompleted = new UnityEvent();

    private bool isCompleted = false;

    public bool IsCompleted => isCompleted;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggerOnce && isCompleted) return;
        if (!IsPlayer(other)) return;

        ActivateExit();
    }

    public void ActivateExit()
    {
        if (triggerOnce && isCompleted) return;

        isCompleted = true;
        Debug.Log($"[ALBEDO] Nível concluído em '{gameObject.name}'!");
        onLevelCompleted?.Invoke();

        if (loadNextSceneInBuild)
        {
            Invoke(nameof(LoadNextScene), nextSceneDelay);
        }
    }

    private void LoadNextScene()
    {
        int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;
        if (nextIndex < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(nextIndex);
        }
    }

    private bool IsPlayer(Collider other)
    {
        if (other == null) return false;
        if (!string.IsNullOrEmpty(playerTag) && other.CompareTag(playerTag))
        {
            return true;
        }
        return other.GetComponentInParent<PlayerController>() != null;
    }
}
