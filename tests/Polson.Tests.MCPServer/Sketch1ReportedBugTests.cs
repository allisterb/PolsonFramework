namespace Polson.Tests.MCPServer;

using System.Linq;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// Defects reported by the <c>sketch1</c> agent run (a two-figure gesture sketch, 2026-09-26), each re-checked against
/// the build before it was fixed. Every one was silent or misleading where it should have been loud.
/// </summary>
public class Sketch1ReportedBugTests : TestsRuntime
{
    #region Lists
    /// <summary>
    /// <c>concat</c> appended a returned list whole, as one element, so the result was the right sort of length and
    /// full of <c>undefined</c>. Spread already worked; concat now agrees with it.
    /// </summary>
    [Fact]
    public void ConcatSpreadsAListTheSdkReturned()
    {
        var result = Execute("""
            const found = Drawing.findTangents(Drawing.createFigureGeometry(Drawing.createMannequinFigure(200, 20, 340)));
            const n = found.tangents.length;
            const joined = found.tangents.concat(found.tangents);
            const mixed = [1].concat(found.tangents, [2]);
            log('n ' + n + ' joined ' + joined.length + ' kinds ' + joined.map(t => t.kind).join(','));
            log('mixed ' + mixed.length + ' first ' + mixed[0] + ' second ' + mixed[1].kind + ' last ' + mixed[mixed.length - 1]);
            log('plain ' + [1, 2].concat([3], 4).join(','));
            """);

        Assert.True(result.Success, result.Error);
        var logs = string.Join("\n", result.Logs);
        var n = int.Parse(System.Text.RegularExpressions.Regex.Match(logs, @"n (\d+)").Groups[1].Value);
        Assert.True(n > 0, "the standing mannequin should report its tangents, so there is a list to join");
        Assert.Contains($"joined {2 * n} ", logs);
        Assert.DoesNotContain("undefined", logs);
        Assert.Contains($"mixed {n + 2} first 1 second ", logs);
        Assert.Contains("plain 1,2,3,4", logs);
    }
    #endregion

    #region Pose keys
    /// <summary>An invented pose key was accepted and did nothing, so the figure quietly was not the pose written.</summary>
    [Theory]
    [InlineData("{ pose: { headTurnDeg: -25 } }", "headTurnDeg", "neckDeg")]
    [InlineData("{ pose: { leftArm: { elbowDegg: 30 } } }", "elbowDegg", "signed")]
    [InlineData("{ pose: { SpineDeg: 10 } }", "SpineDeg", "Did you mean 'spineDeg'")]
    [InlineData("{ pose: { rightLeg: { kneeDeg: 10, ankleDeg: 5 } } }", "ankleDeg", "kneeDeg")]
    [InlineData("{ poses: { spineDeg: 10 } }", "poses", "pose")]
    public void AnUnknownPoseKeyIsRefusedByName(string options, string named, string hint)
    {
        var result = Execute($"Drawing.createMannequinFigure(200, 20, 340, {options});");

        Assert.False(result.Success);
        Assert.Contains(named, result.Error);
        Assert.Contains(hint, result.Error);
    }

    [Fact]
    public void EveryDocumentedPoseKeyIsStillAccepted()
    {
        var result = Execute("""
            const f = Drawing.createMannequinFigure(200, 20, 340, { shoulderTiltDeg: -4, pelvicTiltDeg: 4, spineOffset: 6, shoulderSpanHeads: 2,
                pose: { spineDeg: 5, neckDeg: -5, lineOfAction: { shape: 'C', turnDeg: 20 },
                        leftArm: { shoulderDeg: 120, elbowDeg: -20 }, rightArm: { shoulderDeg: 60, elbowDeg: 20 },
                        leftLeg: { hipDeg: 100, kneeDeg: 10 }, rightLeg: { hipDeg: 80, kneeDeg: -10 } } });
            log('ok ' + f.lineOfAction.swing.toFixed(2));
            """);

        Assert.True(result.Success, result.Error);
    }
    #endregion

