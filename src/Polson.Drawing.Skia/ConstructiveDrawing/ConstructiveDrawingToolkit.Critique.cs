namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using SkiaSharp;

public partial class ConstructiveDrawingToolkit
{
    #region Tangents
    static readonly string[] TangentOptions = ["gap", "near", "angleDeg", "minRun", "step", "attached"];

    /// <summary>
    /// Finds the two kinds of tangent Stanchfield corrects: shapes that <b>touch</b> — outlines kissing,
    /// or overlapping by only a sliver — and shapes that <b>align</b>, running side by side along parallel
    /// edges (<i>Drawn to Life</i> vol. 1 ch. 16, 30; vol. 2 ch. 57).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>shapes</c> is an object of named <c>CanvasPath</c>s, an array of them, or a result of
    /// <c>createFigureGeometry</c>, whose <c>groups</c> are then used. Every pair is tested.
    /// </para>
    /// <para>
    /// A touch is judged by one number, <c>gap</c>: outlines closer than it without overlapping, or an
    /// overlap thinner than it. A decisive overlap — an arm entering the shoulder, one circle behind
    /// another — is thicker than that and is not reported, which is why shapes joined by construction
    /// need no exemption. An alignment is a run of the smaller shape's outline, outside the larger,
    /// within <c>near</c> of the larger's outline and parallel to it within <c>angleDeg</c>, at least
    /// <c>minRun</c> long.
    /// </para>
    /// <para>
    /// Lengths default to fractions of the smaller shape's size (the square root of its area), so the
    /// same pose gives the same answer at any scale; each tangent reports the values it was judged by.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> FindTangents(object shapesObj, object? options = null)
    {
        var shapes = ReadShapes(shapesObj);
        var opt = JsInterop.AsDict(options);
        if (opt != null)
            foreach (var key in opt.Keys)
                if (Array.IndexOf(TangentOptions, key?.ToString()) < 0)
                    throw new ArgumentException($"findTangents has no option '{key}'. It takes {string.Join(", ", TangentOptions)}.");

        // A path with no closed contour is a line: a horizon, a boom, a rope. It has no area, so it is
        // sized by its length, a quarter of which stands in for a shape's size.
        var areas = new float[shapes.Count];
        var lengths = new float[shapes.Count];
        var open = new bool[shapes.Count];
        var smallest = float.MaxValue;
        for (var i = 0; i < shapes.Count; i++)
        {
            open[i] = IsOpen(shapes[i].Path.Path, out lengths[i]);
            if (open[i]) { if (lengths[i] > 0f) smallest = MathF.Min(smallest, 0.25f * lengths[i]); continue; }
            areas[i] = CanvasPath.AreaOf(shapes[i].Path.Path);
            if (areas[i] > 0f) smallest = MathF.Min(smallest, MathF.Sqrt(areas[i]));
        }

        var step = opt != null && opt.Contains("step") ? MathF.Max(0.25f, Num(opt, "step", 1f))
            : smallest == float.MaxValue ? 1f : Math.Clamp(smallest / 40f, 0.5f, 3f);
        var samples = new List<OutlineSample>?[shapes.Count];
        var lines = new (List<OutlineSample> Samples, List<LineEnd> Ends)?[shapes.Count];
        List<OutlineSample> SamplesOf(int i) => samples[i] ??= SampleOutline(shapes[i].Path.Path, step);
        (List<OutlineSample> Samples, List<LineEnd> Ends) LineOf(int i) => lines[i] ??= SampleLine(shapes[i].Path.Path, step);

        var touching = new List<object?>();
        var aligned = new List<object?>();
        var ends = new List<object?>();
        var pairs = 0;

        for (var i = 0; i < shapes.Count; i++)
            for (var j = i + 1; j < shapes.Count; j++)
            {
                if (open[i] || open[j])
                {
                    if ((open[i] ? lengths[i] : areas[i]) <= 0f || (open[j] ? lengths[j] : areas[j]) <= 0f) continue;
                    if (LineTangents(i, j)) pairs++;
                    continue;
                }
                if (areas[i] <= 0f || areas[j] <= 0f) continue;
                var (a, b) = areas[i] <= areas[j] ? (i, j) : (j, i);
                var size = MathF.Sqrt(areas[a]);
                var gap = Num(opt, "gap", size * 0.08f);
                var near = Num(opt, "near", size * 0.35f);
                var minRun = Num(opt, "minRun", size * 0.35f);
                var maxAngle = Num(opt, "angleDeg", 15f);

                var ba = shapes[a].Path.Path.Bounds;
                var bb = shapes[b].Path.Path.Bounds;
                var reach = MathF.Max(gap, near);
                if (ba.Left > bb.Right + reach || bb.Left > ba.Right + reach || ba.Top > bb.Bottom + reach || bb.Top > ba.Bottom + reach)
                    continue;
                pairs++;

                var sa = SamplesOf(a);
                var sb = SamplesOf(b);
                if (sa.Count == 0 || sb.Count == 0) continue;

                // Nearest point of B's outline for every sample of A's.
                var nearest = new int[sa.Count];
                var dist = new float[sa.Count];
                var best = 0;
                for (var k = 0; k < sa.Count; k++)
                {
                    var d2 = float.MaxValue;
                    for (var m = 0; m < sb.Count; m++)
                    {
                        float dx = sb[m].X - sa[k].X, dy = sb[m].Y - sa[k].Y, q = dx * dx + dy * dy;
                        if (q < d2) { d2 = q; nearest[k] = m; }
                    }
                    dist[k] = MathF.Sqrt(d2);
                    if (dist[k] < dist[best]) best = k;
                }

                var names = (shapes[a].Name, shapes[b].Name);
                var run = Alignment(sa, sb, nearest, dist, shapes[a].Path.Path, shapes[b].Path.Path, near, gap, maxAngle, step);
                var isAligned = run != null && run.Length >= minRun;
                if (isAligned) aligned.Add(AlignmentRecord(sa, run!, names, near, minRun, maxAngle));

                // Two edges running side by side are closest somewhere along the run; that point is the
                // alignment, not a second tangent. A kiss elsewhere, or a sliver of overlap, still counts.
                var touch = Touch(shapes[a].Path, shapes[b].Path, areas[a], sa[best], sb[nearest[best]], dist[best], gap);
                if (touch != null && !(isAligned && touch["overlapping"] is false && OnRun(sa, run!, sa[best], near)))
                {
                    touch["a"] = names.Item1;
                    touch["b"] = names.Item2;
                    touching.Add(touch);
                }
            }

        // Joins declared in `attached` are construction, not tangents: a boom must end on its mast. Their ends
        // and touches move to `attached`; running alongside is still reported. A declared pair that does not
        // meet at all is reported under `apart`, because the declaration says they should.
        var joins = ReadAttached(opt?["attached"], shapes);
        var attached = new List<object?>();
        var apart = new List<object?>();
        if (joins.Count > 0)
        {
            bool Joined(object? t) => t is IDictionary d && joins.Any(j => j.Matches(d["a"]?.ToString(), d["b"]?.ToString()));
            attached.AddRange(touching.Where(Joined));
            attached.AddRange(ends.Where(Joined));
            touching.RemoveAll(Joined);
            ends.RemoveAll(Joined);

            foreach (var join in joins)
            {
                var (distance, from, to, gap) = (float.MaxValue, default(Point2D), default(Point2D), 0f);
                for (var i = 0; i < shapes.Count; i++)
                    for (var j = 0; j < shapes.Count; j++)
                    {
                        if (i == j || !join.Left(shapes[i].Name) || !join.Right(shapes[j].Name)) continue;
                        var (d, p, q) = Separation(i, j);
                        if (d < distance) (distance, from, to, gap) = (d, p, q, GapOf(i, j));
                    }
                if (distance <= gap) continue;
                var mark = new CanvasPath();
                mark.MoveTo(from.X, from.Y);
                mark.LineTo(to.X, to.Y);
                apart.Add(new Dictionary<string, object?>
                {
                    ["a"] = join.A,
                    ["b"] = join.B,
                    ["distance"] = distance,
                    ["gap"] = gap,
                    ["at"] = ToDict(new Point2D((from.X + to.X) * 0.5f, (from.Y + to.Y) * 0.5f)),
                    ["mark"] = mark
                });
            }
        }

        var all = new List<object?>(touching);
        all.AddRange(aligned);
        all.AddRange(ends);
        return new Dictionary<string, object?>
        {
            ["tangents"] = all,
            ["count"] = all.Count,
            ["touching"] = touching.Count,
            ["aligned"] = aligned.Count,
            ["ends"] = ends.Count,
            ["attached"] = attached,
            ["apart"] = apart,
            ["pairs"] = pairs,
            ["step"] = step
        };

        // The gap a pair is judged by, sized as the tangent tests size it.
        float GapOf(int i, int j)
        {
            float size;
            if (open[i] && open[j]) size = 0.25f * MathF.Min(lengths[i], lengths[j]);
            else if (open[i] || open[j])
            {
                var (l, sh) = open[i] ? (i, j) : (j, i);
                size = MathF.Min(MathF.Sqrt(areas[sh]), 0.25f * lengths[l]);
            }
            else size = MathF.Sqrt(MathF.Min(areas[i], areas[j]));
            return Num(opt, "gap", size * 0.08f);
        }

        // How far apart two shapes or lines are, and the nearest points; zero where they overlap or cross.
        (float Distance, Point2D From, Point2D To) Separation(int i, int j)
        {
            if (!open[i] && !open[j])
            {
                using var overlap = shapes[i].Path.Intersect(shapes[j].Path);
                if (!overlap.IsEmpty) return (0f, default, default);
            }
            var si = open[i] ? LineOf(i).Samples : SamplesOf(i);
            var sj = open[j] ? LineOf(j).Samples : SamplesOf(j);
            if (open[i] && !open[j] && si.Any(p => shapes[j].Path.Path.Contains(p.X, p.Y))) return (0f, default, default);
            if (open[j] && !open[i] && sj.Any(p => shapes[i].Path.Path.Contains(p.X, p.Y))) return (0f, default, default);
            var (best, from, to) = (float.MaxValue, default(Point2D), default(Point2D));
            foreach (var p in si)
                foreach (var q in sj)
                {
                    float dx = q.X - p.X, dy = q.Y - p.Y, d2 = dx * dx + dy * dy;
                    if (d2 < best) (best, from, to) = (d2, new Point2D(p.X, p.Y), new Point2D(q.X, q.Y));
                }
            return (best == float.MaxValue ? float.MaxValue : MathF.Sqrt(best), from, to);
        }

        // A pair with at least one line in it. A line against a shape can end on its outline, graze it, or run
        // along it; two lines can end on each other or run side by side. A line crossing a shape or another line
        // decisively is ordinary overlap, not a tangent, and is not reported. Returns whether the pair was tested.
        bool LineTangents(int i, int j)
        {
            var bothLines = open[i] && open[j];
            var (l, s) = open[i] && !open[j] ? (i, j) : open[j] && !open[i] ? (j, i)
                : lengths[i] <= lengths[j] ? (i, j) : (j, i);   // two lines: the shorter is tested against the longer
            var size = bothLines ? 0.25f * MathF.Min(lengths[l], lengths[s]) : MathF.Min(MathF.Sqrt(areas[s]), 0.25f * lengths[l]);
            var gap = Num(opt, "gap", size * 0.08f);
            var near = Num(opt, "near", size * 0.35f);
            var minRun = Num(opt, "minRun", size * 0.35f);
            var maxAngle = Num(opt, "angleDeg", 15f);

            var bl = shapes[l].Path.Path.Bounds;
            var bs = shapes[s].Path.Path.Bounds;
            var reach = MathF.Max(gap, near);
            if (bl.Left > bs.Right + reach || bs.Left > bl.Right + reach || bl.Top > bs.Bottom + reach || bs.Top > bl.Bottom + reach)
                return false;

            var line = LineOf(l);
            var other = bothLines ? LineOf(s).Samples : SamplesOf(s);
            if (line.Samples.Count == 0 || other.Count == 0) return true;

            float Nearest(float x, float y, out int index)
            {
                var d2 = float.MaxValue;
                index = 0;
                for (var m = 0; m < other.Count; m++)
                {
                    float dx = other[m].X - x, dy = other[m].Y - y, q = dx * dx + dy * dy;
                    if (q < d2) { d2 = q; index = m; }
                }
                return MathF.Sqrt(d2);
            }

            // Ends: a line that stops on an outline, or on another line.
            var reported = new List<Point2D>();
            void End(string lineName, string otherName, LineEnd end, float d, bool inside)
            {
                if (reported.Any(p => (p.X - end.P.X) * (p.X - end.P.X) + (p.Y - end.P.Y) * (p.Y - end.P.Y) <= gap * gap)) return;
                reported.Add(end.P);
                var mark = new CanvasPath();
                mark.Arc(end.P.X, end.P.Y, MathF.Max(gap, 4f), 0f, MathF.PI * 2f);
                ends.Add(new Dictionary<string, object?>
                {
                    ["kind"] = "end",
                    ["a"] = lineName,
                    ["b"] = otherName,
                    ["end"] = end.Start ? "start" : "end",
                    ["at"] = ToDict(end.P),
                    ["distance"] = d,
                    ["inside"] = inside,
                    ["gap"] = gap,
                    ["mark"] = mark
                });
            }
            // Alignment: the line running along the other's edge. Found first, because an end lying on the run
            // is part of that alignment rather than a second contact.
            var nearest = new int[line.Samples.Count];
            var dist = new float[line.Samples.Count];
            for (var k = 0; k < line.Samples.Count; k++) dist[k] = Nearest(line.Samples[k].X, line.Samples[k].Y, out nearest[k]);
            using var none = new SKPath();
            var run = Alignment(line.Samples, other, nearest, dist, none, bothLines ? none : shapes[s].Path.Path, near, gap, maxAngle, step, open: true);
            var isAligned = run != null && run.Length >= minRun;
            if (isAligned) aligned.Add(AlignmentRecord(line.Samples, run!, (shapes[l].Name, shapes[s].Name), near, minRun, maxAngle));
            bool OnTheRun(Point2D p) => isAligned && OnRun(line.Samples, run!, new OutlineSample(p.X, p.Y, 0f, 0f, 0), gap);

            foreach (var end in line.Ends)
            {
                var d = Nearest(end.P.X, end.P.Y, out _);
                if (d <= gap && !OnTheRun(end.P)) End(shapes[l].Name, shapes[s].Name, end, d, !bothLines && shapes[s].Path.Path.Contains(end.P.X, end.P.Y));
            }
            if (bothLines)
            {
                foreach (var end in LineOf(s).Ends)
                {
                    var d = DistanceToSamples(line.Samples, end.P);
                    if (d <= gap && !OnTheRun(end.P)) End(shapes[s].Name, shapes[l].Name, end, d, false);
                }
                return true;
            }

            // Touch: the line grazes the outline, coming within `gap` of it without crossing deeper than `gap`.
            // Samples beside an end already reported are that end's contact, not a second one.
            var shape = shapes[s].Path.Path;
            int kiss = -1, deepest = -1;
            for (var k = 0; k < line.Samples.Count; k++)
            {
                var p = line.Samples[k];
                if (reported.Any(e => (e.X - p.X) * (e.X - p.X) + (e.Y - p.Y) * (e.Y - p.Y) <= 4f * gap * gap)) continue;
                if (shape.Contains(p.X, p.Y)) { if (deepest < 0 || dist[k] > dist[deepest]) deepest = k; }
                else if (kiss < 0 || dist[k] < dist[kiss]) kiss = k;
            }
            int at;
            if (deepest >= 0) { if (dist[deepest] > gap) return true; at = deepest; }
            else if (kiss >= 0 && dist[kiss] <= gap) at = kiss;
            else return true;
            if (isAligned && OnRun(line.Samples, run!, line.Samples[at], near)) return true;

            var hit = line.Samples[at];
            var touchMark = new CanvasPath();
            touchMark.Arc(hit.X, hit.Y, MathF.Max(gap, 4f), 0f, MathF.PI * 2f);
            touching.Add(new Dictionary<string, object?>
            {
                ["kind"] = "touch",
                ["a"] = shapes[l].Name,
                ["b"] = shapes[s].Name,
                ["overlapping"] = deepest >= 0,
                ["at"] = ToDict(new Point2D(hit.X, hit.Y)),
                ["distance"] = deepest >= 0 ? 0f : dist[at],
                ["depth"] = deepest >= 0 ? dist[at] : 0f,
                ["gap"] = gap,
                ["mark"] = touchMark
            });
            return true;
        }
    }

