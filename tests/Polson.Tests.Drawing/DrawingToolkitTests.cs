namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using Polson.Drawing.Skia;
using Polson.MCPServer;
using SkiaSharp;
using Xunit;

public class DrawingToolkitTests : TestsRuntime
{
    #region Constructive Toolkit Tests
    [Fact]
    public void TestLoomisHeadParametricCalculation()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var head = toolkit.createLoomisHead(500, 400, 200, 35f, 0f);

        Assert.NotNull(head);
        Assert.True(head.ContainsKey("unit"));
        Assert.True(head.ContainsKey("nearEye"));
        Assert.True(head.ContainsKey("farEye"));
        Assert.True(head.ContainsKey("noseWedge"));
        Assert.True(head.ContainsKey("mouthGuides"));
        Assert.True(head.ContainsKey("jaw"));

        var unit = (Dictionary<string, object?>)head["unit"]!;
        Assert.Equal(200f, Convert.ToSingle(unit["H"]));
        Assert.Equal(144f, Convert.ToSingle(unit["W"])); // 200 * 0.72

        var chin = (Dictionary<string, object?>)head["chin"]!;
        Assert.True(Convert.ToSingle(chin["y"]) > 400f);
    }

    [Fact]
    public void TestLoomisWireframeRendering()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var head = toolkit.createLoomisHead(400, 300, 220, 30f, 0f);

        var canvas = new SkiaCanvas(800, 600);
        var ctx = canvas.getContext("2d");

        toolkit.drawLoomisWireframe(ctx, head);

        // Verify non-empty raster rendering
        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestComicEyeNoseMouthRendering()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var head = toolkit.createLoomisHead(400, 300, 200, 35f, 0f);

        var canvas = new SkiaCanvas(800, 600);
        var ctx = canvas.getContext("2d");

        toolkit.drawComicEye(ctx, head["nearEye"]!, false);
        toolkit.drawComicEye(ctx, head["farEye"]!, true);
        toolkit.drawComicNose(ctx, head["noseWedge"]!);
        toolkit.drawComicMouth(ctx, head["mouthGuides"]!);

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestTaperedStrokeRendering()
    {
        var canvas = new SkiaCanvas(400, 400);
        var ctx = canvas.getContext("2d");

        ctx.drawTaperedStroke(50, 50, 150, 20, 250, 180, 350, 350, 8f, "#0a0a0c");

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestFeatheringAndCrossContour()
    {
        var canvas = new SkiaCanvas(400, 400);
        var ctx = canvas.getContext("2d");

        ctx.drawFeathering(100, 100, 45f, 10, 30f, 6f, "#0a0a0c", 1.5f);
        ctx.drawCrossContourHatch(200, 200, 80f, 120f, 0f, MathF.PI, 6, "#555555", 1.2f);

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void Test3DHairRibbonRendering()
    {
        var canvas = new SkiaCanvas(500, 500);
        var ctx = canvas.getContext("2d");

        ctx.drawHairRibbon(
            new Dictionary<string, object?> { ["x"] = 250f, ["y"] = 100f },
            new Dictionary<string, object?> { ["x"] = 50f, ["y"] = 350f },
            40f,
            25f,
            "#c65727",
            "#7e2c12",
            "#0a0a0c",
            2.5f
        );

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestHalftoneDotShaderPreset()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var shader = toolkit.createHalftoneDotShader(new Dictionary<string, object?>
        {
            ["dotSpacing"] = 6.0f,
            ["shadowColor"] = "#b06f4c",
            ["resolution"] = new[] { 800f, 600f }
        });

        Assert.NotNull(shader);

        var canvas = new SkiaCanvas(800, 600);
        var ctx = canvas.getContext("2d");
        ctx.fillStyle = shader;
        ctx.fillRect(0, 0, 800, 600);

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestRopeAndCloudShaderPresets()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var ropeShader = toolkit.createRopeFiberShader();
        var cloudShader = toolkit.createAtmosphericCloudShader();

        Assert.NotNull(ropeShader);
        Assert.NotNull(cloudShader);

        var canvas = new SkiaCanvas(400, 400);
        var ctx = canvas.getContext("2d");
        ctx.fillStyle = ropeShader;
        ctx.fillRect(0, 0, 200, 400);
        ctx.fillStyle = cloudShader;
        ctx.fillRect(200, 0, 200, 400);

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestPlumbAndRelativeDistanceMeasurements()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var pt1 = new Dictionary<string, object?> { ["x"] = 100f, ["y"] = 50f };
        var pt2 = new Dictionary<string, object?> { ["x"] = 105f, ["y"] = 250f };
        var pt3 = new Dictionary<string, object?> { ["x"] = 140f, ["y"] = 250f };

        var check1 = toolkit.verifyPlumbAlignment(pt1, pt2, 10f);
        Assert.True((bool)check1["aligned"]!);

        var check2 = toolkit.verifyPlumbAlignment(pt1, pt3, 10f);
        Assert.False((bool)check2["aligned"]!);

        var relDist = toolkit.computeRelativeDistance(200f, pt1, pt2);
        Assert.True(relDist > 0.9f && relDist < 1.1f);
    }

    [Fact]
    public void TestJavaScriptEngineDrawingGlobals()
    {
        var engine = new JsDrawingEngine();
        const string script = @"
            const canvas = createCanvas(800, 600);
            const ctx = canvas.getContext('2d');

            // 1. Loomis Parametric Head
            const head = Drawing.createLoomisHead(400, 300, 220, 35, 0);
            Drawing.drawLoomisWireframe(ctx, head);

            // 2. Comic Features
            Drawing.drawComicEye(ctx, head.nearEye, false);
            Drawing.drawComicEye(ctx, head.farEye, true);
            Drawing.drawComicNose(ctx, head.noseWedge);
            Drawing.drawComicMouth(ctx, head.mouthGuides);

            // 3. Hair Ribbon
            Drawing.drawHairRibbon(ctx, {x: 350, y: 200}, {x: 120, y: 380}, 45, 28, '#c65727', '#7e2c12', '#0a0a0c', 2.5);

            // 4. Halftone Dot Cel-Shading
            const shadowShader = Drawing.createHalftoneDotShader({ dotSpacing: 6, shadowColor: '#b06f4c' });
            ctx.fillStyle = shadowShader;
            ctx.fillRect(300, 350, 100, 100);

            // 5. CSI Curve Aliases & Tapered Stroke
            ctx.beginPath();
            ctx.moveTo(100, 100);
            ctx.cCurveTo(200, 50, 300, 100);
            ctx.sCurveTo(350, 150, 400, 80, 500, 120);
            ctx.stroke();

            ctx.drawTaperedStroke(50, 500, 150, 450, 250, 550, 350, 500, 6, '#0a0a0c');
            ctx.drawFeathering(400, 450, -45, 8, 25, 5, '#0a0a0c', 1.2);

            canvas;
        ";

        var result = engine.Execute(script);
        Assert.NotNull(result);
        Assert.NotNull(result.ImageBytes);
        Assert.True(result.ImageBytes.Length > 1000);
    }

    [Fact]
    public void TestPerspectiveGridCalculation()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var grid2p = toolkit.createPerspectiveGrid(new Dictionary<string, object?>
        {
            ["type"] = "2point",
            ["horizonY"] = 300f,
            ["centerOfVisionX"] = 400f,
            ["focalLength"] = 800f,
            ["cameraAngleDeg"] = 45f
        });

        Assert.NotNull(grid2p);
        Assert.Equal("2point", grid2p["type"]);
        var vpL = (Dictionary<string, object?>)grid2p["vpL"]!;
        var vpR = (Dictionary<string, object?>)grid2p["vpR"]!;

        // In 45 degree angle with d=800, vpL = 400 - 800 = -400, vpR = 400 + 800 = 1200
        Assert.Equal(-400f, Convert.ToSingle(vpL["x"]));
        Assert.Equal(1200f, Convert.ToSingle(vpR["x"]));

        var canvas = new SkiaCanvas(800, 600);
        var ctx = canvas.getContext("2d");
        toolkit.drawPerspectiveGrid(ctx, grid2p);

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestPerspectiveBoxProjection()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var grid = toolkit.createPerspectiveGrid(new Dictionary<string, object?>
        {
            ["horizonY"] = 250f,
            ["centerOfVisionX"] = 400f,
            ["focalLength"] = 800f,
            ["cameraAngleDeg"] = 40f
        });

        var box = toolkit.createPerspectiveBox(grid, 400f, 450f, 180f, 120f, 150f);
        Assert.NotNull(box);

        var verts = (IList<Dictionary<string, object?>>)box["vertices"]!;
        Assert.Equal(8, verts.Count);

        var faces = (Dictionary<string, object?>)box["faces"]!;
        Assert.True(faces.ContainsKey("top"));
        Assert.True(faces.ContainsKey("left"));
        Assert.True(faces.ContainsKey("right"));

        var canvas = new SkiaCanvas(800, 600);
        var ctx = canvas.getContext("2d");
        toolkit.drawPerspectiveBox(ctx, box, new Dictionary<string, object?> { ["drawHiddenLines"] = true });

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestPerspectiveCylinderRendering()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var grid = toolkit.createPerspectiveGrid(new Dictionary<string, object?>
        {
            ["horizonY"] = 200f,
            ["centerOfVisionX"] = 400f,
            ["focalLength"] = 800f
        });

        var canvas = new SkiaCanvas(800, 600);
        var ctx = canvas.getContext("2d");
        toolkit.drawPerspectiveCylinder(ctx, grid, 400f, 420f, 60f, 140f);

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestPerspectiveQuadSubdivision()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var quad = new List<Dictionary<string, object?>>
        {
            new() { ["x"] = 100f, ["y"] = 300f },
            new() { ["x"] = 300f, ["y"] = 250f },
            new() { ["x"] = 300f, ["y"] = 450f },
            new() { ["x"] = 100f, ["y"] = 500f }
        };

        var cells = toolkit.subdividePerspectiveQuad(quad, 3, 2);
        Assert.NotNull(cells);
        Assert.Equal(6, cells.Count); // 3 x 2 cells
        Assert.Equal(4, cells[0].Count); // each cell has 4 vertices
    }

    [Fact]
    public void TestPerspectiveConvergenceVerification()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var vp = new Dictionary<string, object?> { ["x"] = 1000f, ["y"] = 300f };

        var goodLines = new List<object>
        {
            new List<object> { new Dictionary<string, object?> { ["x"] = 100f, ["y"] = 500f }, new Dictionary<string, object?> { ["x"] = 550f, ["y"] = 400f } },
            new List<object> { new Dictionary<string, object?> { ["x"] = 200f, ["y"] = 600f }, new Dictionary<string, object?> { ["x"] = 600f, ["y"] = 450f } }
        };

        var check = toolkit.verifyPerspectiveConvergence(goodLines, vp, 5f);
        Assert.True((bool)check["passed"]!);
    }

    [Fact]
    public void TestJavaScriptEnginePerspectiveIntegration()
    {
        var engine = new JsDrawingEngine();
        const string script = @"
            const canvas = createCanvas(800, 600);
            const ctx = canvas.getContext('2d');

            // 1. Create Grid
            const grid = Drawing.createPerspectiveGrid({
                type: '2point',
                horizonY: 280,
                centerOfVisionX: 400,
                focalLength: 750,
                cameraAngleDeg: 40
            });
            ctx.drawPerspectiveGrid(grid);

            // 2. 3D Boxes
            ctx.drawPerspectiveBox(grid, 380, 480, 140, 110, 160, {
                topFill: '#f4ebd0',
                leftFill: '#cbb69d',
                rightFill: '#8d7862',
                strokeColor: '#3d2e1e'
            });

            // 3. Cylinder
            ctx.drawPerspectiveCylinder(grid, 580, 460, 45, 90);

            canvas;
        ";

        var result = engine.Execute(script);
        Assert.NotNull(result);
        Assert.NotNull(result.ImageBytes);
        Assert.True(result.ImageBytes.Length > 1000);
    }
    #endregion
}