    #region ctx.filter
    /// <summary><c>ctx.filter</c> took only an SKImageFilter, so the web's own spelling failed with a cast error.</summary>
    [Fact]
    public void FilterTakesACssString()
    {
        var result = Execute("""
            ctx.filter = 'grayscale(1)';
            ctx.fillStyle = '#ff0000';
            ctx.fillRect(0, 0, 100, 100);
            ctx.filter = 'none';
            ctx.fillRect(200, 0, 100, 100);
            ctx.filter = 'blur(6px) brightness(120%) drop-shadow(2px 3px 4px rgba(0,0,0,.5))';
            ctx.fillRect(100, 200, 100, 100);
            log('grey ' + c.bitmap.getPixel(50, 50) + ' red ' + c.bitmap.getPixel(250, 50) + ' soft ' + c.bitmap.getPixel(100, 250));
            """);

        Assert.True(result.Success, result.Error);
        var logs = string.Join("\n", result.Logs);
        Assert.Matches(@"grey #(?<v>[0-9A-F]{2})\k<v>\k<v>FF", logs);
        Assert.Contains("red #FF0000FF", logs);
        Assert.DoesNotMatch(@"soft #FF0000FF", logs);
    }

    [Theory]
    [InlineData("'sharpen(2)'", "sharpen")]
    [InlineData("'blur(two)'", "two")]
    [InlineData("'hue-rotate(90)'", "deg")]
    public void AFilterThisCannotHonourIsRefusedByName(string value, string named)
    {
        var result = Execute($"ctx.filter = {value};");

        Assert.False(result.Success);
        Assert.Contains(named, result.Error);
    }

    [Fact]
    public void FilterStillTakesAnImageFilter()
    {
        var result = Execute("ctx.filter = Skia.ImageFilter.blur(3, 3); ctx.fillRect(10, 10, 50, 50); ctx.filter = null;");

        Assert.True(result.Success, result.Error);
    }
    #endregion

    #region Checks
    /// <summary>
    /// A check's verdict reached only the run record, so a failing check was invisible to the agent until the run
    /// was over. It now comes back in the logs, in order, and as a summary carrying the failures.
    /// </summary>
    [Fact]
    public void ChecksComeBackInTheResponse()
    {
        var result = Execute("""
            log('before');
            Stage.check('accent under 15%', true, 'measured 0.11');
            Stage.check('rope is a steep diagonal', false, 'measured 5 deg');
            Stage.check('no tangents', true);
            log('after');
            """);

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.Checks);
        Assert.Equal(2, result.Checks!.Passed);
        Assert.Equal(1, result.Checks.Failed);
        var failure = Assert.Single(result.Checks.Failures);
        Assert.Equal("rope is a steep diagonal", failure.Claim);
        Assert.Equal("measured 5 deg", failure.Detail);

        var logs = result.Logs.ToList();
        var fail = logs.FindIndex(l => l.Contains("[CHECK] FAIL rope is a steep diagonal - measured 5 deg"));
        Assert.True(fail > logs.FindIndex(l => l.Contains("before")) && fail < logs.FindIndex(l => l.Contains("after")),
            "the check should sit in the logs where it was made");
    }

    [Fact]
    public void AScriptWithNoChecksHasNoSummary()
    {
        var result = Execute("log('nothing measured');");

        Assert.True(result.Success, result.Error);
        Assert.Null(result.Checks);
        var web = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        Assert.DoesNotContain("\"checks\"", System.Text.Json.JsonSerializer.Serialize(result, web));

        var checkedResult = Execute("Stage.check('one', false, 'measured 0');");
        Assert.Contains("\"checks\":{\"passed\":0,\"failed\":1", System.Text.Json.JsonSerializer.Serialize(checkedResult, web));
    }
    #endregion

    #region Private
    private static DrawingExecutionResult Execute(string body) =>
        new JsDrawingEngine().Execute($"const c = createCanvas(400, 400); const ctx = c.getContext('2d'); {body} c;", 400, 400, null, "png", 90);
    #endregion
}
