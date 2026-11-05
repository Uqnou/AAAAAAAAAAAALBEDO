using UnityEngine;

/// <summary>
/// Indicador visual flutuante com a tecla [E] exibido acima do objeto interagível.
/// </summary>
public class InteractionPrompt : MonoBehaviour
{
    [Header("Configuração de Exibição")]
    [SerializeField] private float heightOffset = 0.8f;
    [SerializeField] private Vector3 billboardScale = new Vector3(0.3f, 0.3f, 0.3f);

    private TextMesh textMesh;
    private GameObject promptHolder;
    private bool isVisible = false;

    private void Awake()
    {
        // Cria dinamicamente o objeto visual com TextMesh para não depender obrigatoriamente de UI Canvas complexa
        promptHolder = new GameObject("Prompt_E");
        promptHolder.transform.SetParent(transform);
        promptHolder.transform.localPosition = new Vector3(0, heightOffset, 0);
        promptHolder.transform.localScale = billboardScale;

        textMesh = promptHolder.AddComponent<TextMesh>();
        textMesh.text = "[E]";
        textMesh.fontSize = 48;
        textMesh.alignment = TextAlignment.Center;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.color = Color.yellow;

        promptHolder.SetActive(false);
    }

    private void LateUpdate()
    {
        if (isVisible && Camera.main != null)
        {
            // Sempre voltado de frente para a câmera isométrica
            promptHolder.transform.rotation = Camera.main.transform.rotation;
        }
    }

    public void Show()
    {
        isVisible = true;
        if (promptHolder != null)
            promptHolder.SetActive(true);
    }

    public void Hide()
    {
        isVisible = false;
        if (promptHolder != null)
            promptHolder.SetActive(false);
    }
}
