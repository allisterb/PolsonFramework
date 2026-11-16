// SPDX-License-Identifier: MIT
namespace Polson.Drawing.Svg;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SkiaSharp;
using static System.FormattableString;

/// <summary>
/// Checks on a poster's lines of type: how many characters each carries, and whether the space between lines says
/// which belong together.
/// </summary>
/// <remarks>
/// <para>
/// <b>A line</b> is a <c>&lt;text&gt;</c>, or — when it is built the documented way, one <c>&lt;tspan&gt;</c> per line —
/// each span that sets its own <c>y</c>. Spans that only shift <c>dx</c>/<c>dy</c> continue the line they are in.
/// </para>
/// <para>
/// <b>Spacing is measured on line boxes, not ink.</b> A line's box runs from the font's ascent above its baseline to
/// its descent below, at its printed size, after every transform on the way down. Ink would be the wrong measure: the
/// gap between two lines of the same block changes by nearly half its size with whether the upper line has descenders
/// and the lower one capitals, which is noise of the size of the signal. Gaps are divided by the smaller line's font
/// size, so a heading's own leading is compared like for like with the body's.
/// </para>
/// <para>
/// Sources (design doc §5.0): Carter, Day &amp; Meggs, <i>Typographic Design</i> 5th ed., p. 81 for 60–70 characters a
/// line, stated without a citation; p. 72 for one unit of space within a group and two between, which is the book's
/// worked example, made a rule here.
/// </para>
/// </remarks>
public static partial class PosterTypeAudit
{
    #region Fields
    /// <summary>Characters on a line beyond which it is reported: the bottom of Carter et al.'s 60–70.</summary>
    public const int MaxCharactersPerLine = 60;

    /// <summary>A gap this many times the tightest in its stack is too wide for one group…</summary>
    public const float AmbiguousFrom = 1.3f;

    /// <summary>…and below this, too narrow to start another.</summary>
    public const float SeparateFrom = 1.9f;

    /// <summary>Busyness under a line from which it is reported.</summary>
    /// <remarks>
    /// Calibrated on 2026-10-16: flat colour 0, a full-page gradient 0.15, noise blurred to a soft mottle 0.2–2, the
    /// Mona Lisa enlarged to an A2 page 3.2–7.1, raw pixel noise 23. A white panel at 85% behind a line on the painting
    /// brought it to 0.6.
    /// </remarks>
    public const float BusyFrom = 3f;

    /// <summary>Spread of L* under a line from which it is reported.</summary>
    /// <remarks>
    /// Forty points of L* across mid-grey is more than any one text colour can hold: black text needs a background
    /// near L* 50 or above for WCAG's 4.5:1, white text one near 55 or below.
    /// </remarks>
    public const float SpreadFrom = 40f;

    /// <summary>
    /// The attribute that excuses a line, or every line in a group, from named checks: <c>data-print-exempt="line-length"</c>.
    /// </summary>
    /// <remarks>
    /// For a line whose length is fixed by something other than reading — the legal line a real film's unofficial poster
    /// must carry whole, which fired on every print file of poster3. An exemption is a decision, so the workflow asks for
    /// its reason in <c>direction.md</c>; the check says nothing about the line once it is marked.
    /// </remarks>
    public const string ExemptAttribute = "data-print-exempt";

    /// <summary>The name <see cref="ExemptAttribute"/> uses for <see cref="CheckLineLength"/>.</summary>
    public const string LineLengthCheck = "line-length";

    private const int MaxReported = 6;
    #endregion

    #region Methods
    /// <summary>The lines of type a document draws, in paper units.</summary>
    /// <summary>
    /// Saved markup as the audits must read it: parsed, with every <c>letter-spacing</c> run set as the glyphs it renders
    /// to (<see cref="SvgTracking.ForRender"/>), so a tracked line measures its tracked width.
    /// </summary>
    /// <remarks>A paper saves <c>paper.trackedText</c> as one run with <c>letter-spacing</c> (<see cref="SvgTracking.ForSave"/>),
    /// which the parser alone measures untracked. Parse once and measure lines and shapes from the one document, so their
    /// paint order agrees.</remarks>
    public static SvgDocument Parse(string svgXml) => SvgTracking.ForRender(SvgDocument.FromSvg<SvgDocument>(svgXml));

