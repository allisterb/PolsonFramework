namespace Polson.ExtendedMind.ParallelSearch;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

#region Caller-facing options

/// <summary>Search mode preset. <see cref="Default"/> omits the field and lets the service choose.</summary>
public enum SearchMode
{
    /// <summary>Field omitted; the service defaults to <see cref="Advanced"/>.</summary>
    Default = 0,

    /// <summary>Fastest responses, lowest quality.</summary>
    Turbo,

    /// <summary>High quality within a ~1 second budget.</summary>
    Fast,

    /// <summary>Low latency; works best with 2-3 well-chosen queries.</summary>
    Basic,

    /// <summary>Highest quality, slowest. What a sourced infographic wants.</summary>
    Advanced,
}

/// <summary>
/// Optional search settings. Everything here is omitted from the request when left unset, so the
/// service applies its own defaults rather than ours.
/// </summary>
public sealed record SearchOptions
{
    /// <summary>Upper bound on results. The service defaults to 10.</summary>
    public int? MaxResults { get; init; }

    public SearchMode Mode { get; init; } = SearchMode.Default;

    /// <summary>Upper bound on characters across every excerpt in the response.</summary>
    public int? MaxCharsTotal { get; init; }

    /// <summary>Upper bound on excerpt characters per result.</summary>
    public int? MaxCharsPerResult { get; init; }

    /// <summary>
    /// Restricts results to these domains or domain/path prefixes. When non-empty this is the whole
    /// allowlist and <see cref="ExcludeDomains"/> is ignored — the service's rule, not ours.
    /// </summary>
    public IReadOnlyList<string>? IncludeDomains { get; init; }

    public IReadOnlyList<string>? ExcludeDomains { get; init; }

    /// <summary>Only content published on or after this date.</summary>
    public DateOnly? AfterDate { get; init; }

    /// <summary>ISO 3166-1 alpha-2 country code for geo-targeted results.</summary>
    public string? Location { get; init; }

    /// <summary>
    /// Carries context between calls in one piece of work. Pass the <c>SessionId</c> a previous
    /// search or extract returned, so a later extract knows what the search was for.
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>The model consuming the results. Lets the service tailor excerpt defaults.</summary>
    public string? ClientModel { get; init; }
}

/// <summary>Optional extract settings. As with <see cref="SearchOptions"/>, unset means omitted.</summary>
public sealed record ExtractOptions
{
    /// <summary>
    /// Keyword queries used with the objective to choose which passages become excerpts. Distinct
    /// from a search: these do not choose the pages, only what is quoted from them.
    /// </summary>
    public IReadOnlyList<string>? SearchQueries { get; init; }

    /// <summary>
    /// Requests the whole page as markdown alongside the excerpts. Off by default, because it is
    /// much larger and is billed as its own SKU.
    /// </summary>
    public bool FullContent { get; init; }

    /// <summary>
    /// Truncates full content to this many characters, measured from the top of the page. Implies
    /// <see cref="FullContent"/>.
    /// </summary>
    public int? FullContentMaxChars { get; init; }

    public int? MaxCharsTotal { get; init; }

    public int? MaxCharsPerResult { get; init; }

    public string? SessionId { get; init; }

    public string? ClientModel { get; init; }
}
#endregion

#region Response content

/// <summary>One page returned by Search.</summary>
public sealed record WebSearchResult
{
    #region Properties
    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    /// <summary>Page title, when the service could determine one.</summary>
    /// <remarks>
    /// Present on nearly every result, but not always a human-readable title — a live run returned
    /// <c>http://iea.org/reports/global-energy-review-2025/electricity</c> here. Anything printing
    /// this in a caption should expect a URL occasionally.
    /// </remarks>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>Publish date as the service gave it, normally <c>YYYY-MM-DD</c>. May be absent.</summary>
    /// <remarks>
    /// Far sparser than <see cref="Title"/> — one of five results carried a date in a live run — so a
    /// dated citation cannot be assumed. <see cref="Cite"/> omits what is missing rather than
    /// substituting a placeholder.
    /// </remarks>
    [JsonPropertyName("publish_date")]
    public string? PublishDate { get; init; }

    /// <summary>Relevant passages, as markdown.</summary>
    [JsonPropertyName("excerpts")]
    public IReadOnlyList<string> Excerpts { get; init; } = [];

    /// <summary>
    /// <see cref="PublishDate"/> parsed, or null when absent or unparseable. The string stays
    /// authoritative — a date the service spells unexpectedly is still worth citing.
    /// </summary>
    [JsonIgnore]
    public DateOnly? PublishedOn => ParallelCitation.ParseDate(PublishDate);
    #endregion

    #region Methods
    /// <summary>A caption-ready citation: title, publisher host, and date where each is known.</summary>
    public string Cite() => ParallelCitation.Format(Title, Url, PublishDate);
    #endregion
}

