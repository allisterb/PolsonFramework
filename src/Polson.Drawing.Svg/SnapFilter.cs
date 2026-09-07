namespace Polson.Drawing.Svg;

using System;
using System.Globalization;
using System.Linq;

// File-scoped, not global: Svg.FilterEffects.SvgImage would otherwise collide with Svg.SvgImage
// everywhere else in the project.
using global::Svg.FilterEffects;


/// <summary>
/// An SVG <c>&lt;filter&gt;</c>, built one primitive at a time and referenced by id.
/// </summary>
/// <remarks>
/// <para>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>f.gaussianBlur(6)</c> reaches
/// <see cref="GaussianBlur"/>. The camelCase form is the one documented in
/// <c>docs/Polson.core.md</c> and the studio manuals.
/// </para>
/// <para>
/// <b>This is most of the raster gap, closed natively.</b> The vector surface has no
/// <c>Skia.Brush</c>, <c>Skia.PathEffect</c>, <c>Skia.MaskFilter</c> or SkSL shader, because all four
/// take a canvas context — and the standing conclusion was that grain, soft edges and colour grading
/// were therefore unavailable in a vector deliverable. They are not. SVG has its own filter chain,
/// the renderer draws it, and the only thing missing was a way to build one:
/// </para>
/// <list type="bullet">
/// <item><c>feTurbulence</c> <b>is</b> Perlin noise — the same algorithm <c>Skia.Shader.perlinNoise*</c>
/// wraps, whose own documentation calls it "faithful to SVG <c>feTurbulence</c>".</item>
/// <item><c>feGaussianBlur</c> is the soft edge that <c>Skia.MaskFilter.blur</c> gives on canvas.</item>
/// <item><c>feColorMatrix</c> is <c>Skia.ColorFilter.colorMatrix</c>.</item>
/// <item><c>feTurbulence</c> driving <c>feDisplacementMap</c> roughens a contour, which is the vector
/// idiom nearest a drawn rather than plotted line.</item>
/// </list>
/// <para>
/// Every primitive sets the library's <b>typed</b> properties rather than custom attributes, because
/// the in-memory renderer reads the properties and would ignore an attribute that only appears once
/// the document is serialised — a filter that worked through <c>outSvg</c> and did nothing in the
/// peek would be the worst of both.
/// </para>
/// </remarks>
public class SnapFilter : SnapElement
{
    #region Constructors
    public SnapFilter(SvgFilter filter, SnapPaper? paper = null) : base(filter, paper) { }
    #endregion

    #region Properties
    public SvgFilter FilterNode => (SvgFilter)Node;

    /// <summary>The reference to put in a <c>filter</c> attribute: <c>url(#id)</c>.</summary>
    /// <remarks>
    /// A filter is useless without its reference and the id is generated, so handing back the whole
    /// <c>url(#…)</c> string saves every caller assembling it and getting the parentheses wrong.
    /// </remarks>
    public string Url => "url(#" + Id + ")";
    #endregion

    #region Methods — primitives
    /// <summary>
    /// Procedural noise. <c>type</c> is <c>'fractalNoise'</c> (cloudy) or <c>'turbulence'</c> (wispier).
    /// </summary>
    /// <remarks>
    /// The vector counterpart of <c>Skia.Shader.perlinNoiseFractal</c>. A useful
    /// <paramref name="baseFrequency"/> is small — 0.01 for broad cloud, 0.6 to 0.9 for paper grain.
    /// </remarks>
    public SnapFilter Turbulence(float baseFrequency, int octaves = 3, string type = "fractalNoise",
        float seed = 0f, string? result = null)
    {
        var node = new SvgTurbulence
        {
            BaseFrequency = new SvgNumberCollection { baseFrequency },
            NumOctaves = Math.Max(1, octaves),
            Seed = seed,
            Type = type.Equals("turbulence", StringComparison.OrdinalIgnoreCase)
                ? SvgTurbulenceType.Turbulence
                : SvgTurbulenceType.FractalNoise,
        };
        return Add(node, null, result);
    }

