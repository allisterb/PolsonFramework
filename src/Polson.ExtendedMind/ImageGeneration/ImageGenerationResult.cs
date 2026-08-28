namespace Polson.ExtendedMind.ImageGeneration;

using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Outcome of one cloud image generation, successful or not.
/// </summary>
/// <remarks>
/// <para>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script reading <c>result.imageBytes</c> reaches
/// <c>ImageBytes</c>. The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the
/// studio manuals.
/// </para>
/// <para>
/// Generation is a metered network call against a service that rate-limits, blocks on safety, and
/// occasionally returns a response carrying no image at all. Throwing on each of those would make
/// every requisition a trap the script has to guard, and an agent that cannot tell "the service is
/// briefly busy" from "rephrase your descriptor" will retry the wrong thing. So failure is a value:
/// check <see cref="Success"/>, then read <see cref="Failure"/> and <see cref="Remedy"/> to decide
/// whether to retry, reword, or stop.
/// </para>
/// </remarks>
public class ImageGenerationResult
{
    #region Constructors
    public ImageGenerationResult()
    {
        Logs = new List<string>();
        Prompt = string.Empty;
        Model = string.Empty;
        Hash = string.Empty;
    }
    #endregion

    #region Properties
    public bool Success { get; set; }

    public byte[]? ImageBytes { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public string MimeType { get; set; } = "image/png";

    /// <summary>Model that actually served the request, which may differ from the one asked for.</summary>
    public string Model { get; set; }

    /// <summary>Verbatim prompt sent upstream. Prompts leave the machine; this is the audit record.</summary>
    public string Prompt { get; set; }

    /// <summary>Content address of (model, prompt, params). Also the cache key.</summary>
    public string Hash { get; set; }

    public DateTime GeneratedUtc { get; set; } = DateTime.UtcNow;

    public long ElapsedMs { get; set; }

    /// <summary>True when served from the requisition cache. A cache hit costs nothing.</summary>
    public bool FromCache { get; set; }

    /// <summary>
    /// True when this attempt consumed budget. A refused or failed call should leave this false so a
    /// transient network fault does not silently spend the allowance.
    /// </summary>
    public bool Charged { get; set; }

    /// <summary>Tokens billed for the request side, as reported by the service.</summary>
    public int? PromptTokens { get; set; }

    /// <summary>Tokens billed for the generated output. An image is charged as tokens, not as a unit.</summary>
    public int? OutputTokens { get; set; }

    /// <summary>
    /// Total tokens for this call, as reported by the service rather than estimated.
    /// </summary>
    /// <remarks>
    /// Counting generations is a poor proxy for spend: a Pro image and a Flash image both count one,
    /// and a conditioned request carries an extra image in the prompt. This is the real number, and
    /// it is the only one that supports comparing what a plate cost against what a swatch cost.
    /// </remarks>
    public int? TotalTokens { get; set; }

    /// <summary>Classified reason, so the caller can branch without parsing <see cref="Error"/>.</summary>
    public ImageGenerationFailure Failure { get; set; } = ImageGenerationFailure.None;

    /// <summary>Underlying message, for logs and for showing the agent what actually happened.</summary>
    public string? Error { get; set; }

    /// <summary>HTTP status where the failure came from the service, else null.</summary>
    public int? StatusCode { get; set; }

    public List<string> Logs { get; set; }

    /// <summary>Whether retrying the identical request could plausibly succeed.</summary>
    [JsonIgnore]
    public bool Retryable => IsRetryable(Failure);

    /// <summary>
    /// What the caller should do next, phrased for the agent that will read it.
    /// </summary>
    /// <remarks>Uses the JS spelling of calls, because that is what the reader will type.</remarks>
    [JsonIgnore]
    public string Remedy => RemedyFor(Failure);

    [JsonIgnore]
    public string? ImageDataUri => ImageBytes is { Length: > 0 }
        ? $"data:{MimeType};base64,{Convert.ToBase64String(ImageBytes)}"
        : null;
    #endregion

    #region Methods
    /// <summary>Whether retrying an identical request could plausibly succeed.</summary>
    public static bool IsRetryable(ImageGenerationFailure failure) =>
        failure is ImageGenerationFailure.RateLimited
                or ImageGenerationFailure.ServiceError
                or ImageGenerationFailure.Network
                or ImageGenerationFailure.Timeout;

    /// <summary>Next action for a given failure, phrased for the agent that will read it.</summary>
    public static string RemedyFor(ImageGenerationFailure failure) => failure switch
    {
        ImageGenerationFailure.None => "Succeeded.",
        ImageGenerationFailure.Auth => "The API key was rejected. This is a configuration fault, not something a script can fix — stop and report it.",
        ImageGenerationFailure.ModelNotFound => "That model name is not available on this key. Use the default model rather than naming one.",
        ImageGenerationFailure.RateLimited => "The service is rate-limiting. Wait and retry the same request; it will likely succeed.",
        ImageGenerationFailure.Quota => "The account's generation quota is exhausted. Do not retry. Draw it procedurally instead.",
        ImageGenerationFailure.SafetyBlocked => "The descriptor was blocked. Reword it as a plain material — 'weathered oak planking', not a scene or a person.",
        ImageGenerationFailure.Recitation => "Declined as too close to existing material. The descriptor was too generic — add specifics (finish, wear, colour, lighting) rather than retrying the same words.",
        ImageGenerationFailure.InvalidRequest => "The request was malformed or the descriptor was rejected. Simplify the descriptor and try once more.",
        ImageGenerationFailure.NoImageReturned => "The call succeeded but carried no image. Retry once; if it recurs, reword the descriptor.",
        ImageGenerationFailure.ServiceError => "The service failed on its side. Retry the same request after a short pause.",
        ImageGenerationFailure.Network => "The request never reached the service. Retry the same request.",
        ImageGenerationFailure.Timeout => "The request timed out. Retry, or continue without the asset and draw it procedurally.",
        ImageGenerationFailure.Cancelled => "The execution was cancelled.",
        ImageGenerationFailure.BudgetExhausted => "No generation budget remains this session. Draw the material procedurally — check ExtendedMind.budget.remaining before requisitioning.",
        ImageGenerationFailure.RefusedFormRequest => "This asked for an object rather than a material. Draw the form with the drawing toolkit and requisition its surface instead.",
        ImageGenerationFailure.NotConfigured => "This studio has image generation disabled — no credentials are configured. Draw the material procedurally; requisitioning will not work at all this session.",
        ImageGenerationFailure.ConstraintNotMet => "The image arrived but broke its contract — most often the quiet region came back full of detail. Retry, or relax the constraint and compose around what you get.",
        _ => "Unrecognised failure. Continue without the asset.",
    };

    /// <summary>Builds a failure result. Public so layers wrapping generation can report their own.</summary>
    public static ImageGenerationResult Failed(
        ImageGenerationFailure failure, string error, string prompt, string model, int? statusCode = null) =>
        new()
        {
            Success = false,
            Failure = failure,
            Error = error,
            Prompt = prompt,
            Model = model,
            StatusCode = statusCode,
        };
    #endregion
}

/// <summary>Why a generation did not produce an image. Drives retry-versus-reword decisions.</summary>
public enum ImageGenerationFailure
{
    None = 0,