    record struct OutlineSample(float X, float Y, float Tx, float Ty, int Contour);

    /// <summary>One end of a line: where it is, and whether it is the start of its contour.</summary>
    record struct LineEnd(Point2D P, bool Start);

    /// <summary>Whether a path is a line - no closed contour - and how long it is.</summary>
    static bool IsOpen(SKPath path, out float length)
    {
        length = 0f;
        var any = false;
        var allOpen = true;
        using var measure = new SKPathMeasure(path, false);
        do
        {
            if (measure.Length <= 0f) continue;
            any = true;
            length += measure.Length;
            if (measure.IsClosed) allOpen = false;
        }
        while (measure.NextContour());
        return any && allOpen;
    }

    /// <summary>Evenly spaced points along a line, both ends included, and the ends themselves.</summary>
    static (List<OutlineSample> Samples, List<LineEnd> Ends) SampleLine(SKPath path, float step)
    {
        var samples = new List<OutlineSample>();
        var ends = new List<LineEnd>();
        using var measure = new SKPathMeasure(path, false);
        var contour = 0;
        do
        {
            var length = measure.Length;
            if (length <= 0f) continue;
            var n = Math.Max(2, (int)MathF.Ceiling(length / step));
            for (var i = 0; i <= n; i++)
                if (measure.GetPositionAndTangent(length * i / n, out var p, out var t))
                {
                    var l = MathF.Max(1e-6f, MathF.Sqrt(t.X * t.X + t.Y * t.Y));
                    samples.Add(new OutlineSample(p.X, p.Y, t.X / l, t.Y / l, contour));
                    if (i == 0 || i == n) ends.Add(new LineEnd(new Point2D(p.X, p.Y), i == 0));
                }
            contour++;
        }
        while (measure.NextContour());
        return (samples, ends);
    }

