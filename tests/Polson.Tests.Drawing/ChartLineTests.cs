namespace Polson.Tests.Drawing;

using System;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// <c>Chart.createLineChart</c> — a series joined into a trajectory.
/// </summary>
/// <remarks>
/// <para>
/// <b>Added because its absence was doing damage, and the tests are shaped by that.</b> An Apollo
/// descent-profile run searched the toolkit for a line form, found none among the thirteen, and
/// hand-built the trajectory from quadratic curves whose control point took the previous x with the
/// new y — a rounded step, which between two telemetry samples asserts the vehicle dropped and then
/// held flat. The same run called <c>Scale.checkSeries</c> first, so the SDK shipped the integrity
/// check for a form it could not draw.
/// </para>
/// <para>
/// So the cases here are mostly about what the form <b>refuses</b> and what it <b>asserts</b>, not
/// about geometry: the failure that prompted it produced correct numbers and a lying mark.
/// </para>
/// </remarks>
public class ChartLineTests : TestsRuntime
{
    #region Model Tests
    [Fact]
    public void TestTheModelReportsItsEncodingAndIntegrity()
    {
        var result = Execute("""
            const c = Chart.createLineChart(Layout.rect(0, 0, 400, 200), [10, 20, 15, 30]);
            log(c.type + ',' + c.encoding + ',' + c.encodingRank + ',' + c.lieFactor + ',' + c.points.length);
            """);

        Assert.Equal("line,position,1,1,4", Logged(result));
    }

    /// <summary>
    /// Points sit at their own <c>x</c> where the data has one, not at even intervals.
    /// </summary>
    /// <remarks>
    /// The distortion this prevents is quiet: telemetry sampled at 0, 26, 156 and 480 seconds drawn
    /// at four equal steps shows a constant rate of change that never happened.
    /// </remarks>
    [Fact]
    public void TestPointsSitAtTheirOwnPositions()
    {
        var result = Execute("""
            const c = Chart.createLineChart(Layout.rect(0, 0, 400, 200),
                [{ x: 0, value: 1 }, { x: 10, value: 2 }, { x: 100, value: 3 }]);
            // The middle sample is a tenth of the way along, so it must sit near the left.
            const mid = c.points[1].x, span = c.points[2].x - c.points[0].x;
            log(String(Math.abs((mid - c.points[0].x) / span - 0.1) < 0.01));
            """);

        Assert.Equal("true", Logged(result));
    }

    /// <summary>Without an <c>x</c> the points are evenly spaced by index.</summary>
    [Fact]
    public void TestPlainValuesAreEvenlySpaced()
    {
        var result = Execute("""
            const c = Chart.createLineChart(Layout.rect(0, 0, 400, 200), [5, 6, 7, 8]);
            const gaps = [];
            for (let i = 1; i < c.points.length; i++) gaps.push(c.points[i].x - c.points[i-1].x);
            log(String(Math.max(...gaps) - Math.min(...gaps) < 0.01));
            """);

        Assert.Equal("true", Logged(result));
    }
    #endregion

    #region Integrity Tests
    /// <summary>
    /// A series that cannot honestly be joined is refused, not drawn.
    /// </summary>
    /// <remarks>
    /// Neither fault is a data error — every value is correct and the picture still lies — so nothing
    /// downstream would catch it. Measured on a live run: two chips at 2024 made the line spike to 208
    /// billion and fall back to 28 inside one tick, and the audit recorded "monotonic scaling
    /// preserved".
    /// </remarks>
    [Theory]
    [InlineData("[{x:2024,value:28},{x:2024,value:208},{x:2025,value:300}]", "more than one value")]
    [InlineData("[{x:0,value:1},{x:5,value:2},{x:3,value:3}]", "")]
    public void TestASeriesThatCannotBeJoinedIsRefused(string data, string expected)
    {
        var result = Run($"Chart.createLineChart(Layout.rect(0, 0, 400, 200), {data});");

        Assert.False(result.Success);
        Assert.Contains("A line asserts one value at each position", result.Error, StringComparison.Ordinal);
        if (expected.Length > 0) Assert.Contains(expected, result.Error, StringComparison.Ordinal);
    }

    /// <summary>One point is a callout, not a trajectory.</summary>
    [Fact]
    public void TestASinglePointIsRefused()
    {
        var result = Run("Chart.createLineChart(Layout.rect(0, 0, 400, 200), [42]);");

        Assert.False(result.Success);
        Assert.Contains("at least two points", result.Error, StringComparison.Ordinal);
        Assert.Contains("createCallout", result.Error, StringComparison.Ordinal);
    }

