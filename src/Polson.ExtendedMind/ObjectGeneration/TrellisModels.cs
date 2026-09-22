namespace Polson.ExtendedMind.ObjectGeneration;

using System.Text.Json.Serialization;

#region Request

/// <summary>What to generate, and how hard to try.</summary>
/// <remarks>
/// <para>
/// Field names and bounds are taken from the container's own <c>openapi.json</c>
/// (<c>Object3DRequest</c>, NIM 1.0.2), not from the published documentation — the two disagree in
/// four places and the docs lost every time. See <see cref="TrellisClient"/> for the list.
/// </para>
/// <para>
/// <b>Null fields must not be serialized.</b> The service rejects a body carrying both a prompt and
/// an image with <i>"Multiple prompt fields are detected"</i>, and it counts <c>"image": null</c> as
/// present — so a serializer that writes nulls makes every text request fail with a 422 that reads
/// as though the caller sent an image. <see cref="TrellisClient"/> sets
/// <c>JsonIgnoreCondition.WhenWritingNull</c> for exactly this reason; it is not a tidiness measure.
/// </para>
/// </remarks>
public sealed class TrellisRequest
{
    #region Properties
    /// <summary><c>text</c> or <c>image</c>. Null lets the service infer it from what is set.</summary>
    [JsonPropertyName("mode")]
    public string? Mode { get; init; } = "text";

    /// <summary>The text prompt.</summary>
    /// <remarks>
    /// <b>The container enforces no length limit</b> — <c>Object3DRequest.prompt</c> carries no
    /// <c>maxLength</c>. NVIDIA's hosted documentation claims a 77-character cap, which is either
    /// hosted-only or the CLIP tokenizer's soft limit surfacing as advice. Nothing is enforced here
    /// for that reason, but a prompt far past ~77 characters is likely being silently truncated by
    /// the text encoder wherever it runs.
    /// </remarks>
    [JsonPropertyName("prompt")]
    public string? Prompt { get; init; }

    /// <summary>One base64 data URI, or several. Leave null for a text prompt.</summary>
    [JsonPropertyName("image")]
    public object? Image { get; init; }

    /// <summary><c>stochastic</c> (cycles images, faster) or <c>multidiffusion</c> (averages).</summary>
    [JsonPropertyName("multiimage_algo")]
    public string? MultiImageAlgorithm { get; init; }

    /// <summary>How strictly sparse-structure diffusion follows the prompt. 1 &lt; v ≤ 10.</summary>
    [JsonPropertyName("ss_cfg_scale")]
    public double? StructureCfgScale { get; init; }

    /// <summary>How strictly structured-latent diffusion follows the prompt. 1 &lt; v ≤ 10.</summary>
    [JsonPropertyName("slat_cfg_scale")]
    public double? LatentCfgScale { get; init; }

    /// <summary>Sparse-structure diffusion steps, 10–50.</summary>
    /// <remarks>
    /// <b>Raising this is close to free and is worth doing.</b> Measured on an RTX 5060 Ti: 25 steps
    /// took 18.0 s against 20.3 s for 10 — the faster run was the one doing more work, which is noise
    /// either way. Diffusion is not the bottleneck; mesh extraction and texture baking are.
    /// </remarks>
    [JsonPropertyName("ss_sampling_steps")]
    public int? StructureSteps { get; init; } = TrellisClient.DefaultSamplingSteps;

    /// <summary>Structured-latent diffusion steps, 10–50.</summary>
    [JsonPropertyName("slat_sampling_steps")]
    public int? LatentSteps { get; init; } = TrellisClient.DefaultSamplingSteps;

    /// <summary>Objects to generate. The service supports <b>1 only</b>.</summary>
    [JsonPropertyName("samples")]
    public int? Samples { get; init; } = 1;

    /// <summary>Skip texture baking. Much faster and much smaller; still carries a UV atlas.</summary>
    /// <remarks>
    /// Measured: 8.5 s and 190,736 bytes against 18.0 s and 1,276,784 bytes textured — 2.1x faster
    /// and 6.7x smaller. For a studio that inks geometry rather than presenting a render, this is
    /// often the better default.
    /// <para>
    /// <b>It fails against the hosted endpoint</b> and works against a local container. That is one
    /// of the four documented-versus-actual disagreements.
    /// </para>
    /// </remarks>
    [JsonPropertyName("no_texture")]
    public bool? NoTexture { get; init; }

