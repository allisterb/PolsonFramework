namespace Polson.Tests.MCPServer;

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// <c>Css.*</c> — reading a stylesheet as a design language.
/// </summary>
/// <remarks>
/// The behaviours pinned here are the ones AngleSharp's own computed style gets wrong, which is why
/// this layer reads declared rules instead: a <c>font:</c> shorthand carrying <c>/line-height</c>
/// (its computed path throws), a shorthand carrying <c>var()</c> (silently dropped), and <c>em</c>
/// tracking (resolved against 16px rather than the element's size). If a future AngleSharp changes
/// any of that, these tests are what will notice.
/// </remarks>
public class CssToolkitTests : TestsRuntime
{
    #region Methods
    private static object? Eval(string body)
    {
        var result = new JsDrawingEngine().Execute(body, 200, 200, null, "png", 100);
        Assert.True(result.Success, result.Error);
        return result.ReturnValue;
    }

    private static string Text(string body) => Eval(body)?.ToString() ?? string.Empty;

    private const string Sheet = """
        const sheet = Css.fromCss(`
          :root {
            --ink: #1c2733;
            --accent: #f0b429;
            --font-body: 'Inter Tight', Helvetica, sans-serif;
            --lead: var(--ink);
          }
          .kicker { font: 600 13px/1.2 var(--font-body); letter-spacing: .18em;
                    text-transform: uppercase; color: var(--accent); }
          .lede   { font: italic 400 17px/1.5 Georgia, serif; color: var(--lead); }
          .plain  { color: var(--missing, #ff0000); }
        `);
        """;
    #endregion

    #region Tokens
    /// <summary>Custom properties come back as a design language's token set.</summary>
    [Fact]
    public void TestTokensAreExtracted()
    {
        Assert.Equal("#1c2733|#f0b429", Text($$"""
            {{Sheet}}
            const t = sheet.tokens();
            t['--ink'] + '|' + t['--accent'];
            """));
    }

    /// <summary>A token defined in terms of another resolves to the value, not the reference.</summary>
    [Fact]
    public void TestTokensResolveAgainstEachOther()
    {
        Assert.Equal("#1c2733", Text($$"""
            {{Sheet}}
            sheet.tokens()['--lead'];
            """));
    }

    /// <summary>A `var()` fallback is honoured when the token is undefined.</summary>
    [Fact]
    public void TestVarFallbackIsUsedForAnUnknownToken()
    {
        Assert.Contains("255, 0, 0", Text($$"""
            {{Sheet}}
            sheet.rule('.plain').color;
            """));
    }

    /// <summary>A cyclic token definition terminates rather than hanging.</summary>
    /// <remarks>
    /// Substitution is bounded, not recursive. A stylesheet can define <c>--a: var(--b)</c> and
    /// <c>--b: var(--a)</c>, and the pass limit turns that into a visible leftover rather than a
    /// script that never returns — which in a sandbox with a statement cap would surface as an
    /// unrelated-looking timeout.
    /// </remarks>
    [Fact]
    public void TestCyclicTokensTerminate()
    {
        Assert.Equal("ok", Text("""
            const s = Css.fromCss(':root { --a: var(--b); --b: var(--a); } .x { color: var(--a); }');
            s.rule('.x') ? 'ok' : 'ok';
            """));
    }
    #endregion

    #region Rules
    /// <summary>
    /// The `font:` shorthand expands, slash form and all.
    /// </summary>
    /// <remarks>
    /// This is the case AngleSharp's computed style throws on, and the dominant spelling in real
    /// stylesheets — 86 occurrences across the thirteen reference infographics.
    /// </remarks>
    [Fact]
    public void TestFontShorthandWithLineHeightExpands()
    {
        // Sizes are 32-bit floats, so they widen in JS — 15.600000381469727. Format before comparing,
        // exactly as Manual 11 tells an agent to format before drawing.
        Assert.Equal("600|13|1.2 -> 15.60", Text($$"""
            {{Sheet}}
            const k = sheet.rule('.kicker');
            k.fontWeight + '|' + k.fontSize + '|1.2 -> ' + k.lineHeight.toFixed(2);
            """));
    }

    /// <summary>A shorthand carrying `var()` survives, because substitution happens before parsing.</summary>
    [Fact]
    public void TestFontShorthandWithVarSurvives()
    {
        Assert.Contains("Inter Tight", Text($$"""
            {{Sheet}}
            sheet.rule('.kicker').fontFamily;
            """));
    }

