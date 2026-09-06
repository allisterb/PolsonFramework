namespace Polson.Tests.Drawing;

using System;
using System.Linq;
using Polson.Drawing.Svg;
using Polson.MCPServer;
using SkiaSharp;
using Xunit;

/// <summary>
/// <c>paper.style(css)</c> — a stylesheet applied to a paper, and kept in the document.
/// </summary>
/// <remarks>
/// <para>
/// <b>The first attempt appended the <c>&lt;style&gt;</c> block and nothing else.</b> It serialised
/// perfectly and rendered <b>black</b>: this library resolves CSS inside its parser rather than in
/// memory, so a block added to an already-built tree styles nothing. A call whose effect appears in
/// the artifact and not in the peek is the worst shape available — the author checks the render,
/// sees the wrong thing, and ships the file that looked right.
/// </para>
/// <para>
/// So the render assertions here are not decoration. Each one pins the half that was missing, and
/// the serialisation assertions beside them pin the half a designer opens in Illustrator; a change
/// that satisfies only one of the pair is the regression.
/// </para>
/// </remarks>
public class SnapStylesheetTests : TestsRuntime
{
    #region Rendering Tests
    [Fact]
    public void TestAClassRuleReachesTheRenderAndNotOnlyTheFile()
    {
        var png = Render("""
            paper.rect(0, 0, 200, 200).attr({ class: 'mark' });
            paper.style('.mark { fill: #1f6f8b; }');
            """);

        var pixel = PixelAt(png, 100, 100);
        Assert.True(Math.Abs(pixel.Red - 0x1f) < 12 && Math.Abs(pixel.Green - 0x6f) < 12
            && Math.Abs(pixel.Blue - 0x8b) < 12, $"the sheet did not reach the render; got {pixel}");

        // The control, so the assertion above cannot pass on a default: the identical tree with no
        // sheet applied is SVG's default black, which is exactly what the first attempt produced.
        var unstyled = PixelAt(Render("paper.rect(0, 0, 200, 200).attr({ class: 'mark' });"), 100, 100);
        Assert.Equal(new SKColor(0, 0, 0), new SKColor(unstyled.Red, unstyled.Green, unstyled.Blue));
    }

    [Fact]
    public void TestStrokePropertiesApplyAsWellAsFill()
    {
        var png = Render("""
            paper.rect(20, 20, 160, 160).attr({ class: 'mark' });
            paper.style('.mark { fill: #ffffff; stroke: #c9553d; stroke-width: 8; }');
            """);

        var onTheStroke = PixelAt(png, 100, 21);
        Assert.True(onTheStroke.Red > 150 && onTheStroke.Green < 130 && onTheStroke.Blue < 110,
            $"expected the declared stroke colour on the edge, got {onTheStroke}");
    }
    #endregion

