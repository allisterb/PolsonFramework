namespace Polson.Drawing.Svg;

using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

/// <summary>Snap.svg-compatible entry point and factory.</summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// </remarks>
public static class Snap
{
    #region Properties
    public static string Version => "0.5.1";
    public static SnapPathApi Path { get; } = new();
    public static VectorLogoToolkit VectorLogo { get; } = new();
    public static VectorLogoToolkit Logo => VectorLogo;
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

    /// <summary>
    /// A brush nib: a preset name, your own outline as an SVG <c>d</c> string, or an element whose
    /// geometry to use as the nib.
    /// </summary>
    /// <remarks>
    /// Presets are <c>'taper'</c>, <c>'wedge'</c>, <c>'chisel'</c> and <c>'split'</c>. A string
    /// containing a path command is read as a template instead, so
    /// <c>Snap.brush('M0,0 L100,-4 L100,4 Z')</c> needs no second call to distinguish the two. Bend
    /// it onto a path with <c>nib.deform(d)</c> or <c>paper.brushStroke(d, nib)</c>.
    /// <para>
    /// Not to be confused with <c>Skia.Brush</c>, which is the raster medium — a bundle of canvas
    /// <i>state</i>. This one is a template that becomes <i>geometry</i>, which is why it works on a
    /// paper where the other cannot.
    /// </para>
    /// <para>
    /// <b>A preset name is checked before path data, and path data must begin with a move.</b> The
    /// obvious sniff — does the string contain a path command letter — is wrong in the worst
    /// direction: <c>'taper'</c> contains <c>t</c> and <c>a</c>, so it parsed as a <c>d</c> string,
    /// produced an <i>empty</i> nib, and drew nothing while reporting success.
    /// </para>
    /// </remarks>
    public static SnapBrush Brush(object? source = null, string name = "brush") => source switch
    {
        SnapBrush existing => existing,
        SnapElement element => SnapBrush.FromElement(element, name),
        null => SnapBrush.Taper(),
        string s when SnapBrush.HasPreset(s) => SnapBrush.Preset(s),
        string s when s.AsSpan().TrimStart() is ['M' or 'm', ..] => SnapBrush.FromPath(s, name),

        // Neither a preset nor a path: let Preset throw, so the message lists the real names.
        string s => SnapBrush.Preset(s),
        _ => SnapBrush.Preset(source.ToString() ?? string.Empty),
    };

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

    /// <summary>Snaps <paramref name="value"/> to the closest entry in <paramref name="values"/> within <paramref name="tolerance"/>.</summary>
    /// <remarks>
    /// Returns the *closest* candidate, not the first one that happens to fall inside the tolerance.
    /// Scanning in array order made the result depend on how the caller ordered its list — snapping
    /// 43 against every multiple of five returned 40, because 40 was reached first even though 45 is
    /// nearer — so the same set in a different order gave a different answer. Ties resolve to the
    /// earlier entry, so the result stays deterministic.
    /// </remarks>
    public static float SnapTo(float[] values, float value, float tolerance = 10f)
    {
        if (values is null || values.Length == 0) return value;

        var best = value;
        var bestDistance = float.PositiveInfinity;

        foreach (var v in values)
        {
            var distance = MathF.Abs(v - value);
            if (distance < bestDistance)
            {
                best = v;
                bestDistance = distance;
            }
        }

        return bestDistance <= tolerance ? best : value;
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

