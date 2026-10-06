namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using SkiaSharp;

public partial class ConstructiveDrawingToolkit
{
    #region Tapered Strokes, Feathering & Hair Ribbons
    public CanvasPath DrawTaperedStroke(CanvasRenderingContext2D ctx, object start, object cp1, object cp2, object end, float maxThickness, object? fillOrStrokeStyle = null)
    {
        var s = ExtractPoint(start);
        var c1 = ExtractPoint(cp1);
        var c2 = ExtractPoint(cp2);
        var e = ExtractPoint(end);
        return DrawTaperedStroke(ctx, s.X, s.Y, c1.X, c1.Y, c2.X, c2.Y, e.X, e.Y, maxThickness, fillOrStrokeStyle);
    }

    /// <summary>
    /// The tapered ink mark, as the shape it is: a sine-tapered envelope around a cubic Bézier,
    /// filled on <paramref name="ctx"/> and <b>returned</b> so it can be built on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Returning the path is the point. A mark that is only painted is finished; a mark you hold is
    /// geometry — <c>subtract</c> a bite out of it, fill it with a gradient running <i>across</i> the
    /// stroke rather than along it, clip inside it, union a run of them into one silhouette, or let
    /// it reach <c>outSvg</c> as vector instead of only as pixels. None of that was reachable while
    /// the call painted and returned nothing, and rebuilding the envelope by hand means duplicating
    /// the twenty-five-sample sweep below and getting the normals right.
    /// </para>
    /// <para>
    /// A script that ignores the return value behaves exactly as before, which is what makes this
    /// safe to add to a call that was <c>void</c>.
    /// </para>
    /// </remarks>
    public CanvasPath DrawTaperedStroke(CanvasRenderingContext2D ctx, float sx, float sy, float cp1x, float cp1y, float cp2x, float cp2y, float ex, float ey, float maxThickness, object? fillOrStrokeStyle = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var mark = BuildTaperedStroke(sx, sy, cp1x, cp1y, cp2x, cp2y, ex, ey, maxThickness);

        ctx.Save();
        if (fillOrStrokeStyle != null)
            ctx.FillStyle = fillOrStrokeStyle;
        ctx.Fill(mark);
        ctx.Restore();
        return mark;
    }

    /// <summary>
    /// The same envelope without a context to paint it on — for laying out marks, measuring them, or
    /// combining a run of them before anything is drawn.
    /// </summary>
    public CanvasPath CreateTaperedStrokePath(object start, object cp1, object cp2, object end, float maxThickness)
    {
        var s = ExtractPoint(start);
        var c1 = ExtractPoint(cp1);
        var c2 = ExtractPoint(cp2);
        var e = ExtractPoint(end);
        return BuildTaperedStroke(s.X, s.Y, c1.X, c1.Y, c2.X, c2.Y, e.X, e.Y, maxThickness);
    }

    static CanvasPath BuildTaperedStroke(float sx, float sy, float cp1x, float cp1y, float cp2x, float cp2y, float ex, float ey, float maxThickness)
    {
        const int steps = 24;
        var points = new (float x, float y, float nx, float ny, float thickness)[steps + 1];

        for (var i = 0; i <= steps; i++)
        {
            var t = (float)i / steps;
            var it = 1f - t;

            // Cubic Bezier position
            var x = it * it * it * sx + 3f * it * it * t * cp1x + 3f * it * t * t * cp2x + t * t * t * ex;
            var y = it * it * it * sy + 3f * it * it * t * cp1y + 3f * it * t * t * cp2y + t * t * t * ey;

            // Derivative for tangent & normal
            var dx = 3f * it * it * (cp1x - sx) + 6f * it * t * (cp2x - cp1x) + 3f * t * t * (ex - cp2x);
            var dy = 3f * it * it * (cp1y - sy) + 6f * it * t * (cp2y - cp1y) + 3f * t * t * (ey - cp2y);
            var len = MathF.Sqrt(dx * dx + dy * dy);
            if (len < 0.0001f) len = 1f;

            var nx = -dy / len;
            var ny = dx / len;
            var thickness = maxThickness * MathF.Sin(t * MathF.PI);

            points[i] = (x, y, nx, ny, thickness);
        }

        var path = new CanvasPath();
        path.MoveTo(points[0].x, points[0].y);

        // Forward pass along positive normal
        for (var i = 0; i <= steps; i++)
        {
            var p = points[i];
            path.LineTo(p.x + p.nx * (p.thickness * 0.5f), p.y + p.ny * (p.thickness * 0.5f));
        }

        // Backward pass along negative normal
        for (var i = steps; i >= 0; i--)
        {
            var p = points[i];
            path.LineTo(p.x - p.nx * (p.thickness * 0.5f), p.y - p.ny * (p.thickness * 0.5f));
        }

        path.ClosePath();
        return path;
    }