    static float DistanceToSamples(List<OutlineSample> samples, Point2D p)
    {
        var d2 = float.MaxValue;
        foreach (var s in samples) d2 = MathF.Min(d2, (s.X - p.X) * (s.X - p.X) + (s.Y - p.Y) * (s.Y - p.Y));
        return MathF.Sqrt(d2);
    }

    sealed record AlignedRun(int ContourStart, int ContourCount, int Offset, int Count, float Length, float MeanDistance, float MeanAngle, bool Flush)
    {
        public int Index(int k) => ContourStart + (Offset + k) % ContourCount;
    }

    /// <summary>The shapes to test, in the order they were given.</summary>
    /// <summary>
    /// The <c>attached</c> pairs: <c>[['boom', 'mast'], ['her', 'rope']]</c>. A name matches a shape of that name, or
    /// every part named under it (<c>her</c> matches <c>her.leftArm</c>); one that matches nothing is refused.
    /// </summary>
    static List<Join> ReadAttached(object? value, List<(string Name, CanvasPath Path)> shapes)
    {
        var joins = new List<Join>();
        if (value is null) return joins;
        if (value is not IList list)
            throw new ArgumentException("findTangents' attached is a list of pairs that meet by construction: [['boom', 'mast'], ['her', 'rope']].");
        foreach (var item in list)
        {
            if (item is not IList pair || pair.Count != 2 || pair[0] is not string a || pair[1] is not string b)
                throw new ArgumentException("findTangents' attached takes pairs of names, each [a, b]: [['boom', 'mast']].");
            var join = new Join(a, b);
            foreach (var name in new[] { a, b })
                if (!shapes.Any(sh => Join.Covers(name, sh.Name)))
                    throw new ArgumentException($"findTangents' attached names '{name}', and there is no such shape. There are: {string.Join(", ", shapes.Select(sh => sh.Name))}.");
            joins.Add(join);
        }
        return joins;
    }

