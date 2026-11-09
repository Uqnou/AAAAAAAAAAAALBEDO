using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class BlenderMaterialAssigner : Editor
{
    private static readonly Dictionary<string, string[]> ObjectMaterialMapping = new Dictionary<string, string[]>
    {
        {"Cube", new []{"Parede2", "Outline"}},
        {"Cube.001", new []{"Material.007"}},
        {"Cube.002", new []{"Outline", "Parede1 (ajustavel)"}},
        {"Cube.003", new []{"Parede1 (ajustavel)", "Parede2", "Outline"}},
        {"Cube.004", new []{"Sombra 1"}},
        {"Cube.005", new []{"Material.006"}},
        {"Cube.006", new []{"Parede1 (ajustavel)", "Parede2", "Outline"}},
        {"Cube.007", new []{"Parede1 (ajustavel)", "Parede2", "Outline"}},
        {"Cube.008", new []{"Sombra 1"}},
        {"Cube.009", new []{"Sombra 1"}},
        {"Cube.010", new []{"Parede1 (ajustavel)", "Parede2", "Outline"}},
        {"Cube.011", new []{"Parede1 (ajustavel)", "Parede2", "Outline"}},
        {"Cube.012", new []{"Parede2", "Outline"}},
        {"Cube.013", new []{"Parede1 (ajustavel)", "Parede2", "Outline"}},
        {"Cube.014", new []{"Parede2", "Outline"}},
        {"Cube.015", new []{"Outline", "Parede2"}},
        {"Cube.016", new []{"Parede2", "Outline"}},
        {"Cube.017", new []{"Material.008"}},
        {"Cube.018", new []{"Sombra 1"}},
        {"Cube.019", new []{"Parede1 (ajustavel)", "Parede2", "Outline"}},
        {"Cube.020", new []{"Material.007"}},
        {"Cube.021", new []{"Outline", "Parede1 (ajustavel)"}},
        {"Cube.022", new []{"Parede1 (ajustavel)", "Parede2", "Outline"}},
        {"Cube.023", new []{"Outline", "Parede1 (ajustavel)"}},
        {"Cube.024", new []{"Parede2", "Outline"}},
        {"Cube.025", new []{"Parede1 (ajustavel)", "Parede2", "Outline"}},
        {"Cube.026", new []{"Outline", "Parede1 (ajustavel)"}},
        {"Cube.027", new []{"Parede1 (ajustavel)", "Parede2", "Outline"}},
        {"Cube.028", new []{"Parede1 (ajustavel)", "Parede2", "Outline"}},
        {"Cube.029", new []{"Outline", "Parede1 (ajustavel)"}},
        {"Cube.030", new []{"Parede1 (ajustavel)", "Parede2", "Outline"}},
        {"Cube.031", new []{"Parede1 (ajustavel)", "Parede2", "Outline"}},
        {"Cube.032", new []{"Outline", "Parede1 (ajustavel)"}},
        {"Cube.033", new []{"Parede1 (ajustavel)", "Parede2", "Outline"}},
        {"Cube.034", new []{"Outline", "Parede1 (ajustavel)"}},
        {"Cube.035", new []{"Parede1 (ajustavel)", "Parede2", "Outline"}},
        {"Cube.036", new []{"Parede1 (ajustavel)", "Parede2", "Outline"}},
        {"Cube.037", new []{"Parede1 (ajustavel)", "Parede2", "Outline"}},
        {"Cube.038", new []{"Parede1 (ajustavel)", "Parede2", "Outline"}},
        {"Cube.039", new []{"Outline", "Parede1 (ajustavel)"}},
        {"Cube.040", new []{"Parede1 (ajustavel)", "Parede2", "Outline"}},
        {"Cube.041", new []{"Outline", "Parede1 (ajustavel)"}},
        {"Cube.042", new []{"Parede1 (ajustavel)", "Parede2", "Outline"}},
        {"Cube.043", new []{"Parede1 (ajustavel)", "Parede2", "Outline"}},
        {"Cube.044", new []{"Parede1 (ajustavel)", "Parede2", "Outline"}},
        {"Cube.045", new []{"Outline", "Parede1 (ajustavel)"}},
        {"Cube.046", new []{"Parede1 (ajustavel)", "Parede2", "Outline"}},
        {"Cube.047", new []{"Outline", "Parede2"}},
        {"Cube.048", new []{"Parede2", "Outline"}},
        {"Cube.049", new []{"Parede1 (ajustavel)", "Parede2", "Outline"}},
        {"Cube.050", new []{"Outline", "Parede1 (ajustavel)"}},
        {"Cube.051", new []{"Outline", "Parede1 (ajustavel)"}},
        {"Cube.052", new []{"Parede1 (ajustavel)", "Parede2", "Outline"}},
        {"Cube.053", new []{"Outline", "Parede2"}},
        {"Cube.054", new []{"Parede2", "Outline"}},
        {"Cube.055", new []{"Parede1 (ajustavel)", "Outline"}},
        {"Cube.056", new []{"Parede1 (ajustavel)", "Parede2", "Outline"}},
        {"Cube.340", new []{"Parede1 (ajustavel)", "Parede2", "Outline"}},
        {"Cylinder", new []{"Parede1 (ajustavel)", "Outline"}},
        {"Cylinder.001", new []{"Parede1 (ajustavel)", "Parede2", "Outline"}},
        {"Cylinder.003", new []{"Parede1 (ajustavel)", "Parede2", "Vidro 3"}},
        {"Cylinder.006", new []{"Parede1 (ajustavel)", "Parede2", "Outline"}},
        {"Pilar", new []{"Parede1 (ajustavel)", "Outline"}},
        {"Pilar.001", new []{"Parede1 (ajustavel)", "Outline"}},
        {"Pilar.002", new []{"Parede1 (ajustavel)", "Outline"}},
        {"Pilar.003", new []{"Parede1 (ajustavel)", "Outline"}},
        {"Pilar.004", new []{"Parede1 (ajustavel)", "Outline"}},
        {"Pilar.005", new []{"Parede1 (ajustavel)", "Outline"}},
        {"Pilar.006", new []{"Parede1 (ajustavel)", "Outline"}},
        {"Prisma", new []{"Vidro 2", "Vidro 3"}},
        {"Prisma.001", new []{"Vidro 2", "Vidro 4"}},
        {"Prisma.002", new []{"Vidro 2", "Vidro 5"}},
        {"Prisma.003", new []{"Vidro 2", "Vidro 6"}},
        {"Sphere.001", new []{"Fogo"}},
        {"Sphere.002", new []{"Fogo", "Outline"}},
        {"Sphere.003", new []{"Material.004"}},
        {"Sphere.004", new []{"Material.007"}},
        {"Sphere.006", new []{"Fogo", "Outline"}},
        {"Sphere.007", new []{"Fogo", "Outline"}}
    };

    [MenuItem("Tools/Apply Blender Materials To Scene")]
    public static void ApplyMaterialsToScene()
    {
        CreateBlenderMaterials.GenerateMaterials();

        MeshRenderer[] renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
        int matched = 0;

        foreach (var rend in renderers)
        {
            string objName = rend.gameObject.name;
            if (ObjectMaterialMapping.ContainsKey(objName))
            {
                string[] matNames = ObjectMaterialMapping[objName];
                Material[] newMats = new Material[matNames.Length];

                for (int i = 0; i < matNames.Length; i++)
                {
                    string path = $"Assets/Materials/{matNames[i]}.mat";
                    Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (m != null)
                    {
                        newMats[i] = m;
                    }
                }

                Undo.RecordObject(rend, "Assign Blender Materials");
                rend.sharedMaterials = newMats;
                EditorUtility.SetDirty(rend);
                matched++;
            }
        }

        // Configure Ambient & Environment Lighting to match Blender (Dark comic aesthetic)
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.05f, 0.05f, 0.05f, 1.0f);

        Debug.Log($"Materiais aplicados com sucesso a {matched} objetos na cena!");
    }
}
