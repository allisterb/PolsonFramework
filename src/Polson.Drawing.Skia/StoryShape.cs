// SPDX-License-Identifier: MIT
namespace Polson.Drawing.Skia;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The shape of a story over a film's time (Shaw, <i>Design for Motion</i>, ch. 8): each beat's intensity as a point, joined
/// by a smooth curve, its peak marked. Shaw shows students the graph editor's velocity curve to make the point that a line
/// that never changes is dull; this is that graph, drawn for the story rather than for a move.
/// </summary>
public static class StoryShape
{
    #region Methods
    /// <summary>Draws the curve into the box: time across, intensity up, each beat named under its point.</summary>
    public static void Draw(CanvasRenderingContext2D ctx, IReadOnlyList<(string Name, double Time, double Intensity)> beats,
        double duration, float bx, float by, float bw, float bh, string family = "Georgia")
    {
        ArgumentNullException.ThrowIfNull(ctx);
        float left = bx + 70, right = bx + bw - 30, top = by + 30, bottom = by + bh - 70;
        float X(double t) => left + (float)(t / Math.Max(1e-9, duration)) * (right - left);
        float Y(double v) => bottom - (float)v * (bottom - top);
        var peak = beats.MaxBy(b => b.Intensity);

        ctx.Save();
        // The frame: calm at the foot, intense at the head, the film's seconds along the bottom.
        ctx.StrokeStyle = "#cfcabe"; ctx.LineWidth = 1;
        foreach (var v in new[] { 0.0, 0.5, 1.0 }) { ctx.BeginPath(); ctx.MoveTo(left, Y(v)); ctx.LineTo(right, Y(v)); ctx.Stroke(); }
        ctx.FillStyle = "#6b675e"; ctx.Font = $"italic 12px {family}"; ctx.TextBaseline = "middle"; ctx.TextAlign = "right";
        ctx.FillText("intense", left - 10, Y(1)); ctx.FillText("calm", left - 10, Y(0));
        ctx.TextAlign = "left"; ctx.FillText("0 s", left, bottom + 54);
        ctx.TextAlign = "right"; ctx.FillText($"{duration:0.#} s", right, bottom + 54);

        // The curve: monotone cubic (Fritsch–Carlson) through the beats. A Catmull–Rom spline overshot between two rising
        // beats and drew a climax higher than any beat had; this one never leaves the range of the points it joins.
        var points = beats.Select(b => (X: X(b.Time), Y: Y(b.Intensity))).ToArray();
        var slopes = Slopes(points);
        ctx.StrokeStyle = "#c8553d"; ctx.LineWidth = 3.5f;
        ctx.BeginPath(); ctx.MoveTo(points[0].X, points[0].Y);
        for (var i = 0; i < points.Length - 1; i++)
        {
            var dx = (points[i + 1].X - points[i].X) / 3;
            ctx.BezierCurveTo(points[i].X + dx, points[i].Y + slopes[i] * dx, points[i + 1].X - dx, points[i + 1].Y - slopes[i + 1] * dx,
                points[i + 1].X, points[i + 1].Y);
        }
        ctx.Stroke();

        // Each beat: its point, its name under the axis, the peak named and ringed.
        for (var i = 0; i < beats.Count; i++)
        {
            var (x, y) = points[i];
            var isPeak = beats[i] == peak;
            ctx.BeginPath(); ctx.Arc(x, y, isPeak ? 7 : 5, 0, MathF.Tau);
            ctx.FillStyle = isPeak ? "#c8553d" : "#15151a"; ctx.Fill();
            ctx.StrokeStyle = "#cfcabe"; ctx.LineWidth = 1; ctx.SetLineDash(new object[] { 3, 3 });
            ctx.BeginPath(); ctx.MoveTo(x, y + 8); ctx.LineTo(x, bottom + 6); ctx.Stroke(); ctx.SetLineDash(Array.Empty<object>());
            // Names alternate between two rows, so neighbouring beats never print over each other.
            ctx.FillStyle = "#15151a"; ctx.Font = $"{(isPeak ? "bold " : "")}13px {family}"; ctx.TextAlign = "center";
            ctx.FillText(beats[i].Name, x, bottom + (i % 2 == 0 ? 18 : 36));
        }
        ctx.Restore();
    }
    #endregion

    #region Private methods
    /// <summary>Fritsch–Carlson tangents: zero where the curve turns, and limited so no segment overshoots its ends.</summary>
    private static float[] Slopes((float X, float Y)[] p)
    {
        var n = p.Length;
        var delta = new float[n - 1];
        for (var k = 0; k < n - 1; k++) delta[k] = (p[k + 1].Y - p[k].Y) / MathF.Max(1e-3f, p[k + 1].X - p[k].X);
        var m = new float[n];
        m[0] = delta[0];
        m[n - 1] = delta[n - 2];
        for (var k = 1; k < n - 1; k++) m[k] = delta[k - 1] * delta[k] <= 0 ? 0 : (delta[k - 1] + delta[k]) / 2;
        for (var k = 0; k < n - 1; k++)
        {
            if (delta[k] == 0) { m[k] = m[k + 1] = 0; continue; }
            float a = m[k] / delta[k], b = m[k + 1] / delta[k], s = a * a + b * b;
            if (s > 9) { var t = 3 / MathF.Sqrt(s); m[k] = t * a * delta[k]; m[k + 1] = t * b * delta[k]; }
        }
        return m;
    }
    #endregion
}
