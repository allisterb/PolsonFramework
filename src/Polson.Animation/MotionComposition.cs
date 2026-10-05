namespace Polson.Animation;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

using Polson.Drawing.Skia;

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
    /// <summary>A flat fill over the whole frame: <c>{ color, amount?, blend?, desc? }</c>.</summary>
    public MotionLayerList Fill(object? options)
    {
        var o = new MotionOptions(options, "fill", "color", "amount", "blend", "desc");
        layers.Add(new MotionLayer.SolidColor(o));
        return this;
    }

    /// <summary>A disc: <c>{ origin, radius, color, invert?, amount?, blend?, desc? }</c>.</summary>
    public MotionLayerList Circle(object? options)
    {
        var o = new MotionOptions(options, "circle", "origin", "radius", "color", "invert", "amount", "blend", "desc");
        layers.Add(new MotionLayer.CircleLayer(o));
        return this;
    }

    /// <summary>
    /// A rectangle between two corners, in either order:
    /// <c>{ point1, point2, expand?, color, invert?, amount?, blend?, desc? }</c>. <c>expand</c> grows it on
    /// every side. The bar of a bar chart: keyed corners stay exact in SVG.
    /// </summary>
    public MotionLayerList Rectangle(object? options)
    {
        var o = new MotionOptions(options, "rectangle",
            "point1", "point2", "expand", "color", "invert", "amount", "blend", "desc");
        layers.Add(new MotionLayer.RectangleLayer(o));
        return this;
    }

    /// <summary>A filled spline: <c>{ points, loop?, origin?, color, invert?, amount?, blend?, desc? }</c>.</summary>
    public MotionLayerList Region(object? options)
    {
        var o = new MotionOptions(options, "region", "points", "loop", "origin", "color", "invert", "amount", "blend", "desc");
        layers.Add(new MotionLayer.RegionLayer(o));
        return this;
    }

    /// <summary>
    /// A stroked spline whose width can vary along it:
    /// <c>{ points, loop?, origin?, width?, color, sharpCusps?, roundTips?, amount?, blend?, desc? }</c>.
    /// </summary>
    public MotionLayerList Outline(object? options)
    {
        var o = new MotionOptions(options, "outline",
            "points", "loop", "origin", "width", "color", "sharpCusps", "roundTips", "amount", "blend", "desc");
        layers.Add(new MotionLayer.OutlineLayer(o));
        return this;
    }

    /// <summary>
    /// A picture placed between two corners: <c>{ image, tl?, br?, interpolation?, amount?, blend?, desc? }</c>.
    /// <c>image</c> is a bitmap, a canvas, or anything carrying its own pixels (a cutout cell, a photograph);
    /// it is copied when the layer is made, so drawing on the canvas afterwards does not change it.
    /// <c>tl</c> and <c>br</c> default to the picture's own size at the origin; swapping them mirrors it.
    /// </summary>
    public MotionLayerList Image(object? options)
    {
        var o = new MotionOptions(options, "image", "image", "tl", "br", "interpolation", "amount", "blend", "desc");
        layers.Add(new MotionLayer.ImageLayer(o));
        return this;
    }

    /// <summary>
    /// A piece cut out of a picture, as a group that can be posed: the picture, and an inverted region
    /// that erases everything outside <paramref name="points"/>. <c>options</c> takes the image's
    /// <c>tl</c>, <c>br</c> and <c>interpolation</c> and the group's <c>origin</c>, <c>offset</c>,
    /// <c>angle</c>, <c>skewAngle</c>, <c>scale</c>, <c>bone</c>, <c>amount</c>, <c>blend</c>, <c>timeOffset</c>,
    /// <c>timeDilation</c> and <c>desc</c>. With <c>bone</c> the piece follows the bone from the rest pose.
    /// </summary>
    /// <remarks>
    /// Synfig Studio's Cutout tool builds exactly this: an import layer under a region with
    /// <c>invert</c> on and blend method alpha over (<c>state_lasso.cpp</c>). The outline is in the
    /// composition's units, where the picture is placed — not in the picture's pixels — so a piece
    /// placed at its own size at the origin takes its outline in pixels.
    /// </remarks>
    public MotionGroup Cutout(object? image, object? points, object? options = null)
    {
        var o = new MotionOptions(options, "cutout",
            "tl", "br", "interpolation",
            "origin", "offset", "angle", "skewAngle", "scale", "bone", "amount", "blend", "timeOffset", "timeDilation", "desc");
        var group = Group(o.Subset("origin", "offset", "angle", "skewAngle", "scale", "bone", "amount", "blend", "timeOffset", "timeDilation", "desc"));
        var picture = o.Subset("tl", "br", "interpolation");
        picture["image"] = image;
        group.Image(picture);
        group.Region(new Hashtable { ["points"] = points, ["invert"] = true, ["blend"] = "alphaOver", ["desc"] = "cut" });
        return group;
    }

    /// <summary>
    /// A group with its own transformation and clock, returned so layers can be added to it:
    /// <c>{ origin?, offset?, angle?, skewAngle?, scale?, bone?, amount?, blend?, timeOffset?, timeDilation?, desc? }</c>.
    /// With <c>bone</c> the group takes its transform from the bone instead, carried from the rest pose.
    /// </summary>
    public MotionGroup Group(object? options = null)
    {
        var o = new MotionOptions(options, "group",
            "origin", "offset", "angle", "skewAngle", "scale", "bone", "amount", "blend", "timeOffset", "timeDilation", "desc");
        var group = new MotionGroup(o) { Root = Root };
        layers.Add(group.Layer);
        return group;
    }

    /// <summary>
    /// A layer the SDK draws: <c>draw(ctx, v, t)</c> is called for every frame with a context in the
    /// layer's frame, <c>v</c> holding each of <c>options.values</c> evaluated at <c>t</c>.
    /// <c>options</c>: <c>{ values?, amount?, desc? }</c>.
    /// </summary>
    /// <remarks>
    /// The bridge between the model and the drawing toolkits: nodes say when, the toolkit says what.
    /// <c>t</c> is the layer's own time, so a group's <c>timeOffset</c> and <c>timeDilation</c> reach
    /// it. The canvas is saved before the call and restored after, so nothing the function does to the
    /// transform, clip or state reaches the next layer.
    /// </remarks>
    public MotionLayerList Drawn(Action<CanvasRenderingContext2D, object?, double>? draw, object? options = null)
    {
        if (draw is null)
            throw new ArgumentException("drawn(draw, options?) takes a function first: (ctx, v, t) => { ... }.");
        var o = new MotionOptions(options, "drawn", "values", "amount", "blend", "desc");
        layers.Add(new MotionLayer.DrawnLayer(o, draw));
        return this;
    }

    /// <summary>
    /// Catches <c>drawn({ ... })</c> with no function, which would otherwise fail with the binder's
    /// generic "no public methods" message instead of saying what is missing.
    /// </summary>
    public MotionLayerList Drawn(object? options) =>
        throw new ArgumentException("drawn(draw, options?) takes a function first: comp.drawn((ctx, v, t) => { ... }, { values }).");

    /// <summary>
    /// A bone deformation of everything below it in this stack:
    /// <c>{ bones?, point1?, point2?, xSubdivisions?, ySubdivisions?, amount?, desc? }</c>. <c>bones</c>
    /// defaults to all the composition's bones, the corners to their rest capsules' bounds, the grid to 32 × 32.
    /// </summary>
    /// <remarks>
    /// Synfig's skeleton deformation layer. A bone's <c>width</c> and <c>tipwidth</c> are its reach: only what
    /// lies inside the bones' rest capsules is kept and bent, so give each bone the width of the artwork it carries.
    /// </remarks>
    public MotionLayerList SkeletonDeformation(object? options = null)
    {
        var o = new MotionOptions(options, "skeletonDeformation",
            "bones", "point1", "point2", "xSubdivisions", "ySubdivisions", "amount", "desc");
        layers.Add(new MotionDeformationLayer(o, Root?.Bones ?? []));
        return this;
    }

    /// <summary>
    /// Rigs one drawing to bend: bones from its landmarks, and the drawing in a group with a skeleton deformation
    /// over it. <c>landmarks</c> is <c>Character.detect(image)</c> or <c>{ leftShoulder: [x, y], ... }</c>.
    /// <c>options</c>: <c>{ tl?, br?, poses?, turns?, move?, prefix?, subdivisions?, desc? }</c>.
    /// </summary>
    /// <remarks>
    /// The bones carry <c>Character.jointMap</c>'s body-part names under a <c>hips</c> root. Motion is given up front:
    /// <c>poses</c> as whole-pose keys, <c>[{ time, ease?, leftUpperArm: 40, ... }]</c>, or <c>turns</c> as a node per
    /// bone; <c>move</c> carries the whole figure.
    /// </remarks>
    public MotionRig RigFromDrawing(object? image, object? landmarks, object? options = null) =>
        MotionRigBuilder.Build(this, Root ?? throw new InvalidOperationException("This stack belongs to no composition."), image, landmarks, options);

    /// <summary>The composition this stack belongs to, whose bones it can use.</summary>
    internal MotionComposition? Root { get; private protected set; }

    /// <summary>Every layer here and in the groups below.</summary>
    internal IEnumerable<MotionLayer> AllLayers() =>
        layers.SelectMany(l => l is MotionLayer.GroupLayer g ? g.Children.AllLayers().Prepend(l) : [l]);

    internal void RenderLayers(SkiaCanvas surface, double time)
    {
        if (!layers.Any(l => l is MotionDeformationLayer))
        {
            foreach (var layer in layers) layer.Render(surface, time);
            return;
        }

        // A deformation bends what is below it in the stack, so the stack is drawn on a surface of its own,
        // in device pixels under the same transform, and each deformation replaces that surface with its result.
        var canvas = surface.SkCanvas;
        var matrix = canvas.TotalMatrix;
        var stack = Fresh(surface, matrix);
        try
        {
            foreach (var layer in layers)
            {
                if (layer is not MotionDeformationLayer deformation)
                {
                    layer.Render(stack, time);
                    continue;
                }

                var bent = deformation.Apply(stack, matrix, time);
                stack.Dispose();
                stack = bent;
                stack.SkCanvas.SetMatrix(matrix);
            }

            canvas.Save();
            canvas.ResetMatrix();
            canvas.DrawBitmap(stack.SkBitmap, 0, 0);
            canvas.Restore();
        }
        finally
        {
            stack.Dispose();
        }

        static SkiaCanvas Fresh(SkiaCanvas like, SKMatrix matrix)
        {
            var c = new SkiaCanvas(like.Width, like.Height, like.SkBitmap.ColorType);
            c.SkCanvas.Clear(SKColors.Transparent);
            c.SkCanvas.SetMatrix(matrix);
            return c;
        }
    }

    internal IEnumerable<XElement> LayersToSif(SifWriter sif) => layers.Select(l => l.ToSif(sif));

    internal IEnumerable<XElement> LayersToSvg(SvgAnimationWriter svg, MotionTimeMap map) =>
        [.. svg.Stack([.. layers.Select(l => (l.Blend, l.Desc, l.ToSvg(svg, map)))])];

    internal IEnumerable<MotionNode> Nodes() => layers.SelectMany(l => l.Nodes());

    /// <summary>Whether a layer here blends other than by compositing, so the stack must be drawn on its own.</summary>
    internal bool HasBlendedLayers => layers.Any(l => !l.Blend.IsComposite);

    /// <summary>Whether any layer here or in a group below blends other than by compositing.</summary>
    internal bool HasBlendedLayersDeep =>
        HasBlendedLayers || layers.OfType<MotionLayer.GroupLayer>().Any(g => g.Children.HasBlendedLayersDeep);
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
        Root = this;

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
    private readonly List<MotionBone> bones = [];
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
    /// <summary>
    /// A bone of the composition's skeleton:
    /// <c>{ name?, parent?, from, to, turn?, stretch?, width?, tipwidth? }</c>, or Synfig's own fields
    /// <c>{ name?, parent?, origin, angle, length?, scalelx?, scalex?, width?, tipwidth? }</c>.
    /// </summary>
    /// <remarks>
    /// <c>from</c> and <c>to</c> are the joints in the rest pose, in composition coordinates; <c>turn</c>
    /// (degrees) rotates the bone about <c>from</c> relative to its parent, and <c>stretch</c> lengthens it,
    /// carrying its children along. Synfig's fields are in the parent's frame instead: <c>[parent's length, 0]</c>
    /// is the parent's tip. A layer or point bound to the bone follows it from the rest pose.
    /// </remarks>
    public MotionBone Bone(object? options)
    {
        var bone = new MotionBone(options, bones.Count, bones);
        if (bones.Any(b => b.Name == bone.Name)) throw new ArgumentException($"There is already a bone named '{bone.Name}'.");
        bones.Add(bone);
        return bone;
    }

    /// <summary>The composition's bones, in the order they were made.</summary>
    public IReadOnlyList<MotionBone> Bones => bones;

    /// <summary>
    /// A layer drawing every bone as a capsule from origin to tip, so a rig can be seen:
    /// <c>{ color?, amount?, blend?, desc? }</c>. It draws the bones there are when it renders.
    /// </summary>
    public MotionLayerList Skeleton(object? options = null)
    {
        var o = new MotionOptions(options, "skeleton", "color", "amount", "blend", "desc");
        layers.Add(new MotionLayer.SkeletonLayer(o, bones));
        return this;
    }

    /// <summary>The composition at a time in seconds, on a new canvas.</summary>
    public SkiaCanvas Render(double time)
    {
        var canvas = new SkiaCanvas(Width, Height);
        canvas.SkCanvas.Clear(SKColors.Transparent);
        if (!HasBlendedLayersDeep)
        {
            canvas.SkCanvas.Save();
            canvas.SkCanvas.Concat(ViewMatrix());
            RenderLayers(canvas, time);
            canvas.SkCanvas.Restore();
            return canvas;
        }

        // Synfig's surfaces are float, so a subtract can go below zero and a divide above one and carry
        // that into the next layer. Blend on a float surface and clamp once, at the end.
        using var working = new SkiaCanvas(Width, Height, SKColorType.RgbaF16);
        working.SkCanvas.Clear(SKColors.Transparent);
        working.SkCanvas.Concat(ViewMatrix());
        RenderLayers(working, time);
        canvas.SkCanvas.DrawBitmap(working.SkBitmap, 0, 0);
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

        // A blend reaches only the composition's own layers, never the drawing it is placed in.
        if (HasBlendedLayers) c.SaveLayer();
        RenderLayers(ctx.Canvas, time);
        if (HasBlendedLayers) c.Restore();
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

    /// <summary>
    /// The composition as an animated SVG: SMIL animation on ordinary elements, playing in a browser and
    /// in an <c>&lt;img&gt;</c>. <c>options</c>: <c>{ loop?, fps? }</c> — <c>loop</c> repeats instead of
    /// holding the last frame; <c>fps</c> is the rate for values that cannot be written exactly.
    /// </summary>
    public string ToSvg(object? options = null)
    {
        var o = new MotionOptions(options, "composition.toSvg", "loop", "fps");
        var fps = o.Number("fps", Fps);
        if (fps <= 0 || fps > 240) throw new ArgumentException($"composition.toSvg: fps {fps} is not between 0 and 240.");
        return new SvgAnimationWriter(this, o.Bool("loop", false), fps).Write();
    }

    /// <summary>Writes the animated SVG into the project and returns the path written.</summary>
    public string SaveSvg(string filePath, object? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var svg = ToSvg(options);
        var full = ProjectPath.Resolve(projectRoot, filePath, nameof(filePath), "Write to");
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var bytes = new UTF8Encoding(false).GetBytes(svg);
        File.WriteAllBytes(full, bytes);

        motion?.Record("render", full, new()
        {
            ["motion"] = "svg",
            ["format"] = "svg",
            ["bytes"] = (long)bytes.Length,
            ["durationMs"] = Duration * 1000,
            ["width"] = Width,
            ["height"] = Height
        });
        return full;
    }

    /// <summary>
    /// The composition as Synfig's <c>.sif</c> XML. A composition with an image layer is refused: its
    /// pictures have to be written beside the file, which <see cref="SaveSif"/> does.
    /// </summary>
    public string ToSif() => new SifWriter(this, null).Write();

    /// <summary>
    /// Writes the <c>.sif</c> into the project and returns the path written. Each image layer's picture is
    /// written beside it as <c>&lt;name&gt;-image&lt;n&gt;.png</c>, where Synfig looks for it.
    /// </summary>
    public string SaveSif(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var full = ProjectPath.Resolve(projectRoot, filePath, nameof(filePath), "Write to");
        var dir = Path.GetDirectoryName(full)!;
        var writer = new SifWriter(this, Path.GetFileNameWithoutExtension(full));
        var xml = writer.Write();
        Directory.CreateDirectory(dir);
        foreach (var (name, png) in writer.Pictures) File.WriteAllBytes(Path.Combine(dir, name), png);
        var bytes = new UTF8Encoding(false).GetBytes(xml);
        File.WriteAllBytes(full, bytes);

        // Not a render: nothing in a browser plays a .sif. Recorded as an export so the file the run
        // left behind is still accounted for, pictures included.
        motion?.Record("export", full, new()
        {
            ["motion"] = "sif",
            ["format"] = "sif",
            ["bytes"] = (long)bytes.Length,
            ["pictures"] = writer.Pictures.Select(p => (object?)Path.Combine(dir, p.Name)).ToList()
        });
        return full;
    }

    internal double[] View => view;

    /// <summary>Every node the composition holds, the bones' included.</summary>
    internal IEnumerable<MotionNode> AllNodes() => bones.SelectMany(b => b.Nodes).Concat(Nodes());

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
        Blend = MotionBlend.Parse(o.Raw("blend"), o.Who);
    }
    #endregion

    #region Properties
    public string? Desc { get; }

    public MotionNode Amount { get; }

    public MotionBlend Blend { get; }
    #endregion

    #region Methods
    /// <summary>
    /// Draws the layer at a time. A composited layer is drawn straight onto the canvas at its
    /// <c>amount</c>; any other blend draws it at full strength into a layer of its own and lets the
    /// blender combine that with the backdrop, <c>amount</c> and all, as Synfig's formula does.
    /// </summary>
    public void Render(SkiaCanvas surface, double time)
    {
        var amount = AmountAt(time);
        if (Blend.IsComposite)
        {
            if (amount > 0) Draw(surface, time, amount);
            return;
        }

        var canvas = surface.SkCanvas;
        using var blender = Blend.Blender(amount);
        using var paint = new SKPaint { Blender = blender };
        canvas.SaveLayer(paint);
        Draw(surface, time, 1);
        canvas.Restore();
    }

    /// <summary>Draws the layer's content at a time, at <paramref name="amount"/> opacity.</summary>
    protected abstract void Draw(SkiaCanvas surface, double time, double amount);

    public abstract XElement ToSif(SifWriter sif);

    public abstract XElement ToSvg(SvgAnimationWriter svg, MotionTimeMap map);

    public abstract IEnumerable<MotionNode> Nodes();

    /// <summary>The layer's <c>amount</c> as SVG <c>opacity</c>, omitted while it is 1.</summary>
    protected void Opacity(XElement element, SvgAnimationWriter svg, MotionTimeMap map)
    {
        var track = svg.Track(Amount, 0, map);
        if (track.Values.Any(v => v < 0 || v > 1)) track = svg.Track(Amount, 0, map, v => Math.Clamp(v, 0, 1));
        if (track.IsStatic && Math.Abs(track.Values[0] - 1) < 1e-12) return;
        svg.Attribute(element, "opacity", track);
    }

    /// <summary>A colour as <c>fill</c> or <c>stroke</c> plus its opacity: the RGB sampled, the alpha exact where it can be.</summary>
    protected static void Paint(XElement element, string attribute, MotionNode color, SvgAnimationWriter svg, MotionTimeMap map)
    {
        svg.Attribute(element, attribute, t =>
        {
            var c = MotionTypes.ToSkColor(color.Evaluate(map.Local(t)));
            return $"#{c.Red:X2}{c.Green:X2}{c.Blue:X2}";
        });

        var alpha = svg.Track(color, 3, map);
        if (alpha.IsStatic && Math.Abs(alpha.Values[0] - 1) < 1e-12) return;
        svg.Attribute(element, attribute + "-opacity", alpha);
    }

    protected XElement SvgElement(string name, params object?[] content) =>
        new(SvgAnimationWriter.Svg + name, Desc is null ? null : new XAttribute("data-desc", Desc), content);

    protected XElement Element(string type, string version, SifWriter sif, params object?[] content) =>
        new("layer", new XAttribute("type", type), new XAttribute("active", "true"), new XAttribute("version", version),
            Desc is null ? null : new XAttribute("desc", Desc),
            SifWriter.Param("z_depth", new XElement("real", new XAttribute("value", "0"))),
            sif.Param("amount", Amount),
            SifWriter.Param("blend_method", new XElement("integer", new XAttribute("value", SifBlendMethod))),
            content);

    /// <summary>The <c>blend_method</c> written: the layer's blend, unless the layer composites some other way.</summary>
    protected virtual int SifBlendMethod => Blend.Synfig;

    protected double AmountAt(double time) => Math.Clamp(Amount.Evaluate(time)[0], 0, 1);

    protected static string P(SKPoint p) => $"{SvgAnimationWriter.N(p.X)} {SvgAnimationWriter.N(p.Y)}";

    // An affine map keeps a track exact: a keySpline shapes progress between two values, not the values.
    protected static MotionTrack Affine(MotionTrack t, double k, double c) => t with { Values = [.. t.Values.Select(v => k * v + c)] };

    protected static SKPaint Paint(double[] color, double amount) =>
        new() { Color = MotionTypes.ToSkColor(color, amount), IsAntialias = true, Style = SKPaintStyle.Fill };
    #endregion

    #region Types
    public sealed class SolidColor(MotionOptions o) : MotionLayer(o)
    {
        private readonly MotionNode color = o.Node("color", MotionType.Color, "#000000");

        protected override void Draw(SkiaCanvas surface, double time, double amount)
        {
            using var paint = Paint(color.Evaluate(time), amount);
            surface.SkCanvas.DrawPaint(paint);
        }

        public override XElement ToSif(SifWriter sif) => Element("solid_color", "0.1", sif, sif.Param("color", color));

        public override XElement ToSvg(SvgAnimationWriter svg, MotionTimeMap map)
        {
            // At the root a fill covers the view; inside a group it must survive the group's transform.
            var v = svg.View;
            var (x, y, w, h) = svg.Depth == 0
                ? (Math.Min(v[0], v[2]), Math.Min(v[1], v[3]), Math.Abs(v[2] - v[0]), Math.Abs(v[3] - v[1]))
                : (-100000d, -100000d, 200000d, 200000d);
            var rect = SvgElement("rect", new XAttribute("x", SvgAnimationWriter.N(x)), new XAttribute("y", SvgAnimationWriter.N(y)),
                new XAttribute("width", SvgAnimationWriter.N(w)), new XAttribute("height", SvgAnimationWriter.N(h)));
            Paint(rect, "fill", color, svg, map);
            Opacity(rect, svg, map);
            return rect;
        }

        public override IEnumerable<MotionNode> Nodes() => [Amount, color];
    }

    public sealed class CircleLayer(MotionOptions o) : MotionLayer(o)
    {
        private readonly MotionNode origin = o.Node("origin", MotionType.Vector, new[] { 0d, 0d });
        private readonly MotionNode radius = o.Node("radius", MotionType.Real, 10d);
        private readonly MotionNode color = o.Node("color", MotionType.Color, "#000000");
        private readonly bool invert = o.Bool("invert", false);

        protected override void Draw(SkiaCanvas surface, double time, double amount)
        {
            var p = origin.Evaluate(time);
            using var paint = Paint(color.Evaluate(time), amount);
            using var path = new SKPath { FillType = invert ? SKPathFillType.InverseWinding : SKPathFillType.Winding };
            path.AddCircle((float)p[0], (float)p[1], (float)Math.Abs(radius.Evaluate(time)[0]));
            surface.SkCanvas.DrawPath(path, paint);
        }

        public override XElement ToSif(SifWriter sif) => Element("circle", "0.2", sif,
            sif.Param("color", color), sif.Param("radius", radius),
            SifWriter.Param("feather", new XElement("real", new XAttribute("value", "0"))),
            sif.Param("origin", origin),
            SifWriter.Param("invert", SifWriter.Bool(invert)));

        public override XElement ToSvg(SvgAnimationWriter svg, MotionTimeMap map)
        {
            if (invert)
            {
                // An inverted disc has no SVG element of its own: everything, less a circle drawn as two arcs.
                var path = SvgElement("path", new XAttribute("fill-rule", "evenodd"));
                svg.Attribute(path, "d", t =>
                {
                    var local = map.Local(t);
                    var p = origin.Evaluate(local);
                    var r = Math.Abs(radius.Evaluate(local)[0]);
                    string N(double v) => SvgAnimationWriter.N(v);
                    return $"{SvgAnimationWriter.Everything}M{N(p[0] - r)} {N(p[1])}A{N(r)} {N(r)} 0 1 0 {N(p[0] + r)} {N(p[1])}A{N(r)} {N(r)} 0 1 0 {N(p[0] - r)} {N(p[1])}Z";
                });
                Paint(path, "fill", color, svg, map);
                Opacity(path, svg, map);
                return path;
            }

            var circle = SvgElement("circle");
            svg.Attribute(circle, "cx", svg.Track(origin, 0, map));
            svg.Attribute(circle, "cy", svg.Track(origin, 1, map));
            var r = svg.Track(radius, 0, map);
            if (r.Values.Any(v => v < 0)) r = svg.Track(radius, 0, map, Math.Abs);
            svg.Attribute(circle, "r", r);
            Paint(circle, "fill", color, svg, map);
            Opacity(circle, svg, map);
            return circle;
        }

        public override IEnumerable<MotionNode> Nodes() => [Amount, origin, radius, color];
    }

    public sealed class RectangleLayer(MotionOptions o) : MotionLayer(o)
    {
        private readonly MotionNode point1 = o.Node("point1", MotionType.Vector, new[] { 0d, 0d });
        private readonly MotionNode point2 = o.Node("point2", MotionType.Vector, new[] { 10d, 10d });
        private readonly MotionNode expand = o.Node("expand", MotionType.Real, 0d);
        private readonly MotionNode color = o.Node("color", MotionType.Color, "#000000");
        private readonly bool invert = o.Bool("invert", false);

        public override IEnumerable<MotionNode> Nodes() => [Amount, point1, point2, expand, color];

        protected override void Draw(SkiaCanvas surface, double time, double amount)
        {
            using var paint = Paint(color.Evaluate(time), amount);
            using var path = new SKPath { FillType = invert ? SKPathFillType.InverseWinding : SKPathFillType.Winding };
            path.AddRect(Bounds(time));
            surface.SkCanvas.DrawPath(path, paint);
        }

        private SKRect Bounds(double time)
        {
            var (x, w) = Corners(time, 0);
            var (y, h) = Corners(time, 1);
            return SKRect.Create((float)x, (float)y, (float)w, (float)h);
        }

        public override XElement ToSif(SifWriter sif) => Element("rectangle", "0.2", sif,
            sif.Param("color", color), sif.Param("point1", point1), sif.Param("point2", point2), sif.Param("expand", expand),
            SifWriter.Param("invert", SifWriter.Bool(invert)),
            SifWriter.Param("feather_x", new XElement("real", new XAttribute("value", "0"))),
            SifWriter.Param("feather_y", new XElement("real", new XAttribute("value", "0"))),
            SifWriter.Param("bevel", new XElement("real", new XAttribute("value", "0"))),
            SifWriter.Param("bevCircle", SifWriter.Bool(true)));

        public override XElement ToSvg(SvgAnimationWriter svg, MotionTimeMap map)
        {
            if (invert)
            {
                var path = SvgElement("path", new XAttribute("fill-rule", "evenodd"));
                svg.Attribute(path, "d", t =>
                {
                    var r = Bounds(map.Local(t));
                    return $"{SvgAnimationWriter.Everything}M{P(new(r.Left, r.Top))}H{SvgAnimationWriter.N(r.Right)}V{SvgAnimationWriter.N(r.Bottom)}H{SvgAnimationWriter.N(r.Left)}Z";
                });
                Paint(path, "fill", color, svg, map);
                Opacity(path, svg, map);
                return path;
            }

            var rect = SvgElement("rect");
            Side(svg, map, rect, 0, "x", "width");
            Side(svg, map, rect, 1, "y", "height");
            Paint(rect, "fill", color, svg, map);
            Opacity(rect, svg, map);
            return rect;
        }

        /// <summary>
        /// One axis: its start and its size. When one corner holds still and the other is keyed and never
        /// crosses it, both are affine in the keyed one and stay exact; otherwise both are sampled.
        /// </summary>
        private void Side(SvgAnimationWriter svg, MotionTimeMap map, XElement rect, int c, string start, string size)
        {
            var a = svg.Track(point1, c, map);
            var b = svg.Track(point2, c, map);
            var e = svg.Track(expand, 0, map);
            if (e.IsStatic && (a.IsStatic || b.IsStatic))
            {
                var (still, moving) = a.IsStatic ? (a.Values[0], b) : (b.Values[0], a);
                var ex = Math.Abs(e.Values[0]);
                if (moving.Values.All(v => v >= still))
                {
                    svg.Attribute(rect, start, Affine(a.IsStatic ? a : b, 1, -ex));
                    svg.Attribute(rect, size, Affine(moving, 1, 2 * ex - still));
                    return;
                }

                if (moving.Values.All(v => v <= still))
                {
                    svg.Attribute(rect, start, Affine(moving, 1, -ex));
                    svg.Attribute(rect, size, Affine(moving, -1, still + 2 * ex));
                    return;
                }
            }

            svg.Attribute(rect, start, svg.Track(t => Corners(map.Local(t), c).Min));
            svg.Attribute(rect, size, svg.Track(t => Corners(map.Local(t), c).Size));
        }

        private (double Min, double Size) Corners(double time, int c)
        {
            var a = point1.Evaluate(time)[c];
            var b = point2.Evaluate(time)[c];
            var e = Math.Abs(expand.Evaluate(time)[0]);
            return (Math.Min(a, b) - e, Math.Abs(b - a) + 2 * e);
        }

    }

    /// <summary>
    /// Synfig's import layer: a picture whose top-left corner is drawn at <c>tl</c> and bottom-right at
    /// <c>br</c>, so swapping a pair of coordinates mirrors it. The picture is held, never a filename: a
    /// script hands over pixels, and the <c>.sif</c> writer puts them in a file beside its own.
    /// </summary>
    public sealed class ImageLayer : MotionLayer
    {
        public ImageLayer(MotionOptions o) : base(o)
        {
            picture = MotionPicture.From(o.Raw("image"), o.Who);
            tl = o.Node("tl", MotionType.Vector, new[] { 0d, 0d });
            br = o.Node("br", MotionType.Vector, new double[] { picture.Width, picture.Height });
            var name = o.Has("interpolation") ? o.Raw("interpolation") as string
                ?? throw new ArgumentException($"{o.Who}'s interpolation is a name: nearest, linear or cubic.") : "linear";
            interpolation = name switch
            {
                "nearest" => 0,
                "linear" => 1,
                "cubic" => 3,
                _ => throw new ArgumentException($"{o.Who} has no interpolation '{name}'. It takes: nearest, linear, cubic.")
            };
        }

        private readonly MotionPicture picture;
        private readonly MotionNode tl, br;
        private readonly int interpolation;

        public override IEnumerable<MotionNode> Nodes() => [Amount, tl, br];

        internal MotionPicture Picture => picture;

        protected override void Draw(SkiaCanvas surface, double time, double amount)
        {
            var a = tl.Evaluate(time);
            var b = br.Evaluate(time);
            var sx = (b[0] - a[0]) / picture.Width;
            var sy = (b[1] - a[1]) / picture.Height;
            if (sx == 0 || sy == 0) return;

            var canvas = surface.SkCanvas;
            canvas.Save();
            canvas.Concat(new SKMatrix((float)sx, 0, (float)a[0], 0, (float)sy, (float)a[1], 0, 0, 1));
            using var paint = new SKPaint { Color = SKColors.White.WithAlpha((byte)Math.Round(amount * 255)) };
            var sampling = interpolation switch
            {
                0 => new SKSamplingOptions(SKFilterMode.Nearest),
                3 => new SKSamplingOptions(SKCubicResampler.CatmullRom),
                _ => new SKSamplingOptions(SKFilterMode.Linear)
            };
            canvas.DrawImage(picture.Image, 0, 0, sampling, paint);
            canvas.Restore();
        }

        public override XElement ToSif(SifWriter sif) => Element("import", "0.1", sif,
            sif.Param("tl", tl), sif.Param("br", br),
            SifWriter.Param("c", new XElement("integer", new XAttribute("value", interpolation))),
            SifWriter.Param("gamma_adjust", new XElement("real", new XAttribute("value", "1"))),
            SifWriter.Param("filename", new XElement("string", sif.Picture(picture))),
            SifWriter.Param("time_offset", new XElement("time", new XAttribute("value", "0s"))));

        /// <summary>
        /// The picture at its own size, under a translation to <c>tl</c> and a scale to <c>br</c>: exact
        /// wherever one corner holds still, sampled when both move.
        /// </summary>
        public override XElement ToSvg(SvgAnimationWriter svg, MotionTimeMap map)
        {
            var image = SvgElement("image",
                new XAttribute("width", picture.Width), new XAttribute("height", picture.Height),
                new XAttribute("preserveAspectRatio", "none"),
                interpolation == 0 ? new XAttribute("image-rendering", "pixelated") : null,
                new XAttribute(SvgAnimationWriter.Xlink + "href", picture.DataUri));

            var chain = new[]
            {
                svg.Transform("translate", svg.Track(tl, 0, map), v => $"{SvgAnimationWriter.N(v)} 0", 0),
                svg.Transform("translate", svg.Track(tl, 1, map), v => $"0 {SvgAnimationWriter.N(v)}", 0),
                svg.Transform("scale", Scale(svg, map, 0, picture.Width), v => $"{SvgAnimationWriter.N(v, 6)} 1", 1),
                svg.Transform("scale", Scale(svg, map, 1, picture.Height), v => $"1 {SvgAnimationWriter.N(v, 6)}", 1),
            }.Where(g => g.HasAttributes || g.HasElements).ToArray();

            object inner = image;
            for (var i = chain.Length - 1; i >= 0; i--)
            {
                chain[i].Add(inner);
                inner = chain[i];
            }

            var outer = SvgElement("g", inner);
            Opacity(outer, svg, map);
            return outer;
        }

        private MotionTrack Scale(SvgAnimationWriter svg, MotionTimeMap map, int c, int size)
        {
            var a = svg.Track(tl, c, map);
            var b = svg.Track(br, c, map);
            if (a.IsStatic) return Affine(b, 1d / size, -a.Values[0] / size);
            if (b.IsStatic) return Affine(a, -1d / size, b.Values[0] / size);
            return svg.Track(t => (br.Evaluate(map.Local(t))[c] - tl.Evaluate(map.Local(t))[c]) / size);
        }
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

        /// <summary>Whether the shape covers everything but itself.</summary>
        protected virtual bool Inverted => false;

        public override IEnumerable<MotionNode> Nodes() =>
            new[] { Amount, Origin, Color }.Concat(Points.SelectMany(p => p.Nodes()));

        protected XElement[] ShapeParams(SifWriter sif) =>
        [
            sif.Param("color", Color),
            sif.Param("origin", Origin),
            SifWriter.Param("invert", SifWriter.Bool(Inverted)),
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

        /// <summary>
        /// The spline as SVG path data with the same commands at every time — a cubic for every segment,
        /// even a straight one — so SMIL can interpolate one frame's <c>d</c> into the next.
        /// </summary>
        protected string PathData(double time)
        {
            var s = States(time);
            if (s.Length == 0) return "";

            var d = new StringBuilder($"M{P(s[0].P)}");
            for (var i = 1; i < s.Length; i++) Cubic(d, s[i - 1], s[i]);
            if (Loop)
            {
                Cubic(d, s[^1], s[0]);
                d.Append('Z');
            }

            return d.ToString();

            static void Cubic(StringBuilder d, MotionSplinePoint.State a, MotionSplinePoint.State b) =>
                d.Append($"C{P(a.P + Third(a.T2))} {P(b.P - Third(b.T1))} {P(b.P)}");
        }

        protected MotionSplinePoint.State[] States(double time)
        {
            var o = Origin.Evaluate(time);
            return [.. Points.Select(p => p.At(time, o))];
        }
    }

    public sealed class RegionLayer(MotionOptions o) : Shape(o, "region", true)
    {
        private readonly bool invert = o.Bool("invert", false);

        protected override bool Inverted => invert;

        protected override void Draw(SkiaCanvas surface, double time, double amount)
        {
            using var path = Path(time, out _);
            if (invert) path.FillType = SKPathFillType.InverseWinding;
            using var paint = Paint(Color.Evaluate(time), amount);
            surface.SkCanvas.DrawPath(path, paint);
        }

        public override XElement ToSif(SifWriter sif) => Element("region", "0.1", sif, ShapeParams(sif));

        public override XElement ToSvg(SvgAnimationWriter svg, MotionTimeMap map)
        {
            var path = SvgElement("path");
            if (invert) path.SetAttributeValue("fill-rule", "evenodd");
            svg.Attribute(path, "d", t => (invert ? SvgAnimationWriter.Everything : "") + PathData(map.Local(t)));
            Paint(path, "fill", Color, svg, map);
            Opacity(path, svg, map);
            return path;
        }
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

        /// <summary>
        /// A stroke while every point has the same width at every frame; otherwise the tapered outline as
        /// a filled polygon with a fixed number of vertices, so its <c>d</c> interpolates.
        /// </summary>
        public override XElement ToSvg(SvgAnimationWriter svg, MotionTimeMap map)
        {
            var path = SvgElement("path");
            var even = svg.Times.All(t =>
            {
                var s = States(map.Local(t));
                return s.All(p => Math.Abs(p.Width - s[0].Width) < 1e-9);
            });

            if (even)
            {
                svg.Attribute(path, "d", t => PathData(map.Local(t)));
                path.SetAttributeValue("fill", "none");
                Paint(path, "stroke", Color, svg, map);
                svg.Attribute(path, "stroke-width", svg.Track(t =>
                {
                    var local = map.Local(t);
                    var s = States(local);
                    return s.Length == 0 ? 0 : Math.Abs(width.Evaluate(local)[0] * s[0].Width);
                }));
                path.SetAttributeValue("stroke-linecap", roundTips ? "round" : "butt");
                path.SetAttributeValue("stroke-linejoin", sharpCusps ? "miter" : "round");
            }
            else
            {
                svg.Attribute(path, "d", t => TaperedData(map.Local(t)));
                Paint(path, "fill", Color, svg, map);
            }

            Opacity(path, svg, map);
            return path;
        }

        /// <summary>The tapered outline as one polygon: down the left side, round the far tip, back up the right.</summary>
        private string TaperedData(double time)
        {
            const int samples = 32;
            var s = States(time);
            var w = width.Evaluate(time)[0];
            var left = new List<SKPoint>();
            var right = new List<SKPoint>();
            var count = Loop ? s.Length : s.Length - 1;
            SKPoint tan = new(1, 0), first = new(1, 0);

            for (var i = 0; i < count; i++)
            {
                var a = s[i];
                var b = s[(i + 1) % s.Length];
                using var seg = new SKPath();
                seg.MoveTo(a.P);
                seg.CubicTo(a.P + Third(a.T2), b.P - Third(b.T1), b.P);
                using var measure = new SKPathMeasure(seg);
                for (var k = i == 0 ? 0 : 1; k <= samples; k++)
                {
                    if (!measure.GetPositionAndTangent(measure.Length * k / samples, out var pos, out var tn)) pos = a.P;
                    else if (tn.Length > 0) tan = tn;
                    if (i == 0 && k == 0) first = tan;
                    var half = (float)(Math.Abs(w) * (a.Width + (b.Width - a.Width) * k / samples) / 2);
                    var n = new SKPoint(-tan.Y * half, tan.X * half);
                    left.Add(pos + n);
                    right.Add(pos - n);
                }
            }

            if (left.Count == 0) return "";

            var d = new StringBuilder($"M{P(left[0])}");
            foreach (var q in left.Skip(1)) d.Append($"L{P(q)}");
            if (!Loop && roundTips) Arc(d, s[^1].P, tan, (float)(Math.Abs(w) * s[^1].Width / 2), true);
            for (var k = right.Count - 1; k >= 0; k--) d.Append($"L{P(right[k])}");
            if (!Loop && roundTips) Arc(d, s[0].P, first, (float)(Math.Abs(w) * s[0].Width / 2), false);
            d.Append('Z');
            return d.ToString();

            // A semicircle with a fixed vertex count round an end: from the left side to the right.
            static void Arc(StringBuilder d, SKPoint centre, SKPoint direction, float radius, bool forward)
            {
                const int tip = 8;
                var heading = Math.Atan2(direction.Y, direction.X);
                var a0 = forward ? heading + Math.PI / 2 : heading - Math.PI / 2;
                for (var j = 1; j < tip; j++)
                {
                    var a = a0 - Math.PI * j / tip;
                    d.Append($"L{P(new SKPoint(centre.X + (float)(radius * Math.Cos(a)), centre.Y + (float)(radius * Math.Sin(a))))}");
                }
            }
        }

        protected override void Draw(SkiaCanvas surface, double time, double amount)
        {
            var canvas = surface.SkCanvas;
            using var path = Path(time, out var states);
            if (states.Length == 0) return;

            var w = width.Evaluate(time)[0];
            using var paint = Paint(Color.Evaluate(time), amount);

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

    public sealed class DrawnLayer : MotionLayer
    {
        public DrawnLayer(MotionOptions o, Action<CanvasRenderingContext2D, object?, double> draw) : base(o)
        {
            this.draw = draw;
            values = ReadValues(o.Raw("values"));
        }

        private readonly Action<CanvasRenderingContext2D, object?, double> draw;
        private readonly (string Name, MotionNode Node)[] values;

        public override IEnumerable<MotionNode> Nodes() => values.Select(v => v.Node).Prepend(Amount);

        protected override void Draw(SkiaCanvas surface, double time, double amount)
        {
            var canvas = surface.SkCanvas;
            var depth = canvas.Save();
            if (amount < 1)
            {
                using var fade = new SKPaint { Color = new SKColor(255, 255, 255, (byte)Math.Round(amount * 255)) };
                canvas.SaveLayer(fade);
            }

            try
            {
                var ctx = new CanvasRenderingContext2D(surface, canvas.TotalMatrix);
                var v = values.ToDictionary(e => e.Name, e => (object?)MotionTypes.ToJs(e.Node.Kind, e.Node.Evaluate(time)));
                draw(ctx, v, time);
            }
            finally
            {
                canvas.RestoreToCount(depth);
            }
        }

        public override XElement ToSif(SifWriter sif) => throw new InvalidOperationException(
            $"The drawn layer{(Desc is null ? "" : $" '{Desc}'")} cannot be written as .sif: Synfig cannot run a script. "
            + "Write the composition without it, or capture it as frames.");

        public override XElement ToSvg(SvgAnimationWriter svg, MotionTimeMap map) => throw SvgAnimationWriter.Refuse("drawn layer", Desc);

        /// <summary>Each value's type is read from what it is: a node, a number, a point, or a colour.</summary>
        private static (string, MotionNode)[] ReadValues(object? raw)
        {
            if (raw is null) return [];
            var d = JsInterop.AsDict(raw) ?? throw new ArgumentException("drawn's values is an object: { name: value or node }.");
            return [.. d.Keys.Cast<object>().Select(k => k.ToString()!).Select(name =>
            {
                var value = d[name];
                var who = $"drawn's value '{name}'";
                var kind = value switch
                {
                    MotionNode n => n.Kind,
                    double or int or float or long => MotionType.Real,
                    string => MotionType.Color,
                    IList or IDictionary or IDictionary<string, object?> => MotionType.Vector,
                    _ => throw new ArgumentException($"{who} is a number, a point, a colour or a node, not {value?.GetType().Name ?? "nothing"}.")
                };
                return (name, MotionNodeFactory.Node(value, kind, who));
            })];
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

            if (o.Has("bone"))
            {
                if (o.Raw("bone") is not MotionBone b)
                    throw new ArgumentException($"{o.Who}'s bone is a bone made by composition.bone(...); a weighted list binds points, not groups.");
                var own = new[] { "origin", "offset", "angle", "skewAngle", "scale" }.Where(o.Has).ToArray();
                if (own.Length > 0)
                    throw new ArgumentException(
                        $"{o.Who} follows bone '{b.Name}', which gives it its transform: drop {string.Join(", ", own)}, or put them on a group inside it.");
                bone = b;
                restInverse = b.RestLink.Invert();
            }
        }

        private readonly MotionBone? bone;
        private readonly MotionAffine restInverse;

        private readonly MotionGroup children;
        private readonly MotionNode origin, offset, angle, skewAngle, scale;

        internal MotionGroup Children => children;
        private readonly double timeOffset, timeDilation;

        public override IEnumerable<MotionNode> Nodes() =>
            new[] { Amount, origin, offset, angle, skewAngle, scale }.Concat(children.Nodes());

        /// <summary>
        /// Draws the children in the group's frame. They are drawn on a surface of their own whenever that
        /// could differ from drawing them in place — a fade, or a child that blends — because Synfig blends
        /// a child with its siblings only, never with what lies under the group.
        /// </summary>
        protected override void Draw(SkiaCanvas surface, double time, double amount)
        {
            var canvas = surface.SkCanvas;
            canvas.Save();
            canvas.Concat(Matrix(time));
            var isolate = amount < 1 || children.HasBlendedLayers;
            if (isolate)
            {
                using var fade = new SKPaint { Color = new SKColor(255, 255, 255, (byte)Math.Round(amount * 255)) };
                canvas.SaveLayer(fade);
            }

            children.RenderLayers(surface, time * timeDilation + timeOffset);
            if (isolate) canvas.Restore();
            canvas.Restore();
        }

        /// <summary>Synfig's summary transformation: <c>offset · axes(scale, angle, skew) · translate(−origin)</c>.</summary>
        private SKMatrix Matrix(double time)
        {
            // Bound to a bone: carried from the rest pose by the bone's frame, as Synfig's bone link on a transformation.
            if (bone is not null) return (bone.Link(time) * restInverse).ToSk();

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

        /// <summary>
        /// The summary transformation as nested groups, one transform each, so each animates on its own:
        /// <c>translate(offset) rotate(angle) skewX(−skew) scale(sx, sy·cos skew) translate(−origin)</c>.
        /// </summary>
        public override XElement ToSvg(SvgAnimationWriter svg, MotionTimeMap map)
        {
            var outer = SvgElement("g");
            Opacity(outer, svg, map);
            if (children.HasBlendedLayers) outer.SetAttributeValue("style", "isolation:isolate");

            var chain = bone is null ? NodeChain(svg, map) : BoneChain(svg, map);

            svg.Depth++;
            var content = children.LayersToSvg(svg, map.Then(timeDilation, timeOffset)).ToArray();
            svg.Depth--;

            // Innermost first: each transform wraps everything after it in the chain.
            object inner = content;
            for (var i = chain.Length - 1; i >= 0; i--)
            {
                chain[i].Add(inner);
                inner = chain[i];
            }

            outer.Add(inner);
            return outer;
        }

        /// <summary>
        /// A bound group's transform, sampled: the bone's frame times the inverse rest frame, decomposed per
        /// time as Synfig decomposes a matrix, with the angles unwrapped so a turn past ±180° does not spin back.
        /// </summary>
        private XElement[] BoneChain(SvgAnimationWriter svg, MotionTimeMap map)
        {
            var at = new Dictionary<double, (double X, double Y, double Angle, double Skew, double Sx, double Sy)>();
            double lastAngle = 0, lastSkew = 0;
            var first = true;
            foreach (var t in svg.Times)
            {
                var d = (bone!.Link(map.Local(t)) * restInverse).Decompose();
                var angle = first ? d.Angle : Unwrap(d.Angle, lastAngle);
                var skew = first ? d.Skew : Unwrap(d.Skew, lastSkew);
                (lastAngle, lastSkew, first) = (angle, skew, false);
                at[t] = (d.OffsetX, d.OffsetY, angle, skew, d.ScaleX, d.ScaleY * Math.Cos(skew * Math.PI / 180));
            }

            return new[]
            {
                svg.Transform("translate", svg.Track(t => at[t].X), v => $"{SvgAnimationWriter.N(v)} 0", 0),
                svg.Transform("translate", svg.Track(t => at[t].Y), v => $"0 {SvgAnimationWriter.N(v)}", 0),
                svg.Transform("rotate", svg.Track(t => at[t].Angle), SvgAnimationWriter.N, 0),
                svg.Transform("skewX", svg.Track(t => -at[t].Skew), SvgAnimationWriter.N, 0),
                svg.Transform("scale", svg.Track(t => at[t].Sx), v => $"{SvgAnimationWriter.N(v, 6)} 1", 1),
                svg.Transform("scale", svg.Track(t => at[t].Sy), v => $"1 {SvgAnimationWriter.N(v, 6)}", 1),
            }.Where(g => g.HasAttributes || g.HasElements).ToArray();

            static double Unwrap(double v, double previous)
            {
                while (v - previous > 180) v -= 360;
                while (v - previous < -180) v += 360;
                return v;
            }
        }

        private XElement[] NodeChain(SvgAnimationWriter svg, MotionTimeMap map)
        {
            var skewTrack = svg.Track(skewAngle, 0, map);
            var syTrack = skewTrack.IsStatic && Math.Abs(skewTrack.Values[0]) < 1e-12
                ? svg.Track(scale, 1, map)
                : svg.Track(t => scale.Evaluate(map.Local(t))[1] * Math.Cos(skewAngle.Evaluate(map.Local(t))[0] * Math.PI / 180));

            return new[]
            {
                svg.Transform("translate", svg.Track(offset, 0, map), v => $"{SvgAnimationWriter.N(v)} 0", 0),
                svg.Transform("translate", svg.Track(offset, 1, map), v => $"0 {SvgAnimationWriter.N(v)}", 0),
                svg.Transform("rotate", svg.Track(angle, 0, map), SvgAnimationWriter.N, 0),
                svg.Transform("skewX", Negate(skewTrack), SvgAnimationWriter.N, 0),
                svg.Transform("scale", svg.Track(scale, 0, map), v => $"{SvgAnimationWriter.N(v)} 1", 1),
                svg.Transform("scale", syTrack, v => $"1 {SvgAnimationWriter.N(v)}", 1),
                svg.Transform("translate", Negate(svg.Track(origin, 0, map)), v => $"{SvgAnimationWriter.N(v)} 0", 0),
                svg.Transform("translate", Negate(svg.Track(origin, 1, map)), v => $"0 {SvgAnimationWriter.N(v)}", 0),
            }.Where(g => g.HasAttributes || g.HasElements).ToArray();

            // Negating a track's values leaves its splines right: a spline shapes progress, not direction.
            static MotionTrack Negate(MotionTrack t) => t with { Values = [.. t.Values.Select(v => -v)] };
        }

        public override XElement ToSif(SifWriter sif) => Element("group", "0.3", sif,
            sif.Param("origin", origin),
            SifWriter.Param("transformation", bone is null
                ? new XElement("composite", new XAttribute("type", "transformation"),
                    sif.Link("offset", offset), sif.Link("angle", angle), sif.Link("skew_angle", skewAngle), sif.Link("scale", scale))
                : BoneTransformation(sif)),
            SifWriter.Param("canvas", new XElement("canvas", children.LayersToSif(sif))),
            SifWriter.Param("time_dilation", new XElement("real", new XAttribute("value", MotionTypes.Format(timeDilation)))),
            SifWriter.Param("time_offset", new XElement("time", new XAttribute("value", MotionTypes.Time(timeOffset)))),
            SifWriter.Param("children_lock", SifWriter.Bool(false)),
            SifWriter.Param("outline_grow", new XElement("real", new XAttribute("value", "0"))),
            SifWriter.Param("z_range", SifWriter.Bool(false)),
            SifWriter.Param("z_range_position", new XElement("real", new XAttribute("value", "0"))),
            SifWriter.Param("z_range_depth", new XElement("real", new XAttribute("value", "0"))),
            SifWriter.Param("z_range_blur", new XElement("real", new XAttribute("value", "0"))));

        /// <summary>A bone link on the transformation, its base value the inverse of the bone's rest frame.</summary>
        private XElement BoneTransformation(SifWriter sif)
        {
            var d = restInverse.Decompose();
            var local = new XElement("composite", new XAttribute("type", "transformation"),
                new XElement("offset", SifWriter.Value(MotionType.Vector, [d.OffsetX, d.OffsetY])),
                new XElement("angle", SifWriter.Value(MotionType.Angle, [d.Angle])),
                new XElement("skew_angle", SifWriter.Value(MotionType.Angle, [d.Skew])),
                new XElement("scale", SifWriter.Value(MotionType.Vector, [d.ScaleX, d.ScaleY])));
            return MotionBinding.Parse(bone, "group").Wrap(sif, "transformation", local);
        }
    }

    /// <summary>
    /// Synfig's skeleton layer: every bone of the composition drawn as a capsule from its origin to its
    /// tip, <c>width</c> and <c>tipwidth</c> its radii — how a rig is seen.
    /// </summary>
    public sealed class SkeletonLayer(MotionOptions o, IReadOnlyList<MotionBone> bones) : MotionLayer(o)
    {
        private readonly MotionNode color = o.Node("color", MotionType.Color, "#4a90e2");

        public override IEnumerable<MotionNode> Nodes() => [Amount, color];

        protected override void Draw(SkiaCanvas surface, double time, double amount)
        {
            using var path = Path(time, null);
            using var paint = Paint(color.Evaluate(time), amount);
            surface.SkCanvas.DrawPath(path, paint);
        }

        private SKPath Path(double time, int? segments)
        {
            var path = new SKPath { FillType = SKPathFillType.Winding };
            foreach (var bone in bones)
            {
                var points = bone.Capsule(time, segments);
                path.AddPoly([.. points], close: true);
            }

            return path;
        }

        /// <summary>
        /// Synfig's skeleton is an editor guide: excluded from rendering, its colour fixed, its only
        /// parameters <c>amount</c>, <c>name</c> and <c>bones</c>. So it is written that way, and draws only here.
        /// </summary>
        public override XElement ToSif(SifWriter sif) => new("layer",
            new XAttribute("type", "skeleton"), new XAttribute("active", "true"),
            new XAttribute("exclude_from_rendering", "true"), new XAttribute("version", "0.1"),
            Desc is null ? null : new XAttribute("desc", Desc),
            sif.Param("amount", Amount),
            SifWriter.Param("name", new XElement("string", "skeleton")),
            SifWriter.Param("bones", new XElement("static_list", new XAttribute("type", "bone_object"),
                bones.Select(b => new XElement("entry",
                    new XElement("bone", new XAttribute("type", "bone_object"), new XAttribute("guid", sif.Bone(b))))))));

        public override XElement ToSvg(SvgAnimationWriter svg, MotionTimeMap map)
        {
            const int segments = 12;
            var path = SvgElement("path");
            svg.Attribute(path, "d", t =>
            {
                var local = map.Local(t);
                return string.Concat(bones.Select(b =>
                {
                    var points = b.Capsule(local, segments);
                    return "M" + string.Join("L", points.Select(P)) + "Z";
                }));
            });
            Paint(path, "fill", color, svg, map);
            Opacity(path, svg, map);
            return path;
        }
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
    private static readonly HashSet<string> Keys = ["point", "t1", "t2", "width", "bone"];
    private readonly MotionNode point, t1, t2, width;
    private readonly MotionBinding? binding;
    #endregion

    #region Constructors
    private MotionSplinePoint(MotionNode point, MotionNode t1, MotionNode t2, MotionNode width, MotionBinding? binding = null, string? who = null)
    {
        (this.point, this.t1, this.t2, this.width, this.binding) = (point, t1, t2, width, binding);
        if (binding is null) return;

        // Fixed in the rest pose and measured once in the bones' frame, as Synfig Studio does when a vertex is linked.
        if (point is not MotionConstant || t1 is not MotionConstant || t2 is not MotionConstant)
            throw new ArgumentException($"{who} follows a bone, so its point and tangents are fixed values in the rest pose, not nodes.");
        var inverse = binding.Rest.Invert();
        var p = point.Evaluate(0);
        var (px, py) = inverse.Apply(p[0], p[1]);
        this.point = new MotionConstant(MotionType.Vector, [px, py]);
        this.t1 = Vector(t1);
        this.t2 = ReferenceEquals(t1, t2) ? this.t1 : Vector(t2);

        MotionNode Vector(MotionNode t)
        {
            var v = t.Evaluate(0);
            var (x, y) = inverse.ApplyVector(v[0], v[1]);
            return new MotionConstant(MotionType.Vector, [x, y]);
        }
    }
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
                    throw new ArgumentException($"{at} has no '{key}'. A point takes: point, t1, t2, width, bone.");
            }

            var p = d.Contains("point") ? d["point"] : item;
            var t1 = d["t1"] is { } a ? MotionNodeFactory.Node(a, MotionType.Vector, $"{at}'s t1") : Zero();
            var t2 = d["t2"] is { } b ? MotionNodeFactory.Node(b, MotionType.Vector, $"{at}'s t2") : t1;
            return new MotionSplinePoint(
                MotionNodeFactory.Node(p, MotionType.Vector, $"{at}'s point"), t1, t2,
                d["width"] is { } w ? MotionNodeFactory.Node(w, MotionType.Real, $"{at}'s width") : new MotionConstant(MotionType.Real, [1]),
                d["bone"] is { } bone ? MotionBinding.Parse(bone, $"{at}'s bone") : null, at);
        })];

        static MotionNode Zero() => new MotionConstant(MotionType.Vector, [0, 0]);
    }

    public State At(double time, double[] origin)
    {
        var p = point.Evaluate(time);
        var a = t1.Evaluate(time);
        var b = t2.Evaluate(time);
        if (binding?.At(time) is { } m)
        {
            // The vertex by the whole frame, the tangents by its linear part, as Synfig carries a linked point.
            var (px, py) = m.Apply(p[0], p[1]);
            var (ax, ay) = m.ApplyVector(a[0], a[1]);
            var (bx, by) = m.ApplyVector(b[0], b[1]);
            (p, a, b) = ([px, py], [ax, ay], [bx, by]);
        }

        return new State(
            new SKPoint((float)(p[0] + origin[0]), (float)(p[1] + origin[1])),
            new SKPoint((float)a[0], (float)a[1]),
            new SKPoint((float)b[0], (float)b[1]),
            width.Evaluate(time)[0]);
    }

    public IEnumerable<MotionNode> Nodes() => [point, t1, t2, width];

    public XElement ToSif(SifWriter sif)
    {
        var composite = new XElement("composite", new XAttribute("type", "bline_point"),
            sif.Link("point", point),
            sif.Link("width", width),
            new XElement("origin", new XElement("real", new XAttribute("value", "0.5"))),
            new XElement("split", SifWriter.Bool(true)),
            sif.Link("t1", t1),
            sif.Link("t2", t2),
            new XElement("split_radius", SifWriter.Bool(true)),
            new XElement("split_angle", SifWriter.Bool(true)));
        return binding is null ? composite : binding.Wrap(sif, "bline_point", composite);
    }
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

    #region Properties
    /// <summary>The call the options belong to, for messages.</summary>
    public string Who => who;
    #endregion

    #region Methods
    /// <summary>The given keys that were set, as a new options object.</summary>
    public Hashtable Subset(params string[] keys)
    {
        var subset = new Hashtable();
        foreach (var key in keys.Where(Has)) subset[key] = values[key];
        return subset;
    }

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
    public SifWriter(MotionComposition composition, string? stem)
    {
        this.composition = composition;
        this.stem = stem;

        var uses = new Dictionary<MotionNode, int>(ReferenceEqualityComparer.Instance);
        foreach (var node in composition.AllNodes()) Count(node);
        for (var i = 0; i < composition.Bones.Count; i++) bones[composition.Bones[i]] = Guid(i + 1);

        // A skeleton deformation pairs each bone with a fixed copy of it at rest; those copies, and their
        // ancestors so that they hang from the same places, go into the bones section too.
        var deformed = composition.AllLayers().OfType<MotionDeformationLayer>().SelectMany(d => d.Bones)
            .SelectMany(b => Ancestry(b)).ToHashSet(ReferenceEqualityComparer.Instance);
        foreach (var b in composition.Bones.Where(deformed.Contains)) restBones[b] = "B07D" + (restBones.Count + 1).ToString("X28");

        static IEnumerable<MotionBone> Ancestry(MotionBone b)
        {
            for (var x = b; x is not null; x = x.Parent) yield return x;
        }

        // Children before parents, because Synfig refuses a reference to a def it has not read yet.
        var ordered = new List<MotionNode>();
        var seen = new HashSet<MotionNode>(ReferenceEqualityComparer.Instance);
        foreach (var node in composition.AllNodes()) Visit(node);
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
    private readonly string? stem;
    private readonly Dictionary<MotionNode, string> ids = new(ReferenceEqualityComparer.Instance);
    private readonly MotionNode[] exportOrder;
    private readonly Dictionary<string, string> pictures = [];
    private readonly Dictionary<MotionBone, string> bones = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<MotionBone, string> restBones = new(ReferenceEqualityComparer.Instance);
    private readonly List<(string Name, byte[] Png)> files = [];
    #endregion

    #region Properties
    /// <summary>The pictures the file refers to, by the names it gives them.</summary>
    public IReadOnlyList<(string Name, byte[] Png)> Pictures => files;
    #endregion

    #region Methods
    /// <summary>The guid a bone is written and referred to under.</summary>
    public string Bone(MotionBone bone) => bones.TryGetValue(bone, out var guid)
        ? guid
        : throw new InvalidOperationException($"Bone '{bone.Name}' belongs to another composition, so this one cannot write it.");

    /// <summary>The guid of a bone's fixed rest copy, which a skeleton deformation pairs it with.</summary>
    public string RestBone(MotionBone bone) => restBones.TryGetValue(bone, out var guid)
        ? guid
        : throw new InvalidOperationException($"Bone '{bone.Name}' has no rest copy in this file.");

    // Synfig's guids are 32 hex digits. These only have to be distinct within the file.
    private static string Guid(int n) => "B07E" + n.ToString("X28");

    /// <summary>
    /// The <c>&lt;bones&gt;</c> section: Synfig's root bone, then every bone parent first, each with its
    /// links. It comes before <c>&lt;defs&gt;</c>, where Synfig expects it and allows references forward.
    /// </summary>
    private XElement? BonesSection()
    {
        if (bones.Count == 0) return null;
        const string root = "B07E" + "FFFFFFFFFFFFFFFFFFFFFFFFFFFF";
        return new XElement("bones",
            new XElement("bone_root", new XAttribute("type", "bone_object"), new XAttribute("guid", root)),
            composition.Bones.Select(b => new XElement("bone", new XAttribute("type", "bone_object"), new XAttribute("guid", bones[b]),
                new XElement("name", new XElement("string", b.Name)),
                new XElement("parent", new XElement("bone_valuenode", new XAttribute("type", "bone_object"),
                    new XAttribute("guid", b.Parent is null ? root : bones[b.Parent]))),
                Link("origin", b.Origin), Link("angle", b.Angle), Link("scalelx", b.Scalelx), Link("width", b.Width),
                Link("scalex", b.Scalex), Link("tipwidth", b.Tipwidth),
                new XElement("bone_depth", new XElement("real", new XAttribute("value", "0"))),
                Link("length", b.Length))),
            composition.Bones.Where(restBones.ContainsKey).Select(b => new XElement("bone",
                new XAttribute("type", "bone_object"), new XAttribute("guid", restBones[b]),
                new XElement("name", new XElement("string", b.Name + " (rest)")),
                new XElement("parent", new XElement("bone_valuenode", new XAttribute("type", "bone_object"),
                    new XAttribute("guid", b.Parent is null ? root : restBones[b.Parent]))),
                Fixed("origin", MotionType.Vector, b.Origin.Evaluate(0)),
                Fixed("angle", MotionType.Angle, [b.RestAngle]),
                Fixed("scalelx", MotionType.Real, [b.RestScalelx]),
                Fixed("width", MotionType.Real, b.Width.Evaluate(0)),
                Fixed("scalex", MotionType.Real, b.Scalex.Evaluate(0)),
                Fixed("tipwidth", MotionType.Real, b.Tipwidth.Evaluate(0)),
                new XElement("bone_depth", new XElement("real", new XAttribute("value", "0"))),
                Fixed("length", MotionType.Real, b.Length.Evaluate(0)))));

        static XElement Fixed(string name, MotionType kind, double[] value) => new(name, Value(kind, value));
    }

    /// <summary>The file name an image layer's picture is written under, beside the <c>.sif</c>.</summary>
    public string Picture(MotionPicture picture)
    {
        if (stem is null)
            throw new InvalidOperationException(
                "This composition has an image layer, whose picture is written beside the .sif: use composition.saveSif(path) instead of toSif().");
        // Keyed on the pixels, so the pieces of one cut-out sheet share one file.
        var key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(picture.Png));
        if (pictures.TryGetValue(key, out var name)) return name;
        name = $"{stem}-image{pictures.Count + 1}.png";
        pictures[key] = name;
        files.Add((name, picture.Png));
        return name;
    }

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
            BonesSection(),
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
