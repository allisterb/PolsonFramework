namespace Polson.Drawing.Svg;

using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

public static class Snap
{
    #region Properties
    public static string Version => "0.5.1";
    #endregion

    #region Methods
    public static SnapPaper Create(object? width = null, object? height = null)
    {
        if (width is null && height is null)
        {
            return new SnapPaper(800f, 600f);
        }

        if (width is float wf && height is float hf)
        {
            return new SnapPaper(wf, hf);
        }

        if (width is int wi && height is int hi)
        {
            return new SnapPaper((float)wi, (float)hi);
        }

        if (width is double wd && height is double hd)
        {
            return new SnapPaper((float)wd, (float)hd);
        }

        var wStr = width?.ToString() ?? "100%";
        var hStr = height?.ToString() ?? "100%";
        return new SnapPaper(wStr, hStr);
    }

    public static SnapPaper Parse(string svgString)
    {
        ArgumentNullException.ThrowIfNull(svgString);

        var trimmed = svgString.Trim();
        if (!trimmed.StartsWith("<svg", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = $"<svg xmlns=\"http://www.w3.org/2000/svg\">{trimmed}</svg>";
        }

        var doc = SvgDocument.FromSvg<SvgDocument>(trimmed);
        return new SnapPaper(doc);
    }

    public static SnapMatrix Matrix(float a = 1f, float b = 0f, float c = 0f, float d = 1f, float e = 0f, float f = 0f) =>
        new(a, b, c, d, e, f);

    public static Color Color(string colorStr) =>
        SnapAttributes.ParseColor(colorStr);

    public static string Rgb(int r, int g, int b, float a = 1f) =>
        a < 1f
            ? string.Create(CultureInfo.InvariantCulture, $"rgba({r},{g},{b},{a})")
            : string.Create(CultureInfo.InvariantCulture, $"rgb({r},{g},{b})");

    public static string Hsl(float h, float s, float l, float a = 1f) =>
        a < 1f
            ? string.Create(CultureInfo.InvariantCulture, $"hsla({h},{s}%,{l}%,{a})")
            : string.Create(CultureInfo.InvariantCulture, $"hsl({h},{s}%,{l}%)");

    public static string Format(string template, params object[] args)
    {
        if (string.IsNullOrEmpty(template) || args == null || args.Length == 0)
            return template;

        for (var i = 0; i < args.Length; i++)
        {
            template = template.Replace($"{{{i}}}", args[i]?.ToString() ?? string.Empty);
        }
        return template;
    }

    public static float SnapTo(float[] values, float value, float tolerance = 10f)
    {
        if (values != null && values.Length > 0)
        {
            foreach (var v in values)
            {
                if (MathF.Abs(v - value) <= tolerance) return v;
            }
        }
        return value;
    }

    public static float Rad(float deg) => deg * MathF.PI / 180f;

    public static float Deg(float rad) => rad * 180f / MathF.PI;

    public static float Angle(float x1, float y1, float x2, float y2)
    {
        var rad = MathF.Atan2(y2 - y1, x2 - x1);
        var deg = Deg(rad);
        return deg < 0 ? deg + 360f : deg;
    }
    #endregion
}

