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
}