    /// <summary>Two names, or name prefixes, that meet by construction.</summary>
    sealed record Join(string A, string B)
    {
        public static bool Covers(string declared, string name) =>
            name == declared || name.StartsWith(declared + ".", StringComparison.Ordinal);

        public bool Left(string name) => Covers(A, name);

        public bool Right(string name) => Covers(B, name);

        public bool Matches(string? a, string? b) => a is not null && b is not null
            && ((Covers(A, a) && Covers(B, b)) || (Covers(A, b) && Covers(B, a)));
    }

    static List<(string Name, CanvasPath Path)> ReadShapes(object shapesObj)
    {
        var list = ReadNamedPaths(shapesObj);
        if (list.Count < 2) throw new ArgumentException($"findTangents needs at least two shapes; it was given {list.Count}.");
        return list;
    }

    /// <summary>Named shapes and lines, in any of the forms <c>findTangents</c> takes. <paramref name="who"/> names the caller in errors.</summary>
    static List<(string Name, CanvasPath Path)> ReadNamedPaths(object shapesObj, string who = "findTangents")
    {
        var usage = $"{who} needs an object of named shapes and lines, an array of them, or a createFigureGeometry(...) result. " +
                             "A shape is a closed CanvasPath; a line is { x1, y1, x2, y2 }, an array of points, SVG path data, or a CanvasPath with no closed contour; " +
                             "a createFigureGeometry(...) result inside the object adds its groups as name.group.";
        var list = new List<(string, CanvasPath)>();

        void Add(string name, object? value)
        {
            switch (value)
            {
                case CanvasPath path:
                    list.Add((name, path));
                    return;
                case string d:
                    list.Add((name, new CanvasPath(d)));
                    return;
            }

            if (JsInterop.AsDict(value) is IDictionary dict)
            {
                if (dict.Contains("groups") && JsInterop.AsDict(dict["groups"]) is IDictionary groups)
                {
                    foreach (DictionaryEntry kv in groups) Add($"{name}.{kv.Key}", kv.Value);
                    return;
                }
                if (dict.Contains("x1") && dict.Contains("y1") && dict.Contains("x2") && dict.Contains("y2"))
                {
                    var segment = new CanvasPath();
                    segment.MoveTo(Num(dict, "x1", 0f), Num(dict, "y1", 0f));
                    segment.LineTo(Num(dict, "x2", 0f), Num(dict, "y2", 0f));
                    list.Add((name, segment));
                    return;
                }
                throw new ArgumentException($"{who}: '{name}' is an object, but neither a line {{ x1, y1, x2, y2 }} nor a createFigureGeometry(...) result. {usage}");
            }

            if (value is IEnumerable points)
            {
                var polyline = new CanvasPath();
                var count = 0;
                foreach (var point in points)
                {
                    var p = JsInterop.AsDict(point) is IDictionary pd && pd.Contains("x") && pd.Contains("y") ? ExtractPoint(pd)
                        : point is IList pl && pl.Count >= 2 ? ExtractPoint(pl)
                        : throw new ArgumentException($"{who}: '{name}' is a list, but item {count} is not a point {{ x, y }} or [x, y]. {usage}");
                    if (count++ == 0) polyline.MoveTo(p.X, p.Y); else polyline.LineTo(p.X, p.Y);
                }
                if (count < 2) throw new ArgumentException($"{who}: line '{name}' needs at least two points; it has {count}.");
                list.Add((name, polyline));
                return;
            }

            throw new ArgumentException($"{who}: '{name}' is not a shape or a line. {usage}");
        }

        switch (shapesObj)
        {
            case IDictionary<string, object?> generic:
                if (generic.TryGetValue("groups", out var g1) && g1 is not CanvasPath) return ReadNamedPaths(g1!, who);
                foreach (var kv in generic) Add(kv.Key, kv.Value);
                break;
            case IDictionary dict:
                if (dict.Contains("groups") && dict["groups"] is not CanvasPath) return ReadNamedPaths(dict["groups"]!, who);
                foreach (DictionaryEntry kv in dict) Add(kv.Key.ToString()!, kv.Value);
                break;
            case IEnumerable items and not string:
                var n = 0;
                foreach (var item in items) Add((n++).ToString(CultureInfo.InvariantCulture), item);
                break;
            default:
                throw new ArgumentException(usage, nameof(shapesObj));
        }
        return list;
    }

    /// <summary>Evenly spaced points on every contour, each with its unit tangent.</summary>
    static List<OutlineSample> SampleOutline(SKPath path, float step)
    {
        var result = new List<OutlineSample>();
        using var simple = new SKPath();
        var source = path.Simplify(simple) ? simple : path;
        using var measure = new SKPathMeasure(source, true);
        var contour = 0;
        do
        {
            var length = measure.Length;
            if (length <= 0f) continue;
            var n = Math.Max(8, (int)MathF.Ceiling(length / step));
            for (var i = 0; i < n; i++)
                if (measure.GetPositionAndTangent(length * i / n, out var p, out var t))
                {
                    var l = MathF.Max(1e-6f, MathF.Sqrt(t.X * t.X + t.Y * t.Y));
                    result.Add(new OutlineSample(p.X, p.Y, t.X / l, t.Y / l, contour));
                }
            contour++;
        }
        while (measure.NextContour());
        return result;
    }

