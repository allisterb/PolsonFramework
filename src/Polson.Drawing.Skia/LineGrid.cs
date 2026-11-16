// SPDX-License-Identifier: MIT
namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SkiaSharp;

/// <summary>
/// A grid of lines at any angle: an alternative grid (Lupton, <i>Graphic Design Thinking</i>, pp. 120–125), found in a
/// picture (<c>Layout.linesFrom</c>), tiled from a tessellation (<c>Layout.tessellation</c>) or given by hand
/// (<c>Layout.lines</c>). A page is organised by its lines, and <c>grid.fit</c> measures whether it was.
/// </summary>
/// <remarks>
/// Lupton's method is observe, replicate, organise: find a structure in an object, abstract its lines, then set the type
/// and the elements by them, and cut, crop and overlap with them as well. A column grid is for efficiency; this is for
/// exploring form. Every line is a segment in the page's units; a line found in a picture runs across the whole frame,
/// as the lines of an abstracted sketch cover the page, and keeps the stretch it was found along as <c>support</c>.
/// <para>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS camelCase spelling onto them,
/// so a script calling <c>grid.fit(...)</c> reaches <c>Fit(...)</c>.
/// </para>
/// </remarks>
public sealed class LineGrid
{
    #region Constructors
    internal LineGrid(IReadOnlyList<Segment> lines, SKRect frame, IReadOnlyList<SKPoint[]>? cells, string source)
    {
        segments = lines;
        this.frame = frame;
        polygons = cells ?? [];
        Source = source;
    }
    #endregion

    #region Properties
    /// <summary>Each line: <c>{ x1, y1, x2, y2, angle, length, strength, support? }</c>, angle in degrees.</summary>
    public Dictionary<string, object>[] Lines => _lines ??= [.. segments.Select(Dict)];

    /// <summary>Where lines cross inside the frame, at most 2,000: <c>{ x, y }</c>.</summary>
    public Dictionary<string, object>[] Intersections => _intersections ??= [.. Crossings().Select(p => Point(p.X, p.Y))];

    /// <summary>A tessellation's tiles as point lists; empty for lines found or given.</summary>
    public object[] Cells => _cells ??= [.. polygons.Select(c => (object)c.Select(p => Point(p.X, p.Y)).ToArray())];

    /// <summary>The rectangle the grid covers.</summary>
    public Dictionary<string, object> Frame => LayoutToolkit.Make(frame.Left, frame.Top, frame.Width, frame.Height);

    /// <summary>Where it came from: <c>'picture'</c>, <c>'tessellation:triangle'</c>, <c>'given'</c>.</summary>
    public string Source { get; }

    /// <summary>A sentence to quote: how many lines, at which angles.</summary>
    public string Summary
    {
        get
        {
            var families = segments.GroupBy(s => (int)Math.Round(Fold(s.Angle) / 15.0) * 15 % 180)
                .OrderByDescending(g => g.Count()).Take(4).Select(g => $"{g.Count()} near {g.Key}°");
            return $"{segments.Count} lines ({Source}): {string.Join(", ", families)}.";
        }
    }
    #endregion

    #region Methods
    /// <summary>
    /// The point a fraction <paramref name="t"/> along line <paramref name="index"/>, with the line's angle:
    /// <c>{ x, y, angle }</c>. Translate there and rotate by <c>angle</c> to set a line of type along it.
    /// </summary>
    public Dictionary<string, object> Along(int index, float t)
    {
        var s = Line(index, "grid.along");
        return new() { ["x"] = s.X1 + (s.X2 - s.X1) * t, ["y"] = s.Y1 + (s.Y2 - s.Y1) * t, ["angle"] = s.Angle };
    }

    /// <summary>The nearest line to a point: <c>{ line, distance, x, y, angle }</c>, the point being its foot on the line.</summary>
    public Dictionary<string, object> Nearest(object point)
    {
        var p = AsPoint(point, "grid.nearest");
        Populated("grid.nearest");
        var (index, foot, distance) = segments.Select((s, i) => (i, Foot(s, p))).Select(x => (x.i, x.Item2, SKPoint.Distance(p, x.Item2)))
            .MinBy(x => x.Item3);
        return new() { ["line"] = index, ["distance"] = distance, ["x"] = foot.X, ["y"] = foot.Y, ["angle"] = segments[index].Angle };
    }

