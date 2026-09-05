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
    [
        "baseline", "max", "min", "padding", "tickCount", "labels", "labelGap", "tickGap",
        "radius", "sort", "groupGap", "headingHeight", "frameWidth", "frameHeight", "columns", "gap",
        "form", "rowGap", "titleHeight"
    ];

    /// <summary>Options a per-panel chart understands, so the rest are not handed down to be refused.</summary>
    /// <remarks>
    /// <c>labels</c> belongs here: small multiples usually repeat one set of categories across every
    /// panel — the same months, the same products — so one array serves them all. It was left out of
    /// the first version and, being valid at the grid level and unknown at the panel level, was
    /// accepted and then silently discarded. Found by drawing one and noticing the months were
    /// missing, which is the only way it could have been found.
    /// </remarks>
    private static readonly string[] PanelOptions =
        ["baseline", "padding", "tickCount", "labelGap", "tickGap", "radius", "sort",
         "groupGap", "headingHeight", "frameWidth", "frameHeight", "labels"];
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
    /// A dot chart whose rows are gathered into labelled groups, all against <b>one shared axis</b>.
    /// </summary>
    /// <remarks>
    /// The second of the three forms Cleveland and McGill offer in place of the charts they want
    /// surgery on. Grouping adds structure without spending accuracy: every dot is still read against
    /// the same scale, so the judgment stays rank 1, and the reader gains a second comparison — within
    /// a group, and between groups — for the price of some vertical space.
    /// <para>
    /// <b>The one scale is the whole point, and it is why this is not several small charts.</b> Panels
    /// with their own axes would put each group on its own scale, which is the small-multiples lie of
    /// <c>polson://manual/13</c> §2 — comparable-looking and not comparable. Here the axis is computed
    /// across every group at once and cannot drift apart.
    /// </para>
    /// <para>
    /// Rows carry their group in a <c>group</c> field: <c>{ group: 'Nordics', label: 'Norway',
    /// value: 74.1 }</c>. Group order is first-seen, which keeps a deliberate ordering the caller
    /// chose; <c>sort</c> orders rows <b>within</b> each group rather than across the whole chart.
    /// </para>
    /// <para>
    /// <b><see cref="CreateDotChart"/> ignores a <c>group</c> field rather than grouping by it.</b>
    /// Stated because the alternative is a silent surprise — data shaped for this call, passed to that
    /// one, would draw a correct ungrouped chart and lose the structure without saying so.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> CreateGroupedDotChart(object rect, object data, object? options = null)
    {
        var plot = JsInterop.AsDict(rect)
            ?? throw new ArgumentException(
                "A chart needs a { x, y, width, height } plot area; a Layout rectangle fits.", nameof(rect));

        var opt = JsInterop.AsDict(options);
        RefuseUnknownOptions(opt);

        var (values, labels) = ReadData(data, opt);
        if (values.Length == 0) throw new ArgumentException("A chart needs at least one value.", nameof(data));

        var groupOf = ReadGroups(data, values.Length);

        // First-seen order, so a caller who arranged the groups deliberately keeps that arrangement.
        var groupNames = new List<string>();
        foreach (var name in groupOf)
        {
            if (!groupNames.Contains(name, StringComparer.Ordinal)) groupNames.Add(name);
        }

        var px = Num(plot, "x");
        var py = Num(plot, "y");
        var pw = Num(plot, "width");
        var ph = Num(plot, "height");

        var tickCount = (int)Opt(opt, "tickCount", 5d);
        var labelGap = Opt(opt, "labelGap", 8d);
        var tickGap = Opt(opt, "tickGap", 8d);
        var groupGap = Opt(opt, "groupGap", 14d);
        var headingHeight = Opt(opt, "headingHeight", 18d);

        // One scale over every group at once. Computed here rather than per group, which is the
        // difference between a grouped chart and several small ones that only look comparable.
        var scaleTk = new ScaleToolkit();
        var niced = scaleTk.Nice(values.Min(), values.Max(), tickCount);
        var min = Opt(opt, "min", Convert.ToDouble(niced["min"], CultureInfo.InvariantCulture));
        var max = Opt(opt, "max", Convert.ToDouble(niced["max"], CultureInfo.InvariantCulture));
        var scale = scaleTk.Linear(min, max, px, px + pw);

        var reserved = groupNames.Count * headingHeight + Math.Max(0, groupNames.Count - 1) * groupGap;
        var rowStep = (ph - reserved) / values.Length;

        if (rowStep <= 0d)
        {
            throw new ArgumentException(
                $"{values.Length} rows in {groupNames.Count} groups need more than {ph:0} px of height: "
                + $"{reserved:0} px goes to headings and gaps before any row is drawn. Give the chart a "
                + "taller plot, or reduce headingHeight / groupGap.", nameof(rect));
        }

        var radius = Opt(opt, "radius", Math.Clamp(rowStep * 0.28d, 2d, 9d));

        var dots = new List<Dictionary<string, object>>(values.Length);
        var labelList = new List<Dictionary<string, object>>(values.Length);
        var groups = new List<Dictionary<string, object>>(groupNames.Count);

        var y = py;
        for (var g = 0; g < groupNames.Count; g++)
        {
            var name = groupNames[g];
            var members = Enumerable.Range(0, values.Length)
                .Where(i => string.Equals(groupOf[i], name, StringComparison.Ordinal))
                .ToArray();

            var headingY = y;
            y += headingHeight;
            var firstRowY = y;

            // Sorting is within the group: the comparison a grouped chart is built to support is
            // between its members, and ordering across the whole chart would break the grouping up.
            foreach (var source in SortOrder(values, opt, members))
            {
                var cy = y + rowStep / 2d;
                var cx = scale.Map(values[source]);

                dots.Add(new Dictionary<string, object>
                {
                    ["index"] = dots.Count,
                    ["sourceIndex"] = source,
                    ["value"] = values[source],
                    ["label"] = labels[source],
                    ["group"] = name,
                    ["groupIndex"] = g,
                    ["cx"] = cx,
                    ["cy"] = cy,
                    ["radius"] = radius,
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
                    ["index"] = labelList.Count,
                    ["group"] = name
                });

                y += rowStep;
            }

            var memberValues = members.Select(i => values[i]).ToArray();
            groups.Add(new Dictionary<string, object>
            {
                ["name"] = name,
                ["index"] = g,
                ["count"] = members.Length,
                ["y"] = headingY,
                ["y2"] = y,
                ["height"] = y - headingY,
                ["headingX"] = px - labelGap,
                ["headingY"] = headingY,

                // Cheap to compute here and awkward to recover afterwards once rows are sorted.
                ["min"] = memberValues.Min(),
                ["max"] = memberValues.Max(),
                ["mean"] = memberValues.Average()
            });

            if (g < groupNames.Count - 1) y += groupGap;
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
            ["type"] = "groupedDot",
            ["plot"] = Rect(px, py, pw, ph),
            ["bounds"] = Rect(px, py, pw, ph),
            ["scale"] = scale,
            ["dots"] = dots.ToArray(),
            ["groups"] = groups.ToArray(),
            ["ticks"] = ticks,
            ["labels"] = labelList.ToArray(),
            ["min"] = min,
            ["max"] = max,
            ["radius"] = radius,
            ["rowStep"] = rowStep,
            ["sorted"] = (opt?["sort"]?.ToString() ?? "none").ToLowerInvariant(),
            ["encoding"] = "position",
            ["encodingRank"] = 1,
            ["isZeroBased"] = scale.IsZeroBased,
            ["lieFactor"] = 1d
        };
    }

    /// <summary>
    /// Framed rectangles: a value read as a <b>level inside an identical box</b>, placed anywhere.
    /// </summary>
    /// <remarks>
    /// The third of Cleveland and McGill's replacements, and the one they offer for the **shaded
    /// statistical map** — the choropleth, whose reader must judge shading, the very bottom of the
    /// hierarchy. Every frame is the same size, so the eye compares fill levels between identical
    /// boxes, like reading a row of thermometers.
    /// <para>
    /// <b>The frame is the mechanism, not decoration.</b> In their words: had the bars been shown
    /// without frames, *"the elementary task would then have been perceiving length"* — rank 3 — and
    /// the frames are *"one step higher in the hierarchy"*. That puts this at **rank 2**: position
    /// along identical but non-aligned scales, since the boxes sit at different places. Strip the
    /// frames and you have lost the whole point of the form.
    /// </para>
    /// <para>
    /// <b>It also removes two faults of a shaded map that have nothing to do with the hierarchy.</b>
    /// First, shading a region makes its total ink the value <i>times its area</i>, so on a US map
    /// Texas is imposing and Rhode Island is hard to see, whatever the numbers say; identical frames
    /// cannot do that. Second, contiguous shaded regions merge into clusters the eye reads as
    /// structure whether or not any exists.
    /// </para>
    /// <para>
    /// Rows carry a position — <c>{ label: 'TX', value: 12.7, x: 210, y: 340 }</c> — in the same
    /// coordinate space as the plot rectangle, because the caller owns the projection and this
    /// toolkit has none. Rows without a position are laid out on a grid instead, which makes the form
    /// usable for any set of small comparable readings, not only maps.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> CreateFramedRectangleChart(object rect, object data, object? options = null)
    {
        var plot = JsInterop.AsDict(rect)
            ?? throw new ArgumentException(
                "A chart needs a { x, y, width, height } plot area; a Layout rectangle fits.", nameof(rect));

        var opt = JsInterop.AsDict(options);
        RefuseUnknownOptions(opt);

        var (values, labels) = ReadData(data, opt);
        if (values.Length == 0) throw new ArgumentException("A chart needs at least one value.", nameof(data));

        var positions = ReadPositions(data, values.Length);

        var px = Num(plot, "x");
        var py = Num(plot, "y");
        var pw = Num(plot, "width");
        var ph = Num(plot, "height");

        var frameWidth = Opt(opt, "frameWidth", 14d);
        var frameHeight = Opt(opt, "frameHeight", 34d);
        var tickCount = (int)Opt(opt, "tickCount", 4d);

        if (frameWidth <= 0d || frameHeight <= 0d)
        {
            throw new ArgumentException("frameWidth and frameHeight must be positive.", nameof(options));
        }

        // One scale for every frame, mapping a value to a level inside a box. Identical boxes are
        // what make this rank 2 rather than rank 3; a shared scale is what makes them comparable.
        var scaleTk = new ScaleToolkit();
        var niced = scaleTk.Nice(values.Min(), values.Max(), tickCount);
        var min = Opt(opt, "min", Convert.ToDouble(niced["min"], CultureInfo.InvariantCulture));
        var max = Opt(opt, "max", Convert.ToDouble(niced["max"], CultureInfo.InvariantCulture));
        var level = scaleTk.Linear(min, max, frameHeight, 0d);   // 0 at the frame's foot

        var placed = positions is not null;
        var columns = Math.Max(1, (int)Opt(opt, "columns", Math.Ceiling(Math.Sqrt(values.Length))));
        var gap = Opt(opt, "gap", 12d);

        var items = new List<Dictionary<string, object>>(values.Length);
        var labelList = new List<Dictionary<string, object>>(values.Length);

        for (var i = 0; i < values.Length; i++)
        {
            double anchorX, anchorY;
            if (placed)
            {
                anchorX = positions![i].X;
                anchorY = positions[i].Y;
            }
            else
            {
                // No positions: a grid, so the form still serves a plain set of small readings.
                var row = i / columns;
                var column = i % columns;
                anchorX = px + column * (frameWidth + gap) + frameWidth / 2d;
                anchorY = py + row * (frameHeight + gap + 14d) + frameHeight / 2d;
            }

            var fx = anchorX - frameWidth / 2d;
            var fy = anchorY - frameHeight / 2d;
            var top = fy + level.Map(values[i]);

            items.Add(new Dictionary<string, object>
            {
                ["index"] = i,
                ["sourceIndex"] = i,
                ["value"] = values[i],
                ["label"] = labels[i],
                ["anchorX"] = anchorX,
                ["anchorY"] = anchorY,

                // The reference box — identical for every item, which is the whole mechanism.
                ["frame"] = Rect(fx, fy, frameWidth, frameHeight),

                // The data-bearing part: filled from the foot of the frame up to the value's level.
                ["fill"] = Rect(fx, top, frameWidth, fy + frameHeight - top),

                // How full the frame is, 0 to 1. Reported instead of the fill's absolute y because
                // frames sit at different places — on a map, at different latitudes — so an absolute
                // coordinate is not comparable between them and inviting that comparison would be a
                // trap. The fraction is what the reader actually judges, and it *is* comparable.
                ["fraction"] = (fy + frameHeight - top) / frameHeight
            });

            labelList.Add(new Dictionary<string, object>
            {
                ["text"] = labels[i],
                ["x"] = anchorX,
                ["y"] = fy + frameHeight + 4d,
                ["align"] = "center",
                ["baseline"] = "top",
                ["index"] = i
            });
        }

        // Ticks as offsets from a frame's foot, so a legend frame can be drawn anywhere.
        var ticks = level.Ticks(tickCount).Select(t => new Dictionary<string, object>
        {
            ["value"] = t,
            ["offset"] = level.Map(t),
            ["label"] = Format(t)
        }).ToArray();

        return new Dictionary<string, object?>
        {
            ["type"] = "framedRectangle",
            ["plot"] = Rect(px, py, pw, ph),
            ["bounds"] = Rect(px, py, pw, ph),
            ["scale"] = level,
            ["items"] = items.ToArray(),
            ["ticks"] = ticks,
            ["labels"] = labelList.ToArray(),
            ["min"] = min,
            ["max"] = max,
            ["frameWidth"] = frameWidth,
            ["frameHeight"] = frameHeight,
            ["positioned"] = placed,

            // Rank 2: position along identical but non-aligned scales. The frames buy exactly one
            // step over located bars, which would be rank 3 — see the remarks above.
            ["encoding"] = "position",
            ["encodingRank"] = 2,
            ["isZeroBased"] = level.IsZeroBased,

            // As the dot chart: the reader compares levels, and a linear mapping preserves the ratios
            // of differences wherever the scale starts.
            ["lieFactor"] = 1d
        };
    }

    /// <summary>
    /// A grid of panels sharing <b>one scale</b>, computed across every series at once.
    /// </summary>
    /// <remarks>
    /// <b>This exists to make a rule unbreakable rather than merely stated.</b> Small multiples must
    /// share a scale — panels drawn to their own extents look comparable and are not, which is a lie
    /// told by the layout rather than by any single chart, and the one failure in
    /// <c>polson://manual/13</c> §2 that no individual panel can detect because each is correct on its
    /// own terms. Built by hand it takes one forgotten argument to get wrong. Here the extent is taken
    /// across every series before any panel is built, and there is no argument to forget.
    /// <para>
    /// <b>It returns charts, not pictures.</b> Each panel carries a complete model of whichever form
    /// you asked for, so everything that works on a chart works on a panel — <c>drawChart</c>,
    /// <c>createChartGeometry</c>, the ticks, the labels, the integrity fields. A construction whose
    /// output is more constructions.
    /// </para>
    /// <para>
    /// <c>series</c> is an array of <c>{ label, data }</c>, or of bare arrays. <c>form</c> chooses what
    /// each panel is: <c>'column'</c> (default), <c>'bar'</c>, <c>'dot'</c>, <c>'groupedDot'</c> or
    /// <c>'framedRectangle'</c>. Panel-level options are passed through; grid options
    /// (<c>columns</c>, <c>gap</c>, <c>rowGap</c>, <c>titleHeight</c>) are not handed down.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> CreateSmallMultiples(object rect, object series, object? options = null)
    {
        var area = JsInterop.AsDict(rect)
            ?? throw new ArgumentException(
                "Small multiples need a { x, y, width, height } area; a Layout rectangle fits.", nameof(rect));

        var opt = JsInterop.AsDict(options);
        RefuseUnknownOptions(opt);

        var panelsIn = ReadSeries(series);
        if (panelsIn.Count == 0)
        {
            throw new ArgumentException("Small multiples need at least one series.", nameof(series));
        }

        var form = (opt?["form"]?.ToString() ?? "column").ToLowerInvariant();
        var columns = Math.Max(1, (int)Opt(opt, "columns", Math.Ceiling(Math.Sqrt(panelsIn.Count))));
        var rows = (int)Math.Ceiling(panelsIn.Count / (double)columns);
        var gap = Opt(opt, "gap", 18d);
        var rowGap = Opt(opt, "rowGap", gap);
        var titleHeight = Opt(opt, "titleHeight", 18d);

        // The whole point, and it happens before a single panel is built: one extent over everything.
        var everyValue = panelsIn.SelectMany(p => ReadData(p.Data, null).Values).ToArray();
        if (everyValue.Length == 0)
        {
            throw new ArgumentException("Small multiples need at least one value.", nameof(series));
        }

        var scaleTk = new ScaleToolkit();
        var lo = everyValue.Min();
        var hi = everyValue.Max();

        // A length encoding still needs its zero inside the domain, or every panel would share one
        // scale and each would individually lie. Sharing a scale does not excuse truncating it.
        var lengthForm = form is "column" or "bar";
        var niced = scaleTk.Nice(lengthForm ? Math.Min(0d, lo) : lo, hi);
        var min = Opt(opt, "min", Convert.ToDouble(niced["min"], CultureInfo.InvariantCulture));
        var max = Opt(opt, "max", Convert.ToDouble(niced["max"], CultureInfo.InvariantCulture));

        var cells = new LayoutToolkit().Grid(area, columns, rows, (float)gap, (float)rowGap);
        var panels = new List<Dictionary<string, object?>>(panelsIn.Count);

        for (var i = 0; i < panelsIn.Count; i++)
        {
            var cell = cells[i];
            var plot = new Dictionary<string, object>
            {
                ["x"] = Convert.ToDouble(cell["x"], CultureInfo.InvariantCulture),
                ["y"] = Convert.ToDouble(cell["y"], CultureInfo.InvariantCulture) + titleHeight,
                ["width"] = Convert.ToDouble(cell["width"], CultureInfo.InvariantCulture),
                ["height"] = Convert.ToDouble(cell["height"], CultureInfo.InvariantCulture) - titleHeight
            };

            var panelOpt = PanelOptionsFrom(opt);
            panelOpt["min"] = min;
            panelOpt["max"] = max;

            panels.Add(new Dictionary<string, object?>
            {
                ["index"] = i,
                ["label"] = panelsIn[i].Label,
                ["cell"] = cell,
                ["titleX"] = Convert.ToDouble(cell["x"], CultureInfo.InvariantCulture),
                ["titleY"] = Convert.ToDouble(cell["y"], CultureInfo.InvariantCulture),
                ["chart"] = BuildPanel(form, plot, panelsIn[i].Data, panelOpt)
            });
        }

        var first = (Dictionary<string, object?>)panels[0]["chart"]!;

        return new Dictionary<string, object?>
        {
            ["type"] = "smallMultiples",
            ["form"] = form,
            ["plot"] = Rect(Num(area, "x"), Num(area, "y"), Num(area, "width"), Num(area, "height")),
            ["bounds"] = Rect(Num(area, "x"), Num(area, "y"), Num(area, "width"), Num(area, "height")),
            ["panels"] = panels.ToArray(),
            ["columns"] = columns,
            ["rows"] = rows,
            ["min"] = min,
            ["max"] = max,

            // Every panel was built from these, so the claim is a fact about the construction rather
            // than something a caller has to keep true.
            ["sharedScale"] = true,
            ["scale"] = first["scale"],
            ["encoding"] = first["encoding"],
            ["encodingRank"] = first["encodingRank"],
            ["isZeroBased"] = first["isZeroBased"],
            ["lieFactor"] = first["lieFactor"]
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
        var rows = (model["bars"] ?? model["dots"] ?? model["items"]) as IEnumerable
            ?? throw new ArgumentException(
                "That does not look like a chart model — it has no 'bars', 'dots' or 'items'.",
                nameof(chartModel));

        var circular = model["dots"] is not null;
        var framed = model["items"] is not null;
        var marks = new List<CanvasPath>();
        var frames = new List<CanvasPath>();
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
            else if (framed)
            {
                // The fill is the mark — it is what changes with the data. The frame is the reference
                // the reader judges against, so it comes back separately rather than being unioned in.
                AddRect(path, JsInterop.AsDict(mark["fill"]));

                var frame = new CanvasPath();
                AddRect(frame, JsInterop.AsDict(mark["frame"]));
                frames.Add(frame);
            }
            else
            {
                AddRect(path, mark);
            }

            marks.Add(path);
            silhouette = silhouette is null ? path : silhouette.Union(path);
        }

        return new Dictionary<string, object?>
        {
            ["marks"] = marks.ToArray(),
            ["frames"] = frames.ToArray(),
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
    static int[] SortOrder(double[] values, IDictionary? opt, int[]? subset = null)
    {
        var order = subset ?? [.. Enumerable.Range(0, values.Length)];
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

    /// <summary>One panel's worth of input: its title and the data behind it.</summary>
    private readonly record struct SeriesInput(string Label, object Data);

    /// <summary>Reads <c>[{ label, data }]</c> or an array of bare arrays.</summary>
    static List<SeriesInput> ReadSeries(object series)
    {
        var found = new List<SeriesInput>();
        if (series is not IEnumerable items || series is string)
        {
            throw new ArgumentException(
                "Small multiples take an array of series — [{ label, data }] or an array of arrays.",
                nameof(series));
        }

        var i = 0;
        foreach (var item in items)
        {
            var row = JsInterop.AsDict(item);
            var payload = row?["data"] ?? row?["values"];

            if (payload is not null)
            {
                found.Add(new SeriesInput(
                    row!["label"]?.ToString() ?? (i + 1).ToString(CultureInfo.InvariantCulture), payload));
            }
            else if (item is IEnumerable and not string)
            {
                found.Add(new SeriesInput((i + 1).ToString(CultureInfo.InvariantCulture), item));
            }
            else if (item is not null)
            {
                throw new ArgumentException(
                    $"Series {i + 1} is neither {{ label, data }} nor an array; got a {item.GetType().Name}.",
                    nameof(series));
            }

            i++;
        }

        return found;
    }

    /// <summary>Only the options a panel chart understands, so the grid's own are not passed down.</summary>
    static Dictionary<string, object?> PanelOptionsFrom(IDictionary? opt)
    {
        var panel = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (opt is null) return panel;

        foreach (DictionaryEntry entry in opt)
        {
            var name = entry.Key?.ToString();
            if (name is not null && PanelOptions.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                panel[name] = entry.Value;
            }
        }

        return panel;
    }

    Dictionary<string, object?> BuildPanel(string form, object plot, object data, object options) =>
        form switch
        {
            "column" => CreateColumnChart(plot, data, options),
            "bar" => CreateBarChart(plot, data, options),
            "dot" => CreateDotChart(plot, data, options),
            "groupeddot" => CreateGroupedDotChart(plot, data, options),
            "framedrectangle" => CreateFramedRectangleChart(plot, data, options),
            _ => throw new ArgumentException(
                $"form must be 'column', 'bar', 'dot', 'groupedDot' or 'framedRectangle'; got '{form}'.",
                nameof(form))
        };

    /// <summary>
    /// Each row's position, or null when the data carries none for every row.
    /// </summary>
    /// <remarks>
    /// All or nothing on purpose. Positions for some rows and not others would place part of the set
    /// on a map and lay the rest out on a grid over the top of it, which is a picture nobody wants and
    /// a mistake nothing downstream could report.
    /// </remarks>
    static (double X, double Y)[]? ReadPositions(object data, int count)
    {
        if (data is not IEnumerable items || data is string) return null;

        var found = new List<(double X, double Y)>(count);
        foreach (var item in items)
        {
            var row = JsInterop.AsDict(item);
            if (row is null || !row.Contains("x") || !row.Contains("y")) return null;
            found.Add((Num(row, "x"), Num(row, "y")));
        }

        return found.Count == count ? [.. found] : null;
    }

    /// <summary>Each row's group name, defaulting to one unnamed group when the data has none.</summary>
    /// <remarks>
    /// A grouped chart over ungrouped data is a legitimate degenerate case — one group holding
    /// everything — rather than an error, so a caller switching between the two forms as their data
    /// arrives does not have to branch.
    /// </remarks>
    static string[] ReadGroups(object data, int count)
    {
        var groups = new List<string>(count);

        if (data is IEnumerable items and not string)
        {
            foreach (var item in items)
            {
                var row = JsInterop.AsDict(item);
                var name = row?["group"]?.ToString();
                groups.Add(string.IsNullOrWhiteSpace(name) ? string.Empty : name);
            }
        }

        while (groups.Count < count) groups.Add(string.Empty);
        return [.. groups];
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

    static void AddRect(CanvasPath path, IDictionary? rect)
    {
        if (rect is null) return;
        path.Rect((float)Num(rect, "x"), (float)Num(rect, "y"),
            (float)Num(rect, "width"), (float)Num(rect, "height"));
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
