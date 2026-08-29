namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using SkiaSharp;

#pragma warning disable CS0618

/// <summary>Retained Canvas 2D path, mirroring the DOM <c>Path2D</c> interface.</summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// </remarks>
public class CanvasPath : IDisposable
{
    #region Constructors
    public CanvasPath()
    {
        Path = new SKPath();
    }

    public CanvasPath(CanvasPath other)
    {
        Path = new SKPath(other.Path);
    }

    public CanvasPath(string svgPathData)
    {
        Path = SKPath.ParseSvgPathData(svgPathData) ?? new SKPath();
    }

    /// <summary>Wraps a path a boolean operation produced. Private: not part of the scripted surface.</summary>
    private CanvasPath(SKPath path)
    {
        Path = path;
    }
    #endregion

    #region Properties
    public SKPath Path { get; }
    #endregion

    #region Methods
    public void BeginPath()
    {
        Path.Reset();
    }

    public void MoveTo(float x, float y)
    {
        Path.MoveTo(x, y);
    }

    public void LineTo(float x, float y)
    {
        if (Path.PointCount == 0)
        {
            Path.MoveTo(x, y);
        }
        else
        {
            Path.LineTo(x, y);
        }
    }

    public void ClosePath()
    {
        Path.Close();
    }

    public void Rect(float x, float y, float width, float height)
    {
        Path.AddRect(SKRect.Create(x, y, width, height));
    }

    public void RoundRect(float x, float y, float width, float height, object? radii = null)
    {
        var r = ParseRadii(radii);
        var rect = SKRect.Create(x, y, width, height);
        var rrect = new SKRoundRect();
        rrect.SetRectRadii(rect,
        [
            new SKPoint(r.tl, r.tl),
            new SKPoint(r.tr, r.tr),
            new SKPoint(r.br, r.br),
            new SKPoint(r.bl, r.bl)
        ]);
        Path.AddRoundRect(rrect);
    }

    public void Arc(float x, float y, float radius, float startAngle, float endAngle, bool anticlockwise = false)
    {
        if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be non-negative");

        var sweepAngle = endAngle - startAngle;
        if (anticlockwise)
        {
            if (sweepAngle > 0) sweepAngle = sweepAngle - MathF.PI * 2f;
        }
        else
        {
            if (sweepAngle < 0) sweepAngle = sweepAngle + MathF.PI * 2f;
        }

        // Convert radians to degrees for Skia
        var startDeg = startAngle * 180f / MathF.PI;
        var sweepDeg = sweepAngle * 180f / MathF.PI;

        var oval = SKRect.Create(x - radius, y - radius, radius * 2f, radius * 2f);

        // A full sweep has to become its own closed contour. Skia's ArcTo collapses a 360-degree
        // sweep to nothing, so a circle appended to a non-empty path vanishes silently — taking with
        // it any counter that a fill rule was meant to cut out of the shape.
        if (Path.PointCount == 0 || MathF.Abs(sweepDeg) >= 359.99f)
        {
            Path.AddArc(oval, startDeg, sweepDeg);
        }
        else
        {
            Path.ArcTo(oval, startDeg, sweepDeg, false);
        }
    }

    public void ArcTo(float x1, float y1, float x2, float y2, float radius)
    {
        Path.ArcTo(x1, y1, x2, y2, radius);
    }

    public void Ellipse(float x, float y, float radiusX, float radiusY, float rotation, float startAngle, float endAngle, bool anticlockwise = false)
    {
        if (radiusX < 0 || radiusY < 0) throw new ArgumentOutOfRangeException("Radii must be non-negative");

        var sweepAngle = endAngle - startAngle;
        if (anticlockwise)
        {
            if (sweepAngle > 0) sweepAngle = sweepAngle - MathF.PI * 2f;
        }
        else
        {
            if (sweepAngle < 0) sweepAngle = sweepAngle + MathF.PI * 2f;
        }

        var oval = SKRect.Create(-radiusX, -radiusY, radiusX * 2f, radiusY * 2f);
        using var tempPath = new SKPath();
        tempPath.AddArc(oval, startAngle * 180f / MathF.PI, sweepAngle * 180f / MathF.PI);

        var matrix = SKMatrix.CreateTranslation(x, y);
        var rotMatrix = SKMatrix.CreateRotation(rotation);
        matrix = matrix.PreConcat(rotMatrix);

        tempPath.Transform(matrix);

        // Per the Canvas spec, ellipse() joins the current point to the start of the arc, exactly as
        // arc() does. Appending as a fresh subpath instead leaves the outline open, so any fill that
        // mixes ellipse() with lineTo() closes each fragment separately and folds over itself.
        if (Path.PointCount == 0) Path.AddPath(tempPath);
        else Path.AddPath(tempPath, SKPathAddMode.Extend);
    }

