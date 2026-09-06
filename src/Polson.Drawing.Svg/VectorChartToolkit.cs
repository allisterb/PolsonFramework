namespace Polson.Drawing.Svg;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

/// <summary>
/// Draws a chart model onto a Snap paper, as real SVG elements.
/// </summary>
/// <remarks>
/// <para>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>VectorChart.drawChart(...)</c> reaches
/// <see cref="DrawChart"/>. The camelCase form is the one documented in <c>docs/Polson.core.md</c>
/// and the studio manuals.
/// </para>
/// <para>
/// <b>The models were already surface-agnostic; only the drawing was not.</b> <c>ChartToolkit</c> is
/// 2,436 lines of which exactly one method takes a canvas — <c>drawChart</c>, whose body is
/// <c>foreach (var mark in marks) ctx.Fill(mark)</c>. Everything else is closed-form arithmetic
/// returning plain numbers, so a chart could always be *computed* for a vector deliverable and never
/// *drawn* into one without the caller writing the loop by hand. This is that loop, once.
/// </para>
/// <para>
/// It reads the model and nothing else — no reference to <c>ChartToolkit</c>, and no Skia. That is
/// what lets it live in the vector project without inverting the layering, and it means a model
/// built by any future producer draws here unchanged.
/// </para>
/// <para>
/// <b>Ticks and labels are deliberately not drawn</b>, exactly as the canvas version leaves them.
/// They are data — <c>chart.ticks</c> and <c>chart.labels</c> carry positions and text — and their
/// typography belongs to the caller. Declining to draw them is the erasing pass of
/// <c>polson://manual/13</c> §3 rather than an omission.
/// </para>
/// </remarks>
public class VectorChartToolkit
{
    #region Methods
    /// <summary>
    /// Appends a chart's marks to <paramref name="paper"/> as a single <c>&lt;g&gt;</c>, and returns it.
    /// </summary>
    /// <param name="paper">The paper or element to append to.</param>
    /// <param name="chartModel">Any model from <c>Chart.create*</c>.</param>
    /// <param name="options">
    /// <c>{ fill, stroke, strokeWidth, opacity, colors, radius }</c>. <c>colors</c> is an array
    /// indexed by mark, which is what nearly every real chart wants and what a single
    /// <c>fillStyle</c> cannot express.
    /// </param>
    /// <remarks>
    /// A group rather than loose elements so the whole chart can be transformed, styled or removed as
    /// one, and so a second chart on the same paper cannot be confused with the first.
    /// </remarks>
    public SnapGroup DrawChart(SnapElement paper, object chartModel, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(paper);

        var model = JsInterop.AsDict(chartModel)
            ?? throw new ArgumentException(
                "drawChart(paper, chartModel) needs a model from Chart.create… — got "
                + (chartModel?.GetType().Name ?? "null") + ".", nameof(chartModel));

        var opts = JsInterop.AsDict(options);
        var style = new Style(opts);
        var group = paper.G();
        group.Attr("class", "chart");

        var type = Str(model, "type") ?? string.Empty;
        switch (type)
        {
            case "column":
            case "bar": Bars(group, model, style); break;
            case "dot":
            case "groupedDot": Dots(group, model, style); break;
            case "framedRectangle": Framed(group, model, style); break;
            case "waffle": Waffle(group, model, style); break;
            case "pictogram": Pictogram(group, model, style); break;
            case "proportionalShapes": Shapes(group, model, style); break;
            // "progressMeter", not "meter" — the string the model actually carries. A probe drawing
            // every form caught this: the arc fell through to the slots fallback and emitted one
            // flat rectangle instead of a ring, which looks like a chart and is not one.
            case "progressMeter": Meter(group, model, style); break;
            case "timeline": Timeline(group, model, style); break;
            case "callout": Callout(group, model, style); break;
            case "smallMultiples": Panels(group, model, style); break;

            // An unknown or future form still has `slots`, which every model carries and which says
            // where each mark stands and how big it is. Drawing those beats refusing.
            default: Slots(group, model, style); break;
        }

        return group;
    }
    #endregion

