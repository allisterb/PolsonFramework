// SPDX-License-Identifier: MIT
namespace Polson.Drawing.Svg;

using System.Collections.Generic;
using System.Linq;

/// <summary>
/// SVG <c>letter-spacing</c> made visible to the renderer, which ignores the property: a copy of the document with each
/// tracked run set as one positioned <c>&lt;text&gt;</c> per glyph, as <see cref="VectorLogoToolkit.TrackedText"/> sets one.
/// </summary>
/// <remarks>
/// <para>
/// lanternkeepercloud1 (2026-10-24) tracked a poster's title 0.18em, its tag line 0.22em and its credits 0.14em with the
/// attribute, and the PNG showed none of it while the SVG beside it, opened in a browser, showed all of it: two
/// deliverables of one design that disagreed, and nothing said so.
/// </para>
/// <para>
/// <b>Only the render sees the change.</b> The document is copied before anything is replaced, so the paper, its
/// <c>outSvg</c> and every later script keep the attribute a browser honours; a document with no tracked text is returned
/// as it is, so every other render is the one it was. A run is converted only when it is one plain run: no
/// <c>&lt;tspan&gt;</c>, no <c>textPath</c>, one <c>x</c> and no <c>dx</c>. Anything else is left as it was written.
/// </para>
/// </remarks>
internal static class SvgTracking
{
    #region Methods
    /// <summary>The document to render: <paramref name="document"/> itself, or a copy with its tracked runs set as glyphs.</summary>
    internal static SvgDocument ForRender(SvgDocument document)
    {
        if (!Texts(document).Any(Convertible)) return document;
        var copy = (SvgDocument)document.DeepCopy();
        foreach (var text in Texts(copy).Where(Convertible).ToList()) Expand(text);
        return copy;
    }

    /// <summary>Whether <paramref name="svgXml"/> could carry tracking, before parsing it to find out.</summary>
    internal static bool Mentions(string svgXml) => svgXml.Contains("letter-spacing", System.StringComparison.Ordinal);

    /// <summary>
    /// The document to save: <paramref name="document"/> itself, or a copy with each run <c>paper.trackedText</c> set as glyphs
    /// put back as one <c>&lt;text&gt;</c> carrying <c>letter-spacing</c>, the inverse of <see cref="ForRender"/>.
    /// </summary>
    /// <remarks>
    /// <b>Glyph positions are only right in the font they were measured in.</b> all_in_1 (2026-10-30) saved 1,124 single-letter
    /// texts in Fira Code, which the server has and the director did not; the browser drew each letter in a fallback face at
    /// its Fira Code position, and every tracked line came out unevenly spaced. One run with <c>letter-spacing</c> is laid
    /// out by the browser in whatever face it has, so the spacing stays even. The live document keeps its glyphs, which the
    /// layout audits measure and the render draws; and the saved run renders back to the same glyphs here.
    /// </remarks>
    internal static SvgDocument ForSave(SvgDocument document)
    {
        if (!Tracked(document).Any()) return document;
        var copy = (SvgDocument)document.DeepCopy();
        foreach (var group in Tracked(copy).ToList()) Collapse(group);
        return copy;
    }

    /// <summary>The attribute <c>paper.trackedText</c> marks its group with: the tracking between glyphs, in pixels.</summary>
    internal const string Marker = "data-tracked";
    #endregion

    #region Methods (private)
    private static IEnumerable<SvgText> Texts(SvgElement root)
    {
        foreach (var child in root.Children)
        {
            if (child is SvgText text) yield return text;
            else foreach (var nested in Texts(child)) yield return nested;
        }
    }

    private static IEnumerable<SvgGroup> Tracked(SvgElement root)
    {
        foreach (var child in root.Children)
        {
            if (child is SvgGroup g && g.CustomAttributes.ContainsKey(Marker)) yield return g;
            else foreach (var nested in Tracked(child)) yield return nested;
        }
    }

    /// <summary>Replaces a tracked group's glyphs with one run from the first glyph's left edge, spaced as they were.</summary>
    private static void Collapse(SvgGroup group)
    {
        var spacing = group.CustomAttributes[Marker];
        group.CustomAttributes.Remove(Marker);
        var glyphs = group.Children.OfType<SvgText>().ToList();
        if (glyphs.Count == 0 || glyphs.Count != group.Children.Count) return;   // something else was added to it: leave it

        var run = (SvgText)glyphs[0].DeepCopy();
        run.Text = string.Concat(glyphs.Select(g => g.Text));
        run.TextAnchor = SvgTextAnchor.Start;
        run.CustomAttributes["letter-spacing"] = spacing;
        run.CustomAttributes["xml:space"] = "preserve";
        group.Children.Clear();
        group.Children.Add(run);
    }

