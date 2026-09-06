namespace Polson.ExtendedMind.ParallelSearch;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Typed client for Parallel's Search and Extract endpoints — a natural-language objective to a list
/// of sourced URLs, and a URL to formatted text with the provenance to cite it.
/// </summary>
/// <remarks>
/// <para>
/// Hand-written rather than generated. The NSwag binding in <c>ParallelSearchApi.gen.cs</c> is
/// unusable for these two endpoints: the spec spells every optional field as
/// <c>anyOf: [{type: X}, {type: "null"}]</c>, which NSwag 13.18 rendered against System.Text.Json as
/// a class carrying no value at all. That made <c>objective</c> impossible to send and made any
/// response carrying a real <c>title</c> throw on the way back in. See
/// <c>tests/Polson.Tests.ExtendedMind/ParallelSearchApiTests.cs</c>, which pins each defect. Only two
/// of some forty operations are needed here, so the generator was earning very little.
/// </para>
/// <para>
/// Nothing here throws for a remote fault. Every call returns a <see cref="ParallelResponse"/>
/// carrying a classified <see cref="ParallelFailure"/> and a <see cref="ParallelResponse.Remedy"/>,
/// the same contract as <c>ImageGenerationResult</c>. A malformed response keeps its body in
/// <see cref="ParallelResponse.RawBody"/> rather than discarding it.
/// </para>
/// <para>
/// The caps in the constants below are the service's, taken from
/// <c>docs.parallel.ai/search/best-practices</c> and <c>docs.parallel.ai/extract/best-practices</c>
/// as of 2026-09-06, and are not discoverable from the OpenAPI spec. If a call starts being refused
/// or accepted unexpectedly, check those pages before changing anything here.
/// </para>
/// <para>
/// This type is <b>not</b> exposed to the JavaScript sandbox. Search and extract are metered calls
/// that fetch untrusted text from the open web; putting them in front of an agent needs a budget, a
/// cache and a content boundary, which is the layer above this one — the same split as
/// <c>ImageGenerator</c> and <c>AssetRequisitionToolkit</c>.
/// </para>
/// </remarks>
public sealed class ParallelClient : Runtime, IDisposable
{
    #region Constructors
    /// <param name="apiKey">Read from <c>ApiKeys:Parallel</c>. Sent as <c>x-api-key</c> per request.</param>
    /// <param name="httpClient">
    /// Optional. When supplied it is neither mutated nor disposed — the key travels on the request,
    /// not on the client's default headers, so one <see cref="HttpClient"/> can be shared.
    /// </param>
    /// <param name="baseUrl">Origin only, no path. Overridable for a test double or a proxy.</param>
    public ParallelClient(string apiKey, HttpClient? httpClient = null, string? baseUrl = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        // A key carrying a newline or control character would otherwise throw from the first call
        // rather than from construction, which reads as a service fault instead of a config one.
        if (apiKey.Any(char.IsControl))
        {
            throw new ArgumentException("The Parallel API key contains a control character.", nameof(apiKey));
        }

        this.apiKey = apiKey;
        this.baseUrl = (baseUrl ?? DefaultBaseUrl).TrimEnd('/');
        this.ownsHttp = httpClient is null;

        // A client we own carries no timeout of its own, because every call sets its own deadline and
        // they differ by an order of magnitude — 120s for a search, minutes for a task result. One
        // client-wide timeout cannot serve both, and would silently cut the long poll short.
        this.http = httpClient ?? new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    }
    #endregion

    #region Properties
    public const string DefaultBaseUrl = "https://api.parallel.ai";

    /// <summary>The service's own ceiling. Passing more is rejected before any call is made.</summary>
    public const int MaxUrlsPerExtract = 20;

    /// <summary>
    /// The documented cap on queries per call. Beyond this the service <b>silently drops the
    /// extras</b> and returns a validation warning rather than an error, so a sixth query looks
    /// accepted and never runs. Refusing locally is the honest reading of that.
    /// </summary>
    public const int MaxSearchQueries = 5;

