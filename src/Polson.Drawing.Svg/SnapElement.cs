namespace Polson.Drawing.Svg;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using SkiaSharp;

/// <summary>Base SVG element node. Subclasses add their per-shape members.</summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// </remarks>
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

    public string Id
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

    /// <summary>Appends <paramref name="el"/> as a child of this element.</summary>
    /// <remarks>
    /// <para>
    /// <b>A text node needs more than <c>Children.Add</c>, and without it a <c>&lt;tspan&gt;</c>
    /// vanishes.</b> <see cref="SvgTextBase"/> serialises its <c>Nodes</c> and ignores <c>Children</c>
    /// entirely whenever <c>Nodes</c> is non-empty — and setting <c>Text</c> puts a content node
    /// there, <b>including when the text is empty</b>. So <c>paper.text(x, y, '')</c>, which is the
    /// natural way to make a container for spans, left an empty content node that suppressed every
    /// child: the span was in the tree, <c>children.length</c> reported it, and the document
    /// serialised as <c>&lt;text /&gt;</c> with no markup and no ink, reporting success throughout.
    /// </para>
    /// <para>
    /// Two cases, and both are handled here rather than at the call sites. An <b>empty</b> content
    /// node carries nothing and is simply dropped, which lets the writer fall through to
    /// <c>Children</c>. <b>Real</b> content is kept and the child is mirrored into <c>Nodes</c>, so
    /// mixed content — a run of text followed by a span — writes in document order.
    /// </para>
    /// </remarks>
    public virtual SnapElement Append(SnapElement el)
    {
        ArgumentNullException.ThrowIfNull(el);
        if (el.Node.Parent != null)
        {
            el.Node.Parent.Children.Remove(el.Node);
            RemoveFromNodes(el.Node.Parent, el.Node);
        }

        if (Node is SvgTextBase)
        {
            for (var i = Node.Nodes.Count - 1; i >= 0; i--)
            {
                if (string.IsNullOrWhiteSpace(Node.Nodes[i].Content)) Node.Nodes.RemoveAt(i);
            }
        }

        Node.Children.Add(el.Node);

        // Only once it is a child: mirroring an element the writer would not reach anyway would put
        // it in the output twice.
        if (Node is SvgTextBase && Node.Nodes.Count > 0 && el.Node is ISvgNode node)
        {
            Node.Nodes.Add(node);
        }

        el.Paper = Paper;
        return this;
    }

    /// <summary>Drops <paramref name="child"/> from <paramref name="parent"/>'s mixed content, if it is there.</summary>
    /// <remarks>
    /// The counterpart of the mirroring in <see cref="Append"/>. Without it a span moved from one
    /// text element to another is written by both, because <c>Children.Remove</c> does not touch
    /// <c>Nodes</c>.
    /// </remarks>
    private static void RemoveFromNodes(SvgElement parent, SvgElement child)
    {
        if (parent is not SvgTextBase) return;

        for (var i = parent.Nodes.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(parent.Nodes[i], child)) parent.Nodes.RemoveAt(i);
        }
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

    #region Child Factory Methods
    public virtual SnapRect Rect(float x, float y, float width, float height, float rx = 0f, float ry = 0f)
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
        Node.Children.Add(rect);
        return new SnapRect(rect, Paper);
    }


    public virtual SnapCircle Circle(float cx, float cy, float r)
    {
        var circle = new SvgCircle
        {
            CenterX = new SvgUnit(cx),
            CenterY = new SvgUnit(cy),
            Radius = new SvgUnit(r)
        };
        Node.Children.Add(circle);
        return new SnapCircle(circle, Paper);
    }

    public virtual SnapEllipse Ellipse(float cx, float cy, float rx, float ry)
    {
        var ellipse = new SvgEllipse
        {
            CenterX = new SvgUnit(cx),
            CenterY = new SvgUnit(cy),
            RadiusX = new SvgUnit(rx),
            RadiusY = new SvgUnit(ry)
        };
        Node.Children.Add(ellipse);
        return new SnapEllipse(ellipse, Paper);
    }

    public virtual SnapPath Path(string d = "")
    {
        var path = new SvgPath
        {
            PathData = SvgPathBuilder.Parse(d)
        };
        Node.Children.Add(path);
        return new SnapPath(path, Paper);
    }


    public virtual SnapGroup G(params SnapElement[] elements) => Group(elements);

    public virtual SnapGroup Group(params SnapElement[] elements)
    {
        var group = new SvgGroup();
        Node.Children.Add(group);
        var snapGroup = new SnapGroup(group, Paper);
        if (elements != null)
        {
            foreach (var el in elements)
            {
                if (el != null) snapGroup.Append(el);
            }
        }
        return snapGroup;
    }

    /// <summary>
    /// Appends an <c>&lt;image&gt;</c>. <paramref name="src"/> is a bitmap, canvas, requisitioned
    /// material or reference photograph — which is <b>inlined</b> as a data URI — or a string href.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Pass the object, not a path.</b> An external href in an SVG resolves only when the SVG is
    /// treated as a <i>document</i>: opened directly, or embedded through <c>&lt;object&gt;</c> or
    /// <c>&lt;iframe&gt;</c>. Loaded through <c>&lt;img&gt;</c> or a CSS <c>background-image</c> it is
    /// an image, and an image does not fetch external resources — the photograph is simply absent,
    /// with the sidecar file sitting next to it and serving perfectly well. Our own renderer never
    /// fetches an external href either, and draws a broken-image cross in its place while the run
    /// reports success.
    /// </para>
    /// <para>
    /// So the natural spelling — <c>paper.image('artifacts/portrait.png', …)</c>, which is exactly
    /// the project-relative convention <c>outFile</c> and <c>Skia.Image.load</c> establish — is the
    /// one that fails, and fails quietly. Passing the object inlines it and works everywhere.
    /// Measured: the base64 tax is +33% on disk and <b>0.3% once gzipped</b>, because base64 carries
    /// six bits of entropy in an eight-bit byte and deflate takes it all back.
    /// </para>
    /// <para>
    /// A string is still accepted, because a genuinely external reference is sometimes what is
    /// wanted, and because a deliverable is not the only thing an SVG can be.
    /// </para>
    /// </remarks>
    public virtual SnapImage Image(object src, float x = 0f, float y = 0f, float width = 0f, float height = 0f)
    {
        var href = src switch
        {
            // Checked before string, because a type could be both and the pixels are what we want.
            IDataUriSource source => source.ToDataUri(),
            string text => text,
            null => throw new ArgumentNullException(nameof(src),
                "image(src, …) needs a bitmap, canvas, material, photograph or href string; got null."),
            _ => throw new ArgumentException(
                $"image(src, …) cannot use a {src.GetType().Name}. Pass a bitmap, canvas, "
                + "requisitioned material or photograph to inline it, or an href string.", nameof(src)),
        };

        var image = new SvgImage
        {
            Href = href,
            X = new SvgUnit(x),
            Y = new SvgUnit(y),
            Width = new SvgUnit(width),
            Height = new SvgUnit(height)
        };
        Node.Children.Add(image);
        return new SnapImage(image, Paper);
    }


    public virtual SnapText Text(float x, float y, object? text)
    {
        var textStr = text?.ToString() ?? string.Empty;
        var svgText = new SvgText
        {
            X = new SvgUnitCollection { new SvgUnit(x) },
            Y = new SvgUnitCollection { new SvgUnit(y) },
            Text = textStr
        };
        Node.Children.Add(svgText);
        return new SnapText(svgText, Paper);
    }

    public virtual SnapLine Line(float x1, float y1, float x2, float y2)
    {
        var line = new SvgLine
        {
            StartX = new SvgUnit(x1),
            StartY = new SvgUnit(y1),
            EndX = new SvgUnit(x2),
            EndY = new SvgUnit(y2)
        };
        Node.Children.Add(line);
        return new SnapLine(line, Paper);
    }

    public virtual SnapPolyline Polyline(params float[] points)
    {
        var unitCollection = new SvgPointCollection();
        if (points != null)
        {
            for (var i = 0; i < points.Length - 1; i += 2)
            {
                unitCollection.Add(new SvgUnit(points[i]));
                unitCollection.Add(new SvgUnit(points[i + 1]));
            }
        }
        var polyline = new SvgPolyline { Points = unitCollection };
        Node.Children.Add(polyline);
        return new SnapPolyline(polyline, Paper);
    }

    public virtual SnapPolygon Polygon(params float[] points)
    {
        var unitCollection = new SvgPointCollection();
        if (points != null)
        {
            for (var i = 0; i < points.Length - 1; i += 2)
            {
                unitCollection.Add(new SvgUnit(points[i]));
                unitCollection.Add(new SvgUnit(points[i + 1]));
            }
        }
        var polygon = new SvgPolygon { Points = unitCollection };
        Node.Children.Add(polygon);
        return new SnapPolygon(polygon, Paper);
    }

    public virtual SnapUse Use(object target)
    {
        var id = target is SnapElement el ? el.Id : target?.ToString() ?? string.Empty;
        var use = new SvgUse
        {
            ReferencedElement = new Uri(id.StartsWith('#') ? id : "#" + id, UriKind.RelativeOrAbsolute)
        };
        Node.Children.Add(use);
        return new SnapUse(use, Paper);
    }

    /// <summary>Sets this element's accessible name — its <c>&lt;title&gt;</c> — replacing any existing one.</summary>
    /// <remarks>
    /// <para>
    /// <b>A title is what a screen reader announces, and on the root it names the whole graphic.</b>
    /// An SVG without one is an unlabelled image however carefully it is drawn, and until now there
    /// was no way to emit one at all: <c>el('title')</c> threw.
    /// </para>
    /// <para>
    /// <b>Inserted first, which is not cosmetic.</b> The accessible name is taken from the first
    /// <c>&lt;title&gt;</c> child, so one appended after the artwork is the title of nothing in
    /// particular. Calling it twice replaces rather than appends, because two titles on one element
    /// is not a richer description — it is an ambiguity resolved by document order.
    /// </para>
    /// <code>
    /// paper.title('Apollo 11 descent stage fuel budget');
    /// paper.desc('Mass distribution, propulsion metrics and the 752-second powered descent.');
    /// </code>
    /// </remarks>
    public virtual SnapElement Title(string text)
    {
        SetDescriptive<SvgTitle>(text, first: true);
        return this;
    }

    /// <summary>Sets this element's long description — its <c>&lt;desc&gt;</c> — replacing any existing one.</summary>
    /// <remarks>
    /// The paragraph a title cannot carry: what the graphic shows, for a reader who cannot see it.
    /// Placed after the title and before the artwork, for the same document-order reason.
    /// </remarks>
    public virtual SnapElement Desc(string text)
    {
        SetDescriptive<SvgDescription>(text, first: false);
        return this;
    }

    /// <summary>Replaces the single descriptive child of type <typeparamref name="T"/>.</summary>
    private void SetDescriptive<T>(string text, bool first) where T : SvgElement, new()
    {
        for (var i = Node.Children.Count - 1; i >= 0; i--)
        {
            if (Node.Children[i] is T) Node.Children.RemoveAt(i);
        }

        var node = new T { Content = text ?? string.Empty };

        // A title goes first; a description goes after whatever title there is, and before the
        // artwork either way.
        var at = first ? 0 : Node.Children.Count > 0 && Node.Children[0] is SvgTitle ? 1 : 0;
        Node.Children.Insert(Math.Min(at, Node.Children.Count), node);
    }

    /// <summary>Creates and appends a child element by SVG tag name.</summary>
    /// <remarks>
    /// Placed through <see cref="Append"/> rather than straight into <c>Children</c>, because a text
    /// element needs its mixed content maintained as well — see <see cref="Append"/>. Adding here
    /// directly is what made <c>el('tspan')</c> and <c>el('textPath')</c> silently produce nothing.
    /// </remarks>
    public virtual SnapElement El(string name, IDictionary<string, object?>? attrs = null)
    {
        var element = SnapPaper.CreateElementByName(name);
        var snapEl = Wrap(element, Paper);
        Append(snapEl);
        if (attrs != null)
        {
            snapEl.Attr(attrs);
        }
        return snapEl;
    }


    public virtual void Clear()
    {
        Node.Children.Clear();
    }
    #endregion

    public virtual SnapElement Remove()
    {
        if (Node.Parent is { } parent)
        {
            parent.Children.Remove(Node);
            // Mixed content holds a second reference; without this the span keeps serialising from a
            // parent it is no longer a child of. See Append.
            RemoveFromNodes(parent, Node);
        }
        return this;
    }

    /// <remarks>
    /// A property, not a method, because that is how the SDK reference spells it and the reference is
    /// what an agent reads. Snap.svg's own <c>el.parent()</c> is a method, so a script written from
    /// memory of Snap.svg now fails loudly — which is the point. As a method, <c>el.children.length</c>
    /// answered with the delegate's arity, <c>0</c>, so a wrong spelling was indistinguishable from an
    /// empty tree and no script could tell it had asked the wrong question.
    /// </remarks>
    public virtual SnapElement? Parent =>
        Node.Parent != null ? Wrap(Node.Parent, Paper) : null;

    /// <inheritdoc cref="Parent"/>
    public virtual List<SnapElement> Children =>
        Node.Children.Select(c => Wrap(c, Paper)).ToList();

    /// <remarks>
    /// The clone is <b>detached</b>: it has no parent until <c>appendTo</c>/<c>prependTo</c> places it.
    /// Snap.svg inserts the copy after the original; here the placement is the caller's, so a clone
    /// made only to measure or to seed a <c>&lt;defs&gt;</c> entry does not silently double the drawing.
    /// </remarks>
    public virtual SnapElement Clone()
    {
        var clonedNode = Node.DeepCopy();
        return Wrap(clonedNode, Paper);
    }

    public virtual SnapBBox GetBBox(bool isWithoutTransform = false)
    {
        // Only the agent-facing spelling is counted. attr() and transform() call this internally with
        // isWithoutTransform: true on every attribute set, and a group's box is computed by recursing
        // over its children — counting either would report a script as inspecting when it was drawing.
        if (!isWithoutTransform) ProbeScope.Record(ProbeScope.Kinds.Measure);
        return Bounds(isWithoutTransform);
    }

    private SnapBBox Bounds(bool isWithoutTransform)
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
        ProbeScope.Record(ProbeScope.Kinds.Measure);
        using var skPath = ToSkPath();
        return skPath != null ? SnapPathMeasurement.GetTotalLength(skPath) : 0f;
    }

    public virtual SnapPoint GetPointAtLength(float length)
    {
        ProbeScope.Record(ProbeScope.Kinds.Measure);
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
            SvgDefinitionList => "defs",
            SvgTextSpan => "tspan",
            SvgTextPath => "textPath",
            SvgSymbol => "symbol",
            SvgMarker => "marker",
            // SvgDocument derives from SvgFragment, so this must stay below it; both are "svg".
            SvgFragment => "svg",
            // The fallback lowercases and strips the "Svg" prefix, which is wrong for every tag whose
            // SVG spelling is not one lowercase word — it produced "definitionlist" for <defs> and
            // "textspan" for <tspan>. Anything creatable by el(name) is named explicitly above; this
            // remains only so an element the adapter did not create still reports something.
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
        // SvgTextBase, not SvgText: a <tspan> or <textPath> is a sibling of <text>, not a subclass, so
        // matching the concrete type sent every span to the children branch — where, having no element
        // children of its own, it measured 0x0. A paragraph built from spans was therefore invisible to
        // getBBox(), and so to anything built on it: the label-collision check in the vector workflow
        // measures exactly these, and would have reported a clean sheet for a page of overlapping spans.
        // A container whose own Text is empty still falls through to the children union below, which is
        // what makes a multi-span paragraph measure as the box around its lines.
        else if (element is SvgTextBase text && !string.IsNullOrEmpty(text.Text))
        {
            return SnapTextMeasurement.Measure(text, matrix);
        }
        else if (element.Children.Count > 0)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            var hasChild = false;
            foreach (var child in element.Children)
            {
                var childBox = Wrap(child).Bounds(false);
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
            var (p1x, p1y) = matrix.Map(x, y);
            var (p2x, p2y) = matrix.Map(x + w, y);
            var (p3x, p3y) = matrix.Map(x + w, y + h);
            var (p4x, p4y) = matrix.Map(x, y + h);

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

