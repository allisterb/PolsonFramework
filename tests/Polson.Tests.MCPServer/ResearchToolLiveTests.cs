namespace Polson.Tests.MCPServer;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using Polson.ExtendedMind.ParallelSearch;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// The <c>Research</c> tool driven end to end against the live service, then read back from a script
/// in the same session — the hand-off the whole design turns on.
/// </summary>
/// <remarks>
/// Gated behind <c>POLSON_LIVE_PARALLEL_TESTS=1</c> because it bills (about $0.01 on <c>base</c>),
/// and skipped silently when no key is configured. Everything else about the tool is covered offline
/// by <c>ParallelClientTests</c> and <c>ResearchTests</c>; what only a live run can prove is that the
/// tool, the session registry, the run record and the JS global agree with each other.
/// </remarks>
[Collection(ResearchCollection.Name)]
public class ResearchToolLiveTests : TestsRuntime, IDisposable
{
    #region Fields
    private readonly string root = Path.Combine(Path.GetTempPath(), "polson-research-" + Guid.NewGuid().ToString("N"));
    #endregion

    #region Constructors
    public ResearchToolLiveTests() => Directory.CreateDirectory(root);

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        GC.SuppressFinalize(this);
    }
    #endregion

    #region Tests

    /// <summary>
    /// Commission, then read the figures from a script. The script must see ordinary JavaScript —
    /// objects it can index and numbers it can format — and a citation for the row it drew.
    /// </summary>
    [Fact]
    public async Task TestResearchReachesAScriptAsOrdinaryJavaScript()
    {
        var key = config["ApiKeys:Parallel"];
        if (string.IsNullOrWhiteSpace(key)) return;
        if (Environment.GetEnvironmentVariable("POLSON_LIVE_PARALLEL_TESTS") != "1") return;

        // One tools instance, so the tool call and the script share a session.
        var tools = new DrawingMcpTools(null, null, null, root) { Parallel = new ParallelClient(key!) };

        var commissioned = await tools.Research(
            description: "Apollo mission durations for the timeline panel",
            objective: "Total mission duration in hours for Apollo 11 and Apollo 17.",
            schema: """
                {
                  "type": "object",
                  "properties": {
                    "apollo_11_hours": { "type": "number", "description": "Apollo 11 total mission duration in hours." },
                    "apollo_17_hours": { "type": "number", "description": "Apollo 17 total mission duration in hours." }
                  },
                  "required": ["apollo_11_hours", "apollo_17_hours"]
                }
                """,
            waitSeconds: 240);

        Info("Research tool answered: {Json}", commissioned.ToJsonString());
        Assert.True(commissioned["ok"]!.GetValue<bool>(),
            $"research did not complete: {commissioned["status"]} {commissioned["error"]}");

        var runId = commissioned["runId"]!.GetValue<string>();
        Assert.NotEmpty(runId);

        // The hand-off: the same session's script sees it, without any network call of its own.
        var run = await tools.ExecuteScript("""
            const t = Research.latest;
            log('count=' + Research.count + ' status=' + t.status + ' complete=' + t.isComplete);
            log('apollo11=' + t.result.apollo_11_hours + ' typeof=' + (typeof t.result.apollo_11_hours));
            log('byId=' + (Research.get(t.id) === t));
            log('find=' + (Research.find('timeline') !== null));
            log('cite=' + t.citeField('apollo_11_hours'));
            log('sources=' + t.sources().length);
            log('basisFields=' + t.basis.map(b => b.field).join(','));
            const c = createCanvas(20, 20); c.getContext('2d').fillRect(0, 0, 20, 20); c;
            """, 20, 20);

        Assert.True(run.Success, run.Error);
        foreach (var line in run.Logs) Info("  {Line}", line);

        var logs = string.Join("\n", run.Logs);
        Assert.Contains("complete=true", logs);
        Assert.Contains("typeof=number", logs);        // a real JS number, not an opaque struct
        Assert.Contains("byId=true", logs);            // Get returns the same instance
        Assert.Contains("find=true", logs);
        Assert.DoesNotContain("cite=null", logs);      // the figure carries a source

        // The run record is what a reader sees afterwards.
        var started = Single("research.started");
        Assert.Equal(runId, started.GetProperty("runId").GetString());
        Assert.Equal("Apollo mission durations for the timeline panel", started.GetProperty("description").GetString());
        Assert.Equal(DrawingMcpTools.ResearchProcessor, started.GetProperty("processor").GetString());

        var completed = Single("research.completed");
        Assert.Equal(runId, completed.GetProperty("runId").GetString());
        Assert.True(completed.GetProperty("fields").GetInt32() > 0);
    }

    /// <summary>
    /// What the service actually puts in a task envelope — asked rather than assumed, because Search
    /// and Extract both report <c>usage</c> and it is worth knowing whether a task does too.
    /// </summary>
    [Fact]
    public async Task TestWhatTheTaskEnvelopeCarries()
    {
        var key = config["ApiKeys:Parallel"];
        if (string.IsNullOrWhiteSpace(key)) return;
        if (Environment.GetEnvironmentVariable("POLSON_LIVE_PARALLEL_TESTS") != "1") return;

        using var client = new ParallelClient(key!);
        var started = await client.StartTask(
            "How many crewed missions landed on the Moon?", TaskSpec.Text("A single number."),
            new TaskOptions { Processor = TaskProcessor.Lite });
        Assert.True(started.Success, started.Error);

        var result = await client.AwaitTask(started.RunId, TimeSpan.FromMinutes(4));
        Assert.True(result.Success, $"{result.Failure}: {result.Error}");

        // Read the raw envelope, since the typed model silently drops anything it does not declare.
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("x-api-key", key);
        var raw = await http.GetStringAsync(
            $"https://api.parallel.ai/v1/tasks/runs/{started.RunId}/result?timeout=60");

        using var document = JsonDocument.Parse(raw);
        var root = document.RootElement;

        Info("TASK RESULT top-level keys: {Keys}",
            string.Join(", ", root.EnumerateObject().Select(p => $"{p.Name}:{p.Value.ValueKind}")));
        Info("  run keys: {Keys}", string.Join(", ",
            root.GetProperty("run").EnumerateObject().Select(p => $"{p.Name}:{p.Value.ValueKind}")));
        Info("  output keys: {Keys}", string.Join(", ",
            root.GetProperty("output").EnumerateObject().Select(p => $"{p.Name}:{p.Value.ValueKind}")));

        var basis = root.GetProperty("output").GetProperty("basis");
        if (basis.GetArrayLength() > 0)
        {
            Info("  basis[0] keys: {Keys}", string.Join(", ",
                basis[0].EnumerateObject().Select(p => $"{p.Name}:{p.Value.ValueKind}")));
        }

        // Whatever the answer, record it as a fact rather than leaving it to memory.
        var hasUsage = root.TryGetProperty("usage", out _)
                    || root.GetProperty("run").TryGetProperty("usage", out _)
                    || root.GetProperty("output").TryGetProperty("usage", out _);
        Info("TASK ENVELOPE REPORTS USAGE: {HasUsage}", hasUsage);
    }

    /// <summary>
    /// The ceiling is enforced before anything is reached, so an exhausted allowance costs nothing.
    /// Offline: no key, no network, and none needed — the refusal happens first.
    /// </summary>
    [Fact]
    public async Task TestAnExhaustedAllowanceRefusesBeforeSpending()
    {
        var previous = SessionContext.ResearchBudget;
        try
        {
            SessionContext.ResearchBudget = 0;

            // A client that would throw if it were ever called, which proves nothing reached it.
            var tools = new DrawingMcpTools(null, null, null, root)
            {
                Parallel = new ParallelClient("unused", new HttpClient(new ExplodingHandler())),
            };

            var refused = await tools.Research(
                description: "figures that will not be sourced",
                objective: "Anything at all.");

            Assert.False(refused["ok"]!.GetValue<bool>());
            Assert.Contains("allowance is spent", refused["error"]!.GetValue<string>());
            Assert.Contains("Do NOT invent", refused["remedy"]!.GetValue<string>());

            var e = Single("research.refused");
            Assert.Equal(0, e.GetProperty("total").GetInt32());
        }
        finally
        {
            SessionContext.ResearchBudget = previous;
        }
    }
    /// <summary>
    /// A schema wider than the configured processor handles is a <b>forecast</b>, not a fault: the
    /// caller cannot answer it by choosing a bigger engine, and simple fields may fit anyway. So it
    /// warns and proceeds — the run reaches the service.
    /// </summary>
    [Fact]
    public void TestAnOversizedSchemaWarnsRatherThanRefusing()
    {
        var properties = string.Join(",", Enumerable.Range(0, 14)
            .Select(i => $"\"f{i}\":{{\"type\":\"string\",\"description\":\"Field {i}.\"}}"));
        var schema = $"{{\"type\":\"object\",\"properties\":{{{properties}}},\"required\":[\"f0\"]}}";

        var report = TaskSchema.Inspect(schema, DrawingMcpTools.ResearchProcessor);

        Assert.True(report.Usable);
        Assert.True(report.OverCapacity);
        Assert.Equal(14, report.FieldCount);
        Assert.Contains(report.Warnings, w => w.Contains("14 top-level fields"));
        Assert.Contains(report.Warnings, w => w.Contains("counts as ONE field"));
    }

    /// <summary>
    /// A structural fault is a fault, and refusing costs nothing — proven by a transport that throws
    /// if anything reaches it.
    /// </summary>
    [Fact]
    public async Task TestAStructurallyBrokenSchemaIsRefusedBeforeSpending()
    {
        var tools = new DrawingMcpTools(null, null, null, root)
        {
            Parallel = new ParallelClient("unused", new HttpClient(new ExplodingHandler())),
        };

        var refused = await tools.Research(
            description: "asks for nothing",
            objective: "Anything.",
            schema: """{ "type": "object", "properties": {} }""");

        Assert.False(refused["ok"]!.GetValue<bool>());
        Assert.Contains("asks for nothing", refused["error"]!.GetValue<string>());
        Assert.Contains("Nothing was spent", refused["note"]!.GetValue<string>());

        // The allowance is untouched, which is what "nothing was spent" has to mean.
        Assert.Equal(SessionContext.ResearchBudget, refused["researchRemaining"]!.GetValue<int>());
        Assert.Single(Events("research.schemaRejected"));
        Assert.Empty(Events("research.started"));
    }

    /// <summary>
    /// The default wait must stay under the request timeout an MCP host imposes — 60 seconds on ADK.
    /// </summary>
    /// <remarks>
    /// Measured on a live run with the old 150-second default: the call died at 60 with a transport
    /// error carrying no run id, so the agent could not name the run it already had and started a
    /// second one. Both completed, so neither refunded, and one question ate the whole allowance.
    /// A slow answer is fine; an answer that never arrives takes the run id with it.
    /// </remarks>
    [Fact]
    public void TestTheDefaultWaitFitsInsideATypicalHostTimeout()
    {
        Assert.True(DrawingMcpTools.DefaultWaitSeconds < 60,
            $"a default wait of {DrawingMcpTools.DefaultWaitSeconds}s does not fit inside a 60s host timeout");
        Assert.True(DrawingMcpTools.DefaultWaitSeconds >= 30,
            "too short to collect a fast run in one call, which makes every research two round trips");
    }

    /// <summary>The processor is not a parameter, so an agent cannot pull the cost lever at all.</summary>
    [Fact]
    public void TestTheToolExposesNoProcessorParameter()
    {
        var parameters = typeof(DrawingMcpTools).GetMethod(nameof(DrawingMcpTools.Research))!
            .GetParameters()
            .Select(p => p.Name)
            .ToArray();

        Assert.DoesNotContain("processor", parameters);
        Assert.Contains("schema", parameters);
    }

    /// <summary>Malformed JSON is caught by the same pass, rather than as a 422 a minute later.</summary>
    [Fact]
    public async Task TestAMalformedSchemaIsRefusedBeforeSpending()
    {
        var tools = new DrawingMcpTools(null, null, null, root)
        {
            Parallel = new ParallelClient("unused", new HttpClient(new ExplodingHandler())),
        };

        var refused = await tools.Research("d", "Anything.", schema: "{ not json");

        Assert.False(refused["ok"]!.GetValue<bool>());
        Assert.Contains("not valid JSON", refused["error"]!.GetValue<string>());
        Assert.Empty(Events("research.started"));
    }
    #endregion

    #region Child Types
    /// <summary>Fails loudly if the budget check ever lets a call through.</summary>
    private sealed class ExplodingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, System.Threading.CancellationToken cancellationToken) =>
            throw new InvalidOperationException("A refused research call reached the network.");
    }
    #endregion

    #region Methods (private)
    private JsonElement[] Events(string type) =>
        File.ReadAllLines(Path.Combine(root, "events", "server.jsonl"))
            .Select(l => JsonDocument.Parse(l).RootElement)
            .Where(e => e.GetProperty("type").GetString() == type)
            .ToArray();

    private JsonElement Single(string type) => Assert.Single(Events(type));
    #endregion
}
