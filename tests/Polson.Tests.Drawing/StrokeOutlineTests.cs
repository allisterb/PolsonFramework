namespace Polson.Tests.Drawing;

using System;
using Polson.MCPServer;
using SkiaSharp;
using Xunit;

/// <summary>
/// <c>ctx.strokeToPath()</c> and <c>ctx.dither</c> — the last of the paint surface that a script
/// could not reach.
/// </summary>
/// <remarks>
/// Outlining is what turns a stroke from a line with a width into a shape. Every brush preset draws
/// at constant width because that is all a stroke can be; modulating the *outline* is how a mark gets
/// pressure, and it is also what makes a textured stroke survive into <c>outSvg</c> as vector.
/// </remarks>
public class StrokeOutlineTests : TestsRuntime
{
    #region Methods
    private static SKBitmap Render(string body, int width = 300, int height = 100)
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

    private const string Line = """
        const p = new CanvasPath();
        p.moveTo(20, 50);
        p.lineTo(280, 50);
        """;
    #endregion

    #region Outlining
    /// <summary>
    /// Filling the outline covers what stroking the path would have covered.
    /// </summary>
    /// <remarks>
    /// The outline is the stroke's own geometry, so the two ways of drawing it agree. Compared with a
    /// tolerance rather than exactly: the fill and the stroke antialias their shared edge separately.
    /// </remarks>
    [Fact]
    public void TestTheOutlineCoversWhatTheStrokeWouldHave()
    {
        using var stroked = Render($"""
            {Line}
            x.strokeStyle = '#000000'; x.lineWidth = 12; x.lineCap = 'butt';
            x.stroke(p);
            """);

        using var filled = Render($"""
            {Line}
            x.lineWidth = 12; x.lineCap = 'butt';
            const outline = x.strokeToPath(p);
            x.fillStyle = '#000000';
            x.fill(outline);
            """);

        var a = InkPixels(stroked);
        var b = InkPixels(filled);

        Assert.True(Math.Abs(a - b) < a * 0.05,
            $"outline should cover the same area: stroked={a} filled={b}");
    }

    /// <summary>A wider stroke outlines to a larger shape — the width is baked into the geometry.</summary>
    [Fact]
    public void TestTheOutlineCarriesTheStrokeWidth()
    {
        using var thin = Render($"{Line} x.lineWidth = 4; x.fillStyle = '#000000'; x.fill(x.strokeToPath(p));");
        using var thick = Render($"{Line} x.lineWidth = 16; x.fillStyle = '#000000'; x.fill(x.strokeToPath(p));");

        Assert.True(InkPixels(thick) > InkPixels(thin) * 2,
            $"thin={InkPixels(thin)} thick={InkPixels(thick)}");
    }

    /// <summary>
    /// The outline is an ordinary path, so it can be cut — which a stroke cannot.
    /// </summary>
    /// <remarks>
    /// This is the capability that was missing: modulating a mark after the fact. A stroke has no
    /// interior to operate on; its outline does.
    /// </remarks>
    [Fact]
    public void TestTheOutlineCanBeCombinedWithOtherPaths()
    {
        using var whole = Render($"{Line} x.lineWidth = 14; x.fillStyle = '#000000'; x.fill(x.strokeToPath(p));");
        using var cut = Render($"""
            {Line}
            x.lineWidth = 14;
            const outline = x.strokeToPath(p);
            const hole = new CanvasPath();
            hole.arc(150, 50, 25, 0, Math.PI * 2);
            x.fillStyle = '#000000';
            x.fill(outline.subtract(hole));
            """);

        Assert.True(InkPixels(cut) < InkPixels(whole),
            $"the circle should have been removed: whole={InkPixels(whole)} cut={InkPixels(cut)}");

        // And the bite is where the circle was, not somewhere else.
        Assert.True(cut.GetPixel(150, 50).Red > 250, "the centre of the hole should be background");
        Assert.True(cut.GetPixel(40, 50).Red < 100, "the rest of the stroke should survive");
    }

    /// <summary>A path effect is baked into the outline, so a textured stroke becomes geometry.</summary>
    [Fact]
    public void TestThePathEffectIsBakedIntoTheOutline()
    {
        using var plain = Render($"{Line} x.lineWidth = 8; x.fillStyle = '#000000'; x.fill(x.strokeToPath(p));");
        using var dashed = Render($"""
            {Line}
            x.lineWidth = 8;
            x.pathEffect = Skia.PathEffect.dash([10, 10]);
            x.fillStyle = '#000000';
            x.fill(x.strokeToPath(p));
            """);

        Assert.True(InkPixels(dashed) < InkPixels(plain) * 0.75,
            $"the dash should have carried into the outline: plain={InkPixels(plain)} dashed={InkPixels(dashed)}");
    }

    /// <summary>With no argument it outlines the current path, like the other path-taking calls.</summary>
    [Fact]
    public void TestItDefaultsToTheCurrentPath()
    {
        using var bitmap = Render("""
            x.beginPath();
            x.moveTo(20, 50);
            x.lineTo(280, 50);
            x.lineWidth = 10;
            x.fillStyle = '#000000';
            x.fill(x.strokeToPath());
            """);

        Assert.True(InkPixels(bitmap) > 1000, "the current path should have been outlined");
    }
    #endregion

    #region Dither
    /// <summary>
    /// Dither is off by default and can be turned on, and it changes the pixels of a shallow ramp.
    /// </summary>
    /// <remarks>
    /// Asserted as a difference rather than as an absence of banding: the point is that the knob now
    /// reaches the paint at all, which it did not before — <c>IsDither</c> was never set from script.
    /// </remarks>
    [Fact]
    public void TestDitherReachesThePaint()
    {
        const string Ramp = """
            const g = x.createLinearGradient(0, 0, 300, 0);
            g.addColorStop(0, '#303030');
            g.addColorStop(1, '#343434');
            x.fillStyle = g;
            x.fillRect(0, 0, 300, 100);
            """;

        using var plain = Render(Ramp);
        using var dithered = Render($"x.dither = true;\n{Ramp}");

        var differences = 0;
        for (var px = 0; px < 300; px++)
        {
            if (plain.GetPixel(px, 50).Red != dithered.GetPixel(px, 50).Red) differences++;
        }

        Assert.True(differences > 0, "turning dither on should change the rendered ramp");
    }

    /// <summary>It is drawing state, so save and restore scope it.</summary>
    [Fact]
    public void TestDitherIsSavedAndRestored()
    {
        var result = new JsDrawingEngine().Execute("""
            const c = createCanvas(10, 10);
            const x = c.getContext('2d');
            log('initial=' + x.dither);
            x.save();
            x.dither = true;
            log('inside=' + x.dither);
            x.restore();
            log('after=' + x.dither);
            """, 10, 10, null, "png", 100);

        Assert.True(result.Success, result.Error);

        var text = string.Join('\n', result.Logs);
        Assert.Contains("initial=false", text, StringComparison.Ordinal);
        Assert.Contains("inside=true", text, StringComparison.Ordinal);
        Assert.Contains("after=false", text, StringComparison.Ordinal);
    }
    #endregion
}
