namespace Polson.Tests.ExtendedMind;

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using global::Polson.ExtendedMind;
using global::Polson.Tests;

using Xunit;

/// <summary>
/// Tests for <see cref="ImageGenerator"/> and the requisition surface above it.
/// </summary>
/// <remarks>
/// Everything here is free to run. The failure-path tests reach the live service but never produce
/// an image, and the classification and budget tests refuse before any network call at all. The one
/// test that would actually bill is gated behind <c>POLSON_LIVE_IMAGE_TESTS=1</c>.
/// </remarks>
public class ImageGeneratorTests : TestsRuntime
{
    #region Constructors
    public ImageGeneratorTests()
    {
        this.apiKey = config["ApiKeys:GoogleAgentPlatform"];
    }
    #endregion

    #region Construction and auth
    [Fact]
    public void Constructor_RejectsMissingKey()
    {
        Assert.Throws<ArgumentException>(() => new ImageGenerator(string.Empty));
        Assert.Throws<ArgumentException>(() => new ImageGenerator("   "));
    }

    [Fact]
    public void Constructor_ExpressMode_AcceptsBareApiKey()
    {
        // Express mode is apiKey + enterprise:true and no project/location. Passing project/location
        // alongside a key without that flag is rejected by the SDK, so this combination is the one
        // that has to work; a regression here breaks every requisition.
        using var generator = new ImageGenerator("dummy-key-not-used-offline");
        Assert.Equal(ImageGenerator.DefaultModel, generator.Model);
    }

    [Fact]
    public void DefaultModels_AreTheVerifiedNames()
    {
        Assert.Equal("gemini-2.5-flash-image", ImageGenerator.DefaultModel);
        Assert.Equal("gemini-3-pro-image", ImageGenerator.ProModel);
    }
    #endregion

    #region Failure classification
    [Fact]
    public async Task Generate_EmptyPrompt_FailsWithoutNetwork()
    {
        using var generator = new ImageGenerator("dummy-key-not-used-offline");

        var result = await generator.Generate("   ");

        Assert.False(result.Success);
        Assert.Equal(ImageGenerationFailure.InvalidRequest, result.Failure);
        Assert.False(result.Charged);
        Assert.Null(result.ImageBytes);
    }