    /// <summary>
    /// A point moved onto the grid: to the nearest line, or with <c>{ to: 'intersection' }</c> to the nearest crossing.
    /// <c>{ x, y, distance }</c>.
    /// </summary>
    public Dictionary<string, object> Snap(object point, object? options = null)
    {
        var p = AsPoint(point, "grid.snap");
        var to = JsInterop.AsDict(options)?["to"]?.ToString() ?? "line";
        if (to == "intersection")
        {
            var crossings = Crossings();
            if (crossings.Count == 0) throw new InvalidOperationException("grid.snap: this grid has no crossings; snap to a line instead.");
            var c = crossings.MinBy(q => SKPoint.Distance(p, q));
            return new() { ["x"] = c.X, ["y"] = c.Y, ["distance"] = SKPoint.Distance(p, c) };
        }
        if (to != "line") throw new ArgumentException($"grid.snap's to is 'line' or 'intersection', not '{to}'.");
        var n = Nearest(point);
        return new() { ["x"] = n["x"], ["y"] = n["y"], ["distance"] = n["distance"] };
    }

    /// <summary>
    /// How much of a piece the grid organised: <c>{ share, on, total, items, summary }</c>. Each item is a rectangle
    /// (<c>{ x, y, width, height }</c>, as <c>getBBox()</c> returns), a segment (<c>{ x1, y1, x2, y2 }</c>, a line of type's
    /// baseline) or a point. A segment is on the grid when a line runs along it; a rectangle when one of its edges does,
    /// or its centre sits on a crossing; a point when it lies on a line. <paramref name="options"/>:
    /// <c>{ tolerance? }</c> in the page's units, default 1% of the frame's shorter side; <c>{ degrees? }</c>, 3.
    /// </summary>
    /// <remarks>
    /// The check behind "the grid organised it": a grid drawn over a page laid out by columns decorates it, and
    /// Lupton's third step is that the type and the elements follow the lines.
    /// </remarks>
    public Dictionary<string, object> Fit(object items, object? options = null)
    {
        var o = JsInterop.AsDict(options);
        var tolerance = o?["tolerance"] is { } tol ? Convert.ToSingle(tol, CultureInfo.InvariantCulture) : Math.Max(1f, Math.Min(frame.Width, frame.Height) * 0.01f);
        var degrees = o?["degrees"] is { } deg ? Convert.ToSingle(deg, CultureInfo.InvariantCulture) : 3f;
        var list = items is IEnumerable e and not string && JsInterop.AsDict(items) is null ? e.Cast<object?>().ToArray() : [items];
        if (list.Length == 0) throw new ArgumentException("grid.fit needs the elements to measure: rectangles, baselines or points.");
        Populated("grid.fit");

        var results = list.Select((item, i) =>
        {
            var (on, line, kind) = Fits(item, tolerance, degrees, i);
            return new Dictionary<string, object?> { ["index"] = i, ["kind"] = kind, ["on"] = on, ["line"] = line };
        }).ToArray();
        var count = results.Count(r => (bool)r["on"]!);
        var share = count / (float)results.Length;
        return new()
        {
            ["share"] = share, ["on"] = count, ["total"] = results.Length, ["items"] = results.Cast<object>().ToArray(),
            ["summary"] = $"{count} of {results.Length} elements sit on the grid ({share:P0}), within {tolerance:0.#} units and {degrees:0.#}°."
                + (count < results.Length ? $" Off it: {string.Join(", ", results.Where(r => !(bool)r["on"]!).Take(8).Select(r => r["index"]))}." : ""),
        };
    }

    /// <summary>
    /// The lines as SVG path data — every line, or one with <paramref name="index"/> — for <c>paper.path(d)</c>, a clip, or
    /// <c>comp.outline({ path })</c> written on with <c>trimEnd</c>.
    /// </summary>
    public string PathData(int? index = null)
    {
        var chosen = index is { } i ? [Line(i, "grid.pathData")] : segments;
        var d = new StringBuilder();
        foreach (var s in chosen) d.Append(CultureInfo.InvariantCulture, $"M{s.X1:0.##} {s.Y1:0.##}L{s.X2:0.##} {s.Y2:0.##}");
        return d.ToString();
    }

