using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Modo óptico do Vitral / Divisor Cromático.
/// - SingleFilter: filtra o feixe incidente multiplicando seus canais RGB pelo filtro configurado.
/// - DualSplitter: divide o feixe incidente em até 2 saídas filtradas com direções independentes.
/// </summary>
public enum ChromaticGateMode
{
    [Tooltip("Filtra o feixe que atravessa o vitral mantendo apenas os canais RGB permitidos pelo filtro.")]
    SingleFilter = 0,

    [Tooltip("Divide o feixe incidente em até 2 feixes com filtros e ângulos horizontais independentes.")]
    DualSplitter = 1
}

/// <summary>
/// Barreira física que bloqueia o jogador e prismas carregados, mas permite a passagem da luz
/// aplicando filtragem subtrativa RGB e/ou divisão de feixe em duas direções.
/// </summary>
[ExecuteAlways, SelectionBase, DisallowMultipleComponent]
public class ChromaticFilterGate : MonoBehaviour
{
    [Header("Modo do Vitral / Divisor")]
    [SerializeField] private ChromaticGateMode gateMode = ChromaticGateMode.SingleFilter;

    [Header("Saída A (Principal / Transmissão)")]
    [SerializeField] private AlbedoColorPreset filterPresetA = AlbedoColorPreset.Green;
    [SerializeField] private Color customFilterColorA = Color.green;
    [Tooltip("Deslocamento angular horizontal (graus) da Saída A.")]
    [Range(-180f, 180f)] [SerializeField] private float yawOffsetA = 0f;
    [SerializeField] private Transform customExitPointA;

    [Header("Saída B (Secundária / Divisão)")]
    [SerializeField] private AlbedoColorPreset filterPresetB = AlbedoColorPreset.Red;
    [SerializeField] private Color customFilterColorB = Color.red;
    [Tooltip("Deslocamento angular horizontal (graus) da Saída B no modo DualSplitter.")]
    [Range(-180f, 180f)] [SerializeField] private float yawOffsetB = 90f;
    [SerializeField] private Transform customExitPointB;

    [Header("Comportamento Óptico")]
    [Tooltip("Se ativo, a direção base parte do feixe incidente; se desativado, usa o eixo frontal do vitral.")]
    [SerializeField] private bool alignToIncomingBeam = true;
    [Tooltip("Distância após o centro do vitral onde o feixe filtrado nasce para não colidir com a própria face.")]
    [SerializeField] private float exitThicknessOffset = 0.45f;
    [SerializeField] private bool syncVisualMaterialTint = true;
    [SerializeField] private int visualMaterialSlot = 0;

    private readonly Dictionary<PrismaController, Vector3> incomingBeamDirs = new Dictionary<PrismaController, Vector3>();
    private readonly List<PrismaController> toRemoveBuffer = new List<PrismaController>();
    private readonly List<Color> colorBuffer = new List<Color>();
    private PrismaController emitterA;
    private PrismaController emitterB;
    private MaterialPropertyBlock cachedTintBlock;

    public ChromaticGateMode GateMode { get => gateMode; set { gateMode = value; SyncVisualTint(); } }
    public AlbedoColorPreset FilterPresetA { get => filterPresetA; set { filterPresetA = value; SyncVisualTint(); } }
    public AlbedoColorPreset FilterPresetB { get => filterPresetB; set { filterPresetB = value; SyncVisualTint(); } }
    public float YawOffsetA { get => yawOffsetA; set => yawOffsetA = value; }
    public float YawOffsetB { get => yawOffsetB; set => yawOffsetB = value; }
    public bool AlignToIncomingBeam { get => alignToIncomingBeam; set => alignToIncomingBeam = value; }
    public PrismaController EmitterA => emitterA;
    public PrismaController EmitterB => emitterB;
    public Color EffectiveFilterColorA => AlbedoColorUtility.PresetToColor(filterPresetA, customFilterColorA);
    public Color EffectiveFilterColorB => AlbedoColorUtility.PresetToColor(filterPresetB, customFilterColorB);

    private void OnEnable()
    {
        AlbedoWallLightGlobals.RegisterGate(this);
        SyncVisualTint();
    }

    private void OnValidate() => SyncVisualTint();

    private void OnDisable()
    {
        AlbedoWallLightGlobals.UnregisterGate(this);
        incomingBeamDirs.Clear();
        DisableEmitter(emitterA);
        DisableEmitter(emitterB);
    }