    #region Serialisation Tests
    [Fact]
    public void TestTheStyleBlockIsKeptInTheDeliverable()
    {
        // The point of keeping it: a designer edits one rule rather than ninety baked attributes.
        var svg = Svg("""
            paper.rect(0, 0, 200, 200).attr({ class: 'mark' });
            paper.style('.mark { fill: #1f6f8b; }');
            """);

        Assert.Contains("<style", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".mark", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void TestDeclarationsAreAlsoWrittenOntoTheMatchedElements()
    {
        // Both halves, from one set of declarations, so the file and the peek cannot disagree.
        var svg = Svg("""
            paper.rect(0, 0, 200, 200).attr({ class: 'mark' });
            paper.style('.mark { fill: #1f6f8b; }');
            """);

        var rect = svg[svg.IndexOf("<rect", StringComparison.OrdinalIgnoreCase)..];
        Assert.Contains("<style", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1f6f8b", rect, StringComparison.OrdinalIgnoreCase);
    }
    #endregion

    #region Selector Tests
    [Fact]
    public void TestIdSelectorMatches()
    {
        var result = Execute("""
            const r = paper.rect(0, 0, 200, 200);
            r.id = 'hero';
            log(String(paper.style('#hero { fill: #c9553d; }')));
            """);

        Assert.True(result.Success, result.Error);
        Assert.Equal("1", Logged(result));
    }

    [Fact]
    public void TestTagSelectorMatchesEveryElementOfThatType()
    {
        var result = Execute("""
            paper.rect(0, 0, 40, 40);
            paper.rect(50, 0, 40, 40);
            paper.circle(150, 20, 15);
            log(String(paper.style('rect { fill: #1f6f8b; }')));
            """);

        Assert.True(result.Success, result.Error);
        Assert.Equal("2", Logged(result));
    }

    [Fact]
    public void TestCommaSeparatedSelectorsMatchEachPart()
    {
        var result = Execute("""
            paper.rect(0, 0, 40, 40).attr({ class: 'a' });
            paper.rect(50, 0, 40, 40).attr({ class: 'b' });
            log(String(paper.style('.a, .b { fill: #1f6f8b; }')));
            """);

        Assert.True(result.Success, result.Error);
        Assert.Equal("2", Logged(result));
    }

    [Fact]
    public void TestRootAndUniversalSelectorsAddressTheDocumentItself()
    {
        // Where a page's ground is set. Neither is a descendant sweep — they name the paper.
        foreach (var selector in new[] { ":root", "*" })
        {
            var result = Execute($"paper.rect(0, 0, 40, 40); log(String(paper.style('{selector} {{ fill: #1f6f8b; }}')));");
            Assert.True(result.Success, result.Error);
            Assert.Equal("1", Logged(result));
        }
    }

    [Fact]
    public void TestAnUnmatchedSelectorStylesNothingRatherThanFailing()
    {
        // The return value is what distinguishes a typo'd selector from an empty sheet, which is the
        // whole reason it is a count and not void.
        var result = Execute("paper.rect(0, 0, 40, 40); log(String(paper.style('.nothingHasThisClass { fill: red; }')));");

        Assert.True(result.Success, result.Error);
        Assert.Equal("0", Logged(result));
    }
    #endregion

    #region Cascade Tests
    [Fact]
    public void TestALaterRuleWins()
    {
        // Source order is the one part of the cascade that survives here; there is no specificity.
        var png = Render("""
            paper.rect(0, 0, 200, 200).attr({ class: 'mark' });
            paper.style('.mark { fill: #1f6f8b; } .mark { fill: #c9553d; }');
            """);

        var pixel = PixelAt(png, 100, 100);
        Assert.True(pixel.Red > 150 && pixel.Blue < 110, $"expected the later rule's colour, got {pixel}");
    }

    [Fact]
    public void TestCustomPropertiesAreResolvedAndNotWrittenOntoElements()
    {
        // A token is a value to substitute, not an attribute to set. CssToolkit has already replaced
        // every var() by this point, so what reaches an element is the colour rather than the name.
        var svg = Svg("""
            paper.rect(0, 0, 200, 200).attr({ class: 'mark' });
            paper.style(':root { --ink: #1f6f8b; } .mark { fill: var(--ink); }');
            """);

        var rect = svg[svg.IndexOf("<rect", StringComparison.OrdinalIgnoreCase)..];
        Assert.Contains("1f6f8b", rect, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--ink=", rect, StringComparison.Ordinal);
        Assert.DoesNotContain("var(--ink)", rect, StringComparison.Ordinal);
    }
    #endregion

    #region Timing Tests
    [Fact]
    public void TestElementsDrawnAfterTheCallAreNotStyled()
    {
        // Documented as "call it last": the rules resolve against the tree as it then stands, which
        // is the only semantics that does not mean re-running the sheet on every later mutation.
        var png = Render("""
            paper.rect(0, 0, 100, 200).attr({ class: 'mark' });
            paper.style('.mark { fill: #1f6f8b; }');
            paper.rect(100, 0, 100, 200).attr({ class: 'mark' });
            """);

        var styled = PixelAt(png, 50, 100);
        var late = PixelAt(png, 150, 100);
        Assert.True(styled.Blue > 120, $"the element present at call time should be styled, got {styled}");
        Assert.True(late.Blue < 60, $"an element drawn afterwards should be untouched, got {late}");
    }

    [Fact]
    public void TestApplyingTheSameSheetTwiceIsHarmless()
    {
        var result = Execute("""
            paper.rect(0, 0, 200, 200).attr({ class: 'mark' });
            const css = '.mark { fill: #1f6f8b; }';
            log(String(paper.style(css)) + ',' + String(paper.style(css)));
            """);

        Assert.True(result.Success, result.Error);
        Assert.Equal("1,1", Logged(result));
    }

    [Fact]
    public void TestAnEmptySheetStylesNothing()
    {
        var result = Execute("paper.rect(0, 0, 40, 40).attr({ class: 'mark' }); log(String(paper.style('   ')));");

        Assert.True(result.Success, result.Error);
        Assert.Equal("0", Logged(result));
    }
    #endregion

    #region Methods
    private static DrawingExecutionResult Execute(string body) =>
        new JsDrawingEngine().Execute($"const paper = Snap(200, 200); {body} paper;", 200, 200, null, "png", 100);

    /// <summary>The logged text without the engine's level prefix, so a count reads as a count.</summary>
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
    #endregion
}
