namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using SkiaSharp;

public partial class ConstructiveDrawingToolkit
{
    #region Linear Perspective & 3D Forms
    /// <summary>
    /// The furthest a box side may recede toward its vanishing point, as a fraction of the anchor-to-VP
    /// distance. Beyond this the projection degenerates.
    /// </summary>
    private const float MaxRecession = 0.85f;

    private static Point2D LineIntersection(Point2D p1, Point2D p2, Point2D p3, Point2D p4)
    {
        var denom = (p1.X - p2.X) * (p3.Y - p4.Y) - (p1.Y - p2.Y) * (p3.X - p4.X);
        if (MathF.Abs(denom) < 0.00001f) return new Point2D((p1.X + p3.X) * 0.5f, (p1.Y + p3.Y) * 0.5f);

        var t = ((p1.X - p3.X) * (p3.Y - p4.Y) - (p1.Y - p3.Y) * (p3.X - p4.X)) / denom;
        return new Point2D(p1.X + t * (p2.X - p1.X), p1.Y + t * (p2.Y - p1.Y));
    }

    public Dictionary<string, object?> CreatePerspectiveGrid(object? options = null)
    {
        var opt = JsInterop.AsDict(options);
        var type = opt?["type"]?.ToString() ?? "2point";
        var horizonY = opt != null && opt.Contains("horizonY") ? Convert.ToSingle(opt["horizonY"], CultureInfo.InvariantCulture) : 300f;
        var cvX = opt != null && opt.Contains("centerOfVisionX") ? Convert.ToSingle(opt["centerOfVisionX"], CultureInfo.InvariantCulture) : 400f;
        var focalLength = opt != null && opt.Contains("focalLength") ? Convert.ToSingle(opt["focalLength"], CultureInfo.InvariantCulture) : 800f;
        var cameraAngleDeg = opt != null && opt.Contains("cameraAngleDeg") ? Convert.ToSingle(opt["cameraAngleDeg"], CultureInfo.InvariantCulture) : 45f;

        var rad = (cameraAngleDeg * MathF.PI) / 180f;
        var tanTheta = MathF.Tan(rad);
        var cotTheta = 1f / MathF.Max(0.001f, tanTheta);

        Point2D vpL;
        Point2D vpR;
        Point2D? vpV = null;

        if (string.Equals(type, "1point", StringComparison.OrdinalIgnoreCase))
        {
            vpL = new Point2D(cvX, horizonY);
            vpR = new Point2D(cvX, horizonY);
        }
        else
        {
            vpL = new Point2D(cvX - focalLength * cotTheta, horizonY);
            vpR = new Point2D(cvX + focalLength * tanTheta, horizonY);
        }

        if (string.Equals(type, "3point", StringComparison.OrdinalIgnoreCase))
        {
            var tiltDeg = opt != null && opt.Contains("tiltAngleDeg") ? Convert.ToSingle(opt["tiltAngleDeg"], CultureInfo.InvariantCulture) : 30f;
            var tiltRad = (tiltDeg * MathF.PI) / 180f;
            var verticalY = horizonY + (tiltDeg >= 0 ? 1 : -1) * (focalLength / MathF.Max(0.001f, MathF.Tan(tiltRad)));
            vpV = new Point2D(cvX, verticalY);
        }

        return new Dictionary<string, object?>
        {
            ["type"] = type,
            ["horizonY"] = horizonY,
            ["cv"] = ToDict(new Point2D(cvX, horizonY)),
            ["vpL"] = ToDict(vpL),
            ["vpR"] = ToDict(vpR),
            ["vpV"] = vpV.HasValue ? ToDict(vpV.Value) : null,
            ["focalLength"] = focalLength,
            ["cameraAngleDeg"] = cameraAngleDeg
        };
    }

