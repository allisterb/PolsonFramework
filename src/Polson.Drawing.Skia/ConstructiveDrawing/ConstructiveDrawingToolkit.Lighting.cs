namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using SkiaSharp;

public partial class ConstructiveDrawingToolkit
{
    #region Volumetric Lighting & Cast Shadows
    /// <summary>
    /// Projects a cast shadow onto the ground. <paramref name="groundOrGrid"/> selects the model:
    /// a number is a ground <em>line</em> at that Y (a 2D elevation, where depth comes from
    /// <c>options.groundDepth</c>); a <c>PerspectiveGrid</c> is a ground <em>plane</em>, and the
    /// footprint is then a true projective shadow needing no depth hint.
    /// </summary>
    public Dictionary<string, object?> ProjectCastShadow(object lightSource, object groundOrGrid, object objectVerticesOrBounds, object? options = null)
    {
        var light = ExtractPoint(lightSource);
        var opt = JsInterop.AsDict(options);
        var shadowColor = opt?["shadowColor"]?.ToString() ?? "#1c2733";
        var opacity = opt != null && opt.Contains("opacity") ? Convert.ToSingle(opt["opacity"], CultureInfo.InvariantCulture) : 0.65f;
        var penumbraBlur = opt != null && opt.Contains("penumbraBlur") ? Convert.ToSingle(opt["penumbraBlur"], CultureInfo.InvariantCulture) : 6f;

        var grid = JsInterop.AsDict(groundOrGrid);
        var hasHorizon = grid != null && grid.Contains("horizonY");

        var pairs = ExtractShadowCasters(objectVerticesOrBounds, hasHorizon ? null : Convert.ToSingle(groundOrGrid, CultureInfo.InvariantCulture));

        return hasHorizon
            ? ProjectOntoGroundPlane(light, Convert.ToSingle(grid!["horizonY"], CultureInfo.InvariantCulture), pairs, shadowColor, opacity, penumbraBlur)
            : ProjectOntoGroundLine(light, Convert.ToSingle(groundOrGrid, CultureInfo.InvariantCulture), pairs, opt, shadowColor, opacity, penumbraBlur);
    }

    /// <summary>
    /// The 2D elevation model. Every light ray meets the ground line at the same Y, so the projected
    /// vertices are collinear — a ground plane seen edge-on has no thickness. <c>groundDepth</c>
    /// supplies the recession the projection cannot know; the light still sets length and direction.
    /// </summary>
    private static Dictionary<string, object?> ProjectOntoGroundLine(
        Point2D light, float groundY, List<(Point2D Base, Point2D Top)> pairs,
        IDictionary? opt, string shadowColor, float opacity, float penumbraBlur)
    {
        var basePts = new List<Point2D>();
        var farPts = new List<Point2D>();

        foreach (var (basePt, top) in pairs)
        {
            var dy = top.Y - light.Y;
            if (MathF.Abs(dy) < 0.001f) dy = 0.001f;
            var t = (groundY - light.Y) / dy;

            basePts.Add(new Point2D(basePt.X, groundY));
            farPts.Add(new Point2D(light.X + t * (top.X - light.X), groundY));
        }

        var shadowPts = new List<Dictionary<string, object?>>();
        var groundDepth = 0f;

        if (basePts.Count >= 2)
        {
            // Default the depth from the contact footprint, so a caller that supplies no option still
            // gets a plausible shadow rather than an invisible sliver.
            var extent = basePts.Max(p => p.X) - basePts.Min(p => p.X);
            groundDepth = opt != null && opt.Contains("groundDepth")
                ? Convert.ToSingle(opt["groundDepth"], CultureInfo.InvariantCulture)
                : MathF.Max(4f, extent * 0.20f);

            foreach (var pt in basePts) shadowPts.Add(ToDict(pt));
            for (var i = farPts.Count - 1; i >= 0; i--)
            {
                shadowPts.Add(ToDict(new Point2D(farPts[i].X, groundY + groundDepth)));
            }
        }

        return new Dictionary<string, object?>
        {
            ["shadowPolygon"] = shadowPts,
            ["model"] = "groundLine",
            ["groundY"] = groundY,
            ["groundDepth"] = groundDepth,
            ["shadowColor"] = shadowColor,
            ["opacity"] = opacity,
            ["penumbraBlur"] = penumbraBlur
        };
    }

