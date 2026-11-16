// SPDX-License-Identifier: MIT
namespace Polson.Drawing.Svg;

using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;
using static System.FormattableString;

/// <summary>
/// Where a paper's type collides with something: another line, the edge of the page, or a shape it half covers.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured because it is not seen.</b> Every Nemotron logo run (nemologo2–5, 2026-10-19/20) set type across its own
/// mood board — a specimen over the colour swatches, a wordmark running from one column into the next, a caption under a
/// shape — and called the board clean, having looked at it. Gemini did it too, once (geminilogo2: two palette captions
/// running into each other). A collision is a fact about geometry, so the engine states it on every paper render, as
/// <see cref="FontAudit"/> states a substituted font, and the agent no longer has to notice it.
/// </para>
/// <para>
/// <b>Boxes, not ink.</b> A line is its <see cref="TypeLine.Bounds"/>: the font's ascent to its descent over the measured
/// advance. That is wider than the ink, so the thresholds below leave room: stacked lines at ordinary leading overlap
/// their boxes by well under the fifth that counts as a collision.
/// </para>
/// <para>
/// <b>What is not a collision:</b> type wholly inside a panel or card (text on a button, a label in a swatch), a shape
/// wholly inside a line's box and small beside it (a bullet), an underline in the descent below the baseline, and a shape
/// painted over — an opaque band laid on it before the type, covering it wherever the line meets it (poster5's mirror
/// ball under its title band). A line, or any group around it, marked <c>data-print-exempt="layout"</c> is left out.
/// </para>
/// </remarks>
public static class LayoutAudit
{
    #region Fields
    /// <summary>The name <see cref="PosterTypeAudit.ExemptAttribute"/> uses for this audit.</summary>
    public const string LayoutCheck = "layout";

    /// <summary>Two line boxes sharing more than this of the smaller one's area are set on top of each other.</summary>
    public const float CollisionShare = 0.2f;

    /// <summary>A line with between this much and (1 − this) of its box on a shape straddles the shape's edge.</summary>
    public const float StraddleShare = 0.08f;

    private const int MaxReported = 8;
    #endregion

    #region Methods
    /// <summary>Every collision on the paper, worded for the agent that drew it. Empty when there are none.</summary>
    public static IReadOnlyList<string> Check(string svgXml)
    {
        if (string.IsNullOrWhiteSpace(svgXml)) return [];
        SvgDocument document;
        try { document = PosterTypeAudit.Parse(svgXml); }
        catch (Exception) { return []; }

        var lines = PosterTypeAudit.Lines(document)
            .Where(l => l.Upright && l.Bounds.Width > 0 && l.Bounds.Height > 0 && !l.Exempt.Contains(LayoutCheck))
            .ToList();
        if (lines.Count == 0) return [];

        var page = document.ViewBox is { Width: > 0, Height: > 0 } vb
            ? SKRect.Create(vb.MinX, vb.MinY, vb.Width, vb.Height)
            : SKRect.Create(0, 0, document.Width.Value, document.Height.Value);

        return CheckMarks(lines, Shapes(document), page);
    }

    /// <summary>
    /// The audit on lines and shapes already placed in one coordinate space — a paper's units, or a canvas's pixels.
    /// </summary>
    /// <remarks>A canvas records its own (<c>SkiaCanvas.AuditLayout</c>): five of six mood boards were drawn with
    /// <c>ctx.fillText</c>, where there is no SVG to read.</remarks>
    public static IReadOnlyList<string> CheckMarks(IReadOnlyList<TypeLine> lines, IReadOnlyList<LayoutShape> shapes, SKRect page)
    {
        var upright = lines.Where(l => l.Upright && l.Bounds.Width > 0 && l.Bounds.Height > 0 && !l.Exempt.Contains(LayoutCheck))
            .ToList();
        if (upright.Count == 0) return [];

        var found = new List<string>();
        found.AddRange(OnTopOfEachOther(upright));
        found.AddRange(OffThePage(upright, page));
        found.AddRange(AcrossShapes(shapes, upright));
        found = [.. found.Distinct(StringComparer.Ordinal)];

        return found.Count <= MaxReported
            ? found
            : [.. found.Take(MaxReported), $"{found.Count - MaxReported} more collisions like these."];
    }

