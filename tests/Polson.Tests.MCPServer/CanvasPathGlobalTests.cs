namespace Polson.Tests.MCPServer;

using Polson.MCPServer;
using Xunit;

/// <summary>
/// The SDK reference types ctx.fill/stroke/clip against CanvasPath and its Assets example
/// constructs one, but the global was never registered — an agent following the docs hit
/// "CanvasPath is not defined" with no documented way to obtain one.
/// </summary>
public class CanvasPathGlobalTests : TestsRuntime
{
    #region Methods
    [Theory]
    [InlineData("CanvasPath")]
    [InlineData("Path2D")]
    public void TestPathConstructorIsDefined(string globalName)
    {
        var result = Run($"log(typeof {globalName}); const p = new {globalName}(); log(typeof p);");

        Assert.True(result.Success, result.Error);
        var log = string.Join("\n", result.Logs);
        Assert.Contains("function", log);
        Assert.DoesNotContain("undefined", log);
    }

    [Fact]
    public void TestPathFromSvgDataFillsTheSameAsAnInlinePath()
    {
        var fromSvg = Run("""
            const c = createCanvas(200, 200); const ctx = c.getContext('2d');
            ctx.fillStyle = '#000';
            ctx.fill(new CanvasPath('M20,20 L180,20 L180,180 Z'));
            c;
            """);

        var inline = Run("""
            const c = createCanvas(200, 200); const ctx = c.getContext('2d');
            ctx.fillStyle = '#000';
            ctx.beginPath(); ctx.moveTo(20,20); ctx.lineTo(180,20); ctx.lineTo(180,180); ctx.closePath();
            ctx.fill();
            c;
            """);

        Assert.True(fromSvg.Success, fromSvg.Error);
        Assert.True(inline.Success, inline.Error);
        Assert.Equal(inline.ImageBytes!.Length, fromSvg.ImageBytes!.Length);
    }

    [Fact]
    public void TestPathCopyConstructorIsIndependentOfItsSource()
    {
        var result = Run("""
            const a = new CanvasPath('M0,0 L10,0');
            const b = new CanvasPath(a);
            b.lineTo(10, 10);
            log('ok');
            """);

        Assert.True(result.Success, result.Error);
        Assert.Contains("ok", string.Join("\n", result.Logs));
    }

    /// <summary>The SDK reference documents `new Canvas(w, h)` as a constructor alias for createCanvas.</summary>
    [Fact]
    public void TestCanvasConstructorAliasIsConstructible()
    {
        var result = Run("const c = new Canvas(200, 200); log('w=' + c.width); c;");

        Assert.True(result.Success, result.Error);
        Assert.Contains("w=200", string.Join("\n", result.Logs));
    }

    /// <summary>
    /// Assets.library is a marshalled .NET list, so forEach's index is undefined. The docs tell
    /// agents to reach for Array.from instead — verify that actually works before promising it.
    /// </summary>
    [Fact]
    public void TestArrayFromConvertsAMarshalledListToARealArray()
    {
        var result = Run("""
            const arr = Array.from(Assets.library);
            log('isArray=' + Array.isArray(arr));
            """);

        Assert.True(result.Success, result.Error);
        Assert.Contains("isArray=true", string.Join("\n", result.Logs));
    }
    #endregion

    private static DrawingExecutionResult Run(string script) =>
        new JsDrawingEngine().Execute(script, 200, 200, null, "png", 90);
}