    /// <summary>Documented cap on one query. Queries are meant to be 3-6 keywords, not sentences.</summary>
    public const int MaxQueryChars = 200;

    /// <summary>Documented cap on the objective, for both endpoints.</summary>
    public const int MaxObjectiveChars = 5000;

    /// <summary>Documented cap on a session identifier.</summary>
    public const int MaxSessionIdChars = 1000;

    /// <summary>
    /// Word count past which a query is warned about as prose rather than keywords. <b>Ours, not the
    /// service's</b> — the documented guidance is 3-6 words, which is advice rather than a limit, so
    /// this sits well clear of it and only catches a query that is plainly a sentence.
    /// </summary>
    public const int ProseWordThreshold = 12;

    /// <summary>Deadline for a search or extract. Advanced mode is slow; the BCL's 100s is tight.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(120);

    /// <summary>
    /// How long <see cref="AwaitTask"/> asks the service to hold a result open for. Comfortably past
    /// a <c>core</c> run (1-5 min) and short of a <c>pro</c> one (3-9 min), which wants collecting in
    /// more than one wait.
    /// </summary>
    public static readonly TimeSpan DefaultTaskWait = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Added to the service's own wait before our deadline fires, so a run that finishes just as the
    /// long poll expires returns its answer instead of being reported as our timeout.
    /// </summary>
    public static readonly TimeSpan TaskWaitMargin = TimeSpan.FromSeconds(30);

    /// <summary>Documented caps on a task's metadata entries.</summary>
    public const int MaxMetadataKeyChars = 16;

    /// <inheritdoc cref="MaxMetadataKeyChars"/>
    public const int MaxMetadataValueChars = 512;

    /// <summary>Cap on the body kept in <see cref="ParallelResponse.RawBody"/> after a failure.</summary>
    public const int MaxRetainedBodyChars = 4000;
    #endregion

    #region Methods
    /// <summary>
    /// Searches the web and returns ranked URLs with excerpts.
    /// </summary>
    /// <param name="objective">
    /// The research goal in natural language, with enough context to stand alone, plus any source or
    /// freshness requirement. Optional to the service and worth always supplying: it is what focuses
    /// the excerpts on the passage you actually need. At most
    /// <see cref="MaxObjectiveChars"/> characters.
    /// </param>
    /// <param name="searchQueries">
    /// Keyword queries, 3-6 words each; two or three covering different angles work best. At least
    /// one is required and at most <see cref="MaxSearchQueries"/> are accepted. Keywords, not
    /// sentences, and no <c>site:</c> operators — use <see cref="SearchOptions.IncludeDomains"/> for
    /// that.
    /// </param>
    public async Task<ParallelSearchResponse> Search(
        string? objective,
        IEnumerable<string> searchQueries,
        SearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var queries = (searchQueries ?? []).Where(q => !string.IsNullOrWhiteSpace(q)).ToArray();
        if (queries.Length == 0)
        {
            return Refuse<ParallelSearchResponse>(
                "Search needs at least one non-empty query in searchQueries.");
        }

        options ??= new SearchOptions();
        if (CheckDocumentedLimits(objective, options.SessionId, queries) is string reason)
        {
            return Refuse<ParallelSearchResponse>(reason);
        }

        AdviseOnQueries(queries);

        var body = new SearchRequestWire
        {
            SearchQueries = queries,
            Objective = Trimmed(objective),
            Mode = ModeName(options.Mode),
            MaxCharsTotal = options.MaxCharsTotal,
            SessionId = Trimmed(options.SessionId),
            ClientModel = Trimmed(options.ClientModel),
            AdvancedSettings = SearchAdvancedWire.From(options),
        };

        return await Call<ParallelSearchResponse>("/v1/search", body, cancellationToken);
    }

