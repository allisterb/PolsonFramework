namespace Polson.Tests.ExtendedMind;

using System;
using System.Text.Json;
using System.Threading.Tasks;

using SkiaSharp;

using global::Polson.Tests;
using Polson.ExtendedMind.ObjectGeneration;
using Xunit;
using Xunit.Abstractions;

/// <summary>The TRELLIS client: its bounds, its parsing, and one live generation when a box is up.</summary>
/// <remarks>
/// <b>Everything except the last test runs with no endpoint at all</b>, which is the point — the
/// validation, the serialization rule and the classification are ours and must be checkable without a
/// 16 GB GPU. The live test is gated on <c>Trellis:BaseUrl</c> answering <c>/v1/health/ready</c> and
/// says so rather than passing silently.
/// </remarks>
public class TrellisClientTests : TestsRuntime
{
    #region Fields
    readonly ITestOutputHelper output;
    readonly string? baseUrl;
    #endregion

    #region Constructors
    public TrellisClientTests(ITestOutputHelper output)
    {
        this.output = output;
        this.baseUrl = config["Trellis:BaseUrl"];
    }
    #endregion

    #region Serialization
    /// <summary>A null field must not be written. This is the one that breaks generation.</summary>
    /// <remarks>
    /// The service refuses a body carrying both a prompt and an image — and reads
    /// <c>"image": null</c> as carrying one, answering 422 <i>"Multiple prompt fields are detected"</i>.
    /// So a serializer that writes nulls makes every text prompt fail while naming a field the
    /// caller never set. Observed against the live service before this rule existed.
    /// </remarks>
    [Fact]
    public void Serialization_OmitsNullFields()
    {
        var json = JsonSerializer.Serialize(
            new TrellisRequest { Prompt = "a teapot" }, TrellisClient.SerializerOptions);

        Assert.DoesNotContain("\"image\"", json);
        Assert.DoesNotContain("null", json);
        Assert.Contains("\"prompt\":\"a teapot\"", json);
        output.WriteLine(json);
    }

    /// <summary>The wire names are the service's, not ours.</summary>
    [Fact]
    public void Serialization_UsesTheServicesFieldNames()
    {
        var json = JsonSerializer.Serialize(
            new TrellisRequest { Prompt = "x", NoTexture = true, StructureCfgScale = 7.5 },
            TrellisClient.SerializerOptions);

        foreach (var name in new[]
        {
            "ss_sampling_steps", "slat_sampling_steps", "no_texture", "output_format", "ss_cfg_scale"
        })
        {
            Assert.Contains($"\"{name}\"", json);
        }
    }

    /// <summary>An unspecified generation must be reproducible.</summary>
    /// <remarks>
    /// <c>seed: 0</c> means <b>random</b> — the schema says "Omit this option or use 0 for a random
    /// seed". A studio whose record rests on a saved script re-rendering cannot default to that, so
    /// the default is non-zero and this pins it.
    /// </remarks>
    [Fact]
    public void DefaultSeed_IsNotTheRandomSentinel()
    {
        Assert.NotEqual(TrellisClient.RandomSeed, TrellisClient.DefaultSeed);
        Assert.Equal(TrellisClient.DefaultSeed, new TrellisRequest { Prompt = "x" }.Seed);
    }
    #endregion

    #region Validation
    [Theory]
    [InlineData(9)]
    [InlineData(51)]
    [InlineData(0)]
    public void Validate_RefusesStepsOutsideTheServicesRange(int steps)
    {
        var result = TrellisClient.Validate(
            new TrellisRequest { Prompt = "a teapot", StructureSteps = steps });

        Assert.NotNull(result);
        Assert.Equal(TrellisFailure.InvalidRequest, result!.Failure);
        Assert.False(result.Retryable);
        Assert.Contains("10", result.Remedy);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(10.5)]
    [InlineData(0.5)]
    public void Validate_RefusesCfgScaleOutsideTheServicesRange(double scale)
    {
        var result = TrellisClient.Validate(
            new TrellisRequest { Prompt = "a teapot", LatentCfgScale = scale });

        Assert.NotNull(result);
        Assert.Equal(TrellisFailure.InvalidRequest, result!.Failure);
    }

    [Fact]
    public void Validate_RefusesBothPromptAndImage()
    {
        var result = TrellisClient.Validate(
            new TrellisRequest { Prompt = "a teapot", Image = "data:image/png;base64,AAAA" });

        Assert.NotNull(result);
        Assert.Contains("not both", result!.Remedy);
    }

    [Fact]
    public void Validate_RefusesNeitherPromptNorImage()
    {
        Assert.NotNull(TrellisClient.Validate(new TrellisRequest()));
    }

    [Fact]
    public void Validate_RefusesSamplesOtherThanOne() =>
        Assert.NotNull(TrellisClient.Validate(new TrellisRequest { Prompt = "x", Samples = 2 }));

