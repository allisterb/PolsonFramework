namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>
/// Whole charts as constructions, in the shape the rest of the toolkit hands work back.
/// </summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// <para>
/// <b>Why this exists.</b> <see cref="ScaleToolkit"/> maps values to pixels and <see cref="LayoutToolkit"/>
/// divides a page, and between them a caller still had to write the loop — roughly sixty lines to draw
/// two column series, which is what Manual 13 documented as the way to do it. The drawing side has had
/// <c>createMannequinFigure</c> since the beginning; the charting side had nothing above the
/// primitives, so every chart was rebuilt from arithmetic by whoever needed one.
/// </para>
/// <para>
/// <b>The model/geometry split is deliberate and copied from the figure toolkit.</b>
/// <see cref="CreateColumnChart"/> is closed-form arithmetic — no paths, safe in a loop that only
/// measures — exactly as <c>createMannequinFigure</c> is. <see cref="CreateChartGeometry"/> is the one
/// that allocates, building a <see cref="CanvasPath"/> per mark, exactly as <c>createFigureGeometry</c>
/// does. A small-multiples grid measuring twenty-four panels should not build several hundred native
/// paths to find out how tall they are.
/// </para>
/// <para>
/// <b>The model carries its own integrity.</b> <c>lieFactor</c> and <c>isZeroBased</c> are computed
/// rather than left for the caller to remember, and <c>encoding</c> records which of Cleveland and
/// McGill's perceptual ranks the chart spends. See <c>polson://manual/13</c> §1a and §2a.
/// </para>
/// </remarks>
public class ChartToolkit
{
    #region Fields
    /// <summary>
    /// Accepted option names. An unrecognised one is refused rather than ignored.
    /// </summary>
    /// <remarks>
    /// Options arrive as a dictionary, which means — unlike a typed options class — the misspelling
    /// is visible here and can be reported. <c>{ padding: 0.3 }</c> written as <c>{ pading: 0.3 }</c>
    /// would otherwise bind to nothing and silently draw the default, which is the class of failure
    /// this project treats as worst.
    /// </remarks>
    private static readonly string[] KnownOptions =
        ["baseline", "max", "min", "padding", "tickCount", "labels", "labelGap", "tickGap"];
    #endregion

    #region Methods
    /// <summary>
    /// A column chart: categories across, values up. Returns the model, draws nothing.
    /// </summary>
    /// <param name="rect">The plot area, as any <c>{ x, y, width, height }</c> — a <c>Layout</c> rectangle fits.</param>
    /// <param name="data">Numbers, or objects carrying <c>value</c> and optionally <c>label</c>.</param>
    /// <param name="options">
    /// <c>baseline</c> (default 0), <c>max</c> / <c>min</c> to force a shared scale across panels,
    /// <c>padding</c> (0–1, default 0.3), <c>tickCount</c> (default 5), <c>labels</c>, and
    /// <c>labelGap</c> / <c>tickGap</c> for label offsets.
    /// </param>
    public Dictionary<string, object?> CreateColumnChart(object rect, object data, object? options = null) =>
        Build(rect, data, options, horizontal: false);

    /// <summary>
    /// A bar chart: categories down, values across. Returns the model, draws nothing.
    /// </summary>
    /// <remarks>
    /// The same construction turned on its side, and usually the better of the two — a horizontal bar
    /// gives category labels room to be words rather than abbreviations, and sorting by value is
    /// natural rather than a rearrangement.
    /// </remarks>
    public Dictionary<string, object?> CreateBarChart(object rect, object data, object? options = null) =>
        Build(rect, data, options, horizontal: true);

    /// <summary>
    /// The marks of a chart model as geometry: one path per bar, plus the whole set unioned.
    /// </summary>
    /// <remarks>
    /// Separate from the model for the reason <c>createFigureGeometry</c> is separate from
    /// <c>createMannequinFigure</c>: this allocates native paths and that does not. Reach for it when
    /// the marks are going to be clipped into, subtracted from, filled with a shader, unioned for a
    /// shadow, or handed to a timeline — and skip it when you only need numbers.
    /// </remarks>
    public Dictionary<string, object?> CreateChartGeometry(object chartModel)
    {
        var model = JsInterop.AsDict(chartModel)
            ?? throw new ArgumentException(
                "createChartGeometry(...) takes a chart model from Chart.createColumnChart(...) or "
                + "Chart.createBarChart(...).", nameof(chartModel));

        if (model["bars"] is not IEnumerable rows)
        {
            throw new ArgumentException(
                "That does not look like a chart model — it has no 'bars'.", nameof(chartModel));
        }

        var marks = new List<CanvasPath>();
        CanvasPath? silhouette = null;

        foreach (var row in rows)
        {
            var bar = JsInterop.AsDict(row);
            if (bar is null) continue;

            var path = new CanvasPath();
            path.Rect((float)Num(bar, "x"), (float)Num(bar, "y"),
                (float)Num(bar, "width"), (float)Num(bar, "height"));
            marks.Add(path);
            silhouette = silhouette is null ? path : silhouette.Union(path);
        }

        return new Dictionary<string, object?>
        {
            ["marks"] = marks.ToArray(),
            ["silhouette"] = silhouette ?? new CanvasPath(),
            ["bounds"] = model["plot"]
        };
    }

