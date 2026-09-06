namespace Polson.Tests.MCPServer;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

using Polson.ExtendedMind.ParallelSearch;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// The <c>Research</c> tool driven by a real MCP client over the HTTP/SSE transport.
/// </summary>
/// <remarks>
/// <para>
/// Everything else about research is covered in-process. What only this can prove is what happens at
/// the boundary: that the tool's schema reaches an agent in the shape intended, that the response
/// envelope survives JSON-RPC serialisation, and — the one that matters most — that a task
/// commissioned by one tool call is visible to a script in a <b>later</b> call on the same session.
/// In-process both calls share an object; over the wire they share only a session id.
/// </para>
/// <para>
/// In the same collection as <see cref="ResearchToolLiveTests"/> because both write the research
/// statics on <see cref="DrawingMcpTools"/> and <see cref="SessionContext"/>, and xUnit runs test
/// classes in parallel by default.
/// </para>
/// </remarks>
[Collection(ResearchCollection.Name)]
public class ResearchTransportTests : TestsRuntime, IAsyncLifetime
{
    #region Fields
    private WebApplication app = null!;
    private string baseUrl = string.Empty;
    private ParallelClient? previousClient;
    #endregion

    #region Lifecycle
    public async Task InitializeAsync()
    {
        previousClient = DrawingMcpTools.ResearchClient;

        // A live client when a key is configured, so the gated test can reach the service; otherwise
        // one whose transport throws, which is what proves a refusal never got that far.
        var key = config["ApiKeys:Parallel"];
        DrawingMcpTools.ResearchClient = string.IsNullOrWhiteSpace(key)
            ? new ParallelClient("unused", new HttpClient(new ExplodingHandler()))
            : new ParallelClient(key!);

        app = PolsonMCPServer.BuildHttpApp(config);
        app.Urls.Clear();
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        baseUrl = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First();
    }

    public async Task DisposeAsync()
    {
        DrawingMcpTools.ResearchClient = previousClient;
        await app.StopAsync();
        await app.DisposeAsync();
    }
    #endregion

    #region Tests

    /// <summary>
    /// The tool as an agent actually sees it. The processor is a cost lever held in configuration, so
    /// its absence from the advertised schema is the thing being asserted — an agent cannot pull what
    /// it is never offered.
    /// </summary>
    [Fact]
    public async Task TestTheAdvertisedSchemaOffersNoProcessor()
    {
        await using var client = await NewClientAsync();

        var tools = await client.ListToolsAsync();
        var research = Assert.Single(tools, t => t.Name == "Research");

        var properties = research.ProtocolTool.InputSchema
            .GetProperty("properties").EnumerateObject()
            .Select(p => p.Name)
            .ToArray();

        Info("Research parameters over the wire: {Names}", string.Join(", ", properties));

        Assert.DoesNotContain("processor", properties);
        foreach (var expected in new[] { "description", "objective", "schema", "waitSeconds", "wait", "runId" })
        {
            Assert.Contains(expected, properties);
        }

        // The directive an agent reads is part of the wire contract, not just a source comment.
        Assert.Contains("YOU GET TWO RUNS", research.Description);
        Assert.Contains("NEVER INVENT A FIGURE", research.Description);
    }

    /// <summary>
    /// A refusal envelope, serialised and returned over JSON-RPC. Needs no key: the schema is rejected
    /// before anything is reached, and the transport would throw if it were not.
    /// </summary>
    [Fact]
    public async Task TestASchemaRefusalRoundTripsOverTheWire()
    {
        await using var client = await NewClientAsync();

        var result = await client.CallToolAsync("Research", new Dictionary<string, object?>
        {
            ["description"] = "asks for nothing",
            ["objective"] = "Anything at all.",
            ["schema"] = """{ "type": "object", "properties": {} }""",
        });

        Assert.True(result.IsError != true, Text(result));

        var payload = JsonNode.Parse(Text(result))!.AsObject();
        Info("Refusal envelope: {Json}", payload.ToJsonString());

        Assert.False(payload["ok"]!.GetValue<bool>());
        Assert.Contains("asks for nothing", payload["error"]!.GetValue<string>());
        Assert.Contains("Nothing was spent", payload["note"]!.GetValue<string>());
        Assert.NotNull(payload["remedy"]);
    }

