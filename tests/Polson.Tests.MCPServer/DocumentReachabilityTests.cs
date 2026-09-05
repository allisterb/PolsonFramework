namespace Polson.Tests.MCPServer;

using System.Linq;
using System.Text.Json.Nodes;

using Polson.MCPServer;

using Xunit;

/// <summary>
/// The SDK reference is published as MCP <em>resources</em>, and a resource is not reachable the
/// way a tool is. A live infographic run proved the gap end to end: the agent called
/// <c>load_mcp_resource</c> once for the index, received only ADK's status line — "resource
/// contents temporarily inserted and removed. to access these resources, call load_mcp_resource
/// tool again" — and never called again. The mechanism had worked: the body was injected into the
/// following request and then dropped, because it is appended to <c>llm_request.contents</c>
/// rather than to the session's events. Nothing was broken; the content simply reached the model
/// for one turn, twenty minutes before it wrote the code that needed it.
///
/// It then found <c>Chart.createWaffle</c> and thirteen siblings in a <c>Search</c> result that
/// carried names without signatures, could not learn their parameters, and hand-rolled a 10x10
/// grid. Zero <c>Chart.*</c> calls reached the finished piece.
///
/// So these test the two routes that close it: <c>ReadDoc</c>, which serves the same text through
/// a tool whose result persists, and <c>Search</c>, which now resolves the call names it reports
/// into signatures you can write code from.
/// </summary>
public class DocumentReachabilityTests : TestsRuntime
{
    #region Methods
    private static DrawingMcpTools Tools() => new();

    /// <summary>Every URI the resolver advertises must actually serve a body.</summary>
    [Fact]
    public void TestEveryKnownUriResolves()
    {
        var known = PolsonResources.KnownUris();
        Assert.NotEmpty(known);

        var unresolved = known.Where(u => string.IsNullOrWhiteSpace(PolsonResources.Read(u))).ToList();
        Assert.True(unresolved.Count == 0,
            $"KnownUris() advertises {unresolved.Count} URI(s) that Read() does not serve: {string.Join(", ", unresolved)}");
    }

    /// <summary>The area the live run needed, by the URI the Search result cited.</summary>
    [Fact]
    public void TestReadsTheChartArea()
    {
        var body = PolsonResources.Read("polson://sdk/core/Chart");

        Assert.NotNull(body);
        // The names are worthless without the arguments — that was the whole failure.
        Assert.Contains("Chart.createWaffle(rect, parts, options?)", body);
        Assert.Contains("Chart.createColumnChart(rect, data, options?)", body);
    }

    [Theory]
    [InlineData("polson://sdk/index")]
    [InlineData("polson://sdk/core/Chart")]
    // Drawing rather than Chart: the schema document runs Snap..LogoType and has no Chart section,
    // so polson://sdk/schema/Chart is genuinely unpublished and KnownUris() rightly omits it.
    [InlineData("polson://sdk/schema/Drawing")]
    [InlineData("polson://sdk/core/all")]
    [InlineData("polson://sdk/symbols")]
    [InlineData("polson://sdk/symbols/ctx")]
    [InlineData("polson://manual/13")]
    [InlineData("polson://manual/index")]
    public void TestServesUri(string uri) =>
        Assert.False(string.IsNullOrWhiteSpace(PolsonResources.Read(uri)), $"'{uri}' served nothing.");

    /// <summary>
    /// An agent copying a reference out of prose types the bare form, and being pedantic about the
    /// scheme would fail the exact case this tool exists to serve.
    /// </summary>
    [Theory]
    [InlineData("sdk/core/Chart")]
    [InlineData("/sdk/core/Chart/")]
    [InlineData("polson://sdk/core/chart")]
    public void TestAcceptsBareAndUnevenForms(string uri) =>
        Assert.Equal(PolsonResources.Read("polson://sdk/core/Chart"), PolsonResources.Read(uri));

