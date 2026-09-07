namespace Polson.Tests.Drawing;

using System;
using System.Linq;
using Polson.Drawing.Svg;
using Polson.MCPServer;
using SkiaSharp;
using Xunit;

/// <summary>
/// The vector brush — a nib template bent along a path, as real geometry.
/// </summary>
/// <remarks>
/// <para>
/// This is the capability Manual 14 §9 lists as the vector surface's remaining deficit, so the tests
/// that matter are the ones proving it produces <b>geometry</b> rather than a picture: a filled
/// <c>d</c> string that varies in width along its length, survives serialisation, and follows a
/// curve rather than cutting across it.
/// </para>
/// <para>
/// Assertions are on measured geometry wherever possible — the width of the mark at three points
/// along the stroke, the distance from the drawn outline to the path it was supposed to follow —
/// because "it drew something" is satisfied by every wrong answer this code could give.
/// </para>
/// </remarks>
public class SnapBrushTests : TestsRuntime
{
    #region Template Tests
    [Fact]
    public void TestPresetsAreAllConstructible()
    {
        foreach (var name in Nib.Presets)
        {
            var brush = Nib.Preset(name);
            Assert.True(brush.PointCount > 3, $"{name} has {brush.PointCount} points");
            Assert.True(brush.HalfWidth > 0f, $"{name} has no width");
        }
    }

