// SPDX-License-Identifier: MIT
namespace Polson.Drawing.Skia;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using SkiaSharp;
using HB = HarfBuzzSharp;

/// <summary>Shapes one line of canvas text: a face per character, HarfBuzz per run, runs in visual order.</summary>
/// <remarks>
/// <c>SKCanvas.DrawText</c> maps characters to glyphs one by one: no shaping, no right-to-left, no fallback. On the
/// cloud image (2026-10-19) that set Arabic and Urdu unjoined and backwards, Hebrew backwards, and a character the face
/// lacked as a box, while the same strings through Snap and Motion text were right. Drawing and measuring both go
/// through here, so a width measured is the width drawn.
/// <para>
/// **Direction is resolved per run, not by the full bidirectional algorithm.** Letters are strong left-to-right or
/// right-to-left, digits left-to-right, and neutrals (spaces, punctuation) take the direction of the strong characters
/// either side when those agree, else the line's own, which is its first strong character's. Right-to-left runs are
/// shaped right to left and placed in reverse. That sets a title, a tagline, a credit, or a year inside Arabic, which
/// is what designs carry; a paragraph nesting directions several levels deep is beyond it, by decision.
/// </para>
/// </remarks>
internal static class TextShaper
{
    #region Types
    /// <summary>One run in one face: glyphs and their positions from the run's own origin, in pixels; each glyph's
    /// advance; and where the run's characters are in the line (<see cref="Clusters"/> count from <see cref="Start"/>).</summary>
    internal sealed record Run(SKTypeface Typeface, ushort[] Glyphs, SKPoint[] Positions, uint[] Clusters, float Width,
        bool RightToLeft, float[] Advances, int Start, int Length);

    /// <summary>A shaped line: runs in visual order, left to right, the tracking between them, and the whole advance.</summary>
    internal sealed record Line(Run[] Runs, float Spacing, float Width);

    private enum Dir { Neutral, Ltr, Rtl }

    /// <summary>A HarfBuzz font over bytes HarfBuzz owns. Kept with its blob and face, which it needs alive.</summary>
    private sealed record Face(HB.Blob Blob, HB.Face HbFace, HB.Font Font, int UnitsPerEm);
    #endregion

    #region Fields
    /// <summary>One HarfBuzz face per typeface for the life of the process. A CJK collection is tens of megabytes, so
    /// reading it again for every <c>fillText</c> would make text the slowest thing on the canvas.</summary>
    private static readonly ConcurrentDictionary<string, Lazy<Face?>> Faces = new();
    #endregion

    #region Methods
    /// <summary>Shapes <paramref name="text"/> in <paramref name="font"/>, with <paramref name="spacing"/> pixels of
    /// tracking between clusters (never inside one, so a ligature or a joined letter stays whole).</summary>
    /// <param name="kerning">False sets it without the font's kerning, as Motion text's <c>kerning: false</c> asks.</param>
    public static Line Shape(string text, SKFont font, float spacing, bool kerning = true)
    {
        if (string.IsNullOrEmpty(text)) return new Line([], spacing, 0f);

        var primary = font.Typeface ?? SKTypeface.Default;
        var probes = new Dictionary<SKTypeface, SKFont>();
        try
        {
            var runs = new List<Run>();
            foreach (var (start, length, typeface, dir) in Segment(text, primary, probes))
            {
                runs.Add(ShapeRun(text.Substring(start, length), typeface, font, dir == Dir.Rtl, spacing, kerning) with { Start = start, Length = length });
            }

            var ordered = VisualOrder(runs, ParagraphDirection(text));
            var width = 0f;
            for (var i = 0; i < ordered.Count; i++) width += ordered[i].Width + (i < ordered.Count - 1 ? spacing : 0f);
            return new Line([.. ordered], spacing, width);
        }
        finally
        {
            foreach (var probe in probes.Values) probe.Dispose();
        }
    }

