using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Utilitario para executar via AgentScripts/unity-eval.ps1 com o Unity Editor aberto:
/// 1) FixSala3ExploitInCurrentScene: corrige o bypass da Sala 3 (Tutorial antigo -> Fase1).
/// 2) ApplyClaustroP0P1InCurrentScene: aplica melhorias P0/P1 na Fase2 (Claustro dos Vitrais).
/// 3) PromoteScenesAndConfigureBuildSettings: promove cenas preservando .meta e atualiza Build Settings.
/// </summary>
public static class CampaignAndFixes
{
    public static string FixSala3ExploitInCurrentScene()
    {
        var log = new List<string>();
        GameObject prismaRed = GameObject.Find("Prisma.Red");
        if (prismaRed != null)
        {
            PrismaController pc = prismaRed.GetComponent<PrismaController>();
            if (pc != null)
            {
                SerializedObject so = new SerializedObject(pc);
                SerializedProperty lenProp = so.FindProperty("godRayLength");
                SerializedProperty rangeProp = so.FindProperty("beamRange");
                if (lenProp != null) lenProp.floatValue = 20f;
                if (rangeProp != null) rangeProp.floatValue = 20f;
                so.ApplyModifiedProperties();
                if (pc.PrismGodRay != null) pc.PrismGodRay.SetDimensions(pc.GodRayTopRadius, pc.GodRayBottomRadius, 20f);
                EditorUtility.SetDirty(pc);
                log.Add("Prisma.Red: godRayLength/beamRange ajustados para 20m (Luz.003=16.4m ok; Luz->Sombra.004=24.03m bloqueado).");
            }
        }
        else
        {
            log.Add("AVISO: Prisma.Red nao encontrado na cena aberta.");
        }

        GameObject pillar = GameObject.Find("Pilar_Bloqueio_Sala3");
        if (pillar == null)
        {
            pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = "Pilar_Bloqueio_Sala3";
            Undo.RegisterCreatedObjectUndo(pillar, "Create Sala 3 Blocker Pillar");
        }
        pillar.transform.position = new Vector3(27.5f, 2.6f, -13.0f);
        pillar.transform.localScale = new Vector3(1.1f, 5.2f, 1.5f);
        Renderer pr = pillar.GetComponent<Renderer>();
        if (pr != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = new Color(0.24f, 0.26f, 0.30f, 1f);
            pr.sharedMaterial = mat;
        }
        EditorUtility.SetDirty(pillar);
        log.Add("Pilar_Bloqueio_Sala3 posicionado em (27.5, 2.6, -13.0) bloqueando visada direta Luz->Sombra.004.");

        EnableExitNextSceneLoad(log);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        return string.Join("\n", log);
    }

    public static string ApplyClaustroP0P1InCurrentScene()
    {
        var log = new List<string>();
        GameObject vitralCiano = GameObject.Find("Grade_Vitral_Ciano");
        if (vitralCiano != null)
        {
            ChromaticFilterGate gate = vitralCiano.GetComponent<ChromaticFilterGate>();
            if (gate != null)
            {
                gate.FilterPresetA = AlbedoColorPreset.Blue;
                EditorUtility.SetDirty(gate);
                log.Add("P0: Grade_Vitral_Ciano sincronizado para Blue.");
            }
        }

        Transform guideRoot = GetOrCreateGroup("__Guides_P0_P1");
        CreateFloorStrip("Zona_Pousar_Tocha_C4", guideRoot, new Vector3(30.0f, 0.04f, 6.2f), new Vector3(2.2f, 0.04f, 2.2f), new Color(0.45f, 0.25f, 0.75f, 1f));
        log.Add("P0: Zona_Pousar_Tocha_C4 demarcada em (30.0, 0.04, 6.2) fora dos feixes de Camara 4.");

        // P1: Guias visuais diegeticas na Camara 3 (DualSplitter -> Sombra.004 e Sombra.007)
        CreateGuideBetween("Guia_C3_Red", guideRoot, "Cristal_Divisor_1", "Sombra.004", new Color(0.90f, 0.22f, 0.22f, 1f));
        CreateGuideBetween("Guia_C3_Green", guideRoot, "Cristal_Divisor_1", "Sombra.007", new Color(0.20f, 0.85f, 0.32f, 1f));

        // P1: Guias visuais diegeticas na Camara 4 (Mesa + Slot_Externo_Verde -> Sombra.001)
        CreateGuideBetween("Guia_C4_Mesa", guideRoot, "Mesa", "Sombra.001", new Color(0.75f, 0.45f, 0.95f, 1f));
        CreateGuideBetween("Guia_C4_Verde", guideRoot, "Slot_Externo_Verde", "Sombra.001", new Color(0.25f, 0.90f, 0.35f, 1f));
        log.Add("P1: Guias diegeticas de piso adicionadas nas Camaras 3 e 4.");

        EnableExitNextSceneLoad(log);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        return string.Join("\n", log);
    }