    /// <summary>
    /// Draws a chart model's bars and returns their geometry.
    /// </summary>
    /// <remarks>
    /// Deliberately draws the <b>marks only</b> — no axis, no ticks, no labels. Those are in the model
    /// as data precisely so the caller owns their typography and can decline to draw them at all,
    /// which is the erasing pass of <c>polson://manual/13</c> §3 expressed as a choice rather than an
    /// edit. Fill colour comes from the context's own <c>fillStyle</c>.
    /// </remarks>
    public Dictionary<string, object?> DrawChart(CanvasRenderingContext2D ctx, object chartModel)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var geometry = CreateChartGeometry(chartModel);
        foreach (var mark in (CanvasPath[])geometry["marks"]!) ctx.Fill(mark);
        return geometry;
    }
    #endregion

    #region Methods (private)
    static Dictionary<string, object?> Build(object rect, object data, object? options, bool horizontal)
    {
        var plot = JsInterop.AsDict(rect)
            ?? throw new ArgumentException(
                "A chart needs a { x, y, width, height } plot area; a Layout rectangle fits.", nameof(rect));

        var opt = JsInterop.AsDict(options);
        RefuseUnknownOptions(opt);

        var (values, labels) = ReadData(data, opt);
        if (values.Length == 0)
        {
            throw new ArgumentException("A chart needs at least one value.", nameof(data));
        }

        var px = Num(plot, "x");
        var py = Num(plot, "y");
        var pw = Num(plot, "width");
        var ph = Num(plot, "height");

        var baseline = Opt(opt, "baseline", 0d);
        var padding = Math.Clamp(Opt(opt, "padding", 0.3d), 0d, 0.95d);
        var tickCount = (int)Opt(opt, "tickCount", 5d);
        var labelGap = Opt(opt, "labelGap", 8d);
        var tickGap = Opt(opt, "tickGap", 8d);

        // A caller-supplied max is how small multiples share one scale (Manual 13 §2). Without it the
        // extent comes from this panel's own data, which is exactly the per-panel scale that rule bans.
        var dataMax = values.Max();
        var dataMin = values.Min();
        var scaleTk = new ScaleToolkit();
        var niced = scaleTk.Nice(Math.Min(baseline, dataMin), dataMax, tickCount);

        var max = Opt(opt, "max", Convert.ToDouble(niced["max"], CultureInfo.InvariantCulture));
        var min = Opt(opt, "min", Math.Min(baseline, Convert.ToDouble(niced["min"], CultureInfo.InvariantCulture)));

        // The value axis runs against the pixel axis for a column chart, because larger values belong
        // higher up the canvas and y grows downward.
        var scale = horizontal
            ? scaleTk.Linear(min, max, px, px + pw)
            : scaleTk.Linear(min, max, py + ph, py);

        var band = horizontal
            ? scaleTk.Band(values.Length, py, py + ph, padding)
            : scaleTk.Band(values.Length, px, px + pw, padding);

        var zero = scale.Map(baseline);
        var bars = new List<Dictionary<string, object>>(values.Length);
        var labelList = new List<Dictionary<string, object>>(values.Length);

        for (var i = 0; i < values.Length; i++)
        {
            var at = band.Map(i);
            var thickness = band.Bandwidth;
            var tip = scale.Map(values[i]);
            var length = Math.Abs(tip - zero);

            // A value below the baseline draws on the other side of it rather than inverting the
            // rectangle, which would give a negative extent and silently break every comparison.
            var bar = horizontal
                ? Rect(Math.Min(zero, tip), at, length, thickness)
                : Rect(at, Math.Min(zero, tip), thickness, length);

            bar["index"] = i;
            bar["value"] = values[i];
            bar["label"] = labels[i];
            bars.Add(bar);

            labelList.Add(new Dictionary<string, object>
            {
                ["text"] = labels[i],
                ["x"] = horizontal ? px - labelGap : band.Center(i),
                ["y"] = horizontal ? band.Center(i) : py + ph + labelGap,
                ["align"] = horizontal ? "right" : "center",
                ["baseline"] = horizontal ? "middle" : "top",
                ["index"] = i
            });
        }

        var ticks = scale.Ticks(tickCount).Select(t => new Dictionary<string, object>
        {
            ["value"] = t,
            ["position"] = scale.Map(t),
            ["label"] = Format(t),
            ["x"] = horizontal ? scale.Map(t) : px - tickGap,
            ["y"] = horizontal ? py + ph + tickGap : scale.Map(t)
        }).ToArray();

        return new Dictionary<string, object?>
        {
            ["type"] = horizontal ? "bar" : "column",
            ["plot"] = Rect(px, py, pw, ph),
            ["bounds"] = Rect(px, py, pw, ph),
            ["scale"] = scale,
            ["band"] = band,
            ["bars"] = bars.ToArray(),
            ["ticks"] = ticks,
            ["labels"] = labelList.ToArray(),
            ["baseline"] = baseline,
            ["baselinePosition"] = zero,
            ["min"] = min,
            ["max"] = max,

            // Carried rather than left to the caller: which perceptual rank this chart spends, and
            // whether the ink is proportional to the numbers. Manual 13 §1a and §2a.
            ["encoding"] = "length",
            ["encodingRank"] = 3,
            ["isZeroBased"] = scale.IsZeroBased,
            ["lieFactor"] = LieFactor(baseline, values)
        };
    }

    /// <summary>
    /// Tufte's lie factor: the size of the effect shown divided by the size of the effect in the data.
    /// </summary>
    /// <remarks>
    /// For a length encoding on a linear scale this depends only on the baseline, so it comes out at
    /// exactly 1 whenever the baseline is zero — which is the point. It is not a sixth rule to
    /// remember; it is the number that says whether the other rules survived into the picture.
    /// <para>
    /// Undefined without two distinct positive values to compare, and reported as 1 in that case
    /// rather than as a failure: a single bar cannot overstate a difference it does not draw. A
    /// baseline at or above the smallest value clips that bar away entirely, which is distortion
    /// without limit, and is reported as infinity rather than as some large finite number that might
    /// be mistaken for a measurement.
    /// </para>
    /// </remarks>
    static double LieFactor(double baseline, double[] values)
    {
        var positive = values.Where(v => v > 0d).ToArray();
        if (positive.Length < 2) return 1d;

        var hi = positive.Max();
        var lo = positive.Min();
        if (Math.Abs(hi - lo) < double.Epsilon) return 1d;
        if (baseline >= lo) return double.PositiveInfinity;

        var shown = (hi - baseline) / (lo - baseline);
        var actual = hi / lo;
        return shown / actual;
    }

    static void RefuseUnknownOptions(IDictionary? opt)
    {
        if (opt is null) return;

        var unknown = new List<string>();
        foreach (DictionaryEntry entry in opt)
        {
            var name = entry.Key?.ToString();
            if (name is null) continue;
            if (!KnownOptions.Contains(name, StringComparer.OrdinalIgnoreCase)) unknown.Add(name);
        }

        if (unknown.Count > 0)
        {
            throw new ArgumentException(
                $"Chart option{(unknown.Count > 1 ? "s" : string.Empty)} not recognised: "
                + $"{string.Join(", ", unknown)}. Accepted: {string.Join(", ", KnownOptions)}.", nameof(opt));
        }
    }

    /// <summary>Values and their labels, from numbers or from objects carrying <c>value</c>.</summary>
    static (double[] Values, string[] Labels) ReadData(object data, IDictionary? opt)
    {
        var values = new List<double>();
        var labels = new List<string>();

        if (data is IEnumerable items and not string)
        {
            var i = 0;
            foreach (var item in items)
            {
                var row = JsInterop.AsDict(item);
                if (row is not null && row.Contains("value"))
                {
                    values.Add(Convert.ToDouble(row["value"], CultureInfo.InvariantCulture));
                    labels.Add(row["label"]?.ToString() ?? (i + 1).ToString(CultureInfo.InvariantCulture));
                }
                else if (item is not null)
                {
                    values.Add(Convert.ToDouble(item, CultureInfo.InvariantCulture));
                    labels.Add((i + 1).ToString(CultureInfo.InvariantCulture));
                }
                i++;
            }
        }

        // An explicit labels option overrides whatever the data carried, position by position.
        if (opt is not null && opt["labels"] is IEnumerable given and not string)
        {
            var i = 0;
            foreach (var label in given)
            {
                if (i < labels.Count) labels[i] = label?.ToString() ?? labels[i];
                i++;
            }
        }

        return ([.. values], [.. labels]);
    }

    static Dictionary<string, object> Rect(double x, double y, double width, double height) =>
        new()
        {
            ["x"] = x,
            ["y"] = y,
            ["width"] = width,
            ["height"] = height,
            ["x2"] = x + width,
            ["y2"] = y + height,
            ["cx"] = x + width / 2d,
            ["cy"] = y + height / 2d
        };

    static double Num(IDictionary dict, string key) =>
        dict.Contains(key) ? Convert.ToDouble(dict[key], CultureInfo.InvariantCulture) : 0d;

    static double Opt(IDictionary? opt, string key, double fallback) =>
        opt is not null && opt.Contains(key) && opt[key] is not null
            ? Convert.ToDouble(opt[key], CultureInfo.InvariantCulture)
            : fallback;

    /// <summary>A tick label that reads as the number it is, without trailing zeros.</summary>
    static string Format(double value) =>
        value == Math.Floor(value) && Math.Abs(value) < 1e15
            ? value.ToString("0", CultureInfo.InvariantCulture)
            : value.ToString("0.###", CultureInfo.InvariantCulture);
    #endregion
}