    public void DrawFeathering(CanvasRenderingContext2D ctx, object origin, float angleDeg, int count, float length, float spacing, object? strokeColor = null, float lineWidth = 1.2f)
    {
        var pt = ExtractPoint(origin);
        DrawFeathering(ctx, pt.X, pt.Y, angleDeg, count, length, spacing, strokeColor, lineWidth);
    }

    public void DrawFeathering(CanvasRenderingContext2D ctx, float ox, float oy, float angleDeg, int count, float length, float spacing, object? strokeColor = null, float lineWidth = 1.2f)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var rad = (angleDeg * MathF.PI) / 180f;
        var dx = MathF.Cos(rad);
        var dy = MathF.Sin(rad);
        var px = -dy;
        var py = dx;

        ctx.Save();
        if (strokeColor != null) ctx.StrokeStyle = strokeColor;
        ctx.LineWidth = lineWidth;
        ctx.LineCap = "round";

        for (var i = 0; i < count; i++)
        {
            var sx = ox + px * (i * spacing);
            var sy = oy + py * (i * spacing);
            var lenVar = length * (0.8f + 0.4f * MathF.Sin(i * 1.5f));
            var ex = sx + dx * lenVar;
            var ey = sy + dy * lenVar;

            ctx.BeginPath();
            ctx.MoveTo(sx, sy);
            ctx.LineTo(ex, ey);
            ctx.Stroke();
        }

