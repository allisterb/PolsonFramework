// SPDX-License-Identifier: MIT
namespace Polson.Drawing.Skia;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>One axis of a brand matrix: its two poles, where its values run, and what kind of evidence placed them.</summary>
/// <param name="Low">The pole at the left or bottom: <c>dark</c>.</param>
/// <param name="High">The pole at the right or top: <c>light</c>.</param>
/// <param name="Kind">Said under the axis: <c>measured · lightness</c>, <c>Qloo · popularity</c>, <c>judged by the studio</c>.</param>
public sealed record BrandMatrixAxis(string Low, string High, string Kind, double Min, double Max);

/// <summary>A point on a brand matrix: one of the field, numbered as on the research board, or a concept, lettered.</summary>
public sealed record BrandMatrixPoint(string Label, double X, double Y, string Colour, bool Concept, bool Chosen = false);

/// <summary>
/// Lupton's brand matrix (<i>Graphic Design Thinking</i>, pp. 42–45, after Debbie Millman and Sterling Brands): two
/// polarities crossed, the field plotted on them, and the empty space and the sweet spot read off. Drawn here; what
/// the axes measure is decided by <c>Stage.matrix</c>.
/// </summary>
public static class BrandMatrix
{
    #region Methods
    /// <summary>Draws the matrix into the box: the crossed axes, their poles and kinds, the field as numbered dots and the concepts as lettered squares.</summary>
    public static void Draw(CanvasRenderingContext2D ctx, BrandMatrixAxis x, BrandMatrixAxis y, IReadOnlyList<BrandMatrixPoint> points,
        float bx, float by, float bw, float bh, string family = "Georgia")
    {
        ArgumentNullException.ThrowIfNull(ctx);
        // Each axis's poles sit at its own ends — across beside the frame, up above and below it — so no pole is read
        // as the middle of the other axis (the first draft set "dark … muted … light" along one baseline).
        ctx.Save();
        ctx.Font = $"bold 15px {family}";
        var side = MathF.Max(Width(ctx, x.Low), Width(ctx, x.High)) + 14;
        ctx.Restore();
        float left = bx + side, right = bx + bw - side, top = by + 30, bottom = by + bh - 56;
        float cx = (left + right) / 2, cy = (top + bottom) / 2;
        float Px(double v) => left + (float)((v - x.Min) / (x.Max - x.Min)) * (right - left);
        float Py(double v) => bottom - (float)((v - y.Min) / (y.Max - y.Min)) * (bottom - top);

        ctx.Save();
        // The quadrants, faintly, so the empty one reads as space rather than as nothing drawn.
        ctx.FillStyle = "#ffffff";
        ctx.FillRect(left, top, right - left, bottom - top);
        ctx.StrokeStyle = "#cfcabe"; ctx.LineWidth = 1;
        ctx.StrokeRect(left, top, right - left, bottom - top);
        ctx.StrokeStyle = "#15151a"; ctx.LineWidth = 1.5f;
        ctx.BeginPath(); ctx.MoveTo(left, cy); ctx.LineTo(right, cy); ctx.MoveTo(cx, top); ctx.LineTo(cx, bottom); ctx.Stroke();

        // The quadrants named faintly in their outer corners, so an empty one reads as a place with a name.
        ctx.FillStyle = "#b3ada1"; ctx.Font = $"italic 13px {family}"; ctx.TextBaseline = "middle";
        ctx.TextAlign = "left"; ctx.FillText($"{y.High} & {x.Low}", left + 10, top + 14); ctx.FillText($"{y.Low} & {x.Low}", left + 10, bottom - 14);
        ctx.TextAlign = "right"; ctx.FillText($"{y.High} & {x.High}", right - 10, top + 14); ctx.FillText($"{y.Low} & {x.High}", right - 10, bottom - 14);

        // The poles at the ends of their own axes, in bold.
        ctx.FillStyle = "#15151a"; ctx.Font = $"bold 15px {family}";
        ctx.TextAlign = "right"; ctx.FillText(x.Low, left - 8, cy);
        ctx.TextAlign = "left"; ctx.FillText(x.High, right + 8, cy);
        ctx.TextAlign = "center"; ctx.FillText(y.High, cx, top - 14); ctx.FillText(y.Low, cx, bottom + 14);

        // The key: what the marks are, and what kind of evidence placed them on each axis.
        ctx.FillStyle = "#6b675e"; ctx.Font = $"12px {family}"; ctx.TextAlign = "left";
        ctx.FillText($"● the field, numbered as on the research board   ■ a concept, ringed when chosen   " +
                     $"across: {x.Kind}   up: {y.Kind}", left, bottom + 40);

        // The field first, then the concepts over it. A mark that would land on one already placed steps aside in a
        // widening ring, so every number and letter stays readable; where it was measured is never more than a few
        // marks away, and the returned values are the measured ones.
        var placed = new List<(float X, float Y, float R)>();
        foreach (var p in points.OrderBy(p => p.Concept))
        {
            var r = p.Concept ? 17f : 12f;
            float px = Math.Clamp(Px(p.X), left + r, right - r), py = Math.Clamp(Py(p.Y), top + r, bottom - r), ox = px, oy = py;
            // The axes are obstacles too: a mark sitting on one hides the line, and the layout check reads the line as
            // running through its number (poster5's Tokyo field, mark 9). Clearing it moves a mark at most its own width.
            bool Blocked() => placed.Any(q => MathF.Sqrt((q.X - px) * (q.X - px) + (q.Y - py) * (q.Y - py)) < q.R + r + 2)
                || MathF.Abs(px - cx) < r + 2 || MathF.Abs(py - cy) < r + 2;
            for (var step = 0; step < 60 && Blocked(); step++)
            {
                var a = step * 2.4f;                       // the golden angle, so the search spreads rather than circles
                var d = 6 + step * 2.2f;
                px = Math.Clamp(ox + d * MathF.Cos(a), left + r, right - r);
                py = Math.Clamp(oy + d * MathF.Sin(a), top + r, bottom - r);
            }
            placed.Add((px, py, r));
            Mark(ctx, p, px, py, r, family);
        }
        ctx.Restore();
    }

