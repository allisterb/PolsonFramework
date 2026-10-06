namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using SkiaSharp;

public partial class ConstructiveDrawingToolkit
{
    #region Gesture Contour
    static readonly string[] GestureDrawOptions = ["padding", "strokeColor", "strokeWidth", "stretchWidth", "squashWidth"];

    /// <summary>Below this bend a limb has no inside, so both of its sides are stretched.</summary>
    const float StraightLimbDeg = 8f;

    /// <summary>
    /// The figure's contour as a gesture drawer lines it: straight on the stretch side, curved on the
    /// squash side — Stanchfield's "a straight line is the symbol for a stretch, a bent line for a
    /// squash" (<i>Drawn to Life</i>, ch. 13, 19, 24).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="CreateFigureGeometry"/> builds each limb as a symmetric capsule, which is the right
    /// mass and the wrong line: it has the same contour on both sides of a bent elbow. Here the inside
    /// of each bend is found from the bisector of the joint — no constant decides it — and the
    /// outside gets two straight lines meeting in an angle at the joint, the inside one curve through
    /// the joint's inner edge.
    /// </para>
    /// <para>
    /// The torso is judged by length, not by angle: each side runs along the common tangent of the ribcage
    /// and the pelvis, and the longer side is the stretch. The squash side folds inward by an amount proportional to how much shorter it is,
    /// so the drawing says as much as the pose does and no more. Sides within 3% of each other are
    /// both drawn straight and <c>stretchSide</c> is null — a standing torso has no stretch to show.
    /// </para>
    /// <para>
    /// Lines only, and open: the paths are strokes, not fills. Hands, feet and the head are left to
    /// their own drawers. Radii are the ones the geometry uses, so the lines sit on the silhouette;
    /// <c>padding</c> moves them out with it, which is how a sleeve is lined.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> CreateGestureContour(object figureObj, object? options = null)
    {
        if (JsInterop.AsDict(figureObj) is not IDictionary fig)
            throw new ArgumentException("createGestureContour needs a figure from Drawing.createMannequinFigure(...).", nameof(figureObj));

        var opt = JsInterop.AsDict(options);
        if (opt != null)
            foreach (var key in opt.Keys)
                if (key?.ToString() is not "padding")
                    throw new ArgumentException($"createGestureContour has no option '{key}'. It takes padding (pixels added to every radius).");

        var H = Num(fig, "headUnit", 70f);
        var pad = Num(opt, "padding", 0f);
        var stretch = new CanvasPath();
        var squash = new CanvasPath();
        var parts = new Dictionary<string, object?>();

        void Limb(string name, string j0, string j1, string j2, float r0, float r1, float r2)
        {
            if (JsInterop.AsDict(fig[name]) is not IDictionary d) return;
            var line = LimbContour(ExtractPoint(d[j0]), ExtractPoint(d[j1]), ExtractPoint(d[j2]),
                r0 * H + pad, r1 * H + pad, r2 * H + pad);
            stretch.AddPath(line.Stretch);
            squash.AddPath(line.Squash);
            parts[name] = new Dictionary<string, object?>
            {
                ["stretch"] = line.Stretch,
                ["squash"] = line.Squash,
                ["bendDeg"] = line.BendDeg,
                ["straight"] = line.BendDeg < StraightLimbDeg
            };
        }

        Limb("leftArm", "shoulder", "elbow", "wrist", 0.22f, 0.16f, 0.12f);
        Limb("rightArm", "shoulder", "elbow", "wrist", 0.22f, 0.16f, 0.12f);
        Limb("leftLeg", "hip", "knee", "ankle", 0.28f, 0.20f, 0.14f);
        Limb("rightLeg", "hip", "knee", "ankle", 0.28f, 0.20f, 0.14f);

        var torso = TorsoContour(fig, H, pad);
        stretch.AddPath(torso.Stretch);
        squash.AddPath(torso.Squash);
        parts["torso"] = torso.Record;

        return new Dictionary<string, object?>
        {
            ["stretch"] = stretch,
            ["squash"] = squash,
            ["parts"] = parts,
            ["padding"] = pad
        };
    }

