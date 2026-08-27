namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using SkiaSharp;

#pragma warning disable CS0618

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
    #endregion

    #region Properties
    public SKPath Path { get; }
    #endregion

    #region Methods
    public void beginPath()
    {
        Path.Reset();
    }

    public void moveTo(float x, float y)
    {
        Path.MoveTo(x, y);
    }

    public void lineTo(float x, float y)
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

    public void closePath()
    {
        Path.Close();
    }

    public void rect(float x, float y, float width, float height)
    {
        Path.AddRect(SKRect.Create(x, y, width, height));
    }

    public void roundRect(float x, float y, float width, float height, object? radii = null)
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

    public void arc(float x, float y, float radius, float startAngle, float endAngle, bool anticlockwise = false)
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

    public void arcTo(float x1, float y1, float x2, float y2, float radius)
    {
        Path.ArcTo(x1, y1, x2, y2, radius);
    }

    public void ellipse(float x, float y, float radiusX, float radiusY, float rotation, float startAngle, float endAngle, bool anticlockwise = false)
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

    public void bezierCurveTo(float cp1x, float cp1y, float cp2x, float cp2y, float x, float y)
    {
        Path.CubicTo(cp1x, cp1y, cp2x, cp2y, x, y);
    }

    public void sCurveTo(float cp1x, float cp1y, float cp2x, float cp2y, float x, float y) =>
        bezierCurveTo(cp1x, cp1y, cp2x, cp2y, x, y);

    public void quadraticCurveTo(float cpx, float cpy, float x, float y)
    {
        Path.QuadTo(cpx, cpy, x, y);
    }

    public void cCurveTo(float cpx, float cpy, float x, float y) =>
        quadraticCurveTo(cpx, cpy, x, y);

    public void addPath(CanvasPath other)
    {
        ArgumentNullException.ThrowIfNull(other);
        Path.AddPath(other.Path);
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

