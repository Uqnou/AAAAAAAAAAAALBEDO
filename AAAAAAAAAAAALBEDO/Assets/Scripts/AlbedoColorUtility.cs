using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Presets de cor de luz/prisma selecionáveis no Inspector para criação e configuração de fases.
/// </summary>
public enum AlbedoColorPreset
{
    Custom = 0,
    White = 1,
    Red = 2,
    Green = 3,
    Blue = 4,
    Yellow = 5,
    Cyan = 6,
    Magenta = 7
}

/// <summary>
/// Modo de combinação da cor do feixe emitido pelo prisma.
/// </summary>
public enum PrismColorMode
{
    [Tooltip("Se o prisma tiver cor diferente de branco, ela tinge/soma à luz. Se for branco, usa a cor recebida.")]
    PrismTint = 0,

    [Tooltip("Multiplicação física de cores.")]
    Multiply = 1,

    [Tooltip("Usa sempre a cor do prisma se diferente de branco, caso contrário a cor recebida.")]
    PrismOverrides = 2,

    [Tooltip("Usa diretamente a cor da luz ou prismas que o atingiram.")]
    LightColorDirect = 3
}

/// <summary>
/// Modo físico e visual de uma Sombra no cenário.
/// - Barrier: bloqueia o caminho quando escura; abre quando atingida pela cor de luz exigida.
/// - WalkableBridge: passarela caminhável no escuro; quebrada se atingida por qualquer luz ativa.
/// </summary>
public enum ShadowMode
{
    Barrier = 0,
    WalkableBridge = 1
}

/// <summary>
/// Utilitários compartilhados de mistura aditiva RGB, correspondência de cores e leitura de materiais.
/// </summary>
public static class AlbedoColorUtility
{
    public const float DefaultColorTolerance = 0.25f;

    public static Color PresetToColor(AlbedoColorPreset preset, Color customColor)
    {
        switch (preset)
        {
            case AlbedoColorPreset.White: return new Color(1f, 1f, 1f, 1f);
            case AlbedoColorPreset.Red: return new Color(1f, 0f, 0f, 1f);
            case AlbedoColorPreset.Green: return new Color(0f, 1f, 0f, 1f);
            case AlbedoColorPreset.Blue: return new Color(0f, 0f, 1f, 1f);
            case AlbedoColorPreset.Yellow: return new Color(1f, 1f, 0f, 1f);
            case AlbedoColorPreset.Cyan: return new Color(0f, 1f, 1f, 1f);
            case AlbedoColorPreset.Magenta: return new Color(1f, 0f, 1f, 1f);
            case AlbedoColorPreset.Custom:
            default:
                return new Color(Mathf.Clamp01(customColor.r), Mathf.Clamp01(customColor.g), Mathf.Clamp01(customColor.b), 1f);
        }
    }

