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
        ["baseline", "max", "min", "padding", "tickCount", "labels", "labelGap", "tickGap", "radius", "sort"];
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
    /// A dot chart: value encoded as <b>position along one shared axis</b>, categories down.
    /// </summary>
    /// <remarks>
    /// <b>The form Cleveland and McGill recommend in place of a bar chart</b>, and the reason is their
    /// measured ranking: a dot read against a common scale is the most accurately decoded judgment
    /// there is (rank 1), where a bar's length is rank 3. Their own conclusion was that bar charts,
    /// divided bar charts, pie charts and shaded maps need *"radical surgery"*, with the dot chart as
    /// the first replacement offered. See <c>polson://manual/13</c> §1a.
    /// <para>
    /// <b>Its axis does not have to start at zero, and that is not a licence — it is a consequence.</b>
    /// A bar claims a <i>ratio</i>, because its length is the quantity, so cropping the axis makes the
    /// ink assert something false. A dot claims a <i>difference</i>, because only its position carries
    /// meaning, and under any linear mapping the ratio of pixel distances equals the ratio of value
    /// differences wherever the axis begins. So <c>lieFactor</c> is 1 here by the nature of the
    /// encoding rather than by the baseline, and cropping to the data's own range is the ordinary
    /// thing to do — it is what lets a dot chart show a spread that a zero-based bar chart flattens.
    /// </para>
    /// <para>
    /// Options add <c>radius</c> and <c>sort</c> (<c>'none'</c>, <c>'asc'</c>, <c>'desc'</c>) to the
    /// set the bar charts take. <b>Sorting is usually right</b>: a form whose whole advantage is
    /// comparison gets most of that advantage from putting the values in order.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> CreateDotChart(object rect, object data, object? options = null)
    {
        var plot = JsInterop.AsDict(rect)
            ?? throw new ArgumentException(
                "A chart needs a { x, y, width, height } plot area; a Layout rectangle fits.", nameof(rect));

        var opt = JsInterop.AsDict(options);
        RefuseUnknownOptions(opt);

        var (values, labels) = ReadData(data, opt);
        if (values.Length == 0) throw new ArgumentException("A chart needs at least one value.", nameof(data));

        var order = SortOrder(values, opt);

        var px = Num(plot, "x");
        var py = Num(plot, "y");
        var pw = Num(plot, "width");
        var ph = Num(plot, "height");

        var padding = Math.Clamp(Opt(opt, "padding", 0.4d), 0d, 0.95d);
        var tickCount = (int)Opt(opt, "tickCount", 5d);
        var labelGap = Opt(opt, "labelGap", 8d);
        var tickGap = Opt(opt, "tickGap", 8d);

        var scaleTk = new ScaleToolkit();
        var niced = scaleTk.Nice(values.Min(), values.Max(), tickCount);
        var min = Opt(opt, "min", Convert.ToDouble(niced["min"], CultureInfo.InvariantCulture));
        var max = Opt(opt, "max", Convert.ToDouble(niced["max"], CultureInfo.InvariantCulture));

        var scale = scaleTk.Linear(min, max, px, px + pw);
        var band = scaleTk.Band(order.Length, py, py + ph, padding);
        var radius = Opt(opt, "radius", Math.Clamp(band.Bandwidth * 0.32d, 2d, 9d));

        var dots = new List<Dictionary<string, object>>(order.Length);
        var labelList = new List<Dictionary<string, object>>(order.Length);

        for (var row = 0; row < order.Length; row++)
        {
            var source = order[row];
            var cy = band.Center(row);
            var cx = scale.Map(values[source]);

            dots.Add(new Dictionary<string, object>
            {
                ["index"] = row,
                ["sourceIndex"] = source,
                ["value"] = values[source],
                ["label"] = labels[source],
                ["cx"] = cx,
                ["cy"] = cy,
                ["radius"] = radius,

                // The leader runs from the category axis to the dot. It is chrome, so it is reported
                // rather than drawn — see the erasing pass in polson://manual/13 §3.
                ["leaderX1"] = px,
                ["leaderY1"] = cy,
                ["leaderX2"] = cx,
                ["leaderY2"] = cy
            });

            labelList.Add(new Dictionary<string, object>
            {
                ["text"] = labels[source],
                ["x"] = px - labelGap,
                ["y"] = cy,
                ["align"] = "right",
                ["baseline"] = "middle",
                ["index"] = row
            });
        }

        var ticks = scale.Ticks(tickCount).Select(t => new Dictionary<string, object>
        {
            ["value"] = t,
            ["position"] = scale.Map(t),
            ["label"] = Format(t),
            ["x"] = scale.Map(t),
            ["y"] = py + ph + tickGap
        }).ToArray();

        return new Dictionary<string, object?>
        {
            ["type"] = "dot",
            ["plot"] = Rect(px, py, pw, ph),
            ["bounds"] = Rect(px, py, pw, ph),
            ["scale"] = scale,
            ["band"] = band,
            ["dots"] = dots.ToArray(),
            ["ticks"] = ticks,
            ["labels"] = labelList.ToArray(),
            ["min"] = min,
            ["max"] = max,
            ["radius"] = radius,
            ["sorted"] = (opt?["sort"]?.ToString() ?? "none").ToLowerInvariant(),

            // Rank 1: position along a common scale, the most accurately decoded judgment there is.
            ["encoding"] = "position",
            ["encodingRank"] = 1,
            ["isZeroBased"] = scale.IsZeroBased,

            // Position encodes differences, and a linear mapping preserves their ratios wherever the
            // axis starts — so this is 1 whether or not the axis is cropped. See the remarks above.
            ["lieFactor"] = 1d
        };
    }

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

        // One geometry call for every form: the model says which marks it has, and each shape knows
        // how to become a path. A caller clipping a texture into a chart should not have to know
        // whether it was built from bars or dots.
        var rows = (model["bars"] ?? model["dots"]) as IEnumerable
            ?? throw new ArgumentException(
                "That does not look like a chart model — it has neither 'bars' nor 'dots'.",
                nameof(chartModel));

        var circular = model["dots"] is not null;
        var marks = new List<CanvasPath>();
        CanvasPath? silhouette = null;

        foreach (var row in rows)
        {
            var mark = JsInterop.AsDict(row);
            if (mark is null) continue;

            var path = new CanvasPath();
            if (circular)
            {
                path.Arc((float)Num(mark, "cx"), (float)Num(mark, "cy"),
                    (float)Num(mark, "radius"), 0f, (float)(Math.PI * 2d));
            }
            else
            {
                path.Rect((float)Num(mark, "x"), (float)Num(mark, "y"),
                    (float)Num(mark, "width"), (float)Num(mark, "height"));
            }

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

    /// <summary>The row order a dot chart draws in — given order, or sorted by value.</summary>
    /// <remarks>
    /// Returns indices into the original data rather than reordering it, so every dot keeps a
    /// <c>sourceIndex</c> back to the row it came from. A caller colouring dots from a parallel array
    /// would otherwise silently mismatch them the moment sorting was switched on.
    /// </remarks>
    static int[] SortOrder(double[] values, IDictionary? opt)
    {
        var order = Enumerable.Range(0, values.Length).ToArray();
        var how = opt?["sort"]?.ToString()?.ToLowerInvariant();

        return how switch
        {
            null or "" or "none" => order,
            "asc" or "ascending" => [.. order.OrderBy(i => values[i])],
            "desc" or "descending" => [.. order.OrderByDescending(i => values[i])],
            _ => throw new ArgumentException(
                $"sort must be 'none', 'asc' or 'desc'; got '{how}'.", nameof(opt))
        };
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
