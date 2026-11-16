namespace Polson.Tests.MCPServer;

using System;
using System.IO;
using Polson.MCPServer;
using SkiaSharp;
using Xunit;

/// <summary>
/// Scripts that try to leave the sandbox, from the 2026-11-15 security review. Each one worked before it was closed: a
/// native pointer and a host crash through a canvas's <c>SKBitmap</c> and <c>SKCanvas</c>, a file written outside the
/// project through a paper's <c>SvgDocument</c>, a file outside the project read through an SVG image href, and a
/// 40 GB canvas. Polson.Tests.Engine compiles this file too, so the engine build is held to the same.
/// </summary>
public class SandboxEscapeTests : TestsRuntime
{
    #region Tests
    [Theory]
    [InlineData("createCanvas(4, 4).skBitmap", "getPixels")]
    [InlineData("createCanvas(4, 4).skBitmap", "setPixels")]
    [InlineData("createCanvas(4, 4).skCanvas", "dispose")]
    [InlineData("createCanvas(4, 4).skCanvas", "handle")]
    [InlineData("Snap(4, 4).document", "write")]
    [InlineData("Snap(4, 4).rect(0, 0, 1, 1).node", "write")]
    public void TestALibraryObjectHasNoMembers(string expression, string member)
    {
        var result = Run($"const o = {expression}; log(typeof o.{member} + ' ' + has(o, '{member}'));");

        Assert.True(result.Success, result.Error);
        Assert.Contains("[LOG] undefined false", result.Logs);
    }

    [Fact]
    public void TestDisposingTheCanvasFailsTheScriptAndNotTheHost()
    {
        var result = Run("const c = createCanvas(4, 4); c.skCanvas.dispose(); c.getContext('2d').fillRect(0, 0, 4, 4);");

        Assert.False(result.Success);
    }

    [Fact]
    public void TestAPaperCannotWriteOutsideTheProject()
    {
        var target = Path.Combine(Outside(), "written.svg");
        var result = Run($"Snap(4, 4).document.write({Js(target)});");

        Assert.False(result.Success);
        Assert.False(File.Exists(target));
    }

    [Theory]
    [InlineData("file")]
    [InlineData("path")]
    public void TestAnImageHrefDoesNotReadAFile(string form)
    {
        var secret = Path.Combine(Outside(), "secret.png");
        using (var red = new SKBitmap(8, 8))
        {
            red.Erase(SKColors.Red);
            File.WriteAllBytes(secret, red.Encode(SKEncodedImageFormat.Png, 100).ToArray());
        }
        var href = form == "file" ? new Uri(secret).AbsoluteUri : secret;

        var result = new JsDrawingEngine().Execute(
            $"const p = Snap(8, 8); p.image({Js(href)}, 0, 0, 8, 8); p;", 8, 8, format: "png");

        Assert.True(result.Success, result.Error);
        using var rendered = SKBitmap.Decode(result.ImageBytes);
        Assert.NotEqual(SKColors.Red, rendered.GetPixel(4, 4));
    }

    [Theory]
    [InlineData(100_000, 100_000)]
    [InlineData(40_000, 10)]
    [InlineData(20_000, 20_000)]
    public void TestAnOversizedCanvasIsRefused(int width, int height)
    {
        var result = Run($"createCanvas({width}, {height});");

        Assert.False(result.Success);
        Assert.Contains("larger than this engine will allocate", result.Error);
    }

    [Fact]
    public void TestALargePrintCanvasIsAllowed()
    {
        // A0 at 300 dpi.
        var result = Run("const c = createCanvas(9933, 14043); log(String(c.width));");

        Assert.True(result.Success, result.Error);
    }

    [Fact]
    public void TestAValueTypeStillReads()
    {
        // Colours, points and rectangles are copies with no handle, so their members stay readable.
        Assert.True(JsDrawingEngine.ScriptMayReach(typeof(SKColor).GetProperty(nameof(SKColor.Red))!));
        Assert.False(JsDrawingEngine.ScriptMayReach(typeof(SKBitmap).GetMethod(nameof(SKBitmap.GetPixels), Type.EmptyTypes)!));
        Assert.False(JsDrawingEngine.ScriptMayReach(typeof(File).GetMethod(nameof(File.Delete))!));
    }
    #endregion

    #region Methods
    private static DrawingExecutionResult Run(string script) => new JsDrawingEngine().Execute(script, 16, 16, render: false);

    private static string Js(string text) => System.Text.Json.JsonSerializer.Serialize(text);

    private static string Outside()
    {
        var dir = Path.Combine(Path.GetTempPath(), "polson-sandbox-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
    #endregion
}