    /// <summary>The assembled `font` string is what `ctx.font` parses, weight and all.</summary>
    [Fact]
    public void TestAssembledFontStringDrivesTheContext()
    {
        // Round-trip: the style's font string, applied to a context, measures as its own size.
        Assert.Equal("applied", Text($$"""
            {{Sheet}}
            const c = createCanvas(10, 10);
            const ctx = c.getContext('2d');
            ctx.font = sheet.rule('.lede').font;
            const big = ctx.measureText('Handgloves').width;
            ctx.font = '17px Georgia';
            const same = ctx.measureText('Handgloves').width;
            (big > 0 && Math.abs(big - same) < same * 0.35) ? 'applied' : 'font string was not honoured';
            """));
    }

    /// <summary>Italic survives into the font string.</summary>
    [Fact]
    public void TestFontStyleIsCarried()
    {
        Assert.StartsWith("italic", Text($$"""
            {{Sheet}}
            sheet.rule('.lede').font;
            """));
    }

    /// <summary>
    /// Tracking keeps the unit it was written in.
    /// </summary>
    /// <remarks>
    /// The point of not computing: <c>.18em</c> handed to <c>ctx.letterSpacing</c> resolves against
    /// the size actually in force, which is what the stylesheet meant. AngleSharp's computed style
    /// returns 2.88px here — 0.18 × 16 — regardless of the rule's own 13px.
    /// </remarks>
    [Fact]
    public void TestTrackingKeepsItsUnit()
    {
        Assert.Equal("0.18em", Text($$"""
            {{Sheet}}
            sheet.rule('.kicker').letterSpacing;
            """));
    }

    /// <summary>A unitless line-height is resolved against the rule's own font size.</summary>
    [Fact]
    public void TestUnitlessLineHeightResolvesToPixels()
    {
        Assert.Equal("25.5", Text($$"""
            {{Sheet}}
            sheet.rule('.lede').lineHeight.toString();
            """));
    }

    /// <summary>
    /// A missing rule is absent, not an empty style that would silently draw as something.
    /// </summary>
    /// <remarks>
    /// It arrives as <c>null</c> rather than <c>undefined</c> — a CLR null crossing into Jint — so
    /// the documentation says <c>null</c>. Either way <c>if (!style)</c> is the test to write.
    /// </remarks>
    [Fact]
    public void TestAnUnknownSelectorReturnsNothing()
    {
        Assert.Equal("null|falsy", Text($$"""
            {{Sheet}}
            const missing = sheet.rule('.nope');
            String(missing) + '|' + (missing ? 'truthy' : 'falsy');
            """));
    }

    /// <summary>Selectors are listed in source order.</summary>
    [Fact]
    public void TestSelectorsAreListed()
    {
        Assert.Equal(":root,.kicker,.lede,.plain", Text($$"""
            {{Sheet}}
            sheet.selectors().join(',');
            """));
    }

    /// <summary>Every declared property remains reachable, beyond the named fields.</summary>
    [Fact]
    public void TestDeclaredPropertiesRemainReachable()
    {
        Assert.Equal("uppercase", Text($$"""
            {{Sheet}}
            sheet.rule('.kicker').properties['text-transform'];
            """));
    }
    #endregion

    #region Real stylesheets
    /// <summary>
    /// Every reference infographic parses without throwing.
    /// </summary>
    /// <remarks>
    /// Thirteen real, third-party stylesheets — the reason this layer exists. Skipped rather than
    /// failed when the reference material is absent, since `reference/` is gitignored and a clone
    /// will not have it.
    /// </remarks>
    [Fact]
    public void TestEveryReferenceStylesheetParses()
    {
        var root = Path.Combine(RepoRoot(), "reference", "projects", "EpicInfographics-main", "examples");
        if (!Directory.Exists(root)) return;

        var files = Directory.GetFiles(root, "infographic.html", SearchOption.AllDirectories);
        Assert.NotEmpty(files);

        foreach (var file in files)
        {
            var js = File.ReadAllText(file).Replace("\\", "\\\\").Replace("`", "\\`").Replace("$", "\\$");
            var summary = Text($$"""
                const sheet = Css.parse(`{{js}}`);
                sheet.tokens() && sheet.selectors().length > 0 ? 'parsed' : 'empty';
                """);

            Assert.Equal("parsed", summary);
        }
    }

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Polson.sln")))
        {
            dir = Path.GetDirectoryName(dir);
        }
        return dir ?? AppContext.BaseDirectory;
    }
    #endregion
}
