namespace Polson.Drawing.Svg;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using SkiaSharp;

public class SnapElement
{
    #region Constructors
    public SnapElement(SvgElement node, SnapPaper? paper = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        Node = node;
        Paper = paper;
    }
    #endregion

    #region Properties
    public SvgElement Node { get; }

    public SnapPaper? Paper { get; internal set; }

    public virtual string Type => GetElementType(Node);

    public string ID
    {
        get => Node.ID;
        set => Node.ID = value;
    }
    #endregion

    #region Methods
    public virtual SnapElement Attr(string name, object? value)
    {
        SnapAttributes.ApplyAttribute(Node, name, value, GetBBox(true));
        return this;
    }

    public virtual SnapElement Attr(IDictionary<string, object?> attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        SnapAttributes.ApplyAttributes(Node, attributes, GetBBox(true));
        return this;
    }

    public virtual object? Attr(string name) =>
        SnapAttributes.GetAttribute(Node, name);

    public virtual object? Attr(object? nameOrAttributes)
    {
        if (nameOrAttributes is string str)
        {
            return Attr(str);
        }

        if (nameOrAttributes != null)
        {
            SnapAttributes.ApplyAttributes(Node, nameOrAttributes, GetBBox(true));
        }
        return this;
    }

    public virtual object Transform(string? tstr = null)
    {
        if (tstr == null)
        {
            var matrix = SnapTransformParser.ParseToMatrix(Node.Transforms?.ToString(), GetBBox(true));
            return matrix.ToTransformString();
        }

        Node.Transforms = SnapTransformParser.ParseToSvgTransforms(tstr, GetBBox(true));
        return this;
    }

    public virtual SnapElement Append(SnapElement el)
    {
        ArgumentNullException.ThrowIfNull(el);
        if (el.Node.Parent != null)
        {
            el.Node.Parent.Children.Remove(el.Node);
        }
        Node.Children.Add(el.Node);
        el.Paper = Paper;
        return this;
    }

    public virtual SnapElement Add(SnapElement el) => Append(el);

    public virtual SnapElement Add(params SnapElement[] elements)
    {
        if (elements != null)
        {
            foreach (var el in elements)
            {
                if (el != null) Append(el);
            }
        }
        return this;
    }

    public virtual SnapElement AppendTo(SnapElement parent)
    {
        ArgumentNullException.ThrowIfNull(parent);
        parent.Append(this);
        return this;
    }

    public virtual SnapElement Prepend(SnapElement el)
    {
        ArgumentNullException.ThrowIfNull(el);
        if (el.Node.Parent != null)
        {
            el.Node.Parent.Children.Remove(el.Node);
        }
        Node.Children.Insert(0, el.Node);
        el.Paper = Paper;
        return this;
    }

    public virtual SnapElement PrependTo(SnapElement parent)
    {
        ArgumentNullException.ThrowIfNull(parent);
        parent.Prepend(this);
        return this;
    }

    public virtual SnapElement Before(SnapElement el)
    {
        ArgumentNullException.ThrowIfNull(el);
        var parent = Node.Parent;
        if (parent != null)
        {
            var index = parent.Children.IndexOf(Node);
            if (el.Node.Parent != null)
            {
                el.Node.Parent.Children.Remove(el.Node);
            }
            parent.Children.Insert(index, el.Node);
            el.Paper = Paper;
        }
        return this;
    }

    public virtual SnapElement After(SnapElement el)
    {
        ArgumentNullException.ThrowIfNull(el);
        var parent = Node.Parent;
        if (parent != null)
        {
            var index = parent.Children.IndexOf(Node);
            if (el.Node.Parent != null)
            {
                el.Node.Parent.Children.Remove(el.Node);
            }
            parent.Children.Insert(index + 1, el.Node);
            el.Paper = Paper;
        }
        return this;
    }

    public virtual SnapElement Remove()
    {
        Node.Parent?.Children.Remove(Node);
        return this;
    }

    public virtual SnapElement? Parent() =>
        Node.Parent != null ? Wrap(Node.Parent, Paper) : null;

    public virtual List<SnapElement> Children() =>
        Node.Children.Select(c => Wrap(c, Paper)).ToList();

    public virtual SnapElement Clone()
    {
        var clonedNode = Node.DeepCopy();
        return Wrap(clonedNode, Paper);
    }

    public virtual SnapBBox GetBBox(bool isWithoutTransform = false)
    {
        SnapMatrix? matrix = null;
        if (!isWithoutTransform && Node.Transforms != null && Node.Transforms.Count > 0)
        {
            matrix = SnapTransformParser.ParseToMatrix(Node.Transforms.ToString());
        }

        using var skPath = ToSkPath();
        if (skPath != null && !skPath.IsEmpty)
        {
            return SnapPathMeasurement.GetBBox(skPath, matrix);
        }

        return ComputeElementBBox(Node, matrix);
    }