    [Theory]
    [InlineData("manual/13")]
    [InlineData("13")]
    public void TestAcceptsBareManualForms(string uri) =>
        Assert.Equal(PolsonResources.Read("polson://manual/13"), PolsonResources.Read(uri));

    [Theory]
    [InlineData("polson://sdk/core/Charts")]
    [InlineData("polson://sdk/core/NoSuchArea")]
    [InlineData("polson://nonsense")]
    [InlineData("")]
    public void TestUnknownUriServesNothing(string uri) =>
        Assert.Null(PolsonResources.Read(uri));

    /// <summary>
    /// A miss must name what exists. An empty result reads as "the documentation is absent", which
    /// is the answer that makes an agent invent a call.
    /// </summary>
    [Fact]
    public void TestMissNamesTheRealUri()
    {
        var result = Tools().ReadDoc("polson://sdk/core/Charts");

        Assert.False(result["found"]!.GetValue<bool>());
        var nearest = result["nearest"]!.AsArray().Select(n => n!.GetValue<string>()).ToList();
        Assert.Contains("polson://sdk/core/Chart", nearest);
    }

    [Fact]
    public void TestReadDocReturnsTheBodyInTheResult()
    {
        var result = Tools().ReadDoc("polson://sdk/core/Chart");

        Assert.True(result["found"]!.GetValue<bool>());
        var text = result["text"]!.GetValue<string>();
        Assert.Contains("Chart.createWaffle", text);
        Assert.Equal(text.Length, result["length"]!.GetValue<int>());
    }

    /// <summary>
    /// A prose result is a catalogue entry: what the passage covers and where to read it, not the
    /// passage itself. Signatures lived here briefly and were two thirds of the payload — and every
    /// result stays in the conversation and is re-sent on every later turn, so the waste was charged
    /// for the rest of the run rather than once.
    /// </summary>
    [Fact]
    public async System.Threading.Tasks.Task TestProseHitsAreCatalogueEntries()
    {
        var result = await Tools().Search("Scale.linear Layout Chart", k: 5, scope: "sdk");
        var hits = result["results"]!.AsArray();

        Assert.NotEmpty(hits);
        foreach (var hit in hits)
        {
            // Enough to choose with: what it covers, and the URI that fetches it whole.
            Assert.False(string.IsNullOrWhiteSpace(hit!["uri"]!.GetValue<string>()));
            Assert.NotNull(hit["apis"]);
            Assert.NotNull(hit["chars"]);
            Assert.True(hit["text"]!.GetValue<string>().Length <= DrawingMcpTools.SnippetChars + 2);
            Assert.Null(hit["signatures"]);
        }
    }

    /// <summary>
    /// The Apollo failure was a call name with no way to call it. The exact route answers that, and
    /// is what the catalogue's hint points at.
    /// </summary>
    [Fact]
    public async System.Threading.Tasks.Task TestDottedNameResolvesToASignature()
    {
        var result = await Tools().Search("Chart.createColumnChart", scope: "sdk");

        Assert.Equal("direct", result["confidence"]!.GetValue<string>());
        var signatures = result["symbols"]!.AsArray()
            .Select(s => s!["signature"]!.GetValue<string>())
            .ToList();

        Assert.NotEmpty(signatures);
        Assert.Contains(signatures, s => s.Contains("createColumnChart") && s.Contains('('));
    }

    /// <summary>
    /// One hit per document. Chunks are scored individually, so a long document used to win several
    /// of the k slots with near-identical passages: a five-hit search returned three documents, and
    /// the `Chart` reference the query was about never appeared.
    /// </summary>
    [Fact]
    public async System.Threading.Tasks.Task TestResultsAreOnePerDocument()
    {
        var result = await Tools().Search("editorial typography and layout Scale column chart", k: 5, scope: "all");
        var uris = result["results"]!.AsArray().Select(r => r!["uri"]!.GetValue<string>()).ToList();

        Assert.Equal(uris.Count, uris.Distinct(System.StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(uris, u => u.EndsWith("/Chart", System.StringComparison.OrdinalIgnoreCase));
    }
    #endregion
}
