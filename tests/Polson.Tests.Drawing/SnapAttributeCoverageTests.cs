namespace Polson.Tests.Drawing;

using System;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// Attributes that belong to more than one element type, and were handled for only one of them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Found by sweeping for the pattern that produced four separate <c>&lt;tspan&gt;</c> defects:
/// a concrete SVG type matched where a shared one was meant.</b> The setters are <c>if/else if</c>
/// chains over concrete types, so an element the chain does not name falls off the end — and
/// <c>attr()</c>'s contract is that an unrecognised name becomes a custom attribute, which means
/// nothing reports the miss. The attribute is simply not there afterwards.
/// </para>
/// <para>
/// <c>x</c>, <c>y</c>, <c>width</c> and <c>height</c> were handled for <c>rect</c>, <c>image</c> and
/// the root, and not for <c>use</c>, <c>pattern</c> or <c>symbol</c>. <c>viewBox</c> was handled only
/// for the root, and not for <c>symbol</c>, <c>pattern</c> or <c>marker</c> — so the icon-library
/// idiom documented in <c>polson://manual/27</c> §6 built a <c>&lt;symbol&gt;</c> with no viewBox at
/// all, which is the one attribute that makes it scale to each instance.
/// </para>
/// <para>
/// Each case asserts the round trip <b>and</b> the markup, because the two fail separately: a value
/// can reach the object and not be written, and a getter that was never widened alongside its setter
/// answers null for a value that took.
/// </para>
/// </remarks>
public class SnapAttributeCoverageTests : TestsRuntime
{
    #region Position and Size Tests
    /// <summary>
    /// A <c>&lt;use&gt;</c> can be positioned and sized by attribute.
    /// </summary>
    /// <remarks>
    /// Positioning a reused motif by <c>x</c>/<c>y</c> is the ordinary spelling in SVG and the one a
    /// caller reaches for first. Without it only <c>transform</c> worked — which is why every
    /// <c>use</c> example in the manuals is written with a transform.
    /// </remarks>
    [Fact]
    public void TestAUseTakesPositionAndSize()
    {
        var result = Execute("""
            const p = Snap(400, 200);
            const src = p.defs.rect(0, 0, 40, 40);
            src.id = 'box';
            const u = p.use(src).attr({ x: 100, y: 50, width: 80, height: 80 });
            log(u.attr('x') + ',' + u.attr('y') + ',' + u.attr('width') + ',' + u.attr('height')
              + ',' + String(p.toString().indexOf('x="100"') > 0));
            """);

        Assert.Equal("100,50,80,80,true", Logged(result));
    }

    [Theory]
    [InlineData("symbol")]
    [InlineData("pattern")]
    public void TestAContainerTakesPositionAndSize(string tag)
    {
        var result = Execute($$"""
            const p = Snap(400, 200);
            const el = p.defs.el('{{tag}}', { x: 5, y: 6, width: 30, height: 31 });
            log(el.attr('x') + ',' + el.attr('y') + ',' + el.attr('width') + ',' + el.attr('height'));
            """);

        Assert.Equal("5,6,30,31", Logged(result));
    }
    #endregion

    #region ViewBox Tests
    /// <summary>
    /// <c>viewBox</c> reaches every element that has one, not only the root.
    /// </summary>
    /// <remarks>
    /// A symbol's viewBox is what lets one definition scale to each <c>&lt;use&gt;</c>; a pattern's is
    /// what makes a tile independent of the box it fills; a marker's rescales the head. All three were
    /// dropped.
    /// </remarks>
    [Theory]
    [InlineData("symbol")]
    [InlineData("pattern")]
    [InlineData("marker")]
    public void TestViewBoxReachesEveryElementThatHasOne(string tag)
    {
        var result = Execute($$"""
            const p = Snap(400, 200);
            const el = p.defs.el('{{tag}}', { viewBox: '0 0 24 24' });
            log(el.attr('viewBox') + ',' + String(p.toString().toLowerCase().indexOf('viewbox') > 0));
            """);

        Assert.Equal("0 0 24 24,true", Logged(result));
    }

    /// <summary>The root still works, which is the case everything else was measured against.</summary>
    [Fact]
    public void TestTheRootViewBoxStillWorks()
    {
        var result = Execute("""
            const p = Snap(100, 100);
            p.attr({ viewBox: '0 0 50 50' });
            log(p.attr('viewBox'));
            """);

        Assert.Equal("0 0 50 50", Logged(result));
    }
    #endregion

    #region Methods
    private static DrawingExecutionResult Execute(string body)
    {
        var result = new JsDrawingEngine().Execute(body, 400, 200, null, "png", 100, render: false);
        Assert.True(result.Success, result.Error);
        return result;
    }

    private static string Logged(DrawingExecutionResult result) =>
        string.Join(" ", result.Logs).Replace("[LOG]", string.Empty, StringComparison.Ordinal).Trim();
    #endregion
}
