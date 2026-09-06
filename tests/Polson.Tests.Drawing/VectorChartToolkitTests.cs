namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Polson.Drawing.Skia;
using Polson.Drawing.Svg;

using Xunit;

/// <summary>
/// Drawing a chart model onto a Snap paper.
/// </summary>
/// <remarks>
/// <para>
/// The models were always surface-agnostic — <c>ChartToolkit</c> is 2,436 lines of which exactly one
/// method takes a canvas — so a chart could be computed for a vector deliverable and never drawn
/// into one. These tests pin the drawing half.
/// </para>
/// <para>
/// <b>The shape of the test is "count what each form emitted", and that is what found both bugs.</b>
/// A form that silently falls through to the slots fallback still produces elements, so an eyeball
/// check passes: the ring meter emitted one flat rectangle and looked like a chart. Only counting
/// paths against rectangles showed it.
/// </para>
/// </remarks>
public class VectorChartToolkitTests
{
    #region Every form draws
    public static TheoryData<string, string, string> Forms => new()
    {
        // form,               model type,          the element it must emit
        { "column",            "column",            "rect" },
        { "bar",               "bar",               "rect" },
        { "dot",               "dot",               "circle" },
        { "waffle",            "waffle",            "rect" },
        { "framedRectangle",   "framedRectangle",   "rect" },
        { "proportionalShapes","proportionalShapes","circle" },
        { "pictogram",         "pictogram",         "rect" },
        { "arcMeter",          "progressMeter",     "path" },
        { "callout",           "callout",           "text" },
    };

    [Theory]
    [MemberData(nameof(Forms))]
    public void TestEveryFormEmitsRealGeometry(string form, string expectedType, string element)
    {
        var (paper, model) = Draw(form);

        Assert.Equal(expectedType, model["type"]);

        var xml = paper.ToString();
        Assert.True(Count(xml, element) > 0,
            $"{form} emitted no <{element}>; it produced:\n{Census(xml)}");
    }

    /// <summary>
    /// The ring meter is the case a fallback hides. It must be paths, and it must not be a rectangle
    /// — a rectangle here means the switch missed the type and the slots fallback ran instead.
    /// </summary>
    [Fact]
    public void TestAnArcMeterIsPathsRatherThanTheSlotsFallback()
    {
        var (paper, _) = Draw("arcMeter");
        var xml = paper.ToString();

        Assert.Equal(2, Count(xml, "path"));      // the track and the filled part
        Assert.Equal(0, Count(xml, "rect"));
        Assert.Contains("A", xml, StringComparison.Ordinal);   // real arcs, not a polygon approximation
    }

    /// <summary>
    /// The track sweeps a full turn, and an SVG arc whose endpoints coincide is <b>dropped by the
    /// renderer</b> rather than drawn as a circle. Emitted as one arc the ring silently vanished and
    /// only the coloured fill appeared — which reads as a deliberate design, not a missing element.
    /// </summary>
    [Fact]
    public void TestAFullTurnTrackIsSplitSoItDoesNotVanish()
    {
        var (paper, model) = Draw("arcMeter");
        var track = (IDictionary<string, object?>)model["track"]!;

        // The precondition that makes this the dangerous case.
        var sweep = Math.Abs(Convert.ToDouble(track["endAngleDeg"]) - Convert.ToDouble(track["startAngleDeg"]));
        Assert.Equal(360d, sweep, 1);

        var d = Regex.Match(paper.ToString(), "<path[^>]*d=\"([^\"]+)\"").Groups[1].Value;
        var arcs = Regex.Matches(d, "A").Count;

        Assert.True(arcs >= 4, $"a full-turn annulus needs at least four arc segments; d had {arcs}:\n{d}");

        // Endpoints must differ on every segment, which is the property that was actually violated.
        var moveTo = Regex.Match(d, @"M(-?[\d.]+) (-?[\d.]+)");
        var firstArcEnd = Regex.Match(d, @"A[\d.]+ [\d.]+ 0 \d \d (-?[\d.]+) (-?[\d.]+)");
        Assert.False(moveTo.Groups[1].Value == firstArcEnd.Groups[1].Value
                  && moveTo.Groups[2].Value == firstArcEnd.Groups[2].Value,
            "the first arc starts and ends at the same point, so a renderer will drop it");
    }

