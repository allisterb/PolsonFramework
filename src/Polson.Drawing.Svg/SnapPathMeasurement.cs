namespace Polson.Drawing.Svg;

using System;
using SkiaSharp;

public static class SnapPathMeasurement
{
    #region Methods
    public static SKPath ParseSvgPath(string? pathData)
    {
        if (string.IsNullOrWhiteSpace(pathData))
            return new SKPath();

        try
        {
            return SKPath.ParseSvgPathData(pathData) ?? new SKPath();
        }
        catch
        {
            return new SKPath();
        }
    }

    public static float GetTotalLength(string? pathData)
    {
        using var path = ParseSvgPath(pathData);
        return GetTotalLength(path);
    }

    public static float GetTotalLength(SKPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.IsEmpty) return 0f;

        float total = 0f;
        using var measure = new SKPathMeasure(path, false);
        do
        {
            total += measure.Length;
        } while (measure.NextContour());

        return total;
    }

    public static SnapPoint GetPointAtLength(string? pathData, float length)
    {
        using var path = ParseSvgPath(pathData);
        return GetPointAtLength(path, length);
    }

    public static SnapPoint GetPointAtLength(SKPath path, float length)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.IsEmpty) return new SnapPoint(0, 0);

        if (length < 0f) length = 0f;

        using var measure = new SKPathMeasure(path, false);
        float accumulated = 0f;
        SnapPoint lastPoint = default;

        do
        {
            var contourLen = measure.Length;
            if (length <= accumulated + contourLen)
            {
                var localLen = length - accumulated;
                if (measure.GetPositionAndTangent(localLen, out var pos, out var tan))
                {
                    var alpha = MathF.Atan2(tan.Y, tan.X) * (180f / MathF.PI);
                    return new SnapPoint(pos.X, pos.Y, alpha);
                }
            }

            if (contourLen > 0f && measure.GetPositionAndTangent(contourLen, out var p, out var t))
            {
                var a = MathF.Atan2(t.Y, t.X) * (180f / MathF.PI);
                lastPoint = new SnapPoint(p.X, p.Y, a);
            }

            accumulated += contourLen;
        } while (measure.NextContour());

        return lastPoint;
    }

    public static SnapBBox GetBBox(string? pathData, SnapMatrix? matrix = null)
    {
        using var path = ParseSvgPath(pathData);
        return GetBBox(path, matrix);
    }

    public static SnapBBox GetBBox(SKPath path, SnapMatrix? matrix = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.IsEmpty) return new SnapBBox();

        SKPath pathToMeasure = path;
        SKPath? transformedPath = null;
        try
        {
            if (matrix != null && !matrix.IsIdentity)
            {
                transformedPath = new SKPath(path);
                transformedPath.Transform(matrix.ToSkMatrix());
                pathToMeasure = transformedPath;
            }

            pathToMeasure.GetBounds(out var bounds);
            return new SnapBBox(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
        }
        finally
        {
            transformedPath?.Dispose();
        }
    }
    #endregion
}