    /// <summary>
    /// There is no spline, and asking for one says why rather than silently drawing straight.
    /// </summary>
    /// <remarks>
    /// A smooth curve through sampled points draws values nobody measured and cannot be checked
    /// against anything. Refusing by name is what stops a caller assuming the option was honoured.
    /// </remarks>
    [Fact]
    public void TestASplineIsRefusedByName()
    {
        var result = Run("Chart.createLineChart(Layout.rect(0, 0, 400, 200), [1,2,3], { curve: 'catmullRom' });");

        Assert.False(result.Success);
        Assert.Contains("no spline", result.Error, StringComparison.Ordinal);
        Assert.Contains("nobody measured", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void TestAMisspelledOptionIsRefused()
    {
        var result = Run("Chart.createLineChart(Layout.rect(0, 0, 400, 200), [1,2,3], { aera: true });");

        Assert.False(result.Success);
        Assert.Contains("aera", result.Error, StringComparison.Ordinal);
    }
    #endregion

    #region Area Tests
    /// <summary>
    /// Filling the area changes the claim from position to length, so the baseline is forced in.
    /// </summary>
    /// <remarks>
    /// A line's value is read as position, so cropping the axis is fine and <c>lieFactor</c> is 1.
    /// The moment the area is filled the height <i>is</i> the quantity — a bar's claim — and a cropped
    /// baseline overstates every difference.
    /// </remarks>
    [Fact]
    public void TestAnAreaForcesTheBaselineIntoTheDomain()
    {
        var result = Execute("""
            const plain = Chart.createLineChart(Layout.rect(0, 0, 400, 200), [100, 104, 102]);
            const filled = Chart.createLineChart(Layout.rect(0, 0, 400, 200), [100, 104, 102], { area: true });
            log(String(plain.isZeroBased) + ',' + String(filled.isZeroBased) + ',' + filled.lieFactor);
            """);

        // The plain line may crop to 100–104; the filled one must reach the baseline, and once it does
        // the ink is proportional again.
        Assert.Equal("false,true,1", Logged(result));
    }

    [Fact]
    public void TestTheAreaPathClosesOnTheBaseline()
    {
        var result = Execute("""
            const c = Chart.createLineChart(Layout.rect(0, 0, 400, 200), [1, 2, 3], { area: true });
            log(String(c.areaPath !== null && c.areaPath.indexOf('Z') > 0) + ',' + String(
                Chart.createLineChart(Layout.rect(0, 0, 400, 200), [1, 2, 3]).areaPath === null));
            """);

        Assert.Equal("true,true", Logged(result));
    }
    #endregion

    #region Curve Tests
    /// <summary>The default path is straight segments — one <c>L</c> per point after the first.</summary>
    [Fact]
    public void TestTheDefaultPathIsStraightSegments()
    {
        var result = Execute("""
            const c = Chart.createLineChart(Layout.rect(0, 0, 400, 200), [1, 2, 3, 4]);
            const ls = (c.path.match(/L/g) || []).length;
            log(c.curve + ',' + ls + ',' + String(c.path.indexOf('Q') < 0 && c.path.indexOf('C') < 0));
            """);

        Assert.Equal("linear,3,true", Logged(result));
    }

    /// <summary>A step holds then jumps: two segments per interval, and no curve commands.</summary>
    [Fact]
    public void TestAStepHoldsThenJumps()
    {
        var result = Execute("""
            const c = Chart.createLineChart(Layout.rect(0, 0, 400, 200), [1, 2, 3, 4], { curve: 'step' });
            const ls = (c.path.match(/L/g) || []).length;
            log(c.curve + ',' + ls + ',' + String(c.path.indexOf('Q') < 0));
            """);

        Assert.Equal("step,6,true", Logged(result));
    }
    #endregion

    #region Armature Tests
    /// <summary>
    /// Every point carries a slot, so a custom mark can replace the default circle.
    /// </summary>
    /// <remarks>
    /// The slot means the same thing here as in every other form: <c>baseX</c>/<c>baseY</c> on the
    /// baseline, <c>tipX</c>/<c>tipY</c> at the measured point, and <c>fraction</c> as its place on
    /// the scale — so a mark routine written for a column chart works unchanged, and a mark can be
    /// grown from the axis or placed at the point.
    /// </remarks>
    [Fact]
    public void TestEveryPointCarriesASlotWithBaseAndTip()
    {
        var result = Execute("""
            const c = Chart.createLineChart(Layout.rect(0, 0, 400, 200), [10, 20, 30], { area: true });
            const s = c.slots[2], p = c.points[2];
            log(c.slots.length + ',' + String(Math.abs(s.tipY - p.y) < 0.01)
              + ',' + String(Math.abs(s.baseY - c.baselinePosition) < 0.01)
              + ',' + String(s.fraction > 0 && s.fraction <= 1));
            """);

        Assert.Equal("3,true,true,true", Logged(result));
    }

    /// <summary>The vector surface draws it, rather than falling through to the slots fallback.</summary>
    /// <remarks>
    /// The fallback draws a rectangle per slot, which for a line is a row of bars — a picture that
    /// looks like a chart and is not the one asked for. That failure has happened before on this
    /// dispatch, with <c>progressMeter</c>.
    /// </remarks>
    [Fact]
    public void TestTheVectorSurfaceDrawsTheTrajectory()
    {
        var result = Execute("""
            const paper = Snap(400, 200);
            const c = Chart.createLineChart(Layout.inset(Layout.rect(0, 0, 400, 200), 20), [10, 40, 20, 50]);
            paper.chart(c, { fill: '#1f6f8b' });
            const xml = paper.toString();
            // A stroked path, not a run of rectangles.
            log(String(xml.indexOf('<path') > 0) + ',' + String((xml.match(/<rect/g) || []).length === 0));
            """);

        Assert.Equal("true,true", Logged(result));
    }
    #endregion

    #region Methods
    private static DrawingExecutionResult Run(string body) =>
        new JsDrawingEngine().Execute(body, 400, 200, null, "png", 100, render: false);

    private static DrawingExecutionResult Execute(string body)
    {
        var result = Run(body);
        Assert.True(result.Success, result.Error);
        return result;
    }

    private static string Logged(DrawingExecutionResult result) =>
        string.Join(" ", result.Logs).Replace("[LOG]", string.Empty, StringComparison.Ordinal).Trim();
    #endregion
}