    /// <summary>A bar meter stays rectangles, so the two shapes are told apart by the model not by luck.</summary>
    [Fact]
    public void TestABarMeterIsRectangles()
    {
        var chart = new ChartToolkit();
        var paper = new SnapPaper(400, 200);
        paper.Chart(chart.CreateProgressMeter(Rect(0, 0, 400, 60), 78, null));

        var xml = paper.ToString();
        Assert.Equal(2, Count(xml, "rect"));
        Assert.Equal(0, Count(xml, "path"));
    }
    #endregion

    #region What the counts must be
    /// <summary>
    /// A waffle draws the whole grid, filled and unfilled alike — it is countable only because the
    /// hundred is visible, so an unfilled cell is a mark rather than an absence.
    /// </summary>
    [Fact]
    public void TestAWaffleDrawsEveryCellIncludingTheEmptyOnes()
    {
        var (paper, model) = Draw("waffle");
        var cells = ((IEnumerable<object?>)model["cells"]!).Count();

        Assert.Equal(100, cells);
        Assert.Equal(100, Count(paper.ToString(), "rect"));
    }

    /// <summary>A dot chart draws its leaders; without them a grouped chart reads as scattered points.</summary>
    [Fact]
    public void TestADotChartDrawsLeadersAsWellAsDots()
    {
        var (paper, _) = Draw("dot");
        var xml = paper.ToString();

        Assert.Equal(5, Count(xml, "circle"));
        Assert.Equal(5, Count(xml, "line"));
    }

    /// <summary>
    /// The frame is the mechanism, not decoration: without identical reference boxes the reader's
    /// task drops from position-on-non-aligned-scales to plain length.
    /// </summary>
    [Fact]
    public void TestFramedRectanglesDrawFramesAndFills()
    {
        var (paper, _) = Draw("framedRectangle");
        var xml = paper.ToString();

        Assert.Equal(10, Count(xml, "rect"));                       // five frames, five fills
        // Serialised as a style declaration rather than a presentation attribute, which is what
        // Svg.Skia emits: style="fill:none;". Asserting the attribute form tested the serializer,
        // not the drawing.
        Assert.Contains("fill:none", xml, StringComparison.Ordinal);
    }
    #endregion