    /// <summary>Draws a shaped line with its left edge at <paramref name="x"/> and its baseline at
    /// <paramref name="baselineY"/>.</summary>
    /// <param name="outlineFallback">
    /// Draws the runs set in a face other than <paramref name="font"/>'s as glyph outlines rather than text. For a PDF:
    /// Skia embeds every face it draws text with whole, never subset, and a Japanese face is a 9–14 MB collection, so a
    /// book with one Japanese line came to 5–16 MB more (becool2's, set wholly in Yu Gothic, were 22 MB). Outlined, that
    /// text cannot be selected, and the file stays the size of its own face.
    /// </param>
    public static void Draw(SKCanvas canvas, Line line, SKFont font, float x, float baselineY, SKPaint paint, bool outlineFallback = false)
    {
        if (line.Runs.Length == 0) return;

        var primary = font.Typeface ?? SKTypeface.Default;
        var fonts = new List<SKFont>();
        try
        {
            using var builder = new SKTextBlobBuilder();
            using var outlines = new SKPath();
            var cursor = x;
            foreach (var run in line.Runs)
            {
                var runFont = new SKFont(run.Typeface, font.Size)
                {
                    ScaleX = font.ScaleX, SkewX = font.SkewX, Embolden = font.Embolden,
                    Edging = font.Edging, Subpixel = font.Subpixel, Hinting = font.Hinting,
                };
                fonts.Add(runFont);

                var points = new SKPoint[run.Positions.Length];
                for (var i = 0; i < points.Length; i++)
                {
                    points[i] = new SKPoint(cursor + run.Positions[i].X, run.Positions[i].Y);
                }
                if (outlineFallback && !ReferenceEquals(run.Typeface, primary))
                {
                    for (var i = 0; i < run.Glyphs.Length; i++)
                    {
                        using var glyph = runFont.GetGlyphPath(run.Glyphs[i]);
                        if (glyph is null) continue;
                        glyph.Transform(SKMatrix.CreateTranslation(points[i].X, baselineY + points[i].Y));
                        outlines.AddPath(glyph);
                    }
                }
                else builder.AddPositionedRun(run.Glyphs, runFont, points);
                cursor += run.Width + line.Spacing;
            }

            using var blob = builder.Build();
            if (blob is not null) canvas.DrawText(blob, 0f, baselineY, paint);
            if (!outlines.IsEmpty) canvas.DrawPath(outlines, paint);
        }
        finally
        {
            foreach (var f in fonts) f.Dispose();
        }
    }

    /// <summary>
    /// <c>canvas.DrawText(text, x, y, align, font, paint)</c>, shaped and with a face for every character. For the text a
    /// sheet or a book sets itself: drawn plainly, becool2's Japanese captions came out as boxes on the board and in the
    /// pitch book (2026-10-25), while <c>comp.text</c> set the same lines in the same sheet.
    /// </summary>
    public static void DrawText(SKCanvas canvas, string text, float x, float y, SKTextAlign align, SKFont font, SKPaint paint,
        bool outlineFallback = false)
    {
        var line = Shape(text, font, 0f);
        Draw(canvas, line, font, align switch { SKTextAlign.Center => x - line.Width / 2, SKTextAlign.Right => x - line.Width, _ => x }, y, paint,
            outlineFallback);
    }

    /// <summary><c>font.MeasureText(text)</c> as <see cref="DrawText"/> draws it.</summary>
    public static float Measure(string text, SKFont font) => Shape(text, font, 0f).Width;

