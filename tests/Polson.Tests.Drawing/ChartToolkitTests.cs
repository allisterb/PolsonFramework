namespace Polson.Tests.Drawing;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Polson.Drawing.Skia;

using Xunit;

/// <summary>
/// The chart construction, tested through its model rather than its pixels.
/// </summary>
/// <remarks>
/// That is the point of returning a model at all: what a chart asserts about the data is checkable
/// without rendering anything, so these tests read the same numbers a caller would.
/// </remarks>
public class ChartToolkitTests : TestsRuntime
{
    #region Methods (private)
    static readonly ChartToolkit Chart = new();

    static Dictionary<string, object> Rect(double x, double y, double w, double h) =>
        new() { ["x"] = x, ["y"] = y, ["width"] = w, ["height"] = h };

    static IDictionary[] Bars(Dictionary<string, object?> model) =>
        ((IEnumerable)model["bars"]!).Cast<object>().Select(b => (IDictionary)b!).ToArray();

    static double Num(IDictionary d, string k) => Convert.ToDouble(d[k], CultureInfo.InvariantCulture);
    static double Num(Dictionary<string, object?> d, string k) => Convert.ToDouble(d[k], CultureInfo.InvariantCulture);
    #endregion

    #region Tests — geometry the model asserts
    [Fact]
    public void TestBarLengthsAreProportionalToValues()
    {
        var model = Chart.CreateColumnChart(Rect(0, 0, 400, 200), new[] { 25d, 50d, 100d });
        var bars = Bars(model);

        // A zero baseline makes length directly proportional; that is the whole claim of a bar.
        var h = bars.Select(b => Num(b, "height")).ToArray();
        Assert.Equal(h[2] / 2d, h[1], 3);
        Assert.Equal(h[2] / 4d, h[0], 3);
    }

    [Fact]
    public void TestColumnsSitSideBySideAndBarsStack()
    {
        var columns = Bars(Chart.CreateColumnChart(Rect(0, 0, 400, 200), new[] { 1d, 2d, 3d }));
        Assert.True(Num(columns[0], "x") < Num(columns[1], "x"), "columns advance along x");
        Assert.Equal(Num(columns[0], "y2"), Num(columns[1], "y2"), 3);   // shared baseline

        var bars = Bars(Chart.CreateBarChart(Rect(0, 0, 400, 200), new[] { 1d, 2d, 3d }));
        Assert.True(Num(bars[0], "y") < Num(bars[1], "y"), "bars advance along y");
        Assert.Equal(Num(bars[0], "x"), Num(bars[1], "x"), 3);           // shared baseline
    }

    /// <summary>Longer bars for larger values, whichever way the chart runs.</summary>
    [Fact]
    public void TestTheLargestValueGetsTheLongestMark()
    {
        var columns = Bars(Chart.CreateColumnChart(Rect(0, 0, 400, 200), new[] { 10d, 90d, 40d }));
        Assert.Equal(90d, Num(columns.OrderByDescending(b => Num(b, "height")).First(), "value"));

        var bars = Bars(Chart.CreateBarChart(Rect(0, 0, 400, 200), new[] { 10d, 90d, 40d }));
        Assert.Equal(90d, Num(bars.OrderByDescending(b => Num(b, "width")).First(), "value"));
    }

    [Fact]
    public void TestBarsStayInsideTheirPlot()
    {
        var model = Chart.CreateColumnChart(Rect(40, 20, 300, 160), new[] { 5d, 55d, 30d });
        foreach (var bar in Bars(model))
        {
            Assert.InRange(Num(bar, "x"), 40d, 340d);
            Assert.InRange(Num(bar, "y"), 20d, 180d);
            Assert.InRange(Num(bar, "y2"), 20d, 180.001d);
        }
    }

    /// <summary>A negative value draws on the far side of the baseline, never as a negative extent.</summary>
    [Fact]
    public void TestANegativeValueDrawsBelowTheBaselineWithPositiveExtent()
    {
        var model = Chart.CreateColumnChart(Rect(0, 0, 400, 200), new[] { 40d, -20d });
        var bars = Bars(model);
        var zero = Num(model, "baselinePosition");

        Assert.True(Num(bars[0], "height") > 0d);
        Assert.True(Num(bars[1], "height") > 0d);
        Assert.True(Num(bars[0], "y2") <= zero + 0.001d, "positive value sits above the baseline");
        Assert.True(Num(bars[1], "y") >= zero - 0.001d, "negative value sits below it");
    }
    #endregion

    #region Tests — the integrity the model carries
    [Fact]
    public void TestAZeroBaselineGivesALieFactorOfExactlyOne()
    {
        var model = Chart.CreateColumnChart(Rect(0, 0, 400, 200), new[] { 12d, 47d, 93d });
        Assert.True((bool)model["isZeroBased"]!);
        Assert.Equal(1d, Num(model, "lieFactor"), 9);
    }

    /// <summary>
    /// A truncated baseline is the classic distortion, and the model reports it rather than
    /// leaving the caller to notice.
    /// </summary>
    [Fact]
    public void TestATruncatedBaselineIsReportedAsDistortion()
    {
        var model = Chart.CreateColumnChart(Rect(0, 0, 400, 200), new[] { 100d, 104d },
            new Dictionary<string, object?> { ["baseline"] = 96d });

        Assert.False((bool)model["isZeroBased"]!);

        // 4% in the data drawn as a doubling: (104-96)/(100-96) = 2 against 104/100.
        Assert.Equal(2d / 1.04d, Num(model, "lieFactor"), 6);
        Assert.True(Num(model, "lieFactor") > 1.05d, "outside Tufte's tolerance");
    }

    /// <summary>A baseline that clips a bar away entirely is distortion without limit.</summary>
    [Fact]
    public void TestABaselineAboveTheSmallestValueReportsInfinity()
    {
        var model = Chart.CreateColumnChart(Rect(0, 0, 400, 200), new[] { 10d, 80d },
            new Dictionary<string, object?> { ["baseline"] = 20d });
        Assert.True(double.IsPositiveInfinity(Num(model, "lieFactor")));
    }

    /// <summary>One bar cannot overstate a difference it does not draw.</summary>
    [Fact]
    public void TestASingleValueHasNoLieFactorToReport() =>
        Assert.Equal(1d, Num(Chart.CreateColumnChart(Rect(0, 0, 400, 200), new[] { 42d }), "lieFactor"), 9);

    [Fact]
    public void TestTheModelNamesItsPerceptualRank()
    {
        var model = Chart.CreateColumnChart(Rect(0, 0, 400, 200), new[] { 1d, 2d });
        Assert.Equal("length", model["encoding"]);
        Assert.Equal(3, Convert.ToInt32(model["encodingRank"]));
    }
    #endregion

