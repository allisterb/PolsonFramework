namespace Polson.Tests.Drawing;

using System;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// Children of a <c>&lt;text&gt;</c> — <c>&lt;tspan&gt;</c> and <c>&lt;textPath&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Both were accepted, reported success, and produced nothing at all.</b> Four separate defects
/// stacked, each silent, and every one of them matched <see cref="Svg.SvgText"/> where it should have
/// matched <see cref="Svg.SvgTextBase"/>:
/// </para>
/// <list type="number">
/// <item><c>SvgTextBase</c> serialises its <c>Nodes</c> and ignores <c>Children</c> whenever
/// <c>Nodes</c> is non-empty — and setting <c>Text</c> puts a node there <b>even for an empty
/// string</b>, so <c>paper.text(x, y, '')</c>, the natural span container, suppressed every child.</item>
/// <item><c>El(...)</c> added straight to <c>Children</c>, bypassing the fix in <c>Append</c>.</item>
/// <item><c>attr({ text })</c> matched <c>SvgText</c>, so a span could never carry content.</item>
/// <item><c>attr({ x, y })</c> matched <c>SvgText</c>, so a span could never be positioned; and
/// <c>attr({ href })</c> handled only <c>SvgImage</c> and <c>SvgUse</c>, so a <c>textPath</c>
/// referenced nothing and laid its glyphs along no path.</item>
/// </list>
/// <para>
/// The cost of the silence was not only the feature. <c>polson://manual/14</c> §9,
/// <c>Polson.core.md</c> and the <c>vector_infographic</c> instructions all told an agent to set a
/// paragraph by placing <c>&lt;tspan&gt;</c> lines — advice that produced an empty frame and a success
/// status. These tests assert the rendered pixels, not the markup, because markup was present and
/// correct at the point where two of the four defects were still live.
/// </para>
/// </remarks>
public class SnapTextChildTests : TestsRuntime
{
    #region Rendering Tests
    /// <summary>A positioned span inside an empty text container draws.</summary>
    [Fact]
    public void TestATspanInAnEmptyContainerRenders()
    {
        var result = Execute("""
            const p = Snap(400, 120);
            p.rect(0, 0, 400, 120).attr({ fill: '#ffffff' });
            const t = p.text(0, 0, '').attr({ 'font-size': 28, fill: '#000000' });
            t.el('tspan', { x: 20, y: 60 }).attr({ text: 'TSPAN TEXT' });
            const bmp = Skia.Image.fromBytes(p.toImageBytes(400, 120, 'png', 100));
            log(String(bmp.palette(4).length > 1));
            """);

        Assert.Equal("true", Logged(result));
    }

    /// <summary>Text laid along a referenced path draws.</summary>
    [Fact]
    public void TestATextPathRenders()
    {
        var result = Execute("""
            const p = Snap(400, 160);
            p.rect(0, 0, 400, 160).attr({ fill: '#ffffff' });
            const curve = p.defs.path('M20,120 Q200,30 380,120');
            curve.id = 'curve';
            const t = p.text(0, 0, '').attr({ 'font-size': 22, fill: '#000000' });
            t.el('textPath', { href: '#curve' }).attr({ text: 'TEXT ALONG A CURVE' });
            const bmp = Skia.Image.fromBytes(p.toImageBytes(400, 160, 'png', 100));
            log(String(bmp.palette(4).length > 1));
            """);

        Assert.Equal("true", Logged(result));
    }

    /// <summary>
    /// A multi-line paragraph built from spans, which is what three documents advise.
    /// </summary>
    /// <remarks>
    /// The end-to-end case rather than a unit: this is the technique
    /// <c>vector_infographic/instructions.md</c> names for setting a paragraph, and it drew nothing.
    /// </remarks>
    [Fact]
    public void TestAParagraphOfSpansRenders()
    {
        var result = Execute("""
            const p = Snap(400, 140);
            p.rect(0, 0, 400, 140).attr({ fill: '#ffffff' });
            const t = p.text(0, 0, '').attr({ 'font-size': 18, fill: '#000000' });
            const lines = ['first line of the paragraph', 'second line of the paragraph'];
            for (let i = 0; i < lines.length; i++) {
                t.el('tspan', { x: 20, y: 40 + i * 26 }).attr({ text: lines[i] });
            }
            const bmp = Skia.Image.fromBytes(p.toImageBytes(400, 140, 'png', 100));
            const rows = bmp.rowProfile('#000000', { tolerance: 60 });
            log(String(rows.length > 0));
            """);

        Assert.Equal("true", Logged(result));
    }
    #endregion