    private static bool Convertible(SvgText text) =>
        Declared(text) is { } raw && Pixels(raw, 16f) != 0f
        && text.Parent is not null
        && !text.Children.OfType<SvgTextBase>().Any()
        && !text.Children.OfType<SvgTextPath>().Any()
        && (text.X?.Count ?? 0) <= 1
        && (text.Dx?.Count ?? 0) == 0
        && !string.IsNullOrEmpty(text.Text);

    /// <summary>Replaces <paramref name="text"/> with a group of glyphs, measured while it is still in place to inherit its face.</summary>
    private static void Expand(SvgText text)
    {
        var size = SnapTextMeasurement.SizeOf(text);
        var spacing = Pixels(Declared(text)!, size);
        var glyphs = SnapTextMeasurement.Graphemes(text.Text);
        var advances = glyphs.Select(g => SnapTextMeasurement.Advance(text, g)).ToArray();
        var total = advances.Sum() + spacing * System.Math.Max(0, glyphs.Count - 1);

        var x = text.X?.Count > 0 ? text.X[0].Value : 0f;
        var cursor = text.TextAnchor switch
        {
            SvgTextAnchor.Middle => x - total / 2f,
            SvgTextAnchor.End => x - total,
            _ => x,
        };

        var group = new SvgGroup();
        for (var i = 0; i < glyphs.Count; i++)
        {
            var glyph = (SvgText)text.DeepCopy();
            glyph.Text = glyphs[i];
            glyph.X = [new SvgUnit(SvgUnitType.User, cursor)];
            glyph.LetterSpacing = new SvgUnit(0f);
            glyph.CustomAttributes.Remove("letter-spacing");
            glyph.TextAnchor = SvgTextAnchor.Start;
            group.Children.Add(glyph);
            cursor += advances[i] + spacing;
        }

        var parent = text.Parent!;
        var at = parent.Children.IndexOf(text);
        parent.Children.RemoveAt(at);
        parent.Children.Insert(at, group);
    }

    /// <summary>
    /// The tracking in force, as written: the typed property, else the element's own <c>letter-spacing</c> attribute or
    /// <c>style</c> declaration, else the nearest ancestor's, since the property inherits. Snap's <c>attr()</c> keeps it
    /// as a custom attribute, so the typed property alone reads 0 for everything a script sets.
    /// </summary>
    private static string? Declared(SvgText text)
    {
        if (text.LetterSpacing.Value != 0f)
            return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{text.LetterSpacing.Value}{Suffix(text.LetterSpacing.Type)}");
        for (SvgElement? e = text; e is not null; e = e.Parent)
        {
            if (e.CustomAttributes.TryGetValue("letter-spacing", out var own) && !string.IsNullOrWhiteSpace(own)) return own;
            if (e.CustomAttributes.TryGetValue("style", out var style)
                && System.Text.RegularExpressions.Regex.Match(style, @"(?:^|;)\s*letter-spacing\s*:\s*([^;]+)") is { Success: true } m)
                return m.Groups[1].Value;
        }
        return null;

        static string Suffix(SvgUnitType t) => t switch
        {
            SvgUnitType.Em => "em", SvgUnitType.Ex => "ex", SvgUnitType.Point => "pt", SvgUnitType.Percentage => "%", _ => "px",
        };
    }

    /// <summary>
    /// Tracking in pixels: <c>em</c>, <c>rem</c> and <c>ex</c> against the run's size, points at 96 dpi, <c>normal</c> as 0,
    /// and a bare number or <c>px</c> as pixels — <c>ctx.letterSpacing</c>'s parsing, so the surfaces agree.
    /// </summary>
    private static float Pixels(string raw, float size)
    {
        var t = raw.Trim().ToLowerInvariant();
        if (t is "" or "normal" or "inherit" or "initial") return 0f;
        var split = t.Length;
        while (split > 0 && (char.IsLetter(t[split - 1]) || t[split - 1] == '%')) split--;
        if (!float.TryParse(t[..split].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n))
            return 0f;
        return t[split..] switch
        {
            "em" or "rem" => n * size,
            "ex" => n * size / 2f,
            "pt" => n * 96f / 72f,
            "%" => n / 100f * size,
            _ => n,
        };
    }
    #endregion
}
