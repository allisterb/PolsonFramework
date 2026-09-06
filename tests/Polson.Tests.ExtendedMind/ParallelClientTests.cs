namespace Polson.Tests.ExtendedMind;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using global::Polson.ExtendedMind.ParallelSearch;
using global::Polson.Tests;
using Xunit;

/// <summary>
/// Tests for the hand-written <see cref="ParallelClient"/> that replaced the NSwag binding.
/// </summary>
/// <remarks>
/// <para>
/// The first region is the point of the exercise. The NSwag binding this replaced could express
/// neither <c>objective</c> nor read back a <c>title</c>, and those tests assert that both now work.
/// </para>
/// <para>
/// Everything is offline except the Live region, which is gated behind
/// <c>POLSON_LIVE_PARALLEL_TESTS=1</c> because it bills.
/// </para>
/// </remarks>
public class ParallelClientTests : TestsRuntime
{
    #region Fields
    /// <summary>A search response exactly as the service returns one, titles and dates included.</summary>
    private const string SearchResponseRealistic = """
        {
          "search_id": "search_cad0a6d2dec046bd95ae900527d880e7",
          "session_id": "sess_7f21",
          "results": [
            {
              "url": "https://www.iea.org/reports/renewables-2025",
              "title": "Renewables 2025 - Analysis - IEA",
              "publish_date": "2025-10-08",
              "excerpts": ["Global renewable capacity additions reached 666 GW in 2024.", "Solar PV accounted for three quarters."]
            },
            {
              "url": "https://ourworldindata.org/renewable-energy",
              "title": null,
              "publish_date": null,
              "excerpts": ["Share of electricity from renewables, by country."]
            }
          ],
          "usage": [{ "name": "search-advanced", "count": 1 }],
          "warnings": [{ "type": "input_validation_warning", "message": "max_results clamped to 10", "detail": null }]
        }
        """;

    private const string ExtractResponseRealistic = """
        {
          "extract_id": "extract_cad0a6d2dec046bd95ae900527d880e7",
          "session_id": "sess_7f21",
          "results": [
            {
              "url": "https://www.iea.org/reports/renewables-2025",
              "title": "Renewables 2025 - Analysis - IEA",
              "publish_date": "2025-10-08",
              "excerpts": ["## Capacity\n666 GW added in 2024."],
              "full_content": "# Renewables 2025\n\nGlobal renewable capacity additions reached 666 GW."
            }
          ],
          "errors": [
            { "url": "https://example.org/paywalled", "error_type": "fetch_failed", "http_status_code": 403, "content": "Forbidden" }
          ],
          "usage": [{ "name": "extract-full-content", "count": 1 }]
        }
        """;

    private const string MinimalSearchResponse = """
        { "search_id": "search_1", "session_id": "sess_1", "results": [] }
        """;

    private const string MinimalExtractResponse = """
        { "extract_id": "extract_1", "session_id": "sess_1", "results": [], "errors": [] }
        """;

    private const string ErrorEnvelope = """
        { "type": "error", "error": { "ref_id": "err_9c1", "message": "search_queries must contain at least one query", "detail": null } }
        """;
    #endregion

    #region The objectives the generated binding could not meet

    /// <summary>
    /// The field the whole workflow turns on. <c>Defect_NullableFieldWrapperCarriesNoValue</c> shows
    /// the generated binding could not express it at all.
    /// </summary>
    [Fact]
    public async Task Search_SendsTheObjectiveAsAString()
    {
        var (client, handler) = MakeClient(MinimalSearchResponse);

        await client.Search(
            "Establish how much renewable generating capacity was added worldwide during 2024.",
            ["renewable capacity additions 2024", "IEA renewables report"]);

        var objective = Sent(handler).GetProperty("objective");
        Assert.Equal(JsonValueKind.String, objective.ValueKind);
        Assert.StartsWith("Establish how much renewable", objective.GetString());
    }

    [Fact]
    public async Task Extract_SendsTheObjectiveAsAString()
    {
        var (client, handler) = MakeClient(MinimalExtractResponse);

        await client.Extract(
            ["https://www.iea.org/reports/renewables-2025"],
            "Find the 2024 capacity addition figure and the year it covers.");

        var objective = Sent(handler).GetProperty("objective");
        Assert.Equal(JsonValueKind.String, objective.ValueKind);
        Assert.Contains("2024 capacity addition", objective.GetString());
    }

    /// <summary>
    /// A response carrying real titles and dates. This is the payload that destroyed the generated
    /// binding entirely — see <c>Defect_SearchResponseWithARealTitleFailsEntirely</c>.
    /// </summary>
    [Fact]
    public async Task Search_ReadsTitlesDatesAndExcerpts()
    {
        var (client, _) = MakeClient(SearchResponseRealistic);

        var response = await client.Search("objective", ["renewable capacity 2024"]);

        Assert.True(response.Success, response.Error);
        Assert.Equal(ParallelFailure.None, response.Failure);
        Assert.Equal("search_cad0a6d2dec046bd95ae900527d880e7", response.SearchId);
        Assert.Equal("sess_7f21", response.SessionId);
        Assert.Equal(2, response.Results.Count);

        var first = response.Results[0];
        Assert.Equal("https://www.iea.org/reports/renewables-2025", first.Url);
        Assert.Equal("Renewables 2025 - Analysis - IEA", first.Title);
        Assert.Equal("2025-10-08", first.PublishDate);
        Assert.Equal(new DateOnly(2025, 10, 8), first.PublishedOn);
        Assert.Equal(2, first.Excerpts.Count);

        // A genuinely absent title stays absent rather than breaking the result beside it.
        Assert.Null(response.Results[1].Title);
        Assert.Single(response.Results[1].Excerpts);
    }

    /// <summary>Full content and provenance together — the two things Extract is for.</summary>
    [Fact]
    public async Task Extract_ReadsFullContentAndProvenance()
    {
        var (client, _) = MakeClient(ExtractResponseRealistic);

        var response = await client.Extract(["https://www.iea.org/reports/renewables-2025"], "objective");

        Assert.True(response.Success, response.Error);
        var page = Assert.Single(response.Results);
        Assert.StartsWith("# Renewables 2025", page.FullContent);
        Assert.Contains("666 GW", page.Excerpts[0]);
        Assert.Equal("Renewables 2025 - Analysis - IEA — iea.org, 2025-10-08", page.Cite());
    }

