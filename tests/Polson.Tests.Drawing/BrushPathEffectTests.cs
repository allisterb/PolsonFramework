namespace Polson.Tests.Drawing;

using System;
using Polson.MCPServer;
using SkiaSharp;
using Xunit;

/// <summary>
/// The mid-level stroke primitives: stamping a mark along a path, hatching, tiling, combining
/// effects, and softening a shape's coverage mask.
/// </summary>
/// <remarks>
/// The gap these fill was visible in a multi-agent run's first stage: every line the same width,
/// foliage drawn as a row of identical ellipses placed by hand, grass as straight segments. All of
/// that is one call in Skia and was not reachable from a script.
/// <para>
/// These assert against <em>pixels</em>, not against the objects being constructed. A path effect
/// that builds without throwing and changes nothing on the canvas is the failure worth catching, and
/// it is invisible to a test that only checks the call returns.
/// </para>
/// </remarks>
public class BrushPathEffectTests : TestsRuntime
{
    #region Methods
    /// <summary>Renders a script onto a white canvas and returns the bitmap.</summary>
    private static SKBitmap Render(string body, int width = 200, int height = 80)
    {
        var result = new JsDrawingEngine().Execute($$"""
            const c = createCanvas({{width}}, {{height}});
            const x = c.getContext('2d');
            x.fillStyle = '#ffffff';
            x.fillRect(0, 0, {{width}}, {{height}});
            {{body}}
            c;
            """, width, height, null, "png", 100);

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.ImageBytes);

