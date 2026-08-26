namespace Polson.Tests.Drawing;

using System;
using System.IO;
using System.Linq;
using Polson.Drawing.Svg;
using Polson.MCPServer;
using Xunit;

public class VectorLogoToolkitTests : TestsRuntime
{
    #region Mathematical Path Generator Tests
    [Fact]
    public void TestCreateSquirclePath()
    {
        var path = VectorLogoToolkit.CreateSquirclePath(10, 10, 100, 100, 4.5f);
        Assert.NotNull(path);
        Assert.StartsWith("M", path);
        Assert.EndsWith("Z", path);
        Assert.Contains("L", path);
    }

    [Fact]
    public void TestCreateGoldenSpiralPath()
    {
        var path = VectorLogoToolkit.CreateGoldenSpiralPath(100, 100, 10, 3f);
        Assert.NotNull(path);
        Assert.StartsWith("M", path);
        Assert.Contains("L", path);
    }

    [Theory]
    [InlineData("shield")]
    [InlineData("hexagon")]
    [InlineData("diamond")]
    [InlineData("scallop")]
    [InlineData("circle")]
    public void TestCreateEmblemBadgePaths(string style)
    {
        var path = VectorLogoToolkit.CreateEmblemBadgePath(150, 150, 100, 120, style);
        Assert.NotNull(path);
        Assert.StartsWith("M", path);
        Assert.EndsWith("Z", path);
    }

    [Fact]
    public void TestCreateTangentFilletPath()
    {
        var path = VectorLogoToolkit.CreateTangentFilletPath(0, 100, 100, 100, 100, 0, 20f);
        Assert.NotNull(path);
        Assert.StartsWith("M", path);
        Assert.Contains("A", path);
    }

    [Fact]
    public void TestCreateBoneEffectPath()
    {
        var path = VectorLogoToolkit.CreateBoneEffectPath(0, 0, 100, 0, 5f);
        Assert.NotNull(path);
        Assert.StartsWith("M", path);
        Assert.Contains("Q", path);
    }
    #endregion

    #region SnapPaper Element Builder Tests
    [Fact]
    public void TestSnapPaperSquircle()
    {
        var paper = Snap.Create(400, 400);
        var squircle = paper.Squircle(20, 20, 100, 100, 4.5f);
        Assert.NotNull(squircle);
        Assert.NotEmpty(squircle.D);

        var xml = paper.ToString();
        Assert.Contains("<path", xml);
    }

    [Fact]
    public void TestSnapPaperGoldenSpiral()
    {
        var paper = Snap.Create(400, 400);
        var spiral = paper.GoldenSpiral(200, 200, 15, 2.5f);
        Assert.NotNull(spiral);
        Assert.NotEmpty(spiral.D);
    }

    [Fact]
    public void TestSnapPaperEmblemBadge()
    {
        var paper = Snap.Create(400, 400);
        var badge = paper.EmblemBadge(200, 200, 150, 180, "shield");
        Assert.NotNull(badge);
        Assert.NotEmpty(badge.D);
    }

    [Fact]
    public void TestSnapPaperGridsAndMatrices()
    {
        var paper = Snap.Create(600, 600);

        var circles = paper.GoldenCircles(300, 300, 100, 4);
        Assert.NotNull(circles);

        var iso = paper.IsometricGrid(600, 600, 50);
        Assert.NotNull(iso);

        var polar = paper.PolarGrid(300, 300, 200, 4, 8);
        Assert.NotNull(polar);

        var mono = paper.MonogramMatrix(100, 100, 200, 200, "3x3");
        Assert.NotNull(mono);

        var guide = paper.ClearSpaceGuide(200, 200, 100, 100, 24);
        Assert.NotNull(guide);

        var xml = paper.ToString();
        Assert.Contains("<g", xml);
        Assert.Contains("<circle", xml);
        Assert.Contains("<line", xml);
    }

    [Fact]
    public void TestSnapPathApi()
    {
        var d = Snap.path.squircle(0, 0, 100, 100);
        Assert.NotEmpty(d);

        var totalLen = Snap.path.getTotalLength(d);
        Assert.True(totalLen > 0);

        var pt = Snap.path.getPointAtLength(d, totalLen * 0.5f);
        Assert.True(pt.X >= 0);
    }
    #endregion

    #region End-to-End Jint JS Execution Tests
    [Fact]
    public void TestJsVectorLogoExecution()
    {
        var js = @"
            const s = Snap(800, 600);
            
            // 1. Background Squircle
            const bg = s.squircle(50, 50, 200, 200, 4.5).attr({
                fill: '#0f172a',
                stroke: '#38bdf8',
                strokeWidth: 2
            });

            // 2. Emblem Badge
            const badge = s.emblemBadge(450, 150, 160, 180, 'shield').attr({
                fill: '#4f46e5',
                opacity: 0.9
            });

            // 3. Golden Spiral
            const spiral = s.goldenSpiral(450, 400, 10, 2).attr({
                fill: 'none',
                stroke: '#f59e0b',
                strokeWidth: 3
            });

            // 4. Monogram Matrix Guide
            const mono = s.monogramMatrix(100, 350, 150, 150, '3x3');

            s;
        ";

        var engine = new JsDrawingEngine();
        var result = engine.Execute(js, 800, 600, null, "webp", 90);

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.ImageBytes);
        Assert.NotEmpty(result.SvgXml);
        Assert.Contains("<path", result.SvgXml);
        Assert.Contains("<svg", result.SvgXml);
    }

    [Fact]
    public void TestRenderNexusDynamicsVectorAsset()
    {
        var dir = AppContext.BaseDirectory;
        string? scriptPath = null;
        for (var i = 0; i < 10 && dir != null; i++)
        {
            var candidate1 = Path.Combine(dir, "tests", "agent", "multi_agent", "logo_studio", "artwork_vector.js");
            var candidate2 = Path.Combine(dir, "agent", "multi_agent", "logo_studio", "artwork_vector.js");
            var candidate3 = Path.Combine(dir, "logo_studio", "artwork_vector.js");

            if (File.Exists(candidate1)) { scriptPath = candidate1; break; }
            if (File.Exists(candidate2)) { scriptPath = candidate2; break; }
            if (File.Exists(candidate3)) { scriptPath = candidate3; break; }

            dir = Directory.GetParent(dir)?.FullName;
        }

        Assert.True(scriptPath != null && File.Exists(scriptPath), $"Script not found starting from {AppContext.BaseDirectory}");

        var js = File.ReadAllText(scriptPath);
        var engine = new JsDrawingEngine();
        var result = engine.Execute(js, 800, 800, null, "webp", 95);

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.ImageBytes);
        Assert.NotEmpty(result.SvgXml);

        var artifactDir = @"C:\Users\Allister\.gemini\antigravity\brain\610af013-162d-4d8d-ae5d-370765877d6f";
        if (Directory.Exists(artifactDir))
        {
            File.WriteAllBytes(Path.Combine(artifactDir, "nexus_vector_logo.webp"), result.ImageBytes);
            File.WriteAllText(Path.Combine(artifactDir, "nexus_vector_logo.svg"), result.SvgXml);
        }
    }
    #endregion
}
