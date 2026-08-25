namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using System.Drawing;
using Polson.Drawing.Skia;
using Polson.Drawing.Svg;
using Polson.MCPServer;
using SkiaSharp;
using Xunit;

public class DrawingTests : TestsRuntime
{
    #region SnapPaper and Primitives Tests
    [Fact]
    public void TestCreatePaperAndPrimitives()
    {
        var paper = Snap.Create(800, 600);
        Assert.NotNull(paper);
        Assert.Equal(800f, paper.Width);
        Assert.Equal(600f, paper.Height);

        var rect = paper.Rect(10, 20, 100, 50, 5, 5);
        Assert.NotNull(rect);
        Assert.Equal("rect", rect.Type);
        Assert.Equal(10f, rect.X);
        Assert.Equal(20f, rect.Y);
        Assert.Equal(100f, rect.Width);
        Assert.Equal(50f, rect.Height);

        var circle = paper.Circle(100, 100, 40);
        Assert.NotNull(circle);
        Assert.Equal("circle", circle.Type);
        Assert.Equal(100f, circle.Cx);
        Assert.Equal(100f, circle.Cy);
        Assert.Equal(40f, circle.R);

        var ellipse = paper.Ellipse(200, 150, 60, 30);
        Assert.NotNull(ellipse);
        Assert.Equal("ellipse", ellipse.Type);
        Assert.Equal(200f, ellipse.Cx);
        Assert.Equal(150f, ellipse.Cy);
        Assert.Equal(60f, ellipse.Rx);
        Assert.Equal(30f, ellipse.Ry);

        var line = paper.Line(0, 0, 100, 100);
        Assert.NotNull(line);
        Assert.Equal("line", line.Type);
        Assert.Equal(0f, line.X1);
        Assert.Equal(100f, line.X2);

        var path = paper.Path("M10 10 L50 50 L10 50 Z");
        Assert.NotNull(path);
        Assert.Equal("path", path.Type);
        Assert.NotEmpty(path.D);

        var group = paper.G(rect, circle);
        Assert.NotNull(group);
        Assert.Equal("g", group.Type);
        Assert.Equal(2, group.Children().Count);
    }
    #endregion

    #region Attribute Parsing and Manipulation Tests
    [Fact]
    public void TestAttributesAndStyles()
    {
        var paper = Snap.Create(500, 500);
        var rect = paper.Rect(0, 0, 100, 100);

        rect.Attr("fill", "#ff0000");
        rect.Attr("stroke", "#0000ff");
        rect.Attr("stroke-width", 4);
        rect.Attr("opacity", 0.5f);

        Assert.Equal("#FF0000", rect.Attr("fill")?.ToString());
        Assert.Equal("#0000FF", rect.Attr("stroke")?.ToString());
        Assert.Equal(4f, Convert.ToSingle(rect.Attr("stroke-width")));
        Assert.Equal(0.5f, Convert.ToSingle(rect.Attr("opacity")));

        // Dictionary attr application
        rect.Attr(new Dictionary<string, object?>
        {
            ["fill"] = "#00ff00",
            ["stroke-width"] = 8
        });

        Assert.Equal("#00FF00", rect.Attr("fill")?.ToString());
        Assert.Equal(8f, Convert.ToSingle(rect.Attr("stroke-width")));
    }

    [Fact]
    public void TestColorParsing()
    {
        var colHex3 = SnapAttributes.ParseColor("#f00");
        Assert.Equal(Color.FromArgb(255, 255, 0, 0), colHex3);

        var colHex6 = SnapAttributes.ParseColor("#00ff00");
        Assert.Equal(Color.FromArgb(255, 0, 255, 0), colHex6);

        var colRgb = SnapAttributes.ParseColor("rgb(0, 0, 255)");
        Assert.Equal(Color.FromArgb(255, 0, 0, 255), colRgb);

        var colRgba = SnapAttributes.ParseColor("rgba(255, 255, 0, 0.5)");
        Assert.Equal(Color.FromArgb(127, 255, 255, 0), colRgba);

        var colNamed = SnapAttributes.ParseColor("cyan");
        Assert.Equal(Color.Cyan.ToArgb(), colNamed.ToArgb());
    }
    #endregion

