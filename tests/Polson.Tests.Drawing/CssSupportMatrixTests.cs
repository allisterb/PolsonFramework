namespace Polson.Tests.Drawing;

using System;
using System.Linq;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// Which CSS properties actually reach the render, on each of the two routes that can deliver them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every case renders the same shape with and without the property and compares the two frames.</b>
/// That is the only reliable test: an unsupported property still serialises into the SVG, so reading
/// the markup proves nothing about whether anything was drawn differently.
/// </para>
/// <para>
/// Half of these assert that a property is <b>ignored</b>, which is a strange thing to want until you
/// consider the alternative. These are renderer limits nobody wrote down, so an agent reaching for
/// <c>font-style: italic</c> got silence and no way to tell a typo from an unsupported feature. Pinned
/// here, a renderer upgrade that starts honouring one **fails this suite** and gets documented,
/// instead of quietly changing what every existing sheet means.
/// </para>
/// </remarks>
public class CssSupportMatrixTests : TestsRuntime
{
    #region Attribute Route Tests
    [Theory]
    [InlineData("clip-path", "box", "'clip-path': 'url(#halfClip)'")]
    [InlineData("mask", "box", "mask: 'url(#halfMask)'")]
    [InlineData("filter", "small", "filter: 'url(#softEdge)'")]
    [InlineData("font-weight", "words", "'font-weight': 'bold'")]
    [InlineData("paint-order", "words", "'paint-order': 'stroke fill'")]
    [InlineData("text-decoration", "words", "'text-decoration': 'underline'")]
    [InlineData("marker-end", "line", "'marker-end': 'url(#arrow)'")]
    [InlineData("stroke-dasharray", "box", "'stroke-dasharray': '8 6'")]
    public void TestPropertyReachesTheRenderViaAttr(string property, string shape, string declaration)
    {
        Assert.True(Changes(shape, declaration, null),
            $"'{property}' no longer reaches the render through attr() — a regression, not a limit");
    }

    /// <summary>Renderer limits. A failure here is good news that needs writing down.</summary>
    [Theory]
    [InlineData("fill-rule", "holed", "'fill-rule': 'evenodd'")]
    [InlineData("mix-blend-mode", "box", "'mix-blend-mode': 'multiply'")]
    [InlineData("font-style", "words", "'font-style': 'italic'")]
    [InlineData("letter-spacing", "words", "'letter-spacing': '8px'")]
    [InlineData("dominant-baseline", "words", "'dominant-baseline': 'hanging'")]
    [InlineData("vector-effect", "box", "'vector-effect': 'non-scaling-stroke'")]
    [InlineData("stroke-miterlimit", "vee", "'stroke-miterlimit': 1")]
    public void TestPropertyIsIgnoredByTheRenderer(string property, string shape, string declaration)
    {
        Assert.False(Changes(shape, declaration, null),
            $"'{property}' now renders. Svg.Skia has gained it — document it in Manual 14 and move this "
            + "case to the supported theory above.");
    }

    /// <summary>
    /// <c>letter-spacing</c> is the one whose absence changes a deliverable rather than a detail.
    /// </summary>
    /// <remarks>
    /// <c>ctx.letterSpacing</c> works and is documented, so a tracked wordmark is correct on canvas
    /// and loses its tracking the moment the same design is built as vector — which is the surface a
    /// wordmark is most likely to be delivered on. Called out separately so it is not just one row in
    /// a table of seven.
    /// </remarks>
    [Fact]
    public void TestTrackingIsCanvasOnly()
    {
        Assert.False(Changes("words", "'letter-spacing': '12px'", null),
            "SVG letter-spacing now renders — the vector/canvas tracking gap is closed, so say so in Manual 14");
    }
    #endregion

