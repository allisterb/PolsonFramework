namespace Polson.Drawing.Skia;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using SkiaSharp;

public static class SkiaColorParser
{
    #region Methods
    /// <summary>The colour a string names, or black when it names none.</summary>
    /// <remarks>
    /// Black is indistinguishable from a genuine <c>#000000</c>, so a caller that needs to know
    /// whether the string was a colour at all must use <see cref="TryParse"/>. Kept as it is because
    /// the drawing calls want a colour unconditionally — a fill cannot be "no answer".
    /// </remarks>
    public static SKColor Parse(string? colorStr, float globalAlpha = 1f) =>
        TryParse(colorStr, out var color) ? ApplyAlpha(color, globalAlpha) : SKColors.Black;

    /// <summary>
    /// The colour a string names, and whether it named one at all.
    /// </summary>
    /// <remarks>
    /// Added for interpolation, which has to tell a colour from any other string: asked to tween an
    /// attribute between two values, answering black for both would silently animate nothing while
    /// reporting success. <see cref="Parse"/> is this method with the answer thrown away.
    /// </remarks>
    public static bool TryParse(string? colorStr, out SKColor color)
    {
        color = SKColors.Black;

        if (string.IsNullOrWhiteSpace(colorStr))
            return false;

        var s = colorStr.Trim();

        // Check named colors first
        if (NamedColors.TryGetValue(s.ToLowerInvariant(), out var named))
        {
            color = named;
            return true;
        }

        // Hex colors (#rgb, #rgba, #rrggbb, #rrggbbaa)
        if (s.StartsWith('#'))
        {
            var hex = s[1..];
            if (hex.Length == 3) // #rgb
            {
                var r = byte.Parse(new string(hex[0], 2), NumberStyles.HexNumber);
                var g = byte.Parse(new string(hex[1], 2), NumberStyles.HexNumber);
                var b = byte.Parse(new string(hex[2], 2), NumberStyles.HexNumber);
                color = new SKColor(r, g, b, 255);
                return true;
            }
            if (hex.Length == 4) // #rgba
            {
                var r = byte.Parse(new string(hex[0], 2), NumberStyles.HexNumber);
                var g = byte.Parse(new string(hex[1], 2), NumberStyles.HexNumber);
                var b = byte.Parse(new string(hex[2], 2), NumberStyles.HexNumber);
                var a = byte.Parse(new string(hex[3], 2), NumberStyles.HexNumber);
                color = new SKColor(r, g, b, a);
                return true;
            }
            if (hex.Length == 6) // #rrggbb
            {
                var r = byte.Parse(hex[0..2], NumberStyles.HexNumber);
                var g = byte.Parse(hex[2..4], NumberStyles.HexNumber);
                var b = byte.Parse(hex[4..6], NumberStyles.HexNumber);
                color = new SKColor(r, g, b, 255);
                return true;
            }
            if (hex.Length == 8) // #rrggbbaa
            {
                var r = byte.Parse(hex[0..2], NumberStyles.HexNumber);
                var g = byte.Parse(hex[2..4], NumberStyles.HexNumber);
                var b = byte.Parse(hex[4..6], NumberStyles.HexNumber);
                var a = byte.Parse(hex[6..8], NumberStyles.HexNumber);
                color = new SKColor(r, g, b, a);
                return true;
            }
        }

        // rgb(r, g, b) or rgba(r, g, b, a)
        if (s.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            var m = RgbRegex.Match(s);
            if (m.Success)
            {
                var r = (byte)Math.Clamp(ParseColorComponent(m.Groups[1].Value, 255), 0, 255);
                var g = (byte)Math.Clamp(ParseColorComponent(m.Groups[2].Value, 255), 0, 255);
                var b = (byte)Math.Clamp(ParseColorComponent(m.Groups[3].Value, 255), 0, 255);
                var a = m.Groups[4].Success ? ParseAlphaComponent(m.Groups[4].Value) : 1f;
                color = new SKColor(r, g, b, (byte)(a * 255f));
                return true;
            }
        }

        // hsl(h, s, l) or hsla(h, s, l, a)
        if (s.StartsWith("hsl", StringComparison.OrdinalIgnoreCase))
        {
            var m = HslRegex.Match(s);
            if (m.Success)
            {
                var h = float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                var sat = float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) / 100f;
                var lit = float.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture) / 100f;
                var a = m.Groups[4].Success ? ParseAlphaComponent(m.Groups[4].Value) : 1f;
                color = HslToRgb(h, sat, lit, a);
                return true;
            }
        }

        if (SKColor.TryParse(s, out var parsed))
        {
            color = parsed;
            return true;
        }

        return false;
    }

    public static SKColor ApplyAlpha(SKColor color, float alpha)
    {
        if (alpha >= 1f) return color;
        if (alpha <= 0f) return color.WithAlpha(0);
        var finalAlpha = (byte)(color.Alpha * Math.Clamp(alpha, 0f, 1f));
        return color.WithAlpha(finalAlpha);
    }

    private static float ParseColorComponent(string val, float max)
    {
        val = val.Trim();
        if (val.EndsWith('%'))
            return float.Parse(val[..^1], CultureInfo.InvariantCulture) * max / 100f;
        return float.Parse(val, CultureInfo.InvariantCulture);
    }

    private static float ParseAlphaComponent(string val)
    {
        val = val.Trim();
        if (val.EndsWith('%'))
            return float.Parse(val[..^1], CultureInfo.InvariantCulture) / 100f;
        return float.Parse(val, CultureInfo.InvariantCulture);
    }

    private static SKColor HslToRgb(float h, float s, float l, float a)
    {
        h = ((h % 360f) + 360f) % 360f;
        s = Math.Clamp(s, 0f, 1f);
        l = Math.Clamp(l, 0f, 1f);

        float c = (1f - MathF.Abs(2f * l - 1f)) * s;
        float x = c * (1f - MathF.Abs((h / 60f) % 2f - 1f));
        float m = l - c / 2f;

        float r1 = 0, g1 = 0, b1 = 0;
        if (h < 60f) { r1 = c; g1 = x; b1 = 0; }
        else if (h < 120f) { r1 = x; g1 = c; b1 = 0; }
        else if (h < 180f) { r1 = 0; g1 = c; b1 = x; }
        else if (h < 240f) { r1 = 0; g1 = x; b1 = c; }
        else if (h < 300f) { r1 = x; g1 = 0; b1 = c; }
        else { r1 = c; g1 = 0; b1 = x; }

        var r = (byte)((r1 + m) * 255f);
        var g = (byte)((g1 + m) * 255f);
        var b = (byte)((b1 + m) * 255f);
        var alpha = (byte)(Math.Clamp(a, 0f, 1f) * 255f);

        return new SKColor(r, g, b, alpha);
    }
    #endregion

    #region Fields
    private static readonly Regex RgbRegex = new(
        @"rgba?\s*\(\s*([0-9.]+%?)\s*,\s*([0-9.]+%?)\s*,\s*([0-9.]+%?)(?:\s*,\s*([0-9.]+%?))?\s*\)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex HslRegex = new(
        @"hsla?\s*\(\s*([0-9.]+)\s*,\s*([0-9.]+)%\s*,\s*([0-9.]+)%(?:\s*,\s*([0-9.]+%?))?\s*\)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Dictionary<string, SKColor> NamedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["transparent"] = SKColors.Transparent,
        ["black"] = SKColors.Black,
        ["white"] = SKColors.White,
        ["red"] = SKColors.Red,
        ["green"] = SKColors.Green,
        ["blue"] = SKColors.Blue,
        ["yellow"] = SKColors.Yellow,
        ["cyan"] = SKColors.Cyan,
        ["magenta"] = SKColors.Magenta,
        ["gray"] = SKColors.Gray,
        ["grey"] = SKColors.Gray,
        ["darkgray"] = SKColors.DarkGray,
        ["darkgrey"] = SKColors.DarkGray,
        ["lightgray"] = SKColors.LightGray,
        ["lightgrey"] = SKColors.LightGray,
        ["orange"] = SKColors.Orange,
        ["purple"] = SKColors.Purple,
        ["pink"] = SKColors.Pink,
        ["brown"] = SKColors.Brown,
        ["gold"] = SKColors.Gold,
        ["silver"] = SKColors.Silver,
        ["navy"] = SKColors.Navy,
        ["teal"] = SKColors.Teal,
        ["olive"] = SKColors.Olive,
        ["maroon"] = SKColors.Maroon,
        ["lime"] = SKColors.Lime,
        ["indigo"] = SKColors.Indigo,
        ["violet"] = SKColors.Violet,
        ["turquoise"] = SKColors.Turquoise,
        ["coral"] = SKColors.Coral,
        ["salmon"] = SKColors.Salmon,
        ["khaki"] = SKColors.Khaki,
        ["plum"] = SKColors.Plum,
        ["crimson"] = SKColors.Crimson
    };
    #endregion
}