    [Fact]
    public void Validate_RefusesAnUnknownOutputFormat() =>
        Assert.NotNull(TrellisClient.Validate(new TrellisRequest { Prompt = "x", OutputFormat = "obj" }));

    /// <summary>The ranges the service accepts must pass ours.</summary>
    [Fact]
    public void Validate_AcceptsTheServicesOwnBounds()
    {
        Assert.Null(TrellisClient.Validate(new TrellisRequest
        {
            Prompt = "a teapot",
            StructureSteps = TrellisClient.MinSamplingSteps,
            LatentSteps = TrellisClient.MaxSamplingSteps,
            StructureCfgScale = TrellisClient.MaxCfgScale,
            LatentCfgScale = 1.0001,
            Seed = TrellisClient.RandomSeed,
            OutputFormat = "stl"
        }));
    }
    #endregion

    #region Live
    /// <summary>One real generation, whichever mode the loaded variant accepts.</summary>
    /// <remarks>
    /// <b>The accepted <c>mode</c> is narrowed by the loaded model variant, at runtime.</b> The
    /// container's own <c>openapi.json</c> declares <c>mode</c> as <c>text | image</c> — but that
    /// spec was served by a <c>base:text</c> container, and a <c>large:image</c> one refuses
    /// <c>"text"</c> outright: <c>{"loc":["body","mode"],"msg":"Input should be 'image'"}</c>.
    /// <para>
    /// That follows from the model rather than the API. TRELLIS conditions text through CLIP and
    /// images through DINOv2 (arXiv 2412.01506v3 §3.3), so the variants are genuinely different
    /// models and <c>base:text</c> has no image encoder to hand a picture to. A test that hardcodes
    /// one modality therefore passes or fails on which variant happens to be loaded — this one
    /// asserts only that <i>some</i> modality works, and reports which.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Generates_AGlbInWhicheverModeTheVariantAccepts()
    {
        if (string.IsNullOrWhiteSpace(this.baseUrl))
        {
            output.WriteLine("NOT RUN: set 'Trellis:BaseUrl' to a NIM container to exercise this.");
            return;
        }

        using var client = new TrellisClient(this.baseUrl, config["ApiKeys:NvidiaNIM"]);

        if (!await client.IsReadyAsync())
        {
            output.WriteLine($"NOT RUN: {this.baseUrl} did not answer /v1/health/ready.");
            return;
        }

        TrellisRequest[] candidates =
        [
            new() { Mode = "image", Image = "data:image/png;base64," + Convert.ToBase64String(Shape()),
                    NoTexture = true, Seed = 42 },
            new() { Mode = "text", Prompt = "a small ceramic teapot", NoTexture = true, Seed = 42 }
        ];

        TrellisResult? last = null;
        foreach (var request in candidates)
        {
            last = await client.GenerateAsync(request, attempts: 2);
            if (last.Success) break;

            //: A refusal naming `mode` means the wrong variant is loaded, not a bad request — so
            //: try the other modality rather than failing the run.
            if (last.Failure != TrellisFailure.InvalidRequest || last.Error?.Contains("mode") != true)
            {
                break;
            }

            output.WriteLine($"{request.Mode}: refused by this variant, trying the other");
        }

        Assert.NotNull(last);
        Assert.True(last!.Success, $"{last.FailureName}: {last.Remedy} {last.Error}");
        Assert.Equal(TrellisFinishReason.Success, last.FinishReason);
        Assert.Equal(42, last.Seed);

        //: glTF's magic, so this asserts a model came back rather than merely some bytes.
        Assert.True(last.Bytes.Length > 1000, $"only {last.Bytes.Length} bytes");
        Assert.Equal("glTF"u8.ToArray(), last.Bytes[..4]);

        output.WriteLine($"{last.Bytes.Length} bytes in {last.Ms} ms, {last.Attempts} attempt(s)");
    }

    /// <summary>A plain solid on white — enough for an image prompt to have something to read.</summary>
    static byte[] Shape()
    {
        using var bitmap = new SKBitmap(320, 320);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = new SKColor(0x8a, 0x5c, 0x34), IsAntialias = true };
            canvas.DrawOval(new SKRect(70, 40, 250, 280), paint);
        }

        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>An endpoint that is not there is a result, not an exception.</summary>
    [Fact]
    public async Task Generate_ReportsAnUnreachableEndpointRatherThanThrowing()
    {
        using var client = new TrellisClient("http://127.0.0.1:9");

        var result = await client.GenerateAsync(
            new TrellisRequest { Prompt = "a teapot" }, timeout: TimeSpan.FromSeconds(5));

        Assert.False(result.Success);
        Assert.Equal(TrellisFailure.Network, result.Failure);
        Assert.True(result.Retryable);
        Assert.Contains("Trellis:BaseUrl", result.Remedy);
    }

    [Fact]
    public async Task IsReady_IsFalseForAnUnreachableEndpoint() =>
        Assert.False(await new TrellisClient("http://127.0.0.1:9").IsReadyAsync());
    #endregion
}
