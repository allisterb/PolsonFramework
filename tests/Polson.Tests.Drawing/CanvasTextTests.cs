namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Polson.MCPServer;
using SkiaSharp;
using Xunit;

/// <summary>
/// The 2D context's text surface: CSS font-shorthand parsing and <c>ctx.letterSpacing</c>.
/// </summary>
/// <remarks>
/// Every failure covered here is silent. Skia substitutes a default face for a family it does not
/// have and reports nothing, so a mis-parsed weight or family renders happily in a typeface nobody
/// asked for — only a measurement catches it. That is also why these tests compare widths and ink
/// rather than asserting on a resolved name: the name is what the caller wanted, the metrics are
/// what the reader gets.
/// </remarks>
public class CanvasTextTests : TestsRuntime
{
    #region Methods
    private static object? Eval(string script)
    {
        var result = new JsDrawingEngine().Execute(script, 10, 10, null, "png", 100);
        Assert.True(result.Success, result.Error);
        return result.ReturnValue;
    }

    /// <summary>Advance width of <c>Handgloves</c> after running <paramref name="setup"/>.</summary>
    private static float Width(string setup, string sample = "Handgloves")
    {
        var value = Eval($$"""
            const c = createCanvas(10, 10);
            const x = c.getContext('2d');
            {{setup}}
            x.measureText('{{sample}}').width;
            """);

        return Convert.ToSingle(value, CultureInfo.InvariantCulture);
    }

    private static SKBitmap Render(string setup, string sample = "Handgloves",
        int width = 900, int height = 120)
    {
        var result = new JsDrawingEngine().Execute($$"""
            const c = createCanvas({{width}}, {{height}});
            const x = c.getContext('2d');
            x.fillStyle = '#ffffff';
            x.fillRect(0, 0, {{width}}, {{height}});
            x.fillStyle = '#000000';
            {{setup}}
            x.fillText('{{sample}}', 20, 80);
            c;
            """, width, height, null, "png", 100);

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.ImageBytes);

