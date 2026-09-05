namespace Polson.Tests.Drawing;

using System;
using System.Linq;
using Polson.Drawing.Svg;
using Svg;
using SkiaSharp;
using Xunit;

/// <summary>
/// Rendering an SVG that carries SMIL animation at a chosen time.
/// </summary>
/// <remarks>
/// <para>
/// The failure these exist to prevent is silent. Before <c>atTime</c>, <see cref="SvgRenderPipeline"/>
/// loaded a document and read its picture without ever seeking the clock, so an animated document
/// rendered its opening frame — successfully, with no error and no warning. That looks exactly like a
/// still drawing, which is the worst way for a feature to be missing.
/// </para>
/// <para>
/// Svg.Skia 5.2.1 carries the animation engine (<c>Svg.Animation</c>); these tests pin the parts of it
/// the pipeline depends on, so an upstream bump that changed the timing model would fail here rather
/// than quietly producing frozen frames.
/// </para>
/// </remarks>
public class SvgAnimationTests : TestsRuntime
{
    #region Fields
    /// <summary>An 80px square translating 0 → 400px over four seconds, on a 600×200 board.</summary>
    private const string Moving = """
        <svg xmlns="http://www.w3.org/2000/svg" width="600" height="200" viewBox="0 0 600 200">
          <rect width="600" height="200" fill="#ffffff"/>
          <rect id="mover" x="20" y="60" width="80" height="80" fill="#000000">
            <animateTransform attributeName="transform" type="translate"
                              from="0 0" to="400 0" dur="4s" fill="freeze"/>
          </rect>
        </svg>
        """;

    private const string Still = """
        <svg xmlns="http://www.w3.org/2000/svg" width="600" height="200" viewBox="0 0 600 200">
          <rect width="600" height="200" fill="#ffffff"/>
          <rect x="20" y="60" width="80" height="80" fill="#000000"/>
        </svg>
        """;
    #endregion

    #region Methods
    /// <summary>Leftmost column carrying ink, which is where the square currently is.</summary>
    private static int InkLeft(SKBitmap bitmap)
    {
        for (var x = 0; x < bitmap.Width; x++)
        {
            for (var y = 0; y < bitmap.Height; y++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Alpha > 200 && p.Red < 100 && p.Green < 100 && p.Blue < 100) return x;
            }
        }

        return -1;
    }

    private static int InkLeftAt(TimeSpan? atTime)
    {
        using var bitmap = SvgRenderPipeline.RenderToBitmap(
            SvgDocument.FromSvg<SvgDocument>(Moving), 600, 200, SKColors.White, atTime);
        return InkLeft(bitmap);
    }

    [Fact]
    public void TestAnAnimatedDocumentIsRecognisedAsOne()
    {
        Assert.True(SvgRenderPipeline.HasAnimations(Moving));
        Assert.False(SvgRenderPipeline.HasAnimations(Still));
    }

    /// <summary>The frame asked for is the frame drawn.</summary>
    /// <remarks>
    /// 400px over four seconds is 100px per second, and the square starts at x = 20 — so the
    /// expected positions are exact rather than approximate, and a timing model that drifted would
    /// show up as a wrong number rather than as a vague difference.
    /// </remarks>
    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 120)]
    [InlineData(2, 220)]
    [InlineData(3, 320)]
    [InlineData(4, 420)]
    public void TestTheFrameAskedForIsTheFrameDrawn(int seconds, int expectedLeft)
    {
        Assert.Equal(expectedLeft, InkLeftAt(TimeSpan.FromSeconds(seconds)));
    }

    /// <summary>Omitting the time renders the opening frame, exactly as it always did.</summary>
    [Fact]
    public void TestOmittingTheTimeRendersTheOpeningFrame()
    {
        Assert.Equal(20, InkLeftAt(null));
        Assert.Equal(InkLeftAt(TimeSpan.Zero), InkLeftAt(null));
    }

    /// <summary>Seeking is absolute, so frames may be rendered in any order.</summary>
    /// <remarks>
    /// This is the property the whole feature rests on. If the clock advanced relatively, or carried
    /// state between seeks, a caller could not render frame 40 before frame 10 — and a renderer that
    /// has to produce frames in order cannot be parallelised or resumed.
    /// </remarks>
    [Fact]
    public void TestSeekingIsAbsoluteAndOrderIndependent()
    {
        var ascending = Enumerable.Range(0, 5).Select(s => InkLeftAt(TimeSpan.FromSeconds(s))).ToArray();
        var descending = Enumerable.Range(0, 5).Reverse().Select(s => InkLeftAt(TimeSpan.FromSeconds(s))).Reverse().ToArray();

        Assert.Equal(ascending, descending);
        Assert.Equal(new[] { 20, 120, 220, 320, 420 }, ascending);
    }

    /// <summary>Sub-second times interpolate rather than snapping to a whole frame.</summary>
    [Fact]
    public void TestFractionalTimesInterpolate()
    {
        Assert.Equal(70, InkLeftAt(TimeSpan.FromSeconds(0.5)));
        Assert.Equal(45, InkLeftAt(TimeSpan.FromSeconds(0.25)));
    }

    /// <summary>A time past the end holds the frozen value rather than wrapping or clearing.</summary>
    [Fact]
    public void TestPastTheEndTheFrozenValueHolds()
    {
        Assert.Equal(420, InkLeftAt(TimeSpan.FromSeconds(10)));
    }

    /// <summary>Asking a still document for a time is harmless.</summary>
    /// <remarks>
    /// The seek is skipped when there are no animations, so this must render identically to the same
    /// document with no time at all — a caller sweeping a time range over mixed documents should not
    /// have to know which of them animate.
    /// </remarks>
    [Fact]
    public void TestATimeOnAStillDocumentChangesNothing()
    {
        var withTime = SvgRenderPipeline.RenderToImage(Still, 600, 200, "png", 100, SKColors.White, TimeSpan.FromSeconds(3));
        var without = SvgRenderPipeline.RenderToImage(Still, 600, 200, "png", 100, SKColors.White);

        Assert.NotEmpty(withTime);
        Assert.Equal(without, withTime);
    }

    /// <summary>The encoded path honours the time too, not only the bitmap one.</summary>
    [Fact]
    public void TestTheEncodedImagePathAlsoHonoursTheTime()
    {
        var first = SvgRenderPipeline.RenderToImage(Moving, 600, 200, "png", 100, SKColors.White, TimeSpan.Zero);
        var later = SvgRenderPipeline.RenderToImage(Moving, 600, 200, "png", 100, SKColors.White, TimeSpan.FromSeconds(4));

        Assert.NotEmpty(first);
        Assert.NotEmpty(later);
        Assert.NotEqual(first, later);
    }
    #endregion
}
