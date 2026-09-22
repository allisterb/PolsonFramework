namespace Polson.ExtendedMind.ObjectGeneration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Text or images to a 3D model, by way of Microsoft TRELLIS served as an NVIDIA NIM.</summary>
/// <remarks>
/// <para>
/// <b>This is the studio's third route to a prop.</b> The 2D toolkit draws anything in elevation and
/// costs nothing; a Blender op list does parametric hard-surface that turns in depth; this does
/// organic and irregular form — the barrel whose bulge needs a lathe the op vocabulary has not got.
/// See <c>docs/internal/blender-prop-pipeline.md</c> §11.
/// </para>
/// <para>
/// <b>It is NOT an OpenAI-shaped endpoint</b>, and the container's own spec settles it: the only
/// inference route is <c>POST /v1/infer</c>, and there is no <c>/v1/images/generations</c>. So
/// <c>Microsoft.Extensions.AI.OpenAI</c> and the <c>OpenAI</c> package cannot call this; a plain
/// <see cref="HttpClient"/> can, which is what this is.
/// </para>
/// <para>
/// <b>Four things the published documentation gets wrong</b>, each checked against the container's
/// <c>openapi.json</c> and against live calls on 2026-09-22:
/// </para>
/// <list type="number">
/// <item>There is no 77-character prompt cap. <c>prompt</c> declares no <c>maxLength</c>.</item>
/// <item><c>image</c> accepts an array, with <c>multiimage_algo</c> choosing how they combine.</item>
/// <item><c>no_texture: true</c> works locally and fails against the hosted endpoint.</item>
/// <item>
/// <b><c>seed: 0</c> means RANDOM, not deterministic</b> — the schema says so outright. That is the
/// one that matters here, because a studio whose whole record rests on a saved script re-rendering
/// cannot default to an unreproducible seed. <see cref="DefaultSeed"/> is non-zero for that reason.
/// </item>
/// </list>
/// <para>
/// <b>Configuration.</b> <c>Trellis:BaseUrl</c> points at a local container
/// (<c>http://localhost:8000</c>) or at NVIDIA's hosted route. <c>ApiKeys:NvidiaNIM</c> is required
/// for the hosted endpoint and unnecessary for a local one, which is why the key is optional here
/// and <see cref="ParallelSearch.ParallelClient"/>'s is not.
/// </para>
/// <para>
/// <b>Measured, RTX 5060 Ti, local container:</b> 18.0 s textured at 25 steps, 20.3 s at 10 —
/// step count is within noise, so raising it is free. 8.5 s with <c>no_texture</c>. Meshes came back
/// at 4,093–5,195 vertices, comfortably inside <c>MeshGltf</c>'s 65,535 ceiling. The hosted endpoint
/// managed roughly one success in four, every failure at a ~90 s wall, which is what
/// <see cref="GenerateAsync"/>'s retry exists for.
/// </para>
/// <para>
/// This type is <b>not</b> exposed to the JavaScript sandbox. Like <c>ImageGenerator</c> and
/// <c>ParallelClient</c>, putting it in front of an agent needs a budget and a content-addressed
/// cache, which is the layer above this one.
/// </para>
/// </remarks>
public sealed class TrellisClient : Runtime, IDisposable
{
    #region Constructors
    /// <param name="baseUrl">Origin only, no path. Defaults to a local container.</param>
    /// <param name="apiKey">
    /// <c>ApiKeys:NvidiaNIM</c>. Required by the hosted endpoint, ignored by a local container —
    /// so null is legitimate here rather than a misconfiguration.
    /// </param>
    /// <param name="httpClient">Optional; when supplied it is neither mutated nor disposed.</param>
    public TrellisClient(string? baseUrl = null, string? apiKey = null, HttpClient? httpClient = null)
    {
        if (apiKey is not null && apiKey.Any(char.IsControl))
        {
            throw new ArgumentException("The NVIDIA API key contains a control character.", nameof(apiKey));
        }

        this.apiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey;
        this.baseUrl = (baseUrl ?? DefaultBaseUrl).TrimEnd('/');
        this.ownsHttp = httpClient is null;

        // No client-wide timeout: each call sets its own, and they differ by an order of magnitude
        // between a local container (~20 s) and the hosted route's ~90 s wall.
        this.http = httpClient ?? new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    }
    #endregion

    #region Properties
    /// <summary>A local NIM container. The hosted route is a different origin AND a different path.</summary>
    public const string DefaultBaseUrl = "http://localhost:8000";

    /// <summary>The only inference route the container serves.</summary>
    public const string InferPath = "/v1/infer";