    /// <summary>
    /// One unreachable URL is reported beside the pages that worked, rather than taking them with it
    /// — the failure <c>Defect_OneFailedUrlDestroysTheWholeExtractResponse</c> pins.
    /// </summary>
    [Fact]
    public async Task Extract_PartialFailureKeepsTheGoodResults()
    {
        var (client, _) = MakeClient(ExtractResponseRealistic);

        var response = await client.Extract(
            ["https://www.iea.org/reports/renewables-2025", "https://example.org/paywalled"], "objective");

        Assert.True(response.Success);
        Assert.Single(response.Results);

        var failed = Assert.Single(response.Errors);
        Assert.Equal("https://example.org/paywalled", failed.Url);
        Assert.Equal("fetch_failed", failed.ErrorType);
        Assert.Equal(403, failed.HttpStatusCode);
        Assert.Equal("Forbidden", failed.Content);
    }

    [Fact]
    public async Task Search_ReadsUsageAndWarnings()
    {
        var (client, _) = MakeClient(SearchResponseRealistic);

        var response = await client.Search("objective", ["q"]);

        var usage = Assert.Single(response.Usage!);
        Assert.Equal("search-advanced", usage.Name);
        Assert.Equal(1, usage.Count);

        var warning = Assert.Single(response.Warnings!);
        Assert.Equal("input_validation_warning", warning.Type);
        Assert.Contains("clamped", warning.Message);
    }

    /// <summary>
    /// The warning vocabulary is a string, not an enum, because the service documents new warning
    /// types as backward-compatible. An unrecognised one must not cost us the response.
    /// </summary>
    [Fact]
    public async Task Search_UnknownWarningTypeDoesNotBreakTheResponse()
    {
        var (client, _) = MakeClient("""
            {
              "search_id": "s", "session_id": "x", "results": [],
              "warnings": [{ "type": "some_warning_invented_next_year", "message": "hello" }]
            }
            """);

        var response = await client.Search("objective", ["q"]);

        Assert.True(response.Success, response.Error);
        Assert.Equal("some_warning_invented_next_year", response.Warnings![0].Type);
    }

    /// <summary>An unknown top-level field is likewise ignored rather than fatal.</summary>
    [Fact]
    public async Task Search_UnknownResponseFieldIsIgnored()
    {
        var (client, _) = MakeClient("""
            { "search_id": "s", "session_id": "x", "results": [], "some_future_field": { "a": 1 } }
            """);

        Assert.True((await client.Search("objective", ["q"])).Success);
    }
    #endregion

    #region Request shape

    [Fact]
    public async Task Search_MinimalRequestOmitsEveryOptional()
    {
        var (client, handler) = MakeClient(MinimalSearchResponse);

        await client.Search(null, ["renewable capacity additions 2024"]);

        var sent = Sent(handler);
        Assert.Equal(
            new[] { "renewable capacity additions 2024" },
            sent.GetProperty("search_queries").EnumerateArray().Select(e => e.GetString()));

        foreach (var name in new[] { "objective", "mode", "max_chars_total", "session_id", "client_model", "advanced_settings" })
            Assert.False(sent.TryGetProperty(name, out _), $"'{name}' should be omitted when unset");
    }

    [Fact]
    public async Task Extract_MinimalRequestOmitsEveryOptional()
    {
        var (client, handler) = MakeClient(MinimalExtractResponse);

        await client.Extract(["https://example.org/a"], null);

        var sent = Sent(handler);
        Assert.Equal(new[] { "https://example.org/a" }, sent.GetProperty("urls").EnumerateArray().Select(e => e.GetString()));

        foreach (var name in new[] { "objective", "search_queries", "max_chars_total", "session_id", "client_model", "advanced_settings" })
            Assert.False(sent.TryGetProperty(name, out _), $"'{name}' should be omitted when unset");
    }

    /// <summary>A blank objective is the same as no objective, and must not become <c>""</c>.</summary>
    [Fact]
    public async Task Search_WhitespaceObjectiveIsOmitted()
    {
        var (client, handler) = MakeClient(MinimalSearchResponse);

        await client.Search("   ", ["q"]);

        Assert.False(Sent(handler).TryGetProperty("objective", out _));
    }

    [Theory]
    [InlineData(SearchMode.Turbo, "turbo")]
    [InlineData(SearchMode.Fast, "fast")]
    [InlineData(SearchMode.Basic, "basic")]
    [InlineData(SearchMode.Advanced, "advanced")]
    public async Task Search_ModeSerializesAsTheDocumentedToken(SearchMode mode, string expected)
    {
        var (client, handler) = MakeClient(MinimalSearchResponse);

        await client.Search("objective", ["q"], new SearchOptions { Mode = mode });

        Assert.Equal(expected, Sent(handler).GetProperty("mode").GetString());
    }

    [Fact]
    public async Task Search_DefaultModeIsOmittedSoTheServiceChooses()
    {
        var (client, handler) = MakeClient(MinimalSearchResponse);

        await client.Search("objective", ["q"], new SearchOptions());

        Assert.False(Sent(handler).TryGetProperty("mode", out _));
    }

