namespace Polson.Tests.Drawing;

using System;
using System.Globalization;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// <c>Scale.*</c> — mapping values to pixels, and the quantitative rules that fail silently.
/// </summary>
/// <remarks>
/// The encoding tests matter more than the arithmetic ones. A chart with a non-zero baseline, a
/// circle sized by radius, or small multiples on per-panel scales is <i>correct in its numbers and
/// wrong in its picture</i> — nothing downstream can detect it, and whoever drew it has no reason to
/// look. These pin the rules Manual 13 states.
/// </remarks>
public class ScaleToolkitTests : TestsRuntime
{
    #region Methods
    private static object? Eval(string body)
    {
        var result = new JsDrawingEngine().Execute(body, 100, 100, null, "png", 100);
        Assert.True(result.Success, result.Error);
        return result.ReturnValue;
    }

    private static string Text(string body) => Eval(body)?.ToString() ?? string.Empty;

    private static float Number(string body) =>
        Convert.ToSingle(Eval(body), CultureInfo.InvariantCulture);
    #endregion

    #region Linear mapping
    /// <summary>A value maps proportionally across the range.</summary>
    [Fact]
    public void TestLinearMapsProportionally()
    {
        Assert.Equal(150f, Number("Scale.linear(0, 100, 100, 200).map(50);"), 3);
    }

    /// <summary>
    /// A reversed range is normal, not an error.
    /// </summary>
    /// <remarks>
    /// A vertical axis runs bottom-to-top: larger values at smaller y. If this did not work, every
    /// chart would need the caller to subtract from the plot height by hand, which is where sign
    /// errors live.
    /// </remarks>
    [Fact]
    public void TestAReversedRangePutsLargeValuesAtSmallCoordinates()
    {
        Assert.Equal("300,200,100", Text("""
            const y = Scale.linear(0, 100, 300, 100);
            [y.map(0), y.map(50), y.map(100)].map(Math.round).join(',');
            """));
    }

    /// <summary>Mapping is not clamped, so an outlier stays visible as one.</summary>
    [Fact]
    public void TestMappingIsNotClamped()
    {
        Assert.Equal("250,200", Text("""
            const s = Scale.linear(0, 100, 100, 200);
            [s.map(150), s.clamp(150)].map(Math.round).join(',');
            """));
    }

    /// <summary>Invert round-trips a mapped value.</summary>
    [Fact]
    public void TestInvertRoundTrips()
    {
        Assert.Equal(42f, Number("""
            const s = Scale.linear(-20, 80, 640, 40);
            s.invert(s.map(42));
            """), 2);
    }

    /// <summary>Extent is the positive pixel distance between two values.</summary>
    [Fact]
    public void TestExtentIsPositiveWhicheverWayTheRangeRuns()
    {
        Assert.Equal("120,120", Text("""
            const up = Scale.linear(0, 100, 300, 100);
            const down = Scale.linear(0, 100, 100, 300);
            [up.extent(0, 60), down.extent(0, 60)].map(Math.round).join(',');
            """));
    }

    /// <summary>A degenerate domain maps to the range start rather than dividing by zero.</summary>
    [Fact]
    public void TestADegenerateDomainDoesNotDivideByZero()
    {
        Assert.Equal(50f, Number("Scale.linear(7, 7, 50, 200).map(7);"), 3);
    }
    #endregion

    #region Ticks
    /// <summary>Ticks land on numbers a reader recognises.</summary>
    [Fact]
    public void TestTicksAreRoundNumbers()
    {
        Assert.Equal("0,20,40,60,80", Text("Scale.ticks(0, 95, 5).join(',');"));
    }

    /// <summary>Steps are 1, 2 or 5 times a power of ten, at any magnitude.</summary>
    [Fact]
    public void TestTickStepsAreOneTwoOrFiveTimesAPowerOfTen()
    {
        Assert.Equal("0,2000,4000,6000,8000,10000", Text("Scale.ticks(0, 10000, 5).join(',');"));
        Assert.Equal("0,0.2,0.4,0.6,0.8,1", Text("Scale.ticks(0, 1, 5).join(',');"));
    }

    /// <summary>
    /// Small steps do not accumulate float noise.
    /// </summary>
    /// <remarks>
    /// Ticks are drawn as labels, so <c>0.30000004</c> is not a rounding curiosity — it is what the
    /// reader sees on the axis.
    /// </remarks>
    [Fact]
    public void TestSmallTicksDoNotAccumulateFloatNoise()
    {
        Assert.Equal("0,0.1,0.2,0.3,0.4,0.5", Text("Scale.ticks(0, 0.5, 5).join(',');"));
    }

