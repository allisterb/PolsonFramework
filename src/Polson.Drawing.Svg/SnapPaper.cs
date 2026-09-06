namespace Polson.Drawing.Svg;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

/// <summary>Root SVG document — the object a vector script draws onto.</summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// </remarks>
public class SnapPaper : SnapElement
{
    #region Constructors
    public SnapPaper(SvgDocument document) : base(document)
    {
        Document = document;
        Paper = this;
        EnsureDefs();
    }

    public SnapPaper(float width = 800f, float height = 600f) : this(CreateDocument(width, height))
    {
    }

    public SnapPaper(string width, string height) : this(CreateDocument(width, height))
    {
    }
    #endregion

    #region Properties
    public SvgDocument Document { get; }

    public float Width
    {
        get => Document.Width.Value;
        set => Document.Width = new SvgUnit(value);
    }

    public float Height
    {
        get => Document.Height.Value;
        set => Document.Height = new SvgUnit(value);
    }

    public SnapElement Defs => _defs ??= EnsureDefs();

    public VectorLogoToolkit VectorLogo { get; } = new();

    /// <summary>Vector chart drawing, also reachable as <c>paper.chart(model)</c>.</summary>
    public VectorChartToolkit VectorChart { get; } = new();
    public VectorLogoToolkit Logo => VectorLogo;
    #endregion

    #region Methods
    // Rect, Circle, Ellipse, Path, G, Group, Image, Text, Line, Use and El are deliberately absent:
    // they are inherited. `SnapPaper(SvgDocument)` passes the document to `base(document)`, so a
    // paper's `Node` *is* its `Document` and its `Paper` is itself — which made the twelve copies
    // that used to live here character-identical to the virtuals they hid once those two spellings
    // were normalised. Each one also raised CS0114, because a copy that neither overrides nor hides
    // explicitly is the compiler asking which was meant.
    //
    // `Clear` was the one that genuinely differed, and it is below as an override. That is the whole
    // value of removing the rest: the one method that really does something else is now visible as
    // such, instead of being the twelfth near-identical body in a row.

    public SnapPolyline Polyline(params object[] points)
    {
        var polyline = new SvgPolyline();
        SnapAttributes.ApplyAttribute(polyline, "points", points);
        Document.Children.Add(polyline);
        return new SnapPolyline(polyline, this);
    }

    public SnapPolygon Polygon(params object[] points)
    {
        var polygon = new SvgPolygon();
        SnapAttributes.ApplyAttribute(polygon, "points", points);
        Document.Children.Add(polygon);
        return new SnapPolygon(polygon, this);
    }

    #region Filters and Stylesheets
    /// <summary>
    /// Creates a <c>&lt;filter&gt;</c> in <c>&lt;defs&gt;</c> and returns it for chaining.
    /// </summary>
    /// <remarks>
    /// Reference it with the filter's own <c>url</c>:
    /// <code>
    /// const grain = paper.filter().turbulence(0.85, 3).colorMatrix('0 0 0 0 0.1 …');
    /// paper.rect(0, 0, w, h).attr({ filter: grain.url });
    /// </code>
    /// </remarks>
    public SnapFilter Filter(string? id = null)
    {
        var filter = new global::Svg.FilterEffects.SvgFilter
        {
            ID = string.IsNullOrWhiteSpace(id) ? "filter_" + Guid.NewGuid().ToString("N")[..8] : id,
        };
        Defs.Node.Children.Add(filter);
        return new SnapFilter(filter, this);
    }