    /// <summary>Readiness. A container answers this long before it can generate.</summary>
    public const string ReadyPath = "/v1/health/ready";

    /// <summary>The service's own schema, which is where the loaded variant's limits are declared.</summary>
    public const string SchemaPath = "/openapi.json";

    /// <summary>Which variant is loaded, by NGC profile name.</summary>
    public const string MetadataPath = "/v1/metadata";

    /// <summary>The service's bounds, from <c>Object3DRequest</c>.</summary>
    public const int MinSamplingSteps = 10;

    /// <inheritdoc cref="MinSamplingSteps"/>
    public const int MaxSamplingSteps = 50;

    /// <summary>Step count is close to free, so the default sits at the service's own.</summary>
    public const int DefaultSamplingSteps = 25;

    /// <summary>Exclusive lower bound on both cfg scales.</summary>
    public const double MinCfgScale = 1.0;

    /// <summary>Inclusive upper bound on both cfg scales.</summary>
    public const double MaxCfgScale = 10.0;

    /// <summary>
    /// The seed value the service reads as "pick one at random". <b>Passing this makes a generation
    /// unreproducible</b>, which is why it is named rather than left as a bare zero.
    /// </summary>
    public const long RandomSeed = 0;

    /// <summary>A non-zero default, so an unspecified generation is still reproducible.</summary>
    public const long DefaultSeed = 1;

    /// <summary>Deadline for one attempt. A local run is ~20 s; the hosted wall is ~90 s.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);
    #endregion

    #region Methods
    /// <summary>Whether the endpoint is up and finished warming.</summary>
    /// <remarks>
    /// Worth asking before committing to a route that needs it, exactly as a script asks
    /// <c>Skia.tracer.available</c>. A container answers <c>/v1/health/live</c> well before it can
    /// generate — this asks <c>ready</c>, which is the one that means warmed and loaded.
    /// </remarks>
    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            using var response = await this.http.GetAsync(this.baseUrl + ReadyPath, cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>What the loaded variant accepts, read once and cached for this client.</summary>
    /// <remarks>
    /// <b>Ask this before committing to a modality</b>, exactly as a script asks
    /// <c>Skia.tracer.available</c>. The answer is a property of the container that happens to be
    /// running rather than of TRELLIS, and it changes when the variant is switched — which is how a
    /// text-mode default silently starts failing against a box that was image-only all along.
    /// <para>
    /// Cached per client because the base address is fixed at construction. A failed read is cached
    /// too: an endpoint serving no schema will not grow one, and re-asking on every generation would
    /// pay a timeout per call to learn the same nothing.
    /// </para>
    /// </remarks>
    public async Task<TrellisCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        if (this.capabilities is { } cached)
        {
            return cached;
        }

        var read = await ReadCapabilitiesAsync(cancellationToken);

        // Not cached when the caller's own token cancelled the read: that says nothing about the
        // endpoint, and caching it would make one cancellation poison every later call.
        if (!cancellationToken.IsCancellationRequested)
        {
            this.capabilities = read;
        }

        return read;
    }

