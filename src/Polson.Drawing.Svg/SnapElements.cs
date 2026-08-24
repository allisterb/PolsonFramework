namespace Polson.Drawing.Svg;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public class SnapRect : SnapElement
{
    #region Constructors
    public SnapRect(SvgRectangle rect, SnapPaper? paper = null) : base(rect, paper) { }
    #endregion

    #region Properties
    public SvgRectangle RectangleNode => (SvgRectangle)Node;
    public float X => RectangleNode.X.Value;
    public float Y => RectangleNode.Y.Value;
    public float Width => RectangleNode.Width.Value;
    public float Height => RectangleNode.Height.Value;
    public float Rx => RectangleNode.CornerRadiusX.Value;
    public float Ry => RectangleNode.CornerRadiusY.Value;
    #endregion
}

public class SnapCircle : SnapElement
{
    #region Constructors
    public SnapCircle(SvgCircle circle, SnapPaper? paper = null) : base(circle, paper) { }
    #endregion

    #region Properties
    public SvgCircle CircleNode => (SvgCircle)Node;
    public float Cx => CircleNode.CenterX.Value;
    public float Cy => CircleNode.CenterY.Value;
    public float R => CircleNode.Radius.Value;
    #endregion
}

public class SnapEllipse : SnapElement
{
    #region Constructors
    public SnapEllipse(SvgEllipse ellipse, SnapPaper? paper = null) : base(ellipse, paper) { }
    #endregion

    #region Properties
    public SvgEllipse EllipseNode => (SvgEllipse)Node;
    public float Cx => EllipseNode.CenterX.Value;
    public float Cy => EllipseNode.CenterY.Value;
    public float Rx => EllipseNode.RadiusX.Value;
    public float Ry => EllipseNode.RadiusY.Value;
    #endregion
}

public class SnapPath : SnapElement
{
    #region Constructors
    public SnapPath(SvgPath path, SnapPaper? paper = null) : base(path, paper) { }
    #endregion

    #region Properties
    public SvgPath PathNode => (SvgPath)Node;
    public string D
    {
        get => PathNode.PathData?.ToString() ?? string.Empty;
        set => PathNode.PathData = SvgPathBuilder.Parse(value);
    }
    #endregion
}

public class SnapGroup : SnapElement
{
    #region Constructors
    public SnapGroup(SvgGroup group, SnapPaper? paper = null) : base(group, paper) { }
    #endregion

    #region Properties
    public SvgGroup GroupNode => (SvgGroup)Node;
    #endregion
}

public class SnapImage : SnapElement
{
    #region Constructors
    public SnapImage(SvgImage image, SnapPaper? paper = null) : base(image, paper) { }
    #endregion

    #region Properties
    public SvgImage ImageNode => (SvgImage)Node;
    public string Href
    {
        get => ImageNode.Href ?? string.Empty;
        set => ImageNode.Href = value;
    }
    #endregion
}

public class SnapText : SnapElement
{
    #region Constructors
    public SnapText(SvgText text, SnapPaper? paper = null) : base(text, paper) { }
    #endregion

    #region Properties
    public SvgText TextNode => (SvgText)Node;
    public string Text
    {
        get => TextNode.Text ?? string.Empty;
        set => TextNode.Text = value;
    }
    #endregion
}

public class SnapLine : SnapElement
{
    #region Constructors
    public SnapLine(SvgLine line, SnapPaper? paper = null) : base(line, paper) { }
    #endregion

    #region Properties
    public SvgLine LineNode => (SvgLine)Node;
    public float X1 => LineNode.StartX.Value;
    public float Y1 => LineNode.StartY.Value;
    public float X2 => LineNode.EndX.Value;
    public float Y2 => LineNode.EndY.Value;
    #endregion
}

public class SnapPolyline : SnapElement
{
    #region Constructors
    public SnapPolyline(SvgPolyline polyline, SnapPaper? paper = null) : base(polyline, paper) { }
    #endregion

    #region Properties
    public SvgPolyline PolylineNode => (SvgPolyline)Node;
    #endregion
}