    /// <summary>A kiss or a graze between two shapes, or null when they are clear or overlap decisively.</summary>
    static Dictionary<string, object?>? Touch(CanvasPath a, CanvasPath b, float areaA, OutlineSample pa, OutlineSample pb, float distance, float gap)
    {
        using var overlap = a.Intersect(b);
        var overlapArea = CanvasPath.AreaOf(overlap.Path);
        Point2D at;
        float depth;

        if (overlapArea <= MathF.Max(0.5f, areaA * 1e-4f))
        {
            if (distance > gap) return null;
            at = new Point2D((pa.X + pb.X) * 0.5f, (pa.Y + pb.Y) * 0.5f);
            depth = 0f;
        }
        else
        {
            // Contained entirely is not a tangent; neither is an overlap thicker than the gap.
            if (overlapArea >= areaA * 0.98f) return null;
            using var rim = new SKPathMeasure(overlap.Path, true);
            var perimeter = 0f;
            do perimeter += rim.Length; while (rim.NextContour());
            depth = perimeter > 0f ? 2f * overlapArea / perimeter : 0f;   // mean thickness of the sliver
            if (depth > gap) return null;
            var r = overlap.Path.Bounds;
            at = new Point2D(r.MidX, r.MidY);
            distance = 0f;
        }

        var mark = new CanvasPath();
        mark.Arc(at.X, at.Y, MathF.Max(gap, 4f), 0f, MathF.PI * 2f);
        return new Dictionary<string, object?>
        {
            ["kind"] = "touch",
            ["overlapping"] = depth > 0f,
            ["at"] = ToDict(at),
            ["distance"] = distance,
            ["depth"] = depth,
            ["gap"] = gap,
            ["mark"] = mark
        };
    }

    /// <summary>The longest run of A's outline lying outside B, near B's outline and parallel to it.</summary>
    /// <param name="open">A's contours are lines, so a run stops at a line's end rather than wrapping round to its start.</param>
    static AlignedRun? Alignment(List<OutlineSample> sa, List<OutlineSample> sb, int[] nearest, float[] dist,
        SKPath a, SKPath b, float near, float gap, float maxAngle, float step, bool open = false)
    {
        var cosLimit = MathF.Cos(maxAngle * MathF.PI / 180f);
        var ok = new bool[sa.Count];
        var hidden = new bool[sa.Count];
        for (var k = 0; k < sa.Count; k++)
        {
            var s = sa[k];
            var t = sb[nearest[k]];
            // An edge that faces B runs beside it, within `near`. An edge whose way across to B passes
            // back through A is lying over B's hidden edge: that is the flush case — the silhouette
            // carries on as if A were not there — and only counts within `gap`, or the far side of any
            // thin shape overlapping B would count as running along it.
            hidden[k] = a.Contains((s.X + t.X) * 0.5f, (s.Y + t.Y) * 0.5f);
            ok[k] = dist[k] <= (hidden[k] ? gap : near) && MathF.Abs(s.Tx * t.Tx + s.Ty * t.Ty) >= cosLimit && !b.Contains(s.X, s.Y);
        }

        AlignedRun? best = null;
        var start = 0;
        while (start < sa.Count)
        {
            // One contour at a time, and circular within it, so a run across the seam is one run.
            var end = start;
            while (end < sa.Count && sa[end].Contour == sa[start].Contour) end++;
            var n = end - start;
            var allOk = true;
            for (var k = start; k < end; k++) allOk &= ok[k];

            if (allOk) best = Longer(best, Run(start, n, 0, n));
            else
                for (var k = 0; k < n; k++)
                {
                    var previous = open ? k > 0 && ok[start + k - 1] : ok[start + (k - 1 + n) % n];
                    if (!ok[start + k] || previous) continue;   // begin at each run's first sample
                    var len = 0;
                    while (len < n && (!open || k + len < n) && ok[start + (k + len) % n]) len++;
                    best = Longer(best, Run(start, n, k, len));
                }
            start = end;
        }
        return best;

        AlignedRun Run(int contourStart, int contourCount, int offset, int count)
        {
            float sumD = 0f, sumA = 0f, length = step;
            var flush = 0;
            for (var k = 0; k < count; k++)
            {
                var idx = contourStart + (offset + k) % contourCount;
                var s = sa[idx];
                var t = sb[nearest[idx]];
                sumD += dist[idx];
                if (hidden[idx]) flush++;
                sumA += MathF.Acos(Math.Clamp(MathF.Abs(s.Tx * t.Tx + s.Ty * t.Ty), 0f, 1f)) * 180f / MathF.PI;
                if (k == 0) continue;
                var p = sa[contourStart + (offset + k - 1) % contourCount];
                length += MathF.Sqrt((s.X - p.X) * (s.X - p.X) + (s.Y - p.Y) * (s.Y - p.Y));
            }
            return new AlignedRun(contourStart, contourCount, offset, count, length, sumD / count, sumA / count, flush * 2 > count);
        }

        static AlignedRun? Longer(AlignedRun? a, AlignedRun b) => a == null || b.Length > a.Length ? b : a;
    }

    /// <summary>Whether a point lies along an aligned run, within <paramref name="near"/> of it.</summary>
    static bool OnRun(List<OutlineSample> sa, AlignedRun run, OutlineSample p, float near)
    {
        for (var k = 0; k < run.Count; k++)
        {
            var s = sa[run.Index(k)];
            if ((s.X - p.X) * (s.X - p.X) + (s.Y - p.Y) * (s.Y - p.Y) <= near * near) return true;
        }
        return false;
    }

    static Dictionary<string, object?> AlignmentRecord(List<OutlineSample> sa, AlignedRun run, (string A, string B) names,
        float near, float minRun, float maxAngle)
    {
        OutlineSample At(int k) => sa[run.Index(k)];
        var mark = new CanvasPath();
        for (var k = 0; k < run.Count; k++)
        {
            var s = At(k);
            if (k == 0) mark.MoveTo(s.X, s.Y); else mark.LineTo(s.X, s.Y);
        }

        var first = At(0);
        var last = At(run.Count - 1);
        var mid = At(run.Count / 2);
        return new Dictionary<string, object?>
        {
            ["kind"] = "align",
            ["flush"] = run.Flush,
            ["a"] = names.A,
            ["b"] = names.B,
            ["at"] = ToDict(new Point2D(mid.X, mid.Y)),
            ["from"] = ToDict(new Point2D(first.X, first.Y)),
            ["to"] = ToDict(new Point2D(last.X, last.Y)),
            ["length"] = run.Length,
            ["distance"] = run.MeanDistance,
            ["angleDeg"] = run.MeanAngle,
            ["near"] = near,
            ["minRun"] = minRun,
            ["maxAngleDeg"] = maxAngle,
            ["mark"] = mark
        };
    }
    #endregion