    /// <summary>Strokes the grid onto a canvas as guides: <c>{ color?, width?, dash?, opacity? }</c>, cyan hairlines by default.</summary>
    public void Draw(CanvasRenderingContext2D ctx, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var o = JsInterop.AsDict(options);
        ctx.Save();
        ctx.StrokeStyle = o?["color"]?.ToString() ?? "#2fb7d6";
        ctx.LineWidth = o?["width"] is { } w ? Convert.ToSingle(w, CultureInfo.InvariantCulture) : 1f;
        ctx.GlobalAlpha = o?["opacity"] is { } a ? Convert.ToSingle(a, CultureInfo.InvariantCulture) : 0.8f;
        if (o?["dash"] is { } dash) ctx.SetLineDash(dash);
        ctx.BeginPath();
        foreach (var s in segments)
        {
            ctx.MoveTo(s.X1, s.Y1);
            ctx.LineTo(s.X2, s.Y2);
        }
        ctx.Stroke();
        ctx.Restore();
    }
    #endregion

    #region Internal methods
    /// <summary>
    /// The dominant straight lines of a picture, mapped onto <paramref name="into"/>: Sobel edges, then a Hough transform
    /// that votes only near each edge's own direction, then the strongest peaks with their neighbours suppressed.
    /// </summary>
    /// <remarks>
    /// The picture is read at most 480 pixels on its long side, which is plenty for a grid and keeps a call under a few
    /// hundred milliseconds. <paramref name="cover"/> scales it uniformly to cover the frame, centred, as a photograph is
    /// cropped onto a page; otherwise it is stretched to fill, and its angles change with the aspect.
    /// </remarks>
    internal static LineGrid Find(SKBitmap picture, SKRect into, int count, float minLength, bool cover)
    {
        var scale = Math.Min(1f, 480f / Math.Max(picture.Width, picture.Height));
        var (w, h) = (Math.Max(8, (int)Math.Round(picture.Width * scale)), Math.Max(8, (int)Math.Round(picture.Height * scale)));
        using var small = picture.Resize(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
            ?? throw new ArgumentException("Layout.linesFrom could not read the picture.");

        var luma = new float[w * h];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var c = small.GetPixel(x, y);
                var a = c.Alpha / 255f;
                // Transparent reads as white, as a cut-out on paper would.
                luma[y * w + x] = (0.2126f * c.Red + 0.7152f * c.Green + 0.0722f * c.Blue) / 255f * a + (1 - a);
            }

        // Sobel: magnitude and the gradient's direction, which is the line's normal.
        var magnitude = new float[w * h];
        var normal = new float[w * h];
        for (var y = 1; y < h - 1; y++)
            for (var x = 1; x < w - 1; x++)
            {
                float L(int dx, int dy) => luma[(y + dy) * w + x + dx];
                var gx = L(1, -1) + 2 * L(1, 0) + L(1, 1) - L(-1, -1) - 2 * L(-1, 0) - L(-1, 1);
                var gy = L(-1, 1) + 2 * L(0, 1) + L(1, 1) - L(-1, -1) - 2 * L(0, -1) - L(1, -1);
                magnitude[y * w + x] = MathF.Sqrt(gx * gx + gy * gy);
                normal[y * w + x] = MathF.Atan2(gy, gx);
            }
        var sorted = magnitude.Where(m => m > 0).OrderBy(m => m).ToArray();
        if (sorted.Length == 0) return new LineGrid([], into, null, "picture");
        var threshold = Math.Max(0.25f, sorted[(int)(sorted.Length * 0.85)]);

        const int Thetas = 180;
        var diagonal = (int)Math.Ceiling(Math.Sqrt(w * w + h * h));
        var rhos = diagonal * 2 + 1;
        var votes = new int[Thetas * rhos];
        var cos = Enumerable.Range(0, Thetas).Select(t => MathF.Cos(t * MathF.PI / Thetas)).ToArray();
        var sin = Enumerable.Range(0, Thetas).Select(t => MathF.Sin(t * MathF.PI / Thetas)).ToArray();
        var edges = new List<(int X, int Y)>();
        for (var y = 1; y < h - 1; y++)
            for (var x = 1; x < w - 1; x++)
            {
                if (magnitude[y * w + x] < threshold) continue;
                edges.Add((x, y));
                // Vote only within 8° of the edge's own normal: cleaner peaks, and a tenth of the work.
                var t0 = (int)Math.Round(((normal[y * w + x] * 180 / Math.PI) % 180 + 180) % 180);
                for (var dt = -8; dt <= 8; dt++)
                {
                    var t = ((t0 + dt) % Thetas + Thetas) % Thetas;
                    var rho = (int)Math.Round(x * cos[t] + y * sin[t]) + diagonal;
                    votes[t * rhos + rho]++;
                }
            }

        // Peaks: the strongest first, each suppressing its neighbours, so a drawn line's two edges count once.
        var minVotes = Math.Max(6, (int)(minLength * Math.Min(w, h)));
        // Wide enough to take both edges of a drawn stroke as one line; two lines closer than this are one line of a grid.
        var suppressRho = Math.Max(8, diagonal / 40);
        var found = new List<(int Theta, float Rho, int Votes)>();
        var order = Enumerable.Range(0, votes.Length).Where(i => votes[i] >= minVotes).OrderByDescending(i => votes[i]).ToArray();
        var taken = new bool[votes.Length];
        foreach (var i in order)
        {
            if (found.Count >= count) break;
            if (taken[i]) continue;
            var (t, r) = (i / rhos, i % rhos);
            // A drawn line has two edges, one either side of it: the peak is the stronger edge, so its place is the
            // vote-weighted middle of what it suppresses at its own angle. Found at the edge, a 5 px stroke landed 12 px
            // off once a photograph was scaled four times onto the page.
            var (sum, weight) = (0.0, 0.0);
            for (var dr = -suppressRho; dr <= suppressRho; dr++)
                if (r + dr >= 0 && r + dr < rhos && !taken[t * rhos + r + dr]) (sum, weight) = (sum + (r + dr) * votes[t * rhos + r + dr], weight + votes[t * rhos + r + dr]);
            found.Add((t, (float)(sum / weight) - diagonal, votes[i]));
            for (var dt = -6; dt <= 6; dt++)
                for (var dr = -suppressRho; dr <= suppressRho; dr++)
                {
                    var tt = t + dt;
                    var rr = r + dr;
                    // Across 0°/180° the same line has the opposite rho.
                    if (tt < 0) { tt += Thetas; rr = 2 * diagonal - rr; }
                    else if (tt >= Thetas) { tt -= Thetas; rr = 2 * diagonal - rr; }
                    if (rr >= 0 && rr < rhos) taken[tt * rhos + rr] = true;
                }
        }

        // Back to the picture's own pixels, then onto the frame.
        var (sx, sy, ox, oy) = cover
            ? Cover(picture.Width, picture.Height, into)
            : (into.Width / picture.Width, into.Height / picture.Height, into.Left, into.Top);
        SKPoint Map(float x, float y) => new(ox + x / scale * sx, oy + y / scale * sy);

        var segments = new List<Segment>();
        foreach (var (t, rho, v) in found)
        {
            var (c, s) = (cos[t], sin[t]);
            // The stretch it was found along: the edge pixels within 1.5 px, 5th to 95th percentile along the line.
            var along = edges.Where(p => Math.Abs(p.X * c + p.Y * s - rho) <= 1.5f).Select(p => -p.X * s + p.Y * c).OrderBy(u => u).ToArray();
            if (along.Length < 2) continue;
            var (u0, u1) = (along[(int)(along.Length * 0.05)], along[(int)(along.Length * 0.95)]);
            SKPoint At(float u) => new(rho * c - u * s, rho * s + u * c);
            var (a, b) = (At(u0), At(u1));
            var support = new Segment(Map(a.X, a.Y), Map(b.X, b.Y), v, null);
            if (Clip(Map(At(-diagonal).X, At(-diagonal).Y), Map(At(diagonal).X, At(diagonal).Y), into) is { } whole)
                segments.Add(new Segment(whole.A, whole.B, v, support));
        }
        var strongest = segments.Count == 0 ? 1 : segments.Max(s => s.Strength);
        return new LineGrid([.. segments.Select(s => s with { Strength = MathF.Round(s.Strength / strongest, 3) })], into, null, "picture");
    }

