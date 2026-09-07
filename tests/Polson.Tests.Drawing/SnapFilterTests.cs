namespace Polson.Tests.Drawing;

using System;
using System.Linq;
using Polson.Drawing.Svg;
using Polson.MCPServer;
using SkiaSharp;
using Xunit;

/// <summary>
/// The SVG filter chain — <c>paper.filter()</c> and its primitives.
/// </summary>
/// <remarks>
/// <para>
/// The standing conclusion was that grain, soft edges and colour grading were unavailable in a
/// vector deliverable, because <c>Skia.Brush</c>, <c>Skia.PathEffect</c>, <c>Skia.MaskFilter</c> and
/// SkSL all take a canvas context. That was true of our element factory rather than of the renderer:
/// Svg.Skia draws the whole chain and only <c>filter</c> and the <c>fe*</c> entries were missing.
/// </para>
/// <para>
/// So these assert against <b>sampled pixels</b> wherever a primitive claims a visual effect, not
/// against the serialised markup alone. A filter that appears in the saved file and does nothing in
/// the peek is the failure shape this surface is most prone to — the author verifies against the
/// peek and ships something else — and it is exactly what happened to the stylesheet half of the
/// same change (see <see cref="SnapStylesheetTests"/>).
/// </para>
/// </remarks>
public class SnapFilterTests : TestsRuntime
{
    #region Construction Tests
    [Fact]
    public void TestFilterLandsInDefsWithAGeneratedId()
    {
        var svg = Svg("paper.filter().gaussianBlur(4);");

        Assert.Contains("<defs", svg, StringComparison.Ordinal);
        Assert.Contains("<filter", svg, StringComparison.Ordinal);
        Assert.Contains("id=\"filter_", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void TestUrlIsTheReferenceToPutInAFilterAttribute()
    {
        // The id is generated, so a caller that had to assemble url(#…) itself would be assembling it
        // from something it did not choose. `url` hands back the whole reference.
        var result = Execute("const f = paper.filter('grain'); log(f.url);");

        Assert.True(result.Success, result.Error);
        Assert.Contains("url(#grain)", string.Join(" ", result.Logs), StringComparison.Ordinal);
    }

    [Fact]
    public void TestAnExplicitIdIsKept()
    {
        var svg = Svg("paper.filter('paperGrain').turbulence(0.8, 2);");

        Assert.Contains("id=\"paperGrain\"", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void TestEachFilterGetsItsOwnId()
    {
        var result = Execute("log(paper.filter().url + ' ' + paper.filter().url);");

        Assert.True(result.Success, result.Error);
        var ids = Logged(result).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, ids.Length);
        Assert.NotEqual(ids[0], ids[1]);
    }
    #endregion

    #region Primitive Serialisation Tests
    [Theory]
    [InlineData("turbulence(0.8, 3)", "<feTurbulence")]
    [InlineData("gaussianBlur(4)", "<feGaussianBlur")]
    [InlineData("colorMatrix('saturate', 'saturate')", "<feColorMatrix")]
    [InlineData("offset(4, 4)", "<feOffset")]
    [InlineData("flood('#ff0000', 0.5)", "<feFlood")]
    [InlineData("composite('atop')", "<feComposite")]
    [InlineData("blend('multiply')", "<feBlend")]
    [InlineData("dropShadow(3, 3, 2)", "<feDropShadow")]
    [InlineData("morphology(2, 'erode')", "<feMorphology")]
    [InlineData("displacementMap(20)", "<feDisplacementMap")]
    public void TestPrimitiveSerialisesAsItsElement(string call, string expected)
    {
        Assert.Contains(expected, Svg($"paper.filter().{call};"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TestMergeStacksItsNamedInputs()
    {
        var svg = Svg("""
            paper.filter()
                 .flood('#ff0000', 1, 'red')
                 .offset(4, 4, 'SourceGraphic', 'shifted')
                 .merge('red', 'shifted');
            """);

        Assert.Contains("<feMerge", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<feMergeNode", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("in=\"red\"", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("in=\"shifted\"", svg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TestNamedResultsAndInputsAreWiredThrough()
    {
        // The roughened-contour recipe: turbulence names a result, the displacement map takes it as
        // its second input. Without the wiring the chain still renders — as the unfiltered source —
        // so this is asserted on the markup as well as on the pixels below.
        var svg = Svg("""
            paper.filter()
                 .turbulence(0.04, 3, 'fractalNoise', 7, 'noise')
                 .displacementMap(22, 'SourceGraphic', 'noise');
            """);

        Assert.Contains("result=\"noise\"", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("in=\"SourceGraphic\"", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("in2=\"noise\"", svg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TestTurbulenceTypeIsHonoured()
    {
        Assert.Contains("fractalNoise", Svg("paper.filter().turbulence(0.5, 2, 'fractalNoise');"),
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("type=\"turbulence\"", Svg("paper.filter().turbulence(0.5, 2, 'turbulence');"),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TestColorMatrixTakesAnArrayOrAString()
    {
        // Both spellings reach the same twenty numbers: an agent writing JS reaches for the array,
        // an agent copying an SVG snippet reaches for the string.
        const string values = "0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 1 0";
        var fromString = Svg($"paper.filter().colorMatrix('{values}');");
        var fromArray = Svg("paper.filter().colorMatrix([0,0,0,0,0, 0,0,0,0,0, 0,0,0,0,0, 0,0,0,1,0]);");

        Assert.Contains(values, fromString, StringComparison.Ordinal);
        Assert.Contains(values, fromArray, StringComparison.Ordinal);
    }

    [Fact]
    public void TestRegionWidensTheFilterBoxAsPercentages()
    {
        // A blur or a displacement reaches outside the shape and the default region clips it, so this
        // is the difference between a wide effect and one that looks cropped on all four sides.
        var svg = Svg("paper.filter().region(-0.3, -0.3, 1.6, 1.6).gaussianBlur(8);");

        Assert.Contains("x=\"-30%\"", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("width=\"160%\"", svg, StringComparison.OrdinalIgnoreCase);
    }
    #endregion

    #region Chain Wiring Tests
    /// <summary>
    /// <c>flood</c> paints the colour asked for. It painted black whatever you passed.
    /// </summary>
    /// <remarks>
    /// <c>SvgFlood.FloodColor</c> is a typed <c>SvgPaintServer</c>, not a string attribute, so writing
    /// <c>flood-color</c> through the generic attribute path serialised correctly and rendered black —
    /// the same class of defect as the stylesheet that looked right in the file and wrong in the peek.
    /// Found by rendering a chain copied from outside the project, not by reading the code.
    /// </remarks>
    [Fact]
    public void TestFloodPaintsTheColourItWasGiven()
    {
        var png = Render("""
            paper.rect(0, 0, 200, 200).attr({ fill: '#ffffff' });
            const f = paper.filter().flood('#c9553d', 1, 'red').composite('in', 'red', 'SourceGraphic');
            paper.rect(50, 50, 100, 100).attr({ fill: '#000000', filter: f.url });
            """);

        var pixel = PixelAt(png, 100, 100);
        Assert.True(pixel.Red > 150 && pixel.Green < 130 && pixel.Blue < 110,
            $"flood should paint the colour it was given, got {pixel}");
    }

    /// <summary>
    /// An omitted <c>in</c> resolves to <c>SourceGraphic</c>, <b>not</b> to the previous primitive's
    /// result — so every primitive in a chain must name its input.
    /// </summary>
    /// <remarks>
    /// This is the single most expensive thing to not know about the filter surface, because the
    /// symptom is a chain that renders the untouched source with no error: the noise branch is built,
    /// never consumed, and silently discarded. A chain written to the SVG spec's own defaulting rules
    /// therefore does nothing here. Pinned so a renderer upgrade that fixes it is noticed rather than
    /// quietly changing every existing chain's meaning.
    /// </remarks>
    [Fact]
    public void TestAnOmittedInputIsTheSourceRatherThanThePreviousResult()
    {
        // Turbulence replaces the source with noise. If the blur that follows consumed that noise,
        // the frame would be a blurred noise field; if it consumed the source, it is a blurred square.
        var png = Render("""
            paper.rect(0, 0, 200, 200).attr({ fill: '#ffffff' });
            const f = paper.filter().turbulence(0.9, 4, 'fractalNoise', 1, 'noise').gaussianBlur(2);
            paper.rect(50, 50, 100, 100).attr({ fill: '#000000', filter: f.url });
            """);

        // A blurred *square* leaves the corners of the frame clean; a noise field covers them.
        Assert.True(PixelAt(png, 8, 8).Red > 240,
            "the second primitive consumed the turbulence, so an omitted `in` now chains — update the docs");
    }
    #endregion

    #region Rendering Tests
    /// <summary>Noise clipped to a shape and multiplied back over it — grain, on the vector surface.</summary>
    /// <remarks>
    /// The recipe that closes the medium gap Manual 14 §9 describes. Two things have to be right and
    /// both are easy to get wrong: every input is named, and there is <b>no alpha-amplifying
    /// <c>colorMatrix</c></b> — the widely copied <c>0 0 0 19 -9</c> row, which hard-thresholds the
    /// noise alpha, renders as nothing at all here.
    /// </remarks>
    [Fact]
    public void TestNoiseCompositedIntoAShapeGivesItGrain()
    {
        const string stroke = "paper.rect(0, 0, 200, 200).attr({ fill: '#ffffff' });"
            + "paper.rect(40, 80, 120, 40).attr({ fill: '#2b4c7e'{0} });";

        var flat = Render(stroke.Replace("{0}", string.Empty, StringComparison.Ordinal));
        var grainy = Render("""
            paper.rect(0, 0, 200, 200).attr({ fill: '#ffffff' });
            const f = paper.filter()
                 .turbulence(0.85, 4, 'fractalNoise', 0, 'noise')
                 .composite('in', 'noise', 'SourceGraphic', 'grain')
                 .blend('multiply', 'SourceGraphic', 'grain');
            paper.rect(40, 80, 120, 40).attr({ fill: '#2b4c7e', filter: f.url });
            """);

        Assert.True(Spread(flat, 100) < 12, "the control should be flat colour");
        var flatSpread = Spread(flat, 100);
        var grainSpread = Spread(grainy, 100);
        Assert.True(grainSpread > 30 && grainSpread > flatSpread * 3,
            $"expected grain across the shape: flat {flatSpread}, grainy {grainSpread}");
    }

    [Fact]
    public void TestTheAlphaAmplifyingColorMatrixRendersNothing()
    {
        // Pinned because it is the failure an agent will meet: the recipe is everywhere on the web,
        // it is valid SVG, and here it produces an empty frame rather than an error.
        var png = Render("""
            paper.rect(0, 0, 200, 200).attr({ fill: '#ffffff' });
            const f = paper.filter()
                 .turbulence(0.85, 4, 'fractalNoise', 0, 'noise')
                 .colorMatrix('1 0 0 0 0 0 1 0 0 0 0 0 1 0 0 0 0 0 19 -9', 'matrix', 'noise', 'hi')
                 .composite('in', 'hi', 'SourceGraphic');
            paper.rect(40, 80, 120, 40).attr({ fill: '#2b4c7e', filter: f.url });
            """);

        Assert.True(PixelAt(png, 100, 100).Red > 240,
            "the alpha-amplifying matrix now renders — the manual's warning can be withdrawn");
    }

    /// <summary>A filter that only reaches the saved file is the failure this surface invites.</summary>
    [Fact]
    public void TestGaussianBlurSoftensTheEdgeInTheRenderAndNotOnlyInTheFile()
    {
        // Just outside the rect's own edge: hard where unfiltered, part-covered where blurred.
        var sharp = PixelAt(Render(Square(null)), 45, 100);
        var blurred = PixelAt(Render(Square("paper.filter().gaussianBlur(5)")), 45, 100);

        Assert.True(sharp.Red > 240, $"unfiltered ground should still be white outside the rect, got {sharp}");
        Assert.True(blurred.Red < 230, $"blur did not reach the render; outside pixel was {blurred}");
    }

    [Fact]
    public void TestTurbulenceRendersNoiseRatherThanAFlatFill()
    {
        var png = Render("""
            const grain = paper.filter().turbulence(0.35, 4, 'fractalNoise', 3);
            paper.rect(0, 0, 200, 200).attr({ fill: '#808080', filter: grain.url });
            """);

        // feTurbulence replaces the source, so the fill is the noise field itself: neighbouring
        // samples must disagree. A flat result is the signature of the primitive being ignored.
        var samples = Enumerable.Range(0, 24).Select(i => PixelAt(png, 20 + i * 7, 100)).ToArray();
        var spread = samples.Max(c => (int)c.Red) - samples.Min(c => (int)c.Red);
        Assert.True(spread > 24, $"expected a noise field across the row, got a spread of {spread}");
    }

    [Fact]
    public void TestTurbulenceDrivingADisplacementMapMovesTheContour()
    {
        // The nearest vector idiom to a drawn rather than plotted line. The test is that the straight
        // edge stops being straight: sampled down the left edge, coverage must vary row to row.
        var png = Render("""
            const rough = paper.filter().region(-0.3, -0.3, 1.6, 1.6)
                 .turbulence(0.05, 3, 'fractalNoise', 7, 'noise')
                 .displacementMap(18, 'SourceGraphic', 'noise');
            paper.rect(50, 50, 100, 100).attr({ fill: '#000000', filter: rough.url });
            """);

        var edge = Enumerable.Range(0, 20).Select(i => (int)PixelAt(png, 50, 55 + i * 4).Alpha).ToArray();
        Assert.True(edge.Max() - edge.Min() > 64,
            $"the displaced edge is still straight: alphas {string.Join(",", edge)}");
    }

    [Fact]
    public void TestAFilteredShapeStillRendersSomething()
    {
        // The cheap catch for a chain that resolves to nothing: every primitive above draws, and a
        // blank frame with success:true is what an unwired `in` would produce.
        var png = Render(Square("paper.filter().dropShadow(6, 6, 3, '#000000', 0.8)"));
        var centre = PixelAt(png, 100, 100);
        Assert.True(centre.Red < 80, $"the filtered shape itself vanished; centre was {centre}");
    }
    #endregion

    #region Element Factory Tests
    [Theory]
    [InlineData("filter", "<filter")]
    [InlineData("feTurbulence", "<feTurbulence")]
    [InlineData("feGaussianBlur", "<feGaussianBlur")]
    [InlineData("feColorMatrix", "<feColorMatrix")]
    [InlineData("feDisplacementMap", "<feDisplacementMap")]
    [InlineData("feOffset", "<feOffset")]
    [InlineData("feFlood", "<feFlood")]
    [InlineData("feComposite", "<feComposite")]
    [InlineData("feBlend", "<feBlend")]
    [InlineData("feMerge", "<feMerge")]
    [InlineData("feMergeNode", "<feMergeNode")]
    [InlineData("feDropShadow", "<feDropShadow")]
    [InlineData("feMorphology", "<feMorphology")]
    [InlineData("feTile", "<feTile")]
    public void TestElCreatesTheFilterElements(string tag, string expected)
    {
        Assert.Contains(expected, Svg($"paper.el('{tag}');"), StringComparison.OrdinalIgnoreCase);
    }
    #endregion

    #region Methods
    /// <summary>A black square on a white ground, optionally filtered — the edge test's subject.</summary>
    private static string Square(string? filterExpression) => filterExpression is null
        ? "paper.rect(0, 0, 200, 200).attr({ fill: '#ffffff' }); paper.rect(50, 50, 100, 100).attr({ fill: '#000000' });"
        : $$"""
            paper.rect(0, 0, 200, 200).attr({ fill: '#ffffff' });
            const f = {{filterExpression}};
            paper.rect(50, 50, 100, 100).attr({ fill: '#000000', filter: f.url });
            """;

    private static DrawingExecutionResult Execute(string body) =>
        new JsDrawingEngine().Execute($"const paper = Snap(200, 200); {body} paper;", 200, 200, null, "png", 100);

    /// <summary>The logged text without the engine's level prefix.</summary>
    private static string Logged(DrawingExecutionResult result) =>
        string.Join(" ", result.Logs.Select(l => l.Replace("[LOG]", string.Empty, StringComparison.Ordinal).Trim())).Trim();

    private static string Svg(string body)
    {
        var result = Execute(body);
        Assert.True(result.Success, result.Error);
        return result.SvgXml;
    }

    private static byte[] Render(string body)
    {
        var result = Execute(body);
        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.ImageBytes);
        return result.ImageBytes!;
    }

    private static SKColor PixelAt(byte[] png, int x, int y)
    {
        using var bitmap = SKBitmap.Decode(png);
        return bitmap.GetPixel(x, y);
    }

    /// <summary>
    /// How much brightness varies across one row — flat colour against grain.
    /// </summary>
    /// <remarks>
    /// Summed across all three channels rather than read off one. Measured on the red channel of a
    /// dark blue (<c>#2b4c7e</c>, red 43) a real grain reported a spread of 10, because there was
    /// almost no headroom to vary in — the measurement was weak, not the effect.
    /// </remarks>
    private static int Spread(byte[] png, int y)
    {
        using var bitmap = SKBitmap.Decode(png);
        var samples = Enumerable.Range(0, 100)
            .Select(i => bitmap.GetPixel(45 + i, y))
            .Select(c => c.Red + c.Green + c.Blue)
            .ToArray();
        return samples.Max() - samples.Min();
    }
    #endregion
}