    #region Look Path
    static readonly string[] LookOptions = ["ignore", "obstacles", "from", "step"];

    /// <summary>
    /// Whether the line of a figure's look, from its head to what it looks at, runs clear of its own body and of
    /// anything else in the way. Stanchfield: get the body out of the way of the look (<i>Drawn to Life</i> vol. 1
    /// ch. 39, 77, 93; Manual 28).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The figure is tested part by part, so a hit names the part and its group. The head is where the look
    /// starts and the neck joins it to the body, so neither counts; <c>ignore</c> adds more, by part or group
    /// name — the arms closed on a rope the figure looks up. <c>obstacles</c> adds shapes that can block the
    /// look, in the forms <c>findTangents</c> takes, a figure's geometry included. A line cannot block a look
    /// and is refused: give it a width with <c>strokeToPath</c> if it should.
    /// </para>
    /// <para>
    /// The look starts at the head's centre, or <c>from</c>. Hits are the stretches of the path inside a part
    /// or an obstacle, in order along it; <c>clear</c> is the share of the path outside all of them.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> CheckLookPath(object figureObj, object target, object? options = null)
    {
        var opt = JsInterop.AsDict(options);
        if (opt != null)
            foreach (var key in opt.Keys)
                if (Array.IndexOf(LookOptions, key?.ToString()) < 0)
                    throw new ArgumentException($"checkLookPath has no option '{key}'. It takes {string.Join(", ", LookOptions)}.");

        // The figure's parts, each with its group: from a figure, or from its geometry.
        var fig = JsInterop.AsDict(figureObj)
            ?? throw new ArgumentException("checkLookPath needs a figure from Drawing.createMannequinFigure(...) or its createFigureGeometry(...).", nameof(figureObj));
        var geometry = fig.Contains("parts") && fig.Contains("groups") ? fig : CreateFigureGeometry(fig);
        var parts = JsInterop.AsDict(geometry["parts"]) ?? throw new ArgumentException("checkLookPath: the figure's geometry has no parts.");
        var partGroups = JsInterop.AsDict(geometry.Contains("partGroups") ? geometry["partGroups"] : null);
        var groupOf = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (DictionaryEntry kv in parts)
        {
            var name = kv.Key.ToString()!;
            groupOf[name] = partGroups != null && partGroups.Contains(name) ? partGroups[name]?.ToString() ?? PartGroup(name) : PartGroup(name);
        }

        var blockers = new List<(string Name, string Group, CanvasPath Path)>();
        foreach (DictionaryEntry kv in parts)
            if (kv.Value is CanvasPath p) blockers.Add((kv.Key.ToString()!, groupOf[kv.Key.ToString()!], p));
        if (opt != null && opt.Contains("obstacles") && opt["obstacles"] is { } obstacles)
            foreach (var (name, path) in ReadNamedPaths(obstacles, "checkLookPath"))
            {
                if (IsOpen(path.Path, out _))
                    throw new ArgumentException($"checkLookPath: obstacle '{name}' is a line, and a line cannot block a look. Give it a width with ctx.strokeToPath(...) if it should.");
                blockers.Add((name, "obstacle", path));
            }

        // What does not count: the head and neck always, and whatever the caller names.
        var ignored = new HashSet<string>(["head", "neck"], StringComparer.Ordinal);
        if (opt != null && opt.Contains("ignore") && opt["ignore"] is { } ignoreObj)
        {
            var names = ignoreObj is string one ? [one] : ignoreObj is IEnumerable many ? many.Cast<object?>().Select(o => o?.ToString() ?? "").ToList() : [];
            var known = blockers.Select(b => b.Name).Concat(blockers.Select(b => b.Group)).ToHashSet(StringComparer.Ordinal);
            foreach (var name in names)
                ignored.Add(known.Contains(name) ? name
                    : throw new ArgumentException($"checkLookPath: nothing called '{name}' to ignore. Parts and groups here: {string.Join(", ", known.Where(k => k != "obstacle").Order())}."));
        }
        blockers.RemoveAll(b => ignored.Contains(b.Name) || ignored.Contains(b.Group));

        var head = JsInterop.AsDict(fig["head"]);
        var from = opt != null && opt.Contains("from") ? ExtractPoint(opt["from"])
            : head != null && head.Contains("center") ? ExtractPoint(head["center"])
            : parts["head"] is CanvasPath hp ? new Point2D(hp.Path.Bounds.MidX, hp.Path.Bounds.MidY)
            : throw new ArgumentException("checkLookPath: the figure has no head to look from; pass options.from.");
        var to = ExtractPoint(target);
        float dx = to.X - from.X, dy = to.Y - from.Y, length = MathF.Sqrt(dx * dx + dy * dy);
        if (length <= 0f) throw new ArgumentException("checkLookPath: the target is where the look starts.");

        var step = opt != null && opt.Contains("step") ? MathF.Max(0.25f, Num(opt, "step", 1f)) : MathF.Max(0.5f, length / 500f);
        var n = Math.Max(2, (int)MathF.Ceiling(length / step));
        string? Blocker(float t)
        {
            float x = from.X + dx * t, y = from.Y + dy * t;
            foreach (var b in blockers) if (b.Path.Path.Contains(x, y)) return b.Name;
            return null;
        }

        // Runs of the path inside a blocker, in order along it.
        var hits = new List<object?>();
        float blockedLength = 0f;
        string? current = null;
        var runStart = 0;
        void Close(int end)
        {
            if (current == null) return;
            float t0 = (float)runStart / n, t1 = (float)end / n;
            var a = new Point2D(from.X + dx * t0, from.Y + dy * t0);
            var b = new Point2D(from.X + dx * t1, from.Y + dy * t1);
            var mark = new CanvasPath();
            mark.MoveTo(a.X, a.Y);
            mark.LineTo(b.X, b.Y);
            blockedLength += (t1 - t0) * length;
            var group = blockers.First(x => x.Name == current).Group;
            hits.Add(new Dictionary<string, object?>
            {
                ["by"] = current,
                ["group"] = group,
                ["from"] = ToDict(a),
                ["to"] = ToDict(b),
                ["at"] = t0,
                ["length"] = (t1 - t0) * length,
                ["mark"] = mark
            });
            current = null;
        }
        for (var i = 0; i <= n; i++)
        {
            var by = Blocker((float)i / n);
            if (by == current) continue;
            Close(i);
            if (by != null) { current = by; runStart = i; }
        }
        Close(n);

        var sight = new CanvasPath();
        sight.MoveTo(from.X, from.Y);
        sight.LineTo(to.X, to.Y);
        return new Dictionary<string, object?>
        {
            ["blocked"] = hits.Count > 0,
            ["firstHit"] = hits.Count > 0 ? hits[0] : null,
            ["hits"] = hits,
            ["clear"] = 1f - blockedLength / length,
            ["from"] = ToDict(from),
            ["to"] = ToDict(to),
            ["length"] = length,
            ["ignored"] = ignored.Order().Cast<object?>().ToList(),
            ["samples"] = n + 1,
            ["mark"] = sight
        };
    }
    #endregion