    /// <summary>
    /// A tessellation's grid over <paramref name="into"/>, tiles of side <paramref name="size"/>: <c>triangle</c>
    /// (three families of lines), <c>diamond</c> (two at 45°), <c>hex</c>, or <c>rhombille</c> (each hexagon cut into
    /// three rhombi, the cube pattern). Lupton, pp. 124–125: one tessellation yields many grids.
    /// </summary>
    internal static LineGrid Tessellate(SKRect into, string type, float size, float rotate)
    {
        if (size <= 0 || size < Math.Min(into.Width, into.Height) / 200) throw new ArgumentException("Layout.tessellation's size is a tile's side, and at least a 200th of the frame.");
        var centre = new SKPoint(into.MidX, into.MidY);
        var reach = (float)Math.Sqrt(into.Width * into.Width + into.Height * into.Height) / 2 + size * 2;
        var rotation = SKMatrix.CreateRotationDegrees(rotate, centre.X, centre.Y);
        var lines = new List<Segment>();
        var cells = new List<SKPoint[]>();

        void Family(float angle, float spacing)
        {
            var (dx, dy) = (MathF.Cos(angle * MathF.PI / 180), MathF.Sin(angle * MathF.PI / 180));
            var (nx, ny) = (-dy, dx);
            for (var k = -(int)(reach / spacing) - 1; k <= (int)(reach / spacing) + 1; k++)
            {
                var o = new SKPoint(centre.X + nx * k * spacing, centre.Y + ny * k * spacing);
                if (Clip(rotation.MapPoint(new SKPoint(o.X - dx * reach, o.Y - dy * reach)), rotation.MapPoint(new SKPoint(o.X + dx * reach, o.Y + dy * reach)), into) is { } s)
                    lines.Add(new Segment(s.A, s.B, 1, null));
            }
        }

        void Polygon(SKPoint[] points)
        {
            var mapped = points.Select(rotation.MapPoint).ToArray();
            var mid = new SKPoint(mapped.Average(p => p.X), mapped.Average(p => p.Y));
            if (!into.Contains(mid)) return;
            cells.Add(mapped);
            for (var i = 0; i < mapped.Length; i++)
                if (Clip(mapped[i], mapped[(i + 1) % mapped.Length], into) is { } s) lines.Add(new Segment(s.A, s.B, 1, null));
        }

        switch (type)
        {
            case "triangle":
                foreach (var a in new[] { 0f, 60f, 120f }) Family(a, size * MathF.Sqrt(3) / 2);
                var rowH = size * MathF.Sqrt(3) / 2;
                for (var row = -(int)(reach / rowH) - 1; row <= (int)(reach / rowH) + 1; row++)
                    for (var col = -(int)(reach / size) - 2; col <= (int)(reach / size) + 2; col++)
                    {
                        var x = centre.X + col * size + (row % 2 != 0 ? size / 2 : 0);
                        var y = centre.Y + row * rowH;
                        var up = new[] { new SKPoint(x, y), new SKPoint(x + size, y), new SKPoint(x + size / 2, y - rowH) };
                        var down = new[] { new SKPoint(x, y), new SKPoint(x + size, y), new SKPoint(x + size / 2, y + rowH) };
                        foreach (var tri in new[] { up, down })
                        {
                            var mapped = tri.Select(rotation.MapPoint).ToArray();
                            if (into.Contains(new SKPoint(mapped.Average(p => p.X), mapped.Average(p => p.Y)))) cells.Add(mapped);
                        }
                    }
                break;
            case "diamond":
                // A square of side `size` turned 45°: its two families of sides are `size` apart.
                foreach (var a in new[] { 45f, 135f }) Family(a, size);
                break;
            case "hex" or "rhombille":
                // Pointy-top hexagons of side `size`: centres a √3·size apart in a row, rows 1.5·size apart.
                var (hw, rh) = (size * MathF.Sqrt(3), size * 1.5f);
                for (var row = -(int)(reach / rh) - 1; row <= (int)(reach / rh) + 1; row++)
                    for (var col = -(int)(reach / hw) - 1; col <= (int)(reach / hw) + 1; col++)
                    {
                        var c = new SKPoint(centre.X + col * hw + (row % 2 != 0 ? hw / 2 : 0), centre.Y + row * rh);
                        var corners = Enumerable.Range(0, 6).Select(k => new SKPoint(c.X + size * MathF.Cos((60 * k - 90) * MathF.PI / 180),
                            c.Y + size * MathF.Sin((60 * k - 90) * MathF.PI / 180))).ToArray();
                        if (type == "hex") Polygon(corners);
                        else
                            for (var k = 0; k < 6; k += 2)
                                Polygon([c, corners[k], corners[(k + 1) % 6], corners[(k + 2) % 6]]);
                    }
                break;
            default:
                throw new ArgumentException($"Layout.tessellation's type is 'triangle', 'diamond', 'hex' or 'rhombille', not '{type}'.");
        }
        return new LineGrid(Dedupe(lines), into, cells, $"tessellation:{type}");
    }