    /// <summary>Strokes <see cref="CreateGestureContour"/>'s lines and returns what it drew.</summary>
    /// <remarks>
    /// <c>stretchWidth</c> and <c>squashWidth</c> default to one <c>strokeWidth</c>: Stanchfield
    /// distinguishes the two sides by the <i>shape</i> of the line, not its weight, so a weight
    /// difference is left to the caller rather than asserted here.
    /// </remarks>
    public Dictionary<string, object?> DrawGestureContour(CanvasRenderingContext2D ctx, object figureObj, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var opt = JsInterop.AsDict(options);
        if (opt != null)
            foreach (var key in opt.Keys)
                if (Array.IndexOf(GestureDrawOptions, key?.ToString()) < 0)
                    throw new ArgumentException($"drawGestureContour has no option '{key}'. It takes {string.Join(", ", GestureDrawOptions)}.");
        Dictionary<string, object?>? geometryOptions = null;
        if (opt != null && opt.Contains("padding")) geometryOptions = new() { ["padding"] = opt["padding"] };
        var contour = CreateGestureContour(figureObj, geometryOptions);

        var color = opt?["strokeColor"]?.ToString() ?? "#1a1a18";
        var width = Num(opt, "strokeWidth", 2f);

        ctx.Save();
        ctx.StrokeStyle = color;
        ctx.LineCap = "round";
        ctx.LineJoin = "round";
        ctx.LineWidth = Num(opt, "stretchWidth", width);
        ctx.Stroke((CanvasPath)contour["stretch"]!);
        ctx.LineWidth = Num(opt, "squashWidth", width);
        ctx.Stroke((CanvasPath)contour["squash"]!);
        ctx.Restore();
        return contour;
    }

    /// <summary>One three-joint limb: two straight lines outside the bend, one curve inside it.</summary>
    static (CanvasPath Stretch, CanvasPath Squash, float BendDeg) LimbContour(
        Point2D a, Point2D b, Point2D c, float ra, float rb, float rc)
    {
        var stretch = new CanvasPath();
        var squash = new CanvasPath();
        float ux = a.X - b.X, uy = a.Y - b.Y, vx = c.X - b.X, vy = c.Y - b.Y;
        float lu = MathF.Sqrt(ux * ux + uy * uy), lv = MathF.Sqrt(vx * vx + vy * vy);
        if (lu < 0.001f || lv < 0.001f) return (stretch, squash, 0f);

        // The interior angle at the joint; the bend is how far that falls short of a straight line.
        var cos = Math.Clamp((ux * vx + uy * vy) / (lu * lv), -1f, 1f);
        var bendDeg = 180f - MathF.Acos(cos) * 180f / MathF.PI;

        // Unit normals of each segment, both turned to point into the bend.
        (float X, float Y) Normal(float dx, float dy, float len) => (-dy / len, dx / len);
        var n1 = Normal(-ux, -uy, lu);
        var n2 = Normal(vx, vy, lv);
        float bx = ux / lu + vx / lv, by = uy / lu + vy / lv;
        var bl = MathF.Sqrt(bx * bx + by * by);

        if (bendDeg < StraightLimbDeg || bl < 0.001f)
        {
            // No inside: both sides are stretched. Each is a straight taper along the segment's normal.
            foreach (var s in new[] { 1f, -1f })
            {
                stretch.MoveTo(a.X + n1.Item1 * ra * s, a.Y + n1.Item2 * ra * s);
                stretch.LineTo(b.X + (n1.Item1 + n2.Item1) * 0.5f * rb * s, b.Y + (n1.Item2 + n2.Item2) * 0.5f * rb * s);
                stretch.LineTo(c.X + n2.Item1 * rc * s, c.Y + n2.Item2 * rc * s);
            }
            return (stretch, squash, bendDeg);
        }

        bx /= bl; by /= bl;
        if (n1.Item1 * bx + n1.Item2 * by < 0f) n1 = (-n1.Item1, -n1.Item2);
        if (n2.Item1 * bx + n2.Item2 * by < 0f) n2 = (-n2.Item1, -n2.Item2);

        // Stretch: outside the bend, straight into an angle at the joint.
        stretch.MoveTo(a.X - n1.Item1 * ra, a.Y - n1.Item2 * ra);
        stretch.LineTo(b.X - bx * rb, b.Y - by * rb);
        stretch.LineTo(c.X - n2.Item1 * rc, c.Y - n2.Item2 * rc);

        // Squash: inside, one curve passing through the joint's inner edge.
        Point2D p0 = new(a.X + n1.Item1 * ra, a.Y + n1.Item2 * ra), p2 = new(c.X + n2.Item1 * rc, c.Y + n2.Item2 * rc);
        Point2D mid = new(b.X + bx * rb, b.Y + by * rb);
        squash.MoveTo(p0.X, p0.Y);
        squash.QuadraticCurveTo(2f * mid.X - (p0.X + p2.X) * 0.5f, 2f * mid.Y - (p0.Y + p2.Y) * 0.5f, p2.X, p2.Y);
        return (stretch, squash, bendDeg);
    }

