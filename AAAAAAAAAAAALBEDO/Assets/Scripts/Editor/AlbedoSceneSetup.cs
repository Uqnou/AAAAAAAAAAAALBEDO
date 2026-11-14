using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ferramenta Editor/CLI para criar materiais Noir/Halftone, inspecionar materiais embutidos do FBX
/// e configurar toda a SampleScene com as mecânicas do GDD (ALBEDO).
/// </summary>
public static class AlbedoSceneSetup
{
    private const string ShadersFolder = "Assets/Shaders";
    private const string MaterialsFolder = "Assets/Materials";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    private const string HalftoneEnvMatPath = MaterialsFolder + "/HalftoneNoir_Environment.mat";
    private const string HalftonePlayerMatPath = MaterialsFolder + "/HalftoneNoir_Player.mat";
    private const string OutlineWhiteMatPath = MaterialsFolder + "/OutlineWhite.mat";
    private const string BlackFireMatPath = MaterialsFolder + "/BlackFire.mat";
    private const string HalftoneDarkMatPath = MaterialsFolder + "/HalftoneNoir_Dark.mat";

    [MenuItem("ALBEDO/Setup SampleScene & Materials")]
    public static void RunSetupMenu()
    {
        RunSetupInternal(false);
    }

    /// <summary>
    /// Entrada compatível com -executeMethod AlbedoSceneSetup.RunSetupBatch
    /// </summary>
    public static void RunSetupBatch()
    {
        RunSetupInternal(true);
    }

    private static void RunSetupInternal(bool fromBatchMode)
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        EnsureFolder("Assets", "Materials");

        Material envMat = CreateOrUpdateMaterial(HalftoneEnvMatPath, "ALBEDO/HalftoneNoir", mat =>
        {
            mat.SetColor("_BaseColor", Color.white);
            mat.SetColor("_ShadowColor", Color.black);
            mat.SetFloat("_MainLightWeight", 0.14f);
            mat.SetFloat("_LightIntensityScale", 1.15f);
            mat.SetFloat("_DotScale", 7.0f);
            mat.SetFloat("_DotSharpness", 0.04f);
            mat.SetFloat("_ThresholdMin", 0.03f);
            mat.SetFloat("_ThresholdMax", 0.65f);
            mat.SetFloat("_AdditiveTintStrength", 0.9f);
            mat.SetFloat("_RimPower", 0.0f);
        });

        Material darkMat = CreateOrUpdateMaterial(HalftoneDarkMatPath, "ALBEDO/HalftoneNoir", mat =>
        {
            mat.SetColor("_BaseColor", new Color(0.85f, 0.85f, 0.85f, 1f));
            mat.SetColor("_ShadowColor", Color.black);
            mat.SetFloat("_MainLightWeight", 0.06f);
            mat.SetFloat("_LightIntensityScale", 0.95f);
            mat.SetFloat("_DotScale", 8.0f);
            mat.SetFloat("_DotSharpness", 0.04f);
            mat.SetFloat("_ThresholdMin", 0.05f);
            mat.SetFloat("_ThresholdMax", 0.85f);
            mat.SetFloat("_AdditiveTintStrength", 0.9f);
            mat.SetFloat("_RimPower", 0.0f);
        });

        Material playerMat = CreateOrUpdateMaterial(HalftonePlayerMatPath, "ALBEDO/HalftoneNoir", mat =>
        {
            mat.SetColor("_BaseColor", Color.white);
            mat.SetColor("_ShadowColor", Color.black);
            mat.SetFloat("_MainLightWeight", 0.35f);
            mat.SetFloat("_LightIntensityScale", 1.2f);
            mat.SetFloat("_DotScale", 9.0f);
            mat.SetFloat("_DotSharpness", 0.03f);
            mat.SetFloat("_ThresholdMin", 0.02f);
            mat.SetFloat("_ThresholdMax", 0.5f);
            mat.SetColor("_RimColor", Color.white);
            mat.SetFloat("_RimPower", 0.95f);
            mat.SetFloat("_RimThreshold", 0.58f);
        });

        Material outlineMat = CreateOrUpdateMaterial(OutlineWhiteMatPath, "ALBEDO/OutlineWhite", mat =>
        {
            mat.SetColor("_OutlineColor", Color.white);
            mat.SetFloat("_OutlineExtrude", 0.0f);
            mat.SetFloat("_CullMode", 2f); // Cull Back para slots Solidify com normais invertidas
        });