    /// <summary>A Gaussian blur — the soft edge <c>Skia.MaskFilter.blur</c> gives on canvas.</summary>
    public SnapFilter GaussianBlur(float stdDeviation, string? input = null, string? result = null)
    {
        var node = new SvgGaussianBlur { StdDeviation = new SvgNumberCollection { stdDeviation } };
        return Add(node, input, result);
    }

    /// <summary>
    /// A 4×5 colour matrix, as twenty numbers — the vector counterpart of
    /// <c>Skia.ColorFilter.colorMatrix</c>. Pass <c>type</c> <c>'saturate'</c>, <c>'hueRotate'</c> or
    /// <c>'luminanceToAlpha'</c> with a single value instead where one of those will do.
    /// </summary>
    public SnapFilter ColorMatrix(object values, string type = "matrix", string? input = null, string? result = null)
    {
        var node = new SvgColourMatrix
        {
            Type = type.ToLowerInvariant() switch
            {
                "saturate" => SvgColourMatrixType.Saturate,
                "huerotate" or "hue-rotate" => SvgColourMatrixType.HueRotate,
                "luminancetoalpha" or "luminance-to-alpha" => SvgColourMatrixType.LuminanceToAlpha,
                _ => SvgColourMatrixType.Matrix,
            },
            Values = Numbers(values),
        };
        return Add(node, input, result);
    }

    /// <summary>
    /// Displaces one input by the channels of another — with <see cref="Turbulence"/> as the second
    /// input, the way a contour is roughened so it reads as drawn rather than plotted.
    /// </summary>
    public SnapFilter DisplacementMap(float scale, string? input = null, string? input2 = null,
        string xChannel = "R", string yChannel = "G", string? result = null)
    {
        var node = new SvgDisplacementMap
        {
            Scale = scale,
            XChannelSelector = Channel(xChannel),
            YChannelSelector = Channel(yChannel),
            Input2 = input2 ?? string.Empty,
        };
        return Add(node, input, result);
    }

    /// <summary>Shifts an input, for a hard offset shadow or a registration effect.</summary>
    public SnapFilter Offset(float dx, float dy, string? input = null, string? result = null) =>
        Add(new SvgOffset { Dx = new SvgUnit(dx), Dy = new SvgUnit(dy) }, input, result);

    /// <summary>Fills the filter region with a flat colour, normally to composite against.</summary>
    public SnapFilter Flood(string color, float opacity = 1f, string? result = null)
    {
        var node = new SvgFlood { FloodOpacity = opacity };
        node.FloodColor = new SvgColourServer(SnapAttributes.ParseColor(color));
        return Add(node, null, result);
    }

    /// <summary>Combines two inputs. <c>op</c> is <c>over</c>, <c>in</c>, <c>out</c>, <c>atop</c>, <c>xor</c> or <c>arithmetic</c>.</summary>
    public SnapFilter Composite(string op = "over", string? input = null, string? input2 = null, string? result = null)
    {
        var node = new SvgComposite
        {
            Operator = op.ToLowerInvariant() switch
            {
                "in" => SvgCompositeOperator.In,
                "out" => SvgCompositeOperator.Out,
                "atop" => SvgCompositeOperator.Atop,
                "xor" => SvgCompositeOperator.Xor,
                "arithmetic" => SvgCompositeOperator.Arithmetic,
                _ => SvgCompositeOperator.Over,
            },
            Input2 = input2 ?? string.Empty,
        };
        return Add(node, input, result);
    }

    /// <summary>Blends two inputs with a named mode — <c>multiply</c>, <c>screen</c>, <c>darken</c>, <c>lighten</c>, <c>normal</c>.</summary>
    public SnapFilter Blend(string mode = "normal", string? input = null, string? input2 = null, string? result = null)
    {
        var node = new SvgBlend
        {
            Mode = mode.ToLowerInvariant() switch
            {
                "multiply" => SvgBlendMode.Multiply,
                "screen" => SvgBlendMode.Screen,
                "darken" => SvgBlendMode.Darken,
                "lighten" => SvgBlendMode.Lighten,
                _ => SvgBlendMode.Normal,
            },
            Input2 = input2 ?? string.Empty,
        };
        return Add(node, input, result);
    }

