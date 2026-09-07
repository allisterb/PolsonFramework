namespace Polson.Tests.ExtendedMind;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using global::Polson;
using global::Polson.ExtendedMind;
using global::Polson.Tests;
using Polson.ExtendedMind.ImageGeneration;
using SkiaSharp;

using Xunit;

/// <summary>
/// The stencil half of <c>Assets.matte(...)</c>: the hard cut, the measured level, and the coverage
/// readback that makes the cut checkable.
/// </summary>
/// <remarks>
/// <para>
/// <b>Added because <c>matte</c> was already the only requisition that would answer a form request
/// and nothing said so.</b> The classifier deliberately does not run here — a matte of a silhouette
/// is what a matte is for — so this is the channel a header graphic comes through. What it lacked
/// was a hard edge: <c>ToMatte</c> was a luminance ramp, so a soft-edged generation arrived as grey
/// mush that clips with a halo and traces to nothing.
/// </para>
/// <para>
/// The cases are mostly about the <i>measured</i> level rather than the cut itself, because a fixed
/// threshold on a generated plate is the silent failure this exists to prevent.
/// </para>
/// </remarks>
public class MatteStencilTests : TestsRuntime
{
    #region Threshold Tests
    /// <summary>Without a threshold the ramp survives, which is what a height field wants.</summary>
    [Fact]
    public void ToMatte_KeepsTheRampByDefault()
    {
        using var ramp = Ramp(256);

        using var matte = PlateAnalysis.ToMatte(ramp);

        Assert.Equal(64, matte.GetPixel(64, 0).Red);
        Assert.Equal(192, matte.GetPixel(192, 0).Red);
    }

    /// <summary>A cut leaves pure black and pure white and nothing between.</summary>
    [Fact]
    public void ToMatte_CutsToTwoValues()
    {
        using var ramp = Ramp(256);

        using var matte = PlateAnalysis.ToMatte(ramp, threshold: 128);

        for (var x = 0; x < 256; x++)
        {
            var v = matte.GetPixel(x, 0).Red;
            Assert.True(v is 0 or 255, $"x={x} came back {v}, which is neither black nor white");
        }

        Assert.Equal(0, matte.GetPixel(100, 0).Red);
        Assert.Equal(255, matte.GetPixel(200, 0).Red);
    }

    /// <summary>
    /// Inversion happens after the cut, so an inverted stencil is the same shape reversed.
    /// </summary>
    /// <remarks>
    /// Cutting after inverting would test the level against the un-inverted image and land at
    /// <c>255 - threshold</c> — a picture that looks plausible and carries the wrong coverage.
    /// </remarks>
    [Fact]
    public void ToMatte_InvertsAfterCuttingRatherThanBefore()
    {
        using var ramp = Ramp(256);

        using var plain = PlateAnalysis.ToMatte(ramp, threshold: 200);
        using var inverted = PlateAnalysis.ToMatte(ramp, invert: true, threshold: 200);

        // Same cut point, opposite sides. Inverting first would move the boundary to 55.
        Assert.Equal(255, plain.GetPixel(220, 0).Red);
        Assert.Equal(0, inverted.GetPixel(220, 0).Red);
        Assert.Equal(0, plain.GetPixel(180, 0).Red);
        Assert.Equal(255, inverted.GetPixel(180, 0).Red);
    }
    #endregion

    #region Otsu Tests
    /// <summary>
    /// The level is found between the two populations, wherever the generation put them.
    /// </summary>
    /// <remarks>
    /// This is the case a fixed 128 gets wrong. A plate whose "black" is 35 and whose "white" is 160
    /// is entirely ordinary from an image model, and cutting it at 128 keeps a sliver of the subject.
    /// </remarks>
    [Theory]
    [InlineData(35, 160)]
    [InlineData(20, 250)]
    [InlineData(90, 170)]
    public void OtsuThreshold_LandsBetweenTheTwoPopulations(byte low, byte high)
    {
        using var plate = Generate(128, 128, (x, y) => Grey(x < 48 ? high : low));

        var level = PlateAnalysis.OtsuThreshold(plate);

        Assert.InRange(level, low, high - 1);
    }

