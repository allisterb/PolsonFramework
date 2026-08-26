namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using System.IO;
using Polson.Drawing.Skia;
using Polson.Drawing.Svg;
using Polson.MCPServer;
using Xunit;

public class LogoTypeToolkitTests : TestsRuntime
{
    #region Optical Kerning & Tracking Tests
    [Theory]
    [InlineData('H', 'H', 32f, 1.28f)]
    [InlineData('H', 'O', 32f, 0.80f)]
    [InlineData('O', 'O', 32f, 0.384f)]
    [InlineData('T', 'A', 32f, -2.88f)]
    [InlineData('A', 'V', 32f, -2.88f)]
    public void TestOpticalKerning(char left, char right, float fontSize, float expectedApprox)
    {
        var toolkit = new LogoTypeToolkit();
        var kerning = toolkit.ComputeOpticalKerning(left, right, fontSize);

        // Tolerant within 0.5px
        Assert.True(MathF.Abs(kerning - expectedApprox) < 0.5f,
            $"Expected ~{expectedApprox} for {left}|{right}, got {kerning}");
    }

    [Fact]
    public void TestWordmarkTracking()
    {
        var toolkit = new LogoTypeToolkit();

        var displayTracking = toolkit.ComputeWordmarkTracking(54f, false, "wordmark");
        Assert.True(displayTracking < 0, "Display wordmarks should have tight negative tracking");

        var taglineTracking = toolkit.ComputeWordmarkTracking(12f, true, "tagline");
        Assert.True(taglineTracking > 0.15f, "All-caps taglines should have wide generous tracking");
    }
    #endregion

    #region Typographic Scale Tests
    [Fact]
    public void TestTypographicScaleGeneration()
    {
        var toolkit = new LogoTypeToolkit();
        var scale = toolkit.CalculateTypographicScale(16f, "goldenRatio", 2, 4);

        Assert.NotNull(scale);
        Assert.Equal(16f, (float)scale["baseSize"]);
        Assert.True(scale.ContainsKey("body"));
        Assert.True(scale.ContainsKey("h1"));
        Assert.True(scale.ContainsKey("caption"));

        var steps = scale["steps"] as List<Dictionary<string, object?>>;
        Assert.NotNull(steps);
        Assert.True(steps.Count >= 7);
    }
    #endregion

    #region Font Pairing Matrix Tests
    [Fact]
    public void TestFontPairingEvaluation()
    {
        var toolkit = new LogoTypeToolkit();

        // 1. Contrasting pairing (Sans + Serif) -> High score
        var contrast = toolkit.EvaluateFontPairing("sansSerif", "modernSerif");
        Assert.Equal("contrasting", contrast["relationship"]);
        Assert.True((int)contrast["score"] >= 90);

        // 2. Concordant pairing (Sans + Sans) -> Safe score
        var concord = toolkit.EvaluateFontPairing("sansSerif", "sansSerif");
        Assert.Equal("concordant", concord["relationship"]);

        // 3. Conflicting pairing (Oldstyle + Modern) -> Low score
        var conflict = toolkit.EvaluateFontPairing("oldstyleSerif", "modernSerif");
        Assert.Equal("conflicting", conflict["relationship"]);
        Assert.True((int)conflict["score"] <= 50);
    }
    #endregion

    #region Ogee Curve Path Tests
    [Fact]
    public void TestOgeeCurveGeneration()
    {
        var toolkit = new LogoTypeToolkit();
        var path = toolkit.CreateOgeeCurvePath(0, 0, 100, 100, 0.5f, 30f);

        Assert.NotNull(path);
        Assert.StartsWith("M", path);
        Assert.Contains("C", path);

        var skPath = toolkit.CreateOgeeCurveSKPath(0, 0, 100, 100, 0.5f, 30f);
        Assert.NotNull(skPath);
        Assert.False(skPath.IsEmpty);
    }
    #endregion

    #region End-to-End Jint Brand Lockup Tests
    [Fact]
    public void TestBrandLockupExecutionInCanvas()
    {
        var js = @"
            const canvas = createCanvas(800, 400);
            const ctx = canvas.getContext('2d');

            ctx.fillStyle = '#0f172a';
            ctx.fillRect(0, 0, 800, 400);

            // Horizontal Brand Lockup
            ctx.drawWordmarkLockup(
                (c, size) => {
                    c.fillStyle = '#38bdf8';
                    c.beginPath();
                    c.arc(size * 0.5, size * 0.5, size * 0.45, 0, Math.PI * 2);
                    c.fill();
                },
                'NEXUS',
                'AUTONOMOUS SYSTEMS',
                {
                    layout: 'horizontal',
                    x: 60,
                    y: 150,
                    markSize: 70,
                    fontSize: 42,
                    taglineSize: 12,
                    primaryColor: '#ffffff',
                    taglineColor: '#38bdf8'
                }
            );

            canvas;
        ";

        var engine = new JsDrawingEngine();
        var result = engine.Execute(js, 800, 400, null, "webp", 90);

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.ImageBytes);
    }

    [Fact]
    public void TestOgeeCurveInSnap()
    {
        var js = @"
            const s = Snap(600, 400);
            const ogee = s.ogeeCurve(50, 200, 550, 200, 40).attr({
                fill: 'none',
                stroke: '#ec4899',
                strokeWidth: 3
            });
            s;
        ";

        var engine = new JsDrawingEngine();
        var result = engine.Execute(js, 600, 400, null, "webp", 90);

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.ImageBytes);
        Assert.NotEmpty(result.SvgXml);
        Assert.Contains("<path", result.SvgXml);
    }

    [Fact]
    public void RenderTypographyPresentationBoard()
    {
        var scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "tests", "agent", "multi_agent", "logo_studio", "typography_presentation_board.js");
        if (!File.Exists(scriptPath))
        {
            scriptPath = @"C:\Projects\Polson\tests\agent\multi_agent\logo_studio\typography_presentation_board.js";
        }

        var js = File.ReadAllText(scriptPath);
        var engine = new JsDrawingEngine();
        var result = engine.Execute(js, 1200, 850, null, "webp", 95);

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.ImageBytes);

        var outPath = @"C:\Users\Allister\.gemini\antigravity\brain\610af013-162d-4d8d-ae5d-370765877d6f\typography_presentation_board.webp";
        File.WriteAllBytes(outPath, result.ImageBytes!);
    }
    #endregion
}
