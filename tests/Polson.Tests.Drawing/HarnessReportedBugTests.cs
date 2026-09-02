namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using System.Linq;
using Polson.Drawing.Skia;
using Polson.MCPServer;
using SkiaSharp;
using Xunit;

/// <summary>
/// Two defects a comic-studio agent found in stage 1, reproduced from its own report.
/// </summary>
/// <remarks>
/// Both were silent — no exception, no error, a confident wrong answer — which is why they survived
/// until an agent working the API from outside happened to notice. Kept together because what they
/// have in common matters more than what either does: a call that lies is worse than one that fails.
/// </remarks>
public class HarnessReportedBugTests : TestsRuntime
{
    #region Fill Rule Tests
    /// <summary>
    /// Renders a rect-with-a-hole through a clip and returns whether the centre came out filled.
    /// </summary>
    /// <remarks>
    /// Even-odd knocks the inner rectangle out, so the centre is background; non-zero fills straight
    /// through it. One pixel therefore distinguishes the two rules.
    /// </remarks>
    private static bool[] CentreFilledAfter(params string[] rules)
    {
        var calls = new List<string>();
        foreach (var rule in rules)
        {
            var arg = rule is null ? "" : $", '{rule}'";
            calls.Add($$"""
                x.save();
                x.clip(p{{arg}});
                x.fillStyle = '#000000';
                x.fillRect(0, 0, 300, 200);
                x.restore();
                probes.push(c.bitmap.getPixel(150, 100));
                x.fillStyle = '#ffffff';
                x.fillRect(0, 0, 300, 200);
                """);
        }

        var result = new JsDrawingEngine().Execute($$"""
            const c = createCanvas(300, 200);
            const x = c.getContext('2d');
            x.fillStyle = '#ffffff';
            x.fillRect(0, 0, 300, 200);

            const p = new CanvasPath();
            p.rect(0, 0, 300, 200);
            const q = new CanvasPath();
            q.rect(100, 60, 100, 80);
            p.addPath(q);

            const probes = [];
            {{string.Join('\n', calls)}}
            log(probes.join('|'));
            c;
            """, 300, 200, null, "png", 100);

        Assert.True(result.Success, result.Error);

        var probes = Pixels(result.Logs);
        Assert.Equal(rules.Length, probes.Length);

        // Black centre means the clip let the fill through: non-zero. White means it was knocked out.
        var filled = new bool[probes.Length];
        for (var i = 0; i < probes.Length; i++) filled[i] = probes[i].StartsWith("#00", StringComparison.OrdinalIgnoreCase);
        return filled;
    }

    /// <summary>Pulls the <c>#RRGGBBAA</c> readings out of the log, whatever else a line carries.</summary>
    /// <remarks>
    /// Log entries are prefixed, so splitting the joined text on a separator leaves the prefix glued
    /// to the first reading — which silently corrupted exactly one probe and made two of these tests
    /// fail for a reason that had nothing to do with what they were testing.
    /// </remarks>
    private static string[] Pixels(IEnumerable<string> logs) =>
        System.Text.RegularExpressions.Regex
            .Matches(string.Join('\n', logs), "#[0-9A-Fa-f]{8}")
            .Select(m => m.Value)
            .ToArray();

    /// <summary>
    /// The fill rule is an argument to the call, not a property the path keeps.
    /// </summary>
    /// <remarks>
    /// The agent's reproduction exactly: an even-odd clip followed by a clip with no rule. The second
    /// must be non-zero — the HTML5 default — not "whatever was set last". It used to inherit
    /// even-odd, silently, on any path with a counter.
    /// </remarks>
    [Fact]
    public void TestAnEvenOddClipDoesNotChangeTheNextCall()
    {
        var filled = CentreFilledAfter("evenodd", null!, "nonzero");

        Assert.False(filled[0]);   // evenodd knocks the centre out
        Assert.True(filled[1]);    // no rule means nonzero, regardless of what came before
        Assert.True(filled[2]);    // and explicit nonzero agrees
    }

    /// <summary>The same path, used twice with no rule, behaves the same both times.</summary>
    [Fact]
    public void TestAPathIsReusableAsTheDocsPromise()
    {
        var filled = CentreFilledAfter(null!, "evenodd", null!);

        Assert.True(filled[0]);
        Assert.False(filled[1]);
        Assert.True(filled[2]);
    }