    #region Line Families
    static readonly string[] LineFamilyOptions = ["tolerance"];

    /// <summary>The moods of Stanchfield's <i>Symbols for Poses</i> (ch. 42) that a set of lines can show, in our words.</summary>
    static readonly (string Family, string Feeling)[] LineMoods =
    [
        ("horizontals", "rest, calm, finality"),
        ("verticals", "dignity, height, austerity"),
        ("vertical against horizontal", "solidity, stubbornness"),
        ("conflicting diagonals", "conflict, disturbance"),
        ("unsupported diagonal", "movement across or into the space"),
        ("zigzag", "excitement, vibration"),
        ("wave", "grace and rhythm; at its extreme, turbulence"),
        ("spiral", "great force, awe")
    ];

    /// <summary>
    /// Sorts lines into the families of Stanchfield's <i>Symbols for Poses</i> (<i>Drawn to Life</i> vol. 1 ch. 42;
    /// Manual 28) and names the mood the set adds up to, weighted by length.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each line is straight (horizontal, vertical or diagonal, the diagonal leaning rising or falling), a zigzag
    /// (sharp corners turning back and forth), a wave (a smooth line bending both ways), a spiral (turning through a
    /// full circle), or a curve (bending one way). A line with one sharp corner is split there into its pieces.
    /// </para>
    /// <para>
    /// The moods are the families of ch. 42 that a direction can show. Flame shapes, spheres, the Gothic arch, the
    /// fountain, the cascade and the grief line are shapes rather than directions and are not measured. The
    /// thresholds are the studio's, not Stanchfield's, which is why every share is reported.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> ClassifyLines(object linesObj, object? options = null)
    {
        var opt = JsInterop.AsDict(options);
        if (opt != null)
            foreach (var key in opt.Keys)
                if (Array.IndexOf(LineFamilyOptions, key?.ToString()) < 0)
                    throw new ArgumentException($"classifyLines has no option '{key}'. It takes {string.Join(", ", LineFamilyOptions)}.");
        var tolerance = Math.Clamp(Num(opt, "tolerance", 15f), 1f, 44f);

        var records = new List<LineRecord>();
        foreach (var (name, path) in ReadNamedPaths(linesObj, "classifyLines"))
        {
            if (!IsOpen(path.Path, out _))
                throw new ArgumentException($"classifyLines: '{name}' is a closed shape. Pass its edges as lines; a figure's line of action is fig.lineOfAction.d.");
            var pieces = new List<LineRecord>();
            using var measure = new SKPathMeasure(path.Path, false);
            do
            {
                if (measure.Length > 0f) pieces.AddRange(ClassifyContour(measure, tolerance));
            }
            while (measure.NextContour());
            for (var k = 0; k < pieces.Count; k++) records.Add(pieces[k] with { Name = pieces.Count > 1 ? $"{name}.{k}" : name });
        }
        if (records.Count == 0) throw new ArgumentException("classifyLines needs at least one line with some length.");

        // Shares of the total length, so a long horizon outweighs a short tick.
        var total = records.Sum(r => r.Length);
        float Share(Func<LineRecord, bool> which) => records.Where(which).Sum(r => r.Length) / total;
        var shares = new Dictionary<string, object?>
        {
            ["horizontal"] = Share(r => r.Family == "horizontal"),
            ["vertical"] = Share(r => r.Family == "vertical"),
            ["diagonal"] = Share(r => r.Family == "diagonal"),
            ["rising"] = Share(r => r.Family == "diagonal" && r.Lean == "rising"),
            ["falling"] = Share(r => r.Family == "diagonal" && r.Lean == "falling"),
            ["zigzag"] = Share(r => r.Family == "zigzag"),
            ["wave"] = Share(r => r.Family == "wave"),
            ["spiral"] = Share(r => r.Family == "spiral"),
            ["curve"] = Share(r => r.Family == "curve")
        };
        float S(string key) => (float)shares[key]!;

        // Each mood the set qualifies for, with how strongly. A set can carry more than one.
        var moods = new List<(string Family, float Strength)>();
        if (S("horizontal") >= 0.5f) moods.Add(("horizontals", S("horizontal")));
        if (S("vertical") >= 0.5f) moods.Add(("verticals", S("vertical")));
        if (S("horizontal") >= 0.25f && S("vertical") >= 0.25f) moods.Add(("vertical against horizontal", S("horizontal") + S("vertical")));
        if (S("diagonal") >= 0.5f)
        {
            var minority = MathF.Min(S("rising"), S("falling")) / S("diagonal");
            if (minority >= 0.25f) moods.Add(("conflicting diagonals", S("diagonal")));
            else if (minority <= 0.1f) moods.Add(("unsupported diagonal", S("diagonal")));
        }
        if (S("zigzag") >= 0.3f) moods.Add(("zigzag", S("zigzag")));
        if (S("wave") >= 0.3f) moods.Add(("wave", S("wave")));
        if (S("spiral") >= 0.2f) moods.Add(("spiral", S("spiral")));
        var ranked = moods.OrderByDescending(m => m.Strength).Select(m => (object?)new Dictionary<string, object?>
        {
            ["family"] = m.Family,
            ["feeling"] = LineMoods.First(x => x.Family == m.Family).Feeling,
            ["share"] = MathF.Min(1f, m.Strength)
        }).ToList();

        return new Dictionary<string, object?>
        {
            ["lines"] = records.Select(r => (object?)new Dictionary<string, object?>
            {
                ["name"] = r.Name,
                ["family"] = r.Family,
                ["angleDeg"] = r.AngleDeg,
                ["lean"] = r.Lean,
                ["length"] = r.Length,
                ["turnDeg"] = r.TurnDeg
            }).ToList(),
            ["shares"] = shares,
            ["counts"] = records.GroupBy(r => r.Family).ToDictionary(g => g.Key, g => (object?)g.Count()),
            ["mood"] = ranked.Count > 0 ? ranked[0] : new Dictionary<string, object?> { ["family"] = "mixed", ["feeling"] = "no one family dominates", ["share"] = 0f },
            ["moods"] = ranked,
            ["notMeasured"] = new List<object?> { "flame shapes", "pointed shapes", "spheres", "the Gothic arch", "a fountain", "a cascade", "the grief line" },
            ["tolerance"] = tolerance
        };
    }

