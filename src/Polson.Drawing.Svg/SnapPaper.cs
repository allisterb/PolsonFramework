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
    public VectorLogoToolkit Logo => VectorLogo;
    #endregion

    #region Methods
    public SnapRect Rect(float x, float y, float width, float height, float rx = 0f, float ry = 0f)
    {
        var rect = new SvgRectangle
        {
            X = new SvgUnit(x),
            Y = new SvgUnit(y),
            Width = new SvgUnit(width),
            Height = new SvgUnit(height),
            CornerRadiusX = new SvgUnit(rx),
            CornerRadiusY = new SvgUnit(ry > 0f ? ry : rx)
        };
        Document.Children.Add(rect);
        return new SnapRect(rect, this);
    }

    public SnapCircle Circle(float cx, float cy, float r)
    {
        var circle = new SvgCircle
        {
            CenterX = new SvgUnit(cx),
            CenterY = new SvgUnit(cy),
            Radius = new SvgUnit(r)
        };
        Document.Children.Add(circle);
        return new SnapCircle(circle, this);
    }

    public SnapEllipse Ellipse(float cx, float cy, float rx, float ry)
    {
        var ellipse = new SvgEllipse
        {
            CenterX = new SvgUnit(cx),
            CenterY = new SvgUnit(cy),
            RadiusX = new SvgUnit(rx),
            RadiusY = new SvgUnit(ry)
        };
        Document.Children.Add(ellipse);
        return new SnapEllipse(ellipse, this);
    }

    public SnapPath Path(string d = "")
    {
        var path = new SvgPath
        {
            PathData = SvgPathBuilder.Parse(d)
        };
        Document.Children.Add(path);
        return new SnapPath(path, this);
    }

    public SnapGroup G(params SnapElement[] elements) => Group(elements);

    public SnapGroup Group(params SnapElement[] elements)
    {
        var group = new SvgGroup();
        Document.Children.Add(group);
        var snapGroup = new SnapGroup(group, this);
        if (elements != null)
        {
            foreach (var el in elements)
            {
                if (el != null) snapGroup.Append(el);
            }
        }
        return snapGroup;
    }

    public SnapImage Image(string src, float x = 0f, float y = 0f, float width = 0f, float height = 0f)
    {
        var image = new SvgImage
        {
            Href = src,
            X = new SvgUnit(x),
            Y = new SvgUnit(y),
            Width = new SvgUnit(width),
            Height = new SvgUnit(height)
        };
        Document.Children.Add(image);
        return new SnapImage(image, this);
    }

    public SnapText Text(float x, float y, object? text)
    {
        var textStr = text?.ToString() ?? string.Empty;
        var svgText = new SvgText
        {
            X = new SvgUnitCollection { new SvgUnit(x) },
            Y = new SvgUnitCollection { new SvgUnit(y) },
            Text = textStr
        };
        Document.Children.Add(svgText);
        return new SnapText(svgText, this);
    }

    public SnapLine Line(float x1, float y1, float x2, float y2)
    {
        var line = new SvgLine
        {
            StartX = new SvgUnit(x1),
            StartY = new SvgUnit(y1),
            EndX = new SvgUnit(x2),
            EndY = new SvgUnit(y2)
        };
        Document.Children.Add(line);
        return new SnapLine(line, this);
    }

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

    #region Vector Logo Methods
    public SnapPath Squircle(float x, float y, float width, float height, float exponent = 4.5f) =>
        VectorLogo.Squircle(this, x, y, width, height, exponent);

    public SnapPath GoldenSpiral(float startX, float startY, float initialRadius, float turns = 3f, int segmentsPerTurn = 36) =>
        VectorLogo.GoldenSpiral(this, startX, startY, initialRadius, turns, segmentsPerTurn);

    public SnapPath EmblemBadge(float cx, float cy, float width, float height, string style = "shield") =>
        VectorLogo.EmblemBadge(this, cx, cy, width, height, style);

    public SnapGroup GoldenCircles(float cx, float cy, float baseRadius, int count = 5) =>
        VectorLogo.GoldenCircles(this, cx, cy, baseRadius, count);

    public SnapGroup IsometricGrid(float width, float height, float spacing = 40f) =>
        VectorLogo.IsometricGrid(this, width, height, spacing);

    public SnapGroup PolarGrid(float cx, float cy, float maxRadius, int ringCount = 5, int rayCount = 12) =>
        VectorLogo.PolarGrid(this, cx, cy, maxRadius, ringCount, rayCount);

    public SnapGroup MonogramMatrix(float x, float y, float width, float height, string type = "3x3") =>
        VectorLogo.MonogramMatrix(this, x, y, width, height, type);

    public SnapGroup ClearSpaceGuide(float x, float y, float width, float height, float margin = 24f) =>
        VectorLogo.ClearSpaceGuide(this, x, y, width, height, margin);

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

    public SnapUse Use(object target)
    {
        var id = target is SnapElement el ? el.Id : target?.ToString() ?? string.Empty;
        var use = new SvgUse
        {
            ReferencedElement = new Uri(id.StartsWith('#') ? id : "#" + id, UriKind.RelativeOrAbsolute)
        };
        Document.Children.Add(use);
        return new SnapUse(use, this);
    }

    public SnapElement El(string name, IDictionary<string, object?>? attrs = null)
    {
        var element = CreateElementByName(name);
        Document.Children.Add(element);
        var snapEl = Wrap(element, this);
        if (attrs != null)
        {
            snapEl.Attr(attrs);
        }
        return snapEl;
    }

    public void Clear()
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
            _ => throw new ArgumentException(
                $"'{name}' is not an SVG element this adapter can create. Supported: rect, circle, ellipse, path, " +
                "g, image, text, tspan, textPath, line, polyline, polygon, mask, clipPath, pattern, use, defs, " +
                "linearGradient, radialGradient, stop, symbol, marker, svg. For gradients prefer " +
                "paper.gradient(...), paper.gradientLinear(...) or paper.gradientRadial(...).",
                nameof(name))
        };
    #endregion

    #region Fields
    private SnapElement? _defs;
    #endregion
}