    #region Private methods — one per form
    private static void Bars(SnapElement g, IDictionary model, Style style)
    {
        var i = 0;
        foreach (var bar in Rows(model, "bars"))
        {
            style.Fill(g.Rect(F(bar, "x"), F(bar, "y"), F(bar, "width"), F(bar, "height"),
                style.Radius, style.Radius), i++);
        }
    }

    private static void Dots(SnapElement g, IDictionary model, Style style)
    {
        var i = 0;
        foreach (var dot in Rows(model, "dots"))
        {
            // The leader is what ties a dot to its row across the whitespace; without it a grouped
            // chart reads as scattered points.
            if (Has(dot, "leaderX1"))
            {
                g.Line(F(dot, "leaderX1"), F(dot, "leaderY1"), F(dot, "leaderX2"), F(dot, "leaderY2"))
                 .Attr("stroke", style.Stroke ?? "#d8d8d8")
                 .Attr("stroke-width", style.StrokeWidth);
            }

            var r = F(dot, "radius");
            style.Fill(g.Circle(F(dot, "cx"), F(dot, "cy"), r > 0 ? r : style.Radius), i++);
        }
    }

    private static void Framed(SnapElement g, IDictionary model, Style style)
    {
        // The frame is the mechanism, not decoration: without identical reference boxes these are
        // "located bars" and the reader's judgment drops a rank. See polson://manual/13.
        var i = 0;
        foreach (var item in Rows(model, "items"))
        {
            if (JsInterop.AsDict(item["frame"]) is { } frame)
            {
                g.Rect(F(frame, "x"), F(frame, "y"), F(frame, "width"), F(frame, "height"))
                 .Attr("fill", "none")
                 .Attr("stroke", style.Stroke ?? "#9aa3ad")
                 .Attr("stroke-width", style.StrokeWidth);
            }

            if (JsInterop.AsDict(item["fill"]) is { } fill)
            {
                style.Fill(g.Rect(F(fill, "x"), F(fill, "y"), F(fill, "width"), F(fill, "height")), i);
            }

            i++;
        }
    }

    private static void Waffle(SnapElement g, IDictionary model, Style style)
    {
        foreach (var cell in Rows(model, "cells"))
        {
            var filled = B(cell, "filled");
            var rect = g.Rect(F(cell, "x"), F(cell, "y"), F(cell, "width"), F(cell, "height"),
                style.Radius, style.Radius);

            // An unfilled cell is the remainder of the grid and must still be drawn — a waffle is
            // countable only because the whole hundred is visible.
            if (filled) style.Fill(rect, I(cell, "partIndex"));
            else rect.Attr("fill", style.Empty);
        }
    }

    private static void Pictogram(SnapElement g, IDictionary model, Style style)
    {
        var i = 0;
        foreach (var icon in Rows(model, "icons"))
        {
            // A part-unit is shown by CLIPPING a full-size icon, never by drawing a smaller one:
            // scaling an icon changes its area and so changes the quantity the reader sees.
            var box = B(icon, "partial") && JsInterop.AsDict(icon["clip"]) is { } clip ? clip : icon;
            style.Fill(g.Rect(F(box, "x"), F(box, "y"), F(box, "width"), F(box, "height"),
                style.Radius, style.Radius), I(icon, "rowIndex", i));
            i++;
        }
    }

    private static void Shapes(SnapElement g, IDictionary model, Style style)
    {
        var square = string.Equals(Str(model, "shape"), "square", StringComparison.OrdinalIgnoreCase);
        var i = 0;
        foreach (var shape in Rows(model, "shapes"))
        {
            if (square)
            {
                var size = F(shape, "size");
                style.Fill(g.Rect(F(shape, "cx") - size / 2f, F(shape, "cy") - size / 2f, size, size), i++);
            }
            else
            {
                style.Fill(g.Circle(F(shape, "cx"), F(shape, "cy"), F(shape, "radius")), i++);
            }
        }
    }

