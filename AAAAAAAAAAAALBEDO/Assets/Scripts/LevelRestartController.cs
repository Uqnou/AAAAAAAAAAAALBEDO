using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Reinicia a fase atual ao pressionar a tecla [P] usando o Input System.
/// </summary>
public sealed class LevelRestartController : MonoBehaviour
{
    private const float RestartCooldownSeconds = 0.35f;
    private static float lastRestartUnscaledTime = -10f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRunner()
    {
        if (Object.FindAnyObjectByType<LevelRestartController>() != null) return;
        GameObject go = new GameObject("[ALBEDO_LevelRestart]") { hideFlags = HideFlags.HideAndDontSave };
        Object.DontDestroyOnLoad(go);
        go.AddComponent<LevelRestartController>();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.pKey.wasPressedThisFrame) return;
        if (Time.unscaledTime - lastRestartUnscaledTime < RestartCooldownSeconds) return;

        lastRestartUnscaledTime = Time.unscaledTime;
        Time.timeScale = 1f;

        Scene active = SceneManager.GetActiveScene();
        if (active.buildIndex >= 0) SceneManager.LoadScene(active.buildIndex);
        else if (!string.IsNullOrEmpty(active.name)) SceneManager.LoadScene(active.name);
    }
}
