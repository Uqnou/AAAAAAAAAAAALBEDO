using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Cria e mantém o prefab de Luz Pronta (ALBEDO_LightSpot.prefab), atualiza o GodRay_Mystic.prefab
/// e adiciona GodRays volumétricos + LightSource + Trigger às luzes da cena atual.
/// </summary>
[InitializeOnLoad]
public static class AlbedoLightPrefabBuilder
{
    private const string GodRayPrefabPath = "Assets/GodRay_Mystic.prefab";
    private const string LightPrefabFolder = "Assets/Prefabs";
    private const string LightPrefabPath = "Assets/Prefabs/ALBEDO_LightSpot.prefab";
    private const string BlackFireMatPath = "Assets/Materials/BlackFire.mat";
    private const string SombraMatPath = "Assets/Materials/Sombra 1.mat";
    private const string FogoMatPath = "Assets/Materials/Fogo.mat";

    static AlbedoLightPrefabBuilder()
    {
        EditorApplication.delayCall += EnsurePrefabsOnLoad;
    }

    private static void EnsurePrefabsOnLoad()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        EnsureBlackFireMaterial();
        GameObject lightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LightPrefabPath);
        if (lightPrefab == null || lightPrefab.GetComponentInChildren<CapsuleCollider>(true) != null || lightPrefab.GetComponentInChildren<Rigidbody>(true) != null)
        {
            BuildLightSpotPrefabInternal(logResult: false);
        }
        else
        {
            EnsureGodRayPrefabReady();
        }
    }

    [MenuItem("ALBEDO/3. Aplicar Materiais de Sombra e Fogo Preto na Cena")]
    public static void ApplyShadowAndBlackFireMaterials()
    {
        Material blackFireMat = EnsureBlackFireMaterial();
        Material sombraMat = AssetDatabase.LoadAssetAtPath<Material>(SombraMatPath);
        Material fogoMat = AssetDatabase.LoadAssetAtPath<Material>(FogoMatPath);

        int shadowCount = 0, fireCount = 0;
        foreach (MeshRenderer mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (mr == null) continue;
            string nm = mr.gameObject.name;
            if (nm.StartsWith("Sombra", System.StringComparison.OrdinalIgnoreCase) && sombraMat != null)
            {
                Undo.RecordObject(mr, "Assign Sombra Material");
                Material[] mats = mr.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = sombraMat;
                mr.sharedMaterials = mats;
                EditorUtility.SetDirty(mr);
                shadowCount++;
            }
            else if (blackFireMat != null && (nm.StartsWith("Chama", System.StringComparison.OrdinalIgnoreCase) ||
                     nm == "Sphere.001" || nm == "Sphere.002" || nm == "Sphere.006" || nm == "Sphere.007" ||
                     (fogoMat != null && System.Array.IndexOf(mr.sharedMaterials, fogoMat) >= 0)))
            {
                Undo.RecordObject(mr, "Assign BlackFire Material");
                Material[] mats = mr.sharedMaterials;
                if (mats.Length > 0) mats[0] = blackFireMat;
                for (int i = 0; i < mats.Length; i++) if (fogoMat != null && mats[i] == fogoMat) mats[i] = blackFireMat;
                mr.sharedMaterials = mats;
                EditorUtility.SetDirty(mr);
                fireCount++;
            }
        }

        foreach (TorchController tc in Object.FindObjectsByType<TorchController>(FindObjectsSortMode.None))
        {
            SerializedObject so = new SerializedObject(tc);
            SerializedProperty prop = so.FindProperty("blackFireMaterial");
            if (prop != null && blackFireMat != null) { prop.objectReferenceValue = blackFireMat; so.ApplyModifiedProperties(); }
        }

        Scene active = SceneManager.GetActiveScene();
        if (active.IsValid()) EditorSceneManager.MarkSceneDirty(active);
        Debug.Log($"[ALBEDO] Materiais aplicados: {shadowCount} sombras ('Sombra 1') e {fireCount} chamas ('BlackFire').");
    }

    private static Material EnsureBlackFireMaterial()
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(BlackFireMatPath);
        Shader shader = Shader.Find("ALBEDO/BlackFire");
        if (mat == null && shader != null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
            mat = new Material(shader);
            mat.SetColor("_CoreColor", Color.black);
            mat.SetColor("_RimColor", Color.white);
            mat.SetFloat("_RimPower", 2.6f);
            mat.SetFloat("_FlickerSpeed", 6.5f);
            mat.SetFloat("_WaveAmplitude", 0.08f);
            AssetDatabase.CreateAsset(mat, BlackFireMatPath);
            AssetDatabase.SaveAssets();
        }
        return mat;
    }

    [MenuItem("ALBEDO/1. Criar ou Atualizar Prefab de Luz Pronta")]
    public static void BuildLightSpotPrefab() => BuildLightSpotPrefabInternal(logResult: true);

    [MenuItem("ALBEDO/2. Adicionar GodRays as Luzes da Cena")]
    public static void ApplyGodRaysToSceneLights()
    {
        GameObject godRayPrefab = EnsureGodRayPrefabReady();
        if (godRayPrefab == null)
        {
            Debug.LogError($"[ALBEDO] Prefab '{GodRayPrefabPath}' não encontrado.");
            return;
        }

        int lightLayer = LayerMask.NameToLayer("Light");
        if (lightLayer == -1) lightLayer = 6;

        int updated = 0;
        Light[] allLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (Light lt in allLights)
        {
            if (lt == null || lt.type == LightType.Directional) continue;
            if (lt.GetComponentInParent<PrismaController>() != null) continue;
            if (lt.GetComponentInParent<GodRayBeam>() != null) continue;

            ConfigureLightObject(lt.gameObject, lt, godRayPrefab, lightLayer);
            updated++;
        }

        Scene active = SceneManager.GetActiveScene();
        if (active.IsValid()) EditorSceneManager.MarkSceneDirty(active);
        Debug.Log($"[ALBEDO] GodRays e LightSource configurados em {updated} luzes da cena '{active.name}'.");
    }

    private static void BuildLightSpotPrefabInternal(bool logResult)
    {
        GameObject godRayPrefab = EnsureGodRayPrefabReady();
        if (godRayPrefab == null) return;

        if (!AssetDatabase.IsValidFolder(LightPrefabFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        int lightLayer = LayerMask.NameToLayer("Light");
        if (lightLayer == -1) lightLayer = 6;

        GameObject root = new GameObject("ALBEDO_LightSpot");
        root.layer = lightLayer;
        root.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        Light spot = root.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.color = Color.white;
        spot.intensity = 2000f;
        spot.range = 25f;
        spot.spotAngle = 35f;
        spot.innerSpotAngle = 21.8f;
        spot.shadows = LightShadows.Soft;

        BoxCollider box = root.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(3.5f, 3.5f, 22f);
        box.center = new Vector3(0f, 0f, 10f);

        LightSource ls = root.AddComponent<LightSource>();
        ls.SetLightColor(Color.white);

        GameObject grObj = (GameObject)PrefabUtility.InstantiatePrefab(godRayPrefab, root.transform);
        grObj.name = "GodRay_Mystic";
        grObj.layer = lightLayer;
        grObj.transform.localPosition = Vector3.zero;
        grObj.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        grObj.transform.localScale = Vector3.one;

        GodRayBeam beam = grObj.GetComponent<GodRayBeam>();
        if (beam != null)
        {
            beam.enabled = true;
            beam.SetColor(Color.white);
        }

        PrefabUtility.SaveAsPrefabAsset(root, LightPrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();

        if (logResult)
        {
            Debug.Log($"[ALBEDO] Prefab de luz pronta criado/atualizado em '{LightPrefabPath}'.");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(LightPrefabPath);
        }
    }

    private static GameObject EnsureGodRayPrefabReady()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GodRayPrefabPath);
        if (prefab == null) return null;

        int lightLayer = LayerMask.NameToLayer("Light");
        if (lightLayer == -1) lightLayer = 6;

        GameObject contents = PrefabUtility.LoadPrefabContents(GodRayPrefabPath);
        bool dirty = false;

        if (contents.layer != lightLayer) { contents.layer = lightLayer; dirty = true; }
        GodRayBeam beam = contents.GetComponent<GodRayBeam>();
        if (beam != null && !beam.enabled) { beam.enabled = true; dirty = true; }

        CapsuleCollider cap = contents.GetComponent<CapsuleCollider>();
        if (cap != null) { Object.DestroyImmediate(cap, true); dirty = true; }

        Rigidbody rb = contents.GetComponent<Rigidbody>();
        if (rb != null) { Object.DestroyImmediate(rb, true); dirty = true; }

        GodRayTriggerZone tz = contents.GetComponent<GodRayTriggerZone>();
        if (tz != null) { Object.DestroyImmediate(tz, true); dirty = true; }

        if (contents.GetComponent<LightSource>() == null) { contents.AddComponent<LightSource>(); dirty = true; }

        if (dirty) PrefabUtility.SaveAsPrefabAsset(contents, GodRayPrefabPath);
        PrefabUtility.UnloadPrefabContents(contents);
        return AssetDatabase.LoadAssetAtPath<GameObject>(GodRayPrefabPath);
    }

    private static void ConfigureLightObject(GameObject go, Light lt, GameObject godRayPrefab, int lightLayer)
    {
        Undo.RecordObject(go, "Configure Light & GodRay");
        go.layer = lightLayer;

        BoxCollider box = go.GetComponent<BoxCollider>();
        if (box == null && go.GetComponent<Collider>() == null)
        {
            box = Undo.AddComponent<BoxCollider>(go);
            box.size = new Vector3(3.5f, 3.5f, 22f);
            box.center = new Vector3(0f, 0f, 10f);
        }
        if (box != null)
        {
            box.isTrigger = true;
            box.size = new Vector3(Mathf.Abs(box.size.x), Mathf.Abs(box.size.y), Mathf.Abs(box.size.z));
        }

        if (go.GetComponent<PickableObject>() == null)
        {
            Rigidbody rootRb = go.GetComponent<Rigidbody>();
            if (rootRb != null)
            {
                Undo.DestroyObjectImmediate(rootRb);
            }
        }

        LightSource ls = go.GetComponent<LightSource>() ?? Undo.AddComponent<LightSource>(go);
        ls.SetLightColor(lt.color);

        GodRayBeam existingBeam = go.GetComponentInChildren<GodRayBeam>(true);
        if (existingBeam == null)
        {
            GameObject grObj = (GameObject)PrefabUtility.InstantiatePrefab(godRayPrefab, go.transform);
            grObj.name = "GodRay_Mystic";
            grObj.layer = lightLayer;
            grObj.transform.localPosition = Vector3.zero;
            grObj.transform.localRotation = Quaternion.FromToRotation(Vector3.down, Vector3.forward);
            grObj.transform.localScale = Vector3.one;
            existingBeam = grObj.GetComponent<GodRayBeam>();
            Undo.RegisterCreatedObjectUndo(grObj, "Add GodRay to Light");
        }

        if (existingBeam != null)
        {
            GameObject beamGo = existingBeam.gameObject;
            existingBeam.enabled = true;
            beamGo.layer = lightLayer;

            CapsuleCollider cap = beamGo.GetComponent<CapsuleCollider>();
            if (cap != null) Undo.DestroyObjectImmediate(cap);

            Rigidbody beamRb = beamGo.GetComponent<Rigidbody>();
            if (beamRb != null) Undo.DestroyObjectImmediate(beamRb);

            GodRayTriggerZone tz = beamGo.GetComponent<GodRayTriggerZone>();
            if (tz != null) Undo.DestroyObjectImmediate(tz);

            if (beamGo.GetComponent<LightSource>() == null) Undo.AddComponent<LightSource>(beamGo);

            SerializedObject so = new SerializedObject(existingBeam);
            SerializedProperty tcProp = so.FindProperty("triggerCollider");
            if (tcProp != null) { tcProp.objectReferenceValue = null; so.ApplyModifiedPropertiesWithoutUndo(); }

            existingBeam.SetColor(lt.color);
            existingBeam.UpdateHeightWithRaycast();
            EditorUtility.SetDirty(beamGo);
        }
        EditorUtility.SetDirty(go);
    }
}