    private static void Meter(SnapElement g, IDictionary model, Style style)
    {
        var track = JsInterop.AsDict(model["track"]);
        var fill = JsInterop.AsDict(model["fill"]);

        // An arc band reports startAngleDeg/endAngleDeg; a bar reports a rectangle. The presence of
        // the angles is what distinguishes them, and the model computes both units together.
        if (track is not null && Has(track, "startAngleDeg"))
        {
            g.Path(Annulus(track)).Attr("fill", style.Empty);
            if (fill is not null) style.Fill(g.Path(Annulus(fill)), 0);
            return;
        }

        if (track is not null)
        {
            g.Rect(F(track, "x"), F(track, "y"), F(track, "width"), F(track, "height"),
                style.Radius, style.Radius).Attr("fill", style.Empty);
        }

        if (fill is not null)
        {
            style.Fill(g.Rect(F(fill, "x"), F(fill, "y"), F(fill, "width"), F(fill, "height"),
                style.Radius, style.Radius), 0);
        }
    }

    private static void Timeline(SnapElement g, IDictionary model, Style style)
    {
        var i = 0;
        foreach (var ev in Rows(model, "events"))
        {
            if (Has(ev, "leaderX1"))
            {
                g.Line(F(ev, "leaderX1"), F(ev, "leaderY1"), F(ev, "leaderX2"), F(ev, "leaderY2"))
                 .Attr("stroke", style.Stroke ?? "#c8ced6")
                 .Attr("stroke-width", style.StrokeWidth);
            }

            // A period is a span; a milestone is a point. Both share the lane packing, so both are
            // drawn here rather than leaving the caller to tell them apart.
            if (JsInterop.AsDict(ev["span"]) is { } span)
            {
                style.Fill(g.Rect(F(span, "x"), F(span, "y"), F(span, "width"), F(span, "height"),
                    style.Radius, style.Radius), i);
            }
            else
            {
                var r = F(model, "markerRadius");
                style.Fill(g.Circle(F(ev, "axisX"), F(ev, "axisY"), r > 0 ? r : style.Radius), i);
            }

            i++;
        }
    }

    /// <summary>
    /// The one form whose marks are glyphs: a formatted numeral with an optional label and caption.
    /// </summary>
    /// <remarks>
    /// <b>The model carries no anchors, whatever the reference used to say.</b> It returns
    /// <c>plot</c>, <c>bounds</c>, <c>value</c>, <c>display</c>, <c>label</c>, <c>caption</c> and
    /// <c>align</c> — and <c>display</c> is simply the formatted string. The documented
    /// <c>valueX</c>/<c>valueY</c>/<c>valueSize</c> anchors never existed, so a caller following the
    /// reference got <c>undefined</c>, drew at <c>NaN</c>, and saw nothing. This was found by drawing
    /// every form and counting the elements each produced.
    /// <para>
    /// Positions and sizes here are therefore <i>this toolkit's</i> arithmetic, derived from the plot
    /// and <c>align</c>, not the model's. They are a reasonable default rather than an authority:
    /// restyle the returned <c>&lt;text&gt;</c> nodes, or place the strings yourself from
    /// <c>chart.display</c> and <c>chart.plot</c>.
    /// </para>
    /// </remarks>
    private static void Callout(SnapElement g, IDictionary model, Style style)
    {
        if (JsInterop.AsDict(model["plot"]) is not { } plot) return;

        var x = F(plot, "x");
        var y = F(plot, "y");
        var w = F(plot, "width");
        var h = F(plot, "height");

        var align = (Str(model, "align") ?? "left").ToLowerInvariant();
        var (anchor, tx) = align switch
        {
            "center" or "centre" => ("middle", x + w / 2f),
            "right" or "end" => ("end", x + w),
            _ => ("start", x),
        };

        var value = Str(model, "display");
        var label = Str(model, "label");
        var caption = Str(model, "caption");

        // A ladder off the plot height, so the numeral dominates at any size the caller gave it.
        var valueSize = MathF.Max(12f, h * 0.42f);
        var smallSize = MathF.Max(8f, valueSize * 0.24f);
        var cursor = y + (label is { Length: > 0 } ? smallSize * 1.6f : 0f) + valueSize;

        void Line(string? text, float size, float baseline, string fill)
        {
            if (string.IsNullOrEmpty(text)) return;
            g.Text(tx, baseline, text)
             .Attr("font-size", size)
             .Attr("text-anchor", anchor)
             .Attr("fill", fill);
        }

        Line(label, smallSize, y + smallSize, style.Stroke ?? "#6b7480");
        Line(value, valueSize, cursor, style.FillColor);
        Line(caption, smallSize, cursor + smallSize * 1.8f, style.Stroke ?? "#6b7480");
    }