    /// <summary>Pairs of lines whose boxes share more than <see cref="CollisionShare"/> of the smaller.</summary>
    public static IEnumerable<string> OnTopOfEachOther(IReadOnlyList<TypeLine> lines)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            for (var j = i + 1; j < lines.Count; j++)
            {
                var (a, b) = (lines[i], lines[j]);
                var shared = Area(SKRect.Intersect(a.Bounds, b.Bounds));
                var smaller = MathF.Min(Area(a.Bounds), Area(b.Bounds));
                if (smaller <= 0 || shared <= 0) continue;

                // On one row — two captions running into each other end to end (geminilogo2) — any overlap past a
                // sliver is a collision, however small a share of the lines it is. Stacked lines overlap their boxes
                // a little at ordinary leading, so for them it takes the area threshold.
                var rowShare = Overlap1D(a.Bounds.Top, a.Bounds.Bottom, b.Bounds.Top, b.Bounds.Bottom)
                             / MathF.Min(a.Bounds.Height, b.Bounds.Height);
                var across = Overlap1D(a.Bounds.Left, a.Bounds.Right, b.Bounds.Left, b.Bounds.Right);
                var sameRow = rowShare > 0.6f && across > MathF.Max(1f, 0.1f * MathF.Min(a.SizePaper, b.SizePaper));
                if (!sameRow && shared / smaller <= CollisionShare) continue;
                yield return Invariant($"{PosterTypeAudit.Quote(a.Words)} and {PosterTypeAudit.Quote(b.Words)} are set on top of each other ")
                  + Invariant($"({shared / smaller:P0} of the smaller line). Move one, or give each its own space.");
            }
        }
    }

    /// <summary>Lines that run past an edge of the page, and so are cut off in the render.</summary>
    public static IEnumerable<string> OffThePage(IReadOnlyList<TypeLine> lines, SKRect page)
    {
        foreach (var line in lines)
        {
            var b = line.Bounds;
            var tolerance = MathF.Max(1f, line.SizePaper * 0.05f);
            var (edge, by) = new[]
            {
                ("right", b.Right - page.Right), ("left", page.Left - b.Left),
                ("bottom", b.Bottom - page.Bottom), ("top", page.Top - b.Top),
            }.MaxBy(e => e.Item2);
            if (by <= tolerance) continue;
            yield return Invariant($"{PosterTypeAudit.Quote(line.Words)} runs off the {edge} edge of the page by {by:0} units, so it is cut off ")
              + "in the render. Make it smaller, break it, or move it inside the page.";
        }
    }

    /// <summary>
    /// Lines that straddle a visible shape's edge, or that a divider line runs through — unless an opaque shape painted
    /// between the two covers the shape wherever it meets the line, so the crossing is never seen.
    /// </summary>
    public static IEnumerable<string> AcrossShapes(IReadOnlyList<LayoutShape> shapes, IReadOnlyList<TypeLine> lines)
    {
        foreach (var shape in shapes)
        {
            foreach (var line in lines)
            {
                if (Collides(shape, line) is { } how && !Hidden(shape, line, shapes))
                    yield return Invariant($"{PosterTypeAudit.Quote(line.Words)} {how}.");
            }
        }
    }

    /// <summary>The visible shapes a paper draws, in paper units, that the audit can place exactly, in paint order.</summary>
    public static IReadOnlyList<LayoutShape> Shapes(SvgDocument document)
    {
        var order = PosterTypeAudit.PaintOrder(document);
        return
        [
            .. PosterTypeAudit.Descendants(document).OfType<SvgVisualElement>()
                .Where(e => e is not (SvgTextBase or SvgGroup) && !PosterTypeAudit.IsDefinition(e) && Visible(e)
                            && !PosterTypeAudit.Exemptions(e).Contains(LayoutCheck))
                .Select(e => Shape(e) is { } s ? s with { Order = order[e], Opaque = Opaque(e) } : null)
                .OfType<LayoutShape>()
        ];
    }
    #endregion

    #region Methods (private)
    private const string ClearOfRule = "Move the line of type or the rule so they no longer cross";

    private static string? Collides(LayoutShape shape, TypeLine line)
    {
        var text = line.Bounds;
        var thin = MathF.Max(1f, line.SizePaper * 0.2f);

        // A divider: a line element, or a rectangle too thin to hold anything.
        if (shape.From is { } from && shape.To is { } to)
            return Crosses(from, to, text) ? Invariant($"has a {Direction(from, to)} line running through it. {ClearOfRule}") : null;
        if (!shape.Round && (shape.Box.Width < thin || shape.Box.Height < thin))
        {
            var mid = new SKPoint(shape.Box.MidX, shape.Box.MidY);
            var (a, b) = shape.Box.Width < shape.Box.Height
                ? (new SKPoint(mid.X, shape.Box.Top), new SKPoint(mid.X, shape.Box.Bottom))
                : (new SKPoint(shape.Box.Left, mid.Y), new SKPoint(shape.Box.Right, mid.Y));
            return Crosses(a, b, text) ? Invariant($"has a {Direction(a, b)} bar running through it. {ClearOfRule}") : null;
        }

        var share = shape.Outline is { } outline ? ShareInPath(outline, text)
                  : shape.Round ? ShareInEllipse(shape.Box, text)
                  : Area(SKRect.Intersect(shape.Box, text)) / Area(text);

        // **A label a little too long for its box.** Mostly on the shape, it is no straddle, and the share above let it
        // through: a pill or panel whose text runs past its side by a few letters. Measured on the sides only — a line's box
        // runs from the font's ascent to its descent, taller than its letters, so its top and bottom would cry wolf.
        if (share >= 1 - StraddleShare && share < 1 && shape.Outline is null && !shape.Round
            && text.MidY > shape.Box.Top && text.MidY < shape.Box.Bottom
            && (Side: shape.Box.Left - text.Left > text.Right - shape.Box.Right ? "left" : "right",
                By: MathF.Max(shape.Box.Left - text.Left, text.Right - shape.Box.Right)) is var (side, by)
            && by > MathF.Max(1f, line.SizePaper * 0.1f))
            return Invariant($"runs past the {side} side of the {shape.Name} it sits on at ({shape.Box.Left:0}, {shape.Box.Top:0}, ")
                 + Invariant($"{shape.Box.Width:0}×{shape.Box.Height:0}) by {by:0} units: the box is too small for it. ")
                 + "Widen the box, or set the line smaller or shorter";

        // **A line that runs into its neighbour.** Barely on the shape, it is no straddle either: all_in_2025 (2026-10-30)
        // set its title to end a few letters inside the panel beside it. Entering from one side only, level with the shape:
        // a caption above a bar, whose box dips into it, and a shape the line passes clean over, are neither.
        if (share > 0 && share <= StraddleShare && shape.Outline is null && !shape.Round
            && text.MidY > shape.Box.Top && text.MidY < shape.Box.Bottom
            && (text.Left < shape.Box.Left && text.Right < shape.Box.Right ? text.Right - shape.Box.Left
                : text.Right > shape.Box.Right && text.Left > shape.Box.Left ? shape.Box.Right - text.Left : 0f) is var into
            && into > MathF.Max(1f, line.SizePaper * 0.05f))
            return Invariant($"runs into the {shape.Name} beside it at ({shape.Box.Left:0}, {shape.Box.Top:0}, ")
                 + Invariant($"{shape.Box.Width:0}×{shape.Box.Height:0}) by {into:0} units. ")
                 + "Shorten or shrink the line, or move it or the shape apart";

        if (share <= StraddleShare || share >= 1 - StraddleShare) return null;

        // A small shape inside the line's box is a bullet or an icon, not a collision.
        var inside = text.Contains(shape.Box) || (shape.Box.Left >= text.Left && shape.Box.Right <= text.Right
                                                  && shape.Box.Top >= text.Top && shape.Box.Bottom <= text.Bottom);
        if (inside && Area(shape.Box) < Area(text) * 0.25f) return null;

        return Invariant($"runs across the edge of a {shape.Name} at ({shape.Box.Left:0}, {shape.Box.Top:0}, ")
             + Invariant($"{shape.Box.Width:0}×{shape.Box.Height:0}): {share:P0} of the line is on it and the rest is off. ")
             + "Keep the line wholly on the shape, or clear of it";
    }

    /// <summary>Whether a segment passes through a line's box, clear of the descent where an underline sits.</summary>
    private static bool Crosses(SKPoint a, SKPoint b, SKRect text)
    {
        var vertical = MathF.Abs(b.X - a.X) < MathF.Abs(b.Y - a.Y);
        if (vertical)
        {
            var x = (a.X + b.X) / 2;
            var inset = text.Width * 0.1f;
            return x > text.Left + inset && x < text.Right - inset
                && MathF.Min(a.Y, b.Y) < text.Top + text.Height * 0.3f && MathF.Max(a.Y, b.Y) > text.Bottom - text.Height * 0.35f;
        }
        var y = (a.Y + b.Y) / 2;
        var spans = MathF.Min(MathF.Max(a.X, b.X), text.Right) - MathF.Max(MathF.Min(a.X, b.X), text.Left);
        return y > text.Top + text.Height * 0.3f && y < text.Bottom - text.Height * 0.35f && spans > text.Width * 0.2f;
    }

    private static string Direction(SKPoint a, SKPoint b) => MathF.Abs(b.X - a.X) < MathF.Abs(b.Y - a.Y) ? "vertical" : "horizontal";

    /// <summary>The share of a line's box inside a path's filled area, sampled on the same grid as an ellipse.</summary>
    private static float ShareInPath(SKPath outline, SKRect text)
    {
        if (SKRect.Intersect(outline.Bounds, text).IsEmpty) return 0;
        const int Columns = 12, Rows = 4;
        var inside = 0;
        for (var i = 0; i < Columns; i++)
        {
            for (var j = 0; j < Rows; j++)
            {
                if (outline.Contains(text.Left + text.Width * (i + 0.5f) / Columns, text.Top + text.Height * (j + 0.5f) / Rows))
                    inside++;
            }
        }
        return inside / (float)(Columns * Rows);
    }

    private static float Overlap1D(float a0, float a1, float b0, float b1) => MathF.Max(0, MathF.Min(a1, b1) - MathF.Max(a0, b0));

    /// <summary>The share of a line's box inside an ellipse, sampled on a grid.</summary>
    private static float ShareInEllipse(SKRect ellipse, SKRect text)
    {
        if (ellipse.Width <= 0 || ellipse.Height <= 0 || SKRect.Intersect(ellipse, text).IsEmpty) return 0;
        const int Columns = 12, Rows = 4;
        var (cx, cy, rx, ry) = (ellipse.MidX, ellipse.MidY, ellipse.Width / 2, ellipse.Height / 2);
        var inside = 0;
        for (var i = 0; i < Columns; i++)
        {
            for (var j = 0; j < Rows; j++)
            {
                var x = text.Left + text.Width * (i + 0.5f) / Columns;
                var y = text.Top + text.Height * (j + 0.5f) / Rows;
                var (dx, dy) = ((x - cx) / rx, (y - cy) / ry);
                if (dx * dx + dy * dy <= 1) inside++;
            }
        }
        return inside / (float)(Columns * Rows);
    }

    private static LayoutShape? Shape(SvgVisualElement element)
    {
        var m = PosterTypeAudit.Transform(element);
        switch (element)
        {
            case SvgRectangle r when r.Width.Value > 0 && r.Height.Value > 0:
                return new LayoutShape("rectangle", m.MapRect(SKRect.Create(r.X.Value, r.Y.Value, r.Width.Value, r.Height.Value)), false);
            case SvgImage image when image.Width.Value > 0 && image.Height.Value > 0:
                return new LayoutShape("image", m.MapRect(SKRect.Create(image.X.Value, image.Y.Value, image.Width.Value, image.Height.Value)), false);
            case SvgCircle c when c.Radius.Value > 0:
                return new LayoutShape("circle", m.MapRect(new SKRect(c.CenterX.Value - c.Radius.Value, c.CenterY.Value - c.Radius.Value,
                    c.CenterX.Value + c.Radius.Value, c.CenterY.Value + c.Radius.Value)), true);
            case SvgEllipse e when e.RadiusX.Value > 0 && e.RadiusY.Value > 0:
                return new LayoutShape("ellipse", m.MapRect(new SKRect(e.CenterX.Value - e.RadiusX.Value, e.CenterY.Value - e.RadiusY.Value,
                    e.CenterX.Value + e.RadiusX.Value, e.CenterY.Value + e.RadiusY.Value)), true);
            case SvgLine l:
                var (from, to) = (m.MapPoint(l.StartX.Value, l.StartY.Value), m.MapPoint(l.EndX.Value, l.EndY.Value));
                return new LayoutShape("line", SKRect.Create(MathF.Min(from.X, to.X), MathF.Min(from.Y, to.Y), MathF.Abs(to.X - from.X), MathF.Abs(to.Y - from.Y)), false, from, to);
            default:
                return null;   // Paths are left out: their box is not their shape, and a mark is often a path.
        }
    }

    /// <summary>
    /// Whether an opaque shape painted after <paramref name="shape"/> and before <paramref name="line"/> covers all of
    /// <paramref name="shape"/> that lies under the line, so the crossing is painted over before the type goes on.
    /// </summary>
    /// <remarks>
    /// poster5's tagline was reported across a mirror ball that sat behind an opaque band; the reader never sees that
    /// edge. Conservative by construction: without a known paint order for both, or a shape known to be opaque and
    /// known to cover, the warning stands.
    /// </remarks>
    private static bool Hidden(LayoutShape shape, TypeLine line, IReadOnlyList<LayoutShape> shapes)
    {
        if (shape.Order is not { } behind || line.Order is not { } type || type <= behind) return false;

        // The part of the line's box the shape could show in, widened a unit for its stroke and anti-aliasing.
        var under = SKRect.Intersect(line.Bounds, SKRect.Inflate(shape.Box, 1, 1));
        if (under.IsEmpty) return false;

        return shapes.Any(o => !ReferenceEquals(o, shape) && o.Opaque && o.Order is { } at && at > behind && at < type && Covers(o, under));
    }

    /// <summary>Whether an occluding shape's filled area contains the whole of <paramref name="region"/>.</summary>
    private static bool Covers(LayoutShape occluder, SKRect region)
    {
        if (occluder.From is not null) return false;
        if (occluder.Outline is { } outline)
        {
            using var box = new SKPath();
            box.AddRect(region);
            using var left = box.Op(outline, SKPathOp.Difference);
            return left is not null && (left.IsEmpty || Area(left.Bounds) < 1e-3f);
        }
        if (occluder.Round)
        {
            // An ellipse is convex, so it holds the rectangle when it holds all four corners.
            var (cx, cy, rx, ry) = (occluder.Box.MidX, occluder.Box.MidY, occluder.Box.Width / 2, occluder.Box.Height / 2);
            if (rx <= 0 || ry <= 0) return false;
            bool Inside(float x, float y) => MathF.Pow((x - cx) / rx, 2) + MathF.Pow((y - cy) / ry, 2) <= 1;
            return Inside(region.Left, region.Top) && Inside(region.Right, region.Top)
                && Inside(region.Left, region.Bottom) && Inside(region.Right, region.Bottom);
        }
        return occluder.Box.Contains(region);
    }

    /// <summary>
    /// Whether a shape certainly hides what is under it: a solid, fully opaque fill on an axis-aligned rectangle with
    /// square corners, a circle or an ellipse, drawn with no opacity, filter, clip, mask or blend on it or any group
    /// around it. Anything else — a gradient, a pattern, a rotation — is not assumed to hide anything.
    /// </summary>
    private static bool Opaque(SvgVisualElement element)
    {
        if (element is SvgRectangle r && (r.CornerRadiusX.Value > 0 || r.CornerRadiusY.Value > 0)) return false;
        if (element is not (SvgRectangle or SvgCircle or SvgEllipse)) return false;
        if (element.Fill is not SvgColourServer fill || fill == SvgPaintServer.None || fill.Colour.A < 255 || element.FillOpacity < 1) return false;

        var m = PosterTypeAudit.Transform(element);
        if (MathF.Abs(m.SkewX) > 1e-4f || MathF.Abs(m.SkewY) > 1e-4f) return false;

        for (SvgElement? e = element; e is not null and not SvgDocument; e = e.Parent)
        {
            if (e is SvgVisualElement v && (v.Opacity < 1 || v.Filter is not null || v.ClipPath is not null)) return false;
            foreach (var name in new[] { "mask", "mix-blend-mode", "clip-path", "filter" })
            {
                // Svg keeps an attribute it models in one place and one it does not (a mask, a blend) in another.
                foreach (var value in new[] { e.TryGetAttribute(name, out var known) ? known : null, e.CustomAttributes.TryGetValue(name, out var custom) ? custom : null })
                {
                    if (!string.IsNullOrWhiteSpace(value) && value.Trim() is not ("none" or "normal")) return false;
                }
            }
        }
        return true;
    }

    /// <summary>Whether an element draws anything: not hidden, not transparent, and filled or stroked.</summary>
    private static bool Visible(SvgVisualElement element)
    {
        for (SvgElement? e = element; e is not null and not SvgDocument; e = e.Parent)
        {
            if (e is SvgVisualElement v && (v.Opacity <= 0 || v.Display == "none" || v.Visibility == "hidden")) return false;
        }
        var filled = element is not SvgLine && element.Fill != SvgPaintServer.None && element.FillOpacity > 0;
        var stroked = element.Stroke is { } s && s != SvgPaintServer.None && element.StrokeWidth.Value > 0 && element.StrokeOpacity > 0;
        return filled || stroked;
    }

    private static float Area(SKRect r) => r.IsEmpty ? 0 : r.Width * r.Height;
    #endregion
}

/// <summary>A shape the layout audit can place exactly: a box, an ellipse inside its box, or a segment.</summary>
/// <param name="Name">What to call it in a warning: rectangle, circle, ellipse, image, line.</param>
/// <param name="Round">The shape is the ellipse inscribed in <paramref name="Box"/>.</param>
/// <param name="From">With <paramref name="To"/>, a straight segment rather than an area.</param>
/// <param name="Outline">The filled area itself, for a shape that is neither a box nor an ellipse — a pie, a mark — in
/// the same coordinates as <paramref name="Box"/>.</param>
/// <param name="Order">Where it is painted among the marks it is audited with — later is on top — or null when unknown.</param>
/// <param name="Opaque">It certainly hides what is under its area: a solid, fully opaque fill, drawn exactly as placed.
/// A shape painted after another and before a line of type, covering the first where the line meets it, hides that
/// crossing from the audit.</param>
public sealed record LayoutShape(string Name, SKRect Box, bool Round, SKPoint? From = null, SKPoint? To = null,
    SKPath? Outline = null, int? Order = null, bool Opaque = false);
