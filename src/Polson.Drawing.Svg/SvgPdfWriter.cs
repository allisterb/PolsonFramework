// SPDX-License-Identifier: MIT
namespace Polson.Drawing.Svg;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SkiaSharp;
using static System.FormattableString;

/// <summary>
/// A physical page size in points, from a name (<c>'A2'</c>, <c>'letter'</c>) or dimensions
/// (<c>'420x594mm'</c>, <c>'18x24in'</c>).
/// </summary>
/// <remarks>
/// A named size has no orientation of its own and takes the artwork's; dimensions are taken as written,
/// width first.
/// </remarks>
public sealed partial record PrintSize(float WidthPt, float HeightPt, bool Named)
{
    #region Fields
    public const float PointsPerMm = 72f / 25.4f;

    /// <summary>ISO A and B series and the US sizes a print shop will name, portrait, in millimetres.</summary>
    private static readonly Dictionary<string, (float W, float H)> NamedMm = new(StringComparer.OrdinalIgnoreCase)
    {
        ["A0"] = (841, 1189), ["A1"] = (594, 841), ["A2"] = (420, 594), ["A3"] = (297, 420),
        ["A4"] = (210, 297), ["A5"] = (148, 210), ["A6"] = (105, 148),
        ["B0"] = (1000, 1414), ["B1"] = (707, 1000), ["B2"] = (500, 707), ["B3"] = (353, 500),
        ["letter"] = (215.9f, 279.4f), ["legal"] = (215.9f, 355.6f), ["tabloid"] = (279.4f, 431.8f),
    };
    #endregion

    #region Methods
    /// <summary>Parses <c>'A2'</c>, <c>'letter'</c> or <c>'&lt;w&gt;x&lt;h&gt;&lt;mm|cm|in|pt&gt;'</c>.</summary>
    public static PrintSize Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        if (NamedMm.TryGetValue(text.Trim(), out var mm))
            return new PrintSize(mm.W * PointsPerMm, mm.H * PointsPerMm, true);

        if (Dimensions().Match(text) is { Success: true } m)
        {
            var unit = PointsPer(m.Groups["unit"].Value);
            return new PrintSize(Number(m.Groups["w"].Value) * unit, Number(m.Groups["h"].Value) * unit, false);
        }