    /// <summary>
    /// The perspective model — the classical architectural construction, and the reason a
    /// <c>PerspectiveGrid</c> is worth passing.
    /// <para>
    /// The light is read as the vanishing point of the light rays, so its ground-projected rays
    /// converge on the horizon directly beneath it. Each shadow vertex is the meeting of the ray
    /// through the top vertex with the ground ray through the matching contact point. Because contact
    /// points at different depths sit at different image heights, the footprint comes out as a real
    /// receding quad — no <c>groundDepth</c> hint required.
    /// </para>
    /// </summary>
    private static Dictionary<string, object?> ProjectOntoGroundPlane(
        Point2D light, float horizonY, List<(Point2D Base, Point2D Top)> pairs,
        string shadowColor, float opacity, float penumbraBlur)
    {
        var vpShadow = new Point2D(light.X, horizonY);
        var basePts = new List<Point2D>();
        var shadowVerts = new List<Point2D>();

        foreach (var (basePt, top) in pairs)
        {
            var hit = IntersectLines(light, top, vpShadow, basePt)
                ?? throw new ArgumentException(
                    $"The light ray through ({top.X:F1}, {top.Y:F1}) runs parallel to the ground ray through its " +
                    $"contact point ({basePt.X:F1}, {basePt.Y:F1}), so the shadow never lands. Move the light off " +
                    "the horizon line, or away from directly above the object.",
                    nameof(light));

            if (hit.Y <= horizonY)
            {
                throw new ArgumentException(
                    $"The shadow of ({top.X:F1}, {top.Y:F1}) falls at or beyond the horizon (y {hit.Y:F1} <= " +
                    $"horizonY {horizonY:F1}), which is infinitely far away. Raise the light above the object, " +
                    "or move it further from the horizon.",
                    nameof(light));
            }

            basePts.Add(basePt);
            shadowVerts.Add(hit);
        }

        var shadowPts = new List<Dictionary<string, object?>>();
        if (basePts.Count >= 2)
        {
            foreach (var pt in basePts) shadowPts.Add(ToDict(pt));
            for (var i = shadowVerts.Count - 1; i >= 0; i--) shadowPts.Add(ToDict(shadowVerts[i]));
        }

        return new Dictionary<string, object?>
        {
            ["shadowPolygon"] = shadowPts,
            ["model"] = "perspective",
            ["horizonY"] = horizonY,
            ["groundY"] = basePts.Count > 0 ? basePts.Max(p => p.Y) : horizonY,
            ["vpShadow"] = ToDict(vpShadow),
            ["shadowColor"] = shadowColor,
            ["opacity"] = opacity,
            ["penumbraBlur"] = penumbraBlur
        };
    }

    /// <summary>
    /// Resolves the caster into (contact point, top vertex) pairs. A <c>PerspectiveBox</c> already
    /// carries both, which is what makes the perspective model exact; a bounds rect supplies the top
    /// and bottom edges; a bare point list is tops only, so it needs a ground line to stand on.
    /// </summary>
    private static List<(Point2D Base, Point2D Top)> ExtractShadowCasters(object shape, float? groundY)
    {
        var pairs = new List<(Point2D, Point2D)>();
        var dict = JsInterop.AsDict(shape);

        if (dict != null && dict.Contains("vertices") && dict["vertices"] is IList verts && verts.Count >= 8)
        {
            // PerspectiveBox: V0..V3 are the base, V4..V7 the matching top. The bottom face walks
            // V0, V1, V3, V2 (Manual 06 section 2), so follow that order or the quad self-crosses.
            foreach (var i in new[] { 0, 1, 3, 2 })
            {
                pairs.Add((ExtractPoint(verts[i]), ExtractPoint(verts[i + 4])));
            }
            return pairs;
        }

        if (dict != null && dict.Contains("width") && dict.Contains("height"))
        {
            var bx = Convert.ToSingle(dict["x"], CultureInfo.InvariantCulture);
            var by = Convert.ToSingle(dict["y"], CultureInfo.InvariantCulture);
            var bw = Convert.ToSingle(dict["width"], CultureInfo.InvariantCulture);
            var bh = Convert.ToSingle(dict["height"], CultureInfo.InvariantCulture);

            pairs.Add((new Point2D(bx, by + bh), new Point2D(bx, by)));
            pairs.Add((new Point2D(bx + bw, by + bh), new Point2D(bx + bw, by)));
            return pairs;
        }

        if (shape is IList list)
        {
            foreach (var item in list)
            {
                var entry = JsInterop.AsDict(item);
                if (entry != null && entry.Contains("top") && entry.Contains("base"))
                {
                    pairs.Add((ExtractPoint(entry["base"]), ExtractPoint(entry["top"])));
                    continue;
                }

                var top = ExtractPoint(item);
                if (groundY is not float line)
                {
                    throw new ArgumentException(
                        "A bare point list gives top vertices with no contact points, so it cannot be projected " +
                        "onto a perspective ground plane. Pass a PerspectiveBox from Drawing.createPerspectiveBox(...), " +
                        "a bounds rect, or a list of { top, base } pairs.",
                        nameof(shape));
                }
                pairs.Add((new Point2D(top.X, line), top));
            }
        }

        return pairs;
    }

