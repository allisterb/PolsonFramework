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
/// What happens <em>after</em> a generation succeeds.
/// </summary>
/// <remarks>
/// Every failure path is free to exercise and was already covered; the one path that produces an
/// asset bills for an image, so the whole of what the toolkit does with a returned master — measure
/// the tiling, repair it, resize, encode, spend the budget, write the cache, stamp provenance, add to
/// the library, record the run event — had no offline test at all. <see cref="IImageGenerator"/> is
/// the seam that fixes that: the fake below returns fixed bytes and never touches a network.
/// <para>
/// The fake counts its calls, which is what makes the cache assertion mean something. A second
/// requisition that merely returns an equal asset proves nothing; a second requisition the generator
/// never sees is a cache hit.
/// </para>
/// </remarks>
public class RequisitionSuccessPathTests : TestsRuntime
{
    #region Tests
    /// <summary>A returned master becomes a delivered asset, and the spend is recorded once.</summary>
    [Fact]
    public async Task TestASuccessfulRequisitionDeliversAnAssetAndSpendsOnce()
    {
        var budget = new AssetBudget(3);
        var generator = new FakeImageGenerator(TilingMaster(256));
        var toolkit = new AssetRequisitionToolkit(
            generator, new RequisitionCache(ImageGeneratorTests.TempCacheDir()), budget, "Framer");

        var asset = await toolkit.Material("weathered oak planking", new MaterialOptions { Size = 128 });

        Assert.True(asset.Success, asset.Error);
        Assert.Equal(128, asset.Size);
        Assert.Equal("image/webp", asset.MimeType);
        Assert.NotEmpty(asset.Bytes);
        Assert.Equal(1, generator.Calls);

        // Charged, and charged exactly once. The token count comes from the service, not an estimate.
        Assert.Equal(1, budget.Spent);
        Assert.Equal(2, budget.Remaining);
        Assert.Equal(0, budget.CacheHits);
        Assert.Equal(1234, budget.TokensSpent);
    }

    /// <summary>Provenance names the model, the requester and the prompt actually sent.</summary>
    [Fact]
    public async Task TestTheDeliveredAssetCarriesItsProvenance()
    {
        var generator = new FakeImageGenerator(TilingMaster(256));
        var toolkit = new AssetRequisitionToolkit(
            generator, new RequisitionCache(ImageGeneratorTests.TempCacheDir()), new AssetBudget(3), "Builder");

        var asset = await toolkit.Material("rusted corrugated iron", new MaterialOptions { Size = 128 });

        Assert.True(asset.Success, asset.Error);
        Assert.NotNull(asset.Provenance);
        Assert.Equal(FakeImageGenerator.FakeModel, asset.Provenance!.Model);
        Assert.Equal("Builder", asset.Provenance.Requester);
        Assert.False(asset.Provenance.FromCache);
        Assert.Equal(asset.Provenance.Hash, asset.Id);

        // The prompt is the elaborated one, not the descriptor: it is what left the machine.
        Assert.Contains("rusted corrugated iron", asset.Provenance.Prompt);
        Assert.Contains("FLAT TEXTURE SAMPLE", asset.Provenance.Prompt);

        // The library is what lets a second agent reuse rather than re-buy.
        Assert.Single(toolkit.Library);
        Assert.Equal(asset.Id, toolkit.Library[0].Id);
    }

    /// <summary>The same requisition again is served from cache, and the generator never sees it.</summary>
    [Fact]
    public async Task TestARepeatedRequisitionCostsNothing()
    {
        var budget = new AssetBudget(3);
        var generator = new FakeImageGenerator(TilingMaster(256));
        var toolkit = new AssetRequisitionToolkit(
            generator, new RequisitionCache(ImageGeneratorTests.TempCacheDir()), budget, "Framer");
        var options = new MaterialOptions { Size = 128 };

        var first = await toolkit.Material("brushed stainless steel", options);
        var again = await toolkit.Material("brushed stainless steel", options);

        Assert.True(first.Success, first.Error);
        Assert.True(again.Success, again.Error);

        Assert.Equal(1, generator.Calls);
        Assert.Equal(1, budget.Spent);
        Assert.Equal(1, budget.CacheHits);
        Assert.Equal(1234, budget.TokensSpent);
        Assert.Equal(first.Id, again.Id);
    }