    /// <summary>
    /// Applies a CSS stylesheet to everything drawn so far, and keeps the <c>&lt;style&gt;</c> block.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Call it last.</b> The rules are resolved against the tree as it stands at the moment of the
    /// call, so elements drawn afterwards are not styled — which is how a stylesheet behaves on a
    /// finished document, and the only semantics that does not require re-running on every mutation.
    /// Call it again to pick up later work; applying the same sheet twice is harmless.
    /// </para>
    /// <para>
    /// Both halves happen: the declarations are written onto matching elements as attributes so the
    /// <i>render</i> is right, and the stylesheet is appended so the <i>deliverable</i> still carries
    /// one rule a designer can edit in Illustrator instead of ninety baked attributes. Writing only
    /// the block would render as nothing — the library resolves CSS inside its parser, not in memory,
    /// so a class-styled rect came back black in the peek and correct in the file.
    /// </para>
    /// <para>
    /// Selectors are <c>.class</c>, <c>#id</c>, a bare tag name, <c>*</c> or <c>:root</c>, singly or
    /// comma-separated. Anything more — descendants, attributes — is left to <c>.attr(...)</c>
    /// rather than silently matching nothing.
    /// </para>
    /// </remarks>
    /// <returns>How many elements were styled, so an empty sheet is distinguishable from a typo.</returns>
    public int Style(string css)
    {
        var styled = SnapStylesheet.Apply(this, css);

        var style = new NonSvgElement("style", "http://www.w3.org/2000/svg");
        style.Nodes.Add(new SvgContentNode { Content = css ?? string.Empty });
        Document.Children.Insert(0, style);
        return styled;
    }


    #endregion

    #region Vector Chart Methods
    /// <summary>
    /// Draws any <c>Chart.create*</c> model onto this paper as real SVG elements, returning the group.
    /// </summary>
    /// <remarks>
    /// The vector counterpart of <c>Chart.drawChart(ctx, model)</c>. The models were always
    /// surface-agnostic arithmetic; this is the drawing half they lacked. Ticks and labels are not
    /// drawn, exactly as on canvas — they are data, and their typography is the caller's.
    /// </remarks>
    public SnapGroup Chart(object chartModel, object? options = null) =>
        VectorChart.DrawChart(this, chartModel, options);
    #endregion

    #region Vector Logo Methods
    public SnapPath Squircle(float x, float y, float width, float height, float exponent = 4.5f) =>
        VectorLogo.Squircle(this, x, y, width, height, exponent);

    public SnapPath GoldenSpiral(float startX, float startY, float initialRadius, float turns = 3f, int segmentsPerTurn = 36) =>
        VectorLogo.GoldenSpiral(this, startX, startY, initialRadius, turns, segmentsPerTurn);

    public SnapPath EmblemBadge(float cx, float cy, float width, float height, string style = "shield") =>
        VectorLogo.EmblemBadge(this, cx, cy, width, height, style);

    public SnapGroup GoldenCircles(float cx, float cy, float baseRadius, int count = 5, object? options = null) =>
        VectorLogo.GoldenCircles(this, cx, cy, baseRadius, count, options);

    public SnapGroup IsometricGrid(float width, float height, float spacing = 40f, object? options = null) =>
        VectorLogo.IsometricGrid(this, width, height, spacing, options);

    public SnapGroup PolarGrid(float cx, float cy, float maxRadius, int ringCount = 5, int rayCount = 12, object? options = null) =>
        VectorLogo.PolarGrid(this, cx, cy, maxRadius, ringCount, rayCount, options);

    public SnapGroup MonogramMatrix(float x, float y, float width, float height, string type = "3x3", object? options = null) =>
        VectorLogo.MonogramMatrix(this, x, y, width, height, type, options);

    public SnapGroup ClearSpaceGuide(float x, float y, float width, float height, float margin = 24f, object? options = null) =>
        VectorLogo.ClearSpaceGuide(this, x, y, width, height, margin, options);

    public SnapPath OgeeCurve(float x1, float y1, float x2, float y2, float amplitude = 25f, float inflectionT = 0.5f) =>
        VectorLogo.OgeeCurve(this, x1, y1, x2, y2, amplitude, inflectionT);
    #endregion

