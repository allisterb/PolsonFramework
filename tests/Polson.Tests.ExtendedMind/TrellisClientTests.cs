namespace Polson.Tests.ExtendedMind;

using System;
using System.Text.Json;
using System.Threading.Tasks;

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
    /// <summary>One real generation, if an endpoint is configured and warmed.</summary>
    [Fact]
    public async Task Generates_AGlbFromAPrompt()
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

        var result = await client.GenerateAsync(
            new TrellisRequest { Prompt = "a small ceramic teapot", NoTexture = true, Seed = 42 },
            attempts: 3);

        Assert.True(result.Success, $"{result.FailureName}: {result.Remedy} {result.Error}");
        Assert.Equal(TrellisFinishReason.Success, result.FinishReason);
        Assert.Equal(42, result.Seed);

        //: glTF's magic, so this asserts a model came back rather than merely some bytes.
        Assert.True(result.Bytes.Length > 1000, $"only {result.Bytes.Length} bytes");
        Assert.Equal("glTF"u8.ToArray(), result.Bytes[..4]);

        output.WriteLine($"{result.Bytes.Length} bytes in {result.Ms} ms, {result.Attempts} attempt(s)");
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
