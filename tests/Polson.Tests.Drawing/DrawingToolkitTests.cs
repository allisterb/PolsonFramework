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

    [Fact]
    public void TestCastShadowProjectionAndDrawing()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var light = new Dictionary<string, object?> { ["x"] = 150f, ["y"] = 100f };
        var boxBounds = new Dictionary<string, object?> { ["x"] = 350f, ["y"] = 300f, ["width"] = 100f, ["height"] = 150f };

        var shadow = toolkit.projectCastShadow(light, 450f, boxBounds);
        Assert.NotNull(shadow);
        var poly = (IList<Dictionary<string, object?>>)shadow["shadowPolygon"]!;
        Assert.Equal(4, poly.Count);

        var canvas = new SkiaCanvas(800, 600);
        var ctx = canvas.getContext("2d");
        toolkit.drawCastShadow(ctx, shadow);

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestVolumetricSphereRendering()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var canvas = new SkiaCanvas(800, 600);
        var ctx = canvas.getContext("2d");

        toolkit.renderVolumetricSphere(ctx, 400f, 300f, 120f, new Dictionary<string, object?> { ["x"] = -0.6f, ["y"] = -0.6f });

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestVolumetricCylinderRendering()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var canvas = new SkiaCanvas(800, 600);
        var ctx = canvas.getContext("2d");

        toolkit.renderVolumetricCylinder(ctx, 300f, 200f, 150f, 250f);

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestThreePointLightingSetupAndRimLight()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var lighting = toolkit.createThreePointLighting();
        Assert.NotNull(lighting);
        Assert.True(lighting.ContainsKey("keyLight"));
        Assert.True(lighting.ContainsKey("fillLight"));
        Assert.True(lighting.ContainsKey("rimLight"));

        var canvas = new SkiaCanvas(800, 600);
        var ctx = canvas.getContext("2d");
        toolkit.drawRimLight(ctx, new Dictionary<string, object?> { ["x"] = 300f, ["y"] = 200f, ["width"] = 150f, ["height"] = 200f }, 135f, "#ffffff", 3f);

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestVolumetricSphereShaderCompilation()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var shader = toolkit.createVolumetricSphereShader();
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
    public void TestJavaScriptEngineLightingIntegration()
    {
        var engine = new JsDrawingEngine();
        const string script = @"
            const canvas = createCanvas(800, 600);
            const ctx = canvas.getContext('2d');

            // 1. Cast shadow
            ctx.drawCastShadow({ x: 120, y: 80 }, 500, { x: 300, y: 350, width: 120, height: 150 }, { opacity: 0.6 });

            // 2. Volumetric sphere with ground bounce & highlight
            ctx.renderVolumetricSphere(360, 420, 80, { x: -0.6, y: -0.6 }, {
                baseColor: '#c89a74',
                shadowColor: '#3c2415',
                highlightColor: '#fff5e6',
                bounceColor: '#6384a6'
            });

            // 3. Volumetric cylinder
            ctx.renderVolumetricCylinder(520, 320, 90, 180);

            // 4. Rim light kicker
            ctx.drawRimLight({ x: 520, y: 320, width: 90, height: 180 }, 135, '#ffffff', 3.0);

            canvas;
        ";

        var result = engine.Execute(script);
        Assert.NotNull(result);
        Assert.NotNull(result.ImageBytes);
        Assert.True(result.ImageBytes.Length > 1000);
    }

    [Fact]
    public void TestMannequinFigureCreation()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var fig = toolkit.createMannequinFigure(400f, 50f, 560f);

        Assert.NotNull(fig);
        Assert.Equal(70f, Convert.ToSingle(fig["headUnit"]));
        Assert.True(fig.ContainsKey("head"));
        Assert.True(fig.ContainsKey("ribcage"));
        Assert.True(fig.ContainsKey("pelvis"));
        Assert.True(fig.ContainsKey("leftArm"));
        Assert.True(fig.ContainsKey("rightLeg"));

        var leftArm = (Dictionary<string, object?>)fig["leftArm"]!;
        Assert.True(leftArm.ContainsKey("shoulder"));
        Assert.True(leftArm.ContainsKey("elbow"));
        Assert.True(leftArm.ContainsKey("wrist"));
        Assert.True(leftArm.ContainsKey("hand"));
    }

    [Fact]
    public void TestMannequinWireframeAndSolidRendering()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var fig = toolkit.createMannequinFigure(400f, 50f, 520f);

        var canvas = new SkiaCanvas(800, 600);
        var ctx = canvas.getContext("2d");

        // 1. Draw solid
        toolkit.drawMannequinSolid(ctx, fig);

        // 2. Draw wireframe on top
        toolkit.drawMannequinWireframe(ctx, fig);

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestTorsoMusculatureRendering()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var fig = toolkit.createMannequinFigure(400f, 50f, 520f);

        var canvas = new SkiaCanvas(800, 600);
        var ctx = canvas.getContext("2d");

        toolkit.drawTorsoMusculature(ctx, fig);

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestFacialExpressionsModifiers()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var head = toolkit.createLoomisHead(400f, 300f, 200f);

        var joyHead = toolkit.applyFacialExpression(head, "joy", 1.2f);
        Assert.NotNull(joyHead);
        Assert.Equal("joy", joyHead["expression"]);

        var angerHead = toolkit.applyFacialExpression(head, "anger", 1.5f);
        Assert.NotNull(angerHead);
        Assert.Equal("anger", angerHead["expression"]);

        var fearHead = toolkit.applyFacialExpression(head, "fear", 1.0f);
        Assert.NotNull(fearHead);
        Assert.Equal("fear", fearHead["expression"]);

        var sadHead = toolkit.applyFacialExpression(head, "sadness", 1.0f);
        Assert.NotNull(sadHead);
        Assert.Equal("sadness", sadHead["expression"]);
    }

    [Fact]
    public void TestJavaScriptEngineAnatomyIntegration()
    {
        var engine = new JsDrawingEngine();
        const string script = @"
            const canvas = createCanvas(800, 600);
            const ctx = canvas.getContext('2d');

            // 1. Create full-body 8-head mannequin
            const fig = Drawing.createMannequinFigure(400, 40, 520, {
                shoulderTiltDeg: -8,
                pelvicTiltDeg: 8,
                spineOffset: 10
            });

            // 2. Draw solid volumetric mannequin
            ctx.drawMannequin(fig, true, {
                fillColor: '#d6e4f0',
                strokeColor: '#2b4d6f'
            });

            // 3. Draw torso musculature contours
            ctx.drawTorsoMusculature(fig, { strokeColor: '#1a334d', strokeWidth: 2.2 });

            // 4. Test Loomis head with Expression
            const head = Drawing.createLoomisHead(150, 150, 140);
            const happyHead = Drawing.applyFacialExpression(head, 'joy', 1.2);
            Drawing.drawComicMouth(ctx, happyHead.mouthGuides);

            canvas;
        ";

        var result = engine.Execute(script);
        Assert.NotNull(result);
        Assert.NotNull(result.ImageBytes);
        Assert.True(result.ImageBytes.Length > 1000);
    }

    [Fact]
    public void TestCompositionGridCreation()
    {
        var toolkit = new ConstructiveDrawingToolkit();

        // 1. Rule of Thirds
        var thirds = toolkit.createCompositionGrid(900f, 600f, "ruleOfThirds");
        Assert.NotNull(thirds);
        Assert.Equal("ruleOfThirds", thirds["type"]);
        var pps = (Dictionary<string, object?>)thirds["powerPoints"]!;
        Assert.True(pps.ContainsKey("topLeft"));
        Assert.True(pps.ContainsKey("bottomRight"));

        // 2. Golden Ratio
        var golden = toolkit.createCompositionGrid(900f, 600f, "goldenRatio");
        Assert.NotNull(golden);
        Assert.Equal("goldenRatio", golden["type"]);

        // 3. Dynamic Symmetry
        var dynamicSym = toolkit.createCompositionGrid(900f, 600f, "dynamicSymmetry");
        Assert.NotNull(dynamicSym);
        Assert.Equal("dynamicSymmetry", dynamicSym["type"]);
    }

    [Fact]
    public void TestCompositionGridAndVignetteRendering()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var canvas = new SkiaCanvas(800, 600);
        var ctx = canvas.getContext("2d");

        // 1. Draw composition grid
        toolkit.drawCompositionGrid(ctx, "ruleOfThirds");

        // 2. Draw vignette
        toolkit.drawVignette(ctx, 800f, 600f);

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestNotanPaletteAndLeadingLines()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var pal = toolkit.createNotanPalette("classic3");
        Assert.NotNull(pal);
        Assert.True(pal.ContainsKey("dominantLight"));
        Assert.True(pal.ContainsKey("accentDark"));

        var canvas = new SkiaCanvas(800, 600);
        var ctx = canvas.getContext("2d");

        var origins = new List<object>
        {
            new Dictionary<string, object?> { ["x"] = 0f, ["y"] = 0f },
            new Dictionary<string, object?> { ["x"] = 800f, ["y"] = 0f },
            new Dictionary<string, object?> { ["x"] = 0f, ["y"] = 600f }
        };
        var focal = new Dictionary<string, object?> { ["x"] = 400f, ["y"] = 300f };

        toolkit.drawLeadingLines(ctx, origins, focal);

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestProportionalSubdivision()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var bounds = new Dictionary<string, object?> { ["x"] = 0f, ["y"] = 0f, ["width"] = 1000f, ["height"] = 500f };

        var sub = toolkit.subdivideProportions(bounds, "horizontal");
        Assert.NotNull(sub);
        Assert.True(sub.ContainsKey("big"));
        Assert.True(sub.ContainsKey("medium"));
        Assert.True(sub.ContainsKey("small"));

        var big = (Dictionary<string, object?>)sub["big"]!;
        Assert.Equal(700f, Convert.ToSingle(big["width"]));
    }

    [Fact]
    public void TestJavaScriptEngineCompositionIntegration()
    {
        var engine = new JsDrawingEngine();
        const string script = @"
            const canvas = createCanvas(800, 600);
            const ctx = canvas.getContext('2d');

            // 1. Draw Rule of Thirds armature
            ctx.drawCompositionGrid('ruleOfThirds');

            // 2. Draw leading lines to focal point
            ctx.drawLeadingLines([
                { x: 50, y: 550 },
                { x: 750, y: 550 }
            ], { x: 533, y: 200 }); // Top-Right Power Point

            // 3. Proportional layout
            const layout = Drawing.subdivideProportions({ x: 0, y: 0, width: 800, height: 600 }, 'horizontal');

            // 4. Cinematic vignette
            ctx.drawVignette({ intensity: 0.65 });

            canvas;
        ";

        var result = engine.Execute(script);
        Assert.NotNull(result);
        Assert.NotNull(result.ImageBytes);
        Assert.True(result.ImageBytes.Length > 1000);
    }
    #endregion
}