    sealed record LineRecord(string Name, string Family, float AngleDeg, string? Lean, float Length, float TurnDeg);

    /// <summary>
    /// One contour's family. A smooth line is classified whole; one with a single sharp corner is split at it, since an
    /// L is a horizontal and a vertical rather than a curve.
    /// </summary>
    static List<LineRecord> ClassifyContour(SKPathMeasure measure, float tolerance)
    {
        var length = measure.Length;
        var n = Math.Max(16, (int)MathF.Ceiling(length / MathF.Max(0.5f, length / 200f)));
        var pts = new List<SKPoint>(n + 1);
        for (var i = 0; i <= n; i++)
            if (measure.GetPosition(length * i / n, out var p) && (pts.Count == 0 || SKPoint.Distance(p, pts[^1]) > 1e-4f)) pts.Add(p);
        if (pts.Count < 2) return [];

        // Sharp corners are found on the fine samples, where a corner is a large turn over two or three steps. Bends
        // are found on a coarse resampling of about 24 steps, where even a gentle curve turns measurably per step.
        var (events, total, net) = Turns(pts, 0.5f);
        var stride = Math.Max(1, (pts.Count - 1) / 24);
        var coarse = pts.Where((_, i) => i % stride == 0).ToList();
        if (coarse[^1] != pts[^1]) coarse.Add(pts[^1]);
        var sharp = events.Where(e => MathF.Abs(e.Sum) >= 30f && e.Count <= 3).ToList();
        var bends = Turns(coarse, 1f).Events.Where(e => MathF.Abs(e.Sum) >= 10f).ToList();
        static bool Alternates(List<(float Sum, int Count, int At)> list) =>
            list.Zip(list.Skip(1)).Any(p => MathF.Sign(p.First.Sum) != MathF.Sign(p.Second.Sum));

        LineRecord Whole(string family) => new("", family, ChordAngle(pts[0], pts[^1]), Lean(pts[0], pts[^1], family), length, total);

        if (MathF.Abs(net) >= 330f) return [Whole("spiral")];
        if (sharp.Count >= 2 && Alternates(sharp)) return [Whole("zigzag")];
        if (sharp.Count >= 1)
        {
            // Split at the sharp corners into pieces, each classified as a straight line or a curve. The step that
            // straddles a corner belongs to neither piece: left on one, it reads as a second corner at its end.
            var spans = new List<(int From, int To)>();
            var from = 0;
            foreach (var e in sharp)
            {
                spans.Add((from, Math.Min(pts.Count - 1, e.At)));
                from = Math.Min(pts.Count - 1, e.At + e.Count - 1);
            }
            spans.Add((from, pts.Count - 1));
            var pieces = new List<LineRecord>();
            foreach (var (start, end) in spans)
            {
                if (end <= start) continue;
                var segment = pts.GetRange(start, end - start + 1);
                if (segment.Count < 2) continue;
                using var piece = new SKPath();
                piece.MoveTo(segment[0]);
                foreach (var p in segment.Skip(1)) piece.LineTo(p);
                using var pm = new SKPathMeasure(piece, false);
                if (pm.Length > 0f) pieces.AddRange(ClassifyContour(pm, tolerance));
            }
            return pieces;
        }
        if (total < 20f) return [Straight(pts[0], pts[^1], length, total, tolerance)];
        return [Whole(bends.Count >= 2 && Alternates(bends) ? "wave" : "curve")];
    }

    /// <summary>
    /// The signed turn between successive steps of a polyline, gathered into events of one sign; and the total and
    /// net turning. Turns smaller than <paramref name="threshold"/> degrees are noise and start no event.
    /// </summary>
    static (List<(float Sum, int Count, int At)> Events, float Total, float Net) Turns(List<SKPoint> pts, float threshold)
    {
        var dirs = new List<float>();
        for (var i = 1; i < pts.Count; i++) dirs.Add(MathF.Atan2(pts[i].Y - pts[i - 1].Y, pts[i].X - pts[i - 1].X) * 180f / MathF.PI);
        var events = new List<(float Sum, int Count, int At)>();
        float total = 0f, net = 0f;
        for (var i = 1; i < dirs.Count; i++)
        {
            var d = ((dirs[i] - dirs[i - 1] + 540f) % 360f) - 180f;
            total += MathF.Abs(d);
            net += d;
            if (MathF.Abs(d) < threshold) continue;
            if (events.Count > 0 && MathF.Sign(events[^1].Sum) == MathF.Sign(d) && events[^1].At + events[^1].Count >= i - 1)
                events[^1] = (events[^1].Sum + d, events[^1].Count + 1, events[^1].At);
            else events.Add((d, 1, i));
        }
        return (events, total, net);
    }

    static LineRecord Straight(SKPoint a, SKPoint b, float length, float turn, float tolerance)
    {
        var angle = ChordAngle(a, b);
        var family = MathF.Abs(angle) <= tolerance ? "horizontal" : MathF.Abs(angle) >= 90f - tolerance ? "vertical" : "diagonal";
        return new LineRecord("", family, angle, Lean(a, b, family), length, turn);
    }

    /// <summary>The chord's angle from horizontal, -90 to 90, positive rising to the right on the page.</summary>
    static float ChordAngle(SKPoint a, SKPoint b)
    {
        var angle = MathF.Atan2(-(b.Y - a.Y), b.X - a.X) * 180f / MathF.PI;
        angle = ((angle % 180f) + 180f) % 180f;
        return angle > 90f ? angle - 180f : angle;
    }

    static string? Lean(SKPoint a, SKPoint b, string family) =>
        family is "horizontal" or "vertical" ? null : ChordAngle(a, b) >= 0f ? "rising" : "falling";
    #endregion
}