    /// <summary>Stacks named results back together, last on top.</summary>
    public SnapFilter Merge(params object[] inputs)
    {
        var merge = new SvgMerge();
        foreach (var name in inputs.Select(i => i?.ToString()).Where(n => !string.IsNullOrWhiteSpace(n)))
        {
            merge.Children.Add(new SvgMergeNode { Input = name! });
        }
        FilterNode.Children.Add(merge);
        return this;
    }

    /// <summary>A drop shadow in one primitive, rather than offset + blur + flood + composite.</summary>
    public SnapFilter DropShadow(float dx, float dy, float stdDeviation, string color = "#000000",
        float opacity = 0.5f, string? input = null, string? result = null)
    {
        var node = new SvgDropShadow
        {
            Dx = new SvgUnit(dx),
            Dy = new SvgUnit(dy),
            StdDeviation = new SvgNumberCollection { stdDeviation },
            FloodOpacity = opacity,
        };
        node.FloodColor = new SvgColourServer(SnapAttributes.ParseColor(color));
        return Add(node, input, result);
    }

    /// <summary>Thickens (<c>dilate</c>) or thins (<c>erode</c>) a shape — the vector <c>Skia.ImageFilter.dilate</c>.</summary>
    public SnapFilter Morphology(float radius, string op = "dilate", string? input = null, string? result = null)
    {
        var node = new SvgMorphology
        {
            Operator = op.Equals("erode", StringComparison.OrdinalIgnoreCase)
                ? SvgMorphologyOperator.Erode
                : SvgMorphologyOperator.Dilate,
            Radius = new SvgNumberCollection { radius },
        };
        return Add(node, input, result);
    }

    /// <summary>
    /// Widens the filter region, as fractions of the filtered element's box.
    /// </summary>
    /// <remarks>
    /// A blur or a displacement reaches outside the shape, and the default region — <c>-10%</c> to
    /// <c>110%</c> — clips it. A wide blur that looks cropped on all four sides needs this, not a
    /// smaller deviation.
    /// </remarks>
    public SnapFilter Region(float x, float y, float width, float height)
    {
        FilterNode.X = new SvgUnit(SvgUnitType.Percentage, x * 100f);
        FilterNode.Y = new SvgUnit(SvgUnitType.Percentage, y * 100f);
        FilterNode.Width = new SvgUnit(SvgUnitType.Percentage, width * 100f);
        FilterNode.Height = new SvgUnit(SvgUnitType.Percentage, height * 100f);
        return this;
    }
    #endregion

    #region Private methods
    /// <summary>Appends a primitive, wiring its <c>in</c> and <c>result</c> where they were named.</summary>
    private SnapFilter Add(SvgFilterPrimitive node, string? input, string? result)
    {
        if (!string.IsNullOrWhiteSpace(input)) node.Input = input;
        if (!string.IsNullOrWhiteSpace(result)) node.Result = result;
        FilterNode.Children.Add(node);
        return this;
    }

    private static SvgChannelSelector Channel(string name) => name.ToUpperInvariant() switch
    {
        "R" => SvgChannelSelector.R,
        "G" => SvgChannelSelector.G,
        "B" => SvgChannelSelector.B,
        _ => SvgChannelSelector.A,
    };

    /// <summary>A number list from an array or a space-separated string, so both spellings work from JS.</summary>
    private static string Numbers(object values) => values switch
    {
        string s => s,
        System.Collections.IEnumerable list => string.Join(" ", list.Cast<object?>()
            .Select(v => Convert.ToSingle(v, CultureInfo.InvariantCulture).ToString("0.#####", CultureInfo.InvariantCulture))),
        _ => Convert.ToSingle(values, CultureInfo.InvariantCulture).ToString("0.#####", CultureInfo.InvariantCulture),
    };
    #endregion
}
