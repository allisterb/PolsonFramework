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

    #region Reuse Tests
    /// <summary>
    /// A <c>&lt;use&gt;</c> can only paint what the referenced element leaves unset.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Standard SVG cascade, and the single thing most likely to be got wrong about reuse: the
    /// reference clones the source <i>including its presentation attributes</i>, so a <c>fill</c> on
    /// the source wins and a <c>fill</c> on the <c>use</c> is inert. Every instance then comes out
    /// identical, silently — the drawing is right in structure and wrong in colour, with nothing to
    /// report.
    /// </para>
    /// <para>
    /// Pinned because it defeated the worked example in <c>polson://manual/27</c> §10 on the day it was
    /// written: a five-star rating whose fifth star was meant to be dimmed rendered five gold stars,
    /// because the source star carried the fill. The remedy is the second half of this test — park the
    /// motif in <c>&lt;defs&gt;</c> with no fill of its own, where it also does not draw, and let each
    /// instance carry its own paint.
    /// </para>
    /// </remarks>
    [Fact]
    public void TestAUseRecoloursOnlyWhatTheSourceLeavesUnset()
    {
        // The source carries the fill, so the instance cannot override it.
        var bound = Execute("""
            const s = paper.path('M22,0 L28,15 L44,15 L32,25 L37,41 L22,32 L7,41 L12,25 L0,15 L16,15 Z');
            s.id = 'star';
            s.attr({ fill: '#e5a93c' });
            paper.use(s).attr({ transform: 't100,0', fill: '#e6e8ec' });
            const bmp = Skia.Image.fromBytes(paper.toImageBytes(200, 200, 'png', 100));
            log(bmp.getPixel(122, 20));
            """);
        Assert.True(bound.Success, bound.Error);
        Assert.Contains("#E5A93C", string.Join(" ", bound.Logs), StringComparison.Ordinal);

        // The source leaves it unset, so each instance paints itself.
        var free = Execute("""
            const s = paper.defs.path('M22,0 L28,15 L44,15 L32,25 L37,41 L22,32 L7,41 L12,25 L0,15 L16,15 Z');
            s.id = 'star';
            paper.use(s).attr({ fill: '#e5a93c' });
            paper.use(s).attr({ transform: 't100,0', fill: '#e6e8ec' });
            const bmp = Skia.Image.fromBytes(paper.toImageBytes(200, 200, 'png', 100));
            log(bmp.getPixel(22, 20) + ' ' + bmp.getPixel(122, 20));
            """);
        Assert.True(free.Success, free.Error);
        var line = string.Join(" ", free.Logs);
        Assert.Contains("#E5A93C", line, StringComparison.Ordinal);
        Assert.Contains("#E6E8EC", line, StringComparison.Ordinal);
    }

    /// <summary>An element parked in <c>&lt;defs&gt;</c> does not draw where it is defined.</summary>
    /// <remarks>
    /// The other half of the recolourable-motif idiom. Leaving the source's <c>fill</c> unset so that
    /// instances can paint it makes the source itself render in SVG's default black — so it has to go
    /// somewhere that does not draw, or the drawing gains a stray black shape at the origin.
    /// </remarks>
    [Fact]
    public void TestAnElementInDefsDoesNotDraw()
    {
        var result = Execute("""
            const s = paper.defs.path('M0,0 L40,0 L40,40 L0,40 Z');
            s.id = 'box';
            paper.use(s).attr({ transform: 't120,0', fill: '#1f6f8b' });
            const bmp = Skia.Image.fromBytes(paper.toImageBytes(200, 200, 'png', 100));
            log(bmp.getPixel(20, 20) + ' ' + bmp.getPixel(140, 20));
            """);

        Assert.True(result.Success, result.Error);
        var line = string.Join(" ", result.Logs);
        Assert.Contains("#00000000", line, StringComparison.Ordinal);
        Assert.Contains("#1F6F8B", line, StringComparison.Ordinal);
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
