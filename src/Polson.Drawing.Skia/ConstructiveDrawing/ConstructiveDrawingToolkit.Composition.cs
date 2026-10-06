namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using SkiaSharp;

public partial class ConstructiveDrawingToolkit
{
    #region Composition Armatures, Notan & Visual Emphasis
    public Dictionary<string, object?> CreateCompositionGrid(float width, float height, string type = "ruleOfThirds", object? options = null)
    {
        var gridType = type.Trim().ToLowerInvariant();
        var lines = new List<List<Dictionary<string, object?>>>();
        var powerPoints = new Dictionary<string, object?>();

        switch (gridType)
        {
            case "ruleofthirds":
            case "thirds":
            default:
                gridType = "ruleOfThirds";
                var x1 = width / 3f;
                var x2 = width * 2f / 3f;
                var y1 = height / 3f;
                var y2 = height * 2f / 3f;

                // 2 Vertical Lines
                lines.Add(new List<Dictionary<string, object?>> { ToDict(new Point2D(x1, 0)), ToDict(new Point2D(x1, height)) });
                lines.Add(new List<Dictionary<string, object?>> { ToDict(new Point2D(x2, 0)), ToDict(new Point2D(x2, height)) });

                // 2 Horizontal Lines
                lines.Add(new List<Dictionary<string, object?>> { ToDict(new Point2D(0, y1)), ToDict(new Point2D(width, y1)) });
                lines.Add(new List<Dictionary<string, object?>> { ToDict(new Point2D(0, y2)), ToDict(new Point2D(width, y2)) });

                powerPoints["topLeft"] = ToDict(new Point2D(x1, y1));
                powerPoints["topRight"] = ToDict(new Point2D(x2, y1));
                powerPoints["bottomLeft"] = ToDict(new Point2D(x1, y2));
                powerPoints["bottomRight"] = ToDict(new Point2D(x2, y2));
                break;

            case "goldenratio":
            case "goldenspiral":
            case "fibonacci":
                gridType = "goldenRatio";
                const float phi = 1.6180339887f;
                const float r = 1f / phi; // ~0.618

                var gx1 = width * (1f - r);
                var gx2 = width * r;
                var gy1 = height * (1f - r);
                var gy2 = height * r;

                lines.Add(new List<Dictionary<string, object?>> { ToDict(new Point2D(gx1, 0)), ToDict(new Point2D(gx1, height)) });
                lines.Add(new List<Dictionary<string, object?>> { ToDict(new Point2D(gx2, 0)), ToDict(new Point2D(gx2, height)) });
                lines.Add(new List<Dictionary<string, object?>> { ToDict(new Point2D(0, gy1)), ToDict(new Point2D(width, gy1)) });
                lines.Add(new List<Dictionary<string, object?>> { ToDict(new Point2D(0, gy2)), ToDict(new Point2D(width, gy2)) });

                // Diagonal bar
                lines.Add(new List<Dictionary<string, object?>> { ToDict(new Point2D(0, 0)), ToDict(new Point2D(width, height)) });
                lines.Add(new List<Dictionary<string, object?>> { ToDict(new Point2D(width, 0)), ToDict(new Point2D(gx1, height)) });

                powerPoints["goldenEye"] = ToDict(new Point2D(gx2, gy2));
                powerPoints["secondaryEye"] = ToDict(new Point2D(gx1, gy1));
                break;

            case "dynamicsymmetry":
            case "harmonicarmature":
                gridType = "dynamicSymmetry";
                // 1. Corner Diagonals
                lines.Add(new List<Dictionary<string, object?>> { ToDict(new Point2D(0, 0)), ToDict(new Point2D(width, height)) });
                lines.Add(new List<Dictionary<string, object?>> { ToDict(new Point2D(width, 0)), ToDict(new Point2D(0, height)) });

                // 2. Midpoint Cross
                lines.Add(new List<Dictionary<string, object?>> { ToDict(new Point2D(width * 0.5f, 0)), ToDict(new Point2D(width * 0.5f, height)) });
                lines.Add(new List<Dictionary<string, object?>> { ToDict(new Point2D(0, height * 0.5f)), ToDict(new Point2D(width, height * 0.5f)) });

                // 3. Midpoint Diamond
                lines.Add(new List<Dictionary<string, object?>> { ToDict(new Point2D(width * 0.5f, 0)), ToDict(new Point2D(width, height * 0.5f)) });
                lines.Add(new List<Dictionary<string, object?>> { ToDict(new Point2D(width, height * 0.5f)), ToDict(new Point2D(width * 0.5f, height)) });
                lines.Add(new List<Dictionary<string, object?>> { ToDict(new Point2D(width * 0.5f, height)), ToDict(new Point2D(0, height * 0.5f)) });
                lines.Add(new List<Dictionary<string, object?>> { ToDict(new Point2D(0, height * 0.5f)), ToDict(new Point2D(width * 0.5f, 0)) });

                powerPoints["center"] = ToDict(new Point2D(width * 0.5f, height * 0.5f));
                powerPoints["harmonicTopLeft"] = ToDict(new Point2D(width * 0.25f, height * 0.25f));
                powerPoints["harmonicTopRight"] = ToDict(new Point2D(width * 0.75f, height * 0.25f));
                break;

            case "triangle":
            case "pyramid":
                gridType = "triangle";
                var apex = new Point2D(width * 0.5f, height * 0.15f);
                var botL = new Point2D(width * 0.10f, height * 0.88f);
                var botR = new Point2D(width * 0.90f, height * 0.88f);

                lines.Add(new List<Dictionary<string, object?>> { ToDict(apex), ToDict(botL) });
                lines.Add(new List<Dictionary<string, object?>> { ToDict(apex), ToDict(botR) });
                lines.Add(new List<Dictionary<string, object?>> { ToDict(botL), ToDict(botR) });
                lines.Add(new List<Dictionary<string, object?>> { ToDict(apex), ToDict(new Point2D(width * 0.5f, height * 0.88f)) });

                powerPoints["apex"] = ToDict(apex);
                powerPoints["center"] = ToDict(new Point2D(width * 0.5f, height * 0.55f));
                break;
        }

        return new Dictionary<string, object?>
        {
            ["type"] = gridType,
            ["width"] = width,
            ["height"] = height,
            ["lines"] = lines,
            ["powerPoints"] = powerPoints
        };
    }