    [Fact]
    public async Task Generate_BadApiKey_ReportsAuthAndDoesNotCharge()
    {
        using var generator = new ImageGenerator("AQ.definitely-not-a-valid-key");

        var result = await generator.Generate("flat oak grain swatch");

        Assert.False(result.Success);
        Assert.Equal(ImageGenerationFailure.Auth, result.Failure);
        Assert.False(result.Retryable);
        Assert.False(result.Charged);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task Generate_UnknownModel_ReportsModelNotFound()
    {
        if (string.IsNullOrWhiteSpace(this.apiKey)) return;   // no key configured: nothing to exercise

        using var generator = new ImageGenerator(this.apiKey!);

        var result = await generator.Generate("flat oak grain swatch", "gemini-9-imaginary-image");

        Assert.False(result.Success);
        Assert.Equal(ImageGenerationFailure.ModelNotFound, result.Failure);
        Assert.Equal(404, result.StatusCode);
        Assert.False(result.Charged);
    }

    [Fact]
    public async Task Generate_Cancelled_ReportsCancelled()
    {
        if (string.IsNullOrWhiteSpace(this.apiKey)) return;   // no key configured: nothing to exercise

        using var generator = new ImageGenerator(this.apiKey!);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await generator.GenerateImage("flat oak grain swatch", null, null, null, cts.Token);

        Assert.False(result.Success);
        Assert.Equal(ImageGenerationFailure.Cancelled, result.Failure);
        Assert.False(result.Charged);
    }

    [Theory]
    [InlineData(ImageGenerationFailure.RateLimited, true)]
    [InlineData(ImageGenerationFailure.ServiceError, true)]
    [InlineData(ImageGenerationFailure.Network, true)]
    [InlineData(ImageGenerationFailure.Timeout, true)]
    [InlineData(ImageGenerationFailure.Auth, false)]
    [InlineData(ImageGenerationFailure.SafetyBlocked, false)]
    [InlineData(ImageGenerationFailure.Quota, false)]
    [InlineData(ImageGenerationFailure.BudgetExhausted, false)]
    public void Retryable_SeparatesTransientFromTerminal(ImageGenerationFailure failure, bool expected) =>
        Assert.Equal(expected, ImageGenerationResult.IsRetryable(failure));

    [Fact]
    public void RemedyFor_IsNonEmptyForEveryFailure()
    {
        // The remedy is what an agent acts on; a blank one leaves it unable to choose between
        // retrying, rewording, and giving up.
        foreach (var failure in Enum.GetValues<ImageGenerationFailure>())
        {
            Assert.False(string.IsNullOrWhiteSpace(ImageGenerationResult.RemedyFor(failure)));
        }
    }
    #endregion

    #region Payload handling
    [Fact]
    public void TryReadPngSize_ReadsValidHeader()
    {
        var png = PlateAnalysis.Encode(SyntheticBitmap(64, 48), "png", 100);

        Assert.True(ImageGenerator.TryReadPngSize(png, out var w, out var h));
        Assert.Equal(64, w);
        Assert.Equal(48, h);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(23)]
    public void TryReadPngSize_RejectsTruncatedPayload(int length)
    {
        // Untrusted binary off the network. A short read must fail rather than yield nonsense
        // dimensions that only surface later as a broken draw.
        Assert.False(ImageGenerator.TryReadPngSize(new byte[length], out _, out _));
    }

    [Fact]
    public void TryReadPngSize_RejectsNonPng()
    {
        var notPng = new byte[64];
        Array.Fill(notPng, (byte)0x42);
        Assert.False(ImageGenerator.TryReadPngSize(notPng, out _, out _));
    }
    #endregion

    #region Content addressing
    [Fact]
    public void HashOf_IsStableForIdenticalRequests() => Assert.Equal(
        ImageGenerator.HashOf("m", "oak planking", "1:1", null),
        ImageGenerator.HashOf("m", "oak planking", "1:1", null));

    [Fact]
    public void HashOf_VariesWithEveryComponent()
    {
        var baseline = ImageGenerator.HashOf("m", "oak planking", "1:1", null);

        Assert.NotEqual(baseline, ImageGenerator.HashOf("other", "oak planking", "1:1", null));
        Assert.NotEqual(baseline, ImageGenerator.HashOf("m", "pine planking", "1:1", null));
        Assert.NotEqual(baseline, ImageGenerator.HashOf("m", "oak planking", "16:9", null));
        Assert.NotEqual(baseline, ImageGenerator.HashOf("m", "oak planking", "1:1", [new byte[] { 1, 2, 3 }]));
    }

    [Fact]
    public void HashOf_DistinguishesDifferentConditioningImages() => Assert.NotEqual(
        ImageGenerator.HashOf("m", "sky", "16:9", [new byte[] { 1, 2, 3 }]),
        ImageGenerator.HashOf("m", "sky", "16:9", [new byte[] { 3, 2, 1 }]));
    #endregion

    #region Requisition gatekeeping
    [Theory]
    [InlineData("weathered wooden ship hull planking", RequisitionClass.Substance)]
    [InlineData("ship hull planking", RequisitionClass.Substance)]
    [InlineData("salt-bleached oak grain", RequisitionClass.Substance)]
    [InlineData("brushed copper surface", RequisitionClass.Substance)]
    [InlineData("a seamless wood texture", RequisitionClass.Substance)]
    [InlineData("a wooden ship", RequisitionClass.Form)]
    [InlineData("a pirate ship at sunset", RequisitionClass.Form)]
    [InlineData("a ship made of oak", RequisitionClass.Form)]
    [InlineData("the company logo", RequisitionClass.Form)]
    [InlineData("a dragon", RequisitionClass.Form)]
    public void Classify_SeparatesSubstanceFromForm(string descriptor, RequisitionClass expected) =>
        Assert.Equal(expected, AssetRequisitionToolkit.Classify(descriptor).Class);

    [Fact]
    public void Classify_AllowsAFormNounUsedAttributively()
    {
        // Regression: this is the exact descriptor that produced a good swatch, and a naive
        // contains-"ship" rule refused it. English puts the head noun last.
        var verdict = AssetRequisitionToolkit.Classify("weathered wooden ship hull planking");

        Assert.True(verdict.Allowed);
        Assert.Equal(RequisitionClass.Substance, verdict.Class);
    }

    [Fact]
    public async Task Material_RefusesAFormRequestWithoutSpendingAnything()
    {
        var budget = new AssetBudget(5);
        using var generator = new ImageGenerator("dummy-key-not-used-offline");
        var toolkit = new AssetRequisitionToolkit(generator, new RequisitionCache(TempCacheDir()), budget, "Framer");

        var asset = await toolkit.Material("a wooden ship");

        Assert.False(asset.Success);
        Assert.Equal(ImageGenerationFailure.RefusedFormRequest, asset.Failure);
        Assert.Equal(0, budget.Spent);
        Assert.Empty(asset.Bytes);
        Assert.Contains("drawing toolkit", asset.Remedy);
    }

    [Fact]
    public async Task Material_ReportsBudgetExhaustionWithoutCallingTheService()
    {
        var budget = new AssetBudget(0);
        using var generator = new ImageGenerator("dummy-key-not-used-offline");
        var toolkit = new AssetRequisitionToolkit(generator, new RequisitionCache(TempCacheDir()), budget, "Builder");

        var asset = await toolkit.Material("weathered oak planking");

        Assert.False(asset.Success);
        Assert.Equal(ImageGenerationFailure.BudgetExhausted, asset.Failure);
        Assert.Equal(0, budget.Spent);
    }

    [Fact]
    public void Budget_ReportsAffordabilityFromItsTotal()
    {
        Assert.True(new AssetBudget(2).CanAfford());
        Assert.True(new AssetBudget(2).CanAfford(2));
        Assert.False(new AssetBudget(2).CanAfford(3));
        Assert.False(new AssetBudget(0).CanAfford());
        Assert.Equal(2, new AssetBudget(2).Remaining);
    }

    [Fact]
    public void Budget_SpendIsNotWritableFromOutside()
    {
        // Deliberate: Spent has an internal setter. A script that could zero its own allowance would
        // make the budget advisory, and the whole point is that it is not.
        var setter = typeof(AssetBudget).GetProperty(nameof(AssetBudget.Spent))!.SetMethod;

        Assert.False(setter?.IsPublic ?? false);
    }
    #endregion

    #region Cache
    [Fact]
    public async Task Cache_RoundTripsAMasterAndMarksItUncharged()
    {
        var cache = new RequisitionCache(TempCacheDir());
        var stored = new ImageGenerationResult
        {
            Success = true,
            ImageBytes = PlateAnalysis.Encode(SyntheticBitmap(32, 32), "png", 100),
            Width = 32,
            Height = 32,
            Model = "m",
            Prompt = "oak planking",
            Hash = "CACHETEST0001",
            Charged = true,
        };

        await cache.Put(stored);
        var loaded = await cache.Get("CACHETEST0001");

        Assert.NotNull(loaded);
        Assert.True(loaded!.Success);
        Assert.True(loaded.FromCache);

        // A cache hit must not be charged, or the retry loop this exists to make cheap stays costly.
        Assert.False(loaded.Charged);
        Assert.Equal(32, loaded.Width);
    }

    [Fact]
    public async Task Cache_MissReturnsNull() =>
        Assert.Null(await new RequisitionCache(TempCacheDir()).Get("NOTHINGSTOREDHERE"));

    [Fact]
    public async Task Cache_CorruptedEntryMissesRatherThanThrowing()
    {
        var dir = TempCacheDir();
        var cache = new RequisitionCache(dir);
        await File.WriteAllBytesAsync(Path.Combine(dir, "BADENTRY.png"), [0x00, 0x01, 0x02]);
        await File.WriteAllTextAsync(Path.Combine(dir, "BADENTRY.json"), "{\"Model\":\"m\",\"Prompt\":\"p\"}");

        Assert.Null(await cache.Get("BADENTRY"));
    }

    [Fact]
    public async Task Cache_DoesNotStoreFailures()
    {
        var dir = TempCacheDir();
        var cache = new RequisitionCache(dir);

        await cache.Put(ImageGenerationResult.Failed(
            ImageGenerationFailure.Network, "no route", "oak", "m"));

        Assert.Empty(Directory.EnumerateFiles(dir));
    }
    #endregion

    #region Live
    [Fact]
    public async Task Generate_Live_ProducesA1024Master()
    {
        if (string.IsNullOrWhiteSpace(this.apiKey)) return;   // no key configured: nothing to exercise
        if (Environment.GetEnvironmentVariable("POLSON_LIVE_IMAGE_TESTS") != "1") return;   // opt-in: this one bills

        using var generator = new ImageGenerator(this.apiKey!);

        // Deliberately specific. "plain grey concrete" was declined live with IMAGE_RECITATION: a
        // descriptor generic enough to match existing material gets refused rather than generated.
        var result = await generator.Generate(
            "A flat swatch of pale sea-green weathered concrete, fine hairline cracks, faint rust "
            + "staining at the lower left, flat even lighting, no objects, fills the frame.");

        Assert.True(result.Success, result.Error);
        Assert.True(result.Charged);
        Assert.NotNull(result.ImageBytes);

        // Output size is not controllable: 1K is the floor and the prompt cannot move it.
        Assert.Equal(1024, result.Width);
        Assert.Equal(1024, result.Height);
        Assert.StartsWith("data:image/", result.ImageDataUri);

        // Token counts come from the service, not from an estimate.
        Assert.NotNull(result.TotalTokens);
        Assert.True(result.TotalTokens > 0);

        Info("live generation: {W}x{H}, {Ms} ms, {Prompt} prompt + {Out} output = {Total} tokens",
            result.Width, result.Height, result.ElapsedMs,
            result.PromptTokens ?? 0, result.OutputTokens ?? 0, result.TotalTokens ?? 0);
    }

    [Fact]
    public async Task Material_Live_RunsTheWholePipeline()
    {
        if (string.IsNullOrWhiteSpace(this.apiKey)) return;   // no key configured: nothing to exercise
        if (Environment.GetEnvironmentVariable("POLSON_LIVE_IMAGE_TESTS") != "1") return;   // opt-in: this one bills

        var budget = new AssetBudget(4);
        using var generator = new ImageGenerator(this.apiKey!);
        var cache = new RequisitionCache(TempCacheDir());
        var toolkit = new AssetRequisitionToolkit(generator, cache, budget, "Framer");

        var asset = await toolkit.Material("weathered oak planking with dark caulked seams");

        Assert.True(asset.Success, asset.Error);
        Assert.Equal(512, asset.Size);
        Assert.Equal("image/webp", asset.MimeType);
        Assert.NotNull(asset.Tiling);
        Assert.True(asset.Tiling!.Wraps, "the delivered swatch must wrap, repaired if it did not");
        Assert.Equal(1, budget.Spent);
        Assert.True(budget.TokensSpent > 0);

        // Delivered payload must be small enough to sit in an agent's context.
        Assert.True(asset.Bytes.Length < 120_000, $"delivered {asset.Bytes.Length} bytes");
        Assert.Single(toolkit.Library);

        Info("live material: {Bytes} bytes, tiling h={H:F2} v={V:F2} max={Max:F2} repaired={R}, {Tokens} tokens",
            asset.Bytes.Length, asset.Tiling.HorizontalSeamStep, asset.Tiling.VerticalSeamStep,
            asset.Tiling.NeighbourMax, asset.Tiling.Repaired, budget.TokensSpent);

        // The same requisition again must come from cache and cost nothing.
        var again = await toolkit.Material("weathered oak planking with dark caulked seams");

        Assert.True(again.Success);
        Assert.Equal(1, budget.Spent);
        Assert.Equal(1, budget.CacheHits);
    }
    #endregion

    #region Helpers
    internal static SkiaSharp.SKBitmap SyntheticBitmap(int width, int height)
    {
        var bitmap = new SkiaSharp.SKBitmap(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                bitmap.SetPixel(x, y, new SkiaSharp.SKColor((byte)(x * 4 % 256), (byte)(y * 4 % 256), 128));
            }
        }

        return bitmap;
    }

    internal static string TempCacheDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "polson-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
    #endregion

    #region Fields
    readonly string? apiKey;
    #endregion
}
