namespace Polson.Tests.ExtendedMind;

using System;

using global::Polson.Tests;

using Polson.ExtendedMind.ImageGeneration;

using SkiaSharp;

using Xunit;

/// <summary>
/// The cut-out half of asset requisition: the chroma key, the sheet split, and the likeness refusal.
/// </summary>
/// <remarks>
/// <para>
/// <b>Added because a matte could not answer a face and nothing said why.</b> <c>ToMatte</c>
/// thresholds on luminance, so everything inside the subject collapses to one value — correct for a
/// silhouette and fatal for anything with an interior. No descriptor could reach past that, because
/// the wording belongs to the server, so the gap was a missing call rather than a missing
/// instruction.
/// </para>
/// <para>
/// The cases are mostly about the <i>split</i> rather than the key, because that is the failure that
/// renders perfectly: a sheet divided into equal columns rather than on its gaps cuts through
/// shoulders, and the picture that comes out looks like a drawing mistake rather than a division one.
/// </para>
/// </remarks>
public class CutoutTests : TestsRuntime
{
    #region Chroma Key Tests
    /// <summary>The ground goes; the subject stays, interior and all.</summary>
    /// <remarks>
    /// The whole difference from a matte in one assertion: a mid-grey subject on magenta keeps its
    /// mid-grey, where <c>ToMatte</c> would have driven it to one extreme or the other.
    /// </remarks>
    [Fact]
    public void ChromaKey_RemovesTheGroundAndKeepsTheInterior()
    {
        using var plate = Sheet(128, 128, [(40, 88)]);

        using var keyed = PlateAnalysis.ChromaKey(plate, Magenta);

        Assert.Equal(0, keyed.GetPixel(4, 64).Alpha);
        Assert.Equal(255, keyed.GetPixel(64, 64).Alpha);
        Assert.Equal(128, keyed.GetPixel(64, 64).Red);
    }

    /// <summary>A background near the key colour still goes, which is why the level is measured.</summary>
    [Fact]
    public void SampleBackground_MeasuresTheGroundRatherThanAssumingIt()
    {
        var drifted = new SKColor(0xF2, 0x0C, 0xEE);
        using var plate = Sheet(128, 128, [(40, 88)], drifted);

        var measured = PlateAnalysis.SampleBackground(plate);
        using var keyed = PlateAnalysis.ChromaKey(plate, measured, 0.02);

        Assert.Equal(drifted.Red, measured.Red);
        Assert.Equal(0, keyed.GetPixel(4, 64).Alpha);
    }

    /// <summary>A tolerance wide enough to swallow the subject is a real failure, and coverage says so.</summary>
    /// <remarks>
    /// This is the readback the reference tells an agent to check. It exists because a key that took
    /// the subject produces a fully transparent cell, which composites as nothing at all and looks
    /// exactly like a draw call that never ran.
    /// </remarks>
    [Fact]
    public void AlphaCoverage_ReportsAKeyThatTookTheSubject()
    {
        using var plate = Sheet(128, 128, [(40, 88)]);

        using var sane = PlateAnalysis.ChromaKey(plate, Magenta);
        using var greedy = PlateAnalysis.ChromaKey(plate, Magenta, 0.9);

        Assert.True(PlateAnalysis.AlphaCoverage(sane) > 0.1);
        Assert.Equal(0, PlateAnalysis.AlphaCoverage(greedy));
    }
    #endregion

    #region Split Tests
    /// <summary>Four separated subjects give four runs, found where the background actually is.</summary>
    [Fact]
    public void SegmentsByGaps_FindsOneRunPerSubject()
    {
        using var plate = Sheet(400, 100, [(10, 80), (110, 180), (210, 280), (310, 380)]);
        using var keyed = PlateAnalysis.ChromaKey(plate, Magenta);

        var runs = PlateAnalysis.SegmentsByGaps(PlateAnalysis.AlphaColumnProfile(keyed));

        Assert.Equal(4, runs.Length);
        Assert.Equal(10, runs[0].Start);
        Assert.Equal(380, runs[3].End);
    }

