namespace Polson.Tests.Drawing;

using System;
using Polson.Drawing.Skia;
using Polson.Drawing.Svg;
using Polson.MCPServer;
using Xunit;

public class Canvas2DTests : TestsRuntime
{
    #region Canvas Creation and Path Drawing Tests
    [Fact]
    public void TestCanvasCreationAndBasicDrawing()
    {
        var canvas = new SkiaCanvas(400, 300);
        Assert.Equal(400, canvas.Width);
        Assert.Equal(300, canvas.Height);

        var ctx = canvas.getContext("2d");
        Assert.NotNull(ctx);

        ctx.fillStyle = "#ff0000";
        ctx.fillRect(10, 10, 100, 80);

        ctx.strokeStyle = "#0000ff";
        ctx.lineWidth = 4f;
        ctx.strokeRect(150, 20, 100, 50);

        ctx.clearRect(20, 30, 20, 20);

        var webpBytes = canvas.ToImageBytes();
        Assert.NotNull(webpBytes);
        Assert.True(webpBytes.Length > 0);

        // Verify WebP signature
        Assert.Equal((byte)'R', webpBytes[0]);
        Assert.Equal((byte)'I', webpBytes[1]);
        Assert.Equal((byte)'F', webpBytes[2]);
        Assert.Equal((byte)'F', webpBytes[3]);
    }

    [Fact]
    public void TestPathConstructionAndArcs()
    {
        var canvas = new SkiaCanvas(500, 500);
        var ctx = canvas.getContext("2d");

        ctx.beginPath();
        ctx.moveTo(50, 50);
        ctx.lineTo(200, 50);
        ctx.lineTo(200, 200);
        ctx.closePath();
        ctx.fillStyle = "#10b981";
        ctx.fill();

        ctx.beginPath();
        ctx.arc(350, 200, 50, 0, MathF.PI * 2f);
        ctx.fillStyle = "#3b82f6";
        ctx.fill();

        ctx.beginPath();
        ctx.roundRect(50, 300, 150, 100, 20f);
        ctx.strokeStyle = "#f59e0b";
        ctx.lineWidth = 3f;
        ctx.stroke();

        var imgBytes = canvas.ToImageBytes();
        Assert.True(imgBytes.Length > 0);
    }
    #endregion

    #region State Stack and Transforms Tests
    [Fact]
    public void TestStateStackAndTransforms()
    {
        var canvas = new SkiaCanvas(400, 400);
        var ctx = canvas.getContext("2d");

        ctx.fillStyle = "#ff0000";
        ctx.save();

        ctx.translate(100, 100);
        ctx.rotate(MathF.PI / 4f);
        ctx.scale(2f, 2f);
        ctx.fillStyle = "#00ff00";
        ctx.fillRect(0, 0, 50, 50);

        ctx.restore();
        ctx.fillRect(0, 0, 20, 20); // Should be red (#ff0000)

        var bmp = canvas.toBitmap();
        Assert.NotNull(bmp);
        Assert.Equal(400, bmp.Width);
    }
    #endregion

    #region Gradients and Patterns Tests
    [Fact]
    public void TestLinearAndRadialGradients()
    {
        var canvas = new SkiaCanvas(600, 400);
        var ctx = canvas.getContext("2d");

        // Linear gradient
        var linGrad = ctx.createLinearGradient(0, 0, 600, 0);
        linGrad.addColorStop(0f, "#ff0000");
        linGrad.addColorStop(0.5f, "#00ff00");
        linGrad.addColorStop(1f, "#0000ff");

        ctx.fillStyle = linGrad;
        ctx.fillRect(0, 0, 600, 200);

        // Radial gradient
        var radGrad = ctx.createRadialGradient(300, 300, 20, 300, 300, 100);
        radGrad.addColorStop(0f, "#ffffff");
        radGrad.addColorStop(1f, "#000000");

        ctx.fillStyle = radGrad;
        ctx.fillRect(0, 200, 600, 200);

        var dataUri = canvas.ToDataUri();
        Assert.StartsWith("data:image/webp;base64,", dataUri);
    }
    #endregion

    #region Text Rendering and Metrics Tests
    [Fact]
    public void TestTextRenderingAndMetrics()
    {
        var canvas = new SkiaCanvas(600, 400);
        var ctx = canvas.getContext("2d");

        ctx.font = "bold 32px sans-serif";
        ctx.fillStyle = "#ffffff";
        ctx.textAlign = "center";
        ctx.textBaseline = "middle";

        var metrics = ctx.measureText("Skia Canvas 2D");
        Assert.NotNull(metrics);
        Assert.True(Convert.ToSingle(metrics["width"]) > 0);

        ctx.fillText("Skia Canvas 2D", 300, 200);

        var imgBytes = canvas.ToImageBytes();
        Assert.True(imgBytes.Length > 0);
    }
    #endregion

