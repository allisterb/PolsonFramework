namespace Polson.Tests.Drawing;

using System;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// <c>paper.marker(style, options)</c> — line endings that land on the line.
/// </summary>
/// <remarks>
/// <c>marker-end</c> always rendered — <see cref="CssSupportMatrixTests"/> pins that — but reaching
/// it meant hand-building an <c>SvgMarker</c> with <c>refX</c>, <c>refY</c>, <c>orient</c> and a child
/// path in the marker's own coordinate space. Four things to get right for an arrowhead, and both
/// Apollo plates hand-rolled every one. These tests assert the rendered pixels rather than the
/// markup, because a marker whose <c>refX</c> is wrong serialises perfectly and draws beside the line.
/// </remarks>
public class SnapMarkerTests : TestsRuntime
{
    #region Rendering Tests
    /// <summary>Every style draws, and draws at the end of the line rather than beside it.</summary>
    /// <remarks>
    /// <para>
    /// The probe counts ink in a band <b>centred on the line's end</b> and off its axis, above and
    /// below the 2px rule. Without a marker that band holds only the rule's own two rows; with one it
    /// holds the head as well.
    /// </para>
    /// <para>
    /// <b>Centred, not past the end, because <c>refX</c> is the tip.</b> An arrowhead's reference
    /// point is the vertex that lands on the marked point, so the head extends <i>backwards</i> from
    /// the line's end — sampling past it found nothing for <c>arrow</c>, <c>barb</c> and <c>bar</c>
    /// while <c>dot</c> and <c>open</c> passed, which is the probe being wrong rather than three of
    /// five styles being broken.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("arrow")]
    [InlineData("barb")]
    [InlineData("open")]
    [InlineData("dot")]
    [InlineData("bar")]
    public void TestEveryStyleDrawsAtTheLineEnd(string style)
    {
        var result = Execute($$"""
            function ink(withMarker) {
                const p = Snap(300, 100);
                p.rect(0, 0, 300, 100).attr({ fill: '#ffffff' });
                const line = p.line(40, 50, 200, 50).attr({ stroke: '#000000', 'stroke-width': 2 });
                if (withMarker) {
                    line.attr({ 'marker-end': p.marker('{{style}}', { color: '#000000', size: 8 }).url });
                }
                const bmp = Skia.Image.fromBytes(p.toImageBytes(300, 100, 'png', 100));
                // Centred on the line's end and off its axis: the rule itself is only 2px tall, so
                // rows away from y=50 are empty until a marker puts something there.
                let n = 0;
                for (let x = 186; x < 216; x++) for (let y = 36; y < 64; y++) {
                    if (y >= 48 && y <= 52) continue;
                    if (bmp.getPixel(x, y) !== '#FFFFFFFF') n++;
                }
                return n;
            }
            log(ink(false) + ',' + ink(true));
            """);

        var parts = Logged(result).Split(',');
        Assert.Equal("0", parts[0]);
        Assert.True(int.Parse(parts[1]) > 0, $"'{style}' drew nothing past the line end");
    }
    #endregion

    #region Definition Tests
    /// <summary>The marker lands in <c>&lt;defs&gt;</c> and its url references it.</summary>
    [Fact]
    public void TestTheMarkerIsDefinedOnceAndReferencedByUrl()
    {
        var result = Execute("""
            const p = Snap(200, 100);
            const m = p.marker('arrow');
            const inDefs = p.toString().indexOf('<defs') < p.toString().indexOf('<marker');
            log(m.url.indexOf('url(#marker_') === 0 ? String(inDefs) : 'bad url: ' + m.url);
            """);

        Assert.Equal("true", Logged(result));
    }

    /// <summary>A given id is kept, so a marker can be shared by name.</summary>
    [Fact]
    public void TestAGivenIdIsKept()
    {
        var result = Execute("""
            const p = Snap(200, 100);
            log(p.marker('arrow', { id: 'head' }).url);
            """);

        Assert.Equal("url(#head)", Logged(result));
    }

    /// <summary>
    /// <c>orient</c> is <c>auto</c> by default, which is what makes a head follow its line.
    /// </summary>
    /// <remarks>
    /// Without it every arrow on a fan of dimension lines points the same way — a drawing that looks
    /// wrong rather than a document that reports an error.
    /// </remarks>
    [Fact]
    public void TestMarkersOrientToTheirLineByDefault()
    {
        var result = Execute("""
            const p = Snap(200, 100);
            p.marker('arrow');
            log(String(p.toString().indexOf('orient="auto"') > 0));
            """);

        Assert.Equal("true", Logged(result));
    }

    /// <summary>An unknown style is refused by name, listing the ones that exist.</summary>
    [Fact]
    public void TestAnUnknownStyleIsRefusedByName()
    {
        var result = new JsDrawingEngine().Execute(
            "const p = Snap(200, 100); p.marker('squiggle');", 200, 100, null, "png", 100, render: false);

        Assert.False(result.Success);
        Assert.Contains("not a marker style", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("arrow", result.Error, StringComparison.Ordinal);
    }
    #endregion

    #region Methods
    private static DrawingExecutionResult Execute(string body)
    {
        var result = new JsDrawingEngine().Execute(body, 300, 100, null, "png", 100, render: false);
        Assert.True(result.Success, result.Error);
        return result;
    }

    private static string Logged(DrawingExecutionResult result) =>
        string.Join(" ", result.Logs).Replace("[LOG]", string.Empty, StringComparison.Ordinal).Trim();
    #endregion
}