    /// <summary>The torso's two sides, the longer drawn straight and the shorter folded inward.</summary>
    /// <remarks>
    /// Each side runs along the common tangent of the ribcage and pelvis ellipses, the two masses the
    /// silhouette is built from, so the stretch side lies on the torso's hull whatever the ribcage's tilt.
    /// It used to run from the shoulder tip, out along the clavicle line, to the hip; on a strong C the
    /// clavicle line turns with the ribcage, so one side began in the shoulder knob and the other in the
    /// arm root, and the squash side folded across the chest (sketch1, 2026-10-04). The fold is now also
    /// held to under two thirds of the way from its chord to the torso's axis, so it cannot reach the
    /// other side, which lies beyond the axis.
    /// </remarks>
    static (CanvasPath Stretch, CanvasPath Squash, Dictionary<string, object?> Record) TorsoContour(IDictionary fig, float H, float pad)
    {
        var masses = FigureMasses(fig, H);
        var rib = masses.First(m => m.Name == "ribcage");
        var pel = masses.First(m => m.Name == "pelvis");
        var pelvis = JsInterop.AsDict(fig["pelvis"]);
        var lh = ExtractPoint(pelvis?["leftHip"]);

        // The torso's axis, pelvis to ribcage, and the direction across it.
        float ux = rib.C.X - pel.C.X, uy = rib.C.Y - pel.C.Y, ul = MathF.Sqrt(ux * ux + uy * uy);
        (ux, uy) = ul < 0.001f ? (0f, -1f) : (ux / ul, uy / ul);
        float ax = -uy, ay = ux;
        var leftSign = (lh.X - pel.C.X) * ax + (lh.Y - pel.C.Y) * ay >= 0f ? 1f : -1f;

        // How far an ellipse reaches in direction n (its support function), and the point that reaches it.
        static (float Reach, Point2D At) Support((string Name, string Group, Point2D C, float Rx, float Ry, float Deg) m, float pad, float nx, float ny)
        {
            var t = m.Deg * MathF.PI / 180f;
            float cx = MathF.Cos(t), sy = MathF.Sin(t), rx = m.Rx + pad, ry = m.Ry + pad;
            float along = nx * cx + ny * sy, up = -nx * sy + ny * cx;
            var r = MathF.Sqrt(rx * rx * along * along + ry * ry * up * up);
            if (r < 1e-6f) return (nx * m.C.X + ny * m.C.Y, m.C);
            float px = rx * rx * along / r, py = ry * ry * up / r;
            return (nx * m.C.X + ny * m.C.Y + r, new Point2D(m.C.X + px * cx - py * sy, m.C.Y + px * sy + py * cx));
        }

        // The common tangent on one side: the outward normal tilted toward the ribcage until both masses reach it equally.
        (Point2D Top, Point2D Bottom) Tangent(float sign)
        {
            float bx = ax * sign, by = ay * sign;
            (float, float) N(float phi) => (MathF.Cos(phi) * bx + MathF.Sin(phi) * ux, MathF.Cos(phi) * by + MathF.Sin(phi) * uy);
            float F(float phi)
            {
                var (nx, ny) = N(phi);
                return Support(rib, pad, nx, ny).Reach - Support(pel, pad, nx, ny).Reach;
            }

            float lo = -1.3f, hi = 1.3f, phi = 0f;
            if (F(lo) < 0f && F(hi) > 0f)
            {
                for (var i = 0; i < 40; i++)
                {
                    phi = (lo + hi) * 0.5f;
                    if (F(phi) < 0f) lo = phi; else hi = phi;
                }
            }

            var (x, y) = N(phi);
            return (Support(rib, pad, x, y).At, Support(pel, pad, x, y).At);
        }

        var (lTop, lBottom) = Tangent(leftSign);
        var (rTop, rBottom) = Tangent(-leftSign);

        var leftLen = SegmentLength(lTop, lBottom);
        var rightLen = SegmentLength(rTop, rBottom);
        var longer = MathF.Max(leftLen, rightLen);
        var shortfall = longer > 0f ? MathF.Abs(leftLen - rightLen) / longer : 0f;
        string? stretchSide = shortfall < 0.03f ? null : rightLen > leftLen ? "right" : "left";

        var stretch = new CanvasPath();
        var squash = new CanvasPath();

        void Side(Point2D top, Point2D bottom, bool folded)
        {
            if (!folded)
            {
                stretch.MoveTo(top.X, top.Y);
                stretch.LineTo(bottom.X, bottom.Y);
                return;
            }

            // Folded toward the axis, as deep as the side is short; capped, so an extreme bend reads as a
            // fold rather than as the waist collapsing through the other side.
            Point2D mid = new((top.X + bottom.X) * 0.5f, (top.Y + bottom.Y) * 0.5f);
            var along = (mid.X - pel.C.X) * ux + (mid.Y - pel.C.Y) * uy;
            Point2D foot = new(pel.C.X + ux * along, pel.C.Y + uy * along);
            float dx = foot.X - mid.X, dy = foot.Y - mid.Y, l = MathF.Sqrt(dx * dx + dy * dy);
            var depth = l < 0.001f ? 0f : MathF.Min(MathF.Min(H * 0.35f, H * shortfall), l * 0.6f);
            Point2D fold = l < 0.001f ? mid : new(mid.X + dx / l * depth, mid.Y + dy / l * depth);
            squash.MoveTo(top.X, top.Y);
            squash.QuadraticCurveTo(2f * fold.X - mid.X, 2f * fold.Y - mid.Y, bottom.X, bottom.Y);
        }

        Side(lTop, lBottom, stretchSide == "right");
        Side(rTop, rBottom, stretchSide == "left");

        return (stretch, squash, new Dictionary<string, object?>
        {
            ["stretch"] = stretch,
            ["squash"] = squash,
            ["stretchSide"] = stretchSide,
            ["leftLength"] = leftLen,
            ["rightLength"] = rightLen,
            ["shortfall"] = shortfall
        });
    }
    #endregion

