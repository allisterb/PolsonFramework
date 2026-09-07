namespace Polson.Drawing.Svg;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

using SkiaSharp;

/// <summary>
/// A brush template, and the deformation that bends it along an arbitrary path.
/// </summary>
/// <remarks>
/// <para>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>brush.deform(d)</c> reaches
/// <see cref="Deform"/>.
/// </para>
/// <para>
/// <b>This is the vector answer to <c>Skia.PathEffect.stamp(..., 'morph')</c>, and it is the one
/// capability Manual 14 §9 still lists as absent.</b> A brush here is a closed outline drawn along a
/// straight <i>backbone</i> running from <c>(0, 0)</c> to <c>(100, 0)</c>: each outline point's
/// <c>x</c> is <b>where along the stroke it sits</b> and its <c>y</c> is <b>how far off the centre
/// line</b>. Bending it onto a target path is then one step per point — sample the target at that
/// parameter, and offset perpendicular by <c>y</c>.
/// </para>
/// <para>
/// The output is a filled <c>d</c> string, so unlike every raster brush this survives into
/// <c>outSvg</c> as real geometry, scales without resampling, and can be unioned or cut like any
/// other path. Nothing here needs a canvas context, which is why it can live on the vector side at
/// all.
/// </para>
/// <para>
/// <b>Approach adapted from <c>svg-brush</c> (MIT), ledgered 2026-09-06.</b> Three things are done
/// differently and each fixes a real defect rather than being a matter of taste. That library walks
/// a <i>polyline</i>, so its sampling is O(n) per query inside a loop over every brush point and its
/// tangent is piecewise-constant — the normal jumps at every vertex, which it then hides behind a
/// Ramer–Douglas–Peucker pass and a quadratic smoother that does not interpolate its own vertices.
/// Here <see cref="SKPathMeasure"/> answers position <i>and</i> tangent exactly on the real curve, so
/// there is nothing to hide and no approximation to make twice. Its brush data is also excluded: the
/// fifteen bundled outlines are traced from Figma Draw and are not the author's to license, so every
/// preset below is generated from a formula instead.
/// </para>
/// </remarks>
public class SnapBrush
{
    #region Constructors
    private SnapBrush(string name, List<BrushContour> contours)
    {
        Name = name;
        _contours = contours;
    }
    #endregion

    #region Properties
    /// <summary>What this brush imitates, e.g. <c>"taper"</c>.</summary>
    public string Name { get; set; }

    /// <summary>How many separate outlines the brush is made of — more than one is a split nib.</summary>
    public int ContourCount => _contours.Count;

    /// <summary>Total outline points held. The deformation adds more where a stroke needs them.</summary>
    public int PointCount => _contours.Sum(c => c.Points.Count);

    /// <summary>
    /// The brush's widest half-width, in pixels at <c>thickness</c> 1 — so the mark it draws is
    /// about <c>2 × halfWidth</c> across at its fattest.
    /// </summary>
    public float HalfWidth => _contours.Count == 0
        ? 0f
        : _contours.Max(c => c.Points.Count == 0 ? 0f : c.Points.Max(p => MathF.Abs(p.Y)));
    #endregion

    #region Methods — templates from a path
    /// <summary>
    /// A brush from an outline you drew yourself, as an SVG <c>d</c> string.
    /// </summary>
    /// <remarks>
    /// <b>The outline is read as a brush, not as a picture</b>: its horizontal extent is mapped onto
    /// the whole stroke whatever it measures, and its vertical extent is taken as pixels either side
    /// of the centre line. So the aspect ratio is deliberately <i>not</i> preserved — a longer stroke
    /// is not a fatter one, which is what makes it behave like a brush.
    /// <para>
    /// Sub-paths are kept separate, so a split or multi-ribbon nib works. A path with no length
    /// yields a brush that draws nothing rather than throwing, since an empty template is a
    /// legitimate result of a caller's own geometry collapsing.
    /// </para>
    /// </remarks>
    internal static SnapBrush FromPath(string templatePathData, string name = "brush", float sampleStep = 0.75f)
    {
        using var path = SnapPathMeasurement.ParseSvgPath(templatePathData);
        var flattened = Flatten(path, MathF.Max(0.05f, sampleStep));
        return new SnapBrush(name, Normalise(flattened));
    }