    public void DrawPerspectiveGrid(CanvasRenderingContext2D ctx, object gridObj, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (JsInterop.AsDict(gridObj) is not IDictionary grid) return;

        var opt = JsInterop.AsDict(options);
        var lineColor = opt?["lineColor"]?.ToString() ?? "#dbe7f2";
        var horizonColor = opt?["horizonColor"]?.ToString() ?? "#4a90e2";
        var lineCount = opt != null && opt.Contains("lineCount") ? Convert.ToInt32(opt["lineCount"]) : 14;
        var lineWidth = opt != null && opt.Contains("lineWidth") ? Convert.ToSingle(opt["lineWidth"], CultureInfo.InvariantCulture) : 1f;

        var horizonY = Convert.ToSingle(grid["horizonY"], CultureInfo.InvariantCulture);
        var vpL = ExtractPoint(grid["vpL"]);
        var vpR = ExtractPoint(grid["vpR"]);

        ctx.Save();

        // 1. Vanishing Point Rays
        ctx.StrokeStyle = lineColor;
        ctx.LineWidth = lineWidth;

        var w = ctx.Canvas.Width;
        var h = ctx.Canvas.Height;

        for (var i = 0; i <= lineCount; i++)
        {
            var targetY = horizonY + (i + 1) * ((h - horizonY) / lineCount);
            // Left VP rays
            ctx.BeginPath();
            ctx.MoveTo(vpL.X, vpL.Y);
            ctx.LineTo(w + 200f, targetY);
            ctx.Stroke();

            // Right VP rays
            ctx.BeginPath();
            ctx.MoveTo(vpR.X, vpR.Y);
            ctx.LineTo(-200f, targetY);
            ctx.Stroke();
        }

        // 2. Horizon Line
        ctx.StrokeStyle = horizonColor;
        ctx.LineWidth = lineWidth * 1.5f;
        ctx.BeginPath();
        ctx.MoveTo(0, horizonY);
        ctx.LineTo(w, horizonY);
        ctx.Stroke();

        ctx.Restore();
    }

    /// <summary>
    /// Steps <paramref name="extent"/> screen pixels from <paramref name="from"/> along the ray to
    /// <paramref name="vp"/>, refusing any extent that would reach the vanishing point.
    /// </summary>
    private static Point2D Recede(Point2D from, Point2D vp, float extent, string name, string vpName)
    {
        var dx = vp.X - from.X;
        var dy = vp.Y - from.Y;
        var len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 0.0001f) len = 1f;

        var t = extent / len;
        if (t > MaxRecession)
        {
            throw new ArgumentOutOfRangeException(name, extent,
                $"Drawing.createPerspectiveBox(...) was given a {name} of {extent:F1}px, which is {t:P0} of the " +
                $"{len:F1}px from the anchor to {vpName}. Past {MaxRecession:P0} the far corner reaches the " +
                $"vanishing point and the box turns inside out. Reduce the {name} to at most " +
                $"{len * MaxRecession:F1}px, or move the anchor further from {vpName}.");
        }

