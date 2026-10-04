namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

using SkiaSharp;

/// <summary>
/// An ordered stack of layers, bottom first: what a composition and a group both are.
/// </summary>
/// <remarks>
/// <b>Spike.</b> Synfig's layer model, for the four layers the spike covers plus a solid fill. Each
/// option is a value or a <see cref="MotionNode"/>, so any parameter can be keyed, computed or shared.
/// Coordinates are the composition's view-box units, pixels with y down unless the composition says
/// otherwise; angles are degrees, clockwise on screen when y is down. An option the layer does not have
/// is refused by name.
/// <para>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS camelCase
/// spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>. The camelCase
/// form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// </para>
/// </remarks>
public abstract class MotionLayerList
{
    #region Fields
    private protected readonly List<MotionLayer> layers = [];
    #endregion

    #region Properties
    /// <summary>How many layers it holds.</summary>
    public int LayerCount => layers.Count;
    #endregion

    #region Methods
    /// <summary>A flat fill over the whole frame: <c>{ color, amount?, desc? }</c>.</summary>
    public MotionLayerList Fill(object? options)
    {
        var o = new MotionOptions(options, "fill", "color", "amount", "desc");
        layers.Add(new MotionLayer.SolidColor(o));
        return this;
    }

    /// <summary>A disc: <c>{ origin, radius, color, amount?, desc? }</c>.</summary>
    public MotionLayerList Circle(object? options)
    {
        var o = new MotionOptions(options, "circle", "origin", "radius", "color", "amount", "desc");
        layers.Add(new MotionLayer.CircleLayer(o));
        return this;
    }

    /// <summary>A filled spline: <c>{ points, loop?, origin?, color, amount?, desc? }</c>.</summary>
    public MotionLayerList Region(object? options)
    {
        var o = new MotionOptions(options, "region", "points", "loop", "origin", "color", "amount", "desc");
        layers.Add(new MotionLayer.RegionLayer(o));
        return this;
    }

    /// <summary>
    /// A stroked spline whose width can vary along it:
    /// <c>{ points, loop?, origin?, width?, color, sharpCusps?, roundTips?, amount?, desc? }</c>.
    /// </summary>
    public MotionLayerList Outline(object? options)
    {
        var o = new MotionOptions(options, "outline",
            "points", "loop", "origin", "width", "color", "sharpCusps", "roundTips", "amount", "desc");
        layers.Add(new MotionLayer.OutlineLayer(o));
        return this;
    }

    /// <summary>
    /// A group with its own transformation and clock, returned so layers can be added to it:
    /// <c>{ origin?, offset?, angle?, skewAngle?, scale?, amount?, timeOffset?, timeDilation?, desc? }</c>.
    /// </summary>
    public MotionGroup Group(object? options = null)
    {
        var o = new MotionOptions(options, "group",
            "origin", "offset", "angle", "skewAngle", "scale", "amount", "timeOffset", "timeDilation", "desc");
        var group = new MotionGroup(o);
        layers.Add(group.Layer);
        return group;
    }

    internal void RenderLayers(SKCanvas canvas, double time)
    {
        foreach (var layer in layers) layer.Render(canvas, time);
    }

    internal IEnumerable<XElement> LayersToSif(SifWriter sif) => layers.Select(l => l.ToSif(sif));

    internal IEnumerable<MotionNode> Nodes() => layers.SelectMany(l => l.Nodes());
    #endregion
}

/// <summary>A group of layers under one transformation: what <c>composition.group(...)</c> returns.</summary>
/// <remarks>
/// Synfig's group (paste canvas). Its transformation is <c>offset · rotate(angle) · skew · scale</c>
/// about <c>origin</c>, and its children see time as <c>t × timeDilation + timeOffset</c> — which is
/// how one animation is reused at another moment or speed. Exposed to the JavaScript sandbox. See
/// <see cref="MotionLayerList"/>.
/// </remarks>
public sealed class MotionGroup : MotionLayerList
{
    #region Constructors
    internal MotionGroup(MotionOptions o) => Layer = new MotionLayer.GroupLayer(o, this);
    #endregion

    #region Properties
    internal MotionLayer.GroupLayer Layer { get; }
    #endregion
}