        throw new ArgumentException(
            $"pdfSize '{text}' is not a size. Give a name — {string.Join(", ", NamedMm.Keys)} — or dimensions "
          + "with a unit, width first: '420x594mm', '18x24in'.");
    }

    /// <summary>Parses a length with its unit, <c>'3mm'</c> or <c>'0.125in'</c>, into points.</summary>
    public static float ParseLength(string text, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        // A bare number is refused rather than read as millimetres: 3 and 0.125 are both a usual bleed,
        // in different units, and the wrong guess is off by a factor of 25.
        if (Length().Match(text) is { Success: true } m)
            return Number(m.Groups["n"].Value) * PointsPer(m.Groups["unit"].Value);

        throw new ArgumentException(
            $"{parameterName} '{text}' needs a unit: '3mm' (the usual bleed in Europe), '0.125in' (in the US), or '0' for none.");
    }

    /// <summary>This size turned to match the artwork's orientation, when it is a named size.</summary>
    public PrintSize OrientedLike(float width, float height) =>
        Named && (width > height) != (WidthPt > HeightPt) ? this with { WidthPt = HeightPt, HeightPt = WidthPt } : this;

    public string Describe() =>
        string.Create(CultureInfo.InvariantCulture, $"{WidthPt / PointsPerMm:0.#} x {HeightPt / PointsPerMm:0.#} mm");

    private static float PointsPer(string unit) => unit.ToLowerInvariant() switch
    {
        "mm" => PointsPerMm,
        "cm" => PointsPerMm * 10,
        "in" => 72f,
        _ => 1f,
    };

    private static float Number(string text) => float.Parse(text, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^\s*(?<w>\d+(?:\.\d+)?)\s*[x×]\s*(?<h>\d+(?:\.\d+)?)\s*(?<unit>mm|cm|in|pt)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex Dimensions();

    [GeneratedRegex(@"^\s*(?:(?<n>\d+(?:\.\d+)?)\s*(?<unit>mm|cm|in|pt)|(?<n>0+(?:\.0+)?))\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex Length();
    #endregion
}

/// <summary>What <see cref="SvgPdfWriter.Write"/> produced, and what a print shop would query.</summary>
/// <param name="MillimetresPerUnit">How large one unit of the paper prints: a 40-unit cap height prints at 40 × this.</param>
/// <param name="Bitmaps">Every bitmap the page draws, at the size it prints. Effects Skia rasterised are among them, at <see cref="SvgPdfWriter.RasterDpi"/>.</param>
/// <param name="UnplacedBitmaps">Bitmaps in the file that were not drawn by a measurable placement — a pattern fill, say — so their resolution is unknown.</param>
/// <param name="FileBytes">The size of the written file.</param>
public sealed record PdfPrintReport(string Path, PrintSize Trim, float BleedPt, float MillimetresPerUnit, IReadOnlyList<string> Warnings,
    IReadOnlyList<PlacedBitmap> Bitmaps, int UnplacedBitmaps, long FileBytes = 0);

/// <summary>
/// Writes a vector document as a print PDF: the artwork at a physical trim size, with bleed, and the
/// TrimBox and BleedBox a print shop's preflight reads.
/// </summary>
/// <remarks>
/// <para>
/// <b>The paper is the trim.</b> Its viewBox maps to the trimmed page, as a page does in a layout
/// application, and the bleed is the margin outside it: anything drawn up to the bleed beyond the
/// paper's edges prints and is cut off. A background drawn exactly to the paper's edges therefore
/// stops at the trim, and a cut that drifts shows a white sliver — which is what the bleed check reports.
/// </para>
/// <para>
/// Text stays text, with its fonts embedded, and shapes stay vector. Effects Skia cannot express in PDF
/// (filters, some blends) are rasterised at <see cref="RasterDpi"/>, not at Skia's default of 72.
/// The colour is RGB: there is no CMYK conversion and no PDF/X output intent.
/// </para>
/// <para>
/// Skia writes only a MediaBox, so the print boxes are added afterwards as an incremental update — new
/// revisions of the page objects appended after the original file, which every reader honours and
/// which leaves Skia's bytes untouched.
/// </para>
/// </remarks>
public static partial class SvgPdfWriter
{
    #region Fields
    /// <summary>The resolution Skia rasterises effects at when it cannot keep them vector.</summary>
    public const float RasterDpi = 300;

    /// <summary>How far inside the trim text should stay unless told otherwise, in millimetres.</summary>
    /// <remarks>
    /// Print shops quote 3 to 6 mm for the cut's tolerance; 5 is the middle of that, and a poster's type sits
    /// further in still for the eye's sake. A margin is a floor here, not a layout.
    /// </remarks>
    public const float DefaultSafeMarginMm = 5;

    /// <summary>The print resolution below which a bitmap is reported, in pixels per printed inch.</summary>
    public const float MinimumPpi = 150;

    /// <summary>A filter's rasterised bitmaps this heavy, and most of the file, are reported with their cost.</summary>
    public const long HeavyBitmapBytes = 1_000_000;

    /// <summary>A file this heavy is reported when bitmaps are most of it, and a filter's bitmaps of this size always are.</summary>
    public const long HeavyFileBytes = 10_000_000;

    /// <summary>How far the paper's proportions may differ from the page's before the write is refused.</summary>
    private const float AspectTolerance = 0.01f;
    #endregion

    #region Methods
    /// <summary>Writes <paramref name="svgXml"/> to <paramref name="path"/> at <paramref name="size"/> with <paramref name="bleedPt"/> of bleed.</summary>
    /// <param name="safeMarginPt">How far inside the trim text must stay; null is <see cref="DefaultSafeMarginMm"/>, 0 skips the check.</param>
    /// <exception cref="ArgumentException">The paper's proportions do not match the page's.</exception>
    public static PdfPrintReport Write(string svgXml, string path, PrintSize size, float bleedPt, string? title = null, float? safeMarginPt = null)
    {
        ArgumentNullException.ThrowIfNull(svgXml);
        ArgumentOutOfRangeException.ThrowIfNegative(bleedPt);
        var safePt = safeMarginPt ?? DefaultSafeMarginMm * PrintSize.PointsPerMm;
        ArgumentOutOfRangeException.ThrowIfNegative(safePt);

        var document = SvgDocument.FromSvg<SvgDocument>(StripDeclaration(svgXml));
        var box = document.ViewBox is { Width: > 0, Height: > 0 } vb
            ? vb
            : new SvgViewBox(0, 0, document.Width.Value, document.Height.Value);

        var trim = size.OrientedLike(box.Width, box.Height);
        var scaleX = trim.WidthPt / box.Width;
        var scaleY = trim.HeightPt / box.Height;
        if (MathF.Abs(scaleX / scaleY - 1) > AspectTolerance)
        {
            throw new ArgumentException(
                Invariant($"The paper is {box.Width:0.##} x {box.Height:0.##} (ratio {box.Width / box.Height:0.###}) and the page is ")
              + Invariant($"{trim.Describe()} (ratio {trim.WidthPt / trim.HeightPt:0.###}), so the artwork would print stretched. ")
              + Invariant($"Build the paper in the page's proportions — for this page, Snap(1000, {1000 * trim.HeightPt / trim.WidthPt:0}) — ")
              + "or name the size it was designed for.");
        }

        var bleedX = bleedPt / scaleX;
        var bleedY = bleedPt / scaleY;
        Widen(document, box, bleedX, bleedY);

        using var skSvg = new SKSvg();
        skSvg.FromSvgDocument(SvgTracking.ForRender(document));
        if (skSvg.Picture is null)
            throw new InvalidOperationException("The document produced nothing to draw, so no PDF was written.");

        var mediaWidth = trim.WidthPt + 2 * bleedPt;
        var mediaHeight = trim.HeightPt + 2 * bleedPt;

        // Scaled from the document's size, never the picture's CullRect, which can grow to cover marks
        // drawn outside the viewport and would then shrink the artwork off its trim.
        var toPage = SKMatrix.CreateScale(mediaWidth / document.Width.Value, mediaHeight / document.Height.Value);

        var metadata = SKDocumentPdfMetadata.Default;
        metadata.Title = title ?? Path.GetFileNameWithoutExtension(path);
        metadata.Creator = "Polson";
        metadata.Producer = "Polson (Skia PDF)";
        metadata.RasterDpi = RasterDpi;
        metadata.Creation = DateTime.Now;
        metadata.Modified = DateTime.Now;

        using (var stream = File.Create(path))
        using (var pdf = SKDocument.CreatePdf(stream, metadata))
        {
            var canvas = pdf.BeginPage(mediaWidth, mediaHeight);
            canvas.ClipRect(SKRect.Create(mediaWidth, mediaHeight));
            canvas.DrawPicture(skSvg.Picture, in toPage);
            pdf.EndPage();
            pdf.Close();
        }

        // A page without its TrimBox reads to a preflight as one with no bleed, which prints the bleed as
        // part of the poster. Better no file than that one.
        try
        {
            AddPrintBoxes(path, mediaWidth, mediaHeight, bleedPt);
        }
        catch (InvalidOperationException)
        {
            File.Delete(path);
            throw;
        }

        var warnings = new List<string>();
        if (bleedPt > 0) warnings.AddRange(CheckBleed(skSvg.Picture, toPage, mediaWidth, mediaHeight, bleedPt));
        if (safePt > 0) warnings.AddRange(CheckSafeArea(svgXml, box, bleedX, bleedY, mediaWidth, mediaHeight, bleedPt, safePt));

        var lines = PosterTypeAudit.Lines(PosterTypeAudit.Parse(StripDeclaration(svgXml)));
        warnings.AddRange(PosterTypeAudit.CheckLineLength(lines));
        warnings.AddRange(PosterTypeAudit.CheckGroupSpacing(lines));
        warnings.AddRange(PosterTypeAudit.CheckBackgrounds(PosterTypeAudit.MeasureBackgrounds(StripDeclaration(svgXml), lines)));

        IReadOnlyList<PlacedBitmap> bitmaps = [];
        var unplaced = 0;
        var fileBytes = new FileInfo(path).Length;
        try
        {
            (bitmaps, unplaced) = PdfRasterAudit.Measure(path);
            warnings.AddRange(CheckResolution(bitmaps, unplaced, mediaHeight, bleedPt));

            // Where each filtered element prints, in the PDF's own space (points, origin bottom left), so the bitmap
            // Skia rasterised for it can be found among the placements.
            SKRect ToPdf(SKRect paper) => new(
                (paper.Left - box.MinX + bleedX) * scaleX, mediaHeight - (paper.Bottom - box.MinY + bleedY) * scaleY,
                (paper.Right - box.MinX + bleedX) * scaleX, mediaHeight - (paper.Top - box.MinY + bleedY) * scaleY);
            warnings.AddRange(CheckWeight(Filtered(SvgDocument.FromSvg<SvgDocument>(StripDeclaration(svgXml)), ToPdf), bitmaps, fileBytes));
        }
        catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException or ArgumentException)
        {
            // The PDF is sound and stays; only the measurement failed, and that is said rather than read as
            // "every bitmap is fine".
            warnings.Add($"The bitmaps in the PDF could not be measured ({ex.Message}), so their print resolution is unchecked.");
        }

        return new PdfPrintReport(path, trim, bleedPt, scaleX / PrintSize.PointsPerMm, warnings, bitmaps, unplaced, fileBytes);
    }

    /// <summary>Bitmaps that make a print file heavy, and the filter that put each one there.</summary>
    /// <remarks>
    /// <para>
    /// PDF has no filter primitives, so Skia rasterises a filtered element whole, at <see cref="RasterDpi"/>, over the
    /// filter's region — and nothing said so. poster5's grain filter on the image group of a 27 x 40 in sheet became one
    /// 8,177 x 5,367 px bitmap, 20.3 MB of the file: <c>final_a.pdf</c> was 20.5 MB against its sibling's 228 KB, the only difference the
    /// filter, and the report's one line about it read "1 bitmap(s) placed; the lowest prints at 300 ppi".
    /// </para>
    /// <para>
    /// A bitmap is put down to a filter when most of it lies inside that element's filter region. Reported when the
    /// filter's bitmaps take <see cref="HeavyBitmapBytes"/> or more and are most of the file, or take
    /// <see cref="HeavyFileBytes"/> whatever else is in it; and, whatever drew them, when the file is
    /// <see cref="HeavyFileBytes"/> or more and other bitmaps are most of it.
    /// </para>
    /// </remarks>
    private static IEnumerable<string> CheckWeight(IReadOnlyList<FilteredElement> filtered, IReadOnlyList<PlacedBitmap> bitmaps, long fileBytes)
    {
        // An image placed twice is stored once.
        var unique = bitmaps.Where(b => b.StoredBytes > 0).GroupBy(b => b.ObjectNumber == 0 ? b.GetHashCode() : b.ObjectNumber)
            .Select(g => g.First()).ToList();
        var claimed = new HashSet<PlacedBitmap>();

        foreach (var element in filtered)
        {
            var mine = unique.Where(b => !claimed.Contains(b) && Area(SKRect.Intersect(b.Bounds, element.Region)) > 0.5f * Area(b.Bounds)).ToList();
            var bytes = mine.Sum(b => b.StoredBytes);
            if (mine.Count == 0 || !(bytes >= HeavyBitmapBytes && bytes * 2 >= fileBytes || bytes >= HeavyFileBytes)) continue;
            claimed.UnionWith(mine);

            var largest = mine.MaxBy(b => (long)b.PixelWidth * b.PixelHeight)!;
            var rest = Math.Max(0, fileBytes - unique.Sum(b => b.StoredBytes));
            yield return $"The filter on {element.Name} ({element.Filter}) has no PDF equivalent, so everything it covers was "
              + Invariant($"rasterised at {RasterDpi:0} ppi into {(mine.Count == 1 ? "one" : $"{mine.Count} bitmaps, the largest")} ")
              + Invariant($"{largest.PixelWidth:N0} x {largest.PixelHeight:N0} px{(mine.Count == 1 ? " bitmap" : "")} over ")
              + Invariant($"{largest.WidthPt / PrintSize.PointsPerMm:0} x {largest.HeightPt / PrintSize.PointsPerMm:0} mm — ")
              + $"{Size(bytes)} of this {Size(fileBytes)} file; the rest of the page is {Size(rest)}. Keep it if the effect is worth "
              + "that — a print shop will take the file — but know it is the price. To cut it: put the filter on only the element "
              + "that needs it rather than a group spanning the page, keep it on the screen render and leave it out of the print "
              + "file, or draw the effect as vector.";
        }

        var others = unique.Where(b => !claimed.Contains(b)).ToList();
        var otherBytes = others.Sum(b => b.StoredBytes);
        if (fileBytes >= HeavyFileBytes && otherBytes * 2 >= fileBytes)
        {
            var largest = others.MaxBy(b => b.StoredBytes)!;
            yield return $"The PDF is {Size(fileBytes)}, and {Size(otherBytes)} of it is {others.Count} bitmap(s); the largest is "
              + Invariant($"{largest.PixelWidth:N0} x {largest.PixelHeight:N0} px ({Size(largest.StoredBytes)}), printing at {largest.EffectivePpi:0} ppi. ")
              + "Placed images print well at 300 ppi; more is weight, not detail. Resize any placed far above that before placing it.";
        }
    }

    /// <summary>Every element drawn through a filter, named for a warning, with its filter region in PDF space.</summary>
    private static IReadOnlyList<FilteredElement> Filtered(SvgDocument document, Func<SKRect, SKRect> toPdf)
    {
        var found = new List<FilteredElement>();
        foreach (var element in Descendants(document).OfType<SvgVisualElement>())
        {
            if (element.Filter is not { } uri || IsDefinition(element)) continue;
            var id = uri.OriginalString.Trim().TrimStart('#');
            if (id.StartsWith("url(", StringComparison.OrdinalIgnoreCase)) id = id[4..].TrimEnd(')').Trim().TrimStart('#');
            if (document.GetElementById(id) is not SvgElement filter) continue;

            SKRect region;
            try
            {
                // The element's own box, before its own transform, which Transform below applies with its ancestors'.
                var b = SnapElement.Wrap(element).GetBBox(isWithoutTransform: true);
                if (b.Width <= 0 || b.Height <= 0) continue;
                // The default filter region: the element's box and a tenth again on every side.
                var paper = PosterTypeAudit.Transform(element).MapRect(SKRect.Create(b.X - b.Width / 10, b.Y - b.Height / 10, b.Width * 1.2f, b.Height * 1.2f));
                region = toPdf(paper);
            }
            catch (Exception)
            {
                // An element whose box Svg cannot compute is not placed; the file-size check still covers its bitmap.
                continue;
            }

            var primitives = filter.Children.Select(ElementName).Distinct().ToArray();
            var elementId = string.IsNullOrEmpty(element.ID) ? "" : $" id=\"{element.ID}\"";
            found.Add(new FilteredElement($"<{ElementName(element)}{elementId}>",
                $"#{id}{(primitives.Length == 0 ? "" : ": " + string.Join(", ", primitives))}", region));
        }
        return found;
    }

    /// <summary>The element's SVG name — <c>g</c>, <c>feTurbulence</c> — as the document spells it.</summary>
    private static string ElementName(SvgElement element) =>
        (Attribute.GetCustomAttribute(element.GetType(), typeof(SvgElementAttribute)) as SvgElementAttribute)?.ElementName
        ?? element.GetType().Name.Replace("Svg", "", StringComparison.Ordinal);

    private static string Size(long bytes) => bytes >= 1_000_000
        ? Invariant($"{bytes / 1_000_000.0:0.0} MB")
        : Invariant($"{Math.Max(1, (int)Math.Round(bytes / 1_000.0))} KB");

    private static float Area(SKRect r) => r.IsEmpty ? 0 : r.Width * r.Height;

    /// <summary>An element drawn through a filter: how a warning names it and its filter, and where it prints.</summary>
    private sealed record FilteredElement(string Name, string Filter, SKRect Region);

    /// <summary>Bitmaps that print below <see cref="MinimumPpi"/>, each with the pixels it would need.</summary>
    /// <remarks>
    /// 150 ppi is the floor for a poster read from a step or two away; 300 is the usual target for print held
    /// in the hand, and twice the 150 lpi screen Manual 14's rule is written for. A poster can stand below 300; it
    /// cannot stand below 150 without looking soft at the distance anyone reads its smaller type.
    /// </remarks>
    private static IEnumerable<string> CheckResolution(IReadOnlyList<PlacedBitmap> bitmaps, int unplaced, float mediaHeight, float bleedPt)
    {
        const int MaxReported = 5;
        var low = bitmaps.Where(b => b.PixelWidth > 0 && b.EffectivePpi < MinimumPpi).OrderBy(b => b.EffectivePpi).ToList();

        foreach (var b in low.Take(MaxReported))
        {
            var widthIn = b.WidthPt / 72f;
            var heightIn = b.HeightPt / 72f;
            yield return Invariant($"A {b.PixelWidth} x {b.PixelHeight} px bitmap prints {b.WidthPt / PrintSize.PointsPerMm:0} x {b.HeightPt / PrintSize.PointsPerMm:0} mm, ")
              + Invariant($"at {b.EffectivePpi:0} ppi — under {MinimumPpi:0}, so it will print soft or pixelated. ")
              + Invariant($"At that size it needs {MathF.Ceiling(widthIn * MinimumPpi):0} x {MathF.Ceiling(heightIn * MinimumPpi):0} px for {MinimumPpi:0} ppi ")
              + Invariant($"({MathF.Ceiling(widthIn * 300):0} x {MathF.Ceiling(heightIn * 300):0} for 300), or it must print smaller. ")
              + Invariant($"It sits {(b.Bounds.Left - bleedPt) / PrintSize.PointsPerMm:0} mm from the left and ")
              + Invariant($"{(mediaHeight - b.Bounds.Bottom - bleedPt) / PrintSize.PointsPerMm:0} mm from the top of the trim.");
        }

        if (low.Count > MaxReported)
            yield return $"{low.Count - MaxReported} more bitmaps print under {MinimumPpi:0} ppi.";

        if (unplaced > 0)
            // Image objects, not source bitmaps: Skia can write one bitmap more than once, and a pattern fill
            // measured at 2026-10-16 wrote two objects for one image.
            yield return $"The PDF holds bitmaps drawn some other way than a plain placement — a pattern fill, say ({unplaced} image "
              + "object(s)) — so their print resolution was not measured. Check their pixel size against the area they fill by hand.";
    }

    /// <summary>
    /// Sides where artwork that reaches the trim stops there instead of continuing into the bleed.
    /// </summary>
    /// <remarks>
    /// Rendered small — a few pixels across the bleed is enough to tell ink from paper — and compared
    /// one pixel inside the trim against one pixel inside the outer edge. Paper-white and transparent
    /// both count as no ink, so a white background is not a fault.
    /// </remarks>
    private static IEnumerable<string> CheckBleed(SKPicture picture, SKMatrix toPage, float mediaWidth, float mediaHeight, float bleedPt)
    {
        var k = MathF.Min(4f / bleedPt, 2400f / MathF.Max(mediaWidth, mediaHeight));
        var bleedPx = bleedPt * k;
        if (bleedPx < 2) yield break;

        var w = (int)MathF.Ceiling(mediaWidth * k);
        var h = (int)MathF.Ceiling(mediaHeight * k);
        using var bitmap = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            var matrix = SKMatrix.CreateScale(k, k).PreConcat(toPage);
            canvas.DrawPicture(picture, in matrix);
        }

        var inner = (int)MathF.Ceiling(bleedPx) + 1;
        var outer = 1;
        var sides = new (string Name, Func<int, (int X, int Y)> Trim, Func<int, (int X, int Y)> Edge, int From, int To)[]
        {
            ("top", i => (i, inner), i => (i, outer), inner, w - inner),
            ("bottom", i => (i, h - 1 - inner), i => (i, h - 1 - outer), inner, w - inner),
            ("left", i => (inner, i), i => (outer, i), inner, h - inner),
            ("right", i => (w - 1 - inner, i), i => (w - 1 - outer, i), inner, h - inner),
        };

        foreach (var side in sides)
        {
            var length = side.To - side.From;
            if (length <= 0) continue;

            var stops = Enumerable.Range(side.From, length).Count(i =>
            {
                var (tx, ty) = side.Trim(i);
                var (ex, ey) = side.Edge(i);
                return Inked(bitmap.GetPixel(tx, ty)) && !Inked(bitmap.GetPixel(ex, ey));
            });

            var fraction = (float)stops / length;
            if (fraction > 0.02f)
            {
                yield return Invariant($"Artwork stops at the trim along the {side.Name} edge ({fraction:P0} of its length): it reaches the ")
                  + "cut but not the bleed, so a cut that drifts leaves a white sliver. Draw anything that touches "
                  + Invariant($"the edge {bleedPt / PrintSize.PointsPerMm:0.#} mm further, past the paper's edge.");
            }
        }
    }

    /// <summary>Widens the viewport by the bleed on every side, in the paper's own units, so what was drawn past its edges is inside the picture.</summary>
    private static void Widen(SvgDocument document, SvgViewBox box, float bleedX, float bleedY)
    {
        document.ViewBox = new SvgViewBox(box.MinX - bleedX, box.MinY - bleedY, box.Width + 2 * bleedX, box.Height + 2 * bleedY);
        document.Width = new SvgUnit(box.Width + 2 * bleedX);
        document.Height = new SvgUnit(box.Height + 2 * bleedY);
    }

    /// <summary>
    /// Text that comes within <paramref name="safePt"/> of the trim, or crosses it, named by what it says.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Measured on the rendered text rather than on its <c>x</c> and <c>y</c>: an anchor says nothing about where
    /// a centred line ends, and the renderer has already applied every transform, font and clip. Everything but
    /// the text is taken out of a copy of the document — the shapes, the images, the background — and each text
    /// element is drawn alone, so a warning can quote it. Definitions stay, because a text's clip or mask lives there.
    /// </para>
    /// <para>
    /// <b>What it cannot see:</b> lettering converted to paths, and type drawn into a bitmap. Both are pictures of
    /// text, not text.
    /// </para>
    /// </remarks>
    private static IEnumerable<string> CheckSafeArea(string svgXml, SvgViewBox box, float bleedX, float bleedY,
        float mediaWidth, float mediaHeight, float bleedPt, float safePt)
    {
        const int MaxNamed = 30;
        const int MaxReported = 8;

        var document = SvgDocument.FromSvg<SvgDocument>(StripDeclaration(svgXml));
        Widen(document, box, bleedX, bleedY);

        foreach (var element in Descendants(document).ToList())
        {
            if (IsDefinition(element) || element is SvgTextBase || element.Parent is SvgTextBase) continue;

            var keep = element switch
            {
                SvgGroup or SvgFragment => true,
                SvgUse use => use.ReferencedElement is { } href
                    && document.GetElementById(href.OriginalString.TrimStart('#')) is { } target
                    && (target is SvgTextBase || Descendants(target).Any(d => d is SvgTextBase)),
                SvgVisualElement => false,
                _ => true,
            };
            if (!keep) element.Parent?.Children.Remove(element);
        }

        var texts = Descendants(document).OfType<SvgTextBase>()
            .Where(t => t.Parent is not SvgTextBase && !IsDefinition(t))
            .ToList();
        if (texts.Count == 0) yield break;

        // Small: about eight pixels across the safe margin is enough to tell a millimetre or two.
        var k = MathF.Min(8f / safePt, 2000f / MathF.Max(mediaWidth, mediaHeight));
        var w = (int)MathF.Ceiling(mediaWidth * k);
        var h = (int)MathF.Ceiling(mediaHeight * k);
        var matrix = SKMatrix.CreateScale(k * mediaWidth / document.Width.Value, k * mediaHeight / document.Height.Value);

        // Drawn one at a time when there are few enough to name; otherwise together, as "text".
        var passes = texts.Count <= MaxNamed
            ? texts.Select(t => (Label: Quote(t), Shown: (IReadOnlyList<SvgTextBase>)[t]))
            : [("Text", texts)];

        var reported = 0;
        foreach (var (label, shown) in passes)
        {
            var display = texts.ToDictionary(t => t, t => t.Display);
            foreach (var t in texts) t.Display = shown.Contains(t) ? display[t] : "none";

            using var skSvg = new SKSvg();
            skSvg.FromSvgDocument(SvgTracking.ForRender(document));
            foreach (var t in texts) t.Display = display[t];
            if (skSvg.Picture is null) continue;

            using var bitmap = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.DrawPicture(skSvg.Picture, in matrix);
            }

            foreach (var (side, distancePt) in NearestToTrim(bitmap, k, bleedPt, safePt))
            {
                if (reported++ >= MaxReported) yield break;
                var mm = distancePt / PrintSize.PointsPerMm;
                yield return distancePt < 0
                    ? Invariant($"{label} crosses the {side} trim by about {-mm:0.#} mm and will be cut off. ")
                      + Invariant($"Keep text at least {safePt / PrintSize.PointsPerMm:0.#} mm inside the paper's edge.")
                    : Invariant($"{label} comes within about {mm:0.#} mm of the {side} trim. Keep text at least ")
                      + Invariant($"{safePt / PrintSize.PointsPerMm:0.#} mm inside the paper's edge, or a cut that drifts can clip it.");
            }
        }
    }

    /// <summary>For each side the drawn ink comes closer to than <paramref name="safePt"/>, how close, in points; negative past the trim.</summary>
    /// <remarks>Only the bands along the edges are read: ink further in than the margin cannot be the nearest to an edge that matters.</remarks>
    private static IEnumerable<(string Side, float DistancePt)> NearestToTrim(SKBitmap bitmap, float k, float bleedPt, float safePt)
    {
        var pixels = bitmap.GetPixelSpan();
        int w = bitmap.Width, h = bitmap.Height, rowBytes = bitmap.RowBytes;
        float left = bleedPt * k, top = bleedPt * k, right = w - bleedPt * k, bottom = h - bleedPt * k;
        var band = (int)MathF.Ceiling((bleedPt + safePt) * k) + 1;

        float nearLeft = float.MaxValue, nearRight = float.MaxValue, nearTop = float.MaxValue, nearBottom = float.MaxValue;
        for (var y = 0; y < h; y++)
        {
            var edgeRow = y < band || y >= h - band;
            for (var x = 0; x < w; x++)
            {
                if (!edgeRow && x == band) x = Math.Max(band, w - band);
                if (pixels[y * rowBytes + x * 4 + 3] < 64) continue;

                float cx = x + 0.5f, cy = y + 0.5f;
                nearLeft = MathF.Min(nearLeft, cx - left);
                nearRight = MathF.Min(nearRight, right - cx);
                nearTop = MathF.Min(nearTop, cy - top);
                nearBottom = MathF.Min(nearBottom, bottom - cy);
            }
        }

        var limit = safePt * k;
        foreach (var (side, near) in new[] { ("top", nearTop), ("bottom", nearBottom), ("left", nearLeft), ("right", nearRight) })
        {
            if (near < limit) yield return (side, near / k);
        }
    }

    /// <summary>A text element as a warning names it: its words, quoted and shortened.</summary>
    private static string Quote(SvgTextBase text)
    {
        static string Words(SvgElement e) =>
            (e is SvgTextBase t ? t.Text ?? "" : "") + " " + string.Concat(e.Children.Select(Words));

        var words = Whitespace().Replace(Words(text), " ").Trim();
        if (words.Length == 0) return "A text element";
        return "\"" + (words.Length > 40 ? words[..39] + "…" : words) + "\"";
    }

    private static bool IsDefinition(SvgElement element)
    {
        for (var e = element.Parent; e is not null; e = e.Parent)
        {
            if (e is SvgDefinitionList or SvgClipPath or SvgMask or SvgPatternServer or SvgMarker or SvgSymbol) return true;
        }
        return false;
    }

    private static IEnumerable<SvgElement> Descendants(SvgElement element) =>
        element.Children.SelectMany(c => Descendants(c).Prepend(c));

    private static bool Inked(SKColor c) => c.Alpha >= 128 && !(c.Red >= 245 && c.Green >= 245 && c.Blue >= 245);

    /// <summary>Appends a revision of every page object carrying a TrimBox and a BleedBox.</summary>
    /// <exception cref="InvalidOperationException">The file's structure is not the one Skia writes.</exception>
    private static void AddPrintBoxes(string path, float mediaWidth, float mediaHeight, float bleedPt)
    {
        // Latin-1 maps byte for byte, so every index in the string is the byte offset the xref needs.
        var text = Encoding.Latin1.GetString(File.ReadAllBytes(path));

        var trailer = Trailer().Matches(text).LastOrDefault()
            ?? throw new InvalidOperationException("The PDF has no classic trailer (it may use an xref stream), so its TrimBox could not be added.");
        var startXref = StartXref().Matches(text).LastOrDefault()
            ?? throw new InvalidOperationException("The PDF has no startxref, so its TrimBox could not be added.");

        string Ref(string key) => Regex.Match(trailer.Groups["dict"].Value, $@"/{key}\s+(\d+\s+\d+\s+R)") is { Success: true } m ? m.Groups[1].Value : "";

        var trimBox = Box(bleedPt, bleedPt, mediaWidth - bleedPt, mediaHeight - bleedPt);
        var bleedBox = Box(0, 0, mediaWidth, mediaHeight);

        var update = new StringBuilder(text.EndsWith('\n') ? "" : "\n");
        var offsets = new List<(int Number, int Generation, int Offset)>();

        foreach (Match page in PageObject().Matches(text))
        {
            // Skia rounds the MediaBox it writes — 1207.44 pt for an A2 page with bleed that is 1207.56 —
            // so it is restated exactly here, or the BleedBox would overhang the media it must sit inside.
            var dict = MediaBox().Replace(page.Groups["dict"].Value, "");
            var lastClose = dict.LastIndexOf(">>", StringComparison.Ordinal);
            var revised = dict[..lastClose] + $" /MediaBox {bleedBox} /BleedBox {bleedBox} /TrimBox {trimBox}" + dict[lastClose..];

            var number = int.Parse(page.Groups["num"].Value, CultureInfo.InvariantCulture);
            var generation = int.Parse(page.Groups["gen"].Value, CultureInfo.InvariantCulture);
            offsets.Add((number, generation, text.Length + update.Length));
            update.Append(CultureInfo.InvariantCulture, $"{number} {generation} obj\n{revised}\nendobj\n");
        }

        if (offsets.Count == 0)
            throw new InvalidOperationException("No page object was found in the PDF, so its TrimBox could not be added.");

        var xrefOffset = text.Length + update.Length;
        update.Append("xref\n");
        foreach (var (number, generation, offset) in offsets.OrderBy(o => o.Number))
        {
            // Each entry is exactly twenty bytes, end-of-line included, or the table cannot be indexed.
            update.Append(CultureInfo.InvariantCulture, $"{number} 1\n{offset:D10} {generation:D5} n\r\n");
        }

        var size = Regex.Match(trailer.Groups["dict"].Value, @"/Size\s+(\d+)").Groups[1].Value;
        var info = Ref("Info") is { Length: > 0 } i ? $" /Info {i}" : "";
        update.Append(CultureInfo.InvariantCulture,
            $"trailer\n<< /Size {size} /Root {Ref("Root")}{info} /Prev {startXref.Groups["offset"].Value} >>\nstartxref\n{xrefOffset}\n%%EOF\n");

        using var stream = new FileStream(path, FileMode.Append);
        stream.Write(Encoding.Latin1.GetBytes(update.ToString()));
    }

    private static string Box(float x0, float y0, float x1, float y1) =>
        string.Create(CultureInfo.InvariantCulture, $"[{x0:0.###} {y0:0.###} {x1:0.###} {y1:0.###}]");

    private static string StripDeclaration(string svgXml) =>
        svgXml.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) && svgXml.IndexOf("?>", StringComparison.Ordinal) is var i and >= 0
            ? svgXml[(i + 2)..].TrimStart()
            : svgXml;

    /// <summary>A page object: a dictionary of <c>/Type /Page</c>, not <c>/Pages</c>, with no stream.</summary>
    [GeneratedRegex(@"(?<num>\d+) (?<gen>\d+) obj\s*(?<dict><<(?:(?!endobj|stream).)*?/Type\s*/Page(?![A-Za-z])(?:(?!endobj|stream).)*>>)\s*endobj", RegexOptions.Singleline)]
    private static partial Regex PageObject();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"/MediaBox\s*\[[^\]]*\]")]
    private static partial Regex MediaBox();

    [GeneratedRegex(@"trailer\s*(?<dict><<.*?>>)\s*startxref", RegexOptions.Singleline)]
    private static partial Regex Trailer();

    [GeneratedRegex(@"startxref\s+(?<offset>\d+)")]
    private static partial Regex StartXref();
    #endregion
}
