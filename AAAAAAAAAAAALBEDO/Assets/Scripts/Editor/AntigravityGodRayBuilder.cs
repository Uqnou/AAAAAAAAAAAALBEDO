using UnityEngine;
using UnityEditor;

/// <summary>
/// Construtor e gerenciador no Editor para criar e configurar God Rays volumétricos no projeto ALBEDO.
/// Menus disponíveis em Tools > Antigravity.
/// </summary>
public static class AntigravityGodRayBuilder
{
    [MenuItem("Tools/Antigravity/Create God Ray Beam")]
    public static void CreateSingleGodRay()
    {
        Vector3 spawnPos = new Vector3(0f, 7.0f, 0f);
        if (SceneView.lastActiveSceneView != null)
        {
            spawnPos = SceneView.lastActiveSceneView.pivot;
            spawnPos.y = Mathf.Max(5.0f, spawnPos.y);
        }

        GameObject rayObj = CreateOrUpdateGodRay("GodRay_Beam", spawnPos, new Color(0.3f, 0.9f, 1.0f, 1.0f), 2.0f);
        Selection.activeGameObject = rayObj;
        Undo.RegisterCreatedObjectUndo(rayObj, "Create God Ray Beam");
    }

    [MenuItem("Tools/Antigravity/Setup God Ray Demo Pair (Gold & Cyan)")]
    public static void CreateDemoPair()
    {
        GameObject rayGold = CreateOrUpdateGodRay("GodRay_Sunbeam_Gold", new Vector3(3f, 7.5f, 2f), new Color(1.0f, 0.85f, 0.35f, 1.0f), 2.2f);
        GameObject rayCyan = CreateOrUpdateGodRay("GodRay_Mystic_Cyan", new Vector3(-3.5f, 7.5f, -2f), new Color(0.2f, 0.85f, 1.0f, 1.0f), 2.5f);

        Selection.activeGameObject = rayGold;
        Debug.Log("<color=green>[Antigravity]</color> Par de God Rays (Dourado e Ciano) criado na cena com sucesso!");
    }