    #region Overlap Contour
    static readonly string[] OverlapOptions = ["order", "step"];
    static readonly string[] OverlapDrawOptions = ["order", "step", "strokeColor", "outerWidth", "innerWidth"];

    /// <summary>A figure's groups nearest first when nothing says otherwise: a standing figure seen from the front.</summary>
    static readonly string[] FigureDepth = ["head", "leftArm", "rightArm", "torso", "leftLeg", "rightLeg"];

    /// <summary>
    /// Overlapping shapes lined in depth: each outline is hidden wherever a nearer shape covers it, so a far
    /// contour stops at the near one in a T — Hamm's "principle of the T" (<i>Drawing the Head and Figure</i>,
    /// p. 48): the stem of the T reads as going behind the crossbar, and that junction is what states depth on
    /// a line drawing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>shapes</c> takes what <c>findTangents</c> takes — named closed shapes, an array of them, a
    /// <c>createFigureGeometry</c> result (its groups are used), or several of those in one object — and also a
    /// figure from <c>createMannequinFigure</c>, whose geometry is built here. Lines are refused: a line has no
    /// inside to hide anything behind.
    /// </para>
    /// <para>
    /// Depth is the caller's, because the toolkit has no z. Shapes are nearest first in the order given;
    /// <c>order</c> names some or all of them, nearest first, and a name covers every shape under it
    /// (<c>'him'</c> covers <c>him.leftArm</c>). A figure's groups default to head, arms, torso, legs: a
    /// standing figure seen from the front.
    /// </para>
    /// <para>
    /// The visible lines come back split in two: <c>outer</c>, which lies on the silhouette of the whole
    /// arrangement, and <c>inner</c>, the overlap lines inside it — Janson's heavy contour and lighter interior
    /// (Manual 03). Each T-junction is reported with the near and far shape it joins. Runs are found by sampling
    /// every <c>step</c> pixels and their ends refined by bisection, then cut from the outline itself, so the
    /// lines are the exact curves, not polylines.
    /// </para>
    /// <para>
    /// Groups are united, so a limb has no line across its own joints. Passing a geometry's <c>parts</c> lines
    /// every mass on its own, which also draws a seam across every joint.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> CreateOverlapContour(object shapesObj, object? options = null)
    {
        var opt = JsInterop.AsDict(options);
        if (opt != null)
            foreach (var key in opt.Keys)
                if (Array.IndexOf(OverlapOptions, key?.ToString()) < 0)
                    throw new ArgumentException($"createOverlapContour has no option '{key}'. It takes {string.Join(", ", OverlapOptions)}.");
        return OverlapContour(shapesObj, opt);
    }

