namespace Polson.Tests.MCPServer;

using System;
using System.Linq;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// The claims Manual 18 makes about the globals that nothing else pins.
/// </summary>
/// <remarks>
/// <see cref="StageContextTests"/> already covers <c>Stage</c> thoroughly, and the console levels and
/// <c>table</c> forms are exercised in the drawing tests. What was untested is the part a manual has
/// to state as a warning: the easing curves are <b>not</b> bounded to <c>0…1</c>, and a script using
/// one to distribute marks along an interval will place some of them outside it. Documented in Manual
/// 18 §8 with measured values, so the values need a test or the table rots.
/// </remarks>
public class RunRecordTests : TestsRuntime
{
    #region Tests
    /// <summary>The five curves a distribution can rely on stay inside the unit interval.</summary>
    [Theory]
    [InlineData("linear")]
    [InlineData("easein")]
    [InlineData("easeout")]
    [InlineData("easeinout")]
    [InlineData("bounce")]
    public void TestABoundedCurveStaysWithinTheUnitInterval(string curve)
    {
        var result = Run($$"""
            let low = 0, high = 1;
            for (let i = 0; i <= 40; i++) {
                const v = mina.{{curve}}(i / 40);
                if (v < low) low = v;
                if (v > high) high = v;
            }
            log(low.toFixed(4) + ' ' + high.toFixed(4));
            """);

        Assert.True(result.Success, result.Error);
        var parts = result.Logs[0].Replace("[LOG] ", "").Split(' ');
        Assert.Equal(0, double.Parse(parts[0]));
        Assert.Equal(1, double.Parse(parts[1]));
    }

    /// <summary>
    /// The three that overshoot, which is the trap: they are useful precisely because they leave the
    /// interval, and a caller who does not know that gets marks outside the box.
    /// </summary>
    [Theory]
    [InlineData("backin")]
    [InlineData("backout")]
    [InlineData("elastic")]
    public void TestAnOvershootingCurveLeavesTheUnitInterval(string curve)
    {
        var result = Run($$"""
            let outside = false;
            for (let i = 0; i <= 40; i++) {
                const v = mina.{{curve}}(i / 40);
                if (v < 0 || v > 1) outside = true;
            }
            log('outside=' + outside);
            """);

        Assert.True(result.Success, result.Error);
        Assert.Contains("outside=true", result.Logs[0]);
    }

    /// <summary>Every curve still starts at 0 and ends at 1, overshoot or not.</summary>
    [Theory]
    [InlineData("linear")]
    [InlineData("easein")]
    [InlineData("easeout")]
    [InlineData("easeinout")]
    [InlineData("bounce")]
    [InlineData("backin")]
    [InlineData("backout")]
    [InlineData("elastic")]
    public void TestEveryCurveRunsFromZeroToOne(string curve)
    {
        var result = Run($"log(mina.{curve}(0).toFixed(3) + ' ' + mina.{curve}(1).toFixed(3));");

        Assert.True(result.Success, result.Error);
        Assert.Contains("0.000 1.000", result.Logs[0]);
    }

    /// <summary>
    /// <c>console.clear()</c> discards what came before it rather than merely separating it — the
    /// difference decides whether a diagnostic pass reaches the caller.
    /// </summary>
    [Fact]
    public void TestConsoleClearDiscardsEarlierLines()
    {
        var result = Run("console.log('before'); console.clear(); console.log('after');");

        Assert.True(result.Success, result.Error);
        Assert.DoesNotContain(result.Logs, l => l.Contains("before", StringComparison.Ordinal));
        Assert.Contains(result.Logs, l => l.Contains("after", StringComparison.Ordinal));
    }

    /// <summary>
    /// <c>console.info</c> is its own level. The reference called it an alias for <c>console.log</c>,
    /// which it never was — the two are distinguishable in the log and a reader can act on that.
    /// </summary>
    [Fact]
    public void TestConsoleInfoIsItsOwnLevel()
    {
        var result = Run("console.log('a'); console.info('b');");

        Assert.True(result.Success, result.Error);
        Assert.Contains("[LOG] a", result.Logs);
        Assert.Contains("[INFO] b", result.Logs);
    }

    /// <summary>
    /// <c>exit</c> succeeds, keeps what came before, and runs nothing after — including the render,
    /// which is why exiting before drawing produces no image.
    /// </summary>
    [Fact]
    public void TestExitStopsEverythingAfterIt()
    {
        var result = Run("""
            log('kept');
            exit('stopped on purpose');
            log('never runs');
            const c = createCanvas(20, 20);
            c.getContext('2d').fillRect(0, 0, 20, 20);
            c;
            """);

        Assert.True(result.Success, result.Error);
        Assert.Equal("stopped on purpose", result.ReturnValue);
        Assert.Contains(result.Logs, l => l.Contains("kept", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Logs, l => l.Contains("never runs", StringComparison.Ordinal));
        Assert.Contains(result.Logs, l => l.Contains("[EXIT]", StringComparison.Ordinal));
        Assert.True(result.ImageBytes is null or { Length: 0 },
            "a script that exits before drawing must not produce an image");
    }
    #endregion

    #region Methods (private)
    private static DrawingExecutionResult Run(string script) =>
        new JsDrawingEngine().Execute(script, 20, 20, null, "png", 90);
    #endregion
}
