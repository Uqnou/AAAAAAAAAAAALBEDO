using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Construtor no Editor para o God Ray guia de fim de fase (ExitGodRay_Guiding).
/// Gera materiais URP, partículas de poeira e salva o prefab pronto para uso em qualquer fase.
/// </summary>
public static class ExitGodRayBuilder
{
    private const string OuterMatPath = "Assets/Materials/ExitGodRay_Outer.mat";
    private const string InnerMatPath = "Assets/Materials/ExitGodRay_Inner.mat";
    private const string DustMatPath = "Assets/Materials/ExitGodRay_Dust.mat";
    private const string PrefabPath = "Assets/Prefabs/ExitGodRay_Guiding.prefab";

    [MenuItem("Tools/Antigravity/Create Exit God Ray (Guiding)")]
    public static void CreateInScene()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            prefab = BuildAndSavePrefab();
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "ExitGodRay_Guiding";

        LevelExitButton exitBtn = Object.FindAnyObjectByType<LevelExitButton>();
        if (exitBtn != null)
        {
            instance.transform.position = exitBtn.transform.position + new Vector3(-2.5f, 11.0f, 0f);
        }
        else if (SceneView.lastActiveSceneView != null)
        {
            Vector3 pivot = SceneView.lastActiveSceneView.pivot;
            instance.transform.position = new Vector3(pivot.x, Mathf.Max(10.0f, pivot.y + 8.0f), pivot.z);
        }
        else
        {
            instance.transform.position = new Vector3(0f, 11.0f, 0f);
        }

        Undo.RegisterCreatedObjectUndo(instance, "Create Exit God Ray");
        Selection.activeGameObject = instance;
        Debug.Log("[ExitGodRay] Prefab instanciado na cena! Ajuste a rotação/posição para apontar para o final da fase.");
    }

    [MenuItem("Tools/Antigravity/Rebuild Exit God Ray Prefab")]
    public static GameObject BuildAndSavePrefab()
    {
        EnsureFolder("Assets/Materials");
        EnsureFolder("Assets/Prefabs");

        Shader rayShader = Shader.Find("Custom/ExitGodRay");
        if (rayShader == null)
        {
            Debug.LogError("[ExitGodRay] Shader 'Custom/ExitGodRay' não encontrado!");
            return null;
        }

        Material outerMat = CreateOrUpdateRayMaterial(OuterMatPath, rayShader, 0.34f, 9f, 2.2f, 0.25f);
        Material innerMat = CreateOrUpdateRayMaterial(InnerMatPath, rayShader, 0.28f, 6f, 2.6f, -0.2f);
        Material dustMat = CreateOrUpdateDustMaterial(DustMatPath);

        GameObject root = new GameObject("ExitGodRay_Guiding");
        root.transform.rotation = Quaternion.Euler(0f, 0f, 16f);

        MeshFilter outerFilter = root.AddComponent<MeshFilter>();
        MeshRenderer outerRenderer = root.AddComponent<MeshRenderer>();
        ConfigureRenderer(outerRenderer, outerMat);

        GameObject innerObj = new GameObject("InnerShafts");
        innerObj.transform.SetParent(root.transform, false);
        MeshFilter innerFilter = innerObj.AddComponent<MeshFilter>();
        MeshRenderer innerRenderer = innerObj.AddComponent<MeshRenderer>();
        ConfigureRenderer(innerRenderer, innerMat);

        GameObject dustObj = new GameObject("DustMotes");
        dustObj.transform.SetParent(root.transform, false);
        ParticleSystem ps = ConfigureDustSystem(dustObj, dustMat);

        GameObject lightObj = new GameObject("ExitFloorLight");
        lightObj.transform.SetParent(root.transform, false);
        Light pointLight = lightObj.AddComponent<Light>();
        pointLight.type = LightType.Point;
        pointLight.color = new Color(1.0f, 0.88f, 0.52f, 1.0f);
        pointLight.intensity = 2.5f;
        pointLight.range = 6.0f;
        pointLight.shadows = LightShadows.None;

        ExitGodRayController controller = root.AddComponent<ExitGodRayController>();
        controller.ConfigureReferences(outerFilter, outerRenderer, innerFilter, innerRenderer, ps, pointLight);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[ExitGodRay] Prefab salvo em '{PrefabPath}' com sucesso!");
        return prefab;
    }

    private static Material CreateOrUpdateRayMaterial(
        string path, Shader shader, float baseAlpha, float rayCount, float sharpness, float swaySpeed)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = shader;
        }

        mat.SetColor("_TintColor", new Color(1.0f, 0.88f, 0.52f, 1.0f));
        mat.SetFloat("_Intensity", 2.0f);
        mat.SetFloat("_BaseAlpha", baseAlpha);
        mat.SetFloat("_VerticalFalloff", 1.35f);
        mat.SetFloat("_TopFade", 0.08f);
        mat.SetFloat("_RimPower", 1.8f);
        mat.SetFloat("_DepthFadeDistance", 0.65f);
        mat.SetFloat("_RayCount", rayCount);
        mat.SetFloat("_RaySharpness", sharpness);
        mat.SetFloat("_RayScrollSpeed", swaySpeed);
        mat.SetFloat("_FlowSpeed", 0.5f);
        mat.SetFloat("_ShimmerSpeed", 1.4f);
        mat.SetFloat("_ShimmerAmount", 0.25f);
        mat.SetFloat("_MinRayFloor", 0.22f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material CreateOrUpdateDustMaterial(string path)
    {
        Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(particleShader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = particleShader;
        }

        mat.SetFloat("_Surface", 1f); // Transparent
        mat.SetFloat("_Blend", 2f);   // Additive
        mat.SetColor("_BaseColor", new Color(1.0f, 0.94f, 0.72f, 0.9f));
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.One);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = (int)RenderQueue.Transparent + 10;
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void ConfigureRenderer(MeshRenderer renderer, Material mat)
    {
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    private static ParticleSystem ConfigureDustSystem(GameObject dustObj, Material dustMat)
    {
        ParticleSystem ps = dustObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 90;
        main.gravityModifier = -0.02f;

        var emission = ps.emission;
        emission.rateOverTime = 18f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.22f;
        noise.frequency = 0.45f;
        noise.scrollSpeed = 0.25f;

        var psRenderer = dustObj.GetComponent<ParticleSystemRenderer>();
        psRenderer.sharedMaterial = dustMat;
        psRenderer.shadowCastingMode = ShadowCastingMode.Off;
        psRenderer.receiveShadows = false;
        return ps;
    }

    private static void EnsureFolder(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath)) return;
        string[] parts = assetPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }
}
