namespace Polson.Tests.Drawing;

using System;
using System.Globalization;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// <c>Layout.*</c> rectangle arithmetic and <c>ctx.measureWrappedText(...)</c> — the primitive that
/// lets a script place a second block under a first one.
/// </summary>
/// <remarks>
/// The measurement tests matter more than the arithmetic ones. A layout whose measuring and drawing
/// disagree produces boxes that do not contain their contents, and the disagreement is invisible in
/// any single render — it shows up as collisions later, in the one composition nobody checked.
/// </remarks>
public class LayoutToolkitTests : TestsRuntime
{
    #region Methods
    private static object? Eval(string body)
    {
        var result = new JsDrawingEngine().Execute($$"""
            const c = createCanvas(400, 300);
            const ctx = c.getContext('2d');
            {{body}}
            """, 400, 300, null, "png", 100);

        Assert.True(result.Success, result.Error);
        return result.ReturnValue;
    }

    private static float Number(string body) =>
        Convert.ToSingle(Eval(body), CultureInfo.InvariantCulture);

    private static string Text(string body) => Eval(body)?.ToString() ?? string.Empty;
    #endregion

    #region Dividing
    /// <summary>
    /// Bands sum back to the container, gaps included.
    /// </summary>
    /// <remarks>
    /// Gaps have to come out of the total before dividing. Dividing first and inserting gaps
    /// afterwards overflows by exactly the gap total, which is the commonest way a hand-rolled grid
    /// walks off its page — and it only shows at the far edge, after everything else looks right.
    /// </remarks>
    [Fact]
    public void TestRowsSumBackToTheContainer()
    {
        var span = Number("""
            const r = Layout.rect(10, 20, 300, 240);
            const bands = Layout.rows(r, 4, 12);
            bands[bands.length - 1].y2 - bands[0].y;
            """);

        Assert.Equal(240f, span, 3);
    }

    /// <summary>Columns divide the other axis and leave the container's own edges alone.</summary>
    [Fact]
    public void TestColumnsSumBackToTheContainer()
    {
        Assert.Equal(300f, Number("""
            const bands = Layout.columns(Layout.rect(10, 20, 300, 240), 3, 15);
            bands[bands.length - 1].x2 - bands[0].x;
            """), 3);
    }

    /// <summary>Weighted divisions follow the ratios given, whatever they sum to.</summary>
    [Fact]
    public void TestWeightedRowsFollowTheirRatios()
    {
        // 70-20-10, Manual 09's big/medium/small law, over 300px with no gaps.
        Assert.Equal("210,60,30", Text("""
            const bands = Layout.rows(Layout.rect(0, 0, 100, 300), [70, 20, 10], 0);
            bands.map(b => Math.round(b.height)).join(',');
            """));
    }

    /// <summary>Weights need not be normalised — only their proportions matter.</summary>
    [Fact]
    public void TestWeightsNeedNotSumToAnythingParticular()
    {
        Assert.Equal("200,100", Text("""
            const bands = Layout.rows(Layout.rect(0, 0, 50, 300), [2, 1], 0);
            bands.map(b => Math.round(b.height)).join(',');
            """));
    }

    /// <summary>A grid is row-major, so cell (row, column) is at row * columns + column.</summary>
    [Fact]
    public void TestGridIsRowMajor()
    {
        // 3 columns x 2 rows over 300x200. Index 4 is row 1, column 1.
        Assert.Equal("100,100", Text("""
            const cells = Layout.grid(Layout.rect(0, 0, 300, 200), 3, 2, 0);
            Math.round(cells[4].x) + ',' + Math.round(cells[4].y);
            """));
    }

    /// <summary>Gaps wider than the container collapse the bands rather than inverting them.</summary>
    /// <remarks>
    /// A negative extent silently reverses every comparison downstream — <c>x2 &lt; x</c>, a
    /// containment test that always fails — so it must not be representable.
    /// </remarks>
    [Fact]
    public void TestImpossibleGapsCollapseRatherThanInvert()
    {
        Assert.Equal(0f, Number("""
            const bands = Layout.rows(Layout.rect(0, 0, 100, 50), 4, 400);
            Math.min.apply(null, bands.map(b => b.height));
            """), 3);
    }
    #endregion

    #region Padding and placement
    /// <summary>Inset takes CSS shorthand in the order a stylesheet uses.</summary>
    [Fact]
    public void TestInsetFollowsCssShorthandOrder()
    {
        // top 10, right 20, bottom 30, left 40 on a 200x100 box at the origin.
        Assert.Equal("40,10,140,60", Text("""
            const r = Layout.inset(Layout.rect(0, 0, 200, 100), 10, 20, 30, 40);
            [r.x, r.y, r.width, r.height].map(Math.round).join(',');
            """));
    }