    #region Attribute Tests
    /// <summary>A span's own <c>x</c>/<c>y</c> survive into the markup and read back.</summary>
    /// <remarks>
    /// Without these the span inherits the container's origin. A container is conventionally made at
    /// <c>(0, 0)</c>, so every line landed on a baseline at <c>y = 0</c> — off the top of the canvas,
    /// which looks exactly like "spans do not render".
    /// </remarks>
    [Fact]
    public void TestASpanCarriesItsOwnPosition()
    {
        var result = Execute("""
            const p = Snap(200, 100);
            const t = p.text(0, 0, '');
            const span = t.el('tspan', { x: 20, y: 60 }).attr({ text: 'X' });
            log(span.attr('x') + ',' + span.attr('y') + ',' + (p.toString().indexOf('x="20"') >= 0));
            """);

        Assert.Equal("20,60,true", Logged(result));
    }

    /// <summary>
    /// A span measures its own glyphs, and its container measures the union of its spans.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Found by sweeping for other <c>SvgText</c> matches that should have been <c>SvgTextBase</c>.
    /// The bounds dispatch matched the concrete type, so a span — a <i>sibling</i> of
    /// <c>&lt;text&gt;</c>, not a subclass — fell through to the children branch and, having no
    /// element children of its own, measured <b>0×0</b>.
    /// </para>
    /// <para>
    /// <b>The consequence reached further than measurement.</b> The label-collision check in the
    /// <c>vector_infographic</c> workflow is built on <c>getBBox()</c>, so a page of overlapping
    /// spans would have been reported as a clean sheet — a silent gap in the check whose entire
    /// purpose is catching silent overlaps.
    /// </para>
    /// </remarks>
    [Fact]
    public void TestASpanMeasuresItselfAndItsContainerUnionsThem()
    {
        var result = Execute("""
            const p = Snap(500, 200);
            const t = p.text(0, 0, '').attr({ 'font-size': 18, 'font-family': 'Arial' });
            const first = t.el('tspan', { x: 20, y: 40 }).attr({ text: 'first line of the paragraph' });
            t.el('tspan', { x: 20, y: 66 }).attr({ text: 'second line' });
            const span = first.getBBox();
            const box = t.getBBox();
            log(String(span.width > 0 && span.height > 0) + ',' + String(box.height > span.height));
            """);

        Assert.Equal("true,true", Logged(result));
    }

    /// <summary>A <c>textPath</c> keeps the path it references.</summary>
    [Fact]
    public void TestATextPathKeepsItsReference()
    {
        var result = Execute("""
            const p = Snap(200, 100);
            const c = p.defs.path('M10,80 Q100,10 190,80');
            c.id = 'curve';
            const t = p.text(0, 0, '');
            t.el('textPath', { href: '#curve' }).attr({ text: 'ALONG' });
            log(String(p.toString().indexOf('#curve') > 0 && p.toString().indexOf('<textPath') > 0));
            """);

        Assert.Equal("true", Logged(result));
    }
    #endregion

    #region Mixed Content Tests
    /// <summary>Text then a span writes both, in order.</summary>
    /// <remarks>
    /// The writer takes <c>Nodes</c> once it has any, so a child appended to a text element that
    /// already carries content has to join <c>Nodes</c> too or it is written nowhere.
    /// </remarks>
    [Fact]
    public void TestMixedContentWritesBoth()
    {
        var result = Execute("""
            const p = Snap(300, 80);
            const t = p.text(10, 40, 'LEAD ').attr({ 'font-size': 16 });
            t.el('tspan', {}).attr({ text: 'SPAN' });
            const xml = p.toString();
            log(String(xml.indexOf('LEAD') > 0 && xml.indexOf('<tspan') > 0));
            """);

        Assert.Equal("true", Logged(result));
    }

    /// <summary>Removing a span takes it out of the mixed content too.</summary>
    /// <remarks>
    /// The second reference is the price of mixed content: <c>Children.Remove</c> does not touch
    /// <c>Nodes</c>, so without this a removed span keeps serialising from a parent it has left.
    /// </remarks>
    [Fact]
    public void TestRemovingASpanTakesItOutOfMixedContent()
    {
        var result = Execute("""
            const p = Snap(300, 80);
            const t = p.text(10, 40, 'LEAD ').attr({ 'font-size': 16 });
            const span = t.el('tspan', {}).attr({ text: 'GONE' });
            span.remove();
            log(String(p.toString().indexOf('GONE') < 0));
            """);

        Assert.Equal("true", Logged(result));
    }

    /// <summary>Ordinary text is untouched by all of the above.</summary>
    /// <remarks>
    /// The regression that matters most: every plate in the corpus is built from plain
    /// <c>paper.text(...)</c>, so a fix to span handling that disturbed it would be far worse than the
    /// bug.
    /// </remarks>
    [Fact]
    public void TestPlainTextIsUnaffected()
    {
        var result = Execute("""
            const p = Snap(200, 60);
            p.rect(0, 0, 200, 60).attr({ fill: '#ffffff' });
            p.text(10, 40, 'PLAIN').attr({ 'font-size': 16, fill: '#000000' });
            const bmp = Skia.Image.fromBytes(p.toImageBytes(200, 60, 'png', 100));
            log(String(p.toString().indexOf('>PLAIN<') > 0) + ',' + String(bmp.palette(4).length > 1));
            """);

        Assert.Equal("true,true", Logged(result));
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
