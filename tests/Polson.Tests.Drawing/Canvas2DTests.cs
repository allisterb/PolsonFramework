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

        var ctx = canvas.GetContext("2d");
        Assert.NotNull(ctx);

        ctx.FillStyle = "#ff0000";
        ctx.FillRect(10, 10, 100, 80);

        ctx.StrokeStyle = "#0000ff";
        ctx.LineWidth = 4f;
        ctx.StrokeRect(150, 20, 100, 50);

        ctx.ClearRect(20, 30, 20, 20);

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
        var ctx = canvas.GetContext("2d");

        ctx.BeginPath();
        ctx.MoveTo(50, 50);
        ctx.LineTo(200, 50);
        ctx.LineTo(200, 200);
        ctx.ClosePath();
        ctx.FillStyle = "#10b981";
        ctx.Fill();

        ctx.BeginPath();
        ctx.Arc(350, 200, 50, 0, MathF.PI * 2f);
        ctx.FillStyle = "#3b82f6";
        ctx.Fill();

        ctx.BeginPath();
        ctx.RoundRect(50, 300, 150, 100, 20f);
        ctx.StrokeStyle = "#f59e0b";
        ctx.LineWidth = 3f;
        ctx.Stroke();

        var imgBytes = canvas.ToImageBytes();
        Assert.True(imgBytes.Length > 0);
    }
    #endregion

    #region State Stack and Transforms Tests
    [Fact]
    public void TestStateStackAndTransforms()
    {
        var canvas = new SkiaCanvas(400, 400);
        var ctx = canvas.GetContext("2d");

        ctx.FillStyle = "#ff0000";
        ctx.Save();

        ctx.Translate(100, 100);
        ctx.Rotate(MathF.PI / 4f);
        ctx.Scale(2f, 2f);
        ctx.FillStyle = "#00ff00";
        ctx.FillRect(0, 0, 50, 50);

        ctx.Restore();
        ctx.FillRect(0, 0, 20, 20); // Should be red (#ff0000)

        var bmp = canvas.ToBitmap();
        Assert.NotNull(bmp);
        Assert.Equal(400, bmp.Width);
    }
    #endregion

    #region Gradients and Patterns Tests
    [Fact]
    public void TestLinearAndRadialGradients()
    {
        var canvas = new SkiaCanvas(600, 400);
        var ctx = canvas.GetContext("2d");

        // Linear gradient
        var linGrad = ctx.CreateLinearGradient(0, 0, 600, 0);
        linGrad.AddColorStop(0f, "#ff0000");
        linGrad.AddColorStop(0.5f, "#00ff00");
        linGrad.AddColorStop(1f, "#0000ff");

        ctx.FillStyle = linGrad;
        ctx.FillRect(0, 0, 600, 200);

        // Radial gradient
        var radGrad = ctx.CreateRadialGradient(300, 300, 20, 300, 300, 100);
        radGrad.AddColorStop(0f, "#ffffff");
        radGrad.AddColorStop(1f, "#000000");

        ctx.FillStyle = radGrad;
        ctx.FillRect(0, 200, 600, 200);

        var dataUri = canvas.ToDataUri();
        Assert.StartsWith("data:image/webp;base64,", dataUri);
    }
    #endregion

    #region Text Rendering and Metrics Tests
    [Fact]
    public void TestTextRenderingAndMetrics()
    {
        var canvas = new SkiaCanvas(600, 400);
        var ctx = canvas.GetContext("2d");

        ctx.Font = "bold 32px sans-serif";
        ctx.FillStyle = "#ffffff";
        ctx.TextAlign = "center";
        ctx.TextBaseline = "middle";

        var metrics = ctx.MeasureText("Skia Canvas 2D");
        Assert.NotNull(metrics);
        Assert.True(Convert.ToSingle(metrics["width"]) > 0);

        ctx.FillText("Skia Canvas 2D", 300, 200);

        var imgBytes = canvas.ToImageBytes();
        Assert.True(imgBytes.Length > 0);
    }
    #endregion

    #region Skia Procedural Shaders and Filters Tests
    [Fact]
    public void TestSkiaProceduralNoiseAndFilters()
    {
        var canvas = new SkiaCanvas(400, 400);
        var ctx = canvas.GetContext("2d");
        var skia = new SkiaApi();

        // Procedural Perlin noise
        var noise = skia.Shader.PerlinNoiseTurbulence(0.05f, 0.05f, 4, 12345);
        ctx.FillStyle = noise;
        ctx.FillRect(0, 0, 400, 400);

        // Native Gaussian Blur filter
        ctx.Filter = skia.ImageFilter.Blur(5f, 5f);
        ctx.FillStyle = "#ffffff";
        ctx.FillRect(100, 100, 200, 200);

        var imgBytes = canvas.ToImageBytes();
        Assert.True(imgBytes.Length > 0);
    }
    #endregion

    #region Cross-Engine SVG Layering Tests
    [Fact]
    public void TestCrossEngineSvgDrawing()
    {
        var canvas = new SkiaCanvas(600, 600);
        var ctx = canvas.GetContext("2d");

        // Background in Canvas 2D
        ctx.FillStyle = "#0f172a";
        ctx.FillRect(0, 0, 600, 600);

        // Vector SVG object created via Snap
        var paper = Snap.Create(300, 300);
        paper.Circle(150, 150, 100).Attr("fill", "#6366f1");
        paper.Rect(100, 100, 100, 100).Attr("fill", "#ec4899");

        // Draw SVG onto 2D Canvas
        ctx.DrawSvg(paper, 150, 150, 300, 300);

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