    /// <summary>Meeting point of the lines through (a1, a2) and (b1, b2); null when they are parallel.</summary>
    private static Point2D? IntersectLines(Point2D a1, Point2D a2, Point2D b1, Point2D b2)
    {
        var dxA = a2.X - a1.X;
        var dyA = a2.Y - a1.Y;
        var dxB = b2.X - b1.X;
        var dyB = b2.Y - b1.Y;

        var denominator = (dxA * dyB) - (dyA * dxB);
        if (MathF.Abs(denominator) < 1e-5f) return null;

        var t = (((b1.X - a1.X) * dyB) - ((b1.Y - a1.Y) * dxB)) / denominator;
        return new Point2D(a1.X + (t * dxA), a1.Y + (t * dyA));
    }

    public void DrawCastShadow(CanvasRenderingContext2D ctx, object shadowPolygonOrResult, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        IList? pts = null;
        var shadowColor = "#1c2733";
        var opacity = 0.65f;
        var blur = 6f;

        if (JsInterop.AsDict(shadowPolygonOrResult) is IDictionary res && res.Contains("shadowPolygon"))
        {
            pts = res["shadowPolygon"] as IList;
            if (res.Contains("shadowColor")) shadowColor = res["shadowColor"]?.ToString() ?? shadowColor;
            if (res.Contains("opacity")) opacity = Convert.ToSingle(res["opacity"], CultureInfo.InvariantCulture);
            if (res.Contains("penumbraBlur")) blur = Convert.ToSingle(res["penumbraBlur"], CultureInfo.InvariantCulture);
        }
        else if (shadowPolygonOrResult is IList list)
        {
            pts = list;
        }

        var opt = JsInterop.AsDict(options);
        if (opt != null)
        {
            if (opt.Contains("shadowColor")) shadowColor = opt["shadowColor"]?.ToString() ?? shadowColor;
            if (opt.Contains("opacity")) opacity = Convert.ToSingle(opt["opacity"], CultureInfo.InvariantCulture);
            if (opt.Contains("blur")) blur = Convert.ToSingle(opt["blur"], CultureInfo.InvariantCulture);
        }

        if (pts == null || pts.Count < 3)
        {
            throw new ArgumentException(
                $"DrawCastShadow needs a polygon of at least 3 points, got {pts?.Count ?? 0}. " +
                "Pass the result of Drawing.projectCastShadow(...) or a point list.",
                nameof(shadowPolygonOrResult));
        }

        // A polygon with no area fills to nothing. Report it rather than returning quietly, so a shadow
        // that silently fails to appear is a visible error instead of a mystery.
        var corners = new List<Point2D>();
        for (var i = 0; i < pts.Count; i++) corners.Add(ExtractPoint(pts[i]));
        var spanX = corners.Max(p => p.X) - corners.Min(p => p.X);
        var spanY = corners.Max(p => p.Y) - corners.Min(p => p.Y);

        if (spanX < 0.5f || spanY < 0.5f)
        {
            throw new ArgumentException(
                $"DrawCastShadow was given a degenerate polygon ({spanX:F2} x {spanY:F2}px) that would fill nothing. " +
                "A ground plane seen edge-on projects to a line: pass a 'groundDepth' option to " +
                "Drawing.projectCastShadow(...) to give the footprint depth.",
                nameof(shadowPolygonOrResult));
        }

        ctx.Save();
        ctx.GlobalAlpha = opacity;
        ctx.FillStyle = shadowColor;
        if (blur > 0.5f)
        {
            ctx.ShadowColor = shadowColor;
            ctx.ShadowBlur = blur;
        }

        ctx.BeginPath();
        var first = ExtractPoint(pts[0]);
        ctx.MoveTo(first.X, first.Y);
        for (var i = 1; i < pts.Count; i++)
        {
            var p = ExtractPoint(pts[i]);
            ctx.LineTo(p.X, p.Y);
        }
        ctx.ClosePath();
        ctx.Fill();

        ctx.Restore();
    }