/// <summary>
/// A whole animation as a graph of layers and value nodes: <c>Motion.composition(...)</c>.
/// </summary>
/// <remarks>
/// <b>Spike.</b> Synfig's animation model with Skia drawing it: every parameter is a function of time,
/// so any frame is rendered directly, in any order, and the same graph is written out as a <c>.sif</c>
/// file Synfig's own renderer reads. That second path is the check on the first, not the delivery
/// route. Exposed to the JavaScript sandbox. See <see cref="MotionLayerList"/>.
/// </remarks>
public sealed class MotionComposition : MotionLayerList
{
    #region Constructors
    internal MotionComposition(object? options, MotionToolkit? motion, string? projectRoot)
    {
        this.motion = motion;
        this.projectRoot = projectRoot;

        var o = new MotionOptions(options, "Motion.composition", "width", "height", "fps", "duration", "view");
        Width = (int)o.Number("width", 480);
        Height = (int)o.Number("height", 270);
        Fps = o.Number("fps", 24);
        Duration = o.Number("duration", 2);

        if (Width < 1 || Height < 1 || Width > 8192 || Height > 8192)
            throw new ArgumentException($"Motion.composition: {Width}x{Height} is not a size between 1 and 8192.");
        if (Fps <= 0 || Fps > 240) throw new ArgumentException($"Motion.composition: fps {Fps} is not between 0 and 240.");
        if (Duration < 0) throw new ArgumentException("Motion.composition: duration cannot be negative.");

        view = o.Has("view")
            ? (o.Raw("view") as IList is { Count: 4 } v
                ? [.. v.Cast<object?>().Select((x, i) => MotionTypes.Read(x, MotionType.Real, $"view[{i}]")[0])]
                : throw new ArgumentException("Motion.composition: view is [left, top, right, bottom] in the units layers use."))
            : [0, 0, Width, Height];

        if (view[2] == view[0] || view[3] == view[1])
            throw new ArgumentException("Motion.composition: view has no width or no height.");
    }
    #endregion

    #region Fields
    private readonly MotionToolkit? motion;
    private readonly string? projectRoot;
    private readonly double[] view;
    #endregion

    #region Properties
    public int Width { get; }

    public int Height { get; }

    /// <summary>Frames per second.</summary>
    public double Fps { get; }

    /// <summary>Length in seconds.</summary>
    public double Duration { get; }

    /// <summary>How many frames <see cref="Capture"/> renders: one per frame, both ends included.</summary>
    public int FrameCount => (int)Math.Floor(Duration * Fps + 1e-9) + 1;
    #endregion

    #region Methods
    /// <summary>The composition at a time in seconds, on a new canvas.</summary>
    public SkiaCanvas Render(double time)
    {
        var canvas = new SkiaCanvas(Width, Height);
        canvas.SkCanvas.Clear(SKColors.Transparent);
        canvas.SkCanvas.Save();
        canvas.SkCanvas.Concat(ViewMatrix());
        RenderLayers(canvas.SkCanvas, time);
        canvas.SkCanvas.Restore();
        return canvas;
    }

    /// <summary>
    /// Draws the composition at a time onto an existing context, under its current transform, so an
    /// animated passage sits inside a drawing made with the rest of the SDK.
    /// </summary>
    public void Draw(CanvasRenderingContext2D ctx, double time)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var c = ctx.Canvas.SkCanvas;
        c.Save();
        c.Concat(ViewMatrix());
        RenderLayers(c, time);
        c.Restore();
    }

    /// <summary>
    /// Renders every frame into <c>Motion</c>'s frame buffer, ready for <c>Motion.sheet</c> or
    /// <c>Motion.save</c>. Returns how many it added. <c>options</c>: <c>{ fps?, from?, to? }</c>, in
    /// seconds; a lower <c>fps</c> samples the same timeline more sparsely.
    /// </summary>
    public int Capture(object? options = null)
    {
        if (motion is null) throw new InvalidOperationException("This composition has no Motion frame buffer to capture into.");
        var o = new MotionOptions(options, "composition.capture", "fps", "from", "to");
        var fps = o.Number("fps", Fps);
        var from = o.Number("from", 0);
        var to = o.Number("to", Duration);
        if (fps <= 0) throw new ArgumentException("composition.capture: fps must be positive.");

        var count = 0;
        for (var i = 0; ; i++)
        {
            var t = from + i / fps;
            if (t > to + 1e-9) break;
            using var frame = Render(t);
            motion.Frame(frame);
            count++;
        }

        return count;
    }

    /// <summary>The composition as Synfig's <c>.sif</c> XML.</summary>
    public string ToSif() => new SifWriter(this).Write();

    /// <summary>Writes the <c>.sif</c> into the project and returns the path written.</summary>
    public string SaveSif(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var full = ProjectPath.Resolve(projectRoot, filePath, nameof(filePath), "Write to");
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, ToSif(), new UTF8Encoding(false));
        return full;
    }

    internal double[] View => view;

    private SKMatrix ViewMatrix()
    {
        var sx = Width / (view[2] - view[0]);
        var sy = Height / (view[3] - view[1]);
        return new SKMatrix((float)sx, 0, (float)(-view[0] * sx), 0, (float)sy, (float)(-view[1] * sy), 0, 0, 1);
    }
    #endregion
}