    public void DrawCompositionGrid(CanvasRenderingContext2D ctx, object gridObjOrType, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        IDictionary? grid = null;

        if (gridObjOrType is string typeStr)
        {
            grid = CreateCompositionGrid(ctx.Canvas.Width, ctx.Canvas.Height, typeStr, options);
        }
        else if (JsInterop.AsDict(gridObjOrType) is IDictionary dict)
        {
            grid = dict;
        }

        if (grid == null) return;

        var opt = JsInterop.AsDict(options);
        var lineColor = opt?["lineColor"]?.ToString() ?? "#3ba3d0";
        var pointColor = opt?["pointColor"]?.ToString() ?? "#e74c3c";
        var lineWidth = opt != null && opt.Contains("lineWidth") ? Convert.ToSingle(opt["lineWidth"], CultureInfo.InvariantCulture) : 1.2f;
        var opacity = opt != null && opt.Contains("opacity") ? Convert.ToSingle(opt["opacity"], CultureInfo.InvariantCulture) : 0.45f;

        ctx.Save();
        ctx.GlobalAlpha = opacity;
        ctx.StrokeStyle = lineColor;
        ctx.FillStyle = pointColor;
        ctx.LineWidth = lineWidth;

        // 1. Draw Grid Lines
        if (grid["lines"] is IList lines)
        {
            foreach (var l in lines)
            {
                if (l is not IList pts || pts.Count < 2) continue;
                var a = ExtractPoint(pts[0]);
                var b = ExtractPoint(pts[1]);
                ctx.BeginPath();
                ctx.MoveTo(a.X, a.Y);
                ctx.LineTo(b.X, b.Y);
                ctx.Stroke();
            }
        }

        // 2. Draw Focal Power Points
        if (JsInterop.AsDict(grid["powerPoints"]) is IDictionary pps)
        {
            foreach (DictionaryEntry de in pps)
            {
                var pt = ExtractPoint(de.Value);
                ctx.BeginPath();
                ctx.Arc(pt.X, pt.Y, 5f, 0f, MathF.PI * 2f);
                ctx.Fill();
            }
        }

        ctx.Restore();
    }

    public void DrawVignette(CanvasRenderingContext2D ctx, float width, float height, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var opt = JsInterop.AsDict(options);
        var vignetteColor = opt?["vignetteColor"]?.ToString() ?? "#080b10";
        var intensity = opt != null && opt.Contains("intensity") ? Convert.ToSingle(opt["intensity"], CultureInfo.InvariantCulture) : 0.65f;
        var radius = opt != null && opt.Contains("radius") ? Convert.ToSingle(opt["radius"], CultureInfo.InvariantCulture) : MathF.Max(width, height) * 0.72f;

        var cx = width * 0.5f;
        var cy = height * 0.5f;

        ctx.Save();
        ctx.GlobalAlpha = intensity;
        var grad = ctx.CreateRadialGradient(cx, cy, radius * 0.35f, cx, cy, radius);
        grad.AddColorStop(0.0f, "rgba(0,0,0,0)");
        grad.AddColorStop(0.65f, "rgba(0,0,0,0.15)");
        grad.AddColorStop(1.0f, vignetteColor);

        ctx.FillStyle = grad;
        ctx.FillRect(0, 0, width, height);
        ctx.Restore();
    }