    #region Stylesheet Route Tests
    [Theory]
    [InlineData("fill", "box", "fill: #c9553d;")]
    [InlineData("opacity", "box", "opacity: 0.3;")]
    [InlineData("stroke-width bare", "box", "stroke: #c9553d; stroke-width: 12;")]
    [InlineData("stroke-dasharray", "box", "stroke: #c9553d; stroke-width: 6; stroke-dasharray: 8 6;")]
    [InlineData("font-family", "words", "font-family: Georgia;")]
    [InlineData("font-weight", "words", "font-weight: bold;")]
    public void TestPropertyReachesTheRenderViaStylesheet(string property, string shape, string css)
    {
        Assert.True(Changes(shape, null, css), $"'{property}' no longer reaches the render through paper.style()");
    }

    /// <summary>
    /// A value carrying a unit reaches the element. It did not, and CSS always writes units.
    /// </summary>
    /// <remarks>
    /// <c>ComputeDelta</c> parsed the value to a <see cref="float"/> before <c>ParseUnit</c> — which
    /// understands <c>px</c>, <c>%</c> and <c>em</c> — ever saw it, and its fallback for an
    /// unparseable value was to return the property <i>unchanged</i>. So
    /// <c>paper.style('.h1 { font-size: 40px }')</c> set nothing while
    /// <c>attr({ 'font-size': 40 })</c> worked: the same declaration succeeding or failing on how it
    /// happened to be spelled. It reached <c>font-size</c>, <c>stroke-width</c> and
    /// <c>stroke-dashoffset</c>.
    /// </remarks>
    [Theory]
    [InlineData("font-size px", "words", "font-size: 40px;")]
    [InlineData("stroke-width px", "box", "stroke: #c9553d; stroke-width: 12px;")]
    public void TestAUnitOnAValueDoesNotSilentlyDropIt(string property, string shape, string css)
    {
        Assert.True(Changes(shape, null, css), $"'{property}' was dropped — the unit suffix is being lost again");
    }

    /// <summary>
    /// A unitless <c>font-size</c> applies, even though strict CSS rejects it.
    /// </summary>
    /// <remarks>
    /// A consequence of reading declarations from the source text, and the right one for this target:
    /// what a sheet produces here is an <b>SVG presentation attribute</b>, and <c>font-size="40"</c>
    /// is perfectly valid SVG. Being stricter than the thing we write to would refuse a declaration
    /// that works. Pinned so the leniency is a decision on the record rather than a side effect
    /// nobody noticed.
    /// </remarks>
    [Fact]
    public void TestAUnitlessFontSizeAppliesBecauseTheTargetIsSvgNotCss()
    {
        Assert.True(Changes("words", null, "font-size: 40;"),
            "a unitless font-size no longer applies — valid SVG is being refused for being invalid CSS");
    }

    /// <summary>
    /// <c>em</c> on <c>font-size</c> parses but does not render — a renderer limit, not a lost unit.
    /// </summary>
    /// <remarks>
    /// Distinguished from the unit bug above because they look identical from outside: both are a
    /// sized declaration that draws nothing. This one survives <c>ParseUnit</c> as
    /// <c>SvgUnitType.Em</c> and is dropped further down, in Svg.Skia. <c>px</c> is the spelling to
    /// use in a sheet.
    /// </remarks>
    [Fact]
    public void TestEmFontSizesDoNotRender()
    {
        Assert.False(Changes("words", null, "font-size: 2.5em;"),
            "em font sizes now render — say so in Manual 14, and stop steering sheets to px");
    }

    /// <summary>
    /// A <c>url(...)</c> reference survives the stylesheet. AngleSharp drops every one of them.
    /// </summary>
    /// <remarks>
    /// Measured before the fix: a rule declaring <c>filter</c>, <c>clip-path</c>, <c>mask</c>,
    /// <c>marker-end</c> and <c>fill</c> came back from AngleSharp holding **one** property. Nothing
    /// reported it. The declarations are now recovered from the source text, which is the same remedy
    /// the class already uses for custom properties and for the same reason.
    /// </remarks>
    [Theory]
    [InlineData("filter", "small", "filter: url(#softEdge);")]
    [InlineData("clip-path", "box", "clip-path: url(#halfClip);")]
    [InlineData("mask", "box", "mask: url(#halfMask);")]
    public void TestAUrlReferenceSurvivesTheStylesheet(string property, string shape, string css)
    {
        Assert.True(Changes(shape, null, css),
            $"'{property}' was dropped by the CSS parser — the source-text salvage has regressed");
    }