    /// <summary>Key rejected. Configuration fault; a script cannot recover.</summary>
    Auth = 1,

    /// <summary>The named model is not reachable on this key.</summary>
    ModelNotFound = 2,

    /// <summary>Throttled. Retrying the same request is correct.</summary>
    RateLimited = 3,

    /// <summary>Account allowance is gone. Retrying is not.</summary>
    Quota = 4,

    /// <summary>Blocked by the safety filter. The descriptor must change.</summary>
    SafetyBlocked = 5,

    /// <summary>
    /// Declined as recitation: the descriptor was generic enough that the output would have
    /// reproduced existing material. Observed live on "plain grey concrete". Retrying the same
    /// words recites again, so this needs a more specific descriptor rather than another attempt.
    /// </summary>
    Recitation = 15,

    /// <summary>Rejected as malformed.</summary>
    InvalidRequest = 6,

    /// <summary>
    /// A 200 that carried no image part. Distinct from a transport failure, and it must never be
    /// treated as an empty image: a silently blank asset reads downstream as a deliberate blank.
    /// </summary>
    NoImageReturned = 7,

    /// <summary>5xx from the service.</summary>
    ServiceError = 8,

    /// <summary>The request never reached the service.</summary>
    Network = 9,

    Timeout = 10,

    Cancelled = 11,

    /// <summary>The session's generation budget is spent. Refused before any network call.</summary>
    BudgetExhausted = 12,

    /// <summary>Refused by the form-versus-substance check, before any network call.</summary>
    RefusedFormRequest = 13,

    /// <summary>
    /// This studio has no image-generation credentials configured, so requisition is unavailable for
    /// the whole session. Distinct from an exhausted budget: no amount of waiting or rewording helps.
    /// </summary>
    NotConfigured = 16,

    /// <summary>
    /// An image arrived but broke the contract it was requisitioned under — most often a backdrop
    /// whose requested quiet region came back full of detail. Delivering it anyway would put a
    /// mountain range where the foreground is about to go.
    /// </summary>
    ConstraintNotMet = 14,

    Unknown = 99,
}