    private static void Panels(SnapElement g, IDictionary model, Style style)
    {
        // Every panel carries a complete model of its own form, which is what makes a shared scale
        // enforceable rather than merely advised.
        foreach (var panel in Rows(model, "panels"))
        {
            if (JsInterop.AsDict(panel["chart"]) is { } inner)
            {
                new VectorChartToolkit().DrawChart(g, inner, style.Source);
            }
        }
    }

    private static void Slots(SnapElement g, IDictionary model, Style style)
    {
        var i = 0;
        foreach (var slot in Rows(model, "slots"))
        {
            style.Fill(g.Rect(F(slot, "x"), F(slot, "y"), F(slot, "width"), F(slot, "height"),
                style.Radius, style.Radius), i++);
        }
    }
    #endregion

    #region Private methods — geometry and model reading
    /// <summary>An annulus segment as an SVG path, for a ring or gauge meter.</summary>
    /// <remarks>
    /// <para>
    /// Built from the band's own <c>startAngleDeg</c>/<c>endAngleDeg</c> and its inner and outer
    /// radii, so a meter's track and its fill share one construction and cannot disagree.
    /// </para>
    /// <para>
    /// <b>Walked in segments of at most a half-turn, which is not a style choice.</b> A meter's track
    /// sweeps the full 360° — <c>-90°</c> to <c>270°</c> — and an SVG elliptical arc whose start and
    /// end points coincide is <i>dropped entirely</i> by the renderer, not drawn as a circle. Emitted
    /// as one arc the track silently vanished and only the coloured fill appeared, which reads as a
    /// deliberate design rather than as a missing element. Splitting at half-turns also makes the
    /// large-arc flag always <c>0</c>, removing the other thing hand-rolled arcs get wrong.
    /// </para>
    /// </remarks>
    private static string Annulus(IDictionary band)
    {
        var cx = F(band, "cx");
        var cy = F(band, "cy");
        var radius = F(band, "radius");
        var thickness = F(band, "thickness");
        var outer = radius + thickness / 2f;
        var inner = MathF.Max(0f, radius - thickness / 2f);

        var a0 = F(band, "startAngleDeg") * MathF.PI / 180f;
        var a1 = F(band, "endAngleDeg") * MathF.PI / 180f;
        if (MathF.Abs(a1 - a0) < 1e-4f) return string.Empty;

        var sb = new StringBuilder();
        sb.Append('M').Append(N(cx + outer * MathF.Cos(a0))).Append(' ').Append(N(cy + outer * MathF.Sin(a0)));
        Sweep(sb, cx, cy, outer, a0, a1);
        sb.Append('L').Append(N(cx + inner * MathF.Cos(a1))).Append(' ').Append(N(cy + inner * MathF.Sin(a1)));
        Sweep(sb, cx, cy, inner, a1, a0);
        sb.Append('Z');
        return sb.ToString();
    }