    /// <summary>Strokes <see cref="CreateOverlapContour"/>'s lines, the silhouette heavier than the overlaps, and returns what it drew.</summary>
    public Dictionary<string, object?> DrawOverlapContour(CanvasRenderingContext2D ctx, object shapesObj, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var opt = JsInterop.AsDict(options);
        if (opt != null)
            foreach (var key in opt.Keys)
                if (Array.IndexOf(OverlapDrawOptions, key?.ToString()) < 0)
                    throw new ArgumentException($"drawOverlapContour has no option '{key}'. It takes {string.Join(", ", OverlapDrawOptions)}.");
        var contour = OverlapContour(shapesObj, opt);

        ctx.Save();
        ctx.StrokeStyle = opt?["strokeColor"]?.ToString() ?? "#1a1a18";
        ctx.LineCap = "round";
        ctx.LineJoin = "round";
        ctx.LineWidth = Num(opt, "innerWidth", 2f);
        ctx.Stroke((CanvasPath)contour["inner"]!);
        ctx.LineWidth = Num(opt, "outerWidth", 3.5f);
        ctx.Stroke((CanvasPath)contour["outer"]!);
        ctx.Restore();
        return contour;
    }

    Dictionary<string, object?> OverlapContour(object shapesObj, IDictionary? opt)
    {
        var shapes = OrderByDepth(ReadNamedPaths(WithFigureGeometry(shapesObj, out var single), "createOverlapContour"),
            opt?["order"], single);
        if (shapes.Count == 0) throw new ArgumentException("createOverlapContour needs at least one shape.");
        foreach (var (name, path) in shapes)
            if (IsOpen(path.Path, out _))
                throw new ArgumentException($"createOverlapContour: '{name}' is a line, and a line has no inside to hide anything behind. Give it a width with ctx.strokeToPath(...) to make it a shape.");

        var smallest = shapes.Select(s => CanvasPath.AreaOf(s.Path.Path)).Where(a => a > 0f).DefaultIfEmpty(1600f).Min();
        var step = opt != null && opt.Contains("step") ? MathF.Max(0.25f, Num(opt, "step", 1f)) : Math.Clamp(MathF.Sqrt(smallest) / 40f, 0.5f, 3f);
        var probe = MathF.Max(1.5f, step);

        var outer = new CanvasPath();
        var inner = new CanvasPath();
        var junctions = new List<object?>();
        var records = new Dictionary<string, object?>();

        // The nearest shape in front of shape i that covers a point, or -1.
        int Hider(int i, float x, float y)
        {
            for (var j = 0; j < i; j++) if (shapes[j].Path.Path.Contains(x, y)) return j;
            return -1;
        }

        for (var i = 0; i < shapes.Count; i++)
        {
            var own = shapes[i].Path.Path;
            var lines = new CanvasPath();
            float visibleLength = 0f, hiddenLength = 0f;
            using var simple = new SKPath();
            var source = own.Simplify(simple) ? simple : own;
            using var measure = new SKPathMeasure(source, true);
            do
            {
                var length = measure.Length;
                if (length <= 0f) continue;

                // 0 an overlap line, 1 on the silhouette, -(j + 2) hidden by shape j.
                int Code(float d)
                {
                    if (!measure.GetPositionAndTangent(d, out var p, out var t)) return 0;
                    var h = Hider(i, p.X, p.Y);
                    if (h >= 0) return -(h + 2);
                    var l = MathF.Max(1e-6f, MathF.Sqrt(t.X * t.X + t.Y * t.Y));
                    float nx = t.Y / l, ny = -t.X / l;
                    if (own.Contains(p.X + nx * probe, p.Y + ny * probe)) (nx, ny) = (-nx, -ny);
                    float ox = p.X + nx * probe, oy = p.Y + ny * probe;
                    return shapes.Any(s => s.Path.Path.Contains(ox, oy)) ? 0 : 1;
                }

                var n = Math.Max(16, (int)MathF.Ceiling(length / step));
                var codes = new int[n];
                for (var k = 0; k < n; k++) codes[k] = Code(length * k / n);

                // Where the code changes between sample k - 1 and k, refined to a tenth of a sample.
                var cuts = new List<(float At, int From, int To)>();
                for (var k = 0; k < n; k++)
                {
                    var prev = codes[(k - 1 + n) % n];
                    if (prev == codes[k]) continue;
                    float lo = length * ((k - 1 + n) % n) / n, hi = k == 0 ? length : length * k / n;
                    for (var it = 0; it < 12 && hi - lo > step * 0.01f; it++)
                    {
                        var mid = (lo + hi) * 0.5f;
                        if (Code(mid) == prev) lo = mid; else hi = mid;
                    }
                    cuts.Add(((lo + hi) * 0.5f % length, prev, codes[k]));
                }

                if (cuts.Count == 0)
                {
                    if (codes[0] < 0) { hiddenLength += length; continue; }
                    visibleLength += length;
                    var whole = codes[0] == 1 ? outer : inner;
                    Segment(measure, 0f, length, whole.Path, true);
                    whole.Path.Close();
                    Segment(measure, 0f, length, lines.Path, true);
                    lines.Path.Close();
                    continue;
                }

                cuts.Sort((a, b) => a.At.CompareTo(b.At));
                for (var c = 0; c < cuts.Count; c++)
                {
                    var (from, was, code) = cuts[c];
                    var to = cuts[(c + 1) % cuts.Count].At;
                    var span = to > from ? to - from : length - from + to;

                    // A change between hidden and shown is a T-junction: this far line stops at the near one.
                    if ((was < 0) != (code < 0) && measure.GetPosition(from, out var at))
                    {
                        var near = shapes[-Math.Min(was, code) - 2].Name;
                        var mark = new CanvasPath();
                        mark.Arc(at.X, at.Y, MathF.Max(4f, probe * 2f), 0f, MathF.PI * 2f);
                        junctions.Add(new Dictionary<string, object?>
                        {
                            ["at"] = ToDict(new Point2D(at.X, at.Y)),
                            ["near"] = near,
                            ["far"] = shapes[i].Name,
                            ["mark"] = mark
                        });
                    }

                    if (code < 0) { hiddenLength += span; continue; }
                    visibleLength += span;
                    foreach (var target in new[] { code == 1 ? outer : inner, lines })
                    {
                        if (to > from) Segment(measure, from, to, target.Path, true);
                        else
                        {
                            Segment(measure, from, length, target.Path, true);
                            Segment(measure, 0f, to, target.Path, false);
                        }
                    }
                }
            }
            while (measure.NextContour());

            records[shapes[i].Name] = new Dictionary<string, object?>
            {
                ["lines"] = lines,
                ["visibleLength"] = visibleLength,
                ["hiddenLength"] = hiddenLength,
                ["depth"] = i
            };
        }

        var all = new CanvasPath();
        all.AddPath(outer);
        all.AddPath(inner);
        return new Dictionary<string, object?>
        {
            ["lines"] = all,
            ["outer"] = outer,
            ["inner"] = inner,
            ["junctions"] = junctions,
            ["count"] = junctions.Count,
            ["order"] = shapes.Select(s => (object?)s.Name).ToList(),
            ["shapes"] = records,
            ["step"] = step
        };
    }