        return SKBitmap.Decode(result.ImageBytes!);
    }

    /// <summary>Counts separate runs of ink along one scanline.</summary>
    private static int InkRuns(SKBitmap bitmap, int y)
    {
        var runs = 0;
        var wasInk = false;

        for (var px = 0; px < bitmap.Width; px++)
        {
            var isInk = bitmap.GetPixel(px, y).Red < 128;
            if (isInk && !wasInk) runs++;
            wasInk = isInk;
        }

        return runs;
    }

    /// <summary>How many pixels in the image carry any ink at all.</summary>
    private static int InkPixels(SKBitmap bitmap)
    {
        var count = 0;

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var px = 0; px < bitmap.Width; px++)
            {
                if (bitmap.GetPixel(px, y).Red < 250) count++;
            }
        }

        return count;
    }

    private const string Stroke = """
        x.strokeStyle = '#000000';
        x.lineWidth = 2;
        x.beginPath();
        x.moveTo(0, 40);
        x.lineTo(200, 40);
        x.stroke();
        """;
    #endregion

    #region Stamping
    /// <summary>A stamped stroke is a row of marks, where a plain one is a single unbroken run.</summary>
    [Fact]
    public void TestStampBreaksAStrokeIntoRepeatedMarks()
    {
        using var plain = Render(Stroke);
        using var stamped = Render($"""
            x.pathEffect = Skia.PathEffect.stamp('M-3,-3 L3,-3 L3,3 L-3,3 Z', 14);
            {Stroke}
            """);

        Assert.Equal(1, InkRuns(plain, 40));
        Assert.True(InkRuns(stamped, 40) > 5, $"expected repeated marks, got {InkRuns(stamped, 40)}");
    }

    /// <summary>A wider advance means fewer marks over the same distance.</summary>
    [Fact]
    public void TestAdvanceControlsMarkDensity()
    {
        using var tight = Render($"x.pathEffect = Skia.PathEffect.stamp('M-3,-3 L3,-3 L3,3 L-3,3 Z', 10);\n{Stroke}");
        using var loose = Render($"x.pathEffect = Skia.PathEffect.stamp('M-3,-3 L3,-3 L3,3 L-3,3 Z', 30);\n{Stroke}");

        Assert.True(InkRuns(tight, 40) > InkRuns(loose, 40),
            $"tight={InkRuns(tight, 40)} loose={InkRuns(loose, 40)}");
    }

    /// <summary>A CanvasPath is accepted as the mark, not only an SVG string.</summary>
    [Fact]
    public void TestStampAcceptsACanvasPath()
    {
        using var stamped = Render($"""
            const mark = new CanvasPath();
            mark.moveTo(-3, -3); mark.lineTo(3, -3); mark.lineTo(3, 3); mark.lineTo(-3, 3); mark.closePath();
            x.pathEffect = Skia.PathEffect.stamp(mark, 14);
            {Stroke}
            """);

        Assert.True(InkRuns(stamped, 40) > 5);
    }

    /// <summary>An advance of zero has nowhere to place the next mark, and says so.</summary>
    [Fact]
    public void TestAZeroAdvanceIsRefused()
    {
        var result = new JsDrawingEngine().Execute(
            "Skia.PathEffect.stamp('M0,0 L4,0 L4,4 Z', 0);", 100, 100, null, "png", 100);

        Assert.False(result.Success);
        Assert.Contains("advance", result.Error ?? "", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>An unknown style is refused rather than silently falling back to a default.</summary>
    [Fact]
    public void TestAnUnknownStampStyleIsRefused()
    {
        var result = new JsDrawingEngine().Execute(
            "Skia.PathEffect.stamp('M0,0 L4,0 L4,4 Z', 10, 0, 'splatter');", 100, 100, null, "png", 100);

        Assert.False(result.Success);
        Assert.Contains("splatter", result.Error ?? "", StringComparison.Ordinal);
    }
    #endregion

    #region Hatching and tiling
    /// <summary>Hatching fills a region with lines, so a hatched fill carries far less ink than a solid one.</summary>
    [Fact]
    public void TestHatchFillsWithLinesRatherThanSolidly()
    {
        using var solid = Render("x.fillStyle = '#000000'; x.fillRect(20, 10, 160, 60);");
        using var hatched = Render("""
            x.fillStyle = '#000000';
            x.pathEffect = Skia.PathEffect.hatch(1, 8, 45);
            x.fillRect(20, 10, 160, 60);
            """);

        var solidInk = InkPixels(solid);
        var hatchedInk = InkPixels(hatched);

        Assert.True(hatchedInk > 0, "the hatch drew nothing at all");
        Assert.True(hatchedInk < solidInk / 2, $"expected sparse hatching: solid={solidInk} hatched={hatchedInk}");
    }

    /// <summary>Wider spacing means fewer lines and less ink.</summary>
    [Fact]
    public void TestHatchSpacingControlsDensity()
    {
        using var tight = Render("x.fillStyle='#000000'; x.pathEffect = Skia.PathEffect.hatch(1, 5, 0); x.fillRect(20, 10, 160, 60);");
        using var loose = Render("x.fillStyle='#000000'; x.pathEffect = Skia.PathEffect.hatch(1, 20, 0); x.fillRect(20, 10, 160, 60);");

        Assert.True(InkPixels(tight) > InkPixels(loose),
            $"tight={InkPixels(tight)} loose={InkPixels(loose)}");
    }

    /// <summary>Tiling places actual shapes across the region.</summary>
    [Fact]
    public void TestTileStipplesAShapeAcrossTheRegion()
    {
        using var tiled = Render("""
            x.fillStyle = '#000000';
            x.pathEffect = Skia.PathEffect.tile('M0,0 L2,0 L2,2 L0,2 Z', 10);
            x.fillRect(20, 10, 160, 60);
            """);

        Assert.True(InkPixels(tiled) > 0, "the tile drew nothing");

        // A small motif on a wide lattice inks only a few rows in each pitch, so most scanlines are
        // empty by design. The property under test is that *a* row carries repeated motifs.
        var busiest = 0;
        for (var y = 10; y < 70; y++) busiest = Math.Max(busiest, InkRuns(tiled, y));

        Assert.True(busiest > 3, $"expected repeated motifs across a row, busiest row had {busiest}");
    }
    #endregion

    #region Combining
    /// <summary>
    /// Summing two hatches at opposing angles gives cross-hatching — more ink than either alone.
    /// </summary>
    [Fact]
    public void TestSumCombinesTwoEffectsAdditively()
    {
        const string one = "x.fillStyle='#000000'; x.pathEffect = Skia.PathEffect.hatch(1, 10, 45); x.fillRect(20, 10, 160, 60);";
        const string both = """
            x.fillStyle='#000000';
            x.pathEffect = Skia.PathEffect.sum(
                Skia.PathEffect.hatch(1, 10, 45),
                Skia.PathEffect.hatch(1, 10, -45));
            x.fillRect(20, 10, 160, 60);
            """;

        using var single = Render(one);
        using var crossed = Render(both);

        Assert.True(InkPixels(crossed) > InkPixels(single),
            $"single={InkPixels(single)} crossed={InkPixels(crossed)}");
    }

    /// <summary>
    /// Composing jitter under a stamp is what makes natural media: the marks inherit the
    /// irregularity instead of marching evenly along a clean curve.
    /// </summary>
    [Fact]
    public void TestComposeAppliesTheInnerEffectFirst()
    {
        using var even = Render($"x.pathEffect = Skia.PathEffect.stamp('M-3,-3 L3,-3 L3,3 L-3,3 Z', 12);\n{Stroke}");
        using var jittered = Render($"""
            x.pathEffect = Skia.PathEffect.compose(
                Skia.PathEffect.stamp('M-3,-3 L3,-3 L3,3 L-3,3 Z', 12),
                Skia.PathEffect.discrete(6, 4, 7));
            {Stroke}
            """);

        // The jittered stroke wanders off the centre line, so the two do not agree scanline for
        // scanline. Comparing ink on one row is enough to show the inner effect was applied.
        Assert.NotEqual(InkRuns(even, 40), InkRuns(jittered, 40));
    }
    #endregion

    #region Mask filters
    /// <summary>A blurred mask spreads a shape's coverage past its own edge.</summary>
    [Fact]
    public void TestMaskFilterBlurSoftensTheEdge()
    {
        using var crisp = Render("x.fillStyle = '#000000'; x.fillRect(80, 30, 40, 20);");
        using var soft = Render("""
            x.fillStyle = '#000000';
            x.maskFilter = Skia.MaskFilter.blur(6);
            x.fillRect(80, 30, 40, 20);
            """);

        // Softening spreads coverage outward, so more pixels carry some ink even though the shape
        // is the same size.
        Assert.True(InkPixels(soft) > InkPixels(crisp),
            $"crisp={InkPixels(crisp)} soft={InkPixels(soft)}");

        // And the edge is a ramp rather than a step: a pixel just outside the rectangle is now tinted.
        Assert.True(crisp.GetPixel(76, 40).Red > 250, "the crisp edge should not have bled");
        Assert.True(soft.GetPixel(76, 40).Red < 250, "the soft edge should have bled outward");
    }

    /// <summary>An unknown blur style is refused rather than quietly treated as normal.</summary>
    [Fact]
    public void TestAnUnknownBlurStyleIsRefused()
    {
        var result = new JsDrawingEngine().Execute(
            "Skia.MaskFilter.blur(4, 'feathered');", 100, 100, null, "png", 100);

        Assert.False(result.Success);
        Assert.Contains("feathered", result.Error ?? "", StringComparison.Ordinal);
    }

    /// <summary>The mask filter is part of the drawing state, so it saves and restores.</summary>
    [Fact]
    public void TestMaskFilterIsSavedAndRestored()
    {
        using var bitmap = Render("""
            x.fillStyle = '#000000';
            x.save();
            x.maskFilter = Skia.MaskFilter.blur(6);
            x.fillRect(20, 30, 40, 20);
            x.restore();
            x.fillRect(120, 30, 40, 20);
            """);

        // The first rectangle bled; the second, drawn after restore, did not.
        Assert.True(bitmap.GetPixel(16, 40).Red < 250, "the saved-state blur should have bled");
        Assert.True(bitmap.GetPixel(116, 40).Red > 250, "the restored state should be crisp again");
    }
    #endregion
}
