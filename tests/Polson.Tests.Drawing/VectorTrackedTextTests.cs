namespace Polson.Tests.Drawing;

using System;
using System.Linq;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// Tracked type on a vector paper — <c>paper.trackedText(...)</c> and <c>measureTrackedText(...)</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>SVG's own <c>letter-spacing</c> does not render here</b>, which is pinned separately by
/// <see cref="CssSupportMatrixTests"/>. That made a tracked wordmark correct on canvas and untracked
/// as vector — silently, since the declaration serialises perfectly — on the surface a wordmark is
/// most likely to ship on. These tests pin the remedy: tracking converted into per-glyph positions,
/// which the renderer does honour.
/// </para>
/// <para>
/// The load-bearing claim is <see cref="TestTheTwoSurfacesMeasureATrackedRunIdentically"/>. A vector
/// implementation that tracked by <i>some</i> amount would look right and still break a lockup
/// designed on canvas, so the two are asserted equal rather than merely both non-zero.
/// </para>
/// </remarks>
public class VectorTrackedTextTests : TestsRuntime
{
    #region Rendering Tests
    /// <summary>The gap itself: tracking has to reach the render.</summary>
    [Fact]
    public void TestTrackingWidensTheRenderedRun()
    {
        var result = Execute("""
            log(InkWidth(p => p.text(20, 60, 'HANDGLOVES').attr(A)) + ' '
              + InkWidth(p => p.trackedText(20, 60, 'HANDGLOVES', 12, A)));
            """);

        var parts = Numbers(result);
        Assert.Equal(2, parts.Length);
        Assert.True(parts[0] > 0, "the untracked control drew nothing");

        // Nine gaps of 12px between ten glyphs.
        Assert.Equal(parts[0] + 108f, parts[1], 1f);
    }

    /// <summary>Zero tracking must not disturb the run, or the call is unusable as a default.</summary>
    [Fact]
    public void TestZeroTrackingMatchesPlainText()
    {
        var result = Execute("""
            log(InkWidth(p => p.text(20, 60, 'HANDGLOVES').attr(A)) + ' '
              + InkWidth(p => p.trackedText(20, 60, 'HANDGLOVES', 0, A)));
            """);

        var parts = Numbers(result);
        Assert.Equal(parts[0], parts[1], 1f);
    }

    /// <summary>An <c>em</c> tracking resolves against the font size, as it does on canvas.</summary>
    [Fact]
    public void TestEmTrackingResolvesAgainstFontSize()
    {
        // 0.15em at 40px is 6px, so nine gaps add 54px.
        var result = Execute("""
            log(InkWidth(p => p.text(20, 60, 'HANDGLOVES').attr(A)) + ' '
              + InkWidth(p => p.trackedText(20, 60, 'HANDGLOVES', '0.15em', A)));
            """);

        var parts = Numbers(result);
        Assert.Equal(parts[0] + 54f, parts[1], 1f);
    }

    /// <summary>The run reaches the deliverable as real, individually placed text elements.</summary>
    /// <remarks>
    /// Not tspans: this renderer's serialiser drops child nodes of a <c>&lt;text&gt;</c> entirely, so a
    /// span-per-glyph run is absent from the markup and from the render while reporting success.
    /// </remarks>
    [Fact]
    public void TestEachGlyphIsItsOwnTextElementInTheMarkup()
    {
        var result = Execute("""
            const p = Snap(700, 90);
            const g = p.trackedText(20, 60, 'ABC', 10, A);
            const xml = p.toString();
            log((xml.split('<text').length - 1) + ' ' + g.children.length);
            """);

        var parts = Numbers(result);
        Assert.Equal(3f, parts[0]);
        Assert.Equal(3f, parts[1]);
    }
    #endregion

    #region Agreement Tests
    /// <summary>
    /// Canvas and vector measure the same tracked run to the same width.
    /// </summary>
    /// <remarks>
    /// The whole point of the call. A lockup laid out on canvas and delivered as vector has to
    /// occupy the same space, so this asserts equality rather than mere non-zero tracking — the two
    /// share the arithmetic (per-glyph advances, spacing between glyphs and not after the last) and
    /// the unit parsing, and this is what keeps them from drifting apart.
    /// </remarks>
    [Theory]
    [InlineData("12")]
    [InlineData("'12px'")]
    [InlineData("'0.15em'")]
    [InlineData("0")]
    public void TestTheTwoSurfacesMeasureATrackedRunIdentically(string tracking)
    {
        var result = Execute($$"""
            const c = createCanvas(700, 90).getContext('2d');
            c.font = '40px Arial';
            c.letterSpacing = String({{tracking}});
            log(c.measureText('HANDGLOVES').width + ' '
              + VectorLogo.measureTrackedText('HANDGLOVES', {{tracking}}, A).width);
            """);

        var parts = Numbers(result);
        Assert.Equal(parts[0], parts[1], 0.01f);
    }