/// <summary>One page returned by Extract.</summary>
public sealed record ExtractedPage
{
    #region Properties
    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("publish_date")]
    public string? PublishDate { get; init; }

    /// <summary>Passages selected against the objective, as markdown.</summary>
    [JsonPropertyName("excerpts")]
    public IReadOnlyList<string> Excerpts { get; init; } = [];

    /// <summary>
    /// The whole page as markdown, present only when <see cref="ExtractOptions.FullContent"/> asked
    /// for it. Null otherwise — which is not the same as an empty page.
    /// </summary>
    [JsonPropertyName("full_content")]
    public string? FullContent { get; init; }

    [JsonIgnore]
    public DateOnly? PublishedOn => ParallelCitation.ParseDate(PublishDate);
    #endregion

    #region Methods
    public string Cite() => ParallelCitation.Format(Title, Url, PublishDate);
    #endregion
}

/// <summary>A URL Extract was asked for and could not return.</summary>
/// <remarks>
/// Reported per URL rather than failing the call, so nineteen good pages survive one unreachable
/// twentieth. Check <see cref="ParallelExtractResponse.Errors"/> whenever the result count is short.
/// </remarks>
public sealed record ExtractFailure
{
    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    [JsonPropertyName("error_type")]
    public string ErrorType { get; init; } = string.Empty;

    [JsonPropertyName("http_status_code")]
    public int? HttpStatusCode { get; init; }

    /// <summary>Body returned for an HTTP error, when there was one.</summary>
    [JsonPropertyName("content")]
    public string? Content { get; init; }
}

/// <summary>A non-fatal warning about the request.</summary>
/// <remarks>
/// <see cref="Type"/> is a string rather than an enum on purpose: the service documents new warning
/// types as a backward-compatible change, so an enum would turn a routine addition into a parse
/// failure that loses the whole response.
/// </remarks>
public sealed record ParallelWarning
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("detail")]
    public JsonElement? Detail { get; init; }
}

/// <summary>Billed usage for one call, by SKU.</summary>
public sealed record ParallelUsage
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("count")]
    public int Count { get; init; }
}
#endregion

#region Failure classification

/// <summary>Why a Parallel call did not return content. Drives retry-versus-reword decisions.</summary>
public enum ParallelFailure
{
    None = 0,

    /// <summary>No API key configured. Nothing this session will fix it.</summary>
    NotConfigured = 1,

    /// <summary>Key rejected. A configuration fault; retrying is pointless.</summary>
    Auth = 2,

    /// <summary>Rejected as malformed, or refused by our own checks before any call.</summary>
    InvalidRequest = 3,

    /// <summary>Throttled. Retrying the identical request after a pause is correct.</summary>
    RateLimited = 4,

    /// <summary>Account allowance exhausted. Retrying is not.</summary>
    Quota = 5,

    /// <summary>The service failed on its side.</summary>
    ServiceError = 6,

    /// <summary>The request never reached the service.</summary>
    Network = 7,

    Timeout = 8,

    Cancelled = 9,

    /// <summary>
    /// A 200 whose body did not parse. Kept distinct from <see cref="ServiceError"/> because it is
    /// the failure a broken binding produces, and the body is retained so it can be read.
    /// </summary>
    MalformedResponse = 10,

    Unknown = 99,
}
#endregion

#region Call outcomes

/// <summary>Fields every Parallel call reports whether or not it succeeded.</summary>
/// <remarks>
/// Failure is a value rather than an exception, matching <c>ImageGenerationResult</c>: the caller two
/// layers up has to choose between retrying, rewording and giving up, and cannot make that choice
/// from a stack trace.
/// </remarks>
public abstract record ParallelResponse
{
    #region Properties
    /// <remarks>
    /// The outcome fields are <c>internal set</c> rather than <c>init</c> because the client fills
    /// them in after deserialization, and C# has no <c>with</c> expression on a generic type
    /// parameter. They are read-only to every caller outside this assembly.
    /// </remarks>
    [JsonIgnore]
    public bool Success { get; internal set; }

    [JsonIgnore]
    public ParallelFailure Failure { get; internal set; } = ParallelFailure.None;

    /// <summary>Underlying message, for logs and for showing an agent what happened.</summary>
    [JsonIgnore]
    public string? Error { get; internal set; }

    /// <summary>HTTP status where the failure came from the service, else null.</summary>
    [JsonIgnore]
    public int? StatusCode { get; internal set; }

    /// <summary>The service's own reference for this error, worth quoting in a support request.</summary>
    [JsonIgnore]
    public string? ErrorRefId { get; internal set; }

    /// <summary>
    /// The response body that failed, truncated. Present only on a failure — the generated binding's
    /// habit of discarding it is what made its faults so hard to place.
    /// </summary>
    [JsonIgnore]
    public string? RawBody { get; internal set; }

    [JsonIgnore]
    public long ElapsedMs { get; internal set; }

    /// <summary>Session identifier. Pass it to the next call in the same piece of work.</summary>
    [JsonPropertyName("session_id")]
    public string SessionId { get; init; } = string.Empty;

    [JsonPropertyName("warnings")]
    public IReadOnlyList<ParallelWarning>? Warnings { get; init; }

