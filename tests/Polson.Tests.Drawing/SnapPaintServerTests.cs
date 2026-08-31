namespace Polson.Tests.Drawing;

using System;
using Polson.Drawing.Svg;
using Polson.MCPServer;
using SkiaSharp;
using Xunit;

/// <summary>
/// The vector paint-server surface, as exercised by an agent harness run.
/// <para>
/// A harness agent lost roughly a third of its session believing the vector engine had no gradients:
/// <c>paper.gradient(...)</c> existed but was undocumented, and the documented route —
/// <c>attr({ fill: 'url(#id)' })</c> — silently degraded the reference to a colour literal and rendered
/// black with <c>success: true</c>. These tests pin the behaviour by sampled pixel, not by call success.
/// </para>
/// </summary>
public class SnapPaintServerTests : TestsRuntime
{
    #region Gradient Reference Tests
    [Fact]
    public void TestFillByUrlReferenceRendersTheGradient()
    {
        // The whole shape is filled via attr({ fill: 'url(#id)' }); a mid-left sample must land on the
        // gradient's start colour rather than the black a failed paint-server reference falls back to.
        var png = Render("""
            const g = paper.gradient('l(0,0,1,0)#ff0000-#0000ff');
            paper.rect(0, 0, 200, 200).attr({ fill: 'url(#' + g.attr('id') + ')' });
            """);

        var left = PixelAt(png, 4, 100);
        Assert.True(left.Red > 200 && left.Blue < 60, $"expected the gradient's red start, got {left}");
        Assert.NotEqual(new SKColor(0, 0, 0), new SKColor(left.Red, left.Green, left.Blue));
    }