    /// <summary>Generates one model, retrying a service-side fault.</summary>
    /// <param name="attempts">
    /// How many times to try. <b>Above one only helps a hosted endpoint</b>, whose failures are
    /// transient and arrive at a fixed ~90 s wall; a local container either works or is misconfigured,
    /// and retrying a misconfiguration only wastes the clock.
    /// </param>
    public async Task<TrellisResult> GenerateAsync(
        TrellisRequest request,
        int attempts = 1,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfLessThan(attempts, 1);

        if (Validate(request) is { } invalid)
        {
            return invalid;
        }

        //: The static bounds above accept 'text' and 'image' because both are real TRELLIS modes.
        //: Only the loaded variant knows which of them IT serves, so this is the one check that has
        //: to ask the service — and it fails OPEN, so an endpoint with no schema behaves as before.
        var caps = await GetCapabilitiesAsync(cancellationToken);
        var mode = EffectiveMode(request);
        if (!caps.Accepts(mode))
        {
            return Invalid(
                $"This endpoint's loaded variant accepts mode {caps.ModeList()}, not '{mode}'"
                + (caps.Profile is { } p ? $" — it is running {p}" : string.Empty)
                + $". Supply {(caps.Modes.Contains("image") ? "an image" : "a prompt")} instead, or "
                + "load a variant that serves both. The accepted mode is a property of the container "
                + "that happens to be running, not of TRELLIS.");
        }

        var started = System.Diagnostics.Stopwatch.StartNew();
        TrellisResult? last = null;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            last = await AttemptAsync(request, timeout ?? DefaultTimeout, cancellationToken);
            last.Attempts = attempt;
            last.Ms = (int)started.ElapsedMilliseconds;

            if (last.Success || !last.Retryable || cancellationToken.IsCancellationRequested)
            {
                return last;
            }

            if (attempt < attempts)
            {
                //: Linear rather than exponential. The hosted failure mode is a busy pool rather
                //: than a rate limit, so backing off hard buys nothing and spends the clock.
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt), cancellationToken);
            }
        }

        return last!;
    }

    /// <summary>Our own bounds check, so a bad request costs no network call. Null when it passes.</summary>
    /// <remarks>
    /// <b>The mode check is the one that earns its place.</b> The service refuses a body carrying
    /// both a prompt and an image — and counts <c>"image": null</c> as carrying one, so the failure
    /// reads as though the caller sent an image they never set. Serialization drops nulls to avoid
    /// that; this catches the case where both were genuinely set.
    /// </remarks>
    public static TrellisResult? Validate(TrellisRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var hasPrompt = !string.IsNullOrWhiteSpace(request.Prompt);
        var hasImage = request.Image is not null;

        if (!hasPrompt && !hasImage)
        {
            return Invalid("Set either a prompt or an image. Neither was supplied.");
        }

        if (hasPrompt && hasImage)
        {
            return Invalid(
                "Set a prompt or an image, not both — the service refuses a request carrying both.");
        }

        foreach (var (name, steps) in new[]
        {
            ("ss_sampling_steps", request.StructureSteps), ("slat_sampling_steps", request.LatentSteps)
        })
        {
            if (steps is { } s && (s < MinSamplingSteps || s > MaxSamplingSteps))
            {
                return Invalid(
                    $"{name} must be between {MinSamplingSteps} and {MaxSamplingSteps}; got {s}. "
                    + $"Raising it is close to free — {DefaultSamplingSteps} is the service's own default.");
            }
        }

        foreach (var (name, scale) in new[]
        {
            ("ss_cfg_scale", request.StructureCfgScale), ("slat_cfg_scale", request.LatentCfgScale)
        })
        {
            if (scale is { } v && (v <= MinCfgScale || v > MaxCfgScale))
            {
                return Invalid($"{name} must be greater than {MinCfgScale} and at most {MaxCfgScale}; got {v}.");
            }
        }

        if (request.Samples is { } samples && samples != 1)
        {
            return Invalid($"samples must be 1 — the service supports no other value; got {samples}.");
        }

        if (request.Seed is { } seed && (seed < 0 || seed > 4294967295L))
        {
            return Invalid($"seed must be between 0 and 4294967295; got {seed}.");
        }

        if (request.OutputFormat is { } format && format is not ("glb" or "stl"))
        {
            return Invalid($"output_format must be 'glb' or 'stl'; got '{format}'.");
        }

        if (request.Mode is { } mode && mode is not ("text" or "image"))
        {
            return Invalid($"mode must be 'text' or 'image'; got '{mode}'.");
        }

        return null;
    }
    #endregion

    #region Methods (private)
    /// <summary>The mode the service will actually apply, inferred the way the service infers it.</summary>
    /// <remarks>
    /// <c>mode</c> is optional, and the schema says an unset one is <i>"determined based on the image
    /// and prompt inputs"</i>. So a request that never names a mode still has one, and a text prompt
    /// against an image-only variant fails whether or not the caller spelled it out.
    /// </remarks>
    internal static string EffectiveMode(TrellisRequest request) =>
        request.Mode ?? (request.Image is not null ? "image" : "text");

    async Task<TrellisCapabilities> ReadCapabilitiesAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            using var response = await this.http.GetAsync(this.baseUrl + SchemaPath, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                return new TrellisCapabilities
                {
                    Available = false,
                    Error = $"{SchemaPath} answered {(int)response.StatusCode}."
                };
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cts.Token));
            if (!doc.RootElement.TryGetProperty("components", out var components)
                || !components.TryGetProperty("schemas", out var schemas)
                || !schemas.TryGetProperty("Object3DRequest", out var schema)
                || !schema.TryGetProperty("properties", out var fields))
            {
                return new TrellisCapabilities
                {
                    Available = false,
                    Error = "The schema carries no Object3DRequest."
                };
            }

            return new TrellisCapabilities
            {
                Available = true,
                Profile = await ReadProfileAsync(cts.Token),
                Modes = Literals(fields, "mode"),
                OutputFormats = Literals(fields, "output_format"),
                MultiImageAlgorithms = Literals(fields, "multiimage_algo"),
                AcceptsImageArray = Branches(fields, "image")
                    .Any(b => b.TryGetProperty("type", out var t) && t.ValueEquals("array")),
                MaxSamples = Branches(fields, "samples")
                    .Where(b => b.TryGetProperty("maximum", out _))
                    .Select(b => (int)b.GetProperty("maximum").GetDouble())
                    .DefaultIfEmpty(1)
                    .Max()
            };
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException
            or OperationCanceledException or JsonException)
        {
            return new TrellisCapabilities { Available = false, Error = e.Message };
        }
    }

    /// <summary>The loaded variant's profile name, which is diagnostic rather than load-bearing.</summary>
    async Task<string?> ReadProfileAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await this.http.GetAsync(this.baseUrl + MetadataPath, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

            //: Several models are listed — the generator, a guardrails model and a metadata stub.
            //: The one with a backend in its name is the generator; anything else is scaffolding.
            return doc.RootElement.TryGetProperty("modelInfo", out var models)
                && models.ValueKind == JsonValueKind.Array
                ? models.EnumerateArray()
                    .Select(m => m.TryGetProperty("shortName", out var n) ? n.GetString() : null)
                    .FirstOrDefault(n => n is not null && n.Contains("pytorch", StringComparison.OrdinalIgnoreCase))
                : null;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException
            or OperationCanceledException or JsonException)
        {
            return null;
        }
    }

    /// <summary>The string values a field is constrained to, across every branch of its union.</summary>
    /// <remarks>
    /// <b>The constraint lives inside an <c>anyOf</c>, not beside it</b> — every optional field is a
    /// union with <c>null</c>, so the branch carries the <c>const</c> or <c>enum</c> and the property
    /// itself carries neither. Reading only the top level reports every field as unconstrained, which
    /// is a wrong answer that looks like a permissive one.
    /// </remarks>
    static IReadOnlyList<string> Literals(JsonElement fields, string name)
    {
        List<string> found = [];

        foreach (var branch in Branches(fields, name))
        {
            if (branch.TryGetProperty("const", out var one) && one.ValueKind == JsonValueKind.String)
            {
                found.Add(one.GetString()!);
            }

            if (branch.TryGetProperty("enum", out var many) && many.ValueKind == JsonValueKind.Array)
            {
                found.AddRange(many.EnumerateArray()
                    .Where(v => v.ValueKind == JsonValueKind.String)
                    .Select(v => v.GetString()!));
            }
        }

        return found.Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>A field's union branches, or the field itself when it is not a union.</summary>
    static IEnumerable<JsonElement> Branches(JsonElement fields, string name)
    {
        if (!fields.TryGetProperty(name, out var field)) yield break;

        if (field.TryGetProperty("anyOf", out var any) && any.ValueKind == JsonValueKind.Array)
        {
            foreach (var branch in any.EnumerateArray()) yield return branch;
        }
        else
        {
            yield return field;
        }
    }

    async Task<TrellisResult> AttemptAsync(
        TrellisRequest request, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        using var message = new HttpRequestMessage(HttpMethod.Post, this.baseUrl + InferPath)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(request, SerializerOptions), Encoding.UTF8, "application/json")
        };

        //: On the request rather than on the client's default headers, so one HttpClient can be
        //: shared and a local container is never handed a key it has no use for.
        if (this.apiKey is not null)
        {
            message.Headers.TryAddWithoutValidation("Authorization", "Bearer " + this.apiKey);
        }

        try
        {
            using var response = await this.http.SendAsync(message, cts.Token);
            var body = await response.Content.ReadAsStringAsync(cts.Token);

            return response.IsSuccessStatusCode
                ? Parse(body, request, (int)watch.ElapsedMilliseconds)
                : Classify(response.StatusCode, body, (int)watch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return TrellisResult.Fail(TrellisFailure.Cancelled, "The caller cancelled the request.",
                retryable: false, ms: (int)watch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            return TrellisResult.Fail(TrellisFailure.Timeout,
                $"No answer within {timeout.TotalSeconds:F0}s. A local container answers in about 20s; "
                + "the hosted endpoint errors at a ~90s wall, which is worth retrying.",
                retryable: true, ms: (int)watch.ElapsedMilliseconds);
        }
        catch (HttpRequestException e)
        {
            return TrellisResult.Fail(TrellisFailure.Network,
                $"Could not reach {this.baseUrl}. Check the container is running and that "
                + "'Trellis:BaseUrl' is right.",
                retryable: true, error: e.Message, ms: (int)watch.ElapsedMilliseconds);
        }
    }

    static TrellisResult Parse(string body, TrellisRequest request, int ms)
    {
        try
        {
            using var document = JsonDocument.Parse(body);

            if (!document.RootElement.TryGetProperty("artifacts", out var artifacts)
                || artifacts.ValueKind != JsonValueKind.Array
                || artifacts.GetArrayLength() == 0)
            {
                return TrellisResult.Fail(TrellisFailure.Malformed,
                    "The service answered 200 with no artifacts.", retryable: true, ms: ms);
            }

            var artifact = artifacts[0];
            var reason = ReadFinishReason(artifact);
            var seed = artifact.TryGetProperty("seed", out var s) && s.ValueKind == JsonValueKind.Number
                ? s.GetInt64() : request.Seed ?? DefaultSeed;

            if (reason == TrellisFinishReason.ContentFiltered)
            {
                return TrellisResult.Fail(TrellisFailure.ContentFiltered,
                    "The safety filter declined this prompt. Reword it; retrying it unchanged will "
                    + "be declined again.", retryable: false, ms: ms);
            }

            if (reason != TrellisFinishReason.Success)
            {
                return TrellisResult.Fail(TrellisFailure.ServiceError,
                    $"Generation reported finishReason '{reason}'.", retryable: true, ms: ms);
            }

            if (!artifact.TryGetProperty("base64", out var encoded)
                || encoded.ValueKind != JsonValueKind.String
                || encoded.GetString() is not { Length: > 0 } text)
            {
                return TrellisResult.Fail(TrellisFailure.Malformed,
                    "The artifact reported SUCCESS but carried no model.", retryable: true, ms: ms);
            }

            return new TrellisResult
            {
                Success = true,
                Bytes = Convert.FromBase64String(text),
                Format = request.OutputFormat ?? "glb",
                FinishReason = TrellisFinishReason.Success,
                Seed = seed,
                Ms = ms
            };
        }
        catch (Exception e) when (e is JsonException or FormatException)
        {
            return TrellisResult.Fail(TrellisFailure.Malformed,
                "The answer could not be read as a model.", retryable: true, error: e.Message, ms: ms);
        }
    }

    static TrellisFinishReason ReadFinishReason(JsonElement artifact) =>
        artifact.TryGetProperty("finishReason", out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() switch
            {
                "SUCCESS" => TrellisFinishReason.Success,
                "CONTENT_FILTERED" => TrellisFinishReason.ContentFiltered,
                "ERROR" => TrellisFinishReason.Error,
                _ => TrellisFinishReason.Unknown
            }
            : TrellisFinishReason.Unknown;

    static TrellisResult Classify(HttpStatusCode status, string body, int ms) => status switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => TrellisResult.Fail(
            TrellisFailure.Auth,
            "The endpoint rejected the key. Check 'ApiKeys:NvidiaNIM'. A local container needs none.",
            retryable: false, error: Trim(body), ms: ms),

        HttpStatusCode.UnprocessableEntity or HttpStatusCode.BadRequest => TrellisResult.Fail(
            TrellisFailure.InvalidRequest,
            "The service refused the request body. Its own message names the field.",
            retryable: false, error: Trim(body), ms: ms),

        HttpStatusCode.TooManyRequests => TrellisResult.Fail(
            TrellisFailure.RateLimited, "Throttled. Retry after a pause.",
            retryable: true, error: Trim(body), ms: ms),

        _ => TrellisResult.Fail(
            TrellisFailure.ServiceError,
            $"The service answered {(int)status}. On the hosted endpoint this is common and "
            + "transient — measured at roughly one success in four — so retrying is correct.",
            retryable: true, error: Trim(body), ms: ms)
    };

    static TrellisResult Invalid(string remedy) =>
        TrellisResult.Fail(TrellisFailure.InvalidRequest, remedy, retryable: false);

    static string Trim(string s) =>
        s.Length <= 400 ? s.Trim() : string.Concat(s.AsSpan(0, 400).Trim(), "…");

    public void Dispose()
    {
        if (this.ownsHttp)
        {
            this.http.Dispose();
        }
    }
    #endregion

    #region Fields
    /// <summary>
    /// <b>Nulls are dropped, and that is load-bearing rather than cosmetic.</b> The service reads
    /// <c>"image": null</c> as an image being present and refuses the request with "Multiple prompt
    /// fields are detected" — a 422 that names a field the caller never set.
    /// </summary>
    internal static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    //: The loaded variant's limits, read once. Not `volatile` and not locked: a racing pair of
    //: callers performs the read twice and stores the same answer, which is cheaper than the
    //: synchronisation and cannot be wrong.
    TrellisCapabilities? capabilities;

    readonly HttpClient http;
    readonly string baseUrl;
    readonly string? apiKey;
    readonly bool ownsHttp;
    #endregion
}
