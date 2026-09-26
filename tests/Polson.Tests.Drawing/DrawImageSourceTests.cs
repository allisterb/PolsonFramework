namespace Polson.Tests.Drawing;

using System;
using Polson.Drawing.Skia;
using SkiaSharp;
using Xunit;

/// <summary>
/// <c>ctx.drawImage</c> draws anything that carries its own pixels, and refuses what does not.
/// </summary>
/// <remarks>
/// It used to return silently on a type it did not recognise. A live run passed a requisitioned
/// cutout cell, got back a white canvas in 0 ms, and wrote three blank character sheets before a
/// file size an order of magnitude too small gave it away.
/// </remarks>
public class DrawImageSourceTests : TestsRuntime
{
    /// <summary>Stands in for a requisitioned cell: pixels available only as a data URI.</summary>
    sealed class Asset(string uri) : IDataUriSource
    {
        public string ToDataUri() => uri;
    }

    static string RedSquare()
    {
        var b = new SkiaBitmapWrapper(new SKBitmap(10, 10));
        b.Bitmap.Erase(SKColors.Red);
        return b.ToDataUri("png");
    }

    [Fact]
    public void TestAnAssetCarryingPixelsIsDrawn()
    {
        var canvas = new SkiaCanvas(40, 40);
        var ctx = canvas.GetContext("2d");
        ctx.DrawImage(new Asset(RedSquare()), 5, 5);
        Assert.Equal("#FF0000FF", canvas.Bitmap.GetPixel(9, 9));
        Assert.Equal("#00000000", canvas.Bitmap.GetPixel(30, 30));
    }

    [Fact]
    public void TestSomethingWithoutPixelsIsRefusedByName()
    {
        var ctx = new SkiaCanvas(40, 40).GetContext("2d");
        var e = Assert.Throws<ArgumentException>(() => ctx.DrawImage(new Uri("https://example.com"), 0, 0));
        Assert.Contains("Uri", e.Message);
        Assert.Throws<ArgumentException>(() => ctx.DrawImage(new Asset("data:image/png;base64,AAAA"), 0, 0));
    }
}