    public void RenderVolumetricSphere(CanvasRenderingContext2D ctx, float cx, float cy, float radius, object? lightDirection = null, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var opt = JsInterop.AsDict(options);
        var baseColor = opt?["baseColor"]?.ToString() ?? "#c4a382";
        var shadowColor = opt?["shadowColor"]?.ToString() ?? "#3a281e";
        var highlightColor = opt?["highlightColor"]?.ToString() ?? "#fff6e8";
        var bounceColor = opt?["bounceColor"]?.ToString() ?? "#556e8c";
        var drawGroundShadow = opt == null || !opt.Contains("drawGroundShadow") || Convert.ToBoolean(opt["drawGroundShadow"]);

        var lDir = ExtractPoint(lightDirection, -0.6f, -0.6f);
        var len = MathF.Sqrt(lDir.X * lDir.X + lDir.Y * lDir.Y);
        if (len < 0.001f) len = 1f;
        var lx = lDir.X / len;
        var ly = lDir.Y / len;

        ctx.Save();

        // 1. Ground Cast Shadow (if enabled)
        if (drawGroundShadow)
        {
            var groundY = cy + radius + 8f;
            var shadowCx = cx - lx * (radius * 0.7f);
            var shadowRx = radius * 1.15f;
            var shadowRy = radius * 0.28f;

            ctx.Save();
            ctx.FillStyle = "#1c140e";
            ctx.GlobalAlpha = 0.55f;
            ctx.ShadowColor = "#1c140e";
            ctx.ShadowBlur = 8f;
            ctx.BeginPath();
            ctx.Ellipse(shadowCx, groundY, shadowRx, shadowRy, 0f, 0f, MathF.PI * 2f);
            ctx.Fill();
            ctx.Restore();
        }

        // 2. Base Sphere Fill & 3D Lighting Radial Gradient
        var lightSpotX = cx + lx * (radius * 0.45f);
        var lightSpotY = cy + ly * (radius * 0.45f);

        var grad = ctx.CreateRadialGradient(lightSpotX, lightSpotY, radius * 0.1f, cx, cy, radius * 1.05f);
        grad.AddColorStop(0.0f, highlightColor);
        grad.AddColorStop(0.35f, baseColor);
        grad.AddColorStop(0.75f, shadowColor);
        grad.AddColorStop(1.0f, shadowColor);

        ctx.FillStyle = grad;
        ctx.BeginPath();
        ctx.Arc(cx, cy, radius, 0f, MathF.PI * 2f);
        ctx.Fill();

        // 3. Ambient Bounce / Reflected Light on shadow side
        var bounceSpotX = cx - lx * (radius * 0.65f);
        var bounceSpotY = cy - ly * (radius * 0.65f);

        ctx.Save();
        ctx.BeginPath();
        ctx.Arc(cx, cy, radius, 0f, MathF.PI * 2f);
        ctx.Clip();

        var bounceGrad = ctx.CreateRadialGradient(bounceSpotX, bounceSpotY, radius * 0.05f, bounceSpotX, bounceSpotY, radius * 0.75f);
        bounceGrad.AddColorStop(0.0f, bounceColor);
        bounceGrad.AddColorStop(1.0f, "rgba(0,0,0,0)");

        ctx.GlobalAlpha = 0.45f;
        ctx.GlobalCompositeOperation = "screen";
        ctx.FillStyle = bounceGrad;
        ctx.BeginPath();
        ctx.Arc(cx, cy, radius, 0f, MathF.PI * 2f);
        ctx.Fill();
        ctx.Restore();

        // 4. Specular Highlight Glint
        ctx.Save();
        ctx.FillStyle = "#ffffff";
        ctx.GlobalAlpha = 0.85f;
        ctx.BeginPath();
        ctx.Ellipse(lightSpotX - 2f, lightSpotY - 2f, radius * 0.18f, radius * 0.12f, -0.3f, 0f, MathF.PI * 2f);
        ctx.Fill();
        ctx.Restore();

        ctx.Restore();
    }