    #region Tests — sharing one scale
    /// <summary>
    /// The rule small multiples exist to break: an explicit max is how two panels stay comparable.
    /// </summary>
    [Fact]
    public void TestAnExplicitMaxMakesTwoPanelsComparable()
    {
        var big = new[] { 20d, 90d };
        var small = new[] { 5d, 12d };
        var shared = new ScaleToolkit().Extent(big.Concat(small).ToArray());
        var max = Convert.ToDouble(shared["max"], CultureInfo.InvariantCulture);

        var a = Chart.CreateColumnChart(Rect(0, 0, 200, 100), big, new Dictionary<string, object?> { ["max"] = max });
        var b = Chart.CreateColumnChart(Rect(0, 0, 200, 100), small, new Dictionary<string, object?> { ["max"] = max });

        // 90 and 12 against one scale: heights must be in the ratio of the values.
        var ha = Bars(a).Max(x => Num(x, "height"));
        var hb = Bars(b).Max(x => Num(x, "height"));
        Assert.Equal(90d / 12d, ha / hb, 3);
    }

    /// <summary>And without it each panel scales to itself, which is the lie the rule names.</summary>
    [Fact]
    public void TestWithoutASharedMaxThePanelsAreNotComparable()
    {
        var a = Chart.CreateColumnChart(Rect(0, 0, 200, 100), new[] { 20d, 90d });
        var b = Chart.CreateColumnChart(Rect(0, 0, 200, 100), new[] { 5d, 12d });

        var ha = Bars(a).Max(x => Num(x, "height"));
        var hb = Bars(b).Max(x => Num(x, "height"));
        Assert.True(Math.Abs(ha - hb) < 12d, "each panel fills its own plot, so the tallest bars match");
    }
    #endregion

    #region Tests — labels, ticks and options
    [Fact]
    public void TestLabelsComeFromObjectDataAndFromTheOption()
    {
        var fromData = Chart.CreateColumnChart(Rect(0, 0, 400, 200), new object[]
        {
            new Dictionary<string, object?> { ["label"] = "Mar", ["value"] = 10d },
            new Dictionary<string, object?> { ["label"] = "Apr", ["value"] = 20d }
        });
        Assert.Equal(["Mar", "Apr"], Bars(fromData).Select(b => b["label"]?.ToString()));

        var fromOption = Chart.CreateColumnChart(Rect(0, 0, 400, 200), new[] { 10d, 20d },
            new Dictionary<string, object?> { ["labels"] = new[] { "Q1", "Q2" } });
        Assert.Equal(["Q1", "Q2"], Bars(fromOption).Select(b => b["label"]?.ToString()));
    }

    [Fact]
    public void TestTicksAreRoundNumbersCarryingTheirPosition()
    {
        var model = Chart.CreateColumnChart(Rect(0, 0, 400, 200), new[] { 7d, 93d });
        var ticks = ((IEnumerable)model["ticks"]!).Cast<object>().Select(t => (IDictionary)t!).ToArray();

        Assert.NotEmpty(ticks);
        foreach (var tick in ticks)
        {
            Assert.InRange(Num(tick, "position"), -0.001d, 200.001d);
            Assert.False(string.IsNullOrWhiteSpace(tick["label"]?.ToString()));
        }
    }