    /// <summary>Splits a line into runs of one face and one direction, by grapheme cluster.</summary>
    private static List<(int Start, int Length, SKTypeface Typeface, Dir Dir)> Segment(string text, SKTypeface primary,
        Dictionary<SKTypeface, SKFont> probes)
    {
        var graphemes = new List<(int Start, int Length, int CodePoint)>();
        var walker = StringInfo.GetTextElementEnumerator(text);
        while (walker.MoveNext())
        {
            var start = walker.ElementIndex;
            var element = (string)walker.Current;
            graphemes.Add((start, element.Length, char.ConvertToUtf32(element, 0)));
        }

        // Directions: strong where the character says, neutrals settled by the strong characters around them.
        var dirs = new Dir[graphemes.Count];
        for (var i = 0; i < graphemes.Count; i++) dirs[i] = ClassOf(graphemes[i].CodePoint);
        var paragraph = ParagraphDirection(text);
        for (var i = 0; i < dirs.Length; i++)
        {
            if (dirs[i] != Dir.Neutral) continue;
            var before = Dir.Neutral;
            for (var j = i - 1; j >= 0 && before == Dir.Neutral; j--) before = ClassOf(graphemes[j].CodePoint);
            var after = Dir.Neutral;
            for (var j = i + 1; j < graphemes.Count && after == Dir.Neutral; j++) after = ClassOf(graphemes[j].CodePoint);
            dirs[i] = before == after && before != Dir.Neutral ? before : paragraph;
        }

        // Faces: the chosen one when it has the character, else the face already in use, else whatever the system
        // offers for it. Staying with the face in use keeps a fallback run whole across its spaces and punctuation.
        var faces = new SKTypeface[graphemes.Count];
        SKTypeface current = primary;
        for (var i = 0; i < graphemes.Count; i++)
        {
            var cp = graphemes[i].CodePoint;
            faces[i] = Has(primary, cp, probes) ? primary
                : Has(current, cp, probes) ? current
                : SKFontManager.Default.MatchCharacter(primary.FamilyName, primary.FontStyle, null, cp) ?? primary;
            current = faces[i];
        }

        var units = new List<(int, int, SKTypeface, Dir)>();
        var first = 0;
        for (var i = 1; i <= graphemes.Count; i++)
        {
            if (i < graphemes.Count && dirs[i] == dirs[first] && ReferenceEquals(faces[i], faces[first])) continue;
            var start = graphemes[first].Start;
            var end = graphemes[i - 1].Start + graphemes[i - 1].Length;
            units.Add((start, end - start, faces[first], dirs[first]));
            first = i;
        }
        return units;
    }

    private static Run ShapeRun(string text, SKTypeface typeface, SKFont font, bool rightToLeft, float spacing, bool kerning = true)
    {
        var face = FaceFor(typeface);
        if (face is null) return Unshaped(text, typeface, font, rightToLeft, spacing);

        using var buffer = new HB.Buffer();
        buffer.AddUtf16(text);
        buffer.Direction = rightToLeft ? HB.Direction.RightToLeft : HB.Direction.LeftToRight;
        buffer.GuessSegmentProperties();
        if (kerning) face.Font.Shape(buffer);
        else face.Font.Shape(buffer, HB.Feature.Parse("-kern"));

        var infos = buffer.GlyphInfos;
        var positions = buffer.GlyphPositions;
        var scale = font.Size / face.UnitsPerEm;
        var scaleX = scale * font.ScaleX;

        var glyphs = new ushort[infos.Length];
        var points = new SKPoint[infos.Length];
        var clusters = new uint[infos.Length];
        var advances = new float[infos.Length];
        var pen = 0f;
        for (var i = 0; i < infos.Length; i++)
        {
            glyphs[i] = (ushort)infos[i].Codepoint;
            clusters[i] = infos[i].Cluster;
            points[i] = new SKPoint(pen + positions[i].XOffset * scaleX, -positions[i].YOffset * scale);
            advances[i] = positions[i].XAdvance * scaleX;
            pen += advances[i];
            // Tracking falls between clusters, in visual order, so it never pulls a cluster apart.
            if (spacing != 0f && i < infos.Length - 1 && infos[i + 1].Cluster != infos[i].Cluster) pen += spacing;
        }
        return new Run(typeface, glyphs, points, clusters, pen, rightToLeft, advances, 0, text.Length);
    }

    /// <summary>A run in a face HarfBuzz could not read: character to glyph and advance, as before shaping.</summary>
    private static Run Unshaped(string text, SKTypeface typeface, SKFont font, bool rightToLeft, float spacing)
    {
        using var runFont = new SKFont(typeface, font.Size) { ScaleX = font.ScaleX };
        var glyphs = runFont.GetGlyphs(text);
        var widths = runFont.GetGlyphWidths(glyphs);
        var points = new SKPoint[glyphs.Length];
        var clusters = new uint[glyphs.Length];
        var pen = 0f;
        for (var i = 0; i < glyphs.Length; i++)
        {
            points[i] = new SKPoint(pen, 0f);
            clusters[i] = (uint)Math.Min(i, Math.Max(0, text.Length - 1));
            pen += widths[i] + (i < glyphs.Length - 1 ? spacing : 0f);
        }
        return new Run(typeface, glyphs, points, clusters, pen, rightToLeft, widths, 0, text.Length);
    }