    public void RenderVolumetricCylinder(CanvasRenderingContext2D ctx, float x, float y, float width, float height, object? lightDirection = null, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var opt = JsInterop.AsDict(options);
        var baseColor = opt?["baseColor"]?.ToString() ?? "#c4a382";
        var shadowColor = opt?["shadowColor"]?.ToString() ?? "#3a281e";
        var highlightColor = opt?["highlightColor"]?.ToString() ?? "#fff6e8";
        var bounceColor = opt?["bounceColor"]?.ToString() ?? "#556e8c";

        var ry = width * 0.22f;
        var rx = width * 0.5f;
        var cx = x + rx;

        ctx.Save();

        // 1. Cylinder Body Longitudinal Gradient
        var grad = ctx.CreateLinearGradient(x, y, x + width, y);
        grad.AddColorStop(0.0f, shadowColor);
        grad.AddColorStop(0.25f, highlightColor);
        grad.AddColorStop(0.55f, baseColor);
        grad.AddColorStop(0.85f, shadowColor);
        grad.AddColorStop(1.0f, bounceColor);

        ctx.FillStyle = grad;
        ctx.BeginPath();
        ctx.MoveTo(x, y + ry);
        ctx.LineTo(x, y + height - ry);
        ctx.Ellipse(cx, y + height - ry, rx, ry, 0f, 0f, MathF.PI);
        ctx.LineTo(x + width, y + ry);
        ctx.Ellipse(cx, y + ry, rx, ry, 0f, 0f, MathF.PI);
        ctx.ClosePath();
        ctx.Fill();

        // 2. Top Elliptical Cap
        var topGrad = ctx.CreateRadialGradient(cx, y + ry * 0.6f, rx * 0.1f, cx, y + ry, rx);
        topGrad.AddColorStop(0.0f, highlightColor);
        topGrad.AddColorStop(1.0f, baseColor);

        ctx.FillStyle = topGrad;
        ctx.BeginPath();
        ctx.Ellipse(cx, y + ry, rx, ry, 0f, 0f, MathF.PI * 2f);
        ctx.Fill();

        // Top cap stroke
        ctx.StrokeStyle = shadowColor;
        ctx.LineWidth = 1.5f;
        ctx.Stroke();

        ctx.Restore();
    }

    public Dictionary<string, object?> CreateThreePointLighting(object? options = null)
    {
        var opt = JsInterop.AsDict(options);
        var keyColor = opt?["keyColor"]?.ToString() ?? "#fff3d6";
        var fillColor = opt?["fillColor"]?.ToString() ?? "#8cb5db";
        var rimColor = opt?["rimColor"]?.ToString() ?? "#ffffff";

        var keyAngleDeg = opt != null && opt.Contains("keyAngleDeg") ? Convert.ToSingle(opt["keyAngleDeg"], CultureInfo.InvariantCulture) : -45f;
        var fillAngleDeg = opt != null && opt.Contains("fillAngleDeg") ? Convert.ToSingle(opt["fillAngleDeg"], CultureInfo.InvariantCulture) : 60f;
        var rimAngleDeg = opt != null && opt.Contains("rimAngleDeg") ? Convert.ToSingle(opt["rimAngleDeg"], CultureInfo.InvariantCulture) : 135f;

        return new Dictionary<string, object?>
        {
            ["keyLight"] = new Dictionary<string, object?> { ["angleDeg"] = keyAngleDeg, ["color"] = keyColor, ["intensity"] = 0.75f },
            ["fillLight"] = new Dictionary<string, object?> { ["angleDeg"] = fillAngleDeg, ["color"] = fillColor, ["intensity"] = 0.30f },
            ["rimLight"] = new Dictionary<string, object?> { ["angleDeg"] = rimAngleDeg, ["color"] = rimColor, ["intensity"] = 0.90f }
        };
    }

