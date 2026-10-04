namespace Polson.Animation;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

/// <summary>Number formatting for SVG output: invariant, and no longer than it needs to be.</summary>
internal static class MotionSvgFormat
{
    #region Methods
    public static string Number(double v, int decimals = 3)
    {
        var s = Math.Round(v, decimals).ToString("0." + new string('#', decimals), CultureInfo.InvariantCulture);
        return s == "-0" ? "0" : s;
    }
    #endregion
}

/// <summary>A layer's clock as a function of the composition's: <c>local = A × global + B</c>.</summary>
/// <remarks>Groups compose it: a child of a group with <c>timeDilation</c> d and <c>timeOffset</c> o sees <c>d × local + o</c>.</remarks>
internal readonly record struct MotionTimeMap(double A, double B)
{
    #region Properties
    public static MotionTimeMap Identity => new(1, 0);
    #endregion

    #region Methods
    public double Local(double global) => A * global + B;

    public double Global(double local) => (local - B) / A;

    public MotionTimeMap Then(double dilation, double offset) => new(A * dilation, B * dilation + offset);
    #endregion
}

/// <summary>
/// One animated number over the composition: its values at key times, and the splines between them
/// when it was written exactly. A track with one value is static.
/// </summary>
internal sealed record MotionTrack(double[] Times, double[] Values, string[]? Splines)
{
    public bool IsStatic => Values.Length == 1;
}

/// <summary>
/// Writes a composition as an animated SVG: SMIL <c>&lt;animate&gt;</c> and <c>&lt;animateTransform&gt;</c>
/// on ordinary elements, so it plays in a browser, in an <c>&lt;img&gt;</c>, and in Svg.Skia.
/// </summary>
/// <remarks>
/// A keyed value is written exactly, as <c>keySplines</c>, wherever SMIL can say it; anything else — a
/// formula, an overshooting curve, a colour, a path — is sampled at the composition's frame rate and
/// interpolated linearly between samples, so every frame time is exact and the motion between frames is
/// close. Paths are always written with the same commands, so their <c>d</c> can be interpolated.
/// </remarks>
internal sealed class SvgAnimationWriter
{
    #region Constructors
    public SvgAnimationWriter(MotionComposition composition, bool loop, double fps)
    {
        this.composition = composition;
        this.loop = loop;
        Duration = composition.Duration;
        var count = Duration <= 0 ? 1 : (int)Math.Ceiling(Duration * fps - 1e-9) + 1;
        Times = [.. Enumerable.Range(0, count).Select(i => Math.Min(Duration, i / fps))];
    }
    #endregion

    #region Fields
    private readonly MotionComposition composition;
    private readonly bool loop;
    private readonly List<XElement> defs = [];
    private string? black;
    private int ids;
    #endregion

    #region Properties
    public double Duration { get; }

    /// <summary>The composition's view, <c>[left, top, right, bottom]</c>.</summary>
    public double[] View => composition.View;

    /// <summary>The global times every sampled track is evaluated at.</summary>
    public double[] Times { get; }

    public int ExactTracks { get; private set; }

    public int SampledTracks { get; private set; }

    /// <summary>How deep in groups the layer being written is; a fill at the root covers the view, inside a group much more.</summary>
    public int Depth { get; set; }
    #endregion