    /// <summary>Appends the stretch of a contour from <paramref name="from"/> to <paramref name="to"/>, as a new line or continuing the last.</summary>
    static void Segment(SKPathMeasure measure, float from, float to, SKPath target, bool newLine)
    {
        using var builder = new SKPathBuilder();
        if (!measure.GetSegment(from, to, builder, true)) return;
        using var piece = builder.Detach();
        target.AddPath(piece, newLine ? SKPathAddMode.Append : SKPathAddMode.Extend);
    }

    /// <summary>A figure, or an object holding figures, with each figure replaced by its geometry.</summary>
    object WithFigureGeometry(object shapesObj, out bool single)
    {
        static bool IsFigure(object? o) => JsInterop.AsDict(o) is IDictionary d && d.Contains("headUnit") && d.Contains("pelvis");
        single = IsFigure(shapesObj) || (JsInterop.AsDict(shapesObj) is IDictionary g && g.Contains("groups") && g["groups"] is not CanvasPath);
        if (IsFigure(shapesObj)) return CreateFigureGeometry(shapesObj);
        if (single || JsInterop.AsDict(shapesObj) is not IDictionary dict) return shapesObj;

        var result = new Dictionary<string, object?>();
        foreach (DictionaryEntry kv in dict)
            result[kv.Key.ToString()!] = IsFigure(kv.Value) ? CreateFigureGeometry(kv.Value!) : kv.Value;
        return result;
    }