/// <summary>One layer of a composition, its parameters held as nodes.</summary>
internal abstract class MotionLayer
{
    #region Constructors
    protected MotionLayer(MotionOptions o)
    {
        Desc = o.Has("desc") ? o.Raw("desc")?.ToString() : null;
        Amount = o.Node("amount", MotionType.Real, 1d);
    }
    #endregion

    #region Properties
    public string? Desc { get; }

    public MotionNode Amount { get; }
    #endregion

    #region Methods
    public abstract void Render(SKCanvas canvas, double time);

    public abstract XElement ToSif(SifWriter sif);

    public abstract IEnumerable<MotionNode> Nodes();

    protected XElement Element(string type, string version, SifWriter sif, params object?[] content) =>
        new("layer", new XAttribute("type", type), new XAttribute("active", "true"), new XAttribute("version", version),
            Desc is null ? null : new XAttribute("desc", Desc),
            SifWriter.Param("z_depth", new XElement("real", new XAttribute("value", "0"))),
            sif.Param("amount", Amount),
            SifWriter.Param("blend_method", new XElement("integer", new XAttribute("value", "0"))),
            content);

    protected double AmountAt(double time) => Math.Clamp(Amount.Evaluate(time)[0], 0, 1);

    protected static SKPaint Paint(double[] color, double amount) =>
        new() { Color = MotionTypes.ToSkColor(color, amount), IsAntialias = true, Style = SKPaintStyle.Fill };
    #endregion

    #region Types
    public sealed class SolidColor(MotionOptions o) : MotionLayer(o)
    {
        private readonly MotionNode color = o.Node("color", MotionType.Color, "#000000");

        public override void Render(SKCanvas canvas, double time)
        {
            using var paint = Paint(color.Evaluate(time), AmountAt(time));
            canvas.DrawPaint(paint);
        }

        public override XElement ToSif(SifWriter sif) => Element("solid_color", "0.1", sif, sif.Param("color", color));

        public override IEnumerable<MotionNode> Nodes() => [Amount, color];
    }

    public sealed class CircleLayer(MotionOptions o) : MotionLayer(o)
    {
        private readonly MotionNode origin = o.Node("origin", MotionType.Vector, new[] { 0d, 0d });
        private readonly MotionNode radius = o.Node("radius", MotionType.Real, 10d);
        private readonly MotionNode color = o.Node("color", MotionType.Color, "#000000");

        public override void Render(SKCanvas canvas, double time)
        {
            var p = origin.Evaluate(time);
            using var paint = Paint(color.Evaluate(time), AmountAt(time));
            canvas.DrawCircle((float)p[0], (float)p[1], (float)Math.Abs(radius.Evaluate(time)[0]), paint);
        }

        public override XElement ToSif(SifWriter sif) => Element("circle", "0.2", sif,
            sif.Param("color", color), sif.Param("radius", radius),
            SifWriter.Param("feather", new XElement("real", new XAttribute("value", "0"))),
            sif.Param("origin", origin),
            SifWriter.Param("invert", SifWriter.Bool(false)));

        public override IEnumerable<MotionNode> Nodes() => [Amount, origin, radius, color];
    }

