namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using Polson.Drawing.Skia;
using Polson.MCPServer;
using SkiaSharp;
using Xunit;

public class LogoDesignToolkitTests : TestsRuntime
{
    #region Golden Ratio and Tangents Tests
    [Fact]
    public void TestGoldenCirclesCalculation()
    {
        var toolkit = new LogoDesignToolkit();
        var result = toolkit.CreateGoldenCircles(400f, 300f, 50f, 5, "growing");

        Assert.NotNull(result);
        Assert.True(result.ContainsKey("circles"));
        Assert.True(result.ContainsKey("phi"));
        Assert.True(result.ContainsKey("bounds"));

        var circles = (List<Dictionary<string, object?>>)result["circles"]!;
        Assert.Equal(5, circles.Count);

        var c0 = circles[0];
        Assert.Equal(50f, Convert.ToSingle(c0["radius"]));

        var c1 = circles[1];
        Assert.InRange(Convert.ToSingle(c1["radius"]), 80.8f, 81.0f); // 50 * 1.618034

        var c4 = circles[4];
        Assert.InRange(Convert.ToSingle(c4["radius"]), 342.5f, 343.0f); // 50 * phi^4
    }

    [Fact]
    public void TestGoldenSpiralRendering()
    {
        var toolkit = new LogoDesignToolkit();
        var canvas = new SkiaCanvas(800, 600);
        var ctx = canvas.getContext("2d");

        toolkit.DrawGoldenSpiral(ctx, 400f, 300f, 20f, 3f, new Dictionary<string, object?>
        {
            ["strokeColor"] = "#e2ae38",
            ["lineWidth"] = 2.5f,
            ["drawGoldenRectangles"] = true
        });

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestTangentBlendCalculation()
    {
        var toolkit = new LogoDesignToolkit();
        // Right angle at corner (100, 100) between (100, 200) and (200, 100)
        var p1 = new Dictionary<string, object?> { ["x"] = 100f, ["y"] = 200f };
        var corner = new Dictionary<string, object?> { ["x"] = 100f, ["y"] = 100f };
        var p2 = new Dictionary<string, object?> { ["x"] = 200f, ["y"] = 100f };

        var blend = toolkit.CreateTangentBlend(p1, corner, p2, 30f);

        Assert.NotNull(blend);
        Assert.InRange(Convert.ToSingle(blend["cornerAngleDeg"]), 89.9f, 90.1f);
        Assert.InRange(Convert.ToSingle(blend["sweepAngleDeg"]), 89.9f, 90.1f);
        Assert.InRange(Convert.ToSingle(blend["tangentDistance"]), 29.9f, 30.1f);

        var arcStart = (Dictionary<string, object?>)blend["arcStart"]!;
        var arcEnd = (Dictionary<string, object?>)blend["arcEnd"]!;

        Assert.Equal(100f, Convert.ToSingle(arcStart["x"]));
        Assert.Equal(130f, Convert.ToSingle(arcStart["y"]));

        Assert.Equal(130f, Convert.ToSingle(arcEnd["x"]));
        Assert.Equal(100f, Convert.ToSingle(arcEnd["y"]));
    }
    #endregion

    #region Grids and Monograms Tests
    [Fact]
    public void TestIsometricGridCalculationAndDrawing()
    {
        var toolkit = new LogoDesignToolkit();
        var grid = toolkit.CreateIsometricGrid(800f, 600f, 40f);

        Assert.NotNull(grid);
        Assert.True(Convert.ToSingle(grid["dx"]) > 30f);
        Assert.True(Convert.ToSingle(grid["dy"]) > 15f);

        var canvas = new SkiaCanvas(800, 600);
        var ctx = canvas.getContext("2d");

        toolkit.DrawIsometricGrid(ctx, 800f, 600f, 40f);
        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestPolarGridCalculationAndDrawing()
    {
        var toolkit = new LogoDesignToolkit();
        var canvas = new SkiaCanvas(600, 600);
        var ctx = canvas.getContext("2d");

        toolkit.DrawPolarGrid(ctx, 300f, 300f, new object[] { 50f, 100f, 150f, 200f }, 12);
        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestMonogramGridCalculations()
    {
        var toolkit = new LogoDesignToolkit();
        var grid3x3 = toolkit.CreateMonogramGrid("3x3", 300f, 50f, 50f);

        Assert.NotNull(grid3x3);
        var nodes3x3 = (Dictionary<string, object?>)grid3x3["nodes"]!;
        Assert.True(nodes3x3.ContainsKey("p00"));
        Assert.True(nodes3x3.ContainsKey("p22"));
        Assert.True(nodes3x3.ContainsKey("center"));

        var gridDiamond = toolkit.CreateMonogramGrid("diamond", 200f, 100f, 100f);
        var nodesDiamond = (Dictionary<string, object?>)gridDiamond["nodes"]!;
        Assert.True(nodesDiamond.ContainsKey("top"));
        Assert.True(nodesDiamond.ContainsKey("bottom"));
        Assert.True(nodesDiamond.ContainsKey("left"));
        Assert.True(nodesDiamond.ContainsKey("right"));
    }
    #endregion

    #region Graphic Devices and Containers Tests
    [Fact]
    public void TestSquirclePathAndDrawing()
    {
        var toolkit = new LogoDesignToolkit();
        using var path = toolkit.CreateSquirclePath(50f, 50f, 200f, 200f, 4.5f);
        Assert.NotNull(path);
        Assert.False(path.IsEmpty);

        var canvas = new SkiaCanvas(400, 400);
        var ctx = canvas.getContext("2d");

        toolkit.DrawSquircle(ctx, 50f, 50f, 300f, 300f, new Dictionary<string, object?>
        {
            ["fill"] = "#2563eb",
            ["stroke"] = "#1d4ed8",
            ["strokeWidth"] = 4.0f,
            ["exponent"] = 4.5f
        });

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestEmblemBadgeDrawing()
    {
        var toolkit = new LogoDesignToolkit();
        var canvas = new SkiaCanvas(800, 600);
        var ctx = canvas.getContext("2d");

        toolkit.DrawEmblemBadge(ctx, 150f, 300f, 100f, "shield", new Dictionary<string, object?> { ["fill"] = "#0f172a" });
        toolkit.DrawEmblemBadge(ctx, 400f, 300f, 100f, "hexagon", new Dictionary<string, object?> { ["fill"] = "#1e293b" });
        toolkit.DrawEmblemBadge(ctx, 650f, 300f, 100f, "scallop", new Dictionary<string, object?> { ["fill"] = "#0284c7" });

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestClearSpaceGuideDrawing()
    {
        var toolkit = new LogoDesignToolkit();
        var canvas = new SkiaCanvas(600, 600);
        var ctx = canvas.getContext("2d");

        toolkit.DrawClearSpaceGuide(ctx, new Dictionary<string, object?>
        {
            ["x"] = 200f,
            ["y"] = 200f,
            ["width"] = 200f,
            ["height"] = 200f
        }, 40f, new Dictionary<string, object?> { ["showLabels"] = true });

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }
    #endregion

    #region Optical Tuning and Perception Fixes Tests
    [Fact]
    public void TestBoneEffectCorrection()
    {
        var toolkit = new LogoDesignToolkit();
        var p1 = new Dictionary<string, object?> { ["x"] = 100f, ["y"] = 200f };
        var p2 = new Dictionary<string, object?> { ["x"] = 500f, ["y"] = 200f };

        var poly = toolkit.CorrectBoneEffect(p1, p2, 40f, 0.05f);

        Assert.NotNull(poly);
        Assert.Equal(6, poly.Count);

        // Middle points should have an outward bulge > half width (20)
        var topMid = poly[1];
        Assert.Equal(300f, Convert.ToSingle(topMid["x"]));
        Assert.InRange(Convert.ToSingle(topMid["y"]), 221.5f, 222.5f); // 200 + 22
    }

    [Fact]
    public void TestOvershootAndOpticalCenter()
    {
        var toolkit = new LogoDesignToolkit();
        var triOvershoot = toolkit.ComputeOvershoot(100f, "triangle");
        var circleOvershoot = toolkit.ComputeOvershoot(100f, "circle");

        Assert.InRange(triOvershoot, 2.7f, 2.9f);
        Assert.InRange(circleOvershoot, 1.9f, 2.1f);

        var bounds = new Dictionary<string, object?> { ["x"] = 100f, ["y"] = 100f, ["width"] = 200f, ["height"] = 200f };
        var optCenter = toolkit.ComputeOpticalCenter(bounds, "triangle");

        Assert.Equal(200f, Convert.ToSingle(optCenter["x"]));
        Assert.InRange(Convert.ToSingle(optCenter["y"]), 215f, 217f); // 100 + 200 * 0.58
    }
    #endregion

    #region Brand Sheets and Multi-Scale Tests
    [Fact]
    public void TestFaviconScaleLadderTest()
    {
        var toolkit = new LogoDesignToolkit();
        var canvas = new SkiaCanvas(1000, 400);
        var ctx = canvas.getContext("2d");

        Action<CanvasRenderingContext2D, float> drawMark = (c, size) =>
        {
            c.fillStyle = "#2563eb";
            c.beginPath();
            c.arc(size * 0.5f, size * 0.5f, size * 0.4f, 0, MathF.PI * 2f);
            c.fill();
        };

        toolkit.GenerateFaviconScaleTest(ctx, drawMark);

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestMonochromeAndBrandPresentationSheet()
    {
        var toolkit = new LogoDesignToolkit();
        var canvas = new SkiaCanvas(1200, 800);
        var ctx = canvas.getContext("2d");

        Action<CanvasRenderingContext2D, float> drawMark = (c, size) =>
        {
            c.fillStyle = "#38bdf8";
            c.fillRect(size * 0.2f, size * 0.2f, size * 0.6f, size * 0.6f);
        };

        toolkit.GenerateBrandPresentationSheet(ctx, new Dictionary<string, object?>
        {
            ["brandName"] = "AERO",
            ["tagline"] = "Cloud Infrastructure Platform",
            ["primaryColor"] = "#2563eb",
            ["secondaryColor"] = "#38bdf8",
            ["darkColor"] = "#0f172a",
            ["lightColor"] = "#f8fafc",
            ["drawMark"] = drawMark
        });

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }
    #endregion

    #region End-to-End JavaScript Execution Tests
    [Fact]
    public void TestJsEngineLogoExecution()
    {
        var js = @"
            const canvas = createCanvas(800, 600);
            const ctx = canvas.getContext('2d');

            // 1. Background
            ctx.fillStyle = '#0f172a';
            ctx.fillRect(0, 0, 800, 600);

            // 2. Draw Golden Spiral with rects
            Logo.drawGoldenSpiral(ctx, 400, 300, 20, 2.5, {
                strokeColor: '#38bdf8',
                lineWidth: 3.0,
                drawGoldenRectangles: true,
                rectStrokeColor: 'rgba(56, 189, 248, 0.3)'
            });

            // 3. Draw App Icon Squircle Container
            Logo.drawSquircle(ctx, 50, 50, 140, 140, {
                fill: '#1e293b',
                stroke: '#38bdf8',
                strokeWidth: 2,
                exponent: 4.5
            });

            // 4. Calculate Golden Circles
            const circles = Logo.createGoldenCircles(600, 200, 25, 4, 'growing');
            log('Created ' + circles.circles.length + ' golden circles');

            canvas;
        ";

        var engine = new JsDrawingEngine();
        var result = engine.Execute(js);

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.ImageBytes);
        Assert.True(result.ImageBytes.Length > 100);
        Assert.Contains("Created 4 golden circles", string.Join("\n", result.Logs));
    }

    [Fact]
    public void TestRenderNexusDynamicsArtwork()
    {
        var dir = AppContext.BaseDirectory;
        string? artworkPath = null;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = System.IO.Path.Combine(dir, "tests", "multi_agent", "logo_studio", "artwork.js");
            if (System.IO.File.Exists(candidate))
            {
                artworkPath = candidate;
                break;
            }
            dir = System.IO.Directory.GetParent(dir)?.FullName;
        }

        Assert.NotNull(artworkPath);
        var script = System.IO.File.ReadAllText(artworkPath);

        var engine = new JsDrawingEngine();
        var result = engine.Execute(script, 1200, 800, null, "webp", 90);

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.ImageBytes);
        Assert.True(result.ImageBytes.Length > 1000);

        var outPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(artworkPath)!, "output.webp");
        System.IO.File.WriteAllBytes(outPath, result.ImageBytes);
        Assert.True(System.IO.File.Exists(outPath));
    }

    [Fact]
    public void TestJsObjectInspection()
    {
        var js = @"
            const canvas = createCanvas(800, 600);
            const ctx = canvas.getContext('2d');
            Logo.generateBrandPresentationSheet(ctx, {
                brandName: 'NEXUS DYNAMICS',
                tagline: 'Autonomous Cloud',
                primaryColor: '#4f46e5',
                drawMark: function(c, s) {
                    c.fillStyle = '#ff0000';
                    c.fillRect(0, 0, s, s);
                }
            });
            canvas;
        ";

        var engine = new JsDrawingEngine();
        var result = engine.Execute(js);

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.ImageBytes);
    }

    [Fact]
    public void TestGetPropUnit()
    {
        var js = @"
            const obj = {
                brandName: 'NEXUS DYNAMICS',
                primaryColor: '#4f46e5'
            };
            obj;
        ";
        var engine = new JsDrawingEngine();
        var result = engine.Execute(js);
        var obj = result.ReturnValue;
        var brandName = LogoDesignToolkit.GetProp(obj, "brandName");
        Assert.Equal("NEXUS DYNAMICS", brandName?.ToString());
    }

    [Fact]
    public void TestInvokeCallbackUnit()
    {
        var js = @"
            let called = false;
            function testFn(c, s) {
                called = true;
            }
            testFn;
        ";
        var engine = new JsDrawingEngine();
        var result = engine.Execute(js);
        var fn = result.ReturnValue;
        var canvas = new Polson.Drawing.Skia.SkiaCanvas(100, 100);
        var ctx = canvas.getContext("2d");
        LogoDesignToolkit.InvokeCallback(fn, ctx, 50f);
    }

    [Fact]
    public void TestDrawNexusMarkDirect()
    {
        var js = @"
            const canvas = createCanvas(400, 400);
            const c = canvas.getContext('2d');
            
            function drawNexusMark(c, size) {
                c.save();
                const cx = size * 0.5;
                const cy = size * 0.5;
                const outerR = size * 0.40;
                const innerR = size * 0.22;
                const barW = size * 0.12;

                const grad = c.createLinearGradient(0, 0, size, size);
                grad.addColorStop(0, '#4f46e5');
                grad.addColorStop(0.5, '#06b6d4');
                grad.addColorStop(1, '#3b82f6');

                c.fillStyle = grad;
                c.beginPath();
                c.moveTo(cx - outerR, cy - barW * 0.5);
                c.arc(cx - innerR, cy - barW * 0.5, outerR * 0.45, Math.PI, 0, false);
                c.lineTo(cx + outerR, cy + barW * 0.5);
                c.arc(cx + innerR, cy + barW * 0.5, outerR * 0.45, 0, Math.PI, false);
                c.closePath();
                c.fill();

                c.fillStyle = '#06b6d4';
                c.beginPath();
                c.moveTo(cx + barW * 0.5, cy - outerR);
                c.arc(cx + barW * 0.5, cy - innerR, outerR * 0.45, -Math.PI * 0.5, Math.PI * 0.5, false);
                c.lineTo(cx - barW * 0.5, cy + outerR);
                c.arc(cx - barW * 0.5, cy + innerR, outerR * 0.45, Math.PI * 0.5, -Math.PI * 0.5, false);
                c.closePath();
                c.fill();

                c.fillStyle = '#0b0f19';
                c.beginPath();
                c.moveTo(cx, cy - innerR * 0.5);
                c.lineTo(cx + innerR * 0.5, cy);
                c.lineTo(cx, cy + innerR * 0.5);
                c.lineTo(cx - innerR * 0.5, cy);
                c.closePath();
                c.fill();

                c.restore();
            }

            drawNexusMark(c, 400);
            canvas;
        ";
        var engine = new JsDrawingEngine();
        var result = engine.Execute(js);
        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.ImageBytes);
        System.IO.File.WriteAllBytes("nexus_mark_direct.png", result.ImageBytes);
    }
    #endregion
}