    /// <summary>
    /// Extracts formatted text from specific URLs, selected against an objective.
    /// </summary>
    /// <param name="urls">Absolute http(s) URLs, at most <see cref="MaxUrlsPerExtract"/>.</param>
    /// <param name="objective">
    /// What the extraction is for, including the broader task context, in at most
    /// <see cref="MaxObjectiveChars"/> characters. This is what makes an excerpt the relevant passage
    /// rather than the top of the page, so it is the field that decides whether the call was worth
    /// making — and without it, or without
    /// <see cref="ExtractOptions.SearchQueries"/>, asking for full content merely repeats the
    /// excerpts.
    /// </param>
    public async Task<ParallelExtractResponse> Extract(
        IEnumerable<string> urls,
        string? objective,
        ExtractOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var targets = (urls ?? []).Where(u => !string.IsNullOrWhiteSpace(u)).ToArray();
        if (targets.Length == 0)
        {
            return Refuse<ParallelExtractResponse>("Extract needs at least one URL.");
        }

        if (targets.Length > MaxUrlsPerExtract)
        {
            return Refuse<ParallelExtractResponse>(
                $"Extract takes at most {MaxUrlsPerExtract} URLs; {targets.Length} were given. Split the batch.");
        }

        var malformed = targets.Where(u => !IsAbsoluteWebUrl(u)).ToArray();
        if (malformed.Length > 0)
        {
            return Refuse<ParallelExtractResponse>(
                $"Not absolute http(s) URLs: {string.Join(", ", malformed)}");
        }

        options ??= new ExtractOptions();
        if (CheckDocumentedLimits(objective, options.SessionId, options.SearchQueries) is string reason)
        {
            return Refuse<ParallelExtractResponse>(reason);
        }

        if (options.SearchQueries is { Count: > 0 } queries) AdviseOnQueries(queries);

        // Documented as redundant rather than invalid: with nothing to rank against, the excerpts are
        // just the top of the page, which the full content already contains. Still worth saying.
        if ((options.FullContent || options.FullContentMaxChars > 0) &&
            Trimmed(objective) is null && options.SearchQueries is not { Count: > 0 })
        {
            Warn("Parallel extract asked for full content with no objective and no search queries, "
               + "so the excerpts will duplicate it. Supply an objective.");
        }

        var body = new ExtractRequestWire
        {
            Urls = targets,
            Objective = Trimmed(objective),
            SearchQueries = options.SearchQueries is { Count: > 0 } q ? q : null,
            MaxCharsTotal = options.MaxCharsTotal,
            SessionId = Trimmed(options.SessionId),
            ClientModel = Trimmed(options.ClientModel),
            AdvancedSettings = ExtractAdvancedWire.From(options),
        };

        return await Call<ParallelExtractResponse>("/v1/extract", body, cancellationToken);
    }

    /// <summary>
    /// Starts a deep-research task and returns as soon as it is queued, without waiting for it.
    /// </summary>
    /// <param name="input">
    /// The question, as a string — or any object, which is serialized as the task's JSON input.
    /// </param>
    /// <param name="spec">
    /// What to produce. <see cref="TaskSpec.Json"/> for a table or record set,
    /// <see cref="TaskSpec.Text"/> for prose, <see cref="TaskSpec.Auto"/> to let the service decide.
    /// </param>
    /// <remarks>
    /// A run takes between seconds and tens of minutes depending on the processor, which is why this
    /// hands back a <see cref="ParallelTaskRun.RunId"/> rather than an answer. Keep the id, get on
    /// with other work, and use <see cref="CheckTask"/> or <see cref="AwaitTask"/> to collect it.
    /// </remarks>
    public async Task<ParallelTaskRun> StartTask(
        object input,
        TaskSpec? spec = null,
        TaskOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (input is null || (input is string text && string.IsNullOrWhiteSpace(text)))
        {
            return Refuse<ParallelTaskRun>("A task needs an input.");
        }

        options ??= new TaskOptions();
        if (string.IsNullOrWhiteSpace(options.Processor))
        {
            return Refuse<ParallelTaskRun>(
                $"A task needs a processor, e.g. TaskProcessor.{nameof(TaskProcessor.Core)}.");
        }

        if (CheckMetadata(options.Metadata) is string metadataReason)
        {
            return Refuse<ParallelTaskRun>(metadataReason);
        }

        var body = new TaskRunRequestWire
        {
            Input = input,
            Processor = options.Processor.Trim(),
            TaskSpec = spec is null ? null : new TaskSpecWire { OutputSchema = spec.Output, InputSchema = spec.Input },
            SourcePolicy = SourcePolicyWire.From(options.IncludeDomains, options.ExcludeDomains, options.AfterDate),
            AdvancedSettings = Trimmed(options.Location) is string location
                ? new TaskAdvancedWire { Location = location }
                : null,
            Metadata = options.Metadata,
            EnableEvents = options.EnableEvents,
            PreviousInteractionId = Trimmed(options.PreviousInteractionId),
        };

        var run = await Call<ParallelTaskRun>("/v1/tasks/runs", body, cancellationToken);
        if (run.Success) Info("Parallel task {RunId} started on {Processor}", run.RunId, run.Processor);
        return run;
    }