    /// <summary>Appends arc commands from <paramref name="from"/> to <paramref name="to"/>, never exceeding a half-turn each.</summary>
    private static void Sweep(StringBuilder sb, float cx, float cy, float r, float from, float to)
    {
        var total = to - from;
        var steps = Math.Max(1, (int)MathF.Ceiling(MathF.Abs(total) / (MathF.PI * 0.999f)));
        var step = total / steps;
        var direction = total >= 0 ? 1 : 0;

        for (var i = 1; i <= steps; i++)
        {
            var angle = from + step * i;
            sb.Append('A').Append(N(r)).Append(' ').Append(N(r)).Append(" 0 0 ").Append(direction).Append(' ')
              .Append(N(cx + r * MathF.Cos(angle))).Append(' ').Append(N(cy + r * MathF.Sin(angle)));
        }
    }

    private static string N(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>The rows of a named collection, or nothing when the model has no such field.</summary>
    private static IEnumerable<IDictionary> Rows(IDictionary model, string key)
    {
        if (!model.Contains(key) || model[key] is not IEnumerable list || model[key] is string) yield break;

        foreach (var row in list)
        {
            if (JsInterop.AsDict(row) is { } dict) yield return dict;
        }
    }

    private static bool Has(IDictionary d, string key) => d.Contains(key) && d[key] is not null;

    private static float F(IDictionary d, string key)
    {
        if (!d.Contains(key) || d[key] is not { } v) return 0f;
        try { return Convert.ToSingle(v, CultureInfo.InvariantCulture); }
        catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException) { return 0f; }
    }

    private static int I(IDictionary d, string key, int fallback = 0)
    {
        if (!d.Contains(key) || d[key] is not { } v) return fallback;
        try { return Convert.ToInt32(v, CultureInfo.InvariantCulture); }
        catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException) { return fallback; }
    }

    private static bool B(IDictionary d, string key) =>
        d.Contains(key) && d[key] is { } v && v is bool b && b;

    private static string? Str(IDictionary d, string key) =>
        d.Contains(key) && d[key] is { } v ? v.ToString() : null;
    #endregion

    #region Child types
    /// <summary>How the marks are painted. Resolved once so every form reads it the same way.</summary>
    private sealed class Style
    {
        internal Style(IDictionary? options)
        {
            Source = options;
            FillColor = Str(options, "fill") ?? "#1f6f8b";
            Stroke = Str(options, "stroke");
            Empty = Str(options, "empty") ?? "#e6e8ec";
            StrokeWidth = options is not null && options.Contains("strokeWidth") ? F(options, "strokeWidth") : 1f;
            Opacity = options is not null && options.Contains("opacity") ? F(options, "opacity") : 1f;
            Radius = options is not null && options.Contains("radius") ? F(options, "radius") : 0f;

            if (options?["colors"] is IEnumerable colors and not string)
            {
                Colors = [.. colors.Cast<object?>().Select(c => c?.ToString()).Where(c => !string.IsNullOrEmpty(c))!];
            }
        }

        internal IDictionary? Source { get; }
        internal string FillColor { get; }
        internal string? Stroke { get; }
        internal string Empty { get; }
        internal float StrokeWidth { get; }
        internal float Opacity { get; }
        internal float Radius { get; }
        internal string[] Colors { get; } = [];

        /// <summary>Paints one mark, taking its colour from <c>colors[index]</c> where one was given.</summary>
        internal SnapElement Fill(SnapElement element, int index)
        {
            var colour = Colors.Length > 0 ? Colors[((index % Colors.Length) + Colors.Length) % Colors.Length] : FillColor;
            element.Attr("fill", colour);
            if (Opacity < 1f) element.Attr("opacity", Opacity);
            if (Stroke is not null)
            {
                element.Attr("stroke", Stroke);
                element.Attr("stroke-width", StrokeWidth);
            }
            return element;
        }

        private static string? Str(IDictionary? d, string key) =>
            d is not null && d.Contains(key) && d[key] is { } v ? v.ToString() : null;

        private static float F(IDictionary d, string key)
        {
            try { return Convert.ToSingle(d[key], CultureInfo.InvariantCulture); }
            catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException or ArgumentNullException) { return 0f; }
        }
    }
    #endregion
}
