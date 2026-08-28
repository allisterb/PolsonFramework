namespace Polson.Tests.MCPServer;

using Polson.MCPServer;
using Xunit;

/// <summary>
/// The SDK reference promises that for a 2D canvas script, result.SvgXml "retains the last vector
/// image produced by the agent prior to switching to 2D canvas mode". That is what lets one script
/// deliver both a vector master and a raster presentation via outSvg + outFile — and because the
/// MCP tool only writes outSvg when SvgXml is non-empty, a regression here fails silently.
/// </summary>
public class SvgRetentionTests : TestsRuntime
{
    #region Methods
    [Fact]
    public void TestCanvasReturningScriptRetainsTheVectorImage()
    {
        var result = Run("""
            const paper = Snap(200, 200);
            paper.circle(100, 100, 60).attr({ fill: '#0af' });
            const c = createCanvas(200, 200);
            const ctx = c.getContext('2d');
            ctx.fillStyle = '#111'; ctx.fillRect(0, 0, 200, 200);
            c;
            """);

        Assert.True(result.Success, result.Error);
        Assert.False(string.IsNullOrWhiteSpace(result.SvgXml), "SvgXml was empty, so outSvg would write no file");
        Assert.Contains("circle", result.SvgXml);
        Assert.NotNull(result.ImageBytes);
    }

    /// <summary>Returning the context rather than the canvas must behave the same.</summary>
    [Fact]
    public void TestContextReturningScriptRetainsTheVectorImage()
    {
        var result = Run("""
            const paper = Snap(200, 200);
            paper.rect(20, 20, 160, 160).attr({ fill: '#f0a' });
            const ctx = createCanvas(200, 200).getContext('2d');
            ctx.fillStyle = '#111'; ctx.fillRect(0, 0, 200, 200);
            ctx;
            """);

        Assert.True(result.Success, result.Error);
        Assert.Contains("rect", result.SvgXml);
    }

    /// <summary>A script that exits early keeps the same guarantee.</summary>
    [Fact]
    public void TestExitPathRetainsTheVectorImage()
    {
        var result = Run("""
            const paper = Snap(200, 200);
            paper.circle(100, 100, 40).attr({ fill: '#0af' });
            const c = createCanvas(200, 200);
            c.getContext('2d').fillRect(0, 0, 200, 200);
            exit('done');
            """);

        Assert.True(result.Success, result.Error);
        Assert.Contains("circle", result.SvgXml);
    }

    /// <summary>Returning a paper must still render that paper, not a stray canvas.</summary>
    [Fact]
    public void TestPaperReturningScriptIsUnchanged()
    {
        var result = Run("""
            const paper = Snap(200, 200);
            paper.circle(100, 100, 60).attr({ fill: '#0af' });
            paper;
            """);

        Assert.True(result.Success, result.Error);
        Assert.Contains("circle", result.SvgXml);
        Assert.NotNull(result.ImageBytes);
    }

    /// <summary>A pure raster script has no vector image to retain, and must not invent one.</summary>
    [Fact]
    public void TestPureRasterScriptHasNoSvg()
    {
        var result = Run("""
            const c = createCanvas(200, 200);
            c.getContext('2d').fillRect(0, 0, 200, 200);
            c;
            """);

        Assert.True(result.Success, result.Error);
        Assert.True(string.IsNullOrWhiteSpace(result.SvgXml), $"expected no SVG, got: {result.SvgXml}");
    }
    #endregion

    private static DrawingExecutionResult Run(string script) =>
        new JsDrawingEngine().Execute(script, 200, 200, null, "png", 90);
}