    /// <summary>Lines given by hand: <c>[{ x1, y1, x2, y2 }]</c> or <c>[[x1, y1], [x2, y2]]</c> pairs.</summary>
    internal static LineGrid Given(SKRect into, object lines)
    {
        if (lines is not IEnumerable list || lines is string) throw new ArgumentException("Layout.lines takes a list of lines: [{ x1, y1, x2, y2 }] or [[x1, y1], [x2, y2]].");
        var segments = new List<Segment>();
        foreach (var item in list)
        {
            if (JsInterop.AsDict(item) is { } d && d.Contains("x1"))
                segments.Add(new Segment(new SKPoint(F(d["x1"]), F(d["y1"])), new SKPoint(F(d["x2"]), F(d["y2"])), 1, null));
            else if (item is IEnumerable pair && item is not string && pair.Cast<object?>().ToArray() is [var p, var q])
                segments.Add(new Segment(AsPoint(p!, "Layout.lines"), AsPoint(q!, "Layout.lines"), 1, null));
            else throw new ArgumentException("Each of Layout.lines' lines is { x1, y1, x2, y2 } or a pair of points.");
        }
        if (segments.Any(s => SKPoint.Distance(s.A, s.B) < 1e-3f)) throw new ArgumentException("Layout.lines: a line needs two different ends.");
        return new LineGrid(segments, into, null, "given");
    }
    #endregion

