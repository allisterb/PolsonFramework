namespace Polson.Tests.Drawing;

using System;
using Polson.Drawing.Skia;
using SkiaSharp;
using Xunit;

/// <summary>
/// Regression tests for drawImage/drawSvg honouring the canvas drawing state.
/// Found by the E2E agent harness: ctx.colorFilter was silently ignored by drawImage. The
/// underlying cause was a null paint, so globalAlpha, globalCompositeOperation, filter and the
/// shadow were all dropped as well.
/// </summary>
public class CanvasImageCompositingTests : TestsRuntime
{
    #region Methods
    [Fact]
    public void TestDrawImageHonoursColorFilter()
    {
        var plain = MidGreyChannelAfter(ctx => { });
        var brightened = MidGreyChannelAfter(ctx =>
            ctx.ColorFilter = new SkiaApi().ColorFilter.ColorMatrix(Gain(2.55f)));

        Assert.InRange(plain, 0x50, 0x60);
        Assert.True(brightened > plain + 0x30, $"expected brightening, got {brightened:X2} vs {plain:X2}");
    }

    [Fact]
    public void TestDrawImageHonoursGlobalAlpha()
    {
        var canvas = new SkiaCanvas(64, 64);
        var ctx = canvas.GetContext("2d");

        ctx.FillStyle = "#000000";
        ctx.FillRect(0, 0, 64, 64);

        ctx.GlobalAlpha = 0.5f;
        ctx.DrawImage(SolidBitmap(64, 64, SKColors.White), 0, 0);

        // White at half alpha over black lands mid-grey, not white.
        var channel = ChannelAt(canvas, 32, 32);
        Assert.InRange(channel, 0x70, 0x90);
    }

    [Fact]
    public void TestDrawImageHonoursBlendMode()
    {
        var canvas = new SkiaCanvas(64, 64);
        var ctx = canvas.GetContext("2d");

        ctx.FillStyle = "#808080";
        ctx.FillRect(0, 0, 64, 64);

        ctx.GlobalCompositeOperation = "multiply";
        ctx.DrawImage(SolidBitmap(64, 64, SKColors.Black), 0, 0);

        // Multiplying by black must go to black; ignoring the blend mode would also give black,
        // so pair it with a white multiply, which must leave the grey untouched.
        Assert.InRange(ChannelAt(canvas, 32, 32), 0x00, 0x08);

        var canvas2 = new SkiaCanvas(64, 64);
        var ctx2 = canvas2.GetContext("2d");
        ctx2.FillStyle = "#808080";
        ctx2.FillRect(0, 0, 64, 64);
        ctx2.GlobalCompositeOperation = "multiply";
        ctx2.DrawImage(SolidBitmap(64, 64, SKColors.White), 0, 0);

        Assert.InRange(ChannelAt(canvas2, 32, 32), 0x78, 0x88);
    }

    [Fact]
    public void TestDrawImageStateIsScopedBySaveRestore()
    {
        var canvas = new SkiaCanvas(64, 64);
        var ctx = canvas.GetContext("2d");

        ctx.FillStyle = "#000000";
        ctx.FillRect(0, 0, 64, 64);

        ctx.Save();
        ctx.GlobalAlpha = 0.5f;
        ctx.Restore();
        ctx.DrawImage(SolidBitmap(64, 64, SKColors.White), 0, 0);

        Assert.InRange(ChannelAt(canvas, 32, 32), 0xF8, 0xFF);
    }

    /// <summary>A 4x5 colour matrix that scales RGB by <paramref name="gain"/> and keeps alpha.</summary>
    private static float[] Gain(float gain) =>
    [
        gain, 0, 0, 0, 0,
        0, gain, 0, 0, 0,
        0, 0, gain, 0, 0,
        0, 0, 0, 1, 0
    ];

    private static byte MidGreyChannelAfter(Action<CanvasRenderingContext2D> configure)
    {
        var canvas = new SkiaCanvas(64, 64);
        var ctx = canvas.GetContext("2d");

        configure(ctx);
        ctx.DrawImage(SolidBitmap(64, 64, new SKColor(0x57, 0x57, 0x55)), 0, 0);

        return ChannelAt(canvas, 32, 32);
    }

    private static SkiaBitmapWrapper SolidBitmap(int width, int height, SKColor color)
    {
        var bmp = new SKBitmap(width, height);
        using (var surfaceCanvas = new SKCanvas(bmp))
        {
            surfaceCanvas.Clear(color);
        }

        return new SkiaBitmapWrapper(bmp);
    }

    /// <summary>The red channel of the bitmap's "#RRGGBBAA" pixel readback.</summary>
    private static byte ChannelAt(SkiaCanvas canvas, int x, int y) =>
        Convert.ToByte(canvas.ToBitmap().GetPixel(x, y).Substring(1, 2), 16);
    #endregion
}
