namespace Polson.Tests.MCPServer;

using System;
using System.IO;
using System.Threading.Tasks;

using global::Polson.MCPServer;
using global::Polson.Tests;

using Xunit;

/// <summary>
/// Writing pixels through <c>imageData.data</c>, the way the SDK reference says to.
/// </summary>
/// <remarks>
/// <b>Every one of these writes used to be lost, silently.</b> The engine converted the CLR
/// <c>byte[]</c> into a fresh JS array on every read of <c>data</c>, so <c>img.data === img.data</c> was
/// false, a write went into a copy nobody kept, and <c>putImageData</c> put back the pixels it had been
/// handed. It raised nothing and rendered the untouched picture, which reads as a script with a logic
/// error rather than as a buffer that was never written. <c>data</c> is now a <c>Uint8ClampedArray</c>
/// sharing the buffer's storage, as it is in a browser.
/// </remarks>
public class ImageDataWriteTests : TestsRuntime, IDisposable
{
    #region Fields
    private readonly string root = Path.Combine(Path.GetTempPath(), "polson-imagedata-" + Guid.NewGuid().ToString("N"));
    #endregion

    #region Methods
    public ImageDataWriteTests() => Directory.CreateDirectory(root);

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        GC.SuppressFinalize(this);
    }
    #endregion

    #region Tests
    /// <summary>The reported repro: a write through a held reference reaches the canvas.</summary>
    [Fact]
    public async Task TestAWriteThroughDataReachesTheCanvas()
    {
        var result = await Run("""
            const cv = createCanvas(4, 4), cx = cv.getContext('2d');
            cx.fillStyle = '#336699'; cx.fillRect(0, 0, 4, 4);
            const img = cx.getImageData(0, 0, 4, 4);
            const d = img.data; d[0] = 255; d[3] = 0;
            cx.putImageData(img, 0, 0);
            log(cv.bitmap.getPixel(0, 0) + ' ' + (img.data === img.data));
            """);

        Assert.True(result.Success, result.Error);
        Assert.Equal("#00000000 true", Logged(result));
    }

    /// <summary>A visible write, through <c>img.data</c> read afresh each time.</summary>
    [Fact]
    public async Task TestAnOpaqueWriteChangesTheColour()
    {
        var result = await Run("""
            const cv = createCanvas(4, 4), cx = cv.getContext('2d');
            cx.fillStyle = '#336699'; cx.fillRect(0, 0, 4, 4);
            const img = cx.getImageData(0, 0, 4, 4);
            img.data[4] = 255; img.data[5] = 0; img.data[6] = 0;
            cx.putImageData(img, 0, 0);
            log(cv.bitmap.getPixel(1, 0) + ' ' + cv.bitmap.getPixel(0, 0));
            """);

        Assert.True(result.Success, result.Error);
        Assert.Equal("#FF0000FF #336699FF", Logged(result));
    }

    /// <summary>It is the browser's type, and clamps as the browser's type does.</summary>
    [Fact]
    public async Task TestDataIsAUint8ClampedArray()
    {
        var result = await Run("""
            const img = createCanvas(2, 2).getContext('2d').createImageData(2, 2);
            const d = img.data;
            d[0] = 300; d[1] = -5; d[2] = 12.6;
            log((d instanceof Uint8ClampedArray) + ' ' + d.length + ' ' + d[0] + ' ' + d[1] + ' ' + d[2]);
            """);

        Assert.True(result.Success, result.Error);
        Assert.Equal("true 16 255 0 13", Logged(result));
    }

    /// <summary>A new ImageData built from another's data carries the pixels written into it.</summary>
    [Fact]
    public async Task TestANewImageDataCanBeBuiltFromWrittenData()
    {
        var result = await Run("""
            const cv = createCanvas(2, 2), cx = cv.getContext('2d');
            const src = cx.createImageData(2, 2);
            for (let i = 0; i < src.data.length; i += 4) { src.data[i + 1] = 200; src.data[i + 3] = 255; }
            const copy = new ImageData(src.data, 2, 2);
            cx.putImageData(copy, 0, 0);
            log(cv.bitmap.getPixel(1, 1));
            """);

        Assert.True(result.Success, result.Error);
        Assert.Equal("#00C800FF", Logged(result));
    }

    /// <summary>Every constructor form a browser accepts works with <c>new</c>, and the result is an ImageData.</summary>
    /// <remarks>
    /// The global was a plain function until 2026-09-24, so <c>new ImageData(w, h)</c> - the only spelling a
    /// browser accepts - threw "ImageData is not a constructor". The call form keeps working.
    /// </remarks>
    [Fact]
    public async Task TestImageDataIsAConstructor()
    {
        var result = await Run("""
            const a = new ImageData(2, 3);
            const src = new ImageData(2, 2); src.data[1] = 200;
            const b = new ImageData(src.data, 2);
            const c = new ImageData([1, 2, 3, 255, 4, 5, 6, 255], 2, 1);
            const got = createCanvas(2, 2).getContext('2d').getImageData(0, 0, 2, 2);
            log([a.width, a.height, a.data.length, a instanceof ImageData, b.height, b.data[1], c.data[4],
                 got instanceof ImageData, ImageData(1, 1).width].join(' '));
            """);

        Assert.True(result.Success, result.Error);
        Assert.Equal("2 3 24 true 2 200 4 true 1", Logged(result));
    }

    /// <summary>A constructed ImageData copies the array it is given; unlike a browser, the two do not share.</summary>
    [Fact]
    public async Task TestAConstructedImageDataCopiesItsSource()
    {
        var result = await Run("""
            const src = new ImageData(2, 2);
            const copy = new ImageData(src.data, 2, 2);
            src.data[0] = 9;
            log(String(copy.data[0]));
            """);

        Assert.True(result.Success, result.Error);
        Assert.Equal("0", Logged(result));
    }

    /// <summary>An ImageData kept in Session is still writable in the next script's engine.</summary>
    /// <remarks>
    /// The view belongs to the engine that made it, so the next engine must make its own over the same
    /// buffer rather than reuse one from a realm that no longer exists.
    /// </remarks>
    [Fact]
    public void TestAnImageDataKeptInSessionIsWritableInTheNextScript()
    {
        var session = new SessionContext();

        Execute(session, "Session.img = createCanvas(2, 2).getContext('2d').createImageData(2, 2); Session.img.data[0] = 7;");
        var result = Execute(session, """
            Session.img.data[1] = 9;
            const cv = createCanvas(2, 2), cx = cv.getContext('2d');
            Session.img.data[3] = 255;
            cx.putImageData(Session.img, 0, 0);
            log(Session.img.data[0] + ' ' + Session.img.data[1] + ' ' + cv.bitmap.getPixel(0, 0));
            """);

        Assert.True(result.Success, result.Error);
        Assert.Equal("7 9 #070900FF", Logged(result));
    }
    #endregion

    #region Methods (private)
    private Task<DrawingExecutionResult> Run(string script) =>
        new DrawingMcpTools(null, null, null, root).ExecuteScript(script, 20, 20);

    private static DrawingExecutionResult Execute(SessionContext session, string script) =>
        new JsDrawingEngine().Execute(script, 20, 20, session, "png", 90, render: false);

    private static string Logged(DrawingExecutionResult result)
    {
        Assert.NotEmpty(result.Logs);
        var last = result.Logs[^1];
        var at = last.IndexOf("] ", StringComparison.Ordinal);
        return (at >= 0 ? last[(at + 2)..] : last).Trim();
    }
    #endregion
}
