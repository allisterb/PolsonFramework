namespace Polson.Drawing.Skia;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using SkiaSharp;

/// <summary>
/// A CSS <c>filter</c> value, as <c>ctx.filter</c> takes it in a browser: <c>'blur(4px) grayscale(1)'</c>.
/// </summary>
/// <remarks>
/// <c>ctx.filter</c> took only an <see cref="SKImageFilter"/>, so the web's own spelling failed with a cast error
/// naming CLR types. Functions apply left to right, as in CSS. The colour matrices are the Filter Effects
/// Module's, in linear form; a function this does not know is refused by name rather than dropped.
/// </remarks>
public static partial class CssFilter
{
    #region Methods
    /// <summary>The image filter a CSS filter list describes, or null for <c>none</c> or an empty string.</summary>
    public static SKImageFilter? Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var source = text.Trim();
        if (source.Length == 0 || source.Equals("none", StringComparison.OrdinalIgnoreCase)) return null;

        SKImageFilter? chain = null;
        var at = 0;
        foreach (Match m in Function().Matches(source))
        {
            if (source[at..m.Index].Trim().Length > 0) throw Bad(source, $"'{source[at..m.Index].Trim()}' is not a filter function");
            at = m.Index + m.Length;
            var name = m.Groups["name"].Value.ToLowerInvariant();
            var arg = m.Groups["arg"].Value.Trim();
            chain = name switch
            {
                "blur" => SKImageFilter.CreateBlur(Length(arg, 0f, source), Length(arg, 0f, source), chain),
                "drop-shadow" => DropShadow(arg, chain, source),
                "brightness" => Matrix(Linear(Amount(arg, 1f, source), 0f), chain),
                "contrast" => Matrix(Linear(Amount(arg, 1f, source), 0.5f - (0.5f * Amount(arg, 1f, source))), chain),
                "invert" => Matrix(Linear(1f - (2f * Clamp01(Amount(arg, 1f, source))), Clamp01(Amount(arg, 1f, source))), chain),
                "opacity" => Matrix(Alpha(Clamp01(Amount(arg, 1f, source))), chain),
                "grayscale" => Matrix(Grayscale(Clamp01(Amount(arg, 1f, source))), chain),
                "sepia" => Matrix(Sepia(Clamp01(Amount(arg, 1f, source))), chain),
                "saturate" => Matrix(Saturate(Amount(arg, 1f, source)), chain),
                "hue-rotate" => Matrix(HueRotate(Angle(arg, source)), chain),
                _ => throw Bad(source, $"'{name}' is not a filter this canvas supports. It takes {string.Join(", ", Supported)}, or an SKImageFilter from Skia.ImageFilter")
            };
        }
        if (source[at..].Trim().Length > 0) throw Bad(source, $"'{source[at..].Trim()}' is not a filter function");
        return chain;
    }
    #endregion

    #region Fields
    /// <summary>The functions <see cref="Parse"/> understands.</summary>
    public static readonly string[] Supported =
        ["blur", "brightness", "contrast", "drop-shadow", "grayscale", "hue-rotate", "invert", "opacity", "saturate", "sepia", "none"];
    #endregion

    #region Private
    [GeneratedRegex(@"(?<name>[a-zA-Z-]+)\s*\((?<arg>(?:[^()]|\([^()]*\))*)\)")]
    private static partial Regex Function();

    static SKImageFilter Matrix(float[] m, SKImageFilter? input) => SKImageFilter.CreateColorFilter(SKColorFilter.CreateColorMatrix(m), input);

    static float[] Linear(float slope, float intercept) =>
    [
        slope, 0, 0, 0, intercept,
        0, slope, 0, 0, intercept,
        0, 0, slope, 0, intercept,
        0, 0, 0, 1, 0
    ];

    static float[] Alpha(float a) => [1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, a, 0];

    static float[] Grayscale(float a)
    {
        var s = 1f - a;
        return
        [
            0.2126f + (0.7874f * s), 0.7152f - (0.7152f * s), 0.0722f - (0.0722f * s), 0, 0,
            0.2126f - (0.2126f * s), 0.7152f + (0.2848f * s), 0.0722f - (0.0722f * s), 0, 0,
            0.2126f - (0.2126f * s), 0.7152f - (0.7152f * s), 0.0722f + (0.9278f * s), 0, 0,
            0, 0, 0, 1, 0
        ];
    }

    static float[] Sepia(float a)
    {
        var s = 1f - a;
        return
        [
            0.393f + (0.607f * s), 0.769f - (0.769f * s), 0.189f - (0.189f * s), 0, 0,
            0.349f - (0.349f * s), 0.686f + (0.314f * s), 0.168f - (0.168f * s), 0, 0,
            0.272f - (0.272f * s), 0.534f - (0.534f * s), 0.131f + (0.869f * s), 0, 0,
            0, 0, 0, 1, 0
        ];
    }

    static float[] Saturate(float s) =>
    [
        0.213f + (0.787f * s), 0.715f - (0.715f * s), 0.072f - (0.072f * s), 0, 0,
        0.213f - (0.213f * s), 0.715f + (0.285f * s), 0.072f - (0.072f * s), 0, 0,
        0.213f - (0.213f * s), 0.715f - (0.715f * s), 0.072f + (0.928f * s), 0, 0,
        0, 0, 0, 1, 0
    ];

    static float[] HueRotate(float degrees)
    {
        var r = degrees * MathF.PI / 180f;
        float c = MathF.Cos(r), s = MathF.Sin(r);
        return
        [
            0.213f + (c * 0.787f) - (s * 0.213f), 0.715f - (c * 0.715f) - (s * 0.715f), 0.072f - (c * 0.072f) + (s * 0.928f), 0, 0,
            0.213f - (c * 0.213f) + (s * 0.143f), 0.715f + (c * 0.285f) + (s * 0.140f), 0.072f - (c * 0.072f) - (s * 0.283f), 0, 0,
            0.213f - (c * 0.213f) - (s * 0.787f), 0.715f - (c * 0.715f) + (s * 0.715f), 0.072f + (c * 0.928f) + (s * 0.072f), 0, 0,
            0, 0, 0, 1, 0
        ];
    }

    /// <summary><c>drop-shadow(dx dy [blur] [color])</c>, the colour first or last. CSS's blur is a radius, twice the deviation.</summary>
    static SKImageFilter DropShadow(string arg, SKImageFilter? input, string source)
    {
        var colour = ColourPart().Match(arg);
        var colourText = colour.Success ? colour.Value : null;
        var rest = colour.Success ? arg.Remove(colour.Index, colour.Length) : arg;
        var lengths = rest.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (lengths.Length is < 2 or > 3) throw Bad(source, "drop-shadow takes two or three lengths and an optional colour, as in drop-shadow(4px 6px 8px rgba(0,0,0,.5))");
        var color = SKColors.Black;
        if (colourText is not null && !SkiaColorParser.TryParse(colourText, out color))
            throw Bad(source, $"'{colourText}' is not a colour");
        var blur = lengths.Length == 3 ? Length(lengths[2], 0f, source) / 2f : 0f;
        return SKImageFilter.CreateDropShadow(Length(lengths[0], 0f, source), Length(lengths[1], 0f, source), blur, blur, color, input);
    }

    [GeneratedRegex(@"(?:#[0-9a-fA-F]{3,8}|(?:rgba?|hsla?)\([^()]*\)|\b[a-zA-Z]+\b)")]
    private static partial Regex ColourPart();

    /// <summary>A length in px, or a bare number read as px.</summary>
    static float Length(string arg, float fallback, string source)
    {
        if (arg.Length == 0) return fallback;
        var t = arg.EndsWith("px", StringComparison.OrdinalIgnoreCase) ? arg[..^2] : arg;
        return float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v >= 0f
            ? v : throw Bad(source, $"'{arg}' is not a length in px");
    }

    /// <summary>A number or a percentage, as the colour functions take it.</summary>
    static float Amount(string arg, float fallback, string source)
    {
        if (arg.Length == 0) return fallback;
        var percent = arg.EndsWith('%');
        var t = percent ? arg[..^1] : arg;
        return float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v >= 0f
            ? (percent ? v / 100f : v) : throw Bad(source, $"'{arg}' is not a number or percentage");
    }

    /// <summary>An angle in deg, rad or turn; a bare 0 is allowed.</summary>
    static float Angle(string arg, string source)
    {
        if (arg.Length == 0) return 0f;
        (string unit, float scale)[] units = [("deg", 1f), ("rad", 180f / MathF.PI), ("turn", 360f)];
        foreach (var (unit, scale) in units)
            if (arg.EndsWith(unit, StringComparison.OrdinalIgnoreCase)
                && float.TryParse(arg[..^unit.Length], NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                return v * scale;
        return float.TryParse(arg, NumberStyles.Float, CultureInfo.InvariantCulture, out var z) && z == 0f
            ? 0f : throw Bad(source, $"'{arg}' is not an angle; write it as 90deg, 1.5rad or 0.25turn");
    }

    static float Clamp01(float v) => Math.Clamp(v, 0f, 1f);

    static ArgumentException Bad(string source, string why) => new($"ctx.filter '{source}': {why}.");
    #endregion
}