    #region Child types
    /// <summary>A line segment, its strength (a picture's votes, scaled to the strongest), and for a found line the stretch it was found along.</summary>
    internal sealed record Segment(SKPoint A, SKPoint B, float Strength, Segment? Support)
    {
        public float X1 => A.X;
        public float Y1 => A.Y;
        public float X2 => B.X;
        public float Y2 => B.Y;
        public float Angle => MathF.Round(MathF.Atan2(B.Y - A.Y, B.X - A.X) * 180 / MathF.PI, 2);
        public float Length => SKPoint.Distance(A, B);
    }
    #endregion

    #region Private methods
    /// <summary>A grid with no lines measures nothing; a picture with no straight edges gives one.</summary>
    private void Populated(string who)
    {
        if (segments.Count == 0)
            throw new InvalidOperationException($"{who}: the grid has no lines. A picture with no straight edges finds none: try a lower minLength, or a clearer picture of the object.");
    }

    private Segment Line(int index, string who) => index >= 0 && index < segments.Count ? segments[index]
        : throw new ArgumentException($"{who}: there is no line {index}; the grid has {segments.Count}.");

    private static Dictionary<string, object> Dict(Segment s)
    {
        var d = new Dictionary<string, object>
        {
            ["x1"] = s.X1, ["y1"] = s.Y1, ["x2"] = s.X2, ["y2"] = s.Y2, ["angle"] = s.Angle, ["length"] = s.Length, ["strength"] = s.Strength,
        };
        if (s.Support is { } u) d["support"] = new Dictionary<string, object> { ["x1"] = u.X1, ["y1"] = u.Y1, ["x2"] = u.X2, ["y2"] = u.Y2 };
        return d;
    }

