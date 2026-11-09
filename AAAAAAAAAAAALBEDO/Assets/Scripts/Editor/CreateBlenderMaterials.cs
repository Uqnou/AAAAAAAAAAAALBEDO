using UnityEngine;
using UnityEditor;
using System.IO;

public class CreateBlenderMaterials : Editor
{
    [MenuItem("Tools/Generate Blender Materials")]
    public static void GenerateMaterials()
    {
        string dir = "Assets/Materials";
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        Shader parede1Shader = Shader.Find("Custom/URP_Parede1_Adjustable");
        Shader parede2Shader = Shader.Find("Custom/URP_Parede2_HalftoneDots");
        Shader fireShader = Shader.Find("Custom/URP_StylizedFire");
        Shader glassShader = Shader.Find("Custom/URP_StylizedGlass");
        Shader shadowShader = Shader.Find("Custom/URP_StylizedShadow");
        Shader outlineShader = Shader.Find("Custom/URP_Outline");

        // 1. Parede1 (ajustavel) - Voronoi Procedural Hatching
        CreateOrUpdateMaterial("Parede1 (ajustavel)", parede1Shader, mat =>
        {
            mat.SetColor("_LightColor", Color.white);
            mat.SetColor("_DarkColor", Color.black);
            mat.SetFloat("_Scale", 0.07f);
            mat.SetFloat("_AspectRatio", 0.55f);
            mat.SetFloat("_VoronoiScale", 5.0f);
            mat.SetFloat("_RampStart", 0.268f);
            mat.SetFloat("_RampEnd", 0.300f);
        });

        // 2. Parede2 - Halftone Screen Dots
        CreateOrUpdateMaterial("Parede2", parede2Shader, mat =>
        {
            mat.SetColor("_DotColor", Color.white);
            mat.SetColor("_BackgroundColor", Color.black);
            mat.SetFloat("_Frequency", 100.0f);
            mat.SetFloat("_AspectRatio", 1.7777778f);
            mat.SetFloat("_FromMin", -1.5f);
            mat.SetFloat("_FromMax", 2.1f);
            mat.SetFloat("_Steps", 4.0f);
        });

        // 3. Fogo
        CreateOrUpdateMaterial("Fogo", fireShader, mat =>
        {
            mat.SetColor("_EmissionColor", new Color(1.0f, 0.2f, 0.05f, 1.0f));
            mat.SetFloat("_EmissionStrength", 2.5f);
            mat.SetVector("_NoiseOffset", new Vector4(0f, 0f, -0.4f, 0f));
            mat.SetFloat("_VoronoiScale", 0.85f);
            mat.SetFloat("_Cutoff", 0.391f);
        });

        // 4. Material.004 (Red laser beam)
        CreateOrUpdateMaterial("Material.004", fireShader, mat =>
        {
            mat.SetColor("_EmissionColor", new Color(1.0f, 0.05f, 0.0f, 1.0f));
            mat.SetFloat("_EmissionStrength", 4.0f);
            mat.SetVector("_NoiseOffset", new Vector4(-0.8f, 0f, 0f, 0f));
            mat.SetFloat("_VoronoiScale", 0.85f);
            mat.SetFloat("_Cutoff", 0.486f);
        });

        // 5. Material.006 (Green laser beam)
        CreateOrUpdateMaterial("Material.006", fireShader, mat =>
        {
            mat.SetColor("_EmissionColor", new Color(0.0f, 1.0f, 0.05f, 1.0f));
            mat.SetFloat("_EmissionStrength", 4.0f);
            mat.SetVector("_NoiseOffset", Vector4.zero);
            mat.SetFloat("_VoronoiScale", 0.85f);
            mat.SetFloat("_Cutoff", 0.645f);
        });

        // 6. Material.007 (Blue laser beam)
        CreateOrUpdateMaterial("Material.007", fireShader, mat =>
        {
            mat.SetColor("_EmissionColor", new Color(0.05f, 0.1f, 1.0f, 1.0f));
            mat.SetFloat("_EmissionStrength", 4.0f);
            mat.SetVector("_NoiseOffset", Vector4.zero);
            mat.SetFloat("_VoronoiScale", 0.85f);
            mat.SetFloat("_Cutoff", 0.627f);
        });

        // 7. Material.008 (White/Yellow intense beam)
        CreateOrUpdateMaterial("Material.008", fireShader, mat =>
        {
            mat.SetColor("_EmissionColor", new Color(1.0f, 0.95f, 0.7f, 1.0f));
            mat.SetFloat("_EmissionStrength", 5.2f);
            mat.SetVector("_NoiseOffset", Vector4.zero);
            mat.SetFloat("_VoronoiScale", 0.85f);
            mat.SetFloat("_Cutoff", 0.5f);
        });

        // 8. Sombra 1
        CreateOrUpdateMaterial("Sombra 1", shadowShader, mat =>
        {
            mat.SetColor("_ShadowColor", new Color(0.002f, 0.002f, 0.002f, 0.85f));
            mat.SetVector("_NoiseOffset", new Vector4(0f, 0f, 0.2f, 0f));
            mat.SetFloat("_VoronoiScale", 0.85f);
            mat.SetFloat("_Cutoff", 0.518f);
        });

        // 9. Vidro 2 (Prism Glass body)
        CreateOrUpdateMaterial("Vidro 2", glassShader, mat =>
        {
            mat.SetColor("_BaseColor", new Color(0.1f, 0.1f, 0.15f, 0.2f));
            mat.SetColor("_EmissionColor", Color.white);
            mat.SetFloat("_EmissionStrength", 1.5f);
            mat.SetFloat("_FresnelPower", 2.0f);
            mat.SetFloat("_Opacity", 0.25f);
        });

        // 10. Vidro 3 (White Prism light emitter)
        CreateOrUpdateMaterial("Vidro 3", glassShader, mat =>
        {
            mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.3f));
            mat.SetColor("_EmissionColor", new Color(1.0f, 1.0f, 1.0f, 1.0f));
            mat.SetFloat("_EmissionStrength", 5.0f);
            mat.SetFloat("_FresnelPower", 1.5f);
            mat.SetFloat("_Opacity", 0.5f);
        });

        // 11. Vidro 4 (Red Prism light emitter)
        CreateOrUpdateMaterial("Vidro 4", glassShader, mat =>
        {
            mat.SetColor("_BaseColor", new Color(1f, 0.05f, 0.05f, 0.3f));
            mat.SetColor("_EmissionColor", new Color(1.0f, 0.001f, 0.0f, 1.0f));
            mat.SetFloat("_EmissionStrength", 5.0f);
            mat.SetFloat("_FresnelPower", 1.5f);
            mat.SetFloat("_Opacity", 0.5f);
        });

        // 12. Vidro 5 (Blue Prism light emitter)
        CreateOrUpdateMaterial("Vidro 5", glassShader, mat =>
        {
            mat.SetColor("_BaseColor", new Color(0.05f, 0.1f, 1.0f, 0.3f));
            mat.SetColor("_EmissionColor", new Color(0.0f, 0.001f, 1.0f, 1.0f));
            mat.SetFloat("_EmissionStrength", 5.0f);
            mat.SetFloat("_FresnelPower", 1.5f);
            mat.SetFloat("_Opacity", 0.5f);
        });

        // 13. Vidro 6 (Green Prism light emitter)
        CreateOrUpdateMaterial("Vidro 6", glassShader, mat =>
        {
            mat.SetColor("_BaseColor", new Color(0.05f, 1.0f, 0.1f, 0.3f));
            mat.SetColor("_EmissionColor", new Color(0.0f, 1.0f, 0.0f, 1.0f));
            mat.SetFloat("_EmissionStrength", 5.0f);
            mat.SetFloat("_FresnelPower", 1.5f);
            mat.SetFloat("_Opacity", 0.5f);
        });

        // 14. Outline (Branco ou Preto puro de acordo com iluminação)
        CreateOrUpdateMaterial("Outline", outlineShader, mat =>
        {
            mat.SetColor("_Color", Color.white);
        });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Materiais de alta fidelidade ao Blender gerados com sucesso!");
    }

    private static void CreateOrUpdateMaterial(string name, Shader shader, System.Action<Material> configure)
    {
        string path = $"Assets/Materials/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (shader != null)
        {
            mat.shader = shader;
        }

        configure(mat);
        EditorUtility.SetDirty(mat);
    }
}