        Material blackFireMat = CreateOrUpdateMaterial(BlackFireMatPath, "ALBEDO/BlackFire", mat =>
        {
            mat.SetColor("_CoreColor", Color.black);
            mat.SetColor("_RimColor", Color.white);
            mat.SetFloat("_RimPower", 2.4f);
            mat.SetFloat("_FlickerSpeed", 6.5f);
            mat.SetFloat("_WaveAmplitude", 0.06f);
        });

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError("[AlbedoSceneSetup] Não foi possível abrir " + ScenePath);
            if (fromBatchMode) EditorApplication.Exit(1);
            return;
        }

        InspectAndApplyMaterials(envMat, darkMat, playerMat, outlineMat);
        ConfigureSceneLights();
        ConfigurePrisms();
        ConfigureShadows();
        ConfigureTorchAndBrazier(blackFireMat);
        ConfigureExitButton();
        ConfigurePlayerAndCamera();

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        Debug.Log($"[AlbedoSceneSetup] Setup concluído com sucesso! Cena salva: {saved}");

        if (fromBatchMode)
        {
            EditorApplication.Exit(saved ? 0 : 1);
        }
    }

    private static void EnsureFolder(string parent, string folderName)
    {
        string full = $"{parent}/{folderName}";
        if (!AssetDatabase.IsValidFolder(full))
        {
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }

    private static Material CreateOrUpdateMaterial(string assetPath, string shaderName, Action<Material> configure)
    {
        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogError($"[AlbedoSceneSetup] Shader '{shaderName}' não encontrado! Verifique compilação em {ShadersFolder}.");
            return null;
        }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, assetPath);
        }
        else if (mat.shader != shader)
        {
            mat.shader = shader;
        }

        configure?.Invoke(mat);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void InspectAndApplyMaterials(Material envMat, Material darkMat, Material playerMat, Material outlineMat)
    {
        MeshRenderer[] renderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>();
        foreach (MeshRenderer mr in renderers)
        {
            if (mr == null) continue;
            string goName = mr.gameObject.name;

            if (goName == "Capsule" && playerMat != null)
            {
                mr.sharedMaterial = playerMat;
                EditorUtility.SetDirty(mr);
                continue;
            }

            Material[] mats = mr.sharedMaterials;
            bool changed = false;

            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null) continue;

                string mName = m.name ?? string.Empty;
                Color baseCol = ReadMaterialColor(m);

                // Log detalhado para objetos de mecânica para inspeção de cores FBX
                if (goName.StartsWith("Prisma", StringComparison.OrdinalIgnoreCase) ||
                    goName.StartsWith("Sombra", StringComparison.OrdinalIgnoreCase) ||
                    goName.StartsWith("Cylinder", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log($"[FBX Material Info] {goName}[slot {i}] mat='{mName}' shader='{m.shader?.name}' col=({baseCol.r:F2}, {baseCol.g:F2}, {baseCol.b:F2})");
                }

                // Não substitui shader dos cristais coloridos de Prisma, barras de Sombra ou botão/tocha coloridos;
                // aplica apenas em slots estruturais/borda do Blender Solidify.
                bool isOutlineSlot = mName.IndexOf("outline", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     mName.IndexOf("borda", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     mName.IndexOf("contorno", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     mName.IndexOf("white", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     (goName.StartsWith("Cube", StringComparison.OrdinalIgnoreCase) ||
                                      goName.StartsWith("Pilar", StringComparison.OrdinalIgnoreCase) ||
                                      goName.StartsWith("Sphere", StringComparison.OrdinalIgnoreCase) ||
                                      goName.StartsWith("Cylinder", StringComparison.OrdinalIgnoreCase)) &&
                                     IsPureWhiteMaterial(m, baseCol);

                bool isSombraOrPrisma = goName.StartsWith("Sombra", StringComparison.OrdinalIgnoreCase) ||
                                        goName.StartsWith("Prisma", StringComparison.OrdinalIgnoreCase);

                if (isOutlineSlot && !isSombraOrPrisma && outlineMat != null && i > 0)
                {
                    mats[i] = outlineMat;
                    changed = true;
                }
                else if ((goName.StartsWith("Cube", StringComparison.OrdinalIgnoreCase) ||
                          goName.StartsWith("Pilar", StringComparison.OrdinalIgnoreCase) ||
                          goName.StartsWith("Sphere", StringComparison.OrdinalIgnoreCase) ||
                          (goName.StartsWith("Cylinder", StringComparison.OrdinalIgnoreCase) && goName != "Cylinder.003")) &&
                         !isOutlineSlot)
                {
                    bool isSecondaryDark = mName.IndexOf("Parede2", StringComparison.OrdinalIgnoreCase) >= 0;
                    Material chosen = isSecondaryDark ? darkMat : envMat;
                    if (chosen != null)
                    {
                        mats[i] = chosen;
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                mr.sharedMaterials = mats;
                EditorUtility.SetDirty(mr);
            }
        }
    }

    private static bool IsPureWhiteMaterial(Material m, Color c)
    {
        return c.r > 0.92f && c.g > 0.92f && c.b > 0.92f;
    }

    private static Color ReadMaterialColor(Material m)
    {
        if (m == null) return Color.white;
        if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
        if (m.HasProperty("_Color")) return m.GetColor("_Color");
        return Color.white;
    }

    private static void ConfigureSceneLights()
    {
        Light[] lights = UnityEngine.Object.FindObjectsByType<Light>();
        foreach (Light lt in lights)
        {
            if (lt == null || lt.type == LightType.Directional) continue;

            GameObject go = lt.gameObject;
            LightSource ls = go.GetComponent<LightSource>();
            if (ls == null)
            {
                ls = go.AddComponent<LightSource>();
            }

            // Garante alcance adequado até o chão (luzes estão em y ≈ 8.5–10.7m)
            if (lt.range < 14f)
            {
                lt.range = 16f;
            }

            BoxCollider box = go.GetComponent<BoxCollider>();
            if (box == null && go.GetComponent<Collider>() == null)
            {
                box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(3.8f, 3.8f, 24f);
                box.center = new Vector3(0f, 0f, 0f);
            }
            else if (box != null)
            {
                box.isTrigger = true;
            }

            EditorUtility.SetDirty(lt);
            EditorUtility.SetDirty(ls);
            if (box != null) EditorUtility.SetDirty(box);
        }
    }

    private static void ConfigurePrisms()
    {
        // Procura todos GameObjects chamados Prisma*
        Transform[] allTransforms = UnityEngine.Object.FindObjectsByType<Transform>();
        foreach (Transform t in allTransforms)
        {
            if (t == null) continue;
            if (!t.gameObject.name.StartsWith("Prisma", StringComparison.OrdinalIgnoreCase)) continue;
            GameObject go = t.gameObject;
            if (go.GetComponent<Rigidbody>() == null)
            {
                var rb = go.AddComponent<Rigidbody>();
                rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            }
            if (go.GetComponent<Collider>() == null)
            {
                go.AddComponent<BoxCollider>();
            }
            if (go.GetComponent<PickableObject>() == null)
            {
                go.AddComponent<PickableObject>();
            }

            PrismaController pc = go.GetComponent<PrismaController>();
            if (pc == null)
            {
                pc = go.AddComponent<PrismaController>();
            }

            AlbedoColorPreset fallbackPreset = AlbedoColorPreset.White;
            if (string.Equals(go.name, "Prisma.001", StringComparison.OrdinalIgnoreCase)) fallbackPreset = AlbedoColorPreset.Red;
            else if (string.Equals(go.name, "Prisma.002", StringComparison.OrdinalIgnoreCase)) fallbackPreset = AlbedoColorPreset.Green;
            else if (string.Equals(go.name, "Prisma.003", StringComparison.OrdinalIgnoreCase)) fallbackPreset = AlbedoColorPreset.Blue;

            AlbedoColorPreset detectedPreset = DetectPresetFromRenderers(go, fallbackPreset);
            pc.SetColorPreset(detectedPreset);

            Debug.Log($"[AlbedoSceneSetup] Prisma configurado: '{go.name}' -> Preset={detectedPreset} Color={pc.EffectivePrismColor}");
            EditorUtility.SetDirty(go);
            EditorUtility.SetDirty(pc);
        }
    }

    private static void ConfigureShadows()
    {
        LightSource[] allLights = UnityEngine.Object.FindObjectsByType<LightSource>();
        Transform[] allTransforms = UnityEngine.Object.FindObjectsByType<Transform>();

        foreach (Transform t in allTransforms)
        {
            if (t == null) continue;
            string nm = t.gameObject.name;
            if (!nm.StartsWith("Sombra", StringComparison.OrdinalIgnoreCase)) continue;

            GameObject go = t.gameObject;
            SombraController sc = go.GetComponent<SombraController>();
            if (sc == null)
            {
                sc = go.AddComponent<SombraController>();
            }

            // Conforme confirmação do usuário: Sombra.002 e Sombra.003 são passarelas caminháveis
            if (string.Equals(nm, "Sombra.002", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(nm, "Sombra.003", StringComparison.OrdinalIgnoreCase))
            {
                sc.Mode = ShadowMode.WalkableBridge;
                sc.ConfigureRequiredColor(AlbedoColorPreset.White, true);

                // Vincula automaticamente a luz mais próxima acima da passarela para garantir bloqueio exato
                LightSource nearest = FindClosestLightAboveXZ(go.transform.position, allLights, 9.0f);
                if (nearest != null)
                {
                    sc.RegisterLinkedLight(nearest);
                    Debug.Log($"[AlbedoSceneSetup] Passarela '{nm}' configurada como WalkableBridge vinculada a '{nearest.name}'.");
                }
                else
                {
                    Debug.Log($"[AlbedoSceneSetup] Passarela '{nm}' configurada como WalkableBridge.");
                }
            }
            else
            {
                sc.Mode = ShadowMode.Barrier;
                AlbedoColorPreset fallbackPreset = AlbedoColorPreset.White;
                if (string.Equals(nm, "Sombra.001", StringComparison.OrdinalIgnoreCase)) fallbackPreset = AlbedoColorPreset.Yellow;
                else if (string.Equals(nm, "Sombra.006", StringComparison.OrdinalIgnoreCase)) fallbackPreset = AlbedoColorPreset.Red;

                AlbedoColorPreset detectedPreset = DetectPresetFromRenderers(go, fallbackPreset);
                sc.ConfigureRequiredColor(detectedPreset, true);
                Debug.Log($"[AlbedoSceneSetup] Barreira '{nm}' configurada como Barrier -> Preset={detectedPreset}");
            }

            EditorUtility.SetDirty(sc);
        }
    }

    private static void ConfigureTorchAndBrazier(Material blackFireMat)
    {
        // Cylinder.003 já possui PickableObject na cena e fica sobre o pedestal em (-31.68, 3.14, -12.37)
        GameObject torchGo = GameObject.Find("Cylinder.003");
        if (torchGo != null)
        {
            TorchController tc = torchGo.GetComponent<TorchController>();
            if (tc == null) tc = torchGo.AddComponent<TorchController>();

            if (torchGo.GetComponent<PickableObject>() == null)
            {
                torchGo.AddComponent<PickableObject>();
            }

            Transform existingFlame = torchGo.transform.Find("BlackFireFlame");
            GameObject flameObj = existingFlame != null ? existingFlame.gameObject : null;
            if (flameObj == null)
            {
                flameObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                flameObj.name = "BlackFireFlame";
                UnityEngine.Object.DestroyImmediate(flameObj.GetComponent<Collider>());
                flameObj.transform.SetParent(torchGo.transform, false);
                flameObj.transform.localPosition = new Vector3(0f, 1.35f, 0f);
                flameObj.transform.localScale = new Vector3(1.1f, 1.8f, 1.1f);
            }

            if (blackFireMat != null)
            {
                var r = flameObj.GetComponent<MeshRenderer>();
                if (r != null) r.sharedMaterial = blackFireMat;
            }

            flameObj.SetActive(tc.IsLit);
            tc.SetBlackFireVisual(flameObj);
            EditorUtility.SetDirty(tc);
            EditorUtility.SetDirty(torchGo);
            Debug.Log($"[AlbedoSceneSetup] Tocha configurada em '{torchGo.name}'.");
        }

        // Braseiro próximo em (-31.05, 3.99, -12.36) ou cria zona de Braseiro ao redor de Cube.012 / Cube.047
        GameObject brazierHost = GameObject.Find("Cube.012") ?? GameObject.Find("Cube.047");
        if (brazierHost != null)
        {
            if (brazierHost.GetComponent<Collider>() == null)
            {
                SphereCollider sc = brazierHost.AddComponent<SphereCollider>();
                sc.isTrigger = true;
                sc.radius = 2.6f;
            }

            BlackFireBrazier brazier = brazierHost.GetComponent<BlackFireBrazier>();
            if (brazier == null)
            {
                brazier = brazierHost.AddComponent<BlackFireBrazier>();
            }

            Transform existingBrazierFlame = brazierHost.transform.Find("BrazierBlackFire");
            if (existingBrazierFlame == null)
            {
                GameObject brazierFlame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                brazierFlame.name = "BrazierBlackFire";
                UnityEngine.Object.DestroyImmediate(brazierFlame.GetComponent<Collider>());
                brazierFlame.transform.SetParent(brazierHost.transform, false);
                brazierFlame.transform.localPosition = new Vector3(0f, 0.4f, 0f);
                brazierFlame.transform.localScale = new Vector3(1.2f, 1.8f, 1.2f);
                if (blackFireMat != null)
                {
                    var r = brazierFlame.GetComponent<MeshRenderer>();
                    if (r != null) r.sharedMaterial = blackFireMat;
                }
            }

            EditorUtility.SetDirty(brazierHost);
            Debug.Log($"[AlbedoSceneSetup] Braseiro de Fogo Negro configurado em '{brazierHost.name}'.");
        }
    }

    private static void ConfigureExitButton()
    {
        // Cylinder em (36.96, 1.65, 0.63) é a plataforma circular / botão no final do nível
        GameObject exitObj = GameObject.Find("Cylinder");
        if (exitObj != null)
        {
            BoxCollider col = exitObj.GetComponent<BoxCollider>();
            if (col == null && exitObj.GetComponent<Collider>() == null)
            {
                col = exitObj.AddComponent<BoxCollider>();
            }
            if (col != null)
            {
                col.isTrigger = true;
                col.size = new Vector3(2.2f, 4.0f, 2.2f);
            }

            if (exitObj.GetComponent<LevelExitButton>() == null)
            {
                exitObj.AddComponent<LevelExitButton>();
            }

            EditorUtility.SetDirty(exitObj);
            Debug.Log($"[AlbedoSceneSetup] Botão final configurado em '{exitObj.name}'.");
        }
    }

    private static void ConfigurePlayerAndCamera()
    {
        GameObject player = GameObject.Find("Capsule");
        if (player != null)
        {
            if (string.IsNullOrEmpty(player.tag) || player.tag == "Untagged")
            {
                player.tag = "Player";
            }
            if (player.GetComponent<PlayerController>() == null)
            {
                player.AddComponent<PlayerController>();
            }
            EditorUtility.SetDirty(player);
        }
    }

    private static LightSource FindClosestLightAboveXZ(Vector3 pos, LightSource[] lights, float maxDistXZ)
    {
        LightSource best = null;
        float bestDistSq = maxDistXZ * maxDistXZ;

        foreach (LightSource ls in lights)
        {
            if (ls == null) continue;
            float dx = ls.transform.position.x - pos.x;
            float dz = ls.transform.position.z - pos.z;
            float dSq = dx * dx + dz * dz;
            if (dSq < bestDistSq)
            {
                bestDistSq = dSq;
                best = ls;
            }
        }
        return best;
    }

    private static AlbedoColorPreset DetectPresetFromRenderers(GameObject go, AlbedoColorPreset fallback)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            foreach (Material m in r.sharedMaterials)
            {
                if (m == null) continue;
                string name = m.name ?? string.Empty;

                if (ContainsIgnoreCase(name, "Yellow") || ContainsIgnoreCase(name, "Amarel")) return AlbedoColorPreset.Yellow;
                if (ContainsIgnoreCase(name, "Red") || ContainsIgnoreCase(name, "Vermelh")) return AlbedoColorPreset.Red;
                if (ContainsIgnoreCase(name, "Green") || ContainsIgnoreCase(name, "Verd")) return AlbedoColorPreset.Green;
                if (ContainsIgnoreCase(name, "Blue") || ContainsIgnoreCase(name, "Azul")) return AlbedoColorPreset.Blue;
                if (ContainsIgnoreCase(name, "Cyan") || ContainsIgnoreCase(name, "Ciano")) return AlbedoColorPreset.Cyan;
                if (ContainsIgnoreCase(name, "Magenta") || ContainsIgnoreCase(name, "Roxo")) return AlbedoColorPreset.Magenta;

                Color c = ReadMaterialColor(m);
                // Ignora branco/preto se existe um slot colorido — só classifica se saturado ou explícito
                float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                if (max - min > 0.25f)
                {
                    if (c.r > 0.55f && c.g > 0.55f && c.b < 0.35f) return AlbedoColorPreset.Yellow;
                    if (c.r > 0.55f && c.g < 0.35f && c.b < 0.35f) return AlbedoColorPreset.Red;
                    if (c.g > 0.55f && c.r < 0.35f && c.b < 0.35f) return AlbedoColorPreset.Green;
                    if (c.b > 0.55f && c.r < 0.35f && c.g < 0.35f) return AlbedoColorPreset.Blue;
                    if (c.g > 0.55f && c.b > 0.55f && c.r < 0.35f) return AlbedoColorPreset.Cyan;
                    if (c.r > 0.55f && c.b > 0.55f && c.g < 0.35f) return AlbedoColorPreset.Magenta;
                }
            }
        }
        return fallback;
    }

    private static bool ContainsIgnoreCase(string source, string needle)
    {
        return source.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