    [Fact]
    public async Task Search_AdvancedSettingsMapToTheDocumentedShape()
    {
        var (client, handler) = MakeClient(MinimalSearchResponse);

        await client.Search("objective", ["q"], new SearchOptions
        {
            MaxResults = 5,
            Location = "GB",
            MaxCharsPerResult = 1200,
            IncludeDomains = ["iea.org", "ourworldindata.org"],
            AfterDate = new DateOnly(2024, 1, 1),
        });

        var advanced = Sent(handler).GetProperty("advanced_settings");
        Assert.Equal(5, advanced.GetProperty("max_results").GetInt32());
        Assert.Equal("GB", advanced.GetProperty("location").GetString());
        Assert.Equal(1200, advanced.GetProperty("excerpt_settings").GetProperty("max_chars_per_result").GetInt32());

        var policy = advanced.GetProperty("source_policy");
        Assert.Equal(
            new[] { "iea.org", "ourworldindata.org" },
            policy.GetProperty("include_domains").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("2024-01-01", policy.GetProperty("after_date").GetString());
        Assert.False(policy.TryGetProperty("exclude_domains", out _));
    }

    /// <summary>
    /// <c>full_content</c> is the spec's one union — <c>true</c>, or an object bounding the length.
    /// Both spellings have to reach the wire as the right JSON kind.
    /// </summary>
    [Fact]
    public async Task Extract_FullContentEnabledSendsABoolean()
    {
        var (client, handler) = MakeClient(MinimalExtractResponse);

        await client.Extract(["https://example.org/a"], "objective", new ExtractOptions { FullContent = true });

        var fullContent = Sent(handler).GetProperty("advanced_settings").GetProperty("full_content");
        Assert.Equal(JsonValueKind.True, fullContent.ValueKind);
    }

    [Fact]
    public async Task Extract_FullContentWithALimitSendsAnObjectAndImpliesEnabled()
    {
        var (client, handler) = MakeClient(MinimalExtractResponse);

        // FullContent deliberately left false: asking for a limit is asking for the content.
        await client.Extract(["https://example.org/a"], "objective",
            new ExtractOptions { FullContentMaxChars = 20000 });

        var fullContent = Sent(handler).GetProperty("advanced_settings").GetProperty("full_content");
        Assert.Equal(JsonValueKind.Object, fullContent.ValueKind);
        Assert.Equal(20000, fullContent.GetProperty("max_chars_per_result").GetInt32());
    }

    [Fact]
    public async Task Extract_NoAdvancedSettingsAreSentWhenNoneWereAskedFor()
    {
        var (client, handler) = MakeClient(MinimalExtractResponse);

        await client.Extract(["https://example.org/a"], "objective", new ExtractOptions { SessionId = "sess_1" });

        var sent = Sent(handler);
        Assert.False(sent.TryGetProperty("advanced_settings", out _));
        Assert.Equal("sess_1", sent.GetProperty("session_id").GetString());
    }

    /// <summary>The session carries context between a search and the extract that follows it.</summary>
    [Fact]
    public async Task SessionIdRoundTripsFromSearchIntoExtract()
    {
        var (searchClient, _) = MakeClient(SearchResponseRealistic);
        var search = await searchClient.Search("objective", ["q"]);

        var (extractClient, handler) = MakeClient(MinimalExtractResponse);
        await extractClient.Extract(["https://example.org/a"], "objective",
            new ExtractOptions { SessionId = search.SessionId });

        Assert.Equal("sess_7f21", Sent(handler).GetProperty("session_id").GetString());
    }
    #endregion

    #region Transport

    [Fact]
    public async Task Search_PostsToTheBareOriginPlusThePath()
    {
        var (client, handler) = MakeClient(MinimalSearchResponse);

        await client.Search("objective", ["q"]);

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://api.parallel.ai/v1/search", handler.Request.RequestUri!.ToString());
        Assert.Equal("application/json", handler.Request.Content!.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Extract_PostsToTheBareOriginPlusThePath()
    {
        var (client, handler) = MakeClient(MinimalExtractResponse);

        await client.Extract(["https://example.org/a"], "objective");

        Assert.Equal("https://api.parallel.ai/v1/extract", handler.Request!.RequestUri!.ToString());
    }

    [Fact]
    public async Task ApiKeyTravelsOnEveryRequest()
    {
        var (client, handler) = MakeClient(MinimalSearchResponse);

        await client.Search("objective", ["q"]);

        Assert.Equal("test-key", handler.Request!.Headers.GetValues("x-api-key").Single());
    }

    /// <summary>
    /// The key goes on the request rather than the client's default headers, so a shared or injected
    /// <see cref="HttpClient"/> is not quietly given credentials it will send everywhere else.
    /// </summary>
    [Fact]
    public async Task InjectedHttpClientIsNotMutated()
    {
        var handler = new StubHandler(_ => Respond(MinimalSearchResponse));
        using var http = new HttpClient(handler);
        using var client = new ParallelClient("test-key", http, "https://api.parallel.ai");

        await client.Search("objective", ["q"]);

        Assert.False(http.DefaultRequestHeaders.Contains("x-api-key"));
        Assert.Equal("test-key", handler.Request!.Headers.GetValues("x-api-key").Single());
    }

    [Fact]
    public void Constructor_RejectsAMissingKey()
    {
        Assert.Throws<ArgumentException>(() => new ParallelClient(string.Empty));
        Assert.Throws<ArgumentException>(() => new ParallelClient("   "));
    }

    /// <summary>A base URL given with a trailing slash must not produce a doubled separator.</summary>
    [Fact]
    public async Task BaseUrlTrailingSlashIsNormalised()
    {
        var handler = new StubHandler(_ => Respond(MinimalSearchResponse));
        using var client = new ParallelClient("test-key", new HttpClient(handler), "https://proxy.internal/");

        await client.Search("objective", ["q"]);

        Assert.Equal("https://proxy.internal/v1/search", handler.Request!.RequestUri!.ToString());
    }
    #endregion

    #region Refusals before any network call

    [Fact]
    public Task Search_WithNoQueriesIsRefusedWithoutCallingOut() => AssertSearchRefused([]);

    /// <summary>A list of blanks is an empty list — it must not be sent as a query of spaces.</summary>
    [Fact]
    public Task Search_WithOnlyBlankQueriesIsRefusedWithoutCallingOut() => AssertSearchRefused(["   ", ""]);

    private async Task AssertSearchRefused(string[] queries)
    {
        var (client, handler) = MakeClient(MinimalSearchResponse);

        var response = await client.Search("objective", queries);

        Assert.False(response.Success);
        Assert.Equal(ParallelFailure.InvalidRequest, response.Failure);
        Assert.Null(handler.Request);   // nothing was sent, so nothing was billed
    }

    [Fact]
    public async Task Extract_AboveTwentyUrlsIsRefusedWithTheCount()
    {
        var (client, handler) = MakeClient(MinimalExtractResponse);
        var urls = Enumerable.Range(0, 21).Select(i => $"https://example.org/{i}").ToArray();

        var response = await client.Extract(urls, "objective");

        Assert.False(response.Success);
        Assert.Equal(ParallelFailure.InvalidRequest, response.Failure);
        Assert.Contains("21", response.Error);
        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task Extract_ExactlyTwentyUrlsIsAllowed()
    {
        var (client, handler) = MakeClient(MinimalExtractResponse);
        var urls = Enumerable.Range(0, 20).Select(i => $"https://example.org/{i}").ToArray();

        Assert.True((await client.Extract(urls, "objective")).Success);
        Assert.NotNull(handler.Request);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("example.org/a")]
    [InlineData("ftp://example.org/a")]
    [InlineData("file:///C:/secrets.txt")]
    public async Task Extract_RejectsAnythingButAnAbsoluteWebUrl(string url)
    {
        var (client, handler) = MakeClient(MinimalExtractResponse);

        var response = await client.Extract([url], "objective");

        Assert.False(response.Success);
        Assert.Equal(ParallelFailure.InvalidRequest, response.Failure);
        Assert.Contains(url, response.Error);
        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task Extract_WithNoUrlsIsRefused()
    {
        var (client, handler) = MakeClient(MinimalExtractResponse);

        Assert.False((await client.Extract([], "objective")).Success);
        Assert.Null(handler.Request);
    }
    #endregion

    #region Documented limits

    /// <summary>
    /// The one limit the service does not enforce as an error: past five queries it drops the extras
    /// and returns a validation warning, so a sixth query looks accepted and never runs. Refusing is
    /// the honest reading, and it costs nothing.
    /// </summary>
    [Fact]
    public async Task Search_AboveFiveQueriesIsRefusedRatherThanSilentlyTruncated()
    {
        var (client, handler) = MakeClient(MinimalSearchResponse);
        var queries = Enumerable.Range(0, 6).Select(i => $"query number {i}").ToArray();

        var response = await client.Search("objective", queries);

        Assert.False(response.Success);
        Assert.Equal(ParallelFailure.InvalidRequest, response.Failure);
        Assert.Contains("6", response.Error);
        Assert.Contains("5", response.Error);
        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task Search_ExactlyFiveQueriesIsAllowed()
    {
        var (client, handler) = MakeClient(MinimalSearchResponse);
        var queries = Enumerable.Range(0, 5).Select(i => $"query number {i}").ToArray();

        Assert.True((await client.Search("objective", queries)).Success);
        Assert.Equal(5, Sent(handler).GetProperty("search_queries").GetArrayLength());
    }

    [Fact]
    public async Task Search_AnOverlongQueryIsRefusedWithItsLength()
    {
        var (client, handler) = MakeClient(MinimalSearchResponse);

        var response = await client.Search("objective", [new string('x', 201)]);

        Assert.Equal(ParallelFailure.InvalidRequest, response.Failure);
        Assert.Contains("201", response.Error);
        Assert.Contains("200", response.Error);
        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task Search_AnOverlongObjectiveIsRefused()
    {
        var (client, handler) = MakeClient(MinimalSearchResponse);

        var response = await client.Search(new string('x', 5001), ["q"]);

        Assert.Equal(ParallelFailure.InvalidRequest, response.Failure);
        Assert.Contains("5000", response.Error);
        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task Search_AnOverlongSessionIdIsRefused()
    {
        var (client, handler) = MakeClient(MinimalSearchResponse);

        var response = await client.Search("objective", ["q"],
            new SearchOptions { SessionId = new string('s', 1001) });

        Assert.Equal(ParallelFailure.InvalidRequest, response.Failure);
        Assert.Contains("1000", response.Error);
        Assert.Null(handler.Request);
    }

    /// <summary>Extract's queries are optional, and carry the same caps when supplied.</summary>
    [Fact]
    public async Task Extract_AppliesTheSameQueryLimits()
    {
        var (client, handler) = MakeClient(MinimalExtractResponse);

        var response = await client.Extract(["https://example.org/a"], "objective",
            new ExtractOptions { SearchQueries = Enumerable.Range(0, 6).Select(i => $"q {i}").ToArray() });

        Assert.Equal(ParallelFailure.InvalidRequest, response.Failure);
        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task Extract_AppliesTheObjectiveLimit()
    {
        var (client, handler) = MakeClient(MinimalExtractResponse);

        var response = await client.Extract(["https://example.org/a"], new string('x', 5001));

        Assert.Equal(ParallelFailure.InvalidRequest, response.Failure);
        Assert.Null(handler.Request);
    }

    /// <summary>
    /// The quality guidance is advisory, so a query using a <c>site:</c> operator is warned about and
    /// still sent. Refusing a call the service would happily serve is not ours to do.
    /// </summary>
    [Fact]
    public async Task Search_SiteOperatorIsWarnedAboutButStillSent()
    {
        var (client, handler) = MakeClient(MinimalSearchResponse);

        var response = await client.Search("objective", ["site:iea.org renewable capacity"]);

        Assert.True(response.Success);
        Assert.NotNull(handler.Request);
    }

    [Fact]
    public async Task Search_ProseQueryIsWarnedAboutButStillSent()
    {
        var (client, handler) = MakeClient(MinimalSearchResponse);

        var response = await client.Search("objective",
            ["please tell me how much renewable generating capacity was added during the year 2024"]);

        Assert.True(response.Success);
        Assert.NotNull(handler.Request);
    }

    /// <summary>Documented as redundant rather than invalid, so it warns and proceeds.</summary>
    [Fact]
    public async Task Extract_FullContentWithoutAnObjectiveStillSucceeds()
    {
        var (client, handler) = MakeClient(MinimalExtractResponse);

        var response = await client.Extract(["https://example.org/a"], null,
            new ExtractOptions { FullContent = true });

        Assert.True(response.Success);
        Assert.Equal(JsonValueKind.True,
            Sent(handler).GetProperty("advanced_settings").GetProperty("full_content").ValueKind);
    }
    #endregion

    #region Failure classification

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, ParallelFailure.Auth, false)]
    [InlineData(HttpStatusCode.Forbidden, ParallelFailure.Auth, false)]
    [InlineData(HttpStatusCode.PaymentRequired, ParallelFailure.Quota, false)]
    [InlineData(HttpStatusCode.TooManyRequests, ParallelFailure.RateLimited, true)]
    [InlineData(HttpStatusCode.UnprocessableEntity, ParallelFailure.InvalidRequest, false)]
    [InlineData(HttpStatusCode.BadRequest, ParallelFailure.InvalidRequest, false)]
    [InlineData(HttpStatusCode.InternalServerError, ParallelFailure.ServiceError, true)]
    [InlineData(HttpStatusCode.BadGateway, ParallelFailure.ServiceError, true)]
    [InlineData(HttpStatusCode.GatewayTimeout, ParallelFailure.Timeout, true)]
    public async Task StatusIsClassifiedAndSaysWhetherToRetry(
        HttpStatusCode status, ParallelFailure expected, bool retryable)
    {
        var (client, _) = MakeClient(ErrorEnvelope, status);

        var response = await client.Search("objective", ["q"]);

        Assert.False(response.Success);
        Assert.Equal(expected, response.Failure);
        Assert.Equal((int)status, response.StatusCode);
        Assert.Equal(retryable, response.Retryable);
        Assert.NotEmpty(response.Remedy);
    }

    /// <summary>The documented error envelope is unpacked, so the message names the offending field.</summary>
    [Fact]
    public async Task ErrorEnvelopeIsParsedForMessageAndReference()
    {
        var (client, _) = MakeClient(ErrorEnvelope, HttpStatusCode.UnprocessableEntity);

        var response = await client.Search("objective", ["q"]);

        Assert.Equal("search_queries must contain at least one query", response.Error);
        Assert.Equal("err_9c1", response.ErrorRefId);
        Assert.Contains("search_queries", response.RawBody);
    }

    /// <summary>
    /// A rejected key is answered by the gateway in front of the application, in a gRPC-shaped
    /// <c>{ code, message }</c> rather than the documented envelope. Observed live on a 401, so the
    /// message must be unpacked from this shape too or every auth failure reads as raw JSON.
    /// </summary>
    [Fact]
    public async Task GatewayErrorEnvelopeIsParsedForItsMessage()
    {
        var (client, _) = MakeClient("""{"code":16,"message":"Invalid API key (C.1)"}""", HttpStatusCode.Unauthorized);

        var response = await client.Search("objective", ["q"]);

        Assert.Equal(ParallelFailure.Auth, response.Failure);
        Assert.Equal("Invalid API key (C.1)", response.Error);
        Assert.Null(response.ErrorRefId);
    }

    /// <summary>
    /// An error body that is not the documented envelope must still report the status rather than
    /// being replaced by a parse failure — what went wrong is the 502, not the HTML.
    /// </summary>
    [Fact]
    public async Task NonJsonErrorBodyStillReportsTheStatus()
    {
        var (client, _) = MakeClient("<html><body>502 Bad Gateway</body></html>", HttpStatusCode.BadGateway);

        var response = await client.Search("objective", ["q"]);

        Assert.Equal(ParallelFailure.ServiceError, response.Failure);
        Assert.Contains("502", response.Error);
    }

    /// <summary>
    /// The defect this client exists to fix, inverted: a body that genuinely cannot be read is
    /// reported as its own failure and <b>keeps the body</b>, instead of throwing it away.
    /// </summary>
    [Fact]
    public async Task MalformedSuccessBodyIsReportedAndRetained()
    {
        var (client, _) = MakeClient("""{ "search_id": "s", "results": "not-an-array" }""");

        var response = await client.Search("objective", ["q"]);

        Assert.False(response.Success);
        Assert.Equal(ParallelFailure.MalformedResponse, response.Failure);
        Assert.Contains("not-an-array", response.RawBody);
        Assert.False(response.Retryable);
        Assert.Contains("RawBody", response.Remedy);
    }

    [Fact]
    public async Task LargeMalformedBodyIsTruncatedRatherThanDropped()
    {
        var (client, _) = MakeClient("[" + new string('x', 50_000));

        var response = await client.Search("objective", ["q"]);

        Assert.Equal(ParallelFailure.MalformedResponse, response.Failure);
        Assert.True(response.RawBody!.Length < 5_000, "the retained body should be truncated");
        Assert.EndsWith("[truncated]", response.RawBody);
    }

    [Fact]
    public async Task TransportFailureIsAValueNotAnException()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("no such host"));
        using var client = new ParallelClient("test-key", new HttpClient(handler));

        var response = await client.Search("objective", ["q"]);

        Assert.False(response.Success);
        Assert.Equal(ParallelFailure.Network, response.Failure);
        Assert.True(response.Retryable);
    }

    [Fact]
    public async Task CancellationIsReportedAsCancelledNotAsATimeout()
    {
        var handler = new StubHandler(_ => throw new TaskCanceledException());
        using var client = new ParallelClient("test-key", new HttpClient(handler));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var response = await client.Search("objective", ["q"], null, cts.Token);

        Assert.Equal(ParallelFailure.Cancelled, response.Failure);
        Assert.False(response.Retryable);
    }

    /// <summary>The same exception with our own token unset is the HttpClient timeout, and retryable.</summary>
    [Fact]
    public async Task ClientTimeoutIsReportedAsATimeout()
    {
        var handler = new StubHandler(_ => throw new TaskCanceledException("The request timed out."));
        using var client = new ParallelClient("test-key", new HttpClient(handler));

        var response = await client.Search("objective", ["q"]);

        Assert.Equal(ParallelFailure.Timeout, response.Failure);
        Assert.True(response.Retryable);
    }

    /// <summary>A failed call must not look like an empty one — no results, and no false success.</summary>
    [Fact]
    public async Task AFailedCallReturnsNoResultsAndDoesNotClaimSuccess()
    {
        var (client, _) = MakeClient(ErrorEnvelope, HttpStatusCode.InternalServerError);

        var response = await client.Search("objective", ["q"]);

        Assert.False(response.Success);
        Assert.Empty(response.Results);
    }
    #endregion

    #region Citation

    [Theory]
    [InlineData("Renewables 2025", "https://www.iea.org/reports/x", "2025-10-08", "Renewables 2025 — iea.org, 2025-10-08")]
    [InlineData("Renewables 2025", "https://iea.org/x", null, "Renewables 2025 — iea.org")]
    [InlineData(null, "https://iea.org/x", "2025-10-08", "iea.org, 2025-10-08")]
    [InlineData(null, "https://iea.org/x", null, "iea.org")]
    public void CitationOmitsWhatTheSourceDidNotSupply(string? title, string url, string? date, string expected) =>
        Assert.Equal(expected, ParallelCitation.Format(title, url, date));

    /// <summary>Nothing usable falls back to the URL, so a citation is never blank while a source exists.</summary>
    [Fact]
    public void CitationFallsBackToTheUrl() =>
        Assert.Equal("mailto:x@y.z", ParallelCitation.Format(null, "mailto:x@y.z", null));

    /// <summary>
    /// A date the service spells unexpectedly leaves <c>PublishedOn</c> null while the raw string
    /// survives. The string is what gets cited; the parse is only a convenience.
    /// </summary>
    [Fact]
    public async Task AnUnparseableDateIsKeptVerbatimAndParsesToNull()
    {
        var (client, _) = MakeClient("""
            {
              "search_id": "s", "session_id": "x",
              "results": [{ "url": "https://example.org/a", "excerpts": [], "publish_date": "circa 1954" }]
            }
            """);

        var result = (await client.Search("objective", ["q"])).Results[0];

        Assert.Equal("circa 1954", result.PublishDate);
        Assert.Null(result.PublishedOn);
        Assert.Contains("circa 1954", result.Cite());
    }
    #endregion

    #region Helpers
    private static JsonElement Sent(StubHandler handler) =>
        JsonDocument.Parse(handler.RequestBody!).RootElement.Clone();

    private static HttpResponseMessage Respond(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static (ParallelClient Client, StubHandler Handler) MakeClient(
        string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new StubHandler(_ => Respond(body, status));
        var client = new ParallelClient("test-key", new HttpClient(handler), "https://api.parallel.ai");
        return (client, handler);
    }
    #endregion

    #region Task API

    private const string QueuedRun = """
        {
          "run_id": "trun_abc123", "status": "queued", "is_active": true,
          "processor": "base", "interaction_id": "int_1",
          "created_at": "2026-09-06T00:00:00Z", "modified_at": "2026-09-06T00:00:00Z"
        }
        """;

    /// <summary>A completed JSON run with the per-field basis that is the whole point of the Task API.</summary>
    private const string CompletedJsonResult = """
        {
          "run": {
            "run_id": "trun_abc123", "status": "completed", "is_active": false,
            "processor": "base", "interaction_id": "int_1"
          },
          "output": {
            "type": "json",
            "content": { "mission": "Apollo 11", "duration_hours": 195.3 },
            "basis": [
              {
                "field": "duration_hours",
                "reasoning": "Mission elapsed time reported as 195 hours 18 minutes.",
                "confidence": "high",
                "citations": [
                  { "url": "https://www.nasa.gov/apollo-11", "title": "Apollo 11 - NASA",
                    "excerpts": ["Duration: 195 hours, 18 minutes, 35 seconds"] }
                ]
              },
              { "field": "mission", "reasoning": "Named in the question.", "citations": null }
            ]
          }
        }
        """;

    [Fact]
    public async Task StartTask_PostsInputProcessorAndSpec()
    {
        var (client, handler) = MakeClient(QueuedRun);

        await client.StartTask(
            "Key technical data on Apollo missions.",
            TaskSpec.Json("""{"type":"object","properties":{"mission":{"type":"string"}}}"""),
            new TaskOptions { Processor = TaskProcessor.Core });

        Assert.Equal("https://api.parallel.ai/v1/tasks/runs", handler.Request!.RequestUri!.ToString());
        var sent = Sent(handler);
        Assert.Equal("Key technical data on Apollo missions.", sent.GetProperty("input").GetString());
        Assert.Equal("core", sent.GetProperty("processor").GetString());

        var schema = sent.GetProperty("task_spec").GetProperty("output_schema");
        Assert.Equal("json", schema.GetProperty("type").GetString());
        Assert.Equal("object", schema.GetProperty("json_schema").GetProperty("type").GetString());
    }

    [Fact]
    public async Task StartTask_TextAndAutoSchemasSerialiseAsDocumented()
    {
        var (textClient, textHandler) = MakeClient(QueuedRun);
        await textClient.StartTask("q", TaskSpec.Text("A short paragraph."));
        var text = Sent(textHandler).GetProperty("task_spec").GetProperty("output_schema");
        Assert.Equal("text", text.GetProperty("type").GetString());
        Assert.Equal("A short paragraph.", text.GetProperty("description").GetString());

        var (autoClient, autoHandler) = MakeClient(QueuedRun);
        await autoClient.StartTask("q", TaskSpec.Auto);
        Assert.Equal("auto",
            Sent(autoHandler).GetProperty("task_spec").GetProperty("output_schema").GetProperty("type").GetString());
    }

    /// <summary>No spec at all is legal, and means the same as an auto schema.</summary>
    [Fact]
    public async Task StartTask_WithoutASpecOmitsIt()
    {
        var (client, handler) = MakeClient(QueuedRun);

        await client.StartTask("q");

        var sent = Sent(handler);
        Assert.False(sent.TryGetProperty("task_spec", out _));
        Assert.Equal("base", sent.GetProperty("processor").GetString());   // the default
    }

    /// <summary>A malformed schema fails at the call site, not as a 422 minutes into a run.</summary>
    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2,3]")]
    public void TaskSpec_RejectsANonObjectSchema(string schema) =>
        Assert.Throws<ArgumentException>(() => TaskSpec.Json(schema));

    [Fact]
    public async Task StartTask_ReturnsTheRunWithoutWaiting()
    {
        var (client, _) = MakeClient(QueuedRun);

        var run = await client.StartTask("q");

        Assert.True(run.Success, run.Error);
        Assert.Equal("trun_abc123", run.RunId);
        Assert.Equal("queued", run.Status);
        Assert.True(run.IsActive);
        Assert.False(run.IsCompleted);
        Assert.Equal("int_1", run.InteractionId);
    }

    [Fact]
    public async Task StartTask_SendsSourcePolicyMetadataAndLocation()
    {
        var (client, handler) = MakeClient(QueuedRun);

        await client.StartTask("q", null, new TaskOptions
        {
            Processor = TaskProcessor.Lite,
            IncludeDomains = ["nasa.gov"],
            AfterDate = new DateOnly(2020, 1, 1),
            Location = "US",
            Metadata = new Dictionary<string, string> { ["stage"] = "Concept" },
            EnableEvents = true,
        });

        var sent = Sent(handler);
        Assert.Equal("nasa.gov", sent.GetProperty("source_policy").GetProperty("include_domains")[0].GetString());
        Assert.Equal("2020-01-01", sent.GetProperty("source_policy").GetProperty("after_date").GetString());
        Assert.Equal("US", sent.GetProperty("advanced_settings").GetProperty("location").GetString());
        Assert.Equal("Concept", sent.GetProperty("metadata").GetProperty("stage").GetString());
        Assert.True(sent.GetProperty("enable_events").GetBoolean());
    }

    [Theory]
    [InlineData("a_very_long_metadata_key", "v")]
    [InlineData("k", "")]
    public async Task StartTask_RefusesMetadataBeyondTheDocumentedCaps(string key, string value)
    {
        var (client, handler) = MakeClient(QueuedRun);
        var metadata = new Dictionary<string, string> { [key] = value.Length == 0 ? new string('v', 513) : value };

        var response = await client.StartTask("q", null, new TaskOptions { Metadata = metadata });

        Assert.False(response.Success);
        Assert.Equal(ParallelFailure.InvalidRequest, response.Failure);
        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task StartTask_RefusesAnEmptyInputOrProcessor()
    {
        var (client, handler) = MakeClient(QueuedRun);

        Assert.False((await client.StartTask("   ")).Success);
        Assert.False((await client.StartTask("q", null, new TaskOptions { Processor = " " })).Success);
        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task CheckTask_GetsTheRunWithoutABody()
    {
        var (client, handler) = MakeClient(QueuedRun);

        var run = await client.CheckTask("trun_abc123");

        Assert.Equal(HttpMethod.Get, handler.Request!.Method);
        Assert.Equal("https://api.parallel.ai/v1/tasks/runs/trun_abc123", handler.Request.RequestUri!.ToString());
        Assert.Null(handler.Request.Content);
        Assert.True(run.Success);
    }

    /// <summary>A run id reaches the path escaped, so it cannot alter the URL.</summary>
    [Fact]
    public async Task CheckTask_EscapesTheRunId()
    {
        var (client, handler) = MakeClient(QueuedRun);

        await client.CheckTask("../../v1/tasks/runs");

        Assert.DoesNotContain("../", handler.Request!.RequestUri!.ToString());
    }

    [Fact]
    public async Task CheckTask_ReportsEachTerminalState()
    {
        foreach (var (status, completed, failed, needsAction) in new[]
        {
            ("completed", true, false, false),
            ("failed", false, true, false),
            ("cancelled", false, true, false),
            ("action_required", false, false, true),
        })
        {
            var (client, _) = MakeClient($$"""
                { "run_id": "r", "status": "{{status}}", "is_active": false,
                  "processor": "base", "interaction_id": "i" }
                """);

            var run = await client.CheckTask("r");

            Assert.Equal(completed, run.IsCompleted);
            Assert.Equal(failed, run.IsFailed);
            Assert.Equal(needsAction, run.NeedsAction);
        }
    }

    [Fact]
    public async Task AwaitTask_LongPollsWithTheTimeoutInSeconds()
    {
        var (client, handler) = MakeClient(CompletedJsonResult);

        await client.AwaitTask("trun_abc123", TimeSpan.FromMinutes(2));

        Assert.Equal("https://api.parallel.ai/v1/tasks/runs/trun_abc123/result?timeout=120",
            handler.Request!.RequestUri!.ToString());
    }

    /// <summary>
    /// The per-field basis: a number, the reasoning behind it, the source, and the passage. This is
    /// what Search and Extract cannot give at all, and what a defensible figure needs.
    /// </summary>
    [Fact]
    public async Task AwaitTask_ReadsJsonOutputWithPerFieldProvenance()
    {
        var (client, _) = MakeClient(CompletedJsonResult);

        var result = await client.AwaitTask("trun_abc123");

        Assert.True(result.Success, result.Error);
        Assert.Equal("completed", result.Run!.Status);
        Assert.Null(result.Text);

        Assert.NotNull(result.Json);
        var json = result.Json!.Value;
        Assert.Equal(195.3, json.GetProperty("duration_hours").GetDouble(), 3);

        var basis = result.BasisFor("duration_hours");
        Assert.NotNull(basis);
        Assert.Equal("high", basis.Confidence);
        Assert.Contains("195 hours", basis.Reasoning);

        var citation = Assert.Single(basis.Citations!);
        Assert.Equal("https://www.nasa.gov/apollo-11", citation.Url);
        Assert.Contains("195 hours, 18 minutes", citation.Excerpts![0]);
        Assert.Equal("Apollo 11 - NASA — nasa.gov", citation.Cite());

        // A field with no sources is reported as such rather than omitted.
        Assert.Null(result.BasisFor("mission")!.Citations);
        Assert.Null(result.BasisFor("no_such_field"));
    }

    [Fact]
    public async Task AwaitTask_ReadsTextOutput()
    {
        var (client, _) = MakeClient("""
            {
              "run": { "run_id": "r", "status": "completed", "is_active": false,
                       "processor": "lite", "interaction_id": "i" },
              "output": { "type": "text", "content": "Apollo 11 lasted 195 hours.",
                          "basis": [{ "field": "output", "reasoning": "From the NASA page." }] }
            }
            """);

        var result = await client.AwaitTask("r");

        Assert.Equal("Apollo 11 lasted 195 hours.", result.Text);
        Assert.Null(result.Json);
        Assert.Equal("output", Assert.Single(result.Basis).Field);
    }

    /// <summary>
    /// A run outlasting the wait is normal, not an error: the same id can simply be awaited again.
    /// </summary>
    [Fact]
    public async Task AwaitTask_StillRunningIsRetryable()
    {
        var (client, _) = MakeClient("""{"type":"error","error":{"ref_id":"e","message":"timeout"}}""",
            HttpStatusCode.GatewayTimeout);

        var result = await client.AwaitTask("r", TimeSpan.FromSeconds(30));

        Assert.False(result.Success);
        Assert.Equal(ParallelFailure.Timeout, result.Failure);
        Assert.True(result.Retryable);
    }

    [Fact]
    public async Task AwaitTask_RefusesAnEmptyIdOrNonPositiveWait()
    {
        var (client, handler) = MakeClient(CompletedJsonResult);

        Assert.False((await client.AwaitTask("  ")).Success);
        Assert.False((await client.AwaitTask("r", TimeSpan.Zero)).Success);
        Assert.Null(handler.Request);
    }

    /// <summary>A run's own failure is distinct from a transport failure on the call that read it.</summary>
    [Fact]
    public async Task CheckTask_SurfacesTheRunsOwnError()
    {
        var (client, _) = MakeClient("""
            {
              "run_id": "r", "status": "failed", "is_active": false, "processor": "base",
              "interaction_id": "i",
              "error": { "message": "No sources found", "ref_id": "err_1" }
            }
            """);

        var run = await client.CheckTask("r");

        Assert.True(run.Success);      // the HTTP call worked
        Assert.True(run.IsFailed);     // the run did not
        Assert.Equal("No sources found", run.RunError!.Message);
    }
    #endregion

    #region Live
    /// <summary>
    /// The objective end to end against the real service: search for sources, then extract one of
    /// them under an objective and cite it. Gated behind <c>POLSON_LIVE_PARALLEL_TESTS=1</c> because
    /// both calls bill, and skipped silently when no key is configured.
    /// </summary>
    [Fact]
    public async Task Live_SearchThenExtractProducesSourcedTextWithProvenance()
    {
        var key = config["ApiKeys:Parallel"];
        if (string.IsNullOrWhiteSpace(key)) return;
        if (Environment.GetEnvironmentVariable("POLSON_LIVE_PARALLEL_TESTS") != "1") return;

        using var client = new ParallelClient(key!);

        var search = await client.Search(
            "Establish how much renewable electricity generating capacity was added worldwide in 2024, "
            + "with a figure that can be cited.",
            ["renewable capacity additions 2024", "global renewable capacity 2024 GW"],
            new SearchOptions { MaxResults = 5, Mode = SearchMode.Advanced });

        Assert.True(search.Success, $"{search.Failure}: {search.Error} — {search.Remedy}");
        Assert.NotEmpty(search.Results);
        Info("Live search returned {Count} results in {Ms} ms", search.Results.Count, search.ElapsedMs);
        foreach (var result in search.Results) Info("  {Citation}", result.Cite());

        // At least one real title is the thing the generated binding could never deliver.
        Assert.Contains(search.Results, r => !string.IsNullOrWhiteSpace(r.Title));

        // How much provenance the service actually supplies. Titles are reliable; dates are not, and
        // a caption generator has to cope with that rather than assume both are present.
        // One placeholder per argument: a template naming more than it is given renders nothing at
        // all, which is how the first version of this line silently logged no output.
        Info("Provenance: {Titled} of {Total} carried a title, {Dated} a publish date",
            search.Results.Count(r => !string.IsNullOrWhiteSpace(r.Title)),
            search.Results.Count,
            search.Results.Count(r => r.PublishedOn is not null));

        var extract = await client.Extract(
            search.Results.Take(2).Select(r => r.Url),
            "Find the 2024 global renewable capacity addition figure and the date of publication.",
            new ExtractOptions
            {
                FullContentMaxChars = 20000,
                SessionId = search.SessionId,   // same piece of work as the search above
            });

        Assert.True(extract.Success, $"{extract.Failure}: {extract.Error} — {extract.Remedy}");
        Assert.NotEmpty(extract.Results);

        var page = extract.Results[0];
        Assert.NotEmpty(page.Excerpts);
        Assert.False(string.IsNullOrWhiteSpace(page.FullContent), "full content was requested");
        Info("Live extract: {Citation}, {Chars} chars of full content",
            page.Cite(), page.FullContent!.Length);
    }

    /// <summary>
    /// The start-then-collect pattern end to end, on the cheapest processor that carries a full
    /// basis. Also the measurement that decides the budget question: what <c>base</c> actually costs
    /// in wall-clock, and whether per-field provenance survives at that tier.
    /// </summary>
    [Fact]
    public async Task Live_TaskProducesATableWithPerFieldProvenance()
    {
        var key = config["ApiKeys:Parallel"];
        if (string.IsNullOrWhiteSpace(key)) return;
        if (Environment.GetEnvironmentVariable("POLSON_LIVE_PARALLEL_TESTS") != "1") return;

        using var client = new ParallelClient(key!);

        var schema = """
            {
              "type": "object",
              "properties": {
                "missions": {
                  "type": "array",
                  "description": "One entry per crewed Apollo lunar mission.",
                  "items": {
                    "type": "object",
                    "properties": {
                      "mission":       { "type": "string", "description": "Mission name, e.g. Apollo 11." },
                      "launch_date":   { "type": "string", "description": "Launch date as YYYY-MM-DD." },
                      "duration_hours":{ "type": "number", "description": "Total mission duration in hours." }
                    },
                    "required": ["mission", "launch_date", "duration_hours"]
                  }
                }
              },
              "required": ["missions"]
            }
            """;

        var started = await client.StartTask(
            "Key technical data for the crewed Apollo lunar landing missions.",
            TaskSpec.Json(schema),
            new TaskOptions { Processor = TaskProcessor.Base, Metadata = new Dictionary<string, string> { ["stage"] = "Data" } });

        Assert.True(started.Success, $"{started.Failure}: {started.Error} — {started.Remedy}");
        Info("Task {RunId} queued on {Processor} in {Ms} ms",
            started.RunId, started.Processor, started.ElapsedMs);

        // The Facilitator's move: other work happens here. One cheap status read stands in for it.
        var checkpoint = await client.CheckTask(started.RunId);
        Assert.True(checkpoint.Success, checkpoint.Error);
        Info("  status after start: {Status} (active={Active})", checkpoint.Status, checkpoint.IsActive);

        var result = await client.AwaitTask(started.RunId, TimeSpan.FromMinutes(4));
        Assert.True(result.Success, $"{result.Failure}: {result.Error} — {result.Remedy}");
        Info("Task completed in {Ms} ms total wait; status {Status}",
            result.ElapsedMs, result.Run!.Status);

        Assert.NotNull(result.Json);
        Info("  output: {Json}", result.Json!.Value.ToString());

        Assert.NotEmpty(result.Basis);
        Info("  basis entries: {Count}", result.Basis.Count);
        foreach (var entry in result.Basis)
        {
            Info("   [{Field}] confidence={Confidence} citations={Cites} — {Reasoning}",
                entry.Field, entry.Confidence ?? "-", entry.Citations?.Count ?? 0,
                entry.Reasoning.Length > 120 ? entry.Reasoning[..120] + "…" : entry.Reasoning);
            foreach (var citation in entry.Citations ?? [])
            {
                Info("       {Citation} (excerpts={Ex})", citation.Cite(), citation.Excerpts?.Count ?? 0);
            }
        }
    }

    /// <summary>A bad key must classify as <see cref="ParallelFailure.Auth"/> rather than throwing.</summary>
    [Fact]
    public async Task Live_RejectedKeyClassifiesAsAuth()
    {
        if (Environment.GetEnvironmentVariable("POLSON_LIVE_PARALLEL_TESTS") != "1") return;

        using var client = new ParallelClient("not-a-real-key");

        var response = await client.Search("objective", ["renewable capacity 2024"]);

        Assert.False(response.Success);
        Assert.Equal(ParallelFailure.Auth, response.Failure);
        Assert.False(response.Retryable);
    }
    #endregion

    #region Child Types
    /// <summary>Stands in for the service: records what was sent, and replies however the test says.</summary>
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Request = request;
            return responder(request);
        }
    }
    #endregion
}