    public static IReadOnlyList<TypeLine> Lines(SvgDocument document)
    {
        var lines = new List<TypeLine>();
        var order = PaintOrder(document);

        // `paper.trackedText(...)` sets a run as one <text> per glyph inside a group. Read one at a time, a tracked
        // title was sixteen one-letter lines: too short for the length check and invisible to the spacing one.
        var runs = Descendants(document).OfType<SvgGroup>().Where(g => !IsDefinition(g) && IsTrackedRun(g)).ToList();
        var inRuns = runs.SelectMany(g => g.Children.OfType<SvgText>()).ToHashSet();
        foreach (var run in runs) AddRun(lines, run, order[run]);

        foreach (var text in Descendants(document).OfType<SvgText>().Where(t => !IsDefinition(t) && !inRuns.Contains(t)))
        {
            var spans = text.Children.OfType<SvgTextSpan>().Where(s => s.Y is { Count: > 0 }).ToList();
            if (spans.Count == 0)
            {
                Add(lines, text, text, Words(text), order[text]);
                continue;
            }

            // Text set directly in the container, before or between the spans, is its own line at the container's origin.
            var own = Collapse(string.Concat(text.Nodes.OfType<SvgContentNode>().Select(n => n.Content)));
            if (own.Length > 0) Add(lines, text, text, own, order[text]);
            foreach (var span in spans) Add(lines, span, text, Words(span), order[text]);
        }
        return lines;
    }

    /// <summary>Lines carrying more than <see cref="MaxCharactersPerLine"/> characters, except those marked exempt.</summary>
    public static IEnumerable<string> CheckLineLength(IReadOnlyList<TypeLine> lines)
    {
        var long_ = lines.Where(l => l.Words.Length > MaxCharactersPerLine && !l.Exempt.Contains(LineLengthCheck))
            .OrderByDescending(l => l.Words.Length).ToList();
        foreach (var line in long_.Take(MaxReported))
        {
            yield return Invariant($"{Quote(line.Words)} runs to {line.Words.Length} characters on one line. ")
              + Invariant($"Comfortable reading tops out around 60–70; break it nearer {MaxCharactersPerLine}, where the sense breaks.");
        }
        if (long_.Count > MaxReported)
            yield return $"{long_.Count - MaxReported} more lines run past {MaxCharactersPerLine} characters.";
    }

    /// <summary>
    /// Gaps between stacked lines that are neither the spacing of one group nor clearly a break between two.
    /// </summary>
    /// <remarks>
    /// Lines stack when one sits below another and they overlap horizontally by at least a third of the narrower. In
    /// each stack of three or more lines the tightest gap is taken as the within-group spacing; a gap from
    /// <see cref="AmbiguousFrom"/> to <see cref="SeparateFrom"/> times it is reported. Rotated lines are left out.
    /// </remarks>
    public static IEnumerable<string> CheckGroupSpacing(IReadOnlyList<TypeLine> lines)
    {
        var level = lines.Where(l => l.Upright && l.Bounds.Width > 0).OrderBy(l => l.Bounds.Top).ToList();

        // Each line's nearest line below it in the same column.
        var edges = new List<(TypeLine Upper, TypeLine Lower, float Gap)>();
        foreach (var upper in level)
        {
            var below = level
                .Where(l => l != upper && l.Bounds.Top > upper.Bounds.Top && Overlap(upper.Bounds, l.Bounds) >= 1f / 3)
                .OrderBy(l => l.Bounds.Top)
                .FirstOrDefault();
            if (below is null) continue;

            var gap = (below.Bounds.Top - upper.Bounds.Bottom) / MathF.Min(upper.SizePaper, below.SizePaper);
            if (gap > 0.02f) edges.Add((upper, below, gap));
        }

        // Stacks are the chains those links make.
        var stackOf = level.ToDictionary(l => l, l => l);
        TypeLine Root(TypeLine l) => stackOf[l] == l ? l : stackOf[l] = Root(stackOf[l]);
        foreach (var (upper, lower, _) in edges) stackOf[Root(lower)] = Root(upper);

        var reported = 0;
        foreach (var stack in edges.GroupBy(e => Root(e.Upper)).Where(s => s.Count() >= 2))
        {
            var tightest = stack.Min(e => e.Gap);
            foreach (var (upper, lower, gap) in stack.OrderBy(e => e.Upper.Bounds.Top))
            {
                var ratio = gap / tightest;
                if (ratio < AmbiguousFrom || ratio >= SeparateFrom) continue;
                if (reported++ >= MaxReported) yield break;

                yield return Invariant($"The space between {Quote(upper.Words)} and {Quote(lower.Words)} is {ratio:0.0}x the tightest ")
                  + "line spacing in that block — too much to read as one group, too little to start another. Make it "
                  + "equal to the line spacing, or at least twice it.";
            }
        }
    }