    public void SyncVisualTint()
    {
        if (!syncVisualMaterialTint) return;
        Renderer r = FindVisualRenderer();
        if (r == null || r.sharedMaterials == null || r.sharedMaterials.Length == 0) return;
        int idx = Mathf.Clamp(visualMaterialSlot, 0, r.sharedMaterials.Length - 1);
        Color colA = EffectiveFilterColorA;
        AlbedoColorUtility.ApplyMaterialIndicator(r, idx, colA, true);
        if (cachedTintBlock == null) cachedTintBlock = new MaterialPropertyBlock();
        r.GetPropertyBlock(cachedTintBlock, idx);
        cachedTintBlock.SetColor("_SecondaryColor", EffectiveFilterColorB);
        cachedTintBlock.SetFloat("_UseDualColor", gateMode == ChromaticGateMode.DualSplitter ? 1f : 0f);
        r.SetPropertyBlock(cachedTintBlock, idx);
    }

    private Renderer FindVisualRenderer()
    {
        Renderer own = GetComponent<Renderer>();
        if (own != null && IsGlassMaterial(own)) return own;
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null && IsGlassMaterial(renderers[i])) return renderers[i];
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null && renderers[i].name.IndexOf("Vidro", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return renderers[i];
        return own != null ? own : (renderers.Length > 0 ? renderers[0] : null);
    }

