namespace Polson.Tests.MCPServer;

using Polson.MCPServer;
using SkiaSharp;
using Xunit;

/// <summary>
/// The raster peek must be the same shape as the vector deliverable.
/// </summary>
/// <remarks>
/// <c>ExecuteScript</c>'s <c>width</c>/<c>height</c> are the <b>default viewport</b> — the size a
/// script gets when it calls <c>Snap()</c> or <c>createCanvas()</c> with no arguments. They were
/// also being passed as the <b>render target</b> for a returned paper, so a script that asked for
/// <c>Snap(900, 1350)</c> had its picture squeezed into 800x600. The scale is non-uniform
/// (<c>scaleX</c> and <c>scaleY</c> are computed separately), so the peek was not letterboxed but
/// anamorphically distorted: 0.89x across and 0.44x down.
/// <para>
/// Measured on the kubrick7 run — <c>final.svg</c> 900x1350 portrait, <c>final.webp</c> 800x600
/// landscape. The agent audits by looking at the raster, so every visual check in that run was made
/// against a picture that was not the deliverable, and the crowding it saw was half of it the
/// squash. The canvas branch never had this fault: it renders at the canvas's own size, and the
/// paper branch is now consistent with it.
/// </para>
/// </remarks>
public class PaperRenderSizeTests : TestsRuntime
{
    #region Methods
    /// <summary>Runs a script with a deliberately different default viewport, and measures the render.</summary>
    private static (int Width, int Height) RenderedSize(string script)
    {
        var result = new JsDrawingEngine().Execute(script, 800, 600, null, "png", 100);
        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.ImageBytes);
        using var bitmap = SKBitmap.Decode(result.ImageBytes);
        Assert.NotNull(bitmap);
        return (bitmap.Width, bitmap.Height);
    }
    #endregion

    #region Tests
    /// <summary>The live failure: a portrait deliverable, peeked at in landscape.</summary>
    [Fact]
    public void TestAPortraitPaperRendersPortraitRatherThanAtTheDefaultViewport()
    {
        var size = RenderedSize("""
            const paper = Snap(900, 1350);
            paper.rect(0, 0, 900, 1350).attr({ fill: '#1c2733' });
            paper;
            """);

        Assert.Equal((900, 1350), size);
    }

    /// <summary>A paper returned through an element takes the same route and must size the same way.</summary>
    [Fact]
    public void TestAPaperReachedThroughAReturnedElementAlsoRendersAtItsOwnSize()
    {
        var size = RenderedSize("""
            const paper = Snap(640, 960);
            paper.rect(0, 0, 640, 960).attr({ fill: '#1f6f8b' });
            """);

        Assert.Equal((640, 960), size);
    }

    /// <summary>
    /// Content drawn outside the paper must not resize the render, or the peek and the deliverable
    /// disagree again — this time by however far a stray mark strayed.
    /// </summary>
    [Fact]
    public void TestContentOverflowingThePaperDoesNotChangeTheRenderedSize()
    {
        var size = RenderedSize("""
            const paper = Snap(400, 500);
            paper.rect(0, 0, 400, 500).attr({ fill: '#faf8f4' });
            paper.rect(-120, 620, 900, 200).attr({ fill: '#c9553d' });   // well outside the viewport
            paper;
            """);

        Assert.Equal((400, 500), size);
    }

    /// <summary>The default viewport still applies to a script that asks for no size of its own.</summary>
    [Fact]
    public void TestAPaperWithNoStatedSizeStillTakesTheDefaultViewport()
    {
        var size = RenderedSize("""
            const paper = Snap();
            paper.rect(0, 0, 40, 40).attr({ fill: '#15151a' });
            paper;
            """);

        Assert.Equal((800, 600), size);
    }

    /// <summary>A canvas was never affected; asserted so the two branches stay consistent.</summary>
    [Fact]
    public void TestACanvasStillRendersAtItsOwnSize()
    {
        var size = RenderedSize("""
            const canvas = createCanvas(300, 700);
            const ctx = canvas.getContext('2d');
            ctx.fillStyle = '#1c2733';
            ctx.fillRect(0, 0, 300, 700);
            canvas;
            """);

        Assert.Equal((300, 700), size);
    }

    /// <summary>The same for a paper saved on the way out of an <c>exit(...)</c>.</summary>
    [Fact]
    public void TestAPaperRenderedAfterExitAlsoKeepsItsOwnSize()
    {
        var size = RenderedSize("""
            const paper = Snap(500, 900);
            paper.rect(0, 0, 500, 900).attr({ fill: '#5fb49c' });
            exit('measured');
            """);

        Assert.Equal((500, 900), size);
    }
    #endregion
}