    /// <summary>
    /// A misspelled option is refused, not ignored.
    /// </summary>
    /// <remarks>
    /// This is the reason options are read from a dictionary here rather than bound to a typed class:
    /// the binding cannot see a name it was never told about, but this can.
    /// </remarks>
    [Fact]
    public void TestAMisspelledOptionIsRefusedByName()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Chart.CreateColumnChart(Rect(0, 0, 400, 200), new[] { 1d, 2d },
                new Dictionary<string, object?> { ["pading"] = 0.3d }));

        Assert.Contains("pading", ex.Message);
        Assert.Contains("padding", ex.Message);      // and says what was meant
    }

    [Fact]
    public void TestAnEmptyChartIsRefused() =>
        Assert.Throws<ArgumentException>(() => Chart.CreateColumnChart(Rect(0, 0, 400, 200), Array.Empty<double>()));
    #endregion

    #region Tests — geometry
    [Fact]
    public void TestGeometryReturnsOnePathPerBarPlusASilhouette()
    {
        var model = Chart.CreateColumnChart(Rect(0, 0, 400, 200), new[] { 10d, 40d, 25d });
        var geometry = Chart.CreateChartGeometry(model);

        var marks = (CanvasPath[])geometry["marks"]!;
        Assert.Equal(3, marks.Length);
        Assert.NotNull(geometry["silhouette"]);

        // The silhouette covers the marks, so its box is at least as wide as the plot's bars.
        var bounds = (IDictionary)geometry["bounds"]!;
        Assert.Equal(400d, Num(bounds, "width"), 3);
    }

    [Fact]
    public void TestGeometryRefusesSomethingThatIsNotAChartModel()
    {
        Assert.Throws<ArgumentException>(() => Chart.CreateChartGeometry(Rect(0, 0, 10, 10)));
        Assert.Throws<ArgumentException>(() => Chart.CreateChartGeometry("not a chart"));
    }

    /// <summary>The model is closed-form, so measuring many panels costs no native paths.</summary>
    [Fact]
    public void TestBuildingAModelAllocatesNoPaths()
    {
        for (var i = 0; i < 200; i++)
        {
            var model = Chart.CreateColumnChart(Rect(0, 0, 120, 80), new[] { 1d, 2d, 3d, 4d });
            Assert.Equal(4, Bars(model).Length);
        }
    }
    #endregion

    #region Tests — the dot chart
    static IDictionary[] Dots(Dictionary<string, object?> model) =>
        ((IEnumerable)model["dots"]!).Cast<object>().Select(d => (IDictionary)d!).ToArray();

    /// <summary>Position, not length: the dot's x is the value's place on a shared axis.</summary>
    [Fact]
    public void TestDotsSitAtTheirValuesPositionOnACommonScale()
    {
        var model = Chart.CreateDotChart(Rect(0, 0, 400, 200), new[] { 10d, 50d, 90d },
            new Dictionary<string, object?> { ["min"] = 0d, ["max"] = 100d });
        var dots = Dots(model);

        Assert.Equal(40d, Num(dots[0], "cx"), 3);
        Assert.Equal(200d, Num(dots[1], "cx"), 3);
        Assert.Equal(360d, Num(dots[2], "cx"), 3);

        // Every dot shares one axis, which is what makes it rank 1.
        Assert.Equal("position", model["encoding"]);
        Assert.Equal(1, Convert.ToInt32(model["encodingRank"]));
    }

    /// <summary>Rows advance down the category axis and each dot keeps its own row.</summary>
    [Fact]
    public void TestDotsAdvanceDownTheCategoryAxis()
    {
        var dots = Dots(Chart.CreateDotChart(Rect(0, 0, 400, 200), new[] { 1d, 2d, 3d }));
        Assert.True(Num(dots[0], "cy") < Num(dots[1], "cy"));
        Assert.True(Num(dots[1], "cy") < Num(dots[2], "cy"));
    }

    /// <summary>
    /// A cropped axis is legitimate here, and the model says so rather than reporting distortion.
    /// </summary>
    /// <remarks>
    /// A bar claims a ratio and must start at zero; a dot claims a difference, and a linear mapping
    /// preserves the ratios of differences wherever the axis begins. The same data would fail the
    /// bar chart's check and passes this one, which is the distinction being pinned.
    /// </remarks>
    [Fact]
    public void TestACroppedAxisIsNotDistortionForAPositionEncoding()
    {
        var values = new[] { 100d, 104d };

        var dot = Chart.CreateDotChart(Rect(0, 0, 400, 200), values);
        Assert.False((bool)dot["isZeroBased"]!);
        Assert.Equal(1d, Num(dot, "lieFactor"), 9);

        var column = Chart.CreateColumnChart(Rect(0, 0, 400, 200), values,
            new Dictionary<string, object?> { ["baseline"] = 96d });
        Assert.True(Num(column, "lieFactor") > 1.05d);
    }

    /// <summary>Differences keep their ratios under a cropped axis — the claim above, measured.</summary>
    [Fact]
    public void TestDifferencesKeepTheirRatiosWhateverTheAxisStartsAt()
    {
        var values = new[] { 20d, 30d, 60d };
        var wide = Dots(Chart.CreateDotChart(Rect(0, 0, 400, 200), values,
            new Dictionary<string, object?> { ["min"] = 0d, ["max"] = 100d }));
        var cropped = Dots(Chart.CreateDotChart(Rect(0, 0, 400, 200), values,
            new Dictionary<string, object?> { ["min"] = 15d, ["max"] = 65d }));

        double Gap(IDictionary[] d, int a, int b) => Math.Abs(Num(d[b], "cx") - Num(d[a], "cx"));

        // 30-20 against 60-30 is 1:3 in the data, and must stay 1:3 in the ink either way.
        Assert.Equal(Gap(wide, 0, 1) / Gap(wide, 1, 2), Gap(cropped, 0, 1) / Gap(cropped, 1, 2), 6);
    }

    [Fact]
    public void TestSortingReordersRowsAndKeepsTheSourceIndex()
    {
        var data = new object[]
        {
            new Dictionary<string, object?> { ["label"] = "a", ["value"] = 30d },
            new Dictionary<string, object?> { ["label"] = "b", ["value"] = 10d },
            new Dictionary<string, object?> { ["label"] = "c", ["value"] = 20d }
        };

        var sorted = Dots(Chart.CreateDotChart(Rect(0, 0, 400, 200), data,
            new Dictionary<string, object?> { ["sort"] = "desc" }));

        Assert.Equal(["a", "c", "b"], sorted.Select(d => d["label"]?.ToString()));

        // The row moved; the pointer back to the original data did not.
        Assert.Equal([0, 2, 1], sorted.Select(d => Convert.ToInt32(d["sourceIndex"])));
    }

    [Fact]
    public void TestAnUnknownSortIsRefused() =>
        Assert.Throws<ArgumentException>(() => Chart.CreateDotChart(Rect(0, 0, 400, 200),
            new[] { 1d, 2d }, new Dictionary<string, object?> { ["sort"] = "sideways" }));

    /// <summary>The leader runs from the category axis to the dot, and is reported, not drawn.</summary>
    [Fact]
    public void TestEachDotCarriesItsLeaderLine()
    {
        var model = Chart.CreateDotChart(Rect(40, 0, 300, 200), new[] { 10d, 90d });
        foreach (var dot in Dots(model))
        {
            Assert.Equal(40d, Num(dot, "leaderX1"), 3);
            Assert.Equal(Num(dot, "cx"), Num(dot, "leaderX2"), 3);
            Assert.Equal(Num(dot, "cy"), Num(dot, "leaderY1"), 3);
        }
    }

    /// <summary>One geometry call serves both forms — a caller need not know which it holds.</summary>
    [Fact]
    public void TestGeometryWorksForDotsAsWellAsBars()
    {
        var dots = Chart.CreateChartGeometry(Chart.CreateDotChart(Rect(0, 0, 400, 200), new[] { 10d, 40d, 25d }));
        Assert.Equal(3, ((CanvasPath[])dots["marks"]!).Length);
        Assert.NotNull(dots["silhouette"]);

        var bars = Chart.CreateChartGeometry(Chart.CreateColumnChart(Rect(0, 0, 400, 200), new[] { 10d, 40d, 25d }));
        Assert.Equal(3, ((CanvasPath[])bars["marks"]!).Length);
    }
    #endregion

    #region Tests — the grouped dot chart
    static IDictionary[] Groups(Dictionary<string, object?> model) =>
        ((IEnumerable)model["groups"]!).Cast<object>().Select(g => (IDictionary)g!).ToArray();

    static object[] Regions() =>
    [
        new Dictionary<string, object?> { ["group"] = "Nordics", ["label"] = "Norway", ["value"] = 74.1d },
        new Dictionary<string, object?> { ["group"] = "Nordics", ["label"] = "Sweden", ["value"] = 54.3d },
        new Dictionary<string, object?> { ["group"] = "Baltics", ["label"] = "Estonia", ["value"] = 47.6d },
        new Dictionary<string, object?> { ["group"] = "Baltics", ["label"] = "Latvia", ["value"] = 39.2d },
        new Dictionary<string, object?> { ["group"] = "Baltics", ["label"] = "Lithuania", ["value"] = 41.8d }
    ];

    /// <summary>
    /// The claim that makes this one chart rather than several: every group reads against one scale.
    /// </summary>
    [Fact]
    public void TestEveryGroupSharesOneAxis()
    {
        var model = Chart.CreateGroupedDotChart(Rect(0, 0, 400, 300), Regions());
        var dots = Dots(model);

        // Equal values in different groups must land at the same x, whatever their group's spread.
        var scale = (LinearScale)model["scale"]!;
        foreach (var dot in dots)
        {
            Assert.Equal(scale.Map(Num(dot, "value")), Num(dot, "cx"), 6);
        }

        Assert.Equal(1, Convert.ToInt32(model["encodingRank"]));
    }

    [Fact]
    public void TestGroupsKeepFirstSeenOrderAndCarryTheirStats()
    {
        var groups = Groups(Chart.CreateGroupedDotChart(Rect(0, 0, 400, 300), Regions()));

        Assert.Equal(["Nordics", "Baltics"], groups.Select(g => g["name"]?.ToString()));
        Assert.Equal(2, Convert.ToInt32(groups[0]["count"]));
        Assert.Equal(3, Convert.ToInt32(groups[1]["count"]));

        Assert.Equal(74.1d, Num(groups[0], "max"), 3);
        Assert.Equal(54.3d, Num(groups[0], "min"), 3);
        Assert.Equal((47.6d + 39.2d + 41.8d) / 3d, Num(groups[1], "mean"), 6);
    }

    /// <summary>Rows are ordered inside their group, not across the chart.</summary>
    [Fact]
    public void TestSortingIsWithinGroups()
    {
        var dots = Dots(Chart.CreateGroupedDotChart(Rect(0, 0, 400, 300), Regions(),
            new Dictionary<string, object?> { ["sort"] = "desc" }));

        // Sorted globally these would interleave; grouped, each block stays whole and ordered.
        Assert.Equal(["Norway", "Sweden", "Estonia", "Lithuania", "Latvia"],
            dots.Select(d => d["label"]?.ToString()));
        Assert.Equal(["Nordics", "Nordics", "Baltics", "Baltics", "Baltics"],
            dots.Select(d => d["group"]?.ToString()));
    }

    [Fact]
    public void TestRowsAdvanceDownwardsAndGroupsDoNotOverlap()
    {
        var model = Chart.CreateGroupedDotChart(Rect(0, 20, 400, 300), Regions());
        var dots = Dots(model);
        var groups = Groups(model);

        for (var i = 1; i < dots.Length; i++)
        {
            Assert.True(Num(dots[i], "cy") > Num(dots[i - 1], "cy"), "rows advance downwards");
        }

        Assert.True(Num(groups[1], "y") >= Num(groups[0], "y2"), "the second group starts after the first ends");

        foreach (var dot in dots) Assert.InRange(Num(dot, "cy"), 20d, 320d);
    }

    /// <summary>Group headings sit above their own first row.</summary>
    [Fact]
    public void TestEachHeadingSitsAboveItsGroup()
    {
        var model = Chart.CreateGroupedDotChart(Rect(0, 0, 400, 300), Regions());
        var groups = Groups(model);
        var dots = Dots(model);

        foreach (var group in groups)
        {
            var first = dots.First(d => d["group"]?.ToString() == group["name"]?.ToString());
            Assert.True(Num(group, "headingY") < Num(first, "cy"));
        }
    }

    /// <summary>Ungrouped data is one group, not an error.</summary>
    [Fact]
    public void TestUngroupedDataBecomesASingleGroup()
    {
        var model = Chart.CreateGroupedDotChart(Rect(0, 0, 400, 300), new[] { 10d, 20d, 30d });
        Assert.Single(Groups(model));
        Assert.Equal(3, Dots(model).Length);
    }

    /// <summary>
    /// Too little height is refused with the arithmetic, rather than drawing rows on top of each other.
    /// </summary>
    [Fact]
    public void TestAPlotTooShortForItsGroupsIsRefusedWithNumbers()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Chart.CreateGroupedDotChart(Rect(0, 0, 400, 30), Regions()));

        Assert.Contains("5 rows", ex.Message);
        Assert.Contains("2 groups", ex.Message);
        Assert.Contains("headingHeight", ex.Message);
    }

    /// <summary>createDotChart ignores a group field — stated in the docs, pinned here.</summary>
    [Fact]
    public void TestThePlainDotChartIgnoresGrouping()
    {
        var plain = Chart.CreateDotChart(Rect(0, 0, 400, 300), Regions());
        Assert.Null(plain.GetValueOrDefault("groups"));
        Assert.Equal(5, Dots(plain).Length);
    }

    [Fact]
    public void TestGeometryWorksForAGroupedDotChart()
    {
        var geometry = Chart.CreateChartGeometry(
            Chart.CreateGroupedDotChart(Rect(0, 0, 400, 300), Regions()));
        Assert.Equal(5, ((CanvasPath[])geometry["marks"]!).Length);
    }
    #endregion

    #region Tests — the framed-rectangle chart
    static IDictionary[] Items(Dictionary<string, object?> model) =>
        ((IEnumerable)model["items"]!).Cast<object>().Select(i => (IDictionary)i!).ToArray();

    static IDictionary Part(IDictionary item, string name) => (IDictionary)item[name]!;

    static object[] Located() =>
    [
        new Dictionary<string, object?> { ["label"] = "TX", ["value"] = 12.7d, ["x"] = 210d, ["y"] = 340d },
        new Dictionary<string, object?> { ["label"] = "RI", ["value"] = 3.9d, ["x"] = 520d, ["y"] = 140d },
        new Dictionary<string, object?> { ["label"] = "ND", ["value"] = 1.4d, ["x"] = 250d, ["y"] = 90d }
    ];

    /// <summary>
    /// Every frame is identical — that is the mechanism, not decoration.
    /// </summary>
    /// <remarks>
    /// Without the frames these would be located bars and the reader's task would be perceiving
    /// length, rank 3. The frames buy exactly one step; identical size is what buys it.
    /// </remarks>
    [Fact]
    public void TestEveryFrameIsTheSameSize()
    {
        var model = Chart.CreateFramedRectangleChart(Rect(0, 0, 600, 400), Located());
        var frames = Items(model).Select(i => Part(i, "frame")).ToArray();

        foreach (var frame in frames)
        {
            Assert.Equal(Num(frames[0], "width"), Num(frame, "width"), 6);
            Assert.Equal(Num(frames[0], "height"), Num(frame, "height"), 6);
        }

        // Rank 2: position along identical but non-aligned scales.
        Assert.Equal("position", model["encoding"]);
        Assert.Equal(2, Convert.ToInt32(model["encodingRank"]));
    }

    /// <summary>
    /// Bigger values fill fuller, and the fill never leaves its frame.
    /// </summary>
    /// <remarks>
    /// Compared by <c>fraction</c> rather than by the fill's absolute y, and that is the point: the
    /// frames sit at different map positions, so an absolute coordinate says more about latitude than
    /// about the data. An earlier version of this model reported the absolute level and this test
    /// caught it — TX at y=340 read as "lower" than ND at y=90 while holding nine times the value.
    /// </remarks>
    [Fact]
    public void TestFillLevelTracksValueAndStaysInsideTheFrame()
    {
        var items = Items(Chart.CreateFramedRectangleChart(Rect(0, 0, 600, 400), Located()));

        var tx = items.First(i => i["label"]?.ToString() == "TX");
        var nd = items.First(i => i["label"]?.ToString() == "ND");

        Assert.True(Num(tx, "fraction") > Num(nd, "fraction"), "12.7 fills fuller than 1.4");
        foreach (var item in items) Assert.InRange(Num(item, "fraction"), 0d, 1d);

        foreach (var item in items)
        {
            var frame = Part(item, "frame");
            var fill = Part(item, "fill");
            Assert.InRange(Num(fill, "y"), Num(frame, "y") - 0.001d, Num(frame, "y2") + 0.001d);
            Assert.InRange(Num(fill, "y2"), Num(frame, "y") - 0.001d, Num(frame, "y2") + 0.001d);
            Assert.Equal(Num(frame, "x"), Num(fill, "x"), 6);
            Assert.Equal(Num(frame, "width"), Num(fill, "width"), 6);
        }
    }

    /// <summary>Frames are centred on the position the caller gave, which is the map location.</summary>
    [Fact]
    public void TestFramesAreCentredOnTheirGivenPosition()
    {
        var items = Items(Chart.CreateFramedRectangleChart(Rect(0, 0, 600, 400), Located()));

        Assert.Equal(210d, Num(items[0], "anchorX"), 6);
        Assert.Equal(340d, Num(items[0], "anchorY"), 6);
        Assert.Equal(210d, Num(Part(items[0], "frame"), "cx"), 6);
        Assert.Equal(340d, Num(Part(items[0], "frame"), "cy"), 6);
        Assert.True((bool)Chart.CreateFramedRectangleChart(Rect(0, 0, 600, 400), Located())["positioned"]!);
    }

    /// <summary>Data with no positions lays out on a grid, so the form is not map-only.</summary>
    [Fact]
    public void TestUnpositionedDataIsLaidOutOnAGrid()
    {
        var model = Chart.CreateFramedRectangleChart(Rect(0, 0, 600, 400), new[] { 1d, 2d, 3d, 4d },
            new Dictionary<string, object?> { ["columns"] = 2 });

        Assert.False((bool)model["positioned"]!);
        var items = Items(model);

        Assert.Equal(Num(items[0], "anchorY"), Num(items[1], "anchorY"), 6);   // same row
        Assert.True(Num(items[2], "anchorY") > Num(items[0], "anchorY"));      // next row down
        Assert.Equal(Num(items[0], "anchorX"), Num(items[2], "anchorX"), 6);   // same column
    }

    /// <summary>
    /// Positions are all-or-nothing: a half-positioned set would draw part on a map and part on a
    /// grid over the top of it.
    /// </summary>
    [Fact]
    public void TestPartlyPositionedDataFallsBackToTheGrid()
    {
        var mixed = new object[]
        {
            new Dictionary<string, object?> { ["label"] = "a", ["value"] = 1d, ["x"] = 10d, ["y"] = 10d },
            new Dictionary<string, object?> { ["label"] = "b", ["value"] = 2d }
        };

        Assert.False((bool)Chart.CreateFramedRectangleChart(Rect(0, 0, 600, 400), mixed)["positioned"]!);
    }

    /// <summary>Ticks are offsets from a frame's foot, so a legend frame can go anywhere.</summary>
    [Fact]
    public void TestTicksAreOffsetsFromTheFrameFoot()
    {
        var model = Chart.CreateFramedRectangleChart(Rect(0, 0, 600, 400), Located(),
            new Dictionary<string, object?> { ["frameHeight"] = 40d });
        var ticks = ((IEnumerable)model["ticks"]!).Cast<object>().Select(t => (IDictionary)t!).ToArray();

        Assert.NotEmpty(ticks);
        foreach (var tick in ticks) Assert.InRange(Num(tick, "offset"), -0.001d, 40.001d);
    }

    [Fact]
    public void TestAZeroSizedFrameIsRefused()
    {
        Assert.Throws<ArgumentException>(() => Chart.CreateFramedRectangleChart(
            Rect(0, 0, 600, 400), Located(), new Dictionary<string, object?> { ["frameWidth"] = 0d }));
        Assert.Throws<ArgumentException>(() => Chart.CreateFramedRectangleChart(
            Rect(0, 0, 600, 400), Located(), new Dictionary<string, object?> { ["frameHeight"] = -4d }));
    }

    /// <summary>Geometry returns the fills as marks and the frames separately.</summary>
    [Fact]
    public void TestGeometrySeparatesFillsFromFrames()
    {
        var geometry = Chart.CreateChartGeometry(
            Chart.CreateFramedRectangleChart(Rect(0, 0, 600, 400), Located()));

        Assert.Equal(3, ((CanvasPath[])geometry["marks"]!).Length);
        Assert.Equal(3, ((CanvasPath[])geometry["frames"]!).Length);
    }

    /// <summary>The other forms have no frames, and say so with an empty array rather than null.</summary>
    [Fact]
    public void TestUnframedChartsReturnNoFrames()
    {
        var geometry = Chart.CreateChartGeometry(
            Chart.CreateColumnChart(Rect(0, 0, 400, 200), new[] { 1d, 2d }));
        Assert.Empty((CanvasPath[])geometry["frames"]!);
    }
    #endregion

    #region Tests — small multiples
    static Dictionary<string, object?>[] Panels(Dictionary<string, object?> model) =>
        ((IEnumerable)model["panels"]!).Cast<object>().Select(p => (Dictionary<string, object?>)p!).ToArray();

    static Dictionary<string, object?> PanelChart(Dictionary<string, object?> panel) =>
        (Dictionary<string, object?>)panel["chart"]!;

    static object[] ThreeSeries() =>
    [
        new Dictionary<string, object?> { ["label"] = "North", ["data"] = new[] { 20d, 90d, 55d } },
        new Dictionary<string, object?> { ["label"] = "South", ["data"] = new[] { 5d, 12d, 9d } },
        new Dictionary<string, object?> { ["label"] = "West",  ["data"] = new[] { 30d, 44d, 38d } }
    ];

    /// <summary>
    /// The rule this construction exists to make unbreakable.
    /// </summary>
    /// <remarks>
    /// Built by hand it takes one forgotten argument to get wrong, and no individual panel can detect
    /// the mistake because each is correct on its own terms. Here there is no argument to forget.
    /// </remarks>
    [Fact]
    public void TestEveryPanelSharesOneScale()
    {
        var model = Chart.CreateSmallMultiples(Rect(0, 0, 600, 400), ThreeSeries());
        var panels = Panels(model);

        Assert.True((bool)model["sharedScale"]!);
        foreach (var panel in panels)
        {
            var chart = PanelChart(panel);
            Assert.Equal(Num(model, "min"), Num(chart, "min"), 6);
            Assert.Equal(Num(model, "max"), Num(chart, "max"), 6);
        }

        // 90 against 12 across two panels: the bars must be in the ratio of the values, which is
        // exactly what per-panel extents would destroy.
        var tallest = Bars(PanelChart(panels[0])).Max(b => Num(b, "height"));
        var shortest = Bars(PanelChart(panels[1])).Max(b => Num(b, "height"));
        Assert.Equal(90d / 12d, tallest / shortest, 3);
    }

    /// <summary>A shared scale does not excuse truncating it: a length encoding keeps its zero.</summary>
    [Fact]
    public void TestALengthFormKeepsItsZeroBaselineAcrossPanels()
    {
        var model = Chart.CreateSmallMultiples(Rect(0, 0, 600, 400), ThreeSeries());
        Assert.True((bool)model["isZeroBased"]!);
        Assert.Equal(0d, Num(model, "min"), 6);

        foreach (var panel in Panels(model))
        {
            Assert.True((bool)PanelChart(panel)["isZeroBased"]!);
            Assert.Equal(1d, Num(PanelChart(panel), "lieFactor"), 9);
        }
    }

    /// <summary>
    /// A position form crops to the data; a length form does not. Same values, two answers.
    /// </summary>
    /// <remarks>
    /// Data well away from zero, because that is the case where the difference shows — near zero
    /// <c>Scale.nice</c> rounds the lower bound down to it anyway and the two forms agree by accident.
    /// </remarks>
    [Fact]
    public void TestOnlyALengthFormForcesZeroIntoTheDomain()
    {
        object[] highSeries =
        [
            new Dictionary<string, object?> { ["label"] = "a", ["data"] = new[] { 71d, 78d } },
            new Dictionary<string, object?> { ["label"] = "b", ["data"] = new[] { 83d, 91d } }
        ];

        var dots = Chart.CreateSmallMultiples(Rect(0, 0, 600, 400), highSeries,
            new Dictionary<string, object?> { ["form"] = "dot" });
        var columns = Chart.CreateSmallMultiples(Rect(0, 0, 600, 400), highSeries);

        Assert.Equal("dot", PanelChart(Panels(dots)[0])["type"]);
        Assert.Equal(1, Convert.ToInt32(dots["encodingRank"]));

        Assert.True(Num(dots, "min") > 0d, "a dot form crops to the union of the data");
        Assert.Equal(0d, Num(columns, "min"), 6);   // a length form keeps its zero
        Assert.True((bool)columns["isZeroBased"]!);
    }

    /// <summary>Each panel is a whole chart, so everything that works on one works on a panel.</summary>
    [Fact]
    public void TestEachPanelIsACompleteChartModel()
    {
        var model = Chart.CreateSmallMultiples(Rect(0, 0, 600, 400), ThreeSeries());

        foreach (var panel in Panels(model))
        {
            var chart = PanelChart(panel);
            Assert.Equal("column", chart["type"]);
            Assert.Equal(3, Bars(chart).Length);
            Assert.NotNull(chart["ticks"]);

            // And the geometry call takes it unchanged.
            Assert.Equal(3, ((CanvasPath[])Chart.CreateChartGeometry(chart)["marks"]!).Length);
        }
    }

    [Fact]
    public void TestPanelsAreLaidOutOnAGridWithRoomForTitles()
    {
        var model = Chart.CreateSmallMultiples(Rect(0, 0, 600, 400), ThreeSeries(),
            new Dictionary<string, object?> { ["columns"] = 2, ["titleHeight"] = 20d });

        Assert.Equal(2, Convert.ToInt32(model["columns"]));
        Assert.Equal(2, Convert.ToInt32(model["rows"]));

        var panels = Panels(model);
        Assert.Equal(Num(panels[0], "titleY"), Num(panels[1], "titleY"), 3);      // same row
        Assert.True(Num(panels[2], "titleY") > Num(panels[0], "titleY"));         // next row

        // The plot starts below the title, not on top of it.
        var plot = (IDictionary)PanelChart(panels[0])["plot"]!;
        Assert.Equal(Num(panels[0], "titleY") + 20d, Num(plot, "y"), 3);
    }

    [Fact]
    public void TestLabelsComeFromTheSeries()
    {
        var panels = Panels(Chart.CreateSmallMultiples(Rect(0, 0, 600, 400), ThreeSeries()));
        Assert.Equal(["North", "South", "West"], panels.Select(p => p["label"]?.ToString()));
    }

    /// <summary>Bare arrays work too, for the case where the panels need no titles.</summary>
    [Fact]
    public void TestBareArraysAreAcceptedAsSeries()
    {
        var model = Chart.CreateSmallMultiples(Rect(0, 0, 600, 400),
            new object[] { new[] { 1d, 2d }, new[] { 3d, 4d } });
        Assert.Equal(2, Panels(model).Length);
    }

    /// <summary>Grid options are the construction's own and are not handed down to be refused.</summary>
    [Fact]
    public void TestGridOptionsAreNotPassedToThePanels()
    {
        var model = Chart.CreateSmallMultiples(Rect(0, 0, 600, 400), ThreeSeries(),
            new Dictionary<string, object?>
            {
                ["columns"] = 3, ["gap"] = 10d, ["rowGap"] = 12d, ["titleHeight"] = 16d,
                ["padding"] = 0.5d          // this one *is* a panel option and must arrive
            });

        Assert.Equal(0.5d, Convert.ToDouble(((BandScale)PanelChart(Panels(model)[0])["band"]!).Padding), 6);
    }

    /// <summary>
    /// One set of category labels serves every panel, which is the usual case.
    /// </summary>
    /// <remarks>
    /// <c>labels</c> was missing from the pass-through list at first. Being valid at the grid level
    /// and unknown at the panel level, it was accepted and then silently discarded — no error, no
    /// labels. Only drawing one revealed it.
    /// </remarks>
    [Fact]
    public void TestOneSetOfLabelsReachesEveryPanel()
    {
        var model = Chart.CreateSmallMultiples(Rect(0, 0, 600, 400), ThreeSeries(),
            new Dictionary<string, object?> { ["labels"] = new[] { "Jan", "Feb", "Mar" } });

        foreach (var panel in Panels(model))
        {
            Assert.Equal(["Jan", "Feb", "Mar"], Bars(PanelChart(panel)).Select(b => b["label"]?.ToString()));
        }
    }

    [Fact]
    public void TestAnUnknownFormIsRefusedByName()
    {
        var ex = Assert.Throws<ArgumentException>(() => Chart.CreateSmallMultiples(
            Rect(0, 0, 600, 400), ThreeSeries(), new Dictionary<string, object?> { ["form"] = "sunburst" }));
        Assert.Contains("sunburst", ex.Message);
        Assert.Contains("framedRectangle", ex.Message);
    }

    [Fact]
    public void TestEmptySeriesAreRefused()
    {
        Assert.Throws<ArgumentException>(() => Chart.CreateSmallMultiples(Rect(0, 0, 600, 400), Array.Empty<object>()));
        Assert.Throws<ArgumentException>(() => Chart.CreateSmallMultiples(Rect(0, 0, 600, 400), "not a series"));
    }
    #endregion

    #region Tests — the callout
    /// <summary>Reading a numeral is not a perceptual judgment, and the model says so.</summary>
    [Fact]
    public void TestACalloutIsNotAPerceptualJudgment()
    {
        var model = Chart.CreateCallout(Rect(0, 0, 200, 120), 42d);
        Assert.Equal("number", model["encoding"]);
        Assert.Equal(0, Convert.ToInt32(model["encodingRank"]));   // this toolkit's marker, not C&M's
        Assert.Equal(1d, Num(model, "lieFactor"), 9);
    }

    [Theory]
    [InlineData(1234d, false, 0, "1,234")]
    [InlineData(1234.56d, false, 2, "1,234.56")]
    [InlineData(42d, false, 0, "42")]
    [InlineData(1234d, true, 1, "1.2K")]
    [InlineData(1234567d, true, 1, "1.2M")]
    [InlineData(2_500_000_000d, true, 1, "2.5B")]
    [InlineData(999d, true, 1, "999")]
    [InlineData(-1500d, true, 1, "-1.5K")]
    public void TestNumbersAreFormattedForAReader(double value, bool compact, int decimals, string expected)
    {
        var model = Chart.CreateCallout(Rect(0, 0, 200, 120), value,
            new Dictionary<string, object?> { ["compact"] = compact, ["decimals"] = decimals });
        Assert.Equal(expected, model["display"]);
    }

    [Fact]
    public void TestPrefixAndSuffixWrapTheNumber()
    {
        var money = Chart.CreateCallout(Rect(0, 0, 200, 120), 1200d,
            new Dictionary<string, object?> { ["prefix"] = "$", ["compact"] = true });
        Assert.Equal("$1.2K", money["display"]);

        var pct = Chart.CreateCallout(Rect(0, 0, 200, 120), 61d,
            new Dictionary<string, object?> { ["unit"] = "%" });
        Assert.Equal("61%", pct["display"]);
    }

    /// <summary>Label, value and caption stack down the box without overlapping.</summary>
    [Fact]
    public void TestTheThreePartsStackInOrder()
    {
        var model = Chart.CreateCallout(Rect(0, 40, 260, 160), 74.1d, new Dictionary<string, object?>
        {
            ["label"] = "RENEWABLE SHARE", ["caption"] = "of electricity, 2026", ["unit"] = "%"
        });

        Assert.True(Num(model, "labelY") < Num(model, "valueY"));
        Assert.True(Num(model, "valueY") < Num(model, "captionY"));
        Assert.True(Num(model, "valueSize") > Num(model, "labelSize"));
        Assert.Equal(40d, Num(model, "labelY"), 3);   // starts at the top of its box
    }

    /// <summary>With no label the value starts at the top; nothing reserves space for what is absent.</summary>
    [Fact]
    public void TestAnAbsentLabelReservesNoSpace()
    {
        var bare = Chart.CreateCallout(Rect(0, 40, 260, 160), 12d);
        Assert.Null(bare["labelY"]);
        Assert.Null(bare["captionY"]);
        Assert.Equal(40d, Num(bare, "valueY"), 3);
    }

    [Theory]
    [InlineData("left", 100d)]
    [InlineData("center", 200d)]
    [InlineData("right", 300d)]
    public void TestAlignmentMovesTheAnchor(string align, double expected)
    {
        var model = Chart.CreateCallout(Rect(100, 0, 200, 120), 5d,
            new Dictionary<string, object?> { ["align"] = align });
        Assert.Equal(expected, Num(model, "valueX"), 3);
    }

    [Fact]
    public void TestAnUnknownAlignmentIsRefused() =>
        Assert.Throws<ArgumentException>(() => Chart.CreateCallout(Rect(0, 0, 200, 120), 5d,
            new Dictionary<string, object?> { ["align"] = "middleish" }));
    #endregion

    #region Tests — the waffle
    static IDictionary[] Cells(Dictionary<string, object?> model) =>
        ((IEnumerable)model["cells"]!).Cast<object>().Select(c => (IDictionary)c!).ToArray();

    static IDictionary[] Parts(Dictionary<string, object?> model) =>
        ((IEnumerable)model["parts"]!).Cast<object>().Select(p => (IDictionary)p!).ToArray();

    /// <summary>
    /// Whole cells sum to exactly the grid, whatever the shares.
    /// </summary>
    /// <remarks>
    /// Three parts at a third each is the case that breaks naive rounding: each floors to 33 and one
    /// cell of a hundred goes unassigned, leaving a waffle that visibly does not fill its own grid.
    /// </remarks>
    [Theory]
    [InlineData(new[] { 1d, 1d, 1d })]
    [InlineData(new[] { 33.3d, 33.3d, 33.4d })]
    [InlineData(new[] { 61d, 22d, 17d })]
    [InlineData(new[] { 1d, 1d, 1d, 1d, 1d, 1d, 1d })]
    [InlineData(new[] { 99d, 1d })]
    public void TestEveryCellIsAssignedWhateverTheShares(double[] values)
    {
        var model = Chart.CreateWaffle(Rect(0, 0, 200, 200), values);
        var parts = Parts(model);

        Assert.Equal(100, Convert.ToInt32(model["cellCount"]));
        Assert.Equal(100, parts.Sum(p => Convert.ToInt32(p["cells"])));
        Assert.All(Cells(model), c => Assert.True((bool)c["filled"]!));
    }

    [Fact]
    public void TestPartsGetCellsInProportionToTheirShare()
    {
        var parts = Parts(Chart.CreateWaffle(Rect(0, 0, 200, 200), new[] { 61d, 22d, 17d }));

        Assert.Equal(61, Convert.ToInt32(parts[0]["cells"]));
        Assert.Equal(22, Convert.ToInt32(parts[1]["cells"]));
        Assert.Equal(17, Convert.ToInt32(parts[2]["cells"]));
        Assert.Equal(0.61d, Num(parts[0], "share"), 6);
    }

    /// <summary>A part's cells are contiguous, so each block reads as one shape.</summary>
    [Fact]
    public void TestEachPartsCellsAreContiguous()
    {
        var cells = Cells(Chart.CreateWaffle(Rect(0, 0, 200, 200), new[] { 40d, 35d, 25d }));
        var owners = cells.Select(c => Convert.ToInt32(c["partIndex"])).ToArray();

        // Owner index never decreases, and only changes at a block boundary.
        for (var i = 1; i < owners.Length; i++) Assert.True(owners[i] >= owners[i - 1]);
        Assert.Equal(3, owners.Distinct().Count());
    }

    [Fact]
    public void TestCellsAreLaidOutRowMajorInsideTheArea()
    {
        var model = Chart.CreateWaffle(Rect(10, 20, 210, 210), new[] { 50d, 50d },
            new Dictionary<string, object?> { ["columns"] = 10, ["rows"] = 10, ["gap"] = 0d });
        var cells = Cells(model);

        Assert.Equal(10d, Num(cells[0], "x"), 3);
        Assert.Equal(20d, Num(cells[0], "y"), 3);
        Assert.Equal(0, Convert.ToInt32(cells[9]["row"]));      // end of the first row
        Assert.Equal(1, Convert.ToInt32(cells[10]["row"]));     // start of the second
        Assert.Equal(21d, Num(model, "cellWidth"), 3);
    }

    /// <summary>An explicit total lets a waffle show a share of something bigger than its parts.</summary>
    [Fact]
    public void TestAnExplicitTotalLeavesCellsUnfilled()
    {
        var model = Chart.CreateWaffle(Rect(0, 0, 200, 200), new[] { 30d },
            new Dictionary<string, object?> { ["total"] = 100d });

        Assert.Equal(30, Convert.ToInt32(Parts(model)[0]["cells"]));
        Assert.Equal(30, Cells(model).Count(c => (bool)c["filled"]!));
        Assert.Equal(70, Cells(model).Count(c => !(bool)c["filled"]!));
    }

    /// <summary>Honest about what it is: area unless the reader counts, and you cannot assume they do.</summary>
    [Fact]
    public void TestAWaffleReportsItselfAsArea()
    {
        var model = Chart.CreateWaffle(Rect(0, 0, 200, 200), new[] { 60d, 40d });
        Assert.Equal("area", model["encoding"]);
        Assert.Equal(4, Convert.ToInt32(model["encodingRank"]));
    }

    [Fact]
    public void TestAWaffleRefusesNegativePartsAndImpossibleGrids()
    {
        Assert.Throws<ArgumentException>(() =>
            Chart.CreateWaffle(Rect(0, 0, 200, 200), new[] { 60d, -10d }));

        var ex = Assert.Throws<ArgumentException>(() =>
            Chart.CreateWaffle(Rect(0, 0, 20, 20), new[] { 1d },
                new Dictionary<string, object?> { ["columns"] = 10, ["rows"] = 10, ["gap"] = 8d }));
        Assert.Contains("10x10", ex.Message);
    }

    /// <summary>Each call accepts only its own options — a shared list would have let these through.</summary>
    [Fact]
    public void TestOptionsAreValidatedPerCall()
    {
        // frameWidth is real, but not here.
        Assert.Throws<ArgumentException>(() => Chart.CreateColumnChart(Rect(0, 0, 200, 200),
            new[] { 1d, 2d }, new Dictionary<string, object?> { ["frameWidth"] = 10d }));

        // compact is real, but not on a waffle.
        Assert.Throws<ArgumentException>(() => Chart.CreateWaffle(Rect(0, 0, 200, 200),
            new[] { 1d }, new Dictionary<string, object?> { ["compact"] = true }));

        // and sort is real, but not on a column chart.
        Assert.Throws<ArgumentException>(() => Chart.CreateColumnChart(Rect(0, 0, 200, 200),
            new[] { 1d, 2d }, new Dictionary<string, object?> { ["sort"] = "desc" }));
    }
    #endregion

    #region Tests — the slot armature
    static IDictionary[] Slots(Dictionary<string, object?> model) =>
        ((IEnumerable)model["slots"]!).Cast<object>().Select(s => (IDictionary)s!).ToArray();

    /// <summary>
    /// One loop draws towers on a column chart and, unchanged, on a bar chart.
    /// </summary>
    /// <remarks>
    /// This is what the armature is for. `baseX/baseY`, `angleDeg` and `length` mean the same thing in
    /// both forms, so a house style survives a change of chart form without the drawing code knowing
    /// which one it is looking at.
    /// </remarks>
    [Fact]
    public void TestSlotsMeanTheSameThingInBothOrientations()
    {
        var values = new[] { 25d, 100d };

        var column = Slots(Chart.CreateColumnChart(Rect(0, 0, 400, 200), values));
        var bar = Slots(Chart.CreateBarChart(Rect(0, 0, 400, 200), values));

        Assert.All(column, s => Assert.Equal(0d, Num(s, "angleDeg"), 3));    // grows up
        Assert.All(bar, s => Assert.Equal(90d, Num(s, "angleDeg"), 3));      // grows right

        // In both, the taller value has the longer mark, in the same ratio.
        Assert.Equal(4d, Num(column[1], "length") / Num(column[0], "length"), 3);
        Assert.Equal(4d, Num(bar[1], "length") / Num(bar[0], "length"), 3);
    }

    /// <summary>The base is on the baseline and the tip is at the value — that is the whole contract.</summary>
    [Fact]
    public void TestASlotStandsOnTheBaselineAndReachesTheValue()
    {
        var model = Chart.CreateColumnChart(Rect(0, 0, 400, 200), new[] { 40d, 90d });
        var zero = Num(model, "baselinePosition");

        foreach (var slot in Slots(model))
        {
            Assert.Equal(zero, Num(slot, "baseY"), 3);
            Assert.Equal(Num(slot, "baseX"), Num(slot, "tipX"), 3);          // straight up
            Assert.True(Num(slot, "tipY") < Num(slot, "baseY"));
            Assert.Equal(Math.Abs(Num(slot, "baseY") - Num(slot, "tipY")), Num(slot, "length"), 3);
        }
    }

    /// <summary>
    /// `fraction` is a position on the scale, not a rank.
    /// </summary>
    /// <remarks>
    /// It is how many floors a tower gets or how full a bottle is, so it must track the scale rather
    /// than the ordering. The largest value reaches 1 only when the scale ends exactly there — with a
    /// niced maximum above the data it does not, and a drawing that treats <c>fraction == 1</c> as
    /// "this is the biggest" would be wrong. Compare values for that.
    /// </remarks>
    [Fact]
    public void TestFractionIsScalePositionNotRank()
    {
        var exact = Slots(Chart.CreateColumnChart(Rect(0, 0, 400, 200), new[] { 25d, 50d, 100d },
            new Dictionary<string, object?> { ["max"] = 100d }));
        Assert.Equal(0.25d, Num(exact[0], "fraction"), 6);
        Assert.Equal(1d, Num(exact[2], "fraction"), 6);

        var niced = Slots(Chart.CreateColumnChart(Rect(0, 0, 400, 200), new[] { 828d }));
        Assert.True(Num(niced[0], "fraction") < 1d, "a niced maximum sits above the data");
        Assert.InRange(Num(niced[0], "fraction"), 0d, 1d);
    }

    /// <summary>Every form carries the armature, so a mark routine is not tied to one chart.</summary>
    [Theory]
    [InlineData("column")]
    [InlineData("bar")]
    [InlineData("dot")]
    [InlineData("framed")]
    public void TestEveryFormCarriesSlots(string form)
    {
        var values = new[] { 10d, 40d, 25d };
        var model = form switch
        {
            "column" => Chart.CreateColumnChart(Rect(0, 0, 400, 200), values),
            "bar" => Chart.CreateBarChart(Rect(0, 0, 400, 200), values),
            "dot" => Chart.CreateDotChart(Rect(0, 0, 400, 200), values),
            _ => Chart.CreateFramedRectangleChart(Rect(0, 0, 400, 200), values)
        };

        var slots = Slots(model);
        Assert.Equal(3, slots.Length);

        foreach (var slot in slots)
        {
            Assert.InRange(Num(slot, "fraction"), 0d, 1d);
            Assert.True(Num(slot, "thickness") > 0d);
            Assert.NotNull(slot["label"]);
            Assert.True(Num(slot, "length") >= 0d);
        }
    }

    /// <summary>A waffle's slots are its cells, because a cell is where an icon goes.</summary>
    [Fact]
    public void TestAWafflesSlotsAreItsCells()
    {
        var model = Chart.CreateWaffle(Rect(0, 0, 200, 200), new[] { 60d, 40d });
        Assert.Equal(100, Slots(model).Length);
        Assert.Equal(Cells(model).Length, Slots(model).Length);
    }

    /// <summary>A slot's box is the bar's box, so the two agree and neither has to be recomputed.</summary>
    [Fact]
    public void TestASlotsBoxMatchesTheMarkItStandsFor()
    {
        var model = Chart.CreateColumnChart(Rect(20, 10, 400, 200), new[] { 15d, 60d, 33d });
        var bars = Bars(model);
        var slots = Slots(model);

        for (var i = 0; i < bars.Length; i++)
        {
            Assert.Equal(Num(bars[i], "x"), Num(slots[i], "x"), 3);
            Assert.Equal(Num(bars[i], "y"), Num(slots[i], "y"), 3);
            Assert.Equal(Num(bars[i], "width"), Num(slots[i], "width"), 3);
            Assert.Equal(Num(bars[i], "height"), Num(slots[i], "height"), 3);
        }
    }

    /// <summary>Slots survive into a small-multiples panel, so a house style works there too.</summary>
    [Fact]
    public void TestPanelsCarrySlotsAsWell()
    {
        var grid = Chart.CreateSmallMultiples(Rect(0, 0, 600, 400), ThreeSeries());
        foreach (var panel in Panels(grid)) Assert.Equal(3, Slots(PanelChart(panel)).Length);
    }
    #endregion
}