    /// <summary>
    /// The measured level recovers the subject that a fixed 128 would lose.
    /// </summary>
    [Fact]
    public void OtsuThreshold_RecoversASubjectAFixedCutWouldDrop()
    {
        // A quarter-frame subject at 110 on a 20 ground. Both sit below a fixed 128, which is the
        // whole failure: the plate is perfectly legible and a midpoint cut erases all of it.
        using var plate = Generate(128, 128, (x, y) => Grey(x < 32 ? (byte)110 : (byte)20));

        using var fixedCut = PlateAnalysis.ToMatte(plate, threshold: 128);
        using var measured = PlateAnalysis.ToMatte(plate, threshold: PlateAnalysis.OtsuThreshold(plate));

        Assert.Equal(0, PlateAnalysis.Coverage(fixedCut));
        Assert.Equal(0.25, PlateAnalysis.Coverage(measured), 3);
    }
    #endregion

    #region Coverage Tests
    /// <summary>Coverage is the share of the frame that is on.</summary>
    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(32, 0.25)]
    [InlineData(64, 0.5)]
    [InlineData(128, 1.0)]
    public void Coverage_ReportsTheShareThatIsOn(int litColumns, double expected)
    {
        using var plate = Generate(128, 128, (x, y) => Grey(x < litColumns ? (byte)255 : (byte)0));

        Assert.Equal(expected, PlateAnalysis.Coverage(plate), 3);
    }

    /// <summary>
    /// An empty and a solid stencil are the two failures nothing else would report.
    /// </summary>
    /// <remarks>
    /// Both decode, both encode, and both draw — as an empty frame or a solid block. Coverage is the
    /// only signal a caller gets, which is why it is on the result rather than left to be recomputed.
    /// </remarks>
    [Fact]
    public void Coverage_SeparatesAFailedGenerationFromAWorkingOne()
    {
        using var empty = Generate(64, 64, (x, y) => Grey(0));
        using var solid = Generate(64, 64, (x, y) => Grey(255));
        using var real = Generate(64, 64, (x, y) => Grey(x is > 16 and < 48 ? (byte)255 : (byte)0));

        Assert.True(PlateAnalysis.Coverage(empty) < 0.01);
        Assert.True(PlateAnalysis.Coverage(solid) > 0.99);
        Assert.InRange(PlateAnalysis.Coverage(real), 0.1, 0.9);
    }
    #endregion

    #region Options Tests
    /// <summary>The ramp is still the default, so a height field is unaffected by any of this.</summary>
    [Fact]
    public void MatteOptions_DefaultToTheRamp()
    {
        var opts = new MatteOptions();

        Assert.False(opts.HardEdge);
        Assert.Null(opts.Threshold);
    }
    #endregion

    #region End To End Tests
    /// <summary>
    /// A stencil requisition cuts, reports the level it measured, and reports what it covers.
    /// </summary>
    [Fact]
    public async Task Matte_DeliversACutStencilAndReportsBothMeasurements()
    {
        var generator = new FakeGenerator(Plate());
        var toolkit = new AssetRequisitionToolkit(
            generator, new RequisitionCache(ImageGeneratorTests.TempCacheDir()), new AssetBudget(3), "Designer");

        var asset = await toolkit.Matte("a rearing horse", new MatteOptions { HardEdge = true, Size = 128 });

        Assert.True(asset.Success, asset.Error);
        Assert.NotNull(asset.Threshold);
        Assert.InRange(asset.Threshold!.Value, 20, 109);      // between the ground and the subject
        Assert.Equal(0.25, asset.Coverage, 2);                // the quarter-frame subject survived
        Assert.Contains("STENCIL", generator.Prompts[0], StringComparison.Ordinal);
    }

    /// <summary>
    /// A stencil can be drawn, because <c>image(src, …)</c> dispatches on <see cref="IDataUriSource"/>.
    /// </summary>
    /// <remarks>
    /// The gap this closes cost a live run two scripts. The requisition succeeded, and then
    /// <c>paper.image(stencil, …)</c> — which the workflow instructions tell the agent to write —
    /// fell through to the generic refusal because this record did not implement the interface that
    /// every other drawable asset does.
    /// </remarks>
    [Fact]
    public async Task Matte_CanBeHandedStraightToAnImageElement()
    {
        var toolkit = new AssetRequisitionToolkit(
            new FakeGenerator(Plate()), new RequisitionCache(ImageGeneratorTests.TempCacheDir()), new AssetBudget(3), "Designer");

        var asset = await toolkit.Matte("a rearing horse", new MatteOptions { HardEdge = true, Size = 128 });

        Assert.True(asset.Success, asset.Error);
        Assert.IsAssignableFrom<IDataUriSource>(asset);
        Assert.StartsWith("data:image/png;base64,", asset.ToDataUri(), StringComparison.Ordinal);
    }

    /// <summary>A failed matte yields an empty URI rather than a data URI wrapping nothing.</summary>
    [Fact]
    public void Matte_ThatFailedHasNoDataUri() =>
        Assert.Equal(string.Empty, new MatteAsset { Success = false }.ToDataUri());

    /// <summary>Without the flag nothing changes: same prompt, no cut, and no level to report.</summary>
    /// <remarks>
    /// The regression that matters. A height field and a displacement source are the calls that were
    /// already here, and neither wants a threshold applied to it behind its back.
    /// </remarks>
    [Fact]
    public async Task Matte_LeavesTheRampAloneWhenTheFlagIsOff()
    {
        var generator = new FakeGenerator(Plate());
        var toolkit = new AssetRequisitionToolkit(
            generator, new RequisitionCache(ImageGeneratorTests.TempCacheDir()), new AssetBudget(3), "Designer");

        var asset = await toolkit.Matte("rolling hills height field", new MatteOptions { Size = 128 });

        Assert.True(asset.Success, asset.Error);
        Assert.Null(asset.Threshold);
        Assert.Contains("greyscale mask", generator.Prompts[0], StringComparison.Ordinal);
        Assert.DoesNotContain("STENCIL", generator.Prompts[0], StringComparison.Ordinal);
    }
    #endregion

    #region Methods
    /// <summary>A plate whose subject and ground both sit below a fixed midpoint cut.</summary>
    static byte[] Plate()
    {
        using var bitmap = Generate(128, 128, (x, y) => Grey(x < 32 ? (byte)110 : (byte)20));
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    static SKColor Grey(byte v) => new(v, v, v);

    static SKBitmap Ramp(int size) => Generate(size, size, (x, y) => Grey((byte)(x * 255 / (size - 1))));

    static SKBitmap Generate(int width, int height, Func<int, int, SKColor> f)
    {
        var bitmap = new SKBitmap(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                bitmap.SetPixel(x, y, f(x, y));
            }
        }

        return bitmap;
    }
    #endregion

    #region Types
    /// <summary>Returns fixed bytes and keeps the prompts, so the two wordings can be told apart.</summary>
    sealed class FakeGenerator(byte[] png) : IImageGenerator
    {
        public string Model => "fake-image-model";

        public IReadOnlyList<string> Prompts => prompts;

        public Task<ImageGenerationResult> GenerateImage(
            string prompt,
            string? model = null,
            string? aspectRatio = null,
            IReadOnlyList<byte[]>? conditionOn = null,
            CancellationToken ct = default)
        {
            prompts.Add(prompt);
            var useModel = model ?? Model;
            ImageGenerator.TryReadPngSize(png, out var width, out var height);

            return Task.FromResult(new ImageGenerationResult
            {
                Success = true,
                ImageBytes = png,
                Width = width,
                Height = height,
                MimeType = "image/png",
                Model = useModel,
                Prompt = prompt,
                Hash = ImageGenerator.HashOf(useModel, prompt, aspectRatio, conditionOn),
                GeneratedUtc = DateTime.UtcNow,
                ElapsedMs = 1,
                Charged = true,
            });
        }

        readonly List<string> prompts = [];
    }
    #endregion
}