    /// <summary>What was measured is what gets drawn.</summary>
    [Fact]
    public void TestTheMeasurementMatchesTheDrawnGroup()
    {
        var result = Execute("""
            const p = Snap(700, 90);
            const g = p.trackedText(20, 60, 'HANDGLOVES', 12, A);
            log(VectorLogo.measureTrackedText('HANDGLOVES', 12, A).width + ' ' + g.getBBox().width);
            """);

        var parts = Numbers(result);
        Assert.Equal(parts[0], parts[1], 0.5f);
    }
    #endregion

    #region Anchor Tests
    /// <summary>
    /// <c>text-anchor</c> positions the run, not each glyph.
    /// </summary>
    /// <remarks>
    /// The glyphs are placed individually, so an anchor left on them would re-anchor every one and
    /// scatter the run. It is consumed for the run and forced to <c>start</c> on each glyph.
    /// </remarks>
    [Theory]
    [InlineData("middle", 350f)]
    [InlineData("end", 350f)]
    [InlineData("start", 350f)]
    public void TestTextAnchorPositionsTheWholeRun(string anchor, float x)
    {
        var result = Execute($$"""
            const p = Snap(700, 90);
            const g = p.trackedText(350, 60, 'HANDGLOVES', 12,
                Object.assign({}, A, { 'text-anchor': '{{anchor}}' }));
            const b = g.getBBox();
            log(b.x + ' ' + b.x2);
            """);

        var parts = Numbers(result);
        var (left, right) = (parts[0], parts[1]);

        switch (anchor)
        {
            case "middle":
                Assert.Equal(x, (left + right) / 2f, 1f);
                break;
            case "end":
                Assert.Equal(x, right, 1f);
                break;
            default:
                Assert.Equal(x, left, 1f);
                break;
        }
    }
    #endregion

    #region Glyph Splitting Tests
    /// <summary>
    /// A grapheme cluster is one glyph, so a combining mark is not tracked away from its letter.
    /// </summary>
    /// <remarks>
    /// Splitting by <c>char</c> would place a combining acute a full tracking step to the right of the
    /// e it belongs to, and would cut a surrogate pair in half. Matches the canvas's own splitting.
    /// </remarks>
    [Theory]
    [InlineData("'e\\u0301te'", 3)]
    [InlineData("'abc'", 3)]
    [InlineData("''", 0)]
    public void TestRunsAreSplitByGraphemeNotByChar(string literal, int expected)
    {
        var result = Execute($"log(VectorLogo.measureTrackedText({literal}, 5, A).glyphCount);");

        Assert.Equal(expected, (int)Numbers(result)[0]);
    }

    /// <summary>An empty run draws nothing and returns an empty group rather than throwing.</summary>
    [Fact]
    public void TestAnEmptyRunIsAnEmptyGroup()
    {
        var result = Execute("""
            const p = Snap(700, 90);
            log(p.trackedText(20, 60, '', 12, A).children.length);
            """);

        Assert.Equal(0f, Numbers(result)[0]);
    }
    #endregion

    #region Methods
    /// <summary>Runs <paramref name="body"/> with the shared type style and an ink-width helper.</summary>
    private static DrawingExecutionResult Execute(string body)
    {
        var result = new JsDrawingEngine().Execute($$"""
            const A = { 'font-family': 'Arial', 'font-size': 40, fill: '#000000' };
            function InkWidth(build) {
                const p = Snap(700, 90);
                p.rect(0, 0, 700, 90).attr({ fill: '#ffffff' });
                build(p);
                const bmp = Skia.Image.fromBytes(p.toImageBytes(700, 90, 'png', 100));
                const rows = bmp.rowProfile('#000000', { tolerance: 60 });
                let min = 1e9, max = -1;
                for (let i = 0; i < rows.length; i++) {
                    if (rows[i].start < min) min = rows[i].start;
                    if (rows[i].end > max) max = rows[i].end;
                }
                return max < 0 ? 0 : max - min;
            }
            {{body}}
            exit('done');
            """, 700, 90, null, "png", 100, render: false);

        Assert.True(result.Success, result.Error);
        return result;
    }

    /// <summary>The numbers logged by a body, in order.</summary>
    private static float[] Numbers(DrawingExecutionResult result) =>
        string.Join(" ", result.Logs)
            .Replace("[LOG]", " ", StringComparison.Ordinal)
            .Replace("[EXIT]", " ", StringComparison.Ordinal)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => float.TryParse(t, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var f) ? (float?)f : null)
            .Where(f => f.HasValue)
            .Select(f => f!.Value)
            .ToArray();
    #endregion
}