    /// <summary>A swatch that does not wrap is repaired rather than re-asked for, and says so.</summary>
    /// <remarks>
    /// The model does not honour seamlessness and fails silently, so the guarantee is kept here or
    /// not at all. A full-range ramp is the clearest non-wrapping case: neighbour steps are tiny
    /// everywhere and the seam is the whole range.
    /// </remarks>
    [Fact]
    public async Task TestANonWrappingSwatchIsRepairedAndReportsIt()
    {
        var toolkit = new AssetRequisitionToolkit(
            new FakeImageGenerator(RampMaster(256)),
            new RequisitionCache(ImageGeneratorTests.TempCacheDir()), new AssetBudget(3), "Framer");

        var asset = await toolkit.Material("polished concrete", new MaterialOptions { Size = 128 });

        Assert.True(asset.Success, asset.Error);
        Assert.NotNull(asset.Tiling);
        Assert.True(asset.Tiling!.Repaired, "a swatch that did not wrap must be repaired, not delivered as-is");
        Assert.True(asset.Tiling.Wraps, "the delivered swatch must wrap once repaired");
    }

    /// <summary>Asking for the master as it came back leaves the seam alone.</summary>
    [Fact]
    public async Task TestTileableFalseLeavesTheSeamAlone()
    {
        var toolkit = new AssetRequisitionToolkit(
            new FakeImageGenerator(RampMaster(256)),
            new RequisitionCache(ImageGeneratorTests.TempCacheDir()), new AssetBudget(3), "Framer");

        var asset = await toolkit.Material(
            "polished concrete", new MaterialOptions { Size = 128, Tileable = false });

        Assert.True(asset.Success, asset.Error);
        Assert.False(asset.Tiling!.Repaired);
        Assert.False(asset.Tiling.Wraps);
    }

    /// <summary>
    /// The run record's success shape, from the code that writes it rather than from a hand-built record.
    /// </summary>
    /// <remarks>
    /// Previously only the failure and refusal shapes could be produced without spending. The cached
    /// second attempt is the part worth pinning: <c>fromCache</c> is read off the budget's hit count
    /// rather than off the result, because a cached result is a replay of a charged one and carries
    /// the charge flag with it.
    /// </remarks>
    [Fact]
    public async Task TestASuccessfulRequisitionIsRecordedAndTheCachedRepeatSaysSo()
    {
        var toolkit = new AssetRequisitionToolkit(
            new FakeImageGenerator(TilingMaster(256)),
            new RequisitionCache(ImageGeneratorTests.TempCacheDir()), new AssetBudget(3), "Framer");
        var options = new MaterialOptions { Size = 128 };

        using var scope = RequisitionScope.Begin();
        await toolkit.Material("weathered oak planking", options);
        await toolkit.Material("weathered oak planking", options);

        var records = scope.Records;
        Assert.Equal(2, records.Count);

        Assert.All(records, r =>
        {
            Assert.True(r.Success);
            Assert.Equal("material", r.Kind);
            Assert.Equal("weathered oak planking", r.Descriptor);
            Assert.False(r.Refused);
            Assert.Null(r.Failure);
            Assert.Null(r.Reason);
            Assert.Equal(FakeImageGenerator.FakeModel, r.Model);
        });

        Assert.False(records[0].FromCache);
        Assert.True(records[1].FromCache);

        // The snapshot is the allowance after the last requisition, pushed by the toolkit that ran.
        Assert.NotNull(scope.Budget);
        Assert.Equal(3, scope.Budget!.Total);
        Assert.Equal(1, scope.Budget.Spent);
        Assert.Equal(1, scope.Budget.CacheHits);
    }

    /// <summary>A retried cutout with a new pose and fewer variants is generated, not served the old sheet.</summary>
    /// <remarks>
    /// Found on the lastlight run, 2026-09-24. Each retry came back as a cache hit, so a request for
    /// three views was served the four-figure sheet split into three cells, and the budget did not
    /// move. The near-match compared whole prompts, which are mostly fixed wording, and dropped short
    /// words such as the variant count.
    /// </remarks>
    [Fact]
    public async Task TestARetriedCutoutIsNotServedTheEarlierSheet()
    {
        var budget = new AssetBudget(3);
        var generator = new FakeImageGenerator(CutoutSheet());
        var toolkit = new AssetRequisitionToolkit(
            generator, new RequisitionCache(ImageGeneratorTests.TempCacheDir()), budget, "Framer");
        const string subject = "a heavyset keeper in his sixties, grey beard, oilskin coat, full figure standing in an A-pose, ";

        var first = await toolkit.Cutout(subject + "arms held well away from the body", new CutoutOptions
        {
            Variants = ["front view", "back view", "left side view", "right side view"],
        });
        var retry = await toolkit.Cutout(subject + "both arms angled down and out at 45 degrees", new CutoutOptions
        {
            Variants = ["front view", "back view", "left side view"],
        });

        Assert.True(first.Success, first.Error);
        Assert.True(retry.Success, retry.Error);
        Assert.Equal(2, generator.Calls);
        Assert.Equal(2, budget.Spent);
        Assert.Equal(0, budget.CacheHits);
    }