    public void DrawLeadingLines(CanvasRenderingContext2D ctx, object originPoints, object focalPoint, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var fp = ExtractPoint(focalPoint);
        if (originPoints is not IList list || list.Count == 0) return;

        var opt = JsInterop.AsDict(options);
        var lineColor = opt?["lineColor"]?.ToString() ?? "#e74c3c";
        var lineWidth = opt != null && opt.Contains("lineWidth") ? Convert.ToSingle(opt["lineWidth"], CultureInfo.InvariantCulture) : 1.2f;
        var opacity = opt != null && opt.Contains("opacity") ? Convert.ToSingle(opt["opacity"], CultureInfo.InvariantCulture) : 0.35f;

        ctx.Save();
        ctx.GlobalAlpha = opacity;
        ctx.StrokeStyle = lineColor;
        ctx.LineWidth = lineWidth;

        foreach (var item in list)
        {
            var p = ExtractPoint(item);
            ctx.BeginPath();
            ctx.MoveTo(p.X, p.Y);
            ctx.LineTo(fp.X, fp.Y);
            ctx.Stroke();
        }

        ctx.Restore();
    }

    public Dictionary<string, object?> CreateNotanPalette(string type = "classic3")
    {
        var pal = type.Trim().ToLowerInvariant();
        return pal switch
        {
            "binary" or "2value" => new Dictionary<string, object?>
            {
                ["type"] = "binary",
                ["dominant"] = "#ffffff",
                ["secondary"] = "#0a0a0c"
            },
            "highkey" => new Dictionary<string, object?>
            {
                ["type"] = "highKey",
                ["background"] = "#ffffff",
                ["formLight"] = "#e2e8f0",
                ["formMid"] = "#a0aec0",
                ["darkAccent"] = "#4a5568"
            },
            "lowkey" => new Dictionary<string, object?>
            {
                ["type"] = "lowKey",
                ["background"] = "#0c0f14",
                ["formDark"] = "#1a202c",
                ["formMid"] = "#3e4c5e",
                ["rimAccent"] = "#e2e8f0"
            },
            _ => new Dictionary<string, object?>
            {
                ["type"] = "classic3",
                ["dominantLight"] = "#f4f6f8",
                ["secondaryMid"] = "#828c9b",
                ["accentDark"] = "#181e28"
            }
        };
    }

    public Dictionary<string, object?> SubdivideProportions(object bounds, string direction = "horizontal", object? ratios = null)
    {
        var b = JsInterop.AsDict(bounds);
        var bx = b != null && b.Contains("x") ? Convert.ToSingle(b["x"], CultureInfo.InvariantCulture) : 0f;
        var by = b != null && b.Contains("y") ? Convert.ToSingle(b["y"], CultureInfo.InvariantCulture) : 0f;
        var bw = b != null && b.Contains("width") ? Convert.ToSingle(b["width"], CultureInfo.InvariantCulture) : 800f;
        var bh = b != null && b.Contains("height") ? Convert.ToSingle(b["height"], CultureInfo.InvariantCulture) : 600f;

        var isHoriz = direction.Trim().ToLowerInvariant() == "horizontal";

        // Big: 70%, Medium: 20%, Small: 10%
        if (isHoriz)
        {
            var wBig = bw * 0.70f;
            var wMed = bw * 0.20f;
            var wSmall = bw * 0.10f;

            return new Dictionary<string, object?>
            {
                ["big"] = new Dictionary<string, object?> { ["x"] = bx, ["y"] = by, ["width"] = wBig, ["height"] = bh },
                ["medium"] = new Dictionary<string, object?> { ["x"] = bx + wBig, ["y"] = by, ["width"] = wMed, ["height"] = bh },
                ["small"] = new Dictionary<string, object?> { ["x"] = bx + wBig + wMed, ["y"] = by, ["width"] = wSmall, ["height"] = bh }
            };
        }
        else
        {
            var hBig = bh * 0.70f;
            var hMed = bh * 0.20f;
            var hSmall = bh * 0.10f;

            return new Dictionary<string, object?>
            {
                ["big"] = new Dictionary<string, object?> { ["x"] = bx, ["y"] = by, ["width"] = bw, ["height"] = hBig },
                ["medium"] = new Dictionary<string, object?> { ["x"] = bx, ["y"] = by + hBig, ["width"] = bw, ["height"] = hMed },
                ["small"] = new Dictionary<string, object?> { ["x"] = bx, ["y"] = by + hBig + hMed, ["width"] = bw, ["height"] = hSmall }
            };
        }
    }
    #endregion
}
