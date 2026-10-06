namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using SkiaSharp;

/// <summary>Constructive drawing, perspective, lighting and anatomy toolkit (Studio Manuals 01-09).</summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// </remarks>
public partial class ConstructiveDrawingToolkit
{
    #region Nested Helper Types
    public record struct Point2D(float X, float Y);
    #endregion

    #region Point Helpers
    private static Point2D ExtractPoint(object? pt, float defX = 0f, float defY = 0f)
    {
        if (pt == null) return new Point2D(defX, defY);

        if (pt is Point2D p2d) return p2d;

        if (JsInterop.AsDict(pt) is IDictionary dict)
        {
            var x = dict.Contains("x") ? Convert.ToSingle(dict["x"], CultureInfo.InvariantCulture)
                  : dict.Contains("X") ? Convert.ToSingle(dict["X"], CultureInfo.InvariantCulture)
                  : defX;
            var y = dict.Contains("y") ? Convert.ToSingle(dict["y"], CultureInfo.InvariantCulture)
                  : dict.Contains("Y") ? Convert.ToSingle(dict["Y"], CultureInfo.InvariantCulture)
                  : defY;
            return new Point2D(x, y);
        }

        if (pt is IList list && list.Count >= 2)
        {
            var x = Convert.ToSingle(list[0], CultureInfo.InvariantCulture);
            var y = Convert.ToSingle(list[1], CultureInfo.InvariantCulture);
            return new Point2D(x, y);
        }

        return new Point2D(defX, defY);
    }

    private static Dictionary<string, object?> ToDict(Point2D p) => new() { ["x"] = p.X, ["y"] = p.Y };
    #endregion

    #region Shape Primitives
    /// <summary>A bone as a drawable mass: a tapering band with a disc at each joint.</summary>
    /// <remarks>
    /// Discs rather than hand-swept arc caps. An arc cap has to choose which half of the circle it
    /// sweeps, and choosing wrong subtracts a bite from the join instead of adding one — silently,
    /// as a white disc at every joint. A union of a band and two circles cannot get that wrong.
    /// </remarks>
    static CanvasPath Capsule(Point2D a, Point2D b, float ra, float rb)
    {
        float dx = b.X - a.X, dy = b.Y - a.Y;
        var len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 0.001f) return Disc(a, MathF.Max(ra, rb));

        float nx = -dy / len, ny = dx / len;
        var band = new CanvasPath();
        band.MoveTo(a.X + nx * ra, a.Y + ny * ra);
        band.LineTo(b.X + nx * rb, b.Y + ny * rb);
        band.LineTo(b.X - nx * rb, b.Y - ny * rb);
        band.LineTo(a.X - nx * ra, a.Y - ny * ra);
        band.ClosePath();
        return band.Union(Disc(a, ra)).Union(Disc(b, rb));
    }

    static CanvasPath Disc(Point2D c, float r)
    {
        var p = new CanvasPath();
        p.Arc(c.X, c.Y, MathF.Max(0.01f, r), 0f, MathF.PI * 2f);
        p.ClosePath();
        return p;
    }

    static CanvasPath OrientedEllipse(Point2D c, float rx, float ry, float degrees)
    {
        var p = new CanvasPath();
        p.Ellipse(c.X, c.Y, MathF.Max(0.01f, rx), MathF.Max(0.01f, ry), degrees * MathF.PI / 180f, 0f, MathF.PI * 2f);
        return p;
    }

    static float Num(IDictionary? d, string key, float fallback) =>
        d != null && d.Contains(key) && d[key] != null
            ? Convert.ToSingle(d[key], CultureInfo.InvariantCulture)
            : fallback;
    #endregion

    #region Visual Measurement Helpers
    public Dictionary<string, object?> VerifyPlumbAlignment(object topPoint, object bottomPoint, float maxTolerance = 12f)
    {
        var p1 = ExtractPoint(topPoint);
        var p2 = ExtractPoint(bottomPoint);
        var deltaX = MathF.Abs(p1.X - p2.X);
        var aligned = deltaX <= maxTolerance;

        return new Dictionary<string, object?>
        {
            ["aligned"] = aligned,
            ["deltaX"] = deltaX,
            ["message"] = aligned ? "PASS: Vertically aligned" : $"DRIFT: Offset by {deltaX:F1}px (tolerance: {maxTolerance:F1}px)"
        };
    }

    public float ComputeRelativeDistance(float headHeight, object pointA, object pointB)
    {
        if (headHeight <= 0.001f) return 0f;
        var p1 = ExtractPoint(pointA);
        var p2 = ExtractPoint(pointB);
        var dx = p2.X - p1.X;
        var dy = p2.Y - p1.Y;
        var dist = MathF.Sqrt(dx * dx + dy * dy);
        return dist / headHeight;
    }
    #endregion
}