    [MenuItem("Tools/Antigravity/Setup God Ray on Selected Prism")]
    public static void SetupSelectedPrismGodRay()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("Antigravity", "Por favor, selecione um GameObject com PrismaController na cena.", "OK");
            return;
        }

        PrismaController prisma = selected.GetComponent<PrismaController>();
        if (prisma == null)
        {
            EditorUtility.DisplayDialog("Antigravity", $"O objeto '{selected.name}' não possui o componente PrismaController.", "OK");
            return;
        }

        string rayName = $"{selected.name}_GodRay";
        Transform existing = selected.transform.Find(rayName) ?? selected.transform.Find("PrismGodRay");
        GameObject rayObj = existing != null ? existing.gameObject : null;

        if (rayObj == null)
        {
            rayObj = CreateOrUpdateGodRay(rayName, selected.transform.position, prisma.PrismColor, 2.0f);
            rayObj.transform.SetParent(selected.transform);
            rayObj.transform.localPosition = Vector3.zero;
            rayObj.transform.localRotation = Quaternion.FromToRotation(Vector3.down, Vector3.forward);
        }

        GodRayBeam beam = rayObj.GetComponent<GodRayBeam>();
        SerializedObject so = new SerializedObject(prisma);
        so.FindProperty("prismGodRay").objectReferenceValue = beam;
        so.FindProperty("emitGodRay").boolValue = true;
        so.ApplyModifiedProperties();

        rayObj.SetActive(false);
        EditorUtility.SetDirty(prisma);
        Debug.Log($"<color=cyan>[Antigravity]</color> God Ray configurado no prisma '{selected.name}' com sucesso!");
    }

    /// <summary>
    /// Instancia a hierarquia completa do God Ray com Shaders URP, anel no piso, partículas de poeira e LightSource.
    /// </summary>
    public static GameObject CreateOrUpdateGodRay(string name, Vector3 ceilingPosition, Color rayColor, float intensity)
    {
        GameObject rayObj = GameObject.Find(name);
        if (rayObj == null)
        {
            rayObj = new GameObject(name);
        }
        rayObj.transform.position = ceilingPosition;

        // 1. Shaders e Materiais
        Shader volumetricShader = Shader.Find("Custom/GodRayVolumetric") ?? Shader.Find("Universal Render Pipeline/Unlit");
        Shader ringShader = Shader.Find("Custom/GodRayGroundRing") ?? Shader.Find("Universal Render Pipeline/Unlit");

        Material beamMat = new Material(volumetricShader);
        beamMat.name = $"{name}_BeamMat";

        Material ringMat = new Material(ringShader);
        ringMat.name = $"{name}_RingMat";

        // 2. Renderizador da Malha Volumétrica Principal
        MeshRenderer beamRenderer = rayObj.GetComponent<MeshRenderer>();
        if (beamRenderer == null) beamRenderer = rayObj.AddComponent<MeshRenderer>();
        beamRenderer.sharedMaterial = beamMat;
        beamRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        beamRenderer.receiveShadows = false;

        MeshFilter beamFilter = rayObj.GetComponent<MeshFilter>();
        if (beamFilter == null) beamFilter = rayObj.AddComponent<MeshFilter>();

        // 3. Anel de Demarcação no Solo (Filho)
        Transform ringTransform = rayObj.transform.Find("GroundRing");
        GameObject ringObj;
        if (ringTransform == null)
        {
            ringObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ringObj.name = "GroundRing";
            ringObj.transform.SetParent(rayObj.transform);
            Collider ringCol = ringObj.GetComponent<Collider>();
            if (ringCol != null) Object.DestroyImmediate(ringCol);
        }
        else
        {
            ringObj = ringTransform.gameObject;
        }

        ringObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        ringObj.transform.localScale = new Vector3(4.5f, 4.5f, 1.0f);
        MeshRenderer ringRenderer = ringObj.GetComponent<MeshRenderer>();
        ringRenderer.sharedMaterial = ringMat;
        ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ringRenderer.receiveShadows = false;

        // 4. Partículas de Poeira Suspensa (Filho)
        Transform particlesTransform = rayObj.transform.Find("DustParticles");
        GameObject particlesObj;
        if (particlesTransform == null)
        {
            particlesObj = new GameObject("DustParticles");
            particlesObj.transform.SetParent(rayObj.transform);
            particlesObj.transform.localPosition = Vector3.zero;
            particlesObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }
        else
        {
            particlesObj = particlesTransform.gameObject;
        }

        ParticleSystem ps = particlesObj.GetComponent<ParticleSystem>();
        if (ps == null) ps = particlesObj.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4.0f, 7.0f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.35f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;

        var emission = ps.emission;
        emission.rateOverTime = 8f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.radius = 0.35f;
        shape.angle = 15f;
        shape.length = 7f;

        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.0f, 0.0f), new GradientAlphaKey(0.8f, 0.2f), new GradientAlphaKey(0.8f, 0.8f), new GradientAlphaKey(0.0f, 1.0f) }
        );
        colorOverLife.color = grad;

        var noise = ps.noise;
        noise.enabled = true;
        noise.frequency = 0.4f;
        noise.strength = 0.25f;

        var psRenderer = particlesObj.GetComponent<ParticleSystemRenderer>();
        if (psRenderer != null)
        {
            psRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit"));
        }

        // 5. Integração com LightSource de ALBEDO
        LightSource ls = rayObj.GetComponent<LightSource>();
        if (ls == null) ls = rayObj.AddComponent<LightSource>();
        ls.SetLightColor(rayColor);

        // 6. Configuração do Componente GodRayBeam
        GodRayBeam beam = rayObj.GetComponent<GodRayBeam>();
        if (beam == null) beam = rayObj.AddComponent<GodRayBeam>();

        SerializedObject so = new SerializedObject(beam);
        so.FindProperty("rayColor").colorValue = rayColor;
        so.FindProperty("intensity").floatValue = intensity;
        so.FindProperty("beamMeshFilter").objectReferenceValue = beamFilter;
        so.FindProperty("beamMeshRenderer").objectReferenceValue = beamRenderer;
        so.FindProperty("dustParticles").objectReferenceValue = ps;
        so.FindProperty("groundRingTransform").objectReferenceValue = ringObj.transform;
        so.FindProperty("groundRingRenderer").objectReferenceValue = ringRenderer;
        so.FindProperty("triggerCollider").objectReferenceValue = rayObj.GetComponent<CapsuleCollider>();
        so.ApplyModifiedProperties();

        beam.UpdateHeightWithRaycast();
        beam.ApplyVisualProperties();

        Debug.Log($"<color=cyan>[Antigravity]</color> God Ray '{name}' criado com sucesso em {ceilingPosition} (Cor: #{ColorUtility.ToHtmlStringRGB(rayColor)})!");
        return rayObj;
    }
}