    /// <summary>What region and outline share: a spline of points, possibly closed, moved by <c>origin</c>.</summary>
    public abstract class Shape : MotionLayer
    {
        protected Shape(MotionOptions o, string who, bool loopByDefault) : base(o)
        {
            Origin = o.Node("origin", MotionType.Vector, new[] { 0d, 0d });
            Color = o.Node("color", MotionType.Color, "#000000");
            Loop = o.Bool("loop", loopByDefault);
            Points = MotionSplinePoint.ReadAll(o.Raw("points"), who);
        }

        protected MotionNode Origin { get; }

        protected MotionNode Color { get; }

        protected bool Loop { get; }

        protected IReadOnlyList<MotionSplinePoint> Points { get; }

        public override IEnumerable<MotionNode> Nodes() =>
            new[] { Amount, Origin, Color }.Concat(Points.SelectMany(p => p.Nodes()));

        protected XElement[] ShapeParams(SifWriter sif) =>
        [
            sif.Param("color", Color),
            sif.Param("origin", Origin),
            SifWriter.Param("invert", SifWriter.Bool(false)),
            SifWriter.Param("antialias", SifWriter.Bool(true)),
            SifWriter.Param("feather", new XElement("real", new XAttribute("value", "0"))),
            SifWriter.Param("blurtype", new XElement("integer", new XAttribute("value", "1"))),
            SifWriter.Param("winding_style", new XElement("integer", new XAttribute("value", "0"))),
            SifWriter.Param("bline", new XElement("bline", new XAttribute("type", "bline_point"),
                new XAttribute("loop", Loop ? "true" : "false"),
                Points.Select(p => new XElement("entry", p.ToSif(sif)))))
        ];

        /// <summary>The spline at a time, translated by origin, as Synfig's region builds it.</summary>
        protected SKPath Path(double time, out MotionSplinePoint.State[] states)
        {
            var o = Origin.Evaluate(time);
            states = [.. Points.Select(p => p.At(time, o))];
            var path = new SKPath();
            if (states.Length == 0) return path;

            path.MoveTo(states[0].P);
            for (var i = 1; i < states.Length; i++) Segment(path, states[i - 1], states[i]);
            if (Loop)
            {
                Segment(path, states[^1], states[0]);
                path.Close();
            }

            return path;
        }

        protected static void Segment(SKPath path, MotionSplinePoint.State a, MotionSplinePoint.State b)
        {
            if (a.T2 == SKPoint.Empty && b.T1 == SKPoint.Empty) path.LineTo(b.P);
            else path.CubicTo(a.P + Third(a.T2), b.P - Third(b.T1), b.P);
        }

        protected static SKPoint Third(SKPoint t) => new(t.X / 3f, t.Y / 3f);
    }

    public sealed class RegionLayer(MotionOptions o) : Shape(o, "region", true)
    {
        public override void Render(SKCanvas canvas, double time)
        {
            using var path = Path(time, out _);
            using var paint = Paint(Color.Evaluate(time), AmountAt(time));
            canvas.DrawPath(path, paint);
        }

        public override XElement ToSif(SifWriter sif) => Element("region", "0.1", sif, ShapeParams(sif));
    }

    public sealed class OutlineLayer : Shape
    {
        public OutlineLayer(MotionOptions o) : base(o, "outline", false)
        {
            width = o.Node("width", MotionType.Real, 1d);
            sharpCusps = o.Bool("sharpCusps", true);
            roundTips = o.Bool("roundTips", true);
        }

        private readonly MotionNode width;
        private readonly bool sharpCusps, roundTips;

        public override IEnumerable<MotionNode> Nodes() => base.Nodes().Append(width);

        public override XElement ToSif(SifWriter sif) => Element("outline", "0.3", sif,
            ShapeParams(sif),
            sif.Param("width", width),
            SifWriter.Param("expand", new XElement("real", new XAttribute("value", "0"))),
            SifWriter.Param("sharp_cusps", SifWriter.Bool(sharpCusps)),
            SifWriter.Param("round_tip[0]", SifWriter.Bool(roundTips)),
            SifWriter.Param("round_tip[1]", SifWriter.Bool(roundTips)),
            SifWriter.Param("homogeneous_width", SifWriter.Bool(true)));