    /// <summary>
    /// Research commissioned in one call, read by a script in a later one, on the same MCP session.
    /// In-process the two share an object; here they share only a session id over HTTP, which is the
    /// part that could break without anything else noticing.
    /// </summary>
    [Fact]
    public async Task TestResearchCommissionedOverTheWireReachesALaterScript()
    {
        var key = config["ApiKeys:Parallel"];
        if (string.IsNullOrWhiteSpace(key)) return;
        if (Environment.GetEnvironmentVariable("POLSON_LIVE_PARALLEL_TESTS") != "1") return;

        await using var client = await NewClientAsync();

        var commissioned = await client.CallToolAsync("Research", new Dictionary<string, object?>
        {
            ["description"] = "Saturn V figures for the transport test",
            ["objective"] = "The height in metres of the Saturn V launch vehicle, and the year of its "
                          + "first crewed launch. Use NASA sources where possible.",
            ["schema"] = """
                {
                  "type": "object",
                  "properties": {
                    "height_metres": { "type": "number", "description": "Overall height of Saturn V in metres." },
                    "first_crewed_year": { "type": "number", "description": "Year of the first crewed Saturn V launch." }
                  },
                  "required": ["height_metres", "first_crewed_year"]
                }
                """,
            ["waitSeconds"] = 240,
        }, cancellationToken: TestTimeout);

        var payload = JsonNode.Parse(Text(commissioned))!.AsObject();
        Info("Research over the wire: {Json}", payload.ToJsonString());
        Assert.True(payload["ok"]!.GetValue<bool>(),
            $"research did not complete: {payload["status"]} {payload["error"]}");

        var runId = payload["runId"]!.GetValue<string>();

        // A SECOND call on the same client, so the session id is the only thing tying them together.
        var run = await client.CallToolAsync("ExecuteScript", new Dictionary<string, object?>
        {
            ["script"] = """
                const t = Research.latest;
                log('count=' + Research.count + ' id=' + t.id + ' complete=' + t.isComplete);
                log('height=' + t.result.height_metres + ' typeof=' + (typeof t.result.height_metres));
                log('year=' + t.result.first_crewed_year);
                log('cite=' + t.citeField('height_metres'));
                log('remaining=' + Research.budget.remaining);
                """,
            ["render"] = false,
        }, cancellationToken: TestTimeout);

        var text = Text(run);
        Info("Script read back: {Text}", text);

        Assert.True(run.IsError != true, text);
        Assert.Contains($"id={runId}", text);        // the same task, across two JSON-RPC calls
        Assert.Contains("complete=true", text);
        Assert.Contains("typeof=number", text);
        Assert.DoesNotContain("cite=null", text);
        Assert.Contains("remaining=1", text);        // one run spent of the two allowed
    }

    /// <summary>
    /// Research belongs to the session that commissioned it, exactly as <c>Session</c> does. A second
    /// client must not see another's figures — on a shared deployment that would be a leak of one
    /// visitor's paid-for research into another's graphic.
    /// </summary>
    [Fact]
    public async Task TestResearchIsIsolatedBetweenSessions()
    {
        await using var first = await NewClientAsync();
        await using var second = await NewClientAsync();

        // No research is commissioned at all; what matters is that each session starts empty and the
        // globals are safe to read, which is what a script does before deciding whether to draw.
        foreach (var client in new[] { first, second })
        {
            var run = await client.CallToolAsync("ExecuteScript", new Dictionary<string, object?>
            {
                ["script"] = "log('count=' + Research.count + ' latest=' + (Research.latest === null) "
                           + "+ ' remaining=' + Research.budget.remaining + ' all=' + Research.allComplete());",
                ["render"] = false,
            });

            var text = Text(run);
            Assert.True(run.IsError != true, text);
            Assert.Contains("count=0", text);
            Assert.Contains("latest=true", text);
            Assert.Contains($"remaining={SessionContext.ResearchBudget}", text);
            Assert.Contains("all=true", text);       // vacuously true; nothing was commissioned
        }
    }
    #endregion

    #region Methods (private)
    private static CancellationToken TestTimeout => new CancellationTokenSource(TimeSpan.FromMinutes(6)).Token;

    private async Task<McpClient> NewClientAsync()
    {
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions { Endpoint = new Uri(baseUrl) },
            NullLoggerFactory.Instance);
        return await McpClient.CreateAsync(transport);
    }

    private static string Text(CallToolResult result) =>
        string.Concat(result.Content.OfType<TextContentBlock>().Select(c => c.Text));
    #endregion

    #region Child Types
    /// <summary>Fails loudly if a call that should have been refused reaches the network.</summary>
    private sealed class ExplodingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("A refused research call reached the network.");
    }
    #endregion
}

/// <summary>
/// Serialises the research test classes against each other: both write statics that configure the
/// research surface, and xUnit runs classes in parallel unless told otherwise.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ResearchCollection
{
    public const string Name = "Research";
}
