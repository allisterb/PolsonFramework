namespace Polson.Tests.ExtendedMind;

using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
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

    /// <summary>The <c>Object3DRequest</c> fragment a <c>large:image</c> container really serves.</summary>
    /// <remarks>
    /// Trimmed to the constrained fields and copied from the live schema rather than invented, so the
    /// shape under test is the shape that arrives: every optional field is an <c>anyOf</c> against
    /// <c>null</c>, and the <c>const</c> or <c>enum</c> rides on the non-null branch.
    /// </remarks>
    const string ImageOnlySchema = """
        {"components":{"schemas":{"Object3DRequest":{"properties":{
          "mode":{"anyOf":[{"type":"string","const":"image"},{"type":"null"}]},
          "prompt":{"anyOf":[{"type":"string"},{"type":"null"}]},
          "image":{"anyOf":[{"type":"string"},{"items":{"type":"string"},"type":"array"},{"type":"null"}]},
          "multiimage_algo":{"anyOf":[{"type":"string","enum":["stochastic","multidiffusion"]},{"type":"null"}],"default":"stochastic"},
          "samples":{"anyOf":[{"type":"integer","maximum":1.0,"minimum":1.0},{"type":"null"}],"default":1},
          "output_format":{"anyOf":[{"type":"string","enum":["glb","stl"]},{"type":"null"}],"default":"glb"}
        }}}}}
        """;
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

    #region Capabilities
    /// <summary>The accepted mode is read from the schema, where it is a <c>const</c> in an union.</summary>
    /// <remarks>
    /// <b>The constraint sits INSIDE the <c>anyOf</c>, not beside it</b>, because every optional field
    /// is a union with null. Reading only the top level reports the field as unconstrained — a wrong
    /// answer that looks like a permissive one, and the mistake made while first reading this schema
    /// by hand: a pass that checked only for a top-level <c>enum</c> concluded `mode` was free.
    /// </remarks>
    [Fact]
    public async Task Capabilities_ReadTheLoadedVariantsModeFromTheSchema()
    {
        using var client = Stubbed(ImageOnlySchema);
        var caps = await client.GetCapabilitiesAsync();

        Assert.True(caps.Available);
        Assert.Equal(["image"], caps.Modes);
        Assert.Equal(["glb", "stl"], caps.OutputFormats);
        Assert.Equal(["stochastic", "multidiffusion"], caps.MultiImageAlgorithms);
        Assert.True(caps.AcceptsImageArray);

        //: Declared `maximum: 1` with a description reading "Only samples=1 is supported". Surfaced
        //: because it is the one field that looks as though it might return several variants at once.
        Assert.Equal(1, caps.MaxSamples);
    }

    /// <summary>A variant serving both declares an enum rather than a const, and both are read.</summary>
    [Fact]
    public async Task Capabilities_ReadAnEnumWhereAVariantServesBothModes()
    {
        using var client = Stubbed(ImageOnlySchema.Replace(
            """{"type":"string","const":"image"}""",
            """{"type":"string","enum":["text","image"]}"""));

        var caps = await client.GetCapabilitiesAsync();

        Assert.Equal(["text", "image"], caps.Modes);
        Assert.True(caps.Accepts("text"));
        Assert.True(caps.Accepts("image"));
    }

    /// <summary>An endpoint serving no schema is UNKNOWN, never "refuses everything".</summary>
    [Fact]
    public async Task Capabilities_AreUnknownWhenNoSchemaIsServed()
    {
        using var client = Stubbed(null);
        var caps = await client.GetCapabilitiesAsync();

        Assert.False(caps.Available);
        Assert.NotNull(caps.Error);
        Assert.True(caps.Accepts("text"));
        Assert.True(caps.Accepts("image"));
    }

    /// <summary>A mode the loaded variant does not serve is refused HERE, before any generation.</summary>
    /// <remarks>
    /// <b>This is the failure the detection exists for.</b> The client defaults to <c>text</c>, and a
    /// <c>large:image</c> container answers a 422 naming <c>mode</c> — so a run whose container was
    /// switched fails on every call, having spent a round trip each time, with a message about a field
    /// the caller thought was fine. Asserting that <b>no POST was made</b> is the half that matters.
    /// </remarks>
    [Fact]
    public async Task Generate_RefusesAModeTheLoadedVariantDoesNotServe()
    {
        var handler = new StubHandler(ImageOnlySchema);
        using var client = new TrellisClient("http://stub", httpClient: new HttpClient(handler));

        var result = await client.GenerateAsync(new TrellisRequest { Prompt = "a wooden barrel" });

        Assert.False(result.Success);
        Assert.Equal(TrellisFailure.InvalidRequest, result.Failure);
        Assert.Contains("'image'", result.Remedy);
        Assert.Contains("not 'text'", result.Remedy);
        Assert.False(result.Retryable);
        Assert.Equal(0, handler.Posts);

        output.WriteLine(result.Remedy);
    }

    /// <summary>When capabilities are unknown the request still goes out. Detection fails OPEN.</summary>
    /// <remarks>
    /// The hosted route serves no <c>openapi.json</c>, so a detection that refused on absence would
    /// break the one endpoint it was never tested against. A capability check must be able to say
    /// "I do not know" and get out of the way.
    /// </remarks>
    [Fact]
    public async Task Generate_ProceedsWhenCapabilitiesAreUnknown()
    {
        var handler = new StubHandler(null);
        using var client = new TrellisClient("http://stub", httpClient: new HttpClient(handler));

        var result = await client.GenerateAsync(new TrellisRequest { Prompt = "a wooden barrel" });

        Assert.NotEqual(TrellisFailure.InvalidRequest, result.Failure);
        Assert.Equal(1, handler.Posts);
    }

    /// <summary>An unset mode still has one, inferred the way the service infers it.</summary>
    /// <remarks>
    /// The schema says an absent <c>mode</c> is <i>"determined based on the image and prompt inputs"</i>,
    /// so a text prompt against an image-only variant fails whether or not the caller spelled the mode
    /// out. Checking only an explicit <c>Mode</c> would miss every request that left it null — which is
    /// the common case, since it is optional.
    /// </remarks>
    [Theory]
    [InlineData(null, false, "text")]
    [InlineData(null, true, "image")]
    [InlineData("text", true, "text")]
    public void EffectiveMode_IsInferredFromWhatIsSet(string? mode, bool hasImage, string expected) =>
        Assert.Equal(expected, TrellisClient.EffectiveMode(new TrellisRequest
        {
            Mode = mode,
            Prompt = hasImage ? null : "a barrel",
            Image = hasImage ? "data:image/png;base64,AAAA" : null
        }));
    #endregion

    #region Hosted
    /// <summary>The URL's shape picks the endpoint kind, and with it the inference URL.</summary>
    /// <remarks>
    /// <b>The bug this fixes was a hardcoded path.</b> <c>baseUrl + "/v1/infer"</c> cannot produce
    /// <c>/v1/genai/microsoft/trellis</c> by any choice of base, so the client could not reach the
    /// hosted route at all — measured, it 404s. One setting selects both now, because a URL plus a
    /// separate mode flag is two things to keep in step and one of them gets forgotten.
    /// </remarks>
    [Theory]
    [InlineData("http://localhost:8000", false, "http://localhost:8000/v1/infer")]
    [InlineData("http://192.168.8.171:8000/", false, "http://192.168.8.171:8000/v1/infer")]
    [InlineData(TrellisClient.HostedUrl, true, TrellisClient.HostedUrl)]
    public void Endpoint_IsChosenByTheUrlsShape(string baseUrl, bool hosted, string infer)
    {
        using var client = new TrellisClient(baseUrl);

        Assert.Equal(hosted, client.IsHosted);
        Assert.Equal(infer, client.InferUrl);
    }

    /// <summary>Nothing pattern-matches on NVIDIA's hostname, so a proxied route still reads hosted.</summary>
    [Fact]
    public void Endpoint_DoesNotKeyOnTheHostname()
    {
        using var proxied = new TrellisClient("https://gateway.internal/models/trellis");

        Assert.True(proxied.IsHosted);
        Assert.Equal("https://gateway.internal/models/trellis", proxied.InferUrl);
    }

    /// <summary>The hosted route serves no schema, so its limits are unknown and nothing is refused.</summary>
    /// <remarks>
    /// Verified against the real service: <c>/openapi.json</c> 404s there. Reported as unknown
    /// <b>without spending a round trip</b> to rediscover that, and <c>Accepts</c> then waves every
    /// mode through — the same fail-open the container path relies on when a schema cannot be read.
    /// </remarks>
    [Fact]
    public async Task Hosted_ReportsItsCapabilitiesAsUnknownWithoutAsking()
    {
        var handler = new StubHandler(ImageOnlySchema);
        using var client = new TrellisClient(
            TrellisClient.HostedUrl, "k", new HttpClient(handler));

        var caps = await client.GetCapabilitiesAsync();

        Assert.False(caps.Available);
        Assert.NotNull(caps.Error);
        Assert.True(caps.Accepts("text"));

        //: The schema stub would have answered had it been asked. Nothing asked it.
        Assert.Equal(0, handler.Gets);
    }

    /// <summary>no_texture is refused for hosted rather than dropped.</summary>
    /// <remarks>
    /// It is honoured by a container and not by the hosted route — measured. Stripping it silently
    /// would hand back a <i>textured</i> mesh to a caller who asked for none: billed, slower and
    /// 6.7x larger, with nothing in the result saying the flag had been ignored.
    /// </remarks>
    [Fact]
    public async Task Hosted_RefusesNoTextureRatherThanDroppingIt()
    {
        var handler = new StubHandler(null);
        using var client = new TrellisClient(
            TrellisClient.HostedUrl, "k", new HttpClient(handler));

        var result = await client.GenerateAsync(
            new TrellisRequest { Prompt = "a barrel", NoTexture = true });

        Assert.Equal(TrellisFailure.InvalidRequest, result.Failure);
        Assert.Contains("no_texture", result.Remedy);
        Assert.Equal(0, handler.Posts);

        //: The same request without the flag is not refused locally — so the assertion above is
        //: about the flag rather than about hosted requests being blocked wholesale.
        var allowed = await client.GenerateAsync(new TrellisRequest { Prompt = "a barrel" });
        Assert.NotEqual(TrellisFailure.InvalidRequest, allowed.Failure);
    }

    /// <summary>The hosted route refuses an image of your own, and says why.</summary>
    /// <remarks>
    /// <b>Its image field takes only NVIDIA's four canned demos.</b> Found by walking every encoding
    /// against the live service: inline base64 answers <i>"Expected: example_id, got: base64"</i>;
    /// an uploaded NVCF asset answers <i>"got: asset_id"</i> — and the upload succeeds first, so a
    /// caller doing it by hand gets no warning until the generation; and the right token answers
    /// <i>"Not valid example_id, expected value 0, 1, 2, 3"</i>.
    /// <para>
    /// Refused here so the message explains it once, rather than each caller decoding a 422 about a
    /// token they never wrote. <b>It closes the draw-then-turn route on hosted</b>, which is the one
    /// that matters most to this studio, so it is worth stating loudly rather than discovering.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Hosted_RefusesAnImageOfYourOwnAndNamesTheReason()
    {
        var handler = new StubHandler(null);
        using var client = new TrellisClient(
            TrellisClient.HostedUrl, "k", new HttpClient(handler));

        var result = await client.GenerateAsync(new TrellisRequest
        {
            Mode = "image",
            Image = "data:image/png;base64,AAAA"
        });

        Assert.Equal(TrellisFailure.InvalidRequest, result.Failure);
        Assert.Contains("example_id", result.Remedy);
        Assert.Equal(0, handler.Posts);
    }

    /// <summary>One of its own demo references is allowed through rather than blocked.</summary>
    /// <remarks>
    /// Declining it would be this client overruling the service about its own behaviour. Useless for
    /// a studio drawing its own elevations, and not ours to forbid.
    /// </remarks>
    [Fact]
    public async Task Hosted_AllowsItsOwnExampleReference()
    {
        var handler = new StubHandler(null);
        using var client = new TrellisClient(
            TrellisClient.HostedUrl, "k", new HttpClient(handler));

        var result = await client.GenerateAsync(new TrellisRequest
        {
            Mode = "image",
            Image = "data:image/png;example_id,0"
        });

        Assert.NotEqual(TrellisFailure.InvalidRequest, result.Failure);
        Assert.Equal(1, handler.Posts);
    }

    /// <summary>A container takes an image of your own, which is the contrast that matters.</summary>
    [Fact]
    public async Task AContainerTakesAnImageOfYourOwn()
    {
        var handler = new StubHandler(ImageOnlySchema);
        using var client = new TrellisClient("http://stub", httpClient: new HttpClient(handler));

        var result = await client.GenerateAsync(new TrellisRequest
        {
            Mode = "image",
            Image = "data:image/png;base64,AAAA"
        });

        Assert.NotEqual(TrellisFailure.InvalidRequest, result.Failure);
        Assert.Equal(1, handler.Posts);
    }

    /// <summary>A 202 is polled to its result rather than parsed as one.</summary>
    /// <remarks>
    /// <b><c>IsSuccessStatusCode</c> is true for 202</b>, so without this the body of an
    /// acknowledgement is handed to the parser as though it were a finished generation — and fails
    /// naming the wrong thing. The stub answers 202 once, then 202 again (still working), then the
    /// artifact, which is the shape NVCF actually produces.
    /// </remarks>
    [Fact]
    public async Task Hosted_PollsA202ToItsResult()
    {
        var handler = new StubHandler(null) { AcceptThenPoll = true };
        using var client = new TrellisClient(
            TrellisClient.HostedUrl, "k", new HttpClient(handler));

        var result = await client.GenerateAsync(
            new TrellisRequest { Prompt = "a barrel" }, timeout: TimeSpan.FromSeconds(30));

        Assert.True(result.Success, result.Remedy + " " + result.Error);
        Assert.NotEmpty(result.Bytes);
        Assert.True(handler.Polls >= 2, $"expected the status route to be polled, saw {handler.Polls}");
    }

    /// <summary>A 202 with no request id says so, rather than becoming a generic service error.</summary>
    /// <remarks>
    /// That is a different repair from a failed generation — it means <i>this endpoint asked us to
    /// poll and we could not tell where</i> — and guessing between the two wastes the run.
    /// </remarks>
    [Fact]
    public async Task Hosted_SaysSoWhenA202CarriesNoRequestId()
    {
        var handler = new StubHandler(null) { AcceptThenPoll = true, OmitRequestId = true };
        using var client = new TrellisClient(
            TrellisClient.HostedUrl, "k", new HttpClient(handler));

        var result = await client.GenerateAsync(new TrellisRequest { Prompt = "a barrel" });

        Assert.False(result.Success);
        Assert.Contains("NVCF-REQID", result.Remedy);
        Assert.Equal(0, handler.Polls);
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

    #region Methods (private)
    /// <summary>A client whose schema read is answered by <paramref name="schema"/>, or 404 when null.</summary>
    static TrellisClient Stubbed(string? schema) =>
        new("http://stub", httpClient: new HttpClient(new StubHandler(schema)));
    #endregion

    #region Types
    /// <summary>Answers the schema and metadata reads, and counts inference attempts.</summary>
    /// <remarks>
    /// <b>The POST count is the assertion that matters</b> in two of the tests above: a refusal is
    /// only worth having if it happens BEFORE the round trip, and a generation is only proven to have
    /// been attempted if something saw it. Both are invisible from the result alone.
    /// </remarks>
    sealed class StubHandler(string? schema) : HttpMessageHandler
    {
        /// <summary>Answer the first POST with 202, then make the status route work for it.</summary>
        internal bool AcceptThenPoll { get; init; }

        /// <summary>Send the 202 without its NVCF-REQID, which is the case that cannot be followed.</summary>
        internal bool OmitRequestId { get; init; }

        internal int Posts { get; private set; }

        internal int Gets { get; private set; }

        internal int Polls { get; private set; }

        //: A one-pixel GLB stand-in. The bytes are never decoded by these tests — what is asserted
        //: is that a poll reached a result at all — so any non-empty payload serves.
        static string Artifact() =>
            """{"artifacts":[{"base64":"Z2xURgIAAAA=","finishReason":"SUCCESS","seed":7}]}""";

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;

            if (request.Method == HttpMethod.Post)
            {
                this.Posts++;

                if (!this.AcceptThenPoll)
                {
                    //: A 500 rather than a success: those tests are about whether the call was MADE,
                    //: and a stubbed artifact would assert the decoding path again for nothing.
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    {
                        Content = new StringContent("stub")
                    });
                }

                var accepted = new HttpResponseMessage(HttpStatusCode.Accepted)
                {
                    Content = new StringContent("{\"status\":\"pending\"}")
                };
                if (!this.OmitRequestId) accepted.Headers.TryAddWithoutValidation("NVCF-REQID", "req-1");
                return Task.FromResult(accepted);
            }

            if (path.Contains("/status/", StringComparison.Ordinal))
            {
                this.Polls++;

                //: Still working on the first poll, finished on the second — so the test proves the
                //: loop actually loops rather than passing on a single lucky answer.
                return Task.FromResult(this.Polls < 2
                    ? new HttpResponseMessage(HttpStatusCode.Accepted)
                    {
                        Content = new StringContent("{}")
                    }
                    : new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(Artifact())
                    });
            }

            this.Gets++;

            if (path == TrellisClient.SchemaPath && schema is not null)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(schema)
                });
            }

            //: Metadata is deliberately absent, so `Profile` comes back null and the refusal message
            //: has to read sensibly without it.
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
    #endregion
}