        public override void Render(SKCanvas canvas, double time)
        {
            using var path = Path(time, out var states);
            if (states.Length == 0) return;

            var w = width.Evaluate(time)[0];
            using var paint = Paint(Color.Evaluate(time), AmountAt(time));

            if (states.All(s => Math.Abs(s.Width - states[0].Width) < 1e-9))
            {
                paint.Style = SKPaintStyle.Stroke;
                paint.StrokeWidth = (float)Math.Abs(w * states[0].Width);
                paint.StrokeCap = roundTips ? SKStrokeCap.Round : SKStrokeCap.Butt;
                paint.StrokeJoin = sharpCusps ? SKStrokeJoin.Miter : SKStrokeJoin.Round;
                canvas.DrawPath(path, paint);
                return;
            }

            using var shape = Tapered(states, w);
            canvas.DrawPath(shape, paint);
        }

        /// <summary>
        /// A stroke whose half-width runs linearly in arc length from one point's width to the next,
        /// as Synfig's homogeneous width does, with round joins and tips.
        /// </summary>
        private SKPath Tapered(MotionSplinePoint.State[] s, double width)
        {
            const int samples = 32;
            var left = new List<SKPoint>();
            var right = new List<SKPoint>();
            var result = new SKPath();
            var count = Loop ? s.Length : s.Length - 1;

            for (var i = 0; i < count; i++)
            {
                var a = s[i];
                var b = s[(i + 1) % s.Length];
                using var seg = new SKPath();
                seg.MoveTo(a.P);
                Segment(seg, a, b);
                using var measure = new SKPathMeasure(seg);
                var length = measure.Length;

                for (var k = (i == 0 ? 0 : 1); k <= samples; k++)
                {
                    var d = length * k / samples;
                    if (!measure.GetPositionAndTangent(d, out var pos, out var tan)) continue;
                    var half = (float)(Math.Abs(width) * (a.Width + (b.Width - a.Width) * k / samples) / 2);
                    var n = new SKPoint(-tan.Y, tan.X);
                    left.Add(pos + new SKPoint(n.X * half, n.Y * half));
                    right.Add(pos - new SKPoint(n.X * half, n.Y * half));
                }

                // Round joins and tips: a disc at every point covers the corner between two segments.
                AddDisc(result, a, width);
            }

            if (!Loop || s.Length == 1) AddDisc(result, s[Loop ? 0 : ^1], width);

            if (left.Count > 1)
            {
                using var body = new SKPath();
                body.MoveTo(left[0]);
                foreach (var p in left.Skip(1)) body.LineTo(p);
                for (var k = right.Count - 1; k >= 0; k--) body.LineTo(right[k]);
                body.Close();
                using var simple = body.Simplify() ?? new SKPath(body);
                using var merged = result.Op(simple, SKPathOp.Union) ?? new SKPath(result);
                return new SKPath(merged);
            }

            return result;

            void AddDisc(SKPath into, MotionSplinePoint.State p, double w)
            {
                if (!roundTips && !Loop && (p.Equals(s[0]) || p.Equals(s[^1]))) return;
                var r = (float)(Math.Abs(w) * p.Width / 2);
                if (r <= 0) return;
                using var disc = new SKPath();
                disc.AddCircle(p.P.X, p.P.Y, r);
                using var merged = into.Op(disc, SKPathOp.Union);
                if (merged is not null) { into.Reset(); into.AddPath(merged); }
            }
        }
    }

    public sealed class GroupLayer : MotionLayer
    {
        public GroupLayer(MotionOptions o, MotionGroup children) : base(o)
        {
            this.children = children;
            origin = o.Node("origin", MotionType.Vector, new[] { 0d, 0d });
            offset = o.Node("offset", MotionType.Vector, new[] { 0d, 0d });
            angle = o.Node("angle", MotionType.Angle, 0d);
            skewAngle = o.Node("skewAngle", MotionType.Angle, 0d);
            scale = o.Node("scale", MotionType.Vector, new[] { 1d, 1d });
            timeOffset = o.Number("timeOffset", 0);
            timeDilation = o.Number("timeDilation", 1);
        }

        private readonly MotionGroup children;
        private readonly MotionNode origin, offset, angle, skewAngle, scale;
        private readonly double timeOffset, timeDilation;