    #region Methods
    public string Write()
    {
        var c = composition;
        var v = c.View;
        var sx = c.Width / (v[2] - v[0]);
        var sy = c.Height / (v[3] - v[1]);
        var body = new XElement(Svg + "g", c.LayersToSvg(this, MotionTimeMap.Identity));
        if (Math.Abs(sx - 1) > 1e-12 || Math.Abs(sy - 1) > 1e-12 || v[0] != 0 || v[1] != 0)
            body.SetAttributeValue("transform", $"matrix({N(sx)} 0 0 {N(sy)} {N(-v[0] * sx)} {N(-v[1] * sy)})");

        // The view is the composition's own: a blend reaches nothing under the SVG.
        if (c.HasBlendedLayers) body.SetAttributeValue("style", "isolation:isolate");

        var root = new XElement(Svg + "svg",
            new XAttribute("width", c.Width), new XAttribute("height", c.Height),
            new XAttribute("viewBox", $"0 0 {c.Width} {c.Height}"),
            new XAttribute(XNamespace.Xmlns + "xlink", Xlink),
            new XComment($" Polson motion: {N(Duration)} s, {ExactTracks} exact track(s), {SampledTracks} sampled at {N(Times.Length > 1 ? 1 / (Times[1] - Times[0]) : 0)} fps "
                + (BlendedLayers == 0 ? "" : $"; {BlendedLayers} layer(s) blend by CSS mix-blend-mode, which agrees with Synfig only over an opaque backdrop ")),
            defs.Count == 0 ? null : new XElement(Svg + "defs", defs),
            body);

        var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false), OmitXmlDeclaration = false };
        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, settings)) new XDocument(root).Save(writer);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    public static readonly XNamespace Xlink = "http://www.w3.org/1999/xlink";

    /// <summary>
    /// A square far larger than any view, as path data: put in front of a shape's own path with
    /// <c>fill-rule="evenodd"</c>, it inverts the shape. Inside a group it must survive the group's transform.
    /// </summary>
    public const string Everything = "M-100000 -100000H100000V100000H-100000Z";

    public static string N(double v) => MotionSvgFormat.Number(v);

    public static string N(double v, int decimals) => MotionSvgFormat.Number(v, decimals);

    /// <summary>
    /// Writes a stack of layers, honouring each one's blend: <c>behind</c> goes under what came before,
    /// <c>alphaOver</c> masks it, and the separable methods become CSS <c>mix-blend-mode</c>, which agrees
    /// with Synfig over an opaque backdrop. Anything else is refused by name.
    /// </summary>
    public IEnumerable<XElement> Stack(IEnumerable<(MotionBlend Blend, string? Desc, XElement Element)> layers)
    {
        var output = new List<XElement>();
        foreach (var (blend, desc, element) in layers)
        {
            switch (blend.Name)
            {
                case "composite":
                    output.Add(element);
                    break;
                case "behind":
                    output.Insert(0, element);
                    break;
                case "alphaOver":
                    var erased = Erase(output, element);
                    output = [erased];
                    break;
                default:
                    if (blend.Css is null)
                        throw new InvalidOperationException(
                            $"The layer{(desc is null ? "" : $" '{desc}'")} blends by '{blend.Name}', which SVG cannot say. "
                            + "In SVG a layer can composite, go behind, erase (alphaOver), or use multiply, screen, overlay, "
                            + "hardLight, brighten, darken or difference. Capture the composition as frames instead.");
                    var style = element.Attribute("style")?.Value;
                    element.SetAttributeValue("style", (style is null ? "" : style + ";") + $"mix-blend-mode:{blend.Css}");
                    BlendedLayers++;
                    output.Add(element);
                    break;
            }
        }

        return output;
    }

    /// <summary>How many layers were written with a CSS blend mode, which agrees with Synfig only over an opaque backdrop.</summary>
    public int BlendedLayers { get; private set; }

    /// <summary>
    /// Wraps what is below in a mask that removes where the eraser is: a white field with the eraser drawn
    /// black over it, keeping the eraser's own coverage and opacity.
    /// </summary>
    private XElement Erase(List<XElement> below, XElement eraser)
    {
        if (black is null)
        {
            black = Id("black");
            defs.Add(new XElement(Svg + "filter", new XAttribute("id", black),
                new XAttribute("x", "-100000"), new XAttribute("y", "-100000"),
                new XAttribute("width", "200000"), new XAttribute("height", "200000"),
                new XAttribute("filterUnits", "userSpaceOnUse"),
                new XElement(Svg + "feColorMatrix", new XAttribute("type", "matrix"),
                    new XAttribute("values", "0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 1 0"))));
        }

        var id = Id("erase");
        defs.Add(new XElement(Svg + "mask", new XAttribute("id", id),
            new XAttribute("maskUnits", "userSpaceOnUse"), new XAttribute("maskContentUnits", "userSpaceOnUse"),
            new XAttribute("x", "-100000"), new XAttribute("y", "-100000"),
            new XAttribute("width", "200000"), new XAttribute("height", "200000"),
            new XElement(Svg + "rect", new XAttribute("x", "-100000"), new XAttribute("y", "-100000"),
                new XAttribute("width", "200000"), new XAttribute("height", "200000"), new XAttribute("fill", "#ffffff")),
            new XElement(Svg + "g", new XAttribute("filter", $"url(#{black})"), eraser)));
        return new XElement(Svg + "g", new XAttribute("mask", $"url(#{id})"), below);
    }

    private string Id(string prefix) => $"{prefix}{++ids}";

    /// <summary>One component of a node over the composition, exact where possible.</summary>
    public MotionTrack Track(MotionNode node, int component, MotionTimeMap map, Func<double, double>? shape = null)
    {
        if (shape is null && TryExact(node, component, map) is { } exact) return exact;
        var f = shape ?? (x => x);
        return Sampled(Times.Select(t => f(node.Evaluate(map.Local(t))[component])).ToArray());
    }

    /// <summary>A number computed from the global time, sampled.</summary>
    public MotionTrack Track(Func<double, double> at) => Sampled([.. Times.Select(at)]);

    /// <summary>Sets an attribute to a track: a plain value when static, otherwise its first value and an <c>&lt;animate&gt;</c>.</summary>
    public void Attribute(XElement element, string name, MotionTrack track, Func<double, string>? format = null)
    {
        var f = format ?? N;
        element.SetAttributeValue(name, f(track.Values[0]));
        if (track.IsStatic) return;
        element.Add(Animate("animate", name, track, f, null));
    }

    /// <summary>Sets a text attribute (a path, a colour) from the global time, sampled; plain when it never changes.</summary>
    public void Attribute(XElement element, string name, Func<double, string> at)
    {
        var values = Times.Select(at).ToArray();
        element.SetAttributeValue(name, values[0]);
        if (values.All(x => x == values[0])) return;

        SampledTracks++;
        element.Add(new XElement(Svg + "animate",
            new XAttribute("attributeName", name),
            new XAttribute("values", string.Join(";", values)),
            new XAttribute("keyTimes", string.Join(";", Times.Select(KeyTime))),
            Timing()));
    }

    /// <summary>A group carrying one transform: static as an attribute, animated as an <c>&lt;animateTransform&gt;</c>.</summary>
    public XElement Transform(string type, MotionTrack track, Func<double, string> format, double identity)
    {
        var g = new XElement(Svg + "g");
        if (track.IsStatic)
        {
            if (Math.Abs(track.Values[0] - identity) > 1e-12) g.SetAttributeValue("transform", $"{type}({format(track.Values[0])})");
            return g;
        }

        g.Add(Animate("animateTransform", "transform", track, format, type));
        return g;
    }

    /// <summary>A refusal for a layer SVG cannot carry.</summary>
    public static InvalidOperationException Refuse(string what, string? desc) => new(
        $"The {what}{(desc is null ? "" : $" '{desc}'")} cannot be written as animated SVG: SMIL cannot run a script. "
        + "Write the composition without it, or capture it as frames.");

    private XElement Animate(string element, string attribute, MotionTrack track, Func<double, string> format, string? type) =>
        new(Svg + element,
            new XAttribute("attributeName", attribute),
            type is null ? null : new XAttribute("type", type),
            new XAttribute("values", string.Join(";", track.Values.Select(format))),
            new XAttribute("keyTimes", string.Join(";", track.Times.Select(KeyTime))),
            track.Splines is null ? null : new XAttribute("calcMode", "spline"),
            track.Splines is null ? null : new XAttribute("keySplines", string.Join(";", track.Splines)),
            Timing());

    private object[] Timing() =>
    [
        new XAttribute("dur", $"{N(Duration)}s"),
        loop ? new XAttribute("repeatCount", "indefinite") : new XAttribute("fill", "freeze")
    ];

    private string KeyTime(double t) => MotionSvgFormat.Number(Duration <= 0 ? 0 : Math.Clamp(t / Duration, 0, 1), 6);

    private MotionTrack Sampled(double[] values)
    {
        if (values.All(x => Math.Abs(x - values[0]) < 1e-9)) return new MotionTrack([0], [values[0]], null);
        SampledTracks++;
        return new MotionTrack(Times, values, null);
    }

    /// <summary>A keyed value written as SMIL keys, its clock mapped through the groups above it.</summary>
    private MotionTrack? TryExact(MotionNode node, int component, MotionTimeMap map)
    {
        switch (node)
        {
            case MotionConstant c:
                return new MotionTrack([0], [c.Evaluate(0)[component]], null);
            case MotionComposite composite when component < 2:
                return TryExact(component == 0 ? composite.X : composite.Y, 0, map);
            case MotionLinear linear when linear.IsStraight && Duration > 0:
            {
                // A straight line in local time is a straight line in global time: two keys, linear between.
                var from = linear.Evaluate(map.Local(0))[component];
                var to = linear.Evaluate(map.Local(Duration))[component];
                if (Math.Abs(to - from) < 1e-12) return new MotionTrack([0], [from], null);
                ExactTracks++;
                return new MotionTrack([0, Duration], [from, to], null);
            }
            case MotionAnimated animated when map.A > 0 && Duration > 0
                && animated.TryKeySplines(component, out var keys, out var splines):
            {
                var times = keys.Select(k => map.Global(k.Time)).ToList();
                var values = keys.Select(k => k.Value).ToList();
                if (times[0] < -1e-9 || times[^1] > Duration + 1e-9) return null;
                if (values.All(x => Math.Abs(x - values[0]) < 1e-9)) return new MotionTrack([0], [values[0]], null);

                if (times[0] > 1e-9)
                {
                    times.Insert(0, 0);
                    values.Insert(0, values[0]);
                    splines.Insert(0, "0 0 1 1");
                }

                if (times[^1] < Duration - 1e-9)
                {
                    times.Add(Duration);
                    values.Add(values[^1]);
                    splines.Add("0 0 1 1");
                }

                ExactTracks++;
                return new MotionTrack([.. times], [.. values], [.. splines]);
            }
            default:
                return null;
        }
    }
    #endregion
}