    /// <summary>A brush from an element's own geometry, so a drawn shape can become a nib.</summary>
    internal static SnapBrush FromElement(SnapElement element, string name = "brush", float sampleStep = 0.75f)
    {
        ArgumentNullException.ThrowIfNull(element);
        using var path = element.ToSkPath() ?? new SKPath();
        return new SnapBrush(name, Normalise(Flatten(path, MathF.Max(0.05f, sampleStep))));
    }
    #endregion

    #region Methods — generated presets
    /// <summary>
    /// A leaf: nothing at both ends, fullest in the middle. The default nib, and the one that reads
    /// as a confident single stroke.
    /// </summary>
    /// <remarks><paramref name="fullness"/> below 1 holds the width out toward the ends; above 1
    /// pinches it toward the centre. 1 is a plain sine.</remarks>
    internal static SnapBrush Taper(float width = 14f, float fullness = 1f, int steps = 64) =>
        Symmetric("taper", width, steps, t => MathF.Pow(MathF.Sin(MathF.PI * t), MathF.Max(0.05f, fullness)));

    /// <summary>Full at the start, tapering to a point — a stroke that lands and lifts.</summary>
    internal static SnapBrush Wedge(float width = 14f, float fullness = 1f, int steps = 64) =>
        Symmetric("wedge", width, steps, t => MathF.Pow(1f - t, MathF.Max(0.05f, fullness)));

    /// <summary>
    /// A flat nib held at an angle: constant width, ends cut on the skew, so the mark thins as the
    /// stroke turns toward the nib's own axis.
    /// </summary>
    internal static SnapBrush Chisel(float width = 12f, float skew = 18f)
    {
        var half = MathF.Max(0.01f, width) / 2f;
        var cut = Math.Clamp(skew, 0f, 45f);
        var points = new List<SKPoint>
        {
            new(cut, -half), new(BackboneLength, -half),
            new(BackboneLength - cut, half), new(0f, half),
        };
        return new SnapBrush("chisel", [new BrushContour(points, true)]);
    }

    /// <summary>
    /// A dry, split nib: several thin ribbons that start and stop at different points along the
    /// stroke. Separate contours, so the gaps between them are real holes rather than paint.
    /// </summary>
    internal static SnapBrush Split(int ribbons = 4, float width = 16f, float fullness = 1.3f, int steps = 40)
    {
        var count = Math.Clamp(ribbons, 1, 24);
        var half = MathF.Max(0.01f, width) / 2f;
        var contours = new List<BrushContour>();

        for (var r = 0; r < count; r++)
        {
            // Spread the ribbons across the nib's width, and stagger their ends so the stroke frays
            // rather than starting as one blunt block.
            var centre = count == 1 ? 0f : -half + 2f * half * r / (count - 1);
            var thickness = half * 0.16f * (0.6f + 0.8f * MathF.Abs(MathF.Cos(r * 2.399f)));
            var from = 4f + 10f * MathF.Abs(MathF.Sin(r * 1.7f));
            var to = BackboneLength - 4f - 12f * MathF.Abs(MathF.Sin(r * 2.3f + 1f));
            if (to <= from) continue;

            var upper = new List<SKPoint>();
            var lower = new List<SKPoint>();
            for (var i = 0; i <= steps; i++)
            {
                var t = (float)i / steps;
                var x = from + (to - from) * t;
                var w = thickness * MathF.Pow(MathF.Sin(MathF.PI * t), fullness);
                upper.Add(new SKPoint(x, centre - w));
                lower.Add(new SKPoint(x, centre + w));
            }

            lower.Reverse();
            upper.AddRange(lower);
            contours.Add(new BrushContour(upper, true));
        }

        return new SnapBrush("split", contours);
    }