        public override IEnumerable<MotionNode> Nodes() =>
            new[] { Amount, origin, offset, angle, skewAngle, scale }.Concat(children.Nodes());

        public override void Render(SKCanvas canvas, double time)
        {
            var amount = AmountAt(time);
            if (amount <= 0) return;

            canvas.Save();
            canvas.Concat(Matrix(time));
            if (amount < 1)
            {
                using var fade = new SKPaint { Color = new SKColor(255, 255, 255, (byte)Math.Round(amount * 255)) };
                canvas.SaveLayer(fade);
            }

            children.RenderLayers(canvas, time * timeDilation + timeOffset);
            if (amount < 1) canvas.Restore();
            canvas.Restore();
        }

        /// <summary>Synfig's summary transformation: <c>offset · axes(scale, angle, skew) · translate(−origin)</c>.</summary>
        private SKMatrix Matrix(double time)
        {
            var o = origin.Evaluate(time);
            var off = offset.Evaluate(time);
            var a = angle.Evaluate(time)[0] * Math.PI / 180;
            var sk = skewAngle.Evaluate(time)[0] * Math.PI / 180;
            var sc = scale.Evaluate(time);

            var ax = (X: sc[0] * Math.Cos(a), Y: sc[0] * Math.Sin(a));
            var ay = (X: sc[1] * Math.Cos(a + sk + Math.PI / 2), Y: sc[1] * Math.Sin(a + sk + Math.PI / 2));
            var axes = new SKMatrix((float)ax.X, (float)ay.X, (float)off[0], (float)ax.Y, (float)ay.Y, (float)off[1], 0, 0, 1);
            return SKMatrix.Concat(axes, SKMatrix.CreateTranslation((float)-o[0], (float)-o[1]));
        }

        public override XElement ToSif(SifWriter sif) => Element("group", "0.3", sif,
            sif.Param("origin", origin),
            SifWriter.Param("transformation", new XElement("composite", new XAttribute("type", "transformation"),
                sif.Link("offset", offset), sif.Link("angle", angle), sif.Link("skew_angle", skewAngle), sif.Link("scale", scale))),
            SifWriter.Param("canvas", new XElement("canvas", children.LayersToSif(sif))),
            SifWriter.Param("time_dilation", new XElement("real", new XAttribute("value", MotionTypes.Format(timeDilation)))),
            SifWriter.Param("time_offset", new XElement("time", new XAttribute("value", MotionTypes.Time(timeOffset)))),
            SifWriter.Param("children_lock", SifWriter.Bool(false)),
            SifWriter.Param("outline_grow", new XElement("real", new XAttribute("value", "0"))),
            SifWriter.Param("z_range", SifWriter.Bool(false)),
            SifWriter.Param("z_range_position", new XElement("real", new XAttribute("value", "0"))),
            SifWriter.Param("z_range_depth", new XElement("real", new XAttribute("value", "0"))),
            SifWriter.Param("z_range_blur", new XElement("real", new XAttribute("value", "0"))));
    }
    #endregion
}

/// <summary>
/// One point of a region or outline: <c>{ point, t1?, t2?, width? }</c>.
/// </summary>
/// <remarks>
/// Tangents are Synfig's: the curve leaves a point toward <c>point + t2 / 3</c> and arrives at the next
/// toward <c>next − t1 / 3</c>, so a tangent is three times the distance to its Bézier control point.
/// <c>t2</c> defaults to <c>t1</c>, which keeps the point smooth; both default to zero, a corner.
/// </remarks>
internal sealed class MotionSplinePoint
{
    #region Fields
    private static readonly HashSet<string> Keys = ["point", "t1", "t2", "width"];
    private readonly MotionNode point, t1, t2, width;
    #endregion

    #region Constructors
    private MotionSplinePoint(MotionNode point, MotionNode t1, MotionNode t2, MotionNode width) =>
        (this.point, this.t1, this.t2, this.width) = (point, t1, t2, width);
    #endregion