    [Fact]
    public void TestAnUnknownPresetNamesTheRealOnes()
    {
        // Handing back a default nib would ship the wrong brush silently, which is worse than failing.
        var error = Assert.Throws<ArgumentException>(() => Nib.Preset("bristle"));
        Assert.Contains("taper", error.Message, StringComparison.Ordinal);
        Assert.Contains("split", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("taper")]
    [InlineData("wedge")]
    [InlineData("chisel")]
    [InlineData("split")]
    public void TestAPresetNameIsNeverMistakenForPathData(string preset)
    {
        // Found by test, and it failed in the worst direction: the first sniff asked whether the
        // string contained a path command letter, and 'taper' contains `t` and `a`. So it parsed as
        // a `d` string, produced an empty nib, drew nothing, and reported success. Presets are now
        // checked first and path data must begin with a move.
        var nib = Snap.Brush(preset);
        Assert.True(nib.PointCount > 3, $"'{preset}' was read as path data and produced an empty nib");
        Assert.NotEqual(string.Empty, nib.Deform("M0,100 L200,100"));
    }

    [Fact]
    public void TestASplitNibHasSeveralSeparateContours()
    {
        // The gaps between ribbons must be real holes, so they have to be distinct contours.
        Assert.True(Nib.Split(4).ContourCount >= 3);
        Assert.Equal(1, Nib.Taper().ContourCount);
    }

    [Fact]
    public void TestACustomTemplateIsNormalisedIntoBackboneSpace()
    {
        // Drawn at an arbitrary size and offset, it must still span the whole stroke and stay centred.
        var brush = Nib.FromPath("M400,900 L900,880 L900,920 L400,910 Z");
        var mark = brush.Deform("M0,100 L200,100");

        Assert.NotEqual(string.Empty, mark);
        var bounds = BoundsOf(mark);
        Assert.True(bounds.Left < 4f && bounds.Right > 196f,
            $"the template should map across the whole stroke, got {bounds.Left}..{bounds.Right}");
        Assert.True(Math.Abs(bounds.MidY - 100f) < 6f, $"the mark should straddle the path, got midY {bounds.MidY}");
    }

    [Fact]
    public void TestATemplateWithNoHorizontalExtentDoesNotEmitNaN()
    {
        // A caller's own geometry can collapse; dividing by the width would put NaN in a `d` string,
        // which parses to an empty path and draws nothing with no indication why.
        var mark = Nib.FromPath("M50,0 L50,40").Deform("M0,100 L200,100");
        Assert.DoesNotContain("NaN", mark, StringComparison.OrdinalIgnoreCase);
    }
    #endregion

    #region Deformation Tests
    [Fact]
    public void TestTheMarkVariesInWidthAlongTheStroke()
    {
        // The point of a brush. A taper is nothing at the ends and fullest in the middle, so a
        // constant-width result means the profile was ignored and this is just an offset outline.
        var mark = Nib.Taper(width: 20f).Deform("M0,100 L400,100");
        var atStart = MarkHeightNear(mark, 8f);
        var atMiddle = MarkHeightNear(mark, 200f);
        var atEnd = MarkHeightNear(mark, 392f);

        Assert.True(atMiddle > 14f, $"the taper should be fullest in the middle, measured {atMiddle}");
        Assert.True(atStart < atMiddle / 2f, $"start {atStart} should be far thinner than middle {atMiddle}");
        Assert.True(atEnd < atMiddle / 2f, $"end {atEnd} should be far thinner than middle {atMiddle}");
    }

    [Fact]
    public void TestAWedgeIsThickAtTheStartAndPointedAtTheEnd()
    {
        var mark = Nib.Wedge(width: 20f).Deform("M0,100 L400,100");
        Assert.True(MarkHeightNear(mark, 8f) > MarkHeightNear(mark, 392f) * 3f,
            "a wedge should land full and lift to a point");
    }

    [Fact]
    public void TestThicknessScalesTheMark()
    {
        var thin = Nib.Taper(width: 10f).Deform("M0,100 L300,100");
        var thick = Nib.Taper(width: 10f).Deform("M0,100 L300,100", thickness: 3f);

        var ratio = MarkHeightNear(thick, 150f) / MarkHeightNear(thin, 150f);
        Assert.True(Math.Abs(ratio - 3f) < 0.3f, $"thickness 3 should treble the mark, measured {ratio:0.00}x");
    }

    [Fact]
    public void TestALongerStrokeIsNotAFatterOne()
    {
        // x is a parameter and y is pixels — that asymmetry is what makes it a brush rather than a
        // shape being stretched, and it is the easiest thing to get wrong when normalising.
        var shortMark = Nib.Taper(width: 16f).Deform("M0,100 L100,100");
        var longMark = Nib.Taper(width: 16f).Deform("M0,100 L800,100");

        Assert.True(Math.Abs(MarkHeightNear(shortMark, 50f) - MarkHeightNear(longMark, 400f)) < 1.5f,
            "the nib's width must not scale with the length of the stroke");
    }

    [Fact]
    public void TestTheMarkFollowsACurveRatherThanCuttingAcrossIt()
    {
        // The failure this catches: sampling the target too coarsely, so a long template edge spans
        // an arc as one straight segment. Every point of the mark must stay near the path.
        const string arc = "M40,240 C40,40 360,40 360,240";
        var mark = Nib.Taper(width: 8f).Deform(arc, segmentLength: 2f);

        using var target = SKPath.ParseSvgPathData(arc);
        using var measure = new SKPathMeasure(target, false);
        var worst = PointsOf(mark).Max(p => DistanceToPath(p, measure));

        Assert.True(worst < 12f, $"the mark strays {worst:0.0}px from its path; it is cutting the corner");
    }

    [Fact]
    public void TestEachSubPathOfTheTargetGetsItsOwnStroke()
    {
        // Flattening sub-paths into one point list draws a bridging stroke across the gap — a mark
        // that is in nobody's drawing. Two separated segments must produce two separated marks.
        var mark = Nib.Taper(width: 10f).Deform("M0,100 L100,100 M300,100 L400,100");
        var xs = PointsOf(mark).Select(p => p.X).ToList();

        Assert.True(xs.Any(x => x < 110f) && xs.Any(x => x > 290f), "both sub-paths should be drawn");
        Assert.False(xs.Any(x => x is > 140f and < 260f), "a bridging stroke was drawn between the sub-paths");
    }

    [Fact]
    public void TestSimplificationShrinksTheMarkWithoutChangingIt()
    {
        // The claim the tolerance default rests on. Both halves are asserted: that it is much smaller,
        // and that the mark is the same one — a size win that quietly reshaped the stroke would be
        // the worst outcome, and is exactly what a loose tolerance buys.
        const string spine = "M40,240 C40,40 360,40 360,240";
        var full = Nib.Taper(width: 18f).Deform(spine, tolerance: 0f);
        var lean = Nib.Taper(width: 18f).Deform(spine);

        Assert.True(lean.Length * 3 < full.Length,
            $"expected a large reduction, got {full.Length} to {lean.Length} characters");

        foreach (var x in new[] { 80f, 140f, 200f, 260f, 320f })
        {
            var difference = MathF.Abs(MarkHeightNear(full, x) - MarkHeightNear(lean, x));
            Assert.True(difference < 1f, $"the mark changed width by {difference:0.00}px at x={x}");
        }
    }

    [Fact]
    public void TestAnEmptyOrMissingTargetDrawsNothingRatherThanThrowing()
    {
        Assert.Equal(string.Empty, Nib.Taper().Deform(null));
        Assert.Equal(string.Empty, Nib.Taper().Deform(""));
        Assert.Equal(string.Empty, Nib.Taper().Deform("M50,50"));
    }
    #endregion

    #region Script Surface Tests
    [Fact]
    public void TestAScriptCanDrawABrushStrokeAndItSerialisesAsAFilledPath()
    {
        var svg = Svg("""
            const nib = Snap.brush('taper');
            paper.brushStroke('M20,150 C60,40 140,40 180,150', nib, 2).attr({ fill: '#15151a' });
            """);

        Assert.Contains("<path", svg, StringComparison.Ordinal);
        Assert.Contains("15151A", svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NaN", svg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TestABrushStrokeReachesTheRender()
    {
        var png = Render("""
            paper.rect(0, 0, 200, 200).attr({ fill: '#ffffff' });
            paper.brushStroke('M20,100 L180,100', 'taper', 3).attr({ fill: '#000000' });
            """);

        Assert.True(PixelAt(png, 100, 100).Red < 60, "the brush mark did not render");
        Assert.True(PixelAt(png, 100, 40).Red > 240, "the mark should not fill the whole frame");
    }

    [Fact]
    public void TestAPresetNameWorksInPlaceOfABrushObject()
    {
        var result = Execute("log(String(paper.brushStroke('M0,100 L200,100', 'wedge').attr('d').length > 20));");
        Assert.True(result.Success, result.Error);
        Assert.Contains("true", string.Join(" ", result.Logs), StringComparison.Ordinal);
    }

    [Fact]
    public void TestABadPresetNameFailsInTheScriptRatherThanDrawingTheDefault()
    {
        var result = Execute("paper.brushStroke('M0,100 L200,100', 'bristle');");

        Assert.False(result.Success);
        Assert.Contains("taper", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TestSnapBrushIsCallableAndANamespaceAtOnce()
    {
        // Unusual wiring, so worth pinning: `Snap.brush(x)` constructs and `Snap.brush.taper(...)`
        // tunes, on the same name. If the namespace half regresses, the call half still works and
        // nothing else would notice.
        var result = Execute("""
            const plain = Snap.brush('taper');
            const tuned = Snap.brush.taper(40, 1.6);
            log([typeof Snap.brush, typeof Snap.brush.taper, Snap.brush.presets.length,
                 tuned.halfWidth > plain.halfWidth].join(','));
            """);

        Assert.True(result.Success, result.Error);
        Assert.Contains("function,function,4,true", string.Join(" ", result.Logs), StringComparison.Ordinal);
    }

    [Fact]
    public void TestAnElementCanBeUsedAsTheTargetAndAsTheNib()
    {
        var result = Execute("""
            const spine = paper.path('M20,150 C60,40 140,40 180,150').attr({ fill: 'none' });
            const nib = Snap.brush(paper.path('M0,0 L100,-6 L100,6 Z'));
            log(String(nib.deform(spine).length > 40));
            """);

        Assert.True(result.Success, result.Error);
        Assert.Contains("true", string.Join(" ", result.Logs), StringComparison.Ordinal);
    }
    #endregion

    #region Methods
    /// <summary>The nib factories as a script reaches them: <c>Snap.brush.taper(...)</c>.</summary>
    /// <remarks>
    /// Deliberately the script-facing object rather than <see cref="SnapBrush"/>'s own statics, which
    /// are internal — a static on the nib type would read as <c>nib.taper</c> on the documented
    /// surface, and that is a call nothing can make.
    /// </remarks>
    private static readonly SnapBrushApi Nib = new();

    /// <summary>How tall the filled mark is at <paramref name="x"/> — i.e. how wide the stroke is there.</summary>
    /// <remarks>
    /// <b>Measured by testing what the fill covers, not by looking at the emitted points.</b> The
    /// first version of this counted points within a band of x, which quietly depended on the
    /// deformation emitting them densely — so adding the simplification pass broke the test while the
    /// drawing was unchanged. Coverage is what the mark actually claims, and it is indifferent to how
    /// many points express it.
    /// </remarks>
    private static float MarkHeightNear(string pathData, float x)
    {
        using var path = SKPath.ParseSvgPathData(pathData);
        if (path is null || path.IsEmpty) return 0f;

        var bounds = path.Bounds;
        float top = float.NaN, bottom = float.NaN;
        for (var y = bounds.Top - 1f; y <= bounds.Bottom + 1f; y += 0.2f)
        {
            if (!path.Contains(x, y)) continue;
            if (float.IsNaN(top)) top = y;
            bottom = y;
        }

        return float.IsNaN(top) ? 0f : bottom - top;
    }

    private static SKPoint[] PointsOf(string pathData)
    {
        using var path = SKPath.ParseSvgPathData(pathData);
        return path is null ? [] : path.Points;
    }

    private static SKRect BoundsOf(string pathData)
    {
        using var path = SKPath.ParseSvgPathData(pathData);
        return path?.Bounds ?? SKRect.Empty;
    }

    /// <summary>Shortest distance from a point to a path, by sampling it. Enough for a tolerance check.</summary>
    private static float DistanceToPath(SKPoint point, SKPathMeasure measure)
    {
        var best = float.MaxValue;
        for (var i = 0; i <= 400; i++)
        {
            if (!measure.GetPosition(measure.Length * i / 400f, out var p)) continue;
            var d = MathF.Sqrt((p.X - point.X) * (p.X - point.X) + (p.Y - point.Y) * (p.Y - point.Y));
            if (d < best) best = d;
        }
        return best;
    }

    private static DrawingExecutionResult Execute(string body) =>
        new JsDrawingEngine().Execute($"const paper = Snap(200, 200); {body} paper;", 200, 200, null, "png", 100);

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