    /// <summary>Every preset name, so a script can offer them without hard-coding the list.</summary>
    internal static string[] PresetNames => ["taper", "wedge", "chisel", "split"];

    /// <summary>Whether <paramref name="name"/> is a preset — asked before a string is read as path data.</summary>
    internal static bool HasPreset(string? name) =>
        PresetNames.Contains((name ?? string.Empty).Trim().ToLowerInvariant());

    /// <summary>A preset by name, for <c>Snap.brush('taper')</c>.</summary>
    /// <remarks>An unknown name throws and lists the real ones, rather than quietly handing back a
    /// default nib that the caller would then ship.</remarks>
    internal static SnapBrush Preset(string name) => (name ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "taper" or "" => Taper(),
        "wedge" => Wedge(),
        "chisel" => Chisel(),
        "split" => Split(),
        _ => throw new ArgumentException(
            $"'{name}' is not a brush preset. Available: {string.Join(", ", PresetNames)}. " +
            "For your own nib use Snap.brush(dString) or SnapBrush.fromElement(element).", nameof(name)),
    };
    #endregion

    #region Methods
    /// <summary>
    /// Bends this brush along <paramref name="target"/> and returns the mark as an SVG <c>d</c>
    /// string — a string, not an element, so the caller decides where it goes.
    /// </summary>
    /// <param name="target">An SVG <c>d</c> string, or a <see cref="SnapElement"/> whose geometry to follow.</param>
    /// <param name="thickness">Multiplies the nib's width. 1 is the template's own.</param>
    /// <param name="segmentLength">
    /// Roughly how many pixels of target each emitted segment spans. Smaller is smoother and longer.
    /// </param>
    /// <remarks>
    /// <b>Each contour of the target gets its own stroke.</b> Flattening several sub-paths into one
    /// point list — which is what the source library does — draws a bridging stroke through the gap
    /// between them, and that stroke is not in anyone's drawing.
    /// <para>
    /// The template is subdivided against the <i>target's</i> length rather than sampled at a fixed
    /// resolution, because a template edge spanning a long stretch of a long path would otherwise
    /// emit one straight segment across a curve. That is the case the source library handles with an
    /// opt-in <c>brushAugmentation</c> flag, two binary searches and a documented performance
    /// penalty; here it falls out of choosing the step from the geometry, and is always on.
    /// </para>
    /// </remarks>
    public string Deform(object? target, float thickness = 1f, float segmentLength = 2f, float tolerance = 0.08f)
    {
        using var path = ResolveTarget(target);
        if (path.IsEmpty || _contours.Count == 0) return string.Empty;

        var builder = new StringBuilder();
        using var measure = new SKPathMeasure(path, false);

        do
        {
            var length = measure.Length;
            if (length <= 0f) continue;

            // Backbone units per emitted segment: the whole 100-unit backbone spans `length` pixels.
            var span = Math.Clamp(MathF.Max(0.05f, segmentLength) * BackboneLength / length, 0.02f, 25f);

            foreach (var contour in _contours)
            {
                AppendContour(builder, contour, measure, length, thickness, span, MathF.Max(0f, tolerance));
            }
        } while (measure.NextContour());

        return builder.ToString().TrimStart();
    }
    #endregion