    public virtual float GetTotalLength()
    {
        using var skPath = ToSkPath();
        return skPath != null ? SnapPathMeasurement.GetTotalLength(skPath) : 0f;
    }

    public virtual SnapPoint GetPointAtLength(float length)
    {
        using var skPath = ToSkPath();
        return skPath != null ? SnapPathMeasurement.GetPointAtLength(skPath, length) : default;
    }

    public virtual SnapElement? Select(string selector)
    {
        if (string.IsNullOrWhiteSpace(selector)) return null;

        var all = SelectAll(selector);
        return all.Count > 0 ? all[0] : null;
    }

    public virtual List<SnapElement> SelectAll(string selector)
    {
        var result = new List<SnapElement>();
        if (string.IsNullOrWhiteSpace(selector)) return result;

        var sel = selector.Trim();
        FindMatchingElements(Node, sel, result);
        return result;
    }

    public virtual SKPath? ToSkPath()
    {
        return Node switch
        {
            SvgPath path => SnapPathMeasurement.ParseSvgPath(path.PathData?.ToString()),
            SvgRectangle rect => CreateRectPath(rect),
            SvgCircle circle => CreateCirclePath(circle),
            SvgEllipse ellipse => CreateEllipsePath(ellipse),
            SvgLine line => CreateLinePath(line),
            SvgPolyline polyline => CreatePointPath(polyline.Points, false),
            SvgPolygon polygon => CreatePointPath(polygon.Points, true),
            _ => null
        };
    }

    public override string ToString()
    {
        var doc = new XmlDocument();
        using var sw = new StringWriter();
        using var xw = XmlWriter.Create(sw, new XmlWriterSettings { OmitXmlDeclaration = true, Indent = true });
        Node.Write(xw);
        xw.Flush();
        return sw.ToString();
    }