    /// <summary>A reference sends the sheet the studio generated, and says so in the prompt.</summary>
    /// <remarks>
    /// The bytes sent are read from the cache by id, not off the object passed: a cutout whose bytes had
    /// been swapped for a photograph still sends only what the studio generated under that id.
    /// </remarks>
    [Fact]
    public async Task TestAReferenceSendsTheGeneratedSheet()
    {
        var budget = new AssetBudget(3);
        var sheet = CutoutSheet();
        var generator = new FakeImageGenerator(sheet);
        var toolkit = new AssetRequisitionToolkit(
            generator, new RequisitionCache(ImageGeneratorTests.TempCacheDir()), budget, "Framer");

        var first = await toolkit.Cutout("a heavyset keeper, full figure", new CutoutOptions { Variants = ["front view", "back view"] });
        var tampered = first with { Bytes = [1, 2, 3] };
        var again = await toolkit.Cutout("a heavyset keeper, full figure", new CutoutOptions
        {
            Variants = ["side view, in profile"],
            Reference = tampered,
        });

        Assert.True(again.Success, again.Error);
        Assert.Equal(2, generator.Calls);
        Assert.Empty(generator.Shown[0]);
        Assert.Equal(sheet, Assert.Single(generator.Shown[1]));
        Assert.Contains("attached image", generator.Prompts[1]);
        Assert.DoesNotContain("attached image", generator.Prompts[0]);
        Assert.Equal([first.Id], again.References);
        Assert.NotEqual(first.Id, again.Id);
        Assert.Equal(2, budget.Spent);
    }

    /// <summary>A cell, an id and an array all name the same sheet, which is sent once.</summary>
    [Fact]
    public async Task TestACellAnIdAndAnArrayAllResolveToTheSheet()
    {
        var generator = new FakeImageGenerator(CutoutSheet());
        var toolkit = new AssetRequisitionToolkit(
            generator, new RequisitionCache(ImageGeneratorTests.TempCacheDir()), new AssetBudget(5), "Framer");
        var first = await toolkit.Cutout("a heavyset keeper, full figure", new CutoutOptions { Variants = ["front view", "back view"] });

        Assert.All(first.Cells, c => Assert.Equal(first.Id, c.SheetId));

        foreach (var reference in new object[] { first.Cells[1], first.Id, new object[] { first, first.Cells[0] } })
        {
            var (ids, error) = AssetRequisitionToolkit.ReferenceIdsOf(reference);
            Assert.Null(error);
            Assert.Equal([first.Id], ids);
        }
    }

    /// <summary>Anything but a cutout this project generated is refused before anything is spent.</summary>
    /// <remarks>
    /// A reference is the route by which an image reaches the model as the subject to copy, so a
    /// photograph or a canvas passed here would carry a likeness past every check Photo applies.
    /// </remarks>
    [Fact]
    public async Task TestAReferenceThatIsNotAGeneratedCutoutIsRefused()
    {
        var budget = new AssetBudget(3);
        var generator = new FakeImageGenerator(CutoutSheet());
        var toolkit = new AssetRequisitionToolkit(
            generator, new RequisitionCache(ImageGeneratorTests.TempCacheDir()), budget, "Framer");

        foreach (var reference in new object[] { "NOTAGENERATEDSHEET", new byte[] { 1, 2, 3 }, new CutoutAsset { Success = false } })
        {
            var refused = await toolkit.Cutout("a heavyset keeper, full figure", new CutoutOptions { Reference = reference });

            Assert.False(refused.Success);
            Assert.Equal(ImageGenerationFailure.InvalidRequest, refused.Failure);
        }

        Assert.Equal(0, generator.Calls);
        Assert.Equal(0, budget.Spent);
    }

    /// <summary>More than three sheets is refused rather than truncated.</summary>
    [Fact]
    public void TestMoreThanThreeReferencesAreRefused()
    {
        var (ids, error) = AssetRequisitionToolkit.ReferenceIdsOf(new object[] { "A", "B", "C", "D" });

        Assert.Empty(ids);
        Assert.NotNull(error);
    }