    [Fact]
    public void TestUrlReferenceSurvivesSerialisation()
    {
        var svg = Svg("""
            const g = paper.gradient('l(0,0,1,0)#ff0000-#0000ff');
            paper.rect(0, 0, 200, 200).attr({ fill: 'url(#' + g.attr('id') + ')' });
            """);

        Assert.Contains("url(#grad_", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("fill:#grad_", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void TestGradientLandsInDefsWithItsStops()
    {
        var svg = Svg("paper.gradient('l(0,0,1,0)#ff0000-#00ff00-#0000ff');");

        Assert.Contains("<defs>", svg, StringComparison.Ordinal);
        Assert.Contains("<linearGradient", svg, StringComparison.Ordinal);
        Assert.Contains("stop-color=\"#FF0000\"", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("stop-color=\"#0000FF\"", svg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TestRadialGradientDescriptorIsHonoured()
    {
        var svg = Svg("paper.gradient('r(0.5,0.5,0.5)#ffffff-#000000');");

        Assert.Contains("<radialGradient", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void TestUserSpaceGradientJoinsAdjacentShapesWithoutASeam()
    {
        // Anchored in the paper's space, one gradient spans both rects continuously — this is what
        // lets a limb built from several overlapping shapes read as a single form.
        var png = Render("""
            const g = paper.gradient('l(0,0,1,0)#ff0000-#0000ff');
            g.attr({ gradientUnits: 'userSpaceOnUse', x1: 0, y1: 0, x2: 200, y2: 0 });
            const fill = { fill: 'url(#' + g.attr('id') + ')' };
            paper.rect(0, 0, 100, 200).attr(fill);
            paper.rect(100, 0, 100, 200).attr(fill);
            """);

        var before = PixelAt(png, 98, 100);
        var after = PixelAt(png, 102, 100);
        Assert.True(Math.Abs(before.Red - after.Red) < 24 && Math.Abs(before.Blue - after.Blue) < 24,
            $"seam across the shared user-space gradient: {before} vs {after}");
    }

    [Fact]
    public void TestGradientIsPerShapeByDefault()
    {
        // The default is objectBoundingBox: each shape gets the whole ramp, so the join IS a seam.
        // Pinned because the opposite was documented at one point and is the more intuitive guess.
        var png = Render("""
            const g = paper.gradient('l(0,0,1,0)#ff0000-#0000ff');
            const fill = { fill: 'url(#' + g.attr('id') + ')' };
            paper.rect(0, 0, 100, 200).attr(fill);
            paper.rect(100, 0, 100, 200).attr(fill);
            """);

        var before = PixelAt(png, 98, 100);
        var after = PixelAt(png, 102, 100);
        Assert.True(before.Blue > 200 && after.Red > 200,
            $"expected each shape to carry its own ramp, got {before} then {after}");
    }
    #endregion

    #region Element Factory Tests
    [Theory]
    [InlineData("defs", "<defs")]
    [InlineData("linearGradient", "<linearGradient")]
    [InlineData("radialGradient", "<radialGradient")]
    [InlineData("stop", "<stop")]
    [InlineData("symbol", "<symbol")]
    [InlineData("marker", "<marker")]
    public void TestElCreatesTheNamedElement(string tag, string expected)
    {
        Assert.Contains(expected, Svg($"paper.el('{tag}');"), StringComparison.Ordinal);
    }

    [Fact]
    public void TestElRejectsAnUnknownTagInsteadOfSilentlyMakingAGroup()
    {
        var result = Execute("paper.el('notAnSvgElement');");

        Assert.False(result.Success);
        Assert.Contains("not an SVG element", result.Error, StringComparison.OrdinalIgnoreCase);
    }
    #endregion

    #region Restored Documented Members
    [Fact]
    public void TestPreviouslyMissingDocumentedMembersExist()
    {
        var result = Execute("""
            log([
              typeof Snap.path.ogeeCurve,
              typeof mina.backin, typeof mina.backout, typeof mina.time,
              typeof paper.svg,
              typeof Snap.matrix().mult,
              typeof paper.toImageBytes
            ].join(','));
            """);

        Assert.True(result.Success, result.Error);
        Assert.Contains("function,function,function,function,function,function,function", string.Join(" ", result.Logs));
    }
    #endregion

    #region Methods
    private static DrawingExecutionResult Execute(string body) =>
        new JsDrawingEngine().Execute($"const paper = Snap(200, 200); {body} paper;", 200, 200, null, "png", 100);

    private static string Svg(string body)
    {
        var result = Execute(body);
        Assert.True(result.Success, result.Error);
        return result.SvgXml;
    }

    #region Clearing Tests
    /// <summary>
    /// Clearing a paper keeps its paint servers, and does so through a base reference too.
    /// </summary>
    /// <remarks>
    /// <c>SnapPaper.Clear</c> keeps <c>&lt;defs&gt;</c> where <c>SnapElement.Clear</c> empties every
    /// child. While it merely *hid* the base method rather than overriding it, which of the two ran
    /// depended on the static type of the reference — so the same paper either kept its gradients or
    /// silently destroyed them, leaving every later <c>url(#…)</c> dangling. The compiler said so as
    /// CS0114 and the warning sat among eleven others that were only duplicates.
    /// <para>
    /// Asserted through <see cref="SnapElement"/> deliberately: calling it on the derived type would
    /// pass either way and prove nothing.
    /// </para>
    /// </remarks>
    [Fact]
    public void TestClearingAPaperKeepsItsPaintServersThroughABaseReference()
    {
        var paper = new SnapPaper(200, 200);
        var gradient = paper.Gradient("l(0,0,1,0)#ff0000-#0000ff");
        var id = gradient.Attr("id")?.ToString();
        paper.Rect(0, 0, 200, 200).Attr("fill", $"url(#{id})");

        SnapElement asElement = paper;
        asElement.Clear();

        Assert.Contains("<defs", paper.ToString(), StringComparison.Ordinal);
        Assert.Contains(id!, paper.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("<rect", paper.ToString(), StringComparison.Ordinal);
    }

    /// <summary>The inherited factories still reach the document now the copies are gone.</summary>
    /// <remarks>
    /// Eleven of SnapPaper's methods were character-identical to the virtuals they hid, because a
    /// paper's `Node` is its `Document`. Deleting them is only safe if `paper.rect(...)` still lands
    /// in the document rather than nowhere, which is what this draws to find out.
    /// </remarks>
    [Fact]
    public void TestInheritedFactoriesStillAppendToTheDocument()
    {
        var svg = Svg("paper.rect(0, 0, 50, 50); paper.circle(100, 100, 20); paper.text(10, 180, 'hi');");

        Assert.Contains("<rect", svg, StringComparison.Ordinal);
        Assert.Contains("<circle", svg, StringComparison.Ordinal);
        Assert.Contains("hi", svg, StringComparison.Ordinal);
    }
    #endregion

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
    #endregion
}