    /// <summary>The generation seed. <b>0 means RANDOM</b>; see <see cref="TrellisClient.RandomSeed"/>.</summary>
    [JsonPropertyName("seed")]
    public long? Seed { get; init; } = TrellisClient.DefaultSeed;

    /// <summary><c>glb</c> or <c>stl</c>. With <c>stl</c> the service ignores <see cref="NoTexture"/>.</summary>
    [JsonPropertyName("output_format")]
    public string? OutputFormat { get; init; } = "glb";
    #endregion
}
#endregion

#region Response

/// <summary>The service's own reading of how generation ended.</summary>
public enum TrellisFinishReason
{
    /// <summary>Absent or unrecognised — treated as a failure rather than assumed good.</summary>
    Unknown = 0,

    Success = 1,

    /// <summary>Refused by the pipeline's <c>cosmos_frame_content_safety_filter</c>.</summary>
    ContentFiltered = 2,

    Error = 3
}

/// <summary>Why a generation did not produce a model. Drives retry-versus-reword.</summary>
public enum TrellisFailure
{
    None = 0,

    /// <summary>No endpoint configured. Nothing this session will fix.</summary>
    NotConfigured = 1,

    /// <summary>Key rejected by a hosted endpoint. A local container needs no key.</summary>
    Auth = 2,

    /// <summary>Refused by our own bounds checks, or 422 from the service.</summary>
    InvalidRequest = 3,

    /// <summary>The safety filter declined it. Rewording is the remedy; retrying is not.</summary>
    ContentFiltered = 4,

    /// <summary>Throttled. Retrying after a pause is correct.</summary>
    RateLimited = 5,

    /// <summary>
    /// The service failed on its side. <b>Expected and worth retrying on the hosted endpoint</b>,
    /// which was measured at roughly one success in four, every failure arriving at a ~90 s wall.
    /// </summary>
    ServiceError = 6,

    /// <summary>The request never reached the service.</summary>
    Network = 7,

    Timeout = 8,

    Cancelled = 9,

    /// <summary>A 200 whose body could not be read as a model.</summary>
    Malformed = 10
}

/// <summary>What a generation produced, or why it did not.</summary>
/// <remarks>
/// <b><see cref="Success"/> is the first thing to read.</b> Nothing here throws for a remote fault —
/// a refusal, a filter and a service outage are all results, because a caller composing props wants
/// to report them rather than unwind. Only a missing configuration throws.
/// </remarks>
public sealed class TrellisResult
{
    #region Properties
    public bool Success { get; init; }

    public TrellisFailure Failure { get; init; }

    /// <summary>The failure as a readable name, which is what an agent should surface.</summary>
    public string FailureName => Failure.ToString();

    /// <summary>What to do next, in words. Read this before retrying anything.</summary>
    public string Remedy { get; init; } = string.Empty;

    /// <summary>Whether repeating the identical request could succeed.</summary>
    public bool Retryable { get; init; }

    /// <summary>The underlying message, when there was one.</summary>
    public string? Error { get; init; }

    /// <summary>The model, decoded. Empty on failure.</summary>
    public byte[] Bytes { get; init; } = [];

    /// <summary><c>glb</c> or <c>stl</c>, as asked for.</summary>
    public string Format { get; init; } = "glb";

    /// <summary>The service's own verdict. <see cref="TrellisFinishReason.Success"/> on success.</summary>
    public TrellisFinishReason FinishReason { get; init; }

    /// <summary>
    /// The seed actually used. <b>Worth recording</b> — a request made with
    /// <see cref="TrellisClient.RandomSeed"/> is not reproducible, and this is the only way to learn
    /// which seed would reproduce it.
    /// </summary>
    public long Seed { get; init; }

    /// <summary>How long the call took, including transfer.</summary>
    public int Ms { get; internal set; }

    /// <summary>How many attempts were made, when retrying was enabled.</summary>
    public int Attempts { get; internal set; }
    #endregion

    #region Methods (internal)
    internal static TrellisResult Fail(
        TrellisFailure failure, string remedy, bool retryable, string? error = null,
        int ms = 0, int attempts = 0) =>
        new()
        {
            Success = false, Failure = failure, Remedy = remedy,
            Retryable = retryable, Error = error, Ms = ms, Attempts = attempts
        };
    #endregion
}
#endregion