    #region Private methods
    /// <summary>One template contour, bent along one contour of the target.</summary>
    private static void AppendContour(StringBuilder builder, BrushContour contour, SKPathMeasure measure,
        float length, float thickness, float span, float tolerance)
    {
        if (contour.Points.Count < 2) return;

        var mapped = new List<SKPoint>(contour.Points.Count * 2)
        {
            Map(contour.Points[0], measure, length, thickness),
        };

        for (var i = 1; i < contour.Points.Count; i++)
        {
            var a = contour.Points[i - 1];
            var b = contour.Points[i];

            // Subdivide against the target, so a template edge crossing a long arc still curves.
            var steps = Math.Clamp((int)MathF.Ceiling(MathF.Abs(b.X - a.X) / span), 1, 512);
            for (var s = 1; s <= steps; s++)
            {
                var u = (float)s / steps;
                mapped.Add(Map(new SKPoint(a.X + (b.X - a.X) * u, a.Y + (b.Y - a.Y) * u),
                    measure, length, thickness));
            }
        }

        var points = tolerance > 0f ? Simplify(mapped, tolerance) : mapped;
        if (points.Count < 2) return;

        builder.Append(" M ").Append(Format(points[0]));
        for (var i = 1; i < points.Count; i++) builder.Append(" L ").Append(Format(points[i]));
        if (contour.Closed) builder.Append(" Z");
    }

    /// <summary>
    /// Ramer–Douglas–Peucker: drops points no further than <paramref name="tolerance"/> from the line
    /// they sit on.
    /// </summary>
    /// <remarks>
    /// <b>Purely a size measure, and that is the difference from the source library's use of it.</b>
    /// There it runs at tolerance 0.3 to <i>hide</i> a tangent that jumps at every polyline vertex;
    /// here the tangent is exact, so this runs at a fraction of a pixel and removes only points that
    /// carry no shape. Measured on the preset sheet: a split mark fell from 17.4 KB of path data to
    /// under 3 KB with no visible change.
    /// <para>
    /// Iterative rather than recursive — a contour can reach a few thousand points, and the sandbox
    /// caps recursion at 100 frames for scripts, so a deep native stack here would be an odd thing to
    /// rely on.
    /// </para>
    /// </remarks>
    private static List<SKPoint> Simplify(List<SKPoint> points, float tolerance)
    {
        if (points.Count < 3) return points;

        var keep = new bool[points.Count];
        keep[0] = keep[^1] = true;

        var stack = new Stack<(int From, int To)>();
        stack.Push((0, points.Count - 1));

        while (stack.Count > 0)
        {
            var (from, to) = stack.Pop();
            if (to <= from + 1) continue;

            var worst = 0f;
            var index = from;
            for (var i = from + 1; i < to; i++)
            {
                var d = PerpendicularDistance(points[i], points[from], points[to]);
                if (d > worst) { worst = d; index = i; }
            }

            if (worst <= tolerance) continue;

            keep[index] = true;
            stack.Push((from, index));
            stack.Push((index, to));
        }

        var kept = new List<SKPoint>(points.Count);
        for (var i = 0; i < points.Count; i++) if (keep[i]) kept.Add(points[i]);
        return kept;
    }

