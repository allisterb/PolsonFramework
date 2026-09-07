namespace Polson.ExtendedMind.DocumentProcessing;

using System.Collections.Generic;

/// <summary>
/// Request and result types for document processing.
/// </summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script reading <c>answer.text</c> reaches <c>Text</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c>.
/// </remarks>
public static class Documents
{
    /// <summary>Largest document accepted inline, in bytes.</summary>
    /// <remarks>
    /// Inline data rides in the request body, and the service caps the whole request at roughly 20 MB.
    /// Refusing here — before the bytes are read and before the budget is touched — is a better
    /// failure than a rejected request that has already cost a call, and the message can name the
    /// remedy rather than the limit.
    /// </remarks>
    public const int MaxInlineBytes = 18 * 1024 * 1024;

    /// <summary>What the service is told a document is, keyed by file extension.</summary>
    /// <remarks>
    /// An allowlist rather than a guess, because the MIME type is what decides whether the document
    /// is parsed as a document at all. An unknown extension is refused by name — sending an unknown
    /// type produces a plausible answer about nothing, which is the failure this project treats as
    /// worst.
    /// </remarks>
    public static readonly IReadOnlyDictionary<string, string> MimeTypes = new Dictionary<string, string>(
        System.StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".txt"] = "text/plain",
        [".md"] = "text/markdown",
        [".csv"] = "text/csv",
        [".json"] = "application/json",
        [".xml"] = "text/xml",
        [".html"] = "text/html",
        [".htm"] = "text/html",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".webp"] = "image/webp",
    };
}

#region Requests

/// <summary>Options for <c>Documents.ask(...)</c>.</summary>
public sealed record DocumentOptions
{
    /// <summary>Override the default model for this call.</summary>
    public string? Model { get; init; }

    /// <summary>
    /// MIME type, when the source does not carry a usable extension.
    /// </summary>
    /// <remarks>Required for a <c>byte[]</c> source, which has no name to read a type from.</remarks>
    public string? MimeType { get; init; }
}

#endregion

#region Results

/// <summary>
/// An answer read out of a supplied document.
/// </summary>
/// <remarks>
/// <para>
/// <b>Failure is returned rather than thrown</b>, exactly as it is for a requisition and for a
/// photograph, and for the same reason: the right response differs per failure. An exhausted budget
/// wants the figure sourced another way, an unreadable document wants a different file, and a
/// transport fault wants the same call again. Check <see cref="Success"/>, then read
/// <see cref="Remedy"/>.
/// </para>
/// <para>
/// <b><see cref="Warnings"/> is the part that makes this safe to expose.</b> A document is supplied
/// from outside and its text reaches an agent verbatim, so it is the sharpest injection surface in
/// the studio: a PDF can carry a paragraph addressed to whoever is processing it, and the model will
/// faithfully relay it. The answer is scanned on the way in and the concealment classes are stripped;
/// what was found is reported here rather than silently removed.
/// </para>
/// </remarks>
public sealed record DocumentAnswer
{
    public bool Success { get; init; }

    /// <summary>The answer, scanned and stripped of concealment characters. Empty on failure.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>The failure as a readable name — <c>'None'</c> when it succeeded.</summary>
    public string FailureName => Failure.ToString();

    /// <summary>The same value as a number. Prefer <see cref="FailureName"/>.</summary>
    public DocumentFailure Failure { get; init; }

    /// <summary>What to do next, in words.</summary>
    public string Remedy => DocumentRemedy.For(Failure);

    /// <summary>Whether repeating the identical request could succeed.</summary>
    public bool Retryable => Failure is DocumentFailure.RateLimited or DocumentFailure.Network
        or DocumentFailure.Timeout or DocumentFailure.ServiceError;

    /// <summary>The underlying message, when there was one.</summary>
    public string? Error { get; init; }

    /// <summary>
    /// What the scan found in the answer. <b>Empty is the expected result.</b>
    /// </summary>
    /// <remarks>
    /// A finding is not proof the answer is wrong, but it is a reason to read the document before
    /// acting on it: <b>a document that talks to whoever is processing it is not behaving like a
    /// document.</b> Report what was found and treat the answer as suspect.
    /// </remarks>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>Which document this answers, and what was asked of it.</summary>
    public DocumentProvenance? Provenance { get; init; }
}

/// <summary>Where an answer came from. Attached to every successful read.</summary>
/// <remarks>
/// Without it, "did the agent read this or recall it" is unanswerable — the same question
/// <c>Provenance</c> answers for a requisitioned asset, and the reason a figure drawn from a document
/// can be traced back to the document.
/// </remarks>
public sealed record DocumentProvenance
{
    /// <summary>Model that answered.</summary>
    public required string Model { get; init; }