    /// <summary>Whether white type reads on a colour: its relative luminance under about 0.45.</summary>
    public static bool Dark(string hex)
    {
        var h = hex.TrimStart('#');
        if (h.Length != 6 || !int.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb)) return true;
        static double Lin(int c) => c / 255d is var v && v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        return 0.2126 * Lin(rgb >> 16 & 255) + 0.7152 * Lin(rgb >> 8 & 255) + 0.0722 * Lin(rgb & 255) < 0.45;
    }
    #endregion

    #region Private methods
    /// <summary>The width of a run of type, measured without counting as one of the script's own measurements.</summary>
    private static float Width(CanvasRenderingContext2D ctx, string text)
    {
        using var quiet = ProbeScope.Begin();
        return Convert.ToSingle(ctx.MeasureText(text)["width"], CultureInfo.InvariantCulture);
    }

    private static void Mark(CanvasRenderingContext2D ctx, BrandMatrixPoint p, float x, float y, float r, string family)
    {
        ctx.BeginPath();
        if (p.Concept)
        {
            ctx.RoundRect(x - r, y - r, 2 * r, 2 * r, 4);
            ctx.FillStyle = "#15151a"; ctx.Fill();
            if (p.Chosen) { ctx.StrokeStyle = "#c8553d"; ctx.LineWidth = 3; ctx.Stroke(); }
            ctx.FillStyle = "#ffffff"; ctx.Font = $"bold 16px {family}";
        }
        else
        {
            ctx.Arc(x, y, r, 0, MathF.Tau);
            ctx.FillStyle = p.Colour; ctx.Fill();
            ctx.StrokeStyle = "#ffffff"; ctx.LineWidth = 1.5f; ctx.Stroke();
            ctx.FillStyle = Dark(p.Colour) ? "#ffffff" : "#15151a"; ctx.Font = $"bold 11px {family}";
        }
        ctx.TextAlign = "center"; ctx.TextBaseline = "middle";
        ctx.FillText(p.Label, x, y + 0.5f);
    }
    #endregion
}