    /// <summary>Shapes nearest first: by <paramref name="orderObj"/>, then by figure depth within each figure, then as given.</summary>
    static List<(string Name, CanvasPath Path)> OrderByDepth(List<(string Name, CanvasPath Path)> shapes, object? orderObj, bool single)
    {
        var order = orderObj switch
        {
            null => [],
            string one => [one],
            IEnumerable many => many.Cast<object?>().Select(o => o?.ToString() ?? "").ToList(),
            _ => throw new ArgumentException("createOverlapContour's order is a list of names, nearest first: ['him', 'her.leftArm'].")
        };
        foreach (var name in order)
            if (!shapes.Any(s => Join.Covers(name, s.Name)))
                throw new ArgumentException($"createOverlapContour's order names '{name}', and there is no such shape. There are: {string.Join(", ", shapes.Select(s => s.Name))}.");

        string Owner(string name) => name.LastIndexOf('.') is var dot and > 0 ? name[..dot] : single ? "" : name;
        var firstOwner = new Dictionary<string, int>();
        for (var k = 0; k < shapes.Count; k++) firstOwner.TryAdd(Owner(shapes[k].Name), k);

        return shapes
            .Select((s, k) => (s, k))
            .OrderBy(x => order.FindIndex(o => Join.Covers(o, x.s.Name)) is var r and >= 0 ? r : order.Count)
            .ThenBy(x => firstOwner[Owner(x.s.Name)])
            .ThenBy(x => Array.IndexOf(FigureDepth, x.s.Name[(x.s.Name.LastIndexOf('.') + 1)..]) is var f and >= 0 ? f : FigureDepth.Length)
            .ThenBy(x => x.k)
            .Select(x => x.s)
            .ToList();
    }
    #endregion
}