    #region Transform Parser Tests
    [Fact]
    public void TestTransformShorthand()
    {
        var matrix1 = SnapTransformParser.ParseToMatrix("t10,20");
        Assert.Equal(10f, matrix1.E);
        Assert.Equal(20f, matrix1.F);

        var matrix2 = SnapTransformParser.ParseToMatrix("s2,3");
        Assert.Equal(2f, matrix2.A);
        Assert.Equal(3f, matrix2.D);

        var matrix3 = SnapTransformParser.ParseToMatrix("r90");
        Assert.True(MathF.Abs(matrix3.A) < 1e-4f);
        Assert.True(MathF.Abs(matrix3.B - 1f) < 1e-4f);
        Assert.True(MathF.Abs(matrix3.C - (-1f)) < 1e-4f);
        Assert.True(MathF.Abs(matrix3.D) < 1e-4f);

        // Compound transform
        var matrixCompound = SnapTransformParser.ParseToMatrix("t10,20s2");
        Assert.Equal(2f, matrixCompound.A);
        Assert.Equal(2f, matrixCompound.D);
        Assert.Equal(10f, matrixCompound.E);
        Assert.Equal(20f, matrixCompound.F);
    }
    #endregion

    #region Path Measurement Tests
    [Fact]
    public void TestPathMeasurement()
    {
        var linePath = "M0 0 L100 0";
        var totalLength = SnapPathMeasurement.GetTotalLength(linePath);
        Assert.True(MathF.Abs(totalLength - 100f) < 0.1f);

        var midPoint = SnapPathMeasurement.GetPointAtLength(linePath, 50f);
        Assert.True(MathF.Abs(midPoint.X - 50f) < 0.1f);
        Assert.True(MathF.Abs(midPoint.Y) < 0.1f);
        Assert.True(MathF.Abs(midPoint.Alpha) < 0.1f);

        var boxPath = "M10 20 L110 20 L110 70 L10 70 Z";
        var bbox = SnapPathMeasurement.GetBBox(boxPath);
        Assert.True(MathF.Abs(bbox.X - 10f) < 0.1f);
        Assert.True(MathF.Abs(bbox.Y - 20f) < 0.1f);
        Assert.True(MathF.Abs(bbox.Width - 100f) < 0.1f);
        Assert.True(MathF.Abs(bbox.Height - 50f) < 0.1f);
        Assert.True(MathF.Abs(bbox.Cx - 60f) < 0.1f);
        Assert.True(MathF.Abs(bbox.Cy - 45f) < 0.1f);
    }
    #endregion

    #region Gradients and Definitions Tests
    [Fact]
    public void TestGradientCreationAndStops()
    {
        var paper = Snap.Create(600, 600);
        var grad = paper.Gradient("l(0,0,1,1)#ff0000-#00ff00-#0000ff");
        Assert.NotNull(grad);

        var stops = grad.Stops();
        Assert.Equal(3, stops.Count);

        var rect = paper.Rect(50, 50, 200, 200);
        rect.Attr("fill", grad);

        var xml = paper.ToString();
        Assert.Contains("linearGradient", xml);
        Assert.Contains("stop", xml);
    }
    #endregion

    #region Hierarchy and Manipulation Tests
    [Fact]
    public void TestHierarchyAndQuerySelectors()
    {
        var paper = Snap.Create(600, 600);
        var g1 = paper.G();
        var c1 = paper.Circle(50, 50, 25);
        c1.ID = "mainCircle";
        c1.Attr("class", "clickable highlight");

        var c2 = paper.Circle(150, 150, 25);
        c2.Attr("class", "clickable");

        g1.Add(c1, c2);

        Assert.Equal(2, g1.Children().Count);
        Assert.Equal(g1.Node, c1.Parent()?.Node);

        var foundById = paper.Select("#mainCircle");
        Assert.NotNull(foundById);
        Assert.Equal("mainCircle", foundById.ID);

        var clickables = paper.SelectAll(".clickable");
        Assert.Equal(2, clickables.Count);

        var allCircles = paper.SelectAll("circle");
        Assert.Equal(2, allCircles.Count);
    }

    [Fact]
    public void TestSnapSvgParse()
    {
        var svgStr = "<svg width='300' height='200'><rect id='box' x='10' y='10' width='80' height='80' fill='#ff9900'/></svg>";
        var paper = Snap.Parse(svgStr);
        Assert.NotNull(paper);

        var box = paper.Select("#box");
        Assert.NotNull(box);
        Assert.Equal("rect", box.Type);
    }
    #endregion

