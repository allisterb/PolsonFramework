namespace Polson.Tests.MCPServer;

using Polson.MCPServer;
using Xunit;

/// <summary>
/// canvas.bitmap handed scripts the raw SKBitmap rather than the documented SkiaBitmapWrapper, so
/// canvas.bitmap.getPixel() reached SkiaSharp's own method and returned an SKColor struct: an
/// object rather than a string, printing as #AARRGGBB instead of the documented #RRGGBBAA, and
/// never equal to anything under ===. getPixel is sold in the reference as the way to verify a
/// render, so this made the verification primitive silently report correct renders as wrong.
/// </summary>
public class CanvasBitmapSurfaceTests : TestsRuntime
{
    #region Methods
    [Theory]
    [InlineData("c.bitmap")]
    [InlineData("c.toBitmap()")]
    [InlineData("Skia.Bitmap.create(32, 32)")]
    public void TestGetPixelReturnsARealString(string bitmapExpr)
    {
        var result = Run($"""
            const c = createCanvas(32, 32);
            const px = ({bitmapExpr}).getPixel(1, 1);
            log('typeof=' + (typeof px));
            log('len=' + px.length);
            log('lower=' + px.toLowerCase());
            """);

        Assert.True(result.Success, result.Error);
        var log = string.Join("\n", result.Logs);
        Assert.Contains("typeof=string", log);
        Assert.Contains("len=9", log);
    }

    /// <summary>The documented channel order is #RRGGBBAA, and it must hold on every path.</summary>
    [Theory]
    [InlineData("c.bitmap")]
    [InlineData("c.toBitmap()")]
    public void TestGetPixelUsesDocumentedChannelOrder(string bitmapExpr)
    {
        var result = Run($"""
            const c = createCanvas(32, 32);
            const ctx = c.getContext('2d');
            ctx.fillStyle = '#3366CC';
            ctx.fillRect(0, 0, 32, 32);
            log('px=' + ({bitmapExpr}).getPixel(16, 16));
            """);

        Assert.True(result.Success, result.Error);
        Assert.Contains("px=#3366CCFF", string.Join("\n", result.Logs));
    }

    /// <summary>The obvious way to verify a render is ===, and it must work.</summary>
    [Fact]
    public void TestGetPixelIsStrictlyComparable()
    {
        var result = Run("""
            const c = createCanvas(32, 32);
            const ctx = c.getContext('2d');
            ctx.fillStyle = '#000000';
            ctx.fillRect(0, 0, 32, 32);
            log('eq=' + (c.bitmap.getPixel(16, 16) === '#000000FF'));
            """);

        Assert.True(result.Success, result.Error);
        Assert.Contains("eq=true", string.Join("\n", result.Logs));
    }

    /// <summary>canvas.bitmap is live; canvas.toBitmap() is a snapshot. Both are documented that way.</summary>
    [Fact]
    public void TestBitmapIsLiveAndToBitmapIsACopy()
    {
        var result = Run("""
            const c = createCanvas(32, 32);
            const ctx = c.getContext('2d');
            ctx.fillStyle = '#000000';
            ctx.fillRect(0, 0, 32, 32);

            const live = c.bitmap;
            const snapshot = c.toBitmap();

            ctx.fillStyle = '#FF0000';
            ctx.fillRect(0, 0, 32, 32);

            log('live=' + live.getPixel(16, 16));
            log('snapshot=' + snapshot.getPixel(16, 16));
            """);

        Assert.True(result.Success, result.Error);
        var log = string.Join("\n", result.Logs);
        Assert.Contains("live=#FF0000FF", log);
        Assert.Contains("snapshot=#000000FF", log);
    }
    #endregion

    private static DrawingExecutionResult Run(string script) =>
        new JsDrawingEngine().Execute(script, 32, 32, null, "png", 90);
}