    private static Face? FaceFor(SKTypeface typeface)
    {
        var style = typeface.FontStyle;
        var key = $"{typeface.FamilyName}|{style.Weight}|{style.Width}|{style.Slant}";
        return Faces.GetOrAdd(key, _ => new Lazy<Face?>(() => Load(typeface))).Value;
    }

    private static Face? Load(SKTypeface typeface)
    {
        using var stream = typeface.OpenStream(out var index);
        if (stream is null || stream.Length <= 0) return null;

        var bytes = new byte[stream.Length];
        if (stream.Read(bytes, bytes.Length) != bytes.Length) return null;

        // In memory HarfBuzz owns, never a managed array: native code reading bytes the GC has moved crashed the test
        // host in hb_shape_full (MotionText.TextShape.Build carries the same rule, for the same reason).
        var native = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, native, bytes.Length);
        var blob = new HB.Blob(native, bytes.Length, HB.MemoryMode.ReadOnly, () => Marshal.FreeHGlobal(native));
        var face = new HB.Face(blob, index);
        var font = new HB.Font(face);
        var upem = face.UnitsPerEm > 0 ? face.UnitsPerEm : typeface.UnitsPerEm;
        font.SetScale(upem, upem);
        return new Face(blob, face, font, upem);
    }

    private static bool Has(SKTypeface typeface, int codePoint, Dictionary<SKTypeface, SKFont> probes)
    {
        if (!probes.TryGetValue(typeface, out var probe)) probes[typeface] = probe = new SKFont(typeface);
        // Spaces and other format characters render as nothing in any face, so a face is not abandoned over one.
        return char.IsWhiteSpace((char)Math.Min(codePoint, char.MaxValue)) || probe.ContainsGlyph(codePoint);
    }

    private static Dir ParagraphDirection(string text)
    {
        var walker = StringInfo.GetTextElementEnumerator(text);
        while (walker.MoveNext())
        {
            var dir = ClassOf(char.ConvertToUtf32((string)walker.Current, 0));
            if (dir != Dir.Neutral) return dir;
        }
        return Dir.Ltr;
    }

    /// <summary>Runs in the order they are seen: a right-to-left line reverses its groups, and a right-to-left group
    /// reverses its runs, so a year inside Arabic still reads left to right.</summary>
    private static List<Run> VisualOrder(List<Run> runs, Dir paragraph)
    {
        var groups = new List<List<Run>>();
        foreach (var run in runs)
        {
            if (groups.Count > 0 && groups[^1][0].RightToLeft == run.RightToLeft) groups[^1].Add(run);
            else groups.Add([run]);
        }
        if (paragraph == Dir.Rtl) groups.Reverse();

        var ordered = new List<Run>();
        foreach (var group in groups)
        {
            if (group[0].RightToLeft) group.Reverse();
            ordered.AddRange(group);
        }
        return ordered;
    }

    private static Dir ClassOf(int cp)
    {
        // Arabic-Indic digits sit inside the Arabic block but read left to right, as a number does in any script.
        if (cp is >= 0x0660 and <= 0x0669 or >= 0x06F0 and <= 0x06F9) return Dir.Ltr;
        if (IsRightToLeft(cp)) return Dir.Rtl;
        return CharUnicodeInfo.GetUnicodeCategory(cp) switch
        {
            UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter
                or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter
                or UnicodeCategory.DecimalDigitNumber or UnicodeCategory.LetterNumber => Dir.Ltr,
            _ => Dir.Neutral,
        };
    }

    /// <summary>The right-to-left scripts' blocks: Hebrew, Arabic, Syriac, Thaana, N'Ko, Samaritan, Mandaic, the
    /// Arabic extensions and presentation forms, and the historic right-to-left scripts of the supplementary planes.</summary>
    private static bool IsRightToLeft(int cp) =>
        cp is >= 0x0590 and <= 0x08FF
            or >= 0xFB1D and <= 0xFDFF
            or >= 0xFE70 and <= 0xFEFF
            or >= 0x10800 and <= 0x10FFF
            or >= 0x1E800 and <= 0x1EFFF;
    #endregion
}