    public static SnapElement Wrap(SvgElement element, SnapPaper? paper = null)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element switch
        {
            SvgDocument doc => new SnapPaper(doc),
            SvgRectangle rect => new SnapRect(rect, paper),
            SvgCircle circle => new SnapCircle(circle, paper),
            SvgEllipse ellipse => new SnapEllipse(ellipse, paper),
            SvgPath path => new SnapPath(path, paper),
            SvgGroup group => new SnapGroup(group, paper),
            SvgImage image => new SnapImage(image, paper),
            SvgText text => new SnapText(text, paper),
            SvgLine line => new SnapLine(line, paper),
            SvgPolyline polyline => new SnapPolyline(polyline, paper),
            SvgPolygon polygon => new SnapPolygon(polygon, paper),
            SvgLinearGradientServer linear => new SnapLinearGradient(linear, paper),
            SvgRadialGradientServer radial => new SnapRadialGradient(radial, paper),
            SvgMask mask => new SnapMask(mask, paper),
            SvgClipPath clip => new SnapClipPath(clip, paper),
            SvgPatternServer ptrn => new SnapPattern(ptrn, paper),
            SvgUse use => new SnapUse(use, paper),
            _ => new SnapElement(element, paper)
        };
    }

    protected static string GetElementType(SvgElement node) =>
        node switch
        {
            SvgDocument => "svg",
            SvgRectangle => "rect",
            SvgCircle => "circle",
            SvgEllipse => "ellipse",
            SvgPath => "path",
            SvgGroup => "g",
            SvgImage => "image",
            SvgText => "text",
            SvgLine => "line",
            SvgPolyline => "polyline",
            SvgPolygon => "polygon",
            SvgLinearGradientServer => "linearGradient",
            SvgRadialGradientServer => "radialGradient",
            SvgGradientStop => "stop",
            SvgMask => "mask",
            SvgClipPath => "clipPath",
            SvgPatternServer => "pattern",
            SvgUse => "use",
            _ => node.GetType().Name.ToLowerInvariant().Replace("svg", "")
        };

    private static SnapBBox ComputeElementBBox(SvgElement element, SnapMatrix? matrix)
    {
        var x = 0f;
        var y = 0f;
        var w = 0f;
        var h = 0f;

        if (element is SvgRectangle rect)
        {
            x = rect.X.Value;
            y = rect.Y.Value;
            w = rect.Width.Value;
            h = rect.Height.Value;
        }
        else if (element is SvgCircle circle)
        {
            var r = circle.Radius.Value;
            x = circle.CenterX.Value - r;
            y = circle.CenterY.Value - r;
            w = r * 2f;
            h = r * 2f;
        }
        else if (element is SvgEllipse ellipse)
        {
            var rx = ellipse.RadiusX.Value;
            var ry = ellipse.RadiusY.Value;
            x = ellipse.CenterX.Value - rx;
            y = ellipse.CenterY.Value - ry;
            w = rx * 2f;
            h = ry * 2f;
        }
        else if (element is SvgImage img)
        {
            x = img.X.Value;
            y = img.Y.Value;
            w = img.Width.Value;
            h = img.Height.Value;
        }
        else if (element.Children.Count > 0)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            var hasChild = false;
            foreach (var child in element.Children)
            {
                var childBox = Wrap(child).GetBBox(false);
                if (childBox.Width > 0 || childBox.Height > 0)
                {
                    minX = MathF.Min(minX, childBox.X);
                    minY = MathF.Min(minY, childBox.Y);
                    maxX = MathF.Max(maxX, childBox.X2);
                    maxY = MathF.Max(maxY, childBox.Y2);
                    hasChild = true;
                }
            }
            if (hasChild)
            {
                x = minX;
                y = minY;
                w = maxX - minX;
                h = maxY - minY;
            }
        }

        if (matrix != null && !matrix.IsIdentity)
        {
            var (p1x, p1y) = matrix.TransformPoint(x, y);
            var (p2x, p2y) = matrix.TransformPoint(x + w, y);
            var (p3x, p3y) = matrix.TransformPoint(x + w, y + h);
            var (p4x, p4y) = matrix.TransformPoint(x, y + h);

            var minX = MathF.Min(MathF.Min(p1x, p2x), MathF.Min(p3x, p4x));
            var maxX = MathF.Max(MathF.Max(p1x, p2x), MathF.Max(p3x, p4x));
            var minY = MathF.Min(MathF.Min(p1y, p2y), MathF.Min(p3y, p4y));
            var maxY = MathF.Max(MathF.Max(p1y, p2y), MathF.Max(p3y, p4y));

            return new SnapBBox(minX, minY, maxX - minX, maxY - minY);
        }

        return new SnapBBox(x, y, w, h);
    }

    private static SKPath CreateRectPath(SvgRectangle rect)
    {
        using var builder = new SKPathBuilder();
        var rx = rect.CornerRadiusX.Value;
        var ry = rect.CornerRadiusY.Value > 0 ? rect.CornerRadiusY.Value : rx;
        var r = SKRect.Create(rect.X.Value, rect.Y.Value, rect.Width.Value, rect.Height.Value);
        if (rx > 0 || ry > 0)
        {
            builder.AddRoundRect(r, rx, ry);
        }
        else
        {
            builder.AddRect(r);
        }
        return builder.Detach();
    }

    private static SKPath CreateCirclePath(SvgCircle circle)
    {
        using var builder = new SKPathBuilder();
        builder.AddCircle(circle.CenterX.Value, circle.CenterY.Value, circle.Radius.Value);
        return builder.Detach();
    }

    private static SKPath CreateEllipsePath(SvgEllipse ellipse)
    {
        using var builder = new SKPathBuilder();
        var r = SKRect.Create(ellipse.CenterX.Value - ellipse.RadiusX.Value, ellipse.CenterY.Value - ellipse.RadiusY.Value, ellipse.RadiusX.Value * 2f, ellipse.RadiusY.Value * 2f);
        builder.AddOval(r);
        return builder.Detach();
    }

    private static SKPath CreateLinePath(SvgLine line)
    {
        using var builder = new SKPathBuilder();
        builder.MoveTo(line.StartX.Value, line.StartY.Value);
        builder.LineTo(line.EndX.Value, line.EndY.Value);
        return builder.Detach();
    }

    private static SKPath CreatePointPath(SvgPointCollection? pts, bool close)
    {
        using var builder = new SKPathBuilder();
        if (pts != null && pts.Count >= 2)
        {
            builder.MoveTo(pts[0].Value, pts[1].Value);
            for (var i = 2; i < pts.Count; i += 2)
            {
                var x = pts[i].Value;
                var y = i + 1 < pts.Count ? pts[i + 1].Value : 0f;
                builder.LineTo(x, y);
            }
            if (close) builder.Close();
        }
        return builder.Detach();
    }

    private void FindMatchingElements(SvgElement current, string sel, List<SnapElement> results)
    {
        foreach (var child in current.Children)
        {
            if (MatchesSelector(child, sel))
            {
                results.Add(Wrap(child, Paper));
            }
            FindMatchingElements(child, sel, results);
        }
    }

    private static bool MatchesSelector(SvgElement el, string sel)
    {
        if (sel.StartsWith('#'))
        {
            return string.Equals(el.ID, sel[1..], StringComparison.OrdinalIgnoreCase);
        }
        if (sel.StartsWith('.'))
        {
            return el.CustomAttributes.TryGetValue("class", out var cls) &&
                   cls.Split(' ').Contains(sel[1..], StringComparer.OrdinalIgnoreCase);
        }
        var tagName = GetElementType(el);
        return string.Equals(tagName, sel, StringComparison.OrdinalIgnoreCase);
    }
    #endregion
}