    /// <summary>Appends a nested <c>&lt;svg&gt;</c> viewport, for placing a sub-scene in its own coordinate space.</summary>
    public SnapElement Svg(float x, float y, float width, float height)
    {
        var fragment = new SvgFragment
        {
            X = new SvgUnit(x),
            Y = new SvgUnit(y),
            Width = new SvgUnit(width),
            Height = new SvgUnit(height)
        };
        Document.Children.Add(fragment);
        return new SnapElement(fragment, this);
    }

    public SnapLinearGradient GradientLinear(float x1, float y1, float x2, float y2)
    {
        var grad = new SvgLinearGradientServer
        {
            ID = "grad_" + Guid.NewGuid().ToString("N")[..8],
            X1 = new SvgUnit(SvgUnitType.Percentage, x1 * 100f),
            Y1 = new SvgUnit(SvgUnitType.Percentage, y1 * 100f),
            X2 = new SvgUnit(SvgUnitType.Percentage, x2 * 100f),
            Y2 = new SvgUnit(SvgUnitType.Percentage, y2 * 100f)
        };
        Defs.Node.Children.Add(grad);
        return new SnapLinearGradient(grad, this);
    }

    public SnapRadialGradient GradientRadial(float cx, float cy, float r, float? fx = null, float? fy = null)
    {
        var grad = new SvgRadialGradientServer
        {
            ID = "grad_" + Guid.NewGuid().ToString("N")[..8],
            CenterX = new SvgUnit(SvgUnitType.Percentage, cx * 100f),
            CenterY = new SvgUnit(SvgUnitType.Percentage, cy * 100f),
            Radius = new SvgUnit(SvgUnitType.Percentage, r * 100f)
        };
        if (fx.HasValue) grad.FocalX = new SvgUnit(SvgUnitType.Percentage, fx.Value * 100f);
        if (fy.HasValue) grad.FocalY = new SvgUnit(SvgUnitType.Percentage, fy.Value * 100f);
        Defs.Node.Children.Add(grad);
        return new SnapRadialGradient(grad, this);
    }

    public SnapGradient Gradient(string descriptor)
    {
        var desc = descriptor.Trim();
        var isLinear = desc.StartsWith('l') || desc.StartsWith('L');
        var coordEnd = desc.IndexOf(')');
        var coordsStr = coordEnd > 0 ? desc[2..coordEnd] : "0,0,1,1";
        var stopsStr = coordEnd > 0 ? desc[(coordEnd + 1)..] : desc;

        var coords = coordsStr.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f)
            .ToList();

        SnapGradient gradient;
        if (isLinear)
        {
            var x1 = coords.Count > 0 ? coords[0] : 0f;
            var y1 = coords.Count > 1 ? coords[1] : 0f;
            var x2 = coords.Count > 2 ? coords[2] : 1f;
            var y2 = coords.Count > 3 ? coords[3] : 0f;
            gradient = GradientLinear(x1, y1, x2, y2);
        }
        else
        {
            var cx = coords.Count > 0 ? coords[0] : 0.5f;
            var cy = coords.Count > 1 ? coords[1] : 0.5f;
            var r = coords.Count > 2 ? coords[2] : 0.5f;
            gradient = GradientRadial(cx, cy, r);
        }

        if (!string.IsNullOrWhiteSpace(stopsStr))
        {
            gradient.SetStops(stopsStr);
        }