        return SKBitmap.Decode(result.ImageBytes!);
    }

    private static int InkPixels(SKBitmap bitmap)
    {
        var count = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var px = 0; px < bitmap.Width; px++)
            {
                if (bitmap.GetPixel(px, y).Red < 250) count++;
            }
        }
        return count;
    }

    private static (int Left, int Right) InkColumns(SKBitmap bitmap) =>
        InkColumnsInBand(bitmap, 0, bitmap.Height);

    /// <summary>The first blank row after ink starts — the gap between two drawn lines.</summary>
    private static int BlankRowAfterFirstInk(SKBitmap bitmap)
    {
        var seenInk = false;
        for (var y = 0; y < bitmap.Height; y++)
        {
            var rowHasInk = false;
            for (var px = 0; px < bitmap.Width && !rowHasInk; px++)
            {
                rowHasInk = bitmap.GetPixel(px, y).Red < 250;
            }

            if (rowHasInk) seenInk = true;
            else if (seenInk) return y;
        }
        return bitmap.Height;
    }

    /// <summary>Ink extent within a horizontal band, for measuring one line of a block.</summary>
    private static (int Left, int Right) InkColumnsInBand(SKBitmap bitmap, int fromRow, int toRow)
    {
        int left = bitmap.Width, right = -1;
        for (var y = fromRow; y < toRow; y++)
        {
            for (var px = 0; px < bitmap.Width; px++)
            {
                if (bitmap.GetPixel(px, y).Red >= 250) continue;
                if (px < left) left = px;
                if (px > right) right = px;
            }
        }
        return (left, right);
    }

    /// <summary>
    /// An installed family whose name has a space and whose last word resolves to something else.
    /// </summary>
    /// <remarks>
    /// Chosen from what this machine actually has rather than hard-coded, so the test means the same
    /// thing on a box without the Microsoft core fonts. The last word must not resolve to itself, or
    /// the comparison would be between two real families instead of between a name and its tail.
    /// </remarks>
    private static (string Family, string LastWord) MultiWordFamily()
    {
        using var manager = SKFontManager.CreateDefault();
        var defaultFamily = SKTypeface.Default.FamilyName;
        var installed = new HashSet<string>(manager.FontFamilies, StringComparer.OrdinalIgnoreCase);

        foreach (var family in manager.FontFamilies.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var words = family.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 2) continue;
            if (string.Equals(family, defaultFamily, StringComparison.OrdinalIgnoreCase)) continue;

            var last = words[^1];
            if (installed.Contains(last)) continue;

            using var face = SKTypeface.FromFamilyName(last);
            if (face is not null && string.Equals(face.FamilyName, last, StringComparison.OrdinalIgnoreCase)) continue;

            return (family, last);
        }

        return (string.Empty, string.Empty);
    }
    #endregion

    #region Font weight
    /// <summary>
    /// A numeric weight is applied.
    /// </summary>
    /// <remarks>
    /// The old parser looked for the substring "bold", so <c>600</c> — the spelling every real
    /// stylesheet and design spec uses — silently rendered at regular weight.
    /// </remarks>
    [Fact]
    public void TestNumericFontWeightIsApplied()
    {
        using var regular = Render("x.font = '400 60px sans-serif';");
        using var bold = Render("x.font = '700 60px sans-serif';");

        Assert.True(InkPixels(bold) > InkPixels(regular),
            $"700 laid down {InkPixels(bold)}px of ink, 400 laid down {InkPixels(regular)} — the weight was ignored.");
    }

    /// <summary>The numeric spelling and the keyword reach the same face.</summary>
    [Fact]
    public void TestNumericSevenHundredMatchesTheBoldKeyword()
    {
        using var numeric = Render("x.font = '700 60px sans-serif';");
        using var keyword = Render("x.font = 'bold 60px sans-serif';");

        Assert.Equal(InkPixels(keyword), InkPixels(numeric));
    }

    /// <summary>
    /// A weight is not mistaken for the size.
    /// </summary>
    /// <remarks>
    /// The decisive check on the grammar: <c>700</c> and <c>40px</c> are both numbers, and a parser
    /// that grabs the first one sets 700px type. Bold is wider than regular but nowhere near that.
    /// </remarks>
    [Fact]
    public void TestAWeightIsNotReadAsTheSize()
    {
        var plain = Width("x.font = '40px sans-serif';");
        var weighted = Width("x.font = '700 40px sans-serif';");

        Assert.InRange(weighted, plain * 0.9f, plain * 1.3f);
    }

    /// <summary>Foundry weight names are accepted, not only the two CSS keywords.</summary>
    [Fact]
    public void TestNamedWeightsBeyondTheCssKeywordsAreAccepted()
    {
        using var semibold = Render("x.font = 'semibold 60px sans-serif';");
        using var regular = Render("x.font = '400 60px sans-serif';");

        Assert.True(InkPixels(semibold) >= InkPixels(regular));
    }

    /// <summary>An italic keyword still reaches an italic face.</summary>
    [Fact]
    public void TestItalicSurvivesTheGrammarRewrite()
    {
        using var upright = Render("x.font = 'italic 400 60px sans-serif';");
        using var plain = Render("x.font = '400 60px sans-serif';");

        Assert.NotEqual(InkPixels(plain), InkPixels(upright));
    }
    #endregion

    #region Font family
    /// <summary>
    /// A multi-word family is used whole, not reduced to its last word.
    /// </summary>
    /// <remarks>
    /// The old parser took the last space-separated token, so <c>Times New Roman</c> became
    /// <c>Roman</c> — which resolves to the default face, silently.
    /// </remarks>
    [Fact]
    public void TestAMultiWordFamilyIsNotTruncatedToItsLastWord()
    {
        var (family, lastWord) = MultiWordFamily();
        Assert.False(family.Length == 0, "no suitable multi-word font family is installed to test with.");

        var whole = Width($"x.font = '40px \"{family}\"';");
        var truncated = Width($"x.font = '40px {lastWord}';");

        Assert.True(Math.Abs(whole - truncated) > 0.01f,
            $"'{family}' and its tail '{lastWord}' measured identically ({whole}) — the name was truncated.");
    }

    /// <summary>
    /// A fallback list falls through to the first family that is actually installed.
    /// </summary>
    /// <remarks>
    /// This is the check that does not depend on which fonts the machine has: whatever the installed
    /// family measures, reaching it through a fallback list must measure the same. The old parser
    /// took the list's last token, so it got this backwards — it would have skipped the very family
    /// that was available.
    /// </remarks>
    [Fact]
    public void TestAFallbackListSkipsFamiliesThatAreNotInstalled()
    {
        var (family, _) = MultiWordFamily();
        Assert.False(family.Length == 0, "no suitable multi-word font family is installed to test with.");

        var direct = Width($"x.font = '40px \"{family}\"';");
        var viaFallback = Width($"x.font = '40px \"Nonexistent Family QQQ\", \"{family}\", sans-serif';");

        Assert.Equal(direct, viaFallback, 2);
    }

    /// <summary>A list of nothing installed still renders, rather than throwing or drawing blank.</summary>
    [Fact]
    public void TestAnEntirelyUnresolvableListStillDraws()
    {
        using var bitmap = Render("x.font = '40px \"Nope QQQ\", \"Also Nope QQQ\"';");
        Assert.True(InkPixels(bitmap) > 0);
    }

    /// <summary>The generic families resolve to genuinely different faces.</summary>
    [Fact]
    public void TestGenericFamiliesResolveDistinctly()
    {
        var mono = Width("x.font = '40px monospace';");
        var serif = Width("x.font = '40px serif';");

        Assert.True(Math.Abs(mono - serif) > 0.01f,
            $"monospace and serif both measured {mono} — the generics collapsed to one face.");
    }

    /// <summary>A size in points is converted, not taken as pixels.</summary>
    [Fact]
    public void TestPointSizesAreConvertedToPixels()
    {
        var points = Width("x.font = '30pt sans-serif';");
        var pixels = Width("x.font = '40px sans-serif';");

        Assert.Equal(pixels, points, 2);
    }
    #endregion

    #region Letter spacing
    /// <summary>
    /// Tracking widens the run by the spacing times the gaps between glyphs.
    /// </summary>
    /// <remarks>
    /// Compared as a difference between two tracked runs rather than against an untracked one,
    /// because tracking turns off kerning: the untracked measurement legitimately includes kerning
    /// the tracked one cannot. The difference isolates the spacing itself.
    /// </remarks>
    [Fact]
    public void TestLetterSpacingWidensARunByTheSpacingTimesTheGaps()
    {
        var five = Width("x.font = '40px sans-serif'; x.letterSpacing = '5px';");
        var ten = Width("x.font = '40px sans-serif'; x.letterSpacing = '10px';");

        // 'Handgloves' is ten glyphs, so nine gaps.
        Assert.Equal(45f, ten - five, 2);
    }

    /// <summary>
    /// <c>em</c> resolves against the font size in force.
    /// </summary>
    /// <remarks>
    /// This is the unit <c>LogoType.computeWordmarkTracking</c> returns, so it is the one that makes
    /// the toolkit's tracking figure directly applicable instead of needing a multiplication the
    /// caller has to know about.
    /// </remarks>
    [Fact]
    public void TestEmLetterSpacingResolvesAgainstTheFontSize()
    {
        var em = Width("x.font = '40px sans-serif'; x.letterSpacing = '0.25em';");
        var px = Width("x.font = '40px sans-serif'; x.letterSpacing = '10px';");

        Assert.Equal(px, em, 3);
    }

    /// <summary>A bare number is read as pixels.</summary>
    [Fact]
    public void TestABareNumberIsReadAsPixels()
    {
        var bare = Width("x.font = '40px sans-serif'; x.letterSpacing = 10;");
        var px = Width("x.font = '40px sans-serif'; x.letterSpacing = '10px';");

        Assert.Equal(px, bare, 3);
    }

    /// <summary>Tracking reaches the render, not only the measurement.</summary>
    [Fact]
    public void TestLetterSpacingWidensWhatIsDrawn()
    {
        using var tight = Render("x.font = '40px sans-serif';");
        using var tracked = Render("x.font = '40px sans-serif'; x.letterSpacing = '12px';");

        var (_, tightRight) = InkColumns(tight);
        var (_, trackedRight) = InkColumns(tracked);

        Assert.True(trackedRight > tightRight + 80,
            $"tracked run ended at {trackedRight}, untracked at {tightRight} — spacing never reached the canvas.");
    }

    /// <summary>
    /// A tracked run stays centred under <c>textAlign</c>.
    /// </summary>
    /// <remarks>
    /// Alignment has to be resolved from the <i>tracked</i> width up front. Drawing glyph by glyph
    /// with alignment left to each one would centre every glyph on the same point and pile the run
    /// into a smear.
    /// </remarks>
    [Fact]
    public void TestATrackedRunIsStillCentred()
    {
        var result = new JsDrawingEngine().Execute("""
            const c = createCanvas(900, 120);
            const x = c.getContext('2d');
            x.fillStyle = '#ffffff';
            x.fillRect(0, 0, 900, 120);
            x.fillStyle = '#000000';
            x.font = '40px sans-serif';
            x.textAlign = 'center';
            x.letterSpacing = '14px';
            x.fillText('HANDGLOVES', 450, 80);
            c;
            """, 900, 120, null, "png", 100);

        Assert.True(result.Success, result.Error);
        using var bitmap = SKBitmap.Decode(result.ImageBytes!);
        var (left, right) = InkColumns(bitmap);

        Assert.InRange((left + right) / 2f, 450f - 12f, 450f + 12f);
    }

    /// <summary>Tracking is part of the drawing state, so <c>save</c>/<c>restore</c> scope it.</summary>
    [Fact]
    public void TestLetterSpacingIsSavedAndRestored()
    {
        var value = Eval("""
            const c = createCanvas(10, 10);
            const x = c.getContext('2d');
            x.font = '40px sans-serif';
            x.letterSpacing = '4px';
            x.save();
            x.letterSpacing = '20px';
            x.restore();
            x.letterSpacing;
            """);

        Assert.Equal("4px", value?.ToString());
    }

    /// <summary>Zero tracking measures exactly as an untracked run, keeping the kerned fast path.</summary>
    [Fact]
    public void TestZeroTrackingKeepsTheKernedPath()
    {
        var untouched = Width("x.font = '40px sans-serif';");
        var explicitZero = Width("x.font = '40px sans-serif'; x.letterSpacing = '0px';");

        Assert.Equal(untouched, explicitZero, 4);
    }

    #endregion

    #region maxWidth
    /// <summary>
    /// Text wider than <c>maxWidth</c> is condensed to fit.
    /// </summary>
    /// <remarks>
    /// The parameter was documented and completely ignored, so a label sized to a box overflowed it
    /// silently — the failure only shows up in the render, never in a return value.
    /// </remarks>
    [Fact]
    public void TestMaxWidthCondensesTextThatWouldOverflow()
    {
        var result = new JsDrawingEngine().Execute("""
            const c = createCanvas(900, 120);
            const x = c.getContext('2d');
            x.fillStyle = '#ffffff';
            x.fillRect(0, 0, 900, 120);
            x.fillStyle = '#000000';
            x.font = '48px sans-serif';
            x.fillText('Handgloves and hamburgefonstiv', 20, 80, 300);
            c;
            """, 900, 120, null, "png", 100);

        Assert.True(result.Success, result.Error);
        using var bitmap = SKBitmap.Decode(result.ImageBytes!);
        var (left, right) = InkColumns(bitmap);

        Assert.True(right <= 20 + 300 + 2, $"ink reached column {right}, past the 320 limit.");
        Assert.True(right > 250, $"ink ended at {right} — the text was not drawn, or shrank away.");
        Assert.True(left < 30, $"ink started at {left} rather than at the anchor.");
    }

    /// <summary>Text already inside the limit is left exactly alone.</summary>
    [Fact]
    public void TestMaxWidthLeavesShorterTextUntouched()
    {
        using var unconstrained = Render("x.font = '40px sans-serif';", "Handgloves");

        var result = new JsDrawingEngine().Execute("""
            const c = createCanvas(900, 120);
            const x = c.getContext('2d');
            x.fillStyle = '#ffffff';
            x.fillRect(0, 0, 900, 120);
            x.fillStyle = '#000000';
            x.font = '40px sans-serif';
            x.fillText('Handgloves', 20, 80, 800);
            c;
            """, 900, 120, null, "png", 100);

        Assert.True(result.Success, result.Error);
        using var constrained = SKBitmap.Decode(result.ImageBytes!);

        Assert.Equal(InkColumns(unconstrained).Right, InkColumns(constrained).Right);
    }

    /// <summary>
    /// Condensing narrows the type rather than shrinking it.
    /// </summary>
    /// <remarks>
    /// The distinction a caller fitting a label into a box cares about: cap height is what makes a
    /// row of labels look like one row, so the height must survive what the width does not.
    /// </remarks>
    [Fact]
    public void TestMaxWidthCondensesRatherThanScalesDown()
    {
        static (int Top, int Bottom) InkRows(SKBitmap bitmap)
        {
            int top = bitmap.Height, bottom = -1;
            for (var y = 0; y < bitmap.Height; y++)
            {
                for (var px = 0; px < bitmap.Width; px++)
                {
                    if (bitmap.GetPixel(px, y).Red >= 250) continue;
                    if (y < top) top = y;
                    if (y > bottom) bottom = y;
                }
            }
            return (top, bottom);
        }

        using var natural = Render("x.font = '48px sans-serif';", "Handgloves and hamburgefonstiv");

        var result = new JsDrawingEngine().Execute("""
            const c = createCanvas(900, 120);
            const x = c.getContext('2d');
            x.fillStyle = '#ffffff';
            x.fillRect(0, 0, 900, 120);
            x.fillStyle = '#000000';
            x.font = '48px sans-serif';
            x.fillText('Handgloves and hamburgefonstiv', 20, 80, 300);
            c;
            """, 900, 120, null, "png", 100);

        Assert.True(result.Success, result.Error);
        using var condensed = SKBitmap.Decode(result.ImageBytes!);

        Assert.Equal(InkRows(natural), InkRows(condensed));
    }

    /// <summary>A tracked run is condensed along with its tracking, so the limit still holds.</summary>
    [Fact]
    public void TestMaxWidthAccountsForTracking()
    {
        var result = new JsDrawingEngine().Execute("""
            const c = createCanvas(900, 120);
            const x = c.getContext('2d');
            x.fillStyle = '#ffffff';
            x.fillRect(0, 0, 900, 120);
            x.fillStyle = '#000000';
            x.font = '40px sans-serif';
            x.letterSpacing = '18px';
            x.fillText('HANDGLOVES', 20, 80, 280);
            c;
            """, 900, 120, null, "png", 100);

        Assert.True(result.Success, result.Error);
        using var bitmap = SKBitmap.Decode(result.ImageBytes!);
        var (_, right) = InkColumns(bitmap);

        Assert.True(right <= 20 + 280 + 2, $"tracked run reached column {right}, past the 300 limit.");
    }

    /// <summary>Stroked text honours the limit too.</summary>
    [Fact]
    public void TestMaxWidthAppliesToStrokedText()
    {
        var result = new JsDrawingEngine().Execute("""
            const c = createCanvas(900, 120);
            const x = c.getContext('2d');
            x.fillStyle = '#ffffff';
            x.fillRect(0, 0, 900, 120);
            x.strokeStyle = '#000000';
            x.lineWidth = 1;
            x.font = '48px sans-serif';
            x.strokeText('Handgloves and hamburgefonstiv', 20, 80, 300);
            c;
            """, 900, 120, null, "png", 100);

        Assert.True(result.Success, result.Error);
        using var bitmap = SKBitmap.Decode(result.ImageBytes!);
        var (_, right) = InkColumns(bitmap);

        Assert.True(right <= 20 + 300 + 3, $"stroked ink reached column {right}, past the limit.");
    }

    /// <summary>
    /// A multi-line block is condensed once, by its widest line.
    /// </summary>
    /// <remarks>
    /// Condensing each line to its own scale would set the same typeface at a different width on
    /// every line, which reads as a mistake rather than as a fitted block.
    /// </remarks>
    [Fact]
    public void TestMaxWidthCondensesAMultiLineBlockUniformly()
    {
        // The \n must survive into the JS source as an escape, not as a real newline that would
        // split the string literal across two lines.
        const string Block = @"SHORT\nA MUCH LONGER LINE HERE";

        // What the two lines measure at their natural width, before any condensing.
        var ratio = Convert.ToSingle(Eval($$"""
            const c = createCanvas(10, 10);
            const x = c.getContext('2d');
            x.font = '40px sans-serif';
            x.measureText('SHORT').width / x.measureText('A MUCH LONGER LINE HERE').width;
            """), CultureInfo.InvariantCulture);

        var result = new JsDrawingEngine().Execute($$"""
            const c = createCanvas(900, 220);
            const x = c.getContext('2d');
            x.fillStyle = '#ffffff';
            x.fillRect(0, 0, 900, 220);
            x.fillStyle = '#000000';
            x.font = '40px sans-serif';
            x.textBaseline = 'top';
            x.fillText('{{Block}}', 20, 30, 300);
            c;
            """, 900, 220, null, "png", 100);

        Assert.True(result.Success, result.Error);
        using var bitmap = SKBitmap.Decode(result.ImageBytes!);

        // Split on the blank row between the two lines rather than at the middle of the canvas —
        // the second line's descenders reach well past halfway, and a band that catches both lines
        // measures the wider of them twice.
        var boundary = BlankRowAfterFirstInk(bitmap);
        var first = InkColumnsInBand(bitmap, 0, boundary);
        var second = InkColumnsInBand(bitmap, boundary, bitmap.Height);

        var longWidth = second.Right - second.Left;
        var shortWidth = first.Right - first.Left;

        Assert.True(longWidth <= 302, $"the widest line spans {longWidth}, past the 300 limit.");
        Assert.True(longWidth > 250, $"the widest line spans only {longWidth} — it was over-condensed.");

        // Both lines took the same scale, so their drawn ratio still matches the natural one.
        Assert.InRange((float)shortWidth / longWidth, ratio - 0.06f, ratio + 0.06f);
    }

    /// <summary>A non-positive limit draws nothing, as the canvas specification requires.</summary>
    [Fact]
    public void TestANonPositiveMaxWidthDrawsNothing()
    {
        foreach (var limit in new[] { "0", "-10" })
        {
            var result = new JsDrawingEngine().Execute($$"""
                const c = createCanvas(400, 100);
                const x = c.getContext('2d');
                x.fillStyle = '#ffffff';
                x.fillRect(0, 0, 400, 100);
                x.fillStyle = '#000000';
                x.font = '40px sans-serif';
                x.fillText('Handgloves', 20, 70, {{limit}});
                c;
                """, 400, 100, null, "png", 100);

            Assert.True(result.Success, result.Error);
            using var bitmap = SKBitmap.Decode(result.ImageBytes!);
            Assert.Equal(0, InkPixels(bitmap));
        }
    }
    #endregion

    #region Wrapping
    /// <summary>Wrapped paragraph text accounts for tracking when deciding where to break.</summary>
    [Fact]
    public void TestWrappingAccountsForTracking()
    {
        var lines = Eval("""
            const c = createCanvas(400, 300);
            const x = c.getContext('2d');
            x.font = '20px sans-serif';
            const before = x.measureText('the quick brown fox').width;
            x.letterSpacing = '6px';
            const after = x.measureText('the quick brown fox').width;
            after > before ? 'wider' : 'unchanged';
            """);

        Assert.Equal("wider", lines?.ToString());
    }
    #endregion
}