    private static float PerpendicularDistance(SKPoint p, SKPoint a, SKPoint b)
    {
        float dx = b.X - a.X, dy = b.Y - a.Y;
        var lengthSquared = dx * dx + dy * dy;

        // A degenerate segment has no line to be perpendicular to; fall back to the point distance.
        if (lengthSquared < 1e-9f) return MathF.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));

        var t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lengthSquared, 0f, 1f);
        float cx = a.X + t * dx, cy = a.Y + t * dy;
        return MathF.Sqrt((p.X - cx) * (p.X - cx) + (p.Y - cy) * (p.Y - cy));
    }

    /// <summary>One template point, placed. This is the whole idea, in four lines.</summary>
    private static SKPoint Map(SKPoint template, SKPathMeasure measure, float length, float thickness)
    {
        var t = Math.Clamp(template.X / BackboneLength, 0f, 1f);
        if (!measure.GetPositionAndTangent(t * length, out var position, out var tangent))
        {
            return new SKPoint(position.X, position.Y);
        }

        // Skia hands back a unit tangent, so the perpendicular needs no normalising.
        var offset = template.Y * thickness;
        return new SKPoint(position.X - tangent.Y * offset, position.Y + tangent.X * offset);
    }

    /// <summary>A path's contours as point lists, sampled on the real curve rather than its hull.</summary>
    private static List<BrushContour> Flatten(SKPath path, float step)
    {
        var contours = new List<BrushContour>();
        if (path.IsEmpty) return contours;

        using var measure = new SKPathMeasure(path, false);
        do
        {
            var length = measure.Length;
            if (length <= 0f) continue;

            var count = Math.Clamp((int)MathF.Ceiling(length / step), 2, 4096);
            var points = new List<SKPoint>(count + 1);
            for (var i = 0; i <= count; i++)
            {
                if (measure.GetPosition(length * i / count, out var p)) points.Add(p);
            }

            if (points.Count >= 2) contours.Add(new BrushContour(points, measure.IsClosed));
        } while (measure.NextContour());

        return contours;
    }

    /// <summary>
    /// Puts a hand-drawn template into backbone space: <c>x</c> across the full 0–100 backbone,
    /// <c>y</c> as pixels either side of the template's own centre line.
    /// </summary>
    private static List<BrushContour> Normalise(List<BrushContour> contours)
    {
        if (contours.Count == 0) return contours;

        var all = contours.SelectMany(c => c.Points).ToList();
        if (all.Count == 0) return contours;

        float minX = all.Min(p => p.X), maxX = all.Max(p => p.X);
        float minY = all.Min(p => p.Y), maxY = all.Max(p => p.Y);
        var width = maxX - minX;
        var centreY = (minY + maxY) / 2f;

        // A template with no horizontal extent has no backbone to run along; keep it, mapped to the
        // start of the stroke, rather than dividing by zero and emitting NaN into a `d` string.
        var scale = width > 1e-4f ? BackboneLength / width : 0f;

        return contours
            .Select(c => new BrushContour(
                c.Points.Select(p => new SKPoint((p.X - minX) * scale, p.Y - centreY)).ToList(), c.Closed))
            .ToList();
    }

    private static SKPath ResolveTarget(object? target) => target switch
    {
        null => new SKPath(),
        SKPath path => new SKPath(path),
        SnapElement element => element.ToSkPath() ?? new SKPath(),
        string data => SnapPathMeasurement.ParseSvgPath(data),
        _ => SnapPathMeasurement.ParseSvgPath(target.ToString()),
    };

    private static string Format(SKPoint p) =>
        p.X.ToString("0.##", CultureInfo.InvariantCulture) + "," + p.Y.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>A nib symmetric about the centre line, from a half-width profile.</summary>
    private static SnapBrush Symmetric(string name, float width, int steps, Func<float, float> profile)
    {
        var half = MathF.Max(0.01f, width) / 2f;
        var count = Math.Clamp(steps, 4, 512);
        var upper = new List<SKPoint>(count + 1);
        var lower = new List<SKPoint>(count + 1);

        for (var i = 0; i <= count; i++)
        {
            var t = (float)i / count;
            var w = half * profile(t);
            upper.Add(new SKPoint(t * BackboneLength, -w));
            lower.Add(new SKPoint(t * BackboneLength, w));
        }

        lower.Reverse();
        upper.AddRange(lower);
        return new SnapBrush(name, [new BrushContour(upper, true)]);
    }
    #endregion

    #region Fields
    /// <summary>The backbone runs from (0,0) to (100,0); a point's x is its parameter along it.</summary>
    private const float BackboneLength = 100f;

    private readonly List<BrushContour> _contours;
    #endregion

    #region Types
    /// <summary>One outline of the nib. <c>Closed</c> decides whether the mark gets a <c>Z</c>.</summary>
    private sealed record BrushContour(List<SKPoint> Points, bool Closed);
    #endregion
}