    #region Methods
    public static IReadOnlyList<MotionSplinePoint> ReadAll(object? value, string who)
    {
        if (value is not IList list || list.Count == 0)
            throw new ArgumentException($"{who} takes points: an array of {{ point, t1?, t2?, width? }}.");

        return [.. list.Cast<object?>().Select((item, i) =>
        {
            var at = $"{who} point {i}";
            var d = JsInterop.AsDict(item);
            if (d is null) return new MotionSplinePoint(
                MotionNodeFactory.Node(item, MotionType.Vector, at),
                Zero(), Zero(), new MotionConstant(MotionType.Real, [1]));

            foreach (var key in d.Keys.Cast<object>().Select(k => k.ToString()!))
            {
                if (!Keys.Contains(key) && key is not ("x" or "y"))
                    throw new ArgumentException($"{at} has no '{key}'. A point takes: point, t1, t2, width.");
            }

            var p = d.Contains("point") ? d["point"] : item;
            var t1 = d["t1"] is { } a ? MotionNodeFactory.Node(a, MotionType.Vector, $"{at}'s t1") : Zero();
            var t2 = d["t2"] is { } b ? MotionNodeFactory.Node(b, MotionType.Vector, $"{at}'s t2") : t1;
            return new MotionSplinePoint(
                MotionNodeFactory.Node(p, MotionType.Vector, $"{at}'s point"), t1, t2,
                d["width"] is { } w ? MotionNodeFactory.Node(w, MotionType.Real, $"{at}'s width") : new MotionConstant(MotionType.Real, [1]));
        })];

        static MotionNode Zero() => new MotionConstant(MotionType.Vector, [0, 0]);
    }

    public State At(double time, double[] origin)
    {
        var p = point.Evaluate(time);
        var a = t1.Evaluate(time);
        var b = t2.Evaluate(time);
        return new State(
            new SKPoint((float)(p[0] + origin[0]), (float)(p[1] + origin[1])),
            new SKPoint((float)a[0], (float)a[1]),
            new SKPoint((float)b[0], (float)b[1]),
            width.Evaluate(time)[0]);
    }

    public IEnumerable<MotionNode> Nodes() => [point, t1, t2, width];

    public XElement ToSif(SifWriter sif) => new("composite", new XAttribute("type", "bline_point"),
        sif.Link("point", point),
        sif.Link("width", width),
        new XElement("origin", new XElement("real", new XAttribute("value", "0.5"))),
        new XElement("split", SifWriter.Bool(true)),
        sif.Link("t1", t1),
        sif.Link("t2", t2),
        new XElement("split_radius", SifWriter.Bool(true)),
        new XElement("split_angle", SifWriter.Bool(true)));
    #endregion

    #region Types
    public readonly record struct State(SKPoint P, SKPoint T1, SKPoint T2, double Width);
    #endregion
}

/// <summary>An options object read strictly: an unknown key is refused by name.</summary>
internal sealed class MotionOptions
{
    #region Constructors
    public MotionOptions(object? options, string who, params string[] allowed)
    {
        this.who = who;
        values = JsInterop.AsDict(options) ?? (options is null
            ? new Hashtable()
            : throw new ArgumentException($"{who} takes an options object."));

        foreach (var key in values.Keys.Cast<object>().Select(k => k.ToString()!))
        {
            if (!allowed.Contains(key, StringComparer.Ordinal))
                throw new ArgumentException($"{who} has no '{key}'. It takes: {string.Join(", ", allowed)}.");
        }
    }
    #endregion

    #region Fields
    private readonly IDictionary values;
    private readonly string who;
    #endregion

    #region Methods
    public bool Has(string key) => values.Contains(key) && values[key] is not null;

    public object? Raw(string key) => values[key];

    public MotionNode Node(string key, MotionType kind, object fallback) =>
        MotionNodeFactory.Node(Has(key) ? values[key] : fallback, kind, $"{who}'s {key}");

    public double Number(string key, double fallback) =>
        Has(key) ? MotionTypes.Read(values[key], MotionType.Real, $"{who}'s {key}")[0] : fallback;

    public bool Bool(string key, bool fallback) => !Has(key) ? fallback : values[key] is bool b
        ? b : throw new ArgumentException($"{who}'s {key} is true or false.");
    #endregion
}

