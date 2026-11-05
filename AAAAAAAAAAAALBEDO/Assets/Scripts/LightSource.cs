using UnityEngine;

/// <summary>
/// Representa uma fonte ou volume de luz no cenário.
/// Deve ser adicionado MANUALMENTE aos objetos que emitem luz no jogo.
/// </summary>
[SelectionBase]
[DisallowMultipleComponent]
public class LightSource : MonoBehaviour
{
    [Header("Configuração de Luz")]
    [Tooltip("Cor da luz emitida por esta fonte.")]
    [SerializeField] private Color lightColor = Color.white;

    [Tooltip("Sincroniza automaticamente a cor com o componente Light da Unity, se existir.")]
    [SerializeField] private bool syncWithUnityLight = true;

    [Tooltip("Garante que o Collider deste objeto seja um Trigger para que o prisma possa entrar em contato.")]
    [SerializeField] private bool ensureTriggerCollider = true;

    private Collider col;
    private Light unityLight;

    public Color LightColor => lightColor;

    private void Awake()
    {
        Initialize();
    }

    private void OnValidate()
    {
        Initialize();
    }

    private void Initialize()
    {
        col = GetComponent<Collider>();
        if (col != null && ensureTriggerCollider)
        {
            col.isTrigger = true;
        }

        if (syncWithUnityLight)
        {
            unityLight = GetComponent<Light>();
            if (unityLight != null)
            {
                lightColor = unityLight.color;
            }
        }
    }

    public void SetLightColor(Color newColor)
    {
        lightColor = newColor;
        if (unityLight != null)
        {
            unityLight.color = newColor;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(lightColor.r, lightColor.g, lightColor.b, 0.45f);
        Collider c = GetComponent<Collider>();
        if (c != null)
        {
            Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
}