        ctx.Restore();
    }

    public void DrawCrossContourHatch(CanvasRenderingContext2D ctx, float cx, float cy, float rx, float ry, float startAngle, float endAngle, int count = 8, object? strokeColor = null, float lineWidth = 1.2f)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ctx.Save();
        if (strokeColor != null) ctx.StrokeStyle = strokeColor;
        ctx.LineWidth = lineWidth;

        for (var i = 0; i < count; i++)
        {
            var t = (float)i / Math.Max(1, count - 1);
            var y = cy - ry * 0.5f + t * ry;
            ctx.BeginPath();
            ctx.Ellipse(cx, y, rx, ry * 0.25f, 0f, startAngle, endAngle);
            ctx.Stroke();
        }

        ctx.Restore();
    }

    public void DrawHairRibbon(CanvasRenderingContext2D ctx, object root, object tip, float bendFactor, float width, object fillTop, object fillUnderside, object? strokeColor = null, float strokeWidth = 2.0f)
    {
        var r = ExtractPoint(root);
        var t = ExtractPoint(tip);
        DrawHairRibbon(ctx, r.X, r.Y, t.X, t.Y, bendFactor, width, fillTop, fillUnderside, strokeColor, strokeWidth);
    }

    public void DrawHairRibbon(CanvasRenderingContext2D ctx, float rx, float ry, float tx, float ty, float bendFactor, float width, object fillTop, object fillUnderside, object? strokeColor = null, float strokeWidth = 2.0f)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var dx = tx - rx;
        var dy = ty - ry;
        var dist = MathF.Sqrt(dx * dx + dy * dy);
        if (dist < 0.0001f) dist = 1f;

        var nx = -dy / dist;
        var ny = dx / dist;

        var cp1x = rx + dx * 0.35f + nx * bendFactor;
        var cp1y = ry + dy * 0.35f + ny * bendFactor;
        var cp2x = rx + dx * 0.75f + nx * (bendFactor * 0.8f);
        var cp2y = ry + dy * 0.75f + ny * (bendFactor * 0.8f);

        var rootLowerX = rx + nx * width;
        var rootLowerY = ry + ny * width;
        var cp3x = rootLowerX + dx * 0.70f + nx * (bendFactor * 0.5f);
        var cp3y = rootLowerY + dy * 0.70f + ny * (bendFactor * 0.5f);
        var cp4x = rootLowerX + dx * 0.30f + nx * (bendFactor * 0.7f);
        var cp4y = rootLowerY + dy * 0.30f + ny * (bendFactor * 0.7f);

        ctx.Save();

        // 1. Top Ribbon Surface
        ctx.BeginPath();
        ctx.MoveTo(rx, ry);
        ctx.BezierCurveTo(cp1x, cp1y, cp2x, cp2y, tx, ty);
        ctx.BezierCurveTo(cp3x, cp3y, cp4x, cp4y, rootLowerX, rootLowerY);
        ctx.ClosePath();

        ctx.FillStyle = fillTop;
        ctx.Fill();

        if (strokeColor != null)
        {
            ctx.StrokeStyle = strokeColor;
            ctx.LineWidth = strokeWidth;
            ctx.LineJoin = "round";
            ctx.Stroke();
        }

        // 2. Inner Highlight / Underside Streak
        ctx.BeginPath();
        ctx.MoveTo(rx + dx * 0.15f + nx * (width * 0.3f), ry + dy * 0.15f + ny * (width * 0.3f));
        ctx.BezierCurveTo(
            cp1x + nx * (width * 0.2f), cp1y + ny * (width * 0.2f),
            cp2x + nx * (width * 0.2f), cp2y + ny * (width * 0.2f),
            tx - dx * 0.1f, ty - dy * 0.1f
        );
        ctx.StrokeStyle = fillUnderside;
        ctx.LineWidth = MathF.Max(1.2f, strokeWidth * 0.75f);
        ctx.Stroke();

        ctx.Restore();
    }
    #endregion

    #region Procedural Shaders & Material Presets
    public SKShader CreateHalftoneDotShader(object? options = null)
    {
        var optDict = JsInterop.AsDict(options);
        var dotSpacing = optDict != null && optDict.Contains("dotSpacing")
            ? Convert.ToSingle(optDict["dotSpacing"], CultureInfo.InvariantCulture)
            : 6.5f;

        var colorStr = optDict?["shadowColor"]?.ToString() ?? "#7f4124";
        var shadowColor = SkiaColorParser.Parse(colorStr);

        var resW = 900f;
        var resH = 750f;
        if (optDict != null && optDict.Contains("resolution") && optDict["resolution"] is IList resList && resList.Count >= 2)
        {
            resW = Convert.ToSingle(resList[0], CultureInfo.InvariantCulture);
            resH = Convert.ToSingle(resList[1], CultureInfo.InvariantCulture);
        }

        const string sksl = @"
            uniform float2 u_resolution;
            uniform float4 u_shadowColor;
            uniform float u_dotSpacing;

            half4 main(float2 coord) {
                float2 pos = mod(coord, u_dotSpacing) - (u_dotSpacing * 0.5);
                float dist = length(pos);
                float radius = u_dotSpacing * 0.38;
                float dot = smoothstep(radius + 0.4, radius - 0.4, dist);
                return u_shadowColor * dot;
            }
        ";

        var uniforms = new Dictionary<string, object>
        {
            ["u_resolution"] = new[] { resW, resH },
            ["u_shadowColor"] = new[] { shadowColor.Red / 255f, shadowColor.Green / 255f, shadowColor.Blue / 255f, shadowColor.Alpha / 255f },
            ["u_dotSpacing"] = dotSpacing
        };

        var skiaShaderApi = new SkiaShaderApi();
        return skiaShaderApi.Sksl(sksl, uniforms);
    }

    /// <summary>Twisted-cordage fibre: stretched-Y turbulence, as a value field by default.</summary>
    /// <remarks>
    /// <paramref name="luminanceOnly"/> defaults true because the documented use is a second pass
    /// over a coloured base under <c>overlay</c>, and raw Perlin carries a separate noise field per
    /// channel — which tints the rope in random hues rather than giving it fibre. Pass false for the
    /// unfiltered field; <c>Skia.Shader.perlinNoiseTurbulence(...)</c> is always unfiltered.
    /// </remarks>
    public SKShader CreateRopeFiberShader(float frequencyX = 0.08f, float frequencyY = 0.40f, int octaves = 3, int seed = 42, bool luminanceOnly = true)
    {
        var skiaShaderApi = new SkiaShaderApi();
        var noise = skiaShaderApi.PerlinNoiseTurbulence(frequencyX, frequencyY, octaves, seed);
        return luminanceOnly ? skiaShaderApi.Luminance(noise) : noise;
    }

    /// <summary>Sea-air vapour: isotropic fractal noise, as a value field by default.</summary>
    /// <remarks>
    /// Same reasoning as <c>createRopeFiberShader</c>. The alpha field is left varying, which is what
    /// makes the vapour wispy — greying the noise and flattening alpha together produces an opaque
    /// sheet, so the two corrections are not interchangeable.
    /// </remarks>
    public SKShader CreateAtmosphericCloudShader(float frequencyX = 0.015f, float frequencyY = 0.015f, int octaves = 4, int seed = 101, bool luminanceOnly = true)
    {
        var skiaShaderApi = new SkiaShaderApi();
        var noise = skiaShaderApi.PerlinNoiseFractal(frequencyX, frequencyY, octaves, seed);
        return luminanceOnly ? skiaShaderApi.Luminance(noise) : noise;
    }
    #endregion
}