        return gradient;
    }

    public SnapMask Mask(params SnapElement[] elements)
    {
        var mask = new SvgMask { ID = "mask_" + Guid.NewGuid().ToString("N")[..8] };
        Defs.Node.Children.Add(mask);
        var snapMask = new SnapMask(mask, this);
        if (elements != null)
        {
            foreach (var el in elements)
            {
                if (el != null) snapMask.Append(el);
            }
        }
        return snapMask;
    }

    public SnapPattern Ptrn(float x, float y, float width, float height, float? vx = null, float? vy = null, float? vw = null, float? vh = null)
    {
        var ptrn = new SvgPatternServer
        {
            ID = "ptrn_" + Guid.NewGuid().ToString("N")[..8],
            X = new SvgUnit(x),
            Y = new SvgUnit(y),
            Width = new SvgUnit(width),
            Height = new SvgUnit(height),
            PatternUnits = SvgCoordinateUnits.UserSpaceOnUse
        };
        if (vx.HasValue && vy.HasValue && vw.HasValue && vh.HasValue)
        {
            ptrn.ViewBox = new SvgViewBox(vx.Value, vy.Value, vw.Value, vh.Value);
        }
        Defs.Node.Children.Add(ptrn);
        return new SnapPattern(ptrn, this);
    }

    /// <summary>
    /// Clears the drawing but keeps <c>&lt;defs&gt;</c>.
    /// </summary>
    /// <remarks>
    /// An <c>override</c>, not a hiding method, and the distinction is the point. The base wipes
    /// every child; a paper keeps its <c>&lt;defs&gt;</c>, because the gradients, masks and patterns
    /// in there are referenced by id from whatever gets drawn next, and clearing the canvas is not a
    /// request to destroy the palette. While this merely hid the base version, the same paper did one
    /// thing or the other depending on the static type of the reference it was called through — the
    /// base being reached through a <see cref="SnapElement"/> would have taken the paint servers with
    /// it, silently, leaving every later `url(#…)` dangling.
    /// </remarks>
    public override void Clear()
    {
        var defsNode = _defs?.Node;
        var toRemove = Document.Children.Where(c => c != defsNode).ToList();
        foreach (var child in toRemove)
        {
            Document.Children.Remove(child);
        }
    }

    public override string ToString()
    {
        using var sw = new StringWriter();
        using var xw = XmlWriter.Create(sw, new XmlWriterSettings { OmitXmlDeclaration = true, Indent = true });
        Document.Write(xw);
        xw.Flush();
        return sw.ToString();
    }

    public string ToDataUri(string format = "svg", int? width = null, int? height = null, int quality = 85)
    {
        if (string.Equals(format, "svg", StringComparison.OrdinalIgnoreCase))
        {
            var xml = ToString();
            var bytes = Encoding.UTF8.GetBytes(xml);
            return "data:image/svg+xml;base64," + Convert.ToBase64String(bytes);
        }
        var imgBytes = ToImageBytes(width, height, format, quality);
        var mime = (format.ToLowerInvariant()) switch
        {
            "png" => "image/png",
            "jpeg" or "jpg" => "image/jpeg",
            _ => "image/webp"
        };
        return $"data:{mime};base64,{Convert.ToBase64String(imgBytes)}";
    }


    public string ToDataURL(string format = "svg", int? width = null, int? height = null, int quality = 85) =>
        ToDataUri(format, width, height, quality);

    public byte[] ToImageBytes(int? width = null, int? height = null, string format = "webp", int quality = 85) =>
        SvgRenderPipeline.RenderToImage(Document, width, height, format, quality);


    public void SaveImage(string filePath, int? width = null, int? height = null, string format = "webp", int quality = 85) =>
        SvgRenderPipeline.SaveImage(Document, filePath, width, height, format, quality);

    private static SvgDocument CreateDocument(float width, float height) =>
        new()
        {
            Width = new SvgUnit(width),
            Height = new SvgUnit(height),
            ViewBox = new SvgViewBox(0, 0, width, height)
        };

    private static SvgDocument CreateDocument(string width, string height)
    {
        var wUnit = SnapAttributes.ParseUnit(width);
        var hUnit = SnapAttributes.ParseUnit(height);
        return new SvgDocument
        {
            Width = wUnit,
            Height = hUnit
        };
    }

    private SnapElement EnsureDefs()
    {
        if (_defs != null) return _defs;

        var existing = Document.Children.OfType<SvgDefinitionList>().FirstOrDefault();
        if (existing != null)
        {
            _defs = new SnapElement(existing, this);
            return _defs;
        }

        var defs = new SvgDefinitionList();
        Document.Children.Insert(0, defs);
        _defs = new SnapElement(defs, this);
        return _defs;
    }

    /// <summary>
    /// Maps an SVG tag name onto its element type.
    /// <para>
    /// Unknown names throw rather than falling back to a group: <c>el('linearGradient')</c> quietly
    /// returning a <c>&lt;g&gt;</c> produces a document that serialises and renders without complaint
    /// but has silently lost the element the caller asked for.
    /// </para>
    /// </summary>
    internal static SvgElement CreateElementByName(string name) =>
        name.ToLowerInvariant() switch
        {
            "rect" => new SvgRectangle(),
            "circle" => new SvgCircle(),
            "ellipse" => new SvgEllipse(),
            "path" => new SvgPath(),
            "g" or "group" => new SvgGroup(),
            "image" => new SvgImage(),
            "text" => new SvgText(),
            "tspan" => new SvgTextSpan(),
            "textpath" or "text-path" => new SvgTextPath(),
            "line" => new SvgLine(),
            "polyline" => new SvgPolyline(),
            "polygon" => new SvgPolygon(),
            "mask" => new SvgMask(),
            "clippath" or "clip-path" => new SvgClipPath(),
            "pattern" => new SvgPatternServer(),
            "use" => new SvgUse(),
            "defs" => new SvgDefinitionList(),
            "lineargradient" or "linear-gradient" => new SvgLinearGradientServer(),
            "radialgradient" or "radial-gradient" => new SvgRadialGradientServer(),
            "stop" => new SvgGradientStop(),
            "symbol" => new SvgSymbol(),
            "marker" => new SvgMarker(),
            
            "svg" => new SvgFragment(),

            // The filter chain. Absent until now, which made the standing conclusion — that grain,
            // soft edges and colour grading were unavailable in a vector deliverable — true of the
            // API rather than of the renderer. Svg.Skia draws all of these; only the factory was
            // missing. `feTurbulence` in particular *is* Perlin noise.
            "filter" => new global::Svg.FilterEffects.SvgFilter(),
            "feturbulence" => new global::Svg.FilterEffects.SvgTurbulence(),
            "fegaussianblur" => new global::Svg.FilterEffects.SvgGaussianBlur(),
            "fecolormatrix" or "fecolourmatrix" => new global::Svg.FilterEffects.SvgColourMatrix(),
            "fedisplacementmap" => new global::Svg.FilterEffects.SvgDisplacementMap(),
            "feoffset" => new global::Svg.FilterEffects.SvgOffset(),
            "feflood" => new global::Svg.FilterEffects.SvgFlood(),
            "fecomposite" => new global::Svg.FilterEffects.SvgComposite(),
            "feblend" => new global::Svg.FilterEffects.SvgBlend(),
            "femerge" => new global::Svg.FilterEffects.SvgMerge(),
            "femergenode" => new global::Svg.FilterEffects.SvgMergeNode(),
            "fedropshadow" => new global::Svg.FilterEffects.SvgDropShadow(),
            "femorphology" => new global::Svg.FilterEffects.SvgMorphology(),
            "fetile" => new global::Svg.FilterEffects.SvgTile(),
            _ => throw new ArgumentException(
                $"'{name}' is not an SVG element this adapter can create. Supported: rect, circle, ellipse, path, " +
                "g, image, text, tspan, textPath, line, polyline, polygon, mask, clipPath, pattern, use, defs, " +
                "linearGradient, radialGradient, stop, symbol, marker, svg, filter, feTurbulence, "
                + "feGaussianBlur, feColorMatrix, feDisplacementMap, feOffset, feFlood, feComposite, "
                + "feBlend, feMerge, feMergeNode, feDropShadow, feMorphology, feTile. "
                + "For filters prefer paper.filter(...); for gradients prefer " +
                "paper.gradient(...), paper.gradientLinear(...) or paper.gradientRadial(...).",
                nameof(name))
        };
    #endregion

    #region Fields
    private SnapElement? _defs;
    #endregion
}