    /// <summary>
    /// Renders grazing edge light along a silhouette, culled and faded by the light's angle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <paramref name="lightAngleDeg"/> points <em>from</em> the form <em>toward</em> the light, the
    /// same convention as <c>lightDirection</c> elsewhere in this toolkit. Each stretch of contour is
    /// weighted by <c>max(0, n · L)^spread</c> with <c>n</c> its outward normal, so only the arc
    /// facing the light is drawn and it fades where the form turns away. That is what makes it read
    /// as light rather than as ink: the call used to stroke the entire point list at full opacity
    /// whatever the angle was, which is an outline, and a live run had to hand-trim its point lists
    /// down to the arc that should have caught light.
    /// </para>
    /// <para>
    /// The band sits <em>inside</em> the contour, offset inward by half the thickness, because a rim
    /// is light on the form rather than a wire beside it. The previous version pushed it two pixels
    /// <em>outward</em> along the light vector — the "pale wire floating clear of the figure" the same
    /// run reported. A point list that merely approximates the silhouette will still float: the list
    /// has to <em>be</em> the silhouette, and nothing here can know the form well enough to correct it.
    /// </para>
    /// <para>
    /// <paramref name="options"/> takes <c>{ spread }</c>, the exponent on the cosine — higher is a
    /// tighter rim, 1 is a broad Lambertian falloff over the whole lit half.
    /// </para>
    /// </remarks>
    public void DrawRimLight(CanvasRenderingContext2D ctx, object boundsOrPts, float lightAngleDeg, object? rimColor = null, float thickness = 2.5f, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var opt = JsInterop.AsDict(options);
        var spread = opt != null && opt.Contains("spread")
            ? MathF.Max(0.1f, Convert.ToSingle(opt["spread"], CultureInfo.InvariantCulture))
            : 2f;
        var color = rimColor?.ToString() ?? "#ffffff";

        var contour = RimContour(boundsOrPts);
        if (contour.Count < 2) return;

        var rad = (lightAngleDeg * MathF.PI) / 180f;
        var lightX = MathF.Cos(rad);
        var lightY = MathF.Sin(rad);

        // "Outward" is away from the middle of the contour, which is what a silhouette gives us.
        var cx = 0f;
        var cy = 0f;
        foreach (var p in contour)
        {
            cx += p.X;
            cy += p.Y;
        }
        cx /= contour.Count;
        cy /= contour.Count;

        var weights = new float[contour.Count];
        var offsets = new Point2D[contour.Count];
        var inset = thickness * 0.5f;

        for (var i = 0; i < contour.Count; i++)
        {
            var before = contour[Math.Max(0, i - 1)];
            var after = contour[Math.Min(contour.Count - 1, i + 1)];
            var dx = after.X - before.X;
            var dy = after.Y - before.Y;
            var len = MathF.Sqrt(dx * dx + dy * dy);
            if (len < 0.0001f)
            {
                weights[i] = 0f;
                offsets[i] = contour[i];
                continue;
            }

            var nx = dy / len;
            var ny = -dx / len;
            if (nx * (contour[i].X - cx) + ny * (contour[i].Y - cy) < 0f)
            {
                nx = -nx;
                ny = -ny;
            }

            var facing = nx * lightX + ny * lightY;
            weights[i] = facing <= 0f ? 0f : MathF.Pow(facing, spread);
            offsets[i] = new Point2D(contour[i].X - nx * inset, contour[i].Y - ny * inset);
        }

        var baseAlpha = ctx.GlobalAlpha;

        ctx.Save();
        ctx.StrokeStyle = color;
        ctx.LineWidth = thickness;
        // Butt caps, because runs abut: a round cap would overlap its neighbour and bead the seam.
        ctx.LineCap = "butt";
        ctx.LineJoin = "round";

        // Segments are grouped into runs of equal quantised weight and stroked as polylines. Stroking
        // each segment separately would be simpler and wrong — at any alpha below 1 the caps overlap
        // and the rim comes out beaded rather than continuous.
        const int levels = 14;
        var start = -1;
        var level = 0;

        for (var i = 0; i <= contour.Count - 1; i++)
        {
            var segmentLevel = i < contour.Count - 1
                ? (int)MathF.Round((weights[i] + weights[i + 1]) * 0.5f * levels)
                : 0;

            if (start >= 0 && (segmentLevel != level || i == contour.Count - 1))
            {
                StrokeRun(ctx, offsets, start, i, baseAlpha * level / levels);
                start = -1;
            }
            if (i < contour.Count - 1 && segmentLevel > 0 && start < 0)
            {
                start = i;
                level = segmentLevel;
            }
        }

        ctx.Restore();
    }

    /// <summary>Strokes <c>offsets[from..to]</c> as one polyline at <paramref name="alpha"/>.</summary>
    private static void StrokeRun(CanvasRenderingContext2D ctx, Point2D[] offsets, int from, int to, float alpha)
    {
        if (to <= from || alpha <= 0f) return;

        ctx.GlobalAlpha = MathF.Min(1f, alpha);
        ctx.BeginPath();
        ctx.MoveTo(offsets[from].X, offsets[from].Y);
        for (var i = from + 1; i <= to; i++) ctx.LineTo(offsets[i].X, offsets[i].Y);
        ctx.Stroke();
    }

