namespace Polson.Tests.Drawing;

using System;
using System.Globalization;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// <c>bitmap.diff</c>, <c>diffMap</c>, <c>rowProfile</c> and <c>palette</c> — measuring a render
/// instead of looking at it.
/// </summary>
/// <remarks>
/// These exist because a harness run found every structural defect with a hand-built per-scanline
/// edge report, rebuilt in JavaScript each time, and lost three scripts to the statement cap doing
/// it. The point of moving the loops native is not speed for its own sake — it is that the
/// verification an agent is *told* to perform has to fit in the budget it is given.
/// </remarks>
public class ImageComparisonTests : TestsRuntime
{
    #region Methods
    private static object? Eval(string body)
    {
        var result = new JsDrawingEngine().Execute(body, 120, 80, null, "png", 100);
        Assert.True(result.Success, result.Error);
        return result.ReturnValue;
    }

    private static string Text(string body) => Eval(body)?.ToString() ?? string.Empty;

    private static float Number(string body) =>
        Convert.ToSingle(Eval(body), CultureInfo.InvariantCulture);

    /// <summary>Two canvases, the second with its rectangle shifted right by <c>shift</c>.</summary>
    private const string Pair = """
        const make = (shift, color) => {
            const c = createCanvas(120, 80);
            const x = c.getContext('2d');
            x.fillStyle = '#ffffff';
            x.fillRect(0, 0, 120, 80);
            x.fillStyle = color || '#1f6f8b';
            x.fillRect(20 + shift, 20, 40, 40);
            return c.toBitmap();
        };
        """;
    #endregion

    #region Diff
    /// <summary>Identical bitmaps report identical, with no differing bounds.</summary>
    [Fact]
    public void TestIdenticalBitmapsDifferNowhere()
    {
        Assert.Equal("true,0,null", Text($$"""
            {{Pair}}
            const d = make(0).diff(make(0));
            [d.identical, d.differingPixels, String(d.bounds)].join(',');
            """));
    }

    /// <summary>A shifted shape differs, and similarity reflects how much.</summary>
    [Fact]
    public void TestAShiftedShapeIsDetected()
    {
        // A 40px square moved 10px right: two 10x40 strips differ = 800 of 9600 pixels.
        Assert.Equal("800", Text($$"""
            {{Pair}}
            make(0).diff(make(10)).differingPixels.toString();
            """));
    }

    /// <summary>
    /// The bounds say where the difference is, which a score cannot.
    /// </summary>
    /// <remarks>
    /// "92% similar" is true of a chart with one bar wrong and of one with every edge slightly soft.
    /// The rectangle distinguishes them.
    /// </remarks>
    [Fact]
    public void TestBoundsLocateTheDifference()
    {
        Assert.Equal("20,20,50,40", Text($$"""
            {{Pair}}
            const b = make(0).diff(make(10)).bounds;
            [b.x, b.y, b.width, b.height].join(',');
            """));
    }

    /// <summary>Tolerance absorbs near-identical colour, which is what antialiasing produces.</summary>
    [Fact]
    public void TestToleranceAbsorbsNearIdenticalColour()
    {
        Assert.Equal("differs,same", Text($$"""
            {{Pair}}
            const a = make(0, '#1f6f8b');
            const b = make(0, '#22728e');
            const strict = a.diff(b, { tolerance: 0 }).identical ? 'same' : 'differs';
            const loose = a.diff(b, { tolerance: 8 }).identical ? 'same' : 'differs';
            [strict, loose].join(',');
            """));
    }

