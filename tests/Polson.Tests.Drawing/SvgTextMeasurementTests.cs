namespace Polson.Tests.Drawing;

using System;
using System.Globalization;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// <c>text.getBBox()</c> on the vector side, measured with real font metrics.
/// </summary>
/// <remarks>
/// Every other SVG element states its extent from its own attributes; text cannot, because its
/// extent depends on the glyphs of a particular face at a particular size. It previously returned
/// <c>0×0</c>, which meant a vector-first piece had <b>no way to measure type at all</b> — and
/// nothing can be placed under a line of text whose height is unknown.
/// <para>
/// The property that matters most here is agreement with Canvas2D. A vector layout and a raster one
/// that disagreed about the width of the same words would be worse than neither having the call,
/// because the disagreement would only surface once something overlapped.
/// </para>
/// </remarks>
public class SvgTextMeasurementTests : TestsRuntime
{
    #region Methods
    private static object? Eval(string body)
    {
        var result = new JsDrawingEngine().Execute(body, 100, 100, null, "png", 100);
        Assert.True(result.Success, result.Error);
        return result.ReturnValue;
    }

    private static string Text(string body) => Eval(body)?.ToString() ?? string.Empty;

    private static float Number(string body) =>
        Convert.ToSingle(Eval(body), CultureInfo.InvariantCulture);
    #endregion

    #region Agreement with the canvas
    /// <summary>
    /// The same string, family and size measure the same on both sides of the SDK.
    /// </summary>
    /// <remarks>
    /// Georgia and Courier New differ enough in advance width that a stub returning a guess would
    /// not pass both.
    /// </remarks>
    [Theory]
    [InlineData("Georgia", 20)]
    [InlineData("Georgia", 40)]
    [InlineData("Courier New", 20)]
    [InlineData("Arial", 32)]
    public void TestSvgTextMeasuresAsTheCanvasDoes(string family, int size)
    {
        var delta = Number($$"""
            const sample = 'Handgloves and hamburgefonstiv';
            const paper = Snap(400, 200);
            const t = paper.text(10, 40, sample);
            t.attr({ 'font-family': '{{family}}', 'font-size': {{size}} });

            const ctx = createCanvas(10, 10).getContext('2d');
            ctx.font = '{{size}}px "{{family}}"';
            Math.abs(t.getBBox().width - ctx.measureText(sample).width);
            """);

        Assert.True(delta < 0.5f, $"svg and canvas disagreed by {delta}px for {family} {size}px");
    }

    /// <summary>
    /// A numeric font-weight reaches the measurement.
    /// </summary>
    /// <remarks>
    /// <c>SvgFontWeight.W700</c> is an enum member whose <i>ordinal</i> is not 700, so casting it to
    /// a numeric weight measures everything as regular — silently, since the text still renders and
    /// the number still looks plausible. This is the test that caught it.
    /// </remarks>
    [Fact]
    public void TestFontWeightReachesTheMeasurement()
    {
        Assert.Equal("bolder,matches-canvas", Text("""
            const sample = 'Handgloves and hamburgefonstiv';
            const paper = Snap(400, 200);
            const mk = (w) => {
                const t = paper.text(10, 40, sample);
                t.attr({ 'font-family': 'Arial', 'font-size': 20, 'font-weight': w });
                return t.getBBox().width;
            };
            const regular = mk(400), bold = mk(700);

            const ctx = createCanvas(10, 10).getContext('2d');
            ctx.font = '700 20px Arial';
            const canvasBold = ctx.measureText(sample).width;

            [bold > regular ? 'bolder' : 'ignored',
             Math.abs(bold - canvasBold) < 0.5 ? 'matches-canvas' : 'differs'].join(',');
            """));
    }
    #endregion