    /// <summary>How the document was named to the caller — a project-relative path, or its size.</summary>
    public required string Source { get; init; }

    /// <summary>MIME type the document was sent as.</summary>
    public required string MimeType { get; init; }

    /// <summary>Bytes sent.</summary>
    public required int Bytes { get; init; }

    /// <summary>SHA-256 of the document, so two answers about one file are recognisably about it.</summary>
    public required string Hash { get; init; }

    /// <summary>The question, verbatim. Prompts leave the machine; this is the audit record.</summary>
    public required string Query { get; init; }

    public required System.DateTime ReadUtc { get; init; }

    /// <summary>Tokens billed for this call, as reported by the service.</summary>
    public long TokensSpent { get; init; }
}

/// <summary>Why a document read did not produce an answer.</summary>
public enum DocumentFailure
{
    None = 0,

    /// <summary>No API key, so nothing was attempted.</summary>
    NotConfigured = 1,

    /// <summary>The allowance is spent.</summary>
    BudgetExhausted = 2,

    /// <summary>The path resolved outside the project directory, or the file is not there.</summary>
    NotFound = 3,

    /// <summary>Larger than the service accepts inline.</summary>
    TooLarge = 4,

    /// <summary>The extension is not one this surface knows how to declare a type for.</summary>
    UnsupportedType = 5,

    /// <summary>The query was empty, so there was nothing to ask.</summary>
    NoQuery = 6,

    /// <summary>The model declined on safety grounds.</summary>
    SafetyBlocked = 7,

    /// <summary>A 200 carrying no text.</summary>
    NoAnswer = 8,

    RateLimited = 9,
    Network = 10,
    Timeout = 11,
    Auth = 12,
    ServiceError = 13,
    InvalidRequest = 14,
    Cancelled = 15,
}

/// <summary>What to do about each failure, in words the agent can act on.</summary>
internal static class DocumentRemedy
{
    public static string For(DocumentFailure failure) => failure switch
    {
        DocumentFailure.None => string.Empty,
        DocumentFailure.NotConfigured =>
            "Document processing is not configured on this server, so nothing was attempted. Source "
            + "the figures another way and say in the piece where they came from.",
        DocumentFailure.BudgetExhausted =>
            "The document allowance is spent. Reuse an answer you already have rather than asking "
            + "again, and do not fill the gap with a plausible number.",
        DocumentFailure.NotFound =>
            "No such document. The path is relative to the project directory, exactly as outFile is — "
            + "check the spelling, and that the file was actually placed there.",
        DocumentFailure.TooLarge =>
            "Larger than the service accepts inline. Extract the pages you need and ask about those, "
            + "rather than sending the whole volume.",
        DocumentFailure.UnsupportedType =>
            "This surface does not know what type to declare for that extension, and guessing "
            + "produces a confident answer about nothing. Pass an explicit mimeType, or convert it.",
        DocumentFailure.NoQuery =>
            "A document read needs a question. State what you want out of it, including the units and "
            + "the period, as specifically as you would to a researcher.",
        DocumentFailure.SafetyBlocked =>
            "The model declined to answer. Reword the question; repeating it unchanged will not help.",
        DocumentFailure.NoAnswer =>
            "The service answered with no text. Treat this as no answer at all rather than as an "
            + "empty one, and do not draw a blank in its place.",
        DocumentFailure.RateLimited => "Throttled. The same request will work shortly.",
        DocumentFailure.Network => "The service was not reachable. Retrying is reasonable.",
        DocumentFailure.Timeout => "The service did not answer in time. Retrying is reasonable.",
        DocumentFailure.Auth => "The key was rejected. This is configuration, not something to work around.",
        DocumentFailure.ServiceError => "The service returned an error. Retrying is reasonable.",
        DocumentFailure.InvalidRequest => "The request was malformed. Read the error and change the call.",
        DocumentFailure.Cancelled => "Cancelled before it finished.",
        _ => "Unrecognised failure.",
    };
}

/// <summary>
/// The document allowance, readable so an agent can plan rather than hit a wall.
/// </summary>
/// <remarks>
/// Counted in <b>reads</b> rather than in money, for the same reason <see cref="Documents"/>'s
/// sibling budgets are: a count is knowable before a call and a bill is not.
/// </remarks>
public sealed class DocumentBudget(int total)
{
    public int Total { get; } = total;

    public int Spent { get; internal set; }

    public int Remaining => System.Math.Max(0, Total - Spent);

    /// <summary>Reads served from cache, which cost nothing and are not charged.</summary>
    public int CacheHits { get; internal set; }

    /// <summary>Tokens billed this session, summed from what the service reported per call.</summary>
    public long TokensSpent { get; internal set; }

    public bool CanAfford(int count = 1) => Remaining >= count;
}

#endregion