    /// <summary>Filling is subject to the same rule, and equally must not leave a mark on the path.</summary>
    [Fact]
    public void TestAnEvenOddFillDoesNotChangeTheNextFill()
    {
        var result = new JsDrawingEngine().Execute("""
            const c = createCanvas(300, 200);
            const x = c.getContext('2d');
            x.fillStyle = '#ffffff';
            x.fillRect(0, 0, 300, 200);

            const p = new CanvasPath();
            p.rect(0, 0, 300, 200);
            const q = new CanvasPath();
            q.rect(100, 60, 100, 80);
            p.addPath(q);

            x.fillStyle = '#000000';
            x.fill(p, 'evenodd');
            const afterEvenOdd = c.bitmap.getPixel(150, 100);

            x.fillStyle = '#ffffff';
            x.fillRect(0, 0, 300, 200);
            x.fillStyle = '#000000';
            x.fill(p);
            const afterDefault = c.bitmap.getPixel(150, 100);

            log(afterEvenOdd + '|' + afterDefault);
            c;
            """, 300, 200, null, "png", 100);

        Assert.True(result.Success, result.Error);

        var probes = Pixels(result.Logs);
        Assert.Equal(2, probes.Length);
        Assert.StartsWith("#FF", probes[0], StringComparison.OrdinalIgnoreCase);   // knocked out
        Assert.StartsWith("#00", probes[1], StringComparison.OrdinalIgnoreCase);   // filled through
    }
    #endregion

    #region Convergence Tests
    private static readonly ConstructiveDrawingToolkit Toolkit = new();

    private static Dictionary<string, object?> Converge(bool topToBottom)
    {
        object Pt(float x, float y) => new Dictionary<string, object?> { ["x"] = x, ["y"] = y };

        var lines = topToBottom
            ? new List<object> { new List<object> { Pt(174, 0), Pt(168, 340) },
                                 new List<object> { Pt(322, 0), Pt(300, 340) } }
            : new List<object> { new List<object> { Pt(168, 340), Pt(174, 0) },
                                 new List<object> { Pt(300, 340), Pt(322, 0) } };

        return Toolkit.VerifyPerspectiveConvergence(lines, Pt(250, -4000), 6f);
    }

    /// <summary>
    /// The verdict does not depend on which end of each line was listed first.
    /// </summary>
    /// <remarks>
    /// The agent's reproduction. Listed top-to-bottom against a vanishing point above the frame, the
    /// check reported 179.9° of drift on lines that converge — and top-to-bottom is the natural way
    /// to write a vertical. A verifier that returns a confident wrong answer is worse than none,
    /// because the whole point of calling it is to not have to trust your eye.
    /// </remarks>
    [Fact]
    public void TestConvergenceIsIndependentOfPointOrder()
    {
        var down = Converge(topToBottom: true);
        var up = Converge(topToBottom: false);

        Assert.True((bool)down["passed"]!, $"top-to-bottom: {down["message"]}");
        Assert.True((bool)up["passed"]!, $"bottom-to-top: {up["message"]}");

        var downError = Convert.ToSingle(down["maxAngularErrorDeg"]);
        var upError = Convert.ToSingle(up["maxAngularErrorDeg"]);

        Assert.Equal(upError, downError, 3);
    }

    /// <summary>A line that genuinely does not converge is still reported as drifting.</summary>
    /// <remarks>
    /// The fold must not become a way of passing everything: folding at 90° means a perpendicular
    /// line is the worst case, and that is exactly what this asserts.
    /// </remarks>
    [Fact]
    public void TestAGenuinelyDivergentLineStillFails()
    {
        object Pt(float x, float y) => new Dictionary<string, object?> { ["x"] = x, ["y"] = y };

        var lines = new List<object> { new List<object> { Pt(0, 100), Pt(300, 100) } };   // horizontal
        var check = Toolkit.VerifyPerspectiveConvergence(lines, Pt(150, -4000), 5f);      // VP straight up

        Assert.False((bool)check["passed"]!);
        Assert.True(Convert.ToSingle(check["maxAngularErrorDeg"]) > 80f,
            $"a perpendicular line should be near the 90° worst case, got {check["maxAngularErrorDeg"]}");
    }
    #endregion