    #region Geometry
    /// <summary>Height comes from the font's own ascent and descent, and scales with size.</summary>
    [Fact]
    public void TestHeightScalesWithFontSize()
    {
        Assert.Equal("2.00", Text("""
            const paper = Snap(400, 200);
            const at = (size) => {
                const t = paper.text(10, 40, 'Hxg');
                t.attr({ 'font-family': 'Georgia', 'font-size': size });
                return t.getBBox().height;
            };
            (at(40) / at(20)).toFixed(2);
            """));
    }

    /// <summary>
    /// The box sits above the baseline, because SVG's <c>y</c> is the baseline.
    /// </summary>
    /// <remarks>
    /// Getting this backwards would put every measured block a line-height too low, which is exactly
    /// the kind of error that looks like a design choice.
    /// </remarks>
    [Fact]
    public void TestTheBoxSitsAboveTheBaseline()
    {
        Assert.Equal("above,descends", Text("""
            const paper = Snap(400, 200);
            const t = paper.text(10, 100, 'Hxg');
            t.attr({ 'font-family': 'Georgia', 'font-size': 20 });
            const b = t.getBBox();
            [b.y < 100 ? 'above' : 'below',
             b.y2 > 100 ? 'descends' : 'no-descender'].join(',');
            """));
    }

    /// <summary>`text-anchor` moves the box, so a centred run reports a centred box.</summary>
    [Fact]
    public void TestTextAnchorMovesTheBox()
    {
        Assert.Equal("start,middle,end", Text("""
            const paper = Snap(400, 200);
            const box = (anchor) => {
                const t = paper.text(200, 100, 'centred');
                t.attr({ 'font-family': 'Georgia', 'font-size': 20, 'text-anchor': anchor });
                return t.getBBox();
            };
            const s = box('start'), m = box('middle'), e = box('end');
            [Math.abs(s.x - 200) < 0.5 ? 'start' : 'x=' + s.x,
             Math.abs(m.cx - 200) < 0.5 ? 'middle' : 'cx=' + m.cx,
             Math.abs(e.x2 - 200) < 0.5 ? 'end' : 'x2=' + e.x2].join(',');
            """));
    }

    /// <summary>A font-family that is missing falls through the list rather than to the default.</summary>
    [Fact]
    public void TestFallbackListIsHonoured()
    {
        Assert.Equal("same", Text("""
            const paper = Snap(400, 200);
            const w = (family) => {
                const t = paper.text(10, 40, 'Handgloves');
                t.attr({ 'font-family': family, 'font-size': 20 });
                return t.getBBox().width;
            };
            Math.abs(w("'Nonexistent QQQ', 'Courier New'") - w('Courier New')) < 0.5 ? 'same' : 'fell to default';
            """));
    }
    #endregion

    #region Composing with the layout toolkit
    /// <summary>
    /// A measured vector heading can be stacked under, which is the point of the whole change.
    /// </summary>
    /// <remarks>
    /// This is the workflow's central move — measure, then place beneath — performed entirely on the
    /// vector side, which was impossible before.
    /// </remarks>
    [Fact]
    public void TestAVectorHeadingCanBeStackedUnder()
    {
        Assert.Equal("clear", Text("""
            const paper = Snap(600, 300);
            const heading = paper.text(40, 60, 'A heading of some length');
            heading.attr({ 'font-family': 'Georgia', 'font-size': 34 });
            const hb = heading.getBBox();

            // Place the rule and the body from the measured box, not from a guess.
            const rule = paper.line(40, hb.y2 + 12, 40 + hb.width, hb.y2 + 12);
            const body = paper.text(40, hb.y2 + 40, 'Body copy that follows it');
            body.attr({ 'font-family': 'Georgia', 'font-size': 16 });

            (body.getBBox().y > hb.y2 && rule.getBBox().y > hb.y2) ? 'clear' : 'overlap';
            """));
    }

    /// <summary>Empty text measures as nothing rather than throwing.</summary>
    [Fact]
    public void TestEmptyTextMeasuresAsZero()
    {
        Assert.Equal(0f, Number("""
            const paper = Snap(400, 200);
            paper.text(10, 40, '').getBBox().width;
            """), 3);
    }
    #endregion
}