    /// <summary>
    /// Reads a run's status without waiting. Cheap, and safe to call as often as a workflow likes.
    /// </summary>
    /// <remarks>
    /// This is the call for the pattern where work continues while the research runs: start the task,
    /// carry on, and check between stages. <see cref="ParallelTaskRun.IsActive"/> says whether it is
    /// still going; <see cref="ParallelTaskRun.IsCompleted"/> says there is a result to collect.
    /// </remarks>
    public async Task<ParallelTaskRun> CheckTask(string runId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(runId)) return Refuse<ParallelTaskRun>("A run id is required.");

        return await Send<ParallelTaskRun>(
            HttpMethod.Get, $"/v1/tasks/runs/{Uri.EscapeDataString(runId.Trim())}", null, null, cancellationToken);
    }

    /// <summary>
    /// Collects a run's result, waiting up to <paramref name="waitFor"/> for it to finish.
    /// </summary>
    /// <param name="waitFor">
    /// How long the service should hold the connection open. Defaults to
    /// <see cref="DefaultTaskWait"/>. A run longer than this is not lost — the call returns a
    /// <see cref="ParallelFailure.Timeout"/> and the same id can be awaited again.
    /// </param>
    /// <remarks>
    /// A server-side long poll rather than a busy loop, so it costs nothing to wait. Because the wait
    /// legitimately exceeds an ordinary request, the deadline is set per call — but an
    /// <b>injected</b> <see cref="HttpClient"/> keeps its own <c>Timeout</c>, which will cut a long
    /// wait short unless the caller raised it.
    /// </remarks>
    public async Task<ParallelTaskResult> AwaitTask(
        string runId, TimeSpan? waitFor = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(runId)) return Refuse<ParallelTaskResult>("A run id is required.");

        var wait = waitFor ?? DefaultTaskWait;
        if (wait <= TimeSpan.Zero) return Refuse<ParallelTaskResult>("The wait must be positive.");

        var seconds = (int)Math.Ceiling(wait.TotalSeconds);
        var result = await Send<ParallelTaskResult>(
            HttpMethod.Get,
            $"/v1/tasks/runs/{Uri.EscapeDataString(runId.Trim())}/result?timeout={seconds}",
            null,
            // Past the service's own wait, so its answer arrives rather than our deadline firing first.
            wait + TaskWaitMargin,
            cancellationToken);

        if (result.Failure == ParallelFailure.Timeout)
        {
            Info("Parallel task {RunId} still running after {Seconds}s; await it again.", runId, seconds);
        }

        return result;
    }

    public void Dispose()
    {
        if (ownsHttp) http.Dispose();
    }
    #endregion

    #region Internal methods
    /// <summary>Posts a body and parses the result. What Search and Extract both use.</summary>
    private Task<TResponse> Call<TResponse>(string path, object body, CancellationToken cancellationToken)
        where TResponse : ParallelResponse, new() =>
        Send<TResponse>(HttpMethod.Post, path, body, DefaultTimeout, cancellationToken);

    /// <summary>
    /// The documented caps on task metadata. Returns the reason to refuse, or null.
    /// </summary>
    private static string? CheckMetadata(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null) return null;

        foreach (var (key, value) in metadata)
        {
            if (key.Length > MaxMetadataKeyChars)
            {
                return $"Metadata key \"{key}\" is {key.Length} characters; the limit is {MaxMetadataKeyChars}.";
            }

            if (value?.Length > MaxMetadataValueChars)
            {
                return $"Metadata value for \"{key}\" is {value.Length} characters; "
                     + $"the limit is {MaxMetadataValueChars}.";
            }
        }

        return null;
    }

    /// <summary>
    /// Issues one request, classifies the outcome, and parses on success. The single path every call
    /// takes.
    /// </summary>
    /// <param name="deadline">
    /// How long to allow before giving up, overriding the <see cref="HttpClient"/>'s own timeout.
    /// Needed because collecting a task result is a server-side long poll that legitimately runs far
    /// longer than an ordinary call — and because an injected client's timeout is not ours to change.
    /// </param>
    private async Task<TResponse> Send<TResponse>(
        HttpMethod method,
        string path,
        object? body,
        TimeSpan? deadline,
        CancellationToken cancellationToken)
        where TResponse : ParallelResponse, new()
    {
        var stopwatch = Stopwatch.StartNew();
        HttpStatusCode httpStatus;
        string responseBody;

        using var deadlineSource = deadline is { } d ? new CancellationTokenSource(d) : null;
        using var linked = deadlineSource is null
            ? null
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadlineSource.Token);
        var token = linked?.Token ?? cancellationToken;

        try
        {
            using var request = new HttpRequestMessage(method, baseUrl + path);
            if (body is not null)
            {
                request.Content = new StringContent(
                    JsonSerializer.Serialize(body, body.GetType(), JsonOptions), Encoding.UTF8, "application/json");
            }

            request.Headers.Add("x-api-key", apiKey);
            request.Headers.Accept.ParseAdd("application/json");

            using var response = await http.SendAsync(request, token);
            httpStatus = response.StatusCode;
            responseBody = await response.Content.ReadAsStringAsync(token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failed<TResponse>(ParallelFailure.Cancelled, "The call was cancelled.", stopwatch);
        }
        catch (OperationCanceledException ex)
        {
            // Our own deadline, or the HttpClient's. Either way the caller did not ask to stop.
            return Failed<TResponse>(ParallelFailure.Timeout, ex.Message, stopwatch);
        }
        catch (HttpRequestException ex)
        {
            return Failed<TResponse>(ParallelFailure.Network, ex.Message, stopwatch);
        }

        var status = (int)httpStatus;
        if (status is < 200 or > 299)
        {
            var (message, refId) = ReadErrorEnvelope(responseBody);
            Error("Parallel {Path} failed with {Status}: {Message}", path, status, message);
            return Failed<TResponse>(ClassifyStatus(httpStatus), message, stopwatch, status, refId, responseBody);
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<TResponse>(responseBody, JsonOptions);
            if (parsed is null)
            {
                return Failed<TResponse>(
                    ParallelFailure.MalformedResponse, "The response body was null.", stopwatch, status,
                    raw: responseBody);
            }

            parsed.Success = true;
            parsed.Failure = ParallelFailure.None;
            parsed.StatusCode = status;
            parsed.ElapsedMs = stopwatch.ElapsedMilliseconds;
            return parsed;
        }
        catch (JsonException ex)
        {
            // The whole point of replacing the generated binding: say what broke, and keep the body.
            return Failed<TResponse>(
                ParallelFailure.MalformedResponse, ex.Message, stopwatch, status, raw: responseBody);
        }
    }

    /// <summary>
    /// The caps the service documents, checked before spending anything. Returns the reason to
    /// refuse, or null to proceed.
    /// </summary>
    /// <remarks>
    /// Only the query count is enforced by us where the service would not fail: it drops queries past
    /// the fifth and reports a warning, so a caller who sends six gets five searched and a success.
    /// The rest would be rejected upstream anyway, and refusing here costs a round trip less.
    /// </remarks>
    private static string? CheckDocumentedLimits(
        string? objective, string? sessionId, IReadOnlyList<string>? queries)
    {
        if (objective?.Length > MaxObjectiveChars)
        {
            return $"The objective is {objective.Length} characters; the limit is {MaxObjectiveChars}.";
        }

        if (sessionId?.Length > MaxSessionIdChars)
        {
            return $"The session id is {sessionId.Length} characters; the limit is {MaxSessionIdChars}.";
        }

        if (queries is null) return null;

        if (queries.Count > MaxSearchQueries)
        {
            return $"{queries.Count} search queries were given; the service accepts {MaxSearchQueries} "
                 + "and silently drops the rest. Choose the ones that matter.";
        }

        var overlong = queries.FirstOrDefault(q => q.Length > MaxQueryChars);
        return overlong is null
            ? null
            : $"A search query is {overlong.Length} characters; the limit is {MaxQueryChars}. "
            + $"Queries are keywords, not prose: \"{overlong[..40]}…\"";
    }

    /// <summary>
    /// Logs the quality problems the docs call out. These degrade results rather than failing, so
    /// they are warnings — refusing a call the service would happily serve is not ours to do.
    /// </summary>
    private static void AdviseOnQueries(IReadOnlyList<string> queries)
    {
        foreach (var query in queries)
        {
            if (query.Contains("site:", StringComparison.OrdinalIgnoreCase))
            {
                Warn("Parallel query {Query} uses a site: operator, which is not supported. "
                   + "Use SearchOptions.IncludeDomains instead.", query);
            }

            var words = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
            if (words > ProseWordThreshold)
            {
                Warn("Parallel query {Query} is {Words} words. Queries are keywords, 3-6 words; "
                   + "put the sentence in the objective instead.", query, words);
            }
        }
    }

    /// <summary>Refused by our own checks, before any network call and before any spend.</summary>
    private static T Refuse<T>(string reason) where T : ParallelResponse, new() =>
        new() { Success = false, Failure = ParallelFailure.InvalidRequest, Error = reason };

    private static T Failed<T>(
        ParallelFailure failure,
        string? error,
        Stopwatch stopwatch,
        int? status = null,
        string? refId = null,
        string? raw = null) where T : ParallelResponse, new() =>
        new()
        {
            Success = false,
            Failure = failure,
            Error = error,
            StatusCode = status,
            ErrorRefId = refId,
            RawBody = Truncate(raw),
            ElapsedMs = stopwatch.ElapsedMilliseconds,
        };

    private static ParallelFailure ClassifyStatus(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => ParallelFailure.Auth,
        HttpStatusCode.PaymentRequired => ParallelFailure.Quota,
        HttpStatusCode.TooManyRequests => ParallelFailure.RateLimited,
        HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout => ParallelFailure.Timeout,
        HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity or HttpStatusCode.NotFound
            => ParallelFailure.InvalidRequest,
        >= HttpStatusCode.InternalServerError => ParallelFailure.ServiceError,
        _ => ParallelFailure.Unknown,
    };

    /// <summary>
    /// Reads whichever error envelope came back, falling back to the raw body.
    /// </summary>
    /// <remarks>
    /// There are two, and only one is documented. The application returns
    /// <c>{ type, error: { ref_id, message } }</c>; a rejected key is answered by the gateway in
    /// front of it with a gRPC-shaped <c>{ code, message }</c> and no <c>ref_id</c> — observed live
    /// on a 401. An error body that is itself unreadable must not replace the error with a parse
    /// failure, because the status is what actually went wrong.
    /// </remarks>
    private static (string Message, string? RefId) ReadErrorEnvelope(string body)
    {
        try
        {
            var root = JsonDocument.Parse(body).RootElement;

            if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
            {
                var message = error.TryGetProperty("message", out var m) ? m.GetString() : null;
                var refId = error.TryGetProperty("ref_id", out var r) ? r.GetString() : null;
                return (message ?? Truncate(body) ?? string.Empty, refId);
            }

            // The gateway's flat shape, and any other body carrying a top-level message.
            if (root.TryGetProperty("message", out var flat) && flat.ValueKind == JsonValueKind.String)
            {
                return (flat.GetString() ?? string.Empty, null);
            }
        }
        catch (JsonException)
        {
            // Not JSON at all — an HTML error page from a proxy, say. The body is the best we have.
        }

        return (Truncate(body) ?? string.Empty, null);
    }

    private static bool IsAbsoluteWebUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Truncate(string? body) => body is null
        ? null
        : body.Length <= MaxRetainedBodyChars ? body : body[..MaxRetainedBodyChars] + "…[truncated]";

    private static string? ModeName(SearchMode mode) => mode switch
    {
        SearchMode.Turbo => "turbo",
        SearchMode.Fast => "fast",
        SearchMode.Basic => "basic",
        SearchMode.Advanced => "advanced",
        _ => null,
    };
    #endregion

    #region Fields
    /// <summary>
    /// Null is omitted rather than written, which is how an unset optional stays unset. Writing
    /// <c>"objective": null</c> instead would be a different request from not sending the field.
    /// </summary>
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string apiKey;
    private readonly string baseUrl;
    private readonly HttpClient http;
    private readonly bool ownsHttp;
    #endregion

    #region Child Types
    private sealed record SearchRequestWire
    {
        [JsonPropertyName("search_queries")] public required IReadOnlyList<string> SearchQueries { get; init; }

        [JsonPropertyName("objective")] public string? Objective { get; init; }

        [JsonPropertyName("mode")] public string? Mode { get; init; }

        [JsonPropertyName("max_chars_total")] public int? MaxCharsTotal { get; init; }

        [JsonPropertyName("session_id")] public string? SessionId { get; init; }

        [JsonPropertyName("client_model")] public string? ClientModel { get; init; }

        [JsonPropertyName("advanced_settings")] public SearchAdvancedWire? AdvancedSettings { get; init; }
    }

    private sealed record SearchAdvancedWire
    {
        [JsonPropertyName("max_results")] public int? MaxResults { get; init; }

        [JsonPropertyName("location")] public string? Location { get; init; }

        [JsonPropertyName("excerpt_settings")] public ExcerptWire? ExcerptSettings { get; init; }

        [JsonPropertyName("source_policy")] public SourcePolicyWire? SourcePolicy { get; init; }

        /// <summary>Null when nothing was asked for, so the object is omitted rather than sent empty.</summary>
        internal static SearchAdvancedWire? From(SearchOptions options)
        {
            var sourcePolicy = SourcePolicyWire.From(
                options.IncludeDomains, options.ExcludeDomains, options.AfterDate);
            var excerpts = ExcerptWire.From(options.MaxCharsPerResult);
            var location = Trimmed(options.Location);

            return (options.MaxResults, location, excerpts, sourcePolicy) is (null, null, null, null)
                ? null
                : new SearchAdvancedWire
                {
                    MaxResults = options.MaxResults,
                    Location = location,
                    ExcerptSettings = excerpts,
                    SourcePolicy = sourcePolicy,
                };
        }
    }

    private sealed record SourcePolicyWire
    {
        [JsonPropertyName("include_domains")] public IReadOnlyList<string>? IncludeDomains { get; init; }

        [JsonPropertyName("exclude_domains")] public IReadOnlyList<string>? ExcludeDomains { get; init; }

        [JsonPropertyName("after_date")] public DateOnly? AfterDate { get; init; }

        internal static SourcePolicyWire? From(
            IReadOnlyList<string>? includeDomains, IReadOnlyList<string>? excludeDomains, DateOnly? afterDate)
        {
            var include = includeDomains is { Count: > 0 } ? includeDomains : null;
            var exclude = excludeDomains is { Count: > 0 } ? excludeDomains : null;

            return (include, exclude, afterDate) is (null, null, null)
                ? null
                : new SourcePolicyWire
                {
                    IncludeDomains = include,
                    ExcludeDomains = exclude,
                    AfterDate = afterDate,
                };
        }
    }

    private sealed record TaskRunRequestWire
    {
        [JsonPropertyName("input")] public required object Input { get; init; }

        [JsonPropertyName("processor")] public required string Processor { get; init; }

        [JsonPropertyName("task_spec")] public TaskSpecWire? TaskSpec { get; init; }

        [JsonPropertyName("source_policy")] public SourcePolicyWire? SourcePolicy { get; init; }

        [JsonPropertyName("advanced_settings")] public TaskAdvancedWire? AdvancedSettings { get; init; }

        [JsonPropertyName("metadata")] public IReadOnlyDictionary<string, string>? Metadata { get; init; }

        [JsonPropertyName("enable_events")] public bool? EnableEvents { get; init; }

        [JsonPropertyName("previous_interaction_id")] public string? PreviousInteractionId { get; init; }
    }

    private sealed record TaskSpecWire
    {
        [JsonPropertyName("output_schema")] public required object OutputSchema { get; init; }

        [JsonPropertyName("input_schema")] public object? InputSchema { get; init; }
    }

    private sealed record TaskAdvancedWire
    {
        [JsonPropertyName("location")] public string? Location { get; init; }
    }

    private sealed record ExtractRequestWire
    {
        [JsonPropertyName("urls")] public required IReadOnlyList<string> Urls { get; init; }

        [JsonPropertyName("objective")] public string? Objective { get; init; }

        [JsonPropertyName("search_queries")] public IReadOnlyList<string>? SearchQueries { get; init; }

        [JsonPropertyName("max_chars_total")] public int? MaxCharsTotal { get; init; }

        [JsonPropertyName("session_id")] public string? SessionId { get; init; }

        [JsonPropertyName("client_model")] public string? ClientModel { get; init; }

        [JsonPropertyName("advanced_settings")] public ExtractAdvancedWire? AdvancedSettings { get; init; }
    }

    private sealed record ExtractAdvancedWire
    {
        [JsonPropertyName("excerpt_settings")] public ExcerptWire? ExcerptSettings { get; init; }

        /// <summary>
        /// The spec's one union: <c>true</c> to enable with defaults, or an object to bound the
        /// length. Declared as <see cref="object"/> so the runtime type decides which is written —
        /// a bool and a settings object are genuinely different shapes on the wire.
        /// </summary>
        [JsonPropertyName("full_content")] public object? FullContent { get; init; }

        internal static ExtractAdvancedWire? From(ExtractOptions options)
        {
            var excerpts = ExcerptWire.From(options.MaxCharsPerResult);
            object? fullContent = options switch
            {
                { FullContentMaxChars: > 0 } o => new ExcerptWire { MaxCharsPerResult = o.FullContentMaxChars },
                { FullContent: true } => true,
                _ => null,
            };

            return (excerpts, fullContent) is (null, null)
                ? null
                : new ExtractAdvancedWire { ExcerptSettings = excerpts, FullContent = fullContent };
        }
    }

    /// <summary>Shared by excerpt settings and full-content settings; both carry only this field.</summary>
    private sealed record ExcerptWire
    {
        [JsonPropertyName("max_chars_per_result")] public int? MaxCharsPerResult { get; init; }

        internal static ExcerptWire? From(int? maxCharsPerResult) =>
            maxCharsPerResult is > 0 ? new ExcerptWire { MaxCharsPerResult = maxCharsPerResult } : null;
    }
    #endregion
}