    public static string PromoteScenesAndConfigureBuildSettings()
    {
        var log = new List<string>();
        const string tut = "Assets/Scenes/Tutorial.unity";
        const string f1 = "Assets/Scenes/Fase1.unity";
        const string f2 = "Assets/Scenes/Fase2.unity";
        const string f3 = "Assets/Scenes/Fase3.unity";

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(f1) != null && AssetDatabase.LoadAssetAtPath<SceneAsset>(f2) == null)
        {
            string err = AssetDatabase.MoveAsset(f1, f2);
            log.Add(string.IsNullOrEmpty(err) ? "Movido Fase1.unity -> Fase2.unity (Claustro)." : $"Erro ao mover Fase1: {err}");
        }
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(tut) != null && AssetDatabase.LoadAssetAtPath<SceneAsset>(f1) == null)
        {
            string err = AssetDatabase.MoveAsset(tut, f1);
            log.Add(string.IsNullOrEmpty(err) ? "Movido Tutorial.unity antigo -> Fase1.unity." : $"Erro ao mover Tutorial: {err}");
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(tut) == null)
        {
            var sTut = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(sTut, tut);
            log.Add("Criado stub vazio Tutorial.unity (pronto para blockout do Novo Tutorial).");
        }
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(f3) == null)
        {
            var sF3 = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(sF3, f3);
            log.Add("Criado stub vazio Fase3.unity (pronto para blockout do Grand Finale).");
        }

        AssetDatabase.Refresh();
        var scenes = new List<EditorBuildSettingsScene>();
        foreach (string path in new[] { tut, f1, f2, f3 })
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
            {
                scenes.Add(new EditorBuildSettingsScene(path, true));
                log.Add($"BuildSettings: + {path}");
            }
        }
        EditorBuildSettings.scenes = scenes.ToArray();
        return string.Join("\n", log);
    }

    private static void EnableExitNextSceneLoad(List<string> log)
    {
        foreach (LevelExitButton exitBtn in Object.FindObjectsByType<LevelExitButton>(FindObjectsSortMode.None))
        {
            SerializedObject so = new SerializedObject(exitBtn);
            SerializedProperty prop = so.FindProperty("loadNextSceneInBuild");
            if (prop != null && !prop.boolValue)
            {
                prop.boolValue = true;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(exitBtn);
                log.Add($"LevelExitButton '{exitBtn.name}': loadNextSceneInBuild = true.");
            }
        }
    }

    private static Transform GetOrCreateGroup(string groupName)
    {
        GameObject go = GameObject.Find(groupName);
        if (go == null) go = new GameObject(groupName);
        return go.transform;
    }

    private static void CreateGuideBetween(string stripName, Transform parent, string fromName, string toName, Color tint)
    {
        GameObject a = GameObject.Find(fromName);
        GameObject b = GameObject.Find(toName);
        if (a == null || b == null) return;
        Vector3 pA = new Vector3(a.transform.position.x, 0.03f, a.transform.position.z);
        Vector3 pB = new Vector3(b.transform.position.x, 0.03f, b.transform.position.z);
        Vector3 delta = pB - pA;
        float len = delta.magnitude;
        if (len < 0.2f) return;

        GameObject strip = CreateFloorStrip(stripName, parent, (pA + pB) * 0.5f, new Vector3(0.18f, 0.025f, len), tint);
        strip.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
    }

    private static GameObject CreateFloorStrip(string stripName, Transform parent, Vector3 pos, Vector3 scale, Color tint)
    {
        GameObject strip = GameObject.Find(stripName);
        if (strip == null)
        {
            strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = stripName;
            Object.DestroyImmediate(strip.GetComponent<Collider>());
        }
        strip.transform.SetParent(parent, true);
        strip.transform.position = pos;
        strip.transform.localScale = scale;
        Renderer r = strip.GetComponent<Renderer>();
        if (r != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
            mat.color = tint;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
            r.sharedMaterial = mat;
        }
        EditorUtility.SetDirty(strip);
        return strip;
    }
}
