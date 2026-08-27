namespace Polson.Tests.Drawing;

using Polson.MCPServer;
using SkiaSharp;
using Xunit;

/// <summary>
/// Canvas2D path semantics that negative-space marks depend on. Both behaviours below used to fail
/// silently — the shape still rendered, just solid — which is the worst way for a logo toolkit to be
/// wrong, so these assert on actual pixels rather than on the call succeeding.
/// </summary>
public class CanvasPathSemanticsTests : TestsRuntime
{
    #region Fill Rule Tests
    [Fact]
    public void TestEvenOddFillRuleCutsACounter()
    {
        // Two concentric circles wound the same way: non-zero unions them into a disc,
        // even-odd leaves a ring. This is the case that distinguishes the two rules.
        var solid = Centre(Render("c.fill();"));
        var ring = Centre(Render("c.fill('evenodd');"));

        Assert.Equal(Ink, solid);
        Assert.Equal(Paper, ring);
    }

    [Fact]
    public void TestNonZeroIsTheDefaultAndCanBeNamed()
    {
        Assert.Equal(Ink, Centre(Render("c.fill();")));
        Assert.Equal(Ink, Centre(Render("c.fill('nonzero');")));
    }

    [Fact]
    public void TestFillRuleIsCaseInsensitive()
    {
        Assert.Equal(Paper, Centre(Render("c.fill('EvenOdd');")));
    }
    #endregion

    #region Arc Contour Tests
    [Fact]
    public void TestFullCircleArcAppendedToAPathStillDraws()
    {
        // Skia's ArcTo collapses a 360-degree sweep, so before the fix a circle appended
        // after a moveTo vanished entirely and took its counter with it.
        var png = Draw("""
            c.beginPath();
            c.moveTo(300, 300);
            c.arc(100, 100, 40, 0, Math.PI * 2);
            c.fill();
            """);

        Assert.Equal(Ink, PixelAt(png, 100, 100));
    }

    [Fact]
    public void TestPartialArcStillConnectsToTheCurrentPoint()
    {
        // The full-sweep special case must not change ordinary arcs, which per the Canvas
        // spec draw a line from the current point to the arc's start.
        var png = Draw("""
            c.beginPath();
            c.moveTo(40, 100);
            c.arc(100, 100, 40, Math.PI, 0);
            c.closePath();
            c.fill();
            """);

        Assert.Equal(Ink, PixelAt(png, 100, 80));
    }
    #endregion

    #region Methods
    private static SKColor Ink { get; } = new(0x1f, 0x3b, 0x57);

    private static SKColor Paper { get; } = new(0xff, 0xff, 0xff);

    /// <summary>Outer circle plus a same-direction inner circle, then the caller's fill call.</summary>
    private static byte[] Render(string fillCall) => Draw($"""
        c.beginPath();
        c.arc(100, 100, 80, 0, Math.PI * 2);
        c.moveTo(140, 100);
        c.arc(100, 100, 40, 0, Math.PI * 2);
        {fillCall}
        """);

    private static byte[] Draw(string body)
    {
        var result = new JsDrawingEngine().Execute($"""
            const cv = createCanvas(200, 200);
            const c = cv.getContext('2d');
            c.fillStyle = '#ffffff';
            c.fillRect(0, 0, 200, 200);
            c.fillStyle = '#1f3b57';
            {body}
            cv;
            """, 200, 200, null, "png", 100);

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.ImageBytes);
        return result.ImageBytes!;
    }

    private static SKColor Centre(byte[] png) => PixelAt(png, 100, 100);

    private static SKColor PixelAt(byte[] png, int x, int y)
    {
        using var bitmap = SKBitmap.Decode(png);
        var pixel = bitmap.GetPixel(x, y);
        return new SKColor(pixel.Red, pixel.Green, pixel.Blue);
    }
    #endregion
}