public class SnapPolygon : SnapElement
{
    #region Constructors
    public SnapPolygon(SvgPolygon polygon, SnapPaper? paper = null) : base(polygon, paper) { }
    #endregion

    #region Properties
    public SvgPolygon PolygonNode => (SvgPolygon)Node;
    #endregion
}

public abstract class SnapGradient : SnapElement
{
    #region Constructors
    protected SnapGradient(SvgGradientServer gradient, SnapPaper? paper = null) : base(gradient, paper) { }
    #endregion

    #region Properties
    public SvgGradientServer GradientNode => (SvgGradientServer)Node;
    #endregion

    #region Methods
    public List<SnapGradientStop> Stops() =>
        GradientNode.Children.OfType<SvgGradientStop>().Select(s => new SnapGradientStop(s, Paper)).ToList();

    public SnapGradient AddStop(string color, float offset)
    {
        var stop = new SvgGradientStop
        {
            Offset = new SvgUnit(SvgUnitType.Percentage, offset),
            StopColor = new SvgColourServer(SnapAttributes.ParseColor(color))
        };
        GradientNode.Children.Add(stop);
        return this;
    }

    public SnapGradient SetStops(string descriptor)
    {
        GradientNode.Children.Clear();
        var parts = descriptor.Split('-', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            var p = parts[i];
            var colOffset = p.Split(':');
            var col = colOffset[0];
            var offset = colOffset.Length > 1 && float.TryParse(colOffset[1].TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedOffset)
                ? parsedOffset
                : (parts.Length > 1 ? (float)i / (parts.Length - 1) * 100f : 0f);

            AddStop(col, offset);
        }
        return this;
    }
    #endregion
}

public class SnapLinearGradient : SnapGradient
{
    #region Constructors
    public SnapLinearGradient(SvgLinearGradientServer linear, SnapPaper? paper = null) : base(linear, paper) { }
    #endregion

    #region Properties
    public SvgLinearGradientServer LinearNode => (SvgLinearGradientServer)Node;
    public float X1 => LinearNode.X1.Value;
    public float Y1 => LinearNode.Y1.Value;
    public float X2 => LinearNode.X2.Value;
    public float Y2 => LinearNode.Y2.Value;
    #endregion
}

public class SnapRadialGradient : SnapGradient
{
    #region Constructors
    public SnapRadialGradient(SvgRadialGradientServer radial, SnapPaper? paper = null) : base(radial, paper) { }
    #endregion

    #region Properties
    public SvgRadialGradientServer RadialNode => (SvgRadialGradientServer)Node;
    public float Cx => RadialNode.CenterX.Value;
    public float Cy => RadialNode.CenterY.Value;
    public float R => RadialNode.Radius.Value;
    #endregion
}

public class SnapGradientStop : SnapElement
{
    #region Constructors
    public SnapGradientStop(SvgGradientStop stop, SnapPaper? paper = null) : base(stop, paper) { }
    #endregion

    #region Properties
    public SvgGradientStop StopNode => (SvgGradientStop)Node;
    public float Offset => StopNode.Offset.Value;
    #endregion
}

public class SnapMask : SnapElement
{
    #region Constructors
    public SnapMask(SvgMask mask, SnapPaper? paper = null) : base(mask, paper) { }
    #endregion
}

public class SnapClipPath : SnapElement
{
    #region Constructors
    public SnapClipPath(SvgClipPath clip, SnapPaper? paper = null) : base(clip, paper) { }
    #endregion
}

public class SnapPattern : SnapElement
{
    #region Constructors
    public SnapPattern(SvgPatternServer ptrn, SnapPaper? paper = null) : base(ptrn, paper) { }
    #endregion
}

public class SnapSymbol : SnapElement
{
    #region Constructors
    public SnapSymbol(SvgElement symbol, SnapPaper? paper = null) : base(symbol, paper) { }
    #endregion
}

public class SnapUse : SnapElement
{
    #region Constructors
    public SnapUse(SvgUse use, SnapPaper? paper = null) : base(use, paper) { }
    #endregion
}