    #region Headless Rendering Pipeline Tests
    [Fact]
    public void TestRenderToWebpAndPng()
    {
        var paper = Snap.Create(400, 300);
        paper.Rect(0, 0, 400, 300).Attr("fill", "#222222");
        paper.Circle(200, 150, 80).Attr("fill", "#ff4444");

        // 1. Default: WebP
        var webpBytes = paper.ToImageBytes(400, 300);
        Assert.NotNull(webpBytes);
        Assert.True(webpBytes.Length > 0);
        // Verify WebP magic header: RIFF .... WEBP
        Assert.Equal((byte)'R', webpBytes[0]);
        Assert.Equal((byte)'I', webpBytes[1]);
        Assert.Equal((byte)'F', webpBytes[2]);
        Assert.Equal((byte)'F', webpBytes[3]);

        // 2. Explicit PNG
        var pngBytes = paper.ToImageBytes(400, 300, format: "png");
        Assert.NotNull(pngBytes);
        Assert.True(pngBytes.Length > 0);
        Assert.Equal(0x89, pngBytes[0]);
        Assert.Equal(0x50, pngBytes[1]);
    }
    #endregion

    #region Sandboxed Jint JavaScript Execution Tests
    [Fact]
    public void TestJsDrawingEngineExecution()
    {
        var engine = new JsDrawingEngine();
        var jsCode = @"
            var s = Snap(600, 400);
            var bg = s.rect(0, 0, 600, 400).attr({ fill: '#0f172a' });
            var sun = s.circle(300, 200, 80).attr({ fill: '#f59e0b', stroke: '#fbbf24', strokeWidth: 3 });
            var ray = s.line(100, 100, 500, 300).attr({ stroke: '#ffffff', strokeWidth: 2 });
            console.log('Canvas generated with sun at 300,200');
            s;
        ";

        var result = engine.Execute(jsCode, 600, 400);
        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.ImageBytes);
        Assert.True(result.ImageBytes.Length > 0);
        Assert.Equal("webp", result.ImageFormat);
        Assert.NotEmpty(result.SvgXml);
        Assert.Contains("circle", result.SvgXml);
        Assert.Contains("rect", result.SvgXml);
        Assert.Contains("line", result.SvgXml);
        Assert.Contains("[LOG] Canvas generated with sun at 300,200", result.Logs);
        Assert.NotNull(result.ImageDataUri);
        Assert.StartsWith("data:image/webp;base64,", result.ImageDataUri);
    }

    [Fact]
    public void TestComplexJsSceneWithTransformsAndGradients()
    {
        var engine = new JsDrawingEngine();
        var jsCode = @"
            var s = Snap(800, 600);
            var grad = s.gradient('l(0,0,1,1)#1e1b4b-#4338ca-#06b6d4');
            var bg = s.rect(0, 0, 800, 600).attr({ fill: grad });

            var g = s.g();
            for (var i = 0; i < 5; i++) {
                var c = s.circle(400, 300, 40 + i * 30).attr({
                    fill: 'none',
                    stroke: '#ffffff',
                    strokeWidth: 2,
                    opacity: 0.8 - i * 0.12
                });
                g.add(c);
            }
            g.transform('r45,400,300');

            var txt = s.text(400, 550, 'Enactive Studio').attr({
                fill: '#ffffff',
                fontSize: 32,
                textAnchor: 'middle'
            });

            console.log('Complex generative scene rendered.');
            s;
        ";

        var result = engine.Execute(jsCode, 800, 600);
        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.ImageBytes);
        Assert.True(result.ImageBytes.Length > 0);
        Assert.Contains("linearGradient", result.SvgXml);
        Assert.Contains("Enactive Studio", result.SvgXml);
        Assert.Contains("[LOG] Complex generative scene rendered.", result.Logs);
    }

    [Fact]
    public async Task TestJsDrawingMcpTools()
    {
        var tools = new DrawingMcpTools();
        var script = @"
            var s = Snap(400, 400);
            var p = s.path('M 50 50 L 350 50 L 350 350 Z').attr({ fill: '#6366f1' });
        ";

        var execResult = await tools.ExecuteSvgScript(script, 400, 400);
        Assert.True(execResult.Success, execResult.Error);
        Assert.NotNull(execResult.ImageBytes);

        var measureResult = tools.MeasureSvgPath("M0 0 L200 0 L200 100", 100);
        Assert.NotNull(measureResult);
        Assert.True(measureResult["totalLength"]?.GetValue<float>() > 0);
        Assert.NotNull(measureResult["pointAtLength"]);
    }

    [Fact]
    public void TestSandboxSecurityEvalDisabled()
    {
        var engine = new JsDrawingEngine();
        var script = "eval('1 + 1');";
        var result = engine.Execute(script);
        Assert.False(result.Success);
        Assert.Contains("String compilation has been disabled", result.Error);
    }

    [Fact]
    public void TestSandboxSecurityNoClrTypes()
    {
        var engine = new JsDrawingEngine();
        var script = "typeof System;";
        var result = engine.Execute(script);
        Assert.True(result.Success);
        Assert.Equal("undefined", result.ReturnValue?.ToString());
    }

    [Fact]
    public void TestJSConsoleAndMina()
    {
        var engine = new JsDrawingEngine();
        var script = @"
            console.log('Test log message');
            console.info('Test info message');
            console.warn('Test warn message');
            console.error('Test error message');
            console.debug('Test debug message');

            var easeVal = mina.easeout(0.5);
            var bounceVal = mina.bounce(0.5);
            var linearVal = mina.linear(0.75);

            var s = Snap(200, 200);
            s.circle(100, 100, linearVal * 50);
            s;
        ";

        var result = engine.Execute(script, 200, 200);
        Assert.True(result.Success, result.Error);
        Assert.Contains("[LOG] Test log message", result.Logs);
        Assert.Contains("[INFO] Test info message", result.Logs);
        Assert.Contains("[WARN] Test warn message", result.Logs);
        Assert.Contains("[ERROR] Test error message", result.Logs);
        Assert.Contains("[DEBUG] Test debug message", result.Logs);
    }

    [Fact]
    public void TestSnapFunctionAndStaticHelpers()
    {
        var engine = new JsDrawingEngine();
        var script = @"
            var s = Snap(500, 500);
            var m = Snap.matrix(1, 0, 0, 1, 10, 20);
            var len = Snap.path.getTotalLength('M0 0 L100 0');
            var pt = Snap.path.getPointAtLength('M0 0 L100 0', 50);
            var bbox = Snap.path.getBBox('M10 20 L110 20 L110 70 L10 70 Z');
            var rgb = Snap.rgb(255, 0, 0);
            var hsl = Snap.hsl(120, 100, 50);
            var fmt = Snap.format('{0} x {1}', [100, 200]);
            var parsed = Snap.parse('<circle cx=""50"" cy=""50"" r=""25""/>');
            var rad = Snap.rad(180);
            var deg = Snap.deg(Math.PI);
            var ang = Snap.angle(0, 0, 10, 10);
            var snapped = Snap.snapTo([10, 20, 30], 21, 5);

            s.circle(pt.x, pt.y, 25).attr({ fill: rgb });
            s;
        ";

        var result = engine.Execute(script, 500, 500);
        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.ImageBytes);
        Assert.Contains("circle", result.SvgXml);
    }

    [Fact]
    public void TestGlobalFunctionsAndExit()
    {
        var engine = new JsDrawingEngine();
        var script = @"
            log('Hello from global log');
            error('Global error notice');
            table([{ name: 'circle', count: 5 }, { name: 'rect', count: 12 }]);
            var s = Snap(300, 300);
            s.rect(0, 0, 300, 300).attr({ fill: '#ffcc00' });
            exit('Early exit for testing');
        ";

        var result = engine.Execute(script, 300, 300);
        Assert.True(result.Success, result.Error);
        Assert.Contains("[LOG] Hello from global log", result.Logs);
        Assert.Contains("[ERROR] Global error notice", result.Logs);
        Assert.Contains("[EXIT] Early exit for testing", result.Logs);
        Assert.Contains("Early exit for testing", result.ReturnValue?.ToString());
        Assert.NotNull(result.ImageBytes);
        Assert.Contains("rect", result.SvgXml);
    }

    [Fact]
    public void TestSnapElementGroupChildFactories()
    {
        var engine = new JsDrawingEngine();
        var script = @"
            var s = Snap(600, 600);
            var g = s.g();
            var c = g.circle(100, 100, 50).attr({ fill: '#ff0000' });
            var r = g.rect(200, 100, 80, 80).attr({ fill: '#00ff00' });
            var t = g.text(100, 300, 'Nested in Group').attr({ fill: '#0000ff' });
            var nestedG = g.g();
            var line = nestedG.line(0, 0, 50, 50).attr({ stroke: '#ffffff' });
            s;
        ";

        var result = engine.Execute(script, 600, 600);
        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.SvgXml);
        Assert.Contains("<circle", result.SvgXml);
        Assert.Contains("<rect", result.SvgXml);
        Assert.Contains("<text", result.SvgXml);
        Assert.Contains("<line", result.SvgXml);
        Assert.Contains("Nested in Group", result.SvgXml);
    }

    [Fact]
    public void TestCanvas2DMultiLineAndWrappedText()
    {
        var canvas = new SkiaCanvas(500, 500);
        var ctx = canvas.getContext("2d");

        ctx.font = "16px sans-serif";
        ctx.fillStyle = "#ffffff";

        // Multi-line with \n
        ctx.fillText("Line 1\nLine 2\nLine 3", 50, 50);

        // Word-wrapped paragraph
        var paragraph = "This is a long paragraph that should automatically wrap across multiple lines when rendered onto the canvas surface using fillWrappedText.";
        ctx.fillWrappedText(paragraph, 50, 150, 200);

        var imgBytes = canvas.ToImageBytes();
        Assert.NotNull(imgBytes);
        Assert.True(imgBytes.Length > 0);
    }

    [Fact]
    public void TestMaxStatementsConfigurable()
    {
        var prev = JsDrawingEngine.MaxStatements;
        try
        {
            JsDrawingEngine.MaxStatements = 50_000;
            var engine = new JsDrawingEngine();
            // 60,000 iterations will exceed 50,000 statements limit
            var script = @"
                var sum = 0;
                for (var i = 0; i < 60000; i++) {
                    sum += i;
                }
                sum;
            ";
            var result = engine.Execute(script);
            Assert.False(result.Success);
            Assert.Contains("statements", result.Error?.ToLowerInvariant() ?? "");
        }
        finally
        {
            JsDrawingEngine.MaxStatements = prev;
        }
    }

    [Fact]
    public void TestSkSLRuntimeEffect()
    {
        var sksl = @"
            uniform float2 u_resolution;
            uniform float4 u_color;

            half4 main(float2 coord) {
                float2 uv = coord / u_resolution;
                return half4(uv.x, uv.y, u_color.b, 1.0);
            }
        ";

        using var effect = SKRuntimeEffect.CreateShader(sksl, out var errors);
        Assert.Null(errors);
        Assert.NotNull(effect);

        var uniforms = new SKRuntimeEffectUniforms(effect)
        {
            ["u_resolution"] = new[] { 400f, 400f },
            ["u_color"] = new[] { 1f, 0f, 0.5f, 1f }
        };

        using var shader = effect.ToShader(uniforms);
        Assert.NotNull(shader);

        using var surface = SKSurface.Create(new SKImageInfo(400, 400));
        using var paint = new SKPaint { Shader = shader };
        surface.Canvas.DrawRect(0, 0, 400, 400, paint);

        using var image = surface.Snapshot();
        Assert.NotNull(image);
        Assert.Equal(400, image.Width);
        Assert.Equal(400, image.Height);
    }

    [Fact]
    public void TestSkSLShaderInJavaScriptEngine()
    {
        var engine = new JsDrawingEngine();
        var script = @"
            const canvas = createCanvas(400, 400);
            const ctx = canvas.getContext('2d');

            const sksl = `
                uniform float2 u_resolution;
                uniform float4 u_color1;
                uniform float4 u_color2;

                half4 main(float2 coord) {
                    float2 uv = coord / u_resolution;
                    float d = length(uv - 0.5) * 2.0;
                    float ring = sin(d * 10.0) * 0.5 + 0.5;
                    return mix(u_color1, u_color2, ring);
                }
            `;

            const shader = Skia.Shader.sksl(sksl, {
                u_resolution: [400, 400],
                u_color1: [0.1, 0.2, 0.8, 1.0],
                u_color2: [1.0, 0.6, 0.0, 1.0]
            });

            ctx.fillStyle = shader;
            ctx.fillRect(0, 0, 400, 400);
            canvas;
        ";

        var result = engine.Execute(script);
        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.ImageBytes);
        Assert.True(result.ImageBytes.Length > 0);
    }

    [Fact]
    public void TestSkSLImageFilterInJavaScriptEngine()
    {
        var engine = new JsDrawingEngine();
        var script = @"
            const canvas = createCanvas(300, 300);
            const ctx = canvas.getContext('2d');

            const sksl = `
                uniform shader u_image;
                uniform float u_intensity;

                half4 main(float2 coord) {
                    half4 c = u_image.eval(coord);
                    half gray = dot(c.rgb, half3(0.299, 0.587, 0.114));
                    return mix(c, half4(gray, gray, gray, c.a), u_intensity);
                }
            `;

            ctx.filter = Skia.ImageFilter.runtimeShader(sksl, { u_intensity: 0.8 });
            ctx.fillStyle = '#ff0000';
            ctx.fillRect(50, 50, 200, 200);
            canvas;
        ";

        var result = engine.Execute(script);
        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.ImageBytes);
    }

    [Fact]
    public void TestSkSLColorFilterInJavaScriptEngine()
    {
        var engine = new JsDrawingEngine();
        var script = @"
            const canvas = createCanvas(200, 200);
            const ctx = canvas.getContext('2d');

            const sksl = `
                half4 main(half4 inColor) {
                    return half4(1.0 - inColor.r, 1.0 - inColor.g, 1.0 - inColor.b, inColor.a);
                }
            `;

            ctx.colorFilter = Skia.ColorFilter.runtimeEffect(sksl);
            ctx.fillStyle = '#00ff00';
            ctx.fillRect(0, 0, 200, 200);
            canvas;
        ";

        var result = engine.Execute(script);
        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.ImageBytes);
    }

    [Fact]
    public void TestSkSLCompilationErrorDiagnostics()
    {
        var engine = new JsDrawingEngine();
        var script = @"
            const sksl = `
                half4 main(float2 coord) {
                    this is invalid syntax !!!
                }
            `;
            Skia.Shader.sksl(sksl);
        ";

        var result = engine.Execute(script);
        Assert.False(result.Success);
        Assert.Contains("SkSL compilation error", result.Error);
    }

    [Fact]
    public void TestEncodePerformanceBenchmark()
    {
        var dir = AppContext.BaseDirectory;
        string? scriptPath = null;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "tests", "agent", "mcp_server", "gemini", "artwork.js");
            if (File.Exists(candidate))
            {
                scriptPath = candidate;
                break;
            }
            dir = Directory.GetParent(dir)?.FullName;
        }

        Assert.NotNull(scriptPath);

        var script = File.ReadAllText(scriptPath);
        var engine = new JsDrawingEngine();

        // 1. Warmup run
        engine.Execute(script, 1200, 900, format: "webp", quality: 90);

        var preRender = engine.Execute(script, 1200, 900);
        var canvas = (Polson.Drawing.Skia.SkiaCanvas)preRender.ReturnValue!;
        var bitmap = canvas.Bitmap;

        var formats = new (string format, int quality)[]
        {
            ("png", 100),
            ("webp", 95),
            ("webp", 90),
            ("webp", 85),
            ("webp", 80),
            ("webp", 75),
            ("jpeg", 85)
        };

        const int iterations = 5;
        Console.WriteLine("\n=================== ENCODE & EXECUTION BENCHMARK ===================");
        foreach (var (fmt, q) in formats)
        {
            // Pure encode time
            var swPure = System.Diagnostics.Stopwatch.StartNew();
            long pureBytes = 0;
            for (int i = 0; i < iterations; i++)
            {
                var bytes = SkiaImageEncoder.Encode(bitmap, fmt, q);
                pureBytes = bytes.Length;
            }
            swPure.Stop();
            var avgPureMs = swPure.ElapsedMilliseconds / (double)iterations;

            // Full end-to-end execution time (JS parsing + Canvas 2D render + Image encode)
            var swTotal = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                engine.Execute(script, 1200, 900, format: fmt, quality: q);
            }
            swTotal.Stop();
            var avgTotalMs = swTotal.ElapsedMilliseconds / (double)iterations;

            Console.WriteLine($"[BENCHMARK] Format={fmt,-4} Q={q,-2} | PureEncode={avgPureMs,5:F1}ms | TotalTime={avgTotalMs,5:F1}ms | Size={pureBytes,8:N0} bytes");
        }
        Console.WriteLine("===================================================================\n");
    }
    #endregion
}