    [Fact]
    public void TestSalvagingAUrlDoesNotDisturbTheOtherDeclarations()
    {
        // The parsed values are normalised and must still win where AngleSharp kept one; only the
        // declarations it refused should come from the raw text.
        var result = Execute("""
            const sheet = Css.fromCss('.p { filter: url(#x); fill: red; font-family: Georgia; }');
            const rule = sheet.rule('.p');
            log(rule.fontFamily + '|' + (rule.properties['fill'] || '') + '|' + (rule.properties['filter'] || ''));
            """);

        Assert.True(result.Success, result.Error);
        var line = string.Join(" ", result.Logs);
        Assert.Contains("Georgia", line, StringComparison.Ordinal);
        Assert.Contains("url(#x)", line, StringComparison.Ordinal);
        Assert.Contains("rgba(255, 0, 0, 1)", line, StringComparison.Ordinal);
    }
    #endregion

    #region Methods
    /// <summary>
    /// Whether applying <paramref name="declaration"/> or <paramref name="css"/> changes the frame.
    /// </summary>
    private static bool Changes(string shape, string? declaration, string? css)
    {
        var result = Execute($$"""
            function shot(apply) {
                const p = Snap(130, 130);
                p.rect(0, 0, 130, 130).attr({ fill: '#ffffff' });
                p.defs.el('clipPath', { id: 'halfClip' }).rect(0, 0, 60, 130);
                p.mask(p.rect(0, 0, 60, 130).attr({ fill: '#ffffff' })).attr({ id: 'halfMask' });
                p.filter('softEdge').gaussianBlur(4);
                const mk = p.defs.el('marker', { id: 'arrow', markerWidth: 8, markerHeight: 8,
                                                 refX: 4, refY: 4, orient: 'auto' });
                mk.path('M0,0 L8,4 L0,8 Z').attr({ fill: '#c9553d' });

                const shapes = {
                    box:   () => p.rect(10, 10, 100, 100),
                    small: () => p.rect(20, 20, 80, 80),
                    words: () => p.text(10, 70, 'Handgloves'),
                    line:  () => p.path('M10,60 L100,60'),
                    vee:   () => p.path('M10,100 L60,20 L110,100'),
                    holed: () => p.path('M10,10 L110,10 L110,110 L10,110 Z M40,40 L80,40 L80,80 L40,80 Z')
                };
                const el = shapes['{{shape}}']();
                el.attr({ fill: '#1f6f8b', 'font-size': 30, stroke: '#c9553d', 'stroke-width': 4 });
                if (apply) apply(p, el);
                return Skia.Image.fromBytes(p.toImageBytes(130, 130, 'png', 100));
            }

            const before = shot(null);
            const after = shot((p, el) => { {{Apply(declaration, css)}} });
            log(String(!before.diff(after, { tolerance: 2 }).identical));
            exit('done');
            """);

        Assert.True(result.Success, result.Error);
        return string.Join(" ", result.Logs).Contains("true", StringComparison.Ordinal);
    }

    private static string Apply(string? declaration, string? css) => declaration is not null
        ? $"el.attr({{ {declaration} }});"
        : $"el.attr({{ class: 'probe' }}); p.style('.probe {{ {css} }}');";

    private static DrawingExecutionResult Execute(string script) =>
        new JsDrawingEngine().Execute(script, 130, 130, null, "png", 100, render: false);
    #endregion
}