    #region Styling
    /// <summary>
    /// Per-mark colours, because one fill for a whole chart is what a canvas context imposes and what
    /// nearly every real chart needs to escape.
    /// </summary>
    [Fact]
    public void TestColoursAreAppliedPerMark()
    {
        var chart = new ChartToolkit();
        var paper = new SnapPaper(400, 300);
        paper.Chart(chart.CreateColumnChart(Rect(0, 0, 400, 300), Data(), null),
            new Dictionary<string, object?> { ["colors"] = new[] { "#111111", "#222222", "#333333" } });

        var xml = paper.ToString();
        foreach (var colour in new[] { "#111111", "#222222", "#333333" })
        {
            Assert.Contains(colour, xml, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Fewer colours than marks cycles rather than throwing or leaving marks unpainted.</summary>
    [Fact]
    public void TestAShortColourListCycles()
    {
        var chart = new ChartToolkit();
        var paper = new SnapPaper(400, 300);
        paper.Chart(chart.CreateColumnChart(Rect(0, 0, 400, 300), Data(), null),
            new Dictionary<string, object?> { ["colors"] = new[] { "#aaaaaa", "#bbbbbb" } });

        var xml = paper.ToString();
        Assert.Equal(5, Count(xml, "rect"));
        Assert.True(Regex.Matches(xml, "#aaaaaa", RegexOptions.IgnoreCase).Count >= 3);
    }

    /// <summary>One group per chart, so two charts on one paper stay distinguishable and transformable.</summary>
    [Fact]
    public void TestEachChartIsItsOwnGroup()
    {
        var chart = new ChartToolkit();
        var paper = new SnapPaper(800, 300);
        paper.Chart(chart.CreateColumnChart(Rect(0, 0, 400, 300), Data(), null));
        paper.Chart(chart.CreateBarChart(Rect(400, 0, 400, 300), Data(), null));

        Assert.Equal(2, Count(paper.ToString(), "g"));
    }
    #endregion

    #region Refusals
    [Fact]
    public void TestANonModelIsRefusedByName()
    {
        var paper = new SnapPaper(100, 100);

        var ex = Assert.Throws<ArgumentException>(() => paper.Chart("not a model"));
        Assert.Contains("Chart.create", ex.Message, StringComparison.Ordinal);
        Assert.Contains("String", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// An unknown type falls back to `slots`, which every model carries. Drawing something beats
    /// refusing, and it is what keeps a future form usable before this file knows about it.
    /// </summary>
    [Fact]
    public void TestAnUnknownTypeFallsBackToSlots()
    {
        var paper = new SnapPaper(200, 200);
        var model = new Dictionary<string, object?>
        {
            ["type"] = "somethingNew",
            ["slots"] = new object[]
            {
                new Dictionary<string, object?> { ["x"] = 10.0, ["y"] = 10.0, ["width"] = 40.0, ["height"] = 60.0 },
                new Dictionary<string, object?> { ["x"] = 60.0, ["y"] = 20.0, ["width"] = 40.0, ["height"] = 50.0 },
            },
        };

        paper.Chart(model);
        Assert.Equal(2, Count(paper.ToString(), "rect"));
    }
    #endregion

    #region Fixtures
    private static object Rect(float x, float y, float w, float h) =>
        new LayoutToolkit().Rect(x, y, w, h);

    private static object[] Data() =>
    [
        new Dictionary<string, object?> { ["label"] = "Mar", ["value"] = 38.0 },
        new Dictionary<string, object?> { ["label"] = "Apr", ["value"] = 61.0 },
        new Dictionary<string, object?> { ["label"] = "May", ["value"] = 47.0 },
        new Dictionary<string, object?> { ["label"] = "Jun", ["value"] = 92.0 },
        new Dictionary<string, object?> { ["label"] = "Jul", ["value"] = 74.0 },
    ];

    private static (SnapPaper Paper, IDictionary<string, object?> Model) Draw(string form)
    {
        var chart = new ChartToolkit();
        var plot = Rect(0, 0, 400, 300);
        var data = Data();

        Dictionary<string, object?> model = form switch
        {
            "column" => chart.CreateColumnChart(plot, data, null),
            "bar" => chart.CreateBarChart(plot, data, null),
            "dot" => chart.CreateDotChart(plot, data, null),
            "waffle" => chart.CreateWaffle(plot, data, null),
            "framedRectangle" => chart.CreateFramedRectangleChart(plot, data, null),
            "proportionalShapes" => chart.CreateProportionalShapes(plot, data, null),
            "pictogram" => chart.CreatePictogram(plot, data,
                new Dictionary<string, object?> { ["unit"] = 20.0 }),
            "arcMeter" => chart.CreateProgressMeter(plot, 78,
                new Dictionary<string, object?> { ["shape"] = "arc", ["thickness"] = 18.0 }),
            "callout" => chart.CreateCallout(plot, 135,
                new Dictionary<string, object?> { ["label"] = "TOTAL", ["prefix"] = "$", ["unit"] = "M" }),
            _ => throw new ArgumentException("unknown form " + form, nameof(form)),
        };

        var paper = new SnapPaper(400, 300);
        paper.Chart(model);
        return (paper, model);
    }

    private static int Count(string xml, string tag) => Regex.Matches(xml, "<" + tag + "[ />]").Count;

    /// <summary>What a document actually contains, so a failure names the shape rather than only the absence.</summary>
    private static string Census(string xml) => string.Join(", ",
        new[] { "rect", "circle", "path", "text", "line", "ellipse", "g" }
            .Select(t => $"{t}:{Count(xml, t)}"));
    #endregion
}