    private static Dictionary<string, object> Point(float x, float y) => new() { ["x"] = x, ["y"] = y };

    private List<SKPoint> Crossings()
    {
        if (crossings is not null) return crossings;
        var found = new List<SKPoint>();
        var seen = new HashSet<(int, int)>();
        for (var i = 0; i < segments.Count && found.Count < 2000; i++)
            for (var j = i + 1; j < segments.Count && found.Count < 2000; j++)
                if (Intersect(segments[i], segments[j]) is { } p && frame.Contains(p) && seen.Add(((int)MathF.Round(p.X * 2), (int)MathF.Round(p.Y * 2))))
                    found.Add(p);
        return crossings = found;
    }

    /// <summary>Is an item on the grid, which line, and what it was read as.</summary>
    private (bool On, int? Line, string Kind) Fits(object? item, float tolerance, float degrees, int i)
    {
        var d = JsInterop.AsDict(item) ?? throw new ArgumentException($"grid.fit: element {i} is not a rectangle, a segment or a point.");
        if (d.Contains("x1"))
        {
            var seg = new Segment(new SKPoint(F(d["x1"]), F(d["y1"])), new SKPoint(F(d["x2"]), F(d["y2"])), 1, null);
            var line = Along(seg, tolerance, degrees);
            return (line is not null, line, "segment");
        }
        if (d.Contains("width") && d.Contains("height"))
        {
            var (x, y, w, h) = (F(d["x"]), F(d["y"]), F(d["width"]), F(d["height"]));
            SKPoint[] c = [new(x, y), new(x + w, y), new(x + w, y + h), new(x, y + h)];
            for (var k = 0; k < 4; k++)
                if (Along(new Segment(c[k], c[(k + 1) % 4], 1, null), tolerance, degrees) is { } line) return (true, line, "rectangle");
            var centre = new SKPoint(x + w / 2, y + h / 2);
            var onCrossing = Crossings().Any(p => SKPoint.Distance(p, centre) <= tolerance);
            return (onCrossing, null, "rectangle");
        }
        if (d.Contains("x") && d.Contains("y"))
        {
            var p = new SKPoint(F(d["x"]), F(d["y"]));
            var best = segments.Select((s, k) => (k, SKPoint.Distance(p, Foot(s, p)))).MinBy(x => x.Item2);
            return (best.Item2 <= tolerance, best.Item2 <= tolerance ? best.k : null, "point");
        }
        throw new ArgumentException($"grid.fit: element {i} has none of x1/y1/x2/y2, x/y/width/height or x/y.");
    }

    /// <summary>The grid line that runs along a segment: within the angle, both ends near it, and its middle within the line's extent.</summary>
    private int? Along(Segment item, float tolerance, float degrees)
    {
        if (item.Length < 1e-3f) return null;
        for (var k = 0; k < segments.Count; k++)
        {
            var s = segments[k];
            var diff = Math.Abs(Fold(item.Angle) - Fold(s.Angle));
            if (Math.Min(diff, 180 - diff) > degrees) continue;
            if (LineDistance(s, item.A) > tolerance || LineDistance(s, item.B) > tolerance) continue;
            var mid = new SKPoint((item.X1 + item.X2) / 2, (item.Y1 + item.Y2) / 2);
            if (SKPoint.Distance(mid, Foot(s, mid)) <= tolerance) return k;
        }
        return null;
    }

    private static float Fold(float angle) => ((angle % 180) + 180) % 180;