    /// <summary>A material reworded with the same words is still one material, and bills once.</summary>
    [Fact]
    public async Task TestARewordedMaterialIsServedFromCache()
    {
        var budget = new AssetBudget(3);
        var generator = new FakeImageGenerator(TilingMaster(256));
        var toolkit = new AssetRequisitionToolkit(
            generator, new RequisitionCache(ImageGeneratorTests.TempCacheDir()), budget, "Framer");
        var options = new MaterialOptions { Size = 128 };

        await toolkit.Material("weathered oak planking with dark caulked seams", options);
        var again = await toolkit.Material("Weathered oak planking, dark caulked seams.", options);

        Assert.True(again.Success, again.Error);
        Assert.Equal(1, generator.Calls);
        Assert.Equal(1, budget.CacheHits);
    }

    /// <summary>Two materials differing by one short word are two materials.</summary>
    /// <remarks>
    /// The tokeniser dropped every word of three letters or fewer, so <c>oak</c> and <c>elm</c> both
    /// vanished and the second request was served the first swatch.
    /// </remarks>
    [Fact]
    public async Task TestMaterialsDifferingByAShortWordAreBothGenerated()
    {
        var generator = new FakeImageGenerator(TilingMaster(256));
        var toolkit = new AssetRequisitionToolkit(
            generator, new RequisitionCache(ImageGeneratorTests.TempCacheDir()), new AssetBudget(3), "Framer");
        var options = new MaterialOptions { Size = 128 };

        await toolkit.Material("weathered oak planking", options);
        await toolkit.Material("weathered elm planking", options);

        Assert.Equal(2, generator.Calls);
    }
    #endregion

    #region Helpers
    /// <summary>A magenta sheet carrying four grey figures with clear gaps between them.</summary>
    static byte[] CutoutSheet()
    {
        using var bitmap = new SKBitmap(400, 200);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(0xFF, 0x00, 0xFF));
            using var paint = new SKPaint { Color = new SKColor(128, 128, 128) };
            for (var i = 0; i < 4; i++)
            {
                canvas.DrawRect(SKRect.Create(20 + i * 100, 30, 60, 140), paint);
            }
        }

        return Encode(bitmap);
    }

    /// <summary>A master that already wraps: both channels are periodic within the frame.</summary>
    static byte[] TilingMaster(int size)
    {
        using var bitmap = new SKBitmap(size, size);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                bitmap.SetPixel(x, y, new SKColor(
                    (byte)(128 + 60 * Math.Sin(2 * Math.PI * x / size)),
                    (byte)(128 + 60 * Math.Sin(2 * Math.PI * y / size)),
                    128));
            }
        }

        return Encode(bitmap);
    }

    /// <summary>A master that cannot wrap: a full-range ramp meets its own start at the seam.</summary>
    static byte[] RampMaster(int size)
    {
        using var bitmap = new SKBitmap(size, size);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                bitmap.SetPixel(x, y, new SKColor(
                    (byte)(x * 255 / (size - 1)), (byte)(y * 255 / (size - 1)), 90));
            }
        }

        return Encode(bitmap);
    }

    static byte[] Encode(SKBitmap bitmap)
    {
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
    #endregion

    #region Types
    /// <summary>A generator that returns fixed bytes and counts how often it was asked.</summary>
    /// <remarks>
    /// Deliberately not a mocking framework: what is under test is the contract in
    /// <see cref="IImageGenerator"/>, and a hand-written fake is the kind that fails to compile when
    /// the contract changes.
    /// </remarks>
    sealed class FakeImageGenerator : IImageGenerator
    {
        public const string FakeModel = "fake-image-model";

        public FakeImageGenerator(byte[] png) => this.png = png;

        public string Model => FakeModel;

        /// <summary>How many times the transport was actually reached.</summary>
        public int Calls => calls;

        /// <summary>The prompts that reached the transport, in order.</summary>
        public IReadOnlyList<string> Prompts => prompts;

        /// <summary>The images each call was shown, in order; empty for a call shown none.</summary>
        public IReadOnlyList<IReadOnlyList<byte[]>> Shown => shown;

        public Task<ImageGenerationResult> GenerateImage(
            string prompt,
            string? model = null,
            string? aspectRatio = null,
            IReadOnlyList<byte[]>? conditionOn = null,
            CancellationToken ct = default)
        {
            Interlocked.Increment(ref calls);
            lock (prompts) { prompts.Add(prompt); shown.Add(conditionOn ?? []); }

            var useModel = model ?? FakeModel;
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
                PromptTokens = 34,
                OutputTokens = 1200,
                TotalTokens = 1234,
            });
        }

        readonly byte[] png;
        readonly List<string> prompts = [];
        readonly List<IReadOnlyList<byte[]>> shown = [];
        int calls;
    }
    #endregion
}