    public static Color CombineAdditive(IEnumerable<Color> colors)
    {
        if (colors == null) return Color.black;
        float r = 0f, g = 0f, b = 0f;
        foreach (Color c in colors)
        {
            r += Mathf.Max(0f, c.r);
            g += Mathf.Max(0f, c.g);
            b += Mathf.Max(0f, c.b);
        }
        return new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), 1f);
    }

    public static Color AddColors(Color a, Color b)
    {
        return new Color(Mathf.Clamp01(a.r + b.r), Mathf.Clamp01(a.g + b.g), Mathf.Clamp01(a.b + b.b), 1f);
    }

    public static bool IsNeutralLight(Color c, float maxSpread = 0.22f, float minBrightness = 0.25f)
    {
        float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
        return max >= minBrightness && (max - min) <= maxSpread;
    }

    public static bool ColorsMatch(
        Color received,
        Color required,
        bool requireExactChannels = true,
        float tolerance = DefaultColorTolerance)
    {
        if (requireExactChannels)
        {
            return Mathf.Abs(received.r - required.r) <= tolerance
                && Mathf.Abs(received.g - required.g) <= tolerance
                && Mathf.Abs(received.b - required.b) <= tolerance;
        }

        return received.r + tolerance >= required.r
            && received.g + tolerance >= required.g
            && received.b + tolerance >= required.b;
    }

    public static AlbedoColorPreset InferPreset(Color c, float tolerance = 0.15f)
    {
        if (ColorsMatch(c, Color.white, true, tolerance)) return AlbedoColorPreset.White;
        if (ColorsMatch(c, Color.red, true, tolerance)) return AlbedoColorPreset.Red;
        if (ColorsMatch(c, Color.green, true, tolerance)) return AlbedoColorPreset.Green;
        if (ColorsMatch(c, Color.blue, true, tolerance)) return AlbedoColorPreset.Blue;
        if (ColorsMatch(c, new Color(1f, 1f, 0f, 1f), true, tolerance)) return AlbedoColorPreset.Yellow;
        if (ColorsMatch(c, Color.cyan, true, tolerance)) return AlbedoColorPreset.Cyan;
        if (ColorsMatch(c, Color.magenta, true, tolerance)) return AlbedoColorPreset.Magenta;
        return AlbedoColorPreset.Custom;
    }

    public static Color ComputeEmittedColor(Color intrinsicColor, Color incomingColor, PrismColorMode mode)
    {
        bool intrinsicIsWhite = ColorsMatch(intrinsicColor, Color.white, true, 0.05f);
        bool incomingIsNeutral = ColorsMatch(incomingColor, Color.white, true, 0.05f) || IsNeutralLight(incomingColor);

        switch (mode)
        {
            case PrismColorMode.PrismOverrides:
                return !intrinsicIsWhite ? intrinsicColor : incomingColor;
            case PrismColorMode.LightColorDirect:
                return incomingColor;
            case PrismColorMode.Multiply:
                return new Color(
                    Mathf.Clamp01(incomingColor.r * intrinsicColor.r),
                    Mathf.Clamp01(incomingColor.g * intrinsicColor.g),
                    Mathf.Clamp01(incomingColor.b * intrinsicColor.b),
                    1f);
            case PrismColorMode.PrismTint:
            default:
                if (intrinsicIsWhite) return incomingColor;
                if (incomingIsNeutral) return intrinsicColor;
                return AddColors(incomingColor, intrinsicColor);
        }
    }

    public static bool TryExtractObjectColor(GameObject obj, out Color color)
    {
        color = Color.white;
        if (obj == null) return false;
        var gr = obj.GetComponent<GodRayBeam>() ?? obj.GetComponentInParent<GodRayBeam>() ?? obj.GetComponentInChildren<GodRayBeam>();
        if (gr != null) { color = gr.CurrentColor; return true; }
        var ls = obj.GetComponent<LightSource>() ?? obj.GetComponentInParent<LightSource>() ?? obj.GetComponentInChildren<LightSource>();
        if (ls != null) { color = ls.LightColor; return true; }
        var l = obj.GetComponent<Light>() ?? obj.GetComponentInParent<Light>() ?? obj.GetComponentInChildren<Light>();
        if (l != null) { color = l.color; return true; }
        var r = obj.GetComponent<Renderer>() ?? obj.GetComponentInParent<Renderer>() ?? obj.GetComponentInChildren<Renderer>();
        if (r != null && r.sharedMaterial != null)
        {
            Material m = r.sharedMaterial;
            if (m.HasProperty("_TintColor")) { color = m.GetColor("_TintColor"); return true; }
            if (m.HasProperty("_EmissionColor")) { color = m.GetColor("_EmissionColor"); return true; }
            if (m.HasProperty("_BaseColor")) { color = m.GetColor("_BaseColor"); return true; }
            if (m.HasProperty("_Color")) { color = m.GetColor("_Color"); return true; }
            color = m.color;
            return true;
        }
        return false;
    }

    public static void ApplyMaterialIndicator(Renderer r, int slot, Color color, bool setEmission = false)
    {
        if (r == null || r.sharedMaterials == null || r.sharedMaterials.Length == 0) return;
        int idx = Mathf.Clamp(slot, 0, r.sharedMaterials.Length - 1);
        var mpb = new MaterialPropertyBlock();
        r.GetPropertyBlock(mpb, idx);
        mpb.SetColor("_BaseColor", color);
        mpb.SetColor("_Color", color);
        if (setEmission) mpb.SetColor("_EmissionColor", color * 0.65f);
        r.SetPropertyBlock(mpb, idx);
    }

    public static bool TryDetectPrismIntrinsicColor(GameObject go, out Color detected)
    {
        detected = Color.white;
        if (go == null) return false;
        Renderer r = go.GetComponent<Renderer>() ?? go.GetComponentInChildren<Renderer>();
        if (r == null || r.sharedMaterials == null) return false;

        for (int i = r.sharedMaterials.Length - 1; i >= 0; i--)
        {
            Material m = r.sharedMaterials[i];
            if (m == null) continue;
            Color c = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor")
                    : (m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white);
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            if (max > 0.4f && (max - min) > 0.25f)
            {
                detected = new Color(c.r > 0.5f ? 1f : 0f, c.g > 0.5f ? 1f : 0f, c.b > 0.5f ? 1f : 0f, 1f);
                return true;
            }
        }
        return false;
    }
}