    /// <summary>One value pads all four sides.</summary>
    [Fact]
    public void TestSingleValueInsetPadsEverySide()
    {
        Assert.Equal("12,12,176,76", Text("""
            const r = Layout.inset(Layout.rect(0, 0, 200, 100), 12);
            [r.x, r.y, r.width, r.height].map(Math.round).join(',');
            """));
    }

    /// <summary>Padding bigger than the box collapses it to zero, never past it.</summary>
    [Fact]
    public void TestOverlargeInsetCollapsesToZero()
    {
        Assert.Equal("0,0", Text("""
            const r = Layout.inset(Layout.rect(0, 0, 40, 40), 90);
            [r.width, r.height].join(',');
            """));
    }

    /// <summary>Outset is inset's inverse, so the two round-trip.</summary>
    [Fact]
    public void TestOutsetInvertsInset()
    {
        Assert.Equal("0,0,200,100", Text("""
            const r = Layout.outset(Layout.inset(Layout.rect(0, 0, 200, 100), 15), 15);
            [r.x, r.y, r.width, r.height].map(Math.round).join(',');
            """));
    }

    /// <summary>The nine anchors put a box where they say.</summary>
    [Fact]
    public void TestPlaceHonoursItsAnchor()
    {
        Assert.Equal("180,80", Text("""
            const r = Layout.place(Layout.rect(0, 0, 200, 100), 20, 20, 'bottomRight');
            [r.x, r.y].map(Math.round).join(',');
            """));
        Assert.Equal("90,40", Text("""
            const r = Layout.center(Layout.rect(0, 0, 200, 100), 20, 20);
            [r.x, r.y].map(Math.round).join(',');
            """));
    }

    /// <summary>Anchor spelling is forgiving about case and separators.</summary>
    [Fact]
    public void TestAnchorSpellingIsForgiving()
    {
        var canonical = Text("const r = Layout.place(Layout.rect(0,0,200,100), 20, 20, 'bottomRight'); r.x + ',' + r.y;");
        Assert.Equal(canonical, Text("const r = Layout.place(Layout.rect(0,0,200,100), 20, 20, 'bottom-right'); r.x + ',' + r.y;"));
        Assert.Equal(canonical, Text("const r = Layout.place(Layout.rect(0,0,200,100), 20, 20, 'BOTTOM RIGHT'); r.x + ',' + r.y;"));
    }

    /// <summary>Bounds wraps a set of placed rectangles.</summary>
    [Fact]
    public void TestBoundsWrapsEveryRectangle()
    {
        Assert.Equal("10,5,140,95", Text("""
            const b = Layout.bounds([
                Layout.rect(10, 20, 40, 40),
                Layout.rect(120, 5, 30, 30),
                Layout.rect(50, 60, 20, 40)
            ]);
            [b.x, b.y, b.width, b.height].map(Math.round).join(',');
            """));
    }

    /// <summary>An empty set returns a zero rectangle rather than throwing.</summary>
    [Fact]
    public void TestEmptyBoundsIsZeroNotAnError()
    {
        Assert.Equal("0,0,0,0", Text("""
            const b = Layout.bounds([]);
            [b.x, b.y, b.width, b.height].join(',');
            """));
    }

    /// <summary>
    /// Layout accepts any object shaped like a rectangle, whoever produced it.
    /// </summary>
    /// <remarks>
    /// The toolkit is structural on purpose: this is what lets it compose with <c>getBBox()</c>,
    /// <c>subdivideProportions</c> and <c>measureWrappedText</c> without any of them knowing about
    /// each other.
    /// </remarks>
    [Fact]
    public void TestAnyRectangleShapedObjectIsAccepted()
    {
        Assert.Equal("20,20,60,60", Text("""
            const literal = { x: 10, y: 10, width: 80, height: 80 };
            const r = Layout.inset(literal, 10);
            [r.x, r.y, r.width, r.height].map(Math.round).join(',');
            """));
    }
    #endregion

    #region Measuring before placing
    /// <summary>
    /// A measured block reports the same size the drawn block occupies.
    /// </summary>
    /// <remarks>
    /// The property the whole stage rests on. Measuring and drawing share one wrapping and one
    /// measurement path, so this cannot drift — which is exactly what a width-estimating layout
    /// engine cannot promise.
    /// </remarks>
    [Fact]
    public void TestAMeasuredBlockMatchesTheDrawnBlock()
    {
        Assert.Equal("same", Text("""
            ctx.font = '400 15px sans-serif';
            const body = 'Every block reports the box it will occupy, so the next one knows where to begin.';
            const measured = ctx.measureWrappedText(body, 220, 22);
            const drawn = ctx.fillWrappedText(body, 10, 10, 220, 22);
            (measured.height === drawn.height && measured.width === drawn.width &&
             measured.lineCount === drawn.lineCount) ? 'same' : 'drifted';
            """));
    }