/// <summary>
/// Writes a composition as Synfig's <c>.sif</c>: a node used in more than one place goes into
/// <c>&lt;defs&gt;</c> once and is referenced, so a link stays a link in Synfig.
/// </summary>
internal sealed class SifWriter
{
    #region Constructors
    public SifWriter(MotionComposition composition)
    {
        this.composition = composition;

        var uses = new Dictionary<MotionNode, int>(ReferenceEqualityComparer.Instance);
        foreach (var node in composition.Nodes()) Count(node);

        // Children before parents, because Synfig refuses a reference to a def it has not read yet.
        var ordered = new List<MotionNode>();
        var seen = new HashSet<MotionNode>(ReferenceEqualityComparer.Instance);
        foreach (var node in composition.Nodes()) Visit(node);
        foreach (var node in ordered.Where(n => uses[n] > 1)) ids[node] = $"v{ids.Count + 1}";
        exportOrder = [.. ordered.Where(ids.ContainsKey)];

        void Count(MotionNode node)
        {
            uses[node] = uses.GetValueOrDefault(node) + 1;
            if (uses[node] == 1) foreach (var child in node.Children) Count(child);
        }

        void Visit(MotionNode node)
        {
            if (!seen.Add(node)) return;
            foreach (var child in node.Children) Visit(child);
            ordered.Add(node);
        }
    }
    #endregion

    #region Fields
    private readonly MotionComposition composition;
    private readonly Dictionary<MotionNode, string> ids = new(ReferenceEqualityComparer.Instance);
    private readonly MotionNode[] exportOrder;
    #endregion

    #region Methods
    public string Write()
    {
        var c = composition;
        var v = c.View;
        var root = new XElement("canvas",
            new XAttribute("version", "1.2"),
            new XAttribute("width", c.Width), new XAttribute("height", c.Height),
            new XAttribute("xres", "2834.645669"), new XAttribute("yres", "2834.645669"),
            new XAttribute("gamma-r", "1"), new XAttribute("gamma-g", "1"), new XAttribute("gamma-b", "1"),
            new XAttribute("view-box", string.Join(" ", v.Select(MotionTypes.Format))),
            new XAttribute("antialias", "1"),
            new XAttribute("fps", MotionTypes.Format(c.Fps)),
            new XAttribute("begin-time", "0s"),
            new XAttribute("end-time", MotionTypes.Time(c.Duration)),
            new XElement("name", "Polson"),
            exportOrder.Length == 0 ? null : new XElement("defs", exportOrder.Select(n =>
            {
                var e = Inline(n);
                e.Add(new XAttribute("id", ids[n]));
                return e;
            })),
            c.LayersToSif(this));

        var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };
        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, settings)) new XDocument(new XDeclaration("1.0", "UTF-8", null), root).Save(writer);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>A layer parameter: the node inline, or a reference to its def.</summary>
    public XElement Param(string name, MotionNode node) => ids.TryGetValue(node, out var id)
        ? new XElement("param", new XAttribute("name", name), new XAttribute("use", id))
        : new XElement("param", new XAttribute("name", name), Inline(node));

    /// <summary>A link inside a linkable node, written as a child element; a shared node becomes an attribute.</summary>
    public object Link(string name, MotionNode node) => ids.TryGetValue(node, out var id)
        ? new XAttribute(name, id)
        : new XElement(name, Inline(node));

    public XElement Linkable(string element, string type, params (string Name, MotionNode Node)[] links) =>
        new(element, new XAttribute("type", type), links.Select(l => Link(l.Name, l.Node)));

    public static XElement Param(string name, XElement value) => new("param", new XAttribute("name", name), value);

    public static XElement Bool(bool value) => new("bool", new XAttribute("value", value ? "true" : "false"));

    public static XElement Value(MotionType kind, double[] v) => kind switch
    {
        MotionType.Real => new XElement("real", new XAttribute("value", MotionTypes.Format(v[0]))),
        MotionType.Angle => new XElement("angle", new XAttribute("value", MotionTypes.Format(v[0]))),
        MotionType.Vector => new XElement("vector",
            new XElement("x", MotionTypes.Format(v[0])), new XElement("y", MotionTypes.Format(v[1]))),
        _ => new XElement("color",
            new XElement("r", MotionTypes.Format(v[0])), new XElement("g", MotionTypes.Format(v[1])),
            new XElement("b", MotionTypes.Format(v[2])), new XElement("a", MotionTypes.Format(v[3])))
    };

    private XElement Inline(MotionNode node) => node.ToSif(this);
    #endregion
}