        return new Point2D(from.X + dx * t, from.Y + dy * t);
    }

    public Dictionary<string, object?> CreatePerspectiveBox(object gridObj, float anchorX, float anchorY, float width, float height, float depth)
    {
        if (JsInterop.AsDict(gridObj) is not IDictionary grid)
            throw new ArgumentException("gridObj must be a valid perspective grid dictionary", nameof(gridObj));

        var vpL = ExtractPoint(grid["vpL"]);
        var vpR = ExtractPoint(grid["vpR"]);

        var v0 = new Point2D(anchorX, anchorY);
        var v4 = new Point2D(anchorX, anchorY - height);

        // Width and depth are screen distances stepped along the ray to each vanishing point. Past
        // MaxRecession the far corner arrives at the vanishing point and the box turns inside out, so the
        // request is refused rather than clamped: a silently shortened box is a wrong drawing that looks
        // like the one that was asked for.
        var v1 = Recede(v0, vpL, width, "width", "the left vanishing point");
        var v2 = Recede(v0, vpR, depth, "depth", "the right vanishing point");

        // Top left & top right points
        var v5 = LineIntersection(v4, vpL, v1, new Point2D(v1.X, v1.Y - height * 2f));
        var v6 = LineIntersection(v4, vpR, v2, new Point2D(v2.X, v2.Y - height * 2f));

        // Back bottom & back top
        var v3 = LineIntersection(v1, vpR, v2, vpL);
        var v7 = LineIntersection(v5, vpR, v6, vpL);

        List<Dictionary<string, object?>> ToFace(params Point2D[] pts)
        {
            var list = new List<Dictionary<string, object?>>();
            foreach (var p in pts) list.Add(ToDict(p));
            return list;
        }

        return new Dictionary<string, object?>
        {
            ["vertices"] = new List<Dictionary<string, object?>>
            {
                ToDict(v0), ToDict(v1), ToDict(v2), ToDict(v3),
                ToDict(v4), ToDict(v5), ToDict(v6), ToDict(v7)
            },
            ["faces"] = new Dictionary<string, object?>
            {
                ["top"] = ToFace(v4, v5, v7, v6),
                ["bottom"] = ToFace(v0, v1, v3, v2),
                ["left"] = ToFace(v0, v1, v5, v4),
                ["right"] = ToFace(v0, v2, v6, v4),
                ["backLeft"] = ToFace(v2, v3, v7, v6),
                ["backRight"] = ToFace(v1, v3, v7, v5)
            }
        };
    }

    /// <summary>
    /// Draws the box and <b>returns its faces as geometry</b>: <c>faces</c> (a
    /// <see cref="CanvasPath"/> per face, including the hidden ones whether or not they were drawn)
    /// and <c>silhouette</c>, the three visible faces unioned into the box's outline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A face is the natural unit to <i>clip a texture to</i>, which is what a box in a scene is
    /// usually for — a crate's planking, a wall's brick, a floor's tiling all want the quad the
    /// projection produced, and re-deriving one from <c>box.faces</c> means re-walking the point
    /// list on every use. The <c>silhouette</c> is what a cast shadow, a rim light or an occluding
    /// clip wants instead.
    /// </para>
    /// <para>
    /// The hidden faces are returned even when <c>drawHiddenLines</c> is false: building a path is
    /// not drawing it, and a caller staging occlusion needs the back of the box precisely when it is
    /// not being drawn.
    /// </para>
    /// <para>A script that ignores the return value behaves exactly as before.</para>
    /// </remarks>
    public Dictionary<string, object?> DrawPerspectiveBox(CanvasRenderingContext2D ctx, object boxObj, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (JsInterop.AsDict(boxObj) is not IDictionary box) return [];

        var opt = JsInterop.AsDict(options);
        var topFill = opt?["topFill"]?.ToString() ?? "#e6f0fa";
        var leftFill = opt?["leftFill"]?.ToString() ?? "#b8d5f2";
        var rightFill = opt?["rightFill"]?.ToString() ?? "#7baad4";
        var strokeColor = opt?["strokeColor"]?.ToString() ?? "#2d547d";
        var strokeWidth = opt != null && opt.Contains("strokeWidth") ? Convert.ToSingle(opt["strokeWidth"], CultureInfo.InvariantCulture) : 1.8f;
        var drawHidden = opt != null && opt.Contains("drawHiddenLines") && Convert.ToBoolean(opt["drawHiddenLines"]);

        var faces = JsInterop.AsDict(box["faces"]);
        if (faces == null) return [];

        // Every face the projection produced, drawn or not.
        var built = new Dictionary<string, CanvasPath>();
        foreach (var name in new[] { "bottom", "backLeft", "backRight", "left", "right", "top" })
        {
            if (faces[name] is not IList pts || pts.Count < 3) continue;
            var quad = new CanvasPath();
            var first = ExtractPoint(pts[0]);
            quad.MoveTo(first.X, first.Y);
            for (var i = 1; i < pts.Count; i++)
            {
                var p = ExtractPoint(pts[i]);
                quad.LineTo(p.X, p.Y);
            }
            quad.ClosePath();
            built[name] = quad;
        }

        void RenderFace(string name, object? fill)
        {
            if (!built.TryGetValue(name, out var quad)) return;
            if (fill != null)
            {
                ctx.FillStyle = fill;
                ctx.Fill(quad);
            }
            if (strokeColor != null)
            {
                ctx.StrokeStyle = strokeColor;
                ctx.LineWidth = strokeWidth;
                ctx.LineJoin = "round";
                ctx.Stroke(quad);
            }
        }

        ctx.Save();

        if (drawHidden)
        {
            ctx.Save();
            ctx.StrokeStyle = "#9bbcd9";
            ctx.LineWidth = strokeWidth * 0.7f;
            RenderFace("bottom", null);
            RenderFace("backLeft", null);
            RenderFace("backRight", null);
            ctx.Restore();
        }

        RenderFace("left", leftFill);
        RenderFace("right", rightFill);
        RenderFace("top", topFill);

        ctx.Restore();

        CanvasPath? outline = null;
        foreach (var name in new[] { "left", "right", "top" })
            if (built.TryGetValue(name, out var quad))
                outline = outline == null ? quad : outline.Union(quad);

        var faceDict = new Dictionary<string, object?>();
        foreach (var kv in built) faceDict[kv.Key] = kv.Value;

        return new Dictionary<string, object?>
        {
            ["faces"] = faceDict,
            ["silhouette"] = outline == null ? new CanvasPath() : outline.Simplify()
        };
    }

    /// <summary>
    /// Draws an upright cylinder, both caps foreshortened by the grid at their own screen height.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <paramref name="anchorX"/>/<paramref name="anchorY"/> is the centre of the <em>base circle</em>
    /// and <paramref name="radius"/> is half the cylinder's drawn width, so the silhouette lands
    /// where a caller put it and can be checked with a ruler. Both were previously otherwise: the
    /// call built a <c>2r x 2r</c> perspective box, anchored at that box's near <em>corner</em>, and
    /// took its width from the footprint's diagonal — which drew at 1.9x the requested width, off
    /// centre, without erroring.
    /// </para>
    /// <para>
    /// There is deliberately no elevation parameter. A cap's flatness is read from the directions to
    /// the vanishing points at its own centre, and a point nearer the horizon has shallower rays, so
    /// a bowl on a counter is flatter than the same bowl on the floor for free. That is also why the
    /// two caps get separate ellipses rather than one shared squash.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Draws an upright cylinder, both caps foreshortened by the grid at their own screen height.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <paramref name="anchorX"/>/<paramref name="anchorY"/> is the centre of the <em>base circle</em>
    /// and <paramref name="radius"/> is half the cylinder's drawn width, so the silhouette lands
    /// where a caller put it and can be checked with a ruler. Both were previously otherwise: the
    /// call built a <c>2r x 2r</c> perspective box, anchored at that box's near <em>corner</em>, and
    /// took its width from the footprint's diagonal — which drew at 1.9x the requested width, off
    /// centre, without erroring.
    /// </para>
    /// <para>
    /// There is deliberately no elevation parameter. A cap's flatness is read from the directions to
    /// the vanishing points at its own centre, and a point nearer the horizon has shallower rays, so
    /// a bowl on a counter is flatter than the same bowl on the floor for free. That is also why the
    /// two caps get separate ellipses rather than one shared squash.
    /// </para>
    /// <para>
    /// <b>Both caps are axis-aligned</b>, after Norling: the long axis of the ellipse forms a T with
    /// the cylinder's upright line, so on an upright cylinder it is horizontal. An earlier version
    /// built each cap from conjugate semi-diameters toward the two vanishing points, which tilted the
    /// cap away from the centre of vision — measured at 13-15 px on a 140 px cap placed 250-300 px
    /// off axis, which reads as a leaning bottle. A true wide-angle projection does tilt a circle off
    /// axis, but this grid is a screen-space construction rather than a metric camera, so the drawing
    /// convention is the one to keep.
    /// </para>
    /// </remarks>
    public void DrawPerspectiveCylinder(CanvasRenderingContext2D ctx, object gridObj, float anchorX, float anchorY, float radius, float height, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (JsInterop.AsDict(gridObj) is not IDictionary grid)
            throw new ArgumentException("gridObj must be a valid perspective grid dictionary", nameof(gridObj));

        var opt = JsInterop.AsDict(options);
        var sideFill = opt?["sideFill"]?.ToString() ?? "#b8d5f2";
        var topFill = opt?["topFill"]?.ToString() ?? "#e6f0fa";
        var strokeColor = opt?["strokeColor"]?.ToString() ?? "#2d547d";
        var strokeWidth = opt != null && opt.Contains("strokeWidth") ? Convert.ToSingle(opt["strokeWidth"], CultureInfo.InvariantCulture) : 1.8f;

        var (vpL, vpR) = CircleAxisVanishingPoints(grid);

        var baseY = anchorY;
        var topY = anchorY - height;
        var baseRy = GroundCircleSquash(vpL, vpR, new Point2D(anchorX, baseY), radius);
        var topRy = GroundCircleSquash(vpL, vpR, new Point2D(anchorX, topY), radius);

        ctx.Save();

        // 1. Body — the near half of each cap, joined by the two vertical contours. Each arc starts
        // at the tangent extreme it meets (PI on the left, 0 on the right); sweeping either from the
        // far side folds the body into a bowtie.
        ctx.FillStyle = sideFill;
        ctx.BeginPath();
        ctx.MoveTo(anchorX - radius, baseY);
        ctx.LineTo(anchorX - radius, topY);
        ctx.Ellipse(anchorX, topY, radius, topRy, 0f, MathF.PI, 0f, true);
        ctx.LineTo(anchorX + radius, baseY);
        ctx.Ellipse(anchorX, baseY, radius, baseRy, 0f, 0f, MathF.PI);
        ctx.ClosePath();
        ctx.Fill();

        ctx.StrokeStyle = strokeColor;
        ctx.LineWidth = strokeWidth;
        ctx.LineJoin = "round";

        // Contours. Vertical by construction now that the caps share a horizontal major axis.
        ctx.BeginPath();
        ctx.MoveTo(anchorX - radius, baseY);
        ctx.LineTo(anchorX - radius, topY);
        ctx.MoveTo(anchorX + radius, baseY);
        ctx.LineTo(anchorX + radius, topY);
        ctx.Stroke();

        // Base contour: only the near half is visible past the body.
        ctx.BeginPath();
        ctx.Ellipse(anchorX, baseY, radius, baseRy, 0f, 0f, MathF.PI);
        ctx.Stroke();

        // 2. Top cap, whole — its own squash, not the base's.
        ctx.FillStyle = topFill;
        ctx.BeginPath();
        ctx.Ellipse(anchorX, topY, radius, topRy, 0f, 0f, MathF.PI * 2f);
        ctx.Fill();
        ctx.Stroke();

        ctx.Restore();
    }

    /// <summary>The two vanishing points to take a horizontal circle's foreshortening from.</summary>
    /// <remarks>
    /// A circle has equal radii along <em>any</em> perpendicular pair of ground directions, so either
    /// axis family will do. A one-point grid puts both vanishing points on the centre of vision,
    /// which leaves the two directions coincident and collapses the ellipse to a line; the 45-degree
    /// distance points at <c>cv +/- focalLength</c> are a perpendicular pair in the same plane and
    /// are not degenerate.
    /// </remarks>
    private static (Point2D VpL, Point2D VpR) CircleAxisVanishingPoints(IDictionary grid)
    {
        var vpL = ExtractPoint(grid["vpL"]);
        var vpR = ExtractPoint(grid["vpR"]);
        if (MathF.Abs(vpR.X - vpL.X) > 1f) return (vpL, vpR);

        var horizonY = Convert.ToSingle(grid["horizonY"], CultureInfo.InvariantCulture);
        var cv = ExtractPoint(grid["cv"]);
        var focalLength = grid.Contains("focalLength")
            ? Convert.ToSingle(grid["focalLength"], CultureInfo.InvariantCulture)
            : 800f;
        return (new Point2D(cv.X - focalLength, horizonY), new Point2D(cv.X + focalLength, horizonY));
    }

    /// <summary>
    /// Half the drawn <em>height</em> of a horizontal circle whose drawn half-width is
    /// <paramref name="radius"/>, centred at <paramref name="centre"/>.
    /// </summary>
    /// <remarks>
    /// Taken from the ellipse the two ground directions would describe — semi-diameters along each,
    /// scaled so the horizontal extent comes out at exactly <paramref name="radius"/> — and then only
    /// its vertical extent is kept, because the cap is drawn axis-aligned. So the foreshortening still
    /// comes from the grid while the major axis stays square to the cylinder.
    /// </remarks>
    private static float GroundCircleSquash(Point2D vpL, Point2D vpR, Point2D centre, float radius)
    {
        static Point2D Unit(Point2D from, Point2D to)
        {
            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            var len = MathF.Sqrt(dx * dx + dy * dy);
            return len < 0.0001f ? new Point2D(1f, 0f) : new Point2D(dx / len, dy / len);
        }

        var uL = Unit(centre, vpL);
        var uR = Unit(centre, vpR);

        // Half the drawn width of an ellipse with conjugate semi-diameters a and b is
        // sqrt(a.x^2 + b.x^2), so this is the scale that makes it come out at exactly radius.
        var spread = MathF.Sqrt(uL.X * uL.X + uR.X * uR.X);
        var k = spread < 0.0001f ? radius : radius / spread;

        return k * MathF.Sqrt(uL.Y * uL.Y + uR.Y * uR.Y);
    }

    /// <summary>The two vanishing points to take a horizontal circle's conjugate diameters from.</summary>
    /// <remarks>
    /// A circle has equal radii along <em>any</em> perpendicular pair of ground directions, so either
    /// axis family will do. A one-point grid puts both vanishing points on the centre of vision,
    /// which leaves the two directions coincident and collapses the ellipse to a line; the 45-degree
    /// distance points at <c>cv +/- focalLength</c> are a perpendicular pair in the same plane and
    /// are not degenerate.
    /// </remarks>
    /// <summary>
    /// Conjugate semi-diameters of a horizontal circle centred at <paramref name="centre"/>, scaled so
    /// the ellipse is exactly <c>2 * radius</c> wide on screen.
    /// </summary>
    /// <remarks>
    /// Normalising each cap to the same drawn width is what keeps the contours vertical, as a
    /// vertical cylinder's silhouette must be; only the flatness is left to vary with height.
    /// </remarks>
    /// <summary>The two parameters at which the ellipse reaches its extreme x — its silhouette edges.</summary>
    /// <remarks>
    /// dx/dt is zero where <c>tan t = b.x / a.x</c>; the two roots are half a turn apart. Returned
    /// left-then-right so callers do not have to compare them again.
    /// </remarks>
    /// <summary>
    /// Which way round to sweep from <paramref name="from"/> to <paramref name="to"/> to take the
    /// near half of the ellipse — the half lower on screen.
    /// </summary>
    /// <summary>Appends an elliptical arc as a polyline; the ellipse is rotated, so ctx.Ellipse cannot carry it.</summary>
    public List<List<Dictionary<string, object?>>> SubdividePerspectiveQuad(object quadObj, int uCount, int vCount)
    {
        if (quadObj is not IList pts || pts.Count < 4)
            throw new ArgumentException("quadObj must contain 4 points [P0, P1, P2, P3]", nameof(quadObj));

        var p0 = ExtractPoint(pts[0]);
        var p1 = ExtractPoint(pts[1]);
        var p2 = ExtractPoint(pts[2]);
        var p3 = ExtractPoint(pts[3]);

        var grid = new List<List<Dictionary<string, object?>>>();

        for (var v = 0; v < vCount; v++)
        {
            var v0 = (float)v / vCount;
            var v1 = (float)(v + 1) / vCount;

            for (var u = 0; u < uCount; u++)
            {
                var u0 = (float)u / uCount;
                var u1 = (float)(u + 1) / uCount;

                Point2D Interp(float ui, float vi)
                {
                    var x = (1f - ui) * (1f - vi) * p0.X + ui * (1f - vi) * p1.X + ui * vi * p2.X + (1f - ui) * vi * p3.X;
                    var y = (1f - ui) * (1f - vi) * p0.Y + ui * (1f - vi) * p1.Y + ui * vi * p2.Y + (1f - ui) * vi * p3.Y;
                    return new Point2D(x, y);
                }

                var cell = new List<Dictionary<string, object?>>
                {
                    ToDict(Interp(u0, v0)),
                    ToDict(Interp(u1, v0)),
                    ToDict(Interp(u1, v1)),
                    ToDict(Interp(u0, v1))
                };
                grid.Add(cell);
            }
        }

        return grid;
    }

    public Dictionary<string, object?> VerifyPerspectiveConvergence(object linesList, object expectedVp, float maxToleranceDeg = 5f)
    {
        var vp = ExtractPoint(expectedVp);
        if (linesList is not IList lines || lines.Count == 0)
            return new Dictionary<string, object?> { ["passed"] = true, ["testedLines"] = 0 };

        var passed = true;
        var maxError = 0f;

        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i] is not IList pts || pts.Count < 2) continue;
            var a = ExtractPoint(pts[0]);
            var b = ExtractPoint(pts[1]);

            // Anchored at the midpoint, and folded to 90°, so the answer does not depend on which
            // end of the line was listed first. A line is undirected: it converges on a vanishing
            // point whether you name it top-to-bottom or bottom-to-top.
            //
            // Without the fold, listing a line away from the vanishing point reported ~179.9° — a
            // confident total-drift verdict on a line that converges perfectly. Worse, the failing
            // order is the natural one to write: a vertical listed from the top of the frame
            // downward, with the vanishing point above it. Found by a comic-studio agent, which
            // caught it only because 179.9 is too round a number to be a real measurement.
            var mid = new SKPoint((a.X + b.X) / 2f, (a.Y + b.Y) / 2f);

            var drawnAngle = MathF.Atan2(b.Y - a.Y, b.X - a.X) * 180f / MathF.PI;
            var targetAngle = MathF.Atan2(vp.Y - mid.Y, vp.X - mid.X) * 180f / MathF.PI;

            var diff = MathF.Abs(drawnAngle - targetAngle);
            if (diff > 180f) diff = 360f - diff;   // an angle difference is at most 180°
            if (diff > 90f) diff = 180f - diff;    // and a line's direction is modulo 180°

            if (diff > maxError) maxError = diff;
            if (diff > maxToleranceDeg) passed = false;
        }

        return new Dictionary<string, object?>
        {
            ["passed"] = passed,
            ["maxAngularErrorDeg"] = maxError,
            ["testedLines"] = lines.Count,
            ["message"] = passed ? "PASS: Lines converge correctly" : $"DRIFT: Max angular error of {maxError:F1}° exceeds {maxToleranceDeg:F1}° tolerance"
        };
    }
    #endregion
}
