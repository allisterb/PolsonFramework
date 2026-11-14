namespace Polson.ExtendedMind.DocumentProcessing;

using System.Collections.Generic;
using System.Linq;

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
    /// <summary>Where a director's documents live, relative to the project directory.</summary>
    /// <remarks>
    /// <b>A convention rather than a restriction.</b> <c>ask</c> reads any contained path, so a file
    /// elsewhere in the project still works; what the folder buys is that <c>list</c> has somewhere
    /// definite to look and a director has somewhere obvious to put things. Without one, an agent
    /// told "the figures are in the attached report" has no way to find out what was attached.
    /// </remarks>
    public const string Folder = "documents";

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

        // A reference clip the director supplied. Listed here so `Documents.list()` stays the one place an agent
        // finds out what it was given; `Video.ask` is the surface built for watching one.
        [".mp4"] = "video/mp4",
    };
}

/// <summary>One document available to be read.</summary>
/// <remarks>
/// Deliberately not the contents: listing is free and reading is metered, so an agent should be able
/// to see what it has before deciding what to spend on.
/// </remarks>
public sealed record DocumentEntry
{
    /// <summary>Project-relative path, with forward slashes, ready to hand to <c>ask</c>.</summary>
    public required string Path { get; init; }

    /// <summary>File name alone, for a label.</summary>
    public required string Name { get; init; }

    public required int Bytes { get; init; }

    /// <summary>What it would be sent as.</summary>
    public required string MimeType { get; init; }
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

    /// <summary>A JSON Schema the answer must follow — an object, or its JSON text. The answer is then JSON.</summary>
    /// <remarks>Part of the cache key: the same question in a different shape is a different answer.</remarks>
    public object? Schema { get; init; }

    /// <summary>Whether a PDF's chart, diagram and photo pages go to the model as images too; true unless set false.</summary>
    public bool? Images { get; init; }

    /// <summary>The pages to read, as a print dialog takes them: <c>'40-60, 75'</c>, counted from 1 like the <c>[Page n]</c>
    /// markers. Null reads every page.</summary>
    /// <remarks>
    /// For a long document. A 313-page S-1 is about 400k tokens, all of it sent with every question; its contents page
    /// says where each section is, so map it once (<c>pages: '1-8'</c>) and ask each question of the pages that hold it.
    /// Evidence is found only on the pages read. Part of the cache key.
    /// </remarks>
    public string? Pages { get; init; }
}

#endregion

#region Results

/// <summary>Where a phrase is in a document: <c>Documents.find(...)</c>. Free: no model reads anything.</summary>
/// <remarks>JS-exposed: members are PascalCase here and camelCase in scripts (<c>hits</c>, <c>unsearched</c>).</remarks>
public sealed record DocumentSearch
{
    public bool Success { get; init; }

    /// <summary>The failure as a readable name — <c>'None'</c> when it succeeded.</summary>
    public string FailureName => Failure.ToString();

    public DocumentFailure Failure { get; init; }

    public string? Error { get; init; }

    /// <summary>The pages carrying the phrase, in page order, at most <see cref="DocumentProcessor.MaxHits"/>.</summary>
    public DocumentHit[] Hits { get; init; } = [];

    /// <summary>More pages carry it than <see cref="Hits"/> lists: find a longer phrase.</summary>
    public bool More { get; init; }

    /// <summary>Pages in the document.</summary>
    public int Pages { get; init; }

    /// <summary>Pages, from 1, with no text layer that have not been OCR'd yet, so were not searched. Asking a question of
    /// a page OCRs it, and it is searched from then on.</summary>
    public int[] Unsearched { get; init; } = [];
}

/// <summary>One page a phrase is on.</summary>
public sealed record DocumentHit
{
    /// <summary>Counted from 1, as <c>[Page n]</c> and <c>pages</c> count them; not the number printed on the page.</summary>
    public required int Page { get; init; }

    /// <summary>How many times the phrase is on the page.</summary>
    public required int Count { get; init; }