    #region Skia Procedural Shaders and Filters Tests
    [Fact]
    public void TestSkiaProceduralNoiseAndFilters()
    {
        var canvas = new SkiaCanvas(400, 400);
        var ctx = canvas.getContext("2d");
        var skia = new SkiaApi();

        // Procedural Perlin noise
        var noise = skia.Shader.perlinNoiseTurbulence(0.05f, 0.05f, 4, 12345);
        ctx.fillStyle = noise;
        ctx.fillRect(0, 0, 400, 400);

        // Native Gaussian Blur filter
        ctx.filter = skia.ImageFilter.blur(5f, 5f);
        ctx.fillStyle = "#ffffff";
        ctx.fillRect(100, 100, 200, 200);

        var imgBytes = canvas.ToImageBytes();
        Assert.True(imgBytes.Length > 0);
    }
    #endregion

    #region Cross-Engine SVG Layering Tests
    [Fact]
    public void TestCrossEngineSvgDrawing()
    {
        var canvas = new SkiaCanvas(600, 600);
        var ctx = canvas.getContext("2d");

        // Background in Canvas 2D
        ctx.fillStyle = "#0f172a";
        ctx.fillRect(0, 0, 600, 600);

        // Vector SVG object created via Snap
        var paper = Snap.Create(300, 300);
        paper.Circle(150, 150, 100).Attr("fill", "#6366f1");
        paper.Rect(100, 100, 100, 100).Attr("fill", "#ec4899");

        // Draw SVG onto 2D Canvas
        ctx.drawSvg(paper, 150, 150, 300, 300);

        var imgBytes = canvas.ToImageBytes();
        Assert.NotNull(imgBytes);
        Assert.True(imgBytes.Length > 0);
    }
    #endregion

    #region Sandboxed Jint JavaScript Canvas Execution Tests
    [Fact]
    public void TestJsCanvasScriptExecution()
    {
        var engine = new JsDrawingEngine();
        var jsCode = @"
            var canvas = createCanvas(800, 600);
            var ctx = canvas.getContext('2d');

            // Draw radial background
            var radGrad = ctx.createRadialGradient(400, 300, 50, 400, 300, 350);
            radGrad.addColorStop(0, '#312e81');
            radGrad.addColorStop(1, '#0f172a');
            ctx.fillStyle = radGrad;
            ctx.fillRect(0, 0, 800, 600);

            // Draw generative circles with globalCompositeOperation
            ctx.globalCompositeOperation = 'screen';
            var colors = ['#f43f5e', '#8b5cf6', '#06b6d4', '#10b981'];
            for (var i = 0; i < 4; i++) {
                ctx.beginPath();
                ctx.arc(300 + i * 60, 300, 70, 0, Math.PI * 2);
                ctx.fillStyle = colors[i];
                ctx.globalAlpha = 0.7;
                ctx.fill();
            }

            console.log('2D Canvas scene generated successfully');
            canvas;
        ";

        var result = engine.Execute(jsCode, 800, 600);
        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.ImageBytes);
        Assert.True(result.ImageBytes.Length > 0);
        Assert.Contains("[LOG] 2D Canvas scene generated successfully", result.Logs);
    }

    [Fact]
    public void TestJsSkiaProceduralNoiseScene()
    {
        var engine = new JsDrawingEngine();
        var jsCode = @"
            var canvas = createCanvas(600, 600);
            var ctx = canvas.getContext('2d');

            // Apply Skia procedural perlin noise
            ctx.fillStyle = Skia.Shader.perlinNoiseTurbulence(0.04, 0.04, 3, 999);
            ctx.fillRect(0, 0, 600, 600);

            // Add text overlay
            ctx.font = 'bold 36px Arial';
            ctx.fillStyle = '#ffffff';
            ctx.textAlign = 'center';
            ctx.shadowColor = 'rgba(0,0,0,0.8)';
            ctx.shadowBlur = 8;
            ctx.fillText('Procedural Terrain', 300, 300);

            canvas;
        ";

        var result = engine.Execute(jsCode, 600, 600);
        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.ImageBytes);
        Assert.True(result.ImageBytes.Length > 0);
    }
    #endregion
}