    /// <summary>
    /// Comparing different sizes fails loudly rather than scoring an overlap.
    /// </summary>
    /// <remarks>
    /// A partial-overlap similarity would look exactly like an answer, and the caller would have no
    /// reason to doubt it. The message names both sizes and the way out.
    /// </remarks>
    [Fact]
    public void TestMismatchedSizesRefuseToCompare()
    {
        var result = new JsDrawingEngine().Execute("""
            const a = createCanvas(120, 80).toBitmap();
            const b = createCanvas(60, 40).toBitmap();
            a.diff(b);
            """, 120, 80, null, "png", 100);

        Assert.False(result.Success);
        Assert.Contains("120x80", result.Error);
        Assert.Contains("60x40", result.Error);
        Assert.Contains("resize", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A diff map is a bitmap of the same size, marking the changes.</summary>
    [Fact]
    public void TestDiffMapMarksTheChangedRegion()
    {
        Assert.Equal("120,80,marked,dimmed", Text($$"""
            {{Pair}}
            const m = make(0).diffMap(make(10), { color: '#ff0000' });
            const inside = m.getPixel(25, 40);      // differs: was filled, now white
            const outside = m.getPixel(5, 5);       // matches: dimmed white
            [m.width, m.height,
             inside.slice(0, 7) === '#FF0000' ? 'marked' : inside,
             outside.slice(7) !== 'FF' ? 'dimmed' : 'opaque'].join(',');
            """));
    }
    #endregion

    #region Row profile
    /// <summary>A colour class reports where it starts and ends on each row.</summary>
    [Fact]
    public void TestRowProfileFindsTheRegionEdges()
    {
        Assert.Equal("40,20,59,40", Text($$"""
            {{Pair}}
            const rows = make(0).rowProfile('#1f6f8b');
            const r = rows[0];
            [rows.length, r.start, r.end, r.extent].join(',');
            """));
    }

    /// <summary>Row indices are the actual rows, so a profile can be aligned against another.</summary>
    [Fact]
    public void TestRowProfileReportsRealRowIndices()
    {
        Assert.Equal("20,59", Text($$"""
            {{Pair}}
            const rows = make(0).rowProfile('#1f6f8b');
            [rows[0].index, rows[rows.length - 1].index].join(',');
            """));
    }

    /// <summary>
    /// Two profiles compared row by row locate a shift — the whole point of the primitive.
    /// </summary>
    /// <remarks>
    /// This is the cs-2 edge-delta report in five lines of script and 40 iterations, where the
    /// pixel-level version was thousands and had to be budgeted against the statement cap.
    /// </remarks>
    [Fact]
    public void TestProfilesCompareRowByRowToFindAShift()
    {
        Assert.Equal("40,10", Text($$"""
            {{Pair}}
            const want = make(0).rowProfile('#1f6f8b');
            const got = make(10).rowProfile('#1f6f8b');
            let rows = 0, worst = 0;
            for (let i = 0; i < want.length; i++) {
                rows++;
                worst = Math.max(worst, Math.abs(got[i].start - want[i].start));
            }
            [rows, worst].join(',');
            """));
    }

    /// <summary>Profiling by column works the other way round.</summary>
    [Fact]
    public void TestColumnProfileWorksOnTheOtherAxis()
    {
        Assert.Equal("40,20,59", Text($$"""
            {{Pair}}
            const cols = make(0).rowProfile('#1f6f8b', { axis: 'column' });
            [cols.length, cols[0].start, cols[0].end].join(',');
            """));
    }

    /// <summary>An absent colour profiles as empty, not as the whole image.</summary>
    [Fact]
    public void TestAnAbsentColourProfilesEmpty()
    {
        Assert.Equal(0f, Number($$"""
            {{Pair}}
            make(0).rowProfile('#00ff00').length;
            """), 3);
    }
    #endregion

    #region Palette
    /// <summary>The dominant colours come back with their shares.</summary>
    [Fact]
    public void TestPaletteFindsTheDominantColours()
    {
        // 120x80 = 9600 px; the square is 1600 of them, so white ~83% and the fill ~17%.
        Assert.Equal("2,white-first,0.17", Text($$"""
            {{Pair}}
            const p = make(0).palette(4);
            [p.length,
             p[0].color === '#FFFFFF' ? 'white-first' : p[0].color,
             p[1].share.toFixed(2)].join(',');
            """));
    }

    /// <summary>Shares are fractions of the counted pixels.</summary>
    [Fact]
    public void TestPaletteSharesSumToOne()
    {
        Assert.Equal("1.00", Text($$"""
            {{Pair}}
            const p = make(0).palette(16);
            p.reduce((sum, e) => sum + e.share, 0).toFixed(2);
            """));
    }
    #endregion
}
