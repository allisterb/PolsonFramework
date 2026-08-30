namespace Polson.Drawing.Svg;

using System;
using System.Globalization;
using System.Linq;
using global::Svg;
using SkiaSharp;

/// <summary>
/// Measures an SVG <c>&lt;text&gt;</c> element with real font metrics.
/// </summary>
/// <remarks>
/// Every other element type can state its own extent from its attributes — a rect has a width, a
/// circle has a radius. Text cannot: its extent depends on the glyphs of a particular face at a
/// particular size, which only a font engine knows. Before this, <c>text.getBBox()</c> returned
/// <c>0×0</c>, which meant a vector-first piece had <b>no way to measure type at all</b> — the one
/// measurement a layout most depends on, since nothing can be placed beneath a line of text whose
/// height is unknown.
/// <para>
/// Measured through the same SkiaSharp path the 2D canvas uses, so a string measures the same
/// whichever side of the SDK asks. A vector layout and a raster one that disagreed about the width
/// of the same words would be worse than neither having the call.
/// </para>
/// </remarks>
internal static class SnapTextMeasurement
{
    #region Methods
    /// <summary>The box a text element's glyphs occupy, honouring anchor, size and family.</summary>
    internal static SnapBBox Measure(SvgText text, SnapMatrix? matrix)
    {
        var content = text.Text ?? string.Empty;
        var size = FontSize(text);

        using var typeface = Typeface(text);
        using var font = new SKFont(typeface, size);

        var width = font.MeasureText(content);
        font.GetFontMetrics(out var metrics);

        var anchorX = text.X?.Count > 0 ? text.X[0].Value : 0f;
        var baselineY = text.Y?.Count > 0 ? text.Y[0].Value : 0f;

        // SVG anchors the run at the point; the box starts wherever that leaves its left edge.
        var left = text.TextAnchor switch
        {
            SvgTextAnchor.Middle => anchorX - width / 2f,
            SvgTextAnchor.End => anchorX - width,
            _ => anchorX
        };

        // y is the baseline, and ascent is negative, so the top sits above it.
        var top = baselineY + metrics.Ascent;
        var height = metrics.Descent - metrics.Ascent;

        return Transformed(left, top, width, height, matrix);
    }
    #endregion

    #region Methods (private)
    /// <summary>
    /// Resolves the face, falling back the way a browser would rather than to whatever is default.
    /// </summary>
    /// <remarks>
    /// <c>font-family</c> in SVG is a comma-separated list for the same reason it is in CSS, so a
    /// missing first choice should reach the next one and not the platform default. Skia substitutes
    /// silently, so a candidate is only accepted when it resolves to itself.
    /// </remarks>
    private static SKTypeface Typeface(SvgText text)
    {
        // Mapped member by member rather than cast: SvgFontWeight.W700 is an enum whose *ordinal*
        // is not 700, so casting it to a numeric weight silently measures everything as regular.
        var weight = text.FontWeight switch
        {
            SvgFontWeight.W100 => SKFontStyleWeight.Thin,
            SvgFontWeight.W200 => SKFontStyleWeight.ExtraLight,
            SvgFontWeight.W300 or SvgFontWeight.Lighter => SKFontStyleWeight.Light,
            SvgFontWeight.W500 => SKFontStyleWeight.Medium,
            SvgFontWeight.W600 => SKFontStyleWeight.SemiBold,
            SvgFontWeight.W700 or SvgFontWeight.Bold => SKFontStyleWeight.Bold,
            SvgFontWeight.W800 or SvgFontWeight.Bolder => SKFontStyleWeight.ExtraBold,
            SvgFontWeight.W900 => SKFontStyleWeight.Black,
            _ => SKFontStyleWeight.Normal
        };

        var slant = text.FontStyle == SvgFontStyle.Italic || text.FontStyle == SvgFontStyle.Oblique
            ? SKFontStyleSlant.Italic
            : SKFontStyleSlant.Upright;

        var families = (text.FontFamily ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(f => f.Trim().Trim('\'', '"').Trim())
            .Where(f => f.Length > 0);

        SKTypeface? substitute = null;
        foreach (var family in families)
        {
            var face = SKTypeface.FromFamilyName(family, weight, SKFontStyleWidth.Normal, slant);
            if (face is null) continue;
            if (string.Equals(face.FamilyName, family, StringComparison.OrdinalIgnoreCase)) return face;
            substitute ??= face;
        }

        return substitute
            ?? SKTypeface.FromFamilyName(null, weight, SKFontStyleWidth.Normal, slant)
            ?? SKTypeface.Default;
    }

    /// <summary>Font size in pixels, defaulting to the SVG initial value when unset.</summary>
    private static float FontSize(SvgText text)
    {
        var size = text.FontSize;
        if (size == SvgUnit.None || size.Value <= 0f) return DefaultFontSize;

        return size.Type switch
        {
            SvgUnitType.Point => size.Value * 4f / 3f,
            _ => size.Value
        };
    }

    private static SnapBBox Transformed(float x, float y, float width, float height, SnapMatrix? matrix)
    {
        if (matrix is null) return new SnapBBox(x, y, width, height);

        // Transform the corners rather than the origin: a rotated run's box is the box of what it
        // covers, which is not the original box moved.
        var corners = new[]
        {
            matrix.TransformPoint(x, y),
            matrix.TransformPoint(x + width, y),
            matrix.TransformPoint(x, y + height),
            matrix.TransformPoint(x + width, y + height)
        };

        var minX = corners.Min(p => p.X);
        var minY = corners.Min(p => p.Y);
        var maxX = corners.Max(p => p.X);
        var maxY = corners.Max(p => p.Y);

        return new SnapBBox(minX, minY, maxX - minX, maxY - minY);
    }
    #endregion

    #region Fields
    private const float DefaultFontSize = 16f;
    #endregion
}