    public void BezierCurveTo(float cp1x, float cp1y, float cp2x, float cp2y, float x, float y)
    {
        Path.CubicTo(cp1x, cp1y, cp2x, cp2y, x, y);
    }

    public void SCurveTo(float cp1x, float cp1y, float cp2x, float cp2y, float x, float y) =>
        BezierCurveTo(cp1x, cp1y, cp2x, cp2y, x, y);

    public void QuadraticCurveTo(float cpx, float cpy, float x, float y)
    {
        Path.QuadTo(cpx, cpy, x, y);
    }

    public void CCurveTo(float cpx, float cpy, float x, float y) =>
        QuadraticCurveTo(cpx, cpy, x, y);

    public void AddPath(CanvasPath other)
    {
        ArgumentNullException.ThrowIfNull(other);
        Path.AddPath(other.Path);
    }

    /// <summary>Everything covered by either path.</summary>
    public CanvasPath Union(CanvasPath other) => Combine(other, SKPathOp.Union, nameof(Union));

    /// <summary>What is left of this path once <paramref name="other"/> is cut out of it.</summary>
    /// <remarks>
    /// The counter-cutting operation: a real hole in real geometry, rather than an even-odd subpath
    /// that depends on winding, or a background-coloured shape laid on top that only works on a
    /// plain ground. The result survives a monochrome knockout and exports as one vector path.
    /// </remarks>
    public CanvasPath Subtract(CanvasPath other) => Combine(other, SKPathOp.Difference, nameof(Subtract));

    /// <summary>Only what both paths cover.</summary>
    public CanvasPath Intersect(CanvasPath other) => Combine(other, SKPathOp.Intersect, nameof(Intersect));

    /// <summary>What either path covers, but not both — the overlap is knocked out.</summary>
    public CanvasPath Xor(CanvasPath other) => Combine(other, SKPathOp.Xor, nameof(Xor));

    /// <summary>
    /// Resolves a path's self-intersections into simple non-overlapping contours.
    /// </summary>
    /// <remarks>
    /// Worth doing before a boolean op on a hand-built contour: a stroke that crosses itself has
    /// regions covered twice, and what that means depends on the fill rule rather than on the shape
    /// anyone intended.
    /// </remarks>
    public CanvasPath Simplify()
    {
        var result = new SKPath();

        if (!Path.Simplify(result))
        {
            result.Dispose();
            throw new InvalidOperationException("Could not simplify this path. It may have degenerate or unclosed contours.");
        }

        return new CanvasPath(result);
    }

    /// <summary>
    /// Runs one boolean operation, leaving both operands untouched.
    /// </summary>
    /// <remarks>
    /// Returns a new path rather than mutating the receiver, deliberately. The last silent bug in this
    /// class was a stored fill rule outliving the call that set it; an operation that quietly rewrote
    /// the path it was called on would be the same mistake in a louder place.
    /// </remarks>
    private CanvasPath Combine(CanvasPath other, SKPathOp op, string operation)
    {
        ArgumentNullException.ThrowIfNull(other);

        var combined = new SKPath();

        if (!Path.Op(other.Path, op, combined))
        {
            combined.Dispose();
            throw new InvalidOperationException(
                $"{operation} failed on these paths. Try simplify() on each operand first — self-intersecting contours are the usual cause.");
        }

        // Skia expresses some results — xor especially — as contours that only read correctly under
        // even-odd. Returning that would hand back a shape whose meaning depends on a fill rule the
        // caller has to guess, which is the trap this class just had removed from it. Simplifying
        // normalises the result so it draws the same under either rule.
        var normalised = new SKPath();

        if (!combined.Simplify(normalised))
        {
            normalised.Dispose();
            return new CanvasPath(combined);   // unnormalised beats nothing; the shape is still right
        }

        combined.Dispose();
        return new CanvasPath(normalised);
    }

    public void Dispose()
    {
        Path.Dispose();
        GC.SuppressFinalize(this);
    }

    private static (float tl, float tr, float br, float bl) ParseRadii(object? radii)
    {
        if (radii is null) return (0, 0, 0, 0);
        if (radii is float f) return (f, f, f, f);
        if (radii is double d) return ((float)d, (float)d, (float)d, (float)d);
        if (radii is int i) return (i, i, i, i);

        if (radii is IEnumerable enumerable)
        {
            var list = new List<float>();
            foreach (var item in enumerable)
            {
                list.Add(Convert.ToSingle(item));
            }
            if (list.Count == 1) return (list[0], list[0], list[0], list[0]);
            if (list.Count == 2) return (list[0], list[1], list[0], list[1]);
            if (list.Count == 3) return (list[0], list[1], list[2], list[1]);
            if (list.Count >= 4) return (list[0], list[1], list[2], list[3]);
        }

        return (0, 0, 0, 0);
    }
    #endregion
}

