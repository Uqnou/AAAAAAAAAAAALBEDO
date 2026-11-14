using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Presets de cor de luz/prisma selecionáveis no Inspector para facilitar criação de novos levels.
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
/// Utilitários compartilhados de mistura aditiva RGB e correspondência de cores para prismas e sombras.
/// </summary>
public static class AlbedoColorUtility
{
    public const float DefaultColorTolerance = 0.25f;

    public static Color PresetToColor(AlbedoColorPreset preset, Color customColor)
    {
        switch (preset)
        {
            case AlbedoColorPreset.White:
                return new Color(1f, 1f, 1f, 1f);
            case AlbedoColorPreset.Red:
                return new Color(1f, 0f, 0f, 1f);
            case AlbedoColorPreset.Green:
                return new Color(0f, 1f, 0f, 1f);
            case AlbedoColorPreset.Blue:
                return new Color(0f, 0f, 1f, 1f);
            case AlbedoColorPreset.Yellow:
                return new Color(1f, 1f, 0f, 1f);
            case AlbedoColorPreset.Cyan:
                return new Color(0f, 1f, 1f, 1f);
            case AlbedoColorPreset.Magenta:
                return new Color(1f, 0f, 1f, 1f);
            case AlbedoColorPreset.Custom:
            default:
                return new Color(Mathf.Clamp01(customColor.r), Mathf.Clamp01(customColor.g), Mathf.Clamp01(customColor.b), 1f);
        }
    }

    public static Color CombineAdditive(IEnumerable<Color> colors)
    {
        if (colors == null)
            return Color.black;

        float r = 0f;
        float g = 0f;
        float b = 0f;

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
        return new Color(
            Mathf.Clamp01(a.r + b.r),
            Mathf.Clamp01(a.g + b.g),
            Mathf.Clamp01(a.b + b.b),
            1f);
    }

    /// <summary>
    /// Verifica se a cor recebida abre uma barreira que exige <paramref name="required"/>.
    /// Quando <paramref name="requireExactChannels"/> é verdadeiro, canais extras também bloqueiam
    /// (por exemplo, luz branca não abre barreira amarela, exigindo Vermelho + Verde).
    /// </summary>
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
}