    /// <summary>Subjects that touch give fewer runs than were asked for, which is the fallback signal.</summary>
    /// <remarks>
    /// The case the <c>split</c> readback exists for. Two touching figures read as one run, so the
    /// caller cannot divide on gaps and has to fall back — and a caller that trusted the run count
    /// would silently deliver three cells where four were asked for.
    /// </remarks>
    [Fact]
    public void SegmentsByGaps_ReportsFewerRunsWhenSubjectsTouch()
    {
        using var plate = Sheet(400, 100, [(10, 190), (190, 380)]);
        using var keyed = PlateAnalysis.ChromaKey(plate, Magenta);

        var runs = PlateAnalysis.SegmentsByGaps(PlateAnalysis.AlphaColumnProfile(keyed));

        Assert.Single(runs);
    }

    /// <summary>Nothing opaque is an empty result rather than one run spanning the frame.</summary>
    [Fact]
    public void SegmentsByGaps_FindsNothingInAnEmptySheet()
    {
        using var plate = Sheet(200, 100, []);
        using var keyed = PlateAnalysis.ChromaKey(plate, Magenta);

        Assert.Empty(PlateAnalysis.SegmentsByGaps(PlateAnalysis.AlphaColumnProfile(keyed)));
    }
    #endregion

    #region Trim Tests
    /// <summary>A cell is cropped to its own extent, which is why cells of one sheet differ in size.</summary>
    [Fact]
    public void TrimToAlpha_CropsToTheSubject()
    {
        using var plate = Sheet(200, 100, [(50, 110)], rowFrom: 20, rowTo: 60);
        using var keyed = PlateAnalysis.ChromaKey(plate, Magenta);

        using var trimmed = PlateAnalysis.TrimToAlpha(keyed);

        Assert.Equal(61, trimmed.Width);
        Assert.Equal(41, trimmed.Height);
    }

    /// <summary>A fully keyed sheet trims to one transparent pixel rather than throwing.</summary>
    /// <remarks>
    /// A degenerate cell is a picture problem, not a crash: the caller is meant to read
    /// <c>coverage</c> and decide, so this must survive long enough to be measured.
    /// </remarks>
    [Fact]
    public void TrimToAlpha_SurvivesASheetWithNoSubject()
    {
        using var plate = Sheet(80, 80, []);
        using var keyed = PlateAnalysis.ChromaKey(plate, Magenta);

        using var trimmed = PlateAnalysis.TrimToAlpha(keyed);

        Assert.Equal(1, trimmed.Width);
        Assert.Equal(0, PlateAnalysis.AlphaCoverage(trimmed));
    }
    #endregion

    #region Refusal Tests
    /// <summary>A named person asking for a face is refused before anything is spent.</summary>
    /// <remarks>
    /// The same pair of signals a matte refuses on, and it matters more here: a matte of a named
    /// person is a silhouette that might resemble them, where a cutout is a fabricated portrait.
    /// </remarks>
    [Theory]
    [InlineData("Stanley Kubrick bearded director portrait")]
    [InlineData("a headshot of Grace Hopper")]
    public void NamedLikeness_RefusesAPersonAndAFace(string descriptor) =>
        Assert.NotNull(AssetRequisitionToolkit.NamedLikeness(descriptor));

    /// <summary>One signal alone is ordinary vocabulary and is not refused.</summary>
    /// <remarks>
    /// Both halves are load-bearing: refusing a name alone turns away "Art Deco", and refusing a face
    /// word alone turns away the anonymous figure this call most often wants.
    /// </remarks>
    [Theory]
    [InlineData("an Art Deco fan motif")]
    [InlineData("a weathered ranch hand, head and shoulders, shouting")]
    public void NamedLikeness_AllowsOneSignalAlone(string descriptor) =>
        Assert.Null(AssetRequisitionToolkit.NamedLikeness(descriptor));
    #endregion

    #region Helpers
    static SKColor Magenta => new(0xFF, 0x00, 0xFF);

    /// <summary>A magenta ground carrying mid-grey blocks in the given column ranges.</summary>
    static SKBitmap Sheet(
        int width, int height, (int From, int To)[] blocks,
        SKColor? ground = null, int rowFrom = 0, int rowTo = int.MaxValue)
    {
        var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(ground ?? Magenta);
            using var paint = new SKPaint { Color = new SKColor(128, 128, 128) };
            foreach (var (from, to) in blocks)
            {
                canvas.DrawRect(
                    SKRect.Create(from, rowFrom, to - from + 1, Math.Min(rowTo, height - 1) - rowFrom + 1),
                    paint);
            }
        }

        return bitmap;
    }
    #endregion
}
