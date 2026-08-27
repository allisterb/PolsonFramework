namespace Polson.Tests.Drawing;

using Polson.MCPServer;
using SkiaSharp;
using Xunit;

/// <summary>
/// The Stage 6 verification suites. The monochrome board used to vary only its panel backgrounds, so a
/// mark that set its own colours came out identical in all four and the "1-COLOR BLACK" / "KNOCKOUT
/// WHITE" labels claimed more than the board delivered. These tests drive it with a deliberately
/// full-colour mark and assert the ink actually changes.
/// </summary>
public class LogoVerificationSuiteTests : TestsRuntime
{
    #region Constants
    private const int Width = 1000;
    private const int Height = 640;

    /// <summary>The colour the mark hardcodes, which no panel should show.</summary>
    private static readonly SKColor MarkOwnColour = new(0x2f, 0x7f, 0xd4);
    #endregion

    #region Monochrome Board Tests
    [Fact]
    public void TestMonochromeBoardForcesTheMarkToOneInkPerPanel()
    {
        var board = RenderBoard();

        Assert.Equal(new SKColor(0x11, 0x18, 0x27), PixelAt(board, 250, 160));   // positive
        Assert.Equal(new SKColor(0xff, 0xff, 0xff), PixelAt(board, 750, 160));   // knockout
        Assert.Equal(new SKColor(0x4b, 0x55, 0x63), PixelAt(board, 250, 480));   // greyscale
    }

    [Fact]
    public void TestMonochromeBoardDiscardsTheMarksOwnColour()
    {
        var board = RenderBoard();

        foreach (var (x, y) in new[] { (250, 160), (750, 160), (250, 480), (750, 480) })
        {
            Assert.NotEqual(MarkOwnColour, PixelAt(board, x, y));
        }
    }

    [Fact]
    public void TestMonochromeBoardKeepsTheMarkShape()
    {
        // The knockout replaces hue while preserving alpha, so the silhouette must survive intact:
        // ink inside the mark, panel background just outside it.
        var board = RenderBoard();

        Assert.Equal(new SKColor(0x11, 0x18, 0x27), PixelAt(board, 250, 160));
        Assert.Equal(new SKColor(0xff, 0xff, 0xff), PixelAt(board, 40, 160));
    }
    #endregion

    #region Methods
    /// <summary>A mark that hardcodes its colour — the case the board has to defeat.</summary>
    private static byte[] RenderBoard()
    {
        var result = new JsDrawingEngine().Execute($$"""
            const canvas = createCanvas({{Width}}, {{Height}});
            const ctx = canvas.getContext('2d');
            const drawMark = (c, size) => {
                c.fillStyle = '#2f7fd4';
                c.fillRect(0, 0, size, size);
            };
            Logo.generateMonochromeTest(ctx, drawMark, {{Width}}, {{Height}});
            canvas;
            """, Width, Height, null, "png", 100);

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.ImageBytes);
        return result.ImageBytes!;
    }

    private static SKColor PixelAt(byte[] png, int x, int y)
    {
        using var bitmap = SKBitmap.Decode(png);
        var pixel = bitmap.GetPixel(x, y);
        return new SKColor(pixel.Red, pixel.Green, pixel.Blue);
    }
    #endregion
}
