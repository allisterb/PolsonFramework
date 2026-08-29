namespace Polson.Tests.Drawing;

using System;
using Polson.MCPServer;
using SkiaSharp;
using Xunit;

/// <summary>
/// The <c>Skia.Brush</c> presets, and <c>ctx.useBrush</c>.
/// </summary>
/// <remarks>
/// These add no capability — every preset is a shader, a path effect and a mask filter a script could
/// already assemble. What they add is discoverability, so the tests are about that: each medium looks
/// different from the others, a solid one stays solid, a dry one deposits unevenly, and switching
/// media does not leave the previous one's texture behind.
/// </remarks>
public class BrushPresetTests : TestsRuntime
{
    #region Methods
    /// <summary>Strokes one horizontal line with the given setup and returns the rendered bitmap.</summary>
    private static SKBitmap Stroke(string setup, int width = 300, int height = 60)
    {
        var result = new JsDrawingEngine().Execute($$"""
            const c = createCanvas({{width}}, {{height}});
            const x = c.getContext('2d');
            x.fillStyle = '#ffffff';
            x.fillRect(0, 0, {{width}}, {{height}});
            {{setup}}
            x.beginPath();
            x.moveTo(10, 30);
            x.lineTo({{width - 10}}, 30);
            x.stroke();
            c;
            """, width, height, null, "png", 100);

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.ImageBytes);

        return SKBitmap.Decode(result.ImageBytes!);
    }

    /// <summary>How many pixels along the stroke's own row carry ink.</summary>
    private static int InkAlongTheLine(SKBitmap bitmap)
    {
        var count = 0;
        for (var px = 0; px < bitmap.Width; px++)
        {
            if (bitmap.GetPixel(px, 30).Red < 240) count++;
        }
        return count;
    }

    /// <summary>Total ink anywhere in the image, which catches spread as well as coverage.</summary>
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
    #endregion

    #region Presets
    /// <summary>Every preset puts something on the canvas.</summary>
    [Theory]
    [InlineData("pencil()")]
    [InlineData("ink()")]
    [InlineData("chalk()")]
    [InlineData("marker()")]
    [InlineData("stipple()")]
    public void TestEveryPresetDraws(string call)
    {
        using var bitmap = Stroke($"x.useBrush(Skia.Brush.{call});");

        Assert.True(InkPixels(bitmap) > 0, $"{call} drew nothing");
    }

    /// <summary>
    /// A dry medium deposits unevenly; a solid one does not. That difference is the whole point of
    /// having both.
    /// </summary>
    [Fact]
    public void TestADryMediumBreaksUpWhereASolidOneDoesNot()
    {
        using var ink = Stroke("x.useBrush(Skia.Brush.ink());");
        using var pencil = Stroke("x.useBrush(Skia.Brush.pencil());");

        var inkRun = InkAlongTheLine(ink);
        var pencilRun = InkAlongTheLine(pencil);

        Assert.True(inkRun > 250, $"ink should be continuous, covered {inkRun} of 300");
        Assert.True(pencilRun < inkRun, $"pencil should deposit unevenly: ink={inkRun} pencil={pencilRun}");
        Assert.True(pencilRun > 30, $"pencil should still read as a line, covered only {pencilRun}");
    }

    /// <summary>Grain is dialable, and zero means an even deposit.</summary>
    [Fact]
    public void TestGrainZeroGivesASolidMark()
    {
        using var grainy = Stroke("x.useBrush(Skia.Brush.pencil('#3a3a3a', 2.2, 1));");
        using var even = Stroke("x.useBrush(Skia.Brush.pencil('#3a3a3a', 2.2, 0));");

        Assert.True(InkAlongTheLine(even) > InkAlongTheLine(grainy),
            $"grain 0 should cover more: even={InkAlongTheLine(even)} grainy={InkAlongTheLine(grainy)}");
    }

    /// <summary>Stipple leaves separate deposits rather than a continuous line.</summary>
    [Fact]
    public void TestStippleLeavesSeparateDeposits()
    {
        using var bitmap = Stroke("x.useBrush(Skia.Brush.stipple());");

        var runs = 0;
        var wasInk = false;
        for (var px = 0; px < bitmap.Width; px++)
        {
            var isInk = bitmap.GetPixel(px, 30).Red < 200;
            if (isInk && !wasInk) runs++;
            wasInk = isInk;
        }

        Assert.True(runs > 8, $"expected a trail of separate marks, got {runs}");
    }

    /// <summary>A preset says what it is made of, so a script can take it apart.</summary>
    /// <remarks>
    /// The point of a legible preset: an agent can read a pencil's width and cap, see that it carries
    /// grain and a softened edge, and change one of them — rather than being handed an opaque object.
    /// </remarks>
    [Fact]
    public void TestAPresetIsInspectable()
    {
        var result = new JsDrawingEngine().Execute("""
            const b = Skia.Brush.pencil();
            log(b.name + ' w=' + b.lineWidth.toFixed(1) + ' cap=' + b.lineCap
                + ' grain=' + (b.grain !== null) + ' edge=' + (b.edge !== null));
            const plain = Skia.Brush.ink();
            log(plain.name + ' grain=' + (plain.grain !== null) + ' edge=' + (plain.edge !== null));
            """, 10, 10, null, "png", 100);

        Assert.True(result.Success, result.Error);

        var text = string.Join('\n', result.Logs);
        Assert.Contains("pencil w=2.2 cap=round grain=true edge=true", text, StringComparison.Ordinal);
        Assert.Contains("ink grain=false edge=false", text, StringComparison.Ordinal);
    }
    #endregion

    #region Applying
    /// <summary>
    /// Switching media clears what the previous one set, rather than inheriting it.
    /// </summary>
    /// <remarks>
    /// The failure this prevents is a speckled ink line after a pencil passage — a stale path effect
    /// carried forward, which looks like a bug in the ink rather than in the sequence.
    /// </remarks>
    [Fact]
    public void TestSwitchingBrushesClearsTheOldTexture()
    {
        using var inkAlone = Stroke("x.useBrush(Skia.Brush.ink());");
        using var afterPencil = Stroke("""
            x.useBrush(Skia.Brush.pencil());
            x.useBrush(Skia.Brush.ink());
            """);

        Assert.Equal(InkAlongTheLine(inkAlone), InkAlongTheLine(afterPencil));
    }

    /// <summary>A brush is drawing state, so save and restore scope it.</summary>
    [Fact]
    public void TestABrushIsScopedBySaveAndRestore()
    {
        using var plain = Stroke("x.strokeStyle = '#000000'; x.lineWidth = 2.6; x.lineCap = 'round';");
        using var restored = Stroke("""
            x.strokeStyle = '#000000'; x.lineWidth = 2.6; x.lineCap = 'round';
            x.save();
            x.useBrush(Skia.Brush.chalk());
            x.restore();
            """);

        Assert.Equal(InkAlongTheLine(plain), InkAlongTheLine(restored));
    }
    #endregion
}
