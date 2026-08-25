namespace Polson.Tests.Drawing;

using System;
using Polson.Drawing.Skia;
using Polson.MCPServer;
using Xunit;

public class ImageAndBitmapTests : TestsRuntime
{
    #region Bitmap Creation and Pixel Manipulation Tests
    [Fact]
    public void TestBitmapCreationAndPixelAccess()
    {
        var skia = new SkiaApi();
        var bmp = skia.Bitmap.create(100, 100);
        Assert.Equal(100, bmp.Width);
        Assert.Equal(100, bmp.Height);

        bmp.setPixel(10, 10, "#ff0000ff");
        var pixel = bmp.getPixel(10, 10);
        Assert.Equal("#FF0000FF", pixel);

        var pngBytes = bmp.ToPngBytes();
        Assert.NotNull(pngBytes);
        Assert.True(pngBytes.Length > 0);
    }

    [Fact]
    public void TestBitmapTransformations()
    {
        var skia = new SkiaApi();
        var bmp = skia.Bitmap.create(200, 100);
        bmp.setPixel(0, 0, "#00ff00ff");

        // Crop subset
        var subset = bmp.extractSubset(0, 0, 50, 50);
        Assert.Equal(50, subset.Width);
        Assert.Equal(50, subset.Height);

        // Resize
        var resized = bmp.resize(400, 200, "linear");
        Assert.Equal(400, resized.Width);
        Assert.Equal(200, resized.Height);

        // Rotate
        var rotated = bmp.rotate(90);
        Assert.Equal(100, rotated.Width);
        Assert.Equal(200, rotated.Height);

        // Flip
        var flipped = bmp.flip("horizontal");
        Assert.Equal(200, flipped.Width);
        Assert.Equal(100, flipped.Height);
    }
    #endregion

    #region 9-Parameter drawImage Cropping Tests
    [Fact]
    public void TestNineParameterDrawImage()
    {
        var skia = new SkiaApi();
        var sourceBmp = skia.Bitmap.create(200, 200);
        sourceBmp.setPixel(50, 50, "#ff00ffff");

        var canvas = new SkiaCanvas(400, 400);
        var ctx = canvas.getContext("2d");

        // 9-param drawImage: sx, sy, sw, sh, dx, dy, dw, dh
        ctx.drawImage(sourceBmp, 25, 25, 50, 50, 100, 100, 200, 200);

        var pngBytes = canvas.ToPngBytes();
        Assert.NotNull(pngBytes);
        Assert.True(pngBytes.Length > 0);
    }
    #endregion

    #region ImageData Pixel Buffer Manipulation Tests
    [Fact]
    public void TestImageDataPixelManipulation()
    {
        var canvas = new SkiaCanvas(100, 100);
        var ctx = canvas.getContext("2d");

        ctx.fillStyle = "#ff0000";
        ctx.fillRect(0, 0, 100, 100);

        var imgData = ctx.getImageData(0, 0, 100, 100);
        Assert.Equal(100, imgData.Width);
        Assert.Equal(100, imgData.Height);
        Assert.Equal(100 * 100 * 4, imgData.Data.Length);

        // Invert green channel
        for (var i = 0; i < imgData.Data.Length; i += 4)
        {
            imgData.Data[i + 1] = 255; // Set green to 255 -> makes yellow
        }

        ctx.putImageData(imgData, 0, 0);

        var modifiedData = ctx.getImageData(50, 50, 1, 1);
        Assert.Equal(255, modifiedData.Data[0]); // Red
        Assert.Equal(255, modifiedData.Data[1]); // Green
    }
    #endregion

    #region Direct Bitmap Filtering Tests
    [Fact]
    public void TestDirectBitmapFiltering()
    {
        var skia = new SkiaApi();
        var bmp = skia.Bitmap.create(200, 200);
        bmp.setPixel(100, 100, "#ffffff");

        var blurFilter = skia.ImageFilter.blur(5, 5);
        var filteredBmp = bmp.applyFilter(blurFilter);

        Assert.Equal(200, filteredBmp.Width);
        Assert.Equal(200, filteredBmp.Height);
        Assert.NotNull(filteredBmp.ToPngBytes());
    }
    #endregion

    #region Sandboxed Jint JavaScript Execution Tests
    [Fact]
    public void TestJsBitmapAndImageDataScript()
    {
        var engine = new JsDrawingEngine();
        var jsCode = @"
            var canvas = createCanvas(400, 400);
            var ctx = canvas.getContext('2d');

            // Draw initial background
            ctx.fillStyle = '#0f172a';
            ctx.fillRect(0, 0, 400, 400);

            // Create a procedural tile bitmap
            var tile = Skia.Bitmap.create(50, 50);
            for (var x = 0; x < 50; x++) {
                tile.setPixel(x, x, '#f59e0b');
                tile.setPixel(49 - x, x, '#ec4899');
            }

            // Draw cropped & scaled tile
            ctx.drawImage(tile, 10, 10, 30, 30, 50, 50, 150, 150);

            // Manipulate pixel buffer
            var imgData = ctx.getImageData(0, 0, 50, 50);
            for (var i = 0; i < imgData.data.length; i += 4) {
                imgData.data[i] = 100; // Tint red channel
                imgData.data[i + 3] = 255;
            }
            ctx.putImageData(imgData, 200, 200);

            canvas;
        ";

        var result = engine.Execute(jsCode, 400, 400);
        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.PngBytes);
        Assert.True(result.PngBytes.Length > 0);
    }
    #endregion
}