    private static float LineDistance(Segment s, SKPoint p)
    {
        var (dx, dy) = (s.X2 - s.X1, s.Y2 - s.Y1);
        return Math.Abs(dy * (p.X - s.X1) - dx * (p.Y - s.Y1)) / MathF.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>The nearest point on the segment.</summary>
    private static SKPoint Foot(Segment s, SKPoint p)
    {
        var (dx, dy) = (s.X2 - s.X1, s.Y2 - s.Y1);
        var t = Math.Clamp(((p.X - s.X1) * dx + (p.Y - s.Y1) * dy) / (dx * dx + dy * dy), 0, 1);
        return new SKPoint(s.X1 + dx * t, s.Y1 + dy * t);
    }

    private static SKPoint? Intersect(Segment a, Segment b)
    {
        var (r, s) = (a.B - a.A, b.B - b.A);
        var denominator = r.X * s.Y - r.Y * s.X;
        if (Math.Abs(denominator) < 1e-6f) return null;
        var q = b.A - a.A;
        var t = (q.X * s.Y - q.Y * s.X) / denominator;
        var u = (q.X * r.Y - q.Y * r.X) / denominator;
        return t is >= -1e-4f and <= 1.0001f && u is >= -1e-4f and <= 1.0001f ? new SKPoint(a.X1 + r.X * t, a.Y1 + r.Y * t) : null;
    }

    /// <summary>A segment clipped to a rectangle (Liang–Barsky); null when it misses.</summary>
    private static (SKPoint A, SKPoint B)? Clip(SKPoint a, SKPoint b, SKRect r)
    {
        var (dx, dy) = (b.X - a.X, b.Y - a.Y);
        float t0 = 0, t1 = 1;
        foreach (var (p, q) in new[] { (-dx, a.X - r.Left), (dx, r.Right - a.X), (-dy, a.Y - r.Top), (dy, r.Bottom - a.Y) })
        {
            if (Math.Abs(p) < 1e-9f) { if (q < 0) return null; continue; }
            var t = q / p;
            if (p < 0) { if (t > t1) return null; t0 = Math.Max(t0, t); }
            else { if (t < t0) return null; t1 = Math.Min(t1, t); }
        }
        var (start, end) = (new SKPoint(a.X + dx * t0, a.Y + dy * t0), new SKPoint(a.X + dx * t1, a.Y + dy * t1));
        return SKPoint.Distance(start, end) < 1e-3f ? null : (start, end);
    }

    /// <summary>Shared edges of neighbouring tiles, once.</summary>
    private static List<Segment> Dedupe(List<Segment> lines)
    {
        var seen = new HashSet<(int, int, int, int)>();
        (int, int, int, int) Key(SKPoint p, SKPoint q)
        {
            var (a, b) = p.X < q.X || p.X == q.X && p.Y <= q.Y ? (p, q) : (q, p);
            return ((int)MathF.Round(a.X * 4), (int)MathF.Round(a.Y * 4), (int)MathF.Round(b.X * 4), (int)MathF.Round(b.Y * 4));
        }
        return [.. lines.Where(s => seen.Add(Key(s.A, s.B)))];
    }

    private static (float Sx, float Sy, float Ox, float Oy) Cover(int width, int height, SKRect into)
    {
        var k = Math.Max(into.Width / width, into.Height / height);
        return (k, k, into.Left + (into.Width - width * k) / 2, into.Top + (into.Height - height * k) / 2);
    }

    private static SKPoint AsPoint(object point, string who)
    {
        if (JsInterop.AsDict(point) is { } d && d.Contains("x") && d.Contains("y")) return new SKPoint(F(d["x"]), F(d["y"]));
        if (point is IEnumerable e && point is not string && e.Cast<object?>().ToArray() is [var x, var y]) return new SKPoint(F(x), F(y));
        throw new ArgumentException($"{who} takes a point: {{ x, y }} or [x, y].");
    }

    private static float F(object? value) => Convert.ToSingle(value, CultureInfo.InvariantCulture);
    #endregion

    #region Fields
    private readonly IReadOnlyList<Segment> segments;
    private readonly SKRect frame;
    private readonly IReadOnlyList<SKPoint[]> polygons;
    private List<SKPoint>? crossings;
    private Dictionary<string, object>[]? _lines;
    private Dictionary<string, object>[]? _intersections;
    private object[]? _cells;
    #endregion
}