    /// <summary>
    /// What lies under each upright line once the type is taken away: how busy it is, and how far it runs from dark to light.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The document is rendered without its text, so a panel put behind a line counts and the line itself does not.
    /// Lightness is CIE L* (0 black, 100 white), over white paper where nothing is drawn.
    /// </para>
    /// <para>
    /// <b>Busyness</b> is the mean change in L* between neighbouring samples taken an eighth of the type size apart:
    /// texture at the scale of the letters' strokes, which is what interferes with reading them. Because the step is
    /// tied to the type size, the figure does not depend on the render resolution, and a broad gradient — a large change
    /// over a long distance — scores near zero. <b>Spread</b> is the range of L* between the 5th and 95th percentiles:
    /// a line crossing from a dark area into a light one, which no single text colour can contrast with.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<(TypeLine Line, float Busyness, float Spread)> MeasureBackgrounds(string svgXml, IReadOnlyList<TypeLine> lines)
    {
        var upright = lines.Where(l => l.Upright && l.Bounds.Width > 0 && l.Bounds.Height > 0).ToList();
        if (upright.Count == 0) return [];

        var document = SvgDocument.FromSvg<SvgDocument>(svgXml);
        foreach (var text in Descendants(document).OfType<SvgTextBase>().Where(t => t.Parent is not SvgTextBase && !IsDefinition(t)).ToList())
            text.Parent?.Children.Remove(text);

        var box = document.ViewBox is { Width: > 0, Height: > 0 } vb ? vb : new SvgViewBox(0, 0, document.Width.Value, document.Height.Value);

        // Sixteen pixels to the smallest type size, so an eighth of it is two: enough to sample a stroke's scale.
        var k = MathF.Min(16f / upright.Min(l => l.SizePaper), 4000f / MathF.Max(box.Width, box.Height));
        var w = (int)MathF.Ceiling(box.Width * k);
        var h = (int)MathF.Ceiling(box.Height * k);

        using var skSvg = new SKSvg();
        skSvg.FromSvgDocument(document);
        using var bitmap = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            if (skSvg.Picture is { } picture)
            {
                var matrix = SKMatrix.CreateScale(k * box.Width / document.Width.Value, k * box.Height / document.Height.Value);
                canvas.DrawPicture(picture, in matrix);
            }
        }