    #region Perlin Noise Channel Tests
    /// <summary>Fills a canvas with <paramref name="fillStyleExpr"/> and reports what the channels did.</summary>
    /// <remarks>
    /// Sampled on a coprime stride so the readings are scattered across the field rather than walking
    /// one row of it, which a low-frequency noise would make nearly constant.
    /// </remarks>
    private static (int Grey, int Samples, int ChannelSpread, int AlphaMin, int AlphaMax) NoiseStats(string fillStyleExpr)
    {
        var result = new JsDrawingEngine().Execute($$"""
            const c = createCanvas(64, 64);
            const x = c.getContext('2d');
            x.fillStyle = {{fillStyleExpr}};
            x.fillRect(0, 0, 64, 64);
            for (let i = 0; i < 24; i++) log(c.bitmap.getPixel((i * 7) % 64, (i * 11) % 64));
            c;
            """, 64, 64, null, "png", 100);

        Assert.True(result.Success, result.Error);
        var px = Pixels(result.Logs);
        Assert.Equal(24, px.Length);

        int grey = 0, spread = 0, aMin = 255, aMax = 0;
        foreach (var p in px)
        {
            int Channel(int at) => Convert.ToInt32(p.Substring(at, 2), 16);
            int r = Channel(1), g = Channel(3), b = Channel(5), a = Channel(7);

            if (Math.Abs(r - g) <= 1 && Math.Abs(g - b) <= 1) grey++;
            spread = Math.Max(spread, Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b)));
            aMin = Math.Min(aMin, a);
            aMax = Math.Max(aMax, a);
        }
        return (grey, px.Length, spread, aMin, aMax);
    }

    /// <summary>
    /// The raw Perlin shaders emit an independent noise field per channel. Pinned, not deplored.
    /// </summary>
    /// <remarks>
    /// This is faithful to SVG <c>feTurbulence</c> and is the right behaviour for a primitive that
    /// names itself after it. The test exists because the manuals now carry a warning built on it:
    /// if Skia ever collapses these to a single field the warning becomes false, and a doc that
    /// warns about something that no longer happens is its own kind of defect.
    /// </remarks>
    [Theory]
    [InlineData("perlinNoiseTurbulence")]
    [InlineData("perlinNoiseFractal")]
    public void TestRawPerlinNoiseIsPerChannel(string factory)
    {
        var stats = NoiseStats($"Skia.Shader.{factory}(0.05, 0.05, 4, 7)");

        Assert.True(stats.Grey < stats.Samples / 3,
            $"expected coloured noise, got {stats.Grey}/{stats.Samples} grey samples");
        Assert.True(stats.ChannelSpread > 40,
            $"expected a wide per-channel spread, got {stats.ChannelSpread}");
    }

    /// <summary>
    /// <c>Skia.Shader.luminance</c> greys the field and leaves alpha alone — both halves asserted.
    /// </summary>
    /// <remarks>
    /// The agent that found this also found the trap in the obvious correction: clamping alpha to 1
    /// in the same colour matrix greys the noise perfectly and turns vapour into an opaque sheet.
    /// So the alpha assertion is not incidental — it is the half that distinguishes the fix from the
    /// mistake, and neither half is visible in a channel-spread number alone.
    /// </remarks>
    [Theory]
    [InlineData("perlinNoiseTurbulence")]
    [InlineData("perlinNoiseFractal")]
    public void TestLuminanceGreysTheFieldAndPreservesAlpha(string factory)
    {
        var raw = NoiseStats($"Skia.Shader.{factory}(0.05, 0.05, 4, 7)");
        var lit = NoiseStats($"Skia.Shader.luminance(Skia.Shader.{factory}(0.05, 0.05, 4, 7))");

        Assert.Equal(lit.Samples, lit.Grey);
        Assert.True(lit.ChannelSpread <= 1, $"expected a value field, got spread {lit.ChannelSpread}");

        // Still wispy: the alpha field survives, and survives unchanged.
        Assert.True(lit.AlphaMax - lit.AlphaMin > 40,
            $"alpha was flattened to {lit.AlphaMin}..{lit.AlphaMax}; the vapour would render as a sheet");
        Assert.Equal(raw.AlphaMin, lit.AlphaMin);
        Assert.Equal(raw.AlphaMax, lit.AlphaMax);
    }

    /// <summary>The two material presets are value fields by default, and can opt back out.</summary>
    [Theory]
    [InlineData("createRopeFiberShader(0.08, 0.40, 3, 42")]
    [InlineData("createAtmosphericCloudShader(0.015, 0.015, 4, 101")]
    public void TestMaterialPresetsAreValueFieldsByDefault(string call)
    {
        var byDefault = NoiseStats($"Drawing.{call})");
        Assert.Equal(byDefault.Samples, byDefault.Grey);

        var optedOut = NoiseStats($"Drawing.{call}, false)");
        Assert.True(optedOut.Grey < optedOut.Samples / 3,
            $"luminanceOnly:false should give the raw field, got {optedOut.Grey}/{optedOut.Samples} grey");
    }
    #endregion

    #region Perspective Cylinder Tests
    /// <summary>Renders one cylinder in its own frame and returns the bounding box of its ink.</summary>
    /// <remarks>
    /// Every fill and the stroke share one colour, so the box is the whole silhouette rather than one
    /// of its parts. <c>rowProfile</c> keeps this to a single native pass — a per-pixel scan of an
    /// 800x600 frame is most of the statement budget on its own.
    /// </remarks>
    private static (float MinX, float MaxX, float MinY, float MaxY) CylinderInk(
        string gridOptions, float anchorX, float anchorY, float radius, float height)
    {
        var result = new JsDrawingEngine().Execute($$"""
            const c = createCanvas(800, 600);
            const x = c.getContext('2d');
            x.fillStyle = '#ffffff';
            x.fillRect(0, 0, 800, 600);

            const grid = Drawing.createPerspectiveGrid({{gridOptions}});
            x.drawPerspectiveCylinder(grid, {{anchorX}}, {{anchorY}}, {{radius}}, {{height}},
                { sideFill: '#ff0000', topFill: '#ff0000', strokeColor: '#ff0000' });

            let minX = 9999, maxX = -1, minY = 9999, maxY = -1;
            for (const r of c.bitmap.rowProfile('#ff0000', { tolerance: 60 })) {
                if (r.start < minX) minX = r.start;
                if (r.end > maxX) maxX = r.end;
                if (r.index < minY) minY = r.index;
                if (r.index > maxY) maxY = r.index;
            }
            log('INK ' + minX + ' ' + maxX + ' ' + minY + ' ' + maxY);
            c;
            """, 800, 600, null, "png", 100);

        Assert.True(result.Success, result.Error);
        var line = result.Logs.First(l => l.Contains("INK", StringComparison.Ordinal));
        var n = line[(line.IndexOf("INK", StringComparison.Ordinal) + 4)..].Trim()
            .Split(' ').Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();

        Assert.True(n[1] > n[0], "the cylinder drew no ink at all");
        return (n[0], n[1], n[2], n[3]);
    }

    private const string TwoPoint = "{ type: '2point', horizonY: 200, centerOfVisionX: 400, cameraAngleDeg: 45 }";

    /// <summary>
    /// The anchor is the base circle's centre and <c>radius</c> is half the drawn width.
    /// </summary>
    /// <remarks>
    /// The agent's reproduction: a pot drawn "roughly 2x the requested width". Measured before the
    /// fix at 1.88x-1.92x across camera angles, because the call built a 2r x 2r perspective box,
    /// anchored it at that box's near corner, and took the width from the footprint's diagonal.
    /// Nothing errored — it returned a plausible cylinder of the wrong size, which is why it survived
    /// four iterations of a 50 KB script.
    /// </remarks>
    [Theory]
    [InlineData(30)]
    [InlineData(45)]
    [InlineData(60)]
    public void TestCylinderHonoursItsRadiusAndAnchor(int cameraAngleDeg)
    {
        var grid = $"{{ type: '2point', horizonY: 200, centerOfVisionX: 400, cameraAngleDeg: {cameraAngleDeg} }}";
        var ink = CylinderInk(grid, 400f, 450f, 60f, 150f);

        // 120px of cylinder plus a 1.8px stroke straddling each edge.
        var width = ink.MaxX - ink.MinX;
        Assert.InRange(width, 118f, 126f);

        var centre = (ink.MinX + ink.MaxX) / 2f;
        Assert.InRange(centre, 396f, 404f);
    }

    /// <summary>Each cap is foreshortened at its own height, not both at the base's.</summary>
    /// <remarks>
    /// The second half of the agent's finding — "rim ellipses far rounder than the scene's geometry
    /// allows". One <c>ry</c> was computed from the footprint and reused for the top, so a cylinder's
    /// two caps came out identically squashed however tall it was. The top sits nearer the horizon,
    /// so it must be the flatter of the two.
    /// </remarks>
    [Fact]
    public void TestCylinderCapsAreForeshortenedIndependently()
    {
        const float baseY = 450f, height = 150f;
        var ink = CylinderInk(TwoPoint, 400f, baseY, 60f, height);

        var baseHalfHeight = ink.MaxY - baseY;              // below the base centre
        var topHalfHeight = (baseY - height) - ink.MinY;    // above the top centre

        Assert.True(baseHalfHeight > 4f, $"the base cap collapsed: {baseHalfHeight}");
        Assert.True(topHalfHeight > 2f, $"the top cap collapsed: {topHalfHeight}");
        Assert.True(topHalfHeight < baseHalfHeight - 3f,
            $"the top cap should be the flatter one, got top {topHalfHeight} vs base {baseHalfHeight}");
    }

    /// <summary>
    /// A cylinder standing on a raised surface is flatter than the same one on the floor.
    /// </summary>
    /// <remarks>
    /// The agent asked for an elevation parameter, having found that "anything not standing on the
    /// ground plane comes out wrong". There is none, and there should not be: a cap's flatness comes
    /// from the directions to the vanishing points at its own centre, and a base higher up the screen
    /// has shallower rays. The pot on the counter is handled by putting the anchor on the counter.
    /// This is the assertion that the elevation case is actually covered.
    /// </remarks>
    [Fact]
    public void TestCylinderOnARaisedSurfaceIsFlatterThanOnTheGround()
    {
        var onFloor = CylinderInk(TwoPoint, 400f, 500f, 60f, 90f);
        var onCounter = CylinderInk(TwoPoint, 400f, 380f, 60f, 90f);

        var floorCap = onFloor.MaxY - 500f;
        var counterCap = onCounter.MaxY - 380f;

        Assert.True(counterCap < floorCap - 3f,
            $"a base 120px nearer the horizon should be flatter, got {counterCap} vs {floorCap}");

        // Height above the ground changes the foreshortening, never the drawn width.
        Assert.InRange((onCounter.MaxX - onCounter.MinX) - (onFloor.MaxX - onFloor.MinX), -2f, 2f);
    }

    /// <summary>A one-point grid draws a cylinder rather than a flat line.</summary>
    /// <remarks>
    /// One-point puts both vanishing points on the centre of vision, so the two ground directions
    /// coincide and a circle taken from them degenerates. The 45-degree distance points are used
    /// instead — a perpendicular pair in the same plane, and a circle has equal radii along any such
    /// pair, so either is a valid source of conjugate diameters.
    /// </remarks>
    [Fact]
    public void TestCylinderInAOnePointGridDoesNotCollapse()
    {
        var ink = CylinderInk("{ type: '1point', horizonY: 200, centerOfVisionX: 400, focalLength: 800 }",
            400f, 450f, 60f, 150f);

        Assert.InRange(ink.MaxX - ink.MinX, 118f, 126f);
        Assert.True(ink.MaxY - 450f > 4f, $"the base cap collapsed to a line: {ink.MaxY - 450f}");
    }

    /// <summary>
    /// A cap's long axis is horizontal, so its topmost point sits over the cylinder's own centre.
    /// </summary>
    /// <remarks>
    /// Norling, <i>Perspective Made Easy</i>, p. 137: the long axis always forms a T with the upright
    /// line of the cylinder. An earlier fix built each cap from conjugate semi-diameters toward the
    /// two vanishing points, which is projectively defensible and tilted the cap away from the centre
    /// of vision — 13.5 px left and 15.5 px right on a 140 px cap, against 0.5 px on axis. At that
    /// size it reads as a leaning bottle. A real wide-angle projection does tilt a circle off axis,
    /// but this grid is a screen-space construction rather than a metric camera, so the drawing
    /// convention wins. **The off-axis cases are the test** — on-axis passed throughout.
    /// </remarks>
    [Theory]
    [InlineData(400)]   // on the centre of vision
    [InlineData(150)]   // 250px left of it
    [InlineData(700)]   // 300px right of it
    public void TestCylinderCapsKeepAHorizontalMajorAxis(float anchorX)
    {
        var result = new JsDrawingEngine().Execute($$"""
            const c = createCanvas(900, 700);
            const x = c.getContext('2d');
            x.fillStyle = '#ffffff';
            x.fillRect(0, 0, 900, 700);

            const grid = Drawing.createPerspectiveGrid(
                { type: '2point', horizonY: 180, centerOfVisionX: 400, cameraAngleDeg: 45 });
            // A very short cylinder, so the topmost ink is the top cap's apex and nothing else.
            x.drawPerspectiveCylinder(grid, {{anchorX.ToString(System.Globalization.CultureInfo.InvariantCulture)}}, 520, 70, 6,
                { sideFill: '#ff0000', topFill: '#ff0000', strokeColor: '#ff0000' });

            const rows = c.bitmap.rowProfile('#ff0000', { tolerance: 60 });
            log('APEX ' + rows[0].start + ' ' + rows[0].end);
            c;
            """, 900, 700, null, "png", 100);

        Assert.True(result.Success, result.Error);
        var line = result.Logs.First(l => l.Contains("APEX ", StringComparison.Ordinal));
        var n = line[(line.IndexOf("APEX ", StringComparison.Ordinal) + 5)..].Trim()
            .Split(' ').Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();

        var apex = (n[0] + n[1]) / 2f;
        Assert.InRange(apex - anchorX, -3f, 3f);
    }
    #endregion

    #region Figure Canon Tests
    /// <summary>
    /// Shoulder width is selectable, so a figure can follow whichever canon the work calls for.
    /// </summary>
    /// <remarks>
    /// The published canons disagree and none is wrong — Loomis gives 2.33 head units for the figure
    /// at its widest and about 2.0 for the shoulder "cape", while Faragasso after Reilly gives 2.67
    /// across. The toolkit's own 1.8 is a shoulder-<i>joint</i> span, narrower than all three. Until
    /// this option existed an agent reading either book could not act on it, which made the manual
    /// describe a choice the SDK did not offer.
    /// </remarks>
    [Fact]
    public void TestShoulderSpanFollowsTheChosenCanon()
    {
        static float SpanOf(string options)
        {
            var result = new JsDrawingEngine().Execute($$"""
                const f = Drawing.createMannequinFigure(400, 60, 640{{options}});
                log('SPAN ' + (f.rightArm.shoulder.x - f.leftArm.shoulder.x) + ' UNIT ' + f.headUnit);
                'measured';
                """, 800, 800, null, "png", 100);

            Assert.True(result.Success, result.Error);
            var line = result.Logs.First(l => l.Contains("SPAN ", StringComparison.Ordinal));
            var parts = line[(line.IndexOf("SPAN ", StringComparison.Ordinal) + 5)..].Split(" UNIT ");
            var span = float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
            var unit = float.Parse(parts[1].Trim(), System.Globalization.CultureInfo.InvariantCulture);
            return span / unit;                       // back into head units
        }

        Assert.Equal(1.80f, SpanOf(""), 1);                                       // the default
        Assert.Equal(2.33f, SpanOf(", { shoulderSpanHeads: 2.33 }"), 1);          // Loomis, widest
        Assert.Equal(2.67f, SpanOf(", { shoulderSpanHeads: 2.67 }"), 1);          // Faragasso / Reilly
    }
    #endregion

    #region Rim Light Tests
    /// <summary>
    /// Rims a circle of radius 120 at (300,300) and reads the brightness back around it.
    /// </summary>
    /// <remarks>
    /// Sampled at <paramref name="sampleRadius"/> rather than on the contour itself: the band sits
    /// inside the contour, so sampling the nominal radius reads its antialiased outer edge and
    /// produces quadrant totals that differ by 5x for no reason but the rounding. That artefact cost
    /// a false alarm here before the radius was moved to the middle of the band.
    /// </remarks>
    private static int[] RimBrightnessByDegree(float lightAngleDeg, float sampleRadius, float thickness = 6f)
    {
        var result = new JsDrawingEngine().Execute($$"""
            const c = createCanvas(600, 600);
            const x = c.getContext('2d');
            x.fillStyle = '#000000';
            x.fillRect(0, 0, 600, 600);

            const pts = [];
            for (let a = 0; a < 360; a += 2) {
                const r = a * Math.PI / 180;
                pts.push({ x: 300 + Math.cos(r) * 120, y: 300 + Math.sin(r) * 120 });
            }
            pts.push(pts[0]);
            x.drawRimLight(pts, {{lightAngleDeg.ToString(System.Globalization.CultureInfo.InvariantCulture)}}, '#ffffff', {{thickness.ToString(System.Globalization.CultureInfo.InvariantCulture)}});

            const b = c.bitmap;
            const out = [];
            for (let a = 0; a < 360; a++) {
                const r = a * Math.PI / 180;
                const px = b.getPixel(Math.round(300 + Math.cos(r) * {{sampleRadius.ToString(System.Globalization.CultureInfo.InvariantCulture)}}),
                                      Math.round(300 + Math.sin(r) * {{sampleRadius.ToString(System.Globalization.CultureInfo.InvariantCulture)}}));
                out.push(parseInt(px.substring(1, 3), 16));
            }
            log('RIM ' + out.join(','));
            c;
            """, 600, 600, null, "png", 100);

        Assert.True(result.Success, result.Error);
        var line = result.Logs.First(l => l.Contains("RIM ", StringComparison.Ordinal));
        return line[(line.IndexOf("RIM ", StringComparison.Ordinal) + 4)..].Trim().Split(',').Select(int.Parse).ToArray();
    }

    /// <summary>The half of the contour facing away from the light is not drawn at all.</summary>
    /// <remarks>
    /// The agent's finding: "it strokes essentially the whole list, so at any opacity high enough to
    /// see it, it reads as a continuous outline rather than a rim", and the point lists had to be
    /// hand-trimmed to the arc that should catch light. A rim light is partial by definition, so the
    /// angle now does the selecting.
    /// </remarks>
    [Theory]
    [InlineData(0)]      // from the right
    [InlineData(180)]    // from the left
    [InlineData(-90)]    // from above; canvas y runs down
    public void TestRimLightCullsTheUnlitSide(float lightAngleDeg)
    {
        var ring = RimBrightnessByDegree(lightAngleDeg, 117f);

        // Degrees within 60 of the light are lit; those more than 120 away must be untouched.
        var lightDeg = (lightAngleDeg + 360f) % 360f;
        var lit = 0;
        for (var a = 0; a < 360; a++)
        {
            var delta = MathF.Abs(((a - lightDeg + 540f) % 360f) - 180f);   // 0 = facing the light
            if (delta > 120f) Assert.Equal(0, ring[a]);
            else if (delta < 60f && ring[a] > 40) lit++;
        }

        Assert.True(lit > 90, $"the lit arc is barely drawn: {lit} of ~120 degrees");
    }

    /// <summary>The rim fades toward the terminator instead of stopping at a hard edge.</summary>
    /// <remarks>
    /// Culling alone would leave a bright band ending in a sharp corner, which reads as ink just as
    /// the old full-contour stroke did. The cosine falloff is what makes it light.
    /// </remarks>
    [Fact]
    public void TestRimLightFadesTowardTheTerminator()
    {
        var ring = RimBrightnessByDegree(0f, 117f);

        static int Near(int[] r, int deg) => Enumerable.Range(deg - 2, 5).Max(a => r[(a + 360) % 360]);

        var facing = Near(ring, 0);
        var oblique = Near(ring, 50);
        var grazing = Near(ring, 80);

        Assert.True(facing > oblique + 20, $"no falloff by 50°: {facing} then {oblique}");
        Assert.True(oblique > grazing + 20, $"no falloff by 80°: {oblique} then {grazing}");
    }

    /// <summary>The band sits inside the contour, not outboard of it.</summary>
    /// <remarks>
    /// The second half of the finding — a rim "about 10 px outboard" of the figure, reading as a pale
    /// wire floating clear of it. Two of those pixels were the call's own: it offset every point by
    /// two along the light vector, deliberately pushing the stroke off the form. A list that only
    /// approximates the silhouette will still float, and that is the caller's to fix.
    /// </remarks>
    [Fact]
    public void TestRimLightSitsInsideTheContour()
    {
        // 6px band inside a radius-120 contour occupies 114..120. 123 is outside it either way.
        Assert.All(RimBrightnessByDegree(0f, 123f), v => Assert.Equal(0, v));
        Assert.Contains(RimBrightnessByDegree(0f, 117f), v => v > 40);
    }

    /// <summary>A rect can be rimmed along its top, which it previously could not.</summary>
    /// <remarks>
    /// The rect branch drew one straight line down either the left or the right edge, chosen by
    /// <c>cos(angle) &gt; 0</c>. A light from directly overhead has cosine zero, so it fell to the
    /// else branch and lit the left edge — the one place a light from above puts no rim at all.
    /// </remarks>
    [Fact]
    public void TestRimLightOnARectCanLightTheTopEdge()
    {
        var result = new JsDrawingEngine().Execute("""
            const c = createCanvas(600, 600);
            const x = c.getContext('2d');
            x.fillStyle = '#000000';
            x.fillRect(0, 0, 600, 600);

            x.drawRimLight({ x: 200, y: 200, width: 200, height: 200 }, -90, '#ffffff', 6);

            const b = c.bitmap;
            log('TOP ' + b.getPixel(300, 203));
            log('BOTTOM ' + b.getPixel(300, 397));
            log('LEFT ' + b.getPixel(203, 300));
            c;
            """, 600, 600, null, "png", 100);

        Assert.True(result.Success, result.Error);
        var px = Pixels(result.Logs);
        Assert.Equal(3, px.Length);

        static int Luminance(string hex) => Convert.ToInt32(hex.Substring(1, 2), 16);

        Assert.True(Luminance(px[0]) > 200, $"the top edge should be lit, got {px[0]}");
        Assert.Equal(0, Luminance(px[1]));    // bottom faces away
        Assert.Equal(0, Luminance(px[2]));    // and so does the left, at exactly 90° off
    }
    #endregion

    #region Clip Binding Tests
    /// <summary>
    /// Draws a mannequin spanning y=300, optionally clipped to the top half, and counts ink below it.
    /// </summary>
    private static int InkBelowTheClip(bool clipped)
    {
        var clip = clipped
            ? "const region = new CanvasPath(); region.rect(0, 0, 600, 300); x.clip(region);"
            : "";

        var result = new JsDrawingEngine().Execute($$"""
            const c = createCanvas(600, 600);
            const x = c.getContext('2d');
            x.fillStyle = '#ffffff';
            x.fillRect(0, 0, 600, 600);

            x.save();
            {{clip}}
            const figure = Drawing.createMannequinFigure(300, 90, 420);
            Drawing.drawMannequinSolid(x, figure, { fillColor: '#000000', strokeColor: '#000000' });
            x.restore();

            // Rows strictly below the clip boundary that carry any non-white ink.
            let rows = 0;
            for (const r of c.bitmap.rowProfile('#000000', { tolerance: 90 })) {
                if (r.index > 302) rows++;
            }
            log('ROWS ' + rows);
            c;
            """, 600, 600, null, "png", 100);

        Assert.True(result.Success, result.Error);
        var line = result.Logs.First(l => l.Contains("ROWS ", StringComparison.Ordinal));
        return int.Parse(line[(line.IndexOf("ROWS ", StringComparison.Ordinal) + 5)..].Trim());
    }

    /// <summary>
    /// <c>ctx.clip</c> constrains the <c>Drawing.*</c> toolkit, not only the primitive canvas calls.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Undocumented, and the agent that found it verified it by pixel readback rather than by eye,
    /// which is the right instinct: it is the difference between being able to stage occlusion and
    /// not. The intuitive alternative — draw the occluding form <em>after</em> the form it hides —
    /// does nothing on a construction sheet, because an outline does not hide what is behind it.
    /// Without clip binding toolkit draws there is no way to make a counter cut off a figure's legs.
    /// </para>
    /// <para>
    /// The unclipped control is the half that makes this a test. A mannequin that happened not to
    /// reach past the boundary would satisfy the clipped assertion on its own, and the test would
    /// pass while proving nothing — which is the failure mode this whole file is about.
    /// </para>
    /// </remarks>
    [Fact]
    public void TestClipConstrainsToolkitDrawsAndNotOnlyPrimitives()
    {
        var unclipped = InkBelowTheClip(clipped: false);
        Assert.True(unclipped > 50, $"the figure must cross the boundary for this to mean anything: {unclipped} rows");

        Assert.Equal(0, InkBelowTheClip(clipped: true));
    }
    #endregion
}