    private bool IsGlassMaterial(Renderer r)
    {
        if (r.sharedMaterials == null || r.sharedMaterials.Length == 0) return false;
        int idx = Mathf.Clamp(visualMaterialSlot, 0, r.sharedMaterials.Length - 1);
        Material m = r.sharedMaterials[idx];
        if (m == null || m.shader == null) return false;
        string sn = m.shader.name;
        return sn == "ALBEDO/StainedGlassVitral"
            || sn.IndexOf("Glass", System.StringComparison.OrdinalIgnoreCase) >= 0
            || sn.IndexOf("Vitral", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public bool OwnsEmitter(PrismaController p) => p != null && (p == emitterA || p == emitterB);

    public bool HasUpstreamSource(PrismaController candidate, int depth = 8)
    {
        if (depth <= 0 || candidate == null) return false;
        if (OwnsEmitter(candidate) || incomingBeamDirs.ContainsKey(candidate)) return true;
        foreach (var src in incomingBeamDirs.Keys)
            if (src != null && src.HasUpstreamSource(candidate, depth - 1)) return true;
        return false;
    }

    public void ReceivePrismBeam(bool illuminated, PrismaController source, Vector3 incomingDir)
    {
        if (source == null || OwnsEmitter(source)) return;
        if (illuminated)
        {
            if ((emitterA != null && source.HasUpstreamSource(emitterA, 8)) ||
                (emitterB != null && source.HasUpstreamSource(emitterB, 8))) return;
            Vector3 flat = new Vector3(incomingDir.x, 0f, incomingDir.z);
            incomingBeamDirs[source] = flat.sqrMagnitude > 0.0001f ? flat.normalized : GetFlatTransformForward();
        }
        else
        {
            incomingBeamDirs.Remove(source);
        }
    }

    private void Update()
    {
        if (Application.isPlaying)
        {
            EvaluateGate();
        }
        else if (syncVisualMaterialTint)
        {
            Renderer r = FindVisualRenderer();
            if (r != null && r.sharedMaterials != null && r.sharedMaterials.Length > 0)
            {
                int idx = Mathf.Clamp(visualMaterialSlot, 0, r.sharedMaterials.Length - 1);
                if (cachedTintBlock == null) cachedTintBlock = new MaterialPropertyBlock();
                r.GetPropertyBlock(cachedTintBlock, idx);
                if (cachedTintBlock.isEmpty) SyncVisualTint();
            }
        }
    }

    /// <summary>
    /// Recalcula a mistura de entrada, aplica filtragem subtrativa RGB e atualiza os emissores de saída.
    /// </summary>
    public void EvaluateGate()
    {
        toRemoveBuffer.Clear();
        colorBuffer.Clear();
        float maxStrength = 0f;
        Vector3 combinedDir = Vector3.zero;

        foreach (var kvp in incomingBeamDirs)
        {
            PrismaController src = kvp.Key;
            if (src == null || !src.gameObject.activeInHierarchy || !src.IsEmitting ||
                (emitterA != null && src.HasUpstreamSource(emitterA, 8)) ||
                (emitterB != null && src.HasUpstreamSource(emitterB, 8)))
            {
                toRemoveBuffer.Add(src);
                continue;
            }
            colorBuffer.Add(src.ActiveEmittedColor);
            maxStrength = Mathf.Max(maxStrength, src.CurrentLightStrength);
            combinedDir += kvp.Value;
        }

        for (int i = 0; i < toRemoveBuffer.Count; i++) incomingBeamDirs.Remove(toRemoveBuffer[i]);

        if (colorBuffer.Count == 0 || maxStrength <= 0.01f)
        {
            DisableEmitter(emitterA);
            DisableEmitter(emitterB);
            return;
        }

        Color incomingColor = AlbedoColorUtility.CombineAdditive(colorBuffer);
        Vector3 primaryDir = combinedDir.sqrMagnitude > 0.001f ? combinedDir.normalized : GetFlatTransformForward();

        UpdateBranch(ref emitterA, "GateEmitter_A", EffectiveFilterColorA, incomingColor, maxStrength, primaryDir, yawOffsetA, customExitPointA, true);
        UpdateBranch(ref emitterB, "GateEmitter_B", EffectiveFilterColorB, incomingColor, maxStrength, primaryDir, yawOffsetB, customExitPointB, gateMode == ChromaticGateMode.DualSplitter);
    }

    private void UpdateBranch(
        ref PrismaController emitter, string childName, Color filterColor, Color incomingColor,
        float strength, Vector3 incomingDir, float yawOffset, Transform customExit, bool activeBranch)
    {
        Color filtered = AlbedoColorUtility.ComputeEmittedColor(filterColor, incomingColor, PrismColorMode.Multiply);
        float maxChannel = Mathf.Max(filtered.r, Mathf.Max(filtered.g, filtered.b));
        if (!activeBranch || maxChannel <= 0.05f)
        {
            DisableEmitter(emitter);
            return;
        }

        if (emitter == null)
        {
            Transform existing = transform.Find(childName);
            GameObject go = existing != null ? existing.gameObject : new GameObject(childName);
            if (existing == null) go.transform.SetParent(transform, false);
            emitter = go.GetComponent<PrismaController>() ?? go.AddComponent<PrismaController>();
        }

        Vector3 baseDir = alignToIncomingBeam ? incomingDir : GetFlatTransformForward();
        Vector3 outDir = (Quaternion.Euler(0f, yawOffset, 0f) * baseDir).normalized;
        Vector3 outPos = customExit != null ? customExit.position : (transform.position + outDir * exitThicknessOffset);

        emitter.transform.SetPositionAndRotation(outPos, Quaternion.LookRotation(outDir, Vector3.up));
        emitter.PrismColor = filterColor;
        emitter.SetSyntheticInput(true, filtered, strength);
    }

    private static void DisableEmitter(PrismaController emitter)
    {
        if (emitter != null) emitter.SetSyntheticInput(false, Color.black, 0f);
    }

    private Vector3 GetFlatTransformForward()
    {
        Vector3 f = new Vector3(transform.forward.x, 0f, transform.forward.z);
        return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
    }

    private void OnDrawGizmos()
    {
        Vector3 baseForward = GetFlatTransformForward();
        DrawBranchGizmo(customExitPointA, baseForward, yawOffsetA, EffectiveFilterColorA);
        if (gateMode == ChromaticGateMode.DualSplitter)
            DrawBranchGizmo(customExitPointB, baseForward, yawOffsetB, EffectiveFilterColorB);
    }

    private void DrawBranchGizmo(Transform exitPoint, Vector3 baseForward, float yawOffset, Color col)
    {
        Vector3 dir = (Quaternion.Euler(0f, yawOffset, 0f) * baseForward).normalized;
        Vector3 pos = exitPoint != null ? exitPoint.position : (transform.position + dir * exitThicknessOffset);
        Vector3 tip = pos + dir * 2.0f;
        Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
        Gizmos.color = col;
        Gizmos.DrawWireSphere(pos, 0.2f);
        Gizmos.DrawLine(pos, tip);
        Gizmos.DrawLine(tip, tip - dir * 0.35f + right * 0.18f);
        Gizmos.DrawLine(tip, tip - dir * 0.35f - right * 0.18f);
    }
}