        var lightness = Lightness(bitmap);
        return [.. upright.Select(line =>
        {
            var step = Math.Max(1, (int)MathF.Round(line.SizePaper * k / 8));
            var x0 = Math.Clamp((int)((line.Bounds.Left - box.MinX) * k), 0, w - 1);
            var x1 = Math.Clamp((int)((line.Bounds.Right - box.MinX) * k), 0, w - 1);
            var y0 = Math.Clamp((int)((line.Bounds.Top - box.MinY) * k), 0, h - 1);
            var y1 = Math.Clamp((int)((line.Bounds.Bottom - box.MinY) * k), 0, h - 1);

            var samples = new List<float>();
            var changes = new List<float>();
            for (var y = y0; y <= y1; y += step)
            {
                for (var x = x0; x <= x1; x += step)
                {
                    var l = lightness[y * w + x];
                    samples.Add(l);
                    if (x + step <= x1) changes.Add(MathF.Abs(lightness[y * w + x + step] - l));
                    if (y + step <= y1) changes.Add(MathF.Abs(lightness[(y + step) * w + x] - l));
                }
            }

            samples.Sort();
            var spread = samples.Count == 0
                ? 0f
                : samples[Math.Min(samples.Count - 1, (int)(samples.Count * 0.95f))] - samples[(int)(samples.Count * 0.05f)];
            return (line, changes.Count == 0 ? 0f : changes.Average(), spread);
        })];
    }

    /// <summary>Lines set over a background that is busy, or that runs from dark to light, under them.</summary>
    public static IEnumerable<string> CheckBackgrounds(IReadOnlyList<(TypeLine Line, float Busyness, float Spread)> measured)
    {
        var flagged = measured.Where(m => m.Busyness >= BusyFrom || m.Spread >= SpreadFrom)
            .OrderByDescending(m => m.Busyness / BusyFrom + m.Spread / SpreadFrom)
            .ToList();

        foreach (var (line, busy, spread) in flagged.Take(MaxReported))
        {
            var what = (busy >= BusyFrom, spread >= SpreadFrom) switch
            {
                (true, true) => Invariant($"a busy background (texture {busy:0.0}; flat colour is 0, and {BusyFrom:0} is flagged) that also runs {spread:0} points of lightness from dark to light"),
                (true, false) => Invariant($"a busy background (texture {busy:0.0}; flat colour is 0, and {BusyFrom:0} is flagged)"),
                _ => Invariant($"a background that runs {spread:0} points of lightness from dark to light, which no one text colour contrasts with all along"),
            };
            yield return $"{Quote(line.Words)} is set over {what}. Type over photographs and texture is slow to read. "
              + "Put a solid or softened panel behind it, move it to a calm part of the image, or soften the image under it.";
        }

        if (flagged.Count > MaxReported)
            yield return $"{flagged.Count - MaxReported} more lines are set over busy or uneven backgrounds.";
    }

    /// <summary>CIE L* for every pixel of an opaque bitmap, row by row.</summary>
    private static float[] Lightness(SKBitmap bitmap)
    {
        var pixels = bitmap.GetPixelSpan();
        var result = new float[bitmap.Width * bitmap.Height];
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var i = y * bitmap.RowBytes + x * 4;
                var luminance = 0.2126f * Linear(pixels[i]) + 0.7152f * Linear(pixels[i + 1]) + 0.0722f * Linear(pixels[i + 2]);
                result[y * bitmap.Width + x] = luminance > 216f / 24389 ? 116 * MathF.Cbrt(luminance) - 16 : luminance * 24389f / 27;
            }
        }
        return result;

        static float Linear(byte channel)
        {
            var c = channel / 255f;
            return c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
        }
    }

    /// <summary>Whether a group is a tracked run: two or more single-glyph <c>&lt;text&gt;</c> on one baseline, and nothing else.</summary>
    private static bool IsTrackedRun(SvgGroup group)
    {
        var texts = group.Children.OfType<SvgText>().ToList();
        if (texts.Count < 2 || texts.Count != group.Children.Count) return false;

        // A glyph is at most two characters. A space glyph can arrive empty — a parser drops whitespace-only text — so
        // empty ones are allowed, and the word spaces are put back from the gaps below.
        if (texts.Any(t => t.Children.OfType<SvgTextSpan>().Any() || t.Y is not { Count: > 0 } || Words(t).Length > 2)) return false;
        if (texts.Count(t => Words(t).Length > 0) < 2) return false;

        var baseline = texts[0].Y[0].Value;
        return texts.All(t => MathF.Abs(t.Y[0].Value - baseline) < 0.01f);
    }

    /// <summary>A tracked run as one line: its glyphs in reading order, inside the union of their boxes.</summary>
    /// <remarks>A word space is a gap between glyph boxes wider than a quarter of the type size; tracking alone is narrower.</remarks>
    private static void AddRun(List<TypeLine> lines, SvgGroup run, int order)
    {
        var glyphs = new List<TypeLine>();
        foreach (var text in run.Children.OfType<SvgText>()) Add(glyphs, text, text, Words(text), order);
        if (glyphs.Count == 0) return;

        var ordered = glyphs.OrderBy(g => g.Bounds.Left).ToList();
        var words = new StringBuilder(ordered[0].Words);
        for (var i = 1; i < ordered.Count; i++)
        {
            if (ordered[i].Bounds.Left - ordered[i - 1].Bounds.Right > ordered[i].SizePaper / 4) words.Append(' ');
            words.Append(ordered[i].Words);
        }

        var box = ordered.Skip(1).Aggregate(ordered[0].Bounds, (b, g) => SKRect.Union(b, g.Bounds));
        lines.Add(new TypeLine(words.ToString(), box, ordered.Max(g => g.SizePaper), ordered.All(g => g.Upright), Exemptions(run), order));
    }

    private static void Add(List<TypeLine> lines, SvgTextBase line, SvgText container, string words, int order)
    {
        if (words.Length == 0) return;

        var x = (line.X is { Count: > 0 } lx ? lx : container.X).FirstOrDefault().Value;
        var y = (line.Y is { Count: > 0 } ly ? ly : container.Y).FirstOrDefault().Value;

        var size = line.FontSize is { Value: > 0 } fs ? fs.Value : 16f;
        using var typeface = Typeface(line.FontFamily, line.FontWeight);
        using var font = new SKFont(typeface, size);
        var metrics = font.Metrics;
        var width = font.MeasureText(words);
        var left = line.TextAnchor switch
        {
            SvgTextAnchor.Middle => x - width / 2,
            SvgTextAnchor.End => x - width,
            _ => x,
        };

        var toPaper = Transform(line);
        var box = toPaper.MapRect(new SKRect(left, y + metrics.Ascent, left + width, y + metrics.Descent));
        var scale = MathF.Sqrt(MathF.Abs(toPaper.ScaleX * toPaper.ScaleY - toPaper.SkewX * toPaper.SkewY));
        var upright = MathF.Abs(toPaper.SkewX) < 1e-4f && MathF.Abs(toPaper.SkewY) < 1e-4f;

        lines.Add(new TypeLine(words, box, size * scale, upright, Exemptions(line), order));
    }

    /// <summary>Each element's place in the order the document paints: its position in a depth-first walk, parents first.</summary>
    internal static Dictionary<SvgElement, int> PaintOrder(SvgDocument document) =>
        Descendants(document).Select((e, i) => (e, i)).ToDictionary<(SvgElement e, int i), SvgElement, int>(p => p.e, p => p.i, ReferenceEqualityComparer.Instance);

    /// <summary>The checks an element is excused from, by <see cref="ExemptAttribute"/> on it or any ancestor.</summary>
    internal static HashSet<string> Exemptions(SvgElement element)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var e = element; e is not null and not SvgDocument; e = e.Parent)
        {
            if (e.CustomAttributes.TryGetValue(ExemptAttribute, out var value))
                names.UnionWith(value.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries));
        }
        return names;
    }

    /// <summary>The transform from an element's own units to the paper's: every <c>transform</c> from the root down.</summary>
    internal static SKMatrix Transform(SvgElement element)
    {
        var chain = new List<SvgElement>();
        for (var e = element; e is not null and not SvgDocument; e = e.Parent) chain.Add(e);

        var total = SKMatrix.Identity;
        foreach (var e in Enumerable.Reverse(chain))
        {
            if (e.Transforms is { Count: > 0 } t)
                total = total.PreConcat(SnapTransformParser.ParseToMatrix(t.ToString()).ToSkMatrix());
        }
        return total;
    }

    private static SKTypeface Typeface(string? families, SvgFontWeight weight)
    {
        var style = weight is SvgFontWeight.Bold or SvgFontWeight.Bolder or SvgFontWeight.W600 or SvgFontWeight.W700
            or SvgFontWeight.W800 or SvgFontWeight.W900
            ? SKFontStyle.Bold
            : SKFontStyle.Normal;

        foreach (var name in (families ?? "").Split(',').Select(f => f.Trim().Trim('\'', '"')).Where(f => f.Length > 0))
        {
            if (SKFontManager.Default.MatchFamily(name, style) is { } match) return match;
        }
        return SKTypeface.FromFamilyName(null, style);
    }

    private static float Overlap(SKRect a, SKRect b)
    {
        var shared = MathF.Min(a.Right, b.Right) - MathF.Max(a.Left, b.Left);
        return shared <= 0 ? 0 : shared / MathF.Min(a.Width, b.Width);
    }

    private static string Words(SvgElement element)
    {
        var text = new StringBuilder();
        foreach (var node in element.Nodes)
        {
            if (node is SvgContentNode content) text.Append(content.Content);
            else if (node is SvgElement child) text.Append(' ').Append(Words(child)).Append(' ');
        }
        return Collapse(text.ToString());
    }

    private static string Collapse(string s) => Whitespace().Replace(s, " ").Trim();

    internal static string Quote(string words) => "\"" + (words.Length > 32 ? words[..31] + "…" : words) + "\"";

    internal static bool IsDefinition(SvgElement element)
    {
        for (var e = element.Parent; e is not null; e = e.Parent)
        {
            if (e is SvgDefinitionList or SvgClipPath or SvgMask or SvgPatternServer or SvgMarker or SvgSymbol) return true;
        }
        return false;
    }

    internal static IEnumerable<SvgElement> Descendants(SvgElement element) =>
        element.Children.SelectMany(c => Descendants(c).Prepend(c));

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
    #endregion
}

/// <summary>A line of type as it sits on the paper: its words, its line box, its font size after transforms.</summary>
/// <remarks>A class rather than a record on purpose: two lines with the same words in the same place are still two lines.</remarks>
public sealed class TypeLine(string words, SKRect bounds, float sizePaper, bool upright, IReadOnlySet<string>? exempt = null, int? order = null)
{
    #region Properties
    public string Words { get; } = words;

    /// <summary>Where the line is painted among the marks it is audited with — later is on top — or null when unknown.</summary>
    public int? Order { get; } = order;

    /// <summary>From the font's ascent to its descent, in paper units.</summary>
    public SKRect Bounds { get; } = bounds;

    public float SizePaper { get; } = sizePaper;

    public bool Upright { get; } = upright;

    /// <summary>The checks this line is excused from (<see cref="PosterTypeAudit.ExemptAttribute"/>).</summary>
    public IReadOnlySet<string> Exempt { get; } = exempt ?? new HashSet<string>();
    #endregion
}