    /// <summary>The first place it is, with the text around it, whitespace collapsed.</summary>
    public required string Context { get; init; }
}

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

    /// <summary>With a <c>schema</c>, <see cref="Text"/> parsed: <c>back.answer.lines</c>. Null for a prose answer, or one
    /// that did not parse.</summary>
    /// <remarks>Two runs read the JSON string in <c>text</c> by eye because nothing said to parse it (lanternkeeperlang1, 2).</remarks>
    public object? Answer { get; init; }

    /// <summary>The failure as a readable name — <c>'None'</c> when it succeeded.</summary>
    public string FailureName => Failure.ToString();

    /// <summary>The same value as a number. Prefer <see cref="FailureName"/>.</summary>
    public DocumentFailure Failure { get; init; }

    /// <summary>What to do next, in words.</summary>
    public string Remedy => DocumentRemedy.For(Failure);

    /// <summary>Whether repeating the identical request could succeed.</summary>
    public bool Retryable => Failure is DocumentFailure.RateLimited or DocumentFailure.Network
        or DocumentFailure.Timeout or DocumentFailure.ServiceError or DocumentFailure.Pending;

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
    /// <remarks>
    /// <b>An array, whichever path filled it.</b> Jint remembers the CLR type it first saw behind a member and casts later
    /// values to it: a fresh answer carried an array and a cached one a <c>List</c>, so the second read of a question in one
    /// server process threw "Unable to cast … List`1 to System.Array" (nvdareport2, 2026-10-20).
    /// </remarks>
    public string[] Warnings { get; init; } = [];

    /// <summary>Which document this answers, and what was asked of it.</summary>
    public DocumentProvenance? Provenance { get; init; }

    /// <summary>
    /// Where in the document the answer says it came from, each quote checked against the text of the page it names.
    /// </summary>
    /// <remarks>
    /// Given when the document was read as text, page by page; empty when it was sent whole to a model that reads
    /// it itself. <b>A figure that is not on its page is the error that matters</b>: models reading long documents
    /// misread digits and undercount, and the check is what catches it before it reaches a chart.
    /// </remarks>
    public DocumentEvidence[] Evidence { get; init; } = [];

    /// <summary>The evidence the check could not find on its page, as <c>"p.55: 102,714"</c>. Empty is the expected result.</summary>
    /// <remarks>An array, not a collection expression: Jint cannot convert the list type the compiler makes for one.</remarks>
    public string[] Unverified => Evidence.Where(e => !e.Found && e.From != "image").Select(e => $"p.{e.Page}: {e.Quote}").ToArray();

    /// <summary>
    /// What the answer read from a page image — a chart's bars, a map, a diagram — as <c>"p.2: Q2'2026 50%"</c>.
    /// </summary>
    /// <remarks>
    /// Not a failure, and <b>not checked, even when <c>found</c></b>: the engine can confirm a figure is in the page's
    /// text, not that it belongs to the bar, quarter or site the model says. On a shareholder letter's map, "1GW sites at
    /// Minnesota, Kansas City and Pennsylvania" was found — the digits are on the page — and two of the three were wrong.
    /// Treat each as a reading of a picture: say so where it is used, and have the director confirm it.
    /// </remarks>
    public string[] FromImages => Evidence.Where(e => e.From == "image").Select(e => $"p.{e.Page}: {e.Quote}").ToArray();
}

/// <summary>One piece of evidence an answer rests on: a page, what it says there, and whether it does.</summary>
public sealed record DocumentEvidence
{
    /// <summary>The PDF's page, counted from 1 — not the number printed on it.</summary>
    public required int Page { get; init; }

    /// <summary>The words or figures, as the model quoted them.</summary>
    public required string Quote { get; init; }

    /// <summary>
    /// Whether the page carries it: every figure in the quote, or the quote itself when it has no figure.
    /// </summary>
    /// <remarks>
    /// Figures rather than the whole quote, because a quote stitched from a table row ("Graphics $ 22,459") is never
    /// found word for word in text where each cell is its own line, while the figure is.
    /// </remarks>
    public required bool Found { get; init; }

    /// <summary><c>'text'</c>, or <c>'image'</c> when the model read it from the page's image rather than its text.</summary>
    public string From { get; init; } = "text";
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
    /// <remarks>On a cache hit this is what the <i>original</i> read cost; the hit itself cost nothing.</remarks>
    public long TokensSpent { get; init; }

    /// <summary>True when this answer was served from cache and nothing was spent.</summary>
    /// <remarks>
    /// Paired with <see cref="ReadUtc"/>, which is likewise the original read's time. Together they
    /// say how old the answer being used is — a cached answer to a document that has since been
    /// replaced is the one way this surface can be quietly stale, and the hash is what settles it.
    /// </remarks>
    public bool FromCache { get; init; }

    /// <summary>How the document reached the model: <c>'inline'</c> (sent whole), <c>'text'</c> (its text layer), or <c>'text+ocr'</c>.</summary>
    public string Route { get; init; } = "inline";

    /// <summary>Pages read as text — only those chosen, when <c>pages</c> chose some; 0 when the document was sent whole.</summary>
    public int Pages { get; init; }

    /// <summary>Why the first-choice model did not answer, when another did; null when it did.</summary>
    public string? Fallback { get; init; }

    /// <summary>Which service served Nemotron: <c>'nim'</c> or <c>'nebius'</c>; null when Nemotron did not answer.</summary>
    public string? Endpoint { get; init; }

    /// <summary>The pages also sent as images: the PDF's chart, diagram and photo pages, at most 8.</summary>
    public int[] ImagePages { get; init; } = [];
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

    /// <summary>Still being read when the wait ran out; the answer is on its way to the cache.</summary>
    Pending = 16,
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
            "Larger than the service accepts inline; the error gives the limit. For a document, extract the pages you need "
            + "and ask about those, rather than sending the whole volume. For a video, encode a smaller copy (720p, or a "
            + "lower quality) and ask about that; Verify.film makes one itself.",
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
        DocumentFailure.Pending =>
            "Still reading: a long document can take longer than a script has. The read carries on and its answer is "
            + "kept — run the script again in half a minute and the same question is answered from the cache, at no "
            + "further cost. Do not reword the question meanwhile: a different question is a new read.",
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