    /// <summary>Height is the line count times the line height, so stacking is exact.</summary>
    [Fact]
    public void TestBlockHeightIsLineCountTimesLineHeight()
    {
        Assert.Equal("exact", Text("""
            ctx.font = '400 15px sans-serif';
            const m = ctx.measureWrappedText('one two three four five six seven eight nine ten', 120, 20);
            (m.lineCount > 1 && m.height === m.lineCount * m.lineHeight) ? 'exact' : 'off';
            """));
    }

    /// <summary>A measurement is unanchored — only its size is meaningful.</summary>
    [Fact]
    public void TestAMeasurementIsNotPositioned()
    {
        Assert.Equal("0,0,false", Text("""
            ctx.font = '400 15px sans-serif';
            const m = ctx.measureWrappedText('some words to wrap here', 100);
            [m.x, m.y, m.positioned].join(',');
            """));
    }

    /// <summary>A drawn block is anchored, and reports where the ink actually landed.</summary>
    [Fact]
    public void TestADrawnBlockIsPositionedWhereTheInkLanded()
    {
        // textBaseline 'top' is the case where the reported box starts at the y that was passed.
        Assert.Equal("40,60,true", Text("""
            ctx.font = '400 15px sans-serif';
            ctx.textBaseline = 'top';
            const b = ctx.fillWrappedText('some words to wrap here', 40, 60, 100);
            [Math.round(b.x), Math.round(b.y), b.positioned].join(',');
            """));
    }

    /// <summary>Under a centred alignment the box moves left, because the ink did.</summary>
    [Fact]
    public void TestACentredBlockReportsItsLeftEdge()
    {
        Assert.Equal("shifted left", Text("""
            ctx.font = '400 15px sans-serif';
            ctx.textBaseline = 'top';
            ctx.textAlign = 'center';
            const b = ctx.fillWrappedText('some words to wrap here', 200, 10, 140);
            (Math.abs(b.cx - 200) < 1.5) ? 'shifted left' : 'anchor ignored';
            """));
    }

    /// <summary>Tracking widens a measured block, because it widens the drawn one.</summary>
    [Fact]
    public void TestTrackingIsReflectedInTheMeasurement()
    {
        Assert.Equal("wider", Text("""
            ctx.font = '400 15px sans-serif';
            const plain = ctx.measureWrappedText('spacing changes the wrap', 300).width;
            ctx.letterSpacing = '4px';
            const tracked = ctx.measureWrappedText('spacing changes the wrap', 300).width;
            tracked > plain ? 'wider' : 'ignored';
            """));
    }

    /// <summary>Empty text is a zero-height block, not a failure.</summary>
    [Fact]
    public void TestEmptyTextMeasuresAsZeroHeight()
    {
        Assert.Equal("0,0", Text("""
            const m = ctx.measureWrappedText('', 200);
            [m.height, m.lineCount].join(',');
            """));
    }

    /// <summary>
    /// Measure, stack, draw — the whole point of the stage, end to end.
    /// </summary>
    /// <remarks>
    /// Asserts the thing an agent actually needs: that the second block begins below the first and
    /// they do not overlap, at a height nobody had to guess.
    /// </remarks>
    [Fact]
    public void TestMeasureStackDrawDoesNotOverlap()
    {
        Assert.Equal("clear", Text("""
            ctx.textBaseline = 'top';
            const panel = Layout.inset(Layout.rect(0, 0, 400, 300), 20);

            ctx.font = '700 22px sans-serif';
            const head = ctx.measureWrappedText('A heading that is long enough to wrap', panel.width);
            ctx.font = '400 14px sans-serif';
            const body = ctx.measureWrappedText('Body copy that also wraps across several lines of the panel.', panel.width, 20);

            const boxes = Layout.stack(panel, [head.height, body.height], 14);

            ctx.font = '700 22px sans-serif';
            const drawnHead = ctx.fillWrappedText('A heading that is long enough to wrap', boxes[0].x, boxes[0].y, boxes[0].width);
            ctx.font = '400 14px sans-serif';
            const drawnBody = ctx.fillWrappedText('Body copy that also wraps across several lines of the panel.', boxes[1].x, boxes[1].y, boxes[1].width, 20);

            (drawnBody.y >= drawnHead.y2) ? 'clear' : 'overlap';
            """));
    }

    /// <summary>Stack reports overflow by position rather than clipping it away.</summary>
    [Fact]
    public void TestStackLetsOverflowBeVisible()
    {
        Assert.Equal("overflowing", Text("""
            const panel = Layout.rect(0, 0, 200, 100);
            const boxes = Layout.stack(panel, [60, 60, 60], 10);
            (boxes[boxes.length - 1].y2 > panel.y2) ? 'overflowing' : 'silently fitted';
            """));
    }
    #endregion
}