    [JsonPropertyName("usage")]
    public IReadOnlyList<ParallelUsage>? Usage { get; init; }

    /// <summary>Whether repeating the identical request could plausibly succeed.</summary>
    [JsonIgnore]
    public bool Retryable => IsRetryable(Failure);

    /// <summary>What to do next, phrased for the agent that will read it.</summary>
    [JsonIgnore]
    public string Remedy => RemedyFor(Failure);
    #endregion

    #region Methods
    public static bool IsRetryable(ParallelFailure failure) =>
        failure is ParallelFailure.RateLimited
                or ParallelFailure.ServiceError
                or ParallelFailure.Network
                or ParallelFailure.Timeout;

    public static string RemedyFor(ParallelFailure failure) => failure switch
    {
        ParallelFailure.None => "Succeeded.",
        ParallelFailure.NotConfigured => "No Parallel API key is configured, so search and extract are unavailable for the whole session. Source the figures another way and say so.",
        ParallelFailure.Auth => "The API key was rejected. A configuration fault, not something a query can fix — stop and report it.",
        ParallelFailure.InvalidRequest => "The request was rejected as malformed. Read Error: it names the field. Extract takes at most 20 absolute http(s) URLs; search needs at least one non-empty query.",
        ParallelFailure.RateLimited => "The service is rate-limiting. Wait and repeat the same request.",
        ParallelFailure.Quota => "The account's Parallel quota is exhausted. Do not retry.",
        ParallelFailure.ServiceError => "The service failed on its side. Retry the same request after a short pause.",
        ParallelFailure.Network => "The request never reached the service. Retry the same request.",
        ParallelFailure.Timeout => "The request timed out. Advanced mode is slow — retry, or drop to Fast mode.",
        ParallelFailure.Cancelled => "The call was cancelled.",
        ParallelFailure.MalformedResponse => "The service replied with something this client could not read. Read RawBody, which holds the response verbatim. Do not retry until you know why.",
        _ => "Unrecognised failure. Continue without the sources, and do not cite what you did not receive.",
    };
    #endregion
}

/// <summary>Outcome of one Search call.</summary>
public sealed record ParallelSearchResponse : ParallelResponse
{
    /// <summary>Search ID, e.g. <c>search_cad0a6d2dec046bd95ae900527d880e7</c>.</summary>
    [JsonPropertyName("search_id")]
    public string SearchId { get; init; } = string.Empty;

    /// <summary>Results, most relevant first. Empty on failure, and empty is not a failure.</summary>
    [JsonPropertyName("results")]
    public IReadOnlyList<WebSearchResult> Results { get; init; } = [];
}

/// <summary>Outcome of one Extract call.</summary>
public sealed record ParallelExtractResponse : ParallelResponse
{
    /// <summary>Extract ID, e.g. <c>extract_cad0a6d2dec046bd95ae900527d880e7</c>.</summary>
    [JsonPropertyName("extract_id")]
    public string ExtractId { get; init; } = string.Empty;

    [JsonPropertyName("results")]
    public IReadOnlyList<ExtractedPage> Results { get; init; } = [];

    /// <summary>URLs that could not be extracted. A populated list alongside results is normal.</summary>
    [JsonPropertyName("errors")]
    public IReadOnlyList<ExtractFailure> Errors { get; init; } = [];
}
#endregion

#region Citation formatting

/// <summary>
/// Turns what a result carries into something an infographic can print under a figure.
/// </summary>
/// <remarks>
/// Deliberately small: it states only what the service returned, and omits any part that is absent
/// rather than inventing a placeholder. A caption reading "n.d." is a claim about the source; a
/// caption that simply omits the date is not.
/// </remarks>
public static class ParallelCitation
{
    #region Methods
    /// <summary>Parses the service's <c>YYYY-MM-DD</c> date, or null when absent or unparseable.</summary>
    public static DateOnly? ParseDate(string? publishDate) =>
        DateOnly.TryParse(publishDate, System.Globalization.CultureInfo.InvariantCulture, out var date)
            ? date
            : null;

    /// <summary>
    /// The publisher's host, with any leading <c>www.</c> dropped. Null for anything that is not an
    /// absolute web URL — <c>mailto:</c> has a host too, and naming its domain as a publisher would
    /// be a small invention rather than a citation.
    /// </summary>
    public static string? Publisher(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host
            : null;

    /// <summary>
    /// <c>Title — publisher, 2025-10-08</c>, with each absent part left out. Falls back to the bare
    /// URL when nothing else is known, so a citation is never empty while a source exists.
    /// </summary>
    public static string Format(string? title, string? url, string? publishDate)
    {
        var publisher = Publisher(url);
        var parts = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(publisher)) parts.Add(publisher!);
        if (!string.IsNullOrWhiteSpace(publishDate)) parts.Add(publishDate!);

        var trail = string.Join(", ", parts);
        return (title, trail) switch
        {
            (null or "", "") => url ?? string.Empty,
            (null or "", _) => trail,
            (_, "") => title!,
            _ => $"{title} — {trail}",
        };
    }
    #endregion
}
#endregion