    /// <summary>Nice bounds widen outward so the end ticks sit at the plot's edges.</summary>
    [Fact]
    public void TestNiceWidensOutward()
    {
        Assert.Equal("0,100", Text("""
            const b = Scale.nice(3, 92, 5);
            [b.min, b.max].join(',');
            """));
    }

    /// <summary>An empty or equal interval degrades rather than throwing.</summary>
    [Fact]
    public void TestDegenerateIntervalsDegradeQuietly()
    {
        Assert.Equal("1", Text("Scale.ticks(5, 5).length.toString();"));
        Assert.Equal("0", Text("Scale.extent([]).span.toString();"));
    }
    #endregion

    #region Encoding rules
    /// <summary>
    /// A zero baseline is detectable, because a bar chart without one lies.
    /// </summary>
    /// <remarks>
    /// The classic distortion: truncating the axis makes a 4% difference fill half the panel. The
    /// numbers are right, the picture is not, and nothing but this flag can tell.
    /// </remarks>
    [Fact]
    public void TestZeroBaselineIsDetectable()
    {
        Assert.Equal("true,false", Text("""
            [Scale.linear(0, 100, 0, 200).isZeroBased,
             Scale.linear(80, 100, 0, 200).isZeroBased].join(',');
            """));
    }

    /// <summary>
    /// Radius follows the square root, so area carries the value.
    /// </summary>
    /// <remarks>
    /// Four times the value must be twice the radius — not four times, which would be sixteen times
    /// the ink.
    /// </remarks>
    [Fact]
    public void TestRadiusEncodesAreaNotLength()
    {
        Assert.Equal("50,100", Text("""
            [Scale.radiusFor(25, 100, 100), Scale.radiusFor(100, 100, 100)].map(Math.round).join(',');
            """));
    }

    /// <summary>Area really is proportional to value, which is the property that matters.</summary>
    [Fact]
    public void TestAreaIsProportionalToValue()
    {
        Assert.Equal("4", Text("""
            const small = Scale.radiusFor(20, 80, 60);
            const big = Scale.radiusFor(80, 80, 60);
            ((big * big) / (small * small)).toFixed(0);
            """));
    }

    /// <summary>A zero or negative maximum yields no radius rather than NaN.</summary>
    [Fact]
    public void TestRadiusHandlesAnEmptyMaximum()
    {
        Assert.Equal(0f, Number("Scale.radiusFor(10, 0, 50);"), 3);
    }

    /// <summary>
    /// One extent over every series is what makes small multiples comparable.
    /// </summary>
    /// <remarks>
    /// Two panels scaled to their own data look alike and are not. The shared extent is the fix,
    /// and this asserts the shape a caller actually uses: flatten every series, take one interval.
    /// </remarks>
    [Fact]
    public void TestSharedExtentSpansEverySeries()
    {
        Assert.Equal("3,88", Text("""
            const a = [12, 40, 3];
            const b = [55, 88, 61];
            const shared = Scale.extent(a.concat(b));
            [shared.min, shared.max].join(',');
            """));
    }
    #endregion

    #region Bands
    /// <summary>Bands divide the range with their gaps taken out of it.</summary>
    [Fact]
    public void TestBandsFitTheirRange()
    {
        Assert.Equal("fits", Text("""
            const b = Scale.band(5, 0, 500, 0.2);
            const last = b.map(4) + b.bandwidth;
            (b.map(0) >= 0 && last <= 500.01) ? 'fits' : 'overflows: ' + last;
            """));
    }

    /// <summary>Bandwidth reflects the padding.</summary>
    [Fact]
    public void TestPaddingNarrowsTheBand()
    {
        Assert.Equal("100,80,50", Text("""
            [Scale.band(5, 0, 500, 0).bandwidth,
             Scale.band(5, 0, 500, 0.2).bandwidth,
             Scale.band(5, 0, 500, 0.5).bandwidth].map(Math.round).join(',');
            """));
    }

    /// <summary>A band's centre is where its label goes.</summary>
    [Fact]
    public void TestCentreIsMidBand()
    {
        Assert.Equal("50,150,250,350,450", Text("""
            const b = Scale.band(5, 0, 500, 0.2);
            [0,1,2,3,4].map(i => Math.round(b.center(i))).join(',');
            """));
    }

    /// <summary>Zero categories degrade rather than dividing by zero.</summary>
    [Fact]
    public void TestZeroBandsDoesNotDivideByZero()
    {
        Assert.Equal("0,0", Text("""
            const b = Scale.band(0, 0, 400);
            [b.bandwidth, b.step].join(',');
            """));
    }
    #endregion
}
