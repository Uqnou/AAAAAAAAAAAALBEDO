using UnityEngine;
using UnityEditor;

public class BlenderLightSetup : Editor
{
    private struct LightConfig
    {
        public string name;
        public Vector3 position;
        public Vector3 eulerAngles;
        public Color color;
        public float intensity;
        public float spotAngle;

        public LightConfig(string n, Vector3 pos, Vector3 rot, Color c, float inten, float angle)
        {
            name = n;
            position = pos;
            eulerAngles = rot;
            color = c;
            intensity = inten;
            spotAngle = angle;
        }
    }

    [MenuItem("Tools/Setup Blender Lights In Scene")]
    public static void SetupLights()
    {
        LightConfig[] configs = new[]
        {
            // Spot lights from Blender
            new LightConfig("Point", new Vector3(0.015f, 7.09f, -3.331f), new Vector3(90f, 0f, 0f), Color.white, 25f, 45f),
            new LightConfig("Point.001", new Vector3(1.176f, 9.298f, 18.465f), new Vector3(90f, 0f, 0f), Color.white, 25f, 15f),
            new LightConfig("Point.002", new Vector3(-12.788f, 7.09f, 25.269f), new Vector3(90f, 0f, 0f), Color.white, 25f, 25f),
            new LightConfig("Point.003", new Vector3(32.599f, 9.111f, 24.24f), new Vector3(90f, 0f, 0f), Color.white, 25f, 30f),
            new LightConfig("Point.004", new Vector3(-29.008f, 8.749f, 40.2f), new Vector3(90f, 0f, 0f), Color.white, 40f, 35f),
            new LightConfig("Point.005", new Vector3(-31.83f, 7.09f, 25.937f), new Vector3(90f, 0f, 0f), Color.white, 40f, 32f),
            new LightConfig("Point.006", new Vector3(-18.228f, 7.09f, 36.491f), new Vector3(90f, 0f, 0f), Color.white, 25f, 28f),

            // Colored Glass Caustic Spots
            new LightConfig("glass caustics R.001", new Vector3(0.175f, 1.443f, 1.525f), new Vector3(72f, -1.3f, 4.1f), Color.red, 30f, 45f),
            new LightConfig("glass caustics G.001", new Vector3(0.182f, 1.443f, 1.525f), new Vector3(72f, -1.3f, 4.1f), Color.green, 30f, 45f),
            new LightConfig("glass caustics B.001", new Vector3(0.181f, 1.443f, 1.525f), new Vector3(72f, -1.3f, 4.1f), Color.blue, 30f, 45f),
            new LightConfig("glass caustics R.002", new Vector3(1.185f, 2.252f, 23.635f), new Vector3(88f, -0.2f, 4.3f), Color.red, 100f, 20f)
        };

        GameObject lightsParent = GameObject.Find("Blender_Lights");
        if (lightsParent == null)
        {
            lightsParent = new GameObject("Blender_Lights");
            Undo.RegisterCreatedObjectUndo(lightsParent, "Create Blender Lights Parent");
        }

        foreach (var c in configs)
        {
            Transform existing = lightsParent.transform.Find(c.name);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject(c.name);
                go.transform.SetParent(lightsParent.transform);
                Undo.RegisterCreatedObjectUndo(go, "Create Light " + c.name);
            }

            go.transform.position = c.position;
            go.transform.eulerAngles = c.eulerAngles;

            Light l = go.GetComponent<Light>();
            if (l == null) l = go.AddComponent<Light>();

            l.type = LightType.Spot;
            l.color = c.color;
            l.intensity = c.intensity;
            l.spotAngle = c.spotAngle;
            l.range = 35f;
            l.shadows = LightShadows.Soft;

            EditorUtility.SetDirty(go);
        }

        Debug.Log("Luzes do Blender sincronizadas e configuradas com sucesso na cena do Unity!");
    }
}