    /// <summary>The contour to light: a rect's perimeter, or the point list as given.</summary>
    /// <remarks>
    /// A rect is closed, so it gets its fourth edge back and all four can catch light — the previous
    /// version drew a single straight line down either the left or the right edge and could not rim
    /// the top at all. A point list is left open: a silhouette arc closed with a chord would light
    /// the chord.
    /// </remarks>
    private static List<Point2D> RimContour(object boundsOrPts)
    {
        var contour = new List<Point2D>();

        if (JsInterop.AsDict(boundsOrPts) is IDictionary b && b.Contains("x") && b.Contains("width"))
        {
            var bx = Convert.ToSingle(b["x"], CultureInfo.InvariantCulture);
            var by = Convert.ToSingle(b["y"], CultureInfo.InvariantCulture);
            var bw = Convert.ToSingle(b["width"], CultureInfo.InvariantCulture);
            var bh = Convert.ToSingle(b["height"], CultureInfo.InvariantCulture);

            // Sampled along each edge so the falloff can vary across it, not just between edges.
            const int perEdge = 8;
            Point2D[] corners =
            [
                new(bx, by), new(bx + bw, by), new(bx + bw, by + bh), new(bx, by + bh), new(bx, by)
            ];
            for (var e = 0; e < 4; e++)
            {
                for (var s = 0; s < perEdge; s++)
                {
                    var t = s / (float)perEdge;
                    contour.Add(new Point2D(
                        corners[e].X + (corners[e + 1].X - corners[e].X) * t,
                        corners[e].Y + (corners[e + 1].Y - corners[e].Y) * t));
                }
            }
            contour.Add(corners[0]);
        }
        else if (boundsOrPts is IList pts)
        {
            foreach (var p in pts) contour.Add(ExtractPoint(p));
        }

        return contour;
    }

    public SKShader CreateVolumetricSphereShader(object? options = null)
    {
        var opt = JsInterop.AsDict(options);
        var lightColor = opt?["lightColor"]?.ToString() ?? "#fff6e8";
        var baseColor = opt?["baseColor"]?.ToString() ?? "#c4a382";
        var shadowColor = opt?["shadowColor"]?.ToString() ?? "#3a281e";

        var lc = SkiaColorParser.Parse(lightColor);
        var bc = SkiaColorParser.Parse(baseColor);
        var sc = SkiaColorParser.Parse(shadowColor);

        const string sksl = @"
            uniform float2 u_center;
            uniform float u_radius;
            uniform float3 u_lightDir;
            uniform float4 u_lightColor;
            uniform float4 u_baseColor;
            uniform float4 u_shadowColor;

            half4 main(float2 coord) {
                float2 d = (coord - u_center) / u_radius;
                float distSq = dot(d, d);
                if (distSq > 1.0) {
                    return half4(0.0);
                }
                float nz = sqrt(1.0 - distSq);
                float3 normal = float3(d.x, d.y, nz);

                float diff = max(0.0, dot(normal, normalize(u_lightDir)));
                float4 litColor = mix(u_shadowColor, u_baseColor, diff);

                // Specular glint
                float3 halfVec = normalize(u_lightDir + float3(0.0, 0.0, 1.0));
                float spec = pow(max(0.0, dot(normal, halfVec)), 24.0);
                litColor += u_lightColor * spec * 0.8;

                return half4(litColor);
            }
        ";

        var uniforms = new Dictionary<string, object>
        {
            ["u_center"] = new[] { 400f, 300f },
            ["u_radius"] = 150f,
            ["u_lightDir"] = new[] { 0.577f, -0.577f, 0.577f },
            ["u_lightColor"] = new[] { lc.Red / 255f, lc.Green / 255f, lc.Blue / 255f, 1f },
            ["u_baseColor"] = new[] { bc.Red / 255f, bc.Green / 255f, bc.Blue / 255f, 1f },
            ["u_shadowColor"] = new[] { sc.Red / 255f, sc.Green / 255f, sc.Blue / 255f, 1f }
        };

        var skiaShaderApi = new SkiaShaderApi();
        return skiaShaderApi.Sksl(sksl, uniforms);
    }
    #endregion
}
